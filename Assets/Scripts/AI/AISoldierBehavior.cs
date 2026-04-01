[System.Obsolete("Legacy behavior kept only for project file compatibility. Use AIUnitProfile.")]
public sealed class AISoldierBehavior : AIUnitBehavior
{
    public override bool CanHandle(UnitManager unit) => false;

    public override AIInfantryBehaviorDecision BuildIntent(
        AIPlayerController controller,
        in AIInfantryBehaviorContext context)
    {
        return AIInfantryBehaviorDecision.None;
    }
}
