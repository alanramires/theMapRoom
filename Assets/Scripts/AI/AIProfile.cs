// Base para todos os perfis de IA.
// Cada perfil implementa como avaliar a postura e (futuramente) como construir o plano de turno.
public abstract class AIProfile
{
    public abstract AIStance EvaluateStance(AISnapshot snapshot, BattleStanceDatabase stanceDatabase);
}
