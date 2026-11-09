# Rally — proposta: o montinho que anda

Status: **proposta, sem código.** Decisões do autor em 2026-11-08, numa conversa só. O que
ainda está aberto tem seção própria no fim.

## A ideia

> *"Toda a ideia do rally é sempre fazer montinho e andar junto. Faz um montinho e avança, faz
> um montinho e avança."*

O rally é a bandeirinha de reunião: as produtoras mandam as tropas recém-criadas para um ponto,
elas formam um montinho e depois avançam juntas para o castelo. O autor pôs três bandeirinhas
porque não sabe por onde a IA vai chegar, nem se ela vai controlar uma ou todas: *"onde ela
conseguir fazer um montinho, ela avança pra atacar."*

**A bandeira é só o primeiro montinho.** O que importa é o montinho; a bandeira é onde ele nasce.

### Quem já faz isso

O Civilization V tem exatamente esta máquina: o exército da IA (`CvArmyAI`) passa por quatro
estados:

```text
WAITING_FOR_UNITS_TO_REINFORCE   montinho no muster point (a bandeira)
WAITING_FOR_UNITS_TO_CATCH_UP    parou no caminho esperando os de trás
MOVING_TO_DESTINATION            avança
AT_DESTINATION                   chegou ao cerco
```

O exército mede o espalhamento com `GetCenterOfMass` e `GetFurthestUnitDistance`, e estima o
turno de chegada de cada vaga (`EstimatedTurnAtCheckpoint`). Em 2011 um patch corrigiu o raio do
muster da invasão naval porque **as invasões ficavam presas montando**. É o mesmo risco nosso, e
é por isso que as saídas (abaixo) importam tanto quanto o mínimo.

A IA do Age of Empires 2 monta *attack groups* e os solta juntos, mas depois de solto o grupo não
espera ninguém: é "junta e solta", não "anda junto".

## Decisões

| tema | decisão |
|---|---|
| âncora | **onde o autor marcou "rally"**: o prédio com `isRallyPoint` (em terra, é preciso conquistá-lo), ou a bandeira invisível no mar. Depois que o montinho avança, a âncora é **o próprio montinho** |
| ativação | **o rally só entra em formação quando o eixo atrás dele é seu**: os setores anteriores a ele no eixo do slot (autorado no `QuadranteData`, ou o leque automático do `InvasionAxisMap`) estão controlados. Regra única para terra e mar. Motivo: a bandeira no mar nasce sua e induziria a IA a montar no oceano ignorando os capturáveis de trás; em terra, protege contra um capturador largado lá na frente |
| área | Tático da **unidade-modelo** a partir da âncora = **core**; Operacional dela = **em aproximação** |
| unidade-modelo | mora no **`QuadranteData`**, num `[Header]` próprio, como um **par terra/mar** (terrestre: Soldado). A âncora escolhe: âncora em terra usa o terrestre, no mar usa o naval. Nenhuma bandeira é configurada uma a uma, e vale com eixo autorado ou automático |
| embarcados | **contam** no core (o transporte está no core, o passageiro conta). Esperar em terra é preferível enquanto o core se monta |
| piso | 4 capturadores **e** (2 de assalto terrestre **ou** 1 de ataque aéreo) |
| opcionais | fogo de suporte (0 a 1+) e transportadores (0 a 1+). Somam, mas sozinhos não disparam a invasão. *"Artilharia não trava mais formação de montinho"* |
| múltiplos rallies | *"quando um rally point completa seu montinho, todos os demais são liberados de suas formações pra juntar forças à invasão em andamento"* |
| saídas | 2:1 no tabuleiro inteiro dispara o GoGreen em todos, como a conscrição para com ampla vantagem. Domínio macro também |
| ambiente | funciona no **Editor** (sem gameplay; a ferramenta controla o farol) e em **Play** |

```text
rally A: assembly
rally B: assembly
rally C: completed   →  all rally go!
```

**Quem os liberados reforçam (P6, fechado):**

- o **capitão do montinho que completou**;
- se dois completarem no mesmo turno, cada unidade vai ao grupo em avanço que alcança primeiro
  pelo **alcance Operacional real** (o envelope dela, não a distância em hexes);
- quem não alcança nenhum **fica na própria bandeira** e começa o próximo montinho.

Então "um montinho libera todos" quer dizer: **todos saem da espera, mas só partem quando
conseguem se integrar a um grupo em avanço.** Ninguém avança sozinho.

### A régua do "avança" (recomendada, aguardando o autor)

```text
piso        4 capturadores + (2 assalto terrestre ou 1 ataque aéreo)
avança se   piso E (core >= razão × inimigo local   ou   o montinho saturou)
       ou   2:1 no tabuleiro   ou   domínio macro
```

- **Inimigo local:** inimigo conhecido (névoa confirmada) dentro do Operacional do core, no sentido
  do alvo. É a superioridade local dos wargames: ganha quem concentra no ponto, não quem tem mais
  no mapa. Hoje o código escala o pacote pelo inimigo **do mapa inteiro** (`KnownEnemyForce / 8`),
  e com inimigo grande o montinho nunca fecha.
- **Razão:** recomendada 1,5:1, por perfil no `AIPresetData` (Fácil 1:1, Normal 1,5:1,
  Difícil 2:1). Com 1:1, a névoa esconde parte do inimigo e o "empate" costuma ser inferioridade;
  com 3:1, quase sempre quem dispara é a saturação, e a régua da força deixa de decidir.
- **Saturação:** o montinho parou de crescer (ninguém chegando no Operacional, ou chegando menos
  do que morre). Pergunta: *"o que eu ganho esperando mais um turno?"*. Substitui o relógio fixo
  `RallyAssemblyTimeoutTurns = 4`, que só adivinha. É o que impede a invasão de nunca começar; a
  frase do autor: *"um local concentra a batalha até enfraquecer um dos lados"*.

### Quem lê cada número

Todo número precisa de um leitor, senão vira constante enfeitada:

| peça | leitor |
|---|---|
| core (Tático) | a contagem do piso e da força |
| em aproximação (Operacional) | **raio de recrutamento**: quem está nele é chamado para o montinho; quem está além segue a vida (outro rally, captura). E o farol mostra "chegando" |
| opcionais | desempate do foco entre rallies |
| inimigo local | a razão do "avança" |

A compra **não** é freada pelo montinho: o gathering hall existe para puxar gente.

## O desenho: três camadas, não um sensor

O nome de trabalho foi `PodeFormarRally`, mas os sensores `Pode*` respondem "esta unidade pode
fazer esta ação". Aqui são três perguntas, uma por camada (CLAUDE.md, "dumb service, consumer,
organizer"):

| camada | peça | responde | roda no Editor? |
|---|---|---|---|
| serviço burro | **área do rally** | dada uma âncora e uma unidade-modelo: core (Tático) e aproximação (Operacional) | **sim**, só terreno |
| consumidor | **prontidão do montinho** | quem está no core (embarcados inclusos), quem está chegando, piso, força, inimigo local, saturação | sim, com as unidades da cena |
| organizador | `AIController` | GoGreen, liberação dos outros rallies, quem espera em terra, a parada no caminho | só em Play |

**Editor e Play com a mesma lâmpada.** O farol já é desenhado pelo `ConstructionManager`
(`[ExecuteAlways]`, `ResolveRallyHudState`, em `Assets/Scripts/Construction/ConstructionManager.cs`)
a partir de um dicionário estático (`rallyHudStates`, escrito por `PublishRallyHudState` em
`AIController.PlanEvaluator.RallyPoints.cs`). Em Play quem escreve é o planner; no Editor, a
ferramenta. A lâmpada não muda. A área e a prontidão precisam ser estáticas e não depender de um
`AIController` vivo; o `EvaluateRallyReadiness` de hoje já é `static`.

### O ciclo

```text
REFORÇANDO    o montinho se forma na bandeira (a âncora é a bandeira)
AVANÇANDO     bateu a régua; a âncora passa a ser o montinho (o capitão, que o assalto já orbita)
ESPERANDO     esticou demais: gente saiu do Tático do core → para até os de trás voltarem
NO ALVO       cerco; daqui em diante é o UpdateInvasionMonitor
```

A âncora do avanço deve ser **o capitão** (o capturador que o magnético já usa), não um centro de
massa geométrico: o capitão é uma célula real onde a unidade está, e o resto da IA já sabe orbitá-lo.

## O que muda em relação a hoje

Estado em `Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs`:

| hoje | proposta |
|---|---|
| raios fixos por papel: força 3, ataque aéreo 5, artilharia 4, vigilância 6, logística 5 (linhas 10–25) | uma área só: Tático e Operacional da unidade-modelo |
| embarcado **não** conta (`EvaluateRallyReadiness`, `unit.IsEmbarked` → pula) | conta, se o transporte está no core |
| artilharia obrigatória (`hasArtillery`, `RallyMinimumArtillery = 3`) + válvula da "ruptura esmagadora" para mapas sem artilharia | opcional; a válvula perde a razão de existir |
| pacote escala com o inimigo do mapa inteiro (`RequiredPackages`) | piso fixo + razão contra o inimigo **local** |
| os rallies conquistados **somam** a massa (`BuildRallyAggregate`) e viram GoGreen juntos | **um** montinho completo libera todos; os outros vão reforçar o que avançou |
| timeout fixo de 4 turnos | saturação |
| 2:1 (`numericalDominance`, linha 402) e domínio macro (`macroDominating`) | **ficam** |
| depois do GoGreen, só "fracassou → remonta" (`UpdateInvasionMonitor`) | parada no caminho (ESPERANDO) quando o montinho estica |

Isso também resolve, pela raiz, o Chinook #72 de Golf (marca de 2026-11-08): dois soldados
embarcados num `RallyAssembly` não contavam para a massa, e a entrega procurava um prédio para
capturar no rally. São **dois ajustes, e um não substitui o outro**:

- **prontidão:** os passageiros contam quando o transporte está no core;
- **entrega:** o organizador manda descarregar **no core**, sem exigir prédio capturável; onde
  cada passageiro pode descer continua sendo do `MelhorDesembarque`/`PodeDesembarcar`. Só contar
  o embarcado não faz a carga descer.

### Regras de contagem

- **Cada unidade entra uma vez só**, pela `InstanceId`: o passageiro conta pela ficha dele, não
  duas vezes (no chão e "dentro do transporte").
- **O transporte conta só nas categorias que a própria ficha satisfaz**: um Chinook é transporte
  (opcional), não capturador; um blindado com arma pode ser também assalto se a ficha disser.
  Os rótulos são lidos com `CanSatisfy`/`MatchesLabel`, nunca `roles[0] ==`.

### O que fica igual

- `ShouldHoldRallyAssemblyInsteadOfEmbark` (`AIController.Capturer.Embark.cs`): quem já está no
  core não pega carona. Passa a ler o core da área em vez do raio 3.
- Os mínimos de captura e o `PodeCapturarSensor` não mudam: o rally decide **quando** ir, não se
  pode capturar.

## Riscos técnicos

1. **A unidade-modelo no Editor.** O `UnitReachEnvelopeService` recebe um `UnitManager` (unidade em
   campo), e o soldado-modelo não existe na cena. Saídas: uma instância oculta do prefab, ou o
   envelope aceitar um `UnitData`. Este é o ponto que mais pode dar errado; resolver antes do resto.
2. **Registros de runtime mentem no Edit mode** (memória "ferramenta de editor vê a cena"):
   `UnitManager.AllActive` e os snapshots de ocupação podem estar vazios ou velhos. No Editor a
   prontidão conta as unidades pela cena.
3. **A área é por terreno, a contagem por unidade.** A área não pode depender de ocupação, senão o
   core encolhe quando o próprio montinho se forma em cima dele.
4. **Rally dos dois lados** (`rallyOwnerSlots` é lista): a área é por rally, a prontidão por slot,
   igual à lâmpada de hoje.

## O mar (depois que a área existir)

O rally naval é uma âncora (a bandeira invisível de `project_naval_rally_flag`) com uma
unidade-modelo naval. Os fogos de suporte navais **orbitam o Tático do rally no mar**, bombardeando
a praia e cobrindo o local enquanto os navios se aproximam com a carga. É comportamento de
organizador: o `FireSupport` usa a âncora do rally como capitão. Não entra na primeira entrega.

## Ordem sugerida de entrega

1. **Área do rally** (serviço burro) + desenho no Editor, com o farol controlado pela ferramenta.
   Inclui o par de modelos no `QuadranteData`. Resolve o risco 1 primeiro.
2. **Ativação pelo eixo**: o rally só forma com o eixo atrás dele controlado.
3. **Prontidão** (consumidor) trocando os raios fixos: core, embarcados, piso, artilharia opcional.
   As saídas atuais ficam. Comparar o farol novo com o de hoje no mesmo mapa antes de trocar.
4. **Um montinho libera todos** + os outros reforçando o que avançou.
5. **Inimigo local, razão por perfil e saturação** (depois de fechar as abertas abaixo).
6. **Parada no caminho** (ESPERANDO).
7. Rally naval (a bandeira e o modelo naval).

Cada degrau é uma versão `Y` e passa por Play antes do próximo.

## Ainda aberto

- **A razão do inimigo local:** 1,5:1 recomendado, por perfil. Falta o "sim" do autor.
- **O que é "saturou":** quantos turnos sem crescer? Crescer conta unidades ou força?
- **Unidade-modelo naval:** qual unidade, e se a bandeira naval tem setor.
- **Quanto "esticou demais":** sair do Tático do capitão basta, ou é preciso uma fração do core?
- **Assalto aéreo vale 1 e terrestre 2:** o Apache e o caça contam igual?
