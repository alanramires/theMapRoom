using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Selecao runtime do quadrante. A cena de autoria nunca e carregada: o mapa de
/// campanha e reconstruido como mosaico dos bakes guardados no MundoData.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class CampaignSelectionController : MonoBehaviour
{
    private sealed class QuadrantEntry
    {
        public BlocoData Bloco;
        public CampanhaData Campanha;
        public QuadranteData Quadrante;
    }

    private sealed class ConstructionPreviewEntry
    {
        public int QuadrantIndex;
        public SpriteRenderer Renderer;
        public Color BaseColor;
    }

    [Header("Data")]
    [SerializeField] private MundoData mundo;
    [SerializeField] private ConstructionDatabase constructionDatabase;
    [SerializeField] private string battleSceneName = "Batalha";

    [Header("Scene References")]
    [SerializeField] private Tilemap worldTilemap;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private MatchController matchController;
    [SerializeField] private AIController aiController;

    [Header("Quadrant Presentation")]
    [Tooltip("Multiplicador aplicado ao terreno dos quadrantes que nao estao em foco.")]
    [SerializeField] private Color unfocusedQuadrantTint = new Color(0.62f, 0.66f, 0.70f, 1f);
    [Tooltip("Quanto a cor do vencedor substitui o branco do terreno conquistado.")]
    [Range(0f, 1f)]
    [SerializeField] private float winnerTintStrength = 0.7f;
    [Tooltip("Brilho de um territorio conquistado quando ele nao esta em foco.")]
    [Range(0f, 1f)]
    [SerializeField] private float unfocusedWinnerBrightness = 0.82f;
    [Tooltip("Brilho das construcoes assadas em um quadrante ainda neutro e fora de foco.")]
    [Range(0f, 1f)]
    [SerializeField] private float unfocusedConstructionBrightness = 0.62f;

    private readonly List<QuadrantEntry> quadrants = new List<QuadrantEntry>();
    private readonly List<ConstructionPreviewEntry> constructionPreviews = new List<ConstructionPreviewEntry>();
    private Transform constructionPreviewRoot;
    private QuadrantEntry hovered;
    private QuadrantEntry pending;
    private int selectedQuadrantIndex = -1;
    private int confirmationFocusIndex;
    private bool confirmationOpen;
    private bool launching;
    private bool selectionInputArmed;
    private bool confirmationSubmitArmed;
    private int sceneOpenedFrame;
    private int confirmationOpenedFrame;

    public bool IsConfirmationOpen => confirmationOpen;
    public int ConfirmationFocusIndex => confirmationFocusIndex;

    private void Awake()
    {
        ResolveReferences();
        DisableGameplayFogPresentation();
        BuildWorldMosaic();
    }

    private void Start()
    {
        ResolveReferences();
        if (cursorController != null)
            cursorController.enabled = false;
        FocusInitialQuadrant();
        RefreshHoveredQuadrant(force: true);

        // A cena pode ser carregada no mesmo frame do clique/Enter usado em INICIAR.
        // Antes de aceitar uma selecao, espera esse comando ser completamente solto.
        sceneOpenedFrame = Time.frameCount;
        selectionInputArmed = false;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
    }

    private void Update()
    {
        if (launching)
            return;

        if (!selectionInputArmed)
        {
            UiInputBlocker.SuppressGameplayInputForFrames(1);
            if (Time.frameCount <= sceneOpenedFrame + 1 || IsSubmitHeldNow())
                return;

            selectionInputArmed = true;
            return;
        }

        if (confirmationOpen)
        {
            // O painel de confirmacao tem prioridade sobre o cursor de gameplay.
            UiInputBlocker.SuppressGameplayInputForFrames(1);

            if (WasPreviousPressedThisFrame())
            {
                NavigateConfirmation(-1);
                return;
            }

            if (WasNextPressedThisFrame())
            {
                NavigateConfirmation(+1);
                return;
            }

            if (WasCancelPressedThisFrame())
            {
                CancelConfirmation();
                return;
            }

            if (!confirmationSubmitArmed)
            {
                if (Time.frameCount > confirmationOpenedFrame && !IsSubmitHeldNow())
                    confirmationSubmitArmed = true;
                return;
            }

            if (WasSubmitPressedThisFrame())
                InvokeConfirmationOption(confirmationFocusIndex);

            return;
        }

        if (WasQuadrantDirectionPressedThisFrame(out Vector2 direction))
        {
            MoveQuadrantSelection(direction);
            return;
        }

        if (!WasSubmitPressedThisFrame())
            return;

        // Enter seleciona provisoriamente. Impede que o mesmo Enter vaze para o
        // TurnStateManager que existe na cena-base de Campanha.
        UiInputBlocker.SuppressGameplayInputForFrames(1);
        OpenConfirmation();
    }

    public void NavigateConfirmation(int direction)
    {
        if (!confirmationOpen || direction == 0)
            return;

        confirmationFocusIndex = (confirmationFocusIndex + (direction > 0 ? 1 : -1) + 2) % 2;
        cursorController?.PlayCursorMoveSfx();
    }

    public void InvokeConfirmationOption(int index)
    {
        if (!confirmationOpen)
            return;

        confirmationFocusIndex = Mathf.Clamp(index, 0, 1);
        if (confirmationFocusIndex == 0)
        {
            // Tambem protege o callback do botao criado dinamicamente no PanelHelper:
            // um submit residual nunca pode comprometer a escolha provisoria.
            if (!confirmationSubmitArmed)
            {
                if (Time.frameCount <= confirmationOpenedFrame || IsSubmitHeldNow())
                    return;
                confirmationSubmitArmed = true;
            }

            LaunchSelectedQuadrant();
            return;
        }

        CancelConfirmation();
    }

    public string GetConfirmationSummary()
    {
        if (pending == null)
            return string.Empty;

        QuadranteData q = pending.Quadrante;
        int constructionCount = q.bakedConstrucoes != null ? q.bakedConstrucoes.Count : 0;
        return $"BLOCO: {pending.Bloco.displayName}\n" +
               $"CAMPANHA: {pending.Campanha.displayName}\n" +
               $"QUADRANTE: {q.displayName}\n" +
               $"TAMANHO: {q.width} x {q.height}\n" +
               $"CONSTRUÇÕES: {constructionCount}\n" +
               $"BAKE: {(q.HasBake ? "PRONTO" : "INDISPONÍVEL")}";
    }

    private void OpenConfirmation()
    {
        RefreshHoveredQuadrant(force: false);
        if (hovered == null)
            return;

        pending = hovered;
        confirmationOpen = true;
        confirmationFocusIndex = 0;
        confirmationSubmitArmed = false;
        confirmationOpenedFrame = Time.frameCount;
        cursorController?.PlayConfirmSfx();
        PanelHelperController.TrySetExternalText("CONFIRMAR QUADRANTE", string.Empty);
    }

    private void CancelConfirmation()
    {
        if (!confirmationOpen)
            return;

        confirmationOpen = false;
        confirmationFocusIndex = 0;
        confirmationSubmitArmed = false;
        pending = null;
        UiInputBlocker.SuppressGameplayInputForFrames(2);
        cursorController?.PlayCancelSfx();
        RefreshHoveredQuadrant(force: true);
    }

    private void LaunchSelectedQuadrant()
    {
        if (pending == null || launching)
            return;

        if (!pending.Quadrante.HasBake)
        {
            PanelHelperController.TrySetExternalText(
                "QUADRANTE INDISPONÍVEL",
                "Este quadrante ainda não possui bake para iniciar a batalha.");
            cursorController?.PlayCancelSfx();
            confirmationOpen = false;
            pending = null;
            return;
        }

        ResolveReferences();
        if (matchController == null)
        {
            PanelHelperController.TrySetExternalText(
                "ERRO DE CONFIGURAÇÃO",
                "MatchController não encontrado na cena Campanha.");
            cursorController?.PlayCancelSfx();
            return;
        }

        List<int> teamIds = new List<int>();
        List<bool> flipXs = new List<bool>();
        List<bool> isAIs = new List<bool>();
        List<int> startMoneys = new List<int>();
        List<int> actualMoneys = new List<int>();
        List<int> incomePerTurns = new List<int>();
        List<bool> startMoneyApplied = new List<bool>();
        matchController.ExportPlayersState(
            teamIds,
            flipXs,
            isAIs,
            startMoneys,
            actualMoneys,
            incomePerTurns,
            startMoneyApplied);

        if (teamIds.Count < 2)
        {
            PanelHelperController.TrySetExternalText(
                "ERRO DE CONFIGURAÇÃO",
                "A configuração copiada possui menos de dois jogadores.");
            cursorController?.PlayCancelSfx();
            return;
        }

        TeamId[] teams = new TeamId[teamIds.Count];
        bool[] commandsAutomatic = new bool[teamIds.Count];
        for (int i = 0; i < teamIds.Count; i++)
        {
            teams[i] = (TeamId)teamIds[i];
            commandsAutomatic[i] = matchController.IsPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(i));
        }

        PartidaConfig.Set(
            teamIds.Count,
            teams,
            isAIs.ToArray(),
            flipXs.ToArray(),
            matchController.GameSetup,
            commandsAutomatic,
            battleSceneName);
        PartidaConfig.SetDifficulty(ResolveDifficulty());
        PartidaConfig.SetQuadrante(pending.Campanha.campanhaId, pending.Quadrante.quadranteId);

        launching = true;
        cursorController?.PlayConfirmSfx();
        PanelHelperController.ClearExternalText();
        Debug.Log(
            $"[Campanha] JOGAR confirmado para '{pending.Campanha.campanhaId}/{pending.Quadrante.quadranteId}'. Abrindo '{battleSceneName}'.",
            this);
        SceneManager.LoadScene(battleSceneName);
    }

    private AIDifficulty ResolveDifficulty()
    {
        if (aiController != null)
            return aiController.AppliedDifficulty;

        switch (matchController.GameSetup)
        {
            case MatchController.GameSetupPreset.GameBoyClassic:
                return AIDifficulty.Iniciante;
            case MatchController.GameSetupPreset.NeblinaLeve:
                return AIDifficulty.Facil;
            default:
                return AIDifficulty.Competitiva;
        }
    }

    private void ResolveReferences()
    {
        if (worldTilemap == null)
        {
            Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] != null && string.Equals(maps[i].name, "TileMap", StringComparison.OrdinalIgnoreCase))
                {
                    worldTilemap = maps[i];
                    break;
                }
            }
        }

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (aiController == null)
            aiController = FindAnyObjectByType<AIController>();

        if (constructionDatabase == null)
        {
            ConstructionSpawner[] spawners =
                FindObjectsByType<ConstructionSpawner>(FindObjectsInactive.Include);
            for (int i = 0; i < spawners.Length; i++)
            {
                if (spawners[i] != null && spawners[i].ConstructionDatabase != null)
                {
                    constructionDatabase = spawners[i].ConstructionDatabase;
                    break;
                }
            }
        }
    }

    private void BuildWorldMosaic()
    {
        quadrants.Clear();
        if (mundo == null || worldTilemap == null)
        {
            Debug.LogError("[Campanha] MundoData ou TileMap não configurado.", this);
            return;
        }

        if (mundo.blocos != null)
        {
            for (int b = 0; b < mundo.blocos.Count; b++)
            {
                BlocoData bloco = mundo.blocos[b];
                if (bloco?.campanhas == null)
                    continue;

                for (int c = 0; c < bloco.campanhas.Count; c++)
                {
                    CampanhaData campanha = bloco.campanhas[c];
                    if (campanha?.quadrantes == null)
                        continue;

                    for (int q = 0; q < campanha.quadrantes.Count; q++)
                    {
                        QuadranteData quadrante = campanha.quadrantes[q];
                        if (quadrante == null)
                            continue;

                        quadrants.Add(new QuadrantEntry
                        {
                            Bloco = bloco,
                            Campanha = campanha,
                            Quadrante = quadrante
                        });
                    }
                }
            }
        }

        quadrants.Sort((left, right) =>
            string.CompareOrdinal(left.Quadrante.quadranteId, right.Quadrante.quadranteId));

        worldTilemap.ClearAllTiles();
        int painted = 0;
        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake)
                continue;

            for (int localY = 0; localY < q.height; localY++)
            {
                for (int localX = 0; localX < q.width; localX++)
                {
                    TileBase tile = q.GetBakedTile(localX, localY);
                    if (tile == null)
                        continue;

                    worldTilemap.SetTile(
                        new Vector3Int(q.originX + localX, q.originY + localY, 0),
                        tile);
                    painted++;
                }
            }
        }

        int constructionPreviews = BuildConstructionPreviews();
        RefreshQuadrantPresentation();
        worldTilemap.CompressBounds();
        FrameWorldInCamera();
        Debug.Log(
            $"[Campanha] Mosaico '{mundo.displayName}' construído: {quadrants.Count} quadrantes, " +
            $"{painted} tiles, {constructionPreviews} construções visuais.",
            this);
    }

    /// <summary>
    /// Representa as construcoes assadas no mapa de selecao sem instanciar o
    /// prefab jogavel. Estes objetos possuem apenas SpriteRenderer: nao entram em
    /// captura, renda, ocupacao, FOW, IA nem em qualquer cache da partida.
    /// </summary>
    private int BuildConstructionPreviews()
    {
        ResetConstructionPreviewRoot();

        if (constructionDatabase == null)
        {
            Debug.LogWarning(
                "[Campanha] ConstructionDatabase nao configurado; o mosaico sera exibido sem construcoes.",
                this);
            return 0;
        }

        var occupiedCells = new HashSet<Vector3Int>();
        var missingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int built = 0;

        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (!q.HasBake || q.bakedConstrucoes == null)
                continue;

            for (int c = 0; c < q.bakedConstrucoes.Count; c++)
            {
                ConstrucaoAssada baked = q.bakedConstrucoes[c];
                if (baked == null || string.IsNullOrWhiteSpace(baked.constructionId))
                    continue;

                if (baked.localX < 0 || baked.localX >= q.width ||
                    baked.localY < 0 || baked.localY >= q.height)
                {
                    Debug.LogWarning(
                        $"[Campanha] Construcao assada '{baked}' fora do retangulo de '{q.quadranteId}'. " +
                        "Ela nao sera exibida; asse o quadrante novamente.",
                        this);
                    continue;
                }

                Vector3Int globalCell = new Vector3Int(
                    q.originX + baked.localX,
                    q.originY + baked.localY,
                    0);

                if (!worldTilemap.HasTile(globalCell))
                {
                    Debug.LogWarning(
                        $"[Campanha] Construcao '{baked.constructionId}' em {globalCell} nao possui terreno " +
                        "no mosaico e nao sera exibida.",
                        this);
                    continue;
                }

                if (!constructionDatabase.TryGetById(baked.constructionId, out ConstructionData data))
                {
                    if (missingIds.Add(baked.constructionId))
                    {
                        Debug.LogWarning(
                            $"[Campanha] Construcao '{baked.constructionId}' nao existe no catalogo visual.",
                            this);
                    }
                    continue;
                }

                Sprite sprite = TeamUtils.GetTeamSprite(data, baked.teamId);
                if (sprite == null)
                {
                    if (missingIds.Add(baked.constructionId + "#sprite"))
                    {
                        Debug.LogWarning(
                            $"[Campanha] Construcao '{baked.constructionId}' nao possui sprite para exibir.",
                            data);
                    }
                    continue;
                }

                // Quadrantes podem se sobrepor na autoria. No mosaico global uma
                // mesma celula continua comportando uma unica construcao visual.
                // So reserva a celula depois que id e sprite realmente resolveram.
                if (!occupiedCells.Add(globalCell))
                {
                    Debug.LogWarning(
                        $"[Campanha] Mais de uma construcao assada ocupa {globalCell}; " +
                        $"mantendo a primeira e ignorando '{baked.constructionId}'.",
                        this);
                    continue;
                }

                var preview = new GameObject($"{baked.constructionId} @ {globalCell.x},{globalCell.y}");
                preview.transform.SetParent(constructionPreviewRoot, worldPositionStays: false);
                preview.transform.position = worldTilemap.GetCellCenterWorld(globalCell);

                SpriteRenderer renderer = preview.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                Color baseColor = TeamUtils.GetColor(baked.teamId);
                renderer.color = baseColor;
                renderer.flipX = TeamUtils.ShouldFlipX(baked.teamId);
                renderer.sortingLayerName = "Construcao";
                renderer.sortingOrder = 5;
                constructionPreviews.Add(new ConstructionPreviewEntry
                {
                    QuadrantIndex = i,
                    Renderer = renderer,
                    BaseColor = baseColor
                });
                built++;
            }
        }

        return built;
    }

    private void ResetConstructionPreviewRoot()
    {
        constructionPreviews.Clear();

        if (constructionPreviewRoot != null)
        {
            constructionPreviewRoot.gameObject.SetActive(false);
            Destroy(constructionPreviewRoot.gameObject);
        }

        var root = new GameObject("Campaign Construction Previews");
        Transform parent = worldTilemap != null && worldTilemap.layoutGrid != null
            ? worldTilemap.layoutGrid.transform
            : transform;
        root.transform.SetParent(parent, worldPositionStays: false);
        constructionPreviewRoot = root.transform;
    }

    private void FocusInitialQuadrant()
    {
        if (quadrants.Count <= 0)
            return;

        SelectQuadrant(0, playMoveSfx: false, adjustCamera: false);
    }

    private void MoveQuadrantSelection(Vector2 direction)
    {
        if (quadrants.Count <= 0)
            return;

        if (selectedQuadrantIndex < 0 || selectedQuadrantIndex >= quadrants.Count)
        {
            SelectQuadrant(0, playMoveSfx: true, adjustCamera: true);
            return;
        }

        direction = direction.normalized;
        Vector2 origin = GetQuadrantCenter(quadrants[selectedQuadrantIndex].Quadrante);
        int bestIndex = -1;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < quadrants.Count; i++)
        {
            if (i == selectedQuadrantIndex)
                continue;

            Vector2 delta = GetQuadrantCenter(quadrants[i].Quadrante) - origin;
            float forward = Vector2.Dot(delta, direction);
            if (forward <= 0.01f)
                continue;

            float lateral = Mathf.Abs(direction.x * delta.y - direction.y * delta.x);
            float score = lateral * 4f + forward;
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        // Sem vizinho nessa direcao: o cursor permanece no limite do mapa de quadrantes.
        if (bestIndex >= 0)
            SelectQuadrant(bestIndex, playMoveSfx: true, adjustCamera: true);
    }

    private void SelectQuadrant(int index, bool playMoveSfx, bool adjustCamera)
    {
        if (index < 0 || index >= quadrants.Count)
            return;

        selectedQuadrantIndex = index;
        hovered = quadrants[index];
        RefreshQuadrantPresentation();
        PositionCursorOnQuadrant(hovered.Quadrante, adjustCamera);
        if (playMoveSfx)
            cursorController?.PlayCursorMoveSfx();
        RefreshHoveredQuadrant(force: true);
    }

    /// <summary>
    /// O retangulo assado e a propria mascara do quadrante: foco e dominio sao
    /// apenas cor de apresentacao e nunca alteram o MundoData nem o bake.
    /// </summary>
    private void RefreshQuadrantPresentation()
    {
        if (worldTilemap == null)
            return;

        for (int i = 0; i < quadrants.Count; i++)
        {
            QuadranteData q = quadrants[i].Quadrante;
            if (q == null || !q.HasBake)
                continue;

            Color tint = ResolveQuadrantTerrainTint(i);
            for (int localY = 0; localY < q.height; localY++)
            {
                for (int localX = 0; localX < q.width; localX++)
                {
                    Vector3Int cell = new Vector3Int(q.originX + localX, q.originY + localY, 0);
                    if (!worldTilemap.HasTile(cell))
                        continue;

                    // Tiles de autoria podem vir com LockColor. O tint desta cena
                    // e runtime e precisa ser livre sem tocar no asset compartilhado.
                    worldTilemap.SetTileFlags(cell, TileFlags.None);
                    worldTilemap.SetColor(cell, tint);
                }
            }
        }

        for (int i = 0; i < constructionPreviews.Count; i++)
        {
            ConstructionPreviewEntry preview = constructionPreviews[i];
            if (preview?.Renderer == null)
                continue;

            bool focused = preview.QuadrantIndex == selectedQuadrantIndex;
            bool hasOwner = TryGetQuadrantOwner(preview.QuadrantIndex, out _);
            float brightness = focused
                ? 1f
                : hasOwner ? unfocusedWinnerBrightness : unfocusedConstructionBrightness;
            preview.Renderer.color = ScaleRgb(preview.BaseColor, brightness);
        }
    }

    private Color ResolveQuadrantTerrainTint(int index)
    {
        bool focused = index == selectedQuadrantIndex;
        if (!TryGetQuadrantOwner(index, out TeamId owner))
            return focused ? Color.white : unfocusedQuadrantTint;

        Color winnerTint = Color.Lerp(Color.white, TeamUtils.GetColor(owner), winnerTintStrength);
        return focused ? winnerTint : ScaleRgb(winnerTint, unfocusedWinnerBrightness);
    }

    private bool TryGetQuadrantOwner(int index, out TeamId owner)
    {
        owner = TeamId.Neutral;
        if (mundo == null || index < 0 || index >= quadrants.Count)
            return false;

        QuadrantEntry entry = quadrants[index];
        return CampaignProgressStore.TryGetOwner(
            mundo.mundoId,
            entry.Campanha.campanhaId,
            entry.Quadrante.quadranteId,
            out owner);
    }

    private static Color ScaleRgb(Color color, float scale)
    {
        scale = Mathf.Clamp01(scale);
        return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
    }

    private void PositionCursorOnQuadrant(QuadranteData q, bool adjustCamera)
    {
        if (q == null || cursorController == null || worldTilemap == null)
            return;

        if (TryFindQuadrantFocusCell(q, out Vector3Int cell) &&
            cursorController.SetCell(cell, playMoveSfx: false, adjustCamera: adjustCamera))
            return;

        // Quadrantes sem bake continuam selecionaveis e aparecem como indisponiveis.
        // Como nao existe tile valido para SetCell, posiciona apenas a apresentacao.
        Vector3Int centerCell = new Vector3Int(q.originX + q.width / 2, q.originY + q.height / 2, 0);
        cursorController.transform.position = worldTilemap.GetCellCenterWorld(centerCell);
        if (adjustCamera)
            cursorController.TryAdjustCameraToCursor();
    }

    private static Vector2 GetQuadrantCenter(QuadranteData q)
    {
        return new Vector2(q.originX + q.width * 0.5f, q.originY + q.height * 0.5f);
    }

    private bool TryFindQuadrantFocusCell(QuadranteData q, out Vector3Int cell)
    {
        cell = new Vector3Int(q.originX + q.width / 2, q.originY + q.height / 2, 0);
        if (worldTilemap != null && worldTilemap.HasTile(cell))
            return true;

        for (int y = 0; y < q.height; y++)
        {
            for (int x = 0; x < q.width; x++)
            {
                cell = new Vector3Int(q.originX + x, q.originY + y, 0);
                if (worldTilemap != null && worldTilemap.HasTile(cell))
                    return true;
            }
        }

        return false;
    }

    private void RefreshHoveredQuadrant(bool force)
    {
        QuadrantEntry next = selectedQuadrantIndex >= 0 && selectedQuadrantIndex < quadrants.Count
            ? quadrants[selectedQuadrantIndex]
            : null;
        if (!force && ReferenceEquals(next, hovered))
            return;

        hovered = next;
        if (hovered == null)
        {
            PanelHelperController.TrySetExternalText(
                mundo != null ? mundo.displayName.ToUpperInvariant() : "CAMPANHA",
                "Navegue até um quadrante disponível.");
            return;
        }

        QuadranteData q = hovered.Quadrante;
        string bake = q.HasBake ? string.Empty : "\n<color=#FF8888>SEM BAKE</color>";
        PanelHelperController.TrySetExternalText(
            hovered.Bloco.displayName.ToUpperInvariant(),
            $"{hovered.Campanha.displayName}\n\nQUADRANTE: {q.displayName}\n{q.descricao}{bake}\n\nSETAS: MUDAR QUADRANTE | ENTER: SELECIONAR");
    }

    private void FrameWorldInCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || worldTilemap == null || worldTilemap.cellBounds.size.x <= 0)
            return;

        Bounds localBounds = worldTilemap.localBounds;
        Vector3 center = worldTilemap.transform.TransformPoint(localBounds.center);
        Vector3 size = worldTilemap.transform.TransformVector(localBounds.size);
        float aspect = Mathf.Max(0.1f, cam.aspect);
        float halfHeight = Mathf.Abs(size.y) * 0.5f;
        float halfWidthByAspect = Mathf.Abs(size.x) * 0.5f / aspect;
        cam.orthographicSize = Mathf.Max(1f, Mathf.Max(halfHeight, halfWidthByAspect) + 0.75f);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);
    }

    private void DisableGameplayFogPresentation()
    {
        FogOfWarController[] controllers =
            FindObjectsByType<FogOfWarController>(FindObjectsInactive.Include);
        for (int i = 0; i < controllers.Length; i++)
            if (controllers[i] != null)
                controllers[i].gameObject.SetActive(false);

        Tilemap[] maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Include);
        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && string.Equals(maps[i].name, "FogOfWar", StringComparison.OrdinalIgnoreCase))
                maps[i].gameObject.SetActive(false);
        }
    }

    private static bool WasSubmitPressedThisFrame()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool IsSubmitHeldNow()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
            return true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
            return true;
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) ||
               Input.GetKey(KeyCode.JoystickButton0) || Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private static bool WasCancelPressedThisFrame()
    {
        if (RemoteInput.CancelDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private static bool WasPreviousPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.leftArrowKey.wasPressedThisFrame ||
             Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) ||
               Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A);
    }

    private static bool WasNextPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.rightArrowKey.wasPressedThisFrame ||
             Keyboard.current.sKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) ||
               Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D);
    }

    private static bool WasQuadrantDirectionPressedThisFrame(out Vector2 direction)
    {
        direction = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
                direction += Vector2.up;
            if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
                direction += Vector2.down;
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                direction += Vector2.left;
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                direction += Vector2.right;
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.dpad.up.wasPressedThisFrame || Gamepad.current.leftStick.up.wasPressedThisFrame)
                direction += Vector2.up;
            if (Gamepad.current.dpad.down.wasPressedThisFrame || Gamepad.current.leftStick.down.wasPressedThisFrame)
                direction += Vector2.down;
            if (Gamepad.current.dpad.left.wasPressedThisFrame || Gamepad.current.leftStick.left.wasPressedThisFrame)
                direction += Vector2.left;
            if (Gamepad.current.dpad.right.wasPressedThisFrame || Gamepad.current.leftStick.right.wasPressedThisFrame)
                direction += Vector2.right;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) direction += Vector2.up;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) direction += Vector2.down;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) direction += Vector2.left;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) direction += Vector2.right;
#endif
        return direction.sqrMagnitude > 0.01f;
    }
}
