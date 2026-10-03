using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Auditoria PAPÉIS E CAPACIDADES: o que cada ficha É (família, pelo rótulo) ao lado do
// que ela CONSEGUE fazer (lido dos dados: armas, vagas, serviços, estoque). Só LÊ.
// Contrato: docs/AI Behavior/contrato_questionario.md §7.2b e §7.3.
//
// O filtro "capacidade fora da família" mostra as raças da revisao_papeis.md: a peça
// que faz algo que a família dela não faz (o porta-aviões que atira e supre, o caminhão
// de suprimentos que reboca a artilharia). Não é erro — é o que a família precisa
// saber usar.
//
// Modalidade substituiu o campo preferArtilleryModeBeforeCombatant: mude o alcance da
// arma e ela muda sozinha. "Estacionária" (longRangeStationary) é mobilidade — não
// reposiciona depois de comprado —, não arma.
//
// O MODO do transportador (Pickup, Courier, hospital) não aparece aqui: é estado de
// partida, não ficha.
// =====================================================================================
public class ModalidadeCombateWindow : EditorWindow
{
    private sealed class Linha
    {
        public UnitData Data;
        public string Rotulo;
        public UnitRoleFamily Familia;
        public UnitCombatModality Modalidade;
        public string Armas;
        public string Transporta;
        public string Supre;
        public string Transfere;
        public bool Estacionaria;
        public string ForaDaFamilia;
    }

    private readonly List<Linha> linhas = new List<Linha>();
    private bool soForaDaFamilia;
    private Vector2 scroll;

    [MenuItem("Tools/Auditoria/Papéis e Capacidades")]
    public static void Open() =>
        GetWindow<ModalidadeCombateWindow>("Papéis e Capacidades").Show();

    private void OnEnable() => Recarregar();

    private void Recarregar()
    {
        linhas.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:UnitData"))
        {
            UnitData data = AssetDatabase.LoadAssetAtPath<UnitData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null)
                continue;

            var linha = new Linha
            {
                Data = data,
                Rotulo = data.roles != null && data.roles.Count > 0 ? data.roles[0].ToString() : "(sem papel)",
                Familia = UnitRoleCompatibility.ResolveFamily(data),
                Modalidade = UnitCombatModalityRules.Resolve(data),
                Armas = DescreverArmas(data),
                Transporta = DescreverTransporte(data),
                Supre = DescreverSuprimento(data),
                Transfere = DescreverTransferencia(data),
                Estacionaria = data.longRangeStationary,
            };
            linha.ForaDaFamilia = DescreverForaDaFamilia(linha);
            linhas.Add(linha);
        }

        linhas.Sort((a, b) =>
        {
            int porFamilia = a.Familia.CompareTo(b.Familia);
            return porFamilia != 0 ? porFamilia : string.CompareOrdinal(a.Data.name, b.Data.name);
        });
    }

    // ------------------------------------------------------------------
    // Leitura das capacidades
    // ------------------------------------------------------------------

    private static string DescreverArmas(UnitData data)
    {
        if (data.embarkedWeapons == null || data.embarkedWeapons.Count == 0)
            return "—";

        var partes = new List<string>();
        foreach (UnitEmbarkedWeapon embarked in data.embarkedWeapons)
        {
            if (embarked == null || embarked.weapon == null)
                continue;
            int min = embarked.GetRangeMin();
            int max = embarked.GetRangeMax();
            partes.Add(min == max ? $"{embarked.weapon.name} {min}" : $"{embarked.weapon.name} {min}~{max}");
        }
        return partes.Count > 0 ? string.Join(" · ", partes) : "—";
    }

    private static string DescreverTransporte(UnitData data)
    {
        if (!data.isTransporter || data.transportSlots == null || data.transportSlots.Count == 0)
            return "—";

        int vagas = 0;
        var classes = new SortedSet<string>();
        foreach (UnitTransportSlotRule slot in data.transportSlots)
        {
            if (slot == null)
                continue;
            vagas += Mathf.Max(0, slot.capacity);
            if (slot.allowedClasses != null)
                foreach (GameUnitClass unitClass in slot.allowedClasses)
                    classes.Add(unitClass.ToString());
        }
        return classes.Count > 0
            ? $"{vagas} ({string.Join(", ", classes)})"
            : $"{vagas}";
    }

    private static bool OfereceTransferencia(UnitData data)
    {
        if (!data.isSupplier || data.supplierServicesProvided == null)
            return false;
        if (data.supplierTier != SupplierTier.Hub && data.supplierTier != SupplierTier.Receiver)
            return false;
        foreach (ServiceData service in data.supplierServicesProvided)
            if (service != null && service.serviceType == ServiceType.Transfer)
                return true;
        return false;
    }

    private static string DescreverSuprimento(UnitData data)
    {
        if (!data.isSupplier || data.supplierServicesProvided == null)
            return "—";

        var servicos = new List<string>();
        foreach (ServiceData service in data.supplierServicesProvided)
            if (service != null && service.serviceType != ServiceType.Transfer)
                servicos.Add(service.name);
        return servicos.Count > 0
            ? $"{string.Join(", ", servicos)} [{data.serviceRange}]"
            : "—";
    }

    private static string DescreverTransferencia(UnitData data)
    {
        return OfereceTransferencia(data) ? data.supplierTier.ToString() : "—";
    }

    // A capacidade que a família não tem por natureza. Vigilância armada é a doutrina
    // (Fragata, Submarino), não exceção — por isso arma só conta fora em Transportador
    // e Logística.
    private static string DescreverForaDaFamilia(Linha linha)
    {
        var fora = new List<string>();
        if (linha.Transporta != "—" && linha.Familia != UnitRoleFamily.Transportador)
            fora.Add("transporta");
        if (linha.Supre != "—" && linha.Familia != UnitRoleFamily.Logistica)
            fora.Add("supre");
        if (linha.Transfere != "—" && linha.Familia != UnitRoleFamily.Logistica)
            fora.Add("transfere");
        if (linha.Modalidade != UnitCombatModality.SemArma
            && (linha.Familia == UnitRoleFamily.Transportador || linha.Familia == UnitRoleFamily.Logistica))
            fora.Add("atira");
        return fora.Count > 0 ? string.Join(", ", fora) : string.Empty;
    }

    // ------------------------------------------------------------------
    // GUI
    // ------------------------------------------------------------------

    private void OnGUI()
    {
        int fora = 0;
        foreach (Linha linha in linhas)
            if (!string.IsNullOrEmpty(linha.ForaDaFamilia))
                fora++;

        EditorGUILayout.HelpBox(
            "Só leitura. FAMÍLIA (do rótulo) = o que a peça É: a coluna que segue e onde fica. " +
            "O resto é o que ela CONSEGUE, lido dos dados: MODALIDADE (armas, alcance da unidade), " +
            "vagas, serviços de suprimento, estoque.\n" +
            "'Fora da família' = capacidade que a família não tem por natureza. Não é erro: é o que ela precisa saber usar.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            soForaDaFamilia = EditorGUILayout.ToggleLeft(
                $"Só capacidade fora da família ({fora} de {linhas.Count})", soForaDaFamilia, GUILayout.Width(300));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Recarregar", GUILayout.Width(100)))
                Recarregar();
            if (GUILayout.Button("Copiar tabela", GUILayout.Width(110)))
                EditorGUIUtility.systemCopyBuffer = MontarTabela();
        }

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("#", EditorStyles.miniBoldLabel, GUILayout.Width(28));
            GUILayout.Label("Ficha", EditorStyles.miniBoldLabel, GUILayout.Width(150));
            GUILayout.Label("Família", EditorStyles.miniBoldLabel, GUILayout.Width(95));
            GUILayout.Label("Rótulo", EditorStyles.miniBoldLabel, GUILayout.Width(140));
            GUILayout.Label("Modalidade", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.Label("Transporta", EditorStyles.miniBoldLabel, GUILayout.Width(130));
            GUILayout.Label("Supre", EditorStyles.miniBoldLabel, GUILayout.Width(150));
            GUILayout.Label("Transfere", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label("Estac.", EditorStyles.miniBoldLabel, GUILayout.Width(45));
            GUILayout.Label("Fora da família", EditorStyles.miniBoldLabel, GUILayout.Width(130));
            GUILayout.Label("Armas", EditorStyles.miniBoldLabel);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        int indice = 0;
        foreach (Linha linha in linhas)
        {
            if (soForaDaFamilia && string.IsNullOrEmpty(linha.ForaDaFamilia))
                continue;

            indice++;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(indice.ToString(), EditorStyles.miniLabel, GUILayout.Width(28));
                if (GUILayout.Button(linha.Data.name, EditorStyles.linkLabel, GUILayout.Width(150)))
                    EditorGUIUtility.PingObject(linha.Data);
                GUILayout.Label(linha.Familia.ToString(), GUILayout.Width(95));
                GUILayout.Label(linha.Rotulo, GUILayout.Width(140));
                GUILayout.Label(linha.Modalidade.ToString(), EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label(linha.Transporta, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(130));
                GUILayout.Label(linha.Supre, EditorStyles.wordWrappedMiniLabel, GUILayout.Width(150));
                GUILayout.Label(linha.Transfere, GUILayout.Width(70));
                GUILayout.Label(linha.Estacionaria ? "sim" : "—", GUILayout.Width(45));
                GUILayout.Label(
                    string.IsNullOrEmpty(linha.ForaDaFamilia) ? "—" : "⚑ " + linha.ForaDaFamilia,
                    GUILayout.Width(130));
                GUILayout.Label(linha.Armas, EditorStyles.wordWrappedMiniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private string MontarTabela()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| # | ficha | família | rótulo | modalidade | transporta | supre | transfere | estac. | fora da família | armas |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
        int indice = 0;
        foreach (Linha linha in linhas)
        {
            if (soForaDaFamilia && string.IsNullOrEmpty(linha.ForaDaFamilia))
                continue;
            indice++;
            sb.AppendLine(
                $"| {indice} | {linha.Data.name} | {linha.Familia} | {linha.Rotulo} | {linha.Modalidade} | " +
                $"{linha.Transporta} | {linha.Supre} | {linha.Transfere} | " +
                $"{(linha.Estacionaria ? "sim" : "—")} | " +
                $"{(string.IsNullOrEmpty(linha.ForaDaFamilia) ? "—" : linha.ForaDaFamilia)} | {linha.Armas} |");
        }
        return sb.ToString();
    }
}
