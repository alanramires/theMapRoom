# Relatorio Tecnico - v1.0.15

## Versoes cobertas

- `v1.0.14` - commit `a50c0d4` - `v1.0.14 - fusao de unidades`
- `v1.0.15` - commit `04d813f` - `v1.0.15 - Logistica: Suprir Unidades`
- Branch: `main`

Este relatorio consolida a entrega do fluxo de logistica/suprimento no gameplay e no tooling, incluindo sensor `Pode Suprir`, fila de atendimento, regras de camada/dominio, consumo de supplies, animacoes e ajustes de UX.

---

## 1) Sensor de suprimento no gameplay (acao "S")

### Objetivo

Adicionar `Pode Suprir` como sensor real do turno, habilitando acao por atalho e integrando ao painel de scanners.

### O que entrou

1. Sensor `PodeSuprirSensor` com regra base:
- unidade selecionada valida e nao embarcada
- unidade precisa ser supplier (`isSupplier=true`)
- validacao de `supplierOperationDomains` na camada atual
- validacao de estoque embarcado e servicos operacionais
2. Integracao do codigo `S` no pipeline de sensores do `TurnStateManager`.
3. Exibicao de status no painel/log:
- disponivel/indisponivel
- quantidade de alvos validos
- motivo quando indisponivel

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- `Assets/Scripts/Sensors/PodeSuprirOption.cs`
- `Assets/Scripts/Sensors/PodeSuprirInvalidOption.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`

---

## 2) Estado dedicado de suprimento no TurnState

### Objetivo

Criar fluxo completo de prompt de suprimento no jogo, no mesmo padrao de subetapas e fila usado em fusao.

### O que entrou

1. Estado dedicado `CursorState.Suprindo`.
2. Subetapas do prompt:
- selecao de candidato (numero/cursor)
- confirmacao de entrada na fila
3. Comportamentos de UX:
- navegacao por candidatos sem reservar supplies antecipadamente
- `0` executa fila atual
- `ESC` desfaz ultimo item da fila ou retorna
- execucao automatica ao atingir limite `maxUnitsServedPerTurn`

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Supply.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.StateMachine.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Hex.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`

---

## 3) Regras de elegibilidade e paridade com o tool

### Objetivo

Alinhar runtime com o comportamento do `Tools > Logistica > Pode Suprir`.

### O que entrou

1. Candidato valido precisa de pelo menos 1 necessidade atendivel por servico ofertado.
2. Servicos `apenasEntreSupridores` respeitados.
3. Validacao de estoque por servico (`suppliesUsed`) sem consumo durante selecao.
4. Dominio/camada com transicoes previstas:
- `forceLandBeforeSupply`
- `forceTakeoffBeforeSupply`
- `forceSurfaceBeforeSupply`
- `plannedServiceDomain` e `plannedServiceHeight`
5. Filtro de alcance adjacente por vizinho de hex real (nao por distancia heuristica).
6. Travas adicionais de camada no hex atual (terreno/estrutura/construcao + skill):
- evita falso positivo de unidade que "pode pousar" mas nao na camada exigida pelo supplier no hex atual

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`
- `Assets/Scripts/Sensors/PodeSuprirOption.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`

---

## 4) Execucao de suprimento e aplicacao de servicos

### Objetivo

Executar fila de suprimento por alvo, aplicando apenas servicos efetivamente usados e consumindo recursos no momento correto.

### O que entrou

1. Execucao sequencial da fila em coroutine.
2. Aplicacao por servico com consumo real de supply no ato da execucao.
3. Recuperacao suportada:
- HP
- autonomia
- municao
4. Respeito a limites por servico (`serviceLimitPerUnitPerTurn`) e eficiencia por classe (`serviceEfficiency`).
5. `HasActed` por alvo ao concluir atendimento e `HasActed` do supplier ao fim da fila.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Supply.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`

---

## 5) Animacao de suprimento (sequencia completa)

### Objetivo

Entregar fluxo visual completo ao executar suprimento, com foco em leitura de estado e timing.

### O que entrou

1. Sequencia por alvo:
- supplier vai para camada combinada quando aplicavel
- cursor move para alvo antes do atendimento
- sprites de servico voam em linha reta supplier -> alvo
- aplicacao do servico ao chegar
- `load` no fim do alvo
2. Fuel fill gradual:
- quando reabastece, barra nao "salta", preenche gradualmente ate o valor final
3. Finalizacao:
- cursor retorna ao supplier
- espera final
- `done` no fim da acao do supplier
4. Preview lines:
- removidas no inicio da execucao
- bloqueadas durante animacao para nao reaparecer

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 6) Parametros novos no AnimationManager

### Objetivo

Expor tuning da animacao de suprimento no Inspector.

### O que entrou

1. Metodo novo de projetil de servico em linha reta:
- `PlayServiceProjectileStraight(...)`
2. Grupo de timing/FX de suprimento:
- `supplyCursorFocusDelay`
- `supplySpawnInterval`
- `supplyFlightPadding`
- `supplyPostTargetDelay`
- `supplySupplierFinalDelay`
- `supplyProjectileSpeed`
- `supplyProjectileMinDuration`
- `supplyProjectileScale`
3. Cursor com SFX em cada troca durante suprimento e delay curto para foco visual.

### Arquivo-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 7) VTOL/STOVL Landing FX no fluxo de suprimento

### Objetivo

Padronizar uso do FX de pouso VTOL/STOVL nos fluxos com transicao para `Land/Surface`.

### O que entrou

1. Fluxo de suprimento passou a disparar `PlayVtolLandingEffect(...)` quando houver pouso a partir de `Air`.
2. Remocao de dependencia hardcoded de `HasSkillId("vtol")` nos pontos de pouso.
3. Gating do FX centralizado na lista `vtolLandingFxAllowedSkills` do `AnimationManager`.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Disembark.cs`

---

## 8) Tooling: Tools > Logistica > Pode Suprir

### Objetivo

Adicionar ferramenta de depuracao completa do sensor/fluxo de suprimento no editor.

### O que entrou

1. Nova janela `Tools > Logistica > Pode Suprir`.
2. Lista de candidatos validos e invalidos com razoes.
3. Fila debug de atendimento com limite por supplier.
4. Relatorio detalhado de servicos usados, supplies consumidos e custos estimados.
5. Plano de camada da fila (`Default`, `Air/Low`, `Naval/Surface`) e observacoes.

### Arquivo-chave

- `Assets/Editor/PodeSuprirSensorDebugWindow.cs`

---

## 9) Dados e catalogo logistico

### O que entrou

1. Campos novos em `ServiceData` para suporte ao fluxo:
- `recuperaHp`
- `recuperaAutonomia`
- `recuperaMunicao`
- `apenasEntreSupridores`
2. Ajustes em `UnitData` e unidades do banco para servicos/estoque/camada de operacao.
3. Inclusao de unidade de suporte:
- `Exercito_Trem de Carga`
4. Documentacao auxiliar:
- `docs/supridores.md`
- atualizacoes em `docs/Sensores.md`

### Arquivos-chave

- `Assets/Scripts/Services/ServiceData.cs`
- `Assets/Scripts/Units/UnitData.cs`
- `Assets/DB/Character/Unit/*.asset`
- `Assets/DB/Logistic/Services/*.asset`

---

## Resumo de volume (base `a50c0d4` -> `04d813f`)

- `68 files changed`
- `33107 insertions`
- `12066 deletions`
