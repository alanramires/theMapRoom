# Relatorio Tecnico - v1.0.12

## Versoes cobertas

- `v1.0.11` - commit `7820e7a` - `v1.0.10 - combate basico terminado`
- `v1.0.12` - commit `8f5795b` - `desembarque concluido e minor fixes`
- Branch: `main`

Este relatorio consolida o fechamento do fluxo de desembarque em gameplay, melhorias de ferramentas de debug/sensores e ajustes de UX de turno/combate.

---

## 1) Desembarque concluido (estado "D")

### Objetivo

Sair do placeholder para um fluxo completo com subetapas, fila de ordens e execucao animada.

### O que entrou

1. Estado dedicado de desembarque com subetapas:
- `passenger select`
- `landing select`
- `confirm`

2. Selecao numerica de passageiros:
- auto-pula quando existe apenas 1 passageiro
- suporte a `0` para executar fila parcial
- `ESC` com rollback progressivo entre subetapas

3. Fila de ordens de desembarque:
- reserva de hex por ordem
- bloqueio de passageiro duplicado
- bloqueio de hex duplicado
- execucao sequencial das ordens

4. Integracao com cursor/range:
- highlight dos hex validos
- navegacao por setas nos hex de desembarque
- cursor travado fora da subetapa de escolha de hex

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Disembark.cs`

---

## 2) Sequencia de animacao de desembarque

### Objetivo

Executar ordem visual clara e legivel para transportador + passageiros.

### O que entrou

1. Forced landing do transportador aereo antes do desembarque.
2. Spawn dos passageiros sobre o transportador.
3. Movimento sequencial dos passageiros (nao simultaneo).
4. Follow do cursor durante cada etapa.
5. SFX por etapa (`load`, `done`, movimentacao).
6. Encerramento com `hasActed` aplicado na ordem correta.

### Timing configuravel

Novos controles no `AnimationManager` para delays e transicoes da sequencia de desembarque (modelo irmao do bloco de embarque).

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Disembark.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 3) Regra de aeronave no desembarque (carrier/takeoff)

### Objetivo

Alinhar gameplay e sensor com regras de decolagem para passageiros aereos.

### O que entrou

1. Validacao em runtime e sensor baseada em regra de decolagem.
2. Carrier naval com slot aereo aceito no fluxo de desembarque.
3. Decolagem curta para desembarque de aeronave:
- spawn pousado (`Land/Surface`)
- transicao para `Air/Low`
- deslocamento para hex alvo

4. Ajustes para evitar bloqueio incorreto por custo terrestre no caso de passageiro aeronave.

### Arquivos-chave

- `Assets/Scripts/Sensors/PodeDesembarcarSensor.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Disembark.cs`

---

## 4) Tools > Sensors > Pode Desembarcar (debug)

### Objetivo

Deixar o debug tool aderente ao fluxo real de desembarque e util para simulacao rapida.

### O que entrou

1. Exibicao e selecao de passageiros embarcados.
2. Simulacao com fila de ordens respeitando reservas de hex.
3. Limpeza/atualizacao de listas validas-invalidas conforme mudanca de ordem/passsageiro.
4. Botao de execucao em modo debug (teleporte/skip de animacao permitido no tool).

### Arquivo-chave

- `Assets/Editor/PodeDesembarcarSensorDebugWindow.cs`

---

## 5) Construcao: shopping + propagate + instancia

### Objetivo

Fechar fluxo de oferta por construcao (data -> instancia -> gameplay).

### O que entrou

1. Estado de compra/servicos em construcao aliada com oferta disponivel.
2. Inspecao quando nao ha oferta.
3. Propagacao de dados de construcao para instancias em campo via tools.
4. Ajustes de ownership/original team durante propagate.
5. Suporte a customizacao por instancia (micro-ajustes por construcao especifica no mapa).

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Construction/ConstructionSupplierSettings.cs`
- `Assets/Scripts/Editor/ConstructionDataTools.cs`
- `Assets/Editor/ConstructionFieldDatabaseEditor.cs`
- `Assets/Editor/ConstructionManagerEditor.cs`

---

## 6) Passagem de turno e audio

### Objetivo

Estabilizar a transicao de turno com sequencia de audio/cursor/camera.

### O que entrou

1. Atalho para avancar turno.
2. Sequencia com pre-delay e post-delay configuraveis.
3. Cursor travado durante evento e retorno ao controle no fim.
4. Camera focando HQ do proximo jogador.
5. Integracao com modo de musica por time e modo livre.
6. Novo SFX de fim de turno.

### Arquivos-chave

- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Editor/MatchControllerEditor.cs`
- `Assets/Scripts/Audio/MatchMusicAudioManager.cs`
- `Assets/audio/UI/ending the turn.MP3`

---

## 7) UX de mirar (A) e linha de tiro

### Objetivo

Melhorar leitura do confirm de ataque sem poluir a fase de execucao.

### O que entrou

1. Com alvo unico, confirm target pode manter preview da linha.
2. Ao iniciar combate confirmado, preview e spotter line sao ocultados antes dos tiros.
3. Blindagem para nao reexibir linha durante `combatExecutionInProgress`.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 8) Estabilidade de runtime/editor

### O que entrou

1. Ajustes para preservar configuracoes de embarcados em cenarios de refresh/reload.
2. Correcoes de sincronizacao de sensores e estado.
3. Correcoes de fluxo neutro/confirm em `TurnState`.

### Arquivos-chave

- `Assets/Scripts/Units/UnitManager.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Sensors.cs`
- `Assets/Scripts/Cursor/CursorController.cs`

---

## Assets e conteudo adicionados/organizados

- `Assets/audio/UI/ending the turn.MP3`
- rename de trilha de time: `team4` -> `team3`
- atualizacao de assets de construcao em campo e cena de validacao

---

## Resumo de volume (base `7820e7a` -> `8f5795b`)

- `37 files changed`
- `14306 insertions`
- `10589 deletions`
