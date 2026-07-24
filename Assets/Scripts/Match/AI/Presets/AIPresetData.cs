using System;
using UnityEngine;

// =====================================================================================
// AIPresetData — perfil de doutrina da IA, no mesmo espírito do UnitData.
//
// FASE 1 (este arquivo): o asset EXISTE e é gerado a partir dos valores vivos da cena,
// mas NADA no runtime lê dele ainda. Comportamento inalterado, de propósito. O objetivo
// da fase 1 é tirar os números da cena e colocá-los num asset versionável e comparável.
//
// Os pares "normal/hard" do AIController (ex.: eliteRatioNormalPressure x
// eliteRatioHardPressure) colapsam num ÚNICO campo aqui — o preset já É o modo. Quem
// escolhe o valor é a escolha do preset, não um ternário no código.
//
// Ordem de migração planejada:
//   Fase 1  criar asset + gerar os 6 presets a partir da cena (aqui)
//   Fase 2  trocar as CONSULTAS DE VALOR para ler do preset
//   Fase 3  trocar as PORTAS DE COMPORTAMENTO (if HardMode) por capacidades nomeadas
//   Fase 4  remover os 4 booleanos e migrar save/load
// =====================================================================================

// -------------------------------------------------------------------------------------
// Capacidades: as portas que hoje perguntam "eu sou o difícil?" quando queriam perguntar
// "eu faço tal coisa?". Enquanto forem derivadas de hardMode, nenhum perfil pode ter uma
// sem ter todas — é isso que impede um general que abre com blindado mas não projeta
// produção inimiga.
// -------------------------------------------------------------------------------------
[Serializable]
public class AICapabilityPreset
{
    [Header("Compras — o que a IA se permite comprar")]
    [Tooltip("LIGADO: nunca compra unidades marcadas como 'banidas no difícil' (ex.: as mais baratas e fracas).\n" +
             "DESLIGADO: pode comprar qualquer coisa que o produtor ofereça.\n" +
             "Use ligado para forçar um exército de melhor qualidade.\n\n" +
             "(código: IsHardModeBannedForAI)")]
    public bool respeitarListaBanida = false;

    [Tooltip("LIGADO: ao medir se está ganhando ou perdendo, soma a produção que os produtores inimigos VÃO fazer no próximo turno — não só o que já existe.\n" +
             "DESLIGADO: conta apenas as forças que já estão no tabuleiro.\n" +
             "Ligado deixa a IA mais precavida, reagindo antes de a ameaça nascer.\n\n" +
             "(código: enemyProducersProjected)")]
    public bool projetarProducaoInimiga = false;

    [Header("Abertura de jogo")]
    [Tooltip("LIGADO: os primeiros turnos priorizam comprar um BLINDADO antes de qualquer elite, e o dinheiro é poupado até ele caber.\n" +
             "DESLIGADO: segue a demanda normal do plano desde o início.\n" +
             "Ligado dá uma abertura mais agressiva e sólida no chão.\n\n" +
             "(código: ApplyHardModeArmorFirst + ComputeBlitzFirstArmorReserve)")]
    public bool aberturaBlindadoPrimeiro = false;

    [Header("Logística")]
    [Tooltip("LIGADO: limita quantos caminhões/supridores terrestres a IA mantém em campo (o teto está na seção Composição).\n" +
             "DESLIGADO: compra logística conforme a demanda, sem limite fixo.\n" +
             "Ligado evita que a IA gaste demais em suprimento e de menos em combate.\n\n" +
             "(código: ApplyHardModeLogisticsCap)")]
    public bool limitarLogistica = false;

    [Header("Plano de captura")]
    [Tooltip("LIGADO: cada setor pede o DOBRO de capturadores no plano (com teto próprio). Mais tropa dedicada a tomar território.\n" +
             "DESLIGADO: um conjunto normal de capturadores por setor.\n" +
             "Ligado acelera a conquista do mapa, ao custo de gastar mais em infantaria.\n\n" +
             "(código: slots*2 no PlanEvaluator)")]
    public bool dobrarSlotsCapturadorPorSetor = false;

    [Tooltip("LIGADO: a unidade da ponta NÃO para para terminar a captura — ela passa o prédio para um seguidor e segue avançando no eixo.\n" +
             "DESLIGADO: quem chega no prédio fica ali até capturar.\n" +
             "Ligado mantém o avanço fluido; desligado consolida antes de seguir.\n\n" +
             "(código: PlanEvaluator.Handoff)")]
    public bool handoffEmProfundidade = false;

    // -------------------------------------------------------------------------------
    // POLÍTICA DE ORÇAMENTO — o 2×2 (PISO × TETO)
    //
    //   PISO  = gastaEmNumeros (conscricaoSempre): todo produtor de exército compra o
    //           corpo mais barato todo turno. Garante massa na frente.
    //   TETO  = poupaPraElite: guarda a sobra ACIMA do piso para comprar elite caro.
    //
    // As duas são INDEPENDENTES. Combinando:
    //   poupa ON  + gasta OFF → atual: compra por demanda e junta caixa; pode até passar
    //                           a vez sem comprar nada enquanto poupa.
    //   poupa OFF + gasta OFF → guloso oportunista: compra o que o plano pede quando dá,
    //                           não poupa e não preenche produtor ocioso.
    //   poupa ON  + gasta ON  → SD todo turno + guarda a sobra p/ elite; sem alvo elite,
    //                           a sobra compra unidades do plano.
    //   poupa OFF + gasta ON  → guloso total: SD garantido + torra a sobra no plano,
    //                           nunca deixa produtor parado.
    //
    // Aeroporto e porto NÃO entram no piso — só compram por demanda (inclusive elite).
    // -------------------------------------------------------------------------------
    [Header("Política de Orçamento — PISO × TETO (o 2×2)")]
    [Tooltip("PISO — 'gasta em números' (Doutrina do Enxame).\n" +
             "LIGADO: todo produtor DO EXÉRCITO compra o corpo mais barato TODO turno — nenhum produtor terrestre fica ocioso. Aeroporto e porto NÃO entram (só compram por demanda).\n" +
             "DESLIGADO: compra só o que o plano pedir, podendo deixar produtor parado.\n" +
             "Garante massa na frente. O que fazer com a SOBRA acima do piso é decidido pelo TETO (Poupa Pra Elite) logo abaixo.\n\n" +
             "(código: conscriptionDoctrine / ComputeConscriptionTax)")]
    public bool conscricaoSempre = false;

    [Tooltip("PISO condicional.\n" +
             "LIGADO: o piso de massa barata só liga quando a IA está PERDENDO o macro (defesa emergencial).\n" +
             "DESLIGADO: sem gatilho de emergência.\n" +
             "É a versão 'só quando aperta' do piso acima.\n\n" +
             "(código: conscriptionWhenLosing)")]
    public bool conscricaoQuandoPerdendo = false;

    [Tooltip("TETO — 'poupa pra elite'. Interruptor-mestre da poupança estratégica.\n" +
             "LIGADO: guarda a sobra (acima do piso) para comprar um elite caro quando o núcleo estiver pronto. QUANTOS turnos ela topa poupar está no slider 'Elite Save Turns' (seção Economia).\n" +
             "DESLIGADO: nunca poupa — a sobra é torrada nas unidades do plano (guloso). Equivale a Elite Save Turns = 0.\n" +
             "Este é o outro eixo do 2×2 com o PISO acima. Veja o comentário do bloco para as 4 combinações.\n\n" +
             "(código: ComputeStrategicSavingReserve / eliteSaveTurns > 0)")]
    public bool poupaPraElite = true;

    [Header("Elite — como decidir e priorizar unidades caras")]
    [Tooltip("PROTÓTIPO — em desenvolvimento. LIGADO: classifica os eixos de ataque em forte/equilibrado/fraco e manda mais elite para o lado FORTE (dobra a aposta onde já está ganhando).\n" +
             "DESLIGADO: distribui elite sem esse viés.\n" +
             "Experimental; pode ter comportamento incompleto.\n\n" +
             "(código: strongWeakSidePolitic)")]
    public bool politicaLadoForteFraco = false;

    [Tooltip("O 'núcleo' é o exército-base mínimo (alguns capturadores, assalto e artilharia) que a IA quer ter ANTES de gastar em elite caro. O 'gate' é a trava que segura o elite até o núcleo estar formado.\n\n" +
             "LIGADO (gate SUAVE): a maturidade do núcleo (de 0 a 100%) vira um PESO — quanto mais formado, mais o elite é favorecido. Transição gradual.\n" +
             "DESLIGADO (gate DURO): tudo-ou-nada. Enquanto o núcleo não fecha, o elite é proibido — e, pior, o freio anti-compra-ruim também desliga junto, o que empurra a IA a comprar a PIOR artilharia só para destravar o gate.\n" +
             "Recomendado LIGADO: o modo duro tem esse efeito perverso conhecido.\n\n" +
             "(código: AIController.softCoreGate)")]
    public bool gateNucleoSuave = false;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AIEconomyPreset
{
    [Tooltip("Fração da renda recebida por construções que NÃO são cidades. 1 = renda cheia. O Iniciante usa 1/3. Hoje isso é o easyMode com a fração fixa no MatchController.")]
    [Range(0f, 1f)] public float fracaoRendaForaDeCidades = 1f;

    [Tooltip("Quantos turnos de renda a IA topa POUPAR mirando um elite/capacidade. 0 = nunca poupa (guloso).\n" +
             "É a MAGNITUDE do TETO 'Poupa Pra Elite' (seção Política de Orçamento): esse check liga/desliga a poupança, este número diz por quantos turnos. Se o check estiver desligado, trate como 0.\n\n" +
             "(código: eliteSaveTurnsNormal/Hard)")]
    [Range(0, 4)] public int eliteSaveTurns = 1;

    [Tooltip("Margem de caixa (%) mantida como troco enquanto poupa para elite, para ainda comprar coisas baratas. Origem: eliteMaintenanceReserveNormal/Hard.")]
    [Range(0f, 50f)] public float eliteMaintenanceReservePercent = 20f;

    [Tooltip("Percentual da renda reservado para elite no shopping. Origem: AIShoppingPlanner.SavingPercentualForElite.")]
    [Range(0f, 20f)] public float savingPercentualForElite = 15f;

    [Tooltip("Pressão líquida (escala 0..1 do Unit Analysis) a partir da qual a IA para de repetir counters baratos e poupa para um counter elite. Origem: CounterEliteEscalationPressure.")]
    [Range(0.2f, 10f)] public float counterEliteEscalationPressure = 3.2f;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AICompositionPreset
{
    [Header("Núcleo operacional (Elite Gate)")]
    [Tooltip("Capturadores (infantaria) exigidos antes de liberar compra de elite. Origem: minInfantryNormal/Hard.")]
    [Range(0, 12)] public int coreMinInfantry = 2;
    [Tooltip("Unidades de Assalto exigidas. Origem: minAssaultNormal/Hard. 0 = não exige (no Hard o tank básico costuma estar banido).")]
    [Range(0, 12)] public int coreMinAssault = 2;
    [Tooltip("Unidades de Artilharia/Fogo Indireto exigidas. Origem: minArtilleryNormal/Hard.")]
    [Range(0, 12)] public int coreMinArtillery = 1;

    [Header("Razões de elite")]
    [Tooltip("Fatia de elite entre Assalto e Fogo Indireto enquanto há pressão terrestre aberta. Origem: eliteRatioNormalPressure/HardPressure.")]
    [Range(0f, 1f)] public float eliteRatioPressure = 0.33f;
    [Tooltip("Fatia de elite quando anti-tank E anti-infantaria já estão cobertas (folga vira superioridade qualitativa). Origem: eliteRatioNormalSafe/HardSafe.")]
    [Range(0f, 1f)] public float eliteRatioSafe = 0.50f;
    [Tooltip("Fração de preenchimento de capturadores que libera compra de elite. Origem: EliteCapturerFillRatio.")]
    [Range(0f, 1f)] public float eliteCapturerFillRatio = 0.6f;
    [Tooltip("Slots de assalto preenchidos exigidos. Origem: MinFilledAssaultSlots.")]
    [Range(0, 5)] public int minFilledAssaultSlots = 1;

    [Header("Massa antes do suporte")]
    [Tooltip("Turno mínimo para liberar demanda de fire support. Origem: MinTurnForFireSupport.")]
    [Range(1, 12)] public int minTurnForFireSupport = 3;
    [Tooltip("Origem: MinActiveCapturersForFireSupport.")]
    [Range(0, 8)] public int minActiveCapturersForFireSupport = 2;
    [Tooltip("Origem: MinActiveAssaultForFireSupport.")]
    [Range(0, 5)] public int minActiveAssaultForFireSupport = 1;
    [Tooltip("Massa mínima de capturadores ativos antes de liberar demanda de suporte. Forma a base primeiro. Origem: MinCapturerMassForSupport.")]
    [Range(0, 8)] public int minCapturerMassForSupport = 4;
    [Tooltip("Origem: AssaultPerFireSupportRatio.")]
    [Range(1, 4)] public int assaultPerFireSupportRatio = 2;
    [Tooltip("Origem: CapturersPerPreventiveTransport.")]
    [Range(2, 8)] public int capturersPerPreventiveTransport = 4;
    [Tooltip("Origem: ProgressiveCapturerBatchSize.")]
    [Range(1, 4)] public int progressiveCapturerBatchSize = 2;

    [Header("Tetos")]
    [Tooltip("Teto de logística terrestre em campo. Só aplicado quando a capacidade 'limitarLogistica' está ligada. Origem: maxLogisticUnitsOnHardMode.")]
    [Range(0, 10)] public int maxLogisticUnits = 1;
    [Tooltip("Origem: RepairsPerGroundSupplier.")]
    [Range(1, 8)] public int repairsPerGroundSupplier = 2;
    [Tooltip("Prioridade da demanda de logística quando há um ELITE ferido (menor = mais cedo). Origem: EliteRepairLogisticsPriority.")]
    [Range(1, 20)] public int eliteRepairLogisticsPriority = 7;
    [Tooltip("Origem: MaxProactiveDefensiveFireSupport.")]
    [Range(0, 2)] public int maxProactiveDefensiveFireSupport = 1;
    [Tooltip("Origem: MaxProactiveAntiAirSAM.")]
    [Range(0, 5)] public int maxProactiveAntiAirSAM = 3;
    [Tooltip("Origem: AntiAirCoverageRange.")]
    [Range(2, 10)] public int antiAirCoverageRange = 5;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AIConscriptionPreset
{
    [Tooltip("Fase de Massacre: com vantagem numérica clara a doutrina cessa a massa e o caixa volta pro elite. Entra quando o ForceRatio macro atinge este valor (0.66 = ~2:1). Origem: massacreEnterForceRatio.")]
    [Range(0.5f, 1f)] public float massacreEnterForceRatio = 0.66f;

    [Tooltip("Histerese: uma vez ativa, a Fase de Massacre só volta à conscrição se o ForceRatio cair abaixo deste valor. Origem: massacreExitForceRatio.")]
    [Range(0.5f, 1f)] public float massacreExitForceRatio = 0.58f;

    [Tooltip("Válvula de teto: cessa a massa quando as unidades em campo atingem esta fração do limite por time. Origem: massacreUnitCapFillRatio.")]
    [Range(0.5f, 1f)] public float massacreUnitCapFillRatio = 0.85f;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AIPlannerPreset
{
    [Tooltip("Máximo de objetivos ofensivos simultâneos (Pending/Pursuing/Capturing). Origem: maxActiveObjectives.")]
    [Range(1, 12)] public int maxActiveObjectives = 4;

    [Tooltip("Multiplicador dos slots de capturador por setor. 1 = normal, 2 = a antiga regra do Hard Mode. Origem: PlanEvaluator (slots * 2).")]
    [Range(1, 3)] public int capturerSlotsPerSectorMultiplier = 1;

    [Tooltip("Teto absoluto de slots de capturador por setor, depois do multiplicador. Hoje é const no código: 4 normal, HardModeCapturerSlotCap = 6.")]
    [Range(1, 12)] public int capturerSlotCap = 4;

    [Tooltip("Distância mínima em hexes do QG para um setor gerar slot de transportador no plano e no shopping. Origem: minDistanceForTransportSlot.")]
    [Range(1, 30)] public int minDistanceForTransportSlot = 7;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AITacticsPreset
{
    [Header("Risco")]
    [Tooltip("Impacto do risco na tomada de decisão (0 = ignora risco, 2 = risco pesa muito). Origem: riskDecisionImpact.")]
    [Range(0f, 2f)] public float riskDecisionImpact = 0.5f;

    [Header("Objetivo defensivo")]
    [Tooltip("Raio em hexes para criar objetivo defensivo num setor conquistado quando há inimigo próximo. Origem: defenseEnemyRange.")]
    [Range(1, 8)] public int defenseEnemyRange = 3;
    [Tooltip("Rogues dentro deste raio são convocados para defesa. Origem: defenseCallRange.")]
    [Range(1, 8)] public int defenseCallRange = 4;

    [Header("Reforço SOS")]
    [Tooltip("Raio para detectar inimigos perto do alvo de captura e acionar reforço. Origem: alliesEnemyRange.")]
    [Range(1, 8)] public int alliesEnemyRange = 2;
    [Tooltip("Raio para recrutar rogues como reforço SOS. Origem: alliesCallRange.")]
    [Range(1, 16)] public int alliesCallRange = 8;
    [Tooltip("Ratio HP inimigo / HP aliado que aciona o reforço SOS (2 = inimigo tem o dobro). Origem: alliesAgainstEnemiesHpRatio.")]
    [Range(1f, 5f)] public float alliesAgainstEnemiesHpRatio = 2f;

    [Header("Defesa de base")]
    [Tooltip("Origem: MinBaseArtilharia.")]
    [Range(0, 3)] public int minBaseArtilharia = 1;
    [Tooltip("Origem: MinBaseAAA.")]
    [Range(0, 3)] public int minBaseAAA = 1;
    [Tooltip("Origem: MinTurnBaseDefense.")]
    [Range(1, 12)] public int minTurnBaseDefense = 3;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AIIntelPreset
{
    [Tooltip("Origem: usarIntelJogadasNoShopping.")]
    public bool usarIntelJogadasNoShopping = true;
    [Range(1, 8)] public int lookbackTurns = 4;
    [Range(0f, 10f)] public float infantryPressureAssaultThreshold = 1.5f;
    [Range(0f, 10f)] public float airThreatAntiAirThreshold = 2f;
    [Range(0f, 10f)] public float armorThreatDefenseThreshold = 2f;
    [Range(0f, 10f)] public float capturePressureDefenseThreshold = 2f;
    [Range(0f, 10f)] public float numericalPressureThreshold = 1.5f;
    [Range(0f, 30f)] public float fireSupportGapHotThreshold = 8f;
    [Range(0f, 20f)] public float fireSupportGapDamageThreshold = 3f;
    [Range(0f, 10f)] public float offensiveAntiInfantryFireThreshold = 2.5f;
    [Range(0f, 10f)] public float stalemateElitePressureThreshold = 3.5f;
    [Range(0f, 10f)] public float stalemateFireSupportThreshold = 6f;
    [Range(0f, 1f)] public float stalemateEliteCapturerFillRatio = 0.3f;
    [Range(1, 12)] public int stalemateEliteCapturerRange = 8;
}

// -------------------------------------------------------------------------------------
[Serializable]
public class AIAirPreset
{
    [Range(1, 8)] public int maxAirTransporters = 3;
    [Range(1, 12)] public int minTurnForInterceptador = 4;
    [Range(1, 6)] public int helicopterosPorCacaB = 3;
    [Range(0, 6)] public int maxCacaB = 4;
    [Range(0, 4)] public int maxCacaA = 2;
    [Range(1, 12)] public int minTurnForAtaqueAereo = 5;
    [Range(1, 4)] public int chinooksPorApache = 2;
    [Range(1, 6)] public int helicopterosInimigosPorApache = 3;
    [Range(1, 4)] public int apachesParaBombardeiro = 2;
    public bool comprarApacheEmModoDefesa = true;
    [Range(0, 4)] public int minCacaBPresence = 1;
    [Range(0, 4)] public int minApachePresence = 1;
    [Range(0, 2)] public int minBombaPresence = 0;
    [Range(1, 12)] public int minTurnForIntel = 4;
    [Range(0, 3)] public int maxAirIntel = 1;
    [Range(0, 3)] public int maxMobileAirIntel = 1;
}

// =====================================================================================
// AIPresetData — UM asset baseline, editado como um UnitData: um saco de toggles e valores.
//
// NÃO existe um asset por dificuldade. A dificuldade é um ATALHO que liga toggles e ajusta
// alguns valores POR CIMA de uma cópia deste baseline (ver ApplyDifficultyOverlay). Assim
// o designer edita um lugar só; o jogador continua escolhendo "Fácil/Médio/Difícil" na Tela
// de Entrada, mas isso não gera 6 assets divergentes para manter.
// =====================================================================================
[CreateAssetMenu(fileName = "AIPreset_", menuName = "Game/AI/AI Preset", order = 0)]
public class AIPresetData : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Nome deste baseline. Ex.: \"Padrão\" ou, no futuro, um general (\"Azul — a Fortaleza\").")]
    public string nomeExibido = "Padrão";

    [Tooltip("A doutrina em uma frase. É aqui que os documentos de perfil encostam no código.")]
    [TextArea(2, 6)] public string doutrina = "";

    [Header("Seções")]
    public AICapabilityPreset capacidades = new AICapabilityPreset();
    public AIEconomyPreset economia = new AIEconomyPreset();
    public AICompositionPreset composicao = new AICompositionPreset();
    public AIConscriptionPreset conscricao = new AIConscriptionPreset();
    public AIPlannerPreset plano = new AIPlannerPreset();
    public AITacticsPreset tatica = new AITacticsPreset();
    public AIIntelPreset intel = new AIIntelPreset();
    public AIAirPreset aeronautica = new AIAirPreset();

    /// <summary>Cópia de runtime (não salva em disco). A overlay de dificuldade sempre roda sobre uma cópia, nunca sobre o asset.</summary>
    public AIPresetData CloneRuntime()
    {
        AIPresetData clone = Instantiate(this);
        clone.name = name + " (runtime)";
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    // -------------------------------------------------------------------------------
    // Overlay de dificuldade: a ÚNICA tabela que diz o que cada dificuldade muda em cima
    // do baseline. É código de propósito — é uma tabela pequena, tocada raramente, num
    // lugar só; virar 6 assets foi o erro que esta reestruturação desfez.
    //
    // Espelha exatamente o que ApplyDifficulty faz hoje com os 4 booleanos, MAIS os
    // valores que o antigo par normal/hard trocava. Enquanto a fase 1 durar, os booleanos
    // do AIController seguem sendo a fonte de verdade do runtime; esta overlay só prepara
    // o preset ativo para inspeção e para a fase 2.
    // -------------------------------------------------------------------------------
    public static void ApplyDifficultyOverlay(AIPresetData target, AIDifficulty difficulty)
    {
        if (target == null)
            return;

        bool hard = difficulty == AIDifficulty.Competitiva || difficulty == AIDifficulty.Agressiva;
        bool easy = difficulty == AIDifficulty.Iniciante;
        bool conscricaoSempre = difficulty == AIDifficulty.Formigueiro || difficulty == AIDifficulty.Agressiva;
        bool conscricaoPerdendo = difficulty == AIDifficulty.Medio || difficulty == AIDifficulty.Competitiva;

        // --- capacidades (os toggles que o hardMode antes acendia em bloco) ---
        target.capacidades.respeitarListaBanida = hard;
        target.capacidades.projetarProducaoInimiga = hard;
        target.capacidades.aberturaBlindadoPrimeiro = hard;
        target.capacidades.limitarLogistica = hard;
        target.capacidades.dobrarSlotsCapturadorPorSetor = hard;
        target.capacidades.handoffEmProfundidade = hard;
        target.capacidades.conscricaoSempre = conscricaoSempre;
        target.capacidades.conscricaoQuandoPerdendo = conscricaoPerdendo;
        // TETO do 2×2: derivado da magnitude de poupança que a overlay/baseline já definiu.
        // Poupa turnos > 0 → o interruptor liga; 0 → guloso (nunca poupa).
        target.capacidades.poupaPraElite = target.economia.eliteSaveTurns > 0;

        // --- economia ---
        target.economia.fracaoRendaForaDeCidades = easy ? 1f / 3f : 1f;

        // --- valores que o antigo par normal/hard trocava (só sobem no hard) ---
        // O baseline guarda os valores NORMAL; a overlay hard os substitui. Se você quiser
        // um general que abre com blindado mas mantém composição leve, é aqui que a fase 3
        // vai deixar granular — hoje espelhamos o comportamento atual.
        if (hard)
        {
            target.composicao.eliteRatioPressure = 0.60f;
            target.composicao.eliteRatioSafe = 0.80f;
            target.composicao.coreMinInfantry = 4;
            target.composicao.coreMinAssault = 0;
            target.composicao.coreMinArtillery = 0;
            target.plano.capturerSlotsPerSectorMultiplier = 2;
            target.plano.capturerSlotCap = 6;
        }
    }
}
