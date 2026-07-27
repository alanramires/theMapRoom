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
        StockNetwork,
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
        public bool RouteFound;
        public float RouteDistance;
        public float RouteProgress;
        public float LineDeviation;
        public float Threat;
        public float Dpq;
        public float TacticalScore;
        public float FinalScore;
    }

    // Quantos candidatos (rankeados por progresso de 1º turno) recebem a 2ª jogada cara do
    // two-turn (CalcularCaminhosValidos por candidato). O resto e pontuado so pelo 1º turno.
    // Limita o pathfind de mar (naval) que explodia para dezenas de chamadas por decisao.
    private const int TwoTurnLookaheadCandidateCap = 12;

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
        using var perf = new AIDecisionPerfScope(unit, $"toolProgression.{intent}");
        bestCell = fromCell;
        bestCandidate = new ToolProgressionCandidate();
        reason = "";

        if (unit == null || snapshot == null || paths == null || paths.Count == 0)
            return false;

        fromCell.z = 0;
        targetCell.z = 0;

        float originDistance = CalculateRouteDistanceOrHex(unit, fromCell, targetCell);
        bool originRouteFound = TryCalculateRouteDistance(unit, fromCell, targetCell, out float originRouteDistance);
        float threatScale = ResolveProgressionThreatScale(unit);

        // Alcancabilidade/custo do 1º passo (a partir da posicao atual) e a mesma para todos os
        // candidatos: calcula UMA vez e reusa. Antes o TryScoreToolRouteProgression a refazia por
        // candidato (BFS caro em naval sobre mar aberto).
        Dictionary<Vector3Int, int> costFromOrigin = UnitMovementPathRules.CalculateMovementCostMap(
            boardTilemap, unit, fromCell, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        // Mapa reverso de distancia ATE o target: 1 busca, depois lookups no loop interno do
        // two-turn (antes: N buscas ponto-a-ponto por candidato — o custo do naval). Mesma metrica
        // (SectorManager), bit-a-bit igual. Aereo usa distancia-hexa direta, entao nao precisa de mapa.
        Dictionary<Vector3Int, int> distanceToTargetMap = null;
        if (unit.GetDomain() != Domain.Air
            && unit.TryGetUnitData(out UnitData progressionUnitData)
            && progressionUnitData != null)
        {
            SectorManager.TryBuildLandMovementDistanceToTargetMap(
                targetCell, progressionUnitData, out distanceToTargetMap);
        }
        float bestScore = float.MinValue;
        bool found = false;
        bool debugTransportProgression = intent == ToolProgressionIntent.TransportDelivery
            || intent == ToolProgressionIntent.TransportRendezvous
            || intent == ToolProgressionIntent.AssaultPressure;
        int skipOrigin = 0;
        int skipOccupied = 0;
        int skipStopCell = 0;
        int skipAllow = 0;
        int skipScore = 0;
        List<ToolProgressionCandidate> debugCandidates = debugTransportProgression
            ? new List<ToolProgressionCandidate>()
            : null;

        // Pass 1 — filtra candidatos validos e o proxy barato de 1º turno (routeDistance
        // memoizado). A 2ª jogada (CalcularCaminhosValidos por candidato) e cara em naval, entao
        // so os top-K por esse proxy a recebem.
        var progressionCandidates = new List<(Vector3Int cell, List<Vector3Int> path, float firstTurnProgress)>();
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

            float firstTurnProgress = originDistance - CalculateRouteDistanceOrHex(unit, cell, targetCell);
            progressionCandidates.Add((cell, path, firstTurnProgress));
        }

        // Top-K por progresso de 1º turno (desempate por celula = ordem determinista) recebem a
        // 2ª jogada; os demais sao pontuados so pelo 1º turno.
        var twoTurnCells = new HashSet<Vector3Int>();
        if (progressionCandidates.Count > TwoTurnLookaheadCandidateCap)
        {
            var ranked = new List<(Vector3Int cell, List<Vector3Int> path, float firstTurnProgress)>(progressionCandidates);
            ranked.Sort((a, b) =>
            {
                int cmp = b.firstTurnProgress.CompareTo(a.firstTurnProgress);
                if (cmp != 0) return cmp;
                cmp = a.cell.x.CompareTo(b.cell.x);
                return cmp != 0 ? cmp : a.cell.y.CompareTo(b.cell.y);
            });
            for (int i = 0; i < TwoTurnLookaheadCandidateCap; i++)
                twoTurnCells.Add(ranked[i].cell);
        }
        else
        {
            for (int i = 0; i < progressionCandidates.Count; i++)
                twoTurnCells.Add(progressionCandidates[i].cell);
        }

        // Pass 2 — pontua em ordem original (determinismo da selecao).
        for (int idx = 0; idx < progressionCandidates.Count; idx++)
        {
            Vector3Int cell = progressionCandidates[idx].cell;
            List<Vector3Int> path = progressionCandidates[idx].path;
            bool evaluateSecondMove = twoTurnCells.Contains(cell);

            if (!TryScoreToolRouteProgression(
                    unit,
                    fromCell,
                    targetCell,
                    cell,
                    path,
                    occupied,
                    out int toolScore,
                    out float nextDistance,
                    out int moveCost,
                    costFromOrigin,
                    evaluateSecondMove,
                    distanceToTargetMap))
            {
                skipScore++;
                continue;
            }

            bool roadBonus = path != null
                && UnitMovementPathRules.DidUseRoadFullMoveBonus(boardTilemap, unit, path, terrainDatabase);

            float firstTurnProgress = progressionCandidates[idx].firstTurnProgress;
            float twoTurnProgress = originDistance - nextDistance;
            bool cellRouteFound = TryCalculateRouteDistance(unit, cell, targetCell, out float cellRouteDistance);
            float routeProgress = originRouteFound && cellRouteFound ? originRouteDistance - cellRouteDistance : 0f;
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
                RouteFound = cellRouteFound,
                RouteDistance = cellRouteFound ? cellRouteDistance : float.MaxValue,
                RouteProgress = routeProgress,
                LineDeviation = lineDeviation,
                Threat = threat,
                Dpq = dpq
            };

            float intentScore = ScoreToolProgressionIntent(intent, candidate, threatScale);
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
                .Append(" route=").Append(c.RouteFound ? c.RouteDistance.ToString("F1") : "?")
                .Append(" progR=").Append(c.RouteFound ? c.RouteProgress.ToString("+0.0;-0.0;0.0") : "?")
                .Append(" line=").Append(c.LineDeviation.ToString("F1"))
                .Append(" threat=").Append(c.Threat.ToString("F1"))
                .Append(" dpq=").Append(c.Dpq.ToString("F1"))
                .Append(" tactical=").Append(c.TacticalScore.ToString("F0"));
        }

        Debug.Log(sb.ToString());
    }

    // Prudência (peso de threat) por unidade: um APC vazio que também satisfaz Assalto não precisa
    // se preocupar com perigo (no passado transitava entre Transportador e Assalto-quando-vazio).
    // O caminhão de suprimentos NÃO se aplica: é Logística, não satisfaz Assalto, então mantém 1.
    private static float ResolveProgressionThreatScale(UnitManager unit)
    {
        if (unit == null || !unit.TryGetUnitData(out UnitData data) || data == null)
            return 1f;

        bool combatTransport = UnitRoleCompatibility.CanSatisfy(data, UnitRole.Transportador)
                            && UnitRoleCompatibility.CanSatisfy(data, UnitRole.Assalto);
        if (combatTransport && CollectPassengers(unit).Count == 0)
            return 0f;

        return 1f;
    }

    private static float ScoreToolProgressionIntent(
        ToolProgressionIntent intent,
        ToolProgressionCandidate candidate,
        float threatScale = 1f)
    {
        // base = toolScore (já contém 2T/1T/line) amplificado. Não re-somamos progresso aqui (era
        // contagem dupla) e mv foi removido; só road/dpq/threat/route entram à parte. Espelha
        // CaminhosValidosWindow.ScoreProgressIntent.
        // threatScale: 0 zera a prudência (ex.: APC vazio que vira assalto); 1 mantém.
        float score = candidate.ToolScore * 1000f
            + (candidate.RoadBonus ? 650f : 0f);
        float threat = candidate.Threat * threatScale;

        switch (intent)
        {
            case ToolProgressionIntent.FireSupportReposition:
            case ToolProgressionIntent.FireSupportRendezvous:
                score += candidate.Dpq * 35f;
                score -= threat * 80f;
                break;
            case ToolProgressionIntent.LogisticsService:
            case ToolProgressionIntent.LogisticsReload:
            case ToolProgressionIntent.StockNetwork:
                score += candidate.Dpq * 30f;
                score -= threat * 120f;
                break;
            case ToolProgressionIntent.RepairReturn:
                score -= threat * 60f;
                break;
            case ToolProgressionIntent.TransportDelivery:
            case ToolProgressionIntent.TransportRendezvous:
                score -= threat * 50f;
                break;
            case ToolProgressionIntent.ObserverSpot:
                score += candidate.Dpq * 45f;
                score -= threat * 90f;
                break;
            case ToolProgressionIntent.AssaultPressure:
                if (candidate.RouteFound)
                {
                    score += candidate.RouteProgress * 2500f;
                    score -= candidate.RouteDistance * 80f;
                }
                score += candidate.Dpq * 20f;
                score -= threat * 35f;
                break;
            case ToolProgressionIntent.CaptureAdvance:
            default:
                score += candidate.Dpq * 20f;
                score -= threat * 35f;
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
               $"route={(candidate.RouteFound ? candidate.RouteDistance.ToString("F1") : "?")} " +
               $"progR={(candidate.RouteFound ? candidate.RouteProgress.ToString("+0.0;-0.0;0.0") : "?")} " +
               $"line={candidate.LineDeviation:F1} dpq={candidate.Dpq:F1} threat={candidate.Threat:F1} " +
               $"tactical={candidate.TacticalScore:F0} final={candidate.FinalScore:F0}";
    }
}
