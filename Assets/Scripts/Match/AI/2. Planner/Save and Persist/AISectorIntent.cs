using System;
using System.Collections.Generic;
using UnityEngine;

// --------------------------------------------------------------------------------------------
// Analisa a situação tática de cada setor do mapa, classificando-os em termos de
// intenção de controle (manter, atacar, defender, evitar, etc.) e relação estratégica
// (base própria, base inimiga, natural, centro neutro, etc.). Essa análise
// é baseada em múltiplas fontes de informação, incluindo o plano de objetivos da equipe,
// o relatório de inteligência de campo e o estado atual do mapa. O resultado é um conjunto
// de intenções de setor que guiam as decisões táticas do AI, como onde alocar unidades,
// onde esperar resistência, onde priorizar ataques, etc.
// O processo é reavaliadodo a cada turno, garantindo que o AI se adapte às mudanças na situação
// do jogo e às novas informações de inteligência.
// -------------------------------------------------------------------------------------------------

public enum AISectorIntentKind
{
    Hold,
    Attack,
    Defend,
    Avoid,
    Intercept,
    Secure,
    Covered,
}

public enum AISectorRelation
{
    Unknown,
    OwnBase,
    EnemyBase,
    OwnNatural,
    EnemyNatural,
    NeutralCenter,
    Owned,
    EnemyOwned,
}

[Serializable]
public class AISectorIntent
{
    public TeamId Team;
    public ConstructionSector Sector;
    public AISectorIntentKind Kind;
    public AISectorRelation Relation;
    public float Confidence;
    public float HotScore;
    public string Reason = "";
    public string Source = "";
}

public static class AISectorIntentAnalyzer
{
    private static readonly Dictionary<int, Dictionary<ConstructionSector, AISectorIntent>> intentsBySlot =
        new Dictionary<int, Dictionary<ConstructionSector, AISectorIntent>>();

    public static IReadOnlyDictionary<ConstructionSector, AISectorIntent> GetIntents(PlayerSlotId slotId)
    {
        if (intentsBySlot.TryGetValue(slotId.Value, out Dictionary<ConstructionSector, AISectorIntent> intents))
            return intents;
        return null;
    }

    public static bool TryGetIntent(PlayerSlotId slotId, ConstructionSector sector, out AISectorIntent intent)
    {
        intent = null;
        return intentsBySlot.TryGetValue(slotId.Value, out Dictionary<ConstructionSector, AISectorIntent> intents)
            && intents.TryGetValue(sector, out intent);
    }

    public static void RebuildAndLog(AIWorldSnapshot snapshot, TeamObjectivePlan plan, AIIntelReport intel, string source)
    {
        if (snapshot == null)
            return;
        TeamId team = snapshot.AITeam;
        Dictionary<ConstructionSector, AISectorIntent> intents = Build(team, snapshot, plan, intel, source);
        intentsBySlot[snapshot.AISlotIndex] = intents;
        Log(team, snapshot.TurnNumber, source, intents);
    }

    private static Dictionary<ConstructionSector, AISectorIntent> Build(
        TeamId team,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        AIIntelReport intel,
        string source)
    {
        var result = new Dictionary<ConstructionSector, AISectorIntent>();
        var sectors = new HashSet<ConstructionSector>();

        IReadOnlyList<SectorManager.SectorInfo> allInfos = SectorManager.GetAllSectorInfos();
        if (allInfos != null)
        {
            foreach (SectorManager.SectorInfo info in allInfos)
                if (info != null)
                    sectors.Add(info.Sector);
        }

        IReadOnlyList<SectorManager.SectorInfo> allBases = SectorManager.GetAllBaseInfos();
        if (allBases != null)
        {
            foreach (SectorManager.SectorInfo info in allBases)
                if (info != null)
                    sectors.Add(info.Sector);
        }

        if (intel?.sectors != null)
        {
            foreach (AISectorIntel item in intel.sectors)
                if (TryParseSector(item?.sector, out ConstructionSector sector))
                    sectors.Add(sector);
        }

        foreach (ConstructionSector sector in sectors)
            result[sector] = BuildIntent(team, snapshot, plan, intel, sector, source);

        return result;
    }

    private static AISectorIntent BuildIntent(
        TeamId team,
        AIWorldSnapshot snapshot,
        TeamObjectivePlan plan,
        AIIntelReport intel,
        ConstructionSector sector,
        string source)
    {
        SectorObjective obj = plan != null ? plan.GetObjectiveForSector(sector) : null;
        AISectorIntel sectorIntel = FindIntelForSector(intel, sector);
        SectorManager.SectorInfo info = TryGetSectorInfo(sector);

        PlayerSlotId observerSlot = PlayerSlotId.FromIndex(
            snapshot != null ? snapshot.AISlotIndex : AIController.ResolveAISlotKey(team));
        bool owned = info != null && info.ControllingSlotIndex == observerSlot.Value;
        AISectorRelation relation = ClassifyRelation(observerSlot, info);
        bool ownNaturalNeutral = relation == AISectorRelation.OwnNatural
            && info != null
            && info.ControllingTeam == TeamId.Neutral;
        bool disputed = info != null && info.IsDisputed;
        bool partial = info != null && info.HasPartialCapture;
        bool hasObjective = obj != null;
        bool defensiveObjective = obj != null && obj.Status == ObjectiveStatus.Defending;
        bool staleDefensiveObjective = defensiveObjective && !owned;
        bool offensiveObjective = obj != null && IsOffensiveStatus(obj.Status);
        bool coveredByCascade = obj == null && IsCoveredByObjectiveCascade(plan, team, sector);
        bool hot = IsHot(sectorIntel);
        bool capturePressure = sectorIntel != null && sectorIntel.capturePressure > 0f;
        bool damagePressure = sectorIntel != null && sectorIntel.damageTaken > 0f;
        bool enemyPresence = sectorIntel != null && sectorIntel.enemyPresence >= 2f;
        bool projectedThreat = capturePressure
            || damagePressure
            || enemyPresence
            || (sectorIntel != null && sectorIntel.landingPressure > 0f);
        float hotScore = sectorIntel != null ? sectorIntel.hotScore : 0f;

        AISectorIntentKind kind;
        if (owned && (defensiveObjective || capturePressure || damagePressure || enemyPresence || disputed || partial))
            kind = hot || capturePressure || damagePressure || enemyPresence || disputed || partial
                ? AISectorIntentKind.Defend
                : AISectorIntentKind.Hold;
        else if (staleDefensiveObjective)
            kind = hot && (sectorIntel.enemyActivity > 0f || enemyPresence || capturePressure)
                ? AISectorIntentKind.Intercept
                : AISectorIntentKind.Attack;
        else if (offensiveObjective)
            kind = ownNaturalNeutral ? AISectorIntentKind.Secure : AISectorIntentKind.Attack;
        else if (relation == AISectorRelation.EnemyBase && !projectedThreat)
            kind = AISectorIntentKind.Avoid;
        else if (coveredByCascade)
            kind = AISectorIntentKind.Covered;
        else if (!owned && hot && (sectorIntel.enemyActivity > 0f || enemyPresence))
            kind = AISectorIntentKind.Intercept;
        else if (!owned && hotScore >= 10f)
            kind = AISectorIntentKind.Avoid;
        else
            kind = owned ? AISectorIntentKind.Hold : AISectorIntentKind.Avoid;

        float confidence = 0.25f;
        if (hasObjective) confidence += 0.25f;
        if (hot) confidence += Mathf.Clamp01(hotScore / 20f) * 0.25f;
        if (capturePressure || damagePressure || enemyPresence) confidence += 0.15f;
        if (disputed || partial) confidence += 0.10f;
        confidence = Mathf.Clamp01(confidence);

        return new AISectorIntent
        {
            Team = team,
            Sector = sector,
            Kind = kind,
            Relation = relation,
            Confidence = confidence,
            HotScore = hotScore,
            Source = source ?? "",
            Reason = BuildReason(obj, info, relation, sectorIntel, staleDefensiveObjective, coveredByCascade),
        };
    }

    private static string BuildReason(SectorObjective obj, SectorManager.SectorInfo info, AISectorRelation relation, AISectorIntel intel, bool staleDefensiveObjective, bool coveredByCascade)
    {
        var parts = new List<string>();
        if (obj != null)
            parts.Add($"obj={obj.Status}/pri{obj.Priority}");
        if (coveredByCascade)
            parts.Add("covered_by_cascade");
        if (staleDefensiveObjective)
            parts.Add("stale_defense_owner_mismatch");
        if (info != null)
        {
            if (info.IsDisputed) parts.Add("disputed");
            if (info.HasPartialCapture) parts.Add("partial");
            parts.Add($"owner={info.ControllingTeam}");
            parts.Add($"zone={relation}");
        }
        if (intel != null)
        {
            if (intel.hotScore > 0f) parts.Add($"hot={intel.hotScore:F1}");
            if (intel.enemyActivity > 0f) parts.Add($"enemyAct={intel.enemyActivity:F1}");
            if (intel.enemyPresence > 0f) parts.Add($"inferredPresence={intel.enemyPresence:F1}");
            if (intel.damageTaken > 0f) parts.Add($"dmg={intel.damageTaken:F1}");
            if (intel.capturePressure > 0f) parts.Add($"cap={intel.capturePressure:F1}");
            if (intel.landingPressure > 0f) parts.Add($"landing={intel.landingPressure:F1}");
        }
        return parts.Count > 0 ? string.Join(" ", parts) : "sem sinal forte";
    }

    private static void Log(TeamId team, int turn, string source, Dictionary<ConstructionSector, AISectorIntent> intents)
    {
        foreach (AISectorIntent intent in intents.Values)
        {
            if (intent.Kind == AISectorIntentKind.Hold && intent.HotScore <= 0f)
                continue;

            Debug.Log($"[AI Intel][Intent][T{turn}][{team}][{source}] {intent.Sector}={intent.Kind} relation={intent.Relation} conf={intent.Confidence:F2} reason={intent.Reason}");
        }
    }

    private static bool TryParseSector(string value, out ConstructionSector sector)
    {
        sector = default;
        return !string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value, out sector);
    }

    private static AISectorIntel FindIntelForSector(AIIntelReport intel, ConstructionSector sector)
    {
        if (intel == null || intel.sectors == null)
            return null;

        string name = sector.ToString();
        foreach (AISectorIntel item in intel.sectors)
            if (item != null && item.sector == name)
                return item;
        return null;
    }

    private static SectorManager.SectorInfo TryGetSectorInfo(ConstructionSector sector)
    {
        if (SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info))
            return info;

        IReadOnlyList<SectorManager.SectorInfo> bases = SectorManager.GetAllBaseInfos();
        if (bases != null)
        {
            foreach (SectorManager.SectorInfo item in bases)
                if (item != null && item.Sector == sector)
                    return item;
        }
        return null;
    }

    private static bool IsHot(AISectorIntel intel)
    {
        return intel != null
            && (intel.hotScore >= 2f
                || intel.capturePressure > 0f
                || intel.landingPressure > 0f
                || intel.damageTaken > 0f
                || intel.enemyPresence >= 2f);
    }

    private static bool IsOffensiveStatus(ObjectiveStatus status)
    {
        return status == ObjectiveStatus.Pending
            || status == ObjectiveStatus.Pursuing
            || status == ObjectiveStatus.Capturing
            || status == ObjectiveStatus.PartialReadyForHandoff;
    }

    private static bool IsCoveredByObjectiveCascade(TeamObjectivePlan plan, TeamId team, ConstructionSector sector)
    {
        if (plan == null || plan.Objectives == null)
            return false;

        for (int i = 0; i < plan.Objectives.Count; i++)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null || obj.Status == ObjectiveStatus.Defending)
                continue;
            if (!IsOffensiveStatus(obj.Status))
                continue;
            if (ComputeForwardNeighborSector(obj.Sector, team) == sector)
                return true;
        }

        return false;
    }

    private static ConstructionSector ComputeForwardNeighborSector(ConstructionSector sector, TeamId team)
    {
        // "Nenhum" e None. Devolvendo default isto respondia ALPHA, e o chamador
        // compara o retorno com um setor: perguntar "Alpha e o forward de alguem?"
        // dava true pra todo objetivo que nao tinha forward nenhum.
        if (!SectorManager.TryGetSectorInfo(sector, out SectorManager.SectorInfo info))
            return ConstructionSector.None;

        float myHQDist = info.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(team)));
        if (info.ClosestNeighbor1 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor1, out SectorManager.SectorInfo n1)
            && n1.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(team))) > myHQDist)
            return info.ClosestNeighbor1;
        if (info.ClosestNeighbor2 != ConstructionSector.None
            && SectorManager.TryGetSectorInfo(info.ClosestNeighbor2, out SectorManager.SectorInfo n2)
            && n2.GetDistanceToHQ(PlayerSlotId.FromIndex(AIController.ResolveAISlotKey(team))) > myHQDist)
            return info.ClosestNeighbor2;
        return ConstructionSector.None;
    }

    public static AISectorRelation ClassifyRelation(PlayerSlotId slotId, SectorManager.SectorInfo info)
    {
        if (info == null)
            return AISectorRelation.Unknown;

        if (ConstructionSectorHelper.IsBase(info.Sector))
        {
            PlayerSlotId hqSlot = FindHQSlotInSector(info.Sector);
            if (hqSlot == slotId) return AISectorRelation.OwnBase;
            if (hqSlot.IsValid) return AISectorRelation.EnemyBase;
            return AISectorRelation.Unknown;
        }

        if (info.ControllingSlotIndex == slotId.Value)
            return AISectorRelation.Owned;
        if (info.ControllingSlotIndex >= 0)
            return AISectorRelation.EnemyOwned;

        PlayerSlotId nearest = info.NearestSlot();
        if (nearest == slotId)
            return AISectorRelation.OwnNatural;
        if (nearest.IsValid)
            return AISectorRelation.EnemyNatural;
        return AISectorRelation.NeutralCenter;
    }

    private static PlayerSlotId FindHQSlotInSector(ConstructionSector sector)
    {
        foreach (ConstructionManager c in ConstructionManager.AllActive)
            if (c != null && c.Sector == sector && c.IsPlayerHeadQuarter)
                return PlayerSlotId.FromIndex(c.SlotIndex);
        return PlayerSlotId.Invalid;
    }
}
