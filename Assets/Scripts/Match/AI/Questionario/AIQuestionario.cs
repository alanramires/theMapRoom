using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// AIQuestionario — o motor do questionário. Contrato:
// docs/AI Behavior/contrato_questionario.md
//
// Todo papel responde as MESMAS dez perguntas; o que muda é a ordem e o que faz com
// as respostas. Por isso este motor NÃO PODE conhecer papel: se aparecer
// "if capturador" aqui, o n × n voltou. Ele recebe uma ordem e quem responde cada
// casa, grava as respostas e acha a preliminar. Quem decide é a política do papel.
//
// Degrau 1 (hoje): só o observador usa. Nenhum comportamento muda.
// =====================================================================================

/// <summary>
/// As ações diretas e as 2 capacidades de informação (docs/AI Behavior/papeis.md).
/// Fundir existe aqui só para DESCREVER o que o código fez (a fusão do reparo): não é
/// perguntada por papel nenhum — a decisão de fundir é do reparo (contrato §6.4).
/// </summary>
public enum AICasa
{
    Capturar,
    Embarcar,
    Desembarcar,
    Mirar,
    Fundir,
    Suprir,
    Transferir,
    Reposicionar,
    Enxergar,
    Detectar,
}

/// <summary>Casa de AÇÃO vira candidata; casa de INFORMAÇÃO só condiciona as de baixo.</summary>
public enum AIEspecieCasa
{
    Acao,
    Informacao,
}

/// <summary>
/// Os quatro estados. O quarto é obrigatório: sem ele, casa sem consumidor vira um
/// "NÃO" inventado e o registro mente sobre o que a IA sabe.
/// </summary>
public enum AIResposta
{
    Sim,
    Nao,
    NaoSeAplica,
    NaoSeiResponder,
}

/// <summary>Uma resposta carrega o porquê — um NÃO sozinho não conta história.</summary>
public readonly struct AIRespostaCasa
{
    public readonly AICasa Casa;
    public readonly AIResposta Resposta;
    public readonly string Porque;
    public readonly Vector3Int Celula;
    public readonly bool TemCelula;

    public AIRespostaCasa(AICasa casa, AIResposta resposta, string porque)
    {
        Casa = casa;
        Resposta = resposta;
        Porque = porque ?? string.Empty;
        Celula = default;
        TemCelula = false;
    }

    public AIRespostaCasa(AICasa casa, AIResposta resposta, string porque, Vector3Int celula)
    {
        Casa = casa;
        Resposta = resposta;
        Porque = porque ?? string.Empty;
        celula.z = 0;
        Celula = celula;
        TemCelula = true;
    }
}

/// <summary>O que o questionário de UMA peça produziu, na ordem em que perguntou.</summary>
public sealed class AIQuestionarioRegistro
{
    public readonly List<AIRespostaCasa> Respostas = new List<AIRespostaCasa>(10);

    /// <summary>Índice em <see cref="Respostas"/> da primeira casa de ação com SIM; -1 se nenhuma.</summary>
    public int IndicePreliminar = -1;

    public bool TemPreliminar => IndicePreliminar >= 0;

    public AIRespostaCasa Preliminar => Respostas[IndicePreliminar];

    public bool TryGetResposta(AICasa casa, out AIRespostaCasa resposta)
    {
        for (int i = 0; i < Respostas.Count; i++)
        {
            if (Respostas[i].Casa == casa)
            {
                resposta = Respostas[i];
                return true;
            }
        }

        resposta = default;
        return false;
    }

    /// <summary>A próxima casa de ação com SIM depois de <paramref name="depoisDe"/>, na ordem do papel.</summary>
    public bool TryGetProximaAcaoSim(AICasa depoisDe, out AIRespostaCasa resposta)
    {
        bool passou = false;
        for (int i = 0; i < Respostas.Count; i++)
        {
            AIRespostaCasa r = Respostas[i];
            if (!passou)
            {
                passou = r.Casa == depoisDe;
                continue;
            }

            if (AIQuestionario.EspecieDe(r.Casa) == AIEspecieCasa.Acao && r.Resposta == AIResposta.Sim)
            {
                resposta = r;
                return true;
            }
        }

        resposta = default;
        return false;
    }
}

public static class AIQuestionario
{
    public static AIEspecieCasa EspecieDe(AICasa casa)
    {
        return casa == AICasa.Enxergar || casa == AICasa.Detectar
            ? AIEspecieCasa.Informacao
            : AIEspecieCasa.Acao;
    }

    /// <summary>
    /// Pergunta cada casa na ordem e grava tudo. A preliminar é mecânica: a primeira
    /// casa de AÇÃO que respondeu SIM. Responde as dez sempre — o registro inteiro é o
    /// que as políticas leem, e é o que o observador mostra.
    /// </summary>
    public static AIQuestionarioRegistro Rodar(
        IReadOnlyList<AICasa> ordem,
        Func<AICasa, AIRespostaCasa> responder)
    {
        var registro = new AIQuestionarioRegistro();
        if (ordem == null || responder == null)
            return registro;

        for (int i = 0; i < ordem.Count; i++)
        {
            AIRespostaCasa resposta = responder(ordem[i]);
            registro.Respostas.Add(resposta);

            if (registro.IndicePreliminar < 0
                && EspecieDe(resposta.Casa) == AIEspecieCasa.Acao
                && resposta.Resposta == AIResposta.Sim)
            {
                registro.IndicePreliminar = registro.Respostas.Count - 1;
            }
        }

        return registro;
    }

    public static string Rotulo(AIResposta resposta)
    {
        switch (resposta)
        {
            case AIResposta.Sim: return "SIM";
            case AIResposta.Nao: return "NÃO";
            case AIResposta.NaoSeAplica: return "NÃO SE APLICA";
            default: return "NÃO SEI";
        }
    }
}
