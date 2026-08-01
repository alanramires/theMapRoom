using UnityEngine;

public partial class AIController
{
    // ------------------------------------------------------------------
    // PROMESSA DE RESGATE
    //
    // "Eu vou buscar voce, mas tem esses caras aqui — aguenta ai!"
    //
    // Ate aqui, reservado e prometido eram coisas DIFERENTES: o plano marcava o
    // passageiro como servico de um transportador (bloqueando os outros) e
    // nenhum codigo obrigava esse transportador a ir. Plaquinha de reservado
    // sem ninguem no volante — o passageiro ficava isolado dos dois lados.
    //
    // A promessa mora no transportador, no AIDesignatedMission que ja existia e
    // ja persiste no save. Com ela, reservado e prometido viram o MESMO objeto:
    // so esta reservado quem tem alguem que prometeu.
    //
    // AS REGRAS, e elas nao sao simetricas:
    //
    //   vincula QUEM PROMETE     — a viagem e devida e nao se esquece entre
    //                              turnos, mesmo com a esteira rodando.
    //   NAO vincula quem espera  — qualquer carona que aparecer serve. Recusar
    //                              um embarque que esta ali porque "outro
    //                              prometeu" seria a fome de novo, fantasiada.
    //   exclusividade de VIAGEM  — impede outro transportador de comprometer
    //                              viagem para o mesmo cara; nunca impede um
    //                              embarque imediato.
    //   promessa NAO e preempcao — ela pesa na escolha, nao sequestra o
    //                              veiculo. Avanco tatico do grupo continua
    //                              mandando.
    //
    // NAO EXPIRA por enquanto. Espera vira demanda, nao rodizio: se alguem
    // ficar plantado, o ajuste e prazo — e nao trocar de dono em circulo.
    // ------------------------------------------------------------------

    /// <summary>
    /// Promessa pendente deste transportador. Devolve false quando nao ha, ou
    /// quando o passageiro prometido ja nao existe.
    /// </summary>
    private bool TryGetRidePromise(
        UnitManager transporter,
        out UnitManager passenger)
    {
        passenger = null;
        if (transporter == null
            || !transporter.AIHasDesignatedMission
            || transporter.AIDesignatedMissionIntent
                != AIPlanRuntimeIntent.Transport)
        {
            return false;
        }

        passenger = FindActiveUnit(
            transporter.AIDesignatedMissionTargetUnitInstanceId,
            transporter.TeamId);
        return passenger != null;
    }

    /// <summary>
    /// Registra a viagem devida. So e chamada quando a coleta NAO se resolve
    /// nesta rodada — pickup que termina hoje nao tem o que prometer, e
    /// transformar toda avaliacao em promessa encheria o Mission Intent de
    /// ruido que nenhuma regra de baixa daria conta.
    /// </summary>
    private void CommitRidePromise(
        UnitManager transporter,
        UnitManager passenger,
        Vector3Int meetingCell)
    {
        if (transporter == null || passenger == null)
            return;

        // Nao piso em missao de outro dono (reabastecimento, por exemplo).
        // Sobrescrever a agenda alheia para prometer carona seria trocar um
        // problema por outro.
        if (transporter.AIHasDesignatedMission
            && transporter.AIDesignatedMissionIntent
                != AIPlanRuntimeIntent.Transport)
        {
            return;
        }

        bool isNewPromise =
            !TryGetRidePromise(transporter, out UnitManager previous)
            || previous != passenger;

        meetingCell.z = 0;
        transporter.SetAIDesignatedMission(
            AIPlanRuntimeIntent.Transport,
            meetingCell,
            targetUnitInstanceId: passenger.InstanceId);

        if (isNewPromise && showAILogs)
        {
            Debug.Log(
                $"{TL("Promessa")} #{transporter.InstanceId} promete resgate " +
                $"de pax=#{passenger.InstanceId} em {meetingCell}" +
                (previous != null
                    ? $" (substitui promessa a #{previous.InstanceId})"
                    : string.Empty));
        }
    }

    /// <summary>
    /// Baixa da promessa, uma vez por turno na Fase 2. Cumprida por terceiro e
    /// cumprida do mesmo jeito: o que importa e o passageiro ter saido do chao,
    /// nao quem o tirou.
    /// </summary>
    private void UpdateRidePromiseState(UnitManager transporter)
    {
        if (!TryGetRidePromise(transporter, out UnitManager passenger))
        {
            // Promessa apontando para unidade que nao existe mais (morreu, ou
            // instancia sumiu) tambem tem de sair do caminho.
            if (transporter != null
                && transporter.AIHasDesignatedMission
                && transporter.AIDesignatedMissionIntent
                    == AIPlanRuntimeIntent.Transport)
            {
                transporter.ClearAIDesignatedMission();
                if (showAILogs)
                {
                    Debug.Log(
                        $"{TL("Promessa")} #{transporter.InstanceId} baixa a " +
                        "promessa: passageiro nao existe mais.");
                }
            }
            return;
        }

        string dischargeReason = null;
        if (passenger.IsEmbarked)
            dischargeReason = "passageiro embarcou";
        else if (passenger.IsDead)
            dischargeReason = "passageiro morreu";
        else if (!CanTransporterMeetPassenger(transporter, passenger))
            dischargeReason = "componentes de movimento deixaram de se tocar";

        if (dischargeReason == null)
            return;

        transporter.ClearAIDesignatedMission();
        if (showAILogs)
        {
            Debug.Log(
                $"{TL("Promessa")} #{transporter.InstanceId} baixa a promessa " +
                $"a pax=#{passenger.InstanceId}: {dischargeReason}.");
        }
    }

    /// <summary>
    /// Este passageiro ja tem viagem comprometida por OUTRO transportador?
    ///
    /// Bloqueia planejamento duplicado — dois veiculos cruzando o mapa pelo
    /// mesmo cara — e nada alem disso. O chamador so consulta quando esta
    /// prestes a COMPROMETER viagem; coleta que se resolve na rodada ignora
    /// esta regra de proposito.
    /// </summary>
    private bool IsPassengerPromisedToAnotherTransporter(
        UnitManager transporter,
        UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;

        foreach (UnitManager candidate in UnitManager.AllActive)
        {
            if (candidate == null
                || candidate == transporter
                || candidate.IsDead
                || candidate.TeamId != transporter.TeamId)
            {
                continue;
            }

            if (TryGetRidePromise(candidate, out UnitManager promised)
                && promised == passenger)
            {
                return true;
            }
        }

        return false;
    }
}
