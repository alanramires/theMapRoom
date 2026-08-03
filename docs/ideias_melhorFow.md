# Ideias — Melhor Cobertura de FOW

> **Este documento é o `MelhorVisão`.** A `docs/magnetic_tabela.md` registrou, ao
> descrever a atração da Vigilância, que "para onde revelar" é pergunta de
> **campo** e não de ponto, e que faltava um serviço para reduzir campo a ponto.
> É este.
>
> A mesma pergunta está hoje respondida em **três lugares independentes**, cada
> um com pesos próprios: `AIController.Capturer.Explorer.cs` (seis constantes
> `ExplorerForwardObserver*`), `AIController.Transportador.cs`
> (`FindTransportExplorationMove`) e `AIController.VigilanciaAerea.cs`
> (`unexploredMarginal * 25f`). É o mesmo padrão do `IsRebelCapturable`: uma
> pergunta genérica escrita à mão, várias vezes, com respostas que podem
> discordar.

## Objetivo

Criar uma consulta geral, pura e orientada por camada capaz de responder:

> Se esta unidade terminasse seu movimento neste hex, qual cobertura de visão ela
> acrescentaria, preservaria ou perderia para o seu time?

A primeira entrega será a ferramenta de Scene View `Melhor Cobertura de FOW`,
auditada a partir de uma unidade selecionada. O mesmo núcleo poderá ser consumido
mais tarde por papéis da IA, sem recriar regras de visão dentro de cada
`AIController`.

O serviço não deve decidir sozinho se uma unidade deve mover. Ele mede e organiza
as possibilidades. A política de cada consumidor decide quanto vale cobertura,
exploração, direção da missão, segurança, captura ou suporte de fogo.

## Doutrina de conhecimento da IA

A IA pode conhecer a geografia completa e a localização dos objetivos estáticos
do mapa. Ela não precisa fingir que desconhece onde existem cidades, fábricas,
portos, aeroportos ou demais construções.

Entretanto, ela deve respeitar o FOW para conhecer e usar a situação tática desses
locais.

Separação desejada:

- **Conhecimento estratégico:** localização e tipo dos objetivos estáticos do mapa.
- **Conhecimento tático:** proprietário atual conhecido, ocupantes, ameaças,
  contatos e estado ao redor, limitados por FOW e memória confirmada.
- **Ação materializável:** capturar, atacar, detectar ou reagir somente quando os
  sensores e o snapshot confirmado autorizarem.

Assim, um capturador pode marchar deliberadamente para observar uma cidade que
sabe existir. Ele não pode agir como se soubesse antecipadamente quem a ocupa ou
qual é a situação tática escondida pela nevoa.

## Contrato transacional obrigatório

O plano obedece integralmente a `docs/arquitetura/acoes_transacionais.md`.

Toda projeção de cobertura é somente uma hipótese de planejamento:

- não move o `UnitManager`;
- não altera domínio, altura ou ocupação;
- não revela hexes;
- não publica FOW;
- não registra exploração;
- não atualiza contatos, stealth ou `AIIntelLedger`;
- não incrementa revisões confirmadas;
- não substitui caches confirmados por resultados provisórios;
- não consome movimento, autonomia ou ação.

A posição candidata entra na consulta por parâmetro, usando a capacidade já
existente de `virtualObserverCell`. A cobertura definitiva só é recalculada após
o compromisso da ação, o retorno a `CursorState.Neutral` e a publicação do novo
snapshot confirmado.

Para a IA, escolher uma posição que provavelmente revele um objetivo gera um
batch de movimento. A IA não pode tratar o FOW projetado como já aberto dentro
do mesmo estado provisório.

## Fundamentos existentes

### MelhorDesembarque como modelo visual

`MelhorDesembarqueService` e `MelhorDesembarqueWindow` já fornecem o padrão da
nova ferramenta:

- serviço puro separado da janela;
- `Request`, ranking completo, resultado e melhor opção;
- célula atual incluída como candidata;
- pontuação interna e pontuação legível;
- motivo textual decompondo a nota;
- comparação relativa à posição atual;
- discos no Scene View;
- dourado para o melhor resultado;
- gradiente verde para ganho, amarelo para neutro e laranja/vermelho para perda;
- rótulo curto sobre cada candidato.

`MelhorCoberturaFow` deve repetir essa anatomia, trocando passageiros/rotas por
cobertura projetada, ganho, redundância e perda.

### PodeDetectar/PodeEnxergar como autoridade

`PodeDetectarSensor.CollectVisibleCells` já oferece:

- alcance básico e especializações de `UnitData`;
- domínio e altura do alvo;
- política de LoS por especialização;
- EV do terreno, construções, estruturas e alturas aéreas;
- conectividade aquática para `Submarine/Submerged`;
- consulta a partir de `virtualObserverCell`;
- alvo virtual por `Domain + HeightLevel`;
- resultado puro sem publicação de FOW.

O novo serviço deve compor essa autoridade. Não deve copiar curvas de LoS nem
criar uma segunda implementação de visão.

### Hotzone de movimento

`UnitReachEnvelopeService`, com `ReachIntent.Mobility`, é a fonte dos hexes onde
a unidade pode terminar o movimento.

Geometria padrão:

- aeronaves usam alcance cúbico;
- unidades de superfície usam caminhos válidos, custos e geografia reais;
- a posição atual sempre participa;
- a ferramenta pode usar o movimento restante no runtime e o movimento potencial
  no Edit Mode ou quando explicitamente solicitado.

As candidatas aéreas ainda precisam passar pela validação pura de ocupação final
da camada, pois o mapa cúbico materializa alcance geométrico, mas não promete por
si só que o empilhamento final é válido.

### AirSurveillanceCoverageService como protótipo parcial

`AirSurveillanceCoverageService` já calcula parte da resposta desejada:

- cobertura estrutural a partir de uma posição virtual;
- cobertura marginal em relação a aliados;
- redundância/sobreposição;
- cobertura marginal ainda não explorada;
- detecção de stealth por camada;
- pontuação de cobertura;
- cache estrutural por posição e perfil.

Ele está preso hoje à política de Vigilância Aérea:

- nomes e resultado fixos em `AirLow` e `AirHigh`;
- coleta precisa deliberadamente restrita a `Air/High`;
- cobertura aliada formada somente por outras unidades de Vigilância Aérea;
- pesos táticos embutidos no serviço.

Essa implementação é o protótipo do cálculo geral, mas não deve ser expandida por
mais condicionais de Super Tucano, Fragata ou Submarino.

## Dados atuais de EV e LoS

Configuração atual dos terrenos:

| Terreno | EV como obstáculo/alvo | Bloqueia LoS | EV herdado pelo observador |
|---|---:|---|---:|
| Planície | 0 | não | 0 |
| Floresta | 1 | sim | 0 |
| Montanha | 2,25 | sim | 2 |
| Mar | 0 | não | 0 |
| Praia | 0 | não | 0 |

Regras importantes:

- o observador terrestre herda EV somente do `TerrainTypeData` da origem;
- a herança exige `shooterInheritsTerrainEv`;
- `shooterInheritedEvOverride` negativo usa o EV normal do terreno;
- construções e estruturas não participam hoje do EV herdado pelo observador;
- para alvo ou obstáculo, o EV efetivo é o maior entre terreno, construção,
  estrutura e altura aérea aplicável;
- bloqueio é composto por `OR`: qualquer componente bloqueador mantém a LoS
  bloqueável;
- uma estrada ou construção não remove automaticamente o bloqueio da montanha.

No cadastro atual, apenas a planície possui overrides de construção para visão:
Cidade, Aeroporto, Fábrica Média, HQ e Porto recebem EV 1 e bloqueiam LoS. Não há
overrides de estruturas cadastrados, nem overrides de construção para floresta ou
montanha.

## Curvas de LoS

A LoS usa uma única reta interpolada entre o EV da origem e o EV do alvo:

```text
altura_da_linha_no_hex = Lerp(EV_origem, EV_alvo, distância_projetada)
```

A distância é a projeção real do centro do hex sobre a reta origem-alvo. Um
obstáculo bloqueia quando:

```text
blockLoS
e EV_obstáculo > 0
e EV_obstáculo > altura_da_linha + 0,05
```

Essa mesma fórmula produz:

- curva geralmente descendente da montanha para a superfície;
- curva ascendente de uma unidade no chão para uma aeronave;
- curva descendente de Air/High para Air/Low;
- passagem rasante quando o obstáculo empata com a altura da linha dentro da
  tolerância.

### Unidade detectada sobre FOW sem revelar o hex

Visibilidade geográfica e detecção de unidade são resultados diferentes.

Ao consultar uma unidade real:

- o alvo fornece seu EV real de domínio/altura;
- os hexes intermediários usam as camadas nativas do terreno;
- uma curva ascendente pode superar uma floresta;
- um caça em Air/High pode enxergar um helicóptero Air/Low por cima de uma
  montanha.

Ao consultar um hex virtual de uma camada:

- a camada alvo é forçada na consulta;
- o cálculo responde se aquele hex da camada seria coberto;
- a cobertura geográfica pode continuar bloqueada mesmo que uma unidade real
  acima dela seja detectável.

Quando a unidade é logicamente visível e sua célula geográfica continua sob o
FOW, `UnitManager.SetFogDetectedContactPresentation` cria o eco visual acima da
nevoa, dessaturado e com alpha reduzido. A ferramenta deve medir a cobertura da
camada, não confundir esse contato individual com um hex geograficamente aberto.

## Camadas de visão

Sem especialização, a ferramenta usa a visão geral `All`, preservando a resolução
nativa de terra, mar e submarino. `All` não deve ser reduzido artificialmente a
`Land/Surface`.

Com especializações, a janela deve oferecer uma seleção de camada:

- `Auto`;
- `All`;
- cada combinação `Domain + HeightLevel` declarada na ficha;
- expansão explícita das alturas aplicáveis quando a regra usa `allHeights`.

O cálculo geral sempre recebe a camada explicitamente. O serviço não deve
adivinhar a missão tática da unidade.

### Uma consulta, uma camada — a principal

**Unidade com várias camadas de visão especializada não é pontuada em todas.
Uma delas é a principal, e é a única que entra na nota.**

Um EWACS que enxerga 7 em `Air/High` e 5 em `Air/Low` é consultado **uma vez**,
em `Air/High`. O ganho em `Air/Low` continua acontecendo no tabuleiro — ele
simplesmente não disputa a decisão. O que decide onde o EWACS deve estar é a
camada que ele existe para cobrir.

O mesmo vale para o **Radar Móvel**: sua camada principal é `Air/High`.
`Air/Low` é cobertura secundária e não participa da pontuação de posição. A
especialização `Air/Low` não deve carregar a skill de detecção stealth; se essa
skill estiver cadastrada nela, isso é erro da ficha, não uma escolha tática entre
`Air/High` e `Air/Low`.

Isso não é economia de chamada; é o modelo certo. Pontuar as duas produziria uma
soma sem significado, em que uma camada secundária larga poderia vencer a camada
que justifica a unidade.

#### Como a principal é resolvida

Da **ficha**, sem política — é propriedade da unidade, não da missão:

1. sem `visionSpecializations`: `All`;
2. com especializações, vence a que **detecta furtividade** naquela camada
   (`HasStealthDetectionFor`), porque é a que a unidade existe para cobrir;
3. empate entre duas com detecção: vence o maior `vision` da especialização;
4. empate persistente: ordem declarada na ficha, para o resultado ser estável
   entre duas chamadas iguais.

A janela pode sobrescrever manualmente para auditoria — é ferramenta, tem que
conseguir olhar qualquer camada. O runtime usa a principal.

#### Consequência para o `Result`

O retorno **não pode nomear camada**. O `AirSurveillanceCoverageService` atual
carrega `AirLow`, `AirHigh`, `MarginalAirLow`, `MarginalAirHigh`,
`UnexploredMarginalAirHigh`, `DetectsLowStealth` e `DetectsHighStealth` — sete
campos com o nome da camada dentro do tipo. Generalizar não é trocar um
parâmetro: é o `Result` passar a devolver `Layer` + os números daquela camada.

Durante a migração, o serviço antigo pode virar um wrapper que preenche o
`Result` legado a partir da consulta única na camada principal, deixando os
campos da secundária como estão hoje ou zerados — e o comportamento da
Vigilância Aérea é comparado antes de qualquer campo sumir.

## Conhecido, visível e já visto

O FOW atual distingue:

- **geograficamente visível:** hex aberto agora pela visão geográfica;
- **coberto por sensor:** cobertura lógica atual;
- **conhecido:** união atual das camadas relevantes do slot;
- **explorado/já visto:** memória histórica confirmada;
- **unidade visível:** detecção individual, que pode existir sobre um hex ainda
  coberto.

Limitação atual: `fogExploredCellsBySlot` não possui dimensão de camada. Ele sabe
que a célula foi explorada, mas não se isso ocorreu em Air, Surface ou Sub.

Na primeira versão:

- cobertura atual por camada será calculada com precisão pelo serviço;
- `explored` classificará se o hex já foi visto em qualquer camada;
- não será prometida memória histórica específica por camada;
- uma futura evolução poderá criar exploração por camada, se o jogo realmente
  precisar dessa distinção.

## Arquitetura proposta

### 1. VisionCoverageService

Responsável apenas pela cobertura estrutural de uma unidade, numa posição e
camada.

Entrada conceitual:

```text
Observer
ObserverCell
BoardMap
TerrainDatabase
DpqAirHeightConfig
VisionLayer (All ou Domain + HeightLevel)
EnableLos
```

Saída conceitual:

```text
Layer
VisibleCells
VisibleCellCount
DetectsStealthForLayer
Diagnostic
```

Responsabilidades:

- chamar `PodeDetectarSensor` com `virtualObserverCell`;
- usar visão geral ou camada virtual conforme o pedido;
- respeitar alcance, LoS, EV, domínio, altura e conectividade aquática;
- não conhecer objetivo, papel da IA, aliados, pesos ou FOW histórico;
- não produzir efeitos colaterais.

### 2. VisionCoverageEvaluator

Responsável por comparar a cobertura estrutural do candidato com o contexto do
time.

Entrada conceitual:

```text
CandidateCoverage
OriginCoverage
AlliedCoverageWithoutObserver
IsKnown(cell)
IsExplored(cell)
OptionalFocusCells
ScoringPolicy
```

Saída conceitual:

```text
VisibleTotal
MarginalCells
OverlappingCells
UnexploredMarginalCells
KnownRecoveredCells
RetainedUniqueCells
LostUniqueCells
FocusedCellsRevealed
RawScore
DisplayScore
Reason
```

A cobertura aliada deve ser calculada sem a unidade selecionada. Caso contrário,
a contribuição atual da própria unidade contaminaria todos os candidatos e
esconderia a perda causada por abandonar a posição.

### 3. MelhorCoberturaFowService

Responsável por:

- pedir a hotzone `Mobility` da unidade;
- incluir a origem;
- filtrar células onde a unidade realmente pode terminar;
- consultar `VisionCoverageService` para cada candidata;
- consultar `VisionCoverageEvaluator`;
- devolver ranking completo e melhor opção;
- manter a política inicial da ferramenta, não a política definitiva de cada
  papel da IA.

Estrutura conceitual por candidato:

```text
Cell
MovementCost
Coverage
Marginal
Unexplored
Recovered
Overlap
RetainedUnique
LostUnique
FocusedRevealed
DeltaFromOrigin
Score
DisplayScore
Reason
```

### 4. MelhorCoberturaFowWindow

Primeiro consumidor do novo núcleo.

Funcionalidades:

- selecionar unidade pelo Inspector/Scene View;
- autodetectar Tilemap, TerrainDatabase e configuração aérea;
- escolher camada;
- escolher movimento atual ou potencial;
- calcular ranking;
- listar decomposição da nota;
- selecionar uma bolinha e exibir os hexes cobertos por ela;
- opcionalmente comparar origem e candidata;
- aceitar uma célula de foco manual para simular captura, observação ou apoio;
- funcionar em Edit Mode e Play Mode.

## Pontuação inicial da ferramenta

A nota deve ser explicável, não uma caixa-preta. Categorias iniciais:

- hex nunca explorado: peso alto;
- cobertura marginal atual do time: peso alto;
- célula de foco revelada: peso dominante quando existir foco;
- célula explorada mas não conhecida agora: peso intermediário;
- cobertura exclusiva preservada: valor positivo;
- redundância aliada: valor pequeno, mas não necessariamente zero;
- cobertura exclusiva perdida: penalidade forte;
- custo de movimento: desempate leve;
- permanência na origem: sem bônus artificial; ela vence naturalmente quando
  mover não produz ganho líquido suficiente.

Conta conceitual:

```text
score =
    inéditos * peso_inédito
  + marginais * peso_marginal
  + recuperados * peso_recuperado
  + foco_revelado * peso_foco
  + exclusivos_preservados * peso_preservação
  + redundantes * peso_redundância
  - exclusivos_perdidos * peso_perda
  - custo_movimento * peso_custo
```

Os valores definitivos devem ser calibrados com a janela, observando casos reais.
O relatório deve mostrar cada termo e seu subtotal.

## Edit Mode e Runtime

### Edit Mode

Como não existe FOW confirmado, usar cálculo estrutural bruto:

- total de hexes cobertos;
- ganho/perda em relação à origem;
- EV e LoS reais;
- movimento potencial da unidade;
- cobertura de foco, se fornecido;
- sem afirmar conhecido, explorado ou contato real.

O cache deve ser invalidado ou ignorado quando o usuário recalcular após editar
tiles, EVs ou assets. As revisões runtime não são garantia suficiente no Scene
Edit Mode.

### Runtime

Usar somente contexto confirmado:

- slot da unidade;
- cobertura atual dos demais aliados;
- células conhecidas;
- células exploradas;
- movimento restante e estado de ação;
- flags globais de LoS aplicáveis.

O cálculo continua puro. O resultado não deve ser publicado no FOW e não deve
alimentar memória da IA até que uma ação seja escolhida, comprometida e o jogo
retorne a `Neutral`.

## Apresentação no Scene View

Reaproveitar o vocabulário visual do MelhorDesembarque:

- dourado: melhor candidato;
- verde forte: grande ganho sobre a origem;
- verde fraco: pequeno ganho;
- amarelo: equivalente à origem;
- laranja: perda moderada;
- vermelho: perda importante;
- origem marcada de forma distinta;
- raio maior para o melhor resultado;
- rótulo curto, por exemplo:

```text
125
+8 / -2
```

Onde a segunda linha representa ganho marginal e cobertura exclusiva perdida.

Ao selecionar uma bolinha, a janela pode pintar:

- cobertura total da candidata;
- hexes inéditos;
- cobertura marginal;
- redundância;
- cobertura perdida em relação à origem;
- célula ou conjunto de foco.

## Consumidores futuros

Na primeira etapa, nenhum `AIController` será migrado. O novo núcleo será usado
somente pela ferramenta de auditoria.

### Vigilância Aérea

Migração futura:

- EWACS consulta `Air/High`;
- Radar Móvel consulta `Air/High`; sua cobertura `Air/Low` é secundária e não
  disputa a decisão;
- `AirSurveillanceCoverageService` pode virar wrapper temporário do serviço geral;
- comportamento e pesos atuais devem permanecer iguais durante a migração.

### Super Tucano

Pode consultar sua especialização `Submarine/Submerged` para escolher posições
que aumentem a cobertura antissubmarino. A geometria do seu movimento continua
aeronáutica; a camada observada é submarina. Movimento e camada de visão são
eixos independentes.

### Fragata

Pode avaliar `Submarine/Submerged` usando conectividade aquática e suas skills de
detecção. A política da Fragata decide quanto vale ampliar cobertura, preservar
um corredor naval ou apoiar outra unidade.

### Caça submarina — o hex pontuado VIRA o capitão

Este é o caso que fecha o vínculo com o `MelhorCapitaoService`, e a decisão do
autor é explícita:

> **A caça submarina usa o hex pontuado por esta ferramenta como seu capitão** —
> não uma construção fixa, não uma unidade em campo.

É a resposta para o limite registrado em `docs/magnetic_tabela.md`:

> Construção é ponto. Névoa é campo. Ponto é enumerável, fixo e nomeável — por
> isso serve de âncora. Campo não.
>
> Vigilância naval caçando submarino não tem construção embaixo d'água. A
> referência é o próprio oceano não explorado.

O encaixe não precisa de nada novo. O `MelhorCapitaoAttraction` já tem
`hasFixedCell`, criado para a `RepCell` ser "capitão abstrato":

```text
MelhorCoberturaFow (campo → ponto)          MelhorCapitao (ponto → referência)
  "qual hex revela mais Submerged?"  ─────►   entra como atração de célula fixa
```

A lista de atração da caça submarina fica:

```text
hex de melhor cobertura Submerged  →  Capitão
```

O `MelhorCapitao` continua sem saber o que é névoa; ele recebe uma célula. E a
unidade continua orbitando uma referência, como todo mundo — só que a referência
dela é calculada em vez de existir no tabuleiro.

**Consequência para o `AICaptainData`:** o enum `AICaptainAttractionKind` já tem
`PontoDeObservacao` reservado para isto. O predicado por trás dele é uma chamada
ao `MelhorCoberturaFow`, e é o único da tabela que depende deste documento.

### Submarino

Pode usar o mesmo núcleo em `Submarine/Submerged`, `Surface` ou `Air`, conforme a
especialização e a intenção escolhida pelo controlador. O serviço não escolhe a
camada por ele.

### Capturer

Problema atual: quando um objetivo está coberto pelo FOW e os caminhos válidos não
materializam a aproximação desejada, o Capturer possui uma aproximação hardcoded
para tentar abrir a nevoa.

Fluxo futuro:

```text
objetivo estratégico conhecido
    ↓
captura ainda não materializável por FOW
    ↓
avaliar posições da hotzone tática
    ↓
usar a construção/corredor como foco
    ↓
escolher o hex que melhor revela o objetivo sem perder cobertura importante
    ↓
criar batch somente de movimento
    ↓
commit e retorno a Neutral
    ↓
publicar FOW confirmado
    ↓
reavaliar captura com a nova informação tática
```

A localização real do prédio pode ser usada como foco porque faz parte do
conhecimento estratégico permitido à IA. Proprietário atual, ocupantes e ameaças
continuam sujeitos ao FOW e à memória.

### Assalto e Fire Support — o pedido de spotter

Uso futuro desejado:

- identificar quais hexes de uma artilharia ainda precisam ser liberados;
- permitir que uma unidade de vanguarda receba valor por abrir esses FOWs;
- penalizar o soldado que abandona uma montanha e apaga o único contato disponível
  para a artilharia;
- apoiar o mecanismo existente de passar iniciativa para a artilharia amaciar o
  alvo;
- depois devolver a iniciativa a uma unidade capaz de liberar a visão necessária.

#### A demanda de visão é derivada, não autorada

O conjunto de foco deste caso **não precisa ser escrito por ninguém**. É uma
subtração de conjuntos, e as duas metades já existem:

```text
o que a artilharia precisa que abram
  =  banda de tiro dela  −  o que o time já conhece
```

A banda de tiro vem do `UnitReachEnvelopeService` na subetapa `Artilheiro`, e ela
só serve para isso **desde a v7.0.2**: antes devolvia o disco cheio `0..alcance`,
o que incluiria a zona morta do alcance mínimo e o próprio hex da peça. Hoje
devolve exatamente os anéis que alguma arma cobre — pedir visão para a zona morta
seria pedir que abrissem hexes onde a peça não atira.

O "já conhece" é o FOW confirmado, que o `VisionCoverageEvaluator` consulta de
qualquer jeito.

#### Quem calcula, e quando

Mesmo padrão do `CaptureOpportunityClaimSnapshot`: **um snapshot por time,
construído uma vez por estado confirmado**, e todo mundo lê.

```text
uma vez por (slot, revisão confirmada):
    para cada artilharia aliada:
        demanda += BandaDeTiro(ela) − Conhecido(time)

qualquer unidade que se move:
    focus = demanda
    → ganha nota por abrir hex que alguém precisa
```

**O soldado não precisa saber que artilharia existe.** Ele recebe um conjunto de
foco e o `MelhorCoberturaFow` faz o resto.

E o outro lado fecha sem regra nova: o soldado que abandona a montanha e apaga o
único contato leva a penalidade **porque perdeu cobertura exclusiva de um hex em
foco** (`LostUniqueCells`), não porque alguém escreveu "não saia da montanha".
Isso troca uma exceção por uma consequência.

#### O prédio na montanha é o mesmo mecanismo

Prédio conhecido mas oculto e banda escura de artilharia são **o mesmo pedido** —
*"abram estes hexes para mim"*. Um conjunto de foco de tamanho 1 e outro de
tamanho 20; o EV herdado da montanha faz o trabalho nos dois.

Não são dois recursos. É um, usado duas vezes.

## Substituição de flags autorais

Hoje uma `construction flag` pode ser usada como indicação manual de um bom
forward observer spot. Isso grava no mapa uma avaliação do autor.

Com a nova ferramenta, a qualidade da posição pode emergir de:

- EV herdado;
- obstáculos e curvas de LoS;
- cobertura da camada relevante;
- cobertura exclusiva do time;
- objetivo que precisa ser observado;
- alcance das armas que dependem do observador;
- movimento e segurança da unidade candidata.

As flags existentes devem permanecer até a migração dos consumidores estar
validada. Se forem apenas marcadores autorais de posição, poderão ser removidas
depois que a decisão emergente provar paridade ou superioridade.

## Separação entre medição e política

O serviço geral não deve carregar pesos específicos de papéis.

Exemplos:

- `VisionCoverageService` responde quantos hexes `Submerged` a Fragata cobriria;
- a política da Fragata decide quanto isso vale diante da missão atual;
- `VisionCoverageService` responde quantos hexes `Air/High` o EWACS cobriria;
- a política do EWACS combina cobertura com distância do capitão, ameaça e
  recuperação;
- `MelhorCoberturaFowService` possui pesos didáticos para auditoria, não uma
  doutrina universal imposta aos controladores.

Isso preserva a caixa de Lego: mesma medição, prioridades diferentes.

## Sequência sugerida de implementação

### Etapa 1 — Serviço estrutural geral

1. Criar o descritor de camada de visão.
2. Criar `VisionCoverageRequest/Result`.
3. Implementar cobertura a partir de `virtualObserverCell` usando
   `PodeDetectarSensor`.
4. Cobrir `All`, Air, Surface e Submerged.
5. Não alterar nem migrar `AirSurveillanceCoverageService` ainda.
6. Não adicionar consumidores de IA.

### Etapa 2 — Avaliador contextual

1. Criar comparação com origem.
2. Calcular cobertura aliada sem o observador.
3. Separar marginal, redundante, inédita, recuperada, preservada e perdida.
4. Adicionar foco opcional.
5. Produzir pontuação legível e diagnóstico completo.

### Etapa 3 — Hotzone e ranking

1. Integrar `ReachIntent.Mobility`.
2. Resolver geometria aérea ou geográfica pela autoridade existente.
3. Filtrar ocupação final.
4. Incluir a posição atual.
5. Gerar ranking completo.

### Etapa 4 — Janela Melhor Cobertura de FOW

1. Criar janela em `Tools/Hotzone` ou `Tools/FoW`.
2. Implementar seleção e autodetecção.
3. Implementar seletor de camada.
4. Implementar modo Edit/Runtime.
5. Pintar bolinhas e detalhes de cobertura.
6. Validar manualmente os cenários de floresta, montanha, Air e Submerged.

### Etapa 5 — Migrações futuras, uma por vez

1. Capturer e sua aproximação para abrir FOW.
2. Vigilância Aérea, preservando comportamento atual por wrapper.
3. Fragata.
4. Super Tucano.
5. Submarino.
6. Coordenação entre vanguarda e Fire Support.
7. Remoção de flags autorais comprovadamente obsoletas.

Cada migração deve ser localizada, comparada com o comportamento anterior e
reversível sem exigir que todos os papéis mudem juntos.

## Não objetivos da primeira versão

- não ensinar todos os `AIController` a usar cobertura;
- não alterar a publicação oficial do FOW;
- não criar memória explorada por camada;
- não decidir missão, capitão, alvo ou iniciativa;
- não substituir sensores Pode*;
- não remover imediatamente flags ou fallbacks existentes;
- não permitir ação baseada em FOW meramente projetado;
- não recalcular ou publicar visão durante movimento provisório.

## Riscos e cuidados

### Pesos didáticos podem não ter leitor

O documento propõe que `MelhorCoberturaFowService` tenha "pesos didáticos para
auditoria". Cuidado: na v7.0.2 foi **medido** que nenhum dos dois consumidores de
IA do `MelhorCapturaService` lê a nota dele — os dois reordenam por critério
próprio. O serviço calculava poder, pontos restantes e turnos de captura para
2120 candidatas por chamada e jogava tudo fora.

Se acontecer o mesmo aqui, os pesos didáticos são custo sem leitor.

Sugestão: `ScoringPolicy` **obrigatório e sem default**, como o
`includeBeyondOperational` do Melhor Captura. Quem chama diz o que vale; a
ferramenta passa a sua política didática explicitamente. Assim ninguém herda
pesos por acidente, e desligar o cálculo caro é uma linha.

### Performance

Calcular LoS completa para todos os candidatos pode ser caro. Estratégias possíveis:

- cache estrutural por unidade, posição, camada, mapa e revisões relevantes;
- pré-filtro barato antes da avaliação precisa;
- limite de candidatos precisos em consumidores runtime;
- reutilização da hotzone já calculada;
- invalidação explícita no Edit Mode;
- contadores de performance equivalentes aos já existentes em Vigilância Aérea.

O pré-filtro não pode decidir o resultado final sozinho. Ele apenas reduz o
conjunto que receberá a avaliação autoritativa.

**Comece com contador, não com cache.** O número que decide é
`LoS completa × candidatos da hotzone` — um EWACS com 60 células candidatas e uma
varredura de mapa por LoS é exatamente o padrão que já custou **43 s** na v6.0.x
e **71 s** numa decisão naval nesta base. Um contador em `AIDecisionPerf` é de
graça e diz onde o tempo está; cache escolhido antes da medição já se provou
otimização do lugar errado — na v7.0.2, cortar 80% das chamadas ao sensor de
captura não moveu o tempo, porque o custo estava nos envelopes.

Ler código não acha gargalo.

### Caches em Edit Mode

Mudanças em tiles e assets podem não incrementar revisões runtime. Recalcular na
janela deve limpar o cache próprio ou usar uma chave que reflita alterações de
topologia e dados relevantes.

### Cobertura por camada versus exploração global

Até existir memória por camada, “já explorado” significa já visto em qualquer
camada. A interface e os diagnósticos devem dizer isso claramente.

### All Heights

Especializações `allHeights` precisam ser materializadas nas alturas válidas do
domínio. Não basta reutilizar cegamente o `heightLevel` armazenado como rótulo da
regra.

### Ocupação aérea

Alcance cúbico não é autorização de parada. Toda candidata deve respeitar domínio,
altura, locks pendentes e `OccupancyResolver`.

## Critérios de aceite da ferramenta

1. Selecionar um soldado na planície e na montanha produz coberturas diferentes
   segundo o EV real.
2. A origem sempre aparece e pode vencer o ranking.
3. Mover para uma célula que apaga cobertura exclusiva recebe penalidade visível.
4. Hexes nunca explorados recebem destaque distinto no runtime.
5. Edit Mode funciona sem MatchController e informa que a conta é estrutural.
6. A ferramenta avalia uma posição virtual sem mover a unidade.
7. Calcular, selecionar bolinhas ou fechar a janela não altera FOW, memória,
   contatos, recursos, ocupação ou revisões.
8. Aeronaves usam hotzone cúbica e unidades de superfície usam caminhos reais.
9. Submerged não atravessa terrenos sem conectividade submarina.
10. Uma aeronave detectável sobre um hex coberto não é contada automaticamente
    como abertura geográfica daquele hex.
11. O relatório explica a nota com subtotais reproduzíveis.
12. O resultado permanece igual antes e depois de abrir a janela novamente, desde
    que o tabuleiro confirmado não tenha mudado.

## Cenários mínimos de teste

- soldado em planície comparado com soldado em montanha;
- floresta entre observador e alvo de superfície;
- observador terrestre olhando aeronave acima da floresta;
- caça Air/High olhando helicóptero Air/Low por cima da montanha;
- unidade aérea detectada em degradê sobre hex ainda coberto;
- Radar Móvel pontuando somente `Air/High`, com `Air/Low` tratado como cobertura
  secundária fora da decisão;
- Fragata consultando `Submarine/Submerged`;
- Super Tucano consultando `Submarine/Submerged` com movimento cúbico;
- Submarino usando camada submarina e outra especialização;
- posição atual com cobertura exclusiva importante;
- movimento que aumenta área total, mas perde o único contato útil atual;
- cidade conhecida estrategicamente, porém coberta pelo FOW;
- foco manual sobre cidade e corredor de aproximação;
- candidato bloqueado por ocupação na mesma camada;
- cancelamento de uma ação após consultar a projeção;
- retorno a Neutral e publicação normal do FOW somente após commit.

## Visão de longo prazo

`MelhorCoberturaFow` é a primeira peça de uma auditoria de decisão por unidade.

A ferramenta não tenta prever sozinha toda a jogada da IA. Ela responde uma
pergunta reutilizável e verificável: onde esta unidade presta o melhor serviço de
visão dentro de sua mobilidade atual?

Depois, os controladores podem combinar essa resposta com seus próprios sensores:

```text
posição materializável
    + cobertura de visão
    + objetivo conhecido
    + ameaça e segurança
    + possibilidade de capturar/atirar/suprir
    + coordenação com capitão e aliados
    = decisão contextual do papel
```

O resultado desejado é substituir aproximações hardcoded e marcadores autorais
por avaliações emergentes, explicáveis e baseadas nas mesmas regras que governam
o tabuleiro real.
