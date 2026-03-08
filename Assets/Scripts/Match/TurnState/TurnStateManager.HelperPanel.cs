using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    [Header("Helper - Inspection")]
    [SerializeField] [Range(0.5f, 20f)] private float inspectedHelperDurationSeconds = 4f;
    [Header("Helper - Turn Start Autonomy")]
    [SerializeField] [Range(0.5f, 20f)] private float turnStartAutonomyHelperDurationSeconds = 6f;

    public enum HelperPanelKind
    {
        None = 0,
        Shopping = 1,
        Sensors = 2,
        Disembark = 3,
        Merge = 4,
        CommandService = 5,
        UnitStats = 6,
        Embark = 7,
        Supply = 8,
        TurnStartAutonomy = 9
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
        public readonly List<HelperEmbarkCandidateLine> EmbarkCandidateLines = new List<HelperEmbarkCandidateLine>();
        public readonly List<HelperSupplyTargetLine> SupplyTargetLines = new List<HelperSupplyTargetLine>();
        public readonly List<HelperSupplyResourceLine> SupplyResourceLines = new List<HelperSupplyResourceLine>();
        public readonly List<HelperCommandServiceTargetLine> CommandServiceTargetLines = new List<HelperCommandServiceTargetLine>();
        public readonly List<HelperCommandServiceSkippedUnitLine> CommandServiceSkippedUnitLines = new List<HelperCommandServiceSkippedUnitLine>();
        public readonly List<HelperTurnStartAutonomyLine> TurnStartAutonomyLines = new List<HelperTurnStartAutonomyLine>();
        public string UnitStatsName;
        public readonly List<string> UnitStatsLines = new List<string>();
        public int SupplyServedTargets;
        public int SupplyRecoveredHp;
        public int SupplyRecoveredFuel;
        public int SupplyRecoveredAmmo;
        public int SupplyTotalCost;
        public bool SupplyIsConfirmStep;
        public bool SupplyHasQueuedOrders;
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
    private UnitManager inspectedHelperUnit;
    private float inspectedHelperVisibleUntil = -1f;
    private int inspectedHelperActivatedFrame = -1;
    private Vector3Int inspectedHelperCursorCell;
    private readonly List<HelperTurnStartAutonomyLine> turnStartAutonomyHelperLines = new List<HelperTurnStartAutonomyLine>();
    private float turnStartAutonomyHelperVisibleUntil = -1f;
    private int turnStartAutonomyHelperActivatedFrame = -1;
    private Vector3Int turnStartAutonomyHelperCursorCell;

    public readonly struct TurnStartAutonomyUpkeepEntry
    {
        public readonly string unitName;
        public readonly Vector3Int cell;
        public readonly int autonomyConsumed;
        public readonly int fuelBefore;
        public readonly int fuelAfter;

        public TurnStartAutonomyUpkeepEntry(string unitName, Vector3Int cell, int autonomyConsumed, int fuelBefore, int fuelAfter)
        {
            this.unitName = unitName ?? string.Empty;
            this.cell = cell;
            this.autonomyConsumed = Mathf.Max(0, autonomyConsumed);
            this.fuelBefore = Mathf.Max(0, fuelBefore);
            this.fuelAfter = Mathf.Max(0, fuelAfter);
        }
    }

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

    public sealed class HelperEmbarkCandidateLine
    {
        public int index;
        public string unitName;
        public string stats;
        public bool isValid;
        public string invalidReason;
        public bool isFocused;
    }

    public sealed class HelperSupplyTargetLine
    {
        public int index;
        public string unitName;
        public string gainsLabel;
        public int estimatedCost;
        public bool isFocused;
    }

    public sealed class HelperSupplyResourceLine
    {
        public string supplyName;
        public int beforeAmount;
        public int afterAmount;
    }

    private sealed class SupplyEstimateLine
    {
        public UnitManager target;
        public int hp;
        public int fuel;
        public int ammo;
        public int cost;
        public bool isFocused;
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

    public sealed class HelperTurnStartAutonomyLine
    {
        public string unitName;
        public int autonomyConsumed;
        public int fuelBefore;
        public int fuelAfter;
        public Vector3Int cell;
    }

    public bool TryBuildHelperPanelData(out HelperPanelData data)
    {
        data = new HelperPanelData();

        if (TryBuildCommandServiceHelperPanelData(data))
            return true;

        if (TryBuildTurnStartAutonomyHelperPanelData(data))
            return true;

        if (TryBuildUnitStatsHelperPanelData(data))
            return true;

        if (cursorState == CursorState.Neutral)
            return false;

        if (cursorState == CursorState.ShoppingAndServices)
            return TryBuildShoppingHelperPanelData(data);

        if (cursorState == CursorState.Desembarcando)
            return TryBuildDisembarkHelperPanelData(data);

        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            return TryBuildSensorsHelperPanelData(data);

        if (cursorState == CursorState.Suprindo)
            return TryBuildSupplyHelperPanelData(data);

        if (cursorState == CursorState.Embarcando)
            return TryBuildEmbarkHelperPanelData(data);

        if (cursorState == CursorState.Fundindo)
            return TryBuildMergeHelperPanelData(data);

        return false;
    }

    public void ShowTurnStartAutonomyUpkeepHelper(IReadOnlyList<TurnStartAutonomyUpkeepEntry> entries)
    {
        turnStartAutonomyHelperLines.Clear();
        if (entries == null || entries.Count <= 0)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            TurnStartAutonomyUpkeepEntry entry = entries[i];
            if (entry.autonomyConsumed <= 0)
                continue;

            turnStartAutonomyHelperLines.Add(new HelperTurnStartAutonomyLine
            {
                unitName = entry.unitName ?? string.Empty,
                autonomyConsumed = Mathf.Max(0, entry.autonomyConsumed),
                fuelBefore = Mathf.Max(0, entry.fuelBefore),
                fuelAfter = Mathf.Max(0, entry.fuelAfter),
                cell = entry.cell
            });
        }

        if (turnStartAutonomyHelperLines.Count <= 0)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        turnStartAutonomyHelperVisibleUntil = Time.time + Mathf.Max(0.1f, turnStartAutonomyHelperDurationSeconds);
        turnStartAutonomyHelperActivatedFrame = Time.frameCount;
        turnStartAutonomyHelperCursorCell = cursorController != null ? cursorController.CurrentCell : default;
    }

    private bool TryBuildTurnStartAutonomyHelperPanelData(HelperPanelData data)
    {
        if (data == null || !IsTurnStartAutonomyHelperActive())
            return false;

        data.Kind = HelperPanelKind.TurnStartAutonomy;
        for (int i = 0; i < turnStartAutonomyHelperLines.Count; i++)
            data.TurnStartAutonomyLines.Add(turnStartAutonomyHelperLines[i]);

        return data.TurnStartAutonomyLines.Count > 0;
    }

    private bool IsTurnStartAutonomyHelperActive()
    {
        return turnStartAutonomyHelperLines.Count > 0 &&
               turnStartAutonomyHelperVisibleUntil > 0f &&
               Time.time <= turnStartAutonomyHelperVisibleUntil;
    }

    private void ClearTurnStartAutonomyHelper()
    {
        turnStartAutonomyHelperLines.Clear();
        turnStartAutonomyHelperVisibleUntil = -1f;
        turnStartAutonomyHelperActivatedFrame = -1;
        turnStartAutonomyHelperCursorCell = default;
    }

    private bool TryBuildUnitStatsHelperPanelData(HelperPanelData data)
    {
        if (data == null)
            return false;

        UnitManager unit = null;
        if (cursorState == CursorState.UnitSelected && selectedUnit != null)
        {
            unit = selectedUnit;
        }
        else if (cursorState == CursorState.Neutral && IsInspectedHelperActive())
        {
            unit = inspectedHelperUnit;
        }

        if (unit == null)
            return false;

        data.Kind = HelperPanelKind.UnitStats;
        data.UnitStatsName = ResolveUnitRuntimeName(unit);

        int hpCurrent = Mathf.Max(0, unit.CurrentHP);
        int hpMax = Mathf.Max(1, unit.GetMaxHP());
        int movement = 0;
        if (unit.TryGetUnitData(out UnitData selectedData) && selectedData != null)
            movement = Mathf.Max(0, selectedData.movement);
        else
            movement = Mathf.Max(0, unit.MaxMovementPoints);
        int fuelCurrent = Mathf.Max(0, unit.CurrentFuel);
        int fuelMax = Mathf.Max(1, unit.GetMaxFuel());

        data.UnitStatsLines.Add($"HP: {hpCurrent}/{hpMax}");
        data.UnitStatsLines.Add($"MOV: {movement}");
        data.UnitStatsLines.Add($"AUT: {fuelCurrent}/{fuelMax}");

        IReadOnlyList<UnitTransportSeatRuntime> seats = unit.TransportedUnitSlots;
        bool hasPassengers = false;
        if (seats != null)
        {
            for (int i = 0; i < seats.Count; i++)
            {
                UnitManager passenger = seats[i] != null ? seats[i].embarkedUnit : null;
                if (passenger != null && passenger.IsEmbarked && passenger.EmbarkedTransporter == unit)
                {
                    hasPassengers = true;
                    break;
                }
            }
        }

        if (hasPassengers)
        {
            data.UnitStatsLines.Add(string.Empty);
            data.UnitStatsLines.Add("Transportando");
            AppendTransportedUnitStatsLines(data.UnitStatsLines, unit, depth: 0);
        }

        IReadOnlyList<UnitEmbarkedSupply> resources = unit.GetEmbarkedResources();
        if (resources != null && resources.Count > 0)
        {
            data.UnitStatsLines.Add(string.Empty);
            data.UnitStatsLines.Add("Carroceria");
            AppendSupplierStockLines(data.UnitStatsLines, unit);
        }

        return data.UnitStatsLines.Count > 0;
    }

    private bool IsInspectedHelperActive()
    {
        return inspectedHelperUnit != null &&
            inspectedHelperVisibleUntil > 0f &&
            Time.time <= inspectedHelperVisibleUntil;
    }

    private void BeginInspectedHelper(UnitManager unit)
    {
        if (unit == null || cursorController == null)
            return;

        inspectedHelperUnit = unit;
        inspectedHelperVisibleUntil = Time.time + Mathf.Max(0.1f, inspectedHelperDurationSeconds);
        inspectedHelperActivatedFrame = Time.frameCount;
        inspectedHelperCursorCell = cursorController.CurrentCell;
    }

    private void ClearInspectedHelper()
    {
        inspectedHelperUnit = null;
        inspectedHelperVisibleUntil = -1f;
        inspectedHelperActivatedFrame = -1;
        inspectedHelperCursorCell = default;
    }

    private void UpdateInspectedHelperAutoDismiss()
    {
        UpdateTurnStartAutonomyHelperAutoDismiss();

        if (!IsInspectedHelperActive())
        {
            if (inspectedHelperUnit != null)
                ClearInspectedHelper();
            return;
        }

        if (Time.frameCount <= inspectedHelperActivatedFrame)
            return;

        if (cursorController != null && cursorController.CurrentCell != inspectedHelperCursorCell)
        {
            ClearInspectedHelper();
            return;
        }

        bool anyInput = WasAnyInputPressedThisFrame();
        if (anyInput)
            ClearInspectedHelper();
    }

    private void UpdateTurnStartAutonomyHelperAutoDismiss()
    {
        if (!IsTurnStartAutonomyHelperActive())
        {
            if (turnStartAutonomyHelperLines.Count > 0)
                ClearTurnStartAutonomyHelper();
            return;
        }

        if (Time.frameCount <= turnStartAutonomyHelperActivatedFrame)
            return;

        if (cursorController != null && cursorController.CurrentCell != turnStartAutonomyHelperCursorCell)
        {
            ClearTurnStartAutonomyHelper();
            return;
        }

        if (WasAnyInputPressedThisFrame())
            ClearTurnStartAutonomyHelper();
    }

    private static bool WasAnyInputPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            return true;

        Mouse mouse = Mouse.current;
        if (mouse != null &&
            (mouse.leftButton.wasPressedThisFrame ||
             mouse.rightButton.wasPressedThisFrame ||
             mouse.middleButton.wasPressedThisFrame))
            return true;

        return false;
#else
        return Input.anyKeyDown ||
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1) ||
            Input.GetMouseButtonDown(2);
#endif
    }

    private void AppendTransportedUnitStatsLines(List<string> lines, UnitManager transporter, int depth)
    {
        if (lines == null || transporter == null)
            return;

        IReadOnlyList<UnitTransportSeatRuntime> seats = transporter.TransportedUnitSlots;
        if (seats == null || seats.Count <= 0)
            return;

        string indent = new string(' ', Mathf.Max(0, depth) * 4);
        for (int i = 0; i < seats.Count; i++)
        {
            UnitManager passenger = seats[i] != null ? seats[i].embarkedUnit : null;
            if (passenger == null || !passenger.IsEmbarked || passenger.EmbarkedTransporter != transporter)
                continue;

            lines.Add($"{indent}{ResolveUnitRuntimeName(passenger)} ({BuildUnitStatInline(passenger)})");
            AppendTransportedUnitStatsLines(lines, passenger, depth + 1);
        }
    }

    private void AppendSupplierStockLines(List<string> lines, UnitManager supplier)
    {
        if (lines == null || supplier == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> resources = supplier.GetEmbarkedResources();
        if (resources == null || resources.Count <= 0)
            return;

        for (int i = 0; i < resources.Count; i++)
        {
            UnitEmbarkedSupply runtime = resources[i];
            if (runtime == null || runtime.supply == null)
                continue;

            int current = Mathf.Max(0, runtime.amount);
            int max = ResolveSupplierResourceMaxAmount(supplier, runtime.supply, current);
            string label = ResolveSupplyDisplayName(runtime.supply);
            lines.Add($"{current}/{max} {label}");
        }
    }

    private static int ResolveSupplierResourceMaxAmount(UnitManager supplier, SupplyData supply, int fallbackCurrent)
    {
        if (supplier != null && supply != null && supplier.TryGetUnitData(out UnitData data) && data != null && data.supplierResources != null)
        {
            for (int i = 0; i < data.supplierResources.Count; i++)
            {
                UnitEmbarkedSupply baseline = data.supplierResources[i];
                if (baseline == null || baseline.supply != supply)
                    continue;
                return Mathf.Max(0, baseline.amount);
            }
        }

        return Mathf.Max(0, fallbackCurrent);
    }

    private static string ResolveSupplyDisplayName(SupplyData supply)
    {
        if (supply == null)
            return "Supply";
        if (!string.IsNullOrWhiteSpace(supply.displayName))
            return supply.displayName;
        if (!string.IsNullOrWhiteSpace(supply.id))
            return supply.id;
        return supply.name;
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
        if (data == null)
            return false;

        bool isMovementSensorState = cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado;
        if (!isMovementSensorState && scannerPromptStep != ScannerPromptStep.AwaitingAction)
            return false;

        data.Kind = HelperPanelKind.Sensors;
        TryAddSensorLine(data, 'A', "aim");
        TryAddSensorLine(data, 'E', "embark");
        TryAddSensorLine(data, 'D', "disembark");
        TryAddSensorLine(data, 'C', "capture");
        TryAddSensorLine(data, 'F', "fuse");
        TryAddSensorLine(data, 'S', "supply");
        TryAddSensorLine(data, 'T', "transfer");
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

    private bool TryBuildEmbarkHelperPanelData(HelperPanelData data)
    {
        if (data == null || cursorState != CursorState.Embarcando)
            return false;

        data.Kind = HelperPanelKind.Embark;
        if (cachedPodeEmbarcarTargets != null)
        {
            HashSet<UnitManager> addedTransporters = new HashSet<UnitManager>();
            for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
            {
                PodeEmbarcarOption option = cachedPodeEmbarcarTargets[i];
                UnitManager transporter = option != null ? option.transporterUnit : null;
                if (transporter == null || addedTransporters.Contains(transporter))
                    continue;

                addedTransporters.Add(transporter);
                int shownIndex = i + 1;
                bool isFocused = scannerSelectedEmbarkIndex == i;
                data.EmbarkCandidateLines.Add(new HelperEmbarkCandidateLine
                {
                    index = shownIndex,
                    unitName = ResolveUnitRuntimeName(transporter),
                    stats = BuildUnitStatInline(transporter),
                    isValid = true,
                    invalidReason = string.Empty,
                    isFocused = isFocused
                });
            }

        }

        return data.EmbarkCandidateLines.Count > 0;
    }

    private bool TryBuildSupplyHelperPanelData(HelperPanelData data)
    {
        if (data == null || cursorState != CursorState.Suprindo || selectedUnit == null || supplyExecutionInProgress)
            return false;
        if (scannerPromptStep != ScannerPromptStep.MergeParticipantSelect && scannerPromptStep != ScannerPromptStep.MergeConfirm)
            return false;

        List<UnitManager> executionOrder = new List<UnitManager>();
        if (supplyQueuedOrders != null)
        {
            for (int i = 0; i < supplyQueuedOrders.Count; i++)
            {
                UnitManager queuedTarget = supplyQueuedOrders[i] != null ? supplyQueuedOrders[i].targetUnit : null;
                if (queuedTarget == null || executionOrder.Contains(queuedTarget))
                    continue;
                executionOrder.Add(queuedTarget);
            }
        }

        UnitManager focusedTarget = null;
        if (scannerPromptStep == ScannerPromptStep.MergeConfirm && TryGetSelectedSupplyCandidate(out SupplyCandidateEntry selected) && selected != null)
        {
            focusedTarget = selected.targetUnit;
            if (focusedTarget != null && !executionOrder.Contains(focusedTarget))
                executionOrder.Add(focusedTarget);
        }

        if (executionOrder.Count <= 0)
            return false;

        List<SupplyEstimateLine> estimateLines = EstimateSupplyQueueForHelper(selectedUnit, executionOrder, focusedTarget);
        if (estimateLines.Count <= 0)
            return false;

        data.Kind = HelperPanelKind.Supply;
        data.SupplyIsConfirmStep = scannerPromptStep == ScannerPromptStep.MergeConfirm;
        data.SupplyHasQueuedOrders = supplyQueuedOrders != null && supplyQueuedOrders.Count > 0;

        int totalHp = 0;
        int totalFuel = 0;
        int totalAmmo = 0;
        int totalCost = 0;
        for (int i = 0; i < estimateLines.Count; i++)
        {
            SupplyEstimateLine line = estimateLines[i];
            if (line == null || line.target == null)
                continue;

            totalHp += Mathf.Max(0, line.hp);
            totalFuel += Mathf.Max(0, line.fuel);
            totalAmmo += Mathf.Max(0, line.ammo);
            totalCost += Mathf.Max(0, line.cost);

            data.SupplyTargetLines.Add(new HelperSupplyTargetLine
            {
                index = i + 1,
                unitName = ResolveUnitRuntimeName(line.target),
                gainsLabel = FormatSupplyGains(line.hp, line.fuel, line.ammo),
                estimatedCost = Mathf.Max(0, line.cost),
                isFocused = line.isFocused
            });
        }

        data.SupplyServedTargets = data.SupplyTargetLines.Count;
        data.SupplyRecoveredHp = Mathf.Max(0, totalHp);
        data.SupplyRecoveredFuel = Mathf.Max(0, totalFuel);
        data.SupplyRecoveredAmmo = Mathf.Max(0, totalAmmo);
        data.SupplyTotalCost = Mathf.Max(0, totalCost);
        BuildSupplyResourcePreviewLines(data, selectedUnit, executionOrder);
        return data.SupplyTargetLines.Count > 0;
    }

    private void BuildSupplyResourcePreviewLines(HelperPanelData data, UnitManager supplier, List<UnitManager> executionOrder)
    {
        if (data == null || supplier == null || executionOrder == null || executionOrder.Count <= 0)
            return;

        List<ServiceData> services = BuildDistinctServiceList(supplier.GetEmbarkedServices());
        if (services == null || services.Count <= 0)
            return;

        Dictionary<SupplyData, int> initialStock = BuildSupplierStockSnapshot(supplier);
        Dictionary<SupplyData, int> simulatedStock = CloneSupplySnapshot(initialStock);
        int remainingMoney = matchController != null
            ? Mathf.Max(0, matchController.GetActualMoney(supplier.TeamId))
            : int.MaxValue;

        for (int i = 0; i < executionOrder.Count; i++)
        {
            UnitManager target = executionOrder[i];
            if (target == null)
                continue;

            int simulatedHp = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
            int simulatedFuel = Mathf.Clamp(target.CurrentFuel, 0, target.GetMaxFuel());
            List<int> simulatedAmmoByWeapon = BuildRuntimeAmmoSnapshot(target);

            for (int s = 0; s < services.Count; s++)
            {
                ServiceData service = services[s];
                if (service == null || !service.isService)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!CanServiceApplyByClassAndNeed(target, service))
                    continue;

                Dictionary<SupplyData, int> candidateStock = CloneSupplySnapshot(simulatedStock);
                List<int> candidateSimulatedAmmo = CloneAmmoSnapshot(simulatedAmmoByWeapon);
                List<int> ammoByWeaponGain = new List<int>();
                EstimatePotentialServiceGains(
                    target,
                    service,
                    candidateStock,
                    out int hpGain,
                    out int fuelGain,
                    out int ammoGain,
                    ammoByWeaponGain,
                    simulatedHp,
                    simulatedFuel,
                    candidateSimulatedAmmo);
                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                    continue;

                int finalCost = matchController != null
                    ? matchController.ResolveEconomyCost(ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain))
                    : Mathf.Max(0, ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain));
                if (finalCost > remainingMoney)
                    continue;

                OverwriteSupplySnapshot(simulatedStock, candidateStock);
                remainingMoney = Mathf.Max(0, remainingMoney - Mathf.Max(0, finalCost));
                simulatedHp = Mathf.Clamp(simulatedHp + hpGain, 0, target.GetMaxHP());
                simulatedFuel = Mathf.Clamp(simulatedFuel + fuelGain, 0, target.GetMaxFuel());
                simulatedAmmoByWeapon = candidateSimulatedAmmo;
            }
        }

        foreach (KeyValuePair<SupplyData, int> pair in initialStock)
        {
            SupplyData supply = pair.Key;
            if (supply == null)
                continue;

            int before = Mathf.Max(0, pair.Value);
            int after = simulatedStock.TryGetValue(supply, out int simulated) ? Mathf.Max(0, simulated) : 0;
            if (before == after)
                continue;

            data.SupplyResourceLines.Add(new HelperSupplyResourceLine
            {
                supplyName = ResolveSupplyDisplayName(supply),
                beforeAmount = before,
                afterAmount = after
            });
        }
    }

    private List<SupplyEstimateLine> EstimateSupplyQueueForHelper(UnitManager supplier, List<UnitManager> executionOrder, UnitManager focusedTarget)
    {
        List<SupplyEstimateLine> lines = new List<SupplyEstimateLine>();
        if (supplier == null || executionOrder == null || executionOrder.Count <= 0)
            return lines;

        List<ServiceData> services = BuildDistinctServiceList(supplier.GetEmbarkedServices());
        if (services == null || services.Count <= 0)
            return lines;

        Dictionary<SupplyData, int> sourceStock = BuildSupplierStockSnapshot(supplier);
        int remainingMoney = matchController != null
            ? Mathf.Max(0, matchController.GetActualMoney(supplier.TeamId))
            : int.MaxValue;

        for (int i = 0; i < executionOrder.Count; i++)
        {
            UnitManager target = executionOrder[i];
            if (target == null)
                continue;

            int hpTotal = 0;
            int fuelTotal = 0;
            int ammoTotal = 0;
            int costTotal = 0;
            int simulatedHp = Mathf.Clamp(target.CurrentHP, 0, target.GetMaxHP());
            int simulatedFuel = Mathf.Clamp(target.CurrentFuel, 0, target.GetMaxFuel());
            List<int> simulatedAmmoByWeapon = BuildRuntimeAmmoSnapshot(target);

            for (int s = 0; s < services.Count; s++)
            {
                ServiceData service = services[s];
                if (service == null || !service.isService)
                    continue;
                if (service.apenasEntreSupridores && !IsSupplier(target))
                    continue;
                if (!CanServiceApplyByClassAndNeed(target, service))
                    continue;

                Dictionary<SupplyData, int> candidateStock = CloneSupplySnapshot(sourceStock);
                List<int> candidateSimulatedAmmo = CloneAmmoSnapshot(simulatedAmmoByWeapon);
                List<int> ammoByWeaponGain = new List<int>();
                EstimatePotentialServiceGains(
                    target,
                    service,
                    candidateStock,
                    out int hpGain,
                    out int fuelGain,
                    out int ammoGain,
                    ammoByWeaponGain,
                    simulatedHp,
                    simulatedFuel,
                    candidateSimulatedAmmo);
                if (hpGain <= 0 && fuelGain <= 0 && ammoGain <= 0)
                    continue;

                int finalCost = matchController != null
                    ? matchController.ResolveEconomyCost(ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain))
                    : Mathf.Max(0, ComputeServiceMoneyCost(target, service, hpGain, fuelGain, ammoGain, ammoByWeaponGain));
                if (finalCost > remainingMoney)
                    continue;

                OverwriteSupplySnapshot(sourceStock, candidateStock);
                remainingMoney = Mathf.Max(0, remainingMoney - Mathf.Max(0, finalCost));
                hpTotal += hpGain;
                fuelTotal += fuelGain;
                ammoTotal += ammoGain;
                costTotal += Mathf.Max(0, finalCost);
                simulatedHp = Mathf.Clamp(simulatedHp + hpGain, 0, target.GetMaxHP());
                simulatedFuel = Mathf.Clamp(simulatedFuel + fuelGain, 0, target.GetMaxFuel());
                simulatedAmmoByWeapon = candidateSimulatedAmmo;
            }

            if (hpTotal <= 0 && fuelTotal <= 0 && ammoTotal <= 0 && costTotal <= 0)
                continue;

            lines.Add(new SupplyEstimateLine
            {
                target = target,
                hp = hpTotal,
                fuel = fuelTotal,
                ammo = ammoTotal,
                cost = costTotal,
                isFocused = target == focusedTarget
            });
        }

        return lines;
    }

    private static string FormatSupplyGains(int hp, int fuel, int ammo)
    {
        List<string> segments = new List<string>();
        if (hp > 0)
            segments.Add($"HP +{hp}");
        if (fuel > 0)
            segments.Add($"FUEL +{fuel}");
        if (ammo > 0)
            segments.Add($"AMMO +{ammo}");
        return segments.Count > 0 ? string.Join(" | ", segments) : "-";
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
