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

    // Setores de base — um por jogador. Nome 0-indexado pra casar com o slot (Base0 -> Slot 0 etc.);
    // a atribuicao slot<->base continua sendo o campo slotIndex da construcao (nome e so rotulo).
    // Os ints (100..103) sao preservados pra nao quebrar cenas/saves ja serializados por valor.
    Base0   = 100,
    Base1   = 101,
    Base2   = 102,
    Base3   = 103,
}

public static class ConstructionSectorHelper
{
    public static bool IsBase(ConstructionSector sector)
    {
        return sector == ConstructionSector.Base0
            || sector == ConstructionSector.Base1
            || sector == ConstructionSector.Base2
            || sector == ConstructionSector.Base3;
    }

    /// <summary>Versao string para validacoes em editors e save data.</summary>
    public static bool IsBaseName(string name)
    {
        return name == "Base0" || name == "Base1" || name == "Base2" || name == "Base3";
    }
}
