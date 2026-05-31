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
        if (jogadas == null || jogadas.log == null || jogadas.log.jogadas == null || jogadas.log.jogadas.Count == 0)
            return null;

        int lookback = Mathf.Max(1, Instance.IntelShoppingLookbackTurns);
        return AIIntelAnalyzer.BuildReport(jogadas.log, snapshot.AITeam, lookback, 5, snapshot.TurnNumber);
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

        if (airThreat >= Instance.IntelAirThreatAntiAirThreshold)
        {
            int beforeAssault = openAssaultSlots;
            int beforeFire = openFireSupportSlots;
            int beforeCacaB = openCacaBSlots;
            proactiveAntiAir = true;
            openAssaultSlots = Mathf.Max(openAssaultSlots, 1);
            openCacaBSlots = Mathf.Max(openCacaBSlots, 1);
            if (activeAAAs >= 1 && activeSAMs < 1)
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
            Debug.Log($"[AI Shopping][Intel] team={snapshot.AITeam} top={topSector} infantry={infantryPressure:F1} num={numericalPressure:F1} air={airThreat:F1} armor={armorThreat:F1} capture={captureThreat:F1} -> ass={openAssaultSlots} fire={openFireSupportSlots} cacaB={openCacaBSlots} antiAir={proactiveAntiAir}");
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
