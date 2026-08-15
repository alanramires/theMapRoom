using System.Diagnostics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Debug = UnityEngine.Debug;

/// <summary>
/// Constroi o tabuleiro de um quadrante numa cena VAZIA (Batalha).
///
///   Mundo (asset)
///    └─ Campanha    Africa, Asia, Europa
///        └─ Quadrante   Nigeria, Congo...   ← este componente pinta UM deles
///
/// Recebe um endereco — (campanhaId, quadranteId) — e pinta o que estiver assado
/// naquele quadrante. Hoje o endereco vem do Inspector; mais pra frente vem do
/// save, e ai ele deixa de ser conferido e passa a MANDAR: a cena nasce vazia, o
/// save diz o que pintar, pinta, e so entao as pecas voltam. "Save no mapa
/// errado" deixa de ser representavel.
///
/// ORDEM E CONTRATO, NAO DETALHE. Roda em -9000, antes de tudo (o mais baixo do
/// projeto ate agora era -8500), porque quem consultar o tabuleiro antes da tinta
/// secar recebe resposta VAZIA — e o SectorManager, que se reconstroi sozinho na
/// primeira consulta, assa o vazio e CACHEIA. Sem erro nenhum no console; so um
/// plano degenerado.
///
/// Por isso existe <see cref="BoardReady"/>. Hoje ninguem le esse portao: a cena
/// de batalha sempre nasceu pronta, entao a garantia era acidental. Com a cena
/// vazia ela desaparece, e o portao e o que a substitui.
///
/// Etapa 1 pinta SO TILES. Construcoes e estruturas vem depois, nessa ordem.
/// </summary>
[DefaultExecutionOrder(-9000)]
public class QuadranteController : MonoBehaviour
{
    private static QuadranteController active;

    /// <summary>
    /// O portao. Falso ate a pintura terminar. Nada deve consultar o tabuleiro
    /// antes disto virar verdade.
    /// </summary>
    public static bool BoardReady => active != null && active.built;

    public static QuadranteController Active => active;

    [Header("Endereco")]
    [SerializeField] private MundoData mundo;
    [SerializeField] private string campanhaId = "fixture";
    [SerializeField] private string quadranteId = "Quadrante A";

    [Header("Destino")]
    [Tooltip("Se vazio, resolve pelo CursorController.BoardTilemap da cena.")]
    [SerializeField] private Tilemap targetTilemap;
    [Tooltip("Onde a celula local (0,0) cai no tilemap. O dado e sempre local; isto e so enquadramento.")]
    [SerializeField] private Vector2Int paintOrigin = Vector2Int.zero;

    [Header("Comportamento")]
    [SerializeField] private bool buildOnAwake = true;
    [Tooltip("Apaga o tilemap de destino antes de pintar. A Batalha nasce vazia, mas isto torna o build repetivel.")]
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool logBuild = true;

    private bool built;
    private int paintedCells;
    private int holeCells;

    public bool Built => built;
    public int PaintedCells => paintedCells;
    public string CampanhaId => campanhaId;
    public string QuadranteId => quadranteId;

    private void Awake()
    {
        active = this;
        built = false;

        if (buildOnAwake)
            Build();
    }

    private void OnDestroy()
    {
        if (active == this)
            active = null;
    }

    /// <summary>Endereco vindo de fora (save, tela de campanha). Nao pinta sozinho.</summary>
    public void SetAddress(string campanha, string quadrante)
    {
        campanhaId = campanha;
        quadranteId = quadrante;
        built = false;
    }

    [ContextMenu("Build")]
    public bool Build()
    {
        built = false;
        paintedCells = 0;
        holeCells = 0;

        if (mundo == null)
        {
            Debug.LogError("[Quadrante] Sem MundoData atribuido.", this);
            return false;
        }

        // Diagnostico separa as duas falhas: "a campanha nao existe" e "a campanha
        // existe mas o quadrante nao". Sem isso a mensagem manda adivinhar qual das
        // duas — e a resposta esta sempre a um passo de distancia.
        if (!mundo.TryGetCampanha(campanhaId, out BlocoData bloco, out CampanhaData campanha))
        {
            Debug.LogError(
                $"[Quadrante] Campanha '{campanhaId}' nao existe em nenhum bloco do mundo " +
                $"'{mundo.name}'. Ele tem: [{DescreverCampanhas()}]",
                this);
            return false;
        }

        if (!campanha.TryGetQuadrante(quadranteId, out QuadranteData quadrante))
        {
            Debug.LogError(
                $"[Quadrante] Campanha '{campanha.displayName}' achada, mas sem quadrante " +
                $"'{quadranteId}'. Ela tem {campanha.quadrantes?.Count ?? 0}: " +
                $"[{DescreverQuadrantes(campanha)}]  " +
                "Se estiver vazia, o bake ainda nao rodou: abra a cena de autoria e use " +
                "Tools > Utils > Map Helper.",
                this);
            return false;
        }

        if (!quadrante.HasBake)
        {
            Debug.LogError(
                $"[Quadrante] '{quadrante}' nao esta assado: " +
                $"{quadrante.bakedTiles?.Count ?? 0} tiles para {quadrante.CellCount} celulas. " +
                "Rode o bake no Map Helper com a cena de autoria aberta.",
                this);
            return false;
        }

        Tilemap map = ResolveTargetTilemap();
        if (map == null)
        {
            Debug.LogError("[Quadrante] Nenhum tilemap de destino nesta cena.", this);
            return false;
        }

        Stopwatch watch = Stopwatch.StartNew();

        if (clearBeforeBuild)
            map.ClearAllTiles();

        for (int localY = 0; localY < quadrante.height; localY++)
        {
            for (int localX = 0; localX < quadrante.width; localX++)
            {
                TileBase tile = quadrante.GetBakedTile(localX, localY);
                if (tile == null)
                {
                    // Buraco e valido: o retangulo pode conter celula sem tile.
                    holeCells++;
                    continue;
                }

                map.SetTile(
                    new Vector3Int(paintOrigin.x + localX, paintOrigin.y + localY, 0),
                    tile);
                paintedCells++;
            }
        }

        watch.Stop();
        built = true;

        if (logBuild)
        {
            Debug.Log(
                $"[Quadrante] '{campanha.displayName}/{quadrante.displayName}' construido: " +
                $"{paintedCells} tiles, {holeCells} buraco(s), " +
                $"{quadrante.width}x{quadrante.height} em '{map.name}' " +
                $"(origem local {paintOrigin.x},{paintOrigin.y}; " +
                $"origem de autoria {quadrante.originX},{quadrante.originY}) " +
                $"em {watch.ElapsedMilliseconds} ms.",
                this);
        }

        return true;
    }

    private string DescreverCampanhas()
    {
        if (mundo?.blocos == null || mundo.blocos.Count == 0)
            return "nenhum bloco";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool any = false;

        for (int i = 0; i < mundo.blocos.Count; i++)
        {
            BlocoData b = mundo.blocos[i];
            if (b?.campanhas == null)
                continue;

            for (int j = 0; j < b.campanhas.Count; j++)
            {
                CampanhaData c = b.campanhas[j];
                if (c == null)
                    continue;
                if (any) sb.Append(", ");
                sb.Append($"'{c.campanhaId}' (bloco '{b.blocoId}')");
                any = true;
            }
        }

        return any ? sb.ToString() : "nenhuma campanha";
    }

    private static string DescreverQuadrantes(CampanhaData campanha)
    {
        if (campanha?.quadrantes == null || campanha.quadrantes.Count == 0)
            return "vazio";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < campanha.quadrantes.Count; i++)
        {
            QuadranteData q = campanha.quadrantes[i];
            if (i > 0) sb.Append(", ");
            sb.Append(q == null
                ? "<null>"
                : $"'{q.quadranteId}'{(q.HasBake ? string.Empty : " (SEM BAKE)")}");
        }

        return sb.ToString();
    }

    private Tilemap ResolveTargetTilemap()
    {
        if (targetTilemap != null)
            return targetTilemap;

        CursorController cursor = FindAnyObjectByType<CursorController>();
        if (cursor != null && cursor.BoardTilemap != null)
        {
            targetTilemap = cursor.BoardTilemap;
            return targetTilemap;
        }

        return null;
    }
}
