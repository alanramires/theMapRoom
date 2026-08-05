using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Uma das duas verdades do jogo: **revela HEXAGONOS**. A outra e o
/// PodeDetectar, que faz unidades aparecerem.
///
/// Detectar um alvo nunca revela o hex embaixo dele — o caca marcado a sete
/// hexes sobre territorio desconhecido, o sniper ao lado, o submarino colado no
/// navio. As duas respostas sao independentes nos dois sentidos, e por isso este
/// sensor tem laco proprio: ele nao chama o PodeDetectar nem desliga regras dele
/// por flag. Ja custou caro — uma flag de deteccao apagou o mar inteiro de um
/// submarino descartando celula antes de qualquer conta de linha.
///
/// A regra, como o autor a definiu:
///
///   alcance   UnitData.visao — nada da lista de Detect alarga ou estreita
///   camada    a superficie do terreno da celula; revelacao nao tem meio
///   origem    o EV do lugar onde a unidade esta
///   linha     do EV de origem ate o EV do destino; para so se um bloqueador
///             tiver EV MAIOR que a linha naquele ponto
///   borda     celula sem tile nao e hex recusado, e ausencia de tabuleiro
///
/// Ar e submerso nao sao terreno: o EV deles vem do DPQ Air Height Config. O
/// submarino em Submerged sai de EV 0 e por isso e um soldado em cima da agua —
/// mesma regra, mesmo raio, linha nivelada por planicie, praia e mar.
///
/// Consulta pura: nao move unidade, nao publica FOW, nao grava exploracao e nao
/// registra contato.
/// </summary>
public static class PodeEnxergarSensor
{
    private static readonly List<Vector3Int> candidateCells = new List<Vector3Int>(256);

    /// <summary>
    /// Acrescenta a <paramref name="output"/> as celulas cujo terreno o
    /// observador conhece.
    ///
    /// <paramref name="virtualObserverCell"/> projeta a resposta a partir de uma
    /// posicao hipotetica sem mover nada — posicao provisoria nao publica
    /// conhecimento; quem consome e que decide o que fazer com a projecao.
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
        if (observer.IsEmbarked && !virtualObserverCell.HasValue)
            return;
        if (!observer.TryGetUnitData(out UnitData observerData) || observerData == null)
            return;

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return;

        Vector3Int ownCell = virtualObserverCell ?? observer.CurrentCellPosition;
        ownCell.z = 0;

        int range = Mathf.Max(1, observerData.visao);
        candidateCells.Clear();
        HexGridGeometry.CollectCellsInRadius(boardMap, ownCell, range, candidateCells);

        for (int i = 0; i < candidateCells.Count; i++)
        {
            Vector3Int cell = candidateCells[i];

            // A propria celula: a unidade conhece o terreno onde esta pisando, e
            // nao ha linha a tracar contra si mesma.
            if (cell == ownCell)
            {
                output.Add(cell);
                continue;
            }

            // target null de proposito: quem responde pelo EV do destino e o
            // TERRENO da celula, nao um ocupante. O que se revela e o hex.
            if (ObservationLineService.TryTrace(
                    boardMap,
                    terrainDatabase,
                    ownCell,
                    cell,
                    observer,
                    target: null,
                    dpqAirHeightConfig,
                    out _,
                    out _,
                    out _,
                    enableLosValidation))
            {
                output.Add(cell);
            }
        }
    }

    /// <summary>
    /// A MESMA linha do <see cref="CollectKnownTerrainCells"/>, para um hex so,
    /// com o perfil preenchido — de onde partiu, por onde passou, ate onde subiu
    /// e contra o que parou.
    ///
    /// Existe para a auditoria: a janela precisa contar a viagem da reta, e
    /// refazer a conta por fora foi como ferramenta e jogo ja discordaram. Aqui
    /// e o mesmo traçado, com o mesmo alvo nulo de proposito — quem responde
    /// pelo EV do destino e o TERRENO da celula, porque o que se revela e o hex.
    ///
    /// <paramref name="forcedTargetDomain"/> e o par dele existem so para a
    /// ferramenta projetar uma camada hipotetica; a revelacao real nunca forca
    /// camada.
    /// </summary>
    public static bool TryProfileVisionLine(
        UnitManager observer,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int targetCell,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLosValidation,
        ObservationLineProfile profile,
        Domain? forcedTargetDomain = null,
        HeightLevel? forcedTargetHeightLevel = null)
    {
        profile?.Clear();
        if (observer == null)
            return false;

        Tilemap boardMap = map != null ? map : observer.BoardTilemap;
        if (boardMap == null)
            return false;

        Vector3Int originCell = observer.CurrentCellPosition;
        originCell.z = 0;
        targetCell.z = 0;

        return ObservationLineService.TryTrace(
            boardMap,
            terrainDatabase,
            originCell,
            targetCell,
            observer,
            target: null,
            dpqAirHeightConfig,
            out _,
            out _,
            out _,
            enableLosValidation,
            forcedTargetDomain,
            forcedTargetHeightLevel,
            OriginEvRule.InheritTerrain,
            profile);
    }
}
