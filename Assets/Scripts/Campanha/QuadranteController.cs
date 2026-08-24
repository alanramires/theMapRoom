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
/// Por isso existe <see cref="BoardReady"/>. A cena de batalha sempre nasceu
/// pronta, entao a garantia era acidental; com a cena vazia ela desaparece, e o
/// portao e o que a substitui.
///
/// O primeiro consumidor dele e o refresh visual das construcoes: elas nascem
/// durante este Awake e cacheiam um estado calculado contra managers que ainda nao
/// inicializaram. Quem mais depender de "o tabuleiro ja existe" entra no FIM do
/// Build, e nao espalhado pelo Start de cada um.
///
/// Etapa 1 pinta terreno e construcoes. Estruturas e unidades vem depois.
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
    private bool recordsCampaignResult;

    public bool Built => built;
    public int PaintedCells => paintedCells;
    public string MundoId => mundo != null ? mundo.mundoId : string.Empty;
    public string CampanhaId => campanhaId;
    public string QuadranteId => quadranteId;

    private void OnEnable()
    {
        MatchController.OnMatchConcluded += HandleMatchConcluded;
    }

    private void OnDisable()
    {
        MatchController.OnMatchConcluded -= HandleMatchConcluded;
    }

    private void Awake()
    {
        active = this;
        built = false;
        recordsCampaignResult = false;

        // O ENDERECO VEM DE FORA QUANDO ALGUEM O MANDOU.
        //
        // A cena Campanha publica (campanhaId, quadranteId) no PartidaConfig antes
        // de carregar esta cena. Consumir aqui, no Awake, e antes do Build, e o que
        // faz o save/menu MANDAR no que e pintado em vez de ser conferido depois —
        // "quadrante errado" deixa de ser representavel porque nao existe tabuleiro
        // antes de alguem dizer qual e.
        //
        // Sem pedido pendente, valem os campos do Inspector: e assim que se testa
        // um quadrante direto, sem passar pelo menu.
        if (PartidaConfig.TryConsumeQuadrante(out string pedidoCampanha, out string pedidoQuadrante))
        {
            campanhaId = pedidoCampanha;
            quadranteId = pedidoQuadrante;
            recordsCampaignResult = true;

            if (logBuild)
                Debug.Log($"[Quadrante] Endereco recebido do PartidaConfig: '{campanhaId}/{quadranteId}'.", this);
        }

        if (buildOnAwake)
            Build();
    }

    private void OnDestroy()
    {
        if (active == this)
            active = null;
    }

    private void HandleMatchConcluded(
        TeamId winnerTeam,
        TeamId defeatedTeam,
        MatchController.VictoryReason reason,
        int turn)
    {
        if (active != this || !built || !recordsCampaignResult || winnerTeam == TeamId.Neutral)
            return;

        CampaignProgressStore.RecordOwner(
            MundoId,
            campanhaId,
            quadranteId,
            winnerTeam,
            turn);
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
        {
            map.ClearAllTiles();
            ClearConstrucoes();
        }

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

        // Construcoes DEPOIS do terreno, sempre: o spawner recusa celula ocupada e
        // precisa do tabuleiro no lugar pra converter celula em posicao de mundo.
        int construcoes = BuildConstrucoes(quadrante, map);

        watch.Stop();
        built = true;

        // O PORTAO GANHA SEU PRIMEIRO CONSUMIDOR.
        //
        // As construcoes nascem aqui, no Awake em -9000 — ANTES do Awake do
        // MatchController. Cada uma calcula seu estado visual nesse instante e
        // GUARDA EM CACHE (cachedOccupantShouldDarken e amigos). Os refreshes
        // seguintes chegam com force:false e retornam cedo quando o cache "bate",
        // entao um valor calculado contra um MatchController ainda nao
        // inicializado pode ficar congelado.
        //
        // Numa cena que nascia pronta isso nunca acontecia: a construcao ja estava
        // la quando tudo inicializou. Com o quadrante pintado em runtime, a
        // garantia sumiu — e este e o tipo de coisa que o BoardReady existe pra
        // substituir.
        ConstructionManager.RefreshAllOccupancyVisuals();

        if (logBuild)
        {
            Debug.Log(
                $"[Quadrante] '{campanha.displayName}/{quadrante.displayName}' construido: " +
                $"{paintedCells} tiles, {holeCells} buraco(s), {construcoes} construcao(oes), " +
                $"{quadrante.width}x{quadrante.height} em '{map.name}' " +
                $"(origem local {paintOrigin.x},{paintOrigin.y}; " +
                $"origem de autoria {quadrante.originX},{quadrante.originY}) " +
                $"em {watch.ElapsedMilliseconds} ms.",
                this);
        }

        return true;
    }

    /// <summary>
    /// Sem isto o build nao e repetivel: ClearAllTiles limpa o chao, mas as
    /// construcoes ficariam, e o SpawnAtCell RECUSA celula ocupada — o segundo
    /// build viraria uma fila de avisos em vez de um tabuleiro.
    /// </summary>
    private void ClearConstrucoes()
    {
        ConstructionManager[] all =
            FindObjectsByType<ConstructionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            ConstructionManager c = all[i];
            if (c == null || c.gameObject.scene != gameObject.scene)
                continue;

            if (Application.isPlaying)
                Destroy(c.gameObject);
            else
                DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>
    /// Planta as construcoes assadas. Reusa <see cref="ConstructionSpawner"/> — o
    /// mesmo caminho que todo carregamento de save ja exercita, entao nao e codigo
    /// novo de spawn, e o caminho que ja roda.
    /// </summary>
    private int BuildConstrucoes(QuadranteData quadrante, Tilemap map)
    {
        if (quadrante.bakedConstrucoes == null || quadrante.bakedConstrucoes.Count == 0)
            return 0;

        ConstructionSpawner spawner = FindAnyObjectByType<ConstructionSpawner>();
        if (spawner == null)
        {
            Debug.LogError(
                "[Quadrante] Sem ConstructionSpawner nesta cena — as construcoes assadas nao " +
                "podem ser plantadas. O tabuleiro pinta, mas nao joga.",
                this);
            return 0;
        }

        // O catalogo e da CENA e e compartilhado, como o UnitDatabase: ele diz o
        // que uma construcao E, e um QG e um QG em qualquer mapa. Nao ha catalogo
        // por mundo nem por quadrante.
        if (spawner.ConstructionDatabase == null)
        {
            Debug.LogError(
                "[Quadrante] O ConstructionSpawner desta cena esta sem ConstructionDatabase. " +
                "Aponte-o pro catalogo compartilhado de construcoes — sem ele nenhum id resolve, " +
                "e o quadrante pinta o chao e nasce sem QG.",
                this);
            return 0;
        }

        MatchController match = FindAnyObjectByType<MatchController>();
        int planted = 0;

        for (int i = 0; i < quadrante.bakedConstrucoes.Count; i++)
        {
            ConstrucaoAssada c = quadrante.bakedConstrucoes[i];
            if (c == null || string.IsNullOrWhiteSpace(c.constructionId))
                continue;

            Vector3Int cell = new Vector3Int(
                paintOrigin.x + c.localX,
                paintOrigin.y + c.localY,
                0);

            GameObject go = spawner.SpawnAtCell(c.constructionId, c.teamId, cell);
            if (go == null)
            {
                Debug.LogWarning($"[Quadrante] Nao plantou '{c}' na celula {cell}.", this);
                continue;
            }

            ConstructionManager manager = go.GetComponent<ConstructionManager>();
            if (manager != null)
            {
                // A CELULA LOGICA VEM DAQUI, nao da ida-e-volta pelo mundo.
                //
                // SpawnAtCell converte celula -> centro do mundo, e o Spawn converte
                // de volta mundo -> celula. Se a volta cair num vizinho, o predio
                // FICA VISUALMENTE CERTO e passa a achar que mora noutro hex — e ai
                // HandleUnitOccupancyChanged compara contra a celula errada e nunca
                // dispara. O sintoma e exatamente "o predio nao reage a unidade que
                // entra nem a que sai", com todo o resto parecendo normal.
                Vector3Int derivada = manager.CurrentCellPosition;
                derivada.z = 0;
                if (derivada != cell)
                {
                    Debug.LogWarning(
                        $"[Quadrante] '{c.constructionId}': o spawn derivou a celula {derivada} " +
                        $"mas a assada e {cell}. A ida-e-volta celula->mundo->celula nao fechou; " +
                        "corrigindo para a assada.",
                        this);
                }

                manager.SetCurrentCellPosition(cell);

                // O spawn so recebe o TIME. Slot, setor, ancora e pontos de captura
                // vem a parte — e nenhum deles e cosmetico:
                //   slot     decide producao, renda e vitoria
                //   setor    e por onde o planner da IA le o tabuleiro, e o default
                //            do enum e Alpha (nao None): omitir nao da erro, da
                //            plano degenerado em silencio
                //   captura  o PREFAB molde carrega 40 gravado, e nem Setup nem
                //            Apply o corrigem. Todo caminho de spawn nasce com 40; o
                //            de save so nao mostra porque aplica o estado depois
                if (c.slotIndex >= 0)
                {
                    AvisarSlotInexistente(c, match);
                    manager.SetSlotIndex(c.slotIndex);
                }

                manager.SetSector(c.sector);
                manager.SetAnchorSector(c.isAnchorSector);

                // ANTES dos pontos de captura: o siteRuntime traz o
                // capturePointsMax, e "-1 = usa o maximo" precisa do maximo certo
                // ja no lugar.
                if (c.siteRuntime != null)
                    manager.ApplySiteRuntime(c.siteRuntime);

                AplicarPontosDeCaptura(manager, c);
            }

            planted++;
        }

        return planted;
    }

    /// <summary>
    /// A cena de AUTORIA e a de BATALHA tem listas de jogadores diferentes, e nada
    /// compara as duas. Construcao assada num slot que a partida nao tem nasce
    /// NEUTRA em silencio — e junto some o QG do jogador, o foco do cursor nele, a
    /// renda e a condicao de vitoria. Um aviso custa nada e entrega os tres
    /// sintomas de uma vez.
    /// </summary>
    private void AvisarSlotInexistente(ConstrucaoAssada c, MatchController match)
    {
        if (match == null || match.IsValidPlayerSlotIndex(c.slotIndex))
            return;

        Debug.LogWarning(
            $"[Quadrante] '{c.constructionId}' assado no slot {c.slotIndex}, que NAO EXISTE nesta " +
            "partida. Vai nascer Neutral — sem dono, sem renda, e o cursor nao vai achar QG " +
            "desse jogador. Repinte na cena de autoria com um slot valido e asse de novo.",
            this);
    }

    /// <summary>
    /// -1 significa "usa o maximo do tipo" — a mesma convencao do antigo
    /// ConstructionFieldEntry. Valor >= 0 e intencao de autoria: predio comecando
    /// meio capturado.
    ///
    /// Isto precisa ser aplicado porque o PREFAB molde carrega 40 gravado e nem
    /// Setup nem Apply o corrigem: sem esta linha um QG de 60 nasce com 40.
    /// </summary>
    private static void AplicarPontosDeCaptura(ConstructionManager manager, ConstrucaoAssada c)
    {
        int alvo = c.initialCapturePoints >= 0
            ? c.initialCapturePoints
            : manager.CapturePointsMax;

        manager.SetCurrentCapturePoints(alvo);
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
