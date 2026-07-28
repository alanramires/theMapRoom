using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class UnitOccupancyRules
{
    public static event Action<UnitManager, Vector3Int, Vector3Int> OnUnitOccupancyChanged;

    private static int cachedUnitsFrame = -1;
    private static UnitManager[] cachedUnits = System.Array.Empty<UnitManager>();
    private static bool cachedUnitsFromScene;

    private static UnitManager[] GetActiveUnitsSnapshot()
    {
        int frame = Time.frameCount;
        bool playing = Application.isPlaying;
        if (cachedUnitsFrame == frame && cachedUnits != null
            && cachedUnitsFromScene == !playing)
            return cachedUnits;

        cachedUnitsFromScene = !playing;

        // UnitManager.AllActive so e populado com Application.isPlaying. Fora do
        // Play a lista fica vazia e TODA consulta de ocupacao passaria a
        // responder "hex livre" — as ferramentas de editor (LZ, embarque,
        // desembarque, pouso) enxergariam o tabuleiro inteiro vago. No editor a
        // fonte tem de ser a cena; em Play nada muda.
        if (!playing)
        {
            // Qualificado: o arquivo importa System e UnityEngine, entao
            // "Object" sozinho seria ambiguo.
            cachedUnits = UnityEngine.Object.FindObjectsByType<UnitManager>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            cachedUnitsFrame = frame;
            return cachedUnits;
        }

        var all = UnitManager.AllActive;
        if (all == null || all.Count == 0)
        {
            cachedUnits = System.Array.Empty<UnitManager>();
        }
        else
        {
            if (cachedUnits == null || cachedUnits.Length != all.Count)
                cachedUnits = new UnitManager[all.Count];
            all.CopyTo(cachedUnits);
        }
        cachedUnitsFrame = frame;
        return cachedUnits;
    }

    public static void NotifyUnitOccupancyChanged(UnitManager unit, Vector3Int previousCell, Vector3Int currentCell)
    {
        cachedUnitsFrame = -1;
        if (unit == null || !Application.isPlaying)
            return;

        previousCell.z = 0;
        currentCell.z = 0;
        OnUnitOccupancyChanged?.Invoke(unit, previousCell, currentCell);
    }

    public static bool IsUnitCellOccupied(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
            return IsUnitCellOccupiedForSlot(
                referenceTilemap, cell, PlayerSlotId.FromIndex(exceptUnit.SlotIndex), exceptUnit);

        cell.z = 0;
        if (TryGetIndexedUnitsAtCell(
                referenceTilemap,
                cell,
                out IReadOnlyList<UnitManager> indexedUnits))
        {
            int indexedCount = 0;
            for (int i = 0; i < indexedUnits.Count; i++)
            {
                UnitManager unit = indexedUnits[i];
                if (unit == null || unit == exceptUnit)
                    continue;
                indexedCount++;
                if (indexedCount >= UnitRulesDefinition.MaxUnitsPerHex)
                    return true;
            }
            return false;
        }

        int count = 0;

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell != cell)
                continue;

            count++;
            if (count >= UnitRulesDefinition.MaxUnitsPerHex)
                return true;
        }

        return false;
    }

    public static bool IsUnitCellOccupiedForSlot(
        Tilemap referenceTilemap,
        Vector3Int cell,
        PlayerSlotId slot,
        UnitManager exceptUnit = null)
    {
        cell.z = 0;

        if (TryGetIndexedUnitsAtCell(
                referenceTilemap,
                cell,
                out IReadOnlyList<UnitManager> indexedUnits))
        {
            for (int i = 0; i < indexedUnits.Count; i++)
            {
                UnitManager unit = indexedUnits[i];
                if (unit != null
                    && unit != exceptUnit
                    && unit.SlotIndex == slot.Value)
                    return true;
            }
            return false;
        }

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                continue;
            if (unit.SlotIndex != slot.Value)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return true;
        }

        return false;
    }

    public static UnitManager GetUnitAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        cell.z = 0;

        if (TryGetIndexedUnitsAtCell(
                referenceTilemap,
                cell,
                out IReadOnlyList<UnitManager> indexedUnits))
        {
            // A escolha de "qual" unidade retornar em coabitação faz parte do
            // comportamento histórico de AllActive. Preserve a ordem antiga
            // somente nesse caso raro; 0/1 ocupante usa o acesso direto.
            if (indexedUnits.Count <= 1)
            {
                for (int i = 0; i < indexedUnits.Count; i++)
                {
                    UnitManager unit = indexedUnits[i];
                    if (unit == null || unit == exceptUnit)
                        continue;
                    return unit;
                }
                return null;
            }
        }

        if (UnitRulesDefinition.IsTotalWarEnabled() && exceptUnit != null)
        {
            // Prioriza retornar bloqueador do mesmo time para evitar empilhamento
            // quando coexistencia entre times diferentes for permitida.
            UnitManager sameTeam = null;
            UnitManager otherTeam = null;

            UnitManager[] totalWarUnits = GetActiveUnitsSnapshot();
            for (int i = 0; i < totalWarUnits.Length; i++)
            {
                UnitManager unit = totalWarUnits[i];
                if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                    continue;
                if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                    continue;

                Vector3Int occupiedCell = unit.CurrentCellPosition;
                occupiedCell.z = 0;
                if (occupiedCell != cell)
                    continue;

                if (PlayerSlotRelations.AreAllies(unit, exceptUnit))
                {
                    sameTeam = unit;
                    break;
                }

                if (otherTeam == null)
                    otherTeam = unit;
            }

            if (sameTeam != null)
                return sameTeam;
            if (otherTeam != null)
                return otherTeam;
        }

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                return unit;
        }

        return null;
    }

    public static List<UnitManager> GetUnitsAtCell(Tilemap referenceTilemap, Vector3Int cell, UnitManager exceptUnit = null)
    {
        if (TryGetIndexedUnitsAtCell(
                referenceTilemap,
                cell,
                out IReadOnlyList<UnitManager> indexedUnits))
        {
            // Listas com coabitação mantêm a ordem histórica de AllActive,
            // pois alguns consumidores escolhem o primeiro ocupante.
            if (indexedUnits.Count <= 1)
            {
                var indexedCopy =
                    new List<UnitManager>(indexedUnits.Count);
                for (int i = 0; i < indexedUnits.Count; i++)
                {
                    UnitManager unit = indexedUnits[i];
                    if (unit != null && unit != exceptUnit)
                        indexedCopy.Add(unit);
                }
                return indexedCopy;
            }
        }

        List<UnitManager> result = new List<UnitManager>();
        cell.z = 0;

        UnitManager[] units = GetActiveUnitsSnapshot();
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || unit == exceptUnit || unit.IsEmbarked || unit.IsDead)
                continue;
            if (!IsUnitOnReferenceMap(unit, referenceTilemap))
                continue;

            Vector3Int occupiedCell = unit.CurrentCellPosition;
            occupiedCell.z = 0;
            if (occupiedCell == cell)
                result.Add(unit);
        }

        return result;
    }

    /// <summary>
    /// Informa se existe um ocupante que realmente impede a unidade de usar a
    /// célula na camada operacional atual. Compartilhar coordenada não basta:
    /// Air, Sub e superfície podem coexistir quando pertencem a bandas
    /// diferentes, e convés/água possui sua própria exceção.
    /// </summary>
    public static bool HasBlockingOccupantForUnitAtCell(
        Tilemap referenceTilemap,
        Vector3Int cell,
        UnitManager unit,
        bool alliedOnly = false)
    {
        if (unit == null)
            return false;

        cell.z = 0;
        List<UnitManager> occupants =
            GetUnitsAtCell(referenceTilemap, cell, unit);
        if (occupants.Count == 0)
            return false;

        if (!OccupancyResolver.IsLayerAwareRulesActive)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                UnitManager occupant = occupants[i];
                if (occupant == null)
                    continue;
                if (!alliedOnly ||
                    PlayerSlotRelations.AreAllies(unit, occupant))
                {
                    return true;
                }
            }
            return false;
        }

        IReadOnlyList<UnitManager> considered = occupants;
        if (alliedOnly)
        {
            List<UnitManager> alliedOccupants =
                new List<UnitManager>(occupants.Count);
            for (int i = 0; i < occupants.Count; i++)
            {
                UnitManager occupant = occupants[i];
                if (occupant != null &&
                    PlayerSlotRelations.AreAllies(unit, occupant))
                {
                    alliedOccupants.Add(occupant);
                }
            }
            if (alliedOccupants.Count == 0)
                return false;
            considered = alliedOccupants;
        }

        // CanEndMove é a autoridade: resolve bandas diferentes, aliados,
        // convivência permitida pelo modo de jogo e a separação especial
        // entre convés e água. A IA não deve inventar um bloqueio adicional.
        return !OccupancyResolver.CanEndMove(unit, cell, considered);
    }

    public static bool CanEndLayerTransitionAtCell(
        Tilemap referenceTilemap,
        Vector3Int cell,
        UnitManager unit,
        Domain targetDomain,
        HeightLevel targetHeight,
        out UnitManager blocker,
        bool ignoreSameTeamAirBlocker = false)
    {
        blocker = null;
        if (unit == null)
            return false;
        if (!OccupancyResolver.IsLayerAwareRulesActive)
            return true;

        List<UnitManager> occupants = GetUnitsAtCell(referenceTilemap, cell, unit);
        if (ignoreSameTeamAirBlocker && OccupancyResolver.GetHeightBand(targetDomain, targetHeight) == HeightBand.Air)
        {
            for (int i = occupants.Count - 1; i >= 0; i--)
            {
                UnitManager occupant = occupants[i];
                if (occupant == null)
                    continue;
                if (PlayerSlotRelations.AreAllies(occupant, unit) && OccupancyResolver.GetHeightBand(occupant) == HeightBand.Air)
                    occupants.RemoveAt(i);
            }
        }

        if (OccupancyResolver.CanEndLayerTransition(unit, targetDomain, targetHeight, occupants))
            return true;

        HeightBand targetBand = OccupancyResolver.GetHeightBand(targetDomain, targetHeight);
        for (int i = 0; i < occupants.Count; i++)
        {
            UnitManager occupant = occupants[i];
            if (occupant == null)
                continue;
            if (OccupancyResolver.GetHeightBand(occupant) != targetBand)
                continue;
            // Em Air/Sub apenas o aliado bloqueia; em Blocking qualquer ocupante bloqueia.
            if (targetBand != HeightBand.Blocking && !PlayerSlotRelations.AreAllies(occupant, unit))
                continue;
            blocker = occupant;
            break;
        }

        return false;
    }

    private static bool TryGetIndexedUnitsAtCell(
        Tilemap referenceTilemap,
        Vector3Int cell,
        out IReadOnlyList<UnitManager> units)
    {
        units = null;
        if (!Application.isPlaying
            || referenceTilemap == null
            || !ConfirmedOccupancyIndex.TryGetFor(
                referenceTilemap,
                out ConfirmedOccupancyIndex index)
            || index == null
            || !index.CanServeLiveQueries)
        {
            return false;
        }

        units = index.GetUnitsAtCell(cell);
        return true;
    }

    private static bool IsUnitOnReferenceMap(UnitManager unit, Tilemap referenceTilemap)
    {
        if (unit == null || referenceTilemap == null)
            return false;
        if (unit.BoardTilemap == null || unit.BoardTilemap != referenceTilemap)
            return false;

        return unit.gameObject.scene == referenceTilemap.gameObject.scene;
    }
}
