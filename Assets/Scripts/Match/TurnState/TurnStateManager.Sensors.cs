using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly List<char> availableSensorActionCodes = new List<char>();
    private readonly List<PodeMirarTargetOption> cachedPodeMirarTargets = new List<PodeMirarTargetOption>();
    private readonly List<PodeEmbarcarOption> cachedPodeEmbarcarTargets = new List<PodeEmbarcarOption>();

    public IReadOnlyList<char> AvailableSensorActionCodes => availableSensorActionCodes;
    public IReadOnlyList<PodeMirarTargetOption> CachedPodeMirarTargets => cachedPodeMirarTargets;
    public IReadOnlyList<PodeEmbarcarOption> CachedPodeEmbarcarTargets => cachedPodeEmbarcarTargets;

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
        if (selectedUnit.IsAircraftGrounded || selectedUnit.IsAircraftEmbarkedInCarrier || selectedUnit.AircraftOperationLockTurns > 0)
            hasFireCandidateWeapon = false;
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
        int remainingMovementPoints = ComputeRemainingMovementPointsForSensors(movementMode);
        SensorHandle.RunAll(
            selectedUnit,
            boardMap,
            terrainDatabase,
            weaponPriorityData,
            dpqAirHeightConfig,
            IsFogOfWarEnabled(),
            movementMode,
            remainingMovementPoints,
            availableSensorActionCodes,
            cachedPodeMirarTargets,
            cachedPodeEmbarcarTargets);

        cachedAircraftOperationDecision = AircraftOperationRules.Evaluate(
            selectedUnit,
            boardMap,
            terrainDatabase,
            movementMode);
        if (cachedAircraftOperationDecision.available && !availableSensorActionCodes.Contains('L'))
            availableSensorActionCodes.Add('L');

        if (selectedUnit.AircraftOperationLockTurns > 0)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
        }

        if (selectedUnit.IsAircraftGrounded || selectedUnit.IsAircraftEmbarkedInCarrier)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
        }

        ResetScannerPromptState();
        LogScannerPanel();
    }

    private void ClearSensorResults()
    {
        ResetScannerPromptState();
        availableSensorActionCodes.Clear();
        cachedPodeMirarTargets.Clear();
        cachedPodeEmbarcarTargets.Clear();
        cachedAircraftOperationDecision = default;
        ClearLineOfFireArea();
    }

    private int ComputeRemainingMovementPointsForSensors(SensorMovementMode movementMode)
    {
        if (selectedUnit == null)
            return 0;

        int baseMove = Mathf.Max(0, selectedUnit.GetMovementRange());
        if (movementMode == SensorMovementMode.MoveuParado)
            return baseMove;

        if (movementMode == SensorMovementMode.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
        {
            Tilemap boardMap = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
            int spent = UnitMovementPathRules.CalculateAutonomyCostForPath(
                boardMap,
                selectedUnit,
                committedMovementPath,
                terrainDatabase);
            return Mathf.Max(0, baseMove - Mathf.Max(0, spent));
        }

        return baseMove;
    }

    private void LogScannerPanel()
    {
        bool podeMirar = availableSensorActionCodes.Contains('A');
        bool podeEmbarcar = availableSensorActionCodes.Contains('E');
        bool podeOperacaoAerea = availableSensorActionCodes.Contains('L');

        string painel =
            "Resultado dos Scanners\n" +
            $"Pode Mirar (\"A\"): {(podeMirar ? "sim" : "nao")}\n" +
            $"Pode Embarcar (\"E\"): {(podeEmbarcar ? "sim" : "nao")}\n" +
            $"Operacao Aerea (\"L\"): {(podeOperacaoAerea ? "sim" : "nao")}\n" +
            "Apenas Mover (\"M\")\n" +
            "Desfazer Movimento (ESC)\n\n" +
            ">> digite a acao desejada";

        if (cachedAircraftOperationDecision.available)
            painel += $"\nL: {cachedAircraftOperationDecision.label}";
        else if (!string.IsNullOrWhiteSpace(cachedAircraftOperationDecision.reason))
            painel += $"\nL indisponivel: {cachedAircraftOperationDecision.reason}";

        if (cachedPodeEmbarcarTargets.Count > 0)
            painel += $"\nE opcoes: {cachedPodeEmbarcarTargets.Count}";

        Debug.Log(painel);
    }
}
