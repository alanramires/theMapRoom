using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Faxina de cena: encontra e remove LAYOUT que veio de carona numa duplicacao.
///
/// O problema que ela resolve nao e acoplamento — e o contrario. Rota de estrada
/// mora na CENA de proposito (routesMigratedToScene), que e o tier certo pra
/// layout. Duplicar uma cena copia o que a cena tem, e ate agora nao existia
/// operacao de "esvaziar o tabuleiro". Esta e ela.
///
/// O sinal mais forte de conteudo estrangeiro e o ownerDatabase da rota: se ele
/// aponta pra um StructureDatabase diferente do que este manager usa, a rota veio
/// de outro mapa. Em runtime ela ja e ignorada
/// (RoadNetworkManager.IsRouteAllowedForCurrentDatabase), mas continua sendo:
///
///   1. peso morto no arquivo de cena;
///   2. mina — rota com ownerDatabase NULO passa como legado e volta a valer;
///   3. risco pro bake do recorte, se ele ler a lista crua em vez da filtrada.
///
/// Nada aqui destroi sem confirmacao, e tudo passa por Undo.
/// </summary>
public class SceneSanitizerWindow : EditorWindow
{
    private enum RouteOrigin
    {
        Own = 0,      // ownerDatabase == o database deste manager
        Foreign = 1,  // ownerDatabase != o database deste manager
        Legacy = 2    // ownerDatabase == null  → tratada como global, ATIVA
    }

    private sealed class RouteRow
    {
        public StructureRoadRouteBucket bucket;
        public RoadRouteDefinition route;
        public RouteOrigin origin;
        public int cellCount;
        public bool hasCells;
        public Vector2Int min;
        public Vector2Int max;
        public bool outsideBoard;
    }

    private RoadNetworkManager roadManager;
    private readonly List<RouteRow> rows = new List<RouteRow>();
    private int ownCount;
    private int foreignCount;
    private int legacyCount;
    private int outsideCount;

    private ConstructionManager[] constructions = new ConstructionManager[0];
    private UnitManager[] units = new UnitManager[0];
    private readonly List<Tilemap> tilemaps = new List<Tilemap>();
    private readonly List<int> tilemapCounts = new List<int>();

    private bool hasBoard;
    private Vector2Int boardMin;
    private Vector2Int boardMax;

    private Vector2 scroll;
    private bool showRouteList;
    private bool showTilemaps;
    private string status = "Clique em Diagnosticar.";

    [MenuItem("Tools/Utils/Faxina de Cena")]
    public static void OpenWindow()
    {
        GetWindow<SceneSanitizerWindow>("Faxina de Cena");
    }

    private void OnEnable()
    {
        Diagnose();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Faxina de Cena", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("cena", SceneManager.GetActiveScene().name);

        if (GUILayout.Button("Diagnosticar"))
            Diagnose();

        EditorGUILayout.Space(4f);
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawRoutesSection();
        EditorGUILayout.Space(8f);
        DrawPiecesSection();
        EditorGUILayout.Space(8f);
        DrawTilemapSection();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // ───────────────────────────────────────────────────────────── rotas ──

    private void DrawRoutesSection()
    {
        EditorGUILayout.LabelField("Rotas de estrada", EditorStyles.boldLabel);

        if (roadManager == null)
        {
            EditorGUILayout.HelpBox("Nenhum RoadNetworkManager nesta cena.", MessageType.None);
            return;
        }

        EditorGUILayout.LabelField("catálogo do manager",
            roadManager.StructureDatabase != null ? roadManager.StructureDatabase.name : "— nenhum —");
        EditorGUILayout.LabelField("rotas migradas p/ cena", roadManager.RoutesMigratedToScene ? "sim" : "não");

        EditorGUILayout.LabelField("desta cena (own)", ownCount.ToString());
        EditorGUILayout.LabelField("de outro catálogo", foreignCount.ToString());
        EditorGUILayout.LabelField("sem dono (legado)", legacyCount.ToString());
        EditorGUILayout.LabelField("com célula fora do tabuleiro", outsideCount.ToString());

        if (legacyCount > 0)
        {
            EditorGUILayout.HelpBox(
                "Rota sem ownerDatabase é tratada como legado/global e PASSA pelo filtro de runtime. " +
                "É a única classe que age mesmo sendo estrangeira.",
                MessageType.Warning);
        }

        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(foreignCount == 0))
        {
            if (GUILayout.Button($"Remover rotas de outro catálogo  ({foreignCount})"))
                RemoveRoutes(r => r.origin == RouteOrigin.Foreign, "de outro catálogo");
        }

        using (new EditorGUI.DisabledScope(legacyCount == 0))
        {
            if (GUILayout.Button($"Remover rotas sem dono (legado)  ({legacyCount})"))
                RemoveRoutes(r => r.origin == RouteOrigin.Legacy, "sem dono");
        }

        using (new EditorGUI.DisabledScope(outsideCount == 0))
        {
            if (GUILayout.Button($"Remover rotas com célula fora do tabuleiro  ({outsideCount})"))
                RemoveRoutes(r => r.outsideBoard, "fora do tabuleiro");
        }

        using (new EditorGUI.DisabledScope(rows.Count == 0))
        {
            if (GUILayout.Button($"Limpar TODAS as rotas  ({rows.Count})"))
                RemoveRoutes(_ => true, "TODAS");
        }

        EditorGUILayout.Space(2f);
        showRouteList = EditorGUILayout.Foldout(showRouteList, $"Listar rotas ({rows.Count})");
        if (!showRouteList)
            return;

        EditorGUI.indentLevel++;
        for (int i = 0; i < rows.Count; i++)
        {
            RouteRow row = rows[i];
            string origin = row.origin == RouteOrigin.Own ? "própria"
                          : row.origin == RouteOrigin.Foreign ? "ESTRANGEIRA"
                          : "LEGADO";
            string extent = row.hasCells
                ? $"x {row.min.x}..{row.max.x}  y {row.min.y}..{row.max.y}"
                : "sem células";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{row.route.routeName}",
                $"{origin} · {row.cellCount} cel · {extent}{(row.outsideBoard ? "  << FORA" : string.Empty)}");
            if (GUILayout.Button("x", GUILayout.Width(22f)))
            {
                RouteRow target = row;
                RemoveRoutes(r => r == target, $"'{target.route.routeName}'", skipConfirm: true);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
    }

    private void RemoveRoutes(System.Predicate<RouteRow> match, string label, bool skipConfirm = false)
    {
        if (roadManager == null)
            return;

        List<RouteRow> doomed = rows.FindAll(match);
        if (doomed.Count == 0)
            return;

        if (!skipConfirm && !EditorUtility.DisplayDialog(
                "Faxina de Cena",
                $"Remover {doomed.Count} rota(s) {label} da cena '{SceneManager.GetActiveScene().name}'?\n\n" +
                "Dá pra desfazer com Ctrl+Z.",
                "Remover",
                "Cancelar"))
        {
            return;
        }

        Undo.RecordObject(roadManager, $"Remover rotas {label}");

        for (int i = 0; i < doomed.Count; i++)
        {
            RouteRow row = doomed[i];
            row.bucket?.routes?.Remove(row.route);
        }

        // O lookup e os visuais sao derivados: sem invalidar, a cena continua
        // desenhando estrada que nao existe mais no dado.
        roadManager.InvalidateRoutesLookup();
        roadManager.RebuildRoadVisuals();

        EditorUtility.SetDirty(roadManager);
        EditorSceneManager.MarkSceneDirty(roadManager.gameObject.scene);

        status = $"{doomed.Count} rota(s) {label} removida(s). Salve a cena.";
        Diagnose();
    }

    // ───────────────────────────────────────────────────────────── peças ──

    private void DrawPiecesSection()
    {
        EditorGUILayout.LabelField("Peças em campo", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("construções", constructions.Length.ToString());
        EditorGUILayout.LabelField("unidades", units.Length.ToString());

        using (new EditorGUI.DisabledScope(constructions.Length == 0))
        {
            if (GUILayout.Button($"Destruir construções  ({constructions.Length})"))
                DestroyAll(constructions, "construções");
        }

        using (new EditorGUI.DisabledScope(units.Length == 0))
        {
            if (GUILayout.Button($"Destruir unidades  ({units.Length})"))
                DestroyAll(units, "unidades");
        }
    }

    private void DestroyAll(Component[] targets, string label)
    {
        if (targets == null || targets.Length == 0)
            return;

        if (!EditorUtility.DisplayDialog(
                "Faxina de Cena",
                $"Destruir {targets.Length} {label} da cena '{SceneManager.GetActiveScene().name}'?\n\n" +
                "Dá pra desfazer com Ctrl+Z.",
                "Destruir",
                "Cancelar"))
        {
            return;
        }

        int destroyed = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            Component target = targets[i];
            if (target == null)
                continue;
            Undo.DestroyObjectImmediate(target.gameObject);
            destroyed++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        status = $"{destroyed} {label} destruída(s). Salve a cena.";
        Diagnose();
    }

    // ──────────────────────────────────────────────────────────── tiles ──

    private void DrawTilemapSection()
    {
        showTilemaps = EditorGUILayout.Foldout(showTilemaps, $"Tilemaps ({tilemaps.Count})");
        if (!showTilemaps)
            return;

        EditorGUILayout.HelpBox(
            "Apagar tiles é o martelo grande — use pra transformar uma cópia de mapa na cena " +
            "de Batalha vazia. Numa cena de AUTORIA isso apaga o seu desenho.",
            MessageType.Warning);

        EditorGUI.indentLevel++;
        for (int i = 0; i < tilemaps.Count; i++)
        {
            Tilemap map = tilemaps[i];
            if (map == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(map.name, $"{tilemapCounts[i]} tiles");
            using (new EditorGUI.DisabledScope(tilemapCounts[i] == 0))
            {
                if (GUILayout.Button("apagar", GUILayout.Width(60f)))
                {
                    ClearTilemap(map, tilemapCounts[i]);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
    }

    private void ClearTilemap(Tilemap map, int count)
    {
        if (map == null)
            return;

        if (!EditorUtility.DisplayDialog(
                "Faxina de Cena",
                $"Apagar os {count} tiles de '{map.name}'?\n\nDá pra desfazer com Ctrl+Z.",
                "Apagar",
                "Cancelar"))
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(map, $"Apagar tiles de {map.name}");
        map.ClearAllTiles();
        EditorUtility.SetDirty(map);
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        status = $"'{map.name}' limpo ({count} tiles). Salve a cena.";
        Diagnose();
    }

    // ──────────────────────────────────────────────────────── diagnostico ──

    private void Diagnose()
    {
        rows.Clear();
        ownCount = foreignCount = legacyCount = outsideCount = 0;
        tilemaps.Clear();
        tilemapCounts.Clear();

        Scene active = SceneManager.GetActiveScene();

        roadManager = FindInScene<RoadNetworkManager>(active);
        constructions = FindAllInScene<ConstructionManager>(active);
        units = FindAllInScene<UnitManager>(active);

        CollectTilemaps(active);
        ResolveBoardBounds();

        if (roadManager == null)
        {
            status = "Sem RoadNetworkManager nesta cena.";
            Repaint();
            return;
        }

        StructureDatabase mine = roadManager.StructureDatabase;
        IReadOnlyList<StructureRoadRouteBucket> buckets = roadManager.RoadRoutesByStructure;
        if (buckets == null)
        {
            status = "Nenhuma rota na cena.";
            Repaint();
            return;
        }

        for (int b = 0; b < buckets.Count; b++)
        {
            StructureRoadRouteBucket bucket = buckets[b];
            if (bucket?.routes == null)
                continue;

            for (int r = 0; r < bucket.routes.Count; r++)
            {
                RoadRouteDefinition route = bucket.routes[r];
                if (route == null)
                    continue;

                RouteRow row = new RouteRow
                {
                    bucket = bucket,
                    route = route,
                    origin = route.ownerDatabase == null
                        ? RouteOrigin.Legacy
                        : (route.ownerDatabase == mine ? RouteOrigin.Own : RouteOrigin.Foreign)
                };

                MeasureRoute(route, row);
                rows.Add(row);

                if (row.origin == RouteOrigin.Own) ownCount++;
                else if (row.origin == RouteOrigin.Foreign) foreignCount++;
                else legacyCount++;

                if (row.outsideBoard) outsideCount++;
            }
        }

        status = $"{rows.Count} rota(s): {ownCount} própria(s), {foreignCount} de outro catálogo, " +
                 $"{legacyCount} sem dono. {constructions.Length} construção(ões), {units.Length} unidade(s).";
        Repaint();
    }

    private void MeasureRoute(RoadRouteDefinition route, RouteRow row)
    {
        List<Vector3Int> cells = route.cells;
        if (cells == null || cells.Count == 0)
            return;

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int c = cells[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }

        row.hasCells = true;
        row.cellCount = cells.Count;
        row.min = new Vector2Int(minX, minY);
        row.max = new Vector2Int(maxX, maxY);

        if (hasBoard)
        {
            row.outsideBoard = minX < boardMin.x || maxX > boardMax.x
                            || minY < boardMin.y || maxY > boardMax.y;
        }
    }

    /// <summary>
    /// Caixa do tabuleiro = extensao do tilemap com mais celulas usadas. Serve so
    /// pra classificar rota "fora"; nao e verdade sobre o mapa.
    /// </summary>
    private void ResolveBoardBounds()
    {
        hasBoard = false;
        Tilemap best = null;
        int bestCount = 0;

        for (int i = 0; i < tilemaps.Count; i++)
        {
            if (tilemapCounts[i] <= bestCount)
                continue;
            bestCount = tilemapCounts[i];
            best = tilemaps[i];
        }

        if (best == null || bestCount == 0)
            return;

        BoundsInt b = best.cellBounds;
        boardMin = new Vector2Int(b.xMin, b.yMin);
        boardMax = new Vector2Int(b.xMax - 1, b.yMax - 1);
        hasBoard = true;
    }

    private void CollectTilemaps(Scene active)
    {
        Tilemap[] all = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Tilemap map = all[i];
            if (map == null || map.gameObject.scene != active)
                continue;

            tilemaps.Add(map);
            tilemapCounts.Add(CountTiles(map));
        }
    }

    private static int CountTiles(Tilemap map)
    {
        BoundsInt b = map.cellBounds;
        if (b.size.x <= 0 || b.size.y <= 0)
            return 0;

        TileBase[] block = map.GetTilesBlock(b);
        int count = 0;
        for (int i = 0; i < block.Length; i++)
        {
            if (block[i] != null)
                count++;
        }

        return count;
    }

    private static T FindInScene<T>(Scene active) where T : Component
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == active)
                return all[i];
        }

        return null;
    }

    private static T[] FindAllInScene<T>(Scene active) where T : Component
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<T> mine = new List<T>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene == active)
                mine.Add(all[i]);
        }

        return mine.ToArray();
    }
}
