using System.Text;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public partial class TurnStateManager
{
    private bool TryEnterConstructionShoppingState(ConstructionManager construction, int activeTeam)
    {
        if (construction == null || activeTeam < 0)
            return false;

        TeamId buyerTeam = (TeamId)activeTeam;
        if (!construction.CanProduceUnitsForTeam(buyerTeam))
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
        SetCursorState(CursorState.ShoppingAndServices, "TryEnterConstructionShoppingState: ally construction with units for sale");
        RefreshShoppingSelectionPresentation(logOptions: false);
        LogConstructionShoppingPanel();
        return true;
    }

    private void ExitConstructionShoppingStateToNeutral(bool rollback)
    {
        shoppingConstruction = null;
        shoppingUnitsForSale.Clear();
        shoppingSelectedIndex = -1;
        PanelDialogController.ClearExternalText();
        SetCursorState(CursorState.Neutral, "ExitConstructionShoppingStateToNeutral", rollback: rollback);
    }

    private void ProcessConstructionShoppingInput()
    {
        if (cursorState != CursorState.ShoppingAndServices)
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
        RefreshShoppingSelectionPresentation(logOptions: false);
        TryPurchaseShoppingUnitByIndex(index);
    }

    private bool TryConfirmSelectedShoppingOption()
    {
        if (cursorState != CursorState.ShoppingAndServices)
            return false;
        if (shoppingConstruction == null || shoppingUnitsForSale.Count <= 0)
            return false;

        int index = ClampShoppingSelectedIndex();
        if (index < 0 || index >= shoppingUnitsForSale.Count)
            return false;

        return TryPurchaseShoppingUnitByIndex(index);
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

        int activeTeam = matchController != null ? matchController.ActiveTeamId : -1;
        TeamId spawnTeam = activeTeam >= 0 ? (TeamId)activeTeam : shoppingConstruction.TeamId;
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

        GameObject spawned = unitSpawner.SpawnAtCell(unit, spawnTeam, spawnCell);
        if (spawned == null)
        {
            cursorController?.PlayErrorSfx();
            Debug.LogWarning($"[Shopping] Falha ao comprar {ResolveUnitName(unit)}. Verifique ocupacao/camada da celula.");
            return false;
        }

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
        RecordShoppingBuyReplayCommand(spawned, unit, spawnTeam, spawnCell, economyBefore, economyAfter);

        if (matchController != null)
            PanelMoneyController.PushContextualUpdate(spawnTeam, remainingMoney, ResolveUnitName(unit), -unitCost);

        cursorController?.PlayDoneSfx();
        if (enableTurnStateRuntimeLogs)
            Debug.Log($"[Shopping] Compra concluida: {ResolveUnitName(unit)} por ${unitCost} em {ResolveConstructionName(shoppingConstruction)}.");
        ExitConstructionShoppingStateToNeutral(rollback: false);
        return true;
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

    private bool TryResolveShoppingCursorMove(Vector3Int currentCell, Vector3Int inputDelta)
    {
        if (cursorState != CursorState.ShoppingAndServices || shoppingUnitsForSale == null || shoppingUnitsForSale.Count <= 0)
            return false;

        int step = GetMirandoStepFromInput(inputDelta);
        if (step == 0)
            return false;

        int count = shoppingUnitsForSale.Count;
        if (count <= 1)
            return false;

        int currentIndex = ClampShoppingSelectedIndex();
        int nextIndex = (currentIndex + step + count) % count;
        if (nextIndex == currentIndex)
            return false;

        shoppingSelectedIndex = nextIndex;
        cursorController?.PlayCursorMoveSfx();
        RefreshShoppingSelectionPresentation(logOptions: true);
        return true;
    }

    private void RefreshShoppingSelectionPresentation(bool logOptions)
    {
        int index = ClampShoppingSelectedIndex();
        if (index < 0 || index >= shoppingUnitsForSale.Count)
            return;

        UnitData focusedUnit = shoppingUnitsForSale[index];
        if (focusedUnit == null)
            return;

        string preview = BuildShoppingDialogPreview(focusedUnit);
        TeamId previewTeam = matchController != null && matchController.ActiveTeamId >= 0
            ? (TeamId)matchController.ActiveTeamId
            : (shoppingConstruction != null ? shoppingConstruction.TeamId : TeamId.Neutral);
        Sprite previewSprite = ResolveShoppingPreviewSprite(focusedUnit, previewTeam, out Color previewTint);
        PanelDialogController.TrySetShoppingPreview(preview, previewSprite, previewTint);
        if (logOptions)
            LogConstructionShoppingPanel();
    }

    private string BuildShoppingDialogPreview(UnitData unit)
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

        AppendShoppingPreviewWeaponLines(sb, unit);
        AppendShoppingPreviewSupplyLines(sb, unit);

        if (!string.IsNullOrWhiteSpace(unit.description))
        {
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

        sb.AppendLine();
        sb.AppendLine("Carga:");
        sb.AppendLine($"    {string.Join(" | ", segments)}");
    }

    private static string ResolveGameUnitClassName(GameUnitClass gameUnitClass)
    {
        switch (gameUnitClass)
        {
            case GameUnitClass.Infantry: return "Infantaria";
            case GameUnitClass.Vehicle: return "Veiculo";
            case GameUnitClass.Artillery: return "Artilharia";
            case GameUnitClass.Armored: return "Blindado";
            case GameUnitClass.Jet: return "Jato";
            case GameUnitClass.Helicopter: return "Helicoptero";
            case GameUnitClass.Plane: return "Aviao";
            case GameUnitClass.Submarine: return "Submarino";
            case GameUnitClass.Ship: return "Navio";
            default: return gameUnitClass.ToString();
        }
    }

    private static Sprite ResolveShoppingPreviewSprite(UnitData unit, TeamId team, out Color tint)
    {
        tint = Color.white;
        if (unit == null)
            return null;

        Sprite teamSprite = null;
        switch (team)
        {
            case TeamId.Green:
                teamSprite = unit.spriteGreen;
                break;
            case TeamId.Red:
                teamSprite = unit.spriteRed;
                break;
            case TeamId.Blue:
                teamSprite = unit.spriteBlue;
                break;
            case TeamId.Yellow:
                teamSprite = unit.spriteYellow;
                break;
        }

        if (teamSprite != null)
            return teamSprite;

        tint = TeamUtils.GetColor(team);
        tint.a = 1f;
        return unit.spriteDefault;
    }

    private void LogConstructionShoppingPanel()
    {
        if (!enableTurnStateRuntimeLogs)
            return;
        if (cursorState != CursorState.ShoppingAndServices || shoppingConstruction == null)
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
        int economyAfter)
    {
        if (replayManager == null || spawned == null || unit == null)
            return;

        UnitManager spawnedManager = spawned.GetComponent<UnitManager>();
        if (spawnedManager == null || spawnedManager.InstanceId <= 0)
            return;

        Vector3Int normalizedSpawnCell = spawnCell;
        normalizedSpawnCell.z = 0;
        UnitLayerMode spawnLayer = spawnedManager.GetCurrentLayerMode();

        BuyUnitReplayCommand command = new BuyUnitReplayCommand
        {
            UnitInstanceId = spawnedManager.InstanceId.ToString(),
            UnitTypeId = unit.id,
            SpawnHex = normalizedSpawnCell,
            SpawnLayer = spawnLayer,
            BuyingTeam = buyingTeam,
            EconomyBefore = Mathf.Max(0, economyBefore),
            EconomyAfter = Mathf.Max(0, economyAfter),
            debugLabel = $"Buy: {TeamUtils.GetName(buyingTeam)} spawns {ResolveUnitName(unit)} (id:{spawnedManager.InstanceId}) at ({normalizedSpawnCell.x},{normalizedSpawnCell.y}) | ${Mathf.Max(0, economyBefore)} -> ${Mathf.Max(0, economyAfter)}"
        };

        replayManager.RecordCommand(command);
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


