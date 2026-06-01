public enum ConstructionSector
{
    None    = -1,
    Alpha   = 0,
    Bravo   = 1,
    Charlie = 2,
    Delta   = 3,
    Echo    = 4,
    Foxtrot = 5,
    Golf    = 6,
    Hotel   = 7,
    India   = 8,
    Juliet  = 9,
    Kilo    = 10,
    Lima    = 11,
    Mike    = 12,
    November= 13,
    Oscar   = 14,
    Papa    = 15,
    Quebec  = 16,
    Romeo   = 17,
    Tango   = 18,

    // Setores de base — um por jogador, atribuidos pelo designer do mapa (ordem arbitraria).
    Base1   = 100,
    Base2   = 101,
    Base3   = 102,
    Base4   = 103,
}

public static class ConstructionSectorHelper
{
    public static bool IsBase(ConstructionSector sector)
    {
        return sector == ConstructionSector.Base1
            || sector == ConstructionSector.Base2
            || sector == ConstructionSector.Base3
            || sector == ConstructionSector.Base4;
    }

    /// <summary>Versao string para validacoes em editors e save data.</summary>
    public static bool IsBaseName(string name)
    {
        return name == "Base1" || name == "Base2" || name == "Base3" || name == "Base4";
    }
}
