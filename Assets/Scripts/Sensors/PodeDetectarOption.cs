using UnityEngine;

public sealed class PodeDetectarOption
{
    public UnitManager observerUnit;
    public UnitManager targetUnit;
    public Vector3Int observerCell;
    public Vector3Int targetCell;
    public int distance;
    public Domain targetDomain;
    public HeightLevel targetHeightLevel;
    public int detectionRangeUsed;
    public bool hasDirectLos;
    public bool usedForwardObserver;
    public UnitManager forwardObserverUnit;

    /// <summary>
    /// A reta que decidiu esta deteccao, inteira: de onde partiu, por onde
    /// passou, ate onde subiu e contra o que parou.
    ///
    /// Vem do mesmo traçado que decidiu — nao de um calculo paralelo, que foi
    /// como ferramenta e jogo ja discordaram uma vez. Quem transforma isto em
    /// texto e o <see cref="ObservationLineReport"/>, o mesmo que atende o
    /// PodeEnxergar: mesma reta, mesmo relatorio.
    /// </summary>
    public ObservationLineProfile lineProfile = new ObservationLineProfile();

    public string reason;
}
