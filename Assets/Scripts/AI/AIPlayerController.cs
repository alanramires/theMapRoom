using UnityEngine;

// Coordena o turno da IA: constroi o snapshot, avalia a postura e (futuramente) executa o plano.
// Assina OnActiveTeamChanged e age quando o time ativo tiver isAI = true.
public class AIPlayerController : MonoBehaviour
{
    [SerializeField] private MatchController matchController;
    [SerializeField] private bool aiLog = true;

    private AIProfile profile;
    private AIStance currentStance = AIStance.Attack;

    public AIStance CurrentStance => currentStance;

    private void Awake()
    {
        profile = new BeginnerAIProfile();
    }

    private void OnEnable()
    {
        MatchController.OnActiveTeamChanged += HandleActiveTeamChanged;
    }

    private void OnDisable()
    {
        MatchController.OnActiveTeamChanged -= HandleActiveTeamChanged;
    }

    private void HandleActiveTeamChanged(int teamId)
    {
        if (matchController == null)
            matchController = FindAnyObjectByType<MatchController>();

        if (matchController == null)
            return;

        TeamId team = (TeamId)teamId;
        if (!matchController.IsPlayerAI(team))
            return;

        OnAITurnStarted(team);
    }

    private void OnAITurnStarted(TeamId aiTeam)
    {
        AISnapshot snapshot = AISnapshot.Build(aiTeam, matchController);
        currentStance = profile.EvaluateStance(snapshot);

        if (aiLog)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[AI] {aiTeam} | postura: {currentStance} | amigos: {snapshot.FriendlyUnits.Count} | inimigos visiveis: {snapshot.VisibleEnemies.Count}");
            sb.AppendLine($"  HQ proprio: {(snapshot.HasHq ? snapshot.HqCell.ToString() : "nao encontrado")}");

            for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
            {
                AIConstructionInfo info = snapshot.KnownConstructions[i];
                if (info.IsHq && info.TeamId != aiTeam)
                    sb.AppendLine($"  HQ inimigo ({info.TeamId}): {info.Cell}");
            }

            int totalConstructions = snapshot.KnownConstructions.Count;
            int ownedByAI = 0;
            for (int i = 0; i < snapshot.KnownConstructions.Count; i++)
                if (snapshot.KnownConstructions[i].TeamId == aiTeam) ownedByAI++;

            sb.Append($"  construcoes no mapa: {totalConstructions} | proprias: {ownedByAI}");
            Debug.Log(sb.ToString());
        }
    }
}
