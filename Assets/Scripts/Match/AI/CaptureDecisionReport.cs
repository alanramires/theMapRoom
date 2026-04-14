using System.Collections.Generic;
using UnityEngine;

public enum CandidateStatus
{
    NotGenerated,     // gerador não encontrou ação válida para esse papel
    InvalidNoAction,  // gerador rodou mas não produziu batch válido
    Defeated,         // candidato válido, perdeu no score
    Chosen            // vencedor final
}

[System.Serializable]
public class ScoreBreakdown
{
    public float captureProximity; // progresso em direção ao alvo
    public float combatValue;      // valor do combate (+ bônus se blocker)
    public float cohesion;         // proximidade com aliados
    public float deviation;        // penalidade por desvio da missão
    public float safety;           // penalidade por exposição a inimigos

    public float Total => captureProximity + combatValue + cohesion - deviation + safety;
}

[System.Serializable]
public class CandidateReport
{
    public string role;
    public string label;
    public CandidateStatus status;
    public ScoreBreakdown score;   // null se NotGenerated ou InvalidNoAction
}

[System.Serializable]
public class CaptureObjectiveSnapshot
{
    // Dados crus
    public Vector3Int targetCell;
    public float distanceToTarget;
    public bool targetReachable;
    public int blockerInstanceId = -1;

    // Display
    public string targetBuildingName;
    public string blockerName;
    public bool HasBlocker => blockerInstanceId >= 0;
}

[System.Serializable]
public class CaptureDecisionReport
{
    public int unitInstanceId;
    public string unitName;
    public int turn;
    public string role = "Capturador";

    public CaptureObjectiveSnapshot objective;
    public List<CandidateReport> candidates = new List<CandidateReport>();

    // Escolha final
    public string chosenRole;
    public string actionDescription; // ex: "move → (-1,5,0)" / "attack Soldado#3 @ (2,3)" / "capture @ (2,3)"
    public string reasonSummary;
}
