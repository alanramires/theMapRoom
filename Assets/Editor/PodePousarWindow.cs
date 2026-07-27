using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

/// <summary>
/// Ferramenta unificada de pouso. Um hex, quatro fontes de regra:
/// Terreno, Estrutura+Terreno, Construcao (as tres do contexto do hex) e
/// Plataforma transportadora (slot de transporte da unidade que ocupa o hex).
/// Toda regra exibida vem do resolver/sensor — a janela nao reimplementa
/// hierarquia nem semantica de skill.
/// </summary>
public sealed class PodePousarWindow : EditorWindow
{
    private sealed class PlatformDiagnosis
    {
        public UnitManager transporter;
        public bool available;
        public string reason;
        public int slotIndex;
        public bool manualTarget;
        public readonly List<PodePousarSlotReport> slots =
            new List<PodePousarSlotReport>();
    }

    [SerializeField] private UnitManager aircraft;
    [SerializeField] private Tilemap map;
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private UnitManager manualPlatform;
    [SerializeField] private SensorMovementMode movementMode =
        SensorMovementMode.MoveuParado;
    [SerializeField] private bool hasDestination;
    [SerializeField] private Vector3Int destination;

    private bool pickingDestination;
    private Vector3Int hoverCell;
    private Vector3Int evaluatedCell;
    private PodePousarReport report;
    private Domain landingDomain;
    private HeightLevel landingHeight;
    private bool supportsLandingLayer;
    private AirOperationTileContext evaluatedContext;
    private AirLandingEvaluation landingEvaluation;
    private AirOperationSkillRequirement landingRequirement;
    private string resolvedLayerSource;
    private bool occupancyAllowed;
    private UnitManager occupancyBlocker;
    private bool commonGateOk;
    private string commonGateReason;
    private readonly List<UnitManager> hexOccupants =
        new List<UnitManager>();
    private readonly List<PlatformDiagnosis> platforms =
        new List<PlatformDiagnosis>();
    private Vector2 detailsScroll;

    public static void Open()
    {
        GetWindow<PodePousarWindow>(
            "Pode Pousar").Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSelectionChange()
    {
        TryUseCurrentSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Pode Pousar", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Consulta pura: avalia o hex escolhido pelas quatro fontes de regra " +
            "(terreno, estrutura, construcao e plataforma transportadora). " +
            "Nenhuma acao e confirmada e nenhuma unidade e movida.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        aircraft = (UnitManager)EditorGUILayout.ObjectField(
            "Aeronave", aircraft, typeof(UnitManager), true);
        map = (Tilemap)EditorGUILayout.ObjectField(
            "Tilemap", map, typeof(Tilemap), true);
        terrainDatabase = (TerrainDatabase)EditorGUILayout.ObjectField(
            "Terrain Database",
            terrainDatabase,
            typeof(TerrainDatabase),
            false);
        movementMode = (SensorMovementMode)EditorGUILayout.EnumPopup(
            "Estado de movimento", movementMode);
        manualPlatform = (UnitManager)EditorGUILayout.ObjectField(
            new GUIContent(
                "Plataforma (opcional)",
                "Transportador avaliado alem dos que ocupam o hex. " +
                "Util para testar uma plataforma que a aeronave ainda nao alcancou."),
            manualPlatform,
            typeof(UnitManager),
            true);
        if (EditorGUI.EndChangeCheck())
            ClearResult();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseCurrentSelection();

        GUI.backgroundColor = pickingDestination
            ? new Color(1f, 0.75f, 0.2f)
            : Color.white;
        if (GUILayout.Button(
                pickingDestination
                    ? "Clique no Scene View..."
                    : "Escolher Hex de Destino"))
        {
            pickingDestination = !pickingDestination;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        Vector3Int effectiveCell =
            hasDestination
                ? destination
                : aircraft != null
                    ? aircraft.CurrentCellPosition
                    : Vector3Int.zero;
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector3IntField(
                "Hex avaliado", effectiveCell);

        using (new EditorGUI.DisabledScope(!hasDestination))
        {
            if (GUILayout.Button(
                    "Usar o Próprio Hex da Aeronave"))
            {
                hasDestination = false;
                pickingDestination = false;
                ClearResult();
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUI.DisabledScope(
                   aircraft == null
                   || map == null
                   || terrainDatabase == null))
        {
            if (GUILayout.Button(
                    "Verificar Pouso", GUILayout.Height(28f)))
                Evaluate();
        }

        EditorGUILayout.Space(6f);
        if (report == null)
            return;

        DrawVerdict();
        DrawDetails();
    }

    private void Evaluate()
    {
        AutoDetect();
        if (aircraft == null || map == null
            || terrainDatabase == null)
            return;

        evaluatedCell =
            hasDestination ? destination : aircraft.CurrentCellPosition;
        evaluatedCell.z = 0;

        commonGateOk = AircraftOperationRules.CanAttemptLanding(
            aircraft, out commonGateReason);

        evaluatedContext = AirOperationResolver.ResolveContext(
            map, terrainDatabase, evaluatedCell);
        landingEvaluation = AirOperationResolver.EvaluateLanding(
            aircraft, evaluatedContext, movementMode);
        landingRequirement = AirOperationResolver.DescribeLandingRequirement(
            evaluatedContext);

        // Camada de pouso pela MESMA chamada da regra (que aplica o fallback
        // Land/Surface quando o hex nao resolve), senao a ocupacao seria testada
        // numa banda diferente da que o jogo usa.
        AircraftOperationRules.ResolveGroundedLayerForCell(
            aircraft,
            map,
            terrainDatabase,
            evaluatedCell,
            out landingDomain,
            out landingHeight);
        LayerTransitionRules.TryResolvePrimaryLayerAtCell(
            map,
            terrainDatabase,
            evaluatedCell,
            out _,
            out _,
            out resolvedLayerSource);

        supportsLandingLayer =
            aircraft.SupportsLayerMode(landingDomain, landingHeight);
        occupancyAllowed = UnitOccupancyRules.CanEndLayerTransitionAtCell(
            map,
            evaluatedCell,
            aircraft,
            landingDomain,
            landingHeight,
            out occupancyBlocker);

        hexOccupants.Clear();
        hexOccupants.AddRange(
            UnitOccupancyRules.GetUnitsAtCell(map, evaluatedCell, aircraft));

        // Consulta por hex: o sensor recebe a celula, ninguem e deslocado.
        report = PodePousarSensor.Evaluate(
            aircraft,
            map,
            terrainDatabase,
            movementMode,
            useManualRemainingMovement: false,
            manualRemainingMovement: 0,
            atCell: evaluatedCell);

        EvaluatePlatforms(evaluatedCell);

        Repaint();
        SceneView.RepaintAll();
    }

    // A plataforma nao e hex: seu Aircraft Ops sao os transport slots. Ela entra
    // pela unidade que ocupa o hex avaliado (o destino real do pouso) e, se
    // houver, pelo alvo manual.
    private void EvaluatePlatforms(Vector3Int cell)
    {
        platforms.Clear();

        List<UnitManager> occupants =
            UnitOccupancyRules.GetUnitsAtCell(map, cell, aircraft);
        for (int i = 0; i < occupants.Count; i++)
            TryDiagnosePlatform(occupants[i], manualTarget: false);

        if (manualPlatform != null && manualPlatform != aircraft)
            TryDiagnosePlatform(manualPlatform, manualTarget: true);
    }

    private void TryDiagnosePlatform(UnitManager candidate, bool manualTarget)
    {
        if (candidate == null || candidate == aircraft)
            return;

        for (int i = 0; i < platforms.Count; i++)
            if (platforms[i].transporter == candidate)
                return;

        // Ocupantes comuns nao viram linha de plataforma; alvo manual sempre
        // aparece, para o motivo da recusa ficar visivel.
        bool isTransporter =
            candidate.TryGetUnitData(out UnitData candidateData)
            && candidateData != null
            && candidateData.isTransporter;
        if (!isTransporter && !manualTarget)
            return;

        var diagnosis = new PlatformDiagnosis
        {
            transporter = candidate,
            manualTarget = manualTarget
        };
        diagnosis.available = PodePousarSensor.DescribeTransporterLanding(
            aircraft,
            candidate,
            diagnosis.slots,
            out diagnosis.slotIndex,
            out diagnosis.reason);
        platforms.Add(diagnosis);
    }

    private bool TryGetAvailablePlatform(out PlatformDiagnosis platform)
    {
        for (int i = 0; i < platforms.Count; i++)
        {
            if (platforms[i].available)
            {
                platform = platforms[i];
                return true;
            }
        }

        platform = null;
        return false;
    }

    private void DrawVerdict()
    {
        bool hexOk = report != null && report.status;
        bool platformOk = TryGetAvailablePlatform(out PlatformDiagnosis platform);

        if (hexOk || platformOk)
        {
            var lines = new List<string> { "PODE POUSAR" };
            if (hexOk)
                lines.Add($"• Superfície ({evaluatedContext.source}): {report.explicacao}");
            if (platformOk)
            {
                lines.Add(
                    $"• Plataforma ({DescribeUnit(platform.transporter)}): " +
                    $"slot {platform.slotIndex} — {platform.reason}");
            }
            if (!hexOk)
                lines.Add($"Superfície recusou: {report.explicacao}");

            EditorGUILayout.HelpBox(
                string.Join("\n", lines), MessageType.Info);
        }
        else
        {
            var lines = new List<string>
            {
                "NÃO PODE POUSAR",
                $"• Superfície ({evaluatedContext.source}): {report.explicacao}"
            };
            if (commonGateOk && supportsLandingLayer && !occupancyAllowed)
            {
                lines.Add(
                    "  ↳ recusa é de OCUPAÇÃO, não de terreno: o hex aceitaria " +
                    $"o pouso em {landingDomain}/{landingHeight}, mas " +
                    $"{DescribeUnit(occupancyBlocker)} já ocupa a banda " +
                    $"{OccupancyResolver.GetHeightBand(landingDomain, landingHeight)}.");
            }
            if (platforms.Count == 0)
                lines.Add("• Plataforma: nenhum transportador no hex avaliado.");
            else
                for (int i = 0; i < platforms.Count; i++)
                    lines.Add(
                        $"• Plataforma ({DescribeUnit(platforms[i].transporter)}): " +
                        platforms[i].reason);

            EditorGUILayout.HelpBox(
                string.Join("\n", lines), MessageType.Warning);
        }

        EditorGUILayout.LabelField(
            "Camada após pousar",
            $"{landingDomain} / {landingHeight}" +
            $"  (banda {OccupancyResolver.GetHeightBand(landingDomain, landingHeight)})");
    }

    private void DrawDetails()
    {
        EditorGUILayout.Space(8f);
        detailsScroll = EditorGUILayout.BeginScrollView(
            detailsScroll, GUILayout.MinHeight(240f));

        DrawCommonGates();
        DrawLayerAndOccupancyGate();
        DrawSurfaceContext();
        DrawPlatformContext();
        DrawAircraftSkills();

        EditorGUILayout.EndScrollView();
    }

    // Segunda e terceira etapas do funil real de AircraftOperationRules.Evaluate:
    // a camada resolvida precisa ser suportada pela aeronave E a banda de destino
    // precisa estar livre. Pousar na planicie e legal pelo terreno e ilegal pela
    // ocupacao — sao recusas diferentes e aparecem separadas.
    private void DrawLayerAndOccupancyGate()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            $"Camada e ocupação do hex {evaluatedCell}", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        HeightBand targetBand =
            OccupancyResolver.GetHeightBand(landingDomain, landingHeight);
        EditorGUILayout.LabelField(
            "Camada de pouso",
            $"{landingDomain} / {landingHeight} — banda {targetBand} ({resolvedLayerSource})");
        EditorGUILayout.LabelField(
            supportsLandingLayer
                ? "✓ Aeronave suporta a camada de pouso"
                : $"✗ Aeronave não suporta {landingDomain}/{landingHeight}",
            supportsLandingLayer ? EditorStyles.boldLabel : EditorStyles.miniLabel);

        EditorGUILayout.LabelField(
            "Modelo de ocupação",
            OccupancyResolver.IsLayerAwareRulesActive
                ? $"Camadas ativas | inimigo divide banda={OccupancyResolver.AllowsEnemyShareInSameBand}"
                : "Camadas DESLIGADAS (EnableLayerOccupancyResolver=false) — ocupação não bloqueia");

        EditorGUILayout.LabelField(
            occupancyAllowed
                ? "✓ Banda de destino livre"
                : $"✗ Banda {targetBand} ocupada por {DescribeUnit(occupancyBlocker)}",
            occupancyAllowed ? EditorStyles.boldLabel : EditorStyles.miniLabel);

        if (hexOccupants.Count == 0)
        {
            EditorGUILayout.LabelField(
                "  Hex vazio (fora a própria aeronave).", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField(
                $"  Ocupantes do hex ({hexOccupants.Count}):", EditorStyles.miniLabel);
            for (int i = 0; i < hexOccupants.Count; i++)
            {
                UnitManager occupant = hexOccupants[i];
                if (occupant == null)
                    continue;
                HeightBand band = OccupancyResolver.GetHeightBand(occupant);
                bool sameBand = band == targetBand;
                bool ally = PlayerSlotRelations.AreAllies(occupant, aircraft);
                EditorGUILayout.LabelField(
                    $"    {(sameBand ? "▲" : "·")} {DescribeUnit(occupant)} — banda {band}" +
                    $", {(ally ? "aliado" : "inimigo")}" +
                    $"{(sameBand ? "  ← disputa a banda de pouso" : string.Empty)}",
                    sameBand ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
    }

    // Gates comuns aos dois caminhos: perfil aereo, voo atual e travas de camada.
    private void DrawCommonGates()
    {
        EditorGUILayout.LabelField(
            "Gates comuns (aeronave)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            commonGateOk ? "✓ Aeronave apta a tentar pouso" : "✗ Bloqueada",
            commonGateOk ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        if (!commonGateOk && !string.IsNullOrWhiteSpace(commonGateReason))
            EditorGUILayout.HelpBox(commonGateReason, MessageType.Warning);
        EditorGUILayout.EndVertical();
    }

    private void DrawSurfaceContext()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Superfície do hex", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.LabelField("Hierarquia vencedora", evaluatedContext.source.ToString());
        EditorGUILayout.LabelField(
            "Construção",
            evaluatedContext.construction != null
                ? evaluatedContext.construction.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Estrutura",
            evaluatedContext.structure != null
                ? evaluatedContext.structure.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Terreno",
            evaluatedContext.terrain != null
                ? evaluatedContext.terrain.displayName
                : "—");
        EditorGUILayout.LabelField(
            "Aircraft Ops",
            $"allow={landingEvaluation.allowed} | contexto permite={landingRequirement.contextAllows}" +
            $" | superfície={evaluatedContext.landingSurface}");

        DrawSurfaceSkillRule();

        if (!string.IsNullOrWhiteSpace(landingEvaluation.reason))
            EditorGUILayout.HelpBox(landingEvaluation.reason, MessageType.Warning);

        EditorGUILayout.EndVertical();
    }

    // A regra vem pronta do resolver (fonte + skills + conector), incluindo o
    // par Estrutura+Terreno resolvido por referencia OU por id do terreno.
    private void DrawSurfaceSkillRule()
    {
        if (!landingRequirement.Resolved)
        {
            EditorGUILayout.LabelField(
                "Regra de skills", landingRequirement.unresolvedReason);
            return;
        }

        if (!landingRequirement.HasExplicitSkills)
        {
            EditorGUILayout.LabelField(
                "Regra de skills",
                "Nenhuma skill explícita — vale o gate implícito abaixo");
            DrawImplicitSkillGate();
            return;
        }

        EditorGUILayout.LabelField(
            "Regra de skills",
            landingRequirement.requireAtLeastOne
                ? "OU — basta 1 das skills abaixo"
                : "E — exige todas as skills abaixo");
        DrawSkillChecklist(landingRequirement.requiredSkills);
    }

    private void DrawImplicitSkillGate()
    {
        string[] tokens =
            AirOperationResolver.GetImplicitAirOperationSkillTokens(evaluatedContext);
        EditorGUILayout.LabelField(
            "Gate implícito", "OU — basta 1 dos tokens abaixo");
        for (int i = 0; i < tokens.Length; i++)
        {
            bool has = aircraft != null
                && AirOperationResolver.UnitHasSkillToken(aircraft, tokens[i]);
            EditorGUILayout.LabelField(
                $"  {(has ? "✓" : "✗")} {tokens[i]}",
                has ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        }
    }

    private void DrawPlatformContext()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(
            "Plataformas transportadoras", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");

        if (platforms.Count == 0)
        {
            EditorGUILayout.LabelField(
                "  Nenhum transportador no hex avaliado.",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < platforms.Count; i++)
            DrawPlatform(platforms[i]);

        EditorGUILayout.EndVertical();
    }

    private void DrawPlatform(PlatformDiagnosis platform)
    {
        EditorGUILayout.LabelField(
            $"{(platform.available ? "✓" : "✗")} {DescribeUnit(platform.transporter)}" +
            $"{(platform.manualTarget ? "  (alvo manual)" : string.Empty)}",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"  {platform.reason}", EditorStyles.miniLabel);

        if (platform.slots.Count == 0)
        {
            EditorGUILayout.LabelField(
                "  Sem slots avaliados (recusa anterior ao laço de slots).",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);
            return;
        }

        if (!platform.transporter.TryGetUnitData(out UnitData transporterData)
            || transporterData == null
            || transporterData.transportSlots == null)
        {
            EditorGUILayout.Space(4f);
            return;
        }

        for (int i = 0; i < platform.slots.Count; i++)
        {
            PodePousarSlotReport slotReport = platform.slots[i];
            EditorGUILayout.LabelField(
                $"  {(slotReport.available ? "✓" : "✗")} slot {slotReport.slotIndex} " +
                $"\"{slotReport.slotId}\" ({slotReport.occupiedSeats}/{slotReport.capacity})",
                slotReport.available ? EditorStyles.boldLabel : EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"      {slotReport.reason}", EditorStyles.miniLabel);

            if (slotReport.slotIndex < 0
                || slotReport.slotIndex >= transporterData.transportSlots.Count)
                continue;

            UnitTransportSlotRule slot =
                transporterData.transportSlots[slotReport.slotIndex];
            if (slot == null)
                continue;

            // O slot nao tem flag de conector: requiredSkills e sempre OU.
            if (slot.requiredSkills != null && slot.requiredSkills.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "      Skills exigidas — OU, basta 1 (implícito no slot)",
                    EditorStyles.miniLabel);
                DrawSkillChecklist(slot.requiredSkills, "        ");
            }

            if (slot.blockedSkills != null && slot.blockedSkills.Count > 0)
            {
                EditorGUILayout.LabelField(
                    "      Skills bloqueadas — qualquer uma nega o pouso",
                    EditorStyles.miniLabel);
                for (int j = 0; j < slot.blockedSkills.Count; j++)
                {
                    SkillData blocked = slot.blockedSkills[j];
                    if (blocked == null)
                        continue;
                    bool has = aircraft != null && aircraft.HasSkill(blocked);
                    EditorGUILayout.LabelField(
                        $"        {(has ? "✗" : "✓")} {DescribeSkill(blocked)}",
                        EditorStyles.miniLabel);
                }
            }

            if (slot.allowedClasses != null && slot.allowedClasses.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"      Classes permitidas: {string.Join(", ", slot.allowedClasses)}",
                    EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawSkillChecklist(
        IReadOnlyList<SkillData> skills, string indent = "  ")
    {
        if (skills == null)
            return;

        for (int i = 0; i < skills.Count; i++)
        {
            SkillData skill = skills[i];
            if (skill == null)
                continue;
            bool has = aircraft != null && aircraft.HasSkill(skill);
            EditorGUILayout.LabelField(
                $"{indent}{(has ? "✓" : "✗")} {DescribeSkill(skill)}",
                has ? EditorStyles.boldLabel : EditorStyles.miniLabel);
        }
    }

    private void DrawAircraftSkills()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Skills da aeronave", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical("box");
        if (aircraft == null
            || !aircraft.TryGetUnitData(out UnitData data)
            || data == null
            || data.skills == null
            || data.skills.Count == 0)
        {
            EditorGUILayout.LabelField("  Nenhuma", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            return;
        }

        for (int i = 0; i < data.skills.Count; i++)
        {
            SkillData skill = data.skills[i];
            if (skill != null)
                EditorGUILayout.LabelField(
                    $"  • {DescribeSkill(skill)}", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }

    private static string DescribeSkill(SkillData skill)
    {
        if (skill == null)
            return "(skill nula)";
        return !string.IsNullOrWhiteSpace(skill.displayName)
            ? skill.displayName
            : skill.name;
    }

    private static string DescribeUnit(UnitManager unit)
    {
        if (unit == null)
            return "(unidade nula)";
        return !string.IsNullOrWhiteSpace(unit.UnitDisplayName)
            ? unit.UnitDisplayName
            : unit.name;
    }

    private void TryUseCurrentSelection()
    {
        GameObject selected = Selection.activeGameObject;
        UnitManager unit = selected != null
            ? selected.GetComponentInParent<UnitManager>()
            : null;
        if (unit == null)
            return;

        aircraft = unit;
        AutoDetect();
        ClearResult();
    }

    private void AutoDetect()
    {
        if (aircraft != null
            && aircraft.BoardTilemap != null)
            map = aircraft.BoardTilemap;

        if (map == null)
        {
            Tilemap[] maps =
                Object.FindObjectsByType<Tilemap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null
                    && maps[i].name == "Tilemap")
                {
                    map = maps[i];
                    break;
                }
            }
        }

        if (terrainDatabase == null)
        {
            string[] guids =
                AssetDatabase.FindAssets("t:TerrainDatabase");
            if (guids.Length > 0)
            {
                terrainDatabase =
                    AssetDatabase.LoadAssetAtPath<TerrainDatabase>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
    }

    private void ClearResult()
    {
        report = null;
        resolvedLayerSource = string.Empty;
        occupancyBlocker = null;
        occupancyAllowed = false;
        supportsLandingLayer = false;
        commonGateOk = false;
        commonGateReason = string.Empty;
        landingRequirement = default;
        hexOccupants.Clear();
        platforms.Clear();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!pickingDestination || map == null)
            return;

        Event current = Event.current;
        Ray ray =
            HandleUtility.GUIPointToWorldRay(
                current.mousePosition);
        Plane plane = new Plane(
            map.transform.forward,
            map.transform.position);
        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 world = ray.GetPoint(distance);
        hoverCell = map.WorldToCell(world);
        hoverCell.z = 0;
        Vector3 center = map.GetCellCenterWorld(hoverCell);

        Handles.color = new Color(1f, 0.8f, 0.1f);
        Handles.DrawWireDisc(
            center,
            map.transform.forward,
            Mathf.Max(
                map.cellSize.x,
                map.cellSize.y) * 0.45f);
        Handles.Label(center, $"Pouso {hoverCell}");
        HandleUtility.AddDefaultControl(
            GUIUtility.GetControlID(
                FocusType.Passive));

        if (current.type == EventType.MouseDown
            && current.button == 0
            && !current.alt)
        {
            destination = hoverCell;
            hasDestination = true;
            pickingDestination = false;
            ClearResult();
            current.Use();
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
