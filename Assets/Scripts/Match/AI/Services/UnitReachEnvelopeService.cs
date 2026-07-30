using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Refinamento da banda Tactical para a intencao de combate.
/// Potential usa o movimento maximo (o que a unidade poderia fazer num turno
/// inteiro); CurrentTurn usa o movimento que ainda resta agora.
/// </summary>
public enum UnitThreatEnvelopeMovement
{
    Potential = 0,
    CurrentTurn = 1
}

/// <summary>
/// Primeiro eixo do envelope: O QUE a unidade quer materializar no destino.
/// Cada intencao delega a legalidade e o custo de entrada ao seu proprio
/// sensor Pode*; nenhuma delas reimplementa a regra.
/// </summary>
public enum ReachIntent
{
    Combat = 0,
    Service = 1,
    Transfer = 2,
    Fusion = 3,
    Embark = 4
}

/// <summary>
/// Segundo eixo do envelope: QUAO LONGE no tempo.
///
/// Tactical    - rodada atual, com o movimento disponivel agora.
/// Operational - rota propria em MP x N turnos; custo real por celula.
/// Strategic   - fora da rota propria. NAO materializa celulas: e apenas
///               direcao. Ver <see cref="UnitReachProfile.TryResolveStrategicBearing"/>.
/// </summary>
public enum ReachBand
{
    Tactical = 0,
    Operational = 1,
    Strategic = 2
}

/// <summary>
/// Envelope de alcance materializavel de uma unidade para uma intencao numa
/// banda. Substitui o antigo envelope "de ameaca": ameaca passou a ser apenas
/// uma das intencoes.
/// </summary>
public class UnitReachEnvelope
{
    public readonly ReachIntent Intent;
    public readonly ReachBand Band;

    /// <summary>Caminhos legais por destino. Vazio quando a origem veio de um mapa de custo.</summary>
    public readonly Dictionary<Vector3Int, List<Vector3Int>> PathsByDestination;

    /// <summary>Custo real de movimento por celula alcancavel, quando conhecido.</summary>
    public readonly Dictionary<Vector3Int, int> CostByCell;

    /// <summary>Celulas onde a unidade pode parar.</summary>
    public readonly HashSet<Vector3Int> MovementCells;

    /// <summary>Celulas onde a intencao se materializa (inclui o proprio movimento quando aplicavel).</summary>
    public readonly HashSet<Vector3Int> AttackableCells;

    /// <summary>Mesma colecao de <see cref="MovementCells"/> em forma de lista, para pintura.</summary>
    public readonly List<Vector3Int> RangeCells;

    /// <summary>Anel externo: acao menos movimento. E o que as ferramentas pintam como alcance.</summary>
    public readonly List<Vector3Int> LineCells;

    internal UnitReachEnvelope(
        ReachIntent intent,
        ReachBand band,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        HashSet<Vector3Int> movementCells,
        HashSet<Vector3Int> actionCells,
        HashSet<Vector3Int> lineCells)
    {
        Intent = intent;
        Band = band;
        PathsByDestination = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>();
        CostByCell = costByCell ?? new Dictionary<Vector3Int, int>();
        MovementCells = movementCells ?? new HashSet<Vector3Int>();
        AttackableCells = actionCells ?? new HashSet<Vector3Int>();
        RangeCells = new List<Vector3Int>(MovementCells);
        LineCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>();
    }

    /// <summary>Nome preferido de <see cref="AttackableCells"/> fora do contexto de combate.</summary>
    public HashSet<Vector3Int> ActionCells => AttackableCells;

    /// <summary>Nome preferido de <see cref="LineCells"/>: o anel de acao fora do movimento.</summary>
    public List<Vector3Int> OuterCells => LineCells;

    /// <summary>A intencao se materializa nesta celula?</summary>
    public bool CanAct(Vector3Int cell)
    {
        cell.z = 0;
        return AttackableCells.Contains(cell);
    }

    /// <summary>Alias historico de <see cref="CanAct"/> para consumidores de combate.</summary>
    public bool CanThreaten(Vector3Int cell) => CanAct(cell);

    /// <summary>A unidade consegue parar nesta celula?</summary>
    public bool CanReach(Vector3Int cell)
    {
        cell.z = 0;
        return MovementCells.Contains(cell);
    }

    public bool TryGetCost(Vector3Int cell, out int cost)
    {
        cell.z = 0;
        return CostByCell.TryGetValue(cell, out cost);
    }
}

/// <summary>
/// Compatibilidade temporaria durante a migracao para <see cref="UnitReachEnvelope"/>.
/// Nao acrescenta comportamento: existe apenas para que os consumidores tipados
/// como envelope de ameaca continuem compilando enquanto migram.
/// </summary>
public sealed class UnitThreatEnvelope : UnitReachEnvelope
{
    internal UnitThreatEnvelope(
        ReachIntent intent,
        ReachBand band,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        HashSet<Vector3Int> movementCells,
        HashSet<Vector3Int> actionCells,
        HashSet<Vector3Int> lineCells)
        : base(intent, band, paths, costByCell, movementCells, actionCells, lineCells)
    {
    }
}

/// <summary>
/// Composicao das duas bandas materializaveis de uma mesma intencao.
/// E o objeto que as ferramentas consomem: era exatamente esta composicao que
/// vivia solta dentro da janela de Editor.
/// </summary>
public sealed class UnitReachProfile
{
    public readonly ReachIntent Intent;
    public readonly UnitReachEnvelope Tactical;
    public readonly UnitReachEnvelope Operational;

    internal UnitReachProfile(
        ReachIntent intent,
        UnitReachEnvelope tactical,
        UnitReachEnvelope operational)
    {
        Intent = intent;
        Tactical = tactical;
        Operational = operational;
    }

    /// <summary>
    /// Classifica uma celula nas tres bandas. Strategic e o complemento: nao
    /// existe conjunto de celulas Strategic, existe ausencia de rota propria.
    /// </summary>
    public ReachBand Classify(Vector3Int cell)
    {
        cell.z = 0;
        if (Tactical != null && Tactical.CanAct(cell))
            return ReachBand.Tactical;
        if (Operational != null && Operational.CanAct(cell))
            return ReachBand.Operational;
        return ReachBand.Strategic;
    }

    /// <summary>
    /// Unico uso legitimo da banda Strategic: direcao.
    ///
    /// Devolve a celula da rota propria (Operational, ou Tactical quando nao ha
    /// Operational) que mais aproxima a unidade do alvo distante, junto da
    /// distancia cubica restante. Nao abre pathfinding novo: consome a malha ja
    /// calculada. A distancia apenas ordena; nunca substitui caminhos ou sensores.
    /// </summary>
    public bool TryResolveStrategicBearing(
        Vector3Int target,
        out Vector3Int bearingCell,
        out int remainingCubicDistance)
    {
        target.z = 0;
        bearingCell = target;
        remainingCubicDistance = int.MaxValue;

        UnitReachEnvelope source = Operational ?? Tactical;
        if (source == null || source.MovementCells.Count == 0)
            return false;

        int bestCost = int.MaxValue;
        foreach (Vector3Int cell in source.MovementCells)
        {
            int distance = AIActionReachCoordinator.CubicDistance(cell, target);
            if (distance > remainingCubicDistance)
                continue;

            int cost = source.CostByCell.TryGetValue(cell, out int known)
                ? known
                : 0;
            // Empate de direcao resolve pelo menor custo: aproxima sem gastar
            // rota a mais do que o necessario.
            if (distance == remainingCubicDistance && cost >= bestCost)
                continue;

            remainingCubicDistance = distance;
            bestCost = cost;
            bearingCell = cell;
        }

        return remainingCubicDistance != int.MaxValue;
    }
}

/// <summary>
/// Descreve um pedido de envelope. Existe como classe para que o consumidor
/// preencha apenas o que a sua intencao exige.
/// </summary>
public sealed class UnitReachRequest
{
    public UnitManager Unit;
    public Tilemap BoardMap;
    public TerrainDatabase TerrainDatabase;
    public ReachIntent Intent = ReachIntent.Combat;
    public ReachBand Band = ReachBand.Tactical;

    /// <summary>Orcamento explicito. Quando 0 ou negativo, resolve pela banda.</summary>
    public int MovementBudget;
    public int OperationalTurns = 2;

    /// <summary>Reuso obrigatorio quando o chamador ja tem a onda de movimento.</summary>
    public Dictionary<Vector3Int, List<Vector3Int>> PrebuiltPaths;

    /// <summary>Reuso obrigatorio quando o chamador ja tem a malha de custo.</summary>
    public Dictionary<Vector3Int, int> PrebuiltCosts;

    /// <summary>
    /// Restringe a acao as celulas cuja camada primaria a unidade opera.
    /// Ferramentas de apresentacao usam true; a IA usa false porque ja valida
    /// cada candidato pelo sensor. Ver nota de paridade no servico.
    /// </summary>
    public bool FilterByOperationDomain;

    /// <summary>Sobrescreve o alcance de servico. Sem valor, resolve pela intencao.</summary>
    public SupplierRangeMode? RangeOverride;

    // Combate.
    public UnitThreatEnvelopeMovement MovementMode = UnitThreatEnvelopeMovement.CurrentTurn;
    public DPQAirHeightConfig DpqAirHeightConfig;
    public bool EnableLdt = true;
    public bool EnableLos = true;
    public bool EnableSpotter = true;

    // Embarque.
    public UnitManager EmbarkPassenger;
}

/// <summary>
/// Fonte unica do envelope de alcance materializavel, consumida por Editor,
/// panel helper e IA.
///
/// DOIS EIXOS
///   intencao (<see cref="ReachIntent"/>) x banda (<see cref="ReachBand"/>)
///
/// A intencao decide o custo de entrada e a legalidade no destino, sempre
/// delegando ao sensor correspondente:
///   Combat   -> PodeMirarSensor
///   Service  -> PodeSuprirSensor  (serviceRange)
///   Transfer -> PodeSuprirSensor  (collectionRange)
///   Fusion   -> PodeFundirSensor.TryResolveMergeEnterCost
///   Embark   -> PodeEmbarcarSensor.TryGetEmbarkCostAtCell
///
/// A banda decide o orcamento e a geometria, reaproveitando o que ja existe:
/// UnitMovementPathRules para caminhos e custos, AIActionReachCoordinator para
/// o reach setorial e a metrica cubica das aeronaves. O servico NAO implementa
/// pathfinding proprio e aceita malhas ja calculadas pelo chamador.
///
/// Strategic nao materializa celulas. E o complemento das duas bandas com rota
/// propria e serve apenas como direcao.
///
/// CONTRATO TRANSACIONAL: o servico e puro. Nao move unidades, nao altera
/// ocupacao, FOW, deteccao, recursos, revisoes nem estado de turno. Pode ser
/// chamado livremente em estados provisorios do cursor.
/// </summary>
public static class UnitReachEnvelopeService
{
    private enum CombatProfile
    {
        None,
        Movement,
        DistanceStatic,
        Hybrid
    }

    private sealed class CacheEntry
    {
        public int KeyHash;
        public UnitThreatEnvelope Envelope;
    }

    private static readonly Dictionary<long, CacheEntry> Cache =
        new Dictionary<long, CacheEntry>();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    /// <summary>
    /// Constroi as duas bandas materializaveis de uma intencao numa passada.
    ///
    /// Orcamento e reuso (PrebuiltPaths/PrebuiltCosts) valem apenas para a
    /// banda declarada em <see cref="UnitReachRequest.Band"/>; a outra deriva o
    /// seu proprio do <see cref="AIActionReachCoordinator"/>. Um pedido com
    /// Band = Tactical e MovementBudget = MaxMovementPoints produz o par
    /// "turno cheio agora" + "MP x N turnos", que e o que as ferramentas pintam.
    /// </summary>
    public static UnitReachProfile BuildProfile(UnitReachRequest request)
    {
        if (request == null || request.Unit == null)
            return new UnitReachProfile(ReachIntent.Combat, null, null);

        UnitReachEnvelope tactical = Build(CloneForBand(request, ReachBand.Tactical));
        UnitReachEnvelope operational = Build(CloneForBand(request, ReachBand.Operational));
        return new UnitReachProfile(request.Intent, tactical, operational);
    }

    /// <summary>
    /// Constroi um envelope. Devolve null quando a unidade nao possui a
    /// capacidade exigida pela intencao (ex.: combate sem arma util).
    /// </summary>
    public static UnitReachEnvelope Build(UnitReachRequest request)
    {
        if (request == null || request.Unit == null)
            return null;

        UnitManager unit = request.Unit;
        Tilemap map = request.BoardMap != null ? request.BoardMap : unit.BoardTilemap;
        if (map == null)
            return null;

        // Strategic nao materializa. Quem precisa de direcao usa o profile.
        if (request.Band == ReachBand.Strategic)
        {
            return new UnitReachEnvelope(
                request.Intent,
                ReachBand.Strategic,
                null,
                null,
                null,
                null,
                null);
        }

        // A expansao de tiro so e confirmada na rodada atual, onde o sensor
        // responde sobre o tabuleiro real. Na banda Operational o combate vale
        // como alcance/posicionamento, nao como promessa de tiro: devolve a
        // malha de progressao pura, sem projetar fogo a dois turnos.
        if (request.Intent == ReachIntent.Combat
            && request.Band == ReachBand.Tactical)
        {
            return BuildCombat(request, map, out _);
        }

        int budget = ResolveBudget(request);
        ResolveMovement(
            request,
            map,
            budget,
            out Dictionary<Vector3Int, List<Vector3Int>> paths,
            out Dictionary<Vector3Int, int> costByCell);

        IEnumerable<Vector3Int> reachableKeys = paths.Count > 0
            ? (IEnumerable<Vector3Int>)paths.Keys
            : costByCell.Keys;
        var movementCells = new HashSet<Vector3Int>();
        foreach (Vector3Int rawCell in reachableKeys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (map.GetTile(cell) != null)
                movementCells.Add(cell);
        }

        HashSet<Vector3Int> actionCells;
        switch (request.Intent)
        {
            case ReachIntent.Service:
            case ReachIntent.Transfer:
                actionCells = ExpandByServiceRange(
                    request, map, movementCells);
                break;
            case ReachIntent.Fusion:
                actionCells = ExpandByEntryCost(
                    request, map, movementCells, paths, costByCell, budget,
                    ResolveFusionEnterCost);
                break;
            case ReachIntent.Embark:
                actionCells = ExpandByEntryCost(
                    request, map, movementCells, paths, costByCell, budget,
                    ResolveEmbarkEnterCost);
                break;
            default:
                actionCells = new HashSet<Vector3Int>(movementCells);
                break;
        }

        if (request.FilterByOperationDomain)
            ApplyOperationDomainFilter(request, map, actionCells);

        var lineCells = new HashSet<Vector3Int>(actionCells);
        lineCells.ExceptWith(movementCells);

        return new UnitReachEnvelope(
            request.Intent,
            request.Band,
            paths,
            costByCell,
            movementCells,
            actionCells,
            lineCells);
    }

    // ------------------------------------------------------------------
    // Orcamento e movimento — reuso, nunca pathfinding novo.
    // ------------------------------------------------------------------

    private static int ResolveBudget(UnitReachRequest request)
    {
        if (request.MovementBudget > 0)
            return request.MovementBudget;

        return request.Band == ReachBand.Operational
            ? AIActionReachCoordinator.ResolveOperationalBudget(
                request.Unit, request.OperationalTurns)
            : AIActionReachCoordinator.ResolveTacticalBudget(request.Unit);
    }

    private static void ResolveMovement(
        UnitReachRequest request,
        Tilemap map,
        int budget,
        out Dictionary<Vector3Int, List<Vector3Int>> paths,
        out Dictionary<Vector3Int, int> costByCell)
    {
        if (request.PrebuiltPaths != null)
        {
            paths = request.PrebuiltPaths;
            costByCell = request.PrebuiltCosts ?? new Dictionary<Vector3Int, int>();
            return;
        }

        if (request.PrebuiltCosts != null)
        {
            paths = new Dictionary<Vector3Int, List<Vector3Int>>();
            costByCell = request.PrebuiltCosts;
            return;
        }

        Vector3Int origin = request.Unit.CurrentCellPosition;
        origin.z = 0;

        // Aeronaves e a banda Operational ja tem malha propria no coordenador:
        // custo real para unidades geograficas, cubico para aeronauticas.
        if (request.Band == ReachBand.Operational
            || AIActionReachCoordinator.UsesCubicSectorReach(request.Unit))
        {
            costByCell = AIActionReachCoordinator.BuildSectorReachMap(
                request.Unit,
                map,
                request.TerrainDatabase,
                origin,
                Mathf.Max(0, budget));
            // A malha setorial nao produz caminhos. Publicar os destinos com
            // caminho nulo mantem PathsByDestination como indice de alcance,
            // que e o formato que os consumidores herdados ja esperavam.
            paths = new Dictionary<Vector3Int, List<Vector3Int>>(costByCell.Count);
            foreach (Vector3Int cell in costByCell.Keys)
                paths[cell] = null;
            return;
        }

        paths = UnitMovementPathRules.CalcularCaminhosValidos(
            map,
            request.Unit,
            Mathf.Max(0, budget),
            request.TerrainDatabase);
        costByCell = new Dictionary<Vector3Int, int>();
    }

    private static UnitReachRequest CloneForBand(
        UnitReachRequest source, ReachBand band)
    {
        return new UnitReachRequest
        {
            Unit = source.Unit,
            BoardMap = source.BoardMap,
            TerrainDatabase = source.TerrainDatabase,
            Intent = source.Intent,
            Band = band,
            // O orcamento explicito so vale para a banda que o chamador pediu.
            MovementBudget = band == source.Band ? source.MovementBudget : 0,
            OperationalTurns = source.OperationalTurns,
            PrebuiltPaths = band == source.Band ? source.PrebuiltPaths : null,
            PrebuiltCosts = band == source.Band ? source.PrebuiltCosts : null,
            FilterByOperationDomain = source.FilterByOperationDomain,
            RangeOverride = source.RangeOverride,
            MovementMode = band == ReachBand.Operational
                ? UnitThreatEnvelopeMovement.Potential
                : source.MovementMode,
            DpqAirHeightConfig = source.DpqAirHeightConfig,
            EnableLdt = source.EnableLdt,
            EnableLos = source.EnableLos,
            EnableSpotter = source.EnableSpotter,
            EmbarkPassenger = source.EmbarkPassenger
        };
    }

    // ------------------------------------------------------------------
    // Intencao: servico e transferencia (PodeSuprirSensor).
    // ------------------------------------------------------------------

    private static HashSet<Vector3Int> ExpandByServiceRange(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> movementCells)
    {
        var actionCells = new HashSet<Vector3Int>();
        SupplierRangeMode range = ResolveServiceRange(request);
        var neighbors = new List<Vector3Int>(6);

        foreach (Vector3Int moveCell in movementCells)
        {
            if (range == SupplierRangeMode.Hybrid0Or1Hex
                || range == SupplierRangeMode.SameHexOrEmbarked)
            {
                actionCells.Add(moveCell);
            }

            if (range != SupplierRangeMode.Adjacent1Hex
                && range != SupplierRangeMode.Hybrid0Or1Hex)
            {
                continue;
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map.GetTile(targetCell) != null)
                    actionCells.Add(targetCell);
            }
        }

        return actionCells;
    }

    private static SupplierRangeMode ResolveServiceRange(UnitReachRequest request)
    {
        if (request.RangeOverride.HasValue)
            return request.RangeOverride.Value;

        if (!request.Unit.TryGetUnitData(out UnitData data) || data == null)
            return SupplierRangeMode.SameHexOrEmbarked;

        return request.Intent == ReachIntent.Transfer
            ? data.collectionRange
            : data.serviceRange;
    }

    // ------------------------------------------------------------------
    // Intencao: fusao e embarque — mesma forma, sensores diferentes.
    //
    // O alvo NAO fica a "+1 gratis" do movimento: a unidade precisa conservar
    // MP suficiente para pagar o custo oficial de entrar na celula ocupada.
    // Nao existe hard-code de "orcamento - 1": em terreno de custo 2 sobra 2.
    // ------------------------------------------------------------------

    private delegate bool EntryCostResolver(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int targetCell,
        out int enterCost);

    private static HashSet<Vector3Int> ExpandByEntryCost(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> movementCells,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        int budget,
        EntryCostResolver resolveEnterCost)
    {
        var actionCells = new HashSet<Vector3Int>();
        int totalMovement = Mathf.Max(0, budget);
        var neighbors = new List<Vector3Int>(6);

        foreach (Vector3Int moveCell in movementCells)
        {
            int spent = ResolveSpentMovement(
                request, map, moveCell, paths, costByCell);
            int remaining = Mathf.Max(0, totalMovement - spent);
            if (remaining <= 0)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map.GetTile(targetCell) == null
                    || actionCells.Contains(targetCell))
                {
                    continue;
                }

                if (!resolveEnterCost(request, map, targetCell, out int enterCost))
                    continue;
                if (remaining >= Mathf.Max(1, enterCost))
                    actionCells.Add(targetCell);
            }
        }

        return actionCells;
    }

    private static int ResolveSpentMovement(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int moveCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell)
    {
        if (costByCell.TryGetValue(moveCell, out int knownCost))
            return Mathf.Max(0, knownCost);

        if (paths.TryGetValue(moveCell, out List<Vector3Int> path)
            && path != null
            && path.Count > 0)
        {
            return Mathf.Max(
                0,
                UnitMovementPathRules.CalculateAutonomyCostForPath(
                    map,
                    request.Unit,
                    path,
                    request.TerrainDatabase,
                    applyOperationalAutonomyModifier: false));
        }

        return 0;
    }

    private static bool ResolveFusionEnterCost(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int targetCell,
        out int enterCost)
    {
        return PodeFundirSensor.TryResolveMergeEnterCost(
            map,
            request.TerrainDatabase,
            request.Unit,
            targetCell,
            out enterCost,
            out _);
    }

    private static bool ResolveEmbarkEnterCost(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int targetCell,
        out int enterCost)
    {
        // O passageiro e quem paga a entrada. Sem passageiro explicito, a
        // propria unidade do pedido e o passageiro: o envelope responde "onde
        // esta unidade consegue embarcar", nao "onde este transportador recebe".
        UnitManager passenger = request.EmbarkPassenger != null
            ? request.EmbarkPassenger
            : request.Unit;

        return PodeEmbarcarSensor.TryGetEmbarkCostAtCell(
            map,
            request.TerrainDatabase,
            passenger,
            targetCell,
            out enterCost,
            out _);
    }

    // ------------------------------------------------------------------
    // Filtro de dominio de operacao.
    //
    // NOTA DE PARIDADE: este filtro vivia duplicado na janela de Editor e no
    // panel helper, e nao existia no caminho da IA — que valida dominio por
    // candidato, via PodeSuprirSensor, depois de consultar o envelope. Manter
    // o filtro opcional preserva os dois comportamentos durante a unificacao.
    // Ligar para a IA muda o conjunto de candidatos e e decisao de jogabilidade,
    // nao de arquitetura.
    // ------------------------------------------------------------------

    private static void ApplyOperationDomainFilter(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> actionCells)
    {
        if (actionCells.Count == 0
            || !request.Unit.TryGetUnitData(out UnitData supplierData)
            || supplierData == null)
        {
            return;
        }

        UnitManager unit = request.Unit;
        bool airborneSupplier =
            unit.GetDomain() == Domain.Air && !unit.IsAircraftGrounded;

        actionCells.RemoveWhere(cell =>
        {
            Domain operationDomain;
            HeightLevel operationHeight;
            if (airborneSupplier)
            {
                operationDomain = unit.GetDomain();
                operationHeight = unit.GetHeightLevel();
            }
            else if (!LayerTransitionRules.TryResolvePrimaryLayerAtCell(
                         map,
                         request.TerrainDatabase,
                         cell,
                         out operationDomain,
                         out operationHeight,
                         out _))
            {
                return true;
            }

            return !PodeSuprirSensor.SupportsOperationDomain(
                supplierData,
                operationDomain,
                operationHeight);
        });
    }

    // ------------------------------------------------------------------
    // Intencao: combate (PodeMirarSensor). Preserva cache e perfis.
    // ------------------------------------------------------------------

    internal static UnitThreatEnvelope BuildCombat(
        UnitReachRequest request,
        Tilemap map,
        out bool cacheHit)
    {
        cacheHit = false;
        UnitManager unit = request.Unit;

        CombatProfile profile = ResolveCombatProfile(unit);
        if (profile == CombatProfile.None)
            return null;

        int movementSteps = request.MovementBudget > 0
            ? request.MovementBudget
            : request.MovementMode == UnitThreatEnvelopeMovement.CurrentTurn
                ? Mathf.Max(0, unit.RemainingMovementPoints)
                : Mathf.Max(0, unit.MaxMovementPoints);

        long cacheIndex = ((long)ResolveUnitIndex(unit) << 2) | (uint)request.MovementMode;
        int keyHash = BuildKeyHash(
            unit, map, movementSteps,
            request.EnableLdt, request.EnableLos, request.EnableSpotter);
        if (Cache.TryGetValue(cacheIndex, out CacheEntry existing)
            && existing != null
            && existing.KeyHash == keyHash
            && existing.Envelope != null)
        {
            cacheHit = true;
            return existing.Envelope;
        }

        bool includeStatic = profile == CombatProfile.DistanceStatic
                             || profile == CombatProfile.Hybrid;
        bool includeMovement = profile == CombatProfile.Movement
                               || profile == CombatProfile.Hybrid;
        var staticThreat = new HashSet<Vector3Int>();
        var mobileThreat = new HashSet<Vector3Int>();
        var movementCells = new HashSet<Vector3Int>();
        var paths = new Dictionary<Vector3Int, List<Vector3Int>>();

        if (includeStatic)
        {
            PodeMirarSensor.CollectValidFireCellsFromOrigin(
                unit, map, request.TerrainDatabase, SensorMovementMode.MoveuParado,
                unit.CurrentCellPosition, staticThreat, request.DpqAirHeightConfig,
                request.EnableLdt, request.EnableLos, request.EnableSpotter);
            staticThreat.RemoveWhere(cell => map.GetTile(cell) == null);
        }

        if (includeMovement)
        {
            if (!AIActionReachCoordinator.UsesCubicSectorReach(unit))
            {
                paths = UnitMovementPathRules.CalcularCaminhosValidos(
                    map, unit, movementSteps, request.TerrainDatabase);
                foreach (Vector3Int rawCell in paths.Keys)
                {
                    Vector3Int cell = rawCell;
                    cell.z = 0;
                    if (map.GetTile(cell) != null)
                        movementCells.Add(cell);
                }
            }
            else
            {
                CollectCubicMovementCells(
                    map, unit.CurrentCellPosition, movementSteps, movementCells);
            }

            Vector3Int origin = unit.CurrentCellPosition;
            origin.z = 0;
            if (map.GetTile(origin) != null)
                movementCells.Add(origin);

            var localThreat = new HashSet<Vector3Int>();
            foreach (Vector3Int moveCell in movementCells)
            {
                // Nao pular celulas de movimento que caiam dentro do staticThreat: uma
                // coisa e a celula estar no alcance parado, outra e tudo que se atinge
                // DE LA ja estar coberto. Numa hibrida (min 1 / max 2) o tiro pos-movimento
                // colapsa para alcance 1, entao a partir de uma celula na borda do alcance
                // parado ela alcanca alvos que o tiro parado nao alcanca. Pular aqui abria
                // um buraco no envelope e o CanAct barrava o ataque antes do sensor:
                // a artilheira combatente parava ao lado do alvo sem atirar.
                localThreat.Clear();
                PodeMirarSensor.CollectValidFireCellsFromOrigin(
                    unit, map, request.TerrainDatabase, SensorMovementMode.MoveuAndando,
                    moveCell, localThreat, request.DpqAirHeightConfig,
                    request.EnableLdt, request.EnableLos, request.EnableSpotter);
                foreach (Vector3Int rawTarget in localThreat)
                {
                    Vector3Int target = rawTarget;
                    target.z = 0;
                    if (map.GetTile(target) != null)
                        mobileThreat.Add(target);
                }
            }
        }

        var attackable = new HashSet<Vector3Int>(movementCells);
        attackable.UnionWith(staticThreat);
        attackable.UnionWith(mobileThreat);

        var lineCells = new HashSet<Vector3Int>(staticThreat);
        lineCells.UnionWith(mobileThreat);
        lineCells.ExceptWith(movementCells);

        var envelope = new UnitThreatEnvelope(
            ReachIntent.Combat,
            request.Band,
            paths,
            new Dictionary<Vector3Int, int>(),
            movementCells,
            attackable,
            lineCells);
        Cache[cacheIndex] = new CacheEntry { KeyHash = keyHash, Envelope = envelope };
        return envelope;
    }

    private static void CollectCubicMovementCells(
        Tilemap map,
        Vector3Int origin,
        int radius,
        HashSet<Vector3Int> destination)
    {
        if (map == null || destination == null)
            return;
        origin.z = 0;
        BoundsInt bounds = map.cellBounds;
        int minX = Mathf.Max(bounds.xMin, origin.x - radius * 2);
        int maxX = Mathf.Min(bounds.xMax - 1, origin.x + radius * 2);
        int minY = Mathf.Max(bounds.yMin, origin.y - radius);
        int maxY = Mathf.Min(bounds.yMax - 1, origin.y + radius);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (map.GetTile(cell) == null
                    || AIActionReachCoordinator.CubicDistance(origin, cell) > radius)
                    continue;
                destination.Add(cell);
            }
        }
    }

    private static CombatProfile ResolveCombatProfile(UnitManager unit)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons =
            unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null || weapons.Count == 0)
            return CombatProfile.None;

        bool hasOne = false;
        bool hasLong = false;
        bool hasHybrid = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    SensorMovementMode.MoveuParado,
                    requireAmmo: false,
                    out int min,
                    out int max))
                continue;
            hasOne |= min == 1 && max == 1;
            hasLong |= max > 1;
            hasHybrid |= min == 1 && max > 1;
        }

        if (hasHybrid || (hasOne && hasLong)) return CombatProfile.Hybrid;
        if (hasLong) return CombatProfile.DistanceStatic;
        if (hasOne) return CombatProfile.Movement;
        return CombatProfile.None;
    }

    private static int ResolveUnitIndex(UnitManager unit)
    {
        return unit.InstanceId > 0 ? unit.InstanceId : unit.GetEntityId().GetHashCode();
    }

    private static int BuildKeyHash(
        UnitManager unit,
        Tilemap map,
        int movementSteps,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter)
    {
        unchecked
        {
            int hash = 17;
            Vector3Int cell = unit.CurrentCellPosition;
            hash = hash * 31 + cell.x;
            hash = hash * 31 + cell.y;
            hash = hash * 31 + movementSteps;
            hash = hash * 31 + unit.CurrentFuel;
            hash = hash * 31 + (int)unit.GetDomain();
            hash = hash * 31 + (int)unit.GetHeightLevel();
            hash = hash * 31 + (unit.IsAircraftGrounded ? 1 : 0);
            hash = hash * 31 + map.GetEntityId().GetHashCode();
            hash = hash * 31 + ThreatRevisionTracker.GlobalBoardRevision;
            hash = hash * 31 + ThreatRevisionTracker.GetSlotObserverRevision(
                PlayerSlotId.FromIndex(unit.SlotIndex));
            hash = hash * 31 + ThreatRevisionTracker.MatchFlagsHash;
            hash = hash * 31 + (enableLdt ? 1 : 0);
            hash = hash * 31 + (enableLos ? 1 : 0);
            hash = hash * 31 + (enableSpotter ? 1 : 0);

            IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
            int count = weapons != null ? weapons.Count : 0;
            hash = hash * 31 + count;
            for (int i = 0; i < count; i++)
            {
                UnitEmbarkedWeapon embarked = weapons[i];
                if (embarked == null)
                {
                    hash = hash * 31 + 7;
                    continue;
                }
                hash = hash * 31 + (embarked.weapon != null
                    ? embarked.weapon.GetEntityId().GetHashCode()
                    : 0);
                hash = hash * 31 + embarked.squadAmmunition;
                hash = hash * 31 + (int)embarked.selectedTrajectory;
            }
            return hash;
        }
    }
}
