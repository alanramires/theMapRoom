using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AIController
{
    [Header("AI Step - revisao de decisoes")]
    [Tooltip("Abre uma revisao antes de enviar cada acao ao replay. Nao altera a dificuldade nem os atalhos F10/F11/F12.")]
    [SerializeField] private bool aiStep;
    public bool AIStepEnabled => aiStep;
    public bool HasPendingAIReview => pendingAIReview != null && pendingAIReview.status == "pendente";

    [System.Serializable]
    public sealed class AIReviewRecord
    {
        public int id, turn, slot, stage, boardRevision, moneyBefore, moneyAfter;
        public string status, decision, mission, reasons, commands, result;
        public string current_stance, stanceSource;
        public string shoppingSavingSummary;
        public int schemaVersion;
        public string sessionId, recordedUtc, actionJson, rawReasons, coverage, filePath, markNote;
        public bool markedStrange, hasStateBeforeDecision, hasStateBeforeExecution;
        public string snapshotWarning, markClassification;
        public bool marked;
        public int candidatesObserved, replayActionIndex = -1;
        public SaveGameManager.AIReviewSaveReference nearestSave;
        public AIReviewState stateBeforeDecision, stateBeforeExecution;
        // A decisao nao mexe no tabuleiro (contrato transacional): com a mesma revisao, a foto
        // antes da execucao seria igual a de antes da decisao e nao e tirada de novo.
        public bool executionSameAsDecision;
        // Custo do proprio AI Step neste passo (foto + registro), em ms. E o numero para medir.
        public float captureMs, persistMs;
        public List<AIReviewCandidate> candidates = new List<AIReviewCandidate>();
        public List<AIReviewParticipant> participants = new List<AIReviewParticipant>();
    }

    [System.Serializable]
    public sealed class AIReviewParticipant
    {
        public int unitId;
        public string name, role, source;
        public Vector3Int cell;
        public bool hasRelatedCell;
        public Vector3Int relatedCell;
    }

    private sealed class AIReviewParticipantTrace
    {
        public PlayerAction choice;
        public int unitId;
        public string role, source;
        public Vector3Int? relatedCell;
    }

    private readonly List<AIReviewParticipantTrace> aiReviewParticipantTraces = new List<AIReviewParticipantTrace>();

    private void TraceAIReviewParticipant(PlayerAction choice, int unitId, string role, string source, Vector3Int? relatedCell = null)
    {
        if (!IsCollectingAIReview || choice == null || unitId < 0) return;
        aiReviewParticipantTraces.Add(new AIReviewParticipantTrace
        { choice = choice, unitId = unitId, role = role, source = source, relatedCell = relatedCell });
    }

    private void TraceAIReviewHandoffParticipants(PlayerAction choice, UnitManager actor,
        ConstructionManager origin, SectorObjective objective)
    {
        if (!IsCollectingAIReview || actor == null || objective?.Slots == null) return;
        // Herdeiro escolhido pelo planejador, quando ha: so ele e o substituto. Os
        // demais ocupantes de vaga podem estar em outra tarefa (a ponta de outro predio).
        if (objective.HandoffHeirUnitId >= 0 && objective.HandoffHeirUnitId != actor.InstanceId)
        {
            TraceAIReviewParticipant(choice, objective.HandoffHeirUnitId, "Herdeiro escolhido na captura", "planned_handoff",
                origin != null ? origin.CurrentCellPosition : (Vector3Int?)null);
            return;
        }
        foreach (var need in objective.Slots)
            if (need != null && need.Filled && need.Role == UnitRole.Capturador && need.AssignedUnitId != actor.InstanceId)
                TraceAIReviewParticipant(choice, need.AssignedUnitId, "Substituto previsto na captura", "planned_handoff",
                    origin != null ? origin.CurrentCellPosition : (Vector3Int?)null);
    }

    private List<AIReviewParticipant> BuildAIReviewParticipants(AIReviewRecord record, PlayerAction action)
    {
        var result = new List<AIReviewParticipant>();
        var state = record.stateBeforeExecution ?? record.stateBeforeDecision;
        if (state?.units == null || !int.TryParse(action.UnitInstanceId, out int actorId)) return result;
        void Add(int id, string role, string source, Vector3Int? relatedCell = null)
        {
            if (id == actorId || id < 0) return;
            var unit = state.units.Find(x => x.id == id);
            if (unit == null) return; // Only units present in the frozen, fog-honest snapshot.
            var existing = result.Find(x => x.unitId == id);
            if (existing == null)
            {
                result.Add(new AIReviewParticipant
                {
                    unitId = id, name = unit.name, cell = unit.cell, role = role, source = source,
                    hasRelatedCell = relatedCell.HasValue, relatedCell = relatedCell ?? default
                });
            }
            else
            {
                if (!existing.role.Contains(role)) existing.role += "; " + role;
                if (!existing.source.Contains(source)) existing.source += ";" + source;
                if (relatedCell.HasValue) { existing.hasRelatedCell = true; existing.relatedCell = relatedCell.Value; }
            }
        }
        foreach (var trace in aiReviewParticipantTraces)
            if (ReferenceEquals(trace.choice, action)) Add(trace.unitId, trace.role, trace.source, trace.relatedCell);

        // Existing selected pairings/reservations: read only, never run a matching scan.
        foreach (var pair in transportPickupBeacons)
        {
            if (pair.Key == actorId) Add(pair.Value, "Passageiro potencial pareado", "pickup_beacon");
            if (pair.Value == actorId) Add(pair.Key, "Transportador pareado", "pickup_beacon");
        }
        foreach (var manifest in tacticalPickupManifestBeacons)
        {
            if (manifest.Value == null) continue;
            if (manifest.Key == actorId)
                foreach (int passengerId in manifest.Value) Add(passengerId, "Passageiro potencial na LZ", "tactical_manifest");
            if (manifest.Value.Contains(actorId)) Add(manifest.Key, "Transportador com encontro previsto na LZ", "tactical_manifest");
        }
        foreach (var unit in state.units)
        {
            if (unit.id == actorId && unit.transportTargetId > 0)
                Add(unit.transportTargetId, "Passageiro com promessa de resgate", "ride_promise");
            if (unit.transportTargetId == actorId)
                Add(unit.id, "Transportador com promessa de resgate", "ride_promise");
            if (unit.id == actorId && unit.embarkedTransporterId > 0)
                Add(unit.embarkedTransporterId, "Transporte atual", "current_carrier");
            if (unit.embarkedTransporterId == actorId)
                Add(unit.id, "Passageiro a bordo", "current_cargo");
        }
        foreach (var pending in pendingAIDesignatedMissions)
            if (pending.Value.Intent == AIPlanRuntimeIntent.Transport)
            {
                if (pending.Key == actorId) Add(pending.Value.TargetUnitInstanceId, "Passageiro da coleta planejada", "pending_transport_mission", pending.Value.TargetCell);
                if (pending.Value.TargetUnitInstanceId == actorId) Add(pending.Key, "Transportador da coleta planejada", "pending_transport_mission", pending.Value.TargetCell);
            }
        if (action.SensorAction == SensorActionType.Embark || action.SensorAction == SensorActionType.Disembark
            || action.SensorAction == SensorActionType.Merge || action.SensorAction == SensorActionType.Supply || action.SensorAction == SensorActionType.Transfer)
        {
            if (int.TryParse(action.TargetInstanceId, out int targetId)) Add(targetId, "Alvo da interacao", "command_target");
            if (action.SubSteps != null)
                foreach (var step in action.SubSteps)
                    if (step != null && int.TryParse(step.TargetInstanceId, out int subTarget)) Add(subTarget, "Envolvido no comando " + step.Label, "command_substep");
        }
        // Reverse handoff: the successor can also see who is yielding the capture.
        if (!string.IsNullOrEmpty(state.planJson) && state.planJson != "null")
        {
            var plan = JsonUtility.FromJson<TeamObjectivePlan>(state.planJson);
            if (plan?.Objectives != null)
                foreach (var objective in plan.Objectives)
                    if (objective != null && objective.PreferredHandoffFromUnitId >= 0 && objective.PreferredHandoffFromUnitId != actorId
                        && objective.Slots != null && objective.Slots.Exists(x => x.Filled && x.Role == UnitRole.Capturador && x.AssignedUnitId == actorId))
                        Add(objective.PreferredHandoffFromUnitId, "Unidade que cede a captura no plano", "objective_handoff");
        }
        result.Sort((a, b) => a.unitId.CompareTo(b.unitId));
        return result;
    }

    public static List<AIReviewVisualPair> BuildAIReviewParticipantVisualPairs(AIReviewRecord record, Vector3Int actorCell)
    {
        var pairs = new List<AIReviewVisualPair>();
        if (record?.participants == null) return pairs;
        var color = new Color(1f, 0.25f, 0.65f, 1f);
        foreach (var participant in record.participants)
        {
            pairs.Add(new AIReviewVisualPair { From = participant.cell, To = participant.cell, Label = "AIReviewParticipant", Color = color });
            Vector3Int anchor = participant.hasRelatedCell ? participant.relatedCell : actorCell;
            if (anchor != participant.cell)
                pairs.Add(new AIReviewVisualPair { From = participant.cell, To = anchor, Label = "AIReviewRelationship", Color = color });
        }
        return pairs;
    }

    public bool FocusAIReviewParticipant(int unitId)
    {
        var participant = pendingAIReview?.participants?.Find(x => x.unitId == unitId);
        if (!HasPendingAIReview || participant == null) return false;
        return FocusAIReviewCell(participant.cell);
    }

    private bool FocusAIReviewCell(Vector3Int cell)
    {
        var map = boardTilemap != null ? boardTilemap : turnStateManager?.MovementTilemapRef;
        var camera = FindAnyObjectByType<CameraController>();
        if (map == null || camera == null) return false;
        camera.FocusOn(HexCoordinates.GetCellCenterWorld(map, cell), instant: false);
        return true;
    }

    private void DrawAIReviewParticipants(AIReviewRecord record, GUIStyle style)
    {
        if (record.participants == null || record.participants.Count == 0) return;
        GUILayout.Label("ENVOLVIDOS NA DECISAO ESCOLHIDA - nao agem agora", style);
        foreach (var participant in record.participants)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{participant.name} #{participant.unitId} {FormatCell(participant.cell)}: {participant.role}", style);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && HasPendingAIReview;
            if (GUILayout.Button("Focar #" + participant.unitId, GUILayout.Width(90f)))
            {
                GUI.FocusControl(null); aiReviewNoteFocused = false;
                FocusAIReviewParticipant(participant.unitId);
            }
            GUI.enabled = wasEnabled;
            GUILayout.EndHorizontal();
        }
        GUILayout.Label("Azul = envolvido/pareamento; ligacao sem seta nao e uma ordem de movimento nem garante embarque.", style);
    }


    [System.Serializable]
    public sealed class AIReviewCandidate
    {
        public string category, action, targetId, status, gate, reason, scoreScope;
        public Vector3Int cell;
        public bool hasScore, hasRally, rally, allowed, evaluated = true;
        public float score, distance;
        public int priority, rankWithinScope;
        public bool hasPreference, selected;
    }

    [System.Serializable]
    public sealed class AIReviewUnitState
    {
        public int id, slot, hp, movement, ammo, transportTargetId = -1, embarkedTransporterId = -1;
        public float fuel;
        public string type, name;
        public Vector3Int cell;
        public bool acted, embarked;
    }

    [System.Serializable]
    public sealed class AIReviewBuildingState
    {
        public int id, ownerTeam, capturePoints;
        public Vector3Int cell;
        public string type;
        public bool visible, remembered;
    }

    [System.Serializable]
    public sealed class AIReviewTileState
    {
        public Vector3Int cell;
        public string tile;
        public bool explored;
    }

    [System.Serializable]
    public sealed class AIReviewState
    {
        public string sampledUtc, planJson, difficulty, actorId;
        public string scope = "slot_visible_and_remembered; diagnostic snapshot, not a restorable save";
        public int turn, slot, boardRevision, money;
        public List<AIReviewUnitState> units = new List<AIReviewUnitState>();
        public List<AIReviewBuildingState> buildings = new List<AIReviewBuildingState>();
        public List<AIReviewTileState> terrain = new List<AIReviewTileState>();
        public List<Vector3Int> reservedDestinations = new List<Vector3Int>();
    }

    private readonly List<AIReviewCandidate> aiReviewCandidates = new List<AIReviewCandidate>();
    private AIReviewState aiReviewDecisionState;
    private string aiReviewActorId;
    private int aiReviewCandidateCount;
    private string aiReviewSession;
    private string aiReviewFileMessage;
    public string AIReviewFileMessage => aiReviewFileMessage;
    public string AIReviewDirectory => System.IO.Path.Combine(Application.persistentDataPath, "AIReviews", aiReviewSession ??= System.Guid.NewGuid().ToString("N"));
    public bool IsCollectingAIReview => aiStep && aiReviewCapturing;

    // Called at the existing scoring/gate sites, independent of Console log flags.
    public void TraceAIReviewCandidate(string category, string action, Vector3Int cell,
        string targetId, bool allowed, string gate, string reason,
        bool hasScore = false, float score = 0f, bool hasRally = false, bool rally = false,
        bool evaluated = true, string scoreScope = null, bool hasPreference = false, int priority = 0, float distance = 0f)
    {
        if (!IsCollectingAIReview) return;
        aiReviewCandidateCount++;
        var item = new AIReviewCandidate
        {
            category = category, action = action, cell = cell, targetId = targetId,
            allowed = allowed, gate = gate, reason = reason, hasScore = hasScore, score = score,
            hasRally = hasRally, rally = rally, evaluated = evaluated,
            scoreScope = scoreScope ?? category, hasPreference = hasPreference, priority = priority, distance = distance,
            status = evaluated ? (allowed ? "eligible" : "blocked") : "not_evaluated"
        };
        // Keep the top three scored candidates and three gated candidates per scope.
        // Scores are NEVER compared between different policies/scopes.
        var group = aiReviewCandidates.FindAll(x => x.scoreScope == item.scoreScope && x.hasScore == hasScore && x.allowed == allowed && x.evaluated == evaluated);
        if (group.Count >= 3)
        {
            if (!hasScore) return;
            AIReviewCandidate worst = group[0];
            foreach (var candidate in group) if (candidate.score < worst.score) worst = candidate;
            if (score <= worst.score) return;
            aiReviewCandidates.Remove(worst);
        }
        if (aiReviewCandidates.Count < 96) aiReviewCandidates.Add(item);
    }

    private AIReviewState CaptureAIReviewState(string actorId)
    {
        try { return CaptureAIReviewStateCore(actorId); }
        catch (System.Exception ex)
        {
            aiReviewFileMessage = "Snapshot de diagnostico indisponivel: " + ex.Message;
            Debug.LogWarning(aiReviewFileMessage);
            return null; // Observation must never change the action chosen by the game.
        }
    }

    private AIReviewState CaptureAIReviewStateCore(string actorId)
    {
        if (matchController == null) return null;
        PlayerSlotId observer = matchController.ActiveSlotId;
        var state = new AIReviewState
        {
            sampledUtc = System.DateTime.UtcNow.ToString("O"), actorId = actorId,
            turn = matchController.CurrentTurn, slot = observer.Value,
            boardRevision = ThreatRevisionTracker.GlobalBoardRevision,
            money = matchController.GetActualMoney(observer), difficulty = AppliedDifficulty.ToString(),
            planJson = ObjectiveManager.GetPlanForSlot(observer) != null ? JsonUtility.ToJson(ObjectiveManager.GetPlanForSlot(observer)) : "null",
            reservedDestinations = new List<Vector3Int>(plannedDestinations)
        };
        // Visibilidade pela nevoa CONFIRMADA (a mesma que o jogo e a IA leem), nao recalculada sem
        // cache: o AI Step registra o que o jogo sabe, nao refaz o jogo por baixo dele.
        foreach (var unit in UnitManager.AllActive)
        {
            if (unit == null || unit.IsDead) continue;
            if (unit.SlotIndex != observer.Value && (unit.IsEmbarked || !matchController.IsUnitVisibleForSlot(unit, observer))) continue;
            state.units.Add(new AIReviewUnitState
            {
                id = unit.InstanceId, slot = unit.SlotIndex, hp = unit.CurrentHP,
                movement = unit.RemainingMovementPoints, fuel = unit.CurrentFuel, ammo = unit.CurrentAmmo,
                type = unit.UnitId, name = unit.UnitDisplayName, cell = unit.CurrentCellPosition,
                transportTargetId = unit.AIHasDesignatedMission && unit.AIDesignatedMissionIntent == AIPlanRuntimeIntent.Transport
                    ? unit.AIDesignatedMissionTargetUnitInstanceId : -1,
                embarkedTransporterId = unit.EmbarkedTransporter != null ? unit.EmbarkedTransporter.InstanceId : -1,
                acted = unit.HasActed, embarked = unit.IsEmbarked
            });
        }
        foreach (var building in ConstructionManager.AllActive)
        {
            if (building == null) continue;
            var cell = building.CurrentCellPosition;
            bool visible = building.SlotIndex == observer.Value || matchController.IsCellGeographicallyVisibleForActiveSlot(cell);
            bool remembered = matchController.TryGetKnownConstructionAtCell(observer, cell, out var knownData, out var knownOwner);
            if (!visible && !remembered) continue;
            state.buildings.Add(new AIReviewBuildingState
            {
                id = visible ? building.InstanceId : 0, cell = cell,
                ownerTeam = (int)(visible ? building.TeamId : knownOwner),
                capturePoints = visible ? building.CurrentCapturePoints : -1,
                type = visible ? building.name : knownData.name, visible = visible, remembered = remembered
            });
        }
        // Terreno NAO entra na foto de cada passo: e do bake e nao muda na partida, e varrer o
        // retangulo inteiro por unidade era o grosso do custo. So o passo MARCADO o recebe
        // (CaptureAIReviewTerrain, em MarkAIReview); o resto se reproduz por save + replay.
        return state;
    }

    private void CaptureAIReviewTerrain(AIReviewState state)
    {
        if (state == null || matchController == null || boardTilemap == null
            || (state.terrain != null && state.terrain.Count > 0))
            return;
        state.terrain ??= new List<AIReviewTileState>();
        PlayerSlotId observer = PlayerSlotId.FromIndex(state.slot);
        foreach (var cell in boardTilemap.cellBounds.allPositionsWithin)
        {
            bool explored = matchController.IsCellExploredBySlot(observer, cell);
            if (!explored && !matchController.IsCellKnownForActiveTeam(cell)) continue;
            var tile = boardTilemap.GetTile(cell);
            if (tile != null) state.terrain.Add(new AIReviewTileState { cell = cell, tile = tile.name, explored = explored });
        }
    }

    private static string NormalizeAIReviewCoordinates(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*0\s*\)", "($1,$2)");
        return System.Text.RegularExpressions.Regex.Replace(text, @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)", "($1,$2)");
    }

    private List<AIReviewCandidate> FinalizeAIReviewCandidates(PlayerAction action)
    {
        var result = new List<AIReviewCandidate>();
        foreach (var source in aiReviewCandidates)
        {
            var item = JsonUtility.FromJson<AIReviewCandidate>(JsonUtility.ToJson(source));
            bool matches = action.ActionType == PlayerActionType.Shopping
                ? item.action == "Shopping" && item.targetId == action.ShoppingUnitTypeId && item.cell == action.CursorHex
                : item.action == action.SensorAction.ToString()
                    && (item.cell == action.MoveTo || (action.HasTargetHex && item.cell == action.TargetHex))
                    && (action.SensorAction != SensorActionType.Attack && action.SensorAction != SensorActionType.Embark
                        || item.targetId == action.TargetInstanceId);
            item.selected = item.evaluated && item.allowed && matches;
            if (item.selected) item.status = "selected";
            else if (item.allowed && item.evaluated) item.status = "evaluated_not_selected";
            result.Add(item);
        }
        if (!result.Exists(x => x.selected)) result.Insert(0, new AIReviewCandidate
        {
            category = "Decision", action = action.ActionType == PlayerActionType.Shopping ? "Shopping" : action.SensorAction.ToString(),
            targetId = action.TargetInstanceId, cell = action.MoveTo, allowed = true, selected = true,
            status = "selected", gate = "final_action", reason = action.DebugLabel,
            scoreScope = "final_action_without_global_score"
        });
        foreach (var item in result)
            if (item.status == "evaluated_not_selected" && item.hasScore
                && result.Exists(x => x.selected && x.hasScore && x.scoreScope == item.scoreScope && x.score > item.score))
            { item.gate = "lower_score_in_same_scope"; item.reason = "Pontuacao menor que a escolhida neste mesmo grupo de comparacao."; }
        foreach (var item in result)
            if (item.hasScore && item.allowed) item.rankWithinScope = 1 + result.FindAll(
                x => x.allowed && x.hasScore && x.scoreScope == item.scoreScope && x.score > item.score).Count;
        result.Sort((a, b) =>
        {
            if (a.selected != b.selected) return a.selected ? -1 : 1;
            int group = string.CompareOrdinal(a.scoreScope, b.scoreScope);
            if (group != 0) return group;
            if (a.hasScore && b.hasScore) return b.score.CompareTo(a.score);
            return a.hasScore == b.hasScore ? 0 : (a.hasScore ? -1 : 1);
        });
        return result;
    }

    private void InitializeAIReviewRecord(PlayerAction action)
    {
        try { InitializeAIReviewRecordCore(action); }
        catch (System.Exception ex)
        {
            pendingAIReview.snapshotWarning = "Registro de diagnostico incompleto: " + ex.Message;
            aiReviewFileMessage = pendingAIReview.snapshotWarning;
            Debug.LogWarning(aiReviewFileMessage);
        }
    }

    private void InitializeAIReviewRecordCore(PlayerAction action)
    {
        var record = pendingAIReview;
        record.schemaVersion = 2;
        record.current_stance = aiReviewCurrentStance;
        record.stanceSource = aiReviewStanceSource;
        record.sessionId = aiReviewSession ??= System.Guid.NewGuid().ToString("N");
        record.recordedUtc = System.DateTime.UtcNow.ToString("O");
        record.replayActionIndex = replayManager?.ActionStack?.Actions != null ? replayManager.ActionStack.Actions.Count : -1;
        CaptureAIReviewSaveReference(record);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        record.stateBeforeDecision = (currentAIStage == 2 || currentAIStage == 3)
            && aiReviewDecisionState != null && aiReviewDecisionState.turn == record.turn
            && aiReviewDecisionState.slot == record.slot && aiReviewDecisionState.actorId == action.UnitInstanceId
            ? aiReviewDecisionState : CaptureAIReviewState(action.UnitInstanceId);
        // Mesma revisao do tabuleiro = nada confirmado entre decidir e executar: a segunda foto
        // seria identica. So o shopping (varias compras sobre a mesma foto) costuma mudar aqui.
        record.executionSameAsDecision = record.stateBeforeDecision != null
            && record.stateBeforeDecision.boardRevision == ThreatRevisionTracker.GlobalBoardRevision;
        record.stateBeforeExecution = record.executionSameAsDecision
            ? null : CaptureAIReviewState(action.UnitInstanceId);
        record.captureMs = (float)clock.Elapsed.TotalMilliseconds + aiReviewDecisionCaptureMs;
        aiReviewDecisionCaptureMs = 0f; // conta uma vez: as compras seguintes reusam a mesma foto
        record.hasStateBeforeDecision = record.stateBeforeDecision != null;
        record.hasStateBeforeExecution = record.stateBeforeExecution != null || record.executionSameAsDecision;
        if (!record.hasStateBeforeDecision || !record.hasStateBeforeExecution)
            record.snapshotWarning = "Uma das fotos nao pode ser capturada; este registro nao deve ser usado como prova do estado completo.";
        record.participants = BuildAIReviewParticipants(record, action);
        record.actionJson = JsonUtility.ToJson(action); // captured before attaching record: no serialization cycle
        record.candidates = currentAIStage == 2 || currentAIStage == 3 ? FinalizeAIReviewCandidates(action) : new List<AIReviewCandidate>();
        record.candidatesObserved = currentAIStage == 2 || currentAIStage == 3 ? aiReviewCandidateCount : 0;
        record.coverage = "Instrumented: nearby capture, capture sensor, capturer embark gates/options, attack policy, unit picker. Other policies remain in rawReasons; unevaluated branches have no invented score. Top 3 per scope; scores comparable only within scoreScope.";
        record.rawReasons = record.reasons;
        record.reasons = NormalizeAIReviewCoordinates(record.reasons);
        record.commands = NormalizeAIReviewCoordinates(record.commands);
        action.AIReview = record;
        PersistAIReviewRecord(record);
    }

    [System.Serializable]
    public sealed class AIReviewMark
    {
        public int format = 1;
        public string markId, markedAtUtc;
        public string classification = "estranho";
        public AIReviewRecord step;
    }

    private static readonly string[] aiReviewMarkClassLabels = { "Dúvida", "Estranho", "Possível bug", "Sugestão", "Observação" };
    private static readonly string[] aiReviewMarkClassCodes = { "duvida", "estranho", "possivel_bug", "sugestao", "observacao" };
    private int aiReviewMarkClassIndex;

    public static string NormalizeAIReviewClassification(string classification)
    {
        string code = (classification ?? "").Trim().ToLowerInvariant().Replace(' ', '_');
        switch (code)
        {
            case "dúvida": case "duvida": return "duvida";
            case "possível_bug": case "possivel_bug": return "possivel_bug";
            case "sugestão": case "sugestao": return "sugestao";
            case "observação": case "observacao": return "observacao";
            default: return "estranho";
        }
    }

    private string aiReviewNote = "";
    private bool aiReviewNoteFocused;
    public bool IsAIReviewNoteEditing => aiReviewNoteFocused && aiReviewVisible && (aiStep || HasPendingAIReview) && isActiveAndEnabled;
    public string AIReviewMarksPath
    {
        get
        {
#if UNITY_EDITOR
            string root = System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
#else
            string root = Application.persistentDataPath;
#endif
            return System.IO.Path.Combine(root, "ai_step_marcas.jsonl");
        }
    }

    private void CaptureAIReviewSaveReference(AIReviewRecord record)
    {
        try
        {
            var saves = FindAnyObjectByType<SaveGameManager>();
            record.nearestSave = saves != null ? saves.GetClosestAIReviewSave(record.replayActionIndex)
                : new SaveGameManager.AIReviewSaveReference { found = false, relation = "unavailable", explanation = "SaveGameManager nao encontrado." };
        }
        catch (System.Exception ex)
        {
            record.nearestSave = new SaveGameManager.AIReviewSaveReference { found = false, relation = "unavailable", explanation = ex.Message };
        }
    }

    public static string NormalizeAIReviewNote(string note)
    {
        note = (note ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return note.Length > 2048 ? note.Substring(0, 2048) : note;
    }

    private static void WriteAIReviewMarkLine(string path, AIReviewMark mark)
    {
        // Serialize now: later execution must not mutate an earlier mark's snapshot/note.
        string line = JsonUtility.ToJson(mark, false);
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Append,
            System.IO.FileAccess.Write, System.IO.FileShare.Read))
        using (var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            writer.WriteLine(line);
    }

    private bool AppendAIReviewMark(AIReviewRecord record)
    {
        try
        {
            var mark = new AIReviewMark
            {
                markId = System.Guid.NewGuid().ToString("N"), markedAtUtc = System.DateTime.UtcNow.ToString("O"),
                classification = record.markClassification, step = record
            };
            WriteAIReviewMarkLine(AIReviewMarksPath, mark);
            aiReviewFileMessage = "Marca adicionada: " + AIReviewMarksPath;
            return true;
        }
        catch (System.Exception ex)
        {
            aiReviewFileMessage = "Falha ao acumular a marca: " + ex.Message;
            Debug.LogWarning(aiReviewFileMessage);
            return false;
        }
    }


    private bool PersistAIReviewRecord(AIReviewRecord record)
    {
        if (record == null) return false;
        try
        {
            string directory = System.IO.Path.Combine(Application.persistentDataPath, "AIReviews", record.sessionId);
            System.IO.Directory.CreateDirectory(directory);
            record.filePath = System.IO.Path.Combine(directory, $"step-{record.id:D6}.json");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            void Write(string path)
            {
                string temp = path + ".tmp";
                // Compacto: o formatado inflava o arquivo e a escrita, e quem le e ferramenta.
                System.IO.File.WriteAllText(temp, JsonUtility.ToJson(record, false), new System.Text.UTF8Encoding(false));
                if (System.IO.File.Exists(path)) System.IO.File.Replace(temp, path, null);
                else System.IO.File.Move(temp, path);
            }
            Write(record.filePath);
            record.persistMs = (float)clock.Elapsed.TotalMilliseconds;
            aiReviewFileMessage = record.markedStrange ? "Passo marcado e salvo: " + record.filePath : "Registro salvo: " + record.filePath;
            return true;
        }
        catch (System.Exception ex)
        {
            aiReviewFileMessage = "Falha ao salvar AI Step: " + ex.Message;
            Debug.LogWarning(aiReviewFileMessage);
            return false;
        }
    }

    // Compatibility for callers that explicitly mark a step as strange.
    public bool MarkAIReviewStrange(string note = "") => MarkAIReview(note, "estranho");

    public bool MarkAIReview(string note = "", string classification = "duvida")
    {
        var record = pendingAIReview ?? (aiReviewHistory.Count > 0 ? aiReviewHistory[aiReviewHistory.Count - 1] : null);
        if (record == null) return false;
        record.marked = record.markedStrange = true; // markedStrange is retained for old readers.
        record.markClassification = NormalizeAIReviewClassification(classification);
        record.markNote = NormalizeAIReviewNote(note);
        CaptureAIReviewSaveReference(record);
        // O passo marcado e o que vai ser lido: ai sim vale a foto completa. Com o passo ainda
        // pendente o tabuleiro e o da decisao (nada confirmado), entao o terreno casa com ela.
        // Passo ja executado nao recebe terreno: seria de outro momento.
        if (ReferenceEquals(record, pendingAIReview))
            CaptureAIReviewTerrain(record.stateBeforeExecution ?? record.stateBeforeDecision);
        PersistAIReviewRecord(record);
        bool saved = AppendAIReviewMark(record);
        if (saved) DiagnosticoManager.Registrar(new DiagnosticoEntrada
        {
            rodada = record.turn, slot = record.slot, categoria = "ai_step", operacao = "ai_step.marked",
            severidade = record.markClassification == "estranho" || record.markClassification == "possivel_bug"
                ? DiagnosticoSeveridade.Aviso : DiagnosticoSeveridade.Depuracao, motivo = record.decision
        }.Campo("sessionId", record.sessionId).Campo("stepId", record.id).Campo("filePath", AIReviewMarksPath).Campo("note", record.markNote).Campo("classification", record.markClassification));
        return saved;
    }

    private int aiReviewPreviewCandidate = -1;
    private int aiReviewDrawnCandidate = -2;
    private string aiReviewPreviewMessage;

    public bool SelectAIReviewCandidatePreview(int index)
    {
        if (!HasPendingAIReview || pendingAIReview.candidates == null || index < 0 || index >= pendingAIReview.candidates.Count)
            return false;
        aiReviewPreviewCandidate = index;
        aiReviewDrawnCandidate = -2;
        aiReviewPreviewMessage = "Previa da alternativa; Executar decisao continua executando a escolha original da IA.";
        return true;
    }

    public void ResetAIReviewCandidatePreview()
    {
        aiReviewPreviewCandidate = -1;
        aiReviewDrawnCandidate = -2;
        aiReviewPreviewMessage = null;
    }

    // Pure presentation from the stored record. Does not query live units, sensors or AI.
    public static List<AIReviewVisualPair> BuildAIReviewCandidateVisualPairs(AIReviewRecord record, int index, out string message)
    {
        var empty = new List<AIReviewVisualPair>();
        message = "Alternativa sem geometria registrada.";
        if (record?.candidates == null || index < 0 || index >= record.candidates.Count) return empty;
        var candidate = record.candidates[index];
        if (!candidate.evaluated)
        {
            message = "Sem linha: este ramo nao foi avaliado. " + candidate.gate + ": " + candidate.reason;
            return empty;
        }
        if (string.IsNullOrEmpty(record.actionJson)) return empty;
        var action = JsonUtility.FromJson<PlayerAction>(record.actionJson);
        if (action == null) return empty;
        var state = record.stateBeforeDecision ?? record.stateBeforeExecution;
        bool StoredTarget(string unitId, string buildingId, out Vector3Int cell)
        {
            cell = default;
            if (state == null) return false;
            if (!string.IsNullOrEmpty(unitId) && state.units != null)
                foreach (var unit in state.units)
                    if (unit.id.ToString() == unitId) { cell = unit.cell; return true; }
            if (!string.IsNullOrEmpty(buildingId) && state.buildings != null)
                foreach (var building in state.buildings)
                    if (building.id.ToString() == buildingId) { cell = building.cell; return true; }
            return false;
        }
        Vector3Int origin = action.HasMoveFrom ? action.MoveFrom : action.CursorHex;
        if (!action.HasMoveFrom && !action.HasCursorHex) return empty;
        if (candidate.selected)
        {
            // Fill targets only from frozen data, clearing unresolved IDs to prevent live fallbacks.
            if (!action.HasTargetHex)
                action.HasTargetHex = StoredTarget(action.TargetInstanceId, action.TargetConstructionId, out action.TargetHex);
            action.TargetInstanceId = action.TargetConstructionId = null;
            if (action.SubSteps != null)
                foreach (var step in action.SubSteps)
                    if (step != null)
                    {
                        if (!step.HasTargetHex) step.HasTargetHex = StoredTarget(step.TargetInstanceId, step.TargetConstructionId, out step.TargetHex);
                        step.TargetInstanceId = step.TargetConstructionId = null;
                    }
            message = "Previa da decisao escolhida pela IA.";
            if (action.ActionType == PlayerActionType.Shopping)
                return new List<AIReviewVisualPair> { new AIReviewVisualPair { From = action.CursorHex, To = action.CursorHex, Label = "Shopping", Color = Color.yellow } };
            return BuildAIReviewVisualPairs(action);
        }
        var preview = new PlayerAction
        {
            ActionType = PlayerActionType.UnitAction, HasMoveFrom = true, MoveFrom = origin,
            HasMoveTo = true, MoveTo = candidate.cell
        };
        if (candidate.action == "Shopping")
        {
            message = "Produtora registrada para esta alternativa de compra.";
            return new List<AIReviewVisualPair> { new AIReviewVisualPair { From = candidate.cell, To = candidate.cell, Label = "Shopping", Color = Color.yellow } };
        }
        if (!System.Enum.TryParse(candidate.action, out SensorActionType kind)) return empty;
        preview.SensorAction = kind;
        if (kind == SensorActionType.Attack)
        {
            if (!StoredTarget(candidate.targetId, null, out var enemyCell))
            {
                message = "Sem linha de ataque: posicao do alvo ausente na foto deste passo.";
                return empty;
            }
            // For attack candidates, cell is the firing position, not the target's hex.
            preview.HasTargetHex = true;
            preview.TargetHex = enemyCell;
        }
        else if (kind == SensorActionType.Embark)
        {
            if (string.IsNullOrEmpty(candidate.targetId))
            {
                message = "Sem linha: o gate de embarque nao registrou um transporte alvo. " + candidate.reason;
                return empty;
            }
            preview.MoveTo = origin;
            preview.HasTargetHex = true;
            preview.TargetHex = candidate.cell;
        }
        else if (kind != SensorActionType.None && kind != SensorActionType.Capture && kind != SensorActionType.Land)
        {
            message = "Sem linha: esta alternativa nao registrou todos os pares necessarios.";
            return empty;
        }
        message = candidate.allowed
            ? "Previa da alternativa registrada; nao altera a decisao a executar."
            : "Alternativa recusada: " + candidate.gate + ". A linha nao significa que a acao seja permitida.";
        if (kind == SensorActionType.Embark) message += " Ligacao ao transporte; caminho de aproximacao nao registrado.";
        var pairs = BuildAIReviewVisualPairs(preview);
        if (!candidate.allowed)
            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i]; pair.Color = Color.Lerp(pair.Color, Color.gray, 0.45f); pairs[i] = pair;
            }
        return pairs;
    }

    private void DrawAIReviewAlternatives(AIReviewRecord record, GUIStyle style)
    {
        if (!string.IsNullOrEmpty(record.snapshotWarning)) GUILayout.Label(record.snapshotWarning, style);
        GUILayout.Label("ALTERNATIVAS E GATES - clique para visualizar no mapa", style);
        if (GUILayout.Button("Voltar a decisao escolhida")) ResetAIReviewCandidatePreview();
        if (!string.IsNullOrEmpty(aiReviewPreviewMessage)) GUILayout.Label(aiReviewPreviewMessage, style);
        if (record.candidates == null || record.candidates.Count == 0)
        {
            GUILayout.Label("Nenhum candidato estruturado registrado neste caminho; consulte os motivos. Nao houve recalculo.", style);
            return;
        }
        var rowStyle = new GUIStyle(GUI.skin.button)
        {
            wordWrap = true, alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 8, 8, 8), margin = new RectOffset(0, 0, 3, 3)
        };
        for (int i = 0; i < record.candidates.Count; i++)
        {
            var candidate = record.candidates[i];
            string text = (aiReviewPreviewCandidate == i ? "[VISUALIZANDO] " : "")
                + (candidate.selected ? "[ESCOLHIDA PELA IA] " : "")
                + $"[{candidate.category}] {candidate.action} {FormatCell(candidate.cell)} alvo={candidate.targetId} | {candidate.status}"
                + (candidate.hasScore ? $" score={candidate.score:0.###} rank={candidate.rankWithinScope} grupo={candidate.scoreScope}" : " score=nao calculado")
                + (candidate.hasRally ? $" rally={candidate.rally}" : "")
                + (candidate.hasPreference ? $" priority={candidate.priority} distance={candidate.distance:0.###}" : "")
                + $"\n{candidate.gate}: {candidate.reason}";
            Color previous = GUI.backgroundColor;
            if (aiReviewPreviewCandidate == i) GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && HasPendingAIReview && !aiReviewApproved;
            if (GUILayout.Button(new GUIContent(text, "Somente previa visual; nao executa nem recalcula a alternativa."), rowStyle))
                SelectAIReviewCandidatePreview(i);
            GUI.enabled = wasEnabled;
            GUI.backgroundColor = previous;
        }
    }


    [System.Serializable]
    private sealed class AIReviewExport
    {
        public bool hasPending;
        public AIReviewRecord pending;
        public List<AIReviewRecord> history;
    }

    private readonly List<AIReviewRecord> aiReviewHistory = new List<AIReviewRecord>();
    private readonly System.Text.StringBuilder aiReviewLogs = new System.Text.StringBuilder();
    private AIReviewRecord pendingAIReview;
    private string aiReviewCurrentStance;
    private string aiReviewStanceSource;

    private void RememberAIReviewStance(AIWorldSnapshot snapshot, string source)
    {
        aiReviewCurrentStance = snapshot != null ? snapshot.Stance.ToString() : null;
        aiReviewStanceSource = source;
    }

    private string aiDecisionReasons = "";
    private string aiShoppingSummary = "";
    private string aiShoppingSavingSummary;
    private int aiReviewSequence;
    private bool aiReviewVisible = true;
    private bool aiReviewApproved;
    private bool aiReviewCapturing;
    private Vector2 aiReviewScroll;

    public void SetAIStepEnabled(bool enabled)
    {
        aiStep = enabled;
        if (enabled) aiReviewVisible = true;
        else aiReviewNoteFocused = false;
        // Desligar a ferramenta nao aprova uma decisao que ja esta pendente.
    }

    public bool TrySetAIStepMode(bool enabled)
    {
        if (enabled)
        {
            SetAIStepEnabled(true);
            return true;
        }
        // Same approval gates as the review button; never bypass a stale decision.
        if (HasPendingAIReview && !ApproveAIReview()) return false;
        SetAIStepEnabled(false);
        aiReviewVisible = false;
        SetDebugPaused(false);
        return true;
    }

    public bool AIReviewVisible => aiReviewVisible && (aiStep || HasPendingAIReview);
    public void ShowAIReview() => SetAIReviewVisible(true);

    public void SetAIReviewVisible(bool visible)
    {
        aiReviewVisible = visible;
        // Abrir deve reativar a revisao apos Continuar automaticamente, sem aprovar acao.
        if (visible) SetAIStepEnabled(true);
        else
        {
            aiReviewNoteFocused = false;
            SetDebugPaused(true);
        }
    }

    [System.Serializable]
    public sealed class AIReviewIndexEntry
    {
        public int id, turn, slot;
        public bool marked;
        public string status, decision, filePath, classification;
    }
    [System.Serializable]
    private sealed class AIReviewIndex
    {
        public string directory, marksPath;
        public List<AIReviewIndexEntry> steps = new List<AIReviewIndexEntry>();
    }
    public string GetAIReviewIndexJson()
    {
        var index = new AIReviewIndex { directory = AIReviewDirectory, marksPath = AIReviewMarksPath };
        foreach (var record in aiReviewHistory)
            index.steps.Add(new AIReviewIndexEntry { id = record.id, turn = record.turn, slot = record.slot,
                marked = record.marked || record.markedStrange, classification = record.markClassification, status = record.status, decision = record.decision, filePath = record.filePath });
        return JsonUtility.ToJson(index, true);
    }

    public string GetAIReviewJson()
    {
        return JsonUtility.ToJson(new AIReviewExport
        {
            hasPending = HasPendingAIReview,
            pending = pendingAIReview,
            history = aiReviewHistory
        }, true);
    }

    public bool ApproveAIReview()
    {
        if (!HasPendingAIReview || PlayerPauseHolds() || !CanAcceptDebugStep(out _))
            return false;
        if (matchController == null || matchController.CurrentTurn != pendingAIReview.turn
            || matchController.ActiveSlotId.Value != pendingAIReview.slot
            || ThreatRevisionTracker.GlobalBoardRevision != pendingAIReview.boardRevision
            || matchController.GetActualMoney(matchController.ActiveSlotId) != pendingAIReview.moneyBefore)
        {
            pendingAIReview.result = "Estado mudou durante a revisao. Recarregue o cenario antes de continuar.";
            return false;
        }
        aiReviewApproved = true;
        return true;
    }

    private void BeginAIReviewCapture()
    {
        EndAIReviewCapture();
        aiReviewLogs.Clear();
        aiReviewCandidates.Clear();
        aiReviewParticipantTraces.Clear();
        aiReviewCandidateCount = 0;
        aiReviewDecisionState = null;
        aiDecisionReasons = "";
        if (!aiStep) return;
        aiReviewCapturing = true;
        Application.logMessageReceived += CaptureAIReviewLog;
    }

    private void CaptureAIReviewLog(string message, string stack, LogType type)
    {
        if (aiReviewCapturing && message.StartsWith("[AI") && aiReviewLogs.Length < 24000)
            aiReviewLogs.AppendLine(message);
    }

    private void EndAIReviewCapture()
    {
        Application.logMessageReceived -= CaptureAIReviewLog;
        if (aiReviewCapturing) aiDecisionReasons = aiReviewLogs.ToString();
        aiReviewCapturing = false;
    }

    private float aiReviewDecisionCaptureMs;

    private void CaptureAIReviewDecisionState(string actorId)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        aiReviewDecisionState = CaptureAIReviewState(actorId);
        aiReviewDecisionCaptureMs = (float)clock.Elapsed.TotalMilliseconds;
    }

    private PlayerAction DecideUnitActionForReview(UnitManager unit, AIWorldSnapshot snapshot)
    {
        BeginAIReviewCapture();
        aiReviewActorId = unit.InstanceId.ToString();
        if (aiStep) CaptureAIReviewDecisionState(aiReviewActorId);
        try { return DecideUnitAction(unit, snapshot); }
        finally { EndAIReviewCapture(); }
    }

    private List<AIShoppingPlanner.ShoppingOrder> DecideShoppingForReview(AIWorldSnapshot snapshot)
    {
        RememberAIReviewStance(snapshot, "shopping_snapshot");
        aiShoppingSavingSummary = null;
        if (!aiStep) return AIShoppingPlanner.Decide(snapshot);
        BeginAIReviewCapture();
        aiReviewActorId = null;
        CaptureAIReviewDecisionState(null);
        try
        {
            var orders = AIShoppingPlanner.Decide(snapshot);
            aiShoppingSavingSummary = AIShoppingPlanner.LastSavingSummary;
            var text = new System.Text.StringBuilder("Plano de compras deste turno:\n");
            if (orders != null)
                foreach (var order in orders)
                    if (order.UnitToBuy != null && order.Building != null)
                        text.AppendLine($"{order.UnitToBuy.name} ${order.UnitToBuy.cost} em {FormatCell(order.Building.CurrentCellPosition)}");
            if (!string.IsNullOrEmpty(aiShoppingSavingSummary))
                text.AppendLine(aiShoppingSavingSummary);
            aiShoppingSummary = text.ToString();
            return orders;
        }
        finally { EndAIReviewCapture(); }
    }

    private string DescribeAIReviewMission(PlayerAction action)
    {
        var text = new System.Text.StringBuilder();
        TeamObjectivePlan plan = ObjectiveManager.GetPlanForSlot(matchController.ActiveSlotId);
        if (plan != null)
            foreach (var objective in plan.Objectives)
                foreach (var need in objective.Slots)
                    if (action.ActionType == PlayerActionType.Shopping
                        ? !need.Filled
                        : need.Filled && need.AssignedUnitId.ToString() == action.UnitInstanceId)
                        text.AppendLine($"{objective.Sector}: {objective.ObjectiveType}, {objective.Status}, prioridade {objective.Priority}, papel {need.Role}" + (need.Filled ? "" : " (vaga aberta)"));
        return text.Length > 0 ? text.ToString() : "Sem vaga de missao atribuida no plano atual.";
    }

    private IEnumerator WaitForAIReview(PlayerAction action)
    {
        if (!aiStep || matchController == null) yield break;
        aiReviewApproved = false;
        aiReviewNote = "";
        debugStepRequest = DebugStepRequest.None;
        aiReviewVisible = true;
        var commands = new System.Text.StringBuilder();
        commands.AppendLine($"{action.ActionType} / {action.SensorAction} | {action.DebugLabel}");
        if (action.MovementPath != null)
            foreach (var cell in action.MovementPath) commands.Append(FormatCell(cell)).Append(" ");
        commands.AppendLine();
        if (action.SubSteps != null)
            foreach (var step in action.SubSteps)
                if (step != null) commands.AppendLine($"{step.Label} | alvo={step.TargetInstanceId} predio={step.TargetConstructionId}" + (step.HasTargetHex ? " hex=" + FormatCell(step.TargetHex) : ""));
        pendingAIReview = new AIReviewRecord
        {
            id = ++aiReviewSequence, turn = matchController.CurrentTurn,
            slot = matchController.ActiveSlotId.Value, stage = currentAIStage,
            boardRevision = ThreatRevisionTracker.GlobalBoardRevision,
            moneyBefore = matchController.GetActualMoney(matchController.ActiveSlotId),
            status = "pendente", decision = BuildDebugStepDescription(action),
            mission = DescribeAIReviewMission(action), commands = commands.ToString(),
            shoppingSavingSummary = currentAIStage == 3 && action.ActionType == PlayerActionType.Shopping
                ? aiShoppingSavingSummary : null,
            reasons = (action.ActionType == PlayerActionType.Shopping ? aiShoppingSummary + "\n" : "")
                + (currentAIStage == 2 || currentAIStage == 3 ? aiDecisionReasons : "Etapa automatica do turno.")
        };
        InitializeAIReviewRecord(action);
        ResetAIReviewCandidatePreview();
        aiReviewVisualAction = action;
        aiReviewHistory.Add(pendingAIReview);
        if (aiReviewHistory.Count > 100) aiReviewHistory.RemoveAt(0);
        Debug.Log($"[AI Review] #{pendingAIReview.id} pendente: {pendingAIReview.decision}");
        // Nao chama sensores de preview nem executa entradas. Tabuleiro permanece confirmado.
        yield return new WaitUntil(() => aiReviewApproved || pendingAIReview == null || IsMatchEnded());
    }

    private void CompleteAIReview(bool succeeded)
    {
        if (pendingAIReview == null) return;
        pendingAIReview.status = succeeded ? "executada" : "falhou";
        pendingAIReview.moneyAfter = matchController != null
            ? matchController.GetActualMoney(PlayerSlotId.FromIndex(pendingAIReview.slot)) : 0;
        pendingAIReview.result = $"Batch terminou: sucesso={succeeded}; caixa {pendingAIReview.moneyBefore}->{pendingAIReview.moneyAfter}. Consulte JogadasLog para efeitos de combate/captura.";
        PersistAIReviewRecord(pendingAIReview);
        Debug.Log($"[AI Review] #{pendingAIReview.id} {pendingAIReview.result}");
        turnStateManager?.ClearAIReviewPairs();
        ResetAIReviewCandidatePreview();
        aiReviewVisualAction = aiReviewDrawnAction = null;
        pendingAIReview = null;
        aiReviewApproved = false;
    }

    private void InvalidateAIReview()
    {
        EndAIReviewCapture();
        if (pendingAIReview != null)
        {
            pendingAIReview.status = "interrompida";
            pendingAIReview.result = "Contexto encerrado ou restaurado; decisao nao pode ser reaprovada.";
            PersistAIReviewRecord(pendingAIReview);
        }
        turnStateManager?.ClearAIReviewPairs();
        ResetAIReviewCandidatePreview();
        aiReviewVisualAction = aiReviewDrawnAction = null;
        pendingAIReview = null;
        aiReviewApproved = false;
    }

    public struct AIReviewVisualPair
    {
        public Vector3Int From, To;
        public string Label;
        public Color Color;
    }

    private PlayerAction aiReviewVisualAction;
    private PlayerAction aiReviewDrawnAction;

    public static List<AIReviewVisualPair> BuildAIReviewVisualPairs(PlayerAction action)
    {
        var pairs = new List<AIReviewVisualPair>();
        if (action == null || action.ActionType != PlayerActionType.UnitAction) return pairs;
        Vector3Int origin;
        if (action.HasMoveFrom) origin = action.MoveFrom;
        else if (action.HasCursorHex) origin = action.CursorHex;
        else return pairs;
        origin.z = 0;
        Vector3Int destination = action.HasMoveTo ? action.MoveTo : origin;
        destination.z = 0;
        if (origin != destination)
            pairs.Add(new AIReviewVisualPair { From = origin, To = destination, Label = "Mover", Color = new Color(0.08f, 0.18f, 0.65f, 1f) });

        if (action.SensorAction == SensorActionType.None) return pairs;
        Color color = action.SensorAction == SensorActionType.Attack ? new Color(1f, 0.3f, 0.25f, 1f)
            : action.SensorAction == SensorActionType.Embark || action.SensorAction == SensorActionType.Disembark ? new Color(0.85f, 0.5f, 1f, 1f)
            : new Color(1f, 0.8f, 0.2f, 1f);
        var targets = new HashSet<Vector3Int>();
        void AddTarget(Vector3Int target, string label)
        {
            target.z = 0;
            if (targets.Add(target)) pairs.Add(new AIReviewVisualPair { From = destination, To = target, Label = label, Color = color });
        }
        // Hex explicito tem prioridade: desembarque aponta para o destino, nao para o passageiro embarcado.
        bool ResolveTarget(bool hasHex, Vector3Int hex, string unitId, string buildingId, out Vector3Int target)
        {
            target = hex;
            if (hasHex) return true;
            if (!string.IsNullOrWhiteSpace(unitId))
                foreach (var unit in UnitManager.AllActive)
                    if (unit != null && unit.InstanceId.ToString() == unitId)
                    {
                        var match = Instance != null ? Instance.Match : null;
                        if (match != null && unit.SlotIndex != match.ActiveSlotId.Value
                            && !match.IsUnitVisibleForSlot(unit, match.ActiveSlotId)) return false;
                        target = unit.CurrentCellPosition;
                        return true;
                    }
            if (!string.IsNullOrWhiteSpace(buildingId))
                foreach (var building in ConstructionManager.AllActive)
                    if (building != null && building.InstanceId.ToString() == buildingId)
                    { target = building.CurrentCellPosition; return true; }
            return false;
        }
        if (ResolveTarget(action.HasTargetHex, action.TargetHex, action.TargetInstanceId, action.TargetConstructionId, out var mainTarget))
            AddTarget(mainTarget, action.SensorAction.ToString());
        if (action.SubSteps != null)
            foreach (var step in action.SubSteps)
                if (step != null && ResolveTarget(step.HasTargetHex, step.TargetHex, step.TargetInstanceId, step.TargetConstructionId, out var target))
                    AddTarget(target, step.Label);
        if (targets.Count == 0 && (action.SensorAction == SensorActionType.Capture || action.SensorAction == SensorActionType.Land))
            AddTarget(destination, action.SensorAction.ToString());
        return pairs;
    }

    public static bool TryGetAIReviewActorCell(PlayerAction action, out Vector3Int cell)
    {
        cell = default;
        if (action == null || action.ActionType != PlayerActionType.UnitAction || string.IsNullOrEmpty(action.UnitInstanceId))
            return false;
        if (action.HasMoveFrom) cell = action.MoveFrom;
        else if (action.HasCursorHex) cell = action.CursorHex;
        else return false;
        cell.z = 0;
        return true;
    }

    public bool FocusAIReviewActor()
    {
        if (!HasPendingAIReview || !TryGetAIReviewActorCell(aiReviewVisualAction, out var cell)) return false;
        var map = boardTilemap != null ? boardTilemap : turnStateManager?.MovementTilemapRef;
        var camera = FindAnyObjectByType<CameraController>();
        if (map == null || camera == null) return false;
        // Camera presentation only. Never moves/selects the gameplay cursor or executes an action.
        camera.FocusOn(HexCoordinates.GetCellCenterWorld(map, cell), instant: false);
        return true;
    }


    private void LateUpdate()
    {
        bool visible = !rallyFormationVisible && HasPendingAIReview && !aiReviewApproved && IsAIReviewWindowVisible();
        if (!visible || aiReviewVisualAction == null)
        {
            if (aiReviewDrawnAction != null) turnStateManager?.ClearAIReviewPairs();
            aiReviewDrawnAction = null;
            return;
        }
        if ((aiReviewDrawnAction == aiReviewVisualAction && aiReviewDrawnCandidate == aiReviewPreviewCandidate) || turnStateManager == null) return;
        var pairs = aiReviewPreviewCandidate >= 0
            ? BuildAIReviewCandidateVisualPairs(pendingAIReview, aiReviewPreviewCandidate, out aiReviewPreviewMessage)
            : BuildAIReviewVisualPairs(aiReviewVisualAction);
        if (TryGetAIReviewActorCell(aiReviewVisualAction, out var actorCell))
        {
            pairs.Insert(0, new AIReviewVisualPair { From = actorCell, To = actorCell, Label = "AIReviewActor", Color = Color.white });
            pairs.AddRange(BuildAIReviewParticipantVisualPairs(pendingAIReview, actorCell));
        }
        turnStateManager.ShowAIReviewPairs(pairs);
        aiReviewDrawnAction = aiReviewVisualAction;
        aiReviewDrawnCandidate = aiReviewPreviewCandidate;
    }


    private Rect aiReviewWindowRect;
    private bool aiReviewWindowPositioned;
    private bool aiReviewResizing;
    private Vector2 aiReviewResizeStartMouse, aiReviewResizeStartSize;

    private bool IsAIReviewWindowVisible()
    {
        if (rallyFormationVisible) return true;
        return aiReviewVisible && (aiStep || HasPendingAIReview)
            && matchController != null && matchController.IsActiveTeamAI()
            && !PlayerPauseHolds();
    }

    private Rect GetAIReviewWindowRect()
    {
        float width = Mathf.Min(640f, Mathf.Max(1f, Screen.width - 24f));
        float height = Mathf.Min(620f, Mathf.Max(1f, Screen.height - 24f));
        if (!aiReviewWindowPositioned)
        {
            aiReviewWindowRect = new Rect(Screen.width - width - 12f, 12f, width, height);
            aiReviewWindowPositioned = true;
        }
        width = Mathf.Clamp(aiReviewWindowRect.width, Mathf.Min(rallyFormationVisible ? 560f : 420f, Screen.width), Mathf.Max(1f, Screen.width));
        height = Mathf.Clamp(aiReviewWindowRect.height, Mathf.Min(460f, Screen.height), Mathf.Max(1f, Screen.height));
        aiReviewWindowRect.width = width;
        aiReviewWindowRect.height = height;
        aiReviewWindowRect.x = Mathf.Clamp(aiReviewWindowRect.x, 0f, Mathf.Max(0f, Screen.width - width));
        aiReviewWindowRect.y = Mathf.Clamp(aiReviewWindowRect.y, 0f, Mathf.Max(0f, Screen.height - height));
        return aiReviewWindowRect;
    }

    // Coordenadas de tela (origem embaixo), tanto para Input legacy como Input System.
    // A camera consulta isto no Update: consumir apenas Event.Use no OnGUI nao bloqueia o input bruto.
    public bool IsPointerOverAIReview(Vector2 screenPosition)
    {
        return IsAIReviewWindowVisible() && (aiReviewResizing || GetAIReviewWindowRect().Contains(
            new Vector2(screenPosition.x, Screen.height - screenPosition.y)));
    }

    private bool DrawAIReview()
    {
        if (!IsAIReviewWindowVisible()) { aiReviewNoteFocused = false; return false; }
        Rect windowRect = GetAIReviewWindowRect();
        int resizeId = GUIUtility.GetControlID(0x41495253, FocusType.Passive);
        var resizeEvent = Event.current;
        var grip = new Rect(windowRect.xMax - 22f, windowRect.yMax - 22f, 22f, 22f);
        if (resizeEvent.type == EventType.MouseDown && resizeEvent.button == 0 && grip.Contains(resizeEvent.mousePosition))
        {
            GUIUtility.hotControl = resizeId;
            aiReviewResizing = true;
            aiReviewResizeStartMouse = resizeEvent.mousePosition;
            aiReviewResizeStartSize = windowRect.size;
            resizeEvent.Use();
        }
        if (aiReviewResizing && GUIUtility.hotControl == resizeId)
        {
            if (resizeEvent.type == EventType.MouseDrag)
            {
                Vector2 size = aiReviewResizeStartSize + resizeEvent.mousePosition - aiReviewResizeStartMouse;
                aiReviewWindowRect.size = new Vector2(
                    Mathf.Clamp(size.x, Mathf.Min(rallyFormationVisible ? 560f : 420f, Screen.width - windowRect.x), Screen.width - windowRect.x),
                    Mathf.Clamp(size.y, Mathf.Min(460f, Screen.height - windowRect.y), Screen.height - windowRect.y));
                windowRect = GetAIReviewWindowRect();
                resizeEvent.Use();
            }
            else if (resizeEvent.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                aiReviewResizing = false;
                resizeEvent.Use();
            }
        }
        if (Event.current.type == EventType.MouseDown && !windowRect.Contains(Event.current.mousePosition))
        {
            GUI.FocusControl(null);
            aiReviewNoteFocused = false;
        }
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.035f, 0.05f, 0.88f);
        GUI.DrawTexture(windowRect, Texture2D.whiteTexture);
        GUI.color = previousColor;
        aiReviewWindowRect = GUI.Window(0x41495354, windowRect,
            rallyFormationVisible ? DrawRallyFormationWindowContents : DrawAIReviewWindowContents,
            rallyFormationVisible ? "RALLY - formacao (somente leitura)" : "AI STEP - revisao antes da execucao (arraste pelo titulo)");
        GetAIReviewWindowRect(); // Mantem o titulo acessivel, inclusive apos redimensionar a Game view.
        if (Event.current.type == EventType.ScrollWheel && aiReviewWindowRect.Contains(Event.current.mousePosition))
            Event.current.Use();
        return true;
    }

    private void DrawAIReviewWindowContents(int windowId)
    {
        GUILayout.BeginArea(new Rect(8f, 28f, aiReviewWindowRect.width - 16f, aiReviewWindowRect.height - 52f));
        var record = pendingAIReview ?? (aiReviewHistory.Count > 0 ? aiReviewHistory[aiReviewHistory.Count - 1] : null);
        string stance = record != null ? record.current_stance : aiReviewCurrentStance;
        GUILayout.Label("current_stance: " + (string.IsNullOrEmpty(stance) ? "nao registrada neste passo" : stance),
            new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true });
        if (HasPendingAIReview && TryGetAIReviewActorCell(aiReviewVisualAction, out var actorCell))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Vai agir: " + ResolveUnitLabel(aiReviewVisualAction.UnitInstanceId) + " " + FormatCell(actorCell),
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true });
            if (GUILayout.Button("Focar unidade", GUILayout.Width(125f)))
            {
                GUI.FocusControl(null);
                aiReviewNoteFocused = false;
                FocusAIReviewActor();
            }
            GUILayout.EndHorizontal();
        }
        aiReviewScroll = GUILayout.BeginScrollView(aiReviewScroll);
        GUIStyle textStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
        if (record == null)
            GUILayout.Label("Aguardando a proxima decisao da IA.", textStyle);
        else
        {
            GUILayout.Label($"#{record.id} | T{record.turn} slot {record.slot} fase {record.stage} | {record.status} | caixa ${record.moneyBefore}", textStyle);
            var decisionBoxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(10, 10, 8, 8),
                margin = new RectOffset(0, 0, 6, 10)
            };
            GUILayout.BeginVertical(decisionBoxStyle);
            var decisionStyle = new GUIStyle(textStyle)
            {
                fontStyle = FontStyle.Bold,
                fontSize = (textStyle.fontSize > 0 ? textStyle.fontSize : 12) + 2
            };
            decisionStyle.normal.textColor = new Color(1f, 0.88f, 0.5f);
            GUILayout.Label("DECISAO", new GUIStyle(textStyle) { fontStyle = FontStyle.Bold });
            GUILayout.Label(record.decision, decisionStyle);
            if (record.stage == 3 && !string.IsNullOrWhiteSpace(record.shoppingSavingSummary))
            {
                GUILayout.Space(6f);
                var savingStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
                savingStyle.normal.textColor = new Color(0.65f, 0.9f, 1f);
                GUILayout.Label(record.shoppingSavingSummary, savingStyle);
            }
            GUILayout.EndVertical();
            GUILayout.Label("MISSAO\n" + record.mission, textStyle);
            GUILayout.Label("MOTIVOS (logs reais da IA)\n" + (string.IsNullOrWhiteSpace(record.reasons) ? "Nenhum motivo registrado. Ative Show AI Logs." : record.reasons), textStyle);
            DrawAIReviewParticipants(record, textStyle);
            DrawAIReviewAlternatives(record, textStyle);
            GUILayout.Label("COMANDOS PREVISTOS\n" + record.commands, textStyle);
            GUILayout.Label("Quadrado branco = unidade que vai agir. Linhas: azul escuro = movimento; vermelho = ataque; roxo = transporte; amarelo = outras interacoes. Anel = acao no proprio hex.", textStyle);
            if (!string.IsNullOrEmpty(record.result)) GUILayout.Label("RESULTADO\n" + record.result, textStyle);
        }
        if (record?.nearestSave != null)
            GUILayout.Label(record.nearestSave.found
                ? $"Save de referencia: slot {record.nearestSave.saveSlot}, T{record.nearestSave.turn}, {record.nearestSave.relation}; distancia no replay: {record.nearestSave.replayActionsToStep}."
                : "Sem save de referencia observado nesta partida.", textStyle);
        GUILayout.Label("Arquivo de marcas: " + AIReviewMarksPath, textStyle);
        if (!string.IsNullOrWhiteSpace(aiReviewFileMessage)) GUILayout.Label(aiReviewFileMessage, textStyle);
        GUILayout.EndScrollView();
        GUI.enabled = record != null;
        GUILayout.Label("Classificação da nota:");
        aiReviewMarkClassIndex = GUILayout.SelectionGrid(aiReviewMarkClassIndex, aiReviewMarkClassLabels,
            aiReviewWindowRect.width < 560f ? 3 : aiReviewMarkClassLabels.Length);
        GUILayout.Label("Nota opcional:");
        var noteStyle = new GUIStyle(GUI.skin.textArea) { wordWrap = true };
        float noteWidth = Mathf.Max(40f, aiReviewWindowRect.width - 24f);
        float noteMaxHeight = Mathf.Max(56f, Mathf.Min(150f, aiReviewWindowRect.height * 0.25f));
        float noteHeight = Mathf.Clamp(noteStyle.CalcHeight(new GUIContent(aiReviewNote + "\n"), noteWidth), 56f, noteMaxHeight);
        GUI.SetNextControlName("AIReviewNote");
        aiReviewNote = GUILayout.TextArea(aiReviewNote, 2048, noteStyle, GUILayout.Height(noteHeight), GUILayout.ExpandWidth(true));
        aiReviewNoteFocused = GUI.GetNameOfFocusedControl() == "AIReviewNote";
        if (aiReviewNoteFocused && Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Escape)
        {
            GUI.FocusControl(null);
            aiReviewNoteFocused = false;
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            Event.current.Use();
        }
        if (GUILayout.Button(record != null && (record.marked || record.markedStrange) ? "Adicionar outra marca" : "Salvar marca"))
            MarkAIReview(aiReviewNote, aiReviewMarkClassCodes[aiReviewMarkClassIndex]);
        GUI.enabled = true;
        bool compactButtons = aiReviewWindowRect.width < 560f;
        if (compactButtons) GUILayout.BeginVertical(); else GUILayout.BeginHorizontal();
        GUI.enabled = HasPendingAIReview && !aiReviewApproved;
        if (GUILayout.Button("Executar decisao")) ApproveAIReview();
        if (GUILayout.Button("Continuar automaticamente"))
        {
            if (TrySetAIStepMode(false)) GUI.FocusControl(null);
        }
        GUI.enabled = true;
        if (GUILayout.Button("Fechar e manter pausada"))
        {
            SetAIReviewVisible(false);
        }
        if (compactButtons) GUILayout.EndVertical(); else GUILayout.EndHorizontal();
        GUILayout.EndArea();
        GUI.Label(new Rect(aiReviewWindowRect.width - 22f, aiReviewWindowRect.height - 22f, 22f, 22f),
            new GUIContent("◢", "Arraste para redimensionar"));
        GUI.DragWindow(new Rect(0f, 0f, aiReviewWindowRect.width, 26f));
    }


    /// <summary>
    /// Pausa ou retoma o loop da IA sem cancelar o batch em andamento.
    /// Enquanto pausada, a IA aguarda um comando de "AI RESUME" para retomar 
    /// ou "AI STEP" para executar o pr�ximo batch preparado.
    /// </summary>
    public void SetDebugPaused(bool paused)
    {
        isDebugPaused = paused;
        IsDebugPaused = paused;

        if (!paused)
        {
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
            PanelDialogController.TrySetTransientText("AI RESUME", 1.8f);
        }

        if (showAILogs)
            Debug.Log(paused
                ? "[AI] Pausa de debug solicitada. Aguardando ponto seguro."
                : "[AI] Pausa de debug encerrada. Retomando IA.");

        if (paused)
            PanelDialogController.TrySetExternalText("AI PAUSE\nAguardando AI RESUME ou AI STEP");
    }

    /// <summary>
    /// Pause de JOGADOR: segura o loop da IA enquanto o menu in-game está aberto durante o turno
    /// da IA. Limpo — sem AI STEP/RESUME nem texto externo (isso é do pause de DEV/F10). Retomado
    /// automaticamente quando o menu fecha. Não cancela o batch em andamento (espera ponto seguro).
    /// </summary>
    [System.NonSerialized] private BattleMapMenuRootController playerMenuForPause;

    /// <summary>
    /// A pausa de jogador e DERIVADA do menu, nao uma flag que alguem tem de lembrar
    /// de desligar. Ela segura a IA enquanto o jogador SEGURA o menu: aberto,
    /// pedido (abre no proximo Neutral), ou nas telas de Salvar/Carregar que saem
    /// dele. Saiu por qualquer caminho, solta sozinha.
    ///
    /// Antes so soltava se o fechamento passasse por TryExitPlayerMenuStateToNeutral
    /// com o cursor exatamente em PlayerMenu. Outro caminho (submenu de Opcoes,
    /// Salvar/Carregar, menu que nem chegou a abrir) deixava a IA parada para sempre.
    /// </summary>
    private bool PlayerPauseHolds()
    {
        if (!isPlayerPaused)
            return false;

        if (playerMenuForPause == null)
            playerMenuForPause = FindAnyObjectByType<BattleMapMenuRootController>(FindObjectsInactive.Include);
        bool menuHolds = playerMenuForPause != null &&
                         (playerMenuForPause.IsMenuOpen || playerMenuForPause.IsOpenPending);

        bool stateHolds = turnStateManager != null && turnStateManager.IsInPlayerMenuScope;

        if (menuHolds || stateHolds)
            return true;

        SetPlayerPaused(false);
        return false;
    }

    public void SetPlayerPaused(bool paused)
    {
        if (isPlayerPaused == paused)
            return;
        isPlayerPaused = paused;
        if (showAILogs)
            Debug.Log(paused
                ? "[AI] Pause de jogador (menu in-game aberto). Aguardando fechar o menu."
                : "[AI] Pause de jogador encerrado (menu fechado). Retomando IA.");
    }

    public void RequestDebugStep()
    {
        if (HasPendingAIReview)
        {
            ApproveAIReview();
            return;
        }
        if (!isDebugPaused)
        {
            Debug.Log("[AI Step] Ignorado: acione AI Pause antes de usar AI Step.");
            PanelDialogController.TrySetTransientText("AI STEP ignorado: use AI PAUSE primeiro", 2.0f);
            return;
        }

        if (!CanAcceptDebugStep(out string blockedReason))
        {
            Debug.Log($"[AI Step] Ignorado: batch anterior ainda ativo ({blockedReason}).");
            PanelDialogController.TrySetTransientText(
                "AI STEP: aguarde o batch atual terminar", 1.6f);
            return;
        }

        debugStepRequest = debugStepPendingAction != null
            ? DebugStepRequest.Execute
            : DebugStepRequest.Prepare;

        Debug.Log(debugStepRequest == DebugStepRequest.Prepare
            ? "[AI Step] Preparando proximo batch."
            : "[AI Step] Executando batch preparado.");

        PanelDialogController.TrySetTransientText(
            debugStepRequest == DebugStepRequest.Prepare
                ? "AI STEP: preparando batch"
                : "AI STEP: executando batch",
            1.6f);
    }

    public bool CanAcceptDebugStep(out string reason)
    {
        if (aiTurnBatchExecuting)
        {
            reason = "aiTurnBatchExecuting";
            return false;
        }

        if (replayManager != null && replayManager.IsStepExecutionBusy)
        {
            reason = $"replay={replayManager.GetReplayStepExecutionBusyReason()}";
            return false;
        }

        if (turnStateManager != null
            && turnStateManager.IsScannerActionExecutionInProgress)
        {
            reason = "scannerActionExecution";
            return false;
        }

        if (matchController != null
            && matchController.IsTurnTransitionInProgress)
        {
            reason = "turnTransition";
            return false;
        }

        if (turnStateManager != null
            && turnStateManager.CurrentCursorState
                != TurnStateManager.CursorState.Neutral)
        {
            reason = $"cursor={turnStateManager.CurrentCursorState}";
            return false;
        }

        if (debugStepRequest != DebugStepRequest.None)
        {
            reason = $"request={debugStepRequest}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Expõe somente para diagnóstico a unidade do batch preparado pelo F11.
    /// Não seleciona, executa ou altera a ação pendente.
    /// </summary>
    public bool TryGetDebugStepPendingUnit(out UnitManager unit)
    {
        unit = null;
        string instanceId = debugStepPendingAction?.UnitInstanceId;
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager candidate = UnitManager.AllActive[i];
            if (candidate != null
                && candidate.InstanceId.ToString() == instanceId)
            {
                unit = candidate;
                return true;
            }
        }

        return false;
    }

    public void SetDebugShoppingPaused(bool paused)
    {
        isDebugShoppingPaused = paused;
        IsDebugShoppingPaused = paused;

        Debug.Log(paused
            ? "[AI Shopping] Fase 3 pausada. A IA vai parar antes das compras."
            : "[AI Shopping] Fase 3 liberada. A IA pode entrar nas compras.");

        if (paused)
            PanelDialogController.TrySetTransientText("AI SHOPPING PAUSE", 2.0f);
        else
            PanelDialogController.TrySetTransientText("AI SHOPPING RESUME", 1.8f);
    }

    public bool TryStartDebugStage(int stage, bool resetPlan = false)
    {
        if (stage < 1 || stage > 3)
        {
            Debug.Log($"[AI Stage] Stage invalido: {stage}. Use 1, 2 ou 3.");
            PanelDialogController.TrySetTransientText("AI STAGE invalido: use 1, 2 ou 3", 2.2f);
            return false;
        }

        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (matchController == null)
        {
            Debug.Log("[AI Stage] MatchController nao encontrado.");
            PanelDialogController.TrySetTransientText("AI STAGE: MatchController nao encontrado", 2.2f);
            return false;
        }

        TeamId aiTeam = matchController.ActiveTeam;
        if (!matchController.IsPlayerAI(matchController.ActiveSlotId))
        {
            Debug.Log($"[AI Stage] Time ativo {aiTeam} nao e IA.");
            PanelDialogController.TrySetTransientText($"AI STAGE: time ativo {aiTeam} nao e IA", 2.2f);
            return false;
        }

        if (aiCoroutine != null)
            StopCoroutine(aiCoroutine);

        InvalidateAIReview();
        isActive = true;
        debugStepRequest = DebugStepRequest.None;
        ClearDebugStepPreview();
        aiCoroutine = StartCoroutine(RunAIDebugStage(aiTeam, stage, resetPlan));
        string resetTag = resetPlan ? " (RESET PLANO)" : "";
        Debug.Log($"[AI Stage] Reiniciando IA no stage {stage} para {aiTeam}{resetTag}.");
        PanelDialogController.TrySetTransientText($"AI STAGE {stage}{resetTag}", 2.0f);
        return true;
    }

    // -------------------------------------------------------------------------
    // Fase 1: Serviço do Comando
    // -------------------------------------------------------------------------

    private IEnumerator WaitIfDebugPaused()
    {
        if (ShouldStopAIForMatchEnd("debug_pause_start"))
            yield break;
        if (aiStep && !PlayerPauseHolds()) yield break;
        if (!isDebugPaused && !isPlayerPaused) yield break;

        if (showAILogs)
            Debug.Log(isPlayerPaused
                ? "[AI] Pausada (menu do jogador aberto) - aguardando fechar o menu."
                : "[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.");
        // Pause de jogador é hold ABSOLUTO (sem AI STEP): só retoma quando o menu fecha. Pause de
        // debug mantém a semântica de STEP. Resume = NÃO player-paused E (NÃO debug-paused OU step).
        yield return new WaitUntil(() =>
            !PlayerPauseHolds() && (!isDebugPaused || aiStep || debugStepRequest != DebugStepRequest.None));
        if (ShouldStopAIForMatchEnd("debug_pause_end"))
            yield break;
        if (showAILogs && !isDebugPaused && !isPlayerPaused)
            Debug.Log("[AI] Retomando execucao da IA.");
    }

    private IEnumerator WaitIfDebugShoppingPaused()
    {
        if (ShouldStopAIForMatchEnd("debug_shopping_pause_start"))
            yield break;
        if (!isDebugShoppingPaused) yield break;

        Debug.Log($"{TL("Shopping")} Pausado antes da Fase 3 - aguardando 'AI SHOPPING RESUME'.");
        PanelDialogController.TrySetExternalText("AI Shopping pausado\nAI SHOPPING RESUME para liberar compras");
        yield return new WaitUntil(() => !isDebugShoppingPaused);
        if (ShouldStopAIForMatchEnd("debug_shopping_pause_end"))
            yield break;
        PanelDialogController.ClearExternalText();
        Debug.Log($"{TL("Shopping")} Resume recebido - entrando na Fase 3.");
    }

    private float GetBatchDelay()
    {
        if (replayManager != null)
            return Mathf.Max(0f, replayManager.GetEffectiveTimeBetweenBatchesForAutoplay());
        return 0.5f;
    }

    private float GetAdaptivePhase2BatchDelay(int armySize)
    {
        float configuredDelay = GetBatchDelay();
        if (configuredDelay <= 0f || armySize <= 12)
            return configuredDelay;

        // Preserva o ritmo configurado em exercitos pequenos. Acima de 12,
        // mantem aproximadamente seis segundos de respiro total por fase,
        // com um piso curto para a troca de unidade continuar legivel.
        float scaledDelay =
            configuredDelay * 12f / Mathf.Max(1, armySize);
        return Mathf.Min(
            configuredDelay,
            Mathf.Max(0.08f, scaledDelay));
    }

    private bool lastAIBatchSucceeded;

    private IEnumerator ExecuteAIBatchWithDebugStep(PlayerAction action)
    {
        lastAIBatchSucceeded = false;
        if (ShouldStopAIForMatchEnd("batch_start"))
            yield break;
        if (action == null)
            yield break;

        // Pause de jogador: não inicia um batch enquanto o menu in-game do jogador estiver aberto.
        if (PlayerPauseHolds())
        {
            yield return new WaitUntil(() => !PlayerPauseHolds());
            if (ShouldStopAIForMatchEnd("batch_player_pause"))
                yield break;
        }

        bool reviewed = aiStep;
        if (reviewed)
        {
            yield return WaitForAIReview(action);
            if (!aiReviewApproved || pendingAIReview == null) yield break;
            if (ShouldStopAIForMatchEnd("ai_review")) yield break;
            if (PlayerPauseHolds()) yield return new WaitUntil(() => !PlayerPauseHolds());
        }

        if (isDebugPaused && !reviewed)
        {
            debugStepPendingAction = action;
            debugStepRequest = DebugStepRequest.None;
            ShowDebugStepPreview(action);

            yield return new WaitUntil(() => !isDebugPaused || debugStepRequest == DebugStepRequest.Execute);
            if (ShouldStopAIForMatchEnd("batch_debug_wait"))
                yield break;
            debugStepRequest = DebugStepRequest.None;
            ClearDebugStepPreview();
        }

        if (replayManager == null)
            yield break;

        if (reviewed && pendingAIReview != null) pendingAIReview.status = "executando";
        aiTurnBatchExecuting = true;
        replayManager.ExecuteLiveAIBatch(action, iaRapida);
        yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
        lastAIBatchSucceeded =
            replayManager.LastLiveAIBatchSucceeded;
        aiTurnBatchExecuting = false;
        if (reviewed) CompleteAIReview(lastAIBatchSucceeded);
        if (ShouldStopAIForMatchEnd("batch_end"))
            yield break;
        debugStepPendingAction = null;
    }

    public IEnumerator ExecuteTutorialMoveBatch(UnitManager unit, TeamId team, Vector3Int from, Vector3Int to)
    {
        if (unit == null || replayManager == null)
            yield break;

        from.z = 0;
        to.z = 0;
        PlayerAction batch = BuildMoveBatch(unit, team, from, to);
        yield return ExecuteAIBatchWithDebugStep(batch);
    }

    private void ShowDebugStepPreview(PlayerAction action)
    {
        string description = BuildDebugStepDescription(action);
        string previewMessage = string.Empty;
        bool previewShown = turnStateManager != null &&
            turnStateManager.TryShowAIDebugStepPreview(action, out previewMessage);

        Debug.Log($"[AI DecisionPreview] {description}");
        if (!string.IsNullOrWhiteSpace(previewMessage))
            Debug.Log($"[AI Step] {previewMessage}");

        PanelDialogController.TrySetExternalText(
            $"AI Step\n{description}\nF11: executar | F12: resume");

        if (!previewShown)
            Debug.Log("[AI Step] Preview visual indisponivel para este batch.");
    }

    private void ClearDebugStepPreview()
    {
        debugStepPendingAction = null;
        turnStateManager?.ClearAIDebugStepPreview();
        PanelDialogController.ClearExternalText();
    }

    private string BuildDebugStepDescription(PlayerAction action)
    {
        if (action == null)
            return "batch vazio.";

        string unitLabel = ResolveUnitLabel(action.UnitInstanceId);
        string from = action.HasMoveFrom ? FormatCell(action.MoveFrom) : "origem indefinida";
        string to = action.HasMoveTo ? FormatCell(action.MoveTo) : "destino indefinido";

        switch (action.ActionType)
        {
            case PlayerActionType.CommandService:
                return "servico do comando automatico sera executado.";
            case PlayerActionType.Shopping:
                return $"compra {action.ShoppingUnitTypeId} no hex {FormatCell(action.TargetHex)}.";
            case PlayerActionType.EndTurn:
                return "a IA vai passar a vez.";
            case PlayerActionType.UnitAction:
                switch (action.SensorAction)
                {
                    case SensorActionType.Attack:
                        return $"{unitLabel} vai atacar {ResolveUnitLabel(action.TargetInstanceId)} no hex {FormatCell(action.TargetHex)}, movendo de {from} para {to}.";
                    case SensorActionType.Embark:
                        return $"{unitLabel} vai embarcar em {ResolveUnitLabel(action.TargetInstanceId)}, movendo de {from} para {to}.";
                    case SensorActionType.Disembark:
                        return $"{unitLabel} vai desembarcar {DescribeSubSteps(action)} após mover de {from} para {to}.";
                    case SensorActionType.Capture:
                        return $"{unitLabel} vai capturar no hex {ResolveActionTargetHex(action, to)}, movendo de {from} para {to}.";
                    case SensorActionType.Merge:
                        return $"{unitLabel} vai fundir com {ResolveUnitLabel(action.TargetInstanceId)}, movendo de {from} para {to}.";
                    case SensorActionType.Supply:
                        return $"{unitLabel} vai suprir {DescribeActionTargets(action)}, movendo de {from} para {to}.";
                    case SensorActionType.Transfer:
                        return $"{unitLabel} vai transferir recursos para {DescribeActionTargets(action)}, movendo de {from} para {to}.";
                    case SensorActionType.Land:
                        return $"{unitLabel} vai pousar no hex {ResolveActionTargetHex(action, to)}, movendo de {from} para {to}.";
                    default:
                        return $"{unitLabel} vai apenas mover de {from} para {to}." +
                            (!string.IsNullOrWhiteSpace(action.DebugLabel)
                                ? $" Decisão: {action.DebugLabel}."
                                : "");
                }
            default:
                return !string.IsNullOrWhiteSpace(action.DebugLabel) ? action.DebugLabel : action.ActionType.ToString();
        }
    }

    private static string ResolveActionTargetHex(
        PlayerAction action,
        string fallback)
    {
        return action != null && action.HasTargetHex
            ? FormatCell(action.TargetHex)
            : fallback;
    }

    private static string DescribeActionTargets(PlayerAction action)
    {
        if (action == null)
            return "o alvo planejado";
        if (!string.IsNullOrWhiteSpace(action.TargetInstanceId))
            return ResolveUnitLabel(action.TargetInstanceId);
        if (action.SubSteps != null && action.SubSteps.Count > 0)
            return DescribeSubSteps(action);
        if (action.HasTargetHex)
            return $"o hex {FormatCell(action.TargetHex)}";
        return "o alvo planejado";
    }

    private static string DescribeSubSteps(PlayerAction action)
    {
        if (action?.SubSteps == null || action.SubSteps.Count == 0)
            return "a carga planejada";

        List<string> targets = new List<string>();
        for (int i = 0; i < action.SubSteps.Count; i++)
        {
            PlayerActionSubStep step = action.SubSteps[i];
            if (step == null)
                continue;

            string target = !string.IsNullOrWhiteSpace(step.TargetInstanceId)
                ? ResolveUnitLabel(step.TargetInstanceId)
                : !string.IsNullOrWhiteSpace(step.Label)
                    ? step.Label
                    : "subação";
            if (step.HasTargetHex)
                target += $" em {FormatCell(step.TargetHex)}";
            targets.Add(target);
        }

        return targets.Count > 0
            ? string.Join(", ", targets)
            : $"{action.SubSteps.Count} subação(ões)";
    }

    private static string FormatCell(Vector3Int cell)
    {
        return $"({cell.x},{cell.y})";
    }

    private static string ResolveUnitLabel(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return "a unidade";

        foreach (UnitManager unit in UnitManager.AllActive)
        {
            if (unit == null)
                continue;
            if (unit.InstanceId.ToString() == instanceId)
                return $"{unit.UnitDisplayName} #{unit.InstanceId}";
        }

        return $"unidade #{instanceId}";
    }
}
