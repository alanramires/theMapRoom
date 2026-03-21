using System.Collections;
using UnityEngine;

public class AutomatedPlayer : MonoBehaviour
{
    [SerializeField] private ReplayManager replayManager;

    public bool IsRunning { get; private set; }

    private bool subscribedToCursorNeutral;
    private Coroutine queuedAdvanceRoutine;

    private void Awake()
    {
        if (replayManager == null)
            replayManager = FindAnyObjectByType<ReplayManager>();
    }

    private void OnDisable()
    {
        UnsubscribeFromCursorNeutral();
        StopQueuedAdvanceRoutine();
        IsRunning = false;
    }

    public void StartPlaying()
    {
        if (replayManager == null)
            return;

        IsRunning = true;
        Debug.Log("[Replay][Listener] AutomatedPlayer StartPlaying: subscribe OnCursorReturnedToNeutral");
        SubscribeToCursorNeutral();

        // Kick first batch from current snapshot position.
        replayManager.ExecuteNextReplayBatch();
    }

    public void Pause()
    {
        IsRunning = false;
        Debug.Log("[Replay][Listener] AutomatedPlayer Pause/Stop: unsubscribe OnCursorReturnedToNeutral");
        UnsubscribeFromCursorNeutral();
        StopQueuedAdvanceRoutine();
    }

    public void StopPlaying()
    {
        Pause();
    }

    private void HandleCursorReturnedToNeutral()
    {
        if (!IsRunning || replayManager == null)
            return;
        if (!replayManager.IsReplaying || !replayManager.IsPlaying)
            return;

        Debug.Log("[Replay][Listener] AutomatedPlayer received OnCursorReturnedToNeutral -> queue next batch");
        QueueAdvanceWhenReady();
    }

    private void QueueAdvanceWhenReady()
    {
        if (queuedAdvanceRoutine != null)
            return;

        queuedAdvanceRoutine = StartCoroutine(AdvanceWhenReplayStepIsReady());
    }

    private IEnumerator AdvanceWhenReplayStepIsReady()
    {
        while (IsRunning && replayManager != null && replayManager.IsReplaying && replayManager.IsPlaying && replayManager.IsStepExecutionBusy)
            yield return null;

        bool canAdvance = IsRunning && replayManager != null && replayManager.IsReplaying && replayManager.IsPlaying;
        if (canAdvance)
        {
            bool started = replayManager.ExecuteNextReplayBatch();
            Debug.Log($"[Replay][Listener] AutomatedPlayer queued advance executed started={started}");
        }

        queuedAdvanceRoutine = null;
    }

    private void StopQueuedAdvanceRoutine()
    {
        if (queuedAdvanceRoutine == null)
            return;

        StopCoroutine(queuedAdvanceRoutine);
        queuedAdvanceRoutine = null;
    }

    private void SubscribeToCursorNeutral()
    {
        if (subscribedToCursorNeutral)
            return;

        CursorController.OnCursorReturnedToNeutral += HandleCursorReturnedToNeutral;
        subscribedToCursorNeutral = true;
    }

    private void UnsubscribeFromCursorNeutral()
    {
        if (!subscribedToCursorNeutral)
            return;

        CursorController.OnCursorReturnedToNeutral -= HandleCursorReturnedToNeutral;
        subscribedToCursorNeutral = false;
    }
}
