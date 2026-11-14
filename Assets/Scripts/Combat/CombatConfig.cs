using UnityEngine;

[CreateAssetMenu(menuName = "ParTea/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Header("HP")]
    public int startHP = 100;

    [Header("Damage dealt by stroke result")]
    public int holeInOneDamage = 40;
    public int twoStrokesDamage = 25;
    public int failedDamage = 0;

    [Header("Counter")]
    [Tooltip("Extra damage when your action counters the rival's and you hole the ball.")]
    public int counterBonusDamage = 10;

    public int GetDamage(StrokeResult result)
    {
        switch (result)
        {
            case StrokeResult.HoleInOne: return holeInOneDamage;
            case StrokeResult.TwoStrokes: return twoStrokesDamage;
            default: return failedDamage;
        }
    }

    // Attack beats Defend, Defend beats Reflect, Reflect beats Attack.
    public static bool Counters(CombatAction mine, CombatAction theirs)
    {
        return (mine == CombatAction.Attack && theirs == CombatAction.Defend)
            || (mine == CombatAction.Defend && theirs == CombatAction.Reflect)
            || (mine == CombatAction.Reflect && theirs == CombatAction.Attack);
    }

    // Legacy helpers kept so older UI code still compiles. Not used by the current rules.
    public float GetMultiplier(StrokeResult result)
    {
        return result == StrokeResult.HoleInOne ? 1.5f : result == StrokeResult.TwoStrokes ? 1f : 0f;
    }

    public float GetBaseDamage(CombatAction action)
    {
        return twoStrokesDamage;
    }
}