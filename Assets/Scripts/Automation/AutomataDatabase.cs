using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AutomataDatabase", menuName = "Game/Automation/Automata Database")]
public class AutomataDatabase : ScriptableObject
{
    [SerializeField] private List<AutomataData> entries = new List<AutomataData>();

    public bool TryResolve(UnitManager unit, TeamId activeTeam, string tutorialId, out AutomataData resolved)
    {
        resolved = null;
        if (unit == null || entries == null || entries.Count <= 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            AutomataData entry = entries[i];
            if (entry == null)
                continue;
            if (entry.teamId != activeTeam)
                continue;
            if (!TutorialMatches(entry, tutorialId))
                continue;
            if (!UnitMatchesToken(unit, entry.unitToken))
                continue;

            resolved = entry;
            return true;
        }

        return false;
    }

    private static bool TutorialMatches(AutomataData data, string tutorialId)
    {
        if (data == null)
            return false;
        if (data.tutorials == null || data.tutorials.Count <= 0)
            return true;

        string normalizedTutorial = string.IsNullOrWhiteSpace(tutorialId) ? string.Empty : tutorialId.Trim();
        for (int i = 0; i < data.tutorials.Count; i++)
        {
            TutorialData tutorial = data.tutorials[i];
            if (tutorial == null)
                continue;

            string candidateId = string.IsNullOrWhiteSpace(tutorial.id) ? tutorial.name : tutorial.id;
            if (string.Equals(candidateId.Trim(), normalizedTutorial, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool UnitMatchesToken(UnitManager unit, string token)
    {
        if (unit == null)
            return false;

        string normalized = string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
        if (normalized.Length <= 0)
            return true;

        if (!string.IsNullOrWhiteSpace(unit.UnitId) &&
            unit.UnitId.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(unit.UnitDisplayName) &&
            unit.UnitDisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(unit.name) &&
            unit.name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            return true;

        if (unit.TryGetUnitData(out UnitData unitData) && unitData != null)
        {
            if (!string.IsNullOrWhiteSpace(unitData.id) &&
                unitData.id.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(unitData.displayName) &&
                unitData.displayName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(unitData.apelido) &&
                unitData.apelido.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(unitData.name) &&
                unitData.name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
