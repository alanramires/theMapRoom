#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(BoardTopologyIndex))]
public sealed class BoardTopologyIndexEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BoardTopologyIndex index =
            (BoardTopologyIndex)target;
        EditorGUILayout.Space();
        string shortFingerprint =
            ShortFingerprint(index.TopologyFingerprint);
        EditorGUILayout.HelpBox(
            $"Version: {index.TopologyVersion}\n" +
            $"Cells: {index.CellCount}\n" +
            $"Route edges: {index.RouteEdgeCount}\n" +
            $"Fingerprint: {shortFingerprint}",
            index.IsReady
                ? MessageType.Info
                : MessageType.Warning);

        if (GUILayout.Button("Rebuild Serialized Index"))
        {
            Undo.RecordObject(index, "Rebuild Board Topology Index");
            index.RebuildSerializedIndex();
            EditorUtility.SetDirty(index);
            EditorSceneManager.MarkSceneDirty(
                index.gameObject.scene);
        }

        if (GUILayout.Button("Validate Against Scene"))
            index.ValidateAndLog();
    }

    private static string ShortFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return "-";
        return fingerprint.Length <= 16
            ? fingerprint
            : fingerprint.Substring(0, 16) + "...";
    }
}

public static class BoardTopologyIndexTools
{
    [MenuItem(
        "Tools/Tabuleiro/Board Topology/Rebuild Active Scene")]
    public static void RebuildActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        BoardTopologyIndex index = FindForScene(scene);
        if (index == null)
        {
            GameObject host = new GameObject("BoardTopologyIndex");
            Undo.RegisterCreatedObjectUndo(
                host,
                "Create Board Topology Index");
            SceneManager.MoveGameObjectToScene(host, scene);
            index = Undo.AddComponent<BoardTopologyIndex>(host);
        }

        Undo.RecordObject(index, "Rebuild Board Topology Index");
        index.AutoResolveSources();
        index.RebuildSerializedIndex();
        EditorUtility.SetDirty(index);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeObject = index.gameObject;
    }

    [MenuItem(
        "Tools/Tabuleiro/Board Topology/Validate Active Scene")]
    public static void ValidateActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        BoardTopologyIndex index = FindForScene(scene);
        if (index == null)
        {
            Debug.LogError(
                "[BoardTopology] A cena ativa não possui um " +
                "BoardTopologyIndex serializado.");
            return;
        }
        index.ValidateAndLog();
        Selection.activeObject = index.gameObject;
    }

    [MenuItem(
        "Tools/Tabuleiro/Board Topology/Rebuild Enabled Build Scenes")]
    public static void RebuildEnabledBuildScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] previousSetup =
            EditorSceneManager.GetSceneManagerSetup();
        try
        {
            int failures = RebuildEnabledBuildScenesCore();
            if (failures > 0)
            {
                Debug.LogError(
                    $"[BoardTopology] Build scenes concluídas com " +
                    $"{failures} falha(s).");
            }
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(
                previousSetup);
        }
    }

    public static void RebuildEnabledBuildScenesFromCommandLine()
    {
        int failures = RebuildEnabledBuildScenesCore();
        AssetDatabase.SaveAssets();
        if (failures > 0)
        {
            throw new InvalidOperationException(
                $"BoardTopology: {failures} build scene(s) inválida(s).");
        }
    }

    private static int RebuildEnabledBuildScenesCore()
    {
        int failures = 0;
        int rebuilt = 0;
        EditorBuildSettingsScene[] buildScenes =
            EditorBuildSettings.scenes;
        for (int i = 0; i < buildScenes.Length; i++)
        {
            EditorBuildSettingsScene buildScene = buildScenes[i];
            if (buildScene == null
                || !buildScene.enabled
                || string.IsNullOrWhiteSpace(buildScene.path))
            {
                continue;
            }

            Scene scene = EditorSceneManager.OpenScene(
                buildScene.path,
                OpenSceneMode.Single);
            if (!TryResolveSources(
                    scene,
                    out Tilemap tilemap,
                    out TerrainDatabase terrainDatabase))
            {
                Debug.Log(
                    $"[BoardTopology] Cena sem tabuleiro ignorada: " +
                    $"{buildScene.path}");
                continue;
            }

            BoardTopologyIndex index = FindForScene(scene);
            if (index == null)
            {
                GameObject host =
                    new GameObject("BoardTopologyIndex");
                SceneManager.MoveGameObjectToScene(host, scene);
                index =
                    host.AddComponent<BoardTopologyIndex>();
            }

            index.ConfigureSources(tilemap, terrainDatabase);
            index.RebuildSerializedIndex();
            BoardTopologyValidationReport report =
                index.ValidateAgainstSources();
            if (!report.IsValid)
            {
                failures++;
                Debug.LogError(
                    report.Format(buildScene.path),
                    index);
            }

            EditorUtility.SetDirty(index);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                failures++;
                Debug.LogError(
                    $"[BoardTopology] Falha ao salvar " +
                    $"{buildScene.path}.");
            }
            rebuilt++;
        }

        Debug.Log(
            $"[BoardTopology] Build scenes: {rebuilt} reconstruída(s), " +
            $"{failures} falha(s).");
        return failures;
    }

    private static bool TryResolveSources(
        Scene scene,
        out Tilemap tilemap,
        out TerrainDatabase database)
    {
        tilemap = null;
        database = null;
        TurnStateManager[] turnManagers =
            UnityEngine.Object.FindObjectsByType<TurnStateManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < turnManagers.Length; i++)
        {
            TurnStateManager manager = turnManagers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;
            tilemap = manager.MovementTilemapRef;
            database = manager.TerrainDatabaseRef;
            if (tilemap != null && database != null)
                return true;
        }

        RoadNetworkManager[] roadNetworks =
            UnityEngine.Object.FindObjectsByType<RoadNetworkManager>(
                FindObjectsInactive.Include);
        for (int i = 0; i < roadNetworks.Length; i++)
        {
            RoadNetworkManager network = roadNetworks[i];
            if (network == null || network.gameObject.scene != scene)
                continue;
            if (tilemap == null)
                tilemap = network.BoardTilemap;
            if (database == null)
                database = network.TerrainDatabase;
            if (tilemap != null && database != null)
                return true;
        }
        return false;
    }

    private static BoardTopologyIndex FindForScene(Scene scene)
    {
        BoardTopologyIndex[] indices =
            UnityEngine.Object.FindObjectsByType<BoardTopologyIndex>(
                FindObjectsInactive.Include);
        for (int i = 0; i < indices.Length; i++)
        {
            BoardTopologyIndex index = indices[i];
            if (index != null && index.gameObject.scene == scene)
                return index;
        }
        return null;
    }
}
#endif
