using System;
using System.IO;
using System.Text;
using UnityEngine;

// =====================================================================================
// Arquivo do observador do questionário, na RAIZ do projeto.
//
// Por que arquivo e não Console: os logs do Play não chegam ao Editor.log, e o
// Console afoga uma linha por peça no meio de centenas de outras. Um arquivo só com
// o questionário é lido de ponta a ponta, e dá para comparar duas partidas.
//
// Só no Editor. Num build Web não há onde escrever, e o observador não é do jogo.
// =====================================================================================
public static class AIQuestionarioLog
{
    public const string NomeArquivo = "questionario_observador.log";

    private static bool iniciadoNestaSessao;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetarSessao()
    {
        // Com domain reload desligado, estático sobrevive entre Plays. Sem isto, o
        // segundo Play anexaria ao arquivo do primeiro.
        iniciadoNestaSessao = false;
    }

    public static string Caminho
    {
        get
        {
            DirectoryInfo raiz = Directory.GetParent(Application.dataPath);
            return Path.Combine(raiz != null ? raiz.FullName : Application.dataPath, NomeArquivo);
        }
    }

    public static void Escrever(string bloco)
    {
        if (!Application.isEditor || string.IsNullOrEmpty(bloco))
            return;

        try
        {
            if (!iniciadoNestaSessao)
            {
                iniciadoNestaSessao = true;
                var cabecalho = new StringBuilder();
                cabecalho.AppendLine($"# Observador do questionário — {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                cabecalho.AppendLine("# Contrato: docs/AI Behavior/contrato_questionario.md");
                cabecalho.AppendLine("# Só anota: nenhum comportamento muda. \"<< DIVERGE\" = o código de hoje fez outra casa.");
                cabecalho.AppendLine();
                File.WriteAllText(Caminho, cabecalho.ToString(), Encoding.UTF8);
            }

            File.AppendAllText(Caminho, bloco, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Questionario] não consegui escrever {NomeArquivo}: {ex.Message}");
        }
    }
}
