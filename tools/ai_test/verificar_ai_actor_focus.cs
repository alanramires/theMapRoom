var idle = new PlayerAction { ActionType = PlayerActionType.UnitAction, UnitInstanceId = "unit-test",
    SensorAction = SensorActionType.None, HasMoveFrom = true, HasMoveTo = true,
    MoveFrom = UnityEngine.Vector3Int.zero, MoveTo = UnityEngine.Vector3Int.zero };
if (!AIController.TryGetAIReviewActorCell(idle, out var cell) || cell != UnityEngine.Vector3Int.zero)
    throw new System.Exception("Unidade parada em (0,0) nao identificada.");
idle.MoveFrom = new UnityEngine.Vector3Int(2, 3, 0); idle.MoveTo = new UnityEngine.Vector3Int(8, 9, 0);
if (!AIController.TryGetAIReviewActorCell(idle, out cell) || cell != idle.MoveFrom)
    throw new System.Exception("Ator deve ser destacado na origem, nao no destino.");
idle.SensorAction = SensorActionType.Capture; idle.MoveTo = idle.MoveFrom;
if (!AIController.TryGetAIReviewActorCell(idle, out cell) || cell != idle.MoveFrom)
    throw new System.Exception("Captura parada perdeu identificacao da unidade.");
if (AIController.TryGetAIReviewActorCell(new PlayerAction { ActionType = PlayerActionType.Shopping, HasCursorHex = true }, out _))
    throw new System.Exception("Compra nao deve inventar unidade para focar.");
if (AIController.TryGetAIReviewActorCell(new PlayerAction { ActionType = PlayerActionType.EndTurn }, out _))
    throw new System.Exception("Passar turno nao deve inventar unidade para focar.");
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
if (ai != null && !ai.HasPendingAIReview && ai.FocusAIReviewActor())
    throw new System.Exception("Foco deve recusar sem decisao pendente.");
return "PASS: unidade parada, origem de movimento, captura no lugar e ausencia de ator em compra/fim de turno. Nenhuma jogada executada.";
