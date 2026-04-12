using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class AICombatHpSimulatorWindow : EditorWindow
{
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private RPSDatabase rpsDatabase;
    [SerializeField] private DPQMatchupDatabase dpqMatchupDatabase;
    [SerializeField] private WeaponPriorityData weaponPriorityData;
    [SerializeField] private AIDatabase aiDatabase;
    [SerializeField] private AIGeneralProfile aiProfile;
    [SerializeField] private AIStance stanceMode = AIStance.Attack;
    [SerializeField] private UnitManager attackerUnit;
    [SerializeField] private bool useCurrentHpFromUnit = true;
    [SerializeField] private int attackerHpInput = 10;
    [SerializeField] private bool considerMovement = true;
    [SerializeField] private bool useUnitRemainingMovement = true;
    [SerializeField] private int movementBudgetInput = 3;
    [SerializeField] private bool includeInactiveHierarchyUnits;

    private Vector2 scroll;
    private string report = "Ready.";

    [MenuItem("Tools/AI/AI Combat HP Simulator")]
    private static void OpenWindow()
    {
        GetWindow<AICombatHpSimulatorWindow>("AI Combat HP Simulator");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AI Combat HP Simulator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Escolha o atacante. A ferramenta varre defensores inimigos na cena, ranqueia o melhor alvo e explica o motivo.", MessageType.Info);

        turnStateManager = (TurnStateManager)EditorGUILayout.ObjectField("TurnStateManager", turnStateManager, typeof(TurnStateManager), true);
        rpsDatabase = (RPSDatabase)EditorGUILayout.ObjectField("RPS Database", rpsDatabase, typeof(RPSDatabase), false);
        dpqMatchupDatabase = (DPQMatchupDatabase)EditorGUILayout.ObjectField("DPQ Matchup DB", dpqMatchupDatabase, typeof(DPQMatchupDatabase), false);
        weaponPriorityData = (WeaponPriorityData)EditorGUILayout.ObjectField("Weapon Priority", weaponPriorityData, typeof(WeaponPriorityData), false);
        aiDatabase = (AIDatabase)EditorGUILayout.ObjectField("AI Database", aiDatabase, typeof(AIDatabase), false);
        aiProfile = (AIGeneralProfile)EditorGUILayout.ObjectField("AI Profile", aiProfile, typeof(AIGeneralProfile), false);
        stanceMode = (AIStance)EditorGUILayout.EnumPopup("Modo IA", stanceMode);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect (Contexto + Selecao)"))
            AutoDetectContext();
        if (GUILayout.Button("Limpar"))
            ClearState();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4f);
        attackerUnit = (UnitManager)EditorGUILayout.ObjectField("Atacante (Unit)", attackerUnit, typeof(UnitManager), true);
        useCurrentHpFromUnit = EditorGUILayout.ToggleLeft("Usar HP atual do atacante", useCurrentHpFromUnit);
        if (!useCurrentHpFromUnit)
            attackerHpInput = Mathf.Max(0, EditorGUILayout.IntField("HP Atacante (input)", attackerHpInput));

        considerMovement = EditorGUILayout.ToggleLeft("Considerar movimento antes do ataque", considerMovement);
        if (considerMovement)
        {
            useUnitRemainingMovement = EditorGUILayout.ToggleLeft("Usar movimento restante da Unit", useUnitRemainingMovement);
            if (!useUnitRemainingMovement)
                movementBudgetInput = Mathf.Max(0, EditorGUILayout.IntField("Movimento (input)", movementBudgetInput));
        }

        includeInactiveHierarchyUnits = EditorGUILayout.ToggleLeft("Incluir unidades inativas da hierarchy (Edit Mode)", includeInactiveHierarchyUnits);

        if (GUILayout.Button("Analisar Melhor Alvo"))
            AnalyzeTargets();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Relatorio", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void AnalyzeTargets()
    {
        if (attackerUnit == null)
        {
            report = "Falha: defina o atacante.";
            return;
        }

        if (!attackerUnit.TryGetUnitData(out UnitData attackerData) || attackerData == null)
        {
            report = "Falha: atacante sem UnitData valido.";
            return;
        }

        Tilemap board = ResolveBoardTilemap(attackerUnit);
        if (board == null)
        {
            report = "Falha: sem Tilemap para calcular distancia hex entre unidades.";
            return;
        }

        int attackerHpBefore = useCurrentHpFromUnit
            ? Mathf.Max(0, attackerUnit.CurrentHP)
            : Mathf.Max(0, attackerHpInput);

        int movementBudget = considerMovement
            ? (useUnitRemainingMovement ? Mathf.Max(0, attackerUnit.RemainingMovementPoints) : Mathf.Max(0, movementBudgetInput))
            : 0;
        bool hasOwnHq = TryResolveOwnHqCell(attackerUnit.TeamId, out Vector3Int ownHqCell);

        List<UnitManager> units = CollectUnitsForAnalysis(includeInactiveHierarchyUnits);
        List<CandidateEval> evaluations = new List<CandidateEval>(units.Count);

        TerrainDatabase terrainDb = turnStateManager != null ? turnStateManager.TerrainDatabaseRef : null;
        Dictionary<Vector3Int, List<Vector3Int>> movementPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            board,
            attackerUnit,
            movementBudget,
            terrainDb);
        HashSet<Vector3Int> occupiedByAllies = BuildAllyCellSetForAnalysis(units, attackerUnit);
        List<int> candidateDistances = new List<int>(16);

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager defenderUnit = units[i];
            if (!IsDefenderCandidate(attackerUnit, defenderUnit))
                continue;
            if (!defenderUnit.TryGetUnitData(out UnitData defenderData) || defenderData == null)
                continue;

            int rawDistance = ComputeHexDistance(board, attackerUnit.CurrentCellPosition, defenderUnit.CurrentCellPosition, 128);
            if (rawDistance <= 0)
                continue;

            int defenderHpBefore = Mathf.Max(0, defenderUnit.CurrentHP);
            int defenderDistanceToOwnHq = hasOwnHq
                ? Mathf.Max(0, ComputeHexDistance(board, defenderUnit.CurrentCellPosition, ownHqCell, 128))
                : -1;

            bool hasReachableAttackDistance = TryCollectReachableAttackDistances(
                board,
                attackerUnit.CurrentCellPosition,
                defenderUnit.CurrentCellPosition,
                movementPaths,
                occupiedByAllies,
                candidateDistances);
            if (!hasReachableAttackDistance)
                continue;

            CandidateEval bestEvalForTarget = default;
            bool foundValid = false;

            for (int d = 0; d < candidateDistances.Count; d++)
            {
                int effectiveDistance = Mathf.Max(1, candidateDistances[d]);
                AICombatHpSimulator.AICombatHpResult simulation = AICombatHpSimulator.Simulate(
                    attackerData,
                    defenderData,
                    attackerHpBefore,
                    defenderHpBefore,
                    effectiveDistance,
                    rpsDatabase,
                    dpqMatchupDatabase,
                    weaponPriorityData);

                if (!simulation.isValid)
                    continue;

                CandidateEval eval = BuildCandidateEval(
                    defenderUnit,
                    rawDistance,
                    effectiveDistance,
                    attackerHpBefore,
                    defenderHpBefore,
                    defenderDistanceToOwnHq,
                    simulation,
                    attackerData,
                    defenderData,
                    stanceMode);

                if (!foundValid || CompareCandidates(eval, bestEvalForTarget) < 0)
                {
                    bestEvalForTarget = eval;
                    foundValid = true;
                }
            }

            if (foundValid)
                evaluations.Add(bestEvalForTarget);
        }

        if (evaluations.Count <= 0)
        {
            report = "Sem defensores inimigos alcancaveis para ataque valido na cena atual.";
            return;
        }

        evaluations.Sort(CompareCandidates);

        AIData effectiveData = aiDatabase != null
            ? aiDatabase.ResolveFor(stanceMode, aiProfile)
            : null;

        report = BuildAnalysisReport(attackerUnit, attackerHpBefore, movementBudget, aiDatabase, aiProfile, effectiveData, stanceMode, evaluations);
    }
    private static bool IsDefenderCandidate(UnitManager attacker, UnitManager defender)
    {
        if (attacker == null || defender == null || attacker == defender)
            return false;
        if (!defender.gameObject.activeInHierarchy || defender.IsEmbarked)
            return false;
        if (defender.TeamId == attacker.TeamId)
            return false;
        return true;
    }

    private static CandidateEval BuildCandidateEval(
        UnitManager defenderUnit,
        int rawDistance,
        int effectiveDistance,
        int attackerHpBefore,
        int defenderHpBefore,
        int defenderDistanceToOwnHq,
        AICombatHpSimulator.AICombatHpResult simulation,
        UnitData attackerData,
        UnitData defenderData,
        AIStance stanceMode)
    {
        CandidateEval eval = new CandidateEval
        {
            defenderUnit = defenderUnit,
            rawDistance = rawDistance,
            effectiveDistance = effectiveDistance,
            attackerHpBefore = attackerHpBefore,
            defenderHpBefore = defenderHpBefore,
            defenderDistanceToOwnHq = defenderDistanceToOwnHq,
            simulation = simulation,
            attackerEliminated = simulation.isValid ? Mathf.Max(0, attackerHpBefore - simulation.attackerHpAfter) : 0,
            defenderEliminated = simulation.isValid ? Mathf.Max(0, defenderHpBefore - simulation.defenderHpAfter) : 0
        };

        if (!simulation.isValid)
        {
            eval.score = int.MinValue / 2;
            eval.reason = "invalido para esse alcance/armas";
            return eval;
        }

        int score = 0;
        int killWeight = stanceMode == AIStance.Defend ? 70000 : 100000;
        int surviveWeight = stanceMode == AIStance.Defend ? 30000 : 20000;
        int selfLossWeight = stanceMode == AIStance.Defend ? 1200 : 700;
        int enemyLossWeight = stanceMode == AIStance.Defend ? 900 : 1200;
        int enemyRemainingWeight = stanceMode == AIStance.Defend ? 60 : 90;
        int selfRemainingWeight = stanceMode == AIStance.Defend ? 80 : 40;
        int distanceWeight = stanceMode == AIStance.Defend ? 4 : 10;
        int hqThreatDistanceWeight = stanceMode == AIStance.Defend ? 250 : 0;
        int attackerCost = attackerData != null ? Mathf.Max(0, attackerData.cost) : 0;
        int defenderCost = defenderData != null ? Mathf.Max(0, defenderData.cost) : 0;
        int defenderCostTier = Mathf.Clamp(defenderCost / 1000, 0, 20);
        int attackerCostTier = Mathf.Clamp(attackerCost / 1000, 0, 20);
        int targetValuePerHpWeight = stanceMode == AIStance.Defend ? 55 : 95;
        int selfValuePerHpWeight = stanceMode == AIStance.Defend ? 70 : 45;
        int killValueWeight = stanceMode == AIStance.Defend ? 900 : 1500;

        if (simulation.killGuaranteed)
            score += killWeight;
        if (simulation.attackerSurvives)
            score += surviveWeight;
        else
            score -= 50000;

        score += eval.defenderEliminated * enemyLossWeight;
        score -= eval.attackerEliminated * selfLossWeight;
        score -= simulation.defenderHpAfter * enemyRemainingWeight;
        score += simulation.attackerHpAfter * selfRemainingWeight;
        score += eval.defenderEliminated * defenderCostTier * targetValuePerHpWeight;
        score -= eval.attackerEliminated * attackerCostTier * selfValuePerHpWeight;
        if (simulation.killGuaranteed)
            score += defenderCostTier * killValueWeight;
        score -= eval.effectiveDistance * distanceWeight;
        if (eval.defenderDistanceToOwnHq >= 0)
            score -= eval.defenderDistanceToOwnHq * hqThreatDistanceWeight;

        eval.score = score;
        eval.reason = BuildReasonText(eval, stanceMode);
        return eval;
    }

    private static string BuildReasonText(CandidateEval eval, AIStance stanceMode)
    {
        if (!eval.simulation.isValid)
            return "invalido";

        if (eval.simulation.killGuaranteed && eval.simulation.attackerSurvives)
            return stanceMode == AIStance.Defend
                ? "abate garantido preservando atacante (modo defesa)"
                : "abate garantido com sobrevivencia (modo ataque)";
        if (eval.simulation.killGuaranteed)
            return "abate garantido";
        if (!eval.simulation.attackerSurvives)
            return "alto risco para o atacante";
        return stanceMode == AIStance.Defend
            ? "troca conservadora (preservar HP)"
            : "melhor troca de HP ofensiva";
    }

    private static int CompareCandidates(CandidateEval a, CandidateEval b)
    {
        int scoreCmp = b.score.CompareTo(a.score);
        if (scoreCmp != 0)
            return scoreCmp;

        int killCmp = b.simulation.killGuaranteed.CompareTo(a.simulation.killGuaranteed);
        if (killCmp != 0)
            return killCmp;

        int surviveCmp = b.simulation.attackerSurvives.CompareTo(a.simulation.attackerSurvives);
        if (surviveCmp != 0)
            return surviveCmp;

        int elimCmp = b.defenderEliminated.CompareTo(a.defenderEliminated);
        if (elimCmp != 0)
            return elimCmp;

        bool aHasHqDist = a.defenderDistanceToOwnHq >= 0;
        bool bHasHqDist = b.defenderDistanceToOwnHq >= 0;
        if (aHasHqDist && bHasHqDist)
        {
            int hqCmp = a.defenderDistanceToOwnHq.CompareTo(b.defenderDistanceToOwnHq);
            if (hqCmp != 0)
                return hqCmp;
        }

        int distCmp = a.effectiveDistance.CompareTo(b.effectiveDistance);
        if (distCmp != 0)
            return distCmp;

        return string.CompareOrdinal(a.defenderUnit != null ? a.defenderUnit.name : string.Empty, b.defenderUnit != null ? b.defenderUnit.name : string.Empty);
    }

    private static string BuildAnalysisReport(
        UnitManager attacker,
        int attackerHpBefore,
        int movementBudget,
        AIDatabase aiDatabase,
        AIGeneralProfile aiProfile,
        AIData aiData,
        AIStance stanceMode,
        List<CandidateEval> evaluations)
    {
        StringBuilder sb = new StringBuilder(2048);
        sb.AppendLine("[AI Combat HP Simulator]");
        sb.AppendLine($"Atacante: {(attacker != null ? attacker.name : "(null)")}");
        sb.AppendLine("AI Database: " + (aiDatabase != null ? aiDatabase.name : "(null)") + " | AI Profile: " + (aiProfile != null ? aiProfile.name : "(null)") + " | AI Data: " + (aiData != null ? aiData.name : "(null)") + " | Modo IA: " + stanceMode);
        sb.AppendLine($"HP atacante (entrada): {attackerHpBefore}");
        sb.AppendLine($"Movimento considerado antes do tiro: {movementBudget}");
        sb.AppendLine($"Candidatos analisados: {evaluations.Count}");
        sb.AppendLine();

        CandidateEval best = evaluations[0];
        sb.AppendLine("Melhor alvo sugerido:");
        sb.AppendLine($"- {(best.defenderUnit != null ? best.defenderUnit.name : "(null)")}");
        sb.AppendLine($"- motivo: {best.reason}");
        sb.AppendLine($"- dist bruto={best.rawDistance} | dist efetiva={best.effectiveDistance}");
        if (best.defenderDistanceToOwnHq >= 0)
            sb.AppendLine($"- distancia defensor -> HQ proprio: {best.defenderDistanceToOwnHq}");
        if (best.simulation.isValid)
        {
            sb.AppendLine($"- HP restante previsto: A={best.simulation.attackerHpAfter} | D={best.simulation.defenderHpAfter}");
            sb.AppendLine($"- eliminados previstos: A=-{best.attackerEliminated} | D=-{best.defenderEliminated}");
            sb.AppendLine($"- killGuaranteed={best.simulation.killGuaranteed} | attackerSurvives={best.simulation.attackerSurvives}");
        }

        sb.AppendLine();
        sb.AppendLine("Ranking completo:");
        for (int i = 0; i < evaluations.Count; i++)
        {
            CandidateEval eval = evaluations[i];
            string name = eval.defenderUnit != null ? eval.defenderUnit.name : "(null)";
            if (!eval.simulation.isValid)
            {
                sb.AppendLine($"{i + 1}) {name} | INVALIDO | score={eval.score} | dist={eval.rawDistance}->{eval.effectiveDistance}");
                continue;
            }

            sb.AppendLine(
                $"{i + 1}) {name} | score={eval.score} | dist={eval.rawDistance}->{eval.effectiveDistance} | " +
                $"distHQ={(eval.defenderDistanceToOwnHq >= 0 ? eval.defenderDistanceToOwnHq.ToString() : "n/a")} | " +
                $"HP A={eval.simulation.attackerHpAfter} D={eval.simulation.defenderHpAfter} | " +
                $"elim A=-{eval.attackerEliminated} D=-{eval.defenderEliminated} | " +
                $"kill={eval.simulation.killGuaranteed} survive={eval.simulation.attackerSurvives} | {eval.reason}");
        }

        return sb.ToString();
    }

    private void AutoDetectContext()
    {
        if (turnStateManager == null)
            turnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
        if (rpsDatabase == null)
            rpsDatabase = FindFirstAsset<RPSDatabase>();
        if (dpqMatchupDatabase == null)
            dpqMatchupDatabase = FindFirstAsset<DPQMatchupDatabase>();
        if (weaponPriorityData == null)
            weaponPriorityData = FindFirstAsset<WeaponPriorityData>();

        if (aiDatabase == null)
        {
            AIDataResolution resolved = TryResolveAiDataFromController();
            aiDatabase = resolved.database;
            aiProfile = resolved.profile;
        }

        if (attackerUnit == null)
            attackerUnit = ResolveSelectedUnit();
    }

    private void ClearState()
    {
        attackerUnit = null;
        useCurrentHpFromUnit = true;
        attackerHpInput = 10;
        considerMovement = true;
        useUnitRemainingMovement = true;
        movementBudgetInput = 3;
        aiDatabase = null;
        aiProfile = null;
        stanceMode = AIStance.Attack;
        report = "Ready.";
    }

        private static AIDataResolution TryResolveAiDataFromController()
    {
        AIDataResolution resolved = default;

        AIPlayerController controller = Object.FindAnyObjectByType<AIPlayerController>();
        if (controller == null)
            return resolved;

        SerializedObject so = new SerializedObject(controller);

        SerializedProperty databaseProp = so.FindProperty("aiDatabase");
        if (databaseProp != null && databaseProp.objectReferenceValue is AIDatabase db)
            resolved.database = db;

        TeamId team = TeamId.Neutral;
        SerializedProperty matchProp = so.FindProperty("matchController");
        if (matchProp != null && matchProp.objectReferenceValue is MatchController match)
            team = match.ActiveTeam;

        if (controller.TryGetAssignedAIDataForTeam(team, out _, out AIGeneralProfile profile))
        {
            resolved.profile = profile;
        }

        return resolved;
    }
    private Tilemap ResolveBoardTilemap(UnitManager attacker)
    {
        if (TryGetTurnStateTerrainTilemap(turnStateManager, out Tilemap turnStateBoard))
            return turnStateBoard;
        if (attacker != null && attacker.BoardTilemap != null)
            return attacker.BoardTilemap;
        return Object.FindAnyObjectByType<Tilemap>();
    }

    private static bool TryGetTurnStateTerrainTilemap(TurnStateManager manager, out Tilemap terrainTilemap)
    {
        terrainTilemap = null;
        if (manager == null)
            return false;

        SerializedObject so = new SerializedObject(manager);
        terrainTilemap = so.FindProperty("terrainTilemap")?.objectReferenceValue as Tilemap;
        return terrainTilemap != null;
    }

    private static UnitManager ResolveSelectedUnit()
    {
        if (Selection.activeGameObject == null)
            return null;

        UnitManager unit = Selection.activeGameObject.GetComponent<UnitManager>();
        if (unit == null)
            unit = Selection.activeGameObject.GetComponentInParent<UnitManager>();
        return unit;
    }

    private static List<UnitManager> CollectUnitsForAnalysis(bool includeInactiveInEditMode)
    {
        if (Application.isPlaying)
            return UnitManager.AllActive;

        FindObjectsInactive includeInactive = includeInactiveInEditMode
            ? FindObjectsInactive.Include
            : FindObjectsInactive.Exclude;

        UnitManager[] hierarchyUnits = Object.FindObjectsByType<UnitManager>(includeInactive, FindObjectsSortMode.None);
        List<UnitManager> resolved = new List<UnitManager>(hierarchyUnits != null ? hierarchyUnits.Length : 0);
        if (hierarchyUnits == null)
            return resolved;

        for (int i = 0; i < hierarchyUnits.Length; i++)
        {
            UnitManager unit = hierarchyUnits[i];
            if (unit != null)
                resolved.Add(unit);
        }

        return resolved;
    }

    private static HashSet<Vector3Int> BuildAllyCellSetForAnalysis(List<UnitManager> units, UnitManager attacker)
    {
        HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        if (units == null || attacker == null)
            return occupied;

        TeamId team = attacker.TeamId;
        Vector3Int attackerCell = attacker.CurrentCellPosition;
        attackerCell.z = 0;

        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (unit == attacker)
                continue;
            if (unit.TeamId != team)
                continue;

            Vector3Int cell = unit.CurrentCellPosition;
            cell.z = 0;
            if (cell == attackerCell)
                continue;
            occupied.Add(cell);
        }

        return occupied;
    }

    private static bool TryCollectReachableAttackDistances(
        Tilemap boardTilemap,
        Vector3Int attackerCell,
        Vector3Int defenderCell,
        Dictionary<Vector3Int, List<Vector3Int>> movementPaths,
        HashSet<Vector3Int> occupiedByAllies,
        List<int> outputDistances)
    {
        if (outputDistances == null)
            return false;

        outputDistances.Clear();
        if (boardTilemap == null || movementPaths == null || movementPaths.Count <= 0)
            return false;

        attackerCell.z = 0;
        defenderCell.z = 0;
        HashSet<int> unique = new HashSet<int>();

        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> pair in movementPaths)
        {
            Vector3Int cell = pair.Key;
            cell.z = 0;

            bool isOrigin = cell == attackerCell;
            if (!isOrigin && occupiedByAllies != null && occupiedByAllies.Contains(cell))
                continue;

            int dist = ComputeHexDistance(boardTilemap, cell, defenderCell, 128);
            if (dist <= 0)
                continue;

            // Regra oficial do sensor:
            // - MoveuAndando: apenas armas com min=1 (ataque a dist=1).
            // - MoveuParado: usa alcance completo da arma (distancia real da origem).
            if (!isOrigin && dist != 1)
                continue;

            if (unique.Add(dist))
                outputDistances.Add(dist);
        }

        return outputDistances.Count > 0;
    }
    private static int ComputeHexDistance(Tilemap tilemap, Vector3Int fromCell, Vector3Int toCell, int maxSteps)
    {
        if (tilemap == null || maxSteps < 0)
            return -1;

        fromCell.z = 0;
        toCell.z = 0;
        if (fromCell == toCell)
            return 0;

        Dictionary<Vector3Int, int> distances = new Dictionary<Vector3Int, int>();
        Queue<Vector3Int> frontier = new Queue<Vector3Int>();
        List<Vector3Int> neighbors = new List<Vector3Int>(6);

        distances[fromCell] = 0;
        frontier.Enqueue(fromCell);

        while (frontier.Count > 0)
        {
            Vector3Int current = frontier.Dequeue();
            int currentDistance = distances[current];
            if (currentDistance >= maxSteps)
                continue;

            UnitMovementPathRules.GetImmediateHexNeighbors(tilemap, current, neighbors);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int next = neighbors[i];
                next.z = 0;
                if (distances.ContainsKey(next))
                    continue;

                int nextDistance = currentDistance + 1;
                distances[next] = nextDistance;
                if (next == toCell)
                    return nextDistance;
                frontier.Enqueue(next);
            }
        }

        return -1;
    }

    private static bool TryResolveOwnHqCell(TeamId team, out Vector3Int hqCell)
    {
        hqCell = default;
        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;
            if (c.TeamId != team)
                continue;
            if (!c.IsPlayerHeadQuarter)
                continue;

            hqCell = c.CurrentCellPosition;
            hqCell.z = 0;
            return true;
        }

        return false;
    }

    private static T FindFirstAsset<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
        }

        return null;
    }

    private struct AIDataResolution
    {
        public AIDatabase database;
        public AIGeneralProfile profile;
    }

    private struct CandidateEval
    {
        public UnitManager defenderUnit;
        public int rawDistance;
        public int effectiveDistance;
        public int attackerHpBefore;
        public int defenderHpBefore;
        public int defenderDistanceToOwnHq;
        public AICombatHpSimulator.AICombatHpResult simulation;
        public int attackerEliminated;
        public int defenderEliminated;
        public int score;
        public string reason;
    }
}





