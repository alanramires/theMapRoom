using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Courier — transporter carrying passengers, delivering to objective
    // -------------------------------------------------------------------------

    private PlayerAction DecideTransportadorCourierAction(UnitManager unit, AIWorldSnapshot snapshot,
        Vector3Int assignedSectorTarget = default)
    {
        Vector3Int fromCell = unit.CurrentCellPosition; fromCell.z = 0;
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));

        List<UnitManager> passengers = CollectPassengers(unit);
        if (passengers.Count == 0)
        {
            if (showAILogs)
                Debug.LogWarning($"[AI] {TL("Transporte")} {unit.InstanceId} courier: cargo inconsistente; libera outra atividade.");
            return null;
        }

        UnitManager primaryPassenger = ResolvePrimaryPassenger(unit, passengers, plan);
        bool primaryTargetFound = TryResolveCourierPassengerTarget(primaryPassenger, plan, snapshot, assignedSectorTarget, fromCell, out Vector3Int primaryTarget);
        if (!primaryTargetFound) primaryTarget = fromCell;
        // A banda e do PASSAGEIRO, nao do papel. O teto vai ate o Operational
        // porque cerco, montanha ou LZ sem rota podem nao deixar nenhum spot no
        // Tactical — e duas rodadas de marcha ainda batem continuar a bordo.
        // Quem garante a preferencia pelo Tactical e o RANKING do
        // MelhorDesembarque, que ja escolhe a rota mais curta: no teste de
        // 2026-08-06 ele aceitou R2, R3 e R4 e ficou com o R2.
        //
        // Fogo de suporte fica no numero antigo de proposito: a banda dele e a da
        // ARMA, nao a do movimento (CLAUDE.md, "Known inversion"), e esse resolver
        // ainda nao existe.
        bool fireSupportPassenger = IsFireSupportUnit(primaryPassenger);
        int passengerTactical = ResolvePassengerDropOffRange(
            primaryPassenger, operationalFallback: false);
        int dropOffRange = fireSupportPassenger
            ? FireSupportDropOffRange
            : ResolvePassengerDropOffRange(primaryPassenger, operationalFallback: true);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — passageiro #{primaryPassenger.InstanceId} alvo={primaryTarget} range={dropOffRange}"
            + (fireSupportPassenger
                ? " (fogo de suporte: constante legada)"
                : $" (Operational; Tactical={passengerTactical})")
            + $" distAtual={SectorManager.HexDistance(fromCell, primaryTarget):F0}h");

        // O TRANSPORTADOR TAMBEM DECLARA MISSAO — e com o objetivo da CARGA.
        //
        //   "o transportador tambem precisa da missao transport com o objetivo
        //    do carona. pq se ele for pegar outro transportador como um navio, o
        //    navio vai ler dele" (autor, 2026-08-07)
        //
        // Transporte aninhado: navio carrega APC carrega soldado. O navio
        // pergunta para onde a carga dele quer ir e le o APC — exatamente como o
        // APC le o soldado. Se o APC nao declarou, o navio inventa, e o defeito
        // que a gente acabou de consertar um nivel abaixo reaparece um nivel
        // acima.
        //
        // A promessa de coleta (RidePromise) ja grava Transport, mas com a
        // celula de ENCONTRO. Depois que a carga esta a bordo o destino e outro:
        // o objetivo do passageiro. Aqui o pendente e reescrito com ele.
        //
        // Pendente e nao escrita direta pelo invariante transacional: quem
        // confirma e CommitPendingAIDesignatedMission, na Fase 2, depois do
        // batch dar certo.
        if (primaryTargetFound)
        {
            Vector3Int cargoTarget = primaryTarget;
            cargoTarget.z = 0;
            pendingAIDesignatedMissions[unit.InstanceId] =
                new PendingAIDesignatedMission(
                    AIPlanRuntimeIntent.Transport,
                    cargoTarget,
                    primaryPassenger != null ? primaryPassenger.InstanceId : -1,
                    -1,
                    0);
        }

        Dictionary<Vector3Int, List<Vector3Int>> paths =
            UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);


        if (paths == null || paths.Count == 0)
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell);

        // EVAC mode: passenger is under repair → deliver to nearest safe repair location.
        UnitManager evacuee = passengers.Find(p => p.IsUnderRepair);
        if (evacuee != null)
            return DecideEvacCourierAction(unit, evacuee, passengers, snapshot, fromCell, paths, occupied);

        // Mesma precedencia do capturador rogue a pe: uma captura local util
        // acontece antes da progressao macro ao HQ. Antes estes checks vinham
        // depois do MelhorDesembarque; como quase sempre havia uma entrega
        // valida ao alvo final, o retorno antecipado tornava a oportunidade
        // local inalcançavel.
        if (TryBuildRogueCourierLocalOpportunityDrop(
                unit, passengers, snapshot, plan, fromCell, paths, occupied, out PlayerAction localOpportunity))
            return localOpportunity;

        if (!IsFireSupportUnit(primaryPassenger)
            && TryBuildRogueCourierLocalOpportunityDrop(
                unit, passengers, snapshot, plan, fromCell, paths, occupied,
                out PlayerAction assignedLocalOpportunity,
                allowAssignedPassengers: true))
            return assignedLocalOpportunity;

        // ANCORA NA ZONA DE ENTREGA, nao no alvo.
        //
        // O courier nao precisa chegar no predio — precisa chegar a um lugar de
        // onde o PASSAGEIRO alcanca o predio sozinho. Essa zona e uma propriedade
        // do destino e do passageiro; nao encolhe quando o transportador se
        // enrosca, e e ela que o movimento deve perseguir.
        //
        // Mirar o alvo cru e o que produzia o vaivem: com serra no meio, o alvo
        // fica atras de terreno caro, o progresso empata em zero e o APC oscila
        // entre duas celulas baratas. A zona fica DESTE lado do obstaculo sempre
        // que houver caminho a pe do outro lado — e ai existe progresso real a
        // perseguir. Ver docs/AI Behavior/Transporte.md e a nota do CLAUDE.md
        // ("reverse: teleport the unit onto the target, that area is the drop zone").
        Vector3Int movementAnchor = primaryTarget;
        bool anchorIsTactical = false;
        string anchorMode = "entrega";
        if (TryResolveDeliveryZoneAnchor(
                unit, primaryPassenger, primaryTarget, fromCell,
                out Vector3Int deliveryAnchor, out int anchorPassengerCost,
                out anchorIsTactical, out anchorMode))
        {
            movementAnchor = deliveryAnchor;
            if (movementAnchor != primaryTarget)
                Debug.Log(
                    $"{TL("Transporte")} {unit.InstanceId} ancora {anchorMode} "
                    + $"{movementAnchor} "
                    + (anchorMode == "avancar"
                        ? $"— nenhuma celula CONHECIDA na zona de {primaryTarget}; "
                          + "avanca para o escuro na direcao dela."
                        : $"({(anchorIsTactical ? "Tactical" : "Operational")}; "
                          + $"alvo {primaryTarget}; passageiro anda "
                          + $"{anchorPassengerCost} de la) — mira a ZONA, "
                          + "nao o predio."));
        }

        // -------------------------------------------------------------------
        // LARGAR AGORA x ANDAR E LARGAR MELHOR AMANHA
        //
        // Ate 2026-08-07 isto nao era uma escolha: a largada era o if de cima e
        // o movimento o de baixo, entao qualquer LZ admissivel encerrava o turno
        // e a ancora nem rodava. "Nao era pra ele ter avancado pro operacional
        // com delay de desembarque?" — nao era, porque a decisao nao existia.
        //
        // Agora existe, e em tres tempos:
        //
        //   1. TACTICAL       largada boa: o passageiro chega no turno seguinte.
        //                     Se ha uma, pega e acabou.
        //   2. ADIAR          nao ha Tactical hoje, mas ha ancora Tactical
        //                     CONHECIDA e o casco realmente se move ate la.
        //                     Andar hoje vale mais que largar mal.
        //   3. OPERACIONAL    nem Tactical hoje nem melhora amanha. Ai a
        //                     reserva vale — e e o caso do navio, que nao chega
        //                     mais perto da praia por decreto.
        //
        // Fogo de suporte nao entra: a banda dele e a da ARMA, e o teto legado
        // continua ate existir o resolver proprio.
        // -------------------------------------------------------------------
        if (fireSupportPassenger)
        {
            if (TryBuildBestCourierDisembarkAction(
                    unit, passengers, plan, snapshot, assignedSectorTarget,
                    paths, dropOffRange, false, "Terrestre",
                    out PlayerAction fireSupportDrop))
                return fireSupportDrop;
        }
        else if (TryBuildBestCourierDisembarkAction(
                     unit, passengers, plan, snapshot, assignedSectorTarget,
                     paths, passengerTactical, false, "Terrestre",
                     out PlayerAction tacticalDrop))
        {
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} larga no TACTICAL "
                + $"(rota restante <= {passengerTactical}).");
            return tacticalDrop;
        }

        Vector3Int moveTarget = FindTransportMove(unit, fromCell, movementAnchor, paths, occupied, snapshot.AITeam);

        // O adiamento so vale se o casco REALMENTE anda. Sem esta guarda, um APC
        // bloqueado adiaria a largada para sempre esperando um amanha que nao vem.
        bool advancingImprovesDelivery =
            !fireSupportPassenger
            && anchorMode == "entrega"
            && anchorIsTactical
            && moveTarget != fromCell;

        if (!fireSupportPassenger && !advancingImprovesDelivery
            && TryBuildBestCourierDisembarkAction(
                unit, passengers, plan, snapshot, assignedSectorTarget,
                paths, dropOffRange, false, "Terrestre",
                out PlayerAction operationalDrop))
        {
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} larga no OPERACIONAL "
                + $"(reserva: sem Tactical hoje e andar nao melhora; "
                + $"ancora={anchorMode} tactical={anchorIsTactical} "
                + $"move={(moveTarget != fromCell ? "sim" : "nao")}).");
            return operationalDrop;
        }

        if (advancingImprovesDelivery)
            Debug.Log(
                $"{TL("Transporte")} {unit.InstanceId} ADIA a largada: anda ate "
                + $"{moveTarget} porque a ancora {movementAnchor} e Tactical "
                + "conhecida — largar hoje custaria um turno de marcha a mais.");

        // If FindTransportMove landed on the objective building itself, redirect to an adjacent
        // reachable cell so the passenger can be disembarked directly onto the building.
        if (moveTarget == primaryTarget)
        {
            var neighbors = new List<Vector3Int>(6);
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, primaryTarget, neighbors);
            Vector3Int bestAdj = Vector3Int.zero;
            float bestThreat = float.MaxValue;
            foreach (Vector3Int nb in neighbors)
            {
                Vector3Int nbc = nb; nbc.z = 0;
                if (occupied.Contains(nbc) || !paths.ContainsKey(nbc)) continue;
                float threat = CalculateThreatLevel(nbc, snapshot.AITeam);
                if (bestAdj == Vector3Int.zero || threat < bestThreat - 0.001f)
                    { bestAdj = nbc; bestThreat = threat; }
            }
            if (bestAdj != Vector3Int.zero) moveTarget = bestAdj;
        }

        bool invasionDelivery = IsTransportInvasionDelivery(primaryPassenger, plan, snapshot, primaryTarget);
        bool redirectedToInvasionRendezvous = false;
        if (invasionDelivery && !IsTransportInvasionCourierCellAllowed(unit, snapshot, moveTarget, primaryTarget))
        {
            Vector3Int rendezvousCell = FindTransportInvasionRendezvousCell(
                unit, snapshot, fromCell, primaryTarget, paths, occupied, out string rendezvousReason);
            if (rendezvousCell != moveTarget)
            {
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier invasao — bloqueia avanco {moveTarget}, rendezvous via {rendezvousCell} ({rendezvousReason})");
                moveTarget = rendezvousCell;
                redirectedToInvasionRendezvous = true;
            }
        }

        float moveImprovement = CalculateRouteDistanceOrHex(unit, fromCell, primaryTarget)
                              - CalculateRouteDistanceOrHex(unit, moveTarget, primaryTarget);

        // Priority 1a — FireSupport passenger: first drive as far as this turn can toward
        // the assigned target, then choose the best legal landing cell from there.
        if (IsFireSupportUnit(primaryPassenger))
        {
            // Conservative transporter (logistics/supply truck): don't drive toward the artillery's
            // objective — the truck will never reach the front. Instead, drop the artillery at the
            // best safe rear-area cell reachable this turn (allied building > allies nearby > low threat).
            if (unit.TryGetUnitData(out UnitData towData) && towData != null && towData.playConservative)
        {
            float towDist = SectorManager.HexDistance(fromCell, primaryTarget);
            bool targetIsHot = CalculateThreatLevel(primaryTarget, snapshot.AITeam) > 0.1f
                || HasNearbyVisibleEnemy(primaryTarget, snapshot.AITeam, 2);
            if (targetIsHot)
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — destino quente ({primaryTarget}), ativa conservador dist={towDist:F0}h");
            else
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier conservador — FS embarcado dist={towDist:F0}h, sem progressao carregado");
            return TryDropFireSupportConservative(unit, primaryPassenger, passengers, snapshot, plan, fromCell, moveTarget, paths, occupied);
        }

            SectorObjective fsObj = ResolveAssignedFireSupportObjective(primaryPassenger, plan);
            string fsSector = fsObj != null ? fsObj.Sector.ToString() : "?";
            float distToTarget = SectorManager.HexDistance(fromCell, primaryTarget);

            // Only skip the forward move if it would send the truck BACKWARD (redirect case:
            // truck is already on the objective cell, redirect moved it away → moveImprovement < 0).
            bool hasForwardMove = moveTarget != fromCell && moveImprovement >= 0f;

            if (hasForwardMove)
            {
                List<PodeDesembarcarOption> opts = SimulateDisembarkFromCell(unit, moveTarget);
                if (opts != null && opts.Count > 0)
                {
                    List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(opts, passengers, plan, snapshot);
                    PodeDesembarcarOption primaryOpt = selected.Find(o => o.passengerUnit == primaryPassenger);
                    if (primaryOpt != null)
                    {
                        Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                        float dcDist = SectorManager.HexDistance(dc, primaryTarget);
                        // Drop if the artillery lands near the target OR if the truck itself
                        // is already close enough (artillery can walk the remaining distance).
                        float truckDistAfterMove = SectorManager.HexDistance(moveTarget, primaryTarget);
                        if (dcDist <= FireSupportDropOffRange || truckDistAfterMove <= FireSupportDropOffRange)
                        {
                            float score = ScoreCourierDisembarkOption(primaryPassenger, dc, primaryTarget, snapshot.AITeam,
                                dcDist, CalculateThreatLevel(dc, snapshot.AITeam));
                            paths.TryGetValue(moveTarget, out List<Vector3Int> fsMoved);
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} avança+desembarca via {moveTarget} destDist={dcDist:F0}h truckDist={truckDistAfterMove:F0}h dpq={score:F0} → {primaryTarget}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected, moveTarget, fsMoved);
                        }
                    }
                }
            }
            else
            {
                // If the truck cannot improve this turn, keep the artillery aboard unless
                // the landing cell is already close to the assigned sector.
                var currentOpts = new List<PodeDesembarcarOption>();
                if (PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, currentOpts) && currentOpts.Count > 0)
                {
                    List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(currentOpts, passengers, plan, snapshot);
                    PodeDesembarcarOption primaryOpt = selected.Find(o => o.passengerUnit == primaryPassenger);
                    if (primaryOpt != null)
                    {
                        Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                        float dcDist = SectorManager.HexDistance(dc, primaryTarget);
                        // Also drop in place when the truck itself is within drop range.
                        if (dcDist <= FireSupportDropOffRange || distToTarget <= FireSupportDropOffRange)
                        {
                            float score = ScoreCourierDisembarkOption(primaryPassenger, dc, primaryTarget, snapshot.AITeam,
                                dcDist, CalculateThreatLevel(dc, snapshot.AITeam));
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} desembarca no lugar destDist={dcDist:F0}h truckDist={distToTarget:F0}h dpq={score:F0} → {primaryTarget}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                        }
                    }
                }
            }
            // No disembark option available yet — fall through to Priority 3 (keep moving).
            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — FireSupport #{primaryPassenger.InstanceId} setor={fsSector} sem opção de desembarque ainda dist={distToTarget:F0}h → {primaryTarget}");
        }
        else
        {
            if (invasionDelivery && redirectedToInvasionRendezvous)
            {
                List<PodeDesembarcarOption> rendezvousOptions = moveTarget == fromCell
                    ? CollectCurrentRogueCourierLocalDisembarkOptions(unit)
                    : SimulateDisembarkFromCell(unit, moveTarget);
                if (rendezvousOptions != null && rendezvousOptions.Count > 0
                    && TryBuildRogueCourierContestedRendezvousDrop(
                        unit,
                        passengers,
                        snapshot,
                        plan,
                        fromCell,
                        moveTarget,
                        rendezvousOptions,
                        paths,
                        out PlayerAction rendezvousDrop,
                        allowAssignedPassengers: true,
                        requireUnheldRallyPoint: true))
                    return rendezvousDrop;
            }

        // Priority 1b: move + disembark when moving brings the APC meaningfully closer
        // AND the simulated drop-off from moveTarget is within delivery range.
        if (moveTarget != fromCell && moveImprovement > 1f)
        {
            List<PodeDesembarcarOption> optionsFromMove = SimulateDisembarkFromCell(unit, moveTarget);
            if (optionsFromMove != null && optionsFromMove.Count > 0)
            {
                var optCells = string.Join(", ", optionsFromMove.ConvertAll(o => { var c = o.disembarkCell; c.z = 0; return $"{c}({SectorManager.HexDistance(c, primaryTarget):F0}h)"; }));
                Debug.Log($"{TL("Transporte")} {unit.InstanceId} simDisembark from {moveTarget}: [{optCells}] → target={primaryTarget}");
                if (invasionDelivery
                    && TryBuildRogueCourierContestedRendezvousDrop(
                        unit,
                        passengers,
                        snapshot,
                        plan,
                        fromCell,
                        moveTarget,
                        optionsFromMove,
                        paths,
                        out PlayerAction contestedDrop,
                        allowAssignedPassengers: true,
                        requireUnheldRallyPoint: true))
                    return contestedDrop;

                List<PodeDesembarcarOption> selectedFromMove =
                    SelectBestDisembarkPerPassenger(optionsFromMove, passengers, plan, snapshot);
                PodeDesembarcarOption primaryOpt = selectedFromMove.Count > 0
                    ? selectedFromMove.Find(o => o.passengerUnit == primaryPassenger) : null;
                if (primaryOpt != null)
                {
                    Vector3Int dc = primaryOpt.disembarkCell; dc.z = 0;
                    bool dcInRange = SectorManager.HexDistance(dc, primaryTarget) <= dropOffRange;
                    bool truckInRange = SectorManager.HexDistance(moveTarget, primaryTarget) <= dropOffRange;
                    if (dcInRange || truckInRange)
                    {
                        if (invasionDelivery && !IsTransportInvasionDropAllowed(unit, snapshot, moveTarget, dc, primaryTarget))
                        {
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier invasao — segura desembarque hostil dc={dc} via {moveTarget}");
                        }
                        else
                        {
                            paths.TryGetValue(moveTarget, out List<Vector3Int> movePath);
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move+desembarca {selectedFromMove.Count} passageiro(s) via {moveTarget} dc={dc} dcDist={SectorManager.HexDistance(dc, primaryTarget):F0}h truckDist={SectorManager.HexDistance(moveTarget, primaryTarget):F0}h → {primaryTarget}");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selectedFromMove, moveTarget, movePath);
                        }
                    }
                }
            }
        }

        // Priority 2: disembark from current position.
        // Normal case: moving gains ≤1h, so current position is already near-optimal.
        // Emergency case: completely blocked (moveTarget == fromCell) — disembark regardless of
        // distance so passengers can fight instead of staying trapped in an immobile APC.
        bool isStuck = moveTarget == fromCell;
        var disembarkOptions = new List<PodeDesembarcarOption>();
        bool canDisembark = PodeDesembarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, disembarkOptions);
        if (canDisembark && disembarkOptions.Count > 0 && (moveImprovement <= 1f || isStuck))
        {
            List<PodeDesembarcarOption> selected = SelectBestDisembarkPerPassenger(disembarkOptions, passengers, plan, snapshot);
            if (selected.Count > 0)
            {
                PodeDesembarcarOption primaryOption = selected.Find(o => o.passengerUnit == primaryPassenger);
                if (primaryOption != null)
                {
                    Vector3Int dc = primaryOption.disembarkCell; dc.z = 0;
                    bool inRangeP2 = isStuck
                        || SectorManager.HexDistance(dc, primaryTarget) <= dropOffRange
                        || SectorManager.HexDistance(fromCell, primaryTarget) <= dropOffRange;
                    if (inRangeP2)
                    {
                        if (invasionDelivery && !IsTransportInvasionDropAllowed(unit, snapshot, fromCell, dc, primaryTarget))
                        {
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier invasao — segura desembarque no lugar dc={dc}");
                        }
                        else
                        {
                            string reason = isStuck ? "bloqueado, libera carga" : $"desembarca para {primaryTarget}";
                            Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — {reason} ({selected.Count} passageiro(s))");
                            return BuildDesembarcarBatch(unit, snapshot.AITeam, fromCell, selected);
                        }
                    }
                }
            }
        }
        } // end else (non-FireSupport)

        // No combat with passengers aboard — delivering is the only priority.

        // Priority 3: move toward target
        float distRemaining = SectorManager.HexDistance(moveTarget, primaryTarget);
        Debug.Log($"{TL("Transporte")} {unit.InstanceId} courier — move para {moveTarget} alvo={primaryTarget} dist={distRemaining:F0}h passageiro=#{primaryPassenger.InstanceId}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveTarget, paths);
    }

}
