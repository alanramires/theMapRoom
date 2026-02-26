using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class TurnStateManager
{
    private enum MergeLayerPlan
    {
        None = 0,
        DefaultSameDomain = 1,
        AirLow = 2,
        NavalSurface = 3,
        SubSubmerged = 4
    }

    private sealed class MergeCandidateEntry
    {
        public UnitManager unit;
        public int selectionNumber;
        public Vector3Int cell;
        public string label;
    }

    private sealed class MergeQueuePreviewTrack
    {
        public UnitManager donor;
        public readonly List<LineRenderer> renderers = new List<LineRenderer>();
        public readonly List<Vector3> pathPoints = new List<Vector3>(2);
        public readonly List<Vector3> tempSegmentPoints = new List<Vector3>(8);
        public float pathLength;
        public float headDistance;
    }

    private readonly List<MergeCandidateEntry> mergeCandidateEntries = new List<MergeCandidateEntry>();
    private readonly Dictionary<Vector3Int, int> mergeCandidateIndexByCell = new Dictionary<Vector3Int, int>();
    private readonly List<UnitManager> mergeQueuedUnits = new List<UnitManager>();
    private readonly List<MergeCandidateEntry> mergeTargetOptions = new List<MergeCandidateEntry>();
    private readonly Dictionary<Vector3Int, MergeCandidateEntry> mergeTargetByCell = new Dictionary<Vector3Int, MergeCandidateEntry>();
    private readonly List<MergeQueuePreviewTrack> mergeQueuePreviewTracks = new List<MergeQueuePreviewTrack>();
    private readonly MergeQueuePreviewTrack mergeConfirmPreviewTrack = new MergeQueuePreviewTrack();
    private int mergeSelectedCandidateIndex = -1;
    private Vector3Int mergeSelectedTargetCell = Vector3Int.zero;
    private bool mergeSelectedTargetCellValid;
    private bool mergeTargetAutoEntered;
    private bool mergeSuppressDefaultConfirmSfxOnce;
    private CursorState cursorStateBeforeFundindo = CursorState.MoveuParado;

    private void EnterMergeStateFromSensors()
    {
        if (selectedUnit == null)
            return;
        if (cursorState != CursorState.MoveuAndando && cursorState != CursorState.MoveuParado)
            return;

        cursorController?.PlayConfirmSfx();
        cursorStateBeforeFundindo = cursorState == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(CursorState.Fundindo, "EnterMergeStateFromSensors");
        ClearCommittedPathVisual();
        mergeQueuedUnits.Clear();
        RebuildMergeQueuePreviewTracks();
        EnterMergeParticipantSelectStep();
    }

    private void ProcessMergePromptInput()
    {
        if (cursorState != CursorState.Fundindo)
            return;

        if (scannerPromptStep == ScannerPromptStep.MergeParticipantSelect)
        {
            if (!TryReadPressedDigitIncludingZero(out int number))
                return;

            if (number == 0)
            {
                if (mergeQueuedUnits.Count > 0)
                {
                    StartMergeExecution();
                    return;
                }

                Debug.Log("[Fusao] Nenhuma ordem em fila para executar.");
                return;
            }

            int index = number - 1;
            MergeCandidateEntry picked = null;
            for (int i = 0; i < mergeCandidateEntries.Count; i++)
            {
                MergeCandidateEntry entry = mergeCandidateEntries[i];
                if (entry != null && entry.selectionNumber == number)
                {
                    picked = entry;
                    index = i;
                    break;
                }
            }

            if (picked == null || index < 0 || index >= mergeCandidateEntries.Count)
            {
                Debug.Log($"[Fusao] Participante invalido: {number}. Escolha uma das opcoes listadas.");
                return;
            }

            mergeSelectedCandidateIndex = index;
            cursorController?.PlayConfirmSfx();
            Vector3Int pickedCell = picked.cell;
            pickedCell.z = 0;
            cursorController?.SetCell(pickedCell, playMoveSfx: false);
            Debug.Log($"[Fusao] Candidato selecionado: {picked.selectionNumber}. {picked.label} | Enter para continuar.");
        }
    }

    private bool TryConfirmScannerMerge()
    {
        if (cursorState != CursorState.Fundindo)
            return false;

        if (scannerPromptStep == ScannerPromptStep.MergeParticipantSelect)
        {
            if (mergeSelectedCandidateIndex < 0)
                SyncMergeSelectedCandidateFromCursor();
            if (!TryGetSelectedMergeCandidate(out MergeCandidateEntry selected))
            {
                Debug.Log("[Fusao] Selecione um candidato valido por numero ou cursor antes de confirmar.");
                return true;
            }

            scannerPromptStep = ScannerPromptStep.MergeConfirm;
            mergeTargetAutoEntered = false;
            cursorController?.PlayConfirmSfx();
            LogMergeConfirmPrompt(selected);
            return true;
        }

        if (scannerPromptStep != ScannerPromptStep.MergeConfirm)
            return true;

        if (!TryGetSelectedMergeCandidate(out MergeCandidateEntry target) || target.unit == null)
        {
            ReturnToMergeParticipantSelect();
            return true;
        }

        if (IsMergeUnitAlreadyQueued(target.unit))
        {
            Debug.Log($"[Fusao] {ResolveUnitRuntimeName(target.unit)} ja esta na fila. Escolha outra unidade.");
            ReturnToMergeParticipantSelect();
            return true;
        }

        mergeQueuedUnits.Add(target.unit);
        mergeSuppressDefaultConfirmSfxOnce = true;
        cursorController?.PlayLoadSfx();
        RebuildMergeQueuePreviewTracks();

        int remaining = CountRemainingMergeCandidates();
        if (remaining <= 0)
        {
            StartMergeExecution();
            return true;
        }

        Debug.Log($"[Fusao] Ordem adicionada: {ResolveUnitRuntimeName(target.unit)} -> {ResolveUnitRuntimeName(selectedUnit)}.");
        EnterMergeParticipantSelectStep();
        return true;
    }

    private void EnterMergeParticipantSelectStep()
    {
        ClearMergeTargetOptionsAndPaint();
        RebuildMergeCandidateEntries();
        PaintMergeCandidateOptions();
        scannerPromptStep = ScannerPromptStep.MergeParticipantSelect;
        mergeSelectedCandidateIndex = mergeCandidateEntries.Count > 0 ? 0 : -1;
        mergeTargetAutoEntered = false;

        if (cursorController != null && mergeSelectedCandidateIndex >= 0 && mergeSelectedCandidateIndex < mergeCandidateEntries.Count)
        {
            Vector3Int candidateCell = mergeCandidateEntries[mergeSelectedCandidateIndex].cell;
            candidateCell.z = 0;
            cursorController.SetCell(candidateCell, playMoveSfx: false);
        }

        if (mergeCandidateEntries.Count <= 0)
        {
            if (mergeQueuedUnits.Count > 0)
                StartMergeExecution();
            else
                ExitMergeStateToMovement();
            return;
        }

        if (mergeCandidateEntries.Count == 1 && mergeQueuedUnits.Count <= 0)
        {
            mergeSelectedCandidateIndex = 0;
            mergeTargetAutoEntered = true;
            scannerPromptStep = ScannerPromptStep.MergeConfirm;
            if (cursorController != null)
            {
                Vector3Int onlyCell = mergeCandidateEntries[0].cell;
                onlyCell.z = 0;
                cursorController.SetCell(onlyCell, playMoveSfx: false);
            }
            LogMergeConfirmPrompt(mergeCandidateEntries[0]);
            return;
        }

        LogMergeParticipantSelectionPanel();
    }

    private void ReturnToMergeParticipantSelect()
    {
        scannerPromptStep = ScannerPromptStep.MergeParticipantSelect;
        ClearMergeTargetOptionsAndPaint();
        RebuildMergeCandidateEntries();
        PaintMergeCandidateOptions();
        if (mergeSelectedCandidateIndex < 0 || mergeSelectedCandidateIndex >= mergeCandidateEntries.Count)
            mergeSelectedCandidateIndex = mergeCandidateEntries.Count > 0 ? 0 : -1;
        LogMergeParticipantSelectionPanel();
    }

    private bool TryUndoLastQueuedMergeOrderAndReturnToTarget()
    {
        if (mergeQueuedUnits.Count <= 0)
            return false;

        int lastIndex = mergeQueuedUnits.Count - 1;
        UnitManager last = mergeQueuedUnits[lastIndex];
        mergeQueuedUnits.RemoveAt(lastIndex);
        RebuildMergeQueuePreviewTracks();

        if (last == null)
        {
            EnterMergeParticipantSelectStep();
            return true;
        }

        RebuildMergeCandidateEntries();
        mergeSelectedCandidateIndex = -1;
        for (int i = 0; i < mergeCandidateEntries.Count; i++)
        {
            MergeCandidateEntry entry = mergeCandidateEntries[i];
            if (entry == null || entry.unit == null)
                continue;
            if (entry.unit != last)
                continue;
            mergeSelectedCandidateIndex = i;
            break;
        }

        ReturnToMergeParticipantSelect();
        Debug.Log($"[Fusao] Ordem desfeita para {ResolveUnitRuntimeName(last)}. Retornando para selecao.");
        return true;
    }

    private void ExitMergeStateToMovement()
    {
        if (cursorState != CursorState.Fundindo)
            return;

        CursorState targetMovementState = cursorStateBeforeFundindo == CursorState.MoveuAndando ? CursorState.MoveuAndando : CursorState.MoveuParado;
        SetCursorState(targetMovementState, "ExitMergeStateToMovement", rollback: true);
        if (targetMovementState == CursorState.MoveuAndando && hasCommittedMovement && committedMovementPath.Count >= 2)
            DrawCommittedPathVisual(committedMovementPath);
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int unitCell = selectedUnit.CurrentCellPosition;
            unitCell.z = 0;
            cursorController.SetCell(unitCell, playMoveSfx: false);
        }

        ResetMergeRuntimeState();
        LogScannerPanel();
    }

    private void StartMergeExecution()
    {
        if (mergeExecutionInProgress)
            return;
        if (selectedUnit == null || mergeQueuedUnits.Count <= 0)
        {
            ExitMergeStateToMovement();
            return;
        }

        ClearMergeTargetOptionsAndPaint();
        if (cursorController != null && selectedUnit != null)
        {
            Vector3Int receiverCell = selectedUnit.CurrentCellPosition;
            receiverCell.z = 0;
            cursorController.SetCell(receiverCell, playMoveSfx: false);
        }

        StartCoroutine(ExecuteQueuedMergeOrdersSequence());
    }

    private IEnumerator ExecuteQueuedMergeOrdersSequence()
    {
        mergeExecutionInProgress = true;
        UnitManager receiver = selectedUnit;
        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : (receiver != null ? receiver.BoardTilemap : null);
        if (receiver == null || boardMap == null)
        {
            mergeExecutionInProgress = false;
            ExitMergeStateToMovement();
            yield break;
        }

        List<UnitManager> participants = new List<UnitManager>(mergeQueuedUnits.Count);
        for (int i = 0; i < mergeQueuedUnits.Count; i++)
        {
            UnitManager unit = mergeQueuedUnits[i];
            if (unit == null || unit == receiver || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;
            participants.Add(unit);
        }

        if (participants.Count <= 0)
        {
            mergeExecutionInProgress = false;
            ExitMergeStateToMovement();
            yield break;
        }

        List<UnitManager> mergeMembers = BuildMergeMembersForLayerPlan(receiver, participants);
        MergeLayerPlan layerPlan = ResolveMergeLayerPlanForExecution(mergeMembers, boardMap);
        bool hasFusionLayer = TryGetFusionLayerFromPlan(layerPlan, out Domain fusionDomain, out HeightLevel fusionHeight);

        if (layerPlan == MergeLayerPlan.None || layerPlan == MergeLayerPlan.DefaultSameDomain)
        {
            Debug.Log("[Fusao] Plano de camada: Default Fusion (Same domain).");
        }
        else
        {
            Debug.Log($"[Fusao] Plano de camada: {layerPlan} -> {fusionDomain}/{fusionHeight}.");
            if (!IsUnitOnLayer(receiver, fusionDomain, fusionHeight))
            {
                receiver.TrySetCurrentLayerMode(fusionDomain, fusionHeight);
                PlayMovementStartSfx(receiver);
                if (cursorController != null)
                {
                    Vector3Int receiverCell = receiver.CurrentCellPosition;
                    receiverCell.z = 0;
                    cursorController.SetCell(receiverCell, playMoveSfx: false);
                }
            }
        }

        int baseHp = Mathf.Max(0, receiver.CurrentHP);
        int baseAutonomy = Mathf.Max(0, receiver.CurrentFuel);
        int baseSteps = baseHp * baseAutonomy;

        int participantsHp = 0;
        int participantsSteps = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            UnitManager participant = participants[i];
            int hp = Mathf.Max(0, participant.CurrentHP);
            int autonomy = Mathf.Max(0, participant.CurrentFuel);
            participantsHp += hp;
            participantsSteps += hp * autonomy;
        }

        int resultHp = Mathf.Min(10, baseHp + participantsHp);
        int totalSteps = baseSteps + participantsSteps;
        int resultAutonomy = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;

        Dictionary<WeaponData, int> projectilesByWeapon = BuildMergeWeaponProjectileTotals(receiver, participants);
        int missingWeaponSlots = ApplyMergedWeaponAmmunitionToBaseUnit(receiver, projectilesByWeapon, resultHp);
        Dictionary<SupplyData, int> supplyStepsByType = BuildMergeSupplyStepTotals(receiver, participants);
        int missingSupplySlots = ApplyMergedSupplyAmountsToBaseUnit(receiver, supplyStepsByType, resultHp);

        float mergeStepDuration = GetMergeMoveStepDuration();
        float cursorHopDelay = GetMergeCursorHopDelay();
        float afterParticipantMoveDelay = GetMergeAfterParticipantMoveDelay();
        float afterParticipantLoadDelay = GetMergeAfterParticipantLoadDelay();

        for (int i = 0; i < participants.Count; i++)
        {
            UnitManager participant = participants[i];
            if (participant == null || !participant.gameObject.activeInHierarchy)
                continue;

            bool participantNeedsLayerTransition = hasFusionLayer && !IsUnitOnLayer(participant, fusionDomain, fusionHeight);

            participant.SetTemporarySortingOrder(1000 + i);
            Vector3Int fromCell = participant.CurrentCellPosition;
            fromCell.z = 0;
            Vector3Int targetCell = receiver.CurrentCellPosition;
            targetCell.z = 0;
            List<Vector3Int> path = new List<Vector3Int>(2) { fromCell, targetCell };

            if (cursorController != null)
            {
                Vector3Int cell = fromCell;
                cell.z = 0;
                cursorController.SetCell(cell, playMoveSfx: i > 0);
                if (i > 0 && cursorHopDelay > 0f)
                    yield return new WaitForSeconds(cursorHopDelay);
            }

            bool finished = false;
            if (animationManager != null)
            {
                animationManager.PlayMovement(
                    participant,
                    boardMap,
                    path,
                    playStartSfx: true,
                    onAnimationStart: () =>
                    {
                        if (participantNeedsLayerTransition)
                            participant.TrySetCurrentLayerMode(fusionDomain, fusionHeight);
                        PlayMovementStartSfx(participant);
                    },
                    onAnimationFinished: () => finished = true,
                    onCellReached: reachedCell =>
                    {
                        if (cursorController == null)
                            return;

                        Vector3Int c = reachedCell;
                        c.z = 0;
                        cursorController.SetCell(c, playMoveSfx: false);
                    },
                    stepDurationOverride: mergeStepDuration);
                while (!finished)
                    yield return null;
            }
            else
            {
                if (participantNeedsLayerTransition)
                    participant.TrySetCurrentLayerMode(fusionDomain, fusionHeight);
                PlayMovementStartSfx(participant);
                participant.SetCurrentCellPosition(targetCell, enforceFinalOccupancyRule: false);
                if (cursorController != null)
                    cursorController.SetCell(targetCell, playMoveSfx: false);
            }

            if (afterParticipantMoveDelay > 0f)
                yield return new WaitForSeconds(afterParticipantMoveDelay);

            participant.ClearTemporarySortingOrder();
            cursorController?.PlayLoadSfx();
            KillEntireEmbarkedChain(participant, detachSelf: true);

            if (afterParticipantLoadDelay > 0f)
                yield return new WaitForSeconds(afterParticipantLoadDelay);
        }

        receiver.SetCurrentHP(resultHp);
        receiver.SetCurrentFuel(resultAutonomy);

        if (missingWeaponSlots > 0)
            Debug.Log($"[Fusao] Aviso: {missingWeaponSlots} tipo(s) de arma sem slot na unidade resultante.");
        if (missingSupplySlots > 0)
            Debug.Log($"[Fusao] Aviso: {missingSupplySlots} tipo(s) de suprimento sem slot na unidade resultante.");

        receiver.MarkAsActed();
        cursorController?.PlayDoneSfx();

        bool finalized = TryFinalizeSelectedUnitActionFromDebug();
        if (!finalized)
            ClearSelectionAndReturnToNeutral(keepPreparedFuelCost: true);

        ResetMergeRuntimeState();
        mergeExecutionInProgress = false;
    }

    private MergeLayerPlan ResolveMergeLayerPlanForExecution(List<UnitManager> members, Tilemap boardMap)
    {
        if (members == null || members.Count <= 1)
            return MergeLayerPlan.None;
        if (AreAllMembersOnSameLayer(members))
            return MergeLayerPlan.DefaultSameDomain;

        bool hasAr = HasAnyMergeSkillPrefix(members, "AR ") || HasAnyMergeAirFamily(members);
        if (hasAr)
            return MergeLayerPlan.AirLow;

        bool hasSub = HasAnyMergeSkillPrefix(members, "SUB ") || HasAnyMergeSubFamily(members);
        if (!hasSub)
            return MergeLayerPlan.DefaultSameDomain;

        bool allCanSubmerge = boardMap != null && terrainDatabase != null;
        if (allCanSubmerge)
        {
            for (int i = 0; i < members.Count; i++)
            {
                UnitManager unit = members[i];
                if (unit == null)
                    continue;

                Vector3Int cell = unit.CurrentCellPosition;
                cell.z = 0;
                if (!CanUseLayerModeAtCurrentCell(
                        unit,
                        boardMap,
                        terrainDatabase,
                        cell,
                        Domain.Submarine,
                        HeightLevel.Submerged,
                        out _))
                {
                    allCanSubmerge = false;
                    break;
                }
            }
        }

        return allCanSubmerge ? MergeLayerPlan.SubSubmerged : MergeLayerPlan.NavalSurface;
    }

    private static bool TryGetFusionLayerFromPlan(MergeLayerPlan plan, out Domain domain, out HeightLevel height)
    {
        domain = Domain.Land;
        height = HeightLevel.Surface;
        switch (plan)
        {
            case MergeLayerPlan.AirLow:
                domain = Domain.Air;
                height = HeightLevel.AirLow;
                return true;
            case MergeLayerPlan.NavalSurface:
                domain = Domain.Naval;
                height = HeightLevel.Surface;
                return true;
            case MergeLayerPlan.SubSubmerged:
                domain = Domain.Submarine;
                height = HeightLevel.Submerged;
                return true;
            default:
                return false;
        }
    }

    private static List<UnitManager> BuildMergeMembersForLayerPlan(UnitManager receiver, List<UnitManager> participants)
    {
        List<UnitManager> members = new List<UnitManager>();
        if (receiver != null)
            members.Add(receiver);

        if (participants == null)
            return members;

        for (int i = 0; i < participants.Count; i++)
        {
            UnitManager participant = participants[i];
            if (participant == null || participant == receiver)
                continue;
            if (!members.Contains(participant))
                members.Add(participant);
        }

        return members;
    }

    private static bool AreAllMembersOnSameLayer(List<UnitManager> members)
    {
        if (members == null || members.Count <= 1)
            return true;

        UnitManager first = members[0];
        if (first == null)
            return true;

        Domain domain = first.GetDomain();
        HeightLevel height = first.GetHeightLevel();
        for (int i = 1; i < members.Count; i++)
        {
            UnitManager unit = members[i];
            if (unit == null)
                continue;
            if (unit.GetDomain() != domain || unit.GetHeightLevel() != height)
                return false;
        }

        return true;
    }

    private static bool HasAnyMergeSkillPrefix(List<UnitManager> members, string prefix)
    {
        if (members == null || string.IsNullOrWhiteSpace(prefix))
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            UnitManager unit = members[i];
            if (unit == null)
                continue;

            if (HasMergeSkillPrefix(unit, prefix))
                return true;
        }

        return false;
    }

    private static bool HasMergeSkillPrefix(UnitManager unit, string prefix)
    {
        if (unit == null || string.IsNullOrWhiteSpace(prefix))
            return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null || data.skills == null)
            return false;

        string normalized = prefix.Trim();
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill == null)
                continue;

            string id = skill.id;
            string display = skill.displayName;
            if ((!string.IsNullOrWhiteSpace(id) && id.TrimStart().StartsWith(normalized, System.StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(display) && display.TrimStart().StartsWith(normalized, System.StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static bool HasAnyMergeAirFamily(List<UnitManager> members)
    {
        if (members == null)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            UnitManager unit = members[i];
            if (unit == null)
                continue;

            if (unit.GetDomain() == Domain.Air)
                return true;
            if (unit.SupportsLayerMode(Domain.Air, HeightLevel.AirLow) || unit.SupportsLayerMode(Domain.Air, HeightLevel.AirHigh))
                return true;
        }

        return false;
    }

    private static bool HasAnyMergeSubFamily(List<UnitManager> members)
    {
        if (members == null)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            UnitManager unit = members[i];
            if (unit == null)
                continue;

            if (unit.GetDomain() == Domain.Submarine)
                return true;
            if (unit.SupportsLayerMode(Domain.Submarine, HeightLevel.Submerged))
                return true;
        }

        return false;
    }

    private static bool IsUnitOnLayer(UnitManager unit, Domain domain, HeightLevel height)
    {
        return unit != null && unit.GetDomain() == domain && unit.GetHeightLevel() == height;
    }

    private void ResetMergeRuntimeState()
    {
        mergeCandidateEntries.Clear();
        mergeCandidateIndexByCell.Clear();
        mergeQueuedUnits.Clear();
        mergeTargetOptions.Clear();
        mergeTargetByCell.Clear();
        mergeSelectedCandidateIndex = -1;
        mergeSelectedTargetCell = Vector3Int.zero;
        mergeSelectedTargetCellValid = false;
        mergeTargetAutoEntered = false;
        mergeSuppressDefaultConfirmSfxOnce = false;
        ClearMergeTargetOptionsAndPaint();
        ClearMergeQueuePreviewData();
        mergeConfirmPreviewTrack.donor = null;
        mergeConfirmPreviewTrack.pathPoints.Clear();
        mergeConfirmPreviewTrack.tempSegmentPoints.Clear();
        mergeConfirmPreviewTrack.pathLength = 0f;
        mergeConfirmPreviewTrack.headDistance = 0f;
        if (mergeConfirmPreviewTrack.renderers != null)
        {
            for (int r = 0; r < mergeConfirmPreviewTrack.renderers.Count; r++)
            {
                LineRenderer renderer = mergeConfirmPreviewTrack.renderers[r];
                if (renderer == null)
                    continue;
                renderer.positionCount = 0;
                renderer.enabled = false;
            }
        }
    }

    private void RebuildMergeCandidateEntries()
    {
        mergeCandidateEntries.Clear();
        mergeCandidateIndexByCell.Clear();
        if (selectedUnit == null)
            return;

        Tilemap map = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        if (map == null)
            return;

        List<UnitManager> neighbors = new List<UnitManager>(6);
        CollectMergeEligibleNeighbors(selectedUnit, map, neighbors);
        int number = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            UnitManager unit = neighbors[i];
            if (unit == null || IsMergeUnitAlreadyQueued(unit))
                continue;

            number++;
            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            mergeCandidateEntries.Add(new MergeCandidateEntry
            {
                unit = unit,
                selectionNumber = number,
                cell = cell,
                label = $"{ResolveUnitRuntimeName(unit)} ({cell.x},{cell.y})"
            });
            mergeCandidateIndexByCell[cell] = mergeCandidateEntries.Count - 1;
        }
    }

    private void PaintMergeCandidateOptions()
    {
        ClearMovementRange(keepCommittedMovement: true);
        if (rangeMapTilemap == null || rangeOverlayTile == null || selectedUnit == null)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        for (int i = 0; i < mergeCandidateEntries.Count; i++)
        {
            MergeCandidateEntry candidate = mergeCandidateEntries[i];
            if (candidate == null)
                continue;

            Vector3Int cell = candidate.cell;
            cell.z = 0;
            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
        }
    }

    private void RebuildMergeTargetOptions(MergeCandidateEntry selectedCandidate)
    {
        mergeTargetOptions.Clear();
        mergeTargetByCell.Clear();
        mergeSelectedTargetCellValid = false;

        if (selectedCandidate == null || selectedCandidate.unit == null)
            return;
        if (IsMergeUnitAlreadyQueued(selectedCandidate.unit))
            return;

        Vector3Int cell = selectedCandidate.unit.CurrentCellPosition;
        cell.z = 0;
        if (mergeTargetByCell.ContainsKey(cell))
            return;

        mergeTargetOptions.Add(selectedCandidate);
        mergeTargetByCell.Add(cell, selectedCandidate);
    }

    private void PaintMergeTargetOptions()
    {
        ClearMovementRange(keepCommittedMovement: true);
        if (rangeMapTilemap == null || rangeOverlayTile == null || selectedUnit == null)
            return;

        Color teamColor = TeamUtils.GetColor(selectedUnit.TeamId);
        Color overlayColor = new Color(teamColor.r, teamColor.g, teamColor.b, Mathf.Clamp01(movementRangeAlpha));
        for (int i = 0; i < mergeTargetOptions.Count; i++)
        {
            MergeCandidateEntry option = mergeTargetOptions[i];
            if (option == null)
                continue;

            Vector3Int cell = option.cell;
            cell.z = 0;
            rangeMapTilemap.SetTile(cell, rangeOverlayTile);
            rangeMapTilemap.SetTileFlags(cell, TileFlags.None);
            rangeMapTilemap.SetColor(cell, overlayColor);
            paintedRangeCells.Add(cell);
            paintedRangeLookup.Add(cell);
        }
    }

    private void ClearMergeTargetOptionsAndPaint()
    {
        mergeTargetOptions.Clear();
        mergeTargetByCell.Clear();
        mergeSelectedTargetCellValid = false;
        ClearMovementRange(keepCommittedMovement: true);
    }

    private bool TryResolveMergeCursorMove(Vector3Int currentCell, Vector3Int inputDelta, out Vector3Int resolvedCell)
    {
        resolvedCell = currentCell;
        if (cursorState != CursorState.Fundindo)
            return false;
        if (scannerPromptStep != ScannerPromptStep.MergeParticipantSelect)
            return false;

        if (mergeCandidateEntries.Count <= 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        if (mergeSelectedCandidateIndex < 0 || mergeSelectedCandidateIndex >= mergeCandidateEntries.Count)
            SyncMergeSelectedCandidateFromCursor();

        int currentIndex = mergeSelectedCandidateIndex;
        if (currentIndex < 0 || currentIndex >= mergeCandidateEntries.Count)
            currentIndex = 0;

        int nextIndex = (currentIndex + step + mergeCandidateEntries.Count) % mergeCandidateEntries.Count;
        MergeCandidateEntry next = mergeCandidateEntries[nextIndex];
        if (next == null)
            return false;

        mergeSelectedCandidateIndex = nextIndex;
        Vector3Int nextCell = next.cell;
        nextCell.z = 0;
        resolvedCell = nextCell;
        return true;
    }

    private void SyncMergeSelectedCandidateFromCursor()
    {
        if (cursorController == null)
            return;

        Vector3Int cell = cursorController.CurrentCell;
        cell.z = 0;
        SyncMergeSelectedCandidateFromCell(cell);
    }

    private void SyncMergeSelectedCandidateFromCell(Vector3Int cell)
    {
        cell.z = 0;
        if (!mergeCandidateIndexByCell.TryGetValue(cell, out int index))
            return;
        if (index < 0 || index >= mergeCandidateEntries.Count)
            return;
        mergeSelectedCandidateIndex = index;
    }

    private void SetMergeSelectedTargetCell(Vector3Int cell, bool moveCursor)
    {
        cell.z = 0;
        if (!mergeTargetByCell.ContainsKey(cell))
            return;

        mergeSelectedTargetCell = cell;
        mergeSelectedTargetCellValid = true;
        if (moveCursor && cursorController != null)
            cursorController.SetCell(cell, playMoveSfx: false);
    }

    private bool TryGetSelectedMergeTargetOption(out MergeCandidateEntry option)
    {
        option = null;
        if (!mergeSelectedTargetCellValid)
            return false;
        return mergeTargetByCell.TryGetValue(mergeSelectedTargetCell, out option) && option != null;
    }

    private bool TryGetSelectedMergeCandidate(out MergeCandidateEntry entry)
    {
        entry = null;
        if (mergeSelectedCandidateIndex < 0 || mergeSelectedCandidateIndex >= mergeCandidateEntries.Count)
            return false;
        entry = mergeCandidateEntries[mergeSelectedCandidateIndex];
        return entry != null && entry.unit != null;
    }

    private int CountRemainingMergeCandidates()
    {
        int count = 0;
        if (selectedUnit == null)
            return 0;

        Tilemap map = terrainTilemap != null ? terrainTilemap : selectedUnit.BoardTilemap;
        if (map == null)
            return 0;

        List<UnitManager> neighbors = new List<UnitManager>(6);
        CollectMergeEligibleNeighbors(selectedUnit, map, neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            UnitManager unit = neighbors[i];
            if (unit == null || IsMergeUnitAlreadyQueued(unit))
                continue;
            count++;
        }

        return count;
    }

    private bool IsMergeUnitAlreadyQueued(UnitManager unit)
    {
        if (unit == null)
            return false;

        for (int i = 0; i < mergeQueuedUnits.Count; i++)
        {
            if (mergeQueuedUnits[i] == unit)
                return true;
        }

        return false;
    }

    private void LogMergeParticipantSelectionPanel()
    {
        string text = $"[Fusao] Candidatos elegiveis: {mergeCandidateEntries.Count}\n";
        text += "Use numero (1..9) ou mova o cursor entre os hexes pintados.\n";
        text += "Enter confirma o candidato atual e avanca para o proximo substep.\n";
        if (mergeQueuedUnits.Count > 0)
        {
            text += "Digite 0 para executar as ordens em fila.\n";
            text += "ESC desfaz a ultima ordem e volta para editar.\n";
        }
        else
        {
            text += "ESC volta para sensores.\n";
        }

        for (int i = 0; i < mergeCandidateEntries.Count; i++)
            text += $"{mergeCandidateEntries[i].selectionNumber}. {mergeCandidateEntries[i].label}\n";

        Debug.Log(text);
    }

    private void LogMergeConfirmPrompt(MergeCandidateEntry entry)
    {
        if (entry == null || entry.unit == null)
            return;

        Debug.Log($"[Fusao] Confirmar adicionar {ResolveUnitRuntimeName(entry.unit)} na fila? (Enter=sim, ESC=voltar)");
    }

    private void RebuildMergeQueuePreviewTracks()
    {
        while (mergeQueuePreviewTracks.Count < mergeQueuedUnits.Count)
            mergeQueuePreviewTracks.Add(new MergeQueuePreviewTrack());

        for (int i = 0; i < mergeQueuePreviewTracks.Count; i++)
        {
            MergeQueuePreviewTrack track = mergeQueuePreviewTracks[i];
            if (track == null)
                continue;

            UnitManager newDonor = i < mergeQueuedUnits.Count ? mergeQueuedUnits[i] : null;
            if (track.donor != newDonor)
                track.headDistance = 0f;
            track.donor = newDonor;
            track.pathPoints.Clear();
            track.tempSegmentPoints.Clear();
            track.pathLength = 0f;
            EnsureMergeQueuePreviewRenderers(track, i, Mathf.Max(1, GetMergeQueuePreviewSegmentQuantities()));
        }
    }

    private void EnsureMergeQueuePreviewRenderers(MergeQueuePreviewTrack track, int trackIndex, int count)
    {
        if (track == null)
            return;

        int desired = Mathf.Max(1, count);
        while (track.renderers.Count < desired)
        {
            int segmentIndex = track.renderers.Count;
            string rendererName = $"MergeQueuePreviewLine_{trackIndex + 1}_{segmentIndex + 1}";
            GameObject go = new GameObject(rendererName);
            go.transform.SetParent(transform, false);
            LineRenderer renderer = go.AddComponent<LineRenderer>();
            renderer.useWorldSpace = true;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.numCapVertices = 2;
            renderer.numCornerVertices = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material previewMaterial = GetMirandoPreviewMaterial();
            renderer.material = previewMaterial != null ? previewMaterial : new Material(Shader.Find("Sprites/Default"));
            int sortingLayerId = GetMirandoPreviewSortingLayerId();
            if (sortingLayerId != 0)
                renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = Mathf.Max(0, GetMirandoPreviewSortingOrder() - 1);
            renderer.enabled = false;
            track.renderers.Add(renderer);
        }
    }

    private void UpdateMergeQueuePreviewAnimation()
    {
        bool shouldShow = cursorState == CursorState.Fundindo &&
                          (mergeQueuedUnits.Count > 0 || scannerPromptStep == ScannerPromptStep.MergeConfirm);
        if (!shouldShow)
        {
            SetMergeQueuePreviewVisible(false);
            return;
        }

        if (selectedUnit == null || !selectedUnit.gameObject.activeInHierarchy)
        {
            SetMergeQueuePreviewVisible(false);
            return;
        }

        int segmentQuantities = Mathf.Max(1, GetMergeQueuePreviewSegmentQuantities());
        float previewMultiplier = GetMergeQueuePreviewMultiplier();
        float speed = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpeed());
        float spacingMultiplier = Mathf.Max(0.2f, GetMergeQueuePreviewSegmentSpacingMultiplier());
        float segmentLen = Mathf.Max(0.08f, GetMirandoPreviewSegmentLength() * previewMultiplier);
        float width = Mathf.Max(0.02f, GetMirandoPreviewWidth() * previewMultiplier);
        Color baseColor = GetMirandoPreviewColor();
        Color spotterColor = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Clamp01(baseColor.a * 0.75f));

        for (int i = 0; i < mergeQueuePreviewTracks.Count; i++)
        {
            MergeQueuePreviewTrack track = mergeQueuePreviewTracks[i];
            if (track == null)
                continue;
            EnsureMergeQueuePreviewRenderers(track, i, segmentQuantities);
            if (track.renderers.Count == 0)
                continue;

            UnitManager donor = track.donor;
            if (donor == null || !donor.gameObject.activeInHierarchy || donor == selectedUnit)
            {
                for (int r = 0; r < track.renderers.Count; r++)
                {
                    LineRenderer renderer = track.renderers[r];
                    if (renderer == null)
                        continue;
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                }
                continue;
            }

            Vector3 from = donor.transform.position;
            Vector3 to = selectedUnit.transform.position;
            from.z = to.z;
            track.pathPoints.Clear();
            track.pathPoints.Add(from);
            track.pathPoints.Add(to);
            track.pathLength = ComputePathLength(track.pathPoints);
            if (track.pathLength <= 0.0001f)
            {
                for (int r = 0; r < track.renderers.Count; r++)
                {
                    LineRenderer renderer = track.renderers[r];
                    if (renderer == null)
                        continue;
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                }
                continue;
            }

            float cycleLen = track.pathLength + segmentLen;
            track.headDistance += speed * Time.deltaTime;
            if (track.headDistance > cycleLen)
                track.headDistance = 0f;

            float spacing = (cycleLen / segmentQuantities) * spacingMultiplier;
            for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
            {
                if (segmentIndex >= track.renderers.Count)
                    break;

                LineRenderer renderer = track.renderers[segmentIndex];
                if (renderer == null)
                    continue;

                float segmentHeadDistance = track.headDistance - (spacing * segmentIndex);
                while (segmentHeadDistance < 0f)
                    segmentHeadDistance += cycleLen;
                while (segmentHeadDistance > cycleLen)
                    segmentHeadDistance -= cycleLen;

                float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
                float endDist = Mathf.Min(segmentHeadDistance, track.pathLength);
                if (endDist <= startDist + 0.0001f)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                BuildPathSegmentPointsFrom(track.pathPoints, startDist, endDist, track.tempSegmentPoints);
                if (track.tempSegmentPoints.Count < 2)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                renderer.startWidth = width;
                renderer.endWidth = width;
                renderer.startColor = spotterColor;
                renderer.endColor = spotterColor;
                renderer.positionCount = track.tempSegmentPoints.Count;
                for (int p = 0; p < track.tempSegmentPoints.Count; p++)
                    renderer.SetPosition(p, track.tempSegmentPoints[p]);
                renderer.enabled = true;
            }

            for (int extra = segmentQuantities; extra < track.renderers.Count; extra++)
            {
                LineRenderer extraRenderer = track.renderers[extra];
                if (extraRenderer == null)
                    continue;
                extraRenderer.positionCount = 0;
                extraRenderer.enabled = false;
            }
        }

        UpdateMergeConfirmPreviewTrack(segmentQuantities, speed, segmentLen, width, spotterColor, spacingMultiplier);
    }

    private void UpdateMergeConfirmPreviewTrack(
        int segmentQuantities,
        float speed,
        float segmentLen,
        float width,
        Color color,
        float spacingMultiplier)
    {
        MergeCandidateEntry selected = null;
        bool hasSelectedCandidate = TryGetSelectedMergeCandidate(out selected);
        bool showConfirmPreview =
            cursorState == CursorState.Fundindo &&
            scannerPromptStep == ScannerPromptStep.MergeConfirm &&
            hasSelectedCandidate &&
            selected != null &&
            selected.unit != null &&
            selected.unit.gameObject.activeInHierarchy &&
            !IsMergeUnitAlreadyQueued(selected.unit) &&
            selected.unit != selectedUnit;

        EnsureMergeQueuePreviewRenderers(mergeConfirmPreviewTrack, 999, segmentQuantities);
        if (mergeConfirmPreviewTrack.renderers.Count == 0)
            return;

        if (!showConfirmPreview)
        {
            for (int r = 0; r < mergeConfirmPreviewTrack.renderers.Count; r++)
            {
                LineRenderer renderer = mergeConfirmPreviewTrack.renderers[r];
                if (renderer == null)
                    continue;
                renderer.positionCount = 0;
                renderer.enabled = false;
            }
            mergeConfirmPreviewTrack.donor = null;
            return;
        }

        UnitManager donor = selected.unit;
        if (mergeConfirmPreviewTrack.donor != donor)
            mergeConfirmPreviewTrack.headDistance = 0f;
        mergeConfirmPreviewTrack.donor = donor;

        Vector3 from = donor.transform.position;
        Vector3 to = selectedUnit.transform.position;
        from.z = to.z;
        mergeConfirmPreviewTrack.pathPoints.Clear();
        mergeConfirmPreviewTrack.pathPoints.Add(from);
        mergeConfirmPreviewTrack.pathPoints.Add(to);
        mergeConfirmPreviewTrack.pathLength = ComputePathLength(mergeConfirmPreviewTrack.pathPoints);
        if (mergeConfirmPreviewTrack.pathLength <= 0.0001f)
        {
            for (int r = 0; r < mergeConfirmPreviewTrack.renderers.Count; r++)
            {
                LineRenderer renderer = mergeConfirmPreviewTrack.renderers[r];
                if (renderer == null)
                    continue;
                renderer.positionCount = 0;
                renderer.enabled = false;
            }
            return;
        }

        float cycleLen = mergeConfirmPreviewTrack.pathLength + segmentLen;
        mergeConfirmPreviewTrack.headDistance += speed * Time.deltaTime;
        if (mergeConfirmPreviewTrack.headDistance > cycleLen)
            mergeConfirmPreviewTrack.headDistance = 0f;

        float spacing = (cycleLen / segmentQuantities) * spacingMultiplier;
        for (int segmentIndex = 0; segmentIndex < segmentQuantities; segmentIndex++)
        {
            if (segmentIndex >= mergeConfirmPreviewTrack.renderers.Count)
                break;

            LineRenderer renderer = mergeConfirmPreviewTrack.renderers[segmentIndex];
            if (renderer == null)
                continue;

            float segmentHeadDistance = mergeConfirmPreviewTrack.headDistance - (spacing * segmentIndex);
            while (segmentHeadDistance < 0f)
                segmentHeadDistance += cycleLen;
            while (segmentHeadDistance > cycleLen)
                segmentHeadDistance -= cycleLen;

            float startDist = Mathf.Max(0f, segmentHeadDistance - segmentLen);
            float endDist = Mathf.Min(segmentHeadDistance, mergeConfirmPreviewTrack.pathLength);
            if (endDist <= startDist + 0.0001f)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            BuildPathSegmentPointsFrom(mergeConfirmPreviewTrack.pathPoints, startDist, endDist, mergeConfirmPreviewTrack.tempSegmentPoints);
            if (mergeConfirmPreviewTrack.tempSegmentPoints.Count < 2)
            {
                renderer.positionCount = 0;
                renderer.enabled = false;
                continue;
            }

            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.startColor = color;
            renderer.endColor = color;
            renderer.positionCount = mergeConfirmPreviewTrack.tempSegmentPoints.Count;
            for (int p = 0; p < mergeConfirmPreviewTrack.tempSegmentPoints.Count; p++)
                renderer.SetPosition(p, mergeConfirmPreviewTrack.tempSegmentPoints[p]);
            renderer.enabled = true;
        }

        for (int extra = segmentQuantities; extra < mergeConfirmPreviewTrack.renderers.Count; extra++)
        {
            LineRenderer extraRenderer = mergeConfirmPreviewTrack.renderers[extra];
            if (extraRenderer == null)
                continue;
            extraRenderer.positionCount = 0;
            extraRenderer.enabled = false;
        }
    }

    private void SetMergeQueuePreviewVisible(bool visible)
    {
        for (int i = 0; i < mergeQueuePreviewTracks.Count; i++)
        {
            MergeQueuePreviewTrack track = mergeQueuePreviewTracks[i];
            if (track == null || track.renderers == null || track.renderers.Count == 0)
                continue;

            for (int r = 0; r < track.renderers.Count; r++)
            {
                LineRenderer renderer = track.renderers[r];
                if (renderer == null)
                    continue;

                if (!visible)
                {
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                    continue;
                }

                renderer.enabled = true;
            }
        }
    }

    private void ClearMergeQueuePreviewData()
    {
        for (int i = 0; i < mergeQueuePreviewTracks.Count; i++)
        {
            MergeQueuePreviewTrack track = mergeQueuePreviewTracks[i];
            if (track == null)
                continue;

            track.donor = null;
            track.pathPoints.Clear();
            track.tempSegmentPoints.Clear();
            track.pathLength = 0f;
            track.headDistance = 0f;
            if (track.renderers != null)
            {
                for (int r = 0; r < track.renderers.Count; r++)
                {
                    LineRenderer renderer = track.renderers[r];
                    if (renderer == null)
                        continue;
                    renderer.positionCount = 0;
                    renderer.enabled = false;
                }
            }
        }
    }

    private bool ConsumeMergeSuppressDefaultConfirmSfxOnce()
    {
        if (!mergeSuppressDefaultConfirmSfxOnce)
            return false;

        mergeSuppressDefaultConfirmSfxOnce = false;
        return true;
    }

    private static void CollectMergeEligibleNeighbors(UnitManager source, Tilemap map, List<UnitManager> output)
    {
        if (output == null)
            return;

        output.Clear();
        if (source == null || map == null)
            return;

        Vector3Int origin = source.CurrentCellPosition;
        origin.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;
            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, source);
            if (other == null || other == source || other.IsEmbarked || !other.gameObject.activeInHierarchy)
                continue;
            if (!AreMergeUnitsSameTypeAndTeam(source, other))
                continue;
            output.Add(other);
        }
    }

    private static bool AreMergeUnitsSameTypeAndTeam(UnitManager source, UnitManager other)
    {
        if (source == null || other == null)
            return false;
        if ((int)source.TeamId != (int)other.TeamId)
            return false;

        string sourceId = source.UnitId;
        string otherId = other.UnitId;
        if (!string.IsNullOrWhiteSpace(sourceId) && !string.IsNullOrWhiteSpace(otherId))
            return string.Equals(sourceId.Trim(), otherId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (source.TryGetUnitData(out UnitData sourceData) && other.TryGetUnitData(out UnitData otherData))
            return sourceData != null && otherData != null && sourceData == otherData;

        return false;
    }

    private static Dictionary<WeaponData, int> BuildMergeWeaponProjectileTotals(UnitManager baseUnit, List<UnitManager> participants)
    {
        Dictionary<WeaponData, int> totals = new Dictionary<WeaponData, int>();
        AccumulateUnitWeaponProjectiles(baseUnit, baseUnit != null ? Mathf.Max(0, baseUnit.CurrentHP) : 0, totals);

        if (participants != null)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                UnitManager participant = participants[i];
                if (participant == null)
                    continue;
                AccumulateUnitWeaponProjectiles(participant, Mathf.Max(0, participant.CurrentHP), totals);
            }
        }

        return totals;
    }

    private static void AccumulateUnitWeaponProjectiles(UnitManager unit, int hp, Dictionary<WeaponData, int> totals)
    {
        if (unit == null || totals == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return;

        int safeHp = Mathf.Max(0, hp);
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            int ammo = Mathf.Max(0, embarked.squadAmmunition);
            int projectiles = ammo * safeHp;
            if (projectiles <= 0)
                continue;

            if (totals.TryGetValue(embarked.weapon, out int current))
                totals[embarked.weapon] = current + projectiles;
            else
                totals.Add(embarked.weapon, projectiles);
        }
    }

    private static int ApplyMergedWeaponAmmunitionToBaseUnit(UnitManager baseUnit, Dictionary<WeaponData, int> projectilesByWeapon, int resultHp)
    {
        if (baseUnit == null || projectilesByWeapon == null)
            return 0;

        IReadOnlyList<UnitEmbarkedWeapon> baseWeapons = baseUnit.GetEmbarkedWeapons();
        if (baseWeapons == null)
            return projectilesByWeapon.Count;

        for (int i = 0; i < baseWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = baseWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;
            if (!projectilesByWeapon.TryGetValue(embarked.weapon, out int totalProjectiles))
                continue;

            embarked.squadAmmunition = resultHp > 0 ? Mathf.Max(0, totalProjectiles / resultHp) : 0;
            projectilesByWeapon.Remove(embarked.weapon);
        }

        return projectilesByWeapon.Count;
    }

    private static Dictionary<SupplyData, int> BuildMergeSupplyStepTotals(UnitManager baseUnit, List<UnitManager> participants)
    {
        Dictionary<SupplyData, int> totals = new Dictionary<SupplyData, int>();
        AccumulateUnitSupplySteps(baseUnit, baseUnit != null ? Mathf.Max(0, baseUnit.CurrentHP) : 0, totals);

        if (participants != null)
        {
            for (int i = 0; i < participants.Count; i++)
            {
                UnitManager participant = participants[i];
                if (participant == null)
                    continue;
                AccumulateUnitSupplySteps(participant, Mathf.Max(0, participant.CurrentHP), totals);
            }
        }

        return totals;
    }

    private static void AccumulateUnitSupplySteps(UnitManager unit, int hp, Dictionary<SupplyData, int> totals)
    {
        if (unit == null || totals == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> supplies = unit.GetEmbarkedResources();
        if (supplies == null)
            return;

        int safeHp = Mathf.Max(0, hp);
        for (int i = 0; i < supplies.Count; i++)
        {
            UnitEmbarkedSupply embarked = supplies[i];
            if (embarked == null || embarked.supply == null)
                continue;

            int amount = Mathf.Max(0, embarked.amount);
            int steps = amount * safeHp;
            if (steps <= 0)
                continue;

            if (totals.TryGetValue(embarked.supply, out int current))
                totals[embarked.supply] = current + steps;
            else
                totals.Add(embarked.supply, steps);
        }
    }

    private static int ApplyMergedSupplyAmountsToBaseUnit(UnitManager baseUnit, Dictionary<SupplyData, int> supplyStepsByType, int resultHp)
    {
        if (baseUnit == null || supplyStepsByType == null)
            return 0;

        IReadOnlyList<UnitEmbarkedSupply> baseSupplies = baseUnit.GetEmbarkedResources();
        if (baseSupplies == null)
            return supplyStepsByType.Count;

        for (int i = 0; i < baseSupplies.Count; i++)
        {
            UnitEmbarkedSupply embarked = baseSupplies[i];
            if (embarked == null || embarked.supply == null)
                continue;
            if (!supplyStepsByType.TryGetValue(embarked.supply, out int totalSteps))
                continue;

            embarked.amount = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;
            supplyStepsByType.Remove(embarked.supply);
        }

        return supplyStepsByType.Count;
    }
}
