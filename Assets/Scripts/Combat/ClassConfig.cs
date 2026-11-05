using UnityEngine;

[CreateAssetMenu(menuName = "ParTea/Class Config")]
public class ClassConfig : ScriptableObject
{
    public string displayName = "Attacker";
    [TextArea(2, 5)] public string description;

    [Header("Combat modifiers (1 = no change)")]
    public float damageModifier = 1f;
    public float defenseModifier = 1f;
    public float reflectModifier = 1f;

    [Header("Spoon (club)")]
    [Tooltip("Ball speed at full power.")]
    public float maxShotSpeed = 8f;
    [Tooltip("Power meter sweep speed. Higher is harder to time.")]
    public float meterSpeed = 1.2f;
    [Range(0f, 1f)] public float minPower = 0.05f;

    [Header("Ball")]
    [Tooltip("Rolling slowdown on the table.")]
    public float ballDamping = 0.6f;
    [Tooltip("Bounce and friction of the ball for this class.")]
    public PhysicsMaterial ballMaterial;

    [Header("Signature action bonus")]
    public CombatAction signatureAction = CombatAction.Attack;
    [Tooltip("Extra cup radius in meters when playing the signature action.")]
    public float signatureHoleBonus = 0f;
    [Tooltip("Meter speed multiplier when playing the signature action. Below 1 is easier.")]
    public float signatureMeterMultiplier = 1f;
}