using System;
using UnityEngine;
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

    [Header("Shot tuning")]
    public float maxShotSpeed = 8f;
    [Range(0f, 1f)] public float minPower = 0.05f;
    [Tooltip("Power meter sweep speed (0 to 1 per second). Will come from class config later.")]
    public float meterSpeed = 1.2f;
    public float aimLineLength = 1.5f;
    public int maxStrokes = 2;

    public ShotState State { get; private set; }
    public int StrokesUsed { get; private set; }
    public StrokeResult LastResult { get; private set; }

    public event Action<int> StrokeTaken;            // stroke number
    public event Action<StrokeResult> TurnFinished;

    Vector3 aimDir = Vector3.forward;
    Vector3 turnStartPosition;
    float chargeTime;
    float power;

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
    }

    void Start()
    {
        turnStartPosition = ball.transform.position;
        BeginTurn();
    }

    public void BeginTurn()
    {
        StrokesUsed = 0;
        EnterAiming();
    }

    void Update()
    {
        // Debug: restart the whole turn.
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            ball.ResetTo(turnStartPosition);
            BeginTurn();
            return;
        }

        switch (State)
        {
            case ShotState.Aiming:
                UpdateAim();
                if (SpacePressed())
                {
                    chargeTime = 0f;
                    State = ShotState.Charging;
                }
                break;

            case ShotState.Charging:
                // Aim is locked while charging.
                chargeTime += Time.deltaTime;
                power = Mathf.PingPong(chargeTime * meterSpeed, 1f);
                if (powerSlider) powerSlider.value = power;
                if (SpacePressed()) Fire();
                break;
        }
    }

    void UpdateAim()
    {
        if (Mouse.current == null || cam == null) return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane ground = new Plane(Vector3.up, ball.transform.position);

        if (ground.Raycast(ray, out float enter))
        {
            Vector3 dir = ray.GetPoint(enter) - ball.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) aimDir = dir.normalized;
        }

        if (aimLine)
        {
            Vector3 start = ball.transform.position;
            aimLine.SetPosition(0, start);
            aimLine.SetPosition(1, start + aimDir * aimLineLength);
        }
    }

    void Fire()
    {
        float p = Mathf.Max(power, minPower);
        ball.Shoot(aimDir * maxShotSpeed * p);

        StrokesUsed++;
        State = ShotState.BallMoving;
        if (aimLine) aimLine.enabled = false;

        StrokeTaken?.Invoke(StrokesUsed);
    }

    // Ball stopped on a valid surface without holing.
    void OnBallStopped()
    {
        if (State != ShotState.BallMoving) return;

        if (StrokesUsed >= maxStrokes) FinishTurn(StrokeResult.Failed);
        else EnterAiming();
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
        State = ShotState.Aiming;
    }

    void FinishTurn(StrokeResult result)
    {
        LastResult = result;
        State = ShotState.Finished;
        if (aimLine) aimLine.enabled = false;
        Debug.Log($"Turn finished: {result} (strokes used: {StrokesUsed})");
        TurnFinished?.Invoke(result);
    }

    static bool SpacePressed()
    {
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
    }
}