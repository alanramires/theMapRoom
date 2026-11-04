using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// Registro de diagnóstico da partida — vai dentro do .tmrsave (diagnostico.json)
// e o Save Inspector lê pelo mesmo caminho do load.
//
// É FATO ESTRUTURADO gravado onde ele acontece, não texto do Console
// (docs/modding/guia de modding.md, §1 e §12). Por isso independe das flags de
// log do Console: elas controlam barulho, não o que fica registrado.
//
// Contrato, pensado para mods (§6, §7, §11, §15):
//   origem      quem gravou: "core" hoje; o id do mod ("autor.mod") amanhã
//   categoria   sistema: "combate", "decisao"
//   operacao    o fato: "combate.resolvido", "decisao.status", "decisao.sem_opcao_sensor"
//   codigo      motivo em número (estável); motivo é o texto
//   contagem    entradas agregadas somam aqui em vez de repetir linhas
//   campos      chave/valor livres; quem lê não pode assumir que existem
//   formato     versão deste esquema; descartadas = quantas o teto jogou fora
// ---------------------------------------------------------------------------

public enum DiagnosticoSeveridade
{
    Depuracao = 0,
    Aviso = 1,
    Erro = 2
}

[Serializable]
public class DiagnosticoCampo
{
    public string chave;
    public string valor;
}

[Serializable]
public class DiagnosticoEntrada
{
    public int id;
    public int rodada;
    public int slot = -1;
    public DiagnosticoSeveridade severidade;
    public string origem = "core";
    public string categoria;
    public string operacao;
    public int uid;
    public string sigla;
    public int uid2;
    public string sigla2;
    public int cx;
    public int cy;
    public int codigo;
    public string motivo;
    public bool agregada;
    public int contagem = 1;
    public List<DiagnosticoCampo> campos = new List<DiagnosticoCampo>();

    public DiagnosticoEntrada Campo(string chave, string valor)
    {
        campos.Add(new DiagnosticoCampo { chave = chave, valor = valor ?? string.Empty });
        return this;
    }

    public DiagnosticoEntrada Campo(string chave, int valor) => Campo(chave, valor.ToString());

    public string Valor(string chave)
    {
        for (int i = 0; i < campos.Count; i++)
        {
            if (campos[i] != null && campos[i].chave == chave)
                return campos[i].valor;
        }
        return null;
    }
}

[Serializable]
public class DiagnosticoLog
{
    public const int FormatoAtual = 1;

    public int formato = FormatoAtual;
    public List<DiagnosticoEntrada> entradas = new List<DiagnosticoEntrada>();
    public int proximoId = 1;
    public int descartadas;
}
