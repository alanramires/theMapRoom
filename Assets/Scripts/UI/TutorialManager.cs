using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class TutorialManager : MonoBehaviour
{
    // Disparado quando um objetivo normal completa (nao dispara para condicoes de derrota).
    // Consumido pelo PanelDialogTutorialController para liberar os gates do roteiro.
    public static event System.Action<TutorialObjective> OnObjectiveCompleted;

    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;
    [Header("Tutorial Automation")]
    [SerializeField] private AutomataDatabase automataDatabase;
    [SerializeField] private bool enableTutorialAutomata = true;

    private readonly HashSet<string> spawnedObjectiveIds = new HashSet<string>();
    private Coroutine tutorialAutomataRoutine;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();
        ResetTutorialObjectives();
        ProcessObjectiveSpawns();
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

        // Progressao linear: sempre revela o primeiro objetivo normal pendente.
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

    private bool ExecuteObjectiveSpawn(TutorialObjective obj)
    {
        // Formato esperado: spawn:TEAM_ID UNIT_TOKEN X,Y
        // Exemplo: spawn:1 SD 5,6
        // TEAM_ID tambem aceita slot logico ("slot0", "slot1"): resolve a cor atual do slot.
        // Prefira slots em cenas de tutorial — a cor do jogador pode ser escolhida na Tela de Entrada.
        try
        {
            string raw = obj.parameters.Substring(6).Trim(); // Remove "spawn:"
            string[] parts = raw.Split(' ');
            if (parts.Length < 3) return false;

            int teamId;
            if (parts[0].StartsWith("slot", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(parts[0].Substring(4), out int slotIndex)) return false;
                if (matchController == null) return false;
                teamId = (int)matchController.GetTeamIdForSlot(slotIndex);
                if (teamId == (int)TeamId.Neutral) return false;
            }
            else if (!int.TryParse(parts[0], out teamId)) return false;
            string unitToken = parts[1];
            string coords = parts[2];

            string[] xy = coords.Split(',');
            if (xy.Length < 2) return false;

            if (int.TryParse(xy[0].Trim(), out int x) && int.TryParse(xy[1].Trim(), out int y))
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (turnStateManager.TrySpawnUnitAtCell(unitToken, teamId, cell, out string message))
                {
                    Debug.Log($"[TutorialManager] Spawn executado: {message}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[TutorialManager] Falha no spawn: {message}");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TutorialManager] Erro ao processar spawn do objetivo {obj.id}: {e.Message}");
        }
        return false;
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
            if ((int)unit.TeamId != matchController.ActiveTeamId)
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
            if ((int)unit.TeamId != matchController.ActiveTeamId)
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

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj.id == "UNIT_AT_HEX" && obj.isVisible && IsObjectivePending(obj))
            {
                if (IsUnitAtCoordinates(unit, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
            else if (obj.id == "USED_ROAD_BOOST" && obj.isVisible && IsObjectivePending(obj))
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
        if (passenger == null) return;

        TutorialData tutorial = GetActiveTutorial();
        if (tutorial == null || tutorial.objectives == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            if (obj.id == "UNIT_AT_HEX" && obj.isVisible && IsObjectivePending(obj))
            {
                if (IsUnitAtCoordinates(passenger, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
        }
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
            if (!TryParseCoordinate(coords[i], out Vector2Int cell))
                continue;
            if (cell.x == targetCell.x && cell.y == targetCell.y)
                return true;
        }

        return false;
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
    }

    private void TryStartTutorialAutomata(TeamId activeTeam, TutorialData tutorial)
    {
        if (!enableTutorialAutomata || automataDatabase == null)
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

        List<UnitManager> units = CollectAutomataUnitsForTeam(activeTeam);
        string tutorialId = tutorial != null ? tutorial.id : string.Empty;
        float preSelectDelay = turnStateManager.GetAutomatedPreSelectDelay();
        float betweenUnitsDelay = turnStateManager.GetAutomatedBetweenUnitsDelay();

        for (int i = 0; i < units.Count; i++)
        {
            if (matchController == null || matchController.ActiveTeam != activeTeam || matchController.HasVictoryWinner)
                break;

            UnitManager unit = units[i];
            if (!IsUnitValidForAutomata(unit, activeTeam))
                continue;

            if (!automataDatabase.TryResolve(unit, activeTeam, tutorialId, out AutomataData automata) || automata == null)
                continue;

            Vector3Int unitCell = NormalizeCell(unit.CurrentCellPosition);
            yield return turnStateManager.MoveCursorToCellWithAutomatedTravel(unitCell);
            if (preSelectDelay > 0f)
                yield return new WaitForSeconds(preSelectDelay);

            // Avanco scriptado: caminha em direcao ao hex alvo antes de atacar/finalizar.
            bool enteredViaMove = false;
            if (automata.moveTowardsTarget && TryFindAutomataAdvanceCell(unit, automata, out Vector3Int advanceCell))
            {
                if (turnStateManager.TryAutomatedSelectUnitOnly(unit) &&
                    turnStateManager.TryAutomatedMoveSelectedUnitToCell(advanceCell))
                {
                    yield return turnStateManager.WaitUntilMovementAnimationDone(timeoutSeconds: 10f);
                    enteredViaMove = true;
                }
                else
                {
                    turnStateManager.HandleCancel();
                    yield return null;
                }
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
                handled = turnStateManager.TryExecuteAutomatedAttackFirstTarget();
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

        for (int i = 0; i < allUnits.Count; i++)
        {
            UnitManager unit = allUnits[i];
            if (IsUnitValidForAutomata(unit, activeTeam))
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

