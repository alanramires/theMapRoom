public enum GameUnitClass
{
    Infantry = 0,
    Vehicle = 1,
    Artillery = 2,
    Armored = 3,
    Jet = 4,
    Helicopter = 5,
    Plane = 6,
    Submarine = 7,
    Ship = 8
}

public static class GameUnitClassLabels
{
    public static string GetPortugueseName(GameUnitClass unitClass)
    {
        switch (unitClass)
        {
            case GameUnitClass.Infantry: return "Infantaria";
            case GameUnitClass.Vehicle: return "Veículos";
            case GameUnitClass.Armored: return "Blindados";
            case GameUnitClass.Artillery: return "Artilharia";
            case GameUnitClass.Jet: return "Caças";
            case GameUnitClass.Plane: return "Aviões";
            case GameUnitClass.Helicopter: return "Helicópteros";
            case GameUnitClass.Ship: return "Navios";
            case GameUnitClass.Submarine: return "Submarinos";
            default: return unitClass.ToString();
        }
    }
}
