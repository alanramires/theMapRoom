using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialManager))]
public class TutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Exportar Tutorial para CSV...", GUILayout.Height(26)))
            ExportTutorialCsv((TutorialManager)target);

        EditorGUILayout.HelpBox(
            "Exporta o tutorial ATIVO (uma linha por tarefa e por passo) e abre a janela de salvar " +
            "para você escolher onde. Precisa do MatchController.ActiveTutorial resolvido — se der aviso " +
            "de 'nenhum tutorial ativo', rode em Play.",
            MessageType.Info);
    }

    private static void ExportTutorialCsv(TutorialManager manager)
    {
        if (manager == null)
            return;

        string csv = manager.BuildActiveTutorialCsv();
        if (string.IsNullOrEmpty(csv))
        {
            EditorUtility.DisplayDialog(
                "Exportar Tutorial",
                "Nenhum tutorial ativo. Garanta MatchController.ActiveTutorial (ou rode em Play) e tente de novo.",
                "OK");
            return;
        }

        string suggested = manager.GetActiveTutorialExportName();
        string path = EditorUtility.SaveFilePanel("Exportar Tutorial para CSV", "", suggested, "csv");
        if (string.IsNullOrEmpty(path))
            return; // usuário cancelou

        try
        {
            System.IO.File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true));
            Debug.Log($"[TutorialManager] Tutorial exportado: {path}");
        }
        catch (System.Exception ex)
        {
            EditorUtility.DisplayDialog("Exportar Tutorial", $"Falha ao salvar:\n{ex.Message}", "OK");
        }
    }
}
