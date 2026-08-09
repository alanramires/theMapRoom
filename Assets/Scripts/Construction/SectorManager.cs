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
        [SerializeField] public float  NavalDistance;   // naval reference — APROXIMADA: nenhum QG/fábrica fica na água,
                                                        // então mede água-mais-próxima-da-âncora → água-mais-próxima-do-setor
                                                        // e soma os dois trechos secos em hexes (ver ComputeApproxNavalDistance)
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

        public float GetHQNavalDistance()
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].IsHQ) return Entries[i].NavalDistance;
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
        // "Sem vizinho" e None, nao default: o enum nao tem 0 neutro (Alpha = 0).
        [HideInInspector][SerializeField] private ConstructionSector closestNeighbor1 = ConstructionSector.None;
        [HideInInspector][SerializeField] private float              closestNeighbor1Distance = float.MaxValue;
        [HideInInspector][SerializeField] private ConstructionSector closestNeighbor2 = ConstructionSector.None;
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

        // Aproximada: parte da água mais próxima do QG e chega na água mais próxima do setor.
        // float.MaxValue quando não há água ao alcance de um dos lados (setor mediterrâneo).
        public float GetNavalDistanceToHQ(PlayerSlotId slotId)
        {
            for (int i = 0; i < sectorDistances.Count; i++)
                if (sectorDistances[i].SlotIndex == slotId.Value) return sectorDistances[i].GetHQNavalDistance();
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

    // Cada Scene representa um mapa. O registro por cena impede que mapas
    // aditivos compartilhem setores, bases ou o catalogo de praias.
    private static readonly Dictionary<ulong, SectorManager> InstancesByScene =
        new Dictionary<ulong, SectorManager>();

    [SerializeField] private bool sectorLog;
    [Header("Neighbor Distance")]
    [SerializeField] private bool useTerrainCostForNeighborDistances = true;
    [SerializeField] private Tilemap neighborDistanceTilemap;
    [SerializeField] private TerrainDatabase neighborDistanceTerrainDatabase;
    [SerializeField] private UnitData neighborDistanceReferenceUnitData;  // foot reference (soldier)
    [SerializeField] private UnitData neighborDistanceVehicleUnitData;    // vehicle reference (APC)
    [SerializeField] private UnitData neighborDistanceNavalUnitData;      // naval reference (navio)
    [SerializeField] private int      navalApproachSearchRadius = 6;      // até onde procurar água em volta do QG/setor
    [Header("Named Beaches")]
    [SerializeField] private BeachManager beachManager;
    [SerializeField] private List<SectorInfo> sectorInfos = new List<SectorInfo>();
    [SerializeField] private List<SectorInfo> baseInfos   = new List<SectorInfo>();

    private readonly Dictionary<ConstructionSector, SectorInfo> sectorInfoBySector = new Dictionary<ConstructionSector, SectorInfo>();
    private readonly Dictionary<ConstructionSector, SectorInfo> baseInfoBySector   = new Dictionary<ConstructionSector, SectorInfo>();
    private Coroutine pendingRebuildRoutine;
    private int lastCompletedBoardRevision = int.MinValue;
    private int pendingRebuildBoardRevision = int.MinValue;

    public static SectorManager Instance => EnsureDefaultInstance();
    public Scene MapScene => gameObject.scene;
    public IReadOnlyList<SectorInfo> SectorInfos => sectorInfos;
    public IReadOnlyList<SectorInfo> BaseInfos   => baseInfos;
    public BeachManager BeachManagerRef => ResolveBeachManager();
    public IReadOnlyList<BeachManager.BeachInfo> MilitaryBeachInfos
    {
        get
        {
            BeachManager manager = ResolveBeachManager();
            return manager != null
                ? manager.Beaches
                : System.Array.Empty<BeachManager.BeachInfo>();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapAfterSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        SectorManager[] managers =
            FindObjectsByType<SectorManager>(FindObjectsInactive.Include);
        for (int i = 0; i < managers.Length; i++)
        {
            SectorManager manager = managers[i];
            if (manager != null
                && manager.gameObject.scene == activeScene)
            {
                InstancesByScene[GetSceneKey(activeScene)] = manager;
                return;
            }
        }

        Tilemap[] tilemaps =
            FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap != null
                && tilemap.gameObject.scene == activeScene)
            {
                EnsureInstance(activeScene);
                return;
            }
        }
    }

    public static SectorManager GetForScene(Scene scene) =>
        EnsureInstance(scene);

    public static SectorManager GetForTilemap(Tilemap tilemap) =>
        tilemap != null
            ? EnsureInstance(tilemap.gameObject.scene)
            : EnsureDefaultInstance();

    public static SectorManager GetForComponent(Component context) =>
        context != null
            ? EnsureInstance(context.gameObject.scene)
            : EnsureDefaultInstance();

    public static IReadOnlyList<SectorInfo> GetAllSectorInfos()
        => GetAllSectorInfos(EnsureDefaultInstance());

    public static IReadOnlyList<SectorInfo> GetAllSectorInfos(Scene scene)
        => GetAllSectorInfos(EnsureInstance(scene));

    public static IReadOnlyList<SectorInfo> GetAllSectorInfos(Tilemap tilemap)
        => GetAllSectorInfos(GetForTilemap(tilemap));

    private static IReadOnlyList<SectorInfo> GetAllSectorInfos(
        SectorManager manager)
    {
        if (manager == null)
            return System.Array.Empty<SectorInfo>();

        if (manager.sectorInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.sectorInfos;
    }

    public static bool TryGetSectorInfo(ConstructionSector sector, out SectorInfo info)
        => TryGetSectorInfo(
            EnsureDefaultInstance(),
            sector,
            out info);

    public static bool TryGetSectorInfo(
        Scene scene,
        ConstructionSector sector,
        out SectorInfo info)
        => TryGetSectorInfo(
            EnsureInstance(scene),
            sector,
            out info);

    public static bool TryGetSectorInfo(
        Tilemap tilemap,
        ConstructionSector sector,
        out SectorInfo info)
        => TryGetSectorInfo(
            GetForTilemap(tilemap),
            sector,
            out info);

    private static bool TryGetSectorInfo(
        SectorManager manager,
        ConstructionSector sector,
        out SectorInfo info)
    {
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
        => TryBuildNeighborDistanceDebug(
            EnsureDefaultInstance(),
            sector,
            entries);

    public static bool TryBuildNeighborDistanceDebug(
        Scene scene,
        ConstructionSector sector,
        List<SectorNeighborDistanceDebugEntry> entries)
        => TryBuildNeighborDistanceDebug(
            EnsureInstance(scene),
            sector,
            entries);

    private static bool TryBuildNeighborDistanceDebug(
        SectorManager manager,
        ConstructionSector sector,
        List<SectorNeighborDistanceDebugEntry> entries)
    {
        if (entries == null)
            return false;

        entries.Clear();
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
        => GetAllBaseInfos(EnsureDefaultInstance());

    public static IReadOnlyList<SectorInfo> GetAllBaseInfos(Scene scene)
        => GetAllBaseInfos(EnsureInstance(scene));

    public static IReadOnlyList<SectorInfo> GetAllBaseInfos(Tilemap tilemap)
        => GetAllBaseInfos(GetForTilemap(tilemap));

    private static IReadOnlyList<SectorInfo> GetAllBaseInfos(
        SectorManager manager)
    {
        if (manager == null)
            return System.Array.Empty<SectorInfo>();

        if (manager.sectorInfos.Count == 0 && manager.baseInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.baseInfos;
    }

    public static bool TryGetBaseInfo(ConstructionSector sector, out SectorInfo info)
        => TryGetBaseInfo(
            EnsureDefaultInstance(),
            sector,
            out info);

    public static bool TryGetBaseInfo(
        Scene scene,
        ConstructionSector sector,
        out SectorInfo info)
        => TryGetBaseInfo(
            EnsureInstance(scene),
            sector,
            out info);

    public static bool TryGetBaseInfo(
        Tilemap tilemap,
        ConstructionSector sector,
        out SectorInfo info)
        => TryGetBaseInfo(
            GetForTilemap(tilemap),
            sector,
            out info);

    private static bool TryGetBaseInfo(
        SectorManager manager,
        ConstructionSector sector,
        out SectorInfo info)
    {
        if (manager == null)
        {
            info = null;
            return false;
        }

        if (manager.sectorInfos.Count == 0 && manager.baseInfos.Count == 0)
            manager.RebuildFromActiveConstructions("first-query");

        return manager.baseInfoBySector.TryGetValue(sector, out info);
    }

    /// <summary>
    /// Praias militares nomeadas do mapa. O SectorManager apenas consulta o
    /// catalogo; conectividade, divisao e identidade pertencem ao BeachManager.
    /// </summary>
    public static IReadOnlyList<BeachManager.BeachInfo> GetAllMilitaryBeachInfos()
        => GetAllMilitaryBeachInfos(EnsureDefaultInstance());

    public static IReadOnlyList<BeachManager.BeachInfo>
        GetAllMilitaryBeachInfos(Scene scene)
        => GetAllMilitaryBeachInfos(EnsureInstance(scene));

    public static IReadOnlyList<BeachManager.BeachInfo>
        GetAllMilitaryBeachInfos(Tilemap tilemap)
        => GetAllMilitaryBeachInfos(GetForTilemap(tilemap));

    private static IReadOnlyList<BeachManager.BeachInfo>
        GetAllMilitaryBeachInfos(SectorManager manager)
    {
        return manager != null
            ? manager.MilitaryBeachInfos
            : System.Array.Empty<BeachManager.BeachInfo>();
    }

    public static bool TryGetMilitaryBeachAtCell(
        Vector3Int cell,
        out BeachManager.BeachInfo beach)
        => TryGetMilitaryBeachAtCell(
            EnsureDefaultInstance(),
            cell,
            out beach);

    public static bool TryGetMilitaryBeachAtCell(
        Scene scene,
        Vector3Int cell,
        out BeachManager.BeachInfo beach)
        => TryGetMilitaryBeachAtCell(
            EnsureInstance(scene),
            cell,
            out beach);

    public static bool TryGetMilitaryBeachAtCell(
        Tilemap tilemap,
        Vector3Int cell,
        out BeachManager.BeachInfo beach)
        => TryGetMilitaryBeachAtCell(
            GetForTilemap(tilemap),
            cell,
            out beach);

    private static bool TryGetMilitaryBeachAtCell(
        SectorManager manager,
        Vector3Int cell,
        out BeachManager.BeachInfo beach)
    {
        beach = null;
        BeachManager catalog = manager != null
            ? manager.ResolveBeachManager()
            : null;
        return catalog != null && catalog.TryGetAtCell(cell, out beach);
    }

    public static bool RequestRebuildFromActiveConstructions(string reason = null)
        => RequestRebuildFromActiveConstructions(
            EnsureDefaultInstance(),
            reason);

    public static bool RequestRebuildFromActiveConstructions(
        Scene scene,
        string reason = null)
        => RequestRebuildFromActiveConstructions(
            EnsureInstance(scene),
            reason);

    public static bool RequestRebuildFromActiveConstructions(
        Tilemap tilemap,
        string reason = null)
        => RequestRebuildFromActiveConstructions(
            GetForTilemap(tilemap),
            reason);

    private static bool RequestRebuildFromActiveConstructions(
        SectorManager manager,
        string reason)
    {
        if (manager == null)
            return false;

        return manager.QueueRebuild(reason);
    }

    public static void RebuildNowFromActiveConstructions(string reason = null)
        => RebuildNowFromActiveConstructions(
            EnsureDefaultInstance(),
            reason);

    public static void RebuildNowFromActiveConstructions(
        Scene scene,
        string reason = null)
        => RebuildNowFromActiveConstructions(
            EnsureInstance(scene),
            reason);

    public static void RebuildNowFromActiveConstructions(
        Tilemap tilemap,
        string reason = null)
        => RebuildNowFromActiveConstructions(
            GetForTilemap(tilemap),
            reason);

    private static void RebuildNowFromActiveConstructions(
        SectorManager manager,
        string reason)
    {
        if (manager == null)
            return;

        if (manager.pendingRebuildRoutine != null)
        {
            manager.StopCoroutine(manager.pendingRebuildRoutine);
            manager.pendingRebuildRoutine = null;
        }
        manager.pendingRebuildBoardRevision = int.MinValue;
        manager.RebuildFromActiveConstructions(reason);
    }

    private static SectorManager EnsureDefaultInstance()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (TryFindRegistered(activeScene, out SectorManager active))
            return active;

        SectorManager[] candidates =
            FindObjectsByType<SectorManager>(FindObjectsInactive.Include);
        SectorManager only = null;
        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            SectorManager candidate = candidates[i];
            if (candidate == null)
                continue;
            if (candidate.gameObject.scene == activeScene)
            {
                InstancesByScene[GetSceneKey(activeScene)] = candidate;
                return candidate;
            }
            only = candidate;
            count++;
        }

        if (count == 1)
        {
            InstancesByScene[GetSceneKey(only.gameObject.scene)] = only;
            return only;
        }
        if (count > 1)
        {
            Debug.LogError(
                "[SectorManager] consulta sem contexto com varios mapas " +
                "carregados. Informe a Scene, o Tilemap ou um Component.");
            return null;
        }

        return EnsureInstance(activeScene);
    }

    private static SectorManager EnsureInstance(Scene targetScene)
    {
        if (!targetScene.IsValid())
            targetScene = SceneManager.GetActiveScene();
        if (TryFindRegistered(targetScene, out SectorManager registered))
            return registered;

        SectorManager[] candidates =
            FindObjectsByType<SectorManager>(FindObjectsInactive.Include);
        for (int i = 0; i < candidates.Length; i++)
        {
            SectorManager existing = candidates[i];
            if (existing == null
                || existing.gameObject.scene != targetScene)
            {
                continue;
            }
            InstancesByScene[GetSceneKey(targetScene)] = existing;
            return existing;
        }

        GameObject host = new GameObject(nameof(SectorManager));
        if (targetScene.IsValid()
            && targetScene.isLoaded
            && host.scene != targetScene)
        {
            SceneManager.MoveGameObjectToScene(host, targetScene);
        }
        SectorManager created = host.AddComponent<SectorManager>();
        InstancesByScene[GetSceneKey(created.gameObject.scene)] = created;
        return created;
    }

    private static bool TryFindRegistered(
        Scene scene,
        out SectorManager manager)
    {
        manager = null;
        if (!scene.IsValid())
            return false;
        ulong key = GetSceneKey(scene);
        if (!InstancesByScene.TryGetValue(key, out manager))
            return false;
        if (manager != null && manager.gameObject.scene == scene)
            return true;
        InstancesByScene.Remove(key);
        manager = null;
        return false;
    }

    private static ulong GetSceneKey(Scene scene) =>
        scene.handle.GetRawData();

    private void Awake()
    {
        ulong key = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                key,
                out SectorManager existing)
            && existing != null
            && existing != this)
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                Debug.LogError(
                    $"[SectorManager] mais de um manager na cena " +
                    $"'{gameObject.scene.name}'. Mantenha somente um.",
                    this);
            return;
        }
        InstancesByScene[key] = this;
    }

    private void OnEnable()
    {
        ulong key = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                key,
                out SectorManager existing)
            && existing != null
            && existing != this)
        {
            return;
        }
        InstancesByScene[key] = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
        SaveGameManager.OnAfterLoadSuccess += HandleAfterLoadSuccess;
        ResolveBeachManager();
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
        ulong key = GetSceneKey(gameObject.scene);
        if (InstancesByScene.TryGetValue(
                key,
                out SectorManager registered)
            && registered == this)
        {
            InstancesByScene.Remove(key);
        }
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
        if (gameObject.scene != scene)
            return;
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

    private bool QueueRebuild(string reason)
    {
        if (!Application.isPlaying)
        {
            RebuildFromActiveConstructions(reason);
            return false;
        }

        int boardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        if (pendingRebuildRoutine != null &&
            pendingRebuildBoardRevision == boardRevision)
        {
            return true;
        }
        if (pendingRebuildRoutine == null &&
            lastCompletedBoardRevision == boardRevision)
        {
            return false;
        }

        if (pendingRebuildRoutine != null)
            StopCoroutine(pendingRebuildRoutine);

        pendingRebuildBoardRevision = boardRevision;
        pendingRebuildRoutine = StartCoroutine(
            RebuildNextFrameRoutine(reason));
        return true;
    }

    private IEnumerator RebuildNextFrameRoutine(string reason)
    {
        yield return null;
        pendingRebuildRoutine = null;
        pendingRebuildBoardRevision = int.MinValue;
        RebuildFromActiveConstructions(reason);
    }

    private BeachManager ResolveBeachManager()
    {
        if (beachManager != null
            && beachManager.gameObject.scene != gameObject.scene)
        {
            Debug.LogError(
                $"[SectorManager] ignorou BeachManager da cena " +
                $"'{beachManager.gameObject.scene.name}': este manager " +
                $"pertence a '{gameObject.scene.name}'.",
                this);
            beachManager = null;
        }

        if (beachManager == null)
        {
            // No setup recomendado, o BeachManager vive como filho deste
            // objeto. GetComponentInChildren tambem o encontra se estiver
            // inativo no Editor.
            beachManager = GetComponentInChildren<BeachManager>(
                includeInactive: true);

            if (beachManager == null)
            {
                BeachManager[] candidates =
                    FindObjectsByType<BeachManager>(
                        FindObjectsInactive.Include);
                for (int i = 0; i < candidates.Length; i++)
                {
                    BeachManager candidate = candidates[i];
                    if (candidate == null
                        || candidate.gameObject.scene != gameObject.scene)
                    {
                        continue;
                    }
                    beachManager = candidate;
                    break;
                }
            }

            if (beachManager == null && Application.isPlaying)
            {
                beachManager = BeachManager.GetOrCreateForScene(
                    gameObject.scene,
                    neighborDistanceTilemap,
                    neighborDistanceTerrainDatabase);
            }
        }

        if (beachManager != null)
        {
            beachManager.ConfigureSources(
                neighborDistanceTilemap,
                neighborDistanceTerrainDatabase);
        }
        return beachManager;
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
    public static bool TryGetLandMovementDistance(
        Vector3Int from,
        Vector3Int to,
        out int cost) =>
        TryGetLandMovementDistance(
            EnsureDefaultInstance(), from, to, null, out cost);

    public static bool TryGetLandMovementDistance(
        Tilemap tilemap,
        Vector3Int from,
        Vector3Int to,
        out int cost) =>
        TryGetLandMovementDistance(
            GetForTilemap(tilemap), from, to, null, out cost);

    public static bool TryGetLandMovementDistance(
        Vector3Int from,
        Vector3Int to,
        UnitData referenceUnitData,
        out int cost) =>
        TryGetLandMovementDistance(
            EnsureDefaultInstance(),
            from, to, referenceUnitData, out cost);

    public static bool TryGetLandMovementDistance(
        Tilemap tilemap,
        Vector3Int from,
        Vector3Int to,
        UnitData referenceUnitData,
        out int cost) =>
        TryGetLandMovementDistance(
            GetForTilemap(tilemap),
            from, to, referenceUnitData, out cost);

    private static bool TryGetLandMovementDistance(
        SectorManager manager,
        Vector3Int from,
        Vector3Int to,
        UnitData referenceUnitData,
        out int cost)
    {
        cost = 0;
        if (manager == null)
            return false;
        SectorNeighborDistanceContext context =
            manager.BuildNeighborDistanceContext(referenceUnitData);
        if (!context.IsValid)
            return false;
        from.z = 0;
        to.z = 0;
        return TryComputeLandMovementDistance(
            from, to, context, out cost, null);
    }

    // Distancia de movimento terrestre das celulas ATE 'target', numa unica
    // busca. maxCost permite manter a coleta dentro do envelope tatico.
    // Substitui N chamadas TryGetLandMovementDistance(cell, target) por 1 Dijkstra reverso (a
    // partir de target) + lookups baratos — o custo do two-turn da IA em unidades navais.
    //
    // A busca e reversa: ao expandir current para um possivel predecessor
    // next, cobra a transicao next->current. Isso preserva o canal realmente
    // usado (terreno, rodovia, ferrovia...) mesmo quando varios coexistem.
    // Celula ausente do mapa = inalcancavel (mesmo criterio da ponto-a-ponto; o chamador cai no
    // fallback de HexDistance, igual a CalculateRouteDistanceOrHex).
    public static bool TryBuildLandMovementDistanceToTargetMap(
        Vector3Int target,
        UnitData referenceUnitData,
        out Dictionary<Vector3Int, int> distanceToTarget,
        int maxCost = int.MaxValue) =>
        TryBuildLandMovementDistanceToTargetMap(
            EnsureDefaultInstance(),
            target,
            referenceUnitData,
            out distanceToTarget,
            maxCost);

    public static bool TryBuildLandMovementDistanceToTargetMap(
        Tilemap tilemap,
        Vector3Int target,
        UnitData referenceUnitData,
        out Dictionary<Vector3Int, int> distanceToTarget,
        int maxCost = int.MaxValue) =>
        TryBuildLandMovementDistanceToTargetMap(
            GetForTilemap(tilemap),
            target,
            referenceUnitData,
            out distanceToTarget,
            maxCost);

    private static bool TryBuildLandMovementDistanceToTargetMap(
        SectorManager manager,
        Vector3Int target,
        UnitData referenceUnitData,
        out Dictionary<Vector3Int, int> distanceToTarget,
        int maxCost)
    {
        distanceToTarget = null;
        if (manager == null) return false;
        SectorNeighborDistanceContext ctx = manager.BuildNeighborDistanceContext(referenceUnitData);
        if (!ctx.IsValid || ctx.Tilemap == null) return false;

        target.z = 0;
        if (!HasAnyPaintedTileAtCell(target, ctx))
            return false;

        // Dijkstra reverso a partir de target (mesma estrutura de TryComputeLandMovementDistance,
        // mas sem parada antecipada: espalha por tudo dentro do teto de expansao).
        int ctxId = BuildLandDistanceContextId(ctx);
        var costFromTarget = new Dictionary<Vector3Int, int> { [target] = 0 };
        var frontier = new List<Vector3Int> { target };
        var neighbors = new List<Vector3Int>(6);
        int expanded = 0;
        int maxExpanded = Mathf.Max(512, ctx.Tilemap.cellBounds.size.x * ctx.Tilemap.cellBounds.size.y);

        while (frontier.Count > 0 && expanded < maxExpanded)
        {
            int bestIndex = 0;
            int bestCost = costFromTarget[frontier[0]];
            for (int i = 1; i < frontier.Count; i++)
            {
                int candidateCost = costFromTarget[frontier[i]];
                if (candidateCost >= bestCost)
                    continue;
                bestIndex = i;
                bestCost = candidateCost;
            }

            Vector3Int current = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            expanded++;

            UnitMovementPathRules.GetImmediateHexNeighbors(ctx.Tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (!TryGetLandTransitionCost(
                        next,
                        current,
                        ctx,
                        ctxId,
                        out int enterCost))
                    continue;

                int nextCost = bestCost + enterCost;
                if (nextCost > maxCost)
                    continue;
                if (costFromTarget.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                costFromTarget[next] = nextCost;
                if (!frontier.Contains(next))
                    frontier.Add(next);
            }
        }

        // A relaxacao reversa ja produziu diretamente D(cell->target).
        distanceToTarget = new Dictionary<Vector3Int, int>(
            costFromTarget.Count);
        foreach (KeyValuePair<Vector3Int, int> kv in costFromTarget)
        {
            distanceToTarget[kv.Key] = Mathf.Max(0, kv.Value);
        }
        return true;
    }

    private void RebuildFromActiveConstructions(string reason)
    {
        double rebuildStart = Time.realtimeSinceStartupAsDouble;
        // Consulta sem copiar a verdade do catalogo. Os servicos de LZ podem
        // usar a mesma instancia e os mesmos BeachIds.
        BeachManager namedBeachCatalog = ResolveBeachManager();
        if (namedBeachCatalog == null
            && string.Equals(
                reason,
                "manual",
                System.StringComparison.Ordinal))
        {
            Debug.LogError(
                "[SectorManager] BeachManager nao encontrado nesta cena. " +
                "Coloque-o como filho do SectorManager ou atribua a " +
                "referencia serializada.",
                this);
        }
        int namedBeachCount = namedBeachCatalog != null
            ? namedBeachCatalog.Beaches.Count
            : 0;
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
        int unassignedCapturables = 0;
        List<string> unassignedNames = sectorLog ? new List<string>() : null;
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction == null || !construction.IsCapturable || construction.CapturePointsMax <= 0)
                continue;

            // None nao e um setor: e a AUSENCIA de setor. Agrupando por ele, todo
            // predio solto do mapa caia num setor fantasma "None" — com uma celula
            // representativa arbitraria, vizinhos, distancias e um objetivo no
            // planner. Quem nao tem setor fica de fora do grafo e e capturado pela
            // oportunidade, nao pelo plano.
            if (!ConstructionSectorHelper.IsRealSector(construction.Sector))
            {
                unassignedCapturables++;
                unassignedNames?.Add(
                    $"{construction.ConstructionDisplayName}@{construction.CurrentCellPosition.x},{construction.CurrentCellPosition.y}");
                continue;
            }

            if (!grouped.TryGetValue(construction.Sector, out List<ConstructionManager> list))
            {
                list = new List<ConstructionManager>();
                grouped[construction.Sector] = list;
            }

            list.Add(construction);
        }

        List<ConstructionSector> sectors = new List<ConstructionSector>(grouped.Keys);
        sectors.Sort((a, b) => ((int)a).CompareTo((int)b));

        ResetSectorSearchDebugCounters();
        double contextsStartMs = Time.realtimeSinceStartupAsDouble;
        SectorNeighborDistanceContext neighborDistanceContext = BuildNeighborDistanceContext();
        SectorNeighborDistanceContext vehicleDistanceContext = BuildNeighborDistanceContext(neighborDistanceVehicleUnitData);
        SectorNeighborDistanceContext navalDistanceContext = BuildNavalDistanceContext();
        double contextsMs =
            (Time.realtimeSinceStartupAsDouble - contextsStartMs) * 1000d;
        // Os contextos tambem rodam buscas? Separar o que eles gastaram do que os
        // loops de setor gastaram, senao o total nao diz onde esta o tempo.
        double contextsSearchMs = searchDebugMs;
        int contextsSearchCalls = searchDebugCalls;
        var navalApproachCache = new Dictionary<Vector3Int, List<Vector3Int>>();
        double sectorLoopStartMs = Time.realtimeSinceStartupAsDouble;

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
                    NavalDistance    = ComputeApproxNavalDistance(representativeCell, kv.Value.cell, navalDistanceContext, navalApproachCache, navalApproachSearchRadius),
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
                    NavalDistance    = ComputeApproxNavalDistance(representativeCell, f.CurrentCellPosition, navalDistanceContext, navalApproachCache, navalApproachSearchRadius),
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

        double sectorLoopMs =
            (Time.realtimeSinceStartupAsDouble - sectorLoopStartMs) * 1000d;
        double neighborPassStartMs = Time.realtimeSinceStartupAsDouble;

        // Segundo passo: 2 vizinhos capturáveis mais próximos por setor (células representativas)
        for (int i = 0; i < sectorInfos.Count; i++)
        {
            SectorInfo  infoA  = sectorInfos[i];
            Vector3Int  cellA  = infoA.RepresentativeCell;

            // None EXPLICITO, nunca default: Alpha vale 0, entao default(ConstructionSector)
            // e um setor de verdade. Um setor sozinho no mapa (ou sem rota ate ninguem)
            // saia daqui apontando Alpha como vizinho, e o consumidor acreditava.
            ConstructionSector best1 = ConstructionSector.None; float dist1 = float.MaxValue;
            ConstructionSector best2 = ConstructionSector.None; float dist2 = float.MaxValue;

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
        {
            // semSetor conta capturaveis que ficaram FORA do grafo. Zero e o normal
            // num mapa terminado; qualquer numero ali e prédio que o autor esqueceu
            // de rotular — antes isso virava um setor fantasma e ninguem via.
            string unassignedDetail = unassignedCapturables > 0 && unassignedNames != null
                ? $" [{string.Join(", ", unassignedNames)}]"
                : string.Empty;
            Debug.Log($"[SectorManager] rebuild reason={reason ?? "none"} sectors={sectorInfos.Count} bases={baseInfos.Count} praias={namedBeachCount} constructions={constructions.Count} semSetor={unassignedCapturables}{unassignedDetail}");
        }

        double neighborPassMs =
            (Time.realtimeSinceStartupAsDouble - neighborPassStartMs) * 1000d;

        lastCompletedBoardRevision = ThreatRevisionTracker.GlobalBoardRevision;
        if (Application.isPlaying)
        {
            double totalMs =
                (Time.realtimeSinceStartupAsDouble - rebuildStart) * 1000d;
            Debug.Log(
                $"[SectorManager][Perf] rebuild reason={reason ?? "none"} " +
                $"revision={lastCompletedBoardRevision} " +
                $"sectors={sectorInfos.Count} bases={baseInfos.Count} " +
                $"total={totalMs:F1}ms");
            Debug.Log(
                $"[SectorManager][Perf][Steps] contexts={contextsMs:F1}ms " +
                $"(search={contextsSearchMs:F1}ms calls={contextsSearchCalls}) " +
                $"sectorLoop={sectorLoopMs:F1}ms neighborPass={neighborPassMs:F1}ms | " +
                $"search.calls={searchDebugCalls} search.ms={searchDebugMs:F1} " +
                $"search.hits={searchDebugHits} " +
                $"search.failures={searchDebugFailures} " +
                $"search.exhausted={searchDebugExhausted} " +
                $"search.expanded={searchDebugExpanded} " +
                $"cache.size={landDistanceCache.Count} | " +
                $"vizinhos={(searchDebugNeighborTicks * 1000d / System.Diagnostics.Stopwatch.Frequency):F1}ms " +
                $"transicoes={searchDebugTransitionCalls} " +
                $"rota.calls={searchDebugRouteCalls} " +
                $"rota.cache={searchDebugRouteCacheHits} " +
                $"rota.topologia={searchDebugRouteTopologyHits} " +
                $"rota.varreduraRede={searchDebugRouteNetworkScans} " +
                $"terreno={searchDebugTerrainCacheHits}/{searchDebugTerrainResolves} " +
                $"tile={searchDebugPaintedCacheHits}/{searchDebugPaintedResolves} | " +
                $"constructions={GetTrackedConstructions().Count} " +
                $"unaccounted={(totalMs - contextsMs - sectorLoopMs - neighborPassMs):F1}ms");
        }
    }

    private SectorNeighborDistanceContext BuildNeighborDistanceContext(UnitData referenceUnitOverride = null)
    {
        if (!useTerrainCostForNeighborDistances)
            return default;

        Tilemap map = neighborDistanceTilemap != null
            && neighborDistanceTilemap.gameObject.scene == gameObject.scene
                ? neighborDistanceTilemap
                : ResolveNeighborDistanceTilemap();
        TerrainDatabase terrainDb = neighborDistanceTerrainDatabase != null ? neighborDistanceTerrainDatabase : ResolveNeighborDistanceTerrainDatabase();
        if (map == null || terrainDb == null)
            return default;

        var constructionsByCell = new Dictionary<Vector3Int, ConstructionManager>();
        IReadOnlyList<ConstructionManager> constructions =
            GetTrackedConstructions();
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

        RoadNetworkManager[] allRoadNetworks =
            Object.FindObjectsByType<RoadNetworkManager>(
                FindObjectsInactive.Include);
        var localRoadNetworks = new List<RoadNetworkManager>();
        for (int i = 0; i < allRoadNetworks.Length; i++)
        {
            RoadNetworkManager road = allRoadNetworks[i];
            if (road != null
                && road.gameObject.scene == gameObject.scene)
            {
                localRoadNetworks.Add(road);
            }
        }
        UnitData referenceUnitData = referenceUnitOverride != null
            ? referenceUnitOverride
            : neighborDistanceReferenceUnitData != null
            ? neighborDistanceReferenceUnitData
            : ResolveNeighborDistanceReferenceUnitData();
        BoardTopologyIndex.TryGetFor(
            map,
            out BoardTopologyIndex topology);

        return new SectorNeighborDistanceContext
        {
            Tilemap = map,
            TerrainDatabase = terrainDb,
            GridTilemaps = gridMaps,
            RoadNetworks = localRoadNetworks.ToArray(),
            Topology = topology,
            ConstructionsByCell = constructionsByCell,
            ReferenceUnitData = referenceUnitData,
            IsValid = true,
        };
    }

    // Contexto naval: sem uma referência naval de verdade o contexto sai inválido de propósito,
    // porque o fallback do builder genérico é o soldado a pé — mediria terra e chamaria de mar.
    private SectorNeighborDistanceContext BuildNavalDistanceContext()
    {
        UnitData navalUnitData = neighborDistanceNavalUnitData != null
            ? neighborDistanceNavalUnitData
            : ResolveNeighborDistanceNavalUnitData();

        return navalUnitData != null ? BuildNeighborDistanceContext(navalUnitData) : default;
    }

    private UnitData ResolveNeighborDistanceNavalUnitData()
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null
                && unit.gameObject.scene == gameObject.scene
                && unit.TryGetUnitData(out UnitData data)
                && data != null
                && data.domain == Domain.Naval)
                return data;
        }

        return null;
    }

    // Distância naval APROXIMADA até uma âncora terrestre (QG/fábrica), que por definição não fica
    // na água: mede da água mais próxima da âncora até a água mais próxima do setor e soma os dois
    // trechos secos (em hexes) nas pontas. Sem água ao alcance de um dos lados → float.MaxValue.
    private static float ComputeApproxNavalDistance(
        Vector3Int sectorCell,
        Vector3Int anchorCell,
        SectorNeighborDistanceContext navalContext,
        Dictionary<Vector3Int, List<Vector3Int>> approachCache,
        int searchRadius)
    {
        if (!navalContext.IsValid || navalContext.ReferenceUnitData == null)
            return float.MaxValue;

        List<Vector3Int> embarkCells   = GetNavalApproachCells(anchorCell, navalContext, approachCache, searchRadius);
        List<Vector3Int> approachCells = GetNavalApproachCells(sectorCell, navalContext, approachCache, searchRadius);
        if (embarkCells.Count == 0 || approachCells.Count == 0)
            return float.MaxValue;

        float best = float.MaxValue;
        for (int e = 0; e < embarkCells.Count; e++)
        {
            for (int a = 0; a < approachCells.Count; a++)
            {
                if (!TryComputeLandMovementDistance(embarkCells[e], approachCells[a], navalContext, out int navalCost, null))
                    continue;

                float total = ComputeHexDistance(anchorCell, embarkCells[e])
                            + navalCost
                            + ComputeHexDistance(approachCells[a], sectorCell);
                if (total < best)
                    best = total;
            }

            // O ponto de embarque mais próximo já resolve; os seguintes só existem como plano B
            // quando aquela água é um lago isolado (nenhuma rota atinge o setor).
            if (best < float.MaxValue)
                break;
        }

        return best;
    }

    private static List<Vector3Int> GetNavalApproachCells(
        Vector3Int origin,
        SectorNeighborDistanceContext navalContext,
        Dictionary<Vector3Int, List<Vector3Int>> cache,
        int searchRadius)
    {
        origin.z = 0;
        if (cache != null && cache.TryGetValue(origin, out List<Vector3Int> cached))
            return cached;

        var result = new List<Vector3Int>();
        CollectNavalApproachCells(origin, navalContext, searchRadius, MaxNavalApproachCandidates, result);
        if (cache != null)
            cache[origin] = result;
        return result;
    }

    private const int MaxNavalApproachCandidates = 2;

    // Anéis crescentes sobre células pintadas (transitáveis ou não — o que interessa é a forma do
    // mapa) até achar as primeiras células onde a referência naval consegue entrar.
    private static void CollectNavalApproachCells(
        Vector3Int origin,
        SectorNeighborDistanceContext navalContext,
        int searchRadius,
        int maxCandidates,
        List<Vector3Int> result)
    {
        result.Clear();
        if (!navalContext.IsValid || navalContext.Tilemap == null)
            return;

        origin.z = 0;
        var visited   = new HashSet<Vector3Int> { origin };
        var current   = new List<Vector3Int> { origin };
        var next      = new List<Vector3Int>();
        var neighbors = new List<Vector3Int>(6);

        for (int ring = 0; ring <= Mathf.Max(0, searchRadius) && current.Count > 0; ring++)
        {
            for (int i = 0; i < current.Count; i++)
            {
                Vector3Int cell = current[i];
                if (TryGetLandEnterCost(cell, navalContext, out _))
                {
                    result.Add(cell);
                    if (result.Count >= maxCandidates)
                        return;
                }

                UnitMovementPathRules.GetImmediateHexNeighbors(navalContext.Tilemap, cell, neighbors);
                for (int n = 0; n < neighbors.Count; n++)
                {
                    Vector3Int neighbor = neighbors[n];
                    neighbor.z = 0;
                    if (!visited.Add(neighbor))
                        continue;
                    if (!HasAnyPaintedTileAtCell(neighbor, navalContext))
                        continue;

                    next.Add(neighbor);
                }
            }

            current.Clear();
            current.AddRange(next);
            next.Clear();
        }
    }

    private UnitData ResolveNeighborDistanceReferenceUnitData()
    {
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null
                || unit.gameObject.scene != gameObject.scene)
                continue;
            if (unit.TryGetUnitData(out UnitData data) && data != null && data.unitClass == GameUnitClass.Infantry)
                return data;
        }

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit != null
                && unit.gameObject.scene == gameObject.scene
                && unit.TryGetUnitData(out UnitData data)
                && data != null
                && data.domain == Domain.Land)
                return data;
        }

        return null;
    }

    private Tilemap ResolveNeighborDistanceTilemap()
    {
        CursorController[] cursors =
            Object.FindObjectsByType<CursorController>(
                FindObjectsInactive.Include);
        for (int i = 0; i < cursors.Length; i++)
        {
            CursorController cursor = cursors[i];
            if (cursor != null
                && cursor.gameObject.scene == gameObject.scene
                && cursor.BoardTilemap != null)
            {
                return cursor.BoardTilemap;
            }
        }

        IReadOnlyList<ConstructionManager> constructions =
            GetTrackedConstructions();
        for (int i = 0; i < constructions.Count; i++)
        {
            ConstructionManager construction = constructions[i];
            if (construction != null && construction.BoardTilemap != null)
                return construction.BoardTilemap;
        }

        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(
            FindObjectsInactive.Include);
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null
                && maps[i].gameObject.scene == gameObject.scene
                && string.Equals(
                    maps[i].name,
                    "TileMap",
                    System.StringComparison.OrdinalIgnoreCase))
                return maps[i];

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null
                && maps[i].gameObject.scene == gameObject.scene)
            {
                return maps[i];
            }
        }
        return null;
    }

    private TerrainDatabase ResolveNeighborDistanceTerrainDatabase()
    {
        TurnStateManager[] turnStates =
            Object.FindObjectsByType<TurnStateManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < turnStates.Length; i++)
        {
            TurnStateManager turnState = turnStates[i];
            if (turnState != null
                && turnState.gameObject.scene == gameObject.scene
                && turnState.TerrainDatabaseRef != null)
            {
                return turnState.TerrainDatabaseRef;
            }
        }

        MatchController[] matches =
            Object.FindObjectsByType<MatchController>(
                FindObjectsInactive.Include);
        for (int i = 0; i < matches.Length; i++)
        {
            MatchController match = matches[i];
            if (match != null
                && match.gameObject.scene == gameObject.scene
                && match.TerrainDatabaseRef != null)
            {
                return match.TerrainDatabaseRef;
            }
        }

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

    // Contadores de diagnostico da busca: o rebuild inteiro so reporta um total,
    // e sem separar quantas buscas rodaram e quantos nos cada uma expandiu nao da
    // para distinguir "muitas buscas baratas" de "poucas buscas varrendo o mapa".
    private static int searchDebugCalls;
    private static int searchDebugFailures;
    private static int searchDebugExpanded;
    private static int searchDebugExhausted;
    private static double searchDebugMs;

    // Recorte do custo DENTRO da busca. Contadores sao int++ (praticamente de
    // graca, mesmo em milhoes de chamadas); o tempo e medido so por EXPANSAO,
    // nao por vizinho, para o proprio cronometro nao virar o custo dominante.
    private static long searchDebugNeighborTicks;
    private static int searchDebugTransitionCalls;
    private static int searchDebugRouteCalls;
    private static int searchDebugRouteCacheHits;
    private static int searchDebugRouteTopologyHits;
    private static int searchDebugRouteNetworkScans;
    private static int searchDebugTerrainResolves;
    private static int searchDebugTerrainCacheHits;
    private static int searchDebugPaintedResolves;
    private static int searchDebugPaintedCacheHits;

    private static void ResetSectorSearchDebugCounters()
    {
        searchDebugCalls = 0;
        searchDebugFailures = 0;
        searchDebugExpanded = 0;
        searchDebugExhausted = 0;
        searchDebugHits = 0;
        searchDebugMs = 0d;
        searchDebugNeighborTicks = 0;
        searchDebugTransitionCalls = 0;
        searchDebugRouteCalls = 0;
        searchDebugRouteCacheHits = 0;
        searchDebugRouteTopologyHits = 0;
        searchDebugRouteNetworkScans = 0;
        searchDebugTerrainResolves = 0;
        searchDebugTerrainCacheHits = 0;
        searchDebugPaintedResolves = 0;
        searchDebugPaintedCacheHits = 0;
    }

    // Memoizacao das distancias de movimento entre celulas.
    //
    // O rebuild rodava 88 buscas identicas a cada mudanca de turno (medido:
    // search.calls=88, expanded=47537, ms=1814 -- os mesmos numeros nos tres
    // rebuilds seguidos) porque o gatilho e GlobalBoardRevision, e comprar uma
    // unidade incrementa a revisao global. Distancia entre PREDIOS nao muda
    // quando nasce um soldado.
    //
    // O custo de travessia depende de: terreno, estrutura/rota, presenca e tipo
    // da construcao no hex, e a UnitData de referencia do contexto. Conferido em
    // TryGetLandEnterCost/TryGetLandTransitionCost: NAO le dono, capture points
    // nem unidades. Logo a impressao digital abaixo cobre todas as entradas.
    //
    // Repintar terreno em runtime nao e mecanica deste jogo; se passar a ser,
    // chame InvalidateLandDistanceCache() no ponto que repinta.
    private readonly struct LandDistanceCacheKey : System.IEquatable<LandDistanceCacheKey>
    {
        private readonly int fromX;
        private readonly int fromY;
        private readonly int toX;
        private readonly int toY;
        private readonly int contextId;

        public LandDistanceCacheKey(
            Vector3Int from,
            Vector3Int to,
            int contextId)
        {
            fromX = from.x;
            fromY = from.y;
            toX = to.x;
            toY = to.y;
            this.contextId = contextId;
        }

        public bool Equals(LandDistanceCacheKey other)
        {
            return fromX == other.fromX
                && fromY == other.fromY
                && toX == other.toX
                && toY == other.toY
                && contextId == other.contextId;
        }

        public override bool Equals(object obj)
        {
            return obj is LandDistanceCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + fromX;
                hash = (hash * 31) + fromY;
                hash = (hash * 31) + toX;
                hash = (hash * 31) + toY;
                hash = (hash * 31) + contextId;
                return hash;
            }
        }
    }

    private readonly struct CellContextCacheKey :
        System.IEquatable<CellContextCacheKey>
    {
        private readonly int x;
        private readonly int y;
        private readonly int contextId;

        public CellContextCacheKey(Vector3Int cell, int contextId)
        {
            x = cell.x;
            y = cell.y;
            this.contextId = contextId;
        }

        public bool Equals(CellContextCacheKey other) =>
            x == other.x
            && y == other.y
            && contextId == other.contextId;

        public override bool Equals(object obj) =>
            obj is CellContextCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + x;
                hash = (hash * 31) + y;
                hash = (hash * 31) + contextId;
                return hash;
            }
        }
    }

    private const int LandDistanceUnreachable = int.MinValue;
    private static readonly Dictionary<LandDistanceCacheKey, int> landDistanceCache =
        new Dictionary<LandDistanceCacheKey, int>(512);
    private static int landDistanceCacheFingerprint = int.MinValue;

    // Terreno e "existe tile aqui" dependem SO dos tilemaps, nao da unidade de
    // referencia — e nao mudam durante um rebuild. Sem memoizar, cada uma das
    // ~2 milhoes de transicoes reabria Tilemap.GetTile em todas as camadas do
    // grid: interop gerenciado->nativo, milhoes de vezes, resolvendo os mesmos
    // hexes. Invalida junto com landDistanceCache, pelo mesmo fingerprint de
    // layout.
    // Custo de rota por PAR de celulas vizinhas. Medido: 1,9 milhao de chamadas
    // sobre ~9.700 pares possiveis no tabuleiro — ~196 consultas repetidas por
    // par, porque as buscas atravessam os mesmos hexes de novo e de novo. E o
    // fallback e caro: o indice de topologia responde <1% das vezes e o resto
    // varre RoadNetworks linearmente.
    private readonly struct RouteEnterCostResult
    {
        public readonly bool Found;
        public readonly int Cost;
        public readonly bool HasDeclaredRouteEdge;

        public RouteEnterCostResult(bool found, int cost, bool hasDeclaredRouteEdge)
        {
            Found = found;
            Cost = cost;
            HasDeclaredRouteEdge = hasDeclaredRouteEdge;
        }
    }

    private static readonly Dictionary<LandDistanceCacheKey, RouteEnterCostResult> routeEnterCostCache =
        new Dictionary<LandDistanceCacheKey, RouteEnterCostResult>();

    private static readonly Dictionary<CellContextCacheKey, TerrainTypeData>
        terrainByCellCache =
            new Dictionary<CellContextCacheKey, TerrainTypeData>();
    private static readonly Dictionary<CellContextCacheKey, bool>
        paintedByCellCache =
            new Dictionary<CellContextCacheKey, bool>();
    private static int searchDebugHits;

    public static void InvalidateLandDistanceCache()
    {
        landDistanceCache.Clear();
        routeEnterCostCache.Clear();
        terrainByCellCache.Clear();
        paintedByCellCache.Clear();
        landDistanceCacheFingerprint = int.MinValue;
    }

    private static int BuildLandDistanceContextId(SectorNeighborDistanceContext context)
    {
        unchecked
        {
            int hash = BuildMapContextId(context);
            hash = (hash * 31) + (context.ReferenceUnitData != null
                ? context.ReferenceUnitData.GetEntityId().GetHashCode()
                : 0);
            return hash;
        }
    }

    private static int BuildMapContextId(
        SectorNeighborDistanceContext context)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + (context.Tilemap != null
                ? context.Tilemap.GetEntityId().GetHashCode()
                : 0);
            hash = (hash * 31) + (context.TerrainDatabase != null
                ? context.TerrainDatabase.GetEntityId().GetHashCode()
                : 0);
            return hash;
        }
    }

    // Impressao digital do LAYOUT: so o que a funcao de custo consegue ler.
    // Ordem-independente de proposito (a ordem do registro nao e garantida),
    // por isso cada construcao contribui via soma comutativa.
    private static int BuildLandDistanceLayoutFingerprint()
    {
        IReadOnlyList<ConstructionManager> constructions =
            GetAllTrackedConstructions();
        unchecked
        {
            int accumulated = 0;
            int counted = 0;
            for (int i = 0; i < constructions.Count; i++)
            {
                ConstructionManager construction = constructions[i];
                if (construction == null)
                    continue;

                Vector3Int cell = construction.CurrentCellPosition;
                int entry = 17;
                entry = (entry * 31) + cell.x;
                entry = (entry * 31) + cell.y;
                entry = (entry * 31) +
                    GetSceneKey(construction.gameObject.scene).GetHashCode();
                entry = (entry * 31) + construction.GetBaseMovementCost();
                // Sem a identidade, trocar um predio por outro de mesmo custo no
                // mesmo hex passaria batido, e SupportsLayerMode/InheritsTerrainRules
                // do novo tipo poderiam diferir.
                entry = (entry * 31) + construction.InstanceId;
                accumulated += entry;
                counted++;
            }

            return (accumulated * 397) ^ counted;
        }
    }

    private static void EnsureLandDistanceCacheFresh()
    {
        int fingerprint = BuildLandDistanceLayoutFingerprint();
        if (landDistanceCacheFingerprint == fingerprint)
            return;

        landDistanceCache.Clear();
        routeEnterCostCache.Clear();
        terrainByCellCache.Clear();
        paintedByCellCache.Clear();
        landDistanceCacheFingerprint = fingerprint;
    }

    private static bool TryComputeLandMovementDistance(
        Vector3Int from,
        Vector3Int to,
        SectorNeighborDistanceContext context,
        out int movementCost,
        List<Vector3Int> path)
    {
        searchDebugCalls++;
        double searchStartMs = Time.realtimeSinceStartupAsDouble;
        try
        {
            // Incondicional: os caches de terreno por celula sao consultados
            // mesmo por chamadas nao-cacheaveis (as que pedem rota), entao a
            // invalidacao por fingerprint precisa acontecer sempre.
            EnsureLandDistanceCacheFresh();

            // Quem pede o caminho reconstruido nao pode ser servido pelo cache
            // de custo: ele guarda custo, nao rota. Passa direto para a busca.
            bool cacheable = path == null && context.IsValid;
            LandDistanceCacheKey cacheKey = default;
            if (cacheable)
            {
                Vector3Int cacheFrom = from;
                Vector3Int cacheTo = to;
                cacheFrom.z = 0;
                cacheTo.z = 0;
                cacheKey = new LandDistanceCacheKey(
                    cacheFrom,
                    cacheTo,
                    BuildLandDistanceContextId(context));
                if (landDistanceCache.TryGetValue(cacheKey, out int cachedCost))
                {
                    searchDebugHits++;
                    if (cachedCost == LandDistanceUnreachable)
                    {
                        movementCost = 0;
                        searchDebugFailures++;
                        return false;
                    }

                    movementCost = cachedCost;
                    return true;
                }
            }

            bool found = TryComputeLandMovementDistanceInternal(
                from,
                to,
                context,
                out movementCost,
                path);
            if (!found)
                searchDebugFailures++;
            if (cacheable)
            {
                landDistanceCache[cacheKey] = found
                    ? movementCost
                    : LandDistanceUnreachable;
            }
            return found;
        }
        finally
        {
            searchDebugMs +=
                (Time.realtimeSinceStartupAsDouble - searchStartMs) * 1000d;
        }
    }

    // Entrada da fronteira do Dijkstra setorial. A ordem de insercao e chave
    // secundaria para o desempate ficar identico ao da varredura linear antiga.
    private readonly struct SectorSearchEntry
    {
        public readonly int Cost;
        public readonly int Sequence;
        public readonly Vector3Int Cell;

        public SectorSearchEntry(int cost, int sequence, Vector3Int cell)
        {
            Cost = cost;
            Sequence = sequence;
            Cell = cell;
        }

        public bool IsBetterThan(in SectorSearchEntry other)
        {
            if (Cost != other.Cost)
                return Cost < other.Cost;
            return Sequence < other.Sequence;
        }
    }

    private static void PushSectorSearchEntry(
        List<SectorSearchEntry> heap, SectorSearchEntry entry)
    {
        heap.Add(entry);
        int child = heap.Count - 1;
        while (child > 0)
        {
            int parent = (child - 1) / 2;
            if (!heap[child].IsBetterThan(heap[parent]))
                break;
            (heap[parent], heap[child]) = (heap[child], heap[parent]);
            child = parent;
        }
    }

    private static SectorSearchEntry PopSectorSearchEntry(
        List<SectorSearchEntry> heap)
    {
        SectorSearchEntry top = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);

        int parent = 0;
        while (true)
        {
            int left = parent * 2 + 1;
            if (left >= heap.Count)
                break;

            int best = left;
            int right = left + 1;
            if (right < heap.Count && heap[right].IsBetterThan(heap[left]))
                best = right;
            if (!heap[best].IsBetterThan(heap[parent]))
                break;

            (heap[parent], heap[best]) = (heap[best], heap[parent]);
            parent = best;
        }

        return top;
    }

    private static bool TryComputeLandMovementDistanceInternal(
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

        // Se o destino nao e sequer transponivel por esta unidade (ex.: alvo em TERRA para um
        // navio), nenhuma rota o alcanca. Falha JA, em vez de varrer o tabuleiro inteiro ate
        // estourar o teto de expansao para so entao devolver false. Resultado identico (false),
        // instantaneo. Era o custo real do two-turn naval: centenas de buscas condenadas a
        // ~10ms cada, todas terminando em HexDistance. 'from' nao precisa desta checagem — a
        // busca comeca nele (custo 0) e so exige transito nos vizinhos.
        if (!HasAnyPaintedTileAtCell(to, context))
            return false;

        // Fronteira em heap binario com chave (custo, ordem de insercao).
        //
        // A versao anterior usava List e pagava DOIS laços O(n) dentro do laço
        // principal: varredura linear para achar o minimo a cada pop, e
        // frontier.Contains por vizinho. Numa busca que expande centenas de
        // celulas isso vira quadratico, e o rebuild frio do mapa cobrava
        // segundos por causa disso. maxExpanded nao ajudava: ele limita quantas
        // celulas sao expandidas, nao o custo de cada expansao.
        //
        // A ordem de insercao como chave secundaria reproduz o desempate
        // antigo — a List ficava em ordem de insercao e a varredura guardava o
        // PRIMEIRO minimo —, entao custo e rota saem identicos.
        // Uma vez por busca: o id percorre todas as construcoes rastreadas e e
        // constante enquanto a busca roda.
        int contextId = BuildLandDistanceContextId(context);

        var frontier = new List<SectorSearchEntry>();
        var costByCell = new Dictionary<Vector3Int, int> { [from] = 0 };
        var cameFrom = new Dictionary<Vector3Int, Vector3Int> { [from] = from };
        var neighbors = new List<Vector3Int>(6);
        int expanded = 0;
        int sequence = 0;
        int maxExpanded = Mathf.Max(512, context.Tilemap.cellBounds.size.x * context.Tilemap.cellBounds.size.y);
        PushSectorSearchEntry(frontier, new SectorSearchEntry(0, sequence++, from));

        while (frontier.Count > 0 && expanded < maxExpanded)
        {
            SectorSearchEntry entry = PopSectorSearchEntry(frontier);
            Vector3Int current = entry.Cell;
            int bestCost = entry.Cost;

            // Entrada obsoleta: a celula ja foi alcancada mais barato depois
            // que esta foi empilhada. Descarte preguicoso, sem remocao no meio
            // do heap.
            if (costByCell.TryGetValue(current, out int recordedCost)
                && bestCost > recordedCost)
            {
                continue;
            }

            expanded++;

            if (current == to)
            {
                movementCost = bestCost;
                BuildSectorPath(from, to, cameFrom, path);
                searchDebugExpanded += expanded;
                return true;
            }

            long neighborStart = System.Diagnostics.Stopwatch.GetTimestamp();
            UnitMovementPathRules.GetImmediateHexNeighbors(context.Tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                searchDebugTransitionCalls++;
                if (!TryGetLandTransitionCost(
                        current,
                        next,
                        context,
                        contextId,
                        out int enterCost))
                    continue;

                int nextCost = bestCost + enterCost;
                if (costByCell.TryGetValue(next, out int knownCost) && knownCost <= nextCost)
                    continue;

                costByCell[next] = nextCost;
                cameFrom[next] = current;
                PushSectorSearchEntry(
                    frontier, new SectorSearchEntry(nextCost, sequence++, next));
            }
            searchDebugNeighborTicks +=
                System.Diagnostics.Stopwatch.GetTimestamp() - neighborStart;
        }

        // Estourar o teto significa que a busca varreu o tabuleiro inteiro para
        // devolver false. Contabilizar separado de uma falha barata.
        searchDebugExpanded += expanded;
        if (expanded >= maxExpanded)
            searchDebugExhausted++;
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

        TerrainTypeData terrain =
            ResolveTerrainAtCell(cell, context);
        if (context.TerrainDatabase != null
            && terrain == null)
        {
            return false;
        }

        ConstructionManager construction = null;
        if (context.ConstructionsByCell != null)
            context.ConstructionsByCell.TryGetValue(cell, out construction);

        if (construction != null)
        {
            if (context.ReferenceUnitData != null)
            {
                return TryGetUnitDataEnterCost(
                    context.ReferenceUnitData,
                    construction,
                    null,
                    terrain,
                    out cost);
            }

            if (construction.InheritsTerrainRulesOn(terrain))
            {
                if (terrain == null
                    || !SupportsLayerMode(
                        terrain.domain,
                        terrain.heightLevel,
                        terrain.aditionalDomainsAllowed,
                        Domain.Land,
                        HeightLevel.Surface))
                {
                    return false;
                }

                cost = Mathf.Max(1, terrain.basicAutonomyCost);
                return true;
            }

            if (!construction.SupportsLayerMode(
                    Domain.Land,
                    HeightLevel.Surface))
            {
                return false;
            }

            cost = Mathf.Max(1, construction.GetBaseMovementCost());
            return true;
        }

        StructureData structure = ResolveStructureAtCell(cell, context);

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

    private static bool TryGetLandTransitionCost(
        Vector3Int from,
        Vector3Int to,
        SectorNeighborDistanceContext context,
        int contextId,
        out int cost)
    {
        cost = 1;
        from.z = 0;
        to.z = 0;

        if (context.ReferenceUnitData == null)
            return TryGetLandEnterCost(to, context, out cost);
        if (!HasAnyPaintedTileAtCell(to, context))
            return false;

        TerrainTypeData terrain = ResolveTerrainAtCell(to, context);
        if (context.TerrainDatabase != null
            && terrain == null)
        {
            return false;
        }

        ConstructionManager construction = null;
        if (context.ConstructionsByCell != null)
        {
            context.ConstructionsByCell.TryGetValue(
                to,
                out construction);
        }

        bool hasConnectedRoute =
            TryGetConnectedRouteEnterCostForUnitData(
                from,
                to,
                context,
                contextId,
                terrain,
                out int connectedRouteCost,
                out bool hasDeclaredRouteEdge);

        // A construcao e o canal mais especifico do hex.
        if (construction != null)
        {
            if (construction.InheritsStructureRulesOn(terrain)
                && hasDeclaredRouteEdge)
            {
                // A estrutura conectada assume completamente. Uma aresta que
                // existe mas recusa a unidade nao pode cair nas regras mais
                // permissivas da construcao.
                if (!hasConnectedRoute)
                    return false;
                cost = connectedRouteCost;
                return true;
            }

            if (construction.InheritsTerrainRulesOn(terrain))
            {
                return TryGetUnitDataTerrainEnterCost(
                    context.ReferenceUnitData,
                    terrain,
                    out cost);
            }

            return TryGetUnitDataEnterCost(
                context.ReferenceUnitData,
                construction,
                null,
                terrain,
                out cost);
        }

        // Entre duas celulas, uma estrutura de rota so participa se declarar
        // exatamente esta aresta. Estruturas sobrepostas competem apenas entre
        // os canais que aceitam a unidade.
        if (hasConnectedRoute)
        {
            cost = connectedRouteCost;
            return true;
        }

        StructureData dominantStructure =
            ResolveStructureAtCell(to, context);
        if (dominantStructure != null)
        {
            if (!TryGetUnitDataStructureEnterCost(
                context.ReferenceUnitData,
                dominantStructure,
                terrain,
                out cost,
                out bool terrainPassage))
            {
                return false;
            }

            if (terrainPassage)
                return true;

            if (dominantStructure.routeNetworkType
                != RouteNetworkType.None)
            {
                // Fora de uma aresta declarada, a infraestrutura continua
                // aplicando suas regras/custo, mas o terreno tambem precisa
                // aceitar a unidade. Isso impede o trem de deslizar e permite
                // o cruzamento configurado por Estrutura+Terreno.
                if (!TryGetUnitDataEnterCost(
                        context.ReferenceUnitData,
                        null,
                        null,
                        terrain,
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        return TryGetUnitDataEnterCost(
            context.ReferenceUnitData,
            null,
            null,
            terrain,
            out cost);
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
            if (construction.InheritsTerrainRulesOn(terrain))
            {
                return TryGetUnitDataTerrainEnterCost(
                    unitData,
                    terrain,
                    out cost);
            }

            if (!ConstructionSupportsUnitData(
                    construction,
                    unitData,
                    terrain))
                return false;

            cost = GetCostWithUnitDataSkillOverrides(
                construction.GetBaseMovementCost(),
                construction.GetSkillCostOverrides(terrain),
                unitData);
            cost = Mathf.Max(1, cost);
            return true;
        }

        if (structure != null)
        {
            return TryGetUnitDataStructureEnterCost(
                unitData,
                structure,
                terrain,
                out cost,
                out _);
        }

        return TryGetUnitDataTerrainEnterCost(
            unitData,
            terrain,
            out cost);
    }

    private static bool ConstructionSupportsUnitData(
        ConstructionManager construction,
        UnitData unitData,
        TerrainTypeData terrain)
    {
        if (construction == null || unitData == null)
            return false;

        if (!construction.SupportsLayerMode(unitData.domain, unitData.heightLevel))
            return false;

        return UnitDataPassesSkillRules(
            unitData,
            construction.GetRequiredSkillsToEnter(terrain),
            construction.GetBlockedSkillsToEnter(terrain));
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

    private static bool TryGetConnectedRouteEnterCostForUnitData(
        Vector3Int from,
        Vector3Int to,
        SectorNeighborDistanceContext context,
        int contextId,
        TerrainTypeData destinationTerrain,
        out int cost,
        out bool hasDeclaredRouteEdge)
    {
        cost = 1;
        hasDeclaredRouteEdge = false;
        if (context.ReferenceUnitData == null)
            return false;

        searchDebugRouteCalls++;

        // A chave inclui o contexto porque o custo depende da UnitData de
        // referencia. destinationTerrain e derivado de `to`, entao ja esta
        // coberto pela celula.
        // O id do contexto vem PRONTO de cima. Antes era recalculado aqui, em
        // cada uma das ~1,9 milhao de chamadas, so para montar a chave — e ele
        // percorre todas as construcoes rastreadas. E constante durante uma
        // busca inteira, entao basta calcular uma vez por busca.
        var routeKey = new LandDistanceCacheKey(from, to, contextId);
        if (routeEnterCostCache.TryGetValue(routeKey, out RouteEnterCostResult cachedRoute))
        {
            searchDebugRouteCacheHits++;
            cost = cachedRoute.Cost;
            hasDeclaredRouteEdge = cachedRoute.HasDeclaredRouteEdge;
            return cachedRoute.Found;
        }

        bool routeFound = TryGetConnectedRouteEnterCostForUnitDataUncached(
            from, to, context, destinationTerrain, out cost, out hasDeclaredRouteEdge);
        routeEnterCostCache[routeKey] =
            new RouteEnterCostResult(routeFound, cost, hasDeclaredRouteEdge);
        return routeFound;
    }

    private static bool TryGetConnectedRouteEnterCostForUnitDataUncached(
        Vector3Int from,
        Vector3Int to,
        SectorNeighborDistanceContext context,
        TerrainTypeData destinationTerrain,
        out int cost,
        out bool hasDeclaredRouteEdge)
    {
        cost = 1;
        hasDeclaredRouteEdge = false;
        if (context.ReferenceUnitData == null)
            return false;

        StructureData bestStructure = null;
        int bestStructureCost = 1;
        if (context.Topology != null
            && context.Topology.TryGetRouteStructures(
                from,
                to,
                out IReadOnlyList<StructureData> indexedStructures))
        {
            searchDebugRouteTopologyHits++;
            for (int i = 0; i < indexedStructures.Count; i++)
            {
                StructureData structure = indexedStructures[i];
                if (structure != null)
                    hasDeclaredRouteEdge = true;
                ConsiderRouteStructureForUnitData(
                    structure,
                    destinationTerrain,
                    context.ReferenceUnitData,
                    ref bestStructure,
                    ref bestStructureCost);
            }

            if (bestStructure != null)
            {
                cost = bestStructureCost;
                return true;
            }

            return false;
        }

        if (context.RoadNetworks == null)
            return false;

        searchDebugRouteNetworkScans++;
        for (int i = 0; i < context.RoadNetworks.Length; i++)
        {
            RoadNetworkManager network = context.RoadNetworks[i];
            if (network == null)
                continue;

            Tilemap networkMap = network.BoardTilemap;
            if (context.Tilemap != null
                && networkMap != null
                && networkMap != context.Tilemap
                && networkMap.layoutGrid
                    != context.Tilemap.layoutGrid)
            {
                continue;
            }

            StructureDatabase database = network.StructureDatabase;
            IReadOnlyList<StructureData> structures =
                database != null ? database.Structures : null;
            if (structures == null)
                continue;

            for (int s = 0; s < structures.Count; s++)
            {
                StructureData structure = structures[s];
                if (structure == null)
                    continue;

                // Rota deste tabuleiro vem da cena, nao do catalogo.
                IReadOnlyList<RoadRouteDefinition> routes =
                    network.GetRoadRoutes(structure);
                if (routes == null)
                    continue;

                bool containsEdge = false;
                for (int r = 0; r < routes.Count; r++)
                {
                    RoadRouteDefinition route = routes[r];
                    if (route == null
                        || route.cells == null
                        || route.cells.Count < 2)
                    {
                        continue;
                    }

                    for (int c = 1; c < route.cells.Count; c++)
                    {
                        Vector3Int a = route.cells[c - 1];
                        Vector3Int b = route.cells[c];
                        a.z = 0;
                        b.z = 0;
                        if ((a == from && b == to)
                            || (a == to && b == from))
                        {
                            containsEdge = true;
                            break;
                        }
                    }

                    if (containsEdge)
                        break;
                }

                if (containsEdge)
                {
                    hasDeclaredRouteEdge = true;
                    ConsiderRouteStructureForUnitData(
                        structure,
                        destinationTerrain,
                        context.ReferenceUnitData,
                        ref bestStructure,
                        ref bestStructureCost);
                }
            }
        }

        if (bestStructure == null)
            return false;

        cost = bestStructureCost;
        return true;
    }

    private static bool TryGetUnitDataStructureEnterCost(
        UnitData unitData,
        StructureData structure,
        TerrainTypeData terrain,
        out int cost,
        out bool terrainPassage)
    {
        cost = 1;
        terrainPassage = false;
        if (unitData == null || structure == null)
            return false;

        if (structure.domain == unitData.domain
            && structure.heightLevel == unitData.heightLevel)
        {
            return TryGetUnitDataNativeStructureCost(
                unitData,
                structure,
                terrain,
                out cost);
        }

        if (HasLayerMode(
                structure.aditionalDomainsAllowed,
                unitData.domain,
                unitData.heightLevel)
            && !structure.IsLayerBlockedAt(
                terrain,
                unitData.domain,
                unitData.heightLevel)
            && TryGetUnitDataTerrainEnterCostForMode(
                unitData,
                terrain,
                unitData.domain,
                unitData.heightLevel,
                out cost))
        {
            terrainPassage = true;
            return true;
        }

        if (unitData.aditionalDomainsAllowed != null)
        {
            for (int i = 0;
                 i < unitData.aditionalDomainsAllowed.Count;
                 i++)
            {
                UnitLayerMode unitMode =
                    unitData.aditionalDomainsAllowed[i];
                if (structure.domain == unitMode.domain
                    && structure.heightLevel
                        == unitMode.heightLevel)
                {
                    return TryGetUnitDataNativeStructureCost(
                        unitData,
                        structure,
                        terrain,
                        out cost);
                }
            }

            for (int i = 0;
                 i < unitData.aditionalDomainsAllowed.Count;
                 i++)
            {
                UnitLayerMode unitMode =
                    unitData.aditionalDomainsAllowed[i];
                if (!HasLayerMode(
                        structure.aditionalDomainsAllowed,
                        unitMode.domain,
                        unitMode.heightLevel)
                    || structure.IsLayerBlockedAt(
                        terrain,
                        unitMode.domain,
                        unitMode.heightLevel)
                    || !TryGetUnitDataTerrainEnterCostForMode(
                        unitData,
                        terrain,
                        unitMode.domain,
                        unitMode.heightLevel,
                        out cost))
                {
                    continue;
                }

                terrainPassage = true;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUnitDataNativeStructureCost(
        UnitData unitData,
        StructureData structure,
        TerrainTypeData terrain,
        out int cost)
    {
        cost = 1;
        if (unitData == null || structure == null)
            return false;
        if (structure.IsLayerBlockedAt(
                terrain,
                structure.domain,
                structure.heightLevel))
        {
            return false;
        }
        if (!UnitDataPassesSkillRules(
                unitData,
                structure.GetRequiredSkillsToEnter(terrain),
                structure.GetBlockedSkillsToEnter(terrain)))
        {
            return false;
        }

        cost = GetCostWithUnitDataSkillOverrides(
            structure.baseMovementCost,
            structure.GetSkillCostOverrides(terrain),
            unitData);
        cost = Mathf.Max(1, cost);
        return true;
    }

    private static bool TryGetUnitDataTerrainEnterCost(
        UnitData unitData,
        TerrainTypeData terrain,
        out int cost)
    {
        cost = 1;
        if (unitData == null || terrain == null)
            return false;

        if (TryGetUnitDataTerrainEnterCostForMode(
                unitData,
                terrain,
                unitData.domain,
                unitData.heightLevel,
                out cost))
        {
            return true;
        }

        if (unitData.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0;
             i < unitData.aditionalDomainsAllowed.Count;
             i++)
        {
            UnitLayerMode mode =
                unitData.aditionalDomainsAllowed[i];
            if (TryGetUnitDataTerrainEnterCostForMode(
                    unitData,
                    terrain,
                    mode.domain,
                    mode.heightLevel,
                    out cost))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUnitDataTerrainEnterCostForMode(
        UnitData unitData,
        TerrainTypeData terrain,
        Domain domain,
        HeightLevel height,
        out int cost)
    {
        cost = 1;
        if (unitData == null || terrain == null)
            return false;
        if (!SupportsLayerMode(
                terrain.domain,
                terrain.heightLevel,
                terrain.aditionalDomainsAllowed,
                domain,
                height))
        {
            return false;
        }
        if (!UnitDataPassesSkillRules(
                unitData,
                terrain.requiredSkillsToEnter,
                terrain.blockedSkills))
        {
            return false;
        }

        cost = GetCostWithUnitDataSkillOverrides(
            terrain.basicAutonomyCost,
            terrain.skillCostOverrides,
            unitData);
        cost = Mathf.Max(1, cost);
        return true;
    }

    private static void ConsiderRouteStructureForUnitData(
        StructureData candidate,
        TerrainTypeData destinationTerrain,
        UnitData unitData,
        ref StructureData bestStructure,
        ref int bestCost)
    {
        if (candidate == null
            || !TryGetUnitDataEnterCost(
                unitData,
                null,
                candidate,
                destinationTerrain,
                out int candidateCost))
        {
            return;
        }

        bool isBetter = bestStructure == null
            || candidate.priorityOrder > bestStructure.priorityOrder
            || (candidate.priorityOrder == bestStructure.priorityOrder
                && string.CompareOrdinal(
                    candidate.id ?? string.Empty,
                    bestStructure.id ?? string.Empty) < 0);
        if (!isBetter)
            return;

        bestStructure = candidate;
        bestCost = candidateCost;
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

        cell.z = 0;
        var cacheKey = new CellContextCacheKey(
            cell,
            BuildMapContextId(context));
        if (terrainByCellCache.TryGetValue(
                cacheKey,
                out TerrainTypeData cached))
        {
            searchDebugTerrainCacheHits++;
            return cached;
        }
        searchDebugTerrainResolves++;
        TerrainTypeData resolved = ResolveTerrainAtCellUncached(cell, context);
        terrainByCellCache[cacheKey] = resolved;
        return resolved;
    }

    private static TerrainTypeData ResolveTerrainAtCellUncached(Vector3Int cell, SectorNeighborDistanceContext context)
    {

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
        cell.z = 0;
        var cacheKey = new CellContextCacheKey(
            cell,
            BuildMapContextId(context));
        if (paintedByCellCache.TryGetValue(cacheKey, out bool cached))
        {
            searchDebugPaintedCacheHits++;
            return cached;
        }
        searchDebugPaintedResolves++;
        bool painted = HasAnyPaintedTileAtCellUncached(cell, context);
        paintedByCellCache[cacheKey] = painted;
        return painted;
    }

    private static bool HasAnyPaintedTileAtCellUncached(Vector3Int cell, SectorNeighborDistanceContext context)
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

    private static bool HasLayerMode(
        IReadOnlyList<TerrainLayerMode> modes,
        Domain targetDomain,
        HeightLevel targetHeight)
    {
        if (modes == null)
            return false;

        for (int i = 0; i < modes.Count; i++)
        {
            TerrainLayerMode mode = modes[i];
            if (mode.domain == targetDomain
                && mode.heightLevel == targetHeight)
            {
                return true;
            }
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
        public BoardTopologyIndex Topology;
        public Dictionary<Vector3Int, ConstructionManager> ConstructionsByCell;
        public UnitData ReferenceUnitData;
    }

    private IReadOnlyList<ConstructionManager> GetTrackedConstructions()
    {
        IReadOnlyList<ConstructionManager> all =
            GetAllTrackedConstructions();
        var local = new List<ConstructionManager>();
        for (int i = 0; i < all.Count; i++)
        {
            ConstructionManager construction = all[i];
            if (construction != null
                && construction.gameObject.scene == gameObject.scene)
            {
                local.Add(construction);
            }
        }
        return local;
    }

    private static IReadOnlyList<ConstructionManager>
        GetAllTrackedConstructions()
    {
        if (ConstructionManager.AllActive != null && ConstructionManager.AllActive.Count > 0)
            return ConstructionManager.AllActive;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ConstructionManager[] editorConstructions =
                Object.FindObjectsByType<ConstructionManager>(
                    FindObjectsInactive.Include);
            return editorConstructions ?? System.Array.Empty<ConstructionManager>();
        }
#endif

        ConstructionManager[] runtimeConstructions =
            Object.FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Exclude);
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
