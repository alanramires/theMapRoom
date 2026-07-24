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
        public int OwnedControlPoints;
        public int EnemyControlPoints;
        public int DisputedControlPoints;
        public float OwnedRatio;
        public int OwnForce;       // minhas unidades
        public int EnemyForce;     // força inimiga usada no ratio (conhecidas + projeção Hard)
        public int EnemyProducersProjected; // Hard: produtores inimigos somados como onda projetada (0 se normal/easy)
        public float ForceRatio;   // minhas / (minhas + EnemyForce)
        public int OffensiveCap;
        public bool AppliesCap;
    }

    // Último macro context calculado por time (com força). O painel de inspeção lê DAQUI pra mostrar
    // exatamente o que a AI decidiu (a inspeção não tem intel pra recomputar a força sozinha).
    private static readonly Dictionary<int, AIMacroTerritoryContext> s_lastMacroBySlot
        = new Dictionary<int, AIMacroTerritoryContext>();

    private static AIMacroTerritoryContext BuildMacroTerritoryContext(
        TeamId aiTeam,
        IReadOnlyList<SectorManager.SectorInfo> sectors,
        int defaultOffensiveCap,
        int ownForce,
        int enemyForce,
        int enemyProducersProjected = 0)
    {
        AIMacroTerritoryContext ctx = new AIMacroTerritoryContext
        {
            Phase = AIMacroTerritoryPhase.EarlyExpansion,
            OffensiveCap = Mathf.Max(1, defaultOffensiveCap),
            OwnedRatio = 0.5f,
            OwnForce = ownForce,
            EnemyForce = enemyForce,
            EnemyProducersProjected = enemyProducersProjected,
            // Sem inimigo conhecido => 1.0 (não arrasta nada pra baixo); senão minhas/(minhas+deles).
            ForceRatio = (ownForce + enemyForce) > 0 ? ownForce / (float)(ownForce + enemyForce) : 0.5f
        };

        if (sectors == null || sectors.Count == 0)
        {
            s_lastMacroBySlot[ResolveAISlotKey(aiTeam)] = ctx;
            return ctx;
        }

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

            AccumulateCapturePointControl(
                info, aiTeam,
                ref ctx.OwnedControlPoints,
                ref ctx.EnemyControlPoints,
                ref ctx.DisputedControlPoints);
        }

        int controlled = ctx.OwnedSectors + ctx.EnemySectors;
        int effectiveControlled = ctx.OwnedControlPoints + ctx.EnemyControlPoints;
        ctx.OwnedRatio = effectiveControlled > 0
            ? ctx.OwnedControlPoints / (float)effectiveControlled
            : 0.5f;

        int earlyControlledThreshold = Mathf.Max(2, Mathf.CeilToInt(ctx.TotalSectors * 0.35f));
        float neutralRatio = ctx.TotalSectors > 0 ? ctx.NeutralSectors / (float)ctx.TotalSectors : 1f;
        bool early = controlled < earlyControlledThreshold || neutralRatio >= 0.45f;
        if (early)
        {
            ctx.Phase = AIMacroTerritoryPhase.EarlyExpansion;
            ctx.AppliesCap = false;
            s_lastMacroBySlot[ResolveAISlotKey(aiTeam)] = ctx;
            return ctx;
        }

        // Decisão por território E força: usa o PIOR dos dois (min). PERDENDO se território OU força
        // <= 40% (defesa sensível ao perigo: basta uma das duas estar ruim); GANHANDO só quando AMBOS
        // >= 60%. Faixa 40-60% = Empatado. Ex.: 38% setores -> Perdendo; 63% setores + 79% força -> Ganhando.
        float decisionRatio = Mathf.Min(ctx.OwnedRatio, ctx.ForceRatio);

        if (decisionRatio <= 0.40f)
        {
            ctx.Phase = AIMacroTerritoryPhase.Collapsing;
            ctx.OffensiveCap = Mathf.Clamp(defaultOffensiveCap, 2, 3);
            ctx.AppliesCap = true;
        }
        else if (decisionRatio >= 0.60f)
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

        s_lastMacroBySlot[ResolveAISlotKey(aiTeam)] = ctx;
        return ctx;
    }

    private static void AccumulateCapturePointControl(
        SectorManager.SectorInfo info,
        TeamId aiTeam,
        ref int ownedPoints,
        ref int enemyPoints,
        ref int disputedPoints)
    {
        if (info?.Constructions == null || info.Constructions.Count == 0)
            return;

        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            if (construction == null || construction.CapturePointsMax <= 0
                || construction.OwnerTeam == TeamId.Neutral)
                continue;

            int max = construction.CapturePointsMax;
            int current = Mathf.Clamp(construction.CurrentCapturePoints, 0, max);
            if (construction.OwnerTeam == aiTeam)
                ownedPoints += current;
            else
                enemyPoints += current;
            disputedPoints += max - current;
        }
    }

    // -------------------------------------------------------------------------
    // Inspeção (ShoppingPressureWindow): expõe a visão macro-territorial da AI —
    // setores seus/inimigos/neutros e como ela classifica a partida (perdendo/
    // empatado/ganhando/início). Leitura viva e silenciosa (sem log).
    // -------------------------------------------------------------------------
    public struct MacroTerritoryInspection
    {
        public int OwnedSectors;
        public int EnemySectors;
        public int NeutralSectors;
        public int TotalSectors;
        public int OwnedControlPoints;
        public int EnemyControlPoints;
        public int DisputedControlPoints;
        public float OwnedRatio;
        public int OwnForce;       // minhas unidades
        public int EnemyForce;     // força inimiga usada no ratio (conhecidas + projeção Hard)
        public int EnemyProducersProjected; // Hard: parcela projetada (produtores inimigos); 0 se normal/easy
        public float ForceRatio;   // minhas / (minhas + EnemyForce)
        public string PhaseLabel;  // "Perdendo" / "Empatado" / "Ganhando" / "Início"
        public string PhaseRaw;    // nome cru do enum (referência)
        public bool Losing;
        public bool Winning;
    }

    public static MacroTerritoryInspection GetMacroTerritoryForInspection(TeamId team)
    {
        // Prefere o último valor REAL calculado no plano (com força). Só recomputa sector-only se a
        // AI ainda não rodou o plano pra esse time (cache vazio) — aí sem dados de força (0/0).
        if (!s_lastMacroBySlot.TryGetValue(ResolveAISlotKey(team), out AIMacroTerritoryContext ctx))
            ctx = BuildMacroTerritoryContext(team, SectorManager.GetAllSectorInfos(), 6, 0, 0);

        string label;
        switch (ctx.Phase)
        {
            case AIMacroTerritoryPhase.Collapsing: label = "Perdendo"; break;
            case AIMacroTerritoryPhase.Dominating: label = "Ganhando"; break;
            case AIMacroTerritoryPhase.Balanced:   label = "Empatado"; break;
            default:                               label = "Início/Expansão"; break;
        }

        return new MacroTerritoryInspection
        {
            OwnedSectors   = ctx.OwnedSectors,
            EnemySectors   = ctx.EnemySectors,
            NeutralSectors = ctx.NeutralSectors,
            TotalSectors   = ctx.TotalSectors,
            OwnedControlPoints = ctx.OwnedControlPoints,
            EnemyControlPoints = ctx.EnemyControlPoints,
            DisputedControlPoints = ctx.DisputedControlPoints,
            OwnedRatio     = ctx.OwnedRatio,
            OwnForce       = ctx.OwnForce,
            EnemyForce     = ctx.EnemyForce,
            EnemyProducersProjected = ctx.EnemyProducersProjected,
            ForceRatio     = ctx.ForceRatio,
            PhaseLabel     = label,
            PhaseRaw       = ctx.Phase.ToString(),
            Losing         = ctx.Phase == AIMacroTerritoryPhase.Collapsing,
            Winning        = ctx.Phase == AIMacroTerritoryPhase.Dominating,
        };
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

            if (ShouldSuppressEnemyBaseInvasion(macro, obj, aiTeam))
                continue;

            if (IsMacroProtectedOffensiveObjective(obj, aiTeam, macro))
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
            if (!IsMacroOffensiveObjective(obj))
                continue;

            if (ShouldSuppressEnemyBaseInvasion(macro, obj, aiTeam))
            {
                Debug.Log($"[AI Macro][T{turnNumber}][{aiTeam}] removendo {obj.Sector}: Collapsing suprime invasao de base");
                ClearObjectiveHUD(obj);
                obj.Status = ObjectiveStatus.Abandoned;
                plan.Objectives.RemoveAt(i);
                continue;
            }

            if (keep.Contains(obj))
                continue;

            Debug.Log($"[AI Macro][T{turnNumber}][{aiTeam}] removendo {obj.Sector}: {macro.Phase} off-axis score={ScoreMacroOffensiveObjective(obj, aiTeam, intel):F0}");
            ClearObjectiveHUD(obj);
            obj.Status = ObjectiveStatus.Abandoned;
            plan.Objectives.RemoveAt(i);
        }
    }

    private static bool ShouldSuppressEnemyBaseInvasion(AIMacroTerritoryContext macro, SectorObjective obj, TeamId aiTeam)
    {
        return obj != null
            && macro.Phase == AIMacroTerritoryPhase.Collapsing
            && ConstructionSectorHelper.IsBase(obj.Sector)
            && FindHQTeamInSector(obj.Sector) != aiTeam;
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

    private static bool IsMacroProtectedOffensiveObjective(SectorObjective obj, TeamId aiTeam, AIMacroTerritoryContext macro)
    {
        if (obj == null)
            return false;
        if (macro.Phase == AIMacroTerritoryPhase.Collapsing
            && ConstructionSectorHelper.IsBase(obj.Sector)
            && FindHQTeamInSector(obj.Sector) != aiTeam)
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
