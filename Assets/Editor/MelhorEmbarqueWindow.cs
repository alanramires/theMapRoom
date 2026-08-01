using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorEmbarqueWindow : EditorWindow
{
    [SerializeField] private UnitManager passenger;
    [SerializeField] private UnitManager transporter;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private int operationalTurns = 2;
    [SerializeField] private bool includeStrategic;
    [SerializeField] private bool showProbableDirection;

    private Tilemap map;
    private MelhorEmbarqueResult result;
    private MelhorEmbarqueResult runtimePolicyResult;
    private MelhorEmbarqueLzScore selected;
    private MelhorEmbarqueOption selectedOption;
    private MelhorEmbarqueOption runtimePolicyOption;
    private MelhorEmbarqueLzScore probableDirection;
    private bool comparisonCapturedWhileRunning;
    private Vector2 scroll;
    private string status =
        "Selecione o passageiro; o transportador é opcional.";

    [MenuItem("Tools/Hotzone/Melhor LZ de Embarque")]
    public static void Open() =>
        GetWindow<MelhorEmbarqueWindow>("Melhor LZ de Embarque").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        Repaint();
    }

    private void AutoDetect()
    {
        if (passenger != null && transporter == passenger)
            transporter = null;
        if (passenger != null)
            map = passenger.BoardTilemap;
        else if (transporter != null)
            map = transporter.BoardTilemap;
        if (terrainDatabase != null)
            return;
        string[] guids =
            AssetDatabase.FindAssets("t:TerrainDatabase");
        if (guids.Length > 0)
        {
            terrainDatabase =
                AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Melhor LZ de Embarque", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura centrada no passageiro. Sem filtro, compara " +
            "todos os transportadores aliados compatíveis; com um " +
            "transportador informado, responde somente para ele. A LZ " +
            "vem de UnitData > Transport > Allow Embark When " +
            "Transporter At; Encontro Pax é o hex onde o passageiro " +
            "realmente consegue parar.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        passenger = (UnitManager)EditorGUILayout.ObjectField(
            "Passageiro", passenger,
            typeof(UnitManager), true);
        transporter = (UnitManager)EditorGUILayout.ObjectField(
            "Transportador (opcional)", transporter,
            typeof(UnitManager), true);
        terrainDatabase =
            (TerrainDatabase)EditorGUILayout.ObjectField(
                "Terrain Database", terrainDatabase,
                typeof(TerrainDatabase), false);
        operationalTurns = Mathf.Max(
            1, EditorGUILayout.IntField(
                "Turnos operacionais", operationalTurns));
        includeStrategic = EditorGUILayout.Toggle(
            "Incluir Strategic", includeStrategic);
        showProbableDirection = EditorGUILayout.Toggle(
            new GUIContent(
                "Ver direção provável",
                "Diagnóstico opcional e mais caro. Não participa da decisão; " +
                "a orientação de gameplay pertence aos sensores PodeX."),
            showProbableDirection);
        if (EditorGUI.EndChangeCheck())
        {
            AutoDetect();
            result = null;
            runtimePolicyResult = null;
            selected = null;
            selectedOption = null;
            runtimePolicyOption = null;
            probableDirection = null;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection(silent: false);
        if (GUILayout.Button("Usar como Transportador"))
            TryUseSelectionAsTransporter();
        if (GUILayout.Button("Auto Detect"))
        {
            AutoDetectRuntimeSelection();
        }
        EditorGUILayout.EndHorizontal();

        using (new EditorGUI.DisabledScope(
                   passenger == null
                   || map == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Calcular Melhor LZ de Embarque",
                    GUILayout.Height(30f)))
                Calculate();
        }

        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null)
            return;

        DrawAnalysisMode();

        if (showProbableDirection
            && probableDirection != null)
        {
            MelhorEmbarqueLzScore guidance =
                probableDirection;
            EditorGUILayout.HelpBox(
                $"Diagnóstico visual — não participa da decisão\n" +
                $"Direção provável: LZ {guidance.cell} | " +
                $"pax={guidance.passengers.Count} | " +
                $"dist={guidance.transporterDistance}",
                MessageType.Info);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField(
            $"Opções passageiro–LZ ({result.options.Count})",
            EditorStyles.boldLabel);
        for (int i = 0; i < result.options.Count; i++)
        {
            MelhorEmbarqueOption option = result.options[i];
            bool active = selectedOption == option;
            bool runtimeChoice = IsSameOption(
                option, runtimePolicyOption);
            GUI.backgroundColor = active
                ? new Color(1f, 0.8f, 0.2f)
                : runtimeChoice
                    ? new Color(0.35f, 0.9f, 1f)
                    : Color.white;
            string passengerName = option.passenger != null
                ? option.passenger.name
                : "?";
            string transporterName = option.transporter != null
                ? option.transporter.name
                : "?";
            if (GUILayout.Button(
                    $"{(runtimeChoice ? "[RUNTIME] " : "")}" +
                    $"#{i + 1} {option.transporterTier} " +
                    $"{passengerName} → {transporterName} | " +
                    $"Pax={option.passengerMeetingCell} " +
                    $"LZ={option.lzCell} | " +
                    $"{option.passengerRouteState}",
                    EditorStyles.miniButton))
            {
                selectedOption = option;
                selected = result.ranking.Find(
                    lz => lz.cell == option.lzCell
                        && lz.transporter == option.transporter);
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            if (!active)
                continue;

            EditorGUILayout.LabelField(
                option.reason, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                $"Slot: {option.slotIndex} | " +
                $"Disposition: {option.rideDisposition} | " +
                $"Ajuste carona: {option.rideNeedAdjustment:0} | " +
                $"Nota: {option.score:0}",
                EditorStyles.wordWrappedMiniLabel);
            if (option.rideNeed != null)
            {
                EditorGUILayout.LabelField(
                    $"Quero Carona: {option.rideNeed.reason}",
                    EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.LabelField(
                $"Rota transportador: " +
                $"{FormatCost(option.transporterRouteCost)} | " +
                $"Rota passageiro: " +
                $"{FormatCost(option.passengerRouteCost)} | " +
                $"Embarque: " +
                $"{FormatCost(option.passengerEmbarkCost)} | " +
                $"Total passageiro: " +
                $"{FormatCost(option.passengerTotalCost)}",
                EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            $"LZs válidos legados ({result.ranking.Count})",
            EditorStyles.boldLabel);
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorEmbarqueLzScore lz = result.ranking[i];
            bool active = selected == lz;
            GUI.backgroundColor = active
                ? new Color(1f, 0.8f, 0.2f)
                : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} {lz.tier} " +
                    $"{lz.transporter?.name ?? "?"} LZ={lz.cell} " +
                    $"pax={lz.passengers.Count} " +
                    $"dist={lz.transporterDistance}",
                    EditorStyles.miniButton))
            {
                selected = lz;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;
            if (!active)
                continue;
            EditorGUILayout.LabelField(
                lz.reason, EditorStyles.wordWrappedMiniLabel);
            for (int p = 0; p < lz.passengers.Count; p++)
            {
                MelhorEmbarquePassengerScore passenger =
                    lz.passengers[p];
                EditorGUILayout.LabelField(
                    $"  {passenger.passenger.name} " +
                    $"@{passenger.passengerCell} " +
                    $"→ encontro={passenger.passengerMeetingCell} " +
                    $"slot={passenger.slotIndex} " +
                    $"move={passenger.passengerMoveCost}",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            $"Transportadores incompatíveis " +
            $"({result.rejectedPassengers.Count})",
            EditorStyles.boldLabel);
        for (int i = 0;
             i < result.rejectedPassengers.Count;
             i++)
        {
            MelhorEmbarqueReject reject =
                result.rejectedPassengers[i];
            EditorGUILayout.LabelField(
                $"{(reject.passenger != null ? reject.passenger.name : "?")}: " +
                reject.reason,
                EditorStyles.wordWrappedMiniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void TryUseSelection(bool silent)
    {
        UnitManager picked =
            Selection.activeGameObject != null
                ? Selection.activeGameObject
                    .GetComponent<UnitManager>()
                : null;
        if (picked == null)
        {
            if (!silent)
                status =
                    "O objeto selecionado não possui UnitManager.";
            return;
        }
        passenger = picked;
        if (transporter == picked)
            transporter = null;
        AutoDetect();
        result = null;
        runtimePolicyResult = null;
        selected = null;
        selectedOption = null;
        runtimePolicyOption = null;
        status = $"Passageiro: {picked.name}.";
        SceneView.RepaintAll();
    }

    private void TryUseSelectionAsTransporter()
    {
        UnitManager picked =
            Selection.activeGameObject != null
                ? Selection.activeGameObject
                    .GetComponent<UnitManager>()
                : null;
        if (picked == null)
        {
            status =
                "O objeto selecionado não possui UnitManager.";
            return;
        }
        if (picked == passenger)
        {
            status =
                "Passageiro e transportador não podem ser a mesma unidade.";
            return;
        }
        if (!picked.TryGetUnitData(out UnitData data)
            || data == null
            || !data.isTransporter)
        {
            status =
                $"{picked.name} não está configurado como transportador.";
            return;
        }

        transporter = picked;
        AutoDetect();
        result = null;
        runtimePolicyResult = null;
        selected = null;
        selectedOption = null;
        runtimePolicyOption = null;
        probableDirection = null;
        status = $"Transportador: {picked.name}.";
        SceneView.RepaintAll();
    }

    private void AutoDetectRuntimeSelection()
    {
        if (Application.isPlaying)
        {
            TurnStateManager turnState =
                FindAnyObjectByType<TurnStateManager>();
            UnitManager runtimeUnit = turnState != null
                ? turnState.SelectedUnit
                : null;
            string runtimeSource = "TurnStateManager.SelectedUnit";
            if (runtimeUnit == null)
            {
                AIController ai =
                    FindAnyObjectByType<AIController>();
                if (ai != null
                    && ai.TryGetDebugStepPendingUnit(
                        out UnitManager pendingStepUnit))
                {
                    runtimeUnit = pendingStepUnit;
                    runtimeSource = "batch preparado pelo F11";
                }
            }
            if (runtimeUnit != null)
            {
                passenger = runtimeUnit;
                if (transporter == runtimeUnit)
                    transporter = null;
                AutoDetect();
                result = null;
                runtimePolicyResult = null;
                selected = null;
                selectedOption = null;
                runtimePolicyOption = null;
                probableDirection = null;

                Selection.activeGameObject =
                    runtimeUnit.gameObject;
                EditorGUIUtility.PingObject(runtimeUnit.gameObject);
                SceneView.FrameLastActiveSceneView();
                SceneView.RepaintAll();

                status =
                    $"Passageiro runtime da IA ({runtimeSource}): " +
                    $"{runtimeUnit.name}. Selecionado e enquadrado na " +
                    "Scene View; o filtro de transportador foi preservado.";
                return;
            }
        }

        AutoDetect();
        status = Application.isPlaying
            ? "A IA não possui unidade runtime selecionada neste instante."
            : map != null
                ? "Contexto detectado. Fora do Play Mode não existe " +
                  "unidade runtime da IA."
                : "Tilemap não encontrado.";
    }

    private void Calculate()
    {
        SyncRegistry();
        AutoDetect();
        comparisonCapturedWhileRunning =
            Application.isPlaying && !EditorApplication.isPaused;
        result = MelhorEmbarqueService.EvaluateForPassenger(
            new MelhorEmbarquePassengerRequest
            {
                passenger = passenger,
                transporter = transporter,
                map = map,
                terrainDatabase = terrainDatabase,
                operationalTurns = operationalTurns,
                includeStrategic = includeStrategic,
                evaluateRideNeed = EvaluateRideNeed
            });
        selected = result.best;
        selectedOption = result.bestOption;
        runtimePolicyResult = null;
        runtimePolicyOption = null;
        if (comparisonCapturedWhileRunning)
        {
            runtimePolicyResult = Evaluate(includeStrategic: true);
            runtimePolicyOption =
                SelectRuntimePickupOption(runtimePolicyResult);
        }
        probableDirection = null;
        if (showProbableDirection && !includeStrategic)
        {
            MelhorEmbarqueResult directionProbe =
                runtimePolicyResult ??
                MelhorEmbarqueService.EvaluateForPassenger(
                    new MelhorEmbarquePassengerRequest
                    {
                        passenger = passenger,
                        transporter = transporter,
                        map = map,
                        terrainDatabase = terrainDatabase,
                        operationalTurns = operationalTurns,
                        includeStrategic = true,
                        evaluateRideNeed = EvaluateRideNeed
                    });
            probableDirection = directionProbe.ranking.Find(
                lz => lz.tier == MelhorEmbarqueTier.Strategic);
        }
        status = selectedOption != null
            ? $"Melhor opção: {selectedOption.transporterTier} " +
              $"{selectedOption.passenger?.name} → " +
              $"{selectedOption.transporter?.name} | encontro " +
              $"{selectedOption.passengerMeetingCell} | LZ " +
              $"{selectedOption.lzCell} " +
              $"({selectedOption.passengerRouteState})."
            : "Nenhum encontro válido encontrado.";
        SceneView.RepaintAll();
    }

    private MelhorEmbarqueResult Evaluate(bool includeStrategic) =>
        MelhorEmbarqueService.EvaluateForPassenger(
            new MelhorEmbarquePassengerRequest
            {
                passenger = passenger,
                transporter = transporter,
                map = map,
                terrainDatabase = terrainDatabase,
                operationalTurns = operationalTurns,
                includeStrategic = includeStrategic,
                evaluateRideNeed = EvaluateRideNeed
            });

    private void DrawAnalysisMode()
    {
        if (!comparisonCapturedWhileRunning)
        {
            EditorGUILayout.HelpBox(
                "Retrato atual: consulta feita com o jogo parado ou " +
                "pausado. Somente o estado observado e exibido.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField(
            "Comparacao para analise", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Ferramenta - ranking bruto", EditorStyles.boldLabel);
        DrawOptionSummary(selectedOption);
        EditorGUILayout.LabelField(
            includeStrategic
                ? "Escopo: Tactical + Operational + Strategic."
                : "Escopo: Tactical + Operational (toggle atual).",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Politica runtime - Pickup", EditorStyles.boldLabel);
        DrawOptionSummary(runtimePolicyOption);
        EditorGUILayout.LabelField(
            "Prioriza Tactical -> Operational -> Strategic; descarta " +
            "OpportunisticFallback e encontros nao materializaveis. " +
            "O filtro de seguranca Strategic pertence a decisao real.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        if (!IsSameOption(selectedOption, runtimePolicyOption))
        {
            EditorGUILayout.HelpBox(
                "As escolhas diferem. Amarelo marca o ranking bruto; " +
                "azul marca a escolha simulada da politica runtime.",
                MessageType.Warning);
        }
    }

    private static void DrawOptionSummary(
        MelhorEmbarqueOption option)
    {
        if (option?.passenger == null)
        {
            EditorGUILayout.LabelField(
                "Nenhuma opcao elegivel.",
                EditorStyles.wordWrappedMiniLabel);
            return;
        }

        EditorGUILayout.LabelField(
            $"{option.transporterTier} | {option.passenger.name} -> " +
            $"{option.transporter?.name ?? "?"} | encontro " +
            $"{option.passengerMeetingCell} | LZ {option.lzCell}",
            EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField(
            $"{option.rideDisposition} | " +
            $"{option.passengerRouteState} | nota={option.score:0}",
            EditorStyles.wordWrappedMiniLabel);
    }

    private static MelhorEmbarqueOption SelectRuntimePickupOption(
        MelhorEmbarqueResult pickup)
    {
        if (pickup == null)
            return null;

        for (int tierIndex = (int)MelhorEmbarqueTier.Tactical;
             tierIndex <= (int)MelhorEmbarqueTier.Strategic;
             tierIndex++)
        {
            MelhorEmbarqueTier tier =
                (MelhorEmbarqueTier)tierIndex;
            MelhorEmbarqueOption option = pickup.options.Find(
                candidate =>
                    candidate != null
                    && candidate.transporterTier == tier
                    && candidate.rideDisposition !=
                        MelhorEmbarqueRideDisposition
                            .OpportunisticFallback
                    && CanMaterializePickupRendezvous(
                        candidate, tier));
            if (option != null)
                return option;
        }

        return null;
    }

    private static bool CanMaterializePickupRendezvous(
        MelhorEmbarqueOption option,
        MelhorEmbarqueTier serviceTier)
    {
        if (option?.passenger == null)
            return false;

        bool passengerIsAircraft =
            option.passenger.TryGetUnitData(
                out UnitData passengerData)
            && passengerData != null
            && passengerData.domain == Domain.Air;
        if (passengerIsAircraft)
            return true;
        if (option.passengerRouteState ==
                MelhorEmbarquePassengerRouteState.NoCurrentRoute
            || option.passengerRouteState ==
                MelhorEmbarquePassengerRouteState.ReachableStrategic)
            return false;
        return serviceTier != MelhorEmbarqueTier.Tactical
            || option.passengerRouteState ==
                MelhorEmbarquePassengerRouteState.ReachableNow;
    }

    private static bool IsSameOption(
        MelhorEmbarqueOption a,
        MelhorEmbarqueOption b) =>
        a != null
        && b != null
        && a.passenger != null
        && b.passenger != null
        && a.passenger.InstanceId == b.passenger.InstanceId
        && a.transporter != null
        && b.transporter != null
        && a.transporter.InstanceId == b.transporter.InstanceId
        && a.transporterTier == b.transporterTier
        && a.passengerMeetingCell == b.passengerMeetingCell
        && a.lzCell == b.lzCell;

    private static string FormatCost(int value) =>
        value >= 0 ? value.ToString() : "sem rota atual";

    private QueroCaronaResult EvaluateRideNeed(
        UnitManager passenger)
    {
        ConstructionSector sector = ConstructionSector.None;
        bool hasPlan = passenger != null
            && System.Enum.TryParse(
                passenger.AIAssignedPlanName,
                true,
                out sector)
            && sector != ConstructionSector.None;
        return QueroCaronaService.Evaluate(
            new QueroCaronaRequest
            {
                unit = passenger,
                map = map,
                terrainDatabase = terrainDatabase,
                context = hasPlan
                    ? QueroCaronaContext.ComPlano
                    : QueroCaronaContext.RogueOuRebelde,
                plannedSector = sector,
                operationalTurns = operationalTurns,
                emulateUnderRepairFromUnitData =
                    !Application.isPlaying
            });
    }

    private static void SyncRegistry()
    {
        if (Application.isPlaying)
            return;
        UnitManager.AllActive.Clear();
        UnitManager[] units =
            FindObjectsByType<UnitManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] != null
                && units[i].gameObject.activeInHierarchy)
                UnitManager.AllActive.Add(units[i]);
        }

        ConstructionManager.AllActive.Clear();
        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            if (constructions[i] != null
                && constructions[i].gameObject.activeInHierarchy)
                ConstructionManager.AllActive.Add(constructions[i]);
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null)
            return;
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorEmbarqueLzScore lz = result.ranking[i];
            Vector3 world = map.GetCellCenterWorld(lz.cell);
            Color color = lz == selected
                ? Color.yellow
                : lz.tier == MelhorEmbarqueTier.Tactical
                    ? Color.green
                    : lz.tier == MelhorEmbarqueTier.Operational
                        ? new Color(0.2f, 0.7f, 1f)
                        : new Color(1f, 0.3f, 0.8f);
            Handles.color = color;
            Handles.DrawWireDisc(
                world, Vector3.forward,
                lz == selected ? 0.42f : 0.28f);
            Handles.Label(
                world + Vector3.up * 0.32f,
                $"{lz.tier} P{lz.passengers.Count}");
        }

        if (selectedOption != null)
        {
            Vector3 lzWorld =
                map.GetCellCenterWorld(selectedOption.lzCell);
            Handles.color = Color.yellow;
            Handles.DrawWireDisc(
                lzWorld, Vector3.forward, 0.52f);
            Handles.Label(
                lzWorld + Vector3.up * 0.55f,
                selectedOption.passenger != null
                    ? $"LZ DESTINO {selectedOption.transporter?.name} | " +
                      $"{selectedOption.passengerRouteState}"
                    : selectedOption.passengerRouteState.ToString());
            DrawTransporterOriginToLz(
                selectedOption,
                lzWorld,
                Color.yellow,
                "TRANSPORTADOR AGORA",
                "ROTA → LZ DESTINO");

            if (selectedOption.hasPassengerMeetingCell)
            {
                Vector3 meetingWorld = map.GetCellCenterWorld(
                    selectedOption.passengerMeetingCell);
                Handles.color = new Color(0.2f, 1f, 0.45f);
                Handles.DrawWireDisc(
                    meetingWorld, Vector3.forward, 0.46f);
                Handles.DrawDottedLine(
                    meetingWorld, lzWorld, 4f);
                Handles.Label(
                    meetingWorld + Vector3.up * 0.48f,
                    $"ENCONTRO PAX {selectedOption.passenger?.name}");
            }
        }

        if (comparisonCapturedWhileRunning
            && runtimePolicyOption != null
            && !IsSameOption(selectedOption, runtimePolicyOption))
        {
            Vector3 runtimeWorld =
                map.GetCellCenterWorld(runtimePolicyOption.lzCell);
            Handles.color = new Color(0.2f, 0.9f, 1f);
            Handles.DrawWireDisc(
                runtimeWorld, Vector3.forward, 0.62f);
            Handles.Label(
                runtimeWorld + Vector3.up * 0.75f,
                $"RUNTIME LZ DESTINO " +
                $"{runtimePolicyOption.transporter?.name}");
            DrawTransporterOriginToLz(
                runtimePolicyOption,
                runtimeWorld,
                new Color(0.2f, 0.9f, 1f),
                "RUNTIME TRANSPORTADOR AGORA",
                "RUNTIME ROTA → LZ");
            if (runtimePolicyOption.hasPassengerMeetingCell)
            {
                Vector3 runtimeMeeting = map.GetCellCenterWorld(
                    runtimePolicyOption.passengerMeetingCell);
                Handles.DrawWireDisc(
                    runtimeMeeting, Vector3.forward, 0.48f);
                Handles.DrawDottedLine(
                    runtimeMeeting, runtimeWorld, 4f);
                Handles.Label(
                    runtimeMeeting + Vector3.up * 0.52f,
                    "RUNTIME ENCONTRO PAX");
            }
        }

        if (showProbableDirection
            && probableDirection != null
            && probableDirection.transporter != null)
        {
            Vector3 from = map.GetCellCenterWorld(
                probableDirection.transporter.CurrentCellPosition);
            Vector3 to = map.GetCellCenterWorld(
                probableDirection.cell);
            Handles.color = new Color(1f, 0.3f, 0.8f);
            Handles.DrawDottedLine(from, to, 6f);
            Handles.ArrowHandleCap(
                0,
                to,
                Quaternion.LookRotation(
                    Vector3.forward, to - from),
                0.6f,
                EventType.Repaint);
            Handles.Label(
                Vector3.Lerp(from, to, 0.5f),
                "Direção provável (debug)");
        }
    }

    private void DrawTransporterOriginToLz(
        MelhorEmbarqueOption option,
        Vector3 lzWorld,
        Color color,
        string originLabel,
        string routeLabel)
    {
        if (option?.transporter == null || map == null)
            return;

        Vector3Int originCell =
            option.transporter.CurrentCellPosition;
        originCell.z = 0;
        Vector3 originWorld =
            map.GetCellCenterWorld(originCell);

        Handles.color = color;
        Handles.DrawWireDisc(
            originWorld, Vector3.forward, 0.34f);
        Handles.Label(
            originWorld + Vector3.down * 0.48f,
            $"{originLabel} {option.transporter.name} " +
            $"{originCell}");

        if (originCell == option.lzCell)
        {
            Handles.Label(
                originWorld + Vector3.down * 0.68f,
                "JÁ ESTÁ NA LZ DESTINO");
            return;
        }

        Handles.DrawDottedLine(
            originWorld, lzWorld, 5f);
        Vector3 direction = lzWorld - originWorld;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Handles.ArrowHandleCap(
                0,
                lzWorld,
                Quaternion.LookRotation(
                    Vector3.forward, direction),
                0.48f,
                EventType.Repaint);
        }
        Handles.Label(
            Vector3.Lerp(originWorld, lzWorld, 0.5f),
            routeLabel);
    }
}
