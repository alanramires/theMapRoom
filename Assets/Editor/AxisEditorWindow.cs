using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Tools > Axis Editor. Edita os eixos autorados de UM quadrante, direto no QuadranteData
// do MundoData — a unica verdade dos eixos. Nao depende de clicar predio na cena.
//
// Por que existe: o bloco "Eixo do quadrante" no Inspector da construcao escreve um setor
// por clique e nao mostra a fila inteira. Ordem errada (rally no meio, zigue-zague) so
// aparecia jogando. Aqui a fila do slot fica a vista, em ordem, e se arruma com botoes.
//
// Fonte dos setores e dos rallies: na cena de autoria do mundo, os predios DA CENA dentro
// do retangulo do quadrante (ao vivo); fora dela, o bake. O bake e foto e fica velho.
//
// Selecionar o quadrante liga o mesmo recorte de autoria do Map Helper (SectorManager e
// InvasionAxisMap), para o Scene View desenhar o quadrante certo. Fechar a janela solta.
// =====================================================================================
public class AxisEditorWindow : EditorWindow
{
    private MundoData mundo;
    private int quadranteIndex = -1;
    private Vector2 scroll;
    private readonly Dictionary<int, bool> slotFoldout = new Dictionary<int, bool>();
    private readonly Dictionary<int, bool> slotDesenho = new Dictionary<int, bool>();
    private bool setClip;
    private QuadranteData quadranteAtual;
    private UnityEngine.Tilemaps.Tilemap tilemap;

    // Uma cor por eixo, na ordem: as tres do debug "show eixos" (1 vermelho, 2 laranja,
    // 3 grafite) e depois outras distintas para quem tiver mais frentes.
    private static readonly Color[] CoresEixo =
    {
        new Color(0.92f, 0.16f, 0.16f), new Color(1f, 0.56f, 0.08f), new Color(0.25f, 0.25f, 0.28f),
        new Color(0.20f, 0.55f, 1f), new Color(0.60f, 0.25f, 0.85f), new Color(0.10f, 0.70f, 0.40f),
    };

    [MenuItem("Tools/Axis Editor")]
    public static void OpenWindow() => GetWindow<AxisEditorWindow>("Axis Editor");

    private void OnEnable()
    {
        if (mundo == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:MundoData");
            if (guids.Length > 0)
                mundo = AssetDatabase.LoadAssetAtPath<MundoData>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.RepaintAll();
        if (!setClip) return;
        // So solta o recorte se ainda for o nosso: o Map Helper pode ter assumido.
        if (InvasionAxisMap.AuthoringQuadrante == quadranteAtual)
        {
            SectorManager.ClearAuthoringClip();
            InvasionAxisMap.AuthoringQuadrante = null;
        }
        setClip = false;
    }

    private void OnGUI()
    {
        mundo = (MundoData)EditorGUILayout.ObjectField("Mundo", mundo, typeof(MundoData), false);
        if (mundo == null)
        {
            EditorGUILayout.HelpBox("Escolha um MundoData.", MessageType.Info);
            return;
        }

        var quadrantes = new List<QuadranteData>(mundo.AllQuadrantes());
        if (quadrantes.Count == 0)
        {
            EditorGUILayout.HelpBox("Este mundo nao tem quadrantes.", MessageType.Info);
            return;
        }

        var nomes = new string[quadrantes.Count];
        for (int i = 0; i < quadrantes.Count; i++)
            nomes[i] = $"{quadrantes[i].displayName} ({quadrantes[i].quadranteId})";
        // Primeira abertura: segue o quadrante que o Map Helper ja estiver usando.
        if (quadranteIndex < 0)
            quadranteIndex = Mathf.Max(0, quadrantes.IndexOf(InvasionAxisMap.AuthoringQuadrante));
        quadranteIndex = Mathf.Clamp(quadranteIndex, 0, quadrantes.Count - 1);
        int escolhido = EditorGUILayout.Popup("Quadrante", quadranteIndex, nomes);
        QuadranteData q = quadrantes[escolhido];

        // RECORTE SO NA TROCA. Reaplicar a cada repaint brigava com o Map Helper (que faz
        // o mesmo com o quadrante DELE): cada janela desfazia a outra e cada troca refaz o
        // SectorManager inteiro — o "loop comendo processamento".
        if (escolhido != quadranteIndex || quadranteAtual != q)
        {
            quadranteIndex = escolhido;
            quadranteAtual = q;
            AplicarRecorte(q);
        }

        AtualizarPecas(q);
        EditorGUILayout.LabelField("Prédios lidos de: " + fontePecas, EditorStyles.miniLabel);

        if (InvasionAxisMap.AuthoringQuadrante != q)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(
                "Outra janela (Map Helper) esta recortando outro quadrante. O desenho desta janela segue " +
                "funcionando; o SectorManager segue a outra.", MessageType.Warning);
            if (GUILayout.Button("Usar este", GUILayout.Width(80), GUILayout.Height(38)))
                AplicarRecorte(q);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Desenhar no Scene View:", GUILayout.Width(150));
        foreach (int slot in ColetarSlots(q))
        {
            bool atual = !slotDesenho.TryGetValue(slot, out bool v) || v;
            bool novo = GUILayout.Toggle(atual, $"slot {slot}", GUILayout.Width(70));
            if (novo != atual) { slotDesenho[slot] = novo; SceneView.RepaintAll(); }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Ordem = ordem de avanco, do QG para fora. O ULTIMO setor e o rally. " +
            "Na cena de autoria, setores e rallies vem da propria cena; fora dela, do bake (re-asse para atualizar). " +
            "Ctrl+S salva o mundo.", MessageType.None);

        if (q.eixos == null)
            q.eixos = new List<EixoAutorado>();

        var setoresDoQuadrante = ColetarSetores(q);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (int slot in ColetarSlots(q))
            DesenharSlot(q, slot, setoresDoQuadrante);
        EditorGUILayout.EndScrollView();
    }

    private void AplicarRecorte(QuadranteData q)
    {
        InvasionAxisMap.AuthoringQuadrante = q;
        SectorManager.SetAuthoringClip(new RectInt(q.originX, q.originY, Mathf.Max(1, q.width), Mathf.Max(1, q.height)));
        setClip = true;
        SceneView.RepaintAll();
    }

    // ───────────────────────────────────────────────────────── Scene View ──
    // Bolinha numerada em cada setor, na ordem da fila, e a linha do QG ate o rally.
    // O ponto do setor e o centro dos predios ASSADOS dele — o mesmo dado da lista.
    private void OnSceneGUI(SceneView view)
    {
        QuadranteData q = quadranteAtual;
        if (q == null || q.eixos == null) return;
        UnityEngine.Tilemaps.Tilemap map = ResolverTilemap();
        if (map == null) return;
        AtualizarPecas(q);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        foreach (int slot in ColetarSlots(q))
        {
            if (slotDesenho.TryGetValue(slot, out bool desenha) && !desenha) continue;
            var doSlot = new List<EixoAutorado>();
            q.CollectEixosDoSlot(slot, doSlot);
            bool temQg = TryPontoDoQG(q, slot, map, out Vector3 qg);

            for (int e = 0; e < doSlot.Count; e++)
            {
                Color cor = CoresEixo[e % CoresEixo.Length];
                var pontos = new List<Vector3>();
                if (temQg) pontos.Add(qg);
                foreach (ConstructionSector s in doSlot[e].caminho)
                    if (TryPontoDoSetor(q, s, map, out Vector3 p)) pontos.Add(p);

                Handles.color = cor;
                if (pontos.Count >= 2)
                    Handles.DrawAAPolyLine(5f, pontos.ToArray());

                float raio = HandleUtility.GetHandleSize(temQg ? qg : (pontos.Count > 0 ? pontos[0] : Vector3.zero)) * 0.12f;
                if (temQg)
                {
                    Handles.DrawWireDisc(qg, Vector3.forward, raio * 1.4f);
                }
                for (int i = 0; i < doSlot[e].caminho.Count; i++)
                {
                    ConstructionSector s = doSlot[e].caminho[i];
                    if (!TryPontoDoSetor(q, s, map, out Vector3 p)) continue;
                    bool ultimo = i == doSlot[e].caminho.Count - 1;
                    Handles.color = cor;
                    Handles.DrawSolidDisc(p, Vector3.forward, raio);
                    if (ultimo)
                    {
                        Handles.color = Color.white;
                        Handles.DrawWireDisc(p, Vector3.forward, raio * 1.6f);
                    }
                    var estilo = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = cor } };
                    Handles.Label(p + new Vector3(raio, raio, 0f),
                        $"S{slot} E{e + 1}.{i + 1} {ConstructionSectorHelper.GetBadge(s)}{(ultimo ? " (fim)" : "")}", estilo);
                }
            }
        }
    }

    private UnityEngine.Tilemaps.Tilemap ResolverTilemap()
    {
        if (tilemap != null) return tilemap;
        CursorController cursor = Object.FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
            return tilemap = cursor.BoardTilemap;
        UnityEngine.Tilemaps.Tilemap maior = null;
        int area = -1;
        foreach (var t in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsInactive.Exclude))
        {
            t.CompressBounds();
            int a = t.cellBounds.size.x * t.cellBounds.size.y;
            if (a > area) { area = a; maior = t; }
        }
        return tilemap = maior;
    }

    // ─────────────────────────────────────────── de onde vem cada predio ──
    // Na cena de AUTORIA do mundo (a Fixture), a verdade e a cena: o bake e so uma foto,
    // e fica velho quando se mexe num rally depois de assar (o dono do rally de Juliet
    // estava na cena e nao no bake). Fora dela, o bake e o unico dado que existe.
    private struct Peca
    {
        public ConstructionSector sector;
        public Vector3Int cell;      // celula de campanha (mundo)
        public int slot;
        public bool isHQ;
        public bool isRally;
        public List<int> owners;
    }

    private static List<Peca> pecas = new List<Peca>();
    private static string fontePecas = "";
    private static QuadranteData pecasDe;
    private static double pecasEm;

    private static bool CenaDeAutoriaAberta(MundoData mundo)
    {
        return mundo != null && !string.IsNullOrEmpty(mundo.authoringSceneName)
            && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == mundo.authoringSceneName;
    }

    // Refaz no maximo a cada meio segundo: OnGUI e OnSceneGUI chamam o tempo todo.
    private void AtualizarPecas(QuadranteData q)
    {
        if (q == pecasDe && EditorApplication.timeSinceStartup - pecasEm < 0.5) return;
        pecasDe = q;
        pecasEm = EditorApplication.timeSinceStartup;
        pecas = new List<Peca>();

        if (CenaDeAutoriaAberta(mundo))
        {
            foreach (ConstructionManager c in Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude))
            {
                if (c == null) continue;
                Vector3Int cell = c.CurrentCellPosition; cell.z = 0;
                if (!q.ContainsCampaignCell(cell)) continue;
                pecas.Add(new Peca
                {
                    sector = c.Sector, cell = cell, slot = c.SlotIndex, isHQ = c.IsPlayerHeadQuarter,
                    isRally = c.IsRallyPoint, owners = new List<int>(c.RallyOwnerSlots)
                });
            }
            fontePecas = "cena (ao vivo)";
            return;
        }

        if (q.bakedConstrucoes != null)
            foreach (ConstrucaoAssada c in q.bakedConstrucoes)
            {
                if (c == null) continue;
                pecas.Add(new Peca
                {
                    sector = c.sector, cell = new Vector3Int(q.originX + c.localX, q.originY + c.localY, 0),
                    slot = c.slotIndex, isHQ = c.siteRuntime != null && c.siteRuntime.isPlayerHeadQuarter,
                    isRally = c.isRallyPoint, owners = c.rallyOwnerSlots ?? new List<int>()
                });
            }
        fontePecas = "bake (abra a cena de autoria para ver ao vivo)";
    }

    private static bool TryPontoDoSetor(QuadranteData q, ConstructionSector s, UnityEngine.Tilemaps.Tilemap map, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        int n = 0;
        foreach (Peca p in pecas)
            if (p.sector == s) { ponto += map.GetCellCenterWorld(p.cell); n++; }
        if (n == 0) return false;
        ponto /= n;
        return true;
    }

    private static bool TryPontoDoQG(QuadranteData q, int slot, UnityEngine.Tilemaps.Tilemap map, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        foreach (Peca p in pecas)
            if (p.isHQ && p.slot == slot) { ponto = map.GetCellCenterWorld(p.cell); return true; }
        return false;
    }

    // Slots que tem QG no quadrante, mais os que ja tem eixo gravado.
    private static List<int> ColetarSlots(QuadranteData q)
    {
        var slots = new SortedSet<int>();
        foreach (Peca p in pecas)
            if (p.isHQ && p.slot >= 0) slots.Add(p.slot);
        if (q.eixos != null)
            foreach (EixoAutorado e in q.eixos)
                if (e != null && e.slotIndex >= 0) slots.Add(e.slotIndex);
        if (slots.Count == 0) { slots.Add(0); slots.Add(1); }
        return new List<int>(slots);
    }

    // Setores fora de base presentes no quadrante, em ordem do enum.
    private static List<ConstructionSector> ColetarSetores(QuadranteData q)
    {
        var set = new SortedSet<ConstructionSector>();
        foreach (Peca p in pecas)
            if (p.sector != ConstructionSector.None && !ConstructionSectorHelper.IsBase(p.sector))
                set.Add(p.sector);
        return new List<ConstructionSector>(set);
    }

    // Rally do slot no setor: rally com o slot entre os donos, ou rally sem dono (vale para todos).
    private static bool EhRallyDoSlot(QuadranteData q, int slot, ConstructionSector setor)
    {
        foreach (Peca p in pecas)
        {
            if (p.sector != setor || !p.isRally) continue;
            if (p.owners == null || p.owners.Count == 0 || p.owners.Contains(slot))
                return true;
        }
        return false;
    }
    private void DesenharSlot(QuadranteData q, int slot, List<ConstructionSector> setoresDoQuadrante)
    {
        if (!slotFoldout.TryGetValue(slot, out bool aberto)) aberto = true;
        aberto = EditorGUILayout.Foldout(aberto, $"Slot {slot}", true, EditorStyles.foldoutHeader);
        slotFoldout[slot] = aberto;
        if (!aberto) return;

        var doSlot = new List<EixoAutorado>();
        q.CollectEixosDoSlot(slot, doSlot);

        // Setor em mais de um eixo do mesmo slot: o jogo le o primeiro, o resto e lixo.
        var usados = new Dictionary<ConstructionSector, int>();
        foreach (EixoAutorado e in doSlot)
            foreach (ConstructionSector s in e.caminho)
                usados[s] = usados.TryGetValue(s, out int n) ? n + 1 : 1;

        EditorGUI.indentLevel++;
        for (int e = 0; e < doSlot.Count; e++)
            DesenharEixo(q, slot, e + 1, doSlot[e], setoresDoQuadrante, usados);

        if (GUILayout.Button($"+ Novo eixo para o slot {slot}"))
        {
            Undo.RecordObject(mundo, "Novo eixo");
            q.eixos.Add(new EixoAutorado { slotIndex = slot });
            EditorUtility.SetDirty(mundo);
        }

        var livres = new List<ConstructionSector>();
        foreach (ConstructionSector s in setoresDoQuadrante)
            if (!usados.ContainsKey(s)) livres.Add(s);
        if (livres.Count > 0)
        {
            var nomesLivres = new List<string>();
            foreach (ConstructionSector s in livres) nomesLivres.Add(Rotulo(q, slot, s));
            EditorGUILayout.LabelField("   fora de eixo: " + string.Join(", ", nomesLivres), EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6f);
    }

    private void DesenharEixo(QuadranteData q, int slot, int numero, EixoAutorado eixo,
        List<ConstructionSector> setoresDoQuadrante, Dictionary<ConstructionSector, int> usados)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"E{numero}", EditorStyles.boldLabel, GUILayout.Width(40));
        EditorGUILayout.LabelField(Resumo(eixo), EditorStyles.miniLabel);
        if (GUILayout.Button("Rally pro fim", GUILayout.Width(95)))
            Mudar("Rally pro fim", () => MoverRallyParaOFim(q, slot, eixo));
        if (GUILayout.Button("Apagar eixo", GUILayout.Width(90)))
        {
            Mudar("Apagar eixo", () => q.eixos.Remove(eixo));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUIUtility.ExitGUI();
        }
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < eixo.caminho.Count; i++)
        {
            ConstructionSector s = eixo.caminho[i];
            EditorGUILayout.BeginHorizontal();
            string aviso = "";
            if (EhRallyDoSlot(q, slot, s) && i != eixo.caminho.Count - 1) aviso += "  ⚠ rally fora do fim";
            if (usados.TryGetValue(s, out int n) && n > 1) aviso += "  ⚠ em outro eixo tambem";
            if (!setoresDoQuadrante.Contains(s)) aviso += "  ⚠ sem predio no quadrante";
            EditorGUILayout.LabelField($"{i + 1}. {Rotulo(q, slot, s)}{aviso}");

            int idx = i;
            using (new EditorGUI.DisabledScope(idx == 0))
                if (GUILayout.Button("▲", GUILayout.Width(26)))
                    Mudar("Subir setor", () => Trocar(eixo.caminho, idx, idx - 1));
            using (new EditorGUI.DisabledScope(idx == eixo.caminho.Count - 1))
                if (GUILayout.Button("▼", GUILayout.Width(26)))
                    Mudar("Descer setor", () => Trocar(eixo.caminho, idx, idx + 1));
            if (GUILayout.Button("✕", GUILayout.Width(26)))
            {
                Mudar("Tirar setor", () => eixo.caminho.RemoveAt(idx));
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        // Adicionar: so setores que ainda nao estao em nenhum eixo deste slot.
        var opcoes = new List<ConstructionSector>();
        foreach (ConstructionSector s in setoresDoQuadrante)
            if (!usados.ContainsKey(s)) opcoes.Add(s);
        if (opcoes.Count > 0)
        {
            var labels = new List<string> { "+ adicionar setor no fim..." };
            foreach (ConstructionSector s in opcoes) labels.Add(Rotulo(q, slot, s));
            int escolhido = EditorGUILayout.Popup(0, labels.ToArray());
            if (escolhido > 0)
            {
                ConstructionSector novo = opcoes[escolhido - 1];
                Mudar("Adicionar setor", () => eixo.caminho.Add(novo));
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void Mudar(string nome, System.Action acao)
    {
        Undo.RecordObject(mundo, nome);
        acao();
        EditorUtility.SetDirty(mundo);
        SceneView.RepaintAll();
    }

    private static void Trocar(List<ConstructionSector> lista, int a, int b)
    {
        (lista[a], lista[b]) = (lista[b], lista[a]);
    }

    // Ordem estavel: os comuns mantem a ordem do autor; rallies do slot vao para o fim.
    private static void MoverRallyParaOFim(QuadranteData q, int slot, EixoAutorado eixo)
    {
        var comuns = new List<ConstructionSector>();
        var rallies = new List<ConstructionSector>();
        foreach (ConstructionSector s in eixo.caminho)
            (EhRallyDoSlot(q, slot, s) ? rallies : comuns).Add(s);
        eixo.caminho.Clear();
        eixo.caminho.AddRange(comuns);
        eixo.caminho.AddRange(rallies);
    }

    private static string Rotulo(QuadranteData q, int slot, ConstructionSector s)
    {
        return $"{ConstructionSectorHelper.GetBadge(s)} {s}{(EhRallyDoSlot(q, slot, s) ? " (rally)" : "")}";
    }

    private static string Resumo(EixoAutorado eixo)
    {
        var partes = new List<string> { "base" };
        foreach (ConstructionSector s in eixo.caminho) partes.Add(ConstructionSectorHelper.GetBadge(s));
        return string.Join(" → ", partes);
    }
}
