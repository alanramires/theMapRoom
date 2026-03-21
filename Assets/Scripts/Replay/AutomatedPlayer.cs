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
        StopQueuedAdvanceRoutine();
        IsRunning = false;
    }

    public void StartPlaying()
    {
        if (replayManager == null)
            return;

        IsRunning = true;

        // Kick first batch from current snapshot position.
        replayManager.ExecuteNextReplayBatch();
    }

    public void Pause()
    {
        IsRunning = false;
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

        if (replayManager.IsStepExecutionBusy)
        {
            string reason = replayManager.GetReplayStepExecutionBusyReason();
            Debug.Log($"[Replay][AutomatedPlayer] OnNeutral recebido mas busy=true - aguardando: {reason}");
            // MoveOnly can dispatch neutral before the replay batch really finishes.
            return;
        }

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
        float delay = replayManager != null ? replayManager.GetEffectiveTimeBetweenBatchesForAutoplay() : 0f;
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        bool canAdvance = IsRunning
            && replayManager != null
            && replayManager.IsReplaying
            && replayManager.IsPlaying
            && !replayManager.IsStepExecutionBusy;
        if (canAdvance)
        {
            bool started = replayManager.ExecuteNextReplayBatch();
            Debug.Log($"[Replay][Listener] AutomatedPlayer queued advance executed started={started}");
        }
        else if (IsRunning && replayManager != null && replayManager.IsReplaying && replayManager.IsPlaying)
        {
            string reason = replayManager.GetReplayStepExecutionBusyReason();
            Debug.Log($"[Replay][AutomatedPlayer] avanco cancelado: busy=true ({reason})");
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
