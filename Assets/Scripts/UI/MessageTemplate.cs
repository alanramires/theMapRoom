using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Substitutes named tokens once, preserving TMP tags and inserted text.</summary>
public static class MessageTemplate
{
    private static readonly Regex Token = new Regex(@"<([A-Za-z_][A-Za-z0-9_. ]*)>");

    public static string SelectLanguage(string portuguese, string english, bool useEnglish)
    {
        return useEnglish && !string.IsNullOrWhiteSpace(english)
            ? english
            : portuguese ?? string.Empty;
    }

    public static string Apply(string template, IReadOnlyDictionary<string, string> tokens)
    {
        if (string.IsNullOrEmpty(template) || tokens == null || tokens.Count == 0)
            return template ?? string.Empty;

        return Token.Replace(template, match =>
        {
            string name = match.Groups[1].Value;
            if (tokens.TryGetValue(name, out string value))
                return value ?? string.Empty;
            foreach (var pair in tokens)
                if (string.Equals(pair.Key?.Trim(), name, System.StringComparison.OrdinalIgnoreCase))
                    return pair.Value ?? string.Empty;
            return match.Value;
        });
    }
}
