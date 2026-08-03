using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Distancia de ROTA entre duas celulas, para uma unidade. Nao e distancia
/// cubica: um capturador a quatro hexes em linha reta atras de uma serra fica
/// mais longe que um a cinco hexes de estrada, e e por isso que quem escolhe
/// quem seguir precisa desta conta e nao da regua.
///
/// A GEOMETRIA E DA UNIDADE, nao do chamador: `Domain.Air` volta em distancia
/// de hex direto, porque aeronave nao anda por caminhos. Ninguem precisa pedir
/// "cubica para o caca" — ja sai assim.
///
/// Memoizada por (unidade, origem, destino) e invalidada pela revisao global do
/// tabuleiro. `NaN` guardado significa "sem rota", para nao repetir a travessia
/// que ja falhou.
///
/// Estava como `private static` dentro de AIController.Capturer.Attack.cs, com
/// vinte e poucos chamadores da IA e nenhum de fora. Saiu para ca sem mudar uma
/// linha do corpo: ferramenta de Editor tambem precisa da resposta, e a
/// ferramenta tem que ver o mesmo que a IA ve.
/// </summary>
public static class AIRouteDistance
{
    private static readonly Dictionary<
        (int unitId, Vector3Int from, Vector3Int to), float> Cache =
            new Dictionary<
                (int unitId, Vector3Int from, Vector3Int to), float>();

    private static int cacheRevision = -1;

    public static bool TryGet(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int targetCell,
        out float distance)
    {
        using var perf = new AIDecisionPerfScope(unit, "routeDistance");
        distance = 0f;
        fromCell.z = 0;
        targetCell.z = 0;

        int revision = ThreatRevisionTracker.GlobalBoardRevision;
        if (revision != cacheRevision)
        {
            Cache.Clear();
            cacheRevision = revision;
        }

        bool cacheable = unit != null;
        (int, Vector3Int, Vector3Int) key = default;
        if (cacheable)
        {
            key = (unit.InstanceId, fromCell, targetCell);
            if (Cache.TryGetValue(key, out float cached))
            {
                // NaN = "sem rota" memoizado (o metodo devolve false neste caso).
                if (float.IsNaN(cached))
                    return false;
                distance = cached;
                return true;
            }
        }

        if (TryGetUncached(unit, fromCell, targetCell, out distance))
        {
            if (cacheable)
                Cache[key] = distance;
            return true;
        }

        if (cacheable)
            Cache[key] = float.NaN;
        return false;
    }

    /// <summary>
    /// Rota quando existe; distancia de hex como consolo quando nao existe.
    /// Consolo, e nao resposta: quem precisa saber se HA rota usa o TryGet.
    /// </summary>
    public static float GetOrHexDistance(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int targetCell)
    {
        return TryGet(unit, fromCell, targetCell, out float routeDistance)
            ? routeDistance
            : SectorManager.HexDistance(fromCell, targetCell);
    }

    private static bool TryGetUncached(
        UnitManager unit,
        Vector3Int fromCell,
        Vector3Int targetCell,
        out float distance)
    {
        distance = 0f;

        if (unit != null && unit.GetDomain() == Domain.Air)
        {
            distance = SectorManager.HexDistance(fromCell, targetCell);
            return true;
        }

        if (unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && SectorManager.TryGetLandMovementDistance(
                fromCell, targetCell, unitData, out int unitCost))
        {
            distance = unitCost;
            return true;
        }

        if (SectorManager.TryGetLandMovementDistance(
                fromCell, targetCell, out int fallbackCost))
        {
            distance = fallbackCost;
            return true;
        }

        return false;
    }
}
