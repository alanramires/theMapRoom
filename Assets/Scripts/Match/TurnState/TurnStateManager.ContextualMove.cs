using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// AÇÃO DIRETA a partir da seleção (atalho contextual): com a unidade selecionada, tocar
/// num INIMIGO ou num TRANSPORTE aliado move a unidade, provisoriamente, até uma célula
/// de onde a ação é legal e já abre o prompt mirando aquele alvo. Nada é definitivo:
/// a confirmação continua sendo o segundo toque, e cancelar volta tudo (contrato
/// transacional — docs/arquitetura/acoes_transacionais.md).
///
/// Alvos: INIMIGO -> atirar. ALIADO -> embarcar, fundir (só se a soma do HP couber em
/// 10: o atalho não desperdiça vida) ou suprir — a mesma ordem do atalho pós-movimento.
/// Desembarque fica de fora (o toque já é movimento), captura já tem o segundo toque na
/// própria unidade, e transferir é o "capturar" do supridor.
///
/// Célula de chegada, entre as alcançáveis onde se pode parar e de onde o sensor aceita:
///   ATAQUE
///     1. o melhor DPQ (PositionDpqResolver, a régua do combate) — o atalho ensina:
///        o jogador vê o soldado subir o morro e lê no helper por quê;
///     2. no empate, onde o cursor já estava (no toque, a própria unidade);
///     3. depois, a mais barata. MP não importa depois de atirar — a unidade já agiu.
///   EMBARQUE, FUSÃO, SUPRIR
///     1. onde o cursor já estava; 2. a mais barata — no embarque e na fusão o MP
///     que sobra paga a entrada no hex do parceiro.
/// Quem quer atirar de um lugar específico não toca no inimigo: toca na célula,
/// anda, e de lá mira (o atalho pós-movimento que já existia).
///
/// Quem decide se dá é o SENSOR, a partir da célula projetada, sem mover ninguém:
/// PodeMirarSensor(fromCell) para o ataque, PodeEmbarcarSensor.CanEmbarkFromProjectedCell
/// para o embarque. Mexer na posição da unidade para "simular" dispararia eventos de
/// ocupação no meio da ação — o modo de falha do CLAUDE.md ("O mundo só recalcula no
/// Neutral").
/// </summary>
public partial class TurnStateManager
{
    private enum ContextualMoveKind { None, Attack, Embark, Merge, Supply }

    private ContextualMoveKind pendingContextualKind = ContextualMoveKind.None;
    private Vector3Int pendingContextualTargetCell;
    private Vector3Int pendingContextualArrivalCell;
    private UnitManager pendingContextualUnit;
    private bool pendingContextualSensorsReady;
    private int pendingContextualTerrainDefense;
    private bool pendingContextualChoseByTerrain;

    private readonly List<PodeMirarTargetOption> contextualAimScratch = new List<PodeMirarTargetOption>();
    private readonly List<PodeFundirOption> contextualMergeScratch = new List<PodeFundirOption>();
    private readonly List<PodeSuprirOption> contextualSupplyScratch = new List<PodeSuprirOption>();

    /// <summary>
    /// Toque num alvo durante a seleção. Devolve a célula para onde mover (o chamador
    /// posiciona o cursor ali e confirma, igual a um toque comum) e deixa pendente a
    /// ação sobre o alvo para quando os sensores do destino estiverem prontos.
    /// </summary>
    public bool TryResolveContextualMoveToTarget(
        Vector3Int targetCell,
        Vector3Int preferredArrivalCell,
        out Vector3Int arrivalCell)
    {
        arrivalCell = default;
        ClearPendingContextualMove();

        if (matchController == null || !matchController.AtalhoContextual)
            return false;
        if (CurrentCursorState != CursorState.UnitSelected || selectedUnit == null || selectedUnit.IsEmbarked)
            return false;

        targetCell.z = 0;
        preferredArrivalCell.z = 0;
        Vector3Int unitCell = selectedUnit.CurrentCellPosition;
        unitCell.z = 0;
        if (targetCell == unitCell)
            return false;

        Tilemap map = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        if (map == null)
            return false;

        // Candidatas: a própria célula (agir sem andar) e as alcançáveis.
        var candidates = new List<Vector3Int>(movementPathsByCell.Count + 1) { unitCell };
        foreach (Vector3Int cell in movementPathsByCell.Keys)
        {
            Vector3Int c = cell;
            c.z = 0;
            if (c != unitCell)
                candidates.Add(c);
        }

        // Inimigo: atirar. Aliado: a mesma ordem do atalho pós-movimento —
        // embarcar, fundir, suprir. A primeira que tiver célula legal vence.
        List<UnitManager> enemies = CollectVisibleEnemiesAtCell(map, targetCell);
        ContextualArrival arrival;
        if (enemies.Count > 0)
        {
            if (!TryPickContextualArrival(map, unitCell, preferredArrivalCell, candidates,
                    ContextualMoveKind.Attack, enemies, out arrival))
                return false;
        }
        else
        {
            List<UnitManager> friends = CollectFriendlyUnitsAtCell(map, targetCell);
            if (friends.Count == 0)
                return false;

            // Fusão só quando nada se perde: a soma do HP cabe em 10.
            List<UnitManager> mergePartners = new List<UnitManager>(friends.Count);
            for (int i = 0; i < friends.Count; i++)
                if (friends[i].CurrentHP + selectedUnit.CurrentHP <= 10)
                    mergePartners.Add(friends[i]);

            if (!TryPickContextualArrival(map, unitCell, preferredArrivalCell, candidates,
                    ContextualMoveKind.Embark, friends, out arrival)
                && (mergePartners.Count == 0
                    || !TryPickContextualArrival(map, unitCell, preferredArrivalCell, candidates,
                        ContextualMoveKind.Merge, mergePartners, out arrival))
                && !TryPickContextualArrival(map, unitCell, preferredArrivalCell, candidates,
                    ContextualMoveKind.Supply, friends, out arrival))
                return false;
        }

        arrivalCell = arrival.Cell;
        pendingContextualKind = arrival.Kind;
        pendingContextualChoseByTerrain = arrival.ChoseByTerrain;
        pendingContextualTerrainDefense = arrival.TerrainDefense;
        pendingContextualTargetCell = targetCell;
        pendingContextualArrivalCell = arrival.Cell;
        pendingContextualUnit = selectedUnit;
        pendingContextualSensorsReady = false;
        return true;
    }

    private struct ContextualArrival
    {
        public ContextualMoveKind Kind;
        public Vector3Int Cell;
        public bool ChoseByTerrain;
        public int TerrainDefense;
    }

    private bool TryPickContextualArrival(
        Tilemap map,
        Vector3Int unitCell,
        Vector3Int preferredArrivalCell,
        List<Vector3Int> candidates,
        ContextualMoveKind kind,
        List<UnitManager> targets,
        out ContextualArrival arrival)
    {
        arrival = default;
        bool found = false;
        int bestCost = int.MaxValue;
        int bestSteps = int.MaxValue;
        int bestDpq = int.MinValue;
        bool bestIsPreferred = false;
        Vector3Int best = default;
        int bestDefense = 0;

        // A escolha que a regra SIMPLES faria (onde o cursor estava, senão a mais
        // barata): só se o terreno mudar a resposta é que o helper explica.
        bool plainFound = false;
        bool plainIsPreferred = false;
        int plainCost = int.MaxValue;
        int plainSteps = int.MaxValue;
        Vector3Int plain = default;

        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3Int cell = candidates[i];
            if (!TryGetContextualPathCost(map, unitCell, cell, out int cost, out int steps))
                continue;
            if (cell != unitCell && !IsContextualStopCell(cell))
                continue;
            // Embarque e fusão só acontecem de um vizinho do alvo (os dois sensores
            // olham só os vizinhos). Pular o resto antes evita uma busca de caminho
            // por célula no PodeFundir — no celular, o toque não pode engasgar.
            if ((kind == ContextualMoveKind.Embark || kind == ContextualMoveKind.Merge)
                && targets.Count > 0
                && SectorManager.HexDistance(cell, ZeroZ(targets[0].CurrentCellPosition)) != 1)
                continue;

            bool legal;
            switch (kind)
            {
                case ContextualMoveKind.Attack: legal = CanAimFromProjectedCell(map, cell, cell == unitCell, targets); break;
                case ContextualMoveKind.Embark: legal = CanEmbarkFromProjectedCell(map, cell, cost, targets); break;
                case ContextualMoveKind.Merge: legal = CanMergeFromProjectedCell(map, cell, cost, targets); break;
                case ContextualMoveKind.Supply: legal = CanSupplyFromProjectedCell(map, cell, targets); break;
                default: legal = false; break;
            }
            if (!legal)
                continue;

            bool isPreferred = cell == preferredArrivalCell;
            if (IsBetterPlainArrival(isPreferred, cost, steps, plainFound, plainIsPreferred, plainCost, plainSteps))
            {
                plain = cell;
                plainIsPreferred = isPreferred;
                plainCost = cost;
                plainSteps = steps;
                plainFound = true;
            }

            // O terreno só pesa no ataque: é ele que decide quanto o contra-ataque dói.
            int dpq = 0;
            int defense = 0;
            if (kind == ContextualMoveKind.Attack)
            {
                PositionDpqResult position = PositionDpqResolver.Resolve(
                    selectedUnit, cell, map, terrainDatabase, dpqAirHeightConfig);
                dpq = position.Points;
                defense = position.DefenseBonus;
            }

            bool better = !found
                || dpq > bestDpq
                || (dpq == bestDpq && IsBetterPlainArrival(
                        isPreferred, cost, steps, true, bestIsPreferred, bestCost, bestSteps));
            if (better)
            {
                best = cell;
                bestDpq = dpq;
                bestDefense = defense;
                bestIsPreferred = isPreferred;
                bestCost = cost;
                bestSteps = steps;
                found = true;
            }
        }

        if (!found)
            return false;

        arrival = new ContextualArrival
        {
            Kind = kind,
            Cell = best,
            ChoseByTerrain = kind == ContextualMoveKind.Attack && plainFound && best != plain,
            TerrainDefense = bestDefense
        };
        return true;
    }

    // A regra simples: onde o cursor estava vence; senão a mais barata; senão menos passos.
    private static bool IsBetterPlainArrival(
        bool isPreferred, int cost, int steps,
        bool hasCurrent, bool currentIsPreferred, int currentCost, int currentSteps)
    {
        if (!hasCurrent)
            return true;
        if (isPreferred != currentIsPreferred)
            return isPreferred;
        if (cost != currentCost)
            return cost < currentCost;
        return steps < currentSteps;
    }

    /// <summary>O movimento não saiu (o confirm recusou): nada fica pendente.</summary>
    public void ClearPendingContextualMove()
    {
        pendingContextualKind = ContextualMoveKind.None;
        pendingContextualChoseByTerrain = false;
        pendingContextualTerrainDefense = 0;
        pendingContextualUnit = null;
        pendingContextualSensorsReady = false;
    }

    // Chamado por NotifySensorsReady. A ação em si roda no Update seguinte, fora do
    // RefreshSensorsForCurrentState que disparou o aviso.
    private void MarkPendingContextualSensorsReady()
    {
        if (pendingContextualKind != ContextualMoveKind.None)
            pendingContextualSensorsReady = true;
    }

    private void ProcessPendingContextualMove()
    {
        if (pendingContextualKind == ContextualMoveKind.None || !pendingContextualSensorsReady)
            return;

        ContextualMoveKind kind = pendingContextualKind;
        Vector3Int targetCell = pendingContextualTargetCell;
        Vector3Int arrival = pendingContextualArrivalCell;
        UnitManager unit = pendingContextualUnit;
        bool choseByTerrain = pendingContextualChoseByTerrain;
        int terrainDefense = pendingContextualTerrainDefense;
        ClearPendingContextualMove();

        bool isMovementActionChoice = CurrentCursorState == CursorState.MoveuAndando ||
                                      CurrentCursorState == CursorState.MoveuParado;
        if (!isMovementActionChoice || unit == null || unit != selectedUnit)
            return;
        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;
        if (unitCell != arrival || scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return;

        // A verdade é a dos sensores do destino, recém-calculados. Se o alvo não
        // estiver lá (a projeção divergiu), a unidade fica parada na escolha de ação,
        // como num movimento comum — o jogador escolhe ou cancela.
        if (kind == ContextualMoveKind.Attack)
        {
            // Sem o porquê, metade dos jogadores acha que o caminho saiu torto.
            if (TryBeginAimAtClickedTarget(targetCell) && choseByTerrain)
            {
                string defesa = terrainDefense > 0 ? "+" + terrainDefense : terrainDefense.ToString();
                PushPanelUnitMessage(PanelDialogController.ResolveDialogMessage(
                    "contextual.attack.better_ground",
                    "Melhor posição ao alcance: defesa <defesa>",
                    new Dictionary<string, string> { { "defesa", defesa } }));
            }
        }
        else if (kind == ContextualMoveKind.Embark)
        {
            TryBeginEmbarkAtClickedTarget(targetCell);
        }
        else if (kind == ContextualMoveKind.Merge)
        {
            TryBeginMergeAtClickedTarget(targetCell);
        }
        else if (kind == ContextualMoveKind.Supply)
        {
            TryBeginSupplyAtClickedTarget(targetCell);
        }
    }

    private List<UnitManager> CollectVisibleEnemiesAtCell(Tilemap map, Vector3Int cell)
    {
        var result = new List<UnitManager>(2);
        List<UnitManager> units = UnitOccupancyRules.GetUnitsAtCell(map, cell, selectedUnit);
        for (int i = 0; units != null && i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.TeamId == selectedUnit.TeamId)
                continue;
            // Só o que o lado ativo enxerga: o atalho não pode ser oráculo de névoa.
            if (matchController != null && !matchController.IsUnitVisibleForActiveTeam(unit))
                continue;
            result.Add(unit);
        }
        return result;
    }

    private List<UnitManager> CollectFriendlyUnitsAtCell(Tilemap map, Vector3Int cell)
    {
        var result = new List<UnitManager>(2);
        List<UnitManager> units = UnitOccupancyRules.GetUnitsAtCell(map, cell, selectedUnit);
        for (int i = 0; units != null && i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && !unit.IsDead && !unit.IsEmbarked && unit.TeamId == selectedUnit.TeamId)
                result.Add(unit);
        }
        return result;
    }

    // Mesma régua do movimento confirmado (PrepareMovementCostForCommittedPath).
    private bool TryGetContextualPathCost(Tilemap map, Vector3Int unitCell, Vector3Int cell, out int cost, out int steps)
    {
        cost = 0;
        steps = 0;
        if (cell == unitCell)
            return true;
        if (!movementPathsByCell.TryGetValue(cell, out List<Vector3Int> path) || path == null || path.Count < 2)
            return false;

        steps = path.Count - 1;
        cost = UnitMovementPathRules.CalculateAutonomyCostForPath(
            map,
            selectedUnit,
            path,
            terrainDatabase,
            applyOperationalAutonomyModifier: false);
        return true;
    }

    // A pintura marca como alcançáveis também as células de PASSAGEM (aliado no
    // caminho). Parar exige o hex livre de qualquer unidade que o lado ativo veja —
    // a mesma checagem do confirm (FindUnitAtCell). O resto (hex disputado, decolagem)
    // o próprio confirm recusa, e aí o pendente é descartado.
    private bool IsContextualStopCell(Vector3Int cell)
    {
        UnitManager occupant = FindUnitAtCell(cell);
        return occupant == null || occupant == selectedUnit;
    }

    private bool CanAimFromProjectedCell(Tilemap map, Vector3Int cell, bool isOwnCell, List<UnitManager> enemies)
    {
        PodeMirarSensor.CollectTargets(
            selectedUnit,
            map,
            terrainDatabase,
            isOwnCell ? SensorMovementMode.MoveuParado : SensorMovementMode.MoveuAndando,
            contextualAimScratch,
            null,
            weaponPriorityData,
            dpqAirHeightConfig,
            matchController.EnableLdtValidation,
            matchController.EnableLosValidation,
            matchController.EnableSpotter,
            matchController.EnableStealthValidation,
            respectTotalWarVisibility: true,
            fromCell: cell,
            targetCandidates: enemies);

        for (int i = 0; i < contextualAimScratch.Count; i++)
        {
            UnitManager target = contextualAimScratch[i]?.targetUnit;
            if (target != null && enemies.Contains(target))
                return true;
        }
        return false;
    }

    private bool CanEmbarkFromProjectedCell(Tilemap map, Vector3Int cell, int pathCost, List<UnitManager> transporters)
    {
        int remaining = Mathf.Max(0, selectedUnit.RemainingMovementPoints - pathCost);
        for (int t = 0; t < transporters.Count; t++)
        {
            UnitManager transporter = transporters[t];
            if (transporter == null || !transporter.TryGetUnitData(out UnitData data) || data == null
                || data.transportSlots == null)
                continue;

            for (int slot = 0; slot < data.transportSlots.Count; slot++)
            {
                if (PodeEmbarcarSensor.CanEmbarkFromProjectedCell(
                        selectedUnit, cell, transporter, slot, map, terrainDatabase,
                        remaining, out _, out _))
                    return true;
            }
        }
        return false;
    }

    // PodeFundir com fromCell: daqui, adjacente ao parceiro, com o MP que sobra do
    // caminho, a fusão é legal? (A trava da soma do HP <= 10 é do atalho, não da regra.)
    private bool CanMergeFromProjectedCell(Tilemap map, Vector3Int cell, int pathCost, List<UnitManager> partners)
    {
        int remaining = Mathf.Max(0, selectedUnit.RemainingMovementPoints - pathCost);
        PodeFundirSensor.CollectOptions(
            selectedUnit, map, terrainDatabase, remaining,
            contextualMergeScratch, out _, null, fromCell: cell);
        for (int i = 0; i < contextualMergeScratch.Count; i++)
        {
            UnitManager candidate = contextualMergeScratch[i]?.candidateUnit;
            if (candidate != null && partners.Contains(candidate))
                return true;
        }
        return false;
    }

    // PodeSuprir prospectivo: como se o supridor estivesse nesta célula.
    private bool CanSupplyFromProjectedCell(Tilemap map, Vector3Int cell, List<UnitManager> targets)
    {
        PodeSuprirSensor.CollectOptionsFromCell(
            selectedUnit, map, terrainDatabase, matchController, cell,
            contextualSupplyScratch, out _);
        for (int i = 0; i < contextualSupplyScratch.Count; i++)
        {
            UnitManager target = contextualSupplyScratch[i]?.targetUnit;
            if (target != null && targets.Contains(target))
                return true;
        }
        return false;
    }

    private static Vector3Int ZeroZ(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }
}
