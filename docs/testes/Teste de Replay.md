**Checklist QA - Replay + Hardening (Unity)**

| ID | Cenario | Pre-condicao | Acao | Resultado esperado |
|---|---|---|---|---|
| R01 (OK)| Gravacao ActionStack: `move` | Partida ativa, unidade movel selecionavel | Mover unidade e finalizar acao | 1 batch gravado; replay reproduz exatamente celula origem/destino |
| R02 (OK) | Gravacao ActionStack: `attack` | Unidade com alvo valido | Atacar e concluir combate | Batch gravado; replay reproduz alvo, dano e estado final coerente |
| R03 (ok)| Gravacao ActionStack: `buy`/`shopping` | Construcao com compra disponivel | Abrir shopping e comprar unidade | Batch gravado; unidade spawnada no replay no hex correto |
| R04 (ok) | Gravacao ActionStack: `capture` | Unidade em construcao capturavel | Executar captura | Batch gravado; dono da construcao muda no replay |
| R05 (ok) | Gravacao ActionStack: `embark`/`disembark` | Transporte + passageiro validos | Embarcar e depois desembarcar | Ambos batches gravados; estado de transporte/passageiro consistente |
| R06 (ok) | Gravacao ActionStack: `merge`/`supply` | Unidades elegiveis | Executar fusao e suprimento | Batches gravados; HP/combustivel/municao atualizados corretamente no replay |
--> agora o cursor automatico foi até onde estava o ultimo aviao que caiu, nao o primeiro. e ele ficou preso após a fusão, nao chamou o surpir que veio depois
| R07 (ok) | `StepForward` por snapshot | Replay não pode ser iniciado | Avancar 1 step | Executa 1 batch com animacao; cursor move; camera acompanha |
| R08 (ok) | `StepBackward` por snapshot | Replay em step > 0 | Voltar 1 step | Restaura snapshot correto sem reexecucao animada de batches anteriores |
| R09 (ok) | `Play` automatico | Replay iniciado com multiplos batches | Pressionar Play | Executa sequencia respeitando esperas de animacao e estados intermediarios |
| R10 (ok) | `Pause` | Replay em Play | Pressionar Pause durante execucao | Nao corta no meio do batch; pausa apos batch corrente terminar |
| R11 (ok) | `Stop` | Replay ativo | Pressionar Stop | Sai do replay e retorna ao estado live correto (cursor/FSM/visao) |
| R12 (ok) | Save bloqueado em replay | Replay ativo | Tentar salvar por hotkey e por fluxo de slot | Save cancelado; log `[Save] Bloqueado: replay ativo`; mensagem de dialog exibida |
| R13 (ok) | Load bloqueado em replay | Replay ativo | Tentar load por hotkey e por fluxo de slot | Load cancelado; log `[Load] Bloqueado: replay ativo`; mensagem de dialog exibida |
| R14 | Fechar painel F9 | Replay em Play | Fechar painel com F9 | Replay auto-pausa; mensagem `replay pausado`; sessao continua navegavel |
| R15 | Divergencia `UnitInstanceId` | Forcar mismatch entre snapshot e hex de origem | Executar step da acao divergente | Abort gracioso do batch; warning com esperado/encontrado; dialog de erro; sem crash |
| R16 (ok) | Shopping animado | Replay com compra gravada | Rodar batch de shopping | Cursor navega itens ate indice correto e confirma compra correta |
| R17 | Replay apos Load de save | Save com dados de replay | Carregar save e iniciar replay | Pilha/historico reconstruidos; steps executam na ordem e estado correto |
| R18 (ok) | Watchdog timeout (10s) | Induzir batch travado (ex. desembarque incompleto) | Executar batch travado | Em ~10s aborta com `[Replay] Timeout...`; dialog de erro; replay nao quebra e segue navegavel |
| R19 | Gravacao/Replay de `CommandService` | Cenario com servico de comando disponivel | Executar servico de comando e iniciar replay | Batch reproduz alvos/ganhos/custos sem divergencia |
| R20 | Gravacao/Replay de `RemoveUnit` | Unidade removivel por fluxo de jogo | Executar remocao e iniciar replay | Unidade e removida no mesmo ponto da timeline |
| R21 | Sensor `Transfer` | Acao de transferencia valida | Transferir recurso e iniciar replay | Quantidades e ownership finais iguais ao runtime original |
| R22 (ok) | Sensor `Land` | Aeronave com condicao de pouso | Executar pouso e iniciar replay | Layer/estado final da aeronave identico ao original |
| R23 (ok) | Replay com record sem batches | Historico presente com record vazio | Iniciar replay e pressionar `StepForward` | Nao crasha; step permanece indisponivel |
| R24 (ok) | `StepForward` no fim da timeline | Replay no ultimo batch | Pressionar `StepForward` | Retorna sem avanco e sem efeitos colaterais |
| R25 | `StepBackward` no inicio (`-1`) | Replay em `currentStepIndex = -1` | Pressionar `StepBackward` | Retorna sem regressao de estado e sem erro |
| R26 | Start mode `FromSpecificTurnTeam` valido | Historico multi-turn/team | Iniciar replay em turno/time especifico existente | Snapshot inicial e selecao de turno/time corretos |
| R27 | Start mode `FromSpecificTurnTeam` invalido | Turn/time inexistente | Iniciar replay com alvo invalido | Falha graciosa de start; UI continua utilizavel |
| R28 | Troca de visao durante replay | Replay iniciado | Alternar visao `Omniscient` e `TeamFiltered` | Filtro visual muda corretamente sem reiniciar sessao |
| R29 | Gating de concorrencia (`isBusy`) | Replay executando batch | Spam de `Play/Pause/Step` durante execucao | Sem crash/dupla execucao; comandos invalidos ignorados |
| R30 | Corrida sensor vs fallback neutral | Batch de movimento com sensor | Forcar neutral antes de `OnSensorsReady` | Fallback conclui batch sem deadlock |
| R31 | Shopping com indice/tipo divergente | Batch com metadado de shopping inconsistente | Reproduzir compra com indice/tipo invalido | Warning de mismatch; fallback de confirmacao; sessao continua |
| R32 (ok) | Retorno ao snapshot live apos `Stop` | Replay iniciado a partir de jogo live | Executar alguns steps e pressionar `Stop` | Retorna ao snapshot pre-replay correto |
| R33 | Retomada de gravacao pos-abort | Replay abortado por timeout/mismatch | Executar nova acao no live apos abort | Gravacao volta a funcionar e novo batch e persistido |
| R34 | Observabilidade de listeners/logs | Logs verbosos habilitados | Rodar 1 batch de unidade e 1 de shopping | Logs `subscribe/received/unsubscribe` e transicoes aparecem em ordem |
| R35 | Dialog IDs por transicao de estado | Replay panel ativo | `Start`, `Play`, `Pause`, `Stop` e erro | Dialog IDs esperados sao exibidos para cada transicao |
| R36 (ok) | Aeronaves Caindo | Replay panel ativo | `Processar fila de aeronaves caindo no replay' | Dialog IDs esperados sao exibidos para cada transicao |

**Criterios de aceite**
1. Nenhum cenario acima gera crash, freeze permanente ou soft-lock.
2. Todas as mensagens de hardening aparecem no `panel_dialog` correto.
3. `Pause`, `Stop`, `StepForward`, `StepBackward` permanecem funcionais apos erro/timeout.
4. Save/Load voltam a funcionar normalmente assim que replay nao estiver ativo.
5. Watchdog aborta apenas batch travado e nao corrompe a sessao de replay.


--------
Roteiro
Perfeito, li seu checklist. Aqui vai um guia direto dos cenários “chatos” de simular.

Antes de tudo:
1. `F9` abre/fecha Replay.
2. Debug panel abre com `'` ou `;` ou `` ` ``.
3. Save/Load rápido está em `I` (save) e `O` (load).

IDs que você marcou e como simular:

1. `R14 (Fechar painel F9)`  
Inicie replay + `Play`, depois aperte `F9` para fechar. Esperado: pausa automática + dialog `replay pausado`.

2. `R15 (Divergência UnitInstanceId)`  
Faça uma ação gravada (ex.: move+sensor), inicie replay do começo e pause antes do step dessa ação.  
Abra debug e rode `destroy unit` em cima da unidade de origem esperada.  
Agora `StepForward`: deve abortar com warning de mismatch e dialog de erro.

3. `R17 (Replay após Load)`  
Com histórico já gravado, faça `I` para salvar.  
Depois `O` para carregar o slot.  
Abra Replay e rode `Start` + `StepForward/Play`. Deve reconstruir histórico e executar na ordem.

4. `R19 (CommandService)`  
No estado `Neutral`, use `X` para abrir Serviço do Comando e confirme com `Enter`.  
Depois rode replay e valide batch de `CommandService`.

5. `R20 (RemoveUnit)`  
No estado `Neutral`, com unidade sob cursor, use `U` e confirme com `Enter`.  
Isso grava `RemoveUnit`; valide no replay.

6. `R21 (Transfer)`  
Faça movimento, depois `T` no scanner prompt, confirme transferência.  
Rode replay e compare quantidades/ownership finais.

7. `R25 (StepBackward no início)`  
Inicie replay do começo (`Start` em modo beginning), sem avançar nenhum step.  
Clique `StepBackward`. Deve ignorar sem erro.

8. `R26 (FromSpecificTurnTeam válido)`  
No painel replay, mude Start Mode para `FromSpecificTurnTeam`, escolha turno/time existente e `Start`.  
Tem que abrir no snapshot/time corretos.

9. `R27 (FromSpecificTurnTeam inválido)`  
Mesmo fluxo, mas use turno inexistente (ex.: 99) ou combinação turn/team que não existe.  
`Start` deve falhar sem travar UI.

10. `R28 (Troca de visão durante replay)`  
Com replay ativo, alterne visualização (omnisciente / team filtered) no painel.  
Não deve reiniciar sessão, só trocar filtro.

11. `R29 (Gating de concorrência isBusy)`  
Durante execução de batch (animação rolando), spame `Play/Pause/Step`.  
Esperado: sem dupla execução/crash, inputs inválidos são ignorados.

12. `R31 (Shopping índice/tipo divergente)`  
Mais confiável via save editado: altere no replay salvo `ShoppingSelectedIndex` ou `ShoppingUnitTypeId` para inconsistente, carregue e rode replay.  
Esperado: warning de mismatch + fallback de confirmação, sem quebrar sessão.

13. `R33 (Retomada gravação pós-abort)`  
Use `R15` para forçar abort do batch, dê `Stop`, volte ao live e faça nova ação normal.  
Confira que novo batch foi gravado (timeline aumenta).

14. `R34 (Observabilidade logs/listeners)`  
No `ReplayManager` deixe logs ligados e rode 1 batch de unidade + 1 de shopping.  
Procure logs `subscribe/received/unsubscribe` e transições em ordem.

15. `R35 (Dialog IDs por transição)`  
Valide IDs em Start/Play/Pause/Stop/erro:  
`dialog.replay.loading`, `dialog.replay.autoplay_on`, `dialog.replay.autoplay_off`, `dialog.replay.paused`, `dialog.replay.ending_wait`, `dialog.replay.error`.

Observação importante sobre seu `R18`: no código atual eu não encontrei watchdog de batch em `~10s`; o timeout explícito que vi é de startup/neutral em `15s` em [ReplayManager.cs](D:\Unity Projects\The Map Room\Assets\Scripts\Replay\ReplayManager.cs:2579).  
Se quiser, eu te monto um “roteiro executável” só dos pendentes (R14, R15, R17...R35) já em formato de checklist pronto para você marcar.