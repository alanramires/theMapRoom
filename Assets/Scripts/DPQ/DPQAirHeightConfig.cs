using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Game/DPQ/DPQ Air Height Config", fileName = "DPQAirHeightConfig")]
public class DPQAirHeightConfig : ScriptableObject
{
    [Header("Air DPQ")]
    [Tooltip("DPQ usado por unidades em Domain.Air + HeightLevel.AirLow.")]
    public DPQData airLowDpq;

    [Tooltip("DPQ usado por unidades em Domain.Air + HeightLevel.AirHigh.")]
    public DPQData airHighDpq;

    [Tooltip("Fallback opcional quando AirLow/AirHigh nao estiver configurado.")]
    public DPQData fallbackDpq;

    [Header("Air Tiles")]
    [Tooltip("Tile usado para Domain.Air + HeightLevel.AirLow.")]
    public TileBase airLowTile;

    [Tooltip("Tile usado para Domain.Air + HeightLevel.AirHigh.")]
    public TileBase airHighTile;

    [Header("Air Display Names")]
    [Tooltip("Nome de exibicao para Domain.Air + HeightLevel.AirLow.")]
    public string airLowDisplayName;

    [Tooltip("Nome de exibicao para Domain.Air + HeightLevel.AirHigh.")]
    public string airHighDisplayName;

    [Header("Air Vision")]
    [Tooltip("EV para Domain.Air + HeightLevel.AirLow.")]
    public int airLowEv = 3;

    [Tooltip("EV para Domain.Air + HeightLevel.AirHigh.")]
    public int airHighEv = 4;

    [Tooltip("EV fallback opcional para camada de ar sem config especifica.")]
    public int fallbackEv = 0;

    [Tooltip("Block LoS para Domain.Air + HeightLevel.AirLow.")]
    public bool airLowBlockLoS = true;

    [Tooltip("Block LoS para Domain.Air + HeightLevel.AirHigh.")]
    public bool airHighBlockLoS = false;

    [Tooltip("Block LoS fallback opcional para camada de ar sem config especifica.")]
    public bool fallbackBlockLoS = true;

    // Nao tem relacao com air height, mas aproveitamos este asset para guardar a config
    // do submarino (Domain.Submarine + HeightLevel.Submerged, camada unica).
    [Header("Submar")]
    [Tooltip("DPQ usado por unidades em Domain.Submarine + HeightLevel.Submerged.")]
    public DPQData subDpq;

    [Tooltip("Tile usado para Domain.Submarine + HeightLevel.Submerged.")]
    public TileBase subTile;

    [Tooltip("Nome de exibicao para Domain.Submarine + HeightLevel.Submerged.")]
    public string subDisplayName;

    [Tooltip("EV para Domain.Submarine + HeightLevel.Submerged.")]
    public int subEv = 2;

    [Tooltip("Block LoS para Domain.Submarine + HeightLevel.Submerged.")]
    public bool subBlockLoS;

    public bool TryGetFor(Domain domain, HeightLevel heightLevel, out DPQData dpq)
    {
        if (domain == Domain.Air)
        {
            if (heightLevel == HeightLevel.AirLow)
            {
                dpq = airLowDpq != null ? airLowDpq : fallbackDpq;
                return dpq != null;
            }

            if (heightLevel == HeightLevel.AirHigh)
            {
                dpq = airHighDpq != null ? airHighDpq : fallbackDpq;
                return dpq != null;
            }
        }

        if (domain == Domain.Submarine && heightLevel == HeightLevel.Submerged)
        {
            dpq = subDpq;
            return dpq != null;
        }

        dpq = null;
        return false;
    }

    public bool TryGetVisionFor(Domain domain, HeightLevel heightLevel, out int ev, out bool blockLoS)
    {
        if (domain == Domain.Air)
        {
            if (heightLevel == HeightLevel.AirLow)
            {
                ev = airLowEv;
                blockLoS = airLowBlockLoS;
                return true;
            }

            if (heightLevel == HeightLevel.AirHigh)
            {
                ev = airHighEv;
                blockLoS = airHighBlockLoS;
                return true;
            }
        }

        if (domain == Domain.Submarine && heightLevel == HeightLevel.Submerged)
        {
            ev = subEv;
            blockLoS = subBlockLoS;
            return true;
        }

        ev = fallbackEv;
        blockLoS = fallbackBlockLoS;
        return true;
    }

    private void OnValidate()
    {
        if (airLowEv < 0)
            airLowEv = 0;
        if (airHighEv < 0)
            airHighEv = 0;
        if (fallbackEv < 0)
            fallbackEv = 0;
        if (subEv < 0)
            subEv = 0;
    }
}
