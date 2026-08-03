using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Natureza da referencia magnetica encontrada.</summary>
public enum MelhorCapitaoKind
{
    /// <summary>Uma unidade aliada. O caso comum.</summary>
    Unit,

    /// <summary>Uma construcao.</summary>
    Construction,

    /// <summary>
    /// Uma celula solta — a RepCell do setor. O "capitao abstrato" de quem tem
    /// plano e ainda nao achou lideranca real no setor.
    /// </summary>
    Cell
}

/// <summary>
/// Uma faixa da lista de atracao do chamador. A ORDEM e a prioridade: indice 0
/// ganha de indice 1 mesmo estando mais longe.
///
/// O servico nao conhece papel nenhum. "Antiaereo prefere Vigilancia Aerea e
/// cai para o Capitao" nao mora aqui — mora na lista que o antiaereo monta.
/// </summary>
public sealed class MelhorCapitaoAttraction
{
    /// <summary>Rotulo de diagnostico. Aparece no log e na ferramenta.</summary>
    public string label;

    /// <summary>Quais unidades aliadas servem. Nulo = nao considera unidades.</summary>
    public Func<UnitManager, bool> matchUnit;

    /// <summary>Quais construcoes servem. Nulo = nao considera construcoes.</summary>
    public Func<ConstructionManager, bool> matchConstruction;

    /// <summary>
    /// Celula fixa, avaliada quando informada. E por aqui que a RepCell entra:
    /// ela nao e unidade nem precisa de construcao, e so uma coordenada que
    /// serve de referencia ate aparecer lideranca de verdade.
    ///
    /// Serve igual para a fronteira da nevoa: campo reduzido a ponto por outro
    /// servico entra aqui sem o MelhorCapitao saber o que e nevoa.
    /// </summary>
    public bool hasFixedCell;
    public Vector3Int fixedCell;

    /// <summary>
    /// Aceita capitao EMBARCADO nesta faixa. Desligado por padrao.
    ///
    /// Embarcado nao se segue andando — se segue pedindo carona. Quem liga isto
    /// esta dizendo "eu sei pedir carona atras dele"; hoje, pela doutrina, e o
    /// caso da unidade COM PLANO, que nao troca de capitao so porque o dela
    /// entrou num veiculo. Sem plano a regra continua sendo pegar outro
    /// capitao, e a faixa fica desligada.
    ///
    /// O servico nao pede carona nem escolhe para onde: devolve o capitao
    /// marcado como embarcado e quem o carrega, em `carrier`. Perseguir o hex
    /// atual do transportador ou mirar o destino dele e decisao do papel — o
    /// primeiro vira corrida atras de alvo movel, o segundo exige saber para
    /// onde o transporte vai, e nenhuma das duas e pergunta deste servico.
    /// </summary>
    public bool allowEmbarked;
}

public sealed class MelhorCapitaoOption
{
    public MelhorCapitaoKind kind;
    public UnitManager unit;
    public ConstructionManager construction;
    public Vector3Int cell;

    /// <summary>Posicao na lista de atracao. Menor ganha.</summary>
    public int attractionIndex;
    public string attractionLabel;

    /// <summary>
    /// O capitao esta embarcado. Nao se chega nele andando: quem organiza tem
    /// que pedir carona. Ver `carrier`.
    /// </summary>
    public bool isEmbarked;

    /// <summary>
    /// Quem carrega o capitao embarcado. Nulo no caso normal. E o dado que o
    /// papel precisa para montar o pedido de carona — o servico nao o monta.
    /// </summary>
    public UnitManager carrier;

    public int cubicDistance;

    /// <summary>
    /// Distancia de rota. So e calculada quando a geometria pedida e rota E o
    /// candidato ainda tem chance de ganhar — ver o corte por limite inferior
    /// no Evaluate. `hasRoute` falso significa "nao ha rota" OU "nao precisou
    /// calcular"; `routeSkipped` separa os dois.
    /// </summary>
    public bool hasRoute;
    public float routeDistance = -1f;
    public bool routeSkipped;

    /// <summary>A distancia que entrou na conta.</summary>
    public float effectiveDistance;

    public float score;
    public int displayScore;
    public string reason;
}

public sealed class MelhorCapitaoRequest
{
    public UnitManager unit;

    /// <summary>Avaliar como se a unidade estivesse aqui. Nulo = posicao atual.</summary>
    public Vector3Int? originOverride;

    /// <summary>
    /// Candidatas. O chamador entrega o conjunto; o servico nao sabe de que
    /// time sao nem como foram achadas. Nulo = nao considera.
    /// </summary>
    public IReadOnlyList<UnitManager> allies;
    public IReadOnlyList<ConstructionManager> constructions;

    /// <summary>A lista ordenada. Sem ela nao ha pergunta.</summary>
    public IReadOnlyList<MelhorCapitaoAttraction> attractions;

    /// <summary>
    /// Rota (padrao) ou cubica. Rota e a resposta certa para quem anda: um
    /// capturador a quatro hexes atras de uma serra esta mais longe que um a
    /// cinco de estrada.
    ///
    /// Nao e preciso pedir cubica para aeronave — o AIRouteDistance ja devolve
    /// distancia de hex para `Domain.Air`, porque a geometria e da unidade.
    /// Pedir cubica aqui e para quando o CHAMADOR quer regua, nao para corrigir
    /// o dominio.
    /// </summary>
    public bool useCubicGeometry;

    public Action<string> diagnosticLog;
}

public sealed class MelhorCapitaoResult
{
    public readonly List<MelhorCapitaoOption> ranking =
        new List<MelhorCapitaoOption>();

    public Vector3Int origin;
    public int candidatesVisited;
    public int routeQueries;
    public int routeSkippedByBound;

    /// <summary>
    /// Quantos capitaes embarcados entraram no ranking. Se for maior que zero,
    /// quem organiza tem que resolver por carona, nao por marcha.
    /// </summary>
    public int embarkedCaptains;

    public MelhorCapitaoOption best =>
        ranking.Count > 0 ? ranking[0] : null;

    public int CountInAttraction(int attractionIndex)
    {
        int total = 0;
        for (int i = 0; i < ranking.Count; i++)
        {
            if (ranking[i].attractionIndex == attractionIndex)
                total++;
        }
        return total;
    }
}

/// <summary>
/// Quem esta unidade acompanha? Devolve UMA referencia — "e aquele cara ou
/// aquele predio" — e para por ai.
///
/// ONDE SE POSICIONAR EM RELACAO A ELA NAO E DAQUI. Vanguarda, retaguarda,
/// flanco, hex exato: tudo isso e do papel, com a Hotzone. O contrato esta
/// escrito na governanca:
///
///   "O Magnetismo nao escolhe obrigatoriamente um hexagono exato. Ele define
///    quem ou o que a unidade acompanha (...). A posicao final e escolhida pelo
///    servico responsavel."
///
/// O SERVICO NAO CONHECE PAPEL. "Antiaereo prefere Vigilancia Aerea e cai para
/// o Capitao" e uma LISTA que o antiaereo monta e passa. Trocar o papel e
/// trocar a lista, nao editar este arquivo — que e o ponto inteiro: hoje a
/// mesma pergunta esta respondida em quatro resolvedores, dois compartilhados e
/// dois privados, com predicados incompativeis entre si.
///
/// NAO CORTA POR BANDA, de proposito. Capturar exige chegar; acompanhar nao. Um
/// capitao a dez hexes continua sendo a direcao certa, e cortar por Tactical/
/// Operational mataria o magnetismo de longo alcance — que e justamente o que
/// mantem a formacao junta enquanto ela atravessa o mapa.
/// </summary>
public static class MelhorCapitaoService
{
    // A atracao domina por ordem de grandeza; a distancia desempata dentro
    // dela. Mesmo formato do MelhorDesembarque e do MelhorCaptura.
    private const float AttractionTerm = 100000f;
    private const float DistanceTerm = 100f;

    public static MelhorCapitaoResult Evaluate(MelhorCapitaoRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.unit, "melhorCapitao");
        AIDecisionPerf.AddCount("MelhorCapitaoCalls");

        var result = new MelhorCapitaoResult();
        if (request?.unit == null
            || request.attractions == null
            || request.attractions.Count == 0)
        {
            request?.diagnosticLog?.Invoke(
                "unidade ausente ou lista de atracao vazia");
            return result;
        }

        UnitManager unit = request.unit;
        Vector3Int origin = request.originOverride ?? unit.CurrentCellPosition;
        origin.z = 0;
        result.origin = origin;

        // 1) Coleta com a conta BARATA. A cubica sai de aritmetica; a rota e um
        //    pathfind que ja custou 71 s numa sessao naval desta base.
        var options = new List<MelhorCapitaoOption>();
        for (int a = 0; a < request.attractions.Count; a++)
        {
            MelhorCapitaoAttraction attraction = request.attractions[a];
            if (attraction == null)
                continue;

            CollectUnits(request, attraction, a, origin, options, result);
            CollectConstructions(request, attraction, a, origin, options, result);
            CollectFixedCell(attraction, a, origin, options, result);
        }

        // 2) Resolve a distancia efetiva.
        if (request.useCubicGeometry)
        {
            for (int i = 0; i < options.Count; i++)
            {
                options[i].effectiveDistance = options[i].cubicDistance;
                options[i].routeSkipped = true;
            }
        }
        else
        {
            ResolveRouteDistances(unit, origin, options, result);
        }

        // 3) Pontua e ordena.
        int lastIndex = request.attractions.Count - 1;
        for (int i = 0; i < options.Count; i++)
        {
            MelhorCapitaoOption option = options[i];
            int weight = lastIndex - option.attractionIndex + 1;
            option.score = weight * AttractionTerm
                           - option.effectiveDistance * DistanceTerm;
            option.displayScore = Mathf.RoundToInt(option.score / DistanceTerm);
            option.reason =
                $"atracao=#{option.attractionIndex} {option.attractionLabel} " +
                $"(termo={weight * (int)AttractionTerm}) | " +
                $"distancia={option.effectiveDistance:0.#} " +
                $"(termo={-(int)(option.effectiveDistance * DistanceTerm)}) | " +
                $"cúbica={option.cubicDistance} | " +
                $"geometria={(request.useCubicGeometry ? "cúbica" : option.hasRoute ? "rota" : option.routeSkipped ? "rota não calculada" : "sem rota")} | " +
                $"tipo={option.kind}" +
                (option.isEmbarked
                    ? $" | EMBARCADO em {(option.carrier != null ? option.carrier.UnitDisplayName : "?")} — segue-se de carona, não a pé"
                    : string.Empty);
            result.ranking.Add(option);
        }

        result.ranking.Sort(CompareOption);
        AIDecisionPerf.AddCount(
            "MelhorCapitaoCandidates", result.candidatesVisited);
        AIDecisionPerf.AddCount(
            "MelhorCapitaoRouteQueries", result.routeQueries);
        AIDecisionPerf.AddCount(
            "MelhorCapitaoRouteSkips", result.routeSkippedByBound);
        request.diagnosticLog?.Invoke(
            $"candidatas={result.candidatesVisited}; " +
            $"opções={result.ranking.Count}; " +
            $"rotas calculadas={result.routeQueries}; " +
            $"rotas poupadas pelo limite={result.routeSkippedByBound}");
        return result;
    }

    /// <summary>
    /// A CUBICA E LIMITE INFERIOR DA ROTA — nenhum caminho e mais curto que a
    /// linha reta. Entao: ordena por cubica, calcula rota em ordem, e para
    /// quando a cubica do proximo ja empata ou passa a melhor rota encontrada.
    /// Ninguem atras dele tem como ganhar.
    ///
    /// E corte EXATO, nao heuristica: o vencedor e o mesmo que sairia
    /// calculando as N rotas. So que sao 2 ou 3 pathfinds em vez de N, e cada
    /// um custa 12-16ms em naval.
    ///
    /// A comparacao respeita a atracao: um candidato de faixa pior nunca poda
    /// um de faixa melhor, porque a ordem da lista manda antes da distancia.
    /// </summary>
    private static void ResolveRouteDistances(
        UnitManager unit,
        Vector3Int origin,
        List<MelhorCapitaoOption> options,
        MelhorCapitaoResult result)
    {
        options.Sort((left, right) =>
        {
            int byAttraction =
                left.attractionIndex.CompareTo(right.attractionIndex);
            return byAttraction != 0
                ? byAttraction
                : left.cubicDistance.CompareTo(right.cubicDistance);
        });

        int currentAttraction = -1;
        float bestRouteInAttraction = float.MaxValue;
        for (int i = 0; i < options.Count; i++)
        {
            MelhorCapitaoOption option = options[i];
            if (option.attractionIndex != currentAttraction)
            {
                currentAttraction = option.attractionIndex;
                bestRouteInAttraction = float.MaxValue;
            }

            if (option.cubicDistance >= bestRouteInAttraction)
            {
                // Nao pode ganhar de quem ja temos. Nao paga o pathfind.
                option.routeSkipped = true;
                option.effectiveDistance = option.cubicDistance;
                result.routeSkippedByBound++;
                continue;
            }

            result.routeQueries++;
            if (AIRouteDistance.TryGet(
                    unit, origin, option.cell, out float routeDistance))
            {
                option.hasRoute = true;
                option.routeDistance = routeDistance;
                option.effectiveDistance = routeDistance;
                if (routeDistance < bestRouteInAttraction)
                    bestRouteInAttraction = routeDistance;
            }
            else
            {
                // Sem rota propria ate ele. Nao e candidato a ser seguido a pe,
                // mas continua na lista com a cubica — quem organiza decide se
                // aceita uma referencia que so se alcanca de carona.
                option.hasRoute = false;
                option.effectiveDistance = option.cubicDistance;
            }
        }
    }

    private static void CollectUnits(
        MelhorCapitaoRequest request,
        MelhorCapitaoAttraction attraction,
        int attractionIndex,
        Vector3Int origin,
        List<MelhorCapitaoOption> options,
        MelhorCapitaoResult result)
    {
        if (attraction.matchUnit == null || request.allies == null)
            return;

        for (int i = 0; i < request.allies.Count; i++)
        {
            UnitManager candidate = request.allies[i];
            if (!IsFollowableUnit(
                    candidate, request.unit, attraction.allowEmbarked))
                continue;
            result.candidatesVisited++;
            if (!attraction.matchUnit(candidate))
                continue;

            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            bool embarked = candidate.IsEmbarked;
            if (embarked)
                result.embarkedCaptains++;
            options.Add(new MelhorCapitaoOption
            {
                kind = MelhorCapitaoKind.Unit,
                unit = candidate,
                cell = cell,
                isEmbarked = embarked,
                carrier = embarked ? candidate.EmbarkedTransporter : null,
                attractionIndex = attractionIndex,
                attractionLabel = attraction.label,
                cubicDistance =
                    AIActionReachCoordinator.CubicDistance(origin, cell)
            });
        }
    }

    /// <summary>
    /// As guardas que os quatro resolvedores antigos repetiam identicas.
    ///
    /// Morto, em reparo e inativo nunca lideram — nao vao a lugar nenhum.
    /// EMBARCADO e outra coisa: ele vai, so nao a pe. Por isso deixou de ser
    /// descarte fixo e virou opcao da faixa (`allowEmbarked`), que a unidade com
    /// plano liga para nao trocar de capitao so porque o dela entrou num
    /// veiculo — ela pede carona atras dele.
    /// </summary>
    private static bool IsFollowableUnit(
        UnitManager candidate,
        UnitManager follower,
        bool allowEmbarked)
    {
        return candidate != null
            && candidate != follower
            && !candidate.IsDead
            && (allowEmbarked || !candidate.IsEmbarked)
            && !candidate.IsUnderRepair
            && candidate.gameObject.activeInHierarchy;
    }

    private static void CollectConstructions(
        MelhorCapitaoRequest request,
        MelhorCapitaoAttraction attraction,
        int attractionIndex,
        Vector3Int origin,
        List<MelhorCapitaoOption> options,
        MelhorCapitaoResult result)
    {
        if (attraction.matchConstruction == null
            || request.constructions == null)
            return;

        for (int i = 0; i < request.constructions.Count; i++)
        {
            ConstructionManager candidate = request.constructions[i];
            if (candidate == null)
                continue;
            result.candidatesVisited++;
            if (!attraction.matchConstruction(candidate))
                continue;

            Vector3Int cell = candidate.CurrentCellPosition;
            cell.z = 0;
            options.Add(new MelhorCapitaoOption
            {
                kind = MelhorCapitaoKind.Construction,
                construction = candidate,
                cell = cell,
                attractionIndex = attractionIndex,
                attractionLabel = attraction.label,
                cubicDistance =
                    AIActionReachCoordinator.CubicDistance(origin, cell)
            });
        }
    }

    private static void CollectFixedCell(
        MelhorCapitaoAttraction attraction,
        int attractionIndex,
        Vector3Int origin,
        List<MelhorCapitaoOption> options,
        MelhorCapitaoResult result)
    {
        if (!attraction.hasFixedCell)
            return;

        Vector3Int cell = attraction.fixedCell;
        cell.z = 0;
        result.candidatesVisited++;
        options.Add(new MelhorCapitaoOption
        {
            kind = MelhorCapitaoKind.Cell,
            cell = cell,
            attractionIndex = attractionIndex,
            attractionLabel = attraction.label,
            cubicDistance =
                AIActionReachCoordinator.CubicDistance(origin, cell)
        });
    }

    // Score manda; atracao, distancia e InstanceId desempatam para a lista nao
    // dancar entre duas chamadas identicas.
    private static int CompareOption(
        MelhorCapitaoOption a,
        MelhorCapitaoOption b)
    {
        int byScore = b.score.CompareTo(a.score);
        if (byScore != 0)
            return byScore;

        int byAttraction = a.attractionIndex.CompareTo(b.attractionIndex);
        if (byAttraction != 0)
            return byAttraction;

        int byDistance =
            a.effectiveDistance.CompareTo(b.effectiveDistance);
        if (byDistance != 0)
            return byDistance;

        return ResolveStableId(a).CompareTo(ResolveStableId(b));
    }

    private static int ResolveStableId(MelhorCapitaoOption option)
    {
        if (option.unit != null)
            return option.unit.InstanceId;
        if (option.construction != null)
            return option.construction.InstanceId;
        return option.cell.GetHashCode();
    }
}
