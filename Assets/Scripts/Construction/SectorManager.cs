using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif


[ExecuteAlways]
[DefaultExecutionOrder(-320)]
public sealed class SectorManager : MonoBehaviour
{
    [System.Serializable]
    public sealed class SectorConstructionInfo
    {
        [SerializeField] private int instanceId;
        [SerializeField] private string displayName;
        [SerializeField] private Vector3Int cell;
        [SerializeField] private TeamId ownerTeam;
        [SerializeField] private int ownerSlotIndex = -1;
        [SerializeField] private int currentCapturePoints;
        [SerializeField] private int capturePointsMax;
        [SerializeField] private ConstructionManager source;

        public int InstanceId => instanceId;
        public string DisplayName => displayName;
        public Vector3Int Cell => cell;
        public TeamId OwnerTeam => ownerTeam;
        public int OwnerSlotIndex => ownerSlotIndex;
        public int CurrentCapturePoints => currentCapturePoints;
        public int CapturePointsMax => capturePointsMax;
        public ConstructionManager Source => source;

        public SectorConstructionInfo(ConstructionManager construction)
        {
            source = construction;
            instanceId = construction != null ? construction.InstanceId : 0;
            displayName = construction != null ? construction.ConstructionDisplayName : string.Empty;
            cell = construction != null ? construction.CurrentCellPosition : Vector3Int.zero;
            ownerTeam = construction != null ? construction.TeamId : TeamId.Neutral;
            ownerSlotIndex = construction != null ? construction.SlotIndex : -1;
            currentCapturePoints = construction != null ? construction.CurrentCapturePoints : 0;
            capturePointsMax = construction != null ? construction.CapturePointsMax : 0;
        }
    }

    public sealed class SectorNeighborDistanceDebugEntry
    {
        public ConstructionSector Sector;
        public bool UsedTerrainCost;
        public bool Reachable;
        public float Distance;
        public List<Vector3Int> Path = new List<Vector3Int>();
    }

    [System.Serializable]
    public struct SectorDistanceEntry
    {
        [SerializeField] public string ConstructionName;
        [SerializeField] public int    InstanceId;
        [SerializeField] public float  Distance;        // foot reference
        [SerializeField] public float  VehicleDistance; // vehicle reference (APC, includes road bonus)
        [SerializeField] public float  AirDistance;     // air reference (hex distance — air ignores terrain)
        [SerializeField] public bool   IsHQ;
    }

    [System.Serializable]
    public class SectorTeamDistances
    {
        [SerializeField] public int                       SlotIndex = -1;
        [SerializeField] public TeamId                    Team;
        [SerializeField] public List<SectorDistanceEntry> Entries = new List<SectorDistanceEntry>();

        public float GetHQDistance()
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHQ) return Entries[i].Distance;
            return float.MaxValue;
        }

        public float GetHQVehicleDistance()
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHQ) return Entries[i].VehicleDistance;
            return float.MaxValue;
        }

        public float GetHQAirDistance()
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHQ) return Entries[i].AirDistance;
            return float.MaxValue;
        }

        public float GetNearestFactoryDistance()
        {
            float best = float.MaxValue;
            for (int i = 0; i < Entries.Count; i++)
                if (!Entries[i].IsHQ && Entries[i].Distance < best) best = Entries[i].Distance;
            return best;
        }

        public bool TryGetNearestFactory(out SectorDistanceEntry factory)
        {
            factory = default;
            bool  found = false;
            float best  = float.MaxValue;
            for (int i = 0; i < Entries.Count; i++)
            {
                SectorDistanceEntry e = Entries[i];
                if (e.IsHQ || e.Distance >= best) continue;
                best = e.Distance; factory = e; found = true;
            }
            return found;
        }
    }

    [System.Serializable]
    public struct SectorRiskEntry
    {
        [SerializeField] public int             SlotIndex;
        [SerializeField] public TeamId          Team;
        [SerializeField] public float           RiskRatio;
        [SerializeField] public SectorRiskLevel RiskLevel;
    }

    public enum SectorRiskLevel
    {
        Safe,     // ratio < 0.25  — território próprio, fácil de defender
        Low,      // ratio < 0.40  — levemente avançado
        Medium,   // ratio < 0.60  — zona de conflito equidistante
        High,     // ratio < 0.75  — território adversário
        DeepRaid  // ratio >= 0.75 — deep raid, precisa de suporte pesado
    }

    [System.Serializable]
    public sealed class SectorInfo
    {
        [SerializeField] private ConstructionSector sector;
        [SerializeField] private ConstructionManager representativeConstruction;
        [SerializeField] private Vector3Int representativeCell;
        [SerializeField] private string representativeLabel;
        [SerializeField] private int totalCapturePointsMax;
        [SerializeField] private int totalCurrentCapturePoints;
        [SerializeField] private int constructionCount;
        [SerializeField] private bool isFullyControlled;
        [SerializeField] private bool isDisputed;
        [SerializeField] private bool hasPartialCapture;
        [SerializeField] private TeamId controllingTeam = TeamId.Neutral;
        [SerializeField] private int controllingSlotIndex = -1;
        [SerializeField] private string statusText;
        [SerializeField] private List<SectorConstructionInfo> constructions    = new List<SectorConstructionInfo>();
        [SerializeField] private List<SectorRiskEntry>        riskEntries      = new List<SectorRiskEntry>();
        [SerializeField] private List<SectorTeamDistances>    sectorDistances  = new List<SectorTeamDistances>();
        [HideInInspector][SerializeField] private ConstructionSector closestNeighbor1;
        [HideInInspector][SerializeField] private float              closestNeighbor1Distance = float.MaxValue;
        [HideInInspector][SerializeField] private ConstructionSector closestNeighbor2;
        [HideInInspector][SerializeField] private float              closestNeighbor2Distance = float.MaxValue;

        public ConstructionSector Sector => sector;
        public ConstructionManager RepresentativeConstruction => representativeConstruction;
        public Vector3Int RepresentativeCell => representativeCell;
        public string RepresentativeLabel => representativeLabel;
        public int TotalCapturePointsMax => totalCapturePointsMax;
        public int TotalCurrentCapturePoints => totalCurrentCapturePoints;
        public int ConstructionCount => constructionCount;
        public bool IsFullyControlled => isFullyControlled;
        public bool IsDisputed => isDisputed;
        public bool HasPartialCapture => hasPartialCapture;
        public TeamId ControllingTeam => controllingTeam;
        public int ControllingSlotIndex => controllingSlotIndex;
        public string StatusText => statusText;
        public IReadOnlyList<SectorConstructionInfo> Constructions    => constructions;
        public IReadOnlyList<SectorRiskEntry>        RiskEntries      => riskEntries;
        public IReadOnlyList<SectorTeamDistances>    SectorDistances  => sectorDistances;
        public ConstructionSector ClosestNeighbor1         => closestNeighbor1;
        public float              ClosestNeighbor1Distance => closestNeighbor1Distance;
        public ConstructionSector ClosestNeighbor2         => closestNeighbor2;
        public float              ClosestNeighbor2Distance => closestNeighbor2Distance;

        public float GetDistanceToHQ(PlayerSlotId slotId)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].GetHQDistance();
            return float.MaxValue;
        }

        public float GetVehicleDistanceToHQ(PlayerSlotId slotId)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].GetHQVehicleDistance();
            return float.MaxValue;
        }

        public float GetAirDistanceToHQ(PlayerSlotId slotId)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].GetHQAirDistance();
            return float.MaxValue;
        }

        public enum TransportPreference { Vehicle, Air, Either }

        // Compara distância de veículo vs ar para o HQ do time.
        // Air:     vehicle > air + tieZoneHex (montanhas — helicóptero claramente melhor)
        // Vehicle: vehicle <= air             (estradas — APC claramente melhor)
        // Either:  empate técnico             (usa demanda para decidir)
        public TransportPreference GetTransportPreference(PlayerSlotId slotId, int tieZoneHex = 2)
        {
            float vehicle = GetVehicleDistanceToHQ(slotId);
            float air     = GetAirDistanceToHQ(slotId);
            if (vehicle >= float.MaxValue * 0.5f) return TransportPreference.Air;
            if (air     >= float.MaxValue * 0.5f) return TransportPreference.Vehicle;
            float diff = vehicle - air;
            if (diff > tieZoneHex) return TransportPreference.Air;
            if (diff <= 0)         return TransportPreference.Vehicle;
            return TransportPreference.Either;
        }

        public float GetNearestFactoryDistance(PlayerSlotId slotId)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].GetNearestFactoryDistance();
            return float.MaxValue;
        }

        public bool TryGetNearestFactory(PlayerSlotId slotId, out SectorDistanceEntry factory)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].TryGetNearestFactory(out factory);
            factory = default;
            return false;
        }

        public PlayerSlotId NearestSlot()
        {
            PlayerSlotId best = PlayerSlotId.Invalid;
            float  min  = float.MaxValue;
            for (int i = 0; i < sectorDistances.Count; i++)
            {
                float d = sectorDistances[i].GetHQDistance();
                if (d < min) { min = d; best = PlayerSlotId.FromIndex(sectorDistances[i].SlotIndex); }
            }
            return best;
        }

        public float GetRiskRatioFor(PlayerSlotId slotId)
        {
            float myDist       = GetDistanceToHQ(slotId);
            float enemyMinDist = float.MaxValue;
            for (int i = 0; i < sectorDistances.Count; i++)
            {
                if (sectorDistances[i].SlotIndex == slotId.Value) continue;
                float d = sectorDistances[i].GetHQDistance();
                if (d < enemyMinDist) enemyMinDist = d;
            }
            if (myDist == float.MaxValue)      return 0.5f;
            if (enemyMinDist == float.MaxValue) return 0f;
            float total = myDist + enemyMinDist;
            return total < 0.01f ? 0.5f : myDist / total;
        }

        public SectorRiskLevel GetRiskLevelFor(PlayerSlotId slotId)
        {
            float r = GetRiskRatioFor(slotId);
            if (r < 0.25f) return SectorRiskLevel.Safe;
            if (r < 0.40f) return SectorRiskLevel.Low;
            if (r < 0.60f) return SectorRiskLevel.Medium;
            if (r < 0.75f) return SectorRiskLevel.High;
            return SectorRiskLevel.DeepRaid;
        }

        internal void ApplyNeighbors(ConstructionSector s1, float d1, ConstructionSector s2, float d2)
        {
            closestNeighbor1         = s1;
            closestNeighbor1Distance = d1;
            closestNeighbor2         = s2;
            closestNeighbor2Distance = d2;
        }

        internal void ApplySectorDistances(List<SectorTeamDistances> distances)
        {
            sectorDistances.Clear();
            riskEntries.Clear();
            if (distances == null) return;

            sectorDistances.AddRange(distances);

            for (int i = 0; i < distances.Count; i++)
            {
                PlayerSlotId slotId = PlayerSlotId.FromIndex(distances[i].SlotIndex);
                float ratio = GetRiskRatioFor(slotId);
                riskEntries.Add(new SectorRiskEntry
                {
                    SlotIndex = slotId.Value,
                    Team      = distances[i].Team,
                    RiskRatio = Mathf.Round(ratio * 100f) / 100f,
                    RiskLevel = GetRiskLevelFor(slotId),
                });
            }
        }

        internal void Apply(
            ConstructionSector valueSector,
            ConstructionManager valueRepresentativeConstruction,
            Vector3Int valueRepresentativeCell,
            string valueRepresentativeLabel,
            int valueTotalCapturePointsMax,
            int valueTotalCurrentCapturePoints,
            bool valueIsFullyControlled,
            bool valueIsDisputed,
            bool valueHasPartialCapture,
            TeamId valueControllingTeam,
            int valueControllingSlotIndex,
            string valueStatusText,
            List<SectorConstructionInfo> valueConstructions)
        {
            sector = valueSector;
            representativeConstruction = valueRepresentativeConstruction;
            representativeCell = valueRepresentativeCell;
            representativeLabel = valueRepresentativeLabel ?? string.Empty;
            totalCapturePointsMax = Mathf.Max(0, valueTotalCapturePointsMax);
            totalCurrentCapturePoints = Mathf.Max(0, valueTotalCurrentCapturePoints);
            constructionCount = valueConstructions != null ? valueConstructions.Count : 0;
            isFullyControlled = valueIsFullyControlled;
            isDisputed = valueIsDisputed;
            hasPartialCapture = valueHasPartialCapture;
            controllingTeam = valueControllingTeam;
            controllingSlotIndex = valueControllingSlotIndex;
            statusText = valueStatusText ?? string.Empty;

            constructions.Clear();
            if (valueConstructions != null)
                constructions.AddRange(valueConstructions);
        }
    }

    private static SectorManager instance;

    [SerializeField] private bool sectorLog;
    [Header("Neighbor Distance")]
    [SerializeField] private bool useTerrainCostForNeighborDistances = true;
    [SerializeField] private Tilemap neighborDistanceTilemap;
    [SerializeField] private TerrainDatabase neighborDistanceTerrainDatabase;
    [SerializeField] private UnitData neighborDistanceReferenceUnitData;  // foot reference (soldier)
    [SerializeField] private UnitData neighborDistanceVehicleUnitData;    // vehicle reference (APC)
    [SerializeField] private List<SectorInfo> sectorInfos = new List<SectorInfo>();
    [SerializeField] private List<SectorInfo> baseInfos   = new List<SectorInfo>();

    private readonly Dictionary<ConstructionSector, SectorInfo> sectorInfoBySector = new Dictionary<ConstructionSector, SectorInfo>();
    private readonly Dictionary<ConstructionSector, SectorInfo> baseInfoBySector   = new Dictionary<ConstructionSector, SectorInfo>();
    private Coroutine pendingRebuildRoutine;

    public static SectorManager Instance => EnsureInstance();
    public IReadOnlyList<SectorInfo> SectorInfos => sectorInfos;
    public IReadOnlyList<SectorInfo> BaseInfos   => baseInfos;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        EnsureInstance();
    }

    public static IReadOnlyList<SectorInfo> GetAllSectorInfos()
    {
        SectorManager manager = EnsureInstance();
        if (manager == null)
            return System.Array.Empty<SectorInfo>();

        if (manager.sectorInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.sectorInfos;
    }

    public static bool TryGetSectorInfo(ConstructionSector sector, out SectorInfo info)
    {
        SectorManager manager = EnsureInstance();
        if (manager == null)
        {
            info = null;
            return false;
        }

        if (manager.sectorInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.sectorInfoBySector.TryGetValue(sector, out info);
    }

    public static bool TryBuildNeighborDistanceDebug(ConstructionSector sector, List<SectorNeighborDistanceDebugEntry> entries)
    {
        if (entries == null)
            return false;

        entries.Clear();
        SectorManager manager = EnsureInstance();
        if (manager == null)
            return false;

        if (manager.sectorInfos.Count == 0)
            manager.RebuildFromActiveConstructions("neighbor-debug");

        if (!manager.sectorInfoBySector.TryGetValue(sector, out SectorInfo origin) || origin == null)
            return false;

        SectorNeighborDistanceContext context = manager.BuildNeighborDistanceContext();
        for (int i = 0; i < manager.sectorInfos.Count; i++)
        {
            SectorInfo other = manager.sectorInfos[i];
            if (other == null || other.Sector == sector)
                continue;

            var entry = new SectorNeighborDistanceDebugEntry
            {
                Sector = other.Sector,
                Distance = context.IsValid
                    ? float.MaxValue
                    : ComputeHexDistance(origin.RepresentativeCell, other.RepresentativeCell),
                Reachable = !context.IsValid,
                UsedTerrainCost = false,
            };

            if (context.IsValid &&
                TryComputeLandMovementDistance(origin.RepresentativeCell, other.RepresentativeCell, context, out int movementCost, entry.Path))
            {
                entry.Distance = movementCost;
                entry.UsedTerrainCost = true;
                entry.Reachable = true;
            }

            entries.Add(entry);
        }

        entries.Sort((a, b) =>
        {
            int d = a.Distance.CompareTo(b.Distance);
            return d != 0 ? d : ((int)a.Sector).CompareTo((int)b.Sector);
        });

        return true;
    }

    public static IReadOnlyList<SectorInfo> GetAllBaseInfos()
    {
        SectorManager manager = EnsureInstance();
        if (manager == null)
            return System.Array.Empty<SectorInfo>();

        if (manager.sectorInfos.Count == 0 && manager.baseInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.baseInfos;
    }

    public static bool TryGetBaseInfo(ConstructionSector sector, out SectorInfo info)
    {
        SectorManager manager = EnsureInstance();
        if (manager == null)
        {
            info = null;
            return false;
        }

        if (manager.sectorInfos.Count == 0 && manager.baseInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.baseInfoBySector.TryGetValue(sector, out info);
    }

    public static void RequestRebuildFromActiveConstructions(string reason = null)
    {
        SectorManager manager = EnsureInstance();
        if (manager == null)
            return;

        manager.QueueRebuild(reason);
    }

    private static SectorManager EnsureInstance()
    {
        if (instance != null)
            return instance;

        SectorManager existing = FindAnyObjectByType<SectorManager>();
        if (existing != null)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject(nameof(SectorManager));
        instance = go.AddComponent<SectorManager>();
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (Application.isPlaying && transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;
        QueueRebuild("on-enable");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        SaveGameManager.OnAfterLoadSuccess -= HandleAfterLoadSuccess;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        RebuildFromActiveConstructions("on-validate");
        EditorApplication.QueuePlayerLoopUpdate();
    }
#endif

    [ContextMenu("Rebuild From Active Constructions")]
    public void RebuildFromActiveConstructions()
    {
        RebuildFromActiveConstructions("manual");
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueRebuild($"scene-loaded:{scene.name}");
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        QueueRebuild($"active-team:{teamId}");
    }

    private void HandleAfterLoadSuccess()
    {
        QueueRebuild("after-load-success");
    }

    private void QueueRebuild(string reason)
    {
        if (!Application.isPlaying)
        {
            RebuildFromActiveConstructions(reason);
            return;
        }

        if (pendingRebuildRoutine != null)
            StopCoroutine(pendingRebuildRoutine);

        pendingRebuildRoutine = StartCoroutine(RebuildNextFrameRoutine(reason));
    }

    private IEnumerator RebuildNextFrameRoutine(string reason)
    {
        yield return null;
        pendingRebuildRoutine = null;
        RebuildFromActiveConstructions(reason);
    }

    // Distância em passos de hex (pointy-top, even-r offset — Unity m_CellLayout=1, cellSize.x≈0.866).
    // Público para uso pelo AI planner e outros sistemas que precisam de distância hex correta.
    public static float HexDistance(Vector3Int a, Vector3Int b)
    {
        int aq  = a.x - (a.y - (a.y & 1)) / 2;
        int bq  = b.x - (b.y - (b.y & 1)) / 2;
        int as_ = -aq - a.y;
        int bs  = -bq - b.y;
        return (Mathf.Abs(aq - bq) + Mathf.Abs(a.y - b.y) + Mathf.Abs(as_ - bs)) / 2f;
    }

    private static float ComputeHexDistance(Vector3Int a, Vector3Int b) => HexDistance(a, b);

    // Distância com custo de terreno usando o contexto já configurado no Inspector.
    // Retorna false se o contexto não estiver disponível; nesse caso usa HexDistance como fallback.
    public static bool TryGetLandMovementDistance(Vector3Int from, Vector3Int to, out int cost)
    {
        cost = 0;
        SectorManager manager = EnsureInstance();
        if (manager == null) return false;
        SectorNeighborDistanceContext ctx = manager.BuildNeighborDistanceContext();
        if (!ctx.IsValid) return false;
        from.z = 0; to.z = 0;
        return TryComputeLandMovementDistance(from, to, ctx, out cost, null);
    }

    public static bool TryGetLandMovementDistance(Vector3Int from, Vector3Int to, UnitData referenceUnitData, out int cost)
    {
        cost = 0;
        SectorManager manager = EnsureInstance();
        if (manager == null) return false;
        SectorNeighborDistanceContext ctx = manager.BuildNeighborDistanceContext(referenceUnitData);
        if (!ctx.IsValid) return false;
        from.z = 0; to.z = 0;
        return TryComputeLandMovementDistance(from, to, ctx, out cost, null);
    }

    private void RebuildFromActiveConstructions(string reason)
    {
        sectorInfos.Clear();
        sectorInfoBySector.Clear();
        baseInfos.Clear();
        baseInfoBySector.Clear();

        IReadOnlyList<ConstructionManager> allConstructions = GetTrackedConstructions();

        // Coleta HQs e fábricas antes de processar setores
        var hqBySlot = new Dictionary<int, (TeamId team, string name, Vector3Int cell)>();
        var factories = new List<ConstructionManager>();
        for (int i = 0; i < allConstructions.Count; i++)
        {
            ConstructionManager c = allConstructions[i];
            if (c == null) continue;
            if (c.IsPlayerHeadQuarter && c.SlotIndex >= 0 && !hqBySlot.ContainsKey(c.SlotIndex))
                hqBySlot[c.SlotIndex] = (c.TeamId, c.ConstructionDisplayName, c.CurrentCellPosition);
            if (c.CanProduceUnits && !c.IsPlayerHeadQuarter)
                factories.Add(c);
        }

        var grouped = new Dictionary<ConstructionSector, List<ConstructionManager>>();
        IReadOnlyList<ConstructionManager> constructions = allConstructions;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;

            if (!grouped.TryGetValue(construction.Sector, out List<ConstructionManager> list))
            {
                list = new List<ConstructionManager>();
                grouped[construction.Sector] = list;
            }

            list.Add(construction);
        }

        List<ConstructionSector> sectors = new List<ConstructionSector>(grouped.Keys);
        sectors.Sort((a, b) => ((int)a).CompareTo((int)b));

        SectorNeighborDistanceContext neighborDistanceContext = BuildNeighborDistanceContext();
        SectorNeighborDistanceContext vehicleDistanceContext = BuildNeighborDistanceContext(neighborDistanceVehicleUnitData);

        for (int i = 0; i < sectors.Count; i++)
        {
            ConstructionSector sector = sectors[i];
            List<ConstructionManager> constructionsInSector = grouped[sector];
            constructionsInSector.Sort(CompareConstructionStable);

            Vector2 centroid = ComputeCentroid(constructionsInSector);
            ConstructionManager representative = SelectRepresentativeConstruction(constructionsInSector, centroid);
            List<SectorConstructionInfo> entries = new List<SectorConstructionInfo>(constructionsInSector.Count);

            int totalCaptureMax = 0;
            int totalCurrentCapture = 0;
            bool hasPartialCapture = false;
            bool hasMixedOwners = false;
            TeamId firstOwner = constructionsInSector[0].TeamId;
            int firstOwnerSlot = constructionsInSector[0].SlotIndex;

            for (int c = 0; c < constructionsInSector.Count; c++)
            {
                ConstructionManager construction = constructionsInSector[c];
                entries.Add(new SectorConstructionInfo(construction));
                totalCaptureMax += Mathf.Max(0, construction.CapturePointsMax);
                totalCurrentCapture += Mathf.Max(0, construction.CurrentCapturePoints);

                if (construction.CurrentCapturePoints < construction.CapturePointsMax)
                    hasPartialCapture = true;
                if (construction.SlotIndex != firstOwnerSlot)
                    hasMixedOwners = true;
            }

            bool isFullyControlled = !hasMixedOwners && !hasPartialCapture && constructionsInSector.Count > 0;
            bool isDisputed = hasMixedOwners || hasPartialCapture;
            TeamId controllingTeam = hasMixedOwners || constructionsInSector.Count == 0 ? TeamId.Neutral : firstOwner;
            int controllingSlot = hasMixedOwners || constructionsInSector.Count == 0 ? -1 : firstOwnerSlot;
            string statusText = BuildStatusText(isFullyControlled, hasMixedOwners, hasPartialCapture, controllingTeam);

            SectorInfo info = new SectorInfo();
            Vector3Int representativeCell = representative != null ? representative.CurrentCellPosition : Vector3Int.zero;
            info.Apply(
                sector,
                representative,
                representativeCell,
                representative != null ? representative.ConstructionDisplayName : sector.ToString(),
                totalCaptureMax,
                totalCurrentCapture,
                isFullyControlled,
                isDisputed,
                hasPartialCapture,
                controllingTeam,
                controllingSlot,
                statusText,
                entries);

            // Distâncias por time (HQ + fábricas) — foot/vehicle terrain-aware, air = hex puro
            var slotDistanceMap = new Dictionary<int, SectorTeamDistances>();

            foreach (KeyValuePair<int, (TeamId team, string name, Vector3Int cell)> kv in hqBySlot)
            {
                if (!slotDistanceMap.TryGetValue(kv.Key, out SectorTeamDistances td))
                {
                    td = new SectorTeamDistances { SlotIndex = kv.Key, Team = kv.Value.team };
                    slotDistanceMap[kv.Key] = td;
                }
                td.Entries.Add(new SectorDistanceEntry
                {
                    ConstructionName = kv.Value.name,
                    InstanceId       = 0,
                    Distance         = ComputeSectorNeighborDistance(representativeCell, kv.Value.cell, neighborDistanceContext),
                    VehicleDistance  = ComputeSectorNeighborDistance(representativeCell, kv.Value.cell, vehicleDistanceContext),
                    AirDistance      = ComputeHexDistance(representativeCell, kv.Value.cell),
                    IsHQ             = true,
                });
            }

            foreach (ConstructionManager f in factories)
            {
                if (f.SlotIndex < 0)
                    continue;
                if (!slotDistanceMap.TryGetValue(f.SlotIndex, out SectorTeamDistances td))
                {
                    td = new SectorTeamDistances { SlotIndex = f.SlotIndex, Team = f.TeamId };
                    slotDistanceMap[f.SlotIndex] = td;
                }
                td.Entries.Add(new SectorDistanceEntry
                {
                    ConstructionName = f.ConstructionDisplayName,
                    InstanceId       = f.InstanceId,
                    Distance         = ComputeSectorNeighborDistance(representativeCell, f.CurrentCellPosition, neighborDistanceContext),
                    VehicleDistance  = ComputeSectorNeighborDistance(representativeCell, f.CurrentCellPosition, vehicleDistanceContext),
                    AirDistance      = ComputeHexDistance(representativeCell, f.CurrentCellPosition),
                    IsHQ             = false,
                });
            }

            info.ApplySectorDistances(new List<SectorTeamDistances>(slotDistanceMap.Values));

            if (ConstructionSectorHelper.IsBase(sector))
            {
                baseInfos.Add(info);
                baseInfoBySector[sector] = info;
            }
            else
            {
                sectorInfos.Add(info);
                sectorInfoBySector[sector] = info;
            }
        }

        // Segundo passo: 2 vizinhos capturáveis mais próximos por setor (células representativas)
        for (int i = 0; i < sectorInfos.Count; i++)
        {
            SectorInfo  infoA  = sectorInfos[i];
            Vector3Int  cellA  = infoA.RepresentativeCell;

            ConstructionSector best1 = default; float dist1 = float.MaxValue;
            ConstructionSector best2 = default; float dist2 = float.MaxValue;

            for (int j = 0; j < sectorInfos.Count; j++)
            {
                if (i == j) continue;
                SectorInfo other = sectorInfos[j];
                float d = ComputeSectorNeighborDistance(cellA, other.RepresentativeCell, neighborDistanceContext);
                if (d < dist1)
                {
                    dist2 = dist1; best2 = best1;
                    dist1 = d;     best1 = other.Sector;
                }
                else if (d < dist2)
                {
                    dist2 = d; best2 = other.Sector;
                }
            }

            infoA.ApplyNeighbors(best1, dist1, best2, dist2);
        }

        if (sectorLog)
            Debug.Log($"[SectorManager] rebuild reason={reason ?? "none"} sectors={sectorInfos.Count} bases={baseInfos.Count} constructions={constructions.Count}");
    }

    private SectorNeighborDistanceContext BuildNeighborDistanceContext(UnitData referenceUnitOverride = null)
    {
        if (!useTerrainCostForNeighborDistances)
            return default;

        Tilemap map = neighborDistanceTilemap != null ? neighborDistanceTilemap : ResolveNeighborDistanceTilemap();
        TerrainDatabase terrainDb = neighborDistanceTerrainDatabase != null ? neighborDistanceTerrainDatabase : ResolveNeighborDistanceTerrainDatabase();
        if (map == null || terrainDb == null)
            return default;

        var constructionsByCell = new Dictionary<Vector3Int, ConstructionManager>();
        IReadOnlyList<ConstructionManager> constructions = GetTrackedConstructions();
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            Vector3Int cell = construction.BoardTilemap == map
                ? construction.CurrentCellPosition
                : HexCoordinates.WorldToCell(map, construction.transform.position);
            cell.z = 0;
            if (!constructionsByCell.ContainsKey(cell))
                constructionsByCell[cell] = construction;
        }

        Tilemap[] gridMaps = map.layoutGrid != null
            ? map.layoutGrid.GetComponentsInChildren<Tilemap>(includeInactive: true)
            : System.Array.Empty<Tilemap>();

        RoadNetworkManager[] roadNetworks = Object.FindObjectsByType<RoadNetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        UnitData referenceUnitData = referenceUnitOverride != null
            ? referenceUnitOverride
            : neighborDistanceReferenceUnitData != null
            ? neighborDistanceReferenceUnitData
            : ResolveNeighborDistanceReferenceUnitData();

        return new SectorNeighborDistanceContext
        {
            Tilemap = map,
            TerrainDatabase = terrainDb,
            GridTilemaps = gridMaps,
            RoadNetworks = roadNetworks,
            ConstructionsByCell = constructionsByCell,
            ReferenceUnitData = referenceUnitData,
            IsValid = true,
        };
    }

    private static UnitData ResolveNeighborDistanceReferenceUnitData()
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if (unit.TryGetUnitData(out UnitData data) && data != null && data.unitClass == GameUnitClass.Infantry)
                return data;
        }

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && unit.TryGetUnitData(out UnitData data) && data != null && data.domain == Domain.Land)
                return data;
        }

        return null;
    }

    private static Tilemap ResolveNeighborDistanceTilemap()
    {
        CursorController cursor = Object.FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        ConstructionManager construction = Object.FindAnyObjectByType<ConstructionManager>();
        if (construction != null && construction.BoardTilemap != null)
            return construction.BoardTilemap;

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null && string.Equals(maps[i].name, "TileMap", System.StringComparison.OrdinalIgnoreCase))
                return maps[i];

        return maps != null && maps.Length > 0 ? maps[0] : null;
    }

    private static TerrainDatabase ResolveNeighborDistanceTerrainDatabase()
    {
        TurnStateManager turnState = Object.FindAnyObjectByType<TurnStateManager>();
        if (turnState != null && turnState.TerrainDatabaseRef != null)
            return turnState.TerrainDatabaseRef;

        MatchController match = Object.FindAnyObjectByType<MatchController>();
        if (match != null && match.TerrainDatabaseRef != null)
            return match.TerrainDatabaseRef;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
                return db;
        }
#endif

        return null;
    }

    private static float ComputeSectorNeighborDistance(Vector3Int from, Vector3Int to, SectorNeighborDistanceContext context)
    {
        if (context.IsValid)
        {
            if (TryComputeLandMovementDistance(from, to, context, out int movementCost, null))
                return movementCost;

            return float.MaxValue;
        }

        return ComputeHexDistance(from, to);
    }

    private static bool TryComputeLandMovementDistance(
        Vector3Int from,
        Vector3Int to,
        SectorNeighborDistanceContext context,
        out int movementCost,
        List<Vector3Int> path)
    {
        movementCost = 0;
        path?.Clear();
        if (!context.IsValid || context.Tilemap == null)
            return false;

        from.z = 0;
        to.z = 0;
        if (from == to)
        {
            path?.Add(from);
            return true;
        }

        var frontier = new List<Vector3Int> { from };
        var costByCell = new Dictionary<Vector3Int, int> { [from] = 0 };
        var cameFrom = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        var neighbors = new List<Vector3Int>(6);
        int expanded = 0;
        int maxExpanded = Mathf.Max(512, context.Tilemap.cellBounds.size.x * context.Tilemap.cellBounds.size.y);

        while (frontier.Count > 0 && expanded < maxExpanded)
        {
            int bestIndex = 0;
            int bestCost = costByCell[frontier[0]];
            for (int i = 1; i < frontier.Count; i++)
            {
                int candidateCost = costByCell[frontier[i]];
                if (candidateCost >= bestCost)
                    continue;

                bestIndex = i;
                bestCost = candidateCost;
            }

            Vector3Int current = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            expanded++;

            if (current == to)
            {
                movementCost = bestCost;
                BuildSectorPath(from, to, cameFrom, path);
                return true;
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(context.Tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (!TryGetLandEnterCost(next, context, out int enterCost))
                    continue;

                int nextCost = bestCost + enterCost;
                if (costByCell.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                costByCell[next] = nextCost;
                cameFrom[next] = current;
                if (!frontier.Contains(next))
                    frontier.Add(next);
            }
        }

        return false;
    }

    private static void BuildSectorPath(
        Vector3Int from,
        Vector3Int to,
        Dictionary<Vector3Int, Vector3Int> cameFrom,
        List<Vector3Int> path)
    {
        if (path == null)
            return;

        path.Clear();
        if (cameFrom == null || !cameFrom.ContainsKey(to))
            return;

        Vector3Int current = to;
        path.Add(current);
        int guard = 0;
        while (current != from && guard++ < 4096)
        {
            current = cameFrom[current];
            path.Add(current);
        }

        path.Reverse();
    }

    private static bool TryGetLandEnterCost(Vector3Int cell, SectorNeighborDistanceContext context, out int cost)
    {
        cost = 1;
        cell.z = 0;

        if (!HasAnyPaintedTileAtCell(cell, context))
            return false;

        ConstructionManager construction = null;
        if (context.ConstructionsByCell != null)
            context.ConstructionsByCell.TryGetValue(cell, out construction);

        if (construction != null)
        {
            if (context.ReferenceUnitData != null &&
                !ConstructionSupportsUnitData(construction, context.ReferenceUnitData))
                return false;
            if (context.ReferenceUnitData == null && !construction.SupportsLayerMode(Domain.Land, HeightLevel.Surface))
                return false;

            cost = 1;
            return true;
        }

        StructureData structure = ResolveStructureAtCell(cell, context);
        TerrainTypeData terrain = ResolveTerrainAtCell(cell, context);

        if (context.ReferenceUnitData != null)
            return TryGetUnitDataEnterCost(context.ReferenceUnitData, null, structure, terrain, out cost);

        if (structure != null)
        {
            if (!SupportsLayerMode(structure.domain, structure.heightLevel, structure.aditionalDomainsAllowed, Domain.Land, HeightLevel.Surface))
                return false;

            cost = Mathf.Max(1, structure.baseMovementCost);
            return true;
        }

        if (terrain == null)
            return false;

        if (!SupportsLayerMode(terrain.domain, terrain.heightLevel, terrain.aditionalDomainsAllowed, Domain.Land, HeightLevel.Surface))
            return false;

        cost = Mathf.Max(1, terrain.basicAutonomyCost);
        return true;
    }

    private static bool TryGetUnitDataEnterCost(
        UnitData unitData,
        ConstructionManager construction,
        StructureData structure,
        TerrainTypeData terrain,
        out int cost)
    {
        cost = 1;
        if (unitData == null)
            return false;

        if (construction != null)
        {
            if (!ConstructionSupportsUnitData(construction, unitData))
                return false;

            cost = 1;
            return true;
        }

        if (structure != null)
        {
            if (!SupportsLayerMode(structure.domain, structure.heightLevel, structure.aditionalDomainsAllowed, unitData.domain, unitData.heightLevel))
                return false;
            if (!UnitDataPassesSkillRules(unitData, structure.GetRequiredSkillsToEnter(terrain), structure.GetBlockedSkillsToEnter(terrain)))
                return false;

            cost = GetCostWithUnitDataSkillOverrides(structure.baseMovementCost, terrain != null ? terrain.skillCostOverrides : null, unitData);
            cost = GetCostWithUnitDataSkillOverrides(cost, structure.GetSkillCostOverrides(terrain), unitData);
            cost = Mathf.Max(1, cost);
            return true;
        }

        if (terrain == null)
            return false;
        if (!SupportsLayerMode(terrain.domain, terrain.heightLevel, terrain.aditionalDomainsAllowed, unitData.domain, unitData.heightLevel))
            return false;
        if (!UnitDataPassesSkillRules(unitData, terrain.requiredSkillsToEnter, terrain.blockedSkills))
            return false;

        cost = GetCostWithUnitDataSkillOverrides(terrain.basicAutonomyCost, terrain.skillCostOverrides, unitData);
        cost = Mathf.Max(1, cost);
        return true;
    }

    private static bool ConstructionSupportsUnitData(ConstructionManager construction, UnitData unitData)
    {
        if (construction == null || unitData == null)
            return false;

        if (!construction.SupportsLayerMode(unitData.domain, unitData.heightLevel))
            return false;

        return UnitDataPassesSkillRules(unitData, construction.GetRequiredSkillsToEnter(), construction.GetBlockedSkillsToEnter());
    }

    private static int GetCostWithUnitDataSkillOverrides(
        int baseCost,
        IReadOnlyList<TerrainSkillCostOverride> overrides,
        UnitData unitData)
    {
        int safeBase = Mathf.Max(1, baseCost);
        if (unitData == null || overrides == null)
            return safeBase;

        for (int i = 0; i < overrides.Count; i++)
        {
            TerrainSkillCostOverride entry = overrides[i];
            if (entry == null || entry.skill == null)
                continue;

            if (UnitDataHasSkill(unitData, entry.skill))
                return Mathf.Max(1, entry.autonomyCost);
        }

        return safeBase;
    }

    private static bool UnitDataPassesSkillRules(
        UnitData unitData,
        IReadOnlyList<SkillData> requiredSkills,
        IReadOnlyList<SkillData> blockedSkills)
    {
        if (unitData == null)
            return false;

        if (blockedSkills != null)
        {
            for (int i = 0; i < blockedSkills.Count; i++)
                if (blockedSkills[i] != null && UnitDataHasSkill(unitData, blockedSkills[i]))
                    return false;
        }

        if (requiredSkills == null || requiredSkills.Count == 0)
            return true;

        for (int i = 0; i < requiredSkills.Count; i++)
            if (requiredSkills[i] != null && UnitDataHasSkill(unitData, requiredSkills[i]))
                return true;

        return false;
    }

    private static bool UnitDataHasSkill(UnitData unitData, SkillData skill)
    {
        if (unitData == null || skill == null || unitData.skills == null)
            return false;

        if (unitData.skills.Contains(skill))
            return true;

        string skillId = !string.IsNullOrWhiteSpace(skill.id) ? skill.id : skill.name;
        for (int i = 0; i < unitData.skills.Count; i++)
        {
            SkillData owned = unitData.skills[i];
            if (owned == null)
                continue;
            if (owned == skill)
                return true;
            if (!string.IsNullOrWhiteSpace(skillId) &&
                (string.Equals(owned.id, skillId, System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(owned.name, skillId, System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(owned.displayName, skillId, System.StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static StructureData ResolveStructureAtCell(Vector3Int cell, SectorNeighborDistanceContext context)
    {
        if (context.RoadNetworks == null)
            return null;

        for (int i = 0; i < context.RoadNetworks.Length; i++)
        {
            RoadNetworkManager road = context.RoadNetworks[i];
            if (road == null)
                continue;

            Tilemap roadMap = road.BoardTilemap;
            if (context.Tilemap != null && roadMap != null && roadMap != context.Tilemap && roadMap.layoutGrid != context.Tilemap.layoutGrid)
                continue;

            if (road.TryGetStructureAtCell(cell, out StructureData structure) && structure != null)
                return structure;
        }

        return null;
    }

    private static TerrainTypeData ResolveTerrainAtCell(Vector3Int cell, SectorNeighborDistanceContext context)
    {
        if (context.TerrainDatabase == null)
            return null;

        TileBase tile = context.Tilemap != null ? context.Tilemap.GetTile(cell) : null;
        if (tile != null && context.TerrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData terrain) && terrain != null)
            return terrain;

        if (context.GridTilemaps == null)
            return null;

        for (int i = 0; i < context.GridTilemaps.Length; i++)
        {
            Tilemap map = context.GridTilemaps[i];
            if (map == null)
                continue;

            TileBase other = map.GetTile(cell);
            if (other != null && context.TerrainDatabase.TryGetByPaletteTile(other, out TerrainTypeData byGridTile) && byGridTile != null)
                return byGridTile;
        }

        return null;
    }

    private static bool HasAnyPaintedTileAtCell(Vector3Int cell, SectorNeighborDistanceContext context)
    {
        if (context.Tilemap != null && context.Tilemap.GetTile(cell) != null)
            return true;

        if (context.GridTilemaps == null)
            return false;

        for (int i = 0; i < context.GridTilemaps.Length; i++)
        {
            Tilemap map = context.GridTilemaps[i];
            if (map != null && map.GetTile(cell) != null)
                return true;
        }

        return false;
    }

    private static bool SupportsLayerMode(
        Domain nativeDomain,
        HeightLevel nativeHeight,
        IReadOnlyList<TerrainLayerMode> additionalModes,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (nativeDomain == targetDomain && nativeHeight == targetHeight)
            return true;

        if (additionalModes == null)
            return false;

        for (int i = 0; i < additionalModes.Count; i++)
        {
            TerrainLayerMode mode = additionalModes[i];
            if (mode.domain == targetDomain && mode.heightLevel == targetHeight)
                return true;
        }

        return false;
    }

    private struct SectorNeighborDistanceContext
    {
        public bool IsValid;
        public Tilemap Tilemap;
        public TerrainDatabase TerrainDatabase;
        public Tilemap[] GridTilemaps;
        public RoadNetworkManager[] RoadNetworks;
        public Dictionary<Vector3Int, ConstructionManager> ConstructionsByCell;
        public UnitData ReferenceUnitData;
    }

    private static IReadOnlyList<ConstructionManager> GetTrackedConstructions()
    {
        if (ConstructionManager.AllActive != null && ConstructionManager.AllActive.Count > 0)
            return ConstructionManager.AllActive;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ConstructionManager[] editorConstructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return editorConstructions ?? System.Array.Empty<ConstructionManager>();
        }
#endif

        ConstructionManager[] runtimeConstructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        return runtimeConstructions ?? System.Array.Empty<ConstructionManager>();
    }

    private static int CompareConstructionStable(ConstructionManager a, ConstructionManager b)
    {
        if (ReferenceEquals(a, b))
            return 0;
        if (a == null)
            return 1;
        if (b == null)
            return -1;

        int cellY = a.CurrentCellPosition.y.CompareTo(b.CurrentCellPosition.y);
        if (cellY != 0)
            return cellY;

        int cellX = a.CurrentCellPosition.x.CompareTo(b.CurrentCellPosition.x);
        if (cellX != 0)
            return cellX;

        return a.InstanceId.CompareTo(b.InstanceId);
    }

    private static Vector2 ComputeCentroid(List<ConstructionManager> constructions)
    {
        if (constructions == null || constructions.Count == 0)
            return Vector2.zero;

        Vector2 sum = Vector2.zero;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            sum += new Vector2(cell.x, cell.y);
        }

        return sum / Mathf.Max(1, constructions.Count);
    }

    private static ConstructionManager SelectRepresentativeConstruction(List<ConstructionManager> constructions, Vector2 centroid)
    {
        ConstructionManager best = null;
        float bestSqrDistance = float.MaxValue;
        int bestInstanceId = int.MaxValue;

        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null)
                continue;

            Vector3Int cell = construction.CurrentCellPosition;
            float sqrDistance = (new Vector2(cell.x, cell.y) - centroid).sqrMagnitude;
            int instanceId = construction.InstanceId;
            bool better = sqrDistance < bestSqrDistance
                || (Mathf.Approximately(sqrDistance, bestSqrDistance) && instanceId < bestInstanceId);

            if (!better)
                continue;

            best = construction;
            bestSqrDistance = sqrDistance;
            bestInstanceId = instanceId;
        }

        return best;
    }

    private static string BuildStatusText(bool isFullyControlled, bool hasMixedOwners, bool hasPartialCapture, TeamId controllingTeam)
    {
        if (isFullyControlled)
            return $"Controlado por {TeamUtils.GetName(controllingTeam)}";
        if (hasMixedOwners)
            return "Em disputa";
        if (hasPartialCapture)
            return "Sem controle total";

        return "Sem controle total";
    }
}
