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

    [Header("Table openings")]
    [Tooltip("Layer used while rolling on the table.")]
    public string normalLayer = "Ball";

    [Tooltip("Layer used above an opening. Must not collide with the Table layer.")]
    public string throughLayer = "BallThrough";

    public bool IsMoving { get; private set; }
    public Vector3 ShotStartPosition { get; private set; }
    public float Speed => rb.linearVelocity.magnitude;

    public event Action Stopped;
    public event Action Holed;
    public event Action<GolfZoneType> HitHazard;

    Rigidbody rb;
    Renderer[] renderers;
    float baseDamping;
    float slowTimer;
    float moveTimer;
    int normalLayerId;
    int throughLayerId;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        baseDamping = rb.linearDamping;
        normalLayerId = LayerMask.NameToLayer(normalLayer);
        throughLayerId = LayerMask.NameToLayer(throughLayer);

        if (normalLayerId < 0 || throughLayerId < 0)
            Debug.LogError("GolfBall: create the Ball and BallThrough layers first.");
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
        if (!rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        IsMoving = false;
    }

    public void ResetTo(Vector3 position)
    {
        rb.isKinematic = false;
        ForceStop();
        ClearSurfaceDamping();
        SetLayer(normalLayerId);
        SetVisible(true);
        rb.position = position;
        transform.position = position;
    }

    public void SetSurfaceDamping(float damping) => rb.linearDamping = damping;
    public void ClearSurfaceDamping() => rb.linearDamping = baseDamping;
    public void ApplySettings(float damping, PhysicsMaterial material)
    {
        baseDamping = damping;
        rb.linearDamping = damping;
        if (material) GetComponent<Collider>().sharedMaterial = material;
    }

    // holeCenter is kept so older GolfZone holes still compile.
    public bool NotifyHoled(Vector3 holeCenter)
    {
        if (!IsMoving) return false;
        ForceStop();
        rb.isKinematic = true;  // freeze inside the cup
        SetVisible(false);      // hidden under the coffee surface
        Holed?.Invoke();
        return true;
    }

    public bool NotifyHazard(GolfZoneType type)
    {
        if (!IsMoving) return false;
        HitHazard?.Invoke(type);
        return true;
    }

    void FixedUpdate()
    {
        if (!IsMoving) return;
        if (CheckOpenings()) return;

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

    // Returns true if the ball was caught by an opening this step.
    bool CheckOpenings()
    {
        TableOpening opening = TableOpening.FindAt(rb.position);
        SetLayer(opening ? throughLayerId : normalLayerId);
        if (!opening) return false;

        // Caught once the ball center has dropped below the table surface.
        if (rb.position.y >= opening.transform.position.y) return false;

        bool caught = opening.type == OpeningType.Hole
            ? NotifyHoled(opening.transform.position)
            : NotifyHazard(GolfZoneType.Water);

        if (caught) opening.onBallCaught.Invoke();
        return caught;
    }

    void SetLayer(int layerId)
    {
        if (layerId >= 0 && gameObject.layer != layerId) gameObject.layer = layerId;
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }
}