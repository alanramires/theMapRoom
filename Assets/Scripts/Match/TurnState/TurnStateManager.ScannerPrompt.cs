using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private enum ScannerPromptStep
    {
        AwaitingAction = 0,
        MirandoCycleTarget = 1,
        MirandoConfirmTarget = 2
    }

    private ScannerPromptStep scannerPromptStep = ScannerPromptStep.AwaitingAction;
    private int scannerSelectedTargetIndex = -1;
    private CursorState cursorStateBeforeMirando = CursorState.MoveuParado;
    private LineRenderer mirandoPreviewRenderer;
    private readonly List<Vector3> mirandoPreviewPathPoints = new List<Vector3>();
    private readonly List<Vector3> mirandoPreviewSegmentPoints = new List<Vector3>();
    private float mirandoPreviewPathLength;
    private float mirandoPreviewHeadDistance;
    private bool mirandoPreviewSignatureValid;
    private Vector3 mirandoPreviewLastFrom;
    private Vector3 mirandoPreviewLastTo;
    private WeaponTrajectoryType mirandoPreviewLastTrajectory;
    private float mirandoPreviewLastBend;
    private int mirandoPreviewLastSamples;

    private void Update()
    {
        ProcessScannerPromptInput();
        UpdateMirandoPreviewAnimation();
    }

    private void ResetScannerPromptState()
    {
        scannerPromptStep = ScannerPromptStep.AwaitingAction;
        scannerSelectedTargetIndex = -1;
        ClearMirandoPreview();
    }

    private bool HandleScannerPromptCancel()
    {
        if (cursorState == CursorState.Mirando && scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
        {
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            return true;
        }

        return false;
    }

    private void ProcessScannerPromptInput()
    {
        if (IsMovementAnimationRunning())
            return;

        if (cursorState == CursorState.Mirando)
            return;

        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        if (scannerPromptStep == ScannerPromptStep.AwaitingAction)
        {
            if (WasLetterPressedThisFrame('A'))
            {
                cursorController?.PlayConfirmSfx();
                HandleAimActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('E'))
            {
                HandleEmbarkActionRequested();
                return;
            }

            if (WasLetterPressedThisFrame('L'))
            {
                HandleAircraftOperationRequested();
                return;
            }

            if (WasLetterPressedThisFrame('M'))
            {
                HandleMoveOnlyActionRequested();
                return;
            }

            return;
        }

    }

    private void HandleAimActionRequested()
    {
        bool canAim = availableSensorActionCodes.Contains('A');
        if (!canAim || cachedPodeMirarTargets.Count == 0)
        {
            Debug.Log("Pode Mirar (\"A\"): nao ha alvos validos agora.");
            LogScannerPanel();
            return;
        }

        EnterMirandoState();
    }

    private void HandleMoveOnlyActionRequested()
    {
        bool finished = TryFinalizeSelectedUnitActionFromDebug();
        if (finished)
        {
            cursorController?.PlayDoneSfx();
            Debug.Log("[Acao] Apenas Mover (\"M\") confirmado. Unidade finalizou sem atacar.");
            ResetScannerPromptState();
            return;
        }

        Debug.Log("[Acao] Apenas Mover (\"M\") indisponivel no estado atual.");
    }

    private void HandleAircraftOperationRequested()
    {
        SensorMovementMode movementMode = cursorState == CursorState.MoveuAndando
            ? SensorMovementMode.MoveuAndando
            : SensorMovementMode.MoveuParado;

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (selectedUnit != null ? selectedUnit.BoardTilemap : null);
        if (!AircraftOperationRules.TryApplyOperation(selectedUnit, boardMap, terrainDatabase, movementMode, out AircraftOperationDecision decision))
        {
            string reason = !string.IsNullOrWhiteSpace(decision.reason) ? decision.reason : "Sem operacao aerea valida.";
            Debug.Log($"Operacao Aerea (\"L\"): {reason}");
            return;
        }

        Debug.Log($"[Operacao Aerea] {decision.label} executado.");

        if (decision.consumesAction)
        {
            bool finished = TryFinalizeSelectedUnitActionFromDebug();
            if (finished)
            {
                cursorController?.PlayDoneSfx();
                ResetScannerPromptState();
                return;
            }
        }

        cursorState = CursorState.UnitSelected;
        ClearSensorResults();
        PaintSelectedUnitMovementRange();
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }
    }

    private void HandleEmbarkActionRequested()
    {
        bool canEmbark = availableSensorActionCodes.Contains('E') && cachedPodeEmbarcarTargets.Count > 0;
        if (!canEmbark)
        {
            Debug.Log("Pode Embarcar (\"E\"): nao ha transportador valido adjacente.");
            LogScannerPanel();
            return;
        }

        string log = $"Pode Embarcar (\"E\"): {cachedPodeEmbarcarTargets.Count} opcao(oes) disponivel(is)\n";
        for (int i = 0; i < cachedPodeEmbarcarTargets.Count; i++)
        {
            PodeEmbarcarOption item = cachedPodeEmbarcarTargets[i];
            string label = item != null ? item.displayLabel : "(opcao invalida)";
            log += $"{i + 1}. {label}\n";
        }

        log += "(Acao de embarque ainda nao implementada nesta etapa.)";
        Debug.Log(log);
    }

    private void LogTargetSelectionPanel()
    {
        if (cachedPodeMirarTargets.Count == 0)
        {
            Debug.Log("Sem alvos validos para mirar.");
            return;
        }

        string text = $"Alvos validos retornados pelo sensor: {cachedPodeMirarTargets.Count}\n";
        text += "Mirando: setas alternam entre alvos validos\n";
        for (int i = 0; i < cachedPodeMirarTargets.Count; i++)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[i];
            string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
                ? option.displayLabel
                : (option != null && option.targetUnit != null ? option.targetUnit.name : "alvo");

            string revide = option != null && option.defenderCanCounterAttack ? "sim" : "nao";
            text += $"{i + 1}. {label} | revide: {revide}\n";
        }

        text += ">> Enter confirma | ESC volta";
        Debug.Log(text);
    }

    private void LogAttackConfirmationPrompt(PodeMirarTargetOption option, int shownIndex)
    {
        string label = option != null && !string.IsNullOrWhiteSpace(option.displayLabel)
            ? option.displayLabel
            : (option != null && option.targetUnit != null ? option.targetUnit.name : $"alvo {shownIndex}");

        Debug.Log($"Confirma alvo {shownIndex}? {label}\n(Enter=sim, ESC=voltar para ciclar)");
    }

    private void MoveCursorToTarget(PodeMirarTargetOption option)
    {
        if (option == null || option.targetUnit == null || cursorController == null)
            return;

        Vector3Int targetCell = option.targetUnit.CurrentCellPosition;
        targetCell.z = 0;
        cursorController.SetCell(targetCell, playMoveSfx: false);
    }

    private bool TryConfirmScannerAttack()
    {
        if (cursorState != CursorState.Mirando)
            return false;

        if (scannerPromptStep == ScannerPromptStep.MirandoCycleTarget)
        {
            if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
                return true;

            scannerPromptStep = ScannerPromptStep.MirandoConfirmTarget;
            PodeMirarTargetOption picked = cachedPodeMirarTargets[scannerSelectedTargetIndex];
            LogAttackConfirmationPrompt(picked, scannerSelectedTargetIndex + 1);
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.MirandoConfirmTarget)
            return false;

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
        {
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        if (option == null || option.attackerUnit == null || option.targetUnit == null)
        {
            Debug.Log("Falha ao confirmar ataque: opcao invalida.");
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            scannerSelectedTargetIndex = 0;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        CombatResolutionResult combat = ResolveCombatFromSelectedOption(option);
        Debug.Log(combat.trace);
        if (!combat.success)
        {
            Debug.Log("[Combate] Falha ao resolver combate. Retornando para selecao de alvo.");
            scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
            FocusCurrentMirandoTarget(logDetails: true);
            LogTargetSelectionPanel();
            return true;
        }

        cursorController?.PlayDoneSfx();
        TryFinalizeSelectedUnitActionFromDebug();
        ResetScannerPromptState();
        return true;
    }

    private void EnterMirandoState()
    {
        if (cursorState == CursorState.MoveuAndando || cursorState == CursorState.MoveuParado)
            cursorStateBeforeMirando = cursorState;

        // Ao sair do fluxo de movimento para mirar, oculta o rastro legado do caminho comprometido.
        ClearCommittedPathVisual();

        cursorState = CursorState.Mirando;
        scannerPromptStep = ScannerPromptStep.MirandoCycleTarget;
        scannerSelectedTargetIndex = 0;

        LogTargetSelectionPanel();
        FocusCurrentMirandoTarget(logDetails: true);
    }

    private void FocusCurrentMirandoTarget(bool logDetails, bool moveCursor = true)
    {
        if (cachedPodeMirarTargets.Count == 0)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            scannerSelectedTargetIndex = 0;

        PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
        if (moveCursor)
            MoveCursorToTarget(option);
        RebuildMirandoPreviewPath(option);
        SetMirandoPreviewVisible(cursorState == CursorState.Mirando);
        if (logDetails)
            LogCurrentMirandoTarget(option, scannerSelectedTargetIndex + 1, cachedPodeMirarTargets.Count);
    }

    private void LogCurrentMirandoTarget(PodeMirarTargetOption option, int shownIndex, int total)
    {
        if (option == null || option.targetUnit == null)
            return;

        UnitManager target = option.targetUnit;
        string attackWeapon = option.weapon != null ? option.weapon.displayName : "arma";
        string counterText = option.defenderCanCounterAttack ? "sim" : $"nao ({option.defenderCounterReason})";
        string label = !string.IsNullOrWhiteSpace(option.displayLabel) ? option.displayLabel : target.name;

        Debug.Log(
            $"[Mirando] Alvo {shownIndex}/{total}\n" +
            $"Label: {label}\n" +
            $"Unidade: {target.name}\n" +
            $"HP: {target.CurrentHP}\n" +
            $"Arma atacante: {attackWeapon}\n" +
            $"Revide: {counterText}\n" +
            "Use setas para trocar alvo. Enter confirma. ESC volta.");
    }

    private static int GetMirandoStepFromInput(Vector3Int inputDelta)
    {
        if (inputDelta.x > 0 || inputDelta.y < 0)
            return 1;
        if (inputDelta.x < 0 || inputDelta.y > 0)
            return -1;
        return 0;
    }

    private bool TryResolveMirandoCursorMove(Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = cursorController != null ? cursorController.CurrentCell : Vector3Int.zero;
        if (cursorState != CursorState.Mirando || cachedPodeMirarTargets.Count == 0)
            return false;
        if (scannerPromptStep == ScannerPromptStep.MirandoConfirmTarget)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = cachedPodeMirarTargets.Count;
        if (count <= 1)
            return false;

        scannerSelectedTargetIndex = (scannerSelectedTargetIndex + step + count) % count;
        FocusCurrentMirandoTarget(logDetails: true);

        if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < count)
        {
            UnitManager target = cachedPodeMirarTargets[scannerSelectedTargetIndex].targetUnit;
            if (target != null)
            {
                resolvedCell = target.CurrentCellPosition;
                resolvedCell.z = 0;
                return true;
            }
        }

        return false;
    }

    private void ExitMirandoStateToMovement()
    {
        if (cursorState != CursorState.Mirando)
            return;

        cursorState = cursorStateBeforeMirando == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        if (cursorState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }
        ResetScannerPromptState();
        LogScannerPanel();
    }

    private void UpdateMirandoPreviewAnimation()
    {
        if (cursorState == CursorState.Mirando)
            TryRefreshMirandoPreviewPathIfNeeded();

        bool shouldShow =
            cursorState == CursorState.Mirando &&
            mirandoPreviewPathLength > 0.0001f &&
            mirandoPreviewPathPoints.Count >= 2;

        if (!shouldShow)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        EnsureMirandoPreviewRenderer();
        if (mirandoPreviewRenderer == null)
            return;

        float speed = GetMirandoPreviewSpeed();
        float segmentLen = GetMirandoPreviewSegmentLength();
        float cycleLen = mirandoPreviewPathLength + segmentLen;
        mirandoPreviewHeadDistance += speed * Time.deltaTime;
        if (mirandoPreviewHeadDistance > cycleLen)
            mirandoPreviewHeadDistance = 0f;

        float startDist = Mathf.Max(0f, mirandoPreviewHeadDistance - segmentLen);
        float endDist = Mathf.Min(mirandoPreviewHeadDistance, mirandoPreviewPathLength);
        if (endDist <= startDist + 0.0001f)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        BuildPathSegmentPoints(startDist, endDist, mirandoPreviewSegmentPoints);
        if (mirandoPreviewSegmentPoints.Count < 2)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        SetMirandoPreviewVisible(true);
        float previewWidth = GetMirandoPreviewWidth();
        Color previewColor = GetMirandoPreviewColor();
        mirandoPreviewRenderer.startWidth = previewWidth;
        mirandoPreviewRenderer.endWidth = previewWidth;
        mirandoPreviewRenderer.startColor = previewColor;
        mirandoPreviewRenderer.endColor = previewColor;
        mirandoPreviewRenderer.positionCount = mirandoPreviewSegmentPoints.Count;
        for (int i = 0; i < mirandoPreviewSegmentPoints.Count; i++)
            mirandoPreviewRenderer.SetPosition(i, mirandoPreviewSegmentPoints[i]);
    }

    private void RebuildMirandoPreviewPath(PodeMirarTargetOption option)
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;

        if (option == null || option.attackerUnit == null || option.targetUnit == null)
        {
            SetMirandoPreviewVisible(false);
            return;
        }

        Vector3 attackerPos = option.attackerUnit.transform.position;
        Vector3 targetPos = option.targetUnit.transform.position;
        attackerPos.z = targetPos.z;

        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        CacheMirandoPreviewSignature(attackerPos, targetPos, trajectory);
        if (trajectory == WeaponTrajectoryType.Parabolic)
            BuildParabolicPath(attackerPos, targetPos, mirandoPreviewPathPoints);
        else
        {
            mirandoPreviewPathPoints.Add(attackerPos);
            mirandoPreviewPathPoints.Add(targetPos);
        }

        mirandoPreviewPathLength = ComputePathLength(mirandoPreviewPathPoints);
        if (mirandoPreviewPathLength <= 0.0001f)
            SetMirandoPreviewVisible(false);
    }

    private void TryRefreshMirandoPreviewPathIfNeeded()
    {
        PodeMirarTargetOption option = GetCurrentMirandoOption();
        if (option == null || option.attackerUnit == null || option.targetUnit == null)
            return;

        Vector3 from = option.attackerUnit.transform.position;
        Vector3 to = option.targetUnit.transform.position;
        from.z = to.z;
        WeaponTrajectoryType trajectory = ResolveSelectedTrajectory(option);
        float bend = GetMirandoParabolaBend();
        int samples = GetMirandoParabolaSamples();

        bool changed =
            !mirandoPreviewSignatureValid ||
            from != mirandoPreviewLastFrom ||
            to != mirandoPreviewLastTo ||
            trajectory != mirandoPreviewLastTrajectory ||
            !Mathf.Approximately(bend, mirandoPreviewLastBend) ||
            samples != mirandoPreviewLastSamples;

        if (!changed)
            return;

        RebuildMirandoPreviewPath(option);
    }

    private PodeMirarTargetOption GetCurrentMirandoOption()
    {
        if (scannerSelectedTargetIndex < 0 || scannerSelectedTargetIndex >= cachedPodeMirarTargets.Count)
            return null;

        return cachedPodeMirarTargets[scannerSelectedTargetIndex];
    }

    private void CacheMirandoPreviewSignature(Vector3 from, Vector3 to, WeaponTrajectoryType trajectory)
    {
        mirandoPreviewSignatureValid = true;
        mirandoPreviewLastFrom = from;
        mirandoPreviewLastTo = to;
        mirandoPreviewLastTrajectory = trajectory;
        mirandoPreviewLastBend = GetMirandoParabolaBend();
        mirandoPreviewLastSamples = GetMirandoParabolaSamples();
    }

    private WeaponTrajectoryType ResolveSelectedTrajectory(PodeMirarTargetOption option)
    {
        if (option == null || option.attackerUnit == null)
            return WeaponTrajectoryType.Straight;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = option.attackerUnit.GetEmbarkedWeapons();
        if (weapons != null && option.embarkedWeaponIndex >= 0 && option.embarkedWeaponIndex < weapons.Count)
        {
            UnitEmbarkedWeapon embarked = weapons[option.embarkedWeaponIndex];
            if (embarked != null)
                return embarked.selectedTrajectory;
        }

        if (option.weapon != null && option.weapon.SupportsTrajectory(WeaponTrajectoryType.Parabolic))
            return WeaponTrajectoryType.Parabolic;

        return WeaponTrajectoryType.Straight;
    }

    private void BuildParabolicPath(Vector3 from, Vector3 to, List<Vector3> output)
    {
        output.Clear();
        Vector2 flat = new Vector2(to.x - from.x, to.y - from.y);
        if (flat.sqrMagnitude <= 0.0001f)
        {
            output.Add(from);
            output.Add(to);
            return;
        }

        Vector2 dir = flat.normalized;
        Vector2 clockwiseNormal = new Vector2(dir.y, -dir.x);
        Vector2 antiClockwiseNormal = new Vector2(-dir.y, dir.x);
        bool targetIsRight = to.x >= from.x;
        Vector2 normal = targetIsRight ? antiClockwiseNormal : clockwiseNormal;

        float distance = flat.magnitude;
        float bend = Mathf.Clamp(GetMirandoParabolaBend(), 0.2f, Mathf.Max(0.2f, distance));
        Vector3 control = (from + to) * 0.5f + new Vector3(normal.x, normal.y, 0f) * bend;

        int samples = GetMirandoParabolaSamples();
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)(samples - 1);
            output.Add(QuadraticBezier(from, control, to, t));
        }
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return (u * u * a) + (2f * u * t * b) + (t * t * c);
    }

    private static float ComputePathLength(List<Vector3> points)
    {
        if (points == null || points.Count < 2)
            return 0f;

        float length = 0f;
        for (int i = 1; i < points.Count; i++)
            length += Vector3.Distance(points[i - 1], points[i]);
        return length;
    }

    private void BuildPathSegmentPoints(float startDist, float endDist, List<Vector3> output)
    {
        output.Clear();
        if (mirandoPreviewPathPoints.Count < 2)
            return;

        float accumulated = 0f;
        bool addedFirst = false;
        for (int i = 1; i < mirandoPreviewPathPoints.Count; i++)
        {
            Vector3 a = mirandoPreviewPathPoints[i - 1];
            Vector3 b = mirandoPreviewPathPoints[i];
            float segmentLen = Vector3.Distance(a, b);
            if (segmentLen <= 0.0001f)
                continue;

            float segStart = accumulated;
            float segEnd = accumulated + segmentLen;
            if (segEnd < startDist)
            {
                accumulated = segEnd;
                continue;
            }

            if (segStart > endDist)
                break;

            float localStart = Mathf.Clamp01((startDist - segStart) / segmentLen);
            float localEnd = Mathf.Clamp01((endDist - segStart) / segmentLen);
            if (!addedFirst)
            {
                output.Add(Vector3.Lerp(a, b, localStart));
                addedFirst = true;
            }

            output.Add(Vector3.Lerp(a, b, localEnd));
            accumulated = segEnd;
            if (segEnd >= endDist)
                break;
        }
    }

    private void EnsureMirandoPreviewRenderer()
    {
        if (mirandoPreviewRenderer != null)
            return;

        GameObject go = new GameObject("MirandoPreviewLine");
        go.transform.SetParent(transform, false);
        mirandoPreviewRenderer = go.AddComponent<LineRenderer>();
        mirandoPreviewRenderer.useWorldSpace = true;
        mirandoPreviewRenderer.textureMode = LineTextureMode.Stretch;
        mirandoPreviewRenderer.numCapVertices = 2;
        mirandoPreviewRenderer.numCornerVertices = 2;
        mirandoPreviewRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mirandoPreviewRenderer.receiveShadows = false;
        Material previewMaterial = GetMirandoPreviewMaterial();
        mirandoPreviewRenderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
        int sortingLayerId = GetMirandoPreviewSortingLayerId();
        if (sortingLayerId != 0)
            mirandoPreviewRenderer.sortingLayerID = sortingLayerId;
        mirandoPreviewRenderer.sortingOrder = GetMirandoPreviewSortingOrder();
        mirandoPreviewRenderer.enabled = false;
    }

    private void SetMirandoPreviewVisible(bool visible)
    {
        if (mirandoPreviewRenderer == null)
            return;

        if (!visible)
        {
            mirandoPreviewRenderer.positionCount = 0;
            mirandoPreviewRenderer.enabled = false;
            return;
        }

        mirandoPreviewRenderer.enabled = true;
    }

    private void ClearMirandoPreview()
    {
        mirandoPreviewPathPoints.Clear();
        mirandoPreviewSegmentPoints.Clear();
        mirandoPreviewPathLength = 0f;
        mirandoPreviewHeadDistance = 0f;
        mirandoPreviewSignatureValid = false;
        SetMirandoPreviewVisible(false);
    }

    private Material GetMirandoPreviewMaterial()
    {
        return animationManager != null ? animationManager.MirandoPreviewMaterial : null;
    }

    private Color GetMirandoPreviewColor()
    {
        Color fallback = animationManager != null ? animationManager.MirandoPreviewColor : new Color(1f, 0.65f, 0.2f, 0.95f);
        TeamId? team = ResolveMirandoAttackerTeam();
        if (!team.HasValue)
            return fallback;

        Color teamColor = TeamUtils.GetColor(team.Value);
        teamColor.a = fallback.a;
        return teamColor;
    }

    private float GetMirandoPreviewWidth()
    {
        return animationManager != null ? animationManager.MirandoPreviewWidth : 0.12f;
    }

    private float GetMirandoPreviewSpeed()
    {
        return animationManager != null ? animationManager.MirandoPreviewSpeed : 3f;
    }

    private float GetMirandoPreviewSegmentLength()
    {
        return animationManager != null ? animationManager.MirandoPreviewSegmentLength : 1.1f;
    }

    private float GetMirandoParabolaBend()
    {
        return animationManager != null ? animationManager.MirandoParabolaBend : 1.2f;
    }

    private int GetMirandoParabolaSamples()
    {
        return animationManager != null ? animationManager.MirandoParabolaSamples : 24;
    }

    private int GetMirandoPreviewSortingOrder()
    {
        return animationManager != null ? animationManager.MirandoPreviewSortingOrder : 120;
    }

    private int GetMirandoPreviewSortingLayerId()
    {
        return animationManager != null ? animationManager.MirandoPreviewSortingLayerId : 0;
    }

    private TeamId? ResolveMirandoAttackerTeam()
    {
        if (scannerSelectedTargetIndex >= 0 && scannerSelectedTargetIndex < cachedPodeMirarTargets.Count)
        {
            PodeMirarTargetOption option = cachedPodeMirarTargets[scannerSelectedTargetIndex];
            if (option != null && option.attackerUnit != null)
                return option.attackerUnit.TeamId;
        }

        if (selectedUnit != null)
            return selectedUnit.TeamId;

        return null;
    }

    private static bool TryReadPressedNumber(out int number)
    {
        number = 0;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { number = 1; return true; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { number = 2; return true; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { number = 3; return true; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { number = 4; return true; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { number = 5; return true; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { number = 6; return true; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { number = 7; return true; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { number = 8; return true; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { number = 9; return true; }
        }
#else
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { number = 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { number = 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { number = 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { number = 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { number = 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { number = 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { number = 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { number = 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { number = 9; return true; }
#endif

        return false;
    }

    private static bool WasLetterPressedThisFrame(char letter)
    {
        switch (char.ToUpperInvariant(letter))
        {
            case 'A':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.aKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.A);
#endif
            case 'E':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.E);
#endif
            case 'M':
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.M);
#endif
            default:
                return false;
        }
    }
}
