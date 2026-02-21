using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Autonomy/Autonomy Data", fileName = "AutonomyData_")]
public class AutonomyData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;
    [Tooltip("Marca este perfil como aeronave para validacoes de autonomia.")]
    public bool isAircraft = false;
    [TextArea] public string description;

    [Header("Movement")]
    [Min(1)]
    [Tooltip("Multiplicador aplicado sobre o custo base de autonomia por hex caminhado.")]
    public int movementAutonomyMultiplier = 1;

    [Header("Turn Start Upkeep")]
    [Min(0)]
    [Tooltip("Consumo fixo de autonomia ao iniciar o turno em um dos dominios/alturas permitidos abaixo.")]
    public int turnStartUpkeep = 0;
    [Tooltip("Se vazio (<null>), nao aplica upkeep por inicio de turno.")]
    public List<AutonomyLayerMode> upkeepStartLayerModes = new List<AutonomyLayerMode>();

    public bool AppliesTurnStartUpkeep(Domain domain, HeightLevel heightLevel)
    {
        if (turnStartUpkeep <= 0)
            return false;
        if (upkeepStartLayerModes == null || upkeepStartLayerModes.Count == 0)
            return false;

        for (int i = 0; i < upkeepStartLayerModes.Count; i++)
        {
            AutonomyLayerMode mode = upkeepStartLayerModes[i];
            if (mode.domain == domain && mode.heightLevel == heightLevel)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        movementAutonomyMultiplier = Mathf.Max(1, movementAutonomyMultiplier);
        turnStartUpkeep = Mathf.Max(0, turnStartUpkeep);
        if (upkeepStartLayerModes == null)
            upkeepStartLayerModes = new List<AutonomyLayerMode>();
    }
}
