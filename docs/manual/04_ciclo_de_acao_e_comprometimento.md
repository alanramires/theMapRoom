# Ciclo de Ação e Comprometimento

*Quando uma intenção vira alteração real no tabuleiro.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## O Ciclo da Ação

Você já viu o relógio da partida. Falta o relógio de cada peça — e ele importa, porque quase toda regra deste manual depende de em que ponto do ciclo a unidade está.

### Movimento Mais Ação

Toda jogada tem a mesma forma, e ela é um par:

**movimento (obrigatório) + ação**

O movimento sempre acontece — mesmo quando a unidade fica exatamente onde está. Ficar parado é um movimento de distância zero, e é uma escolha, não uma omissão.

A ação é o que vem depois, e "apenas mover" é uma das opções válidas dela. Você pode andar e encerrar; pode andar e capturar; pode ficar parado e atirar; pode ficar parado e fundir. O par sempre existe, e as duas metades são suas.

### Os Passos

Toda jogada segue a mesma sequência:

Você **seleciona** a unidade.

Ela **se posiciona** — andando, ou ficando exatamente onde está. As duas coisas contam como posicionamento, e algumas ações só existem para quem não andou.

Os **sensores apresentam as opções** daquela posição: o que ela enxerga, o que alcança, o que pode fazer dali.

Você **escolhe uma opção**: atacar, capturar, embarcar, desembarcar, suprir, transferir, fundir — ou simplesmente ficar onde parou.

A **animação toca** e o **mundo recalcula**.

Fim. A unidade não age de novo neste turno.

### Pousar e Decolar Não São Ações

Repare que a lista de opções acima **não** inclui pousar, decolar, subir, descer, emergir ou submergir. Isso é deliberado: mudar de camada nunca é uma jogada que você escolhe. É consequência automática de outra coisa.

Quando você seleciona uma unidade, ela vai para a camada natural do domínio dela — o avião decola, o submarino mergulha, o helicóptero sobe. Você não deu a ordem; a seleção deu. E as transições que aparecem no meio de outras ações são chamadas pelos próprios sensores que precisam delas: suprir uma aeronave a faz pousar, receber o serviço e decolar de novo na mesma jogada; transferir estoque a faz pousar e permanecer; comprar uma aeronave a faz nascer no solo até algo interagir com ela.

O motivo é de fundo, e vale entender porque explica várias regras de aviação: se pousar fosse uma ação de jogador, a aviação usaria o solo como esconderijo antiaéreo. Como toda ação se resolve no comprometimento e o inimigo só responde depois, uma aeronave que pousasse manualmente na frente da bateria antiaérea passaria o turno dela intocada. Tirando pouso e decolagem das mãos do jogador, a aviação fica no ar a maior parte do tempo — que é onde a antiaérea e os caças a alcançam. O detalhe completo está em `08_transporte_fusao_e_operacoes_aereas.md`.

### Existe Uma Confirmação Só

Este é o ponto que mais confunde, então vale ser direto: **mover não confirma nada.**

No xadrez, peça tocada é peça mexida. Aqui não. Pense em levantar o cavalo, percorrer o L com ele no ar, olhar como o tabuleiro ficaria — e devolver a peça à casa de origem. Enquanto você não larga, nada aconteceu.

É exatamente assim. Você posiciona a unidade, os sensores mostram o que dali é possível, e você continua livre. Pode cancelar e mover para outro lugar. Pode abrir a mira, ver o resultado previsto do ataque, desistir na última tecla e voltar ao movimento. Nada disso tocou o tabuleiro.

A única confirmação que existe é a da ação escolhida. É ela que dispara a animação, recalcula o mundo, atualiza a névoa e encerra a jogada daquela unidade.

E "apenas mover" é uma ação como qualquer outra. Escolher andar e não fazer mais nada é uma decisão válida, que fecha o ciclo do mesmo jeito.

### Por Que Isso Importa

Porque a prévia mostra o que você **já sabe**, não o que existe.

Isso é o que impede a exploração hexágono a hexágono. Se você levar o cursor até uma cidade escondida na névoa, a opção de capturar não aparece. Se levar um helicóptero até um hexágono onde há artilharia inimiga, a sua unidade não aparece empilhada denunciando que tem alguém ali. A prévia não vaza o mapa — ela mostra a sua leitura atual dele.

O jogo te deixa experimentar posições e olhar as opções de cada uma antes de se comprometer. O que ele não te deixa fazer é **desfazer o mundo** depois que a ação foi confirmada — a partir dali, a névoa foi recalculada, os contatos foram atualizados e a unidade já agiu.

Planejar é livre. Executar é definitivo.

### Por Que o Estado da Peça Importa Tanto

Volte às regras que você já leu e repare quantas dependem deste ciclo:

O Serviço do Comando só atende quem ainda não agiu.

O suprimento só atende quem ainda não recebeu neste turno.

A fusão exige uma receptora machucada e consome a ação das duas.

Captura, embarque e desembarque exigem terminar o movimento no lugar certo.

Algumas ações só existem para quem permaneceu parado.

E a mais consequente delas merece nome próprio: **quem se deslocou engaja apenas no contato.** Uma unidade que andou neste turno só pode atacar a distância 1 — ou 0, no caso das armas que operam no próprio hexágono. Toda arma de alcance mínimo 2 ou mais fica indisponível depois do movimento.

É o que separa a artilharia do resto do exército. Ela não atira e corre, nem corre e atira: ela precisa estar onde precisa estar **antes** do turno em que dispara. Some isso ao custo de deslocamento dela e você entende por que posicionar artilharia é uma decisão de dois turnos, e por que perder a posição dela custa tão caro.

Repare que a trava não é sobre a arma ter alcance longo — é sobre ela **não alcançar o contato**. Uma arma de 1 a 4 continua disponível depois de andar, limitada ao alcance 1. O que fica de fora é quem não sabe brigar de perto.

"Agiu" e "não agiu" não são detalhes de interface. São o estado que decide o que ainda é possível — e é por isso que a ordem em que você mexe nas suas peças muda o que você consegue fazer com elas.

### Uma Boa Ordem Operacional

Existe uma sequência que funciona bem, e é a que a inteligência artificial usa hoje:

Primeiro o Serviço do Comando. Depois as ações das unidades. Por último, as compras.

Você não tem essa obrigação. Pode comprar antes de agir, agir antes de comprar, alternar entre as duas coisas, ou simplesmente passar a vez sem fazer nada. A liberdade é sua.

Ainda assim, vale entender por que essa ordem funciona. Resolver o comando primeiro significa entrar no turno com as unidades já atendidas. Agir antes de comprar significa saber o que sobrou de dinheiro e o que faltou no front antes de decidir o que reforçar.

E há um motivo mecânico, não só de bom senso: **o Serviço do Comando só atende unidades que ainda não agiram**. Cada peça que você **comprometeu** antes de acionar o comando é uma peça que sai da lista. Deixar o lote para o fim do turno significa encontrá-lo quase vazio.

E "comprometeu" é literal, sem pegadinha: selecionar uma unidade, arrastar o cursor e cancelar **não** a tira da lista. Enquanto você não confirma, ela não agiu — e o Comando ainda a atende normalmente. Vale o mesmo princípio de todo o capítulo: só o commit conta. Ensaiar com a peça é de graça, aqui como em tudo o mais.

É uma boa ordem padrão. Não é uma regra — e a IA pode mudar a dela sem que isso mude o jogo.

## Mover É Se Comprometer

Uma última regra, e ela é a que dá tensão a tudo.

Você não move para descobrir e depois decide se aceita a posição. Você aceita a jogada inteira e só então descobre o que havia além dela.

O deslocamento acontece dentro da névoa, e é aqui que vale ser preciso, porque a leitura errada muda o jogo completamente: **parar o cursor num hexágono não recalcula nada**. A unidade pousada provisoriamente no destino não enxerga mais do que enxergava antes de sair — os sensores continuam mostrando a sua leitura anterior do mundo, e é sobre essa leitura antiga que você escolhe o que fazer.

O recálculo de visão, alcance e detecção vem **depois** da ação confirmada. Você não anda, revela o inimigo e então decide atacá-lo na mesma jogada. Você compromete movimento e ação juntos, às cegas, e o mundo te responde em seguida.

O que você pode fazer a partir de um destino provisório depende do estado de conhecimento do time sobre aquele hexágono — visível agora, apenas explorado, ou nunca explorado. Os três estados estão definidos em `05_visao_deteccao_e_nevoa.md`; aqui fica o que cada um libera:

**Destino visível agora.** Tudo funciona normalmente. Não há o que esconder de você.

**Destino apenas explorado.** Ficam liberados o ataque, o desembarque, a captura e a transferência. Embarque, fusão e suprimento continuam calados, porque dependem de saber quem está lá agora, e isso a fotografia não conta.

**Destino nunca explorado.** Sobra apenas o ataque, e ainda assim restrito aos alvos que você já conhecia e cujo **corredor de tiro** esteja inteiramente conhecido pelo time — o corredor está definido em `05_visao_deteccao_e_nevoa.md`. É o mínimo para que avançar não seja suicídio automático, sem transformar o cursor numa lanterna.

Existe um princípio por trás dessa escada, e ele vale para qualquer opção que o jogo te ofereça: **ou o menu filtra pelo que o seu time conhece e só mostra o conhecido, ou não filtra e mostra tudo. O que ele nunca faz é filtrar pela verdade oculta.**

Guarde isso porque explica uma assimetria que parece incoerente e não é. O leque de movimento pode alcançar o preto — ele te diz que dá para ir até ali, e não te diz por que não dá para ir mais longe. Isso é oferta ampla com motivo escondido, e é honesto. O que seria desonesto é uma lista que aparece e desaparece conforme o que existe no escuro: aí as ausências viram um mapa, e você leria o inimigo sem nunca ter olhado para ele.

Isso impede a exploração gratuita do mapa casa por casa, e transforma cada avanço numa aposta real. Mover-se sem saber quem observa o setor é a maior aposta da partida.

No ar, a velocidade é anulada pela ignorância. No mar, o alcance é anulado pelo medo de emergir. Quem move primeiro no escuro entrega sua posição — e quem espera demais perde a janela.

A guerra aqui não é sobre atirar. É sobre decidir se o alvo que apareceu no seu tabuleiro está realmente vulnerável — ou se foi você quem entrou numa emboscada.
