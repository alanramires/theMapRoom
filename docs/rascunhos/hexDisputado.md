# Hex Disputado: estado atual e plano de evolucao

Documento tecnico para o estado atual do projeto em `D:\Unity Projects\The Map Room`.

Convencoes:
- "Confirmado": observado diretamente no codigo.
- "Inferencia": proposta arquitetural para o refactor.

---

## Status de implementacao (atualizado)

### B.1 `OccupancyResolver` central
Confirmado no codigo:
- Arquivo criado: `Assets/Scripts/Units/Rules/OccupancyResolver.cs`.
- Estruturas adicionadas:
- `HeightBand` com bandas `Air`, `Sub`, `Blocking`.
- `LayerOccupancyKey` com `Cell`, `Domain`, `HeightBand`.
- API minima adicionada:
- `GetHeightBand(UnitManager unit)`
- `IsBlockingLayer(UnitManager unit)`
- `CanPassThrough(UnitManager mover, UnitManager blocker, Vector3Int cell)`
- `CanEndMove(UnitManager mover, Vector3Int cell, IEnumerable<UnitManager> occupants)`
- `CanEnter(UnitManager unit, Vector3Int cell, IEnumerable<UnitManager> occupants)`
- Feature flag adicionada no proprio resolver:
- `OccupancyResolver.EnableLayerOccupancyResolver` (default `false`).

Estado de rollout:
- Nenhum callsite existente foi migrado ainda para `OccupancyResolver`.
- Comportamento runtime atual permanece inalterado (modo compat), conforme solicitado para a etapa B.1.

---

## A) Mapeamento objetivo (A.1..A.7)

### A.1) Validacao de destino final
Confirmado:
- O confirm de movimento valida destino com duas regras:
1. Regra Total War por time (bloqueia terminar no mesmo hex com unidade do mesmo time).
2. Regra geral de ocupacao via lookup de unidade no hex (mensagem "Hex ocupado").

Leitura de modelo:
- Assumicao global: presente no lookup geral de ocupacao no confirm.
- Preparacao por camada: parcial (path ja usa camada, confirm final ainda mistura com lookup global).

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:356`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:364`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:380`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Hex.cs:112`

### A.2) Bloqueio de pathfinding
Confirmado:
- O BFS consulta ocupante e usa `CanPassThrough`.
- `CanPassThrough` ja considera dominio/altura:
- Camada diferente: passa.
- Mesma camada + Total War: passa.
- Mesma camada + sem Total War: inimigo bloqueia.

Leitura de modelo:
- Assumicao global: baixa no path.
- Preparacao por camada: alta (base principal ja existe).

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\Rules\UnitMovementPathRules.cs:88`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\Rules\UnitMovementPathRules.cs:91`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitRulesDefinition.cs:24`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitRulesDefinition.cs:36`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitRulesDefinition.cs:40`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitRulesDefinition.cs:48`

### A.3) Deteccao de hex disputado
Confirmado:
- "Hex disputado" e detectado quando existe inimigo na mesma celula da unidade selecionada.
- So roda com Total War ativo.

Leitura de modelo:
- Assumicao global: alta (usa celula+time, sem filtro de camada).
- Preparacao por camada: baixa.

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:189`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:206`

### A.4) Sensores/acoes bloqueadas por hex disputado
Confirmado:
- Em disputado, bloqueia: captura, fusao, embarque, desembarque, suprir, transferir.
- Tambem remove mirar em `MoveuAndando`.

Leitura de modelo:
- Assumicao global: alta (usa flag disputado global).
- Preparacao por camada: baixa.

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:152`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:156`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:215`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:219`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:242`

### A.5) Interacao com construcoes (captura, compra, deploy, embark/disembark)
Confirmado:
- Captura:
- Sensor exige unidade no hex da construcao.
- Em hex disputado, captura pode ser bloqueada antes no filtro de sensores.
- Compra/deploy:
- Shopping tenta spawn no hex da construcao.
- Spawner bloqueia por ocupacao global (modo normal) ou por time (Total War).
- Embarque/Desembarque:
- Sensores validam por ocupacao/celula.
- Desembarque bloqueia hex alvo ja ocupado.

Leitura de modelo:
- Captura: gate global por disputado.
- Spawn/deploy: parcialmente preparado para Total War por time, mas nao por camada.
- Embark/disembark: majoritariamente por ocupacao de celula.

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Sensors\PodeCapturarSensor.cs:82`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.ConstructionShopping.cs:139`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitSpawner.cs:408`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitSpawner.cs:415`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Sensors\PodeEmbarcarSensor.cs:38`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Sensors\PodeDesembarcarSensor.cs:277`

### A.6) UI/preview de caminho e highlight
Confirmado:
- Range/preview vem do path valido.
- Cursor em `UnitSelected` navega no `paintedRangeLookup`.
- Caminho e preview visual vem de `PathManager`.

Leitura de modelo:
- Herda regra do path (que ja tem camada em `CanPassThrough`).
- Ainda pode divergir do confirm final em casos de ocupacao final.

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Range.cs:135`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Hex.cs:87`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.PathVisual.cs:7`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\Path\PathManager.cs:43`

### A.7) Resolucao no confirm do movimento
Confirmado:
- Fluxo: valida destino -> executa animacao -> entra em `MoveuAndando`/`MoveuParado` -> recalcula sensores.
- Se destino falhar por ocupacao, aborta antes de commit.

Leitura de modelo:
- Commit de path herda regra por camada do path.
- Validacao final ainda precisa consolidacao com regra por camada para evitar mismatch preview x confirm.

Referencias:
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:356`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:413`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Movement.cs:25`

---

## B) Ordem pratica de refactor para "Total War por camada"

Modelo alvo:
- Camadas bloqueantes: `land/surface` e `naval/surface`.
- Camadas nao bloqueantes: `air/*` e `sub/submerged`.

### B.1) Extracao de conceito: `LayerOccupancy`
- Criar estrutura central: `LayerOccupancyKey(cell, domain, heightBand)`.
- Normalizar bandas:
- `air/*` => banda `air`
- `sub/submerged` => banda `sub`
- `land/surface` e `naval/surface` => bandas bloqueantes
- Criar API minima:
- `CanPassThrough(mover, blocker, cell)`
- `CanEndMove(mover, cell)`
- `CanEnter(unit, cell, reason)`

Status:
- Implementado no codigo em `Assets/Scripts/Units/Rules/OccupancyResolver.cs`.
- Sem migracao de callsites nesta etapa (feature flag default `false`).

### B.2) Refactor de `CanEnter / CanPassThrough / CanEndMove`
- Migrar regra atual de `UnitRulesDefinition.CanPassThrough` para resolver central.
- Adicionar `CanEndMove` (destino final) separado de travessia.
- Adicionar `CanEnter` para spawn/deploy/desembarque.

### B.3) Refactor de pathfinding
- Em `UnitMovementPathRules`, usar so `CanPassThrough` do resolver.
- Na geracao de destinos pintados, filtrar por `CanEndMove`.
- Objetivo: remover diferenca preview x confirm.

### B.4) Refactor de hex disputado
- Trocar detector atual por detector "disputado na mesma camada bloqueante".
- Basear disputa no layer da unidade selecionada e no tipo de acao.

### B.5) Refactor de sensores
- Aplicar disputed-layer-aware para:
- captura
- merge
- supply/transfer
- embark/disembark
- manter regras de arma/LoS para ataque, sem bloquear por ocupacao de outra camada.

### B.6) Refactor de construcao
- Shopping/deploy/spawn usar `CanEnter` por camada.
- Garantir acesso a construcao quando ocupante for `air/*` ou `sub/*` em camada nao bloqueante relevante para a operacao.

### B.7) Testes de compatibilidade com Total War atual
- Matriz de regressao:
- Sem Total War (inimigo bloqueia e desvia)
- Total War atual
- Novo modo por camada
- Validar:
- save/load
- preview x confirm
- sensores
- replay

---

## C) Comparacao de arquitetura

### C.1) Patch pontual nos pontos criticos
Vantagem:
- Entrega rapida.

Risco:
- Regras duplicadas e inconsistentes (path, confirm, spawner, sensores).

Impacto no codigo:
- Medio/alto e espalhado.

Chance de regressao:
- Alta.

Recomendacao:
- So para hotfix curto. Nao como arquitetura final.

### C.2) `OccupancyResolver` central
Vantagem:
- Regra unica para travessia, destino final e entrada.
- Facil de testar por contrato.

Risco:
- Migracao inicial exige disciplina de rollout.

Impacto no codigo:
- Medio (troca gradual de callsites).

Chance de regressao:
- Media para baixa, com rollout por etapas.

Recomendacao:
- Melhor opcao para evoluir sem quebrar o comportamento existente.

### C.3) Matriz por hex com slots por layer
Vantagem:
- Modelo explicito e escalavel para longo prazo.

Risco:
- Mudanca estrutural grande (persistencia, queries, debug tooling).

Impacto no codigo:
- Alto.

Chance de regressao:
- Media/alta no curto prazo, baixa no longo apos estabilizar.

Recomendacao:
- Boa evolucao apos C.2 estabilizado.

Recomendacao final:
- Implementar C.2 agora.
- Evoluir para C.3 so se aparecer complexidade recorrente (novas camadas, regras por modo, IA path layer-aware pesada).

---

## D) Regra formal por operacao no modelo desejado

Colunas:
- SCB: mesma camada bloqueante
- SCNB: mesma camada nao bloqueante
- CCB: camada diferente onde existe camada bloqueante no hex
- CCNB: camada diferente nao bloqueante

| Operacao | SCB | SCNB | CCB | CCNB |
|---|---|---|---|---|
| D.1 Passar pelo hex | Permitir para aliado; inimigo depende do modo Total War por camada | Permitir | Permitir | Permitir |
| D.2 Terminar movimento | Bloquear se slot da camada ja ocupado por unidade do mesmo time; regra de inimigo por modo | Permitir | Permitir | Permitir |
| D.3 Capturar construcao | Bloquear so se houver conflito relevante na camada da acao de captura | Permitir | Permitir | Permitir |
| D.4 Comprar/spawnar em construcao | Usar `CanEnter` na camada de spawn; bloquear se slot bloqueante ocupado | Permitir | Permitir | Permitir |
| D.5 Reabastecer/transferir/merge | Exigir compat de camada e alcance; bloquear so em conflito de camada relevante | Permitir | Permitir se alvo valido | Permitir se alvo valido |
| D.6 Embarcar/desembarcar | Validar transportador/destino por `CanEnter` e camada | Permitir | Permitir | Permitir |
| D.7 Atacar | Regra de arma/LoS domina; ocupacao de outra camada nao bloqueia por si | Idem | Idem | Idem |
| D.8 Detectar/revelar sensor | Sensores consultam camada alvo; disputa apenas quando camada relevante conflita | Permitir | Permitir | Permitir |

Notas:
- "Regra de inimigo por modo" em SCB pode manter compatibilidade com Total War atual via feature flag.
- Inferencia: separar formalmente `CanPassThrough`, `CanEndMove` e `CanEnter` reduz regressao.

---

## E) Como implementar o terceiro comportamento sem quebrar save, preview e sensores

Estado atual:
- Sem Total War: inimigo bloqueia e forca desvio.
- Com Total War: path passa por inimigo e validacao final acontece no confirm.

Objetivo novo:
- Inimigo bloqueia apenas se estiver na mesma camada bloqueante.
- Outras camadas coexistem normalmente.
- Construcoes continuam acessiveis quando ocupante for `air/*` ou `sub/*`.

Plano seguro:
1. Introduzir `OccupancyResolver` sem trocar comportamento (modo compat).
2. Mover path para `CanPassThrough` central.
3. Mover validacao de destino para `CanEndMove` central.
4. Mover spawn/deploy/desembarque para `CanEnter` central.
5. Migrar detector de hex disputado para versao por camada.
6. Migrar sensores para consultar disputa por camada.
7. Ativar por feature flags:
- `EnableLayerOccupancyResolver`
- `EnableLayerAwareContestedHex`
- `EnableLayerAwareEndMoveValidation`
8. Rodar regressao em:
- save/load
- preview vs confirm
- sensores
- replay

Compatibilidade de save:
- No curto prazo, derivar layer occupancy em runtime por `GetDomain()/GetHeightLevel()`.
- So versionar save depois de estabilizar comportamento.

Criterios de aceite:
- Nenhuma celula pintada invalida no confirm por regra de ocupacao.
- Construcoes acessiveis conforme regra de camada.
- Sem regressao em captura, compra/deploy, supply/transfer, merge, embark/disembark.

---

## Referencias centrais
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitRulesDefinition.cs:24`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\Rules\OccupancyResolver.cs:1`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\Rules\UnitMovementPathRules.cs:88`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\Rules\UnitOccupancyRules.cs:44`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.StateMachine.cs:356`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Sensors.cs:152`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.Range.cs:135`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Units\UnitSpawner.cs:402`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Match\TurnState\TurnStateManager.ConstructionShopping.cs:139`
- `D:\Unity Projects\The Map Room\Assets\Scripts\Sensors\PodeDesembarcarSensor.cs:277`
