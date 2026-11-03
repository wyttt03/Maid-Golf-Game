using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum OpeningType { Hole, Water }
public enum OpeningShape { Circle, Box }

// Marks an area of the table the ball can drop through.
// Place this object's pivot exactly at the table surface height.
public class TableOpening : MonoBehaviour
{
    static readonly List<TableOpening> active = new List<TableOpening>();

    public OpeningType type = OpeningType.Hole;
    public OpeningShape shape = OpeningShape.Circle;

    [Tooltip("Circle radius in meters. Ball drops in when its center is inside.")]
    public float radius = 0.16f;

    [Tooltip("Box size (X, Z) in meters. Follows this object's Y rotation.")]
    public Vector2 size = new Vector2(1f, 1f);

    [Tooltip("Fired once when the ball drops in. Wire animator, audio and particles here.")]
    public UnityEvent onBallCaught;

    void OnEnable() { active.Add(this); }
    void OnDisable() { active.Remove(this); }

    public static TableOpening FindAt(Vector3 point)
    {
        for (int i = 0; i < active.Count; i++)
            if (active[i].Contains(point)) return active[i];
        return null;
    }

    public bool Contains(Vector3 point)
    {
        Vector3 local = Quaternion.Inverse(transform.rotation) * (point - transform.position);
        if (shape == OpeningShape.Circle)
            return local.x * local.x + local.z * local.z <= radius * radius;
        return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = type == OpeningType.Hole ? Color.green : new Color(1f, 0.4f, 0.1f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        if (shape == OpeningShape.Circle) DrawCircle(radius);
        else Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0f, size.y));
    }

    static void DrawCircle(float r)
    {
        const int segments = 32;
        Vector3 prev = new Vector3(r, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            Vector3 next = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}