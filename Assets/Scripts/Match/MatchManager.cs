using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum MatchPhase { ClassSelect, ActionSelect, PreGolf, Golf, TurnResult, RoundResult, MatchEnd }
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

        // GDD 10.2: hole-in-one = 1, two strokes = 2, failed = 3 (penalty).
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

    [Header("Rules")]
    public int maxRounds = 3;
    [Tooltip("Pause after a golf turn so the result and cup rise can be seen.")]
    public float afterTurnDelay = 1.2f;
    public string menuSceneName = "MainMenu";

    [Header("Feedback hooks (wire audio, particles, UI here)")]
    public UnityEvent onRoundStarted;
    public UnityEvent onActionsLocked;
    public UnityEvent onHoleInOne;
    public UnityEvent onTwoStrokes;
    public UnityEvent onFailed;
    public UnityEvent onRoundResolved;
    public UnityEvent onMatchEnded;

    public PlayerState P1 { get; } = new PlayerState();
    public PlayerState P2 { get; } = new PlayerState();

    public MatchPhase Phase { get; private set; }
    public int PendingClass1 { get; private set; } = -1;
    public int PendingClass2 { get; private set; } = -1;
    public bool Action1Locked { get; private set; }
    public bool Action2Locked { get; private set; }
    public PlayerState CurrentShooter { get; private set; }
    public int Round { get; private set; }
    public CombatOutcome LastOutcome { get; private set; }
    public int Winner { get; private set; } // 0 = draw, 1 = P1, 2 = P2
    public WinReason Reason { get; private set; }

    bool turnDone;

    void Start()
    {
        shot.enabled = false;
        StartCoroutine(RunMatch());
    }

    IEnumerator RunMatch()
    {
        yield return SelectClasses();

        P1.hp = combatConfig.startHP;
        P2.hp = combatConfig.startHP;
        int firstShooter = Random.Range(0, 2); // 0 = P1 starts round 1

        for (Round = 1; Round <= maxRounds; Round++)
        {
            onRoundStarted.Invoke();
            yield return SelectActions();
            onActionsLocked.Invoke();

            // Shooting order alternates every round.
            bool p1First = (firstShooter + Round - 1) % 2 == 0;
            yield return PlayGolf(p1First ? P1 : P2);
            yield return PlayGolf(p1First ? P2 : P1);

            Resolve();
            onRoundResolved.Invoke();
            Phase = MatchPhase.RoundResult;
            yield return WaitForEnter();

            if (P1.hp == 0 || P2.hp == 0) break;
        }

        if (Round > maxRounds) Round = maxRounds;

        DecideWinner();
        Phase = MatchPhase.MatchEnd;
        onMatchEnded.Invoke();
        yield return null;

        while (true)
        {
            var kb = Keyboard.current;
            if (EnterPressed())
            {
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

        while (true)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                int k = ReadP1Key(kb);
                if (k >= 0 && k < classes.Length) PendingClass1 = k;
                k = ReadP2Key(kb);
                if (k >= 0 && k < classes.Length) PendingClass2 = k;
            }

            if (PendingClass1 >= 0 && PendingClass2 >= 0 && EnterPressed()) break;
            yield return null;
        }

        P1.cls = classes[PendingClass1];
        P2.cls = classes[PendingClass2];
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
                if (a1 < 0) a1 = ReadP1Key(kb);
                if (a2 < 0) a2 = ReadP2Key(kb);
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
        CurrentShooter = p;
        Phase = MatchPhase.PreGolf;
        yield return WaitForEnter();

        Phase = MatchPhase.Golf;
        turnDone = false;
        shot.TurnFinished += OnTurnFinished;
        shot.enabled = true;
        switcher.SetupTurn(p.cls, p.action);

        while (!turnDone) yield return null;

        shot.TurnFinished -= OnTurnFinished;
        p.result = shot.LastResult;
        p.strokesUsed = shot.StrokesUsed;
        p.results.Add(p.result);
        FireResultEvent(p.result);

        Phase = MatchPhase.TurnResult;
        yield return new WaitForSeconds(afterTurnDelay);
        shot.enabled = false;
    }

    void OnTurnFinished(StrokeResult result)
    {
        turnDone = true;
    }

    void Resolve()
    {
        var o = CombatResolver.Resolve(combatConfig,
            new PlayerTurn { action = P1.action, result = P1.result, classConfig = P1.cls },
            new PlayerTurn { action = P2.action, result = P2.result, classConfig = P2.cls });

        LastOutcome = o;
        P1.hp = Mathf.Max(0, P1.hp - o.final1);
        P2.hp = Mathf.Max(0, P2.hp - o.final2);
    }

    // GDD 10.3 priority: HP, then holes made, then score strokes, then draw.
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