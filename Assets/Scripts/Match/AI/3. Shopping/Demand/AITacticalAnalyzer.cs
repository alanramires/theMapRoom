using System.Collections.Generic;
using UnityEngine;
// --------------------------------------------------------------------------------------------
// Analisador T�tico da IA: Identifica��o de Necessidades Operacionais
// O AITacticalAnalyzer � respons�vel por analisar o estado atual do jogo e identificar as necessidades t�ticas
// para cada equipe, como defesa de base, defesa de setor, opera��es de captura, suporte a�reo, etc. 
// Ele leva em considera��o a intelig�ncia de campo, amea�as vis�veis, planos de objetivos 
// e a composi��o atual do ex�rcito para gerar uma lista de necessidades t�ticas
// que ser�o usadas pelo AIShoppingPlanner para decidir quais unidades comprar e quais opera��es priorizar.
// O AITacticalAnalyzer � um singleton acess�vel globalmente, permitindo que outros sistemas de IA 
// consultem as necessidades t�ticas atuais para cada equipe. Ele � reconstru�do a cada turno
// com base no estado mais recente do jogo e dos planos de objetivos.
// --------------------------------------------------------------------------------------------

public partial class AITacticalAnalyzer : MonoBehaviour
{
    private const int HomeThreatRange = 3;
    private const int SectorDefenseRange = 3;
    private const int DefensiveArmorThreatRange = 3;
    private const int AirRefuelLowFuelPct = 35;
    private const int AirRefuelCriticalFuelPct = 20;

    private static AITacticalAnalyzer instance;
    public static AITacticalAnalyzer Instance => EnsureInstance();

    private readonly Dictionary<int, List<AITacticalNeed>> operationsBySlot = new Dictionary<int, List<AITacticalNeed>>();
    private int nextId = 1;

    private static AITacticalAnalyzer EnsureInstance()
    {
        if (instance != null) return instance;
        instance = FindAnyObjectByType<AITacticalAnalyzer>();
        if (instance != null) return instance;
        GameObject go = new GameObject(nameof(AITacticalAnalyzer));
        instance = go.AddComponent<AITacticalAnalyzer>();
        DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public void Rebuild(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        var ops = new List<AITacticalNeed>();
        operationsBySlot[snapshot != null ? snapshot.AISlotIndex : AIController.ResolveAISlotKey(team)] = ops;
        if (snapshot == null)
            return;

        AIIntelReport intel = BuildOperationIntelReport(team, snapshot);

        TryBuildBaseDefenseOp(team, snapshot, plan, ops, intel);
        TryBuildSectorDefenseOps(team, snapshot, plan, ops, intel);
        TryBuildGroundCaptureOps(team, snapshot, plan, ops, intel);
        TryBuildEarlyAxisAirliftOps(team, snapshot, ops);
        TryBuildAirliftCaptureOps(team, snapshot, plan, ops, intel);
        TryBuildAirRefuelSupportOp(team, snapshot, ops);
        TryBuildPreventiveDefenseOp(team, snapshot, ops);

        AssignExistingUnitsToOperations(team, snapshot, ops);
        InferCohesionForAllOps(snapshot, ops);
        InferPhasesForAllOps(snapshot, ops);
        LogOperations(team, snapshot.TurnNumber, ops);
    }

    public IReadOnlyList<AITacticalNeed> GetOperationsForTeam(TeamId team)
    {
        if (operationsBySlot.TryGetValue(AIController.ResolveAISlotKey(team), out List<AITacticalNeed> ops))
            return ops;
        return System.Array.Empty<AITacticalNeed>();
    }

    public bool TryGetCaptureOperationForObjective(TeamId team, SectorObjective objective, out AITacticalNeed operation)
    {
        operation = null;
        if (objective == null || !operationsBySlot.TryGetValue(AIController.ResolveAISlotKey(team), out List<AITacticalNeed> ops))
            return false;

        foreach (AITacticalNeed op in ops)
        {
            if (op == null || !IsCaptureOperation(op))
                continue;
            if (op.LinkedObjective == objective || op.Sector == objective.Sector)
            {
                operation = op;
                return true;
            }
        }

        return false;
    }

    public bool IsFireSupportScreenedForObjective(UnitManager fireSupport, TeamId team, SectorObjective objective, out AITacticalNeed operation, out string reason)
    {
        operation = null;
        reason = "";
        if (fireSupport == null || objective == null)
            return true;
        if (!TryGetCaptureOperationForObjective(team, objective, out operation))
            return true;

        Vector3Int fireCell = Normalize(fireSupport.CurrentCellPosition);
        float fireDist = SectorManager.HexDistance(fireCell, operation.TargetCell);
        UnitManager screen = FindBestScreenUnitForOperation(operation, fireSupport.InstanceId);
        if (screen == null)
        {
            reason = $"sem screen minimo em {operation.Type} {operation.Sector}";
            return false;
        }

        float screenDist = SectorManager.HexDistance(Normalize(screen.CurrentCellPosition), operation.TargetCell);
        bool screened = screenDist <= fireDist + 0.5f;
        reason = screened
            ? $"screen #{screen.InstanceId} dist={screenDist:F1} <= fire={fireDist:F1}"
            : $"screen #{screen.InstanceId} atrasado dist={screenDist:F1} > fire={fireDist:F1}";
        return screened;
    }

    public List<TacticalDeficit> GetDeficits(TeamId team, bool log = true)
    {
        var deficits = new List<TacticalDeficit>();
        if (!operationsBySlot.TryGetValue(AIController.ResolveAISlotKey(team), out List<AITacticalNeed> ops))
            return deficits;

        ops.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        foreach (AITacticalNeed op in ops)
        {
            var counts = new Dictionary<AINeedKind, int>();
            foreach (AITacticalSlotNeed slot in op.RequiredSlots)
            {
                if (slot.Filled) continue;
                counts.TryGetValue(slot.Kind, out int count);
                counts[slot.Kind] = count + 1;
            }

            foreach (KeyValuePair<AINeedKind, int> kv in counts)
            {
                if (kv.Value <= 0) continue;
                deficits.Add(new TacticalDeficit
                {
                    Operation = op,
                    Kind = kv.Key,
                    Count = kv.Value,
                });
                if (log)
                    Debug.Log($"[AI Ops][T{op.LastUpdatedTurn}][{team}] deficit {op.Type} {op.Sector}: {kv.Key}x{kv.Value}");
            }
        }

        return deficits;
    }

    private AITacticalNeed CreateOperation(TeamId team, AITacticalNeedType type, int priority, AIWorldSnapshot snapshot, ConstructionSector sector)
    {
        int turn = snapshot != null ? snapshot.TurnNumber : 0;
        return new AITacticalNeed
        {
            Id = nextId++,
            Type = type,
            Phase = AITacticalNeedPhase.Forming,
            Sector = sector,
            Team = team,
            Priority = priority,
            CreatedTurn = turn,
            LastUpdatedTurn = turn,
        };
    }

    private static int GetCaptureOperationPriority(SectorObjective objective)
    {
        int objectivePriority = objective != null ? Mathf.Max(1, objective.Priority) : 99;
        return 3 + objectivePriority;
    }

    private static Vector3Int Normalize(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }
}
