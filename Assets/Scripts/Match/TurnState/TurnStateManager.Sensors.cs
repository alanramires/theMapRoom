using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly List<char> availableSensorActionCodes = new List<char>();
    private readonly List<PodeMirarTargetOption> cachedPodeMirarTargets = new List<PodeMirarTargetOption>();

    public IReadOnlyList<char> AvailableSensorActionCodes => availableSensorActionCodes;
    public IReadOnlyList<PodeMirarTargetOption> CachedPodeMirarTargets => cachedPodeMirarTargets;

    private void RefreshSensorsForCurrentState()
    {
        if (selectedUnit == null)
        {
            ClearSensorResults();
            return;
        }

        SensorMovementMode movementMode;
        if (cursorState == CursorState.MoveuAndando)
            movementMode = SensorMovementMode.MoveuAndando;
        else if (cursorState == CursorState.MoveuParado)
            movementMode = SensorMovementMode.MoveuParado;
        else
        {
            ClearSensorResults();
            return;
        }

        bool hasFireCandidateWeapon = PodeMirarSensor.HasAnyFireCandidateWeapon(selectedUnit, movementMode);
        if (hasFireCandidateWeapon)
        {
            ClearMovementRange(keepCommittedMovement: true);
            PaintLineOfFireArea(movementMode);
        }
        else
        {
            ClearLineOfFireArea();
            if (paintedRangeCells.Count == 0)
                PaintSelectedUnitMovementRange();
        }

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        SensorHandle.RunAll(
            selectedUnit,
            boardMap,
            terrainDatabase,
            weaponPriorityData,
            movementMode,
            availableSensorActionCodes,
            cachedPodeMirarTargets);

        ResetScannerPromptState();
        LogScannerPanel();
    }

    private void ClearSensorResults()
    {
        ResetScannerPromptState();
        availableSensorActionCodes.Clear();
        cachedPodeMirarTargets.Clear();
        ClearLineOfFireArea();
    }

    private void LogScannerPanel()
    {
        bool podeMirar = availableSensorActionCodes.Contains('A');
        bool podeEmbarcar = availableSensorActionCodes.Contains('E');

        string painel =
            "Resultado dos Scanners\n" +
            $"Pode Mirar (\"A\"): {(podeMirar ? "sim" : "nao")}\n" +
            $"Pode Embarcar (\"E\"): {(podeEmbarcar ? "sim" : "nao")}\n" +
            "Apenas Mover (\"M\")\n" +
            "Desfazer Movimento (ESC)\n\n" +
            ">> digite a acao desejada";

        Debug.Log(painel);
    }
}
