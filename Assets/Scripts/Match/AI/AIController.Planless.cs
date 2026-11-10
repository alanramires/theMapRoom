using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // Capturadores sem vaga usam o mesmo caminho rogue em qualquer faccao.
    // Sem QG, todas as unidades sao sem plano; com QG, este roteador atende
    // somente quem nao recebeu slot. Missao e preparada antes dos atalhos e
    // confirmada apenas depois do batch. Os auxiliares abaixo consultam os
    // sensores e o matching comuns, inclusive para transporte/desembarque.

    private PlayerAction TryDecidePlanlessCapturerAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null)
            return null;
        if (IsPassengerInPlanSlot(unit, plan))
            return null;
        if (!unit.TryGetUnitData(out UnitData unitData) || unitData == null
            || !UnitRoleCompatibility.CanSatisfy(unitData, UnitRole.Capturador))
            return null;

        // MISSAO ANTES DOS ATALHOS.
        //
        // Ate 2026-08-06 o alvo so era gravado dentro do caminho rogue (o unico
        // lugar que preenche pendingPlanlessCaptureTargets). Quem saia por um atalho
        // — captura oportunista, embarque, handoff, swap — agia SEM DECLARAR
        // DESTINO. No teste do tabuleiro de treino o soldado capturou uma garagem
        // pelo Oportunista, embarcou no turno seguinte com Mission Intent = None,
        // e o APC teve que inventar para onde levar.
        //
        // Aqui a pergunta e "PARA ONDE eu quero ir", nao "consigo marchar ate la":
        // por isso requireOwnMovementReach fica FALSO. Justamente quando ele NAO
        // alcanca a pe e que ele pede carona — e e nesse turno que o transportador
        // precisa da missao. O caminho rogue regrava com o criterio dele (a pe)
        // quando chega a rodar, e o commit valida antes de escrever.
        if (!pendingPlanlessCaptureTargets.ContainsKey(unit.InstanceId))
        {
            Vector3Int missionFrom = unit.CurrentCellPosition;
            missionFrom.z = 0;
            ConstructionManager missionTarget =
                FindNearestPlanlessCaptureTarget(unit, snapshot, missionFrom);
            if (missionTarget != null)
                pendingPlanlessCaptureTargets[unit.InstanceId] = missionTarget;
        }

        // Sem vaga, o plano coletivo nao vira uma missao emprestada: o
        // capturador usa o caminho rogue e suas politicas compartilhadas.
        return TryDecideCapturerAction(unit, snapshot, plan: null);
    }

    /// <summary>
    /// Capturavel livre mais proximo que ainda nao esta sendo tratado.
    ///
    /// <paramref name="requireOwnMovementReach"/> exige que o predio esteja no
    /// componente de movimento PROPRIO da unidade — "alcancavel a pe". So quem
    /// vai MARCHAR ate la usa isso. O transporte NAO usa: ele pergunta por um
    /// alvo para largar o passageiro, e passageiro embarcado tem componente do
    /// veiculo (agua, no caso do navio), que reprovaria todo predio em terra.
    /// </summary>
    private ConstructionManager FindNearestPlanlessCaptureTarget(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        bool requireOwnMovementReach = false)
    {
        bool hasDesignated =
            TryResolveUnitDesignatedCaptureTarget(
                unit,
                out ConstructionManager designated);

        // Celulas onde ja ha aliado parado — proxy de "predio ja sendo capturado por nos".
        HashSet<Vector3Int> allyCells = new HashSet<Vector3Int>();
        if (snapshot.MyUnits != null)
        {
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager ally = snapshot.MyUnits[i];
                if (ally == null || ally == unit || ally.IsDead || ally.IsEmbarked)
                    continue;
                Vector3Int ac = ally.CurrentCellPosition; ac.z = 0;
                allyCells.Add(ac);
            }
        }

        ConstructionManager best = null;
        float bestDist = float.MaxValue;
        CaptureOpportunityClaimSnapshot collectiveClaims =
            CaptureOpportunityClaimService.GetOrBuild(
                new QueroCaronaRequest
                {
                    unit = unit,
                    map = boardTilemap,
                    terrainDatabase = terrainDatabase,
                    context =
                        QueroCaronaContext.SemPlano,
                    plannedSector =
                        ConstructionSector.None,
                    operationalTurns =
                        TransportPassengerWalkTurns,
                    emulateUnderRepairFromUnitData =
                        false
                });
        if (hasDesignated
            && collectiveClaims.TryGetClaim(
                designated,
                out CaptureOpportunityClaim designatedClaim)
            && designatedClaim.Capturer != null
            && designatedClaim.Capturer != unit)
        {
            // Farol antigo perdeu o pareamento coletivo: nao vira lock.
            hasDesignated = false;
            designated = null;
        }

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            if (construction.TeamId == snapshot.AITeam)
                continue;
            if (!IsPlanlessCapturable(unit, construction))
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            // Ja ha aliado que realmente ocupa a camada de captura aqui
            // (persiste entre turnos), ou outro rogue reservou este objetivo
            // nesta passada? Uma aeronave na mesma coordenada nao reivindica
            // o predio para uma infantaria terrestre.
            bool claimedByAlliedCapturer =
                allyCells.Contains(cell) &&
                UnitOccupancyRules.HasBlockingOccupantForUnitAtCell(
                    boardTilemap,
                    cell,
                    unit,
                    alliedOnly: true);
            bool claimedByOtherCollectiveCapturer =
                collectiveClaims != null
                && collectiveClaims.TryGetClaim(
                    construction,
                    out CaptureOpportunityClaim collectiveClaim)
                && collectiveClaim.Capturer != null
                && collectiveClaim.Capturer != unit;
            if (claimedByAlliedCapturer ||
                claimedByOtherCollectiveCapturer ||
                planlessCaptureTargetReservations.Contains(cell))
                continue;

            // "Alcancavel": esta no componente de movimento proprio. Predio do
            // outro lado do mar nao e destino de marcha — e pedido de carona,
            // e quem responde isso e o Quero Carona.
            if (requireOwnMovementReach)
            {
                MobilityComponent ownComponent =
                    GetOrBuildMobilityComponent(unit);
                if (ownComponent?.Cells == null
                    || !ownComponent.Cells.ContainsKey(cell))
                {
                    continue;
                }
            }

            float dist = SectorManager.HexDistance(fromCell, cell);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = construction;
            }
        }

        if (!hasDesignated)
            return best;

        // A designacao persistente impede troca caotica de objetivo entre turnos,
        // mas nao pode eclipsar uma captura materializavel agora. O snapshot de
        // claims acima continua sendo a fonte de verdade: so desviamos para um
        // predio livre que nao pertence a outro capturador.
        if (best != null && best != designated)
        {
            Vector3Int bestCell = best.CurrentCellPosition;
            bestCell.z = 0;
            int immediateBudget = Mathf.Max(
                0,
                unit.RemainingMovementPoints);
            int immediateCost = TerrainCostToCell(
                unit,
                fromCell,
                bestCell,
                immediateBudget);
            float designatedDistance = SectorManager.HexDistance(
                fromCell,
                designated.CurrentCellPosition);
            if (immediateCost <= immediateBudget
                && bestDist < designatedDistance)
            {
                Debug.Log(
                    $"{TL("SemPlano")} {unit.InstanceId} substitui " +
                    $"DesignatedCaptureTarget #{designated.InstanceId} " +
                    $"por oportunidade imediata #{best.InstanceId} em " +
                    $"{bestCell} custo={immediateCost}<={immediateBudget}.");
                return best;
            }
        }

        Debug.Log(
            $"{TL("SemPlano")} {unit.InstanceId} mantem " +
            $"DesignatedCaptureTarget " +
            $"#{designated.InstanceId} em " +
            $"{designated.CurrentCellPosition}.");
        return designated;
    }

    private bool TryResolveUnitDesignatedCaptureTarget(
        UnitManager unit,
        out ConstructionManager target)
    {
        target = null;
        if (unit == null
            || unit.IsDead
            || !unit.AIHasDesignatedCaptureTarget
            || !unit.TryGetUnitData(out UnitData data)
            || data == null
            || !UnitRoleCompatibility.CanSatisfy(
                data,
                UnitRole.Capturador))
        {
            return false;
        }

        int targetInstanceId =
            unit.AIDesignatedCaptureTargetInstanceId;
        Vector3Int targetCell =
            unit.AIDesignatedCaptureTargetCell;
        targetCell.z = 0;
        foreach (ConstructionManager construction
                 in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            Vector3Int cell =
                construction.CurrentCellPosition;
            cell.z = 0;
            if (construction.InstanceId != targetInstanceId
                && cell != targetCell)
            {
                continue;
            }
            if (!IsPlanlessCapturable(unit, construction))
                return false;
            if (UnitOccupancyRules
                .HasBlockingOccupantForUnitAtCell(
                    boardTilemap,
                    cell,
                    unit,
                    alliedOnly: true))
            {
                return false;
            }

            target = construction;
            return true;
        }

        return false;
    }

    private void CommitPendingPlanlessCaptureTarget(
        UnitManager unit)
    {
        if (unit == null
            || !pendingPlanlessCaptureTargets.TryGetValue(
                unit.InstanceId,
                out ConstructionManager target))
        {
            return;
        }

        pendingPlanlessCaptureTargets.Remove(unit.InstanceId);
        if (target == null
            || unit.IsDead
            || !IsPlanlessCapturable(unit, target))
        {
            if (unit.AIHasDesignatedCaptureTarget)
                unit.ClearAIDesignatedCaptureTarget();
            return;
        }

        Vector3Int cell = target.CurrentCellPosition;
        cell.z = 0;
        bool kept = unit.AIHasDesignatedCaptureTarget
            && unit.AIDesignatedCaptureTargetInstanceId == target.InstanceId;
        if (!unit.SetAIDesignatedCaptureTarget(
                target.InstanceId,
                cell))
        {
            // A unidade ja carrega missao de outro verbo e ela manda. Antes de
            // a captura virar missao isto nao podia acontecer, porque os dois
            // viviam em campos separados e nunca precisavam concordar.
            Debug.Log(
                $"{TL("SemPlano")} {unit.InstanceId} NAO grava captura " +
                $"#{target.InstanceId}: ja tem missao " +
                $"{unit.AIDesignatedMissionIntent}.");
            return;
        }

        Debug.Log(
            $"{TL("Missao")} {unit.InstanceId} Capture -> {cell} " +
            $"predio=#{target.InstanceId} " +
            $"({(kept ? "mantida" : "adquirida")}).");
    }

    /// <summary>
    /// Alvo de captura legitimo para esta unidade na pergunta de planejamento.
    /// Regras de faccao (como pre-requisito para time sem QG) pertencem ao
    /// MatchController e ao sensor oficial, nunca a este auxiliar.
    ///
    /// O corpo passou a delegar ao PodeCapturarSensor. O que saiu daqui:
    ///
    ///   `construction.TeamId == unit.TeamId` — EIXO ERRADO, time em vez de
    ///   slot, e apagava a reconquista inteira. O comentario antigo declarava a
    ///   intencao ("predio do mesmo time nao volta ao ranking"), e o sensor diz
    ///   o contrario: aliado abaixo do maximo e RecoverAlly, alvo legitimo. Como
    ///   quatro papeis herdavam este predicado, os quatro eram cegos para
    ///   reconquista.
    ///
    ///   `CanCaptureConstruction` direto no matchController — meia regra. O
    ///   sensor aplica essa e as outras, e continua aplicando se mudarem.
    ///
    /// Dois portoes de hora-de-agir ficam desligados, porque esta e uma
    /// pergunta de PLANEJAMENTO: a nevoa (o recorte do que o time conhece e
    /// cruzado por quem decide agir) e o embarque (o passageiro ainda esta no
    /// veiculo justamente porque queremos saber onde larga-lo).
    /// </summary>
    private bool IsPlanlessCapturable(UnitManager unit, ConstructionManager construction)
    {
        if (unit == null || construction == null)
            return false;

        Vector3Int cell = construction.CurrentCellPosition;
        cell.z = 0;
        Vector3Int from = unit.CurrentCellPosition;
        from.z = 0;

        return PodeCapturarSensor.TryGetCaptureTargetAtCell(
                   unit,
                   boardTilemap,
                   cell,
                   cell == from
                       ? SensorMovementMode.MoveuParado
                       : SensorMovementMode.MoveuAndando,
                   out ConstructionManager target,
                   out _,
                   out _,
                   matchController,
                   applyFogOfWar: false,
                   knownConstruction: construction,
                   applyEmbarkedGate: false)
               && (target == null || target == construction);
    }
}
