using System.Reflection;
if (UnityEngine.Application.isPlaying) return "SKIP: execute fora de Play.";
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
var debug = UnityEngine.Object.FindAnyObjectByType<DebugManager>();
if (ai == null || debug == null) return "SKIP: AIController/DebugManager ausente.";
var instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
var staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
var request = typeof(DebugManager).GetMethod("HandleAIDebugShortcutRequest", instanceFlags);
if (request == null) return "WAIT: correcao ainda nao importada.";
var saved = new System.Collections.Generic.Dictionary<string, object>();
foreach (string name in new[] { "isDebugPaused", "aiStep", "aiReviewNoteFocused", "aiReviewVisible", "debugStepRequest", "debugStepPendingAction" })
    saved[name] = typeof(AIController).GetField(name, instanceFlags).GetValue(ai);
var singleton = typeof(AIController).GetField("_instance", staticFlags);
var pausedStatic = typeof(AIController).GetField("<IsDebugPaused>k__BackingField", staticFlags);
var textFocus = typeof(UiInputBlocker).GetField("explicitTextInputFocused", staticFlags);
var suppress = typeof(UiInputBlocker).GetField("suppressUntilFrame", staticFlags);
object oldSingleton = singleton.GetValue(null), oldPaused = pausedStatic.GetValue(null), oldFocus = textFocus.GetValue(null), oldSuppress = suppress.GetValue(null);
try
{
    singleton.SetValue(null, ai);
    textFocus.SetValue(null, true);
    ai.SetDebugPaused(false);
    request.Invoke(debug, new object[] { true, false, false });
    if (!AIController.IsDebugPaused) throw new System.Exception("F10 foi bloqueado pelo console.");
    request.Invoke(debug, new object[] { false, false, true });
    if (!AIController.IsDebugPaused) throw new System.Exception("F12 retomou enquanto digitava.");
    object beforeStep = typeof(AIController).GetField("debugStepRequest", instanceFlags).GetValue(ai);
    request.Invoke(debug, new object[] { false, true, false });
    if (!beforeStep.Equals(typeof(AIController).GetField("debugStepRequest", instanceFlags).GetValue(ai))) throw new System.Exception("F11 avancou enquanto digitava.");
    ai.SetDebugPaused(false);
    request.Invoke(debug, new object[] { true, true, true });
    if (!AIController.IsDebugPaused) throw new System.Exception("F10 nao teve prioridade sobre retomar.");
    textFocus.SetValue(null, false); suppress.SetValue(null, -1);
    ai.SetAIStepEnabled(true);
    typeof(AIController).GetField("aiReviewNoteFocused", instanceFlags).SetValue(ai, true);
    if (!UiInputBlocker.IsTextInputFocused()) throw new System.Exception("Foco da nota nao foi simulado.");
    ai.SetDebugPaused(false);
    request.Invoke(debug, new object[] { true, false, false });
    if (!AIController.IsDebugPaused) throw new System.Exception("F10 foi bloqueado pela nota.");
    ai.SetAIStepEnabled(false);
    if ((bool)typeof(AIController).GetField("aiReviewNoteFocused", instanceFlags).GetValue(ai)) throw new System.Exception("Continuar deixou foco residual.");
}
finally
{
    foreach (var item in saved) typeof(AIController).GetField(item.Key, instanceFlags).SetValue(ai, item.Value);
    singleton.SetValue(null, oldSingleton); pausedStatic.SetValue(null, oldPaused);
    textFocus.SetValue(null, oldFocus); suppress.SetValue(null, oldSuppress);
}
return "PASS: F10 pausa com console e nota focados, vence teclas simultaneas; F11/F12 continuam bloqueados na digitacao; Continuar libera foco residual. Nenhuma jogada executada.";
