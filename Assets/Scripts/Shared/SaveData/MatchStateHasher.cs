using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Hash canonico do estado da partida — fundacao do anti-desync do multiplayer
// assincrono (o "hash do estado final" do pacote de turno, ver
// docs/ideias_futuras_multiplayer.md) e da validacao round-trip do save:
// salvar -> carregar -> salvar deve produzir o MESMO hash; divergencia = campo
// se perdendo no load (classe de bug real: o lock de camada do submarino nao
// era persistido e ninguem sabia).
//
// Requisito de canonicidade: dois estados identicos devem gerar bytes
// identicos. O JsonUtility serializa campos em ordem de declaracao
// (deterministico), mas as LISTAS chegam na ordem de iteracao da cena ou de
// dicionarios — por isso SortCanonical ordena por chaves estaveis antes de
// serializar. Campos volateis (savedAtUtcTicks) sao zerados durante o hash.
public static class MatchStateHasher
{
    private static readonly System.Collections.Generic.List<FogCellContributorSaveData> EmptyFogCells =
        new System.Collections.Generic.List<FogCellContributorSaveData>();
    private static readonly System.Collections.Generic.List<FogUnitVisibilitySaveData> EmptyFogUnits =
        new System.Collections.Generic.List<FogUnitVisibilitySaveData>();
    private static readonly System.Collections.Generic.List<FogSourceContributionSaveData> EmptyFogSources =
        new System.Collections.Generic.List<FogSourceContributionSaveData>();

    public static string ComputeHash(SaveGameData data)
    {
        string json = BuildCanonicalJson(data);
        if (string.IsNullOrEmpty(json))
            return string.Empty;

        using (SHA256 sha = SHA256.Create())
        {
            byte[] hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            StringBuilder builder = new StringBuilder(hashBytes.Length * 2);
            for (int i = 0; i < hashBytes.Length; i++)
                builder.Append(hashBytes[i].ToString("x2"));
            return builder.ToString();
        }
    }

    // JSON canonico usado pelo hash e pelo dump de diagnostico ("state dump").
    // Alem dos volateis (savedAtUtcTicks), exclui o estado DERIVADO: os caches
    // de fog sao RECOMPUTADOS no load (e, no multiplayer, cada cliente recomputa
    // os proprios) — hashea-los geraria divergencia falsa permanente. O hash
    // cobre apenas o estado autoritativo da partida.
    public static string BuildCanonicalJson(SaveGameData data)
    {
        if (data == null)
            return string.Empty;

        SortCanonical(data);

        long savedTicks = data.savedAtUtcTicks;
        int savedFogObserverSlot = data.fogObserverSlotIndex;
        int savedLegacyFogTeam = data.fogCacheTeamId;
        System.Collections.Generic.List<FogCellContributorSaveData> savedFogCells = data.fogVisibleContributorsByCell;
        System.Collections.Generic.List<FogUnitVisibilitySaveData> savedFogUnits = data.fogUnitVisibilityByCacheIndex;
        System.Collections.Generic.List<FogSourceContributionSaveData> savedFogSources = data.fogSourceContributions;
        int savedFogSourceCacheFormat = data.fogSourceCacheFormat;
        int savedFogSourceCacheConfigHash = data.fogSourceCacheConfigHash;

        data.savedAtUtcTicks = 0;
        data.fogObserverSlotIndex = int.MinValue;
        data.fogCacheTeamId = int.MinValue;
        data.fogVisibleContributorsByCell = EmptyFogCells;
        data.fogUnitVisibilityByCacheIndex = EmptyFogUnits;
        data.fogSourceContributions = EmptyFogSources;
        data.fogSourceCacheFormat = 0;
        data.fogSourceCacheConfigHash = 0;
        try
        {
            return JsonUtility.ToJson(data, false);
        }
        finally
        {
            data.savedAtUtcTicks = savedTicks;
            data.fogObserverSlotIndex = savedFogObserverSlot;
            data.fogCacheTeamId = savedLegacyFogTeam;
            data.fogVisibleContributorsByCell = savedFogCells;
            data.fogUnitVisibilityByCacheIndex = savedFogUnits;
            data.fogSourceContributions = savedFogSources;
            data.fogSourceCacheFormat = savedFogSourceCacheFormat;
            data.fogSourceCacheConfigHash = savedFogSourceCacheConfigHash;
        }
    }

    // Ordena as listas cuja ordem de origem nao e deterministica entre
    // maquinas/execucoes (iteracao da cena, dicionarios). Idempotente; muta o
    // DTO — chamado tambem no fluxo de save para o proprio arquivo persistido
    // sair canonico.
    //
    // Limitacao conhecida (v1): listas do planner/intel da AI vem de
    // dicionarios por time (poucos elementos, insercao estavel na pratica) e
    // ainda nao sao canonicalizadas — suficiente para round-trip na mesma
    // maquina; canonicalizar quando o hash cruzar maquinas.
    public static void SortCanonical(SaveGameData data)
    {
        if (data == null)
            return;

        data.units?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            int byInstance = a.instanceId.CompareTo(b.instanceId);
            if (byInstance != 0)
                return byInstance;
            int byCellY = a.cellY.CompareTo(b.cellY);
            return byCellY != 0 ? byCellY : a.cellX.CompareTo(b.cellX);
        });

        data.constructions?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            int byInstance = a.instanceId.CompareTo(b.instanceId);
            if (byInstance != 0)
                return byInstance;
            int byCellY = a.cellY.CompareTo(b.cellY);
            if (byCellY != 0)
                return byCellY;
            int byCellX = a.cellX.CompareTo(b.cellX);
            return byCellX != 0 ? byCellX : string.CompareOrdinal(a.constructionId ?? string.Empty, b.constructionId ?? string.Empty);
        });

        data.fogVisibleContributorsByCell?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            int byY = a.y.CompareTo(b.y);
            if (byY != 0)
                return byY;
            int byX = a.x.CompareTo(b.x);
            return byX != 0 ? byX : a.z.CompareTo(b.z);
        });

        data.fogUnitVisibilityByCacheIndex?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            return a.cacheIndex.CompareTo(b.cacheIndex);
        });

        if (data.fogSourceContributions != null)
        {
            for (int i = 0; i < data.fogSourceContributions.Count; i++)
            {
                FogSourceContributionSaveData source = data.fogSourceContributions[i];
                if (source == null)
                    continue;
                SortFogCells(source.geographicCells);
                SortFogCells(source.sensorCells);
            }

            data.fogSourceContributions.Sort((a, b) =>
            {
                if (a == null || b == null)
                    return (a == null ? 1 : 0) - (b == null ? 1 : 0);
                int byObserver = a.observerSlotIndex.CompareTo(b.observerSlotIndex);
                if (byObserver != 0)
                    return byObserver;
                int byType = a.sourceType.CompareTo(b.sourceType);
                return byType != 0 ? byType : a.sourceInstanceId.CompareTo(b.sourceInstanceId);
            });
        }

        data.rallyPoints?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            return a.id.CompareTo(b.id);
        });

        data.rallyAssignments?.Sort((a, b) =>
        {
            if (a == null || b == null)
                return (a == null ? 1 : 0) - (b == null ? 1 : 0);
            int byRally = a.rallyPointId.CompareTo(b.rallyPointId);
            return byRally != 0 ? byRally : a.unitId.CompareTo(b.unitId);
        });
    }

    private static void SortFogCells(System.Collections.Generic.List<Vector3Int> cells)
    {
        cells?.Sort((a, b) =>
        {
            int byY = a.y.CompareTo(b.y);
            if (byY != 0)
                return byY;
            int byX = a.x.CompareTo(b.x);
            return byX != 0 ? byX : a.z.CompareTo(b.z);
        });
    }
}
