using UnityEngine;

public enum ControlMode { Aim, MoveCup, RotateBoard }
public enum CupMoveShape { Area, Rail }

// Put on a layout root to change how the shot is controlled on that course.
// A layout without this component plays as normal golf (free aim).
public class CourseControls : MonoBehaviour
{
    public ControlMode mode = ControlMode.Aim;

    [Header("Fixed shot direction (MoveCup and RotateBoard)")]
    [Tooltip("The shot follows this object's blue arrow (forward). Usually the TeeSpawn. Rotate it in the Scene view.")]
    public Transform directionFrom;
    [Tooltip("Used only when Direction From is empty.")]
    public Vector3 shotDirection = Vector3.right;

    [Header("Rules")]
    [Tooltip("Second stroke is played again from the tee.")]
    public bool retryFromTee = true;
    [Tooltip("The aim line shows at least this many bounces on this course.")]
    public int minAimBounces = 0;
    [Tooltip("Control hint shown on the golf HUD.")]
    public string hint = "";

    [Header("MoveCup")]
    [Tooltip("The CupHole root object.")]
    public Transform cup;
    [Tooltip("Area: the cup moves freely inside a rectangle. Rail: the cup slides along a line.")]
    public CupMoveShape cupMoveShape = CupMoveShape.Area;
    [Tooltip("Area: one corner. Rail: one end.")]
    public Transform railStart;
    [Tooltip("Area: the opposite corner. Rail: the other end.")]
    public Transform railEnd;
    [Tooltip("Let the player keep moving the cup while the ball rolls.")]
    public bool cupMovesDuringRoll = false;

    [Header("RotateBoard")]
    [Tooltip("Bounce board. Its thin side must be the local Z axis.")]
    public Transform board;
    [Tooltip("Degrees the board can turn to each side of its starting rotation.")]
    public float boardAngleRange = 30f;

    Vector3 cupStart;
    Quaternion boardStart;

    void Awake()
    {
        if (cup) cupStart = cup.position;
        if (board) boardStart = board.rotation;
    }

    // Layout roots are re-enabled every turn, so this resets the course.
    void OnEnable()
    {
        if (cup) cup.position = cupStart;
        if (board) board.rotation = boardStart;
    }

    public Vector3 FlatDirection
    {
        get
        {
            Vector3 d = directionFrom ? directionFrom.forward : shotDirection;
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.right;
        }
    }

    // Moves the cup toward the mouse, kept inside the area or on the rail.
    public void MoveCupToward(Vector3 point)
    {
        if (!cup || !railStart || !railEnd) return;

        Vector3 a = railStart.position;
        Vector3 b = railEnd.position;
        Vector3 p;

        if (cupMoveShape == CupMoveShape.Area)
        {
            p = new Vector3(
                Mathf.Clamp(point.x, Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x)),
                cup.position.y,
                Mathf.Clamp(point.z, Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z)));
        }
        else
        {
            Vector3 ab = b - a;
            if (ab.sqrMagnitude < 0.0001f) return;
            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            p = a + ab * t;
            p.y = cup.position.y;
        }

        cup.position = p;
    }

    // Turns the board so a ball hitting its center bounces toward the mouse.
    public void AimBoardAt(Vector3 point)
    {
        if (!board) return;

        Vector3 outDir = point - board.position;
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 0.0001f) return;

        Vector3 normal = outDir.normalized - FlatDirection;
        if (normal.sqrMagnitude < 0.0001f) return;

        float yaw = Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
        float baseYaw = boardStart.eulerAngles.y;
        float delta = Mathf.DeltaAngle(baseYaw, yaw);

        // Both faces of the board bounce the same way.
        if (delta > 90f) delta -= 180f;
        else if (delta < -90f) delta += 180f;

        delta = Mathf.Clamp(delta, -boardAngleRange, boardAngleRange);
        board.rotation = Quaternion.Euler(0f, baseYaw + delta, 0f);
        Physics.SyncTransforms();
    }

    void OnDrawGizmos()
    {
        if (mode == ControlMode.Aim) return;

        // Yellow arrow: fixed shot direction.
        Vector3 start = directionFrom ? directionFrom.position : transform.position;
        Vector3 dir = FlatDirection;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, start + dir * 2f);
        Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.15f;
        Gizmos.DrawLine(start + dir * 2f, start + dir * 1.75f + side);
        Gizmos.DrawLine(start + dir * 2f, start + dir * 1.75f - side);

        // Cyan: where the cup can move.
        if (mode == ControlMode.MoveCup && railStart && railEnd)
        {
            Gizmos.color = Color.cyan;
            Vector3 a = railStart.position;
            Vector3 b = railEnd.position;
            if (cupMoveShape == CupMoveShape.Area)
            {
                Vector3 center = (a + b) * 0.5f;
                Vector3 size = new Vector3(Mathf.Abs(a.x - b.x), 0.01f, Mathf.Abs(a.z - b.z));
                Gizmos.DrawWireCube(center, size);
            }
            else
            {
                Gizmos.DrawLine(a, b);
            }
        }
    }
}