using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PathManager : MonoBehaviour
{
    [Header("Committed Path Visual")]
    [SerializeField] private Material committedPathMaterial;
    [SerializeField] private bool committedPathUseTeamColor = true;
    [SerializeField] private Color committedPathColor = new Color(1f, 1f, 1f, 0.9f);
    [SerializeField] [Range(0.03f, 0.6f)] private float committedPathWidth = 0.18f;
    [SerializeField] private SortingLayerReference committedPathSortingLayer;
    [SerializeField, HideInInspector] private bool committedPathSortingLayerInitialized;
    [SerializeField] private int committedPathSortingOrder = 50;

    private LineRenderer committedPathRenderer;

    private void Awake()
    {
        EnsureDefaults();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDefaults();
    }
#endif

    public void DrawCommittedPath(List<Vector3Int> path, Tilemap tilemap, TeamId? teamId)
    {
        if (path == null || path.Count < 2 || tilemap == null)
        {
            ClearCommittedPath();
            return;
        }

        EnsureCommittedPathRenderer();
        if (committedPathRenderer == null)
            return;
        ApplyRendererSorting(tilemap);

        committedPathRenderer.startWidth = committedPathWidth;
        committedPathRenderer.endWidth = committedPathWidth;
        Color pathColor = committedPathUseTeamColor && teamId.HasValue
            ? TeamUtils.GetColor(teamId.Value)
            : committedPathColor;
        pathColor.a = committedPathColor.a;
        committedPathRenderer.startColor = pathColor;
        committedPathRenderer.endColor = pathColor;
        committedPathRenderer.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = tilemap.GetCellCenterWorld(path[i]);
            p.z = tilemap.transform.position.z;
            committedPathRenderer.SetPosition(i, p);
        }

        committedPathRenderer.enabled = true;
    }

    public void ClearCommittedPath()
    {
        if (committedPathRenderer == null)
            return;

        committedPathRenderer.positionCount = 0;
        committedPathRenderer.enabled = false;
    }

    private void EnsureCommittedPathRenderer()
    {
        if (committedPathRenderer != null)
            return;

        GameObject go = new GameObject("CommittedPathLine");
        go.transform.SetParent(transform, false);
        committedPathRenderer = go.AddComponent<LineRenderer>();
        committedPathRenderer.useWorldSpace = true;
        committedPathRenderer.textureMode = LineTextureMode.Stretch;
        committedPathRenderer.numCapVertices = 0;
        committedPathRenderer.numCornerVertices = 0;
        committedPathRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        committedPathRenderer.receiveShadows = false;
        committedPathRenderer.material = committedPathMaterial != null ? committedPathMaterial : new Material(Shader.Find("Sprites/Default"));
        committedPathRenderer.enabled = false;
    }

    private void ApplyRendererSorting(Tilemap tilemap)
    {
        int configuredLayerId = committedPathSortingLayer.Id;
        if (configuredLayerId != 0)
        {
            committedPathRenderer.sortingLayerID = configuredLayerId;
            committedPathRenderer.sortingOrder = committedPathSortingOrder;
            return;
        }

        TilemapRenderer tilemapRenderer = tilemap != null ? tilemap.GetComponent<TilemapRenderer>() : null;
        if (tilemapRenderer != null)
        {
            committedPathRenderer.sortingLayerID = tilemapRenderer.sortingLayerID;
            committedPathRenderer.sortingOrder = tilemapRenderer.sortingOrder + Mathf.Max(1, committedPathSortingOrder);
            return;
        }

        committedPathRenderer.sortingOrder = committedPathSortingOrder;
    }

    private void EnsureDefaults()
    {
        if (!committedPathSortingLayerInitialized)
        {
            committedPathSortingLayer = SortingLayerReference.FromName("Constru\u00E7\u00F5es");
            committedPathSortingLayerInitialized = true;
        }
    }
}
