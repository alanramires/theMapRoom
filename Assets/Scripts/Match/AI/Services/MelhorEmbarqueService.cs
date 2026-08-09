using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum MelhorEmbarqueTier
{
    Tactical,
    Operational,
    Strategic
}

public enum MelhorEmbarquePassengerRouteState
{
    ReachableNow,
    ReachableLater,
    ReachableStrategic,
    NoCurrentRoute
}

public enum MelhorEmbarqueRideDisposition
{
    NotEvaluated,
    Emergency,
    Requested,
    OpportunisticFallback
}

/// <summary>
/// Uma combinação passageiro-LZ. É um fato classificado da consulta, não uma
/// ordem para aproximar, esperar ou embarcar.
/// </summary>
public sealed class MelhorEmbarqueOption
{
    public UnitManager transporter;
    public UnitManager passenger;
    public int slotIndex;
    public Vector3Int passengerCell;
    public Vector3Int passengerMeetingCell;
    public bool hasPassengerMeetingCell;
    public Vector3Int lzCell;
    public MelhorEmbarqueTier transporterTier;
    public int transporterDistance;
    public int transporterRouteCost;
    public MelhorEmbarquePassengerRouteState passengerRouteState;
    public int passengerRouteCost = -1;
    public int passengerEmbarkCost = -1;
    public int passengerTotalCost = -1;
    public MelhorEmbarqueRideDisposition rideDisposition =
        MelhorEmbarqueRideDisposition.NotEvaluated;
    public QueroCaronaResult rideNeed;
    public float rideNeedAdjustment;
    // Peso da vaga: unidades que a opcao TRAZ menos capacidade que ela
    // DESLOCA. Ver ResolveOptionSlotWeight.
    public int unitsBrought = 1;
    public int seatsDisplaced;
    public int slotWeight;
    public float score;
    public string reason;
}

public sealed class MelhorEmbarquePassengerScore
{
    public UnitManager passenger;
    public int slotIndex;
    public Vector3Int passengerCell;
    public Vector3Int passengerMeetingCell;
    public int passengerMoveCost;
    public string reason;
}

public sealed class MelhorEmbarqueLzScore
{
    public UnitManager transporter;
    public Vector3Int cell;
    public MelhorEmbarqueTier tier;
    public int transporterDistance;
    public int transporterRouteCost;
    public float score;
    public string reason;
    public readonly List<MelhorEmbarquePassengerScore> passengers =
        new List<MelhorEmbarquePassengerScore>();
}

public sealed class MelhorEmbarqueReject
{
    public UnitManager passenger;
    public string reason;
}

public sealed class MelhorEmbarqueRequest
{
    public UnitManager transporter;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int tacticalBudget;
    public int operationalTurns = 2;
    public bool includeStrategic;
    public bool resolveLongRangePassengerMeeting;
    // Consumidores runtime podem encerrar a coleta depois de provar uma
    // solução Requested + ReachableNow no Tactical, desde que nenhuma carga
    // esteja em emergência. Ferramentas/editor mantêm o ranking integral.
    public bool stopAfterDecisiveTactical;
    public Dictionary<Vector3Int, List<Vector3Int>> transporterPaths;
    public Func<UnitManager, bool> allowPassenger;
    public Func<UnitManager, bool> includeInLegacyRanking;
    public Func<UnitManager, QueroCaronaResult> evaluateRideNeed;
    public Func<
        UnitManager,
        IReadOnlyDictionary<Vector3Int, int>,
        int,
        QueroCaronaResult> evaluateRideNeedWithOperationalReach;
    public Action<string> diagnosticLog;
}

/// <summary>
/// Consulta passageiro-centrica. O transportador e um filtro opcional:
/// informado, responde somente para ele; ausente, compara todos os
/// transportadores aliados compativeis.
/// </summary>
public sealed class MelhorEmbarquePassengerRequest
{
    public UnitManager passenger;
    public UnitManager transporter;
    public Tilemap map;
    public TerrainDatabase terrainDatabase;
    public int operationalTurns = 2;
    public bool includeStrategic;
    public Func<UnitManager, QueroCaronaResult> evaluateRideNeed;
    public Action<string> diagnosticLog;
}

public sealed class MelhorEmbarqueResult
{
    public readonly List<MelhorEmbarqueOption> options =
        new List<MelhorEmbarqueOption>();
    public readonly List<MelhorEmbarqueLzScore> ranking =
        new List<MelhorEmbarqueLzScore>();
    public readonly List<MelhorEmbarqueReject> rejectedPassengers =
        new List<MelhorEmbarqueReject>();

    public MelhorEmbarqueLzScore best =>
        ranking.Count > 0 ? ranking[0] : null;
    public MelhorEmbarqueOption bestOption =>
        options.Count > 0 ? options[0] : null;
}

/// <summary>
/// Consulta pura para coleta: transportador -> LZ permitido <- passageiro.
/// A origem das ondas e sempre o transportador. O UnitData decide quais
/// terrenos, pares estrutura+terreno e construcoes podem ser LZ.
/// </summary>
public static class MelhorEmbarqueService
{
    private readonly struct PassengerMeeting
    {
        public readonly Vector3Int passengerCell;
        public readonly int moveCost;

        public PassengerMeeting(
            Vector3Int passengerCell,
            int moveCost)
        {
            passengerCell.z = 0;
            this.passengerCell = passengerCell;
            this.moveCost = moveCost;
        }
    }

    private sealed class PassengerReachProfile
    {
        public Dictionary<Vector3Int, PassengerMeeting> now;
        public Dictionary<Vector3Int, PassengerMeeting> later;
        public Dictionary<Vector3Int, PassengerMeeting> longRange;
        public Dictionary<Vector3Int, int> laterStops;
        public BoardTopologyIndex topology;
        public int nowBudget;
        public int laterStopsBudget;
    }

    public static MelhorEmbarqueResult EvaluateForPassenger(
        MelhorEmbarquePassengerRequest request)
    {
        var merged = new MelhorEmbarqueResult();
        if (request?.passenger == null
            || request.map == null
            || request.terrainDatabase == null)
        {
            return merged;
        }

        IEnumerable<UnitManager> candidates =
            request.transporter != null
                ? new[] { request.transporter }
                : UnitManager.AllActive;
        foreach (UnitManager transporter in candidates)
        {
            if (transporter == null
                || transporter == request.passenger
                || transporter.IsDead
                || transporter.IsEmbarked
                || transporter.IsUnderRepair
                || !PlayerSlotRelations.AreAllies(
                    request.passenger, transporter)
                || !transporter.TryGetUnitData(
                    out UnitData transporterData)
                || transporterData == null
                || !transporterData.isTransporter)
            {
                continue;
            }

            if (!TryResolveCompatiblePassengerSlot(
                    transporter,
                    request.passenger,
                    out _,
                    out string incompatibility))
            {
                merged.rejectedPassengers.Add(
                    new MelhorEmbarqueReject
                    {
                        passenger = request.passenger,
                        reason =
                            $"{transporter.name}: {incompatibility}"
                    });
                continue;
            }

            MelhorEmbarqueResult evaluated = Evaluate(
                new MelhorEmbarqueRequest
                {
                    transporter = transporter,
                    map = request.map,
                    terrainDatabase = request.terrainDatabase,
                    tacticalBudget = Mathf.Max(
                        0, transporter.RemainingMovementPoints),
                    operationalTurns = Mathf.Max(
                        1, request.operationalTurns),
                    includeStrategic = request.includeStrategic,
                    resolveLongRangePassengerMeeting = true,
                    allowPassenger = candidate =>
                        candidate == request.passenger,
                    evaluateRideNeed = request.evaluateRideNeed,
                    diagnosticLog = request.diagnosticLog
                });
            merged.options.AddRange(evaluated.options);
            merged.ranking.AddRange(evaluated.ranking);
        }

        merged.ranking.Sort(Compare);
        merged.options.Sort(CompareOptions);
        return merged;
    }

    public static MelhorEmbarqueResult Evaluate(
        MelhorEmbarqueRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.transporter,
            "melhorEmbarque");
        AIDecisionPerf.AddCount("MelhorEmbarqueCalls");
        var result = new MelhorEmbarqueResult();
        if (request?.transporter == null
            || request.map == null
            || request.terrainDatabase == null
            || !request.transporter.TryGetUnitData(
                out UnitData transporterData)
            || transporterData == null
            || !transporterData.isTransporter
            || transporterData.transportSlots == null
            || transporterData.transportSlots.Count == 0)
            return result;

        // Compatibilidade e vaga sao filtros O(slots). Resolva-os ANTES de
        // calcular caminhos do transportador ou percorrer a topologia. Isso
        // evita que um transporte exclusivo de infantaria, por exemplo,
        // visite o mapa inteiro para somente depois rejeitar uma artilharia.
        var passengers = new List<UnitManager>();
        var passengerSlots = new Dictionary<UnitManager, int>();
        var passengerRideNeed =
            new Dictionary<UnitManager, QueroCaronaResult>();
        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (!TryResolvePassengerSlot(
                    request, transporterData, unit,
                    out int slotIndex, out string reason))
            {
                if (unit != null
                    && unit != request.transporter
                    && (request.allowPassenger == null
                        || request.allowPassenger(unit))
                    && PlayerSlotRelations.AreAllies(
                        request.transporter, unit))
                {
                    result.rejectedPassengers.Add(
                        new MelhorEmbarqueReject
                        {
                            passenger = unit,
                            reason = reason
                        });
                }
                continue;
            }

            passengers.Add(unit);
            passengerSlots[unit] = slotIndex;
        }

        if (passengers.Count == 0)
        {
            AIDecisionPerf.AddCount(
                "MelhorEmbarqueCompatibilityEarlyOuts");
            return result;
        }

        Vector3Int origin = request.transporter.CurrentCellPosition;
        origin.z = 0;
        int tactical = Mathf.Max(0, request.tacticalBudget);
        int operational = tactical * Mathf.Max(1, request.operationalTurns);
        Dictionary<Vector3Int, List<Vector3Int>> tacticalPaths;
        using (new AIDecisionPerfScope(
                   request.transporter,
                   "melhorEmbarque.transporterPaths"))
        {
            tacticalPaths =
                request.transporterPaths
                ?? UnitMovementPathRules.CalcularCaminhosValidos(
                    request.map, request.transporter, tactical,
                    request.terrainDatabase);
        }
        if (request.transporterPaths != null)
            AIDecisionPerf.AddCount(
                "TransportPlanningReachReuses");

        IReadOnlyList<Vector3Int> candidateCells;
        bool usedTopologyIndex;
        using (new AIDecisionPerfScope(
                   request.transporter,
                   "melhorEmbarque.candidateCells"))
        {
            candidateCells = ResolveCandidateCells(
                request,
                out usedTopologyIndex);
        }
        AIDecisionPerf.AddCount(
            "MelhorEmbarqueCandidateCells",
            candidateCells != null ? candidateCells.Count : 0);
        BoardTopologyIndex topology =
            usedTopologyIndex
                ? BoardTopologyIndex.GetOrCreateRuntime(
                    request.map,
                    request.terrainDatabase)
                : null;
        AIDecisionPerf.AddCount("TopologyIndexQueries");
        AIDecisionPerf.AddCount(
            usedTopologyIndex
                ? "TopologyIndexHits"
                : "TopologyIndexMisses");
        if (!usedTopologyIndex)
            AIDecisionPerf.AddCount("TopologyFullScans");

        var passengerReach =
            new Dictionary<UnitManager, PassengerReachProfile>(
                passengers.Count);
        var acceptedPassengers =
            new List<UnitManager>(passengers.Count);
        using (new AIDecisionPerfScope(
                   request.transporter,
                   "melhorEmbarque.passengerReach"))
        {
            for (int i = 0; i < passengers.Count; i++)
            {
                UnitManager passenger = passengers[i];
                PassengerReachProfile reachProfile =
                    BuildPassengerReachProfile(
                        request, passenger, topology);
                QueroCaronaResult rideNeed =
                    request.evaluateRideNeedWithOperationalReach != null
                        ? request.evaluateRideNeedWithOperationalReach(
                            passenger,
                            reachProfile.laterStops,
                            reachProfile.laterStopsBudget)
                        : request.evaluateRideNeed?.Invoke(passenger);

                // "NAO SE APLICA" NAO E "NAO QUER".
                //
                // Quem nao responde a pergunta da carona por este canal nao e
                // passageiro desta varredura: sai antes de virar par no produto
                // cartesiano, em vez de disputar uma vaga que nunca pediu e
                // perder por -5000.
                //
                // O descarte mora AQUI, e nao no allowPassenger do consumidor,
                // porque aquele callback e consultado dentro de
                // TryResolvePassengerSlot ANTES do teste de vaga — la, submarino
                // e trem de carga pagariam a avaliacao tambem. Neste ponto a
                // lista ja e so de quem cabe no transporte.
                //
                // A emergencia atravessa: quem precisa de recuperacao continua
                // candidato, porque essa necessidade o transportador enxerga
                // sozinho.
                if (rideNeed != null
                    && rideNeed.captureQuestionInapplicable
                    && !rideNeed.isEmergency)
                {
                    AIDecisionPerf.AddCount(
                        "MelhorEmbarqueRideQuestionInapplicable");
                    request.diagnosticLog?.Invoke(
                        $"DESCARTA pax=#{passenger.InstanceId}: a pergunta " +
                        "da carona nao e respondida por este canal " +
                        "(sem emergencia).");
                    continue;
                }

                acceptedPassengers.Add(passenger);
                passengerReach[passenger] = reachProfile;
                passengerRideNeed[passenger] = rideNeed;
                request.diagnosticLog?.Invoke(
                    $"ACCEPT pax=#{passenger.InstanceId} " +
                    $"slot={passengerSlots[passenger]} " +
                    FormatRideNeedDiagnostic(rideNeed));
            }
        }
        passengers = acceptedPassengers;
        AIDecisionPerf.AddCount(
            "MelhorEmbarquePassengers", passengers.Count);
        if (passengers.Count == 0)
        {
            AIDecisionPerf.AddCount(
                "MelhorEmbarqueRideQuestionEarlyOuts");
            return result;
        }

        // ── O peso da vaga ────────────────────────────────────────────────
        // O casco nao e uma lista de assentos independentes. Uma vaga
        // exclusiva ocupada apaga todas as outras — e, no espelho, um
        // soldado numa vaga comum apaga a vaga exclusiva.
        //
        // Ate aqui a IA so sabia perguntar "posso usar esta vaga AGORA?",
        // contra quem JA esta a bordo. Com o casco vazio a resposta e sim
        // para todo mundo, e o mesmo Chinook era oferecido a quatro
        // passageiros ao mesmo tempo (slot 0 a dois soldados, slot 1 a dois
        // APCs); o motor recusava depois. A pergunta que faltava nao e sobre
        // o estado do casco, e sobre o que uma opcao CUSTA as outras.
        //
        //     peso = unidades que traz − assentos que desloca
        //
        // "Traz" conta a carga aninhada: um APC com um soldado dentro entrega
        // duas unidades numa vaga so. "Desloca" conta apenas assentos que
        // outro candidato do mesmo pedido de fato disputa — casco sem fila,
        // aninhar sai de graca. Sem essa clausula um APC sozinho recusaria
        // um Chinook vazio, que e regressao no cenario do canal.
        //
        // Depende de (passageiro, slot) e NAO da LZ: fica FORA do laco de
        // pares, que e o mais caro do transporte.
        //
        // E mora DEPOIS da poda de acceptedPassengers, nao antes: quem foi
        // DESCARTADO pela pergunta da carona nunca vira opcao, logo nao
        // disputa assento nenhum. Contado antes, ele viraria concorrente
        // fantasma e encareceria a vaga exclusiva contra ninguem.
        var slotDemand = new Dictionary<int, int>();
        for (int i = 0; i < passengers.Count; i++)
        {
            int slot = passengerSlots[passengers[i]];
            slotDemand.TryGetValue(slot, out int demand);
            slotDemand[slot] = demand + 1;
        }

        var passengerUnitsBrought = new Dictionary<UnitManager, int>();
        var passengerSeatsDisplaced = new Dictionary<UnitManager, int>();
        for (int i = 0; i < passengers.Count; i++)
        {
            UnitManager candidate = passengers[i];
            passengerUnitsBrought[candidate] =
                CountUnitsBrought(candidate);
            passengerSeatsDisplaced[candidate] =
                ResolveSeatsDisplaced(
                    request.transporter,
                    transporterData,
                    passengerSlots[candidate],
                    slotDemand);
        }

        // Quem suspende o encerramento antecipado da varredura. Era so
        // emergencia de reparo; passou a incluir quem nao tem rota propria e ja
        // esperou demais. Sem isso o ilhado nao perdia a disputa — ele nao
        // entrava nela, porque a busca parava no primeiro vizinho que embarca
        // agora e as LZs distantes nunca viravam opcao.
        bool hasNonNegotiableRideNeed = false;
        foreach (QueroCaronaResult rideNeed in passengerRideNeed.Values)
        {
            if (IsNonNegotiableRideNeed(rideNeed))
            {
                hasNonNegotiableRideNeed = true;
                break;
            }
        }

        // O índice topológico não possui ordem de alcance. Tactical precisa
        // ser materializado primeiro para que o runtime possa encerrar antes
        // de construir milhares de opções Operational/Strategic que não
        // participarão da decisão desta rodada.
        var orderedCandidateCells =
            new List<Vector3Int>(candidateCells);
        orderedCandidateCells.Sort((left, right) =>
        {
            int leftDistance = Mathf.RoundToInt(
                SectorManager.HexDistance(origin, left));
            int rightDistance = Mathf.RoundToInt(
                SectorManager.HexDistance(origin, right));
            int byDistance = leftDistance.CompareTo(rightDistance);
            if (byDistance != 0)
                return byDistance;
            int byX = left.x.CompareTo(right.x);
            if (byX != 0)
                return byX;
            int byY = left.y.CompareTo(right.y);
            return byY != 0
                ? byY
                : left.z.CompareTo(right.z);
        });

        int topologyCellsVisited = 0;
        int lzWithoutReachableNow = 0;
        bool hasDecisiveTacticalPickup = false;
        bool stoppedAfterTactical = false;
        // Medicao do laco LZ x passageiro: e aqui que mora o produto cartesiano
        // (options = LZs aceitas x passageiros) e o unico trecho pesado do
        // servico que nenhum cronometro cobria. O tempo de ResolvePassengerMeeting
        // e acumulado num float local e publicado UMA vez no fim — cronometrar
        // par a par por AIDecisionPerfScope custaria um dicionario por par.
        float resolveMeetingMs = 0f;
        float lzGateMs = 0f;
        float lzLoopStartedAt = Time.realtimeSinceStartup;
        for (int candidateIndex = 0;
             candidateIndex < orderedCandidateCells.Count;
             candidateIndex++)
        {
            Vector3Int cell = orderedCandidateCells[candidateIndex];
            cell.z = 0;
            int distance = Mathf.RoundToInt(
                SectorManager.HexDistance(origin, cell));
            MelhorEmbarqueTier tier = distance <= tactical
                ? MelhorEmbarqueTier.Tactical
                : distance <= operational
                    ? MelhorEmbarqueTier.Operational
                    : MelhorEmbarqueTier.Strategic;

            if (tier != MelhorEmbarqueTier.Tactical
                && request.stopAfterDecisiveTactical
                && !hasNonNegotiableRideNeed
                && hasDecisiveTacticalPickup)
            {
                stoppedAfterTactical = true;
                break;
            }

            topologyCellsVisited++;

            // REJEICAO BARATA ANTES DA SONDA CARA.
            //
            // Estes dois continues ja existiam — moravam DEPOIS do portao. Uma
            // celula Tactical fora dos Caminhos Validos pagava ~1,3 ms de
            // sensor para ser descartada por um ContainsKey na linha seguinte,
            // e com o encerramento antecipado quase toda celula visitada e
            // Tactical: era o caso comum, nao o raro. Medido em dois Chinooks,
            // o portao rejeitava 77-82% do que sondava.
            //
            // Reordenacao pura: os tres sao continue, nenhum tem efeito
            // colateral, e topologyCellsVisited++ continua contando todas.
            if (tier == MelhorEmbarqueTier.Strategic
                && !request.includeStrategic)
                continue;
            // Distância cúbica classifica o setor, mas não prova um caminho.
            // Uma opção Tactical só pode decidir a rodada quando Caminhos
            // Válidos realmente alcança a LZ.
            if (tier == MelhorEmbarqueTier.Tactical
                && (tacticalPaths == null
                    || !tacticalPaths.ContainsKey(cell)))
                continue;

            // Sonda de tabuleiro POR LZ CANDIDATA — a irma da sonda por par.
            // Depois que a inversao do par derrubou resolveMeeting, e ela que
            // domina o laco. O contador de probes passa a medir so o que foi
            // efetivamente pago, que e o numero que interessa.
            float lzGateStartedAt = Time.realtimeSinceStartup;
            bool lzGateOk =
                request.map.HasTile(cell)
                && PodeEmbarcarSensor.IsTransporterCellValidForEmbark(
                    request.map, request.terrainDatabase,
                    transporterData, cell)
                && (request.transporter.GetDomain() != Domain.Air
                    || PodePousarSensor.CanLandAtCell(
                        request.transporter, request.map,
                        request.terrainDatabase, cell, out _));
            lzGateMs +=
                (Time.realtimeSinceStartup - lzGateStartedAt) * 1000f;
            AIDecisionPerf.AddCount("MelhorEmbarqueLzGateProbes");
            if (!lzGateOk)
            {
                AIDecisionPerf.AddCount("MelhorEmbarqueLzGateRejects");
                continue;
            }

            var lz = new MelhorEmbarqueLzScore
            {
                transporter = request.transporter,
                cell = cell,
                tier = tier,
                transporterDistance = distance,
                transporterRouteCost =
                    tacticalPaths != null
                    && tacticalPaths.TryGetValue(
                        cell, out List<Vector3Int> route)
                        ? Mathf.Max(0, route.Count - 1)
                        : -1
            };
            int optionCountBeforeLz = result.options.Count;

            for (int i = 0; i < passengers.Count; i++)
            {
                UnitManager passenger = passengers[i];
                Vector3Int passengerCell =
                    passenger.CurrentCellPosition;
                passengerCell.z = 0;
                AIDecisionPerf.AddCount("MelhorEmbarquePairs");
                float meetingStartedAt = Time.realtimeSinceStartup;
                ResolvePassengerMeeting(
                    request,
                    passenger,
                    passengerReach[passenger],
                    cell,
                    out MelhorEmbarquePassengerRouteState routeState,
                    out int moveCost,
                    out int embarkCost,
                    out Vector3Int passengerMeetingCell,
                    out bool hasPassengerMeetingCell);
                resolveMeetingMs +=
                    (Time.realtimeSinceStartup - meetingStartedAt) * 1000f;
                AIDecisionPerf.AddCount(
                    ResolvePairRouteCounter(routeState));
                int slotIndex = passengerSlots[passenger];
                QueroCaronaResult rideNeed =
                    passengerRideNeed[passenger];
                MelhorEmbarqueRideDisposition disposition =
                    ResolveRideDisposition(rideNeed);
                // Par gerado por quem JA recusou a carona (alcanca o proprio
                // objetivo). Entra no cruzamento como OpportunisticFallback e
                // paga o laco inteiro para ser classificado por ultimo: e o
                // candidato numero um a poda, e este contador diz o tamanho
                // exato do desperdicio antes de mexer na regra.
                if (disposition ==
                    MelhorEmbarqueRideDisposition.OpportunisticFallback)
                    AIDecisionPerf.AddCount(
                        "MelhorEmbarquePairsOpportunistic");
                float rideNeedAdjustment =
                    ResolveRideNeedAdjustment(
                        disposition, rideNeed);
                // A grade de movimento do passageiro pode nao conter uma
                // LZ naval mesmo que o slot aceite a camada atual dele
                // (ex.: Apache Air/High -> helipad da Fragata). Isso nao
                // invalida o resgate: apenas significa que o transportador
                // deve aproximar-se da LZ mais proxima do passageiro, para
                // que os sensores de embarque resolvam a transicao final.
                // Sem esta distancia, o hex atual do transportador ganha
                // por custo zero e ele fica esperando longe do passageiro.
                // EVAC emergencial nao pode transformar "o Apache consegue
                // voar ate mim" em motivo para a plataforma ficar parada.
                // Nessa modalidade o transportador e que deve aproximar-se
                // do anel de encontro adjacente ao passageiro; embarcar nao
                // acontece no mesmo hex. Aguardar so e aceitavel quando a
                // plataforma ja esta nesse anel. A mesma regra continua
                // cobrindo a ausencia de rota explicita, comum na transicao
                // Air -> plataforma naval.
                bool emergencyEvac = disposition ==
                    MelhorEmbarqueRideDisposition.Emergency;
                float passengerMeetingDistance =
                    SectorManager.HexDistance(passengerCell, cell);
                int passengerTotalCost =
                    moveCost < int.MaxValue
                    && embarkCost < int.MaxValue
                        ? moveCost + embarkCost
                        : int.MaxValue;
                float rescueApproachPenalty = routeState ==
                        MelhorEmbarquePassengerRouteState.NoCurrentRoute
                    || emergencyEvac
                    ? Mathf.Abs(passengerMeetingDistance - 1f) * 10000f
                    : 0f;

                // "Nao tem rota" e defeito da OPCAO ou condicao do PASSAGEIRO?
                //
                // Para quem tem pernas, e defeito: entre duas LZ, a que ele nao
                // alcanca vale menos, e a penalidade ordena isso direito.
                //
                // Para quem nunca tera rota — ilha, corredor bloqueado, camada
                // incompativel — ela nao descreve a opcao, descreve a vida
                // dele. Cobrada assim, o mais necessitado vira o mais caro de
                // servir e perde sempre: "quero carona" pagava +1000 enquanto
                // "nao tenho rota nenhuma" custava -10000. Quem precisa de
                // resgate era penalizado por precisar de resgate.
                //
                // O anel de aproximacao (rescueApproachPenalty) continua
                // valendo e e ele que ordena as LZ desse passageiro: o
                // transportador tem de encostar, porque quem nao anda nao vai
                // ao encontro.
                float routePenalty = ResolvePassengerRoutePenalty(
                    routeState, passengerTotalCost);
                if (routeState ==
                        MelhorEmbarquePassengerRouteState.NoCurrentRoute
                    && IsNonNegotiableRideNeed(rideNeed))
                {
                    routePenalty = 0f;
                }

                int unitsBrought = passengerUnitsBrought[passenger];
                int seatsDisplaced = passengerSeatsDisplaced[passenger];
                int slotWeight = unitsBrought - seatsDisplaced;

                float optionScore = 100000f
                    - distance * 100f
                    - routePenalty
                    - rescueApproachPenalty
                    + rideNeedAdjustment
                    + slotWeight * SlotWeightScale;
                var option = new MelhorEmbarqueOption
                {
                    transporter = request.transporter,
                    passenger = passenger,
                    slotIndex = slotIndex,
                    passengerCell = passengerCell,
                    passengerMeetingCell = passengerMeetingCell,
                    hasPassengerMeetingCell =
                        hasPassengerMeetingCell,
                    lzCell = cell,
                    transporterTier = tier,
                    transporterDistance = distance,
                    transporterRouteCost =
                        lz.transporterRouteCost,
                    passengerRouteState = routeState,
                    passengerRouteCost =
                        moveCost < int.MaxValue ? moveCost : -1,
                    passengerEmbarkCost =
                        embarkCost < int.MaxValue
                            ? embarkCost
                            : -1,
                    passengerTotalCost =
                        passengerTotalCost < int.MaxValue
                            ? passengerTotalCost
                            : -1,
                    rideDisposition = disposition,
                    rideNeed = rideNeed,
                    rideNeedAdjustment = rideNeedAdjustment,
                    unitsBrought = unitsBrought,
                    seatsDisplaced = seatsDisplaced,
                    slotWeight = slotWeight,
                    score = optionScore,
                    reason =
                        $"slot={slotIndex} LZ={cell} " +
                        $"encontroPax={passengerMeetingCell} " +
                        $"rotaPax={routeState} " +
                        $"custoPax=" +
                        $"{(moveCost < int.MaxValue ? moveCost.ToString() : "n/a")} " +
                        $"custoEmbarque=" +
                        $"{(embarkCost < int.MaxValue ? embarkCost.ToString() : "n/a")} " +
                        $"custoTotal=" +
                        $"{(passengerTotalCost < int.MaxValue ? passengerTotalCost.ToString() : "n/a")} " +
                        $"distTransport={distance} " +
                        $"aproxPax={passengerMeetingDistance:F0} " +
                        $"carona={disposition} " +
                        $"ajusteCarona={rideNeedAdjustment:0} " +
                        $"peso={slotWeight:+0;-0;0} " +
                        $"(traz={unitsBrought} " +
                        $"desloca={seatsDisplaced})"
                };
                result.options.Add(option);
                if (tier == MelhorEmbarqueTier.Tactical
                    && lz.transporterRouteCost >= 0
                    && routeState
                        == MelhorEmbarquePassengerRouteState.ReachableNow
                    && disposition
                        == MelhorEmbarqueRideDisposition.Requested)
                {
                    hasDecisiveTacticalPickup = true;
                }

                // Compatibilidade da parte 1: o controller atual enxerga a
                // coleção legada somente quando o passageiro chega agora.
                // ReachableLater/NoCurrentRoute já aparecem no ranking plano,
                // mas só passam a influenciar decisões nas próximas partes.
                if (routeState ==
                        MelhorEmbarquePassengerRouteState.ReachableNow
                    && (request.includeInLegacyRanking == null
                        || request.includeInLegacyRanking(passenger)))
                {
                    lz.passengers.Add(
                        new MelhorEmbarquePassengerScore
                        {
                            passenger = passenger,
                            slotIndex = slotIndex,
                            passengerCell = passengerCell,
                            passengerMeetingCell =
                                passengerMeetingCell,
                            passengerMoveCost = moveCost,
                            reason = option.reason
                        });
                }
            }

            int optionCountForLz =
                result.options.Count - optionCountBeforeLz;
            if (optionCountForLz <= 0)
                continue;
            if (lz.passengers.Count == 0)
            {
                lzWithoutReachableNow++;
                continue;
            }

            int legacyFirstCost = lz.passengers.Count > 0
                ? lz.passengers[0].passengerMoveCost
                : 0;
            lz.score = lz.passengers.Count * 100000f
                     - distance * 100f
                     - legacyFirstCost;
            lz.reason =
                $"tier={tier} paxAgora={lz.passengers.Count} " +
                $"opcoes={optionCountForLz} " +
                $"distTransport={distance} " +
                $"rotaTatica={lz.transporterRouteCost}";

            result.ranking.Add(lz);
            request.diagnosticLog?.Invoke(
                $"LZ={cell} {lz.reason}");
        }

        AIDecisionPerf.Add(
            "melhorEmbarque.lzLoop",
            (Time.realtimeSinceStartup - lzLoopStartedAt) * 1000f);
        AIDecisionPerf.Add(
            "melhorEmbarque.resolveMeeting", resolveMeetingMs);
        AIDecisionPerf.Add(
            "melhorEmbarque.lzGates", lzGateMs);
        AIDecisionPerf.AddCount(
            "TopologyCellsVisited",
            topologyCellsVisited);
        AIDecisionPerf.AddCount(
            "CellsVisited",
            topologyCellsVisited);
        if (usedTopologyIndex)
        {
            AIDecisionPerf.AddCount(
                "TopologyIndexCandidateCells",
                topologyCellsVisited);
        }
        if (stoppedAfterTactical)
        {
            AIDecisionPerf.AddCount(
                "MelhorEmbarqueDecisiveTacticalEarlyOuts");
            request.diagnosticLog?.Invoke(
                "Ranking encerrado no Tactical: existe passageiro " +
                "Requested + ReachableNow e nenhuma emergência pendente.");
        }
        if (lzWithoutReachableNow > 0)
        {
            request.diagnosticLog?.Invoke(
                $"LZs sem passageiro ReachableNow=" +
                $"{lzWithoutReachableNow}; preservadas apenas no " +
                "ranking plano.");
        }
        result.ranking.Sort(Compare);
        result.options.Sort(CompareOptions);
        return result;
    }

    private static IReadOnlyList<Vector3Int> ResolveCandidateCells(
        MelhorEmbarqueRequest request,
        out bool usedTopologyIndex)
    {
        BoardTopologyIndex topology =
            BoardTopologyIndex.GetOrCreateRuntime(
                request.map,
                request.terrainDatabase);
        usedTopologyIndex = topology != null && topology.IsReady;
        if (usedTopologyIndex)
            return topology.IndexedCells;

        var fallback = new List<Vector3Int>();
        foreach (Vector3Int rawCell in
                 request.map.cellBounds.allPositionsWithin)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            fallback.Add(cell);
        }
        return fallback;
    }

    private static bool TryResolvePassengerSlot(
        MelhorEmbarqueRequest request,
        UnitData transporterData,
        UnitManager passenger,
        out int slotIndex,
        out string reason)
    {
        slotIndex = -1;
        reason = string.Empty;
        if (passenger == null || passenger == request.transporter)
        {
            reason = "unidade invalida";
            return false;
        }
        if (passenger.IsDead || passenger.IsEmbarked)
        {
            reason = passenger.IsDead ? "morta" : "ja embarcada";
            return false;
        }
        if (!PlayerSlotRelations.AreAllies(
                request.transporter, passenger))
        {
            reason = "nao aliada";
            return false;
        }
        if (request.allowPassenger != null
            && !request.allowPassenger(passenger))
        {
            reason = "bloqueada pelo consumidor";
            return false;
        }
        if (!passenger.TryGetUnitData(out UnitData passengerData)
            || passengerData == null)
        {
            reason = "sem UnitData";
            return false;
        }

        string lastReason = "nenhum slot compativel";
        for (int i = 0; i < transporterData.transportSlots.Count; i++)
        {
            UnitTransportSlotRule slot =
                transporterData.transportSlots[i];
            if (!PodeEmbarcarSensor.CanUseSlot(
                    passenger, passengerData, slot,
                    out lastReason))
                continue;
            if (!request.transporter.CanUseTransportSlotExclusivity(
                    i, out lastReason))
                continue;
            int occupied =
                request.transporter.GetOccupiedTransportSeatCountForSlot(i);
            if (occupied >= Mathf.Max(1, slot.capacity))
            {
                lastReason = $"slot {i} lotado";
                continue;
            }

            slotIndex = i;
            reason = "compativel";
            return true;
        }

        reason = lastReason;
        return false;
    }

    public static bool TryResolveCompatiblePassengerSlot(
        UnitManager transporter,
        UnitManager passenger,
        out int slotIndex,
        out string reason)
    {
        slotIndex = -1;
        reason = string.Empty;
        if (transporter == null
            || !transporter.TryGetUnitData(
                out UnitData transporterData)
            || transporterData == null
            || !transporterData.isTransporter
            || transporterData.transportSlots == null
            || transporterData.transportSlots.Count == 0)
        {
            reason = "transportador invalido ou sem slots";
            return false;
        }

        return TryResolvePassengerSlot(
            new MelhorEmbarqueRequest
            {
                transporter = transporter,
                allowPassenger = candidate =>
                    candidate == passenger
            },
            transporterData,
            passenger,
            out slotIndex,
            out reason);
    }

    private static PassengerReachProfile BuildPassengerReachProfile(
        MelhorEmbarqueRequest request,
        UnitManager passenger,
        BoardTopologyIndex topology)
    {
        Vector3Int origin = passenger.CurrentCellPosition;
        origin.z = 0;
        int nowBudget = Mathf.Max(
            0, passenger.RemainingMovementPoints);
        int laterBudget = Mathf.Max(
            nowBudget,
            passenger.MaxMovementPoints
            * Mathf.Max(1, request.operationalTurns));
        Dictionary<Vector3Int, int> nowStops =
            UnitMovementPathRules.CalculateMovementCostMap(
                request.map, passenger, origin, nowBudget,
                request.terrainDatabase);
        Dictionary<Vector3Int, int> laterStops =
            UnitMovementPathRules.CalculateMovementCostMap(
                request.map, passenger, origin, laterBudget,
                request.terrainDatabase);
        return new PassengerReachProfile
        {
            // A consulta seguinte pergunta pela LZ, nao pela celula final
            // do passageiro. Expandir cada parada uma unica vez para ela
            // mesma e seus seis vizinhos troca a busca linear repetida por
            // uma consulta O(1), preservando o mesmo encontro a distancia 1.
            now = BuildMeetingCostMap(
                request.map, topology, nowStops),
            later = BuildMeetingCostMap(
                request.map, topology, laterStops),
            laterStops = laterStops,
            topology = topology,
            nowBudget = nowBudget,
            laterStopsBudget = laterBudget
        };
    }

    private static Dictionary<Vector3Int, PassengerMeeting>
        BuildMeetingCostMap(
        Tilemap map,
        BoardTopologyIndex topology,
        Dictionary<Vector3Int, int> stopCosts)
    {
        var meetingCosts =
            new Dictionary<Vector3Int, PassengerMeeting>();
        if (stopCosts == null || stopCosts.Count == 0)
            return meetingCosts;

        var fallbackNeighbors = new List<Vector3Int>(6);
        foreach (KeyValuePair<Vector3Int, int> pair in stopCosts)
        {
            Vector3Int stop = pair.Key;
            stop.z = 0;
            SetMinimumMeetingCost(
                meetingCosts, stop, stop, pair.Value);

            IReadOnlyList<Vector3Int> neighbors;
            if (topology != null && topology.IsReady)
            {
                neighbors = topology.GetNeighbors(stop);
            }
            else
            {
                UnitMovementPathRules.GetImmediateHexNeighbors(
                    map, stop, fallbackNeighbors);
                neighbors = fallbackNeighbors;
            }

            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int neighbor = neighbors[i];
                neighbor.z = 0;
                SetMinimumMeetingCost(
                    meetingCosts, neighbor, stop, pair.Value);
            }
        }

        return meetingCosts;
    }

    private static void SetMinimumMeetingCost(
        Dictionary<Vector3Int, PassengerMeeting> costs,
        Vector3Int transporterCell,
        Vector3Int passengerCell,
        int candidateCost)
    {
        if (!costs.TryGetValue(
                transporterCell,
                out PassengerMeeting current)
            || candidateCost < current.moveCost
            || (candidateCost == current.moveCost
                && CompareCells(
                    passengerCell,
                    current.passengerCell) < 0))
        {
            costs[transporterCell] =
                new PassengerMeeting(
                    passengerCell,
                    candidateCost);
        }
    }

    private static void ResolvePassengerMeeting(
        MelhorEmbarqueRequest request,
        UnitManager passenger,
        PassengerReachProfile profile,
        Vector3Int transporterCell,
        out MelhorEmbarquePassengerRouteState state,
        out int moveCost,
        out int embarkCost,
        out Vector3Int passengerMeetingCell,
        out bool hasPassengerMeetingCell)
    {
        hasPassengerMeetingCell = false;
        passengerMeetingCell = passenger != null
            ? passenger.CurrentCellPosition
            : transporterCell;
        passengerMeetingCell.z = 0;
        embarkCost = int.MaxValue;
        moveCost = int.MaxValue;
        state = MelhorEmbarquePassengerRouteState.NoCurrentRoute;
        if (request == null || passenger == null || profile == null)
            return;

        // ORDEM: encontro primeiro (dicionario), sensor depois.
        //
        // A sonda de embarque custa ~0,7 ms — sao quatro consultas de tabuleiro
        // SEM cache (construcao, estrutura, terreno, tile) dentro de
        // TryGetEnterCellCost. Ela era paga uma vez POR PAR, antes da busca
        // barata que rejeita a maioria: no Suprimentos#24, 86 dos 92 pares
        // terminavam em NoCurrentRoute e nenhum deles falhou por embarque — a
        // sonda respondia certo e o dicionario descartava logo depois.
        //
        // Invertido, o sensor so roda quando existe encontro para pontuar. Onde
        // nao ha encontro o veredito e NoCurrentRoute de qualquer jeito, e a
        // pontuacao daquele par nao le embarkCost (routePenalty e 10000 fixo).
        // Mesmo resultado, sem a sonda.
        bool hasNow = TryFindMeetingCost(
            profile.now,
            transporterCell,
            out PassengerMeeting nowMeeting);
        bool hasLater = TryFindMeetingCost(
            profile.later,
            transporterCell,
            out PassengerMeeting laterMeeting);
        bool hasLongRange = false;
        PassengerMeeting longRangeMeeting = default;
        if (!hasNow
            && !hasLater
            && request.resolveLongRangePassengerMeeting)
        {
            EnsureLongRangeMeetingMap(
                request, passenger, profile);
            hasLongRange = TryFindMeetingCost(
                profile.longRange,
                transporterCell,
                out longRangeMeeting);
        }

        if (!hasNow && !hasLater && !hasLongRange)
        {
            AIDecisionPerf.AddCount("MelhorEmbarqueEmbarkProbeSkips");
            return;
        }

        AIDecisionPerf.AddCount("MelhorEmbarqueEmbarkProbes");
        if (!PodeEmbarcarSensor.TryGetEmbarkCostAtCell(
                request.map,
                request.terrainDatabase,
                passenger,
                transporterCell,
                out embarkCost,
                out _))
        {
            AIDecisionPerf.AddCount("MelhorEmbarqueEmbarkProbeRejects");
            embarkCost = int.MaxValue;
            return;
        }

        if (hasNow
            && CanAffordMeeting(
                nowMeeting.moveCost,
                embarkCost,
                profile.nowBudget))
        {
            moveCost = nowMeeting.moveCost;
            passengerMeetingCell = nowMeeting.passengerCell;
            hasPassengerMeetingCell = true;
            state = MelhorEmbarquePassengerRouteState.ReachableNow;
            return;
        }
        if (hasLater
            && CanAffordMeeting(
                laterMeeting.moveCost,
                embarkCost,
                profile.laterStopsBudget))
        {
            moveCost = laterMeeting.moveCost;
            passengerMeetingCell = laterMeeting.passengerCell;
            hasPassengerMeetingCell = true;
            state = MelhorEmbarquePassengerRouteState.ReachableLater;
            return;
        }

        // Encontro existia mas nenhum coube no orcamento: o longo alcance ainda
        // e a ultima chance, exatamente como antes da inversao. So aqui ele e
        // materializado para quem tinha now/later — a consulta la em cima cobre
        // apenas quem nao tinha encontro nenhum.
        if (!hasLongRange && request.resolveLongRangePassengerMeeting)
        {
            EnsureLongRangeMeetingMap(
                request, passenger, profile);
            hasLongRange = TryFindMeetingCost(
                profile.longRange,
                transporterCell,
                out longRangeMeeting);
        }

        if (hasLongRange)
        {
            moveCost = longRangeMeeting.moveCost;
            passengerMeetingCell = longRangeMeeting.passengerCell;
            hasPassengerMeetingCell = true;
            state =
                MelhorEmbarquePassengerRouteState.ReachableStrategic;
            return;
        }

        moveCost = int.MaxValue;
    }

    private static void EnsureLongRangeMeetingMap(
        MelhorEmbarqueRequest request,
        UnitManager passenger,
        PassengerReachProfile profile)
    {
        if (profile.longRange != null)
            return;

        // Varredura de tabuleiro INTEIRO (orcamento int.MaxValue/4) por
        // passageiro. Memoizada no perfil, mas basta um par cair aqui para
        // pagar o mapa todo — por isso ela e cronometrada a parte.
        using var perf = new AIDecisionPerfScope(
            passenger, "melhorEmbarque.longRangeMap");
        AIDecisionPerf.AddCount("MelhorEmbarqueLongRangeMapBuilds");
        Vector3Int origin = passenger.CurrentCellPosition;
        origin.z = 0;
        Dictionary<Vector3Int, int> longRangeStops =
            UnitMovementPathRules.CalculateMovementCostMap(
                request.map,
                passenger,
                origin,
                int.MaxValue / 4,
                request.terrainDatabase);
        profile.longRange = BuildMeetingCostMap(
            request.map,
            profile.topology,
            longRangeStops);
    }

    // Distribuicao dos pares por estado de rota do passageiro. Diz de que tipo
    // sao os milhares de opcoes: se quase tudo volta NoRoute, o cruzamento esta
    // sendo pago para produzir lixo; se e Strategic, o mapa de longo alcance e
    // que esta cobrando.
    private static string ResolvePairRouteCounter(
        MelhorEmbarquePassengerRouteState state)
    {
        switch (state)
        {
            case MelhorEmbarquePassengerRouteState.ReachableNow:
                return "MelhorEmbarquePairsReachableNow";
            case MelhorEmbarquePassengerRouteState.ReachableLater:
                return "MelhorEmbarquePairsReachableLater";
            case MelhorEmbarquePassengerRouteState.ReachableStrategic:
                return "MelhorEmbarquePairsStrategic";
            default:
                return "MelhorEmbarquePairsNoRoute";
        }
    }

    private static bool CanAffordMeeting(
        int movementCost,
        int embarkCost,
        int budget)
    {
        return movementCost >= 0
            && embarkCost >= 1
            && movementCost <= budget
            && embarkCost <= budget - movementCost;
    }

    private static bool TryFindMeetingCost(
        Dictionary<Vector3Int, PassengerMeeting> costs,
        Vector3Int transporterCell,
        out PassengerMeeting meeting)
    {
        meeting = default;
        if (costs == null)
            return false;
        transporterCell.z = 0;
        return costs.TryGetValue(
            transporterCell, out meeting);
    }

    private static int CompareCells(
        Vector3Int a,
        Vector3Int b)
    {
        int byX = a.x.CompareTo(b.x);
        if (byX != 0)
            return byX;
        int byY = a.y.CompareTo(b.y);
        return byY != 0 ? byY : a.z.CompareTo(b.z);
    }

    private static float ResolvePassengerRoutePenalty(
        MelhorEmbarquePassengerRouteState state,
        int moveCost)
    {
        switch (state)
        {
            case MelhorEmbarquePassengerRouteState.ReachableNow:
                return Mathf.Max(0, moveCost);
            case MelhorEmbarquePassengerRouteState.ReachableLater:
                return 1000f + Mathf.Max(0, moveCost);
            case MelhorEmbarquePassengerRouteState.ReachableStrategic:
                return 5000f + Mathf.Max(0, moveCost);
            default:
                return 10000f;
        }
    }

    private static MelhorEmbarqueRideDisposition
        ResolveRideDisposition(QueroCaronaResult rideNeed)
    {
        if (rideNeed == null)
            return MelhorEmbarqueRideDisposition.NotEvaluated;
        if (rideNeed.isEmergency)
            return MelhorEmbarqueRideDisposition.Emergency;
        return rideNeed.wantsRide
            ? MelhorEmbarqueRideDisposition.Requested
            : MelhorEmbarqueRideDisposition.OpportunisticFallback;
    }

    /// <summary>
    /// Turnos na fila a partir dos quais o pedido deixa de ser negociavel.
    ///
    /// Nao e "todo ilhado suspende tudo": um ilhado recem-chegado nao pode
    /// custar a varredura completa todo turno. E a ESPERA que promove — quem
    /// foi ignorado tres vezes seguidas para de ser ignoravel.
    /// </summary>
    private const int NonNegotiableRideWaitTurns = 3;

    // Calibre do peso da vaga. O termo ordena PARES — entre duas opcoes da
    // mesma disposicao de carona — e nunca atropela um resgate: a faixa util
    // do peso e ~[-3, +2], logo ate 900, abaixo dos 1000 que separam
    // Requested de Emergency.
    private const float SlotWeightScale = 300f;

    /// <summary>
    /// Pedido que a esteira nao pode mais adiar: emergencia de reparo, ou
    /// alguem sem rota propria que ja esperou demais.
    ///
    /// Existe porque as duas regras de eficiencia do ranking — parar cedo e
    /// punir quem nao tem rota — foram desenhadas olhando UM passageiro por
    /// vez, e juntas produzem fome: o mais necessitado e sempre o mais caro de
    /// servir, entao ele perde sempre.
    /// </summary>
    private static bool IsNonNegotiableRideNeed(QueroCaronaResult rideNeed)
    {
        if (rideNeed == null || !rideNeed.wantsRide)
            return false;
        if (rideNeed.isEmergency)
            return true;
        return rideNeed.isStranded
               && rideNeed.rideWaitTurns >= NonNegotiableRideWaitTurns;
    }

    /// <summary>
    /// Quantas unidades a opcao entrega de fato. Um APC com um soldado dentro
    /// vale duas: a carga aninhada sobe junto e ocupa uma vaga so.
    /// </summary>
    private static int CountUnitsBrought(UnitManager passenger)
    {
        if (passenger == null)
            return 0;

        int total = 1;
        IReadOnlyList<UnitTransportSeatRuntime> seats =
            passenger.TransportedUnitSlots;
        if (seats == null)
            return total;

        for (int i = 0; i < seats.Count; i++)
        {
            UnitTransportSeatRuntime seat = seats[i];
            if (seat == null
                || seat.embarkedUnit == null
                || !seat.embarkedUnit.IsEmbarked)
                continue;
            total += CountUnitsBrought(seat.embarkedUnit);
        }
        return total;
    }

    /// <summary>
    /// Assentos que esta opcao tira dos OUTROS candidatos do mesmo pedido.
    /// So conta assento disputado: capacidade que ninguem quer nao e custo.
    /// </summary>
    private static int ResolveSeatsDisplaced(
        UnitManager transporter,
        UnitData transporterData,
        int chosenSlotIndex,
        Dictionary<int, int> slotDemand)
    {
        List<UnitTransportSlotRule> slots =
            transporterData?.transportSlots;
        if (transporter == null
            || slots == null
            || chosenSlotIndex < 0
            || chosenSlotIndex >= slots.Count
            || slots[chosenSlotIndex] == null)
            return 0;

        bool chosenIsExclusive = slots[chosenSlotIndex].exclusiveSlot;
        int displaced = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            UnitTransportSlotRule slot = slots[i];
            if (slot == null)
                continue;

            slotDemand.TryGetValue(i, out int demand);
            // O proprio candidato nao disputa consigo mesmo.
            if (i == chosenSlotIndex)
                demand--;
            if (demand <= 0)
                continue;

            int free = Mathf.Max(
                0,
                Mathf.Max(1, slot.capacity)
                - transporter.GetOccupiedTransportSeatCountForSlot(i));
            if (free <= 0)
                continue;

            // Na propria vaga some o assento ocupado. Nas demais some a vaga
            // inteira quando a exclusividade morde — dos dois lados, porque
            // CanUseTransportSlotExclusivity recusa tanto entrar numa vaga
            // exclusiva com carga alheia quanto entrar numa vaga comum
            // havendo exclusiva ocupada.
            int lost = i == chosenSlotIndex
                ? 1
                : chosenIsExclusive || slot.exclusiveSlot
                    ? free
                    : 0;
            displaced += Mathf.Min(lost, demand);
        }
        return displaced;
    }

    private static float ResolveRideNeedAdjustment(
        MelhorEmbarqueRideDisposition disposition,
        QueroCaronaResult rideNeed)
    {
        switch (disposition)
        {
            case MelhorEmbarqueRideDisposition.Emergency:
                return Mathf.Max(
                    2000f, rideNeed?.rideNeedScore ?? 0);
            case MelhorEmbarqueRideDisposition.Requested:
                return Mathf.Max(
                    1000f, rideNeed?.rideNeedScore ?? 0);
            case MelhorEmbarqueRideDisposition.OpportunisticFallback:
                return -5000f;
            default:
                return 0f;
        }
    }

    private static string FormatRideNeedDiagnostic(
        QueroCaronaResult rideNeed)
    {
        if (rideNeed == null)
            return "carona=NotEvaluated";
        MelhorEmbarqueRideDisposition disposition =
            ResolveRideDisposition(rideNeed);
        return $"carona={disposition} " +
               $"ajuste={ResolveRideNeedAdjustment(disposition, rideNeed):0} " +
               (rideNeed.rideWaitTurns > 0
                   ? $"fila={rideNeed.rideWaitTurns}t "
                   : string.Empty) +
               (IsNonNegotiableRideNeed(rideNeed)
                   ? "INEGOCIÁVEL "
                   : string.Empty) +
               $"motivo={rideNeed.reason}";
    }

    private static int Compare(
        MelhorEmbarqueLzScore a,
        MelhorEmbarqueLzScore b)
    {
        int byTier = a.tier.CompareTo(b.tier);
        if (byTier != 0) return byTier;
        int byPassengers =
            b.passengers.Count.CompareTo(a.passengers.Count);
        if (byPassengers != 0) return byPassengers;
        int byDistance =
            a.transporterDistance.CompareTo(b.transporterDistance);
        if (byDistance != 0) return byDistance;
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0) return byScore;
        if (a.transporter != null && b.transporter != null)
        {
            return a.transporter.InstanceId.CompareTo(
                b.transporter.InstanceId);
        }
        return a.transporter != null
            ? -1
            : b.transporter != null ? 1 : 0;
    }

    private static int CompareOptions(
        MelhorEmbarqueOption a,
        MelhorEmbarqueOption b)
    {
        int byTier = a.transporterTier.CompareTo(
            b.transporterTier);
        if (byTier != 0) return byTier;
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0) return byScore;
        int byPassenger = a.passenger != null
            && b.passenger != null
                ? a.passenger.InstanceId.CompareTo(
                    b.passenger.InstanceId)
                : 0;
        if (byPassenger != 0) return byPassenger;
        int byTransporter =
            a.transporter != null && b.transporter != null
                ? a.transporter.InstanceId.CompareTo(
                    b.transporter.InstanceId)
                : 0;
        if (byTransporter != 0) return byTransporter;
        int byX = a.lzCell.x.CompareTo(b.lzCell.x);
        return byX != 0 ? byX : a.lzCell.y.CompareTo(b.lzCell.y);
    }
}
