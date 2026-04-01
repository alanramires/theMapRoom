using System;
using System.Collections.Generic;
using UnityEngine;

public enum AIPlanKind
{
    Fixed = 0,
    Variable = 1
}

[Serializable]
public class AIPlanParticipantDefinition
{
    [Tooltip("Unidade especifica para o papel. Opcional.")]
    public UnitData unitData;

    [Tooltip("Classe preferida quando nao houver UnitData fixa.")]
    public GameUnitClass preferredClass = GameUnitClass.Infantry;

    [Tooltip("Papel tatico no plano (capturador, cobertura, transportador etc).")]
    public string role = string.Empty;
}

[CreateAssetMenu(menuName = "Game/AI/AI Plan Data", fileName = "AIPlan_")]
public class AIPlanData : ScriptableObject
{
    [Header("Identity")]
    public string planId = string.Empty;
    public string displayName = string.Empty;
    public AIPlanKind kind = AIPlanKind.Variable;

    [Header("Target")]
    public ConstructionSector targetSector = ConstructionSector.BaseTeam;

    [Header("Rules")]
    [TextArea]
    public string activationCondition = string.Empty;
    [TextArea]
    public string objectiveCompletedWhen = string.Empty;
    [TextArea]
    public string objectiveFailedWhen = string.Empty;

    [Header("Selection Criteria")]
    public List<string> selectionCriteria = new List<string>();

    [Header("Participants")]
    public List<AIPlanParticipantDefinition> participants = new List<AIPlanParticipantDefinition>();

    private void OnValidate()
    {
        if (selectionCriteria == null)
            selectionCriteria = new List<string>();
        if (participants == null)
            participants = new List<AIPlanParticipantDefinition>();
    }
}
