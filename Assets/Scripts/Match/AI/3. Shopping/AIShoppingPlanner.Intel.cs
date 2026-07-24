using System.Collections.Generic;
using UnityEngine;

// Coleta e aplica inteligência de jogadas ao planejamento de compras.
public partial class AIShoppingPlanner
{
    private static AIIntelReport BuildShoppingIntelReport(AIWorldSnapshot snapshot)
    {
        if (snapshot == null || Instance == null || !Instance.usarIntelJogadasNoShopping)
            return null;

        JogadasManager jogadas = JogadasManager.EnsureInstance();
        IReadOnlyCollection<AIIntelContact> contacts =
            AIIntelLedger.UpdateAndGetContacts(snapshot);
        var observableLog = new JogadasLog();
        if (jogadas?.log?.jogadas != null)
        foreach (Jogada play in jogadas.log.jogadas)
        {
            if (play == null)
                continue;
            bool ownAction = play.team == (int)snapshot.AITeam;
            bool enemyCombatAgainstUs = play.hasCombatResult
                && play.team2 == (int)snapshot.AITeam;
            if (ownAction)
                observableLog.jogadas.Add(play);
            else if (enemyCombatAgainstUs)
                observableLog.jogadas.Add(play.attackerVisibleToDefender
                    ? play
                    : BuildSanitizedHiddenAttack(play));
        }

        int lookback = Mathf.Max(1, Instance.IntelShoppingLookbackTurns);
        AIIntelReport report = AIIntelAnalyzer.BuildReport(
            observableLog, snapshot.AITeam, lookback, 5, snapshot.TurnNumber);
        ApplySubjectiveLedgerComposition(report, contacts, snapshot.TurnNumber, lookback);
        return report;
    }

    private static Jogada BuildSanitizedHiddenAttack(Jogada source)
    {
        return new Jogada
        {
            jogadaId = source.jogadaId,
            turno = source.turno,
            team = source.team,
            team2 = source.team2,
            acao = source.acao,
            cx = source.dx,
            cy = source.dy,
            dx = source.dx,
            dy = source.dy,
            unidadeSigla = "?",
            uid = 0,
            unidadeSigla2 = source.unidadeSigla2,
            uid2 = source.uid2,
            hasCombatResult = source.hasCombatResult,
            hpAntes = source.hpAntes,
            hpDepois = source.hpDepois,
            hp2Antes = source.hp2Antes,
            hp2Depois = source.hp2Depois,
            hasAttackIntel = source.hasAttackIntel,
            attackWeaponCategory = source.attackWeaponCategory,
            attackTrajectory = source.attackTrajectory,
            attackerVisibleToDefender = false,
            defenderCost = source.defenderCost,
            defenderEliteLevel = source.defenderEliteLevel,
            combatCargo = source.combatCargo,
            obs = "ataque de origem desconhecida",
        };
    }

    private static void ApplySubjectiveLedgerComposition(
        AIIntelReport report,
        IReadOnlyCollection<AIIntelContact> contacts,
        int currentTurn,
        int lookback)
    {
        if (report == null)
            return;

        report.enemyPurchases.Clear();
        report.enemyPurchasesRecent = 0;
        report.enemyElitePurchaseScore = 0f;
        report.enemyAirThreatScore = 0f;
        report.enemyArmorThreatScore = 0f;
        report.enemyArtilleryThreatScore = 0f;
        report.enemyInfantryPressureScore = 0f;
        report.enemyInfantryForce = 0f;
        report.enemyArmorForce = 0f;
        report.enemyAirForce = 0f;
        report.enemyNavalForce = 0f;
        report.enemyLastKnownUnits.Clear();

        if (contacts == null)
            return;
        foreach (AIIntelContact contact in contacts)
        {
            if (contact == null || contact.destroyed)
                continue;
            int age = Mathf.Max(0, currentTurn - contact.lastSeenTurn);
            if (age >= lookback)
                continue;
            UnitData data = ResolveUnitDataBySigla(contact.sigla);
            if (data == null)
                continue;
            float recency = Mathf.Lerp(0.35f, 1f, 1f - age / (float)lookback);
            float weight = recency * Mathf.Clamp01(contact.confidence)
                * (1f + Mathf.Max(0, data.eliteLevel) * 0.5f);

            report.enemyLastKnownUnits.Add(new AIUnitIntel
            {
                uid = contact.uid,
                team = contact.enemyTeam,
                sigla = contact.sigla,
                lastAction = contact.source,
                lastSeenTurn = contact.lastSeenTurn,
                lastKnownCell = contact.lastKnownCell,
                confidence = contact.confidence,
                destroyed = contact.destroyed,
                recentDamageDealt = contact.recentDamageDealt,
                recentKills = contact.recentKills,
                recentDestroyedValue = contact.recentDestroyedValue,
            });

            switch (data.unitClass)
            {
                case GameUnitClass.Infantry:
                    report.enemyInfantryForce += weight;
                    report.enemyInfantryPressureScore += weight;
                    break;
                case GameUnitClass.Vehicle:
                case GameUnitClass.Armored:
                    report.enemyArmorForce += weight;
                    report.enemyArmorThreatScore += weight;
                    break;
                case GameUnitClass.Artillery:
                    report.enemyArmorForce += weight;
                    report.enemyArtilleryThreatScore += weight;
                    break;
                case GameUnitClass.Jet:
                case GameUnitClass.Helicopter:
                case GameUnitClass.Plane:
                    report.enemyAirForce += weight;
                    report.enemyAirThreatScore += weight;
                    break;
                case GameUnitClass.Ship:
                case GameUnitClass.Submarine:
                    report.enemyNavalForce += weight;
                    break;
            }
        }
        report.enemyKnownUnits = report.enemyLastKnownUnits.Count;
    }

    private static void ApplyJogadasIntelBias(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        ref int openAssaultSlots,
        ref int openFireSupportSlots,
        ref int openCacaBSlots,
        ref bool proactiveAntiAir,
        ref bool preferDefensiveFireSupport,
        ref bool proactiveDefFireSupport,
        ref bool intelArmorThreat,
        int activeAAAs,
        int activeSAMs)
    {
        if (snapshot == null || intel == null || Instance == null || !Instance.usarIntelJogadasNoShopping)
            return;

        bool changed = false;
        float infantryPressure = intel.enemyInfantryPressureScore;
        float numericalPressure = intel.numericalPressure;
        float airThreat = intel.enemyAirThreatScore;
        float armorThreat = intel.enemyArmorThreatScore;
        float captureThreat = Mathf.Max(intel.capturePressure, intel.landingPressure, intel.damageTakenScore);
        float stalemateElitePressure = intel.stalemateElitePressure;
        AISectorIntel topSectorIntel = intel.sectors != null && intel.sectors.Count > 0 ? intel.sectors[0] : null;
        string topSector = topSectorIntel != null ? topSectorIntel.sector : "-";
        float topHot = topSectorIntel != null ? topSectorIntel.hotScore : 0f;
        float topEnemyActivity = topSectorIntel != null ? topSectorIntel.enemyActivity : 0f;
        float topDamageTaken = topSectorIntel != null ? topSectorIntel.damageTaken : 0f;
        float topCapturePressure = topSectorIntel != null ? topSectorIntel.capturePressure : 0f;

        if (infantryPressure >= Instance.IntelInfantryPressureAssaultThreshold ||
            numericalPressure >= Instance.IntelNumericalPressureThreshold)
        {
            int desiredAssault = (infantryPressure >= Instance.IntelInfantryPressureAssaultThreshold * 2f ||
                                  numericalPressure >= Instance.IntelNumericalPressureThreshold * 2f)
                ? 2 : 1;
            int before = openAssaultSlots;
            openAssaultSlots = Mathf.Max(openAssaultSlots, desiredAssault);
            changed |= openAssaultSlots != before;
        }

        if (captureThreat >= Instance.IntelCapturePressureDefenseThreshold)
        {
            int beforeAssault = openAssaultSlots;
            int beforeFire = openFireSupportSlots;
            openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            if (captureThreat >= Instance.IntelCapturePressureDefenseThreshold * 2f)
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = openFireSupportSlots > beforeFire || preferDefensiveFireSupport;
            proactiveDefFireSupport = openFireSupportSlots > beforeFire || proactiveDefFireSupport;
            changed |= openAssaultSlots != beforeAssault || openFireSupportSlots != beforeFire;
        }

        bool noFireSupport = intel.friendlyFireSupportCount <= 0;
        bool hotSectorNeedsFire = topHot >= Instance.IntelFireSupportGapHotThreshold
            && (topEnemyActivity > 0f || topDamageTaken > 0f || topCapturePressure > 0f);
        bool damageNeedsFire = captureThreat >= Instance.IntelFireSupportGapDamageThreshold;
        bool artilleryNeedsFire = intel.enemyArtilleryThreatScore > 0f;
        if (noFireSupport && (hotSectorNeedsFire || damageNeedsFire || artilleryNeedsFire))
        {
            int beforeFire = openFireSupportSlots;
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            if (intel.enemyArtilleryThreatScore > 0f || intel.damageTakenScore >= Instance.IntelFireSupportGapDamageThreshold)
            {
                preferDefensiveFireSupport = true;
                proactiveDefFireSupport = true;
            }
            changed |= openFireSupportSlots != beforeFire;
            Debug.Log($"[AI Shopping][IntelGap] sem_fogo_indireto team={snapshot.AITeam} top={topSector} hot={topHot:F1} enemy={topEnemyActivity:F1} dmg={intel.damageTakenScore:F1}/{topDamageTaken:F1} capture={intel.capturePressure:F1}/{topCapturePressure:F1} enemyArt={intel.enemyArtilleryThreatScore:F1} -> fire={openFireSupportSlots} fireDef={preferDefensiveFireSupport}");
        }

        if (stalemateElitePressure >= Instance.IntelStalemateElitePressureThreshold)
        {
            int beforeAssault = openAssaultSlots;
            int beforeFire = openFireSupportSlots;
            openAssaultSlots = Mathf.Max(openAssaultSlots, 1);

            bool heavyStalemate = stalemateElitePressure >= Instance.IntelStalemateFireSupportThreshold;
            if (heavyStalemate)
            {
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                if (!proactiveAntiAir && armorThreat <= 0f)
                    preferDefensiveFireSupport = false;
            }

            changed |= openAssaultSlots != beforeAssault || openFireSupportSlots != beforeFire;
            Debug.Log($"[AI Shopping][Stalemate] team={snapshot.AITeam} setor={intel.topStalemateSector} score={intel.stalemateScore:F1} elitePressure={stalemateElitePressure:F1} heavy={heavyStalemate} -> ass={openAssaultSlots} fire={openFireSupportSlots} fireDef={preferDefensiveFireSupport}");
        }

        if (airThreat >= Instance.IntelAirThreatAntiAirThreshold)
        {
            bool localAirThreat = CountVisibleEnemyAircraftNearHQ(snapshot, Instance.AntiAirCoverageRange) > 0;
            int beforeAssault = openAssaultSlots;
            int beforeFire = openFireSupportSlots;
            int beforeCacaB = openCacaBSlots;
            if (localAirThreat)
            {
                proactiveAntiAir = true;
                openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            }
            openCacaBSlots = Mathf.Max(openCacaBSlots, 1);
            if (localAirThreat && activeAAAs >= 1 && activeSAMs < 1)
            {
                openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
                preferDefensiveFireSupport = true;
            }
            changed |= openAssaultSlots != beforeAssault || openFireSupportSlots != beforeFire || openCacaBSlots != beforeCacaB;
        }

        if (armorThreat >= Instance.IntelArmorThreatDefenseThreshold)
        {
            int beforeAssault = openAssaultSlots;
            int beforeFire = openFireSupportSlots;
            intelArmorThreat = true;
            openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            openFireSupportSlots = Mathf.Max(openFireSupportSlots, 1);
            preferDefensiveFireSupport = true;
            proactiveDefFireSupport = true;
            changed |= openAssaultSlots != beforeAssault || openFireSupportSlots != beforeFire;
        }

        if (changed)
        {
            Debug.Log($"[AI Shopping][Intel] team={snapshot.AITeam} top={topSector} infantry={infantryPressure:F1} num={numericalPressure:F1} air={airThreat:F1} armor={armorThreat:F1} capture={captureThreat:F1} stalemate={stalemateElitePressure:F1} -> ass={openAssaultSlots} fire={openFireSupportSlots} cacaB={openCacaBSlots} antiAir={proactiveAntiAir}");
        }
    }

    private static AISectorIntel FindIntelSector(AIIntelReport intel, ConstructionSector sector)
    {
        if (intel == null || intel.sectors == null)
            return null;

        string sectorName = sector.ToString();
        for (int i = 0; i < intel.sectors.Count; i++)
        {
            AISectorIntel item = intel.sectors[i];
            if (item == null || string.IsNullOrEmpty(item.sector))
                continue;
            if (string.Equals(item.sector, sectorName, System.StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
    }

    private static bool HasStalemateCapturerCommitment(
        AIWorldSnapshot snapshot,
        AIIntelReport intel,
        out string reason)
    {
        reason = "-";
        if (snapshot == null || intel == null || Instance == null)
            return false;
        if (intel.stalemateElitePressure < Instance.IntelStalemateElitePressureThreshold)
            return false;
        if (string.IsNullOrWhiteSpace(intel.topStalemateSector)
            || !System.Enum.TryParse(intel.topStalemateSector, out ConstructionSector sector)
            || sector == ConstructionSector.None)
            return false;

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        SectorObjective objective = plan != null ? plan.GetObjectiveForSector(sector) : null;
        if (objective != null && objective.Slots != null)
        {
            for (int i = 0; i < objective.Slots.Count; i++)
            {
                SlotNeed slot = objective.Slots[i];
                if (slot == null || slot.Role != UnitRole.Capturador || !slot.Filled)
                    continue;

                reason = $"{sector}:slot Unit{slot.AssignedUnitId}";
                return true;
            }
        }

        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info) || info == null)
            return false;

        Vector3Int targetCell = info.RepresentativeCell;
        targetCell.z = 0;
        int maxRange = Mathf.Max(1, Instance.StalemateEliteCapturerRange);
        UnitManager best = null;
        float bestDist = float.MaxValue;

        if (snapshot.MyUnits != null)
        {
            for (int i = 0; i < snapshot.MyUnits.Count; i++)
            {
                UnitManager unit = snapshot.MyUnits[i];
                if (unit == null || unit.IsDead || unit.IsEmbarked)
                    continue;
                if (!unit.TryGetUnitData(out UnitData data) || data == null
                    || data.roles == null || !data.roles.Contains(UnitRole.Capturador))
                    continue;

                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                float dist = SectorManager.HexDistance(cell, targetCell);
                if (dist > maxRange || dist >= bestDist)
                    continue;

                best = unit;
                bestDist = dist;
            }
        }

        if (best == null)
            return false;

        reason = $"{sector}:near Unit{best.InstanceId} {bestDist:F0}h";
        return true;
    }

    private static bool IsBaseDefenseHotIntelSector(AISectorIntel intel)
    {
        if (intel == null)
            return false;

        return intel.enemyPresence >= 2f
            || intel.capturePressure > 0f
            || intel.landingPressure > 0f
            || intel.damageTaken > 0f
            || intel.hotScore >= 5f;
    }
}
