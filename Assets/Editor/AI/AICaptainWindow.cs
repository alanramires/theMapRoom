using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Tools > AI Captain. A Tabela Magnetica (AICaptainData) organizada como a
// Tools > Role Families: as familias de comportamento (UnitRoleFamily), os rotulos
// (UnitRole) dentro de cada uma e, em cada rotulo, a linha da tabela — quem ele segue
// sem plano e com plano — mais as fichas que tem aquele rotulo como essencia.
//
// A familia vem de UnitRoleCompatibility.ResolveFamily e a lista "com plano" derivada
// vem de AICaptainData.TryResolve: nada e tabela copiada.
//
// AVISO que a janela repete no topo: hoje NENHUM codigo de jogo le este asset. O capitao
// em jogo sai dos resolvedores hardcoded (TryResolveCapturerMagnet e companhia). Editar
// aqui organiza a tabela para o dia em que ela virar fonte de verdade; ate la, nao muda
// a IA ("configurado nao e o que esta valendo", v8.6.1).
// =====================================================================================
public class AICaptainWindow : EditorWindow
{
    private const string DefaultPath = "Assets/DB/AI/AICaptain.asset";

    private AICaptainData data;
    private SerializedObject serialized;
    private Vector2 scroll;
    private List<UnitData> fichas = new List<UnitData>();
    private readonly Dictionary<UnitRoleFamily, bool> aberta = new Dictionary<UnitRoleFamily, bool>();
    private bool mostrarFichas = true;
    private bool soComLinha;

    [MenuItem("Tools/AI Captain (Tabela Magnética)")]
    public static void OpenWindow() => GetWindow<AICaptainWindow>("AI Captain");

    private void OnEnable()
    {
        if (data == null)
            data = AssetDatabase.LoadAssetAtPath<AICaptainData>(DefaultPath);
        Recarregar();
    }

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
        data = (AICaptainData)EditorGUILayout.ObjectField(data, typeof(AICaptainData), false, GUILayout.Width(220));
        if (GUILayout.Button("Recarregar fichas", GUILayout.Width(130))) Recarregar();
        mostrarFichas = GUILayout.Toggle(mostrarFichas, "mostrar fichas", GUILayout.Width(110));
        soComLinha = GUILayout.Toggle(soComLinha, "esconder rótulo sem linha", GUILayout.Width(180));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (data == null)
        {
            EditorGUILayout.HelpBox($"Nenhuma Tabela Magnética em {DefaultPath}.", MessageType.Info);
            if (GUILayout.Button("Gerar tabela padrão", GUILayout.Width(180)))
            {
                AICaptainDataGenerator.Generate();
                data = AssetDatabase.LoadAssetAtPath<AICaptainData>(DefaultPath);
            }
            return;
        }

        if (serialized == null || serialized.targetObject != data)
            serialized = new SerializedObject(data);
        serialized.Update();

        EditorGUILayout.HelpBox(
            "Nenhum código de jogo lê este asset hoje: o capitão em jogo vem dos resolvedores " +
            "hardcoded (TryResolveCapturerMagnet e companhia). Editar aqui organiza a tabela, " +
            "mas NÃO muda a IA até o asset virar fonte de verdade.",
            MessageType.Warning);
        EditorGUILayout.HelpBox(
            "Ordem = prioridade: a primeira faixa que produzir candidato vence, mesmo que alguém " +
            "de faixa inferior esteja mais perto. Rótulo sem linha não orbita ninguém (válido). " +
            "'Com plano' vazia = derivada da 'sem plano' (presa ao setor, aceita embarcado, RepCell no fim).",
            MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (UnitRoleFamily familia in System.Enum.GetValues(typeof(UnitRoleFamily)))
            DesenharFamilia(familia);
        EditorGUILayout.EndScrollView();

        serialized.ApplyModifiedProperties();
    }

    // ───────────────────────────────────────────────────────────── familia / rotulo ──

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
        if (rotulos.Count == 0)
            return;

        int comLinha = 0;
        foreach (UnitRole r in rotulos)
            if (FindProfileIndex(r) >= 0) comLinha++;

        if (!aberta.TryGetValue(familia, out bool open)) open = true;
        string titulo = familia == UnitRoleFamily.Nenhuma ? "Nenhuma família" : $"Família {familia}";
        open = EditorGUILayout.Foldout(open, $"{titulo}   ({rotulos.Count} rótulos, {comLinha} com linha)", true, EditorStyles.foldoutHeader);
        aberta[familia] = open;
        if (!open) return;

        EditorGUI.indentLevel++;
        foreach (UnitRole rotulo in rotulos)
        {
            if (DesenharRotulo(rotulo))
                break; // a lista de perfis mudou de tamanho: redesenha no proximo frame
        }
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(6f);
    }

    // Devolve true quando a estrutura do array de perfis mudou (criar/remover linha).
    private bool DesenharRotulo(UnitRole rotulo)
    {
        int index = FindProfileIndex(rotulo);
        if (soComLinha && index < 0)
            return false;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{rotulo} ({(int)rotulo})", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (index < 0)
        {
            if (GUILayout.Button("criar linha", GUILayout.Width(90)))
            {
                CriarLinha(rotulo);
                EditorGUILayout.EndHorizontal();
                return true;
            }
        }
        else if (GUILayout.Button("remover linha", GUILayout.Width(100))
                 && EditorUtility.DisplayDialog("Tabela Magnética",
                     $"Remover a linha de {rotulo}? Sem linha, o rótulo não orbita ninguém.",
                     "Remover", "Cancelar"))
        {
            serialized.FindProperty("perfis").DeleteArrayElementAtIndex(index);
            EditorGUILayout.EndHorizontal();
            return true;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        if (index < 0)
        {
            EditorGUILayout.LabelField("(sem linha: não orbita ninguém)", EditorStyles.miniLabel);
        }
        else
        {
            SerializedProperty profile = serialized.FindProperty("perfis").GetArrayElementAtIndex(index);
            DesenharLista("Sem plano", profile.FindPropertyRelative("semPlano"));
            DesenharComPlano(rotulo, profile.FindPropertyRelative("comPlano"));
        }

        if (mostrarFichas)
            DesenharFichas(rotulo);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(2f);
        return false;
    }

    // ─────────────────────────────────────────────────────────────────── listas ──

    private void DesenharComPlano(UnitRole rotulo, SerializedProperty comPlano)
    {
        if (comPlano.arraySize > 0)
        {
            DesenharLista("Com plano (personalizada)", comPlano);
            if (GUILayout.Button("voltar a derivar da 'sem plano'", GUILayout.Width(220)))
                comPlano.ClearArray();
            return;
        }

        // Derivada: aplica o que esta na tela antes de perguntar ao asset.
        serialized.ApplyModifiedProperties();
        IReadOnlyList<AICaptainAttractionEntry> derived = data.TryResolve(rotulo, hasPlan: true);
        EditorGUILayout.LabelField("Com plano (derivada)", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        if (derived != null)
            for (int i = 0; i < derived.Count; i++)
                EditorGUILayout.LabelField($"{i + 1}. {DescreverFaixa(derived[i])}", EditorStyles.miniLabel);
        EditorGUI.indentLevel--;
        if (GUILayout.Button("personalizar (copiar a derivada)", GUILayout.Width(220)) && derived != null)
        {
            comPlano.ClearArray();
            for (int i = 0; i < derived.Count; i++)
            {
                comPlano.InsertArrayElementAtIndex(i);
                SerializedProperty e = comPlano.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("procura").intValue = (int)derived[i].procura;
                e.FindPropertyRelative("papel").intValue = (int)derived[i].papel;
                e.FindPropertyRelative("aceitarEmbarcado").boolValue = derived[i].aceitarEmbarcado;
                e.FindPropertyRelative("restringirAoSetorDoPlano").boolValue = derived[i].restringirAoSetorDoPlano;
                e.FindPropertyRelative("rotulo").stringValue = derived[i].rotulo ?? string.Empty;
            }
        }
    }

    private static void DesenharLista(string titulo, SerializedProperty lista)
    {
        EditorGUILayout.LabelField($"{titulo}   ({lista.arraySize} faixas)", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < lista.arraySize; i++)
        {
            SerializedProperty e = lista.GetArrayElementAtIndex(i);
            SerializedProperty procura = e.FindPropertyRelative("procura");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(40));
            using (new EditorGUI.DisabledScope(i == 0))
                if (GUILayout.Button("↑", GUILayout.Width(22))) { lista.MoveArrayElement(i, i - 1); EditorGUILayout.EndHorizontal(); break; }
            using (new EditorGUI.DisabledScope(i == lista.arraySize - 1))
                if (GUILayout.Button("↓", GUILayout.Width(22))) { lista.MoveArrayElement(i, i + 1); EditorGUILayout.EndHorizontal(); break; }

            EditorGUILayout.PropertyField(procura, GUIContent.none, GUILayout.Width(190));
            if ((AICaptainAttractionKind)procura.intValue == AICaptainAttractionKind.UnidadeComPapel)
                EditorGUILayout.PropertyField(e.FindPropertyRelative("papel"), GUIContent.none, GUILayout.Width(150));
            else
                GUILayout.Space(154);

            SerializedProperty embarcado = e.FindPropertyRelative("aceitarEmbarcado");
            embarcado.boolValue = GUILayout.Toggle(embarcado.boolValue, "embarcado", GUILayout.Width(85));
            SerializedProperty setor = e.FindPropertyRelative("restringirAoSetorDoPlano");
            setor.boolValue = GUILayout.Toggle(setor.boolValue, "só setor", GUILayout.Width(70));
            EditorGUILayout.PropertyField(e.FindPropertyRelative("rotulo"), GUIContent.none, GUILayout.MinWidth(80));

            if (GUILayout.Button("x", GUILayout.Width(22)))
            {
                lista.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ faixa", GUILayout.Width(80)))
        {
            int at = lista.arraySize;
            lista.InsertArrayElementAtIndex(at);
            SerializedProperty e = lista.GetArrayElementAtIndex(at);
            if (at == 0)
            {
                e.FindPropertyRelative("procura").intValue = (int)AICaptainAttractionKind.UnidadeComPapel;
                e.FindPropertyRelative("papel").intValue = (int)UnitRole.Capturador;
                e.FindPropertyRelative("aceitarEmbarcado").boolValue = false;
                e.FindPropertyRelative("restringirAoSetorDoPlano").boolValue = false;
            }
            e.FindPropertyRelative("rotulo").stringValue = string.Empty;
        }
        EditorGUI.indentLevel--;
    }

    private static string DescreverFaixa(AICaptainAttractionEntry e)
    {
        if (e == null) return "?";
        string nome = AICaptainData.DescribeEntry(e);
        if (e.procura == AICaptainAttractionKind.UnidadeComPapel && string.IsNullOrWhiteSpace(e.rotulo)
            && e.papel != UnitRole.Capturador)
            nome = $"unidade {e.papel}";
        return nome
            + (e.restringirAoSetorDoPlano ? " · só setor" : "")
            + (e.aceitarEmbarcado ? " · aceita embarcado" : "");
    }

    // ─────────────────────────────────────────────────────────────────── fichas ──

    private void DesenharFichas(UnitRole rotulo)
    {
        var essencia = new List<UnitData>();
        foreach (UnitData d in fichas)
            if (d.roles != null && d.roles.Count > 0 && d.roles[0] == rotulo)
                essencia.Add(d);
        if (essencia.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhuma ficha com este rótulo como essência)", EditorStyles.miniLabel);
            return;
        }
        foreach (UnitData d in essencia)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"↳ {d.displayName}   [{d.domain}/{d.unitClass}]   ${d.cost}", EditorStyles.miniLabel);
            if (GUILayout.Button("ficha", GUILayout.Width(50)))
            {
                Selection.activeObject = d;
                EditorGUIUtility.PingObject(d);
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    // ───────────────────────────────────────────────────────────────── perfis ──

    private int FindProfileIndex(UnitRole rotulo)
    {
        SerializedProperty perfis = serialized.FindProperty("perfis");
        for (int i = 0; i < perfis.arraySize; i++)
            if (perfis.GetArrayElementAtIndex(i).FindPropertyRelative("papel").intValue == (int)rotulo)
                return i;
        return -1;
    }

    private void CriarLinha(UnitRole rotulo)
    {
        SerializedProperty perfis = serialized.FindProperty("perfis");
        int at = perfis.arraySize;
        perfis.InsertArrayElementAtIndex(at);
        SerializedProperty profile = perfis.GetArrayElementAtIndex(at);
        profile.FindPropertyRelative("papel").intValue = (int)rotulo;
        profile.FindPropertyRelative("semPlano").ClearArray();
        profile.FindPropertyRelative("comPlano").ClearArray();
    }
}
