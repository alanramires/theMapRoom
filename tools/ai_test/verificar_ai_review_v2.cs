using System.Reflection;
using System.Collections;
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
if (UnityEngine.Application.isPlaying) return "SKIP: saia do Play antes de rodar os testes de arquivo e replay.";
if (ai == null) return "SKIP: abra Batalha para encontrar AIController.";
var type = typeof(AIController);
if (type.GetMethod("MarkAIReviewStrange") == null) return "WAIT: importe os scripts novos antes de testar.";
var flags = BindingFlags.NonPublic | BindingFlags.Instance;
string[] names = { "aiStep", "aiReviewCapturing", "aiReviewCandidateCount", "pendingAIReview", "aiReviewSession", "aiReviewFileMessage" };
var saved = new System.Collections.Generic.Dictionary<string, object>();
foreach (var name in names) saved[name] = type.GetField(name, flags).GetValue(ai);
var items = (System.Collections.Generic.List<AIController.AIReviewCandidate>)type.GetField("aiReviewCandidates", flags).GetValue(ai);
var previousItems = new System.Collections.Generic.List<AIController.AIReviewCandidate>(items);
string session = "test-" + System.Guid.NewGuid().ToString("N");
string directory = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "AIReviews", session);
var report = new System.Text.StringBuilder();
try
{
    type.GetField("aiStep", flags).SetValue(ai, true);
    type.GetField("aiReviewCapturing", flags).SetValue(ai, true);
    items.Clear();
    for (int i = 1; i <= 4; i++)
        ai.TraceAIReviewCandidate("Capture", "Capture", new UnityEngine.Vector3Int(i, 2, 0), "building" + i,
            true, "scored", "test", hasScore: true, score: i * 10, scoreScope: "test_capture");
    ai.TraceAIReviewCandidate("Priority", "Embark", UnityEngine.Vector3Int.zero, null, false,
        "capture_before_embark", "not evaluated", evaluated: false);
    if (items.Count != 4 || items.Exists(x => x.hasScore && x.score == 10)) throw new System.Exception("Top 3 incorreto.");
    var action = new PlayerAction { ActionType = PlayerActionType.UnitAction, SensorAction = SensorActionType.Capture,
        HasMoveTo = true, MoveTo = new UnityEngine.Vector3Int(4, 2, 0) };
    var candidates = (System.Collections.Generic.List<AIController.AIReviewCandidate>)type.GetMethod("FinalizeAIReviewCandidates", flags).Invoke(ai, new object[] { action });
    if (candidates.FindAll(x => x.selected).Count != 1 || candidates.Find(x => x.selected).score != 40
        || !candidates.Exists(x => x.gate == "lower_score_in_same_scope")
        || !candidates.Exists(x => !x.evaluated && !x.hasScore)) throw new System.Exception("Ranking/gates incorretos.");
    report.AppendLine("PASS top 3, escolhido, descartados e branch nao avaliado sem pontuacao inventada");
    items.Clear();
    var origin = new UnityEngine.Vector3Int(2, 2, 0);
    ai.TraceAIReviewCandidate("Attack", "Attack", origin, "enemy-A", true, "allowed", "A");
    ai.TraceAIReviewCandidate("Attack", "Attack", origin, "enemy-B", true, "allowed", "B");
    action.SensorAction = SensorActionType.Attack; action.MoveTo = origin; action.TargetInstanceId = "enemy-B";
    candidates = (System.Collections.Generic.List<AIController.AIReviewCandidate>)type.GetMethod("FinalizeAIReviewCandidates", flags).Invoke(ai, new object[] { action });
    if (candidates.FindAll(x => x.selected).Count != 1 || candidates.Find(x => x.selected).targetId != "enemy-B") throw new System.Exception("Ataque selecionou alvo errado.");
    report.AppendLine("PASS ataques da mesma celula mantem os alvos separados");
    string normalized = (string)type.GetMethod("NormalizeAIReviewCoordinates", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "(5, 13, 0) (4, 14)" });
    if (normalized != "(5,13) (4,14)") throw new System.Exception("Coordenadas inconsistentes.");
    report.AppendLine("PASS coordenadas padronizadas");
    var record = new AIController.AIReviewRecord
    {
        schemaVersion = 2, sessionId = session, id = 901, turn = 7, slot = 0, status = "pendente",
        decision = "synthetic test", candidates = candidates,
        stateBeforeDecision = new AIController.AIReviewState { turn = 7, slot = 0 }
    };
    record.stateBeforeDecision.units.Add(new AIController.AIReviewUnitState { id = 88, hp = 7 });
    type.GetField("pendingAIReview", flags).SetValue(ai, record);
    record.markedStrange = true;
    record.markNote = AIController.NormalizeAIReviewNote("por que nao embarcou?\r\nnota");
    if (record.markNote.Contains("\n") || record.markNote.Contains("\r")) throw new System.Exception("Nota deixou mais de uma linha.");
    if (!(bool)type.GetMethod("PersistAIReviewRecord", flags).Invoke(ai, new object[] { record })) throw new System.Exception("Falha ao persistir.");
    string marked = System.IO.Path.Combine(directory, "step-000901.json");
    var disk = UnityEngine.JsonUtility.FromJson<AIController.AIReviewRecord>(System.IO.File.ReadAllText(marked));
    if (!disk.markedStrange || disk.stateBeforeDecision.units[0].hp != 7 || disk.candidates.Count != candidates.Count) throw new System.Exception("Registro marcado perdeu snapshot/candidatos.");
    record.status = "executada";
    type.GetMethod("PersistAIReviewRecord", flags).Invoke(ai, new object[] { record });
    disk = UnityEngine.JsonUtility.FromJson<AIController.AIReviewRecord>(System.IO.File.ReadAllText(marked));
    if (disk.status != "executada" || disk.stateBeforeDecision.units[0].hp != 7) throw new System.Exception("Conclusao alterou foto anterior.");
    report.AppendLine("PASS arquivo marcado e atualizacao de resultado preservam a foto anterior");
    string marksFile = System.IO.Path.Combine(directory, "test-marks.jsonl");
    var append = type.GetMethod("WriteAIReviewMarkLine", BindingFlags.Static | BindingFlags.NonPublic);
    append.Invoke(null, new object[] { marksFile, new AIController.AIReviewMark { markId = "first", markedAtUtc = "test", step = record } });
    record.markNote = "segunda marca";
    append.Invoke(null, new object[] { marksFile, new AIController.AIReviewMark { markId = "second", markedAtUtc = "test", step = record } });
    var lines = System.IO.File.ReadAllLines(marksFile);
    if (lines.Length != 2) throw new System.Exception("JSONL nao acumulou uma linha por marca.");
    var firstMark = UnityEngine.JsonUtility.FromJson<AIController.AIReviewMark>(lines[0]);
    var secondMark = UnityEngine.JsonUtility.FromJson<AIController.AIReviewMark>(lines[1]);
    if (firstMark.step.markNote == secondMark.step.markNote || firstMark.step.stateBeforeDecision.units[0].hp != 7)
        throw new System.Exception("Marca antiga foi alterada ou perdeu snapshot.");
    report.AppendLine("PASS nota de uma linha e JSONL acumulado preservam as marcas anteriores");
    action.AIReview = record;
    var clone = (PlayerAction)typeof(ReplayManager).GetMethod("CloneActionForCompactSave", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { action });
    var loaded = UnityEngine.JsonUtility.FromJson<PlayerAction>(UnityEngine.JsonUtility.ToJson(clone));
    var explanation = UnityEngine.JsonUtility.FromJson<AIController.AIReviewRecord>(loaded.AIReviewJson);
    if (explanation.id != 901 || !explanation.markedStrange || explanation.stateBeforeDecision.units[0].hp != 7) throw new System.Exception("Replay perdeu diagnostico.");
    var empty = (PlayerAction)typeof(ReplayManager).GetMethod("CloneActionForCompactSave", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { new PlayerAction() });
    if (!string.IsNullOrEmpty(empty.AIReviewJson)) throw new System.Exception("Acao sem diagnostico ganhou payload.");
    report.AppendLine("PASS roundtrip do replay preserva diagnostico e nao cria payload nas outras acoes");
}
finally
{
    foreach (var pair in saved) type.GetField(pair.Key, flags).SetValue(ai, pair.Value);
    items.Clear(); items.AddRange(previousItems);
    // Only this test's named files; no recursive deletion, no user recordings touched.
    foreach (var file in new[] { "step-000901.json", "step-000901.json.tmp", "test-marks.jsonl" })
    {
        string path = System.IO.Path.Combine(directory, file);
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }
    string markedDirectory = System.IO.Path.Combine(directory, "marked");
    if (System.IO.Directory.Exists(markedDirectory)) System.IO.Directory.Delete(markedDirectory);
    if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory);
}
return report.ToString();
