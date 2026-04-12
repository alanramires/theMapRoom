public enum CaptureInterruptBias
{
    None       = -1, // Desabilita completamente morde no caminho e fallback oportunista
    Passive    =  0, // Score minimo alto (38000): raramente abandona captura para brigar
    Normal     =  1, // Score minimo padrao (28000)
    Aggressive =  2, // Score minimo baixo (22000): interrompe captura com facilidade
}
