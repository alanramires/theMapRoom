using UnityEngine;
using System.Collections.Generic;

public class MatchController : MonoBehaviour
{
    [Header("Match State (MVP)")]
    [SerializeField] private int currentTurn = 0;
    [SerializeField] private int activeTeamId = (int)TeamId.Green;
    [SerializeField] private List<TeamId> players = new List<TeamId> { TeamId.Green, TeamId.Red, TeamId.Blue, TeamId.Yellow };
    [SerializeField] private bool includeNeutralTeam = false;
    [SerializeField] private bool fogOfWar = true;
    [SerializeField] private AutonomyDatabase autonomyDatabase;
    [SerializeField] private int activePlayerListIndex = 0;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField, HideInInspector] private bool pendingTurnStartUpkeep;

    public int CurrentTurn => currentTurn;
    public int ActiveTeamId => activeTeamId;
    public TeamId ActiveTeam => ClampToTeamId(activeTeamId);
    public IReadOnlyList<TeamId> Players => players;
    public bool IncludeNeutralTeam => includeNeutralTeam;
    public bool FogOfWar => fogOfWar;
    public AutonomyDatabase AutonomyDatabase => autonomyDatabase;
    public int ActivePlayerListIndex => activePlayerListIndex;

    private void Awake()
    {
        NormalizeState();
        ApplyActiveTeamIfChanged(force: true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeState();
        ApplyActiveTeamIfChanged(force: false);
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        ApplyActiveTeamIfChanged(force: false);
    }

    public void SetCurrentTurn(int turn)
    {
        currentTurn = Mathf.Max(0, turn);
    }

    public void SetActiveTeamId(int teamId)
    {
        activeTeamId = Mathf.Clamp(teamId, -1, 3);
        SyncActivePlayerIndexFromActiveTeam();
        ApplyActiveTeamIfChanged(force: false);
    }

    // Debug: avanca apenas o cursor de team sem alterar currentTurn.
    public void AdvanceTeam()
    {
        if (players.Count == 0)
        {
            if (includeNeutralTeam)
                SetNeutralActiveTeam();
            return;
        }

        if (activePlayerListIndex >= 0)
        {
            int next = activePlayerListIndex + 1;
            if (next < players.Count)
            {
                SetActivePlayerByIndex(next);
                return;
            }

            if (includeNeutralTeam)
            {
                SetNeutralActiveTeam();
                return;
            }

            SetActivePlayerByIndex(0, forceApply: true);
            return;
        }

        SetActivePlayerByIndex(0, forceApply: true);
    }

    // Avanca para o proximo membro da lista. So incrementa currentTurn ao "fechar ciclo".
    public void AdvanceTurn()
    {
        if (players.Count == 0)
        {
            if (includeNeutralTeam)
                SetNeutralActiveTeam();
            currentTurn = Mathf.Max(0, currentTurn + 1);
            return;
        }

        // Caso padrao: estamos em um player da lista.
        if (activePlayerListIndex >= 0)
        {
            int next = activePlayerListIndex + 1;
            if (next < players.Count)
            {
                pendingTurnStartUpkeep = true;
                SetActivePlayerByIndex(next);
                return;
            }

            // Saiu da lista: vai para neutral se estiver habilitado.
            if (includeNeutralTeam)
            {
                SetNeutralActiveTeam();
                return;
            }

            // Sem neutral: fecha ciclo de turno.
            currentTurn = Mathf.Max(0, currentTurn + 1);
            pendingTurnStartUpkeep = true;
            SetActivePlayerByIndex(0, forceApply: true);
            return;
        }

        // Estavamos em neutral (ou fora da lista): fecha ciclo de turno e volta para o primeiro player.
        currentTurn = Mathf.Max(0, currentTurn + 1);
        pendingTurnStartUpkeep = true;
        SetActivePlayerByIndex(0, forceApply: true);
    }

    private void NormalizeState()
    {
        currentTurn = Mathf.Max(0, currentTurn);
        activeTeamId = Mathf.Clamp(activeTeamId, -1, 3);
        NormalizePlayersList();
        SyncActivePlayerIndexFromActiveTeam();

        if (players.Count == 0)
        {
            activePlayerListIndex = -1;
            if (includeNeutralTeam || activeTeamId < -1 || activeTeamId > 3)
                activeTeamId = (int)TeamId.Neutral;
            return;
        }

        if (activeTeamId == (int)TeamId.Neutral)
        {
            if (!includeNeutralTeam)
                SetActivePlayerByIndex(0);
            return;
        }

        if (activePlayerListIndex < 0)
            SetActivePlayerByIndex(0);
    }

    private static TeamId ClampToTeamId(int value)
    {
        if (value < -1)
            value = -1;
        if (value > 3)
            value = 3;
        return (TeamId)value;
    }

    private void NormalizePlayersList()
    {
        if (players == null)
            players = new List<TeamId>();

        HashSet<TeamId> seen = new HashSet<TeamId>();
        for (int i = players.Count - 1; i >= 0; i--)
        {
            TeamId team = players[i];
            if (team == TeamId.Neutral)
            {
                players.RemoveAt(i);
                continue;
            }

            if (!seen.Add(team))
                players.RemoveAt(i);
        }
    }

    private void SyncActivePlayerIndexFromActiveTeam()
    {
        if (players == null || players.Count == 0 || activeTeamId == (int)TeamId.Neutral)
        {
            activePlayerListIndex = -1;
            return;
        }

        TeamId activeTeam = ClampToTeamId(activeTeamId);
        activePlayerListIndex = players.IndexOf(activeTeam);
    }

    private void SetActivePlayerByIndex(int index, bool forceApply = false)
    {
        if (players == null || players.Count == 0)
            return;

        index = Mathf.Clamp(index, 0, players.Count - 1);
        activePlayerListIndex = index;
        activeTeamId = (int)players[index];
        ApplyActiveTeamIfChanged(force: forceApply);
    }

    private void SetNeutralActiveTeam()
    {
        activePlayerListIndex = -1;
        activeTeamId = (int)TeamId.Neutral;
        ApplyActiveTeamIfChanged(force: false);
    }

    private void ApplyActiveTeamIfChanged(bool force)
    {
        if (!force && appliedActiveTeamId == activeTeamId)
            return;

        appliedActiveTeamId = activeTeamId;
        ReleaseUnitsForActiveTeam();
    }

    private void ReleaseUnitsForActiveTeam()
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0)
            return;

        UnitManager[] units = FindObjectsByType<UnitManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < units.Length; i++)
        {
            UnitManager unit = units[i];
            if (unit == null)
                continue;
            if ((int)unit.TeamId != activeTeamId)
                continue;

            if (pendingTurnStartUpkeep)
            {
                int turnStartUpkeep = OperationalAutonomyRules.GetTurnStartAutonomyUpkeep(unit, autonomyDatabase);
                if (turnStartUpkeep > 0)
                    unit.SetCurrentFuel(Mathf.Max(0, unit.CurrentFuel - turnStartUpkeep));
            }

            unit.ResetActed();
        }

        pendingTurnStartUpkeep = false;
    }
}
