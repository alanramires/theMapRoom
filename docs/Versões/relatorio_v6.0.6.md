# Ferramenta de Hotzone como Serviço: Progressão

## Versão

`v6.0.6`

## Objetivo

Este ponto de verificação fecha a banda temporal do envelope. A `v6.0.5`
trocou o modelo de alcance de dois turnos por soma de turnos; faltava o
consumidor **saber em qual turno** cada resposta cai, e faltava a ferramenta de
progressão obedecer o mesmo contrato.

Também traz a verificação em jogo das duas jogadas migradas na versão anterior.

## A banda real da ação

O envelope `Operational` **contém** o `Tactical`. Sem distinguir os dois, o
diagnóstico rotulava de Operational uma ação que se completava na rodada atual —
foi assim que um caminhão de suprimento apareceu no log como plano de dois
turnos tendo movido e atendido na mesma rodada.

`UnitMovementPathRules.CalculateTurnChainedCostMap` já contava os turnos por
célula durante a travessia. Esse mapa passou a subir até o envelope:

```csharp
public readonly Dictionary<Vector3Int, int> TurnsByCell;   // 1 = nesta rodada

public bool TryGetTurns(Vector3Int cell, out int turns);
public ReachBand ResolveActionBand(Vector3Int actionCell);
```

`ResolveActionBand` responde pela **célula de parada** publicada em
`OriginByActionCell`, não pela banda que foi pedida.

Três origens de turno, todas cobertas:

| caso | origem do turno |
|---|---|
| encadeado (Operational terrestre) | contado durante a travessia |
| cúbico (aeronave) | derivado do custo contra o MP restante |
| malha pronta do chamador, ou Tactical | tudo turno 1 |

O diagnóstico da logística passou a publicar a banda verdadeira:

```text
reason=service_hotzone banda=Tactical de=(-31, 10, 0) sobra=7 turnos=1
```

A etapa do coordenador continua reportando `Operational:hit` — ela descreve
qual estágio da cascata respondeu, não a natureza da ação.

## Origem determinística

A origem publicada para as intenções de serviço vinha sem desempate: dependia
da ordem de iteração do conjunto e mudava sozinha quando a travessia mudava.
Agora `RecordServiceOrigin` guarda a origem **mais barata**, com desempate
estável por coordenada.

No mesmo passo, o `RemainingMovement` das intenções de serviço deixou de ser um
zero fixo e passou a ser `orçamento da banda − custo até a célula de parada`.

## Ferramenta de progressão

`Tools > Transporte > Caminhos Válidos` deixou de chamar
`CalcularCaminhosValidos` diretamente. Toda onda de movimento passa pelo
serviço, o que corrige dois problemas de uma vez.

**Geometria por subetapa.** Aeronave resolve `Aereo` e recebe alcance cúbico.
Rodar a onda geográfica numa unidade aérea com muitos pontos de movimento era o
que travava a ferramenta.

**Dois orçamentos.** O turno atual e os turnos seguintes eram o mesmo número, o
que fazia a progressão projetar `3+3=6` para um soldado que na verdade tinha
`1+3=4`. Agora são campos separados, e o botão **PM da unidade** preenche os
dois pela regra do contrato. Selecionar uma unidade na cena adota o MP dela
automaticamente, em vez de herdar o número da seleção anterior.

O slider manual continua existindo para emular unidade hipotética.

Consequência esperada: em aeronave não há rota, logo não há extrato de PM passo
a passo nem bônus de estrada. O alcance é raio, não caminho.

## Verificação em jogo

**Fusão de reparo.** Soldado com 3 MP avaliando quatro células de parada:

```text
[Repair] fusão de (-31,10) mov=1 canFuse=True   ← custo 2, sobra 1
[Repair] fusão de (-31, 9) mov=0 canFuse=False  ← custo 3, sobra 0
→ fusão oportunista com #122, executada
```

A recusa em `(-31,9)` é a regra de conservação de MP: chegou sem saldo para
pagar a entrada no hex do aliado. O custo vem do envelope, não de recálculo
local.

**Logística de campo.** Ação, alvo, pontuação, descartes por `PodeSuprir` e
execução idênticos ao comportamento anterior à migração, agora com banda e
origem publicadas.

**Progressão.** Aeronave calculada em geometria cúbica, sem travamento.

## Achados que não são deste refactor

**O planner exige setores mapeados.** Num mapa de teste sem topologia,
`BuildObjectivePlan` degenerou e custou cerca de 60 segundos por chamada, duas
vezes por turno. Com os setores mapeados caiu para 4,5 segundos. Os sinais que
identificam o estado degenerado: `sectors=1 bases=0`, objetivos repetidos do
mesmo setor e `stale defense owner=Neutral`.

**Reconstrução fria do SectorManager.** Com 29 setores, o primeiro rebuild
custa cerca de 11 segundos, com 1300 buscas de distância falhando e trezentas
mil células expandidas. O cache absorve tudo: o segundo rebuild custa 30 ms com
zero expansões. É custo único de abertura.

**Duas perguntas, dois códigos.** `FindLogisticsServiceTarget` escolhe o alvo
que justifica o deslocamento; `TryBuildLogisticsSupplyAction` escolhe a célula
e o cliente varrendo os caminhos por conta própria. Elas podem discordar — e
discordaram em teste, uma escolhendo `#121` e a execução atendendo `#123`. É
estrutural e anterior a este trabalho, mas agora está documentado com
evidência.

## Pendências conhecidas

- As duas ativações da logística continuam separadas; unificá-las na mesma
  consulta ao envelope resolve a divergência acima e o custo repetido.
- `UnitThreatEnvelopeService` permanece como adaptador dos consumidores ainda
  não migrados.
- O contrato prevê banda ausente para Fusão e Artilheiro, alcance logístico em
  vermelho, e as intenções `Estoque` e `Desembarque`, que ainda não existem.
- `InitiativeSetup` custa cerca de 2 segundos para 50 unidades, dois terços
  disso na classificação de grupos. Não tem relação com o envelope.
