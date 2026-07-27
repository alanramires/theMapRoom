using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    /// <summary>
    /// Materializa a recomendacao pura do Melhor Estoque para a propria
    /// unidade. Tactical executa movimento + transferencia nesta rodada;
    /// Operational apenas progride para o rendezvous e reavalia no proximo
    /// turno. A consulta nao move a unidade para simular posicoes.
    /// </summary>
    private bool TryBuildLogisticsStockRestockAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = string.Empty;
        if (unit == null
            || snapshot == null
            || boardTilemap == null
            || terrainDatabase == null)
        {
            reason = "contexto de estoque indisponivel";
            return false;
        }

        fromCell.z = 0;
        StockNeedAssessment need =
            StockNeedAssessmentService.Evaluate(unit);
        if (need == null || !need.NeedsStock)
        {
            reason = need != null
                ? need.reason
                : "avaliacao de estoque indisponivel";
            return false;
        }

        MelhorEstoqueResult result =
            MelhorEstoqueService.Evaluate(
                new MelhorEstoqueRequest
                {
                    unit = unit,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    intent = MelhorEstoqueIntent.ReplenishSelf,
                    tacticalBudget = Mathf.Max(
                        0, unit.RemainingMovementPoints),
                    operationalTurns = 2,
                    includeStrategic = false,
                    emulateStockFromUnitData = false,
                    maxThreat = 0f,
                    evaluateThreat = cell =>
                        CalculateThreatLevel(
                            cell, snapshot.AITeam),
                    diagnosticLog = line => Debug.Log(line)
                });

        if (result?.reachDecision == null
            || !result.reachDecision.Found
            || result.reachDecision.Decision?.Value == null)
        {
            int rejected = result != null
                ? result.rejected.Count
                : 0;
            reason =
                $"{need.reason} MelhorEstoque sem encontro " +
                $"Tactical/Operational (rejeitados={rejected})";
            return false;
        }

        MelhorEstoqueOption stock =
            result.reachDecision.Decision.Value;
        PodeTransferirOption transfer =
            stock.prospectiveTransfer;
        if (transfer == null
            || transfer.flowMode !=
                TransferFlowMode.Recebedor)
        {
            reason =
                "MelhorEstoque retornou fluxo diferente de Receber";
            return false;
        }

        Vector3Int rendezvous = stock.actionCell;
        rendezvous.z = 0;
        if (result.reachDecision.Tier ==
            AIReachDecisionTier.Tactical)
        {
            if (rendezvous != fromCell
                && (paths == null
                    || !paths.ContainsKey(rendezvous)))
            {
                reason =
                    $"encontro Tactical {rendezvous} fora dos " +
                    "caminhos atuais";
                return false;
            }

            action = BuildTransferReceiveBatch(
                unit,
                snapshot.AITeam,
                fromCell,
                rendezvous,
                transfer,
                paths);
            reason =
                $"{need.level} via MelhorEstoque Tactical " +
                $"{stock.reason}";
            return true;
        }

        if (result.reachDecision.Tier !=
            AIReachDecisionTier.Operational)
        {
            reason =
                $"tier {result.reachDecision.Tier} nao materializado";
            return false;
        }

        if (!TryChooseLogisticsStockProgressCell(
                unit,
                snapshot,
                fromCell,
                rendezvous,
                paths,
                occupied,
                out Vector3Int progressCell,
                out string progressReason))
        {
            reason =
                $"{need.reason} encontro Operational={rendezvous}, " +
                $"mas sem progressao segura";
            return false;
        }

        action = BuildMoveBatch(
            unit,
            snapshot.AITeam,
            fromCell,
            progressCell,
            paths);
        reason =
            $"{need.level} via MelhorEstoque Operational " +
            $"encontro={rendezvous} {progressReason} " +
            $"{stock.reason}";
        return true;
    }

    private bool TryChooseLogisticsStockProgressCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int rendezvous,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        out Vector3Int bestCell,
        out string reason)
    {
        bestCell = fromCell;
        reason = string.Empty;
        if (unit == null
            || snapshot == null
            || paths == null
            || paths.Count == 0)
            return false;

        float fromDistance =
            SectorManager.HexDistance(
                fromCell, rendezvous);
        if (TryFindBestToolProgressionCell(
                unit,
                snapshot,
                fromCell,
                rendezvous,
                paths,
                occupied,
                ToolProgressionIntent.LogisticsReload,
                out Vector3Int toolCell,
                out ToolProgressionCandidate candidate,
                out string toolReason,
                tacticalScore: (cell, value) =>
                {
                    float threat =
                        CalculateThreatLevel(
                            cell, snapshot.AITeam);
                    if (threat > 0f)
                        return float.MinValue;

                    float distance =
                        SectorManager.HexDistance(
                            cell, rendezvous);
                    float progress =
                        fromDistance - distance;
                    return progress * 650f
                        - distance * 80f
                        + GetTerrainDpqPontos(cell) * 40f
                        - value.MoveCost * 8f;
                }))
        {
            float progress =
                fromDistance
                - SectorManager.HexDistance(
                    toolCell, rendezvous);
            if (toolCell != fromCell && progress > 0f)
            {
                bestCell = toolCell;
                reason =
                    $"{toolReason} progresso={progress:0.0} " +
                    $"tool={candidate.ToolScore:0.0}";
                return true;
            }
        }

        float bestScore = float.MinValue;
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;

            float threat =
                CalculateThreatLevel(
                    cell, snapshot.AITeam);
            if (threat > 0f)
                continue;

            float distance =
                SectorManager.HexDistance(
                    cell, rendezvous);
            float progress =
                fromDistance - distance;
            if (progress <= 0f)
                continue;

            float score =
                progress * 650f
                - distance * 80f
                + GetTerrainDpqPontos(cell) * 40f
                - GetPathStepCount(paths, cell) * 8f;
            if (score <= bestScore)
                continue;
            bestScore = score;
            bestCell = cell;
        }

        if (bestCell == fromCell)
            return false;

        reason =
            $"fallback progresso=" +
            $"{fromDistance - SectorManager.HexDistance(bestCell, rendezvous):0.0} " +
            $"score={bestScore:0}";
        return true;
    }
}
