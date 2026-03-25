using UnityEngine;

public static class TutorialRules
{
    // IDs dos Tutoriais
    public const string TUTORIAL_ID_SOLDADO = "tutorial 1 - soldado";

    // Flags de estado pendente (centralizados aqui)
    private static bool pendingHpResetTutorial1;

    public static void ResetAllStates()
    {
        pendingHpResetTutorial1 = false;
    }

    /// <summary>
    /// Verifica se uma conclusão de objetivo dispara alguma regra especial.
    /// </summary>
    public static void CheckObjectiveRules(string tutorialId, string objectiveId)
    {
        if (tutorialId == TUTORIAL_ID_SOLDADO)
        {
            if (objectiveId == "ATTACK_UNIT_PLAINS" || objectiveId == "ATTACK_UNIT_MOUNTAIN")
            {
                pendingHpResetTutorial1 = true;
                Debug.Log($"[TutorialRules] Regra de Reset de HP agendada para o tutorial: {tutorialId}");
            }
        }
    }

    /// <summary>
    /// Verifica se uma mudança de turno/time ativo dispara alguma regra especial.
    /// </summary>
    public static void CheckTurnStartRules(string tutorialId, int teamId)
    {
        if (tutorialId == TUTORIAL_ID_SOLDADO)
        {
            // Início da rodada (Time 0)
            if (pendingHpResetTutorial1 && teamId == 0)
            {
                ExecuteRestoreAllUnitsHp(10, "TUTORIAL: A diferença entre terrenos foi aplicada!");
                pendingHpResetTutorial1 = false;
            }
        }
    }

    /// <summary>
    /// Regra genérica para restaurar HP de todas as unidades ativas.
    /// </summary>
    public static void ExecuteRestoreAllUnitsHp(int hpValue, string message)
    {
        Debug.Log($"[TutorialRules] Executando restauração de HP para todas as unidades (HP={hpValue}).");
        
        for (int i = UnitManager.AllActive.Count - 1; i >= 0; i--)
        {
            UnitManager unit = UnitManager.AllActive[i];
            if (unit != null && !unit.IsDead)
            {
                unit.SetCurrentHP(hpValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            PanelDialogController.TrySetTransientText(message, 3.5f);
        }
    }
}
