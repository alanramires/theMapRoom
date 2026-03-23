using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlanningManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private Tilemap terrainTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Planning Data")]
    [SerializeField] private PlanningConfig planningConfig = new PlanningConfig();
    [SerializeField] private List<RallyPoint> rallyPoints = new List<RallyPoint>();
    [SerializeField] private List<RallyAssignment> rallyAssignments = new List<RallyAssignment>();
    [SerializeField] private bool planningModeActive;
    [SerializeField] private int selectedRallyPointId = -1;
    [SerializeField] private int nextRallyPointId = 1;

    [Header("Visuals")]
    [SerializeField] private Sprite rallyFlagSprite;
    [SerializeField] private Color rallyFlagColor = new Color(1f, 0.95f, 0.2f, 0.95f);
    [SerializeField] private Vector3 rallyFlagOffset = new Vector3(0f, 0.28f, 0f);
    [SerializeField] private Color pulseA = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] private Color pulseB = new Color(1f, 0.55f, 0.15f, 1f);
    [SerializeField] private float pulseSpeed = 3.4f;

    private readonly Dictionary<int, GameObject> rallyFlags = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, Color> unitOriginalColor = new Dictionary<int, Color>();
    private readonly HashSet<int> combatTouchedUnits = new HashSet<int>();

    private bool turnStartRallyExecutionInProgress;
    private bool suppressMapClickForCurrentFrame;
    private Vector3Int pendingDestination;
    private bool hasPendingDestination;
    private string pendingRallyName = string.Empty;

    public event Action PlanningDataChanged;
    public event Action<bool> PlanningModeChanged;

    public bool IsPlanningModeActive => planningModeActive;
    public bool IsTurnStartRallyExecutionInProgress => turnStartRallyExecutionInProgress;
    public int SelectedRallyPointId => selectedRallyPointId;
    public bool HasPendingDestination => hasPendingDestination;
    public Vector3Int PendingDestination => pendingDestination;
    public string PendingRallyName => pendingRallyName;
    public PlanningConfig Config => planningConfig;
    public IReadOnlyList<RallyPoint> RallyPoints => rallyPoints;
    public IReadOnlyList<RallyAssignment> RallyAssignments => rallyAssignments;

    private void Awake()
    {
        TryAutoAssignReferences();
        RecalculateNextRallyPointId();
    }

    private void OnDisable()
    {
        RestoreAssignedUnitColors();
        SetAllFlagsVisible(false);
    }

    private void Update()
    {
        TryAutoAssignReferences();
        CleanupInvisibleAssignments();

        if (!planningModeActive)
            return;

        if (suppressMapClickForCurrentFrame)
            suppressMapClickForCurrentFrame = false;
        else
            ProcessPlanningMapClick();

        UpdateAssignedUnitsPulse();
        RefreshFlagsForActiveTeam();
    }

    public bool HasActiveAssignmentsForTeam(TeamId team)
    {
        CleanupInvisibleAssignments();
        int owner = (int)team;
        for (int i = 0; i < rallyAssignments.Count; i++)
        {
            RallyAssignment a = rallyAssignments[i];
            RallyPoint p = FindRallyPointById(a.rallyPointId);
            if (p == null || !p.ativo || p.teamOwner != owner)
                continue;
            return true;
        }

        return false;
    }

    public void SetPendingRallyName(string rallyName)
    {
        pendingRallyName = string.IsNullOrWhiteSpace(rallyName) ? string.Empty : rallyName.Trim();
        PlanningDataChanged?.Invoke();
    }

    public void SetPendingDestination(Vector3Int cell)
    {
        cell.z = 0;
        pendingDestination = cell;
        hasPendingDestination = true;
        PlanningDataChanged?.Invoke();
    }

    public bool TryEnterPlanningMode(out string reason)
    {
        reason = string.Empty;
        TryAutoAssignReferences();

        if (planningModeActive)
            return true;
        if (turnStartRallyExecutionInProgress)
        {
            reason = "Planning indisponivel durante fila automatica de rally.";
            return false;
        }
        if (turnStateManager == null || turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            reason = "Planning exige cursor em Neutral.";
            return false;
        }
        if (turnStateManager != null && turnStateManager.IsScannerActionExecutionInProgress)
        {
            reason = "Planning bloqueado: acao em execucao.";
            return false;
        }
        if (replayManager != null && replayManager.IsReplaying)
        {
            reason = "Planning indisponivel durante replay.";
            return false;
        }

        planningModeActive = true;
        hasPendingDestination = false;
        pendingRallyName = string.Empty;
        suppressMapClickForCurrentFrame = true;
        if (selectedRallyPointId <= 0)
        {
            RallyPoint first = GetFirstRallyPointForActiveTeam();
            selectedRallyPointId = first != null ? first.id : -1;
        }

        PlanningModeChanged?.Invoke(true);
        PlanningDataChanged?.Invoke();
        return true;
    }

    public void ExitPlanningMode()
    {
        if (!planningModeActive)
            return;

        planningModeActive = false;
        hasPendingDestination = false;
        pendingRallyName = string.Empty;
        suppressMapClickForCurrentFrame = false;
        RestoreAssignedUnitColors();
        SetAllFlagsVisible(false);
        PlanningModeChanged?.Invoke(false);
        PlanningDataChanged?.Invoke();
    }

    public bool TryCreateRallyPoint(out string message)
    {
        message = string.Empty;
        if (!planningModeActive)
        {
            message = "Planning inativo.";
            return false;
        }
        if (!hasPendingDestination)
        {
            message = "Defina um hex destino antes de criar o rally.";
            return false;
        }

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        if (activeTeam == TeamId.Neutral)
        {
            message = "Rally Point nao pode ser criado para team neutral.";
            return false;
        }

        int owner = (int)activeTeam;
        int maxPerTeam = Mathf.Max(1, planningConfig != null ? planningConfig.maxRallyPointsPerTeam : 5);
        if (CountRallyPointsForTeam(owner) >= maxPerTeam)
        {
            message = $"Limite atingido: maximo de {maxPerTeam} rally points por time.";
            return false;
        }

        RallyPoint point = new RallyPoint
        {
            id = nextRallyPointId++,
            nome = string.IsNullOrWhiteSpace(pendingRallyName) ? $"Rally {nextRallyPointId - 1}" : pendingRallyName.Trim(),
            hexDestino = new Vector2Int(pendingDestination.x, pendingDestination.y),
            teamOwner = owner,
            ativo = false
        };

        rallyPoints.Add(point);
        selectedRallyPointId = point.id;
        hasPendingDestination = false;
        pendingRallyName = string.Empty;
        message = $"Rally point '{point.nome}' criado em ({point.hexDestino.x},{point.hexDestino.y}).";
        PlanningDataChanged?.Invoke();
        return true;
    }

    public bool TrySelectRallyPoint(int rallyPointId)
    {
        RallyPoint point = FindRallyPointById(rallyPointId);
        if (point == null)
            return false;

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        if (point.teamOwner != (int)activeTeam)
            return false;

        selectedRallyPointId = rallyPointId;
        PlanningDataChanged?.Invoke();
        return true;
    }

    public bool TryStartSelectedRallyPoint(out string message)
    {
        message = string.Empty;
        RallyPoint point = FindRallyPointById(selectedRallyPointId);
        if (point == null)
        {
            message = "Selecione um rally point valido.";
            return false;
        }

        point.ativo = true;
        message = $"Rally '{point.nome}' ativado. Execucao inicia no proximo turno.";
        PlanningDataChanged?.Invoke();
        return true;
    }

    public bool TryRemoveSelectedRallyPoint(out string message)
    {
        message = string.Empty;
        RallyPoint point = FindRallyPointById(selectedRallyPointId);
        if (point == null)
        {
            message = "Selecione um rally point valido.";
            return false;
        }

        int removedAssignments = RemoveAssignmentsByRallyPoint(point.id);
        rallyPoints.Remove(point);
        rallyFlags.Remove(point.id, out _);

        RallyPoint fallback = GetFirstRallyPointForActiveTeam();
        selectedRallyPointId = fallback != null ? fallback.id : -1;

        message = $"Rally '{point.nome}' removido ({removedAssignments} assignments removidos).";
        PlanningDataChanged?.Invoke();
        return true;
    }

    public bool TryToggleAssignmentAtCell(Vector3Int cell, out string message)
    {
        message = string.Empty;
        cell.z = 0;

        RallyPoint selected = FindRallyPointById(selectedRallyPointId);
        if (selected == null)
        {
            message = "Selecione um rally point antes de atribuir unidades.";
            return false;
        }

        if (terrainTilemap == null)
        {
            message = "Tilemap de terreno nao encontrada.";
            return false;
        }

        UnitManager unit = UnitOccupancyRules.GetUnitAtCell(terrainTilemap, cell);
        if (!IsUnitEligibleForAssignment(unit, selected.teamOwner))
        {
            message = "Hex clicado nao contem unidade elegivel para assignment.";
            return false;
        }

        int unitId = unit.InstanceId;
        for (int i = rallyAssignments.Count - 1; i >= 0; i--)
        {
            RallyAssignment existing = rallyAssignments[i];
            if (existing == null || existing.unitId != unitId)
                continue;

            if (existing.rallyPointId == selected.id)
            {
                rallyAssignments.RemoveAt(i);
                message = $"Unidade {unitId} removida do rally '{selected.nome}'.";
                PlanningDataChanged?.Invoke();
                return true;
            }

            rallyAssignments.RemoveAt(i);
        }

        rallyAssignments.Add(new RallyAssignment
        {
            rallyPointId = selected.id,
            unitId = unitId
        });

        message = $"Unidade {unitId} atribuida ao rally '{selected.nome}'.";
        PlanningDataChanged?.Invoke();
        return true;
    }

    public IReadOnlyList<RallyPoint> GetRallyPointsForTeam(TeamId team)
    {
        int owner = (int)team;
        List<RallyPoint> result = new List<RallyPoint>();
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint point = rallyPoints[i];
            if (point == null || point.teamOwner != owner)
                continue;
            result.Add(point);
        }

        result.Sort((a, b) => a.id.CompareTo(b.id));
        return result;
    }

    public IReadOnlyList<RallyAssignment> GetAssignmentsForRally(int rallyPointId)
    {
        List<RallyAssignment> result = new List<RallyAssignment>();
        for (int i = 0; i < rallyAssignments.Count; i++)
        {
            RallyAssignment assignment = rallyAssignments[i];
            if (assignment == null || assignment.rallyPointId != rallyPointId)
                continue;
            result.Add(assignment);
        }

        result.Sort((a, b) => a.unitId.CompareTo(b.unitId));
        return result;
    }

    public void NotifyUnitInvolvedInCombat(UnitManager unit)
    {
        if (unit == null)
            return;

        combatTouchedUnits.Add(unit.InstanceId);
        RemoveAssignmentsForUnit(unit.InstanceId, "combate/dano");
    }

    public void NotifyUnitVisibilityPossiblyChanged(UnitManager unit)
    {
        if (unit == null)
            return;

        if (IsUnitFieldVisible(unit))
            return;

        RemoveAssignmentsForUnit(unit.InstanceId, "unidade fora de campo");
    }

    public void ExportPlanningData(out PlanningConfigSaveData config, out List<RallyPointSaveData> points, out List<RallyAssignmentSaveData> assignments)
    {
        CleanupInvisibleAssignments();

        config = new PlanningConfigSaveData
        {
            maxRallyPointsPerTeam = Mathf.Max(1, planningConfig != null ? planningConfig.maxRallyPointsPerTeam : 5)
        };

        points = new List<RallyPointSaveData>(rallyPoints.Count);
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint point = rallyPoints[i];
            if (point == null)
                continue;

            points.Add(new RallyPointSaveData
            {
                id = point.id,
                nome = point.nome,
                hexX = point.hexDestino.x,
                hexY = point.hexDestino.y,
                teamOwner = point.teamOwner,
                ativo = point.ativo
            });
        }

        assignments = new List<RallyAssignmentSaveData>(rallyAssignments.Count);
        for (int i = 0; i < rallyAssignments.Count; i++)
        {
            RallyAssignment assignment = rallyAssignments[i];
            if (assignment == null)
                continue;

            assignments.Add(new RallyAssignmentSaveData
            {
                rallyPointId = assignment.rallyPointId,
                unitId = assignment.unitId
            });
        }
    }

    public void ImportPlanningData(PlanningConfigSaveData config, List<RallyPointSaveData> points, List<RallyAssignmentSaveData> assignments)
    {
        planningConfig ??= new PlanningConfig();
        planningConfig.maxRallyPointsPerTeam = Mathf.Max(1, config != null ? config.maxRallyPointsPerTeam : 5);

        rallyPoints.Clear();
        rallyAssignments.Clear();
        selectedRallyPointId = -1;

        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                RallyPointSaveData saved = points[i];
                if (saved == null)
                    continue;

                rallyPoints.Add(new RallyPoint
                {
                    id = saved.id,
                    nome = string.IsNullOrWhiteSpace(saved.nome) ? $"Rally {saved.id}" : saved.nome,
                    hexDestino = new Vector2Int(saved.hexX, saved.hexY),
                    teamOwner = saved.teamOwner,
                    ativo = saved.ativo
                });
            }
        }

        if (assignments != null)
        {
            for (int i = 0; i < assignments.Count; i++)
            {
                RallyAssignmentSaveData saved = assignments[i];
                if (saved == null)
                    continue;

                if (FindRallyPointById(saved.rallyPointId) == null)
                    continue;

                rallyAssignments.Add(new RallyAssignment
                {
                    rallyPointId = saved.rallyPointId,
                    unitId = saved.unitId
                });
            }
        }

        RecalculateNextRallyPointId();
        CleanupInvisibleAssignments();
        PlanningDataChanged?.Invoke();
    }

    public IEnumerator ExecuteTurnStartRallyPhase(TeamId activeTeam)
    {
        TryAutoAssignReferences();
        turnStartRallyExecutionInProgress = true;

        int movedCount = 0;
        int skippedCount = 0;
        int removedCount = 0;

        CleanupInvisibleAssignments();
        List<RallyAssignment> list = BuildExecutionList(activeTeam);
        Debug.Log($"[Rally][TurnStart] team={(int)activeTeam} assignments={list.Count}");

        for (int i = 0; i < list.Count; i++)
        {
            RallyAssignment assignment = list[i];
            if (assignment == null)
                continue;

            RallyPoint point = FindRallyPointById(assignment.rallyPointId);
            if (point == null || !point.ativo || point.teamOwner != (int)activeTeam)
                continue;

            UnitManager unit = ResolveUnitByInstanceId(assignment.unitId);
            if (!IsUnitFieldVisible(unit) || (int)unit.TeamId != (int)activeTeam)
            {
                RemoveSpecificAssignment(assignment, "unidade fora de campo");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=not_visible");
                continue;
            }

            Vector3Int unitCell = unit.CurrentCellPosition; unitCell.z = 0;
            Vector3Int destination = new Vector3Int(point.hexDestino.x, point.hexDestino.y, 0);

            int distanceBefore = ComputeBoardDistance(unitCell, destination);
            if (distanceBefore >= 0 && distanceBefore <= 2)
            {
                RemoveSpecificAssignment(assignment, "proximo o suficiente");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=distance_le_2");
                continue;
            }

            if (!HasTraversableRouteIgnoringUnits(unit, destination))
            {
                RemoveSpecificAssignment(assignment, "terreno sem rota");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=terrain_no_route");
                continue;
            }

            if (!HasAnyOccupancyRouteProgress(unit, destination, distanceBefore))
            {
                RemoveSpecificAssignment(assignment, "sem rota alternativa");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=no_alternative_route");
                continue;
            }

            if (!TryResolveBestTurnProgressPath(unit, destination, distanceBefore, out List<Vector3Int> bestPath, out _))
            {
                skippedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=skip reason=no_progress_this_turn");
                continue;
            }

            bool completed = false; bool success = false; int movedHexes = 0;
            yield return turnStateManager.ExecutePlanningMoveOnlyAlongPath(
                unit,
                bestPath,
                $"RallyMove: rp={point.id} unit={assignment.unitId}",
                (ok, moved) => { completed = true; success = ok; movedHexes = Mathf.Max(0, moved); });

            if (!completed || !success || movedHexes <= 0)
            {
                skippedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=skip reason=runtime_move_failed");
                continue;
            }

            movedCount++;
            Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=move movedHexes={movedHexes}");

            if (combatTouchedUnits.Contains(assignment.unitId))
            {
                RemoveSpecificAssignment(assignment, "combate/dano");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=combat_or_damage");
                continue;
            }

            Vector3Int postCell = unit.CurrentCellPosition; postCell.z = 0;
            if (ComputeBoardDistance(postCell, destination) == 0)
            {
                RemoveSpecificAssignment(assignment, "destino alcancado");
                removedCount++;
                Debug.Log($"[Rally][Unit] unit={assignment.unitId} decision=removed reason=arrived");
            }
        }

        Debug.Log($"[Rally][TurnStart] summary moved={movedCount} skipped={skippedCount} removed={removedCount}");
        turnStartRallyExecutionInProgress = false;
    }

    private List<RallyAssignment> BuildExecutionList(TeamId activeTeam)
    {
        List<RallyAssignment> result = new List<RallyAssignment>();
        for (int i = 0; i < rallyAssignments.Count; i++)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null)
                continue;
            RallyPoint p = FindRallyPointById(a.rallyPointId);
            if (p == null || !p.ativo || p.teamOwner != (int)activeTeam)
                continue;
            result.Add(new RallyAssignment { rallyPointId = a.rallyPointId, unitId = a.unitId });
        }

        result.Sort((a, b) => {
            int byRp = a.rallyPointId.CompareTo(b.rallyPointId);
            return byRp != 0 ? byRp : a.unitId.CompareTo(b.unitId);
        });
        return result;
    }

    private bool TryResolveBestTurnProgressPath(UnitManager unit, Vector3Int destination, int currentDistance, out List<Vector3Int> bestPath, out int bestDistance)
    {
        bestPath = null;
        bestDistance = currentDistance;
        if (unit == null || terrainTilemap == null)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(terrainTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count <= 0)
            return false;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in paths)
        {
            if (pair.Value == null || pair.Value.Count < 2)
                continue;
            Vector3Int c = pair.Key; c.z = 0;
            int d = ComputeBoardDistance(c, destination);
            if (d < 0 || d >= currentDistance)
                continue;

            if (bestPath == null || d < bestDistance || (d == bestDistance && pair.Value.Count < bestPath.Count))
            {
                bestDistance = d;
                bestPath = new List<Vector3Int>(pair.Value);
            }
        }

        return bestPath != null;
    }

    private bool HasAnyOccupancyRouteProgress(UnitManager unit, Vector3Int destination, int currentDistance)
    {
        if (unit == null || terrainTilemap == null)
            return false;

        int probeSteps = Mathf.Clamp(Mathf.Max(unit.GetMovementRange(), unit.RemainingMovementPoints) * 6, 6, 60);
        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(terrainTilemap, unit, probeSteps, terrainDatabase);
        if (paths == null || paths.Count <= 0)
            return false;

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in paths)
        {
            Vector3Int c = pair.Key; c.z = 0;
            int d = ComputeBoardDistance(c, destination);
            if (d >= 0 && d < currentDistance)
                return true;
        }

        return false;
    }

    private bool HasTraversableRouteIgnoringUnits(UnitManager unit, Vector3Int destination)
    {
        if (unit == null || terrainTilemap == null)
            return false;

        Vector3Int origin = unit.CurrentCellPosition; origin.z = 0;
        destination.z = 0;
        if (origin == destination)
            return true;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        queue.Enqueue(origin);
        visited.Add(origin);
        int guard = 0;
        while (queue.Count > 0 && guard++ < 12000)
        {
            Vector3Int current = queue.Dequeue();
            if (current == destination)
                return true;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i]; next.z = 0;
                if (!visited.Add(next))
                    continue;
                if (!UnitMovementPathRules.TryGetEnterCellCost(terrainTilemap, unit, next, terrainDatabase, out _))
                    continue;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private int ComputeBoardDistance(Vector3Int fromCell, Vector3Int toCell)
    {
        if (terrainTilemap == null)
            return -1;
        fromCell.z = 0;
        toCell.z = 0;
        if (fromCell == toCell)
            return 0;

        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, int> dist = new Dictionary<Vector3Int, int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        queue.Enqueue(fromCell);
        dist[fromCell] = 0;

        int guard = 0;
        while (queue.Count > 0 && guard++ < 12000)
        {
            Vector3Int current = queue.Dequeue();
            int currentDist = dist[current];
            if (current == toCell)
                return currentDist;

            neighbors.Clear();
            UnitMovementPathRules.GetImmediateHexNeighbors(terrainTilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i]; next.z = 0;
                if (dist.ContainsKey(next))
                    continue;
                dist[next] = currentDist + 1;
                queue.Enqueue(next);
            }
        }

        return -1;
    }

    private void ProcessPlanningMapClick()
    {
        if (!WasLeftClickPressedThisFrame() || IsPointerOverUi() || terrainTilemap == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector2 mouse = ReadMouseScreenPosition();
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, Mathf.Abs(cam.transform.position.z)));
        Vector3Int cell = terrainTilemap.WorldToCell(world); cell.z = 0;
        if (terrainTilemap.GetTile(cell) == null)
            return;

        cursorController?.SetCell(cell, playMoveSfx: false, adjustCamera: false);
        if (TryToggleAssignmentAtCell(cell, out string assignmentMessage))
        {
            PanelDialogController.TrySetTransientText(assignmentMessage, 1.9f);
            return;
        }

        SetPendingDestination(cell);
        PanelDialogController.TrySetTransientText($"Planning: destino pendente em ({cell.x},{cell.y})", 1.8f);
    }

    private void CleanupInvisibleAssignments()
    {
        bool changed = false;
        for (int i = rallyAssignments.Count - 1; i >= 0; i--)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null || FindRallyPointById(a.rallyPointId) == null || !IsUnitFieldVisible(ResolveUnitByInstanceId(a.unitId)))
            {
                rallyAssignments.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            PlanningDataChanged?.Invoke();
    }

    private void RemoveAssignmentsForUnit(int unitId, string reason)
    {
        bool changed = false;
        for (int i = rallyAssignments.Count - 1; i >= 0; i--)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null || a.unitId != unitId)
                continue;
            rallyAssignments.RemoveAt(i);
            changed = true;
            Debug.Log($"[Rally][Assignment] removed unit={unitId} reason={reason}");
        }

        if (changed)
            PlanningDataChanged?.Invoke();
    }

    private void RemoveSpecificAssignment(RallyAssignment assignment, string reason)
    {
        if (assignment == null)
            return;

        for (int i = rallyAssignments.Count - 1; i >= 0; i--)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null || a.rallyPointId != assignment.rallyPointId || a.unitId != assignment.unitId)
                continue;
            rallyAssignments.RemoveAt(i);
            Debug.Log($"[Rally][Assignment] removed rp={assignment.rallyPointId} unit={assignment.unitId} reason={reason}");
            PlanningDataChanged?.Invoke();
            return;
        }
    }

    private int RemoveAssignmentsByRallyPoint(int rallyPointId)
    {
        int removed = 0;
        for (int i = rallyAssignments.Count - 1; i >= 0; i--)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null || a.rallyPointId != rallyPointId)
                continue;
            rallyAssignments.RemoveAt(i);
            removed++;
        }
        return removed;
    }

    private RallyPoint FindRallyPointById(int id)
    {
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint p = rallyPoints[i];
            if (p != null && p.id == id)
                return p;
        }

        return null;
    }

    private RallyPoint GetFirstRallyPointForActiveTeam()
    {
        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        RallyPoint first = null;
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint p = rallyPoints[i];
            if (p == null || p.teamOwner != (int)activeTeam)
                continue;
            if (first == null || p.id < first.id)
                first = p;
        }
        return first;
    }

    private int CountRallyPointsForTeam(int teamOwner)
    {
        int count = 0;
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint p = rallyPoints[i];
            if (p != null && p.teamOwner == teamOwner)
                count++;
        }
        return count;
    }

    private UnitManager ResolveUnitByInstanceId(int unitId)
    {
        if (unitId <= 0)
            return null;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager u = units[i];
            if (u != null && u.gameObject.activeInHierarchy && u.InstanceId == unitId)
                return u;
        }

        return null;
    }

    private bool IsUnitEligibleForAssignment(UnitManager unit, int teamOwner)
    {
        return IsUnitFieldVisible(unit) && (int)unit.TeamId == teamOwner;
    }

    private static bool IsUnitFieldVisible(UnitManager unit)
    {
        return unit != null && unit.gameObject.activeInHierarchy && !unit.IsEmbarked && !unit.IsDead;
    }

    private void RefreshFlagsForActiveTeam()
    {
        if (!planningModeActive || terrainTilemap == null)
            return;

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        HashSet<int> visible = new HashSet<int>();

        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint p = rallyPoints[i];
            if (p == null || p.teamOwner != (int)activeTeam)
                continue;

            visible.Add(p.id);
            if (!rallyFlags.TryGetValue(p.id, out GameObject go) || go == null)
            {
                go = new GameObject($"RallyFlag_{p.id}");
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ResolveFlagSprite();
                sr.color = rallyFlagColor;
                sr.sortingOrder = 320;
                rallyFlags[p.id] = go;
            }

            Vector3Int c = new Vector3Int(p.hexDestino.x, p.hexDestino.y, 0);
            go.transform.position = terrainTilemap.GetCellCenterWorld(c) + rallyFlagOffset;
            go.SetActive(true);
        }

        List<int> ids = new List<int>(rallyFlags.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            if (visible.Contains(id))
                continue;
            if (rallyFlags.TryGetValue(id, out GameObject go) && go != null)
                go.SetActive(false);
        }
    }

    private void SetAllFlagsVisible(bool visible)
    {
        List<int> ids = new List<int>(rallyFlags.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!rallyFlags.TryGetValue(ids[i], out GameObject go) || go == null)
                continue;
            go.SetActive(visible);
        }
    }

    private void UpdateAssignedUnitsPulse()
    {
        if (!planningModeActive)
            return;

        TeamId activeTeam = matchController != null ? matchController.ActiveTeam : TeamId.Neutral;
        Color pulse = Color.Lerp(pulseA, pulseB, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.1f, pulseSpeed)));
        HashSet<int> highlighted = new HashSet<int>();

        for (int i = 0; i < rallyAssignments.Count; i++)
        {
            RallyAssignment a = rallyAssignments[i];
            if (a == null)
                continue;
            RallyPoint p = FindRallyPointById(a.rallyPointId);
            if (p == null || p.teamOwner != (int)activeTeam)
                continue;

            UnitManager u = ResolveUnitByInstanceId(a.unitId);
            if (!IsUnitFieldVisible(u))
                continue;
            SpriteRenderer r = u.GetComponentInChildren<SpriteRenderer>();
            if (r == null)
                continue;

            highlighted.Add(u.InstanceId);
            if (!unitOriginalColor.ContainsKey(u.InstanceId))
                unitOriginalColor[u.InstanceId] = r.color;
            r.color = pulse;
        }

        List<int> cached = new List<int>(unitOriginalColor.Keys);
        for (int i = 0; i < cached.Count; i++)
        {
            int unitId = cached[i];
            if (highlighted.Contains(unitId))
                continue;

            UnitManager u = ResolveUnitByInstanceId(unitId);
            if (u != null)
            {
                SpriteRenderer r = u.GetComponentInChildren<SpriteRenderer>();
                if (r != null)
                    r.color = unitOriginalColor[unitId];
            }
            unitOriginalColor.Remove(unitId);
        }
    }

    private void RestoreAssignedUnitColors()
    {
        List<int> ids = new List<int>(unitOriginalColor.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            UnitManager u = ResolveUnitByInstanceId(ids[i]);
            if (u == null)
                continue;

            SpriteRenderer r = u.GetComponentInChildren<SpriteRenderer>();
            if (r != null)
                r.color = unitOriginalColor[ids[i]];
        }

        unitOriginalColor.Clear();
    }

    private Sprite ResolveFlagSprite()
    {
        if (rallyFlagSprite != null)
            return rallyFlagSprite;

        Texture2D tex = Texture2D.whiteTexture;
        return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 32f);
    }

    private void RecalculateNextRallyPointId()
    {
        int maxId = 0;
        for (int i = 0; i < rallyPoints.Count; i++)
        {
            RallyPoint p = rallyPoints[i];
            if (p != null)
                maxId = Mathf.Max(maxId, p.id);
        }
        nextRallyPointId = Mathf.Max(1, maxId + 1);
    }

    private void TryAutoAssignReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
        if (terrainTilemap == null && cursorController != null)
            terrainTilemap = cursorController.BoardTilemap;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private static bool WasLeftClickPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    private static Vector2 ReadMouseScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();
#endif
        return Input.mousePosition;
    }
}
