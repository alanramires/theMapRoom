using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/AI/AI Unit Profile", fileName = "AIUnitProfile_")]
public class AIUnitProfile : ScriptableObject
{
    [Tooltip("Ordem de prioridade dos sensores taticos da unidade.")]
    [SerializeField] private List<AIUnitSensorKind> sensorPriority = new List<AIUnitSensorKind>
    {
        AIUnitSensorKind.Capture,
        AIUnitSensorKind.Attack,
        AIUnitSensorKind.Reposition
    };

    [Tooltip("Permite o sensor de captura nesta unidade.")]
    public bool allowCapture = true;

    [Tooltip("Permite o sensor de ataque nesta unidade.")]
    public bool allowAttack = true;

    [Tooltip("Permite o sensor de reposicionamento nesta unidade.")]
    public bool allowReposition = true;


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

    [Tooltip("Quando estiver em modo de defesa e cair em reposicionamento, emite Fallback ao inves de Reposition.")]
    public bool preferFallbackWhenDefend = true;

    [Header("Repair")]
    [Tooltip("HP maximo (inclusivo) para entrar em modo reparo automatico. 0 = nunca busca reparo.")]
    [Range(0, 10)]
    public int hpRepairThreshold = 3;

    [Tooltip("HP minimo (inclusivo) para sair do modo reparo e voltar ao front.")]
    [Range(1, 10)]
    public int hpRepairExitThreshold = 8;

    public IReadOnlyList<AIUnitSensorKind> SensorPriority => sensorPriority;

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
    public bool PassesAttackThresholds(float dealtPercent, float receivedPercent, bool survives)
    {
        if (minDamageDealtPercent > 0f && dealtPercent + 0.0001f < minDamageDealtPercent)
            return false;

        if (maxDamageReceivedPercent > 0f && receivedPercent - 0.0001f > maxDamageReceivedPercent)
            return false;

        if (mustSurvive && !survives)
            return false;

        return true;
    }

    private void OnValidate()
    {
        if (sensorPriority == null)
            sensorPriority = new List<AIUnitSensorKind>();

        if (sensorPriority.Count == 0)
        {
            sensorPriority.Add(AIUnitSensorKind.Capture);
            sensorPriority.Add(AIUnitSensorKind.Attack);
            sensorPriority.Add(AIUnitSensorKind.Reposition);
            return;
        }

        for (int i = sensorPriority.Count - 1; i >= 0; i--)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (sensorPriority[i] == sensorPriority[j])
                {
                    sensorPriority.RemoveAt(i);
                    break;
                }
            }
        }

        minDamageDealtPercent = Mathf.Clamp(minDamageDealtPercent, 0f, 100f);
        maxDamageReceivedPercent = Mathf.Clamp(maxDamageReceivedPercent, 0f, 100f);
        hpRepairThreshold = Mathf.Clamp(hpRepairThreshold, 0, 10);
        hpRepairExitThreshold = Mathf.Max(hpRepairThreshold + 1, hpRepairExitThreshold);
    }
}

