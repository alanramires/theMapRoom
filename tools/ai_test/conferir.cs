// Depois de um batch: o automata terminou? deu certo? como ficaram as unidades do slot?
int meuSlot = 1;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var rm = UnityEngine.Object.FindAnyObjectByType<ReplayManager>();
var tsm = UnityEngine.Object.FindAnyObjectByType<TurnStateManager>();
var sb = new System.Text.StringBuilder();
sb.AppendLine("turno=" + mc.CurrentTurn + " slotAtivo=" + mc.ActiveSlotId.Value + " automata ocupado=" + rm.IsStepExecutionBusy
    + " ultimo batch ok=" + rm.LastLiveAIBatchSucceeded + " cursor=" + tsm.CurrentCursorState);
foreach (var u in UnitManager.AllActive)
{
    if (u == null || u.IsDead || u.SlotIndex != meuSlot) continue;
    var c = u.CurrentCellPosition;
    sb.AppendLine(u.name + " id=" + u.InstanceId + " @" + c.x + "," + c.y + " hp=" + u.CurrentHP + " agiu=" + u.HasActed);
}
return sb.ToString();
