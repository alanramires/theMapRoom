using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
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
/// Pinta terreno, camadas decorativas, construcoes e rotas. Unidades vem depois.
///
/// SO EM PLAY. Tudo que ele escreve e serializado, e a Batalha e uma cena so pra
/// todos os quadrantes: construir no Editor e salvar gravaria o layout de UM
/// quadrante na cena compartilhada. "Limpar tabuleiro" e a saida se isso ja
/// aconteceu.
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

    [Header("Volta")]
    [Tooltip(
        "Cena para onde o jogador volta quando a partida termina — mas SO se ela "
        + "tiver comecado na campanha. Partida aberta direto pela Batalha nao volta "
        + "pra lugar nenhum.")]
    [SerializeField] private string campaignSceneName = "Campanha";

    [Header("Comportamento")]
    [SerializeField] private bool buildOnAwake = true;
    [Tooltip("Apaga o tilemap de destino antes de pintar. A Batalha nasce vazia, mas isto torna o build repetivel.")]
    [SerializeField] private bool clearBeforeBuild = true;
    [SerializeField] private bool logBuild = true;

    private bool built;
    private int paintedCells;
    private int holeCells;
    private bool recordsCampaignResult;
    private bool aguardandoVolta;
    private bool voltando;
    private int frameDaConclusao;

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
        PlayerSlotId winnerSlot,
        TeamId winnerTeam,
        TeamId defeatedTeam,
        MatchController.VictoryReason reason,
        int turn)
    {
        if (active != this || !built || !recordsCampaignResult)
            return;

        // O SLOT, nao o time. A cor com que este slot lutou hoje nao volta amanha,
        // e o quadrante continua sendo dele.
        //
        // Sem vencedor para coroar (rendicao sem oponente vivo) nao ha dono novo —
        // mas a partida acabou do mesmo jeito, e a volta e armada de qualquer forma.
        if (winnerSlot.IsValid)
        {
            CampaignProgressStore.RecordOwner(
                MundoId,
                campanhaId,
                quadranteId,
                winnerSlot,
                turn);
        }

        aguardandoVolta = true;
        frameDaConclusao = Time.frameCount;
    }

    /// <summary>
    /// A VOLTA. Vitoria ou derrota, o painel aparece e o Enter devolve o jogador ao
    /// mapa de campanha.
    ///
    /// Mora aqui, e nao num controlador de UI, porque a pergunta que decide se ha
    /// volta e "esta partida veio da campanha?" — e quem sabe isso e quem recebeu o
    /// endereco (<see cref="recordsCampaignResult"/>). Partida aberta direto na
    /// Batalha, para testar um quadrante, nao volta pra lugar nenhum: continua
    /// terminando na tela de vitoria, como sempre terminou.
    /// </summary>
    private void Update()
    {
        if (!aguardandoVolta || voltando)
            return;

        // O mesmo Enter que confirmou a ultima acao nao pode ser o que sai da tela
        // de vitoria — a tela apareceria e sumiria no mesmo frame. Exige tecla nova.
        if (Time.frameCount <= frameDaConclusao || IsSubmitHeldNow())
            return;

        if (!WasSubmitPressedThisFrame())
            return;

        VoltarParaCampanha();
    }

    private void VoltarParaCampanha()
    {
        if (voltando)
            return;

        if (string.IsNullOrWhiteSpace(campaignSceneName))
        {
            Debug.LogError(
                "[Quadrante] A partida veio da campanha mas 'campaignSceneName' esta vazio — "
                + "nao ha para onde voltar. O jogador fica preso na tela de vitoria.",
                this);
            return;
        }

        voltando = true;
        RepublicarConfiguracaoDaPartida();

        Debug.Log($"[Quadrante] Partida encerrada; voltando para '{campaignSceneName}'.", this);
        SceneManager.LoadScene(campaignSceneName);
    }

    /// <summary>
    /// A IDA CONSOME A CONFIGURACAO; A VOLTA TEM DE REPUBLICA-LA.
    ///
    /// PartidaConfig e de consumo unico: o Awake do MatchController da Batalha ja
    /// aplicou e limpou. Voltar sem republicar faz a cena Campanha nascer com as
    /// cores SERIALIZADAS nela — e ai o quadrante que voce acabou de conquistar
    /// aparece pintado na cor de outra pessoa, porque o tint resolve o slot contra
    /// a lista errada.
    ///
    /// E a mesma travessia que a CampaignSelectionController faz na ida, so que ao
    /// contrario: exporta o estado desta partida e publica de novo.
    /// </summary>
    private void RepublicarConfiguracaoDaPartida()
    {
        MatchController match = FindAnyObjectByType<MatchController>();
        if (match == null)
        {
            Debug.LogWarning(
                "[Quadrante] Sem MatchController para exportar a configuracao da partida. "
                + "A cena Campanha vai nascer com as cores serializadas nela.",
                this);
            return;
        }

        List<int> teamIds = new List<int>();
        List<bool> flipXs = new List<bool>();
        List<bool> isAIs = new List<bool>();
        List<int> startMoneys = new List<int>();
        List<int> actualMoneys = new List<int>();
        List<int> incomePerTurns = new List<int>();
        List<bool> startMoneyApplied = new List<bool>();
        match.ExportPlayersState(
            teamIds, flipXs, isAIs, startMoneys, actualMoneys, incomePerTurns, startMoneyApplied);

        if (teamIds.Count < 2)
        {
            Debug.LogWarning(
                $"[Quadrante] A partida exportou {teamIds.Count} jogador(es); a volta nao "
                + "republica configuracao com menos de dois.",
                this);
            return;
        }

        TeamId[] teams = new TeamId[teamIds.Count];
        bool[] commandsAutomatic = new bool[teamIds.Count];
        for (int i = 0; i < teamIds.Count; i++)
        {
            teams[i] = (TeamId)teamIds[i];
            commandsAutomatic[i] = match.IsPlayerCommandServiceAutomatic(PlayerSlotId.FromIndex(i));
        }

        PartidaConfig.Set(
            teamIds.Count,
            teams,
            isAIs.ToArray(),
            flipXs.ToArray(),
            match.GameSetup,
            commandsAutomatic,
            campaignSceneName);

        AIController ai = FindAnyObjectByType<AIController>();
        if (ai != null)
            PartidaConfig.SetDifficulty(ai.AppliedDifficulty);
    }

    private static bool WasSubmitPressedThisFrame()
    {
        if (RemoteInput.ConfirmDownThisFrame())
            return true;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    private static bool IsSubmitHeldNow()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null &&
            (Keyboard.current.enterKey.isPressed || Keyboard.current.numpadEnterKey.isPressed))
            return true;
#endif
        return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
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

        // NAO CONSTROI FORA DO PLAY, e o motivo e o teste de aceitacao do projeto:
        // "duplique uma cena, aponte pros catalogos, e o mapa nasce VAZIO".
        //
        // A Batalha e UMA cena pra todos os quadrantes de todos os mundos, e TUDO
        // que o Build escreve e serializado — tiles, construcoes, camadas e rotas.
        // Construir no Editor e salvar grava o layout de UM quadrante na cena
        // compartilhada, e a partir dai todo quadrante nasce com a sobra do
        // anterior.
        //
        // E o modo de falha e o silencioso: so da erro onde as coordenadas nao
        // existirem no outro quadrante. Q1 e Q2 aqui compartilham faixa de x
        // ([-18,-3] e [-18,0]) — um contaminaria o outro sem UM aviso sequer.
        //
        // Pra ver um quadrante, entre em Play: buildOnAwake ja faz o trabalho. Pra
        // desfazer uma contaminacao que ja aconteceu, use "Limpar tabuleiro".
        if (!Application.isPlaying)
        {
            Debug.LogError(
                "[Quadrante] Build so roda em Play. Fora do Play ele gravaria o layout deste "
                + "quadrante DENTRO da cena de Batalha, que e compartilhada por todos os "
                + "quadrantes — e a contaminacao seguinte seria silenciosa. Entre em Play "
                + "(buildOnAwake ja constroi) ou use 'Limpar tabuleiro'.",
                this);
            return false;
        }

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

        int camadas = BuildCamadas(quadrante, map);

        // Construcoes DEPOIS do terreno, sempre: o spawner recusa celula ocupada e
        // precisa do tabuleiro no lugar pra converter celula em posicao de mundo.
        int construcoes = BuildConstrucoes(quadrante, map);

        // Rotas por ULTIMO entre os dados de tabuleiro: RebuildRoadVisuals valida
        // cada celula contra o terreno pintado (enforceLandSurfaceCells), entao
        // rodar antes da tinta secar reprovaria a rodovia inteira — IsRouteValid e
        // tudo-ou-nada.
        int trechos = BuildRotas(quadrante);

        // Unidades DEPOIS das construcoes: o spawner recusa celula ocupada, e uma
        // tropa inicial em cima do proprio QG e desenho legitimo na autoria.
        int unidades = BuildUnidades(quadrante);

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
                $"{unidades} unidade(s), {camadas} camada(s), {trechos} trecho(s) de rota, " +
                $"{quadrante.width}x{quadrante.height} em '{map.name}' " +
                $"(origem local {paintOrigin.x},{paintOrigin.y}; " +
                $"origem de autoria {quadrante.originX},{quadrante.originY}) " +
                $"em {watch.ElapsedMilliseconds} ms.",
                this);
        }

        return true;
    }

    /// <summary>
    /// Pinta as camadas decorativas — quebra-mar e o que mais o mundo listar.
    ///
    /// Enfeite: se a camada nao existir nesta cena, avisa e segue. O quadrante joga
    /// igual sem ela; so fica seco. Nao e motivo pra abortar o build.
    ///
    /// ⚠️ 'quebraMar' e o unico nome que a NEVOA fotografa
    /// (MatchController.RenderFogBreakwaterMemory). Outra camada aparece onde esta
    /// visivel e some onde esta so explorado.
    /// </summary>
    private int BuildCamadas(QuadranteData quadrante, Tilemap terreno)
    {
        if (quadrante.bakedCamadas == null || quadrante.bakedCamadas.Count == 0)
            return 0;

        int w = Mathf.Max(1, quadrante.width);
        int h = Mathf.Max(1, quadrante.height);
        int pintadas = 0;

        for (int i = 0; i < quadrante.bakedCamadas.Count; i++)
        {
            CamadaAssada camadaAssada = quadrante.bakedCamadas[i];
            if (camadaAssada == null || string.IsNullOrWhiteSpace(camadaAssada.tilemapName))
                continue;

            Tilemap destino = FindTilemapByNameOnGrid(camadaAssada.tilemapName, terreno);
            if (destino == null)
            {
                Debug.LogWarning(
                    $"[Quadrante] Camada '{camadaAssada.tilemapName}' nao existe nesta cena " +
                    "(mesmo Grid do tabuleiro). O quadrante joga sem ela.",
                    this);
                continue;
            }

            if (clearBeforeBuild)
                destino.ClearAllTiles();

            // Esparsa: so as celulas marcadas. Buraco nao aparece na lista — nao ha
            // o que pular, e por isso a camada pode ser rala do jeito que for sem
            // custar nada por celula vazia.
            int fora = 0;
            for (int m = 0; m < camadaAssada.marcas.Count; m++)
            {
                CamadaAssada.Marca marca = camadaAssada.marcas[m];
                if (marca.tile == null)
                    continue;

                // Marca fora do retangulo = bake anterior a um resize do quadrante.
                // Pintar assim vazaria enfeite pra fora do tabuleiro.
                if (marca.localX < 0 || marca.localX >= w
                    || marca.localY < 0 || marca.localY >= h)
                {
                    fora++;
                    continue;
                }

                Vector3Int cell =
                    new Vector3Int(paintOrigin.x + marca.localX, paintOrigin.y + marca.localY, 0);

                destino.SetTile(cell, marca.tile);

                // DESTRAVAR ANTES DE ORIENTAR. Um tile pode declarar LockTransform ou
                // LockColor, e nesse caso o tilemap IGNORA SetTransformMatrix e
                // SetColor calado — a peca nasceria apontando pro lado errado sem uma
                // linha de aviso. Como o valor assado ja e o EFETIVO (foi lido da cena
                // de autoria com os locks dela aplicados), destravar e reaplicar
                // reproduz o que o autor desenhou, e nao algo diferente.
                destino.SetTileFlags(cell, TileFlags.None);
                destino.SetTransformMatrix(cell, camadaAssada.GetMatriz(marca.transformIndex));
                destino.SetColor(cell, marca.cor);
            }

            if (fora > 0)
            {
                Debug.LogWarning(
                    $"[Quadrante] Camada '{camadaAssada.tilemapName}': {fora} marca(s) fora " +
                    $"do retangulo {w}x{h} — bake anterior a um resize. Asse de novo.",
                    this);
            }

            pintadas++;
        }

        return pintadas;
    }

    /// <summary>
    /// Devolve as rotas deste quadrante ao RoadNetworkManager da cena.
    ///
    /// SUBSTITUI, nunca empilha: ClearSceneRoadRoutes primeiro. Construir duas
    /// vezes tem de dar o mesmo numero de rotas, nao o dobro — e como a cena de
    /// Batalha e UMA so pra todos os quadrantes, "construir de novo" e o caso
    /// comum, nao a excecao.
    ///
    /// Resolve a estrutura POR ID no catalogo do proprio manager, e nao guarda
    /// referencia direta no assado: o catalogo diz o que uma rodovia E, e cinquenta
    /// mapas compartilham o mesmo. E a mesma escolha do ConstrucaoAssada.
    /// </summary>
    private int BuildRotas(QuadranteData quadrante)
    {
        bool temRotas = quadrante.bakedRotas != null && quadrante.bakedRotas.Count > 0;

        RoadNetworkManager network = FindFirstObjectByType<RoadNetworkManager>(FindObjectsInactive.Include);
        if (network == null)
        {
            if (!temRotas)
                return 0;

            Debug.LogWarning(
                $"[Quadrante] {quadrante.bakedRotas.Count} trecho(s) de rota assado(s), mas nao ha "
                + "RoadNetworkManager nesta cena. O quadrante joga SEM estrada — e sem erro, "
                + "porque estrada ausente so deixa o mapa mais lento.",
                this);
            return 0;
        }

        StructureDatabase catalogo = network.StructureDatabase;
        if (catalogo == null)
        {
            Debug.LogError(
                "[Quadrante] RoadNetworkManager sem StructureDatabase. Sem catalogo nao ha como "
                + "resolver id de estrutura, e nenhuma rota volta.",
                network);
            return 0;
        }

        // LIMPAR VEM ANTES DE SABER SE HA O QUE ESCREVER.
        //
        // A Batalha e UMA cena pra todos os quadrantes, e o Build e repetivel de
        // proposito. Sair cedo por "este quadrante nao tem estrada" deixaria as
        // rotas do quadrante ANTERIOR pintadas neste — o mapa A vazando pro mapa B,
        // sem erro nenhum, porque estrada sobrando so faz o mapa andar mais rapido.
        network.ClearSceneRoadRoutes();

        if (!temRotas)
        {
            network.RebuildRoadVisuals();
            return 0;
        }

        int aplicados = 0;
        int perdidos = 0;

        for (int i = 0; i < quadrante.bakedRotas.Count; i++)
        {
            RotaAssada trecho = quadrante.bakedRotas[i];
            if (trecho == null || trecho.celulas == null || trecho.celulas.Count == 0)
                continue;

            if (!catalogo.TryGetById(trecho.structureId, out StructureData estrutura)
                || estrutura == null)
            {
                perdidos++;
                Debug.LogWarning(
                    $"[Quadrante] Estrutura '{trecho.structureId}' (rota '{trecho.routeName}') nao "
                    + $"existe em '{catalogo.name}'. Trecho descartado.",
                    this);
                continue;
            }

            List<RoadRouteDefinition> destino = network.GetOrCreateRoadRoutes(estrutura);
            if (destino == null)
                continue;

            List<Vector3Int> celulas = new List<Vector3Int>(trecho.celulas.Count);
            for (int c = 0; c < trecho.celulas.Count; c++)
            {
                Vector3Int local = trecho.celulas[c];
                celulas.Add(new Vector3Int(paintOrigin.x + local.x, paintOrigin.y + local.y, 0));
            }

            destino.Add(new RoadRouteDefinition
            {
                routeName = trecho.routeName,
                // Sem dono a rota e tratada como legado/global e passa pelo filtro
                // de qualquer jeito; com dono ela declara de onde veio.
                ownerDatabase = catalogo,
                cells = celulas
            });

            aplicados++;
        }

        if (perdidos > 0)
        {
            Debug.LogError(
                $"[Quadrante] {perdidos} trecho(s) de rota perdido(s) por id nao encontrado. "
                + "O mapa joga sem essas estradas.",
                this);
        }

        // O lookup e cacheado na primeira consulta; escrever no bucket serializado
        // sem invalidar deixaria os consumidores lendo o mapa anterior.
        network.InvalidateRoutesLookup();
        network.RebuildRoadVisuals();

        return aplicados;
    }

    private static Tilemap FindTilemapByNameOnGrid(string targetName, Tilemap boardMap)
    {
        if (string.IsNullOrWhiteSpace(targetName) || boardMap == null)
            return null;

        Tilemap[] all = FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            Tilemap t = all[i];
            if (t == null
                || t.gameObject.scene != boardMap.gameObject.scene
                || t.layoutGrid != boardMap.layoutGrid)
            {
                continue;
            }

            if (string.Equals(t.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }

    /// <summary>
    /// Devolve a cena ao estado de nascenca: sem tile, sem construcao, sem enfeite,
    /// sem rota.
    ///
    /// Existe pelo mesmo motivo do guarda no Build — e a saida pra uma cena que JA
    /// foi contaminada, seja por um build no Editor de antes do guarda, seja por
    /// qualquer coisa colada ali a mao. Roda no Editor de proposito: e exatamente
    /// onde o estrago mora, e depois dela a Batalha volta a passar no teste de
    /// aceitacao ("duplique a cena e ela nasce vazia").
    /// </summary>
    [ContextMenu("Limpar tabuleiro")]
    public void LimparTabuleiro()
    {
        Tilemap map = ResolveTargetTilemap();
        if (map != null)
            map.ClearAllTiles();

        ClearConstrucoes();

        // As camadas decorativas sao tilemaps IRMAOS: ClearAllTiles no tabuleiro nao
        // toca nelas, e enfeite orfao sobreviveria a limpeza sem chamar atencao.
        int camadasLimpas = 0;
        if (mundo?.camadasDecorativas != null && map != null)
        {
            for (int i = 0; i < mundo.camadasDecorativas.Count; i++)
            {
                Tilemap camada = FindTilemapByNameOnGrid(mundo.camadasDecorativas[i], map);
                if (camada == null)
                    continue;

                camada.ClearAllTiles();
                camadasLimpas++;
            }
        }

        RoadNetworkManager network =
            FindFirstObjectByType<RoadNetworkManager>(FindObjectsInactive.Include);
        if (network != null)
        {
            network.ClearSceneRoadRoutes();
            network.RebuildRoadVisuals();
        }

        built = false;
        paintedCells = 0;
        holeCells = 0;

        Debug.Log(
            $"[Quadrante] Tabuleiro limpo: tiles, construcoes, {camadasLimpas} camada(s) "
            + "decorativa(s) e rotas. A cena voltou a nascer vazia — salve para gravar isso.",
            this);
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

        // A CONFIGURACAO DA PARTIDA TEM DE CHEGAR ANTES DA TINTA.
        //
        // Este componente roda em -9000 e o Awake do MatchController em 0, entao
        // sem esta chamada a lista de jogadores aqui ainda e a SERIALIZADA na cena
        // Batalha — e todo dono resolvido abaixo sai com a cor errada. Idempotente:
        // o Awake dele chama de novo e nao entra.
        match?.EnsurePartidaConfigApplied();

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

            // O DONO SAI DO SLOT, NUNCA DA COR ASSADA.
            //
            // O bake grava a cor com que a peca foi PINTADA na cena de autoria, e
            // essa cor nao e a da partida: o jogador escolhe as duas cores no menu.
            // Assar Azul e nascer Azul e o mesmo bug que a regra da casa ja nomeia —
            // "cor de time nunca sai do slot direto".
            //
            // slotIndex -1 e conteudo de time FIXO e nao acompanha o slot (a mesma
            // regra do recolorir do tutorial): ai vale a cor assada, que para toda
            // construcao neutra ja e Neutral.
            TeamId dono = c.slotIndex >= 0 && match != null
                ? match.GetTeamIdForSlot(c.slotIndex)
                : c.teamId;

            GameObject go = spawner.SpawnAtCell(c.constructionId, dono, cell);
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

                    // SetOwnerSlot, nao SetSlotIndex: o segundo so escreve o campo e
                    // deixa a cor como estava, entao slot e time podiam divergir. Este
                    // deriva o time do slot, e e o mesmo caminho que a captura usa.
                    //
                    // Sem MatchController na cena ele resolveria TUDO como Neutral —
                    // um tabuleiro inteiro sem dono, sem erro. Ai vale o campo cru e a
                    // cor assada, que e o que havia antes.
                    if (match != null)
                        manager.SetOwnerSlot(c.slotIndex);
                    else
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
    /// Tropa inicial. Lista vazia e o caso normal hoje — os quadrantes do fixture
    /// abrem sem ninguem em campo e os dois lados comecam comprando. Isto existe
    /// para o autor poder dar tropa a um lado (ou aos dois) so pintando na cena de
    /// autoria, sem nenhuma configuracao a parte.
    ///
    /// O DONO SAI DO SLOT, como em tudo mais que atravessa autoria e partida:
    /// SpawnAtCellForSlot resolve o time visual do slot e ja aplica o slotIndex —
    /// e o caminho que o proprio spawner usa e o unico em que cor e slot nao podem
    /// divergir. Unidade sem slot (facção de time fixo) cai no caminho por cor.
    /// </summary>
    private int BuildUnidades(QuadranteData quadrante)
    {
        if (quadrante.bakedUnidades == null || quadrante.bakedUnidades.Count == 0)
            return 0;

        UnitSpawner spawner = FindAnyObjectByType<UnitSpawner>();
        if (spawner == null)
        {
            Debug.LogError(
                "[Quadrante] Ha unidades assadas mas a cena nao tem UnitSpawner. O quadrante "
                + "pinta e nasce sem a tropa inicial que o autor desenhou.",
                this);
            return 0;
        }

        UnitDatabase database = spawner.UnitDatabase;
        if (database == null)
        {
            Debug.LogError(
                "[Quadrante] O UnitSpawner desta cena esta sem UnitDatabase — nenhum id de "
                + "unidade resolve, e a tropa inicial nao nasce.",
                this);
            return 0;
        }

        MatchController match = FindAnyObjectByType<MatchController>();
        int spawned = 0;

        for (int i = 0; i < quadrante.bakedUnidades.Count; i++)
        {
            UnidadeAssada u = quadrante.bakedUnidades[i];
            if (u == null || string.IsNullOrWhiteSpace(u.unitId))
                continue;

            if (!database.TryGetById(u.unitId, out UnitData data) || data == null)
            {
                Debug.LogWarning(
                    $"[Quadrante] Unidade assada '{u.unitId}' nao existe no UnitDatabase. Pulada.",
                    this);
                continue;
            }

            Vector3Int cell = new Vector3Int(
                paintOrigin.x + u.localX,
                paintOrigin.y + u.localY,
                0);

            GameObject go;
            if (u.slotIndex >= 0 && match != null)
            {
                AvisarSlotDeUnidadeInexistente(u, match);
                go = spawner.SpawnAtCellForSlot(data, PlayerSlotId.FromIndex(u.slotIndex), cell);
            }
            else
            {
                // Sem slot: time fixo, a cor assada e a verdade — a mesma excecao das
                // construcoes.
                go = spawner.SpawnAtCell(data, u.teamId, cell);
            }

            if (go == null)
            {
                Debug.LogWarning($"[Quadrante] Nao nasceu '{u}' na celula {cell}.", this);
                continue;
            }

            spawned++;
        }

        return spawned;
    }

    /// <summary>
    /// Mesmo aviso das construcoes, e pelo mesmo motivo: a cena de autoria e a
    /// partida tem listas de jogadores diferentes e nada compara as duas. Unidade
    /// assada num slot que a partida nao tem simplesmente nao nasce — o
    /// SpawnAtCellForSlot recusa slot invalido e devolve null, e sem este aviso o
    /// sintoma seria "a tropa da IA sumiu" sem uma linha no Console.
    /// </summary>
    private void AvisarSlotDeUnidadeInexistente(UnidadeAssada u, MatchController match)
    {
        if (match == null || match.IsValidPlayerSlotIndex(u.slotIndex))
            return;

        Debug.LogWarning(
            $"[Quadrante] '{u.unitId}' assada no slot {u.slotIndex}, que NAO EXISTE nesta "
            + "partida. Ela nao vai nascer. Repinte na cena de autoria com um slot valido "
            + "e asse de novo.",
            this);
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
