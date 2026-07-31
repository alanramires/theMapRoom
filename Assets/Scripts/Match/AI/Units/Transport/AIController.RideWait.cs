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
    /// Carimba, ou tira da fila, a partir de um pedido de carona JA avaliado.
    ///
    /// Nao avalia nada por conta propria: so e chamado onde o resultado ja
    /// existe na mao, para nao pagar um envelope a mais por unidade.
    /// </summary>
    private void ApplyRideWaitStamp(
        UnitManager unit,
        QueroCaronaResult rideNeed)
    {
        if (unit == null || rideNeed == null)
            return;

        bool wasWaiting = unit.AIIsWaitingForRide;
        int currentTurn = ResolveCurrentTurnNumber();

        // Embarcou (conseguiu a carona) ou parou de querer: sai da fila.
        if (unit.IsEmbarked || !rideNeed.wantsRide)
        {
            if (wasWaiting)
            {
                int waited = unit.ResolveAIRideWaitTurns(currentTurn);
                unit.ClearAIRideWait();
                if (showAILogs)
                {
                    Debug.Log(
                        $"{TL("FilaCarona")} #{unit.InstanceId} sai da fila " +
                        $"apos {waited} turno(s) — " +
                        (unit.IsEmbarked
                            ? "embarcou."
                            : "nao quer mais carona."));
                }
            }
            return;
        }

        // Idempotente de proposito: quem ja estava na fila MANTEM a
        // antiguidade. Reiniciar aqui zeraria a espera a cada reavaliacao — e o
        // pedido e reavaliado dezenas de vezes por turno, uma vez por
        // transportador que planeja coleta.
        unit.MarkAIRideWaitStart(currentTurn);
        ApplyRideWaitUrgency(unit, rideNeed, currentTurn);
        if (!wasWaiting && showAILogs)
        {
            Debug.Log(
                $"{TL("FilaCarona")} #{unit.InstanceId} entra na fila no turno " +
                $"{currentTurn} — {(rideNeed.isStranded ? "SEM ROTA PRÓPRIA" : "fora das bandas")} " +
                $"(score={rideNeed.rideNeedScore}).");
        }
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
