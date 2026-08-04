# Implementar o `MelhorSpotting`

Documento de trabalho. **Nada desta ferramenta foi implementado ainda.** O
objetivo é separar uma pergunta orientada a missão da pergunta geral já
respondida pelo `MelhorVisaoService`.

```text
Melhor Visão       onde esta unidade produz a melhor cobertura geral?
Melhor Spotting    onde esta unidade precisa terminar para iluminar estes alvos?
```

A primeira versão deve funcionar em `Tools > Hotzone > Melhor Spotting`, no
Scene Edit e no runtime, sem consumidor da IA. Captura e artilharia entram só
depois de a resposta ser auditável no tabuleiro.

---

## 1. O problema concreto

O `MelhorCaptura` já distingue um prédio conhecido que está encoberto:

```text
chegada/ocupação   dentro do alcance, com ou sem vaga para terminar
fow/ação           encoberto pela névoa; spotting necessário
cap/reconquista    elegível, mas não materializável agora
```

Falta responder à consequência: dado um soldado próximo, **em qual célula do
seu envelope tático ele deve terminar para iluminar o prédio?**

O mesmo buraco aparece no fogo indireto. A artilharia pode ter alcance sobre
uma região inteira e nenhum alvo legal porque os hexes ainda não foram
iluminados. Ela precisa pedir a outro papel uma precondição, não inventar visão
dentro do próprio controlador.

`FocusCells` não resolve isso hoje. No `MelhorVisaoService`, foco é apenas um
termo da pontuação (`FocusWeight`). Uma posição que não cobre o objetivo ainda
pode vencer somando cobertura geral. Para spotting, cobrir o objetivo é
**condição de admissibilidade**, não prêmio.

---

## 2. O que já existe e deve ser composto

### `UnitReachEnvelopeService`

Fornece as origens hipotéticas onde a unidade pode terminar dentro da banda
`Tactical`, com custo real para unidades de superfície e geometria cúbica para
aeronaves. A posição atual também participa.

### `VisionCoverageService`

Responde de forma pura quais células seriam visíveis a partir de
`ObserverCell`. Já passa `virtualObserverCell` ao `PodeDetectarSensor` e preserva
alcance, LoS, EV, domínio, altura, conectividade aquática e especializações.

O `MelhorSpotting` não chama janelas como `Hex Enxergado` e não copia a regra
delas. Ele chama a mesma autoridade que sustenta essas ferramentas.

### `MelhorVisaoService`

Já cruza envelope tático, ocupação final e cobertura hipotética, devolvendo um
ranking completo. Ele continua dono da pergunta geral e pode fornecer o
ranking-base ao `MelhorSpotting`.

### `FogKnowledgeSnapshot`

O snapshot confirmado no runtime e o bake manual da rodada zero no Edit Mode
já carregam:

- células geograficamente visíveis;
- cobertura de sensores e células conhecidas;
- inimigos visíveis;
- contribuições por observador e por alvo.

Esse contexto deve ser recebido pronto. A ferramenta não pode recalcular a
percepção de todo o time para cada célula hipotética.

---

## 3. Fronteira entre os serviços

O desenho deve permanecer em três degraus:

```text
VisionCoverageService
    cobertura estrutural de UMA origem hipotética

MelhorVisaoService
    ranking de cobertura geral nas origens do envelope tático

MelhorSpottingService
    filtra e ranqueia as origens que cumprem uma missão de iluminação
```

O `MelhorSpottingService` responde **onde esta unidade pode servir de spotter**.
Ele não escolhe qual unidade receberá a missão. Um coordenador futuro poderá
consultar vários candidatos e comparar o melhor resultado de cada um.

Também não cria batch, não movimenta a unidade e não concede visão antecipada.

---

## 4. Contrato proposto

O contrato nasce plural, mesmo que a primeira janela permita escolher apenas um
hex.

```text
MelhorSpottingRequest
  Observer
  Map
  TerrainDatabase
  DpqAirHeightConfig
  Layer
  ObjectiveCells                 conjunto de hexes a iluminar
  ObjectivePolicy                All | Any | Maximize
  MovementBudget
  EnableLos
  ValidateFinalOccupancy
  KnowledgeSnapshot              confirmado ou bake, opcional
  VisionScoringPolicy            desempate entre candidatos admissíveis
```

Semântica das políticas:

| política | uma origem é admissível quando |
|---|---|
| `All` | ilumina todos os objetivos |
| `Any` | ilumina pelo menos um objetivo |
| `Maximize` | ilumina algum objetivo; vence primeiro quem cobre mais |

Um único prédio é `ObjectiveCells` com um elemento e política `All`.

Resultado por origem:

```text
Cell
MovementCost
CoveredObjectiveCells
UncoveredObjectiveCells
ObjectiveCoverageCount
ObjectiveCoverageRatio
VisionScore
Coverage
Reason
```

O resultado geral deve trazer `Origin`, `Ranking`, `Best` e um diagnóstico
explícito quando nenhuma origem tática satisfizer a missão.

---

## 5. Ordem do ranking

O alvo não deve disputar pontos com cobertura geral. A ordem é lexicográfica:

1. satisfazer a política obrigatória de objetivos;
2. cobrir mais objetivos, quando a política for `Maximize`;
3. melhor nota geral já calculada pelo `MelhorVisao`;
4. preservar cobertura exclusiva atual;
5. menor custo de movimento;
6. desempate estável por coordenada.

EV/DPQ pode ser exibido e futuramente entrar numa política de consumidor, mas
**não é prioridade universal do spotting**. Uma posição defensiva excelente que
não ilumina o objetivo é inválida; entre duas que iluminam, o propósito da
missão decide se segurança deve superar cobertura adicional.

---

## 6. Bake e estado confirmado

Há duas perguntas diferentes:

```text
o que o time enxerga agora?              snapshot/bake confirmado
o que este observador enxergaria dali?   VisionCoverageService hipotético
```

O bake não substitui a projeção: ele fotografa as posições atuais. A projeção
não substitui o bake: ela não pode inventar que o movimento já aconteceu.

No runtime, o `MelhorSpotting` recebe o snapshot confirmado do slot. No Scene
Edit, recebe o bake persistido no `MatchController`. Se o autor mover uma peça
depois de cozinhar, o bake fica deliberadamente velho até apertar `Cozinhar FOW
0` novamente; a ferramenta deve mostrar essa origem no diagnóstico, não
recozinhar silenciosamente.

Antes do `MelhorSpotting`, o próprio `MelhorVisaoWindow` ainda precisa aprender a
consumir esse mesmo snapshot/bake. Hoje ele consulta exploração runtime e monta
cobertura aliada novamente.

---

## 7. Contrato transacional

Toda posição é uma projeção consultiva:

- não altera `CurrentCellPosition`, domínio, altura ou ocupação;
- não publica FOW nem grava exploração;
- não atualiza stealth, contatos ou inteligência da IA;
- não consome movimento, autonomia ou ação;
- não transforma o objetivo em visível dentro da mesma ação provisória.

Se um controlador escolher o resultado, ele cria somente um batch de movimento.
A visão real aparece após o compromisso, retorno a `Neutral` e reconstrução do
snapshot confirmado. Só então captura ou artilharia podem agir com a nova
informação.

---

## 8. Ferramenta de Scene View

Menu: `Tools > Hotzone > Melhor Spotting`.

Primeira entrega:

- selecionar uma unidade;
- escolher a camada (`Auto`, `All` ou camada específica);
- clicar em um hex objetivo;
- calcular usando o envelope tático;
- atalho `Cozinhar FOW 0`;
- botão `Limpar`;
- listar posições válidas e o motivo das descartadas;
- selecionar uma posição para inspecionar sua cobertura.

Vocabulário visual:

- alvo com anel magenta/vermelho;
- bolinhas azuis nas origens que iluminam o objetivo;
- dourado na melhor origem;
- linha tracejada da origem selecionada para cada objetivo coberto;
- cinza discreto para origens alcançáveis que falham na iluminação;
- rótulo curto com quantidade coberta e custo.

A linha tracejada reaproveita a gramática visual do `MelhorDesembarque`, mas aqui
tem significado próprio: **esta origem satisfaz este objetivo de spotting**.

---

## 9. Ordem de implementação

### Etapa 1 — levar o bake ao `MelhorVisao`

- receber contexto perceptivo pronto;
- derivar cobertura aliada sem o observador pelas contribuições por fonte;
- manter o caminho estrutural bruto quando não houver snapshot;
- não cozinhar automaticamente ao pintar ou remover unidades.

### Etapa 2 — `MelhorSpottingService`

- contrato plural;
- composição do ranking do `MelhorVisao`;
- filtro obrigatório por objetivo;
- políticas `All`, `Any` e `Maximize`;
- resultado explicável, sem efeitos colaterais.

### Etapa 3 — `MelhorSpottingWindow`

- seleção de unidade e objetivo;
- bake, cálculo, limpeza e desenho no Scene View;
- auditoria em Edit Mode e runtime.

### Etapa 4 — ponte com `MelhorCaptura`

Quando um alvo retornar `spotting necessário`, o consumidor poderá fornecer sua
célula ao `MelhorSpotting`. O serviço de captura não escolhe nem move o spotter.

### Etapa 5 — cobertura de artilharia

A artilharia fornece como `ObjectiveCells` a região de tiro que interessa. O
ranking mede a interseção:

```text
cobertura hipotética do spotter ∩ hexes úteis da artilharia
```

Isso não autoriza conhecimento de inimigos ocultos. O conjunto pode conter
geografia e hexes de tiro potenciais; contatos só entram se já estiverem no
snapshot confirmado.

### Plano B — mais de um spotter

Se nenhuma unidade cobrir tudo, um coordenador pode resolver cobertura
incremental:

```text
Spotter A cobre 1, 2, 3
restam 4, 5
Spotter B é consultado apenas para 4, 5
```

É um problema de cobertura de conjunto e pertence ao coordenador. O
`MelhorSpottingService` continua respondendo por uma unidade de cada vez.

---

## 10. Critério de aceite da primeira versão

1. Um soldado com movimento tático 3 só recebe origens realmente alcançáveis.
2. Com um prédio como objetivo, nenhuma origem que não o ilumine pode vencer.
3. A origem atual vence quando já ilumina o alvo e mover não acrescenta valor.
4. Floresta e montanha produzem as mesmas respostas do `Hex Enxergado` para a
   mesma origem, camada e flags de LoS.
5. Objetivo impossível retorna ranking vazio admissível e motivo claro.
6. Dois ou mais objetivos distinguem corretamente `All`, `Any` e `Maximize`.
7. Runtime usa snapshot confirmado; Edit Mode usa bake quando disponível.
8. Mover, pintar ou remover peça não recozinha o bake automaticamente.
9. Calcular não muda unidade, FOW, memória, contatos, recursos ou revisões.
10. Nenhum `AIController` consome a primeira entrega.

