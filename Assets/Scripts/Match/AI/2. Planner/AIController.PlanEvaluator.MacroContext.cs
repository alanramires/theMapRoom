using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Contexto macro-territorial: classifica a fase da partida e aplica caps
    // de objetivos ofensivos baseados na proporção de setores controlados.
    // -------------------------------------------------------------------------

    private enum AIMacroTerritoryPhase
    {
        EarlyExpansion,
        Balanced,
        Collapsing,
        Dominating
    }

    private struct AIMacroTerritoryContext
    {
        public AIMacroTerritoryPhase Phase;
        public int OwnedSectors;
        public int EnemySectors;
        public int NeutralSectors;
        public int TotalSectors;
        public float OwnedRatio;
        public int OffensiveCap;
        public bool AppliesCap;
    }

    private static AIMacroTerritoryContext BuildMacroTerritoryContext(
        TeamId aiTeam,
        IReadOnlyList<SectorManager.SectorInfo> sectors,
        int defaultOffensiveCap)
    {
        AIMacroTerritoryContext ctx = new AIMacroTerritoryContext
        {
            Phase = AIMacroTerritoryPhase.EarlyExpansion,
            OffensiveCap = Mathf.Max(1, defaultOffensiveCap),
            OwnedRatio = 0.5f
        };

        if (sectors == null || sectors.Count == 0)
            return ctx;

        foreach (SectorManager.SectorInfo info in sectors)
        {
            if (info == null)
                continue;

            ctx.TotalSectors++;
            TeamId owner = info.ControllingTeam;
            if (owner == aiTeam)
                ctx.OwnedSectors++;
            else if (owner == TeamId.Neutral)
                ctx.NeutralSectors++;
            else
                ctx.EnemySectors++;
        }

        int controlled = ctx.OwnedSectors + ctx.EnemySectors;
        ctx.OwnedRatio = controlled > 0 ? ctx.OwnedSectors / (float)controlled : 0.5f;

        int earlyControlledThreshold = Mathf.Max(2, Mathf.CeilToInt(ctx.TotalSectors * 0.35f));
        float neutralRatio = ctx.TotalSectors > 0 ? ctx.NeutralSectors / (float)ctx.TotalSectors : 1f;
        bool early = controlled < earlyControlledThreshold || neutralRatio >= 0.45f;
        if (early)
        {
            ctx.Phase = AIMacroTerritoryPhase.EarlyExpansion;
            ctx.AppliesCap = false;
            return ctx;
        }

        if (ctx.OwnedRatio <= 0.35f)
        {
            ctx.Phase = AIMacroTerritoryPhase.Collapsing;
            ctx.OffensiveCap = 1;
            ctx.AppliesCap = true;
        }
        else if (ctx.OwnedRatio >= 0.65f)
        {
            ctx.Phase = AIMacroTerritoryPhase.Dominating;
            ctx.OffensiveCap = 2;
            ctx.AppliesCap = true;
        }
        else
        {
            ctx.Phase = AIMacroTerritoryPhase.Balanced;
            ctx.AppliesCap = false;
        }

        return ctx;
    }

    private void ApplyMacroExistingOffensiveCap(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        AIMacroTerritoryContext macro,
        AIIntelReport intel,
        int turnNumber)
    {
        if (plan == null || !macro.AppliesCap)
            return;

        var protectedObjectives = new HashSet<SectorObjective>();
        var candidates = new List<(SectorObjective obj, float score)>();

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsMacroOffensiveObjective(obj))
                continue;

            if (IsMacroProtectedOffensiveObjective(obj, aiTeam))
            {
                protectedObjectives.Add(obj);
                Debug.Log($"[AI Macro][T{turnNumber}][{aiTeam}] preservando {obj.Sector}: progresso/captura ativa");
                continue;
            }

            candidates.Add((obj, ScoreMacroOffensiveObjective(obj, aiTeam, intel)));
        }

        int remainingKeep = Mathf.Max(0, macro.OffensiveCap - protectedObjectives.Count);
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        var keep = new HashSet<SectorObjective>(protectedObjectives);
        for (int i = 0; i < candidates.Count && i < remainingKeep; i++)
        {
            keep.Add(candidates[i].obj);
            Debug.Log($"[AI Macro][T{turnNumber}][{aiTeam}] eixo mantido: {candidates[i].obj.Sector} score={candidates[i].score:F0}");
        }

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (!IsMacroOffensiveObjective(obj) || keep.Contains(obj))
                continue;

            Debug.Log($"[AI Macro][T{turnNumber}][{aiTeam}] removendo {obj.Sector}: {macro.Phase} off-axis score={ScoreMacroOffensiveObjective(obj, aiTeam, intel):F0}");
            ClearObjectiveHUD(obj);
            obj.Status = ObjectiveStatus.Abandoned;
            plan.Objectives.RemoveAt(i);
        }
    }

    private static bool IsMacroOffensiveObjective(SectorObjective obj)
    {
        if (obj == null)
            return false;

        return obj.Status == ObjectiveStatus.Pending
            || obj.Status == ObjectiveStatus.Pursuing
            || obj.Status == ObjectiveStatus.Capturing
            || obj.Status == ObjectiveStatus.PartialReadyForHandoff;
    }

    private static bool IsMacroProtectedOffensiveObjective(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null)
            return false;

        if (obj.Status == ObjectiveStatus.Capturing || obj.Status == ObjectiveStatus.PartialReadyForHandoff)
            return true;

        return HasOwnCaptureProgressInSector(obj.Sector, aiTeam);
    }

    private float ScoreMacroOffensiveObjective(SectorObjective obj, TeamId aiTeam, AIIntelReport intel)
    {
        if (obj == null)
            return float.MinValue;

        float score = Mathf.Max(0, 100 - obj.Priority * 4);
        if (obj.Status == ObjectiveStatus.Capturing) score += 120f;
        else if (obj.Status == ObjectiveStatus.PartialReadyForHandoff) score += 100f;
        else if (obj.Status == ObjectiveStatus.Pursuing) score += 20f;

        foreach (SlotNeed slot in obj.Slots)
        {
            if (!slot.Filled)
                continue;

            switch (slot.Role)
            {
                case UnitRole.Capturador: score += 50f; break;
                case UnitRole.Assalto: score += 35f; break;
                case UnitRole.FogoIndireto: score += 25f; break;
                case UnitRole.Transportador: score += 10f; break;
            }

            if (slot.DistanceToObjective >= 0)
                score -= Mathf.Min(20f, slot.DistanceToObjective);
        }

        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            score -= Mathf.Min(25f, info.GetDistanceToHQ(aiTeam));
            if (info.GetRiskLevelFor(aiTeam) >= SectorManager.SectorRiskLevel.High)
                score -= 10f;
            if (info.HasPartialCapture)
                score += 40f;
        }

        AISectorIntel sectorIntel = FindIntelForSector(intel, obj.Sector);
        if (sectorIntel != null)
        {
            score += Mathf.Min(45f, sectorIntel.hotScore);
            score += sectorIntel.capturePressure * 6f;
            score += sectorIntel.enemyPresence * 2f;
        }

        if (ConstructionSectorHelper.IsBase(obj.Sector))
            score -= 20f;

        return score;
    }
}
