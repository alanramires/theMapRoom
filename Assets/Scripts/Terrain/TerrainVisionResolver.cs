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
        ev = terrain != null ? terrain.ev : 0;
        blockLoS = terrain == null || terrain.blockLoS;

        if (terrain != null)
        {
            if (constructionData != null
                && terrain.TryGetConstructionVisionOverride(constructionData, out int constructionEv, out bool constructionBlocksLoS))
            {
                ev = constructionEv;
                blockLoS = constructionBlocksLoS;
            }
            else if (structureData != null
                && terrain.TryGetStructureVisionOverride(structureData, out int structureEv, out bool structureBlocksLoS))
            {
                ev = structureEv;
                blockLoS = structureBlocksLoS;
            }
        }

        if (activeDomain == Domain.Air
            && dpqAirHeightConfig != null
            && dpqAirHeightConfig.TryGetVisionFor(activeDomain, activeHeightLevel, out int airEv, out bool airBlockLoS))
        {
            ev = airEv;
            blockLoS = airBlockLoS;
        }
    }
}
