using UnityEngine;

/// <summary>
/// Preferências do JOGADOR — não da partida. A gaveta "Partida" (cor, dificuldade) vai
/// no save e só muda antes do start; esta gaveta muda a qualquer momento e sobrevive
/// entre partidas, porque é gosto de quem segura o celular, não regra do tabuleiro.
///
/// Sem valor gravado, quem manda é o default autorado na cena (o campo serializado):
/// a preferência só existe depois que o jogador tocou no check pela primeira vez.
///
/// Volume geral e da música também moram aqui (gosto de quem ouve, não regra da partida).
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

    // ── Volume (prática da Mi'Larhys: GameSettings.Volume / MusicVolume) ─────
    // GERAL é o AudioListener: abaixa TUDO de uma vez — efeitos, música, vozes, vídeo —
    // em toda cena, sem que cada fonte precise saber. MÚSICA multiplica por cima do geral,
    // só nas trilhas. Padrão 100%: quem nunca mexeu ouve o jogo como o autor mixou.
    // Escala do jogador, nunca o volume autorado: o musicVolume da cena continua sendo a
    // mixagem; a preferência só multiplica.
    private const string ChaveVolumeGeral = "pref.volumeGeral";
    private const string ChaveVolumeMusica = "pref.volumeMusica";

    /// <summary>Disparado quando o volume da música muda, para as trilhas tocando se ajustarem na hora.</summary>
    public static event System.Action VolumeMusicaMudou;

    public static float VolumeGeral => LerFloat(ChaveVolumeGeral, 1f);
    public static float VolumeMusica => LerFloat(ChaveVolumeMusica, 1f);

    public static void SetVolumeGeral(float valor)
    {
        GravarFloat(ChaveVolumeGeral, Mathf.Clamp01(valor));
        AplicarVolumeGeral();
    }

    public static void SetVolumeMusica(float valor)
    {
        GravarFloat(ChaveVolumeMusica, Mathf.Clamp01(valor));
        VolumeMusicaMudou?.Invoke();
    }

    // Antes da primeira cena: a abertura já toca no volume que o jogador escolheu.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AplicarVolumeGeral() => AudioListener.volume = VolumeGeral;

    private static float LerFloat(string chave, float padrao)
    {
        try
        {
            return PlayerPrefs.HasKey(chave) ? Mathf.Clamp01(PlayerPrefs.GetFloat(chave)) : padrao;
        }
        catch
        {
            return padrao;
        }
    }

    private static void GravarFloat(string chave, float valor)
    {
        try
        {
            PlayerPrefs.SetFloat(chave, valor);
            PlayerPrefs.Save();
        }
        catch
        {
            // Sem armazenamento a escolha vale só até fechar a página.
        }
    }

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
