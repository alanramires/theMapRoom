using UnityEngine;

public partial class TurnStateManager
{
    private void HandleStateAdvanced(CursorState previous, CursorState entered, string reason)
    {
        RuntimeLog($"[FSM][Enter] {previous} -> {entered} | reason={reason}");
    }

    private void HandleStateRevealedByRetreat(CursorState exited, CursorState revealed, string reason)
    {
        RuntimeLog($"[FSM][Reveal] exited={exited} revealed={revealed} | reason={reason}");

        switch (revealed)
        {
            case CursorState.PlayerMenu:
                RestorePlayerMenuAfterRetreat(exited, reason);
                break;
        }
    }

    private void HandleStateStackReset(CursorState previous, string reason)
    {
        RuntimeLog($"[FSM][Reset] previous={previous} -> Neutral | reason={reason}");
    }

    private void RestorePlayerMenuAfterRetreat(CursorState exited, string reason)
    {
        switch (exited)
        {
            case CursorState.CommandService:
            case CursorState.RemovingUnit:
            case CursorState.Saving:
            case CursorState.Loading:
                BattleMapMenuRootController.TryRestoreMenuFromStateStack(exited);
                break;
        }
    }
}
