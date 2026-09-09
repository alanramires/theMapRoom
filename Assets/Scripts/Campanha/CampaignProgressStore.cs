using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado do jogador entre batalhas. O mapa assado continua imutavel; este
/// snapshot do save guarda quem controla cada quadrante e o ultimo resultado.
///
/// O DONO E O SLOT, NUNCA A COR.
///
/// A cor de cada slot e escolhida no menu, uma vez por partida: hoje o jogador e
/// Amarelo, amanha e Vermelho. Gravar "este quadrante e do Amarelo" grava uma
/// fantasia, nao um dono — na outra configuracao a cor pode nao estar em campo, e
/// a pergunta que importa ("fui EU que tomei este?") deixa de ter resposta.
///
/// Gravando o slot, a pergunta continua respondivel para sempre, e a cor volta a
/// ser o que ela e: apresentacao, resolvida na hora de pintar por
/// <c>MatchController.GetTeamIdForSlot</c>.
/// </summary>
public static class CampaignProgressStore
{
    [Serializable]
    public sealed class CampaignProgressData
    {
        public int schemaVersion = 1;
        public string mundoId;
        public string campanhaId;
        public List<QuadrantOwnershipData> quadrantes = new List<QuadrantOwnershipData>();
    }

    [Serializable]
    public sealed class QuadrantOwnershipData
    {
        public string quadranteId;
        public int ownerSlotIndex = PlayerSlotId.InvalidValue;
        public int lastTurn;
        public string updatedAtUtc;
    }

    private static readonly Dictionary<string, CampaignProgressData> Cache =
        new Dictionary<string, CampaignProgressData>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Slot que controla o quadrante. Devolve false quando ninguem o tomou ainda —
    /// e "ninguem" nao e um slot neutro, e a ausencia de registro.
    /// </summary>
    public static bool TryGetOwner(
        string mundoId,
        string campanhaId,
        string quadranteId,
        out PlayerSlotId owner)
    {
        owner = PlayerSlotId.Invalid;
        if (!HasAddress(mundoId, campanhaId, quadranteId))
            return false;

        CampaignProgressData data = Load(mundoId, campanhaId);
        QuadrantOwnershipData quadrant = FindQuadrant(data, quadranteId);
        if (quadrant == null)
            return false;

        owner = PlayerSlotId.FromIndex(quadrant.ownerSlotIndex);
        return owner.IsValid;
    }

    public static bool RecordOwner(
        string mundoId,
        string campanhaId,
        string quadranteId,
        PlayerSlotId owner,
        int turn)
    {
        if (!HasAddress(mundoId, campanhaId, quadranteId) || !owner.IsValid)
            return false;

        CampaignProgressData data = Load(mundoId, campanhaId);
        QuadrantOwnershipData quadrant = FindQuadrant(data, quadranteId);
        if (quadrant == null)
        {
            quadrant = new QuadrantOwnershipData { quadranteId = quadranteId.Trim() };
            data.quadrantes.Add(quadrant);
        }

        quadrant.ownerSlotIndex = owner.Value;
        quadrant.lastTurn = Mathf.Max(0, turn);
        quadrant.updatedAtUtc = DateTime.UtcNow.ToString("O");

        Debug.Log(
            $"[Campanha] '{campanhaId}/{quadranteId}' agora pertence ao " +
            $"{owner} (turno {quadrant.lastTurn}).");
        return true;
    }

    private static CampaignProgressData Load(string mundoId, string campanhaId)
    {
        string cacheKey = BuildCacheKey(mundoId, campanhaId);
        if (Cache.TryGetValue(cacheKey, out CampaignProgressData cached))
            return cached;

        CampaignProgressData data = new CampaignProgressData
        {
            mundoId = mundoId.Trim(),
            campanhaId = campanhaId.Trim()
        };

        if (data.quadrantes == null)
            data.quadrantes = new List<QuadrantOwnershipData>();

        Cache[cacheKey] = data;
        return data;
    }

    [Serializable]
    public sealed class Snapshot
    {
        public List<CampaignProgressData> campanhas = new List<CampaignProgressData>();
    }

    // A sessao atravessa cenas, mas nunca herda conquistas de outro Novo Jogo.
    public static void BeginNewGame() => Cache.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime() => Cache.Clear();

    public static Snapshot ExportSnapshot()
    {
        var snapshot = new Snapshot();
        foreach (CampaignProgressData data in Cache.Values)
            snapshot.campanhas.Add(JsonUtility.FromJson<CampaignProgressData>(JsonUtility.ToJson(data)));
        return snapshot;
    }

    public static void ImportSnapshot(Snapshot snapshot)
    {
        Cache.Clear();
        if (snapshot?.campanhas == null) return;
        foreach (CampaignProgressData data in snapshot.campanhas)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.mundoId) || string.IsNullOrWhiteSpace(data.campanhaId)) continue;
            Cache[BuildCacheKey(data.mundoId, data.campanhaId)] =
                JsonUtility.FromJson<CampaignProgressData>(JsonUtility.ToJson(data));
        }
    }

    private static QuadrantOwnershipData FindQuadrant(CampaignProgressData data, string quadranteId)
    {
        if (data?.quadrantes == null)
            return null;

        for (int i = 0; i < data.quadrantes.Count; i++)
        {
            QuadrantOwnershipData candidate = data.quadrantes[i];
            if (candidate != null && string.Equals(
                    candidate.quadranteId,
                    quadranteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool HasAddress(string mundoId, string campanhaId, string quadranteId)
    {
        return !string.IsNullOrWhiteSpace(mundoId)
            && !string.IsNullOrWhiteSpace(campanhaId)
            && !string.IsNullOrWhiteSpace(quadranteId);
    }

    private static string BuildCacheKey(string mundoId, string campanhaId)
    {
        return $"{mundoId.Trim()}::{campanhaId.Trim()}";
    }

}
