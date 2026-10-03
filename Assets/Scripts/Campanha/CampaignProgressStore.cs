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

        // COMO a partida acabou: o nome de MatchController.VictoryReason. Sem ele,
        // uma vitoria por "exercito eliminado" na rodada 2 do setup era igual a uma
        // vitoria jogada — foi assim que o RODADAS: 3 falso passou por registro
        // real. Gravado como nome, nao int, para o save ser legivel e sobreviver a
        // reordenacao do enum. Vazio = registrado antes do campo existir.
        public string reason;
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

    public static bool TryGetResult(
        string mundoId, string campanhaId, string quadranteId,
        out PlayerSlotId owner, out int turn, out string reason)
    {
        owner = PlayerSlotId.Invalid;
        turn = 0;
        reason = string.Empty;
        if (!HasAddress(mundoId, campanhaId, quadranteId)) return false;
        if (!Cache.TryGetValue(BuildCacheKey(mundoId, campanhaId), out CampaignProgressData data))
            return false;
        QuadrantOwnershipData quadrant = FindQuadrant(data, quadranteId);
        if (quadrant == null) return false;
        owner = PlayerSlotId.FromIndex(quadrant.ownerSlotIndex);
        turn = quadrant.lastTurn;
        reason = quadrant.reason ?? string.Empty;
        return owner.IsValid;
    }

    /// <summary>
    /// Texto do motivo para o jogador. Mora aqui, junto do dado, para o placar da
    /// campanha e o Save Inspector dizerem a mesma coisa.
    /// </summary>
    public static string DescreverMotivo(string reason)
    {
        switch (reason)
        {
            case nameof(MatchController.VictoryReason.HeadQuarterCaptured): return PanelHelperController.ResolveHelperMessage("helper.campaign.reason.hq", "QG capturado");
            case nameof(MatchController.VictoryReason.ArmyEliminated): return PanelHelperController.ResolveHelperMessage("helper.campaign.reason.army", "exército eliminado");
            case nameof(MatchController.VictoryReason.Surrender): return PanelHelperController.ResolveHelperMessage("helper.campaign.reason.surrender", "rendição");
            case nameof(MatchController.VictoryReason.VictoryStars): return PanelHelperController.ResolveHelperMessage("helper.campaign.reason.stars", "estrelas de vitória");
            case null:
            case "": return "—";
            default: return reason;
        }
    }

    public static bool RecordOwner(
        string mundoId,
        string campanhaId,
        string quadranteId,
        PlayerSlotId owner,
        int turn,
        string reason)
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
        quadrant.reason = reason ?? string.Empty;

        Debug.Log(
            $"[Campanha] '{campanhaId}/{quadranteId}' agora pertence ao " +
            $"{owner} (rodada {quadrant.lastTurn}, {DescreverMotivo(quadrant.reason)}).");
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
