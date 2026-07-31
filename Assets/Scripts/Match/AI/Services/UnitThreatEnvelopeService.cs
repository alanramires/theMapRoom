using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// FACHADA DE COMPATIBILIDADE — TEMPORARIA.
///
/// A hotzone deixou de ser um servico de ameaca e passou a ser o envelope de
/// alcance materializavel de <see cref="UnitReachEnvelopeService"/>, com dois
/// eixos: intencao (combate, servico, transferencia, fusao, embarque) e banda
/// (Tactical, Operational, Strategic). Ameaca virou uma das intencoes.
///
/// Este arquivo nao contem mais regra propria: cada metodo traduz a chamada
/// herdada para um <see cref="UnitReachRequest"/> e delega. Ele existe apenas
/// para que os consumidores migrem um de cada vez.
///
/// AO MIGRAR UM CONSUMIDOR:
///   TryGet(...)              -> Build/BuildProfile com Intent = Combat
///   BuildServiceEnvelope(...)-> Intent = Service ou Transfer
///   BuildFusionEnvelope(...) -> Intent = Fusion
/// e prefira <see cref="UnitReachEnvelope"/> a UnitThreatEnvelope no tipo da
/// variavel. Quando nao restar consumidor, apague este arquivo.
/// </summary>
public static class UnitThreatEnvelopeService
{
    /// <summary>
    /// Envelope de servico a partir de uma onda de movimento ja calculada.
    ///
    /// PARIDADE HERDADA: com <see cref="SupplierRangeMode.SameHexOrEmbarked"/>
    /// este metodo sempre devolveu conjunto de acao VAZIO — o alcance util era
    /// reconstruido pelo chamador a partir de RangeCells. O servico unificado
    /// modela o caso corretamente (a acao inclui o proprio hex), entao a
    /// fachada zera o conjunto de volta para nao mexer em jogabilidade durante
    /// a migracao. Consumidores que ja reconstruiam o alcance devem migrar para
    /// UnitReachEnvelopeService e ler ActionCells direto.
    /// </summary>
    public static UnitThreatEnvelope BuildServiceEnvelope(
        UnitManager unit,
        Tilemap boardMap,
        Dictionary<Vector3Int, List<Vector3Int>> paths,
        SupplierRangeMode serviceRange)
    {
        return ToLegacyServiceEnvelope(
            UnitReachEnvelopeService.Build(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = boardMap,
                Intent = ReachIntent.Service,
                Band = ReachBand.Tactical,
                PrebuiltPaths = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>(),
                RangeOverride = serviceRange,
                FilterByOperationDomain = false
            }),
            serviceRange);
    }

    /// <summary>
    /// Entrada herdada para consumidores sem onda de movimento pronta.
    /// Resolve os mesmos Caminhos Validos usados pelo jogo; aeronaves usam o
    /// reach cubico centralizado no AIActionReachCoordinator.
    /// </summary>
    public static UnitThreatEnvelope BuildServiceEnvelope(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        int movementBudget,
        SupplierRangeMode serviceRange)
    {
        return ToLegacyServiceEnvelope(
            UnitReachEnvelopeService.Build(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = boardMap,
                TerrainDatabase = terrainDatabase,
                Intent = ReachIntent.Service,
                Band = ReachBand.Tactical,
                MovementBudget = Mathf.Max(0, movementBudget),
                RangeOverride = serviceRange,
                FilterByOperationDomain = false
            }),
            serviceRange);
    }

    /// <summary>
    /// Hotzone especializada de fusao: o alvo nao fica a "+1 gratis" do
    /// movimento, a unidade precisa conservar MP para entrar no hex do receptor.
    /// </summary>
    public static UnitThreatEnvelope BuildFusionEnvelope(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        Dictionary<Vector3Int, List<Vector3Int>> paths)
    {
        return ToLegacy(
            UnitReachEnvelopeService.Build(new UnitReachRequest
            {
                Unit = unit,
                BoardMap = boardMap,
                TerrainDatabase = terrainDatabase,
                Intent = ReachIntent.Fusion,
                Band = ReachBand.Tactical,
                MovementBudget = unit != null
                    ? Mathf.Max(0, unit.RemainingMovementPoints)
                    : 0,
                PrebuiltPaths = paths ?? new Dictionary<Vector3Int, List<Vector3Int>>(),
                FilterByOperationDomain = false
            }),
            ReachIntent.Fusion);
    }

    public static void ClearCache()
    {
        UnitReachEnvelopeService.ClearCache();
    }

    public static bool TryGet(
        UnitManager unit,
        Tilemap boardMap,
        TerrainDatabase terrainDatabase,
        UnitThreatEnvelopeMovement movementMode,
        DPQAirHeightConfig dpqAirHeightConfig,
        bool enableLdt,
        bool enableLos,
        bool enableSpotter,
        out UnitThreatEnvelope envelope,
        out bool cacheHit)
    {
        envelope = null;
        cacheHit = false;
        if (unit == null)
            return false;

        Tilemap map = boardMap != null ? boardMap : unit.BoardTilemap;
        if (map == null)
            return false;

        envelope = UnitReachEnvelopeService.BuildCombat(
            new UnitReachRequest
            {
                Unit = unit,
                BoardMap = map,
                TerrainDatabase = terrainDatabase,
                Intent = ReachIntent.Combat,
                // Chamador herdado nao informa subetapa: deriva do armamento
                // para preservar o comportamento de hoje. A API nova exige.
                SubStep = UnitReachEnvelopeService.ResolveLegacyCombatSubStep(unit),
                Band = ReachBand.Tactical,
                MovementMode = movementMode,
                DpqAirHeightConfig = dpqAirHeightConfig,
                EnableLdt = enableLdt,
                EnableLos = enableLos,
                EnableSpotter = enableSpotter
            },
            map,
            out cacheHit);
        return envelope != null;
    }

    private static UnitThreatEnvelope ToLegacyServiceEnvelope(
        UnitReachEnvelope envelope,
        SupplierRangeMode serviceRange)
    {
        if (envelope != null
            && serviceRange == SupplierRangeMode.SameHexOrEmbarked)
        {
            return new UnitThreatEnvelope(
                envelope.Intent,
                envelope.Band,
                envelope.PathsByDestination,
                envelope.CostByCell,
                envelope.MovementCells,
                new HashSet<Vector3Int>(),
                new HashSet<Vector3Int>());
        }

        return ToLegacy(envelope, ReachIntent.Service);
    }

    private static UnitThreatEnvelope ToLegacy(
        UnitReachEnvelope envelope,
        ReachIntent fallbackIntent)
    {
        if (envelope == null)
        {
            return new UnitThreatEnvelope(
                fallbackIntent,
                ReachBand.Tactical,
                null,
                null,
                null,
                null,
                null);
        }

        return new UnitThreatEnvelope(
            envelope.Intent,
            envelope.Band,
            envelope.PathsByDestination,
            envelope.CostByCell,
            envelope.MovementCells,
            envelope.AttackableCells,
            new HashSet<Vector3Int>(envelope.LineCells));
    }
}
