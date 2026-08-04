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
/// Por isso a assinatura NAO aceita camada. O alcance e sempre resolvido na
/// superficie da CELULA ALVO: Land/Surface em terra, Naval/Surface na agua.
/// Uma especializacao de ar ou de submerso nunca alarga a revelacao, e uma
/// especializacao de superficie que estreite continua valendo — os dois
/// sentidos saem de graca do ResolveVisionFor da camada certa.
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

        CollectForSurfaceDomain(
            observer,
            map,
            terrainDatabase,
            output,
            dpqAirHeightConfig,
            enableLosValidation,
            virtualObserverCell,
            Domain.Land);
        CollectForSurfaceDomain(
            observer,
            map,
            terrainDatabase,
            output,
            dpqAirHeightConfig,
            enableLosValidation,
            virtualObserverCell,
            Domain.Naval);
    }

    /// <summary>
    /// Uma passada por familia de superficie. O proprio PodeDetectar descarta
    /// as celulas que nao aceitam a camada forcada, entao Land cobre a terra,
    /// Naval cobre a agua, e a uniao das duas cobre o tabuleiro sem que este
    /// sensor precise inspecionar terreno por conta propria.
    /// </summary>
    private static void CollectForSurfaceDomain(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        ICollection<Vector3Int> output,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        Vector3Int? virtualObserverCell,
        Domain surfaceDomain)
    {
        PodeDetectarSensor.CollectVisibleCells(
            observer,
            map,
            terrainDatabase,
            output,
            dpqAirHeightConfig,
            enableLosValidation,
            // Observador avancado designa CONTATO, nao mapa. Terreno revelado
            // por spotter seria conhecimento que ninguem olhou.
            enableSpotter: false,
            // A camada do alvo e imposta abaixo; a do ocupante nao opina sobre
            // o terreno da celula.
            useOccupantLayerForTarget: false,
            // O ponto do split. Com true, o alcance de revelacao e elevado ao
            // alcance da camada do PROPRIO observador — e por isso que hoje o
            // EWACS em AirHigh revela chao no alcance aereo dele e o submarino
            // revela superficie naval no alcance submerso.
            preserveObserverLayerRangeForHexVisibility: false,
            forceVirtualTargetLayer: true,
            forcedVirtualTargetDomain: surfaceDomain,
            forcedVirtualTargetHeight: HeightLevel.Surface,
            forcedDetectionRangeOverride: -1,
            // As especializacoes continuam valendo: quem decide o alcance desta
            // passada e o ResolveVisionFor da superficie consultada, que ja
            // considera a excecao daquela camada quando ela existe.
            skipSpecializedTargetLayers: false,
            useRangeOnlyForAirHighWhenConfigured: false,
            virtualObserverCell: virtualObserverCell);
    }
}
