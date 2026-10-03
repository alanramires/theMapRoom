using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// =====================================================================================
// Gerador do baseline AIPresetData a partir dos valores VIVOS da cena aberta.
//
// Gera UM asset baseline (valores NORMAIS, capacidades desligadas). A dificuldade é uma
// overlay de código (AIPresetData.ApplyDifficultyOverlay) que liga toggles por cima — não
// há asset por dificuldade para manter.
//
// Por que gerar em vez de escrever à mão: os valores da cena divergem dos defaults do
// código (e divergem ENTRE cenas — ver "Auditar cenas"). Escrever à mão mudaria
// silenciosamente a calibração da IA.
// =====================================================================================
public class AIPresetGeneratorWindow : EditorWindow
{
    private const string DefaultFolder = "Assets/DB/AI/Presets";

    private string targetFolder = DefaultFolder;
    private AIController controller;
    private AIShoppingPlanner shopping;
    private Vector2 scroll;
    private bool previewExpanded = true;

    private static readonly AIDifficulty[] AllDifficulties =
    {
        AIDifficulty.Facil,
        AIDifficulty.Medio,
        AIDifficulty.Dificil
    };

    [MenuItem("Tools/AI/Gerar Presets a partir da cena")]
    public static void Open()
    {
        AIPresetGeneratorWindow window = GetWindow<AIPresetGeneratorWindow>("AI Presets");
        window.minSize = new Vector2(720f, 480f);
        window.FindComponents();
        window.Show();
    }

    private void OnFocus() => FindComponents();

    private void FindComponents()
    {
        if (controller == null)
            controller = FindAnyObjectByType<AIController>(FindObjectsInactive.Include);
        if (shopping == null)
            shopping = FindAnyObjectByType<AIShoppingPlanner>(FindObjectsInactive.Include);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Fase 1 da migração para AIPresetData", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Fotografa os valores NORMAIS da CENA ABERTA e grava UM baseline (capacidades desligadas).\n" +
            "Com CATÁLOGO, cada dificuldade usa o seu próprio perfil e a overlay de código não roda;\n" +
            "sem catálogo, vale o modelo antigo — este baseline mais os toggles da dificuldade.\n" +
            "Nenhuma decisão da IA lê deste asset ainda — comportamento inalterado.\n\n" +
            "Gere a partir da cena mais calibrada (normalmente Battle Map 1 - Ground). " +
            "Cenas antigas podem não ter todos os campos serializados e cairiam nos defaults do código.",
            MessageType.Info);

        EditorGUILayout.Space();
        controller = (AIController)EditorGUILayout.ObjectField("AI Controller", controller, typeof(AIController), true);
        shopping = (AIShoppingPlanner)EditorGUILayout.ObjectField("Shopping Planner", shopping, typeof(AIShoppingPlanner), true);
        targetFolder = EditorGUILayout.TextField("Pasta de destino", targetFolder);

        // ANTES do early return: o catalogo so mexe em assets, entao funciona com a cena
        // de autoria aberta — que e justamente onde nao ha AIController. Deixa-lo depois
        // escondia o botao exatamente de quem precisava dele.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Catálogo — que perfil cada dificuldade usa", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Liga as três dificuldades da Tela de Entrada aos três presets, casando pelo RÓTULO DO "
            + "JOGADOR: o botão MÉDIO é AIDifficulty.Facil, então ligar pelo nome do enum erraria "
            + "a linha. Com catálogo, o perfil ativo é o asset autorado e a overlay de código não "
            + "roda. Linha já ligada à mão é respeitada.",
            MessageType.Info);
        if (GUILayout.Button("Criar/atualizar catálogo (dificuldade → perfil)", GUILayout.Height(26f)))
            SyncCatalog();

        if (controller == null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Nenhum AIController na cena aberta. Abra um mapa antes de GERAR O BASELINE — "
                + "o catálogo acima não precisa de cena.",
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (shopping == null)
        {
            EditorGUILayout.HelpBox(
                "Nenhum AIShoppingPlanner na cena. As seções de shopping (economia, composição, intel, aeronáutica) " +
                "vão usar os defaults do código — que é exatamente o que o runtime faria, já que o planner é " +
                "criado sob demanda por EnsureInstance().",
                MessageType.Warning);
        }

        EditorGUILayout.Space();
        previewExpanded = EditorGUILayout.Foldout(previewExpanded, "Prévia — o que a overlay de cada dificuldade muda", true);
        if (previewExpanded)
            DrawPreview();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(controller == null))
        {
            if (GUILayout.Button("Gerar baseline a partir da cena", GUILayout.Height(32f)))
                Generate();
        }

        if (GUILayout.Button("Auditar cenas — os valores da IA divergem entre mapas?"))
            AuditScenes();

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------------
    // Catalogo: uma linha por dificuldade OFERECIDA, apontando o asset do perfil.
    //
    // Preenche sozinho porque a ligacao e onde o erro mora: o enum nao casa com o botao
    // (o "MEDIO" da tela e AIDifficulty.Facil), entao arrastar a mao acerta o nome e
    // erra a dificuldade — e o sintoma seria o perfil errado numa partida, sem log.
    // Por isso casa pelo ROTULO DO JOGADOR: AIPreset_Medio vai na linha do botao MEDIO.
    private void SyncCatalog()
    {
        EnsureFolder(targetFolder);
        string path = $"{targetFolder}/AIPresetCatalog.asset";

        AIPresetCatalog catalogo = AssetDatabase.LoadAssetAtPath<AIPresetCatalog>(path);
        bool criou = catalogo == null;
        if (criou)
        {
            catalogo = CreateInstance<AIPresetCatalog>();
            AssetDatabase.CreateAsset(catalogo, path);
        }

        Undo.RecordObject(catalogo, "Catalogo de presets");

        // Entrada de dificuldade que nao existe mais (o catalogo gerado antes da reducao
        // de seis para tres guardou o DIFICIL como 4) nao casa com nada e cai no caminho
        // antigo SEM DIZER NADA. Some aqui, avisando qual era.
        int removidas = catalogo.entradas.RemoveAll(e =>
            e == null || System.Array.IndexOf(AIPresetCatalog.OferecidasAoJogador, e.dificuldade) < 0);
        var log = new System.Text.StringBuilder(
            criou ? "[AIPreset] catálogo criado em " : "[AIPreset] catálogo atualizado em ");
        log.AppendLine(path);
        if (removidas > 0)
            log.AppendLine($"   {removidas} entrada(s) de dificuldade extinta removida(s) — religue o perfil abaixo.");

        foreach (AIDifficulty dif in AIPresetCatalog.OferecidasAoJogador)
        {
            AIPresetCatalog.Entrada entrada = catalogo.entradas.Find(e => e != null && e.dificuldade == dif);
            if (entrada == null)
            {
                entrada = new AIPresetCatalog.Entrada { dificuldade = dif };
                catalogo.entradas.Add(entrada);
            }

            // Ja ligado a mao? Respeita — o autor manda mais que o palpite por nome.
            if (entrada.preset == null)
                entrada.preset = FindPresetByLabel(AIPresetCatalog.RotuloDoJogador(dif));

            log.AppendLine($"   botão {AIPresetCatalog.RotuloDoJogador(dif),-8} (enum {dif,-12}) → "
                + (entrada.preset != null ? entrada.preset.name : "VAZIO — cai na overlay antiga"));
        }

        EditorUtility.SetDirty(catalogo);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalogo;
        Debug.Log(log.ToString(), catalogo);
    }

    private AIPresetData FindPresetByLabel(string rotulo)
    {
        string[] guids = AssetDatabase.FindAssets("t:AIPresetData", new[] { targetFolder });
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string nome = System.IO.Path.GetFileNameWithoutExtension(p);
            // "AIPreset_Dificil" casa com o rotulo "DIFICIL"; acento e caixa fora da conta.
            if (Normalizar(nome).EndsWith(Normalizar(rotulo)))
                return AssetDatabase.LoadAssetAtPath<AIPresetData>(p);
        }
        return null;
    }

    private static string Normalizar(string valor)
    {
        if (string.IsNullOrEmpty(valor))
            return string.Empty;
        string s = valor.ToUpperInvariant();
        s = s.Replace("Á", "A").Replace("À", "A").Replace("Ã", "A").Replace("Â", "A");
        s = s.Replace("É", "E").Replace("Ê", "E");
        s = s.Replace("Í", "I").Replace("Ó", "O").Replace("Ô", "O").Replace("Õ", "O");
        s = s.Replace("Ú", "U").Replace("Ç", "C");
        return s;
    }

    // -------------------------------------------------------------------------------
    private void DrawPreview()
    {
        var so = new SerializedObject(controller);
        var rows = new List<(string label, string[] values)>();

        string[] Row(System.Func<AIDifficulty, string> f)
        {
            var cells = new string[AllDifficulties.Length];
            for (int i = 0; i < AllDifficulties.Length; i++) cells[i] = f(AllDifficulties[i]);
            return cells;
        }

        rows.Add(("Lista banida", Row(d => IsHard(d) ? "sim" : "—")));
        rows.Add(("Projeta produção inimiga", Row(d => IsHard(d) ? "sim" : "—")));
        rows.Add(("Abertura blindado 1º", Row(d => IsHard(d) ? "sim" : "—")));
        rows.Add(("Conscrição", Row(d => IsDoctrine(d) ? "sempre" : IsWhenLosing(d) ? "perdendo" : "—")));
        rows.Add(("Renda fora de cidades", Row(d => IsEasy(d) ? "1/3" : "cheia")));
        rows.Add(("Núcleo: infantaria", Row(d => Int(so, IsHard(d) ? "minInfantryHard" : "minInfantryNormal").ToString())));
        rows.Add(("Núcleo: assalto", Row(d => Int(so, IsHard(d) ? "minAssaultHard" : "minAssaultNormal").ToString())));
        rows.Add(("Núcleo: artilharia", Row(d => Int(so, IsHard(d) ? "minArtilleryHard" : "minArtilleryNormal").ToString())));
        rows.Add(("Elite ratio (pressão)", Row(d => Flt(so, IsHard(d) ? "eliteRatioHardPressure" : "eliteRatioNormalPressure").ToString("0.00"))));
        rows.Add(("Elite ratio (folga)", Row(d => Flt(so, IsHard(d) ? "eliteRatioHardSafe" : "eliteRatioNormalSafe").ToString("0.00"))));
        rows.Add(("Turnos de poupança elite", Row(d => Int(so, IsHard(d) ? "eliteSaveTurnsHard" : "eliteSaveTurnsNormal").ToString())));
        rows.Add(("Troco na poupança (%)", Row(d => Flt(so, IsHard(d) ? "eliteMaintenanceReserveHard" : "eliteMaintenanceReserveNormal").ToString("0"))));
        rows.Add(("Slots capturador/setor", Row(d => IsHard(d) ? "x2 (teto 6)" : "x1 (teto 4)")));
        rows.Add(("Teto de logística", Row(d => IsHard(d) ? Int(so, "maxLogisticUnitsOnHardMode").ToString() : "sem teto")));

        const float labelW = 200f;
        float colW = Mathf.Max(88f, (position.width - labelW - 40f) / AllDifficulties.Length);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("", GUILayout.Width(labelW));
        foreach (AIDifficulty d in AllDifficulties)
            EditorGUILayout.LabelField(d.ToString(), EditorStyles.miniBoldLabel, GUILayout.Width(colW));
        EditorGUILayout.EndHorizontal();

        foreach ((string label, string[] values) in rows)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel, GUILayout.Width(labelW));
            foreach (string v in values)
                EditorGUILayout.LabelField(v, EditorStyles.miniLabel, GUILayout.Width(colW));
            EditorGUILayout.EndHorizontal();
        }
    }

    // -------------------------------------------------------------------------------
    private void Generate()
    {
        EnsureFolder(targetFolder);
        var so = new SerializedObject(controller);

        string path = $"{targetFolder}/AIPreset_Baseline.asset";
        AIPresetData preset = LoadOrCreate<AIPresetData>(path);
        Populate(preset, so);
        EditorUtility.SetDirty(preset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AI][Preset] baseline gravado em {path}. " +
                  "Ligue-o no campo 'Base Preset' do AIController da cena. " +
                  "A dificuldade liga toggles por cima via ApplyDifficultyOverlay.");
        Selection.activeObject = preset;
    }

    // Baseline = valores NORMAIS, capacidades desligadas. A overlay de dificuldade
    // (AIPresetData.ApplyDifficultyOverlay) é quem liga os toggles e nudga os valores hard.
    private void Populate(AIPresetData preset, SerializedObject so)
    {
        preset.nomeExibido = "Padrão";
        if (string.IsNullOrEmpty(preset.doutrina))
            preset.doutrina = "Baseline gerado da cena. A dificuldade liga toggles por cima.";

        // --- capacidades: baseline neutro. A overlay de dificuldade é que acende cada uma.
        preset.capacidades.respeitarListaBanida = false;
        preset.capacidades.projetarProducaoInimiga = false;
        preset.capacidades.aberturaBlindadoPrimeiro = false;
        preset.capacidades.limitarLogistica = false;
        preset.capacidades.dobrarSlotsCapturadorPorSetor = false;
        preset.papeis.capturador.blitzkrieg = false;
        preset.papeis.capturador.substituicaoPorEficiencia = true;
        preset.papeis.capturador.capturaOportunista = true;
        preset.papeis.assalto.cacarAlvoPreferido = true;
        preset.papeis.fogoDeSuporte.fogoDePreparacao = true;
        preset.papeis.transportador.ataqueOportunistaVazio = true;
        preset.papeis.transportador.desembarcaCapturadorNoCaminho = true;
        preset.papeis.transportador.atenderEvac = true;
        preset.papeis.transportador.modoHospital = true;
        preset.missoes.reparo.fusaoEmReparo = AIFusaoEmReparo.Todos;
        preset.capacidades.conscricaoSempre = false;
        preset.capacidades.conscricaoQuandoPerdendo = false;
        preset.capacidades.politicaLadoForteFraco = Bool(so, "strongWeakSidePolitic");
        preset.capacidades.gateNucleoSuave = Bool(so, "softCoreGate");

        // --- economia (lado NORMAL dos pares)
        preset.economia.fracaoRendaForaDeCidades = 1f;
        preset.economia.eliteSaveTurns = Int(so, "eliteSaveTurnsNormal");
        preset.economia.eliteMaintenanceReservePercent = Flt(so, "eliteMaintenanceReserveNormal");
        if (shopping != null)
        {
            preset.economia.savingPercentualForElite = shopping.SavingPercentualForElite;
            preset.economia.counterEliteEscalationPressure = shopping.CounterEliteEscalationPressure;
        }

        // --- composição (lado NORMAL dos pares; a overlay hard nudga estes)
        preset.composicao.coreMinInfantry = Int(so, "minInfantryNormal");
        preset.composicao.coreMinAssault = Int(so, "minAssaultNormal");
        preset.composicao.coreMinArtillery = Int(so, "minArtilleryNormal");
        preset.composicao.eliteRatioPressure = Flt(so, "eliteRatioNormalPressure");
        preset.composicao.eliteRatioSafe = Flt(so, "eliteRatioNormalSafe");
        preset.composicao.maxLogisticUnits = Int(so, "maxLogisticUnitsOnHardMode");
        if (shopping != null)
        {
            preset.composicao.eliteCapturerFillRatio = shopping.EliteCapturerFillRatio;
            preset.composicao.minFilledAssaultSlots = shopping.MinFilledAssaultSlots;
            preset.composicao.minTurnForFireSupport = shopping.MinTurnForFireSupport;
            preset.composicao.minActiveCapturersForFireSupport = shopping.MinActiveCapturersForFireSupport;
            preset.composicao.minActiveAssaultForFireSupport = shopping.MinActiveAssaultForFireSupport;
            preset.composicao.minCapturerMassForSupport = shopping.MinCapturerMassForSupport;
            preset.composicao.assaultPerFireSupportRatio = shopping.AssaultPerFireSupportRatio;
            preset.composicao.capturersPerPreventiveTransport = shopping.CapturersPerPreventiveTransport;
            preset.composicao.progressiveCapturerBatchSize = shopping.ProgressiveCapturerBatchSize;
            preset.composicao.repairsPerGroundSupplier = shopping.RepairsPerGroundSupplier;
            preset.composicao.eliteRepairLogisticsPriority = shopping.EliteRepairLogisticsPriority;
            preset.composicao.maxProactiveDefensiveFireSupport = shopping.MaxProactiveDefensiveFireSupport;
            preset.composicao.maxProactiveAntiAirSAM = shopping.MaxProactiveAntiAirSAM;
            preset.composicao.antiAirCoverageRange = shopping.AntiAirCoverageRange;
        }

        // --- conscrição
        preset.conscricao.massacreEnterForceRatio = Flt(so, "massacreEnterForceRatio");
        preset.conscricao.massacreExitForceRatio = Flt(so, "massacreExitForceRatio");
        preset.conscricao.massacreUnitCapFillRatio = Flt(so, "massacreUnitCapFillRatio");

        // --- plano (lado NORMAL; a overlay hard sobe multiplier→2 e cap→6)
        preset.plano.maxActiveObjectives = Int(so, "maxActiveObjectives");
        preset.plano.capturerSlotsPerSectorMultiplier = 1;
        preset.plano.capturerSlotCap = 4;
        preset.plano.minDistanceForTransportSlot = Int(so, "minDistanceForTransportSlot");

        // --- tática
        preset.tatica.riskDecisionImpact = Flt(so, "riskDecisionImpact");
        preset.tatica.defenseEnemyRange = Int(so, "defenseEnemyRange");
        preset.tatica.defenseCallRange = Int(so, "defenseCallRange");
        preset.tatica.alliesEnemyRange = Int(so, "alliesEnemyRange");
        preset.tatica.alliesCallRange = Int(so, "alliesCallRange");
        preset.tatica.alliesAgainstEnemiesHpRatio = Flt(so, "alliesAgainstEnemiesHpRatio");
        if (shopping != null)
        {
            preset.tatica.minBaseArtilharia = shopping.MinBaseArtilharia;
            preset.tatica.minBaseAAA = shopping.MinBaseAAA;
            preset.tatica.minTurnBaseDefense = shopping.MinTurnBaseDefense;
        }

        // --- intel
        if (shopping != null)
        {
            preset.intel.usarIntelJogadasNoShopping = shopping.usarIntelJogadasNoShopping;
            preset.intel.lookbackTurns = shopping.IntelShoppingLookbackTurns;
            preset.intel.infantryPressureAssaultThreshold = shopping.IntelInfantryPressureAssaultThreshold;
            preset.intel.airThreatAntiAirThreshold = shopping.IntelAirThreatAntiAirThreshold;
            preset.intel.armorThreatDefenseThreshold = shopping.IntelArmorThreatDefenseThreshold;
            preset.intel.capturePressureDefenseThreshold = shopping.IntelCapturePressureDefenseThreshold;
            preset.intel.numericalPressureThreshold = shopping.IntelNumericalPressureThreshold;
            preset.intel.fireSupportGapHotThreshold = shopping.IntelFireSupportGapHotThreshold;
            preset.intel.fireSupportGapDamageThreshold = shopping.IntelFireSupportGapDamageThreshold;
            preset.intel.offensiveAntiInfantryFireThreshold = shopping.IntelOffensiveAntiInfantryFireThreshold;
            preset.intel.stalemateElitePressureThreshold = shopping.IntelStalemateElitePressureThreshold;
            preset.intel.stalemateFireSupportThreshold = shopping.IntelStalemateFireSupportThreshold;
            preset.intel.stalemateEliteCapturerFillRatio = shopping.StalemateEliteCapturerFillRatio;
            preset.intel.stalemateEliteCapturerRange = shopping.StalemateEliteCapturerRange;

            // --- aeronáutica
            preset.aeronautica.maxAirTransporters = shopping.MaxAirTransporters;
            preset.aeronautica.minTurnForInterceptador = shopping.MinTurnForInterceptador;
            preset.aeronautica.helicopterosPorCacaB = shopping.HelicopterosPorCacaB;
            preset.aeronautica.maxCacaB = shopping.MaxCacaB;
            preset.aeronautica.maxCacaA = shopping.MaxCacaA;
            preset.aeronautica.minTurnForAtaqueAereo = shopping.MinTurnForAtaqueAereo;
            preset.aeronautica.chinooksPorApache = shopping.ChinooksPorApache;
            preset.aeronautica.helicopterosInimigosPorApache = shopping.HelicopterosInimigosPorApache;
            preset.aeronautica.apachesParaBombardeiro = shopping.ApachesParaBombardeiro;
            preset.aeronautica.comprarApacheEmModoDefesa = shopping.ComprarApacheEmModoDefesa;
            preset.aeronautica.minCacaBPresence = shopping.MinCacaBPresence;
            preset.aeronautica.minApachePresence = shopping.MinApachePresence;
            preset.aeronautica.minBombaPresence = shopping.MinBombaPresence;
            preset.aeronautica.minTurnForAirSurveillance =
                shopping.MinTurnForAirSurveillance;
            preset.aeronautica.maxAirSurveillance =
                shopping.MaxAirSurveillance;
            preset.aeronautica.maxMobileAirSurveillance =
                shopping.MaxMobileAirSurveillance;
        }
    }

    // -------------------------------------------------------------------------------
    // Auditoria: prova que os valores divergem entre cenas — o argumento central para
    // tirar essa configuração da cena e colocá-la num asset.
    private static void AuditScenes()
    {
        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" });
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[AI][Preset][Auditoria] valores do AIController serializados por cena:");
        sb.AppendLine("(campo ausente = a cena é anterior ao campo e cai no default do código)");

        string[] watched =
        {
            "hardMode", "easyMode", "conscriptionDoctrine", "conscriptionWhenLosing",
            "defenseEnemyRange", "alliesAgainstEnemiesHpRatio",
            "eliteSaveTurnsHard", "eliteMaintenanceReserveHard", "maxActiveObjectives"
        };

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string text;
            try { text = File.ReadAllText(path); }
            catch { continue; }
            if (!text.Contains("AIController")) continue;

            sb.AppendLine($"  {Path.GetFileNameWithoutExtension(path)}");
            foreach (string field in watched)
            {
                int idx = text.IndexOf($"\n  {field}: ", System.StringComparison.Ordinal);
                if (idx < 0) { sb.AppendLine($"      {field}: <ausente — default do código>"); continue; }
                int start = idx + field.Length + 5;
                int end = text.IndexOf('\n', start);
                sb.AppendLine($"      {field}: {text.Substring(start, Mathf.Max(0, end - start)).Trim()}");
            }
        }

        Debug.Log(sb.ToString());
    }

    // -------------------------------------------------------------------------------
    private static bool IsHard(AIDifficulty d) => d == AIDifficulty.Dificil;
    private static bool IsEasy(AIDifficulty d) => d == AIDifficulty.Facil;
    // A conscricao deixou de ser escolhida pela dificuldade: virou toggle do perfil.
    private static bool IsDoctrine(AIDifficulty d) => false;
    private static bool IsWhenLosing(AIDifficulty d) => d == AIDifficulty.Dificil;

    private static int Int(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        return p != null ? p.intValue : 0;
    }

    private static float Flt(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        return p != null ? p.floatValue : 0f;
    }

    private static bool Bool(SerializedObject so, string field)
    {
        SerializedProperty p = so.FindProperty(field);
        return p != null && p.boolValue;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
