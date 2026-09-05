using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Estado do jogador entre batalhas. O mapa assado continua imutavel; este
/// arquivo guarda somente quem controla cada quadrante e o ultimo resultado.
///
/// O DONO E O SLOT, NUNCA A COR.
///
/// A cor de cada slot e escolhida no menu, uma vez por partida: hoje o jogador e
/// Amarelo, amanha e Vermelho. Gravar "este quadrante e do Amarelo" grava uma
/// fantasia, nao um dono — na partida seguinte a cor pode nao estar em campo, e
/// a pergunta que importa ("fui EU que tomei este?") deixa de ter resposta.
///
/// Gravando o slot, a pergunta continua respondivel para sempre, e a cor volta a
/// ser o que ela e: apresentacao, resolvida na hora de pintar por
/// <c>MatchController.GetTeamIdForSlot</c>.
/// </summary>
public static class CampaignProgressStore
{
    [Serializable]
    private sealed class CampaignProgressData
    {
        public int schemaVersion = 1;
        public string mundoId;
        public string campanhaId;
        public List<QuadrantOwnershipData> quadrantes = new List<QuadrantOwnershipData>();
    }

    [Serializable]
    private sealed class QuadrantOwnershipData
    {
        public string quadranteId;
        public int ownerSlotIndex = PlayerSlotId.InvalidValue;
        public int lastTurn;
        public string updatedAtUtc;
    }

    private const string DirectoryName = "CampaignProgress";
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

        if (!Save(data))
            return false;

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

        string path = GetPath(mundoId, campanhaId);
        CampaignProgressData data = null;
        if (File.Exists(path))
        {
            try
            {
                data = JsonUtility.FromJson<CampaignProgressData>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Campanha] Progresso ilegivel em '{path}': {exception.Message}");
            }
        }

        if (data == null)
        {
            data = new CampaignProgressData
            {
                mundoId = mundoId.Trim(),
                campanhaId = campanhaId.Trim()
            };
        }

        if (data.quadrantes == null)
            data.quadrantes = new List<QuadrantOwnershipData>();

        Cache[cacheKey] = data;
        return data;
    }

    private static bool Save(CampaignProgressData data)
    {
        string path = GetPath(data.mundoId, data.campanhaId);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(data, prettyPrint: true));
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Campanha] Nao foi possivel salvar progresso em '{path}': {exception.Message}");
            return false;
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

    private static string GetPath(string mundoId, string campanhaId)
    {
        string fileName = $"{MakeFileSafe(mundoId)}__{MakeFileSafe(campanhaId)}.json";
        return Path.Combine(Application.persistentDataPath, DirectoryName, fileName);
    }

    private static string MakeFileSafe(string value)
    {
        string safe = value.Trim();
        char[] invalid = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalid.Length; i++)
            safe = safe.Replace(invalid[i], '_');
        return safe;
    }
}
