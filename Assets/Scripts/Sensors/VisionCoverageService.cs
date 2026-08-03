using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum VisionCoverageLayerKind
{
    All = 0,
    Specific = 1
}

/// <summary>
/// Camada consultada por uma pergunta de visao. All representa a visao simples
/// de superficie/FOW; Specific representa uma especializacao da ficha.
/// </summary>
public readonly struct VisionCoverageLayer : IEquatable<VisionCoverageLayer>
{
    public readonly VisionCoverageLayerKind Kind;
    public readonly Domain Domain;
    public readonly HeightLevel Height;

    public bool IsAll => Kind == VisionCoverageLayerKind.All;
    public string Label => IsAll ? "All" : $"{Domain}/{Height}";

    private VisionCoverageLayer(
        VisionCoverageLayerKind kind,
        Domain domain,
        HeightLevel height)
    {
        Kind = kind;
        Domain = domain;
        Height = height;
    }

    public static VisionCoverageLayer All =>
        new VisionCoverageLayer(
            VisionCoverageLayerKind.All,
            Domain.Land,
            HeightLevel.Surface);

    public static VisionCoverageLayer Specific(
        Domain domain,
        HeightLevel height) =>
        new VisionCoverageLayer(
            VisionCoverageLayerKind.Specific,
            domain,
            NormalizeHeight(domain, height));

    public bool Equals(VisionCoverageLayer other) =>
        Kind == other.Kind
        && (IsAll || (Domain == other.Domain && Height == other.Height));

    public override bool Equals(object obj) =>
        obj is VisionCoverageLayer other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Kind;
            if (!IsAll)
            {
                hash = hash * 31 + (int)Domain;
                hash = hash * 31 + (int)Height;
            }
            return hash;
        }
    }

    public override string ToString() => Label;

    private static HeightLevel NormalizeHeight(
        Domain domain,
        HeightLevel height)
    {
        switch (domain)
        {
            case Domain.Air:
                return height == HeightLevel.AirLow
                    ? HeightLevel.AirLow
                    : HeightLevel.AirHigh;
            case Domain.Submarine:
                return HeightLevel.Submerged;
            default:
                return HeightLevel.Surface;
        }
    }
}

/// <summary>
/// Resolve qual pergunta de visao melhor descreve o papel declarado na ficha.
/// A ordem e: deteccao stealth, maior alcance, ordem de declaracao.
/// </summary>
public static class VisionCoverageLayerResolver
{
    public static VisionCoverageLayer ResolvePrincipal(UnitManager unit)
    {
        if (unit != null && unit.TryGetUnitData(out UnitData data))
            return ResolvePrincipal(data);
        return VisionCoverageLayer.All;
    }

    public static VisionCoverageLayer ResolvePrincipal(UnitData data)
    {
        if (data == null
            || data.visionSpecializations == null
            || data.visionSpecializations.Count == 0)
        {
            return VisionCoverageLayer.All;
        }

        UnitVisionException best = null;
        bool bestDetectsStealth = false;
        int bestVision = int.MinValue;
        for (int i = 0; i < data.visionSpecializations.Count; i++)
        {
            UnitVisionException candidate = data.visionSpecializations[i];
            if (candidate == null)
                continue;

            bool detectsStealth =
                candidate.detectUnitsWithFollowingSkills != null
                && candidate.detectUnitsWithFollowingSkills.Count > 0;
            int vision = Mathf.Max(0, candidate.vision);
            if (best != null
                && (bestDetectsStealth && !detectsStealth
                    || bestDetectsStealth == detectsStealth
                    && bestVision >= vision))
            {
                continue;
            }

            best = candidate;
            bestDetectsStealth = detectsStealth;
            bestVision = vision;
        }

        return best == null
            ? VisionCoverageLayer.All
            : VisionCoverageLayer.Specific(
                best.domain,
                best.heightLevel);
    }

    public static List<VisionCoverageLayer> CollectAuditableLayers(
        UnitData data)
    {
        var result = new List<VisionCoverageLayer>
        {
            VisionCoverageLayer.All
        };
        if (data == null || data.visionSpecializations == null)
            return result;

        for (int i = 0; i < data.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = data.visionSpecializations[i];
            if (entry == null)
                continue;

            if (entry.allHeights && entry.domain == Domain.Air)
            {
                AddUnique(result, VisionCoverageLayer.Specific(
                    Domain.Air, HeightLevel.AirLow));
                AddUnique(result, VisionCoverageLayer.Specific(
                    Domain.Air, HeightLevel.AirHigh));
            }
            else
            {
                AddUnique(result, VisionCoverageLayer.Specific(
                    entry.domain, entry.heightLevel));
            }
        }
        return result;
    }

    private static void AddUnique(
        List<VisionCoverageLayer> layers,
        VisionCoverageLayer layer)
    {
        if (!layers.Contains(layer))
            layers.Add(layer);
    }
}

public sealed class VisionCoverageRequest
{
    public UnitManager Observer;
    public Vector3Int ObserverCell;
    public Tilemap Map;
    public TerrainDatabase TerrainDatabase;
    public DPQAirHeightConfig DpqAirHeightConfig;
    public VisionCoverageLayer Layer;
    public bool EnableLos = true;
}

public sealed class VisionCoverageResult
{
    public VisionCoverageLayer Layer;
    public bool DetectsStealth;
    public string Diagnostic;
    public readonly HashSet<Vector3Int> VisibleCells =
        new HashSet<Vector3Int>();

    public int VisibleCount => VisibleCells.Count;
}

/// <summary>
/// Consulta estrutural pura de visao a partir de uma celula hipotetica.
/// Nao move a unidade e nao publica FOW, deteccao, contatos ou revisoes.
/// </summary>
public static class VisionCoverageService
{
    public static VisionCoverageResult Evaluate(
        VisionCoverageRequest request)
    {
        using var perf = new AIDecisionPerfScope(
            request?.Observer,
            "visionCoverage");
        AIDecisionPerf.AddCount("VisionCoverageCalls");

        var result = new VisionCoverageResult
        {
            Layer = request != null
                ? request.Layer
                : VisionCoverageLayer.All
        };
        if (request == null
            || request.Observer == null
            || request.Map == null
            || request.TerrainDatabase == null)
        {
            result.Diagnostic =
                "Consulta incompleta: observador, mapa e TerrainDatabase sao obrigatorios.";
            return result;
        }

        Vector3Int observerCell = request.ObserverCell;
        observerCell.z = 0;
        VisionCoverageLayer layer = request.Layer;
        if (layer.IsAll)
        {
            PodeDetectarSensor.CollectVisibleCells(
                request.Observer,
                request.Map,
                request.TerrainDatabase,
                result.VisibleCells,
                request.DpqAirHeightConfig,
                request.EnableLos,
                enableSpotter: false,
                useOccupantLayerForTarget: false,
                preserveObserverLayerRangeForHexVisibility: true,
                useRangeOnlyForAirHighWhenConfigured: true,
                virtualObserverCell: observerCell);
        }
        else
        {
            PodeDetectarSensor.CollectVisibleCells(
                request.Observer,
                request.Map,
                request.TerrainDatabase,
                result.VisibleCells,
                request.DpqAirHeightConfig,
                request.EnableLos,
                enableSpotter: false,
                useOccupantLayerForTarget: false,
                preserveObserverLayerRangeForHexVisibility: false,
                forceVirtualTargetLayer: true,
                forcedVirtualTargetDomain: layer.Domain,
                forcedVirtualTargetHeight: layer.Height,
                useRangeOnlyForAirHighWhenConfigured: true,
                virtualObserverCell: observerCell);
        }

        if (!layer.IsAll
            && request.Observer.TryGetUnitData(out UnitData data)
            && data != null)
        {
            result.DetectsStealth = data.HasStealthDetectionFor(
                layer.Domain,
                layer.Height);
        }

        result.Diagnostic =
            $"camada={layer.Label} origem={observerCell.x},{observerCell.y} "
            + $"visiveis={result.VisibleCount} stealth={result.DetectsStealth}";
        AIDecisionPerf.AddCount(
            "VisionCoverageCellsProduced",
            result.VisibleCount);
        return result;
    }
}
