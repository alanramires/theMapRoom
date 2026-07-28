using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public readonly struct ConfirmedUnitOccupancyRecord :
    IEquatable<ConfirmedUnitOccupancyRecord>
{
    public readonly UnitManager unit;
    public readonly Vector3Int cell;
    public readonly Domain domain;
    public readonly HeightLevel height;
    public readonly HeightBand band;
    public readonly int slotIndex;
    public readonly bool isEmbarked;
    public readonly UnitManager embarkedTransporter;
    public readonly bool isTransporter;
    public readonly bool isSupplier;
    public readonly SupplierTier supplierTier;

    public ConfirmedUnitOccupancyRecord(
        UnitManager unit,
        Vector3Int cell,
        Domain domain,
        HeightLevel height,
        int slotIndex,
        bool isEmbarked,
        UnitManager embarkedTransporter,
        bool isTransporter,
        bool isSupplier,
        SupplierTier supplierTier)
    {
        cell.z = 0;
        this.unit = unit;
        this.cell = cell;
        this.domain = domain;
        this.height = height;
        band = OccupancyResolver.GetHeightBand(domain, height);
        this.slotIndex = slotIndex;
        this.isEmbarked = isEmbarked;
        this.embarkedTransporter = embarkedTransporter;
        this.isTransporter = isTransporter;
        this.isSupplier = isSupplier;
        this.supplierTier = supplierTier;
    }

    public bool OccupiesBoard => !isEmbarked;

    public bool Equals(ConfirmedUnitOccupancyRecord other)
    {
        return unit == other.unit
            && cell == other.cell
            && domain == other.domain
            && height == other.height
            && slotIndex == other.slotIndex
            && isEmbarked == other.isEmbarked
            && embarkedTransporter == other.embarkedTransporter
            && isTransporter == other.isTransporter
            && isSupplier == other.isSupplier
            && supplierTier == other.supplierTier;
    }

    public override bool Equals(object obj)
    {
        return obj is ConfirmedUnitOccupancyRecord other
            && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = unit != null ? unit.GetHashCode() : 0;
            hash = (hash * 397) ^ cell.GetHashCode();
            hash = (hash * 397) ^ (int)domain;
            hash = (hash * 397) ^ (int)height;
            hash = (hash * 397) ^ slotIndex;
            hash = (hash * 397) ^ (isEmbarked ? 1 : 0);
            hash = (hash * 397)
                ^ (embarkedTransporter != null
                    ? embarkedTransporter.GetHashCode()
                    : 0);
            hash = (hash * 397) ^ (isTransporter ? 1 : 0);
            hash = (hash * 397) ^ (isSupplier ? 1 : 0);
            hash = (hash * 397) ^ (int)supplierTier;
            return hash;
        }
    }
}

public readonly struct ConfirmedOccupancyLayerKey :
    IEquatable<ConfirmedOccupancyLayerKey>
{
    public readonly Vector3Int cell;
    public readonly HeightBand band;

    public ConfirmedOccupancyLayerKey(
        Vector3Int cell,
        HeightBand band)
    {
        cell.z = 0;
        this.cell = cell;
        this.band = band;
    }

    public bool Equals(ConfirmedOccupancyLayerKey other)
    {
        return cell == other.cell && band == other.band;
    }

    public override bool Equals(object obj)
    {
        return obj is ConfirmedOccupancyLayerKey other
            && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (cell.GetHashCode() * 397) ^ (int)band;
        }
    }
}

/// <summary>
/// Índice derivado do último snapshot confirmado. Notificações de unidade
/// apenas marcam dados sujos; a publicação e a revisão só mudam depois do
/// retorno a Neutral, ou após a conclusão explícita de um load.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-8400)]
public sealed class ConfirmedOccupancyIndex : MonoBehaviour
{
    private static readonly List<ConfirmedOccupancyIndex> ActiveIndices =
        new List<ConfirmedOccupancyIndex>();
    private static readonly IReadOnlyList<UnitManager> EmptyUnits =
        Array.Empty<UnitManager>();

    [SerializeField] private Tilemap boardTilemap;
    [NonSerialized] private int confirmedRevision;

    private readonly Dictionary<UnitManager, ConfirmedUnitOccupancyRecord>
        records =
            new Dictionary<UnitManager, ConfirmedUnitOccupancyRecord>();
    private readonly Dictionary<Vector3Int, List<UnitManager>> unitsByCell =
        new Dictionary<Vector3Int, List<UnitManager>>();
    private readonly Dictionary<ConfirmedOccupancyLayerKey, List<UnitManager>>
        unitsByCellAndBand =
            new Dictionary<ConfirmedOccupancyLayerKey, List<UnitManager>>();
    private readonly Dictionary<UnitManager, List<UnitManager>>
        embarkedPassengersByTransporter =
            new Dictionary<UnitManager, List<UnitManager>>();
    private readonly List<UnitManager> trackedUnits =
        new List<UnitManager>();
    private readonly List<UnitManager> boardUnits =
        new List<UnitManager>();
    private readonly List<UnitManager> transporters =
        new List<UnitManager>();
    private readonly List<UnitManager> suppliers =
        new List<UnitManager>();
    private readonly List<UnitManager> hubs =
        new List<UnitManager>();
    private readonly List<UnitManager> receivers =
        new List<UnitManager>();
    private readonly HashSet<UnitManager> dirtyUnits =
        new HashSet<UnitManager>();
    private readonly List<UnitManager> dirtyScratch =
        new List<UnitManager>();

    private TurnStateManager turnStateManager;
    private bool initialized;
    private bool fullRebuildPending;
    private bool subscribed;

    public Tilemap BoardTilemap => boardTilemap;
    public int ConfirmedRevision => confirmedRevision;
    public bool IsReady => initialized && boardTilemap != null;
    public bool HasPendingChanges =>
        fullRebuildPending || dirtyUnits.Count > 0;
    public bool CanServeLiveQueries => IsReady && !HasPendingChanges;
    public IReadOnlyList<UnitManager> TrackedUnits => trackedUnits;
    public IReadOnlyList<UnitManager> BoardUnits => boardUnits;
    public IReadOnlyList<UnitManager> Transporters => transporters;
    public IReadOnlyList<UnitManager> Suppliers => suppliers;
    public IReadOnlyList<UnitManager> Hubs => hubs;
    public IReadOnlyList<UnitManager> Receivers => receivers;

    private void Awake()
    {
        Register();
        ResolveSources();
    }

    private void OnEnable()
    {
        Register();
        Subscribe();
        ResolveSources();
        if (Application.isPlaying && !initialized)
            RebuildFromScene("enable");
    }

    private void OnDisable()
    {
        Unsubscribe();
        ActiveIndices.Remove(this);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying
            || !HasPendingChanges
            || !IsAtConfirmedBoundary())
        {
            return;
        }
        ReconcilePending("neutral late update");
    }

    public void Configure(Tilemap tilemap)
    {
        boardTilemap = tilemap;
        ResolveTurnStateManager();
        initialized = false;
        fullRebuildPending = true;
    }

    public bool TryGetRecord(
        UnitManager unit,
        out ConfirmedUnitOccupancyRecord record)
    {
        record = default;
        return unit != null && records.TryGetValue(unit, out record);
    }

    public IReadOnlyList<UnitManager> GetUnitsAtCell(Vector3Int cell)
    {
        cell.z = 0;
        return unitsByCell.TryGetValue(
                cell,
                out List<UnitManager> found)
            ? found
            : EmptyUnits;
    }

    public IReadOnlyList<UnitManager> GetUnitsAtCell(
        Vector3Int cell,
        HeightBand band)
    {
        return unitsByCellAndBand.TryGetValue(
                new ConfirmedOccupancyLayerKey(cell, band),
                out List<UnitManager> found)
            ? found
            : EmptyUnits;
    }

    public IReadOnlyList<UnitManager> GetEmbarkedPassengers(
        UnitManager transporter)
    {
        return transporter != null
            && embarkedPassengersByTransporter.TryGetValue(
                transporter,
                out List<UnitManager> found)
            ? found
            : EmptyUnits;
    }

    public void CopyUnitsAtCell(
        Vector3Int cell,
        UnitManager exceptUnit,
        List<UnitManager> output)
    {
        if (output == null)
            return;
        output.Clear();
        IReadOnlyList<UnitManager> found = GetUnitsAtCell(cell);
        for (int i = 0; i < found.Count; i++)
        {
            UnitManager unit = found[i];
            if (unit != null && unit != exceptUnit)
                output.Add(unit);
        }
    }

    public bool RebuildFromScene(string reason)
    {
        ResolveSources();
        var next =
            new Dictionary<UnitManager, ConfirmedUnitOccupancyRecord>();
        IReadOnlyList<UnitManager> active = UnitManager.AllActive;
        for (int i = 0; i < active.Count; i++)
        {
            UnitManager unit = active[i];
            if (TryCapture(unit, out ConfirmedUnitOccupancyRecord record))
                next[unit] = record;
        }

        bool changed = !initialized || !RecordsEqual(next);
        if (changed)
        {
            records.Clear();
            foreach (KeyValuePair<UnitManager, ConfirmedUnitOccupancyRecord>
                     pair in next)
            {
                records.Add(pair.Key, pair.Value);
            }
            RebuildLookups();
            confirmedRevision++;
        }

        initialized = boardTilemap != null;
        fullRebuildPending = false;
        dirtyUnits.Clear();
        return changed;
    }

    public void RequestFullRebuild()
    {
        fullRebuildPending = true;
    }

    public bool ReconcilePending(string reason)
    {
        if (!Application.isPlaying || !IsAtConfirmedBoundary())
            return false;
        if (fullRebuildPending || !initialized)
            return RebuildFromScene(reason);
        if (dirtyUnits.Count == 0)
            return false;

        dirtyScratch.Clear();
        foreach (UnitManager unit in dirtyUnits)
            dirtyScratch.Add(unit);
        dirtyUnits.Clear();

        bool changed = false;
        for (int i = 0; i < dirtyScratch.Count; i++)
        {
            UnitManager unit = dirtyScratch[i];
            bool hadRecord = records.TryGetValue(
                unit,
                out ConfirmedUnitOccupancyRecord previous);
            bool hasRecord = TryCapture(
                unit,
                out ConfirmedUnitOccupancyRecord current);

            if (hadRecord && hasRecord && previous.Equals(current))
                continue;
            if (hadRecord)
            {
                RemoveRecordFromLookups(previous);
                records.Remove(unit);
            }
            if (hasRecord)
            {
                records[unit] = current;
                AddRecordToLookups(current);
            }
            changed = true;
        }
        dirtyScratch.Clear();

        if (changed)
            confirmedRevision++;
        return changed;
    }

    public static bool TryGetFor(
        Tilemap tilemap,
        out ConfirmedOccupancyIndex index)
    {
        CleanupRegistry();
        for (int i = 0; i < ActiveIndices.Count; i++)
        {
            ConfirmedOccupancyIndex candidate = ActiveIndices[i];
            if (candidate == null)
                continue;
            if (candidate.boardTilemap == tilemap)
            {
                index = candidate;
                return candidate.IsReady;
            }
        }
        index = null;
        return false;
    }

    internal static ConfirmedOccupancyIndex EnsureForScene(Scene scene)
    {
        CleanupRegistry();
        for (int i = 0; i < ActiveIndices.Count; i++)
        {
            ConfirmedOccupancyIndex existing = ActiveIndices[i];
            if (existing != null && existing.gameObject.scene == scene)
            {
                existing.ResolveSources();
                if (Application.isPlaying)
                    existing.RebuildFromScene("scene loaded");
                return existing;
            }
        }

        BoardTopologyIndex topology =
            BoardTopologyIndex.EnsureForScene(scene);
        Tilemap tilemap = topology != null
            ? topology.BoardTilemap
            : null;
        if (tilemap == null)
            return null;

        GameObject host = new GameObject(
            "[ConfirmedOccupancyIndex Runtime]");
        host.SetActive(false);
        host.hideFlags = HideFlags.DontSave;
        SceneManager.MoveGameObjectToScene(host, scene);
        ConfirmedOccupancyIndex created =
            host.AddComponent<ConfirmedOccupancyIndex>();
        created.Configure(tilemap);
        host.SetActive(true);
        return created;
    }

    private bool TryCapture(
        UnitManager unit,
        out ConfirmedUnitOccupancyRecord record)
    {
        record = default;
        if (unit == null
            || !unit.gameObject.activeInHierarchy
            || unit.IsDead
            || boardTilemap == null
            || unit.BoardTilemap != boardTilemap
            || unit.gameObject.scene != boardTilemap.gameObject.scene)
        {
            return false;
        }

        bool hasData = unit.TryGetUnitData(out UnitData data)
            && data != null;
        record = new ConfirmedUnitOccupancyRecord(
            unit,
            unit.CurrentCellPosition,
            unit.GetDomain(),
            unit.GetHeightLevel(),
            unit.SlotIndex,
            unit.IsEmbarked,
            unit.EmbarkedTransporter,
            hasData && data.isTransporter,
            hasData && data.isSupplier,
            hasData ? data.supplierTier : SupplierTier.Hub);
        return true;
    }

    private bool RecordsEqual(
        Dictionary<UnitManager, ConfirmedUnitOccupancyRecord> next)
    {
        if (next.Count != records.Count)
            return false;
        foreach (KeyValuePair<UnitManager, ConfirmedUnitOccupancyRecord>
                 pair in next)
        {
            if (!records.TryGetValue(
                    pair.Key,
                    out ConfirmedUnitOccupancyRecord current)
                || !current.Equals(pair.Value))
            {
                return false;
            }
        }
        return true;
    }

    private void RebuildLookups()
    {
        unitsByCell.Clear();
        unitsByCellAndBand.Clear();
        embarkedPassengersByTransporter.Clear();
        trackedUnits.Clear();
        boardUnits.Clear();
        transporters.Clear();
        suppliers.Clear();
        hubs.Clear();
        receivers.Clear();

        foreach (ConfirmedUnitOccupancyRecord record in records.Values)
            AddRecordToLookups(record);
    }

    private void AddRecordToLookups(
        ConfirmedUnitOccupancyRecord record)
    {
        UnitManager unit = record.unit;
        if (unit == null)
            return;
        AddUnitSorted(trackedUnits, unit);
        if (record.isTransporter)
            AddUnitSorted(transporters, unit);
        if (record.isSupplier)
        {
            AddUnitSorted(suppliers, unit);
            if (record.supplierTier == SupplierTier.Hub)
                AddUnitSorted(hubs, unit);
            else if (record.supplierTier == SupplierTier.Receiver)
                AddUnitSorted(receivers, unit);
        }

        if (record.isEmbarked)
        {
            if (record.embarkedTransporter != null)
            {
                AddToLookup(
                    embarkedPassengersByTransporter,
                    record.embarkedTransporter,
                    unit);
            }
            return;
        }

        AddUnitSorted(boardUnits, unit);
        AddToLookup(unitsByCell, record.cell, unit);
        AddToLookup(
            unitsByCellAndBand,
            new ConfirmedOccupancyLayerKey(
                record.cell,
                record.band),
            unit);
    }

    private void RemoveRecordFromLookups(
        ConfirmedUnitOccupancyRecord record)
    {
        UnitManager unit = record.unit;
        trackedUnits.Remove(unit);
        if (record.isTransporter)
            transporters.Remove(unit);
        if (record.isSupplier)
        {
            suppliers.Remove(unit);
            if (record.supplierTier == SupplierTier.Hub)
                hubs.Remove(unit);
            else if (record.supplierTier == SupplierTier.Receiver)
                receivers.Remove(unit);
        }

        if (record.isEmbarked)
        {
            if (record.embarkedTransporter != null)
            {
                RemoveFromLookup(
                    embarkedPassengersByTransporter,
                    record.embarkedTransporter,
                    unit);
            }
            return;
        }

        boardUnits.Remove(unit);
        RemoveFromLookup(unitsByCell, record.cell, unit);
        RemoveFromLookup(
            unitsByCellAndBand,
            new ConfirmedOccupancyLayerKey(
                record.cell,
                record.band),
            unit);
    }

    private static void AddToLookup<TKey>(
        Dictionary<TKey, List<UnitManager>> lookup,
        TKey key,
        UnitManager unit)
    {
        if (!lookup.TryGetValue(key, out List<UnitManager> units))
        {
            units = new List<UnitManager>();
            lookup.Add(key, units);
        }
        AddUnitSorted(units, unit);
    }

    private static void RemoveFromLookup<TKey>(
        Dictionary<TKey, List<UnitManager>> lookup,
        TKey key,
        UnitManager unit)
    {
        if (!lookup.TryGetValue(key, out List<UnitManager> units))
            return;
        units.Remove(unit);
        if (units.Count == 0)
            lookup.Remove(key);
    }

    private static void AddUnitSorted(
        List<UnitManager> units,
        UnitManager unit)
    {
        int index = units.BinarySearch(unit, UnitComparer.Instance);
        if (index < 0)
            index = ~index;
        units.Insert(index, unit);
    }

    private static int CompareUnits(UnitManager left, UnitManager right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        int instance = left.InstanceId.CompareTo(right.InstanceId);
        if (instance != 0)
            return instance;
        return left.GetEntityId().GetHashCode().CompareTo(
            right.GetEntityId().GetHashCode());
    }

    private sealed class UnitComparer : IComparer<UnitManager>
    {
        public static readonly UnitComparer Instance = new UnitComparer();

        public int Compare(UnitManager left, UnitManager right)
        {
            return CompareUnits(left, right);
        }
    }

    private void HandleOccupancyChanged(
        UnitManager unit,
        Vector3Int previousCell,
        Vector3Int currentCell)
    {
        if (ReferenceEquals(unit, null))
        {
            fullRebuildPending = true;
            return;
        }
        if (records.ContainsKey(unit)
            || (unit != null
                && boardTilemap != null
                && unit.BoardTilemap == boardTilemap
                && unit.gameObject.scene
                    == boardTilemap.gameObject.scene))
        {
            dirtyUnits.Add(unit);
        }
    }

    private void HandleCursorReturnedToNeutral()
    {
        if (IsAtConfirmedBoundary())
            ReconcilePending("cursor returned to Neutral");
    }

    private void HandleAfterLoadSuccess()
    {
        RebuildFromScene("load completed");
    }

    private void HandleSlotConfigChanged()
    {
        fullRebuildPending = true;
    }

    private bool IsAtConfirmedBoundary()
    {
        ResolveTurnStateManager();
        return turnStateManager == null
            || turnStateManager.CurrentCursorState
                == TurnStateManager.CursorState.Neutral;
    }

    private void ResolveSources()
    {
        if (boardTilemap == null)
        {
            BoardTopologyIndex topology =
                BoardTopologyIndex.EnsureForScene(gameObject.scene);
            if (topology != null)
                boardTilemap = topology.BoardTilemap;
        }
        ResolveTurnStateManager();
    }

    private void ResolveTurnStateManager()
    {
        if (turnStateManager != null
            && turnStateManager.gameObject.scene == gameObject.scene)
        {
            return;
        }

        turnStateManager = null;
        TurnStateManager[] managers =
            UnityEngine.Object.FindObjectsByType<TurnStateManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < managers.Length; i++)
        {
            TurnStateManager manager = managers[i];
            if (manager != null
                && manager.gameObject.scene == gameObject.scene)
            {
                turnStateManager = manager;
                return;
            }
        }
    }

    private void Subscribe()
    {
        if (subscribed)
            return;
        UnitOccupancyRules.OnUnitOccupancyChanged +=
            HandleOccupancyChanged;
        CursorController.OnCursorReturnedToNeutral +=
            HandleCursorReturnedToNeutral;
        SaveGameManager.OnAfterLoadSuccess +=
            HandleAfterLoadSuccess;
        MatchController.OnSlotConfigChanged +=
            HandleSlotConfigChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;
        UnitOccupancyRules.OnUnitOccupancyChanged -=
            HandleOccupancyChanged;
        CursorController.OnCursorReturnedToNeutral -=
            HandleCursorReturnedToNeutral;
        SaveGameManager.OnAfterLoadSuccess -=
            HandleAfterLoadSuccess;
        MatchController.OnSlotConfigChanged -=
            HandleSlotConfigChanged;
        subscribed = false;
    }

    private void Register()
    {
        CleanupRegistry();
        if (!ActiveIndices.Contains(this))
            ActiveIndices.Add(this);
    }

    private static void CleanupRegistry()
    {
        for (int i = ActiveIndices.Count - 1; i >= 0; i--)
        {
            if (ActiveIndices[i] == null)
                ActiveIndices.RemoveAt(i);
        }
    }
}

internal static class ConfirmedOccupancyRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        if (Application.isPlaying)
            ConfirmedOccupancyIndex.EnsureForScene(scene);
    }
}
