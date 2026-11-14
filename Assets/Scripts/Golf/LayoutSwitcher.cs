using UnityEngine;
using UnityEngine.InputSystem;

// Sets up one golf turn: picks a layout for the action and applies the class.
// Signature action -> random layout from the easy pool, otherwise from the hard pool.
public class LayoutSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Layout
    {
        public string name;
        public GameObject root;
        public Transform teeSpawn;
    }

    [System.Serializable]
    public class ActionLayouts
    {
        public string name;
        public Layout[] easy;
        public Layout[] hard;
    }

    public ShotController shot;
    public GolfBall ball;
    [Tooltip("Order must match CombatAction: 0 Attack, 1 Defend, 2 Reflect.")]
    public ActionLayouts[] actions;

    [Header("Tutorial (order: 0 Attack, 1 Defend, 2 Reflect)")]
    public Layout[] tutorialLayouts;

    [Header("Debug")]
    [Tooltip("Off during real matches. On for testing: 1-3 action, 4-6 class.")]
    public bool debugMode = false;
    public ClassConfig[] testClasses;

    public ClassConfig CurrentClass { get; private set; }
    public CombatAction CurrentAction { get; private set; }
    public string CurrentLayoutName { get; private set; }
    public bool CurrentIsEasy { get; private set; }
    public bool CurrentFlipped { get; private set; }

    static readonly Quaternion FlippedRotation = Quaternion.Euler(0f, 180f, 0f);

    void Start()
    {
        if (!debugMode) return;
        CurrentClass = testClasses.Length > 0 ? testClasses[0] : null;
        SetupTurn(CurrentClass, CombatAction.Attack);
    }

    void Update()
    {
        if (!debugMode) return;
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

    public void SetupTurn(ClassConfig cls, CombatAction action, bool flip = false)
    {
        int index = (int)action;
        if (actions == null || index >= actions.Length) return;

        bool signature = cls && cls.signatureAction == action;
        var set = actions[index];

        // Use the matching pool; fall back to the other one if it is empty.
        Layout[] pool = signature ? set.easy : set.hard;
        if (pool == null || pool.Length == 0) pool = signature ? set.hard : set.easy;
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"No layouts set up for {action}.");
            return;
        }

        CurrentIsEasy = pool == set.easy;
        ApplyLayout(pool[Random.Range(0, pool.Length)], cls, action, flip);
    }

    // Practice mode: one simple layout per action.
    public void SetupTutorial(ClassConfig cls, CombatAction action)
    {
        int i = (int)action;
        if (tutorialLayouts == null || i >= tutorialLayouts.Length ||
            tutorialLayouts[i] == null || !tutorialLayouts[i].root)
        {
            Debug.LogWarning($"No tutorial layout set up for {action}.");
            return;
        }

        CurrentIsEasy = true;
        ApplyLayout(tutorialLayouts[i], cls, action, false);
    }

    public void HideAllLayouts()
    {
        DisableAll();
    }

    void ApplyLayout(Layout layout, ClassConfig cls, CombatAction action, bool flip)
    {
        CurrentClass = cls;
        CurrentAction = action;
        CurrentFlipped = flip;
        CurrentLayoutName = layout.name;
        bool signature = cls && cls.signatureAction == action;

        // Disable all first so re-enabling resets animators (cup goes back down).
        DisableAll();
        layout.root.transform.localRotation = flip ? FlippedRotation : Quaternion.identity;
        layout.root.SetActive(true);
        Physics.SyncTransforms();

        var cup = layout.root.GetComponentInChildren<CupHole>();
        if (cup) cup.SetBonus(signature && cls ? cls.signatureHoleBonus : 0f); // bigger cup on your class's action

        if (cls)
        {
            shot.maxDistance = cls.maxDistance;
            shot.ballDamping = cls.ballDamping;
            shot.aimBounces = cls.aimBounces;
            shot.minPower = cls.minPower;
            shot.meterSpeed = cls.meterSpeed * (signature ? cls.signatureMeterMultiplier : 1f);
            ball.ApplySettings(cls.ballDamping, cls.ballMaterial);
        }

        shot.SetCourse(layout.root.GetComponent<CourseControls>());
        shot.StartTurnAt(layout.teeSpawn.position);
        Debug.Log($"Turn setup: {(cls ? cls.displayName : "No class")} / {action} / {layout.name}");
    }

    void DisableAll()
    {
        if (actions != null)
            foreach (var set in actions)
            {
                Disable(set.easy);
                Disable(set.hard);
            }
        Disable(tutorialLayouts);
    }

    static void Disable(Layout[] list)
    {
        if (list == null) return;
        foreach (var l in list)
            if (l != null && l.root) l.root.SetActive(false);
    }
}