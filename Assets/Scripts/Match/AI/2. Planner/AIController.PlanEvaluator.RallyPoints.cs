using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public partial class AIController
{
    private const int RallyPointSectorPriorityBonus = 55;
    private const int RallyHoldRadius = 2;
    private const int RallyAssemblyForceRadius = 3;
    private const int RallyAirAttackRadius = 5;
    private const int RallyArtilleryRadius = 4;
    private const int RallyMinimumArtillery = 3;
    private const int RallyMinimumLocalLaunchArtillery = 3;
    private const int RallyArtilleryRecruitRange = 8;
    private const int RallyMaximumArtilleryUnits = 12;
    private const int RallyInfantryTransportDistance = 7;
    private const int RallyUsefulApcCapacity = 2;
    private const int RallyIntelRadius = 6;
    private const int RallyLogisticsRadius = 5;
    private const int RallyAssemblyTimeoutTurns = 4;
    private const int RallyGoGreenSuppressTurns = 8;
    // Foco pegajoso: o foco da operação só troca se outro rally bater o atual por esta margem.
    private const float RallyFocusSwitchMargin = 1.5f;
    private static readonly Dictionary<string, int> rallyGoGreenTurns = new Dictionary<string, int>();
    private static readonly Dictionary<string, AIRallyHudSnapshot> rallyHudStates = new Dictionary<string, AIRallyHudSnapshot>();
    // Memória do FocusSector por time, para hysteresis (não drenar um rally que já monta massa).
    private static readonly Dictionary<TeamId, ConstructionSector> rallyFocusMemory = new Dictionary<TeamId, ConstructionSector>();

    private struct AIRallyPlanContext
    {
        public HashSet<ConstructionSector> TargetingEnemyHQ;
        public int AISlotIndex;
        public int RallyPointCount;
    }

    private struct AIRallyReadiness
    {
        public bool Held;
        public int Capturers;
        public int Assault;
        public int AirAttack;
        public float Artillery;
        public float LocalArtillery;
        public int HeldRallies;
        public int RequiredArtillery;
        public ConstructionSector FocusSector;
        public bool IsFocus;
        public int Intel;
        public int Logistics;
        public int VisibleThreats;
        public int KnownEnemyForce;
        public int RequiredPackages;
        public int RequiredForce;
        public int ForceScore;
        public bool Timeout;
        public bool GoGreen;
        public AIRallyAssemblyState State;
        public string Status;
        public string Missing;
    }

    private struct AIRallyAggregate
    {
        public int HeldRallies;
        public int Capturers;
        public int Assault;
        public int AirAttack;
        public float Artillery;
        public int Intel;
        public int Logistics;
        public ConstructionSector FocusSector;
    }

    private struct AIRallyInfluence
    {
        public bool Active;
        public ConstructionSector Sector;
        public Vector3Int Anchor;
        public AIRallyAssemblyState State;
        public AIRallyReadiness Readiness;
        public int AssemblyRadius;
        public int SupportRadius;
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
            LogRallyReadiness(rally, rally.RallyOwnerSlotIndex, aiTeam, turnNumber, intel);
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

    // O ponto de rally do setor esta sob controle ATUAL da AI? (rally.TeamId == aiTeam — o "Slot ID"
    // atual do ponto, nao o "Rally Owner Slot" que e so a intencao de dono quando conquistado.)
    private static bool IsRallyPointHeldByTeam(ConstructionSector sector, TeamId aiTeam)
    {
        if (sector == ConstructionSector.None || ConstructionManager.AllActive == null)
            return false;
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
            if (rally != null && rally.IsRallyPoint && rally.Sector == sector)
                return rally.TeamId == aiTeam;
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

        return rally.RallyOwnerSlotIndex == aiSlot;
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
        if (!IsRallyOwnedBySlot(rally, aiTeam, ResolveAISlotIndex(aiTeam, GetMatchController())))
            return false;

        ownerSlot = rally.RallyOwnerSlotIndex;
        return true;
    }

    private static int ResolveAISlotIndex(TeamId aiTeam, MatchController mc)
    {
        if (mc == null || aiTeam == TeamId.Neutral)
            return -1;

        if (mc.ActiveTeam == aiTeam)
            return mc.ActivePlayerListIndex;

        return mc.GetSlotIndexForTeam(aiTeam);
    }

    private static void LogRallyReadiness(
        ConstructionManager rally,
        int ownerSlot,
        TeamId aiTeam,
        int turnNumber,
        AIIntelReport intel)
    {
        AIRallyReadiness readiness = EvaluateRallyReadiness(rally, aiTeam, turnNumber, -1, intel);
        string rallyName = rally != null ? rally.name : "(null)";
        ConstructionSector sector = rally != null ? rally.Sector : ConstructionSector.None;
        PublishRallyHudState(rally, readiness.State, readiness.Status, turnNumber,
            sector != ConstructionSector.None && sector == readiness.FocusSector);

        Debug.Log(
            $"[AI Rally][T{turnNumber}][{aiTeam}] {sector} via {rallyName} owner={ownerSlot} " +
            $"held={readiness.Held} rallies={readiness.HeldRallies} focus={readiness.FocusSector} " +
            $"artGlobal={readiness.Artillery:0.#}/{readiness.RequiredArtillery} " +
            $"artLocal={readiness.LocalArtillery:0.#} cap={readiness.Capturers} " +
            $"ass={readiness.Assault} airAtk={readiness.AirAttack} intel={readiness.Intel} log={readiness.Logistics} " +
            $"threat={readiness.VisibleThreats} knownEnemy={readiness.KnownEnemyForce} " +
            $"packages={readiness.RequiredPackages} force={readiness.ForceScore}/{readiness.RequiredForce} " +
            $"rallyState={readiness.State} goGreen={readiness.GoGreen} timeout={readiness.Timeout} " +
            $"missing={readiness.Missing} {readiness.Status}");
    }

    private static AIRallyReadiness EvaluateRallyReadiness(ConstructionManager rally, TeamId aiTeam)
    {
        return EvaluateRallyReadiness(rally, aiTeam, -1, -1, null);
    }

    private static AIRallyReadiness EvaluateRallyReadiness(
        ConstructionManager rally,
        TeamId aiTeam,
        int turnNumber,
        int startedTurn,
        AIIntelReport intel)
    {
        AIRallyReadiness readiness = new AIRallyReadiness
        {
            Status = "WAIT_HOLD",
            State = AIRallyAssemblyState.WaitHold,
            Missing = "hold"
        };
        if (rally == null)
            return readiness;

        Vector3Int anchor = rally.CurrentCellPosition;
        anchor.z = 0;

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsEmbarked || !unit.gameObject.activeInHierarchy)
                continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            float dist = SectorManager.HexDistance(anchor, unitCell);

            bool capturer = HasRole(data, UnitRole.Capturador);
            bool assault = IsRallyPrimaryAssaultUnit(data);
            bool airAttack = IsOperationalRallyAirAttackUnit(unit, data);
            float artillery = GetRallyArtilleryWeight(data);
            bool intelUnit = HasRole(data, UnitRole.Intel);
            bool logistics = HasRole(data, UnitRole.Logistica);

            if (dist <= RallyAssemblyForceRadius)
            {
                if (capturer)
                    readiness.Capturers++;
                if (assault)
                    readiness.Assault++;
            }

            // Ataque aéreo conta num raio maior (5h — aviões reposicionam rápido), mas ainda por
            // PRESENÇA: um avião disperso longe do rally não infla o GoGreen.
            if (dist <= RallyAirAttackRadius && airAttack)
                readiness.AirAttack++;

            if (dist <= RallyArtilleryRadius && artillery > 0f)
                readiness.Artillery += artillery;
            if (dist <= RallyIntelRadius && intelUnit)
                readiness.Intel++;
            if (dist <= RallyLogisticsRadius && logistics)
                readiness.Logistics++;
        }

        readiness.LocalArtillery = readiness.Artillery;
        AIRallyAggregate aggregate = BuildRallyAggregate(aiTeam);
        readiness.HeldRallies = aggregate.HeldRallies;
        readiness.RequiredArtillery = GetRallyArtilleryRequirement(aggregate.HeldRallies);
        readiness.FocusSector = aggregate.FocusSector;
        readiness.IsFocus = rally.Sector == aggregate.FocusSector;
        if (aggregate.HeldRallies > 0)
        {
            readiness.Capturers = aggregate.Capturers;
            readiness.Assault = aggregate.Assault;
            readiness.AirAttack = aggregate.AirAttack;
            readiness.Artillery = aggregate.Artillery;
            readiness.Intel = aggregate.Intel;
            readiness.Logistics = aggregate.Logistics;
        }

        // HELD/conquistado = o DONO ATUAL do ponto de rally e a AI (rally.TeamId == aiTeam).
        // NAO basta ter unidade amiga perto nem maioria de predios laterais — o slot atual do
        // ponto tem que ser o da AI (== rally owner slot quando conquistado). Ex.: Hotel com
        // Slot ID=0(verde) e Rally Owner=1(vermelho) NAO esta held pelo vermelho.
        readiness.Held = rally.TeamId == aiTeam;
        readiness.VisibleThreats = CountVisibleEnemyThreatsNearRally(anchor, aiTeam, RallyAssemblyForceRadius + 1);
        readiness.KnownEnemyForce = CountLiveEnemyUnits(aiTeam);
        readiness.RequiredPackages = Mathf.Clamp(
            Mathf.Max(
                1,
                Mathf.CeilToInt(readiness.KnownEnemyForce / 8f),
                Mathf.CeilToInt(readiness.VisibleThreats / 3f)),
            1,
            5);
        readiness.RequiredForce = 1
            + (readiness.RequiredPackages * 3)
            + (readiness.RequiredArtillery * 2);

        int breakthrough = readiness.Assault + readiness.AirAttack;
        float usefulArtillery = Mathf.Min(readiness.Artillery, readiness.RequiredArtillery);
        readiness.ForceScore = readiness.Capturers + (breakthrough * 3)
            + Mathf.RoundToInt(usefulArtillery * 2f);
        readiness.Timeout = startedTurn >= 0 && turnNumber >= 0
            && turnNumber - startedTurn >= RallyAssemblyTimeoutTurns;

        bool hasHoldPackage = readiness.Held
            && readiness.Capturers >= 1
            && breakthrough >= readiness.RequiredPackages;
        bool hasArtillery = readiness.Artillery >= readiness.RequiredArtillery;
        bool hasLocalLaunchArtillery = readiness.LocalArtillery
            >= Mathf.Min(RallyMinimumLocalLaunchArtillery, readiness.RequiredArtillery);
        bool hasRequiredForce = readiness.ForceScore >= readiness.RequiredForce;
        // DOMÍNIO macro (Ganhando) GREENA o rally held mesmo sem a composição completa: se você já
        // domina território E força, segurar a montagem só atrasa o fechamento. Assim o HUD fica
        // verde E a massa montada é LIBERADA pra invadir (a lógica de assembly para de segurar).
        // Calculado FRESCO aqui (não pelo cache) porque o rally é avaliado ANTES do macro no fluxo
        // do plano — o cache teria o macro do turno anterior (semáforo ficava amarelo 1 turno atrás).
        bool macroDominating = BuildMacroTerritoryContext(
            aiTeam, SectorManager.GetAllSectorInfos(), 6,
            CountLiveUnitsOfTeam(aiTeam), CountLiveEnemyUnits(aiTeam)).Phase == AIMacroTerritoryPhase.Dominating;
        // Modelo multi-centro: o assalto é liberado pela PRONTIDÃO COMBINADA (todos os rallies held
        // somam, overlap conta 1x) — NÃO por um "foco". Todo rally held vira GoGreen junto quando a
        // massa combinada satisfaz os mínimos, OU domínio macro, OU vantagem numérica >= 2:1.
        int friendlyForce = CountLiveUnitsOfTeam(aiTeam);
        bool numericalDominance = friendlyForce >= 2 * Mathf.Max(1, readiness.KnownEnemyForce);
        readiness.GoGreen = readiness.Held
            && ((hasHoldPackage && hasRequiredForce && hasArtillery) || macroDominating || numericalDominance);

        if (!readiness.Held)
        {
            readiness.State = AIRallyAssemblyState.WaitHold;
            readiness.Status = "WAIT_HOLD";
            readiness.Missing = "hold";
        }
        else if (readiness.GoGreen)
        {
            readiness.State = AIRallyAssemblyState.GoGreen;
            readiness.Status = numericalDominance && !(hasHoldPackage && hasRequiredForce && hasArtillery)
                ? $"GO_GREEN(2:1 {friendlyForce}v{readiness.KnownEnemyForce})"
                : "GO_GREEN";
            readiness.Missing = "-";
        }
        else if (hasHoldPackage && hasArtillery)
        {
            readiness.State = AIRallyAssemblyState.Ready;
            readiness.Status = "READY";
            readiness.Missing = "-";
        }
        else
        {
            readiness.State = AIRallyAssemblyState.Assembling;
            readiness.Status = readiness.Artillery <= 0f ? "ASSEMBLING_WAIT_ART" : "ASSEMBLING";
            readiness.Missing = BuildRallyMissingText(
                readiness,
                hasHoldPackage,
                hasArtillery,
                hasLocalLaunchArtillery,
                hasRequiredForce);
        }

        return readiness;
    }

    private static AIRallyAggregate BuildRallyAggregate(TeamId aiTeam)
    {
        var aggregate = new AIRallyAggregate { FocusSector = ConstructionSector.None };
        var held = new List<ConstructionManager>();
        int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
        foreach (ConstructionManager rally in ConstructionManager.AllActive)
        {
            if (rally == null || !rally.IsRallyPoint || rally.TeamId != aiTeam)
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
        if (rallyFocusMemory.TryGetValue(aiTeam, out ConstructionSector prevFocus)
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
        rallyFocusMemory[aiTeam] = chosenFocus;

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsEmbarked
                || !unit.gameObject.activeInHierarchy
                || !unit.TryGetUnitData(out UnitData data) || data == null)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            bool nearForce = false;
            bool nearAir = false;
            bool nearArtillery = false;
            bool nearIntel = false;
            bool nearLogistics = false;
            foreach (ConstructionManager rally in held)
            {
                Vector3Int anchor = rally.CurrentCellPosition;
                anchor.z = 0;
                float distance = SectorManager.HexDistance(anchor, cell);
                nearForce |= distance <= RallyAssemblyForceRadius;
                nearAir |= distance <= RallyAirAttackRadius;
                nearArtillery |= distance <= RallyArtilleryRadius;
                nearIntel |= distance <= RallyIntelRadius;
                nearLogistics |= distance <= RallyLogisticsRadius;
            }

            if (nearForce && HasRole(data, UnitRole.Capturador))
                aggregate.Capturers++;
            if (nearForce && IsRallyPrimaryAssaultUnit(data))
                aggregate.Assault++;
            if (nearAir && IsOperationalRallyAirAttackUnit(unit, data))
                aggregate.AirAttack++;
            if (nearArtillery)
                aggregate.Artillery += GetRallyArtilleryWeight(data);
            if (nearIntel && HasRole(data, UnitRole.Intel))
                aggregate.Intel++;
            if (nearLogistics && HasRole(data, UnitRole.Logistica))
                aggregate.Logistics++;
        }

        return aggregate;
    }

    private static float ScoreRallyLaunchFocus(ConstructionManager rally, TeamId aiTeam)
    {
        if (rally == null)
            return float.MinValue;
        Vector3Int anchor = rally.CurrentCellPosition;
        anchor.z = 0;
        float score = 0f;
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsEmbarked
                || !unit.gameObject.activeInHierarchy
                || !unit.TryGetUnitData(out UnitData data) || data == null)
                continue;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            float distance = SectorManager.HexDistance(anchor, cell);
            if (distance <= RallyAssemblyForceRadius)
            {
                if (HasRole(data, UnitRole.Capturador)) score += 100f;
                if (IsRallyPrimaryAssaultUnit(data)) score += 300f;
            }
            if (distance <= RallyArtilleryRadius)
                score += GetRallyArtilleryWeight(data) * 200f;
        }
        return score;
    }

    private static int GetRallyArtilleryRequirement(int heldRallies)
    {
        if (heldRallies >= 3) return 6;
        if (heldRallies == 2) return 5;
        return RallyMinimumArtillery;
    }

    private static int GetCurrentRallyArtilleryRequirement(TeamId aiTeam)
    {
        return GetRallyArtilleryRequirement(BuildRallyAggregate(aiTeam).HeldRallies);
    }

    private static int CountVisibleEnemyThreatsNearRally(Vector3Int anchor, TeamId aiTeam, float radius)
    {
        MatchController mc = GetMatchController();
        int count = 0;
        foreach (UnitManager enemy in UnitManager.AllActive)
        {
            if (enemy == null || enemy.TeamId == aiTeam || enemy.IsDead || enemy.IsEmbarked)
                continue;
            if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;
            if (SectorManager.HexDistance(anchor, enemyCell) <= radius)
                count++;
        }

        return count;
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
            if (u.TeamId == aiTeam || u.TeamId == TeamId.Neutral) continue;
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

    private static string BuildRallyMissingText(
        AIRallyReadiness readiness,
        bool hasHoldPackage,
        bool hasArtillery,
        bool hasLocalLaunchArtillery,
        bool hasRequiredForce)
    {
        string missing = "";
        if (!hasHoldPackage)
        {
            if (readiness.Capturers < 1)
                missing += $"cap({readiness.Capturers}/1)";
            int breakthrough = readiness.Assault + readiness.AirAttack;
            if (breakthrough < readiness.RequiredPackages)
                missing += string.IsNullOrEmpty(missing)
                    ? $"ruptura({breakthrough}/{readiness.RequiredPackages})"
                    : $"+ruptura({breakthrough}/{readiness.RequiredPackages})";
        }
        if (!hasArtillery)
            missing += string.IsNullOrEmpty(missing)
                ? $"artGlobal({readiness.Artillery:0.#}/{readiness.RequiredArtillery})"
                : $"+artGlobal({readiness.Artillery:0.#}/{readiness.RequiredArtillery})";
        if (!hasLocalLaunchArtillery)
        {
            int localRequired = Mathf.Min(RallyMinimumLocalLaunchArtillery, readiness.RequiredArtillery);
            missing += string.IsNullOrEmpty(missing)
                ? $"artLocal({readiness.LocalArtillery:0.#}/{localRequired})"
                : $"+artLocal({readiness.LocalArtillery:0.#}/{localRequired})";
        }
        if (!hasRequiredForce)
            missing += string.IsNullOrEmpty(missing)
                ? $"forca({readiness.ForceScore}/{readiness.RequiredForce})"
                : $"+forca({readiness.ForceScore}/{readiness.RequiredForce})";
        return string.IsNullOrEmpty(missing) ? "-" : missing;
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

        if (obj.RallyAssemblyStartedTurn < 0)
            obj.RallyAssemblyStartedTurn = turnNumber;

        AIRallyReadiness readiness = EvaluateRallyReadiness(
            rally,
            aiTeam,
            turnNumber,
            obj.RallyAssemblyStartedTurn,
            intel);
        Vector3Int rallyAnchor = rally.CurrentCellPosition;
        rallyAnchor.z = 0;
        // Multi-centro: TODO rally held mantém seu próprio arco com uma FATIA do requisito combinado
        // (distribuída entre os centros), em vez de um foco provisionar tudo. Capturador: 1 por centro.
        int heldRallies = Mathf.Max(1, readiness.HeldRallies);
        int sharePackages = Mathf.CeilToInt(readiness.RequiredPackages / (float)heldRallies);
        int shareArtillery = Mathf.CeilToInt(readiness.RequiredArtillery / (float)heldRallies);
        EnsureRallyAssemblySlots(obj, sharePackages, 0, shareArtillery, aiTeam, rallyAnchor);
        obj.RallyState = readiness.State;
        obj.RallyReadinessReason =
            $"{readiness.Status} ready={readiness.ForceScore} cap={readiness.Capturers} " +
            $"ass={readiness.Assault} airAtk={readiness.AirAttack} " +
            $"artGlobal={readiness.Artillery:0.#}/{readiness.RequiredArtillery} " +
            $"artLocal={readiness.LocalArtillery:0.#} rallies={readiness.HeldRallies} " +
            $"focus={readiness.FocusSector} intel={readiness.Intel} log={readiness.Logistics} " +
            $"threat={readiness.VisibleThreats} knownEnemy={readiness.KnownEnemyForce} " +
            $"packages={readiness.RequiredPackages} force={readiness.ForceScore}/{readiness.RequiredForce} " +
            $"missing={readiness.Missing}";
        PublishRallyHudState(rally, readiness.State, obj.RallyReadinessReason, turnNumber,
            rally != null && rally.Sector != ConstructionSector.None && rally.Sector == readiness.FocusSector);

        if (readiness.GoGreen && obj.RallyGoGreenTurn < 0)
        {
            obj.RallyGoGreenTurn = turnNumber;
            // O Go Green pertence a operacao, nao a um predio isolado. Suprime todos os
            // rallies assegurados para que feeders nao iniciem uma segunda montagem paralela.
            int slot = ResolveAISlotIndex(aiTeam, GetMatchController());
            foreach (ConstructionManager heldRally in ConstructionManager.AllActive)
                if (heldRally != null && heldRally.IsRallyPoint && heldRally.TeamId == aiTeam
                    && IsRallyOwnedBySlot(heldRally, aiTeam, slot))
                    RememberRallyGoGreen(aiTeam, heldRally.Sector, turnNumber);
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

            string goGreenKey = $"{(int)snapshot.AITeam}:{obj.Sector}";
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
        currentAxisMap = InvasionAxisMap.Build(aiTeam);
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
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || slottedIds.Contains(unit.InstanceId))
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
        int requiredPackages,
        int operationalAirAttack,
        int requiredArtillery,
        TeamId aiTeam,
        Vector3Int rallyAnchor)
    {
        if (obj == null)
            return;

        EnsureRallyRoleSlots(obj, UnitRole.Capturador, 1, aiTeam, rallyAnchor);
        EnsureRallyRoleSlots(
            obj,
            UnitRole.Assalto,
            Mathf.Max(0, requiredPackages - operationalAirAttack),
            aiTeam,
            rallyAnchor);
        EnsureRallyArtillerySlots(obj, requiredArtillery, aiTeam);
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
        Vector3Int rallyAnchor)
    {
        int current = 0;
        for (int i = 0; i < obj.Slots.Count; i++)
        {
            if (obj.Slots[i].Role == role)
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

        while (current > 1)
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
        bool isMain)
    {
        if (rally == null || !rally.IsRallyPoint || rally.RallyOwnerSlotIndex < 0 || rally.Sector == ConstructionSector.None)
            return;

        string key = BuildRallyHudKey(rally.RallyOwnerSlotIndex, rally.Sector);
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
            ConstructionManager.RefreshRallyHudVisuals(rally.Sector, rally.RallyOwnerSlotIndex);
    }

    private static string BuildRallyHudKey(int rallyOwnerSlotIndex, ConstructionSector sector)
    {
        return $"{rallyOwnerSlotIndex}:{sector}";
    }

    private static void RememberRallyGoGreen(TeamId aiTeam, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return;
        rallyGoGreenTurns[$"{(int)aiTeam}:{sector}"] = turnNumber;
    }

    private static bool IsRallyGoGreenSuppressed(TeamId aiTeam, ConstructionSector sector, int turnNumber)
    {
        if (sector == ConstructionSector.None || turnNumber < 0)
            return false;
        string key = $"{(int)aiTeam}:{sector}";
        return rallyGoGreenTurns.TryGetValue(key, out int goTurn)
            && turnNumber - goTurn <= RallyGoGreenSuppressTurns;
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
    {
        return UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Assalto;
    }

    private static bool IsOperationalRallyAirAttackUnit(UnitManager unit, UnitData data)
    {
        if (unit == null || data == null || unit.IsUnderRepair)
            return false;
        if (data.roles == null || data.roles.Count == 0 || data.roles[0] != UnitRole.AtaqueAereo)
            return false;
        if (unit.MaxAmmo > 0 && unit.CurrentAmmo <= 0)
            return false;
        if (unit.MaxFuel > 0 && unit.CurrentFuel <= 0)
            return false;

        return true;
    }

    private static bool IsRealRallyArtilleryUnit(UnitData data)
    {
        return GetRallyArtilleryWeight(data) >= 1f;
    }

    private static float GetRallyArtilleryWeight(UnitData data)
    {
        if (data == null)
            return 0f;
        if (!HasRole(data, UnitRole.FogoIndireto) && data.unitClass != GameUnitClass.Artillery)
            return 0f;

        string key = NormalizeRallyUnitKey($"{data.id} {data.displayName} {data.apelido}");
        if (key.Contains("obus") && key.Contains("leve"))
            return 0.5f;
        if (key.Contains("art") && key.Contains("campanha"))
            return 1.5f;
        if (key.Contains("astros") || (key.Contains("obus") && key.Contains("medio")))
            return 1f;

        // Nova artilharia ainda não cadastrada nominalmente participa como apoio leve,
        // sem equivaler automaticamente às peças pesadas conhecidas.
        return 0.5f;
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
            if (unit == null || unit.TeamId != aiTeam || unit.IsDead || unit.IsUnderRepair
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
        int requiredPower = GetCurrentRallyArtilleryRequirement(aiTeam);
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

    private static bool HasFilledRealRallyArtillerySlot(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return false;

        for (int i = 0; i < obj.Slots.Count; i++)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot == null || slot.Role != UnitRole.FogoIndireto || !slot.Filled)
                continue;

            UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
            if (unit == null || !unit.TryGetUnitData(out UnitData data))
                continue;

            if (IsRealRallyArtilleryUnit(data))
                return true;
        }

        return false;
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
            AIRallyReadiness r = EvaluateRallyReadiness(rally, aiTeam, turnNumber, -1, intel);
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
            AssemblyRadius = RallyAssemblyForceRadius,
            SupportRadius = RallyArtilleryRadius,
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
            bestObj.RallyAssemblyStartedTurn,
            null);

        influence = new AIRallyInfluence
        {
            Active = true,
            Sector = bestObj.Sector,
            Anchor = anchor,
            State = bestObj.RallyState,
            Readiness = readiness,
            AssemblyRadius = RallyAssemblyForceRadius,
            SupportRadius = RallyArtilleryRadius,
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
}
