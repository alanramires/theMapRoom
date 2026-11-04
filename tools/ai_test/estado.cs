// Tabuleiro visto por UM slot (fog-honesto: inimigo so se visivel para ele).
// Uso: unity command script eval --file tools/ai_test/estado.cs
int meuSlot = 1;

var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
var meu = PlayerSlotId.FromIndex(meuSlot);
var sb = new System.Text.StringBuilder();
sb.AppendLine("turno=" + mc.CurrentTurn + " slotAtivo=" + mc.ActiveSlotId.Value + " regras=" + mc.GameSetup
    + " dificuldade=" + (ai != null ? ai.AppliedDifficulty.ToString() : "-")
    + " blitz=" + (ai != null && ai.FazHandoffEmProfundidade));
for (int i = 0; i < mc.SlotCount; i++)
{
    var s = PlayerSlotId.FromIndex(i);
    sb.AppendLine("slot" + i + " cor=" + mc.GetTeamIdForSlot(i) + " IA=" + mc.IsPlayerAI(s)
        + " dinheiro=" + mc.GetActualMoney(s) + " rebelde=" + mc.IsSlotRebel(s));
}
sb.AppendLine("-- unidades");
foreach (var u in UnitManager.AllActive)
{
    if (u == null || u.IsDead) continue;
    bool minha = u.SlotIndex == meuSlot;
    if (!minha && !mc.IsUnitVisibleForSlot(u, meu)) continue;
    var c = u.CurrentCellPosition;
    sb.AppendLine((minha ? "MINHA " : "INIM  ") + u.name + " id=" + u.InstanceId + " @" + c.x + "," + c.y
        + " hp=" + u.CurrentHP + " fuel=" + u.CurrentFuel + " mp=" + u.RemainingMovementPoints
        + " agiu=" + u.HasActed + (u.IsEmbarked ? " EMBARCADA" : ""));
}
sb.AppendLine("-- construcoes");
foreach (var k in UnityEngine.Object.FindObjectsByType<ConstructionManager>())
{
    var c = k.CurrentCellPosition;
    sb.AppendLine(k.name + " @" + c.x + "," + c.y + " slot=" + k.SlotIndex + " cap=" + k.CurrentCapturePoints
        + "/" + k.CapturePointsMax + (k.IsPlayerHeadQuarter ? " QG" : "") + (k.IsVisible ? "" : " oculta"));
}
return sb.ToString();
