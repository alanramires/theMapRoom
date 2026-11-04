using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produtor de diagnostico do combate (ver DiagnosticoLog.cs para o contrato).
///
/// Tres fatos:
///   decisao.status            quantas decisoes de ataque a IA tomou por status,
///                             por rodada e slot (agregado: so contagem)
///   decisao.sem_opcao_sensor  a IA avaliou atacar daqui e o PodeMirar recusa
///                             (agregado por atacante, com o primeiro exemplo)
///   combate.resolvido         o combate executado, com a PREVISAO que a IA fez
///                             para o mesmo par e celula ao lado; diverge = Aviso
///
/// A previsao fica so em memoria ate a execucao: decisao avaliada e nao
/// executada nao vira linha (seriam milhares por turno).
/// </summary>
public static class CombatDiagnostico
{
    public const string OpDecisaoStatus = "decisao.status";
    public const string OpSemOpcaoSensor = "decisao.sem_opcao_sensor";
    public const string OpCombateResolvido = "combate.resolvido";

    public const int CodigoSemPrevisao = 0;
    public const int CodigoPrevisaoConfere = 1;
    public const int CodigoPrevisaoDiverge = 2;

    private struct Previsao
    {
        public int rodada;
        public int atacanteHpDepois;
        public int alvoHpDepois;
        public int atacanteDpq;
        public int alvoDpq;
        public WeaponData arma;
        public WeaponData revide;
    }

    private static readonly Dictionary<(int atacante, int alvo, int x, int y), Previsao> previsoes =
        new Dictionary<(int, int, int, int), Previsao>();
    private static int previsoesRodada = -1;
    private static DiagnosticoManager previsoesDono;

    // Chamado pelo CombatEvaluationService quando simula pela opcao do sensor.
    public static void RegistrarPrevisao(CombatEvaluationRequest request, in CombatEvaluationResult result)
    {
        if (request == null
            || request.Attacker == null
            || request.Target == null
            || !result.IsValid
            || result.WeaponSelectionMode != CombatWeaponSelectionMode.CanonicalSensorOption
            || !DiagnosticoManager.Ativo)
        {
            return;
        }

        int rodada = PrepararPrevisoes();
        Vector3Int cell = request.AttackCell;
        previsoes[(request.Attacker.InstanceId, request.Target.InstanceId, cell.x, cell.y)] = new Previsao
        {
            rodada = rodada,
            atacanteHpDepois = result.Simulation.attackerHpAfter,
            alvoHpDepois = result.Simulation.defenderHpAfter,
            atacanteDpq = result.AttackerDpq.Points,
            alvoDpq = result.DefenderDpq.Points,
            arma = result.AttackWeapon,
            revide = result.CounterWeapon
        };
    }

    // Chamado uma vez por decisao de ataque avaliada.
    public static void RegistrarDecisao(CombatEvaluationRequest request, in AttackDecisionResult result)
    {
        if (request == null || request.Attacker == null || !DiagnosticoManager.Ativo)
            return;

        DiagnosticoManager manager = DiagnosticoManager.EnsureInstance();
        int rodada = manager.RodadaAtual;
        int slot = request.Attacker.SlotIndex;
        int codigo = (int)result.Status;

        if (DiagnosticoManager.TryGetAgregada(OpDecisaoStatus, rodada, slot, 0, codigo, out DiagnosticoEntrada status))
        {
            status.contagem++;
        }
        else
        {
            DiagnosticoManager.RegistrarAgregada(new DiagnosticoEntrada
            {
                rodada = rodada,
                slot = slot,
                severidade = DiagnosticoSeveridade.Depuracao,
                categoria = "decisao",
                operacao = OpDecisaoStatus,
                codigo = codigo,
                motivo = result.Status.ToString()
            }.Campo("permitido", result.IsAllowed ? "sim" : "nao"));
        }

        if (result.Status != AttackDecisionStatus.BlockedNoSensorOption)
            return;

        int uid = request.Attacker.InstanceId;
        if (DiagnosticoManager.TryGetAgregada(OpSemOpcaoSensor, rodada, slot, uid, codigo, out DiagnosticoEntrada semOpcao))
        {
            semOpcao.contagem++;
            return;
        }

        Vector3Int cell = request.AttackCell;
        DiagnosticoManager.RegistrarAgregada(new DiagnosticoEntrada
        {
            rodada = rodada,
            slot = slot,
            severidade = DiagnosticoSeveridade.Depuracao,
            categoria = "decisao",
            operacao = OpSemOpcaoSensor,
            uid = uid,
            sigla = Sigla(request.Attacker),
            uid2 = request.Target != null ? request.Target.InstanceId : 0,
            sigla2 = Sigla(request.Target),
            cx = cell.x,
            cy = cell.y,
            codigo = codigo,
            motivo = "PodeMirar nao oferece o alvo a partir desta celula (alvo e celula = primeiro exemplo da rodada)"
        });
    }

    // Chamado pela execucao (TurnStateManager.Combat) com o resultado da formula.
    public static void RegistrarCombate(
        PodeMirarTargetOption option,
        UnitManager atacante,
        UnitManager alvo,
        int atacanteHpAntes,
        int alvoHpAntes,
        in CombatFormulaResult f,
        bool revideExecutado,
        int atacanteDpq,
        int alvoDpq,
        string atacanteDpqFonte,
        string alvoDpqFonte)
    {
        if (option == null || atacante == null || alvo == null || !DiagnosticoManager.Ativo)
            return;

        Vector3Int cell = atacante.CurrentCellPosition;
        cell.z = 0;
        int rodada = PrepararPrevisoes();
        var key = (atacante.InstanceId, alvo.InstanceId, cell.x, cell.y);
        bool temPrevisao = previsoes.TryGetValue(key, out Previsao p) && p.rodada == rodada;
        if (temPrevisao)
            previsoes.Remove(key);

        WeaponData revideReal = revideExecutado ? option.defenderCounterWeapon : null;
        var entrada = new DiagnosticoEntrada
        {
            rodada = rodada,
            slot = atacante.SlotIndex,
            categoria = "combate",
            operacao = OpCombateResolvido,
            uid = atacante.InstanceId,
            sigla = Sigla(atacante),
            uid2 = alvo.InstanceId,
            sigla2 = Sigla(alvo),
            cx = cell.x,
            cy = cell.y
        };

        entrada.Campo("arma", NomeArma(option.weapon))
               .Campo("revide", revideExecutado ? NomeArma(revideReal) : "-")
               .Campo("distancia", option.distance)
               .Campo("hp_atacante", $"{atacanteHpAntes}->{f.attackerHpAfter}")
               .Campo("hp_alvo", $"{alvoHpAntes}->{f.defenderHpAfter}")
               .Campo("dpq_atacante", $"{atacanteDpq} ({atacanteDpqFonte})")
               .Campo("dpq_alvo", $"{alvoDpq} ({alvoDpqFonte})")
               .Campo("outcome", $"{f.attackerOutcome}/{f.defenderOutcome}");

        if (!temPrevisao)
        {
            entrada.severidade = DiagnosticoSeveridade.Depuracao;
            entrada.codigo = CodigoSemPrevisao;
            entrada.motivo = "sem previsao (jogador humano, ou a IA nao simulou este par desta celula)";
            DiagnosticoManager.Registrar(entrada);
            return;
        }

        entrada.Campo("previsto_hp_atacante", $"{atacanteHpAntes}->{p.atacanteHpDepois}")
               .Campo("previsto_hp_alvo", $"{alvoHpAntes}->{p.alvoHpDepois}")
               .Campo("previsto_dpq", $"{p.atacanteDpq}/{p.alvoDpq}")
               .Campo("previsto_arma", NomeArma(p.arma))
               .Campo("previsto_revide", p.revide != null ? NomeArma(p.revide) : "-");

        var divergencias = new List<string>();
        if (p.atacanteHpDepois != f.attackerHpAfter || p.alvoHpDepois != f.defenderHpAfter)
            divergencias.Add("hp");
        if (p.atacanteDpq != atacanteDpq || p.alvoDpq != alvoDpq)
            divergencias.Add("dpq");
        if (p.arma != option.weapon)
            divergencias.Add("arma");
        if (p.revide != revideReal)
            divergencias.Add("revide");

        bool confere = divergencias.Count == 0;
        entrada.severidade = confere ? DiagnosticoSeveridade.Depuracao : DiagnosticoSeveridade.Aviso;
        entrada.codigo = confere ? CodigoPrevisaoConfere : CodigoPrevisaoDiverge;
        entrada.motivo = confere ? "previsto = realizado" : $"DIVERGE: {string.Join(", ", divergencias)}";
        DiagnosticoManager.Registrar(entrada);
    }

    // As previsoes vivem uma rodada e uma partida: trocou qualquer uma, zera.
    private static int PrepararPrevisoes()
    {
        DiagnosticoManager manager = DiagnosticoManager.EnsureInstance();
        int rodada = manager.RodadaAtual;
        if (rodada != previsoesRodada || manager != previsoesDono)
        {
            previsoes.Clear();
            previsoesRodada = rodada;
            previsoesDono = manager;
        }
        return rodada;
    }

    private static string Sigla(UnitManager unit)
    {
        if (unit == null)
            return "-";
        return unit.TryGetUnitData(out UnitData data) && data != null && !string.IsNullOrEmpty(data.apelido)
            ? data.apelido
            : unit.name;
    }

    private static string NomeArma(WeaponData weapon)
    {
        if (weapon == null)
            return "-";
        return !string.IsNullOrWhiteSpace(weapon.displayName) ? weapon.displayName : weapon.name;
    }
}
