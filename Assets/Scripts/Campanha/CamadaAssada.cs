using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Uma camada DECORATIVA do quadrante, assada junto com o terreno.
///
/// Enfeite: quebra-mar e o que mais vier — o que impede o tabuleiro de ficar seco.
/// Nao tem regra, nao tem custo, nao entra em sensor. Se a camada faltar na cena
/// de destino, o quadrante joga igual; so fica mais feio.
///
/// ⚠️ MAS 'quebraMar' NAO E ROTULO LIVRE. A memoria de nevoa fotografa TRES
/// coisas — hexagono, construcao e quebraMar — e o nome esta fixo no codigo
/// (MatchController.RenderFogBreakwaterMemory). Camada com outro nome e copiada
/// pelo recorte, mas NAO e fotografada: aparece onde esta visivel e SOME onde esta
/// so explorado, sem erro nenhum.
///
/// GUARDADA ESPARSA, e essa e a diferenca que importa pro mundo grande:
///
///   terreno    ~100% das celulas preenchidas  → array do retangulo inteiro
///   enfeite    ~2%  (39 tiles em 1800)        → so as celulas que existem
///
/// E LEVA A ORIENTACAO JUNTO. Tilemap.SetTile copia o tile e DESCARTA a rotacao,
/// o espelho e a cor da celula — que ficam em arrays paralelos. Um quebra-mar e
/// justamente um sprite so, girado pra acompanhar a costa: sem a matriz, ele
/// nasce todo apontando pro mesmo lado. As pecas certas, nos lugares certos, e
/// o desenho irreconhecivel.
///
/// Denso, um quadrante 16x17 gastaria 272 entradas por camada com ~266 nulas — e
/// isso multiplica por quadrante E por camada. Num mundo de quarenta quadrantes
/// com duas camadas seriam ~32.000 referencias quase todas vazias, num asset que e
/// reescrito inteiro a cada bake.
/// </summary>
[System.Serializable]
public class CamadaAssada
{
    /// <summary>
    /// A transformacao de UMA celula, em colunas.
    ///
    /// Guardada como as 4 colunas cruas, e nao decomposta em posicao/rotacao/escala,
    /// porque espelhar e escala NEGATIVA — e Matrix4x4.lossyScale nao devolve o sinal
    /// de forma confiavel. Recompor de um TRS decomposto desespelharia a peca sem
    /// erro nenhum, que e o tipo de defeito que so aparece olhando o mapa.
    /// </summary>
    [System.Serializable]
    public struct Transformacao
    {
        public Vector4 c0;
        public Vector4 c1;
        public Vector4 c2;
        public Vector4 c3;

        public Matrix4x4 ToMatrix()
        {
            return new Matrix4x4(c0, c1, c2, c3);
        }

        public static Transformacao De(Matrix4x4 m)
        {
            return new Transformacao
            {
                c0 = m.GetColumn(0),
                c1 = m.GetColumn(1),
                c2 = m.GetColumn(2),
                c3 = m.GetColumn(3)
            };
        }

        public bool Igual(Matrix4x4 m)
        {
            return c0 == (Vector4)m.GetColumn(0)
                && c1 == (Vector4)m.GetColumn(1)
                && c2 == (Vector4)m.GetColumn(2)
                && c3 == (Vector4)m.GetColumn(3);
        }
    }

    [System.Serializable]
    public struct Marca
    {
        [Tooltip("Coordenada LOCAL do quadrante — ja transladada.")]
        public int localX;
        public int localY;
        public TileBase tile;

        [Tooltip("Indice em 'transformacoes'. 0 e sempre a identidade.")]
        public int transformIndex;

        [Tooltip("Cor efetiva da celula. Branco quando nao ha tinta.")]
        public Color cor;
    }

    [Tooltip("Nome do Tilemap, no mesmo Grid do tabuleiro. Ex.: quebraMar.")]
    public string tilemapName;

    /// <summary>
    /// Paleta de transformacoes da camada. O indice 0 e SEMPRE a identidade.
    ///
    /// Paleta e nao valor-por-celula porque e assim que a coisa realmente e: o
    /// quebra-mar do mapa de teste sao 39 celulas com apenas 10 orientacoes — um
    /// sprite girado de 60 em 60 graus pra acompanhar a costa. A propria Unity
    /// guarda desse jeito (m_TileMatrixArray com contagem de referencia), e copiar a
    /// forma dela mantem o asset pequeno quando a camada crescer com o mundo.
    /// </summary>
    [Tooltip("Orientacoes usadas por esta camada. O indice 0 e a identidade.")]
    public List<Transformacao> transformacoes = new List<Transformacao>();

    public int Count => marcas != null ? marcas.Count : 0;

    [Tooltip("So as celulas que TEM tile. Camada decorativa e esparsa por natureza.")]
    public List<Marca> marcas = new List<Marca>();

    public Matrix4x4 GetMatriz(int index)
    {
        if (transformacoes == null || index < 0 || index >= transformacoes.Count)
            return Matrix4x4.identity;

        return transformacoes[index].ToMatrix();
    }
}
