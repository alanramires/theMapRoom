using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum RallyLocation { Core, Approach, Outside }

public sealed class RallyReadinessReport
{
    public string Status, Missing;
    public bool Hold, Breakthrough, Artillery, Force;
    public (float atual, float exigido) HoldValues, BreakthroughValues, ArtilleryValues, ForceValues;
}

public sealed class RallyFormationParticipant
{
    public UnitManager Unit;
    public Vector3Int Position;
    public RallyLocation Location;
    public Vector3Int? PlannedDestination, MissionDestination;
    public Vector3Int? Destination => PlannedDestination ?? MissionDestination;
    public RallyLocation DestinationLocation;
    public bool GoingToArea;
    public string Expected;
}

// Le plano e fatos publicados. Nao aloca unidades nem sugere acoes.
public sealed class RallyFormationReport
{
    public int Slot;
    public RallyArea Area;
    public SectorObjective Objective;
    public RallyReadinessReport Readiness;
    public bool ModelFallback;
    public readonly List<RallyFormationParticipant> Participants = new List<RallyFormationParticipant>();
    public static RallyLocation Locate(RallyArea area, Vector3Int cell)
    { cell.z = 0; return area.Core.Contains(cell) ? RallyLocation.Core : area.Approach.Contains(cell) ? RallyLocation.Approach : RallyLocation.Outside; }

    // Aeronaves nao seguem o recorte terrestre do modelo. Projeta as mesmas bandas
    // em distancia hexagonal; isto muda so a localizacao exibida, nao a prontidao.
    public static RallyLocation LocateParticipant(RallyArea area, Vector3Int cell, UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || !data.IsAircraft()) return Locate(area, cell);
        float coreRadius = 0f, approachRadius = 0f;
        Vector3Int pivot = area.Rally.CurrentCellPosition;
        foreach (Vector3Int c in area.Core) coreRadius = Mathf.Max(coreRadius, SectorManager.HexDistance(pivot, c));
        foreach (Vector3Int c in area.Approach) approachRadius = Mathf.Max(approachRadius, SectorManager.HexDistance(pivot, c));
        float distance = SectorManager.HexDistance(pivot, cell);
        return area.Core.Count > 0 && distance <= coreRadius ? RallyLocation.Core
            : area.Approach.Count > 0 && distance <= approachRadius ? RallyLocation.Approach : RallyLocation.Outside;
    }

    public static RallyFormationReport BuildForSlot(AIController controller, int slot, RallyArea area,
        IReadOnlyList<UnitManager> units, IReadOnlyDictionary<int, Vector3Int> moves, bool fallback = false)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(slot));
        SectorObjective objective = plan?.Objectives.Find(o => o != null && o.Sector == area.Rally.Sector
            && o.ObjectiveType == AIObjectiveType.RallyAssembly);
        RallyReadinessReport readiness = controller.ReadRallyFormationReadiness(area.Rally, slot, objective);
        return Build(slot, area, readiness, units, moves, fallback);
    }

    public static RallyFormationReport Build(int slot, RallyArea area, RallyReadinessReport readiness,
        IReadOnlyList<UnitManager> units, IReadOnlyDictionary<int, Vector3Int> moves, bool fallback = false)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(slot));
        SectorObjective objective = plan?.Objectives.Find(o => o != null && o.Sector == area.Rally.Sector
            && o.ObjectiveType == AIObjectiveType.RallyAssembly);
        var report = new RallyFormationReport { Slot = slot, Area = area, Objective = objective, Readiness = readiness, ModelFallback = fallback };
        var ids = new HashSet<int>();
        if (objective?.Slots != null) foreach (SlotNeed need in objective.Slots)
            if (need.Filled) ids.Add(need.AssignedUnitId);
        if (plan?.RogueUnitIds != null) ids.UnionWith(plan.RogueUnitIds);
        if (units != null) foreach (UnitManager unit in units)
        {
            if (unit == null || unit.IsDead || unit.SlotIndex != slot || !ids.Remove(unit.InstanceId)) continue;
            Vector3Int position = unit.IsEmbarked && unit.EmbarkedTransporter != null
                ? unit.EmbarkedTransporter.CurrentCellPosition : unit.CurrentCellPosition;
            var participant = new RallyFormationParticipant { Unit = unit, Position = position,
                Location = LocateParticipant(area, position, unit), Expected = ExpectedPosition(unit) };
            if (moves != null && moves.TryGetValue(unit.InstanceId, out Vector3Int move)) participant.PlannedDestination = move;
            if (unit.AIHasDesignatedMission) participant.MissionDestination = unit.AIDesignatedMissionTargetCell;
            if (participant.Destination.HasValue)
            {
                participant.DestinationLocation = LocateParticipant(area, participant.Destination.Value, unit);
                participant.GoingToArea = participant.Destination.Value != position
                    && participant.DestinationLocation != RallyLocation.Outside;
            }
            report.Participants.Add(participant);
        }
        report.Participants.Sort((a,b) => a.Unit.InstanceId.CompareTo(b.Unit.InstanceId));
        return report;
    }

    private static string ExpectedPosition(UnitManager unit)
    {
        if (!unit.TryGetUnitData(out UnitData data)) return "sem ficha";
        return UnitRoleCompatibility.ResolveForce(data) == UnitForce.Ruptura
            ? "vanguarda" : "retaguarda";
    }

    public string ToDisplayText()
    {
        var text = new StringBuilder();
        text.AppendLine($"Slot {Slot} | rally {Area.Rally.Sector} {Area.Rally.CurrentCellPosition}");
        text.AppendLine($"Modelo: {(ModelFallback ? "fallback - " : "")}{Area.Model?.displayName ?? "indisponivel"}");
        text.AppendLine($"Core {Area.Core.Count} | Approach {Area.Approach.Count} | {Area.Orientation}");
        if (!string.IsNullOrEmpty(Area.Error)) text.AppendLine("Geometria: " + Area.Error);
        text.AppendLine($"{Readiness.Status} | Missing: {Readiness.Missing}");
        text.AppendLine($"hold={Readiness.Hold} ruptura={Readiness.Breakthrough} artilharia={Readiness.Artillery} forca={Readiness.Force}");
        if (Objective?.Slots == null) text.AppendLine("Sem objetivo RallyAssembly publicado no plano.");
        else
        {
            var roles = new Dictionary<UnitRole, (int Filled, int Empty)>();
            foreach (SlotNeed need in Objective.Slots)
            { roles.TryGetValue(need.Role, out var count); if (need.Filled) count.Filled++; else count.Empty++; roles[need.Role] = count; }
            foreach (var role in roles) text.AppendLine($"{role.Key}: preenchidas {role.Value.Filled}, vazias {role.Value.Empty}");
        }
        foreach (RallyFormationParticipant p in Participants)
        {
            text.AppendLine($"\n{p.Unit.UnitDisplayName} #{p.Unit.InstanceId}: {p.Position} [{p.Location}]{(p.Unit.IsEmbarked ? " embarcado" : "")}");
            text.AppendLine($"  Decisao: {p.PlannedDestination?.ToString() ?? "nao registrada"}; missao: {p.MissionDestination?.ToString() ?? "nenhuma"}");
            text.AppendLine($"  Destino: {(p.Destination.HasValue ? p.DestinationLocation.ToString() : "nenhum")} | indo: {p.GoingToArea} | esperado: {p.Expected}");
        }
        return text.ToString();
    }
}
