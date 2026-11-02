using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SurfaceZone : MonoBehaviour
{
    [Tooltip("Ball linear damping while inside this zone.")]
    public float linearDamping = 3f;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    // Stay (not Enter) so it still works if the ball is reset inside the zone.
    void OnTriggerStay(Collider other)
    {
        var ball = other.GetComponentInParent<GolfBall>();
        if (ball) ball.SetSurfaceDamping(linearDamping);
    }

    void OnTriggerExit(Collider other)
    {
        var ball = other.GetComponentInParent<GolfBall>();
        if (ball) ball.ClearSurfaceDamping();
    }
}