using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AIShoppingGroup
{
    public string label = "Group";
    [Range(0f, 100f)] public float targetPercentage = 0f;
    public int priority = 1;
    [Tooltip("Lista ordenada de fallback. A IA tenta a primeira unidade; se nao der, tenta a proxima.")]
    public List<UnitData> specificUnits = new List<UnitData>();

    public void EnsureDefaults()
    {
        if (specificUnits == null)
            specificUnits = new List<UnitData>();
        targetPercentage = Mathf.Clamp(targetPercentage, 0f, 100f);
        priority = Mathf.Max(0, priority);
    }
}

[Serializable]
public class AIShoppingMode
{
    public string label = "Mode";
    [Tooltip("Se true, tenta manter dinheiro para a proxima rodada quando nao consegue cumprir a composicao atual.")]
    public bool saveForNextRound = true;
    [Tooltip("Mesmo economizando, permite fallback imediato de contingencia se houver unidade acessivel.")]
    public bool allowFallbackWhenSaving = true;
    [Tooltip("Fallback de contingencia (UnitData). Ordem importa. Ex.: Soldado no ataque, Bazooka na defesa.")]
    public List<UnitData> fallbackUnits = new List<UnitData>();
    public List<AIShoppingGroup> groups = new List<AIShoppingGroup>();

    public void EnsureDefaults()
    {
        if (fallbackUnits == null)
            fallbackUnits = new List<UnitData>();
        if (groups == null)
            groups = new List<AIShoppingGroup>();

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] == null)
                groups[i] = new AIShoppingGroup();
            groups[i].EnsureDefaults();
        }
    }
}

[CreateAssetMenu(menuName = "Game/AI/Shopping Profile", fileName = "AIShoppingProfile_Basic")]
public class AIShoppingProfile : ScriptableObject
{
    public string profileName = "Basic";
    public AIShoppingMode attackMode = new AIShoppingMode();
    public AIShoppingMode defenseMode = new AIShoppingMode();

    [ContextMenu("Reset To Basic")]
    public void ResetToBasic()
    {
        AIShoppingProfile basic = CreateRuntimeBasic();
        profileName = basic.profileName;
        attackMode = basic.attackMode;
        defenseMode = basic.defenseMode;
        EnsureDefaults();
    }

    public static AIShoppingProfile CreateRuntimeBasic()
    {
        AIShoppingProfile profile = CreateInstance<AIShoppingProfile>();
        profile.profileName = "Basic";

        profile.attackMode = new AIShoppingMode
        {
            label = "Attack",
            saveForNextRound = true,
            allowFallbackWhenSaving = true,
            fallbackUnits = new List<UnitData>(),
            groups = new List<AIShoppingGroup>
            {
                new AIShoppingGroup
                {
                    label = "Infantry",
                    targetPercentage = 50f,
                    priority = 1,
                    specificUnits = new List<UnitData>()
                },
                new AIShoppingGroup
                {
                    label = "Tanks",
                    targetPercentage = 30f,
                    priority = 2,
                    specificUnits = new List<UnitData>()
                },
                new AIShoppingGroup
                {
                    label = "Vehicle",
                    targetPercentage = 20f,
                    priority = 3,
                    specificUnits = new List<UnitData>()
                }
            }
        };

        profile.defenseMode = new AIShoppingMode
        {
            label = "Defense",
            saveForNextRound = true,
            allowFallbackWhenSaving = true,
            fallbackUnits = new List<UnitData>(),
            groups = new List<AIShoppingGroup>
            {
                new AIShoppingGroup
                {
                    label = "Infantry",
                    targetPercentage = 50f,
                    priority = 1,
                    specificUnits = new List<UnitData>()
                },
                new AIShoppingGroup
                {
                    label = "Tanks",
                    targetPercentage = 30f,
                    priority = 2,
                    specificUnits = new List<UnitData>()
                },
                new AIShoppingGroup
                {
                    label = "Vehicle",
                    targetPercentage = 20f,
                    priority = 3,
                    specificUnits = new List<UnitData>()
                }
            }
        };

        profile.EnsureDefaults();
        return profile;
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    public void EnsureDefaults()
    {
        if (attackMode == null)
            attackMode = new AIShoppingMode();
        if (defenseMode == null)
            defenseMode = new AIShoppingMode();

        attackMode.EnsureDefaults();
        defenseMode.EnsureDefaults();
    }
}


