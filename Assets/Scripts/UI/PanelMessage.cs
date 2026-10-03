using System;
using System.Collections.Generic;

/// <summary>
/// Player-facing panel text, authored in Assets/DB/Messages.
/// Keep IDs stable and pass dynamic values through named tokens.
/// </summary>
public static class PanelMessage
{
    public static string Helper(string id, params (string name, object value)[] arguments)
    {
        return PanelHelperController.ResolveHelperMessage(id, "[" + id + "]", Tokens(arguments));
    }

    public static string Dialog(string id, params (string name, object value)[] arguments)
    {
        return PanelDialogController.ResolveDialogMessage(id, "[" + id + "]", Tokens(arguments));
    }

    private static IReadOnlyDictionary<string, string> Tokens((string name, object value)[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
            return null;

        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
            tokens[argument.name] = Convert.ToString(argument.value) ?? string.Empty;
        return tokens;
    }
}
