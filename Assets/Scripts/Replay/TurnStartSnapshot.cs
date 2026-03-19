using System;
using System.Collections.Generic;

[Serializable]
public class TurnStartSnapshot
{
    public int TurnNumber;
    public TeamId ActiveTeam;
    // Estrutura para estado completo das unidades (ativas e inativas).
    public List<UnitSaveData> Units = new List<UnitSaveData>();
    // Estrutura para estado completo das construcoes.
    public List<ConstructionSaveData> Constructions = new List<ConstructionSaveData>();
    // Estado de match (turno, equipe ativa, economia e vitoria).
    public MatchStateSaveData MatchState = new MatchStateSaveData();
}
