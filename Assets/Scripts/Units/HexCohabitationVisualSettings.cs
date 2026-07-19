using UnityEngine;

public class HexCohabitationVisualSettings : MonoBehaviour
{
    [Header("Air Unit (helicóptero, avião)")]
    public Vector2 airOffset = new Vector2(-0.1f, 0.2f);

    [Header("Surface Unit (tanque, infantaria)")]
    public Vector2 surfaceOffset = new Vector2(0f, -0.2f);

    [Header("Escala compartilhada")]
    [Range(0.3f, 1f)]
    public float scale = 0.6f;

    [Header("Espalhamento por linha (usado quando ha superficie ou 3+ aereos)")]
    [Range(0f, 0.5f)]
    public float intraLayerSpread = 0.18f;

    private void OnEnable() => Push();

    private void OnValidate() => Push();

    private void Push()
    {
        HexCohabitationVisualManager.AirOffset = new Vector3(airOffset.x, airOffset.y, 0f);
        HexCohabitationVisualManager.SurfaceOffset = new Vector3(surfaceOffset.x, surfaceOffset.y, 0f);
        HexCohabitationVisualManager.SharedScale = new Vector3(scale, scale, 1f);
        HexCohabitationVisualManager.IntraLayerSpread = intraLayerSpread;

        if (Application.isPlaying)
            HexCohabitationVisualManager.ScanAllCells();
    }
}
