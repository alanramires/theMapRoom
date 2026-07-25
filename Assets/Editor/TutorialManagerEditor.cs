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

        // Captura o clique DENTRO do scope e age DEPOIS que ele fecha: as janelas modais
        // (SaveFilePanel/OpenFilePanel/DisplayDialog) interrompem o IMGUI e, se chamadas dentro do
        // HorizontalScope, quebram o balanceamento de layout ("EndLayoutGroup: BeginLayoutGroup...").
        bool doExport, doImport;
        using (new EditorGUILayout.HorizontalScope())
        {
            doExport = GUILayout.Button("Exportar JSON...", GUILayout.Height(26));
            doImport = GUILayout.Button("Importar JSON...", GUILayout.Height(26));
        }

        EditorGUILayout.HelpBox(
            "Export/Import do tutorial ATIVO em JSON (objectives + script). O 'voice' das falas vai " +
            "como caminho do asset (round-trip completo). Importar SUBSTITUI objectives e script do " +
            "tutorial ativo (com Undo). Precisa do MatchController.ActiveTutorial — se avisar 'nenhum " +
            "tutorial ativo', rode em Play.",
            MessageType.Info);

        if (doExport)
            ExportTutorialJson((TutorialManager)target);
        if (doImport)
            ImportTutorialJson((TutorialManager)target);
    }

    // ---------------------------------------------------------------- Export

    private static void ExportTutorialJson(TutorialManager manager)
    {
        if (manager == null)
            return;

        string json = manager.BuildActiveTutorialJson();
        if (string.IsNullOrEmpty(json))
        {
            EditorUtility.DisplayDialog("Exportar Tutorial",
                "Nenhum tutorial ativo. Garanta MatchController.ActiveTutorial (ou rode em Play) e tente de novo.", "OK");
            return;
        }

        string path = EditorUtility.SaveFilePanel("Exportar Tutorial (JSON)", "", manager.GetActiveTutorialExportName(), "json");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, json, new System.Text.UTF8Encoding(true));
            Debug.Log($"[TutorialManager] Tutorial exportado: {path}");
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Exportar Tutorial", $"Falha ao salvar:\n{ex.Message}", "OK");
        }
    }

    // ---------------------------------------------------------------- Import

    private static void ImportTutorialJson(TutorialManager manager)
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

        string path = EditorUtility.OpenFilePanel("Importar Tutorial (JSON)", "", "json");
        if (string.IsNullOrEmpty(path))
            return;

        string json;
        try { json = File.ReadAllText(path); }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Importar Tutorial", $"Falha ao ler:\n{ex.Message}", "OK");
            return;
        }

        TutorialExportDto dto;
        try { dto = JsonUtility.FromJson<TutorialExportDto>(json); }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Importar Tutorial", $"JSON inválido:\n{ex.Message}", "OK");
            return;
        }
        if (dto == null)
        {
            EditorUtility.DisplayDialog("Importar Tutorial", "JSON inválido ou vazio.", "OK");
            return;
        }

        var objectives = new List<TutorialObjective>();
        if (dto.objectives != null)
        {
            foreach (TutorialObjectiveDto o in dto.objectives)
            {
                if (o == null) continue;
                objectives.Add(new TutorialObjective
                {
                    id = o.id,
                    key = o.key,
                    parameters = o.parameters,
                    description = o.description,
                    startHidden = o.startHidden,
                    isOptional = o.isOptional,
                    isDefeatCondition = o.isDefeatCondition,
                    // Estado de runtime reinicializado — o asset de design nao carrega progresso.
                    isVisible = !o.startHidden,
                    isCompleted = false,
                    hasFailed = false,
                });
            }
        }

        var script = new List<TutorialDialogEntry>();
        int voiceMissing = 0;
        if (dto.script != null)
        {
            foreach (TutorialStepDto s in dto.script)
            {
                if (s == null) continue;

                AudioClip voice = null;
                if (!string.IsNullOrEmpty(s.voicePath))
                {
                    voice = AssetDatabase.LoadAssetAtPath<AudioClip>(s.voicePath);
                    if (voice == null)
                    {
                        voiceMissing++;
                        Debug.LogWarning($"[TutorialManager] Import: voice não encontrado '{s.voicePath}'.");
                    }
                }

                script.Add(new TutorialDialogEntry
                {
                    advance = ParseEnum(s.advance, TutorialAdvanceCondition.Immediate),
                    turn = ParseEnum(s.turn, TutorialEndTurnEffect.NoEffect),
                    movement = ParseEnum(s.movement, TutorialMovementEffect.NoEffect),
                    objectiveKey = s.objectiveKey,
                    revealObjective = s.revealObjective,
                    text = s.text,
                    voice = voice,
                    spawnCommand = s.spawnCommand,
                    statCommand = s.statCommand,
                    waitObjectiveKey = s.waitObjectiveKey,
                    waitObjectiveIndex = s.waitObjectiveIndex,
                    waitAllUnitsActed = s.waitAllUnitsActed,
                    waitPlayerTurnStart = s.waitPlayerTurnStart,
                    unlockMovement = s.unlockMovement,
                    revealObjectiveKey = s.revealObjectiveKey,
                    revealObjectiveIndex = s.revealObjectiveIndex,
                });
            }
        }

        bool ok = EditorUtility.DisplayDialog("Importar Tutorial",
            $"Isto vai SUBSTITUIR o tutorial ativo '{asset.name}':\n" +
            $"  tarefas: {(asset.objectives != null ? asset.objectives.Count : 0)} -> {objectives.Count}\n" +
            $"  passos:  {(asset.script != null ? asset.script.Count : 0)} -> {script.Count}\n\n" +
            "Continuar? (dá pra desfazer com Ctrl+Z)",
            "Substituir", "Cancelar");
        if (!ok)
            return;

        Undo.RecordObject(asset, "Importar Tutorial JSON");
        asset.objectives = objectives;
        asset.script = script;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        Debug.Log($"[TutorialManager] Importado de {path}: {objectives.Count} tarefa(s), {script.Count} passo(s)."
                  + (voiceMissing > 0 ? $" {voiceMissing} voice(s) não encontrado(s)." : ""));
    }

    // Converte o nome do enum (string do JSON) de volta ao valor; fallback se vazio/desconhecido.
    private static T ParseEnum<T>(string s, T fallback) where T : struct
        => Enum.TryParse(s, true, out T v) ? v : fallback;
}
