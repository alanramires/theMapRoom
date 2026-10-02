using System.Collections.Generic;
using System.Text;
using UnityEngine;

// =====================================================================================
// Questionário do CAPTURADOR — a ORDEM e as POLÍTICAS. Só isso mora aqui; responder as
// casas é dos respondentes (AIController.Questionario.cs), iguais para todo papel.
// Contrato: docs/AI Behavior/contrato_questionario.md §6 e §7.
//
// Lema: "antes do tiro, vem o dinheiro". Para o Capturador, Enxergar e Detectar são
// CONTEXTO, nunca portão: as casas abaixo delas rodam sempre.
//
// Degrau 1: as políticas só ANOTAM. As que este degrau ainda não sabe avaliar
// aparecem no registro como "não ligada", para não fingir que foram consideradas.
// =====================================================================================
public partial class AIController
{
    /// <summary>
    /// A ordem do desenho do autor (2026-10-02), canônica pelo contrato §7.2 — sem
    /// Fundir, que não é pergunta de papel: é do reparo (§6.4).
    /// </summary>
    private static readonly AICasa[] OrdemCapturador =
    {
        AICasa.Capturar,
        AICasa.Enxergar,
        AICasa.Detectar,
        AICasa.Embarcar,
        AICasa.Reposicionar,
        AICasa.Mirar,
        AICasa.Suprir,
        AICasa.Transferir,
        AICasa.Desembarcar,
    };

    private void ObservarQuestionarioCapturador(
        UnitManager unit,
        UnitData data,
        AIWorldSnapshot snapshot,
        PlayerAction codigoFez,
        bool secondPass)
    {
        var sb = new StringBuilder();
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;
        sb.Append($"[T{snapshot.TurnNumber}{(secondPass ? " 2ª passada" : string.Empty)}] ");
        sb.Append($"#{unit.InstanceId} {unit.UnitDisplayName} @{Celula(fromCell)} ");
        sb.Append($"HP={unit.CurrentHP} MP={unit.RemainingMovementPoints}");

        // O reparo é invariante e fica ACIMA do questionário (§8): aqui ele não roda.
        if (unit.IsUnderRepair)
        {
            sb.AppendLine(" — em reparo: invariante acima do questionário, não pergunta");
            sb.AppendLine($"  código fez: {DescreverAcao(codigoFez)}");
            sb.AppendLine();
            AIQuestionarioLog.Escrever(sb.ToString());
            return;
        }

        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(PlayerSlotId.FromIndex(snapshot.AISlotIndex));
        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        ContextoQuestionario ctx = MontarContextoQuestionario(unit, data, snapshot, secondPass);

        // A âncora vem do MESMO pedido que o capturador usa para a carona: o alvo do
        // plano, ou a reserva 1:1 do sem-plano, ou o magnético. Um lugar só resolve.
        QueroCaronaRequest pedido = BuildCapturerRideRequestWithTarget(unit, assigned);
        if (pedido != null && pedido.useExplicitTarget)
        {
            ctx.TemAncora = true;
            ctx.Ancora = pedido.explicitTarget;
            ctx.Ancora.z = 0;
            ctx.RotuloAncora = string.IsNullOrEmpty(pedido.explicitTargetLabel)
                ? $"âncora @{Celula(ctx.Ancora)}"
                : pedido.explicitTargetLabel;
        }

        sb.Append(assigned != null ? $" plano={assigned.Sector}" : " sem plano");
        sb.AppendLine(ctx.TemAncora ? $" | {ctx.RotuloAncora}" : " | sem âncora");

        // Enxergar e Detectar olham o prédio que a casa Capturar achou (inclusive o
        // preto); sem ele, a âncora.
        bool temAlvo = false;
        Vector3Int alvo = default;
        string rotuloAlvo = string.Empty;

        AIQuestionarioRegistro registro = AIQuestionario.Rodar(OrdemCapturador, casa =>
        {
            switch (casa)
            {
                case AICasa.Capturar:
                {
                    AIRespostaCasa r = ResponderCapturar(ctx);
                    if (r.TemCelula)
                    {
                        temAlvo = true;
                        alvo = r.Celula;
                        rotuloAlvo = r.Resposta == AIResposta.Sim ? "prédio capturável" : "prédio preto";
                    }
                    else if (ctx.TemAncora)
                    {
                        temAlvo = true;
                        alvo = ctx.Ancora;
                        rotuloAlvo = "âncora";
                    }
                    return r;
                }
                case AICasa.Enxergar:
                    return ResponderEnxergar(ctx, temAlvo, alvo, rotuloAlvo);
                case AICasa.Detectar:
                    return ResponderDetectar(ctx, temAlvo, alvo);
                case AICasa.Embarcar:
                    return ResponderEmbarcar(ctx, pedido);
                case AICasa.Reposicionar:
                    return ResponderReposicionar(ctx);
                case AICasa.Mirar:
                    return ResponderMirar(ctx);
                case AICasa.Suprir:
                    return ResponderPorCapacidade(casa, data.isSupplier, "sem capacidade de suprir");
                case AICasa.Transferir:
                    return ResponderPorCapacidade(casa, HasStockTransferCapability(unit, data), "sem capacidade de transferir");
                case AICasa.Desembarcar:
                    return ResponderPorCapacidade(casa, data.isTransporter, "não transporta");
                default:
                    return new AIRespostaCasa(casa, AIResposta.NaoSeiResponder, "casa sem respondente");
            }
        });

        AppendRegistro(sb, registro);

        // ── preliminar ──
        bool temFinal = registro.TemPreliminar;
        AIRespostaCasa final = temFinal ? registro.Preliminar : default;
        sb.AppendLine(temFinal
            ? $"  preliminar: {final.Casa}{(final.TemCelula ? $" @{Celula(final.Celula)}" : string.Empty)}"
            : "  preliminar: nenhuma casa de ação com SIM");

        // ── políticas ──
        var politicas = new List<string>();

        // §6.3 — prédio preto no tático: pede olho e adia UMA vez.
        if (ctx.CapturaPreta)
        {
            politicas.Add(secondPass
                ? $"preto @{Celula(ctx.CelulaPreta)}: 2ª passada, não pede de novo — vai ele mesmo"
                : $"pediria spotting @{Celula(ctx.CelulaPreta)} (prazo: esta fase; adiamento único) " +
                  "— sem MelhorSpotting, cai em \"eu mesmo vou\"");
        }

        // §6.6/§6.8 — quem fica com o prédio. Avaliado pela regra de HOJE, que o
        // registro nomeia: o contrato troca "menos passos" por rótulo e rodadas.
        if (temFinal
            && final.Casa == AICasa.Capturar
            && final.TemCelula
            && ShouldReserveOpportunisticCaptureForCloserUnit(
                unit, snapshot.AITeam, final.Celula, ctx.Paths, out UnitManager cedidoPara)
            && cedidoPara != null)
        {
            politicas.Add(
                $"cederia @{Celula(final.Celula)} a {cedidoPara.UnitDisplayName}#{cedidoPara.InstanceId} " +
                "(regra de hoje: com plano no tático, depois menos passos)");

            if (registro.TryGetProximaAcaoSim(AICasa.Capturar, out AIRespostaCasa proxima))
            {
                final = proxima;
            }
            else
            {
                final = new AIRespostaCasa(AICasa.Reposicionar, AIResposta.Sim, "cedeu", fromCell);
            }
        }

        politicas.Add("não ligadas neste degrau: Blitz, Swap, Vacate, montanha (DPQ), Defensiva, Rally");

        for (int i = 0; i < politicas.Count; i++)
            sb.AppendLine($"  política: {politicas[i]}");

        // ── final ──
        string finalTexto;
        if (temFinal)
        {
            finalTexto = $"{final.Casa}{(final.TemCelula ? $" @{Celula(final.Celula)}" : string.Empty)}" +
                         $" — \"{MotivoCapturador(final.Casa, ctx.TemAncora)}\"";
        }
        else
        {
            finalTexto = "Reposicionar parado (Confirmar Posição) — \"nada a fazer\"";
        }
        sb.AppendLine($"  final: {finalTexto}");

        // ── comparação ──
        AICasa casaFinal = temFinal ? final.Casa : AICasa.Reposicionar;
        bool diverge = !TryCasaDaAcao(codigoFez, out AICasa casaCodigo) || casaCodigo != casaFinal;
        sb.AppendLine($"  código fez: {DescreverAcao(codigoFez)}{(diverge ? "    << DIVERGE" : string.Empty)}");
        sb.AppendLine();

        AIQuestionarioLog.Escrever(sb.ToString());
    }

    /// <summary>O motivo é a agenda do papel, não o efeito colateral (§5.1).</summary>
    private static string MotivoCapturador(AICasa casa, bool temAncora)
    {
        switch (casa)
        {
            case AICasa.Capturar: return "capturar";
            case AICasa.Embarcar: return "carona até o alvo";
            case AICasa.Reposicionar: return temAncora ? "avançar para capturar" : "confirmar posição";
            case AICasa.Mirar: return "abrir passagem";
            default: return casa.ToString();
        }
    }
}
