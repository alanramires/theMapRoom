using UnityEngine;

public partial class AIController
{
    // ------------------------------------------------------------------
    // FILA DA CARONA
    //
    // O ranking do transporte ordena por proximidade. Sem antiguidade, quem
    // esta longe — ou sem rota propria nenhuma — perde para sempre de quem
    // esta a tres hexes, e a unidade certa passa a partida inteira esperando.
    //
    // O carimbo e o anti-starvation classico: prioridade que cresce com o
    // tempo, ate que quem espera ha mais tempo passa na frente por si mesmo.
    // Nao e caso especial para ilhado; e ordem por antiguidade, que resolve
    // ilhado e distante com a mesma regra.
    //
    // ONDE A ESCRITA MORA. O QueroCaronaService e PURO por contrato: responde
    // e nao anota nada. Quem anota e a IA, aqui. Por isso as janelas de Editor
    // podem consultar o servico a vontade sem mexer na fila de ninguem.
    //
    // O QUE SE ESCREVE. Um carimbo de turno — memoria de planejamento da IA,
    // como o objetivo de captura designado e a missao individual. Nao e
    // ocupacao, FOW, deteccao nem recurso: nada do que o invariante
    // transacional protege.
    // ------------------------------------------------------------------

    /// <summary>Quanto a urgencia cresce por turno de espera.</summary>
    private const int RideWaitScoreStepPerTurn = 100;

    /// <summary>
    /// Teto da antiguidade, igual a emergencia de reparo (2000).
    ///
    /// O caroneiro no talo EMPATA com o ferido, nunca passa: ferido morre,
    /// ilhado espera. O que sobra da espera nao vai para o ranking — vai virar
    /// pressao de compra de mais transporte, que e a doutrina da esteira: a
    /// parada lotada nao sequestra o onibus, ela justifica comprar outro.
    ///
    /// Tirar o teto faz o panda de 15 turnos furar a fila do ferido.
    /// </summary>
    private const int RideWaitScoreCeiling = 2000;

    private int ResolveCurrentTurnNumber()
    {
        return matchController != null ? matchController.CurrentTurn : 0;
    }

    /// <summary>
    /// Antiguidade vira urgencia. Aplicado no resultado JA devolvido pelo
    /// servico, nunca dentro dele: o QueroCarona tem cache proprio cuja chave
    /// nao conhece turno, entao somar a espera la dentro serviria numero velho
    /// depois. O objeto devolvido e sempre uma copia (o servico clona na
    /// leitura e na escrita do cache), logo mexer nele aqui e seguro.
    /// </summary>
    private static void ApplyRideWaitUrgency(
        UnitManager unit,
        QueroCaronaResult rideNeed,
        int currentTurn)
    {
        int waited = unit.ResolveAIRideWaitTurns(currentTurn);
        rideNeed.rideWaitTurns = waited;

        // Emergencia de reparo ja esta no teto e nao envelhece: ela nao e uma
        // fila, e uma parada cardiaca.
        if (rideNeed.isEmergency || waited <= 0)
            return;

        rideNeed.rideNeedScore = Mathf.Min(
            RideWaitScoreCeiling,
            rideNeed.rideNeedScore + waited * RideWaitScoreStepPerTurn);
    }

    /// <summary>
    /// Decora um pedido de carona JA avaliado com a espera publicada. NAO
    /// escreve estado na unidade.
    ///
    /// Antes escrevia: era este metodo que colocava e tirava a unidade da fila,
    /// e ele roda dentro do planejamento de QUEM PERGUNTA. Como o QueroCarona e
    /// par a par — cada transportador com a sua banda e o seu horizonte —, dois
    /// transportadores davam respostas opostas sobre a mesma unidade no mesmo
    /// turno e o ultimo a perguntar gravava a ficha. Dai a dança no Inspector, e
    /// dai o aninhamento depender da ordem de iniciativa: o degrau gateia em
    /// AIIsWaitingForRide.
    ///
    /// O DEFEITO ERA A BAIXA, NAO A SUBIDA. Quem responde "sim" sabe de um
    /// caminho real; quem responde "nao" so sabe que ELE nao serve. Tirar da
    /// fila a partir de um "nao" alheio e que fazia o campo oscilar.
    ///
    /// Entao aqui ficou monotono dentro do turno: pode LEVANTAR, nunca baixar.
    /// Quem baixa e PublishRideNeed, uma vez por turno, e o embarque.
    ///
    /// Isso preserva os alvos que a publicacao ainda nao sabe formular — a zona
    /// de vigilancia do Radar e o alvo reservado do capturador nao estao na
    /// missao da unidade, e cortar estes pontos derrubaria essas pecas da fila.
    /// Fatia 2: dobrar esses dois alvos na missao, e ai a publicacao vira
    /// escritora unica de verdade.
    /// </summary>
    private void ApplyRideWaitStamp(
        UnitManager unit,
        QueroCaronaResult rideNeed)
    {
        if (unit == null || rideNeed == null)
            return;

        int currentTurn = ResolveCurrentTurnNumber();
        if (rideNeed.wantsRide && !unit.IsEmbarked)
        {
            bool wasWaiting = unit.AIIsWaitingForRide;
            unit.PublishAIRideNeed(true, currentTurn);
            if (!wasWaiting && showAILogs)
            {
                Debug.Log(
                    $"{TL("FilaCarona")} #{unit.InstanceId} entra na fila no " +
                    $"turno {currentTurn} — " +
                    $"{(rideNeed.isStranded ? "SEM ROTA PRÓPRIA" : "fora das bandas")} " +
                    $"(score={rideNeed.rideNeedScore}).");
            }
        }

        ApplyRideWaitUrgency(unit, rideNeed, currentTurn);
    }

    /// <summary>
    /// O ESCRITOR UNICO do par (quero carona, ha quanto tempo). Uma vez por
    /// unidade por turno, no setup da Fase 2.
    ///
    /// A pergunta e sempre a mesma, e e o que prova que o nivel esta certo:
    /// *alcanco a minha propria missao sozinho?* Sem ramo por papel —
    ///
    ///     soldado         intent capture   (0,0)     nao alcanco  ->  quer
    ///     APC vazio       intent transport (20,5)    alcanco      ->  nao quer
    ///     APC carregado   intent transport (0,0)     nao alcanco  ->  quer
    ///                               ^ herdada da carga
    ///
    /// EvaluatePickupRideNeed ja faz esse despacho (destino da carga para quem
    /// carrega, pergunta de captura para quem captura, emergencia para o
    /// resto). Reusar e deliberado: esta fatia muda QUEM escreve e QUANDO, nao
    /// o conteudo da resposta.
    /// </summary>
    private void PublishRideNeed(
        UnitManager unit,
        TeamObjectivePlan plan,
        AIWorldSnapshot snapshot)
    {
        if (unit == null || unit.IsDead)
            return;

        int currentTurn = ResolveCurrentTurnNumber();
        bool wasWaiting = unit.AIIsWaitingForRide;

        // Embarcado ja conseguiu o que pedia. Nao gasta envelope para descobrir
        // isso, e a saida da fila e registrada por UpdateRideWaitState.
        if (unit.IsEmbarked)
            return;

        QueroCaronaResult rideNeed =
            EvaluatePickupRideNeed(unit, plan, 2, snapshot);
        bool wantsRide = rideNeed != null && rideNeed.wantsRide;
        unit.PublishAIRideNeed(wantsRide, currentTurn);

        if (!showAILogs || wantsRide == wasWaiting)
            return;

        if (wantsRide)
        {
            Debug.Log(
                $"{TL("FilaCarona")} #{unit.InstanceId} entra na fila no turno " +
                $"{currentTurn} — {(rideNeed.isStranded ? "SEM ROTA PRÓPRIA" : "fora das bandas")} " +
                $"(score={rideNeed.rideNeedScore}).");
            return;
        }

        Debug.Log(
            $"{TL("FilaCarona")} #{unit.InstanceId} sai da fila — " +
            "nao quer mais carona.");
    }

    /// <summary>
    /// Manutencao barata, uma vez por unidade por turno na Fase 2. NAO consulta
    /// o QueroCarona: so recolhe da fila quem obviamente ja saiu dela.
    ///
    /// Quem continua querendo carona nao e limpo aqui — e justamente por nao
    /// limpar que a espera segue crescendo nos turnos em que nenhum
    /// transportador chegou a avaliar a unidade. Que e, afinal, o turno em que
    /// esperar dói.
    /// </summary>
    private void UpdateRideWaitState(UnitManager unit)
    {
        if (unit == null || !unit.AIIsWaitingForRide)
            return;
        if (!unit.IsEmbarked)
            return;

        int waited = unit.ResolveAIRideWaitTurns(ResolveCurrentTurnNumber());
        unit.ClearAIRideWait();
        if (showAILogs)
        {
            Debug.Log(
                $"{TL("FilaCarona")} #{unit.InstanceId} embarcado — " +
                $"sai da fila apos {waited} turno(s).");
        }
    }
}
