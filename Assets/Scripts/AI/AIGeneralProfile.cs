using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Profile", fileName = "AIProfile")]
public class AIGeneralProfile : ScriptableObject
{
    public string profileName = "General";
    [Tooltip("Time preferido para este general. Neutral = sem vinculacao.")]
    public TeamId preferedTeamAssignment = TeamId.Neutral;
    [Tooltip("Dados de composicao/compra usados por este general.")]
    public AIData aiData;
}



