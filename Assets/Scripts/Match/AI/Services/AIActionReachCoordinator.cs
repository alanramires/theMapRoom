using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[Flags]
public enum AIReachDecisionStages
{
    None = 0,
    Tactical = 1 << 0,
    Operational = 1 << 1,
    Strategic = 1 << 2
}

public enum AIReachDecisionTier
{
    None = 0,
    Tactical = 1,
    Operational = 2,
    Strategic = 3
}

public readonly struct AIReachDecisionPolicy
{
    public readonly AIReachDecisionStages Stages;
    public readonly int OperationalTurns;

    public AIReachDecisionPolicy(
        AIReachDecisionStages stages,
        int operationalTurns = 2)
    {
        Stages = stages;
        OperationalTurns = Mathf.Max(1, operationalTurns);
    }

    public bool Uses(AIReachDecisionStages stage) =>
        (Stages & stage) != 0;

    public static AIReachDecisionPolicy TacticalOnly =>
        new AIReachDecisionPolicy(AIReachDecisionStages.Tactical);

    public static AIReachDecisionPolicy FieldLogistics =>
        new AIReachDecisionPolicy(
            AIReachDecisionStages.Tactical
            | AIReachDecisionStages.Strategic);

    public static AIReachDecisionPolicy Transport =>
        new AIReachDecisionPolicy(
            AIReachDecisionStages.Tactical
            | AIReachDecisionStages.Operational
            | AIReachDecisionStages.Strategic);

    public static AIReachDecisionPolicy PlannedTransport =>
        new AIReachDecisionPolicy(
            AIReachDecisionStages.Tactical
            | AIReachDecisionStages.Operational
            | AIReachDecisionStages.Strategic);
}

public sealed class AIReachDecisionCandidate<T>
{
    public T Value;
    public Vector3Int ActionCell;
    public Vector3Int TargetCell;
    public float Score;
    public string Reason;
}

public delegate bool AIReachDecisionEvaluator<T>(
    int movementBudget,
    out AIReachDecisionCandidate<T> candidate);

public sealed class AIReachDecisionRequest<T>
{
    public string Context;
    public AIReachDecisionPolicy Policy;
    public int CurrentMovementBudget;
    public int StrategicSearchBudget;
    public AIReachDecisionEvaluator<T> EvaluateTactical;
    public AIReachDecisionEvaluator<T> EvaluateOperational;
    public AIReachDecisionEvaluator<T> EvaluateStrategic;
    public Action<string> DiagnosticLog;
}

public sealed class AIReachDecisionResult<T>
{
    public AIReachDecisionTier Tier;
    public AIReachDecisionCandidate<T> Decision;
    public readonly List<string> Attempts = new List<string>();

    public bool Found => Tier != AIReachDecisionTier.None
                         && Decision != null;
}

/// <summary>
/// Coordena o horizonte de uma decisao sem conhecer sua semantica.
///
/// Tactical consulta a hotzone/sensor do consumidor na rodada atual.
/// Operational consulta Progressao no envelope configurado de turnos.
/// Strategic escolhe uma ancora distante por distancia cubica de hex; a
/// distancia apenas ordena o alvo e nunca substitui caminhos ou sensores.
///
/// VOCABULARIO DE PROJETO / ALIASES ACEITOS:
/// - Tactical = tatico, hotzone, servico de alcance, reach tatico.
/// - Operational = operacional, progressao, reach de progressao,
///   reach operacional.
/// - Strategic = estrategico, router estrategico, seletor distante,
///   reach estrategico.
///
/// Estes nomes descrevem os mesmos tres niveis. Comentarios, relatorios e
/// pedidos de calibracao podem usar qualquer uma dessas formas; consumidores
/// nao devem criar fluxos paralelos apenas por diferenca de nomenclatura.
///
/// O servico e puro: nao move unidades, nao altera ocupacao, FOW, recursos,
/// revisoes, memoria da IA ou estado transacional.
/// </summary>
public static class AIActionReachCoordinator
{
    /// <summary>
    /// Politica geografica compartilhada dos setores da IA.
    /// Aeronaves usam distancia cubica em todos os setores; unidades que
    /// dependem da geografia usam caminhos reais em Tactical/Operational.
    /// A classificacao nao substitui sensores, FOW ou validacao da acao.
    /// </summary>
    public static bool UsesCubicSectorReach(UnitManager unit)
    {
        return unit != null
               && unit.TryGetUnitData(out UnitData data)
               && data != null
               && data.IsAircraft();
    }

    public static int ResolveTacticalBudget(UnitManager unit)
    {
        if (unit == null)
            return 0;
        int remaining = Mathf.Max(0, unit.RemainingMovementPoints);
        return remaining > 0
            ? remaining
            : Mathf.Max(0, unit.MaxMovementPoints);
    }

    public static int ResolveOperationalBudget(
        UnitManager unit,
        int operationalTurns = 2)
    {
        return Mathf.Max(0, unit != null ? unit.MaxMovementPoints : 0)
               * Mathf.Max(1, operationalTurns);
    }

    /// <summary>
    /// Malha de classificacao setorial. Para aeronaves nao abre pathfinding:
    /// materializa apenas as celulas do tabuleiro dentro do raio cubico.
    /// Para as demais unidades preserva custo, dominio e geografia reais.
    ///
    /// `cubicGeometry` e a medicao PEDIDA pelo chamador, e ganha da inferencia
    /// pela ficha: com ele ligado qualquer unidade responde em linha reta. Sem
    /// ele — o padrao, e por isso nenhum chamador existente muda — a geometria
    /// continua saindo de UsesCubicSectorReach.
    /// </summary>
    public static Dictionary<Vector3Int, int> BuildSectorReachMap(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int origin,
        int budget,
        bool cubicGeometry = false)
    {
        origin.z = 0;
        int normalizedBudget = Mathf.Max(0, budget);
        if (!cubicGeometry && !UsesCubicSectorReach(unit))
        {
            return UnitMovementPathRules.CalculateMovementCostMap(
                boardMap,
                unit,
                origin,
                normalizedBudget,
                terrainDatabase);
        }

        return BuildCubicReachMap(boardMap, origin, normalizedBudget);
    }

    /// <summary>
    /// Distancia cubica pura sobre as celulas que existem no tabuleiro. Nao
    /// recebe unidade de proposito: e geometria, nao propriedade de quem mede.
    /// </summary>
    public static Dictionary<Vector3Int, int> BuildCubicReachMap(
        Tilemap boardMap,
        Vector3Int origin,
        int budget)
    {
        origin.z = 0;
        int normalizedBudget = Mathf.Max(0, budget);
        var result = new Dictionary<Vector3Int, int>();
        if (boardMap == null)
            return result;
        BoundsInt bounds = boardMap.cellBounds;
        int minX = Mathf.Max(
            bounds.xMin, origin.x - normalizedBudget * 2);
        int maxX = Mathf.Min(
            bounds.xMax - 1, origin.x + normalizedBudget * 2);
        int minY = Mathf.Max(
            bounds.yMin, origin.y - normalizedBudget);
        int maxY = Mathf.Min(
            bounds.yMax - 1, origin.y + normalizedBudget);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                int distance = CubicDistance(origin, cell);
                if (distance > normalizedBudget
                    || boardMap.GetTile(cell) == null)
                    continue;
                result[cell] = distance;
            }
        }
        return result;
    }

    /// <summary>
    /// Testa se um alvo pertence ao setor Operational.
    /// Aeronaves: cubico puro. Demais unidades: malha de custo real ate MP x2,
    /// aceitando uma origem de ataque dentro do alcance da arma.
    /// </summary>
    public static bool TryResolveOperationalAttackReach(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int origin,
        Vector3Int target,
        int weaponRange,
        out int reachCost,
        out int attackDistance,
        int operationalTurns = 2,
        IReadOnlyDictionary<Vector3Int, int> prebuiltReach = null)
    {
        reachCost = int.MaxValue;
        attackDistance = int.MaxValue;
        if (unit == null)
            return false;

        origin.z = 0;
        target.z = 0;
        int budget = ResolveOperationalBudget(unit, operationalTurns);
        if (UsesCubicSectorReach(unit))
        {
            int distance = CubicDistance(origin, target);
            if (distance > budget)
                return false;
            reachCost = distance;
            attackDistance = distance;
            return true;
        }

        IReadOnlyDictionary<Vector3Int, int> reach =
            prebuiltReach
            ?? BuildSectorReachMap(
                unit,
                boardMap,
                terrainDatabase,
                origin,
                budget);
        int effectiveWeaponRange = Mathf.Max(1, weaponRange);
        foreach (KeyValuePair<Vector3Int, int> candidate in reach)
        {
            int distance = CubicDistance(candidate.Key, target);
            if (distance > effectiveWeaponRange)
                continue;
            if (candidate.Value < reachCost
                || (candidate.Value == reachCost
                    && distance < attackDistance))
            {
                reachCost = candidate.Value;
                attackDistance = distance;
            }
        }
        return reachCost != int.MaxValue && reachCost <= budget;
    }

    public static AIReachDecisionResult<T> Evaluate<T>(
        AIReachDecisionRequest<T> request)
    {
        var result = new AIReachDecisionResult<T>();
        if (request == null)
            return result;

        int tacticalBudget = Mathf.Max(
            0, request.CurrentMovementBudget);
        int operationalBudget = tacticalBudget
            * Mathf.Max(1, request.Policy.OperationalTurns);
        int strategicBudget = request.StrategicSearchBudget > 0
            ? request.StrategicSearchBudget
            : int.MaxValue;

        if (TryTier(
                request,
                result,
                AIReachDecisionStages.Tactical,
                AIReachDecisionTier.Tactical,
                tacticalBudget,
                request.EvaluateTactical))
            return result;

        if (TryTier(
                request,
                result,
                AIReachDecisionStages.Operational,
                AIReachDecisionTier.Operational,
                operationalBudget,
                request.EvaluateOperational))
            return result;

        TryTier(
            request,
            result,
            AIReachDecisionStages.Strategic,
            AIReachDecisionTier.Strategic,
            strategicBudget,
            request.EvaluateStrategic);
        return result;
    }

    // Converte offset even-r do Tilemap para coordenadas cubicas e retorna
    // max(|dx|, |dy|, |dz|), equivalente ao numero minimo de passos hex.
    // Esta e a metrica obrigatoria do reach estrategico.
    public static int CubicDistance(Vector3Int a, Vector3Int b)
    {
        a.z = 0;
        b.z = 0;
        int ax = a.x - (a.y - (a.y & 1)) / 2;
        int az = a.y;
        int ay = -ax - az;
        int bx = b.x - (b.y - (b.y & 1)) / 2;
        int bz = b.y;
        int by = -bx - bz;
        return Mathf.Max(
            Mathf.Abs(ax - bx),
            Mathf.Abs(ay - by),
            Mathf.Abs(az - bz));
    }

    private static bool TryTier<T>(
        AIReachDecisionRequest<T> request,
        AIReachDecisionResult<T> result,
        AIReachDecisionStages stage,
        AIReachDecisionTier tier,
        int budget,
        AIReachDecisionEvaluator<T> evaluator)
    {
        if (!request.Policy.Uses(stage))
        {
            Record(request, result, $"{tier}:disabled");
            return false;
        }

        if (evaluator == null)
        {
            Record(request, result, $"{tier}:no_evaluator");
            return false;
        }

        bool found = evaluator(
            budget, out AIReachDecisionCandidate<T> candidate);
        if (!found || candidate == null)
        {
            Record(request, result, $"{tier}:miss budget={budget}");
            return false;
        }

        candidate.ActionCell.z = 0;
        candidate.TargetCell.z = 0;
        result.Tier = tier;
        result.Decision = candidate;
        Record(
            request,
            result,
            $"{tier}:hit budget={budget} action={candidate.ActionCell} " +
            $"target={candidate.TargetCell} score={candidate.Score:F0} " +
            $"reason={candidate.Reason ?? "-"}");
        return true;
    }

    private static void Record<T>(
        AIReachDecisionRequest<T> request,
        AIReachDecisionResult<T> result,
        string message)
    {
        string context = string.IsNullOrWhiteSpace(request.Context)
            ? "Decision"
            : request.Context;
        string line = $"[AI Reach][{context}] {message}";
        result.Attempts.Add(line);
        request.DiagnosticLog?.Invoke(line);
    }
}
