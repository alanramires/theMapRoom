# Refinamento: Embarque de Capturadores com Quero Carona III

## Versão

`v5.1.3b`

## Objetivo

Consolidar o primeiro grafo de `magnets` da IA e restaurar regras de
coordenação que foram eclipsadas pelos novos fallbacks de direção.

Esta etapa mantém o princípio central:

> O passageiro declara a intenção; o transportador apenas a materializa.

## Magnet como fonte de intenção

Um magnet pode ser uma construção, unidade aliada ou necessidade confirmada.
Ele fornece direção operacional para um papel seguidor, sem substituir ataque,
emergência ou decisão Tactical.

O mapa inicial documentado inclui:

- construção não controlada atraindo Capturadores;
- Capturador funcionando como cabeça de ponte para combatentes;
- Vigilância Aérea atraindo Antiaéreo;
- estoque baixo atraindo Stock;
- `UnderRepair` atraindo Logistics;
- Transporter funcionando como alavanca das intenções produzidas pelos demais
  papéis.

## Hierarquia do Antiaéreo

Unidades que satisfazem `Antiaereo` agora usam a seguinte ordem:

1. Radar Móvel terrestre com rota estrutural válida;
2. EWACS em posição estruturalmente alcançável;
3. Capturador como fallback;
4. comportamento antigo somente quando nenhuma dessas âncoras existe.

Radar Móvel vence EWACS porque compartilha o domínio terrestre com o SAM.
Distância cúbica isolada não é suficiente: uma Vigilância Aérea sobre mar ou
componente desconectado não pode arrastar o SAM até uma costa sem saída.

Artilharia comum continua usando Capturador.

## Âncora preservada entre passagens

O Fire Support rogue possuía dois resolvedores de direção.

O primeiro podia registrar:

```text
CapturerMagnet=#112
```

mas a progressão seguinte substituía silenciosamente a âncora por:

```text
aliado avançado #81
```

As duas passagens agora usam a mesma hierarquia de magnets. O diagnóstico
identifica tipo, unidade, posição e distância.

## Validação do SAM

Resultado confirmado em jogo:

```text
Surface Air Missile #92
origem=(-21,-15)
AirSurveillance:RadarMovel=#119
anchor=(-24,-16)
distância inicial=4h
destino=(-23,-14)
distância final=2h
```

A progressão preservou a mesma âncora:

```text
rogue tool-progress rendezvous
anchor=(-24,-16)
AirSurveillance:RadarMovel=#119
```

O SAM não possuía alvo ou pressão Tactical e se aproximou do Radar Móvel para
formar o embrião de uma rede de defesa aérea.

## Fire Support e transporte

O teste do Lança-Foguetes #39 registrou duas necessidades para o refactor:

- Fire Support não deve pedir carona porque não encontrou prédio capturável;
- `ReachableNow` com transportador na LZ e movimento suficiente deve produzir
  `mover + embarcar`, não somente progressão parcial.

Esses pontos permanecem como evidência para a futura finalidade tipada
`CombinedArmsEscort` ou `SectorPressure`.

## Assault preserva a conquista do capitão

A proteção histórica do Assault reconhecia apenas construções declaradas no
`TeamObjectivePlan`. Alvos de Capturadores rogue/rebeldes, escolhidos por
proximidade, escapavam da regra.

Com o Capturador usado como magnet, isso permitiu que um tanque acompanhasse o
capitão e ocupasse exatamente sua próxima conquista.

Agora o Assault rogue:

- resolve a próxima construção declarada pelo capitão;
- evita esse hex no movimento normal;
- continua podendo usá-lo durante combate;
- só tolera ocupá-lo sem combate quando nenhuma alternativa materializável
  existe.

## Validação do Assault

Resultado confirmado:

```text
Tanque de Batalha #74
capitão=#13 em (-16,-7)
captura declarada=(-16,-8)
origem=(-19,-7)
destino escolhido=(-17,-9)
```

O tanque preservou `(-16,-8)` para o Soldado #13 e ainda obteve cinco pontos de
progresso em direção à cabeça de ponte.

## Contrato transacional

Os magnets e alvos preservados são projeções puras do snapshot confirmado:

- não ocupam célula;
- não confirmam reserva persistente;
- não alteram FOW ou detecção;
- não consomem recursos;
- não modificam `HasActed`;
- são recalculados após compromisso e retorno a `Neutral`.

Combate, movimento, embarque e demais efeitos continuam sendo materializados
pelo batch transacional normal.

## Validação técnica

- `Assembly-CSharp.csproj`: 0 erros;
- `Assembly-CSharp-Editor.csproj`: 0 erros;
- `git diff --check`: limpo;
- SAM #92 escoltando Radar Móvel #119: validado em runtime;
- Tanque #74 preservando a conquista do capitão #13: validado em runtime.

