# Relatorio Tecnico - v1.0.14

## Versoes cobertas

- `v1.0.13` - commit `ecd3358` - `v1.0.13 captura e reconquista`
- `v1.0.14` - commit `a50c0d4` - `v1.0.14 - fusão de unidades`
- Branch: `main`

Este relatorio consolida a entrega do fluxo de fusao de unidades no gameplay e no tooling de sensores, incluindo fila de fusao, animacoes de preview/execucao, regras de camada/dominio e ajustes de qualidade de vida para depuracao.

---

## 1) Sensor de fusao no gameplay (acao "F")

### Objetivo

Adicionar `Pode Fundir` como sensor real do turno, habilitando a acao por atalho e integrando ao painel de scanners.

### O que entrou

1. Sensor `PodeFundirSensor` com regra base:
- unidade selecionada valida
- nao embarcada
- ao menos 1 adjacente (1 hex), mesmo tipo e mesmo time
2. Integracao do codigo `F` no pipeline de sensores do `TurnStateManager`.
3. Exibicao de status no painel/log de scanners:
- disponivel/indisponivel
- quantidade de elegiveis adjacentes
- motivo quando indisponivel

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeFundirSensor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`

---

## 2) Estado dedicado de fusao no TurnState

### Objetivo

Criar o fluxo completo de prompt de fusao no jogo, seguindo padrao de subetapas e fila de ordens.

### O que entrou

1. Estado dedicado `CursorState.Fundindo`.
2. Subetapas de scanner para fusao:
- selecao de candidato (numero/cursor)
- confirmacao
3. Comportamentos de UX do fluxo:
- auto-select quando so ha 1 candidato (com regras para evitar loop)
- opcao `0` para executar ordem parcial quando ja existe fila
- `ESC` para desfazer ultimo item da fila ou voltar
4. Navegacao por lista de candidatos com cursor ciclando entre entradas.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Merge.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.StateMachine.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Hex.cs`

---

## 3) Fila de fusao e execucao sequencial

### Objetivo

Executar a fusao como ordem encadeada de participantes para um recebedor, com previsao e resultado consistentes.

### O que entrou

1. Fila de participantes de fusao no runtime (`mergeQueuedUnits`).
2. Execucao sequencial em coroutine:
- cursor salta entre participantes
- participante move ate o recebedor
- `load` ao concluir participante
- participante e removido (fluxo gameplay)
3. Atualizacao da unidade resultante:
- HP com teto em 10
- autonomia por contribuicao em passos (`hp * autonomia`)
- ammo de armas embarcadas com consolidacao por tipo de arma
4. Finalizacao de acao:
- unidade resultante marcada como `hasActed`
- encerramento padrao de turno/selecionado

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Merge.cs`

---

## 4) Plano de camada/dominio na fusao (AR/SUB)

### Objetivo

Sincronizar comportamento de camada entre sensor e gameplay para fusao entre unidades em dominios diferentes.

### O que entrou

1. Resolucao de plano de camada para a fusao:
- `Default Fusion (Same domain)`
- `Air / Low`
- `Naval / Surface`
- `Sub/Submerged`
2. Regras:
- se todos ja estao na mesma camada: fluxo default
- familia AR: common ground em `Air/AirLow`
- familia SUB: tenta `Submarine/Submerged`; se algum falhar validacao de terreno, fallback para `Naval/Surface`
3. Validacao SUB por contexto de celula:
- prioridade `Construction > Structure + Terrain > Terrain`
4. Timing visual na execucao:
- recebedor troca primeiro para camada da fusao (se necessario), sem concluir acao
- cada participante troca durante o inicio da propria caminhada, mantendo animacao normal de merge

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Merge.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs` (reuso de validacao de camada por terreno)

---

## 5) Preview visual e parametros de animacao

### Objetivo

Entregar feedback visual de fila/confirmacao de fusao e expor knobs de tuning no `AnimationManager`.

### O que entrou

1. Linha tracejada animada de preview de fila de fusao.
2. Preview tambem na subetapa de confirmacao.
3. Uso do mesmo baseline visual do grupo de preview de mirando/spotter.
4. Parametros novos no `AnimationManager`:
- grupo `Merge Queue Preview Line`
- grupo `Merge Sequence Timing`
- campos para velocidade, espacamento, duracoes e delays da sequencia

### Arquivo-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 6) Tooling: Tools > Sensors > Pode Fundir

### Objetivo

Criar uma janela de debug completa para validar fusao fora de jogo.

### O que entrou

1. Nova janela `Tools > Sensors > Pode Fundir`.
2. Layout em duas colunas (listas vs relatorio).
3. Fila de fusao debug:
- adicionar/remover participantes
- elegiveis saem da lista ao entrar na fila e retornam ao remover
4. Lista de nao elegiveis com motivo.
5. Previa detalhada:
- unidade antes
- participantes
- unidade resultante
- contas de HP, autonomia e armas embarcadas
6. Botao `FUNDIR` debug (sem remover participantes, conforme fluxo de teste no tool).
7. `OBS` de camada e checks visuais:
- `Default Fusion (Same domain)`
- `Air / Low`
- `Naval / Surface`
- `Sub/Submerged`

### Arquivo-chave

- `Assets/Editor/PodeFundirSensorDebugWindow.cs`

---

## 7) Ajustes de UX e ferramentas auxiliares

### O que entrou

1. Camera:
- toggle de zoom por `M` com `defaultOrthoSize = 2`
- alternancia entre zoom distante e zoom confortavel
- reposicionamento para area do cursor ao alternar
- beep reutilizando SFX do `CursorController`
2. Scroll wheel nos sensores em `Tools` para telas extensas.
3. Ajustes de organizacao/menus nas janelas de sensores.

### Arquivos-chave

- `Assets/Scripts/Camera/CameraController.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Editor/PodeCapturarSensorDebugWindow.cs`
- `Assets/Editor/PodeDecolarWindow.cs`
- `Assets/Editor/PodeDesembarcarSensorDebugWindow.cs`
- `Assets/Editor/PodeEmbarcarSensorDebugWindow.cs`
- `Assets/Editor/PodeMirarSensorDebugWindow.cs`
- `Assets/Editor/PodePousarWindow.cs`

---

## 8) Asset complementar

### O que entrou

- Nova skill de suporte aos cenarios SUB:
  - `Assets/DB/Character/Skills/SUB Submerse Ops.asset`

---

## Resumo de volume (base `ecd3358` -> `a50c0d4`)

- `23 files changed`
- `3190 insertions`
- `21 deletions`

