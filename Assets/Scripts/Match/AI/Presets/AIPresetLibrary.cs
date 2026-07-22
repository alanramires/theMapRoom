using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// AIPresetLibrary — resolve QUAL AIPresetData vale agora.
//
// Nasce com resolução por TIME de propósito, mesmo que na fase 1 todos os times apontem
// para o mesmo preset. Hoje o runtime lê AIController.Instance.HardMode, que é um valor
// único para a partida inteira — mas o AIController é um singleton que roda vários times
// (RunAITurn(TeamId)). Enquanto a doutrina for global, nunca vai dar para colocar dois
// generais diferentes no mesmo mapa. O seam tem que existir desde já, senão a migração
// acontece duas vezes.
// =====================================================================================
[CreateAssetMenu(fileName = "AIPresetLibrary", menuName = "Game/AI/AI Preset Library", order = 1)]
public class AIPresetLibrary : ScriptableObject
{
    [Serializable]
    public class DificuldadeEntry
    {
        public AIDifficulty dificuldade;
        public AIPresetData preset;
    }

    [Serializable]
    public class TimeOverrideEntry
    {
        [Tooltip("Time que recebe um preset próprio, ignorando a dificuldade escolhida na Tela de Entrada.")]
        public TeamId time = TeamId.Neutral;
        public AIPresetData preset;
    }

    [Header("Mapa da Tela de Entrada")]
    [Tooltip("Um preset por dificuldade. Gerado por Tools > AI > Gerar Presets a partir da cena.")]
    public List<DificuldadeEntry> porDificuldade = new List<DificuldadeEntry>();

    [Header("Override por time (generais)")]
    [Tooltip("Vazio na fase 1. É por aqui que Azul e Vermelho entram no mesmo mapa depois, sem passar pela dificuldade.")]
    public List<TimeOverrideEntry> porTime = new List<TimeOverrideEntry>();

    [Header("Fallback")]
    [Tooltip("Usado quando a dificuldade pedida não tem entrada. Evita cair em null no meio de uma partida.")]
    public AIPresetData presetPadrao;

    public AIPresetData Resolve(AIDifficulty dificuldade)
    {
        for (int i = 0; i < porDificuldade.Count; i++)
        {
            DificuldadeEntry entry = porDificuldade[i];
            if (entry != null && entry.preset != null && entry.dificuldade == dificuldade)
                return entry.preset;
        }
        return presetPadrao;
    }

    public AIPresetData Resolve(AIDifficulty dificuldade, TeamId time)
    {
        if (time != TeamId.Neutral)
        {
            for (int i = 0; i < porTime.Count; i++)
            {
                TimeOverrideEntry entry = porTime[i];
                if (entry != null && entry.preset != null && entry.time == time)
                    return entry.preset;
            }
        }
        return Resolve(dificuldade);
    }

    public void SetPreset(AIDifficulty dificuldade, AIPresetData preset)
    {
        for (int i = 0; i < porDificuldade.Count; i++)
        {
            if (porDificuldade[i] != null && porDificuldade[i].dificuldade == dificuldade)
            {
                porDificuldade[i].preset = preset;
                return;
            }
        }
        porDificuldade.Add(new DificuldadeEntry { dificuldade = dificuldade, preset = preset });
    }
}
