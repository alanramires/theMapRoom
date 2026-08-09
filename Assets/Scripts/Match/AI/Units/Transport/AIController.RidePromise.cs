using UnityEngine;

public partial class AIController
{
    // ------------------------------------------------------------------
    // PROMESSA DE RESGATE
    //
    // "Eu vou buscar voce, mas tem esses caras aqui — aguenta ai!"
    //
    // A promessa mora no transportador, no AIDesignatedMission que ja existia e
    // ja persiste no save. Ela e um FAROL: obriga quem prometeu a continuar
    // considerando P1, mas nao torna P1 propriedade desse transportador.
    //
    // AS REGRAS, e elas nao sao simetricas:
    //
    //   vincula QUEM PROMETE     — a viagem e devida e nao se esquece entre
    //                              turnos, mesmo com a esteira rodando.
    //   NAO vincula quem espera  — qualquer carona que aparecer serve. Recusar
    //                              um embarque que esta ali porque "outro
    //                              prometeu" seria a fome de novo, fantasiada.
    //   SEM exclusividade        — varios transportadores podem convergir para
    //                              o mesmo passageiro. O primeiro embarque
    //                              confirmado apaga a necessidade dos demais.
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
    /// Leitura publica dentro do controller do farol persistido por um casco.
    /// Nao reserva o passageiro e nao impede outro transporte de atende-lo;
    /// serve apenas para distribuir a primeira escolha entre cascos que ainda
    /// possuem alternativas equivalentes.
    /// </summary>
    private bool HasActiveRidePromiseFor(
        UnitManager transporter,
        UnitManager passenger)
    {
        if (transporter == null
            || passenger == null
            || passenger.IsEmbarked
            || transporter == passenger
            || transporter.IsDead
            || transporter.IsEmbarked
            || transporter.IsUnderRepair
            || !PlayerSlotRelations.AreAllies(
                transporter,
                passenger)
            || !transporter.TryGetUnitData(
                out UnitData transporterData)
            || transporterData == null
            || !transporterData.isTransporter
            || !TryGetRidePromise(
                transporter,
                out UnitManager promisedPassenger)
            || promisedPassenger != passenger)
        {
            return false;
        }

        return CanTransporterMeetPassenger(
            transporter,
            passenger);
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
        // EMBARCOU EM MIM NAO E BAIXA — E O COMECO DA ENTREGA.
        //
        // Intent=Transport com alvo #N tem dois donos e a mesma forma:
        //
        //     PROMESSA  "vou buscar #N"        acaba quando #N embarca
        //     HERANCA   "levo #N ate (x,y)"    COMECA quando #N embarca
        //
        // Sem distinguir, o setup da Fase 2 apagava a missao herdada todo
        // turno: o APC agia com intent=None e so a reescrevia depois de agir.
        // E como o navio decide antes dele na iniciativa, o navio lia None —
        // a missao herdada existia, mas nunca no instante em que alguem olhava.
        //
        // Quem embarcou em OUTRO veiculo continua dando baixa: ali a promessa
        // foi cumprida por terceiro, que e o caso para o qual a regra nasceu.
        if (passenger.IsEmbarked
            && passenger.EmbarkedTransporter != transporter)
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

}
