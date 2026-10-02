# v9.2.0 — o jogo aguenta o celular de quem testa

**O primeiro MVP estável no ar.** Publicado no Unity Play e já com gente testando.

A v9.1.0 foi o primeiro build de publicação. O autor mostrou a um amigo pelo
celular, e a tela da Campanha não escolhia quadrante com o dedo. Depois, o
celular quebrava ao entrar na Campanha. Depois, crashava com 17 unidades em
campo. Este dia foi transformar "funciona no PC" em "aguenta o celular de quem
testa". No fim, o build publicado segurou **25 unidades em combate com ~660 MB,
o celular em status "normal" o tempo todo e nenhum erro**.

O fio do dia está numa frase do autor:

> *"são 3 coisas que temos que validar: teclado, mouse e dedo"*

E numa constatação que veio da medição, não do código: **o Simulator da Unity
não reproduz o navegador do celular**. Dentro do Editor "funcionava tudo".

---

## Frente 1 — o dedo na Campanha

A tela de Campanha nasceu só para teclado: o quadrante andava com as setas, e o
Enter abria a confirmação. O cursor do tabuleiro, que já entende toque, fica
desligado nessa cena. No celular não havia como escolher nada.

`CampaignSelectionController` ganhou a mesma regra do cursor da batalha: o toque
vale quando o dedo **solta**, sem arrasto e com um dedo só. O primeiro toque
seleciona o quadrante e o segundo, no mesmo quadrante, abre a confirmação. O
clique do mouse segue o mesmo caminho.

Uma armadilha própria da web: o navegador do celular dispara um **clique de mouse
sintético** depois de cada toque. Sem cuidado, um toque só selecionaria e
confirmaria. O mouse passa a ser ignorado por 0,6 s depois de qualquer toque.

Um toque fora do menu da Campanha fecha o menu. Mas só conta como fora o mapa ou
o fundo do próprio menu, não outro botão: o toque que abre o menu solta em cima
do botão de menu e o fecharia no mesmo gesto.

---

## Frente 2 — o botão de menu no turno da IA

O autor pôs um `menu_shortcut` nas duas cenas, como o "ESC do dedo". Na Batalha,
no turno da IA, ele **não seguia o caminho do ESC**:
- se a IA estava no meio de uma ação, ele chamava `ForceNeutral()` e desfazia a
  ação por fora da transação, violando o invariante transacional do CLAUDE.md;
- se não dava para abrir, só tocava o erro, sem pedir pausa.

Agora ESC e botão chamam o mesmo `RequestMenuDuringAiTurn`. A IA termina a ação
em andamento, para, e o menu abre no Neutral. Um segundo toque com o pedido
pendente desiste dele.

O **Resumo do turno** dizia "Invalid action". Era o texto genérico de qualquer
som de erro sem mensagem própria. Havia duas causas: no turno da IA o jornal é
apagado de propósito, e agora o botão fica desativado; num turno sem nada a
relatar o jornal nem existe, e agora aparece "nada a relatar", com o som de
confirmar e sem o bipe que eu tinha posto a mais.

---

## Frente 3 — o celular morria por memória, não por bug

### Como foi medido

Montei uma captura pelo `adb` e pelo DevTools Protocol. Ela grava o Console da
aba do jogo e a memória do processo direto do Android (`dumpsys meminfo`), sem
tocar no jogo. Foi o que separou palpite de fato.

**A curva que explicou o crash** (build dev, Galaxy A15, 3,6 GB de RAM):

```text
tela de entrada      ~165 MB
Campanha             ~350 MB
Batalha, sem compra  ~775 MB   status moderate
turno 2              ~915 MB   status low
turno 4, 18 unidades ~925 MB   status critical → aba morta, sem erro nenhum
```

Nenhum erro no log: o Android mata o processo sem avisar. O crash das "17
unidades" não vinha das unidades, vinha do tempo de jogo levando o celular ao
limite.

### O que pesava

- **Música.** Na web o navegador decodifica o MP3 **inteiro** em áudio bruto. O
  `map select` de 7,4 min estéreo ocupava ~156 MB. E as quatro faixas `team` não
  saem da memória depois de tocar, porque o `AudioManager` atravessa as cenas
  segurando todas: ~540 MB numa partida longa.
- **Texturas.** Os sprites de 886×1024 não comprimem, porque 886 não é múltiplo
  de 4: são 3,5 MB cada. E o formato DXT é de PC. O log mostrou ~100 avisos
  `DXT5 ... not supported, decompressing texture`: o celular descomprimia tudo no
  processador.
- **Logs.** TurnPerf, FrameSpike e AI Intel geravam centenas de linhas por turno
  no Console do navegador.

### O que mudou

- **Áudio:** todas as trilhas em mono e a 22 050 Hz; o `map select` virou um loop
  de 4 min; os originais saíram do projeto. Total das quatro `team`: ~540 → ~123 MB.
- **Texturas:** override Web com Max Size 512 e ASTC (feito pelo autor no Inspector).
- **Logs:** `ReleaseLogFilter`, uma chave só. No build Release passam apenas
  erros; no Editor e no Development Build tudo continua.
- **`MatchMusicAudioManager`:** o `OnValidate` reatribuía as trilhas pelo **nome
  do arquivo** a cada edição no Inspector. Arrastar `team0_mono` voltava sozinho
  para `team0`, e a playlist era refeita com todo áudio da pasta, original e mono
  juntos, os dois no build. Agora ele só preenche campo vazio.

### O LTO travava o Carregar Jogo

O primeiro build com **Runtime Speed with LTO** travou ao tocar em Carregar Jogo:
`RuntimeError: unreachable`, programa parado e a música continuando. O isolamento:
- os saves estavam inocentes: os dois do Unity Play, puxados do IndexedDB do
  celular e injetados no build dev, abriram normal;
- o dev não travava;
- o Release **sem LTO** abriu normal.

A causa exata dentro do LTO não foi achada, porque o build com LTO não tinha
símbolos. **Publicar sem LTO** até haver motivo.

**Resultado no ar:** 25 unidades em combate, ~640–685 MB, status "normal" o tempo
todo, nenhum erro, e o autor sentiu o jogo mais rápido.

---

## Frente 4 — APC esperando quem não vai embarcar

Três APCs paravam na base, em cima dos prédios e bloqueando a produção,
esperando um soldado ferido. O soldado em reparo **nunca decide embarcar**,
porque o reparo é decidido antes do embarque. A única exceção é a evacuação, com
inimigo visível por perto. O APC não fazia essa pergunta.

`WillShuttleCandidateBoard` faz o APC perguntar o mesmo que o passageiro: um
ferido sem inimigo por perto não é candidato. Vale nos dois lugares em que o APC
escolhe passageiro (`FindBestShuttleCandidate` e `FindNearestPlanCapturer`). Um
ferido em perigo continua candidato, então a evacuação segue funcionando. **Só
compilou**: falta ver em partida.

---

## Autoria, cenas e churn

- **Mundo Fixture + Fixture.unity:** o autor corrigiu as ofertas das construções
  do Fixture (fábricas que não vendiam depois de mudar de dono) e refez o bake.
  Não acompanhei os detalhes.
- **Cenas:** os overrides do AudioManager que guardavam a playlist antiga foram
  revertidos para seguir o prefab. Na Campanha, o debug ficou desligado (logs de
  IA, FrameSpike, start on pause).
- **Lista de cenas do build:** só Tela de Entrada, Campanha e Batalha.
- `docs/modding/publicando na web.md`: nota do autor sobre testar Web no celular.
  Está em ANSI; os acentos quebram em leitor UTF-8.

---

## Meus erros do dia (para não repetir)

- **Eu disse que "Force To Mono" ficava na aba Web.** Não fica: vale para todas
  as plataformas.
- **Eu disse que dava para fazer override de 22 kHz na aba Web.** Não existe na
  Web, onde a Unity só oferece formato e qualidade. A taxa teve de ir para o
  próprio arquivo.
- **Recomendei exportar a 64k e depois voltei atrás.** A Unity recodifica, então
  o bitrate de origem é só qualidade de partida: 128k mono é melhor.
- **O meu medidor de memória derrubou a aba.** `Runtime.queryObjects` varre o
  heap inteiro. Troquei por `dumpsys`, de fora do navegador.
- **Li o RSS em vez do PSS no `dumpsys`.** O número real era bem pior (790 MB, não
  108).
- **Filtrei mensagens pelo relógio do PC**, e o relógio do celular estava atrás.
  A captura descartava tudo o que chegava.

---

## O que não terminou

- **O planejador da IA leva 7–9 s por turno no celular** (`BuildObjectivePlan`:
  105 ms no PC). Em parte é swap. Medir com o Profiler num build dev com
  Autoconnect Profiler, se o lag voltar no Release.
- **~1 s ao selecionar quadrante na Campanha**: quadros de 250 ms sem GC e sem
  alocação. Suspeitos: o rebuild do panel_helper (TMP) e a câmera. Profiler.
- **O ramo "rally vazio" do APC** ainda espera perto da produção de propósito.
  Se o APC continuar parado na base, procurar
  `rally vazio sem leva nova; aguarda reforço`.
- **Avisos do Console:**
  - id duplicado `rotorAutonomy` no AutonomyDatabase;
  - câmera da Campanha sem collider de clamp;
  - `AI Shopping: DontDestroyOnLoad` num objeto que não é raiz.
- **LTO:** a causa não foi achada. Para voltar a usar, gerar com Debug Symbols
  Embedded.
- **Áudio sem licença:** as músicas são remixes no Suno de trilhas de terceiros, e
  os efeitos vieram de um jogo de Game Boy. Tudo precisa ser trocado antes da
  Steam (na memória).
- **As ferramentas de captura** (`captura_console.mjs`, `mem_sampler.sh`) ficaram
  no scratchpad da sessão. Se forem virar rotina, vão para `tools/`.
- **Hot seat humano × humano** segue fora do MVP; os tutoriais, fora do menu.
