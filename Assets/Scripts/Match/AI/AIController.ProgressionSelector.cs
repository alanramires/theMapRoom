using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public partial class AIController
{
    private enum ToolProgressionIntent
    {
        CaptureAdvance,
        AssaultPressure,
        FireSupportReposition,
        FireSupportRendezvous,
        LogisticsService,
        LogisticsReload,
        RepairReturn,
        TransportDelivery,
        TransportRendezvous,
        ObserverSpot
    }

    private struct ToolProgressionCandidate
    {
        public Vector3Int Cell;
        public int ToolScore;
        public float NextDistance;
        public int MoveCost;
        public bool RoadBonus;
        public float FirstTurnProgress;
        public float TwoTurnProgress;
        public float LineDeviation;
        public float Threat;
        public float Dpq;
        public float TacticalScore;
        public float FinalScore;
    }

    private bool TryFindBestToolProgressionCell(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        Vector3Int targetCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        HashSet<Vector3Int> occupied,
        ToolProgressionIntent intent,
        out Vector3Int bestCell,
        out ToolProgressionCandidate bestCandidate,
        out string reason,
        Func<Vector3Int, bool> allowCell = null,
        Func<Vector3Int, ToolProgressionCandidate, float> tacticalScore = null)
    {
        bestCell = fromCell;
        bestCandidate = new ToolProgressionCandidate();
        reason = "";

        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        fromCell.z = 0;
        targetCell.z = 0;

        float originDistance = SectorManager.HexDistance(fromCell, targetCell);
        float bestScore = float.MinValue;
        bool found = false;
        bool debugTransportProgression = intent == ToolProgressionIntent.TransportDelivery
            || intent == ToolProgressionIntent.TransportRendezvous;
        int skipOrigin = 0;
        int skipOccupied = 0;
        int skipStopCell = 0;
        int skipAllow = 0;
        int skipScore = 0;
        List<ToolProgressionCandidate> debugCandidates = debugTransportProgression
            ? new List<ToolProgressionCandidate>()
            : null;

        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
            {
                skipOrigin++;
                continue;
            }
            if (occupied != null && occupied.Contains(cell))
            {
                skipOccupied++;
                continue;
            }
            if (!CanUseAsToolProgressStopCell(unit, cell, fromCell))
            {
                skipStopCell++;
                continue;
            }
            if (allowCell != null && !allowCell(cell))
            {
                skipAllow++;
                continue;
            }

            if (!paths.TryGetValue(rawCell, out List<Vector3Int> path))
                path = null;

            if (!TryScoreToolRouteProgression(
                    unit,
                    fromCell,
                    targetCell,
                    cell,
                    path,
                    occupied,
                    out int toolScore,
                    out float nextDistance,
                    out int moveCost))
            {
                skipScore++;
                continue;
            }

            bool roadBonus = path != null
                && UnitMovementPathRules.DidUseRoadFullMoveBonus(boardTilemap, unit, path, terrainDatabase);

            float firstTurnProgress = originDistance - SectorManager.HexDistance(cell, targetCell);
            float twoTurnProgress = originDistance - nextDistance;
            float lineDeviation = DistanceFromHexLine(cell, fromCell, targetCell);
            float threat = CalculateThreatLevel(cell, snapshot.AITeam);
            float dpq = GetTerrainDpqPontos(cell);

            var candidate = new ToolProgressionCandidate
            {
                Cell = cell,
                ToolScore = toolScore,
                NextDistance = nextDistance,
                MoveCost = moveCost,
                RoadBonus = roadBonus,
                FirstTurnProgress = firstTurnProgress,
                TwoTurnProgress = twoTurnProgress,
                LineDeviation = lineDeviation,
                Threat = threat,
                Dpq = dpq
            };

            float intentScore = ScoreToolProgressionIntent(intent, candidate);
            float extraScore = tacticalScore != null ? tacticalScore(cell, candidate) : 0f;
            candidate.TacticalScore = extraScore;
            candidate.FinalScore = intentScore + extraScore;
            debugCandidates?.Add(candidate);

            if (candidate.FinalScore > bestScore)
            {
                bestScore = candidate.FinalScore;
                bestCell = cell;
                bestCandidate = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        reason = FormatToolProgressionReason(intent, bestCandidate);
        if (debugTransportProgression)
            LogToolProgressionCandidates(unit, intent, fromCell, targetCell, bestCandidate, debugCandidates,
                skipOrigin, skipOccupied, skipStopCell, skipAllow, skipScore);
        return true;
    }

    private static void LogToolProgressionCandidates(
        UnitManager unit,
        ToolProgressionIntent intent,
        Vector3Int fromCell,
        Vector3Int targetCell,
        ToolProgressionCandidate bestCandidate,
        List<ToolProgressionCandidate> candidates,
        int skipOrigin,
        int skipOccupied,
        int skipStopCell,
        int skipAllow,
        int skipScore)
    {
        if (unit == null || candidates == null || candidates.Count == 0)
            return;

        candidates.Sort((a, b) => b.FinalScore.CompareTo(a.FinalScore));
        int limit = Mathf.Min(12, candidates.Count);
        var sb = new StringBuilder(768);
        sb.Append($"[AI][Progressao2][Top] unit={unit.InstanceId} intent={intent} from={fromCell} target={targetCell} ");
        sb.Append($"best={bestCandidate.Cell} final={bestCandidate.FinalScore:F0} ");
        sb.Append($"candidatos={candidates.Count} skips origin={skipOrigin} occupied={skipOccupied} stop={skipStopCell} allow={skipAllow} score={skipScore}");

        for (int i = 0; i < limit; i++)
        {
            ToolProgressionCandidate c = candidates[i];
            sb.AppendLine();
            sb.Append("  #").Append(i + 1).Append(' ')
                .Append(c.Cell)
                .Append(" final=").Append(c.FinalScore.ToString("F0"))
                .Append(" tool=").Append(c.ToolScore)
                .Append(" next=").Append(c.NextDistance.ToString("F1"))
                .Append(" move=").Append(c.MoveCost)
                .Append(" road=").Append(c.RoadBonus)
                .Append(" prog=").Append(c.FirstTurnProgress.ToString("F1")).Append('/').Append(c.TwoTurnProgress.ToString("F1"))
                .Append(" line=").Append(c.LineDeviation.ToString("F1"))
                .Append(" threat=").Append(c.Threat.ToString("F1"))
                .Append(" dpq=").Append(c.Dpq.ToString("F1"))
                .Append(" tactical=").Append(c.TacticalScore.ToString("F0"));
        }

        Debug.Log(sb.ToString());
    }

    private static float ScoreToolProgressionIntent(
        ToolProgressionIntent intent,
        ToolProgressionCandidate candidate)
    {
        float score = candidate.ToolScore * 1000f
            + candidate.TwoTurnProgress * 220f
            + Mathf.Max(0f, candidate.FirstTurnProgress) * 90f
            - candidate.LineDeviation * 80f
            - candidate.MoveCost * 18f
            + (candidate.RoadBonus ? 650f : 0f);

        switch (intent)
        {
            case ToolProgressionIntent.FireSupportReposition:
            case ToolProgressionIntent.FireSupportRendezvous:
                score += candidate.Dpq * 35f;
                score -= candidate.Threat * 80f;
                break;
            case ToolProgressionIntent.LogisticsService:
            case ToolProgressionIntent.LogisticsReload:
                score += candidate.Dpq * 30f;
                score -= candidate.Threat * 120f;
                break;
            case ToolProgressionIntent.RepairReturn:
                score -= candidate.Threat * 60f;
                break;
            case ToolProgressionIntent.TransportDelivery:
            case ToolProgressionIntent.TransportRendezvous:
                score -= candidate.Threat * 50f;
                break;
            case ToolProgressionIntent.ObserverSpot:
                score += candidate.Dpq * 45f;
                score -= candidate.Threat * 90f;
                break;
            case ToolProgressionIntent.AssaultPressure:
            case ToolProgressionIntent.CaptureAdvance:
            default:
                score += candidate.Dpq * 20f;
                score -= candidate.Threat * 35f;
                break;
        }

        return score;
    }

    private static string FormatToolProgressionReason(
        ToolProgressionIntent intent,
        ToolProgressionCandidate candidate)
    {
        return $"toolIntent={intent} tool={candidate.ToolScore} next={candidate.NextDistance:F1} " +
               $"moveCost={candidate.MoveCost} roadBonus={candidate.RoadBonus} " +
               $"prog={candidate.FirstTurnProgress:F1}/{candidate.TwoTurnProgress:F1} " +
               $"line={candidate.LineDeviation:F1} dpq={candidate.Dpq:F1} threat={candidate.Threat:F1} " +
               $"tactical={candidate.TacticalScore:F0} final={candidate.FinalScore:F0}";
    }
}
