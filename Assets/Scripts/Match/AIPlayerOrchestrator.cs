using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;

/// <summary>
/// O Cérebro da Inteligência Artificial V1.
/// Fabrica PlayerActions idênticos aos gerados por humanos e os entrega ao ReplayManager.
/// Toda animação, sensor, fog of war e gravação de replay são herdados de graça.
/// </summary>
public partial class AIPlayerOrchestrator : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private ReplayManager replayManager;
    [SerializeField] private TurnStateManager turnStateManager;

    [Header("Dependencies")]
    [SerializeField] private Tilemap boardTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;

    [Header("Settings")]
    [SerializeField] private bool showAISprites = true;

    private bool isActiveAndPlaying;
    private bool isDebugPaused;
    private Coroutine aiExecutionRoutine;

    /// <summary>
    /// Consultado pelo MatchController para liberar o cursor durante debug pause.
    /// </summary>
    public static bool IsDebugPaused { get; private set; }

    private static readonly List<AISensorPriority> _defaultPriority = new List<AISensorPriority>
    {
        AISensorPriority.Attack,
        AISensorPriority.Reposition,
    };

    // -------------------------------------------------------------------------
    // Debug API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pausa ou retoma o loop da IA sem avançar o turno.
    /// Quando pausada, o batch em andamento termina normalmente (para não corromper
    /// o ReplayManager) e o cursor fica livre para inspeção manual.
    /// </summary>
    public void SetDebugPaused(bool paused)
    {
        isDebugPaused = paused;
        IsDebugPaused = paused;
        if (!paused && isActiveAndPlaying && aiExecutionRoutine == null)
        {
            aiExecutionRoutine = StartCoroutine(AIBatchExecutionLoop());
        }
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        if (matchController == null) matchController = FindAnyObjectByType<MatchController>();
        if (replayManager == null) replayManager = FindAnyObjectByType<ReplayManager>();
        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (boardTilemap == null)
        {
            var cursorControl = FindAnyObjectByType<CursorController>();
            if (cursorControl != null) boardTilemap = cursorControl.BoardTilemap;
        }
        if (terrainDatabase == null) terrainDatabase = Resources.Load<TerrainDatabase>("TerrainDatabase");

        MatchController.OnActiveTeamChanged += HandleTeamChanged;
    }

    private void OnDestroy()
    {
        MatchController.OnActiveTeamChanged -= HandleTeamChanged;

        if (aiExecutionRoutine != null)
            StopCoroutine(aiExecutionRoutine);
    }

    // -------------------------------------------------------------------------
    // Turno
    // -------------------------------------------------------------------------

    private void HandleTeamChanged(int teamIndex)
    {
        if (matchController == null) return;

        TeamId newTeam = (TeamId)teamIndex;
        if (matchController.IsPlayerAI(newTeam))
        {
            Debug.Log($"[AI] Turno da IA comecou: {newTeam}");
            isActiveAndPlaying = true;

            if (aiExecutionRoutine != null)
                StopCoroutine(aiExecutionRoutine);
            aiExecutionRoutine = StartCoroutine(AIBatchExecutionLoop());
        }
        else
        {
            isActiveAndPlaying = false;
        }
    }

    // -------------------------------------------------------------------------
    // Loop principal
    // -------------------------------------------------------------------------

    private IEnumerator AIBatchExecutionLoop()
    {
        // Aguarda o Serviço do Comando automático terminar antes de iniciar os batches,
        // para não conflitar com o cursor em CursorState.CommandService.
        if (turnStateManager != null)
            yield return new WaitUntil(() => !turnStateManager.IsAutoCommandServiceBusy);

        while (isActiveAndPlaying)
        {
            float batchDelay = replayManager != null
                ? replayManager.GetEffectiveTimeBetweenBatchesForAutoplay()
                : 0.5f;
            if (batchDelay > 0f)
                yield return new WaitForSeconds(batchDelay);

            TeamId myTeam = matchController.ActiveTeam;
            List<UnitManager> myAvailableUnits = UnitManager.AllActive
                .Where(u => u.TeamId == myTeam && !u.HasActed && !u.IsDead && !u.IsEmbarked && !u.HasMerged)
                .OrderBy(u =>
                {
                    u.TryGetUnitData(out UnitData ud);
                    return ud != null ? (int)ud.aiInitiative : (int)AiInitiative.Medium;
                })
                .ThenByDescending(u => u.CurrentHP)
                .ToList();

            if (myAvailableUnits.Count == 0)
            {
                Debug.Log("[AI] Nenhuma unidade apta restante. Passando o Turno.");
                isActiveAndPlaying = false;
                matchController.AdvanceTurnWithTransition();
                break;
            }

            UnitManager selectedUnit = myAvailableUnits[0]; // TODO: priorizar por tática

            EvaluateRepairState(selectedUnit);
            if (selectedUnit.IsUnderRepair)
            {
                Vector3Int repairFrom = selectedUnit.CurrentCellPosition; repairFrom.z = 0;

                // Tenta fundir antes de marchar pra casa
                if (selectedUnit.TryGetUnitData(out UnitData repairUd) && repairUd != null && repairUd.fuseWhileInRepair)
                {
                    PlayerAction fuseBatch = TryDecideFuse(selectedUnit, myTeam, repairFrom);
                    if (fuseBatch != null)
                    {
                        replayManager.ExecuteLiveAIBatch(fuseBatch);
                        yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
                        continue;
                    }
                }

                Dictionary<Vector3Int, List<Vector3Int>> repairPaths = UnitMovementPathRules.CalcularCaminhosValidos(
                    boardTilemap, selectedUnit, Mathf.Max(0, selectedUnit.RemainingMovementPoints), terrainDatabase);
                HashSet<Vector3Int> repairOccupied = BuildOccupiedSnapshot(selectedUnit);
                List<Vector3Int> repairFree = repairPaths != null
                    ? repairPaths.Keys.Where(c => !repairOccupied.Contains(c)).ToList()
                    : new List<Vector3Int>();
                Vector3Int? alliedTarget = FindNearestAlliedBuilding(myTeam, repairFrom, repairOccupied);
                Debug.Log($"[AI] {selectedUnit.InstanceId} em reparos → reposicionando para prédio aliado {alliedTarget}");
                PlayerAction repairBatch = DecideReposition(selectedUnit, myTeam, repairFrom, repairFree, false, alliedTarget);
                replayManager.ExecuteLiveAIBatch(repairBatch);
                yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);
                continue;
            }

            PlayerAction actionBatch = DecideBatch(selectedUnit, myTeam);

            if (replayManager == null)
            {
                Debug.LogError("[AI] ReplayManager nulo!");
                break;
            }

            replayManager.ExecuteLiveAIBatch(actionBatch);
            yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);

            // Pausa de debug: batch atual terminou, cursor livre para inspeção
            if (isDebugPaused)
            {
                Debug.Log("[AI] Pausa de debug ativa — aguardando 'ai resume'");
                yield return new WaitUntil(() => !isDebugPaused);
                Debug.Log("[AI] Pausa de debug encerrada — retomando");
            }
        }

        aiExecutionRoutine = null;
    }

    // -------------------------------------------------------------------------
    // Decisão principal
    // -------------------------------------------------------------------------

    private PlayerAction DecideBatch(UnitManager unit, TeamId myTeam)
    {
        Vector3Int fromCell = unit.CurrentCellPosition;
        fromCell.z = 0;

        HashSet<Vector3Int> occupiedCells = BuildOccupiedSnapshot(unit);

        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            boardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);

        List<Vector3Int> freeCells = validPaths != null
            ? validPaths.Keys.Where(c => !occupiedCells.Contains(c)).ToList()
            : new List<Vector3Int>();

        bool useDpq = unit.TryGetUnitData(out UnitData unitData) && unitData != null && unitData.prioritizeDpqAtBattle;
        List<AISensorPriority> priorities = (unitData != null && unitData.aiSensorPriority != null && unitData.aiSensorPriority.Count > 0)
            ? unitData.aiSensorPriority
            : _defaultPriority;

        Vector3Int? contestedBuildingCell = null;
        Vector3Int? captureStepCell = null;
        PlayerAction decided = null;

        foreach (AISensorPriority priority in priorities)
        {
            switch (priority)
            {
                case AISensorPriority.Attack:
                {
                    decided = TryDecideAttack(unit, myTeam, fromCell, freeCells, useDpq);
                    if (decided != null) break;

                    // Attack falhou — se Capture tinha um objetivo pendente, retoma o avanço
                    if (captureStepCell.HasValue)
                    {
                        Debug.Log($"[AI] {unit.InstanceId} Attack falhou (aliados bloqueando?) → retoma avanço de captura para {captureStepCell.Value}");
                        decided = BuildMoveBatch(unit, myTeam, fromCell, captureStepCell.Value);
                        break;
                    }

                    // Attack falhou por FOW → aproximação cautelosa ao prédio contestado
                    if (contestedBuildingCell.HasValue)
                    {
                        Debug.Log($"[AI] {unit.InstanceId} Attack falhou por FOW → aproximação cautelosa a {contestedBuildingCell.Value}");
                        decided = DecideCautiousApproach(unit, myTeam, fromCell, freeCells, contestedBuildingCell.Value);
                        break;
                    }
                    break;
                }
                case AISensorPriority.Capture:
                {
                    decided = TryDecideCapture(unit, myTeam, fromCell, freeCells, out contestedBuildingCell, out captureStepCell);
                    break;
                }
                case AISensorPriority.Reposition:
                    decided = DecideReposition(unit, myTeam, fromCell, freeCells, useDpq);
                    break;
            }

            if (decided != null) break;
        }

        if (decided == null)
            decided = DecideReposition(unit, myTeam, fromCell, freeCells, useDpq);

        return decided;
    }

    // -------------------------------------------------------------------------
    // Construtores de batch compartilhados
    // -------------------------------------------------------------------------

    private PlayerAction BuildMoveBatch(UnitManager unit, TeamId myTeam,
        Vector3Int moveFrom, Vector3Int moveTo)
    {
        return new PlayerAction
        {
            IsAIGenerated = true,
            ActionType = PlayerActionType.UnitAction,
            ActingTeam = myTeam,
            TurnNumber = matchController.CurrentTurn,
            CursorHex = moveFrom,
            HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom = moveFrom,
            MoveTo = moveTo,
            HasMoveFrom = true,
            HasMoveTo = true,
            SensorAction = SensorActionType.None, // Menu: Wait
        };
    }

    // -------------------------------------------------------------------------
    // Repair Evaluation
    // -------------------------------------------------------------------------

    private void EvaluateRepairState(UnitManager unit)
    {
        if (!unit.TryGetUnitData(out UnitData ud) || ud == null) return;

        bool wasUnderRepair = unit.IsUnderRepair;
        bool shouldBeUnderRepair;

        if (wasUnderRepair)
        {
            // Sai do reparo: HP >= teto E recursos acima dos limites que dispararam o reparo
            bool hpRecovered = unit.CurrentHP >= ud.repairRecoverHpAbove;
            bool autonomyOk = ud.repairTriggerAutonomyPct <= 0 || unit.MaxFuel <= 0
                || (unit.CurrentFuel * 100 / unit.MaxFuel) > ud.repairTriggerAutonomyPct;
            bool ammoOk = !ud.repairTriggerAmmoEnabled || !HasAnyWeaponBelowAmmoPct(unit, ud.repairTriggerAmmoPct);
            shouldBeUnderRepair = !(hpRecovered && autonomyOk && ammoOk);
        }
        else
        {
            // Entra em reparo: qualquer gatilho ativo
            bool hpTrigger = ud.repairTriggerHpBelow > 0 && unit.CurrentHP <= ud.repairTriggerHpBelow;
            bool autonomyTrigger = ud.repairTriggerAutonomyPct > 0 && unit.MaxFuel > 0
                && (unit.CurrentFuel * 100 / unit.MaxFuel) <= ud.repairTriggerAutonomyPct;
            bool ammoTrigger = ud.repairTriggerAmmoEnabled && HasAnyWeaponBelowAmmoPct(unit, ud.repairTriggerAmmoPct);
            shouldBeUnderRepair = hpTrigger || autonomyTrigger || ammoTrigger;
        }

        if (shouldBeUnderRepair != wasUnderRepair)
        {
            unit.SetIsUnderRepair(shouldBeUnderRepair);
            Debug.Log($"[AI] {unit.InstanceId} isUnderRepair → {shouldBeUnderRepair}");
        }

        // Sprite de manutenção: visível apenas se showAISprites está ligado
        unit.SetAIMaintenanceActive(unit.IsUnderRepair && showAISprites);
    }

    private bool HasAnyWeaponBelowAmmoPct(UnitManager unit, int thresholdPct)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0) return false;
        int maxAmmo = unit.GetMaxAmmo();
        if (maxAmmo <= 0) return false;
        foreach (UnitEmbarkedWeapon w in weapons)
        {
            if (w == null || w.weapon == null) continue;
            if (w.squadAmmunition * 100 / maxAmmo <= thresholdPct) return true;
        }
        return false;
    }

    private Vector3Int? FindNearestAlliedBuilding(TeamId myTeam, Vector3Int fromCell,
        HashSet<Vector3Int> occupiedCells)
    {
        ConstructionManager bestFree = null;
        float bestFreeDist = float.MaxValue;
        ConstructionManager bestHq = null;
        float bestHqDist = float.MaxValue;

        foreach (ConstructionManager c in ConstructionManager.AllActive)
        {
            if (c.TeamId != myTeam) continue;

            Vector3Int cCell = c.CurrentCellPosition;
            cCell.z = 0;
            float dist = HexWorldDistance(fromCell, cCell);

            // HQ aliado como fallback sempre
            if (c.IsPlayerHeadQuarter && dist < bestHqDist)
            {
                bestHqDist = dist;
                bestHq = c;
            }

            // Prédio livre (sem unidade em cima)
            if (!occupiedCells.Contains(cCell) && dist < bestFreeDist)
            {
                bestFreeDist = dist;
                bestFree = c;
            }
        }

        if (bestFree != null)
        {
            Vector3Int t = bestFree.CurrentCellPosition; t.z = 0;
            return t;
        }

        // Todos os prédios aliados ocupados → marcha ao HQ e fica o mais perto possível
        if (bestHq != null)
        {
            Debug.Log($"[AI] Todos os prédios aliados ocupados → fallback ao HQ {bestHq.CurrentCellPosition}");
            Vector3Int t = bestHq.CurrentCellPosition; t.z = 0;
            return t;
        }

        return null;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private HashSet<Vector3Int> BuildOccupiedSnapshot(UnitManager excludeUnit)
    {
        var set = new HashSet<Vector3Int>();
        foreach (var u in UnitManager.AllActive)
        {
            if (u == excludeUnit || u.IsEmbarked) continue;
            Vector3Int pos = u.CurrentCellPosition;
            pos.z = 0;
            set.Add(pos);
        }
        return set;
    }

    private UnitManager FindClosestVisibleEnemy(Vector3Int fromCell, TeamId myTeam)
    {
        UnitManager closest = null;
        float bestDist = float.MaxValue;

        foreach (var u in UnitManager.AllActive)
        {
            if (u.TeamId == myTeam || u.IsDead || u.IsEmbarked) continue;
            if (!matchController.IsUnitVisibleForTeam(u, myTeam)) continue;

            Vector3Int pos = u.CurrentCellPosition;
            pos.z = 0;
            float dist = HexWorldDistance(fromCell, pos);
            if (dist < bestDist)
            {
                bestDist = dist;
                closest = u;
            }
        }

        return closest;
    }

    /// <summary>
    /// Distância real entre dois hexes usando posições no mundo do Tilemap —
    /// mesma fonte de verdade que GetImmediateHexNeighbors.
    /// </summary>
    private float HexWorldDistance(Vector3Int a, Vector3Int b)
    {
        if (boardTilemap == null) return 0f;
        Vector3 wa = boardTilemap.GetCellCenterWorld(a);
        Vector3 wb = boardTilemap.GetCellCenterWorld(b);
        return Vector2.Distance(wa, wb);
    }
}
