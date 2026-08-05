using System.Collections.Generic;
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
    public List<Vector3Int> lineOfSightIntermediateCells = new List<Vector3Int>();

    /// <summary>
    /// Altura da linha em cada ponto: EV de origem, um valor por hex cruzado, e
    /// o EV do alvo. E o que diz se a linha SUBIU ou DESCEU — o radar terrestre
    /// olhando um caca alto sobe; o caca olhando o chao desce.
    ///
    /// Vem do mesmo tracado que decide a deteccao, nao de um calculo paralelo.
    /// </summary>
    public List<float> lineOfSightEvPath = new List<float>();

    public Vector3Int blockedCell = Vector3Int.zero;
    public string reason;

    /// <summary>EV de onde a linha partiu, e EV onde ela deveria chegar.</summary>
    public float lineOriginEv;
    public float lineTargetEv;
}
