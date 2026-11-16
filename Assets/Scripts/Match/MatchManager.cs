using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum MatchPhase { ClassSelect, ActionSelect, Reveal, PreGolf, Golf, TurnResult, CombatAnim, RoundResult, MatchEnd, Tutorial }
public enum WinReason { Draw, HP, Holes, Strokes }

public class MatchManager : MonoBehaviour
{
    public class PlayerState
    {
        public ClassConfig cls;
        public int hp;
        public CombatAction action;
        public StrokeResult result;
        public int strokesUsed;
        public readonly List<StrokeResult> results = new List<StrokeResult>();

        public int Holes
        {
            get
            {
                int n = 0;
                foreach (var r in results) if (r != StrokeResult.Failed) n++;
                return n;
            }
        }

        // Hole-in-one = 1, two strokes = 2, failed = 3 (penalty).
        public int ScoreStrokes
        {
            get
            {
                int s = 0;
                foreach (var r in results)
                    s += r == StrokeResult.HoleInOne ? 1 : r == StrokeResult.TwoStrokes ? 2 : 3;
                return s;
            }
        }
    }

    [Header("References")]
    public CombatConfig combatConfig;
    [Tooltip("Order: 0 Attacker, 1 Defender, 2 Reflector.")]
    public ClassConfig[] classes;
    public LayoutSwitcher switcher;
    public ShotController shot;
    [Tooltip("Optional. Plays the chibi battle animations.")]
    public BattleStage stage;

    [Header("Rules")]
    public int maxRounds = 3;
    [Tooltip("Pause after a golf turn so the result and cup rise can be seen.")]
    public float afterTurnDelay = 1.8f;
    [Tooltip("Pause after a practice try before it restarts.")]
    public float tutorialRetryDelay = 1.5f;
    public string menuSceneName = "MainMenu";

    [Header("Feedback hooks (wire audio, particles, UI here)")]
    public UnityEvent onMatchStarted;  // both classes confirmed, match begins
    public UnityEvent onRoundStarted;
    public UnityEvent onActionsLocked; // fired when both actions are revealed
    public UnityEvent onTurnStarted;   // a player's golf turn is about to start
    public UnityEvent onHoleInOne;
    public UnityEvent onTwoStrokes;
    public UnityEvent onFailed;
    public UnityEvent onRoundResolved;
    public UnityEvent onCounter;       // a counter landed (countered and holed) this round
    public UnityEvent onMatchEnded;
    public UnityEvent onSelect;        // a class picked or an action locked
    public UnityEvent onConfirm;       // Enter pressed to continue

    public PlayerState P1 { get; } = new PlayerState();
    public PlayerState P2 { get; } = new PlayerState();

    public MatchPhase Phase { get; private set; } = MatchPhase.ClassSelect;
    public int PendingClass1 { get; private set; } = -1;
    public int PendingClass2 { get; private set; } = -1;
    public bool Action1Locked { get; private set; }
    public bool Action2Locked { get; private set; }
    public PlayerState CurrentShooter { get; private set; }
    public int Round { get; private set; }
    public CombatOutcome LastOutcome { get; private set; }
    public int Winner { get; private set; } // 0 = draw, 1 = P1, 2 = P2
    public WinReason Reason { get; private set; }

    public ClassConfig TutorialClass { get; private set; }
    public CombatAction TutorialAction { get; private set; }

    bool turnDone;
    int tutorialRequest = -1;

    void Start()
    {
        shot.enabled = false;
        StartCoroutine(RunMatch());
    }

    // Called by the class cards (Button On Click) on the class select screen.
    public void RequestTutorial(int classIndex)
    {
        if (Phase == MatchPhase.ClassSelect && classIndex >= 0 && classIndex < classes.Length)
            tutorialRequest = classIndex;
    }

    IEnumerator RunMatch()
    {
        // ---------- Class select ----------
        yield return SelectClasses();
        onMatchStarted.Invoke();

        P1.hp = combatConfig.startHP;
        P2.hp = combatConfig.startHP;
        int firstShooter = Random.Range(0, 2); // 0 = P1 starts round 1

        // ---------- Rounds ----------
        for (Round = 1; Round <= maxRounds; Round++)
        {
            onRoundStarted.Invoke();

            // Secret action select.
            yield return SelectActions();

            // Both actions are now public, with the counter result, before anyone shoots.
            Phase = MatchPhase.Reveal;
            onActionsLocked.Invoke();
            yield return WaitForEnter();

            // Golf, shooting order alternates every round.
            bool p1First = (firstShooter + Round - 1) % 2 == 0;
            yield return PlayGolf(p1First ? P1 : P2);
            yield return PlayGolf(p1First ? P2 : P1);

            // Damage is computed now but applied after the action animations.
            ComputeOutcome();
            Phase = MatchPhase.CombatAnim;

            if (stage)
                yield return stage.PlayActions(
                    P1.action, P1.result != StrokeResult.Failed,
                    P2.action, P2.result != StrokeResult.Failed,
                    LastOutcome.final1 > 0, LastOutcome.final2 > 0);

            ApplyDamage();
            onRoundResolved.Invoke();
            if (LastOutcome.bonus1 || LastOutcome.bonus2) onCounter.Invoke();

            if (stage)
                yield return stage.PlayHits(LastOutcome.final1 > 0, LastOutcome.final2 > 0);

            Phase = MatchPhase.RoundResult;
            yield return WaitForEnter();

            if (P1.hp == 0 || P2.hp == 0) break;
        }

        if (Round > maxRounds) Round = maxRounds;

        // ---------- Match end ----------
        DecideWinner();
        if (stage) stage.PlayEnd(Winner);
        Phase = MatchPhase.MatchEnd;
        onMatchEnded.Invoke();
        yield return null;

        while (true)
        {
            var kb = Keyboard.current;
            if (EnterPressed())
            {
                onConfirm.Invoke();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield break;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && !string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
                yield break;
            }
            yield return null;
        }
    }

    IEnumerator SelectClasses()
    {
        Phase = MatchPhase.ClassSelect;
        PendingClass1 = -1;
        PendingClass2 = -1;
        Debug.Log("[Match] Class select started. P1: A/S/D, P2: J/K/L, then Enter. Click a card to practice.");

        // Ignore any key still held from the previous scene.
        yield return new WaitForSeconds(0.2f);

        while (true)
        {
            // A class card was clicked: run the practice, then come back here.
            if (tutorialRequest >= 0)
            {
                int c = tutorialRequest;
                tutorialRequest = -1;
                yield return RunTutorial(classes[c]);
                Phase = MatchPhase.ClassSelect;
                yield return null; // skip the Esc frame
                continue;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                int k = ReadP1Key(kb);
                if (k >= 0 && k < classes.Length)
                {
                    PendingClass1 = k;
                    onSelect.Invoke();
                    Debug.Log($"[Match] P1 picked {classes[k].displayName}");
                }
                k = ReadP2Key(kb);
                if (k >= 0 && k < classes.Length)
                {
                    PendingClass2 = k;
                    onSelect.Invoke();
                    Debug.Log($"[Match] P2 picked {classes[k].displayName}");
                }
            }

            if (PendingClass1 >= 0 && PendingClass2 >= 0 && EnterPressed())
            {
                onConfirm.Invoke();
                break;
            }
            yield return null;
        }

        P1.cls = classes[PendingClass1];
        P2.cls = classes[PendingClass2];
        Debug.Log($"[Match] Classes confirmed: P1 {P1.cls.displayName}, P2 {P2.cls.displayName}");
    }

    // Practice mode: endless tries on the simple tutorial layouts.
    // 1 / 2 / 3 switch action, Esc goes back to class select.
    // Practice mode: each maid practices only her own action.
    // 1 / 2 / 3 switch maid (and so the course), Esc goes back to class select.
    IEnumerator RunTutorial(ClassConfig cls)
    {
        Phase = MatchPhase.Tutorial;
        StartPractice(cls);
        Debug.Log($"[Match] Practice started with {cls.displayName}");
        yield return null;

        while (true)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) break;

                int pick = -1;
                if (kb.digit1Key.wasPressedThisFrame) pick = 0;
                if (kb.digit2Key.wasPressedThisFrame) pick = 1;
                if (kb.digit3Key.wasPressedThisFrame) pick = 2;
                if (pick >= 0 && pick < classes.Length)
                {
                    onSelect.Invoke();
                    StartPractice(classes[pick]);
                }
            }

            // After a finished try, let the result show, then start the same practice again.
            if (shot.State == ShotState.Finished)
            {
                FireResultEvent(shot.LastResult); // same result sounds as a real match
                yield return new WaitForSeconds(tutorialRetryDelay);
                switcher.SetupTutorial(TutorialClass, TutorialAction);
            }
            yield return null;
        }

        shot.enabled = false;
        switcher.HideAllLayouts();
        TutorialClass = null;
        Debug.Log("[Match] Practice ended");
    }

    // The maid's own action decides the course.
    void StartPractice(ClassConfig cls)
    {
        TutorialClass = cls;
        TutorialAction = cls.signatureAction;
        shot.enabled = true;
        switcher.SetupTutorial(TutorialClass, TutorialAction);
    }

    IEnumerator SelectActions()
    {
        Phase = MatchPhase.ActionSelect;
        Action1Locked = false;
        Action2Locked = false;
        int a1 = -1, a2 = -1;

        yield return null; // ignore key presses from the previous screen

        while (a1 < 0 || a2 < 0)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // Once locked, a choice cannot be changed.
                if (a1 < 0) { a1 = ReadP1Key(kb); if (a1 >= 0) onSelect.Invoke(); }
                if (a2 < 0) { a2 = ReadP2Key(kb); if (a2 >= 0) onSelect.Invoke(); }
            }
            Action1Locked = a1 >= 0;
            Action2Locked = a2 >= 0;
            yield return null;
        }

        P1.action = (CombatAction)a1;
        P2.action = (CombatAction)a2;
        yield return new WaitForSeconds(0.4f); // let players see both locks
    }

    IEnumerator PlayGolf(PlayerState p)
    {
        bool isP1 = p == P1;
        CurrentShooter = p;
        Phase = MatchPhase.PreGolf;
        onTurnStarted.Invoke();
        yield return WaitForEnter();

        Phase = MatchPhase.Golf;
        if (stage) stage.PlayReady(isP1);

        turnDone = false;
        shot.TurnFinished += OnTurnFinished;
        shot.enabled = true;
        switcher.SetupTurn(p.cls, p.action); // both players shoot from the left

        while (!turnDone) yield return null;

        shot.TurnFinished -= OnTurnFinished;
        p.result = shot.LastResult;
        p.strokesUsed = shot.StrokesUsed;
        p.results.Add(p.result);
        FireResultEvent(p.result);

        Phase = MatchPhase.TurnResult;
        yield return new WaitForSeconds(afterTurnDelay);
        shot.enabled = false;
        if (stage) stage.PlayIdle(isP1);
    }

    void OnTurnFinished(StrokeResult result)
    {
        turnDone = true;
    }

    void ComputeOutcome()
    {
        LastOutcome = CombatResolver.Resolve(combatConfig,
            new PlayerTurn { action = P1.action, result = P1.result, classConfig = P1.cls },
            new PlayerTurn { action = P2.action, result = P2.result, classConfig = P2.cls });
    }

    void ApplyDamage()
    {
        P1.hp = Mathf.Max(0, P1.hp - LastOutcome.final1);
        P2.hp = Mathf.Max(0, P2.hp - LastOutcome.final2);
    }

    // Priority: HP, then holes made, then score strokes, then draw.
    void DecideWinner()
    {
        if (P1.hp != P2.hp)
        {
            Winner = P1.hp > P2.hp ? 1 : 2;
            Reason = WinReason.HP;
        }
        else if (P1.Holes != P2.Holes)
        {
            Winner = P1.Holes > P2.Holes ? 1 : 2;
            Reason = WinReason.Holes;
        }
        else if (P1.ScoreStrokes != P2.ScoreStrokes)
        {
            Winner = P1.ScoreStrokes < P2.ScoreStrokes ? 1 : 2;
            Reason = WinReason.Strokes;
        }
        else
        {
            Winner = 0;
            Reason = WinReason.Draw;
        }
    }

    void FireResultEvent(StrokeResult r)
    {
        if (r == StrokeResult.HoleInOne) onHoleInOne.Invoke();
        else if (r == StrokeResult.TwoStrokes) onTwoStrokes.Invoke();
        else onFailed.Invoke();
    }

    IEnumerator WaitForEnter()
    {
        yield return null; // skip the frame of the previous key press
        while (!EnterPressed()) yield return null;
        onConfirm.Invoke();
    }

    static bool EnterPressed()
    {
        var kb = Keyboard.current;
        return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
    }

    static int ReadP1Key(Keyboard kb)
    {
        if (kb.aKey.wasPressedThisFrame) return 0;
        if (kb.sKey.wasPressedThisFrame) return 1;
        if (kb.dKey.wasPressedThisFrame) return 2;
        return -1;
    }

    static int ReadP2Key(Keyboard kb)
    {
        if (kb.jKey.wasPressedThisFrame) return 0;
        if (kb.kKey.wasPressedThisFrame) return 1;
        if (kb.lKey.wasPressedThisFrame) return 2;
        return -1;
    }
}