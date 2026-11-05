using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Handoff: capturador mais saudável herda objetivo parcial;
    // Cascade: suprime o setor vizinho forward durante distribuição inicial.
    // -------------------------------------------------------------------------

    private void EvaluateCaptureHandoffs(TeamObjectivePlan plan, TeamId aiTeam)
    {
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) assignedIds.Add(slot.AssignedUnitId);

        List<UnitManager> allCapturers = GetAvailableCapturers(aiTeam);
        var freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in allCapturers)
            if (!assignedIds.Contains(u.InstanceId)) freeCapturers.Add(u);

        // No Hard, a retaguarda pode j� estar preenchendo o segundo slot do mesmo objetivo.
        // Portanto, aus�ncia de capturadores livres n�o significa aus�ncia de sucessor.
        if (freeCapturers.Count == 0 && !FazHandoffEmProfundidade) return;

        var handoffHeirs = new HashSet<int>();
        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];

            if (obj.Status == ObjectiveStatus.Defending) continue;

            ConstructionManager target = FindCapturableInSector(obj.Sector, aiTeam);
            if (target == null) continue;

            // A PONTA E QUEM ESTA EM CIMA DO PREDIO, nao a primeira vaga preenchida. O herdeiro
            // entra na primeira vaga livre (TryFillSlot) — muitas vezes a que a ponta acabou de
            // deixar —, e no replanejamento do meio do turno (WorldCommit, a cada jogada pesada)
            // a ordem das vagas invertia os papeis: o herdeiro virava ponta e a ponta, herdeira.
            // Visto no AI Step 2026-11-04: Bravo [#8, #2], o #8 "largou" uma cidade em que nem
            // estava e o #2 ficou parado nela. Ninguem em cima: vale a primeira vaga (como antes).
            Vector3Int targetCell0 = target.CurrentCellPosition; targetCell0.z = 0;
            SlotNeed filledSlot = null;
            bool leaderOnTarget = false;
            foreach (SlotNeed s in obj.Slots)
            {
                if (!s.Filled || s.Role != UnitRole.Capturador) continue;
                if (filledSlot == null) filledSlot = s;
                UnitManager onSlot = FindActiveUnit(s.AssignedUnitId, aiTeam);
                if (onSlot == null) continue;
                Vector3Int slotCell = onSlot.CurrentCellPosition; slotCell.z = 0;
                if (slotCell == targetCell0) { filledSlot = s; leaderOnTarget = true; break; }
            }

            // PONTA DE OUTRO OBJETIVO. Quem abriu o predio pode estar na vaga de outro setor:
            // o #7 em cima da fabrica de Alpha com missao em Delta (AI Step 2026-11-04). Olhar
            // so as vagas DESTE objetivo elegia como ponta alguem fora do predio e deixava o #7
            // capturando — o rabo da fila travava. Com blitz, a unidade em cima do predio e a
            // ponta, seja de qual vaga for; ela NAO perde a vaga dela (segue para o proprio
            // objetivo), e todas as vagas deste objetivo ficam elegiveis como herdeiras.
            UnitManager foreignLeader = null;
            if (!leaderOnTarget && FazHandoffEmProfundidade)
                foreignLeader = FindOwnCapturerAtCell(targetCell0, aiTeam);
            SlotNeed leaderSlot = foreignLeader != null ? null : filledSlot;
            if (leaderSlot == null && foreignLeader == null) continue;

            UnitManager assignedUnit = foreignLeader ?? FindActiveUnit(leaderSlot.AssignedUnitId, aiTeam);
            if (assignedUnit == null || assignedUnit.IsUnderRepair) continue;
            int pts = target.CurrentCapturePoints;
            int max = target.CapturePointsMax;
            if (pts <= 0 || pts >= max) continue;

            if (!HasOpenObjectiveOtherThan(plan, obj)) continue;

            Vector3Int targetCell = target.CurrentCellPosition; targetCell.z = 0;
            // Inimigo colado no predio: largar a captura e arriscar perde-la. Exceto quando o
            // herdeiro FECHA neste turno e a ponta nao — ai trocar e justamente o que tira o predio
            // do perigo (decidido depois da escolha do herdeiro, abaixo).
            bool enemyNearTarget = HasEnemyNearCell(targetCell, aiTeam);

            // "O DE TRAS PAGA." A ponta NUNCA para para fechar: abre o predio e segue, e quem
            // fecha e quem vem atras. Mesmo que a ponta fechasse neste turno, ela reveza — o turno
            // de renda que o seguidor atrasa e o preco do avanco, e e a doutrina do autor. So o
            // ULTIMO da fila paga: sem herdeiro, a ponta termina (fallback abaixo). Uma trava
            // "ponta que fecha nao reveza" chegou a existir aqui e matava o blitz inteiro.
            // CurrentCapturePoints e o que FALTA (a captura subtrai; conclui em 0).
            HandoffReach leaderReach = ResolveHandoffReach(assignedUnit, targetCell, allowNeighbor: false);

            // Blitzkrieg: a ponta de lança pode estar EM CIMA do prédio parcial, bloqueando a
            // célula. Nesse caso o seguidor do eixo nunca "alcança" o alvo (célula ocupada) e o
            // handoff era pulado — a ponta ficava terminando a captura. Se há Foxtrot à frente,
            // aceitamos um seguidor que alcance um VIZINHO do prédio: ele entra assim que a ponta
            // vaga a célula (grp=0), e a ponta segue o eixo. Sem seguidor, mantém o fallback.
            Vector3Int assignedPos0 = assignedUnit.CurrentCellPosition; assignedPos0.z = 0;
            bool frontBlocksTarget = FazHandoffEmProfundidade && assignedPos0 == targetCell
                && ComputeBlitzkriegForwardSector(obj.Sector, aiTeam) != ConstructionSector.None;

            UnitManager substitute   = null;
            SlotNeed substituteExistingSlot = null;
            float       bestSubScore = float.MinValue;
            var slotHolderScores = new Dictionary<int, float>();

            var handoffCandidates = new List<UnitManager>(freeCapturers);
            if (FazHandoffEmProfundidade)
            {
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (!slot.Filled || slot == leaderSlot || slot.Role != UnitRole.Capturador)
                        continue;
                    UnitManager trailing = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (trailing != null && !trailing.IsUnderRepair && !handoffCandidates.Contains(trailing))
                        handoffCandidates.Add(trailing);
                }
            }

            // FILA DO EIXO: com blitz, quem vem pelo MESMO eixo herda mesmo estando na vaga de
            // outro objetivo. O plano diz PARA ONDE cada um vai; a posicao no eixo diz QUEM
            // REVEZA COM QUEM. Antes so o mesmo objetivo herdava, e o seguidor de Foxtrot passava
            // ao lado da cidade de Bravo sem poder assumi-la. A ajuda LATERAL (outro eixo) fica de
            // fora de proposito: ela ja e coberta pelo capturador oportunista.
            // So com a ponta EM CIMA do predio (frontBlocksTarget): e ela que "passa pelo
            // sorveteiro". Sem ninguem em cima — por exemplo no replanejamento depois que a ponta
            // ja saiu — a primeira vaga e o proprio herdeiro, e a fila o repassaria a um terceiro
            // antes de ele pisar no predio.
            var axisOrigin = new Dictionary<int, KeyValuePair<SectorObjective, SlotNeed>>();
            if (frontBlocksTarget)
                CollectSameAxisHandoffCandidates(plan, obj, assignedUnit, aiTeam, handoffCandidates, axisOrigin);

            foreach (UnitManager candidate in handoffCandidates)
            {
                // Herdeiro ja prometido a outro predio nesta passada nao e roubado: o #16 herdou
                // Golf (fechava) e a fila do eixo o levou para Foxtrot logo em seguida.
                if (handoffHeirs.Contains(candidate.InstanceId)) continue;
                if (!SimulateCaptureSensor(candidate, targetCell, out _)) continue;

                SlotNeed candidateExistingSlot = null;
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled && slot.Role == UnitRole.Capturador
                        && slot.AssignedUnitId == candidate.InstanceId)
                    {
                        candidateExistingSlot = slot;
                        break;
                    }
                bool sameAxis = axisOrigin.ContainsKey(candidate.InstanceId);

                // Alcance pela BANDA da propria unidade, nunca por hex: 4 hexes de montanha nao
                // sao 4 hexes de estrada, e o Bazooka de MP 2 tem banda menor sem por isso ficar
                // fora da passagem. Com a ponta em cima do predio, vale chegar num vizinho.
                HandoffReach reach = ResolveHandoffReach(candidate, targetCell, allowNeighbor: frontBlocksTarget);
                // HERDEIRO TEM DE CAPTURAR NO TATICO — de qualquer origem. "O de tras paga" so
                // vale se o de tras chega ao sorveteiro: a ponta nao larga o predio e foda-se.
                // Antes vaga do objetivo herdava sem alcance e fila do eixo no Operacional; no
                // AI Step 2026-11-04 o #5 largou Foxtrot (10/20, inimigos perto) para o #16, que
                // so chegava no turno seguinte — e o #16 ainda foi tirado de Golf, onde fecharia.
                if (!reach.Tactical)
                    continue;

                int capturePower = PodeCapturarSensor.GetCapturePower(
                    candidate,
                    target,
                    matchController);
                // CurrentCapturePoints e o que FALTA. A conta antiga (pts + poder >= max)
                // dizia "completa" numa fabrica 20/30 com poder 10, a dois turnos do fim.
                bool completesCapture = reach.Tactical && capturePower >= pts;

                // "O de tras paga": herdeiro que esta A FRENTE do predio e volta para pega-lo
                // devolve o avanco que ja tinha. Vem de tras = esta na base (raiz dos eixos) ou
                // mais perto do proprio QG do que o predio. Visto no AI Step 2026-11-04: o #9, a
                // frente da fabrica de Alpha, voltou um hex com o #13 e o #14 parados na base.
                bool comesFromBehind = IsStandingInOwnBase(candidate)
                    || IsBehindBuildingTowardOwnHQ(candidate, targetCell);

                // Ordem lexicografica, cada degrau domina os de baixo:
                //   1. chega neste turno (Tatico)        2. vem de tras (base ou mais perto do QG)
                //   3. fecha a captura neste turno       4. poder de captura (desempate do autor
                //   entre Soldado e Bazooka no Tatico)   5. mesmo eixo   6. menor custo REAL
                float score = (reach.Tactical ? 10000000f : 0f)
                            + (comesFromBehind ? 1000000f : 0f)
                            + (completesCapture ? 100000f : 0f)
                            + Mathf.Clamp(capturePower, 0, 99) * 1000f
                            + (sameAxis ? 300f : 0f)
                            - Mathf.Clamp(reach.Cost, 0, 299);
                Debug.Log($"{TL("Handoff")}[Candidato] {obj.Sector} Unit{candidate.InstanceId} " +
                          $"tatico={reach.Tactical} operacional={reach.Operational} deTras={comesFromBehind} " +
                          $"fecha={completesCapture} poder={capturePower} eixo={sameAxis} custo={reach.Cost} " +
                          $"origem={(candidateExistingSlot != null ? "vaga do objetivo" : sameAxis ? "fila do eixo/base" : "livre")} " +
                          $"score={score:F0}");
                if (candidateExistingSlot != null)
                    slotHolderScores[candidate.InstanceId] = score;

                if (score > bestSubScore)
                {
                    bestSubScore = score;
                    substitute = candidate;
                    substituteExistingSlot = candidateExistingSlot;
                }
            }

            // TROCA POR PODER DE CAPTURA. O lema do capturador e dinheiro rapido: com inimigo
            // perto, so vale revezar se o herdeiro FECHA neste turno e a ponta nao fecharia. A
            // ponta (ferida, poder menor) sai e luta — o ataque da ponta (Blitzkrieg.cs) ja prefere
            // quem ameaca o predio. AI Step 2026-11-04: Echo 10/20, #4 HP 7 (poder 7) em cima,
            // #10 HP 10 chegava no Tatico; esta trava calava o handoff e o #10 so andou ao lado.
            if (enemyNearTarget)
            {
                int leaderPower = PodeCapturarSensor.GetCapturePower(assignedUnit, target, matchController);
                bool heirCloses = substitute != null
                    && ResolveHandoffReach(substitute, targetCell, allowNeighbor: frontBlocksTarget).Tactical
                    && PodeCapturarSensor.GetCapturePower(substitute, target, matchController) >= pts;
                if (!heirCloses || leaderPower >= pts)
                {
                    Debug.Log($"{TL("Handoff")}[Skip] {obj.Sector}: inimigo colado no predio ({pts}/{max}) e " +
                              (substitute == null ? "sem herdeiro no Tatico"
                               : leaderPower >= pts ? $"a ponta fecha sozinha (poder {leaderPower})"
                               : $"Unit{substitute.InstanceId} nao fecha neste turno") +
                              " — ponta termina a captura");
                    continue;
                }
                Debug.Log($"{TL("Handoff")}[Poder] {obj.Sector}: inimigo colado, mas Unit{substitute.InstanceId} fecha " +
                          $"({pts} faltando) e a ponta Unit{assignedUnit.InstanceId} (poder {leaderPower}) nao — troca e sai para lutar");
            }

            // Herdeiro do mesmo eixo vinha de OUTRA vaga: ela fica livre, como na troca. A ponta
            // segue pelo eixo e tende a cair justamente no setor que ele deixou.
            if (substitute != null && substituteExistingSlot == null
                && axisOrigin.TryGetValue(substitute.InstanceId, out KeyValuePair<SectorObjective, SlotNeed> origin))
            {
                origin.Value.Filled         = false;
                origin.Value.AssignedUnitId = -1;
                Debug.Log($"{TL("Handoff")}[Eixo] Unit{substitute.InstanceId} deixa {origin.Key.Sector} " +
                          $"para herdar {obj.Sector} no mesmo eixo");
            }

            if (substitute == null)
            {
                // Troca = resgate de ponta que NAO chega: so vale quando ela fica fora do proprio
                // Tatico e o candidato chega no dele — ou seja, economiza pelo menos um turno.
                // Antes era "ponta a 4h ou mais", e 4 hexes de montanha nao sao 4 de estrada.
                UnitManager     swapCandidate = null;
                SectorObjective swapFromObj   = null;
                SlotNeed        swapFromSlot  = null;

                if (!leaderReach.Tactical)
                {
                    foreach (SectorObjective otherObj in plan.Objectives)
                    {
                        if (otherObj == obj) continue;
                        if (otherObj.Priority < obj.Priority) continue;

                        foreach (SlotNeed otherSlot in otherObj.Slots)
                        {
                            if (!otherSlot.Filled || otherSlot.Role != UnitRole.Capturador) continue;
                            UnitManager cand = FindActiveUnit(otherSlot.AssignedUnitId, aiTeam);
                            if (cand == null || cand.IsUnderRepair) continue;
                            if (!SimulateCaptureSensor(cand, targetCell, out _)) continue;

                            var swapPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                                boardTilemap, cand,
                                Mathf.Max(0, cand.RemainingMovementPoints), terrainDatabase);
                            if (swapPaths == null || !swapPaths.ContainsKey(targetCell)) continue;

                            swapCandidate = cand;
                            swapFromObj   = otherObj;
                            swapFromSlot  = otherSlot;
                            break;
                        }
                        if (swapCandidate != null) break;
                    }
                }

                if (swapCandidate == null)
                {
                    Debug.Log($"{TL("Handoff")}[Skip] sem substituto para {obj.Sector} (faltam {pts}/{max})" +
                              $" ponta {(leaderReach.Tactical ? "chega neste turno" : leaderReach.Operational ? "chega no proximo" : "fora do Operacional")}");
                    continue;
                }

                ReleaseHandoffLeader(leaderSlot, assignedUnit);
                plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
                ConstructionSector fwdSec = FazHandoffEmProfundidade
                    ? ComputeBlitzkriegForwardSector(obj.Sector, aiTeam)
                    : ComputeForwardNeighborSector(obj.Sector, aiTeam);
                // None, nao default: Compute*ForwardSector devolve None quando nao acha,
                // e default(ConstructionSector) e Alpha — aceitava None e recusava Alpha.
                if (fwdSec != ConstructionSector.None) plan.VacaterForwardSectors.Add(fwdSec);

                swapFromSlot.Filled         = false;
                swapFromSlot.AssignedUnitId = -1;

                obj.TryFillSlot(UnitRole.Capturador, swapCandidate.InstanceId);
                obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
                obj.HandoffEligible            = true;
                obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
                obj.HandoffHeirUnitId          = swapCandidate.InstanceId;
                ApplyPlanHUD(swapCandidate, obj);

                bool swapCompletes = PodeCapturarSensor.GetCapturePower(
                    swapCandidate,
                    target,
                    matchController) >= pts;
                Debug.Log($"{TL("Handoff")}[Swap] Unit{swapCandidate.InstanceId} " +
                          $"({swapFromObj.Sector} pri={swapFromObj.Priority}→{obj.Sector} pri={obj.Priority}) " +
                          $"já no prédio; Unit{assignedUnit.InstanceId} livre (nao chegava neste turno)" +
                          $"{(swapCompletes ? " completa captura" : "")}");
                continue;
            }

            bool subCompletes = PodeCapturarSensor.GetCapturePower(
                substitute,
                target,
                matchController) >= pts;
            Debug.Log($"{TL("Handoff")} Unit{assignedUnit.InstanceId} hp={assignedUnit.CurrentHP} avança; " +
                      $"Unit{substitute.InstanceId} hp={substitute.CurrentHP} herda {obj.Sector} " +
                      $"({pts}/{max}){(subCompletes ? " → completa" : "")}" +
                      $"{(frontBlocksTarget ? " [blitz: ponta sai de cima, seguidor do eixo assume]" : "")}");

            ReleaseHandoffLeader(leaderSlot, assignedUnit);
            plan.HandoffVacaterIds.Add(assignedUnit.InstanceId);
            handoffHeirs.Add(substitute.InstanceId);
            // Para onde a ponta segue depois de ceder e parte da mesma capacidade que a
            // deixou ceder — nao da dificuldade.
            ConstructionSector fwdSector = FazHandoffEmProfundidade
                ? ComputeBlitzkriegForwardSector(obj.Sector, aiTeam)
                : ComputeForwardNeighborSector(obj.Sector, aiTeam);
            if (fwdSector != ConstructionSector.None) plan.VacaterForwardSectors.Add(fwdSector);

            if (substituteExistingSlot == null
                && !obj.TryFillSlot(UnitRole.Capturador, substitute.InstanceId))
            {
                // VAGAS CHEIAS. Com ponta de OUTRO objetivo nenhuma vaga daqui foi liberada, e o
                // TryFillSlot falhava calado: o herdeiro (#13, na base) ficava livre, era atribuido
                // a outro setor mais adiante, e o ocupante da vaga (#9, a frente) voltava para
                // capturar (AI Step 2026-11-04). O herdeiro toma a vaga do PIOR colocado entre os
                // ocupantes que perderam para ele; esse fica livre para seguir em frente.
                SlotNeed evicted = null;
                float worst = float.MaxValue;
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (!slot.Filled || slot.Role != UnitRole.Capturador) continue;
                    if (!slotHolderScores.TryGetValue(slot.AssignedUnitId, out float holderScore)) continue;
                    if (holderScore < worst) { worst = holderScore; evicted = slot; }
                }
                if (evicted != null)
                {
                    UnitManager released = FindActiveUnit(evicted.AssignedUnitId, aiTeam);
                    Debug.Log($"{TL("Handoff")}[Vaga] {obj.Sector} cheio: Unit{substitute.InstanceId} toma a vaga " +
                              $"de Unit{evicted.AssignedUnitId}, que fica livre para avancar");
                    released?.ClearAIAssignedPlan();
                    evicted.AssignedUnitId = substitute.InstanceId;
                }
                else
                {
                    Debug.Log($"{TL("Handoff")}[Vaga] {obj.Sector} cheio e sem ocupante para ceder: " +
                              $"Unit{substitute.InstanceId} herda sem vaga");
                }
            }
            obj.Status                     = ObjectiveStatus.PartialReadyForHandoff;
            obj.HandoffEligible            = true;
            obj.PreferredHandoffFromUnitId = assignedUnit.InstanceId;
            obj.HandoffHeirUnitId          = substitute.InstanceId;
            ApplyPlanHUD(substitute, obj);

            if (substituteExistingSlot == null)
                freeCapturers.Remove(substitute);
            assignedIds.Add(substitute.InstanceId);
        }
    }

    // Alcance máximo (hexes entre setores) para a cascata suprimir o vizinho. Equivale à
    // "ponte" de mover ~3 hexes e capturar; acima disso o vizinho precisa da própria unidade.
    private const float MaxCascadeBridgeDistance = 3f;

    private void MarkCascadeNeighbor1(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, HashSet<ConstructionSector> vacaterProtected = null, AIRallyPlanContext rallyContext = default)
    {
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return;

        float myHQDist = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));

        ConstructionSector candidate     = ConstructionSector.None;
        float              candidateDist = float.MaxValue;

        if (info.ClosestNeighbor1 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) > myHQDist)
        {
            candidate     = info.ClosestNeighbor1;
            candidateDist = info.ClosestNeighbor1Distance;
        }
        else if (info.ClosestNeighbor2 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) > myHQDist)
        {
            candidate     = info.ClosestNeighbor2;
            candidateDist = info.ClosestNeighbor2Distance;
        }

        if (candidate == ConstructionSector.None) return;

        // A cascata só pode cobrir o vizinho se ele estiver dentro do alcance de "ponte"
        // (a unidade captura o setor atual e no(s) turno(s) seguinte(s) faz a ponte de
        // ~3 hexes até o vizinho). Acima disso o vizinho precisa da própria unidade —
        // suprimi-lo deixa capturadores ociosos virando rogue.
        if (candidateDist > MaxCascadeBridgeDistance)
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h) fora do alcance de ponte (>{MaxCascadeBridgeDistance:F0}h), não cobre");
            return;
        }

        if (vacaterProtected != null && vacaterProtected.Contains(candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} protegido por vacater, supressão ignorada");
            return;
        }

        if (IsEnemyHQRallySector(rallyContext, candidate))
        {
            Debug.Log($"{TL("Plan")} cascata: {sector} -> {candidate} ignorada; rally point de invasao");
            return;
        }

        covered.Add(candidate);
        Debug.Log($"{TL("Plan")} cascata: {sector} → {candidate} ({candidateDist:F1}h)");
    }

    private void MarkSelectionCascadeNeighbor(ConstructionSector sector, HashSet<ConstructionSector> covered, TeamId aiTeam, AIRallyPlanContext rallyContext = default)
    {
        if (covered == null)
            return;

        ConstructionSector forward = ComputeForwardNeighborSector(sector, aiTeam);
        if (forward == ConstructionSector.None)
            return;

        if (IsEnemyHQRallySector(rallyContext, forward))
        {
            Debug.Log($"{TL("Plan")} cascata inicial: {sector} nao cobre {forward}; rally point de invasao");
            return;
        }

        covered.Add(forward);
        Debug.Log($"{TL("Plan")} cascata inicial: {sector} cobre {forward}");
    }

    private static ConstructionSector ComputeForwardNeighborSector(ConstructionSector sector, TeamId aiTeam)
    {
        if (TryGetPrimaryCampaignSuccessor(sector, aiTeam, out ConstructionSector campaignForward))
            return campaignForward;

        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info)) return ConstructionSector.None;
        float myHQDist = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        if (info.ClosestNeighbor1 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) > myHQDist)
            return info.ClosestNeighbor1;
        if (info.ClosestNeighbor2 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) > myHQDist)
            return info.ClosestNeighbor2;
        return ConstructionSector.None;
    }

    private static bool HasOpenObjectiveOtherThan(TeamObjectivePlan plan, SectorObjective exclude)
    {
        foreach (SectorObjective obj in plan.Objectives)
            if (obj != exclude && obj.HasOpenSlot(UnitRole.Capturador)) return true;
        return false;
    }

    // Verdadeiro se o candidato consegue parar num hex adjacente ao prédio parcial neste turno.
    // Usado no handoff blitz quando a ponta de lança ocupa a própria célula do alvo: o seguidor
    // não pode parar sobre o prédio (ocupado), mas fica pronto ao lado para assumir assim que a
    // ponta vaga. A célula do alvo em si é liberada em Phase 2 (a ponta é grp=0 e sai primeiro).
    private readonly List<Vector3Int> handoffNeighborBuffer = new List<Vector3Int>(6);
    // Alcance de um capturador ate o predio do revezamento, pela banda DELE (envelope de
    // captura, Operacional encadeado: o turno atual vale o MP que sobra, os seguintes o MP
    // cheio). Tactical = chega neste turno. Celula ocupada por outra unidade nao vale como
    // parada — mesma regra do caminho antigo; com a ponta em cima, vale um vizinho livre.
    private struct HandoffReach
    {
        public bool Tactical;
        public bool Operational;
        public int Cost;
    }

    private HandoffReach ResolveHandoffReach(UnitManager unit, Vector3Int targetCell, bool allowNeighbor)
    {
        var result = new HandoffReach { Cost = int.MaxValue };
        if (unit == null)
            return result;

        Vector3Int here = unit.CurrentCellPosition; here.z = 0;
        targetCell.z = 0;
        if (here == targetCell)
        {
            result.Tactical = true;
            result.Operational = true;
            result.Cost = 0;
            return result;
        }

        UnitReachEnvelope envelope = UnitReachEnvelopeService.Build(new UnitReachRequest
        {
            Unit = unit,
            BoardMap = boardTilemap,
            TerrainDatabase = terrainDatabase,
            Intent = ReachIntent.Capture,
            Band = ReachBand.Operational,
            OperationalTurns = 2,
            IncludeMovementCosts = true
        });
        if (envelope == null)
            return result;

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        int bestTurns = int.MaxValue;
        void Consider(Vector3Int cell)
        {
            cell.z = 0;
            if (occupied.Contains(cell) || !envelope.MovementCells.Contains(cell))
                return;
            if (!envelope.TryGetTurns(cell, out int turns))
                return;
            int cost = envelope.CostByCell.TryGetValue(cell, out int c) ? c : int.MaxValue - 1;
            if (turns < bestTurns || (turns == bestTurns && cost < result.Cost))
            {
                bestTurns = turns;
                result.Cost = cost;
            }
        }

        Consider(targetCell);
        if (allowNeighbor)
        {
            UnitMovementPathRules.GetImmediateHexNeighbors(boardTilemap, targetCell, handoffNeighborBuffer);
            foreach (Vector3Int neighbor in handoffNeighborBuffer)
                Consider(neighbor);
        }

        result.Tactical = bestTurns <= 1;
        result.Operational = bestTurns <= 2;
        return result;
    }

    // Capturadores com vaga em OUTRO objetivo cujo setor esta no mesmo eixo do predio. Ficam
    // de fora: a propria ponta, quem esta em reparo, quem ja cedeu (vacater) e quem esta
    // ocupado capturando outro predio pela metade — tirar esse dali seria abrir outro buraco.
    private void CollectSameAxisHandoffCandidates(
        TeamObjectivePlan plan, SectorObjective obj, UnitManager leader, TeamId aiTeam,
        List<UnitManager> candidates, Dictionary<int, KeyValuePair<SectorObjective, SlotNeed>> origin)
    {
        InvasionAxisMap axisMap = InvasionAxisMap.Build(
            PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)), boardTilemap);
        // Sem eixo, a fila ainda tem raiz: so a base entra.
        int axis = axisMap.GetEixo(obj.Sector);

        foreach (SectorObjective other in plan.Objectives)
        {
            if (other == obj || other.Status == ObjectiveStatus.Defending)
                continue;
            bool otherSameAxis = axis > 0 && axisMap.GetEixo(other.Sector) == axis;
            foreach (SlotNeed slot in other.Slots)
            {
                if (!slot.Filled || slot.Role != UnitRole.Capturador
                    || slot.AssignedUnitId == leader.InstanceId
                    || plan.HandoffVacaterIds.Contains(slot.AssignedUnitId))
                    continue;
                UnitManager member = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (member == null || member.IsUnderRepair || candidates.Contains(member)
                    || IsBusyOnPartialCapture(member))
                    continue;
                if (!otherSameAxis && !IsStandingInOwnBase(member))
                    continue;
                candidates.Add(member);
                origin[member.InstanceId] = new KeyValuePair<SectorObjective, SlotNeed>(other, slot);
            }
        }
    }

    // A ponta da casa perde a vaga do objetivo que cedeu e fica livre para avancar pelo eixo.
    // A ponta de OUTRO objetivo (leaderSlot == null) so passou pelo predio: mantem a vaga e a
    // missao dela, e o blitz a manda para o proprio objetivo.
    private static void ReleaseHandoffLeader(SlotNeed leaderSlot, UnitManager leader)
    {
        if (leaderSlot == null)
            return;
        leaderSlot.Filled         = false;
        leaderSlot.AssignedUnitId = -1;
        leader.ClearAIAssignedPlan();
    }

    private UnitManager FindOwnCapturerAtCell(Vector3Int cell, TeamId aiTeam)
    {
        cell.z = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.IsUnderRepair) continue;
            if (unit.TeamId != aiTeam || !unit.TryGetUnitData(out UnitData data)
                || !UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador))
                continue;
            Vector3Int c = unit.CurrentCellPosition; c.z = 0;
            if (c == cell) return unit;
        }
        return null;
    }

    // A BASE E A RAIZ DOS TRES EIXOS: quem esta em cima de uma construcao propria do setor
    // base esta "atras" em qualquer eixo — inclusive quem acabou de nascer num produtor.
    // Visto no AI Step: Foxtrot (eixo 2) parado no QG podia herdar a fabrica de Alpha (eixo 3).
    private bool IsStandingInOwnBase(UnitManager unit)
    {
        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return building != null && building.SlotIndex == unit.SlotIndex
            && ConstructionSectorHelper.IsBase(building.Sector);
    }

    // "Atras" e geometria do leque, nao custo de movimento: o eixo nasce no QG e cresce para
    // fora, entao quem esta mais perto do proprio QG do que o predio esta atras dele.
    private static bool IsBehindBuildingTowardOwnHQ(UnitManager unit, Vector3Int buildingCell)
    {
        ConstructionManager hq = null;
        foreach (ConstructionManager c in ConstructionManager.AllActive)
            if (c != null && c.IsPlayerHeadQuarter && c.SlotIndex == unit.SlotIndex) { hq = c; break; }
        if (hq == null)
            return false;
        Vector3Int hqCell = hq.CurrentCellPosition; hqCell.z = 0;
        Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
        buildingCell.z = 0;
        return SectorManager.HexDistance(unitCell, hqCell) < SectorManager.HexDistance(buildingCell, hqCell);
    }

    private bool IsBusyOnPartialCapture(UnitManager unit)
    {
        Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
        ConstructionManager building = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        return building != null && building.IsCapturable
            && building.CurrentCapturePoints > 0
            && building.CurrentCapturePoints < building.CapturePointsMax
            && building.SlotIndex != unit.SlotIndex;
    }
}
