using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CinematicEvent
{
    public Vector3Int CursorHex;
    public CinematicAction Action;
    public float DelayAfter;
    public string DebugLabel;
}

public enum CinematicAction
{
    None = 0,
    Confirm = 1,
    AimAction = 2
}

[Serializable]
public class CinematicTrack
{
    public List<CinematicEvent> Events = new List<CinematicEvent>();
}
