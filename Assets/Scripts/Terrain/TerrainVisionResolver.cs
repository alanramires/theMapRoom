using UnityEngine;

public static class TerrainVisionResolver
{
    public static void Resolve(
        TerrainTypeData terrain,
        Domain activeDomain,
        HeightLevel activeHeightLevel,
        DPQAirHeightConfig dpqAirHeightConfig,
        ConstructionData constructionData,
        StructureData structureData,
        out int ev,
        out bool blockLoS)
    {
        int terrainEv = terrain != null ? Mathf.Max(0, terrain.ev) : 0;
        bool terrainBlocks = terrain == null || terrain.blockLoS;

        int constructionEv = 0;
        bool constructionBlocks = false;
        bool hasConstructionOverride = false;
        int structureEv = 0;
        bool structureBlocks = false;
        bool hasStructureOverride = false;

        if (terrain != null)
        {
            if (constructionData != null
                && terrain.TryGetConstructionVisionOverride(constructionData, out int resolvedConstructionEv, out bool constructionBlocksLoS))
            {
                hasConstructionOverride = true;
                constructionEv = Mathf.Max(0, resolvedConstructionEv);
                constructionBlocks = constructionBlocksLoS;
            }
            
            if (structureData != null
                && terrain.TryGetStructureVisionOverride(structureData, out int resolvedStructureEv, out bool structureBlocksLoS))
            {
                hasStructureOverride = true;
                structureEv = Mathf.Max(0, resolvedStructureEv);
                structureBlocks = structureBlocksLoS;
            }
        }

        int composedEv = terrainEv;
        bool composedBlocks = terrainBlocks;
        if (hasConstructionOverride)
        {
            composedEv = Mathf.Max(composedEv, constructionEv);
            composedBlocks |= constructionBlocks;
        }
        if (hasStructureOverride)
        {
            composedEv = Mathf.Max(composedEv, structureEv);
            composedBlocks |= structureBlocks;
        }

        if (activeDomain == Domain.Air
            && dpqAirHeightConfig != null
            && dpqAirHeightConfig.TryGetVisionFor(activeDomain, activeHeightLevel, out int airEv, out bool airBlockLoS))
        {
            composedEv = Mathf.Max(composedEv, Mathf.Max(0, airEv));
            composedBlocks |= airBlockLoS;
        }

        ev = composedEv;
        blockLoS = composedBlocks;
    }
}
