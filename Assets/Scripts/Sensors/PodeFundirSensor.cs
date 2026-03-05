using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class PodeFundirOption
{
    public UnitManager receiverUnit;
    public UnitManager candidateUnit;
    public Vector3Int candidateCell;
    public int remainingMovement;
    public int requiredMovementCost;
    public string displayLabel;
}

public class PodeFundirInvalidOption
{
    public const string ReasonIdInsufficientMovement = "fuse.invalid.insufficient_movement";

    public UnitManager receiverUnit;
    public UnitManager candidateUnit;
    public Vector3Int candidateCell;
    public int remainingMovement;
    public int requiredMovementCost;
    public string reasonId;
    public string reason;
}

public static class PodeFundirSensor
{
    public static bool TryEvaluate(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        out int sameTypeAdjacentCount,
        out string reason)
    {
        List<PodeFundirOption> options = new List<PodeFundirOption>();
        bool canMerge = CollectOptions(
            selectedUnit,
            boardTilemap,
            terrainDatabase: null,
            output: options,
            reason: out reason,
            invalidOutput: null);
        sameTypeAdjacentCount = options.Count;
        return canMerge;
    }

    public static bool CollectOptions(
        UnitManager selectedUnit,
        Tilemap boardTilemap,
        TerrainDatabase terrainDatabase,
        List<PodeFundirOption> output,
        out string reason,
        List<PodeFundirInvalidOption> invalidOutput = null)
    {
        reason = string.Empty;
        output?.Clear();
        invalidOutput?.Clear();

        if (selectedUnit == null)
        {
            reason = "Selecione uma unidade.";
            return false;
        }

        if (selectedUnit.IsEmbarked)
        {
            reason = "Unidade embarcada nao pode fundir.";
            return false;
        }

        if (selectedUnit.CurrentHP >= 10)
        {
            reason = "Unidade com HP maximo nao pode fundir.";
            return false;
        }

        Tilemap map = boardTilemap != null ? boardTilemap : selectedUnit.BoardTilemap;
        if (map == null)
        {
            reason = "Tilemap indisponivel para avaliar fusao.";
            return false;
        }

        Vector3Int origin = selectedUnit.CurrentCellPosition;
        origin.z = 0;
        List<Vector3Int> neighbors = new List<Vector3Int>(6);
        UnitMovementPathRules.GetImmediateHexNeighbors(map, origin, neighbors);

        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3Int cell = neighbors[i];
            cell.z = 0;

            UnitManager other = UnitOccupancyRules.GetUnitAtCell(map, cell, selectedUnit);
            if (other == null || other == selectedUnit || !other.gameObject.activeInHierarchy || other.IsEmbarked)
                continue;

            if (!IsSameTypeAndTeam(selectedUnit, other))
                continue;

            bool valid = EvaluateCandidateMovement(
                selectedUnit,
                other,
                map,
                terrainDatabase,
                out string invalidReasonId,
                out string invalidReason,
                out int requiredMovementCost,
                out int remainingMovement);

            if (valid)
            {
                output?.Add(new PodeFundirOption
                {
                    receiverUnit = selectedUnit,
                    candidateUnit = other,
                    candidateCell = cell,
                    remainingMovement = remainingMovement,
                    requiredMovementCost = requiredMovementCost,
                    displayLabel = $"{other.name} ({cell.x},{cell.y})"
                });
            }
            else if (invalidOutput != null)
            {
                invalidOutput.Add(new PodeFundirInvalidOption
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

        int validCount = output != null ? output.Count : 0;
        int invalidCount = invalidOutput != null ? invalidOutput.Count : 0;
        if (validCount > 0)
            return true;

        if (invalidCount > 0)
        {
            string firstReason = !string.IsNullOrWhiteSpace(invalidOutput[0].reason)
                ? invalidOutput[0].reason
                : "Sem candidatos validos para fusao.";
            reason = $"Sem candidatos validos para fusao. {firstReason}";
            return true;
        }

        reason = "Sem unidade adjacente (1 hex) do mesmo tipo para fundir.";
        return false;
    }

    public static bool EvaluateCandidateMovement(
        UnitManager receiver,
        UnitManager candidate,
        Tilemap map,
        TerrainDatabase terrainDatabase,
        out string invalidReasonId,
        out string invalidReason,
        out int requiredMovementCost,
        out int remainingMovement)
    {
        invalidReasonId = string.Empty;
        invalidReason = string.Empty;
        requiredMovementCost = 0;
        remainingMovement = 0;

        if (receiver == null || candidate == null || map == null)
        {
            invalidReason = "Contexto de fusao invalido.";
            return false;
        }

        remainingMovement = Mathf.Max(0, candidate.RemainingMovementPoints);
        Vector3Int receiverCell = receiver.CurrentCellPosition;
        receiverCell.z = 0;

        Dictionary<Vector3Int, List<Vector3Int>> validPaths = UnitMovementPathRules.CalcularCaminhosValidos(
            map,
            candidate,
            remainingMovement,
            terrainDatabase);

        if (validPaths != null &&
            validPaths.TryGetValue(receiverCell, out List<Vector3Int> mergePath) &&
            mergePath != null &&
            mergePath.Count >= 2)
        {
            requiredMovementCost = Mathf.Max(0, UnitMovementPathRules.CalculateAutonomyCostForPath(
                map,
                candidate,
                mergePath,
                terrainDatabase,
                applyOperationalAutonomyModifier: false));

            if (requiredMovementCost > remainingMovement)
            {
                invalidReasonId = PodeFundirInvalidOption.ReasonIdInsufficientMovement;
                invalidReason = $"Movimento insuficiente ({remainingMovement}/{requiredMovementCost}).";
                return false;
            }

            return true;
        }

        if (!UnitMovementPathRules.TryGetEnterCellCost(
                map,
                candidate,
                receiverCell,
                terrainDatabase,
                applyOperationalAutonomyModifier: false,
                out requiredMovementCost))
        {
            invalidReason = "Nao consegue entrar no hex do receptor (terreno/camada bloqueia).";
            return false;
        }

        requiredMovementCost = Mathf.Max(0, requiredMovementCost);
        if (requiredMovementCost > remainingMovement)
        {
            invalidReasonId = PodeFundirInvalidOption.ReasonIdInsufficientMovement;
            invalidReason = $"Movimento insuficiente ({remainingMovement}/{requiredMovementCost}).";
            return false;
        }

        invalidReason = "Sem caminho valido ate o receptor (bloqueio no trajeto).";
        return false;
    }

    private static bool IsSameTypeAndTeam(UnitManager a, UnitManager b)
    {
        if (a == null || b == null)
            return false;

        if ((int)a.TeamId != (int)b.TeamId)
            return false;

        string aId = a.UnitId;
        string bId = b.UnitId;
        if (!string.IsNullOrWhiteSpace(aId) && !string.IsNullOrWhiteSpace(bId))
            return string.Equals(aId.Trim(), bId.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (a.TryGetUnitData(out UnitData aData) && b.TryGetUnitData(out UnitData bData))
            return aData != null && bData != null && aData == bData;

        return false;
    }
}
