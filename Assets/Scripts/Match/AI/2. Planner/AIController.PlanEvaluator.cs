using System.Collections.Generic;
using UnityEngine;

// --------------------------------------------------------------------------------------------
// Top Tier do Comando de IA: Planejamento de Objetivos de Captura
// Orquestra os 8 passos de BuildObjectivePlan � sele��o, atribui��o, defesa, handoff.
// Implementa��es de suporte: PlanEvaluator.MacroContext / Defense / Handoff /
//                            SectorScoring / Helpers
// --------------------------------------------------------------------------------------------

public partial class AIController
{
    // Mapa de eixos de invasao do turno corrente (setor -> eixo 1..N). Descritivo:
    // alimenta o aiEixo das unidades via ApplyPlanHUD. Reconstruido a cada BuildObjectivePlan.
    private InvasionAxisMap currentAxisMap;

    // Exposto para a fase de shopping (AITacticalAnalyzer) reusar o mapa do turno corrente
    // em vez de reconstruir. Pode ser null antes do primeiro BuildObjectivePlan; o consumidor
    // confere o Team e cai para InvasionAxisMap.Build se nao bater.
    public InvasionAxisMap CurrentAxisMap => currentAxisMap;

    // R1 (a ponta avanca): bonus de prioridade no setor-frente de cada eixo, para empurrar
    // o corredor como linha conectada em vez de pegar setores espalhados. Abaixo do rally (+55).
    private const int AxisFrontPriorityBonus = 30;
    // Estabilidade de eixo: custo extra para uma unidade trocar do seu eixo atual para outro
    // que ja esta igual/mais coberto. Acima do bonus de vizinho (-6); distancia/handoff grande
    // ainda pode vencer. Rogue (aiEixo<=0) e alvo-fora-de-eixo nao pagam. Dial de ajuste.
    private const float AxisStabilitySwitchCost = 10f;
    // Recompensa (custo negativo) para uma unidade trocar para um eixo FAMINTO (>=2 a menos que
    // o atual): rebalanceia, puxando alguem para cobrir um eixo vazio em vez de lotar o cheio.
    private const float AxisStarvedCoverReward = 10f;
    // Diferenca minima de presenca (atual - alvo) para considerar o eixo-alvo faminto.
    private const int AxisStarvedPresenceGap = 2;
    // Histerese: bonus (custo negativo) por reatribuir um capturador ao MESMO objetivo/setor que ele
    // tinha no turno anterior. Evita que o otimizador global embaralhe quem ja estava a caminho de um
    // setor (ex.: chegando em Charlie e ser mandado pra Bravo do outro lado). Forte o bastante pra
    // vencer trocas globais marginais; distancia/necessidade real ainda podem quebrar. Dial de ajuste.
    private const float PreviousObjectiveHysteresisBonus = 15f;

    // Hard Mode dobra a demanda de capturadores por setor, mas com teto proprio para
    // aumentar pressao territorial sem explodir setores grandes em demanda absurda de
    // infantaria (cap normal 4 -> teto hard 6).
    private const int HardModeCapturerSlotCap = 6;

    // Plano de invasão da base inimiga (">>") — captura FINAL: precisa das PEÇAS FORTES (assalto +
    // fogo indireto, atendidos pelo reserve de elite do shopping) E de MASSA barata com suporte. O
    // modelo de gasto é: as elites são atendidas primeiro; o que sobra do orçamento inunda
    // capturador (massa, SEM teto — escala com a base), transporte (leva a massa) e logística
    // (sustenta). Por isso assalto/fogo são demanda MODESTA (não 1:1 com o capturador).
    private const int BaseInvasionEliteAssaultSlots = 2;        // peça forte: ruptura
    private const int BaseInvasionEliteFireSupportSlots = 2;    // peça forte: amaciamento
    private const int BaseInvasionLogisticsSlots = 1;          // sustenta a invasão na frente
    private const int BaseInvasionTransportCapacity = 2;       // capturadores por APC (ferry da massa)

    // Presenca por eixo (eixo 1..N -> nº de unidades do time com aquele aiEixo). Snapshot do
    // campo no inicio do BuildObjectivePlan, usado para decidir rebalanceamento de eixo.
    private Dictionary<int, int> currentEixoPresence;
    // Setor do objetivo de cada unidade NO TURNO ANTERIOR (unitId -> nome do setor). Snapshot tirado
    // no topo do BuildObjectivePlan, ANTES das liberacoes limparem o AIAssignedPlan. Usado pela
    // histerese de atribuicao pra manter o capturador no objetivo pra onde ja estava indo.
    private Dictionary<int, string> previousUnitObjectiveSectorName;

    // Garante que TODO objetivo de base inimiga no plano esteja marcado InvasionAttack — inclusive
    // os já existentes (criados antes desta marcação ou restaurados de save antigo), que o Passo 2c
    // não re-marca (ele só cria objetivos novos). Roda antes de construir o mapa de eixos para o 4º
    // eixo (invasão) aparecer imediatamente, no fluxo normal e no resume. Critério canônico de base
    // inimiga, igual ao monitor de invasão e à normalização de fogo.
    private static void MarkEnemyBaseObjectivesAsInvasion(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan?.Objectives == null)
            return;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.ObjectiveType == AIObjectiveType.InvasionAttack)
                continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector) && FindHQTeamInSector(obj.Sector) != aiTeam)
                obj.ObjectiveType = AIObjectiveType.InvasionAttack;
        }
    }

    private void BuildObjectivePlan(AIWorldSnapshot snapshot)
    {
        TeamId aiTeam = snapshot.AITeam;
        PlayerSlotId aiSlot = PlayerSlotId.FromIndex(snapshot.AISlotIndex);
        if (matchController != null && matchController.IsSlotRebel(aiSlot))
        {
            TeamObjectivePlan previousPlan = ObjectiveManager.GetOrCreatePlanForSlot(aiSlot, aiTeam);
            foreach (SectorObjective objective in previousPlan.Objectives)
                ClearObjectiveHUD(objective);
            ObjectiveManager.ClearPlanForSlot(aiSlot);
            currentAxisMap = null;
            Debug.Log($"{TL("Plan")} slot={aiSlot.Value} rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.");
            return;
        }

        TeamObjectivePlan plan = ObjectiveManager.GetOrCreatePlanForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)), aiTeam);
        MarkEnemyBaseObjectivesAsInvasion(plan, aiTeam);
        currentAxisMap = InvasionAxisMap.Build(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        // Strong/Weak Side Politic (prototipo, flag off por padrao): classifica os eixos e
        // deixa o vies de elite pronto ANTES da atribuicao de fire support mais abaixo.
        UpdateAxisSidePolitics(snapshot);
        // Presenca por eixo (quantas unidades em cada eixo AGORA, incluindo as que vao dar
        // handoff). Construido antes dos releases para a contagem refletir o campo atual.
        BuildEixoPresence(aiTeam);
        // Snapshot do objetivo anterior de cada unidade, ANTES das liberacoes deste turno limparem o
        // AIAssignedPlan — a histerese usa isso pra nao arrancar quem ja estava a caminho do setor.
        BuildPreviousObjectiveSnapshot(aiTeam);
        AIIntelReport intel = BuildPlanIntelReport(snapshot);
        AIRallyPlanContext rallyContext = BuildRallyPlanContext(
            aiTeam,
            snapshot.AISlotIndex,
            snapshot.TurnNumber,
            intel);
        AIAnchorPlanContext anchorContext = BuildAnchorPlanContext(aiTeam, snapshot.TurnNumber);

        // Monitora a invasão em voo (sobre o plano do turno anterior) ANTES do Passo 1 ler a
        // supressão: se a operação fracassou (colapso/estagnação), limpa o GoGreen aqui pra que o
        // Passo 1 já reabra a montagem da 2ª onda neste mesmo turno.
        UpdateInvasionMonitor(plan, aiTeam, snapshot);

        // A invasao (base inimiga ">>") so persiste enquanto a AI GOVERNA algum rally. Sem rally
        // held, o objetivo de invasao e dissolvido no Passo 1 e suas unidades voltam ao pool.
        bool anyRallyHeld = AnyOwnedRallyHeld(aiTeam, rallyContext);
        // ...MAS so quando o gate e satisfazivel. Sem nenhum rally designado para este SLOT,
        // anyRallyHeld e false pra sempre e a invasao era criada e dissolvida todo turno, num ciclo
        // que nunca convergia. Ver MapHasAnyRallyPointForSlot: distingue "ainda nao conquistei" de
        // "nao existe rally meu neste mapa".
        bool rallyGateApplicableForDissolve = MapHasAnyRallyPointForSlot(snapshot.AISlotIndex);

        // Passo 1: valida objetivos existentes
        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];

            // Invasao (base inimiga ">>") sem rally governado: dissolve e LIBERA as unidades para
            // outros planos que precisem delas (em vez de marcharem sozinhas ate o QG inimigo).
            if (rallyGateApplicableForDissolve
                && !anyRallyHeld
                && ConstructionSectorHelper.IsBase(obj.Sector)
                && FindHQTeamInSector(obj.Sector) != aiTeam)
            {
                Debug.Log($"{TL("Plan")} invasao {obj.Sector} dissolvida: AI nao governa nenhum rally; libera unidades");
                foreach (SlotNeed freed in obj.Slots)
                    if (freed.Filled)
                        FindActiveUnit(freed.AssignedUnitId, aiTeam)?.ClearAIAssignedPlan();
                ClearObjectiveHUD(obj);
                plan.Objectives.RemoveAt(i);
                continue;
            }

            bool ownedRallyHeld = IsEnemyHQRallySectorHeld(rallyContext, obj.Sector, aiTeam);
            bool goGreenSuppressed = ownedRallyHeld && IsRallyGoGreenSuppressed(
                PlayerSlotId.FromIndex(snapshot.AISlotIndex), obj.Sector, snapshot.TurnNumber);
            bool objectiveIsRallySector = ownedRallyHeld && !goGreenSuppressed;
            if (objectiveIsRallySector && obj.ObjectiveType == AIObjectiveType.CaptureSector)
                obj.ObjectiveType = AIObjectiveType.RallyAssembly;
            else if (!objectiveIsRallySector && obj.ObjectiveType == AIObjectiveType.RallyAssembly)
            {
                obj.ObjectiveType = AIObjectiveType.CaptureSector;
                Debug.Log($"{TL("Plan")} rally {obj.Sector} REBAIXADO p/ captura: ownedHeld={ownedRallyHeld} goGreenSuppressed={goGreenSuppressed}");
            }

            if (!IsRallyAssemblyObjective(obj))
                NormalizeStandardObjectiveFireSupportSlots(obj, aiTeam);

            if (IsRallyAssemblyObjective(obj)
                && TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager existingRally))
            {
                UpdateRallyObjectiveState(obj, existingRally, aiTeam, snapshot.TurnNumber, intel);
                Debug.Log($"{TL("Plan")} rally {obj.Sector}: rallyState={obj.RallyState} {obj.RallyReadinessReason}");
                if (IsRallyGoGreenObjective(obj))
                {
                    Debug.Log($"{TL("Plan")} rally {obj.Sector}: GO_GREEN libera montagem e retorna ao plano ofensivo");
                    ClearObjectiveHUD(obj);
                    plan.Objectives.RemoveAt(i);
                    continue;
                }
            }

            if (obj.Status == ObjectiveStatus.Defending
                && TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo ownedCheckInfo)
                && !IsOwnedDefensibleSector(ownedCheckInfo, aiTeam))
            {
                if (FindCapturableInSector(obj.Sector, aiTeam) == null)
                {
                    Debug.Log($"{TL("Plan")} defesa obsoleta removida: {obj.Sector} nao pertence mais ao time e nao tem capturavel");
                    ClearObjectiveHUD(obj);
                    plan.Objectives.RemoveAt(i);
                    continue;
                }

                Debug.Log($"{TL("Plan")} defesa obsoleta convertida: {obj.Sector} owner={ownedCheckInfo.ControllingTeam} -> Pursuing");
                ConvertStaleDefenseToCaptureObjective(obj, aiTeam);
            }

            if (FindCapturableInSector(obj.Sector, aiTeam) == null)
            {
                if (IsRallyAssemblyObjective(obj) && IsEnemyHQRallySectorHeld(rallyContext, obj.Sector, aiTeam))
                {
                    if (obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                        obj.Status = ObjectiveStatus.Pending;
                    continue;
                }

                bool wasCaptureObjective = IsCaptureProgressStatus(obj.Status);
                if (wasCaptureObjective)
                    RememberRecentlyCapturedSector(aiTeam, obj.Sector, snapshot.TurnNumber);
                bool recentlyCaptured = IsRecentlyCapturedSector(aiTeam, obj.Sector, snapshot.TurnNumber);
                if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInf)
                    && IsOwnedDefensibleSector(defInf, aiTeam)
                    && (recentlyCaptured
                        || HasNearbyVisibleEnemy(defInf.RepresentativeCell, aiTeam, defenseEnemyRange)
                        || defInf.IsDisputed
                        || defInf.HasPartialCapture
                        || (IsCriticalHomeDefenseSector(defInf, aiTeam)
                            && IsHomeDefenseThreatened(defInf, aiTeam, HomeDefenseThreatRange))))
                {
                    obj.Status = ObjectiveStatus.Defending;
                    for (int s = obj.Slots.Count - 1; s >= 0; s--)
                    {
                        SlotNeed slot = obj.Slots[s];
                        if (!slot.Filled || slot.Role == UnitRole.Transportador)
                        {
                            if (slot.Filled)
                            {
                                UnitManager transUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                                transUnit?.ClearAIAssignedPlan();
                            }
                            obj.Slots.RemoveAt(s);
                            continue;
                        }
                        UnitManager slotUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                        if (slotUnit == null || slotUnit.IsUnderRepair)
                        {
                            slotUnit?.ClearAIAssignedPlan();
                            obj.Slots.RemoveAt(s);
                        }
                    }
                    if (obj.Slots.Count == 0)
                        obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                    if (recentlyCaptured)
                        Debug.Log($"{TL("Plan")} Guarnicao: {obj.Sector} recem-capturado, segurando capturador por {GetRecentlyCapturedTurnsLeft(aiTeam, obj.Sector, snapshot.TurnNumber)}T");
                    continue;
                }
                ClearObjectiveHUD(obj);
                plan.Objectives.RemoveAt(i);
                continue;
            }
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled) continue;
                UnitManager slotUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (slotUnit == null || slotUnit.IsUnderRepair)
                {
                    slotUnit?.ClearAIAssignedPlan();
                    slot.Filled = false;
                    slot.AssignedUnitId = -1;
                }
            }
        }

        // Passo 2: adiciona objetivos para setores ainda n�o cobertos.
        // CÓPIA defensiva: GetAllSectorInfos/GetAllBaseInfos retornam as LISTAS INTERNAS VIVAS do
        // SectorManager (não cópias). Criar objetivos dentro dos loops abaixo pode disparar um
        // RebuildFromActiveConstructions (que dá Clear+refill nessas listas) e invalidar o
        // enumerador → "Collection was modified". Materializar aqui desacopla a iteração da lista viva.
        var allSectors = new List<SectorManager.SectorInfo>(SectorManager.GetAllSectorInfos());
        var allBases = new List<SectorManager.SectorInfo>(SectorManager.GetAllBaseInfos());

        int existingOffensive = 0;
        foreach (SectorObjective existing in plan.Objectives)
            if (existing.Status != ObjectiveStatus.Defending
                && existing.Status != ObjectiveStatus.Complete
                && existing.Status != ObjectiveStatus.Abandoned)
                existingOffensive++;

        int configuredMaxObj = Instance != null ? Instance.MaxActiveObjectives : 4;
        int sectorCount = allSectors != null ? allSectors.Count : 0;
        int sectorScaledMaxObj = sectorCount > configuredMaxObj
            ? configuredMaxObj + Mathf.CeilToInt((sectorCount - configuredMaxObj) / 2f)
            : configuredMaxObj;
        int maxObj = Mathf.Clamp(Mathf.Max(configuredMaxObj, sectorScaledMaxObj), 1, 8);
        if (maxObj != configuredMaxObj)
            Debug.Log($"{TL("Plan")} cap objetivos escalado: piso={configuredMaxObj} setores={sectorCount} -> {maxObj}");

        if (snapshot.MyUnits != null && snapshot.MyUnits.Count == 0)
        {
            int producerCap = Mathf.Max(1, CountControlledProductionBuildings(snapshot, aiTeam));
            int beforeProducerCap = maxObj;
            maxObj = Mathf.Clamp(Mathf.Min(maxObj, producerCap), 1, 8);
            Debug.Log($"{TL("Plan")} cap inicial por produtores: unidades=0 produtores={producerCap} cap={beforeProducerCap}->{maxObj}");
        }

        int macroOwnForce = snapshot.MyUnits != null ? snapshot.MyUnits.Count : 0;
        int knownEnemyForce = CountLiveEnemyUnits(aiTeam);
        // HARD: projeta a ONDA de produção inimiga do próximo turno — o inimigo compra em cada produtor
        // antes da AI agir de novo, então a análise de força reage à ameaça de AMANHÃ, não só à foto de
        // hoje. Normal/Easy ficam no retrato atual (comportamento validado, INTOCADO).
        int enemyProducers = hardMode ? CountEnemyProductionBuildings(aiTeam) : 0;
        int macroEnemyForce = knownEnemyForce + enemyProducers;
        AIMacroTerritoryContext macro = BuildMacroTerritoryContext(aiTeam, allSectors, maxObj, macroOwnForce, macroEnemyForce, enemyProducers);
        string macroForceTxt = enemyProducers > 0
            ? $"força={macro.OwnForce}v{macro.EnemyForce}(conhec={knownEnemyForce}+{enemyProducers}prod) fr={macro.ForceRatio:P0}"
            : $"força={macro.OwnForce}v{macro.EnemyForce} fr={macro.ForceRatio:P0}";
        if (macro.AppliesCap)
        {
            int beforeMacroCap = maxObj;
            maxObj = Mathf.Clamp(Mathf.Min(maxObj, macro.OffensiveCap), 1, 8);
            Debug.Log($"[AI Macro][T{snapshot.TurnNumber}][{aiTeam}] phase={macro.Phase} setores={macro.OwnedSectors}/{macro.EnemySectors}/{macro.NeutralSectors} pontos={macro.OwnedControlPoints}/{macro.EnemyControlPoints} disputa={macro.DisputedControlPoints} ratio={macro.OwnedRatio:P0} {macroForceTxt} offensiveCap={beforeMacroCap}->{maxObj}");
        }
        else
        {
            Debug.Log($"[AI Macro][T{snapshot.TurnNumber}][{aiTeam}] phase={macro.Phase} setores={macro.OwnedSectors}/{macro.EnemySectors}/{macro.NeutralSectors} pontos={macro.OwnedControlPoints}/{macro.EnemyControlPoints} disputa={macro.DisputedControlPoints} ratio={macro.OwnedRatio:P0} {macroForceTxt} offensiveCap={maxObj}");
        }

        if (macro.AppliesCap)
            ApplyMacroExistingOffensiveCap(plan, aiTeam, macro, intel, snapshot.TurnNumber);

        PruneDelayedEnemyNaturalObjectives(plan, aiTeam, snapshot, intel, macro);
        existingOffensive = 0;
        foreach (SectorObjective existing in plan.Objectives)
            if (existing.Status != ObjectiveStatus.Defending
                && existing.Status != ObjectiveStatus.Complete
                && existing.Status != ObjectiveStatus.Abandoned)
                existingOffensive++;

        int newSlots = Mathf.Max(0, maxObj - existingOffensive);

        var sectorCandidates = new List<SectorObjective>();
        foreach (SectorManager.SectorInfo info in allSectors)
        {
            bool isRallyAssemblySector = IsEnemyHQRallySectorHeld(rallyContext, info.Sector, aiTeam)
                && !IsRallyGoGreenSuppressed(
                    PlayerSlotId.FromIndex(snapshot.AISlotIndex), info.Sector, snapshot.TurnNumber);
            if (IsOwnedDefensibleSector(info, aiTeam) && !isRallyAssemblySector) continue;
            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
            {
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
            }
            if (!hasCapturable && !isRallyAssemblySector) { Debug.Log($"{TL("Plan")} skip {info.Sector}: sem captur�vel"); continue; }
            if (plan.GetObjectiveForSector(info.Sector) != null) { Debug.Log($"{TL("Plan")} skip {info.Sector}: j� tem objetivo"); continue; }

            // 2a trava (escopada): captura OFENSIVA fora do eixo (jardim inimigo, GetEixo==0) nao
            // recebe unidades ENQUANTO algum rally ainda monta massa e NENHUM atingiu GoGreen —
            // pre-GoGreen tudo afunila pro rally (mesma filosofia do gate de invasao ">>"). GoGreen,
            // ou ausencia de eixo de rally, destrava (sobra força pra oportunismo no quintal inimigo).
            if (!isRallyAssemblySector
                && IsOffAxisCaptureGatedByRally(info.Sector, aiTeam, rallyContext, snapshot.TurnNumber, intel))
            {
                Debug.Log($"{TL("Plan")} skip {info.Sector}: fora do eixo, rally ainda montando massa (sem GoGreen)");
                continue;
            }

            bool rearBridgeAvailable = HasAvailableRearNeighborTowardHQ(info, plan, aiTeam, out ConstructionSector rearBridgeSector);
            if (ShouldDelayEnemyNaturalByFrontier(info, plan, aiTeam, out ConstructionSector requiredRearSector))
            {
                Debug.Log($"{TL("Plan")} skip {info.Sector}: fronteira exige {requiredRearSector} antes");
                continue;
            }
            if (ShouldDelayEnemyNaturalOpening(info, aiTeam, snapshot, intel, macro) && !rearBridgeAvailable)
            {
                Debug.Log($"{TL("Plan")} skip {info.Sector}: {macro.Phase} EnemyNatural sem pressao local");
                continue;
            }
            if (rearBridgeAvailable && AISectorIntentAnalyzer.ClassifyRelation(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)), info) == AISectorRelation.EnemyNatural)
                Debug.Log($"{TL("Plan")} {info.Sector}: fronteira liberada por {rearBridgeSector}");

            bool hasPartialOwnCapture = false;
            foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                    && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                { hasPartialOwnCapture = true; break; }

            if (snapshot.Stance == AIStance.Defensive
                && info.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= SectorManager.SectorRiskLevel.Medium
                && !hasPartialOwnCapture)
            {
                Debug.Log($"{TL("Plan")} skip {info.Sector}: Defensive + risco={info.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)))} (>= Medium)");
                continue;
            }

            if (info.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= SectorManager.SectorRiskLevel.High)
            {
                ConstructionManager riskTgt = FindCapturableInSector(info.Sector, aiTeam);
                if (riskTgt != null)
                {
                    const float MaxArrivalGap = 5f;
                    Vector3Int rtc = riskTgt.CurrentCellPosition; rtc.z = 0;
                    float near1 = float.MaxValue, near2 = float.MaxValue;
                    foreach (UnitManager cap in GetAvailableCapturers(aiTeam))
                    {
                        Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(cc, rtc, out int td)
                            ? td : SectorManager.HexDistance(cc, rtc);
                        if (d < near1) { near2 = near1; near1 = d; }
                        else if (d < near2) near2 = d;
                    }
                    float gap = near2 - near1;
                    if (near2 < float.MaxValue && gap > MaxArrivalGap)
                    {
                        Debug.Log($"{TL("Plan")} Setor {info.Sector} ignorado: risco alto, batedor muito distante (gap={gap:F0}h)");
                        continue;
                    }
                }
            }

            SectorObjective obj = new SectorObjective
            {
                Sector       = info.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                ObjectiveType = isRallyAssemblySector ? AIObjectiveType.RallyAssembly : AIObjectiveType.CaptureSector,
                Priority     = CalculateSectorPriority(info, aiTeam, snapshot.Stance)
                    + GetIntelSectorPriorityBonus(intel, info.Sector)
                    + GetCampaignAdvancePriorityBonus(info.Sector, plan, aiTeam)
                    + GetAnchorSectorPriorityBonus(anchorContext, info.Sector, macro.Phase)
                    + GetAxisFrontPriorityBonus(info.Sector)
                    + (macro.Phase == AIMacroTerritoryPhase.Collapsing
                        ? 0
                        : GetRallySectorPriorityBonus(rallyContext, info.Sector, aiTeam)),
            };
            if (isRallyAssemblySector)
            {
                obj.RallyState = AIRallyAssemblyState.WaitHold;
                obj.RallyAssemblyStartedTurn = snapshot.TurnNumber;
                if (TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager newRally))
                    UpdateRallyObjectiveState(obj, newRally, aiTeam, snapshot.TurnNumber, intel);
                if (IsRallyGoGreenObjective(obj))
                {
                    Debug.Log($"{TL("Plan")} rally {obj.Sector}: GO_GREEN imediato, nao cria montagem");
                    continue;
                }
            }
            int slots = Mathf.Clamp(Mathf.CeilToInt(info.ConstructionCount / 2f), 1, 4);
            bool highRisk = info.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= SectorManager.SectorRiskLevel.High;
            if (highRisk) slots = Mathf.Max(slots, 2);
            if (hardMode) slots = Mathf.Min(slots * 2, HardModeCapturerSlotCap); // Hard Mode: dobra a demanda de capturadores por setor (com teto)
            if (!isRallyAssemblySector)
            {
                for (int s = 0; s < slots; s++)
                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                if (highRisk)
                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
            }
            float distHQ = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            int   transThreshold = GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            // Transporte e antecipatorio/posicional (R4): o slot so nasce quando o eixo de fato
            // vai precisar de carona — no ALVO DE TRANSPORTE PROFUNDO do eixo (a frente, ou o
            // proximo no se a frente ja esta sob captura: pipelining) ou num RALLY SEGURADO.
            // O gate de profundidade (>= limiar) e medido no proprio alvo (distHQ deste setor):
            // se a frente sob captura ainda esta perto (dentro dos ~7h), o proximo no tambem esta
            // raso e a pe resolve — nao carimba transporte.
            bool axisTargetDeep = IsAxisTransportTarget(obj.Sector) && distHQ >= transThreshold;
            bool addTrans = axisTargetDeep || isRallyAssemblySector;
            if (addTrans) obj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
            sectorCandidates.Add(obj);
        }

        sectorCandidates.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        int addedSectors = 0;
        var selectionCascadeCovered = new HashSet<ConstructionSector>();
        var selectionCascadeDeferred = new List<SectorObjective>();
        foreach (SectorObjective obj in sectorCandidates)
        {
            if (addedSectors < newSlots
                && selectionCascadeCovered.Contains(obj.Sector)
                && !IsEnemyHQRallySector(rallyContext, obj.Sector))
            {
                selectionCascadeDeferred.Add(obj);
                Debug.Log($"{TL("Plan")} defer {obj.Sector}: coberto por cascata inicial");
                continue;
            }

            if (addedSectors >= newSlots)
            {
                if (TryPreemptLowUrgencyObjectiveForHotCandidate(plan, obj, aiTeam, intel, rallyContext, snapshot.TurnNumber, maxObj, out SectorObjective removed))
                {
                    int capSlotsAdded = obj.Slots.FindAll(s => s.Role == UnitRole.Capturador).Count;
                    bool hasAssAdded = obj.Slots.Exists(s => s.Role == UnitRole.Assalto);
                    bool hasTransAdded = obj.Slots.Exists(s => s.Role == UnitRole.Transportador);
                    Debug.Log($"{TL("Plan")} preempt hot {obj.Sector}: remove {removed.Sector} para abrir cap ({maxObj}) " +
                              $"add={capSlotsAdded}xCap{(hasAssAdded ? " +Ass" : "")}{(hasTransAdded ? " +Trans" : "")} pri={obj.Priority}");
                    plan.Objectives.Add(obj);
                    MarkSelectionCascadeNeighbor(obj.Sector, selectionCascadeCovered, aiTeam, rallyContext);
                    addedSectors++;
                    continue;
                }
                Debug.Log($"{TL("Plan")} cap atingido ({maxObj}): {obj.Sector} descartado (pri={obj.Priority})");
                continue;
            }
            int capSlots  = obj.Slots.FindAll(s => s.Role == UnitRole.Capturador).Count;
            bool hasAss   = obj.Slots.Exists(s => s.Role == UnitRole.Assalto);
            bool hasTrans = obj.Slots.Exists(s => s.Role == UnitRole.Transportador);
            Debug.Log($"{TL("Plan")} {obj.Sector}: {capSlots}xCap{(hasAss ? " +Ass" : "")}{(hasTrans ? " +Trans" : "")} pri={obj.Priority}");
            plan.Objectives.Add(obj);
            MarkSelectionCascadeNeighbor(obj.Sector, selectionCascadeCovered, aiTeam, rallyContext);
            addedSectors++;
        }

        // Passo 2b: cascata deferred
        foreach (SectorObjective obj in selectionCascadeDeferred)
        {
            if (addedSectors >= newSlots)
            {
                Debug.Log($"{TL("Plan")} cap atingido ({maxObj}): {obj.Sector} descartado (pri={obj.Priority})");
                continue;
            }

            int capSlots  = obj.Slots.FindAll(s => s.Role == UnitRole.Capturador).Count;
            bool hasAss   = obj.Slots.Exists(s => s.Role == UnitRole.Assalto);
            bool hasTrans = obj.Slots.Exists(s => s.Role == UnitRole.Transportador);
            Debug.Log($"{TL("Plan")} {obj.Sector} (fallback cascata): {capSlots}xCap{(hasAss ? " +Ass" : "")}{(hasTrans ? " +Trans" : "")} pri={obj.Priority}");
            plan.Objectives.Add(obj);
            addedSectors++;
        }

        // Passo 2c: base inimiga (invasao ">>")
        // A invasao SO e alocada quando um rally ja juntou massa (GoGreen) — nao porque a AI quer.
        // Sem isso, unidades soltas marcham 11-12h sozinhas ate a base inimiga (suicidas). O rally
        // verde e a chave que destrava o ">>"; a montagem do rally segue intacta (nao mexo nela).
        // Invasao destrava por (a) rally que juntou massa (GoGreen) OU (b) DOMINIO macro (Ganhando:
        // >=60% territorio E forca). No dominio, voce ja venceu o jogo de massa globalmente — exigir
        // um rally local formal so trava o fechamento; com forca dominante o push nao e suicida.
        bool macroDominating = macro.Phase == AIMacroTerritoryPhase.Dominating;
        bool rallyGoGreen = AnyOwnedRallyAtGoGreen(aiTeam, rallyContext, snapshot.TurnNumber, intel);
        // Gate INAPLICAVEL: sem nenhum rally designado para este SLOT, a exigencia de GoGreen nunca
        // pode ser satisfeita. Sem esta saida, a invasao jamais e criada e o plano fica vazio pra
        // sempre — o caso base (tabuleiro so com QGs) tinha o planner inteiro desligado. Havendo
        // rally do slot, nada muda: o gate segue exigindo massa montada.
        bool rallyGateApplicable = MapHasAnyRallyPointForSlot(snapshot.AISlotIndex);
        bool invasionUnlocked = rallyGoGreen || macroDominating || !rallyGateApplicable;
        if (invasionUnlocked && !rallyGoGreen)
            Debug.Log($"{TL("Plan")} invasão liberada por {(macroDominating ? "DOMÍNIO (Ganhando)" : "SEM RALLY DO SLOT (gate inaplicável)")}, sem rally GoGreen");
        foreach (SectorManager.SectorInfo baseInfo in allBases)
        {
            if (FindHQTeamInSector(baseInfo.Sector) == aiTeam) continue;
            if (macro.Phase == AIMacroTerritoryPhase.Collapsing)
            {
                Debug.Log($"{TL("Plan")} skip base inimiga {baseInfo.Sector}: Collapsing suprime plano de invasao");
                continue;
            }
            if (plan.GetObjectiveForSector(baseInfo.Sector) != null) continue;
            if (!invasionUnlocked)
            {
                Debug.Log($"{TL("Plan")} base inimiga {baseInfo.Sector} travada: nenhum rally juntou massa (GoGreen) ainda");
                continue;
            }

            bool hasCapturable = false;
            foreach (SectorManager.SectorConstructionInfo c in baseInfo.Constructions)
            {
                if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
            }
            if (!hasCapturable) continue;

            bool hasPartialOwnCapture = false;
            foreach (SectorManager.SectorConstructionInfo c in baseInfo.Constructions)
                if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                    && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                { hasPartialOwnCapture = true; break; }

            if (snapshot.Stance == AIStance.Defensive && !hasPartialOwnCapture)
            {
                Debug.Log($"{TL("Plan")} skip base inimiga {baseInfo.Sector}: Defensive sem captura parcial");
                continue;
            }

            if (ShouldDelayEnemyBaseOpening(baseInfo, aiTeam, intel, macro, hasPartialOwnCapture))
            {
                Debug.Log($"{TL("Plan")} skip base inimiga {baseInfo.Sector}: {macro.Phase} sem pressao projetada");
                continue;
            }

            ConstructionManager baseTgt = FindCapturableInSector(baseInfo.Sector, aiTeam);
            if (baseTgt != null)
            {
                const float MaxArrivalGap = 5f;
                Vector3Int rtc = baseTgt.CurrentCellPosition; rtc.z = 0;
                float near1 = float.MaxValue, near2 = float.MaxValue;
                foreach (UnitManager cap in GetAvailableCapturers(aiTeam))
                {
                    Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(cc, rtc, out int td)
                        ? td : SectorManager.HexDistance(cc, rtc);
                    if (d < near1) { near2 = near1; near1 = d; }
                    else if (d < near2) near2 = d;
                }
                float gap = near2 - near1;
                if (near2 < float.MaxValue && gap > MaxArrivalGap)
                {
                    Debug.Log($"{TL("Plan")} base inimiga {baseInfo.Sector} aguarda co-chegada (gap={gap:F0}h)");
                    continue;
                }
            }

            int currentOffensive = existingOffensive + addedSectors;
            if (currentOffensive >= maxObj)
            {
                Debug.Log($"{TL("Plan")} cap atingido ({maxObj}): base inimiga {baseInfo.Sector} descartada");
                continue;
            }

            // Captura final = PEÇAS FORTES + MASSA. Assalto/fogo são demanda MODESTA de elite (o
            // reserve de compra prioriza essas peças fortes). Capturador é a MASSA, SEM teto (escala
            // com a base; Hard Mode dobra): o que sobra do orçamento depois das elites inunda
            // capturador, transporte e logística. Slot vazio não custa nada.
            int massCapturerSlots = Mathf.Max(2, baseInfo.ConstructionCount);
            if (hardMode) massCapturerSlots *= 2;
            var baseObj = new SectorObjective
            {
                Sector       = baseInfo.Sector,
                AssignedTeam = aiTeam,
                Status       = ObjectiveStatus.Pending,
                // Marca como InvasionAttack: o HUD/Shopping Pressure passa a exibir ">> Invasão" (e
                // não "Captura"), e a unidade ganha o tratamento de invasão (entrega/fogo) coerente.
                ObjectiveType = AIObjectiveType.InvasionAttack,
                Priority     = CalculateSectorPriority(baseInfo, aiTeam, snapshot.Stance)
                    + GetIntelSectorPriorityBonus(intel, baseInfo.Sector)
                    + GetAnchorSectorPriorityBonus(anchorContext, baseInfo.Sector, macro.Phase)
                    + GetRallySectorPriorityBonus(rallyContext, baseInfo.Sector, aiTeam),
            };
            for (int s = 0; s < massCapturerSlots; s++)
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            for (int s = 0; s < BaseInvasionEliteAssaultSlots; s++)
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
            // Fogo indireto na invasão é AMACIAMENTO, não requisito: só demanda se este mapa
            // vende artilharia. Sem produtor de fogo indireto, exigir o slot gera demanda-fantasma
            // que nunca fecha — e num roster de infantaria isso empurrava a compra de obus fraco só
            // para "pagar o imposto". Mesma lição do Fix A (núcleo) e do gate inaplicável.
            int invasionFireSupportSlots = CanProduceCompositionRole(snapshot, aiTeam, UnitRole.FogoIndireto)
                ? BaseInvasionEliteFireSupportSlots
                : 0;
            for (int s = 0; s < invasionFireSupportSlots; s++)
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.FogoIndireto });
            // Transportador: leva a massa ao campo. Quando a base é longe, escala com a leva de
            // capturadores (capacidade por APC) para reposicionar rápido o que foi comprado no QG.
            if (baseInfo.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))))
            {
                int transportSlots = Mathf.Max(1,
                    Mathf.CeilToInt(baseInfo.ConstructionCount / (float)BaseInvasionTransportCapacity));
                for (int s = 0; s < transportSlots; s++)
                    baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
            }
            // Logística: sustenta a invasão na frente (reparo/reabastecimento), parte da massa de apoio.
            for (int s = 0; s < BaseInvasionLogisticsSlots; s++)
                baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Logistica });
            plan.Objectives.Add(baseObj);
            addedSectors++;
            Debug.Log($"{TL("Plan")} base inimiga {baseInfo.Sector}: {massCapturerSlots}xCap(massa) + {BaseInvasionEliteAssaultSlots}xAssalto + {invasionFireSupportSlots}xFogo{(invasionFireSupportSlots == 0 ? "(sem produtor)" : "")} + {BaseInvasionLogisticsSlots}xLog construcoes={baseInfo.ConstructionCount} dist={baseInfo.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))):F0}h");
        }

        ClearResolvedCriticalHomeDefenseObjectives(plan, aiTeam);
        EnsureCriticalHomeDefenseObjectives(plan, aiTeam, allBases);
        EnsureCriticalHomeDefenseObjectivesFromConstructions(plan, aiTeam);

        // Edge case defensivo: se n�o sobrou nenhum objetivo
        if (snapshot.Stance == AIStance.Defensive && plan.Objectives.Count == 0)
        {
            SectorManager.SectorInfo closest = null;
            float closestDist = float.MaxValue;
            foreach (SectorManager.SectorInfo info in allSectors)
            {
                if (info.IsFullyControlled && info.ControllingTeam == aiTeam) continue;
                bool hasCapturable = false;
                foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                {
                    if (c.OwnerTeam != aiTeam) { hasCapturable = true; break; }
                    if (c.CapturePointsMax > 0 && c.CurrentCapturePoints < c.CapturePointsMax) { hasCapturable = true; break; }
                }
                if (!hasCapturable) continue;
                float d = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
                if (d < closestDist) { closestDist = d; closest = info; }
            }
            if (closest != null && plan.GetObjectiveForSector(closest.Sector) == null)
            {
                SectorObjective fallback = new SectorObjective
                {
                    Sector       = closest.Sector,
                    AssignedTeam = aiTeam,
                    Status       = ObjectiveStatus.Pending,
                    Priority     = CalculateSectorPriority(closest, aiTeam, snapshot.Stance)
                        + GetIntelSectorPriorityBonus(intel, closest.Sector)
                        + GetAnchorSectorPriorityBonus(anchorContext, closest.Sector, macro.Phase)
                        + GetRallySectorPriorityBonus(rallyContext, closest.Sector, aiTeam),
                };
                fallback.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
                plan.Objectives.Add(fallback);
                Debug.Log($"{TL("Plan")} Defensive sem objetivos seguros � fallback para {closest.Sector} ({closestDist:F1}h do HQ)");
            }
        }

        // Passo 3: recalcula prioridades e ordena
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj.Status == ObjectiveStatus.Defending) continue;
            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo inf))
                obj.Priority = CalculateSectorPriority(inf, aiTeam, snapshot.Stance)
                    + GetIntelSectorPriorityBonus(intel, inf.Sector)
                    + GetAnchorSectorPriorityBonus(anchorContext, inf.Sector, macro.Phase)
                    + GetRallySectorPriorityBonus(rallyContext, inf.Sector, aiTeam);
        }

        SortPlanObjectivesByStrategicPriority(plan, aiTeam, priorityNumberAscending: false);

        // Passo 3c: objetivos defensivos
        {
            int DefenseEnemyRange = defenseEnemyRange;
            int defPriority = plan.Objectives.Count + 1;
            foreach (SectorManager.SectorInfo info in allSectors)
            {
                if (!IsOwnedDefensibleSector(info, aiTeam)) continue;
                Vector3Int rc = info.RepresentativeCell; rc.z = 0;
                bool criticalHomeThreat = IsCriticalHomeDefenseSector(info, aiTeam)
                    && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);
                AISectorIntel sectorIntel = FindIntelForSector(intel, info.Sector);
                bool intelDefenseThreat = IsHotPlanIntelDefenseSector(sectorIntel, out string intelDefenseReason);
                bool visibleEnemy = HasNearbyVisibleEnemy(rc, aiTeam, DefenseEnemyRange);
                bool sectorDefenseThreat = criticalHomeThreat
                    || info.IsDisputed
                    || info.HasPartialCapture
                    || visibleEnemy
                    || intelDefenseThreat;
                if (!sectorDefenseThreat) continue;

                string defenseReason = criticalHomeThreat ? "QG ameaçado"
                    : info.IsDisputed ? "disputado"
                    : info.HasPartialCapture ? "captura parcial"
                    : visibleEnemy ? $"inimigo visível <= {DefenseEnemyRange}h"
                    : $"intel ({intelDefenseReason})";

                SectorObjective existingDefense = plan.GetObjectiveForSector(info.Sector);
                if (existingDefense != null)
                {
                    if (existingDefense.Status != ObjectiveStatus.Defending)
                    {
                        // Não sequestrar um rally em montagem: a massa que ele já está juntando
                        // É a defesa do setor, e NormalizeDefenseObjectiveSlots destruiria a
                        // atribuição do rally (slots reescritos → tropa volta a rogue/guarnição).
                        // Quando o rally dispara GoGreen/Expira, deixa de ser ativo e a conversão volta.
                        if (IsActiveRallyAssemblyObjective(existingDefense))
                        {
                            Debug.Log($"{TL("Plan")} {info.Sector} sob ameaça mas é rally em montagem ({existingDefense.RallyState}); mantém montagem, não converte para defesa");
                            continue;
                        }

                        existingDefense.Status = ObjectiveStatus.Defending;
                        existingDefense.Priority = defPriority++;
                        existingDefense.DefenseReason = defenseReason;
                        NormalizeDefenseObjectiveSlots(existingDefense, info.HasPartialCapture || criticalHomeThreat || (sectorIntel != null && sectorIntel.capturePressure > 0f), !criticalHomeThreat || intelDefenseThreat, aiTeam);
                        Debug.Log($"{TL("Plan")} Objetivo convertido para defesa: {info.Sector} (pri {existingDefense.Priority}, motivo={defenseReason})");
                    }
                    continue;
                }

                var defObj = new SectorObjective
                {
                    Sector = info.Sector, AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending, Priority = defPriority++,
                    DefenseReason = defenseReason,
                };
                NormalizeDefenseObjectiveSlots(defObj, info.HasPartialCapture || criticalHomeThreat || (sectorIntel != null && sectorIntel.capturePressure > 0f), !criticalHomeThreat || intelDefenseThreat, aiTeam);
                plan.Objectives.Add(defObj);
                Debug.Log($"{TL("Plan")} Objetivo defensivo: {info.Sector} (pri {defPriority - 1}, motivo={defenseReason})");
            }
        }

        SortPlanObjectivesByStrategicPriority(plan, aiTeam, priorityNumberAscending: true);

        plan.HandoffVacaterIds.Clear();
        plan.VacaterForwardSectors.Clear();

        // Passo 3b: handoff
        EvaluateCaptureHandoffs(plan, aiTeam);

        // Passo 3d: transporte vazio perto demais do objetivo
        ReleaseShortRangeEmptyTransportAssignments(plan, aiTeam);

        // Passo 3e: suporte ofensivo sem capturador
        ReleaseOffensiveSupportWithoutCapturer(plan, aiTeam);

        // Passo 3f: anchors abertos seguram capturadores antes de avan�o/rally.
        ReleaseNonAnchorCapturersForAnchorNeed(plan, aiTeam, anchorContext, macro.Phase, snapshot.TurnNumber);
        bool anchorCapturerReserveActive = ShouldReserveCapturersForAnchors(anchorContext, macro.Phase)
            && HasOpenAnchorCapturerNeed(plan, anchorContext);

        // Modelo multi-centro: cada rally held é um centro próprio (sem foco/feeder/dreno).
        // NÃO consolidar — unidades montam no rally mais próximo, e a prontidão é combinada.

        // Passo 4: coleta IDs j� atribu�dos
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) assignedIds.Add(slot.AssignedUnitId);

        // Passo 5: atribui capturadores livres
        plan.RogueUnitIds.Clear();
        List<UnitManager> allCapturers  = GetAvailableCapturers(aiTeam);
        List<UnitManager> freeCapturers = new List<UnitManager>();
        foreach (UnitManager u in allCapturers)
            if (!assignedIds.Contains(u.InstanceId)) freeCapturers.Add(u);

        // 5a-0: guarnicao local. Se o setor acabou de virar defesa e o capturador
        // ainda esta no predio, nao deixa o solver global puxa-lo para outra frente.
        var localGarrisonList = new List<UnitManager>();
        foreach (UnitManager u in freeCapturers)
        {
            ConstructionSector currentSector = ResolveUnitSectorForPlan(u);
            if (currentSector == ConstructionSector.None)
                continue;

            SectorObjective obj = plan.GetObjectiveForSector(currentSector);
            if (obj == null
                || obj.Status != ObjectiveStatus.Defending
                || !obj.HasOpenSlot(UnitRole.Capturador))
                continue;

            obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
            ApplyPlanHUD(u, obj);
            localGarrisonList.Add(u);
            Debug.Log($"{TL("Plan")} {u.InstanceId} permanece em {currentSector} como guarnicao local");
        }
        foreach (UnitManager u in localGarrisonList) freeCapturers.Remove(u);

        // A reserva de âncora só deve bloquear os demais objetivos quando os capturadores são
        // escassos (não sobram além do que as âncoras precisam). Havendo excedente, ele é livre
        // para os outros setores — senão capturadores ficam ociosos como rogue.
        int anchorOpenCapturerSlots = CountOpenAnchorCapturerSlots(plan, anchorContext);
        bool anchorReserveBlocksOthers = anchorCapturerReserveActive
            && freeCapturers.Count <= anchorOpenCapturerSlots;
        if (anchorCapturerReserveActive && !anchorReserveBlocksOthers)
            Debug.Log($"{TL("Plan")} reserva âncora: {freeCapturers.Count} capturadores livres > {anchorOpenCapturerSlots} vaga(s) âncora — excedente liberado para outros objetivos");

        // 5a: captura imediata
        var immediateList = new List<UnitManager>();
        foreach (UnitManager u in freeCapturers)
        {
            Vector3Int uCell = u.CurrentCellPosition; uCell.z = 0;
            if (!SimulateCaptureSensor(u, uCell, out ConstructionManager bldg)) continue;
            SectorObjective obj = plan.GetObjectiveForSector(bldg.Sector);
            if (obj == null || !obj.HasOpenSlot(UnitRole.Capturador)) continue;
            if (anchorReserveBlocksOthers && !IsOwnAnchorSector(anchorContext, obj.Sector)) continue;
            obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
            if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
            ApplyPlanHUD(u, obj);
            immediateList.Add(u);
            Debug.Log($"{TL("Plan")} {u.InstanceId} j� est� em {bldg.Sector} ? captura imediata");
        }
        foreach (UnitManager u in immediateList) freeCapturers.Remove(u);

        // 5b: atribui��o �tima por backtracking
        int openOffensiveObjCount = 0;
        foreach (SectorObjective o in plan.Objectives)
            if (o.Status != ObjectiveStatus.Defending && o.HasOpenSlot(UnitRole.Capturador))
                openOffensiveObjCount++;
        bool isInitialDistribution = !plan.Objectives.Exists(o => o.Status == ObjectiveStatus.Pursuing)
            && freeCapturers.Count < openOffensiveObjCount;
        const float MaxCoArrivalGap = 5f;
        var cascadeCovered = new HashSet<ConstructionSector>();
        var assignableObjs = new List<(SectorObjective obj, Vector3Int cell)>();

        // Hard Mode — distribuição em largura ENTRE turnos: as vagas dobradas (2ª+) de um setor só
        // abrem quando NENHUM outro objetivo ofensivo ainda está sem o 1º capturador. Assim os
        // capturadores novos vão ABRIR/AVANÇAR frentes novas (Hotel/Echo/Foxtrot) em vez de encher
        // o 2º slot de setores já iniciados (que costumam estar mais perto da base). As 2ªs vagas
        // enchem depois, com o excedente. Mantém o "feel" do normal mode mesmo com capacidade dobrada.
        bool anyFirstCapturerPending = false;
        if (hardMode)
        {
            foreach (SectorObjective o in plan.Objectives)
            {
                if (o.Status == ObjectiveStatus.Defending) continue;
                if (!o.HasOpenSlot(UnitRole.Capturador)) continue;
                if (!o.Slots.Exists(s => s.Role == UnitRole.Capturador && s.Filled))
                {
                    anyFirstCapturerPending = true;
                    break;
                }
            }
        }

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!obj.HasOpenSlot(UnitRole.Capturador)) continue;
            if (obj.Status == ObjectiveStatus.Defending)
                continue;
            if (anchorReserveBlocksOthers && !IsOwnAnchorSector(anchorContext, obj.Sector))
                continue;
            bool isDefensive = false;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            Vector3Int tc;
            if (tgt != null)
            {
                tc = tgt.CurrentCellPosition; tc.z = 0;
            }
            else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)
                && defInfo.IsFullyControlled && defInfo.ControllingTeam == aiTeam)
            {
                isDefensive = true;
                tc = defInfo.RepresentativeCell; tc.z = 0;
            }
            else continue;
            if (!isDefensive
                && cascadeCovered.Contains(obj.Sector)
                && !IsOwnAnchorSector(anchorContext, obj.Sector)
                && !IsEnemyHQRallySector(rallyContext, obj.Sector))
                continue;

            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo slotInfo)
                && slotInfo.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= SectorManager.SectorRiskLevel.High)
            {
                SlotNeed filledSlot = obj.Slots.Find(s => s.Role == UnitRole.Capturador && s.Filled);
                if (filledSlot != null)
                {
                    UnitManager assigned = FindActiveUnit(filledSlot.AssignedUnitId, aiTeam);
                    Vector3Int  aPos     = assigned != null ? assigned.CurrentCellPosition : tc; aPos.z = 0;
                    float assignedDist   = SectorManager.TryGetLandMovementDistance(aPos, tc, out int adCost)
                        ? adCost : SectorManager.HexDistance(aPos, tc);

                    float nearestFree = float.MaxValue;
                    foreach (UnitManager cap in freeCapturers)
                    {
                        Vector3Int cc = cap.CurrentCellPosition; cc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(cc, tc, out int td)
                            ? td : SectorManager.HexDistance(cc, tc);
                        if (d < nearestFree) nearestFree = d;
                    }

                    if (nearestFree == float.MaxValue || nearestFree > assignedDist + MaxCoArrivalGap)
                    {
                        Debug.Log($"{TL("Plan")} {obj.Sector} 2� slot bloqueado: livre mais pr�ximo " +
                                  $"({(nearestFree == float.MaxValue ? "?" : nearestFree.ToString("F0"))}h) " +
                                  $"fora de alcance do 1� ({assignedDist:F0}h)");
                        continue;
                    }
                }
            }

            int openCapturerSlots = CountOpenSlots(obj, UnitRole.Capturador);
            int exposedCapturerSlots;
            if (!hardMode)
            {
                exposedCapturerSlots = openCapturerSlots;
            }
            else
            {
                bool hasFirstCapturer = obj.Slots.Exists(s => s.Role == UnitRole.Capturador && s.Filled);
                // 1ª vaga sempre exposta (abrir a frente). 2ª+ só quando ninguém mais precisa do 1º
                // capturador — e ainda assim 1 por passada (gradual, conforme novos vão chegando).
                exposedCapturerSlots = !hasFirstCapturer
                    ? 1
                    : (anyFirstCapturerPending ? 0 : 1);
            }
            for (int slotIndex = 0; slotIndex < exposedCapturerSlots; slotIndex++)
                assignableObjs.Add((obj, tc));

            if (isInitialDistribution && !isDefensive)
                MarkCascadeNeighbor1(obj.Sector, cascadeCovered, aiTeam, plan.VacaterForwardSectors, rallyContext);
        }

        int nu = Mathf.Min(freeCapturers.Count, assignableObjs.Count);
        if (nu > 0)
        {
            int nObj = assignableObjs.Count;

            if (freeCapturers.Count > nu)
            {
                freeCapturers.Sort((a, b) =>
                {
                    float minA = float.MaxValue, minB = float.MaxValue;
                    Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
                    Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
                    foreach ((_, Vector3Int tc) in assignableObjs)
                    {
                        float da = Vector3Int.Distance(ca, tc);
                        float db = Vector3Int.Distance(cb, tc);
                        if (da < minA) minA = da;
                        if (db < minB) minB = db;
                    }
                    return minA.CompareTo(minB);
                });
            }

            const int   PlanBudget     = 30;
            float RiskCostWeight = riskDecisionImpact;

            var riskMultipliers = new float[nObj];
            for (int oj = 0; oj < nObj; oj++)
            {
                riskMultipliers[oj] = 1f;
                if (SectorManager.TryGetSectorInfo(assignableObjs[oj].obj.Sector,
                        out SectorManager.SectorInfo sInfo))
                {
                    riskMultipliers[oj] = 1f + sInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) * RiskCostWeight;
                    foreach (SectorManager.SectorConstructionInfo c in sInfo.Constructions)
                    {
                        if (c.OwnerTeam == aiTeam && c.CapturePointsMax > 0
                            && c.CurrentCapturePoints > 0 && c.CurrentCapturePoints < c.CapturePointsMax)
                        {
                            riskMultipliers[oj] = Mathf.Max(1f, riskMultipliers[oj] * 0.5f);
                            break;
                        }
                    }
                }
            }

            var distMatrix = new float[nu, nObj];
            for (int ui = 0; ui < nu; ui++)
            {
                UnitManager u   = freeCapturers[ui];
                Vector3Int  uc  = u.CurrentCellPosition; uc.z = 0;
                var planPaths   = UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, u, PlanBudget, terrainDatabase);
                for (int oj = 0; oj < nObj; oj++)
                {
                    Vector3Int tc = assignableObjs[oj].cell;
                    float rawDist;
                    if (planPaths != null
                        && planPaths.TryGetValue(tc, out List<Vector3Int> path)
                        && path.Count > 0)
                        rawDist = path.Count;
                    else if (SectorManager.TryGetLandMovementDistance(uc, tc, out int terrainCost))
                        rawDist = terrainCost;
                    else
                        rawDist = SectorManager.HexDistance(uc, tc);
                    float continuityCost = CalculateSectorContinuityAssignmentCost(
                        u,
                        assignableObjs[oj].obj.Sector,
                        aiTeam);
                    distMatrix[ui, oj] = Mathf.Max(0.1f, rawDist * riskMultipliers[oj] + continuityCost);
                }
            }

            int[] bestAssign = new int[nu];
            float bestCost   = float.MaxValue;
            SolveAssignment(freeCapturers, assignableObjs,
                new bool[nObj], new int[nu],
                0, nu, 0f, distMatrix, ref bestCost, ref bestAssign);

            for (int i = 0; i < nu; i++)
            {
                UnitManager u       = freeCapturers[i];
                SectorObjective obj = assignableObjs[bestAssign[i]].obj;
                obj.TryFillSlot(UnitRole.Capturador, u.InstanceId);
                if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                ApplyPlanHUD(u, obj);
            }
            freeCapturers.RemoveRange(0, nu);
        }

        // Passo 5c: assaltos prim�rios
        {
            var assignedAfterCapturers = new HashSet<int>();
            foreach (SectorObjective obj in plan.Objectives)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot.Filled) assignedAfterCapturers.Add(slot.AssignedUnitId);

            List<UnitManager> freeAssaults = GetAvailablePrimaryAssaults(aiTeam);
            for (int i = freeAssaults.Count - 1; i >= 0; i--)
                if (assignedAfterCapturers.Contains(freeAssaults[i].InstanceId))
                    freeAssaults.RemoveAt(i);

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int targetCell = defInfo.RepresentativeCell; targetCell.z = 0;
                if (!IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                        defenseEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp))
                    continue;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} ? defesa cr�tica de {obj.Sector} " +
                          $"(inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}� dist={bestDist:F0}h)");
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Pursuing && obj.Status != ObjectiveStatus.Capturing) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;

                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                if (tgt == null) continue;
                Vector3Int targetCell = tgt.CurrentCellPosition; targetCell.z = 0;

                if (!IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                        alliesEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp))
                    continue;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} ? SOS ofensivo {obj.Sector} " +
                          $"(inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}� dist={bestDist:F0}h)");
            }

            const float DefenseEscortMaxPM = 8f;
            foreach (SectorObjective obj in plan.Objectives)
            {
                if (freeAssaults.Count == 0) break;
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                Vector3Int targetCell = defInfo.RepresentativeCell; targetCell.z = 0;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d > DefenseEscortMaxPM) continue;
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} ? defesa est�vel de {obj.Sector} (dist={bestDist:F0}h)");
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (!obj.HasOpenSlot(UnitRole.Assalto)) continue;
                if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                Vector3Int targetCell = escortInfo.RepresentativeCell; targetCell.z = 0;

                UnitManager best = null;
                float bestDist = float.MaxValue;
                foreach (UnitManager u in freeAssaults)
                {
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    if (d < bestDist) { bestDist = d; best = u; }
                }

                if (best == null) continue;

                obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                ApplyPlanHUD(best, obj, UnitRole.Assalto);
                freeAssaults.Remove(best);
                Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} ? batedor de {obj.Sector} (dist={bestDist:F0}h)");
            }

            if (freeAssaults.Count > 0)
            {
                const float MinLowRiskForEscort = 0.45f;
                var escortFallbacks = new List<SectorObjective>();
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (!HasFilledSlot(obj, UnitRole.Capturador)) continue;
                    if (HasAnySlot(obj, UnitRole.Assalto)) continue;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                    var riskLevel = escortInfo.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
                    bool isHigh = riskLevel == SectorManager.SectorRiskLevel.High;
                    bool isMedium = riskLevel == SectorManager.SectorRiskLevel.Medium;
                    bool isLow = riskLevel == SectorManager.SectorRiskLevel.Low
                        && escortInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) >= MinLowRiskForEscort;
                    if (!isHigh && !isMedium && !isLow) continue;
                    escortFallbacks.Add(obj);
                }

                escortFallbacks.Sort((a, b) =>
                {
                    int aRank = TryGetAnySectorInfo(a.Sector, out SectorManager.SectorInfo ai)
                        ? GetEscortFallbackRiskRank(ai.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)))) : 0;
                    int bRank = TryGetAnySectorInfo(b.Sector, out SectorManager.SectorInfo bi)
                        ? GetEscortFallbackRiskRank(bi.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)))) : 0;
                    if (aRank != bRank) return bRank.CompareTo(aRank);
                    float ar = SectorManager.TryGetSectorInfo(a.Sector, out SectorManager.SectorInfo aInfo)
                        ? aInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) : 0f;
                    float br = SectorManager.TryGetSectorInfo(b.Sector, out SectorManager.SectorInfo bInfo)
                        ? bInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))) : 0f;
                    int riskCompare = br.CompareTo(ar);
                    return riskCompare != 0 ? riskCompare : a.Priority.CompareTo(b.Priority);
                });

                foreach (SectorObjective obj in escortFallbacks)
                {
                    if (freeAssaults.Count == 0) break;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo escortInfo)) continue;
                    Vector3Int targetCell = escortInfo.RepresentativeCell; targetCell.z = 0;

                    UnitManager best = null;
                    float bestDist = float.MaxValue;
                    foreach (UnitManager u in freeAssaults)
                    {
                        Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                        float d = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                            ? td : SectorManager.HexDistance(uc, targetCell);
                        if (d < bestDist) { bestDist = d; best = u; }
                    }

                    if (best == null) continue;

                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
                    obj.TryFillSlot(UnitRole.Assalto, best.InstanceId);
                    if (obj.Status != ObjectiveStatus.Defending) obj.Status = ObjectiveStatus.Pursuing;
                    ApplyPlanHUD(best, obj, UnitRole.Assalto);
                    freeAssaults.Remove(best);
                    Debug.Log($"{TL("Plan")} Assalto {best.InstanceId} ? batedor fallback {escortInfo.GetRiskLevelFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)))} de {obj.Sector} " +
                              $"(risco={escortInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam))):F2}, dist={bestDist:F0}h)");
                }
            }
        }

        // Passo 5d: rogues pr�ximos refor�am defesa
        {
            int maxDefenders  = 3;
            int defReachRange = Mathf.Max(HomeDefenseThreatRange, defenseEnemyRange);
            var rogueAssigned = new List<UnitManager>();

            var defCandidates = new List<UnitManager>(freeCapturers);
            if (snapshot.MyHQ != null)
            {
                Vector3Int hqCell = snapshot.MyHQ.CurrentCellPosition; hqCell.z = 0;
                defCandidates.Sort((a, b) =>
                {
                    Vector3Int ca = a.CurrentCellPosition; ca.z = 0;
                    Vector3Int cb = b.CurrentCellPosition; cb.z = 0;
                    return SectorManager.HexDistance(ca, hqCell)
                        .CompareTo(SectorManager.HexDistance(cb, hqCell));
                });
            }

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Defending) continue;
                if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo defInfo)) continue;
                int defenders = 0; foreach (SlotNeed s in obj.Slots) if (s.Filled) defenders++;
                Vector3Int rc = ResolveCriticalHomeDefenseTargetCell(obj, aiTeam, defInfo.RepresentativeCell);
                rc.z = 0;
                foreach (UnitManager u in defCandidates)
                {
                    if (rogueAssigned.Contains(u)) continue;
                    if (defenders >= maxDefenders) break;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float responseDist = CalculateUnitResponseDistance(u, uc, rc);
                    if (responseDist > defReachRange) continue;
                    if (!obj.TryFillSlot(UnitRole.Capturador, u.InstanceId))
                        obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador, Filled = true, AssignedUnitId = u.InstanceId });
                    ApplyPlanHUD(u, obj);
                    rogueAssigned.Add(u);
                    defenders++;
                    Debug.Log($"{TL("Plan")} Rogue {u.InstanceId} ? defesa de {obj.Sector} (dist={SectorManager.HexDistance(uc, rc)})");
                }
            }
            foreach (UnitManager u in rogueAssigned) freeCapturers.Remove(u);
        }

        // Passo 5e: rogues pr�ximos refor�am captura em desvantagem
        {
            const int MaxCaptureReinforcements = 2;
            float     SosRatio                 = alliesAgainstEnemiesHpRatio;
            int       sosReachRange            = alliesCallRange;
            var         captureReinforced        = new List<UnitManager>();

            foreach (SectorObjective obj in plan.Objectives)
            {
                if (obj.Status != ObjectiveStatus.Pursuing && obj.Status != ObjectiveStatus.Capturing) continue;

                ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                if (tgt == null) continue;
                Vector3Int tc = tgt.CurrentCellPosition; tc.z = 0;

                int enemyHp = 0;
                MatchController mc = GetMatchController();
                foreach (UnitManager enemy in UnitManager.AllActive)
                {
                    if (enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked) continue;
                    if (mc != null && !mc.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(currentAISlotIndex))) continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(ec, tc) <= alliesEnemyRange) enemyHp += enemy.CurrentHP;
                }
                if (enemyHp == 0) continue;

                int allyHp = 0;
                foreach (SlotNeed s in obj.Slots)
                {
                    if (!s.Filled) continue;
                    UnitManager ally = FindActiveUnit(s.AssignedUnitId, aiTeam);
                    if (ally != null) allyHp += ally.CurrentHP;
                }

                if (enemyHp < allyHp * SosRatio) continue;

                int reinforcers = 0;
                foreach (UnitManager u in freeCapturers)
                {
                    if (captureReinforced.Contains(u)) continue;
                    if (reinforcers >= MaxCaptureReinforcements) break;
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    if (SectorManager.HexDistance(uc, tc) > sosReachRange) continue;
                    obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador, Filled = true, AssignedUnitId = u.InstanceId });
                    ApplyPlanHUD(u, obj);
                    captureReinforced.Add(u);
                    reinforcers++;
                    Debug.Log($"{TL("Plan")} SOS {u.InstanceId} ? refor�o de captura {obj.Sector} (inimigo={enemyHp}HP aliado={allyHp}HP ratio={enemyHp / (float)allyHp:F1}�)");
                }
            }
            foreach (UnitManager u in captureReinforced) freeCapturers.Remove(u);
        }

        foreach (UnitManager u in freeCapturers)
        {
            u.ClearAIAssignedPlan();
            plan.RogueUnitIds.Add(u.InstanceId);
        }

        // Passo 5h: endgame all-in — quando todos os setores regulares estão controlados,
        // absorve rogues e assaltos livres no plano de invasão da base inimiga.
        {
            bool allRegularSectorsCaptured = allSectors != null && allSectors.Count > 0;
            if (allRegularSectorsCaptured)
                foreach (SectorManager.SectorInfo info in allSectors)
                    if (!IsOwnedDefensibleSector(info, aiTeam)) { allRegularSectorsCaptured = false; break; }

            if (allRegularSectorsCaptured)
            {
                SectorObjective baseObj = null;
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned) continue;
                    if (ConstructionSectorHelper.IsBase(obj.Sector) && FindHQTeamInSector(obj.Sector) != aiTeam)
                    { baseObj = obj; break; }
                }

                if (baseObj != null)
                {
                    int absorbed = 0;

                    // Capturadores rogues já miram no HQ inimigo nativamente via DecideRogueCapturerAction
                    // e têm lógica de combate para abrir caminho. Não absorver — manter como rogues.

                    var finalAssignedIds = new HashSet<int>();
                    foreach (SectorObjective obj in plan.Objectives)
                        foreach (SlotNeed slot in obj.Slots)
                            if (slot.Filled) finalAssignedIds.Add(slot.AssignedUnitId);

                    List<UnitManager> remainingAssaults = GetAvailablePrimaryAssaults(aiTeam);
                    foreach (UnitManager u in remainingAssaults)
                    {
                        if (finalAssignedIds.Contains(u.InstanceId)) continue;
                        baseObj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto, Filled = true, AssignedUnitId = u.InstanceId });
                        assignedIds.Add(u.InstanceId);
                        ApplyPlanHUD(u, baseObj, UnitRole.Assalto);
                        absorbed++;
                    }

                    if (absorbed > 0)
                        Debug.Log($"{TL("Plan")} endgame all-in: {absorbed} unidades absorvidas -> {baseObj.Sector}");
                }
            }
        }

        // Passo 5g: artilharia livre acompanha o front
        {
            // Antes da distribuição genérica, concentra rogues próximos no melhor rally
            // ainda incompleto. Um único assembly completo já libera massa de invasão.
            // Rally assegurado inicia a concentracao final: toda unidade operacional realmente
            // livre entra sem teto; infantaria distante ainda gera backlog minimo de APC.
            RecruitFreeUnitsForRally(plan, aiTeam, assignedIds);
            RecruitNearbyRogueArtilleryForRally(plan, aiTeam, assignedIds);

            List<UnitManager> freeFireSupports = GetAvailablePrimaryFireSupports(aiTeam);
            foreach (UnitManager u in freeFireSupports)
            {
                if (assignedIds.Contains(u.InstanceId)) continue;
                bool isRallyArtillery = u.TryGetUnitData(out UnitData fireSupportData)
                    && GetRallyArtilleryWeight(fireSupportData) > 0f;

                SectorObjective bestObj = null;
                float bestScore = float.MinValue;
                float bestDist = float.MaxValue;
                foreach (SectorObjective obj in plan.Objectives)
                {
                    if (obj == null || obj.Status == ObjectiveStatus.Complete || obj.Status == ObjectiveStatus.Abandoned)
                        continue;
                    if (!HasFilledSlot(obj, UnitRole.Capturador) && !HasFilledSlot(obj, UnitRole.Assalto))
                        continue;
                    if (!TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                        continue;
                    if (IsRallyAssemblyObjective(obj))
                    {
                        if (!isRallyArtillery)
                            continue;
                        if (!obj.HasOpenSlot(UnitRole.FogoIndireto))
                            continue;
                    }
                    else if (HasFilledSlot(obj, UnitRole.FogoIndireto))
                    {
                        // Um objetivo comum recebe uma escolta de fogo. Rallys criam
                        // explicitamente as tres vagas de montagem que precisam preencher.
                        continue;
                    }

                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                    Vector3Int targetCell = tgt != null ? tgt.CurrentCellPosition : info.RepresentativeCell;
                    targetCell.z = 0;

                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
                    float dist = SectorManager.TryGetLandMovementDistance(uc, targetCell, out int td)
                        ? td : SectorManager.HexDistance(uc, targetCell);
                    float risk = info.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
                    float score = -obj.Priority * 900f
                        - dist * 45f
                        + risk * 220f
                        + (obj.Status == ObjectiveStatus.Defending ? 180f : 0f)
                        + ComputeFireSupportSideBias(obj.Sector);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestObj = obj;
                        bestDist = dist;
                    }
                }

                if (bestObj == null) continue;
                if (!bestObj.HasOpenSlot(UnitRole.FogoIndireto))
                    bestObj.Slots.Add(new SlotNeed { Role = UnitRole.FogoIndireto });
                bestObj.TryFillSlot(UnitRole.FogoIndireto, u.InstanceId);
                assignedIds.Add(u.InstanceId);
                ApplyPlanHUD(u, bestObj, UnitRole.FogoIndireto);
                bool fsIsEnemySector;
                if (ConstructionSectorHelper.IsBase(bestObj.Sector))
                    fsIsEnemySector = FindHQTeamInSector(bestObj.Sector) != aiTeam;
                else
                    fsIsEnemySector = TryGetAnySectorInfo(bestObj.Sector, out SectorManager.SectorInfo fsSecInfo)
                        && fsSecInfo.ControllingTeam != aiTeam;
                string fsActionLabel = fsIsEnemySector ? "apoio a captura de" : "apoio de";
                Debug.Log($"{TL("Plan")} FireSupport {u.InstanceId} -> {fsActionLabel} {bestObj.Sector} (dist={bestDist:F0}h score={bestScore:F0})");
            }
        }

        // Passo 5f: atribui transportadores livres
        {
            List<UnitManager> freeTransporters = GetAvailableTransporters(aiTeam);
            foreach (UnitManager u in freeTransporters)
            {
                if (assignedIds.Contains(u.InstanceId)) continue;
                if (!u.TryGetUnitData(out UnitData uData) || uData == null) continue;

                SectorObjective bestObj = null;
                UnitManager bestPassenger = null;
                float bestScore = float.MinValue;
                float bestCapturerDist = -1f;
                float bestRisk = float.MaxValue;
                float bestApcDist = float.MaxValue;
                float bestPickupDist = float.MaxValue;
                bool bestCreatedSlot = false;
                int planThreshold = GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
                foreach (SectorObjective obj in plan.Objectives)
                {
                    bool hasOpenTransportSlot = obj.HasOpenSlot(UnitRole.Transportador);
                    Vector3Int tc;
                    ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
                    if (tgt != null)
                    {
                        tc = tgt.CurrentCellPosition; tc.z = 0;
                    }
                    else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo targetInfo))
                    {
                        tc = targetInfo.RepresentativeCell; tc.z = 0;
                    }
                    else
                    {
                        continue;
                    }
                    Vector3Int uc = u.CurrentCellPosition; uc.z = 0;

                    float sectorRisk = 0f;
                    float capturerDistToObj = -1f;
                    float pickupDist = float.MaxValue;
                    UnitManager pickupPassenger = null;
                    TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo sInfo);
                    if (sInfo != null) sectorRisk = sInfo.GetRiskRatioFor(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
                    foreach (SlotNeed slot in obj.Slots)
                    {
                        if (!IsGroundTransportPassengerSlot(obj, slot, aiTeam)) continue;
                        UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                        if (capturer == null || capturer.IsEmbarked) continue;
                        if (!capturer.TryGetUnitData(out UnitData capData) || FindFittingSlotIndex(u, uData, capturer, capData) < 0) continue;
                        Vector3Int cc = capturer.CurrentCellPosition; cc.z = 0;
                        if (IsTeamProductionBuilding(cc, aiTeam)) continue;
                        float dist = TerrainCostToCell(capturer, cc, tc, planThreshold * 2);
                        float apcToPassenger = SectorManager.HexDistance(uc, cc);
                        if (dist > capturerDistToObj
                            || (Mathf.Abs(dist - capturerDistToObj) <= 0.5f && apcToPassenger < pickupDist))
                        {
                            capturerDistToObj = dist;
                            pickupDist = apcToPassenger;
                            pickupPassenger = capturer;
                        }
                    }
                    if (capturerDistToObj < 0f)
                    {
                        foreach (SlotNeed slot in obj.Slots)
                        {
                            if (!IsGroundTransportPassengerSlot(obj, slot, aiTeam)) continue;
                            UnitManager capturer = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                            if (capturer == null || capturer.IsEmbarked) continue;
                            if (!capturer.TryGetUnitData(out UnitData capData) || FindFittingSlotIndex(u, uData, capturer, capData) < 0) continue;
                            Vector3Int cc = capturer.CurrentCellPosition; cc.z = 0;
                            float dist = TerrainCostToCell(capturer, cc, tc, planThreshold * 2);
                            float apcToPassenger = SectorManager.HexDistance(uc, cc);
                            if (dist > capturerDistToObj
                                || (Mathf.Abs(dist - capturerDistToObj) <= 0.5f && apcToPassenger < pickupDist))
                            {
                                capturerDistToObj = dist;
                                pickupDist = apcToPassenger;
                                pickupPassenger = capturer;
                            }
                        }
                    }
                    if (capturerDistToObj < 0f) continue;

                    List<UnitManager> activeCapturers = new List<UnitManager>();
                    foreach (SlotNeed ts in obj.Slots)
                    {
                        if (!IsGroundTransportPassengerSlot(obj, ts, aiTeam)) continue;
                        UnitManager cap = FindActiveUnit(ts.AssignedUnitId, aiTeam);
                        if (cap != null && !cap.IsEmbarked) activeCapturers.Add(cap);
                    }
                    if (GetCompatibleSlotCapacity(u, activeCapturers) == 0) continue;

                    float apcDistToObj = SectorManager.HexDistance(uc, tc);
                    float pickupReach = Mathf.Max(0, u.RemainingMovementPoints) + ShuttlePickupRange;

                    int totalTransportCapacity = 0;
                    foreach (SlotNeed ts in obj.Slots)
                    {
                        if (ts.Role != UnitRole.Transportador || !ts.Filled) continue;
                        UnitManager transporter = FindActiveUnit(ts.AssignedUnitId, aiTeam);
                        if (transporter == null) continue;
                        totalTransportCapacity += GetCompatibleSlotCapacity(transporter, activeCapturers);
                    }
                    bool transportCapacityMet = totalTransportCapacity >= activeCapturers.Count;
                    bool canCreateOpportunisticSlot = !hasOpenTransportSlot
                        && !transportCapacityMet
                        && capturerDistToObj >= GetEffectiveTransportThresholdForSlot(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)))
                        && pickupDist <= pickupReach + 0.5f;
                    if (!hasOpenTransportSlot && !canCreateOpportunisticSlot) continue;

                    float localScore = capturerDistToObj * 120f
                        - pickupDist * 95f
                        - apcDistToObj * 8f
                        - sectorRisk * 120f;
                    if (canCreateOpportunisticSlot) localScore += 180f;
                    const float eps = 0.5f;
                    bool isBetter = localScore > bestScore + eps
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist - eps)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj > bestCapturerDist + eps)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk - 0.01f)
                        || (localScore >= bestScore - eps && pickupDist < bestPickupDist + eps && capturerDistToObj >= bestCapturerDist - eps && sectorRisk < bestRisk + 0.01f && apcDistToObj < bestApcDist);
                    if (isBetter)
                    {
                        bestScore = localScore;
                        bestCapturerDist = capturerDistToObj;
                        bestRisk = sectorRisk;
                        bestApcDist = apcDistToObj;
                        bestPickupDist = pickupDist;
                        bestPassenger = pickupPassenger;
                        bestCreatedSlot = canCreateOpportunisticSlot;
                        bestObj = obj;
                    }
                }

                if (bestObj == null) continue;
                if (bestCreatedSlot)
                    bestObj.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
                bestObj.TryFillSlot(UnitRole.Transportador, u.InstanceId);
                assignedIds.Add(u.InstanceId);
                ApplyPlanHUD(u, bestObj, UnitRole.Transportador);
                string passengerLabel = bestPassenger != null ? $" passenger={bestPassenger.InstanceId}" : " passenger=?";
                string slotLabel = bestCreatedSlot ? " slot=opportunistic" : " slot=open";
                Debug.Log($"{TL("Plan")} Transportador {u.InstanceId} -> {bestObj.Sector} ({passengerLabel}{slotLabel} capturerDist={bestCapturerDist:F0}h pickup={bestPickupDist:F0}h risk={bestRisk:F2} apcDist={bestApcDist:F0}h score={bestScore:F0})");
            }
        }

        // Passo 6: reaplica HUD + libera suporte sem capturador
        ReleaseOffensiveSupportWithoutCapturer(plan, aiTeam);

        // Go Green suspende o papel rogue para TODA composição. Só depois de todas as alocações
        // normais absorvemos cada unidade que ainda ficou sem slot no plano final ">>". Se o
        // monitor declarar fracasso, rallyGoGreen volta a false e essas novas absorções cessam,
        // restaurando naturalmente o comportamento rogue durante o reassembly.
        if (rallyGoGreen)
            AssignUnslottedUnitsToGoGreenInvasion(plan, aiTeam);

        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled && !freeCapturers.Exists(u => u.InstanceId == slot.AssignedUnitId))
                {
                    UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    if (u != null) ApplyPlanHUD(u, obj, slot.Role);
                }

        // Passo 7: limpa badge de unidades fora de qualquer slot ativo
        var slottedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            foreach (SlotNeed slot in obj.Slots)
                if (slot.Filled) slottedIds.Add(slot.AssignedUnitId);
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.SlotIndex != ResolveAISlotKey(aiTeam) || u.IsDead) continue;
            if (!slottedIds.Contains(u.InstanceId)) u.ClearAIAssignedPlan();
        }

        // Passo 8: atualiza dist�ncias reais de cada slot at� seu objetivo
        RefreshSlotDistances(plan, aiTeam);

        int totalAssigned = 0;
        var planLog = new System.Text.StringBuilder();
        planLog.AppendLine($"{TL("Plan")} {aiTeam} � {plan.Objectives.Count} objetivos:");
        foreach (SectorObjective obj in plan.Objectives)
        {
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled)
                {
                    planLog.AppendLine($"  pri={obj.Priority} {obj.Sector}: �");
                    continue;
                }
                totalAssigned++;
                UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                string distStr = slot.DistanceToObjective >= 0 ? $"{slot.DistanceToObjective}h" : "?";
                string embarkedTag = (u != null && u.IsEmbarked) ? " [APC]" : "";
                planLog.AppendLine($"  pri={obj.Priority} {obj.Sector}: {slot.Role} Unit{slot.AssignedUnitId}{embarkedTag} @ {distStr}");
            }
        }
        planLog.Append($"  ? {totalAssigned} atribu�dos | {plan.RogueUnitIds.Count} rogues");
        Debug.Log(planLog.ToString());

        AISectorIntentAnalyzer.RebuildAndLog(snapshot, plan, intel, "Plan");
    }

    private ConstructionSector ResolveUnitSectorForPlan(UnitManager unit)
    {
        if (unit == null)
            return ConstructionSector.None;

        Vector3Int cell = unit.CurrentCellPosition;
        cell.z = 0;

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(boardTilemap, cell);
        if (construction != null && construction.Sector != ConstructionSector.None)
            return construction.Sector;

        ConstructionSector bestSector = ConstructionSector.None;
        float bestDist = float.MaxValue;
        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info == null) continue;
            Vector3Int rc = info.RepresentativeCell;
            rc.z = 0;
            float dist = SectorManager.HexDistance(cell, rc);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestSector = info.Sector;
            }
        }

        return bestDist <= 2f ? bestSector : ConstructionSector.None;
    }

    private float CalculateSectorContinuityAssignmentCost(UnitManager unit, ConstructionSector targetSector, TeamId aiTeam)
    {
        if (unit == null || targetSector == ConstructionSector.None)
            return 0f;

        // Estabilidade de eixo + histerese (manter o objetivo do turno anterior): somam-se a
        // qualquer custo base de continuidade.
        float axisCost = CalculateAxisStabilityCost(unit, targetSector)
            + GetObjectiveHysteresisBonus(unit, targetSector);

        ConstructionSector originSector = ResolveUnitSectorForPlan(unit);
        if (originSector == ConstructionSector.None)
            return axisCost;
        if (originSector == targetSector)
            return -8f + axisCost;
        if (IsSectorContinuityNeighbor(originSector, targetSector, aiTeam))
            return -6f + axisCost;

        if (TryGetAnySectorInfo(originSector, out SectorManager.SectorInfo originInfo)
            && TryGetAnySectorInfo(targetSector, out SectorManager.SectorInfo targetInfo))
        {
            float originHqDist = originInfo.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            float targetHqDist = targetInfo.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
            if (targetHqDist > originHqDist)
                return 12f + axisCost;
        }

        return axisCost;
    }

    // Custo de estabilidade/rebalanceamento de eixo, com sinal:
    //  - mesmo eixo / rogue / alvo-fora-de-eixo: 0 (neutro).
    //  - eixo-alvo FAMINTO (presenca >= AxisStarvedPresenceGap menor que a do atual): recompensa
    //    (custo negativo) — puxa a unidade para cobrir o eixo vazio em vez de lotar o cheio.
    //  - eixo-alvo igual/mais coberto: penalidade — mantem "os caras do eixo 1 no eixo 1".
    // Distancia/handoff grande ainda pode vencer qualquer dos dois.
    private float CalculateAxisStabilityCost(UnitManager unit, ConstructionSector targetSector)
    {
        if (currentAxisMap == null || unit == null)
            return 0f;
        int unitEixo = unit.AIEixo;
        if (unitEixo <= 0)
            return 0f;
        int targetEixo = currentAxisMap.GetEixo(targetSector);
        if (targetEixo <= 0 || targetEixo == unitEixo)
            return 0f;

        int curPresence = GetEixoPresence(unitEixo);
        int tgtPresence = GetEixoPresence(targetEixo);
        if (curPresence - tgtPresence >= AxisStarvedPresenceGap)
            return -AxisStarvedCoverReward; // eixo-alvo faminto: rebalanceia
        return AxisStabilitySwitchCost;      // eixo-alvo coberto: gruda na faixa
    }

    private void BuildEixoPresence(TeamId aiTeam)
    {
        var presence = new Dictionary<int, int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.SlotIndex != ResolveAISlotKey(aiTeam) || u.IsDead)
                continue;
            int e = u.AIEixo;
            if (e <= 0)
                continue;
            presence.TryGetValue(e, out int c);
            presence[e] = c + 1;
        }
        currentEixoPresence = presence;
    }

    private int GetEixoPresence(int eixo)
        => currentEixoPresence != null && currentEixoPresence.TryGetValue(eixo, out int c) ? c : 0;

    private void BuildPreviousObjectiveSnapshot(TeamId aiTeam)
    {
        var map = new Dictionary<int, string>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u == null || u.SlotIndex != ResolveAISlotKey(aiTeam) || u.IsDead)
                continue;
            if (!u.AIHasAssignedPlan)
                continue;
            string sector = u.AIAssignedPlanName;
            if (!string.IsNullOrEmpty(sector))
                map[u.InstanceId] = sector;
        }
        previousUnitObjectiveSectorName = map;
    }

    // Bonus de histerese: negativo se `targetSector` e o mesmo objetivo/setor que a unidade tinha no
    // turno anterior (mantem quem ja estava a caminho). 0 caso contrario.
    private float GetObjectiveHysteresisBonus(UnitManager unit, ConstructionSector targetSector)
    {
        if (previousUnitObjectiveSectorName == null || unit == null
            || targetSector == ConstructionSector.None)
            return 0f;
        return previousUnitObjectiveSectorName.TryGetValue(unit.InstanceId, out string prev)
            && prev == targetSector.ToString()
            ? -PreviousObjectiveHysteresisBonus
            : 0f;
    }

    private int GetAxisFrontPriorityBonus(ConstructionSector sector)
    {
        if (currentAxisMap == null)
            return 0;
        int eixo = currentAxisMap.GetEixo(sector);
        if (eixo <= 0)
            return 0;
        return currentAxisMap.TryGetAxis(eixo, out InvasionAxisMap.Axis axis) && axis.FrontSector == sector
            ? AxisFrontPriorityBonus
            : 0;
    }

    // Verdadeiro se o setor e o ALVO DE TRANSPORTE do seu eixo: normalmente a frente, mas o
    // proximo no do corredor se a frente ja esta sob captura (pipelining — ver
    // InvasionAxisMap.GetTransportTargetSector). Usado para gatear intencao antecipatoria de
    // transporte; o gate de PROFUNDIDADE (>= limiar) e aplicado pelo chamador sobre este setor.
    private bool IsAxisTransportTarget(ConstructionSector sector)
    {
        if (currentAxisMap == null)
            return false;
        int eixo = currentAxisMap.GetEixo(sector);
        if (eixo <= 0)
            return false;
        return currentAxisMap.GetTransportTargetSector(eixo) == sector;
    }

    // 2a trava: captura ofensiva de setor FORA do eixo (GetEixo==0 — jardim inimigo, alem do rally
    // ou sem eixo naquela direcao) fica travada enquanto ha eixo de rally montando massa e NENHUM
    // GoGreen. Pre-GoGreen tudo deve afunilar pro rally; peelar uma unidade pro quintal inimigo
    // atrasa o GoGreen e a manda sozinha (suicida). Destrava com GoGreen ou sem eixo de rally.
    private bool IsOffAxisCaptureGatedByRally(
        ConstructionSector sector, TeamId aiTeam, AIRallyPlanContext rallyContext, int turnNumber, AIIntelReport intel)
    {
        if (currentAxisMap == null)
            return false;
        if (currentAxisMap.GetEixo(sector) > 0)
            return false; // setor coberto por um eixo — e o avanco principal, nao trava
        if (rallyContext.RallyPointCount <= 0)
            return false; // sem eixo de rally definido — nada pra proteger, captura normal
        if (AnyOwnedRallyAtGoGreen(aiTeam, rallyContext, turnNumber, intel))
            return false; // massa pronta em algum rally — libera oportunismo off-axis
        if (GetMacroTerritoryForInspection(aiTeam).Winning)
            return false; // DOMÍNIO macro (Ganhando) — libera oportunismo off-axis igual ao GoGreen
        return true; // off-axis + rally montando + sem GoGreen + não dominando => trava
    }

    private static bool IsSectorContinuityNeighbor(ConstructionSector originSector, ConstructionSector targetSector, TeamId aiTeam)
    {
        if (originSector == ConstructionSector.None || targetSector == ConstructionSector.None)
            return false;
        if (ComputeForwardNeighborSector(originSector, aiTeam) == targetSector)
            return true;
        if (TryGetRequiredCampaignPredecessor(targetSector, aiTeam, out ConstructionSector campaignRear)
            && campaignRear == originSector)
            return true;
        if (!SectorManager.TryGetSectorInfo(originSector, out SectorManager.SectorInfo originInfo))
            return false;
        return originInfo.ClosestNeighbor1 == targetSector || originInfo.ClosestNeighbor2 == targetSector;
    }
    private void RefreshSlotDistances(TeamObjectivePlan plan, TeamId aiTeam)
    {
        foreach (SectorObjective obj in plan.Objectives)
        {
            Vector3Int targetCell = Vector3Int.zero;
            bool hasTarget = false;
            ConstructionManager tgt = FindCapturableInSector(obj.Sector, aiTeam);
            if (tgt != null)
            {
                targetCell = tgt.CurrentCellPosition; targetCell.z = 0;
                hasTarget = true;
            }
            else if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            {
                targetCell = info.RepresentativeCell; targetCell.z = 0;
                hasTarget = true;
            }

            foreach (SlotNeed slot in obj.Slots)
            {
                slot.DistanceToObjective = -1;
                if (!slot.Filled || !hasTarget) continue;

                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (unit == null) continue;

                Vector3Int fromCell = (unit.IsEmbarked && unit.EmbarkedTransporter != null)
                    ? unit.EmbarkedTransporter.CurrentCellPosition
                    : unit.CurrentCellPosition;
                fromCell.z = 0;

                if (unit.TryGetUnitData(out UnitData unitData) && unitData != null
                    && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, unitData, out int d1))
                    slot.DistanceToObjective = d1;
                else if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int d2))
                    slot.DistanceToObjective = d2;
                else
                    slot.DistanceToObjective = (int)SectorManager.HexDistance(fromCell, targetCell);
            }
        }
    }

    // Backtracking �timo: minimiza a soma de passos reais de caminho unit?objetivo.
    // Para N = 8 e M = 8 objetivos abertos, P(M,N) = 40 320 itera��es � trivial.
    private static void SolveAssignment(
        List<UnitManager> units,
        List<(SectorObjective obj, Vector3Int cell)> objs,
        bool[] usedObj,
        int[] current,
        int depth,
        int maxDepth,
        float cost,
        float[,] distMatrix,
        ref float bestCost,
        ref int[] bestAssign)
    {
        if (depth == maxDepth)
        {
            if (cost < bestCost)
            {
                bestCost = cost;
                System.Array.Copy(current, bestAssign, maxDepth);
            }
            return;
        }

        for (int j = 0; j < objs.Count; j++)
        {
            if (usedObj[j]) continue;
            float newCost = cost + distMatrix[depth, j];
            if (newCost >= bestCost) continue;
            current[depth] = j;
            usedObj[j] = true;
            SolveAssignment(units, objs, usedObj, current, depth + 1, maxDepth, newCost, distMatrix, ref bestCost, ref bestAssign);
            usedObj[j] = false;
        }
    }
}
