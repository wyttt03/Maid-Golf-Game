using UnityEngine;
using UnityEngine.UI;

public sealed class SkillKeyHighlight : MonoBehaviour
{
    [System.Serializable]
    public class Slot
    {
        public Image image;
        public Sprite normal;
        public Sprite highlight;
    }

    [SerializeField] MatchManager match;
    [Tooltip("顺序：Attack、Defense、Reflect")]
    [SerializeField] Slot[] player1 = new Slot[3];
    [Tooltip("顺序：Attack、Defense、Reflect")]
    [SerializeField] Slot[] player2 = new Slot[3];

    void Update()
    {
        if (!match) return;

        var phase = match.Phase;
        bool reveal = phase == MatchPhase.Reveal || phase == MatchPhase.PreGolf ||
                      phase == MatchPhase.Golf || phase == MatchPhase.TurnResult ||
                      phase == MatchPhase.CombatAnim || phase == MatchPhase.RoundResult;

        Apply(player1, reveal ? (int)match.P1.action : -1);
        Apply(player2, reveal ? (int)match.P2.action : -1);
    }

    static void Apply(Slot[] slots, int selected)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (s == null || !s.image) continue;
            var sprite = i == selected ? s.highlight : s.normal;
            if (sprite && s.image.sprite != sprite) s.image.sprite = sprite;
        }
    }
}