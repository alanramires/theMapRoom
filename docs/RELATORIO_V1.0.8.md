# Relatorio Tecnico - v1.0.8

## Versoes cobertas

- `v1.0.8` - commit `d2d0191` - `v1.0.8 - sistema de embarque concluido`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.8`.

---

## 1) Sistema de embarque (fluxo completo em gameplay)

### Objetivo

Fechar o ciclo de embarque com selecao, confirmacao, animacao, validacao de vaga e aplicacao de estado.

### O que entrou

1. Fluxo jogavel completo:
- `sensor -> lista de transportadores validos -> confirmacao -> execucao de embarque`.
- transporte invalido continua visivel no debug, mas selecao em gameplay ocorre apenas entre validos.

2. Preview visual e navegacao:
- linha de preview entre passageiro e transportador (reuso da linha de `Pode Mirar`);
- ciclagem de opcoes por setas;
- ao entrar no submenu do sensor valido, cursor vai para o primeiro item.

3. Confirmacao e estado:
- confirmacao do embarque integrada ao loop de input;
- durante confirmacao, linha de preview permanece ativa;
- `ESC` retorna para sensores com cursor restaurado na unidade quando aplicavel.

4. Execucao do embarque:
- animacao de movimento do passageiro ate o transportador;
- suporte a `forced landing` do transportador aereo quando necessario;
- unidade embarcada assume cell do transportador e fica invisivel;
- unidade embarcada deixa de participar dos sensores de alvo.

5. Ato e audio:
- ao concluir embarque, passageiro finaliza acao (`HasActed = true`);
- transportador tambem marca `HasActed` conforme regra atual;
- audio `load.mp3` tocado na conclusao.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 2) Runtime de transporte no UnitManager

### Objetivo

Permitir controle manual e visual de vagas ocupadas/disponiveis por instancia em campo.

### O que entrou

1. Slots runtime por instancia:
- `transportedUnitSlots` no `UnitManager`;
- vinculo bidirecional passageiro <-> transportador (`EmbarkedTransporter`, `EmbarkedTransporterSlotIndex`).

2. Inspector de runtime:
- lista de vagas por slot/capacidade;
- atribuicao manual de passageiro por vaga no inspector;
- suporte a limpar vagas e refresh a partir do `UnitData`.

3. Indicador visual de transporte:
- HUD de transporte aparece quando houver pelo menos 1 vaga ocupada;
- nao aparece para unidade sem passageiros ou nao-transportadora.

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Units/UnitHudController.cs`

---

## 3) Autonomia: migracao para AutonomyData/AutonomyDatabase

### Objetivo

Desacoplar autonomia de classes fixas e deixar configuracao por data asset.

### O que entrou

1. Novo modelo de dados:
- `AutonomyData` (perfil de multiplicador de movimento e upkeep);
- `AutonomyDatabase` (catalogo de perfis).

2. Vinculo por unidade:
- `UnitData` passa a ter referencia `autonomyData`;
- regras de autonomia passam a ler o perfil vinculado na unidade.

3. Regras aplicadas:
- multiplicador de custo por passo via perfil de autonomia;
- upkeep por virada de turno conforme dominio/altura no inicio do turno.

4. Ajuste de regra:
- deteccao de autonomia operacional passou a usar `autonomyData` como fonte de verdade.

### Arquivos-chave

- `Assets/Scripts/Units/AutonomyData.cs`
- `Assets/Scripts/Units/AutonomyDatabase.cs`
- `Assets/Scripts/Units/Rules/OperationalAutonomyRules.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Units/UnitData.cs`

---

## 4) HUD de altitude e sincronizacao de camada

### Objetivo

Exibir estado de altitude/submersao da unidade no HUD e manter sincronia com mudancas de camada.

### O que entrou

1. Indicador `altitude` no `Unit HUD`:
- `air high` -> icone high;
- `air low` -> icone low;
- `land/surface` -> oculto;
- `submerged` -> icone submerged.

2. Sincronizacao:
- atualizacao ao propagar dados da unidade (`Tools > Propagate Unit Data`);
- atualizacao ao usar `Subir Domain` / `Descer Domain`.

3. Atalhos de camada:
- no gameplay em `UnitSelected`, atalhos `S/D` para ciclar camadas (sem animacao de Y).

### Arquivos-chave

- `Assets/Scripts/Units/UnitHudController.cs`
- `Assets/Scripts/Editor/UnitLayerTools.cs`
- `Assets/Editor/UnitManagerEditor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 5) Ajustes visuais e UX adicionais

1. Sorting temporario em embarque:
- unidade em pouso/embarque sobe temporariamente o `Order in Layer` e restaura ao final.

2. Move preview:
- ocultacao do `move preview path` ao entrar no submenu de sensor.

3. Logs e feedback:
- painel de sensores com disponibilidade de acoes;
- feedback sonoro consistente para opcoes validas/ciclagem.

4. Construcoes:
- construcao escurece quando ocupada por unidade do mesmo time que ainda nao agiu;
- volta ao normal quando unidade ja agiu;
- unidades de time diferente nao disparam escurecimento.

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`
- `Assets/Scripts/Cursor/CursorController.cs`

---

## 6) Ajuste final de custo de embarque

### Objetivo

Melhorar legibilidade do consumo de autonomia durante o embarque.

### O que entrou

- custo de autonomia do embarque agora e aplicado antes do delay final da sequencia;
- em caso de falha apos desconto, ha rollback do valor;
- log final exibe `custo` e `autonomia antes->depois`.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## Observacoes

- O sistema de embarque foi fechado com regras e UX de selecao/confirmacao/execucao.
- A calibracao de `YPos` para troca de sprite por camada foi removida por variacao artistica entre sprites.
- O estado atual mantem troca de camada instantanea com atalhos `S/D`.
