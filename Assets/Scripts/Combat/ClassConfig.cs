using UnityEngine;

[CreateAssetMenu(menuName = "ParTea/Class Config")]
public class ClassConfig : ScriptableObject
{
    public string displayName = "Attacker";

    [Header("Combat modifiers (1 = no change)")]
    public float damageModifier = 1f;
    public float defenseModifier = 1f;
    public float reflectModifier = 1f;
}