using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Capturador Rogue — captura HQ e construções no caminho, engaja para abrir passagem
    // -------------------------------------------------------------------------

    private PlayerAction DecideRogueCapturerAction(UnitManager unit, AIWorldSnapshot snapshot)
    {
        Vector3Int from   = unit.CurrentCellPosition; from.z = 0;
        Vector3Int target = snapshot.EnemyHQ.CurrentCellPosition; target.z = 0;

        // Rogue abre caminho: ataca diretamente — não delega ao HexEvaluator (que evitaria o combate)
        if (HasAttackTargetAtCurrentPos(unit))
        {
            var stayTargets = new List<PodeMirarTargetOption>();
            PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                SensorMovementMode.MoveuParado, stayTargets);
            UnitManager stayBest = PickBestRogueTarget(stayTargets, snapshot.AITeam, unit, from, false, out _);
            if (stayBest != null)
            {
                Vector3Int stCell = stayBest.CurrentCellPosition; stCell.z = 0;
                if (unit.TryGetUnitData(out UnitData rogueData)
                    && rogueData != null
                    && rogueData.prioritizeDpqAtBattle)
                {
                    Dictionary<Vector3Int, List<Vector3Int>> dpqPaths =
                        UnitMovementPathRules.CalcularCaminhosValidos(
                            boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
                    HashSet<Vector3Int> dpqOccupied = BuildOccupied(unit);

                    if (TryFindBetterDpqAttackCellForTarget(unit, snapshot.AITeam, from, stayBest, dpqPaths, dpqOccupied,
                            out Vector3Int dpqAttackCell, out string dpqReason))
                    {
                        Debug.Log($"{TL("Rogue")} {unit.InstanceId} reposiciona DPQ e ataca {stayBest.UnitDisplayName}#{stayBest.InstanceId} via {dpqAttackCell} ({dpqReason})");
                        return BuildAttackBatch(unit, snapshot.AITeam, from, dpqAttackCell,
                            stayBest.InstanceId.ToString(), stCell, dpqPaths);
                    }
                }
                Debug.Log($"{TL("Rogue")} {unit.InstanceId} ataca {stayBest.UnitDisplayName}#{stayBest.InstanceId} da posição atual");
                return BuildAttackBatch(unit, snapshot.AITeam, from, from,
                    stayBest.InstanceId.ToString(), stCell);
            }
        }

        if (HasEnemyInEngageRadius(unit, from, snapshot.AITeam))
        {
            Dictionary<Vector3Int, List<Vector3Int>> engagePaths =
                UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
            HashSet<Vector3Int> engageOccupied = BuildOccupied(unit);

            // Captura oportunista tem prioridade sobre o combate: prédio disponível é mais
            // valioso do que eliminar um inimigo que não bloqueia o caminho.
            if (TryFindUnreservedOpportunisticCapture(unit, snapshot.AITeam, engagePaths, engageOccupied, target, out Vector3Int engageOpCell, "rogue combate"))
            {
                Debug.Log($"{TL("Rogue")} {unit.InstanceId} captura oportunista (inimigos no raio) @ {engageOpCell}");
                return BuildCaptureBatch(unit, snapshot.AITeam, from, engageOpCell, engagePaths);
            }

            // Sem captura disponível → abre caminho por combate
            var engageBuffer = new List<PodeMirarTargetOption>();
            foreach (Vector3Int cell in engagePaths.Keys)
            {
                if (engageOccupied.Contains(cell)) continue;
                engageBuffer.Clear();
                PodeMirarSensor.CollectTargets(unit, boardTilemap, terrainDatabase,
                    SensorMovementMode.MoveuAndando, engageBuffer, fromCell: cell);
                UnitManager candidate = PickBestRogueTarget(engageBuffer, snapshot.AITeam, unit, cell, false, out _);
                if (candidate != null)
                {
                    Vector3Int btCell = candidate.CurrentCellPosition; btCell.z = 0;
                    Debug.Log($"{TL("Rogue")} {unit.InstanceId} move+ataca {candidate.UnitDisplayName}#{candidate.InstanceId} via {cell}");
                    return BuildAttackBatch(unit, snapshot.AITeam, from, cell,
                        candidate.InstanceId.ToString(), btCell, engagePaths);
                }
            }

            return null; // inimigos próximos, sem captura nem ataque → HexEvaluator
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);

        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, from, from);

        // HQ alcançável → captura ou entra
        if (paths.ContainsKey(target) && !occupied.Contains(target))
        {
            if (SimulateCaptureSensor(unit, target, out _))
                return BuildCaptureBatch(unit, snapshot.AITeam, from, target, paths);
            return BuildMoveBatch(unit, snapshot.AITeam, from, target, paths);
        }

        // Captura oportunista: qualquer prédio capturável no caminho ao HQ
        if (TryFindUnreservedOpportunisticCapture(unit, snapshot.AITeam, paths, occupied, target, out Vector3Int opCell, "rogue"))
        {
            Debug.Log($"{TL("Rogue")} {unit.InstanceId} captura oportunista @ {opCell}");
            return BuildCaptureBatch(unit, snapshot.AITeam, from, opCell, paths);
        }

        // FoW passinho: HQ tem ocupante invisível → sobe no DPQ mais elevado adjacente
        {
            UnitManager hqOccupant = HexOccupancyQuery.FindUnitAtCell(target);
            if (hqOccupant != null && hqOccupant.TeamId != snapshot.AITeam)
            {
                MatchController mc = GetMatchController();
                if (mc == null || !mc.IsUnitVisibleForTeam(hqOccupant, snapshot.AITeam))
                {
                    if (TryFindBestLoSCell(unit, paths, occupied, target, out Vector3Int dpqCell))
                    {
                        Debug.Log($"{TL("FoW")} {unit.InstanceId} DPQ para revelar HQ via {dpqCell} (ev={GetTerrainEv(dpqCell):F0})");
                        return BuildMoveBatch(unit, snapshot.AITeam, from, dpqCell, paths);
                    }
                }
            }
        }

        // Move+ataca: inimigo visível (AllActive + FoW) alcançável de célula que avança ao HQ
        {
            float fromDistHQ = SectorManager.HexDistance(from, target);
            bool fromRouteFound = TryCalculateRouteDistance(unit, from, target, out float fromRouteDist);
            MatchController mcAdv = GetMatchController();
            UnitManager advAttackTarget = null;
            Vector3Int  advAttackCell   = from;
            float       advAttackPri    = float.MinValue;
            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy.TeamId == snapshot.AITeam || enemy.IsDead || enemy.IsEmbarked) continue;
                if (mcAdv != null && !mcAdv.IsUnitVisibleForTeam(enemy, snapshot.AITeam)) continue;
                foreach (Vector3Int cell in paths.Keys)
                {
                    if (occupied.Contains(cell)) continue;
                    float cellDist = SectorManager.HexDistance(cell, target);
                    bool cellRouteFound = TryCalculateRouteDistance(unit, cell, target, out float cellRouteDist);
                    float routeProgress = fromRouteFound && cellRouteFound ? fromRouteDist - cellRouteDist : 0f;
                    bool advances = (!fromRouteFound && cellRouteFound)
                        || routeProgress > 0f
                        || (!fromRouteFound && !cellRouteFound && cellDist < fromDistHQ);
                    if (!advances) continue;
                    if (!CanAttackTargetFrom(from, cell, unit, enemy)) continue;
                    if (!PassesAttackDecision(unit, enemy, cell, false, out _)) continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    float pri = -SectorManager.HexDistance(ec, target); // prefere inimigo mais perto do HQ
                    if (pri > advAttackPri) { advAttackPri = pri; advAttackTarget = enemy; advAttackCell = cell; }
                    break;
                }
            }
            if (advAttackTarget != null)
            {
                Vector3Int atCell = advAttackTarget.CurrentCellPosition; atCell.z = 0;
                Debug.Log($"{TL("Rogue")} {unit.InstanceId} move+ataca {advAttackTarget.UnitDisplayName}#{advAttackTarget.InstanceId} via {advAttackCell} (no avanço ao HQ)");
                return BuildAttackBatch(unit, snapshot.AITeam, from, advAttackCell,
                    advAttackTarget.InstanceId.ToString(), atCell, paths);
            }
        }

        // Avança para o hex mais próximo do HQ
        Vector3Int best     = from;
        float      bestDist = CalculateRouteDistanceOrHex(unit, from, target);
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            float dist = CalculateRouteDistanceOrHex(unit, cell, target);
            if (IsBetterRogueAdvance(from, target, cell, dist, best, bestDist))
            {
                bestDist = dist;
                best = cell;
            }
        }

        Debug.Log($"{TL("Rogue")} {unit.InstanceId} marcha para HQ inimigo via {best}");
        return BuildMoveBatch(unit, snapshot.AITeam, from, best, paths);
    }
}
