using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Banda em que a construcao caiu. Mesmo vocabulario do QueroCaronaReach: nao
/// existe banda estrategica no envelope, e "alem do Operational" e uma
/// classificacao do consumidor, nao um terceiro envelope.
/// </summary>
public enum MelhorCapturaTier
{
    Tactical,
    Operational,
    BeyondOperational
}

/// <summary>
/// Uma construcao classificada para UMA unidade. E um fato da consulta, nao uma
/// ordem para ir capturar.
/// </summary>
public sealed class MelhorCapturaAlvoScore
{
    public ConstructionManager construction;
    public Vector3Int cell;
    public MelhorCapturaTier tier;
    public PodeCapturarSensor.CaptureOperationType operation;

    /// <summary>Custo real de MP ate a celula. -1 fora do envelope.</summary>
    public int routeCost = -1;

    /// <summary>Distancia cubica. Sempre preenchida.</summary>
    public int cubicDistance;

    /// <summary>O custo que entrou na conta: rota dentro, cubica fora.</summary>
    public int effectiveCost;

    /// <summary>
    /// Quanto ESTA unidade tira (ou repoe) por turno nesta construcao. Vem do
    /// PodeCapturar, na sobrecarga que ja aplica a penalidade de pre-requisito
    /// — a mesma que a execucao usa em TurnStateManager.Capture.
    /// </summary>
    public int capturePower;

    /// <summary>Penalidade de pre-requisito (50%) incidiu no poder acima.</summary>
    public bool prerequisitePenalty;

    /// <summary>
    /// Pontos que faltam para concluir. A conta muda com a operacao, e por isso
    /// nao da para deduzir de fora: CaptureEnemy derruba ate zero (falta o
    /// atual), RecoverAlly repoe ate o teto (falta max - atual).
    /// </summary>
    public int remainingCapturePoints;

    /// <summary>
    /// Turnos parado em cima ate concluir. -1 quando o poder e zero e a captura
    /// nunca fecharia.
    /// </summary>
    public int turnsToCapture = -1;

    /// <summary>
    /// Capturador parado em cima desta construcao, se houver. E FATO, nao
    /// veredito: o alvo continua pontuado. Quem organiza ja e atraido para o
    /// capturador e sabe o que fazer com a informacao — e se quiser mesmo
    /// remover, existe `skipConstructionsWithCapturer`.
    /// </summary>
    public UnitManager capturerOnCell;

    /// <summary>Ajuste do chamador. Zero quando ninguem opinou.</summary>
    public float adjustment;

    public int displayScore;
    public float score;
    public string reason;
}

public sealed class MelhorCapturaReject
{
    public ConstructionManager construction;
    public Vector3Int cell;
    public string reason;
}

public sealed class MelhorCapturaRequest
{
    public UnitManager unit;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;

    /// <summary>Zero cai para o MP maximo da ficha.</summary>
    public int tacticalBudget;
    public int operationalTurns = 2;

    /// <summary>
    /// Como medir. Terrestre (caminhos) e o padrao; quem quiser a regua linear
    /// pede Aereo. Aeronave resolve cubica sozinha de qualquer jeito.
    /// </summary>
    public ReachSubStep subStep = ReachSubStep.Terrestre;

    /// <summary>
    /// Conjunto de candidatas. NULO cai para ConstructionManager.AllActive, que
    /// e o registro de runtime. Ferramenta de Editor passa a varredura da cena.
    /// </summary>
    public IReadOnlyList<ConstructionManager> constructions;

    /// <summary>
    /// Filtro de politica do chamador — setor, tipo de predio, o que for. A
    /// consulta continua classificando pelo envelope e validando pelo
    /// PodeCapturar; quem escolhe o conjunto e quem chamou.
    /// </summary>
    public Func<ConstructionManager, bool> includeConstruction;

    /// <summary>
    /// Ajuste de nota do chamador, somado ao score. E por AQUI que entra
    /// "outro capturador ja reservou este predio": a reserva e politica de
    /// quem organiza, o servico so nao pode fingir que ela nao existe.
    ///
    /// Escala, para o ajuste significar o que voce quer:
    ///   -1 a -99          desempata dentro da mesma banda e do mesmo custo
    ///   -100 por hex      equivale a ficar um hex mais longe
    ///   -100.000          derruba uma banda inteira (Tactical vira Operational)
    ///   float.MinValue/2  enterra o alvo sem removê-lo da lista
    ///
    /// Devolver 0 e o mesmo que nao informar. O servico nao sabe o que o numero
    /// significa, e nao deve passar a saber.
    /// </summary>
    public Func<ConstructionManager, float> evaluateAdjustment;

    public MatchController matchController;

    /// <summary>
    /// A CONSULTA PASSA TUDO. Desligado por padrao: nevoa nao e regra de
    /// captura, e recorte do que o time enxerga, e cruzar o alcance com o que
    /// se conhece e trabalho de quem organiza. Uma consulta que ja chega
    /// recortada nao consegue responder "vale a pena ir descobrir aquilo?",
    /// porque o alvo sumiu antes de ser pontuado.
    ///
    /// Ligue para ver exatamente o que a nevoa cortaria — mesmo uso dos
    /// `enable*` da Hotzone, que tambem nascem desligados.
    /// </summary>
    public bool applyFogOfWar;

    /// <summary>
    /// Remove da lista a construcao que ja tem um capturador em cima.
    /// Desligado por padrao, pelo mesmo motivo da nevoa: e recorte, nao regra.
    /// Ligado ou nao, o ocupante sempre sai reportado em `capturerOnCell` —
    /// o fato e da consulta, o veredito e de quem organiza.
    /// </summary>
    public bool skipConstructionsWithCapturer;

    public Action<string> diagnosticLog;
}

public sealed class MelhorCapturaResult
{
    public readonly List<MelhorCapturaAlvoScore> ranking =
        new List<MelhorCapturaAlvoScore>();
    public readonly List<MelhorCapturaReject> rejected =
        new List<MelhorCapturaReject>();

    public Vector3Int origin;
    public int tacticalBudget;
    public int operationalBudget;

    /// <summary>Quantas o conjunto de entrada trouxe, antes de qualquer corte.</summary>
    public int candidatesOffered;

    /// <summary>
    /// Quantas o `includeConstruction` do chamador cortou. Contado, e nao
    /// descartado em silencio: sem este numero nao da para separar "o filtro
    /// tirou" de "o PodeCapturar recusou", e as duas somem do mesmo jeito.
    /// </summary>
    public int candidatesFilteredOut;

    /// <summary>Quantas sobreviveram ao filtro e foram de fato avaliadas.</summary>
    public int candidatesVisited;

    public bool hasCaptureSkill;

    public MelhorCapturaAlvoScore best =>
        ranking.Count > 0 ? ranking[0] : null;

    /// <summary>Quantas cairam em cada banda. Le-se antes de olhar o ranking.</summary>
    public int CountInTier(MelhorCapturaTier tier)
    {
        int total = 0;
        for (int i = 0; i < ranking.Count; i++)
        {
            if (ranking[i].tier == tier)
                total++;
        }
        return total;
    }

    public string BuildRejectedSummary(int maxReasons = 4)
    {
        if (rejected.Count <= 0)
            return "nenhuma recusa registrada";

        var counts = new Dictionary<string, int>();
        for (int i = 0; i < rejected.Count; i++)
        {
            string reason = rejected[i]?.reason;
            if (string.IsNullOrWhiteSpace(reason))
                reason = "motivo vazio";
            counts.TryGetValue(reason, out int count);
            counts[reason] = count + 1;
        }

        var ordered = new List<KeyValuePair<string, int>>(counts);
        ordered.Sort((a, b) =>
        {
            int byCount = b.Value.CompareTo(a.Value);
            return byCount != 0
                ? byCount
                : string.CompareOrdinal(a.Key, b.Key);
        });

        int limit = Mathf.Min(Mathf.Max(1, maxReasons), ordered.Count);
        var parts = new List<string>(limit);
        for (int i = 0; i < limit; i++)
            parts.Add($"{ordered[i].Value}x {ordered[i].Key}");
        return string.Join(" | ", parts);
    }
}

/// <summary>
/// Consulta pura: dada UMA unidade e um conjunto de construcoes, quais ela pode
/// capturar e em que ordem.
///
/// O QUE ELE NAO SABE, e nao pode passar a saber: se a unidade tem plano, qual
/// o setor dela, se a faccao tem QG, qual o papel dela. Nada disso entra —
/// papel e plano sao do chamador, e o unico canal e o conjunto de candidatas
/// mais o filtro `includeConstruction`. Um setor chega aqui como "estas quatro
/// construcoes", nunca como "o setor C".
///
/// A PERMISSAO NAO E DAQUI. Quem responde "esta unidade captura esta
/// construcao" e o PodeCapturarSensor, projetado na celula da construcao. O
/// servico nao le skill, nao compara TeamId e nao reimplementa nenhum
/// predicado de elegibilidade — se a regra mudar no sensor, muda aqui de graca.
///
/// DUAS UNIDADES RECEBEM O MESMO PREDIO, E ISSO ESTA CERTO. Se voce perguntar
/// pelo soldado#2 e pelo bazooka#72, os dois podem vir com o predio#29 no topo.
/// Nao e conflito: e o INSUMO do matching 1:1, que mora um andar acima, no
/// CaptureOpportunityClaimService. Pra maximizar quantos capturadores ficam com
/// alvo, o matcher precisa da lista de preferencias INTEIRA de cada um —
/// entregar "seu predio exclusivo" daqui produziria guloso, nao otimo: quem
/// perguntou primeiro leva, e o segundo fica sem alvo mesmo quando a troca
/// serviria os dois.
///
/// O ERRO A NAO COMETER: chamar este servico do handler da unidade e AGIR na
/// hora, uma unidade por vez. Isso e thrash garantido. A reserva e do snapshot
/// compartilhado — um por slot, cacheado por (slot, plano, revisao confirmada)
/// —, e todo capturador le o mesmo. Se voce precisa que a nota caia por causa
/// de uma reserva ja feita, o canal e `evaluateAdjustment`, nao um filtro aqui.
///
/// A NEVOA NAO E DAQUI. A consulta passa tudo: cruzar o resultado com o que o
/// time enxerga, e descartar o que ele nao deveria saber, e trabalho de quem
/// organiza. Recortar aqui destruiria a unica pergunta que so este servico
/// responde — "vale a pena ir descobrir aquilo?" — porque o alvo sumiria antes
/// de receber nota. Ver `applyFogOfWar`, que nasce desligado.
///
/// O ALCANCE NAO E DAQUI. As bandas Tactical e Operational vem do
/// UnitReachEnvelopeService na intencao Capture. Fora das duas bandas o servico
/// nao abre pathfinding: usa distancia cubica, porque a essa altura a resposta
/// util e "para que lado" e nao "por qual estrada".
///
/// Ver docs/AI Behavior/contrato_envelope_alcance.md.
/// </summary>
public static class MelhorCapturaService
{
    // Pesos do mesmo formato do MelhorDesembarque: a banda domina por ordem de
    // grandeza e o custo desempata dentro dela. O `score` e agregado de
    // exibicao — quem ordena de verdade e o CompareAlvo, que compara os termos
    // reais e nao o float.
    private const float TierTerm = 100000f;
    private const float CostTerm = 100f;

    public static MelhorCapturaResult Evaluate(MelhorCapturaRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.unit,
            "melhorCaptura");
        AIDecisionPerf.AddCount("MelhorCapturaCalls");

        var result = new MelhorCapturaResult();
        if (request?.unit == null
            || request.map == null
            || request.terrainDatabase == null)
        {
            request?.diagnosticLog?.Invoke(
                "unidade, tabuleiro ou catalogo de terrenos ausente");
            return result;
        }

        UnitManager unit = request.unit;

        // Fonte de verdade da habilitacao. O nome da skill mora no
        // PodeCapturarSensor; ler UnitData aqui seria uma segunda copia da
        // regra, e um UnitData novo com a skill deixaria de funcionar sozinho.
        result.hasCaptureSkill =
            PodeCapturarSensor.HasCaptureConstructionSkill(unit);
        if (!result.hasCaptureSkill)
        {
            request.diagnosticLog?.Invoke(
                "unidade sem a habilitacao de capturar construcoes");
            return result;
        }

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        result.origin = origin;

        int tactical = request.tacticalBudget > 0
            ? request.tacticalBudget
            : Mathf.Max(0, unit.MaxMovementPoints);
        int operationalTurns = Mathf.Max(1, request.operationalTurns);

        UnitReachProfile profile =
            UnitReachEnvelopeService.BuildProfile(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = request.map,
                TerrainDatabase = request.terrainDatabase,
                Intent = ReachIntent.Capture,
                SubStep = request.subStep,
                Band = ReachBand.Tactical,
                MovementBudget = tactical,
                OperationalTurns = operationalTurns,
                IncludeMovementCosts = true
            });

        result.tacticalBudget = tactical;
        result.operationalBudget =
            AIActionReachCoordinator.ResolveOperationalBudget(
                unit, operationalTurns);

        // QUANTO VALE UM TURNO PARADO CAPTURANDO — em custo de rota.
        //
        // Vale o que a unidade deixa de andar naquele turno, entao e o MP dela,
        // e nao um numero fixo. Um jipe de 6 MP que fica tres turnos em cima de
        // um predio pagou dezoito hexes de deslocamento; um soldado de 3 MP
        // pagou nove. Constante aqui congelaria de novo o que e parametro da
        // unidade — o mesmo erro que as bandas fixas ja cobraram desta base.
        float turnCostWeight =
            Mathf.Max(1, unit.MaxMovementPoints) * CostTerm;

        IReadOnlyList<ConstructionManager> candidates =
            request.constructions ?? ConstructionManager.AllActive;
        if (candidates == null)
        {
            request.diagnosticLog?.Invoke("nenhum registro de construcoes");
            return result;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            ConstructionManager construction = candidates[i];
            if (construction == null)
                continue;

            result.candidatesOffered++;
            if (request.includeConstruction != null
                && !request.includeConstruction(construction))
            {
                result.candidatesFilteredOut++;
                continue;
            }

            result.candidatesVisited++;
            Vector3Int cell = construction.CurrentCellPosition;
            cell.z = 0;

            bool hasCapturer = TryFindCapturerOnCell(
                request, cell, out UnitManager occupant);
            if (hasCapturer && request.skipConstructionsWithCapturer)
            {
                Reject(
                    result,
                    construction,
                    cell,
                    $"capturador em cima ({occupant.UnitDisplayName})");
                continue;
            }

            // Projeta a unidade na celula da construcao e pergunta ao sensor.
            // Mesmo par de modos do SimulateCaptureSensor da IA: parada quando
            // ja esta ali, andando quando chegaria.
            SensorMovementMode movementMode = cell == origin
                ? SensorMovementMode.MoveuParado
                : SensorMovementMode.MoveuAndando;
            if (!PodeCapturarSensor.TryGetCaptureTargetAtCell(
                    unit,
                    request.map,
                    cell,
                    movementMode,
                    out ConstructionManager target,
                    out PodeCapturarSensor.CaptureOperationType operation,
                    out string sensorReason,
                    request.matchController,
                    request.applyFogOfWar))
            {
                Reject(
                    result,
                    construction,
                    cell,
                    string.IsNullOrWhiteSpace(sensorReason)
                        ? "PodeCapturar recusou"
                        : sensorReason);
                continue;
            }

            // A celula pode abrigar outra construcao que nao a candidata.
            if (target != null && target != construction)
            {
                Reject(
                    result,
                    construction,
                    cell,
                    "a celula responde por outra construcao");
                continue;
            }

            int cubic =
                AIActionReachCoordinator.CubicDistance(origin, cell);
            ResolveTier(
                profile,
                cell,
                cubic,
                out MelhorCapturaTier tier,
                out int routeCost,
                out int effectiveCost);

            int power = PodeCapturarSensor.GetCapturePower(
                unit,
                construction,
                operation,
                request.matchController,
                out bool prerequisitePenalty);
            ResolveCaptureEffort(
                construction,
                operation,
                power,
                out int remainingPoints,
                out int turnsToCapture);

            var alvo = new MelhorCapturaAlvoScore
            {
                construction = construction,
                cell = cell,
                tier = tier,
                operation = operation,
                routeCost = routeCost,
                cubicDistance = cubic,
                effectiveCost = effectiveCost,
                capturePower = power,
                prerequisitePenalty = prerequisitePenalty,
                remainingCapturePoints = remainingPoints,
                turnsToCapture = turnsToCapture,
                capturerOnCell = hasCapturer ? occupant : null
            };

            int tierWeight = ResolveTierWeight(tier);
            int turnPenalty = Mathf.Max(0, turnsToCapture);
            alvo.adjustment = request.evaluateAdjustment != null
                ? request.evaluateAdjustment(construction)
                : 0f;
            alvo.score = tierWeight * TierTerm
                         - effectiveCost * CostTerm
                         - turnPenalty * turnCostWeight
                         + alvo.adjustment;
            alvo.displayScore = Mathf.RoundToInt(alvo.score / CostTerm);
            alvo.reason =
                $"banda={tier} (termo={tierWeight * (int)TierTerm}) | " +
                $"custo={effectiveCost} (termo={-effectiveCost * (int)CostTerm}) | " +
                $"turnos={(turnsToCapture >= 0 ? turnsToCapture.ToString() : "nunca")} " +
                $"(termo={-turnPenalty * (int)turnCostWeight}) | " +
                (Mathf.Approximately(alvo.adjustment, 0f)
                    ? string.Empty
                    : $"ajuste do chamador={alvo.adjustment:0} | ") +
                $"poder={power}{(prerequisitePenalty ? " (pré-req -50%)" : string.Empty)} | " +
                $"faltam={remainingPoints} | " +
                $"rota={(routeCost >= 0 ? routeCost.ToString() : "fora do envelope")} | " +
                $"cúbica={cubic} | operacao={operation}" +
                (alvo.capturerOnCell != null
                    ? $" | capturador em cima={alvo.capturerOnCell.UnitDisplayName}"
                    : string.Empty);
            result.ranking.Add(alvo);
        }

        result.ranking.Sort(CompareAlvo);
        AIDecisionPerf.AddCount(
            "MelhorCapturaCandidates", result.candidatesVisited);
        AIDecisionPerf.AddCount(
            "MelhorCapturaTargets", result.ranking.Count);
        request.diagnosticLog?.Invoke(
            $"ofertadas={result.candidatesOffered}; " +
            $"cortadas pelo filtro do chamador={result.candidatesFilteredOut}; " +
            $"avaliadas={result.candidatesVisited}; " +
            $"alvos={result.ranking.Count}; " +
            $"recusas={result.rejected.Count} ({result.BuildRejectedSummary()})");
        return result;
    }

    /// <summary>
    /// Ha um capturador em cima da construcao? Vale para qualquer unidade
    /// habilitada, aliada ou nao. A habilitacao sai do sensor, nunca de leitura
    /// de skill aqui.
    ///
    /// O resultado e informativo por padrao. Quem organiza ja e atraido pelo
    /// capturador que chegou e sabe o que fazer com o fato; remover a
    /// construcao da lista aqui esconderia dele uma coisa que ele ja sabia
    /// tratar. Ver `skipConstructionsWithCapturer`.
    /// </summary>
    private static bool TryFindCapturerOnCell(
        MelhorCapturaRequest request,
        Vector3Int cell,
        out UnitManager occupant)
    {
        occupant = null;
        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(
                request.map, cell, request.unit);
        if (occupants == null)
            return false;

        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager candidate = occupants[i];
            if (candidate == null || candidate.IsDead)
                continue;
            if (!PodeCapturarSensor.HasCaptureConstructionSkill(candidate))
                continue;
            occupant = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Em que banda a celula caiu, e por qual custo.
    ///
    /// Fora das duas bandas NAO se abre pathfinding. A pergunta que sobra e
    /// "para que lado", e distancia cubica responde isso de graca — abrir uma
    /// travessia por candidata distante e o laco que ja custou 43 s nesta base.
    /// </summary>
    private static void ResolveTier(
        UnitReachProfile profile,
        Vector3Int cell,
        int cubicDistance,
        out MelhorCapturaTier tier,
        out int routeCost,
        out int effectiveCost)
    {
        if (profile?.Tactical != null
            && profile.Tactical.CanAct(cell)
            && profile.Tactical.TryGetCost(cell, out int tacticalCost))
        {
            tier = MelhorCapturaTier.Tactical;
            routeCost = tacticalCost;
            effectiveCost = tacticalCost;
            return;
        }

        if (profile?.Operational != null
            && profile.Operational.CanAct(cell)
            && profile.Operational.TryGetCost(cell, out int operationalCost))
        {
            tier = MelhorCapturaTier.Operational;
            routeCost = operationalCost;
            effectiveCost = operationalCost;
            return;
        }

        tier = MelhorCapturaTier.BeyondOperational;
        routeCost = -1;
        effectiveCost = cubicDistance;
    }

    /// <summary>
    /// Quanto falta e em quantos turnos, pelo modelo real da execucao
    /// (TurnStateManager.Capture): RecoverAlly SOBE ate o teto, CaptureEnemy
    /// DESCE ate zero. Sao contas opostas, e por isso "quanto falta" nao existe
    /// sem saber a operacao.
    /// </summary>
    private static void ResolveCaptureEffort(
        ConstructionManager construction,
        PodeCapturarSensor.CaptureOperationType operation,
        int power,
        out int remainingPoints,
        out int turnsToCapture)
    {
        int max = Mathf.Max(0, construction.CapturePointsMax);
        int current = Mathf.Max(0, construction.CurrentCapturePoints);
        remainingPoints =
            operation == PodeCapturarSensor.CaptureOperationType.RecoverAlly
                ? Mathf.Max(0, max - current)
                : current;
        turnsToCapture = power > 0
            ? Mathf.CeilToInt(remainingPoints / (float)power)
            : -1;
    }

    private static int ResolveTierWeight(MelhorCapturaTier tier)
    {
        switch (tier)
        {
            case MelhorCapturaTier.Tactical:
                return 3;
            case MelhorCapturaTier.Operational:
                return 2;
            default:
                return 1;
        }
    }

    // O score MANDA na ordem, e nao os termos separados, porque o ajuste do
    // chamador precisa poder derrubar uma banda — e um desempate lexicografico
    // por banda o ignoraria justamente onde ele importa. Sem ajuste as duas
    // ordens sao a mesma: o termo de banda vale 100.000 e o de custo 100, entao
    // custo nenhum de um tabuleiro de hex cruza a fronteira. Banda, custo e
    // InstanceId ficam como desempate, o ultimo so para a lista nao dancar
    // entre duas chamadas identicas.
    private static int CompareAlvo(
        MelhorCapturaAlvoScore a,
        MelhorCapturaAlvoScore b)
    {
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0)
            return byScore;

        int byTier =
            ResolveTierWeight(b.tier).CompareTo(ResolveTierWeight(a.tier));
        if (byTier != 0)
            return byTier;

        int byCost = a.effectiveCost.CompareTo(b.effectiveCost);
        if (byCost != 0)
            return byCost;

        int idA = a.construction != null ? a.construction.InstanceId : 0;
        int idB = b.construction != null ? b.construction.InstanceId : 0;
        return idA.CompareTo(idB);
    }

    private static void Reject(
        MelhorCapturaResult result,
        ConstructionManager construction,
        Vector3Int cell,
        string reason)
    {
        result.rejected.Add(new MelhorCapturaReject
        {
            construction = construction,
            cell = cell,
            reason = reason
        });
    }
}
