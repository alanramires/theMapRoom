using System.Collections.Generic;

public enum UnitRole
{
    None = 0,
    // Participacao direta ou especializada em batalha.
    Capturador = 1,
    Assalto = 2,
    FogoIndireto = 5,
    Interceptador = 8,
    AtaqueAereo = 9,
    Antiaereo = 10,
    RaidAntiSub = 11,
    CapturadorAgressivo = 12,
    ArtilheiroCombatente = 13,
    AntiaereoCombatente = 14,
    // Apoio operacional. Os valores numericos permanecem estaveis porque
    // UnitRole e serializado nos UnitData.
    Transportador = 3,
    Logistica = 4,
    Intel = 6,
    // Antes chamado Suprimentos: o papel e movimentar carga, nao prestar servico de
    // suprimento (quem supre e Logistica). Valor 7 preservado — esta serializado nos UnitData.
    Estoque = 7,
    // Transporte AÉREO operacional (Chinook): mesma mecânica do Transportador, mas com
    // política de shopping própria — compra de início de jogo focada nos nós
    // INTERMEDIÁRIOS do eixo, enquanto o APC só gera demanda depois que os nós
    // iniciais do eixo foram conquistados.
    TransportadorAereo = 15
}

public enum UnitBattleParticipation
{
    None = 0,
    Indirect = 1,
    Direct = 2
}

public static class UnitRoleCompatibility
{
    public static UnitBattleParticipation ResolveBattleParticipation(
        UnitRole role)
    {
        switch (role)
        {
            case UnitRole.FogoIndireto:
                return UnitBattleParticipation.Indirect;

            case UnitRole.Capturador:
            case UnitRole.Assalto:
            case UnitRole.Interceptador:
            case UnitRole.AtaqueAereo:
            case UnitRole.Antiaereo:
            case UnitRole.RaidAntiSub:
            case UnitRole.CapturadorAgressivo:
            case UnitRole.ArtilheiroCombatente:
            case UnitRole.AntiaereoCombatente:
                return UnitBattleParticipation.Direct;

            default:
                return UnitBattleParticipation.None;
        }
    }

    public static UnitBattleParticipation ResolveBattleParticipation(
        UnitData data)
    {
        if (data == null || data.roles == null)
            return UnitBattleParticipation.None;

        UnitBattleParticipation best = UnitBattleParticipation.None;
        for (int i = 0; i < data.roles.Count; i++)
        {
            UnitBattleParticipation current =
                ResolveBattleParticipation(data.roles[i]);
            if (current > best)
                best = current;
        }

        return best;
    }

    public static bool ParticipatesInBattle(UnitData data) =>
        ResolveBattleParticipation(data) != UnitBattleParticipation.None;

    public static bool CanSatisfy(UnitRole actualRole, UnitRole requestedRole)
    {
        if (actualRole == requestedRole)
            return true;

        switch (actualRole)
        {
            case UnitRole.CapturadorAgressivo:
                return requestedRole == UnitRole.Capturador
                    || requestedRole == UnitRole.Assalto;
            case UnitRole.ArtilheiroCombatente:
                return requestedRole == UnitRole.Assalto
                    || requestedRole == UnitRole.FogoIndireto;
            case UnitRole.Antiaereo:
                return requestedRole == UnitRole.FogoIndireto;
            case UnitRole.AntiaereoCombatente:
                return requestedRole == UnitRole.Antiaereo
                    || requestedRole == UnitRole.Assalto
                    || requestedRole == UnitRole.FogoIndireto;
            case UnitRole.TransportadorAereo:
                return requestedRole == UnitRole.Transportador;
            default:
                return false;
        }
    }

    public static bool CanSatisfy(UnitData data, UnitRole requestedRole)
    {
        if (data == null || data.roles == null)
            return false;

        for (int i = 0; i < data.roles.Count; i++)
            if (CanSatisfy(data.roles[i], requestedRole))
                return true;

        // Capacidade mecânica é a fonte de verdade: quem carrega satisfaz Transportador e quem
        // supre satisfaz Logística — sem precisar de papel híbrido (ex.: Logística Móvel).
        if (requestedRole == UnitRole.Transportador && data.isTransporter)
            return true;
        if (requestedRole == UnitRole.Logistica && data.isSupplier)
            return true;

        return false;
    }

    public static bool CanSatisfy(IReadOnlyList<UnitRole> roles, UnitRole requestedRole)
    {
        if (roles == null)
            return false;

        for (int i = 0; i < roles.Count; i++)
            if (CanSatisfy(roles[i], requestedRole))
                return true;

        return false;
    }

    public static UnitRole ResolveCompositionRole(UnitData data)
    {
        if (data == null || data.roles == null || data.roles.Count == 0)
            return UnitRole.None;

        UnitRole primary = data.roles[0];
        if (primary == UnitRole.CapturadorAgressivo)
            return UnitRole.Capturador;
        if (primary == UnitRole.ArtilheiroCombatente)
            return data.unitClass == GameUnitClass.Armored
                ? UnitRole.Assalto
                : UnitRole.FogoIndireto;
        if (primary == UnitRole.TransportadorAereo)
            return UnitRole.Transportador;
        return primary;
    }

    // Transporte operacional (APC, Chinook, navio de desembarque etc.) é diferente da
    // capacidade mecânica auxiliar de carregar/rebocar. Um supridor pode ter isTransporter
    // para rebocar artilharia sem virar transporte de tropas no plano ou no shopping.
    public static bool IsOperationalTransporter(UnitData data)
    {
        return data != null
            && data.isTransporter
            && ResolveCompositionRole(data) == UnitRole.Transportador;
    }
}
