using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Publicação > Checklist: o build sai sem os trem do debug?
// Ideia trazida do Checklist da Mi'Larhys (Tools > Mylarhis > Checklist), com uma diferença:
// lá o JSON lembra o que FOI PEDIDO; aqui a janela LÊ o valor de verdade nas cenas do build,
// porque esquecer o debug ligado não deixa rastro em lugar nenhum.
//
// "Preparar para publicar" grava o valor de antes em UserSettings/ (local, fora do Git) e
// "Voltar ao debug" restaura exatamente aquilo — o autor desenvolve com o debug ligado.
// Cena que já estava aberta com alterações NÃO salvas só é marcada, nunca salva por aqui.
public class PublicacaoChecklist : EditorWindow
{
    const string BackupPath = "UserSettings/PublicacaoChecklist.json";
    const string ManualPath = "UserSettings/PublicacaoChecklistManual.json";

    sealed class Item
    {
        public Type Component;
        public string Property;
        public bool Expected;
        public string Label;
        public string Why;
    }

    [Serializable] class Saved { public string scene; public string component; public string path; public string property; public bool value; }
    [Serializable] class SavedList { public string when; public List<Saved> itens = new List<Saved>(); }
    [Serializable] class ManualList { public List<string> feitos = new List<string>(); }

    sealed class Finding
    {
        public Item Item;
        public string Scene;
        public string ObjectPath;
        public bool Value;
        public bool Ok => Value == Item.Expected;
    }

    static readonly Item[] Items =
    {
        // ---- o que o jogador SENTE ----
        new Item { Component = typeof(AIController), Property = "startOnPause", Expected = false,
            Label = "IA nasce pausada (Start On Pause)",
            Why = "A IA começa parada, como se tivesse apertado F10 — o jogador acha que o jogo travou. Vale até depois de carregar save." },
        new Item { Component = typeof(DebugManager), Property = "aiDebugShortcutsEnabled", Expected = false,
            Label = "Atalhos da IA (F10/F11/F12)",
            Why = "F10 pausa a IA, F11 anda um passo, F12 retoma. Um F10 sem querer e a partida 'trava'." },
        new Item { Component = typeof(DebugManager), Property = "debugShortcutsEnabled", Expected = false,
            Label = "Atalhos do Panel_Debug (' ; crase Ctrl+D)",
            Why = "Abre o painel de desenvolvedor no meio da partida." },
        new Item { Component = typeof(DebugManager), Property = "panelRodadaDesativadoPvpDebug", Expected = false,
            Label = "Pular apresentação da rodada no hot seat",
            Why = "Sem o Panel_Rodada, o hot seat não avisa de quem é a vez." },
        new Item { Component = typeof(AIController), Property = "showAIUnitHUD", Expected = false,
            Label = "HUD da IA sobre as unidades",
            Why = "Plano, badge e eixo desenhados em cima das peças inimigas — entrega a intenção da IA." },
        new Item { Component = typeof(DebugManager), Property = "gravarDiagnosticoNoSave", Expected = true,
            Label = "Diagnóstico no save (manter LIGADO no playtest)",
            Why = "Não aparece pro jogador. Se ela mandar o .tmrsave, o Save Inspector lê o que a IA decidiu." },

        // ---- logs: no build Web cada Debug.Log custa caro ----
        new Item { Component = typeof(AIController), Property = "showAILogs", Expected = false, Label = "Logs da IA", Why = "Console cheio e lentidão, principalmente na Web." },
        new Item { Component = typeof(MatchController), Property = "enableTurnPerfLogs", Expected = false, Label = "Logs de perf do turno", Why = "Log por turno." },
        new Item { Component = typeof(MatchController), Property = "enableFogSourceDebugLogs", Expected = false, Label = "Logs de fonte da névoa", Why = "Log por refresh de névoa." },
        new Item { Component = typeof(MatchController), Property = "enableFogStepPerfLogs", Expected = false, Label = "Logs de perf da névoa", Why = "Log por passo." },
        new Item { Component = typeof(MatchController), Property = "enableFogValidationLogs", Expected = false, Label = "Logs de validação da névoa", Why = "Log por refresh." },
        new Item { Component = typeof(MatchController), Property = "enableSensorsRuntimeLogs", Expected = false, Label = "Logs dos sensores", Why = "Log por sensor." },
        new Item { Component = typeof(MatchController), Property = "enablePodeDetectarRuntimeLogs", Expected = false, Label = "Logs do PodeDetectar", Why = "Log por detecção." },
        new Item { Component = typeof(MatchController), Property = "enablePodeEnxergarRuntimeLogs", Expected = false, Label = "Logs do PodeEnxergar", Why = "Log por visão." },
        new Item { Component = typeof(PathManager), Property = "enablePathfindingDebugLogs", Expected = false, Label = "Logs de pathfinding", Why = "Log por busca de caminho." },
        new Item { Component = typeof(TurnStateManager), Property = "enableTurnStateRuntimeLogs", Expected = false, Label = "Logs do TurnState", Why = "Log por transição." },
        new Item { Component = typeof(TurnStateManager), Property = "showMovementLogs", Expected = false, Label = "Logs de movimento", Why = "Log por passo." },
        new Item { Component = typeof(TurnStateManager), Property = "enableRangeCacheDebugLogs", Expected = false, Label = "Logs do cache de alcance", Why = "Log por consulta." },
        new Item { Component = typeof(TurnStateManager), Property = "showFrameSpikeLogs", Expected = false, Label = "Logs de frame spike", Why = "Log por pico." },
        new Item { Component = typeof(TurnStateManager), Property = "showClickSpikeLogs", Expected = false, Label = "Logs de click spike", Why = "Log por clique." },
        new Item { Component = typeof(ReplayManager), Property = "enableReplayRuntimeLogs", Expected = false, Label = "Logs do replay", Why = "Log por ação." },
        new Item { Component = typeof(SaveGameManager), Property = "verboseLogs", Expected = false, Label = "Logs verbosos do save", Why = "Log por save/load." },
        new Item { Component = typeof(SectorManager), Property = "sectorLog", Expected = false, Label = "Logs de setor", Why = "Log por setor." },
    };

    // O que a janela não consegue ler sozinha: o autor marca.
    static readonly (string id, string label, string why)[] Manual =
    {
        ("web-sem-lto", "Web: Code Optimization SEM 'Runtime Speed with LTO'",
            "Com LTO o Carregar Jogo dá 'RuntimeError: unreachable'. Sem LTO funciona."),
        ("jogar-uma-partida", "Jogar 1 turno no build gerado (não no Editor)",
            "Compilar e rodar no Editor não prova o build; o crash da Campanha no celular só apareceu lá."),
    };

    // ---- Texturas Web (prática da Mi'Larhys: Tools > Mylarhis > Apply Web Overrides) ----
    // Override SO na plataforma Web; PC e Editor seguem com a qualidade cheia. A convenção
    // veio do autor (97a05b7, no Inspector): 512 + ASTC 6x6 em unidades, construções e
    // tiles — DXT é formato de PC, e no celular cada textura era descomprimida no processador.
    // Feito à mão, a convenção vazava: town.png entrou depois (9fa44f9) já sem override.
    //   512   o que aparece do tamanho de um hex ou menor (peça, prédio, ícone, munição)
    //   1024  o que ocupa boa parte da tela (retrato, logo, mapa, cena de rodada) e as
    //         folhas de animação, onde cada quadro é só uma fatia da imagem
    // Override que o autor JÁ marcou não é tocado (escolha manual vale mais que a regra), e
    // áudio fica de fora: um menu que reescreve o import do áudio desfaz a escolha dele a
    // cada execução — a armadilha que a Mi'Larhys anotou.
    const string WebPlatform = "WebGL";
    static readonly (string folder, int max)[] WebRules =
    {
        ("Assets/img/unidades", 512), ("Assets/img/construcoes", 512), ("Assets/img/tiles", 512),
        ("Assets/img/icones", 512), ("Assets/img/municao", 512), ("Assets/img/armas", 512),
        ("Assets/img/cursor", 512),
        ("Assets/img/animations", 1024), ("Assets/img/generais", 1024), ("Assets/img/logo", 1024),
        ("Assets/img/maps", 1024), ("Assets/Videos/PanelRodada", 1024), ("Assets/help", 1024),
    };

    sealed class WebPending { public string Path; public int Max; }
    readonly List<WebPending> webPending = new List<WebPending>();
    int webManual;
    bool webScanned;

    readonly List<Finding> findings = new List<Finding>();
    readonly List<string> missing = new List<string>();
    string status = "Clique em Verificar.";
    Vector2 scroll;

    [MenuItem("Tools/Publicação/Checklist", priority = 0)]
    static void Open() => GetWindow<PublicacaoChecklist>("Checklist de Publicação");

    static List<string> BuildScenes() =>
        EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();

    void OnGUI()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorGUILayout.HelpBox("Saia do Play: a checagem abre e grava cenas.", MessageType.Info);
            return;
        }

        // Da Mi'Larhys: o build sai do que está no DISCO. Cena aberta com alteração não salva
        // gera um build diferente do que se vê no Editor.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isDirty)
                EditorGUILayout.HelpBox($"'{open.name}' tem alterações NÃO SALVAS — o build usa o que está no disco. Ctrl+S antes.",
                    MessageType.Warning);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Verificar")) Scan();
            GUI.enabled = findings.Any(f => !f.Ok);
            if (GUILayout.Button("Preparar para publicar")) Apply();
            GUI.enabled = File.Exists(BackupPath);
            if (GUILayout.Button("Voltar ao debug")) Restore();
            GUI.enabled = true;
        }
        EditorGUILayout.LabelField(status, EditorStyles.miniLabel);
        if (File.Exists(BackupPath))
        {
            var backup = JsonUtility.FromJson<SavedList>(File.ReadAllText(BackupPath));
            EditorGUILayout.HelpBox($"Debug guardado em {backup?.when} ({backup?.itens.Count ?? 0} valor(es)). "
                + "\"Voltar ao debug\" restaura exatamente aqueles.", MessageType.None);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        // Build: o que não mora em cena.
        EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);
        DrawLine(!EditorUserBuildSettings.development, "Development Build desligado",
            "Development Build mostra o console de erros na tela e é mais lento.");
        EditorGUILayout.LabelField("Cenas no build: " + string.Join(" · ", BuildScenes().Select(Path.GetFileNameWithoutExtension)),
            EditorStyles.wordWrappedMiniLabel);

        foreach (string m in missing)
            EditorGUILayout.HelpBox(m, MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Texturas Web", EditorStyles.boldLabel);
        if (!webScanned)
            EditorGUILayout.LabelField("Clique em Verificar.", EditorStyles.miniLabel);
        else
        {
            DrawLine(webPending.Count == 0,
                webPending.Count == 0
                    ? "Toda textura das pastas da regra tem override Web"
                    : $"{webPending.Count} textura(s) sem override Web",
                "Sem override, o build Web leva a textura no tamanho e formato do PC: pesa na memória do celular e é descomprimida no processador.");
            if (webManual > 0)
                EditorGUILayout.LabelField($"      {webManual} com override manual diferente da regra — mantidos.", EditorStyles.miniLabel);
            foreach (WebPending p in webPending.Take(12))
                EditorGUILayout.LabelField($"      {p.Max}  {p.Path}", EditorStyles.miniLabel);
            if (webPending.Count > 12)
                EditorGUILayout.LabelField($"      … e mais {webPending.Count - 12}", EditorStyles.miniLabel);
            GUI.enabled = webPending.Count > 0;
            if (GUILayout.Button("Aplicar overrides Web (permanente, não volta com o debug)", GUILayout.Width(380)))
                ApplyWebOverrides();
            GUI.enabled = true;
        }

        foreach (var group in findings.GroupBy(f => f.Scene))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(group.Key), EditorStyles.boldLabel);
            foreach (Finding f in group)
                DrawLine(f.Ok, $"{f.Item.Label}  —  {f.Item.Component.Name}.{f.Item.Property} = {f.Value}"
                    + (f.Ok ? "" : $" (esperado {f.Item.Expected})"), f.Item.Why);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Manual", EditorStyles.boldLabel);
        ManualList manual = LoadManual();
        foreach (var (id, label, why) in Manual)
        {
            bool done = manual.feitos.Contains(id);
            bool now = EditorGUILayout.ToggleLeft(label, done);
            EditorGUILayout.LabelField("      " + why, EditorStyles.wordWrappedMiniLabel);
            if (now != done)
            {
                if (now) manual.feitos.Add(id); else manual.feitos.Remove(id);
                Directory.CreateDirectory(Path.GetDirectoryName(ManualPath));
                File.WriteAllText(ManualPath, JsonUtility.ToJson(manual, true));
            }
        }
        if (GUILayout.Button("Desmarcar os manuais (novo build)", GUILayout.Width(240)))
            File.Delete(ManualPath);

        EditorGUILayout.EndScrollView();
    }

    static void DrawLine(bool ok, string text, string why)
    {
        var style = new GUIStyle(EditorStyles.label)
        { normal = { textColor = ok ? new Color(0.4f, 0.8f, 0.4f) : new Color(1f, 0.7f, 0.2f) }, wordWrap = true };
        EditorGUILayout.LabelField((ok ? "✓ " : "⚠ ") + text, style);
        if (!ok && !string.IsNullOrEmpty(why))
            EditorGUILayout.LabelField("      " + why, EditorStyles.wordWrappedMiniLabel);
    }

    static ManualList LoadManual() =>
        File.Exists(ManualPath) ? JsonUtility.FromJson<ManualList>(File.ReadAllText(ManualPath)) ?? new ManualList() : new ManualList();

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // Abre cada cena do build (aditiva, se não estiver aberta), entrega ao visitante e fecha
    // de novo a que abriu. Visitante devolve true se mudou algo: a cena é salva se não tinha
    // alteração pendente antes; se tinha, só fica marcada (o autor decide o Ctrl+S).
    void ForEachBuildScene(Func<Scene, bool> visit, List<string> notes)
    {
        foreach (string path in BuildScenes())
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            bool wasDirty = wasLoaded && scene.isDirty;
            if (!wasLoaded)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                bool changed = visit(scene);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!wasDirty) EditorSceneManager.SaveScene(scene);
                    else notes?.Add($"{scene.name} já tinha alterações não salvas: marquei, mas NÃO salvei. Ctrl+S nela.");
                }
            }
            finally
            {
                if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    IEnumerable<(Component c, Item item, SerializedProperty p, SerializedObject so)> Props(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Item item in Items)
                foreach (Component c in root.GetComponentsInChildren(item.Component, true))
                {
                    var so = new SerializedObject(c);
                    SerializedProperty p = so.FindProperty(item.Property);
                    if (p == null || p.propertyType != SerializedPropertyType.Boolean)
                    {
                        missing.Add($"{item.Component.Name}.{item.Property} não existe mais (renomeado?) — conferir o checklist.");
                        continue;
                    }
                    yield return (c, item, p, so);
                }
    }

    void Scan()
    {
        findings.Clear();
        missing.Clear();
        ForEachBuildScene(scene =>
        {
            foreach (var (c, item, p, _) in Props(scene))
                findings.Add(new Finding { Item = item, Scene = scene.path, ObjectPath = PathOf(c.transform), Value = p.boolValue });
            return false;
        }, null);
        missing.Sort();
        ScanWebTextures();
        int bad = findings.Count(f => !f.Ok) + (webPending.Count > 0 ? 1 : 0);
        status = $"verificado {DateTime.Now:HH:mm} — " + (bad == 0 ? "tudo pronto ✓" : $"{bad} item(ns) fora do lugar");
    }

    static IEnumerable<(TextureImporter importer, string path, int max)> WebTargets()
    {
        foreach (var (folder, max) in WebRules)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                    yield return (importer, path, max);
            }
        }
    }

    void ScanWebTextures()
    {
        webPending.Clear();
        webManual = 0;
        foreach (var (importer, path, max) in WebTargets())
        {
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebPlatform);
            if (!web.overridden)
                webPending.Add(new WebPending { Path = path, Max = max });
            else if (web.maxTextureSize != max)
                webManual++;
        }
        webPending.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        webScanned = true;
    }

    void ApplyWebOverrides()
    {
        int done = 0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var (importer, path, max) in WebTargets())
            {
                TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings(WebPlatform);
                if (web.overridden) continue; // escolha manual do autor: não mexe
                web.overridden = true;
                web.maxTextureSize = max;
                web.format = TextureImporterFormat.ASTC_6x6; // a convenção de 97a05b7
                importer.SetPlatformTextureSettings(web);
                importer.SaveAndReimport();
                done++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        ScanWebTextures();
        status = $"overrides Web aplicados em {done} textura(s). Áudio não é tocado.";
    }

    void Apply()
    {
        if (!EditorUtility.DisplayDialog("Preparar para publicar",
                "Desliga o debug nas cenas do build e salva. O valor de antes fica guardado para \"Voltar ao debug\".",
                "Preparar", "Cancelar"))
            return;
        var backup = new SavedList { when = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        var notes = new List<string>();
        ForEachBuildScene(scene =>
        {
            bool changed = false;
            foreach (var (c, item, p, so) in Props(scene))
            {
                if (p.boolValue == item.Expected) continue;
                backup.itens.Add(new Saved { scene = scene.path, component = item.Component.Name, path = PathOf(c.transform), property = item.Property, value = p.boolValue });
                p.boolValue = item.Expected;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            return changed;
        }, notes);
        if (backup.itens.Count > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
        }
        Scan();
        status += notes.Count > 0 ? "  |  " + string.Join("  ", notes) : "";
    }

    void Restore()
    {
        var backup = JsonUtility.FromJson<SavedList>(File.ReadAllText(BackupPath));
        if (backup == null) return;
        var notes = new List<string>();
        int restored = 0;
        ForEachBuildScene(scene =>
        {
            bool changed = false;
            foreach (var (c, item, p, so) in Props(scene))
            {
                Saved s = backup.itens.FirstOrDefault(b => b.scene == scene.path && b.component == item.Component.Name
                    && b.path == PathOf(c.transform) && b.property == item.Property);
                if (s == null || p.boolValue == s.value) continue;
                p.boolValue = s.value;
                so.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
                restored++;
            }
            return changed;
        }, notes);
        File.Delete(BackupPath);
        Scan();
        status = $"debug restaurado ({restored} valor(es))" + (notes.Count > 0 ? "  |  " + string.Join("  ", notes) : "");
    }
}
