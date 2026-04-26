using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [SerializeField] private int currentCapturePoints;
        [SerializeField] private int capturePointsMax;
        [SerializeField] private ConstructionManager source;

        public int InstanceId => instanceId;
        public string DisplayName => displayName;
        public Vector3Int Cell => cell;
        public TeamId OwnerTeam => ownerTeam;
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
            currentCapturePoints = construction != null ? construction.CurrentCapturePoints : 0;
            capturePointsMax = construction != null ? construction.CapturePointsMax : 0;
        }
    }

    [System.Serializable]
    public struct SectorHQDistance
    {
        [SerializeField] public TeamId Team;
        [SerializeField] public float  Distance;
    }

    [System.Serializable]
    public struct SectorRiskEntry
    {
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
        [SerializeField] private string statusText;
        [SerializeField] private List<SectorConstructionInfo> constructions = new List<SectorConstructionInfo>();
        [SerializeField] private List<SectorHQDistance> hqDistances = new List<SectorHQDistance>();
        [SerializeField] private List<SectorRiskEntry>  riskEntries  = new List<SectorRiskEntry>();

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
        public string StatusText => statusText;
        public IReadOnlyList<SectorConstructionInfo> Constructions => constructions;
        public IReadOnlyList<SectorHQDistance> HQDistances => hqDistances;
        public IReadOnlyList<SectorRiskEntry>  RiskEntries  => riskEntries;

        public float GetDistanceToHQ(TeamId team)
        {
            for (int i = 0; i < hqDistances.Count; i++)
                if (hqDistances[i].Team == team) return hqDistances[i].Distance;
            return float.MaxValue;
        }

        public TeamId NearestTeam()
        {
            TeamId best = TeamId.Neutral;
            float  min  = float.MaxValue;
            for (int i = 0; i < hqDistances.Count; i++)
            {
                if (hqDistances[i].Distance < min)
                {
                    min  = hqDistances[i].Distance;
                    best = hqDistances[i].Team;
                }
            }
            return best;
        }

        /// <summary>
        /// Risco relativo do setor para o time informado: 0 = seguro, 1 = deep raid.
        /// Fórmula: minhaDist / (minhaDist + menorDistInimiga)
        /// </summary>
        public float GetRiskRatioFor(TeamId team)
        {
            float myDist      = GetDistanceToHQ(team);
            float enemyMinDist = float.MaxValue;

            for (int i = 0; i < hqDistances.Count; i++)
            {
                if (hqDistances[i].Team == team) continue;
                if (hqDistances[i].Distance < enemyMinDist)
                    enemyMinDist = hqDistances[i].Distance;
            }

            if (myDist == float.MaxValue)   return 0.5f;
            if (enemyMinDist == float.MaxValue) return 0f;

            float total = myDist + enemyMinDist;
            return total < 0.01f ? 0.5f : myDist / total;
        }

        /// <summary>Classificação categórica do risco para uso em decisões da IA.</summary>
        public SectorRiskLevel GetRiskLevelFor(TeamId team)
        {
            float r = GetRiskRatioFor(team);
            if (r < 0.25f) return SectorRiskLevel.Safe;
            if (r < 0.40f) return SectorRiskLevel.Low;
            if (r < 0.60f) return SectorRiskLevel.Medium;
            if (r < 0.75f) return SectorRiskLevel.High;
            return SectorRiskLevel.DeepRaid;
        }

        internal void ApplyHQDistances(List<SectorHQDistance> distances)
        {
            hqDistances.Clear();
            riskEntries.Clear();
            if (distances == null) return;

            hqDistances.AddRange(distances);

            for (int i = 0; i < distances.Count; i++)
            {
                TeamId team  = distances[i].Team;
                float  ratio = GetRiskRatioFor(team);
                riskEntries.Add(new SectorRiskEntry
                {
                    Team      = team,
                    RiskRatio = Mathf.Round(ratio * 100f) / 100f,
                    RiskLevel = GetRiskLevelFor(team),
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
            statusText = valueStatusText ?? string.Empty;

            constructions.Clear();
            if (valueConstructions != null)
                constructions.AddRange(valueConstructions);
        }
    }

    private static SectorManager instance;

    [SerializeField] private bool sectorLog;
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

    private void RebuildFromActiveConstructions(string reason)
    {
        sectorInfos.Clear();
        sectorInfoBySector.Clear();
        baseInfos.Clear();
        baseInfoBySector.Clear();

        // Coleta HQs de cada time antes de processar setores
        var hqByTeam = new Dictionary<TeamId, Vector3Int>();
        IReadOnlyList<ConstructionManager> allConstructions = GetTrackedConstructions();
        for (int i = 0; i < allConstructions.Count; i++)
        {
            ConstructionManager c = allConstructions[i];
            if (c == null || !c.IsPlayerHeadQuarter) continue;
            if (!hqByTeam.ContainsKey(c.TeamId))
                hqByTeam[c.TeamId] = c.CurrentCellPosition;
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

            for (int c = 0; c < constructionsInSector.Count; c++)
            {
                ConstructionManager construction = constructionsInSector[c];
                entries.Add(new SectorConstructionInfo(construction));
                totalCaptureMax += Mathf.Max(0, construction.CapturePointsMax);
                totalCurrentCapture += Mathf.Max(0, construction.CurrentCapturePoints);

                if (construction.CurrentCapturePoints < construction.CapturePointsMax)
                    hasPartialCapture = true;
                if (construction.TeamId != firstOwner)
                    hasMixedOwners = true;
            }

            bool isFullyControlled = !hasMixedOwners && !hasPartialCapture && constructionsInSector.Count > 0;
            bool isDisputed = hasMixedOwners || hasPartialCapture;
            TeamId controllingTeam = hasMixedOwners || constructionsInSector.Count == 0 ? TeamId.Neutral : firstOwner;
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
                statusText,
                entries);

            // Distâncias de cada HQ ao centroide do setor (em coordenadas de célula)
            var distances = new List<SectorHQDistance>(hqByTeam.Count);
            Vector2 sectorCenter = new Vector2(representativeCell.x, representativeCell.y);
            foreach (KeyValuePair<TeamId, Vector3Int> kv in hqByTeam)
            {
                Vector2 hqCenter = new Vector2(kv.Value.x, kv.Value.y);
                distances.Add(new SectorHQDistance
                {
                    Team     = kv.Key,
                    Distance = Vector2.Distance(sectorCenter, hqCenter),
                });
            }
            info.ApplyHQDistances(distances);

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

        if (sectorLog)
            Debug.Log($"[SectorManager] rebuild reason={reason ?? "none"} sectors={sectorInfos.Count} bases={baseInfos.Count} constructions={constructions.Count}");
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
