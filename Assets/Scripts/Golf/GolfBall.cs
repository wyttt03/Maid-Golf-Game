using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class GolfBall : MonoBehaviour
{
    [Tooltip("Below this speed the ball counts as slow.")]
    public float stopSpeed = 0.08f;

    [Tooltip("How long the ball must stay slow before it is stopped.")]
    public float stopDelay = 0.3f;

    [Tooltip("Safety limit to keep the pace fast. Ball is force-stopped after this time.")]
    public float maxMoveTime = 8f;

    public bool IsMoving { get; private set; }
    public Vector3 ShotStartPosition { get; private set; }
    public float Speed => rb.linearVelocity.magnitude;

    public event Action Stopped;
    public event Action Holed;
    public event Action<GolfZoneType> HitHazard;

    Rigidbody rb;
    float baseDamping;
    float slowTimer;
    float moveTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        baseDamping = rb.linearDamping;
    }

    public void Shoot(Vector3 velocity)
    {
        ShotStartPosition = transform.position;
        rb.WakeUp();
        rb.AddForce(velocity, ForceMode.VelocityChange); // mass-independent
        IsMoving = true;
        slowTimer = 0f;
        moveTimer = 0f;
    }

    public void ForceStop()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        IsMoving = false;
    }

    public void ResetTo(Vector3 position)
    {
        ForceStop();
        ClearSurfaceDamping();
        rb.position = position;
        transform.position = position;
    }

    public void SetSurfaceDamping(float damping) => rb.linearDamping = damping;
    public void ClearSurfaceDamping() => rb.linearDamping = baseDamping;

    public void NotifyHoled(Vector3 holeCenter)
    {
        if (!IsMoving) return;
        Vector3 p = holeCenter;
        p.y = transform.position.y;
        ResetTo(p);
        Holed?.Invoke();
    }

    public void NotifyHazard(GolfZoneType type)
    {
        if (!IsMoving) return;
        HitHazard?.Invoke(type);
    }

    void FixedUpdate()
    {
        if (!IsMoving) return;

        moveTimer += Time.fixedDeltaTime;

        if (rb.linearVelocity.magnitude < stopSpeed)
            slowTimer += Time.fixedDeltaTime;
        else
            slowTimer = 0f;

        // Skip the first physics steps so the shot impulse has applied.
        bool settled = moveTimer > Time.fixedDeltaTime * 2f && slowTimer >= stopDelay;

        if (settled || moveTimer >= maxMoveTime)
        {
            ForceStop();
            Stopped?.Invoke();
        }
    }
}