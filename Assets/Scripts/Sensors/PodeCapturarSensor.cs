using UnityEngine;
using UnityEngine.Tilemaps;

public static class PodeCapturarSensor
{
    public enum CaptureOperationType
    {
        None = 0,
        CaptureEnemy = 1,
        RecoverAlly = 2
    }

    /// <summary>
    /// Fonte de verdade da capacidade de capturar construcoes.
    /// O papel da IA define comportamento; a permissao vem de
    /// UnitData > Training > Skills > Captura Construcoes.
    /// </summary>
    public static bool HasCaptureConstructionSkill(UnitManager unit)
    {
        return unit != null
            && unit.TryGetUnitData(out UnitData unitData)
            && HasCaptureConstructionSkill(unitData);
    }

    public static bool HasCaptureConstructionSkill(UnitData unitData)
    {
        if (unitData == null || unitData.skills == null)
            return false;

        for (int i = 0; i < unitData.skills.Count; i++)
        {
            SkillData skill = unitData.skills[i];
            if (skill == null)
                continue;

            if (skill.canCaptureConstructions)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Retorna quantos pontos esta unidade aplica ao capturar ou recuperar uma construcao.
    /// Capturadores agressivos trocam eficiencia de captura pelo alcance: 2 HP por ponto,
    /// arredondando para cima e com minimo de 1 enquanto estiverem vivos.
    /// </summary>
    public static int GetCapturePower(UnitManager unit)
    {
        if (unit == null || unit.IsDead)
            return 0;

        int hp = Mathf.Max(0, unit.CurrentHP);
        if (hp <= 0)
            return 0;

        if (unit.TryGetUnitData(out UnitData unitData)
            && unitData != null
            && unitData.roles != null
            && unitData.roles.Count > 0
            && unitData.roles[0] == UnitRole.CapturadorAgressivo)
        {
            return Mathf.Max(1, Mathf.CeilToInt(hp / 2f));
        }

        return hp;
    }

    /// <summary>
    /// Retorna a forca efetiva contra uma construcao. Capturas inimigas sem o
    /// pre-requisito de progressao aplicam metade da forca base, arredondada
    /// para baixo e com minimo de 1. Recuperacao aliada nunca recebe essa penalidade.
    /// </summary>
    public static int GetCapturePower(
        UnitManager unit,
        ConstructionManager construction,
        CaptureOperationType operationType,
        MatchController matchController,
        out bool prerequisitePenaltyApplied)
    {
        prerequisitePenaltyApplied = false;
        int basePower = GetCapturePower(unit);
        if (basePower <= 0
            || construction == null
            || operationType != CaptureOperationType.CaptureEnemy)
        {
            return basePower;
        }

        if (!construction.TryResolveConstructionData(out ConstructionData constructionData)
            || constructionData == null)
        {
            return basePower;
        }

        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (matchController == null
            || !matchController.ShouldPenalizeCaptureForMissingPrerequisite(
                PlayerSlotId.FromIndex(unit.SlotIndex),
                constructionData,
                out _))
        {
            return basePower;
        }

        prerequisitePenaltyApplied = true;
        return Mathf.Max(1, basePower / 2);
    }

    public static int GetCapturePower(
        UnitManager unit,
        ConstructionManager construction,
        MatchController matchController = null)
    {
        CaptureOperationType operationType =
            unit != null
            && construction != null
            && PlayerSlotRelations.AreAllies(unit.SlotIndex, construction.SlotIndex)
                ? CaptureOperationType.RecoverAlly
                : CaptureOperationType.CaptureEnemy;
        return GetCapturePower(
            unit,
            construction,
            operationType,
            matchController,
            out _);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out string reason,
        MatchController matchController = null)
    {
        return TryGetCaptureTarget(
            selectedUnit,
            boardTilemap,
            movementMode,
            out targetConstruction,
            out _,
            out reason,
            matchController);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason,
        MatchController matchController = null)
    {
        Vector3Int evaluatedCell = selectedUnit != null
            ? selectedUnit.CurrentCellPosition
            : default;
        return TryGetCaptureTargetAtCell(
            selectedUnit,
            boardTilemap,
            evaluatedCell,
            movementMode,
            out targetConstruction,
            out operationType,
            out reason,
            matchController);
    }

    /// <summary>
    /// Consulta a mesma regra oficial de captura em uma célula projetada, sem
    /// alterar a posição ou qualquer estado confirmado da unidade. Usado por
    /// planejadores para avaliar o resultado de um movimento provisório.
    /// </summary>
    public static bool TryGetCaptureTargetAtCell(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        Vector3Int evaluatedCell,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason,
        MatchController matchController = null)
    {
        targetConstruction = null;
        operationType = CaptureOperationType.None;
        reason = string.Empty;
        bool sensorLogs = SensorLogGate.IsPodeCapturarEnabled();

        if (sensorLogs)
            SensorLogGate.Log("PodeCapturarSensor", $"collect unit={(selectedUnit != null ? selectedUnit.name : "(null)")} movement={movementMode}");

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        if (selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode capturar.";
            return false;
        }

        if (movementMode != SensorMovementMode.MoveuParado && movementMode != SensorMovementMode.MoveuAndando)
        {
            reason = "Captura so pode ser avaliada em Moveu Parado ou Moveu Andando.";
            return false;
        }

        if (!selectedUnit.TryGetUnitData(out UnitData unitData) || unitData == null)
        {
            reason = "UnitData indisponivel.";
            return false;
        }

        // A capacidade e declarada pela skill, independentemente do papel de IA.
        // Assim Soldado, Bazooka, Metranca e futuras unidades usam a mesma regra.
        if (!HasCaptureConstructionSkill(unitData))
        {
            reason = "A unidade nao possui a skill Captura Construcoes.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar captura.";
            return false;
        }

        Vector3Int cell = evaluatedCell;
        cell.z = 0;
        if (matchController == null)
            matchController = Object.FindAnyObjectByType<MatchController>();
        if (matchController != null && matchController.IsFogOfWarDebugEnabled &&
            !matchController.IsCellVisibleForActiveTeam(cell) &&
            !matchController.IsCellExploredBySlot(PlayerSlotId.FromIndex(selectedUnit.SlotIndex), cell))
        {
            reason = "Terreno ainda desconhecido.";
            return false;
        }

        ConstructionManager construction = ConstructionOccupancyRules.GetConstructionAtCell(map, cell);
        if (construction == null)
        {
            reason = "Nao ha construcao no hex atual.";
            return false;
        }

        if (!construction.IsCapturable || construction.CapturePointsMax <= 0)
        {
            reason = "Construcao atual nao e capturavel.";
            return false;
        }

        if (selectedUnit.SlotIndex < 0)
        {
            reason = "Unidade neutra nao captura.";
            return false;
        }

        if (PlayerSlotRelations.AreAllies(selectedUnit.SlotIndex, construction.SlotIndex))
        {
            if (construction.CurrentCapturePoints < construction.CapturePointsMax)
            {
                targetConstruction = construction;
                operationType = CaptureOperationType.RecoverAlly;
                if (sensorLogs)
                    SensorLogGate.Log("PodeCapturarSensor", $"result hasAny=true op={operationType} construction={construction.name}");
                return true;
            }

            reason = "Construcao aliada ja esta com captura maxima.";
            return false;
        }

        targetConstruction = construction;
        operationType = CaptureOperationType.CaptureEnemy;
        if (sensorLogs)
            SensorLogGate.Log("PodeCapturarSensor", $"result hasAny=true op={operationType} construction={construction.name}");
        return true;
    }
}
