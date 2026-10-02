using UnityEngine;

// =====================================================================================
// Ponte AIController <-> AIPresetData.
//
// Modelo: UM baseline (editável como um UnitData) + a dificuldade como atalho que liga
// toggles por cima de uma CÓPIA do baseline. Não há asset por dificuldade.
//
// CAPACIDADES (os toggles): todas leem do preset ativo, com o campo da cena como reserva
// quando nao ha preset. Nenhum portao de comportamento pergunta mais "sou o dificil?".
//
// VALORES (fase 2, pendente): os numeros do preset ainda nao sao lidos. Os getters de
// valor do AIController e os campos do AIShoppingPlanner seguem vindo da cena.
// =====================================================================================
public partial class AIController
{
    [Header("Perfil de IA (Preset)")]
    [Tooltip("RESERVA: usado só quando o catálogo não tem entrada para a dificuldade (ou não há catálogo). Aí vira baseline + overlay de código. Com o catálogo cobrindo as três dificuldades, este campo não é consultado — o perfil em uso aparece no quadro do fim do Inspector.")]
    [InspectorName("Reserva (sem catálogo)")]
    [SerializeField] private AIPresetData basePreset;

    [Tooltip("Catalogo que diz qual perfil cada dificuldade usa. Com ele, o preset e AUTORADO por dificuldade e a overlay nao roda. Vazio = comportamento antigo (baseline + overlay).")]
    [SerializeField] private AIPresetCatalog presetCatalog;

    [System.NonSerialized] private AIPresetData activePreset;
    [System.NonSerialized] private AIDifficulty appliedDifficulty = AIDifficulty.Facil;
    [System.NonSerialized] private bool hasAppliedDifficulty;

    [System.NonSerialized] private string presetSource = string.Empty;

    public AIPresetData BasePreset => basePreset;

    public AIPresetCatalog PresetCatalog => presetCatalog;

    /// <summary>De onde o preset ativo veio: o nome do asset do catalogo, ou "baseline + overlay".</summary>
    public string PresetSource => presetSource;

    // ─────────────────────────────────────────────────────── capacidades ──
    //
    // Cada portao volta a perguntar O QUE ELE QUER: "faco handoff?", "respeito a lista
    // banida?". Antes todos perguntavam "eu sou o dificil?" — uma chave so com seis
    // lampadas, que impedia o perfil que o autor queria ("media faz blitzkrieg mas nao
    // respeita a lista banida").
    //
    // Sem preset ativo, cada uma responde o que o flag legado responderia: cena sem
    // asset nenhum continua identica, e a troca e rastreavel portao a portao.
    public bool RespeitaListaBanida =>
        activePreset != null ? activePreset.capacidades.respeitarListaBanida : hardMode;

    public bool ProjetaProducaoInimiga =>
        activePreset != null ? activePreset.capacidades.projetarProducaoInimiga : hardMode;

    public bool AbreComBlindado =>
        activePreset != null ? activePreset.capacidades.aberturaBlindadoPrimeiro : hardMode;

    public bool LimitaLogistica =>
        activePreset != null ? activePreset.capacidades.limitarLogistica : hardMode;

    public bool DobraSlotsDeCapturador =>
        activePreset != null ? activePreset.capacidades.dobrarSlotsCapturadorPorSetor : hardMode;

    /// <summary>O "blitzkrieg": a ponta nao para para terminar a captura, passa e segue.</summary>
    public bool FazHandoffEmProfundidade =>
        activePreset != null ? activePreset.capacidades.handoffEmProfundidade : hardMode;

    /// <summary>Quem pode fundir durante o reparo. Sem preset, a regra antiga: todos.</summary>
    public AIFusaoEmReparo FusaoEmReparo =>
        activePreset != null ? activePreset.capacidades.fusaoEmReparo : AIFusaoEmReparo.Todos;

    /// <summary>
    /// A pergunta unica de "esta unidade funde no reparo?". A flag da ficha continua
    /// sendo a permissao da peca; o perfil decide quem, dentre as permitidas, usa.
    /// Quem pondera fusao (o proprio reparo, e a logistica que adia quem vai fundir)
    /// pergunta aqui — duas respostas diferentes fariam o caminhao esperar uma fusao
    /// que o reparo nunca vai fazer.
    /// </summary>
    private bool PermiteFusaoEmReparo(UnitData data)
    {
        if (data == null || !data.fuseWhileInRepair)
            return false;

        switch (FusaoEmReparo)
        {
            case AIFusaoEmReparo.SoCapturador:
                return UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador);
            case AIFusaoEmReparo.Desligado:
                return false;
            default:
                return true;
        }
    }

    /// <summary>
    /// Quanto da renda de predios FORA das cidades esta IA recebe. 1 = tudo. O perfil
    /// facil historicamente recebia 1/3, e era o unico jeito de dizer isso — agora e um
    /// numero do perfil, e da para ter uma IA meio-pobre sem ser "a facil".
    /// </summary>
    public float FracaoRendaForaDeCidades =>
        activePreset != null
            ? Mathf.Clamp01(activePreset.economia.fracaoRendaForaDeCidades)
            : (EasyMode ? 1f / 3f : 1f);

    /// <summary>Preset já com a overlay da dificuldade aplicada. Cópia de runtime; nunca é o asset. Null se basePreset não estiver ligado na cena.</summary>
    public AIPresetData ActivePreset => activePreset;

    /// <summary>Dificuldade efetivamente aplicada. Antes de ApplyDifficulty, é reconstruída dos booleanos serializados na cena.</summary>
    public AIDifficulty AppliedDifficulty => hasAppliedDifficulty ? appliedDifficulty : InferDifficultyFromFlags();

    private void ResolveActivePreset(AIDifficulty difficulty)
    {
        appliedDifficulty = difficulty;
        hasAppliedDifficulty = true;

        // CAMINHO NOVO: o catalogo aponta um perfil autorado para esta dificuldade. A
        // overlay NAO roda — ela acende as capacidades em bloco a partir de hardMode, e
        // e justamente isso que impede "media com blitzkrieg mas sem lista banida".
        // O que esta escrito no asset e o que vale.
        if (presetCatalog != null && presetCatalog.TryGetPreset(difficulty, out AIPresetData doCatalogo))
        {
            activePreset = doCatalogo.CloneRuntime();
            presetSource = doCatalogo.name;

            if (showAILogs)
            {
                Debug.Log($"[AI][Preset] dificuldade={difficulty} " +
                          $"({AIPresetCatalog.RotuloDoJogador(difficulty)}) → perfil '{doCatalogo.name}' " +
                          "do catalogo, sem overlay (capacidades do perfil; valores ainda da cena)");
            }
            return;
        }

        if (basePreset == null)
        {
            activePreset = null;
            presetSource = string.Empty;
            return;
        }

        // RESERVA: o modelo antigo, baseline + overlay. Vale enquanto o catalogo nao
        // cobrir a dificuldade — cena sem catalogo continua identica.
        activePreset = basePreset.CloneRuntime();
        AIPresetData.ApplyDifficultyOverlay(activePreset, difficulty);

        // A overlay nao sabe destes quatro: eram toggles da CENA, nao da dificuldade.
        // Sem copiar, o caminho de reserva passaria a obedecer o asset baseline e mudaria
        // comportamento de quem nem usa catalogo. Aqui os campos ja refletem
        // ApplyDifficulty ou o save restaurado.
        activePreset.capacidades.conscricaoSempre = conscriptionDoctrine;
        activePreset.capacidades.conscricaoQuandoPerdendo = conscriptionWhenLosing;
        activePreset.capacidades.politicaLadoForteFraco = strongWeakSidePolitic;
        activePreset.capacidades.gateNucleoSuave = softCoreGate;
        presetSource = basePreset.name + " + overlay";

        if (showAILogs)
        {
            Debug.Log($"[AI][Preset] baseline={basePreset.name} dificuldade={difficulty} " +
                      $"→ preset ativo montado (capacidades da overlay + toggles da cena; valores ainda da cena)");
        }
    }

    /// <summary>
    /// Reconstrói a dificuldade a partir dos 4 booleanos. Necessário porque a dificuldade
    /// escolhida na Tela de Entrada não existe como campo — só como combinação de flags —
    /// e cenas abertas direto no Editor nunca passam por ApplyDifficulty.
    /// </summary>
    private AIDifficulty InferDifficultyFromFlags() => InferDifficulty(easyMode, hardMode);

    /// <summary>Dificuldade que os flags desta cena significam: o que o Play direto usa.</summary>
    public AIDifficulty SceneFlagsDifficulty => InferDifficultyFromFlags();

    /// <summary>
    /// De onde viria o perfil desta dificuldade, sem resolver nada. Mesma ordem de
    /// ResolveActivePreset: catalogo, depois reserva, depois nenhum.
    /// </summary>
    public string PreviewPresetSource(AIDifficulty difficulty)
    {
        if (presetCatalog != null && presetCatalog.TryGetPreset(difficulty, out AIPresetData doCatalogo))
            return $"{doCatalogo.name} (catálogo)";
        if (basePreset != null)
            return $"{basePreset.name} + overlay (reserva)";
        return "nenhum — flags da cena";
    }

    /// <summary>
    /// A regra unica de "que dificuldade estes flags significam". Publica para que
    /// ferramentas de auditoria (Save Inspector) respondam o mesmo que o load.
    /// </summary>
    public static AIDifficulty InferDifficulty(bool easy, bool hard)
    {
        // conscriptionDoctrine nao e mais escolhida por dificuldade: quem a liga na cena
        // esta somando doutrina a um perfil, nao trocando de perfil.
        if (hard) return AIDifficulty.Dificil;
        if (easy) return AIDifficulty.Facil;
        return AIDifficulty.Medio;
    }
}
