using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    public enum HelperPanelKind
    {
        None = 0,
        Shopping = 1,
        Sensors = 2,
        Disembark = 3,
        Merge = 4,
        CommandService = 5
    }

    public sealed class HelperPanelData
    {
        public HelperPanelKind Kind = HelperPanelKind.None;
        public readonly List<HelperShoppingLine> ShoppingLines = new List<HelperShoppingLine>();
        public readonly List<HelperSensorLine> SensorLines = new List<HelperSensorLine>();
        public readonly List<HelperDisembarkOrderLine> DisembarkOrderLines = new List<HelperDisembarkOrderLine>();
        public readonly List<HelperDisembarkPassengerLine> DisembarkPassengerLines = new List<HelperDisembarkPassengerLine>();
        public readonly List<HelperMergeQueueLine> MergeQueueLines = new List<HelperMergeQueueLine>();
        public readonly List<HelperMergeCandidateLine> MergeCandidateLines = new List<HelperMergeCandidateLine>();
        public readonly List<HelperCommandServiceTargetLine> CommandServiceTargetLines = new List<HelperCommandServiceTargetLine>();
        public readonly List<HelperCommandServiceSkippedUnitLine> CommandServiceSkippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();
        public bool HasQueuedDisembarkOrders;
        public bool IsMergeConfirmStep;
        public bool HasSelectedMergeCandidate;
        public int SelectedMergeCandidateNumber;
        public string SelectedMergeCandidateName;
        public string SelectedMergeCandidateStats;
        public string MergeConfirmPreview;
        public string MergeQueuePreview;
        public int CommandServiceServedTargets;
        public int CommandServiceRecoveredHp;
        public int CommandServiceRecoveredFuel;
        public int CommandServiceRecoveredAmmo;
        public int CommandServiceTotalCost;
        public bool CommandServiceStoppedByEconomy;
        public bool CommandServiceIsEstimate;
        public int CommandServiceMoneyBefore;
        public int CommandServiceMoneyAfter;
    }

    private float commandServiceHelperVisibleUntil = -1f;
    private int commandServiceHelperServedTargets;
    private int commandServiceHelperRecoveredHp;
    private int commandServiceHelperRecoveredFuel;
    private int commandServiceHelperRecoveredAmmo;
    private int commandServiceHelperTotalCost;
    private bool commandServiceHelperStoppedByEconomy;
    private bool commandServiceHelperIsEstimate;
    private int commandServiceHelperMoneyBefore;
    private int commandServiceHelperMoneyAfter;
    private readonly List<HelperCommandServiceTargetLine> commandServiceHelperTargetLines = new List<HelperCommandServiceTargetLine>();
    private readonly List<HelperCommandServiceSkippedUnitLine> commandServiceHelperSkippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();

    public sealed class HelperShoppingLine
    {
        public int index;
        public string unitName;
        public int? cost;
    }

    public sealed class HelperSensorLine
    {
        public char actionCode;
        public string sensorKey;
    }

    public sealed class HelperDisembarkOrderLine
    {
        public int index;
        public string unitName;
        public string stats;
        public string terrainName;
    }

    public sealed class HelperDisembarkPassengerLine
    {
        public int index;
        public string unitName;
        public string stats;
    }

    public sealed class HelperMergeQueueLine
    {
        public int index;
        public string unitName;
        public string stats;
    }

    public sealed class HelperMergeCandidateLine
    {
        public int index;
        public string unitName;
        public string stats;
        public bool isValid;
        public string invalidReason;
    }

    public sealed class HelperCommandServiceTargetLine
    {
        public string unitName;
        public string sourceLabel;
        public string gainsLabel;
        public bool isFocused;
    }

    public sealed class HelperCommandServiceSkippedUnitLine
    {
        public string unitName;
        public string sourceLabel;
        public bool isFocused;
    }

    public bool TryBuildHelperPanelData(out HelperPanelData data)
    {
        data = new HelperPanelData();

        if (TryBuildCommandServiceHelperPanelData(data))
            return true;

        if (cursorState == CursorState.Neutral)
            return false;

        if (cursorState == CursorState.ShoppingAndServices)
            return TryBuildShoppingHelperPanelData(data);

        if (cursorState == CursorState.Desembarcando)
            return TryBuildDisembarkHelperPanelData(data);

        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            return TryBuildSensorsHelperPanelData(data);

        if (cursorState == CursorState.Fundindo)
            return TryBuildMergeHelperPanelData(data);

        return false;
    }

    private bool TryBuildCommandServiceHelperPanelData(HelperPanelData data)
    {
        bool shouldShowEstimate = commandServiceConfirmationPending && commandServiceHelperServedTargets > 0;
        bool shouldShowSummary = !commandServiceConfirmationPending &&
            Time.time <= commandServiceHelperVisibleUntil &&
            commandServiceHelperServedTargets > 0;
        if (data == null || (!shouldShowEstimate && !shouldShowSummary))
            return false;

        data.Kind = HelperPanelKind.CommandService;
        data.CommandServiceServedTargets = Mathf.Max(0, commandServiceHelperServedTargets);
        data.CommandServiceRecoveredHp = Mathf.Max(0, commandServiceHelperRecoveredHp);
        data.CommandServiceRecoveredFuel = Mathf.Max(0, commandServiceHelperRecoveredFuel);
        data.CommandServiceRecoveredAmmo = Mathf.Max(0, commandServiceHelperRecoveredAmmo);
        data.CommandServiceTotalCost = Mathf.Max(0, commandServiceHelperTotalCost);
        data.CommandServiceStoppedByEconomy = commandServiceHelperStoppedByEconomy;
        data.CommandServiceIsEstimate = commandServiceHelperIsEstimate;
        data.CommandServiceMoneyBefore = Mathf.Max(0, commandServiceHelperMoneyBefore);
        data.CommandServiceMoneyAfter = Mathf.Max(0, commandServiceHelperMoneyAfter);
        for (int i = 0; i < commandServiceHelperTargetLines.Count; i++)
        {
            HelperCommandServiceTargetLine line = commandServiceHelperTargetLines[i];
            if (line == null)
                continue;

            data.CommandServiceTargetLines.Add(new HelperCommandServiceTargetLine
            {
                unitName = line.unitName,
                sourceLabel = line.sourceLabel,
                gainsLabel = line.gainsLabel,
                isFocused = line.isFocused
            });
        }

        for (int i = 0; i < commandServiceHelperSkippedUnitLines.Count; i++)
        {
            HelperCommandServiceSkippedUnitLine line = commandServiceHelperSkippedUnitLines[i];
            if (line == null)
                continue;

            data.CommandServiceSkippedUnitLines.Add(new HelperCommandServiceSkippedUnitLine
            {
                unitName = line.unitName,
                sourceLabel = line.sourceLabel,
                isFocused = line.isFocused
            });
        }
        return true;
    }

    private void ShowCommandServiceHelperSummary(
        int servedTargets,
        int recoveredHp,
        int recoveredFuel,
        int recoveredAmmo,
        int totalCost,
        bool stoppedByEconomy,
        float durationSeconds = 3.2f)
    {
        commandServiceHelperServedTargets = Mathf.Max(0, servedTargets);
        commandServiceHelperRecoveredHp = Mathf.Max(0, recoveredHp);
        commandServiceHelperRecoveredFuel = Mathf.Max(0, recoveredFuel);
        commandServiceHelperRecoveredAmmo = Mathf.Max(0, recoveredAmmo);
        commandServiceHelperTotalCost = Mathf.Max(0, totalCost);
        commandServiceHelperStoppedByEconomy = stoppedByEconomy;
        commandServiceHelperIsEstimate = false;
        commandServiceHelperMoneyBefore = 0;
        commandServiceHelperMoneyAfter = 0;
        commandServiceHelperTargetLines.Clear();
        commandServiceHelperSkippedUnitLines.Clear();
        commandServiceHelperVisibleUntil = Time.time + Mathf.Max(0.1f, durationSeconds);
    }

    private void ShowCommandServiceHelperEstimate(
        int servedTargets,
        int recoveredHp,
        int recoveredFuel,
        int recoveredAmmo,
        int totalCost,
        bool stoppedByEconomy,
        int moneyBefore,
        int moneyAfter,
        List<HelperCommandServiceTargetLine> targetLines = null,
        List<HelperCommandServiceSkippedUnitLine> skippedUnitLines = null)
    {
        commandServiceHelperServedTargets = Mathf.Max(0, servedTargets);
        commandServiceHelperRecoveredHp = Mathf.Max(0, recoveredHp);
        commandServiceHelperRecoveredFuel = Mathf.Max(0, recoveredFuel);
        commandServiceHelperRecoveredAmmo = Mathf.Max(0, recoveredAmmo);
        commandServiceHelperTotalCost = Mathf.Max(0, totalCost);
        commandServiceHelperStoppedByEconomy = stoppedByEconomy;
        commandServiceHelperIsEstimate = true;
        commandServiceHelperMoneyBefore = Mathf.Max(0, moneyBefore);
        commandServiceHelperMoneyAfter = Mathf.Max(0, moneyAfter);
        commandServiceHelperTargetLines.Clear();
        if (targetLines != null)
        {
            for (int i = 0; i < targetLines.Count; i++)
            {
                HelperCommandServiceTargetLine line = targetLines[i];
                if (line == null)
                    continue;

                commandServiceHelperTargetLines.Add(new HelperCommandServiceTargetLine
                {
                    unitName = line.unitName,
                    sourceLabel = line.sourceLabel,
                    gainsLabel = line.gainsLabel,
                    isFocused = line.isFocused
                });
            }
        }
        commandServiceHelperSkippedUnitLines.Clear();
        if (skippedUnitLines != null)
        {
            for (int i = 0; i < skippedUnitLines.Count; i++)
            {
                HelperCommandServiceSkippedUnitLine line = skippedUnitLines[i];
                if (line == null)
                    continue;

                commandServiceHelperSkippedUnitLines.Add(new HelperCommandServiceSkippedUnitLine
                {
                    unitName = line.unitName,
                    sourceLabel = line.sourceLabel,
                    isFocused = line.isFocused
                });
            }
        }
        commandServiceHelperVisibleUntil = -1f;
    }

    private void ClearCommandServiceHelper()
    {
        commandServiceHelperVisibleUntil = -1f;
        commandServiceHelperServedTargets = 0;
        commandServiceHelperRecoveredHp = 0;
        commandServiceHelperRecoveredFuel = 0;
        commandServiceHelperRecoveredAmmo = 0;
        commandServiceHelperTotalCost = 0;
        commandServiceHelperStoppedByEconomy = false;
        commandServiceHelperIsEstimate = false;
        commandServiceHelperMoneyBefore = 0;
        commandServiceHelperMoneyAfter = 0;
        commandServiceHelperTargetLines.Clear();
        commandServiceHelperSkippedUnitLines.Clear();
    }

    private bool TryBuildShoppingHelperPanelData(HelperPanelData data)
    {
        if (data == null || shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
            return false;

        data.Kind = HelperPanelKind.Shopping;

        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData unit = shoppingUnitsForSale[i];
            if (unit == null)
                continue;

            int? resolvedCost = null;
            if (matchController != null)
                resolvedCost = matchController.ResolveEconomyCost(unit.cost);

            data.ShoppingLines.Add(new HelperShoppingLine
            {
                index = i + 1,
                unitName = ResolveUnitName(unit),
                cost = resolvedCost
            });
        }

        return data.ShoppingLines.Count > 0;
    }

    private bool TryBuildSensorsHelperPanelData(HelperPanelData data)
    {
        if (data == null || scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return false;

        data.Kind = HelperPanelKind.Sensors;
        TryAddSensorLine(data, 'A', "aim");
        TryAddSensorLine(data, 'E', "embark");
        TryAddSensorLine(data, 'D', "disembark");
        TryAddSensorLine(data, 'C', "capture");
        TryAddSensorLine(data, 'F', "fuse");
        TryAddSensorLine(data, 'S', "supply");
        TryAddSensorLine(data, 'T', "transfer");
        TryAddSensorLine(data, 'L', "layer");
        TryAddSensorLine(data, 'M', "move_only", forceInclude: true);

        return data.SensorLines.Count > 0;
    }

    private void TryAddSensorLine(HelperPanelData data, char actionCode, string sensorKey, bool forceInclude = false)
    {
        if (data == null)
            return;
        if (!forceInclude && (availableSensorActionCodes == null || !availableSensorActionCodes.Contains(actionCode)))
            return;

        data.SensorLines.Add(new HelperSensorLine
        {
            actionCode = actionCode,
            sensorKey = sensorKey ?? string.Empty
        });
    }

    private bool TryBuildDisembarkHelperPanelData(HelperPanelData data)
    {
        if (data == null || disembarkPassengerEntries == null || disembarkPassengerEntries.Count <= 0)
            return false;

        data.Kind = HelperPanelKind.Disembark;

        if (disembarkQueuedOrders != null && disembarkQueuedOrders.Count > 0)
        {
            for (int i = 0; i < disembarkQueuedOrders.Count; i++)
            {
                DisembarkOrder order = disembarkQueuedOrders[i];
                if (order == null || order.passenger == null)
                    continue;

                data.DisembarkOrderLines.Add(new HelperDisembarkOrderLine
                {
                    index = i,
                    unitName = ResolveUnitRuntimeName(order.passenger),
                    stats = BuildUnitStatInline(order.passenger),
                    terrainName = ResolveTerrainLabelForCell(order.targetCell)
                });
            }
        }

        for (int i = 0; i < disembarkPassengerEntries.Count; i++)
        {
            DisembarkPassengerEntry entry = disembarkPassengerEntries[i];
            if (entry == null || entry.passenger == null)
                continue;

            data.DisembarkPassengerLines.Add(new HelperDisembarkPassengerLine
            {
                index = entry.selectionNumber,
                unitName = ResolveUnitRuntimeName(entry.passenger),
                stats = BuildUnitStatInline(entry.passenger)
            });
        }

        data.HasQueuedDisembarkOrders = data.DisembarkOrderLines.Count > 0;
        return data.DisembarkPassengerLines.Count > 0;
    }

    private bool TryBuildMergeHelperPanelData(HelperPanelData data)
    {
        bool isMergeAnimating = animationManager != null && animationManager.IsAnimatingMovement;
        if (data == null || cursorState != CursorState.Fundindo || mergeExecutionInProgress || isMergeAnimating)
            return false;

        data.Kind = HelperPanelKind.Merge;
        data.IsMergeConfirmStep = scannerPromptStep == ScannerPromptStep.MergeConfirm;

        if (mergeQueuedUnits != null && mergeQueuedUnits.Count > 0)
        {
            for (int i = 0; i < mergeQueuedUnits.Count; i++)
            {
                UnitManager unit = mergeQueuedUnits[i];
                if (unit == null)
                    continue;

                data.MergeQueueLines.Add(new HelperMergeQueueLine
                {
                    index = i + 1,
                    unitName = ResolveUnitRuntimeName(unit),
                    stats = BuildUnitStatInline(unit)
                });
            }
        }

        if (mergeCandidateEntries != null && mergeCandidateEntries.Count > 0)
        {
            for (int i = 0; i < mergeCandidateEntries.Count; i++)
            {
                MergeCandidateEntry entry = mergeCandidateEntries[i];
                if (entry == null || entry.unit == null)
                    continue;

                data.MergeCandidateLines.Add(new HelperMergeCandidateLine
                {
                    index = entry.selectionNumber,
                    unitName = ResolveUnitRuntimeName(entry.unit),
                    stats = BuildUnitStatInline(entry.unit),
                    isValid = entry.isValid,
                    invalidReason = ResolveMergeInvalidReason(entry)
                });
            }
        }

        if (data.IsMergeConfirmStep &&
            mergeSelectedCandidateIndex >= 0 &&
            mergeSelectedCandidateIndex < mergeCandidateEntries.Count)
        {
            MergeCandidateEntry selected = mergeCandidateEntries[mergeSelectedCandidateIndex];
            if (selected != null && selected.unit != null)
            {
                data.HasSelectedMergeCandidate = true;
                data.SelectedMergeCandidateNumber = selected.selectionNumber;
                data.SelectedMergeCandidateName = ResolveUnitRuntimeName(selected.unit);
                data.SelectedMergeCandidateStats = BuildUnitStatInline(selected.unit);
                data.MergeConfirmPreview = BuildMergePreviewInline(selected.unit);
            }
        }

        if (data.MergeQueueLines.Count > 0)
            data.MergeQueuePreview = BuildMergePreviewInline(null);

        return data.MergeQueueLines.Count > 0 || data.MergeCandidateLines.Count > 0 || data.HasSelectedMergeCandidate;
    }

    private string BuildUnitStatInline(UnitManager unit)
    {
        if (unit == null)
            return "-";

        int hp = Mathf.Max(0, unit.CurrentHP);
        int fuel = Mathf.Max(0, unit.CurrentFuel);
        List<string> segments = new List<string>
        {
            $"{hp}HP",
            $"{fuel}F"
        };

        AppendWeaponStatSegments(segments, unit);
        AppendSupplyStatSegments(segments, unit);
        return segments.Count > 0 ? string.Join(" | ", segments) : "-";
    }

    private void AppendWeaponStatSegments(List<string> segments, UnitManager unit)
    {
        if (segments == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> runtimeWeapons = unit.GetEmbarkedWeapons();
        List<UnitEmbarkedWeapon> baselineWeapons = null;
        if (unit.TryGetUnitData(out UnitData data) && data != null)
            baselineWeapons = data.embarkedWeapons;

        int maxEntries = Mathf.Max(runtimeWeapons != null ? runtimeWeapons.Count : 0, baselineWeapons != null ? baselineWeapons.Count : 0);
        int weaponCounter = 0;
        for (int i = 0; i < maxEntries; i++)
        {
            UnitEmbarkedWeapon runtime = runtimeWeapons != null && i < runtimeWeapons.Count ? runtimeWeapons[i] : null;
            UnitEmbarkedWeapon baseline = baselineWeapons != null && i < baselineWeapons.Count ? baselineWeapons[i] : null;
            bool hasWeapon = (runtime != null && runtime.weapon != null) || (baseline != null && baseline.weapon != null);
            if (!hasWeapon)
                continue;

            weaponCounter++;
            int currentAmmo = runtime != null ? Mathf.Max(0, runtime.squadAmmunition) : 0;
            segments.Add($"W{weaponCounter}:{currentAmmo}");
        }
    }

    private void AppendSupplyStatSegments(List<string> segments, UnitManager unit)
    {
        if (segments == null || unit == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return;

        int supplyCounter = 0;
        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply entry = resources[i];
            if (entry == null || entry.supply == null)
                continue;

            supplyCounter++;
            int amount = Mathf.Max(0, entry.amount);
            segments.Add($"R{supplyCounter}:{amount}");
        }
    }

    private string BuildMergePreviewInline(UnitManager candidateOrNull)
    {
        if (selectedUnit == null)
            return string.Empty;

        List<UnitManager> participants = new List<UnitManager>();
        if (mergeQueuedUnits != null)
        {
            for (int i = 0; i < mergeQueuedUnits.Count; i++)
            {
                UnitManager queued = mergeQueuedUnits[i];
                if (queued == null || queued == selectedUnit || participants.Contains(queued))
                    continue;
                participants.Add(queued);
            }
        }

        if (candidateOrNull != null && candidateOrNull != selectedUnit && !participants.Contains(candidateOrNull))
            participants.Add(candidateOrNull);

        if (participants.Count <= 0)
            return string.Empty;

        int baseHp = Mathf.Max(0, selectedUnit.CurrentHP);
        int baseAutonomy = Mathf.Max(0, selectedUnit.CurrentFuel);
        int baseSteps = baseHp * baseAutonomy;

        int participantsHp = 0;
        int participantsSteps = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            UnitManager participant = participants[i];
            if (participant == null)
                continue;

            int hp = Mathf.Max(0, participant.CurrentHP);
            int autonomy = Mathf.Max(0, participant.CurrentFuel);
            participantsHp += hp;
            participantsSteps += hp * autonomy;
        }

        int resultHp = Mathf.Min(10, baseHp + participantsHp);
        int totalSteps = baseSteps + participantsSteps;
        int resultAutonomy = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;

        Dictionary<WeaponData, int> projectilesByWeapon = BuildMergeWeaponProjectileTotals(selectedUnit, participants);
        Dictionary<SupplyData, int> supplyStepsByType = BuildMergeSupplyStepTotals(selectedUnit, participants);

        List<string> segments = new List<string>
        {
            $"{resultHp}HP",
            $"{resultAutonomy}F"
        };

        AppendMergeResultWeaponSegments(segments, selectedUnit, resultHp, projectilesByWeapon);
        AppendMergeResultSupplySegments(segments, selectedUnit, resultHp, supplyStepsByType);
        return string.Join(" | ", segments);
    }

    private static void AppendMergeResultWeaponSegments(
        List<string> segments,
        UnitManager baseUnit,
        int resultHp,
        Dictionary<WeaponData, int> projectilesByWeapon)
    {
        if (segments == null || baseUnit == null || projectilesByWeapon == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> baseWeapons = baseUnit.GetEmbarkedWeapons();
        if (baseWeapons == null)
            return;

        int weaponCounter = 0;
        for (int i = 0; i < baseWeapons.Count; i++)
        {
            UnitEmbarkedWeapon runtime = baseWeapons[i];
            if (runtime == null || runtime.weapon == null)
                continue;

            weaponCounter++;
            int projectedAmmo = 0;
            if (projectilesByWeapon.TryGetValue(runtime.weapon, out int totalProjectiles) && resultHp > 0)
                projectedAmmo = Mathf.Max(0, totalProjectiles / resultHp);
            segments.Add($"W{weaponCounter}:{projectedAmmo}");
        }
    }

    private static void AppendMergeResultSupplySegments(
        List<string> segments,
        UnitManager baseUnit,
        int resultHp,
        Dictionary<SupplyData, int> supplyStepsByType)
    {
        if (segments == null || baseUnit == null || supplyStepsByType == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> baseSupplies = baseUnit.GetEmbarkedResources();
        if (baseSupplies == null)
            return;

        int supplyCounter = 0;
        for (int i = 0; i < baseSupplies.Count; i++)
        {
            UnitEmbarkedSupply runtime = baseSupplies[i];
            if (runtime == null || runtime.supply == null)
                continue;

            supplyCounter++;
            int projectedAmount = 0;
            if (supplyStepsByType.TryGetValue(runtime.supply, out int totalSteps) && resultHp > 0)
                projectedAmount = Mathf.Max(0, totalSteps / resultHp);
            segments.Add($"R{supplyCounter}:{projectedAmount}");
        }
    }

    private string ResolveTerrainLabelForCell(Vector3Int cell)
    {
        Tilemap map = terrainTilemap;
        if (map == null && selectedUnit != null)
            map = selectedUnit.BoardTilemap;

        if (TryResolveTerrainAtCell(map, terrainDatabase, cell, out TerrainTypeData terrain) && terrain != null)
            return ResolveTerrainName(terrain);

        return $"({cell.x},{cell.y})";
    }
}
