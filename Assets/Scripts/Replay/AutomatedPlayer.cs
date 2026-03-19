using System.Collections;
using UnityEngine;

public class AutomatedPlayer : MonoBehaviour
{
    [SerializeField] private ReplayManager replayManager;

    public bool IsRunning { get; private set; }
    public int CurrentActionIndex { get; private set; }

    private void Awake()
    {
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
    }

    public void StartFrom(int turnNumber, TeamId observerTeam)
    {
        if (replayManager == null)
            return;

        IsRunning = true;
        CurrentActionIndex = replayManager.ResolveActionIndexForTurn(turnNumber, observerTeam);
    }

    public void Stop()
    {
        IsRunning = false;
    }

    public IEnumerator ExecuteNextAction()
    {
        if (!IsRunning || replayManager == null)
            yield break;

        yield return replayManager.ExecuteActionFromAutomatedPlayer(CurrentActionIndex);
        CurrentActionIndex++;
    }
}
