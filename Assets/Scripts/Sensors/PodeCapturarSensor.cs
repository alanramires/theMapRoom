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

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out string reason)
    {
        return TryGetCaptureTarget(
            selectedUnit,
            boardTilemap,
            movementMode,
            out targetConstruction,
            out _,
            out reason);
    }

    public static bool TryGetCaptureTarget(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        SensorMovementMode movementMode,
        out ConstructionManager targetConstruction,
        out CaptureOperationType operationType,
        out string reason)
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

        // Aceita qualquer papel que SATISFAÇA Capturador (Capturador e CapturadorAgressivo) — o check
        // canônico de compatibilidade, mesmo que a AI usa. O Contains estrito anterior rejeitava o
        // CapturadorAgressivo (ex.: Bazooka) e fazia o batch de captura falhar mesmo com slots ok.
        if (!UnitRoleCompatibility.CanSatisfy(unitData, UnitRole.Capturador))
        {
            reason = "Apenas unidades com papel de Capturador (ou Capturador Agressivo) podem capturar.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar captura.";
            return false;
        }

        Vector3Int cell = selectedUnit.CurrentCellPosition;
        cell.z = 0;
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

        TeamId unitTeam = selectedUnit.TeamId;
        if (unitTeam == TeamId.Neutral)
        {
            reason = "Unidade neutra nao captura.";
            return false;
        }

        if (construction.TeamId == unitTeam)
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
