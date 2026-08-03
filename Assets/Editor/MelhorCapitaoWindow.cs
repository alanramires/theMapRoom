using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class MelhorCapitaoWindow : EditorWindow
{
    /// <summary>
    /// Cada preset é a lista de atração de um papel, copiada da tabela de
    /// "Atração dos Papéis" da governança. Elas moram AQUI, no chamador, e não
    /// no serviço — é o ponto inteiro do desenho.
    /// </summary>
    private enum Preset
    {
        AssaltoOuFireSupport,
        Antiaereo,
        AtaqueAereo,
        LogisticaOuEstoque,
        ComPlano_CapturadorDoSetorSenaoRepCell
    }

    [SerializeField] private UnitManager unit;
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private Preset preset = Preset.AssaltoOuFireSupport;
    [SerializeField] private ConstructionSector plannedSector =
        ConstructionSector.None;
    [SerializeField] private bool useCubicGeometry;
    [SerializeField] private bool useOriginOverride;
    [SerializeField] private Vector3Int originOverride;
    [SerializeField] private int listFontSize = 12;

    private MelhorCapitaoResult result;
    private MelhorCapitaoOption selected;
    private string status = "Selecione uma unidade em campo.";
    private Vector2 scroll;
    private GUIStyle listStyle;

    [MenuItem("Tools/Hotzone/Melhor Capitão")]
    public static void Open() =>
        GetWindow<MelhorCapitaoWindow>("Melhor Capitão").Show();

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        AutoDetect();
    }

    private void OnDisable() =>
        SceneView.duringSceneGui -= OnSceneGUI;

    private void OnSelectionChange()
    {
        TryUseSelection(silent: true);
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Melhor Capitão", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Quem esta unidade acompanha? Devolve UMA referência — é aquele " +
            "cara ou aquele prédio — e para aí.\n\n" +
            "Onde se posicionar em relação a ela (vanguarda, retaguarda, " +
            "flanco, hex exato) NÃO é deste serviço: é do papel, com a " +
            "Hotzone. O magnetismo dá a direção, não a casa.\n\n" +
            "A lista de atração é do chamador. O serviço não conhece papel " +
            "nenhum — trocar de papel é trocar a lista.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        unit = (UnitManager)EditorGUILayout.ObjectField(
            "Unidade", unit, typeof(UnitManager), true);
        tilemap = (Tilemap)EditorGUILayout.ObjectField(
            "Tabuleiro", tilemap, typeof(Tilemap), true);
        preset = (Preset)EditorGUILayout.EnumPopup(
            new GUIContent(
                "Lista de atração",
                "Copiada da tabela de Atração dos Papéis da governança."),
            preset);
        if (preset == Preset.ComPlano_CapturadorDoSetorSenaoRepCell)
        {
            plannedSector = ConstructionSectorOrder.Popup(
                "Setor do plano", plannedSector);
        }
        useCubicGeometry = EditorGUILayout.Toggle(
            new GUIContent(
                "Medir em cúbica",
                "Desligado usa rota: quatro hexes atrás de uma serra ficam " +
                "mais longe que cinco de estrada. Aeronave já vem em cúbica " +
                "sozinha — isto aqui é para quando VOCÊ quer régua."),
            useCubicGeometry);
        DrawOriginOverride();
        if (EditorGUI.EndChangeCheck())
        {
            result = null;
            selected = null;
            AutoDetect();
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Usar Selecionado"))
            TryUseSelection(silent: false);
        if (GUILayout.Button("Auto Detect"))
            AutoDetect();
        EditorGUILayout.EndHorizontal();

        listFontSize = EditorGUILayout.IntSlider(
            "Fonte da lista", listFontSize, 9, 22);

        using (new EditorGUI.DisabledScope(unit == null || tilemap == null))
        {
            if (GUILayout.Button("Eleger Capitão", GUILayout.Height(30f)))
                Evaluate();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        if (result == null)
            return;

        EditorGUILayout.LabelField(
            "Rotas",
            $"{result.routeQueries} calculadas | " +
            $"{result.routeSkippedByBound} poupadas pelo limite cúbico");

        if (result.best != null)
        {
            EditorGUILayout.HelpBox(
                $"CAPITÃO: {DescribeOption(result.best)}" +
                (result.best.isEmbarked
                    ? "\n\nEle está EMBARCADO. Não se chega nele a pé — quem " +
                      "segue precisa pedir carona. O serviço aponta o " +
                      "transportador; mirar o hex atual dele ou o destino da " +
                      "viagem é decisão do papel."
                    : string.Empty),
                result.best.isEmbarked
                    ? MessageType.Warning
                    : MessageType.Info);
        }

        EditorGUILayout.HelpBox(
            "Âmbar: o eleito. Verde: mesma faixa de atração. Cinza: faixa " +
            "inferior. Linha tracejada da unidade até o eleito.\n" +
            "Clique numa linha para inspecionar e destacar na cena.",
            MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorCapitaoOption option = result.ranking[i];
            bool isSelected = selected == option;
            GUI.backgroundColor = isSelected
                ? new Color(1f, 0.8f, 0.2f)
                : Color.white;
            if (GUILayout.Button(
                    $"#{i + 1} [{option.attractionLabel}] " +
                    $"{DescribeOption(option)} | " +
                    $"dist={option.effectiveDistance:0.#} " +
                    $"pontos={option.displayScore}",
                    EditorStyles.miniButton))
            {
                selected = option;
                SceneView.RepaintAll();
            }
            GUI.backgroundColor = Color.white;

            if (!isSelected)
                continue;
            EditorGUILayout.LabelField(option.reason, ResolveListStyle());
            if (option.unit != null)
            {
                EditorGUILayout.ObjectField(
                    "   Unidade", option.unit, typeof(UnitManager), true);
            }
            if (option.construction != null)
            {
                EditorGUILayout.ObjectField(
                    "   Construção",
                    option.construction,
                    typeof(ConstructionManager),
                    true);
            }
            if (option.isEmbarked)
            {
                EditorGUILayout.ObjectField(
                    "   Carregado por",
                    option.carrier,
                    typeof(UnitManager),
                    true);
            }
            EditorGUILayout.LabelField(
                "   Hex", option.cell.ToString(), EditorStyles.miniLabel);
        }
        if (result.ranking.Count == 0)
        {
            EditorGUILayout.LabelField(
                "Nenhuma referência encontrada para esta lista de atração.",
                EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private static string DescribeOption(MelhorCapitaoOption option)
    {
        switch (option.kind)
        {
            case MelhorCapitaoKind.Unit:
                return option.unit != null
                    ? $"{option.unit.UnitDisplayName}#{option.unit.InstanceId} {option.cell}"
                    : $"unidade ? {option.cell}";
            case MelhorCapitaoKind.Construction:
                return option.construction != null
                    ? $"{option.construction.ConstructionDisplayName} {option.cell}"
                    : $"construção ? {option.cell}";
            default:
                return $"RepCell {option.cell}";
        }
    }

    private GUIStyle ResolveListStyle()
    {
        if (listStyle == null)
        {
            listStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };
        }
        listStyle.fontSize = Mathf.Clamp(listFontSize, 9, 22);
        return listStyle;
    }

    /// <summary>
    /// As listas da tabela de "Atração dos Papéis". Repare que nenhuma delas
    /// entra no serviço: elas nascem aqui e são passadas.
    /// </summary>
    private List<MelhorCapitaoAttraction> BuildAttractions()
    {
        var list = new List<MelhorCapitaoAttraction>();
        switch (preset)
        {
            case Preset.Antiaereo:
                list.Add(RoleAttraction(
                    "Vigilância", UnitRole.Vigilancia));
                list.Add(RoleAttraction("Capitão", UnitRole.Capturador));
                break;

            case Preset.AtaqueAereo:
                list.Add(RoleAttraction(
                    "Interceptador", UnitRole.Interceptador));
                list.Add(RoleAttraction("Capitão", UnitRole.Capturador));
                break;

            case Preset.LogisticaOuEstoque:
                list.Add(RoleAttraction("Capitão", UnitRole.Capturador));
                break;

            case Preset.ComPlano_CapturadorDoSetorSenaoRepCell:
                ConstructionSector wanted = plannedSector;
                list.Add(new MelhorCapitaoAttraction
                {
                    label = $"Capturador no setor {wanted}",
                    matchUnit = candidate =>
                        SatisfiesRole(candidate, UnitRole.Capturador)
                        && IsInSector(candidate, wanted),
                    // Com plano NÃO troca de capitão só porque o dele embarcou:
                    // segue de carona. É a única lista que liga isto.
                    allowEmbarked = true
                });
                if (SectorManager.TryGetSectorInfo(
                        wanted, out SectorManager.SectorInfo info)
                    && info != null)
                {
                    Vector3Int repCell = info.RepresentativeCell;
                    repCell.z = 0;
                    list.Add(new MelhorCapitaoAttraction
                    {
                        label = "RepCell (capitão abstrato)",
                        hasFixedCell = true,
                        fixedCell = repCell
                    });
                }
                break;

            default:
                list.Add(RoleAttraction("Capitão", UnitRole.Capturador));
                break;
        }
        return list;
    }

    private static MelhorCapitaoAttraction RoleAttraction(
        string label, UnitRole role)
    {
        return new MelhorCapitaoAttraction
        {
            label = label,
            matchUnit = candidate => SatisfiesRole(candidate, role)
        };
    }

    /// <summary>
    /// `CanSatisfy`, nunca `roles[0] ==`. O estrito barra especializações —
    /// CapturadorAgressivo deixaria de servir de capitão, e é exatamente o bug
    /// que sobrou num dos resolvedores antigos.
    /// </summary>
    private static bool SatisfiesRole(UnitManager candidate, UnitRole role)
    {
        return candidate != null
            && candidate.TryGetUnitData(out UnitData data)
            && data != null
            && UnitRoleCompatibility.CanSatisfy(data, role);
    }

    private static bool IsInSector(
        UnitManager candidate, ConstructionSector sector)
    {
        if (sector == ConstructionSector.None)
            return true;
        return Enum.TryParse(
                   candidate.AIAssignedPlanName,
                   true,
                   out ConstructionSector parsed)
               && parsed == sector;
    }

    private void Evaluate()
    {
        AutoDetect();
        selected = null;

        var allies = new List<UnitManager>();
        UnitManager[] found = FindObjectsByType<UnitManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            // Mesmo slot = mesmo lado. A ferramenta não inventa relação de
            // times: quem responde isso no jogo é PlayerSlotRelations.
            if (found[i] != null
                && found[i] != unit
                && PlayerSlotRelations.AreAllies(
                    unit.SlotIndex, found[i].SlotIndex))
            {
                allies.Add(found[i]);
            }
        }

        result = MelhorCapitaoService.Evaluate(new MelhorCapitaoRequest
        {
            unit = unit,
            originOverride = useOriginOverride
                ? originOverride
                : (Vector3Int?)null,
            allies = allies,
            constructions = CollectConstructions(),
            attractions = BuildAttractions(),
            useCubicGeometry = useCubicGeometry,
            diagnosticLog = message => status = message
        });

        SceneView.RepaintAll();
    }

    /// <summary>Runtime lê o registro vivo; Editor varre a cena.</summary>
    private List<ConstructionManager> CollectConstructions()
    {
        if (Application.isPlaying)
            return new List<ConstructionManager>(ConstructionManager.AllActive);

        var scene = new List<ConstructionManager>();
        ConstructionManager[] found = FindObjectsByType<ConstructionManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.activeInHierarchy)
                scene.Add(found[i]);
        }
        return scene;
    }

    private void DrawOriginOverride()
    {
        useOriginOverride = EditorGUILayout.ToggleLeft(
            "Usar hex de referência", useOriginOverride);
        using (new EditorGUI.DisabledScope(!useOriginOverride))
        {
            Vector3Int edited = EditorGUILayout.Vector3IntField(
                "Hex de referência", originOverride);
            edited.z = 0;
            originOverride = edited;
        }
    }

    private void AutoDetect()
    {
        if (unit != null && unit.BoardTilemap != null)
            tilemap = unit.BoardTilemap;
    }

    private void TryUseSelection(bool silent)
    {
        UnitManager picked = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<UnitManager>()
            : null;
        if (picked == null)
        {
            if (!silent)
                status = "O objeto selecionado não possui UnitManager.";
            return;
        }

        unit = picked;
        result = null;
        selected = null;
        AutoDetect();
        status = $"Unidade: {picked.name}.";
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (result == null || unit == null || tilemap == null)
            return;

        Vector3 from = tilemap.GetCellCenterWorld(result.origin);
        MelhorCapitaoOption champion = selected ?? result.best;
        int bestAttraction = result.best != null
            ? result.best.attractionIndex
            : 0;

        for (int i = 0; i < result.ranking.Count; i++)
        {
            MelhorCapitaoOption option = result.ranking[i];
            Vector3 to = tilemap.GetCellCenterWorld(option.cell);
            bool isChampion = option == champion;

            Handles.color = isChampion
                ? new Color(1f, 0.75f, 0.05f, 0.95f)
                : option.attractionIndex == bestAttraction
                    ? new Color(0.20f, 0.90f, 0.35f, 0.80f)
                    : new Color(0.55f, 0.55f, 0.55f, 0.70f);
            Handles.DrawSolidDisc(
                to, Vector3.back, isChampion ? 0.32f : 0.22f);

            // Anel branco = referência abstrata (RepCell), que não é ninguém.
            // Anel ciano = capitão embarcado: existe, mas só se alcança de
            // carona. Os dois anéis dizem "não marche direto para cá".
            if (option.kind == MelhorCapitaoKind.Cell || option.isEmbarked)
            {
                Handles.color = option.isEmbarked
                    ? new Color(0.20f, 0.85f, 1f, 0.95f)
                    : Color.white;
                Handles.DrawWireDisc(
                    to, Vector3.back, isChampion ? 0.38f : 0.28f);
            }

            if (isChampion)
            {
                Handles.color = new Color(1f, 0.75f, 0.05f, 0.95f);
                Handles.DrawDottedLine(from, to, 4f);
            }

            Handles.Label(
                to,
                $"{option.displayScore}\n{option.effectiveDistance:0.#}",
                ScoreLabelStyle(Color.black, isChampion ? 14 : 11));
        }
    }

    private static GUIStyle ScoreLabelStyle(Color color, int fontSize = 12) =>
        new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            normal = { textColor = color }
        };
}
