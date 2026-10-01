using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Save Inspector — le um .tmrsave e mostra o estado salvo sem precisar
/// extrair o zip a mao.
///
/// Le pelo MESMO caminho do load (SaveGameManager.TryReadSaveForAudit): se um
/// campo nao aparece aqui, o jogo tambem nao o carregaria. So leitura; nao toca
/// em cena, nao carrega o save, nao altera arquivo.
///
/// Nasceu do guia de modding do autor (§11: "ofereca uma ferramenta oficial para
/// inspecionar saves"). Ler o save 2 da v8.6.1 exigiu descobrir que o .tmrsave e
/// um zip e escrever um script; esta janela e o atalho para isso.
/// </summary>
public sealed class SaveInspectorWindow : EditorWindow
{
    private sealed class SaveEntry
    {
        public string Path;
        public string Name;
        public DateTime Modified;
        public int Slot = int.MaxValue;
        public string Label;
    }

    private readonly List<SaveEntry> saves = new List<SaveEntry>();
    private string selectedPath;
    private SaveGameManager.SaveAuditRead read;
    private string status = "Escolha um save na lista ou abra um arquivo.";
    private Vector2 listScroll;
    private Vector2 scroll;
    private bool showDeadUnits;

    private bool foldFile = true;
    private bool foldMatch = true;
    private bool foldPlayers = true;
    private bool foldAI = true;
    private bool foldUnits = true;
    private bool foldConstructions = true;
    private bool foldPlans = true;
    private bool foldProgress = true;
    private bool foldBriefing;

    [MenuItem("Tools/Auditoria/Save Inspector")]
    public static void Open() =>
        GetWindow<SaveInspectorWindow>("Save Inspector").Show();

    private void OnEnable() => RefreshList();

    // ------------------------------------------------------------------
    // Lista de saves
    // ------------------------------------------------------------------

    private void RefreshList()
    {
        saves.Clear();
        foreach (string directory in CollectSaveDirectories())
        {
            string[] files;
            try { files = Directory.GetFiles(directory, "*.tmrsave"); }
            catch { continue; }
            foreach (string file in files)
            {
                if (saves.Any(s => string.Equals(s.Path, file, StringComparison.OrdinalIgnoreCase)))
                    continue;
                saves.Add(DescribeEntry(file));
            }
        }
        // Por SLOT, como o jogador pensa nos saves. Ordenar por data fazia o
        // "segundo da lista" ser o slot 1 — e foi assim que o slot errado foi aberto.
        saves.Sort((a, b) => a.Slot != b.Slot
            ? a.Slot.CompareTo(b.Slot)
            : b.Modified.CompareTo(a.Modified));
    }

    // O rotulo vem do MANIFESTO, nao do nome do arquivo: o nome carrega a cena em
    // que o slot foi criado, e um save da Campanha pode se chamar "Battle Map".
    private static SaveEntry DescribeEntry(string file)
    {
        var entry = new SaveEntry
        {
            Path = file,
            Name = Path.GetFileNameWithoutExtension(file),
            Modified = File.GetLastWriteTime(file)
        };

        System.Text.RegularExpressions.Match slotMatch =
            System.Text.RegularExpressions.Regex.Match(entry.Name, @"_slot(\d+)");
        if (slotMatch.Success && int.TryParse(slotMatch.Groups[1].Value, out int slot))
            entry.Slot = slot;
        string slotText = entry.Slot == int.MaxValue ? "slot ?" : $"slot {entry.Slot}";

        if (SaveGameManager.TryReadSaveManifestForAudit(
                file, out string scene, out string map, out long ticks, out _))
        {
            DateTime when = ticks > 0
                ? new DateTime(ticks, DateTimeKind.Utc).ToLocalTime()
                : entry.Modified;
            string mapText = string.IsNullOrEmpty(map) || map == scene ? string.Empty : $" · {map}";
            entry.Label = $"{slotText} · {scene}{mapText} · {when:dd/MM HH:mm}";
        }
        else
        {
            entry.Label = $"{slotText} · (manifesto ilegível) · {entry.Name}";
        }
        return entry;
    }

    // Onde o jogo grava: o persistentDataPath e o diretorio de cada
    // SaveGameManager das cenas abertas (que pode ser customizado por cena).
    private static IEnumerable<string> CollectSaveDirectories()
    {
        var directories = new List<string> { Application.persistentDataPath };
        foreach (SaveGameManager manager in FindObjectsByType<SaveGameManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string directory = null;
            try { directory = manager.GetResolvedSaveDirectory(); }
            catch { }
            if (!string.IsNullOrWhiteSpace(directory))
                directories.Add(directory);
        }
        return directories
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void Load(string path)
    {
        selectedPath = path;
        read = null;
        if (!SaveGameManager.TryReadSaveForAudit(path, out read, out string error))
        {
            status = $"Falha ao ler: {error}";
            read = null;
        }
        else
        {
            status = $"Lido: {Path.GetFileName(path)}";
        }
        Repaint();
    }

    // ------------------------------------------------------------------
    // GUI
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Save Inspector", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Le o save pelo mesmo caminho do load. So leitura: nao carrega a partida "
            + "nem altera o arquivo.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Atualizar lista"))
            RefreshList();
        if (GUILayout.Button("Abrir arquivo..."))
        {
            string picked = EditorUtility.OpenFilePanel(
                "Abrir save", Application.persistentDataPath, "tmrsave");
            if (!string.IsNullOrEmpty(picked))
                Load(picked);
        }
        using (new EditorGUI.DisabledScope(read == null))
        {
            if (GUILayout.Button("Exportar JSON"))
                ExportJson();
        }
        EditorGUILayout.EndHorizontal();

        DrawSaveList();
        EditorGUILayout.HelpBox(status, MessageType.None);

        if (read?.data == null)
            return;

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawFile();
        DrawMatch();
        DrawPlayers();
        DrawAI();
        DrawUnits();
        DrawConstructions();
        DrawPlans();
        DrawProgress();
        DrawBriefing();
        EditorGUILayout.EndScrollView();
    }

    private void DrawSaveList()
    {
        if (saves.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhum .tmrsave nas pastas de save conhecidas.");
            return;
        }

        listScroll = EditorGUILayout.BeginScrollView(
            listScroll, GUILayout.Height(Mathf.Min(140f, 22f * saves.Count + 6f)));
        foreach (SaveEntry entry in saves)
        {
            bool isSelected = string.Equals(entry.Path, selectedPath, StringComparison.OrdinalIgnoreCase);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = isSelected ? new Color(0.6f, 0.85f, 1f) : Color.white;
            if (GUILayout.Button(new GUIContent(entry.Label, entry.Path), GUILayout.ExpandWidth(true)))
                Load(entry.Path);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawFile()
    {
        foldFile = EditorGUILayout.Foldout(foldFile, "Arquivo", true);
        if (!foldFile)
            return;

        SaveGameData d = read.data;
        EditorGUI.indentLevel++;
        Row("Caminho", read.path);
        Row("Salvo em", read.savedAtUtcTicks > 0
            ? new DateTime(read.savedAtUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            : "(sem data)");
        Row("Versão", $"save {read.saveVersion} (dados {d.version}) · container {read.containerVersion}");
        Row("Tamanho", $"{read.containerBytes / 1024f:F1} KB no disco · game.json {read.gameJsonBytes / 1024f:F1} KB");
        Row("Replay / jogadas", $"{(read.hasReplay ? "sim" : "não")} / {(read.hasJogadas ? "sim" : "não")}");
        Row("Hash do estado", string.IsNullOrEmpty(read.stateHash) ? "(save antigo, sem hash)" : read.stateHash);
        EditorGUI.indentLevel--;
    }

    private void DrawMatch()
    {
        foldMatch = EditorGUILayout.Foldout(foldMatch, "Partida", true);
        if (!foldMatch)
            return;

        SaveGameData d = read.data;
        EditorGUI.indentLevel++;
        Row("Cena", d.sceneName);
        Row("Mapa", d.mapDisplayName);
        if (d.battleMap != null && !string.IsNullOrEmpty(d.battleMap.quadranteId))
        {
            Row("Endereço", $"{d.battleMap.mundoId} / {d.battleMap.campanhaId} / {d.battleMap.quadranteId} (serial {d.battleMap.quadranteSerial})");
            Row("Registra na campanha", d.battleMap.recordsCampaignResult ? "sim" : "não");
        }
        // currentTurn so sobe quando a vez volta ao primeiro jogador: conta RODADAS.
        Row("Rodada", d.currentTurn.ToString());
        Row("Vez de", $"slot {d.activeSlotIndex} ({TeamName(d.activeTeamId)})");
        Row("Vencedor", d.hasVictoryWinner
            ? $"slot {d.victoryWinnerSlotIndex} ({TeamName(d.victoryWinnerTeamId)})"
            : "nenhum ainda");
        EditorGUI.indentLevel--;
    }

    private void DrawPlayers()
    {
        foldPlayers = EditorGUILayout.Foldout(foldPlayers, $"Jogadores ({read.data.players.Count})", true);
        if (!foldPlayers)
            return;

        Header(("slot", 40), ("cor", 90), ("quem", 120), ("caixa", 70), ("renda", 70), ("caixa inicial", 90));
        foreach (MatchPlayerSaveData p in read.data.players)
        {
            string who = p.isAI ? "IA" : p.isLocal ? "humano local" : "humano remoto";
            Cells((p.slotIndex.ToString(), 40), (TeamName(p.teamId), 90), (who, 120),
                (p.actualMoney.ToString(), 70), (p.incomePerTurn.ToString(), 70),
                (p.startMoney.ToString(), 90));
        }
    }

    private void DrawAI()
    {
        foldAI = EditorGUILayout.Foldout(foldAI, "IA — dificuldade e perfil", true);
        if (!foldAI)
            return;

        SaveGameData d = read.data;
        EditorGUI.indentLevel++;
        if (!d.aiDifficultySaved)
        {
            EditorGUILayout.HelpBox(
                "Save sem dificuldade gravada (antigo): no load valem os flags serializados na cena.",
                MessageType.Warning);
            EditorGUI.indentLevel--;
            return;
        }

        AIDifficulty difficulty = AIController.InferDifficulty(d.aiEasyMode, d.aiHardMode);
        Row("Dificuldade", $"{AIPresetCatalog.RotuloDoJogador(difficulty)} (enum {difficulty})");
        Row("Perfil que o load usaria", ResolveCatalogPreset(difficulty));
        Row("Flags salvos", $"easy={On(d.aiEasyMode)} hard={On(d.aiHardMode)} "
            + $"conscrição perdendo={On(d.aiConscriptionWhenLosing)} sempre={On(d.aiConscriptionDoctrine)}");
        Row("Fase de Massacre", On(d.aiMassacrePhase));
        EditorGUILayout.HelpBox(
            "O perfil é resolvido pelo catálogo ATUAL, não pelo do dia do save. Com catálogo, "
            + "os toggles do perfil mandam; os flags de conscrição salvos só valem na reserva "
            + "(sem catálogo).",
            MessageType.None);
        EditorGUI.indentLevel--;
    }

    private static string ResolveCatalogPreset(AIDifficulty difficulty)
    {
        string[] guids = AssetDatabase.FindAssets("t:AIPresetCatalog");
        if (guids.Length == 0)
            return "nenhum catálogo no projeto — baseline + overlay";
        var catalog = AssetDatabase.LoadAssetAtPath<AIPresetCatalog>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
        if (catalog != null && catalog.TryGetPreset(difficulty, out AIPresetData preset))
            return $"{preset.name} (catálogo {catalog.name})";
        return "catálogo sem entrada para esta dificuldade — baseline + overlay";
    }

    private void DrawUnits()
    {
        List<UnitSaveData> units = read.data.units
            .Where(u => showDeadUnits || !u.isDead)
            .OrderBy(u => u.slotIndex).ThenBy(u => u.instanceId)
            .ToList();
        int dead = read.data.units.Count(u => u.isDead);

        foldUnits = EditorGUILayout.Foldout(
            foldUnits, $"Unidades ({read.data.units.Count - dead} vivas, {dead} mortas)", true);
        if (!foldUnits)
            return;

        showDeadUnits = EditorGUILayout.ToggleLeft("Mostrar mortas", showDeadUnits);
        foreach (IGrouping<int, UnitSaveData> group in units.GroupBy(u => u.slotIndex))
        {
            EditorGUILayout.LabelField($"slot {group.Key}", EditorStyles.miniBoldLabel);
            Header(("id", 40), ("unidade", 130), ("hex", 70), ("HP", 40), ("comb.", 50), ("agiu", 40), ("situação", 160), ("plano", 90));
            foreach (UnitSaveData u in group)
            {
                string situation = u.isDead
                    ? $"morta (rodada {u.deadWhenTurn})"
                    : u.isEmbarked
                        ? $"embarcada em #{u.transporterInstanceId}"
                        : u.isUnderRepair ? "em reparo" : "";
                Cells((u.instanceId.ToString(), 40), (u.unitId, 130), ($"({u.cellX},{u.cellY})", 70),
                    (u.currentHP.ToString(), 40), (u.currentFuel.ToString(), 50),
                    (u.hasActed ? "sim" : "", 40), (situation, 160),
                    (u.aiHasAssignedPlan ? u.aiAssignedPlanName : "", 90));
            }
        }
    }

    private void DrawConstructions()
    {
        List<ConstructionSaveData> constructions = read.data.constructions;
        int inCapture = constructions.Count(IsBeingCaptured);
        foldConstructions = EditorGUILayout.Foldout(
            foldConstructions, $"Construções ({constructions.Count}, {inCapture} em captura)", true);
        if (!foldConstructions)
            return;

        foreach (IGrouping<int, ConstructionSaveData> group in constructions
                     .OrderBy(c => c.slotIndex).ThenBy(c => c.cellX).ThenBy(c => c.cellY)
                     .GroupBy(c => c.slotIndex))
        {
            EditorGUILayout.LabelField(group.Key < 0 ? "neutras" : $"slot {group.Key}", EditorStyles.miniBoldLabel);
            Header(("tipo", 110), ("hex", 70), ("pontos", 70), ("renda", 60), ("setor", 80), ("nota", 160));
            foreach (ConstructionSaveData c in group)
            {
                int max = c.siteRuntime != null ? c.siteRuntime.capturePointsMax : 0;
                int income = c.siteRuntime != null ? c.siteRuntime.capturedIncoming : 0;
                string note = c.siteRuntime != null && c.siteRuntime.isPlayerHeadQuarter ? "HQ" : "";
                if (IsBeingCaptured(c))
                    note = (note.Length > 0 ? note + " · " : "") + "captura em andamento";
                Cells((c.constructionId, 110), ($"({c.cellX},{c.cellY})", 70),
                    (max > 0 ? $"{c.currentCapturePoints}/{max}" : c.currentCapturePoints.ToString(), 70),
                    (income.ToString(), 60), (EnumName<ConstructionSector>(c.sector), 80), (note, 160));
            }
        }
    }

    private static bool IsBeingCaptured(ConstructionSaveData c) =>
        c.siteRuntime != null
        && c.siteRuntime.capturePointsMax > 0
        && c.currentCapturePoints < c.siteRuntime.capturePointsMax;

    private void DrawPlans()
    {
        List<AIObjectivePlanSaveData> plans = read.data.aiObjectivePlans;
        foldPlans = EditorGUILayout.Foldout(foldPlans, $"Plano da IA ({plans.Count})", true);
        if (!foldPlans)
            return;

        foreach (AIObjectivePlanSaveData plan in plans)
        {
            EditorGUILayout.LabelField(
                $"slot {plan.slotIndex} ({TeamName(plan.teamId)}) — {plan.objectives.Count} objetivos, "
                + $"{plan.rogueUnitIds.Count} sem plano, {plan.handoffVacaterIds.Count} cedendo",
                EditorStyles.miniBoldLabel);
            Header(("setor", 80), ("tipo", 110), ("estado", 150), ("prior.", 50), ("vagas", 260));
            foreach (AIObjectiveSaveData o in plan.objectives.OrderBy(o => o.priority))
            {
                string slots = string.Join(", ", o.slots.Select(s =>
                    $"{EnumName<UnitRole>(s.role)}:{(s.filled ? "#" + s.assignedUnitId : "vaga")}"));
                Cells((EnumName<ConstructionSector>(o.sector), 80), (EnumName<AIObjectiveType>(o.objectiveType), 110),
                    (EnumName<ObjectiveStatus>(o.status), 150), (o.priority.ToString(), 50), (slots, 260));
            }
        }
    }

    private void DrawProgress()
    {
        CampaignProgressStore.Snapshot progress = read.data.campaignProgress;
        int count = progress?.campanhas?.Sum(c => c?.quadrantes?.Count ?? 0) ?? 0;
        foldProgress = EditorGUILayout.Foldout(foldProgress, $"Progresso da campanha ({count} quadrantes com dono)", true);
        if (!foldProgress)
            return;

        EditorGUI.indentLevel++;
        if (progress?.campanhas == null || progress.campanhas.Count == 0)
        {
            EditorGUILayout.LabelField("Nenhuma campanha registrada neste save.");
            EditorGUI.indentLevel--;
            return;
        }
        foreach (CampaignProgressStore.CampaignProgressData campaign in progress.campanhas)
        {
            EditorGUILayout.LabelField($"{campaign.mundoId} / {campaign.campanhaId}", EditorStyles.miniBoldLabel);
            if (campaign.quadrantes == null || campaign.quadrantes.Count == 0)
            {
                EditorGUILayout.LabelField("nenhum quadrante concluído");
                continue;
            }
            foreach (CampaignProgressStore.QuadrantOwnershipData q in campaign.quadrantes)
                Row(q.quadranteId,
                    $"slot {q.ownerSlotIndex} · rodada {q.lastTurn} · "
                    + $"{CampaignProgressStore.DescreverMotivo(q.reason)} · {q.updatedAtUtc}");
        }
        EditorGUI.indentLevel--;
    }

    private void DrawBriefing()
    {
        List<TurnBriefingEventSaveData> events = read.data.turnBriefingEvents;
        foldBriefing = EditorGUILayout.Foldout(foldBriefing, $"Jornal do Comandante pendente ({events.Count})", true);
        if (!foldBriefing)
            return;

        EditorGUI.indentLevel++;
        foreach (TurnBriefingEventSaveData e in events)
            Row($"rodada {e.turnNumber} → slot {e.slotIndex}", $"{e.subjectName} {e.detail} ({e.cellX},{e.cellY})");
        EditorGUI.indentLevel--;
    }

    // ------------------------------------------------------------------
    // Exportar
    // ------------------------------------------------------------------

    private void ExportJson()
    {
        string folder = Path.Combine(
            Path.GetDirectoryName(Application.dataPath) ?? ".",
            "Temp", "SaveInspector", Path.GetFileNameWithoutExtension(read.path));
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "manifest.json"), read.manifestJson ?? string.Empty);
        File.WriteAllText(Path.Combine(folder, "game.json"), read.gameJson ?? string.Empty);
        status = $"Exportado em {folder}";
        EditorUtility.RevealInFinder(Path.Combine(folder, "game.json"));
    }

    // ------------------------------------------------------------------
    // Utilitarios de desenho
    // ------------------------------------------------------------------

    private static void Row(string label, string value) =>
        EditorGUILayout.LabelField(label, value ?? string.Empty, EditorStyles.wordWrappedLabel);

    private static void Header(params (string text, float width)[] columns)
    {
        EditorGUILayout.BeginHorizontal();
        foreach ((string text, float width) in columns)
            GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
        EditorGUILayout.EndHorizontal();
    }

    private static void Cells(params (string text, float width)[] columns)
    {
        EditorGUILayout.BeginHorizontal();
        foreach ((string text, float width) in columns)
            GUILayout.Label(text ?? string.Empty, EditorStyles.miniLabel, GUILayout.Width(width));
        EditorGUILayout.EndHorizontal();
    }

    private static string On(bool value) => value ? "sim" : "não";

    private static string TeamName(int teamId) => EnumName<TeamId>(teamId);

    private static string EnumName<T>(int value) where T : struct, Enum =>
        Enum.IsDefined(typeof(T), Convert.ChangeType(value, Enum.GetUnderlyingType(typeof(T))))
            ? Enum.ToObject(typeof(T), value).ToString()
            : value.ToString();
}
