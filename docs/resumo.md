# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-08-14, **depois** da tag `v8.3.0`.
Leia isto primeiro.

---

## Estado

`v8.3.0` tagueada e publicada. Relatório:
[`relatorio_v8.3.0.md`](relatorio_v8.3.0.md).

**A campanha saiu do papel.** Uma cena de batalha vazia recebe um endereço, lê o
que está assado e desenha o tabuleiro:

```text
[Quadrante] 'campanha A1/QA1' construido: 361 tiles, 0 buraco(s), 19x19
            origem local 0,0 · origem de autoria -18,-9 · em 2 ms
```

O recorte está provado — **e a prova não precisou de partida, QG nem unidade.**

---

## Vocabulário — fixe isto antes de qualquer coisa

```text
MUNDO       uma cena de autoria + UM asset. O globo inteiro, desenhado de uma vez
 └─ BLOCO       Europeu · América do Norte · Rússia    ← o jogador escolhe
     └─ CAMPANHA    Europa · África
         └─ QUADRANTE   Inglaterra · Congo...          ← aqui se luta
```

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável onde se joga |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção (Alpha… Base0..4) |

Um quadrante **contém** setores. Não confundir.

**Uma cena só para o mundo inteiro**, porque tudo encosta em tudo: Europa faz
fronteira com África *e* com a Rússia. Não se desenha fronteira contínua entre
dois arquivos, e é a continuidade que faz o mapa acender por região.

---

## O que existe e funciona

```text
Assets/Scripts/Campanha/       MundoData · BlocoData · CampanhaData
                               QuadranteData · INoDoMapa · QuadranteController
Assets/Editor/MapHelperWindow.cs        o editor de campanha (1562 linhas)
Assets/Editor/SceneSanitizerWindow.cs   Faxina de Cena
Assets/DB/Campanha/Mundo Fixture.asset  2 blocos · 2 campanhas · 3 quadrantes assados
Assets/Scenes/Autoria/Fixture.unity     950 tiles, 50 colunas, com ilha
Assets/Scenes/Batalha.unity             + Quadrante Controller
```

O fixture está assim, e as costuras caíram em cima de feições geográficas:

```text
bloco A  (-18,-9) 37×19   QA1 x -18..0    QA2 x 0..18     emenda em x=0  → SERRA
bloco B  ( 18,-9) 14×19   QB1 x 18..31                    emenda em x=18 → MAR
```

---

## Onde eu parei

### ⚠️ Primeira coisa a arrumar

**A `Batalha.unity` está commitada com um endereço que não resolve:**
`campanhaId: fixture`, `quadranteId: Q1`. O teste rodou com `campanha A1` /
`QA1` — foi mudado no Inspector e a cena não foi salva. Abrir, corrigir, salvar.

### Falta

- **Estruturas e construções.** O recorte só leva **terreno**. A estrada não é
  recortada nem pintada, e por isso **o teste de aceitação do plano não foi
  feito**.
- ⚠️ **Quando o recorte de rotas for escrito, ele tem de ler pelo caminho
  filtrado (`RoadNetworkManager.GetRoadRoutes`), nunca pela lista crua
  (`RoadRoutesByStructure`)** — senão pega rota estrangeira que o runtime já
  ignora.
- **`BoardReady` não tem leitor.** O portão existe e ninguém consulta.
- **A avaliação de destrave não existe.** Só os campos (`destravadoPor`,
  `exigeIrmaos`).
- **As duas cenas de execução seguem fora do Build Settings.**

### Dá para fechar metade do teste sem escrever nada

`QA1` já mostra a serra e o passo na **borda leste**, em local `x=18`, `y=9`.
Trocar o `quadranteId` para `QA2` e dar Play: a mesma serra e o mesmo passo têm
de aparecer na **borda oeste**, em local `x=0`, **na mesma linha 9**. Se bater, a
translação está provada nos dois sentidos.

### Dívida com gatilho conhecido

**Identidade por string vai quebrar save.** Renomear uma campanha invalida o
endereço. A saída é um `guid` estável separado do nome editável.

> **Tem de entrar antes do primeiro arquivo de progresso.** Depois disso custa
> migração; antes, custa dez linhas.

O projeto já aprendeu isso no `ConstructionSector` (*"os ints são contrato de
serialização; nunca reaproveitar um número já usado"*).

Menor: `mundoId` está como `mundo fixture`, **com espaço**.

### Não medido

- Bancada e pintor rodaram sobre **950 células**. Os três gates do rótulo por
  hexágono foram desenhados para dezenas de milhares.
- Os `FrameSpike` de 5 s e 14 s **são anteriores a este trabalho** — apareciam no
  Play da cena vazia. O pintor custou 2 ms.

---

## Os dois bloqueios continuam de pé

Herdados da `v8.2.2`, **intocados**, e nenhum depende de decisão em aberto:

| # | frente | por que ainda importa |
|---|---|---|
| **0a** | evento no funil de vitória | conserta um beco sem saída que **já existe hoje** |
| **0b** | `sceneLoaded` nos 4 managers | a campanha **vai** encadear cenas |

**A 0a é menor do que parece.** O funil já existe:

```csharp
// MatchController.cs:3025
enum VictoryReason { HeadQuarterCaptured, ArmyEliminated, Surrender, VictoryStars }
// MatchController.cs:3033 — a assinatura ja e a que a campanha precisa
HandleVictoryAestheticPresentation(TeamId winner, TeamId defeated, VictoryReason reason)
```

Os dois motivos do MVP já passam por lá. Falta: publicar o evento, rotear os três
caminhos que o contornam (`DeclareTutorialVictory`, `DeclareTutorialDefeat`,
`DeclareDefeat`), e ⚠️ **blindar `ImportVictoryState` (`:1250`)** — restauração
não é conclusão, e um evento ingênuo dispara no *load*.

**Teste da 0b, hoje:** menu → mapa A → jogue até o turno 5 → menu → mapa B. No
turno 1 do mapa B, o plano tem de nascer **vazio**.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 terreno pinta; estruturas e peças faltam
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **aplicar o padrão da casa sem checar a razão dele** | `Database → Data` como assets vale porque `UnitData` é **compartilhado**. Campanha pertence a um mundo só. Generalizar sem verificar a razão errou **três vezes** num dia |
| **parse frágil de `.asset` virando afirmação** | errei a leitura do arquivo **três vezes** (`awk` cortando em espaço; indentação a mais do nível novo). Contagem por faixa de linha é confiável; `$3` não é |
| **`.asset` em disco tomado como estado atual** | edição no Inspector marca `dirty` e **não grava**. Disco e tela divergem até alguém salvar |
| **id repetido** | `TryGet*` devolve o **primeiro**. O segundo existe no asset e é inalcançável, sem erro nenhum |
| **assar com a cena errada aberta** | grava tiles nulos, e o sintoma parece "bake não rodou" |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí o `-9000` e o portão |
| **listar irmãos na mão** | quebra em silêncio quando se acrescenta um irmão. `exigeIrmaos` é flag |
| **identidade de serialização igual ao nome editável** | renomear invalida save |
| **retângulo de célula desenhado a olho** | grade hexagonal tem borda serrilhada. Aconteceu **três vezes** no mesmo dia |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois |
| **pintor escrevendo na cena de autoria** | origem e destino nunca são o mesmo arquivo |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem **toda** tradução de coordenada |
| **`git status` limpo confundido com cena limpa** | o teste "nasceu vazia?" tem de olhar **rotas** também — 46 estrangeiras passaram por isso |
| **contaminação inerte descrita como ativa** | `IsRouteAllowedForCurrentDatabase` (`:361`) barra rota de outro catálogo. Conferir o filtro antes de descrever o efeito |
| **script de Editor culpado por build lento** | `Assets/Editor/` não entra em player build |
| **`Assets/Resources/` como pasta neutra** | embarca **inteira** em todo build |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem outro de ajudar |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` pertencem à cena corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação, recursos ou caches |
| busca vazia tomada como prova de ausência | para ausência, a pergunta é `git ls-files` |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`relatorio_v8.3.0.md`](relatorio_v8.3.0.md) | o dia em que o dado achou a forma na terceira tentativa |
| [`relatorio_v8.2.2.md`](relatorio_v8.2.2.md) | o plano nasce, e as duas bancadas |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | doutrina da família do capturador |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.**
- **Plano pedido não autoriza implementação.**
- **Verificar antes de documentar.** Busca vazia não prova ausência, e `.asset`
  em disco não prova estado.
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.
