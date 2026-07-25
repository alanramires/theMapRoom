# v4.3.0 - Refactor de FOW para AI (antes)

Esta versão estabelece o marco anterior ao próximo refactor de Fog of War, execução de batches e apresentação de turnos da AI.

O objetivo do marco é preservar o estado reproduzível no qual foram medidos três problemas relacionados:

1. cold cache do slot da AI após carregar um save;
2. risco de memória visual da AI aparecer para o observador humano;
3. custo total do turno da AI e seus gargalos reais.

## Diagnóstico 1: primeira passagem para a AI

O save v17 restaura a fotografia de contribuições do observador ativo, mas não persiste fotografias de todos os slots.

Na primeira passagem para o Vermelho:

```text
FoW AI: total=5405 ms hits=0 misses=36
FoW apresentação: total=3357 ms hits=0 misses=25
ApplyActiveTeam.FogAndVisibility=9029 ms
AdvanceTurnTransitionRoutine.Total=10405 ms
```

O primeiro cálculo das 36 fontes da AI é cold com o formato atual. O recálculo das 25 fontes do observador humano é redundante.

## Diagnóstico 2: gameplay e apresentação

Durante um turno da AI existem duas perspectivas:

- `gameplaySlot`: participante que executa e atualiza seus dados;
- `presentationSlot`: participante local cujo FoW pode ser renderizado.

A camada visual de memória e overlay é compartilhada. Um contexto DataOnly da AI não pode escrever nessa camada, pois isso revela ao humano lugares conhecidos apenas pelo adversário.

Esse princípio também vale quando o executor for alimentado por:

- jogador remoto;
- segundo jogador local;
- replay;
- teste automatizado.

A origem do batch não concede autoridade de apresentação.

## Proteção visual presente neste marco

O caminho incremental passa a:

1. reativar a fotografia runtime do slot da AI;
2. atualizar somente a fonte comprometida;
3. publicar os dados do gameplay sem renderizar;
4. reativar a fotografia do observador humano;
5. republicar a visibilidade dos alvos;
6. renderizar memória e overlay apenas para o slot de apresentação.

`RenderFogOverlayFromRuntimeCache` também rejeita defensivamente um cache cujo observador não corresponda ao `PlayerSlotId` de apresentação.

## Runtime quente por slot

Foi introduzida uma fotografia transitória de contribuições por slot contendo:

- contribuições geográficas e sensoriais por fonte;
- agregados geográficos e sensoriais;
- chaves incrementais das unidades.

Ela permite alternar entre gameplay da AI e apresentação humana sem recolher todas as fontes a cada movimento.

O fallback conservador continua sendo o full refresh quando uma fotografia necessária não existe.

## Resultado do movimento incremental

Antes, um movimento podia produzir:

```text
FoW AI: aproximadamente 5,2 s
FoW humano: aproximadamente 3,4 s
frame de confirmação: aproximadamente 8,8 s
```

Depois da fotografia por slot:

```text
[FoW][Perf][Incremental] ... splitPresentation=True
```

Os commits observados ficaram geralmente entre 1,1 e 1,6 segundo, sem 36 + 25 misses por movimento.

Ainda resta aproximadamente:

- 115–465 ms para atualizar/coletar a fonte movida;
- 540–590 ms em visibilidade;
- custo adicional para republicar a apresentação humana.

## Medição do turno completo

O turno vermelho registrou:

```text
TURNO TOTAL=398483 ms
Stage2=257710 ms
```

Breakdown medido da Stage2:

| Componente | Tempo |
|---|---:|
| Decisões | 48279 ms |
| Execução | 123857 ms |
| Snapshot | 1 ms |
| Delays | 60868 ms |
| Total atribuído às unidades | 233005 ms |

## Stalls externos ou não classificados

O wall time contém pausas que não se comportam como computação normal:

- frame de 76042 ms antes da execução;
- delays configurados para cerca de 500 ms medidos como 15853 ms e 27718 ms;
- frame de 52216 ms em `UnitSelected`;
- memória e GC praticamente estáveis durante esses intervalos.

Esses tempos precisam ser separados de CPU ativa, perda de foco, pausa do Editor, breakpoint ou espera de frame antes de orientar otimizações.

## Gargalos confirmados

### Fog of War

- aproximadamente 1,2 s por commit incremental, inclusive em várias ações `wait`;
- full refresh de aproximadamente 8,5 s antes das compras;
- full refresh em captura parcial sem troca de proprietário;
- full refresh ao retornar ao jogador humano.

### Planejamento

- `BuildObjectivePlan`: aproximadamente 2,7 s;
- unidades de suprimentos: decisões de aproximadamente 16–17,5 s;
- logística naval: aproximadamente 2,4 s;
- chamadas repetidas de `routeDistance` e `LogisticsService`.

### Execução e apresentação

- visibilidade global ainda custa aproximadamente 550 ms por ação;
- a apresentação humana é republicada integralmente;
- ações sem mudança geográfica ainda percorrem o pipeline de FoW.

## Plano de refactor

### 1. Barreira DataOnly/Presentation

- impedir qualquer escrita visual pelo slot executor adversário;
- validar memória, overlay, estruturas lembradas e contatos;
- criar testes de não vazamento.

### 2. Contexto explícito

Substituir inferências e trocas temporárias de estado por um contexto contendo:

```text
gameplaySlot
presentationSlot
publishGameplayData
publishVisuals
recordExplorationMemory
recordIntel
```

### 3. Persistência de todos os slots

Evoluir o save para `fogSourceCachesByObserverSlot`, com validação e fallback independentes por slot.

### 4. Início de turno incremental

Reativar fotografias quentes e aplicar apenas deltas de upkeep, mortes, spawns, embarque e propriedade.

### 5. `CommittedBoardDelta`

Classificar explicitamente:

- fontes movidas, adicionadas ou removidas;
- mudanças reais de propriedade;
- visibilidade de alvos afetada;
- visão de construções alterada;
- apresentação suja.

Captura parcial sem troca de dono não deve provocar full refresh.

### 6. Visibilidade por alvos afetados

Evitar republicar todas as unidades quando somente uma fonte ou um alvo mudou.

### 7. Planejamento e medição

- separar CPU ativa de wall time;
- registrar foco/pausa;
- cachear rotas;
- reduzir avaliações repetidas da logística.

## Preparação para multiplayer

`docs/ideias_futuras_multiplayer.md` foi atualizado para registrar:

- batches como intenção, não resultado imposto;
- executor transacional independente da origem;
- autoridade por `actorSlotId`;
- apresentação e conhecimento por `PlayerSlotId`;
- hashes de base e resultado;
- sequência e idempotência;
- quarentena de desync;
- risco de o próprio log integral furar o FoW.

## Alterações paralelas incluídas

Conforme o `git add .` solicitado, este marco também inclui o estado corrente de:

- ajustes do preset `AIPreset_Gastadora`;
- áudio de abertura/fechamento do Jornal no cursor;
- exportação do tutorial ativo para CSV;
- assets SDF modificados pelo Unity.

## Contrato transacional

- batches somente comprometem efeitos pelo executor comum;
- posição provisória nunca alimenta memória ou cache confirmado;
- o commit termina em `Neutral`;
- gameplay DataOnly não possui autoridade visual;
- o observador humano só recebe memória derivada da própria visão confirmada;
- falhas de cache permanecem recuperáveis por full refresh.

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- Logs reais confirmaram o caminho incremental com `splitPresentation=True`.
- O marco preserva as medições anteriores ao refactor planejado.
