# Relatorio Consolidado v1.0.9 a v1.0.16

## Fontes incluidas
- `RELATORIO_V1.0.9.md`
- `RELATORIO_V1.0.11.md`
- `RELATORIO_V1.0.12.md`
- `RELATORIO_V1.0.13.md`
- `RELATORIO_V1.0.14.md`
- `RELATORIO_V1.0.15.md`
- `RELATORIO_V1.0.16.md`

## Versoes sem arquivo no repositorio
- `v1.0.10`

---

## Conteudo original: `RELATORIO_V1.0.9.md`

# Relatorio Tecnico - v1.0.9

## Versoes cobertas

- `v1.0.9` - commit `6a4cd92` - `refatoraÃ§Ã£o do sistema de pouso e decolagem`
- Branch: `main`

Este relatorio resume as principais mudancas entregues na versao `v1.0.9`.

---

## 1) Refatoracao de regras de decolagem por skill-slot (Construction)

### Objetivo

Remover regras hardcoded por tipo de skill e permitir configuracao de modo de decolagem por item da lista de skills aceitas na construcao.

### O que entrou

1. Novo modelo de regra por skill:
- `ConstructionLandingSkillRule` com:
  - `skill`
  - `takeoffMode` (`TakeoffProcedure`)

2. Novo campo principal em `ConstructionData`:
- `requiredLandingSkillRules` (lista de `skill + takeoffMode`).

3. Compatibilidade de dados:
- campo legado `legacyRequiredLandingSkills` mantido para migracao;
- migracao automatica no `OnValidate` quando a lista nova estiver vazia;
- saneamento de valores de enum legado para `InstantToPreferredHeight`.

4. Resolver de Air Ops atualizado:
- validacao de skill em construcao passou a extrair skills da lista nova;
- plano de decolagem passa a usar o `takeoffMode` configurado no slot da skill.

### Arquivos-chave

- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Editor/ConstructionDataEditor.cs`

---

## 2) Refatoracao de regras de decolagem por skill-slot (Structure + Terrain Pair)

### Objetivo

Aplicar o mesmo padrao configuravel por slot na secao `Aircraft Ops By Terrain`.

### O que entrou

1. Novo modelo de regra por skill em estrutura:
- `StructureLandingSkillRule` com:
  - `skill`
  - `takeoffMode`

2. Novo campo por par Estrutura+Terreno:
- `requiredLandingSkillRules` (lista de `skill + takeoffMode`).

3. Compatibilidade de dados:
- campo legado `legacyRequiredLandingSkills` mantido e oculto no inspector;
- migracao automatica no `OnValidate`.

4. Padronizacao de nomenclatura:
- `isRoadRunway` renomeado para `allowTakeoffAndLanding`;
- migracao preservada com `FormerlySerializedAs("isRoadRunway")`.

5. Resolver atualizado:
- validacao de pouso/decolagem em contexto `Structure` usa lista nova;
- plano de decolagem em `Structure` usa `takeoffMode` do slot configurado.

### Arquivos-chave

- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Editor/StructureDataEditor.cs`

---

## 3) Simplificacao de procedimentos de decolagem

### Objetivo

Eliminar acoplamento com nomenclatura antiga de skill e reduzir duplicidade de procedimento equivalente.

### O que entrou

- removido `VTOLPopUpToPreferredHeight` do enum `TakeoffProcedure`;
- VTOL passa a usar `InstantToPreferredHeight`;
- sensor de decolagem considera `full move` apenas por `InstantToPreferredHeight`.

### Arquivos-chave

- `Assets/Scripts/Units/AirOperationTypes.cs`
- `Assets/Scripts/Units/Rules/AirOperationResolver.cs`
- `Assets/Scripts/Sensors/PodeDecolarSensor.cs`

---

## 4) PodeDecolar com retorno operacional por codigos

### Objetivo

Padronizar retorno do sensor para dirigir fluxo de selecao/movimento no `TurnStateManager`.

### O que entrou

- `PodeDecolarReport.takeoffMoveOptions` (`List<int>`) com codigos:
  - `-1`: ignorar sensor (nao-aeronave ou aeronave ja em voo);
  - `0`: decolagem mantendo 0 hex;
  - `1`: decolagem com 1 hex;
  - `0,1`: decolagem com 0 ou 1 hex;
  - `9`: decolagem para altitude nativa com liberdade de movimento.

### Arquivo-chave

- `Assets/Scripts/Sensors/PodeDecolarSensor.cs`

---

## 5) TurnStateManager: snapshot de decolagem temporaria + ESC em duas etapas

### Objetivo

Permitir "decolagem temporaria" ao selecionar unidade e restaurar estado corretamente com `ESC`.

### O que entrou

1. Snapshot temporario ao sair de `Neutral`:
- guarda dominio/altura/flags originais da aeronave;
- guarda tambem as opcoes retornadas pelo `PodeDecolar`.

2. Comportamento de selecao:
- se retorno for `0`, `1` ou `0,1`, unidade sobe para `AirLow` ao entrar em `UnitSelected`;
- movimento permitido respeita exatamente esse conjunto de opcoes.

3. ESC em duas etapas:
- `ESC` de `MoveuParado/MoveuAndando` volta para `UnitSelected` mantendo estado aereo temporario;
- segundo `ESC` (saindo de `UnitSelected` para `Neutral`) restaura estado pousado original do snapshot.

4. Guarda para auto takeoff forcado:
- rotina de auto takeoff para movimento e ignorada quando o fluxo temporario (`0/1/0,1`) ja esta ativo.

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.StateMachine.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Movement.cs`

---

## 6) Range map filtrado por opcoes do PodeDecolar

### Objetivo

Restringir area pintada de movimento conforme retorno operacional do sensor.

### O que entrou

Filtro no paint do range:
- `-1` ou `9`: ignora filtro (range normal);
- `0`: pinta apenas hex atual;
- `1`: pinta apenas vizinhos imediatos;
- `0,1`: pinta hex atual + vizinhos.

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.Range.cs`
- `Assets/Scripts/Match/TurnStateManager.cs`

---

## 7) Promocao automatica para altitude nativa em unidade ja no ar

### Objetivo

Ao selecionar aeronave ja em voo (retorno `-1` no `PodeDecolar`), promover para altitude nativa de operacao e manter rollback em `ESC`.

### O que entrou

1. Promocao automatica na selecao:
- exemplo: unidade em `AirLow` sobe para `AirHigh` se essa for a altitude preferida.

2. Snapshot de entrada para callbacks/rollback:
- dominio/altura de entrada da promocao sao guardados.

3. ESC de saida da selecao:
- ao voltar para `Neutral`, restaura o estado de entrada (ex.: volta para `AirLow`).

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`

---

## Observacoes

- A versao `v1.0.9` consolida o sistema de pouso/decolagem como configuracao por contexto e por slot de skill.
- Construction e Structure agora seguem o mesmo padrao de configuracao (`skill + takeoffMode`), com migracao de dados legados preservada.


---

## Conteudo original: `RELATORIO_V1.0.11.md`

# Relatorio Tecnico - v1.0.11

## Versoes cobertas

- `v1.0.10` (remoto existente) - commit `950c348` - `sistema de pouso e decolagem completo, sistema naval e submersivel`
- `v1.0.11` - commit `7820e7a` - `v1.0.10 - combate basico terminado`
- Branch: `main`

Este relatorio consolida o pacote de combate fechado em `v1.0.11`, incluindo o contexto da `v1.0.10` ja existente no remoto.

---

## 1) Fluxo de combate em gameplay (estado "A")

### Objetivo

Sair do placeholder de "confirmar e encerrar" para uma sequencia visual/sonora completa.

### O que entrou

1. Confirmacao de alvo com subetapas:
- ciclo de alvos
- confirma alvo
- rollback por `ESC`

2. Sequencia de combate:
- audio inicial por tipo de tiro (straight/parabolic)
- efeitos de impacto
- voo dos projetis
- aplicacao de HP no momento correto
- tratamento de morte
- finalizacao da acao

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

---

## 2) Projetil e audio por arma

### Objetivo

Cada arma disparar com seu proprio som e sprite de municao em voo.

### O que entrou

1. `WeaponData`:
- `fireSfx`
- `fireSfxVolume`
- `ammunitionSprite`

2. Override de escala por arma:
- `useExplicitProjectileScale`
- `projectileScale`
- fallback para escala global do `AnimationManager`.

3. Projetil em reta/parabola no `AnimationManager`.

### Arquivos-chave

- `Assets/Scripts/Weapons/WeaponData.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 3) Hit FX e resposta visual no alvo

### Objetivo

Dar feedback claro de dano no impacto.

### O que entrou

1. Taking hit por sprite sequence (`hit1..5`).
2. Flash vermelho/branco.
3. Shake na unidade atingida.

### Arquivo-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`

---

## 4) Bump de combate ("bicada")

### Objetivo

Adicionar antecipacao corporal antes do disparo.

### O que entrou

1. `bump together`:
- quando ambos lados atacam em `Straight` e existe revide.

2. `bump towards`:
- quando apenas um lado ataca em `Straight`.

3. Regras:
- tiros parabolicos ficam fora do bump.

### Arquivos-chave

- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 5) Morte: blink, explosao e sumico

### Objetivo

Executar morte em ordem visual correta.

### O que entrou

1. Fluxo por unidade morta:
- espera `combatDeathStartDelay`
- cursor vai ate a unidade (com som de cursor)
- unidade pisca varias vezes
- unidade some
- toca `explosion` (SFX) junto do VFX de explosao
- pequena pausa

2. Dupla morte:
- trata em sequencia.

3. Ajuste de blink:
- prioriza o sprite principal da unidade (nao tile/hex overlay).

### Arquivos-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Scripts/Units/UnitManager.cs`

---

## 6) Dano proporcional em cadeia de embarcados

### Objetivo

Propagar impacto do transportador para unidades embarcadas (inclusive recursivo).

### O que entrou

1. Se transportador leva `X%` de dano:
- embarcados levam `X%` do HP atual.
- embarcados dos embarcados tambem.

2. Se transportador morre:
- cadeia embarcada morre sem animacao individual.

3. Combatente direto morto:
- permanece para animacao de morte (nao e removido cedo pela cascade).

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 7) Ajustes de UX de mirando

### Objetivo

Reduzir friccao no uso do sensor de ataque.

### O que entrou

1. Auto-select quando existe apenas 1 alvo.
2. `ESC` no confirma de alvo unico volta para sensores de movimento.
3. Linha de mira oculta no substep de confirmacao.

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`

---

## 8) Ajustes auxiliares

### O que entrou

- reforco de auto-bind de DBs no `TurnStateManager` para evitar divergencia de calculo entre tools e gameplay.
- correcoes em cursor/camera e fluxo de neutral/hq aplicadas no periodo.

### Arquivos-chave

- `Assets/Scripts/Match/TurnStateManager.cs`
- `Assets/Scripts/Cursor/CursorController.cs`
- `Assets/Scripts/Camera/CameraController.cs`
- `Assets/Scripts/Match/MatchController.cs`

---

## Assets e conteudo adicionados/organizados

- `Assets/audio/combat/explosion.MP3`
- `Assets/img/animations/hit/*`
- `Assets/img/animations/explosion/*`
- `Assets/img/armas/*`
- `Assets/img/municao/*`

---

## Resumo de volume (base `950c348` -> `7820e7a`)

- `96 files changed`
- `23191 insertions`
- `9338 deletions`


---

## Conteudo original: `RELATORIO_V1.0.12.md`

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


---

## Conteudo original: `RELATORIO_V1.0.13.md`

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


---

## Conteudo original: `RELATORIO_V1.0.14.md`

# Relatorio Tecnico - v1.0.14

## Versoes cobertas

- `v1.0.13` - commit `ecd3358` - `v1.0.13 captura e reconquista`
- `v1.0.14` - commit `a50c0d4` - `v1.0.14 - fusÃ£o de unidades`
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


---

## Conteudo original: `RELATORIO_V1.0.15.md`

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


---

## Conteudo original: `RELATORIO_V1.0.16.md`

# Relatorio Tecnico - v1.0.16

## Versoes cobertas

- `v1.0.15` - commit `04d813f` - `v1.0.15 - Logistica: Suprir Unidades`
- `v1.0.16` - commit `fd9990e` - `v1.0.16 - logistica de porta avioes`
- Branch: `main`

Este relatorio consolida os ajustes de logistica embarcada (porta-avioes), junto com a nova regra de trilhos por skill e melhorias de tooling/dados de estruturas e terrenos.

---

## 1) Logistica embarcada (porta-avioes)

### Objetivo

Habilitar `Pode Suprir` para suppliers com `serviceRange=EmbarkedOnly`, com fluxo completo de selecao e execucao sobre passageiros embarcados.

### O que entrou

1. Sensor `PodeSuprir` com range embarcado:
- coleta candidatos a partir de `TransportedUnitSlots` do supplier
- permite alvo embarcado no proprio supplier (inclusive quando inativo no hierarchy)
2. Bypass de validacao de dominio para passageiros embarcados do proprio supplier:
- para `EmbarkedOnly`, nao bloqueia por `supplierOperationDomains` no check inicial
- para candidato embarcado, usa dominio/altura do supplier como plano efetivo
3. Mensagem de indisponibilidade dedicada:
- `Sem unidades embarcadas validas para suprir.`

### Arquivo-chave

- `Assets/Scripts/Sensors/PodeSuprirSensor.cs`

---

## 2) Runtime de fila de suprimento para embarcados

### Objetivo

Fazer a fila `Suprindo` aceitar e executar atendimento de unidades embarcadas no proprio supplier, sem quebrar regras de camada para alvos externos.

### O que entrou

1. Lista de candidatos aceita embarcados do supplier selecionado.
2. Em embarcados:
- cursor/alvo usa cell do supplier
- ignora transicoes `forceLand/forceTakeoff/forceSurface`
- ignora validacao de camada de atendimento da fila
3. Visual de selecao/execucao embarcada:
- mostra 1 embarcado por vez no preview de selecao (conforme cursor)
- durante execucao sequencial, garante 1 por vez (sem empilhamento visual)
- supplier oculta HUD durante o fluxo embarcado; HUD volta ao final
4. Ao atender cada embarcado:
- unidade aparece em `Land/Surface` no cell do supplier
- recebe sorting temporario acima do supplier
- HUD da unidade e atualizado/apresentado
- apos atendimento, unidade volta a estado oculto embarcado

### Arquivo-chave

- `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`

---

## 3) Regras de movimento por trilho (skill)

### Objetivo

Introduzir regra explicita para unidades com skill de trem, independente da hierarquia padrao de terreno/estrutura.

### O que entrou

1. Nova regra em pathing:
- se unidade tem skill `Linha de Trem`, so pode andar em hex com estrutura `isRail=true` e `structureBlocksRail=false`
2. Regra aplicada no validador de passagem de hex em movimento.

### Arquivo-chave

- `Assets/Scripts/Units/Rules/UnitMovementPathRules.cs`

---

## 4) Skill Rules em Terrain/Structure/Construction

### Objetivo

Padronizar bloqueios por skill no modelo de dados de mapa.

### O que entrou

1. `blockedSkills` em:
- `TerrainTypeData`
- `StructureData`
- `ConstructionData`
2. Organizacao da secao `Skill Rules` em structure/construction (junto de `requiredSkillsToEnter` e `skillCostOverrides`).
3. Inicializacao/garantia de listas em `OnValidate`.
4. Flags de trilho em `StructureData`:
- `isRail`
- `structureBlocksRail`

### Arquivos-chave

- `Assets/Scripts/Terrain/TerrainTypeData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`

---

## 5) Tooling: Road Route Painter

### Objetivo

Melhorar UX do painter para selecionar estrutura diretamente do catalogo do banco.

### O que entrou

1. Substituicao do `ObjectField` manual por seletor (`Popup`) com itens da `StructureDatabase` do `RoadNetworkManager`.
2. Sincronizacao de indice selecionado com estrutura atual.
3. Rotulo de opcoes no formato `id (displayName)`.
4. Mensagens de validacao quando nao ha database/itens.

### Arquivo-chave

- `Assets/Editor/RoadRoutePainterWindow.cs`

---

## 6) Dados e assets

### O que entrou

1. Nova skill:
- `Linha de Trem`
2. Novas estruturas:
- `Trilhos`
- `Ponte com Trilhos`
3. Ajustes de catalogos e estruturas/terrenos para regras de skill e trilho.
4. Novos tiles:
- `traintracks.png`
- `traintracks bridge.png`

### Arquivos de dados relevantes

- `Assets/DB/Character/Skills/Linha de Trem.asset`
- `Assets/DB/World Building/Structures/Trilhos.asset`
- `Assets/DB/World Building/Structures/Ponte com Trilhos.asset`
- `Assets/DB/World Building/Structures/Catalogo de Estruturas.asset`
- `Assets/DB/Character/Skills/Catalogo de Skills.asset`

---

## 7) Resumo de volume (base `04d813f` -> `fd9990e`)

- `29 files changed`
- `12865 insertions`
- `11748 deletions`


