using System.Collections.Generic;
using UnityEngine;

public enum AIPlanRole
{
    Capture = 0,
    Escort = 1,
    Assault = 2,
    Support = 3,
    Artillery = 4
}

public static class AIPlanRoleExtensions
{
    public static string ToDebugLabel(this AIPlanRole role)
    {
        switch (role)
        {
            case AIPlanRole.Capture: return "capture";
            case AIPlanRole.Escort: return "escort";
            case AIPlanRole.Assault: return "assault";
            case AIPlanRole.Support: return "support";
            case AIPlanRole.Artillery: return "artillery";
            default: return role.ToString();
        }
    }
}

// Resultado do planejamento para um plano ativo num turno.
// Criado pelo AIPlanEvaluator, copiado para AISnapshot.
public class AIPlanIntent
{
    public ConstructionSector Sector;
    public string DisplayName;
    // Simbolo visual de debug usado no HUD da unidade.
    public string BadgeSymbol;

    // Alvo de captura resolvido para este plano (construcao capturavel no setor)
    public bool HasCaptureTarget;
    public Vector3Int CaptureTargetCell;
    public string CaptureTargetLabel;

    // Inimigo visivel mais proximo do setor (pode ser null)
    public UnitManager SectorEnemy;

    // Score de risco tatico calculado no planner (usado no log/debug).
    public int TacticalRiskScore;

    // Motivo resumido pelo qual este plano ficou ativo no turno.
    public string SelectionReason;

    // Estado de controle do setor para debug/persistencia.
    public bool SectorClear;
    public TeamId ControllingTeam = TeamId.Neutral;

    // Demanda planejada por papel para este setor.
    public int DesiredCaptureCount;
    public int DesiredEscortCount;
    public int DesiredArtilleryCount;
    public int DesiredTransportCount;
    public int DesiredSupportCount;

    // Unidades designadas a este plano neste turno
    public List<AIPlanAssignment> Assignments = new List<AIPlanAssignment>();
}

// Designacao de uma unidade a um papel dentro de um plano no turno.
public class AIPlanAssignment
{
    public int UnitInstanceId;
    public AIPlanRole Role = AIPlanRole.Assault;
    public AIPlanIntent Intent;

    // Alvo de captura individual da unidade dentro do setor do plano.
    // Permite que dois capturadores do mesmo setor escolham construcoes diferentes.
    public bool HasPlannedCaptureTarget;
    public Vector3Int PlannedCaptureCell;
    public string PlannedCaptureLabel;
}

