using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Reivindicacao provisoria produzida pelo planejamento coletivo de captura.
/// Nao ocupa construcao, nao altera objetivo e nao sobrevive a mudanca do
/// snapshot confirmado.
/// </summary>
public readonly struct CaptureOpportunityClaim
{
    public readonly ConstructionManager Construction;
    public readonly UnitManager Capturer;
    public readonly int RouteCost;
    public readonly bool FormalPlan;

    public CaptureOpportunityClaim(
        ConstructionManager construction,
        UnitManager capturer,
        int routeCost,
        bool formalPlan)
    {
        Construction = construction;
        Capturer = capturer;
        RouteCost = routeCost;
        FormalPlan = formalPlan;
    }
}

public sealed class CaptureOpportunityClaimSnapshot
{
    public readonly int StateHash;
    public readonly int ConfirmedRevision;

    private readonly Dictionary<int, CaptureOpportunityClaim>
        claimsByConstructionId;

    internal CaptureOpportunityClaimSnapshot(
        int stateHash,
        int confirmedRevision,
        Dictionary<int, CaptureOpportunityClaim>
            claimsByConstructionId)
    {
        StateHash = stateHash;
        ConfirmedRevision = confirmedRevision;
        this.claimsByConstructionId =
            claimsByConstructionId
            ?? new Dictionary<int, CaptureOpportunityClaim>();
    }

    public bool TryGetClaim(
        ConstructionManager construction,
        out CaptureOpportunityClaim claim)
    {
        claim = default;
        return construction != null
            && claimsByConstructionId.TryGetValue(
                construction.InstanceId,
                out claim);
    }
}

/// <summary>
/// Distribui construcoes alcancaveis entre capturadores do mesmo slot.
/// Plano formal vence oportunidade; dentro de cada grupo, um matching
/// deterministico maximiza o numero de capturadores com alvo. A prioridade
/// estavel do capturador vence o desempate antes do custo: o claim representa
/// a intencao do passageiro e o transporte apenas a consulta.
///
/// POLITICA, NAO ALCANCE. Elegibilidade, precedencia plano-formal-vence-
/// oportunidade, matching 1:1 e desempate sao deste servico. Quao longe cada
/// capturador chega e de quanto custa a rota vem do UnitReachEnvelopeService,
/// na intencao Capture: o envelope devolve so alcance, sem consultar
/// PodeCapturar. Ver docs/contrato_envelope_alcance.md.
/// </summary>
public static class CaptureOpportunityClaimService
{
    private const int MaxCacheEntries = 8;

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly int MapObjectId;
        public readonly int TerrainDatabaseObjectId;
        public readonly int SlotIndex;
        public readonly int OperationalTurns;
        public readonly int ConfirmedRevision;
        public readonly int StateHash;

        public CacheKey(
            QueroCaronaRequest request,
            int confirmedRevision,
            int stateHash)
        {
            MapObjectId =
                request.map.GetEntityId().GetHashCode();
            TerrainDatabaseObjectId =
                request.terrainDatabase.GetEntityId().GetHashCode();
            SlotIndex = request.unit.SlotIndex;
            OperationalTurns = Mathf.Max(
                1, request.operationalTurns);
            ConfirmedRevision = confirmedRevision;
            StateHash = stateHash;
        }

        public bool Equals(CacheKey other)
        {
            return MapObjectId == other.MapObjectId
                && TerrainDatabaseObjectId
                    == other.TerrainDatabaseObjectId
                && SlotIndex == other.SlotIndex
                && OperationalTurns == other.OperationalTurns
                && ConfirmedRevision == other.ConfirmedRevision
                && StateHash == other.StateHash;
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MapObjectId;
                hash = (hash * 397) ^ TerrainDatabaseObjectId;
                hash = (hash * 397) ^ SlotIndex;
                hash = (hash * 397) ^ OperationalTurns;
                hash = (hash * 397) ^ ConfirmedRevision;
                hash = (hash * 397) ^ StateHash;
                return hash;
            }
        }
    }

    private sealed class Edge
    {
        public ConstructionManager Construction;
        public int RouteCost;
    }

    private sealed class Candidate
    {
        public UnitManager Unit;
        public bool FormalPlan;
        public ConstructionSector FormalSector;
        public readonly List<Edge> Edges = new List<Edge>();
    }

    private static readonly Dictionary<
        CacheKey,
        CaptureOpportunityClaimSnapshot> Cache =
            new Dictionary<
                CacheKey,
                CaptureOpportunityClaimSnapshot>();

    /// <summary>
    /// <paramref name="requestReach"/> e o envelope de Captura que o chamador
    /// ja construiu para <c>request.unit</c>. O reuso e por IDENTIDADE de
    /// envelope — mesma unidade, mesma intencao, mesma banda, mesmos turnos —
    /// e nao mais por igualdade de orcamento inteiro, que deixou de existir.
    /// </summary>
    public static CaptureOpportunityClaimSnapshot GetOrBuild(
        QueroCaronaRequest request,
        UnitReachProfile requestReach = null)
    {
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null)
        {
            return new CaptureOpportunityClaimSnapshot(
                0,
                -1,
                null);
        }

        int confirmedRevision =
            ResolveConfirmedRevision(request.map);
        TeamObjectivePlan plan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(request.unit.SlotIndex));
        int stateHash = BuildStateHash(
            request.unit.SlotIndex,
            plan);
        var key = new CacheKey(
            request,
            confirmedRevision,
            stateHash);
        if (Cache.TryGetValue(
                key,
                out CaptureOpportunityClaimSnapshot cached))
        {
            AIDecisionPerf.AddCount(
                "CaptureClaimSnapshotHits");
            return cached;
        }

        AIDecisionPerf.AddCount(
            "CaptureClaimSnapshotBuilds");
        CaptureOpportunityClaimSnapshot built = Build(
            request,
            plan,
            confirmedRevision,
            stateHash,
            requestReach);
        if (Cache.Count >= MaxCacheEntries)
            Cache.Clear();
        Cache[key] = built;
        return built;
    }

    public static int ResolveStateHash(
        QueroCaronaRequest request)
    {
        if (request?.unit == null)
            return 0;
        TeamObjectivePlan plan =
            ObjectiveManager.GetPlanForSlot(
                PlayerSlotId.FromIndex(request.unit.SlotIndex));
        return BuildStateHash(
            request.unit.SlotIndex,
            plan);
    }

    private static CaptureOpportunityClaimSnapshot Build(
        QueroCaronaRequest request,
        TeamObjectivePlan plan,
        int confirmedRevision,
        int stateHash,
        UnitReachProfile requestReach)
    {
        var formalCandidates = new List<Candidate>();
        var rogueCandidates = new List<Candidate>();
        IReadOnlyList<ConstructionManager> constructions =
            ConstructionManager.AllActive;

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (!IsEligibleCapturer(
                    unit,
                    request.unit.SlotIndex))
            {
                continue;
            }

            // Alcance vem do envelope, nunca daqui. A intencao Capture devolve
            // SO alcance — a construcao ainda passa pela elegibilidade abaixo e
            // pelo PodeCapturarSensor na hora de agir.
            //
            // O orcamento deixou de ser MP x N num bolso so: o turno atual vale
            // o que ainda sobrou e os seguintes valem MP cheio (banda
            // Operational). O alcance encolhe em terreno caro e conforme a
            // unidade age na rodada, que e o comportamento real do jogo.
            //
            // Aeronave: a arvore da Captura nao tem ramo aereo, mas o servico
            // resolve a geometria cubica internamente para quem e isAircraft.
            UnitReachEnvelope reach;
            if (unit == request.unit
                && requestReach != null
                && requestReach.Operational != null)
            {
                // Mesma unidade, mesma intencao, mesma banda: e o mesmo
                // envelope. Reconstruir seria repetir a travessia encadeada.
                reach = requestReach.Operational;
                AIDecisionPerf.AddCount(
                    "CaptureClaimReachReuses");
            }
            else
            {
                reach = UnitReachEnvelopeService.Build(
                    new UnitReachRequest
                    {
                        Unit = unit,
                        BoardMap = request.map,
                        TerrainDatabase = request.terrainDatabase,
                        Intent = ReachIntent.Capture,
                        SubStep = ReachSubStep.Terrestre,
                        Band = ReachBand.Operational,
                        OperationalTurns = Mathf.Max(
                            1, request.operationalTurns)
                    });
                AIDecisionPerf.AddCount(
                    "CaptureClaimReachBuilds");
            }

            if (reach == null)
                continue;

            bool formal = TryResolveFormalCaptureSector(
                unit,
                plan,
                out ConstructionSector formalSector);
            var candidate = new Candidate
            {
                Unit = unit,
                FormalPlan = formal,
                FormalSector = formalSector
            };

            for (int i = 0;
                 i < constructions.Count;
                 i++)
            {
                ConstructionManager construction =
                    constructions[i];
                if (!IsEligibleConstruction(
                        request.map,
                        unit,
                        construction,
                        formal,
                        formalSector,
                        reach,
                        out int routeCost))
                {
                    continue;
                }

                candidate.Edges.Add(
                    new Edge
                    {
                        Construction = construction,
                        RouteCost = routeCost
                    });
            }

            candidate.Edges.Sort(CompareEdges);
            if (candidate.Edges.Count == 0)
                continue;
            if (formal)
                formalCandidates.Add(candidate);
            else
                rogueCandidates.Add(candidate);
        }

        SortCandidates(formalCandidates);
        SortCandidates(rogueCandidates);

        var claims =
            new Dictionary<int, CaptureOpportunityClaim>();
        var formallyClaimedConstructionIds =
            new HashSet<int>();
        MatchCandidates(
            formalCandidates,
            claims,
            blockedConstructionIds: null);
        foreach (int constructionId in claims.Keys)
            formallyClaimedConstructionIds.Add(constructionId);
        MatchCandidates(
            rogueCandidates,
            claims,
            formallyClaimedConstructionIds);

        AIDecisionPerf.AddCount(
            "CaptureClaimAssignments",
            claims.Count);
        return new CaptureOpportunityClaimSnapshot(
            stateHash,
            confirmedRevision,
            claims);
    }

    private static void MatchCandidates(
        List<Candidate> candidates,
        Dictionary<int, CaptureOpportunityClaim> claims,
        HashSet<int> blockedConstructionIds)
    {
        if (candidates == null || candidates.Count == 0)
            return;

        var candidatesByUnitId =
            new Dictionary<int, Candidate>();
        var ownerByConstructionId =
            new Dictionary<int, int>();
        for (int i = 0; i < candidates.Count; i++)
        {
            Candidate candidate = candidates[i];
            candidatesByUnitId[candidate.Unit.InstanceId] =
                candidate;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            TryAssign(
                candidates[i],
                candidatesByUnitId,
                ownerByConstructionId,
                blockedConstructionIds,
                new HashSet<int>());
        }

        foreach (KeyValuePair<int, int> pair
                 in ownerByConstructionId)
        {
            if (!candidatesByUnitId.TryGetValue(
                    pair.Value,
                    out Candidate owner))
            {
                continue;
            }
            Edge edge = owner.Edges.Find(candidateEdge =>
                candidateEdge.Construction != null
                && candidateEdge.Construction.InstanceId
                    == pair.Key);
            if (edge == null)
                continue;
            claims[pair.Key] =
                new CaptureOpportunityClaim(
                    edge.Construction,
                    owner.Unit,
                    edge.RouteCost,
                    owner.FormalPlan);
        }
    }

    private static bool TryAssign(
        Candidate candidate,
        Dictionary<int, Candidate> candidatesByUnitId,
        Dictionary<int, int> ownerByConstructionId,
        HashSet<int> blockedConstructionIds,
        HashSet<int> visitedConstructionIds)
    {
        for (int i = 0; i < candidate.Edges.Count; i++)
        {
            Edge edge = candidate.Edges[i];
            if (edge?.Construction == null)
                continue;
            int constructionId =
                edge.Construction.InstanceId;
            if (blockedConstructionIds != null
                && blockedConstructionIds.Contains(
                    constructionId))
            {
                continue;
            }
            if (!visitedConstructionIds.Add(
                    constructionId))
            {
                continue;
            }

            if (!ownerByConstructionId.TryGetValue(
                    constructionId,
                    out int previousOwnerId))
            {
                ownerByConstructionId[constructionId] =
                    candidate.Unit.InstanceId;
                return true;
            }

            if (!candidatesByUnitId.TryGetValue(
                    previousOwnerId,
                    out Candidate previousOwner)
                || !TryAssign(
                    previousOwner,
                    candidatesByUnitId,
                    ownerByConstructionId,
                    blockedConstructionIds,
                    visitedConstructionIds))
            {
                continue;
            }

            ownerByConstructionId[constructionId] =
                candidate.Unit.InstanceId;
            return true;
        }

        return false;
    }

    private static bool IsEligibleCapturer(
        UnitManager unit,
        int slotIndex)
    {
        return unit != null
            && unit.SlotIndex == slotIndex
            && !unit.IsDead
            && !unit.IsEmbarked
            && !unit.IsUnderRepair
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador);
    }

    private static bool IsEligibleConstruction(
        Tilemap map,
        UnitManager unit,
        ConstructionManager construction,
        bool formal,
        ConstructionSector formalSector,
        UnitReachEnvelope reach,
        out int routeCost)
    {
        routeCost = int.MaxValue;
        if (construction == null
            || !construction.IsCapturable
            || construction.TeamId == unit.TeamId
            || (formal
                && construction.Sector != formalSector))
        {
            return false;
        }

        Vector3Int cell =
            construction.CurrentCellPosition;
        cell.z = 0;
        // Pertencer a banda e uma pergunta ao envelope; o custo da rota e o que
        // ele ja calculou para responde-la. Nao ha segunda varredura aqui.
        if (reach == null
            || !reach.CanAct(cell)
            || !reach.TryGetCost(cell, out routeCost))
        {
            routeCost = int.MaxValue;
            return false;
        }

        return !UnitOccupancyRules
            .HasBlockingOccupantForUnitAtCell(
                map,
                cell,
                unit,
                alliedOnly: true);
    }

    private static bool TryResolveFormalCaptureSector(
        UnitManager unit,
        TeamObjectivePlan plan,
        out ConstructionSector sector)
    {
        sector = ConstructionSector.None;
        if (unit == null
            || plan?.Objectives == null)
        {
            return false;
        }

        foreach (SectorObjective objective in plan.Objectives)
        {
            if (objective == null
                || objective.Status == ObjectiveStatus.Defending
                || objective.Status == ObjectiveStatus.Complete
                || objective.Status == ObjectiveStatus.Abandoned
                || objective.Slots == null)
            {
                continue;
            }
            foreach (SlotNeed slot in objective.Slots)
            {
                if (slot != null
                    && slot.Filled
                    && slot.Role == UnitRole.Capturador
                    && slot.AssignedUnitId == unit.InstanceId)
                {
                    sector = objective.Sector;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Capturador puro escolhe antes do Capturador Agressivo.
    ///
    /// O agressivo e BACKUP de captura: ele satisfaz o papel (CanSatisfy) e por
    /// isso disputa, mas a vocacao dele e abrir caminho. Quando os dois querem o
    /// MESMO predio, o puro leva e o agressivo cai para o proximo — que e a
    /// regra "no empate, o agressivo cede a vez".
    ///
    /// Isso e ordem de ESCOLHA, nao de distancia: cada candidato ja tem as
    /// arestas ordenadas por custo de rota (CompareEdges), entao um capturador
    /// puro distante nao rouba predio que ele nao quer — ele pega o dele. A
    /// precedencia so decide o desempate quando ha disputa pelo mesmo alvo.
    ///
    /// Vale igual com e sem plano: as duas listas (formal e rogue) passam por
    /// esta mesma ordenacao.
    /// </summary>
    private static int ResolveCapturerRolePrecedence(UnitManager unit)
    {
        if (unit == null
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || data.roles == null
            || data.roles.Count == 0)
        {
            return 0;
        }

        return data.roles[0] == UnitRole.CapturadorAgressivo ? 1 : 0;
    }

    private static void SortCandidates(
        List<Candidate> candidates)
    {
        candidates.Sort((left, right) =>
        {
            int compare =
                ResolveCapturerRolePrecedence(left.Unit)
                    .CompareTo(
                        ResolveCapturerRolePrecedence(right.Unit));
            if (compare != 0)
                return compare;
            compare =
                left.Unit.InstanceId.CompareTo(
                    right.Unit.InstanceId);
            if (compare != 0)
                return compare;
            compare =
                left.Edges.Count.CompareTo(
                    right.Edges.Count);
            if (compare != 0)
                return compare;
            compare =
                left.Edges[0].RouteCost.CompareTo(
                    right.Edges[0].RouteCost);
            return compare;
        });
    }

    private static int CompareEdges(
        Edge left,
        Edge right)
    {
        int compare =
            left.RouteCost.CompareTo(right.RouteCost);
        return compare != 0
            ? compare
            : left.Construction.InstanceId.CompareTo(
                right.Construction.InstanceId);
    }

    private static int ResolveConfirmedRevision(
        Tilemap map)
    {
        if (map != null
            && ConfirmedOccupancyIndex.TryGetFor(
                map,
                out ConfirmedOccupancyIndex occupancy)
            && occupancy != null
            && occupancy.CanServeLiveQueries)
        {
            return occupancy.ConfirmedRevision;
        }

        return ThreatRevisionTracker.GlobalBoardRevision;
    }

    private static int BuildStateHash(
        int slotIndex,
        TeamObjectivePlan plan)
    {
        unchecked
        {
            int hash = 17;
            foreach (UnitManager unit
                     in UnitManager.AllActive)
            {
                if (unit == null
                    || unit.SlotIndex != slotIndex)
                {
                    continue;
                }
                Vector3Int cell =
                    unit.CurrentCellPosition;
                cell.z = 0;
                hash = (hash * 31) + unit.InstanceId;
                hash = (hash * 31) + cell.GetHashCode();
                hash = (hash * 31)
                    + unit.RemainingMovementPoints;
                hash = (hash * 31)
                    + unit.MaxMovementPoints;
                hash = (hash * 31)
                    + (unit.HasActed ? 1 : 0);
                hash = (hash * 31)
                    + (unit.IsDead ? 1 : 0);
                hash = (hash * 31)
                    + (unit.IsEmbarked ? 1 : 0);
                hash = (hash * 31)
                    + (unit.IsUnderRepair ? 1 : 0);
                hash = (hash * 31)
                    + (unit.AIHasDesignatedCaptureTarget
                        ? 1
                        : 0);
                hash = (hash * 31)
                    + unit
                        .AIDesignatedCaptureTargetInstanceId;
                hash = (hash * 31)
                    + unit
                        .AIDesignatedCaptureTargetCell
                        .GetHashCode();
            }

            foreach (ConstructionManager construction
                     in ConstructionManager.AllActive)
            {
                if (construction == null)
                    continue;
                Vector3Int cell =
                    construction.CurrentCellPosition;
                cell.z = 0;
                hash = (hash * 31)
                    + construction.InstanceId;
                hash = (hash * 31)
                    + cell.GetHashCode();
                hash = (hash * 31)
                    + (int)construction.TeamId;
                hash = (hash * 31)
                    + (int)construction.Sector;
                hash = (hash * 31)
                    + (construction.IsCapturable ? 1 : 0);
            }

            if (plan?.Objectives != null)
            {
                foreach (SectorObjective objective
                         in plan.Objectives)
                {
                    if (objective == null)
                        continue;
                    hash = (hash * 31)
                        + (int)objective.Sector;
                    hash = (hash * 31)
                        + (int)objective.Status;
                    foreach (SlotNeed slot
                             in objective.Slots)
                    {
                        if (slot == null)
                            continue;
                        hash = (hash * 31)
                            + (int)slot.Role;
                        hash = (hash * 31)
                            + (slot.Filled ? 1 : 0);
                        hash = (hash * 31)
                            + slot.AssignedUnitId;
                    }
                }
            }

            return hash;
        }
    }
}
