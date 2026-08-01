# Refactor: unidade sem plano não é uma IA paralela

## O modelo (definido pelo autor)

Existem **duas** coisas no jogo:

| | |
|---|---|
| **a)** | unidades **com** plano |
| **b)** | unidades **sem** plano |

E duas formas de IA usá-las:

- **IA com QG** usa as duas. Tem setores, faz planos, tem eixos.
- **IA sem QG (rebelde)** só tem unidades **sem plano**. Não usa setores, não
  faz planos, escolhe alvo por proximidade, e as coisas flutuam ao redor dela.

A consequência arquitetural é uma frase só:

> A estrutura de IA que existe **recebe parâmetros de entrada** dos rebeldes,
> que sobrescrevem **algumas** decisões — como marchar para algum lugar ou
> embarcar — e **não todas**.

Rebelde é um **conjunto de parâmetros** sobre o controlador que já existe. Não é
um controlador.

E "sem plano" **não é sinônimo de rebelde**: a IA com QG também produz unidades
sem plano (rogue). O que a facção rebelde tem de especial é que *todas* as suas
unidades são desse tipo, e que a âncora do mapa não pode ser o próprio QG,
porque ela não tem um.

## O que foi feito de errado

`AIController.Rebel.cs` (criado em 2026-07-24, commit "Ajustes gerais de
estrutura e AI Rebelde") virou um **espelho do capturador**, não um roteador.
454 linhas, seis funções próprias.

A justificativa está escrita no próprio arquivo:

> *"o plano normal assume um eixo a partir do próprio QG — a rebelde não tem, e
> sem este curto-circuito todo capturador dela vira rogue e marcha para o QG
> inimigo"*

O problema real era **um**: a âncora do capturador rogue é fixa no QG inimigo, e
facção sem QG não pode usar essa âncora. Em vez de parametrizar a âncora,
escreveu-se um caminho paralelo — e com ele vieram busca de alvo própria,
aproximação própria e portão de deslocamento próprio.

### O custo já medido

Cada regra nova precisa ser escrita duas vezes, e a segunda sempre atrasa:

| regra | no fluxo normal | no espelho rebelde |
|---|---|---|
| `prioritizeDpqAtBattle` (flag da ficha) | honrada em Assault, Capturer, Defender, Explorer, Pursuer, HQBreaker | **ignorada** até v6.1.2 |
| alcance a pé | envelope, turnos encadeados | **`MP × 2` pooled**, `Rebel.cs:96-104` |
| decisão de carona | `QueroCaronaService` | **decidida antes de consultar o serviço** |
| alocação 1:1 de capturável | `CaptureOpportunityClaimService` | busca própria por proximidade |

### E o espelho vazou

As funções "de rebelde" já são chamadas por código genérico:

| função | chamadores fora do `Rebel.cs` |
|---|---|
| `IsRebelCapturable` | 6 — `MelhorDesembarque` (4), `Courier.Disembark` (2) |
| `FindNearestRebelCaptureTarget` | 3 — `HQBreaker`, `Courier.Passengers`, `Naval` |
| `TryResolveUnitDesignatedCaptureTarget` | 1 — `MelhorDesembarque` |
| `CommitPendingRebelCaptureTarget` | 1 — `Phase2` |

Ou seja: o mirror deixou de ser opcional. Código de transporte e desembarque
depende dele para **qualquer** unidade.

## O plano

### 1. Separar as três categorias

**Geral com nome errado** — renomear e mover para onde já pertence. Nenhuma
delas tem lógica de rebelde dentro:

| hoje | é, na verdade | destino |
|---|---|---|
| `IsRebelCapturable` | "esta unidade pode capturar esta construção" | predicado de captura, junto do sensor |
| `TryResolveUnitDesignatedCaptureTarget` | leitura do alvo designado | `AIController.Capturer.*` |
| `CommitPendingRebelCaptureTarget` | confirmação do alvo designado | `AIController.Capturer.*` |

**Duplicata do que já existe** — apagar, e usar o que está lá:

| hoje | já existe em |
|---|---|
| busca de alvo por proximidade | `CaptureOpportunityClaimService` (matching 1:1) + proximidade |
| `FindRebelApproachCell` | aproximação do capturador |
| portão "vai a pé" com `MP × 2` | `QueroCaronaService` (envelope, bandas por turno) |

**Parâmetro genuíno da facção sem QG** — vira entrada do fluxo normal, não
código próprio:

- a âncora do funil **não** é o QG inimigo;
- não há setor, não há eixo, não há objetivo formal;
- alvo é o capturável livre mais próximo.

### 2. `FindNearestRebelCaptureTarget` vira "objetivo de unidade sem plano"

A pergunta que ela responde — *"qual capturável livre mais próximo ainda não
está sendo tratado?"* — vale para **toda** unidade sem plano, rebelde ou rogue
da IA com QG. Renomear e generalizar; hoje ela já é chamada por três sítios que
não são rebeldes.

### 3. A âncora do rogue deixa de ser o QG — sem ramo

Primeira versão deste plano tratava a âncora como uma escolha (*tem QG? funila
para o QG; não tem? capturável mais próximo*). **Decisão do autor: não há ramo.**

> *"é pra rogue se comportar como rebel agora, inclusive rebel vai deixar de
> existir no nosso refactor, vai virar tudo rogue."*

Ou seja: o funil para o QG inimigo **acaba**, para rogue de qualquer facção. A
âncora do rogue é sempre o capturável livre mais próximo. "Rebelde" deixa de ser
uma categoria de comportamento e sobra só como propriedade da facção (não tem
QG, não produz, imune à derrota por zero unidades).

Isso simplifica o refactor em vez de complicá-lo: some o parâmetro, some o
ramo, e some a necessidade de testar as duas metades. Um comportamento só.

Consequência de vocabulário: onde o código hoje diz *rebelde* querendo dizer
*sem plano*, passa a dizer **rogue**. Onde diz *rogue* querendo dizer *funila
para o QG*, é código a apagar.

### 4. `Rebel.cs` vira o que deveria ter sido

Reconhece o slot rebelde, marca o contexto "sem plano" e chama
`TryDecideCapturerAction`. Nada mais. As seis funções próprias somem ou mudam de
casa.

O gate `plan != null` do roteador não é bloqueio real: o caminho rogue do
capturador já roda com objetivo nulo por dentro (`AIController.Capturer.Rogue.cs`
existe exatamente para isso). O que falta é aceitar `plan == null` na entrada e
não assumir QG na âncora.

### 5. O desembarque também funila para o QG — e é o mesmo bug

Levantado em 2026-08-01, ao conferir a doutrina de desembarque contra o código.
O transporte tem a **sua própria cópia** do funil, e ela não foi tocada pela
v6.1.2/v6.1.3: o caminhão ainda entrega o rogue pela regra velha.

| # | achado | onde |
|---|---|---|
| T-A | rogue de IA **com** QG resolve alvo por `TryResolveRogueCorridorCaptureTarget` — "corredor rumo ao QG". É a doutrina derrubada, viva no transporte | `AIController.Transportador.Courier.Passengers.cs:236-252` e `AIController.MelhorDesembarque.cs:1156-1191` |
| T-B | o comentário de `MelhorDesembarque.cs:1151-1155` **já afirma** que o rogue "é irmão da IA rebelde: escolhe o capturável livre mais próximo", mas o código chama o corredor primeiro. Comentário atualizado, código não | idem |
| T-C | a atribuição (`AIDesignatedMission*`) só é lida no ramo rebelde — **um único** call site, dentro de `if (IsRuntimeRebelSnapshot)`. Numa IA com QG a Designated Mission é ignorada no desembarque | `AIController.MelhorDesembarque.cs:1242` |
| T-D | `TryResolveCourierPassengerTarget` cai no `RepresentativeCell` do setor (linha 228) contra o comentário logo acima (linha 211-213), que manda **não** cair — em setor já capturado o RepCell é a própria célula do caminhão, e sai um desembarque de distância zero | `AIController.Transportador.Courier.Passengers.cs:211-229` |

T-A e T-B morrem junto com o funil do item 3 — é o mesmo código escrito duas
vezes, exatamente o custo que este documento descreve. **T-C some sozinho**
quando `IsRuntimeRebelSnapshot` deixar de ser um ramo: com um comportamento só,
a atribuição passa a ser lida para todo mundo, que é o que o autor já assume.

T-D é independente e sobrevive ao refactor. Não é dívida de rebelde: é um
fallback que contradiz o próprio comentário. Tratar à parte.

## O que NÃO muda

Regras da facção sem QG que continuam valendo, e não são deste refactor:

- derivada de "não possui `isPlayerHeadQuarter`";
- nunca produz unidades;
- imune à derrota por zero unidades;
- captura livre, sem restrição de setor.

## Verificação

O teste que separa "funcionou" de "quebrou" é o mesmo dos dois lados:

> **IA com QG e planos × IA rebelde sem QG e sem planos**, no mesmo mapa.

Rodar **antes** do refactor para ter linha de base do rebelde, e depois. O
rebelde tem que se comportar igual ou melhor, com metade do código — e as
regras que hoje ele ignora (DPQ, envelope, fila da carona) passam a aparecer no
log dele sem ninguém ter escrito nada específico.

Sinais de sucesso no log do rebelde, depois do refactor:

```text
[Capturador] <id> ... dpq=... preferDpq=True        ← ficha honrada
[Capturador] <id> QueroCarona=... envelope=...      ← alcance pelo envelope
[FilaCarona] <id> entra na fila ...                 ← mesma fila de todos
```

## Ordem sugerida

Este refactor muda comportamento de captura da facção rebelde. Fazê-lo no meio
da migração do envelope ou da esteira de transporte tira a base de comparação —
não daria para saber se uma diferença veio da fila da carona ou da âncora nova.

1. fechar a esteira de transporte (itens B e pressão de compra);
2. rodar o teste com o rebelde **como está** — linha de base;
3. desmontar o espelho;
4. rodar o mesmo teste — a verificação.
