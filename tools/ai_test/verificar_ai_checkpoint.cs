using System.Reflection;
if (UnityEngine.Application.isPlaying) return "SKIP: teste requer Edit Mode.";
var saves = UnityEngine.Object.FindAnyObjectByType<SaveGameManager>();
if (saves == null) return "SKIP: SaveGameManager ausente.";
var field = typeof(SaveGameManager).GetField("aiReviewCheckpoints", BindingFlags.Instance | BindingFlags.NonPublic);
var checkpoints = (System.Collections.Generic.List<SaveGameManager.AIReviewSaveReference>)field.GetValue(saves);
var previous = new System.Collections.Generic.List<SaveGameManager.AIReviewSaveReference>(checkpoints);
string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ai-step-checkpoints-" + System.Guid.NewGuid().ToString("N"));
System.IO.Directory.CreateDirectory(directory);
SaveGameManager.AIReviewSaveReference Reference(int index, int slot)
{
    string path = System.IO.Path.Combine(directory, "slot" + slot + ".test");
    System.IO.File.WriteAllText(path, "synthetic checkpoint " + slot);
    var info = new System.IO.FileInfo(path);
    return new SaveGameManager.AIReviewSaveReference { found = true, path = path, saveSlot = slot,
        replayActionCount = index, fileLength = info.Length, fileWriteUtcTicks = info.LastWriteTimeUtc.Ticks };
}
try
{
    checkpoints.Clear();
    var earlier = Reference(3, 1);
    var later = Reference(7, 2);
    var closer = Reference(4, 3);
    checkpoints.Add(earlier); checkpoints.Add(later);
    var nearest = saves.GetClosestAIReviewSave(5);
    if (nearest.saveSlot != 1 || nearest.relation != "before_step" || nearest.replayActionsToStep != 2) throw new System.Exception("Nao preferiu anterior.");
    checkpoints.Add(closer);
    if (saves.GetClosestAIReviewSave(5).saveSlot != 3) throw new System.Exception("Nao escolheu predecessor mais proximo.");
    if (saves.GetClosestAIReviewSave(7).relation != "same_replay_boundary") throw new System.Exception("Fronteira identica incorreta.");
    System.IO.File.AppendAllText(later.path, "overwritten");
    if (saves.GetClosestAIReviewSave(8).saveSlot != 3) throw new System.Exception("Aceitou arquivo sobrescrito.");
    if (saves.GetClosestAIReviewSave(1).relation != "after_step") throw new System.Exception("Save posterior mal identificado.");
    checkpoints.Clear();
    if (saves.GetClosestAIReviewSave(5).found) throw new System.Exception("Inventou um save inexistente.");
}
finally
{
    checkpoints.Clear(); checkpoints.AddRange(previous);
    foreach (int slot in new[] { 1, 2, 3 })
    {
        string path = System.IO.Path.Combine(directory, "slot" + slot + ".test");
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
    System.IO.Directory.Delete(directory);
}
return "PASS: predecessor, proximidade, fronteira identica, arquivo sobrescrito, save posterior e ausencia de save. Nenhum save real alterado.";
