using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum QueroCaronaContext
{
    ComPlano,
    RogueOuRebelde
}

public enum QueroCaronaReach
{
    None,
    Tactical,
    Operational,
    BeyondOperational
}

public sealed class QueroCaronaRequest
{
    public UnitManager unit;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public QueroCaronaContext context;
    public ConstructionSector plannedSector;
    public bool useExplicitTarget;
    public Vector3Int explicitTarget;
    public string explicitTargetLabel;
    public int operationalTurns = 2;
    public bool emulateUnderRepairFromUnitData;
    public IReadOnlyDictionary<Vector3Int, int> operationalReach;
    public int operationalReachBudget = -1;
    public Action<string> diagnosticLog;
}

public sealed class QueroCaronaResult
{
    public bool wantsRide;
    public bool isEstimate = true;
    public bool isEmergency;
    public bool isUnderRepairRuntime;
    public bool isUnderRepairEmulated;
    public string repairEvaluation;
    public bool isInfantry;
    public QueroCaronaReach reach;
    public Vector3Int evaluatedTarget;
    public ConstructionManager evaluatedConstruction;
    public int tacticalBudget;
    public int operationalBudget;
    public int routeCost = int.MaxValue;
    public int rideNeedScore;
    public int captureClaimsBlocked;
    public int captureClaimOwnerUnitId = -1;
    public string reason;
}

/// <summary>
/// Contrapeso puro do Melhor Embarque. Estima se a unidade ainda precisa de
/// transporte depois de verificar se consegue cumprir seu objetivo sozinha
/// dentro dos envelopes Tactical e Operational. Nao reserva transporte, nao
/// move unidades e nao substitui prioridades operacionais do papel da unidade.
/// </summary>
public static class QueroCaronaService
{
    private const int MaxCacheEntries = 256;

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        public readonly int mapObjectId;
        public readonly int terrainDatabaseObjectId;
        public readonly int unitObjectId;
        public readonly int unitInstanceId;
        public readonly int unitDataObjectId;
        public readonly Vector3Int origin;
        public readonly int remainingMovement;
        public readonly int maxMovement;
        public readonly int currentHp;
        public readonly int currentFuel;
        public readonly int maxFuel;
        public readonly int repairStateHash;
        public readonly bool isUnderRepair;
        public readonly bool isEmbarked;
        public readonly Domain domain;
        public readonly HeightLevel height;
        public readonly TeamId team;
        public readonly int slotIndex;
        public readonly QueroCaronaContext context;
        public readonly ConstructionSector plannedSector;
        public readonly bool useExplicitTarget;
        public readonly Vector3Int explicitTarget;
        public readonly int operationalTurns;
        public readonly bool emulateRepair;
        public readonly int occupancyRevision;
        public readonly int constructionStateHash;
        public readonly int captureClaimStateHash;
        public readonly int topologyVersion;
        public readonly string topologyFingerprint;

        public CacheKey(
            QueroCaronaRequest request,
            UnitData data,
            ConfirmedOccupancyIndex occupancy,
            BoardTopologyIndex topology)
        {
            UnitManager unit = request.unit;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            mapObjectId = request.map.GetEntityId().GetHashCode();
            terrainDatabaseObjectId =
                request.terrainDatabase.GetEntityId().GetHashCode();
            unitObjectId = unit.GetEntityId().GetHashCode();
            unitInstanceId = unit.InstanceId;
            unitDataObjectId = data.GetEntityId().GetHashCode();
            origin = cell;
            remainingMovement =
                Mathf.Max(0, unit.RemainingMovementPoints);
            maxMovement = Mathf.Max(0, unit.MaxMovementPoints);
            currentHp = unit.CurrentHP;
            currentFuel = Mathf.Max(0, unit.CurrentFuel);
            maxFuel = Mathf.Max(0, unit.GetMaxFuel());
            repairStateHash = BuildRepairStateHash(unit);
            isUnderRepair = unit.IsUnderRepair;
            isEmbarked = unit.IsEmbarked;
            domain = unit.GetDomain();
            height = unit.GetHeightLevel();
            team = unit.TeamId;
            slotIndex = unit.SlotIndex;
            context = request.context;
            plannedSector = request.plannedSector;
            useExplicitTarget = request.useExplicitTarget;
            Vector3Int normalizedExplicitTarget =
                request.explicitTarget;
            normalizedExplicitTarget.z = 0;
            explicitTarget = normalizedExplicitTarget;
            operationalTurns = Mathf.Max(
                1, request.operationalTurns);
            emulateRepair =
                request.emulateUnderRepairFromUnitData;
            occupancyRevision = occupancy.ConfirmedRevision;
            constructionStateHash =
                BuildConstructionStateHash(request);
            captureClaimStateHash =
                UnitRoleCompatibility.CanSatisfy(
                    data,
                    UnitRole.Capturador)
                    ? CaptureOpportunityClaimService.ResolveStateHash(
                        request)
                    : 0;
            topologyVersion = topology.TopologyVersion;
            topologyFingerprint =
                topology.TopologyFingerprint ?? string.Empty;
        }

        public bool Equals(CacheKey other)
        {
            return mapObjectId == other.mapObjectId
                && terrainDatabaseObjectId
                    == other.terrainDatabaseObjectId
                && unitObjectId == other.unitObjectId
                && unitInstanceId == other.unitInstanceId
                && unitDataObjectId == other.unitDataObjectId
                && origin == other.origin
                && remainingMovement == other.remainingMovement
                && maxMovement == other.maxMovement
                && currentHp == other.currentHp
                && currentFuel == other.currentFuel
                && maxFuel == other.maxFuel
                && repairStateHash == other.repairStateHash
                && isUnderRepair == other.isUnderRepair
                && isEmbarked == other.isEmbarked
                && domain == other.domain
                && height == other.height
                && team == other.team
                && slotIndex == other.slotIndex
                && context == other.context
                && plannedSector == other.plannedSector
                && useExplicitTarget == other.useExplicitTarget
                && explicitTarget == other.explicitTarget
                && operationalTurns == other.operationalTurns
                && emulateRepair == other.emulateRepair
                && occupancyRevision == other.occupancyRevision
                && constructionStateHash
                    == other.constructionStateHash
                && captureClaimStateHash
                    == other.captureClaimStateHash
                && topologyVersion == other.topologyVersion
                && string.Equals(
                    topologyFingerprint,
                    other.topologyFingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is CacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = mapObjectId;
                hash = (hash * 397) ^ terrainDatabaseObjectId;
                hash = (hash * 397) ^ unitObjectId;
                hash = (hash * 397) ^ unitInstanceId;
                hash = (hash * 397) ^ unitDataObjectId;
                hash = (hash * 397) ^ origin.GetHashCode();
                hash = (hash * 397) ^ remainingMovement;
                hash = (hash * 397) ^ maxMovement;
                hash = (hash * 397) ^ currentHp;
                hash = (hash * 397) ^ currentFuel;
                hash = (hash * 397) ^ maxFuel;
                hash = (hash * 397) ^ repairStateHash;
                hash = (hash * 397) ^ (isUnderRepair ? 1 : 0);
                hash = (hash * 397) ^ (isEmbarked ? 1 : 0);
                hash = (hash * 397) ^ (int)domain;
                hash = (hash * 397) ^ (int)height;
                hash = (hash * 397) ^ (int)team;
                hash = (hash * 397) ^ slotIndex;
                hash = (hash * 397) ^ (int)context;
                hash = (hash * 397) ^ (int)plannedSector;
                hash = (hash * 397)
                    ^ (useExplicitTarget ? 1 : 0);
                hash = (hash * 397)
                    ^ explicitTarget.GetHashCode();
                hash = (hash * 397) ^ operationalTurns;
                hash = (hash * 397) ^ (emulateRepair ? 1 : 0);
                hash = (hash * 397) ^ occupancyRevision;
                hash = (hash * 397) ^ constructionStateHash;
                hash = (hash * 397) ^ captureClaimStateHash;
                hash = (hash * 397) ^ topologyVersion;
                hash = (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(
                        topologyFingerprint ?? string.Empty);
                return hash;
            }
        }
    }

    private static readonly Dictionary<CacheKey, QueroCaronaResult>
        Cache = new Dictionary<CacheKey, QueroCaronaResult>();
    private static readonly Dictionary<int, int>
        ConstructionHashByScope =
            new Dictionary<int, int>();
    private static int constructionHashFrame = -1;
    private static int constructionHashBoardRevision = -1;

    public static QueroCaronaResult Evaluate(
        QueroCaronaRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.unit,
            "queroCarona");
        AIDecisionPerf.AddCount("QueroCaronaCalls");
        var result = new QueroCaronaResult
        {
            wantsRide = false,
            reason = "Contexto incompleto."
        };
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null
            || !request.unit.TryGetUnitData(out UnitData data)
            || data == null)
            return result;

        bool cacheable = TryBuildCacheKey(
            request, data, out CacheKey cacheKey);
        if (cacheable
            && Cache.TryGetValue(
                cacheKey,
                out QueroCaronaResult cached))
        {
            AIDecisionPerf.AddCount("QueroCaronaCacheHits");
            QueroCaronaResult hit = CloneResult(cached);
            request.diagnosticLog?.Invoke(hit.reason);
            return hit;
        }
        AIDecisionPerf.AddCount(
            cacheable
                ? "QueroCaronaCacheMisses"
                : "QueroCaronaCacheBypasses");

        QueroCaronaResult Finish()
        {
            if (cacheable)
                Store(cacheKey, result);
            return result;
        }

        result.isInfantry =
            data.unitClass == GameUnitClass.Infantry;
        result.tacticalBudget = Mathf.Max(
            0, request.unit.RemainingMovementPoints);
        if (result.tacticalBudget <= 0)
            result.tacticalBudget =
                Mathf.Max(0, request.unit.MaxMovementPoints);
        result.operationalBudget =
            result.tacticalBudget
            * Mathf.Max(1, request.operationalTurns);

        Vector3Int origin = request.unit.CurrentCellPosition;
        origin.z = 0;

        string repairEvaluation = "Emulação desativada.";
        bool emulatedUnderRepair = request.emulateUnderRepairFromUnitData
            && EvaluateRepairTriggers(
                request.unit, data, out repairEvaluation);
        result.isUnderRepairRuntime = request.unit.IsUnderRepair;
        result.isUnderRepairEmulated =
            !result.isUnderRepairRuntime && emulatedUnderRepair;
        result.repairEvaluation = repairEvaluation;

        // UnderRepair representa uma necessidade operacional anterior ao
        // objetivo normal da unidade. O passageiro pede resgate mesmo que um
        // prédio ou representante esteja próximo; compatibilidade, fornecedor
        // e prioridade final continuam sendo decididos pelos outros serviços.
        if (result.isUnderRepairRuntime || result.isUnderRepairEmulated)
        {
            result.wantsRide = true;
            result.isEmergency = true;
            result.reach = QueroCaronaReach.None;
            result.evaluatedTarget = origin;
            result.routeCost = 0;
            result.rideNeedScore = 2000;
            result.reason =
                $"{ResolveUnitKind(result)} " +
                (result.isUnderRepairRuntime
                    ? "IsUnderRepair runtime"
                    : "IsUnderRepair simulado por AI Behavior") +
                ": necessidade " +
                "emergencial aceita carona antes da avaliação de objetivo. " +
                "O resultado é um pedido, não uma ordem de transporte.";
            request.diagnosticLog?.Invoke(result.reason);
            return Finish();
        }

        bool canReuseOperationalReach =
            request.operationalReach != null
            && request.operationalReachBudget
                == result.operationalBudget;
        IReadOnlyDictionary<Vector3Int, int> reach =
            canReuseOperationalReach
                ? request.operationalReach
                : AIActionReachCoordinator.BuildSectorReachMap(
                request.unit,
                request.map,
                request.terrainDatabase,
                origin,
                Mathf.Max(0, result.operationalBudget));
        if (canReuseOperationalReach)
            AIDecisionPerf.AddCount(
                "QueroCaronaOperationalReachReuses");
        CaptureOpportunityClaimSnapshot captureClaims =
            UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador)
                ? CaptureOpportunityClaimService.GetOrBuild(
                    request,
                    reach,
                    result.operationalBudget)
                : null;

        if (request.useExplicitTarget)
        {
            Vector3Int explicitTarget = request.explicitTarget;
            explicitTarget.z = 0;
            string targetLabel =
                string.IsNullOrWhiteSpace(
                    request.explicitTargetLabel)
                    ? $"alvo explicito {explicitTarget}"
                    : request.explicitTargetLabel;

            result.evaluatedTarget = explicitTarget;
            if (reach.TryGetValue(
                    explicitTarget,
                    out int explicitCost))
            {
                result.routeCost = explicitCost;
                SetReachAndDecision(
                    result,
                    explicitCost,
                    targetLabel);
            }
            else
            {
                result.wantsRide = true;
                result.reach =
                    QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = 1000;
                result.reason =
                    $"{ResolveUnitKind(result)} nao alcanca " +
                    $"{targetLabel} em Tactical ou Operational: " +
                    "aceita carona.";
            }

            request.diagnosticLog?.Invoke(result.reason);
            return Finish();
        }

        if (request.context == QueroCaronaContext.ComPlano)
        {
            if (request.plannedSector == ConstructionSector.None
                || !SectorManager.TryGetSectorInfo(
                    request.plannedSector,
                    out SectorManager.SectorInfo info)
                || info == null)
            {
                result.wantsRide = true;
                result.reach = QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = 1000;
                result.reason =
                    "Plano sem representante válido no SectorManager; " +
                    "estimativa aceita carona.";
                return Finish();
            }

            Vector3Int representative = info.RepresentativeCell;
            representative.z = 0;
            if (TryFindBestAvailablePlannedTarget(
                    request,
                    reach,
                    captureClaims,
                    info,
                    representative,
                    out Vector3Int plannedTarget,
                    out ConstructionManager plannedConstruction,
                    out int plannedCost,
                    out int claimBlocks,
                    out int claimOwnerUnitId))
            {
                result.evaluatedTarget = plannedTarget;
                result.evaluatedConstruction = plannedConstruction;
                result.routeCost = plannedCost;
                result.captureClaimsBlocked = claimBlocks;
                result.captureClaimOwnerUnitId =
                    claimOwnerUnitId;
                SetReachAndDecision(
                    result,
                    plannedCost,
                    FormatCaptureTargetLabel(
                        captureClaims,
                        plannedConstruction,
                        request.unit,
                        plannedTarget == representative
                        ? $"representante de {request.plannedSector}"
                        : $"alternativa livre no setor " +
                          $"{request.plannedSector} {plannedTarget}"));
            }
            else
            {
                result.wantsRide = true;
                result.reach =
                    QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = 1000;
                result.captureClaimsBlocked = claimBlocks;
                result.captureClaimOwnerUnitId =
                    claimOwnerUnitId;
                result.reason =
                    $"{ResolveUnitKind(result)} sem destino livre " +
                    $"alcançável no setor {request.plannedSector} " +
                    "em Tactical ou Operational" +
                    FormatCaptureClaimSuffix(
                        claimBlocks,
                        claimOwnerUnitId) +
                    ": aceita carona.";
            }
            request.diagnosticLog?.Invoke(result.reason);
            return Finish();
        }

        ConstructionManager nearest = null;
        int nearestCost = int.MaxValue;
        int captureClaimBlocks = 0;
        int lastCaptureClaimOwnerId = -1;
        foreach (ConstructionManager construction
                 in ConstructionManager.AllActive)
        {
            if (construction == null
                || !construction.IsCapturable
                || construction.TeamId == request.unit.TeamId
                || IsClaimedByAlliedUnit(
                    request, construction.CurrentCellPosition))
                continue;
            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (!reach.TryGetValue(cell, out int cost)
                || cost >= nearestCost)
                continue;
            if (IsClaimedByAnotherCapturer(
                    captureClaims,
                    construction,
                    request.unit,
                    out CaptureOpportunityClaim claim))
            {
                captureClaimBlocks++;
                lastCaptureClaimOwnerId =
                    claim.Capturer != null
                        ? claim.Capturer.InstanceId
                        : -1;
                continue;
            }
            nearest = construction;
            nearestCost = cost;
        }

        if (nearest != null)
        {
            Vector3Int target = nearest.CurrentCellPosition;
            target.z = 0;
            result.evaluatedTarget = target;
            result.evaluatedConstruction = nearest;
            result.routeCost = nearestCost;
            result.captureClaimsBlocked =
                captureClaimBlocks;
            result.captureClaimOwnerUnitId =
                lastCaptureClaimOwnerId;
            SetReachAndDecision(
                result, nearestCost,
                FormatCaptureTargetLabel(
                    captureClaims,
                    nearest,
                    request.unit,
                    $"prédio capturável próximo {target}"));
        }
        else
        {
            result.wantsRide = true;
            result.reach = QueroCaronaReach.BeyondOperational;
            result.rideNeedScore = 1000;
            result.captureClaimsBlocked =
                captureClaimBlocks;
            result.captureClaimOwnerUnitId =
                lastCaptureClaimOwnerId;
            result.reason =
                $"{ResolveUnitKind(result)} rogue/rebelde sem prédio " +
                "capturável livre alcançável em Tactical ou Operational" +
                FormatCaptureClaimSuffix(
                    captureClaimBlocks,
                    lastCaptureClaimOwnerId) +
                ": " +
                "aceita carona.";
        }

        request.diagnosticLog?.Invoke(result.reason);
        return Finish();
    }

    public static QueroCaronaResult EvaluateEmergencyOnly(
        QueroCaronaRequest request)
    {
        AIDecisionPerf.AddCount("QueroCaronaEmergencyProbes");
        var result = new QueroCaronaResult
        {
            wantsRide = false,
            reason = "Contexto de emergência incompleto."
        };
        if (request?.unit == null
            || !request.unit.TryGetUnitData(out UnitData data)
            || data == null)
            return result;

        UnitManager unit = request.unit;
        result.isInfantry =
            data.unitClass == GameUnitClass.Infantry;
        result.tacticalBudget = Mathf.Max(
            0, unit.RemainingMovementPoints);
        if (result.tacticalBudget <= 0)
            result.tacticalBudget =
                Mathf.Max(0, unit.MaxMovementPoints);
        result.operationalBudget =
            result.tacticalBudget
            * Mathf.Max(1, request.operationalTurns);
        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;

        string repairEvaluation = "Emulação desativada.";
        bool emulatedUnderRepair =
            request.emulateUnderRepairFromUnitData
            && EvaluateRepairTriggers(
                unit, data, out repairEvaluation);
        result.isUnderRepairRuntime = unit.IsUnderRepair;
        result.isUnderRepairEmulated =
            !result.isUnderRepairRuntime && emulatedUnderRepair;
        result.repairEvaluation = repairEvaluation;
        result.isEmergency =
            result.isUnderRepairRuntime
            || result.isUnderRepairEmulated;
        result.wantsRide = result.isEmergency;
        result.reach = QueroCaronaReach.None;
        result.evaluatedTarget = origin;
        result.routeCost = 0;
        result.rideNeedScore = result.isEmergency ? 2000 : 0;
        result.reason = result.isEmergency
            ? $"{ResolveUnitKind(result)} em emergência de reparo: " +
              "aceita carona antes da avaliação de objetivo."
            : $"{ResolveUnitKind(result)} sem emergência de reparo.";
        request.diagnosticLog?.Invoke(result.reason);
        return result;
    }

    private static bool TryBuildCacheKey(
        QueroCaronaRequest request,
        UnitData data,
        out CacheKey key)
    {
        key = default;
        if (!Application.isPlaying
            || request?.unit == null
            || request.map == null
            || request.terrainDatabase == null
            || !ConfirmedOccupancyIndex.TryGetFor(
                request.map,
                out ConfirmedOccupancyIndex occupancy)
            || occupancy == null
            || !occupancy.CanServeLiveQueries
            || !occupancy.TryGetRecord(
                request.unit,
                out ConfirmedUnitOccupancyRecord confirmed)
            || !BoardTopologyIndex.TryGetFor(
                request.map,
                out BoardTopologyIndex topology)
            || topology == null
            || !topology.IsReady)
        {
            return false;
        }

        Vector3Int liveCell =
            request.unit.CurrentCellPosition;
        liveCell.z = 0;
        if (confirmed.cell != liveCell
            || confirmed.domain != request.unit.GetDomain()
            || confirmed.height !=
                request.unit.GetHeightLevel()
            || confirmed.slotIndex != request.unit.SlotIndex
            || confirmed.team != request.unit.TeamId
            || confirmed.isEmbarked != request.unit.IsEmbarked)
        {
            return false;
        }

        key = new CacheKey(
            request, data, occupancy, topology);
        return true;
    }

    private static int BuildRepairStateHash(UnitManager unit)
    {
        unchecked
        {
            int hash = 17;
            IReadOnlyList<UnitEmbarkedWeapon> weapons =
                unit?.GetEmbarkedWeapons();
            if (weapons == null)
                return hash;
            for (int i = 0; i < weapons.Count; i++)
            {
                UnitEmbarkedWeapon weapon = weapons[i];
                hash = (hash * 31)
                    + (weapon != null
                        ? weapon.squadAmmunition
                        : -1);
            }
            return hash;
        }
    }

    private static int BuildConstructionStateHash(
        QueroCaronaRequest request)
    {
        int scope = ((int)request.context * 397)
            ^ (int)request.plannedSector;
        if (Application.isPlaying)
        {
            int frame = Time.frameCount;
            int boardRevision =
                ThreatRevisionTracker.GlobalBoardRevision;
            if (constructionHashFrame != frame
                || constructionHashBoardRevision
                    != boardRevision)
            {
                ConstructionHashByScope.Clear();
                constructionHashFrame = frame;
                constructionHashBoardRevision =
                    boardRevision;
            }
            if (ConstructionHashByScope.TryGetValue(
                    scope, out int cached))
                return cached;
        }

        unchecked
        {
            int hash = 17;
            IReadOnlyList<ConstructionManager> constructions =
                ConstructionManager.AllActive;
            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager construction =
                    constructions[i];
                if (construction == null)
                {
                    hash = (hash * 31) - 1;
                    continue;
                }

                // Com plano, prédios fora do setor não participam da
                // resposta. Rogue/rebelde continua cobrindo todos.
                if (request.context == QueroCaronaContext.ComPlano
                    && request.plannedSector
                        != ConstructionSector.None
                    && construction.Sector
                        != request.plannedSector)
                {
                    continue;
                }

                Vector3Int cell =
                    construction.CurrentCellPosition;
                cell.z = 0;
                hash = (hash * 31) + construction.InstanceId;
                hash = (hash * 31) + cell.GetHashCode();
                hash = (hash * 31)
                    + (int)construction.TeamId;
                hash = (hash * 31)
                    + construction.CurrentCapturePoints;
                hash = (hash * 31)
                    + construction.CapturePointsMax;
                hash = (hash * 31)
                    + (construction.IsCapturable ? 1 : 0);
            }
            if (Application.isPlaying)
                ConstructionHashByScope[scope] = hash;
            return hash;
        }
    }

    private static void Store(
        CacheKey key,
        QueroCaronaResult result)
    {
        if (result == null)
            return;
        if (Cache.Count >= MaxCacheEntries
            && !Cache.ContainsKey(key))
        {
            Cache.Clear();
            AIDecisionPerf.AddCount(
                "QueroCaronaCacheEvictions");
        }
        Cache[key] = CloneResult(result);
        AIDecisionPerf.AddCount("QueroCaronaCacheStores");
    }

    private static QueroCaronaResult CloneResult(
        QueroCaronaResult source)
    {
        if (source == null)
            return null;
        return new QueroCaronaResult
        {
            wantsRide = source.wantsRide,
            isEstimate = source.isEstimate,
            isEmergency = source.isEmergency,
            isUnderRepairRuntime =
                source.isUnderRepairRuntime,
            isUnderRepairEmulated =
                source.isUnderRepairEmulated,
            repairEvaluation = source.repairEvaluation,
            isInfantry = source.isInfantry,
            reach = source.reach,
            evaluatedTarget = source.evaluatedTarget,
            evaluatedConstruction =
                source.evaluatedConstruction,
            tacticalBudget = source.tacticalBudget,
            operationalBudget = source.operationalBudget,
            routeCost = source.routeCost,
            rideNeedScore = source.rideNeedScore,
            captureClaimsBlocked =
                source.captureClaimsBlocked,
            captureClaimOwnerUnitId =
                source.captureClaimOwnerUnitId,
            reason = source.reason
        };
    }

    private static bool EvaluateRepairTriggers(
        UnitManager unit,
        UnitData data,
        out string details)
    {
        var triggered = new List<string>();
        var evaluated = new List<string>();

        bool hpTriggered = data.repairTriggerHpBelow > 0
            && unit.CurrentHP <= data.repairTriggerHpBelow;
        evaluated.Add(
            data.repairTriggerHpBelow > 0
                ? $"HP {unit.CurrentHP} <= {data.repairTriggerHpBelow}: " +
                  (hpTriggered ? "ATIVO" : "não")
                : "HP: desativado");
        if (hpTriggered)
            triggered.Add("HP");

        int maxFuel = Mathf.Max(1, unit.GetMaxFuel());
        float fuelPct = unit.CurrentFuel * 100f / maxFuel;
        bool autonomyTriggered =
            data.repairTriggerAutonomyPct > 0
            && fuelPct <= data.repairTriggerAutonomyPct;
        evaluated.Add(
            data.repairTriggerAutonomyPct > 0
                ? $"Autonomia {unit.CurrentFuel}/{maxFuel} " +
                  $"({fuelPct:0.#}%) <= " +
                  $"{data.repairTriggerAutonomyPct}%: " +
                  (autonomyTriggered ? "ATIVO" : "não")
                : "Autonomia: desativada");
        if (autonomyTriggered)
            triggered.Add("autonomia");

        bool ammoTriggered = false;
        if (data.repairTriggerAmmoEnabled)
        {
            IReadOnlyList<UnitEmbarkedWeapon> weapons =
                unit.GetEmbarkedWeapons();
            int trackedWeapons = 0;
            if (weapons != null)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    UnitEmbarkedWeapon runtimeWeapon = weapons[i];
                    int baseAmmo =
                        data.embarkedWeapons != null
                        && i < data.embarkedWeapons.Count
                        && data.embarkedWeapons[i] != null
                            ? data.embarkedWeapons[i].squadAmmunition
                            : 0;
                    if (runtimeWeapon == null || baseAmmo <= 0)
                        continue;

                    trackedWeapons++;
                    float ammoPct =
                        runtimeWeapon.squadAmmunition * 100f / baseAmmo;
                    if (ammoPct <= data.repairTriggerAmmoPct)
                    {
                        ammoTriggered = true;
                        break;
                    }
                }
            }

            evaluated.Add(
                trackedWeapons > 0
                    ? $"Munição <= {data.repairTriggerAmmoPct}%: " +
                      (ammoTriggered ? "ATIVO" : "não")
                    : "Munição: habilitada, sem arma runtime rastreável");
        }
        else
        {
            evaluated.Add("Munição: desativada");
        }
        if (ammoTriggered)
            triggered.Add("munição");

        details =
            $"Critérios AI Behavior: {string.Join(" | ", evaluated)}. " +
            (triggered.Count > 0
                ? $"Disparou: {string.Join(", ", triggered)}."
                : "Nenhum gatilho disparou.");
        return triggered.Count > 0;
    }

    private static bool TryFindBestAvailablePlannedTarget(
        QueroCaronaRequest request,
        IReadOnlyDictionary<Vector3Int, int> reach,
        CaptureOpportunityClaimSnapshot captureClaims,
        SectorManager.SectorInfo info,
        Vector3Int representative,
        out Vector3Int target,
        out ConstructionManager construction,
        out int routeCost,
        out int claimBlocks,
        out int claimOwnerUnitId)
    {
        target = Vector3Int.zero;
        construction = null;
        routeCost = int.MaxValue;
        claimBlocks = 0;
        claimOwnerUnitId = -1;

        ConstructionManager representativeConstruction =
            info.RepresentativeConstruction;
        bool representativeClaimed =
            IsClaimedByAnotherCapturer(
                captureClaims,
                representativeConstruction,
                request.unit,
                out CaptureOpportunityClaim representativeClaim);
        if (representativeClaimed)
        {
            claimBlocks++;
            claimOwnerUnitId =
                representativeClaim.Capturer != null
                    ? representativeClaim.Capturer.InstanceId
                    : -1;
        }

        if (!representativeClaimed
            && !IsClaimedByAlliedUnit(request, representative)
            && reach != null
            && reach.TryGetValue(
                representative, out int representativeCost))
        {
            target = representative;
            construction = info.RepresentativeConstruction;
            routeCost = representativeCost;
        }

        foreach (ConstructionManager candidate
                 in ConstructionManager.AllActive)
        {
            if (candidate == null
                || candidate == representativeConstruction
                || candidate.Sector != request.plannedSector
                || !candidate.IsCapturable
                || candidate.TeamId == request.unit.TeamId
                || IsClaimedByAlliedUnit(
                    request, candidate.CurrentCellPosition))
                continue;
            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            if (reach == null
                || !reach.TryGetValue(cell, out int cost)
                || cost >= routeCost)
                continue;
            if (IsClaimedByAnotherCapturer(
                    captureClaims,
                    candidate,
                    request.unit,
                    out CaptureOpportunityClaim claim))
            {
                claimBlocks++;
                claimOwnerUnitId =
                    claim.Capturer != null
                        ? claim.Capturer.InstanceId
                        : -1;
                continue;
            }
            target = cell;
            construction = candidate;
            routeCost = cost;
        }

        return routeCost < int.MaxValue;
    }

    private static bool IsClaimedByAnotherCapturer(
        CaptureOpportunityClaimSnapshot claims,
        ConstructionManager construction,
        UnitManager unit,
        out CaptureOpportunityClaim claim)
    {
        claim = default;
        if (TryGetDesignatedCaptureOwner(
                construction,
                unit,
                out UnitManager designatedOwner))
        {
            claim = new CaptureOpportunityClaim(
                construction,
                designatedOwner,
                int.MaxValue,
                false);
            return designatedOwner != unit;
        }

        return claims != null
            && construction != null
            && claims.TryGetClaim(
                construction,
                out claim)
            && claim.Capturer != null
            && claim.Capturer != unit;
    }

    private static bool TryGetDesignatedCaptureOwner(
        ConstructionManager construction,
        UnitManager requestingUnit,
        out UnitManager owner)
    {
        owner = null;
        if (construction == null
            || requestingUnit == null)
            return false;

        Vector3Int constructionCell =
            construction.CurrentCellPosition;
        constructionCell.z = 0;
        foreach (UnitManager candidate
                 in UnitManager.AllActive)
        {
            if (candidate == null
                || candidate.IsDead
                || candidate.SlotIndex
                    != requestingUnit.SlotIndex
                || !candidate.AIHasDesignatedCaptureTarget
                || candidate.TeamId == construction.TeamId
                || !candidate.TryGetUnitData(
                    out UnitData data)
                || data == null
                || !UnitRoleCompatibility.CanSatisfy(
                    data,
                    UnitRole.Capturador))
            {
                continue;
            }

            Vector3Int designatedCell =
                candidate.AIDesignatedCaptureTargetCell;
            designatedCell.z = 0;
            if (candidate
                    .AIDesignatedCaptureTargetInstanceId
                    != construction.InstanceId
                && designatedCell != constructionCell)
            {
                continue;
            }

            owner = candidate;
            return true;
        }

        return false;
    }

    private static string FormatCaptureClaimSuffix(
        int claimBlocks,
        int ownerUnitId)
    {
        if (claimBlocks <= 0)
            return string.Empty;
        return
            $"; {claimBlocks} oportunidade(s) reservada(s) " +
            "1:1 para outro capturador" +
            (ownerUnitId >= 0
                ? $" (ex.: #{ownerUnitId})"
                : string.Empty);
    }

    private static string FormatCaptureTargetLabel(
        CaptureOpportunityClaimSnapshot claims,
        ConstructionManager construction,
        UnitManager unit,
        string targetLabel)
    {
        if (claims != null
            && construction != null
            && unit != null
            && claims.TryGetClaim(
                construction,
                out CaptureOpportunityClaim claim)
            && claim.Capturer == unit)
        {
            return
                $"{targetLabel} [reserva 1:1 " +
                $"capturador=#{unit.InstanceId}]";
        }

        return targetLabel;
    }

    private static bool IsClaimedByAlliedUnit(
        QueroCaronaRequest request,
        Vector3Int cell)
    {
        if (request?.unit == null || request.map == null)
            return false;

        // Um aliado só reivindica o prédio se realmente disputar a camada do
        // passageiro. Apache/avião sobre a mesma coordenada não ocupa a
        // construção para um capturador terrestre.
        return UnitOccupancyRules.HasBlockingOccupantForUnitAtCell(
            request.map,
            cell,
            request.unit,
            alliedOnly: true);
    }

    private static void SetReachAndDecision(
        QueroCaronaResult result,
        int routeCost,
        string targetLabel)
    {
        result.wantsRide = false;
        result.rideNeedScore = 0;
        if (routeCost <= result.tacticalBudget)
        {
            result.reach = QueroCaronaReach.Tactical;
            result.reason =
                $"{ResolveUnitKind(result)} alcança {targetLabel} " +
                $"no Tactical: custo={routeCost}<=" +
                $"{result.tacticalBudget}. Recusa carona.";
            return;
        }

        result.reach = QueroCaronaReach.Operational;
        result.reason =
            $"{ResolveUnitKind(result)} alcança {targetLabel} " +
            $"no Operational: custo={routeCost}<=" +
            $"{result.operationalBudget}. Recusa carona.";
    }

    private static string ResolveUnitKind(
        QueroCaronaResult result) =>
        result.isInfantry ? "Infantaria" : "Unidade";
}
