using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Preenche as chaves de captura padrão em todas as fichas de construção.
///
/// Existe porque `requiredSkillsToCapture` deixou de ser `List&lt;SkillData&gt;` e
/// virou `List&lt;CaptureSkillEfficiency&gt;`. A Unity não converte um pelo outro:
/// o que estava serializado nos assets **é descartado** na primeira
/// desserialização com o tipo novo, e as listas voltam vazias — ou seja, nenhuma
/// construção seria capturável até alguém repovoar.
///
/// Este é o "alguém".
///
/// **Nunca sobrescreve entrada existente.** Se você já ajustou um bunker para
/// 0,8, ele continua 0,8; o semeador só acrescenta o que falta.
/// </summary>
public static class CaptureKeySeeder
{
    // Os nomes dos assets de skill. Procurados por nome porque o semeador roda
    // uma vez, em migração — codigo de runtime nunca procura skill por nome.
    private const string DefaultKeyName = "Captura Construções";
    private const string AlternateKeyName = "Capturador Alternativo";

    private const float DefaultEfficiency = 1f;
    private const float AlternateEfficiency = 0.5f;

    [MenuItem("Tools/AI/Semear Chaves de Captura")]
    public static void Seed()
    {
        SkillData defaultKey = FindSkill(DefaultKeyName);
        SkillData alternateKey = FindSkill(AlternateKeyName);

        if (defaultKey == null)
        {
            EditorUtility.DisplayDialog(
                "Semear Chaves de Captura",
                $"Não encontrei o SkillData '{DefaultKeyName}'.\n\n" +
                "Sem a chave padrão, semear deixaria as construções sem " +
                "ninguém capaz de capturá-las.",
                "Ok");
            return;
        }

        List<ConstructionData> all = FindAllConstructionData();
        var report = new StringBuilder();
        report.AppendLine($"Fichas de construção: {all.Count}");
        report.AppendLine();
        report.AppendLine("Vão receber, se ainda não tiverem:");
        report.AppendLine($"  • {defaultKey.name}  ×{DefaultEfficiency}");
        if (alternateKey != null)
            report.AppendLine($"  • {alternateKey.name}  ×{AlternateEfficiency}");
        else
            report.AppendLine($"  • ({AlternateKeyName} não encontrado — pulado)");
        report.AppendLine();
        report.AppendLine(
            "Entrada que já existe NÃO é sobrescrita: eficiência ajustada à " +
            "mão sobrevive.");
        report.AppendLine();
        report.AppendLine(
            "Entradas fantasma (sem skill) são removidas. Elas aparecem quando " +
            "a lista muda de tipo: a Unity preserva a contagem do array antigo " +
            "e não consegue mapear o conteúdo.");

        if (!EditorUtility.DisplayDialog(
                "Semear Chaves de Captura", report.ToString(),
                "Semear", "Cancelar"))
        {
            return;
        }

        int touched = 0;
        int purged = 0;
        for (int i = 0; i < all.Count; i++)
        {
            ConstructionData data = all[i];
            if (data.requiredSkillsToCapture == null)
                data.requiredSkillsToCapture =
                    new List<CaptureSkillEfficiency>();

            int removed = PurgeGhostEntries(data);
            purged += removed;

            bool changed = removed > 0;
            changed |= EnsureKey(data, defaultKey, DefaultEfficiency);
            if (alternateKey != null)
                changed |= EnsureKey(data, alternateKey, AlternateEfficiency);

            if (changed)
            {
                EditorUtility.SetDirty(data);
                touched++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Chaves de captura] {touched} ficha(s) atualizada(s) de " +
            $"{all.Count}" +
            (purged > 0
                ? $", {purged} entrada(s) fantasma removida(s)"
                : string.Empty) +
            ". Rode Tools > AI > Auditar Chaves de Captura para conferir que " +
            "nenhuma capturável ficou sem chave.");
    }

    /// <summary>
    /// Remove entradas com skill nula.
    ///
    /// A troca de `List&lt;SkillData&gt;` para `List&lt;CaptureSkillEfficiency&gt;`
    /// não esvaziou as listas como se poderia esperar: a Unity **preservou a
    /// contagem** do array antigo e não conseguiu mapear a referência para
    /// dentro do struct novo. Cada ficha ficou com um elemento fantasma —
    /// `skill = null`, `efficiency = 0` — que ocupa lugar e não abre nada.
    ///
    /// Idempotente: rodar de novo não remove mais nada.
    /// </summary>
    private static int PurgeGhostEntries(ConstructionData data)
    {
        int removed = 0;
        for (int i = data.requiredSkillsToCapture.Count - 1; i >= 0; i--)
        {
            CaptureSkillEfficiency entry = data.requiredSkillsToCapture[i];
            if (entry == null || entry.skill == null)
            {
                data.requiredSkillsToCapture.RemoveAt(i);
                removed++;
            }
        }
        return removed;
    }

    private static bool EnsureKey(
        ConstructionData data, SkillData skill, float efficiency)
    {
        for (int i = 0; i < data.requiredSkillsToCapture.Count; i++)
        {
            if (data.requiredSkillsToCapture[i]?.skill == skill)
                return false;
        }

        data.requiredSkillsToCapture.Add(new CaptureSkillEfficiency
        {
            skill = skill,
            efficiency = efficiency
        });
        return true;
    }

    private static SkillData FindSkill(string assetName)
    {
        string[] guids = AssetDatabase.FindAssets($"t:SkillData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(path);
            if (skill != null && skill.name == assetName)
                return skill;
        }
        return null;
    }

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
