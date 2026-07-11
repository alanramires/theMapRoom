# v4.0.29c - Tutorial para novatos em andamento

## Foco

Fechamento da Historia 1 de ponta a ponta: o contato com o inimigo ficou 100% dirigido pelo roteiro
(marcha scriptada sem AIController na cena), o passo do ataque ganhou interceptacao do Mirar com trava
propria, e o tutorial agora TERMINA — objetivo ENDING completado por comando de roteiro e Panel_vitoria
oficial no final.

## Marcha scriptada do inimigo (statCommand `move`)

- Novo verbo no roteiro: `slot1 SD move 7,-2 4,-2` — move uma unidade do slot pelo executor REAL de
  batches (mesmo `ReplayManager.ExecuteLiveAIBatch` da IA oficial).
- Causa raiz do "soldado nao anda": o batch dependia de `FindAnyObjectByType<AIController>()` e cenas
  de tutorial nao tem AIController — saida silenciosa por `yield break`. O TutorialManager agora monta
  o `PlayerAction` (formato identico ao `BuildMoveBatch`) e fala direto com o ReplayManager; as duas
  saidas silenciosas viraram `LogWarning`.
- Prioridade de comando: a rotina de turno do automata espera o comando scriptado concluir
  (`automataCommandInProgress`, timeout 20s) antes de decidir pelas unidades — sem disputa pela mesma
  unidade (a rotina confirmava o soldado parado no meio da marcha).
- Passar a vez fica bloqueado enquanto um comando automata esta em andamento.

## Interceptacao do Mirar (step do ataque)

- Novo evento `TurnStateManager.OnUnitAimOpened`: dispara quando o JOGADOR abre o comando de ataque
  (tecla A ou clique; o automata e filtrado por `automatedSelection`).
- Nova condicao de avanco por fala: `Aim Opened (Mirar)` — a fala "Abra o comando de ataque" avanca
  sozinha assim que o jogador mira. Gate edge-triggered (Mirar anterior a fala nao conta) e restrito
  ao turno do jogador.
- Novo 4o nivel do `movement`: **Attack Only** — manter posicao/atacar parado SIM, sair da celula NAO,
  e finalizar parado ("apenas mover"/M) leva bronca nova (`scoldAttackOrder`: "A ordem e MIRAR,
  recruta!"). Sem isso o jogador podia queimar a acao do turno sem atirar. Estado persistente: cancelar
  o Mirar no meio das falas seguintes nao abre brecha.

## Fim de tutorial scriptado

- Objetivo novo `ENDING` (`hist_1_08`, "Voce aprendeu o basico") revelado na fala "Resultado registrado".
- Novo verbo no roteiro: `complete <key>` — completa um objetivo por key (beep, check na task list,
  `OnObjectiveCompleted`, `CheckTutorialCompletion`). Fala muda final executa `complete hist_1_08`
  depois do "Paga 20!" → vitoria.
- Isso mata a corrida antiga vitoria vs falas finais: "todos os objetivos completos" so acontece no
  momento exato que o roteiro manda — o Sargento termina o sermao e ENTAO o tutorial fecha.

## Panel_vitoria no tutorial

- `DeclareTutorialVictory` era legado: procurava um `Panel_endGame` inexistente e empurrava texto no
  panel_dialog — nada aparecia.
- `ShowVictoryPanel(titulo, cor, descricao)` virou ponto unico no MatchController (partida normal e
  tutorial); painel ausente na cena agora gera warning em vez de falha muda.
- Vitoria do tutorial usa a apresentacao oficial: "VITORIA!" na cor do time do JOGADOR (slot 0, nao
  mais `activeTeamId` cru) e descricao "TIME <cor> — TREINAMENTO CONCLUIDO" (customizavel pelo
  `victoryDialog.message` do TutorialData).

## Hold position validado no fim do pipeline

- `OnUnitHeldPosition` migrou do confirm da propria celula (entrada do MoveuParado) para a FINALIZACAO
  da acao — clicar "Manter posicao" so completa a tarefa quando a unidade termina a acao de fato,
  respeitando a FSM (unit selected → mover parado/andando → sensores → movimento → fim).

## Roteiro da Historia 1 (asset)

- Sequencia do contato consolidada: spawn `slot1 SD 7,-2 acted` + pan no turno inimigo (fala muda) →
  reacao do Sargento com passar a vez travado → ordem de segurar o morro (`Hold Only`) → fala muda com
  `move 7,-2 4,-2` no turno inimigo seguinte → "Contato a frente" com `Aim Opened` + `Attack Only` →
  tiro → ENDING → vitoria.
- Restauracao pos-colisao editor×disco: edicoes de asset em disco com o inspector aberto na Unity
  descartam o que so existia em memoria. Protocolo novo: salvar na Unity antes de pedir edicao, e
  deixar a Unity reimportar antes de voltar ao inspector.

## Estado

- Historia 1 completa de ponta a ponta no asset: abertura, camera, barras, inspecao, selecao, marcha
  em 2 turnos, hold, contato dirigido, tiro e fechamento com vitoria.
- Pendencias: playtest integral, vozes do Sargento, retrato de bronca opcional, conferir Panel_vitoria
  presente na cena da Historia 1.
