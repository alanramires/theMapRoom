using System.Collections.Generic;
using UnityEngine;

// Resultado do planejamento para um plano ativo num turno.
// Criado pelo AIPlanEvaluator, copiado para AISnapshot.
public class AIPlanIntent
{
    // Plano ScriptableObject que originou este intent (null para planos dinamicos)
    public AIPlanData Plan;
    public ConstructionSector Sector;
    // Nome legivel - preenchido tanto para planos fixos quanto para planos gerados dinamicamente
    public string DisplayName;

    // Alvo de captura resolvido para este plano (construcao capturavel no setor)
    public bool HasCaptureTarget;
    public Vector3Int CaptureTargetCell;
    public string CaptureTargetLabel;

    // Inimigo visivel mais proximo do setor (pode ser null)
    public UnitManager SectorEnemy;

    // Score de risco tatico calculado no planner (usado no log/debug).
    // 0 = nao calculado/nao aplicavel (ex: planos fixos).
    public int TacticalRiskScore;

    // Unidades designadas a este plano neste turno
    public List<AIPlanAssignment> Assignments = new List<AIPlanAssignment>();
}

// Designacao de uma unidade a um papel dentro de um plano no turno.
public class AIPlanAssignment
{
    public int UnitInstanceId;
    public string Role;
    public AIPlanIntent Intent;

    // Alvo de captura individual da unidade dentro do setor do plano.
    // Permite que dois capturadores do mesmo setor escolham construcoes diferentes.
    public bool HasPlannedCaptureTarget;
    public Vector3Int PlannedCaptureCell;
    public string PlannedCaptureLabel;
}
