using System.Reflection;
var method = typeof(AIController).GetMethod("BuildAIReviewCandidateVisualPairs", BindingFlags.Public | BindingFlags.Static);
if (method == null) return "WAIT: nova versao ainda nao importada. Nao recompilar em Play.";
var origin = new UnityEngine.Vector3Int(1, 1, 0);
var firingCell = new UnityEngine.Vector3Int(2, 1, 0);
var frozenEnemy = new UnityEngine.Vector3Int(99, 99, 0);
var action = new PlayerAction { ActionType = PlayerActionType.UnitAction, SensorAction = SensorActionType.Attack,
    HasMoveFrom = true, MoveFrom = origin, HasMoveTo = true, MoveTo = new UnityEngine.Vector3Int(3, 1, 0),
    HasTargetHex = true, TargetHex = new UnityEngine.Vector3Int(6, 1, 0), TargetInstanceId = "12" };
var record = new AIController.AIReviewRecord { actionJson = UnityEngine.JsonUtility.ToJson(action), stateBeforeDecision = new AIController.AIReviewState() };
record.stateBeforeDecision.units.Add(new AIController.AIReviewUnitState { id = 13, cell = frozenEnemy });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Attack", cell = firingCell, targetId = "13", allowed = true });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Capture", cell = new UnityEngine.Vector3Int(4, 2, 0), allowed = true });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Embark", evaluated = false, gate = "capture_before_embark", reason = "not evaluated" });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Capture", cell = new UnityEngine.Vector3Int(5, 2, 0), allowed = false, gate = "capture_sensor_rejected" });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Attack", cell = firingCell, targetId = "missing", allowed = true });
record.candidates.Add(new AIController.AIReviewCandidate { action = "Attack", selected = true, allowed = true });
System.Collections.Generic.List<AIController.AIReviewVisualPair> Build(int index, out string message)
{
    object[] args = { record, index, null };
    var pairs = (System.Collections.Generic.List<AIController.AIReviewVisualPair>)method.Invoke(null, args);
    message = (string)args[2];
    return pairs;
}
string jsonBefore = UnityEngine.JsonUtility.ToJson(record);
var attack = Build(0, out _);
if (attack.Count != 2 || attack[0].From != origin || attack[0].To != firingCell || attack[1].From != firingCell || attack[1].To != frozenEnemy)
    throw new System.Exception("Alternativa de ataque nao usou o alvo congelado.");
var capture = Build(1, out _);
if (capture.Count != 2 || capture[1].From != capture[1].To) throw new System.Exception("Captura sem marcador no destino.");
if (Build(2, out string skipped).Count != 0 || !skipped.Contains("nao foi avaliado")) throw new System.Exception("Inventou geometria de ramo nao avaliado.");
if (Build(3, out string blocked).Count == 0 || !blocked.Contains("recusada")) throw new System.Exception("Gate recusado nao explicado.");
if (Build(4, out _).Count != 0) throw new System.Exception("Alvo ausente nao deve consultar unidade viva.");
var chosen = Build(5, out _);
if (chosen.Count != 2 || chosen[1].To != action.TargetHex) throw new System.Exception("Previa escolhida diferente do comando original.");
if (jsonBefore != UnityEngine.JsonUtility.ToJson(record)) throw new System.Exception("Visualizacao alterou o registro.");
return "PASS: ataque congelado, captura, ramo nao avaliado, gate recusado, alvo ausente, decisao original e registro imutavel. Nenhuma jogada executada.";
