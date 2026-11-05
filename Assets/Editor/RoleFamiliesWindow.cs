using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Tools > Role Families. As SEIS familias de comportamento (UnitRoleFamily), os rotulos
// (UnitRole) dentro de cada uma e as fichas (UnitData) que declaram cada rotulo.
//
// Nada aqui e tabela copiada: a familia vem de UnitRoleCompatibility.ResolveFamily, a
// "composicao" de ResolveCompositionRole (a leitura do shopping) e o "satisfaz" de
// CanSatisfy. Se uma regra mudar no codigo, a janela muda junto.
//
// Para que serve: ver de uma vez quem conta como o que. A familia e o COMPORTAMENTO
// (questionario, magnetico); a composicao e a CONTAGEM do shopping, e as duas divergem de
// proposito em alguns rotulos — a coluna "composição" destaca onde.
// =====================================================================================
public class RoleFamiliesWindow : EditorWindow
{
    private Vector2 scroll;
    private List<UnitData> fichas = new List<UnitData>();
    private readonly Dictionary<UnitRoleFamily, bool> aberta = new Dictionary<UnitRoleFamily, bool>();
    private bool mostrarSecundarios = true;
    private bool soComFicha;

    [MenuItem("Tools/Role Families")]
    public static void OpenWindow() => GetWindow<RoleFamiliesWindow>("Role Families");

    private void OnEnable() => Recarregar();

    private void Recarregar()
    {
        fichas = new List<UnitData>();
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData"))
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null) fichas.Add(d);
        }
        fichas.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.OrdinalIgnoreCase));
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Recarregar fichas", GUILayout.Width(130))) Recarregar();
        mostrarSecundarios = GUILayout.Toggle(mostrarSecundarios, "mostrar papéis secundários", GUILayout.Width(190));
        soComFicha = GUILayout.Toggle(soComFicha, "esconder rótulo sem ficha", GUILayout.Width(180));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{fichas.Count} fichas", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Família = comportamento (roles[0], a essência). Composição = como o Shopping CONTA a ficha. " +
            "⚠ marca onde as duas divergem. Secundário = a ficha tem o rótulo, mas não como essência.",
            MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (UnitRoleFamily familia in System.Enum.GetValues(typeof(UnitRoleFamily)))
            DesenharFamilia(familia);
        EditorGUILayout.EndScrollView();
    }

    private static List<UnitRole> RotulosDa(UnitRoleFamily familia)
    {
        var lista = new List<UnitRole>();
        foreach (UnitRole r in System.Enum.GetValues(typeof(UnitRole)))
        {
            if (r == UnitRole.None) continue;
            if (UnitRoleCompatibility.ResolveFamily(r) == familia) lista.Add(r);
        }
        return lista;
    }

    private void DesenharFamilia(UnitRoleFamily familia)
    {
        List<UnitRole> rotulos = RotulosDa(familia);

        // "Nenhuma": rotulos sem familia e fichas sem papel.
        var semPapel = new List<UnitData>();
        if (familia == UnitRoleFamily.Nenhuma)
            foreach (UnitData d in fichas)
                if (d.roles == null || d.roles.Count == 0) semPapel.Add(d);
        if (familia == UnitRoleFamily.Nenhuma && rotulos.Count == 0 && semPapel.Count == 0)
            return;

        int total = 0;
        foreach (UnitData d in fichas)
            if (UnitRoleCompatibility.ResolveFamily(d) == familia && d.roles != null && d.roles.Count > 0) total++;

        if (!aberta.TryGetValue(familia, out bool open)) open = true;
        string titulo = familia == UnitRoleFamily.Nenhuma ? "Nenhuma família" : $"Família {familia}";
        open = EditorGUILayout.Foldout(open, $"{titulo}   ({rotulos.Count} rótulos, {total} fichas)", true, EditorStyles.foldoutHeader);
        aberta[familia] = open;
        if (!open) return;

        EditorGUI.indentLevel++;
        foreach (UnitRole rotulo in rotulos)
            DesenharRotulo(rotulo);
        if (semPapel.Count > 0)
        {
            EditorGUILayout.LabelField("Fichas sem papel nenhum", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (UnitData d in semPapel) DesenharFicha(d, UnitRole.None, false);
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6f);
    }

    private void DesenharRotulo(UnitRole rotulo)
    {
        var essencia = new List<UnitData>();
        var secundario = new List<UnitData>();
        foreach (UnitData d in fichas)
        {
            if (d.roles == null || d.roles.Count == 0) continue;
            if (d.roles[0] == rotulo) essencia.Add(d);
            else if (d.roles.Contains(rotulo)) secundario.Add(d);
        }
        if (soComFicha && essencia.Count == 0 && (!mostrarSecundarios || secundario.Count == 0))
            return;

        string satisfaz = DescreverSatisfaz(rotulo);
        EditorGUILayout.LabelField(
            $"{rotulo} ({(int)rotulo})   participação {UnitRoleCompatibility.ResolveBattleParticipation(rotulo)}" +
            (satisfaz.Length > 0 ? $"   satisfaz também: {satisfaz}" : ""),
            EditorStyles.boldLabel);

        EditorGUI.indentLevel++;
        if (essencia.Count == 0)
            EditorGUILayout.LabelField("(nenhuma ficha com este rótulo como essência)", EditorStyles.miniLabel);
        foreach (UnitData d in essencia) DesenharFicha(d, rotulo, false);
        if (mostrarSecundarios)
            foreach (UnitData d in secundario) DesenharFicha(d, rotulo, true);
        EditorGUI.indentLevel--;
    }

    // Quais OUTROS rotulos este satisfaz (UnitRoleCompatibility.CanSatisfy), na ordem do enum.
    private static string DescreverSatisfaz(UnitRole rotulo)
    {
        var outros = new List<string>();
        foreach (UnitRole r in System.Enum.GetValues(typeof(UnitRole)))
            if (r != UnitRole.None && r != rotulo && UnitRoleCompatibility.CanSatisfy(rotulo, r))
                outros.Add(r.ToString());
        return string.Join(", ", outros);
    }

    private static void DesenharFicha(UnitData d, UnitRole rotulo, bool secundario)
    {
        UnitRole composicao = UnitRoleCompatibility.ResolveCompositionRole(d);
        UnitRoleFamily familiaComposicao = UnitRoleCompatibility.ResolveFamily(composicao);
        UnitRoleFamily familia = UnitRoleCompatibility.ResolveFamily(d);
        bool diverge = !secundario && composicao != UnitRole.None && familiaComposicao != familia;

        string papeis = d.roles != null && d.roles.Count > 0 ? string.Join(", ", d.roles) : "—";
        EditorGUILayout.BeginHorizontal();
        string texto =
            $"{(secundario ? "↳ " : "")}{d.displayName}   [{d.domain}/{d.unitClass}]   ${d.cost}   " +
            $"papéis: {papeis}   composição: {composicao}{(diverge ? $"  ⚠ conta como família {familiaComposicao}" : "")}";
        EditorGUILayout.LabelField(texto, secundario ? EditorStyles.miniLabel : EditorStyles.label);
        if (GUILayout.Button("ficha", GUILayout.Width(50)))
        {
            Selection.activeObject = d;
            EditorGUIUtility.PingObject(d);
        }
        EditorGUILayout.EndHorizontal();
    }
}
