using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly List<AxisDebugTrack> rallyDebugTracks = new List<AxisDebugTrack>();
    private readonly List<GameObject> rallyDebugOutlines = new List<GameObject>();
    private Coroutine rallyDebugAnimation;

    public void SetRallyDebugReports(IReadOnlyList<RallyFormationReport> reports)
    {
        if (rallyDebugAnimation != null) StopCoroutine(rallyDebugAnimation);
        rallyDebugAnimation = null;
        foreach (var track in rallyDebugTracks) foreach (var line in track.renderers)
            if (line != null) { line.gameObject.SetActive(false); Destroy(line.gameObject); }
        rallyDebugTracks.Clear();
        foreach (var outline in rallyDebugOutlines) if (outline != null) { outline.SetActive(false); Destroy(outline); }
        rallyDebugOutlines.Clear();
        if (reports == null) return;
        Tilemap map = terrainTilemap != null ? terrainTilemap : cursorController?.BoardTilemap;
        if (map == null) return;
        foreach (RallyFormationReport report in reports)
        {
            DrawRallyBand(map, report.Area.Rear, new Color(0.6f,0.3f,0.8f,0.55f), false);
            DrawRallyBand(map, report.Area.Vanguard, new Color(1f,0.25f,0.25f,0.65f), false);
            DrawRallyBand(map, report.Area.Approach, new Color(1f,0.65f,0.1f), true);
            DrawRallyBand(map, report.Area.Core, new Color(0.1f,0.35f,1f), true);
            DrawRallyBand(map, new HashSet<Vector3Int>{report.Area.Rally.CurrentCellPosition}, Color.white, false);
            foreach (RallyFormationParticipant participant in report.Participants)
            {
                if (!participant.Destination.HasValue || participant.Destination.Value == participant.Position) continue;
                var track = new AxisDebugTrack { Color = participant.DestinationLocation != RallyLocation.Outside ? Color.green : Color.gray };
                AddAxisDebugPoint(track, map, participant.Position);
                AddAxisDebugPoint(track, map, participant.Destination.Value);
                track.pathLength = ComputePathLength(track.pathPoints);
                if (track.pathLength > 0) rallyDebugTracks.Add(track);
            }
        }
        if (Application.isPlaying) rallyDebugAnimation = StartCoroutine(AnimateRallyFormation());
    }

    private IEnumerator AnimateRallyFormation()
    {
        while (true) { AnimateAxisDebugTracks(rallyDebugTracks); yield return null; }
    }

    private void DrawRallyBand(Tilemap map, HashSet<Vector3Int> cells, Color color, bool boundaryOnly)
    {
        var neighbors = new List<Vector3Int>(6);
        foreach (Vector3Int cell in cells)
        {
            UnitMovementPathRules.GetImmediateHexNeighbors(map, cell, neighbors);
            if (boundaryOnly && neighbors.TrueForAll(cells.Contains)) continue;
            Vector3 center = HexCoordinates.GetCellCenterWorld(map, cell);
            var directions = new List<Vector3>();
            foreach (Vector3Int neighbor in neighbors) directions.Add(HexCoordinates.GetCellCenterWorld(map, neighbor) - center);
            directions.Sort((a,b) => Mathf.Atan2(a.y,a.x).CompareTo(Mathf.Atan2(b.y,b.x)));
            var go = new GameObject("RallyDebugOutline"); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.loop = true;
            Material material = GetMirandoPreviewMaterial();
            if (material == null)
            {
                if (axisDebugFallbackMaterial == null) axisDebugFallbackMaterial = new Material(Shader.Find("Sprites/Default"));
                material = axisDebugFallbackMaterial;
            }
            line.sharedMaterial = material; line.sortingLayerID = GetMirandoPreviewSortingLayerId(); line.sortingOrder = GetMirandoPreviewSortingOrder();
            line.startColor = line.endColor = color; line.widthMultiplier = boundaryOnly ? 0.045f : 0.022f;
            line.positionCount = directions.Count;
            for (int i=0; i<directions.Count; i++) line.SetPosition(i, center + (directions[i]+directions[(i+1)%directions.Count])/3f);
            rallyDebugOutlines.Add(go);
        }
    }
}
