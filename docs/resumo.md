# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-08-14, **depois** da tag `v8.4.0`.
Leia isto primeiro.

---

## Estado

`v8.4.0` tagueada e publicada. Relatório:
[`relatorio_v8.4.0.md`](relatorio_v8.4.0.md).

**Duas coisas aconteceram hoje**, e a segunda é a que muda o projeto inteiro:

```text
v8.3.0   o primeiro quadrante pintou      361 tiles, 2 ms, cena vazia
v8.4.0   o catálogo parou de dizer ONDE   três camadas de layout removidas
```

### A frase virou estado

> **O catálogo diz o que uma coisa É. A cena diz onde ela ESTÁ.**

Era doutrina do `CLAUDE.md` desde sempre. Hoje é verdade no código:

```text
ConstructionDatabase.fieldEntries          1058 entradas · 7 catálogos · ZERO leitores
StructureDatabase.roadRoutesByStructure      93 rotas · 16 catálogos
StructureData.roadRoutes                     16 rotas em 4 TIPOS        ← a pior
```

A terceira era a mais grave: o asset **"Rodovias"** — que diz o que uma rodovia
*é* — carregava **onze traçados concretos**. Toda cena que usasse o tipo herdava
estrada de outro mapa, sem erro.

**O teste de aceitação do `CLAUDE.md` passa agora:** duplicar cena não traz mais
layout de ninguém, e passa *por construção*, não por disciplina.

Os catálogos viraram agrupamentos por conteúdo, como o `TerrainDatabase` sempre
foi: `All Buildings` (15), `All Units` (35), `All Structures` (4).

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
                            ConstrucaoAssada · INoDoMapa · QuadranteController
Assets/Editor/              MapHelperWindow (o editor de campanha) · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Scenes/Autoria/      Fixture (1800 tiles, 13 construções, 2 rotas) · Mundo
Assets/Scenes/              Campanha · Batalha  (vazias, fora do Build Settings)
```

O fixture:

```text
bloco A
 └─ campanha A_IA
     ├─ A_IA_Q1   (-18,10) 16×17   272 tiles · 13 construções assadas
     └─ A_IA_Q2                    SEM BAKE
```

**Terreno pinta. Construções plantam em parte. Estruturas não entram.**

---

## Onde eu parei

### ⚠️ Primeira coisa, e é um minuto

**A `Batalha` está com o `ConstructionSpawner` sem catálogo**
(`constructionDatabase: {fileID: 0}`). O `Fixture` aponta pro `All Buildings`; a
`Batalha` não. Enquanto não ligar, nenhum id resolve e o quadrante nasce sem QG.

O log diz isso explicitamente — se ele não aparecer, confira erro de compilação
antes.

### Depois

```text
2. investigar por que as construções plantam só em PARTE
3. assar o A_IA_Q2
4. recorte de ESTRUTURAS   → e aí o teste de aceitação fecha
5. bake de unidades iniciais (mesmo molde do bakedConstrucoes)
```

⚠️ **O recorte de rotas lê o `RoadNetworkManager` da cena.** Não existe mais
catálogo de onde ler — o hábito antigo de olhar o catálogo agora acha vazio em vez
de errado.

### Herança de YAML morto

`All Buildings` (126 `fieldEntries`) e `All Structures` (23 rotas) foram
duplicados dos per-mapa e carregam campos que **não existem mais nas classes**. A
Unity descarta na próxima reserialização; até lá, é dado que ninguém lê.

### Dívidas com gatilho conhecido

- **`guid` estável** separado do nome editável. Hoje o **id** é o que o save
  grava, então renomear o id quebra. **Tem de entrar antes do primeiro arquivo de
  progresso.**
- **`BoardReady` não tem leitor.** O portão existe e ninguém consulta.
- **Avaliação de destrave não existe.** Só os campos (`destravadoPor`,
  `exigeIrmaos`).
- **As cenas de execução seguem fora do Build Settings.**
- **`História 3 - Resgate Off Road` perdeu suas 3 rotas**, por decisão do autor.
- **Nada foi medido em escala.** Bancada e pintor rodaram sobre 1800 células.

---

## Os dois bloqueios continuam de pé

Herdados da `v8.2.2`, **intocados**, e nenhum depende de decisão em aberto:

| # | frente | por que ainda importa |
|---|---|---|
| **0a** | evento no funil de vitória | conserta um beco sem saída que **já existe hoje** |
| **0b** | `sceneLoaded` nos 4 managers | a campanha **vai** encadear cenas |

**A 0a é menor do que parece** — o funil já existe:

```csharp
// MatchController.cs:3025
enum VictoryReason { HeadQuarterCaptured, ArmyEliminated, Surrender, VictoryStars }
// MatchController.cs:3033 — a assinatura ja e a que a campanha precisa
HandleVictoryAestheticPresentation(TeamId winner, TeamId defeated, VictoryReason reason)
```

Falta: publicar o evento, rotear os três caminhos que o contornam
(`DeclareTutorialVictory`, `DeclareTutorialDefeat`, `DeclareDefeat`), e ⚠️
**blindar `ImportVictoryState` (`:1250`)** — restauração não é conclusão.

**Teste da 0b, hoje:** menu → mapa A → turno 5 → menu → mapa B. No turno 1 do B, o
plano tem de nascer **vazio**.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 terreno + construções parciais; estruturas faltam
```

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
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
