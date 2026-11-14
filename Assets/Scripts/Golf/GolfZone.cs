using UnityEngine;
using UnityEngine.Events;

public enum GolfZoneType { Hole, Water, OutOfBounds }

[RequireComponent(typeof(Collider))]
public class GolfZone : MonoBehaviour
{
    public GolfZoneType type;

    [Header("Hole only")]
    [Tooltip("Capture radius in world units at scale 1. Scales with this object's X scale.")]
    public float captureRadius = 0.15f;
    [Tooltip("Ball faster than this is not captured.")]
    public float maxCaptureSpeed = 2.5f;

    [Header("Feedback")]
    [Tooltip("Fired once when this zone catches the ball.")]
    public UnityEvent onBallCaught;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (type == GolfZoneType.Hole) return;

        var ball = other.GetComponentInParent<GolfBall>();
        if (ball && ball.NotifyHazard(type)) onBallCaught.Invoke();
    }

    void OnTriggerStay(Collider other)
    {
        if (type != GolfZoneType.Hole) return;

        var ball = other.GetComponentInParent<GolfBall>();
        if (!ball || !ball.IsMoving) return;

        Vector3 offset = ball.transform.position - transform.position;
        offset.y = 0f;
        float radius = captureRadius * transform.lossyScale.x;

        if (offset.magnitude <= radius && ball.Speed <= maxCaptureSpeed)
        {
            if (ball.NotifyHoled(transform.position)) onBallCaught.Invoke();
        }
    }
}