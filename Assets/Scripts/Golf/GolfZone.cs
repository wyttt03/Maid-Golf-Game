using UnityEngine;

public enum GolfZoneType { Hole, Water, OutOfBounds }

[RequireComponent(typeof(Collider))]
public class GolfZone : MonoBehaviour
{
    public GolfZoneType type;

    [Header("Hole only")]
    [Tooltip("Capture radius in world units at scale 1. Scales with this object's X scale.")]
    public float captureRadius = 0.15f;

    [Tooltip("Ball faster than this rolls over the hole instead of dropping in.")]
    public float maxCaptureSpeed = 2.5f;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (type == GolfZoneType.Hole) return;

        var ball = other.GetComponentInParent<GolfBall>();
        if (ball) ball.NotifyHazard(type);
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
            ball.NotifyHoled(transform.position);
    }

    void OnDrawGizmos()
    {
        if (type != GolfZoneType.Hole) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, captureRadius * transform.lossyScale.x);
    }
}