using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialManager))]
public class TutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Exportar CSV...", GUILayout.Height(26)))
                ExportTutorialCsv((TutorialManager)target);
            if (GUILayout.Button("Importar CSV...", GUILayout.Height(26)))
                ImportTutorialCsv((TutorialManager)target);
        }

        EditorGUILayout.HelpBox(
            "Export/Import do tutorial ATIVO em CSV (uma linha por tarefa e por passo; voice vai como " +
            "caminho do asset, round-trip completo). Import SUBSTITUI objectives e script do tutorial " +
            "ativo (com Undo). Precisa do MatchController.ActiveTutorial resolvido — se avisar 'nenhum " +
            "tutorial ativo', rode em Play. NÃO reordene/remova colunas do CSV: o import lê por posição.",
            MessageType.Info);
    }

    // ---------------------------------------------------------------- Export

    private static void ExportTutorialCsv(TutorialManager manager)
    {
        if (manager == null)
            return;

        string csv = manager.BuildActiveTutorialCsv();
        if (string.IsNullOrEmpty(csv))
        {
            EditorUtility.DisplayDialog("Exportar Tutorial",
                "Nenhum tutorial ativo. Garanta MatchController.ActiveTutorial (ou rode em Play) e tente de novo.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Exportar Tutorial para CSV", "", manager.GetActiveTutorialExportName(), "csv");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true));
            Debug.Log($"[TutorialManager] Tutorial exportado: {path}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Exportar Tutorial", $"Falha ao salvar:\n{ex.Message}", "OK");
        }
    }

    // ---------------------------------------------------------------- Import

    private static void ImportTutorialCsv(TutorialManager manager)
    {
        if (manager == null)
            return;

        TutorialData asset = manager.ResolveActiveTutorialAsset();
        if (asset == null)
        {
            EditorUtility.DisplayDialog("Importar Tutorial",
                "Nenhum tutorial ativo. Garanta MatchController.ActiveTutorial (ou rode em Play) e tente de novo.", "OK");
            return;
        }

        string path = EditorUtility.OpenFilePanel("Importar Tutorial de CSV", "", "csv");
        if (string.IsNullOrEmpty(path))
            return;

        string content;
        try { content = File.ReadAllText(path); }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Importar Tutorial", $"Falha ao ler:\n{ex.Message}", "OK");
            return;
        }

        var objectives = new List<TutorialObjective>();
        var script = new List<TutorialDialogEntry>();
        int voiceMissing = 0;

        string[] lines = content.Split('\n');
        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                continue;

            List<string> f = ParseCsvLine(line);
            if (f.Count == 0)
                continue;

            string tag = f[0].Trim();
            if (string.Equals(tag, "OBJ", StringComparison.OrdinalIgnoreCase))
            {
                objectives.Add(new TutorialObjective
                {
                    id = GetS(f, 2),
                    key = GetS(f, 3),
                    parameters = GetS(f, 4),
                    description = GetS(f, 5),
                    startHidden = GetB(f, 6),
                    isVisible = GetB(f, 7),
                    isOptional = GetB(f, 8),
                    isDefeatCondition = GetB(f, 9),
                    isCompleted = GetB(f, 10),
                    hasFailed = GetB(f, 11),
                });
            }
            else if (string.Equals(tag, "STEP", StringComparison.OrdinalIgnoreCase))
            {
                string voicePath = GetS(f, 15);
                AudioClip voice = null;
                if (!string.IsNullOrEmpty(voicePath))
                {
                    voice = AssetDatabase.LoadAssetAtPath<AudioClip>(voicePath);
                    if (voice == null)
                    {
                        voiceMissing++;
                        Debug.LogWarning($"[TutorialManager] Import: voice nao encontrado '{voicePath}' (passo idx={GetS(f, 1)}).");
                    }
                }

                script.Add(new TutorialDialogEntry
                {
                    advance = GetEnum(f, 2, TutorialAdvanceCondition.Immediate),
                    objectiveKey = GetS(f, 3),
                    revealObjective = GetB(f, 4),
                    waitObjectiveKey = GetS(f, 5),
                    waitObjectiveIndex = GetI(f, 6, -1),
                    waitAllUnitsActed = GetB(f, 7),
                    waitPlayerTurnStart = GetB(f, 8),
                    turn = GetEnum(f, 9, TutorialEndTurnEffect.NoEffect),
                    movement = GetEnum(f, 10, TutorialMovementEffect.NoEffect),
                    unlockMovement = GetB(f, 11),
                    revealObjectiveKey = GetS(f, 12),
                    revealObjectiveIndex = GetI(f, 13, -1),
                    text = GetS(f, 14),
                    voice = voice,
                    spawnCommand = GetS(f, 16),
                    statCommand = GetS(f, 17),
                });
            }
        }

        if (objectives.Count == 0 && script.Count == 0)
        {
            EditorUtility.DisplayDialog("Importar Tutorial",
                "O arquivo nao tem linhas OBJ nem STEP reconheciveis. Nada importado.", "OK");
            return;
        }

        bool ok = EditorUtility.DisplayDialog("Importar Tutorial",
            $"Isto vai SUBSTITUIR o tutorial ativo '{asset.name}':\n" +
            $"  tarefas: {(asset.objectives != null ? asset.objectives.Count : 0)} -> {objectives.Count}\n" +
            $"  passos:  {(asset.script != null ? asset.script.Count : 0)} -> {script.Count}\n\n" +
            "Continuar? (dá pra desfazer com Ctrl+Z)",
            "Substituir", "Cancelar");
        if (!ok)
            return;

        Undo.RecordObject(asset, "Importar Tutorial CSV");
        asset.objectives = objectives;
        asset.script = script;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialManager] Importado de {path}: {objectives.Count} tarefa(s), {script.Count} passo(s)."
                  + (voiceMissing > 0 ? $" {voiceMissing} voice(s) nao encontrado(s)." : ""));
    }

    // ---------------------------------------------------------------- Helpers

    // Parser de UMA linha CSV: respeita aspas e "" como aspas escapadas. O export troca quebras de
    // linha internas por espaco, entao cada registro cabe numa linha (parser simples de linha basta).
    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static string GetS(List<string> f, int i) => (i >= 0 && i < f.Count) ? f[i] : "";
    private static bool GetB(List<string> f, int i) => bool.TryParse(GetS(f, i), out bool v) && v;
    private static int GetI(List<string> f, int i, int fallback) => int.TryParse(GetS(f, i), out int v) ? v : fallback;
    private static T GetEnum<T>(List<string> f, int i, T fallback) where T : struct
        => Enum.TryParse(GetS(f, i), true, out T v) ? v : fallback;
}
