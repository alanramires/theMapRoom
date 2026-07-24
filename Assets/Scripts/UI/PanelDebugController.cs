using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Lado PAINEL do debug: vive dentro do Panel_Debug e cuida do campo de comando,
// do botao de envio, do foco e da execucao dos comandos digitados.
// NAO decide flags nem atalhos globais — isso e do DebugManager, que vive fora do
// painel e sobrevive a ele ser desativado. A separacao existe porque o papel antes
// era inferido de campo serializado (sendButton/commandInputObject); cenas com os
// dois componentes preenchidos ficavam sem nenhuma autoridade de atalho.
public class PanelDebugController : MonoBehaviour
{
    private static PanelDebugController instance;

    [Header("References")]
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private MatchController matchController;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private Button sendButton;
    [Tooltip("Arraste o objeto do input (raiz ou filho).")]
    [SerializeField] private GameObject commandInputObject;

    private Component resolvedCommandInputField;
    private PropertyInfo cachedTextProperty;
    private InputField resolvedLegacyInputField;
    private TMP_InputField resolvedTmpInputField;
    private int lastSubmitFrame = -1;

    private void Awake()
    {
        TryAutoAssignReferences();
        instance = this;

        if (sendButton != null)
            sendButton.onClick.AddListener(HandleSendClicked);
        RegisterInputSubmitListeners();
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(HandleSendClicked);
        UnregisterInputSubmitListeners();
        UiInputBlocker.SetExplicitTextInputFocused(false);

        if (instance == this)
            instance = null;
    }

    private void OnDisable()
    {
        UiInputBlocker.SetExplicitTextInputFocused(false);
    }

    public static bool TryFocusCommandInput()
    {
        PanelDebugController panel = Resolve();
        if (panel == null)
            return false;

        panel.StartCoroutine(panel.FocusCommandInputNextFrame());
        return true;
    }

    public static bool TryReleaseCommandInput()
    {
        PanelDebugController panel = Resolve();
        if (panel == null)
            return false;

        panel.ReleaseCommandInputFocus();
        return true;
    }

    public static bool IsDebugCommandInputFocused()
    {
        PanelDebugController panel = Resolve();
        return panel != null && panel.IsCommandInputFocused();
    }

    public static bool TryConsumeDebugToggleCharacterFromInput()
    {
        PanelDebugController panel = Resolve();
        if (panel == null)
            return false;

        string value = panel.GetInputText();
        if (string.IsNullOrEmpty(value))
            return false;

        char last = value[value.Length - 1];
        if (last != '\'' && last != ';' && last != '`')
            return false;

        panel.SetInputText(value.Substring(0, value.Length - 1));
        return true;
    }

    // PanelVisibilityHotkeysController tem execucao antecipada e desativa o
    // Panel_Debug no Awake dele. Enquanto o painel nunca foi aberto, o Awake deste
    // componente ainda nao rodou e o estatico fica nulo — por isso a busca precisa
    // incluir objetos inativos para o painel abrir na primeira vez.
    private static PanelDebugController Resolve()
    {
        if (instance != null)
            return instance;

        PanelDebugController[] found = Resources.FindObjectsOfTypeAll<PanelDebugController>();
        for (int i = 0; i < found.Length; i++)
        {
            PanelDebugController candidate = found[i];
            if (candidate == null || candidate.gameObject == null)
                continue;
            if (!candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                continue;

            instance = candidate;
            return candidate;
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
    }
#endif

    private void Update()
    {
        bool commandInputFocused = IsCommandInputFocused();
        if (!commandInputFocused)
        {
            // Mantem foco "grudado" no input enquanto o panel_debug estiver aberto.
            FocusCommandInputNow();
            commandInputFocused = IsCommandInputFocused();
        }
        UiInputBlocker.SetExplicitTextInputFocused(commandInputFocused);

        if (!commandInputFocused)
            return;

        // Enquanto estiver digitando no panel_debug, bloqueia qualquer atalho de gameplay.
        UiInputBlocker.SuppressGameplayInputForFrames(1);

        if (IsEnterPressedThisFrame())
            TrySubmitCommandInputOncePerFrame();
    }

    private void TryAutoAssignReferences()
    {
        if (turnStateManager == null)
            turnStateManager = FindAnyObjectByType<TurnStateManager>();

        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (sendButton == null)
            sendButton = GetComponentInChildren<Button>();

        resolvedCommandInputField = ResolveCommandInputComponentFromGameObject(commandInputObject);
        if (resolvedCommandInputField == null)
        {
            InputField fallback = GetComponentInChildren<InputField>();
            if (fallback != null)
                resolvedCommandInputField = fallback;
            else
                resolvedCommandInputField = FindAnyInputLikeComponentInChildren();
        }

        resolvedLegacyInputField = resolvedCommandInputField as InputField;
        resolvedTmpInputField = resolvedCommandInputField as TMP_InputField;
    }

    private void HandleSendClicked()
    {
        string rawCommand = GetInputText();
        if (string.IsNullOrWhiteSpace(rawCommand))
            return;

        // Evita que o mesmo Enter usado para enviar o comando seja processado
        // como confirmacao de gameplay (ex.: finalizar acao/turno) no mesmo frame.
        UiInputBlocker.SuppressGameplayInputForFrames(2);

        if (turnStateManager == null)
            return;

        string command = NormalizeCommand(rawCommand);
        bool executed = false;

        if (command == "DESTROY UNIT" || command == "REMOVE UNIT")
        {
            executed = turnStateManager.TryDestroyUnitUnderCursorFromDebug(out string message);
            if (!executed && !string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "WAKE UNIT")
        {
            executed = turnStateManager.TryWakeUnitUnderCursorFromDebug(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "WAKE ALL UNITS")
        {
            executed = turnStateManager.TryWakeAllUnitsForActiveTeamFromDebug(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "SET POSITION")
        {
            executed = turnStateManager.TrySyncDebugSelectedUnitPositionFromTransform(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "REFRESH CACHE" || command == "REFRESH CACHES" || command == "RESET CACHE")
        {
            executed = turnStateManager.TryRefreshRuntimeCachesFromDebug(out string message);
            if (executed)
            {
                cursorController?.PlayDoneSfx();
                PanelDialogController.TrySetTransientText("DEBUG: caches atualizados", 2.2f);
            }
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "STATE HASH")
        {
            SaveGameManager saveManager = FindAnyObjectByType<SaveGameManager>();
            if (saveManager != null)
            {
                string stateHash = saveManager.ComputeCurrentStateHash();
                Debug.Log($"[Debug Command] state_hash={stateHash}");
                PanelDialogController.TrySetTransientText($"HASH: {stateHash.Substring(0, Mathf.Min(16, stateHash.Length))}…", 4f);
                cursorController?.PlayDoneSfx();
                executed = true;
            }
            else
            {
                Debug.Log("[Debug Command] STATE HASH: SaveGameManager nao encontrado na cena.");
            }
        }
        else if (command == "STATE DUMP")
        {
            SaveGameManager saveManager = FindAnyObjectByType<SaveGameManager>();
            if (saveManager != null && saveManager.TryDumpCanonicalStateToFile(out string dumpPath))
            {
                Debug.Log($"[Debug Command] state dump: {dumpPath}");
                PanelDialogController.TrySetTransientText("DEBUG: state dump gravado (ver console)", 2.6f);
                cursorController?.PlayDoneSfx();
                executed = true;
            }
            else
            {
                Debug.Log("[Debug Command] STATE DUMP: falha ao gravar (SaveGameManager ausente ou erro de IO).");
            }
        }
        else if (command == "LAND UNIT")
        {
            executed = turnStateManager.TryExecuteLayerCommandFromDebug(DebugLayerCommand.Landing, out string message);
            if (!executed && !string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetActiveTeamCommand(command, out int activeTeamValue))
        {
            executed = turnStateManager.TrySetActiveTeamFromDebug(activeTeamValue, out string message);
            if (executed)
            {
                cursorController?.PlayDoneSfx();
                PanelDialogController.TrySetTransientText($"DEBUG: Active Team forced to {activeTeamValue}", 2.6f);
            }
            if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "HELP")
        {
            string helpText = BuildDebugHelpSummary();
            ShowDynamicHelpPanel(helpText);

            Debug.Log($"[Debug Command] HELP\n{helpText}");
            cursorController?.PlayBeepSfx();
            executed = true;
        }
        else if (TryParseSetHpCommand(command, out int hpValue))
        {
            executed = turnStateManager.TrySetUnitHpUnderCursorFromDebug(hpValue, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetAutonomyCommand(command, out int autonomyValue))
        {
            executed = turnStateManager.TrySetUnitAutonomyUnderCursorFromDebug(autonomyValue, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetEmbarkedSupplyCommand(command, out string supplyToken, out int supplyAmount))
        {
            executed = turnStateManager.TrySetUnitEmbarkedSupplyUnderCursorFromDebug(supplyToken, supplyAmount, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetMoveRemainCommand(command, out int remainingMovementValue))
        {
            executed = turnStateManager.TrySetUnitRemainingMovementUnderCursorFromDebug(remainingMovementValue, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "REFUEL UNIT")
        {
            executed = turnStateManager.TryRefuelUnitAutonomyUnderCursorFromDebug(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetAmmoCommand(command, out int ammoWeaponIndex, out int ammoValue))
        {
            executed = turnStateManager.TrySetUnitEmbarkedAmmoUnderCursorFromDebug(ammoWeaponIndex, ammoValue, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetConstructionTeamCommand(command, out int constructionTeam))
        {
            executed = turnStateManager.TrySetConstructionTeamUnderCursorFromDebug(constructionTeam, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetOwnerCommand(command, out int ownerTeam))
        {
            executed = turnStateManager.TrySetConstructionTeamUnderCursorFromDebug(ownerTeam, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetCapturePointsCommand(command, out int capturePoints))
        {
            executed = turnStateManager.TrySetConstructionCapturePointsUnderCursorFromDebug(capturePoints, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetSellingRulesCommand(command, out string sellingRuleToken, out int sellingOwnerSlot))
        {
            executed = turnStateManager.TrySetSellingRulesUnderCursorFromDebug(sellingRuleToken, sellingOwnerSlot, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "REARM UNIT")
        {
            executed = turnStateManager.TryReplenishUnitEmbarkedAmmoUnderCursorFromDebug(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (command == "REPAIR UNIT")
        {
            executed = turnStateManager.TryRepairUnitUnderCursorFromDebug(out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSpawnCommand(rawCommand, out int? teamOverride, out string unitToken))
        {
            executed = turnStateManager.TrySpawnUnitUnderCursorFromDebug(unitToken, teamOverride, out string message);
            if (executed)
                cursorController?.PlayLoadSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseAdjustMoneyCommand(rawCommand, out int? moneyTeamOverride, out int moneyDelta))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else if (moneyTeamOverride.HasValue)
            {
                PlayerSlotId slot = PlayerSlotId.FromIndex(Mathf.Clamp(moneyTeamOverride.Value, 0, 3));
                int currentMoney = matchController.GetActualMoney(slot);
                int adjustedMoney = ClampMoneyDelta(currentMoney, moneyDelta);
                executed = matchController.TrySetActualMoney(slot, adjustedMoney);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do slot {slot.Value} ajustado em {moneyDelta:+#;-#;0}: ${currentMoney} -> ${adjustedMoney}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Slot {slot.Value} nao encontrado na lista de players.");
                }
            }
            else
            {
                PlayerSlotId resolvedSlot = matchController.ActiveSlotId;
                int currentMoney = matchController.GetActualMoney(resolvedSlot);
                int adjustedMoney = ClampMoneyDelta(currentMoney, moneyDelta);
                executed = matchController.TrySetActualMoney(resolvedSlot, adjustedMoney);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do slot ativo ({resolvedSlot.Value}) ajustado em {moneyDelta:+#;-#;0}: ${currentMoney} -> ${adjustedMoney}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Slot ativo ({resolvedSlot.Value}) nao encontrado na lista de players.");
                }
            }
        }
        else if (TryParseSetMoneyCommand(rawCommand, out moneyTeamOverride, out int moneyValue))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else if (moneyTeamOverride.HasValue)
            {
                PlayerSlotId slot = PlayerSlotId.FromIndex(Mathf.Clamp(moneyTeamOverride.Value, 0, 3));
                executed = matchController.TrySetActualMoney(slot, moneyValue);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do slot {slot.Value} atualizado para ${Mathf.Max(0, moneyValue)}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Slot {slot.Value} nao encontrado na lista de players.");
                }
            }
            else
            {
                PlayerSlotId resolvedSlot = matchController.ActiveSlotId;
                executed = matchController.TrySetActualMoney(resolvedSlot, moneyValue);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do slot ativo ({resolvedSlot.Value}) atualizado para ${Mathf.Max(0, moneyValue)}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Slot ativo ({resolvedSlot.Value}) nao encontrado na lista de players.");
                }
            }
        }
        else if (TryParseSetEconomyCommand(rawCommand, out bool economyEnabled))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else
            {
                matchController.SetEconomyEnabled(economyEnabled);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log($"[Debug Command] Economy {(economyEnabled ? "ON" : "OFF")}.");
            }
        }
        else if (TryParseDebugLayerCommand(rawCommand, out DebugLayerCommand layerCommand))
        {
            executed = turnStateManager.TryExecuteLayerCommandFromDebug(layerCommand, out string message);
            if (!executed && !string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
        }
        else if (TryParseSetFogAlphaCommand(rawCommand, out int fogAlphaPercent))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else
            {
                matchController.SetFogOfWarAlphaPercent(fogAlphaPercent);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log($"[Debug Command] FoW alpha = {fogAlphaPercent}%.");
            }
        }
        else if (command == "FOW PARTIAL" || command == "FOG OF WAR PARTIAL" ||
                 command == "SET FOG PARTIAL" || command == "SET FOW PARTIAL")
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else
            {
                matchController.SetFogOfWarDebugPartial();
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log("[Debug Command] FoW PARTIAL.");
            }
        }
        else if (TryParseSetFoWCommand(rawCommand, out bool fogEnabled))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else
            {
                matchController.SetFogOfWarDebugEnabled(fogEnabled);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log($"[Debug Command] FoW {(fogEnabled ? "ON" : "OFF")}.");
            }
        }
        else if (command == "AI PAUSE" || command == "PAUSE AI")
        {
            AIController aiController = FindAnyObjectByType<AIController>();
            if (aiController == null)
            {
                Debug.Log("[Debug Command] AIController nao encontrado.");
            }
            else
            {
                aiController.SetDebugPaused(true);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log("[Debug Command] AI PAUSE.");
            }
        }
        else if (command == "AI RESUME" || command == "RESUME AI")
        {
            AIController aiController = FindAnyObjectByType<AIController>();
            if (aiController == null)
            {
                Debug.Log("[Debug Command] AIController nao encontrado.");
            }
            else
            {
                aiController.SetDebugPaused(false);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log("[Debug Command] AI RESUME.");
            }
        }
        else if (command == "AI SHOPPING PAUSE" || command == "PAUSE AI SHOPPING")
        {
            AIController aiController = FindAnyObjectByType<AIController>();
            if (aiController == null)
            {
                Debug.Log("[Debug Command] AIController nao encontrado.");
            }
            else
            {
                aiController.SetDebugShoppingPaused(true);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log("[Debug Command] AI SHOPPING PAUSE.");
            }
        }
        else if (command == "AI SHOPPING RESUME" || command == "RESUME AI SHOPPING")
        {
            AIController aiController = FindAnyObjectByType<AIController>();
            if (aiController == null)
            {
                Debug.Log("[Debug Command] AIController nao encontrado.");
            }
            else
            {
                aiController.SetDebugShoppingPaused(false);
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log("[Debug Command] AI SHOPPING RESUME.");
            }
        }
        else if (TryParseAIStageCommand(command, out int aiStage))
        {
            AIController aiController = FindAnyObjectByType<AIController>();
            if (aiController == null)
            {
                Debug.Log("[Debug Command] AIController nao encontrado.");
            }
            else if (aiController.TryStartDebugStage(aiStage, DebugManager.ShouldResetPlanOnDebugStage()))
            {
                executed = true;
                cursorController?.PlayDoneSfx();
                Debug.Log($"[Debug Command] AI STAGE {aiStage}.");
            }
        }
        else
        {
            Debug.Log($"[Debug Command] Comando desconhecido: \"{rawCommand}\"");
        }

        if (executed)
            SetInputText(string.Empty);
    }

    private void RegisterInputSubmitListeners()
    {
        if (resolvedLegacyInputField != null)
            resolvedLegacyInputField.onEndEdit.AddListener(HandleLegacyInputEndEdit);

        if (resolvedTmpInputField != null)
        {
            resolvedTmpInputField.onSubmit.AddListener(HandleTmpInputSubmit);
            resolvedTmpInputField.onEndEdit.AddListener(HandleTmpInputEndEdit);
        }
    }

    private void UnregisterInputSubmitListeners()
    {
        if (resolvedLegacyInputField != null)
            resolvedLegacyInputField.onEndEdit.RemoveListener(HandleLegacyInputEndEdit);

        if (resolvedTmpInputField != null)
        {
            resolvedTmpInputField.onSubmit.RemoveListener(HandleTmpInputSubmit);
            resolvedTmpInputField.onEndEdit.RemoveListener(HandleTmpInputEndEdit);
        }
    }

    private void HandleLegacyInputEndEdit(string _)
    {
        // Inclui ESC/Cancel do input para nao vazar para atalhos de gameplay.
        UiInputBlocker.SuppressGameplayInputForFrames(2);

        if (!IsEnterPressedThisFrame())
            return;

        TrySubmitCommandInputOncePerFrame();
    }

    private void HandleTmpInputSubmit(string _)
    {
        TrySubmitCommandInputOncePerFrame();
    }

    private void HandleTmpInputEndEdit(string _)
    {
        // Inclui ESC/Cancel do input para nao vazar para atalhos de gameplay.
        UiInputBlocker.SuppressGameplayInputForFrames(2);

        if (!IsEnterPressedThisFrame())
            return;

        TrySubmitCommandInputOncePerFrame();
    }

    private void TrySubmitCommandInputOncePerFrame()
    {
        if (lastSubmitFrame == Time.frameCount)
            return;

        lastSubmitFrame = Time.frameCount;
        HandleSendClicked();
    }

    private System.Collections.IEnumerator FocusCommandInputNextFrame()
    {
        yield return null;
        FocusCommandInputNow();
    }

    private void FocusCommandInputNow()
    {
        TryAutoAssignReferences();
        if (resolvedCommandInputField == null)
            return;

        if (resolvedLegacyInputField != null)
        {
            EventSystem.current?.SetSelectedGameObject(resolvedLegacyInputField.gameObject);
            resolvedLegacyInputField.Select();
            resolvedLegacyInputField.ActivateInputField();
            return;
        }

        if (resolvedTmpInputField != null)
        {
            EventSystem.current?.SetSelectedGameObject(resolvedTmpInputField.gameObject);
            resolvedTmpInputField.Select();
            resolvedTmpInputField.ActivateInputField();
            return;
        }

        GameObject target = resolvedCommandInputField.gameObject;
        EventSystem.current?.SetSelectedGameObject(target);
    }

    private void ReleaseCommandInputFocus()
    {
        if (resolvedLegacyInputField != null)
            resolvedLegacyInputField.DeactivateInputField();
        if (resolvedTmpInputField != null)
            resolvedTmpInputField.DeactivateInputField();

        EventSystem.current?.SetSelectedGameObject(null);
        UiInputBlocker.SetExplicitTextInputFocused(false);
    }

    private static bool IsEnterPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
            return false;

        return Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
    }

    private static string NormalizeCommand(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] pieces = value.Trim().Split(' ');
        System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i];
            if (string.IsNullOrWhiteSpace(piece))
                continue;

            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append(piece.Trim().ToUpperInvariant());
        }

        return sb.ToString();
    }

    private static bool TryParseSetHpCommand(string normalizedCommand, out int hpValue)
    {
        hpValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;
        if (!normalizedCommand.StartsWith("SET HP "))
            return false;

        string valueToken = normalizedCommand.Substring("SET HP ".Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out hpValue);
    }

    private static bool TryParseSetAutonomyCommand(string normalizedCommand, out int autonomyValue)
    {
        autonomyValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefixA = "SET AUTONOMY ";
        const string prefixB = "SET AUTONOMI ";
        const string prefixC = "SET FUEL ";
        string valueToken = string.Empty;
        if (normalizedCommand.StartsWith(prefixA))
            valueToken = normalizedCommand.Substring(prefixA.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixB))
            valueToken = normalizedCommand.Substring(prefixB.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixC))
            valueToken = normalizedCommand.Substring(prefixC.Length).Trim();
        else
            return false;

        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out autonomyValue);
    }

    private static bool TryParseSetEmbarkedSupplyCommand(string normalizedCommand, out string supplyToken, out int amountValue)
    {
        supplyToken = string.Empty;
        amountValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        if (TryParseSetEmbarkedSupplyCommandForPrefix(normalizedCommand, "SET GALAO ", out amountValue) ||
            TryParseSetEmbarkedSupplyCommandForPrefix(normalizedCommand, "SET GALOES ", out amountValue))
        {
            supplyToken = "gasolina";
            return true;
        }

        if (TryParseSetEmbarkedSupplyCommandForPrefix(normalizedCommand, "SET CAIXAS ", out amountValue))
        {
            supplyToken = "caixaMunicao";
            return true;
        }

        if (TryParseSetEmbarkedSupplyCommandForPrefix(normalizedCommand, "SET PECAS ", out amountValue))
        {
            supplyToken = "pecas";
            return true;
        }

        return false;
    }

    private static bool TryParseSetEmbarkedSupplyCommandForPrefix(
        string normalizedCommand,
        string prefix,
        out int amountValue)
    {
        amountValue = 0;
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out amountValue);
    }

    private static bool TryParseSetAmmoCommand(string normalizedCommand, out int weaponIndex, out int ammoValue)
    {
        weaponIndex = 0;
        ammoValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string indexedPrefix = "SET AMMO:";
        if (normalizedCommand.StartsWith(indexedPrefix))
        {
            string remainder = normalizedCommand.Substring(indexedPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(remainder))
                return false;

            int split = remainder.IndexOf(' ');
            if (split <= 0)
                return false;

            string weaponToken = remainder.Substring(0, split).Trim();
            string valueToken = remainder.Substring(split + 1).Trim();
            if (!int.TryParse(weaponToken, out weaponIndex))
                return false;
            if (!int.TryParse(valueToken, out ammoValue))
                return false;
            return weaponIndex > 0;
        }

        const string defaultPrefix = "SET AMMO ";
        if (!normalizedCommand.StartsWith(defaultPrefix))
            return false;

        string defaultValueToken = normalizedCommand.Substring(defaultPrefix.Length).Trim();
        if (!int.TryParse(defaultValueToken, out ammoValue))
            return false;

        weaponIndex = 1; // Sem indice explicito, assume arma #1.
        return true;
    }

    private static bool TryParseSetMoveRemainCommand(string normalizedCommand, out int remainingMovementValue)
    {
        remainingMovementValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefixA = "SET MOVE_REMAIN ";
        const string prefixB = "SET MOVE REMAIN ";
        const string prefixC = "SET MOVE ";
        string valueToken;
        if (normalizedCommand.StartsWith(prefixA))
            valueToken = normalizedCommand.Substring(prefixA.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixB))
            valueToken = normalizedCommand.Substring(prefixB.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixC))
            valueToken = normalizedCommand.Substring(prefixC.Length).Trim();
        else
            return false;

        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out remainingMovementValue);
    }

    // "SET SELLING RULES <free|original|first|disabled> [slot]" — muda a regra de venda do prédio sob
    // o cursor. free/disabled ignoram o slot; original/first usam o slot como owner slot.
    private static bool TryParseSetSellingRulesCommand(string normalizedCommand, out string ruleToken, out int ownerSlot)
    {
        ruleToken = string.Empty;
        ownerSlot = -1;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "SET SELLING RULES ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string remainder = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        string[] parts = remainder.Split(' ');
        ruleToken = parts[0]; // já em maiúsculas (NormalizeCommand): FREE/ORIGINAL/FIRST/DISABLED
        if (parts.Length >= 2 && int.TryParse(parts[1], out int slot))
            ownerSlot = slot;
        return true;
    }

    private static bool TryParseSetConstructionTeamCommand(string normalizedCommand, out int teamValue)
    {
        teamValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "SET CONSTRUCTION TEAM ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out teamValue);
    }

    private static bool TryParseSetOwnerCommand(string normalizedCommand, out int teamValue)
    {
        teamValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "SET OWNER ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out teamValue);
    }

    private static bool TryParseSetActiveTeamCommand(string normalizedCommand, out int teamValue)
    {
        teamValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "SET ACTIVE TEAM ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out teamValue);
    }

    private static string BuildDebugHelpSummary()
    {
        return
            "wake unit - acorda unidade no cursor\n" +
            "wake all units - acorda todas unidades do time ativo\n" +
            "destroy unit | remove unit - destroi unidade no cursor\n" +
            "set position - sincroniza a unidade selecionada no Scene/Hierarchy com o hex do Transform\n" +
            "refresh cache | refresh caches | reset cache - invalida caches, republica o FoW (unidades novas entram em alvos/visao; exige estado Neutral) e recalcula os sensores\n" +
            "state hash - hash canonico do estado atual (salvar loga o hash; comparar apos load valida o round-trip do save)\n" +
            "state dump - grava o JSON canonico do estado num arquivo (diffar dump pre-save vs pos-load acha o campo divergente)\n" +
            "set hp <v>\n" +
            "set autonomy <v>\n" +
            "set fuel <v> (alias de set autonomy)\n" +
            "set move <v> (temporario, reseta ao virar rodada)\n" +
            "set move_remain <v> (alias de set move)\n" +
            "set ammo <v> | set ammo:<idx> <v>\n" +
            "set galao <v> | set galoes <v> | set caixas <v> | set pecas <v> (unidade ou construcao no cursor)\n" +
            "refuel unit | rearm unit | repair unit\n" +
            "set construction team <x>\n" +
            "set owner <x> (alias, -1 neutro, 0 verde, 1 azul, 2 vermelho, 3 amarelo)\n" +
            "set active team <x> (troca time ativo sem avancar turno)\n" +
            "set capture points <v>\n" +
            "set selling rules <free|original [slot]|first [slot]|disabled> (prédio no cursor)\n" +
            "spawn <unit> | ai spawn <unit>\n" +
            "spawn:<team> <unit>\n" +
            "set money <v> | set money +<v> | set money:<team> <v>\n" +
            "set economy on|off\n" +
            "altitude high|low (aeronave no ar)\n" +
            "change altitude high|low (legado)\n" +
            "land unit\n" +
            "landing | emerge | submerge | take off | fast take off\n" +
            "fow on|off|partial | set fow <0-100>\n" +
            "ai pause | pause ai\n" +
            "ai resume | resume ai\n" +
            "ai shopping pause | pause ai shopping\n" +
            "ai shopping resume | resume ai shopping\n" +
            "ai stage <1-3> (reinicia a IA no bloco escolhido)\n" +
            "help";
    }

    private static bool TryParseAIStageCommand(string normalizedCommand, out int stage)
    {
        stage = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "AI STAGE ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (!int.TryParse(valueToken, out stage))
            return true;

        if (stage < 1 || stage > 3)
            return true;

        return true;
    }

    private static bool TryParseSetCapturePointsCommand(string normalizedCommand, out int capturePointsValue)
    {
        capturePointsValue = 0;
        if (string.IsNullOrWhiteSpace(normalizedCommand))
            return false;

        const string prefix = "SET CAPTURE POINTS ";
        if (!normalizedCommand.StartsWith(prefix))
            return false;

        string valueToken = normalizedCommand.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out capturePointsValue);
    }

    private static bool TryParseSpawnCommand(string rawCommand, out int? teamOverride, out string unitToken)
    {
        teamOverride = null;
        unitToken = string.Empty;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        if (trimmed.StartsWith("spawn:", System.StringComparison.OrdinalIgnoreCase))
        {
            string remainder = trimmed.Substring("spawn:".Length).Trim();
            if (string.IsNullOrWhiteSpace(remainder))
                return false;

            int firstSpace = remainder.IndexOf(' ');
            if (firstSpace <= 0)
                return false;

            string teamToken = remainder.Substring(0, firstSpace).Trim();
            if (!int.TryParse(teamToken, out int parsedTeam))
                return false;
            if (parsedTeam < 0 || parsedTeam > 3)
                return false;

            teamOverride = parsedTeam;
            unitToken = remainder.Substring(firstSpace + 1).Trim();
            return !string.IsNullOrWhiteSpace(unitToken);
        }

        const string prefix = "spawn ";
        const string aiPrefix = "ai spawn ";
        if (trimmed.StartsWith(aiPrefix, System.StringComparison.OrdinalIgnoreCase))
        {
            unitToken = trimmed.Substring(aiPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(unitToken);
        }

        if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        unitToken = trimmed.Substring(prefix.Length).Trim();
        return !string.IsNullOrWhiteSpace(unitToken);
    }

    private static bool TryParseSetMoneyCommand(string rawCommand, out int? teamOverride, out int moneyValue)
    {
        teamOverride = null;
        moneyValue = 0;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        const string prefixWithTeam = "set money:";
        if (trimmed.StartsWith(prefixWithTeam, System.StringComparison.OrdinalIgnoreCase))
        {
            string remainder = trimmed.Substring(prefixWithTeam.Length).Trim();
            int firstSpace = remainder.IndexOf(' ');
            if (firstSpace <= 0)
                return false;

            string teamToken = remainder.Substring(0, firstSpace).Trim();
            string valueToken = remainder.Substring(firstSpace + 1).Trim();
            if (!int.TryParse(teamToken, out int parsedTeam))
                return false;
            if (parsedTeam < 0 || parsedTeam > 3)
                return false;
            if (!int.TryParse(valueToken, out moneyValue))
                return false;

            teamOverride = parsedTeam;
            return true;
        }

        const string prefixNoTeam = "set money ";
        if (!trimmed.StartsWith(prefixNoTeam, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string valueOnly = trimmed.Substring(prefixNoTeam.Length).Trim();
        return int.TryParse(valueOnly, out moneyValue);
    }

    private static bool TryParseAdjustMoneyCommand(string rawCommand, out int? teamOverride, out int moneyDelta)
    {
        teamOverride = null;
        moneyDelta = 0;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        const string prefixWithTeam = "set money:";
        if (trimmed.StartsWith(prefixWithTeam, System.StringComparison.OrdinalIgnoreCase))
        {
            string remainder = trimmed.Substring(prefixWithTeam.Length).Trim();
            int firstSpace = remainder.IndexOf(' ');
            if (firstSpace <= 0)
                return false;

            string teamToken = remainder.Substring(0, firstSpace).Trim();
            string valueToken = remainder.Substring(firstSpace + 1).Trim();
            if (!IsSignedDeltaToken(valueToken))
                return false;
            if (!int.TryParse(teamToken, out int parsedTeam))
                return false;
            if (parsedTeam < 0 || parsedTeam > 3)
                return false;
            if (!int.TryParse(valueToken, out moneyDelta))
                return false;

            teamOverride = parsedTeam;
            return true;
        }

        const string prefixNoTeam = "set money ";
        if (!trimmed.StartsWith(prefixNoTeam, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string valueOnly = trimmed.Substring(prefixNoTeam.Length).Trim();
        return IsSignedDeltaToken(valueOnly) && int.TryParse(valueOnly, out moneyDelta);
    }

    private static bool IsSignedDeltaToken(string valueToken)
    {
        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        char first = valueToken.TrimStart()[0];
        return first == '+' || first == '-';
    }

    private static int ClampMoneyDelta(int currentMoney, int moneyDelta)
    {
        long adjusted = (long)Mathf.Max(0, currentMoney) + moneyDelta;
        if (adjusted <= 0)
            return 0;
        if (adjusted >= int.MaxValue)
            return int.MaxValue;
        return (int)adjusted;
    }

    private static bool TryParseSetEconomyCommand(string rawCommand, out bool economyEnabled)
    {
        economyEnabled = true;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        const string prefix = "set economy ";
        if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string token = trimmed.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.Equals(token, "on", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "true", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "1", System.StringComparison.OrdinalIgnoreCase))
        {
            economyEnabled = true;
            return true;
        }

        if (string.Equals(token, "off", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "false", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "0", System.StringComparison.OrdinalIgnoreCase))
        {
            economyEnabled = false;
            return true;
        }

        return false;
    }

    private static bool TryParseChangeAltitudeCommand(string rawCommand, out Domain targetDomain, out HeightLevel targetHeight)
    {
        targetDomain = Domain.Land;
        targetHeight = HeightLevel.Surface;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        if (TryParseAltitudeAliasCommand(trimmed, out targetDomain, out targetHeight))
            return true;

        const string prefix = "change altitude ";
        if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string token = trimmed.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (TryParseDomainHeightPair(token, out targetDomain, out targetHeight))
            return true;

        // Compatibilidade com comandos antigos.
        if (string.Equals(token, "high", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("air/high", out targetDomain, out targetHeight);
        if (string.Equals(token, "low", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("air/low", out targetDomain, out targetHeight);
        if (string.Equals(token, "surface", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("land/surface", out targetDomain, out targetHeight);
        if (string.Equals(token, "sub", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("submarine/submerged", out targetDomain, out targetHeight);

        return TryParseAltitudeAliasCommand(token, out targetDomain, out targetHeight);
    }

    private static bool TryParseDebugLayerCommand(string rawCommand, out DebugLayerCommand command)
    {
        command = DebugLayerCommand.Landing;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string normalized = rawCommand.Trim();
        if (string.Equals(normalized, "landing", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "land unit", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.Landing;
            return true;
        }
        if (string.Equals(normalized, "take off", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.Takeoff;
            return true;
        }
        if (string.Equals(normalized, "altitude low", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.AltitudeLow;
            return true;
        }
        if (string.Equals(normalized, "altitude high", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "fast take off", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.AltitudeHigh;
            return true;
        }
        if (string.Equals(normalized, "emerge", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.Emerge;
            return true;
        }
        if (string.Equals(normalized, "submerge", System.StringComparison.OrdinalIgnoreCase))
        {
            command = DebugLayerCommand.Submerge;
            return true;
        }

        if (!TryParseChangeAltitudeCommand(rawCommand, out Domain domain, out HeightLevel height))
            return false;

        if (domain != Domain.Air || (height != HeightLevel.AirLow && height != HeightLevel.AirHigh))
            return false;

        command = height == HeightLevel.AirHigh
            ? DebugLayerCommand.AltitudeHigh
            : DebugLayerCommand.AltitudeLow;
        return true;
    }

    private static bool TryParseAltitudeAliasCommand(string token, out Domain targetDomain, out HeightLevel targetHeight)
    {
        targetDomain = Domain.Land;
        targetHeight = HeightLevel.Surface;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string normalized = token.Trim();
        if (string.Equals(normalized, "landing", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("land/surface", out targetDomain, out targetHeight);
        if (string.Equals(normalized, "emerge", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("naval/surface", out targetDomain, out targetHeight);
        if (string.Equals(normalized, "submerge", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("submarine/submerged", out targetDomain, out targetHeight);
        if (string.Equals(normalized, "take off", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("air/low", out targetDomain, out targetHeight);
        if (string.Equals(normalized, "fast take off", System.StringComparison.OrdinalIgnoreCase))
            return TryParseDomainHeightPair("air/high", out targetDomain, out targetHeight);

        return false;
    }

    private static bool TryParseDomainHeightPair(string token, out Domain targetDomain, out HeightLevel targetHeight)
    {
        targetDomain = Domain.Land;
        targetHeight = HeightLevel.Surface;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        string[] parts = token.Split('/');
        if (parts.Length != 2)
            return false;

        if (!TryParseDomainToken(parts[0].Trim(), out targetDomain))
            return false;
        if (!TryParseHeightToken(parts[1].Trim(), out targetHeight))
            return false;

        return true;
    }

    private static bool TryParseDomainToken(string token, out Domain domain)
    {
        domain = Domain.Land;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.Equals(token, "land", System.StringComparison.OrdinalIgnoreCase))
        {
            domain = Domain.Land;
            return true;
        }
        if (string.Equals(token, "naval", System.StringComparison.OrdinalIgnoreCase))
        {
            domain = Domain.Naval;
            return true;
        }
        if (string.Equals(token, "submarine", System.StringComparison.OrdinalIgnoreCase))
        {
            domain = Domain.Submarine;
            return true;
        }
        if (string.Equals(token, "air", System.StringComparison.OrdinalIgnoreCase))
        {
            domain = Domain.Air;
            return true;
        }

        return false;
    }

    private static bool TryParseHeightToken(string token, out HeightLevel height)
    {
        height = HeightLevel.Surface;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.Equals(token, "surface", System.StringComparison.OrdinalIgnoreCase))
        {
            height = HeightLevel.Surface;
            return true;
        }
        if (string.Equals(token, "submerged", System.StringComparison.OrdinalIgnoreCase))
        {
            height = HeightLevel.Submerged;
            return true;
        }
        if (string.Equals(token, "low", System.StringComparison.OrdinalIgnoreCase))
        {
            height = HeightLevel.AirLow;
            return true;
        }
        if (string.Equals(token, "high", System.StringComparison.OrdinalIgnoreCase))
        {
            height = HeightLevel.AirHigh;
            return true;
        }

        return false;
    }

    private static bool TryParseSetFoWCommand(string rawCommand, out bool fogEnabled)
    {
        fogEnabled = true;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        const string prefixA = "fow ";
        const string prefixB = "fog of war ";
        const string prefixC = "set fog ";
        const string prefixD = "set fow ";
        string token;
        if (trimmed.StartsWith(prefixA, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixA.Length).Trim();
        else if (trimmed.StartsWith(prefixB, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixB.Length).Trim();
        else if (trimmed.StartsWith(prefixC, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixC.Length).Trim();
        else if (trimmed.StartsWith(prefixD, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixD.Length).Trim();
        else
            return false;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.Equals(token, "on", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "true", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "1", System.StringComparison.OrdinalIgnoreCase))
        {
            fogEnabled = true;
            return true;
        }

        if (string.Equals(token, "off", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "false", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(token, "0", System.StringComparison.OrdinalIgnoreCase))
        {
            fogEnabled = false;
            return true;
        }

        return false;
    }

    private static bool TryParseSetFogAlphaCommand(string rawCommand, out int alphaPercent)
    {
        alphaPercent = 0;
        if (string.IsNullOrWhiteSpace(rawCommand))
            return false;

        string trimmed = rawCommand.Trim();
        const string prefix = "set fow ";
        if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string token = trimmed.Substring(prefix.Length).Trim();
        if (!int.TryParse(token, out int parsed) || parsed < 0 || parsed > 100)
            return false;

        alphaPercent = parsed;
        return true;
    }

    private string GetInputText()
    {
        if (resolvedCommandInputField == null)
            resolvedCommandInputField = ResolveCommandInputComponentFromGameObject(commandInputObject);
        if (resolvedCommandInputField == null)
            return string.Empty;

        if (resolvedCommandInputField is InputField uiInputField)
            return uiInputField.text;

        PropertyInfo textProperty = GetCachedTextProperty(resolvedCommandInputField.GetType());
        if (textProperty == null || textProperty.PropertyType != typeof(string))
            return string.Empty;

        object value = textProperty.GetValue(resolvedCommandInputField);
        return value as string ?? string.Empty;
    }

    private void SetInputText(string value)
    {
        if (resolvedCommandInputField == null)
            resolvedCommandInputField = ResolveCommandInputComponentFromGameObject(commandInputObject);
        if (resolvedCommandInputField == null)
            return;

        if (resolvedCommandInputField is InputField uiInputField)
        {
            uiInputField.text = value;
            return;
        }

        PropertyInfo textProperty = GetCachedTextProperty(resolvedCommandInputField.GetType());
        if (textProperty == null || textProperty.PropertyType != typeof(string) || !textProperty.CanWrite)
            return;

        textProperty.SetValue(resolvedCommandInputField, value);
    }

    private PropertyInfo GetCachedTextProperty(System.Type inputType)
    {
        if (inputType == null)
            return null;

        if (cachedTextProperty != null && cachedTextProperty.DeclaringType == inputType)
            return cachedTextProperty;

        cachedTextProperty = inputType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
        return cachedTextProperty;
    }

    private Component ResolveCommandInputComponentFromGameObject(GameObject candidateObject)
    {
        if (candidateObject == null)
            return null;

        InputField directInputField = candidateObject.GetComponent<InputField>();
        if (directInputField != null)
            return directInputField;

        InputField parentInputField = candidateObject.GetComponentInParent<InputField>();
        if (parentInputField != null)
            return parentInputField;

        Component[] components = candidateObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null)
                continue;
            if (c.GetType().Name.Contains("TMP_InputField"))
                return c;

            PropertyInfo textPropAny = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textPropAny != null && textPropAny.PropertyType == typeof(string))
                return c;
        }

        return null;
    }

    private Component FindAnyInputLikeComponentInChildren()
    {
        Component[] components = GetComponentsInChildren<Component>(includeInactive: true);
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c == null)
                continue;

            if (c is InputField)
                return c;

            PropertyInfo textProp = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProp != null && textProp.PropertyType == typeof(string) && c.GetType().Name.Contains("InputField"))
                return c;
        }

        return null;
    }

    private bool IsCommandInputFocused()
    {
        if (resolvedCommandInputField == null)
            resolvedCommandInputField = ResolveCommandInputComponentFromGameObject(commandInputObject);

        if (resolvedLegacyInputField == null)
            resolvedLegacyInputField = resolvedCommandInputField as InputField;
        if (resolvedTmpInputField == null)
            resolvedTmpInputField = resolvedCommandInputField as TMP_InputField;

        if (resolvedLegacyInputField != null && resolvedLegacyInputField.isFocused)
            return true;
        if (resolvedTmpInputField != null && resolvedTmpInputField.isFocused)
            return true;

        return false;
    }

    private string currentDynamicHelpText = null;
    private Vector2 dynamicHelpScrollPos = Vector2.zero;
    private Rect dynamicHelpWindowRect = new Rect(20, 20, 300, 400);

    private void ShowDynamicHelpPanel(string text)
    {
        currentDynamicHelpText = text;
        dynamicHelpWindowRect = new Rect(20, 40, 300, Screen.height * 0.7f);
        dynamicHelpScrollPos = Vector2.zero;
    }

    private void OnGUI()
    {
        if (!string.IsNullOrEmpty(currentDynamicHelpText))
        {
            dynamicHelpWindowRect = GUI.Window(8888, dynamicHelpWindowRect, DrawDynamicHelpWindow, "DEBUG COMMANDS");
        }
    }

    private void DrawDynamicHelpWindow(int windowID)
    {
        if (GUI.Button(new Rect(dynamicHelpWindowRect.width - 25, 2, 20, 16), "X"))
        {
            currentDynamicHelpText = null;
        }

        dynamicHelpScrollPos = GUILayout.BeginScrollView(dynamicHelpScrollPos);
        GUILayout.Label(currentDynamicHelpText);
        GUILayout.EndScrollView();

        GUI.DragWindow();
    }
}
