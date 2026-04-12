using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AIShoppingSimulatorWindow : EditorWindow
{
    // ── contexto ──────────────────────────────────────────────────────────────
    [SerializeField] private MatchController matchController;
    [SerializeField] private AIPlayerController aiController;
    [SerializeField] private UnitSpawner unitSpawner;
    [SerializeField] private TeamId selectedTeam = TeamId.Green;
    [SerializeField] private bool defenseMode = false;

    // ── resultado ─────────────────────────────────────────────────────────────
    private AIShoppingTurnPlan lastPlan;
    private string statusMessage = "Pronto.";
    private int selectedDecisionIndex = -1;

    // ── scroll ────────────────────────────────────────────────────────────────
    private Vector2 windowScroll;
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    // ── estilos (lazy) ────────────────────────────────────────────────────────
    private GUIStyle _cardStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _dimLabelStyle;

    private GUIStyle CardStyle => _cardStyle ??= new GUIStyle("box")
        { padding = new RectOffset(6, 6, 4, 4) };

    private GUIStyle SectionStyle => _sectionStyle ??= new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 11 };

    private GUIStyle DimLabelStyle => _dimLabelStyle ??= new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.55f, 0.55f, 0.55f) } };

    private GUIStyle _wrapReasonStyle;
    private GUIStyle WrapReasonStyle => _wrapReasonStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
        wordWrap = true,
        normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
    };

    private void DrawWrappedReason(string label, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
        GUILayout.Label(text, WrapReasonStyle);
    }

    [MenuItem("Tools/AI/Simuladores/AI Pode Comprar")]
    public static void OpenWindow()
    {
        GetWindow<AIShoppingSimulatorWindow>("AI Pode Comprar");
    }

    private void OnEnable()
    {
        AutoDetectContext();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        DrawHeader();
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);

        if (lastPlan != null)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();

            // ── coluna esquerda: decisoes por construcao ──────────────────────
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(340f), GUILayout.ExpandWidth(true));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
            DrawDecisionList();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ── coluna direita: relatorio de bastidores ───────────────────────
            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(GUILayout.Width(380f));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            DrawReport();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(6f);

        bool canExecute = lastPlan != null && unitSpawner != null && matchController != null;
        using (new EditorGUI.DisabledScope(!canExecute))
        {
            if (GUILayout.Button("Executar Compras", GUILayout.Height(28f)))
                ExecuteShoppingPlan();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Atalhos de Turno", EditorStyles.miniLabel);
        using (new EditorGUI.DisabledScope(matchController == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Advance Turn"))
            {
                Undo.RecordObject(matchController, "Advance Turn");
                matchController.AdvanceTurn();
                ApplyEconomyToActivePlayer();
                EditorUtility.SetDirty(matchController);
                lastPlan = null;
            }
            if (GUILayout.Button("Emular Game Start"))
            {
                Undo.RecordObject(matchController, "Emular Game Start");
                ApplyGameStartEconomyToFirstPlayer();
                EditorUtility.SetDirty(matchController);
                lastPlan = null;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HEADER
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("AI Pode Comprar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Simula o plano de compras da IA para o time escolhido usando o estado atual da cena. " +
            "Roda Planner + Shopping e mostra decisoes por construcao e pressoes internas.",
            MessageType.Info);

        matchController = (MatchController)EditorGUILayout.ObjectField(
            "Match Controller", matchController, typeof(MatchController), true);
        aiController = (AIPlayerController)EditorGUILayout.ObjectField(
            "AI Controller", aiController, typeof(AIPlayerController), true);
        unitSpawner = (UnitSpawner)EditorGUILayout.ObjectField(
            "Unit Spawner", unitSpawner, typeof(UnitSpawner), true);

        selectedTeam = (TeamId)EditorGUILayout.EnumPopup("Time", selectedTeam);
        defenseMode = EditorGUILayout.Toggle("Modo Defesa", defenseMode);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular", GUILayout.Height(24f)))
            RunSimulation();
        EditorGUILayout.EndHorizontal();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  COLUNA ESQUERDA: lista de decisoes
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawDecisionList()
    {
        EditorGUILayout.LabelField(
            $"Decisoes por Construcao ({lastPlan.constructionDecisions.Count})", SectionStyle);

        if (lastPlan.constructionDecisions.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma decisao gerada.", MessageType.Info);
            return;
        }

        for (int i = 0; i < lastPlan.constructionDecisions.Count; i++)
        {
            AIShoppingConstructionDecision d = lastPlan.constructionDecisions[i];
            if (d == null) continue;

            bool isSelected = selectedDecisionIndex == i;
            Color bg = GetDecisionColor(d.kind);
            GUI.color = bg;
            EditorGUILayout.BeginVertical(CardStyle);
            GUI.color = Color.white;

            // toggle de selecao
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {d.constructionLabel}", "Button");
            if (toggled && selectedDecisionIndex != i) selectedDecisionIndex = i;

            EditorGUILayout.LabelField("Decisao", d.kind.ToString(), EditorStyles.boldLabel);

            if (d.kind == AIShoppingDecisionKind.Buy)
            {
                EditorGUILayout.LabelField("Unidade", string.IsNullOrWhiteSpace(d.plannedUnitId) ? "-" : d.plannedUnitId);
                EditorGUILayout.LabelField("Custo", d.cost.ToString());
                EditorGUILayout.LabelField("Capability", d.capability.ToString());
                EditorGUILayout.LabelField("Plano", string.IsNullOrWhiteSpace(d.planKey) ? "-" : d.planKey);
                if (d.usedSavingFallback)
                    EditorGUILayout.LabelField("Fallback", "sim", DimLabelStyle);
            }

            if (d.kind == AIShoppingDecisionKind.Blocked && d.blockingUnit != null)
                EditorGUILayout.LabelField("Bloqueado por", d.blockingUnit.name);

            if (!string.IsNullOrWhiteSpace(d.plannedReason))
                DrawWrappedReason("Motivo", d.plannedReason);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }

    private Color GetDecisionColor(AIShoppingDecisionKind kind) => kind switch
    {
        AIShoppingDecisionKind.Buy      => new Color(0.72f, 1f, 0.72f),
        AIShoppingDecisionKind.Save     => new Color(1f, 1f, 0.72f),
        AIShoppingDecisionKind.Blocked  => new Color(1f, 0.72f, 0.72f),
        AIShoppingDecisionKind.NoOffer  => new Color(0.85f, 0.85f, 0.85f),
        _                               => Color.white
    };

    // ─────────────────────────────────────────────────────────────────────────
    //  COLUNA DIREITA: relatorio
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawReport()
    {
        EditorGUILayout.LabelField("Relatorio de Bastidores", SectionStyle);

        DrawBudgetSection();
        EditorGUILayout.Space(6f);
        DrawPressureSection();
        EditorGUILayout.Space(6f);
        DrawOrdersSection();
        EditorGUILayout.Space(6f);
        DrawDemandsSection();
        EditorGUILayout.Space(6f);
        DrawStrategicSaveSection();
        EditorGUILayout.Space(6f);
        DrawSupplyChainSection();
    }

    private void DrawBudgetSection()
    {
        EditorGUILayout.LabelField("Orcamento", SectionStyle);
        var b = lastPlan.budget;
        EditorGUILayout.BeginVertical(CardStyle);
        EditorGUILayout.LabelField("Total",            b.totalMoney.ToString());
        EditorGUILayout.LabelField("Renda/turno",      b.incomePerTurn.ToString());
        EditorGUILayout.LabelField("Reservado",        b.reservedMoney.ToString());
        EditorGUILayout.LabelField("Reserva Estrat.",  b.strategicReserveMoney.ToString());
        EditorGUILayout.LabelField("Livre",            b.freeMoney.ToString());
        if (b.saveTargetMoney > 0)
            EditorGUILayout.LabelField("Meta Poupanca", b.saveTargetMoney.ToString());
        if (b.massFloorBlocked)
        {
            GUI.color = new Color(1f, 0.8f, 0.4f);
            EditorGUILayout.LabelField("Mass Floor", "BLOQUEADO");
            GUI.color = Color.white;
            DrawWrappedReason("Motivo", lastPlan.massFloorReason);
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawPressureSection()
    {
        EditorGUILayout.LabelField($"Pressoes por Capability ({lastPlan.capabilityPressures.Count})", SectionStyle);
        if (lastPlan.capabilityPressures.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma pressao calculada.", MessageType.Info);
            return;
        }

        for (int i = 0; i < lastPlan.capabilityPressures.Count; i++)
        {
            AIShoppingCapabilityPressure p = lastPlan.capabilityPressures[i];
            if (p == null) continue;

            EditorGUILayout.BeginVertical(CardStyle);
            EditorGUILayout.LabelField(p.capability.ToString(), EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Total Pressao",   p.totalPressure.ToString());
            EditorGUILayout.LabelField("Base",            p.basePressure.ToString());
            EditorGUILayout.LabelField("Missing",         p.missingPressure.ToString());
            EditorGUILayout.LabelField("Risk",            p.riskPressure.ToString());
            EditorGUILayout.LabelField("Critical",        p.criticalPressure.ToString());
            EditorGUILayout.LabelField("Dynamic",         p.dynamicPressure.ToString());
            EditorGUILayout.LabelField("Ordens",          p.orderCount.ToString());
            DrawWrappedReason("Criterios", p.criteriaSummary);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }

    private void DrawOrdersSection()
    {
        EditorGUILayout.LabelField($"Ordens de Compra ({lastPlan.orders.Count})", SectionStyle);
        if (lastPlan.orders.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma ordem gerada.", MessageType.Info);
            return;
        }

        for (int i = 0; i < lastPlan.orders.Count; i++)
        {
            AIShoppingOrder o = lastPlan.orders[i];
            if (o == null) continue;

            EditorGUILayout.BeginVertical(CardStyle);
            EditorGUILayout.LabelField($"{i + 1}. [{o.capability}] {o.planLabel}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Faltando",    o.remainingCount.ToString());
            EditorGUILayout.LabelField("Prioridade",  o.priorityScore.ToString());
            EditorGUILayout.LabelField("Critica",     o.critical ? "sim" : "nao");
            if (o.deferredByStrategicSave)
                EditorGUILayout.LabelField("Deferida", "strategic-save", DimLabelStyle);
            if (!string.IsNullOrWhiteSpace(o.reason))
                DrawWrappedReason("Motivo", o.reason);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }

    private void DrawDemandsSection()
    {
        EditorGUILayout.LabelField($"Demandas por Plano ({lastPlan.planDemands.Count})", SectionStyle);
        if (lastPlan.planDemands.Count == 0)
        {
            EditorGUILayout.HelpBox("Sem planos ativos (planner nao encontrou setores).", MessageType.Warning);
            return;
        }

        for (int i = 0; i < lastPlan.planDemands.Count; i++)
        {
            AIShoppingPlanDemand d = lastPlan.planDemands[i];
            if (d == null || d.missingCount == 0) continue;

            EditorGUILayout.BeginVertical(CardStyle);
            EditorGUILayout.LabelField($"{d.planLabel} / {d.capability}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Desejado",  d.desiredCount.ToString());
            EditorGUILayout.LabelField("Atribuido", d.assignedCount.ToString());
            EditorGUILayout.LabelField("Faltando",  d.missingCount.ToString());
            EditorGUILayout.LabelField("Risco",     d.tacticalRiskScore.ToString());
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }

    private void DrawStrategicSaveSection()
    {
        if (!lastPlan.strategicSaveActive) return;

        EditorGUILayout.LabelField("Poupanca Estrategica", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        GUI.color = new Color(1f, 1f, 0.6f);
        EditorGUILayout.LabelField("ATIVA", EditorStyles.boldLabel);
        GUI.color = Color.white;
        EditorGUILayout.LabelField("Unidade",      lastPlan.strategicSaveUnitId ?? "-");
        EditorGUILayout.LabelField("Origem",       lastPlan.strategicSaveSourceLabel ?? "-");
        EditorGUILayout.LabelField("Custo",        lastPlan.strategicSaveCost.ToString());
        EditorGUILayout.LabelField("Turnos",       lastPlan.strategicSaveTurnsToAfford.ToString());
        EditorGUILayout.LabelField("Ordens dif.", lastPlan.strategicSaveDeferredOrders.ToString());
        if (!string.IsNullOrWhiteSpace(lastPlan.strategicSaveReason))
            DrawWrappedReason("Motivo", lastPlan.strategicSaveReason);
        EditorGUILayout.EndVertical();
    }

    private void DrawSupplyChainSection()
    {
        if (!lastPlan.urgentLogisticsNeed && lastPlan.supplyChainPressureScore == 0) return;

        EditorGUILayout.LabelField("Supply Chain", SectionStyle);
        EditorGUILayout.BeginVertical(CardStyle);
        EditorGUILayout.LabelField("Pressao Score",       lastPlan.supplyChainPressureScore.ToString());
        EditorGUILayout.LabelField("Supridores Dispon.",  lastPlan.supplyChainAvailableSuppliers.ToString());
        EditorGUILayout.LabelField("Supridores Alvo",     lastPlan.supplyChainTargetSuppliers.ToString());
        EditorGUILayout.LabelField("Faltam Supridores",   lastPlan.supplyChainAdditionalSuppliersNeeded.ToString());
        EditorGUILayout.LabelField("Planos s/ logistica", lastPlan.supplyChainPlansMissingLogistics.ToString());
        EditorGUILayout.LabelField("Baixa Autonomia",     lastPlan.supplyChainLowAutonomyUnits.ToString());
        EditorGUILayout.LabelField("Sem Municao",         lastPlan.supplyChainOutOfAmmoUnits.ToString());
        EditorGUILayout.LabelField("Danificados",         lastPlan.supplyChainDamagedUnits.ToString());
        EditorGUILayout.LabelField("Frontline Criticos",  lastPlan.supplyChainFrontlineCriticalUnits.ToString());
        if (!string.IsNullOrWhiteSpace(lastPlan.supplyChainReason))
            DrawWrappedReason("Motivo", lastPlan.supplyChainReason);
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SIMULACAO
    // ─────────────────────────────────────────────────────────────────────────

    private void RunSimulation()
    {
        lastPlan = null;
        selectedDecisionIndex = -1;
        Repaint();

        // 1. garante AllActive em editor
        SyncEditorRegistries();

        // 2. contexto basico
        if (matchController == null)
        {
            statusMessage = "Erro: MatchController nao encontrado.";
            return;
        }

        // 3. snapshot (Planner precisa de planos ativos)
        AISnapshot snapshot = AISnapshot.Build(selectedTeam, matchController);
        if (snapshot == null)
        {
            statusMessage = "Erro: falha ao construir AISnapshot.";
            return;
        }

        // 4. roda o Planner para popular ActivePlans + UnitPlanAssignments
        List<AIPlanIntent> plans = AIPlanEvaluator.Evaluate(snapshot);
        snapshot.ActivePlans = plans ?? new List<AIPlanIntent>();
        if (plans != null)
        {
            foreach (AIPlanIntent intent in plans)
            {
                foreach (AIPlanAssignment a in intent.Assignments)
                {
                    snapshot.UnitPlanAssignments[a.UnitInstanceId] = a;
                    snapshot.UnitRoles[a.UnitInstanceId] = intent;
                }
            }
        }

        // 5. resolve AIData e modo
        AIData aiData = ResolveAIData();
        if (aiData == null)
        {
            statusMessage = "Erro: AIData nao encontrado para o time. Associe um AIPlayerController ou AIGeneralProfile na cena.";
            return;
        }

        AIDataMode mode = defenseMode ? aiData.defenseMode : aiData.attackMode;
        if (mode == null)
        {
            statusMessage = "Erro: AIDataMode nulo. Verifique o asset AIData.";
            return;
        }

        // 6. saldo e turno do MatchController
        int money      = matchController.GetActualMoney(selectedTeam);
        int income     = matchController.GetIncomePerTurn(selectedTeam);
        int turn       = matchController.CurrentTurn;

        // 7. BuildTurnPlan
        AIShoppingManager mgr = new AIShoppingManager();
        lastPlan = mgr.BuildTurnPlan(
            aiTeam:                        selectedTeam,
            snapshot:                      snapshot,
            mode:                          mode,
            defenseMode:                   defenseMode,
            currentMoney:                  money,
            incomePerTurn:                 income,
            currentTurn:                   turn,
            maxVariablePlans:              3,
            savingFallbackPurchasesThisTurn: 0);

        int buyCount  = 0;
        int saveCount = 0;
        int noneCount = 0;
        foreach (var d in lastPlan.constructionDecisions)
        {
            if (d == null) continue;
            if (d.kind == AIShoppingDecisionKind.Buy)  buyCount++;
            else if (d.kind == AIShoppingDecisionKind.Save) saveCount++;
            else noneCount++;
        }

        statusMessage =
            $"Turno {turn} | Time {selectedTeam} | Saldo {money} (+{income}/turno) | " +
            $"Planos: {snapshot.ActivePlans.Count} | " +
            $"Compras: {buyCount}  Poupando: {saveCount}  Sem decisao: {noneCount}";

        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    private AIData ResolveAIData()
    {
        if (aiController != null &&
            aiController.TryGetAssignedAIDataForTeam(selectedTeam, out AIData data, out _) &&
            data != null)
        {
            data.EnsureDefaults();
            return data;
        }

        // fallback: busca AIGeneralProfile nos assets para o time pedido
        string[] profileGuids = AssetDatabase.FindAssets("t:AIGeneralProfile");
        foreach (string guid in profileGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AIGeneralProfile p = AssetDatabase.LoadAssetAtPath<AIGeneralProfile>(path);
            if (p != null && p.aiData != null)
            {
                p.aiData.EnsureDefaults();
                return p.aiData;
            }
        }

        // fallback final: primeiro AIData disponivel nos assets
        string[] dataGuids = AssetDatabase.FindAssets("t:AIData");
        foreach (string guid in dataGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AIData d = AssetDatabase.LoadAssetAtPath<AIData>(path);
            if (d != null)
            {
                d.EnsureDefaults();
                return d;
            }
        }

        return null;
    }

    private static void SyncEditorRegistries()
    {
        if (Application.isPlaying)
            return;

        // UnitManager
        UnitManager.AllActive.Clear();
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (UnitManager u in units)
        {
            if (u != null && u.gameObject.activeInHierarchy)
                UnitManager.AllActive.Add(u);
        }

        // ConstructionManager
        ConstructionManager.AllActive.Clear();
        ConstructionManager[] constructions = Object.FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ConstructionManager c in constructions)
        {
            if (c != null && c.gameObject.activeInHierarchy)
                ConstructionManager.AllActive.Add(c);
        }
    }

    private void AutoDetectContext()
    {
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (aiController == null)
            aiController = Object.FindAnyObjectByType<AIPlayerController>();
        if (unitSpawner == null)
            unitSpawner = Object.FindAnyObjectByType<UnitSpawner>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  EXECUTAR COMPRAS
    // ─────────────────────────────────────────────────────────────────────────

    private void ExecuteShoppingPlan()
    {
        if (lastPlan == null || unitSpawner == null || matchController == null)
            return;

        int slotIndex = ResolveSlotForTeam(selectedTeam);

        int spawned  = 0;
        int skipped  = 0;
        int totalCost = 0;

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("AI Executar Compras");

        for (int i = 0; i < lastPlan.constructionDecisions.Count; i++)
        {
            AIShoppingConstructionDecision d = lastPlan.constructionDecisions[i];
            if (d == null || d.kind != AIShoppingDecisionKind.Buy)
                continue;
            if (d.construction == null || string.IsNullOrWhiteSpace(d.plannedUnitId))
            {
                skipped++;
                continue;
            }

            // verifica saldo antes de gastar
            if (!matchController.TrySpendActualMoney(selectedTeam, d.cost, out int remaining))
            {
                Debug.LogWarning($"[AI Comprar] Saldo insuficiente para {d.plannedUnitId} em {d.constructionLabel} (custo={d.cost} saldo={matchController.GetActualMoney(selectedTeam)})");
                skipped++;
                continue;
            }

            Vector3Int cell = d.construction.CurrentCellPosition;
            cell.z = 0;

            GameObject go = unitSpawner.SpawnAtCell(d.plannedUnitId, selectedTeam, cell);
            if (go == null)
            {
                // spawn falhou apos deduzir — devolve o dinheiro via SerializedObject
                RefundMoney(selectedTeam, d.cost);
                Debug.LogWarning($"[AI Comprar] Spawn falhou para {d.plannedUnitId} em {d.constructionLabel}");
                skipped++;
                continue;
            }

            // define slot do jogador na unidade spawned
            UnitManager unit = go.GetComponent<UnitManager>();
            if (unit != null && slotIndex >= 0)
            {
                SerializedObject so = new SerializedObject(unit);
                SerializedProperty slotProp = so.FindProperty("slotIndex");
                if (slotProp != null)
                {
                    slotProp.intValue = slotIndex;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "AI Comprar Unidade");
            if (go.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

            totalCost += d.cost;
            spawned++;

            Debug.Log($"[AI Comprar] {d.plannedUnitId} spawned em {cell} | custo={d.cost} | restante={remaining}");
        }

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.SetDirty(matchController);
        statusMessage = $"Executado: {spawned} unidade(s) comprada(s), {skipped} pulada(s). Gasto total: {totalCost}. Saldo: {matchController.GetActualMoney(selectedTeam)}";
        lastPlan = null; // força novo simular antes de executar de novo
        Repaint();
    }

    // TrySpendActualMoney nao aceita valores negativos para devolver dinheiro.
    // Usamos SerializedObject para escrever direto no actualMoney do player.
    private void RefundMoney(TeamId team, int amount)
    {
        if (matchController == null || amount <= 0)
            return;

        SerializedObject so = new SerializedObject(matchController);
        SerializedProperty playersProp = so.FindProperty("players");
        if (playersProp == null)
            return;

        so.Update();
        for (int i = 0; i < playersProp.arraySize; i++)
        {
            SerializedProperty player = playersProp.GetArrayElementAtIndex(i);
            SerializedProperty teamProp = player.FindPropertyRelative("teamId");
            if (teamProp == null || teamProp.intValue != (int)team)
                continue;

            SerializedProperty actualProp = player.FindPropertyRelative("actualMoney");
            if (actualProp != null)
                actualProp.intValue = Mathf.Max(0, actualProp.intValue + amount);
            break;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private int ResolveSlotForTeam(TeamId team)
    {
        if (matchController == null)
            return -1;

        int count = matchController.SlotCount;
        for (int i = 0; i < count; i++)
        {
            if (matchController.GetTeamIdForSlot(i) == team)
                return i;
        }

        return -1;
    }

    private void ApplyEconomyToActivePlayer()
    {
        if (matchController == null)
            return;

        SerializedObject so = new SerializedObject(matchController);
        so.Update();

        SerializedProperty activeTeamIdProp = so.FindProperty("activeTeamId");
        SerializedProperty playersProp      = so.FindProperty("players");
        if (activeTeamIdProp == null || playersProp == null)
            return;

        int activeId = activeTeamIdProp.intValue;

        for (int i = 0; i < playersProp.arraySize; i++)
        {
            SerializedProperty player        = playersProp.GetArrayElementAtIndex(i);
            SerializedProperty teamProp      = player.FindPropertyRelative("teamId");
            if (teamProp == null || teamProp.intValue != activeId)
                continue;

            SerializedProperty actualProp   = player.FindPropertyRelative("actualMoney");
            SerializedProperty incomeProp   = player.FindPropertyRelative("incomePerTurn");
            SerializedProperty startProp    = player.FindPropertyRelative("startMoney");
            SerializedProperty appliedProp  = player.FindPropertyRelative("startMoneyApplied");
            if (actualProp == null || incomeProp == null || startProp == null || appliedProp == null)
                break;

            int credit = Mathf.Max(0, incomeProp.intValue);
            if (!appliedProp.boolValue)
            {
                credit += Mathf.Max(0, startProp.intValue);
                appliedProp.boolValue = true;
            }

            actualProp.intValue = Mathf.Max(0, actualProp.intValue + credit);
            break;
        }

        so.ApplyModifiedProperties();
    }

    private void ApplyGameStartEconomyToFirstPlayer()
    {
        if (matchController == null)
            return;

        SerializedObject so = new SerializedObject(matchController);
        so.Update();

        SerializedProperty playersProp = so.FindProperty("players");
        if (playersProp == null || playersProp.arraySize == 0)
            return;

        SerializedProperty player       = playersProp.GetArrayElementAtIndex(0);
        SerializedProperty actualProp   = player.FindPropertyRelative("actualMoney");
        SerializedProperty incomeProp   = player.FindPropertyRelative("incomePerTurn");
        SerializedProperty startProp    = player.FindPropertyRelative("startMoney");
        SerializedProperty appliedProp  = player.FindPropertyRelative("startMoneyApplied");
        if (actualProp == null || incomeProp == null || startProp == null || appliedProp == null)
            return;

        int credit = Mathf.Max(0, incomeProp.intValue) + Mathf.Max(0, startProp.intValue);
        actualProp.intValue = Mathf.Max(0, actualProp.intValue + credit);
        appliedProp.boolValue = true;

        so.ApplyModifiedProperties();
    }
}
