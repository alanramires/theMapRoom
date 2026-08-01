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

### 3. A âncora do rogue passa a ser parâmetro

O capturador rogue hoje funila para o QG inimigo. Isso vira uma escolha:

```text
tem QG?  → âncora = eixo/QG inimigo (comportamento atual)
não tem? → âncora = capturável livre mais próximo
```

Com isso o curto-circuito perde a razão de existir.

### 4. `Rebel.cs` vira o que deveria ter sido

Reconhece o slot rebelde, marca o contexto "sem plano" e chama
`TryDecideCapturerAction`. Nada mais. As seis funções próprias somem ou mudam de
casa.

O gate `plan != null` do roteador não é bloqueio real: o caminho rogue do
capturador já roda com objetivo nulo por dentro (`AIController.Capturer.Rogue.cs`
existe exatamente para isso). O que falta é aceitar `plan == null` na entrada e
não assumir QG na âncora.

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
