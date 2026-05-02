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
            UnitManager stayBest = PickBestRogueTarget(stayTargets, snapshot.AITeam);
            if (stayBest != null)
            {
                Vector3Int stCell = stayBest.CurrentCellPosition; stCell.z = 0;
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
            if (TryFindOpportunisticCapture(unit, engagePaths, engageOccupied, target, out Vector3Int engageOpCell))
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
                UnitManager candidate = PickBestRogueTarget(engageBuffer, snapshot.AITeam);
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
        if (TryFindOpportunisticCapture(unit, paths, occupied, target, out Vector3Int opCell))
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

        // Avança para o hex mais próximo do HQ
        Vector3Int best     = from;
        float      bestDist = SectorManager.HexDistance(from, target);
        foreach (Vector3Int cell in paths.Keys)
        {
            if (occupied.Contains(cell)) continue;
            float dist = SectorManager.HexDistance(cell, target);
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
