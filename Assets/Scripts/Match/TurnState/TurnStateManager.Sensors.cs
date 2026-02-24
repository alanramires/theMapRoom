using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private readonly List<char> availableSensorActionCodes = new List<char>();
    private readonly List<PodeMirarTargetOption> cachedPodeMirarTargets = new List<PodeMirarTargetOption>();
    private readonly List<PodeMirarInvalidOption> cachedPodeMirarInvalidTargets = new List<PodeMirarInvalidOption>();
    private readonly List<PodeEmbarcarOption> cachedPodeEmbarcarTargets = new List<PodeEmbarcarOption>();
    private readonly List<PodeEmbarcarInvalidOption> cachedPodeEmbarcarInvalidTargets = new List<PodeEmbarcarInvalidOption>();
    private readonly List<PodeDesembarcarOption> cachedPodeDesembarcarTargets = new List<PodeDesembarcarOption>();
    private readonly List<PodeDesembarcarInvalidOption> cachedPodeDesembarcarInvalidTargets = new List<PodeDesembarcarInvalidOption>();

    public IReadOnlyList<char> AvailableSensorActionCodes => availableSensorActionCodes;
    public IReadOnlyList<PodeMirarTargetOption> CachedPodeMirarTargets => cachedPodeMirarTargets;
    public IReadOnlyList<PodeMirarInvalidOption> CachedPodeMirarInvalidTargets => cachedPodeMirarInvalidTargets;
    public IReadOnlyList<PodeEmbarcarOption> CachedPodeEmbarcarTargets => cachedPodeEmbarcarTargets;
    public IReadOnlyList<PodeEmbarcarInvalidOption> CachedPodeEmbarcarInvalidTargets => cachedPodeEmbarcarInvalidTargets;
    public IReadOnlyList<PodeDesembarcarOption> CachedPodeDesembarcarTargets => cachedPodeDesembarcarTargets;
    public IReadOnlyList<PodeDesembarcarInvalidOption> CachedPodeDesembarcarInvalidTargets => cachedPodeDesembarcarInvalidTargets;

    private void RefreshSensorsForCurrentState()
    {
        if (selectedUnit == null)
        {
            ClearSensorResults();
            return;
        }

        landingOptionUnavailableReason = string.Empty;

        if (!TryResolveSensorMovementModeForCurrentState(out SensorMovementMode movementMode))
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
            matchController != null ? matchController.EnableLdtValidation : true,
            matchController != null ? matchController.EnableLosValidation : true,
            matchController != null ? matchController.EnableSpotter : true,
            matchController != null ? matchController.EnableStealthValidation : true,
            movementMode,
            remainingMovementPoints,
            availableSensorActionCodes,
            cachedPodeMirarTargets,
            cachedPodeMirarInvalidTargets,
            cachedPodeEmbarcarTargets,
            cachedPodeEmbarcarInvalidTargets,
            cachedPodeDesembarcarTargets,
            cachedPodeDesembarcarInvalidTargets);

        // Normaliza os codigos de acao com base nos resultados efetivos dos sensores.
        availableSensorActionCodes.Remove('A');
        if (cachedPodeMirarTargets.Count > 0)
            availableSensorActionCodes.Add('A');

        availableSensorActionCodes.Remove('E');
        if (cachedPodeEmbarcarTargets.Count > 0)
            availableSensorActionCodes.Add('E');

        availableSensorActionCodes.Remove('D');
        if (cachedPodeDesembarcarTargets.Count > 0)
            availableSensorActionCodes.Add('D');

        cachedAircraftOperationDecision = AircraftOperationRules.Evaluate(
            selectedUnit,
            boardMap,
            terrainDatabase,
            movementMode);
        List<LandingOption> layerOperationOptions = new List<LandingOption>();
        bool hasLayerOperation = TryCollectLayerOperationOptions(selectedUnit, movementMode, layerOperationOptions, out string layerOperationReason);
        if (hasLayerOperation && !availableSensorActionCodes.Contains('L'))
            availableSensorActionCodes.Add('L');
        else if (!string.IsNullOrWhiteSpace(layerOperationReason))
            landingOptionUnavailableReason = layerOperationReason;

        if (selectedUnit.AircraftOperationLockTurns > 0)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
            cachedPodeMirarInvalidTargets.Clear();
        }

        if (selectedUnit.IsAircraftGrounded || selectedUnit.IsAircraftEmbarkedInCarrier)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
            cachedPodeMirarInvalidTargets.Clear();
        }

        ResetScannerPromptState();
        LogScannerPanel();
    }

    private void ClearSensorResults()
    {
        ResetScannerPromptState();
        availableSensorActionCodes.Clear();
        cachedPodeMirarTargets.Clear();
        cachedPodeMirarInvalidTargets.Clear();
        cachedPodeEmbarcarTargets.Clear();
        cachedPodeEmbarcarInvalidTargets.Clear();
        cachedPodeDesembarcarTargets.Clear();
        cachedPodeDesembarcarInvalidTargets.Clear();
        landingOptionUnavailableReason = string.Empty;
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
                terrainDatabase,
                applyOperationalAutonomyModifier: false);
            return Mathf.Max(0, baseMove - Mathf.Max(0, spent));
        }

        return baseMove;
    }

    private bool TryResolveSensorMovementModeForCurrentState(out SensorMovementMode mode)
    {
        if (cursorState == CursorState.MoveuAndando)
        {
            mode = SensorMovementMode.MoveuAndando;
            return true;
        }

        if (cursorState == CursorState.MoveuParado)
        {
            mode = SensorMovementMode.MoveuParado;
            return true;
        }

        if (cursorState == CursorState.Pousando)
        {
            mode = cursorStateBeforePousando == CursorState.MoveuAndando
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;
            return true;
        }

        if (cursorState == CursorState.Embarcando)
        {
            mode = cursorStateBeforeEmbarcando == CursorState.MoveuAndando
                ? SensorMovementMode.MoveuAndando
                : SensorMovementMode.MoveuParado;
            return true;
        }

        if (cursorState == CursorState.Desembarcando)
        {
            mode = SensorMovementMode.MoveuParado;
            return true;
        }

        mode = SensorMovementMode.MoveuParado;
        return false;
    }

    private void LogScannerPanel()
    {
        bool podeMirar = availableSensorActionCodes.Contains('A');
        bool podeEmbarcar = availableSensorActionCodes.Contains('E');
        bool podeDesembarcar = availableSensorActionCodes.Contains('D');
        bool podeMudarAltitude = availableSensorActionCodes.Contains('L');

        Debug.Log(
            $"[Sensors] state={cursorState} | A={(podeMirar ? "sim" : "nao")} ({cachedPodeMirarTargets.Count}) | " +
            $"E={(podeEmbarcar ? "sim" : "nao")} ({cachedPodeEmbarcarTargets.Count}) | " +
            $"D={(podeDesembarcar ? "sim" : "nao")} ({cachedPodeDesembarcarTargets.Count}) | " +
            $"L={(podeMudarAltitude ? "sim" : "nao")}");

        string painel =
            "Resultado dos Scanners | " +
            $"Pode Mirar (\"A\"): {(podeMirar ? "sim" : "nao")} | " +
            $"Pode Embarcar (\"E\"): {(podeEmbarcar ? "sim" : "nao")} | " +
            $"Pode Desembarcar (\"D\"): {(podeDesembarcar ? "sim" : "nao")} | " +
            $"Pode Mudar de Altitude (\"L\"): {(podeMudarAltitude ? "sim" : "nao")} | " +
            "Apenas Mover (\"M\") | " +
            "Desfazer Movimento (ESC) | " +
            ">> digite a acao desejada";

        if (podeMudarAltitude)
            painel += "\nL: Mudar Altitude";
        else if (cachedAircraftOperationDecision.available)
            painel += $"\nL: {cachedAircraftOperationDecision.label}";
        else if (!string.IsNullOrWhiteSpace(landingOptionUnavailableReason))
            painel += $"\nL indisponivel: {landingOptionUnavailableReason}";
        else if (!string.IsNullOrWhiteSpace(cachedAircraftOperationDecision.reason))
            painel += $"\nL indisponivel: {cachedAircraftOperationDecision.reason}";

        if (cachedPodeEmbarcarTargets.Count > 0)
            painel += $"\nE opcoes: {cachedPodeEmbarcarTargets.Count}";
        if (cachedPodeEmbarcarInvalidTargets.Count > 0)
            painel += $"\nE invalidos: {cachedPodeEmbarcarInvalidTargets.Count}";
        if (cachedPodeDesembarcarTargets.Count > 0)
            painel += $"\nD opcoes: {cachedPodeDesembarcarTargets.Count}";
        if (cachedPodeDesembarcarInvalidTargets.Count > 0)
            painel += $"\nD invalidos (placeholder): {cachedPodeDesembarcarInvalidTargets.Count}";
        if (!podeEmbarcar && cachedPodeEmbarcarInvalidTargets.Count > 0)
        {
            int detailCount = Mathf.Min(4, cachedPodeEmbarcarInvalidTargets.Count);
            painel += "\nE motivos (amostra):";
            for (int i = 0; i < detailCount; i++)
            {
                PodeEmbarcarInvalidOption invalid = cachedPodeEmbarcarInvalidTargets[i];
                if (invalid == null)
                    continue;

                string transporterName = invalid.transporterUnit != null
                    ? invalid.transporterUnit.name
                    : $"hex {invalid.evaluatedCell.x},{invalid.evaluatedCell.y}";
                string reason = !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
                painel += $"\n- {transporterName}: {reason}";
            }
        }
        if (cachedPodeDesembarcarInvalidTargets.Count > 0)
        {
            int detailCount = Mathf.Min(4, cachedPodeDesembarcarInvalidTargets.Count);
            painel += "\nD motivos (amostra):";
            for (int i = 0; i < detailCount; i++)
            {
                PodeDesembarcarInvalidOption invalid = cachedPodeDesembarcarInvalidTargets[i];
                if (invalid == null)
                    continue;

                string passengerName = invalid.passengerUnit != null ? invalid.passengerUnit.name : "(sem passageiro)";
                string reason = !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
                painel += $"\n- {passengerName} @ {invalid.evaluatedCell.x},{invalid.evaluatedCell.y}: {reason}";
            }
        }
        if (!podeMirar && cachedPodeMirarInvalidTargets.Count > 0)
        {
            int detailCount = Mathf.Min(6, cachedPodeMirarInvalidTargets.Count);
            painel += "\nA motivos (amostra):";
            for (int i = 0; i < detailCount; i++)
            {
                PodeMirarInvalidOption invalid = cachedPodeMirarInvalidTargets[i];
                if (invalid == null)
                    continue;

                string targetName = invalid.targetUnit != null ? invalid.targetUnit.name : "(alvo desconhecido)";
                string weaponName = invalid.weapon != null
                    ? (!string.IsNullOrWhiteSpace(invalid.weapon.displayName) ? invalid.weapon.displayName : invalid.weapon.name)
                    : "(arma desconhecida)";
                string reason = !string.IsNullOrWhiteSpace(invalid.reason) ? invalid.reason : "motivo nao informado";
                painel += $"\n- {targetName} | {weaponName} | dist={invalid.distance}: {reason}";
            }
        }

        Debug.Log(painel);
    }

}
