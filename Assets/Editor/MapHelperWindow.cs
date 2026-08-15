using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Bancada de autoria do mapa. Edita o MundoData da cena de autoria aberta.
///
///   MUNDO          um asset, uma cena
///    └─ BLOCO          Europeu, America do Norte, Russia
///        └─ CAMPANHA       Europa, Africa
///            └─ QUADRANTE      Inglaterra, Franca...   ← este e assado e jogado
///
/// Os tres niveis sao RETANGULOS e sao editados pelo MESMO codigo, via INoDoMapa:
/// retangulo, destrave e continencia sao a mesma coisa em toda escala; o que muda
/// e so quem esta embaixo.
///
/// Responde tambem as perguntas de terreno que so se respondia contando hexagono
/// na unha: que caixa foi desenhada, onde faltou pintar, que coordenada e esta
/// celula.
///
/// NADA de prefab de texto: tudo e Handles, que so existe no Editor e nao suja a
/// cena. O contorno e desenhado ligando os CENTROS das celulas da borda, entao sai
/// serrilhado de proposito — num grid hexagonal com linhas deslocadas um retangulo
/// limpo em coordenada de celula NAO e um retangulo na tela, e foi exatamente
/// pintar "onde parece reto" que furou a borda do Fixture em linha sim, linha nao.
///
/// A ARVORE EDITADA E A DO ASSET, direto. Nao existe copia dentro da ferramenta:
/// copia criaria a divergencia "desenhei o retangulo mas esqueci de assar", com a
/// tela mostrando uma coisa e o arquivo guardando outra.
/// </summary>
public class MapHelperWindow : EditorWindow
{
    private const int MaxHolesTracked = 4000;
    private const float MinHandleSizeForCellLabels = 1.5f;

    private enum PickLevel
    {
        None = 0,
        Bloco = 1,
        Campanha = 2,
        Quadrante = 3
    }

    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private MundoData mundo;

    [SerializeField] private bool showBounds = true;
    [SerializeField] private bool showHoles = true;
    [SerializeField] private bool showRuler = true;
    [SerializeField] private int rulerStep = 5;
    [SerializeField] private bool showCellLabels;
    [SerializeField] private int cellLabelBudget = 400;
    [SerializeField] private bool showBlocos = true;
    [SerializeField] private bool showCampanhas = true;
    [SerializeField] private bool showQuadrantes = true;

    [SerializeField] private int splitColumns = 2;
    [SerializeField] private int splitWidth = 19;

    private int selectedBloco = -1;
    private int selectedCampanha = -1;
    private int selectedQuadrante = -1;

    // Desenho por dois cliques: entre o primeiro canto e o segundo o retangulo e
    // PROVISORIO e nada foi gravado. Mesma lei do Neutral -> provisorio ->
    // compromisso -> Neutral que rege toda acao do jogo.
    private PickLevel pickLevel;
    private int pickIndex = -1;
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
    private int overlapCellCount;

    private Vector2 scroll;
    private string status = "Clique em Recalcular para varrer o tilemap.";

    private GUIStyle rulerStyle;
    private GUIStyle cellStyle;
    private GUIStyle rectStyle;

    private BlocoData CurrentBloco =>
        mundo?.blocos != null && selectedBloco >= 0 && selectedBloco < mundo.blocos.Count
            ? mundo.blocos[selectedBloco]
            : null;

    private CampanhaData CurrentCampanha
    {
        get
        {
            BlocoData b = CurrentBloco;
            return b?.campanhas != null && selectedCampanha >= 0 && selectedCampanha < b.campanhas.Count
                ? b.campanhas[selectedCampanha]
                : null;
        }
    }

    [MenuItem("Tools/Utils/Map Helper")]
    public static void OpenWindow() => GetWindow<MapHelperWindow>("Map Helper");

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetectTilemap();
        Scan();
        RecomputeOverlap();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        CancelPickSilently();
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

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(4f);
        DrawMundoSection();

        EditorGUILayout.Space(6f);
        DrawScanReadout();

        EditorGUILayout.Space(6f);
        DrawOverlayToggles();

        EditorGUILayout.Space(6f);
        DrawArvore();

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    // ─────────────────────────────────────────────────────────────── mundo ──

    private void DrawMundoSection()
    {
        EditorGUILayout.LabelField("Mundo", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        mundo = (MundoData)EditorGUILayout.ObjectField("Asset", mundo, typeof(MundoData), false);
        if (EditorGUI.EndChangeCheck())
        {
            selectedBloco = selectedCampanha = selectedQuadrante = -1;
            CancelPickSilently();
            RecomputeOverlap();
        }

        if (mundo == null)
        {
            EditorGUILayout.HelpBox(
                "Sem Mundo. É UM asset por cena de autoria: lista os blocos que existem "
                + "nesta cena, e é por ele que o QuadranteController resolve no Play.",
                MessageType.Info);

            if (GUILayout.Button("Criar Mundo"))
                CreateMundoAsset();
            return;
        }

        EditorGUI.BeginChangeCheck();
        string id = EditorGUILayout.TextField("mundoId", mundo.mundoId);
        string nome = EditorGUILayout.TextField("displayName", mundo.displayName);
        string cena = EditorGUILayout.TextField("cena de autoria", mundo.authoringSceneName);
        Sprite foto = (Sprite)EditorGUILayout.ObjectField("foto (menu)", mundo.foto, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mundo, "Editar mundo");
            mundo.mundoId = id;
            mundo.displayName = nome;
            mundo.authoringSceneName = cena;
            mundo.foto = foto;
            EditorUtility.SetDirty(mundo);
        }

        // Assar com a cena errada aberta grava terreno vazio, e o sintoma
        // ("quadrante sem tiles") parece "bake nao rodou", nao "bake rodou no
        // lugar errado". Com o botao Assar todos, isso estragaria tudo de uma vez.
        if (!string.IsNullOrWhiteSpace(mundo.authoringSceneName)
            && !string.Equals(mundo.authoringSceneName, SceneManager.GetActiveScene().name,
                              System.StringComparison.OrdinalIgnoreCase))
        {
            EditorGUILayout.HelpBox(
                $"Este mundo é desenhado em '{mundo.authoringSceneName}' e a cena aberta é "
                + $"'{SceneManager.GetActiveScene().name}'. Assar aqui grava o terreno ERRADO.",
                MessageType.Error);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(!EditorUtility.IsDirty(mundo)))
        {
            if (GUILayout.Button("Salvar"))
            {
                AssetDatabase.SaveAssetIfDirty(mundo);
                status = $"'{mundo.name}' gravado em disco.";
            }
        }
        if (GUILayout.Button("Assar TODOS os quadrantes"))
            BakeAll();
        EditorGUILayout.EndHorizontal();

        if (EditorUtility.IsDirty(mundo))
        {
            EditorGUILayout.HelpBox(
                "Há alterações não gravadas em disco. O .asset e a tela estão diferentes.",
                MessageType.Warning);
        }

        string dup = FindDuplicated();
        if (dup != null)
            EditorGUILayout.HelpBox(dup, MessageType.Error);
    }

    private void CreateMundoAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Novo Mundo", SceneManager.GetActiveScene().name, "asset",
            "Um asset por cena de autoria.", "Assets/DB/Campanha");
        if (string.IsNullOrEmpty(path))
            return;

        MundoData created = CreateInstance<MundoData>();
        created.mundoId = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        created.displayName = System.IO.Path.GetFileNameWithoutExtension(path);
        created.authoringSceneName = SceneManager.GetActiveScene().name;

        AssetDatabase.CreateAsset(created, path);
        AssetDatabase.SaveAssets();

        mundo = created;
        selectedBloco = selectedCampanha = selectedQuadrante = -1;
        status = $"Mundo '{created.displayName}' criado em {path}.";
    }

    /// <summary>
    /// Ids repetidos sao mortais e mudos: TryGet* casa por string e devolve sempre
    /// o PRIMEIRO, entao o segundo existe no asset e e impossivel de carregar.
    /// A checagem e por MUNDO, nao por pai, porque o endereco do save nao carrega
    /// o bloco — e o que permite mover campanha de bloco sem invalidar save.
    /// </summary>
    private string FindDuplicated()
    {
        if (mundo == null)
            return null;

        HashSet<string> blocos = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        if (mundo.blocos != null)
        {
            for (int i = 0; i < mundo.blocos.Count; i++)
            {
                BlocoData b = mundo.blocos[i];
                if (b == null || string.IsNullOrWhiteSpace(b.blocoId)) continue;
                if (!blocos.Add(b.blocoId))
                    return $"blocoId '{b.blocoId}' repetido — o segundo vira inalcançável.";
            }
        }

        HashSet<string> campanhas = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (CampanhaData c in mundo.AllCampanhas())
        {
            if (string.IsNullOrWhiteSpace(c.campanhaId)) continue;
            if (!campanhas.Add(c.campanhaId))
                return $"campanhaId '{c.campanhaId}' repetido no mundo — o segundo vira inalcançável.";
        }

        HashSet<string> quadrantes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (QuadranteData q in mundo.AllQuadrantes())
        {
            if (string.IsNullOrWhiteSpace(q.quadranteId)) continue;
            if (!quadrantes.Add(q.quadranteId))
                return $"quadranteId '{q.quadranteId}' repetido no mundo — o segundo vira inalcançável.";
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────── varredura ──

    private void DrawScanReadout()
    {
        EditorGUILayout.LabelField("Caixa desenhada", EditorStyles.boldLabel);

        if (!hasScan)
        {
            EditorGUILayout.LabelField("—", "sem varredura");
            return;
        }

        int w = scanMax.x - scanMin.x + 1;
        int h = scanMax.y - scanMin.y + 1;

        EditorGUILayout.LabelField("origem", $"({scanMin.x}, {scanMin.y})");
        EditorGUILayout.LabelField("até", $"({scanMax.x}, {scanMax.y})");
        EditorGUILayout.LabelField("tamanho", $"{w} × {h}  =  {w * h} células");
        EditorGUILayout.LabelField("tiles pintados", scanTileCount.ToString());

        if (scanHoleCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{scanHoleCount} buraco(s) dentro da caixa.\n"
                + "Buraco em borda alternando linha sim/linha não é o serrilhado do grid "
                + "hexagonal: a borda foi pintada a olho, não pela coordenada.",
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
        showBlocos = EditorGUILayout.Toggle("Blocos (traço grosso)", showBlocos);
        showCampanhas = EditorGUILayout.Toggle("Campanhas (traço médio)", showCampanhas);
        showQuadrantes = EditorGUILayout.Toggle("Quadrantes (traço fino)", showQuadrantes);
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
                "Rótulo por hexágono é lupa, não vista geral: só o que está na tela, e só "
                + "com zoom suficiente. A régua é o modo que escala pro mapa grande.",
                MessageType.None);
        }
    }

    // ─────────────────────────────────────────────────────────────── árvore ──

    private void DrawArvore()
    {
        if (mundo == null)
            return;

        if (mundo.blocos == null)
        {
            mundo.blocos = new List<BlocoData>();
            EditorUtility.SetDirty(mundo);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Blocos ({mundo.blocos.Count})", EditorStyles.boldLabel);
        if (GUILayout.Button("+ Bloco", GUILayout.Width(80f)))
            AddBloco();
        EditorGUILayout.EndHorizontal();

        if (overlapCellCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{overlapCellCount} célula(s) em 2+ quadrantes. Sobreposição é recurso — é a "
                + "faixa de fronteira. Só não ponha CONSTRUÇÃO nela: peça na interseção nasce "
                + "nos dois quadrantes.",
                MessageType.None);
        }

        int removeBloco = -1;
        for (int i = 0; i < mundo.blocos.Count; i++)
        {
            BlocoData b = mundo.blocos[i];
            if (b == null) continue;
            if (DrawBlocoRow(i, b))
                removeBloco = i;
        }

        if (removeBloco >= 0)
            RemoveBloco(removeBloco);
    }

    private bool DrawBlocoRow(int index, BlocoData b)
    {
        bool selected = index == selectedBloco;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (DrawNoHeader(index, b, NivelColor(0, index), $"{b.campanhas?.Count ?? 0} campanha(s)",
                    selected, out bool remove))
            {
                selectedBloco = selected ? -1 : index;
                selectedCampanha = selectedQuadrante = -1;
                CancelPickSilently();
                SceneView.RepaintAll();
            }
            if (remove) return true;

            if (!selected)
                return false;

            DrawNoBody(b, PickLevel.Bloco, index);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Campanhas ({b.campanhas?.Count ?? 0})", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("+ Campanha", GUILayout.Width(100f)))
                AddCampanha(b);
            EditorGUILayout.EndHorizontal();

            int fora = ContarFilhosFora(b, b.campanhas);
            if (fora > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{fora} campanha(s) fora do retângulo deste bloco. Num mundo contíguo, o "
                    + "terreno que vaza pertence ao bloco VIZINHO.",
                    MessageType.Error);
            }

            if (b.campanhas == null)
                return false;

            int removeCampanha = -1;
            EditorGUI.indentLevel++;
            for (int i = 0; i < b.campanhas.Count; i++)
            {
                CampanhaData c = b.campanhas[i];
                if (c == null) continue;
                if (DrawCampanhaRow(i, c, b))
                    removeCampanha = i;
            }
            EditorGUI.indentLevel--;

            if (removeCampanha >= 0)
                RemoveCampanha(b, removeCampanha);
        }

        return false;
    }

    private bool DrawCampanhaRow(int index, CampanhaData c, BlocoData parent)
    {
        bool selected = index == selectedCampanha;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (DrawNoHeader(index, c, NivelColor(1, index), $"{c.quadrantes?.Count ?? 0} quadrante(s)",
                    selected, out bool remove))
            {
                selectedCampanha = selected ? -1 : index;
                selectedQuadrante = -1;
                CancelPickSilently();
                SceneView.RepaintAll();
            }
            if (remove) return true;

            if (!selected)
                return false;

            DrawNoBody(c, PickLevel.Campanha, index);

            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Quadrantes ({c.quadrantes?.Count ?? 0})", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("+ Quadrante", GUILayout.Width(100f)))
                AddQuadrante(c);
            EditorGUILayout.EndHorizontal();

            int fora = ContarFilhosFora(c, c.quadrantes);
            if (fora > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{fora} quadrante(s) fora do retângulo desta campanha.",
                    MessageType.Error);
            }

            DrawSplitTool(c);

            if (c.quadrantes == null)
                return false;

            int removeQ = -1;
            EditorGUI.indentLevel++;
            for (int i = 0; i < c.quadrantes.Count; i++)
            {
                QuadranteData q = c.quadrantes[i];
                if (q == null) continue;
                if (DrawQuadranteRow(i, q))
                    removeQ = i;
            }
            EditorGUI.indentLevel--;

            if (removeQ >= 0)
                RemoveQuadrante(c, removeQ);
        }

        return false;
    }

    private bool DrawQuadranteRow(int index, QuadranteData q)
    {
        bool selected = index == selectedQuadrante;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            string estado = q.HasBake ? $"assado {q.bakedTiles.Count}" : "SEM BAKE";
            if (DrawNoHeader(index, q, NivelColor(2, index), estado, selected, out bool remove))
            {
                selectedQuadrante = selected ? -1 : index;
                CancelPickSilently();
                SceneView.RepaintAll();
            }
            if (remove) return true;

            if (!selected)
                return false;

            DrawNoBody(q, PickLevel.Quadrante, index);

            EditorGUILayout.LabelField(
                "células",
                $"{q.width * q.height}"
                + (string.IsNullOrEmpty(q.bakedFromScene) ? string.Empty : $"   ·   de '{q.bakedFromScene}'"));

            if (GUILayout.Button("Assar"))
                Bake(q);
        }

        return false;
    }

    /// <returns>true se pediu foco.</returns>
    private bool DrawNoHeader(int index, INoDoMapa no, Color color, string extra, bool selected, out bool remove)
    {
        remove = false;
        bool focus = false;

        EditorGUILayout.BeginHorizontal();

        Color previous = GUI.color;
        GUI.color = color;
        EditorGUILayout.LabelField("█", GUILayout.Width(16f));
        GUI.color = previous;

        EditorGUILayout.LabelField(no.Id, EditorStyles.boldLabel, GUILayout.MinWidth(60f));
        EditorGUILayout.LabelField(extra, GUILayout.Width(110f));

        if (GUILayout.Button(selected ? "◉" : "abrir", GUILayout.Width(50f)))
            focus = true;
        if (GUILayout.Button("−", GUILayout.Width(22f)))
            remove = true;

        EditorGUILayout.EndHorizontal();
        return focus;
    }

    /// <summary>
    /// O corpo de qualquer nivel — identidade, retangulo, desenho e destrave.
    /// Um codigo so pros tres, que e o ponto do INoDoMapa.
    /// </summary>
    private void DrawNoBody(INoDoMapa no, PickLevel level, int index)
    {
        EditorGUI.BeginChangeCheck();
        string id = EditorGUILayout.TextField("id", no.Id);
        string nome = EditorGUILayout.TextField("nome", no.Nome);
        Vector2Int origin = EditorGUILayout.Vector2IntField("origem", new Vector2Int(no.OriginX, no.OriginY));
        Vector2Int size = EditorGUILayout.Vector2IntField("tamanho", new Vector2Int(no.Width, no.Height));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mundo, "Editar nó");
            no.Id = id;
            no.Nome = nome;
            no.OriginX = origin.x;
            no.OriginY = origin.y;
            no.Width = Mathf.Max(1, size.x);
            no.Height = Mathf.Max(1, size.y);
            EditorUtility.SetDirty(mundo);
            RecomputeOverlap();
            SceneView.RepaintAll();
        }

        bool picking = pickLevel == level && pickIndex == index;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(picking ? "…clicando" : "Desenhar no Scene"))
            BeginPick(level, index);
        using (new EditorGUI.DisabledScope(!hasScan))
        {
            if (GUILayout.Button("Usar a caixa desenhada"))
            {
                Undo.RecordObject(mundo, "Nó = caixa desenhada");
                no.OriginX = scanMin.x;
                no.OriginY = scanMin.y;
                no.Width = scanMax.x - scanMin.x + 1;
                no.Height = scanMax.y - scanMin.y + 1;
                EditorUtility.SetDirty(mundo);
                RecomputeOverlap();
                SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndHorizontal();

        if (picking)
        {
            EditorGUILayout.HelpBox(
                hasFirstCorner ? "Clique no segundo canto. ESC cancela."
                               : "Clique no primeiro canto no Scene. ESC cancela.",
                MessageType.Warning);
        }

        DrawDestraves(no);
    }

    /// <summary>
    /// Destrave: ids de nos que precisam estar CONCLUIDOS. "Concluido" e recursivo
    /// (campanha concluida = todos os quadrantes dela), e e isso que faz um campo
    /// so resolver os tres niveis. A avaliacao ainda nao existe — so o dado.
    /// </summary>
    private void DrawDestraves(INoDoMapa no)
    {
        List<string> lista = no.DestravadoPor;
        if (lista == null)
            return;

        EditorGUI.BeginChangeCheck();
        bool irmaos = EditorGUILayout.Toggle("exige irmãos (last map)", no.ExigeIrmaos);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(mundo, "Destrave");
            no.ExigeIrmaos = irmaos;
            EditorUtility.SetDirty(mundo);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(lista.Count == 0 ? "destravado por: (livre)" : $"destravado por ({lista.Count})");
        if (GUILayout.Button("+", GUILayout.Width(22f)))
        {
            Undo.RecordObject(mundo, "Destrave");
            lista.Add(string.Empty);
            EditorUtility.SetDirty(mundo);
        }
        EditorGUILayout.EndHorizontal();

        int removeAt = -1;
        for (int i = 0; i < lista.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string value = EditorGUILayout.TextField(lista[i]);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mundo, "Destrave");
                lista[i] = value;
                EditorUtility.SetDirty(mundo);
            }
            if (GUILayout.Button("x", GUILayout.Width(22f)))
                removeAt = i;
            EditorGUILayout.EndHorizontal();
        }

        if (removeAt >= 0)
        {
            Undo.RecordObject(mundo, "Destrave");
            lista.RemoveAt(removeAt);
            EditorUtility.SetDirty(mundo);
        }
    }

    private void DrawSplitTool(CampanhaData c)
    {
        EditorGUILayout.BeginHorizontal();
        splitColumns = Mathf.Max(1, EditorGUILayout.IntField("colunas", splitColumns));
        splitWidth = Mathf.Max(1, EditorGUILayout.IntField("largura", splitWidth));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button($"Dividir a campanha em {splitColumns} quadrante(s)"))
            SplitCampanha(c);
    }

    // ──────────────────────────────────────────────────────── mutacoes ──

    private void AddBloco()
    {
        Undo.RecordObject(mundo, "Novo bloco");
        string id = $"bloco{mundo.blocos.Count + 1}";
        BlocoData b = mundo.GetOrCreateBloco(id);
        FitToScan(b);
        EditorUtility.SetDirty(mundo);
        selectedBloco = mundo.blocos.Count - 1;
        selectedCampanha = selectedQuadrante = -1;
        SceneView.RepaintAll();
        status = $"Bloco '{id}' criado.";
    }

    private void AddCampanha(BlocoData parent)
    {
        Undo.RecordObject(mundo, "Nova campanha");
        string id = $"campanha{(parent.campanhas?.Count ?? 0) + 1}";
        CampanhaData c = parent.GetOrCreateCampanha(id);
        c.originX = parent.originX;
        c.originY = parent.originY;
        c.width = parent.width;
        c.height = parent.height;
        EditorUtility.SetDirty(mundo);
        selectedCampanha = parent.campanhas.Count - 1;
        selectedQuadrante = -1;
        RecomputeOverlap();
        SceneView.RepaintAll();
        status = $"Campanha '{id}' criada.";
    }

    private void AddQuadrante(CampanhaData parent)
    {
        Undo.RecordObject(mundo, "Novo quadrante");
        string id = $"Q{(parent.quadrantes?.Count ?? 0) + 1}";
        QuadranteData q = parent.GetOrCreateQuadrante(id);
        q.originX = parent.originX;
        q.originY = parent.originY;
        q.width = Mathf.Min(splitWidth, parent.width);
        q.height = parent.height;
        EditorUtility.SetDirty(mundo);
        selectedQuadrante = parent.quadrantes.Count - 1;
        RecomputeOverlap();
        SceneView.RepaintAll();
        status = $"Quadrante '{id}' criado.";
    }

    private void RemoveBloco(int index)
    {
        BlocoData b = mundo.blocos[index];
        if (!Confirmar($"Remover o bloco '{b.blocoId}' e as {b.campanhas?.Count ?? 0} campanha(s) dele?"))
            return;

        Undo.RecordObject(mundo, "Remover bloco");
        mundo.blocos.RemoveAt(index);
        EditorUtility.SetDirty(mundo);
        selectedBloco = selectedCampanha = selectedQuadrante = -1;
        CancelPickSilently();
        RecomputeOverlap();
        SceneView.RepaintAll();
    }

    private void RemoveCampanha(BlocoData parent, int index)
    {
        CampanhaData c = parent.campanhas[index];
        if (!Confirmar($"Remover a campanha '{c.campanhaId}' e os {c.quadrantes?.Count ?? 0} quadrante(s) dela?"))
            return;

        Undo.RecordObject(mundo, "Remover campanha");
        parent.campanhas.RemoveAt(index);
        EditorUtility.SetDirty(mundo);
        selectedCampanha = selectedQuadrante = -1;
        CancelPickSilently();
        RecomputeOverlap();
        SceneView.RepaintAll();
    }

    private void RemoveQuadrante(CampanhaData parent, int index)
    {
        QuadranteData q = parent.quadrantes[index];
        if (!Confirmar($"Remover o quadrante '{q.quadranteId}'?"))
            return;

        Undo.RecordObject(mundo, "Remover quadrante");
        parent.quadrantes.RemoveAt(index);
        EditorUtility.SetDirty(mundo);
        selectedQuadrante = -1;
        CancelPickSilently();
        RecomputeOverlap();
        SceneView.RepaintAll();
    }

    private static bool Confirmar(string mensagem) =>
        EditorUtility.DisplayDialog("Map Helper", $"{mensagem}\n\nDá pra desfazer com Ctrl+Z.", "Remover", "Cancelar");

    private void FitToScan(INoDoMapa no)
    {
        if (!hasScan) return;
        no.OriginX = scanMin.x;
        no.OriginY = scanMin.y;
        no.Width = scanMax.x - scanMin.x + 1;
        no.Height = scanMax.y - scanMin.y + 1;
    }

    /// <summary>
    /// Encosta o primeiro na borda esquerda da campanha e o ultimo na direita. Se a
    /// soma das larguras passar, a diferenca vira SOBREPOSICAO — que e a faixa de
    /// fronteira que se quer, e o melhor lugar pra ela e em cima de uma feicao
    /// geografica (serra, lago).
    /// </summary>
    private void SplitCampanha(CampanhaData c)
    {
        int n = Mathf.Max(1, splitColumns);
        int w = Mathf.Max(1, splitWidth);
        int maxX = c.originX + c.width - 1;

        Undo.RecordObject(mundo, "Dividir campanha");

        for (int i = 0; i < n; i++)
        {
            QuadranteData q = c.GetOrCreateQuadrante($"Q{i + 1}");
            q.originX = n == 1
                ? c.originX
                : Mathf.RoundToInt(Mathf.Lerp(c.originX, maxX - w + 1, i / (float)(n - 1)));
            q.originY = c.originY;
            q.width = w;
            q.height = c.height;
        }

        EditorUtility.SetDirty(mundo);
        RecomputeOverlap();
        SceneView.RepaintAll();

        int total = n * w;
        status = total > c.width
            ? $"{n} quadrante(s) de {w}×{c.height} numa campanha de {c.width} → {total - c.width} coluna(s) de sobreposição."
            : total == c.width
                ? $"{n} quadrante(s) cobrem a campanha exatamente."
                : $"{n} quadrante(s) deixam {c.width - total} coluna(s) descoberta(s).";
    }

    private static int ContarFilhosFora<T>(INoDoMapa pai, List<T> filhos) where T : INoDoMapa
    {
        if (filhos == null) return 0;

        int paiMaxX = pai.OriginX + Mathf.Max(1, pai.Width) - 1;
        int paiMaxY = pai.OriginY + Mathf.Max(1, pai.Height) - 1;
        int fora = 0;

        for (int i = 0; i < filhos.Count; i++)
        {
            INoDoMapa f = filhos[i];
            if (f == null) continue;
            int fMaxX = f.OriginX + Mathf.Max(1, f.Width) - 1;
            int fMaxY = f.OriginY + Mathf.Max(1, f.Height) - 1;
            if (f.OriginX < pai.OriginX || f.OriginY < pai.OriginY || fMaxX > paiMaxX || fMaxY > paiMaxY)
                fora++;
        }

        return fora;
    }

    private void RecomputeOverlap()
    {
        overlapCellCount = 0;
        if (mundo == null) return;

        long budget = 0;
        foreach (QuadranteData q in mundo.AllQuadrantes())
        {
            budget += (long)q.width * q.height;
            if (budget > 500000L) return;
        }

        Dictionary<Vector2Int, int> claims = new Dictionary<Vector2Int, int>();
        foreach (QuadranteData q in mundo.AllQuadrantes())
        {
            for (int y = 0; y < q.height; y++)
            {
                for (int x = 0; x < q.width; x++)
                {
                    Vector2Int cell = new Vector2Int(q.originX + x, q.originY + y);
                    claims.TryGetValue(cell, out int count);
                    claims[cell] = count + 1;
                    if (count == 1) overlapCellCount++;
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────── bake ──

    /// <summary>
    /// Le o retangulo da cena de autoria ABERTA e grava no asset. Guarda TileBase
    /// direto: o jogo ja resolve terreno a partir do tile
    /// (TerrainDatabase.TryGetByPaletteTile), entao uma tabela de traducao no meio
    /// so criaria uma segunda fonte pra divergir.
    /// </summary>
    private void Bake(QuadranteData q)
    {
        if (mundo == null || q == null) return;

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            status = "Bake abortado: nenhum tilemap nesta cena.";
            return;
        }

        int w = Mathf.Max(1, q.width);
        int h = Mathf.Max(1, q.height);

        Undo.RecordObject(mundo, $"Assar {q.quadranteId}");
        q.bakedFromScene = SceneManager.GetActiveScene().name;
        q.bakedAtUtcTicks = System.DateTime.UtcNow.Ticks;

        if (q.bakedTiles == null)
            q.bakedTiles = new List<TileBase>(w * h);
        q.bakedTiles.Clear();

        int painted = 0, holeCount = 0;

        // Row-major, y crescendo — mesma ordem que QuadranteData.GetBakedTile espera.
        for (int localY = 0; localY < h; localY++)
        {
            for (int localX = 0; localX < w; localX++)
            {
                TileBase tile = map.GetTile(new Vector3Int(q.originX + localX, q.originY + localY, 0));
                q.bakedTiles.Add(tile);
                if (tile == null) holeCount++; else painted++;
            }
        }

        EditorUtility.SetDirty(mundo);
        AssetDatabase.SaveAssetIfDirty(mundo);

        status = $"'{q.quadranteId}' assado de '{q.bakedFromScene}': ({q.originX},{q.originY}) "
               + $"{w}×{h} = {w * h} células · {painted} tiles, {holeCount} buraco(s).";
        Repaint();
    }

    private void BakeAll()
    {
        if (mundo == null) return;

        int n = 0;
        foreach (QuadranteData q in mundo.AllQuadrantes())
        {
            Bake(q);
            n++;
        }

        status = $"{n} quadrante(s) assado(s) de '{SceneManager.GetActiveScene().name}'.";
    }

    // ────────────────────────────────────────────────────────────── cena ──

    private void OnSceneGUI(SceneView sceneView)
    {
        if (Event.current == null) return;

        Tilemap map = ResolveTilemap();
        if (map == null) return;

        // Input ANTES do gate de Repaint: clique e movimento nao chegam como
        // Repaint, e sem isto a selecao por dois cliques nunca receberia nada.
        HandlePickInput(map);

        if (Event.current.type != EventType.Repaint) return;

        EnsureStyles();
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        if (hasScan && showBounds)
            DrawCellRectOutline(map, scanMin.x, scanMin.y, scanMax.x, scanMax.y, new Color(0.25f, 0.85f, 1f, 0.9f), 3f);

        if (hasScan && showHoles)
            DrawHoles(map);

        if (hasScan && showRuler)
            DrawRuler(map);

        DrawArvoreNaCena(map);

        if (showCellLabels)
            DrawCellLabels(map, sceneView);

        DrawPickPreview(map);
    }

    private void DrawArvoreNaCena(Tilemap map)
    {
        if (mundo?.blocos == null) return;

        for (int bi = 0; bi < mundo.blocos.Count; bi++)
        {
            BlocoData b = mundo.blocos[bi];
            if (b == null) continue;

            bool blocoAtivo = selectedBloco < 0 || selectedBloco == bi;

            if (showBlocos)
                DrawNo(map, b, NivelColor(0, bi), 7f, "BLOCO", blocoAtivo);

            if (b.campanhas == null) continue;

            for (int ci = 0; ci < b.campanhas.Count; ci++)
            {
                CampanhaData c = b.campanhas[ci];
                if (c == null) continue;

                bool campAtiva = blocoAtivo && (selectedCampanha < 0 || selectedCampanha == ci);

                if (showCampanhas)
                    DrawNo(map, c, NivelColor(1, ci), 4.5f, "campanha", campAtiva);

                if (!showQuadrantes || c.quadrantes == null) continue;

                for (int qi = 0; qi < c.quadrantes.Count; qi++)
                {
                    QuadranteData q = c.quadrantes[qi];
                    if (q == null) continue;
                    DrawNo(map, q, NivelColor(2, qi), 2.5f, string.Empty,
                           campAtiva && (selectedQuadrante < 0 || selectedQuadrante == qi));
                }
            }
        }
    }

    private void DrawNo(Tilemap map, INoDoMapa no, Color color, float thickness, string prefixo, bool ativo)
    {
        if (!ativo) color.a *= 0.25f;

        int maxX = no.OriginX + Mathf.Max(1, no.Width) - 1;
        int maxY = no.OriginY + Mathf.Max(1, no.Height) - 1;
        DrawCellRectOutline(map, no.OriginX, no.OriginY, maxX, maxY, color, thickness);

        Vector3 corner = map.GetCellCenterWorld(new Vector3Int(no.OriginX, maxY, 0));
        float step = HandleUtility.GetHandleSize(corner) * (0.6f + thickness * 0.08f);
        rectStyle.normal.textColor = color;
        Handles.Label(
            corner + new Vector3(0f, step, 0f),
            $"{(string.IsNullOrEmpty(prefixo) ? string.Empty : prefixo + " ")}{no.Id}  "
            + $"({no.OriginX},{no.OriginY})  {no.Width}x{no.Height}",
            rectStyle);
    }

    /// <summary>Cor estavel por nivel e indice — angulo aureo, vizinhos nunca colidem.</summary>
    private static Color NivelColor(int nivel, int index)
    {
        float hue = Mathf.Repeat(index * 0.618034f + nivel * 0.31f, 1f);
        float sat = nivel == 0 ? 0.45f : nivel == 1 ? 0.62f : 0.78f;
        return Color.HSVToRGB(hue, sat, 1f);
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
        for (int x = scanMin.x; x <= scanMax.x; x++)
        {
            if (x != scanMin.x && x != scanMax.x && ((x - scanMin.x) % rulerStep) != 0) continue;
            Vector3 world = map.GetCellCenterWorld(new Vector3Int(x, scanMax.y, 0));
            Handles.Label(world + new Vector3(0f, HandleUtility.GetHandleSize(world) * 0.5f, 0f),
                          x.ToString(), rulerStyle);
        }

        for (int y = scanMin.y; y <= scanMax.y; y++)
        {
            if (y != scanMin.y && y != scanMax.y && ((y - scanMin.y) % rulerStep) != 0) continue;
            Vector3 world = map.GetCellCenterWorld(new Vector3Int(scanMin.x, y, 0));
            Handles.Label(world - new Vector3(HandleUtility.GetHandleSize(world) * 0.9f, 0f, 0f),
                          y.ToString(), rulerStyle);
        }
    }

    /// <summary>
    /// Contorno ligando os CENTROS das celulas da borda. Sai serrilhado, e isso e o
    /// ponto: mostra onde o retangulo de CELULA realmente cai, que nao e onde
    /// "parece reto" na tela.
    /// </summary>
    private static void DrawCellRectOutline(
        Tilemap map, int minX, int minY, int maxX, int maxY, Color color, float thickness)
    {
        if (map == null || minX > maxX || minY > maxY) return;

        List<Vector3> points = new List<Vector3>();
        for (int x = minX; x <= maxX; x++)
            points.Add(map.GetCellCenterWorld(new Vector3Int(x, minY, 0)));
        for (int y = minY + 1; y <= maxY; y++)
            points.Add(map.GetCellCenterWorld(new Vector3Int(maxX, y, 0)));
        for (int x = maxX - 1; x >= minX; x--)
            points.Add(map.GetCellCenterWorld(new Vector3Int(x, maxY, 0)));
        for (int y = maxY - 1; y >= minY; y--)
            points.Add(map.GetCellCenterWorld(new Vector3Int(minX, y, 0)));

        if (points.Count < 2) return;

        points.Add(points[0]);
        Handles.color = color;
        Handles.DrawAAPolyLine(thickness, points.ToArray());
    }

    /// <summary>
    /// Rotulo por hexagono: so o que esta na tela, so com zoom suficiente, e com
    /// teto de quantidade. Sem essas tres travas, ligar isto no mundo inteiro
    /// congela o Editor.
    /// </summary>
    private void DrawCellLabels(Tilemap map, SceneView sceneView)
    {
        if (sceneView == null || sceneView.camera == null) return;
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
        if (count <= 0) return;

        Vector3 hintAt = map.GetCellCenterWorld(new Vector3Int(minX, maxY, 0));

        if (HandleUtility.GetHandleSize(hintAt) > MinHandleSizeForCellLabels)
        {
            Handles.Label(hintAt, "(aproxime o zoom para ver as coordenadas)", rulerStyle);
            return;
        }

        if (count > cellLabelBudget)
        {
            Handles.Label(hintAt, $"({count} células na tela — acima do teto de {cellLabelBudget})", rulerStyle);
            return;
        }

        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
                Handles.Label(map.GetCellCenterWorld(new Vector3Int(x, y, 0)), $"{x},{y}", cellStyle);
    }

    private static bool TryGetVisibleCellRange(
        Tilemap map, SceneView sceneView, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = minY = maxX = maxY = 0;

        Camera cam = sceneView.camera;
        if (cam == null) return false;

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
            if (!plane.Raycast(ray, out float enter)) continue;

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

        if (!any) return false;

        // Margem de uma celula: a projecao dos cantos erra por meio hexagono nas
        // linhas deslocadas.
        minX--; minY--; maxX++; maxY++;
        return true;
    }

    // ─────────────────────────────────────────────── selecao por 2 cliques ──

    private void BeginPick(PickLevel level, int index)
    {
        pickLevel = level;
        pickIndex = index;
        hasFirstCorner = false;
        hasHoverCell = false;
        status = "Clique no primeiro canto no Scene.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void CancelPickSilently()
    {
        pickLevel = PickLevel.None;
        pickIndex = -1;
        hasFirstCorner = false;
        hasHoverCell = false;
    }

    private void CancelPick()
    {
        CancelPickSilently();
        status = "Seleção cancelada.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void HandlePickInput(Tilemap map)
    {
        if (pickLevel == PickLevel.None) return;

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

        if (e.type != EventType.MouseDown || e.button != 0) return;

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
    /// pixels), o retangulo nasce exato: toda linha tem a mesma largura, e os quatro
    /// inteiros descrevem o no sem perda. E o "centro dentro" saindo de graca — nao
    /// existe celula meio-dentro pra arredondar.
    /// </summary>
    private void CommitPick(Vector3Int a, Vector3Int b)
    {
        INoDoMapa alvo = ResolvePickTarget();
        if (alvo == null)
        {
            CancelPick();
            return;
        }

        int minX = Mathf.Min(a.x, b.x);
        int minY = Mathf.Min(a.y, b.y);
        int w = Mathf.Abs(a.x - b.x) + 1;
        int h = Mathf.Abs(a.y - b.y) + 1;

        Undo.RecordObject(mundo, "Desenhar nó");
        alvo.OriginX = minX;
        alvo.OriginY = minY;
        alvo.Width = w;
        alvo.Height = h;
        EditorUtility.SetDirty(mundo);

        bool bakeVelho = alvo is QuadranteData q && q.HasBake;
        status = $"'{alvo.Id}' = ({minX}, {minY})  {w}×{h}  =  {w * h} células. "
               + (bakeVelho ? "O bake ficou VELHO — asse de novo." : string.Empty);

        CancelPickSilently();
        RecomputeOverlap();
        Repaint();
        SceneView.RepaintAll();
    }

    private INoDoMapa ResolvePickTarget()
    {
        switch (pickLevel)
        {
            case PickLevel.Bloco:
                return mundo?.blocos != null && pickIndex >= 0 && pickIndex < mundo.blocos.Count
                    ? mundo.blocos[pickIndex]
                    : null;

            case PickLevel.Campanha:
            {
                BlocoData b = CurrentBloco;
                return b?.campanhas != null && pickIndex >= 0 && pickIndex < b.campanhas.Count
                    ? b.campanhas[pickIndex]
                    : null;
            }

            case PickLevel.Quadrante:
            {
                CampanhaData c = CurrentCampanha;
                return c?.quadrantes != null && pickIndex >= 0 && pickIndex < c.quadrantes.Count
                    ? (INoDoMapa)c.quadrantes[pickIndex]
                    : null;
            }
        }

        return null;
    }

    /// <summary>
    /// O retangulo provisorio, entre o primeiro clique e o segundo. Cinza pra
    /// deixar claro que ainda nao e o no — nada foi gravado.
    /// </summary>
    private void DrawPickPreview(Tilemap map)
    {
        if (pickLevel == PickLevel.None || !hasFirstCorner || !hasHoverCell) return;

        int minX = Mathf.Min(firstCorner.x, hoverCell.x);
        int minY = Mathf.Min(firstCorner.y, hoverCell.y);
        int maxX = Mathf.Max(firstCorner.x, hoverCell.x);
        int maxY = Mathf.Max(firstCorner.y, hoverCell.y);

        Color provisional = new Color(0.95f, 0.95f, 0.95f, 0.85f);
        DrawCellRectOutline(map, minX, minY, maxX, maxY, provisional, 3f);

        Vector3 corner = map.GetCellCenterWorld(new Vector3Int(minX, maxY, 0));
        rectStyle.normal.textColor = provisional;
        Handles.Label(
            corner + new Vector3(0f, HandleUtility.GetHandleSize(corner) * 0.6f, 0f),
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

    // ─────────────────────────────────────────────────────────── varredura ──

    /// <summary>
    /// Varre o tilemap UMA vez e guarda o resultado. Nao roda por frame de
    /// proposito: no mundo inteiro isso e dezenas de milhares de celulas, e varrer
    /// no OnSceneGUI arrastaria o Editor.
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
                    if (index >= block.Length || block[index] == null) continue;
                    painted[(y * sx) + x] = true;
                }
            }
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int y = 0; y < sy; y++)
        {
            for (int x = 0; x < sx; x++)
            {
                if (!painted[(y * sx) + x]) continue;

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
                if (painted[((cellY - bounds.yMin) * sx) + (cellX - bounds.xMin)]) continue;
                scanHoleCount++;
                if (holes.Count < MaxHolesTracked)
                    holes.Add(new Vector3Int(cellX, cellY, 0));
            }
        }

        status = $"'{map.name}': {maxX - minX + 1} × {maxY - minY + 1}, "
               + $"{scanTileCount} tiles, {scanHoleCount} buraco(s).";

        Repaint();
        SceneView.RepaintAll();
    }

    // ──────────────────────────────────────────────────────────── tilemap ──

    private void AutoDetectTilemap()
    {
        if (overrideTilemap != null) return;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
        {
            overrideTilemap = cursor.BoardTilemap;
            return;
        }

        overrideTilemap = FindLargestTilemapInActiveScene();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null) return overrideTilemap;

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
            if (map == null || map.gameObject.scene != active) continue;

            BoundsInt b = map.cellBounds;
            long volume = (long)Mathf.Max(0, b.size.x) * Mathf.Max(0, b.size.y);
            if (volume <= bestVolume) continue;

            bestVolume = volume;
            best = map;
        }

        return best;
    }
}
