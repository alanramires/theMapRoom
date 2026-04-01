[System.Obsolete("Legacy base kept only for project file compatibility. Use AIUnitProfile.")]
public abstract class AIUnitBehavior
{
    public abstract bool CanHandle(UnitManager unit);

    public abstract AIInfantryBehaviorDecision BuildIntent(
        AIPlayerController controller,
        in AIInfantryBehaviorContext context);
}
