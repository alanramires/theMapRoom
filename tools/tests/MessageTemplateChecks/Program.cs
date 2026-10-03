using System;
using System.Collections.Generic;

internal static class Program
{
    private static int checks;
    private static void Equal(string expected, string actual)
    {
        checks++;
        if (expected != actual)
            throw new Exception($"Expected [{expected}], received [{actual}]");
    }

    private static void Main()
    {
        var tokens = new Dictionary<string, string> { ["unit"] = "Tanque", ["damage"] = "5" };
        Equal("5 de dano: Tanque", MessageTemplate.Apply("<damage> de dano: <unit>", tokens));
        Equal("<b>Tanque</b> <color=#FF0000>5</color>", MessageTemplate.Apply("<b><unit></b> <color=#FF0000><damage></color>", tokens));
        Equal("Tanque/Tanque/Tanque", MessageTemplate.Apply("<UNIT>/<Unit>/<unit>", tokens));
        Equal("<unknown> Tanque", MessageTemplate.Apply("<unknown> <unit>", tokens));
        // Data resembling a token must not be interpreted again, regardless of dictionary order.
        tokens["unit"] = "<damage>";
        Equal("<damage> recebe 5", MessageTemplate.Apply("<unit> recebe <damage>", tokens));
        tokens["unit"] = null;
        Equal("/5", MessageTemplate.Apply("<unit>/<damage>", tokens));
        Equal("", MessageTemplate.Apply(null, tokens));
        Equal("<unit>", MessageTemplate.Apply("<unit>", null));
        Equal("ready", MessageTemplate.Apply("ready", tokens));
        Equal("Doar/Receber", MessageTemplate.Apply("<transfer_type.selecionado>/<transfer type>",
            new Dictionary<string, string> { ["transfer_type.selecionado"] = "Doar", ["transfer type"] = "Receber" }));
        Equal("<color=#00FF00>Tanque</color>", MessageTemplate.Apply("<color=#<color>><unit></color>",
            new Dictionary<string, string> { ["color"] = "00FF00", ["unit"] = "Tanque" }));
        Equal("Cancelar", MessageTemplate.SelectLanguage("Cancelar", "Cancel", false));
        Equal("Cancel", MessageTemplate.SelectLanguage("Cancelar", "Cancel", true));
        Equal("Cancelar", MessageTemplate.SelectLanguage("Cancelar", null, true));
        Equal("Cancelar", MessageTemplate.SelectLanguage("Cancelar", "", true));
        Equal("Cancelar", MessageTemplate.SelectLanguage("Cancelar", "  ", true));
        Equal("", MessageTemplate.SelectLanguage(null, null, true));
        Console.WriteLine($"{checks} formatter checks passed.");
    }
}
