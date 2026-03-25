using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private TerrainDatabase terrainDatabase;
    [SerializeField] private MatchController matchController;
    [SerializeField] private TurnStateManager turnStateManager;

    private readonly HashSet<string> spawnedObjectiveIds = new HashSet<string>();

    private void Start()
    {
        ResetTutorialObjectives();
        ProcessObjectiveSpawns();
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
            }
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
        TurnStateManager.OnUnitSelected += HandleUnitSelected;
        TurnStateManager.OnUnitEmbarked += HandleUnitEmbarked;
        TurnStateManager.OnUnitDisembarked += HandleUnitDisembarked;
        MatchController.OnBeforeAdvanceTurn += HandleTurnEnded;
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
    }

    private void OnDisable()
    {
        TurnStateManager.OnUnitPurchased -= HandleUnitPurchased;
        TurnStateManager.OnUnitInspected -= HandleUnitInspected;
        TurnStateManager.OnAttackResolved -= HandleAttackResolved;
        TurnStateManager.OnUnitDestroyed -= HandleUnitDestroyed;
        TurnStateManager.OnUnitRevealedFromFog -= HandleUnitRevealedFromFog;
        TurnStateManager.OnUnitMovementExecuted -= HandleUnitMoved;
        TurnStateManager.OnUnitSelected -= HandleUnitSelected;
        TurnStateManager.OnUnitEmbarked -= HandleUnitEmbarked;
        TurnStateManager.OnUnitDisembarked -= HandleUnitDisembarked;
        MatchController.OnBeforeAdvanceTurn -= HandleTurnEnded;
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
    }

    private TutorialData GetActiveTutorial()
    {
        if (HelpManager.Instance == null) return null;
        return HelpManager.Instance.ActiveTutorial;
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

            CheckTutorialCompletion();
        }

        // Delega regras especiais para TutorialRules (mantido por ID para compatibilidade se necessário)
        TutorialRules.CheckObjectiveRules(tutorial.id, obj.id);

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
            if (tutorial.objectives[i].id == id && IsObjectivePending(tutorial.objectives[i]))
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
        if (turnStateManager == null) turnStateManager = FindAnyObjectByType<TurnStateManager>();
        if (turnStateManager == null) return;

        for (int i = 0; i < tutorial.objectives.Count; i++)
        {
            TutorialObjective obj = tutorial.objectives[i];
            
            // Se o objetivo ja foi completado, pula pro proximo
            if (obj.isCompleted) continue;

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

    private bool ExecuteObjectiveSpawn(TutorialObjective obj)
    {
        // Formato esperado: spawn:TEAM_ID UNIT_TOKEN X,Y
        // Exemplo: spawn:1 SD 5,6
        try
        {
            string raw = obj.parameters.Substring(6).Trim(); // Remove "spawn:"
            string[] parts = raw.Split(' ');
            if (parts.Length < 3) return false;

            if (!int.TryParse(parts[0], out int teamId)) return false;
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
            if (!tutorial.objectives[i].isCompleted && !tutorial.objectives[i].isDefeatCondition)
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
            // Apenas unidades inimigas
            if ((int)unit.TeamId != matchController.ActiveTeamId)
            {
                MarkObjectiveCompleteById("INSPECT_ENEMY_UNIT");
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
                    if (isDeathObjective && pending)
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
            if (obj.id == "UNIT_AT_HEX" && IsObjectivePending(obj))
            {
                if (IsUnitAtCoordinates(unit, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
            // NOVO: Verifica UNIT_DEAD por autonomia durante movimento
            else if (obj.id == "UNIT_DEAD" && IsObjectivePending(obj))
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
            if (obj.id == "UNIT_SELECTED" && IsObjectivePending(obj))
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
            if (obj.id == "HAS_EMBARKED_UNIT" && IsObjectivePending(obj))
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
            if (obj.id == "UNIT_AT_HEX" && IsObjectivePending(obj))
            {
                if (IsUnitAtCoordinates(passenger, obj.parameters))
                {
                    MarkObjectiveComplete(obj);
                }
            }
        }
    }

    private bool IsUnitAtCoordinates(UnitManager unit, string parameters)
    {
        if (unit == null || string.IsNullOrWhiteSpace(parameters)) return false;

        Vector3Int unitCell = unit.CurrentCellPosition;
        unitCell.z = 0;

        // Suporta "5,6 || 6,6 || 7,6" ou "SD 5,6 || APC 6,6"
        string[] parts = parameters.Split(new[] { "||" }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string segment = parts[i].Trim();
            string[] subParts = segment.Split(' ');
            
            string targetType = null;
            string coordPart = null;

            if (subParts.Length > 1)
            {
                // Formato: TIPO COORD (ex: SD 0,0)
                targetType = subParts[0].Trim();
                coordPart = subParts[1].Trim();
            }
            else
            {
                // Formato: COORD (ex: 0,0)
                coordPart = subParts[0].Trim();
            }

            // Valida tipo se houver
            if (!string.IsNullOrWhiteSpace(targetType))
            {
                if (!unit.UnitId.Equals(targetType, System.StringComparison.OrdinalIgnoreCase) &&
                    !unit.name.Contains(targetType, System.StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Valida coordenada
            string[] xy = coordPart.Split(',');
            if (xy.Length >= 2 && int.TryParse(xy[0].Trim(), out int x) && int.TryParse(xy[1].Trim(), out int y))
            {
                if (unitCell.x == x && unitCell.y == y) return true;
            }
        }
        return false;
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

            if (unit.UnitId.Equals(type, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (unit.name.Contains(type, System.StringComparison.OrdinalIgnoreCase)) return true;
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
        bool unitMatches = unit.UnitId.Equals(unitIdOrName, System.StringComparison.OrdinalIgnoreCase) ||
                         unit.name.Contains(unitIdOrName, System.StringComparison.OrdinalIgnoreCase);

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

    private void HandleAttackResolved(UnitManager attacker, UnitManager defender)
    {
        MarkObjectiveCompleteById("ATTACK_UNIT");
        
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
            TutorialRules.CheckTurnStartRules(tutorial.id, teamId);

            // NOVO: Verifica UNIT_DEAD por autonomia em todas as unidades ativas no inicio do turno
            if (tutorial.objectives != null)
            {
                foreach (var obj in tutorial.objectives)
                {
                    if (obj.id == "UNIT_DEAD" && IsObjectivePending(obj))
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
    }
}
