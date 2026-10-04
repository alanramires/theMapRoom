using UnityEngine;

/// <summary>
/// Preferências do JOGADOR — não da partida. A gaveta "Partida" (cor, dificuldade) vai
/// no save e só muda antes do start; esta gaveta muda a qualquer momento e sobrevive
/// entre partidas, porque é gosto de quem segura o celular, não regra do tabuleiro.
///
/// Sem valor gravado, quem manda é o default autorado na cena (o campo serializado):
/// a preferência só existe depois que o jogador tocou no check pela primeira vez.
///
/// Tela cheia NÃO mora aqui: o navegador recusa entrar em tela cheia sem um toque do
/// jogador, então "lembrar ligada" nunca funcionaria na Web. Ela é espelho da tela.
/// </summary>
public static class PreferenciasDoJogador
{
    private const string ChaveModoTurbo = "pref.modoTurbo";
    private const string ChaveAcaoDireta = "pref.acaoDireta";

    /// <summary>Modo Turbo (AIController.iaRapida): a IA joga sem pausas, o cursor salta.</summary>
    public static bool ModoTurbo(bool defaultDaCena) => Ler(ChaveModoTurbo, defaultDaCena);
    public static void SetModoTurbo(bool valor) => Gravar(ChaveModoTurbo, valor);

    /// <summary>Ação Direta (MatchController.atalhoContextual): tocar no alvo executa sem confirmar.</summary>
    public static bool AcaoDireta(bool defaultDaCena) => Ler(ChaveAcaoDireta, defaultDaCena);
    public static void SetAcaoDireta(bool valor) => Gravar(ChaveAcaoDireta, valor);

    private static bool Ler(string chave, bool padrao)
    {
        try
        {
            return PlayerPrefs.HasKey(chave) ? PlayerPrefs.GetInt(chave) != 0 : padrao;
        }
        catch
        {
            // Armazenamento bloqueado (aba anônima, preview): fica o default da cena.
            return padrao;
        }
    }

    private static void Gravar(string chave, bool valor)
    {
        try
        {
            PlayerPrefs.SetInt(chave, valor ? 1 : 0);
            PlayerPrefs.Save();
        }
        catch
        {
            // Sem armazenamento a escolha vale só até fechar a página.
        }
    }
}
