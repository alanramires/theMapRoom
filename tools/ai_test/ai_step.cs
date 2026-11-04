// Consulta a revisao e as ultimas 100 decisoes. Nao decide novamente nem executa.
// unity command script eval --file tools/ai_test/ai_step.cs
var ai = UnityEngine.Object.FindAnyObjectByType<AIController>();
return ai != null ? ai.GetAIReviewIndexJson() : "AIController nao encontrado.";
