using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fase de compras da IA: ao final do turno (sem unidades restantes),
/// envia um batch de Shopping para cada construção aliada desocupada
/// que tenha Selling Rules habilitado — exatamente como o replay faz.
/// Partial class de AIPlayerOrchestrator.
/// </summary>
public partial class AIPlayerOrchestrator
{
    /// <summary>
    /// Coroutine que compra 1 soldado em cada construção aliada desocupada,
    /// enviando um PlayerAction de Shopping por construção e aguardando
    /// o ReplayManager terminar cada batch antes de continuar.
    /// </summary>
    private IEnumerator DoShoppingPhase(TeamId myTeam)
    {
        // Snapshot de células ocupadas (qualquer unidade viva, não embarcada)
        var occupied = new HashSet<Vector3Int>();
        foreach (UnitManager u in UnitManager.AllActive)
        {
            if (u.IsDead || u.IsEmbarked) continue;
            Vector3Int p = u.CurrentCellPosition; p.z = 0;
            occupied.Add(p);
        }

        foreach (ConstructionManager construction in ConstructionManager.AllActive)
        {
            // Só construções aliadas com mercado habilitado
            if (!construction.CanProduceUnitsForTeam(myTeam))
                continue;

            // Célula desocupada
            Vector3Int cell = construction.CurrentCellPosition; cell.z = 0;
            if (occupied.Contains(cell))
                continue;

            // Decide qual unidade comprar (primeiro soldado; fallback: índice 0)
            UnitData unitToBuy = FindSoldado(construction);
            if (unitToBuy == null)
                continue;

            int selectedIndex = IndexOf(construction.OfferedUnits, unitToBuy);

            PlayerAction batch = BuildShoppingBatch(myTeam, cell, selectedIndex, unitToBuy);
            Debug.Log($"[AI][Shopping] Comprando {unitToBuy.name} em {cell} (idx={selectedIndex}).");

            replayManager.ExecuteLiveAIBatch(batch);
            yield return new WaitUntil(() => !replayManager.IsStepExecutionBusy);

            // Garante que o cursor voltou ao Neutral — se a compra falhou (sem dinheiro, etc.)
            // o menu de shopping pode ter ficado aberto, o que trava o CommandService no próximo turno.
            if (turnStateManager != null &&
                turnStateManager.CurrentCursorState == TurnStateManager.CursorState.ShoppingAndServices)
            {
                Debug.LogWarning($"[AI][Shopping] Menu ficou aberto após batch em {cell}. Fechando.");
                turnStateManager.HandleCancel();
            }

            // Marca a célula como ocupada para as próximas construções desta rodada
            occupied.Add(cell);
        }
    }

    private PlayerAction BuildShoppingBatch(TeamId myTeam, Vector3Int cell, int selectedIndex, UnitData unit)
    {
        return new PlayerAction
        {
            IsAIGenerated        = true,
            ActionType           = PlayerActionType.Shopping,
            ActingTeam           = myTeam,
            TurnNumber           = matchController.CurrentTurn,
            CursorHex            = cell,
            HasCursorHex         = true,
            TargetHex            = cell,
            SensorAction         = SensorActionType.Shopping,
            ShoppingSelectedIndex = selectedIndex,
            ShoppingUnitTypeId   = !string.IsNullOrWhiteSpace(unit.id) ? unit.id : unit.name,
            Confirmed            = true,
            DebugLabel           = $"AI Shopping: {unit.name} @ {cell}",
        };
    }

    /// <summary>
    /// Retorna o primeiro UnitData com "soldado" no id ou nome (case-insensitive),
    /// ou o primeiro da lista se nenhum bater.
    /// </summary>
    private static UnitData FindSoldado(ConstructionManager construction)
    {
        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return null;

        foreach (UnitData u in offered)
        {
            if (u == null) continue;
            string id   = u.id   ?? string.Empty;
            string name = u.name ?? string.Empty;
            if (id.IndexOf("soldado",   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("soldado", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return u;
        }

        return offered[0];
    }

    private static int IndexOf(IReadOnlyList<UnitData> list, UnitData target)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == target) return i;
        return 0;
    }
}
