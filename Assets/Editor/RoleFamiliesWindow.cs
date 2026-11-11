using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Tools > Role Families. EDITOR do UnitRoleCatalog (D3, 2026-11-11): cada rotulo
// (UnitRole) tem uma linha no asset com familia (comportamento), Forca (grupo de
// combate), participacao, "conta como" (a leitura do Shopping) e "satisfaz tambem"
// (quais pedidos atende). Tudo o que a janela mostra vem do asset; tudo o que se edita
// aqui vai para o asset. Mudar uma flag muda o jogo inteiro — e e para isso que existe.
//
// As fichas (UnitData) sao so leitura: aparecem uma vez, debaixo do rotulo de essencia
// (roles[0]). A coluna "composição" marca onde a leitura do Shopping diverge da familia.
// =====================================================================================
public class RoleFamiliesWindow : EditorWindow
{
    private Vector2 scroll;
    private List<UnitData> fichas = new List<UnitData>();
    private readonly Dictionary<string, bool> aberta = new Dictionary<string, bool>();
    private bool soComFicha;
    private UnitRoleCatalog catalogo;

    private enum Visualizacao { Familia, GrupoDeCombate }
    private Visualizacao visualizacao = Visualizacao.Familia;
    private static readonly string[] nomesVisualizacao = { "Por família", "Por grupo de combate (Força)" };

    // Mascara para o MaskField de "satisfaz tambem": bit i = roleValues[i].
    private static readonly UnitRole[] roleValues = BuildRoleValues();
    private static readonly string[] roleNames = System.Array.ConvertAll(roleValues, r => r.ToString());

    private static UnitRole[] BuildRoleValues()
    {
        var list = new List<UnitRole>();
        foreach (UnitRole r in System.Enum.GetValues(typeof(UnitRole)))
            if (r != UnitRole.None) list.Add(r);
        return list.ToArray();
    }

    [MenuItem("Tools/Role Families")]
    public static void OpenWindow() => GetWindow<RoleFamiliesWindow>("Role Families");

    private void OnEnable() => Recarregar();

    private void Recarregar()
    {
        catalogo = UnitRoleCatalog.Active;
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
        if (catalogo == null)
        {
            EditorGUILayout.HelpBox("UnitRoleCatalog ausente. Crie com tools/ai_test/criar_catalogo_papeis.cs " +
                                    "(unity command script eval --file ...) ou Assets > Create > Map Room > Unit Role Catalog em Assets/DB/Resources.",
                MessageType.Error);
            if (GUILayout.Button("Procurar de novo")) Recarregar();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Recarregar", GUILayout.Width(90))) Recarregar();
        if (GUILayout.Button("Salvar asset", GUILayout.Width(90))) AssetDatabase.SaveAssets();
        if (GUILayout.Button("ver asset", GUILayout.Width(70))) { Selection.activeObject = catalogo; EditorGUIUtility.PingObject(catalogo); }
        soComFicha = GUILayout.Toggle(soComFicha, "esconder rótulo sem ficha", GUILayout.Width(180));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField($"{fichas.Count} fichas", GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        List<UnitRole> faltantes = RotulosSemLinha();
        if (faltantes.Count > 0)
        {
            EditorGUILayout.HelpBox($"Rótulos do enum sem linha no catálogo: {string.Join(", ", faltantes)}. " +
                                    "Eles valem Nenhuma/None até ganharem linha.", MessageType.Warning);
            if (GUILayout.Button("Adicionar linhas faltantes"))
            {
                Undo.RecordObject(catalogo, "Adicionar papéis ao catálogo");
                foreach (UnitRole r in faltantes) catalogo.GetOrAdd(r);
                Marcar();
            }
        }

        visualizacao = (Visualizacao)GUILayout.Toolbar((int)visualizacao, nomesVisualizacao);

        EditorGUILayout.HelpBox(
            visualizacao == Visualizacao.Familia
                ? "Família = comportamento (questionário, magnético). Composição = como o Shopping CONTA a ficha; " +
                  "⚠ marca onde as duas divergem. Só a essência (roles[0]) decide o nó da ficha. Os campos por rótulo são do asset."
                : "Grupo de combate = Força (rally, formação, pressão do rally). Para o caça virar 'porradeiro', marque " +
                  "Assalto em 'satisfaz também' do Interceptador; para virar Dissuasão, troque a Força. O comportamento continua da família.",
            MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (visualizacao == Visualizacao.Familia)
        {
            foreach (UnitRoleFamily familia in System.Enum.GetValues(typeof(UnitRoleFamily)))
                DesenharGrupo($"fam:{familia}", familia == UnitRoleFamily.Nenhuma ? "Nenhuma família" : $"Família {familia}",
                    r => UnitRoleCompatibility.ResolveFamily(r) == familia, familia == UnitRoleFamily.Nenhuma);
        }
        else
        {
            foreach (UnitForce forca in System.Enum.GetValues(typeof(UnitForce)))
                DesenharGrupo($"for:{forca}", forca == UnitForce.Nenhuma ? "Sem grupo de combate" : $"Forças de {NomeDa(forca)}",
                    r => UnitRoleCompatibility.ResolveForce(r) == forca, forca == UnitForce.Nenhuma);
        }
        EditorGUILayout.EndScrollView();
    }

    private List<UnitRole> RotulosSemLinha()
    {
        var lista = new List<UnitRole>();
        foreach (UnitRole r in roleValues)
            if (catalogo.Get(r) == null) lista.Add(r);
        return lista;
    }

    private static string NomeDa(UnitForce forca)
    {
        switch (forca)
        {
            case UnitForce.Ruptura: return "Ruptura";
            case UnitForce.Dissuasao: return "Dissuasão";
            case UnitForce.Ocupacao: return "Ocupação";
            case UnitForce.Apoio: return "Apoio";
            default: return forca.ToString();
        }
    }

    // Um no da arvore: os rotulos que passam no filtro, cada um com sua linha editavel e
    // suas fichas de essencia. "incluiSemPapel": o no Nenhuma lista as fichas sem papel.
    private void DesenharGrupo(string chave, string titulo, System.Predicate<UnitRole> filtro, bool incluiSemPapel)
    {
        var rotulos = new List<UnitRole>();
        foreach (UnitRole r in roleValues) if (filtro(r)) rotulos.Add(r);

        var semPapel = new List<UnitData>();
        if (incluiSemPapel)
            foreach (UnitData d in fichas)
                if (d.roles == null || d.roles.Count == 0) semPapel.Add(d);
        if (incluiSemPapel && rotulos.Count == 0 && semPapel.Count == 0) return;

        int total = 0;
        foreach (UnitData d in fichas)
            if (d.roles != null && d.roles.Count > 0 && rotulos.Contains(d.roles[0])) total++;

        if (!aberta.TryGetValue(chave, out bool open)) open = true;
        open = EditorGUILayout.Foldout(open, $"{titulo}   ({rotulos.Count} rótulos, {total} fichas)", true, EditorStyles.foldoutHeader);
        aberta[chave] = open;
        if (!open) return;

        EditorGUI.indentLevel++;
        foreach (UnitRole rotulo in rotulos) DesenharRotulo(rotulo);
        if (semPapel.Count > 0)
        {
            EditorGUILayout.LabelField("Fichas sem papel nenhum", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            foreach (UnitData d in semPapel) DesenharFicha(d);
            EditorGUI.indentLevel--;
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6f);
    }

    private void DesenharRotulo(UnitRole rotulo)
    {
        var essencia = new List<UnitData>();
        foreach (UnitData d in fichas)
            if (d.roles != null && d.roles.Count > 0 && d.roles[0] == rotulo) essencia.Add(d);
        if (soComFicha && essencia.Count == 0) return;

        UnitRoleCatalogEntry linha = catalogo.Get(rotulo);
        EditorGUILayout.LabelField($"{rotulo} ({(int)rotulo})", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        if (linha == null)
        {
            EditorGUILayout.LabelField("(sem linha no catálogo)", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            var familia = (UnitRoleFamily)EditorGUILayout.EnumPopup("família", linha.familia);
            var forca = (UnitForce)EditorGUILayout.EnumPopup("Força", linha.forca);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            var participacao = (UnitBattleParticipation)EditorGUILayout.EnumPopup("participação", linha.participacao);
            var contaComo = (UnitRole)EditorGUILayout.EnumPopup("conta como (Shopping)", linha.contaComo);
            EditorGUILayout.EndHorizontal();
            int mask = MaskDe(linha.satisfazTambem, rotulo);
            int novoMask = EditorGUILayout.MaskField("satisfaz também", mask, roleNames);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(catalogo, $"Catálogo: {rotulo}");
                linha.familia = familia; linha.forca = forca; linha.participacao = participacao; linha.contaComo = contaComo;
                linha.satisfazTambem = ListaDe(novoMask, rotulo);
                Marcar();
            }
        }

        if (essencia.Count == 0)
            EditorGUILayout.LabelField("(nenhuma ficha com este rótulo como essência)", EditorStyles.miniLabel);
        foreach (UnitData d in essencia) DesenharFicha(d);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2f);
    }

    private static int MaskDe(List<UnitRole> lista, UnitRole proprio)
    {
        int mask = 0;
        if (lista == null) return mask;
        for (int i = 0; i < roleValues.Length; i++)
            if (roleValues[i] != proprio && lista.Contains(roleValues[i])) mask |= 1 << i;
        return mask;
    }

    private static List<UnitRole> ListaDe(int mask, UnitRole proprio)
    {
        var lista = new List<UnitRole>();
        for (int i = 0; i < roleValues.Length; i++)
            if (roleValues[i] != proprio && (mask & (1 << i)) != 0) lista.Add(roleValues[i]);
        return lista;
    }

    private void Marcar()
    {
        catalogo.Invalidate();
        EditorUtility.SetDirty(catalogo);
    }

    private static void DesenharFicha(UnitData d)
    {
        UnitRole composicao = UnitRoleCompatibility.ResolveCompositionRole(d);
        UnitRoleFamily familiaComposicao = UnitRoleCompatibility.ResolveFamily(composicao);
        UnitRoleFamily familia = UnitRoleCompatibility.ResolveFamily(d);
        bool diverge = composicao != UnitRole.None && familiaComposicao != familia;

        string papeis = d.roles != null && d.roles.Count > 0 ? string.Join(", ", d.roles) : "—";
        EditorGUILayout.BeginHorizontal();
        string texto =
            $"{d.displayName}   [{d.domain}/{d.unitClass}]   ${d.cost}   " +
            $"papéis: {papeis}   composição: {composicao}{(diverge ? $"  ⚠ conta como família {familiaComposicao}" : "")}";
        EditorGUILayout.LabelField(texto);
        if (GUILayout.Button("ficha", GUILayout.Width(50)))
        {
            Selection.activeObject = d;
            EditorGUIUtility.PingObject(d);
        }
        EditorGUILayout.EndHorizontal();
    }
}
