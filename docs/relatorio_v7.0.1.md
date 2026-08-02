# v7.0.1 — O alvo de captura tem um dono, e o desembarque tem um preço

Duas frentes que não se cruzaram no código e se cruzaram no jogo.

De um lado, a captura passou a ter **uma única fonte** de "qual prédio". Do
outro, o desembarque passou a **custar o hex de destino**, como o contrato já
mandava.

---

## Parte 1 — Melhor Captura, e o capturador que perguntava duas vezes

### O problema não era o código, era a camada

A pergunta *"qual construção esta unidade captura?"* estava respondida em cinco
lugares, cada um com predicados próprios. Nenhum estava errado isoladamente. O
problema é que eles podiam **discordar** — e discordavam.

O caso concreto, achado rodando um turno:

```
1. EvaluateCapturerRideNeed → QueroCarona INVENTA um alvo    ← resolução A
2. decide embarque com base em A
3. TryResolvePlanlessCapturerAnchor → FindNearest…           ← resolução B
4. marcha para B, tendo recusado carona por A
```

A unidade perguntava *"preciso de carona pro meu alvo?"* **antes de decidir qual
era o alvo dela**. O serviço de carona inventava um pra conseguir responder, a
resposta decidia o embarque, e aí o capturador resolvia o alvo de novo, por
outro caminho, e jogava o primeiro fora.

### `MelhorCapturaService` — a consulta

Nasceu no formato da família (`Evaluate(Request) → Result`, `ranking` + `best`,
política por delegate). Vale mais pelo que **não** sabe:

> Não sabe se a unidade tem plano, qual o setor dela, se a facção tem QG, ou
> qual o papel dela. O único canal é o conjunto de candidatas mais o filtro
> `includeConstruction`. **Um setor chega como "estas quatro construções",
> nunca como "o setor C".**

A permissão é toda do `PodeCapturar`, projetado na célula da construção. O
serviço não lê skill, não compara `TeamId`, não reimplementa elegibilidade. A
consequência não foi planejada: `RecoverAlly` passou a funcionar de graça.

O alcance vem do envelope nas bandas Tactical e Operational. **Fora das duas não
abre pathfinding** — distância cúbica, porque ali a resposta útil é "para que
lado" e não "por qual estrada". É também a defesa contra a inundação de
tabuleiro por candidata, que já custou 43 s nesta base.

Junto veio `Tools > Hotzone > Melhor Captura`, para olhar antes de trocar.

### O eixo errado que apagava a reconquista

Dois sítios descartavam candidatas com `construction.TeamId == unit.TeamId`
**antes de perguntar ao sensor**. Além de ser o eixo errado — time, não slot —
isso significava que, no jogo, **capturador nenhum era mandado reconquistar
prédio aliado sob captura**. Nem os com plano, no próprio setor.

Não era decisão: era um predicado à mão que ninguém tinha conferido contra o
sensor.

### A inversão

O conserto não foi lógica nova. O `CaptureOpportunityClaimService` **já alocava
um alvo por capturador** pelo matching 1:1 — só não havia como perguntar *"o que
sobrou pra mim?"*. Faltava um índice reverso.

```
CaptureOpportunityClaimService  ← já decidia UM alvo por capturador
        ↓ TryGetClaimForUnit(unit)
   ┌────┴────┐
carona    âncora        ← os dois LEEM a mesma alocação
```

O `QueroCarona` voltou a receber o alvo por `useExplicitTarget` — canal que já
existia, e que a Vigilância Aérea já usava certo — e voltou a responder só *"eu
chego sozinho?"*.

### O efeito que ninguém programou

O navio de transporte voltou a esperar na praia. **Sem uma linha tocada no
transporte.**

O `MelhorEmbarque` não decide necessidade de carona; ele recebe por delegate
(`evaluateRideNeed`). O ranking do transporte é inteiramente downstream da
resposta de carona. Antes, vários passageiros "recusavam carona" por um prédio
que não era deles, e o navio perseguia quem não precisava dele. Consertada a
resposta, o ranking se consertou sozinho:

| passageiro | resposta | ajuste |
|---|---|---|
| tem alvo alocado | `OpportunisticFallback` | **−5000** |
| sem alvo, sem rota própria | `Requested` | **+1500** |
| alvo tomado por outro | `Requested` | +1000 |

É a primeira evidência real de que a camada está certa: um papel passou a
consumir a fonte certa e o vizinho melhorou de graça.

### Performance: a medição me desmentiu

Hipótese: o sensor por candidata era o gargalo. Foram consertadas três sangrias
reais — `GetConstructionAtCell` fazia `FindObjectsByType` da cena inteira por
chamada; o `MatchController` era resolvido por varredura antes de saber se seria
usado; o esforço de captura era calculado para 2120 candidatas e descartado,
porque **nenhum consumidor da IA lê a nota**.

Depois, a ordem foi invertida: banda antes do sensor, cortando ~80% das
perguntas caras.

**O tempo não se mexeu.** Um caso subiu, outro caiu — ruído. O sensor nunca foi
o gargalo.

Onde o tempo está, pelo próprio log: `MelhorCapturaReachBuilds:16` e
`turnChainedCostMap` com um terço do total. São os **16 envelopes** que o claim
service constrói, um por capturador, dos quais só um consegue reusar. Isso
existia antes deste refactor, com outro nome.

As mudanças ficaram por um motivo que a medição **não** prova: o custo do sensor
cresce com o número de construções e o do envelope não. Em mapa de 64 dá empate;
em mapa grande deve valer. É argumento, não evidência.

### Um bug evitado a tempo

`CollectCaptureCandidates` serve dois clientes com perguntas **opostas**: a
escolha de alvo quer só o alcançável; a fome estrutural pergunta exatamente
sobre o que está longe. Fixar o corte na função declararia **encalhada** — com
nota de urgência máxima — uma unidade com capturável alcançável a pé a muitos
turnos. Virou parâmetro obrigatório, sem default, para ninguém acertar por
acidente depois.

---

## Parte 2 — O desembarque custa o destino

O contrato já dizia que o hex de destino do passageiro tem regra e preço
próprios. O código cobrava outra coisa.

`UnitData` ganhou três listas, editáveis por unidade:

| campo | o que delimita |
|---|---|
| `validDisembarkLocationTerrains` | terrenos válidos para o **hex de destino** |
| `validDisembarkLocationTerrainStructures` | pares estrutura + terreno base |
| `validDisembarkLocationFacilities` | categorias de construção |

O `PodeDesembarcarSensor` passou a validar o destino contra essas listas
(`IsDestinationAllowedByTransporterDisembarkRules`) e a cobrar o **custo de
entrada do hex de destino** (`disembarkCost = enterCost`) em vez de um valor
fixo. As recusas ganharam motivo específico — construção, estrutura, par
estrutura+terreno, ou "fora dos terrenos válidos" — em vez de um "não pode"
genérico.

Um caso explícito no código: destino que **não é entrada terrestre** não
participa das listas nem do custo do terreno local.

A migração dos dados exigiu dois passos (`639c02e`, depois `947919f` para
restaurar valores que a migração mexeu) — o lembrete de sempre de que editar
`.asset` no disco com o inspector aberto faz o reimport descartar a memória da
Unity.

---

## O estado real do refactor

**Não terminou.** O que foi feito é a espinha.

| | |
|---|---|
| ✅ | `MelhorCapturaService` + ferramenta |
| ✅ | claim service consome o serviço; `IsEligibleConstruction` deletado |
| ✅ | `QueroCaronaService` parou de varrer o tabuleiro |
| ✅ | ordem invertida: alocação → carona e âncora |
| ⬜ | 7 varreduras de tabuleiro e 6 arquivos com `IsCapturable` ainda no `Capturer/` |
| ⬜ | `QueroCaronaContext { ComPlano, RogueOuRebelde }` |
| ⬜ | `Rebel.cs` |

### O achado que reordena a fila

O `Rebel.cs` **vazou para fora do capturador**. `FindNearestPlanlessCaptureTarget`
e `IsRebelCapturable` são chamados por `MelhorDesembarque` (5 sítios),
`Transportador.Courier.Disembark` (2), `Transportador.Courier.Passengers`,
`Transportador.Naval`, `Assault.HQBreaker` e `Phase2`.

**Transporte, Assalto e Desembarque decidem alvo de captura chamando funções do
rebelde.** É a mesma pergunta que o Melhor Captura responde, feita por três
papéis diferentes, através de um quarto.

Ele não é "o passo depois do capturador" — é a **ponte** para os degraus 4 e 5.
Matar aquelas duas funções converte três papéis de uma vez.

---

## O número que não mudou

`IsCapturable` aparecia em **27 arquivos** e 7 papéis no começo. Saiu de dois.

Continua sendo o mesmo teste final: os 7 perfis chamando uma fonte única, não 7
perfis com 7 definições diferentes.
