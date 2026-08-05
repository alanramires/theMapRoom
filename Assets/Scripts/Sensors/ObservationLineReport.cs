using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Uma linha do relatorio: rotulo a esquerda, valor a direita.</summary>
public struct ObservationLineReportRow
{
    public string label;
    public string value;

    public ObservationLineReportRow(string label, string value)
    {
        this.label = label;
        this.value = value;
    }
}

/// <summary>
/// O RELATORIO da linha. Recebe um <see cref="ObservationLineProfile"/> e devolve
/// as mesmas frases, nos mesmos rotulos, para qualquer janela que pergunte.
///
/// Enquanto cada janela montava o proprio texto, a mesma reta aparecia em dois
/// formatos: uma contava a subida passo a passo e nomeava o bloqueador, a outra
/// imprimia floats colados e o leitor deduzia que 2,25 > 2. Comparar as duas
/// virava traducao manual — e comparar as duas e para isso que elas existem.
///
/// Nao calcula linha nenhuma. Se um numero nao esta no perfil, ele nao aparece
/// aqui: relatorio que recalcula e a porta pela qual ferramenta e jogo
/// discordam.
/// </summary>
public static class ObservationLineReport
{
    /// <summary>
    /// Acima disto a subida vira ilegivel numa linha de Inspector, e o que
    /// importa — de onde partiu e onde parou — fica no meio do texto. Entao as
    /// pontas ficam e o miolo e resumido pela contagem.
    /// </summary>
    private const int MaxEvPathValuesShown = 16;
    private const int EvPathHeadValues = 8;
    private const int EvPathTailValues = 6;

    /// <summary>
    /// O relatorio inteiro, na ordem em que se le: para onde a linha foi, como
    /// ela subiu, se chegou e — conforme o desfecho — por cima do que ela passou
    /// ou contra o que ela parou.
    /// </summary>
    public static void Build(
        ObservationLineProfile profile,
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        List<ObservationLineReportRow> rows)
    {
        if (rows == null)
            return;

        rows.Clear();
        if (profile == null || !profile.HasProfile)
            return;

        rows.Add(new ObservationLineReportRow("Viagem da linha", FormatTravel(profile)));
        rows.Add(new ObservationLineReportRow("Subida da linha", FormatEvPath(profile)));
        rows.Add(new ObservationLineReportRow("LoS direta", profile.reached ? "sim" : "nao"));

        if (!profile.losValidationEnabled)
        {
            rows.Add(new ObservationLineReportRow(
                "Validacao de LoS",
                "desligada — nada pode deter a linha"));
        }

        if (profile.reached)
        {
            rows.Add(new ObservationLineReportRow(
                "EV passou",
                profile.hasStrongestPassed
                    ? profile.lineHeightAtStrongestPassed.ToString("0.00")
                    : "-"));
            rows.Add(new ObservationLineReportRow(
                "Passou por",
                profile.hasStrongestPassed
                    ? DescribeCell(tilemap, terrainDatabase, profile.strongestPassedCell, profile.strongestPassedCellEv)
                    : "nenhum bloqueador relevante"));
            rows.Add(new ObservationLineReportRow(
                "EV final (chegou)",
                profile.FinalReachedEv.ToString("0.00")));
            return;
        }

        if (!profile.hasBlocker)
        {
            rows.Add(new ObservationLineReportRow("EV Bloqueador", "linha nao concluida"));
            return;
        }

        rows.Add(new ObservationLineReportRow(
            "EV na parada",
            profile.lineHeightAtBlockedCell.ToString("0.00")));
        rows.Add(new ObservationLineReportRow(
            "Tentou ver EV",
            profile.blockedCellEv.ToString("0.00")));
        rows.Add(new ObservationLineReportRow(
            "EV Bloqueador",
            DescribeCell(tilemap, terrainDatabase, profile.blockedCell, profile.blockedCellEv)));
        rows.Add(new ObservationLineReportRow(
            "Bloqueio LOS",
            $"{profile.blockedCell.x},{profile.blockedCell.y}"));
    }

    /// <summary>Uma linha so, para log de Console.</summary>
    public static string BuildSingleLine(
        ObservationLineProfile profile,
        Tilemap tilemap,
        TerrainDatabase terrainDatabase)
    {
        List<ObservationLineReportRow> rows = new List<ObservationLineReportRow>();
        Build(profile, tilemap, terrainDatabase, rows);
        if (rows.Count <= 0)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0)
                sb.Append(" | ");
            sb.Append(rows[i].label).Append('=').Append(rows[i].value);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Ascendente, descendente ou nivelada — e de que EV para que EV.
    ///
    /// O EV do alvo e o DESTINO PRETENDIDO, nao a chegada: e dele que sai a
    /// inclinacao, e sem ele a altura da linha em cada hex nao se explica. Mas
    /// quando a reta e detida ele nao pode se passar por resultado — lido em
    /// sequencia com a subida logo abaixo, "0,00 -> 4,00" parece dizer que ela
    /// chegou a 4, quando ela morreu em 1,75.
    /// </summary>
    public static string FormatTravel(ObservationLineProfile profile)
    {
        if (profile == null)
            return string.Empty;

        string direction =
            Mathf.Abs(profile.targetEv - profile.originEv) < 0.001f ? "nivelada"
            : profile.targetEv > profile.originEv ? "ascendente"
            : "descendente";

        string travel = $"{direction}  {profile.originEv:0.00} -> {profile.targetEv:0.00}";
        if (profile.reached)
            return travel;

        return $"{travel}   NAO CHEGOU (parou em {profile.FinalReachedEv:0.00})";
    }

    /// <summary>A altura da linha em cada ponto, na ordem.</summary>
    public static string FormatEvPath(ObservationLineProfile profile)
    {
        if (profile == null || profile.evPath.Count <= 0)
            return string.Empty;

        List<float> path = profile.evPath;
        StringBuilder sb = new StringBuilder();
        if (path.Count <= MaxEvPathValuesShown)
        {
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                    sb.Append(" -> ");
                sb.Append(path[i].ToString("0.00"));
            }

            return sb.ToString();
        }

        int hidden = path.Count - EvPathHeadValues - EvPathTailValues;
        for (int i = 0; i < EvPathHeadValues; i++)
        {
            if (i > 0)
                sb.Append(" -> ");
            sb.Append(path[i].ToString("0.00"));
        }

        sb.Append(" -> ... (+").Append(hidden).Append(") -> ");
        for (int i = path.Count - EvPathTailValues; i < path.Count; i++)
        {
            if (i > path.Count - EvPathTailValues)
                sb.Append(" -> ");
            sb.Append(path[i].ToString("0.00"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Como se chama o que esta naquele hex, e com que EV ele respondeu.
    ///
    /// Le pelo <see cref="ObservationCellService"/>, a MESMA fonte que a linha
    /// consultou. Um relatorio que resolve o terreno por conta propria pode
    /// nomear algo que o traçado nao viu — foi assim que ferramenta e jogo
    /// discordaram uma vez, e a janela deu confianca falsa sobre uma ficha
    /// correta.
    /// </summary>
    public static string DescribeCell(
        Tilemap tilemap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        float fallbackEv)
    {
        cell.z = 0;
        bool hasTerrain =
            ObservationCellService.TryResolveTerrain(tilemap, terrainDatabase, cell, out TerrainTypeData terrain) &&
            terrain != null;

        if (tilemap != null)
        {
            if (ObservationCellService.TryResolveConstruction(tilemap, cell, out ConstructionData constructionData) &&
                constructionData != null)
            {
                string constructionName = ResolveEntityLabel(
                    constructionData.displayName,
                    constructionData.id,
                    constructionData.name);
                float displayEv = hasTerrain &&
                    terrain.TryGetConstructionVisionOverride(constructionData, out int constructionOverrideEv, out _)
                    ? Mathf.Max(0, constructionOverrideEv)
                    : (hasTerrain ? Mathf.Max(0, terrain.ev) : Mathf.Max(0f, fallbackEv));
                return $"{constructionName} (EV: {displayEv})";
            }

            StructureData structure = ObservationCellService.ResolveStructure(tilemap, cell);
            if (structure != null)
            {
                string structureName = ResolveEntityLabel(structure.displayName, structure.id, structure.name);
                float displayEv = hasTerrain &&
                    terrain.TryGetStructureVisionOverride(structure, out int structureOverrideEv, out _)
                    ? Mathf.Max(0, structureOverrideEv)
                    : (hasTerrain ? Mathf.Max(0, terrain.ev) : Mathf.Max(0f, fallbackEv));
                return $"{structureName} (EV: {displayEv})";
            }
        }

        if (hasTerrain)
        {
            string terrainName = ResolveEntityLabel(terrain.displayName, terrain.id, terrain.name);
            return $"{terrainName} (EV: {Mathf.Max(0, terrain.ev)})";
        }

        return $"bloqueador sem nome (EV: {Mathf.Max(0f, fallbackEv)})";
    }

    private static string ResolveEntityLabel(string displayName, string id, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();
        if (!string.IsNullOrWhiteSpace(assetName))
            return assetName.Trim();
        return "sem_nome";
    }
}
