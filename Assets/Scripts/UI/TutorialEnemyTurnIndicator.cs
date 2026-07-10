using UnityEngine;

/// <summary>
/// Indicador de turno inimigo para cenas de TUTORIAL (que nao tem AIController):
/// espelha o visual do "TURNO DA IA" do Battle Map — caixa central pulsante com a
/// cor do time ativo — enquanto o turno nao for do jogador (slot 0).
/// Basta adicionar este componente a qualquer objeto da cena do tutorial.
/// </summary>
public class TutorialEnemyTurnIndicator : MonoBehaviour
{
    // Auto-instala em toda cena de tutorial (IsTutorialMode): nenhum passo manual
    // no editor, e as Historias futuras ja nascem com o indicador.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstanceForTutorialScene();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => EnsureInstanceForTutorialScene();
    }

    private static void EnsureInstanceForTutorialScene()
    {
        MatchController mc = FindAnyObjectByType<MatchController>();
        if (mc == null || !mc.IsTutorialMode)
            return;
        if (FindAnyObjectByType<TutorialEnemyTurnIndicator>() != null)
            return;

        new GameObject("Tutorial Enemy Turn Indicator").AddComponent<TutorialEnemyTurnIndicator>();
    }

    [SerializeField] private MatchController matchController;

    [Tooltip("Titulo exibido durante o turno inimigo.")]
    [SerializeField] private string title = "TURNO DO INIMIGO";

    [Tooltip("Linha de estagio quando ha tropas inimigas em campo.")]
    [SerializeField] private string stageWithUnits = "MOVIMENTANDO TROPAS...";

    [Tooltip("Linha de estagio quando o campo inimigo esta vazio.")]
    [SerializeField] private string stageWithoutUnits = "OBSERVANDO O CAMPO...";

    private GUIStyle titleStyle;
    private GUIStyle stageStyle;
    private GUIStyle boxStyle;

    private void Awake()
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();
    }

    private void OnGUI()
    {
        if (matchController == null || matchController.HasVictoryWinner || !matchController.IsTutorialMode)
            return;

        TeamId activeTeam = matchController.ActiveTeam;
        if (activeTeam == TeamId.Neutral || activeTeam == matchController.GetTeamIdForSlot(0))
            return;

        // Nao duplicar com o AIController, se a cena tiver um (ele ja desenha o dele).
        if (matchController.IsPlayerAI(activeTeam))
            return;

        EnsureStyles();

        // Mesmo layout do indicador da IA no Battle Map.
        float width = Mathf.Clamp(Screen.width * 0.34f, 250f, 440f);
        float height = Mathf.Clamp(Screen.height * 0.085f, 58f, 90f);
        Rect panel = new Rect(
            (Screen.width - width) * 0.5f,
            (Screen.height - height) * 0.5f,
            width,
            height);
        float pulse = 0.68f + 0.32f * (0.5f + 0.5f * Mathf.Sin(Time.realtimeSinceStartup * 5f));
        Color team = TeamUtils.GetColor(activeTeam);

        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.06f, 0.075f, 0.88f * pulse);
        GUI.Box(panel, GUIContent.none, boxStyle);

        GUI.color = new Color(1f, 1f, 1f, pulse);
        titleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.027f, 18f, 32f));
        titleStyle.normal.textColor = Color.Lerp(team, Color.white, 0.28f);
        GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, panel.width - 16f, panel.height * 0.55f),
            title, titleStyle);

        stageStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.017f, 12f, 20f));
        stageStyle.normal.textColor = Color.Lerp(team, Color.white, 0.5f);
        GUI.Label(new Rect(panel.x + 8f, panel.y + panel.height * 0.5f, panel.width - 16f, panel.height * 0.42f),
            HasActiveTeamUnits(activeTeam) ? stageWithUnits : stageWithoutUnits, stageStyle);
        GUI.color = previousColor;
    }

    private bool HasActiveTeamUnits(TeamId team)
    {
        var units = UnitManager.AllActive;
        for (int i = 0; i < units.Count; i++)
        {
            UnitManager unit = units[i];
            if (unit != null && !unit.IsDead && !unit.IsEmbarked && unit.TeamId == team)
                return true;
        }

        return false;
    }

    private void EnsureStyles()
    {
        if (boxStyle == null)
            boxStyle = new GUIStyle(GUI.skin.box);
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
        if (stageStyle == null)
        {
            stageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
