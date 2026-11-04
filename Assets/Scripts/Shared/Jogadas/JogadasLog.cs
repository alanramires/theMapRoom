using System;
using System.Collections.Generic;
using UnityEngine;

// ---------------------------------------------------------------------------
// Registro de jogadas da partida — gerado por humanos e pela IA.
// Salvo como sidecar: save.jogadas.json
// A IA usa as últimas N rodadas para memória entre turnos.
//
// Ações implementadas:
//   ServicoComando  — poder de fase do jogador, sem coordenada
//   Compra          — unidade comprada em cx,cy com uid atribuído
//   Mover           — unidade uid de cx,cy para dx,dy
//   Capturar        — unidade uid de cx,cy captura dx,dy
//   Embarque        — unidade uid de cx,cy embarca no transporte uid2 em dx,dy
//   Ataque          — unidade uid de cx,cy ataca unidade uid2 em dx,dy
//   Desembarque     — transporte uid se move de cx,cy para dx,dy (drop point)
//   Desembarcando   — passageiro uid sai do drop point cx,cy e pousa em dx,dy
//   Fusao           — unidade uid de cx,cy funde com uid2 em dx,dy
//   Estacionario    — unidade uid segura posição em cx,cy sem mover
//   Suprir          — supressor uid se move de cx,cy para dx,dy
//   Suprindo        — alvo uid recebe suprimento em cx,cy (sem mover)
//   Destruir        — unidade uid removida em cx,cy pelo comando destroy unit
//   PartidaIniciada — modo, dificuldade, regras e slots (cor + IA) no inicio da partida
//   PartidaEncerrada— vencedor (slot em team, -1 = nenhum), motivo e modo
//   Renda           — credito de inicio de turno do slot (valor)
//   Autonomia       — consumo de combustivel do inicio de turno (fuelAntes/fuelDepois)
//   PousoEmergencia — aeronave uid pousou por combustivel zerado em cx,cy
//   PaneSeca        — aeronave uid caiu por combustivel zerado em cx,cy
//
// Fatos para medalhas: ver docs/conquistas/sensores.md. Registrar FATOS, nunca
// conclusoes, e so o que foi confirmado (Neutral / resolucao do inicio de turno).
// ---------------------------------------------------------------------------

[Serializable]
public class JogadasLog
{
    public static event Action<Jogada> OnJogadaRegistrada;

    public List<Jogada> jogadas = new List<Jogada>();
    public int proximoId = 1;

    public void Registrar(Jogada j)
    {
        if (j != null && !j.hasRepairState && j.uid > 0)
        {
            UnitManager unit = UnitManager.AllActive.Find(u => u != null && u.InstanceId == j.uid);
            if (unit != null)
            {
                j.hasRepairState = true;
                j.repairBefore = unit.IsUnderRepair;
                j.repairAfter = unit.IsUnderRepair;
            }
        }
        j.jogadaId = proximoId++;
        jogadas.Add(j);
        OnJogadaRegistrada?.Invoke(j);
        if (JogadasManager.ShouldLog)
            Debug.Log($"[Jogadas] #{j.jogadaId} T{j.turno} team={j.team} {j.acao} uid={j.uid} ({j.unidadeSigla}) de ({j.cx},{j.cy}) para ({j.dx},{j.dy})");
    }
}

[Serializable]
public class Jogada
{
    public int    jogadaId;
    public int    turno;
    // SLOT de quem agiu (nao a cor: o nome e historico). Todo registrador passa
    // SlotIndex / ActingSlotIndex. Medalha e memoria da IA comparam slot com slot.
    public int    team;
    public string acao;         // "ServicoComando" | "Compra" | "Mover" | "Capturar" | "Embarque" | "Ataque" | "Desembarque" | "Desembarcando" | "Fusao" | "Estacionario" | "Suprir" | "Suprindo" | "Destruir"

    // Coordenada de origem — null/zero quando a ação não tem localização (ex: ServicoComando)
    public int    cx;
    public int    cy;

    // Destino — preenchido em Mover/Capturar/Atacar
    public int    dx;
    public int    dy;

    // Campos de identificação de unidades
    public string unidadeSigla; // ex: "SD", "MBT", "APC"
    public int    uid;          // instanceId da unidade atuante (0 se não aplicável)
    public int    uid2;         // instanceId da unidade alvo (transporter em Embarque, alvo em Ataque)

    // Resultado estruturado de combate. O bool preserva compatibilidade com logs antigos.
    public bool   hasCombatResult;
    public bool   hasHpState;
    public string unidadeSigla2;
    public int    team2;        // SLOT do alvo (ver team)
    public int    hpAntes;
    public int    hpDepois;
    public int    hp2Antes;
    public int    hp2Depois;
    public bool   hasAttackIntel;
    public WeaponCategory attackWeaponCategory;
    public WeaponTrajectoryType attackTrajectory;
    public bool   attackerVisibleToDefender;
    public int    defenderCost;
    public int    defenderEliteLevel;
    public List<CombatCargoResult> combatCargo = new List<CombatCargoResult>();

    public bool   hasRepairState;
    public bool   repairBefore;
    public bool   repairAfter;

    public bool   hasServiceResult;
    public string serviceSource;
    public string serviceId;
    public int    serviceCost;
    public int    serviceHpGain;
    public int    serviceFuelGain;
    public int    serviceAmmoGain;

    // Observação contextual gravada no momento da jogada (ex.: progresso de captura "10/20",
    // "capturado", "reparado"). Preenchida hoje só para Capturar.
    public string obs;

    // Fatos estruturados (medalhas, ver docs/conquistas/sensores.md).
    public bool   hasFuelState;
    public int    fuelAntes;
    public int    fuelDepois;
    public int    valor;          // Renda: credito; Compra: custo
    public string motivo;         // PartidaEncerrada: VictoryReason
    public string modo;           // Partida*: "aula" | "campanha" | "avulsa"
    public string dificuldade;    // PartidaIniciada: AIDifficulty
    public string regras;         // PartidaIniciada: GameSetupPreset
    public List<int>  slotsTime = new List<int>();   // PartidaIniciada: cor de cada slot
    public List<bool> slotsIA   = new List<bool>();  // PartidaIniciada: slot controlado pela IA

    // Capturar: o predio e o dono ANTES da captura (fato 4).
    public string predioId;
    public bool   predioQG;
    public int    donoAnterior = -1;   // slot; -1 = neutro
    public bool   capturaConcluida;

    // Ataque: posicao dos dois lados no combate (fato 5).
    public bool   hasDpq;
    public int    dpqAtacante;
    public int    dpqDefensor;
    public string posicaoAtacante;    // ex.: "Terreno: Montanha"
    public string posicaoDefensor;

    public bool TemCoordenada => cx != 0 || cy != 0;
    public bool TemDestino    => dx != 0 || dy != 0;
    public Vector3Int Coord   => new Vector3Int(cx, cy, 0);
    public Vector3Int Destino => new Vector3Int(dx, dy, 0);
}

[Serializable]
public class CombatCargoResult
{
    public int rootUid;
    public int parentUid;
    public int depth;
    public int uid;
    public int team;
    public string sigla;
    public int hpAntes;
    public int hpDepois;
    public int cost;
    public int eliteLevel;
    public GameUnitClass unitClass;
    public string cause;
}
