olha o replay tem q funcionar assim, baseado em pilha

snapshot#0: jogo carregado ou novo jogo aberto, guarda a posição do cursor depois dos checks de inicio de partida ou de carregamento de partida se não tiver

enquanto o jogador estiver fazendo ações não comitadas fica em um buffer o que ele está fazendo (tem algo assim no replay manager) entao ele pode mudar, fazer outras coisas , desistir de atirar pra embarcar qq coisa, quando ele termina vai pro action na pilha e qdo liberar o cursor faz um snapshot

entao a pilha vai sempre cresecendo em ações em lote, snapshot, ações em lot, snapshot

turno 0: começou um novo jogo, o sistema faz o q tem q fazer, libera o cursor pro jogador, faz snapshot#0 

no replay mostra lá q tem 1/1 gravado

o jogador move o cursor, escolhe uma unidade, movimenta, seleciona um sensor, mas nao confirma a ação (essas ações e troca de escolhas estão no buffer), entao ele bate esc ate cancelar e voltar pra neutral, o buffer é descartado

o jogador move o cursor, escolhe outra unidade, movimenta, escolhe o sensor de mirar, escolhe o alvo e atira, confirma, guarda no buffer. a animação executa as unidades trocam dano, algumas morrem e tal, guarda no buffer quando liberar o cursor em neutro.

agora a pilha está assim

snapshot#1
action#1
snapshot#0

entao o jogador move o cursor, escolhe um soldado, embarca em um helicoptero, confirma -> guarda o buffer -> a animação acontece, o cursor é liberado no final -> guarda snapshot

entao agora temos assim

snapshot#2
action#2
snapshot#1
action#1
snapshot#0

entendeu?

tinha uma rotina que comparava o local do cursor se era diferente ele fazia uma pequena animação de truque pra mover o cursor até la antes de executar a rotina.

o back move pra tras em snapshots mas nao executa, o fwd executa apenas 1 bloco de action da pilha, o play executa todos, mas se o jogador der pause, tem q esperar terminar o lote de comando e chegar no snapshot

no replay manager ou replay panel controller sei la, tem q deixar o cara esoclher em qual posição da pilha começa se bottom up, up bottom ou local especifico (creio q ainda está lá)

faltou tambem no replay panel ui.cs pra escolher um specific team, e se for -1 significa q vc não está emulando a visão de nenhum jogador (pq as vezses vc quer assistir o replay de um time sobre a otica de outro time

-----------
eu ja falei, e vou repetir

snapshot#2
action#2
snapshot#1
action#1
snapshot#0  (inicio de turno, cursror 0,0) <<

aperto fwd, o cursor sobe

snapshot#2
action#2
snapshot#1
action#1 <<  
snapshot#0  

descobre que o batch inicia em 3,3, entao o cursor movimenta do 0,0 até o 3,3 fazendo o som de cursor normal e a camera segue como se fosse um jogador jogando, quando chega la, executa o bloco batch, selecionando a unidade, movendo, escolhendo cursor, o substep e confirmando. agente ve a animação do sensor escolhido e termina, o cursor sobe mais um degrao

snapshot#2
action#2
snapshot#1 << 
action#1  
snapshot#0  

e fica aguardando, se o jogador apertar FWD denovo, le o snapshot, o cursor avança pro action#2

snapshot#2
action#2  << 
snapshot#1 
action#1  
snapshot#0  

entao descobre que o cursor esta na 7,15, faz a corotina de mover ate la, e inicia o batch, que é dessa vez um embarque em helicoptero, faz as paradas, seleciona, move, sensor, substep, confirma. agente ve a animação acontecendo e qdo termina avança mais um

snapshot#2 << 
action#2  
snapshot#1 
action#1  
snapshot#0  

a turma do gameboy em 1981 fez isso com os recurso da epoca, agente não pode apanhar pra eles em 2026!

se o jogador apertar back, ele volta entre snapshots (nao entre ações) entao se o snapshot #2 era depois do embarque no helicoptero e o snapshot #1 era depois do combate

snapshot#2 << 
action#2  
snapshot#1 
action#1  
snapshot#0  

o jogador aperta back e o cursor volta 1 snapshot, mas nao executa o batch (só quem executa é o fwd ou o play)

snapshot#2  
action#2  
snapshot#1 << 
action#1  
snapshot#0  

e se ele voltar denovo, volta para o inicio do jogo. Nota que quando voltar pro snapshot#1 q era antes do embarque, as unidades embarcadas aparecem, pq elas foram gravadas no snapshot #1

snapshot#2  
action#2  
snapshot#1 
action#1  
snapshot#0 <<
 
e se eu tiver no snapshot#0 e encerrar o replay, eu volto pro jogo onde tava

a qualquer momento durante a execução do replay o jogador pode apertar pause, mas o pause nao interrompe o batch que já iniciou, ele para no snapshot após o batch, entao se iniciou a animação de combate ele tem q ver a animação e depois o replay pausa pra ele escolher manualmente outras opções ou encerrar o replay, retornando pra partida atual.

o replay é uma ferramenta de ver o passado, não um undo pra sacanear e fazer novas escolhas

se ele apertar stop, sai do replay e volta pro jogo onde estava, quando eu apertar Start no replay, tem q iniciar o replay (nao está funcionando)

meu amigo, o batch é uma ação pre-gravada que é gravada quando o jogador confirmar o sensor escolhido, enquanto ele estiver selecionando unidade, decindo onde ir, mudando de ideia e escolhendo outra coisa o buffer é volatil e não é gravado, assim q ele confirmar e antes da animação começar isso é gravado na pilha


assim q o jogo inicia, se não tiver snapshot#0 ele aguarda o jogo fazer todas as validações de inicio de partida (subir contador de turno de 0 pra 1, aplicar dinheiro, spawnar se tiver coisas pra isso) e quando liberar pro jogador em neutro, grava o snapshot#0

se vier de um loadgame, usa o snapshot#0 que tem la, alem de tudo o que já estiver lá