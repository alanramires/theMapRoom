using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procura construção capturável que ficou sem chave.
///
/// A combinação `Is Capturable` ligado + lista de skills vazia é a falha mais
/// silenciosa que a captura tem: o prédio parece capturável no editor, parece
/// capturável na cena, e ninguém o captura — nem o jogador, nem a IA. Nada
/// reclama em runtime, porque "não pode capturar" é uma resposta legítima.
///
/// O editor do ConstructionData já acusa isso caso a caso; isto aqui varre o
/// projeto inteiro de uma vez.
///
/// Ver docs/manual/01_principios_e_vocabulario.md — "uma habilidade é uma chave,
/// não um poder".
/// </summary>
public static class CaptureKeyAuditor
{
    /// <summary>Só lê. Não escreve em asset nenhum.</summary>
    [MenuItem("Tools/AI/Auditar Chaves de Captura")]
    public static void Audit()
    {
        List<ConstructionData> all = FindAllConstructionData();

        // O caso perigoso e SO este: marcada como capturavel e sem chave. Ela
        // parece capturavel no editor, aparece capturavel na cena, e nao e
        // capturavel por ninguem — falha silenciosa.
        var broken = new List<ConstructionData>();
        var inertKeys = new List<ConstructionData>();
        int capturableCount = 0;

        for (int i = 0; i < all.Count; i++)
        {
            ConstructionData data = all[i];
            bool capturable = data.constructionConfiguration != null
                              && data.constructionConfiguration.isCapturable;
            bool hasKeys = data.requiredSkillsToCapture != null
                           && data.requiredSkillsToCapture.Count > 0;

            if (capturable)
                capturableCount++;
            if (capturable && !hasKeys)
                broken.Add(data);
            else if (!capturable && hasKeys)
                inertKeys.Add(data);
        }

        var report = new StringBuilder();
        report.AppendLine(
            $"[Auditoria de captura] {all.Count} ficha(s), " +
            $"{capturableCount} marcada(s) como capturável(is).");

        if (broken.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"ERRO — {broken.Count} capturável(is) SEM chave. " +
                "Ninguém captura, nem jogador nem IA:");
            for (int i = 0; i < broken.Count; i++)
                report.AppendLine($"  • {broken[i].name}");
        }

        if (inertKeys.Count > 0)
        {
            report.AppendLine();
            report.AppendLine(
                $"Aviso — {inertKeys.Count} com chave e Is Capturable " +
                "desligado. A lista não faz nada ali:");
            for (int i = 0; i < inertKeys.Count; i++)
                report.AppendLine($"  • {inertKeys[i].name}");
        }

        if (broken.Count > 0)
            Debug.LogError(report.ToString());
        else if (inertKeys.Count > 0)
            Debug.LogWarning(report.ToString());
        else
            Debug.Log(report.ToString() + "\nNenhum problema.");
    }

    /// <summary>
    /// Todas as fichas do projeto. O filtro de capturabilidade é feito depois,
    /// no laço, porque as duas metades da regra vivem em lugares diferentes:
    /// `isCapturable` é do tipo (e a instância na cena tem a sua cópia), a
    /// chave é do tipo e lida ao vivo do asset.
    /// </summary>
    private static List<ConstructionData> FindAllConstructionData()
    {
        var found = new List<ConstructionData>();
        string[] guids = AssetDatabase.FindAssets("t:ConstructionData");
        for (int i = 0; i < guids.Length; i++)
        {
            ConstructionData data =
                AssetDatabase.LoadAssetAtPath<ConstructionData>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
            if (data != null)
                found.Add(data);
        }
        return found;
    }
}
