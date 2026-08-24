using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuração de nova partida a ser transportada entre cenas (Tela de Entrada → cena de batalha).
/// Uso: NewGamePanelController chama Set() antes de LoadScene; MatchController.Awake chama Apply() + Clear().
/// </summary>
public static class PartidaConfig
{
    public static bool HasPending { get; private set; }

    // Dificuldade da IA escolhida na Tela de Entrada. Vive separada do restante do config
    // (nao e limpa por Clear()) porque quem a consome e o AIController.Start, que roda depois
    // do MatchController.Awake ja ter chamado Apply()+Clear().
    private static AIDifficulty? pendingDifficulty;

    // Endereco do quadrante escolhido na cena Campanha. Assim como a dificuldade,
    // usa consumo proprio porque o QuadranteController constroi o tabuleiro antes
    // de o MatchController consumir e limpar a configuracao geral da partida.
    private static string pendingCampanhaId;
    private static string pendingQuadranteId;

    public static void SetDifficulty(AIDifficulty difficulty) => pendingDifficulty = difficulty;

    public static bool TryConsumeDifficulty(out AIDifficulty difficulty)
    {
        if (pendingDifficulty.HasValue)
        {
            difficulty = pendingDifficulty.Value;
            pendingDifficulty = null;
            return true;
        }
        difficulty = AIDifficulty.Facil;
        return false;
    }

    public static void SetQuadrante(string campanhaId, string quadranteId)
    {
        pendingCampanhaId = campanhaId;
        pendingQuadranteId = quadranteId;
    }

    public static bool TryConsumeQuadrante(out string campanhaId, out string quadranteId)
    {
        campanhaId = pendingCampanhaId;
        quadranteId = pendingQuadranteId;
        if (string.IsNullOrWhiteSpace(campanhaId) || string.IsNullOrWhiteSpace(quadranteId))
            return false;

        pendingCampanhaId = null;
        pendingQuadranteId = null;
        return true;
    }

    // Cor escolhida pelo jogador ao iniciar um tutorial pela Tela de Entrada.
    // Canal separado do Set/Apply: o tutorial preserva toda a config serializada da
    // cena (preset, economia, isAI) e apenas recolore o slot 0 (com swap se colidir).
    private static TeamId? pendingTutorialPlayerTeam;

    public static void SetTutorialPlayerTeam(TeamId team) => pendingTutorialPlayerTeam = team;

    public static bool TryConsumeTutorialPlayerTeam(out TeamId team)
    {
        if (pendingTutorialPlayerTeam.HasValue)
        {
            team = pendingTutorialPlayerTeam.Value;
            pendingTutorialPlayerTeam = null;
            return true;
        }
        team = TeamId.Neutral;
        return false;
    }

    public static int PlayerCount { get; private set; }
    public static TeamId[] Teams { get; private set; }
    public static bool[] IsAI { get; private set; }
    public static bool[] FlipX { get; private set; }
    public static MatchController.GameSetupPreset Preset { get; private set; }
    public static bool[] CommandServiceAutomatic { get; private set; }
    public static string TargetScene { get; private set; }

    public static void Set(
        int playerCount,
        TeamId[] teams,
        bool[] isAI,
        bool[] flipX,
        MatchController.GameSetupPreset preset,
        bool[] commandServiceAutomatic,
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

        // A economia da cena-base pertence ao slot logico, nao a cor escolhida.
        // Ex.: ao trocar slot 0 de Green para Yellow, ele deve conservar o caixa
        // inicial configurado para o slot 0, em vez de consultar a entrada Yellow
        // (que normalmente esta zerada na cena de dois jogadores).
        List<int> sourceTeamIds = new List<int>();
        List<bool> sourceFlipXs = new List<bool>();
        List<bool> sourceIsAIs = new List<bool>();
        List<int> sourceStartMoneys = new List<int>();
        List<int> sourceActualMoneys = new List<int>();
        List<int> sourceIncomePerTurns = new List<int>();
        List<bool> sourceStartMoneyApplied = new List<bool>();
        mc.ExportPlayersState(
            sourceTeamIds,
            sourceFlipXs,
            sourceIsAIs,
            sourceStartMoneys,
            sourceActualMoneys,
            sourceIncomePerTurns,
            sourceStartMoneyApplied);

        for (int i = 0; i < PlayerCount; i++)
        {
            TeamId team = (Teams != null && i < Teams.Length) ? Teams[i] : TeamId.Green;
            teamIds.Add((int)team);
            flipXs.Add((FlipX != null && i < FlipX.Length) ? FlipX[i] : DefaultFlipX(team));
            isAIs.Add((IsAI != null && i < IsAI.Length) && IsAI[i]);
            startMoneys.Add(i < sourceStartMoneys.Count ? sourceStartMoneys[i] : 0);
            actualMoneys.Add(i < sourceActualMoneys.Count ? sourceActualMoneys[i] : 0);
            incomePerTurns.Add(i < sourceIncomePerTurns.Count ? sourceIncomePerTurns[i] : 0);
            startMoneyAppliedFlags.Add(false);
        }

        mc.ImportPlayersState(teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyAppliedFlags, false);
        // Define o slot 0 como ativo sem consumir economia/upkeep durante o Awake.
        // O MatchController.Start reaplica o time com efeitos de inicio de turno,
        // depois que construcoes e unidades ja resolveram seus slotIndex.
        if (teamIds.Count > 0)
            mc.SetActiveTeamIdWithoutTurnStart(teamIds[0]);
        mc.SetGameSetupPreset(Preset);
        for (int i = 0; i < PlayerCount; i++)
        {
            bool cmdAuto = (CommandServiceAutomatic != null && i < CommandServiceAutomatic.Length) ? CommandServiceAutomatic[i] : false;
            mc.SetPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(i), cmdAuto);
        }
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
