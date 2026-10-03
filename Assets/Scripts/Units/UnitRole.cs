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
    // 11 reservado — antigo RaidAntiSub; nao reutilizar.
    //
    // Removido porque nunca foi papel: "encontrar submarino" e visao
    // (Vigilancia com camada principal Submarine/Submerged) e "destruir
    // submarino" e arma. Nenhuma ficha o usava, mas o shopping o pedia — e a
    // contagem dava zero para sempre, entao a demanda disparava todo turno sem
    // poder ser preenchida.
    //
    // O numero fica de fora do enum, e nao como membro obsoleto executavel:
    // nenhum save persiste UnitData.roles (o load recupera a ficha pelo unitId),
    // e nao existe 11 em asset, prefab ou cena. O comentario existe so para
    // ninguem reaproveitar o valor e ressuscitar dados antigos por acidente.
    CapturadorCombatente = 12,
    ArtilheiroCombatente = 13,
    AntiaereoCombatente = 14,
    // Apoio operacional. Os valores numericos permanecem estaveis porque
    // UnitRole e serializado nos UnitData.
    Transportador = 3,
    Logistica = 4,
    // Observação e detecção orientadas pela camada principal da ficha. O papel
    // diz O QUE a unidade faz; Domain/Height dizem o que ela observa e como se
    // move. Valor 6 preservado porque UnitRole é serializado numericamente.
    Vigilancia = 6,
    // Antes chamado Suprimentos: o papel e movimentar carga, nao prestar servico de
    // suprimento (quem supre e Logistica). Valor 7 preservado — esta serializado nos UnitData.
    Estoque = 7,
    // Transporte AÉREO operacional (Chinook): mesma mecânica do Transportador, mas com
    // política de shopping própria — compra de início de jogo focada nos nós
    // INTERMEDIÁRIOS do eixo, enquanto o APC só gera demanda depois que os nós
    // iniciais do eixo foram conquistados.
    TransportadorAereo = 15
}

/// <summary>
/// A GRANDE FAMÍLIA — o papel no sentido de COMPORTAMENTO: a coluna do questionário,
/// a ordem das casas, o magnético. Seis, e só seis. Os valores de UnitRole viram
/// RÓTULOS dentro de uma família: o que o shopping pede e o que a ficha declara.
/// Contrato: docs/AI Behavior/contrato_questionario.md §7.1; ficha_do_papel.md §7.7.
///
/// Não é serializado em lugar nenhum: deriva sempre do rótulo (roles[0], a essência).
/// </summary>
public enum UnitRoleFamily
{
    Nenhuma = 0,
    Capturador = 1,
    Assalto = 2,
    FogoDeSuporte = 3,
    Transportador = 4,
    Vigilancia = 5,
    Logistica = 6,
}

public enum UnitBattleParticipation
{
    None = 0,
    Indirect = 1,
    Direct = 2
}

public static class UnitRoleCompatibility
{
    /// <summary>
    /// Rótulo → família (autor, 2026-10-03). Os rótulos ficaram por conta do shopping;
    /// o comportamento é da família.
    ///
    /// NÃO substitui ResolveCompositionRole, e diverge dele de propósito num ponto: lá o
    /// ArtilheiroCombatente é Assalto só se for blindado (senão Fogo Indireto), porque
    /// aquilo conta COMPOSIÇÃO para a compra. Aqui é sempre Assalto — o Obus Leve é
    /// "uma unidade de assalto do exército". Como ele luta (a seta do híbrido) vem da
    /// arma, não daqui (UnitCombatModalityRules).
    /// </summary>
    public static UnitRoleFamily ResolveFamily(UnitRole role)
    {
        switch (role)
        {
            case UnitRole.Capturador:
            case UnitRole.CapturadorCombatente:
                return UnitRoleFamily.Capturador;

            case UnitRole.Assalto:
            case UnitRole.AtaqueAereo:
            case UnitRole.Interceptador:
            case UnitRole.ArtilheiroCombatente:
                return UnitRoleFamily.Assalto;

            case UnitRole.FogoIndireto:
            case UnitRole.Antiaereo:
            case UnitRole.AntiaereoCombatente:
                return UnitRoleFamily.FogoDeSuporte;

            case UnitRole.Transportador:
            case UnitRole.TransportadorAereo:
                return UnitRoleFamily.Transportador;

            case UnitRole.Vigilancia:
                return UnitRoleFamily.Vigilancia;

            case UnitRole.Logistica:
            case UnitRole.Estoque:
                return UnitRoleFamily.Logistica;

            default:
                return UnitRoleFamily.Nenhuma;
        }
    }

    /// <summary>A família da essência: roles[0]. Ficha sem papel não tem família.</summary>
    public static UnitRoleFamily ResolveFamily(UnitData data)
    {
        return data != null && data.roles != null && data.roles.Count > 0
            ? ResolveFamily(data.roles[0])
            : UnitRoleFamily.Nenhuma;
    }

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
            case UnitRole.CapturadorCombatente:
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
            case UnitRole.CapturadorCombatente:
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
        if (primary == UnitRole.CapturadorCombatente)
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
