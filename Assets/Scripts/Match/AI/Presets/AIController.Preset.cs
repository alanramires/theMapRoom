using UnityEngine;

// =====================================================================================
// Ponte AIController <-> AIPresetData.
//
// FASE 1: o preset é RESOLVIDO e fica disponível para inspeção, mas nenhuma decisão lê
// dele. Os 4 booleanos legados continuam sendo a fonte de verdade do runtime. Isso é
// intencional: a fase 1 não pode mudar comportamento, senão fica impossível saber se uma
// regressão veio do refactor ou de um valor que mudou junto.
//
// Quando a fase 2 começar, os getters de valor do AIController (EliteRatioPressure,
// CoreMinInfantry, ...) passam a ler daqui em vez do ternário hardMode ? a : b.
// =====================================================================================
public partial class AIController
{
    [Header("Perfil de IA (Preset)")]
    [Tooltip("Biblioteca que resolve qual AIPresetData vale para a dificuldade escolhida. FASE 1: apenas inspeção — nenhuma decisão lê deste asset ainda.")]
    [SerializeField] private AIPresetLibrary presetLibrary;

    [System.NonSerialized] private AIPresetData activePreset;
    [System.NonSerialized] private AIDifficulty appliedDifficulty = AIDifficulty.Facil;
    [System.NonSerialized] private bool hasAppliedDifficulty;

    public AIPresetLibrary PresetLibrary => presetLibrary;

    /// <summary>Preset resolvido para a dificuldade atual. Pode ser null se a biblioteca não estiver ligada na cena.</summary>
    public AIPresetData ActivePreset => activePreset;

    /// <summary>Dificuldade efetivamente aplicada nesta partida. Antes de ApplyDifficulty, reflete os booleanos serializados na cena.</summary>
    public AIDifficulty AppliedDifficulty => hasAppliedDifficulty ? appliedDifficulty : InferDifficultyFromFlags();

    /// <summary>
    /// Preset de um time específico. Na fase 1 devolve o mesmo preset para todos os times —
    /// o parâmetro existe para que os call sites já nasçam corretos e a migração para
    /// doutrina por time (generais) não precise revisitar tudo de novo.
    /// </summary>
    public AIPresetData ResolvePresetForTeam(TeamId team)
    {
        if (presetLibrary == null)
            return activePreset;
        return presetLibrary.Resolve(AppliedDifficulty, team);
    }

    private void ResolveActivePreset(AIDifficulty difficulty)
    {
        appliedDifficulty = difficulty;
        hasAppliedDifficulty = true;
        activePreset = presetLibrary != null ? presetLibrary.Resolve(difficulty) : null;

        if (showAILogs)
        {
            string presetName = activePreset != null ? activePreset.name : "<nenhum>";
            Debug.Log($"[AI][Preset] dificuldade={difficulty} preset={presetName} " +
                      $"(fase 1: preset resolvido para inspeção; runtime ainda usa os flags)");
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
