using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AIDataGroup
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
public class AIDataMode
{
    public string label = "Mode";
    [Tooltip("Se true, tenta manter dinheiro para a proxima rodada quando nao consegue cumprir a composicao atual.")]
    public bool saveForNextRound = true;
    [Tooltip("Mesmo economizando, permite fallback imediato de contingencia se houver unidade acessivel.")]
    public bool allowFallbackWhenSaving = true;
    [Tooltip("Quando allowFallbackWhenSaving estiver ativo, limita o fallback de economia a uma unica compra por turno.")]
    public bool allowFallbackWhenSavingButOnce = true;
    [Tooltip("Em modo de defesa, permite ignorar o limite de uma unica compra e comprar fallback enquanto economiza.")]
    public bool buyFallbackWhenSavingOnDefenseMode = true;
    [Tooltip("Fallback de contingencia (UnitData). Ordem importa.")]
    public List<UnitData> fallbackUnits = new List<UnitData>();
    public List<AIDataGroup> groups = new List<AIDataGroup>();

    public void EnsureDefaults()
    {
        if (fallbackUnits == null)
            fallbackUnits = new List<UnitData>();
        if (groups == null)
            groups = new List<AIDataGroup>();

        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i] == null)
                groups[i] = new AIDataGroup();
            groups[i].EnsureDefaults();
        }
    }
}

[CreateAssetMenu(menuName = "Game/AI/AI Data", fileName = "AIData_Basic")]
public class AIData : ScriptableObject
{
    public string profileName = "Basic";
    public AIDataMode attackMode = new AIDataMode();
    public AIDataMode defenseMode = new AIDataMode();

    [ContextMenu("Reset To Basic")]
    public void ResetToBasic()
    {
        AIData basic = CreateRuntimeBasic();
        profileName = basic.profileName;
        attackMode = basic.attackMode;
        defenseMode = basic.defenseMode;
        EnsureDefaults();
    }

    public static AIData CreateRuntimeBasic()
    {
        AIData data = CreateInstance<AIData>();
        data.profileName = "Basic";

        data.attackMode = new AIDataMode
        {
            label = "Attack",
            saveForNextRound = true,
            allowFallbackWhenSaving = true,
            allowFallbackWhenSavingButOnce = true,
            buyFallbackWhenSavingOnDefenseMode = true,
            fallbackUnits = new List<UnitData>(),
            groups = new List<AIDataGroup>
            {
                new AIDataGroup { label = "Infantry", targetPercentage = 50f, priority = 1, specificUnits = new List<UnitData>() },
                new AIDataGroup { label = "Tanks", targetPercentage = 30f, priority = 2, specificUnits = new List<UnitData>() },
                new AIDataGroup { label = "Vehicle", targetPercentage = 20f, priority = 3, specificUnits = new List<UnitData>() }
            }
        };

        data.defenseMode = new AIDataMode
        {
            label = "Defense",
            saveForNextRound = true,
            allowFallbackWhenSaving = true,
            allowFallbackWhenSavingButOnce = true,
            buyFallbackWhenSavingOnDefenseMode = true,
            fallbackUnits = new List<UnitData>(),
            groups = new List<AIDataGroup>
            {
                new AIDataGroup { label = "Infantry", targetPercentage = 50f, priority = 1, specificUnits = new List<UnitData>() },
                new AIDataGroup { label = "Tanks", targetPercentage = 30f, priority = 2, specificUnits = new List<UnitData>() },
                new AIDataGroup { label = "Vehicle", targetPercentage = 20f, priority = 3, specificUnits = new List<UnitData>() }
            }
        };

        data.EnsureDefaults();
        return data;
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    public void EnsureDefaults()
    {
        if (attackMode == null)
            attackMode = new AIDataMode();
        if (defenseMode == null)
            defenseMode = new AIDataMode();

        attackMode.EnsureDefaults();
        defenseMode.EnsureDefaults();
    }
}
