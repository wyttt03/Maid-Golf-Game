using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Plays the chibi battle sequence on the stage:
// 1) both maids cast at the same time, 2) both action particles play,
// 3) maids that cast Defend skip the hit reaction.
// Each slot holds one child per class; only the chosen one is active.
public class BattleStage : MonoBehaviour
{
    [Header("Slots (parents of the class maids)")]
    public Transform p1Slot;
    public Transform p2Slot;

    [Header("Particles (order: 0 Attack, 1 Defend, 2 Reflect)")]
    public ParticleSystem[] p1Effects = new ParticleSystem[3];
    public ParticleSystem[] p2Effects = new ParticleSystem[3];

    [Header("Maid magic")]
    public MaidMagic magic = new MaidMagic();
    MaidBolt bolt1, bolt2;
    MaidShield shield1, shield2;
    bool earlyHit1, earlyHit2, returned1, returned2;

    [Header("Animator trigger names")]
    public string idleTrigger = "Idle";
    public string readyTrigger = "Ready";
    public string attackTrigger = "Attack";
    public string defendTrigger = "Defend";
    public string reflectTrigger = "Reflect";
    public string hitTrigger = "Hit";
    public string winTrigger = "Win";
    public string loseTrigger = "Lose";

    [Header("Timing (seconds)")]
    [Tooltip("Length of the cast animations before the particles start.")]
    public float castTime = 0.6f;
    [Tooltip("Length of the particles before the hit step.")]
    public float particleTime = 0.8f;
    [Tooltip("Length of the hit step before the result panel.")]
    public float hitTime = 0.6f;

    [Header("Rules")]
    [Tooltip("On: a maid who missed both strokes still casts. Off: she stays idle and plays no particles.")]
    public bool castOnMiss = false;
    [Tooltip("Require damage for a hit reaction. Maids that cast Defend skip the reaction either way.")]
    public bool hitOnlyWhenDamaged = true;

    [Header("Sound hooks")]
    public UnityEvent onAttack;
    public UnityEvent onDefend;
    public UnityEvent onReflect;
    public UnityEvent onParticles;
    public UnityEvent onHit;

    bool defended1, defended2;

    // Steps 1 and 2: cast together, then particles together.
    public IEnumerator PlayActions(CombatAction a1, bool holed1, CombatAction a2, bool holed2,
        bool damaged1 = true, bool damaged2 = true)
    {
        ClearBolts();
        bool cast1 = holed1 || castOnMiss;
        bool cast2 = holed2 || castOnMiss;
        defended1 = cast1 && a1 == CombatAction.Defend;
        defended2 = cast2 && a2 == CombatAction.Defend;
        if (!cast1 && !cast2) yield break;

        // 1. Cast
        if (cast1) { Trigger(p1Slot, ActionTrigger(a1)); ActionEvent(a1).Invoke(); }
        if (cast2) { Trigger(p2Slot, ActionTrigger(a2)); ActionEvent(a2).Invoke(); }
        returned1 = cast2 && a2 == CombatAction.Reflect;
        returned2 = cast1 && a1 == CombatAction.Reflect;
        float lead = returned1 || returned2 ? Mathf.Min(MaidShield.CastLead, Mathf.Max(0f, castTime)) : 0f;
        yield return new WaitForSeconds(castTime - lead);

        var maid1 = p1Slot ? p1Slot.GetComponentInChildren<Animator>() : null;
        var maid2 = p2Slot ? p2Slot.GetComponentInChildren<Animator>() : null;
        if (returned2) shield1 = RaiseShield(maid1, p1Slot, p2Slot);
        if (returned1) shield2 = RaiseShield(maid2, p2Slot, p1Slot);
        for (float age = 0f; age < lead; age += Time.deltaTime)
        {
            shield1?.FollowMaid();
            shield2?.FollowMaid();
            shield1?.Draw(age, -1f, false);
            shield2?.Draw(age, -1f, false);
            yield return null;
        }

        // Sample the same contact point as the incoming bolt on this frame.
        shield1?.FollowMaid();
        shield2?.FollowMaid();
        if (cast1) bolt1 = Launch(p1Slot, maid1, p2Slot, maid2, p1Effects, a1, returned1);
        if (cast2) bolt2 = Launch(p2Slot, maid2, p1Slot, maid1, p2Effects, a2, returned2);
        onParticles.Invoke();
        if (bolt1 == null && bolt2 == null && shield1 == null && shield2 == null)
        {
            yield return new WaitForSeconds(particleTime);
            yield break;
        }
        // Both flights share a clock: a ricochet touches the shield halfway through.
        float elapsed = 0f;
        bool shieldContact = false;
        while (true)
        {
            float t = particleTime > 0f ? Mathf.Clamp01(elapsed / particleTime) : 1f;
            float age = lead + t * particleTime;
            shield1?.Draw(age, t, bolt2 != null);
            shield2?.Draw(age, t, bolt1 != null);
            bolt1?.DrawFlight(t);
            bolt2?.DrawFlight(t);
            if (!shieldContact && t >= 0.5f)
            {
                shieldContact = true;
                earlyHit1 = bolt2 != null && returned2 && (damaged1 || !hitOnlyWhenDamaged);
                earlyHit2 = bolt1 != null && returned1 && (damaged2 || !hitOnlyWhenDamaged);
                if (earlyHit1) Trigger(p1Slot, hitTrigger);
                if (earlyHit2) Trigger(p2Slot, hitTrigger);
                if (earlyHit1 || earlyHit2) onHit.Invoke();
            }
            if (t >= 1f) break;
            yield return null;
            elapsed += Time.deltaTime;
        }
    }

    // Step 3: only a defense that was actually cast suppresses the hit reaction.
    // damaged1 / damaged2 = that player took damage this round.
    public IEnumerator PlayHits(bool damaged1, bool damaged2)
    {
        bool hit1 = !defended1 && (damaged1 || !hitOnlyWhenDamaged);
        bool hit2 = !defended2 && (damaged2 || !hitOnlyWhenDamaged);

        if (!earlyHit1) Trigger(p1Slot, hit1 ? hitTrigger : idleTrigger);
        if (!earlyHit2) Trigger(p2Slot, hit2 ? hitTrigger : idleTrigger);
        if ((hit1 && !earlyHit1) || (hit2 && !earlyHit2)) onHit.Invoke();

        if (bolt1 == null && bolt2 == null)
        {
            yield return new WaitForSeconds(hitTime);
            yield break;
        }
        for (float elapsed = 0f; elapsed < hitTime; elapsed += Time.deltaTime)
        {
            bolt1?.DrawImpact(elapsed, returned1 ? hit1 : hit2);
            bolt2?.DrawImpact(elapsed, returned2 ? hit2 : hit1);
            yield return null;
        }
        ClearBolts();
    }

    public void PlayReady(bool isP1) => Trigger(isP1 ? p1Slot : p2Slot, readyTrigger);
    public void PlayIdle(bool isP1) => Trigger(isP1 ? p1Slot : p2Slot, idleTrigger);

    // winner: 0 = draw, 1 = P1, 2 = P2
    public void PlayEnd(int winner)
    {
        if (winner == 0) return;
        Trigger(p1Slot, winner == 1 ? winTrigger : loseTrigger);
        Trigger(p2Slot, winner == 2 ? winTrigger : loseTrigger);
    }

    MaidShield RaiseShield(Animator maid, Transform slot, Transform opposite)
    {
        var style = magic?.For(maid);
        if (style == null || !style.shield || !opposite) return null;
        float incoming = Mathf.Sign(slot.position.x - opposite.position.x);
        return new MaidShield(magic, style, transform, maid, slot, incoming);
    }

    MaidBolt Launch(Transform source, Animator caster, Transform target, Animator receiver,
        ParticleSystem[] fallback, CombatAction action, bool reflect)
    {
        var style = magic?.For(caster);
        if (target && style != null && style.core && style.solid && style.outline &&
            magic.star && magic.thinStar && magic.cometMist)
            return new MaidBolt(magic, style, transform, source, caster, target, receiver, reflect);
        PlayEffect(fallback, action);
        return null;
    }

    void ClearBolts()
    {
        shield1?.Dispose();
        shield2?.Dispose();
        shield1 = shield2 = null;
        bolt1?.Dispose();
        bolt2?.Dispose();
        bolt1 = bolt2 = null;
        earlyHit1 = earlyHit2 = false;
    }

    void OnDisable() => ClearBolts();

    static void PlayEffect(ParticleSystem[] effects, CombatAction a)
    {
        int i = (int)a;
        if (effects == null || i >= effects.Length || !effects[i]) return;
        effects[i].gameObject.SetActive(true);
        effects[i].Play(true);
    }

    string ActionTrigger(CombatAction a)
    {
        switch (a)
        {
            case CombatAction.Attack: return attackTrigger;
            case CombatAction.Defend: return defendTrigger;
            default: return reflectTrigger;
        }
    }

    UnityEvent ActionEvent(CombatAction a)
    {
        switch (a)
        {
            case CombatAction.Attack: return onAttack;
            case CombatAction.Defend: return onDefend;
            default: return onReflect;
        }
    }

    static void Trigger(Transform slot, string trigger)
    {
        if (!slot || string.IsNullOrEmpty(trigger)) return;

        // Inactive children are skipped, so this finds the chosen class maid.
        var animator = slot.GetComponentInChildren<Animator>();
        if (!animator || animator.runtimeAnimatorController == null) return;

        // Animations not made yet are skipped silently.
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
            {
                animator.SetTrigger(trigger);
                return;
            }
    }
}