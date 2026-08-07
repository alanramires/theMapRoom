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
    // A assimetria ANTERIOR (transportador com componente completo, passageiro
    // com malha de ~2 turnos) media outra coisa: "ele chega ao encontro em
    // tempo util". Isso e POLITICA — e num naval ela decidia por geometria, nao
    // por doutrina: o Chinook passava porque o componente aereo encosta em
    // todos, e o navio reprovava porque a praia ficava a 3 turnos de caminhada.
    // Filtro chamado ESTRUTURAL responde estrutura: os dois se tocam, algum
    // dia? Prazo e ranking, uma camada acima.
    //
    // O custo dos 43 SEGUNDOS era do flood fill SEM CACHE, feito por par. Nao
    // e mais o caso: GetOrBuildMobilityComponent e por PERFIL de movimento, e
    // o memo abaixo e por PAR DE COMPONENTES — quatro soldados iguais na mesma
    // ilha sao uma pergunta so, nao quatro.
    // ------------------------------------------------------------------

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
    // Chaveado pelos DOIS componentes, nao pela unidade: 32 passageiros do
    // mesmo perfil na mesma massa de terra sao uma pergunta so.
    private readonly Dictionary<long, bool> transporterMeetCache =
        new Dictionary<long, bool>();

    /// <summary>
    /// Existe encontro possivel entre estes dois? Pergunta TOPOLOGICA: algum
    /// dia, a qualquer distancia. Quao longe fica a praia nao entra aqui.
    ///
    ///   - infantaria no continente + navio: o componente dela chega a praia,
    ///     que e vizinha de agua. Encontro existe — a 2 ou a 30 hexes.
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

        // Caminho curto: o transportador ja encosta no passageiro onde ele
        // esta. Resolve a maioria esmagadora dos pares sem tocar no memo.
        if (TouchesComponent(transporterComponent.Cells, passengerCell))
            return true;

        // O passageiro ganha componente proprio — pelo MESMO cache por perfil
        // do transportador. Sem isso a pergunta ficava presa a "quanto ele
        // anda em 2 turnos", que e prazo, nao topologia.
        MobilityComponent passengerComponent =
            GetOrBuildMobilityComponent(passenger);
        if (passengerComponent?.Cells == null
            || passengerComponent.Cells.Count == 0)
        {
            return false;
        }

        long pairKey = ((long)transporterComponent.Id << 32)
                       ^ (uint)passengerComponent.Id;
        if (transporterMeetCache.TryGetValue(pairKey, out bool memo))
            return memo;

        bool result = ComponentsTouch(
            transporterComponent.Cells, passengerComponent.Cells);
        transporterMeetCache[pairKey] = result;
        return result;
    }

    /// <summary>
    /// Os dois componentes tem alguma celula em comum, ou adjacente?
    ///
    /// Varre o MENOR dos dois: a resposta e simetrica, e a praia costuma ser
    /// a borda do conjunto pequeno. Roda uma vez por PAR DE COMPONENTES,
    /// nunca por par de unidades — e essa e a diferenca dos 43 segundos.
    /// </summary>
    private bool ComponentsTouch(
        Dictionary<Vector3Int, int> a,
        Dictionary<Vector3Int, int> b)
    {
        Dictionary<Vector3Int, int> smaller = a.Count <= b.Count ? a : b;
        Dictionary<Vector3Int, int> larger = smaller == a ? b : a;

        AIDecisionPerf.AddCount("MobilityComponentTouchTests");
        foreach (Vector3Int cell in smaller.Keys)
        {
            if (TouchesComponent(larger, cell))
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
