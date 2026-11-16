using UnityEngine;

[CreateAssetMenu(menuName = "ParTea/Class Config")]
public class ClassConfig : ScriptableObject
{
    public string displayName = "ATTACKER";
    [TextArea(2, 5)] public string description;

    [Header("Combat modifiers (1 = no change)")]
    public float damageModifier = 1f;
    public float defenseModifier = 1f;
    public float reflectModifier = 1f;

    [Header("Club")]
    [Tooltip("Roll distance at full power, in meters. The aim line shows this length.")]
    public float maxDistance = 7f;
    [Tooltip("Wall bounces shown on the aim line. 0 = line stops at the first wall.")]
    public int aimBounces = 0;
    [Tooltip("Power meter sweep speed. Higher is harder to time.")]
    public float meterSpeed = 1.2f;
    [Range(0f, 1f)] public float minPower = 0.05f;
    [Tooltip("No longer used. Launch speed is now worked out from Max Distance.")]
    public float maxShotSpeed = 8f;

    [Header("Ball")]
    [Tooltip("Rolling slowdown on the table. Higher stops sooner.")]
    public float ballDamping = 0.6f;
    [Tooltip("Bounce and friction of the ball for this class.")]
    public PhysicsMaterial ballMaterial;

    [Header("Signature action (picks the easy course)")]
    public CombatAction signatureAction = CombatAction.Attack;
    [Tooltip("Keep at 0. Course difficulty comes from the layout, not from bonuses.")]
    public float signatureHoleBonus = 0f;
    [Tooltip("Keep at 1. No meter bonus for the signature action.")]
    public float signatureMeterMultiplier = 1f;
}