# Relatorio Tecnico - v1.0.17

## Versoes cobertas

- `v1.0.16` - commit `fd9990e` - `v1.0.16 - logistica de porta avioes`
- `v1.0.17` - commit `d11422d` - `Logistica: Movimento de Estoque`
- Branch: `main`

Este relatorio consolida os ajustes de logistica de transferencia/suprimento, melhorias de diagnostico em tooling, regras de anti-combo de suprimento e correcoes visuais de HUD (unidade e construcao).

---

## 1) Nova acao de logistica: Pode Transferir (T)

### Objetivo

Introduzir o fluxo de transferencia de estoque entre hubs/unidades conforme modo de fluxo (`Fornecimento` e `Recebedor`) com regras explicitas de dominio, range, tier e capacidade.

### O que entrou

1. Novo sensor de transferencia com opcoes validas e invalidas:
- `PodeTransferirSensor`
- `PodeTransferirOption`
- `PodeTransferirInvalidOption`
2. Integracao no turno (`ScannerPrompt`/sensores) para acao `T`.
3. Suporte a range `Hybrid 0 or 1 hex`.
4. Regras de bloqueio para cenarios invalidos (ex.: construction tier `Receiver` nao fornece).
5. Labels padronizados e mais intuitivos:
- `<A> -> Fornece -> <B>`
- `<A> <- Recebe <- <B>`

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeTransferirSensor.cs`
- `Assets/Scripts/Sensors/PodeTransferirOption.cs`
- `Assets/Scripts/Sensors/PodeTransferirInvalidOption.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Transfer.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`

---

## 2) Tooling de diagnostico: Tools > Logistica > Pode Transferir

### Objetivo

Dar visibilidade detalhada do por que uma transferencia e valida/invalida e como os valores foram aplicados.

### O que entrou

1. Nova janela de debug:
- `PodeTransferirSensorDebugWindow`
2. Relatorio reorganizado por ordem de fluxo com secoes:
- `Fornecedor`
- `Destino`
- `Excedente nao enviado: <quem>`
3. Melhorias de legibilidade:
- `infinite` para estoque infinito
- exibicao apenas do que foi efetivamente necessario/transferido
- motivos de bloqueio em candidatos invalidos

### Arquivo-chave

- `Assets/Editor/PodeTransferirSensorDebugWindow.cs`

---

## 3) Anti-combo de suprimento por rodada

### Objetivo

Bloquear o combo de curar/reabastecer a mesma unidade duas vezes na mesma rodada com suppliers diferentes.

### O que entrou

1. Nova flag runtime na unidade:
- `receivedSuppliesThisTurn` (atalho visual `_X` no nome da instancia)
2. Marcacao automatica ao receber servico de suprimento com efeito real.
3. Limpeza da flag no inicio do turno do time (junto de `ResetActed`).
4. Validacao no `PodeSuprir`:
- candidato fica invalido quando ja recebeu suprimentos na rodada
- motivo explicito no diagnostico
5. Escopo intencional:
- **nao** aplicado ao `PodeTransferir` (transferencia de estoque continua independente desta flag)

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Supply.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`

---

## 4) Regras de supply/logistica em dados

### Objetivo

Alinhar nomenclatura e comportamento de regras de emergir/subir camada e operation domains em dados de unidade/construcao/terreno/estrutura.

### O que entrou

1. Ajustes de dados para suportar `Hybrid 0 or 1 hex` em ranges de servico/coleta.
2. Ajustes de catalogos/unidades/construcoes para coerencia de dominios operacionais e tiers.
3. Padronizacao do conceito `forced to emerge` nos pontos de configuracao relevantes.

### Arquivos relevantes

- `Assets/DB/Character/Unit/*.asset`
- `Assets/DB/World Building/Construction/*.asset`
- `Assets/Scripts/Units/SupplierSettings.cs`
- `Assets/Scripts/Construction/ConstructionSupplierSettings.cs`

---

## 5) Correcoes visuais de HUD (construcao/unidade)

### Objetivo

Eliminar inconsistencias de camada, bind e legado em HUD, principalmente em `capture_bar` de construcao e fuel bar de unidade.

### O que entrou

1. Construction HUD:
- bind mais robusto para `capture_bar`, `capture`, `capture_text`
- ordem visual garantida (texto acima da barra)
- hide/show consistente quando unidade ocupa o hex
- correcoes para cenarios de instancias antigas em cena (limpeza de lixo legado via propagate)
2. Unit HUD:
- sorting mais robusto para evitar barra de fuel atras de sprite de construcao
- ajuste para acompanhar camada do sprite pai quando necessario

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionHudController.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Scripts/Editor/ConstructionDataTools.cs`
- `Assets/Scripts/Units/UnitHudController.cs`

---

## 6) Ajustes complementares de regras/sensores

### O que entrou

1. Blindagens adicionais em sensores de mira/logistica.
2. Ajustes em movimento/cursor/scanner para consistencia com os novos fluxos de supply/transfer.

### Arquivos relevantes

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Movement.cs`
- `Assets/Scripts/Cursor/CursorController.cs`

---

## 7) Resumo de volume (base `fd9990e` -> `d11422d`)

- `60 files changed`
- `25882 insertions`
- `14403 deletions`
