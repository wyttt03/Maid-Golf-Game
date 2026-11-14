using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Plays the chibi battle animations on the stage.
// Each slot holds one child per class; only the chosen one is active.
public class BattleStage : MonoBehaviour
{
    [Header("Slots (parents of the class maids)")]
    public Transform p1Slot;
    public Transform p2Slot;

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
    [Tooltip("Time given to the action animations before damage lands.")]
    public float actionTime = 1.0f;
    [Tooltip("Time given to the hit animations before the result panel.")]
    public float hitTime = 0.6f;

    [Header("Sound hooks")]
    public UnityEvent onAttack;
    public UnityEvent onDefend;
    public UnityEvent onReflect;
    public UnityEvent onHit;

    // A failed shot means no skill: that maid stays idle.
    public IEnumerator PlayActions(CombatAction a1, bool cast1, CombatAction a2, bool cast2)
    {
        if (cast1) { Trigger(p1Slot, ActionTrigger(a1)); ActionEvent(a1).Invoke(); }
        if (cast2) { Trigger(p2Slot, ActionTrigger(a2)); ActionEvent(a2).Invoke(); }
        if (cast1 || cast2) yield return new WaitForSeconds(actionTime);
    }

    public IEnumerator PlayHits(bool hit1, bool hit2)
    {
        if (hit1) Trigger(p1Slot, hitTrigger);
        if (hit2) Trigger(p2Slot, hitTrigger);
        if (hit1 || hit2)
        {
            onHit.Invoke();
            yield return new WaitForSeconds(hitTime);
        }
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
        if (!HasTrigger(animator, trigger)) return;

        animator.SetTrigger(trigger);
    }

    static bool HasTrigger(Animator animator, string name)
    {
        foreach (var p in animator.parameters)
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == name) return true;
        return false;
    }
}