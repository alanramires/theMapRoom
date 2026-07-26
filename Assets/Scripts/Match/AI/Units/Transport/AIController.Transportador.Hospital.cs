using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // -------------------------------------------------------------------------
    // Modo hospital — supridor QUE TAMBEM TRANSPORTA carregando um ferido.
    //
    // Um caminhao de suprimentos rebocando artilharia leva a peca ate um ponto e
    // larga: certo, inclusive em EVAC. Uma fragata carregando um apache ferido nao
    // pode fazer o mesmo — ela e o proprio ponto de reparo do passageiro (o
    // PodeSuprir com serviceRange SameHexOrEmbarked so atende os PROPRIOS
    // embarcados). Largar o ferido ali seria jogar fora a manutencao em curso.
    //
    // A flag aiDisembarkWhenCannotSupply diz literalmente o que fazer: desembarca
    // SE nao conseguir suprir. A ordem, entao, e:
    //
    //   1. supre a bordo enquanto houver servico e estoque;
    //   2. sem estoque, volta para transferir MANTENDO o ferido a bordo;
    //   3. paciente ja atendido nesta rodada: tiro parado / recolhe pra retaguarda;
    //   4. sem servico E sem recarga alcancavel: devolve null e o EVAC normal
    //      (courier terrestre/aereo ou o ramo naval) desembarca o ferido.
    //
    // O passageiro sai sozinho do modo: Phase2 roda UpdateRepairState em toda
    // unidade viva do slot, embarcada inclusive, entao ao bater
    // repairRecoverHpAbove ele deixa de ser paciente e o courier normal assume.
    //
    // Postura: com paciente a bordo o navio nao persegue ninguem. Aceita apenas
    // tiro a partir da celula onde ja esta — por isso o ataque abaixo recebe um
    // "paths" de uma celula so.
    // -------------------------------------------------------------------------

    private static bool IsSupplyCapableTransporter(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData data)
            && data != null
            && data.isSupplier
            && data.isTransporter
            && data.aiDisembarkWhenCannotSupply;
    }

    // Paciente = passageiro em reparo. Com mais de um, o mais critico manda.
    private static UnitManager FindEmbarkedPatient(UnitManager transporter)
    {
        List<UnitManager> passengers = CollectPassengers(transporter);
        UnitManager worst = null;
        for (int i = 0; i < passengers.Count; i++)
        {
            UnitManager passenger = passengers[i];
            if (passenger == null || passenger.IsDead || !passenger.IsUnderRepair)
                continue;
            if (worst == null || passenger.CurrentHP < worst.CurrentHP)
                worst = passenger;
        }
        return worst;
    }

    private PlayerAction TryDecideSupplierHospitalAction(
        UnitManager unit, AIWorldSnapshot snapshot, TeamObjectivePlan plan)
    {
        if (unit == null || snapshot == null || !IsSupplyCapableTransporter(unit))
            return null;

        // O proprio transportador em reparo tem prioridade sobre a enfermaria: um navio
        // que esta se perdendo leva o paciente junto no recuo. Quem decide isso e o
        // TryDecideRepairAction do fluxo normal.
        if (unit.IsUnderRepair)
            return null;

        UnitManager patient = FindEmbarkedPatient(unit);
        if (patient == null)
            return null;

        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> paths = BuildLogisticsPaths(unit);
        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        int limit = GetLogisticsServiceLimit(unit);

        string patientTag = $"#{patient.InstanceId} hp={patient.CurrentHP}/{patient.GetMaxHP()}";

        // 1. Suprir a bordo. O sensor e quem responde se ha servico; nao replicamos regra.
        if (TryBuildHospitalSupplyAction(
                unit, snapshot, patient, fromCell, paths, limit,
                out PlayerAction supplyAction, out string supplyReason, out bool waitOneTurn))
        {
            Debug.Log($"{TL("Hospital")} {unit.InstanceId} supre paciente {patientTag} a bordo — {supplyReason}");
            return supplyAction;
        }

        Debug.Log($"{TL("Hospital")} {unit.InstanceId} nao supre {patientTag} agora — {supplyReason}");

        // 2. Sem estoque: volta para transferir, com o ferido a bordo. Mesmo fluxo do
        //    restock logistico normal — a unica diferenca e que a carga nao desce.
        bool needsReload = ShouldRestockLogisticsUnit(unit, out string restockReason);
        if (needsReload)
        {
            if (TryBuildLogisticsTransferReceiveAction(
                    unit, snapshot, fromCell, paths, out PlayerAction transferAction, out string transferReason))
            {
                Debug.Log($"{TL("Hospital")} {unit.InstanceId} recarrega por transferencia com {patientTag} a bordo — {restockReason} {transferReason}");
                return transferAction;
            }

            if (TryFindLogisticsReloadCell(
                    unit, snapshot, fromCell, paths, occupied,
                    out Vector3Int reloadCell, out string reloadReason))
            {
                if (TryBuildLogisticsTransferReceiveActionAtCell(
                        unit, snapshot, fromCell, reloadCell, paths,
                        out PlayerAction moveTransferAction, out string moveTransferReason))
                {
                    Debug.Log($"{TL("Hospital")} {unit.InstanceId} move + recarrega com {patientTag} a bordo — {restockReason} {moveTransferReason}");
                    return moveTransferAction;
                }

                Debug.Log($"{TL("Hospital")} {unit.InstanceId} volta para recarga via {reloadCell} com {patientTag} a bordo — {restockReason} {reloadReason}");
                return BuildMoveBatch(unit, snapshot.AITeam, fromCell, reloadCell, paths);
            }

            Debug.Log($"{TL("Hospital")} {unit.InstanceId} sem recarga alcancavel — {restockReason}; libera EVAC normal para {patientTag}");
            return null;
        }

        // 3. Impedimento ESTRUTURAL (o sensor nao aceita este paciente de jeito nenhum:
        //    servico nao coberto, alcance que nao alcanca embarcado etc.). Segurar aqui
        //    seria congelar o ferido dentro de um transporte que nunca vai trata-lo —
        //    e exatamente o caso que a flag nomeia. Devolve o turno ao EVAC.
        if (!waitOneTurn)
        {
            Debug.Log($"{TL("Hospital")} {unit.InstanceId} nao consegue suprir {patientTag} " +
                      $"(impedimento estrutural) — libera desembarque pelo EVAC normal");
            return null;
        }

        // 4. Impedimento passageiro (o ferido ja recebeu suprimento nesta rodada): o
        //    tratamento continua no proximo turno. Segura: tiro parado, senao recolhe
        //    pra retaguarda. Nunca avanca com paciente a bordo.
        if (TryBuildStationaryHospitalAttack(
                unit, snapshot, fromCell, occupied, out PlayerAction attackAction, out string attackReason))
        {
            Debug.Log($"{TL("Hospital")} {unit.InstanceId} tiro parado com {patientTag} a bordo — {attackReason}");
            return attackAction;
        }

        // serviceTarget = null de proposito: o paciente esta A BORDO, entao a celula dele
        // e a nossa. Com alvo de servico o reposicionador PROGRIDE em direcao a ele e
        // desliga o filtro de retaguarda segura — o oposto do que se quer carregando
        // ferido. Sem alvo, ele recolhe rumo a ancora e so aceita celula segura.
        Vector3Int anchor = ResolveLogisticsAnchor(snapshot, fromCell);
        if (paths != null && paths.Count > 0
            && TryFindLogisticsRepositionCell(
                unit, snapshot, fromCell, anchor, serviceTarget: null, baseDefense: false,
                paths, occupied, out Vector3Int moveCell, out string repositionReason)
            && moveCell != fromCell)
        {
            Debug.Log($"{TL("Hospital")} {unit.InstanceId} recolhe com {patientTag} a bordo via {moveCell} — {repositionReason}");
            return BuildMoveBatch(unit, snapshot.AITeam, fromCell, moveCell, paths);
        }

        Debug.Log($"{TL("Hospital")} {unit.InstanceId} mantem {fromCell} protegendo {patientTag}");
        return BuildMoveBatch(unit, snapshot.AITeam, fromCell, fromCell, paths);
    }

    // O paciente PRECISA estar na lista servida — o objetivo do modo e ele, nao a
    // ocupacao das vagas de servico. As vagas que sobrarem sao preenchidas com os
    // demais alvos validos do sensor, para nao desperdicar o turno do supridor.
    private bool TryBuildHospitalSupplyAction(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        UnitManager patient,
        Vector3Int fromCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        int limit,
        out PlayerAction action,
        out string reason,
        out bool waitOneTurn)
    {
        action = null;
        reason = "";
        // Impedimento que se resolve sozinho no proximo turno — nao justifica largar o
        // ferido. Tudo que nao for isto e tratado como impedimento estrutural.
        waitOneTurn = false;
        if (limit <= 0)
        {
            reason = "maxUnitsServedPerTurn=0";
            return false;
        }
        if (patient.ReceivedSuppliesThisTurn)
        {
            reason = "paciente ja recebeu suprimento nesta rodada";
            waitOneTurn = true;
            return false;
        }

        var options = new List<PodeSuprirOption>();
        var invalidOptions = new List<PodeSuprirInvalidOption>();
        bool hasAny = PodeSuprirSensor.CollectOptions(
            unit, boardTilemap, terrainDatabase, matchController,
            options, out string sensorReason, invalidOptions);

        if (hasAny && TryBuildHospitalSupplyBatchFromOptions(
                unit, snapshot, patient, fromCell, fromCell, paths, limit, options, out action))
        {
            reason = $"servico parado em {fromCell}";
            return true;
        }

        // Para SameHexOrEmbarked o alcance nao depende da celula: andar nao muda a
        // resposta do sensor, entao nao ha varredura a fazer. So um supridor cujo
        // alcance depende de posicao (hibrido) ganha algo procurando outra origem.
        if (!unit.TryGetUnitData(out UnitData data) || data == null
            || data.serviceRange == SupplierRangeMode.SameHexOrEmbarked
            || paths == null || paths.Count == 0)
        {
            reason = BuildHospitalSensorReason(patient, options, invalidOptions, sensorReason);
            return false;
        }

        HashSet<Vector3Int> occupied = BuildOccupied(unit);
        foreach (Vector3Int rawCell in paths.Keys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (cell == fromCell)
                continue;
            if (occupied != null && occupied.Contains(cell))
                continue;
            if (!IsLogisticsServiceCellAllowed(unit, snapshot, cell))
                continue;

            var cellOptions = new List<PodeSuprirOption>();
            if (!CollectHospitalSupplyOptionsAtCell(unit, cell, cellOptions))
                continue;
            if (!TryBuildHospitalSupplyBatchFromOptions(
                    unit, snapshot, patient, fromCell, cell, paths, limit, cellOptions, out action))
                continue;

            reason = $"servico apos mover para {cell}";
            return true;
        }

        reason = BuildHospitalSensorReason(patient, options, invalidOptions, sensorReason);
        return false;
    }

    private bool TryBuildHospitalSupplyBatchFromOptions(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        UnitManager patient,
        Vector3Int fromCell,
        Vector3Int serviceCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        int limit,
        List<PodeSuprirOption> options,
        out PlayerAction action)
    {
        action = null;
        if (options == null || options.Count <= 0)
            return false;

        bool patientOffered = false;
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] != null && options[i].targetUnit == patient)
            {
                patientOffered = true;
                break;
            }
        }
        if (!patientOffered)
            return false;

        var targets = new List<UnitManager> { patient };
        var seen = new HashSet<int> { patient.InstanceId };
        for (int i = 0; i < options.Count && targets.Count < limit; i++)
        {
            UnitManager other = options[i] != null ? options[i].targetUnit : null;
            if (other == null || !seen.Add(other.InstanceId))
                continue;
            if (!IsLogisticsServiceTarget(unit, other, allowPreventiveMaintenance: true))
                continue;
            targets.Add(other);
        }

        action = BuildSupplyBatch(unit, snapshot.AITeam, fromCell, serviceCell, targets, paths);
        return true;
    }

    // Simula o sensor a partir de outra celula pelo mesmo padrao ja usado no
    // transporte (SimulateDisembarkFromCell): desloca, consulta, restaura. E
    // sincrono e termina antes de qualquer outra decisao rodar.
    private bool CollectHospitalSupplyOptionsAtCell(
        UnitManager unit, Vector3Int serviceCell, List<PodeSuprirOption> options)
    {
        Vector3Int originalCell = unit.CurrentCellPosition;
        originalCell.z = 0;
        serviceCell.z = 0;

        unit.SetCurrentCellPosition(serviceCell, enforceFinalOccupancyRule: false);
        try
        {
            return PodeSuprirSensor.CollectOptions(
                unit, boardTilemap, terrainDatabase, matchController, options, out _, null);
        }
        finally
        {
            unit.SetCurrentCellPosition(originalCell, enforceFinalOccupancyRule: false);
        }
    }

    private static string BuildHospitalSensorReason(
        UnitManager patient,
        List<PodeSuprirOption> options,
        List<PodeSuprirInvalidOption> invalidOptions,
        string sensorReason)
    {
        if (invalidOptions != null)
        {
            for (int i = 0; i < invalidOptions.Count; i++)
            {
                PodeSuprirInvalidOption invalid = invalidOptions[i];
                if (invalid != null && invalid.targetUnit == patient)
                    return $"PodeSuprir recusa paciente: {invalid.reason}";
            }
        }

        int validCount = options != null ? options.Count : 0;
        return string.IsNullOrWhiteSpace(sensorReason)
            ? $"PodeSuprir sem opcao para o paciente (valid={validCount})"
            : $"PodeSuprir: {sensorReason}";
    }

    // Tiro sem sair do lugar: o mesmo gate de ataque dos outros papeis, mas com um
    // unico destino possivel — a celula atual. Nao vira perseguicao nem expoe o
    // ferido a um avanco.
    private bool TryBuildStationaryHospitalAttack(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        Vector3Int fromCell,
        HashSet<Vector3Int> occupied,
        out PlayerAction action,
        out string reason)
    {
        action = null;
        reason = "";

        var stationaryPaths = new Dictionary<Vector3Int, List<Vector3Int>>
        {
            { fromCell, new List<Vector3Int> { fromCell } }
        };

        return TryBuildRolePreemptiveAttack(
            unit, snapshot, stationaryPaths, occupied,
            defensiveContext: true, out action, out reason);
    }
}
