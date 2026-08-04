using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Pergunta de HEXES. Responde de quais celulas este observador conhece o
/// terreno — a linha descendente do EV dele ate a superficie.
///
/// Ele nao responde por unidades, e isso nao e uma limitacao: detectar um
/// contato nunca revela o hex embaixo dele. O caca marcado a sete hexes sobre
/// territorio desconhecido, o sniper ao lado, o submarino colado no navio. As
/// duas respostas sao independentes nos dois sentidos, e por isso moram em
/// entidades separadas — quem responde por unidades e o PodeDetectar.
///
/// Por isso a assinatura NAO aceita camada, e o alcance e SEMPRE o campo
/// UnitData.visao — a visao padrao da ficha, que e o que revela hexes.
/// Toda visao adicional pendurada na lista de Detect Specializations existe
/// para o PodeDetectar: ela faz unidade aparecer, nunca terreno. Nem alarga
/// (EWACS, submarino) nem estreita.
///
/// Revelacao tambem nao tem MEIO, so alcance: um submarino encostado na praia
/// revela praia, planicie, floresta e mar dentro do mesmo raio. Cada celula
/// responde pela camada nativa do terreno dela; o observador nao impoe a sua.
///
/// Consulta pura: nao move unidade, nao publica FOW, nao grava exploracao e
/// nao registra contato.
/// </summary>
public static class PodeEnxergarSensor
{
    /// <summary>
    /// Diagnostico opcional. Liga pelo nome da unidade (substring, case
    /// insensitive) e escreve uma linha por chamada em PodeEnxergar.log na raiz
    /// do projeto. Serve para responder o que rodou, nao o que se supoe.
    /// </summary>
    public static string DebugUnitNameFilter;

    private static void LogDebug(
        UnitManager observer,
        UnitData observerData,
        int appliedRange,
        ICollection<Vector3Int> output,
        Vector3Int ownCell,
        int countBefore)
    {
        if (string.IsNullOrEmpty(DebugUnitNameFilter)
            || observer == null
            || observer.name.IndexOf(
                   DebugUnitNameFilter,
                   System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        int maxDistance = 0;
        foreach (Vector3Int cell in output)
        {
            int distance = Mathf.RoundToInt(
                SectorManager.HexDistance(ownCell, cell));
            if (distance > maxDistance)
                maxDistance = distance;
        }

        string line =
            $"[PodeEnxergar] unit={observer.name} " +
            $"domain={observer.GetDomain()}/{observer.GetHeightLevel()} " +
            $"cell=({ownCell.x},{ownCell.y}) " +
            $"visao={(observerData != null ? observerData.visao : -1)} " +
            $"alcanceAplicado={appliedRange} " +
            $"celulas={output.Count - countBefore} " +
            $"maiorDistancia={maxDistance}";
        Debug.Log(line, observer);
        try
        {
            System.IO.File.AppendAllText(
                "PodeEnxergar.log",
                line + System.Environment.NewLine);
        }
        catch (System.Exception)
        {
            // Diagnostico nunca derruba partida.
        }
    }

    /// <summary>
    /// Acrescenta a <paramref name="output"/> as celulas cujo terreno o
    /// observador conhece. As duas passadas de superficie sao unidas ali, entao
    /// <paramref name="output"/> deve ser um conjunto.
    ///
    /// <paramref name="virtualObserverCell"/> projeta a resposta a partir de uma
    /// posicao hipotetica sem mover nada — posicao provisoria nao publica
    /// conhecimento, quem consome e que decide o que fazer com a projecao.
    /// </summary>
    public static void CollectKnownTerrainCells(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> output,
        DPQAirHeightConfig dpqAirHeightConfig = null,
        bool enableLosValidation = true,
        Vector3Int? virtualObserverCell = null)
    {
        if (output == null || observer == null)
            return;
        if (!observer.TryGetUnitData(out UnitData observerData)
            || observerData == null)
        {
            return;
        }

        // O proprio hex. O coletor pula distancia 0 ("if (distance <= 0)
        // continue"), entao ele nunca sai de la — e a unidade obviamente
        // conhece o terreno onde esta pisando.
        Vector3Int ownCell = virtualObserverCell ?? observer.CurrentCellPosition;
        ownCell.z = 0;
        int countBefore = output.Count;
        output.Add(ownCell);

        int appliedRange = Mathf.Max(1, observerData.visao);
        PodeDetectarSensor.CollectVisibleCells(
            observer,
            map,
            terrainDatabase,
            output,
            dpqAirHeightConfig,
            enableLosValidation,
            // Observador avancado designa CONTATO, nao mapa.
            enableSpotter: false,
            // A camada do alvo e a do TERRENO da celula, nao a do ocupante:
            // o que se revela e o hex, e quem esta em cima nao opina.
            useOccupantLayerForTarget: false,
            // Com true, o alcance de revelacao era elevado ao alcance da
            // camada do proprio observador — o EWACS em AirHigh revelando
            // chao no alcance aereo, o submarino revelando mar no alcance de
            // cacar submarino.
            preserveObserverLayerRangeForHexVisibility: false,
            // Sem camada forcada: cada celula responde pela camada nativa
            // dela. E por isso que o submarino encostado na praia revela
            // praia, planicie, floresta e mar no mesmo raio — revelacao nao
            // tem meio, so alcance.
            forceVirtualTargetLayer: false,
            forcedVirtualTargetDomain: Domain.Land,
            forcedVirtualTargetHeight: HeightLevel.Surface,
            // O alcance e a visao padrao da ficha. Curto-circuita o
            // ResolveDetectionRange e, com ele, qualquer especializacao.
            forcedDetectionRangeOverride: appliedRange,
            skipSpecializedTargetLayers: true,
            // Nem alcance, nem metodo, nem chave da lista de Detect.
            ignoreDetectSpecializations: true,
            useRangeOnlyForAirHighWhenConfigured: false,
            virtualObserverCell: virtualObserverCell);

        LogDebug(observer, observerData, appliedRange, output, ownCell, countBefore);
    }

}
