using UnityEngine;
using UnityEngine.Tilemaps;

public static class ConstructionOccupancyRules
{
    // DE ONDE VEM A LISTA: em Play, do registro ConstructionManager.AllActive (entra no OnEnable,
    // sai no OnDisable — o mesmo conjunto que a busca na cena com FindObjectsInactive.Exclude).
    // Antes era FindObjectsByType a CADA chamada, alocando um array; e esta funcao roda por
    // celula em laco de IA (rota, encontro, filtro de mapa) — o filtro "fora da produtora"
    // sozinho custou ~13 s por decisao. Fora do Play (ferramentas de Editor) o registro nao e
    // confiavel e a cena e a fonte: ferramenta de editor tem que ver a cena. Registro vazio em
    // Play (janela de carga) tambem cai na busca: e raro e barato.
    public static ConstructionManager GetConstructionAtCell(Tilemap referenceTilemap, Vector3Int cell)
    {
        cell.z = 0;

        if (Application.isPlaying && ConstructionManager.AllActive.Count > 0)
        {
            var registry = ConstructionManager.AllActive;
            for (int i = 0; i < registry.Count; i++)
                if (Matches(registry[i], referenceTilemap, cell))
                    return registry[i];
            return null;
        }

        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
            if (Matches(constructions[i], referenceTilemap, cell))
                return constructions[i];

        return null;
    }

    private static bool Matches(ConstructionManager construction, Tilemap referenceTilemap, Vector3Int cell)
    {
        if (construction == null || !construction.gameObject.activeInHierarchy)
            return false;

        Vector3Int occupiedCell = construction.BoardTilemap == referenceTilemap
            ? construction.CurrentCellPosition
            : HexCoordinates.WorldToCell(referenceTilemap, construction.transform.position);

        occupiedCell.z = 0;
        return occupiedCell == cell && !construction.IsFakeBuilding;
    }
}
