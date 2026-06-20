using System.Collections.Generic;

public enum UnitRole
{
    None = 0,
    Capturador = 1,
    Assalto = 2,
    Transportador = 3,
    Logistica = 4,
    FogoIndireto = 5,
    Intel = 6,
    Suprimentos = 7,
    Interceptador = 8,
    AtaqueAereo = 9,
    Antiaereo = 10,
    RaidAntiSub = 11,
    CapturadorAgressivo = 12,
    ArtilheiroCombatente = 13,
    AntiaereoCombatente = 14
}

public static class UnitRoleCompatibility
{
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
        return primary;
    }
}
