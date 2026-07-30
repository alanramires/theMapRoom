using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Avalia candidatos de hex para o Capturador via scoring multi-critério.
///
/// Fluxo:
///   1. ResolveContext  — determina (role, targetCell) único a partir do estado do mapa
///   2. ScoreHex        — calcula os 5 critérios para cada hex candidato
///   3. Evaluate        — agrega, marca o vencedor (isChosen = true)
///
/// Fórmula: total = captureProximity + combatValue + cohesion − deviation + safety
/// </summary>
public static class HexEvaluator
{
    // -----------------------------------------------------------------
    // Thresholds v1 (aprovados pelo usuário)
    // -----------------------------------------------------------------

    /// <summary>≥N aliados nas proximidades do alvo → superlotado, pula para próxima opção.</summary>
    private const int   OvercrowdingThreshold     = 2;
    /// <summary>Raio (world units) para contar aliados convergindo / superlotação.</summary>
    private const float OvercrowdingRadius        = 2.5f;
    /// <summary>Inimigo a esta distância ou menos dispara condição de Sustain.</summary>
    private const float SustainEnemyRange         = 3.5f;
    /// <summary>HP igual ou abaixo dispara condição de Sustain (aliado fraco).</summary>
    private const int   SustainAllyHpThreshold    = 4;
    /// <summary>Aliado sem companhia neste raio → isolado → dispara Sustain.</summary>
    private const float SustainIsolationRadius    = 2.5f;
    /// <summary>Raio de cohesion: aliados dentro deste raio contribuem positivamente.</summary>
    private const float CohesionRadius            = 3.5f;
    /// <summary>Raio de safety: inimigos dentro deste raio penalizam o hex.</summary>
    private const float SafetyThreatRadius        = 2.5f;
    /// <summary>Contribuicao por ponto de DPQ quando a doutrina da unidade permite DPQ. Unique(4)=+0.60.</summary>
    private const float DpqPositionWeight         = 0.15f;

    // -----------------------------------------------------------------
    // Tabela de desvio por papel (subtrai do total — fixo, não geométrico)
    // -----------------------------------------------------------------

    private static readonly Dictionary<CandidateType, float> DeviationTable =
        new Dictionary<CandidateType, float>
        {
            { CandidateType.CaptureNow,    0.00f },
            { CandidateType.CaptureAdvance,0.10f },
            { CandidateType.UsefulCombat,  0.30f },
            { CandidateType.Cohesion,      0.40f },
            { CandidateType.PrudentFow,    0.20f },
            { CandidateType.UnderRepair,   0.00f },
        };

    // -----------------------------------------------------------------
    // Contexto resolvido antes do scoring
    // -----------------------------------------------------------------

    private struct CaptureContext
    {
        public CandidateType role;
        public Vector3Int    targetCell;
        public bool          hasTarget;
    }

    // -----------------------------------------------------------------
    // Entrada pública
    // -----------------------------------------------------------------

    /// <summary>
    /// Avalia todas as células candidatas e retorna uma lista de HexEvaluation,
    /// com <see cref="HexEvaluation.isChosen"/> marcado no melhor hex.
    /// </summary>
    /// <param name="unit">Unidade que está sendo avaliada.</param>
    /// <param name="myTeam">Time da unidade.</param>
    /// <param name="fromCell">Posição atual da unidade (z=0).</param>
    /// <param name="candidateCells">Células alcançáveis e livres (freeCells do orchestrator).</param>
    /// <param name="boardTilemap">Tilemap do tabuleiro.</param>
    /// <param name="terrainDatabase">Banco de terreno.</param>
    public static List<HexEvaluation> Evaluate(
        UnitManager unit,
        TeamId myTeam,
        Vector3Int fromCell,
        List<Vector3Int> candidateCells,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        out CandidateType resolvedRole,
        out Vector3Int resolvedTarget,
        out bool resolvedHasTarget,
        TurnStateManager turnStateManager = null)
    {
        unit.TryGetUnitData(out UnitData unitData);

        // ----------------------------------------------------------
        // Curto-circuito: unidade em modo de manutenção
        //   O orchestrator desvia do fluxo normal e marcha para um
        //   prédio aliado. Não há scoring útil a fazer — exibe estado.
        // ----------------------------------------------------------
        if (unit.IsUnderRepair)
        {
            resolvedRole      = CandidateType.UnderRepair;
            resolvedTarget    = fromCell;
            resolvedHasTarget = false;

            var repairEntry = new HexEvaluation
            {
                cell          = fromCell,
                type          = CandidateType.UnderRepair,
                actionSummary = "MANUTENÇÃO — unidade vai para prédio aliado",
                combatSummary = "—",
                isChosen      = true,
            };
            return new List<HexEvaluation> { repairEntry };
        }

        // Constrói listas de posição uma vez para toda a avaliação desta unidade.
        // ResolveContext e ScoreHex reutilizam as mesmas listas em vez de reiterar AllActive
        // para cada hex candidato (O(candidatos × unidades) → O(unidades) por unidade que age).
        var alliedPos = new List<Vector3Int>();
        var enemyPos  = new List<Vector3Int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == unit || u.IsEmbarked || u.IsDead) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            if (u.TeamId == myTeam) alliedPos.Add(p);
            else                    enemyPos.Add(p);
        }

        CaptureContext ctx = ResolveContext(unit, myTeam, fromCell, boardTilemap, unitData, alliedPos, enemyPos);

        resolvedRole      = ctx.role;
        resolvedTarget    = ctx.hasTarget ? ctx.targetCell : fromCell;
        resolvedHasTarget = ctx.hasTarget;

        // Inclui fromCell (ficar parado) + todas as células alcançáveis
        var allCells = new List<Vector3Int>(candidateCells.Count + 1);
        allCells.Add(fromCell);
        foreach (Vector3Int c in candidateCells)
            if (c != fromCell) allCells.Add(c);

        var targetBuffer = new List<PodeMirarTargetOption>();
        var results      = new List<HexEvaluation>(allCells.Count);

        unit.TryGetUnitData(out UnitData unitDataForDpq);
        bool preferMoveOnBestDpq = unitDataForDpq != null && unitDataForDpq.preferMoveOnBestDPQ;
        bool prioritizeDpqAtBattle = unitDataForDpq != null && unitDataForDpq.prioritizeDpqAtBattle;
        bool suppressSafety = unitDataForDpq != null && !unitDataForDpq.playConservative;

        foreach (Vector3Int cell in allCells)
        {
            HexEvaluation eval = ScoreHex(
                unit, myTeam, cell, fromCell, ctx,
                boardTilemap, terrainDatabase, targetBuffer,
                alliedPos, enemyPos,
                preferMoveOnBestDpq, prioritizeDpqAtBattle, turnStateManager, suppressSafety);
            results.Add(eval);
        }

        // Marca o vencedor
        int   bestIdx   = 0;
        for (int i = 0; i < results.Count; i++)
        {
            if (IsBetterEvaluation(results[i], results[bestIdx], ctx, prioritizeDpqAtBattle))
            {
                bestIdx   = i;
            }
        }

        if (results.Count > 0)
        {
            HexEvaluation winner = results[bestIdx];
            winner.isChosen = true;
            results[bestIdx] = winner;
        }

        return results;
    }

    // -----------------------------------------------------------------
    // ResolveContext
    // -----------------------------------------------------------------

    private static CaptureContext ResolveContext(
        UnitManager unit, TeamId myTeam, Vector3Int fromCell, Tilemap boardTilemap, UnitData unitData,
        List<Vector3Int> alliedPos, List<Vector3Int> enemyPos)
    {
        // ----------------------------------------------------------
        // Prioridade 1: CaptureNow — já está em cima de prédio capturável
        // ----------------------------------------------------------
        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable) continue;
            Vector3Int cCell = c.CurrentCellPosition; cCell.z = 0;
            if (cCell != fromCell) continue;

            bool alreadyFullAlly = c.TeamId == myTeam && c.CurrentCapturePoints >= c.CapturePointsMax;
            if (!alreadyFullAlly)
                return new CaptureContext { role = CandidateType.CaptureNow, targetCell = cCell, hasTarget = true };
        }

        // ----------------------------------------------------------
        // Classifica prédios capturáveis — score = peso estratégico / distância
        // ----------------------------------------------------------
        ConstructionManager bestFree          = null;
        float               bestFreeScore     = float.MinValue;
        ConstructionManager bestContested     = null;
        float               bestContestedScore = float.MinValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (!c.IsCapturable) continue;

            // Ignora prédio aliado já completo
            if (c.TeamId == myTeam && c.CurrentCapturePoints >= c.CapturePointsMax) continue;

            Vector3Int cCell = c.CurrentCellPosition; cCell.z = 0;
            float dist  = Mathf.Max(0.01f, WorldDist(boardTilemap, fromCell, cCell));
            float score = GetSectorStrategicWeight(c, myTeam) / dist;

            bool hasEnemy = enemyPos.Contains(cCell);
            bool hasAlly  = alliedPos.Contains(cCell);
            if (hasAlly) continue;

            if (hasEnemy)
            {
                if (score > bestContestedScore) { bestContestedScore = score; bestContested = c; }
            }
            else
            {
                if (score > bestFreeScore) { bestFreeScore = score; bestFree = c; }
            }
        }

        // ----------------------------------------------------------
        // Prioridade 2: CaptureAdvance — prédio livre acessível e sem superlotação
        // ----------------------------------------------------------
        if (bestFree != null)
        {
            Vector3Int freeCell = bestFree.CurrentCellPosition; freeCell.z = 0;
            if (!IsOvercrowded(freeCell, alliedPos, boardTilemap))
                return new CaptureContext { role = CandidateType.CaptureAdvance, targetCell = freeCell, hasTarget = true };
        }

        // ----------------------------------------------------------
        // Prioridade 3: Contested — avança para expulsar ocupante
        //   Sem concorrência de Sustain: prédio contestado sempre vale a ação
        // ----------------------------------------------------------
        if (bestContested != null)
        {
            Vector3Int contestedCell = bestContested.CurrentCellPosition; contestedCell.z = 0;
            return new CaptureContext { role = CandidateType.CaptureAdvance, targetCell = contestedCell, hasTarget = true };
        }

        // ----------------------------------------------------------
        // Prioridade 4: Cohesion/Sustain — fallback quando não há prédio
        //   livre nem contestado disponível
        // ----------------------------------------------------------
        {
            UnitManager sustainTarget = FindSustainTarget(myTeam, enemyPos, alliedPos, boardTilemap);
            if (sustainTarget != null)
            {
                Vector3Int sustainCell = sustainTarget.CurrentCellPosition; sustainCell.z = 0;
                return new CaptureContext { role = CandidateType.Cohesion, targetCell = sustainCell, hasTarget = true };
            }
        }

        // ----------------------------------------------------------
        // Prioridade 5: PrudentFow — rush ao HQ inimigo apenas quando a
        //   unidade possui a skill oficial Captura Construcoes.
        // ----------------------------------------------------------
        bool isCapturador = PodeCapturarSensor.HasCaptureConstructionSkill(unitData);

        if (isCapturador)
        {
            ConstructionManager enemyHq = null;
            float               hqDist  = float.MaxValue;

            foreach (ConstructionManager c in ConstructionManager.AllActive)
            {
                if (c.TeamId == myTeam || !c.IsPlayerHeadQuarter) continue;
                Vector3Int hqCell = c.CurrentCellPosition; hqCell.z = 0;
                float dist = WorldDist(boardTilemap, fromCell, hqCell);
                if (dist < hqDist) { hqDist = dist; enemyHq = c; }
            }

            if (enemyHq != null)
            {
                Vector3Int hqTarget = enemyHq.CurrentCellPosition; hqTarget.z = 0;
                return new CaptureContext { role = CandidateType.PrudentFow, targetCell = hqTarget, hasTarget = true };
            }
        }

        return new CaptureContext { role = CandidateType.PrudentFow, hasTarget = false };
    }

    // -----------------------------------------------------------------
    // ScoreHex
    // -----------------------------------------------------------------

    private static HexEvaluation ScoreHex(
        UnitManager unit,
        TeamId myTeam,
        Vector3Int cell,
        Vector3Int fromCell,
        CaptureContext ctx,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        List<PodeMirarTargetOption> buffer,
        List<Vector3Int> alliedPos,
        List<Vector3Int> enemyPos,
        bool preferMoveOnBestDpq = false,
        bool prioritizeDpqAtBattle = false,
        TurnStateManager turnStateManager = null,
        bool suppressSafety = false)
    {
        bool isStaying = cell == fromCell;

        var eval = new HexEvaluation
        {
            cell = cell,
            type = ctx.role,
        };

        // ----------------------------------------------------------
        // captureProximity — 3 níveis explícitos
        // ----------------------------------------------------------
        if (ctx.hasTarget)
        {
            float distFrom   = WorldDist(boardTilemap, fromCell, ctx.targetCell);
            float distNow    = WorldDist(boardTilemap, cell,     ctx.targetCell);
            float oneHexDist = boardTilemap != null ? boardTilemap.cellSize.x * 1.2f : 1f;

            if (distFrom < 0.01f)
            {
                // CaptureNow: já estava no alvo — ficar = +1, sair = −1
                eval.captureProximity = (cell == fromCell) ? 1f : -1f;
            }
            else if (cell == ctx.targetCell)
            {
                // Chegou no prédio alvo neste hex
                eval.captureProximity = 1.0f;
            }
            else if (distNow <= oneHexDist)
            {
                // Adjacente ao alvo — pode capturar no próximo turno
                eval.captureProximity = 0.5f;
            }
            else
            {
                // Progressão linear: positivo = avança, negativo = recua
                eval.captureProximity = (distFrom - distNow) / distFrom;
            }
        }

        // ----------------------------------------------------------
        // combatValue — via PodeMirarSensor (fonte de verdade)
        // ----------------------------------------------------------
        SensorMovementMode sensorMode = isStaying
            ? SensorMovementMode.MoveuParado
            : SensorMovementMode.MoveuAndando;

        buffer.Clear();
        bool hasTargets = PodeMirarSensor.CollectTargets(
            attacker:        unit,
            boardTilemap:    boardTilemap,
            terrainDatabase: terrainDatabase,
            movementMode:    sensorMode,
            output:          buffer,
            fromCell:        cell);

        bool attackingContestedOccupant = false;
        if (hasTargets && buffer.Count > 0)
        {
            float rawValue = 0f;
            foreach (PodeMirarTargetOption opt in buffer)
            {
                float targetWeight = 0.3f;

                // Alvo em prédio contestado vale 2× — ataca e libera o objetivo ao mesmo tempo
                if (opt.targetUnit != null)
                {
                    Vector3Int targetCell = opt.targetUnit.CurrentCellPosition; targetCell.z = 0;
                    ConstructionManager targetConstruction =
                        ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, targetCell);
                    if (targetConstruction != null && targetConstruction.IsCapturable
                        && targetConstruction.TeamId != myTeam)
                    {
                        targetWeight = 0.6f;
                        attackingContestedOccupant = true;
                    }
                }

                rawValue += targetWeight;
            }
            eval.combatValue = Mathf.Min(rawValue, 1.0f);

            // Popula combatSummary com os alvos encontrados
            var sb = new System.Text.StringBuilder();
            foreach (PodeMirarTargetOption opt in buffer)
            {
                if (sb.Length > 0) sb.Append(", ");
                Vector3Int tc = opt.targetUnit != null ? opt.targetUnit.CurrentCellPosition : Vector3Int.zero;
                tc.z = 0;
                sb.Append($"{opt.targetUnit?.UnitDisplayName}#{opt.targetUnit?.InstanceId} @({tc.x},{tc.y})");
            }
            eval.combatSummary = sb.ToString();
        }
        else
        {
            eval.combatSummary = "—";
        }

        // ----------------------------------------------------------
        // positionQuality: DPQ de movimento depende de preferMoveOnBestDPQ.
        // DPQ de batalha depende de prioritizeDpqAtBattle e so entra quando
        // a celula realmente oferece alvo de ataque.
        // ----------------------------------------------------------
        bool allowDpqPosition = preferMoveOnBestDpq
            || (prioritizeDpqAtBattle && eval.combatValue > 0f);
        if (allowDpqPosition && turnStateManager != null)
        {
            int dpq = turnStateManager.GetCellDpqPoints(cell, unit);
            eval.positionQuality = dpq * DpqPositionWeight;
        }

        // ----------------------------------------------------------
        // cohesion — aliados dentro do raio contribuem positivamente
        // ----------------------------------------------------------
        float cohesionAccum = 0f;
        foreach (Vector3Int aPos in alliedPos)
        {
            float dist = WorldDist(boardTilemap, cell, aPos);
            if (dist <= CohesionRadius)
                cohesionAccum += (CohesionRadius - dist) / CohesionRadius;
        }
        // Normaliza para [0, 0.5] para não dominar sobre captureProximity
        eval.cohesion = Mathf.Min(cohesionAccum * 0.25f, 0.5f);

        // ----------------------------------------------------------
        // deviation — penalidade fixa pelo papel (não geométrica)
        // ----------------------------------------------------------
        eval.deviation = DeviationTable.TryGetValue(ctx.role, out float devPen) ? devPen : 0f;

        // ----------------------------------------------------------
        // safety — penalidade por exposição a inimigos
        //   Suprimida se combatValue > 0 e o alvo é ocupante de prédio contestado:
        //   exposição é aceitável quando a ação resolve o objetivo.
        //   Também suprimida quando prioritizeDpqAtBattle=true e a célula tem
        //   DPQ positivo com alvo de ataque: doutrina pede combate de posição
        //   privilegiada — aceita exposição.
        // ----------------------------------------------------------
        bool suppressedByDpqCombat = prioritizeDpqAtBattle && eval.positionQuality > 0f && eval.combatValue > 0f;
        if (suppressSafety
            || (attackingContestedOccupant || suppressedByDpqCombat) && eval.combatValue > 0f)
        {
            eval.safety = 0f;
        }
        else
        {
            float exposureAccum = 0f;
            foreach (Vector3Int ePos in enemyPos)
            {
                float dist = WorldDist(boardTilemap, cell, ePos);
                if (dist <= SafetyThreatRadius)
                    exposureAccum += (SafetyThreatRadius - dist) / SafetyThreatRadius;
            }
            eval.safety = -Mathf.Min(exposureAccum, 2.0f);
        }

        // ----------------------------------------------------------
        // total
        // ----------------------------------------------------------
        eval.total = eval.captureProximity
                   + eval.combatValue
                   + eval.positionQuality
                   + eval.cohesion
                   - eval.deviation
                   + eval.safety;

        // ----------------------------------------------------------
        // actionSummary — ação real, não apenas contexto
        // ----------------------------------------------------------
        if (attackingContestedOccupant && eval.combatValue > 0f)
        {
            // Encontra o alvo contestado principal para nomear a ação
            string targetName = "?";
            foreach (PodeMirarTargetOption opt in buffer)
            {
                if (opt.targetUnit == null) continue;
                Vector3Int tc = opt.targetUnit.CurrentCellPosition; tc.z = 0;
                ConstructionManager cc = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, tc);
                if (cc != null && cc.IsCapturable && cc.TeamId != myTeam)
                {
                    targetName = $"{opt.targetUnit.UnitDisplayName}#{opt.targetUnit.InstanceId}";
                    break;
                }
            }
            string moveStr = isStaying ? "parado" : $"→ {cell}";
            eval.actionSummary = $"attack {targetName} ({moveStr})";
        }
        else if (isStaying && ctx.role == CandidateType.CaptureNow)
            eval.actionSummary = $"capture @ {cell}";
        else if (isStaying)
            eval.actionSummary = $"stay @ {cell}";
        else if (ctx.role == CandidateType.CaptureAdvance)
            eval.actionSummary = $"advance → {cell} (→ {ctx.targetCell})";
        else if (ctx.role == CandidateType.Cohesion)
            eval.actionSummary = $"sustain → {cell} (→ {ctx.targetCell})";
        else
            eval.actionSummary = $"move → {cell} [{ctx.role}]";

        return eval;
    }

    // -----------------------------------------------------------------
    // Helpers privados
    // -----------------------------------------------------------------

    private static bool IsOvercrowded(
        Vector3Int targetCell, List<Vector3Int> alliedPos, Tilemap boardTilemap)
    {
        int count = 0;
        foreach (Vector3Int pos in alliedPos)
        {
            if (WorldDist(boardTilemap, pos, targetCell) <= OvercrowdingRadius)
                count++;
        }
        return count >= OvercrowdingThreshold;
    }

    private static bool HasAllyConverging(
        Vector3Int targetCell, List<Vector3Int> alliedPos, Tilemap boardTilemap)
    {
        foreach (Vector3Int pos in alliedPos)
        {
            if (WorldDist(boardTilemap, pos, targetCell) <= OvercrowdingRadius)
                return true;
        }
        return false;
    }

    private static UnitManager FindSustainTarget(
        TeamId myTeam,
        List<Vector3Int> enemyPos,
        List<Vector3Int> alliedPos,
        Tilemap boardTilemap)
    {
        foreach (UnitManager ally in UnitManager.AllActive)
        {
            if (ally.IsEmbarked || ally.IsDead || ally.TeamId != myTeam) continue;

            Vector3Int allyCell = ally.CurrentCellPosition; allyCell.z = 0;

            // Condição 1: inimigo ≤ SustainEnemyRange hexes
            bool enemyNear = false;
            foreach (Vector3Int ep in enemyPos)
            {
                if (WorldDist(boardTilemap, allyCell, ep) <= SustainEnemyRange)
                {
                    enemyNear = true;
                    break;
                }
            }

            // Condição 2: HP baixo
            bool lowHp = ally.CurrentHP <= SustainAllyHpThreshold;

            // Condição 3: isolado (sem outro aliado dentro de SustainIsolationRadius)
            bool isolated = true;
            foreach (Vector3Int ap in alliedPos)
            {
                if (ap == allyCell) continue;
                if (WorldDist(boardTilemap, allyCell, ap) <= SustainIsolationRadius)
                {
                    isolated = false;
                    break;
                }
            }

            if (enemyNear || lowHp || isolated)
                return ally;
        }
        return null;
    }

    private static float WorldDist(Tilemap map, Vector3Int a, Vector3Int b)
    {
        if (map == null) return 0f;
        Vector3 wa = map.GetCellCenterWorld(a);
        Vector3 wb = map.GetCellCenterWorld(b);
        return Vector2.Distance(wa, wb);
    }

    private static bool IsBetterEvaluation(
        HexEvaluation candidate,
        HexEvaluation current,
        CaptureContext ctx,
        bool prioritizeDpqAtBattle)
    {
        if (ctx.role == CandidateType.CaptureAdvance && ctx.hasTarget)
        {
            bool candidateHasCombat = candidate.combatValue > 0f;
            bool currentHasCombat = current.combatValue > 0f;
            if (candidateHasCombat != currentHasCombat)
                return candidateHasCombat;

            if (prioritizeDpqAtBattle && candidateHasCombat)
            {
                const float DpqEpsilon = 0.0001f;
                if (Mathf.Abs(candidate.positionQuality - current.positionQuality) > DpqEpsilon)
                    return candidate.positionQuality > current.positionQuality;
            }

            if (!candidateHasCombat)
            {
                const float ProgressEpsilon = 0.0001f;
                bool candidateAdvances = candidate.captureProximity > 0f;
                bool currentAdvances = current.captureProximity > 0f;
                if (candidateAdvances != currentAdvances)
                    return candidateAdvances;

                if (candidateAdvances && currentAdvances
                    && Mathf.Abs(candidate.captureProximity - current.captureProximity) > ProgressEpsilon)
                    return candidate.captureProximity > current.captureProximity;
            }
        }

        return candidate.total > current.total;
    }

    // -----------------------------------------------------------------
    // Peso estratégico do prédio para a IA (por tipo de construção)
    //   3.0 — HQ inimigo          → alvo de vitória
    //   2.0 — Fábrica             → produção de unidades
    //   1.5 — Prédio com renda    → geração de fundos
    //   0.5 — Prédio aliado completo → baixa prioridade
    //   1.0 — default
    // -----------------------------------------------------------------
    private static float GetSectorStrategicWeight(ConstructionManager building, TeamId myTeam)
    {
        if (building == null) return 1f;

        if (building.IsPlayerHeadQuarter && building.TeamId != myTeam)
            return 3.0f;

        if (building.CanProduceUnits)
            return 2.0f;

        if (building.CapturedIncoming > 0)
            return 1.5f;

        if (building.TeamId == myTeam && building.CurrentCapturePoints >= building.CapturePointsMax)
            return 0.5f;

        return 1.0f;
    }
}
