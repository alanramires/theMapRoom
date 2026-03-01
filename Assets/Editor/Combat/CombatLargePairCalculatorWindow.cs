using UnityEditor;
using UnityEngine;

public class CombatLargePairCalculatorWindow : EditorWindow
{
    private UnitData attacker;
    private UnitData defender;
    private int range;
    private bool includeCounterAttack;
    private string summary;
    private string details;
    private Vector2 scroll;

    public static void Open(UnitData attacker, UnitData defender, int range, bool includeCounterAttack, string summary, string details)
    {
        CombatLargePairCalculatorWindow window = GetWindow<CombatLargePairCalculatorWindow>("Calc Simples");
        window.attacker = attacker;
        window.defender = defender;
        window.range = Mathf.Max(1, range);
        window.includeCounterAttack = includeCounterAttack;
        window.summary = summary ?? "-";
        window.details = details ?? string.Empty;
        window.minSize = new Vector2(540f, 320f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Calculadora Simples (atalho da Grande Matriz)", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Atacante", attacker, typeof(UnitData), false);
        EditorGUILayout.ObjectField("Defensor", defender, typeof(UnitData), false);
        EditorGUILayout.IntField("Range", range);
        EditorGUILayout.Toggle("Revide", includeCounterAttack);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Formula Aplicada", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Defensores Eliminados = [HP Atacante x (FA base da Arma do Atacante + FA RPS do Atacante + FA Elite Atacante)] / " +
            "(FD base Defensor + FD DPQ do Defensor + FD RPS Defensor + FD Elite Defensor)\n\n" +
            "Atacantes Eliminados = [HP Defensor x (FA base da Arma do Defensor + FA RPS do Defensor + FA Elite Defensor)] / " +
            "(FD base Atacante + FD DPQ do Atacante + FD RPS Atacante + FD Elite Atacante)\n\n" +
            "FA Elite Atacante = ownerAttack(Atacante) + opponentAttack(Defensor)\n" +
            "FD Elite Defensor = ownerDefense(Defensor) + opponentDefense(Atacante)\n" +
            "FA Elite Defensor = ownerAttack(Defensor) + opponentAttack(Atacante)\n" +
            "FD Elite Atacante = ownerDefense(Atacante) + opponentDefense(Defensor)",
            MessageType.None);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Resumo", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(summary, EditorStyles.textArea, GUILayout.Height(42f));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Detalhes", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(details, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
}
