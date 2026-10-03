using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// FAIXA DE ENTREGA — serviço burro. Recebe o passageiro, uma célula e um alvo; devolve
/// um fato: "descendo aqui, ele alcança o alvo?". Não sabe de papel, missão, política
/// nem de qual transportador pergunta. Contrato: CLAUDE.md, "Ranges are bands, not hex
/// numbers"; pendência C7 do Capturador.md.
///
/// Os caminhos do transporte respondiam esta pergunta cada um com o seu número fixo
/// (TransportDropOffRange 4, AirDropOffRange 2, FireSupportDropOffRange 3) — a mesma
/// pergunta, mais de trinta vezes, com respostas que podiam discordar. Agora é aqui.
///
///   combatente / sem arma   custo de ROTA do passageiro até o alvo ≤ o movimento
///                           dele. O soldado que anda 3 desce até 3 de rota do prédio;
///                           o bazooka que anda 2, até 2.
///   artilheiro              a banda é a da ARMA (a inversão): o alvo está ao alcance
///                           MÁXIMO de alguma arma a partir de onde ele desce.
///   híbrido                 qualquer das duas.
///
/// O custo de rota é calculado de trás para frente a partir do alvo, uma vez por
/// passageiro, alvo e orçamento, e reaproveitado no mesmo quadro.
/// </summary>
public static class FaixaDeEntregaService
{
    private static readonly Dictionary<(int, Vector3Int, int), Dictionary<Vector3Int, int>> custoPorQuadro =
        new Dictionary<(int, Vector3Int, int), Dictionary<Vector3Int, int>>();
    private static int quadroDoCache = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarSessao()
    {
        // Com domain reload desligado, estático sobrevive entre Plays.
        custoPorQuadro.Clear();
        quadroDoCache = -1;
    }

    /// <summary>
    /// O passageiro, descendo em <paramref name="dropCell"/>, alcança
    /// <paramref name="target"/>? <paramref name="turns"/> 2 = o operacional (duas rodadas
    /// de caminhada).
    /// </summary>
    public static bool PassageiroChegaAoAlvo(
        UnitManager passenger,
        Vector3Int dropCell,
        Vector3Int target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int turns = 1)
    {
        if (passenger == null)
            return false;
        dropCell.z = 0;
        target.z = 0;
        if (dropCell == target)
            return true;

        UnitCombatModality modalidade = UnitCombatModalityRules.Resolve(passenger);
        if (modalidade == UnitCombatModality.Artilheiro || modalidade == UnitCombatModality.Hibrida)
        {
            if (ArmaCobreAlvo(passenger, dropCell, target))
                return true;
            if (modalidade == UnitCombatModality.Artilheiro)
                return false;
        }

        if (map == null)
            return false;

        int orcamento = Mathf.Max(1, passenger.GetMovementRange()) * Mathf.Max(1, turns);
        if (Time.frameCount != quadroDoCache)
        {
            quadroDoCache = Time.frameCount;
            custoPorQuadro.Clear();
        }

        var chave = (passenger.InstanceId, target, orcamento);
        if (!custoPorQuadro.TryGetValue(chave, out Dictionary<Vector3Int, int> custos))
        {
            custos = UnitMovementPathRules.CalculateMovementCostMap(
                map, passenger, target, orcamento, terrainDatabase);
            custoPorQuadro[chave] = custos;
        }

        return custos != null && custos.TryGetValue(dropCell, out int custo) && custo <= orcamento;
    }

    /// <summary>Alguma das opções deixa o seu passageiro descer na faixa de entrega do alvo?</summary>
    public static bool AlgumPassageiroChega(
        List<PodeDesembarcarOption> options,
        Vector3Int target,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        int turns = 1)
    {
        for (int i = 0; options != null && i < options.Count; i++)
        {
            PodeDesembarcarOption option = options[i];
            if (option?.passengerUnit != null
                && PassageiroChegaAoAlvo(option.passengerUnit, option.disembarkCell, target, map, terrainDatabase, turns))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A inversão do artilheiro: o alvo está ao alcance MÁXIMO de alguma arma da peça, com
    /// o alcance da PEÇA (o mesmo do PodeMirar). A munição não entra: ela chega depois.
    ///
    /// A ZONA MORTA (mais perto que o mínimo) é ACEITA de propósito, ao contrário da
    /// Hotzone — que mostra onde a peça atira AGORA. Na entrega o caminhão anda em
    /// direção ao alvo: exigir o anel [mín, máx] faria a primeira parada já dentro do
    /// mínimo recusar a largada, e o anel ficaria para trás para sempre.
    /// </summary>
    public static bool ArmaCobreAlvo(UnitManager passenger, Vector3Int cell, Vector3Int target)
    {
        if (passenger == null)
            return false;
        IReadOnlyList<UnitEmbarkedWeapon> armas = passenger.GetEmbarkedWeapons();
        float distancia = SectorManager.HexDistance(cell, target);
        for (int i = 0; armas != null && i < armas.Count; i++)
        {
            UnitEmbarkedWeapon arma = armas[i];
            if (arma == null || arma.weapon == null)
                continue;
            if (distancia <= arma.GetRangeMax())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Teto de rota da entrega para um passageiro de FOGO DE SUPORTE: o alcance máximo da
    /// arma dele, ou o próprio movimento, se for maior. Mesma régua do ArmaCobreAlvo.
    /// </summary>
    public static int TetoDeEntregaFogoDeSuporte(UnitManager passenger)
    {
        if (passenger == null)
            return 0;
        int teto = Mathf.Max(1, passenger.GetMovementRange());
        IReadOnlyList<UnitEmbarkedWeapon> armas = passenger.GetEmbarkedWeapons();
        for (int i = 0; armas != null && i < armas.Count; i++)
        {
            UnitEmbarkedWeapon arma = armas[i];
            if (arma != null && arma.weapon != null)
                teto = Mathf.Max(teto, arma.GetRangeMax());
        }
        return teto;
    }

    /// <summary>
    /// Teto de rota da entrega para a carga INTEIRA: a maior faixa entre os passageiros.
    /// Vai junto com o limite POR PASSAGEIRO ligado no MelhorDesembarque, que segura cada
    /// um no próprio tático; o teto só não o deixa olhar além de quem vai mais longe.
    /// </summary>
    public static int TetoDeEntregaDaCarga(List<UnitManager> passengers)
    {
        int teto = 1;
        for (int i = 0; passengers != null && i < passengers.Count; i++)
        {
            UnitManager passageiro = passengers[i];
            if (passageiro == null)
                continue;
            bool fogoDeSuporte = passageiro.TryGetUnitData(out UnitData data)
                && UnitRoleCompatibility.CanSatisfy(data, UnitRole.FogoIndireto);
            teto = Mathf.Max(teto, fogoDeSuporte
                ? TetoDeEntregaFogoDeSuporte(passageiro)
                : Mathf.Max(1, passageiro.GetMovementRange()));
        }
        return teto;
    }

    /// <summary>
    /// Há inimigo detectado no OPERACIONAL DA ARMA desta peça — até 2× o alcance máximo de
    /// uma arma que consegue atingir a camada dele (WeaponData.SupportsOperationOn, a
    /// mesma checagem do PodeMirar)? É a banda do artilheiro (CLAUDE.md, "Known
    /// inversion"): o obus 3~4 tem operacional 8. Inimigo ali está vindo para dentro do
    /// alcance. Só lê a lista que o chamador passa — o chamador garante que são
    /// contatos detectados.
    /// </summary>
    public static bool HaInimigoNoOperacionalDaArma(
        UnitManager unit,
        IReadOnlyList<UnitManager> detectedEnemies,
        out UnitManager inimigo)
    {
        inimigo = null;
        if (unit == null || detectedEnemies == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> armas = unit.GetEmbarkedWeapons();
        if (armas == null || armas.Count == 0)
            return false;

        Vector3Int origem = unit.CurrentCellPosition;
        origem.z = 0;
        for (int e = 0; e < detectedEnemies.Count; e++)
        {
            UnitManager candidato = detectedEnemies[e];
            if (candidato == null || candidato.IsDead || candidato.IsEmbarked)
                continue;

            Vector3Int cell = candidato.CurrentCellPosition;
            cell.z = 0;
            float distancia = SectorManager.HexDistance(origem, cell);
            for (int i = 0; i < armas.Count; i++)
            {
                UnitEmbarkedWeapon arma = armas[i];
                if (arma == null || arma.weapon == null)
                    continue;
                if (!arma.weapon.SupportsOperationOn(candidato.GetDomain(), candidato.GetHeightLevel()))
                    continue;
                if (distancia <= arma.GetRangeMax() * 2)
                {
                    inimigo = candidato;
                    return true;
                }
            }
        }
        return false;
    }
}
