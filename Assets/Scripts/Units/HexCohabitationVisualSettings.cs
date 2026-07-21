using UnityEngine;

// Arranjo visual do hex compartilhado. Cada andar de ocupacao (aereo, superficie,
// submerso) comporta uma presenca, entao os offsets posicionam a FILEIRA do andar,
// nunca uma unidade especifica.
//
// O arranjo bom para tres fileiras nao serve para duas: o espaco vertical do hex
// se divide de forma diferente em cada caso. Por isso cada combinacao possivel tem
// o proprio conjunto de offsets, tunavel de forma independente.
public class HexCohabitationVisualSettings : MonoBehaviour
{
    // Cada participante tem o proprio ajuste de HUD: <andar>HudY desce (Y negativo)
    // ou sobe o coracao E o numero de HP juntos, que sao irmaos na hierarquia da
    // ficha. Serve para achatar a ficha daquele andar sem mexer nos outros.
    [System.Serializable]
    public class AirSurfaceLayout
    {
        public Vector2 air = new Vector2(-0.1f, 0.2f);
        [Range(-3f, 1f)] public float airHudY = 0f;
        public Vector2 surface = new Vector2(0f, -0.2f);
        [Range(-3f, 1f)] public float surfaceHudY = 0f;
    }

    [System.Serializable]
    public class AirSubmergedLayout
    {
        public Vector2 air = new Vector2(-0.1f, 0.2f);
        [Range(-3f, 1f)] public float airHudY = 0f;
        public Vector2 submerged = new Vector2(0f, -0.2f);
        [Range(-3f, 1f)] public float submergedHudY = 0f;
    }

    [System.Serializable]
    public class SurfaceSubmergedLayout
    {
        public Vector2 surface = new Vector2(0f, 0.15f);
        [Range(-3f, 1f)] public float surfaceHudY = 0f;
        public Vector2 submerged = new Vector2(0f, -0.25f);
        [Range(-3f, 1f)] public float submergedHudY = 0f;
    }

    [System.Serializable]
    public class FullStackLayout
    {
        public Vector2 air = new Vector2(-0.1f, 0.3f);
        [Range(-3f, 1f)] public float airHudY = 0f;
        public Vector2 surface = new Vector2(0f, 0f);
        [Range(-3f, 1f)] public float surfaceHudY = 0f;
        public Vector2 submerged = new Vector2(0f, -0.32f);
        [Range(-3f, 1f)] public float submergedHudY = 0f;
    }

    [Header("Aéreo + Superfície")]
    public AirSurfaceLayout airSurface = new AirSurfaceLayout();

    [Header("Aéreo + Submerso")]
    public AirSubmergedLayout airSubmerged = new AirSubmergedLayout();

    [Header("Superfície + Submerso")]
    public SurfaceSubmergedLayout surfaceSubmerged = new SurfaceSubmergedLayout();

    [Header("Aéreo + Superfície + Submerso")]
    public FullStackLayout fullStack = new FullStackLayout();

    [Header("Escala compartilhada")]
    [Range(0.3f, 1f)]
    public float scale = 0.6f;

    [Header("Espalhamento por linha (duas unidades no MESMO andar, hex contestado)")]
    [Range(0f, 0.5f)]
    public float intraLayerSpread = 0.18f;

    private void OnEnable() => Push();

    private void OnValidate() => Push();

    private void Push()
    {
        HexCohabitationVisualManager.AirSurface = new HexCohabitationVisualManager.LayerOffsets(
            airSurface.air, airSurface.airHudY,
            airSurface.surface, airSurface.surfaceHudY,
            Vector2.zero, 0f);
        HexCohabitationVisualManager.AirSubmerged = new HexCohabitationVisualManager.LayerOffsets(
            airSubmerged.air, airSubmerged.airHudY,
            Vector2.zero, 0f,
            airSubmerged.submerged, airSubmerged.submergedHudY);
        HexCohabitationVisualManager.SurfaceSubmerged = new HexCohabitationVisualManager.LayerOffsets(
            Vector2.zero, 0f,
            surfaceSubmerged.surface, surfaceSubmerged.surfaceHudY,
            surfaceSubmerged.submerged, surfaceSubmerged.submergedHudY);
        HexCohabitationVisualManager.FullStack = new HexCohabitationVisualManager.LayerOffsets(
            fullStack.air, fullStack.airHudY,
            fullStack.surface, fullStack.surfaceHudY,
            fullStack.submerged, fullStack.submergedHudY);

        HexCohabitationVisualManager.SharedScale = new Vector3(scale, scale, 1f);
        HexCohabitationVisualManager.IntraLayerSpread = intraLayerSpread;

        if (Application.isPlaying)
            HexCohabitationVisualManager.ScanAllCells();
    }
}
