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
// ---------------------------------------------------------------------------

[Serializable]
public class JogadasLog
{
    public List<Jogada> jogadas = new List<Jogada>();
    public int proximoId = 1;

    public void Registrar(Jogada j)
    {
        j.jogadaId = proximoId++;
        jogadas.Add(j);
        Debug.Log($"[Jogadas] #{j.jogadaId} T{j.turno} team={j.team} {j.acao} uid={j.uid} ({j.unidadeSigla}) de ({j.cx},{j.cy}) para ({j.dx},{j.dy})");
    }
}

[Serializable]
public class Jogada
{
    public int    jogadaId;
    public int    turno;
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

    // Observação contextual gravada no momento da jogada (ex.: progresso de captura "10/20",
    // "capturado", "reparado"). Preenchida hoje só para Capturar.
    public string obs;

    public bool TemCoordenada => cx != 0 || cy != 0;
    public bool TemDestino    => dx != 0 || dy != 0;
    public Vector3Int Coord   => new Vector3Int(cx, cy, 0);
    public Vector3Int Destino => new Vector3Int(dx, dy, 0);
}
