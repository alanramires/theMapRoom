using System.Collections.Generic;
using UnityEngine;

public partial class AIShoppingPlanner
{
    public sealed class AxisTransportPressureInspection
    {
        public int Eixo;
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
        public float Score;
    }

    public sealed class OperationalPressureInspection
    {
        public readonly List<AxisTransportPressureInspection> Axes =
            new List<AxisTransportPressureInspection>();
        public float Transport;
        public int DesiredTransports;
        public int ActiveTransports;
        public int TransportGap;
        public float Logistics;
        public float CurrentRepair;
        public float RememberedRepair;
        public float Preventive;
        public int CurrentRepairUnits;
        public int RememberedRepairUnits;
        public int ActiveLogistics;
        public int DesiredLogistics;
        public int LogisticsGap;
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
        return result;
    }

    private static void BuildTransportOperationalPressure(
        AIWorldSnapshot snapshot,
        OperationalPressureInspection result)
    {
        InvasionAxisMap map = AIController.Instance != null
            ? AIController.Instance.CurrentAxisMap
            : null;
        if (map == null || map.Team != snapshot.AITeam)
            map = InvasionAxisMap.Build(snapshot.AITeam);
        if (map == null)
            return;

        foreach (InvasionAxisMap.Axis axis in map.Axes)
        {
            int total = axis.Corridor.Count + 1;
            int conquered = axis.Complete ? total : Mathf.Clamp(axis.FrontIndex, 0, total);
            float partialFrontProgress = axis.Complete
                ? 0f
                : GetAxisFrontCaptureProgress(axis.FrontSector, snapshot.AITeam);
            float advance = Mathf.Clamp(conquered + partialFrontProgress, 0f, total);
            float progress = total > 0 ? advance / total : 0f;
            ConstructionSector target = map.GetTransportTargetSector(axis.EixoIndex);
            float depth = 0f;
            if (axis.IsInvasionAxis)
                // A base inimiga não está em TryGetSectorInfo e não tem dist-de-HQ confiável:
                // profundidade pela GEOMETRIA do eixo (HQ -> célula da base). Mesma base do builder.
                depth = SectorManager.HexDistance(axis.HqCell, axis.RallyCell);
            else if (target != ConstructionSector.None
                && SectorManager.TryGetSectorInfo(target, out SectorManager.SectorInfo info))
                depth = info.GetDistanceToHQ(snapshot.AITeam);

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
                desired = activeFront && hasRealAdvance && depth >= 7f ? 1 : 0;
                float depthPressure = Mathf.Clamp01((depth - 4f) / 8f);
                score = activeFront && hasRealAdvance
                    ? progress * 1.5f + depthPressure * 1.5f
                    : 0f;
            }

            result.Axes.Add(new AxisTransportPressureInspection
            {
                Eixo = axis.EixoIndex,
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
                Score = score,
            });
            result.Transport += score;
            result.DesiredTransports += desired;
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

        if (pressure.TransportGap > 0)
            EnsureRoleDemand(
                demands, UnitRole.Transportador, pressure.TransportGap,
                pressure.Transport >= 2f ? 17 : 23,
                "operational-pressure",
                $"eixos={pressure.Axes.Count} pressão={pressure.Transport:F1} cobertura={pressure.ActiveTransports}/{pressure.DesiredTransports}");

        if (pressure.LogisticsGap > 0)
            EnsureRoleDemand(
                demands, UnitRole.Logistica, pressure.LogisticsGap,
                pressure.CurrentRepairUnits >= 3 ? 10 : 18,
                "operational-pressure",
                $"logística={pressure.Logistics:F1} repair={pressure.CurrentRepairUnits} memória={pressure.RememberedRepairUnits} cobertura={pressure.ActiveLogistics}/{pressure.DesiredLogistics}");
    }
}
