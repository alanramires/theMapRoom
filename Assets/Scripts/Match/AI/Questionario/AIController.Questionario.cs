using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// =====================================================================================
// Questionário — os RESPONDENTES e o observador. Contrato:
// docs/AI Behavior/contrato_questionario.md
//
// Os respondentes são a camada de CONSUMIDOR: cada um pergunta ao sensor/serviço da
// sua casa e devolve o fato com o porquê. São iguais para todo papel; o papel só
// entrega a ÂNCORA (para onde a peça quer ir). Ordem e políticas moram no arquivo do
// papel (AIController.Questionario.Capturador.cs).
//
// SOMENTE LEITURA. O observador roda depois da decisão e antes da execução, sobre o
// mesmo estado que a decisão viu. Não move, não reserva, não carimba, não publica
// intenção: o contrato transacional vale aqui como em qualquer consulta provisória.
// =====================================================================================
public partial class AIController
{
    [Header("Questionário (observador)")]
    [Tooltip("Degrau 1 do questionário: roda ao lado do código atual e SÓ ANOTA, em " +
             AIQuestionarioLog.NomeArquivo + " na raiz do projeto. Nenhum comportamento muda.\n" +
             "Só no Editor. Custa os sensores das dez casas por peça observada — desligue " +
             "se for medir desempenho.")]
    [SerializeField] private bool observarQuestionario = true;

    /// <summary>O que os respondentes precisam saber da peça. Montado uma vez por peça.</summary>
    private sealed class ContextoQuestionario
    {
        public UnitManager Unit;
        public UnitData Data;
        public AIWorldSnapshot Snapshot;
        public Vector3Int FromCell;
        public Dictionary<Vector3Int, List<Vector3Int>> Paths;
        public HashSet<Vector3Int> Occupied;
        public bool SecondPass;

        // Entregue pelo papel: para onde a peça quer ir.
        public bool TemAncora;
        public Vector3Int Ancora;
        public string RotuloAncora = string.Empty;

        // Fatos que uma casa descobre e outra (ou a política) lê.
        public bool CapturaPreta;
        public Vector3Int CelulaPreta;
        public QueroCaronaResult Carona;
    }

    private ContextoQuestionario MontarContextoQuestionario(
        UnitManager unit,
        UnitData data,
        AIWorldSnapshot snapshot,
        bool secondPass)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        return new ContextoQuestionario
        {
            Unit = unit,
            Data = data,
            Snapshot = snapshot,
            FromCell = fromCell,
            Paths = UnitMovementPathRules.CalcularCaminhosValidos(
                boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase)
                ?? new Dictionary<Vector3Int, List<Vector3Int>>(),
            Occupied = BuildOccupied(unit),
            SecondPass = secondPass,
        };
    }

    // ─────────────────────────────────────────────────────────── respondentes ──

    /// <summary>
    /// "Existe capturável na minha célula ou no meu TÁTICO?" (§6.5). Pergunta ao
    /// PodeCapturar duas vezes por prédio: com a névoa (a hora de agir) e sem ela (o
    /// planejamento). Quando só a segunda passa, o prédio está no tático mas é PRETO —
    /// o gatilho do pedido de spotting (§6.3).
    /// </summary>
    private AIRespostaCasa ResponderCapturar(ContextoQuestionario ctx)
    {
        var capturaveis = new List<Vector3Int>();
        bool achouPreta = false;
        Vector3Int preta = default;

        IReadOnlyList<ConstructionManager> construcoes = ConstructionManager.AllActive;
        for (int i = 0; construcoes != null && i < construcoes.Count; i++)
        {
            ConstructionManager construcao = construcoes[i];
            if (construcao == null)
                continue;

            Vector3Int cell = construcao.CurrentCellPosition;
            cell.z = 0;
            bool naCelula = cell == ctx.FromCell;
            if (!naCelula && !ctx.Paths.ContainsKey(cell))
                continue;
            if (!naCelula
                && ctx.Occupied.Contains(cell)
                && UnitOccupancyRules.HasBlockingOccupantForUnitAtCell(boardTilemap, cell, ctx.Unit))
                continue;

            SensorMovementMode modo = naCelula
                ? SensorMovementMode.MoveuParado
                : SensorMovementMode.MoveuAndando;

            if (PodeCapturarSensor.TryGetCaptureTargetAtCell(
                    ctx.Unit, boardTilemap, cell, modo,
                    out _, out _, out _,
                    matchController,
                    applyFogOfWar: true,
                    knownConstruction: construcao))
            {
                capturaveis.Add(cell);
                continue;
            }

            if (!achouPreta
                && PodeCapturarSensor.TryGetCaptureTargetAtCell(
                    ctx.Unit, boardTilemap, cell, modo,
                    out _, out _, out _,
                    matchController,
                    applyFogOfWar: false,
                    knownConstruction: construcao))
            {
                achouPreta = true;
                preta = cell;
            }
        }

        if (capturaveis.Count > 0)
        {
            // Escolha PROVISÓRIA, e o porquê diz isso: escolher entre vários é do
            // MelhorCaptura (consumidor). Aqui só a ordem óbvia, para o registro ler.
            Vector3Int escolhida = capturaveis[0];
            string criterio = "menos passos";
            if (capturaveis.Contains(ctx.FromCell))
            {
                escolhida = ctx.FromCell;
                criterio = "célula atual";
            }
            else if (ctx.TemAncora && capturaveis.Contains(ctx.Ancora))
            {
                escolhida = ctx.Ancora;
                criterio = "é a âncora";
            }
            else
            {
                int melhor = int.MaxValue;
                for (int i = 0; i < capturaveis.Count; i++)
                {
                    int passos = GetPathStepCount(ctx.Paths, capturaveis[i]);
                    if (passos < melhor)
                    {
                        melhor = passos;
                        escolhida = capturaveis[i];
                    }
                }
            }

            return new AIRespostaCasa(
                AICasa.Capturar, AIResposta.Sim,
                $"{capturaveis.Count} capturável(is) no tático; escolha provisória por {criterio}",
                escolhida);
        }

        if (achouPreta)
        {
            ctx.CapturaPreta = true;
            ctx.CelulaPreta = preta;
            return new AIRespostaCasa(
                AICasa.Capturar, AIResposta.Nao,
                "prédio no tático, mas PRETO (terreno nunca explorado)",
                preta);
        }

        return new AIRespostaCasa(AICasa.Capturar, AIResposta.Nao, "nenhum capturável no tático");
    }

    /// <summary>Três estados do hex (§3.2), lidos da névoa confirmada do slot.</summary>
    private AIRespostaCasa ResponderEnxergar(ContextoQuestionario ctx, bool temAlvo, Vector3Int alvo, string rotuloAlvo)
    {
        if (!temAlvo)
            return new AIRespostaCasa(AICasa.Enxergar, AIResposta.NaoSeAplica, "sem alvo para olhar");

        MatchController mc = matchController != null ? matchController : GetMatchController();
        if (mc == null)
            return new AIRespostaCasa(AICasa.Enxergar, AIResposta.NaoSeiResponder, "sem MatchController", alvo);

        if (mc.IsCellVisibleForActiveTeam(alvo))
            return new AIRespostaCasa(AICasa.Enxergar, AIResposta.Sim, $"{rotuloAlvo}: visível agora", alvo);

        bool explorado = mc.IsCellExploredBySlot(PlayerSlotId.FromIndex(ctx.Snapshot.AISlotIndex), alvo);
        return new AIRespostaCasa(
            AICasa.Enxergar, AIResposta.Nao,
            explorado
                ? $"{rotuloAlvo}: conhecido, fora da visão (foto velha)"
                : $"{rotuloAlvo}: desconhecido",
            alvo);
    }

    /// <summary>
    /// "Detecto alguém no alvo ou no meu tático?" Lê só inimigos que o slot detecta
    /// (snapshot.EnemyUnits já vem filtrado pela névoa). Indício de ocupante não conta.
    /// </summary>
    private AIRespostaCasa ResponderDetectar(ContextoQuestionario ctx, bool temAlvo, Vector3Int alvo)
    {
        int noAlvo = 0;
        int noTatico = 0;
        List<UnitManager> inimigos = ctx.Snapshot.EnemyUnits;
        for (int i = 0; inimigos != null && i < inimigos.Count; i++)
        {
            UnitManager inimigo = inimigos[i];
            if (inimigo == null || inimigo.IsDead || inimigo.IsEmbarked)
                continue;

            Vector3Int cell = inimigo.CurrentCellPosition;
            cell.z = 0;
            if (temAlvo && cell == alvo)
            {
                noAlvo++;
                continue;
            }

            // Inimigo nunca está nos caminhos (ele bloqueia); conta quem encosta neles.
            if (SectorManager.HexDistance(cell, ctx.FromCell) <= 1f)
            {
                noTatico++;
                continue;
            }

            foreach (Vector3Int pathCell in ctx.Paths.Keys)
            {
                if (SectorManager.HexDistance(cell, pathCell) <= 1f)
                {
                    noTatico++;
                    break;
                }
            }
        }

        string porque = $"no alvo: {noAlvo}, encostados no tático: {noTatico}";
        return noAlvo + noTatico > 0
            ? new AIRespostaCasa(AICasa.Detectar, AIResposta.Sim, porque)
            : new AIRespostaCasa(AICasa.Detectar, AIResposta.Nao, porque);
    }

    /// <summary>"Preciso embarcar para chegar lá?" O QueroCarona é puro e cacheado.</summary>
    private AIRespostaCasa ResponderEmbarcar(ContextoQuestionario ctx, QueroCaronaRequest pedido)
    {
        if (pedido == null)
            return new AIRespostaCasa(AICasa.Embarcar, AIResposta.NaoSeiResponder, "sem pedido de carona montado");

        QueroCaronaResult carona = QueroCaronaService.Evaluate(pedido);
        ctx.Carona = carona;
        if (carona == null)
            return new AIRespostaCasa(AICasa.Embarcar, AIResposta.NaoSeiResponder, "QueroCarona sem resposta");
        if (carona.captureQuestionInapplicable)
            return new AIRespostaCasa(AICasa.Embarcar, AIResposta.NaoSeAplica, carona.reason);

        string porque = $"envelope={carona.reach}; {carona.reason}";
        return carona.wantsRide
            ? new AIRespostaCasa(AICasa.Embarcar, AIResposta.Sim, porque, carona.evaluatedTarget)
            : new AIRespostaCasa(AICasa.Embarcar, AIResposta.Nao, porque);
    }

    /// <summary>
    /// "Reposiciono para chegar perto?" SIM quando alguma célula livre do tático
    /// baixa o custo REAL de movimento até a âncora. Custo de rota, nunca HexDistance.
    /// </summary>
    private AIRespostaCasa ResponderReposicionar(ContextoQuestionario ctx)
    {
        if (!ctx.TemAncora)
            return new AIRespostaCasa(AICasa.Reposicionar, AIResposta.Nao, "sem âncora");
        if (ctx.FromCell == ctx.Ancora)
            return new AIRespostaCasa(AICasa.Reposicionar, AIResposta.Nao, "já está na âncora");

        int orcamento = Mathf.Max(1, ctx.Unit.MaxMovementPoints) * 4;
        Dictionary<Vector3Int, int> custoAteAncora = UnitMovementPathRules.CalculateMovementCostMap(
            boardTilemap, ctx.Unit, ctx.Ancora, orcamento, terrainDatabase);

        bool temAtual = custoAteAncora.TryGetValue(ctx.FromCell, out int custoAtual);
        int melhorCusto = temAtual ? custoAtual : int.MaxValue;
        bool achou = false;
        Vector3Int melhor = ctx.FromCell;
        foreach (Vector3Int raw in ctx.Paths.Keys)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            if (cell == ctx.FromCell || ctx.Occupied.Contains(cell))
                continue;
            if (!custoAteAncora.TryGetValue(cell, out int custo) || custo >= melhorCusto)
                continue;
            melhorCusto = custo;
            melhor = cell;
            achou = true;
        }

        if (!achou)
        {
            return new AIRespostaCasa(
                AICasa.Reposicionar, AIResposta.Nao,
                temAtual
                    ? $"nenhuma célula livre do tático chega mais perto de {ctx.RotuloAncora}"
                    : $"{ctx.RotuloAncora} além de {orcamento} MP de rota");
        }

        string atual = temAtual ? custoAtual.ToString() : $">{orcamento}";
        return new AIRespostaCasa(
            AICasa.Reposicionar, AIResposta.Sim,
            $"custo até {ctx.RotuloAncora}: {atual} → {melhorCusto}",
            melhor);
    }

    /// <summary>
    /// "Existe célula no tático de onde posso atirar?" Só o FATO da legalidade
    /// (PodeMirar); se vale o tiro é política, e o PassesAttackDecision fica de fora.
    /// </summary>
    private AIRespostaCasa ResponderMirar(ContextoQuestionario ctx)
    {
        List<UnitManager> inimigos = CollectVisibleAssaultEnemies(ctx.Snapshot.AITeam);
        if (inimigos == null || inimigos.Count == 0)
            return new AIRespostaCasa(AICasa.Mirar, AIResposta.Nao, "nenhum inimigo detectado");

        foreach (Vector3Int raw in ctx.Paths.Keys)
        {
            Vector3Int cell = raw;
            cell.z = 0;
            if (cell != ctx.FromCell && ctx.Occupied.Contains(cell))
                continue;

            for (int i = 0; i < inimigos.Count; i++)
            {
                UnitManager inimigo = inimigos[i];
                if (inimigo == null || inimigo.IsDead || inimigo.IsEmbarked)
                    continue;
                if (!CanAttackTargetFrom(ctx.FromCell, cell, ctx.Unit, inimigo))
                    continue;

                return new AIRespostaCasa(
                    AICasa.Mirar, AIResposta.Sim,
                    $"alvo legal: {inimigo.UnitDisplayName}#{inimigo.InstanceId}",
                    cell);
            }
        }

        return new AIRespostaCasa(AICasa.Mirar, AIResposta.Nao, "nenhum alvo legal a partir do tático");
    }

    /// <summary>As casas que este degrau ainda não sabe responder, ou que a ficha não tem.</summary>
    private static AIRespostaCasa ResponderPorCapacidade(AICasa casa, bool temCapacidade, string semCapacidade)
    {
        return temCapacidade
            ? new AIRespostaCasa(casa, AIResposta.NaoSeiResponder, "respondente ainda não ligado")
            : new AIRespostaCasa(casa, AIResposta.NaoSeAplica, semCapacidade);
    }

    // ───────────────────────────────────────────────────────────── observador ──

    /// <summary>
    /// Chamado pela Fase 2 depois de DecideUnitAction e antes da execução. Escolhe o
    /// papel, roda o questionário dele e grava o registro ao lado do que o código fez.
    /// Nunca derruba o turno: qualquer erro aqui vira aviso e o turno segue.
    /// </summary>
    private void ObservarQuestionario(
        UnitManager unit,
        AIWorldSnapshot snapshot,
        PlayerAction codigoFez,
        bool secondPass)
    {
        if (!observarQuestionario || !Application.isEditor)
            return;
        if (unit == null || snapshot == null || unit.IsDead || unit.IsEmbarked)
            return;
        if (!unit.TryGetUnitData(out UnitData data) || data == null)
            return;

        try
        {
            // Degrau 1: só o Capturador tem questionário. Os outros cinco entram na
            // mesma forma, papel por papel (§9).
            if (UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador))
                ObservarQuestionarioCapturador(unit, data, snapshot, codigoFez, secondPass);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[Questionario] observador falhou em {unit.UnitDisplayName}#{unit.InstanceId}: {ex.Message}");
        }
    }

    /// <summary>A casa que uma ação do código de hoje materializa, para comparar.</summary>
    private static bool TryCasaDaAcao(PlayerAction action, out AICasa casa)
    {
        casa = AICasa.Reposicionar;
        if (action == null)
            return false;

        switch (action.SensorAction)
        {
            case SensorActionType.Capture: casa = AICasa.Capturar; return true;
            case SensorActionType.Attack: casa = AICasa.Mirar; return true;
            case SensorActionType.Embark: casa = AICasa.Embarcar; return true;
            case SensorActionType.Disembark: casa = AICasa.Desembarcar; return true;
            case SensorActionType.Merge: casa = AICasa.Fundir; return true;
            case SensorActionType.Supply: casa = AICasa.Suprir; return true;
            case SensorActionType.Transfer: casa = AICasa.Transferir; return true;
            case SensorActionType.None:
            case SensorActionType.Land:
                casa = AICasa.Reposicionar; return true;
            default:
                return false;
        }
    }

    private static string DescreverAcao(PlayerAction action)
    {
        if (action == null)
            return "nenhuma ação";

        var sb = new StringBuilder();
        sb.Append(TryCasaDaAcao(action, out AICasa casa) ? casa.ToString() : action.SensorAction.ToString());
        if (action.HasMoveFrom && action.HasMoveTo)
        {
            sb.Append(action.MoveFrom == action.MoveTo
                ? $" parado @{Celula(action.MoveTo)}"
                : $" {Celula(action.MoveFrom)}→{Celula(action.MoveTo)}");
        }
        if (action.HasTargetHex)
            sb.Append($" alvo @{Celula(action.TargetHex)}");
        if (!string.IsNullOrEmpty(action.DebugLabel))
            sb.Append($" [{action.DebugLabel}]");
        return sb.ToString();
    }

    private static string Celula(Vector3Int cell) => $"({cell.x},{cell.y})";

    private static void AppendRegistro(StringBuilder sb, AIQuestionarioRegistro registro)
    {
        for (int i = 0; i < registro.Respostas.Count; i++)
        {
            AIRespostaCasa r = registro.Respostas[i];
            string especie = AIQuestionario.EspecieDe(r.Casa) == AIEspecieCasa.Informacao ? "i" : " ";
            string celula = r.TemCelula ? $" @{Celula(r.Celula)}" : string.Empty;
            sb.AppendLine(
                $"  {i + 1,2}{especie} {r.Casa,-13}{AIQuestionario.Rotulo(r.Resposta),-14}{r.Porque}{celula}");
        }
    }
}
