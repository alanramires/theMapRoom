using System;
using System.Collections.Generic;
using UnityEngine;

public static class SaveDataMapper
{
    public static MatchStateSaveData BuildMatchStateSaveData(MatchController matchController)
    {
        MatchStateSaveData result = new MatchStateSaveData
        {
            currentTurn = matchController != null ? matchController.CurrentTurn : 0,
            activeTeamId = matchController != null ? matchController.ActiveTeamId : (int)TeamId.Green,
            includeNeutralTeam = matchController != null && matchController.IncludeNeutralTeam,
            economyEnabled = matchController == null || matchController.EconomyEnabled
        };

        if (matchController == null)
            return result;

        List<int> teamIds = new List<int>();
        List<bool> flipXs = new List<bool>();
        List<bool> isAIs = new List<bool>();
        List<int> startMoneys = new List<int>();
        List<int> actualMoneys = new List<int>();
        List<int> incomePerTurns = new List<int>();
        List<bool> startMoneyAppliedFlags = new List<bool>();
        matchController.ExportPlayersState(teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyAppliedFlags);

        int playerCount = teamIds.Count;
        for (int i = 0; i < playerCount; i++)
        {
            result.players.Add(new MatchPlayerSaveData
            {
                teamId = teamIds[i],
                flipX = i < flipXs.Count && flipXs[i],
                isAI = i < isAIs.Count && isAIs[i],
                startMoney = i < startMoneys.Count ? Mathf.Max(0, startMoneys[i]) : 0,
                actualMoney = i < actualMoneys.Count ? Mathf.Max(0, actualMoneys[i]) : 0,
                incomePerTurn = i < incomePerTurns.Count ? Mathf.Max(0, incomePerTurns[i]) : 0,
                startMoneyApplied = i < startMoneyAppliedFlags.Count && startMoneyAppliedFlags[i]
            });
        }

        List<int> victoryTeamIds = new List<int>();
        List<int> victoryStars = new List<int>();
        matchController.ExportVictoryStarsState(
            victoryTeamIds,
            victoryStars,
            out bool victoryEnabled,
            out int victoryStarsToWin,
            out bool hasWinner,
            out int winnerTeamId);
        result.victoryStarsEnabled = victoryEnabled;
        result.victoryStarsToWin = Mathf.Max(1, victoryStarsToWin);
        result.hasVictoryWinner = hasWinner;
        result.victoryWinnerTeamId = winnerTeamId;

        for (int i = 0; i < victoryTeamIds.Count; i++)
        {
            result.victoryStars.Add(new MatchVictoryStarSaveData
            {
                teamId = victoryTeamIds[i],
                stars = i < victoryStars.Count ? Mathf.Max(0, victoryStars[i]) : 0
            });
        }

        return result;
    }

    public static void ApplyMatchStateSaveData(MatchController matchController, MatchStateSaveData data)
    {
        if (matchController == null || data == null)
            return;
        if (data.players == null || data.players.Count == 0)
            return;

        List<int> teamIds = new List<int>(data.players.Count);
        List<bool> flipXs = new List<bool>(data.players.Count);
        List<bool> isAIs = new List<bool>(data.players.Count);
        List<int> startMoneys = new List<int>(data.players.Count);
        List<int> actualMoneys = new List<int>(data.players.Count);
        List<int> incomePerTurns = new List<int>(data.players.Count);
        List<bool> startMoneyAppliedFlags = new List<bool>(data.players.Count);

        for (int i = 0; i < data.players.Count; i++)
        {
            MatchPlayerSaveData player = data.players[i];
            if (player == null)
                continue;

            teamIds.Add(player.teamId);
            flipXs.Add(player.flipX);
            isAIs.Add(player.isAI);
            startMoneys.Add(Mathf.Max(0, player.startMoney));
            actualMoneys.Add(Mathf.Max(0, player.actualMoney));
            incomePerTurns.Add(Mathf.Max(0, player.incomePerTurn));
            startMoneyAppliedFlags.Add(player.startMoneyApplied);
        }

        matchController.ImportPlayersState(
            teamIds,
            flipXs,
            isAIs,
            startMoneys,
            actualMoneys,
            incomePerTurns,
            startMoneyAppliedFlags,
            data.includeNeutralTeam);

        List<int> victoryTeamIds = new List<int>();
        List<int> victoryStars = new List<int>();
        if (data.victoryStars != null)
        {
            for (int i = 0; i < data.victoryStars.Count; i++)
            {
                MatchVictoryStarSaveData entry = data.victoryStars[i];
                if (entry == null)
                    continue;

                victoryTeamIds.Add(entry.teamId);
                victoryStars.Add(Mathf.Max(0, entry.stars));
            }
        }

        matchController.ImportVictoryStarsState(
            victoryTeamIds,
            victoryStars,
            data.victoryStarsEnabled,
            Mathf.Max(1, data.victoryStarsToWin),
            data.hasVictoryWinner,
            data.victoryWinnerTeamId);
    }

    public static UnitSaveData BuildUnitSaveData(UnitManager unit)
    {
        if (unit == null)
            return null;

        UnitSaveData item = new UnitSaveData
        {
            instanceId = unit.InstanceId,
            unitId = unit.UnitId,
            isActiveInHierarchy = unit.gameObject.activeInHierarchy,
            teamId = (int)unit.TeamId,
            cellX = unit.CurrentCellPosition.x,
            cellY = unit.CurrentCellPosition.y,
            worldX = unit.transform.position.x,
            worldY = unit.transform.position.y,
            currentHP = unit.CurrentHP,
            isDead = unit.IsDead,
            currentAmmo = unit.CurrentAmmo,
            currentFuel = unit.CurrentFuel,
            remainingMovementPoints = unit.RemainingMovementPoints,
            hasActed = unit.HasActed,
            receivedSuppliesThisTurn = unit.ReceivedSuppliesThisTurn,
            isEmbarked = unit.IsEmbarked,
            transporterInstanceId = unit.EmbarkedTransporter != null ? unit.EmbarkedTransporter.InstanceId : 0,
            transporterSlotIndex = unit.IsEmbarked ? unit.EmbarkedTransporterSlotIndex : -1,
            domain = (int)unit.GetDomain(),
            heightLevel = (int)unit.GetHeightLevel(),
            aiHasAssignedPlan = unit.AIHasAssignedPlan,
            aiAssignedPlanKey = unit.AIAssignedPlanKey,
            aiAssignedPlanName = unit.AIAssignedPlanName,
            aiAssignedPlanBadge = unit.AIAssignedPlanBadge,
            aiAssignedPlanRole = unit.AIAssignedPlanRole,
            aiAssignedPlanBadgeVisible = unit.AIAssignedPlanBadgeVisible
        };

        IReadOnlyList<UnitEmbarkedWeapon> embarkedWeapons = unit.GetEmbarkedWeapons();
        if (embarkedWeapons != null)
        {
            for (int weaponIndex = 0; weaponIndex < embarkedWeapons.Count; weaponIndex++)
            {
                UnitEmbarkedWeapon weapon = embarkedWeapons[weaponIndex];
                item.embarkedWeaponAmmo.Add(weapon != null ? Mathf.Max(0, weapon.squadAmmunition) : 0);
            }
        }

        IReadOnlyList<UnitEmbarkedSupply> supplies = unit.GetEmbarkedResources();
        if (supplies != null)
        {
            for (int supplyIndex = 0; supplyIndex < supplies.Count; supplyIndex++)
            {
                UnitEmbarkedSupply supply = supplies[supplyIndex];
                if (supply == null || supply.supply == null || string.IsNullOrWhiteSpace(supply.supply.id))
                    continue;

                item.embarkedSupplies.Add(new RuntimeSupplySaveData
                {
                    supplyId = supply.supply.id,
                    quantity = Mathf.Max(0, supply.amount)
                });
            }
        }

        return item;
    }

    public static void ApplyUnitSaveData(UnitManager unit, UnitSaveData saved)
    {
        if (unit == null || saved == null)
            return;

        unit.AssignSpawnInstanceId(saved.instanceId);
        unit.SetCurrentCellPosition(new Vector3Int(saved.cellX, saved.cellY, 0), enforceFinalOccupancyRule: false);
        unit.SetCurrentHP(saved.currentHP);
        unit.SetIsDead(saved.isDead);
        unit.SetCurrentAmmo(saved.currentAmmo);
        unit.SetCurrentFuel(saved.currentFuel);
        unit.SetReceivedSuppliesThisTurn(saved.receivedSuppliesThisTurn);
        if (saved.hasActed)
            unit.MarkAsActed();
        else
            unit.ResetActed();
        unit.SetRemainingMovementPoints(saved.remainingMovementPoints);

        Domain domain = (Domain)saved.domain;
        HeightLevel heightLevel = (HeightLevel)saved.heightLevel;
        unit.TrySetCurrentLayerMode(domain, heightLevel);

        IReadOnlyList<UnitEmbarkedWeapon> embarked = unit.GetEmbarkedWeapons();
        if (embarked != null && saved.embarkedWeaponAmmo != null)
        {
            int count = Mathf.Min(embarked.Count, saved.embarkedWeaponAmmo.Count);
            for (int weaponIndex = 0; weaponIndex < count; weaponIndex++)
            {
                if (embarked[weaponIndex] == null)
                    continue;
                embarked[weaponIndex].squadAmmunition = Mathf.Max(0, saved.embarkedWeaponAmmo[weaponIndex]);
            }
        }

        ApplySavedEmbarkedSupplies(unit, saved.embarkedSupplies);

        if (saved.aiHasAssignedPlan)
            unit.SetAIAssignedPlan(saved.aiAssignedPlanKey, saved.aiAssignedPlanName, saved.aiAssignedPlanBadge, saved.aiAssignedPlanRole, saved.aiAssignedPlanBadgeVisible);
        else
            unit.ClearAIAssignedPlan();
    }

    public static void ApplyUnitTurnFlagsFromSaveData(UnitManager unit, UnitSaveData saved)
    {
        if (unit == null || saved == null)
            return;

        if (saved.hasActed)
            unit.MarkAsActed();
        else
            unit.ResetActed();

        unit.SetRemainingMovementPoints(saved.remainingMovementPoints);
        unit.SetReceivedSuppliesThisTurn(saved.receivedSuppliesThisTurn);
    }

    public static void ApplySavedEmbarkedSupplies(UnitManager unit, List<RuntimeSupplySaveData> savedSupplies)
    {
        if (unit == null || savedSupplies == null || savedSupplies.Count <= 0)
            return;

        IReadOnlyList<UnitEmbarkedSupply> runtimeSupplies = unit.GetEmbarkedResources();
        if (runtimeSupplies == null || runtimeSupplies.Count <= 0)
            return;

        Dictionary<string, Queue<int>> amountsBySupplyId = new Dictionary<string, Queue<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < savedSupplies.Count; i++)
        {
            RuntimeSupplySaveData entry = savedSupplies[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.supplyId))
                continue;

            string key = entry.supplyId.Trim();
            if (!amountsBySupplyId.TryGetValue(key, out Queue<int> bucket))
            {
                bucket = new Queue<int>();
                amountsBySupplyId[key] = bucket;
            }

            bucket.Enqueue(Mathf.Max(0, entry.quantity));
        }

        for (int i = 0; i < runtimeSupplies.Count; i++)
        {
            UnitEmbarkedSupply runtime = runtimeSupplies[i];
            if (runtime == null || runtime.supply == null || string.IsNullOrWhiteSpace(runtime.supply.id))
                continue;

            string key = runtime.supply.id.Trim();
            if (!amountsBySupplyId.TryGetValue(key, out Queue<int> bucket) || bucket.Count <= 0)
                continue;

            runtime.amount = Mathf.Max(0, bucket.Dequeue());
        }
    }

    public static ConstructionSaveData BuildConstructionSaveData(ConstructionManager construction)
    {
        if (construction == null)
            return null;

        return new ConstructionSaveData
        {
            instanceId = construction.InstanceId,
            constructionId = construction.ConstructionId,
            isActiveInHierarchy = construction.gameObject.activeInHierarchy,
            teamId = (int)construction.TeamId,
            sector = (int)construction.Sector,
            cellX = construction.CurrentCellPosition.x,
            cellY = construction.CurrentCellPosition.y,
            worldX = construction.transform.position.x,
            worldY = construction.transform.position.y,
            currentCapturePoints = construction.CurrentCapturePoints,
            originalOwnerTeamId = (int)construction.OriginalOwnerTeamId,
            hasOriginalOwner = construction.HasOriginalOwner,
            firstOwnerTeamId = (int)construction.FirstOwnerTeamId,
            hasFirstOwner = construction.HasFirstOwner,
            hasInfiniteSuppliesOverride = construction.HasInfiniteSuppliesOverride,
            siteRuntime = BuildConstructionSiteRuntimeSaveData(construction.GetSiteRuntimeSnapshot())
        };
    }

    public static void ApplyConstructionSaveData(
        ConstructionManager manager,
        ConstructionSaveData saved,
        Func<ConstructionSiteRuntimeSaveData, ConstructionSiteRuntime> siteRuntimeResolver)
    {
        if (manager == null || saved == null)
            return;

        manager.AssignSpawnInstanceId(saved.instanceId);
        manager.SetSector(System.Enum.IsDefined(typeof(ConstructionSector), saved.sector) ? (ConstructionSector)saved.sector : manager.Sector);
        manager.SetCurrentCellPosition(new Vector3Int(saved.cellX, saved.cellY, 0));
        manager.ApplyOwnershipState(
            (TeamId)saved.teamId,
            (TeamId)saved.originalOwnerTeamId,
            saved.hasOriginalOwner,
            (TeamId)saved.firstOwnerTeamId,
            saved.hasFirstOwner);

        ConstructionSiteRuntime runtime = siteRuntimeResolver != null
            ? siteRuntimeResolver(saved.siteRuntime)
            : null;
        if (runtime != null)
            manager.ApplySiteRuntime(runtime);

        manager.SetCurrentCapturePoints(saved.currentCapturePoints);
        manager.SetInfiniteSuppliesOverride(saved.hasInfiniteSuppliesOverride);
    }

    public static ConstructionSiteRuntimeSaveData BuildConstructionSiteRuntimeSaveData(ConstructionSiteRuntime runtime)
    {
        if (runtime == null)
            return null;

        runtime.Sanitize();
        ConstructionSiteRuntimeSaveData result = new ConstructionSiteRuntimeSaveData
        {
            isPlayerHeadQuarter = runtime.isPlayerHeadQuarter,
            isVictoryBuilding = runtime.isVictoryBuilding,
            isCapturable = runtime.isCapturable,
            capturePointsMax = Mathf.Max(0, runtime.capturePointsMax),
            capturedIncoming = Mathf.Max(0, runtime.capturedIncoming),
            sellingRule = (int)runtime.sellingRule,
            canProvideSupplies = runtime.canProvideSupplies
        };

        if (runtime.offeredUnits != null)
        {
            for (int i = 0; i < runtime.offeredUnits.Count; i++)
            {
                UnitData unit = runtime.offeredUnits[i];
                if (unit == null || string.IsNullOrWhiteSpace(unit.id))
                    continue;
                result.offeredUnitIds.Add(unit.id);
            }
        }

        if (runtime.offeredServices != null)
        {
            for (int i = 0; i < runtime.offeredServices.Count; i++)
            {
                ServiceData service = runtime.offeredServices[i];
                if (service == null || string.IsNullOrWhiteSpace(service.id))
                    continue;
                result.offeredServiceIds.Add(service.id);
            }
        }

        if (runtime.offeredSupplies != null)
        {
            for (int i = 0; i < runtime.offeredSupplies.Count; i++)
            {
                ConstructionSupplyOffer offer = runtime.offeredSupplies[i];
                if (offer == null || offer.supply == null || string.IsNullOrWhiteSpace(offer.supply.id))
                    continue;
                result.offeredSupplies.Add(new RuntimeSupplySaveData
                {
                    supplyId = offer.supply.id,
                    quantity = Mathf.Max(0, offer.quantity)
                });
            }
        }

        return result;
    }

    public static ConstructionSiteRuntime BuildConstructionSiteRuntimeFromSaveData(
        ConstructionSiteRuntimeSaveData saved,
        Func<string, UnitData> unitResolver,
        Func<string, ServiceData> serviceResolver,
        Func<string, SupplyData> supplyResolver)
    {
        if (saved == null)
            return null;

        ConstructionSiteRuntime runtime = new ConstructionSiteRuntime
        {
            isPlayerHeadQuarter = saved.isPlayerHeadQuarter,
            isVictoryBuilding = saved.isVictoryBuilding,
            isCapturable = saved.isCapturable,
            capturePointsMax = Mathf.Max(0, saved.capturePointsMax),
            capturedIncoming = Mathf.Max(0, saved.capturedIncoming),
            sellingRule = Enum.IsDefined(typeof(ConstructionUnitMarketRule), saved.sellingRule)
                ? (ConstructionUnitMarketRule)saved.sellingRule
                : ConstructionUnitMarketRule.FreeMarket,
            canProvideSupplies = saved.canProvideSupplies,
            offeredUnits = new List<UnitData>(),
            offeredServices = new List<ServiceData>(),
            offeredSupplies = new List<ConstructionSupplyOffer>()
        };

        if (saved.offeredUnitIds != null)
        {
            for (int i = 0; i < saved.offeredUnitIds.Count; i++)
            {
                string id = saved.offeredUnitIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                UnitData unit = unitResolver != null ? unitResolver(id) : null;
                if (unit != null)
                    runtime.offeredUnits.Add(unit);
            }
        }

        if (saved.offeredServiceIds != null)
        {
            for (int i = 0; i < saved.offeredServiceIds.Count; i++)
            {
                string id = saved.offeredServiceIds[i];
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                ServiceData service = serviceResolver != null ? serviceResolver(id) : null;
                if (service != null)
                    runtime.offeredServices.Add(service);
            }
        }

        if (saved.offeredSupplies != null)
        {
            for (int i = 0; i < saved.offeredSupplies.Count; i++)
            {
                RuntimeSupplySaveData entry = saved.offeredSupplies[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.supplyId))
                    continue;

                SupplyData supply = supplyResolver != null ? supplyResolver(entry.supplyId) : null;
                if (supply == null)
                    continue;

                runtime.offeredSupplies.Add(new ConstructionSupplyOffer
                {
                    supply = supply,
                    quantity = Mathf.Max(0, entry.quantity)
                });
            }
        }

        runtime.Sanitize();
        return runtime;
    }
}

