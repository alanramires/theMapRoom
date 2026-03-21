**Checklist QA — Replay + Hardening (Unity)**

| ID | Cenário | Pré-condição | Ação | Resultado esperado |
|---|---|---|---|---|
| R01 (OK)| Gravação ActionStack: `move` | Partida ativa, unidade móvel selecionável | Mover unidade e finalizar ação | 1 batch gravado; replay reproduz exatamente célula origem/destino |
| R02 (OK) | Gravação ActionStack: `attack` | Unidade com alvo válido | Atacar e concluir combate | Batch gravado; replay reproduz alvo, dano e estado final coerente |
| R03 | Gravação ActionStack: `buy`/`shopping` | Construção com compra disponível | Abrir shopping e comprar unidade | Batch gravado; unidade spawnada no replay no hex correto |
| R04 | Gravação ActionStack: `capture` | Unidade em construção capturável | Executar captura | Batch gravado; dono da construção muda no replay |
| R05 | Gravação ActionStack: `embark`/`disembark` | Transporte + passageiro válidos | Embarcar e depois desembarcar | Ambos batches gravados; estado de transporte/passageiro consistente |
| R06 | Gravação ActionStack: `merge`/`supply` | Unidades elegíveis | Executar fusão e suprimento | Batches gravados; HP/combustível/munição atualizados corretamente no replay |
| R07 | `StepForward` animado | Replay iniciado e pausado | Avançar 1 step | Executa 1 batch com animação; cursor move; câmera acompanha |
| R08 | `StepBackward` por snapshot | Replay em step > 0 | Voltar 1 step | Restaura snapshot correto sem reexecução animada de batches anteriores |
| R09 | `Play` automático | Replay iniciado com múltiplos batches | Pressionar Play | Executa sequência respeitando esperas de animação e estados intermediários |
| R10 | `Pause` | Replay em Play | Pressionar Pause durante execução | Não corta no meio do batch; pausa após batch corrente terminar |
| R11 | `Stop` | Replay ativo | Pressionar Stop | Sai do replay e retorna ao estado live correto (cursor/FSM/visão) |
| R12 | Save bloqueado em replay | Replay ativo | Tentar salvar por hotkey e por fluxo de slot | Save cancelado; log `[Save] Bloqueado: replay ativo`; mensagem de dialog exibida |
| R13 | Load bloqueado em replay | Replay ativo | Tentar load por hotkey e por fluxo de slot | Load cancelado; log `[Load] Bloqueado: replay ativo`; mensagem de dialog exibida |
| R14 | Fechar painel F9 | Replay em Play | Fechar painel com F9 | Replay auto-pausa; mensagem `replay pausado`; sessão continua navegável |
| R15 | Divergência `UnitInstanceId` | Forçar mismatch entre snapshot e hex de origem | Executar step da ação divergente | Abort gracioso do batch; warning com esperado/encontrado; dialog de erro; sem crash |
| R16 | Shopping animado | Replay com compra gravada | Rodar batch de shopping | Cursor navega itens até índice correto e confirma compra correta |
| R17 | Replay após Load de save | Save com dados de replay | Carregar save e iniciar replay | Pilha/histórico reconstruídos; steps executam na ordem e estado correto |
| R18 | Watchdog timeout (10s) | Induzir batch travado (ex. desembarque incompleto) | Executar batch travado | Em ~10s aborta com `[Replay] Timeout...`; dialog de erro; replay não quebra e segue navegável |

**Critérios de aceite**
1. Nenhum cenário acima gera crash, freeze permanente ou soft-lock.
2. Todas as mensagens de hardening aparecem no `panel_dialog` correto.
3. `Pause`, `Stop`, `StepForward`, `StepBackward` permanecem funcionais após erro/timeout.
4. Save/Load voltam a funcionar normalmente assim que replay não estiver ativo.
5. Watchdog aborta apenas batch travado e não corrompe a sessão de replay.