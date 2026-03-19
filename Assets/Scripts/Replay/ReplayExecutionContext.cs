using System;

[Serializable]
public class ReplayExecutionContext
{
    public MatchController MatchController;
    public UnitManager UnitManager;
    public ConstructionManager ConstructionManager;
    public FogOfWarController FogOfWarController;
    public AnimationManager AnimationManager;
    public CursorController CursorController;
    public bool IsReplayMode;
    public bool AnimateCombatPresentation;
    public ReplayVisionMode VisionMode = ReplayVisionMode.Omniscient;
    public TeamId ObserverTeam = TeamId.Neutral;
}
