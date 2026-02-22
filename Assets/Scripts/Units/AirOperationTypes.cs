public enum LandingClass
{
    None = 0,
    RunwayOnly = 1,
    VTOL = 2,
    NavalRunway = 3
}

public enum TakeoffMode
{
    None = 0,
    Runway = 1,
    VTOL = 2,
    Naval = 3
}

public enum LandingProcedure
{
    Instant = 0,
    RequiresStopped = 1,
    RunwayApproach = 2
}

public enum TakeoffProcedure
{
    InstantToPreferredHeight = 0,
    RunwayRoll1HexEndAirLow = 1,
    ShortRoll0to1HexEndAirLow = 2
}

public enum LandingSurface
{
    None = 0,
    AirportRunway = 1,
    RoadRunway = 2,
    FlatGround = 3
}

public enum DockingSurface
{
    None = 0,
    PortDock = 1,
    BeachDock = 2,
    OpenSeaDock = 3
}
