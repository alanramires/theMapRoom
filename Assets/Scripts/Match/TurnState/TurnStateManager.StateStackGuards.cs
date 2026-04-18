using System.Text;
using UnityEngine;

public partial class TurnStateManager
{
    private bool TryPeekPreviousState(out CursorState previous)
    {
        previous = CursorState.Neutral;
        EnsureStateStackInitialized();

        if (stateStack.Count < 2)
            return false;

        CursorState[] states = stateStack.ToArray();
        previous = states[1];
        return true;
    }

    private bool IsPreviousState(CursorState state)
    {
        return TryPeekPreviousState(out CursorState previous) && previous == state;
    }

    private bool IsCurrentState(CursorState state)
    {
        return CurrentCursorState == state;
    }

    private static bool IsMovementState(CursorState state)
    {
        return state == CursorState.MoveuAndando || state == CursorState.MoveuParado;
    }

    private static bool IsResetOnlyState(CursorState state)
    {
        return state == CursorState.AircraftFuelDepletionQueue ||
               state == CursorState.TurnStartRallyQueue ||
               state == CursorState.CommandServiceExecuting ||
               state == CursorState.RemovingUnitExecuting;
    }

    private void ValidateStateStack(string reason)
    {
        if (!enableTurnStateRuntimeLogs)
            return;

        EnsureStateStackInitialized();
        CursorState[] states = stateStack.ToArray();
        if (states.Length == 0)
        {
            WarnStateStackIssue(reason, "pilha vazia");
            return;
        }

        int bottomIndex = states.Length - 1;
        if (states[bottomIndex] != CursorState.Neutral)
            WarnStateStackIssue(reason, $"base da pilha deveria ser Neutral, atual={states[bottomIndex]}");

        for (int i = 0; i < states.Length; i++)
        {
            CursorState current = states[i];
            bool isTop = i == 0;
            bool isBottom = i == bottomIndex;

            if (current == CursorState.Neutral && !isBottom)
                WarnStateStackIssue(reason, "Neutral apareceu acima da base da pilha");

            if (i + 1 < states.Length && current == states[i + 1])
                WarnStateStackIssue(reason, $"estado duplicado consecutivo: {current}");

            if (current == CursorState.CommandServiceExecuting)
            {
                CursorState parent = i + 1 < states.Length ? states[i + 1] : CursorState.Neutral;
                if (parent != CursorState.CommandService)
                    WarnStateStackIssue(reason, $"CommandServiceExecuting requer pai CommandService, pai atual={parent}");
            }

            if (current == CursorState.RemovingUnitExecuting)
            {
                CursorState parent = i + 1 < states.Length ? states[i + 1] : CursorState.Neutral;
                if (parent != CursorState.RemovingUnit)
                    WarnStateStackIssue(reason, $"RemovingUnitExecuting requer pai RemovingUnit, pai atual={parent}");
            }

            if (current == CursorState.PlayerMenu && !isTop)
            {
                CursorState child = i - 1 >= 0 ? states[i - 1] : CursorState.Neutral;
                if (child != CursorState.CommandService && child != CursorState.RemovingUnit)
                    WarnStateStackIssue(reason, $"PlayerMenu so deve manter CommandService/RemovingUnit acima, filho atual={child}");
            }

            if (IsResetOnlyState(current) && !isTop)
                WarnStateStackIssue(reason, $"{current} deveria estar no topo e finalizar por ExecuteAndReset");
        }
    }

    private void ValidateRetreatSource(CursorState exited, string reason)
    {
        if (!enableTurnStateRuntimeLogs)
            return;

        if (IsResetOnlyState(exited))
            WarnStateStackIssue(reason, $"Retreat chamado a partir de estado reset-only: {exited}");
    }

    private void WarnStateStackIssue(string reason, string issue)
    {
        Debug.LogWarning($"[FSM][StackGuard] {issue} | reason={reason} | stack={FormatStateStack()}", this);
    }

    private string FormatStateStackMultiline()
    {
        EnsureStateStackInitialized();
        CursorState[] states = stateStack.ToArray();
        StringBuilder sb = new StringBuilder();
        for (int i = states.Length - 1; i >= 0; i--)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(states[i]);
        }
        return sb.ToString();
    }
}
