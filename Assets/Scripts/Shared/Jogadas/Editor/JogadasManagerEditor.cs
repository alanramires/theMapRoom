using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(JogadasManager))]
public class JogadasManagerEditor : Editor
{
    private Vector2 _scroll;
    private int     _filtroTeam = -1;   // -1 = todos
    private int     _filtroTurno = 0;   // 0 = todos
    private bool    _agruparPorTurno = true;

    public override void OnInspectorGUI()
    {
        JogadasManager mgr = (JogadasManager)target;
        JogadasLog     log  = mgr.log;
        List<Jogada>   all  = log?.jogadas ?? new List<Jogada>();

        // ---- cabeçalho ----
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Jogadas", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Total: {all.Count}  |  Próximo ID: {log?.proximoId ?? 1}",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(4);

        // ---- export de TUDO (ignora os filtros) — sempre visível, desabilitado se vazio ----
        using (new EditorGUI.DisabledScope(all.Count == 0))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Exportar tudo ({all.Count}):", GUILayout.Width(130));
            if (GUILayout.Button("CSV", GUILayout.Width(70)))
                ExportarArquivo(all, JogadasManager.JogadasExportFormat.Csv);
            if (GUILayout.Button("Texto", GUILayout.Width(70)))
                ExportarArquivo(all, JogadasManager.JogadasExportFormat.Texto);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space(4);

        // ---- filtros ----
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Team:", GUILayout.Width(40));
        _filtroTeam = EditorGUILayout.IntField(_filtroTeam, GUILayout.Width(30));
        EditorGUILayout.LabelField("(-1=todos)", EditorStyles.miniLabel, GUILayout.Width(60));
        EditorGUILayout.LabelField("Turno:", GUILayout.Width(40));
        _filtroTurno = EditorGUILayout.IntField(_filtroTurno, GUILayout.Width(30));
        EditorGUILayout.LabelField("(0=todos)", EditorStyles.miniLabel, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        _agruparPorTurno = EditorGUILayout.Toggle("Agrupar por turno", _agruparPorTurno);
        EditorGUILayout.Space(4);

        // ---- lista filtrada ----
        IEnumerable<Jogada> filtradas = all;
        if (_filtroTeam >= 0)  filtradas = filtradas.Where(j => j.team  == _filtroTeam);
        if (_filtroTurno > 0)  filtradas = filtradas.Where(j => j.turno == _filtroTurno);
        List<Jogada> lista = filtradas.OrderByDescending(j => j.jogadaId).ToList();

        if (lista.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma jogada registrada.", MessageType.None);
            return;
        }

        // ---- export FILTRADO (respeita os filtros acima) ----
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Exportar filtradas ({lista.Count}):", GUILayout.Width(150));
        if (GUILayout.Button("CSV", GUILayout.Width(70)))
            ExportarArquivo(lista, JogadasManager.JogadasExportFormat.Csv);
        if (GUILayout.Button("Texto", GUILayout.Width(70)))
            ExportarArquivo(lista, JogadasManager.JogadasExportFormat.Texto);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        // ---- cabeçalho da tabela ----
        DrawTableHeader();

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(500));

        if (_agruparPorTurno)
        {
            foreach (int turno in lista.Select(j => j.turno).Distinct().OrderByDescending(t => t))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField($"── Turno {turno} ──", EditorStyles.miniLabel);
                foreach (Jogada j in lista.Where(j => j.turno == turno))
                    DrawRow(j);
            }
        }
        else
        {
            foreach (Jogada j in lista)
                DrawRow(j);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawTableHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        Col("ID",     32);
        Col("T",      18);
        Col("Team",   36);
        Col("Ação",   110);
        Col("Sigla",  42);
        Col("UID",    36);
        Col("UID2",   36);
        Col("Coord",  120);
        Col("Destino",120);
        Col("Obs",    80);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawRow(Jogada j)
    {
        Color bg = j.team == 0
            ? new Color(0.55f, 0.75f, 0.55f, 0.18f)
            : new Color(0.75f, 0.45f, 0.45f, 0.18f);

        Rect rect = EditorGUILayout.BeginHorizontal();
        EditorGUI.DrawRect(rect, bg);

        Col(j.jogadaId.ToString(), 32);
        Col(j.turno.ToString(),    18);
        Col(j.team.ToString(),     36);
        Col(j.acao ?? "-",        110);
        Col(j.unidadeSigla ?? "-", 42);
        Col(j.uid  > 0 ? j.uid.ToString()  : "-", 36);
        Col(j.uid2 > 0 ? j.uid2.ToString() : "-", 36);
        Col(j.TemCoordenada ? $"{j.cx},{j.cy}{Paren(JogadasManager.TipoConstrucaoNaCelula(j.cx, j.cy))}" : "-", 120);
        Col(j.TemDestino   ? $"{j.dx},{j.dy}{Paren(JogadasManager.DestinoLabel(j.dx, j.dy))}" : "-",  120);
        Col(string.IsNullOrEmpty(j.obs) ? "-" : j.obs, 80);

        EditorGUILayout.EndHorizontal();
    }

    private static void Col(string text, float width)
        => EditorGUILayout.LabelField(text, EditorStyles.miniLabel, GUILayout.Width(width));

    // Envolve o rótulo (resolvido pelo JogadasManager) em " (...)" para a tabela.
    private static string Paren(string s) => string.IsNullOrEmpty(s) ? "" : $" ({s})";

    private static void ExportarArquivo(List<Jogada> jogadas, JogadasManager.JogadasExportFormat format)
    {
        string ext = format == JogadasManager.JogadasExportFormat.Csv ? "csv" : "txt";
        string nome = $"jogadas_{System.DateTime.Now:yyyyMMdd_HHmmss}";
        string path = EditorUtility.SaveFilePanel("Exportar Jogadas", "", nome, ext);
        if (string.IsNullOrEmpty(path)) return;

        string content = format == JogadasManager.JogadasExportFormat.Csv
            ? JogadasManager.BuildCsv(jogadas)
            : JogadasManager.BuildTexto(jogadas);

        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        Debug.Log($"[Jogadas] exportado ({format}) {jogadas.Count} jogada(s) em: {path}");
        EditorUtility.RevealInFinder(path);
    }
}
