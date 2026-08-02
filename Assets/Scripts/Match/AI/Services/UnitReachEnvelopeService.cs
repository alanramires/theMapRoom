using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Refinamento da banda Tactical para a intencao de combate.
/// Potential usa o movimento maximo (o que a unidade poderia fazer num turno
/// inteiro); CurrentTurn usa o movimento que ainda resta agora.
/// </summary>
public enum UnitThreatEnvelopeMovement
{
    Potential = 0,
    CurrentTurn = 1
}

/// <summary>
/// Primeiro eixo do envelope: O QUE a unidade quer materializar no destino.
/// Cada intencao delega a legalidade e o custo de entrada ao seu proprio
/// sensor Pode*; nenhuma delas reimplementa a regra.
/// </summary>
public enum ReachIntent
{
    /// <summary>Expansao: alcance de arma a partir de cada celula de parada. O tiro nao custa MP.</summary>
    Combat = 0,

    /// <summary>Expansao: alcance de servico do supridor (serviceRange).</summary>
    Service = 1,

    /// <summary>Expansao: alcance de coleta do Hub (collectionRange).</summary>
    Transfer = 2,

    /// <summary>Expansao com custo: entrar no hex do receptor consome MP.</summary>
    Fusion = 3,

    /// <summary>Expansao com custo: entrar no hex do transportador consome MP.</summary>
    Embark = 4,

    /// <summary>
    /// So alcance. A captura acontece no hex onde a unidade PARA, entao o
    /// envelope nao expande — e nao consulta PodeCapturar. Cruzar alcance com
    /// objetivo e da IA (CaptureOpportunityClaimService).
    /// </summary>
    Capture = 5,

    /// <summary>Identidade: onde a unidade poderia estar, sem acao nenhuma.</summary>
    Mobility = 6
}

/// <summary>
/// Subetapa do envelope. E PARAMETRO DE ENTRADA, como a intencao: o servico
/// nao a deduz. Ela decide apenas a GEOMETRIA e se a unidade se desloca.
///
/// O alcance de arma (vermelho) e decidido pela INTENCAO Combate, nao pela
/// subetapa. "Hibrido" nao existe aqui: tentar Artilheiro e cair para
/// Terrestre/Aereo e comportamento da IA, nao etapa do servico.
///
/// Ver docs/contrato_envelope_alcance.md.
/// </summary>
public enum ReachSubStep
{
    /// <summary>Caminhos validos. Padrao de qualquer unidade de superficie.</summary>
    Terrestre = 0,

    /// <summary>Distancia cubica.</summary>
    Aereo = 1,

    /// <summary>Nao move (MP=0); mira em cubica. So sob a intencao Combate.</summary>
    Artilheiro = 2
}

/// <summary>
/// Relacao de uma celula com o grafo de movimento proprio da unidade.
///
/// Responde a pergunta geral do refactor — "a unidade possui uma rota propria
/// completa ate a missao?" — sem modelar excecao chamada "ilha".
/// </summary>
public enum MobilityRelation
{
    /// <summary>A unidade chega andando, dado tempo suficiente.</summary>
    OwnComponent = 0,

    /// <summary>A unidade PARARIA ali se fosse entregue, mas nao chega sozinha. Pedido de carona.</summary>
    OtherComponent = 1,

    /// <summary>A unidade nunca ocupa esta celula. Nao e destino de nada.</summary>
    NotOccupiable = 2
}

/// <summary>
/// Segundo eixo do envelope: QUAO LONGE no tempo.
///
/// Tactical    - rodada atual, com o movimento disponivel agora.
/// Operational - rota propria em MP x N turnos; custo real por celula.
///
/// Nao existe banda estrategica aqui. O que fazer com o que esta fora destas
/// duas — direcao, alvo distante, pedido de carona — e decisao da IA, nao
/// retorno do envelope.
/// </summary>
public enum ReachBand
{
    Tactical = 0,
    Operational = 1
}

/// <summary>
/// De onde a unidade materializa uma acao, e com quanto sobrando.
///
/// E a informacao que separa as duas contas de MP do jogo:
///   combate  - o tiro nao custa MP. Anda 3, atira no 4: EnterCost = 0 e o
///              RemainingMovement pode ser 0.
///   fusao /  - entrar no hex custa. Anda 2 na montanha (2 MP), sobra 1, e so
///   embarque   funde/embarca no 3 se aquele hex custar 1.
/// </summary>
public readonly struct ReachOrigin
{
    /// <summary>Celula onde a unidade precisa parar para materializar a acao.</summary>
    public readonly Vector3Int FromCell;

    /// <summary>MP restante ao chegar em <see cref="FromCell"/>.</summary>
    public readonly int RemainingMovement;

    /// <summary>MP consumido para entrar na celula de acao. Zero quando a acao nao move.</summary>
    public readonly int EnterCost;

    public ReachOrigin(Vector3Int fromCell, int remainingMovement, int enterCost)
    {
        fromCell.z = 0;
        FromCell = fromCell;
        RemainingMovement = remainingMovement;
        EnterCost = enterCost;
    }
}

/// <summary>
/// Envelope de alcance materializavel de uma unidade para uma intencao numa
/// banda. Substitui o antigo envelope "de ameaca": ameaca passou a ser apenas
/// uma das intencoes.
/// </summary>
public class UnitReachEnvelope
{
    public readonly ReachIntent Intent;
    public readonly ReachBand Band;

    /// <summary>Subetapa efetivamente usada para construir este envelope.</summary>
    public ReachSubStep SubStep { get; internal set; }

    /// <summary>Caminhos legais por destino. Vazio quando a origem veio de um mapa de custo.</summary>
    public readonly Dictionary<Vector3Int, List<Vector3Int>> PathsByDestination;

    /// <summary>Custo real de movimento por celula alcancavel, quando conhecido.</summary>
    public readonly Dictionary<Vector3Int, int> CostByCell;

    /// <summary>Celulas onde a unidade pode parar.</summary>
    public readonly HashSet<Vector3Int> MovementCells;

    /// <summary>Celulas onde a intencao se materializa (inclui o proprio movimento quando aplicavel).</summary>
    public readonly HashSet<Vector3Int> AttackableCells;

    /// <summary>Mesma colecao de <see cref="MovementCells"/> em forma de lista, para pintura.</summary>
    public readonly List<Vector3Int> RangeCells;

    /// <summary>Anel externo: acao menos movimento. E o que as ferramentas pintam como alcance.</summary>
    public readonly List<Vector3Int> LineCells;

    /// <summary>
    /// De onde cada celula de acao e materializavel. Evita que o consumidor
    /// reconstrua na mao a origem, o custo do caminho e o MP restante — coisas
    /// que o servico ja calculou para decidir que a celula entra no envelope.
    /// </summary>
    public readonly Dictionary<Vector3Int, ReachOrigin> OriginByActionCell;

    /// <summary>
    /// Em QUANTOS TURNOS cada celula de movimento e alcancada. 1 = nesta
    /// rodada. Como o envelope Operational CONTEM o Tactical, sem isto o
    /// consumidor nao sabe se a resposta e para agora ou para o turno seguinte
    /// — e o diagnostico acaba rotulando de Operational uma acao que se
    /// completa hoje.
    /// </summary>
    public readonly Dictionary<Vector3Int, int> TurnsByCell;

    internal UnitReachEnvelope(
        ReachIntent intent,
        ReachBand band,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        HashSet<Vector3Int> movementCells,
        HashSet<Vector3Int> actionCells,
        HashSet<Vector3Int> lineCells,
        Dictionary<Vector3Int, ReachOrigin> originByActionCell = null,
        Dictionary<Vector3Int, int> turnsByCell = null)
    {
        TurnsByCell = turnsByCell ?? new Dictionary<Vector3Int, int>();
        Intent = intent;
        Band = band;
        PathsByDestination = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>();
        CostByCell = costByCell ?? new Dictionary<Vector3Int, int>();
        MovementCells = movementCells ?? new HashSet<Vector3Int>();
        AttackableCells = actionCells ?? new HashSet<Vector3Int>();
        RangeCells = new List<Vector3Int>(MovementCells);
        LineCells = lineCells != null ? new List<Vector3Int>(lineCells) : new List<Vector3Int>();
        OriginByActionCell = originByActionCell
                             ?? new Dictionary<Vector3Int, ReachOrigin>();
    }

    /// <summary>De onde materializar a acao nesta celula.</summary>
    public bool TryGetOrigin(Vector3Int actionCell, out ReachOrigin origin)
    {
        actionCell.z = 0;
        return OriginByActionCell.TryGetValue(actionCell, out origin);
    }

    /// <summary>Em quantos turnos a unidade chega nesta celula. 1 = nesta rodada.</summary>
    public bool TryGetTurns(Vector3Int cell, out int turns)
    {
        cell.z = 0;
        return TurnsByCell.TryGetValue(cell, out turns);
    }

    /// <summary>
    /// Banda REAL desta celula de acao, independente da banda pedida.
    /// Um envelope Operational responde Tactical para o que se completa nesta
    /// rodada — que e a maior parte dele.
    /// </summary>
    public ReachBand ResolveActionBand(Vector3Int actionCell)
    {
        actionCell.z = 0;
        if (!OriginByActionCell.TryGetValue(actionCell, out ReachOrigin origin))
            return Band;
        return TryGetTurns(origin.FromCell, out int turns) && turns <= 1
            ? ReachBand.Tactical
            : ReachBand.Operational;
    }

    /// <summary>
    /// Funil da construcao, em texto, para ferramentas e logs. Diz em qual
    /// etapa o envelope ficou vazio: orcamento, movimento ou custo de entrada.
    /// Nunca participa de decisao.
    /// </summary>
    public string Diagnostic { get; internal set; } = string.Empty;

    /// <summary>Nome preferido de <see cref="AttackableCells"/> fora do contexto de combate.</summary>
    public HashSet<Vector3Int> ActionCells => AttackableCells;

    /// <summary>Nome preferido de <see cref="LineCells"/>: o anel de acao fora do movimento.</summary>
    public List<Vector3Int> OuterCells => LineCells;

    /// <summary>A intencao se materializa nesta celula?</summary>
    public bool CanAct(Vector3Int cell)
    {
        cell.z = 0;
        return AttackableCells.Contains(cell);
    }

    /// <summary>Alias historico de <see cref="CanAct"/> para consumidores de combate.</summary>
    public bool CanThreaten(Vector3Int cell) => CanAct(cell);

    /// <summary>A unidade consegue parar nesta celula?</summary>
    public bool CanReach(Vector3Int cell)
    {
        cell.z = 0;
        return MovementCells.Contains(cell);
    }

    public bool TryGetCost(Vector3Int cell, out int cost)
    {
        cell.z = 0;
        return CostByCell.TryGetValue(cell, out cost);
    }
}

/// <summary>
/// Compatibilidade temporaria durante a migracao para <see cref="UnitReachEnvelope"/>.
/// Nao acrescenta comportamento: existe apenas para que os consumidores tipados
/// como envelope de ameaca continuem compilando enquanto migram.
/// </summary>
public sealed class UnitThreatEnvelope : UnitReachEnvelope
{
    internal UnitThreatEnvelope(
        ReachIntent intent,
        ReachBand band,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        HashSet<Vector3Int> movementCells,
        HashSet<Vector3Int> actionCells,
        HashSet<Vector3Int> lineCells,
        Dictionary<Vector3Int, ReachOrigin> originByActionCell = null,
        Dictionary<Vector3Int, int> turnsByCell = null)
        : base(
            intent,
            band,
            paths,
            costByCell,
            movementCells,
            actionCells,
            lineCells,
            originByActionCell,
            turnsByCell)
    {
    }
}

/// <summary>
/// Composicao das duas bandas materializaveis de uma mesma intencao.
/// E o objeto que as ferramentas consomem: era exatamente esta composicao que
/// vivia solta dentro da janela de Editor.
/// </summary>
public sealed class UnitReachProfile
{
    public readonly ReachIntent Intent;
    public readonly UnitReachEnvelope Tactical;
    public readonly UnitReachEnvelope Operational;

    internal UnitReachProfile(
        ReachIntent intent,
        UnitReachEnvelope tactical,
        UnitReachEnvelope operational)
    {
        Intent = intent;
        Tactical = tactical;
        Operational = operational;
    }

    /// <summary>
    /// Em qual banda esta celula e materializavel. Devolve false quando esta
    /// fora das duas — o que fazer com isso e decisao da IA.
    /// </summary>
    public bool TryClassify(Vector3Int cell, out ReachBand band)
    {
        cell.z = 0;
        if (Tactical != null && Tactical.CanAct(cell))
        {
            band = ReachBand.Tactical;
            return true;
        }
        if (Operational != null && Operational.CanAct(cell))
        {
            band = ReachBand.Operational;
            return true;
        }
        band = ReachBand.Tactical;
        return false;
    }
}

/// <summary>
/// Descreve um pedido de envelope. Existe como classe para que o consumidor
/// preencha apenas o que a sua intencao exige.
/// </summary>
public sealed class UnitReachRequest
{
    public UnitManager Unit;
    public Tilemap BoardMap;
    public TerrainDatabase TerrainDatabase;
    public ReachIntent Intent = ReachIntent.Combat;
    public ReachBand Band = ReachBand.Tactical;

    /// <summary>Orcamento explicito. Quando 0 ou negativo, resolve pela banda.</summary>
    public int MovementBudget;
    public int OperationalTurns = 2;

    /// <summary>Reuso obrigatorio quando o chamador ja tem a onda de movimento.</summary>
    public Dictionary<Vector3Int, List<Vector3Int>> PrebuiltPaths;

    /// <summary>Reuso obrigatorio quando o chamador ja tem a malha de custo.</summary>
    public Dictionary<Vector3Int, int> PrebuiltCosts;

    /// <summary>
    /// Restringe a acao as celulas cuja camada primaria a unidade opera.
    /// Ferramentas de apresentacao usam true; a IA usa false porque ja valida
    /// cada candidato pelo sensor. Ver nota de paridade no servico.
    /// </summary>
    public bool FilterByOperationDomain;

    /// <summary>Sobrescreve o alcance de servico. Sem valor, resolve pela intencao.</summary>
    public SupplierRangeMode? RangeOverride;

    /// <summary>
    /// Preenche CostByCell com o custo real de cada celula de movimento.
    /// Custa uma varredura de custo por caminho, entao fica desligado por
    /// padrao: ferramentas ligam, a IA so liga onde precisa do numero.
    /// </summary>
    public bool IncludeMovementCosts;

    // Combate.
    public UnitThreatEnvelopeMovement MovementMode = UnitThreatEnvelopeMovement.CurrentTurn;
    public DPQAirHeightConfig DpqAirHeightConfig;
    public bool EnableLdt = true;
    public bool EnableLos = true;
    public bool EnableSpotter = true;

    // Embarque.
    public UnitManager EmbarkPassenger;

    /// <summary>
    /// Subetapa pedida. É PARÂMETRO DE ENTRADA: o serviço não a deduz.
    /// Quem classifica a unidade e escolhe a subetapa é o chamador — a IA.
    /// </summary>
    public ReachSubStep SubStep = ReachSubStep.Terrestre;

    /// <summary>
    /// Calcula a partir de OUTRA célula que não a atual da unidade: "e se ela
    /// estivesse ali?". Nada é movido — só muda o ponto de partida.
    ///
    /// A origem era o único parâmetro implícito que sobrava neste pedido, e o
    /// projeto já aprendeu que banda, âncora e camada têm de ser parâmetro.
    /// Ela sustenta pelo menos três coisas da doutrina:
    ///
    ///   desembarque — teleporta o passageiro para cima do objetivo e a banda
    ///                 dele vira a zona de largada (projeção invertida);
    ///   fogo indireto — o teste da "unidade fantasma": há vanguarda naquele
    ///                 destino antes de eu aceitar carona?
    ///   retaguarda  — a faixa relativa ao capitão, com origem nele.
    /// </summary>
    public Vector3Int? OriginOverride;
}

/// <summary>
/// Fonte unica do envelope de alcance materializavel, consumida por Editor,
/// panel helper e IA.
///
/// DOIS EIXOS
///   intencao (<see cref="ReachIntent"/>) x banda (<see cref="ReachBand"/>)
///
/// A intencao decide o custo de entrada e a legalidade no destino, sempre
/// delegando ao sensor correspondente:
///   Combat   -> PodeMirarSensor
///   Service  -> PodeSuprirSensor  (serviceRange)
///   Transfer -> PodeSuprirSensor  (collectionRange)
///   Fusion   -> PodeFundirSensor.TryResolveMergeEnterCost
///   Embark   -> PodeEmbarcarSensor.TryGetEmbarkCostAtCell
///
/// A banda decide o orcamento e a geometria, reaproveitando o que ja existe:
/// UnitMovementPathRules para caminhos e custos, AIActionReachCoordinator para
/// o reach setorial e a metrica cubica das aeronaves. O servico NAO implementa
/// pathfinding proprio e aceita malhas ja calculadas pelo chamador.
///
/// So devolve resposta materializavel: Tactical, Operational, movimento, acao,
/// custo e origem da acao. Nao existe banda estrategica — objetivo fora dessas
/// duas bandas e pergunta da IA, nao retorno do envelope. Pelo mesmo motivo o
/// servico nao varre o tabuleiro atras de objetivo nem conclui sobre carona.
///
/// CONTRATO TRANSACIONAL: o servico e puro. Nao move unidades, nao altera
/// ocupacao, FOW, deteccao, recursos, revisoes nem estado de turno. Pode ser
/// chamado livremente em estados provisorios do cursor.
/// </summary>
public static class UnitReachEnvelopeService
{
    private enum CombatProfile
    {
        None,
        Movement,
        DistanceStatic,
        Hybrid
    }

    private sealed class CacheEntry
    {
        public int KeyHash;
        public UnitThreatEnvelope Envelope;
    }

    private static readonly Dictionary<long, CacheEntry> Cache =
        new Dictionary<long, CacheEntry>();

    public static void ClearCache()
    {
        Cache.Clear();
    }

    /// <summary>
    /// A subetapa pedida usa distancia cubica? Depende SO da subetapa: quem
    /// classificou a unidade foi o chamador ao escolhe-la.
    /// </summary>
    public static bool UsesCubicGeometry(ReachSubStep subStep)
    {
        return subStep == ReachSubStep.Aereo;
    }

    /// <summary>
    /// Subetapas validas de cada intencao — a arvore do contrato.
    /// Artilheiro so existe sob Combate: ele nao e geometria, e "nao move e a
    /// banda e da arma", e so o combate tem arma.
    ///
    /// GEOMETRIA NAO E RAMO DE INTENCAO. Captura media so em caminhos era regra
    /// gravada aqui dentro: o servico decidia por conta propria que quem captura
    /// anda. Como medir e pergunta do chamador, igual a intencao.
    /// </summary>
    public static IReadOnlyList<ReachSubStep> GetSubSteps(ReachIntent intent)
    {
        return intent == ReachIntent.Combat
            ? CombatSubSteps
            : GroundAndAirSubSteps;
    }

    /// <summary>
    /// A UNIDADE suporta esta subetapa?
    ///
    /// Sim, todas. A subetapa e PARAMETRO DE ENTRADA e diz como medir, nao o que
    /// a unidade e — perguntar a distancia cubica de uma unidade de superficie e
    /// pergunta legitima ("de quantos hexes em linha reta ela esta?"), nao pedido
    /// invalido. O que a unidade E continua decidindo sozinho pela geometria
    /// PADRAO, em UsesCubicSectorReach: aeronave mede em cubica mesmo quando o
    /// chamador nao pediu.
    ///
    /// Mantido como ponto de extensao — subetapa que exija capacidade da unidade
    /// entra aqui, e nao espalhada pelos chamadores.
    /// </summary>
    public static bool SupportsSubStep(UnitManager unit, ReachSubStep subStep)
    {
        return true;
    }

    /// <summary>
    /// A unidade tem arma que dispara a partir do alcance 1?
    ///
    /// E o que separa "move e atira" de "atira parada". Depois de mover, o tiro
    /// colapsa para alcance 1: numa arma de minimo 1 e maximo 2 ele ainda sai,
    /// numa de minimo 2 nao sai de jeito nenhum.
    /// </summary>
    private static bool HasCloseQuartersWeapon(UnitManager unit)
    {
        if (unit == null)
            return false;
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked?.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            if (embarked.GetRangeMin() <= 1)
                return true;
        }
        return false;
    }

    public static bool IsSubStepValid(
        ReachIntent intent, ReachSubStep subStep, UnitManager unit = null)
    {
        IReadOnlyList<ReachSubStep> valid = GetSubSteps(intent);
        bool inTree = false;
        for (int i = 0; i < valid.Count; i++)
        {
            if (valid[i] == subStep)
            {
                inTree = true;
                break;
            }
        }

        if (!inTree)
            return false;
        if (unit != null && !SupportsSubStep(unit, subStep))
            return false;

        // SOB COMBATE, A GEOMETRIA VOLTA A SER PROPRIEDADE DA UNIDADE.
        //
        // Nas demais intencoes a medicao e pergunta pura ("a quantos hexes em
        // linha reta isto esta?") e qualquer unidade responde. Combate e o caso
        // em que o verde afirma "paro AQUI e atiro DALI" — e ai a geometria da
        // banda e uma afirmacao sobre deslocamento, nao uma regua. Medir um obus
        // de superficie em cubica diria que ele estaciona atravessando serra e
        // oceano. Pedido INVALIDO, nao envelope vazio.
        if (intent == ReachIntent.Combat
            && subStep == ReachSubStep.Aereo
            && unit != null
            && !AIActionReachCoordinator.UsesCubicSectorReach(unit))
        {
            return false;
        }

        // COMBATE TERRESTRE EXIGE ARMA DE ALCANCE MINIMO 1.
        //
        // Terrestre e "move e atira": o tiro pos-movimento colapsa para 1. Uma
        // peca de minimo 2 nao dispara depois de andar. Devolver o verde e o
        // azul do MOVIMENTO seria pior que devolver nada: sao bandas de
        // deslocamento respondendo a uma pergunta de combate, e quem lesse
        // concluiria que a peca briga onde ela so consegue andar. Pedido
        // INVALIDO. A pergunta certa para ela e Artilheiro, cuja banda e da
        // arma.
        if (intent == ReachIntent.Combat
            && subStep == ReachSubStep.Terrestre
            && unit != null
            && !HasCloseQuartersWeapon(unit))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Subetapas da intencao que ESTA unidade suporta. E o que a ferramenta usa
    /// para nem oferecer a opcao — por isso valida pelo mesmo predicado do
    /// Build, e nao so pela geometria.
    /// </summary>
    public static List<ReachSubStep> GetSubSteps(
        ReachIntent intent, UnitManager unit)
    {
        IReadOnlyList<ReachSubStep> tree = GetSubSteps(intent);
        var result = new List<ReachSubStep>(tree.Count);
        for (int i = 0; i < tree.Count; i++)
        {
            if (IsSubStepValid(intent, tree[i], unit))
                result.Add(tree[i]);
        }
        return result;
    }

    private static readonly ReachSubStep[] CombatSubSteps =
    {
        ReachSubStep.Terrestre, ReachSubStep.Aereo, ReachSubStep.Artilheiro
    };

    private static readonly ReachSubStep[] GroundAndAirSubSteps =
    {
        ReachSubStep.Terrestre, ReachSubStep.Aereo
    };

    /// <summary>
    /// Constroi as duas bandas materializaveis de uma intencao numa passada.
    ///
    /// Orcamento e reuso (PrebuiltPaths/PrebuiltCosts) valem apenas para a
    /// banda declarada em <see cref="UnitReachRequest.Band"/>; a outra deriva o
    /// seu proprio do <see cref="AIActionReachCoordinator"/>. Um pedido com
    /// Band = Tactical e MovementBudget = MaxMovementPoints produz o par
    /// "turno cheio agora" + "MP x N turnos", que e o que as ferramentas pintam.
    /// </summary>
    public static UnitReachProfile BuildProfile(UnitReachRequest request)
    {
        if (request == null || request.Unit == null)
            return new UnitReachProfile(ReachIntent.Combat, null, null);

        UnitReachEnvelope tactical = Build(CloneForBand(request, ReachBand.Tactical));
        UnitReachEnvelope operational = Build(CloneForBand(request, ReachBand.Operational));
        return new UnitReachProfile(request.Intent, tactical, operational);
    }

    /// <summary>
    /// Constroi um envelope. Devolve null quando a unidade nao possui a
    /// capacidade exigida pela intencao (ex.: combate sem arma util).
    /// </summary>
    public static UnitReachEnvelope Build(UnitReachRequest request)
    {
        if (request == null || request.Unit == null)
            return null;

        UnitManager unit = request.Unit;
        ReachSubStep subStep = request.SubStep;
        Tilemap map = request.BoardMap != null ? request.BoardMap : unit.BoardTilemap;
        if (map == null)
            return null;

        // Pedido invalido, nao envelope vazio: `Aereo` exige isAircraft.
        if (!IsSubStepValid(request.Intent, subStep, unit))
            return null;

        // A expansao de tiro so e confirmada na rodada atual, onde o sensor
        // responde sobre o tabuleiro real. Na banda Operational o combate vale
        // como alcance/posicionamento, nao como promessa de tiro: devolve a
        // malha de progressao pura, sem projetar fogo a dois turnos.
        // ARTILHEIRO INVERTE A BANDA: ela e da ARMA, nao do movimento.
        //
        // Medir banda em movimento e absurdo para quem atira parado: a
        // Artilharia de Campanha move 1, entao o Operational dela seria o hex 2
        // — para uma peca cuja razao de existir e alcancar longe.
        //
        //   verde  = do hex 0 ate o alcance maximo
        //   azul   = o dobro do alcance maximo
        //
        // O azul aqui nao responde "para onde eu ando"; responde "de onde eu
        // posso ser alcancado, ou alcancar, se a situacao mudar". E por isso que
        // artilharia de alcance 4 nao embarca com inimigo no raio 8: embarcar e
        // ficar indefeso, e o blindado rapido cobre essa distancia.
        //
        // Ver docs/contrato_envelope_alcance.md e docs/AI Behavior/FireSupport.md.
        if (request.Intent == ReachIntent.Combat
            && subStep == ReachSubStep.Artilheiro)
        {
            return BuildArtilleryBand(request, map, subStep);
        }

        if (request.Intent == ReachIntent.Combat
            && request.Band == ReachBand.Tactical)
        {
            UnitReachEnvelope combat = BuildCombat(request, map, out _);
            if (combat != null)
                combat.SubStep = subStep;
            return combat;
        }

        lastEntryRejection = null;
        int budget = ResolveBudget(request);
        ResolveMovement(
            request,
            subStep,
            map,
            budget,
            out Dictionary<Vector3Int, List<Vector3Int>> paths,
            out Dictionary<Vector3Int, int> costByCell,
            out Dictionary<Vector3Int, int> turnsByCell);

        IEnumerable<Vector3Int> reachableKeys = paths.Count > 0
            ? (IEnumerable<Vector3Int>)paths.Keys
            : costByCell.Keys;
        var movementCells = new HashSet<Vector3Int>();
        foreach (Vector3Int rawCell in reachableKeys)
        {
            Vector3Int cell = rawCell;
            cell.z = 0;
            if (map.GetTile(cell) != null)
                movementCells.Add(cell);
        }

        // Sem encadeamento por turno (banda Tactical, ou malha vinda pronta do
        // chamador), tudo o que esta no conjunto e desta rodada.
        if (turnsByCell == null)
        {
            turnsByCell = new Dictionary<Vector3Int, int>(movementCells.Count);
            foreach (Vector3Int cell in movementCells)
                turnsByCell[cell] = 1;
        }

        // Custo real por celula de movimento. Sem isso a ferramenta nao
        // consegue mostrar "montanha custou 2, sobrou 1" — que e exatamente a
        // conta que explica onde a unidade para. So preenche quando o servico
        // e dono do dicionario; malha pronta do chamador nao se mexe.
        if (request.IncludeMovementCosts
            && request.PrebuiltCosts == null
            && paths.Count > 0)
        {
            foreach (Vector3Int cell in movementCells)
            {
                costByCell[cell] = ResolveSpentMovement(
                    request, map, cell, paths, costByCell);
            }
        }

        var origins = new Dictionary<Vector3Int, ReachOrigin>();
        HashSet<Vector3Int> actionCells;
        switch (request.Intent)
        {
            case ReachIntent.Service:
            case ReachIntent.Transfer:
                actionCells = ExpandByServiceRange(
                    request, map, movementCells, costByCell, budget, origins);
                break;
            case ReachIntent.Fusion:
                actionCells = ExpandByEntryCost(
                    request, map, movementCells, paths, costByCell, budget,
                    ResolveFusionEnterCost, origins);
                break;
            case ReachIntent.Embark:
                actionCells = ExpandByEntryCost(
                    request, map, movementCells, paths, costByCell, budget,
                    CanEmbarkAtCell, origins);
                break;
            default:
                // Captura e mobilidade devolvem SO alcance. Cruzar alcance com
                // objetivo (e chamar PodeCapturar) e trabalho da IA, nao do
                // envelope: ver CaptureOpportunityClaimService.
                actionCells = new HashSet<Vector3Int>(movementCells);
                break;
        }

        // Captura e mobilidade materializam na propria celula de parada: a
        // origem e ela mesma e a entrada ja foi paga pelo movimento.
        if (request.Intent == ReachIntent.Capture
            || request.Intent == ReachIntent.Mobility)
        {
            foreach (Vector3Int cell in actionCells)
            {
                origins[cell] = new ReachOrigin(
                    cell,
                    Mathf.Max(0, budget - ResolveKnownCost(costByCell, cell)),
                    0);
            }
        }

        // SupportsOperationDomain e um predicado de SUPRIDOR. Aplicar em fusao
        // ou embarque reprova tudo, porque a unidade nao e supridora — foi
        // exatamente assim que a hotzone de embarque zerou na primeira versao.
        bool domainFilterApplies =
            request.FilterByOperationDomain
            && (request.Intent == ReachIntent.Service
                || request.Intent == ReachIntent.Transfer);
        int beforeDomainFilter = actionCells.Count;
        if (domainFilterApplies)
            ApplyOperationDomainFilter(request, map, actionCells);

        var lineCells = new HashSet<Vector3Int>(actionCells);
        lineCells.ExceptWith(movementCells);

        return new UnitReachEnvelope(
            request.Intent,
            request.Band,
            paths,
            costByCell,
            movementCells,
            actionCells,
            lineCells,
            origins,
            turnsByCell)
        {
            SubStep = subStep,
            Diagnostic =
                $"orçamento={budget}; movimento={movementCells.Count}; " +
                $"ação={beforeDomainFilter}"
                + (domainFilterApplies
                    ? $"→{actionCells.Count} (filtro de camada)"
                    : string.Empty)
                + (lastEntryRejection != null
                    ? $"; 1ª rejeição de entrada: {lastEntryRejection}"
                    : string.Empty)
        };
    }

    /// <summary>
    /// Primeira recusa do sensor de custo de entrada na ultima construcao.
    /// Existe so para diagnostico de ferramenta; nao e lido por decisao alguma.
    /// </summary>
    [ThreadStatic]
    private static string lastEntryRejection;

    // ------------------------------------------------------------------
    // Orcamento e movimento — reuso, nunca pathfinding novo.
    // ------------------------------------------------------------------

    /// <summary>
    /// Origem do calculo: a sobrescrita quando existe, a celula atual quando
    /// nao. Nada e movido — o override so troca o ponto de partida.
    /// </summary>
    private static Vector3Int ResolveOrigin(UnitReachRequest request)
    {
        Vector3Int origin = request.OriginOverride
                            ?? request.Unit.CurrentCellPosition;
        origin.z = 0;
        return origin;
    }

    private static int ResolveBudget(UnitReachRequest request)
    {
        if (request.MovementBudget > 0)
            return request.MovementBudget;

        return request.Band == ReachBand.Operational
            ? AIActionReachCoordinator.ResolveOperationalBudget(
                request.Unit, request.OperationalTurns)
            : AIActionReachCoordinator.ResolveTacticalBudget(request.Unit);
    }

    private static void ResolveMovement(
        UnitReachRequest request,
        ReachSubStep subStep,
        Tilemap map,
        int budget,
        out Dictionary<Vector3Int, List<Vector3Int>> paths,
        out Dictionary<Vector3Int, int> costByCell,
        out Dictionary<Vector3Int, int> turnsByCell)
    {
        // Malha vinda pronta do chamador: e sempre desta rodada.
        if (request.PrebuiltPaths != null)
        {
            paths = request.PrebuiltPaths;
            costByCell = request.PrebuiltCosts ?? new Dictionary<Vector3Int, int>();
            turnsByCell = null;
            return;
        }

        if (request.PrebuiltCosts != null)
        {
            paths = new Dictionary<Vector3Int, List<Vector3Int>>();
            costByCell = request.PrebuiltCosts;
            turnsByCell = null;
            return;
        }

        Vector3Int origin = ResolveOrigin(request);
        turnsByCell = null;

        if (request.Band == ReachBand.Operational
            || UsesCubicGeometry(subStep))
        {
            // A medicao PEDIDA desce ate o builder. Sem isso o popup oferecia
            // "Linear (cubica)" e o servico devolvia caminhos, porque a
            // geometria era reinferida da ficha da unidade la embaixo.
            bool cubicGeometry = UsesCubicGeometry(subStep);
            costByCell =
                request.Band == ReachBand.Operational
                    ? BuildTurnChainedReach(
                        request.Unit,
                        map,
                        request.TerrainDatabase,
                        origin,
                        request.OperationalTurns,
                        cubicGeometry,
                        out turnsByCell)
                    : AIActionReachCoordinator.BuildSectorReachMap(
                        request.Unit,
                        map,
                        request.TerrainDatabase,
                        origin,
                        Mathf.Max(0, budget),
                        cubicGeometry);
            // A malha setorial nao produz caminhos. Publicar os destinos com
            // caminho nulo mantem PathsByDestination como indice de alcance,
            // que e o formato que os consumidores herdados ja esperavam.
            paths = new Dictionary<Vector3Int, List<Vector3Int>>(costByCell.Count);
            foreach (Vector3Int cell in costByCell.Keys)
                paths[cell] = null;
            return;
        }

        paths = UnitMovementPathRules.CalcularCaminhosValidos(
            map,
            request.Unit,
            Mathf.Max(0, budget),
            request.TerrainDatabase,
            request.OriginOverride);
        costByCell = new Dictionary<Vector3Int, int>();
    }

    /// <summary>
    /// Alcance de N turnos como SOMA de turnos, nao como orcamento unico.
    ///
    /// MP e teto por turno (UnitData) e nao acumula. Um soldado de 3 MP faz
    /// 3+3, nao 6: na montanha de custo 2 ele entra em UMA por turno, porque
    /// depois da primeira sobra 1 e a segunda pede 2. Um BFS de orcamento 6
    /// daria tres montanhas — alcance que nao existe no jogo.
    ///
    /// Cada turno reinicia o teto, entao a busca e encadeada: o alcance do
    /// turno seguinte parte de cada celula alcancada no anterior.
    /// </summary>
    private static Dictionary<Vector3Int, int> BuildTurnChainedReach(
        UnitManager unit,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        Vector3Int origin,
        int turns,
        bool cubicGeometry,
        out Dictionary<Vector3Int, int> turnsByCell)
    {
        turnsByCell = new Dictionary<Vector3Int, int>();
        int maxPerTurn = Mathf.Max(0, unit != null ? unit.MaxMovementPoints : 0);
        if (maxPerTurn <= 0)
        {
            turnsByCell[origin] = 1;
            return new Dictionary<Vector3Int, int> { [origin] = 0 };
        }

        // O turno ATUAL vale o que ainda sobrou; os seguintes valem MP cheio.
        // Uma unidade que ja gastou movimento nesta rodada projeta menos, e o
        // Operational encolhe conforme ela age — que e o comportamento real.
        // Zero restante cai para o teto cheio, mesma regra do
        // AIActionReachCoordinator.ResolveTacticalBudget.
        int firstTurn = Mathf.Max(0, unit.RemainingMovementPoints);
        if (firstTurn <= 0)
            firstTurn = maxPerTurn;

        // Quem mede em cubica ignora geografia: o encadeamento por turno nao
        // muda nada, o alcance e cubico puro sobre a soma dos tetos. Vale para
        // aeronave (geometria da ficha) e para quem PEDIU a medicao linear.
        if (cubicGeometry
            || AIActionReachCoordinator.UsesCubicSectorReach(unit))
        {
            Dictionary<Vector3Int, int> cubic =
                AIActionReachCoordinator.BuildSectorReachMap(
                    unit,
                    map,
                    terrainDatabase,
                    origin,
                    firstTurn + maxPerTurn * Mathf.Max(0, turns - 1),
                    cubicGeometry);
            // Sem geografia o turno sai direto do custo: o que cabe no que
            // sobrou hoje e desta rodada; o resto e do turno seguinte.
            foreach (KeyValuePair<Vector3Int, int> pair in cubic)
            {
                turnsByCell[pair.Key] = pair.Value <= firstTurn
                    ? 1
                    : 1 + Mathf.CeilToInt(
                        (pair.Value - firstTurn) / (float)maxPerTurn);
            }
            return cubic;
        }

        // UMA passada. A versao anterior encadeava um BFS por celula da
        // fronteira — dezenas de travessias completas por decisao, cada uma com
        // origem diferente (portanto sem reuso do MovementReachCache).
        return UnitMovementPathRules.CalculateTurnChainedCostMap(
            map,
            unit,
            origin,
            firstTurn,
            maxPerTurn,
            turns,
            terrainDatabase,
            out turnsByCell);
    }

    private static UnitReachRequest CloneForBand(
        UnitReachRequest source, ReachBand band)
    {
        return new UnitReachRequest
        {
            Unit = source.Unit,
            BoardMap = source.BoardMap,
            TerrainDatabase = source.TerrainDatabase,
            Intent = source.Intent,
            Band = band,
            // O orcamento explicito so vale para a banda que o chamador pediu.
            MovementBudget = band == source.Band ? source.MovementBudget : 0,
            OperationalTurns = source.OperationalTurns,
            PrebuiltPaths = band == source.Band ? source.PrebuiltPaths : null,
            PrebuiltCosts = band == source.Band ? source.PrebuiltCosts : null,
            FilterByOperationDomain = source.FilterByOperationDomain,
            RangeOverride = source.RangeOverride,
            SubStep = source.SubStep,
            OriginOverride = source.OriginOverride,
            MovementMode = band == ReachBand.Operational
                ? UnitThreatEnvelopeMovement.Potential
                : source.MovementMode,
            DpqAirHeightConfig = source.DpqAirHeightConfig,
            EnableLdt = source.EnableLdt,
            EnableLos = source.EnableLos,
            EnableSpotter = source.EnableSpotter,
            EmbarkPassenger = source.EmbarkPassenger,
            IncludeMovementCosts = source.IncludeMovementCosts
        };
    }

    // ------------------------------------------------------------------
    // Intencao: servico e transferencia (PodeSuprirSensor).
    // ------------------------------------------------------------------

    private static HashSet<Vector3Int> ExpandByServiceRange(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> movementCells,
        Dictionary<Vector3Int, int> costByCell,
        int budget,
        Dictionary<Vector3Int, ReachOrigin> origins)
    {
        var actionCells = new HashSet<Vector3Int>();
        SupplierRangeMode range = ResolveServiceRange(request);
        var neighbors = new List<Vector3Int>(6);

        foreach (Vector3Int moveCell in movementCells)
        {
            if (range == SupplierRangeMode.Hybrid0Or1Hex
                || range == SupplierRangeMode.SameHexOrEmbarked)
            {
                actionCells.Add(moveCell);
                RecordServiceOrigin(
                    origins, costByCell, budget, moveCell, moveCell);
            }

            if (range != SupplierRangeMode.Adjacent1Hex
                && range != SupplierRangeMode.Hybrid0Or1Hex)
            {
                continue;
            }

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map.GetTile(targetCell) == null)
                    continue;
                actionCells.Add(targetCell);
                RecordServiceOrigin(
                    origins, costByCell, budget, moveCell, targetCell);
            }
        }

        return actionCells;
    }

    /// <summary>
    /// Servico nao move para o alvo: o custo de entrada e zero e a origem e a
    /// celula de onde o supridor atende.
    ///
    /// Guarda a origem MAIS BARATA, com desempate estavel por coordenada. Sem
    /// isso a origem publicada dependia da ordem de iteracao do HashSet e
    /// mudava sozinha quando a travessia mudava — diagnostico nao reproduzivel.
    /// </summary>
    private static void RecordServiceOrigin(
        Dictionary<Vector3Int, ReachOrigin> origins,
        Dictionary<Vector3Int, int> costByCell,
        int budget,
        Vector3Int fromCell,
        Vector3Int actionCell)
    {
        int cost = ResolveKnownCost(costByCell, fromCell);
        if (origins.TryGetValue(actionCell, out ReachOrigin known))
        {
            int knownCost = ResolveKnownCost(costByCell, known.FromCell);
            if (knownCost < cost)
                return;
            if (knownCost == cost
                && CompareCells(known.FromCell, fromCell) <= 0)
            {
                return;
            }
        }

        origins[actionCell] = new ReachOrigin(
            fromCell, Mathf.Max(0, budget - cost), 0);
    }

    private static int CompareCells(Vector3Int a, Vector3Int b)
    {
        if (a.x != b.x)
            return a.x < b.x ? -1 : 1;
        if (a.y != b.y)
            return a.y < b.y ? -1 : 1;
        return 0;
    }

    // ------------------------------------------------------------------
    // Mobilidade: componente de movimento proprio da unidade.
    //
    // CONSULTA DIRIGIDA. A IA entrega UM objetivo ja escolhido e pergunta a
    // relacao dele com o grafo de movimento. O envelope nunca varre o tabuleiro
    // classificando celulas distantes: isso nao e resposta materializavel.
    //
    // O componente e a malha de custo SEM TETO — a mesma funcao do Operational
    // com orcamento ilimitado. Nao existe algoritmo novo aqui, so a ausencia
    // de limite. Caro por decisao: cacheie por unidade/revisao antes de usar
    // isso em loop de IA.
    // ------------------------------------------------------------------

    public static Dictionary<Vector3Int, int> BuildOwnMovementComponent(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase)
    {
        // Flood fill do tabuleiro inteiro. Caro por construcao; o cronometro
        // fica aqui para que ele apareca nominalmente no
        // [AI Perf][Phase2 Breakdown] em vez de virar suspeita.
        using var perf = new AIDecisionPerfScope(unit, "ownMovementComponent");
        AIDecisionPerf.AddCount("OwnMovementComponentBuilds");
        var reached = new Dictionary<Vector3Int, int>();
        if (unit == null)
            return reached;

        Tilemap map = boardMap != null ? boardMap : unit.BoardTilemap;
        if (map == null)
            return reached;

        Vector3Int origin = unit.CurrentCellPosition;
        origin.z = 0;

        // MP e teto POR TURNO (UnitData). Como o movimento nao acumula entre
        // turnos, um hex cujo custo de entrada passa desse teto e intransponivel
        // para sempre — nao adianta ter mais turnos. Logo, a conectividade nao
        // depende de distancia nenhuma: e flood fill sobre "consigo pagar a
        // entrada deste hex num turno". Obus de 2 MP nunca entra em floresta 3,
        // e a floresta ainda BLOQUEIA o corredor atras dela.
        int perTurn = Mathf.Max(1, unit.MaxMovementPoints);
        if (AIActionReachCoordinator.UsesCubicSectorReach(unit))
        {
            // Aeronautica ignora geografia: o componente e o tabuleiro.
            BoundsInt airBounds = map.cellBounds;
            foreach (Vector3Int raw in airBounds.allPositionsWithin)
            {
                Vector3Int cell = raw;
                cell.z = 0;
                if (map.GetTile(cell) != null)
                    reached[cell] = AIActionReachCoordinator.CubicDistance(origin, cell);
            }
            return reached;
        }

        // DELEGACAO, nao BFS proprio. A busca encadeada por turno ja implementa
        // exatamente esta regra — teto de MP por turno, hex mais caro que o
        // teto e intransponivel para sempre e bloqueia o corredor — e, ao
        // contrario da versao manual que vivia aqui, ela monta um
        // MovementQueryCache: terreno, construcao e estrutura resolvidos UMA
        // vez por celula em vez de seis.
        //
        // A versao manual custava ~1s por chamada em mapa grande e, com quinze
        // unidades pedindo carona no mesmo turno, dominava a Fase 2 inteira.
        return UnitMovementPathRules.CalculateTurnChainedCostMap(
            map,
            unit,
            origin,
            perTurn,
            perTurn,
            ComponentTurnHorizon,
            terrainDatabase,
            out _);
    }

    /// <summary>
    /// Horizonte do componente. Nao e regra de jogo: e so um teto para a busca
    /// terminar. Componente e "onde a unidade chega DADO TEMPO SUFICIENTE", e
    /// quinhentos turnos atravessam qualquer mapa deste jogo varias vezes.
    /// </summary>
    private const int ComponentTurnHorizon = 512;

    /// <summary>
    /// Classifica uma celula contra o grafo de movimento da unidade.
    /// Passe <paramref name="ownComponent"/> pronto para nao reconstruir a
    /// malha a cada celula.
    /// </summary>
    public static MobilityRelation ClassifyMobility(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Vector3Int cell,
        IReadOnlyDictionary<Vector3Int, int> ownComponent)
    {
        cell.z = 0;
        if (ownComponent != null && ownComponent.ContainsKey(cell))
            return MobilityRelation.OwnComponent;

        Tilemap map = boardMap != null && unit != null
            ? boardMap
            : unit != null ? unit.BoardTilemap : null;
        if (map == null)
            return MobilityRelation.NotOccupiable;

        // Fora do componente, so interessa se a unidade PARARIA ali caso fosse
        // entregue. Mar para infantaria reprova aqui: nao e destino de nada.
        return UnitMovementPathRules.TryGetEnterCellCost(
            map,
            unit,
            cell,
            terrainDatabase,
            applyOperationalAutonomyModifier: false,
            out _)
            ? MobilityRelation.OtherComponent
            : MobilityRelation.NotOccupiable;
    }

    private static SupplierRangeMode ResolveServiceRange(UnitReachRequest request)
    {
        if (request.RangeOverride.HasValue)
            return request.RangeOverride.Value;

        if (!request.Unit.TryGetUnitData(out UnitData data) || data == null)
            return SupplierRangeMode.SameHexOrEmbarked;

        return request.Intent == ReachIntent.Transfer
            ? data.collectionRange
            : data.serviceRange;
    }

    // ------------------------------------------------------------------
    // Intencao: fusao e embarque — mesma forma, sensores diferentes.
    //
    // O alvo NAO fica a "+1 gratis" do movimento: a unidade precisa conservar
    // MP suficiente para pagar o custo oficial de entrar na celula ocupada.
    // Nao existe hard-code de "orcamento - 1": em terreno de custo 2 sobra 2.
    // ------------------------------------------------------------------

    /// <summary>
    /// Decide se a intencao se materializa em targetCell partindo de fromCell
    /// com remainingMovement pontos. O predicado inteiro — legalidade E custo
    /// conservado — pertence ao sensor; o servico so varre a geometria.
    /// </summary>
    private delegate bool EntryPredicate(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int fromCell,
        Vector3Int targetCell,
        int remainingMovement,
        out int enterCost,
        out string reason);

    private static HashSet<Vector3Int> ExpandByEntryCost(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> movementCells,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell,
        int budget,
        EntryPredicate canMaterialize,
        Dictionary<Vector3Int, ReachOrigin> origins)
    {
        var actionCells = new HashSet<Vector3Int>();
        int totalMovement = Mathf.Max(0, budget);
        var neighbors = new List<Vector3Int>(6);

        foreach (Vector3Int moveCell in movementCells)
        {
            int spent = ResolveSpentMovement(
                request, map, moveCell, paths, costByCell);
            int remaining = Mathf.Max(0, totalMovement - spent);
            if (remaining <= 0)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(map, moveCell, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int targetCell = neighbors[i];
                targetCell.z = 0;
                if (map.GetTile(targetCell) == null
                    || actionCells.Contains(targetCell))
                {
                    continue;
                }

                if (canMaterialize(
                        request, map, moveCell, targetCell,
                        remaining, out int enterCost, out string reason))
                {
                    actionCells.Add(targetCell);
                    origins[targetCell] = new ReachOrigin(
                        moveCell, remaining, enterCost);
                    continue;
                }

                lastEntryRejection = lastEntryRejection
                    ?? $"{targetCell} partindo de {moveCell} " +
                       $"(restante={remaining}): {reason}";
            }
        }

        return actionCells;
    }

    private static int ResolveSpentMovement(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int moveCell,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        Dictionary<Vector3Int, int> costByCell)
    {
        if (costByCell.TryGetValue(moveCell, out int knownCost))
            return Mathf.Max(0, knownCost);

        if (paths.TryGetValue(moveCell, out List<Vector3Int> path)
            && path != null
            && path.Count > 0)
        {
            return Mathf.Max(
                0,
                UnitMovementPathRules.CalculateAutonomyCostForPath(
                    map,
                    request.Unit,
                    path,
                    request.TerrainDatabase,
                    applyOperationalAutonomyModifier: false));
        }

        return 0;
    }

    private static bool ResolveFusionEnterCost(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int fromCell,
        Vector3Int targetCell,
        int remainingMovement,
        out int enterCost,
        out string reason)
    {
        if (!PodeFundirSensor.TryResolveMergeEnterCost(
                map,
                request.TerrainDatabase,
                request.Unit,
                targetCell,
                out enterCost,
                out reason))
        {
            return false;
        }

        enterCost = Mathf.Max(1, enterCost);
        if (remainingMovement >= enterCost)
            return true;

        reason = $"MP insuficiente para fundir (custo={enterCost}).";
        return false;
    }

    private static bool CanEmbarkAtCell(
        UnitReachRequest request,
        Tilemap map,
        Vector3Int fromCell,
        Vector3Int targetCell,
        int remainingMovement,
        out int enterCost,
        out string reason)
    {
        enterCost = 0;
        // O passageiro e quem embarca. Sem passageiro explicito, a propria
        // unidade do pedido: o envelope responde "onde esta unidade consegue
        // embarcar", nao "onde este transportador recebe".
        UnitManager passenger = request.EmbarkPassenger != null
            ? request.EmbarkPassenger
            : request.Unit;

        UnitManager transporter =
            UnitOccupancyRules.GetUnitAtCell(map, targetCell, passenger);
        if (transporter == null
            || !transporter.TryGetUnitData(out UnitData transporterData)
            || transporterData == null
            || transporterData.transportSlots == null)
        {
            reason = "Sem transportador na celula.";
            return false;
        }

        // O predicado completo — transportador valido, contexto do terreno,
        // slot compativel, exclusividade, vaga E o MP conservado para entrar —
        // pertence inteiro ao PodeEmbarcarSensor. Basta um slot servir.
        reason = "Nenhum slot compativel.";
        for (int slotIndex = 0;
             slotIndex < transporterData.transportSlots.Count;
             slotIndex++)
        {
            if (PodeEmbarcarSensor.CanEmbarkFromProjectedCell(
                    passenger,
                    fromCell,
                    transporter,
                    slotIndex,
                    map,
                    request.TerrainDatabase,
                    remainingMovement,
                    out int slotCost,
                    out string slotReason))
            {
                enterCost = slotCost;
                return true;
            }

            reason = slotReason;
        }

        return false;
    }

    // ------------------------------------------------------------------
    // Filtro de dominio de operacao.
    //
    // NOTA DE PARIDADE: este filtro vivia duplicado na janela de Editor e no
    // panel helper, e nao existia no caminho da IA — que valida dominio por
    // candidato, via PodeSuprirSensor, depois de consultar o envelope. Manter
    // o filtro opcional preserva os dois comportamentos durante a unificacao.
    // Ligar para a IA muda o conjunto de candidatos e e decisao de jogabilidade,
    // nao de arquitetura.
    // ------------------------------------------------------------------

    private static void ApplyOperationDomainFilter(
        UnitReachRequest request,
        Tilemap map,
        HashSet<Vector3Int> actionCells)
    {
        if (actionCells.Count == 0
            || !request.Unit.TryGetUnitData(out UnitData supplierData)
            || supplierData == null)
        {
            return;
        }

        UnitManager unit = request.Unit;
        bool airborneSupplier =
            unit.GetDomain() == Domain.Air && !unit.IsAircraftGrounded;

        actionCells.RemoveWhere(cell =>
        {
            Domain operationDomain;
            HeightLevel operationHeight;
            if (airborneSupplier)
            {
                operationDomain = unit.GetDomain();
                operationHeight = unit.GetHeightLevel();
            }
            else if (!LayerTransitionRules.TryResolvePrimaryLayerAtCell(
                         map,
                         request.TerrainDatabase,
                         cell,
                         out operationDomain,
                         out operationHeight,
                         out _))
            {
                return true;
            }

            return !PodeSuprirSensor.SupportsOperationDomain(
                supplierData,
                operationDomain,
                operationHeight);
        });
    }

    // ------------------------------------------------------------------
    // Intencao: combate (PodeMirarSensor). Preserva cache e perfis.
    // ------------------------------------------------------------------

    /// <summary>
    /// Maior alcance util entre as armas com municao. Zero quando nao ha arma
    /// utilizavel — e ai o pedido de artilheiro e invalido, como combate sem
    /// arma.
    /// </summary>
    private static int ResolveMaxWeaponRange(UnitManager unit)
    {
        if (unit == null)
            return 0;
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return 0;

        int best = 0;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked?.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            best = Mathf.Max(best, embarked.GetRangeMax());
        }
        return best;
    }

    /// <summary>
    /// A distancia cai dentro do alcance de ALGUMA arma utilizavel?
    ///
    /// E a UNIAO dos intervalos [min,max], nao o intervalo da arma mais longa:
    /// uma unidade com um obus 3-4 e uma metralhadora 1-2 nao tem zona morta
    /// nenhuma, e tratar o minimo do obus como minimo da unidade abriria um
    /// buraco onde ela de fato atira.
    /// </summary>
    private static bool IsWithinAnyWeaponRange(UnitManager unit, int distance)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit?.GetEmbarkedWeapons();
        if (weapons == null)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked?.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            if (distance >= embarked.GetRangeMin()
                && distance <= embarked.GetRangeMax())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Banda do artilheiro. NAO ha deslocamento aqui: a unidade atira parada, e
    /// o que a banda descreve e o raio da ARMA.
    ///
    ///   MovementCells — os aneis que alguma arma cobre. E o "verde".
    ///   ActionCells   — o que a arma REALMENTE atinge parada, pelo sensor. E o
    ///                   "vermelho", e ele sobrescreve o verde.
    ///
    /// A ZONA MORTA NAO E BANDA, E O HEX DA UNIDADE TAMBEM NAO. A versao
    /// anterior publicava o disco cheio 0..alcance e contava com o vermelho por
    /// cima para revelar o buraco do alcance minimo. Isso pintava de verde hexes
    /// onde a unidade nao faz nada: ela nao anda ate la (nao ha deslocamento)
    /// nem atira la (esta abaixo do minimo). Verde quer dizer "materializa aqui"
    /// em toda a ferramenta, e ali nada se materializa — jogador e IA liam
    /// alcance onde ha buraco.
    ///
    /// O hex 0 sai pelo mesmo motivo, e nao por ser menos importante: pintado,
    /// ele afirma que a peca ataca no alcance 0. Ela esta ali, nao age ali.
    ///
    /// Num obus 3-4 a banda Tactical e {3, 4}. Os hexes 0, 1 e 2 saem VAZIOS.
    ///
    /// Na banda Operational o anel ALEM do alcance maximo continua na resposta
    /// — ele responde "de onde me alcancam se a situacao mudar". A zona morta
    /// nao: ela e buraco permanente de quem atira parado, nao distancia.
    /// </summary>
    private static UnitReachEnvelope BuildArtilleryBand(
        UnitReachRequest request,
        Tilemap map,
        ReachSubStep subStep)
    {
        UnitManager unit = request.Unit;
        int maxRange = ResolveMaxWeaponRange(unit);
        if (maxRange <= 0)
            return null;

        int bandRadius = request.Band == ReachBand.Operational
            ? maxRange * 2
            : maxRange;

        Vector3Int origin = ResolveOrigin(request);

        var bandCells = new HashSet<Vector3Int>();
        CollectCubicMovementCells(map, origin, bandRadius, bandCells);

        // Fura a zona morta, e o proprio hex da unidade vai junto: ficam os
        // aneis que alguma arma cobre e, na banda Operational, o anel alem do
        // alcance maximo. Todo o resto e buraco e sai da banda.
        int deadZoneCells = bandCells.RemoveWhere(cell =>
        {
            int distance =
                AIActionReachCoordinator.CubicDistance(origin, cell);
            if (IsWithinAnyWeaponRange(unit, distance))
                return false;
            return distance <= maxRange;
        });

        var fireCells = new HashSet<Vector3Int>();
        PodeMirarSensor.CollectValidFireCellsFromOrigin(
            unit, map, request.TerrainDatabase,
            SensorMovementMode.MoveuParado, origin, fireCells,
            request.DpqAirHeightConfig,
            request.EnableLdt, request.EnableLos, request.EnableSpotter);
        fireCells.RemoveWhere(cell => map.GetTile(cell) == null);

        var costByCell = new Dictionary<Vector3Int, int>(bandCells.Count);
        var turnsByCell = new Dictionary<Vector3Int, int>(bandCells.Count);
        foreach (Vector3Int cell in bandCells)
        {
            costByCell[cell] =
                AIActionReachCoordinator.CubicDistance(origin, cell);
            turnsByCell[cell] = 1;
        }

        // O tiro sai sempre do hex onde ela esta: nao ha deslocamento, entao a
        // origem da acao e a propria unidade e a entrada nao custa MP.
        var origins = new Dictionary<Vector3Int, ReachOrigin>(fireCells.Count);
        foreach (Vector3Int cell in fireCells)
            origins[cell] = new ReachOrigin(origin, 0, 0);

        var lineCells = new HashSet<Vector3Int>(fireCells);
        lineCells.ExceptWith(bandCells);

        return new UnitReachEnvelope(
            request.Intent,
            request.Band,
            new Dictionary<Vector3Int, List<Vector3Int>>(),
            costByCell,
            bandCells,
            fireCells,
            lineCells,
            origins,
            turnsByCell)
        {
            SubStep = subStep,
            Diagnostic =
                $"artilheiro: alcance máximo={maxRange}; " +
                $"banda={bandRadius} ({(request.Band == ReachBand.Operational ? "2× alcance" : "alcance")}); " +
                $"banda={bandCells.Count}; tiro real={fireCells.Count}; " +
                $"zona morta removida={deadZoneCells} hexes" +
                (fireCells.Count == 0
                    ? " — nenhuma célula de tiro (sem alvo válido)"
                    : string.Empty)
        };
    }

    internal static UnitThreatEnvelope BuildCombat(
        UnitReachRequest request,
        Tilemap map,
        out bool cacheHit)
    {
        cacheHit = false;
        UnitManager unit = request.Unit;
        ReachSubStep subStep = request.SubStep;

        // Reativo, nao dedutivo: pediram Combate e a unidade nao tem arma
        // utilizavel. Nada a devolver.
        if (ResolveCombatProfile(unit) == CombatProfile.None)
            return null;

        int movementSteps = request.MovementBudget > 0
            ? request.MovementBudget
            : request.MovementMode == UnitThreatEnvelopeMovement.CurrentTurn
                ? Mathf.Max(0, unit.RemainingMovementPoints)
                : Mathf.Max(0, unit.MaxMovementPoints);

        long cacheIndex = ((long)ResolveUnitIndex(unit) << 2) | (uint)request.MovementMode;
        int keyHash = BuildKeyHash(
            unit, map, movementSteps,
            request.EnableLdt, request.EnableLos, request.EnableSpotter,
            request.IncludeMovementCosts, subStep);
        if (Cache.TryGetValue(cacheIndex, out CacheEntry existing)
            && existing != null
            && existing.KeyHash == keyHash
            && existing.Envelope != null)
        {
            cacheHit = true;
            return existing.Envelope;
        }

        // A intencao Combate sempre expoe o vermelho. A SUBETAPA decide apenas
        // a geometria e se ha deslocamento:
        //   Artilheiro — nao move: verde em MP=0, vermelho do tiro parado.
        //   Terrestre  — caminhos validos.
        //   Aereo      — distancia cubica.
        //
        // O tiro parado entra SEMPRE: numa arma de alcance minimo 1 e maximo 2,
        // o tiro pos-movimento colapsa para 1, entao MoveuParado alcanca alvos
        // que MoveuAndando nao alcanca. Era o que o antigo perfil "Hibrido"
        // fazia — deixou de ser etapa e virou regra.
        bool includeStatic = true;
        bool includeMovement = subStep != ReachSubStep.Artilheiro;
        var staticThreat = new HashSet<Vector3Int>();
        var mobileThreat = new HashSet<Vector3Int>();
        var movementCells = new HashSet<Vector3Int>();
        var paths = new Dictionary<Vector3Int, List<Vector3Int>>();
        var costByCell = new Dictionary<Vector3Int, int>();
        var origins = new Dictionary<Vector3Int, ReachOrigin>();

        if (includeStatic)
        {
            PodeMirarSensor.CollectValidFireCellsFromOrigin(
                unit, map, request.TerrainDatabase, SensorMovementMode.MoveuParado,
                ResolveOrigin(request), staticThreat, request.DpqAirHeightConfig,
                request.EnableLdt, request.EnableLos, request.EnableSpotter);
            staticThreat.RemoveWhere(cell => map.GetTile(cell) == null);
        }

        // Artilheiro tem verde em MP=0: o hex onde ela esta continua sendo
        // celula de parada valida, so nao ha deslocamento.
        Vector3Int stand = ResolveOrigin(request);
        if (!includeMovement && map.GetTile(stand) != null)
            movementCells.Add(stand);

        if (includeMovement)
        {
            if (!UsesCubicGeometry(subStep))
            {
                paths = UnitMovementPathRules.CalcularCaminhosValidos(
                    map, unit, movementSteps, request.TerrainDatabase,
                    request.OriginOverride);
                foreach (Vector3Int rawCell in paths.Keys)
                {
                    Vector3Int cell = rawCell;
                    cell.z = 0;
                    if (map.GetTile(cell) != null)
                        movementCells.Add(cell);
                }
            }
            else
            {
                CollectCubicMovementCells(
                    map, ResolveOrigin(request), movementSteps, movementCells);
            }

            Vector3Int origin = ResolveOrigin(request);
            if (map.GetTile(origin) != null)
                movementCells.Add(origin);

            if (request.IncludeMovementCosts && paths.Count > 0)
            {
                foreach (Vector3Int cell in movementCells)
                {
                    costByCell[cell] = ResolveSpentMovement(
                        request, map, cell, paths, costByCell);
                }
            }

            var localThreat = new HashSet<Vector3Int>();
            foreach (Vector3Int moveCell in movementCells)
            {
                // Nao pular celulas de movimento que caiam dentro do staticThreat: uma
                // coisa e a celula estar no alcance parado, outra e tudo que se atinge
                // DE LA ja estar coberto. Numa hibrida (min 1 / max 2) o tiro pos-movimento
                // colapsa para alcance 1, entao a partir de uma celula na borda do alcance
                // parado ela alcanca alvos que o tiro parado nao alcanca. Pular aqui abria
                // um buraco no envelope e o CanAct barrava o ataque antes do sensor:
                // a artilheira combatente parava ao lado do alvo sem atirar.
                localThreat.Clear();
                PodeMirarSensor.CollectValidFireCellsFromOrigin(
                    unit, map, request.TerrainDatabase, SensorMovementMode.MoveuAndando,
                    moveCell, localThreat, request.DpqAirHeightConfig,
                    request.EnableLdt, request.EnableLos, request.EnableSpotter);
                foreach (Vector3Int rawTarget in localThreat)
                {
                    Vector3Int target = rawTarget;
                    target.z = 0;
                    if (map.GetTile(target) == null)
                        continue;
                    mobileThreat.Add(target);
                    // O tiro nao custa MP: EnterCost = 0 e o resto e o que
                    // sobrou ao chegar em moveCell. Guarda a origem mais barata,
                    // que e a resposta a "de onde eu atiro nesse alvo".
                    if (!origins.TryGetValue(target, out ReachOrigin known)
                        || ResolveKnownCost(costByCell, moveCell)
                           < ResolveKnownCost(costByCell, known.FromCell))
                    {
                        origins[target] = new ReachOrigin(
                            moveCell,
                            Mathf.Max(
                                0,
                                movementSteps
                                - ResolveKnownCost(costByCell, moveCell)),
                            0);
                    }
                }
            }
        }

        var attackable = new HashSet<Vector3Int>(movementCells);
        attackable.UnionWith(staticThreat);
        attackable.UnionWith(mobileThreat);

        var lineCells = new HashSet<Vector3Int>(staticThreat);
        lineCells.UnionWith(mobileThreat);
        lineCells.ExceptWith(movementCells);

        // Celula de movimento e materializavel a partir de si mesma.
        foreach (Vector3Int moveCell in movementCells)
        {
            if (!origins.ContainsKey(moveCell))
            {
                origins[moveCell] = new ReachOrigin(
                    moveCell,
                    Mathf.Max(
                        0,
                        movementSteps - ResolveKnownCost(costByCell, moveCell)),
                    0);
            }
        }

        // BuildCombat so atende a banda Tactical: tudo aqui e desta rodada.
        var combatTurns =
            new Dictionary<Vector3Int, int>(movementCells.Count);
        foreach (Vector3Int moveCell in movementCells)
            combatTurns[moveCell] = 1;

        var envelope = new UnitThreatEnvelope(
            ReachIntent.Combat,
            request.Band,
            paths,
            costByCell,
            movementCells,
            attackable,
            lineCells,
            origins,
            combatTurns)
        {
            Diagnostic =
                $"orçamento={movementSteps}; movimento={movementCells.Count}; " +
                $"ação={attackable.Count} (anel de tiro={lineCells.Count}); " +
                $"subetapa={subStep}"
        };
        Cache[cacheIndex] = new CacheEntry { KeyHash = keyHash, Envelope = envelope };
        return envelope;
    }

    private static int ResolveKnownCost(
        Dictionary<Vector3Int, int> costByCell, Vector3Int cell)
    {
        cell.z = 0;
        return costByCell.TryGetValue(cell, out int cost) ? cost : 0;
    }

    private static void CollectCubicMovementCells(
        Tilemap map,
        Vector3Int origin,
        int radius,
        HashSet<Vector3Int> destination)
    {
        if (map == null || destination == null)
            return;
        origin.z = 0;
        BoundsInt bounds = map.cellBounds;
        int minX = Mathf.Max(bounds.xMin, origin.x - radius * 2);
        int maxX = Mathf.Min(bounds.xMax - 1, origin.x + radius * 2);
        int minY = Mathf.Max(bounds.yMin, origin.y - radius);
        int maxY = Mathf.Min(bounds.yMax - 1, origin.y + radius);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (map.GetTile(cell) == null
                    || AIActionReachCoordinator.CubicDistance(origin, cell) > radius)
                    continue;
                destination.Add(cell);
            }
        }
    }

    /// <summary>
    /// USO EXCLUSIVO DA FACHADA HERDADA. Os chamadores antigos de
    /// UnitThreatEnvelopeService.TryGet nao informam subetapa, entao ela e
    /// derivada do armamento para preservar o comportamento de hoje. A API
    /// nova exige subetapa explicita — quem classifica a unidade e a IA.
    /// </summary>
    internal static ReachSubStep ResolveLegacyCombatSubStep(UnitManager unit)
    {
        if (ResolveCombatProfile(unit) == CombatProfile.DistanceStatic)
            return ReachSubStep.Artilheiro;

        return AIActionReachCoordinator.UsesCubicSectorReach(unit)
            ? ReachSubStep.Aereo
            : ReachSubStep.Terrestre;
    }

    private static CombatProfile ResolveCombatProfile(UnitManager unit)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons =
            unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null || weapons.Count == 0)
            return CombatProfile.None;

        bool hasOne = false;
        bool hasLong = false;
        bool hasHybrid = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null || embarked.squadAmmunition <= 0)
                continue;
            if (!PodeMirarSensor.TryResolveWeaponRangeCandidate(
                    embarked,
                    SensorMovementMode.MoveuParado,
                    requireAmmo: false,
                    out int min,
                    out int max))
                continue;
            hasOne |= min == 1 && max == 1;
            hasLong |= max > 1;
            hasHybrid |= min == 1 && max > 1;
        }

        if (hasHybrid || (hasOne && hasLong)) return CombatProfile.Hybrid;
        if (hasLong) return CombatProfile.DistanceStatic;
        if (hasOne) return CombatProfile.Movement;
        return CombatProfile.None;
    }

    private static int ResolveUnitIndex(UnitManager unit)
    {
        return unit.InstanceId > 0 ? unit.InstanceId : unit.GetEntityId().GetHashCode();
    }

    private static int BuildKeyHash(
        UnitManager unit,
        Tilemap map,
        int movementSteps,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter,
        bool includeMovementCosts,
        ReachSubStep subStep)
    {
        unchecked
        {
            int hash = 17;
            Vector3Int cell = unit.CurrentCellPosition;
            hash = hash * 31 + cell.x;
            hash = hash * 31 + cell.y;
            hash = hash * 31 + movementSteps;
            hash = hash * 31 + unit.CurrentFuel;
            hash = hash * 31 + (int)unit.GetDomain();
            hash = hash * 31 + (int)unit.GetHeightLevel();
            hash = hash * 31 + (unit.IsAircraftGrounded ? 1 : 0);
            hash = hash * 31 + map.GetEntityId().GetHashCode();
            hash = hash * 31 + ThreatRevisionTracker.GlobalBoardRevision;
            hash = hash * 31 + ThreatRevisionTracker.GetSlotObserverRevision(
                PlayerSlotId.FromIndex(unit.SlotIndex));
            hash = hash * 31 + ThreatRevisionTracker.MatchFlagsHash;
            hash = hash * 31 + (enableLdt ? 1 : 0);
            hash = hash * 31 + (enableLos ? 1 : 0);
            hash = hash * 31 + (enableSpotter ? 1 : 0);
            hash = hash * 31 + (includeMovementCosts ? 1 : 0);
            hash = hash * 31 + (int)subStep;

            IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
            int count = weapons != null ? weapons.Count : 0;
            hash = hash * 31 + count;
            for (int i = 0; i < count; i++)
            {
                UnitEmbarkedWeapon embarked = weapons[i];
                if (embarked == null)
                {
                    hash = hash * 31 + 7;
                    continue;
                }
                hash = hash * 31 + (embarked.weapon != null
                    ? embarked.weapon.GetEntityId().GetHashCode()
                    : 0);
                hash = hash * 31 + embarked.squadAmmunition;
                hash = hash * 31 + (int)embarked.selectedTrajectory;
            }
            return hash;
        }
    }
}
