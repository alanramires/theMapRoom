using System.Collections.Generic;

/// <summary>
/// Rotulo de setor estrategico. Dois blocos convivem no mesmo enum de proposito:
///
///   0..99    setores de campanha (alfabeto OTAN 0..25, alfabeto grego 30..51).
///   100..119 setores de BASE (um por slot de jogador) — faixa reservada.
///
/// Os ints sao contrato de serializacao (cenas, prefabs e saves gravam o VALOR).
/// Nunca reaproveitar um numero ja usado; para acrescentar nomes, use os buracos
/// no fim de cada bloco.
///
/// A ordem de DECLARACAO aqui nao decide nada para quem le o dado: quem manda na
/// ordem EXIBIDA e ConstructionSectorOrder (Assets/Editor) — as bases vao pro topo
/// por la. Editor que precise ler ou gravar um destes le e grava o VALOR
/// (SerializedProperty.intValue). O indice do Unity (enumValueIndex) NAO e o indice
/// de Enum.GetValues neste enum, e a diferenca e silenciosa: gravava o setor vizinho
/// e mostrava de volta o rotulo escolhido. Um QG marcado "Base0" foi parar em Omega
/// assim, e so o SectorManager viu.
///
/// ARMADILHA PERMANENTE: default(ConstructionSector) e ALPHA, nao None — Alpha vale
/// 0 e None vale -1, e renumerar quebraria toda cena e save ja gravados. Entao
/// "sem setor" se escreve ConstructionSector.None, sempre, em campo, em retorno e em
/// comparacao. Quem usou `!= default` como "tem vizinho" apagou Alpha do grafo de
/// setores sem que nada reclamasse.
/// </summary>
public enum ConstructionSector
{
    // ── Bloco de BASE (100..119) ──────────────────────────────────────────
    // Um por jogador. Nome 0-indexado pra casar com o slot (Base0 -> Slot 0 etc.);
    // a atribuicao slot<->base continua sendo o campo slotIndex da construcao
    // (o nome e so rotulo). Os ints sao preservados pra nao quebrar cenas/saves.
    Base0   = 100,
    Base1   = 101,
    Base2   = 102,
    Base3   = 103,
    Base4   = 104,

    None    = -1,

    // ── Bloco OTAN (0..25) ────────────────────────────────────────────────
    // Sierra..Zulu entraram depois de Tango, entao ficam fora de ordem alfabetica
    // no dropdown. Alterar isso exigiria remapear cenas ja salvas — nao vale.
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
    Sierra  = 19,
    Uniform = 20,
    Victor  = 21,
    Whiskey = 22,
    Xray    = 23,
    Yankee  = 24,
    Zulu    = 25,

    // ── Bloco grego (30..51) ──────────────────────────────────────────────
    // Alpha e Delta ficaram de fora: ja existem no bloco OTAN com o mesmo nome.
    // O badge do HUD destes e de DUAS letras (ver ConstructionSectorHelper.GetBadge),
    // senao Bravo/Beta, Golf/Gamma, Papa/Pi etc. apareceriam com a mesma inicial.
    Beta    = 30,
    Gamma   = 31,
    Epsilon = 32,
    Zeta    = 33,
    Eta     = 34,
    Theta   = 35,
    Iota    = 36,
    Kappa   = 37,
    Lambda  = 38,
    Mu      = 39,
    Nu      = 40,
    Xi      = 41,
    Omicron = 42,
    Pi      = 43,
    Rho     = 44,
    Sigma   = 45,
    Tau     = 46,
    Upsilon = 47,
    Phi     = 48,
    Chi     = 49,
    Psi     = 50,
    Omega   = 51,
}

public static class ConstructionSectorHelper
{
    /// <summary>Primeiro valor da faixa reservada aos setores de base.</summary>
    public const int BaseRangeStart = 100;

    /// <summary>Ultimo valor da faixa reservada aos setores de base (inclusivo).</summary>
    public const int BaseRangeEnd = 119;

    /// <summary>Maior slot de base declarado no enum (Base0..Base4).</summary>
    public const int MaxDeclaredBaseSlot = 4;

    /// <summary>
    /// Um setor de base? Teste por FAIXA, nao por lista de valores: acrescentar
    /// Base5 no enum passa a funcionar sem tocar em nenhum dos ~40 call sites.
    /// </summary>
    public static bool IsBase(ConstructionSector sector)
    {
        int v = (int)sector;
        return v >= BaseRangeStart && v <= BaseRangeEnd;
    }

    /// <summary>
    /// Setor de verdade — nao None e nao lixo. Use isto para decidir se algo entra no
    /// grafo de setores: None e a AUSENCIA de setor, nao um setor chamado "None", e
    /// um int que nao existe mais no enum nao vira o vizinho por cast.
    /// </summary>
    public static bool IsRealSector(ConstructionSector sector)
    {
        return sector != ConstructionSector.None
            && System.Enum.IsDefined(typeof(ConstructionSector), sector);
    }

    /// <summary>Versao string para validacoes em editors e save data.</summary>
    public static bool IsBaseName(string name)
    {
        return TryParseBaseName(name, out _);
    }

    /// <summary>
    /// Slot do jogador dono deste setor de base (Base0 -> 0). Falso se nao for base.
    /// Continua sendo apenas o rotulo: quem manda de fato e o slotIndex da construcao.
    /// </summary>
    public static bool TryGetBaseSlotIndex(ConstructionSector sector, out int slotIndex)
    {
        slotIndex = -1;
        if (!IsBase(sector))
            return false;
        slotIndex = (int)sector - BaseRangeStart;
        return true;
    }

    /// <summary>Setor de base correspondente a um slot. None se fora da faixa declarada.</summary>
    public static ConstructionSector BaseSectorForSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > MaxDeclaredBaseSlot)
            return ConstructionSector.None;
        return (ConstructionSector)(BaseRangeStart + slotIndex);
    }

    /// <summary>
    /// Rotulo curto e UNICO do setor, usado nos badges do HUD (predio e unidade).
    /// OTAN = 1 letra (as 26 iniciais sao distintas); grego = 2 letras, porque a
    /// inicial colidiria com o bloco OTAN. Nao trocar por sector.ToString()[0].
    /// </summary>
    public static string GetBadge(ConstructionSector sector)
    {
        if (Badges.TryGetValue(sector, out string badge))
            return badge;

        string name = sector.ToString();
        return name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
    }

    private static readonly Dictionary<ConstructionSector, string> Badges =
        new Dictionary<ConstructionSector, string>
    {
        // OTAN — inicial, todas distintas de A a Z.
        { ConstructionSector.Alpha,   "A" },
        { ConstructionSector.Bravo,   "B" },
        { ConstructionSector.Charlie, "C" },
        { ConstructionSector.Delta,   "D" },
        { ConstructionSector.Echo,    "E" },
        { ConstructionSector.Foxtrot, "F" },
        { ConstructionSector.Golf,    "G" },
        { ConstructionSector.Hotel,   "H" },
        { ConstructionSector.India,   "I" },
        { ConstructionSector.Juliet,  "J" },
        { ConstructionSector.Kilo,    "K" },
        { ConstructionSector.Lima,    "L" },
        { ConstructionSector.Mike,    "M" },
        { ConstructionSector.November,"N" },
        { ConstructionSector.Oscar,   "O" },
        { ConstructionSector.Papa,    "P" },
        { ConstructionSector.Quebec,  "Q" },
        { ConstructionSector.Romeo,   "R" },
        { ConstructionSector.Sierra,  "S" },
        { ConstructionSector.Tango,   "T" },
        { ConstructionSector.Uniform, "U" },
        { ConstructionSector.Victor,  "V" },
        { ConstructionSector.Whiskey, "W" },
        { ConstructionSector.Xray,    "X" },
        { ConstructionSector.Yankee,  "Y" },
        { ConstructionSector.Zulu,    "Z" },

        // Grego — 2 letras. Omicron="Om" e Omega="Og" (o "g" vem de omeGa) porque
        // as duas comecam igual; o resto e so as duas primeiras letras do nome.
        { ConstructionSector.Beta,    "Be" },
        { ConstructionSector.Gamma,   "Ga" },
        { ConstructionSector.Epsilon, "Ep" },
        { ConstructionSector.Zeta,    "Ze" },
        { ConstructionSector.Eta,     "Et" },
        { ConstructionSector.Theta,   "Th" },
        { ConstructionSector.Iota,    "Io" },
        { ConstructionSector.Kappa,   "Ka" },
        { ConstructionSector.Lambda,  "La" },
        { ConstructionSector.Mu,      "Mu" },
        { ConstructionSector.Nu,      "Nu" },
        { ConstructionSector.Xi,      "Xi" },
        { ConstructionSector.Omicron, "Om" },
        { ConstructionSector.Pi,      "Pi" },
        { ConstructionSector.Rho,     "Rh" },
        { ConstructionSector.Sigma,   "Si" },
        { ConstructionSector.Tau,     "Ta" },
        { ConstructionSector.Upsilon, "Up" },
        { ConstructionSector.Phi,     "Ph" },
        { ConstructionSector.Chi,     "Ch" },
        { ConstructionSector.Psi,     "Ps" },
        { ConstructionSector.Omega,   "Og" },

        // Bases — o HUD de predio esconde o badge em base e o HUD de unidade usa
        // simbolo proprio (>>, !, #). Isto aqui e so um fallback sensato.
        { ConstructionSector.Base0,   "B0" },
        { ConstructionSector.Base1,   "B1" },
        { ConstructionSector.Base2,   "B2" },
        { ConstructionSector.Base3,   "B3" },
        { ConstructionSector.Base4,   "B4" },
    };

    private static bool TryParseBaseName(string name, out int slotIndex)
    {
        slotIndex = -1;
        if (string.IsNullOrEmpty(name) || !name.StartsWith("Base"))
            return false;
        if (!int.TryParse(name.Substring(4), out int parsed))
            return false;
        if (parsed < 0 || parsed > BaseRangeEnd - BaseRangeStart)
            return false;
        slotIndex = parsed;
        return true;
    }
}
