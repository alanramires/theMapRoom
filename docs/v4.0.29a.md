# v4.0.29a - Tutorial para novatos em andamento

## Foco

Ciclo grande do motor de tutorial: o roteiro da Historia 1 saiu do papel e virou sistema declarativo completo — falas com gates, comandos de cena, travas didaticas com bronca do Sargento e validacoes por eventos reais do jogo.

## Motor de roteiro (TutorialData)

- Roteiro (`script`) dentro do TutorialData: falas do Sargento com texto, voz (AudioClip), gates e comandos.
- Objetivos com `key` unica no padrao `hist_Y_XX` (identidade) separada do `id` (tipo de evento) — inserir tarefa no meio nao renumera nada; gates/reveals referenciam a key.
- Task list dirigida pelo roteiro: painel comeca vazio ("Aguardando proximo objetivo...") e cada ordem do Sargento revela a tarefa (`revealObjectiveKey`).
- Gates de fala: por objetivo (`waitObjectiveKey`), por "todas as unidades do jogador agiram" (`waitAllUnitsActed`) e por "novo turno do jogador comecou" (`waitPlayerTurnStart`, edge-triggered).
- Auto-avanco: quando o jogador cumpre a ordem da fala atual, o Sargento segue sozinho — com o balao visivel ou escondido no gate.
- Comandos declarativos por fala:
  - `spawnCommand`: `slot0 SD 1,3 name=Ryan cursor` — slot logico (respeita escolha de cor), `acted`, `name=`, `cursor`; done.mp3 por lote.
  - `statCommand`: `hp/fuel/ammo=valor` (demonstracoes vivas das barras), `wake` (reativa unidade), `show/hide` (isVisible de construcao, ex.: Bandeira), `pan` (desliza SO a camera ate celula/unidade/construcao, cursor intocado).
  - `unlockEndTurn` / `unlockMovement`: destraves narrativos (ordem de marcha, autorizacao de passar a vez).

## panel_dialog_tutorial

- Painel do Sargento (retrato + balao, center-left) com Avancar (confirm.mp3) / Voltar (cancel.mp3), historico navegavel e voz por fala.
- Markup leve nas falas: `[ordem]` (amarelo+negrito = fazer), `[enfase]` (laranja = fixar), `[azul]/[amarelo]/[vermelho]` (apontar UI).
- Broncas do Sargento para acoes bloqueadas: texto e voz configuraveis por tutorial (secao "Broncas"), balao segura ate o audio terminar, retrato de bronca opcional.

## Validacoes e eventos novos

- `CAMERA_ZOOM` (orthographicSize saiu do baseline) e `CAMERA_PAN` (camera perto de celula alvo).
- `INSPECT_ALLY_UNIT` (inspecao aliada) e `HOLD_POSITION` (novo evento `OnUnitHeldPosition` no confirm da propria celula).
- `UNIT_AT_HEX` aceita nome de construcao (`SD && Bandeira`) e valida no FIM da acao (HasActed na celula) — rollback nao completa mais a tarefa; poll cobre mover, mover+atacar, apenas mover e desembarque.
- Spawn de objetivos com slot logico (`spawn:slot1 SD x,y`), opcoes `acted`/`name=`/`cursor`.

## Travas didaticas

- Passar a vez travado ate a autorizacao (R, botao flutuante e menu), movimento travado ate a ordem de marcha (mover, manter posicao e atalho M) — selecionar e ver alcance continua livre.
- Reabastecer (X), dispensar unidade (U), render-se e Situacao bloqueaveis por cena inteira (flags no TutorialData).
- Atalho contextual desligado automaticamente no inicio de aulas com roteiro (religavel nas preferencias).
- Todas as recusas com bronca do Sargento + error.mp3; travas valem so no turno do jogador (automata inimigo ileso).

## Automata e cena

- AutomataData ganhou marcha: `moveTowardsTarget/moveTargetCell/stopDistance` — avanca com custos reais e para adjacente ao alvo; `teamId Neutral` = curinga.
- Rotina do automata devolve o turno mesmo sem AutomataDatabase (time inimigo vazio nao pendura mais a partida).
- Figurantes (`alwaysActedUnits`): Mathias e Dias amanhecem "ja agiram" todo turno do jogador.
- Inspect coerente com classificacao: `Civil` = inspect basico; militar sem municao = ficha + raio de movimento, sem camada de mira nem segundo clique.

## Historia 1

- Roteiro completo no asset (abertura com spawns nomeados, zoom/pan/inspecao, demonstracoes de hp/fuel/ammo com Mathias e Dias, marcha em 2 turnos com destrave no meio, manter posicao, contato e primeiro tiro).
- Marcha desenhada para 2 turnos: o passar a vez nasce de necessidade real; fala de retomada aparece na virada do turno.
- Bandeira revelada e focada pela camera no momento certo (`show Bandeira; pan Bandeira`).
- Menu da Tela de Entrada: painel Tutorial com as 5 Historias (2-5 bloqueadas por enquanto) e passo "ESCOLHA SUA COR".
- Documentacao viva em `docs/tutorial/planejamento.md` e `docs/tutorial/cena1.md` (status e pendencias no fim).

## Estado

- Tutorial para novatos segue em andamento.
- Pendencias da Historia 1 para o playtest completo: celula do spawn inimigo na estrada, entrada do soldado no AutomataDatabase, vozes do Sargento.
