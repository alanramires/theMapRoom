# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-08-24, **depois** da tag `v8.4.1`.
Leia isto primeiro.

---

## Estado

`v8.4.1` tagueada e publicada. Relatório:
[`relatorio_v8.4.1.md`](relatorio_v8.4.1.md).

```text
v8.3.0   o primeiro quadrante pintou      361 tiles, 2 ms, cena vazia
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
v8.4.1   a peça tem lado                  orientação, rotas partidas, identidade
```

**Duas frentes correm em paralelo.** Esta (o recorte do quadrante) e a da cena
de seleção de campanha, briefada em
[`Planos/briefing_cena_campanha.md`](Planos/briefing_cena_campanha.md). Elas se
encontraram, e o encontro é o que mais muda a retomada — ver "O 0a chegou".

### A descoberta que organiza o resto

> **Copiar uma peça sem o lado dela não é copiar.**

`Tilemap.SetTile` leva o tile e **descarta rotação, espelho e cor** — a Unity
guarda isso em arrays paralelos. O quebra-mar é um sprite só, girado de 60 em 60
graus para acompanhar a costa:

```text
quebraMar     39 células   →  10 matrizes distintas
terreno     1800 células   →   1 (identidade)
```

Por isso o recorte de terreno funcionava havia dias: terreno é o caso em que o
defeito é **invisível**. Hexágono de planície fica igual girado.

### O 0a chegou — e trouxe uma colisão

A frente paralela publicou `MatchController.OnMatchConcluded(vencedor,
derrotado, motivo, turno)` de dentro do funil real. **Verificado:**
`ImportVictoryState` não passa por lá, então carregar save concluído não
registra outra vitória. O bloqueio 0a está **fechado**.

Mas o `CampaignProgressStore` que veio junto diverge do plano em três pontos —
decisões, não consertos. Está detalhado no relatório e repetido em "Onde eu
parei".

---

## Vocabulário

```text
MUNDO       uma cena de autoria + UM asset. O globo inteiro, desenhado de uma vez
 └─ BLOCO       Europeu · América do Norte · Rússia    ← o jogador escolhe
     └─ CAMPANHA    Europa · África
         └─ QUADRANTE   Inglaterra · Congo...          ← aqui se luta, e é o que é assado
```

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável onde se joga |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção |

Um quadrante **contém** setores.

**ids são técnicos** (`feijao-torto`), nomes são livres (`Feijão Torto`). O id é o
que o save grava e o que o endereço casa; a bancada avisa quando ele tem acento ou
espaço.

---

## O que existe

```text
Assets/Scripts/Campanha/    MundoData · BlocoData · CampanhaData · QuadranteData
                            INoDoMapa · QuadranteController
                            ConstrucaoAssada · CamadaAssada · RotaAssada
                            CampaignProgressStore        ← da frente paralela
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Scenes/Autoria/      Fixture (13 construções, 3 rotas) · Mundo
Assets/Scenes/              Campanha (com seleção) · Batalha (vazia)
```

O fixture, agora com quatro quadrantes de tamanhos diferentes:

```text
bloco A · Auridia
 └─ campanha A_IA · "A invasão a Auridia"
     ├─ A_IA_Q1  Feijão Torto     (-18,10)  16×17   272 tiles · 13 construções
     ├─ A_IA_Q2  Terra Firme      (-18,-9)  21×20   420 tiles
     ├─ A_IA_Q3  Peixe Pequeno     (-3,10)  35×17   595 tiles
     └─ A_IA_Q4  Tubarão Branco     (2,-9)  30×20   600 tiles
```

**Terreno, construções, camadas decorativas e rotas entram. Unidades não.**

A "Euro Road" cruza os **quatro** quadrantes e foi cortada na borda de cada um —
sete trechos assados. É o recorte de rota exercitado em dado real.

---

## Onde eu parei

### ⚠️ Duas coisas que precisam de um clique na Unity

**1. Reassar os quatro quadrantes.** O asset está com `transformacoes: 0` — o
bake do quebra-mar rodou **antes** do conserto de orientação. Até reassar, o
enfeite continua torto. O status da bancada vai passar a dizer
`quebraMar 39 (9 giro(s))`; se disser `0 giro(s)`, o bake pegou a camada errada.

**2. Selar os nós.** A seção Mundo vai mostrar `Selar 6 nó(s)`. O `idSerial`
ainda não existe no asset.

⚠️ **Nada da `v8.4.1` foi compilado.** O código foi escrito contra as APIs lidas,
sem passar pelo compilador. Confira o Console antes de concluir qualquer coisa.

### A colisão do progresso — decisão, não conserto

`CampaignProgressStore` (frente paralela) guarda:

```csharp
public string quadranteId;   // endereça pelo texto EDITÁVEL
public int ownerTeamId;
public int lastTurn;         // sobrescrito sempre, e mora junto do dono
```

| # | divergência do plano | consequência |
|---|---|---|
| 1 | endereça pelo `id` editável | é o que o `idSerial` desta versão existe para evitar. Renomear órfã o progresso em silêncio. Agora é barato de consertar |
| 2 | `lastTurn` é *último*, não *melhor*, e mora junto do dono | o plano separa `dono` (estado, muda sempre) de `marca` (registro, nunca piora). Colapsados, perder o quadrante apaga que você o tomou em 11 turnos |
| 3 | `schemaVersion` | inofensivo, mas nada foi distribuído — migração não compra nada hoje |

O **2** primeiro: é o único que exige decisão de design.

### Depois

```text
1. bake de unidades iniciais (mesmo molde do bakedConstrucoes)
2. destravadoPor passa a guardar idSerial em vez de texto
3. sceneLoaded nos 4 managers  (o 0b, único bloqueio que sobrou)
4. avaliação de destrave — hoje só existem os campos
```

### Dívidas com gatilho conhecido

- ~~**`guid` estável**~~ ✅ virou `idSerial`, nos três níveis via `INoDoMapa`.
  **Falta selar**, e falta o `destravadoPor` passar a usá-lo.
- **`BoardReady` tem um leitor** (`RefreshAllOccupancyVisuals`); os demais
  consumidores ainda não consultam.
- **As cenas de execução seguem fora do Build Settings.**
- **Nada foi medido em escala.** A bancada e o pintor rodaram sobre 1800 células.
- **A lógica de rota partida não foi exercitada.** As três rotas caem inteiras em
  seus quadrantes. Só uma que saia e volte prova.
- **`.vscode/settings.json` voltou para `.slnx`**, desfazendo o `769a3dc`. O
  `.sln` é gerado pela Unity e não existe no disco. Deixado sujo de propósito.

---

## O bloqueio que sobrou

`0a` **fechado** pela frente paralela (`OnMatchConcluded`, verificado contra
`ImportVictoryState`). Resta um:

| # | frente | por que ainda importa |
|---|---|---|
| **0b** | `sceneLoaded` nos 4 managers | a campanha **vai** encadear cenas, e agora ela existe |

**Teste da 0b, hoje:** menu → mapa A → turno 5 → menu → mapa B. No turno 1 do B,
o plano tem de nascer **vazio**.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 terreno, construções, enfeite e rotas entram;
                                     faltam unidades e o progresso endereçado por serial
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **`SetTile` tomado como cópia** | leva o tile e **descarta rotação, espelho e cor** — a Unity guarda em arrays paralelos. Só aparece em arte que tem lado; terreno é o caso em que o defeito é invisível |
| **`SetTransformMatrix` ignorado calado** | um tile com `LockTransform` faz o tilemap descartar a matriz sem avisar. Destravar (`SetTileFlags(None)`) antes de orientar |
| **recortar uma sequência como se fosse conjunto** | rota é ordenada, e os consumidores leem PARES. Tirar uma célula do meio COLA as vizinhas numa aresta que ninguém desenhou — e se elas forem adjacentes de verdade, vira atalho com bônus, sem erro |
| **`IsRouteValid` é tudo-ou-nada** | uma célula inválida descarta o desenho da rota INTEIRA. Por isso o recorte quebra em buraco, não só na borda |
| **`Build` fora do Play** | tudo que ele escreve é serializado. Numa cena compartilhada por todos os quadrantes, salvar grava o layout de um dentro dos outros |
| **camada decorativa é tilemap IRMÃO** | `ClearAllTiles` no tabuleiro não encosta nela. Enfeite órfão sobrevive a uma limpeza inteira |
| **contador de id recalculado do máximo** | certo no `UnitSpawner` (ids morrem com a partida), **errado** no mundo (o registro sobrevive ao nó). Serial queimado fica queimado |
| **hexágono tratado como grade quadrada** | é odd-r: linha ímpar desloca meia célula. Verificado contra o traçado da rodovia — 33/33 pares contra 24/33 |
| **escape `
` em geração de código por script** | a ferramenta processa a barra antes do Python, e o literal C# quebra em duas linhas. Usar `chr(92)` |
| **generalizar do que se encontra sem checar a razão** | quatro vezes em dois dias: padrão `Database→Data`, nível de bloco ausente, "um mundo por cena", catálogo por mapa. **Perguntar por que aquilo existe antes de construir em cima** |
| **catálogo por mapa tomado como padrão** | existia só porque o catálogo carregava layout. O `TerrainDatabase` — 1 asset — provava o contrário |
| **layout no TIPO compartilhado** | "Rodovias" carregava 11 traçados. Toda cena que usasse o tipo herdava |
| **campo sem leitor tomado como inofensivo** | `fieldEntries` tinha zero leitores **e** obrigava sete catálogos a existir |
| **`ConstructionSector` default** | é `Alpha = 0`, não `None = -1`. Esquecer o setor não dá erro: dá plano degenerado |
| **build não idempotente** | limpar tiles sem limpar construções faz o segundo build virar fila de avisos |
| **id com acento ou espaço** | o YAML escapa (`"Feij\xE3o Torto"`) e o texto é digitado em dois lugares que precisam bater |
| **parse frágil de `.asset` virando afirmação** | errei três vezes. Contagem por faixa de linha é confiável; `$3` de `awk` não |
| **`.asset` em disco tomado como estado atual** | Inspector marca `dirty` e **não grava** |
| **`sed` em C# sem conferir chaves** | comeu um `}` de fechamento. Contar profundidade antes de seguir |
| **apagar dado do autor sem perguntar** | 1058 entradas e 3 rotas de tutorial. Perguntar, sempre |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí o `-9000` e o portão |
| **retângulo de célula desenhado a olho** | grade hexagonal tem borda serrilhada |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem toda tradução de coordenada |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` são da cena corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação ou caches |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`relatorio_v8.4.1.md`](relatorio_v8.4.1.md) | orientação, rotas partidas e identidade estável |
| [`relatorio_v8.4.0.md`](relatorio_v8.4.0.md) | o dia em que o catálogo parou de dizer onde |
| [`relatorio_v8.3.0.md`](relatorio_v8.3.0.md) | o dado achou a forma na terceira tentativa |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.**
- **Plano pedido não autoriza implementação.**
- **Verificar antes de documentar.** Busca vazia não prova ausência, e `.asset`
  em disco não prova estado.
- **Perguntar por que uma coisa existe antes de construir em cima dela.**
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.
