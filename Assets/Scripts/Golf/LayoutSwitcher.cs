using UnityEngine;
using UnityEngine.InputSystem;

// Sets up one golf turn: picks the layout for the action and applies the class.
// Layout order must match CombatAction: 0 Attack, 1 Defend, 2 Reflect.
public class LayoutSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Layout
    {
        public string name;
        public GameObject root;
        public Transform teeSpawn;
    }

    public ShotController shot;
    public GolfBall ball;
    public Layout[] layouts;

    [Header("Debug")]
    [Tooltip("Keys 4, 5, 6 pick these classes. Keys 1, 2, 3 pick the action.")]
    public ClassConfig[] testClasses;

    public ClassConfig CurrentClass { get; private set; }
    public CombatAction CurrentAction { get; private set; }
    [Tooltip("Off during real matches. On for testing layouts with keys 1-6.")]
    public bool debugMode = true;
    void Start()
    {
        if (!debugMode) return;
        CurrentClass = testClasses.Length > 0 ? testClasses[0] : null;
        SetupTurn(CurrentClass, CombatAction.Attack);
    }

    void Update()
    {
        if (!debugMode) return;
        // ...keep the rest of Update unchanged
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit1Key.wasPressedThisFrame) SetupTurn(CurrentClass, CombatAction.Attack);
        if (kb.digit2Key.wasPressedThisFrame) SetupTurn(CurrentClass, CombatAction.Defend);
        if (kb.digit3Key.wasPressedThisFrame) SetupTurn(CurrentClass, CombatAction.Reflect);

        if (kb.digit4Key.wasPressedThisFrame) PickTestClass(0);
        if (kb.digit5Key.wasPressedThisFrame) PickTestClass(1);
        if (kb.digit6Key.wasPressedThisFrame) PickTestClass(2);
    }

    void PickTestClass(int i)
    {
        if (i < testClasses.Length) SetupTurn(testClasses[i], CurrentAction);
    }

    public void SetupTurn(ClassConfig cls, CombatAction action)
    {
        int index = (int)action;
        if (index >= layouts.Length) return;

        CurrentClass = cls;
        CurrentAction = action;

        // Disable all first so re-enabling resets animators (cup goes back down).
        foreach (var l in layouts)
            if (l.root) l.root.SetActive(false);

        var layout = layouts[index];
        layout.root.SetActive(true);

        bool signature = cls && cls.signatureAction == action;

        var cup = layout.root.GetComponentInChildren<CupHole>();
        if (cup) cup.SetBonus(signature ? cls.signatureHoleBonus : 0f);

        if (cls)
        {
            shot.maxShotSpeed = cls.maxShotSpeed;
            shot.minPower = cls.minPower;
            shot.meterSpeed = cls.meterSpeed * (signature ? cls.signatureMeterMultiplier : 1f);
            ball.ApplySettings(cls.ballDamping, cls.ballMaterial);
        }

        shot.StartTurnAt(layout.teeSpawn.position);
        Debug.Log($"Turn setup: {(cls ? cls.displayName : "No class")} / {action} / signature: {signature}");
    }
}