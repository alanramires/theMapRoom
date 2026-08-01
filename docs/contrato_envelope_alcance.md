# Contrato do envelope de alcance

Contrato definido pelo autor. É a especificação de referência do
`UnitReachEnvelopeService`. Onde o código diverge, o código está errado.

## Cores

| cor | significado |
|-----|-------------|
| verde | Tactical — o que a unidade faz nesta rodada |
| azul | Operational — o turno seguinte (`+MP`) |
| vermelho | alcance de arma (inclui "armas" logísticas) |

`MP` é teto **por turno** e vem do `UnitData`. Não acumula: `+MP` é um segundo
turno, não orçamento dobrado. Um soldado de 3 MP faz 3+3, nunca 6 — por isso na
montanha de custo 2 ele entra em uma no verde e mais uma no azul.

## A Hotzone só devolve o que é materializável

O envelope responde seis coisas e nada além: **Tactical**, **Operational**,
**movimento**, **ação**, **custo**, **origem da ação**.

**Não existe banda estratégica.** Strategic não é um envelope finito — é a IA
dizendo *"meu objetivo está fora dessas bandas; qual direção sigo ou preciso de
transporte?"*. Essa pergunta é dela.

Pelo mesmo motivo, o envelope **não varre o tabuleiro** procurando objetivo,
não chama `PodeCapturar` e não conclui "dispensa carona". A inteligência de
objetivos já tem lugar próprio:

- alocação 1:1 — `CaptureOpportunityClaimService`
- objetivo persistido da unidade — `AIController.Rebel`
- validação final da construção — `PodeCapturarSensor`

`ClassifyMobility` fica como **consulta dirigida**: a IA entrega um alvo
específico e pergunta se é `OwnComponent`, `OtherComponent` ou `NotOccupiable`.

```text
IA escolhe objetivo
        ↓
pede envelope Tactical/Operational
        ↓
envelope responde alcance / custo / origem
        ↓
IA cruza objetivo × alcance
        ↓
IA decide andar, capturar ou pedir carona
```

### Divisão de responsabilidade

| camada | papel |
|---|---|
| Hotzone (ferramenta) | formula o pedido e pinta a resposta |
| Envelope (serviço) | calcula alcance materializável |
| IA | escolhe objetivos, interpreta Strategic e decide |

## Subetapas

Subetapa é **parâmetro de entrada**, como a intenção. O serviço não deduz nada:
quem classifica a unidade e escolhe é o chamador — a IA. O serviço é burro e
reativo.

Só existem três, e elas decidem **apenas a geometria e se a unidade se desloca**:

| subetapa | geometria | desloca |
|---|---|---|
| `Terrestre` | caminhos válidos | sim |
| `Aereo` | distância cúbica | sim |
| `Artilheiro` | mira em cúbica | não (MP=0) |

O alcance de arma (vermelho) é decidido pela **intenção Combate**, não pela
subetapa.

**Híbrido não é subetapa.** Tentar Artilheiro e cair para Terrestre/Aéreo é
comportamento da IA. **Ficção também não existe**: o jogo não tem zepelins.

### Árvore

```text
Mobilidade ── Terrestre, Aereo
Combate ───── Terrestre, Aereo, Artilheiro
Captura ───── (sem ramo: sempre terrestre)
Fusão ─────── Terrestre, Aereo
Suprir ────── Terrestre, Aereo
Estoque ───── Terrestre, Aereo
Embarque ──── Terrestre, Aereo
Desembarque ─ (sem ramo)
```

`UnitReachEnvelopeService.GetSubSteps(intent)` é a fonte dessa árvore.

### Validação pela unidade

`Aereo` exige `isAircraft` no `UnitData`. Pedir geometria cúbica para uma
unidade de superfície é **pedido inválido**, não envelope vazio: `Build`
devolve `null`. Isso é validação de entrada, não dedução.

**Combate + `Terrestre` exige arma de alcance mínimo 1.** Terrestre é "move e
atira", e o tiro pós-movimento colapsa para 1: uma peça de mínimo 2 não dispara
depois de andar. Pedir Terrestre para ela é pedido inválido pelo mesmo motivo —
a pergunta certa é a subetapa `Artilheiro`, cuja banda é da arma.

`GetSubSteps(intent, unit)` devolve a árvore já filtrada pelo que a unidade
suporta; é o que a ferramenta usa para nem oferecer a opção.

## Intenções

### Mobilidade
Verde em MP, azul em `+MP`. Sem vermelho.

A IA decide o que fazer com isso: seguir o capitão, caças nos flancos e
vanguarda, bombardeiros na retaguarda.

### Combate

**No azul não entra a arma.** Não precisa.

| subetapa | retorno |
|---|---|
| `Terrestre` | verde em MP + vermelho no alcance da arma; azul em `+MP` |
| `Aereo` | verde em MP (cúbica) + vermelho no alcance da arma |
| `Artilheiro` | **verde do hex 0 até o alcance máximo** (vermelho sobrescreve); **azul no dobro do alcance máximo** |

### Artilheiro inverte a banda: ela é da ARMA, não do movimento

Para quem atira parado, medir banda em movimento é absurdo. A Artilharia de
Campanha move **1**: o Operational dela seria o hex 2, para uma peça cuja razão
de existir é alcançar longe.

| banda | artilheiro | todo o resto |
|---|---|---|
| Tactical (verde) | 0 → alcance máximo da arma | alcance de movimento |
| Operational (azul) | **2 × alcance máximo** | movimento do turno seguinte |

O azul aqui não é "para onde eu ando" — é **de onde eu posso ser alcançado, ou
alcançar, se a situação mudar**. É a banda de ameaça recíproca, e ela existe por
um motivo concreto:

> Artilharia de alcance 4 **não embarca** se houver inimigo no raio 8. Embarcar é
> ficar indefeso; um blindado rápido a 8 hexes chega e pega a peça com as calças
> na mão.

Ver `docs/AI Behavior/FireSupport.md` §0 e §7.

- Terrestre: a IA decide atacar ou avançar até o oponente no azul.
- Artilheiro: é comum atirar se estiver no tactical. No operational depende —
  na vanguarda recua, na retaguarda talvez avance. Decisão da IA.
- Dupla função (artilheiro combatente, antiaéreo combatente): a IA valida
  `Artilheiro`; se não der, pede `Terrestre` ou `Aereo`.

O tiro parado entra sempre: numa arma de alcance mínimo 1 e máximo 2, o tiro
pós-movimento colapsa para 1, então `MoveuParado` alcança alvos que
`MoveuAndando` não alcança.

### Captura / Reconquista

Verde `+MP`, azul `+MP`. **Só alcance.**

A IA cruza esse alcance com os objetivos que ela escolheu. Capturadores
geralmente recusam carona se conseguem chegar até o azul.

### Fusão

Verde `+MP`, **sem azul**.

Se não consegue fundir no verde, usa Mobilidade para recuar e tentar na próxima
rodada. Na invasão final, fundir na retaguarda é obrigatório.

### Suprir

Verde `+MP` com "arma logística" em vermelho (supply range); azul `+MP`.
Supply range 0 → não há vermelho.

Se não conseguir suprir, pode querer avançar para o operational — mas se isso o
puser na vanguarda, talvez não vá.

### Estoque

Verde `+MP` com "arma de caixa" em vermelho (operational range); azul `+MP`.
Range 0 → não há vermelho.

Se não conseguir estoque, pode querer avançar para o operational; checar
"play conservative" antes de decidir avançar.

### Embarque

Verde `+MP` −MPRequired (custo do terreno), azul `+MP`.

A área verde é menor porque a unidade precisa se posicionar E conservar MP para
entrar no hex do transportador. Montanha e floresta entram, porque pode haver um
APC ali. Se o transportador estiver no azul, vai ao encontro dele.

A subetapa `Aereo` serve para procurar porta-aviões ou fragatas.

Quando o transportador está fora do componente de movimento da unidade, quem
descobre isso é a IA, via `ClassifyMobility` dirigida ao alvo escolhido.

### Desembarque

**Projeção invertida.** Não começa no passageiro nem no transportador: começa no
**objetivo**. A pergunta é *"em quais hexes o passageiro pode ser largado para
alcançar o objetivo depois?"*.

Verde: o MP do **passageiro** projetado ao redor do objetivo. Azul: `+MP`.

Um helicóptero carregando um soldado de 3 MP pode largá-lo em cima do prédio ou
a até 3 hexes dele — em qualquer desses pontos o soldado fecha na rodada
seguinte. Um bazuca de 2 MP projeta 2 hexes.

Desembarque exige **1 MP válido**, por isso o `+1MP` entra; embarcado
`isAircraft` decola, então qualquer hex serve.

Quando o objetivo está cercado — montanhas, ilha — e o passageiro não alcança
por meios normais, o desembarque ainda aproxima, mas o destino **deixa de ser
Tactical**: vira Operational e a unidade precisa de rodadas extras.

#### Três decisões desta intenção

**1. Transportador sem carga devolve `null`.** Pedir "onde eu largo" sem ter o
que largar é pedido inválido, não envelope vazio — mesma regra do `Aereo` sem
`isAircraft` e do Combate sem arma utilizável. Capacidade que a intenção exige,
ausente, é `null`.

**2. A banda é do PASSAGEIRO, e podem ser vários.** Um caminhão com um soldado
de 3 MP e um bazuca de 2 MP carrega **duas bandas ao mesmo tempo**.

O envelope responde **um passageiro por consulta** e nada além disso. A
interseção — *"onde os dois chegam"* — é trabalho do **consumidor**
(`MelhorDesembarque`), que pergunta uma vez por embarcado e cruza os resultados.
É a mesma forma do encontro por cruzamento de zonas que o `MelhorEmbarque` já
usa do lado do embarque.

Ver "As três camadas" no `CLAUDE.md`: cruzar, ordenar e desempatar nunca são do
serviço.

**3. A projeção precisa de um objetivo.** Ela começa no alvo, não na unidade —
então a consulta exige uma célula de destino. A fonte natural é o alvo designado
(`AIDesignatedMissionTargetCell`, ou o alvo de captura), que é o que a IA usa de
verdade; a ferramenta deve aceitar sobrescrita manual, para testar hipóteses sem
mexer no estado da unidade.

> Sem alvo não há desembarque a calcular. Também é `null`.

## Cobertura

Toda missão de unidade cai em alguma categoria acima — seguir o capitão,
abastecimento, e as demais.

---

## MP e Range

**MP é o movimento RESTANTE**, não o máximo. A banda Tactical consulta
`RemainingMovementPoints` e cai para o máximo só quando já está zerado.
Consequência aceita: **a banda encolhe conforme a unidade age na rodada**. Uma
peça de 4 MP com 2 restantes projeta a partir de 2.

**Range é o alcance do efeito** — arma, serviço, coleta, estoque. Nem sempre é
número: serviço e coleta usam **modos**.

```text
SameHexOrEmbarked   mesmo hex, ou sobre embarcados quando a regra permitir
Adjacent1Hex        expande a área de movimento em um hex
Hybrid0Or1Hex       mesmo hex ou um de distância
```

Nesses casos **não se soma `MP + Range`**. O procedimento é:

1. calcular a área alcançável pelo movimento;
2. expandir essa área conforme o modo.

## Resumo das bandas

| intenção | Tactical | Operational | geometria |
|---|---|---|---|
| Mobilidade | MP | MP + MP | caminhos válidos |
| Captura | MP | MP + MP | caminhos válidos |
| Combate — combatente | MP + Range | MP + MP (sem arma) | conforme a unidade |
| Combate — **artilheiro** | **0 → Range máximo** | **2 × Range máximo** | **cúbica** |
| Fusão | MP − custo de entrada | **não tem** | caminhos válidos |
| Embarque | MP − custo de entrada | MP + MP | caminhos válidos |
| Desembarque | MP do passageiro **projetado do objetivo** | MP + MP | invertida |
| Suprir | MP expandido pelo modo de serviço | MP + MP | caminhos válidos |
| Transferir | MP expandido pelo modo de coleta | MP + MP | caminhos válidos |
| Estoque | conforme `operationalRange` | conforme a ficha | conforme a fonte |

O Operational **não é o Tactical vezes dois**. Cada intenção define a sua regra:
no artilheiro é área de preservação, no desembarque é o custo extra de rodadas,
na fusão não existe.

---

## Estado do código

Verificado em `Assets/Scripts/Match/AI/Services/UnitReachEnvelopeService.cs`.

### Conforme

- as três cores e a ausência de banda estratégica;
- `+MP` como turno encadeado (`BuildTurnChainedReach`), não MP×2;
- bloqueio de terreno por teto de MP por turno (`BuildOwnMovementComponent`):
  hex acima do teto é intransponível e **bloqueia o corredor** atrás dele;
- ausência de arma no azul;
- subetapa como parâmetro de entrada, com árvore e validação por unidade;
- Captura devolve só alcance — sem `PodeCapturar` dentro do envelope;
- `ClassifyMobility` só como consulta dirigida.

### Falta

| # | contrato | código hoje |
|---|---|---|
| 1 | Fusão não tem azul | `BuildProfile` sempre devolve Tactical + Operational |
| 1b | **Artilheiro: verde = 0→alcance, azul = 2×alcance** | banda de MOVIMENTO nos dois casos; `BuildFireSupportPaths` devolve `CalcularCaminhosValidos(RemainingMovementPoints)`, consumido por 11 sítios |
| ~~1c~~ | ~~a **ferramenta Hotzone** precisa pintar essa inversão~~ | **feito** — a subetapa `Artilheiro` pinta `ActionCells` por cima do verde |
| 2 | Suprir/Estoque expõem o range como vermelho | range de serviço entra em `ActionCells`, sem distinção de "arma" |
| 3 | Estoque é intenção própria ("arma de caixa") | não existe; só `Service` e `Transfer` |
| 4 | Desembarque é intenção própria, com projeção invertida, `null` sem carga e `null` sem alvo | **parcial** — a janela já tem hex de referência e modalidade `Desembarque`, montada sobre `Mobility` + `OriginOverride`; falta a intenção no serviço (o `+1MP`, o `null` sem carga e o alvo lido de `AIDesignatedMissionTargetCell`) |
| 5 | **a IA consome a banda do artilheiro** | `BuildFireSupportPaths` devolve malha de MOVIMENTO em 11 sítios; a ferramenta já pinta a banda certa, nenhuma decisão a usa |

### Dívida de migração

`UnitThreatEnvelopeService` continua como fachada de compatibilidade dos 4 call
sites antigos da IA. Ela é o único lugar que ainda deriva subetapa da unidade
(`ResolveLegacyCombatSubStep`), para não mudar o comportamento deles. Quando a
IA for migrada arquivo por arquivo, a fachada e `ResolveCombatProfile` somem
juntas.
