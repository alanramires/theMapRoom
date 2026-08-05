using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O PERFIL DA LINHA: tudo que o traçado descobriu sobre UMA reta, num tipo so.
///
/// Existe porque as tres verdades que consomem o <see cref="ObservationLineService"/>
/// — revelar hexagono, detectar unidade, mirar — tracavam a MESMA reta e a
/// descreviam com campos diferentes. Cada janela remontava o relatorio a partir
/// do que sobrava: uma tinha a subida passo a passo e o nome do bloqueador, a
/// outra tinha uma lista de floats crua. Comparar as duas virava traducao
/// manual, que e o oposto do que elas existem para fazer.
///
/// E a mesma licao de "uma pergunta, uma implementacao", agora aplicada a
/// SAIDA: quem calcula a linha e quem conta o que aconteceu com ela sao o mesmo
/// codigo. O perfil nao e recalculado por ninguem — ele e preenchido no mesmo
/// laco que decidiu se a linha passa.
///
/// Nao formata nada. Quem transforma isto em texto e o
/// <see cref="ObservationLineReport"/>.
/// </summary>
public sealed class ObservationLineProfile
{
    /// <summary>A linha chegou ao alvo.</summary>
    public bool reached;

    /// <summary>
    /// Se o traçado foi autorizado a deter a linha. Com a validacao desligada
    /// nada bloqueia, e o relatorio precisa dizer isso em vez de exibir uma
    /// linha limpa que nunca foi testada.
    /// </summary>
    public bool losValidationEnabled;

    public Vector3Int originCell;
    public Vector3Int targetCell;

    /// <summary>EV de onde a linha partiu, e EV onde ela deveria chegar.</summary>
    public float originEv;
    public float targetEv;

    /// <summary>Hexes cruzados, na ordem em que a reta os encontrou.</summary>
    public readonly List<Vector3Int> crossedCells = new List<Vector3Int>();

    /// <summary>
    /// Altura da linha em cada ponto: EV de origem, um valor por hex cruzado e,
    /// quando ela chega, o EV do alvo. E o que diz se a linha SUBIU ou DESCEU —
    /// o radar terrestre olhando um caca alto sobe; o caca olhando o chao desce.
    ///
    /// Quando a linha e detida, o ultimo valor e a altura dela no hex que a
    /// deteve: e ate ali que ela chegou.
    /// </summary>
    public readonly List<float> evPath = new List<float>();

    public bool hasBlocker;
    public Vector3Int blockedCell;

    /// <summary>EV do bloqueador — o que a linha tentou ver por cima e nao deu.</summary>
    public float blockedCellEv;

    /// <summary>Altura da linha no hex que a deteve.</summary>
    public float lineHeightAtBlockedCell;

    /// <summary>
    /// O MAIOR obstaculo que a linha limpou. So conta quem realmente bloqueia
    /// linha: um hex de EV alto que nao tem blockLoS nunca foi obstaculo, e
    /// anuncia-lo como "passou por" sugere uma folga que nao existiu.
    /// </summary>
    public bool hasStrongestPassed;
    public Vector3Int strongestPassedCell;
    public float strongestPassedCellEv;
    public float lineHeightAtStrongestPassed;

    /// <summary>
    /// Onde a linha efetivamente terminou: o EV do alvo quando ela chega, a
    /// altura no bloqueador quando nao chega.
    /// </summary>
    public float FinalReachedEv => evPath.Count > 0 ? evPath[evPath.Count - 1] : 0f;

    /// <summary>Subiu, desceu ou ficou nivelada.</summary>
    public bool HasProfile => evPath.Count > 0;

    public void Clear()
    {
        reached = false;
        losValidationEnabled = false;
        originCell = Vector3Int.zero;
        targetCell = Vector3Int.zero;
        originEv = 0f;
        targetEv = 0f;
        crossedCells.Clear();
        evPath.Clear();
        hasBlocker = false;
        blockedCell = Vector3Int.zero;
        blockedCellEv = 0f;
        lineHeightAtBlockedCell = 0f;
        hasStrongestPassed = false;
        strongestPassedCell = Vector3Int.zero;
        strongestPassedCellEv = 0f;
        lineHeightAtStrongestPassed = 0f;
    }
}
