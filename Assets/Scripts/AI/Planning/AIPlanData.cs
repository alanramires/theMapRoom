using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class AIPlanParticipantDefinition : ISerializationCallbackReceiver
{
    [Tooltip("Unidade especifica para o papel. Opcional.")]
    public UnitData unitData;

    [Tooltip("Papel tatico no plano.")]
    public AIPlanRole role = AIPlanRole.Assault;

    [FormerlySerializedAs("role")]
    [SerializeField, HideInInspector] private string legacyRole = string.Empty;

    [SerializeField, HideInInspector] private bool legacyRoleMigrated;

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        if (legacyRoleMigrated || string.IsNullOrWhiteSpace(legacyRole))
            return;

        if (TryParseLegacyRole(legacyRole, out AIPlanRole parsed))
            role = parsed;

        legacyRoleMigrated = true;
        legacyRole = string.Empty;
    }

    private static bool TryParseLegacyRole(string value, out AIPlanRole parsed)
    {
        parsed = AIPlanRole.Assault;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "capturador":
            case "capture":
                parsed = AIPlanRole.Capture;
                return true;

            case "escolta blindada":
            case "escolta leve":
            case "escort":
                parsed = AIPlanRole.Escort;
                return true;

            case "apoio artilharia":
            case "artillery":
                parsed = AIPlanRole.Artillery;
                return true;

            case "transporte escolta":
            case "support":
                parsed = AIPlanRole.Support;
                return true;

            case "assault":
                parsed = AIPlanRole.Assault;
                return true;

            default:
                if (Enum.TryParse(value, true, out AIPlanRole enumRole))
                {
                    parsed = enumRole;
                    return true;
                }
                return false;
        }
    }
}

[CreateAssetMenu(menuName = "Game/AI/AI Plan Data", fileName = "AIPlan_")]
public class AIPlanData : ScriptableObject
{
    [Header("Identity")]
    public string planId = string.Empty;
    public string displayName = string.Empty;

    [Header("Target")]
    public ConstructionSector targetSector = ConstructionSector.BaseTeam;

    [Header("Rules")]
    [Tooltip("Condicoes de ativacao - todas devem ser verdadeiras para o plano ser ativado.")]
    public List<PlanCondition> activationConditions = new List<PlanCondition>();

    [Header("Participants")]
    public List<AIPlanParticipantDefinition> participants = new List<AIPlanParticipantDefinition>();

    private void OnValidate()
    {
        if (activationConditions == null)
            activationConditions = new List<PlanCondition>();
        if (participants == null)
            participants = new List<AIPlanParticipantDefinition>();
    }
}
