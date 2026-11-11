using System.Collections.Generic;

public enum UnitRole
{
    None = 0,
    // Participacao direta ou especializada em batalha.
    Capturador = 1,
    Assalto = 2,
    FogoIndireto = 5,
    Interceptador = 8,
    [UnityEngine.InspectorName("Ataque ao Solo")]
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
    // 15 reservado — antigo TransportadorAereo; nao reutilizar. Incorporado ao
    // Transportador (governanca G17): o Chinook declara Transportador e a politica de
    // compra aerea vive no Shopping pela ficha (isTransporter + dominio), nao pelo rotulo.
    // Em 2026-11-11 (D3) nao havia leitor em codigo nem ficha com o valor 15.
    // Disruptor de captura (autor, 2026-11-06): "Voce nao vai fazer dinheiro nao!".
    // Caca quem faz dinheiro para o inimigo — capturador capturando, transportador,
    // capturador — em vez de orbitar o capitao. Sub-rotulo de AtaqueAereo (o shopping o
    // conta como ataque aereo; ver MatchesLabel). O que ele caca sai das ARMAS da ficha,
    // nao do rotulo: nada aqui sabe que e um helicoptero.
    DisruptorDeCaptura = 16,
    // Interceptador de Disruptores (autor, 2026-11-07): o "Interceptador" eram dois trabalhos
    // com um nome so. Este caca o DISRUPTOR (Apache) que morde o nosso comboio aereo — e quem
    // a ESCOLTA pede. O Interceptador (8) segue sendo o de aeronaves (Caca A/F: abate o que
    // voa, bombardeiro primeiro). Sub-rotulo de Interceptador, no mesmo molde do Disruptor
    // sob AtaqueAereo: MatchesLabel faz o codigo antigo continuar vendo-o como interceptador.
    // A presa preferida sai da prioridade de alvo da ficha.
    InterceptadorDeDisruptor = 17
}

/// <summary>
/// A GRANDE FAMÍLIA — o papel no sentido de COMPORTAMENTO: a coluna do questionário,
/// a ordem das casas, o magnético. Seis, e só seis. Os valores de UnitRole viram
/// RÓTULOS dentro de uma família: o que o shopping pede e o que a ficha declara.
/// Contrato: docs/AI Behavior/contrato_questionario.md §7.1; ficha_do_papel.md §7.7.
///
/// Não é serializado em ficha nem em save: deriva do rótulo (roles[0], a essência)
/// pelo UnitRoleCatalog.
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

/// <summary>Grupo de combate (matriz do autor). Por rotulo, no UnitRoleCatalog.</summary>
public enum UnitForce { Nenhuma, Ruptura, Dissuasao, Ocupacao, Apoio }

public enum UnitBattleParticipation
{
    None = 0,
    Indirect = 1,
    Direct = 2
}

/// <summary>
/// Leitura do UnitRoleCatalog (D3, 2026-11-11). Antes, cinco switch moravam aqui;
/// agora toda resposta vem do asset. O que FICA em codigo e capacidade mecanica da
/// ficha (quem carrega satisfaz Transportador, quem supre satisfaz Logistica) e a
/// identidade de sub-rotulo (MatchesLabel), que nao e doutrina: e o nome.
/// </summary>
public static class UnitRoleCompatibility
{
    private static UnitRoleCatalogEntry Entry(UnitRole role) => UnitRoleCatalog.Active?.Get(role);

    public static UnitRoleFamily ResolveFamily(UnitRole role)
        => Entry(role)?.familia ?? UnitRoleFamily.Nenhuma;

    /// <summary>A família da essência: roles[0]. Ficha sem papel não tem família.</summary>
    public static UnitRoleFamily ResolveFamily(UnitData data)
    {
        return data != null && data.roles != null && data.roles.Count > 0
            ? ResolveFamily(data.roles[0])
            : UnitRoleFamily.Nenhuma;
    }

    public static UnitForce ResolveForce(UnitRole role)
        => Entry(role)?.forca ?? UnitForce.Nenhuma;

    /// <summary>A Força da essência: roles[0].</summary>
    public static UnitForce ResolveForce(UnitData data)
    {
        return data != null && data.roles != null && data.roles.Count > 0
            ? ResolveForce(data.roles[0])
            : UnitForce.Nenhuma;
    }

    public static UnitBattleParticipation ResolveBattleParticipation(UnitRole role)
        => Entry(role)?.participacao ?? UnitBattleParticipation.None;

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
        UnitRoleCatalogEntry entry = Entry(actualRole);
        return entry != null && entry.satisfazTambem != null && entry.satisfazTambem.Contains(requestedRole);
    }

    /// <summary>
    /// O rotulo (roles[0], a essencia) E o pedido, ou um SUB-rotulo dele que o codigo
    /// deve tratar como o mesmo. Hoje: DisruptorDeCaptura conta como AtaqueAereo, e
    /// InterceptadorDeDisruptor conta como Interceptador.
    ///
    /// Existe porque dezenas de pontos testam `roles[0] == AtaqueAereo` estrito (shopping,
    /// combate aereo, rally, carona aerea). Sem um ponto unico, criar o sub-rotulo tirava
    /// o Apache de todos eles em silencio. Mais estreito que CanSatisfy de proposito:
    /// CapturadorCombatente satisfaz Assalto, mas nao E um Assalto na essencia. Fica em
    /// codigo: e identidade do nome, nao doutrina configuravel.
    /// </summary>
    public static bool MatchesLabel(UnitRole actualRole, UnitRole requestedRole)
    {
        if (actualRole == requestedRole)
            return true;
        return (requestedRole == UnitRole.AtaqueAereo
                && actualRole == UnitRole.DisruptorDeCaptura)
            || (requestedRole == UnitRole.Interceptador
                && actualRole == UnitRole.InterceptadorDeDisruptor);
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

    /// <summary>Como o Shopping CONTA a ficha: o "conta como" da essencia no catalogo.</summary>
    public static UnitRole ResolveCompositionRole(UnitData data)
    {
        if (data == null || data.roles == null || data.roles.Count == 0)
            return UnitRole.None;

        UnitRole primary = data.roles[0];
        UnitRoleCatalogEntry entry = Entry(primary);
        return entry != null && entry.contaComo != UnitRole.None ? entry.contaComo : primary;
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
