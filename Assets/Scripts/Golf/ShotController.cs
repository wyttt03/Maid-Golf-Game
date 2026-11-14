using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum ShotState { Aiming, Charging, BallMoving, Finished }

public class ShotController : MonoBehaviour
{
    [Header("References")]
    public GolfBall ball;
    public Camera cam;
    public LineRenderer aimLine;
    public Slider powerSlider;

    [Header("Club (set per class by LayoutSwitcher)")]
    [Tooltip("Roll distance at full power, in meters. The aim line shows this length.")]
    public float maxDistance = 7f;
    [Tooltip("Ball linear damping, used to turn distance into launch speed.")]
    public float ballDamping = 0.6f;
    [Tooltip("Wall bounces shown on the aim line. 0 = line stops at the first wall.")]
    public int aimBounces = 0;
    [Range(0f, 1f)] public float minPower = 0.05f;
    [Tooltip("Power meter sweep speed (0 to 1 per second).")]
    public float meterSpeed = 1.2f;

    [Header("Distance calibration")]
    [Tooltip("Keep at 1 with a frictionless ball. Raise if the ball stops short of the line, lower if it goes past.")]
    public float distanceCalibration = 1f;

    [Header("Aim line")]
    [Tooltip("Layers the aim line stops at or bounces off: walls and obstacles only.")]
    public LayerMask aimBlockers = 1; // Default layer
    [Tooltip("While charging, the line shrinks to the distance of the current power.")]
    public bool lineFollowsPower = true;
    public float ballRadius = 0.1f;
    [Tooltip("Aim line color while adjusting.")]
    public Color aimingColor = Color.white;
    [Tooltip("Aim line color once confirmed with left click.")]
    public Color lockedColor = new Color(1f, 0.8f, 0.2f);

    [Header("Previous shot hint (second stroke)")]
    [Tooltip("Faint line showing the last shot's predicted path at its real power. Optional.")]
    public LineRenderer ghostLine;
    [Tooltip("Small marker on the power bar at the last shot's power. Optional.")]
    public RectTransform powerMarker;

    [Header("Rules")]
    public int maxStrokes = 2;
    [Tooltip("Hint shown on courses without their own hint (normal golf).")]
    public string defaultHint = "Mouse: aim    Left click: confirm    Space: shoot    Right click: cancel";

    [Header("Sound hooks")]
    public UnityEvent onConfirm;
    public UnityEvent onChargeStart;
    public UnityEvent onShot;

    public ShotState State { get; private set; }
    public int StrokesUsed { get; private set; }
    public StrokeResult LastResult { get; private set; }

    // Launch speed for a given roll distance. With no friction, the ball slows only by
    // linear damping, so distance = (speed - stopSpeed) / damping.
    // Physics applies damping once per physics step, which adds (1 + dt * damping) to the roll.
    // Dividing it out makes the aim line length match the real roll distance.
    public float SpeedForDistance(float distance) =>
        distance * ballDamping * distanceCalibration / (1f + Time.fixedDeltaTime * ballDamping) + ball.stopSpeed;

    public float MaxShotSpeed => SpeedForDistance(maxDistance);

    public string ControlHint =>
        course && !string.IsNullOrEmpty(course.hint) ? course.hint : defaultHint;

    public event Action<int> StrokeTaken;            // stroke number
    public event Action<StrokeResult> TurnFinished;

    const int MaxPathPoints = 8;
    readonly Vector3[] pathPoints = new Vector3[MaxPathPoints];
    readonly Vector3[] lastPathPoints = new Vector3[MaxPathPoints];

    CourseControls course; // null = normal golf
    Vector3 aimDir = Vector3.right;
    Vector3 turnStartPosition;
    float chargeTime;
    float power;

    // Previous shot in this turn, used for the second-stroke hint.
    bool hasLastShot;
    float lastPower;
    int lastPathCount;

    ControlMode Mode => course ? course.mode : ControlMode.Aim;

    void OnEnable()
    {
        ball.Stopped += OnBallStopped;
        ball.Holed += OnBallHoled;
        ball.HitHazard += OnBallHazard;
    }

    void OnDisable()
    {
        ball.Stopped -= OnBallStopped;
        ball.Holed -= OnBallHoled;
        ball.HitHazard -= OnBallHazard;
        ShowLastShotHint(false);
    }

    void Start()
    {
        turnStartPosition = ball.transform.position;
        BeginTurn();
    }

    // Call before StartTurnAt. Null = normal golf.
    public void SetCourse(CourseControls controls)
    {
        course = controls;
        Debug.Log($"Shot controls: {(course ? course.mode + " on " + course.name : "Aim (no CourseControls found)")}");
    }

    public void BeginTurn()
    {
        StrokesUsed = 0;
        hasLastShot = false;
        EnterAiming();
    }

    public void StartTurnAt(Vector3 position)
    {
        turnStartPosition = position;
        ball.ResetTo(position);
        BeginTurn();
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // Debug: restart the whole turn.
        if (kb != null && kb.rKey.wasPressedThisFrame)
        {
            ball.ResetTo(turnStartPosition);
            BeginTurn();
            return;
        }

        switch (State)
        {
            case ShotState.Aiming:
                // Move the mouse to adjust. Left click confirms and starts the power meter.
                UpdateControls();
                DrawAimPath(maxDistance);
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    chargeTime = 0f;
                    State = ShotState.Charging;
                    SetLineColor(lockedColor);
                    onConfirm.Invoke();
                    onChargeStart.Invoke();
                }
                break;

            case ShotState.Charging:
                // Right click cancels and goes back to adjusting.
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                {
                    power = 0f;
                    if (powerSlider) powerSlider.value = 0f;
                    State = ShotState.Aiming;
                    SetLineColor(aimingColor);
                    break;
                }

                // One press of Space sets the power and shoots.
                chargeTime += Time.deltaTime;
                power = Mathf.PingPong(chargeTime * meterSpeed, 1f);
                if (powerSlider) powerSlider.value = power;
                if (lineFollowsPower) DrawAimPath(maxDistance * Mathf.Max(power, minPower));
                if (SpacePressed()) Fire();
                break;

            case ShotState.BallMoving:
                if (Mode == ControlMode.MoveCup && course.cupMovesDuringRoll &&
                    TryGetMousePoint(out Vector3 catchPoint))
                    course.MoveCupToward(catchPoint);
                break;
        }
    }

    // Attack: mouse aims the shot. Defense: mouse moves the cup. Reflect: mouse turns the board.
    void UpdateControls()
    {
        ControlMode mode = Mode;
        if (mode != ControlMode.Aim) aimDir = course.FlatDirection;

        if (!TryGetMousePoint(out Vector3 point)) return;

        switch (mode)
        {
            case ControlMode.Aim:
                Vector3 dir = point - ball.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) aimDir = dir.normalized;
                break;
            case ControlMode.MoveCup:
                course.MoveCupToward(point);
                break;
            case ControlMode.RotateBoard:
                course.AimBoardAt(point);
                break;
        }
    }

    bool TryGetMousePoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (Mouse.current == null || cam == null) return false;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, ball.transform.position);
        if (!ground.Raycast(ray, out float enter)) return false;

        point = ray.GetPoint(enter);
        return true;
    }

    void DrawAimPath(float length)
    {
        if (!aimLine) return;

        int count = ComputePath(ball.transform.position, aimDir, length, pathPoints);
        aimLine.positionCount = count;
        for (int i = 0; i < count; i++) aimLine.SetPosition(i, pathPoints[i]);
    }

    // Predicts the path into points and returns how many were used. Each bounce uses the real
    // bounciness of the ball and the wall, so both the angle and the shorter distance after the
    // bounce match the physics.
    int ComputePath(Vector3 origin, Vector3 dir, float length, Vector3[] points)
    {
        float remaining = length;
        int wanted = Mathf.Max(aimBounces, course ? course.minAimBounces : 0);
        int bounces = Mathf.Clamp(wanted, 0, MaxPathPoints - 2);
        var ballCollider = ball.GetComponent<Collider>();
        PhysicsMaterial ballMat = ballCollider ? ballCollider.sharedMaterial : null;

        int count = 0;
        points[count++] = origin;

        for (int i = 0; i <= bounces && remaining > 0.01f; i++)
        {
            bool hitSomething = Physics.SphereCast(origin, ballRadius * 0.95f, dir, out RaycastHit hit,
                remaining, aimBlockers, QueryTriggerInteraction.Ignore);

            if (!hitSomething)
            {
                points[count++] = origin + dir * remaining;
                break;
            }

            // Ball center at the moment of impact.
            Vector3 contact = origin + dir * hit.distance;
            points[count++] = contact;

            if (i == bounces) break; // no more bounces to show: stop at the wall

            Vector3 normal = hit.normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < 0.0001f) break;
            normal.Normalize();

            // Bounce: the part along the wall is kept, the part into the wall is reversed and scaled.
            float e = CombinedBounce(ballMat, hit.collider.sharedMaterial);
            Vector3 intoWall = Vector3.Dot(dir, normal) * normal;
            Vector3 alongWall = dir - intoWall;
            Vector3 after = alongWall - intoWall * e;
            after.y = 0f;
            float speedLeft = after.magnitude;
            if (speedLeft < 0.01f) break;

            // With linear damping, remaining distance scales with remaining speed.
            remaining = (remaining - hit.distance) * speedLeft;
            dir = after / speedLeft;
            origin = contact + normal * 0.01f; // step off the wall so the next cast starts clear
        }

        return count;
    }

    // Same rule Unity uses: Maximum beats Multiply beats Minimum beats Average.
    static float CombinedBounce(PhysicsMaterial a, PhysicsMaterial b)
    {
        float ba = a ? a.bounciness : 0f;
        float bb = b ? b.bounciness : 0f;
        var ca = a ? a.bounceCombine : PhysicsMaterialCombine.Average;
        var cb = b ? b.bounceCombine : PhysicsMaterialCombine.Average;

        if (ca == PhysicsMaterialCombine.Maximum || cb == PhysicsMaterialCombine.Maximum) return Mathf.Max(ba, bb);
        if (ca == PhysicsMaterialCombine.Multiply || cb == PhysicsMaterialCombine.Multiply) return ba * bb;
        if (ca == PhysicsMaterialCombine.Minimum || cb == PhysicsMaterialCombine.Minimum) return Mathf.Min(ba, bb);
        return (ba + bb) * 0.5f;
    }

    void SetLineColor(Color c)
    {
        if (!aimLine) return;
        aimLine.startColor = c;
        aimLine.endColor = c;
    }

    // Shows or hides the faint path and power marker of the previous shot.
    void ShowLastShotHint(bool show)
    {
        if (ghostLine)
        {
            ghostLine.enabled = show;
            if (show)
            {
                ghostLine.positionCount = lastPathCount;
                for (int i = 0; i < lastPathCount; i++) ghostLine.SetPosition(i, lastPathPoints[i]);
            }
        }

        if (powerMarker)
        {
            powerMarker.gameObject.SetActive(show);
            if (show)
            {
                powerMarker.anchorMin = new Vector2(lastPower, powerMarker.anchorMin.y);
                powerMarker.anchorMax = new Vector2(lastPower, powerMarker.anchorMax.y);
                powerMarker.anchoredPosition = new Vector2(0f, powerMarker.anchoredPosition.y);
            }
        }
    }

    void Fire()
    {
        float p = Mathf.Max(power, minPower);

        // Remember this shot for the second-stroke hint.
        lastPower = p;
        lastPathCount = ComputePath(ball.transform.position, aimDir, maxDistance * p, lastPathPoints);
        hasLastShot = true;

        ball.Shoot(aimDir * SpeedForDistance(maxDistance * p));
        onShot.Invoke();

        StrokesUsed++;
        State = ShotState.BallMoving;
        if (aimLine) aimLine.enabled = false;
        ShowLastShotHint(false);

        StrokeTaken?.Invoke(StrokesUsed);
    }

    // Ball stopped on a valid surface without holing.
    void OnBallStopped()
    {
        if (State != ShotState.BallMoving) return;

        if (StrokesUsed >= maxStrokes)
        {
            FinishTurn(StrokeResult.Failed);
            return;
        }

        // Fixed-direction courses replay the second stroke from the tee.
        if (course && course.mode != ControlMode.Aim && course.retryFromTee)
            ball.ResetTo(turnStartPosition);

        EnterAiming();
    }

    void OnBallHoled()
    {
        if (State != ShotState.BallMoving) return;

        var result = StrokesUsed == 1 ? StrokeResult.HoleInOne : StrokeResult.TwoStrokes;
        FinishTurn(result);
    }

    // Water or out of bounds: stroke is consumed, ball returns to where this stroke started.
    void OnBallHazard(GolfZoneType type)
    {
        if (State != ShotState.BallMoving) return;

        Debug.Log($"Hazard: {type} on stroke {StrokesUsed}");
        ball.ResetTo(ball.ShotStartPosition);

        if (StrokesUsed >= maxStrokes) FinishTurn(StrokeResult.Failed);
        else EnterAiming();
    }

    void EnterAiming()
    {
        power = 0f;
        if (powerSlider) powerSlider.value = 0f;
        if (aimLine) aimLine.enabled = true;
        SetLineColor(aimingColor);
        ShowLastShotHint(hasLastShot);
        State = ShotState.Aiming;
    }

    void FinishTurn(StrokeResult result)
    {
        LastResult = result;
        State = ShotState.Finished;
        if (aimLine) aimLine.enabled = false;
        ShowLastShotHint(false);
        Debug.Log($"Turn finished: {result} (strokes used: {StrokesUsed})");
        TurnFinished?.Invoke(result);
    }

    static bool SpacePressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }
}