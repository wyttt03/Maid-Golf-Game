using UnityEngine;

public struct PlayerTurn
{
    public CombatAction action;
    public StrokeResult result;
    public ClassConfig classConfig; // null = all modifiers 1
}

public struct CombatOutcome
{
    public float active1, active2;     // active damage dealt
    public float reduction1, reduction2;
    public float reflectRatio1, reflectRatio2;
    public float taken1, taken2;       // active damage received after defense
    public float reflected1, reflected2; // reflect damage sent to opponent
    public int final1, final2;         // final damage received (rounded)

    public override string ToString()
    {
        return $"P1 active {active1:0.##} | P2 active {active2:0.##}\n" +
               $"P1 reflects {reflected1:0.##} | P2 reflects {reflected2:0.##}\n" +
               $"P1 takes {final1} | P2 takes {final2}";
    }
}

public static class CombatResolver
{
    public static CombatOutcome Resolve(CombatConfig cfg, PlayerTurn p1, PlayerTurn p2)
    {
        var o = new CombatOutcome();

        // Step 1-2: multiplier and active damage
        float m1 = cfg.GetMultiplier(p1.result);
        float m2 = cfg.GetMultiplier(p2.result);
        o.active1 = cfg.GetBaseDamage(p1.action) * m1 * DamageMod(p1.classConfig);
        o.active2 = cfg.GetBaseDamage(p2.action) * m2 * DamageMod(p2.classConfig);

        // Step 3: defense
        o.reduction1 = GetReduction(cfg, p1, m1);
        o.reduction2 = GetReduction(cfg, p2, m2);
        o.taken1 = o.active2 * (1f - o.reduction1);
        o.taken2 = o.active1 * (1f - o.reduction2);

        // Step 4: reflect (reflect damage never triggers another reflect)
        o.reflectRatio1 = GetReflect(cfg, p1, m1);
        o.reflectRatio2 = GetReflect(cfg, p2, m2);
        o.reflected1 = o.taken1 * o.reflectRatio1;
        o.reflected2 = o.taken2 * o.reflectRatio2;

        // Step 5: final damage, defense also reduces incoming reflect
        float f1 = o.taken1 + o.reflected2 * (1f - o.reduction1);
        float f2 = o.taken2 + o.reflected1 * (1f - o.reduction2);

        o.final1 = RoundHalfUp(Mathf.Max(0f, f1));
        o.final2 = RoundHalfUp(Mathf.Max(0f, f2));
        return o;
    }

    static float GetReduction(CombatConfig cfg, PlayerTurn p, float m)
    {
        if (p.action != CombatAction.Defend) return 0f;
        float r = cfg.defendReduction * m * DefenseMod(p.classConfig);
        return Mathf.Clamp(r, 0f, cfg.maxReduction);
    }

    static float GetReflect(CombatConfig cfg, PlayerTurn p, float m)
    {
        if (p.action != CombatAction.Reflect) return 0f;
        float r = cfg.reflectRatio * m * ReflectMod(p.classConfig);
        return Mathf.Clamp(r, 0f, cfg.maxReflect);
    }

    static float DamageMod(ClassConfig c) => c ? c.damageModifier : 1f;
    static float DefenseMod(ClassConfig c) => c ? c.defenseModifier : 1f;
    static float ReflectMod(ClassConfig c) => c ? c.reflectModifier : 1f;

    // Mathf.RoundToInt uses banker's rounding (2.5 -> 2), so round half up manually.
    static int RoundHalfUp(float v) => Mathf.FloorToInt(v + 0.5f);
}