if (UnityEngine.Application.isPlaying) throw new System.Exception("Execute fora do Play.");
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
if (ai == null) throw new System.Exception("AIController ausente.");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var type = typeof(AIController);
var enabled = type.GetField("aiStep", flags);
var capturing = type.GetField("aiReviewCapturing", flags);
var traces = (System.Collections.IList)type.GetField("aiReviewParticipantTraces", flags).GetValue(ai);
var oldEnabled = enabled.GetValue(ai); var oldCapturing = capturing.GetValue(ai);
var oldTraces = new object[traces.Count]; traces.CopyTo(oldTraces, 0);
try {
    enabled.SetValue(ai, true); capturing.SetValue(ai, true); traces.Clear();
    var action = new PlayerAction { ActionType = PlayerActionType.UnitAction, UnitInstanceId = "900001" };
    var other = new PlayerAction { ActionType = PlayerActionType.UnitAction, UnitInstanceId = "900001" };
    var trace = type.GetMethod("TraceAIReviewParticipant", flags);
    trace.Invoke(ai, new object[] { action, 900002, "Substituto", "test", new UnityEngine.Vector3Int(2,3,0) });
    trace.Invoke(ai, new object[] { other, 900003, "Descartado", "test", null });
    trace.Invoke(ai, new object[] { action, 900099, "Ausente", "test", null });
    var state = new AIController.AIReviewState();
    state.units.Add(new AIController.AIReviewUnitState { id = 900001, name = "Ator", cell = new UnityEngine.Vector3Int(1,1,0) });
    state.units.Add(new AIController.AIReviewUnitState { id = 900002, name = "Parceiro", cell = new UnityEngine.Vector3Int(8,8,0), transportTargetId = 900001 });
    state.units.Add(new AIController.AIReviewUnitState { id = 900003, name = "Descartado" });
    var record = new AIController.AIReviewRecord { stateBeforeExecution = state };
    record.participants = (System.Collections.Generic.List<AIController.AIReviewParticipant>)type.GetMethod("BuildAIReviewParticipants", flags).Invoke(ai, new object[] { record, action });
    if (record.participants.Count != 1 || record.participants[0].unitId != 900002 || !record.participants[0].role.Contains("Substituto") || !record.participants[0].role.Contains("Transportador"))
        throw new System.Exception("Filtro da escolha, visibilidade ou deduplicacao incorretos.");
    var copy = UnityEngine.JsonUtility.FromJson<AIController.AIReviewRecord>(UnityEngine.JsonUtility.ToJson(record));
    var pairs = AIController.BuildAIReviewParticipantVisualPairs(copy, new UnityEngine.Vector3Int(1,1,0));
    if (pairs.Count != 2 || pairs[0].Label != "AIReviewParticipant" || pairs[0].From != new UnityEngine.Vector3Int(8,8,0) || pairs[1].Label != "AIReviewRelationship" || pairs[1].To != new UnityEngine.Vector3Int(2,3,0))
        throw new System.Exception("Marcacao congelada ou vinculo perdeu dados no JSON.");
    return "PASS: parceiros da escolha, filtro de alternativa/ausente, promessa reciproca, deduplicacao, JSON e coordenadas congeladas.";
} finally {
    enabled.SetValue(ai, oldEnabled); capturing.SetValue(ai, oldCapturing);
    traces.Clear(); foreach (var item in oldTraces) traces.Add(item);
}
