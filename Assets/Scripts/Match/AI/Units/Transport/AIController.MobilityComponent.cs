using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    // ------------------------------------------------------------------
    // COMPONENTE DE MOVIMENTO — "eu chego ate voce?"
    //
    // O isStranded do passageiro responde metade da pergunta: "nao alcanco meu
    // objetivo a pe". A outra metade e do transportador: adianta nada prometer
    // resgate se o veiculo tambem nao chega. APC nao cruza o mar.
    //
    // CUSTO — a licao cara desta funcao. A primeira versao construia o
    // componente (flood fill do tabuleiro inteiro, SEM cache de consulta de
    // terreno) para o transportador E para cada passageiro, e depois varria o
    // componente completo em cada par. Com 32 candidatos isso custou 43
    // SEGUNDOS num turno que rodava em 852ms.
    //
    // O desenho atual e assimetrico de proposito:
    //   transportador -> componente completo. E ele que viaja longe, e a
    //                    pergunta de conectividade e dele. Um flood por perfil
    //                    de movimento, compartilhado entre unidades iguais.
    //   passageiro    -> alcance LIMITADO (malha de custo de ~2 turnos, que
    //                    passa pelo MovementReachCache). Encontro nao e "ele
    //                    caminha meio mapa ate a praia": e "ele chega ao ponto
    //                    de encontro em tempo util".
    // ------------------------------------------------------------------

    /// <summary>Turnos de caminhada que o passageiro pode gastar rumo ao encontro.</summary>
    private const int MeetingWalkTurns = 2;

    private sealed class MobilityComponent
    {
        public int Id;
        public Dictionary<Vector3Int, int> Cells;
    }

    // perfil de movimento -> componentes ja descobertos para esse perfil.
    private readonly Dictionary<int, List<MobilityComponent>>
        mobilityComponentsByProfile =
            new Dictionary<int, List<MobilityComponent>>();
    private int nextMobilityComponentId = 1;

    /// <summary>
    /// Duas unidades com o mesmo perfil de movimento na mesma massa de terra
    /// tem o MESMO componente — literalmente o mesmo conjunto. Reconhecer isso
    /// derruba 32 flood fills para um por perfil.
    ///
    /// O teto de MP entra na chave porque o componente depende dele: hex mais
    /// caro que o teto de um turno e intransponivel para sempre, entao obus de
    /// 2 MP e soldado de 3 MP nao tem o mesmo mapa.
    /// </summary>
    private static int BuildMobilityProfileKey(UnitManager unit)
    {
        unchecked
        {
            int hash = unit.TryGetUnitData(out UnitData data) && data != null
                ? data.GetEntityId().GetHashCode()
                : 0;
            hash = (hash * 397) ^ (int)unit.GetDomain();
            hash = (hash * 397) ^ (int)unit.GetHeightLevel();
            hash = (hash * 397) ^ (unit.IsEmbarked ? 1 : 0);
            hash = (hash * 397) ^ Mathf.Max(0, unit.MaxMovementPoints);
            return hash;
        }
    }

    private MobilityComponent GetOrBuildMobilityComponent(UnitManager unit)
    {
        if (unit == null)
            return null;

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;
        int profileKey = BuildMobilityProfileKey(unit);
        if (!mobilityComponentsByProfile.TryGetValue(
                profileKey, out List<MobilityComponent> known))
        {
            known = new List<MobilityComponent>(2);
            mobilityComponentsByProfile[profileKey] = known;
        }

        for (int i = 0; i < known.Count; i++)
        {
            if (known[i].Cells.ContainsKey(origin))
            {
                AIDecisionPerf.AddCount("MobilityComponentHits");
                return known[i];
            }
        }

        AIDecisionPerf.AddCount("MobilityComponentBuilds");
        var built = new MobilityComponent
        {
            Id = nextMobilityComponentId++,
            Cells = UnitReachEnvelopeService.BuildOwnMovementComponent(
                unit, boardTilemap, terrainDatabase)
        };
        known.Add(built);
        return built;
    }

    // (componente do transportador, componente do passageiro) -> se encontram.
    // Chaveado por COMPONENTE, nao por unidade: 32 passageiros do mesmo perfil
    // na mesma massa de terra sao uma pergunta so.
    private readonly Dictionary<long, bool> transporterMeetCache =
        new Dictionary<long, bool>();

    /// <summary>
    /// Existe encontro possivel entre estes dois?
    ///
    ///   - infantaria no continente + navio: ela caminha ate a praia, que e
    ///     vizinha de agua. Encontro existe.
    ///   - infantaria na ilha + APC terrestre: o componente dela e a ilha, o
    ///     dele e o continente, e nenhum hex vizinho pertence aos dois.
    ///   - qualquer um + aeronave: o componente da aeronave e o tabuleiro.
    /// </summary>
    private bool CanTransporterMeetPassenger(
        UnitManager transporter,
        UnitManager passenger)
    {
        if (transporter == null || passenger == null)
            return false;

        MobilityComponent transporterComponent =
            GetOrBuildMobilityComponent(transporter);
        if (transporterComponent?.Cells == null
            || transporterComponent.Cells.Count == 0)
        {
            return false;
        }

        Vector3Int passengerCell = passenger.CurrentCellPosition;
        passengerCell.z = 0;

        // Caminho curto, sem cache nenhum: o transportador ja encosta no
        // passageiro. Resolve a maioria esmagadora dos pares.
        if (TouchesComponent(transporterComponent.Cells, passengerCell))
            return true;

        // O passageiro NAO ganha componente proprio: seria um flood fill do
        // tabuleiro inteiro so para virar chave de memo. Basta o alcance
        // limitado dele, que ja passa por cache.
        long pairKey = ((long)transporterComponent.Id << 32)
                       ^ (uint)passenger.InstanceId;
        if (transporterMeetCache.TryGetValue(pairKey, out bool memo))
            return memo;

        bool result = ResolveMeetingByPassengerWalk(
            transporterComponent.Cells, passenger, passengerCell);
        transporterMeetCache[pairKey] = result;
        return result;
    }

    /// <summary>
    /// O passageiro caminha ate o ponto de encontro — mas dentro de um
    /// orcamento, nao pelo componente inteiro. A malha de custo limitada passa
    /// pelo MovementReachCache; o componente inteiro nao passa por cache algum
    /// e foi o que custou os 43 segundos.
    /// </summary>
    private bool ResolveMeetingByPassengerWalk(
        Dictionary<Vector3Int, int> transporterCells,
        UnitManager passenger,
        Vector3Int passengerCell)
    {
        int walkBudget = Mathf.Max(1, passenger.MaxMovementPoints)
                         * MeetingWalkTurns;
        Dictionary<Vector3Int, int> walkable =
            UnitMovementPathRules.CalculateMovementCostMap(
                boardTilemap,
                passenger,
                passengerCell,
                walkBudget,
                terrainDatabase);
        if (walkable == null)
            return false;

        foreach (Vector3Int cell in walkable.Keys)
        {
            if (TouchesComponent(transporterCells, cell))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A celula, ou algum vizinho dela, pertence ao componente? Vizinho porque
    /// embarque acontece com os dois em hexes adjacentes, nao no mesmo hex.
    /// </summary>
    private bool TouchesComponent(
        Dictionary<Vector3Int, int> component,
        Vector3Int cell)
    {
        cell.z = 0;
        if (component.ContainsKey(cell))
            return true;

        meetingNeighborBuffer.Clear();
        UnitMovementPathRules.GetImmediateHexNeighbors(
            boardTilemap, cell, meetingNeighborBuffer);
        for (int i = 0; i < meetingNeighborBuffer.Count; i++)
        {
            Vector3Int neighbor = meetingNeighborBuffer[i];
            neighbor.z = 0;
            if (component.ContainsKey(neighbor))
                return true;
        }
        return false;
    }

    private readonly List<Vector3Int> meetingNeighborBuffer =
        new List<Vector3Int>(6);
}
