using System.Collections.Generic;
using UnityEngine;

// Construtores de operações táticas: um método TryBuild* por tipo de necessidade.
public partial class AITacticalAnalyzer
{
    private void TryBuildBaseDefenseOp(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        int aircraftNearHQ = CountVisibleEnemyAircraftNearHQ(snapshot, InstanceSafeAntiAirCoverageRange());
        int fighterANearHQ = CountVisibleEnemyFighterANearHQ(snapshot, InstanceSafeAntiAirCoverageRange());
        int armorNearBase = CountVisibleEnemyArmorNearOwnedBase(snapshot, DefensiveArmorThreatRange);
        bool captureActive = CountOwnedHomeConstructionsUnderCapture(snapshot, team) > 0;
        bool homeThreat = IsHomeDefenseThreatened(snapshot, team, HomeThreatRange) || captureActive;
        AISectorIntel homeIntel = FindIntelForSector(intel, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        bool intelHomeThreat = homeIntel != null && IsHotIntelSector(homeIntel);

        if (aircraftNearHQ <= 0 && armorNearBase <= 0 && !homeThreat && !intelHomeThreat)
            return;

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.BaseDefense, 1, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsUrgent = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.LinkedObjective = FindHomeDefenseObjective(plan, team);

        if (aircraftNearHQ > 0 || (homeIntel != null && intel.enemyAirThreatScore >= 2f))
        {
            if (fighterANearHQ > 0)
                op.AddSlots(AINeedKind.FighterA, CountActiveNeed(snapshot, AINeedKind.FighterA) <= 0 ? 1 : 0);
            else if (CountActiveNeed(snapshot, AINeedKind.FighterA) + CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0)
                op.AddSlots(AINeedKind.FighterB, 1);
            op.AddSlots(AINeedKind.AAA, Mathf.Max(0, InstanceMinBaseAAA() - CountActiveNeed(snapshot, AINeedKind.AAA)));
            if (CountActiveNeed(snapshot, AINeedKind.SAM) < 1)
                op.AddSlots(AINeedKind.SAM, 1);
        }

        if (homeThreat || armorNearBase > 0)
        {
            if (captureActive || (homeIntel != null && homeIntel.capturePressure > 0f))
                op.AddSlots(AINeedKind.Capturer, 2);
            else if (CountOwnedConstructionsUnderCapture(snapshot, team) > 0)
                op.AddSlots(AINeedKind.Capturer, 1);
            op.AddSlots(AINeedKind.Assault, armorNearBase > 0 || (intel != null && intel.enemyArmorThreatScore >= 2f) ? 2 : 1);
            op.AddSlots(AINeedKind.Artillery, 1);
        }

        if (op.RequiredSlots.Count == 0)
            return;

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] BaseDefense URGENTE aircraft={aircraftNearHQ} fighterA={fighterANearHQ} armor={armorNearBase} capture={captureActive} intelHot={(homeIntel != null ? homeIntel.hotScore.ToString("F1") : "-")} slots={DescribeSlots(op)}");
    }

    private void TryBuildSectorDefenseOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null)
            return;

        var built = new HashSet<ConstructionSector>();
        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Status != ObjectiveStatus.Defending) continue;
            if (ConstructionSectorHelper.IsBase(obj.Sector)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;
            if (!IsOwnedDefensibleSector(info, team))
            {
                Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense skip {obj.Sector}: stale defense owner={info.ControllingTeam}");
                continue;
            }
            BuildSectorDefenseOp(team, snapshot, obj, info, ops, built, FindIntelForSector(intel, info.Sector));
        }

        foreach (SectorManager.SectorInfo info in SectorManager.GetAllSectorInfos())
        {
            if (info == null || ConstructionSectorHelper.IsBase(info.Sector)) continue;
            if (built.Contains(info.Sector)) continue;
            if (!IsOwnedDefensibleSector(info, team)) continue;
            AISectorIntel sectorIntel = FindIntelForSector(intel, info.Sector);
            if (!HasNearbyVisibleEnemy(snapshot, info.RepresentativeCell, SectorDefenseRange) && !IsHotIntelSector(sectorIntel)) continue;
            BuildSectorDefenseOp(team, snapshot, plan.GetObjectiveForSector(info.Sector), info, ops, built, sectorIntel);
        }
    }

    private void TryBuildGroundCaptureOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null || snapshot == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;
            if (obj.Status != ObjectiveStatus.Pending
                && obj.Status != ObjectiveStatus.Pursuing
                && obj.Status != ObjectiveStatus.Capturing
                && obj.Status != ObjectiveStatus.PartialReadyForHandoff)
                continue;
            // Base-inclusive: a base inimiga (objetivo de invasão) NÃO está em TryGetSectorInfo —
            // vive em GetAllBaseInfos. Sem isto o objetivo ">>" era pulado aqui e nunca gerava
            // demanda de transporte operacional (o eixo 4 ficava trans=0).
            if (!TryGetOpsSectorInfo(obj.Sector, out SectorManager.SectorInfo info))
                continue;
            bool isInvasion = obj.ObjectiveType == AIObjectiveType.InvasionAttack;
            SectorManager.SectorInfo.TransportPreference transportPref = info.GetTransportPreference(team);
            // Invasão cruza o mapa por terra até o QG inimigo: não cede o transporte ao airlift.
            if (!isInvasion
                && HasAnySlot(obj, UnitRole.Transportador)
                && transportPref == SectorManager.SectorInfo.TransportPreference.Air)
                continue;

            int capturers = CountSlots(obj, UnitRole.Capturador);
            int assaults = CountSlots(obj, UnitRole.Assalto);
            int fireSupport = CountSlots(obj, UnitRole.FogoIndireto);
            // Transporte terrestre só quando o setor prefere veículo; "Either" e "Air" ficam
            // com o airlift (helicóptero). A demanda é por NECESSIDADE DE CARONA, não por
            // distância do setor — ver ComputeGroundTransportNeed. Invasão tem via própria
            // (profundidade pela geometria do eixo), pois a base não tem dist-de-HQ confiável.
            int groundTransports = obj.ObjectiveType == AIObjectiveType.RallyAssembly
                ? CountSlots(obj, UnitRole.Transportador)
                : isInvasion
                    ? ComputeInvasionGroundTransportNeed(team, snapshot, obj)
                    : transportPref == SectorManager.SectorInfo.TransportPreference.Vehicle
                        ? ComputeGroundTransportNeed(team, snapshot, obj)
                        : 0;
            AISectorIntel sectorIntel = FindIntelForSector(intel, obj.Sector);
            bool risky = info.GetRiskLevelFor(team) >= SectorManager.SectorRiskLevel.Medium || IsHotIntelSector(sectorIntel);
            bool hasBasicTaskForce = capturers > 0 && (assaults > 0 || fireSupport > 0);
            bool needsAssaultScreen = risky && !hasBasicTaskForce;

            AITacticalNeed op = CreateOperation(team, AITacticalNeedType.GroundCapture, GetCaptureOperationPriority(obj), snapshot, obj.Sector);
            op.LinkedObjective = obj;
            op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
            op.TargetCell = Normalize(info.RepresentativeCell);
            op.AddSlots(AINeedKind.Capturer, Mathf.Max(0, capturers));
            op.AddSlots(AINeedKind.Assault, Mathf.Max(assaults, needsAssaultScreen ? 1 : 0));
            op.AddSlots(AINeedKind.FireSupport, Mathf.Max(0, fireSupport));
            op.AddSlots(AINeedKind.GroundTransport, Mathf.Max(0, groundTransports));

            if (op.RequiredSlots.Count == 0)
                continue;

            ops.Add(op);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] GroundCapture {obj.Sector}: cap={capturers} ass={assaults} fire={fireSupport} trans={groundTransports} pref={transportPref} risky={risky} slots={DescribeSlots(op)}");
        }
    }

    // Distância de embarque = 2 turnos (~7 hexes) por caminhos válidos. A massa mínima de
    // capturadores antes de liberar suporte vem do knob compartilhado AIShoppingPlanner.
    private const int GroundTransportEmbarkDistance = 7;

    // Eixo de invasão (HQ -> QG inimigo): a massa final cruza o mapa todo, então não usa o teto
    // 1/eixo dos eixos rally. Demanda escalada com a profundidade (APCs no pipeline), com piso/teto.
    // Mantém em sincronia com a inspeção em AIShoppingPlanner.OperationalPressure.
    internal const float InvasionTransportDepthPerApc = 6f;
    internal const int InvasionMinTransports = 2;
    internal const int InvasionMaxTransports = 4;

    internal static int ComputeInvasionTransportDesired(float depth)
    {
        if (depth < GroundTransportEmbarkDistance)
            return 0;
        return Mathf.Clamp(
            Mathf.CeilToInt(depth / InvasionTransportDepthPerApc),
            InvasionMinTransports, InvasionMaxTransports);
    }

    // Demanda de APC POR EIXO, escalada pela PROFUNDIDADE DA FRENTE (R4). Paradigma:
    // o transporte é o único papel antecipatório/posicional — prepara o terreno que ainda
    // vai ser tomado, não reage a um capturador específico já longe. Intuição-norte:
    //   frente rasa (eixo recém-saído do HQ) → a pé resolve, pressão ZERO;
    //   frente profunda (vários setores segurados atrás) → o próximo capturador nasce no HQ
    //     e tem de cruzar tudo → 1 APC.
    // Estrutura:
    //  - a demanda nasce SÓ no ALVO DE TRANSPORTE do eixo (a frente, ou o próximo nó se a
    //    frente já está sob captura: pipelining do corredor) → teto 1/eixo natural;
    //  - gateada por massa mínima de capturadores e por profundidade DO ALVO >= limiar de
    //    embarque (frente perta sob captura → próximo nó também raso → a pé resolve);
    //  - desconta APCs terrestres já alocados ao eixo (presença por aiEixo).
    private int ComputeGroundTransportNeed(TeamId team, AIWorldSnapshot snapshot, SectorObjective obj)
    {
        if (obj == null || obj.Slots == null)
            return 0;
        int massGate = AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinCapturerMassForSupport : 4;
        if (CountActiveNeed(snapshot, AINeedKind.Capturer) < massGate)
            return 0;

        InvasionAxisMap axisMap = GetShoppingAxisMap(team);
        int eixo = axisMap != null ? axisMap.GetEixo(obj.Sector) : 0;
        // Fora de qualquer eixo (rogue/base/fora de alcance): sem pressão antecipatória.
        if (eixo <= 0 || axisMap == null)
            return 0;
        // Teto 1/eixo: só o ALVO DE TRANSPORTE do eixo gera demanda — a frente, ou o próximo
        // nó se a frente já está sob captura (pipelining). Eixo completo não tem alvo.
        ConstructionSector target = axisMap.GetTransportTargetSector(eixo);
        if (target == ConstructionSector.None || obj.Sector != target)
            return 0;

        // Profundidade do ALVO = quão longe o próximo capturador (nascido no HQ) terá de
        // cruzar. Alvo raso → a pé resolve (pressão zero). Alvo profundo → 1 APC. Medir no
        // alvo garante que pipelinar uma frente perto (dentro dos ~7h) não gere demanda.
        if (!SectorManager.TryGetSectorInfo(target, out SectorManager.SectorInfo frontInfo))
            return 0;
        float depth = frontInfo.GetDistanceToHQ(team);
        if (depth < GroundTransportEmbarkDistance)
            return 0;

        // Teto 1/eixo: desconta APCs terrestres já alocados a este eixo.
        int assigned = CountGroundTransportsOnEixo(snapshot, obj, eixo);
        return Mathf.Max(0, 1 - assigned);
    }

    // Demanda de transporte do EIXO DE INVASÃO (">>"). A base inimiga NÃO está em TryGetSectorInfo
    // (vive em GetAllBaseInfos) e não tem distância-de-HQ confiável, então: profundidade pela
    // GEOMETRIA do eixo (HQ -> célula da base, sempre disponível). Escala com a profundidade (não
    // é teto 1/eixo) e desconta APCs já no eixo.
    private int ComputeInvasionGroundTransportNeed(TeamId team, AIWorldSnapshot snapshot, SectorObjective obj)
    {
        // O eixo 4 pode existir antes para persistencia/inspecao, mas a frota so nasce
        // quando a operacao GoGreen esta realmente em andamento.
        if (obj == null || snapshot == null || !snapshot.IsInvading)
            return 0;

        InvasionAxisMap axisMap = GetShoppingAxisMap(team);
        int eixo = axisMap != null ? axisMap.GetEixo(obj.Sector) : 0;
        if (eixo <= 0 || axisMap == null || !axisMap.TryGetAxis(eixo, out InvasionAxisMap.Axis axis))
            return 0;

        float depth = SectorManager.HexDistance(axis.HqCell, axis.RallyCell);
        int assigned = CountGroundTransportsOnEixo(snapshot, obj, eixo);
        return Mathf.Max(0, ComputeInvasionTransportDesired(depth) - assigned);
    }

    // Info do setor incluindo BASES. TryGetSectorInfo cobre só os setores de campo; a base inimiga
    // (alvo de invasão) vive em GetAllBaseInfos e precisa do fallback TryGetBaseInfo.
    private static bool TryGetOpsSectorInfo(ConstructionSector sector, out SectorManager.SectorInfo info)
    {
        if (SectorManager.TryGetSectorInfo(sector, out info))
            return true;
        return SectorManager.TryGetBaseInfo(sector, out info);
    }

    // Conta APCs terrestres já comprometidos com o eixo: presença por aiEixo (memória que
    // persiste entre objetivos) + os recém-atribuídos ao objetivo da frente (que podem ainda
    // não ter o aiEixo deste turno). Dedupe por InstanceId.
    private int CountGroundTransportsOnEixo(AIWorldSnapshot snapshot, SectorObjective obj, int eixo)
    {
        var counted = new HashSet<int>();
        if (snapshot?.MyUnits != null)
        {
            foreach (UnitManager u in snapshot.MyUnits)
            {
                if (!UnitSatisfiesNeed(u, AINeedKind.GroundTransport))
                    continue;
                if (u.AIEixo == eixo)
                    counted.Add(u.InstanceId);
            }
        }
        if (obj?.Slots != null)
        {
            foreach (SlotNeed slot in obj.Slots)
            {
                if (!slot.Filled)
                    continue;
                UnitManager u = FindActiveUnit(slot.AssignedUnitId);
                if (UnitSatisfiesNeed(u, AINeedKind.GroundTransport))
                    counted.Add(u.InstanceId);
            }
        }
        return counted.Count;
    }

    private void BuildSectorDefenseOp(
        TeamId team,
        AIWorldSnapshot snapshot,
        SectorObjective linked,
        SectorManager.SectorInfo info,
        List<AITacticalNeed> ops,
        HashSet<ConstructionSector> built,
        AISectorIntel sectorIntel = null)
    {
        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.SectorDefense, 2, snapshot, info.Sector);
        op.LinkedObjective = linked;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = Normalize(info.RepresentativeCell);

        bool visibleGroundThreat = HasGroundEnemyNearCell(snapshot, info.RepresentativeCell, SectorDefenseRange);
        bool visibleAirThreat = CountVisibleEnemyAircraftNearCell(snapshot, info.RepresentativeCell, 2) > 0;

        if (info.HasPartialCapture)
            op.AddSlots(AINeedKind.Capturer, 1);
        if (visibleGroundThreat && sectorIntel != null && sectorIntel.capturePressure > 0f)
            op.AddSlots(AINeedKind.Capturer, 1);
        if (visibleGroundThreat)
            op.AddSlots(AINeedKind.Assault, 1);
        if (visibleGroundThreat && sectorIntel != null && (sectorIntel.enemyPresence >= 2f || sectorIntel.landingPressure > 0f))
            op.AddSlots(AINeedKind.Assault, 1);
        if (visibleGroundThreat && info.GetDistanceToHQ(team) <= GetEffectiveTransportThreshold(team))
            op.AddSlots(AINeedKind.Artillery, 1);
        else if (visibleGroundThreat && sectorIntel != null && sectorIntel.damageTaken > 0f)
            op.AddSlots(AINeedKind.Artillery, 1);
        if (visibleAirThreat)
            op.AddSlots(AINeedKind.AAA, 1);

        if (op.RequiredSlots.Count == 0)
        {
            built.Add(info.Sector);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense {info.Sector}: sem demanda atual visibleGround={visibleGroundThreat} visibleAir={visibleAirThreat} intelHot={(sectorIntel != null ? sectorIntel.hotScore.ToString("F1") : "-")}");
            return;
        }

        ops.Add(op);
        built.Add(info.Sector);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] SectorDefense {info.Sector}: partial={info.HasPartialCapture} ground={visibleGroundThreat} air={visibleAirThreat} intelHot={(sectorIntel != null ? sectorIntel.hotScore.ToString("F1") : "-")} slots={DescribeSlots(op)}");
    }

    private void TryBuildAirliftCaptureOps(TeamId team, AIWorldSnapshot snapshot, TeamObjectivePlan plan, List<AITacticalNeed> ops, AIIntelReport intel)
    {
        if (plan == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null) continue;
            // Rally usa APC terrestre como shuttle da infantaria open-bar. Nao converte essa
            // necessidade em Chinook so porque o setor tambem aceita transporte aereo.
            if (obj.ObjectiveType == AIObjectiveType.RallyAssembly) continue;
            if (obj.Status != ObjectiveStatus.Pending
                && obj.Status != ObjectiveStatus.Pursuing
                && obj.Status != ObjectiveStatus.Capturing)
                continue;
            if (!HasAnySlot(obj, UnitRole.Transportador)) continue;
            if (!SectorManager.TryGetSectorInfo(obj.Sector, out SectorManager.SectorInfo info)) continue;
            // Setores que preferem veículo recebem APC pelo GroundCapture; evita demanda dupla
            // (APC terrestre + helicóptero) para o mesmo objetivo.
            if (info.GetTransportPreference(team) == SectorManager.SectorInfo.TransportPreference.Vehicle)
                continue;

            int desiredPassengers = CountSlots(obj, UnitRole.Capturador);
            int qualifiedCapturers = CountFilledCompatibleSlots(obj, AINeedKind.Capturer);
            int assignedChinooks = CountFilledCompatibleSlots(obj, AINeedKind.AirTransport);
            int embarkedCapturers = CountEmbarkedCapturersForObjective(snapshot, obj);
            int capturerDeficit = Mathf.Max(0, desiredPassengers - qualifiedCapturers - embarkedCapturers);
            int neededChinooks = Mathf.CeilToInt(desiredPassengers / 2f);
            int airDeficit = Mathf.Max(0, neededChinooks - assignedChinooks);

            AITacticalNeed op = CreateOperation(team, AITacticalNeedType.AirliftCapture, GetCaptureOperationPriority(obj), snapshot, obj.Sector);
            op.LinkedObjective = obj;
            op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
            op.TargetCell = Normalize(info.RepresentativeCell);
            AISectorIntel sectorIntel = FindIntelForSector(intel, obj.Sector);
            op.AddSlots(AINeedKind.Capturer, capturerDeficit);
            op.AddSlots(AINeedKind.AirTransport, airDeficit);
            if (info.GetRiskLevelFor(team) >= SectorManager.SectorRiskLevel.High || IsHotIntelSector(sectorIntel))
                op.AddSlots(AINeedKind.Assault, 1);
            if (sectorIntel != null && sectorIntel.damageTaken > 0f)
                op.AddSlots(AINeedKind.Artillery, 1);
            if (CountTotalVisibleEnemyFighterA(snapshot) > 0)
                op.AddSlots(AINeedKind.FighterA, CountActiveNeed(snapshot, AINeedKind.FighterA) <= 0 ? 1 : 0);
            else if (CountTotalVisibleEnemyAircraft(snapshot) > 0
                && CountActiveNeed(snapshot, AINeedKind.FighterA) + CountActiveNeed(snapshot, AINeedKind.FighterB) <= 0)
                op.AddSlots(AINeedKind.FighterB, 1);

            if (op.RequiredSlots.Count == 0)
                continue;

            ops.Add(op);
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirliftCapture {obj.Sector}: desejado={desiredPassengers} qualified={qualifiedCapturers} embarked={embarkedCapturers} cap_deficit={capturerDeficit} air_deficit={airDeficit} intelHot={(sectorIntel != null ? sectorIntel.hotScore.ToString("F1") : "-")}");
        }
    }

    private void TryBuildPreventiveDefenseOp(TeamId team, AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        if (snapshot == null)
            return;

        int minTurn = AIShoppingPlanner.Instance != null ? AIShoppingPlanner.Instance.MinTurnBaseDefense : 3;
        bool rich = HasPreventiveDefenseBudget(snapshot);
        if (snapshot.TurnNumber > 0 && snapshot.TurnNumber < minTurn && !rich)
            return;

        int activeArt = CountActiveNeed(snapshot, AINeedKind.Artillery) + CountActiveNeed(snapshot, AINeedKind.FireSupport);
        int activeAAA = CountActiveNeed(snapshot, AINeedKind.AAA);
        int activeSAM = CountActiveNeed(snapshot, AINeedKind.SAM);
        int aircraftNearHQ = CountVisibleEnemyAircraftNearHQ(snapshot, InstanceSafeAntiAirCoverageRange());

        int artDeficit = Mathf.Max(0, InstanceMinBaseArtillery() - activeArt);
        int aaaDeficit = aircraftNearHQ > 0 ? Mathf.Max(0, InstanceMinBaseAAA() - activeAAA) : 0;
        int samDeficit = aircraftNearHQ > 0 && activeAAA >= 1 && activeSAM < 1 ? 1 : 0;
        if (artDeficit <= 0 && aaaDeficit <= 0 && samDeficit <= 0)
            return;

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.PreventiveDefense, 6, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.IsPreventive = true;
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.Artillery, artDeficit);
        op.AddSlots(AINeedKind.AAA, aaaDeficit);
        op.AddSlots(AINeedKind.SAM, samDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] PreventiveDefense: Artilleryx{artDeficit} AAAx{aaaDeficit} SAMx{samDeficit} aircraftNearHQ={aircraftNearHQ} activeArt={activeArt} activeAAA={activeAAA} activeSAM={activeSAM}");
    }

    private void TryBuildAirRefuelSupportOp(TeamId team, AIWorldSnapshot snapshot, List<AITacticalNeed> ops)
    {
        if (snapshot?.MyUnits == null)
            return;

        int lowFuelAircraft = 0;
        int criticalFuelAircraft = 0;
        int airFleet = 0;

        foreach (UnitManager unit in snapshot.MyUnits)
        {
            if (unit == null || unit.IsDead || unit.IsEmbarked) continue;
            if (!unit.TryGetUnitData(out UnitData data) || data == null || data.domain != Domain.Air) continue;
            if (UnitDataSatisfiesNeed(data, AINeedKind.AirTanker)) continue;

            airFleet++;
            int maxFuel = Mathf.Max(1, unit.GetMaxFuel());
            float fuelPct = unit.CurrentFuel * 100f / maxFuel;
            if (fuelPct <= AirRefuelLowFuelPct) lowFuelAircraft++;
            if (fuelPct <= AirRefuelCriticalFuelPct) criticalFuelAircraft++;
        }

        if (lowFuelAircraft <= 0)
            return;

        int desiredTankers = 1;
        if ((airFleet >= 8 && lowFuelAircraft >= 4) || criticalFuelAircraft >= 2)
            desiredTankers = 2;

        int activeTankers = CountActiveNeed(snapshot, AINeedKind.AirTanker);
        int tankerDeficit = Mathf.Max(0, desiredTankers - activeTankers);
        if (tankerDeficit <= 0)
        {
            Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport coberto: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} activeTankers={activeTankers}/{desiredTankers}");
            return;
        }

        AITacticalNeed op = CreateOperation(team, AITacticalNeedType.AirRefuelSupport, 3, snapshot, snapshot.MyHQ != null ? snapshot.MyHQ.Sector : ConstructionSector.Base1);
        op.AnchorCell = snapshot.MyHQ != null ? Normalize(snapshot.MyHQ.CurrentCellPosition) : Vector3Int.zero;
        op.TargetCell = op.AnchorCell;
        op.AddSlots(AINeedKind.AirTanker, tankerDeficit);

        ops.Add(op);
        Debug.Log($"[AI Ops][T{snapshot.TurnNumber}][{team}] AirRefuelSupport: lowFuel={lowFuelAircraft} critical={criticalFuelAircraft} airFleet={airFleet} activeTankers={activeTankers} deficit={tankerDeficit}");
    }
}
