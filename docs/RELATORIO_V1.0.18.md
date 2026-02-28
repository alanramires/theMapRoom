# Relatorio Tecnico - v1.0.18

## Versoes cobertas

- `v1.0.17` - commit `d11422d` - `Logistica: Movimento de Estoque`
- `v1.0.18` - commit `cf76466` - `v1.0.18 -m" Serviço do Comand\`
- Branch: `main`

Este relatorio consolida a entrega do fluxo de **Servico do Comando (X)**, ajustes de fila para transportadores/embarcados, melhorias de animacao e diagnostico de suprimento, e correcoes de calculo em cenarios de estoque infinito.

---

## 1) Nova acao global: Servico do Comando (X)

### Objetivo

Executar, a partir do cursor em estado `Neutral`, uma ordem automatica de servicos sobre unidades elegiveis em construcoes aliadas supridoras e em unidades embarcadas de transportadores supridores.

### O que entrou

1. Novo fluxo no turno para disparo e execucao da ordem:
- hotkey `X`
- coleta de elegiveis/invalidos
- execucao em fila com animacao de cursor/projeteis/servicos
2. Reuso das mesmas validacoes centrais de suprimento:
- necessidade real de servico
- disponibilidade de estoque
- compatibilidade de dominio/camada (incluindo pouso/decolagem/emergir quando aplicavel)
3. Regra de anti-combo preservada:
- unidade atendida marca `receivedSuppliesThisTurn`

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`
- `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`
- `Assets/Scripts/Sensors/ServicoDoComandoOption.cs`
- `Assets/Scripts/Sensors/ServicoDoComandoInvalidOption.cs`

---

## 2) Tooling: debug do Servico do Comando

### Objetivo

Dar visibilidade de elegiveis/invalidos e comportamento de execucao para troubleshooting.

### O que entrou

1. Janela dedicada em `Tools` para inspeção de opcoes do sensor.
2. Logs detalhados por item da fila durante a execucao.

### Arquivo-chave

- `Assets/Editor/ServicoDoComandoDebugWindow.cs`

---

## 3) Fila de execucao para transportador + embarcados

### Objetivo

Quando um transportador suprido tiver embarcados elegiveis, manter o grupo junto na fila de iniciativa da ordem.

### Comportamento aplicado

1. Reordenacao da fila por “familia” de transportador:
- embarcados primeiro (ordem de assento)
- depois o proprio transportador
2. Demais candidatos seguem apos os blocos de familia.

### Arquivo-chave

- `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`

---

## 4) Ajustes visuais de animacao (HP/Fuel) em execucao de fila

### Objetivo

Eliminar percepcao de comportamento aleatorio em atualizacao de HUD durante execucao sequencial.

### O que entrou

1. Animacao dedicada de recuperacao de HP (`AnimateHpRecoverFill`), alinhada ao fluxo de fuel.
2. Refino da animacao de fuel:
- duracao minima/visivel
- refresh imediato do HUD no inicio
- frame final de estabilizacao antes de avancar na fila
3. Tratamento de HUD em alvos embarcados:
- ativacao segura de HUD/fuel container antes da animacao quando necessario
4. Alinhamento do hide/show do HUD do transportador no fluxo de embarcados do comando.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`

---

## 5) Diagnostico expandido em logs

### Objetivo

Explicar claramente por que cada alvo recebeu (ou nao) HP/Fuel/Ammo.

### O que entrou

1. Logs por alvo/fila no Servico do Comando.
2. Logs de fuel:
- inicio/fim de animacao
- motivo quando nao ha animacao de fuel
3. Logs de HP (`[HpRepair]`):
- ganho efetivo
- motivo de nao reparo (HP cheio, cap=0, eficiencia=0, sem estoque, falha de consumo, etc.)
4. Logs de municao (`[AmmoGain]`) por arma embarcada:
- ganho por arma (before->after)
- motivo de “sem rearm”

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Supply.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`

---

## 6) Correcao critica: overflow com estoque “infinito”

### Problema

Calculos como `stock * pointsPerSupply` podiam estourar `int` quando `stock=int.MaxValue`, levando a recuperacao calculada incorreta (inclusive zero).

### Correcao

1. Multiplicacao segura com clamp para `int.MaxValue` (`SafeMultiplyToIntMax`).
2. Aplicacao em HP/Fuel/Ammo nos fluxos de suprimento/comando.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Supply.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`

---

## 7) Inspector de UnitManager (runtime/debug)

### Objetivo

Facilitar leitura do limite de ataques/municao por slot de arma embarcada em unidades instanciadas.

### O que entrou

1. Campo de leitura em `Embarked Weapons`:
- `Ammo / Attacks Max (UnitData)`
2. Exibicao por slot runtime, usando baseline do `UnitData`.

### Arquivo-chave

- `Assets/Editor/UnitManagerEditor.cs`

---

## 8) Resumo de volume (base `d11422d` -> `cf76466`)

- `23 files changed`
- `21628 insertions`
- `16687 deletions`
