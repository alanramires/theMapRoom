using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerActionSubStep
{
    public string Label;
    public string TargetInstanceId;
    public string TargetConstructionId;
    public Vector3Int TargetHex;
    public bool HasTargetHex;
}

[Serializable]
public class PlayerAction
{
    public PlayerActionType ActionType;
    public int TurnNumber;
    public TeamId ActingTeam;

    public Vector3Int CursorHex;
    public bool HasCursorHex;

    public string UnitInstanceId;

    public Vector3Int MoveFrom;
    public bool HasMoveFrom;
    public Vector3Int MoveTo;
    public bool HasMoveTo;
    public UnitLayerMode LayerBefore;
    public UnitLayerMode LayerAfter;

    public SensorActionType SensorAction;

    public string TargetInstanceId;
    public string TargetConstructionId;
    public Vector3Int TargetHex;
    public bool HasTargetHex;

    public int ShoppingSelectedIndex = -1;
    public string ShoppingUnitTypeId;

    public string SubStepLabel;
    public List<PlayerActionSubStep> SubSteps = new List<PlayerActionSubStep>();

    public bool Confirmed;

    // true quando a acao foi gerada pela IA (nao por input humano).
    // Usado para filtragem no debug e para anotacoes visuais no replay.
    public bool IsAIGenerated;

    // Snapshot do estado do mapa associado a esta acao na timeline.
    public TurnStartSnapshot Snapshot;

    public bool IsTurnMarker;
    public string DebugLabel;
}
