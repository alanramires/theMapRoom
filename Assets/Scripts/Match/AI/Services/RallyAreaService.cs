using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class RallyArea
{
    public ConstructionManager Rally;
    public UnitData Model;
    public readonly HashSet<Vector3Int> Core = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> Approach = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> Vanguard = new HashSet<Vector3Int>();
    public readonly HashSet<Vector3Int> Rear = new HashSet<Vector3Int>();
    public string Orientation;
    public string Error;
}

// Geometria apenas. Nao escolhe destinos nem classifica unidades da formacao.
public static class RallyAreaService
{
    public static RallyArea Build(ConstructionManager rally, UnitData model, TeamId team, int slot,
        Tilemap map, TerrainDatabase terrain, IReadOnlyList<UnitManager> allies,
        IReadOnlyList<UnitManager> knownEnemies, Vector3Int? axisOrigin = null)
    {
        var area = new RallyArea { Rally = rally, Model = model };
        if (rally == null || model == null || map == null || terrain == null)
        { area.Error = "rally, modelo ou tabuleiro indisponivel"; return area; }
        Vector3Int pivot = rally.CurrentCellPosition; pivot.z = 0;
        foreach (ReachBand band in new[] { ReachBand.Tactical, ReachBand.Operational })
        {
            UnitReachEnvelope reach = UnitReachEnvelopeService.Build(new UnitReachRequest {
                ModelData = model, ModelTeam = team, ModelSlot = slot, BoardMap = map,
                TerrainDatabase = terrain, OriginOverride = pivot, Intent = ReachIntent.Mobility,
                Band = band, SubStep = ReachSubStep.Terrestre });
            if (reach == null) { area.Error = "modelo sem envelope de movimento"; return area; }
            (band == ReachBand.Tactical ? area.Core : area.Approach).UnionWith(reach.MovementCells);
        }
        var combatants = new List<Vector3Int>();
        var enemies = new List<Vector3Int>();
        if (allies != null) foreach (UnitManager unit in allies)
            if (unit != null && !unit.IsDead && !unit.IsEmbarked && unit.SlotIndex == slot
                && unit.TryGetUnitData(out UnitData data) && UnitRoleCompatibility.ParticipatesInBattle(data)
                && area.Approach.Contains(unit.CurrentCellPosition)) combatants.Add(unit.CurrentCellPosition);
        if (knownEnemies != null) foreach (UnitManager unit in knownEnemies)
            if (unit != null && !unit.IsDead && !unit.IsEmbarked) enemies.Add(unit.CurrentCellPosition);
        Vector2? forward = null;
        Vector2 center = HexCoordinates.GetCellCenterWorld(map, pivot);
        if (enemies.Count > 0)
        {
            Vector2 mass = Vector2.zero;
            foreach (Vector3Int cell in enemies) mass += (Vector2)HexCoordinates.GetCellCenterWorld(map, cell);
            forward = mass / enemies.Count - center; area.Orientation = "massa inimiga conhecida";
        }
        else if (axisOrigin.HasValue)
        { forward = center - (Vector2)HexCoordinates.GetCellCenterWorld(map, axisOrigin.Value); area.Orientation = "eixo (sem inimigo conhecido)"; }
        else area.Orientation = "sem referencia de eixo/inimigo";
        AIBacklineSettings settings = AIBacklineSettings.Default;
        settings.CellToWorld = cell => HexCoordinates.GetCellCenterWorld(map, cell);
        AIBacklineResult line = AIBacklineAnalyzer.Analyze(combatants, enemies, pivot, settings,
            forwardOverride: forward);
        if (line.Success)
        {
            area.Vanguard.UnionWith(line.VanguardCells);
            area.Vanguard.IntersectWith(area.Approach);
            area.Rear.UnionWith(line.RearScoreMap.Keys);
            area.Rear.IntersectWith(area.Approach);
            area.Rear.ExceptWith(area.Vanguard);
        }
        else area.Error = line.Error;
        return area;
    }
}
