# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-08-14, **depois** da tag `v8.2.2`.
Leia isto primeiro.

---

## Estado

`v8.2.2` tagueada e publicada. Relatório:
[`relatorio_v8.2.2.md`](relatorio_v8.2.2.md).

### O que mudou de rumo

O projeto ganhou um **tronco mestre**:
[`Planos/plano_campanha.md`](Planos/plano_campanha.md), 1120 linhas de desenho
com **zero linhas de código**.

Até aqui todos os planos eram internos à IA — iniciativa, transportador,
capturador. Este é o primeiro que descreve **o que o jogador faz com o jogo**, e
por isso é o primeiro que *puxa* arquitetura em vez de empurrar.

Isso é o que ele muda na prática: várias pendências estavam paradas **por serem
higiene sem consumidor**. A campanha não descobriu nenhuma delas — ela deu
**motivo** a todas.

```text
fim de partida sem evento          conhecido, parado
4 managers DontDestroyOnLoad       CLAUDE.md já suspeitava, parado
10 catálogos de estrutura por mapa doc já chamava de violação, parado
routesMigratedToScene sem prazo    a flag existe PORQUE não havia prazo
zero persistência de preferência   fullscreen duplicado em 2 lugares
```

### As duas frases que organizam a campanha

> **O mapa grande governa os pequenos. Não é zoom, é recorte** — o hexágono tem o
> mesmo tamanho nos dois lugares; o que muda é a janela.

> **O recorte é imutável.** A única coisa que muda entre jogadas é *de quem é o
> quadrante*, e isso mora no arquivo de progresso, nunca no mapa.

Disso sai de graça a feature que justifica tudo: a estrada que cruza a fronteira
**não precisa ser sincronizada**, porque são os mesmos tiles no mesmo sistema de
coordenadas.

### O achado que veio do autor

> **Com uma cena de batalha única, o save deixa de ser conferido e passa a
> mandar.**

A primeira reação foi trocar a chave da guarda (`sceneName` → `(campanha,
quadrante)`). Continua sendo **validação**: alguém compara duas coisas que
poderiam divergir.

O melhor é outro: a `Batalha` nasce vazia, o save **diz** que quadrante pintar,
pinta, e só então restaura as peças. Save no mapa errado deixa de ser detectável
porque deixa de ser **representável**. Mata também o `PendingMainMenuLoadRequest`.

---

## Vocabulário — fixe isto antes de ler o plano

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável do mapa de campanha, onde se luta |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção (Alpha… + Base0..4) |

Um quadrante **contém** setores. O enum já usava a palavra "campanha" no próprio
comentário, e carrega `ARMADILHA PERMANENTE` na documentação — não é vizinho com
quem valha confundir nome.

---

## Onde eu parei

### Existe e está commitado

```text
docs/Planos/plano_campanha.md          o tronco — 1120 linhas, zero código
Assets/Editor/MapHelperWindow.cs       Tools > Utils > Map Helper
Assets/Editor/SceneSanitizerWindow.cs  Tools > Utils > Faxina de Cena
Assets/Scenes/Autoria/Fixture.unity    37×19, sólido, serra + passo + 1 rota
Assets/Scenes/Autoria/Mundo.unity      vazio
Assets/Scenes/Campanha.unity           vazio, execução
Assets/Scenes/Batalha.unity            vazio, execução
```

### Não existe

- **Nenhuma linha de código de campanha.** Nem bake, nem pintor, nem
  `CampaignData`, nem arquivo de progresso.
- **As duas cenas de execução não estão no Build Settings.** Hoje é proteção;
  vira pendência quando alguém tentar carregá-las.

### Não foi validado

- A frente de **sondagem curta na iniciativa** (`085a74b`) teve o diff lido por
  inteiro e **nenhuma corrida em partida**. É mudança na fila, e defers ali já
  geraram cessão mútua antes.
- **Map Helper e Faxina de Cena só rodaram em 703 células.** Os três gates de
  performance do rótulo por hexágono foram desenhados para dezenas de milhares.
- Os **~92.000 tiles** da cena de Mundo são estimativa. *Medir, não assumir.*
- **19×19 = 361 células por quadrante** está acima das ~196 já provadas.

---

## Próximo passo — e ele não depende de nenhuma decisão

O plano define oito fases. As duas primeiras **valem por si mesmas** e não
dependem de nada em aberto:

| # | frente | por que agora |
|---|---|---|
| **0a** | evento no funil de vitória | conserta um beco sem saída que **já existe hoje** |
| **0b** | `sceneLoaded` nos 4 managers | testável agora, sem campanha nenhuma |

**A 0a é menor do que parece.** O funil já existe:

```csharp
// MatchController.cs:3025 — a taxonomia está pronta
enum VictoryReason { HeadQuarterCaptured, ArmyEliminated, Surrender, VictoryStars }

// MatchController.cs:3033 — a assinatura já é a que a campanha precisa
HandleVictoryAestheticPresentation(TeamId winner, TeamId defeated, VictoryReason reason)
```

Os dois motivos do MVP já passam por lá. Falta: publicar o evento, rotear os
**três caminhos que o contornam** (`DeclareTutorialVictory`,
`DeclareTutorialDefeat`, `DeclareDefeat`), e ⚠️ **blindar `ImportVictoryState`
(`:1250`)** — restauração não é conclusão, e um evento ingênuo dispara no *load*.

E `freezeTurnAdvanceAfterVictory` (`:431`) não é obra inacabada: é um campo
deliberado, ligado por padrão, esperando a camada que agora existe no papel.

**Teste da 0b, hoje, sem campanha:** menu → mapa A → jogue até o turno 5 → menu →
mapa B. No turno 1 do mapa B, o plano tem que nascer **vazio**.

### Depois, a máquina de recorte

Ordem refinada em relação à tabela do plano — **a leitura vem antes da pintura**,
porque não se pinta o que ainda não se sabe ler:

```text
C1  seleção que só LÊ (já meio-feita no Map Helper)
C2  o bake: retângulo → terreno + rotas filtradas e transladadas
C3  botão "pintar agora" no Editor  +  o portão de "pintura terminou"
►   TESTE DE ACEITAÇÃO — sem Play
C4  pintor em runtime na Batalha
C5  save carrega (campanha, quadrante) e DIRIGE a pintura
```

⚠️ **O C2 tem de ler as rotas pelo caminho filtrado (`GetRoadRoutes`), nunca pela
lista crua (`RoadRoutesByStructure`)** — senão pega rota estrangeira que o runtime
já ignora.

### Teste de aceitação

> Pinte a estrada cruzando o passo no mapa de autoria. Regere os dois quadrantes.
> Sem tocar em nenhuma cena de batalha: a estrada entra no **Q1 pela borda leste,
> na linha R**, e no **Q2 pela borda oeste, na MESMA linha R**.

Confere-se em cinco segundos. Se a linha não bater, o erro é de **translação de
origem** — não precisa investigar mais nada.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
                                  ✅ MelhorEmbarque ganhou sondagem curta
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      📄 planejada, zero código
```

O degrau 5 é novo. Ele não é continuação dos outros — é o primeiro que tem um
jogador dentro.

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **`git status` limpo confundido com cena limpa** | rodei o teste "nasceu vazia?" olhando tiles, construções e unidades — e **não olhei rotas**. Havia 46 estrangeiras. O teste do `CLAUDE.md` inclui estradas |
| **contaminação inerte descrita como ativa** | `IsRouteAllowedForCurrentDatabase` (`:361`) barra rota de outro catálogo. Conferir o filtro **antes** de descrever o efeito |
| **rota com `ownerDatabase` nulo** | tratada como legado/global, **passa** no filtro. Única classe estrangeira que age |
| **retângulo de célula desenhado a olho** | grade hexagonal tem borda serrilhada; pintar pelo que "parece reto" fura linha sim, linha não. Aconteceu **duas vezes no mesmo dia** |
| **script de Editor culpado por build lento** | `Assets/Editor/` não entra em player build. Quem mexe no build é runtime |
| **`Assets/Resources/` como pasta neutra** | embarca **inteira** em todo build; um build cancelado deixou dois JSON ali |
| **duplicar cena esperando cópia limpa** | duplicação copia o layout que a cena legitimamente possui. Faltava a operação de esvaziar, não mais desacoplamento |
| **derivado não invalidado** | remover rota sem `InvalidateRoutesLookup` + `RebuildRoadVisuals` deixa a cena desenhando estrada que já não existe |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois: QG duplicado quebra a contagem por slot |
| **pintor escrevendo na cena de autoria** | origem e destino nunca são o mesmo arquivo — corrompe a fonte, e o resultado *parece* válido |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem **toda** tradução de coordenada, parecendo bug de recorte |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem outro candidato de ajudar |
| destino confundido com âncora imediata | o prédio permanece a missão; a LZ só substitui o próximo passo quando não há rota própria |
| matcher global sem histerese | custo de troca nasce junto com o eixo N; hoje vale `15` |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` pertencem à cena/tilemap corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação, recursos ou caches confirmados |
| compilar não prova que o arquivo mudou | conferir o diff e o arquivo-alvo antes do commit |
| busca vazia tomada como prova de ausência | para ausência, a pergunta é `git ls-files`, não `git status` |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`relatorio_v8.2.2.md`](relatorio_v8.2.2.md) | o dia de hoje, e duas correções de afirmação minha |
| [`relatorio_v8.2.1.md`](relatorio_v8.2.1.md) | a travessia naval e o táxi que bloqueava a própria entrega |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega — §10 tem a sondagem curta |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | doutrina e voz da família do capturador |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.** Toda ação começa e
  termina em `CursorState.Neutral`; o meio é cancelável.
- **Plano pedido não autoriza implementação.** Avaliar e executar são trabalhos
  diferentes.
- **Verificar antes de documentar.** Busca vazia não prova ausência.
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.
