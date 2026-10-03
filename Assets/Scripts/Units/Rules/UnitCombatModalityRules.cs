using System.Collections.Generic;

/// <summary>
/// Como a unidade LUTA, lido das armas — nunca de um campo da ficha.
/// Contrato: docs/AI Behavior/contrato_questionario.md §7.3.
/// </summary>
public enum UnitCombatModality
{
    /// <summary>Nenhuma arma embarcada.</summary>
    SemArma = 0,

    /// <summary>Só contato: toda arma tem alcance máximo 1 (ou 0). Coluna do Assalto.</summary>
    Combatente = 1,

    /// <summary>Só à distância: toda arma tem alcance mínimo 2 ou mais. Coluna do Fogo de Suporte.</summary>
    Artilheiro = 2,

    /// <summary>
    /// Alcança o contato E a distância — numa arma só (canhão 1~2) ou somando armas
    /// (canhão 2~3 + metralhadora 1). Pega a seta: tiro parado do FS primeiro, e o
    /// contato do Assalto se não valer.
    /// </summary>
    Hibrida = 3,
}

/// <summary>
/// Serviço burro: recebe as armas, devolve a modalidade. Não sabe papel, rótulo,
/// política nem perfil — o rótulo decide ONDE a peça fica; a arma decide COMO luta.
///
/// Lê o MESMO alcance que o PodeMirar usa (UnitEmbarkedWeapon.GetRangeMin/Max, o
/// valor da unidade, não o default da WeaponData). Senão a classificação e o tiro
/// discordam.
///
/// A munição NÃO entra: ela muda a resposta do Mirar neste turno, não a identidade
/// da peça. Um obus sem bala continua artilheiro.
///
/// Passa no teste do renome: renomeie o rótulo e nada aqui muda; mude o alcance da
/// arma e a modalidade muda sozinha, sem uma linha de código.
/// </summary>
public static class UnitCombatModalityRules
{
    public static UnitCombatModality Resolve(UnitData data)
    {
        return data != null ? Resolve(data.embarkedWeapons) : UnitCombatModality.SemArma;
    }

    public static UnitCombatModality Resolve(UnitManager unit)
    {
        return unit != null ? Resolve(unit.GetEmbarkedWeapons()) : UnitCombatModality.SemArma;
    }

    /// <summary>
    /// Alcança o contato e a distância: pega a seta (tiro parado do FS primeiro).
    /// Substitui o antigo campo preferArtilleryModeBeforeCombatant, que ficava preso
    /// quando a arma mudava — o Bazooka e a Metranca foram nerfados de 1~2 para 1 e
    /// continuaram marcados.
    /// </summary>
    public static bool IsHybrid(UnitData data) => Resolve(data) == UnitCombatModality.Hibrida;

    /// <summary>
    /// A versão que a IA usa: lê as armas DA PEÇA, a cópia feita quando ela nasceu
    /// (UnitManager.SyncEmbarkedWeaponsFromData) — a mesma que o PodeMirar usa no
    /// tiro. Ler a ficha aqui faria uma peça já em campo ser classificada pelo
    /// alcance novo da ficha enquanto ainda atira com o antigo. A ficha (UnitData)
    /// só vale nas telas de autoria.
    /// </summary>
    public static bool IsHybrid(UnitManager unit) => Resolve(unit) == UnitCombatModality.Hibrida;

    public static UnitCombatModality Resolve(IReadOnlyList<UnitEmbarkedWeapon> weapons)
    {
        bool alcancaContato = false;
        bool alcancaDistancia = false;

        for (int i = 0; weapons != null && i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            // Contato = alcance 0 (mesmo hex) ou 1, a mesma fronteira do PodeMirar
            // para quem se moveu. Distância = alcança 2 ou mais.
            if (embarked.GetRangeMin() <= 1)
                alcancaContato = true;
            if (embarked.GetRangeMax() >= 2)
                alcancaDistancia = true;
        }

        if (alcancaContato && alcancaDistancia)
            return UnitCombatModality.Hibrida;
        if (alcancaDistancia)
            return UnitCombatModality.Artilheiro;
        if (alcancaContato)
            return UnitCombatModality.Combatente;
        return UnitCombatModality.SemArma;
    }
}
