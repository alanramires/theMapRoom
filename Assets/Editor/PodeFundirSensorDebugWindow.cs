using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeFundirSensorDebugWindow : EditorWindow
{
    private enum FusionLayerPlan
    {
        None = 0,
        DefaultSameDomain = 1,
        AirLow = 2,
        NavalSurface = 3,
        SubSubmerged = 4
    }

    private sealed class DebugMergeOrder
    {
        public UnitManager candidate;
        public string label;
    }

    private sealed class DebugIneligibleNeighbor
    {
        public UnitManager unit;
        public string reason;
        public Vector3Int cell;
        public int remainingMovement;
        public int requiredMovementCost;
    }

    [SerializeField] private UnitManager selectedUnit;
    [SerializeField] private Tilemap overrideTilemap;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private int simulatedRemainingMovement = -1;

    private readonly List<PodeFundirOption> eligibleNeighborOptions = new List<PodeFundirOption>();
    private readonly List<DebugIneligibleNeighbor> ineligibleNeighbors = new List<DebugIneligibleNeighbor>();
    private readonly List<DebugMergeOrder> mergeQueue = new List<DebugMergeOrder>();
    private string statusMessage = "Ready.";
    private string sensorReason = "Ready.";
    private string mergeQueueMessage = "Fila vazia.";
    private bool canMerge;
    private int eligibleCount;
    private Vector2 windowScroll;
    private int selectedNeighborIndex = -1;
    private int selectedIneligibleIndex = -1;
    private bool hasSelectedLine;
    private Vector3Int selectedLineStartCell;
    private Vector3Int selectedLineEndCell;
    private Color selectedLineColor = Color.cyan;
    private string selectedLineLabel = string.Empty;

    [MenuItem("Tools/Sensors/Pode Fundir")]
    public static void OpenWindow()
    {
        GetWindow<PodeFundirSensorDebugWindow>("Pode Fundir");
    }

    private void OnEnable()
    {
        AutoDetectContext();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        ClearSelectedLine();
    }

    private void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        EditorGUILayout.LabelField("Sensor Pode Fundir", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Regra atual:\n" +
            "1) Unidade selecionada\n" +
            "2) Nao embarcada\n" +
            "3) Pelo menos 1 unidade adjacente (1 hex) do mesmo tipo e mesmo time\n" +
            "4) Unidade selecionada precisa alcancar o local do candidato com movimento restante (caminhos validos)\n" +
            "5) Camada NAO bloqueia elegibilidade no sensor (troca de dominio/camada fica para a etapa de fusao/animação)",
            MessageType.Info);

        selectedUnit = (UnitManager)EditorGUILayout.ObjectField("Unidade", selectedUnit, typeof(UnitManager), true);
        overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap (opcional)", overrideTilemap, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField("Terrain Database", terrainDatabase, typeof(TerrainDatabase), false);
        DrawSimulatedMovementField();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();
        if (GUILayout.Button("Auto Detect"))
            AutoDetectContext();
        if (GUILayout.Button("Simular"))
            RunSimulation();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(statusMessage, MessageType.None);
        EditorGUILayout.HelpBox(mergeQueueMessage, MessageType.None);
        EditorGUILayout.LabelField("Pode Fundir", canMerge ? "SIM" : "NAO");
        EditorGUILayout.LabelField("Elegiveis adjacentes", eligibleCount.ToString());
        if (!string.IsNullOrWhiteSpace(sensorReason))
            EditorGUILayout.HelpBox($"Sensor: {sensorReason}", canMerge ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space(8f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.MinWidth(420f), GUILayout.ExpandWidth(true));
        DrawMergeQueueSection();
        EditorGUILayout.Space(8f);
        DrawEligibleNeighborsSection();
        EditorGUILayout.Space(8f);
        DrawIneligibleNeighborsSection();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(12f);
        EditorGUILayout.BeginVertical(GUILayout.Width(420f));
        DrawMergeComputationSection();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEligibleNeighborsSection()
    {
        EditorGUILayout.LabelField($"Unidades elegiveis ({eligibleNeighborOptions.Count})", EditorStyles.boldLabel);
        if (eligibleNeighborOptions.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma unidade elegivel adjacente.", MessageType.Info);
            return;
        }

        for (int i = 0; i < eligibleNeighborOptions.Count; i++)
        {
            PodeFundirOption option = eligibleNeighborOptions[i];
            UnitManager unit = option != null ? option.candidateUnit : null;
            if (option == null || unit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedNeighborIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {unit.name}", "Button");
            if (toggled && selectedNeighborIndex != i)
            {
                selectedNeighborIndex = i;
                selectedIneligibleIndex = -1;
                SelectLineForDrawing(unit);
            }
            EditorGUILayout.LabelField("UnitId", string.IsNullOrWhiteSpace(unit.UnitId) ? "-" : unit.UnitId);
            EditorGUILayout.LabelField("HP", unit.CurrentHP.ToString());
            EditorGUILayout.LabelField("Camada", $"{unit.GetDomain()}/{unit.GetHeightLevel()}");
            EditorGUILayout.LabelField("Mov. restante do receptor", option.remainingMovement.ToString());
            if (option.requiredMovementCost > 0)
                EditorGUILayout.LabelField("Custo para alvo", option.requiredMovementCost.ToString());
            Vector3Int cell = unit.CurrentCellPosition;
            EditorGUILayout.LabelField("Hex", $"{cell.x},{cell.y}");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Desenhar Linha no Scene View"))
            {
                selectedNeighborIndex = i;
                selectedIneligibleIndex = -1;
                SelectLineForDrawing(unit);
            }
            if (GUILayout.Button("Adicionar na Fila"))
                TryAddCandidateToQueue(unit);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawIneligibleNeighborsSection()
    {
        EditorGUILayout.LabelField($"Unidades nao elegiveis ({ineligibleNeighbors.Count})", EditorStyles.boldLabel);
        if (ineligibleNeighbors.Count == 0)
        {
            EditorGUILayout.HelpBox("Nenhuma unidade adjacente nao elegivel encontrada.", MessageType.Info);
            return;
        }

        for (int i = 0; i < ineligibleNeighbors.Count; i++)
        {
            DebugIneligibleNeighbor item = ineligibleNeighbors[i];
            if (item == null || item.unit == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            bool isSelected = selectedIneligibleIndex == i;
            bool toggled = GUILayout.Toggle(isSelected, $"{i + 1}. {item.unit.name}", "Button");
            if (toggled && selectedIneligibleIndex != i)
            {
                selectedIneligibleIndex = i;
                selectedNeighborIndex = -1;
                SelectIneligibleLineForDrawing(item);
            }
            EditorGUILayout.LabelField("UnitId", string.IsNullOrWhiteSpace(item.unit.UnitId) ? "-" : item.unit.UnitId);
            EditorGUILayout.LabelField("HP", item.unit.CurrentHP.ToString());
            EditorGUILayout.LabelField("Camada", $"{item.unit.GetDomain()}/{item.unit.GetHeightLevel()}");
            EditorGUILayout.LabelField("Mov. restante do receptor", item.remainingMovement.ToString());
            if (item.requiredMovementCost > 0)
                EditorGUILayout.LabelField("Custo para alvo", item.requiredMovementCost.ToString());
            EditorGUILayout.LabelField("Hex", $"{item.cell.x},{item.cell.y}");
            EditorGUILayout.LabelField("Motivo", string.IsNullOrWhiteSpace(item.reason) ? "-" : item.reason);
            if (GUILayout.Button("Desenhar Linha Vermelha"))
            {
                selectedIneligibleIndex = i;
                selectedNeighborIndex = -1;
                SelectIneligibleLineForDrawing(item);
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void RunSimulation()
    {
        SyncEditorUnitRegistryForSensors();

        eligibleNeighborOptions.Clear();
        ineligibleNeighbors.Clear();
        selectedNeighborIndex = -1;
        selectedIneligibleIndex = -1;
        ClearSelectedLine();
        canMerge = false;
        eligibleCount = 0;
        sensorReason = string.Empty;

        if (selectedUnit == null)
        {
            statusMessage = "Selecione uma unidade valida.";
            return;
        }

        Tilemap map = ResolveTilemap();
        if (map == null)
        {
            statusMessage = "Tilemap base nao encontrado.";
            return;
        }

        RebuildNeighborLists(map);

        statusMessage = canMerge
            ? $"Sensor TRUE. {eligibleCount} unidade(s) adjacente(s) elegivel(is)."
            : "Sensor FALSE. Fusao indisponivel.";

        if (eligibleNeighborOptions.Count > 0)
        {
            selectedNeighborIndex = 0;
            SelectLineForDrawing(eligibleNeighborOptions[0].candidateUnit);
        }

        Debug.Log(
            $"[PodeFundirSensorDebug] unit={(selectedUnit != null ? selectedUnit.name : "(null)")} | " +
            $"simRemaining={ResolveSimulatedRemainingMovement()} | canMerge={canMerge} | eligible={eligibleCount} | reason={sensorReason}");
    }

    private void TryUseCurrentSelection()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null)
            return;

        UnitManager unit = go.GetComponent<UnitManager>();
        if (unit == null)
            unit = go.GetComponentInParent<UnitManager>();
        if (unit != null)
        {
            selectedUnit = unit;
            overrideTilemap = selectedUnit.BoardTilemap != null ? selectedUnit.BoardTilemap : FindPreferredTilemap();
            if (terrainDatabase == null)
                terrainDatabase = FindFirstTerrainDatabaseAsset();
            PrefillSimulatedMovementFromSelectedUnit();
        }
    }

    private void AutoDetectContext()
    {
        if (selectedUnit == null)
            selectedUnit = FindAnyObjectByType<TurnStateManager>()?.SelectedUnit;
        if (selectedUnit == null)
            TryUseCurrentSelection();
        overrideTilemap = selectedUnit != null && selectedUnit.BoardTilemap != null
            ? selectedUnit.BoardTilemap
            : FindPreferredTilemap();
        terrainDatabase ??= FindFirstTerrainDatabaseAsset();
        if (selectedUnit != null && simulatedRemainingMovement < 0)
            PrefillSimulatedMovementFromSelectedUnit();
    }

    private Tilemap ResolveTilemap()
    {
        if (overrideTilemap != null)
            return overrideTilemap;
        if (selectedUnit != null && selectedUnit.BoardTilemap != null)
            return selectedUnit.BoardTilemap;
        return FindPreferredTilemap();
    }

    private void DrawSimulatedMovementField()
    {
        int displayedMax = selectedUnit != null ? Mathf.Max(0, selectedUnit.MaxMovementPoints) : 0;
        int displayedCurrent = selectedUnit != null ? Mathf.Max(0, selectedUnit.RemainingMovementPoints) : 0;
        int displayedValue = ResolveSimulatedRemainingMovement();

        EditorGUILayout.BeginHorizontal();
        int newValue = EditorGUILayout.IntField("Mov. restante simulado", displayedValue);
        if (GUILayout.Button("Usar Atual", GUILayout.Width(90f)))
            newValue = displayedCurrent;
        if (GUILayout.Button("Usar Max", GUILayout.Width(90f)))
            newValue = displayedMax;
        EditorGUILayout.EndHorizontal();

        simulatedRemainingMovement = Mathf.Max(0, newValue);

        if (selectedUnit != null)
            EditorGUILayout.LabelField("Movimento real", $"{displayedCurrent}/{displayedMax}");
    }

    private void PrefillSimulatedMovementFromSelectedUnit()
    {
        simulatedRemainingMovement = selectedUnit != null ? Mathf.Max(0, selectedUnit.MaxMovementPoints) : 0;
    }

    private int ResolveSimulatedRemainingMovement()
    {
        if (selectedUnit == null)
            return Mathf.Max(0, simulatedRemainingMovement);

        int max = Mathf.Max(0, selectedUnit.MaxMovementPoints);
        if (simulatedRemainingMovement < 0)
            return max;
        return Mathf.Clamp(simulatedRemainingMovement, 0, max);
    }

    private void DrawMergeQueueSection()
    {
        EditorGUILayout.LabelField($"Fila de Fusao (Debug) ({mergeQueue.Count}/1)", EditorStyles.boldLabel);
        if (selectedUnit != null)
            EditorGUILayout.LabelField("Recebedor Camada", $"{selectedUnit.GetDomain()}/{selectedUnit.GetHeightLevel()}");
        if (mergeQueue.Count == 0)
        {
            EditorGUILayout.HelpBox("Fila vazia. Clique em \"Adicionar na Fila\" em uma unidade elegivel.", MessageType.Info);
            return;
        }

        for (int i = 0; i < mergeQueue.Count; i++)
        {
            DebugMergeOrder order = mergeQueue[i];
            if (order == null || order.candidate == null)
                continue;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{i + 1}. {order.label}");
            EditorGUILayout.LabelField("Camada", $"{order.candidate.GetDomain()}/{order.candidate.GetHeightLevel()}");
            if (GUILayout.Button("Remover da Fila"))
            {
                UnitManager removed = order.candidate;
                mergeQueue.RemoveAt(i);
                mergeQueueMessage = removed != null ? $"Candidato removido da fila: {removed.name}." : "Candidato removido da fila.";
                RebuildNeighborLists(ResolveTilemap());
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndVertical();
        }
    }

    private void DrawMergeComputationSection()
    {
        EditorGUILayout.LabelField("Previa da Fusao", EditorStyles.boldLabel);
        if (selectedUnit == null)
        {
            EditorGUILayout.HelpBox("Selecione uma unidade base para calcular a fusao.", MessageType.Info);
            using (new EditorGUI.DisabledScope(true))
                GUILayout.Button("FUNDIR");
            return;
        }

        ComputeMergePreview(
            selectedUnit,
            mergeQueue,
            out int baseHp,
            out int baseAutonomy,
            out int baseSteps,
            out int participantsHp,
            out int participantsSteps,
            out int resultHp,
            out int resultAutonomy);

        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Unidade antes");
        EditorGUILayout.LabelField($"HP: {baseHp}");
        EditorGUILayout.LabelField($"Camada: {selectedUnit.GetDomain()}/{selectedUnit.GetHeightLevel()}");
        EditorGUILayout.LabelField($"Autonomia: {baseAutonomy}  ({baseSteps} passos: {baseHp}x{baseAutonomy})");
        EditorGUILayout.LabelField("Armas embarcadas");
        DrawUnitWeaponsContribution(selectedUnit, baseHp);
        EditorGUILayout.LabelField("Suprimentos embarcados");
        DrawUnitSuppliesContribution(selectedUnit, baseHp);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Unidades participantes");

        if (mergeQueue.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhuma)");
        }
        else
        {
            for (int i = 0; i < mergeQueue.Count; i++)
            {
                DebugMergeOrder order = mergeQueue[i];
                if (order == null || order.candidate == null)
                    continue;

                int hp = Mathf.Max(0, order.candidate.CurrentHP);
                int autonomy = Mathf.Max(0, order.candidate.CurrentFuel);
                int steps = hp * autonomy;
                EditorGUILayout.LabelField($"{order.candidate.name}");
                EditorGUILayout.LabelField($"HP: {hp}");
                EditorGUILayout.LabelField($"Camada: {order.candidate.GetDomain()}/{order.candidate.GetHeightLevel()}");
                EditorGUILayout.LabelField($"Autonomia: {autonomy}  ({steps} passos: {hp}x{autonomy})");
                EditorGUILayout.LabelField("Armas embarcadas");
                DrawUnitWeaponsContribution(order.candidate, hp);
                EditorGUILayout.LabelField("Suprimentos embarcados");
                DrawUnitSuppliesContribution(order.candidate, hp);
                EditorGUILayout.Space(2f);
            }
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Unidade resultante");
        int totalSteps = baseSteps + participantsSteps;
        float exactAutonomy = resultHp > 0 ? (float)totalSteps / resultHp : 0f;
        EditorGUILayout.LabelField($"HP: {resultHp}");
        if (resultHp > 0)
        EditorGUILayout.LabelField($"Autonomia: {resultAutonomy}  ({baseSteps}+{participantsSteps} / {resultHp} = {exactAutonomy:0.##} = {resultAutonomy})");
        else
            EditorGUILayout.LabelField($"Autonomia: {resultAutonomy}  (HP resultante zero)");
        EditorGUILayout.LabelField("Armas embarcadas");
        DrawMergedWeaponsContribution(selectedUnit, mergeQueue, resultHp);
        EditorGUILayout.LabelField("Suprimentos embarcados");
        DrawMergedSuppliesContribution(selectedUnit, mergeQueue, resultHp);
        Tilemap map = ResolveTilemap();
        FusionLayerPlan layerPlan = ResolveFusionLayerPlan(selectedUnit, mergeQueue, map, terrainDatabase, out string observation);
        EditorGUILayout.HelpBox($"OBS: {observation}", MessageType.Info);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ToggleLeft("Default Fusion (Same domain)", layerPlan == FusionLayerPlan.DefaultSameDomain);
            EditorGUILayout.ToggleLeft("Air / Low", layerPlan == FusionLayerPlan.AirLow);
            EditorGUILayout.ToggleLeft("Naval / Surface", layerPlan == FusionLayerPlan.NavalSurface);
            EditorGUILayout.ToggleLeft("Sub/Submerged", layerPlan == FusionLayerPlan.SubSubmerged);
        }
        EditorGUILayout.EndVertical();

        bool canMergeNow = selectedUnit != null && mergeQueue.Count > 0;
        using (new EditorGUI.DisabledScope(!canMergeNow))
        {
            if (GUILayout.Button("FUNDIR"))
                ExecuteDebugMerge(resultHp, resultAutonomy);
        }
    }

    private static void ComputeMergePreview(List<DebugMergeOrder> queue, out int participantsHp, out int participantsSteps)
    {
        participantsHp = 0;
        participantsSteps = 0;
        if (queue == null)
            return;

        for (int i = 0; i < queue.Count; i++)
        {
            DebugMergeOrder order = queue[i];
            if (order == null || order.candidate == null)
                continue;
            int hp = Mathf.Max(0, order.candidate.CurrentHP);
            int autonomy = Mathf.Max(0, order.candidate.CurrentFuel);
            participantsHp += hp;
            participantsSteps += hp * autonomy;
        }
    }

    private static void ComputeMergePreview(
        UnitManager baseUnit,
        List<DebugMergeOrder> queue,
        out int baseHp,
        out int baseAutonomy,
        out int baseSteps,
        out int participantsHp,
        out int participantsSteps,
        out int resultHp,
        out int resultAutonomy)
    {
        baseHp = baseUnit != null ? Mathf.Max(0, baseUnit.CurrentHP) : 0;
        baseAutonomy = baseUnit != null ? Mathf.Max(0, baseUnit.CurrentFuel) : 0;
        baseSteps = baseHp * baseAutonomy;
        ComputeMergePreview(queue, out participantsHp, out participantsSteps);
        resultHp = Mathf.Min(10, baseHp + participantsHp);
        int totalSteps = baseSteps + participantsSteps;
        resultAutonomy = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;
    }

    private void ExecuteDebugMerge(int resultHp, int resultAutonomy)
    {
        if (selectedUnit == null)
            return;

        Dictionary<WeaponData, int> projectilesByWeapon = BuildMergeWeaponProjectileTotals(selectedUnit, mergeQueue);
        Dictionary<SupplyData, int> supplyStepsByType = BuildMergeSupplyStepTotals(selectedUnit, mergeQueue);

        Undo.RecordObject(selectedUnit, "Pode Fundir (Debug)");
        selectedUnit.SetCurrentHP(resultHp);
        selectedUnit.SetCurrentFuel(resultAutonomy);

        int skippedWeapons = ApplyMergedWeaponAmmunitionToBaseUnit(selectedUnit, projectilesByWeapon, resultHp);
        int skippedSupplies = ApplyMergedSupplyAmountsToBaseUnit(selectedUnit, supplyStepsByType, resultHp);

        EditorUtility.SetDirty(selectedUnit);
        if (selectedUnit.gameObject != null && selectedUnit.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(selectedUnit.gameObject.scene);

        statusMessage = (skippedWeapons > 0 || skippedSupplies > 0)
            ? $"Fusao debug aplicada: {selectedUnit.name} HP {selectedUnit.CurrentHP}, Autonomia {selectedUnit.CurrentFuel}. Armas sem slot: {skippedWeapons}. Suprimentos sem slot: {skippedSupplies}."
            : $"Fusao debug aplicada: {selectedUnit.name} agora tem HP {selectedUnit.CurrentHP} e Autonomia {selectedUnit.CurrentFuel}.";
        mergeQueueMessage = "Participantes mantidos no mapa (sem remocao, conforme solicitado).";
        Repaint();
    }

    private static void DrawUnitWeaponsContribution(UnitManager unit, int hp)
    {
        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit != null ? unit.GetEmbarkedWeapons() : null;
        if (weapons == null || weapons.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhuma)");
            return;
        }

        bool any = false;
        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            any = true;
            int ammo = Mathf.Max(0, embarked.squadAmmunition);
            int projectiles = ammo * Mathf.Max(0, hp);
            EditorGUILayout.LabelField($"{ResolveWeaponName(embarked.weapon)}: {ammo}  ({projectiles} projeteis: {ammo}x{hp})");
        }

        if (!any)
            EditorGUILayout.LabelField("(nenhuma)");
    }

    private static void DrawMergedWeaponsContribution(UnitManager baseUnit, List<DebugMergeOrder> queue, int resultHp)
    {
        Dictionary<WeaponData, int> totals = BuildMergeWeaponProjectileTotals(baseUnit, queue);
        if (totals.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhuma)");
            return;
        }

        List<WeaponData> order = BuildMergedWeaponDisplayOrder(baseUnit, queue, totals);
        for (int i = 0; i < order.Count; i++)
        {
            WeaponData weapon = order[i];
            if (weapon == null || !totals.TryGetValue(weapon, out int totalProjectiles))
                continue;

            int resultAmmo = resultHp > 0 ? Mathf.Max(0, totalProjectiles / resultHp) : 0;
            float exact = resultHp > 0 ? (float)totalProjectiles / resultHp : 0f;
            if (resultHp > 0)
                EditorGUILayout.LabelField($"{ResolveWeaponName(weapon)}: {resultAmmo}  ({totalProjectiles} / {resultHp} = {exact:0.##} = {resultAmmo})");
            else
                EditorGUILayout.LabelField($"{ResolveWeaponName(weapon)}: {resultAmmo}  (HP resultante zero)");
        }
    }

    private static void DrawUnitSuppliesContribution(UnitManager unit, int hp)
    {
        IReadOnlyList<UnitEmbarkedSupply> supplies = unit != null ? unit.GetEmbarkedResources() : null;
        if (supplies == null || supplies.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhum)");
            return;
        }

        bool any = false;
        for (int i = 0; i < supplies.Count; i++)
        {
            UnitEmbarkedSupply embarked = supplies[i];
            if (embarked == null || embarked.supply == null)
                continue;

            any = true;
            int amount = Mathf.Max(0, embarked.amount);
            int steps = amount * Mathf.Max(0, hp);
            EditorGUILayout.LabelField($"{ResolveSupplyName(embarked.supply)}: {amount}  ({steps}: {amount}x{hp})");
        }

        if (!any)
            EditorGUILayout.LabelField("(nenhum)");
    }

    private static void DrawMergedSuppliesContribution(UnitManager baseUnit, List<DebugMergeOrder> queue, int resultHp)
    {
        Dictionary<SupplyData, int> totals = BuildMergeSupplyStepTotals(baseUnit, queue);
        if (totals.Count == 0)
        {
            EditorGUILayout.LabelField("(nenhum)");
            return;
        }

        List<SupplyData> order = BuildMergedSupplyDisplayOrder(baseUnit, queue, totals);
        for (int i = 0; i < order.Count; i++)
        {
            SupplyData supply = order[i];
            if (supply == null || !totals.TryGetValue(supply, out int totalSteps))
                continue;

            int resultAmount = resultHp > 0 ? Mathf.Max(0, totalSteps / resultHp) : 0;
            float exact = resultHp > 0 ? (float)totalSteps / resultHp : 0f;
            if (resultHp > 0)
                EditorGUILayout.LabelField($"{ResolveSupplyName(supply)}: {resultAmount}  ({totalSteps} / {resultHp} = {exact:0.##} = {resultAmount})");
            else
                EditorGUILayout.LabelField($"{ResolveSupplyName(supply)}: {resultAmount}  (HP resultante zero)");
        }
    }

    private static Dictionary<WeaponData, int> BuildMergeWeaponProjectileTotals(UnitManager baseUnit, List<DebugMergeOrder> queue)
    {
        Dictionary<WeaponData, int> totals = new Dictionary<WeaponData, int>();
        AccumulateUnitWeaponProjectiles(baseUnit, baseUnit != null ? Mathf.Max(0, baseUnit.CurrentHP) : 0, totals);

        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                DebugMergeOrder order = queue[i];
                if (order == null || order.candidate == null)
                    continue;

                int hp = Mathf.Max(0, order.candidate.CurrentHP);
                AccumulateUnitWeaponProjectiles(order.candidate, hp, totals);
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

    private static List<WeaponData> BuildMergedWeaponDisplayOrder(UnitManager baseUnit, List<DebugMergeOrder> queue, Dictionary<WeaponData, int> totals)
    {
        List<WeaponData> order = new List<WeaponData>();
        if (totals == null)
            return order;

        AppendWeaponOrderFromUnit(baseUnit, totals, order);
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                DebugMergeOrder orderItem = queue[i];
                if (orderItem == null || orderItem.candidate == null)
                    continue;
                AppendWeaponOrderFromUnit(orderItem.candidate, totals, order);
            }
        }

        // Fallback para qualquer arma que nao entrou na ordem por algum caso de runtime.
        foreach (WeaponData weapon in totals.Keys)
        {
            if (weapon != null && !order.Contains(weapon))
                order.Add(weapon);
        }

        return order;
    }

    private static void AppendWeaponOrderFromUnit(UnitManager unit, Dictionary<WeaponData, int> totals, List<WeaponData> order)
    {
        if (unit == null || totals == null || order == null)
            return;

        IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = weapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;
            if (!totals.ContainsKey(embarked.weapon))
                continue;
            if (order.Contains(embarked.weapon))
                continue;
            order.Add(embarked.weapon);
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

    private static Dictionary<SupplyData, int> BuildMergeSupplyStepTotals(UnitManager baseUnit, List<DebugMergeOrder> queue)
    {
        Dictionary<SupplyData, int> totals = new Dictionary<SupplyData, int>();
        AccumulateUnitSupplySteps(baseUnit, baseUnit != null ? Mathf.Max(0, baseUnit.CurrentHP) : 0, totals);

        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                DebugMergeOrder order = queue[i];
                if (order == null || order.candidate == null)
                    continue;
                AccumulateUnitSupplySteps(order.candidate, Mathf.Max(0, order.candidate.CurrentHP), totals);
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

    private static List<SupplyData> BuildMergedSupplyDisplayOrder(UnitManager baseUnit, List<DebugMergeOrder> queue, Dictionary<SupplyData, int> totals)
    {
        List<SupplyData> order = new List<SupplyData>();
        if (totals == null)
            return order;

        AppendSupplyOrderFromUnit(baseUnit, totals, order);
        if (queue != null)
        {
            for (int i = 0; i < queue.Count; i++)
            {
                DebugMergeOrder item = queue[i];
                if (item == null || item.candidate == null)
                    continue;
                AppendSupplyOrderFromUnit(item.candidate, totals, order);
            }
        }

        foreach (SupplyData supply in totals.Keys)
        {
            if (supply != null && !order.Contains(supply))
                order.Add(supply);
        }

        return order;
    }

    private static void AppendSupplyOrderFromUnit(UnitManager unit, Dictionary<SupplyData, int> totals, List<SupplyData> order)
    {
        if (unit == null || totals == null || order == null)
            return;

        IReadOnlyList<UnitEmbarkedSupply> supplies = unit.GetEmbarkedResources();
        if (supplies == null)
            return;

        for (int i = 0; i < supplies.Count; i++)
        {
            UnitEmbarkedSupply embarked = supplies[i];
            if (embarked == null || embarked.supply == null)
                continue;
            if (!totals.ContainsKey(embarked.supply))
                continue;
            if (order.Contains(embarked.supply))
                continue;
            order.Add(embarked.supply);
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

    private static string ResolveWeaponName(WeaponData weapon)
    {
        if (weapon == null)
            return "(arma desconhecida)";

        if (!string.IsNullOrWhiteSpace(weapon.displayName))
            return weapon.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(weapon.id))
            return weapon.id.Trim();
        return weapon.name;
    }

    private static string ResolveSupplyName(SupplyData supply)
    {
        if (supply == null)
            return "(suprimento desconhecido)";
        if (!string.IsNullOrWhiteSpace(supply.displayName))
            return supply.displayName.Trim();
        if (!string.IsNullOrWhiteSpace(supply.id))
            return supply.id.Trim();
        return supply.name;
    }

    private bool IsCandidateQueued(UnitManager candidate)
    {
        if (candidate == null)
            return false;

        for (int i = 0; i < mergeQueue.Count; i++)
        {
            DebugMergeOrder order = mergeQueue[i];
            if (order == null || order.candidate == null)
                continue;
            if (order.candidate == candidate)
                return true;
        }

        return false;
    }

    private void TryAddCandidateToQueue(UnitManager candidate)
    {
        if (candidate == null)
            return;

        if (IsCandidateQueued(candidate))
        {
            mergeQueueMessage = $"{candidate.name} ja esta na fila.";
            return;
        }

        if (mergeQueue.Count > 0)
        {
            DebugMergeOrder current = mergeQueue[0];
            string currentName = current != null && current.candidate != null ? current.candidate.name : "outro candidato";
            mergeQueueMessage = $"A fila de fusao aceita apenas 1 candidato por vez. Remova {currentName} antes de adicionar outro.";
            return;
        }

        mergeQueue.Add(new DebugMergeOrder
        {
            candidate = candidate,
            label = $"{candidate.name} (HP {candidate.CurrentHP})"
        });
        mergeQueueMessage = $"Candidato adicionado: {candidate.name}.";
        RebuildNeighborLists(ResolveTilemap());
    }

    private void RemoveCandidateFromQueue(UnitManager candidate)
    {
        if (candidate == null)
            return;

        for (int i = mergeQueue.Count - 1; i >= 0; i--)
        {
            DebugMergeOrder order = mergeQueue[i];
            if (order == null || order.candidate == null)
                continue;
            if (order.candidate != candidate)
                continue;

            mergeQueue.RemoveAt(i);
            mergeQueueMessage = $"Candidato removido: {candidate.name}.";
            RebuildNeighborLists(ResolveTilemap());
            return;
        }
    }

    private void SyncQueueWithEligibleNeighbors()
    {
        for (int i = mergeQueue.Count - 1; i >= 0; i--)
        {
            DebugMergeOrder order = mergeQueue[i];
            if (order == null || order.candidate == null)
            {
                mergeQueue.RemoveAt(i);
                continue;
            }

            bool stillEligible = false;
            for (int j = 0; j < eligibleNeighborOptions.Count; j++)
            {
                PodeFundirOption option = eligibleNeighborOptions[j];
                if (option != null && option.candidateUnit == order.candidate)
                {
                    stillEligible = true;
                    break;
                }
            }

            if (!stillEligible)
                mergeQueue.RemoveAt(i);
        }

        if (mergeQueue.Count == 0)
            mergeQueueMessage = "Fila vazia.";
    }

    private void RebuildNeighborLists(Tilemap map)
    {
        SyncEditorUnitRegistryForSensors();

        eligibleNeighborOptions.Clear();
        ineligibleNeighbors.Clear();
        selectedNeighborIndex = -1;
        selectedIneligibleIndex = -1;

        if (selectedUnit == null || map == null)
        {
            canMerge = false;
            eligibleCount = 0;
            sensorReason = "Sem contexto valido.";
            ClearSelectedLine();
            return;
        }

        List<PodeFundirOption> validOptions = new List<PodeFundirOption>();
        List<PodeFundirInvalidOption> invalidOptions = new List<PodeFundirInvalidOption>();
        int simulatedMovement = ResolveSimulatedRemainingMovement();
        bool sensorStatus;
        string sensorCollectedReason;
        sensorStatus = PodeFundirSensor.CollectOptions(
            selectedUnit,
            map,
            terrainDatabase,
            simulatedMovement,
            validOptions,
            out sensorCollectedReason,
            invalidOptions);

        if (validOptions.Count == 0 && invalidOptions.Count == 0)
            RebuildNeighborListsFallbackFromAdjacentUnits(map, simulatedMovement, validOptions, invalidOptions);

        for (int i = 0; i < validOptions.Count; i++)
        {
            PodeFundirOption option = validOptions[i];
            if (option == null || option.candidateUnit == null)
                continue;
            eligibleNeighborOptions.Add(option);
        }

        for (int i = 0; i < invalidOptions.Count; i++)
        {
            PodeFundirInvalidOption invalid = invalidOptions[i];
            if (invalid == null || invalid.candidateUnit == null)
                continue;

            ineligibleNeighbors.Add(new DebugIneligibleNeighbor
            {
                unit = invalid.candidateUnit,
                reason = invalid.reason,
                cell = invalid.candidateCell,
                remainingMovement = invalid.remainingMovement,
                requiredMovementCost = invalid.requiredMovementCost
            });
        }

        SyncQueueWithEligibleNeighbors();

        // Remove da lista de elegiveis os candidatos que ja estao na fila.
        for (int i = eligibleNeighborOptions.Count - 1; i >= 0; i--)
        {
            PodeFundirOption option = eligibleNeighborOptions[i];
            if (option == null || IsCandidateQueued(option.candidateUnit))
                eligibleNeighborOptions.RemoveAt(i);
        }

        eligibleCount = eligibleNeighborOptions.Count;
        canMerge = eligibleNeighborOptions.Count > 0;

        if (eligibleNeighborOptions.Count > 0)
        {
            sensorReason = string.IsNullOrWhiteSpace(sensorCollectedReason)
                ? $"Encontrados {eligibleNeighborOptions.Count} candidato(s) valido(s) para fusao."
                : sensorCollectedReason;
        }
        else if (ineligibleNeighbors.Count > 0)
        {
            string firstInvalidReason = ineligibleNeighbors[0] != null && !string.IsNullOrWhiteSpace(ineligibleNeighbors[0].reason)
                ? ineligibleNeighbors[0].reason
                : "Sem candidatos validos para fusao.";
            sensorReason = $"Sem candidatos validos para fusao. {firstInvalidReason}";
        }
        else
        {
            sensorReason = string.IsNullOrWhiteSpace(sensorCollectedReason)
                ? "Sem unidade adjacente (1 hex) do mesmo tipo para fundir."
                : sensorCollectedReason;
        }

        if (eligibleNeighborOptions.Count > 0)
        {
            selectedNeighborIndex = 0;
            selectedIneligibleIndex = -1;
            SelectLineForDrawing(eligibleNeighborOptions[0].candidateUnit);
        }
        else
        {
            ClearSelectedLine();
        }
    }

    private static void SyncEditorUnitRegistryForSensors()
    {
        if (Application.isPlaying)
            return;

        UnitManager.AllActive.Clear();
        UnitManager[] units = Object.FindObjectsByType<UnitManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (units == null || units.Length == 0)
            return;

        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy)
                continue;

            UnitManager.AllActive.Add(unit);
        }
    }

    private void RebuildNeighborListsFallbackFromAdjacentUnits(
        Tilemap map,
        int simulatedMovement,
        List<PodeFundirOption> validOptions,
        List<PodeFundirInvalidOption> invalidOptions)
    {
        if (selectedUnit == null || map == null)
            return;

        Vector3Int origin = selectedUnit.CurrentCellPosition;
        origin.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);
        Debug.Log(
            $"[PodeFundirSensorDebug][FALLBACK] selected={selectedUnit.name} " +
            $"origin={origin.x},{origin.y} simRemaining={simulatedMovement} " +
            $"map={(map != null ? map.name : "(null)")} " +
            $"unitBoard={(selectedUnit.BoardTilemap != null ? selectedUnit.BoardTilemap.name : "(null)")}");

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;
            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedUnit);
            string allAtCell = DescribeUnitsAtCellIgnoringReferenceMap(cell, selectedUnit);
            Debug.Log(
                $"[PodeFundirSensorDebug][FALLBACK][CELL] {i + 1}. cell={cell.x},{cell.y} " +
                $"getUnitAtCell={(other != null ? other.name : "(null)")} " +
                $"allAtCell={allAtCell}");
            if (other == null || other == selectedUnit || !other.gameObject.activeInHierarchy || other.IsEmbarked)
                continue;

                if ((int)selectedUnit.TeamId != (int)other.TeamId)
                {
                    invalidOptions.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = simulatedMovement,
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdDifferentTeam,
                        reason = "Unidade adjacente eh de outro time."
                    });
                    continue;
                }

                if (!AreUnitsSameTypeForDebug(selectedUnit, other))
                {
                    invalidOptions.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = simulatedMovement,
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdDifferentType,
                        reason = "Unidade adjacente nao eh do mesmo tipo."
                    });
                    continue;
                }

                if (DebugUnitHasPassengers(other))
                {
                    invalidOptions.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = simulatedMovement,
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdCandidateHasCargo,
                        reason = "Candidato transportando passageiros nao pode fundir."
                    });
                    continue;
                }

                bool sameOperationalLayer =
                    selectedUnit.GetDomain() == other.GetDomain() &&
                    selectedUnit.GetHeightLevel() == other.GetHeightLevel();
                if (!sameOperationalLayer)
                {
                    invalidOptions.Add(new PodeFundirInvalidOption
                    {
                        receiverUnit = selectedUnit,
                        candidateUnit = other,
                        candidateCell = cell,
                        remainingMovement = simulatedMovement,
                        requiredMovementCost = 0,
                        reasonId = PodeFundirInvalidOption.ReasonIdLayerMismatch,
                        reason = "Camada/altura diferente do receptor."
                    });
                    continue;
                }

            bool valid = PodeFundirSensor.EvaluateCandidateMovement(
                selectedUnit,
                other,
                map,
                terrainDatabase,
                simulatedMovement,
                out string invalidReasonId,
                out string invalidReason,
                out int requiredMovementCost,
                out int remainingMovement);

            if (valid)
            {
                validOptions.Add(new PodeFundirOption
                {
                    receiverUnit = selectedUnit,
                    candidateUnit = other,
                    candidateCell = cell,
                    remainingMovement = remainingMovement,
                    requiredMovementCost = requiredMovementCost,
                    displayLabel = $"{other.name} ({cell.x},{cell.y})"
                });
            }
            else
            {
                invalidOptions.Add(new PodeFundirInvalidOption
                {
                    receiverUnit = selectedUnit,
                    candidateUnit = other,
                    candidateCell = cell,
                    remainingMovement = remainingMovement,
                    requiredMovementCost = requiredMovementCost,
                    reasonId = invalidReasonId,
                    reason = string.IsNullOrWhiteSpace(invalidReason) ? "Candidato invalido para fusao." : invalidReason
                });
            }
        }
    }

    private static bool DebugUnitHasPassengers(UnitManager unit)
    {
        IReadOnlyList<UnitTransportSeatRuntime> seats = unit != null ? unit.TransportedUnitSlots : null;
        if (seats == null)
            return false;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] != null && seats[i].embarkedUnit != null)
                return true;
        }

        return false;
    }

    private static bool AreUnitsSameTypeForDebug(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        string aId = a.UnitId;
        string bId = b.UnitId;
        if (!string.IsNullOrWhiteSpace(aId) && !string.IsNullOrWhiteSpace(bId))
            return string.Equals(aId.Trim(), bId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (a.TryGetUnitData(out UnitData aData) && b.TryGetUnitData(out UnitData bData))
            return aData != null && bData != null && aData == bData;

        return false;
    }

    private static string DescribeUnitsAtCellIgnoringReferenceMap(Vector3Int cell, UnitManager exceptUnit = null)
    {
        if (UnitManager.AllActive == null || UnitManager.AllActive.Count == 0)
            return "[]";

        List<string> hits = new List<string>();
        for (int i = 0; i < UnitManager.AllActive.Count; i++)
        {
            UnitManager unit = UnitManager.AllActive[i];
            if (unit == null || unit == exceptUnit || !unit.gameObject.activeInHierarchy || unit.IsEmbarked)
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell != cell)
                continue;

            string board = unit.BoardTilemap != null ? unit.BoardTilemap.name : "(null)";
            hits.Add($"{unit.name}[team={(int)unit.TeamId},unitId={unit.UnitId},map={board}]");
        }

        if (hits.Count == 0)
            return "[]";

        return "[" + string.Join("; ", hits) + "]";
    }

    private void SelectLineForDrawing(UnitManager target)
    {
        if (selectedUnit == null || target == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = selectedUnit.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = target.CurrentCellPosition;
        selectedLineEndCell.z = 0;
        selectedLineColor = Color.cyan;
        selectedLineLabel = $"Fusao: {selectedUnit.name} -> {target.name}";
        hasSelectedLine = true;
        SceneView.RepaintAll();
    }

    private void SelectIneligibleLineForDrawing(DebugIneligibleNeighbor item)
    {
        if (selectedUnit == null || item == null || item.unit == null)
        {
            ClearSelectedLine();
            return;
        }

        selectedLineStartCell = selectedUnit.CurrentCellPosition;
        selectedLineStartCell.z = 0;
        selectedLineEndCell = item.unit.CurrentCellPosition;
        selectedLineEndCell.z = 0;
        selectedLineColor = Color.red;
        string reason = string.IsNullOrWhiteSpace(item.reason) ? "invalido" : item.reason;
        selectedLineLabel = $"Fusao invalida: {selectedUnit.name} -> {item.unit.name} | {reason}";
        hasSelectedLine = true;
        SceneView.RepaintAll();
    }

    private void ClearSelectedLine()
    {
        hasSelectedLine = false;
        selectedLineLabel = string.Empty;
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!hasSelectedLine)
            return;

        Tilemap map = ResolveTilemap();
        if (map == null)
            return;

        Vector3 start = map.GetCellCenterWorld(selectedLineStartCell);
        Vector3 end = map.GetCellCenterWorld(selectedLineEndCell);

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        Handles.color = selectedLineColor;
        Handles.DrawAAPolyLine(4f, start, end);
        Handles.SphereHandleCap(0, start, Quaternion.identity, 0.12f, EventType.Repaint);
        Handles.SphereHandleCap(0, end, Quaternion.identity, 0.12f, EventType.Repaint);

        Vector3 mid = Vector3.Lerp(start, end, 0.5f);
        Handles.Label(mid + new Vector3(0.1f, 0.1f, 0f), selectedLineLabel);
    }

    private static FusionLayerPlan ResolveFusionLayerPlan(
        UnitManager baseUnit,
        List<DebugMergeOrder> queue,
        Tilemap map,
        TerrainDatabase terrainDb,
        out string observation)
    {
        List<UnitManager> members = BuildMergeMembers(baseUnit, queue);
        if (members.Count <= 1)
        {
            observation = "Sem participantes na fila de fusao.";
            return FusionLayerPlan.None;
        }

        if (AllMembersOnSameLayer(members))
        {
            observation = "Sem ajuste de camada: todos os membros da fusao estao na mesma camada/altitude.";
            return FusionLayerPlan.DefaultSameDomain;
        }

        bool hasAr = HasAnySkillPrefix(members, "AR ") || HasAnyAirFamily(members);
        if (hasAr)
        {
            observation = "AR: a fusao ocorrera em Air/AirLow.";
            return FusionLayerPlan.AirLow;
        }

        bool hasSub = HasAnySkillPrefix(members, "SUB ") || HasAnySubFamily(members);
        if (!hasSub)
        {
            observation = "Camadas diferentes detectadas (sem familia AR/SUB identificada).";
            return FusionLayerPlan.None;
        }

        bool allCanSubmerge = map != null && terrainDb != null;
        if (allCanSubmerge)
        {
            for (int i = 0; i < members.Count; i++)
            {
                UnitManager unit = members[i];
                if (unit == null)
                    continue;

                Vector3Int layerCell = unit.CurrentCellPosition;
                layerCell.z = 0;
                if (!LayerTransitionRules.CanUseLayerModeAtCell(
                        unit,
                        map,
                        terrainDb,
                        layerCell,
                        Domain.Submarine,
                        HeightLevel.Submerged,
                        out _))
                {
                    allCanSubmerge = false;
                    break;
                }
            }
        }

        if (allCanSubmerge)
        {
            observation = "SUB: a fusao ocorrera em Submarine/Submerged.";
            return FusionLayerPlan.SubSubmerged;
        }

        observation = "SUB: a fusao ocorrera em Naval/Surface.";
        return FusionLayerPlan.NavalSurface;
    }

    private static List<UnitManager> BuildMergeMembers(UnitManager baseUnit, List<DebugMergeOrder> queue)
    {
        List<UnitManager> members = new List<UnitManager>();
        if (baseUnit != null)
            members.Add(baseUnit);

        if (queue == null)
            return members;

        for (int i = 0; i < queue.Count; i++)
        {
            DebugMergeOrder order = queue[i];
            if (order == null || order.candidate == null)
                continue;
            if (!members.Contains(order.candidate))
                members.Add(order.candidate);
        }

        return members;
    }

    private static bool AllMembersOnSameLayer(List<UnitManager> members)
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

    private static bool HasAnySkillPrefix(List<UnitManager> members, string prefix)
    {
        if (members == null || string.IsNullOrWhiteSpace(prefix))
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            UnitManager unit = members[i];
            if (unit == null)
                continue;
            if (HasSkillPrefix(unit, prefix))
                return true;
        }

        return false;
    }

    private static bool HasAnyAirFamily(List<UnitManager> members)
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

    private static bool HasAnySubFamily(List<UnitManager> members)
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

    private static bool HasSkillPrefix(UnitManager unit, string prefix)
    {
        if (unit == null || string.IsNullOrWhiteSpace(prefix))
            return false;
        if (!unit.TryGetUnitData(out UnitData data) || data == null || data.skills == null)
            return false;

        string normalizedPrefix = prefix.Trim();
        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill == null)
                continue;
            if (StartsWithToken(skill.displayName, normalizedPrefix) || StartsWithToken(skill.id, normalizedPrefix))
                return true;
        }

        return false;
    }

    private static bool StartsWithToken(string value, string tokenPrefix)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(tokenPrefix))
            return false;

        return value.TrimStart().StartsWith(tokenPrefix, System.StringComparison.OrdinalIgnoreCase);
    }

    private static Tilemap FindPreferredTilemap()
    {
        Tilemap[] maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (maps == null || maps.Length == 0)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            Tilemap map = maps[i];
            if (map != null && string.Equals(map.name, "Tilemap", System.StringComparison.OrdinalIgnoreCase))
                return map;
        }

        return maps[0];
    }

    private static TerrainDatabase FindFirstTerrainDatabaseAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TerrainDatabase db = AssetDatabase.LoadAssetAtPath<TerrainDatabase>(path);
            if (db != null)
                return db;
        }

        return null;
    }

}


