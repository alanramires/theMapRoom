# Relatorio Tecnico - v1.0.13

## Versoes cobertas

- `v1.0.12` - commit `8f5795b` - `desembarque concluido e minor fixes`
- `v1.0.13` - commit `ecd3358` - `v1.0.13 captura e reconquista`
- Branch: `main`

Este relatorio consolida a entrega do fluxo de captura/reconquista, incluindo recuperacao de base aliada, ajustes de HUD de construcao e especializacao do sensor/debug tool de captura.

---

## 1) Captura em gameplay (acao "C")

### Objetivo

Fechar um fluxo completo de captura no `TurnStateManager`, com estado dedicado, sequencia de SFX e finalizacao de acao.

### O que entrou

1. Estado dedicado `CursorState.Capturando`.
2. Entrada por atalho `C` nos estados de scanner de movimento (`MoveuParado` e `MoveuAndando`).
3. Execucao em coroutine com ordenacao de passos:
- pre delay
- `capturing.mp3`
- aplicacao de efeito (dano de captura ou cura de recuperacao)
- `done.mp3` (quando nao conclui) ou `captured.mp3` (quando conclui)
- `hasActed = true` e liberacao do cursor
4. Finalizacao unificada da acao para manter consistencia com os demais fluxos do turno.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Capture.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`

---

## 2) Reconquista de base aliada (recuperacao)

### Objetivo

Permitir operacao de "capturar a propria base" quando a construcao aliada estiver danificada (`CurrentCapturePoints < CapturePointsMax`).

### O que entrou

1. Especializacao do sensor `PodeCapturar` para dois papeis:
- `CaptureEnemy`: base inimiga/neutra (dano de captura)
- `RecoverAlly`: base aliada danificada (cura de captura)
2. Regra de recuperacao:
- usa HP da unidade como forca de cura
- soma em `CurrentCapturePoints` ate `CapturePointsMax`
3. Fluxo de conclusao:
- ao completar recuperacao (`cap atual == cap max`), toca `captured.mp3`
- nao troca time da base (ja e aliada)

### Arquivo-chave

- `Assets/Scripts/Sensors/PodeCapturarSensor.cs`

---

## 3) Timing centralizado no AnimationManager

### Objetivo

Padronizar parametros de tempo da sequencia de captura no mesmo ponto onde ja ficam timings de animacao (embarque/desembarque/combate).

### O que entrou

1. Novos parametros em `AnimationManager` para captura:
- `capturePreSfxDelay`
- `capturePostCapturingSfxDelay`
- `capturePostDoneSfxDelay`
- `capturePostCapturedSfxDelay`
2. Consumo desses valores pelo fluxo de captura no `TurnStateManager`.
3. Remocao dos delays locais do `TurnStateManager`.

### Arquivo-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 4) HUD de construcao (capture bar + foguinhos)

### Objetivo

Melhorar legibilidade do estado de captura e evitar poluicao visual quando ha unidade sobre a construcao.

### O que entrou

1. Barra de captura oculta quando existe unidade no hex da construcao:
- vale para unidade aliada e inimiga
- inclui ocultacao do texto da barra
2. Rebind defensivo de referencias do `ConstructionHudController` para resolver casos de referencia externa/quebrada em instancia de cena.
3. Ajuste de regra visual dos foguinhos:
- sem fogo apenas em `cap atual == cap max`
- qualquer discrepancia ja exibe 1 fogo
- thresholds adicionais mantidos para 2/3 fogos em faixas criticas
4. Ajuste de ordem de atualizacao da animacao dos foguinhos (`LateUpdate`) para evitar efeito visual de "fogo parado".

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionHudController.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`

---

## 5) Tooling: Tools > Sensors > Pode Capturar

### Objetivo

Deixar o debug tool aderente ao novo contrato do sensor (captura ofensiva e recuperacao defensiva).

### O que entrou

1. Exibicao do tipo de operacao (`CaptureEnemy` / `RecoverAlly`).
2. Simulacao debug atualizada para:
- aplicar dano de captura em inimigo/neutro
- aplicar cura de captura em aliado danificado
3. Mensagens de status e logs diferenciados para captura vs recuperacao.

### Arquivo-chave

- `Assets/Editor/PodeCapturarSensorDebugWindow.cs`

---

## 6) Assets de audio de estado

### O que entrou

- `Assets/audio/state/capturing.MP3`
- `Assets/audio/state/captured.MP3`

Integracao desses clips no fluxo de captura/reconquista.

---

## 7) Observacoes de volume desta versao

O volume bruto do commit inclui mudancas grandes de cena/snapshot (`SampleScene` e `_Recovery`), o que infla o diff total em relacao ao escopo funcional de captura/reconquista.

---

## Resumo de volume (base `8f5795b` -> `ecd3358`)

- `37 files changed`
- `216400 insertions`
- `34073 deletions`
