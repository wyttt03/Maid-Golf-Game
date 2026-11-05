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
        [Tooltip("Image with Image Type = Filled.")]
        public Image hpFill;
    }

    [System.Serializable]
    public class ClassCard
    {
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public GameObject p1Marker;
        public GameObject p2Marker;
    }

    public MatchManager match;
    public ShotController shot;

    [Header("Panels")]
    public GameObject hudTop;
    public GameObject classSelectPanel;
    public GameObject actionSelectPanel;
    public GameObject preGolfPanel;
    public GameObject golfHud;
    public GameObject turnResultPanel;
    public GameObject roundResultPanel;
    public GameObject matchEndPanel;
    [Header("Stage maids (same order as MatchManager.classes)")]
    public GameObject[] p1Maids;
    public GameObject[] p2Maids;
    [Tooltip("Golf camera object. Only on while a skill shot is being played.")]
    public GameObject golfCamera;

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

    [Header("Golf")]
    public TMP_Text preGolfText;
    public TMP_Text golfInfoText;
    public TMP_Text strokesText;

    [Header("Results")]
    public TMP_Text turnResultText;
    public TMP_Text p1RoundColumn;
    public TMP_Text p2RoundColumn;
    public TMP_Text winnerText;

    [Header("Labels ({0}, {1}... are data slots, keep them)")]
    public string[] playerNames = { "P1", "P2" };
    public string[] actionNames = { "Attack", "Defend", "Reflect" };
    public string[] resultNames = { "Hole-in-One!", "Two Strokes", "Failed" };
    [Tooltip("Indexed by WinReason: Draw, HP, Holes, Strokes. {0} = winner name.")]
    public string[] winFormats = { "Draw", "{0} wins by HP", "{0} wins by holes made", "{0} wins by strokes" };
    public string roundFormat = "Round {0}/{1}";
    public string lockedLabel = "LOCKED";
    public string choosingLabel = "Choosing...";
    public string bonusFormat = "Bonus action: {0}";
    [Tooltip("{0} player, {1} class, {2} action")]
    public string preGolfFormat = "{0} ({1})\n{2}\n\nPress Enter";
    [Tooltip("{0} player, {1} action")]
    public string golfInfoFormat = "{0} - {1}";
    public string strokesFormat = "Strokes {0}/{1}";
    [Tooltip("{0} player, {1} result")]
    public string turnResultFormat = "{0}\n{1}";
    [Tooltip("{0} action, {1} result, {2} multiplier, {3} dealt, {4} block, {5} reflect, {6} taken, {7} HP")]
    public string roundColumnFormat = "{0}\n{1}  x{2:0.#}\nDealt {3}\nBlock {4:P0}\nReflect {5}\nTaken {6}\nHP {7}";

    void Start()
    {
        for (int i = 0; i < classCards.Length && i < match.classes.Length; i++)
        {
            var card = classCards[i];
            var cls = match.classes[i];
            SetText(card.nameText, cls.displayName);
            SetText(card.descriptionText, cls.description);
        }
    }

    void Update()
    {
        var phase = match.Phase;

        Show(hudTop, phase != MatchPhase.ClassSelect);
        Show(classSelectPanel, phase == MatchPhase.ClassSelect);
        Show(actionSelectPanel, phase == MatchPhase.ActionSelect);
        Show(preGolfPanel, phase == MatchPhase.PreGolf);
        Show(golfHud, phase == MatchPhase.Golf);
        Show(turnResultPanel, phase == MatchPhase.TurnResult);
        Show(roundResultPanel, phase == MatchPhase.RoundResult);
        Show(matchEndPanel, phase == MatchPhase.MatchEnd);
        Show(golfCamera, phase == MatchPhase.Golf || phase == MatchPhase.TurnResult);
        UpdateStageMaids(phase);
        if (phase == MatchPhase.ClassSelect)
        {
            UpdateClassSelect();
            return;
        }

        UpdateTopHud();

        switch (phase)
        {
            case MatchPhase.ActionSelect: UpdateActionSelect(); break;
            case MatchPhase.PreGolf: UpdatePreGolf(); break;
            case MatchPhase.Golf: UpdateGolf(); break;
            case MatchPhase.TurnResult: UpdateTurnResult(); break;
            case MatchPhase.RoundResult: UpdateRoundResult(); break;
            case MatchPhase.MatchEnd: UpdateMatchEnd(); break;
        }
    }

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
        // While choosing, show the pending pick; after that, the confirmed class.
        int c1 = phase == MatchPhase.ClassSelect
            ? match.PendingClass1
            : System.Array.IndexOf(match.classes, match.P1.cls);
        int c2 = phase == MatchPhase.ClassSelect
            ? match.PendingClass2
            : System.Array.IndexOf(match.classes, match.P2.cls);

        if (p1Maids != null)
            for (int i = 0; i < p1Maids.Length; i++) Show(p1Maids[i], i == c1);
        if (p2Maids != null)
            for (int i = 0; i < p2Maids.Length; i++) Show(p2Maids[i], i == c2);
    }
    void UpdateTopHud()
    {
        FillPlayer(p1Hud, match.P1);
        FillPlayer(p2Hud, match.P2);
        SetText(roundText, string.Format(roundFormat, match.Round, match.maxRounds));
    }

    void UpdateActionSelect()
    {
        SetText(p1LockText, match.Action1Locked ? lockedLabel : choosingLabel);
        SetText(p2LockText, match.Action2Locked ? lockedLabel : choosingLabel);
        SetText(p1BonusText, BonusText(match.P1));
        SetText(p2BonusText, BonusText(match.P2));
    }

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
    }

    void UpdateTurnResult()
    {
        var p = match.CurrentShooter;
        if (p == null) return;
        SetText(turnResultText, string.Format(turnResultFormat, PlayerName(p), ResultName(p.result)));
    }

    void UpdateRoundResult()
    {
        var o = match.LastOutcome;
        SetText(p1RoundColumn, RoundColumn(match.P1, o.active1, o.reduction1, o.reflected1, o.final1));
        SetText(p2RoundColumn, RoundColumn(match.P2, o.active2, o.reduction2, o.reflected2, o.final2));
    }

    void UpdateMatchEnd()
    {
        string winner = match.Winner > 0 ? Pick(playerNames, match.Winner - 1, "") : "";
        SetText(winnerText, string.Format(Pick(winFormats, (int)match.Reason, "{0}"), winner));
    }

    string RoundColumn(MatchManager.PlayerState p, float dealt, float block, float reflect, int taken)
    {
        float mult = match.combatConfig.GetMultiplier(p.result);
        return string.Format(roundColumnFormat,
            ActionName(p.action), ResultName(p.result), mult,
            Mathf.RoundToInt(dealt), block, Mathf.RoundToInt(reflect), taken, p.hp);
    }

    void FillPlayer(PlayerHud hud, MatchManager.PlayerState p)
    {
        SetText(hud.classText, ClassName(p));
        SetText(hud.hpText, p.hp.ToString());
        if (hud.hpFill)
        {
            int max = match.combatConfig.startHP;
            hud.hpFill.fillAmount = max > 0 ? (float)p.hp / max : 0f;
        }
    }

    string BonusText(MatchManager.PlayerState p)
    {
        return p.cls ? string.Format(bonusFormat, ActionName(p.cls.signatureAction)) : "";
    }

    string PlayerName(MatchManager.PlayerState p) => Pick(playerNames, p == match.P1 ? 0 : 1, "");
    string ClassName(MatchManager.PlayerState p) => p.cls ? p.cls.displayName : "";
    string ActionName(CombatAction a) => Pick(actionNames, (int)a, a.ToString());
    string ResultName(StrokeResult r) => Pick(resultNames, (int)r, r.ToString());

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