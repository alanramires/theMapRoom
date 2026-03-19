using System;
using System.Collections.Generic;

[Serializable]
public class ReplayTurnRecord
{
    public int TurnNumber;
    public TeamId ActingTeam;
    public TurnStartSnapshot StartSnapshot;
    public List<ReplayStep> Steps = new List<ReplayStep>();
}
