using System.Collections.Generic;
using UnityEngine;

// Resultado do planejamento para um plano ativo num turno.
// Criado pelo AIPlanEvaluator, copiado para AISnapshot.
public class AIPlanIntent
{
    // Plano ScriptableObject que originou este intent (null para planos dinâmicos)
    public AIPlanData Plan;
    public ConstructionSector Sector;
    // Nome legível — preenchido tanto para planos fixos quanto para planos gerados dinamicamente
    public string DisplayName;

    // Alvo de captura resolvido para este plano (construção capturável no setor)
    public bool HasCaptureTarget;
    public Vector3Int CaptureTargetCell;
    public string CaptureTargetLabel;

    // Inimigo visível mais próximo do setor (pode ser null)
    public UnitManager SectorEnemy;

    // Unidades designadas a este plano neste turno
    public List<AIPlanAssignment> Assignments = new List<AIPlanAssignment>();
}

// Designação de uma unidade a um papel dentro de um plano no turno.
public class AIPlanAssignment
{
    public int UnitInstanceId;
    public string Role;
    public AIPlanIntent Intent;
}
