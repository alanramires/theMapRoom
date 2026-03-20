# Macro Turno

Documento canonico do fluxo macro de turno/partida (camada `MatchController`).

## Escopo

- `MatchController`: turno ativo, ordem de times, economia, FoW, transicoes.
- `TurnStateManager`: microfluxo da unidade dentro do turno ativo.
- `ReplayManager`: gravacao/reproducao da timeline por snapshots e batches.

## Modelo macro

Estado principal:
- `currentTurn`
- `activeTeamId`
- lista de jogadores (`players`) com `startMoney`, `actualMoney`, `incomePerTurn`
- opcional de time neutro (`includeNeutralTeam`)

Orquestrador principal:
- `AdvanceTurn()` e `AdvanceTurnWithTransition()`

## Inicio de partida e inicializacao

No bootstrap da cena:
1. aplica preset de jogo (`GameSetupPreset`)
2. resolve time ativo inicial
3. aplica efeitos visuais/equipe
4. prepara FoW e sistemas de apoio

Para replay/gravacao:
- o `snapshot#0` deve representar o jogo ja liberado para o jogador (estado neutro pronto).
- em load, snapshot inicial ja salvo deve ser reaproveitado quando compativel com turno/time atual.

## Troca de turno (alto nivel)

Quando o turno avanca:
1. emite `OnBeforeAdvanceTurn`
2. escolhe proximo time valido (incluindo neutro, se habilitado e com unidades)
3. aplica efeitos de inicio de turno do time ativo:
- upkeep/autonomia
- economia (credito de renda)
- refresh de estados de unidade e sistemas ligados ao turno
4. atualiza musica, cursor, FoW e UI
5. emite eventos de sincronizacao (`OnActiveTeamChanged`, etc)

## Economia por turno

Fluxo macro:
1. calcula renda por construcoes controladas
2. atualiza `incomePerTurn` por time
3. credita `actualMoney` no inicio do turno do time
4. gastos de compra/servico deduzem do caixa atual

## FoW no macro

`MatchController` coordena atualizacao de visibilidade:
- recalculo de contribuicao por unidade
- visao de construcoes amigas quando aplicavel
- refresh em troca de turno e eventos relevantes de unidade

## Relacao com o microfluxo (`TurnStateManager`)

- Macro decide "de quem e o turno" e aplica efeitos globais.
- Micro decide "o que a unidade faz agora" (selecionar, mover, sensor, confirmar).
- Encerrar turno eh responsabilidade macro; resolver acao individual eh micro.

## Pontos de extensao/importantes

- `SetActiveTeamIdWithoutTurnStart(...)`: troca team sem aplicar efeitos macro completos (uso controlado/debug).
- `OnActiveTeamChanged`/`OnFogOfWarUpdated`: pontos de acoplamento para UI e sistemas auxiliares.
- Times neutros podem entrar no ciclo se configurados e houver unidade neutra em campo.

## Referencias

- `Assets/Scripts/Match/MatchController.cs`
- `docs/turnState.md`
- `docs/FOW.md`
- `docs/analises/06_relatorio_economia.md`
