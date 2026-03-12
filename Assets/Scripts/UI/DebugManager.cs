using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DebugManager : MonoBehaviour
{
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

    private void Awake()
    {
        TryAutoAssignReferences();
        if (sendButton != null)
            sendButton.onClick.AddListener(HandleSendClicked);
        RegisterInputSubmitListeners();
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(HandleSendClicked);
        UnregisterInputSubmitListeners();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoAssignReferences();
    }
#endif

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

        if (turnStateManager == null)
            return;

        string command = NormalizeCommand(rawCommand);
        bool executed = false;

        if (command == "DESTROY UNIT")
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
        else if (TryParseSetCapturePointsCommand(command, out int capturePoints))
        {
            executed = turnStateManager.TrySetConstructionCapturePointsUnderCursorFromDebug(capturePoints, out string message);
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
        else if (TryParseSetMoneyCommand(rawCommand, out int? moneyTeamOverride, out int moneyValue))
        {
            if (matchController == null)
            {
                Debug.Log("[Debug Command] MatchController nao encontrado.");
            }
            else if (moneyTeamOverride.HasValue)
            {
                TeamId team = (TeamId)Mathf.Clamp(moneyTeamOverride.Value, 0, 3);
                executed = matchController.TrySetActualMoney(team, moneyValue);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do team {(int)team} atualizado para ${Mathf.Max(0, moneyValue)}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Team {(int)team} nao encontrado na lista de players.");
                }
            }
            else
            {
                TeamId resolvedTeam = matchController.ActiveTeam;
                if (resolvedTeam == TeamId.Neutral)
                    resolvedTeam = TeamId.Green;

                executed = matchController.TrySetActualMoney(resolvedTeam, moneyValue);
                if (executed)
                {
                    cursorController?.PlayDoneSfx();
                    Debug.Log($"[Debug Command] Actual money do team ativo ({(int)resolvedTeam}) atualizado para ${Mathf.Max(0, moneyValue)}.");
                }
                else
                {
                    Debug.Log($"[Debug Command] Team ativo ({(int)resolvedTeam}) nao encontrado na lista de players.");
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
        else if (TryParseChangeAltitudeCommand(rawCommand, out Domain targetDomain, out HeightLevel targetHeight))
        {
            executed = turnStateManager.TryChangeAltitudeFromDebug(targetDomain, targetHeight, out string message);
            if (executed)
                cursorController?.PlayDoneSfx();
            else if (!string.IsNullOrWhiteSpace(message))
                Debug.Log($"[Debug Command] {message}");
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
        if (!IsEnterPressedThisFrame())
            return;

        HandleSendClicked();
    }

    private void HandleTmpInputSubmit(string _)
    {
        HandleSendClicked();
    }

    private void HandleTmpInputEndEdit(string _)
    {
        if (!IsEnterPressedThisFrame())
            return;

        HandleSendClicked();
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
        string valueToken = string.Empty;
        if (normalizedCommand.StartsWith(prefixA))
            valueToken = normalizedCommand.Substring(prefixA.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixB))
            valueToken = normalizedCommand.Substring(prefixB.Length).Trim();
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

        if (TryParseSetEmbarkedSupplyCommandForPrefix(normalizedCommand, "SET GALAO ", out amountValue))
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
        string valueToken;
        if (normalizedCommand.StartsWith(prefixA))
            valueToken = normalizedCommand.Substring(prefixA.Length).Trim();
        else if (normalizedCommand.StartsWith(prefixB))
            valueToken = normalizedCommand.Substring(prefixB.Length).Trim();
        else
            return false;

        if (string.IsNullOrWhiteSpace(valueToken))
            return false;

        return int.TryParse(valueToken, out remainingMovementValue);
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
        const string prefix = "change altitude ";
        if (!trimmed.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            return false;

        string token = trimmed.Substring(prefix.Length).Trim();
        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (string.Equals(token, "high", System.StringComparison.OrdinalIgnoreCase))
        {
            targetDomain = Domain.Air;
            targetHeight = HeightLevel.AirHigh;
            return true;
        }

        if (string.Equals(token, "low", System.StringComparison.OrdinalIgnoreCase))
        {
            targetDomain = Domain.Air;
            targetHeight = HeightLevel.AirLow;
            return true;
        }

        if (string.Equals(token, "surface", System.StringComparison.OrdinalIgnoreCase))
        {
            targetDomain = Domain.Land;
            targetHeight = HeightLevel.Surface;
            return true;
        }

        if (string.Equals(token, "sub", System.StringComparison.OrdinalIgnoreCase))
        {
            targetDomain = Domain.Submarine;
            targetHeight = HeightLevel.Submerged;
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
        string token;
        if (trimmed.StartsWith(prefixA, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixA.Length).Trim();
        else if (trimmed.StartsWith(prefixB, System.StringComparison.OrdinalIgnoreCase))
            token = trimmed.Substring(prefixB.Length).Trim();
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
}

public static class UiInputBlocker
{
    public static bool IsTextInputFocused()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (selected == null)
            return false;

        InputField legacyInput = selected.GetComponentInParent<InputField>();
        if (legacyInput != null && legacyInput.isFocused)
            return true;

        TMP_InputField tmpInput = selected.GetComponentInParent<TMP_InputField>();
        if (tmpInput != null && tmpInput.isFocused)
            return true;

        return false;
    }
}
