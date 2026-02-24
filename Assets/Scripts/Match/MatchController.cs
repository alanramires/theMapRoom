using UnityEngine;
using System.Collections.Generic;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MatchController : MonoBehaviour
{
    public enum GameSetupPreset
    {
        GameBoyClassic = 0,
        FisicaBasica = 1,
        AMontanhaAvacalha = 2,
        NeblinaLeve = 3,
        FogOfWarTotal = 4
    }

    [Header("Match State (MVP)")]
    [SerializeField] private int currentTurn = 0;
    [SerializeField] private int activeTeamId = (int)TeamId.Green;
    [SerializeField] private List<TeamId> players = new List<TeamId> { TeamId.Green, TeamId.Red, TeamId.Blue, TeamId.Yellow };
    [SerializeField] private bool includeNeutralTeam = false;
    // Placeholder para futura pintura de visibilidade no mapa (nao governa regras de combate no momento).
    [SerializeField, HideInInspector] private bool fogOfWar = true;
    [Header("Gameplay Setup")]
    [SerializeField] private GameSetupPreset gameSetup = GameSetupPreset.FogOfWarTotal;
    [SerializeField] private bool enableLdtValidation = true;
    [SerializeField] private bool enableLosValidation = true;
    [SerializeField] private bool enableSpotter = true;
    [SerializeField] private bool enableStealthValidation = true;
    [SerializeField] private AutonomyDatabase autonomyDatabase;
    [SerializeField] private CursorController cursorController;
    [Header("Turn Transition")]
    [SerializeField] private MatchMusicAudioManager matchMusicAudioManager;
    [SerializeField] private AudioClip advanceTurnSfx;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPreDelay = 0.5f;
    [SerializeField] [Range(0f, 2f)] private float advanceTurnPostDelay = 0.2f;
    [SerializeField] [Range(0f, 1f)] private float advanceTurnSfxVolume = 1f;
    [SerializeField] private int activePlayerListIndex = 0;
    [SerializeField, HideInInspector] private int appliedActiveTeamId = int.MinValue;
    [SerializeField, HideInInspector] private bool pendingTurnStartUpkeep;

    public int CurrentTurn => currentTurn;
    public int ActiveTeamId => activeTeamId;
    public TeamId ActiveTeam => ClampToTeamId(activeTeamId);
    public IReadOnlyList<TeamId> Players => players;
    public bool IncludeNeutralTeam => includeNeutralTeam;
    public GameSetupPreset GameSetup => gameSetup;
    public bool EnableLdtValidation => enableLdtValidation;
    public bool EnableLosValidation => enableLosValidation;
    public bool EnableSpotter => enableSpotter;
    public bool EnableStealthValidation => enableStealthValidation;
    public AutonomyDatabase AutonomyDatabase => autonomyDatabase;
    public int ActivePlayerListIndex => activePlayerListIndex;
    public bool IsTurnTransitionInProgress => advanceTurnTransitionRoutine != null;
    private Coroutine advanceTurnTransitionRoutine;

    private void Awake()
    {
        ApplyGameSetupPreset();
        NormalizeState();
        TryAutoAssignCursorController();
        TryAutoAssignTurnTransitionReferences();
        ApplyActiveTeamIfChanged(force: true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyGameSetupPreset();
        NormalizeState();
        TryAutoAssignCursorController();
        TryAutoAssignTurnTransitionReferences();
        ApplyActiveTeamIfChanged(force: false);
    }
#endif

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        TryAutoAssignCursorController();
        TryAutoAssignTurnTransitionReferences();
        ApplyActiveTeamIfChanged(force: false);
    }

    public void SetCurrentTurn(int turn)
    {
        currentTurn = Mathf.Max(0, turn);
    }

    public void SetGameSetupPreset(GameSetupPreset preset)
    {
        gameSetup = preset;
        ApplyGameSetupPreset();
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

    public void AdvanceTurnWithTransition()
    {
        if (!Application.isPlaying)
        {
            AdvanceTurn();
            return;
        }

        if (advanceTurnTransitionRoutine != null)
            StopCoroutine(advanceTurnTransitionRoutine);

        advanceTurnTransitionRoutine = StartCoroutine(AdvanceTurnTransitionRoutine());
    }

    private IEnumerator AdvanceTurnTransitionRoutine()
    {
        bool wasMusicPlaying = matchMusicAudioManager != null && matchMusicAudioManager.IsPlaying;
        bool wasPausedByUser = matchMusicAudioManager != null && matchMusicAudioManager.IsPausedByUser;
        bool usePauseResume = matchMusicAudioManager != null && matchMusicAudioManager.IsFreeMode;
        if (matchMusicAudioManager != null)
            matchMusicAudioManager.BeginTurnTransition();

        if (wasMusicPlaying && matchMusicAudioManager != null)
        {
            if (usePauseResume)
                matchMusicAudioManager.PauseForTurnTransition();
            else
                matchMusicAudioManager.StopForTurnTransition();
        }

        PlayAdvanceTurnSfx();
        if (advanceTurnPreDelay > 0f)
            yield return new WaitForSeconds(advanceTurnPreDelay);

        AdvanceTurn();

        if (advanceTurnPostDelay > 0f)
            yield return new WaitForSeconds(advanceTurnPostDelay);

        if (wasMusicPlaying && !wasPausedByUser && matchMusicAudioManager != null)
        {
            if (usePauseResume)
                matchMusicAudioManager.ResumeAfterTurnTransition();
            else
                matchMusicAudioManager.RestartCurrentModePlayback();
        }
        else if (matchMusicAudioManager != null)
        {
            matchMusicAudioManager.EndTurnTransition();
        }

        advanceTurnTransitionRoutine = null;
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

    private void ApplyGameSetupPreset()
    {
        switch (gameSetup)
        {
            case GameSetupPreset.GameBoyClassic:
                enableLdtValidation = false;
                enableLosValidation = false;
                enableSpotter = false;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.FisicaBasica:
                enableLdtValidation = true;
                enableLosValidation = false;
                enableSpotter = false;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.AMontanhaAvacalha:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = false;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.NeblinaLeve:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = false;
                break;
            case GameSetupPreset.FogOfWarTotal:
            default:
                enableLdtValidation = true;
                enableLosValidation = true;
                enableSpotter = true;
                enableStealthValidation = true;
                break;
        }
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
        TeleportCursorToActiveTeamHeadQuarterSilently();
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

    private void TryAutoAssignCursorController()
    {
        if (cursorController == null)
            cursorController = FindAnyObjectByType<CursorController>();
    }

    private void TryAutoAssignTurnTransitionReferences()
    {
        if (matchMusicAudioManager == null)
            matchMusicAudioManager = FindAnyObjectByType<MatchMusicAudioManager>();

#if UNITY_EDITOR
        if (advanceTurnSfx == null)
            advanceTurnSfx = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/audio/UI/ending the turn.MP3");
#endif
    }

    private void PlayAdvanceTurnSfx()
    {
        if (advanceTurnSfx == null)
            return;

        float volume = Mathf.Clamp01(advanceTurnSfxVolume);
        AudioSource.PlayClipAtPoint(advanceTurnSfx, transform.position, volume);
    }

    private void TeleportCursorToActiveTeamHeadQuarterSilently()
    {
        if (!Application.isPlaying)
            return;
        if (activeTeamId < 0)
            return;
        if (cursorController == null)
            return;

        ConstructionManager[] constructions = FindObjectsByType<ConstructionManager>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        ConstructionManager bestHq = null;
        for (int i = 0; i < constructions.Length; i++)
        {
            ConstructionManager c = constructions[i];
            if (c == null || !c.gameObject.activeInHierarchy)
                continue;
            if (!IsHeadQuarterConstruction(c))
                continue;
            if ((int)c.TeamId != activeTeamId)
                continue;

            if (bestHq == null || c.InstanceId < bestHq.InstanceId)
                bestHq = c;
        }

        if (bestHq == null)
            return;

        Vector3Int hqCell = bestHq.CurrentCellPosition;
        hqCell.z = 0;
        cursorController.SetCell(hqCell, playMoveSfx: false);
    }

    private static bool IsHeadQuarterConstruction(ConstructionManager construction)
    {
        if (construction == null)
            return false;

        if (construction.IsPlayerHeadQuarter)
            return true;

        string constructionId = construction.ConstructionId;
        if (!string.IsNullOrWhiteSpace(constructionId) &&
            string.Equals(constructionId.Trim(), "hq", System.StringComparison.OrdinalIgnoreCase))
            return true;

        string displayName = construction.ConstructionDisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName.IndexOf("hq", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}
