using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Game/Terrain/Terrain Database", fileName = "TerrainDatabase")]
public class TerrainDatabase : ScriptableObject
{
    [Tooltip("Lista manual dos terrenos que fazem parte do jogo.")]
    [SerializeField] private List<TerrainTypeData> terrains = new List<TerrainTypeData>();

    private readonly Dictionary<string, TerrainTypeData> byId = new Dictionary<string, TerrainTypeData>();
    private readonly Dictionary<TileBase, TerrainTypeData> byPaletteTile = new Dictionary<TileBase, TerrainTypeData>();

    public IReadOnlyList<TerrainTypeData> Terrains => terrains;

    private void OnEnable()
    {
        RebuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif

    public bool TryGetById(string id, out TerrainTypeData terrain)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            terrain = null;
            return false;
        }

        if (byId.Count == 0)
            RebuildLookup();

        return byId.TryGetValue(id.Trim(), out terrain);
    }

    public bool TryGetFirst(out TerrainTypeData terrain)
    {
        for (int i = 0; i < terrains.Count; i++)
        {
            if (terrains[i] != null)
            {
                terrain = terrains[i];
                return true;
            }
        }

        terrain = null;
        return false;
    }

    public bool TryGetByPaletteTile(TileBase tile, out TerrainTypeData terrain)
    {
        if (tile == null)
        {
            terrain = null;
            return false;
        }

        if (byPaletteTile.Count == 0)
            RebuildLookup();

        return byPaletteTile.TryGetValue(tile, out terrain);
    }

    private void RebuildLookup()
    {
        byId.Clear();
        byPaletteTile.Clear();

        for (int i = 0; i < terrains.Count; i++)
        {
            TerrainTypeData def = terrains[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id))
                continue;

            string key = def.id.Trim();
            if (byId.ContainsKey(key))
            {
                Debug.LogWarning($"[TerrainDatabase] ID duplicado '{key}' em TerrainTypeData. Mantendo o primeiro.");
                continue;
            }

            byId.Add(key, def);

            if (def.paletteTile == null)
                continue;

            if (byPaletteTile.ContainsKey(def.paletteTile))
            {
                Debug.LogWarning($"[TerrainDatabase] PaletteTile duplicado em TerrainTypeData ('{key}'). Mantendo o primeiro.");
                continue;
            }

            byPaletteTile.Add(def.paletteTile, def);
        }
    }
}
