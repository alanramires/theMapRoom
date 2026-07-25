using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class TutorialManager : MonoBehaviour
{
    // Disparado quando um objetivo normal completa (nao dispara para condicoes de derrota).
    // Consumido pelo PanelDialogTutorialController para liberar os gates do roteiro.
    public static event System.Action<TutorialObjective> OnObjectiveCompleted;

    private static TutorialManager activeInstance;
    private bool endTurnLockedByScript;
    private bool automataCommandInProgress;
    private TutorialMovementEffect movementState = TutorialMovementEffect.NoEffect;

    // Passar a vez travado pelo estado persistente definido no roteiro do tutorial.
    // Sem TutorialManager na cena => nunca trava (partidas normais ilesas).
    public static bool IsEndTurnLockedByTutorial =>
        activeInstance != null &&
        (activeInstance.endTurnLockedByScript || activeInstance.automataCommandInProgress);

    private void BeginAutomataCommand()
    {
        automataCommandInProgress = true;
        Debug.Log("[TutorialManager] Comando automata em andamento: passar a vez permanece bloqueado.");
    }

    private void EndAutomataCommand()
    {
        automataCommandInProgress = false;
        Debug.Log("[TutorialManager] Comando automata concluido: passar a vez pode seguir o roteiro.");
    }

    public static void ApplyEndTurnEffectFromScript(TutorialEndTurnEffect effect)
    {
        if (activeInstance == null || effect == TutorialEndTurnEffect.NoEffect)
            return;

        bool locked = effect == TutorialEndTurnEffect.Locked;
        if (activeInstance.endTurnLockedByScript == locked)
            return;

        activeInstance.endTurnLockedByScript = locked;
        Debug.Log(locked
            ? "[TutorialManager] Passar a vez travado pelo roteiro."
            : "[TutorialManager] Passar a vez liberado pelo roteiro.");
    }

    // Movimento controlado pelo roteiro em tres niveis (ver TutorialMovementEffect).
    // So vale no turno do jogador (slot 0): o automata inimigo se move normalmente.
    // Locked: nem mover nem manter posicao. HoldOnly: manter/atacar parado sim, sair nao.
    public static bool IsMovementLockedByTutorial =>
        IsMovementStateActive(TutorialMovementEffect.Locked);

    public static bool IsLeaveCellLockedByTutorial =>
        IsMovementStateActive(TutorialMovementEffect.Locked) ||
        IsMovementStateActive(TutorialMovementEffect.HoldOnly) ||
        IsMovementStateActive(TutorialMovementEffect.AttackOnly);

    // Ordem de ataque (movement=Attack Only): finalizar parado ("apenas mover"/M)
    // desperdicaria a acao — o caminho liberado e o Mirar.
    public static bool IsFinalizeInPlaceBlockedByTutorial =>
        IsMovementStateActive(TutorialMovementEffect.AttackOnly);

    private static bool IsMovementStateActive(TutorialMovementEffect state)
    {
        if (activeInstance == null || activeInstance.movementState != state)
            return false;

        MatchController mc = activeInstance.matchController;
        if (mc == null)
            return true;
        return mc.ActiveTeamId == (int)mc.GetTeamIdForSlot(0);
    }

    public static void ApplyMovementEffectFromScript(TutorialMovementEffect effect)
    {
        if (activeInstance == null || effect == TutorialMovementEffect.NoEffect)
            return;
        if (activeInstance.movementState == effect)
            return;

        activeInstance.movementState = effect;
        Debug.Log($"[TutorialManager] Movimento agora: {effect} (pelo roteiro).");
    }

    // LEGADO (entradas com o bool unlockMovement): equivale a movement=Unlocked.
    public static void UnlockMovementFromScript()
    {
        ApplyMovementEffectFromScript(TutorialMovementEffect.Unlocked);
    }

    // Bloqueios declarados no TutorialData ativo (valem a cena inteira, nao destravam
    // com o roteiro). Sem TutorialManager/tutorial na cena => nada bloqueado.
    public static bool IsCommandServiceBlockedByTutorial
    {
        get { TutorialData t = GetActiveTutorialStatic(); return t != null && t.blockCommandService; }
    }

    public static bool IsRemoveUnitBlockedByTutorial
    {
        get { TutorialData t = GetActiveTutorialStatic(); return t != null && t.blockRemoveUnit; }
    }

    public static bool IsSurrenderBlockedByTutorial
    {
        get { TutorialData t = GetActiveTutorialStatic(); return t != null && t.blockSurrender; }
    }

    public static bool IsStatusSummaryBlockedByTutorial
    {
        get { TutorialData t = GetActiveTutorialStatic(); return t != null && t.blockStatusSummary; }
    }

    private static TutorialData GetActiveTutorialStatic()
    {
        return activeInstance != null ? activeInstance.GetActiveTutorial() : null;
    }

    // Ponto unico das broncas de acao bloqueada: texto/voz vem do TutorialData ativo
    // (secao "Broncas do Sargento"); texto vazio cai no padrao daqui.
    public static void ShowBlockedActionScold(TutorialScoldKind kind)
    {
        string text = GetDefaultScoldText(kind);
        AudioClip voice = null;

        TutorialData tutorial = GetActiveTutorialStatic();
        TutorialScoldEntry entry = tutorial != null ? tutorial.GetScold(kind) : null;
        if (entry != null)
        {
            if (!string.IsNullOrWhiteSpace(entry.text))
                text = entry.text;
            voice = entry.voice;
        }

        PanelDialogTutorialController.ShowBlockedActionMessage(text, voice);
    }

    private static string GetDefaultScoldText(TutorialScoldKind kind)
    {
        switch (kind)
        {
            case TutorialScoldKind.EndTurnLocked:
                return "Eu ainda estou falando, recruta! Aguarde a ordem para passar a vez.";
            case TutorialScoldKind.CommandService:
                return "Serviço do Comando? Você ainda não ganhou esse brinquedo, recruta.";
            case TutorialScoldKind.RemoveUnit:
                return "Dispensar unidade? Ninguém dispensa ninguém sem a minha ordem, recruta.";
            case TutorialScoldKind.Surrender:
                return "Render-se?! No meu treinamento?! Nem pensar, recruta.";
            case TutorialScoldKind.StatusSummary:
                return "Estatística é para depois. Foco na lição, recruta.";
            case TutorialScoldKind.MovementLocked:
                return "Quem mandou marchar, recruta?! Eu ainda não dei ordem de movimento.";
            case TutorialScoldKind.HoldPosition:
                return "Ninguém desce desse morro, recruta! A ordem é SEGURAR a posição.";
            case TutorialScoldKind.AttackOrdered:
                return "A ordem é MIRAR, recruta! Abra o comando de ataque e escolha o alvo.";
            default:
                return "O Sargento não autorizou isso, recruta.";
        }
    }

    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [Header("Tutorial Automation")]
    [SerializeField] private AutomataDatabase automataDatabase;
    [SerializeField] private bool enableTutorialAutomata = true;

    private readonly HashSet<string> spawnedObjectiveIds = new HashSet<string>();
    private Coroutine tutorialAutomataRoutine;
    private Camera zoomCamera;
    private float zoomBaselineOrthoSize = -1f;
    private const float ZoomDetectionEpsilon = 0.05f;
    private Vector3 panBaselinePosition;
    private bool panBaselineCaptured;
    private const float PanDetectionMinDelta = 0.75f;

    private void Awake()
    {
        activeInstance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void Start()
    {
        ResolveReferences();
        ResetTutorialObjectives();
        InitializeEndTurnLockFromScript();
        ProcessObjectiveSpawns();
    }

    private float unitAtHexPollTimer;

    private void Update()
    {
        CheckCameraZoomObjective();
        CheckCameraPanObjective();

        // UNIT_AT_HEX e checado por poll espacado (resolve nomes de construcao,
        // que custam FindObjectsByType) — 4x por segundo e mais que suficiente.
        unitAtHexPollTimer += Time.deltaTime;
        if (unitAtHexPollTimer >= 0.25f)
        {
            unitAtHexPollTimer = 0f;
            CheckUnitAtHexObjectives();
        }
    }

    // UNIT_AT_HEX valida no FIM da acao (HasActed na celula): chegar com a animacao
    // e ainda poder cancelar (rollback) nao conta. O poll cobre mover, mover+atacar,
    // "apenas mover" e desembarque — e so unidades do time do jogador contam.
    private void CheckUnitAtHexObjectives()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null || matchController == null)
            return;

        TeamId playerTeam = matchController.GetTeamIdForSlot(0);
        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null || obj.id != "UNIT_AT_HEX" || !obj.isVisible || !IsObjectivePending(obj))
                continue;

            List<UnitManager> units = UnitManager.AllActive;
            for (int u = 0; u < units.Count; u++)
            {
                UnitManager unit = units[u];
                if (unit == null || unit.IsDead || unit.IsEmbarked || !unit.HasActed)
                    continue;
                if (unit.TeamId != playerTeam)
                    continue;
                if (!IsUnitAtCoordinates(unit, obj.parameters))
                    continue;

                MarkObjectiveComplete(obj);
                break;
            }
        }
    }

    // Objetivo CAMERA_PAN: completa quando a camera se desloca do baseline (arrasto
    // com dedo/botao direito, ou seguindo o cursor). Com parameters "x,y", exige
    // tambem que o centro da camera chegue perto daquela celula (ex.: focar o Ryan).
    private void CheckCameraPanObjective()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
            return;

        TutorialObjective panObjective = null;
        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj != null && obj.id == "CAMERA_PAN" && obj.isVisible && IsObjectivePending(obj))
            {
                panObjective = obj;
                break;
            }
        }

        if (panObjective == null)
            return;

        if (!TryResolveZoomCamera())
            return;

        if (!panBaselineCaptured)
        {
            panBaselinePosition = zoomCamera.transform.position;
            panBaselineCaptured = true;
            return;
        }

        Vector3 position = zoomCamera.transform.position;
        float moved = Vector2.Distance(position, panBaselinePosition);
        if (moved <= PanDetectionMinDelta)
            return;

        if (!string.IsNullOrWhiteSpace(panObjective.parameters))
        {
            if (!TryGetCellWorldCenter(panObjective.parameters, out Vector3 target, out float hexSpacing))
                return;

            // Perto o suficiente = ~2.5 hexes do alvo.
            if (Vector2.Distance(position, target) > hexSpacing * 2.5f)
                return;
        }

        MarkObjectiveComplete(panObjective);
    }

    private bool TryResolveZoomCamera()
    {
        if (zoomCamera != null)
            return true;

        zoomCamera = Camera.main;
        if (zoomCamera == null)
            zoomCamera = FindAnyObjectByType<Camera>();
        return zoomCamera != null;
    }

    // Converte "x,y" para o centro da celula em mundo, usando o tilemap de qualquer
    // unidade ativa. Tambem devolve a largura de um hex para escalar tolerancias.
    private static bool TryGetCellWorldCenter(string coords, out Vector3 worldCenter, out float hexSpacing)
    {
        worldCenter = Vector3.zero;
        hexSpacing = 1f;

        string[] xy = coords.Split(',');
        if (xy.Length < 2)
            return false;
        if (!int.TryParse(xy[0].Trim(), out int x) || !int.TryParse(xy[1].Trim(), out int y))
            return false;

        Tilemap tilemap = null;
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null && units[i].BoardTilemap != null)
            {
                tilemap = units[i].BoardTilemap;
                break;
            }
        }

        if (tilemap == null)
            return false;

        Vector3Int cell = new Vector3Int(x, y, 0);
        worldCenter = HexCoordinates.GetCellCenterWorld(tilemap, cell);
        Vector3 neighbor = HexCoordinates.GetCellCenterWorld(tilemap, new Vector3Int(x + 1, y, 0));
        hexSpacing = Mathf.Max(0.1f, Vector2.Distance(worldCenter, neighbor));
        return true;
    }

    // Objetivo CAMERA_ZOOM: completa quando o orthographicSize muda em relacao ao
    // baseline capturado no primeiro frame em que a tarefa esta ativa (cobre a
    // bolinha do mouse e a pinca no toque, que mexem no mesmo valor).
    private void CheckCameraZoomObjective()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
            return;

        TutorialObjective zoomObjective = null;
        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj != null && obj.id == "CAMERA_ZOOM" && obj.isVisible && IsObjectivePending(obj))
            {
                zoomObjective = obj;
                break;
            }
        }

        if (zoomObjective == null)
            return;

        if (!TryResolveZoomCamera())
            return;

        if (zoomBaselineOrthoSize < 0f)
        {
            zoomBaselineOrthoSize = zoomCamera.orthographicSize;
            return;
        }

        if (Mathf.Abs(zoomCamera.orthographicSize - zoomBaselineOrthoSize) > ZoomDetectionEpsilon)
            MarkObjectiveComplete(zoomObjective);
    }

    // End Turn comeca livre e muda de estado conforme cada fala aparece.
    // Movimento preserva o modelo legado: se existe um destrave, comeca travado.
    // Aulas com roteiro tambem desligam o atalho contextual (clique inferindo acao),
    // senao o jogador dribla as travas — ele pode religar nas preferencias se quiser.
    private void InitializeEndTurnLockFromScript()
    {
        movementState = TutorialMovementEffect.NoEffect;
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.script == null)
            return;

        bool hasScript = tutorial.script.Count > 0;
        for (int i = 0; i < tutorial.script.Count; i++)
        {
            TutorialDialogEntry entry = tutorial.script[i];
            if (entry == null)
                continue;
            if (entry.unlockMovement || entry.movement == TutorialMovementEffect.Unlocked)
                movementState = TutorialMovementEffect.Locked;
        }

        if (hasScript && matchController != null && matchController.AtalhoContextual)
        {
            matchController.SetAtalhoContextual(false);
            Debug.Log("[TutorialManager] Atalho contextual desativado para a aula.");
        }
    }

    private void ResolveReferences()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (terrainDatabase == null)
        {
            if (matchController != null && matchController.TerrainDatabaseRef != null)
                terrainDatabase = matchController.TerrainDatabaseRef;
            else if (turnStateManager != null && turnStateManager.TerrainDatabaseRef != null)
                terrainDatabase = turnStateManager.TerrainDatabaseRef;
        }
    }

    private void ResetTutorialObjectives()
    {
        TutorialRules.ResetAllStates();
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial != null && tutorial.objectives != null)
        {
            for (int i = 0; i < tutorial.objectives.Count; i++)
            {
                TutorialObjective obj = tutorial.objectives[i];
                obj.hasFailed = false;
                
                // NOVO: Condições de derrota começam como "Concluídas" (OK)
                if (obj.isDefeatCondition)
                    obj.isCompleted = true;
                else
                    obj.isCompleted = false;

                obj.isVisible = !obj.startHidden;
            }

            RefreshObjectiveVisibility(tutorial);
        }
    }

    private void OnEnable()
    {
        TurnStateManager.OnUnitPurchased += HandleUnitPurchased;
        TurnStateManager.OnUnitInspected += HandleUnitInspected;
        TurnStateManager.OnAttackResolved += HandleAttackResolved;
        TurnStateManager.OnUnitDestroyed += HandleUnitDestroyed;
        TurnStateManager.OnUnitRevealedFromFog += HandleUnitRevealedFromFog;
        TurnStateManager.OnUnitMovementExecuted += HandleUnitMoved;
        TurnStateManager.OnUnitHeldPosition += HandleUnitHeldPosition;
        TurnStateManager.OnUnitSelected += HandleUnitSelected;
        TurnStateManager.OnUnitEmbarked += HandleUnitEmbarked;
        TurnStateManager.OnUnitDisembarked += HandleUnitDisembarked;
        TurnStateManager.OnUnitSupplied += HandleUnitSupplied;
        MatchController.OnBeforeAdvanceTurn += HandleTurnEnded;
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
    }

    private void OnDisable()
    {
        StopTutorialAutomataRoutine();
        TurnStateManager.OnUnitPurchased -= HandleUnitPurchased;
        TurnStateManager.OnUnitInspected -= HandleUnitInspected;
        TurnStateManager.OnAttackResolved -= HandleAttackResolved;
        TurnStateManager.OnUnitDestroyed -= HandleUnitDestroyed;
        TurnStateManager.OnUnitRevealedFromFog -= HandleUnitRevealedFromFog;
        TurnStateManager.OnUnitMovementExecuted -= HandleUnitMoved;
        TurnStateManager.OnUnitHeldPosition -= HandleUnitHeldPosition;
        TurnStateManager.OnUnitSelected -= HandleUnitSelected;
        TurnStateManager.OnUnitEmbarked -= HandleUnitEmbarked;
        TurnStateManager.OnUnitDisembarked -= HandleUnitDisembarked;
        TurnStateManager.OnUnitSupplied -= HandleUnitSupplied;
        MatchController.OnBeforeAdvanceTurn -= HandleTurnEnded;
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
    }

    private TutorialData GetActiveTutorial()
    {
        if (matchController == null)
            ResolveReferences();
        if (matchController == null)
            return null;
        return matchController.ActiveTutorial;
    }

    // Export/Import do tutorial ATIVO em JSON. Usa JsonUtility: serializa TODOS os campos
    // serializados sem mapeamento manual — o unico cuidado e o AudioClip 'voice', que vira caminho
    // de asset (JsonUtility grava referencia de objeto como instanceID, que nao sobrevive). Os
    // botoes de Save/Open estao no TutorialManagerEditor.

    // Monta o JSON do tutorial ATIVO. null se nao ha tutorial.
    public string BuildActiveTutorialJson()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null)
            return null;

        var dto = new TutorialExportDto { id = tutorial.id, description = tutorial.description };
        if (tutorial.objectives != null)
            for (int i = 0; i < tutorial.objectives.Count; i++)
                if (tutorial.objectives[i] != null)
                    dto.objectives.Add(tutorial.objectives[i]);
        if (tutorial.script != null)
            for (int i = 0; i < tutorial.script.Count; i++)
                if (tutorial.script[i] != null)
                    dto.script.Add(StepToDto(tutorial.script[i]));

        return JsonUtility.ToJson(dto, true);
    }

    // Copia uma fala para o DTO, trocando o AudioClip 'voice' pelo caminho do asset.
    private static TutorialStepDto StepToDto(TutorialDialogEntry e)
    {
        return new TutorialStepDto
        {
            advance = e.advance,
            objectiveKey = e.objectiveKey,
            revealObjective = e.revealObjective,
            waitObjectiveKey = e.waitObjectiveKey,
            waitObjectiveIndex = e.waitObjectiveIndex,
            waitAllUnitsActed = e.waitAllUnitsActed,
            waitPlayerTurnStart = e.waitPlayerTurnStart,
            text = e.text,
            voicePath = VoicePath(e.voice),
            spawnCommand = e.spawnCommand,
            statCommand = e.statCommand,
            turn = e.turn,
            movement = e.movement,
            unlockMovement = e.unlockMovement,
            revealObjectiveKey = e.revealObjectiveKey,
            revealObjectiveIndex = e.revealObjectiveIndex,
        };
    }

    // Caminho do AudioClip para o JSON. No editor usa o path do asset (round-trip completo); em
    // runtime cai no nome (o fluxo real de import/export e editor).
    private static string VoicePath(AudioClip clip)
    {
        if (clip == null)
            return "";
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.GetAssetPath(clip);
#else
        return clip.name;
#endif
    }

    // Asset do tutorial ativo (para o editor de import escrever de volta). Ver TutorialManagerEditor.
    public TutorialData ResolveActiveTutorialAsset() => GetActiveTutorial();

    // Nome-base sugerido para o arquivo exportado (id do tutorial saneado).
    public string GetActiveTutorialExportName()
    {
        TutorialData tutorial = GetActiveTutorial();
        string baseName = tutorial == null
            ? "tutorial"
            : (string.IsNullOrWhiteSpace(tutorial.id) ? tutorial.name : tutorial.id);
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(c, '_');
        return "tutorial_" + baseName;
    }

    // Export via ContextMenu (runtime): salva em persistentDataPath com timestamp e loga o caminho.
    // O botao do inspector (TutorialManagerEditor) usa Save File Panel para escolher o local.
    [ContextMenu("Exportar Tutorial (JSON)")]
    public void ExportActiveTutorialToJson()
    {
        string json = BuildActiveTutorialJson();
        if (json == null)
        {
            Debug.LogWarning("[TutorialManager] Export JSON: nenhum tutorial ativo (rode em Play ou garanta MatchController.ActiveTutorial).");
            return;
        }
        string path = System.IO.Path.Combine(
            Application.persistentDataPath,
            $"{GetActiveTutorialExportName()}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
        try
        {
            System.IO.File.WriteAllText(path, json, new System.Text.UTF8Encoding(true));
            Debug.Log($"[TutorialManager] Tutorial exportado: {path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[TutorialManager] Falha ao escrever JSON em {path}: {ex.Message}");
        }
    }

    private void MarkObjectiveComplete(TutorialObjective obj)
    {
        if (obj == null) return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null) return;

        if (obj.isDefeatCondition)
        {
            // Se já falhou, ignora
            if (obj.hasFailed) return;

            // FALHA: Marca como falhou e desmarca o check visual de OK
            obj.hasFailed = true;
            obj.isCompleted = false;
            Debug.Log($"[TutorialManager] Condição de derrota '{obj.id}' FALHOU!");
            
            // Tocar beep de erro ou sfx de derrota
            DeclareDefeat(tutorial, obj);
        }
        else
        {
            // Se já completou ou falhou, ignora
            if (obj.isCompleted || obj.hasFailed) return;

            // SUCESSO: Marca como completo
            obj.isCompleted = true;
            Debug.Log($"[TutorialManager] Objetivo '{obj.id}' completado!");

            // Tocar beep
            CursorController cursor = FindAnyObjectByType<CursorController>();
            if (cursor != null) cursor.PlayBeepSfx();

            OnObjectiveCompleted?.Invoke(obj);
            CheckTutorialCompletion();
        }

        // Delega regras especiais para TutorialRules (mantido por ID para compatibilidade se necessário)
        TutorialRules.CheckObjectiveRules(tutorial.id, obj.id);

        RefreshObjectiveVisibility(tutorial);
        ProcessObjectiveSpawns();
    }

    private bool IsObjectivePending(TutorialObjective obj)
    {
        if (obj == null) return false;
        
        // Se já falhou, não está mais pendente
        if (obj.hasFailed) return false;

        // Se é derrota e está como "concluído" (OK), ele ainda pode falhar
        if (obj.isDefeatCondition) return obj.isCompleted;
        
        // Se é comum e não está concluído, ele ainda pode ser realizado
        return !obj.isCompleted;
    }

    private void MarkObjectiveCompleteById(string id)
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            if (tutorial.objectives[i].id == id &&
                tutorial.objectives[i].isVisible &&
                IsObjectivePending(tutorial.objectives[i]))
            {
                MarkObjectiveComplete(tutorial.objectives[i]);
                break; // Apenas o primeiro encontrado para evitar conclusão múltipla indesejada
            }
        }
    }

    private void ProcessObjectiveSpawns()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;
        if (turnStateManager == null)
            ResolveReferences();
        if (turnStateManager == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null)
                continue;
            
            if (!obj.isVisible)
                continue;

            // Se o objetivo nao estiver pendente, pula para o proximo
            if (!IsObjectivePending(obj))
                continue;

            // Se o objetivo nao foi completado, ele e o "alvo" atual (ou um dos alvos ativos)
            // Verificamos se tem o prefixo "spawn:"
            if (!string.IsNullOrWhiteSpace(obj.parameters) && obj.parameters.StartsWith("spawn:", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!spawnedObjectiveIds.Contains(obj.id))
                {
                    if (ExecuteObjectiveSpawn(obj))
                    {
                        spawnedObjectiveIds.Add(obj.id);
                    }
                }
            }

            // O sistema de tutorial parece tratar o primeiro objetivo incompleto como o ativo.
            // Para simplificar, vamos parar no primeiro objetivo incompleto que processarmos (ou que nao tem spawn).
            // Se voce quiser que múltiplos spawnem de uma vez, remova o break.
            break; 
        }
    }

    private void RefreshObjectiveVisibility(TutorialData tutorial)
    {
        if (tutorial == null || tutorial.objectives == null || tutorial.objectives.Count <= 0)
            return;

        // Roteiro com reveals explicitos assume o controle: o manager nao revela
        // nada sozinho e a task list comeca vazia ("Aguardando proximo objetivo...").
        if (ScriptControlsObjectiveReveal(tutorial))
            return;

        // Progressao linear (legado): sempre revela o primeiro objetivo normal pendente.
        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null || obj.isDefeatCondition)
                continue;

            if (!IsObjectivePending(obj))
                continue;

            obj.isVisible = true;
            break;
        }
    }

    private static bool ScriptControlsObjectiveReveal(TutorialData tutorial)
    {
        if (tutorial == null || tutorial.script == null)
            return false;

        tutorial.MigrateLegacyDialogFlow();

        for (int i = 0; i < tutorial.script.Count; i++)
        {
            TutorialDialogEntry entry = tutorial.script[i];
            if (entry != null && entry.revealObjective)
                return true;
        }

        return false;
    }

    // Chamado pelo PanelDialogTutorialController quando uma fala com reveal aparece:
    // a tarefa entra na task list e spawns pendentes dela sao processados.
    public static void RevealObjectiveFromScript(int objectiveIndex)
    {
        if (activeInstance == null)
            return;

        TutorialData tutorial = activeInstance.GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
            return;
        if (objectiveIndex < 0 || objectiveIndex >= tutorial.objectives.Count)
            return;

        TutorialObjective obj = tutorial.objectives[objectiveIndex];
        if (obj == null || obj.isVisible)
            return;

        obj.isVisible = true;
        activeInstance.ProcessObjectiveSpawns();
    }

    private bool ExecuteObjectiveSpawn(TutorialObjective obj)
    {
        string raw = obj.parameters.Substring(6).Trim(); // Remove "spawn:"
        return ExecuteSpawnCommands(raw);
    }

    // Executa um ou mais comandos de spawn separados por ';'.
    // Formato de cada comando: TEAM_ID UNIT_TOKEN X,Y [acted]
    // TEAM_ID aceita numero ("1") ou slot logico ("slot0", "slot1") — prefira slots em
    // cenas de tutorial, pois a cor do jogador pode ser escolhida na Tela de Entrada.
    // O sufixo "acted" marca a unidade como "ja agiu" ao nascer.
    // Usado pelos objetivos (prefixo "spawn:") e pelas falas do roteiro (spawnCommand).
    public bool ExecuteSpawnCommands(string commands)
    {
        if (string.IsNullOrWhiteSpace(commands))
            return false;

        bool allSucceeded = true;
        bool anySucceeded = false;
        string[] list = commands.Split(';');
        for (int i = 0; i < list.Length; i++)
        {
            string command = list[i].Trim();
            if (command.Length <= 0)
                continue;
            if (TryExecuteSingleSpawnCommand(command))
                anySucceeded = true;
            else
                allSucceeded = false;
        }

        // Feedback sonoro de "unidade entrou em campo": mesmo done.mp3 da compra
        // na gameplay oficial. Um toque por lote (dois NPCs juntos nao tocam dobrado).
        if (anySucceeded)
        {
            CursorController cursor = FindAnyObjectByType<CursorController>();
            if (cursor != null) cursor.PlayDoneSfx();
        }

        return allSucceeded;
    }

    private bool TryExecuteSingleSpawnCommand(string command)
    {
        try
        {
            if (turnStateManager == null)
                ResolveReferences();
            if (turnStateManager == null)
                return false;

            string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                return false;

            int teamId;
            int logicalSlotIndex = -1;
            if (parts[0].StartsWith("slot", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(parts[0].Substring(4), out int slotIndex)) return false;
                if (matchController == null) return false;
                logicalSlotIndex = slotIndex;
                teamId = (int)matchController.GetTeamIdForSlot(slotIndex);
                if (teamId == (int)TeamId.Neutral) return false;
            }
            else if (!int.TryParse(parts[0], out teamId)) return false;

            string unitToken = parts[1];
            string coords = parts[2];

            // Opcionais apos as coordenadas: "acted" (nasce ja agiu), "name=Ryan"
            // (renomeia; use _ para espacos, ex.: name=Recruta_Ryan) e "cursor"
            // (move o cursor ate a unidade spawnada, com travel animado).
            bool markActed = false;
            bool moveCursorToSpawn = false;
            string customName = null;
            for (int i = 3; i < parts.Length; i++)
            {
                string option = parts[i];
                if (option.Equals("acted", System.StringComparison.OrdinalIgnoreCase))
                    markActed = true;
                else if (option.Equals("cursor", System.StringComparison.OrdinalIgnoreCase))
                    moveCursorToSpawn = true;
                else if (option.StartsWith("name=", System.StringComparison.OrdinalIgnoreCase))
                    customName = option.Substring(5).Replace('_', ' ');
            }

            string[] xy = coords.Split(',');
            if (xy.Length < 2) return false;
            if (!int.TryParse(xy[0].Trim(), out int x) || !int.TryParse(xy[1].Trim(), out int y))
                return false;

            Vector3Int cell = new Vector3Int(x, y, 0);
            if (turnStateManager.TrySpawnUnitAtCell(unitToken, teamId, cell, out string message))
            {
                UnitManager spawned = FindNewestActiveUnitAtCell(cell, (TeamId)teamId);
                if (spawned != null)
                {
                    if (logicalSlotIndex >= 0)
                        spawned.SetSlotIndex(logicalSlotIndex);
                    if (markActed)
                        spawned.MarkAsActed();
                    if (!string.IsNullOrWhiteSpace(customName))
                        spawned.SetUnitDisplayName(customName);
                    if (logicalSlotIndex >= 0)
                        spawned.ApplyTeamVisualFlipX(matchController.GetSlotFlipX(logicalSlotIndex));
                }
                if (moveCursorToSpawn)
                    StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(cell));
                Debug.Log($"[TutorialManager] Spawn executado: {message}");
                return true;
            }

            Debug.LogWarning($"[TutorialManager] Falha no spawn: {message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TutorialManager] Erro ao processar spawn '{command}': {e.Message}");
        }
        return false;
    }

    // Ajuda dos comandos de roteiro do tutorial. Exposta pelo comando de debug "TUTORIAL HELP" /
    // "HELP TUTORIAL" (ver PanelDebugController). Vive AQUI, ao lado do parser, para nao divergir
    // da sintaxe real (spawnCommand em ProcessSpawnCommand, statCommand em TryExecuteSingleStatCommand).
    public static string BuildTutorialCommandHelp()
    {
        return
            "COMANDOS DE ROTEIRO DO TUTORIAL (campos das falas em TutorialData)\n" +
            "Multiplos comandos no mesmo campo: separe por ';'.\n" +
            "\n" +
            "spawnCommand — nasce unidade(s):\n" +
            "  slotN SIGLA x,y      ex.: slot0 SD 1,3   (N tambem sem 'slot': 1 SD 5,6)\n" +
            "  opcoes apos as coordenadas:\n" +
            "    acted              nasce ja tendo agido\n" +
            "    name=Ryan          renomeia (use _ para espaco: name=Recruta_Ryan)\n" +
            "    cursor             move o cursor ate a unidade\n" +
            "  ex.: slot0 SD 1,3 name=Ryan cursor\n" +
            "\n" +
            "statCommand — ajustes e direcao de cena:\n" +
            "  NOME stat=valor      stat = hp | fuel | ammo   (NOME casa por nome/apelido/id)\n" +
            "  wake <alvo>          reativa (limpa 'ja agiu'). alvo: 1,3 | SD 1,3 | Ryan\n" +
            "  complete <key>       completa objetivo por key (ex.: complete hist_1_08)\n" +
            "  show <alvo>          torna construcao visivel. alvo: nome | x,y\n" +
            "  hide <alvo>          oculta construcao\n" +
            "  pan <alvo>           desliza SO a camera. alvo: x,y | unidade | construcao\n" +
            "  cursor <alvo>        move SO o cursor (cinematografico)\n" +
            "  zoom <valor>         orthographicSize exato (ex.: zoom 1.5 ou 1,5)\n" +
            "  slotN move ...       movimento scriptado\n" +
            "\n" +
            "Tarefas (objectives): o campo 'parameters' aceita 'spawn:...' (mesma sintaxe do spawnCommand).";
    }

    // Executa ajustes de status vindos do roteiro: "NOME stat=valor" separados por ';'.
    // Stats: hp, fuel (autonomia), ammo (municao). NOME casa por nome/apelido/id.
    public bool ExecuteStatCommands(string commands)
    {
        if (string.IsNullOrWhiteSpace(commands))
            return false;

        bool allSucceeded = true;
        string[] list = commands.Split(';');
        for (int i = 0; i < list.Length; i++)
        {
            string command = list[i].Trim();
            if (command.Length <= 0)
                continue;
            if (!TryExecuteSingleStatCommand(command))
                allSucceeded = false;
        }

        return allSucceeded;
    }

    private bool TryExecuteSingleStatCommand(string command)
    {
        string[] parts = command.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4 && parts[0].StartsWith("slot", System.StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("move", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteMoveCommand(command, parts);

        if (parts.Length < 2)
        {
            Debug.LogWarning($"[TutorialManager] statCommand invalido: '{command}' (esperado 'NOME stat=valor' ou 'wake ...').");
            return false;
        }

        // "wake 1,3" ou "wake SD 1,3" ou "wake Ryan": reativa a unidade (mesmo efeito
        // do debug "wake unit" — limpa fusao e o estado de "ja agiu").
        if (parts[0].Equals("wake", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteWakeCommand(command, parts);

        // "complete hist_1_08": completa um objetivo por KEY a partir do roteiro.
        // E o fim de tutorial scriptado: tarefas sem evento de jogo (ex.: ENDING)
        // sao dadas por cumpridas pelo proprio Sargento; se for a ultima pendente,
        // dispara a vitoria do tutorial normalmente.
        if (parts[0].Equals("complete", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteCompleteCommand(command, parts);

        // "show Bandeira" / "hide Bandeira" / "show 5,4": alterna o isVisible de uma
        // construcao (ex.: revelar a bandeira da montanha no momento certo do roteiro).
        if (parts[0].Equals("show", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteConstructionVisibilityCommand(command, parts, visible: true);
        if (parts[0].Equals("hide", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteConstructionVisibilityCommand(command, parts, visible: false);

        // "pan 5,4" / "pan Bandeira" / "pan Ryan": desliza SO A CAMERA ate a celula/
        // unidade/construcao (FocusOn). O cursor nao se move — com unidade selecionada
        // ele e preso a area de movimento, e o pan nao pode violar essa regra.
        if (parts[0].Equals("pan", System.StringComparison.OrdinalIgnoreCase))
            return TryExecutePanCommand(command, parts);

        // "cursor Ryan" / "cursor Ramelle" / "cursor 3,1": move apenas o cursor
        // cinematograficamente ate o alvo, sem executar clique ou selecionar nada.
        if (parts[0].Equals("cursor", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteCursorCommand(command, parts);

        // "zoom 1.5" (tambem aceita "zoom 1,5"): define o orthographicSize exato
        // da camera para enquadramentos cinematograficos do roteiro.
        if (parts[0].Equals("zoom", System.StringComparison.OrdinalIgnoreCase))
            return TryExecuteZoomCommand(command, parts);

        string token = parts[0];
        string assignment = parts[1];
        int equals = assignment.IndexOf('=');
        if (equals <= 0 || equals >= assignment.Length - 1)
        {
            Debug.LogWarning($"[TutorialManager] statCommand invalido: '{command}' (esperado 'NOME stat=valor').");
            return false;
        }

        string stat = assignment.Substring(0, equals).Trim().ToLowerInvariant();
        if (!int.TryParse(assignment.Substring(equals + 1).Trim(), out int value))
        {
            Debug.LogWarning($"[TutorialManager] statCommand invalido: '{command}' (valor nao numerico).");
            return false;
        }

        UnitManager unit = FindActiveUnitByToken(token);
        if (unit == null)
        {
            Debug.LogWarning($"[TutorialManager] statCommand: unidade '{token}' nao encontrada em campo.");
            return false;
        }

        switch (stat)
        {
            case "hp":
                unit.SetCurrentHP(value);
                return true;
            case "fuel":
            case "autonomia":
                unit.SetCurrentFuel(value);
                return true;
            case "ammo":
            case "municao":
            {
                // A barra azul le a municao das armas embarcadas (squadAmmunition),
                // nao o agregado do UnitManager — mesmo caminho do debug "set ammo".
                unit.SetCurrentAmmo(value);
                IReadOnlyList<UnitEmbarkedWeapon> weapons = unit.GetEmbarkedWeapons();
                if (weapons != null)
                {
                    for (int w = 0; w < weapons.Count; w++)
                    {
                        if (weapons[w] != null)
                            weapons[w].squadAmmunition = Mathf.Max(0, value);
                    }
                }
                unit.RefreshRuntimeVisualState();
                return true;
            }
            default:
                Debug.LogWarning($"[TutorialManager] statCommand: stat '{stat}' desconhecido (use hp, fuel ou ammo).");
                return false;
        }
    }

    private bool TryExecuteZoomCommand(string command, string[] parts)
    {
        if (parts.Length != 2)
        {
            Debug.LogWarning($"[TutorialManager] zoom invalido: '{command}' (use 'zoom 1.5').");
            return false;
        }

        string rawValue = parts[1].Replace(',', '.');
        if (!float.TryParse(
                rawValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out float value) || value <= 0f)
        {
            Debug.LogWarning($"[TutorialManager] zoom invalido: '{command}' (valor ilegivel).");
            return false;
        }

        if (panCameraController == null)
            panCameraController = FindAnyObjectByType<CameraController>();
        if (panCameraController == null)
        {
            Debug.LogWarning("[TutorialManager] zoom: CameraController nao encontrado na cena.");
            return false;
        }

        panCameraController.SetTutorialZoom(value);
        return true;
    }

    private bool TryExecuteMoveCommand(string command, string[] parts)
    {
        if (matchController == null || turnStateManager == null || automataDatabase == null)
        {
            Debug.LogWarning($"[TutorialManager] moveCommand sem referencias: '{command}'.");
            return false;
        }

        if (!int.TryParse(parts[0].Substring(4), out int slotIndex) || slotIndex < 0)
        {
            Debug.LogWarning($"[TutorialManager] moveCommand invalido: '{command}' (slot ilegivel).");
            return false;
        }

        if (!TryParseTutorialCell(parts[3], out Vector3Int fromCell))
        {
            Debug.LogWarning($"[TutorialManager] moveCommand invalido: '{command}' (origem ilegivel).");
            return false;
        }

        TeamId team = matchController.GetTeamIdForSlot(slotIndex);
        UnitManager unit = FindActiveUnitAtCell(fromCell);
        if (unit == null || unit.TeamId != team || !UnitMatchesTargetToken(unit, parts[1]))
        {
            Debug.LogWarning($"[TutorialManager] moveCommand: unidade '{parts[1]}' nao encontrada em {fromCell} no slot {slotIndex}.");
            return false;
        }

        TutorialData tutorial = GetActiveTutorial();
        if (!automataDatabase.TryResolve(unit, team, tutorial != null ? tutorial.id : string.Empty, out AutomataData automata) || automata == null)
        {
            Debug.LogWarning($"[TutorialManager] moveCommand: AutomataData nao encontrado para '{parts[1]}'.");
            return false;
        }

        Vector3Int destination;
        if (parts.Length >= 5 && !parts[4].Equals("attack", System.StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseTutorialCell(parts[4], out destination))
            {
                Debug.LogWarning($"[TutorialManager] moveCommand invalido: '{command}' (destino ilegivel).");
                return false;
            }
        }
        else if (!TryFindAutomataAdvanceCell(unit, automata, out destination))
        {
            Debug.LogWarning($"[TutorialManager] moveCommand: nenhuma celula alcancavel para '{parts[1]}'.");
            return false;
        }

        // O comando so ataca apos chegar se o roteiro pedir explicitamente
        // ('... move 7,-2 4,-2 attack'). O preferAttack do AutomataData vale para
        // a rotina de turno, nao para a marcha scriptada — senao o inimigo atira
        // na chegada e quebra o "ele ainda nao te viu".
        bool attackAfterMove = parts[parts.Length - 1].Equals("attack", System.StringComparison.OrdinalIgnoreCase);

        BeginAutomataCommand();
        StartCoroutine(ExecuteTutorialAutomataMoveAndAttack(unit, team, destination, attackAfterMove, automata.targetPreference, parts[1]));
        return true;
    }

    private IEnumerator ExecuteTutorialAutomataMoveAndAttack(
        UnitManager unit,
        TeamId team,
        Vector3Int destination,
        bool attackAfterMove,
        AutomataTargetPreference targetPreference,
        string unitToken)
    {
        try
        {
            bool succeeded = false;
            yield return ExecuteTutorialAutomataMoveBatch(unit, team, destination, value => succeeded = value);
            if (!succeeded || !attackAfterMove)
                yield break;

            if (!turnStateManager.TryAutomatedSelectUnitAndEnterMoveuParado(unit))
            {
                Debug.LogWarning($"[TutorialManager] moveCommand: nao foi possivel re-selecionar '{unitToken}' para ataque.");
                yield break;
            }

            if (!turnStateManager.TryExecuteAutomatedAttackWithPreference(targetPreference))
            {
                turnStateManager.HandleCancel();
                Debug.Log($"[TutorialManager] moveCommand: '{unitToken}' chegou ao destino sem alvo valido.");
                yield break;
            }

            yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 12f);
        }
        finally
        {
            EndAutomataCommand();
        }
    }

    private static bool TryParseTutorialCell(string value, out Vector3Int cell)
    {
        cell = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] coordinates = value.Trim().Split(',');
        if (coordinates.Length < 2 ||
            !int.TryParse(coordinates[0], out int x) ||
            !int.TryParse(coordinates[1], out int y))
            return false;

        cell = new Vector3Int(x, y, 0);
        return true;
    }

    private bool TryExecuteCompleteCommand(string command, string[] parts)
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
        {
            Debug.LogWarning($"[TutorialManager] complete: sem tutorial ativo para '{command}'.");
            return false;
        }

        int index = tutorial.FindObjectiveIndexByKey(parts[1]);
        if (index < 0)
        {
            Debug.LogWarning($"[TutorialManager] complete: objetivo com key '{parts[1]}' nao existe no tutorial ativo.");
            return false;
        }

        TutorialObjective objective = tutorial.objectives[index];
        // Garante o check visivel na task list mesmo se o roteiro nao revelou antes.
        objective.isVisible = true;
        MarkObjectiveComplete(objective);
        return true;
    }

    private bool TryExecuteWakeCommand(string command, string[] parts)
    {
        UnitManager unit = null;

        // Ultimo argumento com virgula = celula; o argumento do meio (se houver) e token de conferencia.
        string last = parts[parts.Length - 1];
        if (last.Contains(","))
        {
            string[] xy = last.Split(',');
            if (xy.Length < 2 ||
                !int.TryParse(xy[0].Trim(), out int x) ||
                !int.TryParse(xy[1].Trim(), out int y))
            {
                Debug.LogWarning($"[TutorialManager] wake invalido: '{command}' (celula ilegivel).");
                return false;
            }

            unit = FindActiveUnitAtCell(new Vector3Int(x, y, 0));
            if (unit != null && parts.Length > 2 && !UnitMatchesTargetToken(unit, parts[1]))
            {
                Debug.LogWarning($"[TutorialManager] wake: unidade em {last} nao casa com o token '{parts[1]}'.");
                return false;
            }
        }
        else
        {
            unit = FindActiveUnitByToken(parts[1]);
        }

        if (unit == null)
        {
            Debug.LogWarning($"[TutorialManager] wake: unidade nao encontrada para '{command}'.");
            return false;
        }

        // Mesmo efeito do debug "wake unit".
        if (unit.HasMerged)
            unit.ClearMergeAudit();
        unit.ResetActed();
        Debug.Log($"[TutorialManager] wake: {unit.name} reativada.");
        return true;
    }

    private CameraController panCameraController;

    private bool TryExecuteCursorCommand(string command, string[] parts)
    {
        if (parts.Length != 2)
        {
            Debug.LogWarning($"[TutorialManager] cursor invalido: '{command}' (use 'cursor Ryan', 'cursor Ramelle' ou 'cursor 3,1').");
            return false;
        }

        Vector3Int targetCell;
        string arg = parts[1].Trim();
        if (arg.Contains(","))
        {
            if (!TryParseTutorialCell(arg, out targetCell))
            {
                Debug.LogWarning($"[TutorialManager] cursor invalido: '{command}' (celula ilegivel).");
                return false;
            }
        }
        else
        {
            UnitManager unit = FindActiveUnitByToken(arg);
            if (unit != null)
            {
                targetCell = unit.CurrentCellPosition;
            }
            else
            {
                ConstructionManager construction = FindConstructionByName(arg);
                if (construction == null)
                {
                    Debug.LogWarning($"[TutorialManager] cursor: alvo '{arg}' nao encontrado (unidade ou construcao).");
                    return false;
                }
                targetCell = construction.CurrentCellPosition;
            }
        }

        targetCell.z = 0;
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null)
        {
            Debug.LogWarning("[TutorialManager] cursor: TurnStateManager nao encontrado na cena.");
            return false;
        }

        StartCoroutine(turnStateManager.MoveCursorToCellWithAutomatedTravel(targetCell));
        return true;
    }

    // Pan de camera puro (FocusOn, com clamp de borda): nao mexe no cursor.
    private bool TryExecutePanCommand(string command, string[] parts)
    {
        Vector3 target;
        string arg = parts[1].Trim();

        if (arg.Contains(","))
        {
            if (!TryGetCellWorldCenter(arg, out target, out _))
            {
                Debug.LogWarning($"[TutorialManager] pan invalido: '{command}' (celula ilegivel ou sem tilemap).");
                return false;
            }
        }
        else
        {
            UnitManager unit = FindActiveUnitByToken(arg);
            if (unit != null)
            {
                target = unit.transform.position;
            }
            else
            {
                ConstructionManager construction = FindConstructionByName(arg);
                if (construction == null)
                {
                    Debug.LogWarning($"[TutorialManager] pan: alvo '{arg}' nao encontrado (unidade ou construcao).");
                    return false;
                }
                target = construction.transform.position;
            }
        }

        if (panCameraController == null)
            panCameraController = FindAnyObjectByType<CameraController>();
        if (panCameraController == null)
        {
            Debug.LogWarning("[TutorialManager] pan: CameraController nao encontrado na cena.");
            return false;
        }

        // O MatchController reposiciona o foco no HQ logo depois de disparar
        // OnActiveTeamChanged. Aplicar no proximo frame deixa o foco do roteiro
        // vencer sem alterar o comportamento global de inicio de turno.
        StartCoroutine(ApplyTutorialPanNextFrame(target));
        return true;
    }

    private IEnumerator ApplyTutorialPanNextFrame(Vector3 target)
    {
        yield return null;
        if (panCameraController == null)
            panCameraController = FindAnyObjectByType<CameraController>();
        panCameraController?.FocusOn(target);
    }

    private static ConstructionManager FindConstructionByName(string token)
    {
        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager cm = constructions[i];
            if (cm != null && cm.name.Contains(token, System.StringComparison.OrdinalIgnoreCase))
                return cm;
        }

        return null;
    }

    private bool TryExecuteConstructionVisibilityCommand(string command, string[] parts, bool visible)
    {
        ConstructionManager target = null;
        string arg = parts[1].Trim();

        ConstructionManager[] constructions =
            FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (arg.Contains(","))
        {
            string[] xy = arg.Split(',');
            if (xy.Length < 2 ||
                !int.TryParse(xy[0].Trim(), out int x) ||
                !int.TryParse(xy[1].Trim(), out int y))
            {
                Debug.LogWarning($"[TutorialManager] {parts[0]} invalido: '{command}' (celula ilegivel).");
                return false;
            }

            Vector3Int cell = new Vector3Int(x, y, 0);
            for (int i = 0; i < constructions.Length; i++)
            {
                ConstructionManager cm = constructions[i];
                if (cm == null)
                    continue;

                Vector3Int cmCell = cm.CurrentCellPosition;
                cmCell.z = 0;
                if (cmCell == cell)
                {
                    target = cm;
                    break;
                }
            }
        }
        else
        {
            for (int i = 0; i < constructions.Length; i++)
            {
                ConstructionManager cm = constructions[i];
                if (cm != null && cm.name.Contains(arg, System.StringComparison.OrdinalIgnoreCase))
                {
                    target = cm;
                    break;
                }
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[TutorialManager] {parts[0]}: construcao nao encontrada para '{command}'.");
            return false;
        }

        target.SetVisible(visible);
        Debug.Log($"[TutorialManager] {parts[0]}: construcao '{target.name}' visivel={visible}.");
        return true;
    }

    private UnitManager FindActiveUnitByToken(string token)
    {
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;
            if (UnitMatchesTargetToken(unit, token))
                return unit;
        }

        return null;
    }

    private static UnitManager FindActiveUnitAtCell(Vector3Int cell)
    {
        cell.z = 0;
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            if (unitCell == cell)
                return unit;
        }

        return null;
    }

    private static UnitManager FindNewestActiveUnitAtCell(Vector3Int cell, TeamId team)
    {
        cell.z = 0;
        List<UnitManager> units = UnitManager.AllActive;
        for (int i = units.Count - 1; i >= 0; i--)
        {
            UnitManager unit = units[i];
            if (unit == null || unit.IsDead || unit.IsEmbarked || unit.TeamId != team)
                continue;

            Vector3Int unitCell = unit.CurrentCellPosition;
            unitCell.z = 0;
            if (unitCell == cell)
                return unit;
        }

        return null;
    }

    private void CheckTutorialCompletion()
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null)
                continue;

            if (!obj.isCompleted && !obj.isDefeatCondition && !obj.isOptional)
                return;
        }

        if (matchController != null && !matchController.HasVictoryWinner)
        {
            matchController.DeclareTutorialVictory(tutorial);
        }
    }

    private void DeclareDefeat(TutorialData tutorial, TutorialObjective objective)
    {
        Debug.Log($"[TutorialManager] Derrota disparada pelo objetivo: {objective.id}");
        if (matchController != null)
        {
            matchController.DeclareTutorialDefeat(tutorial, objective.description);
        }
    }

    private void HandleUnitPurchased(UnitManager unit)
    {
        MarkObjectiveCompleteById("PURCHASE_UNIT");
    }

    private void HandleUnitInspected(UnitManager unit)
    {
        if (matchController != null && unit != null)
        {
            if (unit.SlotIndex != matchController.ActiveSlotId.Value)
                MarkObjectiveCompleteById("INSPECT_ENEMY_UNIT");
            else
                MarkObjectiveCompleteById("INSPECT_ALLY_UNIT");
        }
    }

    private void HandleUnitHeldPosition(UnitManager unit)
    {
        if (unit == null)
            return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
            return;

        // So o gesto do jogador conta: o automata inimigo tambem confirma no proprio hex.
        if (matchController != null && unit.TeamId != matchController.GetTeamIdForSlot(0))
            return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null || obj.id != "HOLD_POSITION" || !obj.isVisible || !IsObjectivePending(obj))
                continue;

            // parameters opcional: token da unidade (ex.: SD) ou expressao de coords no formato do UNIT_AT_HEX.
            if (string.IsNullOrWhiteSpace(obj.parameters) ||
                UnitMatchesTargetToken(unit, obj.parameters.Trim()) ||
                IsUnitAtCoordinates(unit, obj.parameters))
            {
                MarkObjectiveComplete(obj);
            }
        }
    }

    private void HandleUnitRevealedFromFog(UnitManager unit)
    {
        MarkObjectiveCompleteById("FOW_REVEAL_UNIT");
    }

    private void HandleUnitDestroyed(UnitManager unit)
    {
        if (matchController != null && unit != null)
        {
            // Apenas se a unidade destruída for inimiga (time diferente do ativo)
            if (unit.SlotIndex != matchController.ActiveSlotId.Value)
            {
                MarkObjectiveCompleteById("DESTROY_ENEMY_UNIT");
            }

            TutorialData tutorial = GetActiveTutorial();
            if (tutorial != null && tutorial.objectives != null)
            {
                for (int i = 0; i < tutorial.objectives.Count; i++)
                {
                    TutorialObjective obj = tutorial.objectives[i];
                    bool pending = IsObjectivePending(obj);
                    
                    bool isDeathObjective = obj.id == "UNIT_DEAD" || obj.id == "DEAD_UNIT";
                    if (isDeathObjective && obj.isVisible && pending)
                    {
                        if (EvaluateUnitCondition(unit, obj, isDeathEvent: true))
                        {
                            MarkObjectiveComplete(obj);
                        }
                    }
                }
            }
        }
    }

    private void HandleUnitMoved(UnitManager unit)
    {
        if (unit == null) return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        // UNIT_AT_HEX nao valida aqui: OnUnitMovementExecuted dispara antes da
        // confirmacao (rollback ainda possivel) — o poll CheckUnitAtHexObjectives cobre.
        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj.id == "USED_ROAD_BOOST" && obj.isVisible && IsObjectivePending(obj))
            {
                // Parameters esperado: token da unidade (ex.: APC). Vazio = qualquer unidade.
                if (unit.UsedRoadBoostOnLastMove &&
                    (string.IsNullOrWhiteSpace(obj.parameters) || UnitMatchesTargetToken(unit, obj.parameters)))
                {
                    MarkObjectiveComplete(obj);
                }
            }
            // NOVO: Verifica UNIT_DEAD por autonomia durante movimento
            else if (obj.id == "UNIT_DEAD" && obj.isVisible && IsObjectivePending(obj))
            {
                if (EvaluateUnitCondition(unit, obj, isDeathEvent: false))
                {
                    MarkObjectiveComplete(obj);
                }
            }
        }
    }

    private void HandleUnitSelected(UnitManager unit)
    {
        if (unit == null) return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj.id == "UNIT_SELECTED" && obj.isVisible && IsObjectivePending(obj))
            {
                if (IsUnitAtCoordinates(unit, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
        }
    }

    private void HandleUnitEmbarked(UnitManager passenger, UnitManager transporter)
    {
        if (transporter == null) return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj.id == "HAS_EMBARKED_UNIT" && obj.isVisible && IsObjectivePending(obj))
            {
                if (MatchesUnitType(transporter, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
        }
    }

    private void HandleUnitDisembarked(UnitManager passenger, UnitManager transporter)
    {
        // UNIT_AT_HEX por desembarque tambem e coberto pelo poll CheckUnitAtHexObjectives
        // (passageiro desembarcado fica HasActed na celula de destino).
    }

    private void HandleUnitSupplied(UnitManager supplier, UnitManager target)
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null)
            return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj == null || obj.id != "SUPPLY_UNIT" || !obj.isVisible || !IsObjectivePending(obj))
                continue;

            if (string.IsNullOrWhiteSpace(obj.parameters))
            {
                MarkObjectiveComplete(obj);
                continue;
            }

            string token = obj.parameters.Trim();
            if (UnitMatchesTargetToken(supplier, token) || UnitMatchesTargetToken(target, token))
                MarkObjectiveComplete(obj);
        }
    }

    private bool IsUnitAtCoordinates(UnitManager unit, string parameters)
    {
        if (unit == null || string.IsNullOrWhiteSpace(parameters))
            return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        string raw = parameters.Trim();

        // Novo formato explicito: "SD && (23,1 || 24,1 || ...)"
        int andIndex = raw.IndexOf("&&", System.StringComparison.Ordinal);
        if (andIndex >= 0)
        {
            string unitToken = raw.Substring(0, andIndex).Trim();
            string coordsExpr = raw.Substring(andIndex + 2).Trim();
            coordsExpr = TrimOuterParentheses(coordsExpr);

            if (!string.IsNullOrWhiteSpace(unitToken) && !UnitMatchesTargetToken(unit, unitToken))
                return false;

            return CoordinatesExpressionContainsCell(coordsExpr, unitCell);
        }

        // Compatibilidade:
        // 1) "SD || 23,1 || 24,1" => token global + lista de coords.
        // 2) "SD 23,1 || APC 24,1" => token por segmento.
        // 3) "23,1 || 24,1" => qualquer unidade nessas coords.
        string[] parts = raw.Split(new[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 0)
            return false;

        string globalToken = null;
        int startIndex = 0;
        if (parts.Length > 1 && !ContainsCoordinate(parts[0]))
        {
            globalToken = parts[0].Trim();
            startIndex = 1;
        }

        if (!string.IsNullOrWhiteSpace(globalToken) && !UnitMatchesTargetToken(unit, globalToken))
            return false;

        for (int i = startIndex; i < parts.Length; i++)
        {
            string segment = parts[i].Trim();
            if (segment.Length <= 0)
                continue;

            if (TryParseTokenAndCoordinate(segment, out string localToken, out Vector2Int cell))
            {
                string tokenToCheck = !string.IsNullOrWhiteSpace(localToken) ? localToken : globalToken;
                if (!string.IsNullOrWhiteSpace(tokenToCheck) && !UnitMatchesTargetToken(unit, tokenToCheck))
                    continue;

                if (unitCell.x == cell.x && unitCell.y == cell.y)
                    return true;
            }
        }

        return false;
    }

    private static string TrimOuterParentheses(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '(' && trimmed[trimmed.Length - 1] == ')')
            return trimmed.Substring(1, trimmed.Length - 2).Trim();
        return trimmed;
    }

    private static bool CoordinatesExpressionContainsCell(string coordsExpression, Vector3Int targetCell)
    {
        if (string.IsNullOrWhiteSpace(coordsExpression))
            return false;

        string[] coords = coordsExpression.Split(new[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < coords.Length; i++)
        {
            // Segmento pode ser "x,y" ou o nome de uma construcao (ex.: "Bandeira"),
            // que resolve para a celula onde ela esta.
            if (!TryResolveCellReference(coords[i], out Vector2Int cell))
                continue;
            if (cell.x == targetCell.x && cell.y == targetCell.y)
                return true;
        }

        return false;
    }

    // "x,y" vira celula direto; qualquer outro texto tenta casar uma construcao
    // pelo nome e usa a celula dela. Mantem os assets sem coordenada hardcoded.
    private static bool TryResolveCellReference(string reference, out Vector2Int cell)
    {
        if (TryParseCoordinate(reference, out cell))
            return true;

        string token = reference != null ? reference.Trim() : string.Empty;
        if (token.Length <= 0)
            return false;

        ConstructionManager construction = FindConstructionByName(token);
        if (construction == null)
            return false;

        Vector3Int constructionCell = construction.CurrentCellPosition;
        cell = new Vector2Int(constructionCell.x, constructionCell.y);
        return true;
    }

    private static bool TryParseTokenAndCoordinate(string segment, out string token, out Vector2Int cell)
    {
        token = null;
        cell = default;
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        string trimmed = segment.Trim();
        if (TryParseCoordinate(trimmed, out cell))
            return true;

        string[] subParts = trimmed.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (subParts.Length < 2)
            return false;

        token = subParts[0].Trim();
        string coordPart = subParts[subParts.Length - 1].Trim();
        return TryParseCoordinate(coordPart, out cell);
    }

    private static bool TryParseCoordinate(string input, out Vector2Int cell)
    {
        cell = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string[] xy = input.Trim().Split(',');
        if (xy.Length < 2)
            return false;

        if (!int.TryParse(xy[0].Trim(), out int x) || !int.TryParse(xy[1].Trim(), out int y))
            return false;

        cell = new Vector2Int(x, y);
        return true;
    }

    private static bool ContainsCoordinate(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        return segment.Contains(",");
    }

    private bool MatchesUnitType(UnitManager unit, string parameters)
    {
        if (unit == null || string.IsNullOrWhiteSpace(parameters)) return false;

        // Split por || para suportar múltiplos critérios ou NOME || CONDICAO
        string[] types = parameters.Split(new[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < types.Length; i++)
        {
            string type = types[i].Trim();
            // Ignora termos de condição como HP=0 ou AUT=0 na verificação de match de nome
            if (type.Contains("=") || type.Contains("<") || type.Contains(">")) continue;

            if (UnitMatchesTargetToken(unit, type)) return true;
        }

        return false;
    }

    private bool EvaluateUnitCondition(UnitManager unit, TutorialObjective obj, bool isDeathEvent)
    {
        if (unit == null || string.IsNullOrWhiteSpace(obj.parameters)) return false;

        // Split por || para suportar múltiplos critérios ou NOME || CONDICAO
        string[] parts = obj.parameters.Split(new[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
        
        // Se for um evento de morte, qualquer parte que bata com o tipo/nome ja valida
        if (isDeathEvent)
        {
            return MatchesUnitType(unit, obj.parameters);
        }

        // Se nao for morte, procuramos por critério de autonomia: AUT = X
        string unitIdOrName = parts[0].Trim();
        
        // Primeiro valida se a unidade em questão é a correta
        bool unitMatches = UnitMatchesTargetToken(unit, unitIdOrName);

        if (!unitMatches) return false;

        // Se bateu o nome, verifica se há uma segunda parte com AUT
        if (parts.Length > 1)
        {
            string condition = parts[1].Trim();
            if (condition.StartsWith("AUT", System.StringComparison.OrdinalIgnoreCase))
            {
                if (condition.Contains("="))
                {
                    string valStr = condition.Split('=')[1].Trim();
                    if (int.TryParse(valStr, out int targetAut))
                    {
                        return unit.CurrentFuel <= targetAut;
                    }
                }
            }
        }

        return false;
    }

    private bool UnitMatchesTargetToken(UnitManager unit, string token)
    {
        if (unit == null || string.IsNullOrWhiteSpace(token))
            return false;

        string normalized = token.Trim();
        if (normalized.Length <= 0)
            return false;

        if (!string.IsNullOrWhiteSpace(unit.UnitId) &&
            unit.UnitId.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName) &&
            unit.UnitDisplayName.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (unit.name != null && unit.name.Contains(normalized, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (unit.TryGetUnitData(out UnitData data) && data != null)
        {
            if (!string.IsNullOrWhiteSpace(data.id) &&
                data.id.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(data.displayName) &&
                data.displayName.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(data.apelido) &&
                data.apelido.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;

            if (data.name != null && data.name.Contains(normalized, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void HandleAttackResolved(UnitManager attacker, UnitManager defender)
    {
        MarkObjectiveCompleteById("ATTACK_UNIT");

        if (terrainDatabase == null)
            ResolveReferences();
        
        // Verifica terreno onde o atacante esta
        if (attacker != null && attacker.BoardTilemap != null && terrainDatabase != null)
        {
            Vector3Int cell = attacker.CurrentCellPosition;
            cell.z = 0;
            TileBase tile = attacker.BoardTilemap.GetTile(cell);
            
            if (tile != null && terrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData terrainData) && terrainData != null)
            {
                string tName = !string.IsNullOrWhiteSpace(terrainData.id) ? terrainData.id.ToLower() : terrainData.name.ToLower();
                
                // Adapte essas strings se os IDs da sua TerrainDatabase forem diferentes
                if (tName.Contains("mountain") || tName.Contains("montanha"))
                {
                    MarkObjectiveCompleteById("ATTACK_UNIT_MOUNTAIN");
                }
                else if (tName.Contains("plain") || tName.Contains("planicie") || tName.Contains("grass"))
                {
                    MarkObjectiveCompleteById("ATTACK_UNIT_PLAINS");
                }
            }
        }
    }

    private void HandleTurnEnded()
    {
        MarkObjectiveCompleteById("END_TURN");
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        TutorialData tutorial = GetActiveTutorial();
        if (tutorial != null)
        {
            int playerTeamId = matchController != null ? (int)matchController.GetTeamIdForSlot(0) : 0;
            TutorialRules.CheckTurnStartRules(tutorial.id, teamId, playerTeamId);

            // NOVO: Verifica UNIT_DEAD por autonomia em todas as unidades ativas no inicio do turno
            if (tutorial.objectives != null)
            {
                foreach (var obj in tutorial.objectives)
                {
                    if (obj.id == "UNIT_DEAD" && obj.isVisible && IsObjectivePending(obj))
                    {
                        foreach (var unit in UnitManager.AllActive)
                        {
                            if (EvaluateUnitCondition(unit, obj, isDeathEvent: false))
                            {
                                MarkObjectiveComplete(obj);
                                break;
                            }
                        }
                    }
                }
            }
        }

        TryStartTutorialAutomata((TeamId)teamId, tutorial);
        TryMarkAlwaysActedUnits((TeamId)teamId, tutorial);
    }

    // Figurantes (ex.: Mathias e Dias na fila): amanhecem "ja agiram" em todo turno
    // do jogador. Sem cursor, sem eventos — so o MarkAsActed depois da transicao
    // de turno (que e quem reseta o HasActed do time).
    private void TryMarkAlwaysActedUnits(TeamId activeTeam, TutorialData tutorial)
    {
        if (tutorial == null || string.IsNullOrWhiteSpace(tutorial.alwaysActedUnits))
            return;
        if (matchController == null || activeTeam != matchController.GetTeamIdForSlot(0))
            return;

        StartCoroutine(RunMarkAlwaysActedUnits(tutorial.alwaysActedUnits));
    }

    private IEnumerator RunMarkAlwaysActedUnits(string tokens)
    {
        // Espera a transicao terminar para marcar DEPOIS do reset de HasActed do turno.
        yield return WaitForTurnTransitionGate(timeoutSeconds: 6f);
        yield return null;

        string[] list = tokens.Split(';');
        for (int i = 0; i < list.Length; i++)
        {
            string token = list[i].Trim();
            if (token.Length <= 0)
                continue;

            UnitManager unit = FindActiveUnitByToken(token);
            if (unit != null)
                unit.MarkAsActed();
        }
    }

    private void TryStartTutorialAutomata(TeamId activeTeam, TutorialData tutorial)
    {
        // A rotina roda para qualquer turno nao-jogador mesmo SEM AutomataDatabase:
        // e ela quem devolve o turno (senao um time vermelho vazio pendura a partida).
        // O database so da comportamento as unidades — sem ele, ficam paradas.
        if (!enableTutorialAutomata)
            return;
        // O automata dirige qualquer time que nao seja o do jogador (slot 0).
        // Nao comparar com cor fixa: a cor do jogador pode ter sido escolhida na Tela de Entrada.
        if (matchController == null || matchController.HasVictoryWinner)
            return;
        if (activeTeam == TeamId.Neutral || activeTeam == matchController.GetTeamIdForSlot(0))
            return;

        StopTutorialAutomataRoutine();
        tutorialAutomataRoutine = StartCoroutine(RunTutorialAutomataTurn(activeTeam, tutorial));
    }

    private void StopTutorialAutomataRoutine()
    {
        if (tutorialAutomataRoutine == null)
            return;

        StopCoroutine(tutorialAutomataRoutine);
        tutorialAutomataRoutine = null;
    }

    private IEnumerator RunTutorialAutomataTurn(TeamId activeTeam, TutorialData tutorial)
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (turnStateManager == null)
            yield break;

        yield return WaitForTurnTransitionGate(timeoutSeconds: 6f);
        yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 3f);

        // Movimento scriptado da fala muda tem prioridade: espera concluir antes
        // de decidir pelas unidades — senao os dois disputam a mesma unidade
        // (a rotina confirmaria o soldado parado no meio do comando de marcha).
        float commandDeadline = Time.time + 20f;
        while (automataCommandInProgress && Time.time < commandDeadline)
            yield return null;

        string tutorialId = tutorial != null ? tutorial.id : string.Empty;
        float preSelectDelay = turnStateManager.GetAutomatedPreSelectDelay();
        float betweenUnitsDelay = turnStateManager.GetAutomatedBetweenUnitsDelay();

        // Falas mudas podem criar a unidade no mesmo ciclo da troca de turno.
        // Aguarda esse comando terminar antes de congelar a lista do automata.
        List<UnitManager> units = null;
        float collectDeadline = Time.time + 2.5f;
        while (Time.time < collectDeadline)
        {
            units = CollectAutomataUnitsForTeam(activeTeam);
            if (units.Count > 0)
                break;
            if (matchController == null || matchController.ActiveTeam != activeTeam || matchController.HasVictoryWinner)
                break;
            yield return null;
        }
        units ??= new List<UnitManager>();

        for (int i = 0; i < units.Count; i++)
        {
            if (matchController == null || matchController.ActiveTeam != activeTeam || matchController.HasVictoryWinner)
                break;

            UnitManager unit = units[i];
            if (!IsUnitValidForAutomata(unit, activeTeam))
                continue;

            if (automataDatabase == null ||
                !automataDatabase.TryResolve(unit, activeTeam, tutorialId, out AutomataData automata) ||
                automata == null)
                continue;

            // Guarnicao (stationary): nunca se move e so age quando tem alvo no
            // alcance parado. Sem alvo, nem seleciona — turno silencioso, sem
            // passeio de cursor a cada turno vazio da marcha do jogador.
            if (IsStationaryIdle(automata, unit))
                continue;

            Vector3Int unitCell = NormalizeCell(unit.CurrentCellPosition);
            yield return turnStateManager.MoveCursorToCellWithAutomatedTravel(unitCell);
            if (preSelectDelay > 0f)
                yield return new WaitForSeconds(preSelectDelay);

            // Avanco scriptado: caminha em direcao ao hex alvo antes de atacar/finalizar.
            bool enteredViaMove = false;
            if (!automata.stationary && automata.moveTowardsTarget && TryFindAutomataAdvanceCell(unit, automata, out Vector3Int advanceCell))
            {
                yield return ExecuteTutorialAutomataMoveBatch(unit, activeTeam, advanceCell, value => enteredViaMove = value);
            }

            if (!enteredViaMove && !turnStateManager.TryAutomatedSelectUnitAndEnterMoveuParado(unit))
            {
                turnStateManager.HandleCancel();
                yield return null;
                continue;
            }

            bool hasAttack = turnStateManager.HasAutomatedAttackAvailable();
            bool hasMove = turnStateManager.HasAutomatedMoveAvailable();
            bool handled = false;

            if (automata.preferAttack && hasAttack)
            {
                handled = turnStateManager.TryExecuteAutomatedAttackWithPreference(automata.targetPreference);
                if (!handled && automata.fallbackMove && hasMove)
                    handled = turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.None);
            }
            else if (automata.fallbackMove && hasMove)
            {
                handled = turnStateManager.HandleAutomatedSensorActionRequested(SensorActionType.None);
            }

            if (!handled)
                turnStateManager.HandleCancel();

            yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 8f);

            if (betweenUnitsDelay > 0f)
                yield return new WaitForSeconds(betweenUnitsDelay);
        }

        yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 3f);
        if (ShouldAutoAdvanceTurn(activeTeam))
            matchController.AdvanceTurnWithTransition();

        tutorialAutomataRoutine = null;
    }

    // Guarnicao ociosa: stationary e sem alvo (ou sem vocacao de ataque). Nao age
    // neste turno e NAO pode impedir a devolucao do turno (ShouldAutoAdvanceTurn).
    private bool IsStationaryIdle(AutomataData automata, UnitManager unit)
    {
        return automata != null && automata.stationary &&
               (!automata.preferAttack || !HasStationaryAttackTarget(unit));
    }

    private readonly List<PodeMirarTargetOption> stationaryTargetsBuffer = new List<PodeMirarTargetOption>();

    // Pre-check leve da guarnicao: ha alvo valido atacando PARADO da celula atual?
    // Fonte de verdade: PodeMirarSensor — a mesma checagem que os sensores refarao
    // oficialmente depois da selecao, entao nao ha risco de divergencia.
    private bool HasStationaryAttackTarget(UnitManager unit)
    {
        if (unit == null || unit.BoardTilemap == null || terrainDatabase == null)
            return false;

        PodeMirarSensor.CollectTargets(
            unit,
            unit.BoardTilemap,
            terrainDatabase,
            SensorMovementMode.MoveuParado,
            stationaryTargetsBuffer);
        return stationaryTargetsBuffer.Count > 0;
    }

    private IEnumerator ExecuteTutorialAutomataMoveBatch(
        UnitManager unit,
        TeamId activeTeam,
        Vector3Int destination,
        System.Action<bool> completed)
    {
        completed?.Invoke(false);
        if (unit == null || turnStateManager == null)
        {
            Debug.LogWarning("[TutorialManager] Automata batch cancelado: unidade/TurnStateManager ausente.");
            yield break;
        }

        // Cena de tutorial nao tem AIController: o batch de movimento vai direto
        // no ReplayManager (mesmo executor do ExecuteLiveAIBatch da IA oficial).
        ReplayManager replay = FindAnyObjectByType<ReplayManager>();
        if (replay == null)
        {
            Debug.LogWarning("[TutorialManager] Automata batch cancelado: ReplayManager nao encontrado na cena.");
            yield break;
        }

        // O comando anterior pode ter acabado visualmente em MoveuParado, mas
        // o batch seguinte só pode começar com a FSM realmente neutra. Caso
        // contrário, confirmar a origem vira "manter posição" em vez de
        // selecionar a unidade.
        yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 6f);
        if (turnStateManager.CurrentCursorState != TurnStateManager.CursorState.Neutral)
        {
            Debug.LogWarning($"[TutorialManager] Automata batch cancelado: FSM nao ficou Neutral antes da selecao (state={turnStateManager.CurrentCursorState}).");
            yield break;
        }

        Vector3Int origin = NormalizeCell(unit.CurrentCellPosition);
        destination = NormalizeCell(destination);
        Debug.Log($"[TutorialManager] Automata batch: unidade={unit.InstanceId} origem={origin} destino={destination}");
        yield return new WaitForSeconds(2f);
        turnStateManager.SuppressNextNeutralConfirm();

        // Mesmo formato do AIController.BuildMoveBatch (MovementPath nulo: o
        // executor do replay resolve o caminho real).
        PlayerAction batch = new PlayerAction
        {
            IsAIGenerated  = true,
            ActionType     = PlayerActionType.UnitAction,
            ActingTeam     = activeTeam,
            TurnNumber     = matchController != null ? matchController.CurrentTurn : 0,
            CursorHex      = origin, HasCursorHex = true,
            UnitInstanceId = unit.InstanceId.ToString(),
            MoveFrom       = origin, HasMoveFrom = true,
            MoveTo         = destination, HasMoveTo = true,
            SensorAction   = SensorActionType.None,
            MovementPath   = null,
            DebugLabel     = $"Tutorial Move {unit.InstanceId} → {destination}",
        };

        replay.ExecuteLiveAIBatch(batch, fastAI: false);
        yield return new WaitUntil(() => !replay.IsStepExecutionBusy);
        yield return turnStateManager.WaitUntilAutomatedNeutralReady(timeoutSeconds: 6f);

        bool succeeded = turnStateManager.CurrentCursorState == TurnStateManager.CursorState.Neutral &&
                         NormalizeCell(unit.CurrentCellPosition) == destination;
        completed?.Invoke(succeeded);
        if (!succeeded)
            Debug.LogWarning($"[TutorialManager] Automata batch falhou: state={turnStateManager.CurrentCursorState} atual={unit.CurrentCellPosition} destino={destination}");
    }

    private IEnumerator WaitForTurnTransitionGate(float timeoutSeconds)
    {
        if (matchController == null)
            yield break;

        float endTime = Time.time + Mathf.Max(0.2f, timeoutSeconds);
        while (Time.time < endTime && matchController.IsTurnTransitionInProgress)
            yield return null;

        // Garante 1 frame extra para UI/musica/fog estabilizarem apos a transicao.
        yield return null;
    }

    // Escolhe a celula alcancavel neste turno que mais aproxima a unidade do alvo do automata,
    // respeitando custo de terreno e ocupacao (fonte de verdade: UnitMovementPathRules).
    // Retorna false se ja esta na distancia de parada ou se nenhuma celula aproxima.
    private bool TryFindAutomataAdvanceCell(UnitManager unit, AutomataData automata, out Vector3Int advanceCell)
    {
        advanceCell = default;
        if (unit == null || automata == null || unit.BoardTilemap == null)
            return false;

        Vector3Int target = automata.moveTargetCell;
        target.z = 0;
        Vector3Int current = NormalizeCell(unit.CurrentCellPosition);
        int stopDistance = Mathf.Max(0, automata.stopDistance);
        int currentDistance = Mathf.RoundToInt(SectorManager.HexDistance(current, target));
        if (currentDistance <= stopDistance)
            return false;

        Dictionary<Vector3Int, List<Vector3Int>> paths = UnitMovementPathRules.CalcularCaminhosValidos(
            unit.BoardTilemap, unit, Mathf.Max(0, unit.RemainingMovementPoints), terrainDatabase);
        if (paths == null || paths.Count <= 0)
            return false;

        int bestDistance = currentDistance;
        int bestCost = int.MaxValue;
        bool found = false;
        foreach (KeyValuePair<Vector3Int, List<Vector3Int>> entry in paths)
        {
            Vector3Int cell = NormalizeCell(entry.Key);
            if (cell == current)
                continue;

            // O caminho pode atravessar aliado, mas nao pode terminar em hex ocupado.
            if (UnitOccupancyRules.GetUnitAtCell(unit.BoardTilemap, cell) != null)
                continue;

            int distance = Mathf.RoundToInt(SectorManager.HexDistance(cell, target));
            if (distance < stopDistance)
                continue;

            int cost = entry.Value != null ? entry.Value.Count : int.MaxValue;
            if (distance < bestDistance || (distance == bestDistance && found && cost < bestCost))
            {
                bestDistance = distance;
                bestCost = cost;
                advanceCell = cell;
                found = true;
            }
        }

        return found;
    }

    private List<UnitManager> CollectAutomataUnitsForTeam(TeamId team)
    {
        var result = new List<UnitManager>();
        List<UnitManager> allUnits = UnitManager.AllActive;
        if (allUnits == null || allUnits.Count <= 0)
            return result;

        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (!IsUnitValidForAutomata(unit, team))
                continue;
            result.Add(unit);
        }

        // Ordem simples, estavel e previsivel: de cima para baixo no mapa.
        result.Sort((a, b) => NormalizeCell(b.CurrentCellPosition).y.CompareTo(NormalizeCell(a.CurrentCellPosition).y));
        return result;
    }

    private static bool IsUnitValidForAutomata(UnitManager unit, TeamId team)
    {
        return unit != null &&
               !unit.IsDead &&
               !unit.IsEmbarked &&
               unit.TeamId == team &&
               !unit.HasActed;
    }

    private bool ShouldAutoAdvanceTurn(TeamId activeTeam)
    {
        if (matchController == null || matchController.HasVictoryWinner)
            return false;
        if (matchController.ActiveTeam != activeTeam)
            return false;

        List<UnitManager> allUnits = UnitManager.AllActive;
        if (allUnits == null || allUnits.Count <= 0)
            return true;

        TutorialData tutorial = GetActiveTutorial();
        string tutorialId = tutorial != null ? tutorial.id : string.Empty;
        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (!IsUnitValidForAutomata(unit, activeTeam))
                continue;

            // Guarnicao ociosa nao age de proposito — nao pode segurar o turno.
            if (automataDatabase != null &&
                automataDatabase.TryResolve(unit, activeTeam, tutorialId, out AutomataData automata) &&
                IsStationaryIdle(automata, unit))
                continue;

            return false;
        }

        return true;
    }

    private static Vector3Int NormalizeCell(Vector3Int cell)
    {
        cell.z = 0;
        return cell;
    }
}

