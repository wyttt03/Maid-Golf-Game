using UnityEngine;

public class CombatSandbox : MonoBehaviour
{
    public CombatConfig config;

    [Header("Player 1")]
    public ClassConfig p1Class;
    public CombatAction p1Action;
    public StrokeResult p1Result = StrokeResult.TwoStrokes;

    [Header("Player 2")]
    public ClassConfig p2Class;
    public CombatAction p2Action;
    public StrokeResult p2Result = StrokeResult.TwoStrokes;

    [ContextMenu("Resolve Current Setup")]
    void ResolveCurrent()
    {
        var o = CombatResolver.Resolve(config,
            new PlayerTurn { action = p1Action, result = p1Result, classConfig = p1Class },
            new PlayerTurn { action = p2Action, result = p2Result, classConfig = p2Class });
        Debug.Log(o.ToString());
    }

    // Verifies the six cases from GDD section 7.3 (100% multiplier, no class modifiers).
    [ContextMenu("Run GDD Cases")]
    void RunDocCases()
    {
        Check("A Attack vs Attack", CombatAction.Attack, CombatAction.Attack, 30, 30);
        Check("B Attack vs Defend", CombatAction.Attack, CombatAction.Defend, 10, 15);
        Check("C Attack vs Reflect", CombatAction.Attack, CombatAction.Reflect, 34, 30);
        Check("D Defend vs Defend", CombatAction.Defend, CombatAction.Defend, 5, 5);
        Check("E Defend vs Reflect", CombatAction.Defend, CombatAction.Reflect, 9, 10);
        Check("F Reflect vs Reflect", CombatAction.Reflect, CombatAction.Reflect, 18, 18);
    }

    void Check(string label, CombatAction a1, CombatAction a2, int expect1, int expect2)
    {
        var o = CombatResolver.Resolve(config,
            new PlayerTurn { action = a1, result = StrokeResult.TwoStrokes },
            new PlayerTurn { action = a2, result = StrokeResult.TwoStrokes });
        bool ok = o.final1 == expect1 && o.final2 == expect2;
        string msg = $"{label}: P1 takes {o.final1} (expect {expect1}), P2 takes {o.final2} (expect {expect2})";
        if (ok) Debug.Log("PASS " + msg); else Debug.LogError("FAIL " + msg);
    }
}