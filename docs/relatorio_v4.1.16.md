# Fow Partial, AI vs AI vs AI vs AI

Versao: v4.1.16-m
Status: em validacao no Unity

## Resumo

- Habilitada a IA em mapas **sem setores de campo e sem rally** (tabuleiro só de QGs), o caso base
  que antes deixava o planner inteiro desligado. Motivador: mapa de teste simétrico de 4 cantos
  (AI vs AI vs AI vs AI).
- Princípio recorrente aplicado em vários pontos: **todo gate precisa distinguir "ainda não
  satisfeito" de "impossível neste mapa"** — o segundo caso degrada, não bloqueia.
- Início da migração dos tunables da IA para um asset (`AIPresetData`), fase 1 (inerte).
- Correções de crash, travamento de Editor e regressão de performance descobertos no teste 4-IAs.

## Planner — caso base (sem rally / sem setor)

- **Gate de invasão inaplicável** (`AIController.PlanEvaluator`): a invasão da base inimiga exigia
  um rally com massa montada (GoGreen). Sem rally no mapa, o objetivo era criado e dissolvido todo
  turno e o plano ficava vazio. Novo `MapHasAnyRallyPointForSlot(slot)` libera a invasão quando
  **não há rally designado para o slot** — escopado por slot (rally é designado via
  `RallyOwnerSlotIndex`), com falha conservadora (slot não resolvido = gate mantido).
- **Leque de eixos com base inimiga como ápice** (`InvasionAxisMap.BuildEnemyBaseFan`): sem rally do
  slot, os QGs inimigos viram os ápices do leque (um eixo por inimigo). O rally deixa de ser
  pré-condição e vira refinamento. Escopo conservador: só quando não há rally do slot, para não
  mexer nos corredores dos mapas já calibrados. Eixos marcados `IsInvasionAxis` (a profundidade de
  base depende disso na pressão de transporte).
- **Desempate determinístico** do alvo de invasão (`FindNearestEnemyHq`): empate de distância era
  resolvido por ordem de cena (não reproduzível). Agora resolve pelo menor `SlotIndex`.

## Shopping — composição e núcleo

- **Componente de núcleo insatisfazível sai da conta** (`ResolveOperationalCoreTargets`): exigir
  artilharia num mapa que não vende artilharia travava o elite para sempre. Ponto único usado pelo
  gate (`HasOperationalCore`) e pelo gradiente (`ComputeOperationalCoreMaturity`), via
  `ResolveCompositionRole` (mesmo predicado da contagem). Fura o mesmo catch-22 que o bootstrap do
  MBT no Hard já contornava.
- **Slots de fogo indireto da invasão condicionais à produção** (`AIController.PlanEvaluator`): a
  invasão só demanda `FogoIndireto` se o mapa vende artilharia (`CanProduceCompositionRole`). Sem
  produtor: cap + assalto; com produtor: cap + assalto + art. Mata a demanda-fantasma de obus.
- **Toggle `softCoreGate`** (`AIController`, espelhado em `AIPresetData.capacidades.gateNucleoSuave`,
  default off): a maturidade do núcleo (0..1) vira peso contínuo no score do elite em vez de muro
  tudo-ou-nada. Corrige o incentivo perverso do gate duro, que ao banir o elite E desligar a
  penalidade anti-barato empurrava a compra da pior artilharia só para "pagar o imposto". O piso de
  caixa continua duro (poder de compra ≠ doutrina).

## Migração para AIPresetData (fase 1, inerte)

- `AIPresetData` (ScriptableObject) — **um baseline único** editável como um `UnitData`: seções de
  capacidades (toggles), economia, composição, conscrição, plano, tática, intel e aeronáutica. A
  dificuldade é uma **overlay de código** (`ApplyDifficultyOverlay`) por cima de uma cópia de
  runtime — não há asset por dificuldade.
- `AICapabilityPreset` quebra o `hardMode` (que acendia 6 capacidades em bloco) em toggles nomeados.
- Gerador de Editor `Tools > AI > Gerar baseline a partir da cena` (lê os valores vivos da cena via
  `SerializedObject`; inclui auditoria que prova a divergência de valores entre mapas).
- Nenhuma decisão lê do preset ainda — os 4 booleanos legados seguem sendo a fonte de verdade.
  Comportamento inalterado, de propósito.

## Correções de estabilidade e performance (descobertas no teste 4-IAs)

- **Crash "Collection was modified"** (`AIController.PlanEvaluator`): `GetAllSectorInfos`/
  `GetAllBaseInfos` retornam as listas internas VIVAS do `SectorManager`; criar objetivos dentro dos
  loops podia disparar um rebuild que as modificava. Cópia defensiva das duas listas antes dos loops.
  O gate de invasão apenas expôs a bomba-relógio latente ao tornar o corpo do loop alcançável.
- **Travamento do Editor com o SectorManager selecionado** (`SectorManagerEditor`): `DrawEixoInfos`
  reconstruía o `InvasionAxisMap` de cada time a cada repaint do inspector (`FindObjectsByType` +
  possível rebuild de setor), virando tempestade de repaint junto com o `QueuePlayerLoopUpdate` do
  `OnValidate`. Agora atrás de um foldout "Eixo Infos" **fechado por padrão** (colapsado = zero build).
- **Regressão de performance no shopping** (`AIShoppingPlanner`): os alvos do núcleo passaram a
  varrer produtoras×ofertas por candidato dentro da busca em feixe (largura 1024). Memoizados por
  referência de snapshot (invariantes durante o turno).
- **Blindagem do `MainMenuStateController`** contra cópia de cena: `ResolveReferences` varria a cena
  com `FindObjectsByType` todo frame quando o menu não existe (cena de batalha). Contador de
  tentativas desativa o componente após ~30 frames sem resolver. (Latente; não era a causa do
  travamento do Quadrado, mas é a mesma classe de recaída registrada em `project_phase2_perf`.)

## Pendencias

- Desbloqueio do GoGreen com "mar de tanque sem canhão": os slots de invasão foram limpos, mas o
  gate real é a prontidão do rally (`overwhelmingBreakthrough` exige hold-package com ≥1 capturador
  e `breakthrough = Assalto + Aéreo`; capturador-agressivo resolve como Capturador e não conta como
  assalto). Precisa do log `[AI Rally] ... missing=...` de um turno com massa parada para atacar o
  suspeito certo.
- Gargalo de produção única: 1 compra por turno por produtora — esperado, mas magro no caso base.
- Fase 2 da migração para `AIPresetData`: getters lendo do preset com fallback para o campo da cena.
- Gatilho do rebuild de setor dentro do `BuildObjectivePlan` (investigar só se o planning inflar em
  partidas longas).
