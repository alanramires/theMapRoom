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
        output.Add(ownCell);

        // Fora da borda nao existe hexagono. O que sai do coletor passa por um
        // filtro proprio antes de virar terreno conhecido — celula sem tile nao
        // e hex recusado, e ausencia de tabuleiro.
        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        var collected = new HashSet<Vector3Int>();
        PodeDetectarSensor.CollectVisibleCells(
            observer,
            map,
            terrainDatabase,
            collected,
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
            forcedDetectionRangeOverride: Mathf.Max(1, observerData.visao),
            // NAO ligar skipSpecializedTargetLayers aqui: ele nao ignora o
            // alcance das especializacoes, ele DESCARTA toda celula cuja camada
            // tenha uma Detect Specialization. O submarino tem uma para
            // Naval/Surface, e com a flag ligada todo hex de mar sumia antes de
            // qualquer conta de linha. Quem ignora a lista e o parametro
            // abaixo.
            skipSpecializedTargetLayers: false,
            // Nem alcance, nem metodo, nem chave da lista de Detect.
            ignoreDetectSpecializations: true,
            useRangeOnlyForAirHighWhenConfigured: false,
            virtualObserverCell: virtualObserverCell);

        foreach (Vector3Int rawCell in collected)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (boardMap != null && !boardMap.HasTile(cell))
                continue;
            output.Add(cell);
        }
    }

}
