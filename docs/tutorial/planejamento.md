# Planejamento dos tutoriais

## Direção geral

Em vez de criar uma cena para cada tarefa pequena, a proposta é trabalhar com **cinco cenas**, cada uma cobrindo um conjunto coerente de mecânicas.

As tarefas aparecem progressivamente por meio do `TutorialData`. O jogador conclui uma etapa, recebe a próxima e permanece no mesmo mapa. Isso reduz a quantidade de cenas para manter e evita carregamentos constantes.

Cada cena deve:

- introduzir poucas mecânicas novas;
- reutilizar as mecânicas aprendidas anteriormente;
- permitir experimentação sem punição imediata;
- terminar com uma situação curta que combine as tarefas ensinadas;
- usar eventos reais do jogo para validar objetivos, evitando scripts específicos sempre que possível.

## Cena 1 — Fundamentos

Objetivo: ensinar a interação básica com unidades e o mapa.

Possíveis tarefas:

1. Selecionar uma unidade.
2. Inspecionar seus atributos.
3. Mover para um hex indicado.
4. Comparar movimento em terreno aberto e terreno difícil.
5. Usar `MANTER POSIÇÃO`.
6. Escolher um alvo.
7. Confirmar e executar um ataque.
8. Encerrar o turno.

Situação final sugerida: atravessar um pequeno trecho com dois tipos de terreno e derrotar um inimigo simples.

Base existente: `História 1 - Aprendendo a Atirar`.

## Cena 2 — Armas e proteção

Objetivo: mostrar que posição, alcance e arma adequada mudam o resultado do combate.

Possíveis tarefas:

1. Atacar infantaria em terreno aberto.
2. Atacar uma unidade protegida por montanha ou floresta.
3. Comparar combate corporal e ataque à distância.
4. Identificar alcance mínimo e máximo das armas.
5. Escolher uma arma adequada contra um veículo.
6. Usar linha de visão e posição elevada.
7. Destruir um APC com a composição disponível.

Situação final sugerida: defender uma posição contra infantaria e um veículo leve.

Base existente: `História 2 - A Arma certa`.

## Cena 3 — Operações

Objetivo: ensinar compra, transporte, captura e retorno à base.

Possíveis tarefas:

1. Voltar ao HQ.
2. Comprar um APC.
3. Encontrar uma unidade isolada.
4. Aproximar o transportador.
5. Embarcar a unidade.
6. Cruzar uma área perigosa.
7. Desembarcar em um hex válido.
8. Capturar uma construção.
9. Retornar com a unidade sobrevivente.

Situação final sugerida: resgatar Ryan, eliminar a guarda do caminho e levá-lo em segurança ao objetivo.

Base existente: `História 3 - Resgate Off Road`.

## Cena 4 — Logística

Objetivo: ensinar autonomia, estradas, suprimento e Serviço do Comando.

Possíveis tarefas:

1. Identificar uma unidade com pouca autonomia.
2. Levar um caminhão de suprimentos ao ponto de encontro.
3. Reabastecer uma unidade.
4. Usar estrada para aproveitar o bônus de movimento.
5. Transferir ou distribuir suprimentos.
6. Usar o Serviço do Comando.
7. Manter caminhão e unidade escoltada vivos.
8. Alcançar a área de defesa antes do inimigo.

Situação final sugerida: abastecer o APC e conduzir o grupo até Ramelle sob fogo de obuses.

Base existente: `História 4 - Sem Combustível`.

A AI Easy pode substituir o Automata antigo dos obuses. O comportamento deve ser previsível o suficiente para ensinar, mas continuar usando as regras reais de sensores, alcance e combate.

## Cena 5 — Batalha guiada

Objetivo: combinar os sistemas anteriores em uma partida curta contra a AI.

Possíveis tarefas:

1. Explorar sem conhecer a posição inimiga.
2. Entender a nevoa e os sensores.
3. Detectar uma unidade escondida.
4. Usar um observador avançado.
5. Atacar uma ameaça fora da visão direta da artilharia.
6. Defender uma ponte ou construção estratégica.
7. Comprar reforços com recursos limitados.
8. Capturar ou manter o objetivo até a vitória.

Situação final sugerida: defender a ponte enquanto a AI Easy monta sua força gradualmente.

Base existente: `História 5 - Defenda a Ponte`.

## Papel da AI

- Cenas 1 e 2 podem usar inimigos estáticos ou ações altamente controladas.
- Cena 3 pode usar pouca AI, limitada à guarda e reação local.
- Cena 4 deve introduzir pressão à distância com AI Easy.
- Cena 5 deve usar o turno completo da AI Easy.

Quando o comportamento precisa ser didático, é preferível limitar objetivos, unidades disponíveis e espaço do mapa em vez de criar uma segunda lógica artificial exclusiva para tutorial.

## Estrutura técnica (implementada — 09/07/2026)

A base proposta virou sistema. O que existe hoje:

### TutorialData (asset por História)
- `objectives`: tarefas com `id` = **tipo de evento** (`UNIT_AT_HEX`, `END_TURN`...) e `key` =
  **identidade única** no padrão `hist_Y_XX` (ex.: `hist_1_04`). Gates/reveals referenciam a key —
  inserir tarefa no meio não renumera nada.
- `script`: roteiro do panel_dialog_tutorial. Cada fala tem `text` (com markup), `voice` (AudioClip),
  `waitObjectiveKey` (gate: só aparece quando a tarefa completa), `revealObjectiveKey` (a ordem do
  Sargento faz a tarefa pingar na task list), `spawnCommand`, `statCommand`, `unlockEndTurn`.
- Comandos declarativos nas falas:
  - `spawnCommand`: `slot0 SD 1,3 name=Ryan cursor` — slot lógico (respeita escolha de cor), `acted`
    (nasce "já agiu"), `name=` (renomeia), `cursor` (cursor desliza até a unidade); done.mp3 por lote.
  - `statCommand`: `Mathias hp=4; Dias fuel=15; Dias ammo=0` — demonstrações vivas das barras.
- Bloqueios por cena: `blockCommandService` (X), `blockRemoveUnit` (U), `blockSurrender`,
  `blockStatusSummary` — recusa vira **bronca do Sargento** no balão + error.mp3.
- Markup nas falas: `[ordem]` (amarelo+negrito = o que fazer), `[enfase]` (laranja = o que fixar),
  `[azul]/[amarelo]/[vermelho]` (apontar elementos da UI).

### TutorialManager
- Escuta eventos reais (fonte de verdade = sensores/TurnStateManager). Validações disponíveis:
  `CAMERA_ZOOM`, `CAMERA_PAN` (com célula alvo), `INSPECT_ALLY_UNIT` / `INSPECT_ENEMY_UNIT`,
  `UNIT_SELECTED`, `UNIT_AT_HEX`, `HOLD_POSITION` (novo evento `OnUnitHeldPosition`), `END_TURN`,
  `ATTACK_UNIT` (+ variantes de terreno), `PURCHASE_UNIT`, `HAS_EMBARKED_UNIT`, `SUPPLY_UNIT`,
  `USED_ROAD_BOOST`, `UNIT_DEAD` (com `AUT=`), `FOW_REVEAL_UNIT`, `DESTROY_ENEMY_UNIT`.
- Task list dirigida pelo roteiro: com qualquer `reveal` no script, o painel começa vazio
  ("Aguardando próximo objetivo...") e as tarefas aparecem quando o Sargento ordena.
- Trava de passar a vez (`unlockEndTurn`) cobrindo R, botão do panel_remaining e menu.
- Automata ganhou marcha: `AutomataData.moveTowardsTarget/moveTargetCell/stopDistance` — avança com
  custos reais de terreno e para adjacente ao alvo (ex.: inimigo marcha da estrada até o morro).
  `teamId: Neutral` no AutomataData = curinga (compatível com escolha de cor).

### panel_dialog_tutorial (PanelDialogTutorialController)
- Retrato do Sargento + balão center-left, Avançar/Voltar (confirm/cancel.mp3), histórico navegável,
  voz por fala, esconde nos gates e reaparece sozinho quando a tarefa completa.
- Bronca transiente para ações bloqueadas (o painel aparece, xinga e some) com retrato de bronca
  opcional (`Scold Portrait Sprite`).

### Menu e cena
- Tela de Entrada: painel Tutorial com as 5 Histórias (2–5 desabilitadas por `historiasLiberadas = 1`),
  passo "ESCOLHA SUA COR" antes de carregar (recolore o slot 0; requer unidades da cena com slotIndex).
- Painel de tarefas é prefab (`Assets/Prefab/Panel_tutorial.prefab`) — precisa estar DENTRO do Canvas.
- Inspect coerente com classificação: `Civil` = inspect básico (quase terreno); militar sem munição =
  ficha + raio de movimento, sem camada de mira nem segundo clique.
- `TutorialRules` segue reservado para exceções (a regra de reset de HP do tutorial antigo está inerte).

## Pontos para conversa (respostas até aqui)

- **Bloquear ou orientar?** Orientar como regra; travas pontuais e narradas (passar a vez até a ordem;
  X/U/render/situação a cena toda) com bronca do Sargento — o bloqueio faz parte da encenação.
- **Falhar e continuar?** Experimentação livre; `isDefeatCondition` reservado para o que quebra a
  narrativa (ex.: Ryan morrer nas cenas seguintes).
- **Campanha ou avulsas?** Desbloqueio sequencial, rejogáveis. Por ora `historiasLiberadas` no
  inspector; progressão salva (PlayerPrefs) fica para quando a Cena 2 existir.
- **Ryan/Ramelle?** Mantidos — e ampliados: recrutas Mathias e Dias viraram personagens de demonstração.
- **Diálogo vs panel_helper?** Diálogo para conceito novo e narrativa; task list para o que fazer;
  panel_helper segue com as confirmações padrão do jogo.
- **Cena 5 termina como?** Pelas regras normais de vitória (decisão mantida).
- **Progresso salvo?** Ainda aberto (ver campanha acima).

## Estado das cenas

| Cena | Status |
|------|--------|
| 1 — Aprendendo a Atirar | Roteiro completo no asset; mapa novo; faltam coordenadas finais (montanha, spawn do inimigo), entrada no AutomataDatabase, vozes. Ver `cena1.md`. |
| 2 a 5 | Aguardando fechamento da Cena 1. Cenas antigas serão refeitas com o sistema novo (keys, reveals, spawns declarativos). |

## Próximo passo

Fechar as pendências da Cena 1 (lista no fim de `cena1.md`) e rodar o primeiro playtest completo
de ponta a ponta. Depois: sessão com a sobrinha, e só então Cena 2.

