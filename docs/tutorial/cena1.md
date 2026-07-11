# História 1 — Aprendendo a Atirar

> **Fonte de verdade em jogo:** `Assets/DB/Tutorial/Tutorial Data/História 1 - Aprendendo a Atirar.asset`
> (falas, gates, spawns e demonstrações vivem no asset — editar texto lá, não aqui).
> Este doc é o roteiro de referência. Status de implementação no fim do arquivo.

## Abertura

Mapa começa vazio (sem unidades). Painel de tarefas aberto e vazio ("Aguardando próximo objetivo...").
Passar a vez travado (R, botão flutuante e menu) até o Sargento autorizar no Step "Encerrar turno".
Reabastecer (X), dispensar unidade (U), render-se e Situação travados a cena inteira — tentar dispara
bronca do Sargento no próprio balão + error.mp3.

Sargento:
Bem-vindo, recruta. Já encheu o bucho com a comida da caserna e tomou o chá brochante do tonel de óleo diesel?

<ao avançar: spawn do Ryan em 1,3 (`slot0 SD 1,3 name=Ryan cursor`) — toca done.mp3 e o cursor desliza até ele>

Sargento:
Recruta Ryan, certo? Muito bem. Você é o número 17. Em fila, soldado.

<ao avançar: spawn dos recrutas **Mathias** (0,3) e **Dias** (2,3) com `acted` — nascem "já agiram". A barra de passar turno permanece desativada.>

Sargento:
Esses dois aí já cumpriram a parte deles. Agora é você.

Enquanto eu estiver falando, ninguém passa turno, ninguém dá uma de turista e ninguém aperta botão por curiosidade. Entendido?

---

## Step 1 — Conhecer a unidade (câmera + barras + inspeção)

Objetivos do sistema (validados por eventos, revelados na task list quando o Sargento dá cada ordem):
1. `hist_1_01 CAMERA_ZOOM` — aproximar o zoom (mudança do orthographicSize, bolinha ou pinça).
2. `hist_1_02 CAMERA_PAN` — arrastar a câmera até o Ryan (dedo/botão direito; câmera perto da célula 1,3).
3. `hist_1_03 INSPECT_ALLY_UNIT` — inspecionar um aliado (evento real de inspeção).

Sargento:
Antes de puxar o gatilho, primeiro é preciso conhecer a si mesmo.

Meu Velho falava algo nessa linha, mas ele provavelmente não precisava lidar com recruta sonolento.

Olhe bem para você e seus companheiros.
[ordem]Aproxime o zoom usando a bolinha do mouse ou o toque do celular.[/ordem]

<gate: espera CAMERA_ZOOM completar>

Sargento:
Essa informação é persistente. Ela mostra os dados importantes do combatente.

Está vendo o coração?

Ele indica sua capacidade de luta.
Quando está cheio, o esquadrão combate com força total.
Quando esvazia, sua força de tiro cai junto.
[ordem]Arraste a tela com o botão direito do mouse ou com o toque.[/ordem]

<gate: espera CAMERA_PAN completar>

Sargento:
A sua figura em campo não representa um soldado sozinho.
Ela representa 10 soldados lutando juntos no mesmo setor.

Menos soldados atirando, menos chumbo indo para o outro lado.

<demonstração viva: `Mathias hp=4`>

Sargento:
Uma unidade ferida luta com menos força, entendido?
Mathias ainda luta, mas ferido desse jeito não vai conseguir fazer as coisas direito.

Sargento:
Agora olhe a barra laranja.

Essa é sua autonomia, sua capacidade de marcha.
Se isso zerar, você não morre, mas fica estacionário no campo, feito poste com capacete.

<demonstração viva: `Mathias fuel=40`>

Sargento:
Quando a autonomia estiver próxima da metade, ela muda pra amarelo. Olha o Mathias.

<demonstração viva: `Dias fuel=15`>

Sargento:
O recruta Dias já tá na capa da gaita pra marchar longas distâncias.
Pelotão faminto não anda.

Sargento:
E essa barra azul é munição.

Se ela zerar, você não atira.
E soldado sem munição vira decoração de cemitério.

Caixão e vela preta.

<demonstração viva: `Mathias ammo=2; Dias ammo=0`>

Sargento:
Olha os cintos agora: Mathias com a munição no fim e o Dias sem nada pra atirar.
Em batalha, preste atenção na munição antes de precisar dela, entendido?

Sargento:
Mas olhar por cima não é conhecer.

[ordem]Inspecione um dos seus companheiros de fila. Abra a ficha dele.[/ordem]

<gate: espera INSPECT_ALLY_UNIT completar>

Nota de sistema: inspecionar o Dias (sem munição) mostra a ficha + só o raio de movimento — sem camada
de mira e sem o segundo clique do inspect ("anda mas não atira"). Unidades `Civil` têm inspect básico.

Sargento:
Essa é a ficha completa do combatente: arma, alcance, capacidade de marcha. Tudo o que ele é está aí. Note que o soldado move 3, e o rifle tem alcance de 1, por isso tem esse simbolo de mira na quarta casa, indica o alcance maximo de onde ele combate em 1 rodada. mas você pode atacar qualquer alvo estando parado. Isso é útil pra vc saber onde se posicionar com segurança em relação ao inimigo que vc está caçando ok?

E repare: esses dois já agiram neste turno, mas a ficha continua aberta para leitura.
Informação não tira folga, recruta.

Sargento:
Muito bem. Você já sabe olhar para uma unidade sem babar no mapa.

É um começo.

---

## Step 2 — Selecionar Ryan

Objetivo do sistema: `hist_1_04 UNIT_SELECTED` (parâmetro `SD && (1,3)`) — clicar/tocar no Soldado Ryan.

Sargento:
Agora selecione você mesmo, recruta Ryan.

<o sistema espera o jogador selecionar o Soldado Ryan>

<ao selecionar, aparece a área de movimento>

Sargento:
Está vendo essa área marcada?

Ela mostra até onde você pode ir neste turno.

Você anda 3 casas em terreno normal.
Mas cada movimento consome autonomia.

Marchar cansa, soldado. Até no treinamento de fuga e evasão da AMAN.

---

## Step 3 — Movimento e terreno

Objetivo do sistema: `hist_1_05 UNIT_AT_HEX` — mover Ryan até a bandeira no morro.
(Coordenada da montanha ainda como `0,0` no asset — **preencher com a célula real**.)

<o sistema marca uma bandeira em um hex de montanha ou terreno elevado>

Sargento:
Está vendo aquela bandeira?

Vá até lá.

Mas preste atenção: terreno diferente cobra preço diferente.

Planície é fácil.
Floresta atrasa veiculos, mas não influencia a infantaria. Aqui é CIGS!
Montanha cobra mais da perna.

Para a infantaria, subir montanha custa mais esforço.
Uma casa de montanha consome 2 de autonomia.

É só uma casa no mapa, mas suas pernas discordam.

Sargento:
Agora mova até a bandeira.

Um, dois! Um, dois!
Acelerado, recruta!

Sargento:
A bandeira está longe — suas pernas não chegam lá num turno só.
Marche o que der e, quando a tropa cansar, [ordem]encerre o turno[/ordem].
Amanhã se continua.

<esta fala destrava o passar a vez (unlockEndTurn) e revela `hist_1_06 END_TURN` —
a marcha é desenhada para levar 2 turnos; o gate da chegada atravessa a passagem de turno>

<o sistema espera o jogador mover Ryan até o hex indicado (2 turnos de marcha)>

Ao chegar:

Sargento:
Bom. Você chegou.

Não foi bonito, mas foi útil.

Sargento:
Sua unidade já agiu neste turno.
Quem age, espera. Quem ainda não agiu, recebe ordem.
[ordem]Encerre o turno, recruta.[/ordem]

<orientação pura: o destrave e a tarefa END_TURN já aconteceram no meio da marcha;
este segundo passe não é rastreado como tarefa — o jogador passa a vez guiado pelo diálogo>

---

## Step 4 — Manter posição

Objetivo do sistema: `hist_1_07 HOLD_POSITION` — ensinar que confirmar sem mover também é ação válida
(evento `OnUnitHeldPosition`: MANTER POSIÇÃO ou confirmar na própria célula).

Sargento:
Agora aprenda uma coisa importante: nem toda ordem é avanço.

Às vezes, a melhor decisão é manter posição.
Você já está onde precisa estar. Não invente moda.

Fique aí parado e aprenda: soldado que anda demais morre cansado.
Soldado que segura o ponto obriga o inimigo a pensar.

Confirme sua posição atual.

<o sistema espera o jogador usar MANTER POSIÇÃO ou confirmar sem mover>

Sargento:
Muito bem.

Manter posição não é ficar parado por medo.
É segurar o ponto certo até a hora certa.

Sargento:
Sua unidade segurou o ponto. [ordem]Agora encerre o turno de novo.[/ordem]
Quem age, espera.

<revela `hist_1_08 END_TURN`; ao passar a vez, a próxima fala revela `hist_1_09 ATTACK_UNIT`,
cujo `spawn:slot1 SD x,y` faz o inimigo nascer na estrada (done.mp3) — **célula ainda `0,0` no asset**>

---

## Step 5 — Primeiro alvo

Objetivo do sistema: inimigo spawna na estrada e marcha até a floresta adjacente à montanha
(AutomataData com `moveTowardsTarget` → célula da montanha, `stopDistance 1`, `preferAttack false` —
**entrada no AutomataDatabase ainda pendente**).

Sargento:
Contato à frente e ele ainda não te viu.

Esse alvo está no seu alcance.

Não precisa fazer discurso.
Não precisa odiar o sujeito.
Você só precisa confirmar se sua arma alcança.

Abra o comando de ataque.

<o sistema destaca o botão/atalho de ataque>

<o sistema espera o jogador abrir o comando de ataque>

---

## Step 6 — Escolher alvo

Objetivo do sistema: jogador alternar/selecionar o alvo válido.

Sargento:
O scanner mostrou um alvo válido.

Isso significa que você tem alcance, munição e condição de disparo.

Escolha o alvo.

<o jogador seleciona o inimigo>

Sargento:
Alvo selecionado.

Agora leia antes de confirmar.
Depois do tiro, não existe "foi sem querer".

---

## Step 7 — Confirmar ataque

Objetivo do sistema: `hist_1_09 ATTACK_UNIT` — executar o primeiro combate (valida no ataque resolvido;
Steps 5 e 6 são guiados só por diálogo).

Sargento:
Confirme o ataque.

<jogador confirma>

<combate executa>

Sargento:
Resultado registrado.

Aqui não tem dado escondido, sorte milagrosa ou desculpa de azar.

Se acertou, havia motivo.
Se falhou, também.

Você estava em posição, tinha munição, tinha alcance e recebeu ordem de fogo.

Isso é combate.

---

## Fechamento
Objetivo do sistema: Revisar as lições aprendidas, já que para executar diversas das tarefas, o jogador terá q passar a vez varias vezes.

Sargento:
Primeira lição concluída.

Você aprendeu a olhar uma unidade, inspecionar um companheiro, selecionar, mover, entender terreno, manter posição, escolher alvo, atacar e encerrar o turno.

Ainda não é estratégia.

É alfabetização de comando.

Amanhã a gente descobre se você consegue fazer isso sem eu gritar no seu ouvido.

No chão, recruta! Paga 20!

<toca vitoria.mp3>

---

## Status de implementação (09/07/2026)

### Pronto e funcionando
- **panel_dialog_tutorial** (`PanelDialogTutorialController`): retrato + balão center-left, Avançar
  (confirm.mp3) / Voltar (cancel.mp3) com histórico, voz por fala (campo `voice`), gates por objetivo.
- **Markup nas falas**: `[ordem]` (amarelo+negrito = fazer), `[enfase]` (laranja = fixar),
  `[azul]/[amarelo]/[vermelho]` (cores puras para apontar UI).
- **Task list dirigida pelo roteiro**: painel começa vazio; cada ordem do Sargento revela a tarefa
  (`revealObjectiveKey`). Objetivos identificados por key `hist_1_01..09` — inserir tarefa no meio
  não quebra mais nada.
- **Comandos de fala no asset**: `spawnCommand` (slot0/slot1, `acted`, `name=`, `cursor` + done.mp3),
  `statCommand` (hp/fuel/ammo — demonstrações do Mathias/Dias), `unlockEndTurn`.
- **Eventos/validações novas**: `CAMERA_ZOOM`, `CAMERA_PAN`, `INSPECT_ALLY_UNIT`, `HOLD_POSITION`
  (evento `OnUnitHeldPosition` no confirm da própria célula).
- **Travas com bronca do Sargento** (balão troca texto por ~2,6s + error.mp3; retrato de bronca
  opcional via `Scold Portrait Sprite`): passar a vez até o destrave; Reabastecer (X), dispensar (U),
  render-se e Situação a cena inteira (flags `block*` no TutorialData).
- **Automata com marcha**: `AutomataData.moveTowardsTarget/moveTargetCell/stopDistance` — inimigo anda
  com custos reais e para adjacente ao alvo.
- **Inspect coerente**: `Civil` = inspect básico; militar sem munição = ficha + raio de movimento,
  sem camada de mira nem segundo clique.

### Pendências para o primeiro playtest completo
1. Coordenada da montanha no `UNIT_AT_HEX` (`hist_1_05`) — hoje `0,0`.
2. Célula do spawn do inimigo na estrada (`hist_1_09`, `spawn:slot1 SD 0,0`) — 3–4 hexes do morro
   para a marcha ser visível.
3. Entrada do soldado inimigo no **AutomataDatabase** (`SD`, `teamId Neutral`, História 1,
   `moveTowardsTarget` → célula da montanha, `stopDistance 1`, `preferAttack false`).
4. Gravar as vozes do Sargento e arrastar nos campos `voice`.
5. Sprite de bronca do Sargento (opcional, campo já existe).
6. Conferir limiar de amarelo da barra de autonomia (fala promete amarelo com fuel 40/70).
7. Corrida no fim: vitória dispara no `ATTACK_UNIT` junto com as falas finais — avaliar no playtest
   (opções: mover Fechamento pro victoryDialog ou segurar a vitória até o roteiro acabar).

## Estado consolidado após v4.0.29b (10/07/2026)

Esta seção substitui o status e as pendências antigas acima quando houver divergência. O asset da
História 1 permanece como fonte de verdade para texto, ordem das falas e configuração dos eventos.

### Estrutura atual do roteiro

- A fala que dá uma ordem também declara quando avança. `Advance: Objective Completed` +
  `Objective Key` substitui o antigo gate colocado no Element seguinte.
- `Reveal Objective` usa a mesma key e controla a entrada da tarefa na task list.
- Passar turno usa `Turn: No Effect / Locked / Unlocked`, com estado persistente entre falas.
- Movimento usa `Movement: No Effect / Locked / Hold Only / Unlocked`. Durante `Hold Only`, Ryan
  pode manter posição e atacar parado, mas não pode sair do morro.
- `All Units Acted`, `Player Turn Started` e `Enemy Turn Started` avançam a fala atual.
- Falas mudas executam spawn, pan e mudanças de estado sem abrir o painel do Sargento.

### Sequência implementada do contato

1. Ryan chega à bandeira e recebe ordem para passar a vez.
2. No início do turno inimigo, uma fala muda cria o soldado em `slot1 SD 7,-2 acted`.
3. No turno do jogador, a câmera apresenta o movimento na estrada.
4. Ryan recebe ordem de usar `MANTER POSIÇÃO`; a tarefa de hold é revelada e o movimento entra em
   `Hold Only`.
5. Depois de segurar o morro, o jogador passa a vez para observar o inimigo.
6. No turno da AI, o Automata `Tutorial 1 - Inimigos na Estrada` marcha de `7,-2` até `4,-2`, para
   adjacente ao Ryan e não atira.
7. No turno seguinte, a tarefa de ataque é revelada e Ryan executa o primeiro tiro parado no morro.

Os objetivos ativos foram consolidados em `hist_1_01..hist_1_07`. As antigas tarefas de passar turno
e as keys `hist_1_08/09` foram removidas; passar a vez agora é direção de cena, não uma tarefa.

### Spawn e flip

- Coordenadas do `spawnCommand` são absolutas e nunca são espelhadas pelo código.
- Quando o comando usa `slotN`, a unidade recebe o mesmo `slotIndex` usado pelo Unit Painter.
- A orientação consulta diretamente `MatchController.GetSlotFlipX(slotIndex)` depois de aplicar
  `acted` e `name`, preservando o flip configurado no slot.
- `cursor` move o cursor até a célula original do comando; recrutas sem essa opção não puxam o cursor.

### Inspect usado nesta aula

- A ficha mostra nome, classe em português, HP, movimento e autonomia antes da lista de armas.
- Armas exibem a categoria curta em português, por exemplo `R:1 {anti-inf}`.
- A inspeção temporária tem uma barra regressiva de 6 segundos, na cor do time ativo.
- O segundo clique separa alcance com movimento e alcance parado; unidades sem munição não exibem
  uma camada de tiro inexistente.

### Ajustes posteriores ao commit

- O spawn por `slotN` preserva a coordenada absoluta do comando e aplica o flip diretamente do
  `MatchController` depois de configurar `acted` e o nome da unidade.
- Ryan usa `cursor` no spawn inicial; Mathias e Dias nascem sem puxar o cursor para eles.
- A ficha do inspect mostra a classe traduzida antes de HP e as armas com apelido de categoria,
  como `R:1 {anti-inf}`.
- A inspeção de 6 segundos mostra uma barra que diminui continuamente e usa a cor do time ativo.

### Pendências reais para o próximo playtest

1. Rodar toda a História 1 sem atalhos e validar todos os modos de `Advance`.
2. Confirmar que `Turn` trava e destrava R, panel_remaining e menu nos pontos corretos.
3. Confirmar `Hold Only`: manter posição e atacar parado funcionam; sair da célula recebe bronca.
4. Validar spawn em `7,-2`, flip visual por slot, pan da câmera e marcha da AI até `4,-2`.
5. Avaliar a corrida entre o fechamento do Sargento e a tela de vitória após `ATTACK_UNIT`.
6. Gravar e atribuir as vozes; escolher o retrato de bronca opcional.
7. Fazer o primeiro teste com uma pessoa novata antes de iniciar a História 2.

## Estado consolidado após v4.0.29c (10/07/2026)

Esta seção substitui as anteriores quando houver divergência. A História 1 está completa de ponta a
ponta no asset: abertura → câmera/barras/inspeção → seleção → marcha em 2 turnos → hold → contato
dirigido → tiro → fechamento com vitória.

### Contato e ataque (Steps 4–7, forma final)

1. Ryan chega à bandeira; "Missão dada... passe a vez" avança no **turno do inimigo**.
2. Fala muda no turno da IA: `slot1 SD 7,-2 acted` + `pan 7,-2` — o soldado aparece e não se move.
3. Turno do jogador: Sargento reage ("Espere!", com passar a vez travado), pan até o inimigo.
4. Ordem de segurar o morro: revela `hist_1_06 HOLD_POSITION`, `movement: Hold Only`. O hold valida
   na **finalização** da ação (FSM completa), não no clique de "Manter posição".
5. Após o hold: "Passe a vez e observe" (advance no turno inimigo).
6. Fala muda no turno da IA: `slot1 SD move 7,-2 4,-2` — marcha scriptada pelo executor real de
   batches (ReplayManager direto; cena de tutorial não precisa de AIController). A rotina do automata
   espera o comando concluir antes de mexer nas unidades.
7. Turno do jogador: "Contato à frente" revela `hist_1_07 ATTACK_UNIT` com `advance: Aim Opened`
   (avança quando o jogador abre o Mirar) e `movement: Attack Only` (sair da célula = bronca;
   "apenas mover"/M = bronca "A ordem é MIRAR, recruta!" — não queima a ação sem atirar).
8. Tiro → "Resultado registrado" revela `hist_1_08 ENDING` e destrava o turno → "Primeira lição
   concluída... Paga 20!" → fala muda final com `complete hist_1_08` → vitória.

### Fim de tutorial e vitória

- Objetivo `ENDING` (`hist_1_08`, "Você aprendeu o básico") é completado por comando de roteiro
  (`statCommand: complete <key>`) — o Sargento dá a missão por cumprida; sem corrida com as falas.
- `DeclareTutorialVictory` usa o **Panel_vitoria oficial** (mesmo da partida normal): "VITÓRIA!" na
  cor do time do jogador + "TIME <cor> — TREINAMENTO CONCLUÍDO" (customizável via
  `victoryDialog.message`). Painel ausente na cena gera warning no Console.
- **Conferir que o Panel_vitoria existe na cena da História 1** (pode estar desativado).

### Pendências finais

1. Playtest integral sem atalhos (todos os modos de `Advance`, travas, marcha, vitória).
2. Vozes do Sargento (falas + broncas, incluindo as novas `scoldHoldPosition` e `scoldAttackOrder`).
3. Retrato de bronca opcional.
4. Teste com uma pessoa novata antes da História 2.

### Protocolo de edição do asset

Edição do TutorialData em disco com o inspector aberto na Unity descarta o que só existia em memória
(já perdemos o `move` e um `advance` assim). Antes de pedir edição externa: salvar na Unity; depois
dela: focar a Unity para reimportar antes de voltar ao inspector.
