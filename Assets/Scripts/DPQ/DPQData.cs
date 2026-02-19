using UnityEngine;

public enum DPQQualidadeDePosicao
{
    Unfavorable = 0,
    Default = 1,
    Improved = 2,
    Favorable = 3,
    Unique = 4
}

[CreateAssetMenu(menuName = "Game/DPQ/DPQ Data", fileName = "DPQData_")]
public class DPQData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("ID unico usado para lookup e referencia.")]
    public string id;

    [Tooltip("Nome mostrado na UI/debug.")]
    public string nome;

    [TextArea]
    public string descricao;

    [Header("Qualidade de Posicao")]
    [Tooltip("Nivel de qualidade de posicao deste DPQ.")]
    public DPQQualidadeDePosicao qualidadeDePosicao = DPQQualidadeDePosicao.Default;

    [Tooltip("Quando ativo, aplica automaticamente os valores padrao da qualidade selecionada.")]
    public bool usarValoresPadraoDaQualidade = true;

    [Min(0)]
    [Tooltip("Pontos deste DPQ.")]
    public int pontos = 1;

    [Tooltip("Bonus de defesa deste DPQ (pode ser negativo).")]
    public int defesaBonus = 0;

    public int Pontos => pontos;
    public int DefesaBonus => defesaBonus;

    [ContextMenu("Aplicar Valores Padrao da Qualidade")]
    public void AplicarValoresPadraoDaQualidade()
    {
        pontos = GetPontosPadrao(qualidadeDePosicao);
        defesaBonus = GetDefesaPadrao(qualidadeDePosicao);
    }

    public static int GetPontosPadrao(DPQQualidadeDePosicao qualidade)
    {
        switch (qualidade)
        {
            case DPQQualidadeDePosicao.Unfavorable: return 0;
            case DPQQualidadeDePosicao.Default: return 1;
            case DPQQualidadeDePosicao.Improved: return 2;
            case DPQQualidadeDePosicao.Favorable: return 3;
            case DPQQualidadeDePosicao.Unique: return 4;
            default: return 1;
        }
    }

    public static int GetDefesaPadrao(DPQQualidadeDePosicao qualidade)
    {
        switch (qualidade)
        {
            case DPQQualidadeDePosicao.Unfavorable: return -1;
            case DPQQualidadeDePosicao.Default: return 0;
            case DPQQualidadeDePosicao.Improved: return 2;
            case DPQQualidadeDePosicao.Favorable: return 4;
            case DPQQualidadeDePosicao.Unique: return 6;
            default: return 0;
        }
    }

    private void OnValidate()
    {
        if (usarValoresPadraoDaQualidade)
            AplicarValoresPadraoDaQualidade();

        if (pontos < 0)
            pontos = 0;
    }
}
