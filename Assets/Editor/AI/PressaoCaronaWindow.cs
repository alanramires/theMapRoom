using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tela da fila de carona. Le o PressaoCaronaService — a MESMA leitura que a compra usa
/// para o piso de Chinook — e so desenha. Somente leitura.
/// </summary>
public sealed class PressaoCaronaWindow : EditorWindow
{
    private readonly List<PressaoCaronaLeitura> leituras = new List<PressaoCaronaLeitura>();
    private readonly Dictionary<int, bool> foldoutBySlot = new Dictionary<int, bool>();
    private string status = "Entre em Play e clique em Atualizar.";
    private Vector2 scroll;

    [MenuItem("Tools/Transporte/Pressao Carona")]
    public static void Open() =>
        GetWindow<PressaoCaronaWindow>("Pressao Carona").Show();

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Atualizar", EditorStyles.toolbarButton, GUILayout.Width(80)))
                Refresh();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Só funciona em Play: lê a partida ao vivo.", MessageType.Info);
            return;
        }
        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.LabelField(status, EditorStyles.miniLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (PressaoCaronaLeitura l in leituras)
            DrawSlot(l);
        EditorGUILayout.EndScrollView();
    }

    internal static string Summary(PressaoCaronaLeitura l)
    {
        int stranded = l.Count(PressaoCaronaCategoria.IlhadoComPlano);
        return $"ilhados do plano {stranded} · bancos aéreos livres {l.AirSeatsFree}/{l.AirSeatsTotal}"
            + $" · falta {l.StrandedAirSeatShortage} banco(s)";
    }

    private void DrawSlot(PressaoCaronaLeitura l)
    {
        int want = l.Pedidos.Count;
        int saldo = l.SeatsFree - want;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        string header = $"slot {l.Slot}:  querem carona {want}  ·  a bordo {l.Aboard}  ·  "
            + $"vagas livres {l.SeatsFree}/{l.SeatsTotal}  ·  saldo {saldo:+0;-0;0}";
        Color prev = GUI.color;
        if (saldo < 0) GUI.color = new Color(1f, 0.6f, 0.6f);
        bool open = foldoutBySlot.TryGetValue(l.Slot, out bool f) ? f : true;
        open = EditorGUILayout.Foldout(open, header + (saldo < 0 ? "   << FALTA ONIBUS" : ""), true, EditorStyles.foldoutHeader);
        foldoutBySlot[l.Slot] = open;
        GUI.color = prev;

        EditorGUILayout.LabelField(
            $"ilhados do plano {l.Count(PressaoCaronaCategoria.IlhadoComPlano)}  ·  "
            + $"com plano (têm chão) {l.Count(PressaoCaronaCategoria.ComPlano)}  ·  "
            + $"rogue {l.Count(PressaoCaronaCategoria.Rogue)}  ·  "
            + $"emergência {l.Count(PressaoCaronaCategoria.Emergencia)}  ·  "
            + $"não captura {l.Count(PressaoCaronaCategoria.NaoCapturador)}",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField("compra lê: " + Summary(l), EditorStyles.miniLabel);

        if (open)
        {
            EditorGUILayout.LabelField("Ônibus", EditorStyles.boldLabel);
            foreach (PressaoCaronaOnibus b in l.Buses)
                DrawRow(b.Unit,
                    $"{Label(b.Unit)}  {b.Used}/{b.Capacity}  {(b.Air ? "ar" : "terra")}"
                    + (b.Unit != null && b.Unit.HasActed ? "  (agiu)" : ""),
                    null);

            EditorGUILayout.LabelField("Pedidos", EditorStyles.boldLabel);
            foreach (PressaoCaronaPedido p in l.Pedidos)
            {
                QueroCaronaResult r = p.Result;
                DrawRow(p.Unit,
                    $"[{p.Categoria}] {Label(p.Unit)}  alcance={r.reach}  espera={r.rideWaitTurns}",
                    r.reason);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private static string Label(UnitManager u)
    {
        if (u == null) return "(sumiu)";
        string name = u.TryGetUnitData(out UnitData d) && d != null ? d.displayName : "?";
        return $"{name} #{u.InstanceId} hp={u.CurrentHP} ({u.CurrentCellPosition.x},{u.CurrentCellPosition.y})";
    }

    private static void DrawRow(UnitManager unit, string label, string detail)
    {
        if (GUILayout.Button(new GUIContent(label, detail), EditorStyles.label) && unit != null)
        {
            Selection.activeGameObject = unit.gameObject;
            EditorGUIUtility.PingObject(unit.gameObject);
        }
        if (!string.IsNullOrEmpty(detail))
            EditorGUILayout.LabelField("      " + detail, EditorStyles.wordWrappedMiniLabel);
    }

    private void Refresh()
    {
        leituras.Clear();
        AIController ai = AIController.Instance;
        if (ai == null)
        {
            status = "Sem AIController.Instance na cena.";
            return;
        }

        var slots = new SortedSet<int>();
        foreach (UnitManager u in UnitManager.AllActive)
            if (u != null && !u.IsDead)
                slots.Add(u.SlotIndex);

        int errors = 0;
        foreach (int slot in slots)
        {
            PressaoCaronaLeitura l = PressaoCaronaService.Read(ai, slot);
            errors += l.Errors;
            leituras.Add(l);
        }
        status = $"lido em {DateTime.Now:HH:mm:ss}" + (errors > 0 ? $" — {errors} erro(s), ver Console" : "");
        Repaint();
    }
}
