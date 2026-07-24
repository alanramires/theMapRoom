using UnityEngine;

// =====================================================================================
// Ponte AIController <-> AIPresetData.
//
// Modelo: UM baseline (editável como um UnitData) + a dificuldade como atalho que liga
// toggles por cima de uma CÓPIA do baseline. Não há asset por dificuldade.
//
// FASE 1: o preset ativo é RESOLVIDO e fica disponível para inspeção, mas nenhuma decisão
// lê dele. Os 4 booleanos legados continuam sendo a fonte de verdade do runtime — assim a
// fase 1 não muda comportamento, e uma regressão futura é rastreável ao passo que a causou.
//
// Fase 2: os getters de valor do AIController passam a ler de ActivePreset, com fallback
// para o campo da cena quando basePreset for null (cenas antigas continuam idênticas).
// =====================================================================================
public partial class AIController
{
    [Header("Perfil de IA (Preset)")]
    [Tooltip("Baseline único da doutrina da IA (toggles + valores). A dificuldade liga toggles por cima. FASE 1: só inspeção — nenhuma decisão lê deste asset ainda.")]
    [SerializeField] private AIPresetData basePreset;

    [System.NonSerialized] private AIPresetData activePreset;
    [System.NonSerialized] private AIDifficulty appliedDifficulty = AIDifficulty.Facil;
    [System.NonSerialized] private bool hasAppliedDifficulty;

    public AIPresetData BasePreset => basePreset;

    /// <summary>Preset já com a overlay da dificuldade aplicada. Cópia de runtime; nunca é o asset. Null se basePreset não estiver ligado na cena.</summary>
    public AIPresetData ActivePreset => activePreset;

    /// <summary>Dificuldade efetivamente aplicada. Antes de ApplyDifficulty, é reconstruída dos booleanos serializados na cena.</summary>
    public AIDifficulty AppliedDifficulty => hasAppliedDifficulty ? appliedDifficulty : InferDifficultyFromFlags();

    private void ResolveActivePreset(AIDifficulty difficulty)
    {
        appliedDifficulty = difficulty;
        hasAppliedDifficulty = true;

        if (basePreset == null)
        {
            activePreset = null;
            return;
        }

        activePreset = basePreset.CloneRuntime();
        AIPresetData.ApplyDifficultyOverlay(activePreset, difficulty);

        if (showAILogs)
        {
            Debug.Log($"[AI][Preset] baseline={basePreset.name} dificuldade={difficulty} " +
                      $"→ preset ativo montado (fase 1: só inspeção; runtime ainda usa os flags)");
        }
    }

    /// <summary>
    /// Reconstrói a dificuldade a partir dos 4 booleanos. Necessário porque a dificuldade
    /// escolhida na Tela de Entrada não existe como campo — só como combinação de flags —
    /// e cenas abertas direto no Editor nunca passam por ApplyDifficulty.
    /// </summary>
    private AIDifficulty InferDifficultyFromFlags()
    {
        if (hardMode && conscriptionDoctrine) return AIDifficulty.Agressiva;
        if (hardMode) return AIDifficulty.Competitiva;
        if (conscriptionDoctrine) return AIDifficulty.Formigueiro;
        if (conscriptionWhenLosing) return AIDifficulty.Medio;
        if (easyMode) return AIDifficulty.Iniciante;
        return AIDifficulty.Facil;
    }
}
