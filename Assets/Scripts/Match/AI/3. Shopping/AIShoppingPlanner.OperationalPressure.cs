using System.Collections.Generic;
using UnityEngine;

public partial class AIShoppingPlanner
{
    public sealed class AxisTransportPressureInspection
    {
        public int Eixo;
        public bool IsInvasionAxis;
        public ConstructionSector Front;
        public ConstructionSector Rally;
        public int Conquered;
        public int Total;
        public float PartialFrontProgress;
        public float Advance;
        public float Progress;
        public float Depth;
        public int AssignedTransports;
        public int DesiredTransports;
        public int AssignedUnits;
        public float Score;
        // O alvo do eixo esta na NOSSA terra? Sem ligacao por terra o APC nao opera ali.
        public bool LandRoute = true;
    }

    public sealed class OperationalPressureInspection
    {
        public readonly List<AxisTransportPressureInspection> Axes =
            new List<AxisTransportPressureInspection>();
        public float Transport;
        public int DesiredTransports;
        public int ActiveTransports;
        // A falta e medida em APC atribuido ao eixo; a frota (ActiveTransports) inclui Chinook e
        // transporte sem eixo, e "cobertura=3/1" comparava duas grandezas diferentes.
        public int AssignedGroundTransports;
        public int TransportGap;
        public float Logistics;
        public float CurrentRepair;
        public float RememberedRepair;
        public float Preventive;
        public int CurrentRepairUnits;
        public int RememberedRepairUnits;
        public int EliteUnitsUnderRepair;
        public int ActiveLogistics;
        public int DesiredLogistics;
        public int LogisticsGap;
        // Fila de carona (PressaoCaronaService): capturador do plano ilhado contra banco aereo livre.
        public PressaoCaronaLeitura RideQueue;
        public int RideSeatsPerAirBus;
        public int RideAirBusesShort;
        // Preenchidos ao virar demanda: o que as operacoes ja pediam e o piso aplicado.
        public int RideAirPlanDemand;
        public int RideAirFloor;
    }

    public static OperationalPressureInspection InspectOperationalPressure(
        AIWorldSnapshot snapshot)
        => BuildOperationalPressure(snapshot);

    private static OperationalPressureInspection BuildOperationalPressure(
        AIWorldSnapshot snapshot)
    {
        var result = new OperationalPressureInspection();
        if (snapshot == null)
            return result;
        BuildTransportOperationalPressure(snapshot, result);
        BuildLogisticsOperationalPressure(snapshot, result);
        BuildRideQueuePressure(snapshot, result);
        return result;
    }

    // Teto da fila de carona: no maximo +N onibus aereos ACIMA do que as operacoes ja pedem
    // por turno. 15 ilhados contra 4 bancos dariam ~6 Chinooks de uma vez; sem teto a compra
    // trocava o vicio da AAA pelo do Chinook.
    public const int RideQueueMaxExtraAirBuses = 2;

    private static void BuildRideQueuePressure(
        AIWorldSnapshot snapshot,
        OperationalPressureInspection result)
    {
        if (AIController.Instance == null)
            return;
        result.RideQueue = PressaoCaronaService.Read(AIController.Instance, snapshot.AISlotIndex);
        result.RideSeatsPerAirBus = ResolveCheapestAirTransportSeats(snapshot);
        int shortage = result.RideQueue.StrandedAirSeatShortage;
        result.RideAirBusesShort = shortage > 0 && result.RideSeatsPerAirBus > 0
            ? Mathf.CeilToInt(shortage / (float)result.RideSeatsPerAirBus)
            : 0;
    }

    // Bancos do transporte aereo MAIS BARATO que algum produtor nosso oferta (o que a compra
    // compraria). Zero = nao ha onibus aereo na oferta, e a fila nao vira demanda.
    private static int ResolveCheapestAirTransportSeats(AIWorldSnapshot snapshot)
    {
        if (snapshot?.MyBuildings == null)
            return 0;
        UnitData cheapest = null;
        foreach (ConstructionManager building in snapshot.MyBuildings)
        {
            if (building == null || !building.CanProduceUnitsForSlot(snapshot.AISlotIndex)
                || building.OfferedUnits == null)
                continue;
            foreach (UnitData unit in building.OfferedUnits)
                if (unit != null && unit.isTransporter && unit.domain == Domain.Air
                    && unit.transportSlots != null && unit.transportSlots.Count > 0
                    && (cheapest == null || unit.cost < cheapest.cost))
                    cheapest = unit;
        }
        if (cheapest == null)
            return 0;
        int seats = 0;
        foreach (var slot in cheapest.transportSlots)
            seats += Mathf.Max(1, slot.capacity);
        return seats;
    }

    private static void BuildTransportOperationalPressure(
        AIWorldSnapshot snapshot,
        OperationalPressureInspection result)
    {
        InvasionAxisMap map = AIController.Instance != null
            ? AIController.Instance.CurrentAxisMap
            : null;
        if (map == null || map.Team != snapshot.AITeam)
            map = InvasionAxisMap.Build(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (map == null)
            return;

        // O eixo de invasão é o eixo agregado da campanha: todos os nós regulares já
        // conquistados/convertidos formam o caminho até o HQ rival, que é o último nó.
        int campaignRegularTotal = 0;
        int campaignRegularConquered = 0;
        float campaignRegularAdvance = 0f;
        foreach (InvasionAxisMap.Axis regularAxis in map.Axes)
        {
            if (regularAxis == null || regularAxis.IsInvasionAxis)
                continue;
            int regularTotal = regularAxis.Corridor.Count + 1;
            int regularConquered = regularAxis.Complete
                ? regularTotal
                : Mathf.Clamp(regularAxis.FrontIndex, 0, regularTotal);
            float regularPartial = regularAxis.Complete
                ? 0f
                : GetAxisFrontCaptureProgress(regularAxis.FrontSector, snapshot.AITeam);
            campaignRegularTotal += regularTotal;
            campaignRegularConquered += regularConquered;
            campaignRegularAdvance += Mathf.Clamp(regularConquered + regularPartial, 0f, regularTotal);
        }

        // A pergunta "isto e nossa terra?" e a mesma do counter e da invasao (ilha do QG, ou o
        // alcance terrestre a partir dele). Sem dado responde sim: falha conservadora.
        OwnLandProbe ownLand = BuildOwnLandProbe(snapshot);

        foreach (InvasionAxisMap.Axis axis in map.Axes)
        {
            int total = axis.Corridor.Count + 1;
            int conquered = axis.Complete ? total : Mathf.Clamp(axis.FrontIndex, 0, total);
            float partialFrontProgress = axis.Complete
                ? 0f
                : GetAxisFrontCaptureProgress(axis.FrontSector, snapshot.AITeam);
            float advance = Mathf.Clamp(conquered + partialFrontProgress, 0f, total);
            float progress = total > 0 ? advance / total : 0f;
            if (axis.IsInvasionAxis)
            {
                // O HQ rival vale o último nó. Antes de tocá-lo, o eixo 4 já reflete toda a
                // campanha conquistada nos eixos 1..3; 100% continua reservado à queda do HQ.
                float hqProgress = GetAxisFrontCaptureProgress(axis.FrontSector, snapshot.AITeam);
                total = campaignRegularTotal + 1;
                conquered = campaignRegularConquered;
                partialFrontProgress = hqProgress;
                advance = Mathf.Clamp(campaignRegularAdvance + hqProgress, 0f, total);
                progress = total > 0 ? advance / total : 0f;
            }
            ConstructionSector target = map.GetTransportTargetSector(axis.EixoIndex);
            float depth = 0f;
            Vector3Int? targetCell = null;
            if (axis.IsInvasionAxis)
            {
                // A base inimiga não está em TryGetSectorInfo e não tem dist-de-HQ confiável:
                // profundidade pela GEOMETRIA do eixo (HQ -> célula da base). Mesma base do builder.
                depth = SectorManager.HexDistance(axis.HqCell, axis.RallyCell);
                targetCell = axis.RallyCell;
            }
            else if (target != ConstructionSector.None
                && SectorManager.TryGetSectorInfo(target, out SectorManager.SectorInfo info))
            {
                depth = info.GetDistanceToHQ(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
                targetCell = info.RepresentativeCell;
            }
            // APC so pede eixo cujo alvo esta na nossa terra. Antes, eixo com travessia naval
            // pedia APC pela profundidade (AI Step: "eixos=4 cobertura=3/1", APC $4200 numa ilha
            // sem ligacao). A carona aerea desses alvos vem das operacoes (AirliftCapture).
            bool landRoute = ownLand == null || !targetCell.HasValue || ownLand.Contains(targetCell.Value);

            int assigned = CountAxisGroundTransports(snapshot, axis.EixoIndex);
            int desired;
            float score;
            if (axis.IsInvasionAxis)
            {
                // Eixo de invasão: demanda escalada com a profundidade até o QG inimigo, NÃO gated
                // por advance — o transporte é o que ENABLES o assalto (leva a massa do HQ). Mesma
                // fórmula do builder real de demanda (AITacticalAnalyzer).
                desired = snapshot.IsInvading
                    ? AITacticalAnalyzer.ComputeInvasionTransportDesired(depth)
                    : 0;
                float depthPressureInv = Mathf.Clamp01((depth - 4f) / 8f);
                score = desired > 0 ? 1.5f + depthPressureInv * 1.5f : 0f;
            }
            else
            {
                bool activeFront = !axis.Complete && target != ConstructionSector.None;
                // Eixo apenas aberto no mapa ainda nao cria demanda. Transporte operacional
                // aparece quando a frente realmente avancou pelo corredor; caso contrario,
                // tres eixos potenciais geravam artificialmente Transportador x3 no inicio.
                bool hasRealAdvance = advance > 0.001f;
                // Divisão com o Ar Transportador (mesma regra do builder real,
                // ComputeGroundTransportNeed): APC só depois do nó INICIAL conquistado
                // (FrontIndex >= 1) — captura parcial do 1º nó ainda não libera.
                bool initialNodeConquered = axis.FrontIndex >= 1;
                desired = activeFront && hasRealAdvance && initialNodeConquered
                    && depth >= 7f ? 1 : 0;
                float depthPressure = Mathf.Clamp01((depth - 4f) / 8f);
                score = activeFront && hasRealAdvance && initialNodeConquered
                    ? progress * 1.5f + depthPressure * 1.5f
                    : 0f;
            }

            if (!landRoute)
            {
                desired = 0;
                score = 0f;
            }

            result.Axes.Add(new AxisTransportPressureInspection
            {
                LandRoute = landRoute,
                Eixo = axis.EixoIndex,
                IsInvasionAxis = axis.IsInvasionAxis,
                Front = axis.FrontSector,
                Rally = axis.RallySector,
                Conquered = conquered,
                Total = total,
                PartialFrontProgress = partialFrontProgress,
                Advance = advance,
                Progress = progress,
                Depth = depth,
                AssignedTransports = assigned,
                DesiredTransports = desired,
                AssignedUnits = CountAxisUnits(snapshot, axis.EixoIndex),
                Score = score,
            });
            result.Transport += score;
            result.DesiredTransports += desired;
            result.AssignedGroundTransports += assigned;
            result.TransportGap += Mathf.Max(0, desired - assigned);
        }

        result.ActiveTransports = CountCompositionRole(snapshot, UnitRole.Transportador);
    }

    private static float GetAxisFrontCaptureProgress(
        ConstructionSector front,
        TeamId team)
    {
        // Base-inclusive: a base inimiga (frente do eixo de invasão) vive em GetAllBaseInfos, não
        // em TryGetSectorInfo — senão o avanço da captura da base (HQ + fábricas) ficava sempre 0%.
        if (front == ConstructionSector.None
            || !(SectorManager.TryGetSectorInfo(front, out SectorManager.SectorInfo info)
                 || SectorManager.TryGetBaseInfo(front, out info))
            || info == null || info.Constructions == null)
            return 0f;

        int totalMax = 0;
        float convertedPoints = 0f;
        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            if (construction == null || construction.CapturePointsMax <= 0)
                continue;

            int max = construction.CapturePointsMax;
            int current = Mathf.Clamp(construction.CurrentCapturePoints, 0, max);
            totalMax += max;
            convertedPoints += construction.OwnerTeam == team
                ? current
                : max - current;
        }

        return totalMax > 0
            ? Mathf.Clamp01(convertedPoints / totalMax)
            : 0f;
    }

    private static int CountAxisGroundTransports(AIWorldSnapshot snapshot, int eixo)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && !unit.IsUnderRepair
                && unit.AIEixo == eixo
                && unit.TryGetUnitData(out UnitData data)
                && data != null && data.domain == Domain.Land
                && UnitRoleCompatibility.IsOperationalTransporter(data))
                count++;
        return count;
    }

    private static int CountAxisUnits(AIWorldSnapshot snapshot, int eixo)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && unit.AIEixo == eixo)
                count++;
        return count;
    }

    private static void BuildLogisticsOperationalPressure(
        AIWorldSnapshot snapshot,
        OperationalPressureInspection result)
    {
        var currentRepairIds = new HashSet<int>();
        if (snapshot.MyUnits != null)
        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || !unit.IsUnderRepair
                || !unit.TryGetUnitData(out UnitData data)
                || data == null || data.domain == Domain.Air)
                continue;
            currentRepairIds.Add(unit.InstanceId);
            if (data.eliteLevel >= 1)
                result.EliteUnitsUnderRepair++;
            float hpMissing = data.maxHP > 0
                ? 1f - Mathf.Clamp01(unit.CurrentHP / (float)data.maxHP)
                : 0f;
            result.CurrentRepair += 0.75f + hpMissing
                + data.eliteLevel * 0.4f
                + Mathf.Clamp(data.cost / 20000f, 0f, 1f);
        }
        result.CurrentRepairUnits = currentRepairIds.Count;

        JogadasLog log = JogadasManager.Instance != null ? JogadasManager.Instance.log : null;
        var remembered = new HashSet<int>();
        var latestRepairTransitionResolved = new HashSet<int>();
        if (log?.jogadas != null)
        for (int i = log.jogadas.Count - 1; i >= 0; i--)
        {
            Jogada play = log.jogadas[i];
            if (play == null || play.team != (int)snapshot.AITeam
                || !play.hasRepairState || play.repairBefore == play.repairAfter
                || !latestRepairTransitionResolved.Add(play.uid))
                continue;
            int age = Mathf.Max(0, snapshot.TurnNumber - play.turno);
            if (age > 3 || play.repairBefore || !play.repairAfter
                || currentRepairIds.Contains(play.uid) || !remembered.Add(play.uid))
                continue;
            result.RememberedRepair += Mathf.Lerp(0.25f, 0.8f, 1f - age / 3f);
        }
        result.RememberedRepairUnits = remembered.Count;

        int preventiveCount = CountCriticalPreventiveGroundLogisticsDemand(snapshot);
        result.Preventive = preventiveCount * 0.5f;
        result.Logistics = result.CurrentRepair + result.RememberedRepair + result.Preventive;
        result.ActiveLogistics = CountActiveOperationalRole(snapshot, UnitRole.Logistica);
        result.DesiredLogistics = Mathf.Clamp(Mathf.CeilToInt(result.Logistics / 2.5f), 0, 3);
        // Elite ferido: garante ao menos 1 supridor desejado (mesmo com peso baixo), pra existir
        // uma demanda a ser priorizada — o supridor móvel conserta o elite no campo quando as bases
        // estão ocupadas produzindo (doutrina do enxame). O boost de PRIORIDADE é aplicado no
        // emissor da demanda; aqui só garantimos que o gap não seja zerado por arredondamento.
        if (result.EliteUnitsUnderRepair > 0)
            result.DesiredLogistics = Mathf.Max(result.DesiredLogistics, 1);
        result.LogisticsGap = Mathf.Max(0, result.DesiredLogistics - result.ActiveLogistics);
    }

    private static int CountActiveOperationalRole(AIWorldSnapshot snapshot, UnitRole role)
    {
        int count = 0;
        if (snapshot?.MyUnits == null)
            return 0;
        foreach (UnitManager unit in snapshot.MyUnits)
            if (unit != null && !unit.IsDead && !unit.IsUnderRepair
                && unit.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.CanSatisfy(data, role))
                count++;
        return count;
    }

    private static void AddOperationalPressureDemands(
        AIWorldSnapshot snapshot,
        List<AIShoppingDemand> demands,
        OperationalPressureInspection pressure)
    {
        if (snapshot == null || demands == null || pressure == null)
            return;

        // Dominio Land: a falta e medida em APC atribuido ao eixo. Um Chinook comprado por ela
        // nunca fecharia a conta, e a demanda voltaria no turno seguinte.
        if (pressure.TransportGap > 0)
        {
            AIShoppingDemand transport = NewRoleDemand(UnitRole.Transportador, pressure.TransportGap,
                pressure.Transport >= 2f ? 17 : 23,
                "operational-pressure",
                $"eixos={pressure.Axes.Count} pressão={pressure.Transport:F1} "
                + $"APC nos eixos={pressure.AssignedGroundTransports}/{pressure.DesiredTransports} "
                + $"(frota={pressure.ActiveTransports})", false);
            transport.Domain = Domain.Land;
            MergeRoleDemand(demands, transport, false);
        }

        // FILA DE CARONA: PISO, nao soma. As operacoes de airlift ja pedem Chinook por
        // objetivo (origem "operation"); a fila so sobe esse numero quando ha mais ilhados do
        // plano que bancos aereos livres, e no maximo +RideQueueMaxExtraAirBuses. O merge sem
        // somar (Max) faz o piso: demanda de operacao maior que a fila fica como esta.
        if (pressure.RideAirBusesShort > 0)
        {
            int planAir = 0;
            foreach (AIShoppingDemand d in demands)
                if (d != null && d.Count > 0 && d.Role == UnitRole.Transportador && d.Domain == Domain.Air)
                    planAir += d.Count;
            int floor = Mathf.Min(pressure.RideAirBusesShort, planAir + RideQueueMaxExtraAirBuses);
            pressure.RideAirPlanDemand = planAir;
            pressure.RideAirFloor = floor;
            if (floor > planAir)
            {
                PressaoCaronaLeitura q = pressure.RideQueue;
                AIShoppingDemand ride = NewRoleDemand(UnitRole.Transportador, floor, 15,
                    "fila-carona",
                    $"ilhados do plano={q.Count(PressaoCaronaCategoria.IlhadoComPlano)} "
                    + $"bancos aereos livres={q.AirSeatsFree}/{q.AirSeatsTotal} "
                    + $"faltam {pressure.RideAirBusesShort} onibus ({pressure.RideSeatsPerAirBus} bancos cada), "
                    + $"operacoes pediam {planAir}, piso {floor}", false);
                ride.Domain = Domain.Air;
                MergeRoleDemand(demands, ride, false);
            }
        }

        if (pressure.LogisticsGap > 0)
        {
            // BOOST DE ELITE FERIDO (doutrina do enxame): o elite é investimento caro e, com as
            // bases ocupadas produzindo (conscrição), não sobra prédio pra ele reparar — o supridor
            // móvel é a única forma de consertá-lo no campo pra voltar a lutar. Sobe a demanda de
            // logística acima do counter-pressure-elite (8/9) pra o supridor não perder a fila.
            // Fora da doutrina, mantém o comportamento validado (10 com repair alto, senão 18).
            bool eliteRepairBoost = pressure.EliteUnitsUnderRepair > 0
                && AIController.Instance != null && AIController.Instance.ConscriptionDoctrine;
            int logisticsPriority = eliteRepairBoost && AIShoppingPlanner.Instance != null
                ? AIShoppingPlanner.Instance.EliteRepairLogisticsPriority
                : pressure.CurrentRepairUnits >= 3 ? 10 : 18;
            EnsureRoleDemand(
                demands, UnitRole.Logistica, pressure.LogisticsGap,
                logisticsPriority,
                "operational-pressure",
                $"logística={pressure.Logistics:F1} repair={pressure.CurrentRepairUnits} "
                + $"elite_ferido={pressure.EliteUnitsUnderRepair}{(eliteRepairBoost ? " (BOOST)" : "")} "
                + $"memória={pressure.RememberedRepairUnits} cobertura={pressure.ActiveLogistics}/{pressure.DesiredLogistics}");
        }
    }
}
