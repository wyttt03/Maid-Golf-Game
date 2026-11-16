using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Display only: reads MatchManager every frame and fills the UI.
// No game logic here, so all visuals can be replaced in the editor.
public class MatchUI : MonoBehaviour
{
    [System.Serializable]
    public class PlayerHud
    {
        public TMP_Text classText;
        public TMP_Text hpText;
        [Tooltip("Image with Image Type = Filled. Moves smoothly to the new HP.")]
        public Image hpFill;
        [Tooltip("Optional. Light bar behind the fill that drains slowly after a hit.")]
        public Image hpTrail;
        [Tooltip("Optional. Floating damage number near this player's maid.")]
        public TMP_Text damageText;
    }

    [System.Serializable]
    public class ClassCard
    {
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public GameObject p1Marker;
        public GameObject p2Marker;
    }

    // Runtime state for one player's HP bar and damage popup.
    class HudState
    {
        public int lastHp = -1;
        public float shown;
        public float trail;
        public float trailTimer;
        public Vector2 damageBasePos;
        public bool basePosSaved;
        public Coroutine popup;
    }

    public MatchManager match;
    public ShotController shot;

    [Header("Panels")]
    public GameObject hudTop;
    public GameObject classSelectPanel;
    public GameObject actionSelectPanel;
    public GameObject revealPanel;
    public GameObject preGolfPanel;
    public GameObject golfHud;
    public GameObject turnResultPanel;
    public GameObject counterPanel;
    public GameObject roundResultPanel;
    public GameObject matchEndPanel;
    [Tooltip("Golf camera object. Only on while a shot is being played.")]
    public GameObject golfCamera;

    [Header("Stage maids (same order as MatchManager.classes)")]
    public GameObject[] p1Maids;
    public GameObject[] p2Maids;

    [Header("Top HUD")]
    public PlayerHud p1Hud;
    public PlayerHud p2Hud;
    public TMP_Text roundText;

    [Header("Class select (same order as MatchManager.classes)")]
    public ClassCard[] classCards;

    [Header("Action select")]
    public TMP_Text p1LockText;
    public TMP_Text p2LockText;
    public TMP_Text p1BonusText;
    public TMP_Text p2BonusText;
    public TMP_Text p1LastText;
    public TMP_Text p2LastText;

    [Header("Reveal")]
    public TMP_Text p1RevealText;
    public TMP_Text p2RevealText;
    public TMP_Text revealResultText;

    [Header("Golf")]
    public TMP_Text preGolfText;
    public TMP_Text golfInfoText;
    public TMP_Text strokesText;
    public TMP_Text controlHintText;

    [Header("Results")]
    public TMP_Text turnResultText;
    public TMP_Text counterText;
    public TMP_Text p1RoundColumn;
    public TMP_Text p2RoundColumn;
    public TMP_Text winnerText;
    [Header("Tutorial")]
    public GameObject tutorialPanel;
    public TMP_Text tutorialTitleText;
    public TMP_Text tutorialInstructionText;
    public TMP_Text tutorialResultText;
    [Tooltip("{0} class, {1} action")]
    public string tutorialTitleFormat = "PRACTICE  {0}  {1}";
    [Tooltip("Order: Attack, Defense, Reflect.")]
    [TextArea(2, 4)]
    public string[] tutorialInstructions =
    {
        "AIM WITH THE MOUSE\nLEFT CLICK TO LOCK AIM\nPRESS SPACE TO SET POWER",
        "THE SHOT DIRECTION IS FIXED\nMOVE THE CUP INTO THE BALL PATH\nLEFT CLICK TO LOCK IT\nPRESS SPACE TO SET POWER",
        "THE SHOT AND CUP ARE FIXED\nTURN THE OBSTACLE TO LINE UP A BOUNCE\nLEFT CLICK TO LOCK IT\nPRESS SPACE TO SET POWER"
    };

    [Header("HP bar animation")]
    [Tooltip("How fast the HP bar moves (full bar per second).")]
    public float hpBarSpeed = 1.5f;
    [Tooltip("Pause before the light trail bar starts to drain.")]
    public float trailDelay = 0.4f;
    [Tooltip("How fast the trail bar drains (full bar per second).")]
    public float trailSpeed = 0.6f;
    [Tooltip("How long a damage number stays on screen.")]
    public float damagePopupTime = 1.0f;
    [Tooltip("How far a damage number floats up, in pixels.")]
    public float damagePopupRise = 60f;

    [Header("Labels ({0}, {1}... are data slots, keep them)")]
    public string[] playerNames = { "P1", "P2" };
    public string[] actionNames = { "ATTACK", "DEFEND", "REFLECT" };
    public string[] resultNames = { "PERFECT!", "NICE!", "MISS!" };
    [Tooltip("Colors for PERFECT, NICE and MISS.")]
    public Color[] resultColors =
    {
        new Color(1f, 0.8f, 0.2f),
        new Color(0.45f, 0.85f, 1f),
        new Color(0.75f, 0.75f, 0.75f)
    };
    [Tooltip("Indexed by WinReason: Draw, HP, Holes, Strokes. {0} = winner name.")]
    public string[] winFormats = { "DRAW", "{0} WINS ON HP", "{0} WINS ON HOLES", "{0} WINS ON STROKES" };
    public string roundFormat = "ROUND {0} OF {1}";
    public string lockedLabel = "LOCKED";
    public string choosingLabel = "CHOOSING";
    public string bonusFormat = "BONUS MOVE  {0}";
    public string lastActionFormat = "LAST MOVE  {0}";
    [Tooltip("{0} player, {1} action")]
    public string revealActionFormat = "{0}\n{1}";
    [Tooltip("{0} player, {1} winning action, {2} losing action")]
    public string revealCounterFormat = "{0} COUNTERS!\n{1} BEATS {2}\nHOLE OUT FOR 10 BONUS DAMAGE";
    [Tooltip("{0} action")]
    public string revealSameFormat = "BOTH CHOSE {0}\nNO COUNTER";
    [Tooltip("{0} player, {1} class, {2} action")]
    public string preGolfFormat = "{0}  {1}\n{2}\n\nPRESS ENTER";
    [Tooltip("{0} player, {1} action")]
    public string golfInfoFormat = "{0}  {1}";
    public string strokesFormat = "STROKES {0} OF {1}";
    [Tooltip("{0} player, {1} result")]
    public string turnResultFormat = "{0}\n{1}";
    [Tooltip("{0} player, {1} winning action, {2} losing action")]
    public string counterFormat = "10 BONUS DAMAGE!\n{0}  {1} BEATS {2}";
    public string counterLabel = "COUNTER!";
    public string damageFormat = "{0} DAMAGE";
    [Tooltip("{0} action, {1} result, {3} damage dealt, {6} damage taken, {7} HP")]
    public string roundColumnFormat = "{0}\n{1}\nDEALT {3}\nTAKEN {6}\nHP {7}";

    readonly HudState s1 = new HudState();
    readonly HudState s2 = new HudState();

    void Start()
    {
        for (int i = 0; i < classCards.Length && i < match.classes.Length; i++)
        {
            var card = classCards[i];
            var cls = match.classes[i];
            SetText(card.nameText, cls.displayName);
            SetText(card.descriptionText, cls.description);
        }

        if (p1Hud.damageText) p1Hud.damageText.gameObject.SetActive(false);
        if (p2Hud.damageText) p2Hud.damageText.gameObject.SetActive(false);
    }

    void Update()
    {
        var phase = match.Phase;
        var o = match.LastOutcome;
        bool tutorial = phase == MatchPhase.Tutorial;

        Show(hudTop, phase != MatchPhase.ClassSelect && !tutorial);
        Show(classSelectPanel, phase == MatchPhase.ClassSelect);
        Show(actionSelectPanel, phase == MatchPhase.ActionSelect);
        Show(revealPanel, phase == MatchPhase.Reveal);
        Show(preGolfPanel, phase == MatchPhase.PreGolf);
        Show(golfHud, phase == MatchPhase.Golf || tutorial);
        Show(turnResultPanel, phase == MatchPhase.TurnResult);
        Show(counterPanel, phase == MatchPhase.CombatAnim && (o.bonus1 || o.bonus2));
        Show(roundResultPanel, phase == MatchPhase.RoundResult);
        Show(matchEndPanel, phase == MatchPhase.MatchEnd);
        Show(tutorialPanel, tutorial);
        Show(golfCamera, phase == MatchPhase.Golf || phase == MatchPhase.TurnResult || tutorial);

        UpdateStageMaids(phase);

        if (phase == MatchPhase.ClassSelect)
        {
            UpdateClassSelect();
            return;
        }

        if (tutorial)
        {
            UpdateTutorial();
            return;
        }

        UpdateTopHud();

        switch (phase)
        {
            case MatchPhase.ActionSelect: UpdateActionSelect(); break;
            case MatchPhase.Reveal: UpdateReveal(); break;
            case MatchPhase.PreGolf: UpdatePreGolf(); break;
            case MatchPhase.Golf: UpdateGolf(); break;
            case MatchPhase.TurnResult: UpdateTurnResult(); break;
            case MatchPhase.CombatAnim: UpdateCounter(); break;
            case MatchPhase.RoundResult: UpdateRoundResult(); break;
            case MatchPhase.MatchEnd: UpdateMatchEnd(); break;
        }
    }

    // ---------- Class select and stage ----------

    void UpdateClassSelect()
    {
        for (int i = 0; i < classCards.Length; i++)
        {
            Show(classCards[i].p1Marker, match.PendingClass1 == i);
            Show(classCards[i].p2Marker, match.PendingClass2 == i);
        }
    }

    void UpdateStageMaids(MatchPhase phase)
    {
        int c1, c2;
        if (phase == MatchPhase.ClassSelect)
        {
            c1 = match.PendingClass1;
            c2 = match.PendingClass2;
        }
        else if (phase == MatchPhase.Tutorial)
        {
            // Practice: show the practising maid on the left only.
            c1 = System.Array.IndexOf(match.classes, match.TutorialClass);
            c2 = -1;
        }
        else
        {
            c1 = System.Array.IndexOf(match.classes, match.P1.cls);
            c2 = System.Array.IndexOf(match.classes, match.P2.cls);
        }

        if (p1Maids != null)
            for (int i = 0; i < p1Maids.Length; i++) Show(p1Maids[i], i == c1);
        if (p2Maids != null)
            for (int i = 0; i < p2Maids.Length; i++) Show(p2Maids[i], i == c2);
    }

    // ---------- Top HUD ----------

    void UpdateTopHud()
    {
        UpdatePlayerHud(p1Hud, s1, match.P1);
        UpdatePlayerHud(p2Hud, s2, match.P2);
        SetText(roundText, string.Format(roundFormat, match.Round, match.maxRounds));
    }

    void UpdatePlayerHud(PlayerHud hud, HudState s, MatchManager.PlayerState p)
    {
        SetText(hud.classText, ClassName(p));
        SetText(hud.hpText, p.hp.ToString());

        int max = match.combatConfig.startHP;
        float target = max > 0 ? (float)p.hp / max : 0f;

        // First frame after the match starts: jump straight to full.
        if (s.lastHp < 0)
        {
            s.lastHp = p.hp;
            s.shown = target;
            s.trail = target;
        }

        // HP went down: pop a damage number and hold the trail for a moment.
        if (p.hp < s.lastHp)
        {
            ShowDamage(hud, s, s.lastHp - p.hp);
            s.trailTimer = trailDelay;
        }
        s.lastHp = p.hp;

        float dt = Time.deltaTime;
        s.shown = Mathf.MoveTowards(s.shown, target, hpBarSpeed * dt);

        if (s.trailTimer > 0f) s.trailTimer -= dt;
        else s.trail = Mathf.MoveTowards(s.trail, s.shown, trailSpeed * dt);
        if (s.trail < s.shown) s.trail = s.shown;

        if (hud.hpFill) hud.hpFill.fillAmount = s.shown;
        if (hud.hpTrail) hud.hpTrail.fillAmount = s.trail;
    }

    void ShowDamage(PlayerHud hud, HudState s, int amount)
    {
        if (!hud.damageText) return;

        if (!s.basePosSaved)
        {
            s.damageBasePos = hud.damageText.rectTransform.anchoredPosition;
            s.basePosSaved = true;
        }

        if (s.popup != null) StopCoroutine(s.popup);
        s.popup = StartCoroutine(DamagePopup(hud.damageText, s.damageBasePos, amount));
    }

    IEnumerator DamagePopup(TMP_Text t, Vector2 basePos, int amount)
    {
        var rt = t.rectTransform;
        Color c = t.color;
        t.text = string.Format(damageFormat, amount);
        t.gameObject.SetActive(true);

        for (float time = 0f; time < damagePopupTime; time += Time.deltaTime)
        {
            float k = time / damagePopupTime;
            rt.anchoredPosition = basePos + Vector2.up * damagePopupRise * k;
            c.a = 1f - k * k;
            t.color = c;
            yield return null;
        }

        rt.anchoredPosition = basePos;
        c.a = 1f;
        t.color = c;
        t.gameObject.SetActive(false);
    }

    // ---------- Action select and reveal ----------

    void UpdateActionSelect()
    {
        SetText(p1LockText, match.Action1Locked ? lockedLabel : choosingLabel);
        SetText(p2LockText, match.Action2Locked ? lockedLabel : choosingLabel);
        SetText(p1BonusText, BonusText(match.P1));
        SetText(p2BonusText, BonusText(match.P2));

        // Until new actions are locked, each player's action still holds last round's choice.
        bool hasLast = match.Round > 1;
        SetText(p1LastText, hasLast ? string.Format(lastActionFormat, ActionName(match.P1.action)) : "");
        SetText(p2LastText, hasLast ? string.Format(lastActionFormat, ActionName(match.P2.action)) : "");
    }

    void UpdateReveal()
    {
        var p1 = match.P1;
        var p2 = match.P2;

        SetText(p1RevealText, string.Format(revealActionFormat, PlayerName(p1), ActionName(p1.action)));
        SetText(p2RevealText, string.Format(revealActionFormat, PlayerName(p2), ActionName(p2.action)));

        if (CombatConfig.Counters(p1.action, p2.action))
            SetText(revealResultText, string.Format(revealCounterFormat,
                PlayerName(p1), ActionName(p1.action), ActionName(p2.action)));
        else if (CombatConfig.Counters(p2.action, p1.action))
            SetText(revealResultText, string.Format(revealCounterFormat,
                PlayerName(p2), ActionName(p2.action), ActionName(p1.action)));
        else
            SetText(revealResultText, string.Format(revealSameFormat, ActionName(p1.action)));
    }

    // ---------- Golf ----------

    void UpdatePreGolf()
    {
        var p = match.CurrentShooter;
        if (p == null) return;
        SetText(preGolfText, string.Format(preGolfFormat, PlayerName(p), ClassName(p), ActionName(p.action)));
    }

    void UpdateGolf()
    {
        var p = match.CurrentShooter;
        if (p == null) return;
        SetText(golfInfoText, string.Format(golfInfoFormat, PlayerName(p), ActionName(p.action)));
        SetText(strokesText, string.Format(strokesFormat, shot.StrokesUsed, shot.maxStrokes));
        SetText(controlHintText, shot.ControlHint);
    }
    void UpdateTutorial()
    {
        var cls = match.TutorialClass;
        var action = match.TutorialAction;

        SetText(tutorialTitleText, string.Format(tutorialTitleFormat,
            cls ? cls.displayName : "", ActionName(action)));
        SetText(tutorialInstructionText, Pick(tutorialInstructions, (int)action, ""));
        SetText(golfInfoText, "");
        SetText(strokesText, string.Format(strokesFormat, shot.StrokesUsed, shot.maxStrokes));
        SetText(controlHintText, shot.ControlHint);

        bool done = shot.State == ShotState.Finished;
        SetText(tutorialResultText, done ? ResultName(shot.LastResult) : "");
        int i = (int)shot.LastResult;
        if (done && tutorialResultText && resultColors != null && i < resultColors.Length)
            tutorialResultText.color = resultColors[i];
    }
    void UpdateTurnResult()
    {
        var p = match.CurrentShooter;
        if (p == null) return;

        SetText(turnResultText, string.Format(turnResultFormat, PlayerName(p), ResultName(p.result)));

        int i = (int)p.result;
        if (turnResultText && resultColors != null && i < resultColors.Length)
            turnResultText.color = resultColors[i];
    }

    // ---------- Combat and results ----------

    void UpdateCounter()
    {
        var o = match.LastOutcome;
        if (o.bonus1)
            SetText(counterText, string.Format(counterFormat,
                PlayerName(match.P1), ActionName(match.P1.action), ActionName(match.P2.action)));
        else if (o.bonus2)
            SetText(counterText, string.Format(counterFormat,
                PlayerName(match.P2), ActionName(match.P2.action), ActionName(match.P1.action)));
    }

    void UpdateRoundResult()
    {
        var o = match.LastOutcome;
        string col1 = RoundColumn(match.P1, o.active1, o.final1);
        string col2 = RoundColumn(match.P2, o.active2, o.final2);
        if (o.bonus1) col1 = counterLabel + "\n" + col1;
        if (o.bonus2) col2 = counterLabel + "\n" + col2;
        SetText(p1RoundColumn, col1);
        SetText(p2RoundColumn, col2);
    }

    void UpdateMatchEnd()
    {
        string winner = match.Winner > 0 ? Pick(playerNames, match.Winner - 1, "") : "";
        SetText(winnerText, string.Format(Pick(winFormats, (int)match.Reason, "{0}"), winner));
    }

    // Argument order kept from older versions so saved format strings still work.
    string RoundColumn(MatchManager.PlayerState p, float dealt, int taken)
    {
        return string.Format(roundColumnFormat,
            ActionName(p.action), ResultName(p.result), 0f,
            Mathf.RoundToInt(dealt), 0f, 0, taken, p.hp);
    }

    // ---------- Helpers ----------

    string BonusText(MatchManager.PlayerState p)
    {
        return p.cls ? string.Format(bonusFormat, ActionName(p.cls.signatureAction)) : "";
    }

    string PlayerName(MatchManager.PlayerState p) => Pick(playerNames, p == match.P1 ? 0 : 1, "");
    string ClassName(MatchManager.PlayerState p) => p.cls ? p.cls.displayName : "";
    string ActionName(CombatAction a) => Pick(actionNames, (int)a, a.ToString().ToUpperInvariant());
    string ResultName(StrokeResult r) => Pick(resultNames, (int)r, r.ToString().ToUpperInvariant());

    static string Pick(string[] arr, int i, string fallback)
    {
        return arr != null && i >= 0 && i < arr.Length ? arr[i] : fallback;
    }

    static void SetText(TMP_Text t, string s)
    {
        if (t) t.text = s;
    }

    static void Show(GameObject go, bool visible)
    {
        if (go && go.activeSelf != visible) go.SetActive(visible);
    }
}