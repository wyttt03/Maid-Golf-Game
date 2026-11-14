using UnityEngine;

public struct PlayerTurn
{
    public CombatAction action;
    public StrokeResult result;
    public ClassConfig classConfig; // not used for damage
}

public struct CombatOutcome
{
    public float active1, active2;       // damage each player deals (bonus included)
    public bool counter1, counter2;      // this player's action counters the other's
    public bool bonus1, bonus2;          // counter bonus applied (countered and holed)
    public int final1, final2;           // damage each player receives

    // Kept at 0 so older UI code still compiles.
    public float reduction1, reduction2;
    public float reflectRatio1, reflectRatio2;
    public float taken1, taken2;
    public float reflected1, reflected2;

    public override string ToString()
    {
        return $"P1 deals {active1} (counter {counter1}, bonus {bonus1})\n" +
               $"P2 deals {active2} (counter {counter2}, bonus {bonus2})\n" +
               $"P1 takes {final1} | P2 takes {final2}";
    }
}

public static class CombatResolver
{
    public static CombatOutcome Resolve(CombatConfig cfg, PlayerTurn p1, PlayerTurn p2)
    {
        var o = new CombatOutcome();

        o.counter1 = CombatConfig.Counters(p1.action, p2.action);
        o.counter2 = CombatConfig.Counters(p2.action, p1.action);

        // Counter bonus only counts if the countering player actually holed the ball.
        o.bonus1 = o.counter1 && p1.result != StrokeResult.Failed;
        o.bonus2 = o.counter2 && p2.result != StrokeResult.Failed;

        int dealt1 = cfg.GetDamage(p1.result) + (o.bonus1 ? cfg.counterBonusDamage : 0);
        int dealt2 = cfg.GetDamage(p2.result) + (o.bonus2 ? cfg.counterBonusDamage : 0);

        o.active1 = dealt1;
        o.active2 = dealt2;

        // Both hit at the same time.
        o.final1 = Mathf.Max(0, dealt2);
        o.final2 = Mathf.Max(0, dealt1);
        o.taken1 = o.final1;
        o.taken2 = o.final2;
        return o;
    }
}