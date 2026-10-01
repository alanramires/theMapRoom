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
        out float ev,
        out bool blockLoS)
    {
        float composedEv = terrain != null ? Mathf.Max(0f, terrain.ev) : 0f;
        bool composedBlocks = terrain == null || terrain.blockLoS;

        // A construcao OCUPA o hex: a altura dela SUBSTITUI a do terreno, como a do
        // soldado substituiria. Cidade na montanha = 2 (emprestado), nao 2,25.
        //
        // So quem bloqueia ocupa. Construcao sem Block LoS (flag, marco) e
        // marcador: deixa o terreno como esta — uma flag na floresta nao derruba
        // a arvore.
        if (constructionData != null && constructionData.blockLoS)
        {
            composedEv = ResolveConstructionHeight(terrain, constructionData);
            composedBlocks = true;
        }

        if (terrain != null
            && structureData != null
            && terrain.TryGetStructureVisionOverride(structureData, out int structureEv, out bool structureBlocksLoS))
        {
            composedEv = Mathf.Max(composedEv, Mathf.Max(0, structureEv));
            composedBlocks |= structureBlocksLoS;
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

    /// <summary>
    /// Altura da construcao no hex. A MESMA regra da unidade
    /// (ObservationLineService.ResolveOriginEv): se o terreno empresta EV a quem o
    /// ocupa, vale o emprestado; senao, vale a altura do proprio ocupante.
    ///
    ///   cidade na planicie  (nao empresta)   EV base 1
    ///   cidade na floresta  (nao empresta)   EV base 1
    ///   cidade na montanha  (empresta 2)     2 — na base, nao no cume
    ///
    /// E a altura do HEX inteiro quando a construcao bloqueia (ver Resolve).
    /// </summary>
    public static float ResolveConstructionHeight(TerrainTypeData terrain, ConstructionData constructionData)
    {
        if (constructionData == null)
            return 0f;
        if (terrain != null && terrain.shooterInheritsTerrainEv)
            return terrain.ResolveShooterInheritedEv();
        return Mathf.Max(0f, constructionData.ev);
    }
}
