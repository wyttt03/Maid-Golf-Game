using UnityEngine;

[CreateAssetMenu(menuName = "ParTea/Combat Config")]
public class CombatConfig : ScriptableObject
{
    [Header("HP")]
    public int startHP = 100;

    [Header("Base active damage")]
    public float attackBase = 30f;
    public float defendBase = 10f;
    public float reflectBase = 10f;

    [Header("Special effects")]
    [Range(0f, 1f)] public float defendReduction = 0.5f;
    public float reflectRatio = 0.8f;
    [Range(0f, 1f)] public float maxReduction = 0.9f;
    public float maxReflect = 1.5f;

    [Header("Stroke multipliers")]
    public float holeInOneMultiplier = 1.5f;
    public float twoStrokesMultiplier = 1.0f;
    public float failedMultiplier = 0f;

    public float GetMultiplier(StrokeResult result)
    {
        switch (result)
        {
            case StrokeResult.HoleInOne: return holeInOneMultiplier;
            case StrokeResult.TwoStrokes: return twoStrokesMultiplier;
            default: return failedMultiplier;
        }
    }

    public float GetBaseDamage(CombatAction action)
    {
        switch (action)
        {
            case CombatAction.Attack: return attackBase;
            case CombatAction.Defend: return defendBase;
            default: return reflectBase;
        }
    }
}