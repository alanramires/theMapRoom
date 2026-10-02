using UnityEngine;

/// <summary>
/// Chave unica de log para o build de publicacao.
///
/// No Editor e no Development Build nada muda: todo Debug.Log continua saindo.
/// No build Release so passam erros e excecoes. Os logs de diagnostico (TurnPerf,
/// FrameSpike, AI Intel, SectorManager...) viram centenas de linhas por turno no
/// Console do navegador, e no celular cada linha custa processador. Medido no
/// Galaxy A15 em 2026-10-02.
///
/// Uma chave aqui, em vez de desligar flag por flag: as flags continuam valendo
/// para quem testa no Editor, e o jogador nao paga por elas.
/// </summary>
public static class ReleaseLogFilter
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        if (Application.isEditor || Debug.isDebugBuild)
            return;

        // Error deixa passar LogType.Error e LogType.Exception; corta Assert,
        // Warning e Log.
        Debug.unityLogger.filterLogType = LogType.Error;
    }
}
