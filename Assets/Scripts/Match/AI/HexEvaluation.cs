using UnityEngine;

public enum CandidateType
{
    CaptureNow,
    CaptureAdvance,
    UsefulCombat,
    Cohesion,
    PrudentFow,
    UnderRepair   // unidade em modo de manutenção → vai para prédio aliado
}

public struct HexEvaluation
{
    public Vector3Int   cell;
    public CandidateType type;
    public float        captureProximity;
    public float        combatValue;
    public float        cohesion;
    public float        deviation;
    public float        safety;
    public float        total;
    public string       actionSummary;   // ex: "move → (-1,5,0)" / "capture @ (2,3)"
    public string       combatSummary;   // ex: "Soldado#6 @ (-3,0)" ou "—" (saída real do sensor)
    public bool         isChosen;
}
