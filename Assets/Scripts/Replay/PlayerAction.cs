using System;
using UnityEngine;

[Serializable]
public class PlayerAction
{
    public PlayerActionType ActionType;
    public int TurnNumber;
    public TeamId ActingTeam;

    public Vector3Int CursorHex;

    public string UnitInstanceId;

    public Vector3Int MoveFrom;
    public Vector3Int MoveTo;
    public UnitLayerMode LayerBefore;
    public UnitLayerMode LayerAfter;

    public SensorActionType SensorAction;

    public string TargetInstanceId;
    public string TargetConstructionId;
    public Vector3Int TargetHex;

    public string SubStepLabel;

    public bool Confirmed;

    // Snapshot do estado do mapa associado a esta acao na timeline.
    public TurnStartSnapshot Snapshot;

    public bool IsTurnMarker;
    public string DebugLabel;
}

