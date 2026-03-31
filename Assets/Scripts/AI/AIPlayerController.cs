using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Coordena o turno da IA em 4 fases sequenciais:
//   1. Servico do Comando  ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â reabastece/repara unidades
//   2. Mover Unidades      ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â executa movimento das unidades amigas
//   3. Comprar Unidades    ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â compra unidades nas fabricas proprias
//   4. Passar a Vez        ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â encerra o turno
public class AIPlayerController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private bool aiLog = true;

    [Header("Shopping AI")]
    [SerializeField, Tooltip("Perfil de compras da IA. Crie varios assets para perfis diferentes.")]
    private AIShoppingProfile shoppingProfile;

    private AIProfile profile;
    private AIStance currentStance = AIStance.Attack;
    private Coroutine activeTurnRoutine;
    private AIShoppingProfile runtimeShoppingProfileFallback;

    public AIStance CurrentStance => currentStance;

    private string T(TeamId team, int fase) =>
        $"[AI][T{(matchController != null ? matchController.CurrentTurn : 0)}][{team}][Fase {fase}]";

    private void Awake()
    {
        EnsureShoppingProfileDefaults();
        profile = new BeginnerAIProfile();
    }

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
    }


#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureShoppingProfileDefaults();
    }
#endif

    private void EnsureShoppingProfileDefaults()
    {
        if (shoppingProfile != null)
            shoppingProfile.EnsureDefaults();
    }

    private AIShoppingProfile GetEffectiveShoppingProfile()
    {
        if (shoppingProfile != null)
        {
            shoppingProfile.EnsureDefaults();
            return shoppingProfile;
        }

        if (runtimeShoppingProfileFallback == null)
            runtimeShoppingProfileFallback = AIShoppingProfile.CreateRuntimeBasic();
        else
            runtimeShoppingProfileFallback.EnsureDefaults();

        return runtimeShoppingProfileFallback;
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
        if (activeTurnRoutine != null)
            StopCoroutine(activeTurnRoutine);
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (matchController == null)
            return;

        if (!matchController.IsActiveTeamAI())
            return;

        TeamId aiTeam = (TeamId)teamId;

        if (activeTurnRoutine != null)
            StopCoroutine(activeTurnRoutine);
        activeTurnRoutine = StartCoroutine(ExecuteAITurn(aiTeam));
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Orquestrador ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private IEnumerator ExecuteAITurn(TeamId aiTeam)
    {
        float delay = AnimationManager.Instance != null ? AnimationManager.Instance.AIPhaseDuration : 0.5f;

        // Fase 0 inicial + Fase 1
        yield return null; // aguarda FoW ser atualizada
        AISnapshot snapshot = TakeSnapshot(aiTeam);

        yield return StartCoroutine(Phase1_CommandService(aiTeam, snapshot));
        yield return new WaitForSeconds(delay);

        // Fase 2: 1 unidade por vez
        // Coleta IDs agora para saber quais unidades existiam no inicio do turno,
        // mas cada decisao usa snapshot fresco apos retornar ao neutro
        List<int> unitInstanceIds = CollectFriendlyUnitIds(snapshot);

        for (int i = 0; i < unitInstanceIds.Count; i++)
        {
            // Garante neutro antes de escanear e decidir
            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(3f));

            // Scan fresco: FoW atualizada, inimigos possivelmente diferentes
            snapshot = TakeSnapshot(aiTeam);

            UnitManager unit = FindUnitById(unitInstanceIds[i]);
            if (unit == null || unit.IsDead)
                continue;

            // Atribui alvo a partir do snapshot atual
            UnitManager assignedEnemy = AssignTargetForUnit(unit, snapshot, unitInstanceIds, i);

            yield return StartCoroutine(Phase2_MoveUnit(aiTeam, snapshot, unit, assignedEnemy));
            yield return new WaitForSeconds(delay);
        }

        // Fase 3 + Fase 4
        snapshot = TakeSnapshot(aiTeam);
        yield return StartCoroutine(Phase3_BuyUnits(aiTeam, snapshot));
        yield return new WaitForSeconds(delay);

        Phase4_EndTurn(aiTeam);
    }

    private AISnapshot TakeSnapshot(TeamId aiTeam)
    {
        AISnapshot snapshot = AISnapshot.Build(aiTeam, matchController);
        currentStance = profile.EvaluateStance(snapshot);
        LogSnapshot(aiTeam, snapshot);
        return snapshot;
    }

    private static List<int> CollectFriendlyUnitIds(AISnapshot snapshot)
    {
        List<int> ids = new List<int>(snapshot.FriendlyUnits.Count);
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager u = snapshot.FriendlyUnits[i];
            if (u != null && !u.IsDead)
                ids.Add(u.InstanceId);
        }
        return ids;
    }

    private static UnitManager FindUnitById(int instanceId)
    {
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager u = UnitManager.AllActive[i];
            if (u != null && u.InstanceId == instanceId)
                return u;
        }
        return null;
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Fase 1: Servico do Comando ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private IEnumerator Phase1_CommandService(TeamId aiTeam, AISnapshot snapshot)
    {
        if (aiLog) Debug.Log($"{T(aiTeam, 1)} Servico do Comando (stub)");
        // TODO: usar ServicoDoComandoSensor.CollectOptions e executar via TurnStateManager
        yield break;
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Fase 2: Mover 1 Unidade ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private IEnumerator Phase2_MoveUnit(TeamId aiTeam, AISnapshot snapshot, UnitManager unit, UnitManager assignedEnemy)
    {
        if (turnStateManager == null || unit == null || unit.IsDead)
            yield break;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        // Decide alvo: inimigo atribuido pelo orquestrador, fallback ao mais proximo, fallback ao HQ inimigo
        bool engagingEnemy = false;
        bool repositioningForDefense = false;
        Vector3Int moveTarget = default;
        bool defendMode = currentStance == AIStance.Defend;

        UnitManager targetEnemy = assignedEnemy != null && !assignedEnemy.IsDead
            ? assignedEnemy
            : FindClosestVisibleEnemy(unit, snapshot, restrictToDefenseRadius: false);

        if (targetEnemy != null)
        {
            moveTarget = targetEnemy.CurrentCellPosition;
            moveTarget.z = 0;
            engagingEnemy = true;
        }
        else if (defendMode && snapshot.HasHq)
        {
            moveTarget = snapshot.HqCell;
            moveTarget.z = 0;
            repositioningForDefense = true;
        }
        else if (snapshot.EnemyHqs.Count > 0)
        {
            moveTarget = snapshot.EnemyHqs[0].Cell;
            moveTarget.z = 0;
        }
        else
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} sem alvo para {unitCell}, pulando");
            yield break;
        }

        float selectDelay = AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f;

        // Cursor viaja ate a unidade e seleciona (popula movementPathsByCell)
        yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(unitCell));
        turnStateManager.HandleConfirmWithFeedback();
        yield return new WaitForSeconds(selectDelay);

        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.UnitSelected)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} nao selecionou unidade em {unitCell}");
            turnStateManager.HandleCancel();
            yield break;
        }

        // Celulas ocupadas pelos aliados (excluindo a propria unidade) ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â do snapshot atual
        HashSet<Vector3Int> occupiedByAllies = BuildAllyCellSet(snapshot, unit);
        bool unitIsOnHq = snapshot.HasHq && unitCell == snapshot.HqCell;
        bool forceAdvanceInDefense = defendMode && engagingEnemy && unitIsOnHq;
        if (forceAdvanceInDefense)
            occupiedByAllies.Add(unitCell);

        bool foundDestination = false;
        Vector3Int bestDest = unitCell;

        // Artilharia com alcance minimo > 1 deve parar em banda de distancia efetiva.
        if (engagingEnemy && TryGetPreferredArtilleryRange(unit, out int artilleryMinRange, out int artilleryMaxRange))
        {
            foundDestination = turnStateManager.TryGetBestReachableCellAtHexDistanceBand(
                snapshot.BoardTilemap,
                moveTarget,
                artilleryMinRange,
                artilleryMaxRange,
                occupiedByAllies,
                out bestDest,
                prioritizeDpq: true,
                unit: unit,
                preferMaxDistance: true);
        }

        // Fallback padrao: aproxima pelo menor hex distance com DPQ quando engajando.
        if (!foundDestination)
        {
            foundDestination = turnStateManager.TryGetBestReachableCellTowardsHexDistance(
                snapshot.BoardTilemap,
                moveTarget,
                occupiedByAllies,
                out bestDest,
                prioritizeDpq: engagingEnemy,
                unit: unit);
        }

        if (!foundDestination)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} sem celulas alcancaveis para {unitCell}");
            turnStateManager.HandleCancel();
            yield break;
        }

        bestDest.z = 0;

        // Move para o melhor destino (ou confirma no proprio cell = move parado)
        yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(bestDest));
        turnStateManager.HandleConfirmWithFeedback();

        if (bestDest != unitCell)
            yield return StartCoroutine(turnStateManager.WaitUntilMovementAnimationDone(5f));
        else
            yield return new WaitForSeconds(selectDelay);

        // Guard: estado inesperado apos movimento
        if (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.UnitSelected ||
            turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 2)} estado inesperado {turnStateManager.CurrentCursorState}, encerrando unidade");
            turnStateManager.HandleCancel();
            yield break;
        }

        // Sensores agora ativos ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â tenta atacar se engajando
        bool attacked = engagingEnemy && turnStateManager.HasAutomatedAttackAvailable()
            && turnStateManager.TryExecuteAutomatedAttackFirstTarget();

        if (!attacked)
            turnStateManager.HandleAutomatedMoveOnlyActionRequested();

        yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(4f));

        if (aiLog)
            Debug.Log($"{T(aiTeam, 2)} {unitCell} -> {bestDest} (alvo: {moveTarget}) | {(engagingEnemy ? (attacked ? "atacou" : "moveu sem ataque") : (repositioningForDefense ? "reposicionou defesa" : "avancou HQ"))}");
    }

    // Atribui o melhor inimigo para a unidade atual, evitando os ja escolhidos por unidades anteriores neste turno.
    // unitInstanceIds + unitIndex servem para saber quais unidades ja foram processadas e seus alvos.
    // Como cada decisao usa snapshot fresco, apenas rastreamos via campo de instancia.
    private readonly HashSet<int> _assignedEnemyIdsThisTurn = new HashSet<int>();

    private UnitManager AssignTargetForUnit(UnitManager unit, AISnapshot snapshot, List<int> allUnitIds, int unitIndex)
    {
        // Primeira unidade do turno: limpa o registro
        if (unitIndex == 0)
            _assignedEnemyIdsThisTurn.Clear();

        if (snapshot.VisibleEnemies.Count == 0)
            return null;

        Vector3Int friendlyCell = unit.CurrentCellPosition;
        friendlyCell.z = 0;
        bool defendMode = currentStance == AIStance.Defend && snapshot.HasHq && snapshot.BoardTilemap != null;
        Vector3Int defenseReferenceCell = defendMode ? snapshot.HqCell : friendlyCell;
        defenseReferenceCell.z = 0;
        float bestDistSq = float.MaxValue;
        UnitManager best = null;
        int bestBand = int.MaxValue;
        int bestAssignedPenalty = int.MaxValue;

        for (int j = 0; j < snapshot.VisibleEnemies.Count; j++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[j];
            if (enemy == null || enemy.IsDead)
                continue;

            Vector3Int enemyCell = enemy.CurrentCellPosition;
            enemyCell.z = 0;

            int defenseBand = 0;
            if (defendMode && !IsEnemyWithinDefendRadius(snapshot, enemy))
                defenseBand = 1;

            int assignedPenalty = _assignedEnemyIdsThisTurn.Contains(enemy.InstanceId) ? 1 : 0;
            float distSq = (enemyCell - (defendMode ? defenseReferenceCell : friendlyCell)).sqrMagnitude;

            bool better = defenseBand < bestBand
                || (defenseBand == bestBand && assignedPenalty < bestAssignedPenalty)
                || (defenseBand == bestBand && assignedPenalty == bestAssignedPenalty && distSq < bestDistSq);

            if (!better)
                continue;

            bestBand = defenseBand;
            bestAssignedPenalty = assignedPenalty;
            bestDistSq = distSq;
            best = enemy;
        }

        if (best != null)
            _assignedEnemyIdsThisTurn.Add(best.InstanceId);

        return best;
    }

    private static bool IsEnemyWithinDefendRadius(AISnapshot snapshot, UnitManager enemy)
    {
        if (snapshot == null || enemy == null || !snapshot.HasHq || snapshot.BoardTilemap == null)
            return false;

        Vector3Int hqCell = snapshot.HqCell;
        hqCell.z = 0;
        Vector3Int enemyCell = enemy.CurrentCellPosition;
        enemyCell.z = 0;
        return HexCoordinates.IsWithinRange(snapshot.BoardTilemap, hqCell, enemyCell, snapshot.HqDefendRadius);
    }

    private static HashSet<Vector3Int> BuildAllyCellSet(AISnapshot snapshot, UnitManager excluding)
    {
        HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager u = snapshot.FriendlyUnits[i];
            if (u == null || u == excluding || u.IsDead)
                continue;
            Vector3Int cell = u.CurrentCellPosition;
            cell.z = 0;
            set.Add(cell);
        }
        return set;
    }

    private UnitManager FindClosestVisibleEnemy(UnitManager unit, AISnapshot snapshot, bool restrictToDefenseRadius = false)
    {
        Tilemap board = snapshot.BoardTilemap;
        Vector3 unitWorld = board != null
            ? board.GetCellCenterWorld(unit.CurrentCellPosition)
            : new Vector3(unit.CurrentCellPosition.x, unit.CurrentCellPosition.y, 0f);

        UnitManager closest = null;
        float bestDistSq = float.MaxValue;
        for (int i = 0; i < snapshot.VisibleEnemies.Count; i++)
        {
            UnitManager enemy = snapshot.VisibleEnemies[i];
            if (enemy == null || enemy.IsDead)
                continue;
            if (restrictToDefenseRadius && !IsEnemyWithinDefendRadius(snapshot, enemy))
                continue;
            Vector3 enemyWorld = board != null
                ? board.GetCellCenterWorld(enemy.CurrentCellPosition)
                : new Vector3(enemy.CurrentCellPosition.x, enemy.CurrentCellPosition.y, 0f);
            float distSq = (enemyWorld - unitWorld).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                closest = enemy;
            }
        }
        return closest;
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Fase 3: Comprar Unidades ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬
    private IEnumerator Phase3_BuyUnits(TeamId aiTeam, AISnapshot snapshot)
    {
        if (turnStateManager == null)
        {
            if (aiLog) Debug.Log($"{T(aiTeam, 3)} TurnStateManager nao encontrado, pulando");
            yield break;
        }

        int bought = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            AIConstructionInfo info = snapshot.KnownConstructions[i];
            if (info.TeamId != aiTeam || !info.CanProduceUnits || info.Source == null)
                continue;

            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));

            // Snapshot fresco para manter composicao/money/plano atualizados apos cada compra.
            AISnapshot current = TakeSnapshot(aiTeam);
            int currentMoney = matchController != null ? matchController.GetActualMoney(aiTeam) : 0;
            int incomePerTurn = matchController != null ? matchController.GetIncomePerTurn(aiTeam) : 0;

            if (!TryResolveShoppingPlan(aiTeam, current, info.Source, currentMoney, incomePerTurn, out int targetIndex, out string plannedUnitId, out string plannedReason))
            {
                if (aiLog) Debug.Log($"{T(aiTeam, 3)} sem compra planejada em {info.DisplayName} (saldo={currentMoney}) | motivo: {plannedReason}");
                continue;
            }

            Vector3Int cell = info.Source.CurrentCellPosition;
            cell.z = 0;

            // Fluxo replay-like: cursor vai para a construcao e confirma para abrir ShoppingAndServices.
            yield return StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(cell));
            turnStateManager.HandleConfirmWithFeedback();

            float selectDelay = AnimationManager.Instance != null ? AnimationManager.Instance.AIUnitSelectDelay : 0.12f;
            yield return new WaitForSeconds(selectDelay);

            int guard = 0;
            const int maxGuard = 256;
            while (turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                int currentIndex = turnStateManager.GetShoppingSelectedIndexForReplay();
                if (currentIndex >= targetIndex)
                    break;

                if (guard++ >= maxGuard)
                {
                    if (aiLog) Debug.Log($"{T(aiTeam, 3)} guarda de navegacao atingida (targetIndex={targetIndex})");
                    break;
                }

                bool moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(Vector3Int.right);
                if (!moved)
                    moved = turnStateManager.TryResolveShoppingCursorMoveForReplay(new Vector3Int(0, -1, 0));
                if (!moved)
                {
                    if (aiLog) Debug.Log($"{T(aiTeam, 3)} falha ao navegar catalogo (current={currentIndex}, target={targetIndex})");
                    break;
                }

                if (selectDelay > 0f)
                    yield return new WaitForSeconds(selectDelay);
                yield return null;
            }

            bool success = turnStateManager.TryConfirmSelectedShoppingOptionForReplay();
            if (aiLog) Debug.Log($"{T(aiTeam, 3)} compra em {info.DisplayName}: {(success ? "OK" : "falhou")} ({plannedUnitId}) | motivo: {plannedReason}");
            if (success)
            {
                bought++;
            }

            yield return StartCoroutine(turnStateManager.WaitUntilAutomatedNeutralReady(2f));
        }

        if (aiLog) Debug.Log($"{T(aiTeam, 3)} total comprado: {bought}");
    }

    private bool TryResolveShoppingPlan(
        TeamId aiTeam,
        AISnapshot snapshot,
        ConstructionManager construction,
        int currentMoney,
        int incomePerTurn,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = "nenhum";

        if (construction == null)
        {
            plannedReason = "construcao invalida";
            return false;
        }

        AIShoppingProfile effectiveProfile = GetEffectiveShoppingProfile();
        AIShoppingMode mode = currentStance == AIStance.Defend
            ? effectiveProfile.defenseMode
            : effectiveProfile.attackMode;

        return TryResolveShoppingPlanFromMode(snapshot, construction, currentMoney, incomePerTurn, mode, out targetIndex, out plannedUnitId, out plannedReason);
    }

    private bool TryResolveShoppingPlanFromMode(
        AISnapshot snapshot,
        ConstructionManager construction,
        int currentMoney,
        int incomePerTurn,
        AIShoppingMode mode,
        out int targetIndex,
        out string plannedUnitId,
        out string plannedReason)
    {
        targetIndex = -1;
        plannedUnitId = null;
        plannedReason = "nenhum";

        if (mode == null || mode.groups == null || mode.groups.Count == 0)
        {
            plannedReason = "modo sem grupos configurados";
            return false;
        }

        List<AIShoppingGroup> orderedGroups = GetGroupsByPriority(mode.groups);
        Dictionary<AIShoppingGroup, int> countsByGroup = CountFriendlyUnitsByConfiguredGroup(snapshot, orderedGroups);
        int totalUnits = snapshot != null && snapshot.FriendlyUnits != null ? snapshot.FriendlyUnits.Count : 0;
        int denominator = Mathf.Max(1, totalUnits);

        for (int i = 0; i < orderedGroups.Count; i++)
        {
            AIShoppingGroup group = orderedGroups[i];
            if (group == null)
                continue;

            float targetRatio = Mathf.Clamp01(group.targetPercentage / 100f);
            int currentCount = countsByGroup.TryGetValue(group, out int value) ? value : 0;
            float currentRatio = Mathf.Clamp01((float)currentCount / denominator);

            if (currentRatio + 0.0001f >= targetRatio)
                continue;

            if (TryResolveFirstAffordableFromGroup(construction, group, currentMoney, out targetIndex, out plannedUnitId))
            {
                plannedReason = $"grupo={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0} | prioridade={group.priority}";
                return true;
            }

            if (mode.saveForNextRound)
            {
                if (mode.allowFallbackWhenSaving &&
                    TryResolveFirstAffordableFromUnitList(construction, mode.fallbackUnits, currentMoney, out targetIndex, out plannedUnitId))
                {
                    plannedReason = $"fallback-save | modo={mode.label} | motivo=guardando para composicao de grupo {group.label}";
                    return true;
                }

                plannedReason = $"economizou para proxima rodada | grupo pendente={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0}";
                return false;
            }
        }

        // Se todas as metas ja foram atendidas (ou nao havia compra possivel na meta),
        // compra pela ordem de prioridade/lista configurada.
        for (int i = 0; i < orderedGroups.Count; i++)
        {
            AIShoppingGroup group = orderedGroups[i];
            if (group == null)
                continue;

            if (TryResolveFirstAffordableFromGroup(construction, group, currentMoney, out targetIndex, out plannedUnitId))
            {
                int currentCount = countsByGroup.TryGetValue(group, out int value) ? value : 0;
                float currentRatio = Mathf.Clamp01((float)currentCount / denominator);
                float targetRatio = Mathf.Clamp01(group.targetPercentage / 100f);
                plannedReason = $"grupo-prioridade={group.label} | composicao={currentCount}/{denominator} ({currentRatio:P0}) | alvo={targetRatio:P0} | prioridade={group.priority}";
                return true;
            }
        }

        // Sem nada acessivel nos grupos: tenta fallback de contingencia configurado no modo.
        if (TryResolveFirstAffordableFromUnitList(construction, mode.fallbackUnits, currentMoney, out targetIndex, out plannedUnitId))
        {
            plannedReason = $"fallback-modo={mode.label} | motivo=sem oferta acessivel nos grupos";
            return true;
        }

        // Sem nada acessivel agora: opcionalmente poupa para o proximo turno.
        if (mode.saveForNextRound)
        {
            plannedReason = "economizou para proxima rodada | sem ofertas acessiveis nos grupos/fallback";
            return false;
        }

        // Fallback final: tenta qualquer oferta acessivel no catalogo.
        if (TryResolveAnyAffordableOffer(construction, currentMoney, out targetIndex, out plannedUnitId))
        {
            plannedReason = "fallback-catalogo | motivo=qualquer oferta acessivel";
            return true;
        }

        plannedReason = "nenhuma oferta acessivel no catalogo";
        return false;
    }

    private static List<AIShoppingGroup> GetGroupsByPriority(List<AIShoppingGroup> groups)
    {
        List<AIShoppingGroup> ordered = new List<AIShoppingGroup>();
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] != null)
                ordered.Add(groups[i]);
        }

        ordered.Sort((a, b) =>
        {
            int pa = a != null ? a.priority : int.MaxValue;
            int pb = b != null ? b.priority : int.MaxValue;
            int cmp = pa.CompareTo(pb);
            if (cmp != 0)
                return cmp;

            string la = a != null ? a.label : string.Empty;
            string lb = b != null ? b.label : string.Empty;
            return string.CompareOrdinal(la, lb);
        });

        return ordered;
    }

    private static Dictionary<AIShoppingGroup, int> CountFriendlyUnitsByConfiguredGroup(AISnapshot snapshot, List<AIShoppingGroup> orderedGroups)
    {
        Dictionary<AIShoppingGroup, int> counts = new Dictionary<AIShoppingGroup, int>();
        if (orderedGroups == null)
            return counts;

        for (int i = 0; i < orderedGroups.Count; i++)
            counts[orderedGroups[i]] = 0;

        if (snapshot == null || snapshot.FriendlyUnits == null)
            return counts;

        for (int i = 0; i < snapshot.FriendlyUnits.Count; i++)
        {
            UnitManager unit = snapshot.FriendlyUnits[i];
            if (unit == null || unit.IsDead || string.IsNullOrWhiteSpace(unit.UnitId))
                continue;

            for (int g = 0; g < orderedGroups.Count; g++)
            {
                AIShoppingGroup group = orderedGroups[g];
                if (group == null || group.specificUnits == null || group.specificUnits.Count == 0)
                    continue;

                if (!GroupContainsUnitId(group, unit.UnitId))
                    continue;

                counts[group] = counts[group] + 1;
                break;
            }
        }

        return counts;
    }

    private static bool GroupContainsUnitId(AIShoppingGroup group, string unitId)
    {
        if (group == null || group.specificUnits == null || string.IsNullOrWhiteSpace(unitId))
            return false;

        for (int i = 0; i < group.specificUnits.Count; i++)
        {
            UnitData candidate = group.specificUnits[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                continue;

            if (string.Equals(candidate.id, unitId, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool TryResolveFirstAffordableFromGroup(
        ConstructionManager construction,
        AIShoppingGroup group,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null || group == null || group.specificUnits == null)
            return false;

        for (int i = 0; i < group.specificUnits.Count; i++)
        {
            UnitData wantedUnit = group.specificUnits[i];
            if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                continue;

            if (!TryGetAffordableOfferIndex(construction, wantedUnit, currentMoney, out int index, out UnitData offer))
                continue;

            targetIndex = index;
            plannedUnitId = offer != null ? offer.id : wantedUnit.id;
            return true;
        }

        return false;
    }

    private static bool TryResolveFirstAffordableFromUnitList(
        ConstructionManager construction,
        IReadOnlyList<UnitData> fallbackUnits,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null || fallbackUnits == null)
            return false;

        for (int i = 0; i < fallbackUnits.Count; i++)
        {
            UnitData wantedUnit = fallbackUnits[i];
            if (wantedUnit == null || string.IsNullOrWhiteSpace(wantedUnit.id))
                continue;

            if (!TryGetAffordableOfferIndex(construction, wantedUnit, currentMoney, out int index, out UnitData offer))
                continue;

            targetIndex = index;
            plannedUnitId = offer != null ? offer.id : wantedUnit.id;
            return true;
        }

        return false;
    }

    private static bool TryResolveAnyAffordableOffer(
        ConstructionManager construction,
        int currentMoney,
        out int targetIndex,
        out string plannedUnitId)
    {
        targetIndex = -1;
        plannedUnitId = null;

        if (construction == null)
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null)
            return false;

        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            int cost = Mathf.Max(0, unit.cost);
            if (cost > currentMoney)
                continue;

            targetIndex = i;
            plannedUnitId = unit.id;
            return true;
        }

        return false;
    }

    private static bool TryGetAffordableOfferIndex(ConstructionManager construction, UnitData wantedUnit, int currentMoney, out int index, out UnitData offer)
    {
        index = -1;
        offer = null;
        if (!TryGetOfferIndex(construction, wantedUnit, out int found, out UnitData unit) || unit == null)
            return false;

        int cost = Mathf.Max(0, unit.cost);
        if (currentMoney < cost)
            return false;

        index = found;
        offer = unit;
        return true;
    }

    private static bool TryGetOfferIndex(ConstructionManager construction, UnitData wantedUnit, out int index, out UnitData offer)
    {
        index = -1;
        offer = null;

        if (construction == null || wantedUnit == null)
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return false;

        string wantedId = wantedUnit.id;
        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            if (ReferenceEquals(unit, wantedUnit))
            {
                index = i;
                offer = unit;
                return true;
            }

            if (!string.IsNullOrWhiteSpace(wantedId) && !string.IsNullOrWhiteSpace(unit.id)
                && string.Equals(unit.id, wantedId, System.StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                offer = unit;
                return true;
            }
        }

        return false;
    }
    private static bool TryGetPreferredArtilleryRange(UnitManager unit, out int minRange, out int maxRange)
    {
        minRange = 1;
        maxRange = 1;
        if (unit == null)
            return false;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null || weapons.Count == 0)
            return false;

        Domain domain = unit.GetDomain();
        HeightLevel height = unit.GetHeightLevel();
        int bestMax = int.MinValue;
        bool found = false;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon weapon = weapons[i];
            if (weapon == null)
                continue;
            if (weapon.squadAmmunition <= 0)
                continue;
            if (!weapon.CanFireAtLayer(domain, height))
                continue;

            int wMin = weapon.GetRangeMin();
            int wMax = weapon.GetRangeMax();
            if (wMin <= 1)
                continue;

            if (!found || wMax > bestMax)
            {
                found = true;
                bestMax = wMax;
                minRange = wMin;
                maxRange = wMax;
            }
        }

        return found;
    }

    private void Phase4_EndTurn(TeamId aiTeam)
    {
        if (aiLog) Debug.Log($"{T(aiTeam, 4)} Passando a vez");
        matchController.AdvanceTurnWithTransition();
    }

    // ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ Log ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬ÃƒÂ¢Ã¢â‚¬ÂÃ¢â€šÂ¬

    private void LogSnapshot(TeamId aiTeam, AISnapshot snapshot)
    {
        if (!aiLog) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        string stanceLabel = currentStance == AIStance.Defend
            ? $"DEFESA (inimigo a <= {BeginnerAIProfile.DefendRadius} hexes do HQ)"
            : "ATAQUE";
        int turn = matchController != null ? matchController.CurrentTurn : 0;
        sb.AppendLine($"[AI][T{turn}][{aiTeam}][Fase 0] postura: {stanceLabel} | amigos: {snapshot.FriendlyUnits.Count} | inimigos visiveis: {snapshot.VisibleEnemies.Count}");
        sb.AppendLine($"  HQ proprio: {(snapshot.HasHq ? snapshot.HqCell.ToString() : "nao encontrado")}");

        if (snapshot.EnemyHqs.Count == 0)
            sb.AppendLine("  HQ inimigo: nenhum encontrado");
        for (int i = 0; i < snapshot.EnemyHqs.Count; i++)
            sb.AppendLine($"  HQ inimigo ({snapshot.EnemyHqs[i].TeamId}): {snapshot.EnemyHqs[i].Cell}");

        int ownedByAI = 0, neutral = 0, enemy = 0;
        for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
        {
            TeamId t = snapshot.KnownConstructions[i].TeamId;
            if (t == aiTeam) ownedByAI++;
            else if (t == TeamId.Neutral) neutral++;
            else enemy++;
        }
        sb.AppendLine($"  construcoes ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â proprias: {ownedByAI} | neutras: {neutral} | inimigas: {enemy}");

        if (snapshot.HasHq)
        {
            sb.Append($"  proximas do HQ (r={snapshot.HqDefendRadius}): ");
            if (snapshot.ConstructionsNearHq.Count == 0)
            {
                sb.AppendLine("nenhuma");
            }
            else
            {
                for (int i = 0; i < snapshot.ConstructionsNearHq.Count; i++)
                {
                    AIConstructionInfo info = snapshot.ConstructionsNearHq[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append($"{info.DisplayName}({info.TeamId})");
                }
                sb.AppendLine();
            }
        }

        Debug.Log(sb.ToString());
    }
}
























