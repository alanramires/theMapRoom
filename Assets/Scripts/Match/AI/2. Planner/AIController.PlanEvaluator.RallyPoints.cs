using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public partial class AIController
{
    private const int RallyPointSectorPriorityBonus = 55;
    private const int RallyHoldRadius = 2;
    private const int RallyMinimumCapturers = 4;
    private const int RallyMinimumBreakthrough = 2;
    private const int RallyArtilleryRecruitRange = 8;
    private const int RallyMaximumArtilleryUnits = 12;
    private const int RallyInfantryTransportDistance = 7;
    private const int RallyUsefulApcCapacity = 2;
    // Após o GoGreen a supressão NÃO solta mais no relógio: o release é por DESFECHO (a invasão
    // fracassou — colapso ou estagnação — via UpdateInvasionMonitor). Este valor é só um TETO de
    // segurança pra um sensor bugado não suprimir pra sempre.
    private const int RallyGoGreenSuppressTurns = 12;
    // Invasão pós-GoGreen: monitor de desfecho (re-montagem da 2ª onda quando a operação falha).
    private const int InvasionGraceTurns = 2;          // não julga antes da onda formar/avançar
    private const int InvasionStallTurns = 3;          // turnos sem progresso => estagnou
    private const int InvasionAssaultGateRange = 2;    // <=2h do alvo = assaltando, não travado
    // Foco pegajoso: o foco da operação só troca se outro rally bater o atual por esta margem.
    private const float RallyFocusSwitchMargin = 1.5f;
    private static readonly Dictionary<string, int> rallyGoGreenTurns = new Dictionary<string, int>();
    private static readonly Dictionary<string, AIRallyHudSnapshot> rallyHudStates = new Dictionary<string, AIRallyHudSnapshot>();
    // Monitor da operação de invasão POR TIME (a força de invasão é team-global — união das
    // unidades nos objetivos de base inimiga, não atrelada a um rally específico).
    private static readonly Dictionary<int, InvasionMonitor> invasionMonitors = new Dictionary<int, InvasionMonitor>();
    // Memória do FocusSector por time, para hysteresis (não drenar um rally que já monta massa).
    private static readonly Dictionary<int, ConstructionSector> rallyFocusMemory = new Dictionary<int, ConstructionSector>();

    private struct InvasionMonitor
    {
        public int BestDistance;   // menor distância-terrestre já alcançada da força -> alvo (MaxValue = sem medição)
        public int StallCounter;   // turnos seguidos sem melhorar BestDistance (fora de hold legítimo)
    }

    private struct AIRallyPlanContext
    {
        public HashSet<ConstructionSector> TargetingEnemyHQ;
        public int AISlotIndex;
        public int RallyPointCount;
    }

    private struct AIRallyReadiness
    {
        public bool Held;
        public bool HoldReady, BreakthroughReady, ArtilleryReady, ForceReady;
        public int Capturers;
        public float Assault;
        public float Artillery;
        public int HeldRallies;
        public ConstructionSector FocusSector;
        public bool IsFocus;
        public int AirSurveillance;
        public int Logistics;
        public int VisibleThreats;
        public int KnownEnemyForce;
        public int RequiredForce;
        public int ForceScore;
        public int Occupation;
        public float LocalEnemyForce;
        public float LocalRatio;
        public bool Saturated;
        public bool GoGreen;
        public AIRallyAssemblyState State;
        public string Status;
        public string Missing;
    }

    private struct AIRallyAggregate
    {
        public int HeldRallies;
        public int Capturers;
        public float Assault;
        public float Artillery;
        public int AirSurveillance;
        public int Logistics;
        public ConstructionSector FocusSector;
        public int Occupation;
    }

    private struct AIRallyInfluence
    {
        public bool Active;
        public ConstructionSector Sector;
        public Vector3Int Anchor;
        public AIRallyAssemblyState State;
        public AIRallyReadiness Readiness;
        public string Reason;
    }

    private struct AIRallyHudSnapshot
    {
        public AIRallyAssemblyState State;
        public string Reason;
        public int TurnNumber;
        public bool IsMain;   // rally principal = setor do foco da operação (FocusSector)
    }

    private static AIRallyPlanContext BuildRallyPlanContext(
        TeamId aiTeam,
        int aiSlotIndex,
        int turnNumber,
        AIIntelReport intel)
    {
        var staleFacts = new List<(int rally, int slot, int turn)>();
        foreach (var key in rallyReadinessFacts.Keys) if (key.slot == aiSlotIndex) staleFacts.Add(key);
        foreach (var key in staleFacts) rallyReadinessFacts.Remove(key);
        rallyReachFacts.Clear();
        ClearRallyAreasForSlot(aiSlotIndex);
        AIRallyPlanContext context = new AIRallyPlanContext
        {
            TargetingEnemyHQ = new HashSet<ConstructionSector>(),
            AISlotIndex = aiSlotIndex
        };

        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint)
                continue;

            if (rally.Sector == ConstructionSector.None)
            {
                Debug.Log($"[AI Rally][T{turnNumber}][{aiTeam}] ignorado {rally.name}: rally sem setor");
                continue;
            }

            if (!IsRallyOwnedBySlot(rally, aiTeam, aiSlotIndex))
                continue;

            context.TargetingEnemyHQ.Add(rally.Sector);
            context.RallyPointCount++;
            // Num rally compartilhado o dono que interessa no log e QUEM ESTA PLANEJANDO,
            // nao o primeiro da lista: os dois lados param no mesmo predio.
            LogRallyReadiness(
                rally,
                aiSlotIndex >= 0 ? aiSlotIndex : rally.PrimaryRallyOwnerSlot,
                aiTeam, turnNumber, intel);
        }

        return context;
    }

    private static bool IsEnemyHQRallySector(AIRallyPlanContext context, ConstructionSector sector)
    {
        return context.TargetingEnemyHQ != null && context.TargetingEnemyHQ.Contains(sector);
    }

    private static bool IsEnemyHQRallySectorHeld(AIRallyPlanContext context, ConstructionSector sector, TeamId aiTeam)
    {
        // Conquistado = o DONO ATUAL do ponto de rally e a AI, nao maioria/setor (ma leitura).
        return IsEnemyHQRallySector(context, sector)
            && IsRallyPointHeldByTeam(sector, aiTeam);
    }

    // O ponto de rally do setor esta sob controle ATUAL da AI? (rally.SlotIndex == ResolveAISlotKey(aiTeam) — o "Slot ID"
    // atual do ponto, nao o "Rally Owner Slot" que e so a intencao de dono quando conquistado.)
    private static bool IsRallyPointHeldByTeam(ConstructionSector sector, TeamId aiTeam)
    {
        if (sector == ConstructionSector.None || ConstructionManager.AllActive == null)
            return false;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
            if (rally != null && rally.IsRallyPoint && rally.Sector == sector)
                return rally.SlotIndex == ResolveAISlotKey(aiTeam);
        return false;
    }

    private static int GetRallySectorPriorityBonus(AIRallyPlanContext context, ConstructionSector sector, TeamId aiTeam)
    {
        return IsEnemyHQRallySector(context, sector) ? RallyPointSectorPriorityBonus : 0;
    }

    // RallyOwnerSlotIndex marks the slot that uses this sector as its invasion rally.
    private static bool IsRallyOwnedBySlot(
        ConstructionManager rally,
        TeamId aiTeam,
        int aiSlotIndex)
    {
        if (rally == null || !rally.IsRallyPoint)
            return false;

        MatchController mc = GetMatchController();
        if (mc == null)
            return false;

        int aiSlot = aiSlotIndex >= 0 ? aiSlotIndex : ResolveAISlotIndex(aiTeam, mc);
        if (aiSlot < 0)
            return false;

        return rally.IsRallyForSlot(aiSlot);
    }

    private static bool IsValidRallyAssemblySectorForSlot(ConstructionSector sector, TeamId aiTeam, int aiSlotIndex)
    {
        if (sector == ConstructionSector.None)
            return false;

        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != sector)
                continue;

            if (IsRallyOwnedBySlot(rally, aiTeam, aiSlotIndex))
                return true;
        }

        return false;
    }

    private static bool TryGetOwnedRallySlot(ConstructionManager rally, TeamId aiTeam, out int ownerSlot)
    {
        ownerSlot = -1;
        int aiSlot = ResolveAISlotIndex(aiTeam, GetMatchController());
        if (!IsRallyOwnedBySlot(rally, aiTeam, aiSlot))
            return false;

        // O slot de QUEM PERGUNTOU, nao o primeiro da lista: a pergunta e "este rally e
        // meu?", e num rally dos dois lados o primeiro da lista pode ser o outro.
        ownerSlot = aiSlot;
        return true;
    }

    private static int ResolveAISlotIndex(TeamId aiTeam, MatchController mc)
    {
        if (mc == null || aiTeam == TeamId.Neutral)
            return -1;

        if (mc.ActiveTeam == aiTeam)
            return mc.ActivePlayerListIndex;

        return ResolveAISlotKey(aiTeam);
    }

    private static void LogRallyReadiness(ConstructionManager rally, int ownerSlot, TeamId aiTeam, int turnNumber, AIIntelReport intel)
    {
        AIRallyReadiness r = EvaluateRallyReadiness(rally, aiTeam, turnNumber, intel);
        PublishRallyHudState(rally, r.State, r.Status, turnNumber, r.IsFocus, ownerSlot);
        Debug.Log($"[AI Rally][T{turnNumber}][{aiTeam}] {rally.Sector} held={r.Held} cap={r.Capturers}/{RallyMinimumCapturers} ruptura={r.Assault:0.#}/{RallyMinimumBreakthrough} suporte={r.Artillery:0.#} ocupacao={r.Occupation} force={r.ForceScore}/{r.RequiredForce} inimigoLocal={r.LocalEnemyForce:0.#} razao={r.LocalRatio:0.#} saturado={r.Saturated} missing={r.Missing} {r.Status}");
    }

    private static AIRallyReadiness EvaluateRallyReadiness(ConstructionManager rally, TeamId aiTeam)
    {
        return EvaluateRallyReadiness(rally, aiTeam, -1, null);
    }

    private static AIRallyReadiness EvaluateRallyReadiness(
        ConstructionManager rally, TeamId aiTeam, int turnNumber, AIIntelReport intel)
    {
        var r = new AIRallyReadiness { Status = "WAIT_HOLD", State = AIRallyAssemblyState.WaitHold, Missing = "hold" };
        if (rally == null) return r;
        int slot = ResolveAISlotKey(aiTeam);
        var context = GetRallyContext(slot, rally.BoardTilemap);
        int turn = turnNumber >= 0 ? turnNumber : GetMatchController()?.CurrentTurn ?? -1;
        var key = (rally.GetEntityId().GetHashCode(), slot, turn);
        if (rallyReadinessFacts.TryGetValue(key, out AIRallyReadiness fact)) return fact;
        AIRallyAggregate aggregate = BuildRallyAggregate(aiTeam);
        r.Held = rally.SlotIndex == slot;
        r.HeldRallies = aggregate.HeldRallies;
        r.FocusSector = aggregate.FocusSector;
        r.IsFocus = rally.Sector == aggregate.FocusSector;
        r.Capturers = aggregate.Capturers;
        r.Assault = aggregate.Assault;
        r.Artillery = aggregate.Artillery;
        r.Occupation = aggregate.Occupation;
        r.AirSurveillance = aggregate.AirSurveillance;
        r.Logistics = aggregate.Logistics;
        r.ForceScore = RallyForceScore(r.Capturers, r.Assault, r.Artillery);
        r.LocalRatio = context != null && context.ActivePreset != null ? context.ActivePreset.rallyLocalRatio : 1.5f;
        r.LocalEnemyForce = CountRallyLocalEnemyForce(rally, aiTeam, out int threats);
        r.VisibleThreats = threats;
        r.KnownEnemyForce = CountLiveEnemyUnits(aiTeam);
        r.RequiredForce = Mathf.CeilToInt(r.LocalRatio * r.LocalEnemyForce);
        r.HoldReady = r.Held && r.Capturers >= RallyMinimumCapturers;
        r.BreakthroughReady = r.Assault >= RallyMinimumBreakthrough;
        r.ArtilleryReady = true; // suporte e informacao, nunca uma trava.
        r.ForceReady = r.ForceScore >= r.RequiredForce;
        SectorObjective objective = FindRallyObjective(slot, rally.Sector);
        r.Saturated = IsRallySaturated(objective, turn, r.HoldReady && r.BreakthroughReady);
        bool macroDominating = BuildMacroTerritoryContext(aiTeam, SectorManager.GetAllSectorInfos(), 6,
            CountLiveUnitsOfTeam(aiTeam), r.KnownEnemyForce, projectionSource: context).Phase == AIMacroTerritoryPhase.Dominating;
        int friendlyForce = CountLiveUnitsOfTeam(aiTeam);
        bool numericalDominance = friendlyForce >= 2 * Mathf.Max(1, r.KnownEnemyForce);
        r.GoGreen = r.Held && ((r.HoldReady && r.BreakthroughReady && (r.ForceReady || r.Saturated))
            || macroDominating || numericalDominance);
        if (r.GoGreen)
        {
            r.State = AIRallyAssemblyState.GoGreen;
            r.Status = r.HoldReady && r.BreakthroughReady && r.ForceReady ? "GO_GREEN"
                : r.Saturated ? "GO_GREEN(saturado)" : numericalDominance ? "GO_GREEN(2:1)" : "GO_GREEN(macro)";
            r.Missing = "-";
        }
        else if (r.Held)
        {
            r.State = AIRallyAssemblyState.Assembling;
            r.Status = "ASSEMBLING";
            r.Missing = BuildRallyMissingText(r);
        }
        rallyReadinessFacts[key] = r;
        return r;
    }

    private static AIRallyAggregate BuildRallyAggregate(TeamId aiTeam)
    {
        var aggregate = new AIRallyAggregate { FocusSector = ConstructionSector.None };
        var held = new List<ConstructionManager>();
        int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.SlotIndex != ResolveAISlotKey(aiTeam))
                continue;
            if (!IsRallyOwnedBySlot(rally, aiTeam, slot))
                continue;
            held.Add(rally);
        }

        aggregate.HeldRallies = held.Count;
        if (held.Count == 0)
            return aggregate;

        float bestFocusScore = float.MinValue;
        ConstructionSector bestFocusSector = ConstructionSector.None;
        foreach (ConstructionManager rally in held)
        {
            float score = ScoreRallyLaunchFocus(rally, aiTeam);
            if (score > bestFocusScore
                || (Mathf.Approximately(score, bestFocusScore)
                    && (bestFocusSector == ConstructionSector.None
                        || (int)rally.Sector < (int)bestFocusSector)))
            {
                bestFocusScore = score;
                bestFocusSector = rally.Sector;
            }
        }

        // Foco pegajoso (hysteresis): mantém o foco do turno anterior enquanto ele continuar held,
        // a não ser que outro rally o bata por margem folgada (>= RallyFocusSwitchMargin x). Sem isso,
        // a oscilação de massa local fazia o foco pular entre rallies e DRENAR o que já montava massa.
        ConstructionSector chosenFocus = bestFocusSector;
        int aiSlotKey = ResolveAISlotKey(aiTeam);
        if (rallyFocusMemory.TryGetValue(aiSlotKey, out ConstructionSector prevFocus)
            && prevFocus != ConstructionSector.None
            && prevFocus != bestFocusSector)
        {
            ConstructionManager prevRally = held.Find(r => r != null && r.Sector == prevFocus);
            if (prevRally != null)
            {
                float prevScore = ScoreRallyLaunchFocus(prevRally, aiTeam);
                if (bestFocusScore < prevScore * RallyFocusSwitchMargin)
                    chosenFocus = prevFocus;
            }
        }

        aggregate.FocusSector = chosenFocus;
        rallyFocusMemory[aiSlotKey] = chosenFocus;

        var counted = new HashSet<int>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.SlotIndex != aiSlotKey || !unit.gameObject.activeInHierarchy
                || !counted.Add(unit.InstanceId) || !unit.TryGetUnitData(out UnitData data)) continue;
            bool inCore = false;
            foreach (ConstructionManager rally in held)
                if (IsRallyParticipantPresent(unit, rally, ReachBand.Tactical)) { inCore = true; break; }
            if (!inCore) continue;
            if (HasRallyCaptureSkill(data)) aggregate.Capturers++;
            if (unit.IsEmbarked) { aggregate.Occupation++; continue; }
            UnitForce force = UnitRoleCompatibility.ResolveForce(data);
            if (force == UnitForce.Ocupacao) aggregate.Occupation++;
            if (!IsRallyUnitOperational(unit)) continue;
            aggregate.Assault += GetRallyAssaultWeight(data);
            aggregate.Artillery += GetRallyArtilleryWeight(data);
            if (IsAirSurveillanceUnit(unit)) aggregate.AirSurveillance++;
            if (HasRole(data, UnitRole.Logistica)) aggregate.Logistics++;
        }

        return aggregate;
    }

    private static float ScoreRallyLaunchFocus(ConstructionManager rally, TeamId aiTeam)
    {
        float score = 0f;
        foreach (UnitManager unit in UnitManager.AllActive)
            if (unit != null && !unit.IsDead && unit.SlotIndex == ResolveAISlotKey(aiTeam)
                && unit.TryGetUnitData(out UnitData data) && IsRallyParticipantPresent(unit, rally, ReachBand.Tactical))
                score += HasRallyCaptureSkill(data) ? 100f : unit.IsEmbarked ? 0f
                    : GetRallyAssaultWeight(data) * 300f + GetRallyArtilleryWeight(data) * 200f;
        return score;
    }







    // Nº de unidades inimigas VIVAS de fato (ground truth via UnitManager.AllActive). NÃO usa o intel
    // histórico (`enemyLastKnownUnits`): ele acumula fantasmas — unidades mortas fora da visão ou que
    // FUNDIRAM continuam na lista (e até com confiança 1.0 quando a morte/fusão foi recente, porque o
    // `destroyed` nem sempre é marcado). O número real é o de unidades inimigas vivas no tabuleiro
    // (ex.: 6), simétrico ao próprio MyUnits.Count. Ignora fog-of-war de propósito: é um termômetro
    // estratégico (ganhando/perdendo), não decisão tática.
    private static int CountLiveEnemyUnits(TeamId aiTeam)
    {
        List<UnitManager> all = UnitManager.AllActive;
        if (all == null) return 0;
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            UnitManager u = all[i];
            if (u == null || u.IsDead) continue;
            if (u.SlotIndex == ResolveAISlotKey(aiTeam) || u.TeamId == TeamId.Neutral) continue;
            n++;
        }
        return n;
    }

    // Unidades vivas do próprio time (ground truth). Simétrico ao CountLiveEnemyUnits — usado pra
    // calcular o macro fresco no rally sem depender do snapshot.
    private static int CountLiveUnitsOfTeam(TeamId team)
    {
        List<UnitManager> all = UnitManager.AllActive;
        if (all == null) return 0;
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            UnitManager u = all[i];
            if (u != null && !u.IsDead && u.TeamId == team)
                n++;
        }
        return n;
    }

    private static string BuildRallyMissingText(AIRallyReadiness readiness)
    {
        var missing = new List<string>();
        if (!readiness.Held) missing.Add("hold");
        if (readiness.Capturers < RallyMinimumCapturers) missing.Add($"cap({readiness.Capturers}/{RallyMinimumCapturers})");
        if (!readiness.BreakthroughReady) missing.Add($"ruptura({readiness.Assault:0.#}/{RallyMinimumBreakthrough})");
        if (!readiness.ForceReady) missing.Add($"forca({readiness.ForceScore}/{readiness.RequiredForce})");
        return missing.Count == 0 ? "-" : string.Join("+", missing);
    }

    private static void UpdateRallyObjectiveState(
        SectorObjective obj,
        ConstructionManager rally,
        TeamId aiTeam,
        int turnNumber,
        AIIntelReport intel)
    {
        if (obj == null || rally == null)
            return;

        AIRallyReadiness readiness = EvaluateRallyReadiness(
            rally,
            aiTeam,
            turnNumber,
            intel);
        ObserveRallySaturation(obj, turnNumber, readiness.HoldReady && readiness.BreakthroughReady,
            readiness.Assault, readiness.ForceScore);
        readiness.Saturated = IsRallySaturated(obj, turnNumber, readiness.HoldReady && readiness.BreakthroughReady);
        if (readiness.Saturated && readiness.Held && readiness.HoldReady && readiness.BreakthroughReady)
        { readiness.GoGreen = true; readiness.State = AIRallyAssemblyState.GoGreen; readiness.Status = "GO_GREEN(saturado)"; readiness.Missing = "-"; }
        rallyReadinessFacts[(rally.GetEntityId().GetHashCode(), ResolveAISlotKey(aiTeam), turnNumber)] = readiness;
        Vector3Int rallyAnchor = rally.CurrentCellPosition;
        int heldRallies = Mathf.Max(1, readiness.HeldRallies);
        EnsureRallyAssemblySlots(obj, Mathf.CeilToInt(RallyMinimumCapturers / (float)heldRallies),
            Mathf.CeilToInt(RallyMinimumBreakthrough / (float)heldRallies), aiTeam, rallyAnchor);
        obj.RallyState = readiness.State;
        obj.RallyReadinessReason = $"{readiness.Status} cap={readiness.Capturers}/{RallyMinimumCapturers} ruptura={readiness.Assault:0.#}/{RallyMinimumBreakthrough} suporte={readiness.Artillery:0.#} force={readiness.ForceScore}/{readiness.RequiredForce} inimigoLocal={readiness.LocalEnemyForce:0.#} razao={readiness.LocalRatio:0.#} saturado={readiness.Saturated} missing={readiness.Missing}";
        PublishRallyHudState(rally, readiness.State, obj.RallyReadinessReason, turnNumber,
            rally != null && rally.Sector != ConstructionSector.None && rally.Sector == readiness.FocusSector,
            ResolveAISlotKey(aiTeam));

        if (readiness.GoGreen && obj.RallyGoGreenTurn < 0)
        {
            obj.RallyGoGreenTurn = turnNumber;
            StartRallyAdvance(obj, rally, aiTeam);
            // O Go Green pertence a operacao, nao a um predio isolado. Suprime todos os
            // rallies DESIGNADOS para este slot, para que feeders nao iniciem uma segunda
            // montagem paralela.
            //
            // Designacao, nao posse: antes isto exigia tambem que o predio ja fosse meu, e
            // ai um rally meu ainda por conquistar ficava de fora da supressao — justamente
            // o candidato a virar a segunda montagem, porque a montagem so pede designacao
            // (IsValidRallyAssemblySectorForSlot). Mesma distincao que o resto do arquivo
            // ja faz: rally e onde eu PAROU, nao o que esta na minha mao hoje.
            int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
            foreach (ConstructionManager designado in ConstructionManager.AllActive)
                if (designado != null && IsRallyOwnedBySlot(designado, aiTeam, slot))
                    RememberRallyGoGreen(aiTeam, designado.Sector, turnNumber);
        }
    }

    // O save pode retomar a partir do Stage 2, caminho que deliberadamente nao refaz o plano.
    // Recalcula somente o estado operacional dos rallies e repopula os caches runtime/HUD.
    private void RefreshRestoredRallyObjectiveStates(
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        if (plan == null || plan.Objectives == null || snapshot == null)
            return;

        AIIntelReport intel = BuildPlanIntelReport(snapshot);
        int refreshed = 0;
        int missing = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.ObjectiveType != AIObjectiveType.RallyAssembly)
                continue;

            // SLOT, nao time: quem escreve a chave e RememberRallyGoGreen, com
            // ResolveAISlotKey. Com o numero do time aqui, este Remove so acertava quando
            // time e slot calhavam de ter o mesmo numero — e nos outros casos apagava
            // a entrada de ninguem, calado.
            string goGreenKey = $"{ResolveAISlotKey(snapshot.AITeam)}:{obj.Sector}";
            rallyGoGreenTurns.Remove(goGreenKey);
            if (obj.RallyGoGreenTurn >= 0)
                RememberRallyGoGreen(snapshot.AITeam, obj.Sector, obj.RallyGoGreenTurn);

            if (!TryFindOwnedRallyForSector(obj.Sector, snapshot.AITeam, out ConstructionManager rally))
            {
                obj.RallyState = AIRallyAssemblyState.WaitHold;
                obj.RallyReadinessReason = "WAIT_HOLD missing=rally";
                missing++;
                continue;
            }

            UpdateRallyObjectiveState(obj, rally, snapshot.AITeam, snapshot.TurnNumber, intel);
            refreshed++;
        }

        if (refreshed > 0 || missing > 0)
            Debug.Log($"[AI Rally][T{snapshot.TurnNumber}][{snapshot.AITeam}] resume: " +
                      $"estado restaurado={refreshed} rally ausente={missing}");

        // Multi-centro: sem consolidação/dreno no resume também.
        var assignedIds = new HashSet<int>();
        foreach (SectorObjective obj in plan.Objectives)
            if (obj?.Slots != null)
                foreach (SlotNeed slot in obj.Slots)
                    if (slot != null && slot.Filled)
                        assignedIds.Add(slot.AssignedUnitId);
        RecruitFreeUnitsForRally(plan, snapshot.AITeam, assignedIds);
        RecruitNearbyRogueArtilleryForRally(plan, snapshot.AITeam, assignedIds);
        ReconcileRestoredPlanAssignments(plan, snapshot.AITeam, snapshot.TurnNumber);
    }

    // UnitSaveData guarda o badge, mas o plano e seus slots sao a fonte de verdade. No resume
    // Stage 2+ nao existe BuildObjectivePlan para executar os Passos 6/7 que normalmente
    // reaplicam HUD e removem rogues. Reconcilia sem redistribuir nenhuma unidade.
    private void ReconcileRestoredPlanAssignments(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        int turnNumber)
    {
        // Resume pula o BuildObjectivePlan, então re-marca a base inimiga aqui também — senão o 4º
        // eixo (invasão) não apareceria após o load para um objetivo restaurado como CaptureSector.
        MarkEnemyBaseObjectivesAsInvasion(plan, aiTeam);
        currentAxisMap = InvasionAxisMap.Build(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(aiTeam)));
        if (GetGoGreenInvasionForInspection(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)), turnNumber).Active)
            AssignUnslottedUnitsToGoGreenInvasion(plan, aiTeam);
        var slottedIds = new HashSet<int>();
        int restored = 0;
        int unavailable = 0;
        int duplicates = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;

            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot == null || !slot.Filled || slot.AssignedUnitId < 0)
                    continue;

                if (!slottedIds.Add(slot.AssignedUnitId))
                {
                    slot.Filled = false;
                    slot.AssignedUnitId = -1;
                    duplicates++;
                    continue;
                }

                plan.RogueUnitIds?.Remove(slot.AssignedUnitId);
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (unit == null)
                {
                    // Pode estar embarcada/inativa durante o load. Mantem o slot e o badge
                    // serializado; ele volta a ser resolvido quando a unidade reaparecer.
                    unavailable++;
                    continue;
                }

                ApplyPlanHUD(unit, obj, slot.Role);
                restored++;
            }
        }

        int cleared = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.SlotIndex != ResolveAISlotKey(aiTeam) || unit.IsDead || slottedIds.Contains(unit.InstanceId))
                continue;
            if (!unit.AIHasAssignedPlan)
                continue;

            unit.ClearAIAssignedPlan();
            cleared++;
        }

        Debug.Log($"[AI Plan][T{turnNumber}][{aiTeam}] resume assignments: " +
                  $"restaurados={restored} indisponiveis={unavailable} duplicados={duplicates} " +
                  $"badges obsoletos={cleared} rogues={plan.RogueUnitIds?.Count ?? 0}");
    }

    private static void EnsureRallyAssemblySlots(
        SectorObjective obj,
        int requiredCapturers,
        int requiredBreakthrough,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        if (obj == null)
            return;

        EnsureRallyRoleSlots(obj, UnitRole.Capturador, requiredCapturers, aiTeam, rallyAnchor);
        // D3 (autor, 2026-11-11): a vaga de ruptura e por FORCA. Qualquer rotulo de Ruptura
        // ja recrutado (Caca B como Interceptador, Apache como Disruptor) cobre a vaga; a
        // conta de prontidao e a vaga passam a falar a mesma lingua, e o Shopping deixa de
        // pedir blindado para um piso que o caca ja fechou.
        EnsureRallyRoleSlots(
            obj,
            UnitRole.Assalto,
            requiredBreakthrough,
            aiTeam,
            rallyAnchor,
            byForce: true);
        EnsureRallyArtillerySlots(obj, 0, aiTeam); // nao cria vagas obrigatorias de suporte.
    }

    private static void EnsureRallyArtillerySlots(SectorObjective obj, int requiredArtillery, TeamId aiTeam)
    {
        int totalSlots = 0;
        int filledSlots = 0;
        float assignedPower = 0f;
        for (int i = 0; i < obj.Slots.Count; i++)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != UnitRole.FogoIndireto)
                continue;
            totalSlots++;
            if (!slot.Filled)
                continue;
            filledSlots++;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit != null && unit.TryGetUnitData(out UnitData data))
                assignedPower += GetRallyArtilleryWeight(data);
        }

        int missingStandardPieces = Mathf.CeilToInt(
            Mathf.Max(0f, requiredArtillery - assignedPower));
        int desiredSlots = Mathf.Clamp(
            filledSlots + missingStandardPieces,
            Mathf.Min(filledSlots, requiredArtillery),
            RallyMaximumArtilleryUnits);

        // Nunca expulsa uma peça já recrutada só porque outra mais pesada elevou o poder.
        // Remove apenas vagas abertas excedentes.
        for (int i = obj.Slots.Count - 1; totalSlots > desiredSlots && i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != UnitRole.FogoIndireto || slot.Filled)
                continue;
            obj.Slots.RemoveAt(i);
            totalSlots--;
        }
        while (totalSlots < desiredSlots)
        {
            obj.Slots.Add(new SlotNeed { Role = UnitRole.FogoIndireto });
            totalSlots++;
        }
    }

    private static void RemoveOpenRallySlots(SectorObjective obj)
    {
        if (obj?.Slots == null)
            return;
        for (int i = obj.Slots.Count - 1; i >= 0; i--)
            if (!obj.Slots[i].Filled)
                obj.Slots.RemoveAt(i);
    }

    private static void EnsureRallyRoleSlots(
        SectorObjective obj,
        UnitRole role,
        int required,
        TeamId aiTeam,
        Vector3Int rallyAnchor,
        bool byForce = false)
    {
        // byForce: conta toda vaga cujo rotulo pertence a mesma Forca do pedido (vaga de
        // Assalto preenchida por Interceptador conta ruptura). O excedente removido continua
        // sendo so vaga ABERTA do proprio rotulo (FindRallyExcessSlotIndex).
        UnitForce force = byForce ? UnitRoleCompatibility.ResolveForce(role) : UnitForce.Nenhuma;
        int current = 0;
        for (int i = 0; i < obj.Slots.Count; i++)
        {
            UnitRole slotRole = obj.Slots[i].Role;
            if (slotRole == role || (byForce && force != UnitForce.Nenhuma && UnitRoleCompatibility.ResolveForce(slotRole) == force))
                current++;
        }

        while (current > required)
        {
            int removeIndex = FindRallyExcessSlotIndex(obj, role, aiTeam, rallyAnchor);
            if (removeIndex < 0)
                break;

            SlotNeed removed = obj.Slots[removeIndex];
            if (removed.Filled)
            {
                UnitManager released = FindActiveUnit(removed.AssignedUnitId, aiTeam);
                if (released != null)
                    released.ClearAIAssignedPlan();
                Debug.Log($"[AI Rally] {obj.AssignedTeam} {obj.Sector} libera excedente {role} " +
                          $"Unit{removed.AssignedUnitId}: slots={current}->{current - 1} alvo={required}");
            }

            obj.Slots.RemoveAt(removeIndex);
            current--;
        }

        for (int i = current; i < required; i++)
            obj.Slots.Add(new SlotNeed { Role = role });
    }

    private static int FindRallyExcessSlotIndex(
        SectorObjective obj,
        UnitRole role,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        int farthestFilledIndex = -1;
        float farthestDistance = float.MinValue;
        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != role)
                continue;
            if (!slot.Filled)
                return i;

            UnitManager assigned = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (assigned == null)
                return i;

            // No rally os requisitos de capturador/assalto sao piso, nao teto. Vagas abertas
            // excedentes somem, mas nenhuma unidade ja acolhida e expulsa da massa de invasao.
            if (IsRallyAssemblyObjective(obj)
                && (role == UnitRole.Capturador || role == UnitRole.Assalto)
                && assigned != null)
                continue;

            Vector3Int cell = assigned.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.HexDistance(cell, rallyAnchor);
            if (distance > farthestDistance)
            {
                farthestDistance = distance;
                farthestFilledIndex = i;
            }
        }

        return farthestFilledIndex;
    }

    private static void NormalizeStandardObjectiveFireSupportSlots(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return;

        // Plano de invasão da base inimiga (">>"): a captura final tem peças fortes de fogo indireto,
        // então o teto é BaseInvasionEliteFireSupportSlots em vez de 1 (setor comum segue 1).
        int maxFireSupport = (ConstructionSectorHelper.IsBase(obj.Sector) && FindHQTeamInSector(obj.Sector) != aiTeam)
            ? BaseInvasionEliteFireSupportSlots
            : 1;

        Vector3Int anchor = Vector3Int.zero;
        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            anchor = info.RepresentativeCell;
            anchor.z = 0;
        }

        int current = 0;
        for (int i = 0; i < obj.Slots.Count; i++)
            if (obj.Slots[i].Role == UnitRole.FogoIndireto)
                current++;

        while (current > maxFireSupport)
        {
            int removeIndex = FindRallyExcessSlotIndex(
                obj,
                UnitRole.FogoIndireto,
                aiTeam,
                anchor);
            if (removeIndex < 0)
                break;

            SlotNeed removed = obj.Slots[removeIndex];
            if (removed.Filled)
            {
                UnitManager released = FindActiveUnit(removed.AssignedUnitId, aiTeam);
                released?.ClearAIAssignedPlan();
                Debug.Log($"[AI Rally] {obj.AssignedTeam} {obj.Sector} perdeu montagem: " +
                          $"libera FireSupport Unit{removed.AssignedUnitId} slots={current}->{current - 1}");
            }

            obj.Slots.RemoveAt(removeIndex);
            current--;
        }
    }

    public static bool TryGetRallyHudState(
        int rallyOwnerSlotIndex,
        ConstructionSector sector,
        out AIRallyAssemblyState state,
        out string reason,
        out bool isMain)
    {
        state = AIRallyAssemblyState.None;
        reason = string.Empty;
        isMain = false;

        if (rallyOwnerSlotIndex < 0 || sector == ConstructionSector.None)
            return false;

        string key = BuildRallyHudKey(rallyOwnerSlotIndex, sector);
        if (!rallyHudStates.TryGetValue(key, out AIRallyHudSnapshot snapshot))
            return false;

        state = snapshot.State;
        reason = snapshot.Reason;
        isMain = snapshot.IsMain;
        return true;
    }

    private static void PublishRallyHudState(
        ConstructionManager rally,
        AIRallyAssemblyState state,
        string reason,
        int turnNumber,
        bool isMain,
        int ownerSlot)
    {
        // A luz e guardada por SLOT, e quem escreve e quem esta planejando. Num rally dos
        // dois lados isso da duas leituras guardadas para o mesmo predio, e a lampada
        // acende a de quem CONTROLA o predio (ConstructionManager.ResolveRallyHudState):
        // para o lado que nao o controla, a cor dele nao diz nada. Na captura, a lampada
        // passa a ler a leitura do novo dono sozinha.
        if (rally == null || !rally.IsRallyPoint || ownerSlot < 0
            || rally.Sector == ConstructionSector.None)
            return;

        string key = BuildRallyHudKey(ownerSlot, rally.Sector);
        bool changed = !rallyHudStates.TryGetValue(key, out AIRallyHudSnapshot previous)
            || previous.State != state
            || previous.Reason != reason
            || previous.IsMain != isMain;

        rallyHudStates[key] = new AIRallyHudSnapshot
        {
            State = state,
            Reason = reason,
            TurnNumber = turnNumber,
            IsMain = isMain
        };

        if (changed)
            ConstructionManager.RefreshRallyHudVisuals(rally.Sector, ownerSlot);
    }

    private static string BuildRallyHudKey(int rallyOwnerSlotIndex, ConstructionSector sector)
    {
        return $"{rallyOwnerSlotIndex}:{sector}";
    }

    private static void RememberRallyGoGreen(TeamId aiTeam, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return;
        rallyGoGreenTurns[$"{ResolveAISlotKey(aiTeam)}:{sector}"] = turnNumber;
    }

    private static bool IsRallyGoGreenSuppressed(PlayerSlotId aiSlot, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return false;
        string key = $"{aiSlot.Value}:{sector}";
        return rallyGoGreenTurns.TryGetValue(key, out int goTurn)
            && turnNumber - goTurn <= RallyGoGreenSuppressTurns;
    }

    // Inspeção (HUD/Shopping Pressure): há uma invasão GoGreen em andamento para o time? Após o
    // GoGreen, o objetivo RallyAssembly é REMOVIDO do plano (a massa volta ao plano ofensivo e
    // marcha para a base inimiga), então o painel não vê mais nenhum rally — mas a operação segue.
    // Considera "em andamento" enquanto o GoGreen estiver dentro da janela de supressão.
    public struct GoGreenInvasionInspection
    {
        public bool Active;
        public List<ConstructionSector> Sectors;  // setores que viraram GoGreen na operação
        public int SinceTurn;                      // turno do GoGreen mais antigo ainda ativo
    }

    public static GoGreenInvasionInspection GetGoGreenInvasionForInspection(PlayerSlotId aiSlot, int turnNumber)
    {
        var result = new GoGreenInvasionInspection
        {
            Sectors = new List<ConstructionSector>(),
            SinceTurn = -1
        };
        if (turnNumber < 0)
            return result;

        string prefix = $"{aiSlot.Value}:";
        foreach (KeyValuePair<string, int> entry in rallyGoGreenTurns)
        {
            if (!entry.Key.StartsWith(prefix, System.StringComparison.Ordinal))
                continue;
            if (turnNumber - entry.Value > RallyGoGreenSuppressTurns)
                continue;

            string sectorName = entry.Key.Substring(prefix.Length);
            if (!System.Enum.TryParse(sectorName, out ConstructionSector sector)
                || sector == ConstructionSector.None)
                continue;

            result.Sectors.Add(sector);
            if (result.SinceTurn < 0 || entry.Value < result.SinceTurn)
                result.SinceTurn = entry.Value;
        }

        result.Active = result.Sectors.Count > 0;
        return result;
    }

    // --- Persistência do estado GoGreen/Invasão (save/load) -------------------------------------
    // rallyGoGreenTurns é estático e não vive em nenhum SectorObjective depois que o objetivo de
    // rally é removido no GoGreen. Os helpers abaixo deixam o ObjectiveManager exportar/importar
    // essas entradas junto do plano para que a invasão "em andamento" sobreviva ao save/load.

    public static void CollectGoGreenTurnsForSlot(
        PlayerSlotId aiSlot,
        List<ConstructionSector> sectorsOut,
        List<int> turnsOut)
    {
        if (sectorsOut == null || turnsOut == null)
            return;

        string prefix = $"{aiSlot.Value}:";
        foreach (KeyValuePair<string, int> entry in rallyGoGreenTurns)
        {
            if (!entry.Key.StartsWith(prefix, System.StringComparison.Ordinal))
                continue;
            string sectorName = entry.Key.Substring(prefix.Length);
            if (!System.Enum.TryParse(sectorName, out ConstructionSector sector)
                || sector == ConstructionSector.None)
                continue;
            sectorsOut.Add(sector);
            turnsOut.Add(entry.Value);
        }
    }

    public static void ClearGoGreenTurnsForSlot(PlayerSlotId aiSlot)
    {
        string prefix = $"{aiSlot.Value}:";
        var toRemove = new List<string>();
        foreach (string key in rallyGoGreenTurns.Keys)
            if (key.StartsWith(prefix, System.StringComparison.Ordinal))
                toRemove.Add(key);
        foreach (string key in toRemove)
            rallyGoGreenTurns.Remove(key);
    }

    public static void RestoreGoGreenTurn(PlayerSlotId aiSlot, ConstructionSector sector, int turnNumber)
    {
        if (!aiSlot.IsValid || sector == ConstructionSector.None || turnNumber < 0)
            return;
        rallyGoGreenTurns[$"{aiSlot.Value}:{sector}"] = turnNumber;
    }

    // Acesso público pro semáforo (ConstructionManager): enquanto a operação está em voo (supressa),
    // o ponto de rally mostra verde — a luz segue a OPERAÇÃO, não a massa parada no ancoradouro.
    public static bool IsRallyGoGreenSuppressedForHud(PlayerSlotId aiSlot, ConstructionSector sector, int turnNumber)
    {
        return IsRallyGoGreenSuppressed(aiSlot, sector, turnNumber);
    }

    public static void GetInvasionMonitorForSave(PlayerSlotId aiSlot, out int bestDistance, out int stallCounter)
    {
        if (invasionMonitors.TryGetValue(aiSlot.Value, out InvasionMonitor m))
        {
            bestDistance = m.BestDistance == int.MaxValue ? -1 : m.BestDistance;
            stallCounter = m.StallCounter;
        }
        else
        {
            bestDistance = -1;
            stallCounter = 0;
        }
    }

    public static void RestoreInvasionMonitor(PlayerSlotId aiSlot, int bestDistance, int stallCounter)
    {
        if (bestDistance < 0 && stallCounter <= 0)
        {
            invasionMonitors.Remove(aiSlot.Value);
            return;
        }
        invasionMonitors[aiSlot.Value] = new InvasionMonitor
        {
            BestDistance = bestDistance < 0 ? int.MaxValue : bestDistance,
            StallCounter = Mathf.Max(0, stallCounter)
        };
    }

    // --- Monitor de desfecho da invasão (re-montagem da 2ª onda) --------------------------------
    // Roda no topo do BuildObjectivePlan, sobre o plano do turno anterior (a onda em voo). Detecta
    // fracasso por OR(colapso, estagnação) e, quando falha, LIMPA o registro GoGreen do time — o
    // que solta a supressão e faz os rallies reabrirem montagem (2ª onda pelo portão normal). A
    // frente sobrevivente NÃO recua: continua nos slots do objetivo de base.
    private void UpdateInvasionMonitor(TeamObjectivePlan plan, TeamId aiTeam, AIWorldSnapshot snapshot)
    {
        if (plan == null || snapshot == null)
            return;

        int turn = snapshot.TurnNumber;
        bool rallyWaiting = UpdateRallyAdvanceWaiting(plan, aiTeam);

        // Operação em voo? (algum rally GoGreen registrado). Sem isso, não há o que monitorar.
        var ggSectors = new List<ConstructionSector>();
        var ggTurns = new List<int>();
        CollectGoGreenTurnsForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex), ggSectors, ggTurns);
        if (ggTurns.Count == 0)
        {
            invasionMonitors.Remove(ResolveAISlotKey(aiTeam));
            return;
        }
        int earliestGoGreen = int.MaxValue;
        foreach (int t in ggTurns)
            if (t < earliestGoGreen) earliestGoGreen = t;

        // Força de invasão = unidades vivas atribuídas aos objetivos de base inimiga.
        SectorObjective baseObj = null;
        var force = new List<UnitManager>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || !ConstructionSectorHelper.IsBase(obj.Sector)) continue;
            if (FindHQTeamInSector(obj.Sector) == aiTeam) continue;
            baseObj = obj;
            if (obj.Slots == null) continue;
            foreach (SlotNeed slot in obj.Slots)
            {
                if (slot == null || !slot.Filled) continue;
                UnitManager u = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (u != null && !u.IsDead) force.Add(u);
            }
        }

        // A onda liberada inclui seus participantes, mesmo quando viraram rogue no novo plano.
        var forceIds = new HashSet<int>();
        foreach (UnitManager unit in force) forceIds.Add(unit.InstanceId);
        foreach (SectorObjective group in plan.RallyAdvances)
            foreach (int id in group.RallyParticipantIds)
            {
                UnitManager unit = FindActiveUnit(id, aiTeam);
                if (unit != null && !unit.IsDead && forceIds.Add(id)) force.Add(unit);
            }

        // Sem objetivo de base = operação encerrada (sucesso/dissolvida): limpa silenciosamente.
        if (baseObj == null)
        {
            ClearGoGreenTurnsForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)));
            invasionMonitors.Remove(ResolveAISlotKey(aiTeam));
            plan.RallyAdvances.Clear();
            return;
        }

        Vector3Int target = ResolveInvasionTargetCell(baseObj, snapshot, aiTeam);
        int cur = int.MaxValue;
        int liveAssault = 0;
        bool inContact = false;
        foreach (UnitManager u in force)
        {
            if (!u.IsEmbarked && u.TryGetUnitData(out UnitData d) && d != null
                && UnitRoleCompatibility.ResolveForce(d) == UnitForce.Ruptura)
                liveAssault++;

            Vector3Int c = u.CurrentCellPosition; c.z = 0;
            int dist = SectorManager.TryGetLandMovementDistance(c, target, out int td)
                ? td
                : Mathf.RoundToInt(SectorManager.HexDistance(c, target));
            if (dist < cur) cur = dist;
            if (!inContact) inContact = IsAdjacentToVisibleEnemy(c, snapshot);
        }

        int aiSlotKey = ResolveAISlotKey(aiTeam);
        InvasionMonitor mon = invasionMonitors.TryGetValue(aiSlotKey, out InvasionMonitor prev)
            ? prev
            : new InvasionMonitor { BestDistance = int.MaxValue, StallCounter = 0 };

        if (cur != int.MaxValue)
        {
            bool atGate = cur <= InvasionAssaultGateRange;
            if (cur < mon.BestDistance)
            {
                mon.BestDistance = cur;     // progrediu -> zera estagnação
                mon.StallCounter = 0;
            }
            else if (!atGate && !inContact && !rallyWaiting)
            {
                mon.StallCounter++;         // parado e fora de hold legítimo
            }
            // atGate (assaltando) ou inContact (lutando): hold legítimo, não conta estagnação.
        }

        if (rallyWaiting) mon.StallCounter = 0;
        bool graceOver = turn - earliestGoGreen >= InvasionGraceTurns;
        bool collapse = graceOver && liveAssault == 0;
        bool stall = graceOver && mon.StallCounter >= InvasionStallTurns;

        if (collapse || stall)
        {
            Debug.Log($"{TL("Plan")} invasão FRACASSOU (" +
                (collapse ? "colapso: 0 assalto vivo na força" : $"estagnação: {mon.StallCounter}T sem progresso, best={mon.BestDistance}h") +
                ") -> libera re-montagem (2ª onda); frente mantém posição");
            ReleaseGoGreenFallbackAssignments(plan, aiTeam);
            ClearGoGreenTurnsForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(aiTeam)));   // solta supressão -> rallies reabrem montagem
            invasionMonitors.Remove(aiSlotKey);
            plan.RallyAdvances.Clear();
            // Zera o marcador de GoGreen dos objetivos pra que a 2ª onda registre um GoGreen LIMPO
            // (re-estabelecendo a supressão) em vez de ser bloqueada pelo marcador da onda falha.
            foreach (SectorObjective obj in plan.Objectives)
                if (obj != null && obj.RallyGoGreenTurn >= 0)
                    obj.RallyGoGreenTurn = -1;
            return;
        }

        invasionMonitors[aiSlotKey] = mon;
    }

    private Vector3Int ResolveInvasionTargetCell(SectorObjective baseObj, AIWorldSnapshot snapshot, TeamId aiTeam)
    {
        ConstructionManager tgt = FindCapturableInSector(baseObj.Sector, aiTeam);
        if (tgt != null)
        {
            Vector3Int c = tgt.CurrentCellPosition; c.z = 0;
            return c;
        }
        if (snapshot.EnemyHQ != null)
        {
            Vector3Int hq = snapshot.EnemyHQ.CurrentCellPosition; hq.z = 0;
            return hq;
        }
        return Vector3Int.zero;
    }

    private static bool IsAdjacentToVisibleEnemy(Vector3Int cell, AIWorldSnapshot snapshot)
    {
        if (snapshot?.EnemyUnits == null)
            return false;
        foreach (UnitManager e in snapshot.EnemyUnits)
        {
            if (e == null || e.IsDead) continue;
            Vector3Int ec = e.CurrentCellPosition; ec.z = 0;
            if (SectorManager.HexDistance(cell, ec) <= 1f)
                return true;
        }
        return false;
    }

    private static bool IsRallySectorHeldByTeam(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;
        if (info.ControllingTeam == aiTeam)
            return true;
        // Se um time INIMIGO (nao-neutro, nao a AI) controla o setor, o rally NAO esta held pela
        // AI — mesmo que a AI possua a maioria das construcoes laterais. Controlar o setor/rally
        // e do inimigo; a AI precisa RECUPERAR o controle pra "conquistar". (Antes: o fallback de
        // maioria abaixo dizia "conquistado" com o inimigo ainda no controle — ma leitura.)
        if (info.ControllingTeam != TeamId.Neutral && info.ControllingTeam != aiTeam)
            return false;

        int owned = 0;
        int total = 0;
        IReadOnlyList<SectorManager.SectorConstructionInfo> constructions = info.Constructions;
        if (constructions == null)
            return false;

        for (int i = 0; i < constructions.Count; i++)
        {
            SectorManager.SectorConstructionInfo construction = constructions[i];
            if (construction == null || construction.CapturePointsMax <= 0)
                continue;

            total++;
            if (construction.OwnerTeam == aiTeam)
                owned++;
        }

        return total > 0 && owned >= (total / 2) + 1;
    }

    private static bool IsRallyPrimaryAssaultUnit(UnitData data)
        => data != null && UnitRoleCompatibility.ResolveForce(data) == UnitForce.Ruptura;

    private static bool IsRallyUnitOperational(UnitManager unit)
        => unit != null && !unit.IsDead && !unit.IsUnderRepair
            && (unit.MaxAmmo <= 0 || unit.CurrentAmmo > 0)
            && (unit.MaxFuel <= 0 || unit.CurrentFuel > 0);



    private static float GetRallyArtilleryWeight(UnitData data)
    {
        if (data == null || UnitRoleCompatibility.ResolveForce(data) != UnitForce.Dissuasao) return 0f;
        string key = NormalizeRallyUnitKey($"{data.id} {data.displayName} {data.apelido}");
        if (key.Contains("obus") && key.Contains("leve")) return 0.5f;
        if (key.Contains("art") && key.Contains("campanha")) return 1.5f;
        return 1f;
    }

    private SectorObjective FindPrimaryRallyObjective(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan?.Objectives == null)
            return null;
        ConstructionSector focusSector = BuildRallyAggregate(aiTeam).FocusSector;
        foreach (SectorObjective obj in plan.Objectives)
            if (IsActiveRallyAssemblyObjective(obj) && obj.Sector == focusSector)
                return obj;
        return null;
    }

    private void ConsolidateRallyAssemblyAssignments(TeamObjectivePlan plan, TeamId aiTeam)
    {
        SectorObjective focus = FindPrimaryRallyObjective(plan, aiTeam);
        if (focus == null)
            return;

        var focusIds = new HashSet<int>();
        foreach (SlotNeed slot in focus.Slots)
            if (slot.Filled)
                focusIds.Add(slot.AssignedUnitId);

        int moved = 0;
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj == focus || !IsRallyAssemblyObjective(obj) || obj.Slots == null)
                continue;
            for (int i = obj.Slots.Count - 1; i >= 0; i--)
            {
                SlotNeed slot = obj.Slots[i];
                obj.Slots.RemoveAt(i);
                if (slot == null || !slot.Filled || !focusIds.Add(slot.AssignedUnitId))
                    continue;
                focus.Slots.Add(slot);
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (unit != null)
                    ApplyPlanHUD(unit, focus, slot.Role);
                moved++;
            }
        }

        if (moved > 0)
            Debug.Log($"{TL("Plan")} Rally operation concentra {moved} unidades em {focus.Sector}");
    }

    // Multi-centro open-bar: cada unidade livre é atraída pelo rally held MAIS PRÓXIMO (montagem
    // local — não atravessa o mapa pra um foco). Cada centro forma seu próprio arco.
    private void RecruitFreeUnitsForRally(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        HashSet<int> assignedIds)
    {
        if (plan?.Objectives == null || assignedIds == null)
            return;

        // Centros = rallies held ativos (não GoGreen/Expired) com âncora resolvida.
        var centerObjs = new List<SectorObjective>();
        var centerAnchors = new List<Vector3Int>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsActiveRallyAssemblyObjective(obj))
                continue;
            if (!TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager rally))
                continue;
            Vector3Int a = rally.CurrentCellPosition; a.z = 0;
            centerObjs.Add(obj);
            centerAnchors.Add(a);
        }
        if (centerObjs.Count == 0)
            return;

        int recruited = 0;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.SlotIndex != ResolveAISlotKey(aiTeam) || unit.IsDead || unit.IsUnderRepair
                || assignedIds.Contains(unit.InstanceId)
                || !unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
            int nearest = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < centerObjs.Count; i++)
            {
                float d = SectorManager.HexDistance(cell, centerAnchors[i]);
                if (d < bestDist) { bestDist = d; nearest = i; }
            }
            if (nearest < 0)
                continue;

            SectorObjective target = centerObjs[nearest];
            UnitRole role = UnitRoleCompatibility.ResolveCompositionRole(data);
            if (role == UnitRole.None)
                role = data.roles != null && data.roles.Count > 0 ? data.roles[0] : UnitRole.Capturador;
            target.Slots.Add(new SlotNeed { Role = role, Filled = true, AssignedUnitId = unit.InstanceId });
            assignedIds.Add(unit.InstanceId);
            plan.RogueUnitIds?.Remove(unit.InstanceId);
            ApplyPlanHUD(unit, target, role);
            recruited++;
        }

        // APC mínimo por centro (infantaria distante daquele centro).
        for (int i = 0; i < centerObjs.Count; i++)
        {
            int distantInfantry = CountDistantRallyInfantry(centerObjs[i], aiTeam, centerAnchors[i]);
            int desiredApcs = Mathf.Max(
                Mathf.CeilToInt(distantInfantry / (float)RallyUsefulApcCapacity),
                CountLoadedRallyTransporters(centerObjs[i], aiTeam));
            EnsureRallyTransportMinimumSlots(centerObjs[i], desiredApcs);
        }

        Debug.Log($"{TL("Plan")} Rally multi-centro open-bar: centros={centerObjs.Count} recrutadas={recruited}");
    }

    private static void EnsureRallyTransportMinimumSlots(SectorObjective focus, int required)
    {
        if (focus?.Slots == null)
            return;
        int current = 0;
        foreach (SlotNeed slot in focus.Slots)
            if (slot != null && slot.Role == UnitRole.Transportador)
                current++;
        while (current < required)
        {
            focus.Slots.Add(new SlotNeed { Role = UnitRole.Transportador });
            current++;
        }
    }

    private static int CountDistantRallyInfantry(
        SectorObjective focus,
        TeamId aiTeam,
        Vector3Int anchor)
    {
        int count = 0;
        if (focus?.Slots == null)
            return count;
        foreach (SlotNeed slot in focus.Slots)
        {
            if (slot == null || !slot.Filled || slot.Role == UnitRole.Transportador)
                continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit == null || unit.IsEmbarked || !unit.TryGetUnitData(out UnitData data)
                || data == null || data.unitClass != GameUnitClass.Infantry)
                continue;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (SectorManager.HexDistance(cell, anchor) >= RallyInfantryTransportDistance)
                count++;
        }
        return count;
    }

    private static int CountLoadedRallyTransporters(SectorObjective focus, TeamId aiTeam)
    {
        int count = 0;
        if (focus?.Slots == null)
            return count;
        foreach (SlotNeed slot in focus.Slots)
        {
            if (slot == null || slot.Role != UnitRole.Transportador || !slot.Filled)
                continue;
            UnitManager transporter = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (transporter?.TransportedUnitSlots == null)
                continue;
            bool loaded = false;
            foreach (UnitTransportSeatRuntime seat in transporter.TransportedUnitSlots)
                if (seat != null && seat.embarkedUnit != null && seat.embarkedUnit.IsEmbarked)
                {
                    loaded = true;
                    break;
                }
            if (loaded)
                count++;
        }
        return count;
    }

    private static bool IsGroundTransportPassengerSlot(
        SectorObjective obj,
        SlotNeed slot,
        TeamId aiTeam)
    {
        if (slot == null || !slot.Filled)
            return false;
        if (slot.Role == UnitRole.Capturador)
            return true;
        if (!IsRallyAssemblyObjective(obj))
            return false;
        UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.unitClass == GameUnitClass.Infantry;
    }

    private void RecruitNearbyRogueArtilleryForRally(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        HashSet<int> assignedIds)
    {
        if (plan?.Objectives == null || assignedIds == null)
            return;

        SectorObjective focus = FindPrimaryRallyObjective(plan, aiTeam);
        ConstructionManager focusRally = null;
        if (focus != null)
            TryFindOwnedRallyForSector(focus.Sector, aiTeam, out focusRally);
        if (focus == null || focusRally == null)
            return;

        Vector3Int anchor = focusRally.CurrentCellPosition;
        anchor.z = 0;
        float assignedPower = GetAssignedRallyArtilleryPower(focus, aiTeam);
        int requiredPower = RallyMaximumArtilleryUnits; // teto de recrutamento opcional, nunca gate de prontidao.
        if (assignedPower >= requiredPower)
            return;

        var candidates = new List<UnitManager>();
        foreach (UnitManager unit in GetAvailablePrimaryFireSupports(aiTeam))
        {
            if (unit == null || assignedIds.Contains(unit.InstanceId)
                || !unit.TryGetUnitData(out UnitData data)
                || GetRallyArtilleryWeight(data) <= 0f)
                continue;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.TryGetLandMovementDistance(cell, anchor, out int terrain)
                ? terrain : SectorManager.HexDistance(cell, anchor);
            if (distance <= RallyArtilleryRecruitRange)
                candidates.Add(unit);
        }

        candidates.Sort((a, b) =>
        {
            Vector3Int ac = a.CurrentCellPosition; ac.z = 0;
            Vector3Int bc = b.CurrentCellPosition; bc.z = 0;
            float ad = SectorManager.TryGetLandMovementDistance(ac, anchor, out int at)
                ? at : SectorManager.HexDistance(ac, anchor);
            float bd = SectorManager.TryGetLandMovementDistance(bc, anchor, out int bt)
                ? bt : SectorManager.HexDistance(bc, anchor);
            int distance = ad.CompareTo(bd);
            if (distance != 0) return distance;
            a.TryGetUnitData(out UnitData aData);
            b.TryGetUnitData(out UnitData bData);
            return GetRallyArtilleryWeight(bData).CompareTo(GetRallyArtilleryWeight(aData));
        });

        foreach (UnitManager unit in candidates)
        {
            if (assignedPower >= requiredPower)
                break;
            if (!focus.HasOpenSlot(UnitRole.FogoIndireto))
            {
                int fireSlots = 0;
                foreach (SlotNeed slot in focus.Slots)
                    if (slot.Role == UnitRole.FogoIndireto) fireSlots++;
                if (fireSlots >= RallyMaximumArtilleryUnits)
                    break;
                focus.Slots.Add(new SlotNeed { Role = UnitRole.FogoIndireto });
            }
            if (!focus.TryFillSlot(UnitRole.FogoIndireto, unit.InstanceId))
                continue;
            unit.TryGetUnitData(out UnitData data);
            float weight = GetRallyArtilleryWeight(data);
            assignedPower += weight;
            assignedIds.Add(unit.InstanceId);
            ApplyPlanHUD(unit, focus, UnitRole.FogoIndireto);
            Vector3Int cell = unit.CurrentCellPosition; cell.z = 0;
            Debug.Log($"{TL("Plan")} Rally {focus.Sector} pesca FireSupport rogue "
                + $"{unit.InstanceId} pesoArt={weight:0.#} "
                + $"dist={SectorManager.HexDistance(cell, anchor)}h "
                + $"poderAtribuido={assignedPower:0.#}/{requiredPower}");
        }
    }

    private static float GetAssignedRallyArtilleryPower(SectorObjective obj, TeamId aiTeam)
    {
        float power = 0f;
        if (obj?.Slots == null)
            return power;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role != UnitRole.FogoIndireto || !slot.Filled)
                continue;
            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit != null && unit.TryGetUnitData(out UnitData data))
                power += GetRallyArtilleryWeight(data);
        }
        return power;
    }

    private static bool HasRole(UnitData data, UnitRole role)
    {
        return data != null && data.roles != null && data.roles.Contains(role);
    }

    // Portadora da skill "Captura Construcoes" (id capturaConstrucoes). Marcador exclusivo
    // da CONTAGEM de capturador do rally/GoGreen — desacopla essa cota de papel/classe e
    // deixa qualquer futura unidade portadora contar. Nao afeta o gate real de captura.
    private const string RallyCaptureSkillId = "capturaConstrucoes";
    private static bool HasRallyCaptureSkill(UnitData data)
    {
        if (data == null || data.skills == null)
            return false;
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill != null && string.Equals(skill.id, RallyCaptureSkillId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Peso de RUPTURA de uma unidade de assalto (0 se nao for assalto primario). Como a
    // artilharia, ruptura conta por PESO, nao por cabeca: um MBT (blindado pesado) rompe por
    // mais de um tankinho. Base pela armadura + refino por elite.
    //   Tanque Leve (Medium, elite 0) = 1.0   |  MBT (Heavy, elite 1) = 2.5  |  Pesado (Heavy, elite 2) = 3.0
    private static float GetRallyAssaultWeight(UnitData data)
    {
        if (!IsRallyPrimaryAssaultUnit(data)) return 0f;
        if (data.IsAircraft() || data.unitClass != GameUnitClass.Armored) return 1f;
        return (data.ArmorClass >= ArmorClass.Heavy ? 2f : 1f) + 0.5f * Mathf.Clamp(data.eliteLevel, 0, 2);
    }

    private static string NormalizeRallyUnitKey(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string lower = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(lower.Length);
        for (int i = 0; i < lower.Length; i++)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(lower[i]);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(lower[i]);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }



    private static bool IsRallyAssemblyObjective(SectorObjective obj)
    {
        return obj != null && obj.ObjectiveType == AIObjectiveType.RallyAssembly;
    }

    private static bool IsActiveRallyAssemblyObjective(SectorObjective obj)
    {
        return IsRallyAssemblyObjective(obj)
            && obj.RallyState != AIRallyAssemblyState.GoGreen
            && obj.RallyState != AIRallyAssemblyState.Expired;
    }

    private static bool IsRallyGoGreenObjective(SectorObjective obj)
    {
        return IsRallyAssemblyObjective(obj)
            && obj.RallyState == AIRallyAssemblyState.GoGreen;
    }

    // TRUE se algum rally do time que mira o QG inimigo esta held E ja juntou massa (GoGreen,
    // por presenca). E a chave que destrava a invasao da base inimiga (">>"). Checagem momentanea:
    // o objetivo da base, uma vez criado, persiste (tem capturavel) — entao nao ha flip.
    private bool AnyOwnedRallyAtGoGreen(TeamId aiTeam, AIRallyPlanContext rallyContext, int turnNumber, AIIntelReport intel)
    {
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint) continue;
            if (!IsRallyOwnedBySlot(rally, aiTeam, rallyContext.AISlotIndex)) continue;
            if (!IsEnemyHQRallySectorHeld(rallyContext, rally.Sector, aiTeam)) continue;
            AIRallyReadiness r = EvaluateRallyReadiness(rally, aiTeam, turnNumber, intel);
            if (r.GoGreen) return true;
        }
        return false;
    }

    // TRUE se a AI GOVERNA (held) algum rally que mira o QG inimigo. Enquanto governar um rally,
    // a invasao (base inimiga ">>") persiste; se nao governa nenhum, a invasao e dissolvida e as
    // unidades sao liberadas para outros planos.
    private bool AnyOwnedRallyHeld(TeamId aiTeam, AIRallyPlanContext rallyContext)
    {
        if (rallyContext.TargetingEnemyHQ == null) return false;
        foreach (ConstructionSector sector in rallyContext.TargetingEnemyHQ)
            if (IsEnemyHQRallySectorHeld(rallyContext, sector, aiTeam)) return true;
        return false;
    }

    // TRUE se o mapa TEM algum rally DESIGNADO para este SLOT (podendo ainda nao estar conquistado).
    //
    // O gate de invasao (so invade a base inimiga com um rally governado/GoGreen) colapsa duas
    // situacoes diferentes num mesmo "false":
    //   (a) "existe rally meu, mas ainda nao conquistei" -> gate LEGITIMO: bloqueia e espera
    //   (b) "nao ha rally meu neste mapa"                -> gate INAPLICAVEL: bloquearia pra sempre
    // Em (b) o planner ficava criando e dissolvendo a invasao todo turno e o plano nunca saia do
    // zero — num mapa so de QGs (tabuleiro de teste) a AI nunca atacava pelo plano, so pelo
    // fallback generico do HexEvaluator.
    //
    // Escopo por SLOT, nao global: rally e DESIGNADO por slot via RallyOwnerSlotIndex (a
    // "intencao de dono quando conquistado", ver IsRallyOwnedBySlot). Um mapa pode ter rally do
    // slot 0 e nenhum do slot 1 — um teste global devolveria "gate aplicavel" pro slot 1 e
    // recriaria o bug exatamente para ele.
    //
    // Usa RallyOwnerSlotIndex (designacao), NAO SlotIndex (posse atual): rally meu ainda por
    // conquistar mantem o gate valendo, e a AI vai captura-lo antes de invadir — filtrar por posse
    // abriria a invasao no turno 1 de qualquer mapa, o gotejamento suicida que o gate impede.
    //
    // RallyOwnerSlotIndex < 0 = sem dono explicito (InvasionAxisMap.FindAxisHQ resolve pelo HQ mais
    // proximo), logo PODE ser meu: conta como aplicavel. Na duvida preserva o comportamento atual.
    //
    // Mesmo padrao que IsOffAxisCaptureGatedByRally ja usa (RallyPointCount <= 0 => nao trava)
    // e que GetEffectiveTransportThresholdForSlot usa no transporte (limiar adapta em mapa curto).
    // Slot nao resolvido (aiSlotIndex < 0) cai no lado CONSERVADOR: qualquer rally do mapa conta,
    // preservando o gate como esta hoje. Falhar "aberto" aqui liberaria invasao por acidente de
    // resolucao de slot — o oposto do que o gate existe para impedir.
    private static bool MapHasAnyRallyPointForSlot(int aiSlotIndex)
    {
        IReadOnlyList<ConstructionManager> all = ConstructionManager.AllActive;
        if (all == null) return false;
        bool slotUnknown = aiSlotIndex < 0;
        for (int i = 0; i < all.Count; i++)
        {
            ConstructionManager c = all[i];
            if (c == null || !c.IsRallyPoint) continue;
            if (slotUnknown || c.RallyOwnerSlots.Count == 0 || c.IsRallyForSlot(aiSlotIndex))
                return true;
        }
        return false;
    }

    private static bool TryFindOwnedRallyForSector(
        ConstructionSector sector,
        TeamId aiTeam,
        out ConstructionManager bestRally)
    {
        bestRally = null;
        if (sector == ConstructionSector.None)
            return false;

        int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.Sector != sector)
                continue;
            if (!IsRallyOwnedBySlot(rally, aiTeam, slot))
                continue;

            bestRally = rally;
            return true;
        }

        return false;
    }

    private static bool TryResolveRallyInfluence(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        Vector3Int fromCell,
        bool includeGoGreen,
        out AIRallyInfluence influence)
    {
        influence = new AIRallyInfluence
        {
            Active = false,
            Anchor = fromCell,
            State = AIRallyAssemblyState.None,
            Reason = "sem rally"
        };

        if (plan == null || plan.Objectives == null)
            return false;

        SectorObjective bestObj = null;
        ConstructionManager bestRally = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];
            if (!IsRallyAssemblyObjective(obj))
                continue;
            if (!includeGoGreen && !IsActiveRallyAssemblyObjective(obj))
                continue;
            if (obj.RallyState == AIRallyAssemblyState.Expired)
                continue;
            if (!TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager rally))
                continue;

            Vector3Int rallyCell = rally.CurrentCellPosition;
            rallyCell.z = 0;
            float score = Mathf.Max(0f, 20f - obj.Priority) * 100f
                - SectorManager.HexDistance(fromCell, rallyCell) * 25f
                + (obj.RallyState == AIRallyAssemblyState.GoGreen ? 150f : 350f);
            if (score <= bestScore)
                continue;

            bestScore = score;
            bestObj = obj;
            bestRally = rally;
        }

        if (bestObj == null || bestRally == null)
            return false;

        Vector3Int anchor = bestRally.CurrentCellPosition;
        anchor.z = 0;
        AIRallyReadiness readiness = EvaluateRallyReadiness(
            bestRally,
            aiTeam,
            -1,
            null);

        influence = new AIRallyInfluence
        {
            Active = true,
            Sector = bestObj.Sector,
            Anchor = anchor,
            State = bestObj.RallyState,
            Readiness = readiness,
            Reason = bestObj.RallyReadinessReason
        };
        return true;
    }

    private static bool IsRallyAssemblingState(AIRallyAssemblyState state)
    {
        return state == AIRallyAssemblyState.WaitHold
            || state == AIRallyAssemblyState.Assembling
            || state == AIRallyAssemblyState.Ready;
    }

    private static Vector3Int ResolveRallyAssemblyAnchor(SectorObjective obj, TeamId aiTeam, Vector3Int fallback)
    {
        if (obj == null)
            return fallback;

        if (obj.RallyCaptainId >= 0)
        {
            UnitManager captain = FindActiveUnit(obj.RallyCaptainId, aiTeam);
            if (captain != null && !captain.IsDead && !captain.IsEmbarked) return captain.CurrentCellPosition;
        }
        if (TryFindOwnedRallyForSector(obj.Sector, aiTeam, out ConstructionManager bestRally))
        {
            Vector3Int rallyCell = bestRally.CurrentCellPosition;
            rallyCell.z = 0;
            return rallyCell;
        }

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int repCell = info.RepresentativeCell;
            repCell.z = 0;
            return repCell;
        }

        return fallback;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRallyDecisionFacts()
    {
        rallyReadinessFacts.Clear(); rallyReachFacts.Clear(); rallyReachRevision = -1;
        rallyAreaFacts.Clear(); rallyAreaRevision = -1;
    }

    private static readonly Dictionary<(int rally, int slot, int turn), AIRallyReadiness> rallyReadinessFacts = new Dictionary<(int, int, int), AIRallyReadiness>();
    private static int rallyReachRevision = -1;
    private static readonly Dictionary<(int unit, Vector3Int origin, int revision, int budget, ReachBand band), UnitReachEnvelope> rallyReachFacts = new Dictionary<(int, Vector3Int, int, int, ReachBand), UnitReachEnvelope>();

    private static AIController GetRallyContext(int slot, UnityEngine.Tilemaps.Tilemap map = null)
    {
        if (Instance != null && Instance.matchController != null && (map == null || Instance.boardTilemap == map)) return Instance;
        foreach (AIController ai in FindObjectsByType<AIController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if ((map == null || ai.boardTilemap == map) && ai.matchController != null)
                return ai;
        return null;
    }

    private static SectorObjective FindRallyObjective(int slot, ConstructionSector sector)
        => ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(slot))?.Objectives.Find(o => o != null && o.Sector == sector);

    // Consulta de presenca: banda propria, orcamento potencial e terreno, sem mover unidades.
    private static UnitReachEnvelope GetRallyReach(UnitManager unit, ReachBand band, Vector3Int? origin = null)
    {
        if (unit == null) return null;
        if (unit.IsEmbarked) unit = unit.EmbarkedTransporter;
        if (unit == null || !unit.TryGetUnitData(out UnitData data)) return null;
        Vector3Int from = origin ?? unit.CurrentCellPosition; from.z = 0;
        int budget = band == ReachBand.Tactical ? unit.MaxMovementPoints : 0; // Operacional encadeia turnos no servico.
        int revision = ThreatRevisionTracker.GlobalBoardRevision;
        if (rallyReachRevision != revision) { rallyReachFacts.Clear(); rallyReachRevision = revision; }
        var key = (unit.GetEntityId().GetHashCode(), from, revision, budget, band);
        if (rallyReachFacts.TryGetValue(key, out UnitReachEnvelope cached)) return cached;
        AIController context = GetRallyContext(unit.SlotIndex, unit.BoardTilemap);
        var map = unit.BoardTilemap != null ? unit.BoardTilemap : context?.boardTilemap;
        TerrainDatabase terrain = context?.terrainDatabase;
        // TerrainDatabase e um asset: o contexto do controller e obrigatorio em runtime.
        if (map == null || terrain == null) return null;
        var request = new UnitReachRequest { Unit = unit, BoardMap = map, TerrainDatabase = terrain,
            Intent = ReachIntent.Mobility, Band = band, MovementMode = UnitThreatEnvelopeMovement.Potential,
            MovementBudget = budget, OriginOverride = from,
            SubStep = data.IsAircraft() ? ReachSubStep.Aereo : ReachSubStep.Terrestre };
        if (!data.IsAircraft() && band == ReachBand.Tactical) request.PrebuiltCosts = AIActionReachCoordinator.BuildSectorReachMap(unit, map, terrain, from, budget, false);
        UnitReachEnvelope result = UnitReachEnvelopeService.Build(request);
        rallyReachFacts[key] = result;
        return result;
    }

    public static bool IsRallyParticipantInBand(UnitManager unit, Vector3Int anchor, ReachBand band, Vector3Int? origin = null)
    {
        anchor.z = 0;
        UnitManager mover = unit != null && unit.IsEmbarked ? unit.EmbarkedTransporter : unit;
        if (mover == null) return false;
        if ((origin ?? mover.CurrentCellPosition) == anchor) return true;
        return GetRallyReach(mover, band, origin)?.MovementCells.Contains(anchor) == true;
    }

    // Area publicada por revisao confirmada: planner, tela e transporte compartilham
    // as mesmas bandas e faixas. Nunca constroi um envelope por participante.
    private static int rallyAreaRevision = -1;
    private static readonly Dictionary<(ConstructionManager rally, int slot), RallyArea> rallyAreaFacts
        = new Dictionary<(ConstructionManager, int), RallyArea>();

    private static void ClearRallyAreasForSlot(int slot)
    {
        var keys = new List<(ConstructionManager rally, int slot)>();
        foreach (var key in rallyAreaFacts.Keys) if (key.slot == slot) keys.Add(key);
        foreach (var key in keys) rallyAreaFacts.Remove(key);
    }

    private static RallyArea GetRallyArea(ConstructionManager rally, TeamId team)
    {
        if (rally == null) return null;
        int revision = ThreatRevisionTracker.GlobalBoardRevision;
        if (rallyAreaRevision != revision) { rallyAreaFacts.Clear(); rallyAreaRevision = revision; }
        int slot = ResolveAISlotKey(team);
        var key = (rally, slot);
        if (rallyAreaFacts.TryGetValue(key, out RallyArea cached)) return cached;
        AIController ai = GetRallyContext(slot, rally.BoardTilemap);
        if (ai == null || ai.boardTilemap == null || ai.terrainDatabase == null) return null;
        var allies = new List<UnitManager>(); var enemies = new List<UnitManager>();
        UnitDatabase database = null;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.gameObject.scene != rally.gameObject.scene) continue;
            if (unit.SlotIndex == slot)
            {
                allies.Add(unit);
                if (database == null && unit.UnitDatabase != null) database = unit.UnitDatabase;
            }
            else if (!unit.IsEmbarked && PlayerSlotRelations.AreEnemies(slot, unit.SlotIndex)
                && ai.matchController.IsUnitVisibleForSlot(unit, PlayerSlotId.FromIndex(slot))) enemies.Add(unit);
        }
        if (database == null) database = FindAnyObjectByType<UnitSpawner>()?.UnitDatabase;
        UnitData model = ai.ResolveRallyFormationModel(rally, QuadranteController.Active?.BuiltQuadrant, database, out _);
        Vector3Int? axisOrigin = null;
        InvasionAxisMap axisMap = ai.currentAxisMap ?? InvasionAxisMap.Build(PlayerSlotId.FromIndex(slot), ai.boardTilemap);
        foreach (var axis in axisMap.Axes)
            if (!axis.IsInvasionAxis && axis.RallySector == rally.Sector) { axisOrigin = axis.HqCell; break; }
        RallyArea area = RallyAreaService.Build(rally, model, team, slot, ai.boardTilemap, ai.terrainDatabase, allies, enemies, axisOrigin);
        rallyAreaFacts[key] = area;
        return area;
    }

    public static bool IsRallyParticipantPresent(UnitManager unit, ConstructionManager rally, ReachBand band)
    {
        UnitManager mover = unit != null && unit.IsEmbarked ? unit.EmbarkedTransporter : unit;
        return mover != null && IsRallyParticipantPresent(unit, rally, band, mover.CurrentCellPosition);
    }

    internal static bool IsRallyParticipantPresent(UnitManager unit, ConstructionManager rally, ReachBand band, Vector3Int position)
    {
        UnitManager mover = unit != null && unit.IsEmbarked ? unit.EmbarkedTransporter : unit;
        if (mover == null || rally == null || !mover.TryGetUnitData(out UnitData data)) return false;
        position.z = 0;
        if (!data.IsAircraft()) return IsRallyParticipantInBand(unit, rally.CurrentCellPosition, band, position);
        RallyArea area = GetRallyArea(rally, mover.TeamId);
        if (area == null) return false;
        return band == ReachBand.Tactical ? area.Core.Contains(position) || area.Vanguard.Contains(position)
            : area.Approach.Contains(position);
    }

    private static int RallyForceScore(int capturers, float breakthrough, float support)
        => capturers + Mathf.RoundToInt(breakthrough * 3f) + Mathf.RoundToInt(support * 2f);

    private static float CountRallyLocalEnemyForce(ConstructionManager rally, TeamId team, out int threats)
    {
        threats = 0;
        int slot = ResolveAISlotKey(team);
        AIController ai = GetRallyContext(slot, rally.BoardTilemap);
        MatchController match = ai?.matchController;
        if (ai == null || ai.boardTilemap == null || match == null) return 0f;
        RallyArea area = GetRallyArea(rally, team);
        if (area == null) return 0f;
        HashSet<Vector3Int> cells = area.Approach;
        Vector2 center = HexCoordinates.GetCellCenterWorld(ai.boardTilemap, rally.CurrentCellPosition);
        Vector2 forward = Vector2.zero;
        InvasionAxisMap axisMap = ai.currentAxisMap ?? InvasionAxisMap.Build(PlayerSlotId.FromIndex(slot), ai.boardTilemap);
        foreach (var axis in axisMap.Axes)
            if (!axis.IsInvasionAxis && axis.RallySector == rally.Sector)
            { forward = center - (Vector2)HexCoordinates.GetCellCenterWorld(ai.boardTilemap, axis.HqCell); break; }
        float force = 0f;
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.IsDead || enemy.IsEmbarked || !PlayerSlotRelations.AreEnemies(slot, enemy.SlotIndex)
                || !match.IsUnitVisibleForSlot(enemy, PlayerSlotId.FromIndex(slot)) || !cells.Contains(enemy.CurrentCellPosition)) continue;
            Vector2 delta = (Vector2)HexCoordinates.GetCellCenterWorld(ai.boardTilemap, enemy.CurrentCellPosition) - center;
            if (forward.sqrMagnitude > 0f && Vector2.Dot(delta, forward) < 0f) continue;
            threats++;
            if (!enemy.TryGetUnitData(out UnitData data)) { force += 1f; continue; }
            force += Mathf.Max(1f, RallyForceScore(HasRallyCaptureSkill(data) ? 1 : 0, GetRallyAssaultWeight(data), GetRallyArtilleryWeight(data)));
        }
        return force;
    }

    private static void ObserveRallySaturation(SectorObjective obj, int turn, bool floor, float rupture, int force)
    {
        if (obj == null || turn < 0 || obj.RallyLastObservedTurn == turn) return;
        if (!floor)
        {
            obj.RallyFloorTurn = -1; obj.RallyStagnantTurns = 0;
        }
        else if (obj.RallyFloorTurn < 0) { obj.RallyFloorTurn = turn; obj.RallyStagnantTurns = 0; }
        else if (obj.RallyLastObservedTurn >= 0)
            obj.RallyStagnantTurns = rupture - obj.RallyPreviousRupture >= 1f || force - obj.RallyPreviousForce >= 2
                ? 0 : obj.RallyStagnantTurns + 1;
        obj.RallyLastObservedTurn = turn;
        obj.RallyPreviousRupture = rupture;
        obj.RallyPreviousForce = force;
    }

    private static bool IsRallySaturated(SectorObjective obj, int turn, bool floor)
        => floor && obj != null && obj.RallyFloorTurn >= 0
            && (obj.RallyStagnantTurns >= 2 || turn - obj.RallyFloorTurn >= 4);

    private static void StartRallyAdvance(SectorObjective objective, ConstructionManager rally, TeamId team)
    {
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(ResolveAISlotKey(team)));
        if (plan == null) return;
        var participants = new HashSet<int>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead || unit.SlotIndex != ResolveAISlotKey(team)
                ) continue;
            bool core = IsRallyParticipantPresent(unit, rally, ReachBand.Tactical);
            if (!core && !IsRallyParticipantPresent(unit, rally, ReachBand.Operational)) continue;
            bool assigned = objective.Slots.Exists(need => need.Filled && need.AssignedUnitId == unit.InstanceId);
            if (!core && !assigned && !plan.RogueUnitIds.Contains(unit.InstanceId)) continue;
            participants.Add(unit.InstanceId);
            if (unit.IsEmbarked || !unit.TryGetUnitData(out UnitData data) || !HasRallyCaptureSkill(data)
                || !IsRallyParticipantPresent(unit, rally, ReachBand.Tactical)) continue;
            if (objective.RallyCaptainId < 0 || unit.InstanceId < objective.RallyCaptainId) objective.RallyCaptainId = unit.InstanceId;
        }
        objective.RallyParticipantIds = new List<int>(participants);
        if (!plan.RallyAdvances.Exists(o => o.Sector == objective.Sector && o.RallyGoGreenTurn == objective.RallyGoGreenTurn))
            plan.RallyAdvances.Add(objective);
        ReconcileRallyAdvanceParticipants(plan, team);
    }

    // Um liberado so pertence a um montinho. Nao deixa dois capitaes esperando
    // a mesma unidade quando os rallies completam juntos.
    private static void ReconcileRallyAdvanceParticipants(TeamObjectivePlan plan, TeamId team)
    {
        var ids = new HashSet<int>();
        foreach (SectorObjective group in plan.RallyAdvances) ids.UnionWith(group.RallyParticipantIds);
        foreach (SectorObjective group in plan.RallyAdvances) group.RallyParticipantIds.Clear();
        var snapshot = new AIWorldSnapshot { AITeam = team, AISlotIndex = ResolveAISlotKey(team) };
        foreach (int id in ids)
        {
            UnitManager unit = FindActiveUnit(id, team);
            if (unit == null || unit.IsDead) continue;
            SectorObjective own = plan.RallyAdvances.Find(g => g.RallyCaptainId == id);
            if (own != null) { own.RallyParticipantIds.Add(id); continue; }
            if (!TryResolveRallyAdvanceCaptain(unit, snapshot, out UnitManager captain)) continue;
            plan.RallyAdvances.Find(g => g.RallyCaptainId == captain.InstanceId)?.RallyParticipantIds.Add(id);
        }
    }

    private static bool TryResolveRallyAdvanceCaptain(UnitManager follower, AIWorldSnapshot snapshot, out UnitManager captain)
    {
        captain = null;
        if (snapshot == null || follower == null) return false;
        var plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        int bestCost = int.MaxValue;
        UnitReachEnvelope operational = null;
        foreach (SectorObjective group in plan?.RallyAdvances ?? new List<SectorObjective>())
        {
            UnitManager candidate = FindActiveUnit(group.RallyCaptainId, snapshot.AITeam);
            if (candidate == null || candidate.IsDead || candidate.IsEmbarked || candidate == follower) continue;
            operational ??= GetRallyReach(follower, ReachBand.Operational);
            if (operational == null || !operational.MovementCells.Contains(candidate.CurrentCellPosition)) continue;
            int cost = operational.CostByCell.TryGetValue(candidate.CurrentCellPosition, out int c) ? c
                : operational.PathsByDestination.TryGetValue(candidate.CurrentCellPosition, out var path) && path != null ? path.Count - 1 : int.MaxValue - 1;
            if (cost < bestCost || (cost == bestCost && (captain == null || candidate.InstanceId < captain.InstanceId)))
            { captain = candidate; bestCost = cost; }
        }
        return captain != null;
    }

    private static bool UpdateRallyAdvanceWaiting(TeamObjectivePlan plan, TeamId team)
    {
        bool anyWaiting = false;
        foreach (SectorObjective group in plan.RallyAdvances)
        {
            group.RallyWaiting = false;
            UnitManager captain = FindActiveUnit(group.RallyCaptainId, team);
            if (captain == null || captain.IsDead || captain.IsEmbarked) continue;
            for (int i = group.RallyParticipantIds.Count - 1; i >= 0; i--)
            {
                int id = group.RallyParticipantIds[i];
                UnitManager unit = FindActiveUnit(id, team);
                if (unit == null || unit.IsDead || unit.IsUnderRepair || unit == captain) continue;
                if (!IsRallyParticipantInBand(unit, captain.CurrentCellPosition, ReachBand.Operational))
                {
                    group.RallyParticipantIds.RemoveAt(i);
                    Debug.Log($"[Rally] solta retardatário #{id}: capitão fora do Operacional");
                    continue;
                }
                if (!IsRallyParticipantInBand(unit, captain.CurrentCellPosition, ReachBand.Tactical))
                { group.RallyWaiting = true; anyWaiting = true; }
            }
        }
        return anyWaiting;
    }

    private static bool IsRallyCaptainWaiting(UnitManager unit, AIWorldSnapshot snapshot)
    {
        if (unit == null || snapshot == null || unit.IsUnderRepair) return false;
        var plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        if (plan == null || !plan.RallyAdvances.Exists(g => g.RallyCaptainId == unit.InstanceId)) return false;
        UpdateRallyAdvanceWaiting(plan, snapshot.AITeam);
        return plan.RallyAdvances.Exists(g => g.RallyCaptainId == unit.InstanceId && g.RallyWaiting);
    }

}
