using UnityEngine;

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

        dpq = fallbackDpq;
        return dpq != null;
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
    }
}
