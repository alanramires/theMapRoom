using System.Reflection;
if (UnityEngine.Application.isPlaying) return "SKIP: execute fora de Play.";
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
var turn = UnityEngine.Object.FindAnyObjectByType<TurnStateManager>();
var menu = UnityEngine.Object.FindAnyObjectByType<BattleMapMenuRootController>(UnityEngine.FindObjectsInactive.Include);
if (ai == null || turn == null || menu == null) return "SKIP: abra a cena Batalha.";
var instanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
var staticFlags = BindingFlags.NonPublic | BindingFlags.Static;
var instanceField = typeof(AIController).GetField("_instance", staticFlags);
object previousInstance = instanceField.GetValue(null);
var saved = new System.Collections.Generic.Dictionary<string, object>();
foreach (string name in new[] { "aiReviewNoteFocused", "aiReviewVisible", "aiStep" })
    saved[name] = typeof(AIController).GetField(name, instanceFlags).GetValue(ai);
var explicitFocus = typeof(UiInputBlocker).GetField("explicitTextInputFocused", staticFlags);
var suppressFrame = typeof(UiInputBlocker).GetField("suppressUntilFrame", staticFlags);
object oldExplicit = explicitFocus.GetValue(null), oldSuppress = suppressFrame.GetValue(null);
try
{
    instanceField.SetValue(null, ai);
    explicitFocus.SetValue(null, false); suppressFrame.SetValue(null, -1);
    foreach (string name in saved.Keys) typeof(AIController).GetField(name, instanceFlags).SetValue(ai, true);
    if (!ai.IsAIReviewNoteEditing || !UiInputBlocker.IsTextInputFocused()) throw new System.Exception("Nota nao bloqueou input global.");
    bool beforeMenu = menu.IsMenuOpen;
    var beforeState = turn.CurrentCursorState;
    var beforeStep = typeof(TurnStateManager).GetField("scannerPromptStep", instanceFlags).GetValue(turn);
    if (menu.TryToggleMenuFromShortcut()) throw new System.Exception("Atalho de menu aceito durante nota.");
    if (menu.TryHandleMenuInput(null, turn)) throw new System.Exception("Menu processou teclado durante nota.");
    typeof(TurnStateManager).GetMethod("ProcessScannerPromptInput", instanceFlags).Invoke(turn, null);
    typeof(TurnStateManager).GetMethod("ProcessConstructionShoppingInput", instanceFlags).Invoke(turn, null);
    var debug = UnityEngine.Object.FindAnyObjectByType<DebugManager>();
    if (debug != null) typeof(DebugManager).GetMethod("HandleAIDebugShortcuts", instanceFlags).Invoke(debug, null);
    var camera = UnityEngine.Object.FindAnyObjectByType<CameraController>();
    if (camera != null) typeof(CameraController).GetMethod("Update", instanceFlags).Invoke(camera, null);
    if (menu.IsMenuOpen != beforeMenu || turn.CurrentCursorState != beforeState
        || !beforeStep.Equals(typeof(TurnStateManager).GetField("scannerPromptStep", instanceFlags).GetValue(turn)))
        throw new System.Exception("Estado do jogo alterado por handlers durante nota.");
    typeof(AIController).GetField("aiReviewNoteFocused", instanceFlags).SetValue(ai, false);
    if (ai.IsAIReviewNoteEditing) throw new System.Exception("Foco da nota nao foi liberado.");
}
finally
{
    foreach (var entry in saved) typeof(AIController).GetField(entry.Key, instanceFlags).SetValue(ai, entry.Value);
    instanceField.SetValue(null, previousInstance);
    explicitFocus.SetValue(null, oldExplicit); suppressFrame.SetValue(null, oldSuppress);
}
return "PASS: foco da nota bloqueia input global, menu, scanner, compra, debug e camera; estado preservado e foco liberavel. Nenhuma jogada executada.";
