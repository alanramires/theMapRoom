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
    private readonly List<PodeSuprirOption> cachedPodeSuprirTargets = new List<PodeSuprirOption>();
    private readonly List<PodeSuprirInvalidOption> cachedPodeSuprirInvalidTargets = new List<PodeSuprirInvalidOption>();
    private readonly List<PodeTransferirOption> cachedPodeTransferirTargets = new List<PodeTransferirOption>();
    private readonly List<PodeTransferirInvalidOption> cachedPodeTransferirInvalidTargets = new List<PodeTransferirInvalidOption>();
    private readonly List<PodeFundirOption> cachedPodeFundirTargets = new List<PodeFundirOption>();
    private readonly List<PodeFundirInvalidOption> cachedPodeFundirInvalidTargets = new List<PodeFundirInvalidOption>();
    private ConstructionManager cachedPodeCapturarConstruction;
    private string cachedPodeCapturarReason = string.Empty;
    private int cachedPodeFundirAdjacentCount;
    private string cachedPodeFundirReason = string.Empty;
    private string cachedPodeSuprirReason = string.Empty;
    private string cachedPodeTransferirReason = string.Empty;

    public IReadOnlyList<char> AvailableSensorActionCodes => availableSensorActionCodes;
    public IReadOnlyList<PodeMirarTargetOption> CachedPodeMirarTargets => cachedPodeMirarTargets;
    public IReadOnlyList<PodeMirarInvalidOption> CachedPodeMirarInvalidTargets => cachedPodeMirarInvalidTargets;
    public IReadOnlyList<PodeEmbarcarOption> CachedPodeEmbarcarTargets => cachedPodeEmbarcarTargets;
    public IReadOnlyList<PodeEmbarcarInvalidOption> CachedPodeEmbarcarInvalidTargets => cachedPodeEmbarcarInvalidTargets;
    public IReadOnlyList<PodeDesembarcarOption> CachedPodeDesembarcarTargets => cachedPodeDesembarcarTargets;
    public IReadOnlyList<PodeDesembarcarInvalidOption> CachedPodeDesembarcarInvalidTargets => cachedPodeDesembarcarInvalidTargets;
    public IReadOnlyList<PodeSuprirOption> CachedPodeSuprirTargets => cachedPodeSuprirTargets;
    public IReadOnlyList<PodeSuprirInvalidOption> CachedPodeSuprirInvalidTargets => cachedPodeSuprirInvalidTargets;
    public IReadOnlyList<PodeTransferirOption> CachedPodeTransferirTargets => cachedPodeTransferirTargets;
    public IReadOnlyList<PodeTransferirInvalidOption> CachedPodeTransferirInvalidTargets => cachedPodeTransferirInvalidTargets;

    private void RefreshSensorsForCurrentState()
    {
        double perfStart = Time.realtimeSinceStartupAsDouble;
        try
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

            // Nao pinta mais area automatica de linha de tiro ao final do movimento.
            // A visualizacao de ameaca agora fica restrita aos fluxos de inspecao.
            ClearLineOfFireArea();
            if (paintedRangeCells.Count == 0)
                PaintSelectedUnitMovementRange();

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
            CollapseMirarTargetsByTargetUnit(cachedPodeMirarTargets);

        // Normaliza os codigos de acao com base nos resultados efetivos dos sensores.
        availableSensorActionCodes.Remove('A');
        if (cachedPodeMirarTargets.Count > 0 || cachedPodeMirarInvalidTargets.Count > 0)
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
        availableSensorActionCodes.Remove('L');

        bool canCapture = PodeCapturarSensor.TryGetCaptureTarget(
            selectedUnit,
            boardMap,
            movementMode,
            out cachedPodeCapturarConstruction,
            out cachedPodeCapturarReason);
        availableSensorActionCodes.Remove('C');
        if (canCapture)
            availableSensorActionCodes.Add('C');

        bool canMerge = PodeFundirSensor.CollectOptions(
            selectedUnit,
            boardMap,
            terrainDatabase,
            cachedPodeFundirTargets,
            out cachedPodeFundirReason,
            cachedPodeFundirInvalidTargets);
        cachedPodeFundirAdjacentCount = cachedPodeFundirTargets.Count;
        if (!canMerge)
            cachedPodeFundirReason = string.IsNullOrWhiteSpace(cachedPodeFundirReason) ? "Sem candidatos para fusao." : cachedPodeFundirReason;
        availableSensorActionCodes.Remove('F');
        if (canMerge)
            availableSensorActionCodes.Add('F');

        bool canSupply = PodeSuprirSensor.CollectOptions(
            selectedUnit,
            boardMap,
            terrainDatabase,
            cachedPodeSuprirTargets,
            out cachedPodeSuprirReason,
            cachedPodeSuprirInvalidTargets);
        availableSensorActionCodes.Remove('S');
        if (canSupply)
            availableSensorActionCodes.Add('S');

        bool canTransfer = PodeTransferirSensor.CollectOptions(
            selectedUnit,
            boardMap,
            cachedPodeTransferirTargets,
            out cachedPodeTransferirReason,
            cachedPodeTransferirInvalidTargets);
        availableSensorActionCodes.Remove('T');
        if (canTransfer)
            availableSensorActionCodes.Add('T');

        bool isContestedHexTotalWar = IsSelectedUnitInContestedHexTotalWar();
        if (isContestedHexTotalWar)
            ApplyContestedHexActionRestrictions();

        if (isContestedHexTotalWar && movementMode == SensorMovementMode.MoveuAndando)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
            cachedPodeMirarInvalidTargets.Clear();
        }

        if (selectedUnit.AircraftOperationLockTurns > 0)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
            cachedPodeMirarInvalidTargets.Clear();
        }

        if (selectedUnit.IsEmbarked)
        {
            availableSensorActionCodes.Remove('A');
            cachedPodeMirarTargets.Clear();
            cachedPodeMirarInvalidTargets.Clear();
        }

            RefreshEnemyThreatLayersOverlayIfEnabled();
            ResetScannerPromptState();
            LogScannerPanel();
        }
        finally
        {
            RegisterPerfSensorsDuration((Time.realtimeSinceStartupAsDouble - perfStart) * 1000d);
        }
    }

    private bool IsSelectedUnitInContestedHexTotalWar()
    {
        if (selectedUnit == null || !UnitRulesDefinition.IsTotalWarEnabled())
            return false;

        Vector3Int selectedCell = selectedUnit.CurrentCellPosition;
        selectedCell.z = 0;

        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager candidate = units[i];
            if (candidate == null || candidate == selectedUnit || !candidate.gameObject.activeInHierarchy || candidate.IsEmbarked)
                continue;
            if (candidate.TeamId == selectedUnit.TeamId)
                continue;

            Vector3Int candidateCell = candidate.CurrentCellPosition;
            candidateCell.z = 0;
            if (candidateCell == selectedCell)
                return true;
        }

        return false;
    }

    private void ApplyContestedHexActionRestrictions()
    {
        const string contestedReason = "Hex disputado: acao indisponivel.";

        availableSensorActionCodes.Remove('C');
        cachedPodeCapturarConstruction = null;
        cachedPodeCapturarReason = contestedReason;

        availableSensorActionCodes.Remove('F');
        cachedPodeFundirTargets.Clear();
        cachedPodeFundirInvalidTargets.Clear();
        cachedPodeFundirAdjacentCount = 0;
        cachedPodeFundirReason = contestedReason;

        availableSensorActionCodes.Remove('E');
        cachedPodeEmbarcarTargets.Clear();
        cachedPodeEmbarcarInvalidTargets.Clear();

        availableSensorActionCodes.Remove('D');
        cachedPodeDesembarcarTargets.Clear();
        cachedPodeDesembarcarInvalidTargets.Clear();

        availableSensorActionCodes.Remove('S');
        cachedPodeSuprirTargets.Clear();
        cachedPodeSuprirInvalidTargets.Clear();
        cachedPodeSuprirReason = contestedReason;

        availableSensorActionCodes.Remove('T');
        cachedPodeTransferirTargets.Clear();
        cachedPodeTransferirInvalidTargets.Clear();
        cachedPodeTransferirReason = contestedReason;
    }

    private static void CollapseMirarTargetsByTargetUnit(List<PodeMirarTargetOption> options)
    {
        if (options == null || options.Count <= 1)
            return;

        List<PodeMirarTargetOption> filtered = new List<PodeMirarTargetOption>(options.Count);
        HashSet<UnitManager> seenTargets = new HashSet<UnitManager>();

        for (int i = 0; i < options.Count; i++)
        {
            PodeMirarTargetOption option = options[i];
            if (option == null)
                continue;

            UnitManager target = option.targetUnit;
            if (target == null)
            {
                filtered.Add(option);
                continue;
            }

            if (seenTargets.Add(target))
                filtered.Add(option);
        }

        options.Clear();
        options.AddRange(filtered);
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
        cachedPodeSuprirTargets.Clear();
        cachedPodeSuprirInvalidTargets.Clear();
        cachedPodeTransferirTargets.Clear();
        cachedPodeTransferirInvalidTargets.Clear();
        cachedPodeCapturarConstruction = null;
        cachedPodeCapturarReason = string.Empty;
        cachedPodeFundirAdjacentCount = 0;
        cachedPodeFundirReason = string.Empty;
        cachedPodeFundirTargets.Clear();
        cachedPodeFundirInvalidTargets.Clear();
        cachedPodeSuprirReason = string.Empty;
        cachedPodeTransferirReason = string.Empty;
        landingOptionUnavailableReason = string.Empty;
        cachedAircraftOperationDecision = default;
        ClearEnemyThreatLayersOverlay();
        ClearLineOfFireArea();
    }

    private int ComputeRemainingMovementPointsForSensors(SensorMovementMode movementMode)
    {
        if (selectedUnit == null)
            return 0;

        return Mathf.Max(0, selectedUnit.RemainingMovementPoints);
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
        bool podeLayersAmeaca = false;
        bool podeCapturar = availableSensorActionCodes.Contains('C');
        bool podeFundir = availableSensorActionCodes.Contains('F');
        bool podeSuprir = availableSensorActionCodes.Contains('S');
        bool podeTransferir = availableSensorActionCodes.Contains('T');

        Debug.Log(
            $"[Sensors] state={cursorState} | A={(podeMirar ? "sim" : "nao")} ({cachedPodeMirarTargets.Count}) | " +
            $"E={(podeEmbarcar ? "sim" : "nao")} ({cachedPodeEmbarcarTargets.Count}) | " +
            $"D={(podeDesembarcar ? "sim" : "nao")} ({cachedPodeDesembarcarTargets.Count}) | " +
            $"C={(podeCapturar ? "sim" : "nao")} | " +
            $"F={(podeFundir ? "sim" : "nao")} ({cachedPodeFundirAdjacentCount}) | " +
            $"S={(podeSuprir ? "sim" : "nao")} ({cachedPodeSuprirTargets.Count}) | " +
            $"T={(podeTransferir ? "sim" : "nao")} ({cachedPodeTransferirTargets.Count}) | " +
            $"L={(podeLayersAmeaca ? "sim" : "nao")}");

        string painel =
            "Resultado dos Scanners | " +
            $"Pode Mirar (\"A\"): {(podeMirar ? "sim" : "nao")} | " +
            $"Pode Embarcar (\"E\"): {(podeEmbarcar ? "sim" : "nao")} | " +
            $"Pode Desembarcar (\"D\"): {(podeDesembarcar ? "sim" : "nao")} | " +
            $"Pode Capturar (\"C\"): {(podeCapturar ? "sim" : "nao")} | " +
            $"Pode Fundir (\"F\"): {(podeFundir ? "sim" : "nao")} | " +
            $"Pode Suprir (\"S\"): {(podeSuprir ? "sim" : "nao")} | " +
            $"Pode Transferir (\"T\"): {(podeTransferir ? "sim" : "nao")} | " +
            $"Pode Layers de Ameaca (\"L\"): {(podeLayersAmeaca ? "sim" : "nao")} | " +
            "Apenas Mover (\"M\") | " +
            "Desfazer Movimento (ESC) | " +
            ">> digite a acao desejada";

        if (podeLayersAmeaca)
            painel += enemyThreatLayersEnabled
                ? "\nL: Ocultar layers de ameaca inimiga"
                : "\nL: Mostrar layers de ameaca inimiga";

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
        if (!podeCapturar && !string.IsNullOrWhiteSpace(cachedPodeCapturarReason))
            painel += $"\nC indisponivel: {cachedPodeCapturarReason}";
        if (podeFundir)
            painel += $"\nF elegiveis adjacentes (mesmo tipo): {cachedPodeFundirAdjacentCount}";
        else if (!string.IsNullOrWhiteSpace(cachedPodeFundirReason))
            painel += $"\nF indisponivel: {cachedPodeFundirReason}";
        if (podeSuprir)
            painel += $"\nS alvos validos: {cachedPodeSuprirTargets.Count}";
        else if (!string.IsNullOrWhiteSpace(cachedPodeSuprirReason))
            painel += $"\nS indisponivel: {cachedPodeSuprirReason}";
        if (podeTransferir)
            painel += $"\nT opcoes validas: {cachedPodeTransferirTargets.Count}";
        else if (!string.IsNullOrWhiteSpace(cachedPodeTransferirReason))
            painel += $"\nT indisponivel: {cachedPodeTransferirReason}";

        Debug.Log(painel);
    }

}
