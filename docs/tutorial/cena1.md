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

<o sistema espera o jogador mover Ryan até o hex indicado>

Ao chegar:

Sargento:
Bom. Você chegou.

Não foi bonito, mas foi útil.

Sargento:
Sua unidade já agiu neste turno.
Quem age, espera. Quem ainda não agiu, recebe ordem.
[ordem]Encerre o turno, recruta.[/ordem]

<esta fala destrava o passar a vez (unlockEndTurn) e revela `hist_1_06 END_TURN`>
<o jogador passa a vez; o turno inimigo (vazio) retorna sozinho via automata>

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
