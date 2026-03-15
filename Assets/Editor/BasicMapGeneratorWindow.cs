using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class BasicMapGeneratorWindow : EditorWindow
{
    private const int ArchipelagoPresetSize = 60;

    private readonly struct ZonePreset
    {
        public readonly string label;
        public readonly int xMin;
        public readonly int xMax;
        public readonly int yMin;
        public readonly int yMax;
        public readonly string description;
        public readonly float landRatioBase;

        public ZonePreset(string label, int xMin, int xMax, int yMin, int yMax, string description, float landRatioBase)
        {
            this.label = label;
            this.xMin = xMin;
            this.xMax = xMax;
            this.yMin = yMin;
            this.yMax = yMax;
            this.description = description;
            this.landRatioBase = landRatioBase;
        }
    }

    private enum ZoneGeneratorMode
    {
        ConvincingFromDescription = 0,
        WaterOnly = 1
    }

    private enum GeneratorMode
    {
        ConvincingFromDescription = 0,
        WaterOnly = 1
    }

    private enum CellKind
    {
        Water = 0,
        Beach = 1,
        Plains = 2,
        Forest = 3,
        Mountain = 4
    }

    private struct TerrainSet
    {
        public TerrainTypeData water;
        public TerrainTypeData beach;
        public TerrainTypeData plains;
        public TerrainTypeData forest;
        public TerrainTypeData mountain;
    }

    private struct DescriptionProfile
    {
        public float targetLandRatio;
        public float coastlineBias;
        public float mountainBoost;
        public float forestBoost;
        public float beachChance;
    }

    private static readonly Vector2Int[] EvenRowNeighborOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(-1, -1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(0, 1)
    };

    private static readonly Vector2Int[] OddRowNeighborOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(1, -1),
        new Vector2Int(0, 1),
        new Vector2Int(1, 1)
    };

    private static readonly ZonePreset[] ArchipelagoPresets =
    {
        new ZonePreset("Base NO", 0, 19, 0, 19, "floresta com montanha", 0.85f),
        new ZonePreset("Base NE", 40, 59, 0, 19, "floresta com montanha", 0.85f),
        new ZonePreset("Base SO", 0, 19, 40, 59, "floresta com montanha", 0.85f),
        new ZonePreset("Base SE", 40, 59, 40, 59, "floresta com montanha", 0.85f),
        new ZonePreset("Flanco Oeste", 0, 19, 20, 39, "planicie costeira", 0.75f),
        new ZonePreset("Flanco Leste", 40, 59, 20, 39, "planicie costeira", 0.75f),
        new ZonePreset("Canal Central", 20, 39, 0, 59, "agua", 0.05f),
        new ZonePreset("Ilha Central", 25, 34, 25, 34, "planicie costeira", 0.75f)
    };

    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private GeneratorMode mode = GeneratorMode.ConvincingFromDescription;
    [SerializeField] private int width = 60;
    [SerializeField] private int height = 60;
    [SerializeField] private int seed = 1337;
    [SerializeField] private bool randomizeSeed = true;
    [SerializeField] private string description = "arquipelago com praia e montanhas";
    [SerializeField] private int minIslandSize = 6;
    [SerializeField] private bool lockOriginAtZeroTopLeft = true;
    [SerializeField] private Vector2Int originCell = Vector2Int.zero;
    [SerializeField] private ZoneGeneratorMode zoneMode = ZoneGeneratorMode.ConvincingFromDescription;
    [SerializeField] private int zoneXMin = 0;
    [SerializeField] private int zoneXMax = 14;
    [SerializeField] private int zoneYMin = 0;
    [SerializeField] private int zoneYMax = 14;
    [SerializeField] private string zoneDescription = string.Empty;
    [SerializeField] private int zoneSeed = 0;
    [SerializeField] private bool zoneRandomSeed = true;
    [SerializeField] private bool zoneOverrideLandRatio;
    [SerializeField] [Range(0f, 1f)] private float zoneLandRatio = 0.5f;
    [SerializeField] private bool archipelagoSymmetricMode = true;

    private string status = "Configure e clique em Gerar.";
    private Vector2 scroll;

    [MenuItem("Tools/Utils/Map Generator (Basic)")]
    public static void OpenWindow()
    {
        BasicMapGeneratorWindow window = GetWindow<BasicMapGeneratorWindow>("Map Generator (Basic)");
        window.minSize = new Vector2(400f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        AutoDetectContext(force: false);
    }

    private void OnFocus()
    {
        AutoDetectContext(force: false);
    }

    private void OnGUI()
    {
        AutoDetectContext(force: false);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Map Generator (Basic)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Pinta apenas na Tilemap usando TerrainTypeData.\n" +
            "Origem travada: primeiro hex em (0,0), preenchendo para direita e para baixo.",
            MessageType.Info);

        terrainTilemap = (Tilemap)EditorGUILayout.ObjectField("Terrain Tilemap", terrainTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        mode = (GeneratorMode)EditorGUILayout.EnumPopup("Modo", mode);

        width = Mathf.Max(1, EditorGUILayout.IntField("Largura", width));
        height = Mathf.Max(1, EditorGUILayout.IntField("Altura", height));
        minIslandSize = Mathf.Max(1, EditorGUILayout.IntField("Min Island Size", minIslandSize));

        lockOriginAtZeroTopLeft = EditorGUILayout.ToggleLeft("Travar origem em (0,0) no canto superior esquerdo", lockOriginAtZeroTopLeft);
        if (lockOriginAtZeroTopLeft)
        {
            originCell = Vector2Int.zero;
            EditorGUILayout.LabelField("Origem", "(0,0)");
        }
        else
        {
            originCell = EditorGUILayout.Vector2IntField("Origem", originCell);
        }

        if (mode == GeneratorMode.ConvincingFromDescription)
        {
            description = EditorGUILayout.TextField("Descricao", description);
            randomizeSeed = EditorGUILayout.ToggleLeft("Seed aleatoria a cada geracao", randomizeSeed);
            using (new EditorGUI.DisabledScope(randomizeSeed))
            {
                seed = EditorGUILayout.IntField("Seed", seed);
            }
        }

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Detect", GUILayout.Width(120f)))
                AutoDetectContext(force: true);

            if (GUILayout.Button(mode == GeneratorMode.WaterOnly ? "Gerar Apenas Agua" : "Gerar Mapa"))
                Generate();
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(status, MessageType.None);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("--- Modo Zona ---", EditorStyles.boldLabel);
        zoneMode = (ZoneGeneratorMode)EditorGUILayout.EnumPopup("Modo", zoneMode);

        using (new EditorGUILayout.HorizontalScope())
        {
            zoneXMin = EditorGUILayout.IntField("xMin", zoneXMin);
            zoneXMax = EditorGUILayout.IntField("xMax", zoneXMax);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            zoneYMin = EditorGUILayout.IntField("yMin", zoneYMin);
            zoneYMax = EditorGUILayout.IntField("yMax", zoneYMax);
        }

        if (zoneMode == ZoneGeneratorMode.ConvincingFromDescription)
        {
            zoneDescription = EditorGUILayout.TextField("Descricao", zoneDescription);
            zoneOverrideLandRatio = EditorGUILayout.ToggleLeft("Override Target Land Ratio", zoneOverrideLandRatio);
            using (new EditorGUI.DisabledScope(!zoneOverrideLandRatio))
                zoneLandRatio = EditorGUILayout.Slider("Target Land Ratio", zoneLandRatio, 0f, 1f);
        }

        zoneRandomSeed = EditorGUILayout.ToggleLeft("Seed aleatoria", zoneRandomSeed);
        using (new EditorGUI.DisabledScope(zoneRandomSeed))
            zoneSeed = EditorGUILayout.IntField("Seed", zoneSeed);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Gerar Zona"))
                GenerateZone();
            if (GUILayout.Button("Limpar Zona"))
                ClearZone();
        }

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("--- Presets: Arquipelago 60x60 ---", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Base NO"))
                ApplyZonePreset(ArchipelagoPresets[0]);
            if (GUILayout.Button("Base NE"))
                ApplyZonePreset(ArchipelagoPresets[1]);
            if (GUILayout.Button("Base SO"))
                ApplyZonePreset(ArchipelagoPresets[2]);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Base SE"))
                ApplyZonePreset(ArchipelagoPresets[3]);
            if (GUILayout.Button("Flanco Oeste"))
                ApplyZonePreset(ArchipelagoPresets[4]);
            if (GUILayout.Button("Flanco Leste"))
                ApplyZonePreset(ArchipelagoPresets[5]);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Canal Central"))
                ApplyZonePreset(ArchipelagoPresets[6]);
            if (GUILayout.Button("Ilha Central"))
                ApplyZonePreset(ArchipelagoPresets[7]);
        }

        archipelagoSymmetricMode = EditorGUILayout.ToggleLeft("Modo Simetrico (espelha esquerda -> direita)", archipelagoSymmetricMode);
        if (GUILayout.Button("Gerar Arquipelago Completo", GUILayout.Height(28f)))
            GenerateArchipelagoComplete();

        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        if (terrainTilemap == null)
        {
            status = "Falha: selecione uma Tilemap.";
            return;
        }

        if (terrainDatabase == null)
        {
            status = "Falha: selecione um TerrainDatabase.";
            return;
        }

        if (!TryResolveTerrains(terrainDatabase, out TerrainSet set, out string resolveError))
        {
            status = $"Falha: {resolveError}";
            return;
        }

        int effectiveSeed = randomizeSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : seed;
        if (randomizeSeed)
            seed = effectiveSeed;

        int globalOriginX = originCell.x;
        int globalOriginY = -originCell.y;
        CellKind[,] map = BuildMap(
            width,
            height,
            mode,
            description,
            effectiveSeed,
            set,
            minIslandSize,
            globalOriginX,
            globalOriginY);
        PaintMap(terrainTilemap, map, originCell, set);

        status = mode == GeneratorMode.WaterOnly
            ? $"Mapa de agua gerado: {width}x{height} (seed {effectiveSeed})."
            : $"Mapa gerado: {width}x{height} (seed {effectiveSeed}, descricao: \"{description}\").";
    }

    private static CellKind[,] BuildMap(
        int mapWidth,
        int mapHeight,
        GeneratorMode mode,
        string descriptionText,
        int worldSeed,
        TerrainSet set,
        int islandMinSize,
        int globalOriginX,
        int globalOriginY,
        bool useLandRatioOverride = false,
        float landRatioOverride = 0f)
    {
        CellKind[,] result = new CellKind[mapWidth, mapHeight];

        if (mode == GeneratorMode.WaterOnly)
        {
            FillAll(result, CellKind.Water);
            return result;
        }

        DescriptionProfile profile = BuildProfile(descriptionText);
        if (useLandRatioOverride)
            profile.targetLandRatio = Mathf.Clamp01(landRatioOverride);

        if (profile.targetLandRatio <= 0.1f)
        {
            FillAll(result, CellKind.Water);
            return result;
        }

        float[,] landSignal = BuildLandSignal(mapWidth, mapHeight, globalOriginX, globalOriginY, worldSeed, profile);
        bool[,] landMask = BuildLandMask(landSignal, profile.targetLandRatio);
        SmoothLandMask(landMask, 2);
        bool[,] forcedPlainMask = new bool[mapWidth, mapHeight];
        if (profile.targetLandRatio >= 0.2f)
            PostProcessConnectivity(landMask, Mathf.Max(1, islandMinSize), out landMask, out forcedPlainMask);
        bool[,] effectiveLandMask = CombineLandMasks(landMask, forcedPlainMask);

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if (!effectiveLandMask[x, y])
                {
                    result[x, y] = CellKind.Water;
                    continue;
                }

                bool hasWaterNeighbor = HasNeighborWithValue(effectiveLandMask, x, y, expected: false);
                if (hasWaterNeighbor && Random01(x, y, worldSeed, salt: 37) < profile.beachChance)
                {
                    result[x, y] = CellKind.Beach;
                    continue;
                }

                int gx = globalOriginX + x;
                int gy = globalOriginY + y;
                float mountainN = Mathf.PerlinNoise((gx + worldSeed * 0.07f) * 0.12f, (gy + worldSeed * 0.11f) * 0.12f);
                float forestN = Mathf.PerlinNoise((gx + worldSeed * 0.17f) * 0.16f, (gy + worldSeed * 0.03f) * 0.16f);

                float mountainThreshold = Mathf.Clamp01(0.83f - profile.mountainBoost);
                float forestThreshold = Mathf.Clamp01(0.62f - profile.forestBoost);

                if (mountainN >= mountainThreshold && set.mountain != null)
                    result[x, y] = CellKind.Mountain;
                else if (forestN >= forestThreshold && set.forest != null)
                    result[x, y] = CellKind.Forest;
                else
                    result[x, y] = CellKind.Plains;
            }
        }

        // Nao achata montanha/floresta de componentes pequenos para planicie.
        // So garante planicie quando um forced cell, por algum motivo, ainda resultar em agua.
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                if (forcedPlainMask[x, y] && result[x, y] == CellKind.Water)
                    result[x, y] = CellKind.Plains;
            }
        }

        return result;
    }

    private static CellKind[,] BuildMap(
        int mapWidth,
        int mapHeight,
        ZoneGeneratorMode mode,
        string descriptionText,
        int worldSeed,
        TerrainSet set,
        int islandMinSize,
        int globalOriginX,
        int globalOriginY,
        bool useLandRatioOverride = false,
        float landRatioOverride = 0f)
    {
        GeneratorMode globalMode = mode == ZoneGeneratorMode.WaterOnly
            ? GeneratorMode.WaterOnly
            : GeneratorMode.ConvincingFromDescription;
        return BuildMap(
            mapWidth,
            mapHeight,
            globalMode,
            descriptionText,
            worldSeed,
            set,
            islandMinSize,
            globalOriginX,
            globalOriginY,
            useLandRatioOverride,
            landRatioOverride);
    }

    private static void FillAll(CellKind[,] map, CellKind value)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                map[x, y] = value;
        }
    }

    private static float[,] BuildLandSignal(
        int mapWidth,
        int mapHeight,
        int globalOriginX,
        int globalOriginY,
        int seedValue,
        DescriptionProfile profile)
    {
        float[,] signal = new float[mapWidth, mapHeight];
        float sx = Mathf.Abs(seedValue % 104729) * 0.001f;
        float sy = Mathf.Abs(seedValue % 130363) * 0.001f;

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int gx = globalOriginX + x;
                int gy = globalOriginY + y;
                float n1 = Mathf.PerlinNoise((gx + sx) * 0.085f, (gy + sy) * 0.085f);
                float n2 = Mathf.PerlinNoise((gx + sx * 0.5f) * 0.19f, (gy + sy * 0.5f) * 0.19f);
                float baseNoise = n1 * 0.7f + n2 * 0.3f;

                float coastGradient = mapWidth <= 1 ? 0f : 1f - (float)x / (mapWidth - 1);
                float centered = 1f - Vector2.Distance(
                    new Vector2(x / Mathf.Max(1f, mapWidth - 1f), y / Mathf.Max(1f, mapHeight - 1f)),
                    new Vector2(0.5f, 0.5f));
                centered = Mathf.Clamp01(centered);

                signal[x, y] = baseNoise + coastGradient * profile.coastlineBias + centered * 0.12f;
            }
        }

        return signal;
    }

    private static bool[,] BuildLandMask(float[,] signal, float targetLandRatio)
    {
        int width = signal.GetLength(0);
        int height = signal.GetLength(1);
        int cellCount = width * height;
        float[] flat = new float[cellCount];

        int index = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                flat[index++] = signal[x, y];
        }

        Array.Sort(flat);
        int desiredLand = Mathf.Clamp(Mathf.RoundToInt(cellCount * Mathf.Clamp01(targetLandRatio)), 1, cellCount - 1);
        int thresholdIndex = Mathf.Clamp(cellCount - desiredLand, 0, cellCount - 1);
        float threshold = flat[thresholdIndex];

        bool[,] landMask = new bool[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                landMask[x, y] = signal[x, y] >= threshold;
        }

        return landMask;
    }

    private static void SmoothLandMask(bool[,] landMask, int iterations)
    {
        int width = landMask.GetLength(0);
        int height = landMask.GetLength(1);
        bool[,] scratch = new bool[width, height];

        for (int pass = 0; pass < iterations; pass++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int landNeighbors = CountHexNeighbors(landMask, x, y, expected: true);
                    bool isBorder = x == 0 || x == width - 1 || y == 0 || y == height - 1;
                    if (landMask[x, y])
                    {
                        int survivalThreshold = isBorder ? 2 : 3;
                        scratch[x, y] = landNeighbors >= survivalThreshold;
                    }
                    else
                        scratch[x, y] = landNeighbors >= 4;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    landMask[x, y] = scratch[x, y];
            }
        }
    }

    private static bool[,] CombineLandMasks(bool[,] primaryLand, bool[,] forcedPlainLand)
    {
        int width = primaryLand.GetLength(0);
        int height = primaryLand.GetLength(1);
        bool[,] combined = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                combined[x, y] = primaryLand[x, y] || forcedPlainLand[x, y];
        }

        return combined;
    }

    private static void PostProcessConnectivity(bool[,] landMask, int minSize, out bool[,] mainLandMask, out bool[,] forcedPlainMask)
    {
        int width = landMask.GetLength(0);
        int height = landMask.GetLength(1);
        mainLandMask = new bool[width, height];
        forcedPlainMask = new bool[width, height];

        bool[,] visited = new bool[width, height];
        List<List<Vector2Int>> components = new List<List<Vector2Int>>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (visited[x, y] || !landMask[x, y])
                    continue;

                List<Vector2Int> component = new List<Vector2Int>();
                visited[x, y] = true;
                queue.Enqueue(new Vector2Int(x, y));

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    component.Add(current);

                    GetHexNeighborOffsets(current.y, out Vector2Int[] offsets);
                    for (int i = 0; i < offsets.Length; i++)
                    {
                        int nx = current.x + offsets[i].x;
                        int ny = current.y + offsets[i].y;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                            continue;
                        if (visited[nx, ny] || !landMask[nx, ny])
                            continue;

                        visited[nx, ny] = true;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }

                components.Add(component);
            }
        }

        if (components.Count == 0)
            return;

        int largestIndex = 0;
        int largestCount = components[0].Count;
        for (int i = 1; i < components.Count; i++)
        {
            int count = components[i].Count;
            if (count > largestCount)
            {
                largestCount = count;
                largestIndex = i;
            }
        }

        int threshold = Mathf.Max(1, minSize);
        for (int i = 0; i < components.Count; i++)
        {
            List<Vector2Int> component = components[i];
            bool isLargest = i == largestIndex;
            bool forcePlains = !isLargest && component.Count < threshold;

            for (int c = 0; c < component.Count; c++)
            {
                Vector2Int cell = component[c];
                if (isLargest)
                    mainLandMask[cell.x, cell.y] = true;
                else if (forcePlains)
                    forcedPlainMask[cell.x, cell.y] = true;
            }
        }
    }

    private static bool HasNeighborWithValue(bool[,] grid, int x, int y, bool expected)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        GetHexNeighborOffsets(y, out Vector2Int[] offsets);

        for (int i = 0; i < offsets.Length; i++)
        {
            int nx = x + offsets[i].x;
            int ny = y + offsets[i].y;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                continue;
            if (grid[nx, ny] == expected)
                return true;
        }

        return false;
    }

    private static int CountHexNeighbors(bool[,] grid, int x, int y, bool expected)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        int count = 0;
        GetHexNeighborOffsets(y, out Vector2Int[] offsets);

        for (int i = 0; i < offsets.Length; i++)
        {
            int nx = x + offsets[i].x;
            int ny = y + offsets[i].y;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                continue;
            if (grid[nx, ny] == expected)
                count++;
        }

        return count;
    }

    private static void GetHexNeighborOffsets(int row, out Vector2Int[] offsets)
    {
        if ((row & 1) == 0)
        {
            offsets = EvenRowNeighborOffsets;
        }
        else
        {
            offsets = OddRowNeighborOffsets;
        }
    }

    private static float Random01(int x, int y, int seedValue, int salt)
    {
        unchecked
        {
            int h = x * 374761393;
            h = (h << 5) - h + y * 668265263;
            h = (h << 5) - h + seedValue * 69069;
            h = (h << 5) - h + salt * 362437;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            uint u = (uint)h;
            return (u & 0x00FFFFFF) / 16777215f;
        }
    }

    private static DescriptionProfile BuildProfile(string descriptionText)
    {
        string txt = NormalizeForMatch(descriptionText);
        DescriptionProfile profile = new DescriptionProfile
        {
            targetLandRatio = 0.36f,
            coastlineBias = 0.20f,
            mountainBoost = 0f,
            forestBoost = 0f,
            beachChance = 0.65f
        };

        if (ContainsAny(txt, "arquipelago", "ilhas", "islands", "island"))
        {
            profile.targetLandRatio = 0.28f;
            profile.coastlineBias = 0.05f;
            profile.beachChance = 0.85f;
        }

        if (ContainsAny(txt, "continente", "coast", "costa", "litoral"))
        {
            profile.targetLandRatio = Mathf.Max(profile.targetLandRatio, 0.40f);
            profile.coastlineBias = Mathf.Max(profile.coastlineBias, 0.32f);
        }

        if (ContainsAny(txt, "montanha", "montanhas", "mountain", "mountains"))
        {
            profile.targetLandRatio = Mathf.Max(profile.targetLandRatio, 0.85f);
            profile.mountainBoost += 0.20f;
        }

        if (ContainsAny(txt, "floresta", "florestas", "forest", "jungle", "selva"))
        {
            profile.targetLandRatio = Mathf.Max(profile.targetLandRatio, 0.85f);
            profile.forestBoost += 0.12f;
        }

        if (ContainsAny(txt, "praia", "praias", "beach", "beaches"))
            profile.beachChance = Mathf.Clamp01(profile.beachChance + 0.10f);

        return profile;
    }

    private static string NormalizeForMatch(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(normalized.Length);

        for (int i = 0; i < normalized.Length; i++)
        {
            char c = normalized[i];
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString();
    }

    private static bool ContainsAny(string text, params string[] candidates)
    {
        if (string.IsNullOrEmpty(text) || candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrEmpty(candidate))
                continue;
            if (text.Contains(candidate))
                return true;
        }

        return false;
    }

    private static void PaintMap(Tilemap tilemap, CellKind[,] map, Vector2Int origin, TerrainSet set)
    {
        int mapWidth = map.GetLength(0);
        int mapHeight = map.GetLength(1);

        Undo.RegisterCompleteObjectUndo(tilemap, "Generate Basic Map");

        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                Vector3Int cell = new Vector3Int(origin.x + x, origin.y - y, 0);
                TileBase tile = ResolveTileForKind(map[x, y], set);
                tilemap.SetTile(cell, tile);
            }
        }

        EditorUtility.SetDirty(tilemap);
        if (tilemap.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
    }

    private static void FillRectWithTile(Tilemap tilemap, int xMin, int xMax, int yMin, int yMax, TileBase tile)
    {
        Undo.RegisterCompleteObjectUndo(tilemap, "Clear Map Zone");
        for (int x = xMin; x <= xMax; x++)
        {
            for (int y = yMin; y <= yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, -y, 0);
                tilemap.SetTile(cell, tile);
            }
        }

        EditorUtility.SetDirty(tilemap);
        if (tilemap.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
    }

    private static TileBase ResolveTileForKind(CellKind kind, TerrainSet set)
    {
        TerrainTypeData terrain;
        switch (kind)
        {
            case CellKind.Beach:
                terrain = set.beach != null ? set.beach : (set.plains != null ? set.plains : set.water);
                break;
            case CellKind.Plains:
                terrain = set.plains != null ? set.plains : (set.beach != null ? set.beach : set.water);
                break;
            case CellKind.Forest:
                terrain = set.forest != null ? set.forest : (set.plains != null ? set.plains : set.water);
                break;
            case CellKind.Mountain:
                terrain = set.mountain != null ? set.mountain : (set.plains != null ? set.plains : set.water);
                break;
            default:
                terrain = set.water;
                break;
        }

        return terrain != null ? terrain.paletteTile : null;
    }

    private static bool TryResolveTerrains(TerrainDatabase db, out TerrainSet set, out string error)
    {
        set = default;
        error = string.Empty;
        if (db == null)
        {
            error = "TerrainDatabase nulo.";
            return false;
        }

        set.water = FindTerrain(db, "sea", "mar", "agua", "water");
        set.beach = FindTerrain(db, "beach", "praia");
        set.plains = FindTerrain(db, "plains", "planicie", "campo");
        set.forest = FindTerrain(db, "forest", "floresta", "selva");
        set.mountain = FindTerrain(db, "mountain", "montanha");

        if (set.water == null)
        {
            error = "Nao encontrei terreno de agua (ex: id 'sea' / 'mar').";
            return false;
        }

        if (set.plains == null)
            set.plains = set.beach != null ? set.beach : set.water;

        if (set.water.paletteTile == null)
        {
            error = "Terreno de agua encontrado, mas sem Palette Tile.";
            return false;
        }

        return true;
    }

    private static TerrainTypeData FindTerrain(TerrainDatabase db, params string[] tokens)
    {
        if (db == null || db.Terrains == null)
            return null;

        for (int i = 0; i < db.Terrains.Count; i++)
        {
            TerrainTypeData terrain = db.Terrains[i];
            if (terrain == null)
                continue;
            if (terrain.paletteTile == null)
                continue;

            string id = NormalizeForMatch(terrain.id);
            string display = NormalizeForMatch(terrain.displayName);
            string name = NormalizeForMatch(terrain.name);

            for (int t = 0; t < tokens.Length; t++)
            {
                string token = NormalizeForMatch(tokens[t]);
                if (string.IsNullOrEmpty(token))
                    continue;

                if (id.Contains(token) || display.Contains(token) || name.Contains(token))
                    return terrain;
            }
        }

        return null;
    }

    private void AutoDetectContext(bool force)
    {
        if (force || terrainTilemap == null)
            terrainTilemap = FindPreferredTilemap();

        if (force || terrainDatabase == null)
            terrainDatabase = FindPreferredTerrainDatabase();
    }

    private static Tilemap FindPreferredTilemap()
    {
        TurnStateManager tsm = FindAnyObjectByType<TurnStateManager>();
        if (tsm != null)
        {
            SerializedObject so = new SerializedObject(tsm);
            SerializedProperty prop = so.FindProperty("terrainTilemap");
            if (prop != null && prop.objectReferenceValue is Tilemap mapFromTurnState)
                return mapFromTurnState;
        }

        Tilemap byName = FindTilemapByName("Tilemap");
        if (byName != null)
            return byName;
        byName = FindTilemapByName("TileMap");
        if (byName != null)
            return byName;

        return FindAnyObjectByType<Tilemap>();
    }

    private static Tilemap FindTilemapByName(string expectedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName))
            return null;

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map == null)
                continue;
            if (string.Equals(map.name, expectedName, StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return null;
    }

    private static TerrainDatabase FindPreferredTerrainDatabase()
    {
        TurnStateManager tsm = FindAnyObjectByType<TurnStateManager>();
        if (tsm != null)
        {
            SerializedObject so = new SerializedObject(tsm);
            SerializedProperty prop = so.FindProperty("terrainDatabase");
            if (prop != null && prop.objectReferenceValue is TerrainDatabase dbFromTurnState)
                return dbFromTurnState;
        }

        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        TerrainDatabase fallback = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase candidate = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (candidate == null)
                continue;

            if (NormalizeForMatch(candidate.name).Contains("catalogo"))
                return candidate;

            if (fallback == null)
                fallback = candidate;
        }

        return fallback;
    }

    private void GenerateZone()
    {
        if (!EnsureZonePrerequisites(out TerrainSet set))
            return;

        if (!TryClampZoneRect(zoneXMin, zoneXMax, zoneYMin, zoneYMax, out int xMin, out int xMax, out int yMin, out int yMax, persistToFields: true))
            return;

        int effectiveSeed = zoneRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : zoneSeed;
        if (zoneRandomSeed)
            zoneSeed = effectiveSeed;

        int zoneWidth = (xMax - xMin) + 1;
        int zoneHeight = (yMax - yMin) + 1;
        string desc = zoneMode == ZoneGeneratorMode.ConvincingFromDescription ? zoneDescription : "water";
        bool useLandRatioOverride = zoneMode == ZoneGeneratorMode.ConvincingFromDescription && zoneOverrideLandRatio;
        CellKind[,] map = BuildMap(
            zoneWidth,
            zoneHeight,
            zoneMode,
            desc,
            effectiveSeed,
            set,
            minIslandSize,
            xMin,
            yMin,
            useLandRatioOverride,
            zoneLandRatio);
        PaintMap(terrainTilemap, map, new Vector2Int(xMin, -yMin), set);

        status = $"Zona gerada: ({xMin},{yMin}) -> ({xMax},{yMax}) | modo={zoneMode} | seed={effectiveSeed}";
        Debug.Log($"[MapGen] {status}");
    }

    private void ClearZone()
    {
        if (!EnsureZonePrerequisites(out TerrainSet set))
            return;

        if (!TryClampZoneRect(zoneXMin, zoneXMax, zoneYMin, zoneYMax, out int xMin, out int xMax, out int yMin, out int yMax, persistToFields: true))
            return;

        FillRectWithTile(terrainTilemap, xMin, xMax, yMin, yMax, set.water != null ? set.water.paletteTile : null);
        status = $"Zona limpa: ({xMin},{yMin}) -> ({xMax},{yMax})";
        Debug.Log($"[MapGen] {status}");
    }

    private bool EnsureZonePrerequisites(out TerrainSet set)
    {
        set = default;
        if (terrainTilemap == null)
        {
            status = "Falha: selecione uma Tilemap.";
            return false;
        }

        if (terrainDatabase == null)
        {
            status = "Falha: selecione um TerrainDatabase.";
            return false;
        }

        if (!TryResolveTerrains(terrainDatabase, out set, out string resolveError))
        {
            status = $"Falha: {resolveError}";
            return false;
        }

        return true;
    }

    private bool TryClampZoneRect(
        int inputXMin,
        int inputXMax,
        int inputYMin,
        int inputYMax,
        out int xMin,
        out int xMax,
        out int yMin,
        out int yMax,
        bool persistToFields,
        bool useCustomBounds = false,
        int customBoundXMin = 0,
        int customBoundXMax = 0,
        int customBoundYMin = 0,
        int customBoundYMax = 0)
    {
        xMin = inputXMin;
        xMax = inputXMax;
        yMin = inputYMin;
        yMax = inputYMax;

        if (inputXMin >= inputXMax || inputYMin >= inputYMax)
        {
            status = "Erro: zona invalida. xMin/yMin precisam ser menores que xMax/yMax.";
            Debug.LogError($"[MapGen] {status}");
            return false;
        }

        int boundXMin = useCustomBounds ? customBoundXMin : 0;
        int boundYMin = useCustomBounds ? customBoundYMin : 0;
        int boundXMax = useCustomBounds ? customBoundXMax : Mathf.Max(0, width - 1);
        int boundYMax = useCustomBounds ? customBoundYMax : Mathf.Max(0, height - 1);

        int clampedXMin = Mathf.Clamp(inputXMin, boundXMin, boundXMax);
        int clampedXMax = Mathf.Clamp(inputXMax, boundXMin, boundXMax);
        int clampedYMin = Mathf.Clamp(inputYMin, boundYMin, boundYMax);
        int clampedYMax = Mathf.Clamp(inputYMax, boundYMin, boundYMax);

        if (clampedXMin >= clampedXMax || clampedYMin >= clampedYMax)
        {
            status = $"Erro: zona saiu dos limites e ficou invalida apos clamp. Bounds: x[{boundXMin}..{boundXMax}] y[{boundYMin}..{boundYMax}]";
            Debug.LogError($"[MapGen] {status}");
            return false;
        }

        bool changed = clampedXMin != inputXMin
                       || clampedXMax != inputXMax
                       || clampedYMin != inputYMin
                       || clampedYMax != inputYMax;

        xMin = clampedXMin;
        xMax = clampedXMax;
        yMin = clampedYMin;
        yMax = clampedYMax;

        if (changed)
        {
            if (persistToFields)
            {
                zoneXMin = xMin;
                zoneXMax = xMax;
                zoneYMin = yMin;
                zoneYMax = yMax;
            }

            status = $"Aviso: zona foi ajustada por clamp para ({xMin},{yMin}) -> ({xMax},{yMax}) dentro de x[{boundXMin}..{boundXMax}] y[{boundYMin}..{boundYMax}].";
            Debug.LogWarning($"[MapGen] {status}");
        }

        return true;
    }

    private void ApplyZonePreset(ZonePreset preset)
    {
        zoneMode = ZoneGeneratorMode.ConvincingFromDescription;
        zoneXMin = preset.xMin;
        zoneXMax = preset.xMax;
        zoneYMin = preset.yMin;
        zoneYMax = preset.yMax;
        zoneDescription = preset.description;
        zoneOverrideLandRatio = true;
        zoneLandRatio = Mathf.Clamp01(preset.landRatioBase);
        zoneRandomSeed = true;
        status = $"Preset aplicado: {preset.label}. Ajuste seed se quiser e clique em Gerar Zona.";
    }

    private void GenerateArchipelagoComplete()
    {
        if (!EnsureZonePrerequisites(out TerrainSet set))
            return;

        int globalTerrainSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        int generatedCount = 0;

        if (archipelagoSymmetricMode)
        {
            ZonePreset[] leftPresets =
            {
                ArchipelagoPresets[0], // Base NO
                ArchipelagoPresets[2], // Base SO
                ArchipelagoPresets[4]  // Flanco Oeste
            };

            for (int i = 0; i < leftPresets.Length; i++)
            {
                ZonePreset leftPreset = leftPresets[i];
                if (!TryClampZoneRect(
                        leftPreset.xMin,
                        leftPreset.xMax,
                        leftPreset.yMin,
                        leftPreset.yMax,
                        out int lxMin,
                        out int lxMax,
                        out int lyMin,
                        out int lyMax,
                        persistToFields: false,
                        useCustomBounds: true,
                        customBoundXMin: 0,
                        customBoundXMax: ArchipelagoPresetSize - 1,
                        customBoundYMin: 0,
                        customBoundYMax: ArchipelagoPresetSize - 1))
                    continue;

                int zoneWidth = (lxMax - lxMin) + 1;
                int zoneHeight = (lyMax - lyMin) + 1;
                CellKind[,] leftMap = BuildMap(
                    zoneWidth,
                    zoneHeight,
                    ZoneGeneratorMode.ConvincingFromDescription,
                    leftPreset.description,
                    globalTerrainSeed,
                    set,
                    minIslandSize,
                    lxMin,
                    lyMin,
                    useLandRatioOverride: true,
                    landRatioOverride: leftPreset.landRatioBase);

                PaintMap(terrainTilemap, leftMap, new Vector2Int(lxMin, -lyMin), set);
                Debug.Log($"[MapGen] Arquipelago preset '{leftPreset.label}' => zone ({lxMin},{lyMin}) -> ({lxMax},{lyMax}) | cellY {-lyMin}..{-lyMax} | seed={globalTerrainSeed}");
                generatedCount++;

                ZonePreset rightPreset = GetMirroredArchipelagoPreset(leftPreset);
                if (!TryClampZoneRect(
                        rightPreset.xMin,
                        rightPreset.xMax,
                        rightPreset.yMin,
                        rightPreset.yMax,
                        out int rxMin,
                        out int rxMax,
                        out int ryMin,
                        out int ryMax,
                        persistToFields: false,
                        useCustomBounds: true,
                        customBoundXMin: 0,
                        customBoundXMax: ArchipelagoPresetSize - 1,
                        customBoundYMin: 0,
                        customBoundYMax: ArchipelagoPresetSize - 1))
                    continue;

                MirrorZoneHorizontally(
                    terrainTilemap,
                    lxMin,
                    lxMax,
                    lyMin,
                    lyMax,
                    rxMin,
                    rxMax,
                    ryMin,
                    ryMax);
                Debug.Log($"[MapGen] Arquipelago preset espelhado '{rightPreset.label}' <= '{leftPreset.label}' => zone ({rxMin},{ryMin}) -> ({rxMax},{ryMax})");
                generatedCount++;
            }

            ZonePreset[] specialPresets =
            {
                ArchipelagoPresets[6], // Canal Central
                ArchipelagoPresets[7]  // Ilha Central
            };

            for (int i = 0; i < specialPresets.Length; i++)
            {
                ZonePreset preset = specialPresets[i];
                if (!TryClampZoneRect(
                        preset.xMin,
                        preset.xMax,
                        preset.yMin,
                        preset.yMax,
                        out int xMin,
                        out int xMax,
                        out int yMin,
                        out int yMax,
                        persistToFields: false,
                        useCustomBounds: true,
                        customBoundXMin: 0,
                        customBoundXMax: ArchipelagoPresetSize - 1,
                        customBoundYMin: 0,
                        customBoundYMax: ArchipelagoPresetSize - 1))
                    continue;

                int effectiveSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                int zoneWidth = (xMax - xMin) + 1;
                int zoneHeight = (yMax - yMin) + 1;

                CellKind[,] map = BuildMap(
                    zoneWidth,
                    zoneHeight,
                    ZoneGeneratorMode.ConvincingFromDescription,
                    preset.description,
                    effectiveSeed,
                    set,
                    minIslandSize,
                    xMin,
                    yMin,
                    useLandRatioOverride: true,
                    landRatioOverride: preset.landRatioBase);

                PaintMap(terrainTilemap, map, new Vector2Int(xMin, -yMin), set);
                Debug.Log($"[MapGen] Arquipelago preset '{preset.label}' => zone ({xMin},{yMin}) -> ({xMax},{yMax}) | cellY {-yMin}..{-yMax} | seed={effectiveSeed}");
                generatedCount++;
            }

            status = $"Arquipelago completo gerado (simetrico): {generatedCount}/{ArchipelagoPresets.Length} zonas.";
            Debug.Log($"[MapGen] {status}");
            return;
        }

        for (int i = 0; i < ArchipelagoPresets.Length; i++)
        {
            ZonePreset preset = ArchipelagoPresets[i];
            if (!TryClampZoneRect(
                    preset.xMin,
                    preset.xMax,
                    preset.yMin,
                    preset.yMax,
                    out int xMin,
                    out int xMax,
                    out int yMin,
                    out int yMax,
                    persistToFields: false,
                    useCustomBounds: true,
                    customBoundXMin: 0,
                    customBoundXMax: ArchipelagoPresetSize - 1,
                    customBoundYMin: 0,
                    customBoundYMax: ArchipelagoPresetSize - 1))
                continue;

            bool isSpecialSeedPreset = IsSpecialArchipelagoPreset(preset);
            int effectiveSeed = isSpecialSeedPreset
                ? UnityEngine.Random.Range(int.MinValue, int.MaxValue)
                : globalTerrainSeed;
            int zoneWidth = (xMax - xMin) + 1;
            int zoneHeight = (yMax - yMin) + 1;

            CellKind[,] map = BuildMap(
                zoneWidth,
                zoneHeight,
                ZoneGeneratorMode.ConvincingFromDescription,
                preset.description,
                effectiveSeed,
                set,
                minIslandSize,
                xMin,
                yMin,
                useLandRatioOverride: true,
                landRatioOverride: preset.landRatioBase);

            PaintMap(terrainTilemap, map, new Vector2Int(xMin, -yMin), set);
            Debug.Log($"[MapGen] Arquipelago preset '{preset.label}' => zone ({xMin},{yMin}) -> ({xMax},{yMax}) | cellY {-yMin}..{-yMax} | seed={effectiveSeed}");
            generatedCount++;
        }

        status = $"Arquipelago completo gerado: {generatedCount}/{ArchipelagoPresets.Length} zonas.";
        Debug.Log($"[MapGen] {status}");
    }

    private static ZonePreset GetMirroredArchipelagoPreset(ZonePreset leftPreset)
    {
        if (leftPreset.label == "Base NO")
            return ArchipelagoPresets[1];
        if (leftPreset.label == "Base SO")
            return ArchipelagoPresets[3];
        if (leftPreset.label == "Flanco Oeste")
            return ArchipelagoPresets[5];
        return leftPreset;
    }

    private static void MirrorZoneHorizontally(
        Tilemap tilemap,
        int srcXMin,
        int srcXMax,
        int srcYMin,
        int srcYMax,
        int dstXMin,
        int dstXMax,
        int dstYMin,
        int dstYMax)
    {
        if (tilemap == null)
            return;

        int srcWidth = (srcXMax - srcXMin) + 1;
        int srcHeight = (srcYMax - srcYMin) + 1;
        int dstWidth = (dstXMax - dstXMin) + 1;
        int dstHeight = (dstYMax - dstYMin) + 1;
        if (srcWidth != dstWidth || srcHeight != dstHeight)
            return;

        Undo.RegisterCompleteObjectUndo(tilemap, "Mirror Map Zone");
        for (int y = 0; y < srcHeight; y++)
        {
            int srcY = srcYMin + y;
            int dstY = dstYMin + y;
            for (int i = 0; i < srcWidth; i++)
            {
                int srcX = srcXMin + i;
                int dstX = dstXMax - i;
                Vector3Int srcCell = new Vector3Int(srcX, -srcY, 0);
                Vector3Int dstCell = new Vector3Int(dstX, -dstY, 0);
                tilemap.SetTile(dstCell, tilemap.GetTile(srcCell));
            }
        }

        EditorUtility.SetDirty(tilemap);
        if (tilemap.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(tilemap.gameObject.scene);
    }

    private static bool IsSpecialArchipelagoPreset(ZonePreset preset)
    {
        string id = NormalizeForMatch(preset.label);
        return id.Contains("canal central") || id.Contains("ilha central");
    }
}
