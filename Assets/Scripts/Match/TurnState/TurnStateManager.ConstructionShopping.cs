using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private bool shoppingSuppressDefaultConfirmSfxOnce;

    private bool ConsumeShoppingSuppressDefaultConfirmSfxOnce()
    {
        if (!shoppingSuppressDefaultConfirmSfxOnce)
            return false;
        shoppingSuppressDefaultConfirmSfxOnce = false;
        return true;
    }

    private bool TryEnterConstructionShoppingState(ConstructionManager construction, int activeSlot)
    {
        if (construction == null || activeSlot < 0)
            return false;

        if (!construction.CanProduceUnitsForSlot(activeSlot))
            return false;

        IReadOnlyList<UnitData> offered = construction.OfferedUnits;
        if (offered == null || offered.Count == 0)
            return false;

        shoppingUnitsForSale.Clear();
        for (int i = 0; i < offered.Count; i++)
        {
            UnitData unit = offered[i];
            if (unit == null)
                continue;

            shoppingUnitsForSale.Add(unit);
        }

        if (shoppingUnitsForSale.Count == 0)
            return false;

        shoppingConstruction = construction;
        shoppingSelectedIndex = 0;
        shoppingCancelFocused = false;
        Advance(CursorState.ShoppingAndServices, "TryEnterConstructionShoppingState: ally construction with units for sale");
        RefreshShoppingSelectionPresentation(logOptions: false);
        LogConstructionShoppingPanel();
        return true;
    }

    public bool TryOpenConstructionShoppingFromPointer(Vector3Int cell)
    {
        if (CurrentCursorState != CursorState.Neutral)
            return false;

        cell.z = 0;
        ConstructionManager construction = FindConstructionAtCell(cell);
        int activeSlot = matchController != null ? matchController.ActiveSlotId.Value : -1;
        if (!TryEnterConstructionShoppingState(construction, activeSlot))
            return false;

        DialogManager.Instance?.MarkHintLearned(matchController.ActiveTeam, HelpHintId.Produce);
        cursorController?.PlayConfirmSfx();
        return true;
    }

    private void ExitConstructionShoppingStateToNeutral(bool rollback)
    {
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();
        shoppingSelectedIndex = -1;
        shoppingCancelFocused = false;
        PanelDialogController.ClearExternalText();
        if (rollback)
            Retreat("ExitConstructionShoppingStateToNeutral");
        else
            ExecuteAndReset("ExitConstructionShoppingStateToNeutral");
    }

    private void ProcessConstructionShoppingInput()
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return;

        if (shoppingConstruction == null || shoppingUnitsForSale.Count == 0)
        {
            ExitConstructionShoppingStateToNeutral(rollback: true);
            return;
        }

        if (!TryReadShoppingPressedNumber(out int number))
            return;

        int index = number - 1;
        if (index < 0 || index >= shoppingUnitsForSale.Count)
        {
            cursorController?.PlayErrorSfx();
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[Shopping] Opcao invalida: {number}. Escolha entre 1 e {shoppingUnitsForSale.Count}.");
            return;
        }

        shoppingSelectedIndex = index;
        shoppingCancelFocused = false;
        RefreshShoppingSelectionPresentation(logOptions: false);
        TryPurchaseShoppingUnitByIndex(index);
    }

    private bool TryConfirmSelectedShoppingOption()
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return false;
        if (shoppingConstruction == null || shoppingUnitsForSale.Count <= 0)
            return false;

        int index = ClampShoppingSelectedIndex();
        if (index < 0 || index >= shoppingUnitsForSale.Count)
            return false;

        return TryPurchaseShoppingUnitByIndex(index);
    }

    public int ShoppingOptionCount => shoppingUnitsForSale != null ? shoppingUnitsForSale.Count : 0;

    public int ShoppingSelectedOptionIndex => ClampShoppingSelectedIndex();

    // Foco no botao CANCELAR da lista de compra: um slot virtual logo apos o ultimo item. Permite
    // sair da loja navegando ate ele com as setas e confirmando — sem mouse e sem Esc.
    private bool shoppingCancelFocused;
    public bool ShoppingCancelFocused =>
        shoppingCancelFocused && CurrentCursorState == CursorState.ShoppingAndServices;

    public int ShoppingSelectedOptionCost
    {
        get
        {
            int index = ClampShoppingSelectedIndex();
            if (index < 0 || index >= shoppingUnitsForSale.Count || shoppingUnitsForSale[index] == null)
                return 0;

            UnitData unit = shoppingUnitsForSale[index];
            return matchController != null
                ? matchController.ResolveEconomyCost(unit.cost)
                : Mathf.Max(0, unit.cost);
        }
    }

    public bool ShoppingSelectedOptionCanPurchase
    {
        get
        {
            int index = ClampShoppingSelectedIndex();
            if (index < 0 || index >= shoppingUnitsForSale.Count || shoppingUnitsForSale[index] == null)
                return false;

            UnitData unit = shoppingUnitsForSale[index];
            TeamId team = matchController != null && matchController.ActiveTeamId >= 0
                ? (TeamId)matchController.ActiveTeamId
                : (shoppingConstruction != null ? shoppingConstruction.TeamId : TeamId.Neutral);
            if (matchController != null && !matchController.CanProduceUnit(team, unit, out _))
                return false;
            return matchController == null || matchController.GetActualMoney(team) >= ShoppingSelectedOptionCost;
        }
    }

    public bool TrySelectShoppingOptionFromPointer(int direction)
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices || shoppingUnitsForSale.Count <= 1 || direction == 0)
            return false;

        int count = shoppingUnitsForSale.Count;
        int currentIndex = ClampShoppingSelectedIndex();
        shoppingSelectedIndex = (currentIndex + (direction > 0 ? 1 : -1) + count) % count;
        shoppingCancelFocused = false;
        cursorController?.PlayCursorMoveSfx();
        RefreshShoppingSelectionPresentation(logOptions: true);
        return true;
    }

    public bool TryConfirmShoppingFromPointer()
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return false;

        return TryConfirmSelectedShoppingOption();
    }

    public bool TryPurchaseShoppingOptionFromPointer(int index)
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices ||
            index < 0 || index >= shoppingUnitsForSale.Count)
            return false;

        shoppingSelectedIndex = index;
        shoppingCancelFocused = false;
        RefreshShoppingSelectionPresentation(logOptions: false);
        return TryPurchaseShoppingUnitByIndex(index);
    }

    public bool TryConfirmShoppingByClickingConstruction(Vector3Int clickedCell)
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices || shoppingConstruction == null)
            return false;

        clickedCell.z = 0;
        Vector3Int constructionCell = shoppingConstruction.CurrentCellPosition;
        constructionCell.z = 0;
        if (clickedCell != constructionCell)
            return false;

        if (!AllowsContextualPointerConfirmation())
            return true;

        return TryConfirmSelectedShoppingOption();
    }

    public bool TryCancelShoppingFromPointer()
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return false;

        ExitConstructionShoppingStateToNeutral(rollback: true);
        cursorController?.PlayCancelSfx();
        return true;
    }

    private bool TryPurchaseShoppingUnitByIndex(int index)
    {
        if (index < 0 || index >= shoppingUnitsForSale.Count)
            return false;

        UnitData unit = shoppingUnitsForSale[index];
        if (unit == null)
        {
            Debug.LogWarning("[Shopping] Unidade selecionada esta nula.");
            return false;
        }

        if (unitSpawner == null)
        {
            Debug.LogWarning("[Shopping] UnitSpawner nao encontrado na cena.");
            return false;
        }

        int activeSlot = matchController != null ? matchController.ActiveSlotId.Value : shoppingConstruction.SlotIndex;
        TeamId spawnTeam = matchController != null && activeSlot >= 0
            ? matchController.GetTeamIdForSlot(activeSlot)
            : shoppingConstruction.TeamId;
        if (matchController != null && !matchController.CanProduceUnit(spawnTeam, unit, out string requirementReason))
        {
            PushPanelUnitMessage(requirementReason, 3.2f);
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[Shopping] Requisito nao cumprido para {ResolveUnitName(unit)}: {requirementReason}");
            return false;
        }
        if (matchController != null && matchController.HasReachedMaxUnitsPerTeam(spawnTeam))
        {
            cursorController?.PlayErrorSfx();
            Debug.LogError($"[Shopping] Limite de unidades atingido para {TeamUtils.GetName(spawnTeam)} ({matchController.MaxUnitsPerTeam}).");
            return false;
        }

        int economyBefore = matchController != null ? matchController.GetActualMoney(spawnTeam) : 0;
        int unitCost = matchController != null
            ? matchController.ResolveEconomyCost(unit.cost)
            : Mathf.Max(0, unit.cost);
        if (matchController != null)
        {
            int currentMoney = matchController.GetActualMoney(spawnTeam);
            if (currentMoney < unitCost)
            {
                PushPanelUnitMessage("Sem dinheiro suficiente", 2.6f);
                cursorController?.PlayErrorSfx();
                Debug.LogWarning($"[Shopping] Dinheiro insuficiente para comprar {ResolveUnitName(unit)}. Custo=${unitCost}, saldo=${currentMoney}.");
                return false;
            }
        }

        Vector3Int spawnCell = shoppingConstruction.CurrentCellPosition;
        spawnCell.z = 0;
        if (IsShoppingSpawnCellBlocked(shoppingConstruction, unit, spawnCell, activeSlot, out string blockedReason))
        {
            PushPanelUnitMessage(blockedReason, 2.8f);
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[Shopping] Compra bloqueada em {spawnCell}: {blockedReason}");
            return false;
        }

        GameObject spawned = unitSpawner.SpawnAtCell(unit, spawnTeam, spawnCell);
        if (spawned == null)
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[Shopping] Falha ao comprar {ResolveUnitName(unit)}. Verifique ocupacao/camada da celula.");
            return false;
        }

        UnitManager spawnedUnitManager = spawned.GetComponent<UnitManager>();
        if (spawnedUnitManager != null)
            spawnedUnitManager.SetSlotIndex(activeSlot);
        TryApplyForcedSpawnLayerFromCell(spawnedUnitManager, spawnCell);

        int remainingMoney = matchController != null ? matchController.GetActualMoney(spawnTeam) : 0;
        if (matchController != null && !matchController.TrySpendActualMoney(spawnTeam, unitCost, out remainingMoney))
        {
            // Protecao contra corrida/estado inesperado: se falhou no debito, desfaz spawn.
            Destroy(spawned);
            cursorController?.PlayErrorSfx();
            Debug.LogError($"[Shopping] Falha ao debitar custo da unidade {ResolveUnitName(unit)}. Saldo atual=${remainingMoney}, custo=${unitCost}.");
            return false;
        }

        int economyAfter = matchController != null ? matchController.GetActualMoney(spawnTeam) : economyBefore;
        RecordShoppingBuyReplayCommand(spawned, unit, spawnTeam, spawnCell, economyBefore, economyAfter, index);

        if (matchController != null)
            PanelMoneyController.PushContextualUpdate(spawnTeam, remainingMoney, ResolveUnitName(unit), -unitCost);

        shoppingSuppressDefaultConfirmSfxOnce = true;
        cursorController?.PlayDoneSfx();
        if (enableTurnStateRuntimeLogs)
            Debug.Log($"[Shopping] Compra concluida: {ResolveUnitName(unit)} por ${unitCost} em {ResolveConstructionName(shoppingConstruction)}.");
        OnUnitPurchased?.Invoke(spawnedUnitManager);
        // A compra ja foi comprometida, mas a publicacao do FOW confirmado deve esperar
        // o retorno a Neutral. Atualize somente a contribuicao da unidade nova; FullVisual
        // apagaria todos os caches e recalcularia a visao de cada unidade em campo.
        matchController?.NotifyCommittedUnitSpawnedForFog(spawnedUnitManager);
        ExitConstructionShoppingStateToNeutral(rollback: false);
        return true;
    }

    private void TryApplyForcedSpawnLayerFromCell(UnitManager spawnedUnit, Vector3Int spawnCell)
    {
        if (spawnedUnit == null)
            return;

        if (spawnedUnit.GetAircraftType() != AircraftType.None)
            spawnedUnit.SetAircraftGrounded(true);

        Tilemap boardMap = terrainTilemap != null ? terrainTilemap : spawnedUnit.BoardTilemap;
        if (boardMap == null)
            return;

        Domain currentDomain = spawnedUnit.GetDomain();
        HeightLevel currentHeight = spawnedUnit.GetHeightLevel();
        if (!TryResolveForcedEndMovementTargetForCell(
                boardMap,
                terrainDatabase,
                spawnCell,
                currentDomain,
                currentHeight,
                out Domain forcedDomain,
                out HeightLevel forcedHeight,
                out _))
        {
            return;
        }

        if (forcedDomain == currentDomain && forcedHeight == currentHeight)
            return;
        if (!spawnedUnit.SupportsLayerMode(forcedDomain, forcedHeight))
            return;

        spawnedUnit.TrySetCurrentLayerMode(forcedDomain, forcedHeight);
    }

    private int ClampShoppingSelectedIndex()
    {
        if (shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
        {
            shoppingSelectedIndex = -1;
            return -1;
        }

        if (shoppingSelectedIndex < 0 || shoppingSelectedIndex >= shoppingUnitsForSale.Count)
            shoppingSelectedIndex = Mathf.Clamp(shoppingSelectedIndex, 0, shoppingUnitsForSale.Count - 1);
        return shoppingSelectedIndex;
    }

    public int GetShoppingSelectedIndexForReplay()
    {
        return ClampShoppingSelectedIndex();
    }

    public string GetShoppingSelectedUnitTypeIdForReplay()
    {
        int index = ClampShoppingSelectedIndex();
        if (index < 0 || shoppingUnitsForSale == null || index >= shoppingUnitsForSale.Count)
            return null;

        UnitData unit = shoppingUnitsForSale[index];
        if (unit == null)
            return null;

        return !string.IsNullOrWhiteSpace(unit.id) ? unit.id : unit.name;
    }

    public bool TryResolveShoppingCursorMoveForReplay(Vector3Int inputDelta)
    {
        if (cursorController == null)
            return false;

        return TryResolveShoppingCursorMove(cursorController.CurrentCell, inputDelta);
    }

    public bool TryConfirmSelectedShoppingOptionForReplay()
    {
        return TryConfirmSelectedShoppingOption();
    }

    // Seleciona a unidade pelo id no menu de compras atual (usado pela IA para apontar para o soldado antes de confirmar).
    // Retorna false se nao estiver no estado ShoppingAndServices ou se a unidade nao for encontrada.
    public bool TryAISetShoppingSelectedUnit(string unitId)
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return false;
        if (shoppingUnitsForSale == null || shoppingUnitsForSale.Count == 0)
            return false;

        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData u = shoppingUnitsForSale[i];
            if (u != null && string.Equals(u.id, unitId, System.StringComparison.OrdinalIgnoreCase))
            {
                shoppingSelectedIndex = i;
                return true;
            }
        }
        return false;
    }

    // Compra direta para a IA — sem estado de cursor nem UI.
    public bool TryAIDirectPurchase(ConstructionManager construction, UnitData unit, TeamId team)
    {
        if (construction == null || unit == null)
            return false;
        int ownerSlot = construction.SlotIndex;
        if (!construction.CanProduceUnitsForSlot(ownerSlot))
            return false;
        if (matchController != null && !matchController.CanProduceUnit(team, unit, out string requirementReason))
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[AI][Shopping] Requisito nao cumprido para {ResolveUnitName(unit)}: {requirementReason}");
            return false;
        }
        if (unitSpawner == null)
            return false;
        if (matchController != null && matchController.HasReachedMaxUnitsPerTeam(team))
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[AI][Shopping] Limite de unidades atingido para {team}.");
            return false;
        }

        int cost = matchController != null ? matchController.ResolveEconomyCost(unit.cost) : Mathf.Max(0, unit.cost);
        if (matchController != null && matchController.GetActualMoney(team) < cost)
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[AI][Shopping] Sem dinheiro para {ResolveUnitName(unit)}. Custo=${cost}, saldo={matchController.GetActualMoney(team)}.");
            return false;
        }

        Vector3Int spawnCell = construction.CurrentCellPosition;
        spawnCell.z = 0;
        if (IsShoppingSpawnCellBlocked(construction, unit, spawnCell, ownerSlot, out string blockedReason))
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[AI][Shopping] Compra bloqueada em {spawnCell}: {blockedReason}");
            return false;
        }

        int economyBefore = matchController != null ? matchController.GetActualMoney(team) : 0;

        GameObject spawned = unitSpawner.SpawnAtCell(unit, team, spawnCell);
        if (spawned == null)
        {
            if (enableTurnStateRuntimeLogs)
                Debug.Log($"[AI][Shopping] Falha ao spawnar {ResolveUnitName(unit)} em {spawnCell}.");
            return false;
        }

        UnitManager spawnedManager = spawned.GetComponent<UnitManager>();
        if (spawnedManager != null)
            spawnedManager.SetSlotIndex(ownerSlot);
        TryApplyForcedSpawnLayerFromCell(spawnedManager, spawnCell);

        int remainingMoney;
        if (matchController != null && !matchController.TrySpendActualMoney(team, cost, out remainingMoney))
        {
            Destroy(spawned);
            Debug.LogError($"[AI][Shopping] Falha ao debitar custo de {ResolveUnitName(unit)}.");
            return false;
        }

        int economyAfter = matchController != null ? matchController.GetActualMoney(team) : economyBefore;
        RecordShoppingBuyReplayCommand(spawned, unit, team, spawnCell, economyBefore, economyAfter, 0);

        if (enableTurnStateRuntimeLogs)
            Debug.Log($"[AI][Shopping] Comprado: {ResolveUnitName(unit)} para {team} em {spawnCell}. Saldo restante: {economyAfter}.");

        OnUnitPurchased?.Invoke(spawnedManager);
        matchController?.NotifyCommittedUnitSpawnedForFog(spawnedManager);
        return true;
    }

    private bool IsShoppingSpawnCellBlocked(
        ConstructionManager construction,
        UnitData unit,
        Vector3Int spawnCell,
        int spawnSlot,
        out string reason)
    {
        reason = "";
        if (construction == null || unit == null)
            return false;

        spawnCell.z = 0;
        HeightBand spawnBand = ResolveShoppingSpawnHeightBand(unit, spawnCell);
        if (spawnBand != HeightBand.Blocking)
            return false;

        foreach (UnitManager occupant in UnitManager.AllActive)
        {
            if (occupant == null || occupant.IsDead || occupant.IsEmbarked)
                continue;

            Vector3Int occupantCell = occupant.CurrentCellPosition;
            occupantCell.z = 0;
            if (occupantCell != spawnCell)
                continue;
            if (OccupancyResolver.GetHeightBand(occupant) != HeightBand.Blocking)
                continue;

            bool enemy = PlayerSlotRelations.AreEnemies(occupant.SlotIndex, spawnSlot);
            reason = enemy
                ? "Predio ocupado por oponente, compra bloqueada."
                : "Predio ocupado por unidade aliada, compra bloqueada.";
            return true;
        }

        return false;
    }

    private HeightBand ResolveShoppingSpawnHeightBand(UnitData unit, Vector3Int spawnCell)
    {
        if (unit == null)
            return HeightBand.Blocking;

        Domain spawnDomain = unit.domain;
        HeightLevel spawnHeight = unit.heightLevel;
        Tilemap boardMap = terrainTilemap;
        if (boardMap != null
            && TryResolveForcedEndMovementTargetForCell(
                boardMap,
                terrainDatabase,
                spawnCell,
                spawnDomain,
                spawnHeight,
                out Domain forcedDomain,
                out HeightLevel forcedHeight,
                out _)
            && UnitDataSupportsLayerMode(unit, forcedDomain, forcedHeight))
        {
            spawnDomain = forcedDomain;
            spawnHeight = forcedHeight;
        }

        return OccupancyResolver.GetHeightBand(spawnDomain, spawnHeight);
    }

    private static bool UnitDataSupportsLayerMode(UnitData unit, Domain domain, HeightLevel height)
    {
        if (unit == null)
            return false;
        if (unit.domain == domain && unit.heightLevel == height)
            return true;
        if (unit.aditionalDomainsAllowed == null)
            return false;

        for (int i = 0; i < unit.aditionalDomainsAllowed.Count; i++)
        {
            UnitLayerMode mode = unit.aditionalDomainsAllowed[i];
            if (mode.domain == domain && mode.heightLevel == height)
                return true;
        }

        return false;
    }
    private bool TryResolveShoppingCursorMove(Vector3Int currentCell, Vector3Int inputDelta)
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices || shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = shoppingUnitsForSale.Count;
        if (count <= 0)
            return false;

        // Slot virtual CANCELAR logo apos o ultimo item: total de posicoes = count + 1.
        // Navegar para baixo passando do ultimo item chega no CANCELAR; para cima, volta.
        int total = count + 1;
        int current = shoppingCancelFocused ? count : ClampShoppingSelectedIndex();
        int next = (current + step + total) % total;
        if (next == current)
            return false;

        if (next == count)
        {
            shoppingCancelFocused = true;
        }
        else
        {
            shoppingCancelFocused = false;
            shoppingSelectedIndex = next;
        }
        cursorController?.PlayCursorMoveSfx();
        RefreshShoppingSelectionPresentation(logOptions: true);
        return true;
    }

    private void UpdateShoppingPreviewPersistence()
    {
        if (CurrentCursorState != CursorState.ShoppingAndServices)
            return;

        if (shoppingConstruction == null || shoppingUnitsForSale.Count == 0)
            return;

        // Se o painel estiver vazio (mensagem transiente expirou) mas ainda estamos no estado de shopping,
        // restaura a visualizacao do item selecionado.
        if (!PanelDialogController.HasActiveExternalText())
        {
            RefreshShoppingSelectionPresentation(logOptions: false);
        }
    }

    private void RefreshShoppingSelectionPresentation(bool logOptions)
    {
        int index = ClampShoppingSelectedIndex();
        if (index < 0 || index >= shoppingUnitsForSale.Count)
            return;

        UnitData focusedUnit = shoppingUnitsForSale[index];
        if (focusedUnit == null)
            return;

        string header = BuildShoppingDialogHeader(focusedUnit);
        string body = BuildShoppingDialogBody(focusedUnit);
        TeamId previewTeam = matchController != null && matchController.ActiveTeamId >= 0
            ? (TeamId)matchController.ActiveTeamId
            : (shoppingConstruction != null ? shoppingConstruction.TeamId : TeamId.Neutral);
        Sprite previewSprite = ResolveShoppingPreviewSprite(focusedUnit, previewTeam, out Color previewTint);
        PanelDialogController.TrySetShoppingPreview(header, body, previewSprite, previewTint);
        if (logOptions)
            LogConstructionShoppingPanel();
    }

    // Cabecalho: so identidade e stats, ao lado da imagem da unidade (mesma
    // "faixa" de altura dela). Armas/carga/descricao vao pro corpo, que ocupa a
    // largura total abaixo da imagem e rola quando nao cabe.
    private string BuildShoppingDialogHeader(UnitData unit)
    {
        if (unit == null)
            return string.Empty;

        int resolvedCost = matchController != null
            ? matchController.ResolveEconomyCost(unit.cost)
            : Mathf.Max(0, unit.cost);
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{ResolveUnitName(unit)} $ {resolvedCost.ToString("N0", CultureInfo.GetCultureInfo("pt-BR"))} | Classe: {ResolveGameUnitClassName(unit.unitClass)}");
        string airUpkeepSuffix = ResolveAirTurnUpkeepSuffix(unit);
        sb.AppendLine($"Movimento: {Mathf.Max(0, unit.movement)} | Autonomia: {Mathf.Max(0, unit.autonomia)}{airUpkeepSuffix} | Visao: {Mathf.Max(0, unit.visao)}");
        if (TryBuildVisionSpecializationsSummary(unit, out string visionSpecializationsSummary))
            sb.AppendLine($"    {visionSpecializationsSummary}");

        return sb.ToString().TrimEnd();
    }

    // Corpo: armas, carga e descricao, em largura total. Unidades sem armas nem
    // carga (civis desarmadas) mostram so a descricao — as secoes nao aparecem.
    private string BuildShoppingDialogBody(UnitData unit)
    {
        if (unit == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        TeamId team = matchController != null && matchController.ActiveTeamId >= 0
            ? (TeamId)matchController.ActiveTeamId
            : (shoppingConstruction != null ? shoppingConstruction.TeamId : TeamId.Neutral);
        if (matchController != null && !matchController.CanProduceUnit(team, unit, out string blockedReason))
        {
            sb.AppendLine($"BLOQUEADO - {blockedReason}");
            sb.AppendLine();
        }
        AppendShoppingPreviewWeaponLines(sb, unit);
        AppendShoppingPreviewSupplyLines(sb, unit);

        if (!string.IsNullOrWhiteSpace(unit.description))
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(unit.description.Trim());
        }

        return sb.ToString().TrimEnd();
    }


    private static string ResolveAirTurnUpkeepSuffix(UnitData unit)
    {
        if (unit == null || unit.autonomyData == null)
            return string.Empty;

        AutonomyData profile = unit.autonomyData;
        int upkeep = Mathf.Max(0, profile.turnStartUpkeep);
        if (upkeep <= 0)
            return string.Empty;

        bool hasAirUpkeep = false;
        if (profile.upkeepStartLayerModes != null)
        {
            for (int i = 0; i < profile.upkeepStartLayerModes.Count; i++)
            {
                AutonomyLayerMode mode = profile.upkeepStartLayerModes[i];
                if (mode.domain == Domain.Air)
                {
                    hasAirUpkeep = true;
                    break;
                }
            }
        }

        if (!hasAirUpkeep)
            return string.Empty;

        return $" (-{upkeep} por turno no ar)";
    }
    private static bool TryBuildVisionSpecializationsSummary(UnitData unit, out string summary)
    {
        summary = string.Empty;
        if (unit == null || unit.visionSpecializations == null || unit.visionSpecializations.Count <= 0)
            return false;

        List<string> segments = new List<string>();
        for (int i = 0; i < unit.visionSpecializations.Count; i++)
        {
            UnitVisionException entry = unit.visionSpecializations[i];
            if (entry == null)
                continue;

            string domainLabel = ResolveDomainName(entry.domain);
            string heightLabel = ResolveHeightName(entry.heightLevel);
            int visionValue = Mathf.Max(0, entry.vision);
            segments.Add($"{domainLabel} {heightLabel}: {visionValue}");
        }

        if (segments.Count <= 0)
            return false;

        summary = string.Join(", ", segments);
        return true;
    }

    private static string ResolveDomainName(Domain domain)
    {
        switch (domain)
        {
            case Domain.Land: return "Land";
            case Domain.Naval: return "Naval";
            case Domain.Submarine: return "Submarine";
            case Domain.Air: return "Air";
            default: return domain.ToString();
        }
    }

    private static string ResolveHeightName(HeightLevel heightLevel)
    {
        switch (heightLevel)
        {
            case HeightLevel.Submerged: return "Submerged";
            case HeightLevel.Surface: return "Surface";
            case HeightLevel.AirLow: return "Low";
            case HeightLevel.AirHigh: return "High";
            default: return heightLevel.ToString();
        }
    }

    private static void AppendShoppingPreviewWeaponLines(StringBuilder sb, UnitData unit)
    {
        if (sb == null || unit == null || unit.embarkedWeapons == null || unit.embarkedWeapons.Count <= 0)
            return;

        bool hasAny = false;
        for (int i = 0; i < unit.embarkedWeapons.Count; i++)
        {
            UnitEmbarkedWeapon embarked = unit.embarkedWeapons[i];
            if (embarked == null || embarked.weapon == null)
                continue;

            if (!hasAny)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.AppendLine("Armas");
                hasAny = true;
            }

            string weaponName = !string.IsNullOrWhiteSpace(embarked.weapon.displayName)
                ? embarked.weapon.displayName
                : (!string.IsNullOrWhiteSpace(embarked.weapon.id) ? embarked.weapon.id : embarked.weapon.name);
            int ammo = Mathf.Max(0, embarked.squadAmmunition);
            int rangeMin = Mathf.Max(0, embarked.GetRangeMin());
            int rangeMax = Mathf.Max(rangeMin, embarked.GetRangeMax());
            string rangeLabel = rangeMin == rangeMax
                ? rangeMin.ToString()
                : $"{rangeMin}-{rangeMax}";
            string weaponCategory = ResolveWeaponCategoryName(embarked.weapon.WeaponCategory);
            sb.AppendLine($"- {weaponName} | Ammo: {ammo} | Alcance: {rangeLabel} ({weaponCategory})");
        }
    }

    private static string ResolveWeaponCategoryName(WeaponCategory weaponCategory)
    {
        switch (weaponCategory)
        {
            case WeaponCategory.AntiInfantaria: return "anti infantaria";
            case WeaponCategory.AntiTanque: return "anti tanque";
            case WeaponCategory.AntiAerea: return "anti aerea";
            case WeaponCategory.AntiNavio: return "anti navio";
            default: return weaponCategory.ToString().ToLowerInvariant();
        }
    }

    private static void AppendShoppingPreviewSupplyLines(StringBuilder sb, UnitData unit)
    {
        if (sb == null || unit == null || unit.supplierResources == null || unit.supplierResources.Count <= 0)
            return;

        List<string> segments = new List<string>();
        for (int i = 0; i < unit.supplierResources.Count; i++)
        {
            UnitEmbarkedSupply entry = unit.supplierResources[i];
            if (entry == null || entry.supply == null)
                continue;

            string supplyName = !string.IsNullOrWhiteSpace(entry.supply.displayName)
                ? entry.supply.displayName
                : (!string.IsNullOrWhiteSpace(entry.supply.id) ? entry.supply.id : entry.supply.name);
            segments.Add($"{supplyName}: {Mathf.Max(0, entry.amount)}");
        }

        if (segments.Count <= 0)
            return;

        if (sb.Length > 0)
            sb.AppendLine();
        sb.AppendLine("Carga:");
        sb.AppendLine($"    {string.Join(" | ", segments)}");
    }

    private static string ResolveGameUnitClassName(GameUnitClass gameUnitClass)
    {
        return GameUnitClassLabels.GetPortugueseName(gameUnitClass);
    }

    private static Sprite ResolveShoppingPreviewSprite(UnitData unit, TeamId team, out Color tint)
    {
        tint = TeamUtils.GetColor(team);
        tint.a = 1f;
        if (unit == null)
            return null;

        // Excecao hardcoded: no shopping, submarino mostra sprite naval/surface quando existir.
        if (unit.unitClass == GameUnitClass.Submarine && unit.aditionalDomainsAllowed != null)
        {
            for (int i = 0; i < unit.aditionalDomainsAllowed.Count; i++)
            {
                UnitLayerMode mode = unit.aditionalDomainsAllowed[i];
                if (mode.domain != Domain.Naval || mode.heightLevel != HeightLevel.Surface)
                    continue;

                Sprite surfacedSprite = TeamUtils.GetTeamSprite(mode, team);
                if (surfacedSprite != null)
                {
                    return surfacedSprite;
                }

                break;
            }
        }

        return TeamUtils.GetTeamSprite(unit, team, preferTransportSprite: false);
    }

    private void LogConstructionShoppingPanel()
    {
        if (!enableTurnStateRuntimeLogs)
            return;
        if (CurrentCursorState != CursorState.ShoppingAndServices || shoppingConstruction == null)
            return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"[Shopping] {ResolveConstructionName(shoppingConstruction)} - escolha a opcao de compra:");

        for (int i = 0; i < shoppingUnitsForSale.Count; i++)
        {
            UnitData unit = shoppingUnitsForSale[i];
            if (unit == null)
                continue;

            string marker = i == ClampShoppingSelectedIndex() ? ">" : " ";
            sb.Append(marker);
            sb.Append(' ');
            sb.Append(i + 1);
            sb.Append(". ");
            sb.Append(ResolveUnitName(unit));
            sb.Append(" $");
            sb.Append(matchController != null ? matchController.ResolveEconomyCost(unit.cost) : Mathf.Max(0, unit.cost));
            sb.AppendLine();
        }

        sb.Append("Setas: muda foco | Enter: comprar focada | Atalhos: 1-9, 0=10, Shift+1=11... ESC cancela.");
        Debug.Log(sb.ToString());
    }

    private static bool TryReadShoppingPressedNumber(out int number)
    {
        number = 0;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            bool shift = (Keyboard.current.leftShiftKey != null && Keyboard.current.leftShiftKey.isPressed) ||
                         (Keyboard.current.rightShiftKey != null && Keyboard.current.rightShiftKey.isPressed);

            if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) { number = 10; return true; }
            if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) { number = shift ? 11 : 1; return true; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) { number = shift ? 12 : 2; return true; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) { number = shift ? 13 : 3; return true; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) { number = shift ? 14 : 4; return true; }
            if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) { number = shift ? 15 : 5; return true; }
            if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) { number = shift ? 16 : 6; return true; }
            if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad7Key.wasPressedThisFrame) { number = shift ? 17 : 7; return true; }
            if (Keyboard.current.digit8Key.wasPressedThisFrame || Keyboard.current.numpad8Key.wasPressedThisFrame) { number = shift ? 18 : 8; return true; }
            if (Keyboard.current.digit9Key.wasPressedThisFrame || Keyboard.current.numpad9Key.wasPressedThisFrame) { number = shift ? 19 : 9; return true; }
        }
#else
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) { number = 10; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { number = shift ? 11 : 1; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { number = shift ? 12 : 2; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { number = shift ? 13 : 3; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { number = shift ? 14 : 4; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) { number = shift ? 15 : 5; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) { number = shift ? 16 : 6; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) { number = shift ? 17 : 7; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) { number = shift ? 18 : 8; return true; }
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) { number = shift ? 19 : 9; return true; }
#endif

        return false;
    }

    private void RecordShoppingBuyReplayCommand(
        GameObject spawned,
        UnitData unit,
        TeamId buyingTeam,
        Vector3Int spawnCell,
        int economyBefore,
        int economyAfter,
        int selectedIndex)
    {
        if (replayManager == null || spawned == null || unit == null)
            return;

        UnitManager spawnedManager = spawned.GetComponent<UnitManager>();
        if (spawnedManager == null || spawnedManager.InstanceId <= 0)
            return;

        Vector3Int normalizedSpawnCell = spawnCell;
        normalizedSpawnCell.z = 0;

        replayManager.RecordStandaloneAction(new PlayerAction
        {
            ActionType = PlayerActionType.Shopping,
            TurnNumber = matchController != null ? matchController.CurrentTurn : 0,
            ActingTeam = buyingTeam,
            CursorHex = normalizedSpawnCell,
            UnitInstanceId = spawnedManager.InstanceId.ToString(),
            TargetHex = normalizedSpawnCell,
            SensorAction = SensorActionType.Shopping,
            ShoppingSelectedIndex = selectedIndex,
            ShoppingUnitTypeId = !string.IsNullOrWhiteSpace(unit.id) ? unit.id : unit.name,
            Confirmed = true,
            DebugLabel = $"Shopping: {ResolveUnitName(unit)} ({spawnedManager.InstanceId})"
        });

        JogadasManager.EnsureInstance()?.RegistrarCompra(
            matchController != null ? matchController.CurrentTurn : 0,
            (int)buyingTeam,
            normalizedSpawnCell.x, normalizedSpawnCell.y,
            unit.apelido,
            spawnedManager.InstanceId);
    }

    private static string ResolveUnitName(UnitData unit)
    {
        if (unit == null)
            return "<null>";
        if (!string.IsNullOrWhiteSpace(unit.displayName))
            return unit.displayName;
        if (!string.IsNullOrWhiteSpace(unit.id))
            return unit.id;
        return unit.name;
    }

}
