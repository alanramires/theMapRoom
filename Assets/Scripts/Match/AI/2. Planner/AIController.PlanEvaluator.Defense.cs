using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Defesa: validação mid-turn, objetivos domésticos críticos, handoff de
    // transporte, SOS de base, e slots de defesa.
    // -------------------------------------------------------------------------

    private int HomeDefenseThreatRange => Mathf.Max(3, defenseEnemyRange);

    private static bool IsOwnedDefensibleSector(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        return info != null
            && info.ControllingTeam == aiTeam
            && (info.IsFullyControlled || info.IsDisputed || info.HasPartialCapture);
    }

    internal void InvalidateStaleThreatObjectives(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null) return;

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj.Status != ObjectiveStatus.Defending) continue;
            // Rally é guardado com Status=Defending mas NÃO é defesa-stale: sua massa monta pra
            // invasão. Sem este guard, a "ameaça eliminada" dissolvia o rally e soltava a massa.
            if (obj.ObjectiveType == AIObjectiveType.RallyAssembly) continue;

            bool stillThreatened;
            if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            {
                if (!IsOwnedDefensibleSector(info, aiTeam))
                {
                    if (FindCapturableInSector(obj.Sector, aiTeam) != null)
                    {
                        Debug.Log($"{TL("Plan")} Objetivo defensivo {obj.Sector} virou ofensivo mid-turn (owner={info.ControllingTeam})");
                        ConvertStaleDefenseToCaptureObjective(obj, aiTeam);
                    }
                    else
                    {
                        Debug.Log($"{TL("Plan")} Objetivo defensivo {obj.Sector} removido mid-turn (owner={info.ControllingTeam}, sem capturavel)");
                        ClearObjectiveHUD(obj);
                        plan.Objectives.RemoveAt(i);
                    }
                    continue;
                }

                bool criticalHomeThreat = IsCriticalHomeDefenseSector(info, aiTeam)
                    && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);
                if (criticalHomeThreat)
                {
                    stillThreatened = true;
                }
                else
                {
                    Vector3Int rc = info.RepresentativeCell; rc.z = 0;
                    stillThreatened = info.IsDisputed
                        || info.HasPartialCapture
                        || HasNearbyVisibleEnemy(rc, aiTeam, defenseEnemyRange);
                    if (!stillThreatened && ConstructionSectorHelper.IsBase(obj.Sector))
                    {
                        foreach (SectorManager.SectorConstructionInfo c in info.Constructions)
                        {
                            Vector3Int cc = c.Cell; cc.z = 0;
                            if (HasNearbyVisibleEnemy(cc, aiTeam, defenseEnemyRange))
                            {
                                stillThreatened = true;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                stillThreatened = true;
            }

            if (!stillThreatened)
            {
                Debug.Log($"{TL("Plan")} Objetivo defensivo {obj.Sector} invalidado mid-turn (ameaça eliminada)");
                ClearObjectiveHUD(obj);
                plan.Objectives.RemoveAt(i);
            }
        }
    }

    private void ConvertStaleDefenseToCaptureObjective(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null) return;

        obj.Status = ObjectiveStatus.Pursuing;
        obj.HandoffEligible = false;
        obj.PreferredHandoffFromUnitId = -1;

        bool hasCapturerSlot = false;
        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role == UnitRole.Capturador)
            {
                hasCapturerSlot = true;
                continue;
            }

            bool keepSupport = slot.Role == UnitRole.Assalto || slot.Role == UnitRole.FogoIndireto;
            if (keepSupport)
                continue;

            if (slot.Filled)
            {
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                unit?.ClearAIAssignedPlan();
            }
            obj.Slots.RemoveAt(i);
        }

        if (!hasCapturerSlot)
            obj.Slots.Insert(0, new SlotNeed { Role = UnitRole.Capturador });
    }

    private void NormalizeDefenseObjectiveSlots(SectorObjective obj, bool urgentCapturer, bool addAssault, TeamId aiTeam)
    {
        if (obj == null)
            return;

        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != UnitRole.Transportador)
                continue;

            if (slot.Filled)
            {
                UnitManager transUnit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                transUnit?.ClearAIAssignedPlan();
            }
            obj.Slots.RemoveAt(i);
        }

        int desiredCapturers = urgentCapturer ? 2 : 1;
        int capturers = 0;
        bool hasAssault = false;
        foreach (SlotNeed slot in obj.Slots)
        {
            if (slot.Role == UnitRole.Capturador) capturers++;
            if (slot.Role == UnitRole.Assalto) hasAssault = true;
        }

        while (capturers < desiredCapturers)
        {
            obj.Slots.Add(new SlotNeed { Role = UnitRole.Capturador });
            capturers++;
        }

        if (addAssault && !hasAssault)
            obj.Slots.Add(new SlotNeed { Role = UnitRole.Assalto });
    }

    private bool IsCriticalHomeDefenseObjective(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Status != ObjectiveStatus.Defending)
            return false;
        if (TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
            return IsCriticalHomeDefenseSector(info, aiTeam)
                && IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange);

        return IsCriticalHomeDefenseSector(obj.Sector, aiTeam)
            && IsHomeDefenseThreatened(obj.Sector, aiTeam, HomeDefenseThreatRange);
    }

    private bool TryFindCriticalHomeDefenseObjective(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        out SectorObjective best)
    {
        best = null;
        if (plan == null)
            return false;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsCriticalHomeDefenseObjective(obj, aiTeam))
                continue;
            if (best == null || obj.Priority < best.Priority)
                best = obj;
        }

        return best != null;
    }

    private bool TryFindCriticalHomeDefenseObjectiveForUnit(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        UnitManager unit,
        Vector3Int fromCell,
        string callerLabel,
        out SectorObjective best)
    {
        best = null;
        if (plan == null || unit == null)
            return false;

        int responseRange = Mathf.Max(HomeDefenseThreatRange, defenseEnemyRange * defenseCallRange);
        float bestDistance = float.MaxValue;
        bool foundCritical = false;
        fromCell.z = 0;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (!IsCriticalHomeDefenseObjective(obj, aiTeam))
                continue;
            foundCritical = true;
            Vector3Int targetCell = ResolveCriticalHomeDefenseTargetCell(obj, aiTeam, fromCell);
            float distance = CalculateUnitResponseDistance(unit, fromCell, targetCell);
            if (distance > responseRange)
            {
                Debug.Log($"{TL("Plan")} {callerLabel} {unit.InstanceId} nao redireciona para {obj.Sector}: dist={distance:F0}h > chamada={responseRange}h");
                continue;
            }
            if (best == null
                || obj.Priority < best.Priority
                || (obj.Priority == best.Priority && distance < bestDistance))
            {
                best = obj;
                bestDistance = distance;
            }
        }

        if (!foundCritical)
            Debug.Log($"{TL("Plan")} {callerLabel} {unit.InstanceId} nao achou SOS Base/HQ ativo no plano");

        return best != null;
    }

    private Vector3Int ResolveCriticalHomeDefenseTargetCell(
        SectorObjective obj,
        TeamId aiTeam,
        Vector3Int fallback)
    {
        ConstructionManager target = obj != null
            ? FindCapturableInSector(obj.Sector, aiTeam, fallback)
            : null;
        if (target != null)
        {
            Vector3Int tc = target.CurrentCellPosition; tc.z = 0;
            return tc;
        }

        if (obj != null && TryGetAnySectorInfo(obj.Sector, out SectorManager.SectorInfo info))
        {
            Vector3Int rc = info.RepresentativeCell; rc.z = 0;
            return rc;
        }

        if (obj != null && TryFindHomeDefenseLiveAnchor(obj.Sector, aiTeam, fallback, out Vector3Int liveAnchor))
            return liveAnchor;

        fallback.z = 0;
        return fallback;
    }

    private static float CalculateUnitResponseDistance(UnitManager unit, Vector3Int fromCell, Vector3Int targetCell)
    {
        fromCell.z = 0;
        targetCell.z = 0;
        if (unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && SectorManager.TryGetLandMovementDistance(fromCell, targetCell, data, out int unitCost))
            return unitCost;
        if (SectorManager.TryGetLandMovementDistance(fromCell, targetCell, out int terrainCost))
            return terrainCost;
        return SectorManager.HexDistance(fromCell, targetCell);
    }

    private void EnsureCriticalHomeDefenseObjectives(
        TeamObjectivePlan plan,
        TeamId aiTeam,
        IReadOnlyList<SectorManager.SectorInfo> allSectors)
    {
        if (plan == null || allSectors == null)
            return;

        foreach (SectorManager.SectorInfo info in allSectors)
        {
            if (!IsCriticalHomeDefenseSector(info, aiTeam))
                continue;
            if (!IsHomeDefenseThreatened(info, aiTeam, HomeDefenseThreatRange))
                continue;

            SectorObjective obj = plan.GetObjectiveForSector(info.Sector);
            if (obj == null)
            {
                obj = new SectorObjective
                {
                    Sector = info.Sector,
                    AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending,
                    Priority = int.MaxValue,
                };
                plan.Objectives.Add(obj);
            }
            else
            {
                obj.Status = ObjectiveStatus.Defending;
                obj.AssignedTeam = aiTeam;
                obj.Priority = int.MaxValue;
            }

            RemoveTransportSlotsFromDefense(obj, aiTeam);
            EnsureOpenSlots(obj, UnitRole.Capturador, 2);

            Debug.Log($"{TL("Plan")} SOS Base/HQ: {info.Sector} sob captura/ameaca critica");
        }
    }

    private void ClearResolvedCriticalHomeDefenseObjectives(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        for (int i = plan.Objectives.Count - 1; i >= 0; i--)
        {
            SectorObjective obj = plan.Objectives[i];
            if (obj == null || obj.Status != ObjectiveStatus.Defending)
                continue;
            if (!IsCriticalHomeDefenseSector(obj.Sector, aiTeam))
                continue;
            if (IsHomeDefenseThreatened(obj.Sector, aiTeam, HomeDefenseThreatRange))
                continue;
            if (FindCapturableInSector(obj.Sector, aiTeam) != null)
                continue;

            if (obj.Slots != null)
            {
                foreach (SlotNeed slot in obj.Slots)
                {
                    if (slot == null || !slot.Filled)
                        continue;
                    UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    unit?.ClearAIAssignedPlan();
                }
            }

            ClearObjectiveHUD(obj);
            plan.Objectives.RemoveAt(i);
            Debug.Log($"{TL("Plan")} SOS Base/HQ resolvido: {obj.Sector} seguro - objetivo defensivo removido");
        }
    }

    private void EnsureCriticalHomeDefenseObjectivesFromConstructions(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null)
            return;

        var sectors = new HashSet<ConstructionSector>();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null)
                continue;
            if (!IsCriticalHomeDefenseSector(construction.Sector, aiTeam))
                continue;
            if (!IsHomeDefenseThreatened(construction.Sector, aiTeam, HomeDefenseThreatRange))
                continue;
            sectors.Add(construction.Sector);
        }

        foreach (ConstructionSector sector in sectors)
        {
            SectorObjective obj = plan.GetObjectiveForSector(sector);
            if (obj == null)
            {
                obj = new SectorObjective
                {
                    Sector = sector,
                    AssignedTeam = aiTeam,
                    Status = ObjectiveStatus.Defending,
                    Priority = int.MaxValue,
                };
                plan.Objectives.Add(obj);
            }
            else
            {
                obj.Status = ObjectiveStatus.Defending;
                obj.AssignedTeam = aiTeam;
                obj.Priority = int.MaxValue;
            }

            RemoveTransportSlotsFromDefense(obj, aiTeam);
            EnsureOpenSlots(obj, UnitRole.Capturador, 2);

            Debug.Log($"{TL("Plan")} SOS Base/HQ live: {sector} sob captura/ameaca no conjunto da base");
        }
    }

    private void RemoveTransportSlotsFromDefense(SectorObjective obj, TeamId aiTeam)
    {
        if (obj == null || obj.Slots == null)
            return;

        for (int i = obj.Slots.Count - 1; i >= 0; i--)
        {
            SlotNeed slot = obj.Slots[i];
            if (slot.Role != UnitRole.Transportador)
                continue;
            if (slot.Filled)
            {
                UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                unit?.ClearAIAssignedPlan();
            }
            obj.Slots.RemoveAt(i);
        }
    }

    private void ReleaseShortRangeEmptyTransportAssignments(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;

            if (!TryResolveObjectiveTargetCell(obj, aiTeam, out Vector3Int targetCell))
                continue;

            for (int i = obj.Slots.Count - 1; i >= 0; i--)
            {
                SlotNeed slot = obj.Slots[i];
                if (slot.Role != UnitRole.Transportador)
                    continue;

                if (!slot.Filled)
                    continue;

                UnitManager transporter = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                if (transporter == null)
                {
                    obj.Slots.RemoveAt(i);
                    continue;
                }

                if (HasTransportCargo(transporter))
                    continue;

                if (ConstructionSectorHelper.IsBase(obj.Sector)
                    && FindHQTeamInSector(obj.Sector) != aiTeam)
                    continue;

                Vector3Int transporterCell = transporter.CurrentCellPosition; transporterCell.z = 0;
                float apcDist = SectorManager.HexDistance(transporterCell, targetCell);
                int effectiveThreshold = GetEffectiveTransportThreshold(aiTeam);
                if (apcDist >= effectiveThreshold)
                    continue;

                bool supportNeeded = IsObjectiveInCombatDisadvantage(obj, aiTeam, targetCell,
                    alliesEnemyRange, alliesAgainstEnemiesHpRatio, out int enemyHp, out int allyHp);
                if (supportNeeded)
                {
                    Debug.Log($"{TL("Plan")} Transportador {transporter.InstanceId} mantém apoio em {obj.Sector} " +
                              $"({apcDist:F0}h<{effectiveThreshold}h, combate inimigo={enemyHp}HP aliado={allyHp}HP)");
                    continue;
                }

                transporter.ClearAIAssignedPlan();
                obj.Slots.RemoveAt(i);
                Debug.Log($"{TL("Plan")} Transportador {transporter.InstanceId} libera {obj.Sector}: " +
                          $"vazio e perto ({apcDist:F0}h<{effectiveThreshold}h), volta a shuttle/HQ");
            }
        }
    }

    private void ReleaseOffensiveSupportWithoutCapturer(TeamObjectivePlan plan, TeamId aiTeam)
    {
        if (plan == null || plan.Objectives == null)
            return;

        foreach (SectorObjective obj in plan.Objectives)
        {
            if (obj == null || obj.Slots == null)
                continue;
            // Assembly e uma massa de invasao, nao uma escolta de captura convencional.
            // Infantaria, artilharia e APC permanecem concentrados mesmo se o slot de
            // capturador estiver temporariamente aberto.
            if (IsActiveRallyAssemblyObjective(obj))
                continue;
            if (HasFilledSlot(obj, UnitRole.Capturador))
                continue;

            for (int i = obj.Slots.Count - 1; i >= 0; i--)
            {
                SlotNeed slot = obj.Slots[i];
                if (slot.Role == UnitRole.Capturador)
                    continue;
                // Transporte é aposta de futuro: a alocação acontece no turno seguinte, então o
                // slot permanece como demanda mesmo sem capturador agora (espelha o AirTransport,
                // que mede demanda pelos slots de capturador, não pelos preenchidos).
                if (slot.Role == UnitRole.Transportador)
                    continue;
                if (slot.Role != UnitRole.Assalto
                    && slot.Role != UnitRole.FogoIndireto)
                    continue;

                if (slot.Filled)
                {
                    UnitManager unit = FindActiveUnit(slot.AssignedUnitId, aiTeam);
                    unit?.ClearAIAssignedPlan();
                    Debug.Log($"{TL("Plan")} {slot.Role} {slot.AssignedUnitId} liberado de {obj.Sector}: sem capturador no plano");
                }

                obj.Slots.RemoveAt(i);
            }
        }
    }

    private static bool IsCriticalHomeDefenseSector(SectorManager.SectorInfo info, TeamId aiTeam)
    {
        if (info == null)
            return false;
        if (IsCriticalHomeDefenseSector(info.Sector, aiTeam))
            return true;

        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            if (construction?.Source == null) continue;
            if (construction.Source.IsPlayerHeadQuarter && construction.OwnerTeam == aiTeam)
                return true;
        }

        return false;
    }

    private static bool IsCriticalHomeDefenseSector(ConstructionSector sector, TeamId aiTeam)
    {
        if (ConstructionSectorHelper.IsBase(sector))
            return FindHQTeamInSector(sector) == aiTeam;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (construction.SlotIndex == ResolveAISlotKey(aiTeam) && construction.IsPlayerHeadQuarter)
                return true;
        }

        return false;
    }

    private bool IsHomeDefenseThreatened(SectorManager.SectorInfo info, TeamId aiTeam, int range)
    {
        if (info == null)
            return false;
        if (IsHomeDefenseThreatened(info.Sector, aiTeam, range))
            return true;

        Vector3Int rep = info.RepresentativeCell; rep.z = 0;
        if (HasNearbyVisibleEnemy(rep, aiTeam, range))
            return true;

        foreach (SectorManager.SectorConstructionInfo construction in info.Constructions)
        {
            Vector3Int cell = construction.Cell; cell.z = 0;
            if (HasNearbyVisibleEnemy(cell, aiTeam, range))
                return true;
        }

        return false;
    }

    private bool IsHomeDefenseThreatened(ConstructionSector sector, TeamId aiTeam, int range)
    {
        if (!IsCriticalHomeDefenseSector(sector, aiTeam))
            return false;

        MatchController mc = GetMatchController();
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;

            Vector3Int cc = construction.CurrentCellPosition; cc.z = 0;
            foreach (UnitManager enemy in UnitManager.AllActive)
            {
                if (enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked)
                    continue;
                if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                    continue;
                Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                if (SectorManager.HexDistance(ec, cc) <= range)
                    return true;
            }
        }

        return false;
    }

    private bool TryFindHomeDefenseLiveAnchor(
        ConstructionSector sector,
        TeamId aiTeam,
        Vector3Int fromCell,
        out Vector3Int anchor)
    {
        anchor = fromCell;
        ConstructionManager best = null;
        float bestScore = float.MinValue;
        MatchController mc = GetMatchController();
        fromCell.z = 0;
        bool ownBaseSector = ConstructionSectorHelper.IsBase(sector)
            && FindHQTeamInSector(sector) == aiTeam;

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;

            Vector3Int cc = construction.CurrentCellPosition; cc.z = 0;
            bool ownedHomeConstruction = construction.SlotIndex == ResolveAISlotKey(aiTeam)
                && (ownBaseSector || construction.IsPlayerHeadQuarter);
            bool underCapture = construction.IsCapturable
                && construction.CapturePointsMax > 0
                && ownedHomeConstruction
                && construction.CurrentCapturePoints < construction.CapturePointsMax;
            bool hasNearbyEnemy = false;
            if (ownedHomeConstruction)
            {
                foreach (UnitManager enemy in UnitManager.AllActive)
                {
                    if (enemy.SlotIndex == ResolveAISlotKey(aiTeam) || enemy.IsDead || enemy.IsEmbarked)
                        continue;
                    if (mc != null && !mc.IsUnitVisibleForTeam(enemy, aiTeam))
                        continue;
                    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
                    if (SectorManager.HexDistance(ec, cc) <= HomeDefenseThreatRange)
                    {
                        hasNearbyEnemy = true;
                        break;
                    }
                }
            }

            if (!underCapture && !hasNearbyEnemy && !ownedHomeConstruction)
                continue;

            float score = 0f;
            if (underCapture) score += 100000f;
            if (hasNearbyEnemy) score += 50000f;
            if (construction.IsPlayerHeadQuarter) score += 1000f;
            score -= SectorManager.HexDistance(fromCell, cc);

            if (best == null || score > bestScore)
            {
                best = construction;
                bestScore = score;
                anchor = cc;
            }
        }

        return best != null;
    }

    private static bool HasHomeConstructionUnderCapture(ConstructionSector sector, TeamId aiTeam)
    {
        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            if (construction == null || construction.Sector != sector)
                continue;
            if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;
            if (construction.SlotIndex != ResolveAISlotKey(aiTeam))
                continue;
            if (construction.CurrentCapturePoints < construction.CapturePointsMax)
                return true;
        }

        return false;
    }
}
