using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cria o asset da Tabela Magnética já preenchido com docs/magnetic_tabela.md.
///
/// Existe para você não digitar treze listas à mão. Depois de criado, o asset é a
/// verdade — este gerador não roda de novo sozinho e não sobrescreve nada.
/// </summary>
public static class AICaptainDataGenerator
{
    private const string TargetPath = "Assets/DB/AI/AICaptain.asset";

    [MenuItem("Tools/AI/Gerar Tabela Magnética (Capitão)")]
    public static void Generate()
    {
        if (File.Exists(TargetPath))
        {
            if (!EditorUtility.DisplayDialog(
                    "Tabela Magnética",
                    $"Já existe um asset em {TargetPath}.\n\n" +
                    "Regerar DESCARTA qualquer edição que você tenha feito nele.",
                    "Regerar mesmo assim",
                    "Cancelar"))
            {
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(TargetPath));
        AICaptainData asset = ScriptableObject.CreateInstance<AICaptainData>();
        asset.perfis = BuildDefaultTable();

        AssetDatabase.CreateAsset(asset, TargetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log(
            $"[Tabela Magnética] {asset.perfis.Count} perfis gerados em {TargetPath}. " +
            "As listas 'com plano' ficam vazias de propósito: o código deriva da " +
            "lista 'sem plano' (mesma lista + restringir ao setor + RepCell no fim). " +
            "Preencha só se algum papel precisar de algo diferente disso.");
    }

    /// <summary>
    /// A tabela de docs/magnetic_tabela.md, linha por linha.
    ///
    /// Só a coluna "sem plano" é escrita — a "com plano" é derivada, e é justamente
    /// essa derivação que conserta as duas linhas erradas do rascunho original
    /// (Capturador e Estoque iam direto para a RepCell e deixavam de olhar o próprio
    /// setor).
    /// </summary>
    private static List<AICaptainProfile> BuildDefaultTable()
    {
        return new List<AICaptainProfile>
        {
            Profile(UnitRole.Capturador,
                Kind(AICaptainAttractionKind.ConstrucaoCapturavel)),

            Profile(UnitRole.CapturadorCombatente,
                Kind(AICaptainAttractionKind.ConstrucaoCapturavel)),

            Profile(UnitRole.Assalto,
                Captain()),

            Profile(UnitRole.ArtilheiroCombatente,
                Captain()),

            // FogoIndireto é o Fire Support da doutrina.
            Profile(UnitRole.FogoIndireto,
                Captain()),

            Profile(UnitRole.AntiaereoCombatente,
                Kind(AICaptainAttractionKind.AeronaveInimigaDetectada),
                Captain()),

            Profile(UnitRole.Antiaereo,
                Role(UnitRole.Vigilancia),
                Captain()),

            Profile(UnitRole.Interceptador,
                Role(UnitRole.Vigilancia),
                Role(UnitRole.AtaqueAereo),
                Captain()),

            Profile(UnitRole.AtaqueAereo,
                Kind(AICaptainAttractionKind.SuperficieInimigaDetectada),
                Captain()),

            Profile(UnitRole.Transportador,
                Kind(AICaptainAttractionKind.PassageiroPedindoCarona)),

            Profile(UnitRole.Logistica,
                Kind(AICaptainAttractionKind.AliadoFerido),
                Kind(AICaptainAttractionKind.AliadoEmManutencao),
                Captain()),

            Profile(UnitRole.Estoque,
                Kind(AICaptainAttractionKind.ConstrucaoAliadaFalida),
                Role(UnitRole.Logistica),
                Captain()),

            // Vigilância terrestre orbita a construção que quer observar; o
            // predicado tira a construção da lista quando os arredores já estão
            // revelados, e ela passa para a próxima. A vigilância NAVAL não tem
            // prédio embaixo d'água — esse caso entra como célula, vinda de outro
            // serviço. Ver docs/magnetic_tabela.md.
            Profile(UnitRole.Vigilancia,
                Kind(AICaptainAttractionKind.PontoDeObservacao),
                Captain())
        };
    }

    private static AICaptainProfile Profile(
        UnitRole role,
        params AICaptainAttractionEntry[] withoutPlan)
    {
        return new AICaptainProfile
        {
            papel = role,
            semPlano = new List<AICaptainAttractionEntry>(withoutPlan),
            // Vazia de propósito: o AICaptainData deriva.
            comPlano = new List<AICaptainAttractionEntry>()
        };
    }

    private static AICaptainAttractionEntry Captain() =>
        Role(UnitRole.Capturador, "Capitão");

    private static AICaptainAttractionEntry Role(
        UnitRole role, string label = null)
    {
        return new AICaptainAttractionEntry
        {
            procura = AICaptainAttractionKind.UnidadeComPapel,
            papel = role,
            rotulo = label ?? string.Empty
        };
    }

    private static AICaptainAttractionEntry Kind(
        AICaptainAttractionKind kind)
    {
        return new AICaptainAttractionEntry
        {
            procura = kind
        };
    }
}
