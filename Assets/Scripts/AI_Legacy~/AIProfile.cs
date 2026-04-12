using UnityEngine;

// Base para todos os perfis de IA.
// Cada perfil implementa como avaliar a postura e (futuramente) como construir o plano de turno.
// ScriptableObject: subclasses concretas podem ser criadas como assets e atribuidas a AIGeneralProfile.
public abstract class AIProfile : ScriptableObject
{
    public abstract AIStance EvaluateStance(AISnapshot snapshot, BattleStanceDatabase stanceDatabase);
}
