# v4.3.7 - Refactor de FOW para AI concluído

Esta versão conclui o refactor de Fog of War para turnos executados por AI,
jogador remoto, replay ou qualquer outra origem de batches.

O trabalho foi dividido em sete etapas para preservar o contrato transacional,
manter compatibilidade com saves anteriores e permitir validação incremental.

## Resultado arquitetural

A identidade de autoridade de FoW, memória, inteligência e planejamento é
`PlayerSlotId`.

`TeamId` permanece como identidade visual de cor/facção e pode ser derivado do
slot quando necessário. Ele não seleciona cache, snapshot, ledger ou plano.

Isso permite que dois slots compartilhem a mesma cor sem compartilhar:

- visão atual;
- memória de exploração;
- detecção;
- contatos de inteligência;
- contribuições de unidades e construções;
- planejamento da AI.

## Etapa 1 — canais de atualização

Foi estabelecida a separação entre:

- gameplay confirmado em `DataOnly`;
- apresentação local em `FullVisual`.

Um observador que calcula dados não recebe automaticamente autoridade para
escrever overlay, memória visual ou visibilidade da perspectiva local.

## Etapa 2 — contexto explícito

As atualizações passaram a usar `FogUpdateContext`, contendo:

- slot executor;
- slot observador;
- slot de apresentação;
- permissões de gameplay;
- permissões visuais;
- gravação de memória;
- gravação de inteligência.

Isso remove a dependência implícita do time ativo durante cálculos de outros
observadores.

## Etapa 3 — persistência por slot

O save v18 passou a persistir fotografias de contribuições por
`PlayerSlotId`.

Cada bloco é restaurado e validado independentemente. A falha de um slot não
descarta os caches válidos dos demais. Saves v17 continuam compatíveis por
migração em memória.

## Etapa 4 — início de turno incremental

No início do turno, o controlador tenta reativar a fotografia quente do slot.

Unidades cujo estado de visão não mudou preservam suas células e apenas
rebaseiam a chave de revisão. Unidades novas, alteradas, removidas ou embarcadas
são reconciliadas. Construções são reconstruídas como subconjunto barato.

O full refresh permanece como fallback.

## Etapa 5 — `CommittedBoardDelta`

Alterações confirmadas do tabuleiro passaram a ser representadas por um
envelope acumulável.

O delta registra:

- tipos de alteração;
- unidades envolvidas;
- células confirmadas;
- exigência de reconciliação;
- exigência excepcional de full refresh.

O envelope somente é consumido depois do retorno a `CursorState.Neutral`.

## Etapa 6 — alvos afetados

O caminho incremental calcula a união da cobertura geográfica e sensorial antes
e depois da mudança, acrescida das células confirmadas do delta.

Somente unidades dentro desse conjunto são reavaliadas em:

- snapshot de gameplay;
- apresentação runtime;
- persistência stealth;
- `AIIntelLedger`.

Executor e observador visual usam conjuntos separados quando pertencem a slots
diferentes.

## Etapa 7 — barreira de planejamento

Os commits da AI deixaram de solicitar full refresh como forma de sincronizar
o mundo.

Antes do planejamento, eles usam:

```text
EnsureConfirmedFogGameplaySnapshotForSlot(PlayerSlotId)
```

A barreira:

- exige `Neutral`;
- opera em `DataOnly`;
- ativa e reconcilia o cache quente do slot;
- republica seu snapshot confirmado;
- usa full fallback somente quando necessário;
- restaura o proprietário anterior do runtime visual.

`RunAITurn`, commits leves e commits pesados agora recebem o slot como
identidade principal. A busca de inimigos próximos usada pelo commit também foi
corrigida para comparar slots diretamente.

## Contrato transacional

Durante todo o refactor foi preservada a regra:

> Nada no jogo é definitivo até o jogador comprometer a ação.

Consequentemente:

- movimento provisório não revela terreno;
- animações não publicam memória;
- mortes apresentadas em fila aguardam `Neutral`;
- deltas comprometidos podem ser acumulados, mas não consumidos antes da
  fronteira confirmada;
- AI, humano, jogador remoto e replay obedecem ao mesmo contrato.

## Diagnóstico

Os principais logs introduzidos ou ampliados são:

```text
[FoW][LoadCacheRestore]
[FoW][TurnStartCache]
[FoW][CommittedDelta]
[FoW][AffectedTargets]
[FoW][AffectedTargets][Visual]
[FoW][PlanningBarrier]
[AI Commit Light]
[AI Commit Heavy]
```

Eles permitem separar:

- cache restaurado e cold fallback;
- fontes alteradas e inalteradas;
- células recolhidas;
- alvos efetivamente avaliados;
- tempo da barreira de planejamento;
- rebuild de setores;
- construção do snapshot;
- planejamento, execução e espera.

## Comportamento esperado

Em um save quente:

- o início do turno da AI deve mostrar `TurnStartCache activated=true`;
- a maioria das unidades deve aparecer como `units.unchanged`;
- a barreira de planejamento deve informar `result=reused`;
- `CollectVisibleCells` deve ocorrer apenas para fontes alteradas;
- a perspectiva humana não deve receber memória da AI;
- um full refresh deve aparecer somente como fallback justificável.

Em um save antigo ou sem cache:

- o primeiro acesso pode executar cold fallback;
- o cache é reconstruído por slot;
- o próximo save persiste as fotografias;
- os acessos seguintes devem usar o caminho quente.

## Documentação

O contrato consolidado está em:

`docs/arquitetura/fow_canais_visibilidade.md`

O contrato geral de ações confirmadas permanece em:

`docs/arquitetura/acoes_transacionais.md`

## Validação

- `Assembly-CSharp.csproj` compilado com zero erros e zero warnings.
- `git diff --check` concluído sem erros.
- A identidade operacional do fluxo final é `PlayerSlotId`.
- Full refresh permanece disponível como fallback conservador.
- Nenhuma publicação definitiva foi antecipada para estados provisórios.
