using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RoadRouteDefinition
{
    [Tooltip("Nome da rodovia/rota (ex.: BR-101).")]
    public string routeName = "Nova Rodovia";

    [Tooltip("Lista ordenada de hexes por onde a rodovia passa.")]
    public List<Vector3Int> cells = new List<Vector3Int>();
}
