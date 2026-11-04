using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// =====================================================================================
// Debug: "show axis" / "show eixos". Desenha os eixos de invasao de UM slot como linhas
// tracejadas que ANDAM da base, pelos setores do corredor, ate o rally — a mesma mecanica
// das linhas de spotter do PodeMirar (trilha + segmentos que correm a polilinha), com o
// mesmo material, largura e camada de ordenacao.
//
// Apresentacao isolada: le o InvasionAxisMap e nada mais. Nao mexe no plano, nao move
// cursor/camera, nao chama sensores, nao revela FOW.
//
// Cores pedidas pelo autor: eixo 1 vermelho, eixo 2 laranja, eixo 3 grafite. O eixo
// sintetico de invasao (o 4o, QG -> QG inimigo) e qualquer eixo acima do 3 NAO sao
// desenhados: nao tem cor nomeada, e ferramenta de debug pinta so o que foi pedido.
// =====================================================================================
public partial class TurnStateManager
{
    private static readonly Color AxisDebugRed = new Color(0.92f, 0.16f, 0.16f, 0.95f);
    private static readonly Color AxisDebugOrange = new Color(1f, 0.56f, 0.08f, 0.95f);
    private static readonly Color AxisDebugGraphite = new Color(0.2f, 0.2f, 0.23f, 0.95f);

    private sealed class AxisDebugTrack
    {
        public int EixoIndex;
        public Color Color;
        public readonly List<LineRenderer> renderers = new List<LineRenderer>();
        public readonly List<Vector3> pathPoints = new List<Vector3>();
        public readonly List<Vector3> tempSegmentPoints = new List<Vector3>();
        public float pathLength;
        public float headDistance;
    }

    private readonly List<AxisDebugTrack> axisDebugTracks = new List<AxisDebugTrack>();
    private Material axisDebugFallbackMaterial;
    private Coroutine axisDebugRoutine;
    private bool axisDebugVisible;
    private int axisDebugRequestedSlot = -1;   // -1 = segue o slot ativo
    private int axisDebugDrawnSlot = int.MinValue;

    public bool AxisDebugVisible => axisDebugVisible;

    /// <summary>
    /// Liga/desliga o desenho dos eixos. slot &lt; 0 segue o slot ativo (redesenha quando a vez
    /// muda); slot &gt;= 0 fixa aquele slot. Devolve um resumo do que foi desenhado.
    /// </summary>
    public string SetAxisDebugVisible(bool visible, int slot = -1)
    {
        axisDebugVisible = visible;
        axisDebugRequestedSlot = slot;
        if (!visible)
        {
            if (axisDebugRoutine != null) StopCoroutine(axisDebugRoutine);
            axisDebugRoutine = null;
            ClearAxisDebugTracks();
            axisDebugDrawnSlot = int.MinValue;
            return "Eixos ocultos.";
        }

        string summary = RebuildAxisDebugTracks(ResolveAxisDebugSlot());
        if (axisDebugRoutine == null)
            axisDebugRoutine = StartCoroutine(AxisDebugLoop());
        return summary;
    }

    private int ResolveAxisDebugSlot()
    {
        if (axisDebugRequestedSlot >= 0) return axisDebugRequestedSlot;
        return matchController != null ? matchController.ActiveSlotId.Value : 0;
    }

    private IEnumerator AxisDebugLoop()
    {
        while (axisDebugVisible)
        {
            int slot = ResolveAxisDebugSlot();
            if (slot != axisDebugDrawnSlot)
                RebuildAxisDebugTracks(slot);
            AnimateAxisDebugTracks();
            yield return null;
        }
        axisDebugRoutine = null;
    }

    private string RebuildAxisDebugTracks(int slot)
    {
        ClearAxisDebugTracks();
        axisDebugDrawnSlot = slot;

        Tilemap map = terrainTilemap != null ? terrainTilemap : (cursorController != null ? cursorController.BoardTilemap : null);
        if (map == null || slot < 0)
            return "Eixos: sem tabuleiro.";

        InvasionAxisMap axisMap = InvasionAxisMap.Build(PlayerSlotId.FromIndex(slot), map);
        var drawn = new List<string>();
        var skipped = new List<string>();
        foreach (InvasionAxisMap.Axis axis in axisMap.Axes)
        {
            if (axis == null) continue;
            if (axis.IsInvasionAxis || !TryGetAxisDebugColor(axis.EixoIndex, out Color color))
            {
                skipped.Add(axis.EixoIndex + (axis.IsInvasionAxis ? " (invasao)" : ""));
                continue;
            }

            AxisDebugTrack track = new AxisDebugTrack { EixoIndex = axis.EixoIndex, Color = color };
            AddAxisDebugPoint(track, map, axis.HqCell);
            foreach (ConstructionSector sector in axis.Corridor)
                if (SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info) && info != null)
                    AddAxisDebugPoint(track, map, info.RepresentativeCell);
            AddAxisDebugPoint(track, map, axis.RallyCell);
            track.pathLength = ComputePathLength(track.pathPoints);
            if (track.pathLength <= 0.0001f) continue;

            axisDebugTracks.Add(track);
            var nomes = new List<string>();
            foreach (ConstructionSector sector in axis.Corridor) nomes.Add(sector.ToString());
            drawn.Add($"eixo {axis.EixoIndex}: base -> {string.Join(" -> ", nomes)} -> rally {axis.RallySector}");
        }

        string result = $"Eixos do slot {slot}: " + (drawn.Count > 0 ? string.Join(" | ", drawn) : "nenhum");
        if (skipped.Count > 0)
            result += $" (sem cor, nao desenhados: {string.Join(", ", skipped)})";
        return result;
    }

    private static bool TryGetAxisDebugColor(int eixoIndex, out Color color)
    {
        switch (eixoIndex)
        {
            case 1: color = AxisDebugRed; return true;
            case 2: color = AxisDebugOrange; return true;
            case 3: color = AxisDebugGraphite; return true;
            default: color = default; return false;
        }
    }

    private static void AddAxisDebugPoint(AxisDebugTrack track, Tilemap map, Vector3Int cell)
    {
        cell.z = 0;
        Vector3 point = HexCoordinates.GetCellCenterWorld(map, cell);
        int n = track.pathPoints.Count;
        if (n > 0)
        {
            point.z = track.pathPoints[0].z;
            if (Vector3.Distance(track.pathPoints[n - 1], point) <= 0.0001f) return;
        }
        track.pathPoints.Add(point);
    }

    // Mesma conta do spotter: cada segmento e um pedaco da polilinha que corre da base para o
    // rally e recomeca. A quantidade de segmentos cresce com o comprimento do eixo, para o
    // tracejado manter o espacamento num caminho longo.
    private void AnimateAxisDebugTracks()
    {
        float multiplier = GetMirandoSpotterPreviewMultiplier();
        float speed = Mathf.Max(0.2f, GetMirandoSpotterSegmentSpeed());
        float segmentLen = Mathf.Max(0.08f, GetMirandoPreviewSegmentLength() * multiplier);
        float width = Mathf.Max(0.02f, GetMirandoPreviewWidth() * multiplier);
        int baseQuantity = Mathf.Max(1, GetMirandoSpotterSegmentQuantities());

        foreach (AxisDebugTrack track in axisDebugTracks)
        {
            float cycleLen = track.pathLength + segmentLen;
            int quantity = Mathf.Max(baseQuantity, Mathf.CeilToInt(cycleLen / (segmentLen * 2.5f)));
            EnsureAxisDebugRenderers(track, quantity);

            track.headDistance += speed * Time.deltaTime;
            if (track.headDistance > cycleLen) track.headDistance -= cycleLen;

            float spacing = cycleLen / quantity;
            for (int i = 0; i < track.renderers.Count; i++)
            {
                LineRenderer renderer = track.renderers[i];
                if (renderer == null) continue;
                if (i >= quantity) { renderer.positionCount = 0; renderer.enabled = false; continue; }

                float head = track.headDistance - spacing * i;
                while (head < 0f) head += cycleLen;
                while (head > cycleLen) head -= cycleLen;
                float startDist = Mathf.Max(0f, head - segmentLen);
                float endDist = Mathf.Min(head, track.pathLength);
                if (endDist <= startDist + 0.0001f) { renderer.positionCount = 0; renderer.enabled = false; continue; }

                BuildPathSegmentPointsFrom(track.pathPoints, startDist, endDist, track.tempSegmentPoints);
                if (track.tempSegmentPoints.Count < 2) { renderer.positionCount = 0; renderer.enabled = false; continue; }

                renderer.startWidth = renderer.endWidth = width;
                renderer.startColor = renderer.endColor = track.Color;
                renderer.positionCount = track.tempSegmentPoints.Count;
                for (int p = 0; p < track.tempSegmentPoints.Count; p++)
                    renderer.SetPosition(p, track.tempSegmentPoints[p]);
                renderer.enabled = true;
            }
        }
    }

    private void EnsureAxisDebugRenderers(AxisDebugTrack track, int quantity)
    {
        while (track.renderers.Count < quantity)
        {
            var go = new GameObject($"AxisDebugDash_{track.EixoIndex}_{track.renderers.Count}");
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Material material = GetMirandoPreviewMaterial();
            if (material == null)
            {
                if (axisDebugFallbackMaterial == null)
                    axisDebugFallbackMaterial = new Material(Shader.Find("Sprites/Default"));
                material = axisDebugFallbackMaterial;
            }
            line.sharedMaterial = material;
            line.sortingLayerID = GetMirandoPreviewSortingLayerId();
            line.sortingOrder = GetMirandoPreviewSortingOrder();
            line.enabled = false;
            track.renderers.Add(line);
        }
    }

    private void ClearAxisDebugTracks()
    {
        foreach (AxisDebugTrack track in axisDebugTracks)
            foreach (LineRenderer line in track.renderers)
                if (line != null) Destroy(line.gameObject);
        axisDebugTracks.Clear();
    }
}
