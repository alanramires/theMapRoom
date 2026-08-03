using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================================
// AICaptainData — a Tabela Magnética como ASSET.
//
// "Quem este papel acompanha" era codigo: quatro resolvedores hardcoded
// (TryResolveCapturerMagnet, TryResolveFireSupportMagnet, TryResolveNearestEwacsMagnet,
// TryResolveStockRearCaptain), cada um com a sua lista embutida e dois deles com
// predicados incompativeis entre si.
//
// Aqui a COMPOSICAO e a ORDEM viram dado. Trocar quem o antiaereo segue passa a ser
// abrir o asset e arrastar uma linha, nao editar C#.
//
// O QUE NAO VIRA DADO, e por que:
//
//   "Aeronaves detectadas", "construcao aliada falida", "feridos", "capturavel" —
//   nenhum desses e um papel. Sao PREDICADOS, e predicado e funcao: precisa consultar
//   sensor, ficha, estoque, deteccao. Fingir que cabem num asset produziria um campo
//   de texto que ninguem valida.
//
//   Entao o asset guarda QUAL predicado e em QUE ORDEM (o enum abaixo), e o codigo
//   guarda COMO cada um responde. E a mesma divisao do resto do projeto: a politica e
//   do chamador, a regra e do sensor.
//
// Ver docs/magnetic_tabela.md.
// =====================================================================================

/// <summary>
/// O que uma faixa de atracao procura. Cada valor tem uma implementacao em codigo;
/// o asset escolhe quais usar e em que ordem.
/// </summary>
public enum AICaptainAttractionKind
{
    [Tooltip("Unidade aliada que satisfaz um papel. Use o campo Papel ao lado.")]
    UnidadeComPapel = 0,

    [Tooltip("Construcao capturavel ou aliada sob captura (reconquista). Consulta o PodeCapturar.")]
    ConstrucaoCapturavel = 1,

    [Tooltip("Construcao aliada sem recursos.")]
    ConstrucaoAliadaFalida = 2,

    [Tooltip("Aliado ferido, candidato a reparo.")]
    AliadoFerido = 3,

    [Tooltip("Aliado que precisa de manutencao preventiva.")]
    AliadoEmManutencao = 4,

    [Tooltip("Aeronave inimiga detectada.")]
    AeronaveInimigaDetectada = 5,

    [Tooltip("Unidade de superficie inimiga detectada.")]
    SuperficieInimigaDetectada = 6,

    [Tooltip("Passageiro que pediu carona.")]
    PassageiroPedindoCarona = 7,

    [Tooltip("Construcao que vale observar (vigilancia terrestre). Sai da lista quando os arredores ja estao revelados.")]
    PontoDeObservacao = 8,

    [Tooltip("Celula representativa do setor do plano. E o 'capitao abstrato': so serve enquanto nao ha lideranca real.")]
    RepCellDoSetor = 9
}

/// <summary>
/// Uma faixa da lista. A ORDEM no array e a prioridade: a primeira que produzir
/// candidato vence, mesmo que alguem de faixa inferior esteja mais perto.
/// </summary>
[Serializable]
public class AICaptainAttractionEntry
{
    [Tooltip("O que esta faixa procura.")]
    public AICaptainAttractionKind procura = AICaptainAttractionKind.UnidadeComPapel;

    [Tooltip("So vale para 'Unidade com papel'. O papel que serve de referencia.\n\n" +
             "A comparacao usa UnitRoleCompatibility.CanSatisfy, entao especializacoes " +
             "servem: pedir Capturador aceita CapturadorAgressivo.")]
    public UnitRole papel = UnitRole.Capturador;

    [Tooltip("LIGADO: aceita capitao EMBARCADO nesta faixa.\n\n" +
             "Embarcado nao se segue andando — se segue pedindo carona. Ligue para a " +
             "unidade COM PLANO, que nao deve trocar de capitao so porque o dela entrou " +
             "num veiculo. Sem plano, deixe desligado: ela pega outro capitao proximo.\n\n" +
             "Morto, em reparo e inativo nunca entram, com ou sem esta opcao — esses nao " +
             "vao a lugar nenhum.")]
    public bool aceitarEmbarcado = false;

    [Tooltip("LIGADO: so considera candidatos do setor do plano da unidade.\n\n" +
             "E o que transforma a lista 'sem plano' na lista 'com plano': mesma lista, " +
             "filtrada pelo setor, com a RepCell no fim.")]
    public bool restringirAoSetorDoPlano = false;

    [Tooltip("Rotulo que aparece no log e na ferramenta. Deixe vazio para gerar automatico.")]
    public string rotulo = string.Empty;
}

/// <summary>Uma linha da tabela: o papel e as duas listas dele.</summary>
[Serializable]
public class AICaptainProfile
{
    [Tooltip("O papel dono desta linha.")]
    public UnitRole papel = UnitRole.Assalto;

    [Tooltip("Lista usada quando a unidade NAO tem plano. Ordem = prioridade.")]
    public List<AICaptainAttractionEntry> semPlano =
        new List<AICaptainAttractionEntry>();

    [Tooltip("Lista usada quando a unidade TEM plano. Ordem = prioridade.\n\n" +
             "Se ficar vazia, o codigo deriva da lista 'sem plano': mesma lista com " +
             "'restringir ao setor' ligado, mais a RepCell no fim. Preencha apenas se " +
             "este papel precisar de algo diferente disso.")]
    public List<AICaptainAttractionEntry> comPlano =
        new List<AICaptainAttractionEntry>();
}

[CreateAssetMenu(
    fileName = "AICaptain",
    menuName = "Map Room/AI/Tabela Magnetica (Capitao)",
    order = 20)]
public class AICaptainData : ScriptableObject
{
    [TextArea(3, 8)]
    [Tooltip("Anotacao livre. Nao e lido por nada.")]
    public string observacoes =
        "Quem cada papel acompanha. A ordem dentro de cada lista e a prioridade: " +
        "a primeira faixa que produzir candidato vence, mesmo que alguem de faixa " +
        "inferior esteja mais perto.";

    [Tooltip("Uma linha por papel. Papel sem linha aqui nao tem magnetismo — ele nao " +
             "segue ninguem, o que e valido (o Capturador puro, por exemplo, e seguido " +
             "e nao segue).")]
    public List<AICaptainProfile> perfis = new List<AICaptainProfile>();

    /// <summary>
    /// A lista deste papel, ja resolvida para o caso com ou sem plano.
    ///
    /// Devolve nulo quando o papel nao tem linha no asset — que nao e erro: significa
    /// "este papel nao orbita ninguem".
    /// </summary>
    public IReadOnlyList<AICaptainAttractionEntry> TryResolve(
        UnitRole role,
        bool hasPlan)
    {
        AICaptainProfile profile = FindProfile(role);
        if (profile == null)
            return null;

        if (!hasPlan)
            return profile.semPlano;

        if (profile.comPlano != null && profile.comPlano.Count > 0)
            return profile.comPlano;

        return DeriveWithPlan(profile.semPlano);
    }

    /// <summary>
    /// A regra que evita repetir a tabela inteira duas vezes: "com plano" e a mesma
    /// lista, presa ao setor, com a RepCell como ultimo consolo.
    ///
    /// Uma linha do rascunho original se conserta sozinha aqui — o Estoque com plano
    /// deixava de atender construcao falida do proprio setor e ia direto para a
    /// RepCell.
    /// </summary>
    private static List<AICaptainAttractionEntry> DeriveWithPlan(
        List<AICaptainAttractionEntry> withoutPlan)
    {
        var derived = new List<AICaptainAttractionEntry>();
        if (withoutPlan != null)
        {
            for (int i = 0; i < withoutPlan.Count; i++)
            {
                AICaptainAttractionEntry source = withoutPlan[i];
                if (source == null)
                    continue;
                derived.Add(new AICaptainAttractionEntry
                {
                    procura = source.procura,
                    papel = source.papel,
                    // Com plano a unidade nao troca de capitao so porque o dela
                    // embarcou: pede carona atras dele.
                    aceitarEmbarcado = true,
                    restringirAoSetorDoPlano = true,
                    rotulo = source.rotulo
                });
            }
        }

        derived.Add(new AICaptainAttractionEntry
        {
            procura = AICaptainAttractionKind.RepCellDoSetor,
            rotulo = "RepCell (capitão abstrato)"
        });
        return derived;
    }

    private AICaptainProfile FindProfile(UnitRole role)
    {
        if (perfis == null)
            return null;
        for (int i = 0; i < perfis.Count; i++)
        {
            if (perfis[i] != null && perfis[i].papel == role)
                return perfis[i];
        }
        return null;
    }

    /// <summary>Rotulo automatico quando o autor nao escreveu um.</summary>
    public static string DescribeEntry(AICaptainAttractionEntry entry)
    {
        if (entry == null)
            return "?";
        if (!string.IsNullOrWhiteSpace(entry.rotulo))
            return entry.rotulo;

        switch (entry.procura)
        {
            case AICaptainAttractionKind.UnidadeComPapel:
                return entry.papel == UnitRole.Capturador
                    ? "Capitão"
                    : entry.papel.ToString();
            case AICaptainAttractionKind.ConstrucaoCapturavel:
                return "capturável / reconquistável";
            case AICaptainAttractionKind.ConstrucaoAliadaFalida:
                return "construção aliada falida";
            case AICaptainAttractionKind.AliadoFerido:
                return "ferido";
            case AICaptainAttractionKind.AliadoEmManutencao:
                return "manutenção";
            case AICaptainAttractionKind.AeronaveInimigaDetectada:
                return "aeronave detectada";
            case AICaptainAttractionKind.SuperficieInimigaDetectada:
                return "superfície detectada";
            case AICaptainAttractionKind.PassageiroPedindoCarona:
                return "passageiro pedindo carona";
            case AICaptainAttractionKind.PontoDeObservacao:
                return "ponto de observação";
            default:
                return "RepCell (capitão abstrato)";
        }
    }
}
