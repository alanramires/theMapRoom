// Only the stored explanation for the currently selected replay action. No new decision.
var replay = UnityEngine.Object.FindAnyObjectByType<ReplayManager>();
return replay != null ? replay.GetCurrentAIReviewJson() : "ReplayManager nao encontrado.";
