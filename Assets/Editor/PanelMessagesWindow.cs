using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Search the actual catalog assets and their code references, without entering Play mode.</summary>
public class PanelMessagesWindow : EditorWindow
{
    private sealed class Entry
    {
        public UnityEngine.Object asset;
        public string id, condition, message, messageEnglish, path;
        public readonly List<(string path, int line)> usages = new List<(string, int)>();
        public bool expanded;
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<string> issues = new List<string>();
    private string search = string.Empty;
    private Vector2 scroll;
    private int kind;
    private const string Root = "Assets/DB/Messages";

    [MenuItem("Tools/Messages/Catalogo dos paineis")]
    public static void Open() => GetWindow<PanelMessagesWindow>("Mensagens dos paineis");

    private void OnEnable() => RefreshCatalog();

    private void RefreshCatalog()
    {
        entries.Clear();
        issues.Clear();
        var registered = new HashSet<UnityEngine.Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:HelperDatabase", new[] { Root }))
        {
            var database = AssetDatabase.LoadAssetAtPath<HelperDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var item in database.Messages)
                if (item != null) registered.Add(item);
                else issues.Add(database.name + ": referencia vazia.");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:DialogDatabase", new[] { Root }))
        {
            var database = AssetDatabase.LoadAssetAtPath<DialogDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var item in database.Messages)
                if (item != null) registered.Add(item);
                else issues.Add(database.name + ": referencia vazia.");
        }
        var byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Entry entry;
            if (asset is HelperData helper)
                entry = new Entry { asset = asset, id = helper.id, condition = helper.condition, message = helper.message, messageEnglish = helper.messageEnglish, path = path };
            else if (asset is DialogData dialog)
                entry = new Entry { asset = asset, id = dialog.id, condition = dialog.condition, message = dialog.message, messageEnglish = dialog.messageEnglish, path = path };
            else continue;
            entries.Add(entry);
            if (string.IsNullOrWhiteSpace(entry.id)) issues.Add(path + ": ID vazio.");
            else if (byId.ContainsKey(entry.id)) issues.Add(entry.id + ": ID duplicado (" + path + ").");
            else byId.Add(entry.id, entry);
            if (string.IsNullOrWhiteSpace(entry.message)) issues.Add(entry.id + ": mensagem vazia.");
            if (!registered.Contains(asset)) issues.Add(entry.id + ": nao registrada em um Database.");
        }
        foreach (string path in Directory.EnumerateFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains("~")) continue;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
                foreach (Match match in Regex.Matches(line, "\"([^\"]+)\""))
                    if (byId.TryGetValue(match.Groups[1].Value, out Entry entry))
                        entry.usages.Add((path.Replace('\\', '/'), i + 1));
            }
        }
        entries.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Dialog = painel curto. Helper = painel de apoio. Edite a mensagem no asset; preserve o ID e os tokens <nome>. As referencias abaixo incluem IDs usados indiretamente por wrappers.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            search = EditorGUILayout.TextField("Buscar", search);
            if (GUILayout.Button("Atualizar / validar", GUILayout.Width(145))) RefreshCatalog();
        }
        kind = GUILayout.Toolbar(kind, new[] { "Todos", "Dialog", "Helper" });
        EditorGUILayout.LabelField(entries.Count + " mensagens | " + entries.Count(e => !string.IsNullOrWhiteSpace(e.messageEnglish)) + " com ingles | " + issues.Count + " problemas de cadastro");
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Warning);
        foreach (var entry in entries)
        {
            if (kind == 1 && !(entry.asset is DialogData)) continue;
            if (kind == 2 && !(entry.asset is HelperData)) continue;
            string haystack = entry.id + " " + entry.messageEnglish + " " + entry.message + " " + entry.condition + " " + entry.path + " " + string.Join(" ", entry.usages.Select(u => u.path));
            if (!string.IsNullOrWhiteSpace(search) && haystack.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.expanded = EditorGUILayout.Foldout(entry.expanded, entry.id ?? "(sem ID)", true);
                    if (GUILayout.Button("Editar asset", GUILayout.Width(90)))
                    {
                        Selection.activeObject = entry.asset;
                        EditorGUIUtility.PingObject(entry.asset);
                    }
                }
                EditorGUILayout.LabelField(entry.message ?? string.Empty, EditorStyles.wordWrappedLabel);
                if (!entry.expanded) continue;
                EditorGUILayout.LabelField("English: " + (string.IsNullOrWhiteSpace(entry.messageEnglish) ? "(vazio: usa portugues)" : entry.messageEnglish), EditorStyles.wordWrappedLabel);
                EditorGUILayout.LabelField(entry.condition ?? string.Empty, EditorStyles.wordWrappedLabel);
                if (entry.usages.Count == 0)
                    EditorGUILayout.LabelField("Sem referencia literal: pode ser ID dinamico ou mensagem ainda sem uso.", EditorStyles.wordWrappedMiniLabel);
                foreach (var usage in entry.usages)
                    if (GUILayout.Button(usage.path + ":" + usage.line, EditorStyles.linkLabel))
                        AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(usage.path), usage.line);
            }
        }
        EditorGUILayout.EndScrollView();
    }
}
