using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuração de nova partida a ser transportada entre cenas (Tela de Entrada → cena de batalha).
/// Uso: NewGamePanelController chama Set() antes de LoadScene; MatchController.Awake chama Apply() + Clear().
/// </summary>
public static class PartidaConfig
{
    public static bool HasPending { get; private set; }

    public static int PlayerCount { get; private set; }
    public static TeamId[] Teams { get; private set; }
    public static bool[] IsAI { get; private set; }
    public static bool[] FlipX { get; private set; }
    public static MatchController.GameSetupPreset Preset { get; private set; }
    public static bool CommandServiceAutomatic { get; private set; }
    public static string TargetScene { get; private set; }

    public static void Set(
        int playerCount,
        TeamId[] teams,
        bool[] isAI,
        bool[] flipX,
        MatchController.GameSetupPreset preset,
        bool commandServiceAutomatic,
        string targetScene)
    {
        PlayerCount = Mathf.Clamp(playerCount, 2, 4);
        Teams = teams;
        IsAI = isAI;
        FlipX = flipX;
        Preset = preset;
        CommandServiceAutomatic = commandServiceAutomatic;
        TargetScene = targetScene;
        HasPending = true;
    }

    public static void Apply(MatchController mc)
    {
        if (!HasPending || mc == null)
            return;

        List<int> teamIds = new List<int>(PlayerCount);
        List<bool> flipXs = new List<bool>(PlayerCount);
        List<bool> isAIs = new List<bool>(PlayerCount);
        List<int> startMoneys = new List<int>(PlayerCount);
        List<int> actualMoneys = new List<int>(PlayerCount);
        List<int> incomePerTurns = new List<int>(PlayerCount);
        List<bool> startMoneyAppliedFlags = new List<bool>(PlayerCount);

        for (int i = 0; i < PlayerCount; i++)
        {
            TeamId team = (Teams != null && i < Teams.Length) ? Teams[i] : TeamId.Green;
            teamIds.Add((int)team);
            flipXs.Add((FlipX != null && i < FlipX.Length) ? FlipX[i] : DefaultFlipX(team));
            isAIs.Add((IsAI != null && i < IsAI.Length) && IsAI[i]);
            startMoneys.Add(0);
            actualMoneys.Add(0);
            incomePerTurns.Add(0);
            startMoneyAppliedFlags.Add(false);
        }

        mc.ImportPlayersState(teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyAppliedFlags, false);
        mc.SetGameSetupPreset(Preset);
        mc.CommandServiceAutomatic = CommandServiceAutomatic;
    }

    public static void Clear()
    {
        HasPending = false;
        Teams = null;
        IsAI = null;
        FlipX = null;
        TargetScene = null;
    }

    private static bool DefaultFlipX(TeamId team)
    {
        return team == TeamId.Red || team == TeamId.Yellow;
    }
}
