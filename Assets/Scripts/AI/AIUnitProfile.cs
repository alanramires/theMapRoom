using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class AIUnitStanceBehavior
{
    [Tooltip("Posturas em que este bloco se aplica. Arraste os assets BattleStanceData aqui.")]
    public List<BattleStanceData> stances = new List<BattleStanceData>();

    [Header("Sensor Priority")]
    [Tooltip("Ordem de execucao dos sensores. Apenas sensores presentes aqui disparam nesta postura.")]
    public List<AIUnitSensorKind> sensorPriority = new List<AIUnitSensorKind>();

    [Header("Attack Decision")]
    [Tooltip("Dano minimo em percentual do HP maximo do alvo para considerar engajar. 0 ignora.")]
    [Range(0f, 100f)]
    public float minDamageDealtPercent = 10f;
    [Tooltip("Dano maximo recebido em percentual do HP maximo do atacante. 0 ignora.")]
    [Range(0f, 100f)]
    public float maxDamageReceivedPercent = 50f;
    [Tooltip("Se true, exige que o atacante sobreviva para engajar.")]
    public bool mustSurvive = true;
    [Tooltip("Preferencia do alvo para ataque (Primary/Secondary/Either).")]
    public AIAttackTargetPreference targetPreference = AIAttackTargetPreference.Either;

    [Header("Behavior")]
    [Tooltip("Permite que a unidade seja designada como escolta pelo planner nesta postura.")]
    public bool canEscort;
    [Tooltip("Prioriza celulas DPQ ao se posicionar em combate (engajando inimigo).")]
    public bool prioritizeDpqAtBattle;
    [Tooltip("Prioriza celulas DPQ mesmo durante a viagem (sem inimigo engajado).")]
    public bool prioritizeDpqDuringTravel;
    [Tooltip("Sem objetivo ativo, a unidade recua ao proprio HQ. Tem prioridade sobre coesao de plano.")]
    public bool retreatToHqWhenIdle;
    [Tooltip("Joga de forma conservadora: move em celulas seguras, patrulha territorio aliado e prioriza sobrevivencia sobre kill no scoring de combate.")]
    public bool playConservative;
    [Tooltip("Ataca qualquer inimigo visivel sem restringir ao contexto do plano de escolta.")]
    public bool engageNearestEnemies;

    public bool PassesAttackThresholds(float dealtPercent, float receivedPercent, bool survives)
    {
        if (minDamageDealtPercent > 0f && dealtPercent + 0.0001f < minDamageDealtPercent) return false;
        if (maxDamageReceivedPercent > 0f && receivedPercent - 0.0001f > maxDamageReceivedPercent) return false;
        if (mustSurvive && !survives) return false;
        return true;
    }

    public int GetTargetPreferenceBonus(BazookaTargetPriority priority)
    {
        switch (targetPreference)
        {
            case AIAttackTargetPreference.Primary:
                if (priority == BazookaTargetPriority.Primary) return 2000;
                if (priority == BazookaTargetPriority.Secondary) return 1000;
                return 0;
            case AIAttackTargetPreference.Secondary:
                if (priority == BazookaTargetPriority.Secondary) return 2000;
                if (priority == BazookaTargetPriority.Primary) return 1000;
                return 0;
            default:
                return 0;
        }
    }
}

[CreateAssetMenu(menuName = "Game/AI/AI Unit Profile", fileName = "AIUnitProfile_")]
public class AIUnitProfile : ScriptableObject
{
    [Tooltip("Comportamento por postura. Cada elemento cobre uma ou mais stances. A primeira entrada que contiver a stance ativa e usada.")]
    public List<AIUnitStanceBehavior> stanceBehaviors = new List<AIUnitStanceBehavior>();

    [Header("Return to Base / Repair")]
    [Tooltip("Quando estiver em modo de defesa e cair em reposicionamento, emite Fallback ao inves de Reposition.")]
    public bool preferFallbackWhenDefend = true;
    [Tooltip("Se true, permite fusao com aliados feridos durante o retorno para reparo.")]
    public bool fuseWhileOnRepairMode = true;
    [Tooltip("HP maximo (inclusivo) para entrar em modo reparo automatico. 0 = nunca busca reparo.")]
    [Range(0, 10)]
    public int hpRepairThreshold = 3;
    [Tooltip("HP minimo (inclusivo) para sair do modo reparo e voltar ao front.")]
    [Range(1, 10)]
    public int hpRepairExitThreshold = 8;
    [Tooltip("Autonomia propria (%) para entrar em modo reparo. 0 = ignora autonomia.")]
    [Range(0, 100)]
    public int repairAutonomyThresholdPercent = 25;
    [Tooltip("Se true, entra em modo reparo quando qualquer arma embarcada zerar municao.")]
    public bool repairWhenAnyWeaponOutOfAmmo = true;

    [Header("Restock Decision")]
    [Tooltip("Limiar de galoes na carroceria (%) para voltar a base e reabastecer. 0 = apenas quando zerado.")]
    [Range(0, 100)]
    public int restockFuelThresholdPercent = 0;
    [Tooltip("Limiar de caixas de municao na carroceria (%) para voltar a base e reabastecer. 0 = apenas quando zerado.")]
    [Range(0, 100)]
    public int restockAmmoThresholdPercent = 0;
    [Tooltip("Limiar de pecas na carroceria (%) para voltar a base e reabastecer. 0 = apenas quando zerado.")]
    [Range(0, 100)]
    public int restockPartsThresholdPercent = 0;

    [Header("Supply Decision")]
    [Tooltip("Limiar de combustivel do aliado (%) para considerar reabastecimento. 0 = ignora combustivel.")]
    [Range(0, 100)]
    public int supplyAllyFuelThresholdPercent = 25;
    [Tooltip("Limiar de municao do aliado (%) para considerar reabastecimento. 0 = apenas quando sem municao.")]
    [Range(0, 100)]
    public int supplyAllyAmmoThresholdPercent = 0;
    [Tooltip("Limiar de HP do aliado (%) para priorizar como alvo de suprimento. 0 = ignora HP.")]
    [Range(0, 100)]
    public int supplyAllyHpThresholdPercent = 50;

    private static readonly AIUnitStanceBehavior _emptyBehavior = new AIUnitStanceBehavior();

    public AIUnitStanceBehavior GetStanceBehavior(AIStance stance)
    {
        if (stanceBehaviors == null || stanceBehaviors.Count == 0) return _emptyBehavior;
        for (int i = 0; i < stanceBehaviors.Count; i++)
        {
            AIUnitStanceBehavior b = stanceBehaviors[i];
            if (b?.stances == null) continue;
            for (int j = 0; j < b.stances.Count; j++)
            {
                if (b.stances[j] != null && b.stances[j].stanceType == stance)
                    return b;
            }
        }
        return stanceBehaviors[0] ?? _emptyBehavior;
    }

    public bool HasSensorInStance(AIStance stance, AIUnitSensorKind sensor)
    {
        var b = GetStanceBehavior(stance);
        return b.sensorPriority != null && b.sensorPriority.Contains(sensor);
    }

    private void OnValidate()
    {
        if (stanceBehaviors == null)
            stanceBehaviors = new List<AIUnitStanceBehavior>();

        for (int i = 0; i < stanceBehaviors.Count; i++)
        {
            if (stanceBehaviors[i] == null)
                stanceBehaviors[i] = new AIUnitStanceBehavior();
            if (stanceBehaviors[i].stances == null)
                stanceBehaviors[i].stances = new List<BattleStanceData>();
            if (stanceBehaviors[i].sensorPriority == null)
                stanceBehaviors[i].sensorPriority = new List<AIUnitSensorKind>();
        }

        hpRepairThreshold = Mathf.Clamp(hpRepairThreshold, 0, 10);
        hpRepairExitThreshold = Mathf.Max(hpRepairThreshold + 1, hpRepairExitThreshold);
    }
}
