using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class DebugManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TurnStateManager turnStateManager;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private Button sendButton;
    [Tooltip("Arraste o objeto do input (raiz ou filho).")]
    [SerializeField] private GameObject commandInputObject;

    private Component resolvedCommandInputField;
    private PropertyInfo cachedTextProperty;

    private void Awake()
    {
        TryAutoAssignReferences();
        if (sendButton != null)
            sendButton.onClick.AddListener(HandleSendClicked);
    }

    private void OnDestroy()
    {
        if (sendButton != null)
            sendButton.onClick.RemoveListener(HandleSendClicked);
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
    }

    private void HandleSendClicked()
    {
        string command = GetInputText();
        if (string.IsNullOrWhiteSpace(command))
            return;

        command = command.Trim().ToUpperInvariant();
        if (command != "M")
            return;

        if (turnStateManager == null)
            return;

        if (!turnStateManager.TryFinalizeSelectedUnitActionFromDebug())
            return;

        cursorController?.PlayDoneSfx();
        SetInputText(string.Empty);
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
