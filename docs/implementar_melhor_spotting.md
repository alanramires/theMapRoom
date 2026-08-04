# Implementar o `MelhorSpotting`

Documento de trabalho. **Nada desta ferramenta foi implementado ainda.** O
objetivo é separar uma pergunta orientada a missão da pergunta geral já
respondida pelo `MelhorVisaoService`.

```text
Melhor Visão       onde esta unidade produz a melhor cobertura geral?
Melhor Spotting    quem consegue iluminar estes alvos, e de qual posição?
```

A primeira versão deve funcionar em `Tools > Hotzone > Melhor Spotting`, no
Scene Edit e no runtime, sem consumidor da IA. Captura e artilharia entram só
depois de a resposta ser auditável no tabuleiro.

O Melhor Spotting é uma **variante orientada a missão do Melhor Visão**. Com
observador fornecido, procura o melhor local daquela unidade. Sem observador
forçado, avalia as unidades candidatas do slot e devolve, para cada uma, o melhor
local técnico. A AI continua dona de escolher quem realmente atenderá ao
chamado.

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

Há duas formas da mesma pergunta:

```text
observador fornecido      encontre o melhor local desta unidade
observador não fornecido  encontre as unidades capazes e o melhor local de cada
```

No segundo caso, o serviço não declara automaticamente um “spotter recomendado”.
Ele devolve uma lista tecnicamente comparável. Missão atual, iniciativa,
prioridade do papel e custo de abandonar outra tarefa pertencem à AI que pediu a
consulta.

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

Isso já resolve o caso aéreo sem regra especial. Um caça com visão 4 não precisa
chegar ao hex `(0,0)`: qualquer origem alcançável cuja cobertura contenha o alvo
serve — por exemplo `(0,4)`, se alcance, camada e LoS autorizarem. O serviço
procura posições de observação, não uma rota até o objetivo.

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

MelhorSpottingService — por unidade
    filtra e ranqueia as origens que cumprem uma missão de iluminação

MelhorSpottingService — consulta sem observador forçado
    repete a avaliação por candidata e devolve o melhor local de cada uma
```

O `MelhorSpottingService` responde **quem possui solução mecânica e de onde**.
Ele pode ordenar resultados pela qualidade técnica, mas não escolhe qual unidade
abandonará sua agenda. Esse último passo permanece no coordenador da AI.

A unidade que pede visão e a candidata a observar são conceitos separados. Uma
artilharia pode ser `Requester` e um soldado ser `Observer`; se nenhum aliado
for conveniente, a própria solicitante também pode aparecer como candidata,
desde que sua visão e seu envelope produzam uma solução.

“Está em outra missão” não é invisibilidade mecânica e não deve desaparecer no
serviço. A AI pode recusar candidatos ocupados e cair para a entrada da própria
solicitante. Se ninguém mais alcança uma posição válida, mas ela alcança, o
resultado naturalmente contém apenas ela e o respectivo hex.

Também não cria batch, não movimenta a unidade e não concede visão antecipada.

---

## 4. Contrato proposto

O contrato nasce plural, mesmo que a primeira janela permita escolher apenas um
hex.

```text
MelhorSpottingRequest
  Requester                       unidade que necessita da informação, opcional
  ForcedObserver                  restringe a consulta a esta unidade, opcional
  ObserverSlot                    necessário quando não há observador forçado
  CandidateObservers              lista pré-coletada, opcional
  Map
  TerrainDatabase
  DpqAirHeightConfig
  Layer
  ObjectiveCells                 conjunto de hexes a iluminar
  ObjectivePolicy                All | Any | AtLeastPercent | Maximize
  RequiredCoveragePercent        limiar da missão, quando aplicável
  ObjectiveWeights               pesos opcionais fornecidos pelo consumidor
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
| `AtLeastPercent` | cobre no mínimo o percentual exigido da área |
| `Maximize` | ilumina algum objetivo; vence primeiro quem cobre mais |

Um único prédio é `ObjectiveCells` com um elemento e política `All`.

Com `ForcedObserver`, o resultado contém o ranking de locais daquela unidade.
Sem ele, o resultado agrega uma entrada por unidade mecanicamente capaz:

```text
MelhorSpottingObserverResult
  Observer
  IsRequester
  BestLocation
  LocationRanking
  CoveredObjectiveCells
  CoveragePercent
  Diagnostic
```

A lista pode ser ordenada tecnicamente, mas não deve possuir um
`RecommendedObserver` com semântica estratégica. A AI escolhe o recomendado
depois de considerar suas outras missões.

Resultado por origem:

```text
Cell
MovementCost
CoveredObjectiveCells
UncoveredObjectiveCells
ObjectiveCoverageCount
ObjectiveCoverageRatio
LineOfSightQuality
LineProfile
VisionScore
Coverage
Reason
```

O resultado por unidade deve trazer `Origin`, `Ranking`, `Best` e um diagnóstico
explícito quando nenhuma origem tática satisfizer a missão. A consulta agregada
traz a lista desses resultados, inclusive a própria solicitante quando ela for
capaz de atender.

---

## 5. Ordem do ranking

O alvo não deve disputar pontos com cobertura geral. A ordem é lexicográfica:

1. satisfazer a política obrigatória de objetivos;
2. cobrir mais objetivos, quando a política for `Maximize`;
3. melhor qualidade de linha de visão até o objetivo;
4. melhor nota geral já calculada pelo `MelhorVisao`;
5. preservar cobertura exclusiva atual;
6. menor custo de movimento;
7. desempate estável por coordenada.

### Melhor linha descendente, não apenas maior EV

Quando o observador é fornecido, duas origens podem enxergar o mesmo alvo e
ainda assim não serem equivalentes. Uma montanha alta pode produzir uma linha
descendente limpa; outra, cercada por uma cadeia de montanhas vizinhas, pode
passar raspando ou perder vários corredores. Logo, “escolher o maior EV” é uma
aproximação errada.

A qualidade deve vir do **perfil completo da mesma LoS usada pelo sensor**:
altura da reta em cada intermediário, margem sobre o obstáculo mais próximo e
direção da descida. O Melhor Spotting não deve reimplementar essa geometria. Se
a autoridade atual só devolve visível/bloqueado, será necessário expor um
diagnóstico puro compartilhado — por exemplo margem mínima da linha — para que
a ferramenta diferencie duas soluções válidas.

Para um alvo único, primeiro vale “enxerga ou não enxerga”; a robustez da linha
ordena as origens que enxergam. Para um envelope, cobertura percentual vem antes
da qualidade de uma linha isolada.

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

- fornecer opcionalmente uma unidade observadora;
- sem unidade forçada, escolher o slot e avaliar as candidatas aliadas;
- distinguir visualmente a unidade solicitante da observadora;
- escolher a camada (`Auto`, `All` ou camada específica);
- clicar em um hex objetivo;
- calcular usando o envelope tático;
- atalho `Cozinhar FOW 0`;
- botão `Limpar`;
- listar unidades capazes, o melhor local de cada uma e suas alternativas;
- listar posições válidas e o motivo das descartadas;
- selecionar uma posição para inspecionar sua cobertura.

Vocabulário visual:

- alvo com anel magenta/vermelho;
- bolinhas azuis nas origens que iluminam o objetivo;
- dourado na melhor origem;
- linha tracejada da origem selecionada para cada objetivo coberto;
- cinza discreto para origens alcançáveis que falham na iluminação;
- rótulo curto com unidade, quantidade/percentual coberto e custo.

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

- contrato plural de objetivos;
- composição do ranking do `MelhorVisao`;
- filtro obrigatório por objetivo;
- políticas `All`, `Any`, `AtLeastPercent` e `Maximize`;
- avaliação da linha completa, sem reduzir qualidade a EV da origem;
- resultado explicável, sem efeitos colaterais.

### Etapa 3 — busca por observador

- separar `Requester` de `ForcedObserver`;
- sem observador forçado, avaliar candidatas do slot;
- devolver o melhor local por unidade, não uma decisão de agenda;
- sempre considerar a própria solicitante quando ela for mecanicamente capaz;
- permitir que o chamador forneça a lista já filtrada, evitando nova varredura.

### Etapa 4 — `MelhorSpottingWindow`

- observador opcional, slot, solicitante e objetivo;
- lista de unidades candidatas e locais por unidade;
- bake, cálculo, limpeza e desenho no Scene View;
- auditoria em Edit Mode e runtime.

### Etapa 5 — ponte com `MelhorCaptura`

`PodeCapturar` não consulta o Melhor Spotting. O `MelhorCaptura` continua
devolvendo suas três posições e, na posição `fow/ação`, uma chave/status
`SpottingRequired`. O papel lê essa recomendação e decide se chama o serviço de
spotting.

O serviço de captura não escolhe nem move o spotter. Ele apenas entrega o hex
objetivo e a razão pela qual a captura ainda não é materializável.

### Etapa 6 — cobertura de artilharia

A artilharia fornece como `ObjectiveCells` a região de tiro que interessa. O
ranking mede a interseção:

```text
cobertura hipotética do spotter ∩ hexes úteis da artilharia
```

Isso não autoriza conhecimento de inimigos ocultos. O conjunto pode conter
geografia e hexes de tiro potenciais; contatos só entram se já estiverem no
snapshot confirmado.

A requisição também carrega o percentual mínimo desejado. A resposta de cada
unidade informa quantos hexes e qual percentual do envelope ela iluminaria. Uma
montanha, um observador aéreo ou a combinação futura de ambos aparecem como
consequência das mesmas regras, não como casos especiais.

Artilharia costuma demandar a vanguarda porque opera na retaguarda, mas essa
direção é política do consumidor. Ela deve fornecer `ObjectiveCells` ou
`ObjectiveWeights` concentrados na frente; o Melhor Spotting não adivinha onde
fica a vanguarda.

### Plano B — mais de um spotter

Se nenhuma unidade cobrir tudo, um coordenador pode resolver cobertura
incremental:

```text
Spotter A cobre 1, 2, 3
restam 4, 5
Spotter B é consultado apenas para 4, 5
```

É um problema de cobertura de conjunto e pertence ao coordenador. O serviço
agregado pode devolver várias unidades e seus melhores locais, mas não combina
missões nem compromete mais de uma peça sozinho.

---

## 10. Critério de aceite da primeira versão

1. Um soldado com movimento tático 3 só recebe origens realmente alcançáveis.
2. Com um prédio como objetivo, nenhuma origem que não o ilumine pode vencer.
3. Sem observador forçado, a ferramenta devolve as unidades capazes e o melhor
   local de cada uma; não escolhe quem abandona a missão atual.
4. A própria solicitante aparece como fallback quando consegue iluminar o alvo.
5. Uma unidade aérea pode vencer parando no limite do alcance visual, sem ser
   atraída artificialmente até o hex objetivo.
6. A origem atual vence quando já ilumina o alvo e mover não acrescenta valor.
7. Floresta e montanha produzem as mesmas respostas do `Hex Enxergado` para a
   mesma origem, camada e flags de LoS.
8. Entre duas montanhas válidas, o resultado considera o perfil completo da
   linha e não apenas o EV da origem.
9. Objetivo impossível retorna ranking vazio admissível e motivo claro.
10. Dois ou mais objetivos distinguem `All`, `Any`, `AtLeastPercent` e
    `Maximize`.
11. Uma requisição de artilharia retorna cobertura por unidade em número e
    percentual, sem presumir a direção da vanguarda.
12. Runtime usa snapshot confirmado; Edit Mode usa bake quando disponível.
13. Mover, pintar ou remover peça não recozinha o bake automaticamente.
14. Calcular não muda unidade, FOW, memória, contatos, recursos ou revisões.
15. `PodeCapturar` e `MelhorCaptura` não chamam o spotting internamente.
16. Nenhum `AIController` consome a primeira entrega.
