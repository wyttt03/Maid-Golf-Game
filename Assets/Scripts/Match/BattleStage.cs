using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Plays the chibi battle sequence on the stage:
// 1) both maids cast at the same time, 2) both action particles play,
// 3) Attack and Reflect maids play Hit, Defend maids go back to Idle.
// Each slot holds one child per class; only the chosen one is active.
public class BattleStage : MonoBehaviour
{
    [Header("Slots (parents of the class maids)")]
    public Transform p1Slot;
    public Transform p2Slot;

    [Header("Particles (order: 0 Attack, 1 Defend, 2 Reflect)")]
    public ParticleSystem[] p1Effects = new ParticleSystem[3];
    public ParticleSystem[] p2Effects = new ParticleSystem[3];

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
    [Tooltip("On: Attack / Reflect maids play Hit only if they took damage. Off: always, by matchup.")]
    public bool hitOnlyWhenDamaged = true;

    [Header("Sound hooks")]
    public UnityEvent onAttack;
    public UnityEvent onDefend;
    public UnityEvent onReflect;
    public UnityEvent onParticles;
    public UnityEvent onHit;

    CombatAction action1, action2;

    // Steps 1 and 2: cast together, then particles together.
    public IEnumerator PlayActions(CombatAction a1, bool holed1, CombatAction a2, bool holed2)
    {
        action1 = a1;
        action2 = a2;
        bool cast1 = holed1 || castOnMiss;
        bool cast2 = holed2 || castOnMiss;
        if (!cast1 && !cast2) yield break;

        // 1. Cast
        if (cast1) { Trigger(p1Slot, ActionTrigger(a1)); ActionEvent(a1).Invoke(); }
        if (cast2) { Trigger(p2Slot, ActionTrigger(a2)); ActionEvent(a2).Invoke(); }
        yield return new WaitForSeconds(castTime);

        // 2. Particles
        if (cast1) PlayEffect(p1Effects, a1);
        if (cast2) PlayEffect(p2Effects, a2);
        onParticles.Invoke();
        yield return new WaitForSeconds(particleTime);
    }

    // Step 3: Defend maids stay idle, Attack and Reflect maids take the hit.
    // damaged1 / damaged2 = that player took damage this round.
    public IEnumerator PlayHits(bool damaged1, bool damaged2)
    {
        bool hit1 = action1 != CombatAction.Defend && (damaged1 || !hitOnlyWhenDamaged);
        bool hit2 = action2 != CombatAction.Defend && (damaged2 || !hitOnlyWhenDamaged);

        Trigger(p1Slot, hit1 ? hitTrigger : idleTrigger);
        Trigger(p2Slot, hit2 ? hitTrigger : idleTrigger);

        if (hit1 || hit2) onHit.Invoke();
        yield return new WaitForSeconds(hitTime);
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