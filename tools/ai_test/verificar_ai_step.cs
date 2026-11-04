using System.Reflection;
using System.Collections;
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
if (UnityEngine.Application.isPlaying) return "SKIP: teste exige Edit Mode para preservar partida.";
if (ai == null) return "FAIL: AIController ausente.";
var type = typeof(AIController);
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
string[] fields = { "aiStep", "aiReviewVisible", "aiReviewApproved", "pendingAIReview", "debugStepRequest", "aiReviewSequence", "matchController", "turnStateManager", "replayManager" };
var saved = new System.Collections.Generic.Dictionary<string, object>();
foreach (string field in fields) saved[field] = type.GetField(field, flags).GetValue(ai);
var history = (System.Collections.Generic.List<AIController.AIReviewRecord>)type.GetField("aiReviewHistory", flags).GetValue(ai);
int historyCount = history.Count;
var mc = UnityEngine.Object.FindAnyObjectByType<MatchController>();
if (mc == null || historyCount != 0) return "SKIP: teste requer cena com MatchController e historico vazio.";
var rm = UnityEngine.Object.FindAnyObjectByType<ReplayManager>();
var tm = UnityEngine.Object.FindAnyObjectByType<TurnStateManager>();
int money = mc.GetActualMoney(mc.ActiveSlotId);
int revision = ThreatRevisionTracker.GlobalBoardRevision;
var result = new System.Text.StringBuilder();
try
{
    type.GetField("matchController", flags).SetValue(ai, mc);
    type.GetField("replayManager", flags).SetValue(ai, rm);
    type.GetField("turnStateManager", flags).SetValue(ai, tm);
    var action = new PlayerAction { ActionType = PlayerActionType.EndTurn, DebugLabel = "TESTE SEM EXECUCAO" };
    ai.SetAIStepEnabled(false);
    var disabled = (IEnumerator)type.GetMethod("WaitForAIReview", flags).Invoke(ai, new object[] { action });
    if (disabled.MoveNext()) throw new System.Exception("Desligado nao deve pausar.");
    result.AppendLine("PASS modo desligado nao cria revisao");
    ai.SetAIStepEnabled(true);
    var routine = (IEnumerator)type.GetMethod("WaitForAIReview", flags).Invoke(ai, new object[] { action });
    if (!routine.MoveNext() || !ai.HasPendingAIReview) throw new System.Exception("Revisao nao aguardou aprovacao.");
    var gate = routine.Current as UnityEngine.CustomYieldInstruction;
    if (gate == null || !gate.keepWaiting) throw new System.Exception("Coroutine nao ficou bloqueada antes da aprovacao.");
    string json = ai.GetAIReviewJson();
    if (!json.Contains("pendente") || !json.Contains("TESTE SEM EXECUCAO")) throw new System.Exception("JSON incompleto.");
    result.AppendLine("PASS decisao pendente e exportacao JSON");
    var record = history[history.Count - 1];
    record.boardRevision++;
    if (ai.ApproveAIReview()) throw new System.Exception("Aceitou revisao obsoleta.");
    record.boardRevision = revision;
    result.AppendLine("PASS bloqueia aprovacao com tabuleiro alterado");
    record.result = null;
    if (!ai.ApproveAIReview()) throw new System.Exception("Recusou aprovacao no estado original.");
    if (gate.keepWaiting) throw new System.Exception("Aprovacao nao liberou a espera.");
    if (revision != ThreatRevisionTracker.GlobalBoardRevision || money != mc.GetActualMoney(mc.ActiveSlotId)
        || (rm != null && rm.IsStepExecutionBusy)) throw new System.Exception("Aprovacao executou ou alterou o tabuleiro.");
    result.AppendLine("PASS aprovacao so sinaliza, sem executar ou gastar recursos");
    type.GetMethod("CompleteAIReview", flags).Invoke(ai, new object[] { true });
    if (ai.HasPendingAIReview || record.status != "executada" || !ai.GetAIReviewJson().Contains("sucesso=True")) throw new System.Exception("Resultado nao registrado.");
    result.AppendLine("PASS historico registra resultado do automata");
    routine = (IEnumerator)type.GetMethod("WaitForAIReview", flags).Invoke(ai, new object[] { action });
    routine.MoveNext();
    type.GetMethod("InvalidateAIReview", flags).Invoke(ai, null);
    if (ai.HasPendingAIReview || ai.ApproveAIReview()) throw new System.Exception("Revisao invalidada ainda aprovavel.");
    result.AppendLine("PASS contexto invalidado nao pode ser reaprovado");
}
finally
{
    history.RemoveRange(historyCount, history.Count - historyCount);
    foreach (var entry in saved) type.GetField(entry.Key, flags).SetValue(ai, entry.Value);
}
return result.ToString();
