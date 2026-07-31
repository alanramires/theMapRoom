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

    /// <summary>
    /// Nao existe rota propria a pe ate o objetivo — nem em mil turnos. Nao e
    /// "longe": e ilha, corredor bloqueado por hex mais caro que o teto de um
    /// turno, ou dominio incompativel. Quem esta assim so chega de carona, e
    /// perde de todo mundo num ranking ordenado por proximidade.
    /// </summary>
    public bool isStranded;

    /// <summary>
    /// Ha quantos turnos esta unidade esta na fila da carona. Preenchido pela
    /// IA (o servico e puro e nao conhece turno); zero quando consultado por
    /// ferramenta de Editor.
    /// </summary>
    public int rideWaitTurns;

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
///
/// POLITICA, NAO ALCANCE. A escala de urgencia, o curto-circuito de
/// emergencia, a reserva 1:1 e a escolha do objetivo sao daqui. Quao longe a
/// unidade chega e de quanto custa a rota vem do UnitReachEnvelopeService, na
/// intencao Capture — inclusive a resposta "nao alcanca", que e a ausencia de
/// banda e nao a ausencia de chave num dicionario de orcamento.
/// Ver docs/contrato_envelope_alcance.md.
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
        ResolveDiagnosticBudgets(request, result);

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

        // Alcance vem do envelope de Captura, nas duas bandas. Nao ha mais
        // dicionario de orcamento MP x N: o turno atual vale o que sobrou, os
        // seguintes valem MP cheio, e um hex mais caro que o teto de um turno e
        // intransponivel. E o que impede a recusa de carona por alcance
        // fantasma — soldado de 3 MP numa cordilheira de custo 2 entra em UMA
        // montanha por turno, nao em tres com um bolso de 6.
        UnitReachProfile captureReach = BuildCaptureReachProfile(request);
        AIDecisionPerf.AddCount("QueroCaronaCaptureReachBuilds");
        CaptureOpportunityClaimSnapshot captureClaims =
            UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador)
                // Mesmo envelope para os dois servicos: unidade + intencao +
                // banda. O reuso deixou de ser igualdade de orcamento inteiro e
                // virou identidade de envelope.
                ? CaptureOpportunityClaimService.GetOrBuild(
                    request,
                    captureReach)
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
            if (TryResolveCaptureBand(
                    captureReach,
                    explicitTarget,
                    out int explicitCost,
                    out ReachBand explicitBand))
            {
                result.routeCost = explicitCost;
                SetReachAndDecision(
                    result,
                    request,
                    captureReach,
                    explicitTarget,
                    explicitCost,
                    explicitBand,
                    targetLabel);
            }
            else
            {
                ApplyBeyondReachRideNeed(
                    request, result, explicitTarget);
                result.reason =
                    $"{ResolveUnitKind(result)} nao alcanca " +
                    $"{targetLabel} em Tactical ou Operational" +
                    FormatStrandedSuffix(result) + ": aceita carona.";
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
                // Sem representante nao ha celula de objetivo, entao nao da
                // para perguntar se existe rota propria: fica na urgencia
                // basica, sem classificar fome estrutural.
                result.wantsRide = true;
                result.reach = QueroCaronaReach.BeyondOperational;
                result.rideNeedScore = BeyondOperationalRideNeedScore;
                result.reason =
                    "Plano sem representante válido no SectorManager; " +
                    "estimativa aceita carona.";
                return Finish();
            }

            Vector3Int representative = info.RepresentativeCell;
            representative.z = 0;
            if (TryFindBestAvailablePlannedTarget(
                    request,
                    captureReach,
                    captureClaims,
                    info,
                    representative,
                    out Vector3Int plannedTarget,
                    out ConstructionManager plannedConstruction,
                    out int plannedCost,
                    out ReachBand plannedBand,
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
                    request,
                    captureReach,
                    plannedTarget,
                    plannedCost,
                    plannedBand,
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
                ApplyBeyondReachRideNeed(
                    request, result, representative);
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
                    FormatStrandedSuffix(result) +
                    ": aceita carona.";
            }
            request.diagnosticLog?.Invoke(result.reason);
            return Finish();
        }

        ConstructionManager nearest = null;
        int nearestCost = int.MaxValue;
        ReachBand nearestBand = ReachBand.Tactical;
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
            if (!TryResolveCaptureBand(
                    captureReach,
                    cell,
                    out int cost,
                    out ReachBand band)
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
            nearestBand = band;
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
                result, request, captureReach, target,
                nearestCost, nearestBand,
                FormatCaptureTargetLabel(
                    captureClaims,
                    nearest,
                    request.unit,
                    $"prédio capturável próximo {target}"));
        }
        else
        {
            ApplyBeyondReachRideNeedForAnyCapturable(request, result);
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
                FormatStrandedSuffix(result) +
                ": aceita carona.";
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
        ResolveDiagnosticBudgets(request, result);
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

    // ------------------------------------------------------------------
    // Fome do caroneiro: quem NAO TEM ROTA PROPRIA vs quem so esta longe.
    //
    // O envelope tem duas bandas e para no 2o turno — por contrato. Ele nao
    // sabe dizer se o que ficou de fora e turno 3 ou ilha. Sem essa distincao
    // todo pedido vale o mesmo, e o ilhado perde para sempre de quem esta a
    // tres hexes, porque o ranking do transporte ordena por proximidade.
    //
    // A pergunta certa e a consulta dirigida do contrato: "a unidade possui
    // rota propria completa ate a missao?". MobilityRelation.OtherComponent e,
    // literalmente, "pedido de carona".
    // ------------------------------------------------------------------

    private const int BeyondOperationalRideNeedScore = 1000;

    /// <summary>
    /// Entre "quero carona" (1000) e emergencia de reparo (2000). Ferido morre;
    /// ilhado so espera — mas espera para sempre, e nenhum dos dois anda.
    /// </summary>
    private const int StrandedRideNeedScore = 1500;

    private const int MaxMobilityComponentEntries = 32;

    /// <summary>
    /// Chave do componente: PERFIL DE MOVIMENTO, sem origem.
    ///
    /// Duas unidades do mesmo perfil na mesma massa de terra tem literalmente o
    /// mesmo componente — reconhecer isso e a diferenca entre um flood fill por
    /// unidade e um por perfil. Com quinze rogues pedindo carona no mesmo
    /// turno, a versao com origem na chave custou dezenas de segundos.
    ///
    /// O teto de MP entra porque o componente depende dele: hex mais caro que o
    /// teto de um turno e intransponivel para sempre. NAO entra
    /// GlobalBoardRevision — o componente e terreno e construcao, nao ocupacao,
    /// e essa revisao muda a cada passo de unidade.
    /// </summary>
    private readonly struct MobilityProfileKey
        : IEquatable<MobilityProfileKey>
    {
        private readonly int unitDataObjectId;
        private readonly int mapObjectId;
        private readonly int maxMovement;
        private readonly Domain domain;
        private readonly HeightLevel height;
        private readonly bool isEmbarked;
        private readonly int topologyVersion;
        private readonly string topologyFingerprint;

        public MobilityProfileKey(
            QueroCaronaRequest request,
            UnitData data,
            BoardTopologyIndex topology)
        {
            UnitManager unit = request.unit;
            unitDataObjectId = data != null
                ? data.GetEntityId().GetHashCode()
                : 0;
            mapObjectId = request.map.GetEntityId().GetHashCode();
            maxMovement = Mathf.Max(0, unit.MaxMovementPoints);
            domain = unit.GetDomain();
            height = unit.GetHeightLevel();
            isEmbarked = unit.IsEmbarked;
            topologyVersion = topology != null
                ? topology.TopologyVersion
                : -1;
            topologyFingerprint = topology != null
                ? topology.TopologyFingerprint ?? string.Empty
                : string.Empty;
        }

        public bool Equals(MobilityProfileKey other)
        {
            return unitDataObjectId == other.unitDataObjectId
                && mapObjectId == other.mapObjectId
                && maxMovement == other.maxMovement
                && domain == other.domain
                && height == other.height
                && isEmbarked == other.isEmbarked
                && topologyVersion == other.topologyVersion
                && string.Equals(
                    topologyFingerprint,
                    other.topologyFingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MobilityProfileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = unitDataObjectId;
                hash = (hash * 397) ^ mapObjectId;
                hash = (hash * 397) ^ maxMovement;
                hash = (hash * 397) ^ (int)domain;
                hash = (hash * 397) ^ (int)height;
                hash = (hash * 397) ^ (isEmbarked ? 1 : 0);
                hash = (hash * 397) ^ topologyVersion;
                hash = (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(
                        topologyFingerprint ?? string.Empty);
                return hash;
            }
        }
    }

    private static readonly Dictionary<
        MobilityProfileKey,
        List<Dictionary<Vector3Int, int>>> MobilityComponentCache =
            new Dictionary<
                MobilityProfileKey,
                List<Dictionary<Vector3Int, int>>>();

    /// <summary>
    /// Malha de movimento proprio SEM TETO de turnos. Cara — flood fill do
    /// tabuleiro — entao so e construida para quem ja falhou as duas bandas,
    /// ou seja, para quem vai pedir carona de qualquer jeito. E, dentro disso,
    /// uma vez por PERFIL por massa de terra, nao uma vez por unidade.
    /// </summary>
    private static Dictionary<Vector3Int, int> ResolveOwnMovementComponent(
        QueroCaronaRequest request)
    {
        if (!request.unit.TryGetUnitData(out UnitData data))
            data = null;
        BoardTopologyIndex topology =
            BoardTopologyIndex.TryGetFor(
                request.map, out BoardTopologyIndex resolved)
                ? resolved
                : null;

        Vector3Int origin = request.unit.CurrentCellPosition;
        origin.z = 0;
        var key = new MobilityProfileKey(request, data, topology);
        if (!MobilityComponentCache.TryGetValue(
                key, out List<Dictionary<Vector3Int, int>> known))
        {
            if (MobilityComponentCache.Count >= MaxMobilityComponentEntries)
                MobilityComponentCache.Clear();
            known = new List<Dictionary<Vector3Int, int>>(2);
            MobilityComponentCache[key] = known;
        }

        // Ja conheco um componente deste perfil que contem a origem? Entao e
        // exatamente o componente dela.
        for (int i = 0; i < known.Count; i++)
        {
            if (known[i].ContainsKey(origin))
            {
                AIDecisionPerf.AddCount("QueroCaronaMobilityComponentHits");
                return known[i];
            }
        }

        AIDecisionPerf.AddCount("QueroCaronaMobilityComponentBuilds");
        Dictionary<Vector3Int, int> component =
            UnitReachEnvelopeService.BuildOwnMovementComponent(
                request.unit,
                request.map,
                request.terrainDatabase);
        known.Add(component);
        return component;
    }

    /// <summary>
    /// Necessidade de carona de quem ficou fora das duas bandas, dirigida a UM
    /// objetivo ja escolhido. Fora do componente proprio a unidade nunca chega
    /// andando: a carona deixa de ser conveniencia e vira a unica rota.
    /// </summary>
    private static void ApplyBeyondReachRideNeed(
        QueroCaronaRequest request,
        QueroCaronaResult result,
        Vector3Int objectiveCell)
    {
        result.wantsRide = true;
        result.reach = QueroCaronaReach.BeyondOperational;
        result.rideNeedScore = BeyondOperationalRideNeedScore;
        result.isStranded = false;

        objectiveCell.z = 0;
        MobilityRelation relation =
            UnitReachEnvelopeService.ClassifyMobility(
                request.unit,
                request.map,
                request.terrainDatabase,
                objectiveCell,
                ResolveOwnMovementComponent(request));
        if (relation == MobilityRelation.OwnComponent)
            return;

        result.isStranded = true;
        result.rideNeedScore = StrandedRideNeedScore;
    }

    /// <summary>
    /// Versao do rogue/rebelde, que nao tem UM objetivo: a pergunta vira
    /// "existe ALGUM capturavel dentro do meu componente?". Nenhum = ilhado.
    /// </summary>
    private static void ApplyBeyondReachRideNeedForAnyCapturable(
        QueroCaronaRequest request,
        QueroCaronaResult result)
    {
        result.wantsRide = true;
        result.reach = QueroCaronaReach.BeyondOperational;
        result.rideNeedScore = BeyondOperationalRideNeedScore;
        result.isStranded = false;

        Dictionary<Vector3Int, int> component =
            ResolveOwnMovementComponent(request);
        IReadOnlyList<ConstructionManager> constructions =
            ConstructionManager.AllActive;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null
                || !construction.IsCapturable
                || construction.TeamId == request.unit.TeamId)
            {
                continue;
            }

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;
            if (component.ContainsKey(cell))
                return;
        }

        result.isStranded = true;
        result.rideNeedScore = StrandedRideNeedScore;
    }

    /// <summary>Sufixo de diagnostico da fome estrutural.</summary>
    private static string FormatStrandedSuffix(QueroCaronaResult result)
    {
        return result.isStranded
            ? " SEM ROTA PRÓPRIA (só chega de carona)"
            : string.Empty;
    }

    /// <summary>
    /// Envelope de Captura da unidade, nas duas bandas. E a UNICA fonte de
    /// alcance deste servico.
    ///
    /// A subetapa e sempre Terrestre porque a arvore da Captura nao tem ramo
    /// aereo; para quem e isAircraft o proprio servico resolve a geometria
    /// cubica. IncludeMovementCosts liga o custo real por celula na banda
    /// Tactical — este servico precisa do numero, nao so da pertinencia.
    /// </summary>
    private static UnitReachProfile BuildCaptureReachProfile(
        QueroCaronaRequest request)
    {
        return UnitReachEnvelopeService.BuildProfile(
            new UnitReachRequest
            {
                Unit = request.unit,
                BoardMap = request.map,
                TerrainDatabase = request.terrainDatabase,
                Intent = ReachIntent.Capture,
                SubStep = ReachSubStep.Terrestre,
                Band = ReachBand.Tactical,
                OperationalTurns = Mathf.Max(
                    1, request.operationalTurns),
                IncludeMovementCosts = true
            });
    }

    /// <summary>
    /// Em qual banda a unidade materializa a captura desta celula, e a que
    /// custo. Quem responde e o envelope; nao ha teste numerico de orcamento
    /// aqui. Fora das duas bandas devolve false — e o que a IA le como
    /// "preciso de carona".
    /// </summary>
    private static bool TryResolveCaptureBand(
        UnitReachProfile captureReach,
        Vector3Int cell,
        out int routeCost,
        out ReachBand band)
    {
        routeCost = int.MaxValue;
        band = ReachBand.Tactical;
        if (captureReach == null
            || !captureReach.TryClassify(cell, out band))
        {
            return false;
        }

        // O custo sai do envelope que classificou a celula: ele ja o calculou
        // para responder a pertinencia. O outro envelope serve de reserva
        // porque as duas bandas usam a mesma metrica de MP a partir da origem.
        UnitReachEnvelope classifier =
            band == ReachBand.Tactical
                ? captureReach.Tactical
                : captureReach.Operational;
        if (classifier != null
            && classifier.TryGetCost(cell, out routeCost))
        {
            return true;
        }

        UnitReachEnvelope fallback =
            band == ReachBand.Tactical
                ? captureReach.Operational
                : captureReach.Tactical;
        if (fallback != null
            && fallback.TryGetCost(cell, out routeCost))
        {
            return true;
        }

        // Alcance sem custo publicado nao acontece nas duas bandas de Captura.
        // Se acontecer, a falha e conservadora: sem numero, o servico prefere
        // pedir carona a inventar uma rota barata. Pedido, nao ordem.
        routeCost = int.MaxValue;
        return false;
    }

    /// <summary>
    /// Numeros de DIAGNOSTICO, nao de decisao. O alcance real e o envelope;
    /// estes tetos existem para o painel e para o texto da razao. O teto
    /// Operational segue o contrato — turno atual vale o que sobrou, os
    /// seguintes valem MP cheio — e nunca MP x N num bolso so.
    /// </summary>
    private static void ResolveDiagnosticBudgets(
        QueroCaronaRequest request,
        QueroCaronaResult result)
    {
        UnitManager unit = request.unit;
        int tactical =
            AIActionReachCoordinator.ResolveTacticalBudget(unit);
        int laterTurns = Mathf.Max(1, request.operationalTurns) - 1;
        result.tacticalBudget = tactical;
        result.operationalBudget =
            tactical
            + Mathf.Max(0, unit.MaxMovementPoints) * laterTurns;
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
            isStranded = source.isStranded,
            rideWaitTurns = source.rideWaitTurns,
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
        UnitReachProfile captureReach,
        CaptureOpportunityClaimSnapshot captureClaims,
        SectorManager.SectorInfo info,
        Vector3Int representative,
        out Vector3Int target,
        out ConstructionManager construction,
        out int routeCost,
        out ReachBand routeBand,
        out int claimBlocks,
        out int claimOwnerUnitId)
    {
        target = Vector3Int.zero;
        construction = null;
        routeCost = int.MaxValue;
        routeBand = ReachBand.Tactical;
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
            && TryResolveCaptureBand(
                captureReach,
                representative,
                out int representativeCost,
                out ReachBand representativeBand))
        {
            target = representative;
            construction = info.RepresentativeConstruction;
            routeCost = representativeCost;
            routeBand = representativeBand;
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
            if (!TryResolveCaptureBand(
                    captureReach,
                    cell,
                    out int cost,
                    out ReachBand band)
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
            routeBand = band;
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

    /// <summary>
    /// A banda vem do envelope, nao de comparacao de inteiro.
    ///
    /// O teste antigo (routeCost &lt;= tacticalBudget, com Operational inferido
    /// de "estar no dicionario") media alcance contra um orcamento de MP x N
    /// num bolso so. Ele dava alcance que o jogo nao aceita — e recusar carona
    /// por alcance que nao existe e o pior erro possivel deste servico, porque
    /// deixa a unidade andando sozinha rumo a um objetivo inalcancavel.
    ///
    /// O DIAGNOSTICO PUBLICA O TURNO, NAO UM TETO. O que e fixo aqui e a banda
    /// — duas rodadas, por definicao do contrato. O alcance dentro dela e
    /// variavel e depende do terreno: a 3a montanha de custo 2, para 3 MP,
    /// acumula 6 e cai no TURNO 3, fora das bandas. Imprimir um teto de MP x N
    /// ao lado do custo ressuscita justamente o predicado que foi removido.
    /// </summary>
    private static void SetReachAndDecision(
        QueroCaronaResult result,
        QueroCaronaRequest request,
        UnitReachProfile captureReach,
        Vector3Int cell,
        int routeCost,
        ReachBand band,
        string targetLabel)
    {
        int bandTurns = Mathf.Max(1, request.operationalTurns);
        result.wantsRide = false;
        result.rideNeedScore = 0;
        int turns = ResolveTurnsToCell(
            captureReach, cell, band, bandTurns);
        if (band == ReachBand.Tactical)
        {
            result.reach = QueroCaronaReach.Tactical;
            result.reason =
                $"{ResolveUnitKind(result)} alcança {targetLabel} " +
                $"no Tactical: custo={routeCost} no turno {turns} " +
                "(nesta rodada). Recusa carona.";
            return;
        }

        result.reach = QueroCaronaReach.Operational;
        result.reason =
            $"{ResolveUnitKind(result)} alcança {targetLabel} " +
            $"no Operational: custo={routeCost} no turno {turns} " +
            $"de {bandTurns}. Recusa carona.";
    }

    /// <summary>
    /// Em qual turno a unidade chega nesta celula, segundo o envelope que a
    /// classificou. E o numero que descreve o alcance de verdade — o custo
    /// acumulado sozinho nao diz nada, porque MP nao acumula entre turnos.
    /// </summary>
    private static int ResolveTurnsToCell(
        UnitReachProfile captureReach,
        Vector3Int cell,
        ReachBand band,
        int bandTurns)
    {
        UnitReachEnvelope envelope =
            band == ReachBand.Tactical
                ? captureReach?.Tactical
                : captureReach?.Operational;
        return envelope != null && envelope.TryGetTurns(cell, out int turns)
            ? Mathf.Max(1, turns)
            : band == ReachBand.Tactical ? 1 : bandTurns;
    }

    private static string ResolveUnitKind(
        QueroCaronaResult result) =>
        result.isInfantry ? "Infantaria" : "Unidade";
}
