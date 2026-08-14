using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Ferramenta de autoria de mapa (cenas de Autoria/, mas serve em qualquer cena).
///
/// Responde quatro perguntas que so se respondia contando hexagono na unha:
///   1. que caixa eu desenhei, e de que tamanho    (contorno + leitura)
///   2. onde faltou pintar                          (buracos em vermelho)
///   3. que coordenada e esta celula                (regua nas bordas / rotulo por hex)
///   4. onde vai cair o quadrante                   (previa de retangulo, dois de uma vez)
///
/// NADA de prefab de texto: tudo e Handles, que so existe no Editor, some sozinho
/// e nao suja a cena. Rotular hexagono com GameObject seriam centenas de objetos
/// pra criar, esconder e limpar — e inviavel na cena de Mundo.
///
/// O contorno e desenhado ligando os CENTROS das celulas da borda, entao ele sai
/// serrilhado de proposito: num grid hexagonal com linhas deslocadas, um retangulo
/// limpo em coordenada de celula NAO e um retangulo na tela. Ver o zigue-zague e o
/// ponto — foi exatamente pintar "onde parece reto" que deixou a borda direita do
/// Fixture furada em linha sim, linha nao.
/// </summary>
public class MapHelperWindow : EditorWindow
{
    private const int MaxHolesTracked = 4000;
    private const float MinHandleSizeForCellLabels = 1.5f;

    [SerializeField] private Tilemap overrideTilemap;

    [Header("Overlay")]
    [SerializeField] private bool showBounds = true;
    [SerializeField] private bool showHoles = true;
    [SerializeField] private bool showRuler = true;
    [SerializeField] private int rulerStep = 5;
    [SerializeField] private bool showCellLabels = false;
    [SerializeField] private int cellLabelBudget = 400;

    [Header("Previa de quadrante")]
    [SerializeField] private bool showRectA;
    [SerializeField] private Vector2Int rectAOrigin;
    [SerializeField] private Vector2Int rectASize = new Vector2Int(18, 18);
    [SerializeField] private bool showRectB;
    [SerializeField] private Vector2Int rectBOrigin;
    [SerializeField] private Vector2Int rectBSize = new Vector2Int(18, 18);
    [SerializeField] private int splitWidth = 18;

    private enum PickTarget
    {
        None = 0,
        RectA = 1,
        RectB = 2
    }

    // Desenho por dois cliques. Enquanto so o primeiro canto existe, o retangulo e
    // PROVISORIO: acompanha o mouse, e nada foi gravado. O segundo clique e o
    // compromisso. Mesma forma do Neutral -> provisorio -> compromisso -> Neutral
    // que rege toda acao do jogo (docs/arquitetura/acoes_transacionais.md).
    private PickTarget pickTarget;
    private bool hasFirstCorner;
    private Vector3Int firstCorner;
    private bool hasHoverCell;
    private Vector3Int hoverCell;

    private bool hasScan;
    private Vector2Int scanMin;
    private Vector2Int scanMax;
    private int scanTileCount;
    private int scanHoleCount;
    private readonly List<Vector3Int> holes = new List<Vector3Int>();
    private string status = "Clique em Recalcular para varrer o tilemap.";

    private GUIStyle rulerStyle;
    private GUIStyle cellStyle;
    private GUIStyle rectStyle;

    [MenuItem("Tools/Utils/Map Helper")]
    public static void OpenWindow()
    {
        GetWindow<MapHelperWindow>("Map Helper");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectTilemap();
        Scan();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        // Sem isto o modo de clique fica armado depois da janela fechar, e o
        // proximo clique no Scene some sem explicacao.
        pickTarget = PickTarget.None;
        hasFirstCorner = false;
        hasHoverCell = false;
    }

    // ────────────────────────────────────────────────────────────── janela ──

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Map Helper", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", overrideTilemap, typeof(Tilemap), true);
        if (EditorGUI.EndChangeCheck())
            Scan();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
        {
            overrideTilemap = null;
            AutoDetectTilemap();
            Scan();
        }
        if (GUILayout.Button("Recalcular"))
            Scan();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        DrawScanReadout();

        EditorGUILayout.Space(6f);
        DrawOverlayToggles();

        EditorGUILayout.Space(6f);
        DrawRectSection();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    private void DrawScanReadout()
    {
        EditorGUILayout.LabelField("Caixa desenhada", EditorStyles.boldLabel);

        if (!hasScan)
        {
            EditorGUILayout.LabelField("—", "sem varredura");
            return;
        }

        int width = scanMax.x - scanMin.x + 1;
        int height = scanMax.y - scanMin.y + 1;

        EditorGUILayout.LabelField("origem", $"({scanMin.x}, {scanMin.y})");
        EditorGUILayout.LabelField("até", $"({scanMax.x}, {scanMax.y})");
        EditorGUILayout.LabelField("tamanho", $"{width} × {height}  =  {width * height} células");
        EditorGUILayout.LabelField("tiles pintados", scanTileCount.ToString());

        if (scanHoleCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{scanHoleCount} buraco(s) dentro da caixa.\n" +
                "Buraco em borda alternando linha sim/linha não é o serrilhado do grid hexagonal: " +
                "a borda foi pintada a olho, não pela coordenada.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("Caixa sólida — nenhum buraco.", MessageType.Info);
        }
    }

    private void DrawOverlayToggles()
    {
        EditorGUILayout.LabelField("Overlay", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        showBounds = EditorGUILayout.Toggle("Contorno da caixa", showBounds);
        showHoles = EditorGUILayout.Toggle("Buracos (vermelho)", showHoles);
        showRuler = EditorGUILayout.Toggle("Régua nas bordas", showRuler);
        using (new EditorGUI.DisabledScope(!showRuler))
            rulerStep = Mathf.Max(1, EditorGUILayout.IntField("passo da régua", rulerStep));

        showCellLabels = EditorGUILayout.Toggle("Rótulo por hexágono", showCellLabels);
        using (new EditorGUI.DisabledScope(!showCellLabels))
            cellLabelBudget = Mathf.Max(50, EditorGUILayout.IntField("teto de rótulos", cellLabelBudget));

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        if (showCellLabels)
        {
            EditorGUILayout.HelpBox(
                "Rótulo por hexágono é lupa, não vista geral: desenha só o que está na tela, " +
                "e só com zoom suficiente. A régua é o modo que escala pro mapa grande.",
                MessageType.None);
        }
    }

    private void DrawRectSection()
    {
        EditorGUILayout.LabelField("Prévia de quadrante", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        showRectA = EditorGUILayout.Toggle("Mostrar Q1 (verde)", showRectA);
        using (new EditorGUI.DisabledScope(!showRectA))
        {
            rectAOrigin = EditorGUILayout.Vector2IntField("Q1 origem", rectAOrigin);
            rectASize = EditorGUILayout.Vector2IntField("Q1 tamanho", rectASize);
        }

        EditorGUILayout.Space(2f);
        showRectB = EditorGUILayout.Toggle("Mostrar Q2 (laranja)", showRectB);
        using (new EditorGUI.DisabledScope(!showRectB))
        {
            rectBOrigin = EditorGUILayout.Vector2IntField("Q2 origem", rectBOrigin);
            rectBSize = EditorGUILayout.Vector2IntField("Q2 tamanho", rectBSize);
        }

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        EditorGUILayout.Space(4f);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(pickTarget == PickTarget.RectA ? "…escolhendo Q1" : "Desenhar Q1 no Scene"))
            BeginPick(PickTarget.RectA);
        if (GUILayout.Button(pickTarget == PickTarget.RectB ? "…escolhendo Q2" : "Desenhar Q2 no Scene"))
            BeginPick(PickTarget.RectB);
        EditorGUILayout.EndHorizontal();

        if (pickTarget != PickTarget.None)
        {
            EditorGUILayout.HelpBox(
                hasFirstCorner
                    ? "Clique no segundo canto para fechar o retângulo. ESC cancela."
                    : "Clique no primeiro canto no Scene. ESC cancela.",
                MessageType.Warning);

            if (GUILayout.Button("Cancelar seleção"))
                CancelPick();
        }

        EditorGUILayout.Space(4f);
        splitWidth = Mathf.Max(1, EditorGUILayout.IntField("largura do quadrante", splitWidth));

        using (new EditorGUI.DisabledScope(!hasScan))
        {
            if (GUILayout.Button("Dividir a caixa em 2 (encosta nas bordas)"))
                SplitScanIntoTwo();
        }

        if (showRectA && showRectB)
            DrawOverlapReadout();
    }

    private void DrawOverlapReadout()
    {
        RectInt a = new RectInt(rectAOrigin.x, rectAOrigin.y, Mathf.Max(1, rectASize.x), Mathf.Max(1, rectASize.y));
        RectInt b = new RectInt(rectBOrigin.x, rectBOrigin.y, Mathf.Max(1, rectBSize.x), Mathf.Max(1, rectBSize.y));

        int overlapMinX = Mathf.Max(a.xMin, b.xMin);
        int overlapMaxX = Mathf.Min(a.xMax, b.xMax) - 1;
        int overlapMinY = Mathf.Max(a.yMin, b.yMin);
        int overlapMaxY = Mathf.Min(a.yMax, b.yMax) - 1;

        if (overlapMinX > overlapMaxX || overlapMinY > overlapMaxY)
        {
            EditorGUILayout.LabelField("sobreposição", "nenhuma");
            return;
        }

        int w = overlapMaxX - overlapMinX + 1;
        int h = overlapMaxY - overlapMinY + 1;
        EditorGUILayout.LabelField("sobreposição", $"x {overlapMinX}..{overlapMaxX} · y {overlapMinY}..{overlapMaxY}  ({w} × {h})");
        EditorGUILayout.HelpBox(
            "Sobreposição é recurso, não erro: é a faixa de fronteira compartilhada. " +
            "É aqui que a muralha e o passo da estrada devem morar.",
            MessageType.None);
    }

    /// <summary>
    /// Encosta Q1 na borda esquerda e Q2 na direita, ambos com a largura pedida.
    /// Se a caixa for mais estreita que 2× a largura, a diferenca vira sobreposicao
    /// no meio — que e exatamente a faixa de fronteira que se quer.
    /// </summary>
    private void SplitScanIntoTwo()
    {
        if (!hasScan)
            return;

        int height = scanMax.y - scanMin.y + 1;
        int width = Mathf.Max(1, splitWidth);

        rectAOrigin = new Vector2Int(scanMin.x, scanMin.y);
        rectASize = new Vector2Int(width, height);

        rectBOrigin = new Vector2Int(scanMax.x - width + 1, scanMin.y);
        rectBSize = new Vector2Int(width, height);

        showRectA = true;
        showRectB = true;

        int boxWidth = scanMax.x - scanMin.x + 1;
        int overlap = (width * 2) - boxWidth;
        status = overlap > 0
            ? $"Dois quadrantes de {width}×{height} numa caixa de {boxWidth} → {overlap} coluna(s) de sobreposição."
            : overlap == 0
                ? $"Dois quadrantes de {width}×{height} cobrem a caixa exatamente, sem sobreposição."
                : $"Dois quadrantes de {width}×{height} deixam {-overlap} coluna(s) descoberta(s) no meio.";

        SceneView.RepaintAll();
        Repaint();
    }

    // ─────────────────────────────────────────────────────────── varredura ──

    /// <summary>
    /// Varre o tilemap UMA vez e guarda o resultado. Nao roda por frame de
    /// proposito: na cena de Mundo isso e dezenas de milhares de celulas, e
    /// varrer no OnSceneGUI arrastaria o Editor inteiro.
    /// </summary>
    private void Scan()
    {
        holes.Clear();
        hasScan = false;
        scanTileCount = 0;
        scanHoleCount = 0;

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            status = "Nenhum tilemap encontrado. Arraste um no campo acima.";
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        BoundsInt bounds = map.cellBounds;
        if (bounds.size.x <= 0 || bounds.size.y <= 0)
        {
            status = $"'{map.name}' não tem nenhum tile.";
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        // Uma chamada so em vez de HasTile celula a celula.
        TileBase[] block = map.GetTilesBlock(bounds);

        // Colapsa o eixo z: a celula conta como pintada se QUALQUER z tem tile.
        // O projeto zera z em toda comparacao, entao z e ruido aqui.
        int sx = bounds.size.x;
        int sy = bounds.size.y;
        int sz = Mathf.Max(1, bounds.size.z);
        bool[] painted = new bool[sx * sy];

        for (int z = 0; z < sz; z++)
        {
            int layerOffset = z * sx * sy;
            for (int y = 0; y < sy; y++)
            {
                int rowOffset = layerOffset + (y * sx);
                for (int x = 0; x < sx; x++)
                {
                    int index = rowOffset + x;
                    if (index >= block.Length || block[index] == null)
                        continue;
                    painted[(y * sx) + x] = true;
                }
            }
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        for (int y = 0; y < sy; y++)
        {
            for (int x = 0; x < sx; x++)
            {
                if (!painted[(y * sx) + x])
                    continue;

                scanTileCount++;
                int cellX = bounds.xMin + x;
                int cellY = bounds.yMin + y;
                if (cellX < minX) minX = cellX;
                if (cellX > maxX) maxX = cellX;
                if (cellY < minY) minY = cellY;
                if (cellY > maxY) maxY = cellY;
            }
        }

        if (scanTileCount == 0)
        {
            status = $"'{map.name}' não tem nenhum tile.";
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        scanMin = new Vector2Int(minX, minY);
        scanMax = new Vector2Int(maxX, maxY);
        hasScan = true;

        for (int cellY = minY; cellY <= maxY; cellY++)
        {
            for (int cellX = minX; cellX <= maxX; cellX++)
            {
                if (painted[((cellY - bounds.yMin) * sx) + (cellX - bounds.xMin)])
                    continue;

                scanHoleCount++;
                if (holes.Count < MaxHolesTracked)
                    holes.Add(new Vector3Int(cellX, cellY, 0));
            }
        }

        int w = maxX - minX + 1;
        int h = maxY - minY + 1;
        status = $"'{map.name}': {w} × {h}, {scanTileCount} tiles, {scanHoleCount} buraco(s).";

        Repaint();
        SceneView.RepaintAll();
    }

    // ────────────────────────────────────────────────────────────── cena ──

    private void OnSceneGUI(SceneView sceneView)
    {
        if (Event.current == null)
            return;

        Tilemap map = ResolveTilemap();
        if (map == null)
            return;

        // Input ANTES do gate de Repaint: clique e movimento do mouse nao chegam
        // como Repaint, e sem isto a selecao por dois cliques nunca receberia nada.
        HandlePickInput(map);

        if (Event.current.type != EventType.Repaint)
            return;

        EnsureStyles();
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        if (hasScan && showBounds)
            DrawCellRectOutline(map, scanMin.x, scanMin.y, scanMax.x, scanMax.y, new Color(0.25f, 0.85f, 1f, 0.9f));

        if (hasScan && showHoles)
            DrawHoles(map);

        if (hasScan && showRuler)
            DrawRuler(map);

        if (showRectA)
            DrawRectPreview(map, rectAOrigin, rectASize, new Color(0.35f, 1f, 0.45f, 0.95f), "Q1");

        if (showRectB)
            DrawRectPreview(map, rectBOrigin, rectBSize, new Color(1f, 0.6f, 0.15f, 0.95f), "Q2");

        if (showCellLabels)
            DrawCellLabels(map, sceneView);

        DrawPickPreview(map);
    }

    // ─────────────────────────────────────────────── selecao por 2 cliques ──

    private void BeginPick(PickTarget target)
    {
        pickTarget = target;
        hasFirstCorner = false;
        hasHoverCell = false;
        status = target == PickTarget.RectA
            ? "Q1: clique no primeiro canto no Scene."
            : "Q2: clique no primeiro canto no Scene.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void CancelPick()
    {
        pickTarget = PickTarget.None;
        hasFirstCorner = false;
        hasHoverCell = false;
        status = "Seleção cancelada.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void HandlePickInput(Tilemap map)
    {
        if (pickTarget == PickTarget.None)
            return;

        Event e = Event.current;

        // Segura o clique pra ele nao virar selecao de objeto da cena.
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            CancelPick();
            e.Use();
            return;
        }

        if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
        {
            hoverCell = GetCellUnderMouse(map, e.mousePosition);
            hasHoverCell = true;
            SceneView.RepaintAll();
            return;
        }

        if (e.type != EventType.MouseDown || e.button != 0)
            return;

        Vector3Int cell = GetCellUnderMouse(map, e.mousePosition);

        if (!hasFirstCorner)
        {
            firstCorner = cell;
            hasFirstCorner = true;
            hoverCell = cell;
            hasHoverCell = true;
            status = $"Primeiro canto: ({cell.x}, {cell.y}). Clique no segundo.";
            e.Use();
            Repaint();
            SceneView.RepaintAll();
            return;
        }

        CommitPick(firstCorner, cell);
        e.Use();
    }

    /// <summary>
    /// Dois cantos viram origem + tamanho. Como os cantos ja SAO celulas (e nao
    /// pixels), o retangulo nasce exato: toda linha tem a mesma largura, e os
    /// quatro inteiros descrevem o quadrante sem perda. E o "centro dentro" saindo
    /// de graca — nao existe celula meio-dentro pra arredondar.
    /// </summary>
    private void CommitPick(Vector3Int cornerA, Vector3Int cornerB)
    {
        int minX = Mathf.Min(cornerA.x, cornerB.x);
        int minY = Mathf.Min(cornerA.y, cornerB.y);
        int width = Mathf.Abs(cornerA.x - cornerB.x) + 1;
        int height = Mathf.Abs(cornerA.y - cornerB.y) + 1;

        if (pickTarget == PickTarget.RectA)
        {
            rectAOrigin = new Vector2Int(minX, minY);
            rectASize = new Vector2Int(width, height);
            showRectA = true;
            status = $"Q1 = ({minX}, {minY})  {width}×{height}  =  {width * height} células.";
        }
        else if (pickTarget == PickTarget.RectB)
        {
            rectBOrigin = new Vector2Int(minX, minY);
            rectBSize = new Vector2Int(width, height);
            showRectB = true;
            status = $"Q2 = ({minX}, {minY})  {width}×{height}  =  {width * height} células.";
        }

        pickTarget = PickTarget.None;
        hasFirstCorner = false;
        hasHoverCell = false;

        Repaint();
        SceneView.RepaintAll();
    }

    /// <summary>
    /// O retangulo provisorio, entre o primeiro clique e o segundo. Cinza pra
    /// deixar claro que ainda nao e o quadrante — nada foi gravado.
    /// </summary>
    private void DrawPickPreview(Tilemap map)
    {
        if (pickTarget == PickTarget.None || !hasFirstCorner || !hasHoverCell)
            return;

        int minX = Mathf.Min(firstCorner.x, hoverCell.x);
        int minY = Mathf.Min(firstCorner.y, hoverCell.y);
        int maxX = Mathf.Max(firstCorner.x, hoverCell.x);
        int maxY = Mathf.Max(firstCorner.y, hoverCell.y);

        Color provisional = new Color(0.95f, 0.95f, 0.95f, 0.85f);
        DrawCellRectOutline(map, minX, minY, maxX, maxY, provisional);

        Vector3 corner = map.GetCellCenterWorld(new Vector3Int(minX, maxY, 0));
        float step = HandleUtility.GetHandleSize(corner) * 0.6f;
        rectStyle.normal.textColor = provisional;
        Handles.Label(
            corner + new Vector3(0f, step, 0f),
            $"({minX},{minY})  {maxX - minX + 1}×{maxY - minY + 1}",
            rectStyle);
    }

    private static Vector3Int GetCellUnderMouse(Tilemap map, Vector2 mousePosition)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        Plane plane = new Plane(map.transform.forward, map.transform.position);

        Vector3 world = map.transform.position;
        if (plane.Raycast(ray, out float enter))
            world = ray.GetPoint(enter);

        Vector3Int cell = map.WorldToCell(world);
        cell.z = 0;
        return cell;
    }

    private void DrawHoles(Tilemap map)
    {
        Handles.color = new Color(1f, 0.2f, 0.2f, 1f);
        for (int i = 0; i < holes.Count; i++)
        {
            Vector3 world = map.GetCellCenterWorld(holes[i]);
            float r = HandleUtility.GetHandleSize(world) * 0.16f;
            Handles.DrawWireDisc(world, Vector3.forward, r);
            Handles.DrawLine(world + new Vector3(-r, -r, 0f), world + new Vector3(r, r, 0f));
            Handles.DrawLine(world + new Vector3(-r, r, 0f), world + new Vector3(r, -r, 0f));
        }
    }

    private void DrawRuler(Tilemap map)
    {
        // Colunas, rotuladas acima da borda de cima.
        for (int x = scanMin.x; x <= scanMax.x; x++)
        {
            if (x != scanMin.x && x != scanMax.x && ((x - scanMin.x) % rulerStep) != 0)
                continue;

            Vector3 world = map.GetCellCenterWorld(new Vector3Int(x, scanMax.y, 0));
            float step = HandleUtility.GetHandleSize(world) * 0.5f;
            Handles.Label(world + new Vector3(0f, step, 0f), x.ToString(), rulerStyle);
        }

        // Linhas, rotuladas à esquerda da borda esquerda.
        for (int y = scanMin.y; y <= scanMax.y; y++)
        {
            if (y != scanMin.y && y != scanMax.y && ((y - scanMin.y) % rulerStep) != 0)
                continue;

            Vector3 world = map.GetCellCenterWorld(new Vector3Int(scanMin.x, y, 0));
            float step = HandleUtility.GetHandleSize(world) * 0.9f;
            Handles.Label(world - new Vector3(step, 0f, 0f), y.ToString(), rulerStyle);
        }
    }

    private void DrawRectPreview(Tilemap map, Vector2Int origin, Vector2Int size, Color color, string label)
    {
        int w = Mathf.Max(1, size.x);
        int h = Mathf.Max(1, size.y);
        int maxX = origin.x + w - 1;
        int maxY = origin.y + h - 1;

        DrawCellRectOutline(map, origin.x, origin.y, maxX, maxY, color);

        Vector3 corner = map.GetCellCenterWorld(new Vector3Int(origin.x, maxY, 0));
        float step = HandleUtility.GetHandleSize(corner) * 0.6f;
        rectStyle.normal.textColor = color;
        Handles.Label(
            corner + new Vector3(0f, step, 0f),
            $"{label}  ({origin.x},{origin.y})  {w}×{h}",
            rectStyle);
    }

    /// <summary>
    /// Contorno ligando os CENTROS das celulas da borda. Sai serrilhado, e isso e
    /// o ponto: mostra onde o retangulo de CELULA realmente cai, que nao e onde
    /// "parece reto" na tela.
    /// </summary>
    private static void DrawCellRectOutline(Tilemap map, int minX, int minY, int maxX, int maxY, Color color)
    {
        if (map == null || minX > maxX || minY > maxY)
            return;

        List<Vector3> points = new List<Vector3>();

        for (int x = minX; x <= maxX; x++)
            points.Add(map.GetCellCenterWorld(new Vector3Int(x, minY, 0)));
        for (int y = minY + 1; y <= maxY; y++)
            points.Add(map.GetCellCenterWorld(new Vector3Int(maxX, y, 0)));
        for (int x = maxX - 1; x >= minX; x--)
            points.Add(map.GetCellCenterWorld(new Vector3Int(x, maxY, 0)));
        for (int y = maxY - 1; y >= minY; y--)
            points.Add(map.GetCellCenterWorld(new Vector3Int(minX, y, 0)));

        if (points.Count < 2)
            return;

        points.Add(points[0]);
        Handles.color = color;
        Handles.DrawAAPolyLine(3f, points.ToArray());
    }

    /// <summary>
    /// Rotulo por hexagono: so o que esta na tela, so com zoom suficiente, e com
    /// teto de quantidade. Sem essas tres travas, ligar isto na cena de Mundo
    /// (dezenas de milhares de celulas) congela o Editor.
    /// </summary>
    private void DrawCellLabels(Tilemap map, SceneView sceneView)
    {
        if (sceneView == null || sceneView.camera == null)
            return;

        if (!TryGetVisibleCellRange(map, sceneView, out int minX, out int minY, out int maxX, out int maxY))
            return;

        if (hasScan)
        {
            minX = Mathf.Max(minX, scanMin.x);
            minY = Mathf.Max(minY, scanMin.y);
            maxX = Mathf.Min(maxX, scanMax.x);
            maxY = Mathf.Min(maxY, scanMax.y);
        }

        long count = (long)(maxX - minX + 1) * (maxY - minY + 1);
        if (count <= 0)
            return;

        // Aviso desenhado no canto do que esta VISIVEL, senao ele nasce fora da tela
        // justamente quando o zoom esta longe — que e quando ele precisa ser lido.
        Vector3 hintAt = map.GetCellCenterWorld(new Vector3Int(minX, maxY, 0));

        if (HandleUtility.GetHandleSize(hintAt) > MinHandleSizeForCellLabels)
        {
            Handles.Label(hintAt, "(aproxime o zoom para ver as coordenadas)", rulerStyle);
            return;
        }

        if (count > cellLabelBudget)
        {
            Handles.Label(
                hintAt,
                $"({count} células na tela — acima do teto de {cellLabelBudget})",
                rulerStyle);
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                Handles.Label(map.GetCellCenterWorld(cell), $"{x},{y}", cellStyle);
            }
        }
    }

    private static bool TryGetVisibleCellRange(
        Tilemap map,
        SceneView sceneView,
        out int minX,
        out int minY,
        out int maxX,
        out int maxY)
    {
        minX = minY = maxX = maxY = 0;

        Camera cam = sceneView.camera;
        if (cam == null)
            return false;

        Plane plane = new Plane(map.transform.forward, map.transform.position);
        Vector2[] corners =
        {
            new Vector2(0f, 0f),
            new Vector2(cam.pixelWidth, 0f),
            new Vector2(0f, cam.pixelHeight),
            new Vector2(cam.pixelWidth, cam.pixelHeight)
        };

        bool any = false;
        for (int i = 0; i < corners.Length; i++)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(corners[i]);
            if (!plane.Raycast(ray, out float enter))
                continue;

            Vector3Int cell = map.WorldToCell(ray.GetPoint(enter));
            if (!any)
            {
                minX = maxX = cell.x;
                minY = maxY = cell.y;
                any = true;
                continue;
            }

            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }

        if (!any)
            return false;

        // Margem de uma celula: a projecao dos cantos erra por meio hexagono nas
        // linhas deslocadas.
        minX--; minY--; maxX++; maxY++;
        return true;
    }

    private void EnsureStyles()
    {
        if (rulerStyle == null)
        {
            rulerStyle = new GUIStyle(EditorStyles.miniBoldLabel);
            rulerStyle.normal.textColor = new Color(1f, 0.95f, 0.4f, 1f);
        }

        if (cellStyle == null)
        {
            cellStyle = new GUIStyle(EditorStyles.miniLabel);
            cellStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            cellStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (rectStyle == null)
            rectStyle = new GUIStyle(EditorStyles.miniBoldLabel);
    }

    // ────────────────────────────────────────────────────────── tilemap ──

    private void AutoDetectTilemap()
    {
        if (overrideTilemap != null)
            return;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
        {
            overrideTilemap = cursor.BoardTilemap;
            return;
        }

        // Cena de autoria nao tem CursorController nem unidade: cai no tilemap
        // com mais celulas usadas, que na pratica e o do terreno.
        overrideTilemap = FindLargestTilemapInActiveScene();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return cursor.BoardTilemap;

        return FindLargestTilemapInActiveScene();
    }

    private static Tilemap FindLargestTilemapInActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        Tilemap[] all = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Tilemap best = null;
        long bestVolume = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Tilemap map = all[i];
            if (map == null || map.gameObject.scene != active)
                continue;

            BoundsInt b = map.cellBounds;
            long volume = (long)Mathf.Max(0, b.size.x) * Mathf.Max(0, b.size.y);
            if (volume <= bestVolume)
                continue;

            bestVolume = volume;
            best = map;
        }

        return best;
    }
}
