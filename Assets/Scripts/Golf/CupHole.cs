using UnityEngine;

[RequireComponent(typeof(TableOpening))]
public class CupHole : MonoBehaviour
{
    [Tooltip("Cup model that scales with the hole size.")]
    public Transform cupVisual;

    TableOpening opening;
    float baseRadius;
    Vector3 baseScale;

    void Awake()
    {
        opening = GetComponent<TableOpening>();
        baseRadius = opening.radius;
        if (cupVisual) baseScale = cupVisual.localScale;
    }

    // bonus = extra radius in meters on top of the layout's own radius.
    public void SetBonus(float bonus)
    {
        float r = baseRadius + bonus;
        opening.radius = r;

        if (cupVisual)
        {
            float k = r / baseRadius;
            cupVisual.localScale = new Vector3(baseScale.x * k, baseScale.y, baseScale.z * k);
        }
    }
}