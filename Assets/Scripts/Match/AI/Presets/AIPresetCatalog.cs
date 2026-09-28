using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// AIPresetCatalog — que perfil de IA cada dificuldade usa.
//
// O modelo anterior era UM baseline mais uma tabela em codigo (ApplyDifficultyOverlay)
// que acendia toggles por cima. Ele nao expressa o que o autor quer: "a media faz
// blitzkrieg MAS nao respeita a lista banida". Na overlay as capacidades acendem em
// bloco, porque todas derivam de hardMode — uma chave so com seis lampadas.
//
// Aqui cada dificuldade aponta para um preset AUTORADO, e o perfil diz o que faz. A
// overlay continua existindo como reserva: dificuldade sem asset no catalogo cai nela,
// e nada muda para quem nao usar o catalogo.
//
// CATALOGO, nao cena: diz o que EXISTE (os tres perfis), e vale para todo mapa. Que
// dificuldade esta em jogo e da partida, e chega pelo PartidaConfig.
//
// A ARMADILHA DOS NOMES, que este asset torna visivel: o enum nao casa com o botao.
// A Tela de Entrada oferece tres opcoes e a do meio ("MEDIO") e AIDifficulty.Facil.
// Por isso a entrada mostra o rotulo do jogador ao lado do enum — sem isso, editar o
// preset "facil" mudaria o botao MEDIO e ninguem entenderia por que.
// =====================================================================================
[CreateAssetMenu(fileName = "AIPresetCatalog", menuName = "Game/AI/Catalogo de Presets", order = 1)]
public class AIPresetCatalog : ScriptableObject
{
    [Serializable]
    public class Entrada
    {
        [Tooltip("Dificuldade interna. Confira o rotulo do jogador na linha abaixo — eles NAO casam por nome.")]
        public AIDifficulty dificuldade = AIDifficulty.Facil;

        [Tooltip("Perfil que esta dificuldade usa. Vazio = cai na overlay de codigo, como antes.")]
        public AIPresetData preset;
    }

    [Tooltip("Uma entrada por dificuldade oferecida ao jogador. Dificuldade ausente usa a overlay.")]
    public List<Entrada> entradas = new List<Entrada>();

    public bool TryGetPreset(AIDifficulty dificuldade, out AIPresetData preset)
    {
        preset = null;
        if (entradas == null)
            return false;

        for (int i = 0; i < entradas.Count; i++)
        {
            Entrada e = entradas[i];
            if (e != null && e.dificuldade == dificuldade && e.preset != null)
            {
                preset = e.preset;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Como o jogador chama esta dificuldade. Existe porque o enum diverge do botao, e
    /// quem edita o catalogo precisa ver os dois lado a lado. Espelha PanelMenu.
    /// </summary>
    public static string RotuloDoJogador(AIDifficulty dificuldade)
    {
        switch (dificuldade)
        {
            case AIDifficulty.Facil:   return "FACIL";
            case AIDifficulty.Medio:   return "MEDIO";
            case AIDifficulty.Dificil: return "DIFICIL";
            default:                   return dificuldade.ToString().ToUpperInvariant();
        }
    }

    /// <summary>As tres que a Tela de Entrada realmente oferece, na ordem em que aparecem.</summary>
    public static readonly AIDifficulty[] OferecidasAoJogador =
    {
        AIDifficulty.Facil,
        AIDifficulty.Medio,
        AIDifficulty.Dificil,
    };
}
