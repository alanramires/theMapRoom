using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Politica obrigatoria de utilidade. O servico nao possui pesos implicitos:
/// cada consumidor declara o que considera uma boa posicao de observacao.
/// </summary>
public sealed class VisionCoverageScoringPolicy
{
    public readonly float VisibleWeight;
    public readonly float MarginalWeight;
    public readonly float UnexploredMarginalWeight;
    public readonly float RecoveredWeight;
    public readonly float RetainedUniqueWeight;
    public readonly float OverlapWeight;
    public readonly float LostUniqueWeight;
    public readonly float FocusWeight;
    public readonly float LostFocusedWeight;
    public readonly float MovementCostWeight;

    public VisionCoverageScoringPolicy(
        float visibleWeight,
        float marginalWeight,
        float unexploredMarginalWeight,
        float recoveredWeight,
        float retainedUniqueWeight,
        float overlapWeight,
        float lostUniqueWeight,
        float focusWeight,
        float lostFocusedWeight,
        float movementCostWeight)
    {
        VisibleWeight = visibleWeight;
        MarginalWeight = marginalWeight;
        UnexploredMarginalWeight = unexploredMarginalWeight;
        RecoveredWeight = recoveredWeight;
        RetainedUniqueWeight = retainedUniqueWeight;
        OverlapWeight = overlapWeight;
        LostUniqueWeight = lostUniqueWeight;
        FocusWeight = focusWeight;
        LostFocusedWeight = lostFocusedWeight;
        MovementCostWeight = movementCostWeight;
    }
}

public sealed class VisionCoverageEvaluationRequest
{
    public ISet<Vector3Int> CandidateCoverage;
    public ISet<Vector3Int> OriginCoverage;
    public ISet<Vector3Int> AlliedCoverageWithoutObserver;
    public ISet<Vector3Int> FocusCells;
    public Func<Vector3Int, bool> IsKnown;
    public Func<Vector3Int, bool> IsExplored;
    public VisionCoverageScoringPolicy ScoringPolicy;
    public bool HasTeamContext;
    public int MovementCost;
}

public sealed class VisionCoverageEvaluation
{
    public readonly HashSet<Vector3Int> GainedFromOriginCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> LostFromOriginCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> MarginalCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> OverlapCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> UnexploredMarginalCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> RecoveredCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> RetainedUniqueCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> LostUniqueCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> FocusRevealedCells = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> LostFocusedCells = new HashSet<Vector3Int>();

    public int VisibleCount;
    public float Score;
    public int DisplayScore;
    public string Reason;
}

public static class VisionCoverageEvaluator
{
    public static VisionCoverageEvaluation Evaluate(
        VisionCoverageEvaluationRequest request)
    {
        var result = new VisionCoverageEvaluation();
        if (request == null
            || request.CandidateCoverage == null
            || request.OriginCoverage == null
            || request.ScoringPolicy == null)
        {
            result.Reason =
                "Avaliacao incompleta: coberturas e politica de score sao obrigatorias.";
            return result;
        }

        ISet<Vector3Int> allies =
            request.AlliedCoverageWithoutObserver
            ?? EmptyCells;
        ISet<Vector3Int> focus = request.FocusCells ?? EmptyCells;
        var currentKnown = new HashSet<Vector3Int>(allies);
        currentKnown.UnionWith(request.OriginCoverage);

        foreach (Vector3Int cell in request.CandidateCoverage)
        {
            result.VisibleCount++;
            if (!request.OriginCoverage.Contains(cell))
                result.GainedFromOriginCells.Add(cell);
            if (allies.Contains(cell))
                result.OverlapCells.Add(cell);
            else
                result.MarginalCells.Add(cell);
            if (focus.Contains(cell))
                result.FocusRevealedCells.Add(cell);
        }

        var originUnique = new HashSet<Vector3Int>(request.OriginCoverage);
        originUnique.ExceptWith(allies);
        foreach (Vector3Int cell in request.OriginCoverage)
        {
            if (!request.CandidateCoverage.Contains(cell))
                result.LostFromOriginCells.Add(cell);
        }
        foreach (Vector3Int cell in originUnique)
        {
            if (request.CandidateCoverage.Contains(cell))
                result.RetainedUniqueCells.Add(cell);
            else
            {
                result.LostUniqueCells.Add(cell);
                if (focus.Contains(cell))
                    result.LostFocusedCells.Add(cell);
            }
        }

        if (request.IsExplored != null)
        {
            foreach (Vector3Int cell in result.MarginalCells)
            {
                if (!request.IsExplored(cell))
                    result.UnexploredMarginalCells.Add(cell);
                else if (!(request.IsKnown != null
                    ? request.IsKnown(cell)
                    : currentKnown.Contains(cell)))
                    result.RecoveredCells.Add(cell);
            }
        }

        VisionCoverageScoringPolicy p = request.ScoringPolicy;
        int effectiveMarginal = request.HasTeamContext
            ? result.MarginalCells.Count
            : result.GainedFromOriginCells.Count;
        result.Score =
            result.VisibleCount * p.VisibleWeight
            + effectiveMarginal * p.MarginalWeight
            + result.UnexploredMarginalCells.Count * p.UnexploredMarginalWeight
            + result.RecoveredCells.Count * p.RecoveredWeight
            + result.RetainedUniqueCells.Count * p.RetainedUniqueWeight
            + result.OverlapCells.Count * p.OverlapWeight
            + result.LostUniqueCells.Count * p.LostUniqueWeight
            + result.FocusRevealedCells.Count * p.FocusWeight
            + result.LostFocusedCells.Count * p.LostFocusedWeight
            + request.MovementCost * p.MovementCostWeight;
        result.DisplayScore = Mathf.RoundToInt(result.Score);
        result.Reason =
            $"vis={result.VisibleCount} ganho={result.GainedFromOriginCells.Count} "
            + $"marginal={result.MarginalCells.Count} overlap={result.OverlapCells.Count} "
            + $"novo={result.UnexploredMarginalCells.Count} recuperado={result.RecoveredCells.Count} "
            + $"mantem={result.RetainedUniqueCells.Count} perde={result.LostUniqueCells.Count} "
            + $"foco={result.FocusRevealedCells.Count} custo={request.MovementCost}";
        return result;
    }

    private static readonly HashSet<Vector3Int> EmptyCells =
        new HashSet<Vector3Int>();
}

public sealed class MelhorVisaoRequest
{
    public UnitManager Unit;
    public Tilemap Map;
    public TerrainDatabase TerrainDatabase;
    public DPQAirHeightConfig DpqAirHeightConfig;
    public VisionCoverageLayer Layer;
    public VisionCoverageScoringPolicy ScoringPolicy;
    public ISet<Vector3Int> FocusCells;
    public Func<Vector3Int, bool> IsKnown;
    public Func<Vector3Int, bool> IsExplored;
    public int MovementBudget;
    public bool EnableLos = true;
    public bool IncludeAlliedCoverage;
    public bool ValidateFinalOccupancy = true;
}

public sealed class MelhorVisaoCellScore
{
    public Vector3Int Cell;
    public int MovementCost;
    public float Score;
    public float DeltaFromOrigin;
    public int DisplayScore;
    public VisionCoverageResult Coverage;
    public VisionCoverageEvaluation Evaluation;
    public string Reason;
}

public sealed class MelhorVisaoResult
{
    public VisionCoverageLayer Layer;
    public MelhorVisaoCellScore Origin;
    public readonly HashSet<Vector3Int> AlliedCoverageWithoutObserver =
        new HashSet<Vector3Int>();
    public readonly List<MelhorVisaoCellScore> Ranking =
        new List<MelhorVisaoCellScore>();
    public string Diagnostic;

    public MelhorVisaoCellScore Best =>
        Ranking.Count > 0 ? Ranking[0] : null;
}

/// <summary>
/// Ranqueia as celulas da hotzone tatica Mobility pelo valor da visao que a
/// unidade teria nelas. Consulta hipotetica e transacional: nao move unidade,
/// nao publica FOW e nao altera memoria, recursos, ocupacao ou revisoes.
/// </summary>
public static class MelhorVisaoService
{
    public static MelhorVisaoResult Evaluate(MelhorVisaoRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.Unit,
            "melhorVisao");
        AIDecisionPerf.AddCount("MelhorVisaoCalls");
        var result = new MelhorVisaoResult
        {
            Layer = request != null
                ? request.Layer
                : VisionCoverageLayer.All
        };
        if (request == null
            || request.Unit == null
            || request.Map == null
            || request.TerrainDatabase == null
            || request.ScoringPolicy == null)
        {
            result.Diagnostic =
                "Consulta incompleta: unidade, mapa, TerrainDatabase e politica sao obrigatorios.";
            return result;
        }

        Vector3Int origin = request.Unit.CurrentCellPosition;
        origin.z = 0;
        VisionCoverageResult originCoverage = BuildCoverage(
            request, request.Unit, origin);

        if (request.IncludeAlliedCoverage)
            CollectAlliedCoverage(request, result.AlliedCoverageWithoutObserver);

        Dictionary<Vector3Int, int> candidates =
            CollectCandidates(request, origin);
        foreach (KeyValuePair<Vector3Int, int> pair in candidates)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;
            if (request.ValidateFinalOccupancy
                && cell != origin
                && !CanEndAt(request.Unit, request.Map, cell))
            {
                continue;
            }

            VisionCoverageResult coverage = cell == origin
                ? originCoverage
                : BuildCoverage(request, request.Unit, cell);
            VisionCoverageEvaluation evaluation =
                VisionCoverageEvaluator.Evaluate(
                    new VisionCoverageEvaluationRequest
                    {
                        CandidateCoverage = coverage.VisibleCells,
                        OriginCoverage = originCoverage.VisibleCells,
                        AlliedCoverageWithoutObserver =
                            result.AlliedCoverageWithoutObserver,
                        FocusCells = request.FocusCells,
                        IsKnown = request.IsKnown,
                        IsExplored = request.IncludeAlliedCoverage
                            ? request.IsExplored
                            : null,
                        ScoringPolicy = request.ScoringPolicy,
                        HasTeamContext = request.IncludeAlliedCoverage,
                        MovementCost = pair.Value
                    });
            var score = new MelhorVisaoCellScore
            {
                Cell = cell,
                MovementCost = pair.Value,
                Score = evaluation.Score,
                DisplayScore = evaluation.DisplayScore,
                Coverage = coverage,
                Evaluation = evaluation,
                Reason = evaluation.Reason
            };
            result.Ranking.Add(score);
            if (cell == origin)
                result.Origin = score;
        }

        if (result.Origin == null)
        {
            result.Origin = BuildScoreForMissingOrigin(
                request,
                result,
                origin,
                originCoverage);
            result.Ranking.Add(result.Origin);
        }

        float originScore = result.Origin.Score;
        for (int i = 0; i < result.Ranking.Count; i++)
            result.Ranking[i].DeltaFromOrigin =
                result.Ranking[i].Score - originScore;
        result.Ranking.Sort(CompareScores);
        result.Diagnostic =
            $"camada={request.Layer.Label} candidatos={result.Ranking.Count} "
            + $"aliados={result.AlliedCoverageWithoutObserver.Count} "
            + $"melhor={(result.Best != null ? result.Best.Cell.ToString() : "-")}";
        AIDecisionPerf.AddCount(
            "MelhorVisaoCandidatesEvaluated",
            result.Ranking.Count);
        AIDecisionPerf.AddCount("CellsVisited", result.Ranking.Count);
        return result;
    }

    private static VisionCoverageResult BuildCoverage(
        MelhorVisaoRequest request,
        UnitManager observer,
        Vector3Int cell) =>
        VisionCoverageService.Evaluate(new VisionCoverageRequest
        {
            Observer = observer,
            ObserverCell = cell,
            Map = request.Map,
            TerrainDatabase = request.TerrainDatabase,
            DpqAirHeightConfig = request.DpqAirHeightConfig,
            Layer = request.Layer,
            EnableLos = request.EnableLos
        });

    private static void CollectAlliedCoverage(
        MelhorVisaoRequest request,
        HashSet<Vector3Int> output)
    {
        UnitManager[] editUnits = null;
        IReadOnlyList<UnitManager> units;
        if (Application.isPlaying)
            units = UnitManager.AllActive;
        else
        {
            editUnits = UnityEngine.Object.FindObjectsByType<UnitManager>(
                FindObjectsInactive.Exclude);
            units = editUnits;
        }

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager ally = units[i];
            if (ally == null
                || ally == request.Unit
                || ally.IsDead
                || ally.IsEmbarked
                || !ally.gameObject.activeInHierarchy
                || !PlayerSlotRelations.AreAllies(ally, request.Unit)
                || ally.BoardTilemap != null
                    && ally.BoardTilemap != request.Map)
            {
                continue;
            }

            Vector3Int cell = ally.CurrentCellPosition;
            cell.z = 0;
            VisionCoverageResult coverage = BuildCoverage(
                request, ally, cell);
            output.UnionWith(coverage.VisibleCells);
        }
    }

    private static Dictionary<Vector3Int, int> CollectCandidates(
        MelhorVisaoRequest request,
        Vector3Int origin)
    {
        var result = new Dictionary<Vector3Int, int>
        {
            [origin] = 0
        };
        if (request.MovementBudget <= 0)
            return result;

        ReachSubStep subStep =
            AIActionReachCoordinator.UsesCubicSectorReach(request.Unit)
                ? ReachSubStep.Aereo
                : ReachSubStep.Terrestre;
        UnitReachEnvelope envelope = UnitReachEnvelopeService.Build(
            new UnitReachRequest
            {
                Unit = request.Unit,
                BoardMap = request.Map,
                TerrainDatabase = request.TerrainDatabase,
                Intent = ReachIntent.Mobility,
                Band = ReachBand.Tactical,
                MovementBudget = request.MovementBudget,
                IncludeMovementCosts = true,
                SubStep = subStep
            });
        if (envelope == null)
            return result;

        foreach (Vector3Int rawCell in envelope.MovementCells)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            int cost = envelope.CostByCell.TryGetValue(cell, out int knownCost)
                ? Mathf.Max(0, knownCost)
                : cell == origin ? 0 : request.MovementBudget;
            result[cell] = cost;
        }
        return result;
    }

    private static bool CanEndAt(
        UnitManager unit,
        Tilemap map,
        Vector3Int destination)
    {
        List<UnitManager> occupants = UnitOccupancyRules.GetUnitsAtCell(
            map, destination, unit);
        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        bool projectsToAir =
            unit.GetDomain() == Domain.Air
            && !unit.IsAircraftGrounded;
        if (!projectsToAir
            && unit.IsAircraftGrounded
            && destination != origin)
        {
            projectsToAir = true;
        }

        if (!projectsToAir)
            return OccupancyResolver.CanEndMove(unit, destination, occupants);

        HeightLevel finalHeight = unit.GetDomain() == Domain.Air
            ? unit.GetHeightLevel()
            : HeightLevel.AirLow;
        return OccupancyResolver.CanEndMoveAsLayer(
            unit,
            Domain.Air,
            finalHeight,
            occupants);
    }

    private static MelhorVisaoCellScore BuildScoreForMissingOrigin(
        MelhorVisaoRequest request,
        MelhorVisaoResult result,
        Vector3Int origin,
        VisionCoverageResult originCoverage)
    {
        VisionCoverageEvaluation evaluation =
            VisionCoverageEvaluator.Evaluate(
                new VisionCoverageEvaluationRequest
                {
                    CandidateCoverage = originCoverage.VisibleCells,
                    OriginCoverage = originCoverage.VisibleCells,
                    AlliedCoverageWithoutObserver =
                        result.AlliedCoverageWithoutObserver,
                    FocusCells = request.FocusCells,
                    IsKnown = request.IsKnown,
                    IsExplored = request.IncludeAlliedCoverage
                        ? request.IsExplored
                        : null,
                    ScoringPolicy = request.ScoringPolicy,
                    HasTeamContext = request.IncludeAlliedCoverage,
                    MovementCost = 0
                });
        return new MelhorVisaoCellScore
        {
            Cell = origin,
            MovementCost = 0,
            Score = evaluation.Score,
            DisplayScore = evaluation.DisplayScore,
            Coverage = originCoverage,
            Evaluation = evaluation,
            Reason = evaluation.Reason
        };
    }

    private static int CompareScores(
        MelhorVisaoCellScore a,
        MelhorVisaoCellScore b)
    {
        int byScore = b.Score.CompareTo(a.Score);
        if (byScore != 0) return byScore;
        int byFocus = b.Evaluation.FocusRevealedCells.Count.CompareTo(
            a.Evaluation.FocusRevealedCells.Count);
        if (byFocus != 0) return byFocus;
        int byLoss = a.Evaluation.LostUniqueCells.Count.CompareTo(
            b.Evaluation.LostUniqueCells.Count);
        if (byLoss != 0) return byLoss;
        int byMarginal = b.Evaluation.MarginalCells.Count.CompareTo(
            a.Evaluation.MarginalCells.Count);
        if (byMarginal != 0) return byMarginal;
        int byCost = a.MovementCost.CompareTo(b.MovementCost);
        if (byCost != 0) return byCost;
        int byX = a.Cell.x.CompareTo(b.Cell.x);
        return byX != 0 ? byX : a.Cell.y.CompareTo(b.Cell.y);
    }
}
