public partial class AIController
{
    /// <summary>
    /// O pedido de carona que o PressaoCaronaService usa: o MESMO do capturador
    /// (BuildCapturerRideRequest), com o objetivo resolvido da alocacao do plano.
    /// Montar outro pedido faria a pressao responder outra pergunta e cair em outra
    /// entrada do cache do QueroCarona.
    /// </summary>
    internal QueroCaronaRequest BuildRidePressureRequest(
        UnitManager unit, TeamObjectivePlan plan, out bool comPlano)
    {
        SectorObjective assigned = plan != null ? ResolveAssignedObjective(unit, plan) : null;
        comPlano = assigned != null;
        return BuildCapturerRideRequest(unit, assigned);
    }
}
