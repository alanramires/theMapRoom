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
}

