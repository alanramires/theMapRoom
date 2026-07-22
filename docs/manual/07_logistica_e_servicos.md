# Logística e Serviços

*Como uma força continua operacional.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## Autonomia

Munição acaba. Combustível acaba. Nenhuma força opera indefinidamente longe de casa — e no The Map Room isso não é sabor narrativo, é uma conta que corre a cada turno.

Autonomia é o fôlego operacional do esquadrão. Ela se gasta de duas maneiras muito diferentes, e entender qual das duas te afeta muda completamente como você joga aquela unidade.

### Gastar Andando

Cada terreno tem um custo básico para ser atravessado. Alguns perfis de unidade multiplicam esse custo.

A artilharia de campanha é o exemplo extremo: ela multiplica por cinco o custo de cada hexágono. Rebocar um obus pesado pelo mapa bebe combustível de uma forma que uma tropa a pé não bebe. Em compensação, parada ela não gasta absolutamente nada.

Essa é a assinatura das unidades pesadas de solo: caras para deslocar, gratuitas para manter. Posicione bem uma vez e ela sustenta a posição de graça.

### Gastar Existindo

Aeronaves funcionam ao contrário. Elas não multiplicam o custo por hexágono — voar de um lado a outro do mapa não é o problema. O problema é estar no ar.

Toda aeronave paga um consumo fixo no início de cada turno, só por estar em operação:

| Perfil | Consumo por turno | Unidades |
|---|---|---|
| Jatos | 5 | Caças, bombardeiros, EWACS |
| Turboélices | 3 | Super Tucano, avião-tanque |
| Helicópteros | 2 | Apache, Chinook, hidroavião |

O jato é a unidade mais impaciente do jogo. Ele chega a qualquer lugar depressa e não pode ficar em lugar nenhum por muito tempo.

### A Regra que Pega Todo Mundo

O consumo da aeronave não para quando ela pousa.

Uma aeronave pousada continua pagando o mesmo consumo de turno que pagava no ar. Estacionar no Quartel General não ajuda. Na fábrica, na cidade, no porto — não ajuda. Ela continua queimando.

As exceções são três, e todas do mesmo tipo — instalações aeronáuticas de verdade: o **Aeroporto**, o **Aeroporto Avançado** e a **Hidrobase**.

Aeronave pousada em qualquer uma delas não paga nada. É a diferença entre desligar os motores num hangar com equipe de solo e deixar a aeronave de prontidão num campo qualquer. Cidade, fábrica, porto, docas, quartel general — nenhum desses é hangar, por mais importante que seja o prédio.

A Hidrobase merece nota, porque ela é fácil de subestimar: ela isenta o consumo de **qualquer** aeronave que consiga pousar ali, não só do hidroavião. O que é exclusivo do hidroavião é o **mercado** dela — a Hidrobase só vende hidroaviões, mas hospeda a aviação inteira que souber descer na água.

Isso dá às instalações aeronáuticas um peso estratégico que o mapa não anuncia. Serviço para aeronave você consegue em vários lugares — cidades, fábricas, o Quartel General, caminhões, fragatas, porta-aviões, aviões-tanque. Manutenção não é o que faz do aeroporto um aeroporto. São duas outras coisas.

A primeira você já viu: é onde uma aeronave pousada **deixa de consumir autonomia**. O relógio para.

A segunda é a decolagem: só do aeroporto a subida é completa. O procedimento e as consequências disso estão em `08_transporte_fusao_e_operacoes_aereas.md` — aqui basta saber que existe e que é exclusividade da instalação aeronáutica.

Então o aeroporto não é onde a aviação é atendida. É o hangar completo: o lugar que interrompe o relógio e devolve a aeronave direto ao seu céu. Perder o aeroporto não corta a manutenção — corta o descanso e a subida segura.

Uma última isenção, essa mais intuitiva: unidade embarcada não paga consumo. Dentro de um transportador, ela é carga. Quem paga a viagem é quem dirige.

### Quando o Combustível Acaba

Se uma aeronave em voo chega ao início do turno com a autonomia zerada, o jogo tenta salvá-la antes de puni-la.

Primeiro, procura-se um pouso de emergência. Se o hexágono onde ela está aceita pouso, ela desce — viva, imóvel e sem combustível, esperando que alguém a reabasteça.

Se o hexágono não aceita pouso, a aeronave é perdida. Sem combate, sem inimigo, sem disparo. Apenas acabou.

Os dois desfechos aparecem no relatório de início de turno, para você não descobrir o buraco na sua força aérea por acaso.

A lição operacional é direta: autonomia é um relógio que corre mesmo quando você não move nada. Voar sobre território inimigo sem plano de retorno não é audácia. É aritmética contra você.

## Suprimento

Autonomia acaba. Munição acaba. HP acaba. As três coisas se recuperam pelo mesmo sistema — e é ele que transforma um avanço num avanço sustentável.

### As Três Reservas

Agentes logísticos — unidades e construções — carregam três tipos de reserva física, e cada uma vira uma coisa diferente:

Galões viram Autonomia, pelo serviço de Reabastecimento.

Caixas de Munição viram Munição, pelo serviço de Rearme.

Peças viram HP, pelo serviço de Reparo.

Repare que reparar não é curar ferido: é repor membros do esquadrão. Peças entram, bolas de gude voltam para o copo.

Unidades têm teto de reserva — o caminhão carrega até encher e não aceita mais. Construções não têm teto: acumulam indefinidamente enquanto alguém entrega, e esvaziam conforme as unidades consomem.

### Serviço Custa Dinheiro

Reserva não é a única coisa que se gasta. Todo serviço cobra uma taxa em dinheiro, calculada sobre o valor de compra da unidade atendida.

Os percentuais abaixo são o preço de encher aquela categoria **do zero até o máximo**, calculados sobre o valor de compra da unidade atendida:

| Serviço | Custo cheio | Limite por turno |
|---|---|---|
| Reabastecer | 5% | sem limite — enche até o topo |
| Rearmar | 10% | sem limite — enche até o topo |
| Reparar (construção) | 40% | 2 pontos de vida |
| Reparar (caminhão, versão leve) | 40% | 1 ponto de vida |

Repare na assimetria, porque ela organiza o turno inteiro: **combustível e munição voltam de uma vez; vida volta devagar.** Encher o tanque e refazer o paiol é questão de encostar num supridor com estoque e dinheiro. Remontar o esquadrão é questão de turnos.

E aqui está o detalhe que muda tudo: **você paga só pelo que recuperou**. O custo é proporcional. Reparar um único ponto de HP num esquadrão de dez custa um décimo daquele teto, não os 40% inteiros.

Isso significa que não existe penalidade por atendimento pequeno. Encostar um caminhão e devolver dois pontos de combustível é barato, e continua sendo barato se você fizer isso todo turno.

### Um Atendimento por Turno

Antes do resto, a regra que organiza tudo: **cada unidade recebe atendimento uma vez por turno.**

Um atendimento pode incluir vários serviços de uma vez — o mesmo caminhão pode reabastecer, rearmar e reparar na mesma visita. Mas depois que você recebeu, acabou por este turno.

Isso significa que atendimentos **não somam**. Se a sua unidade já tomou o Serviço do Comando no início do turno, procurar um caminhão em campo depois não acrescenta nada. Ela já tomou o banho de cura deste turno.

Mas repare no que o atendimento **não** custa: ser atendido não consome a ação da unidade. Um esquadrão reparado ainda pode se mover e atacar no mesmo turno. Quem gasta a ação é quem presta o serviço, não quem recebe.

Isso muda a leitura do reparo. A unidade machucada não fica imobilizada — ela fica **presa à fonte**. Pode avançar depois de ser tratada, mas se avançar demais, não estará lá para o próximo atendimento.

### Um Prestador, Um Paciente

E existe um segundo limite, do outro lado do balcão, que costuma pegar o jogador desprevenido: **cada prestador atende uma unidade por turno.**

Isso vale para as construções inteiras. Uma cidade não é um hospital de campanha com dez macas — ela atende um esquadrão por turno. Fábrica, porto, aeroporto e o próprio Quartel General, todos iguais: um.

Do lado das unidades, quase todos os prestadores também atendem um — inclusive o Caminhão de Suprimentos, que é o supridor que você mais vai usar. As duas exceções do jogo inteiro são o **avião-tanque** e o **porta-aviões**, que atendem dois.

Vale dizer o que isso significa para o caminhão, porque a intuição erra: ele não é um posto de gasolina para a coluna. É um atendimento por turno, para uma unidade. Uma frente de quatro tanques precisa de quatro turnos de caminhão, ou de mais caminhões.

Há um detalhe generoso: uma construção atende a unidade **e os passageiros que ela carrega no primeiro nível**. Um transporte blindado cheio de tropa que encosta numa cidade sai dali com o veículo e os soldados atendidos, tudo dentro da mesma vaga.

O caminhão na rua não faz isso. Ele atende a unidade e só ela — quem está dentro continua como estava. Se a tropa precisa de serviço, precisa desembarcar ou chegar num prédio.

Junte tudo e o quadro operacional aparece: quatro unidades machucadas recuando para a mesma cidade formam uma fila de quatro turnos. Logística não escala empilhando gente no mesmo lugar — escala com mais pontos de atendimento. E transportar tropa até um prédio atende mais gente de uma vez do que levar o caminhão até ela.

### Onde Você Se Cura Importa

E aqui entra o limite que muda a natureza do reparo: **não se recupera um esquadrão de uma vez.** O quanto você recupera depende de onde é atendido.

Em uma construção — cidade, fábrica, aeroporto, porto, quartel general — o reparo devolve **2 pontos de vida** por turno. Não é a construção que faz diferença: é o serviço. Construções oferecem o reparo completo; o caminhão oferece a versão leve.

Dois navios prestam esse mesmo reparo completo, mas apenas para o ar. A **Fragata** tem um heliponto e atende exclusivamente helicópteros, um de cada vez. O **Porta-Aviões** tem hangar para duas aeronaves e atende quem souber pousar em convés. Nenhum dos dois conserta navios ou tropa terrestre — são bases aéreas que flutuam, não oficinas gerais.

Com o caminhão de suprimentos em campo, é reparo leve: **1 ponto de vida** por turno.

Um esquadrão reduzido a 2 pontos precisa de quatro atendimentos numa construção, ou oito atendimentos de campo.

E aqui está o detalhe que muda a leitura: ele **pode agir entre um atendimento e outro**. Pode lutar, pode defender o prédio onde está sendo consertado, pode recuar mais. O que ele não pode é se afastar da fonte — porque cada avanço que o tira do alcance do prestador interrompe a sequência.

O custo do reparo não é imobilização obrigatória. É **continuidade logística**. Você não está preso; você está amarrado a um ponto do mapa.

### O Custo Real do Reparo

Isso reposiciona a decisão inteira. O preço em dinheiro nunca chega de uma vez, porque a recuperação nunca chega de uma vez.

O custo verdadeiro de recuperar uma unidade muito desgastada é **tempo e posição**. E a escolha é sempre entre dois males:

Recuar para uma construção cura o dobro por turno — e gasta os turnos de ida e volta, além de manter a unidade colada naquele prédio enquanto dura o tratamento.

Ficar na frente e receber do caminhão mantém a posição — e cura na metade da velocidade, num lugar onde ela ainda pode ser atacada.

Em nenhum dos dois casos a unidade fica de braços cruzados. Uma tropa em reparo dentro de uma cidade continua defendendo aquela cidade.

Comprar uma unidade nova entrega força total imediatamente, num lugar que você escolhe. Reparar entrega força aos poucos, no lugar onde a unidade já está.

Por isso reparo é excelente para quem está pouco machucado e ruim para quem está quase morto. Não porque a conta fique cara — mas porque o relógio fica longo demais.

### Nem Todos Recebem Igual

A mesma quantidade de reserva rende diferente conforme a classe de quem recebe. Blindagem pesada consome mais para andar o mesmo tanto:

| Serviço | Leve | Média | Pesada |
|---|---|---|---|
| Reabastecimento | 3 | 2 | 1 |
| Rearmamento | 3 | 2 | 1 |
| Reparo | 2 | 1 | 1 |

*Pontos entregues por unidade de reserva consumida.* É aqui que a classe de armadura, que não limita nada em combate, cobra o seu preço: ela decide quantos galões o mesmo tanque bebe.

E o rearmamento cobra duas vezes pelo mesmo peso. Além de aproveitar menos cada caixa, armamento pesado custa mais caro por tiro reposto: um projétil de arma pesada pesa o **triplo** de um de arma leve na conta final, e o médio pesa o dobro. Note que esta segunda conta olha para a classe da **arma**, não da unidade — uma plataforma leve carregando canhão pesado paga munição de pesado. Reabastecer o paiol de um blindado não é a mesma despesa que reabastecer o de um soldado, nem por unidade de reserva, nem por moeda.

Um tanque pesado não é só caro de comprar. É caro de manter, de abastecer e de consertar. O custo de uma força pesada não termina na loja.

Vale ver o tamanho disso com números redondos. Recuperar um blindado caro que saiu de um combate sério — remendando o esquadrão, enchendo o tanque e refazendo os dois paióis — consome uma fatia grande da sua renda daquele turno. Não é uma despesa de manutenção: é uma decisão orçamentária que compete com comprar uma unidade nova.

É esse o efeito pretendido. Combate pesado gera pressão econômica, e uma doutrina de força pesada só se sustenta se a sua renda territorial acompanhar. Blindado sem cidade atrás é blindado que luta uma vez.

A transferência de reservas entre agentes é a única operação gratuita da cadeia. Mover estoque não custa nada; convertê-lo em benefício custa sempre.

### O Serviço Acontece na Mesma Camada

Atender alguém exige estar no mesmo plano operacional que essa pessoa — e o jogo resolve isso mexendo em quem for preciso.

Um avião-tanque em alta altitude que vai reabastecer um helicóptero desce para Air/Low, porque é ali que o helicóptero vive. Se ele atende também um caça na mesma leva, o caça é trazido para Air/Low junto. Todo mundo se encontra no mesmo andar para a operação acontecer.

O mesmo vale para baixo: um caça em voo que vai receber suprimento de um caminhão terrestre pousa para ser atendido — e, se puder, arremete depois, pelo ciclo que você já conhece.

E existe o caso em que não dá. Se o hexágono não aceita a camada necessária — a aeronave não tem onde pousar, o transporte não pode subir —, aquele alvo simplesmente é pulado. A fila segue para o próximo.

Terminado o atendimento, todos permanecem na camada em que ele aconteceu — o caça que desceu para Air/Low fica em Air/Low. E o que vier depois é automático: quem precisou tocar o solo arremete sozinha, pelo ciclo que você já conhece.

Vale dizer isso de forma geral, porque vale para o jogo inteiro: **não existem comandos de altitude.** Você não manda subir, descer, emergir nem submergir. A unidade vai para a camada preferida do domínio dela assim que é selecionada, e todas as outras mudanças são consequência de alguma coisa que ela fez — decolar, pousar, atacar, ser atingida, receber serviço.

Altitude não é uma alavanca que você opera. É o resultado do que a unidade está fazendo.

Vale guardar isso quando montar uma operação de reabastecimento: você não está só verificando alcance e estoque. Está verificando se todos conseguem se encontrar no mesmo plano, naquele terreno.

### O Serviço do Comando

Existe um atalho para tudo isso, e ele é apenas suprimento em lote.

O Serviço do Comando pega as unidades elegíveis, monta uma fila e executa os mesmos serviços que um supridor executaria em campo — pelas mesmas regras, com os mesmos custos e os mesmos limites. Não é um sistema separado: é o suprimento de sempre, aplicado de uma vez.

Com duas restrições, e elas decidem a ordem do seu turno. O Serviço do Comando só atende unidades que **ainda não agiram** e que **ainda não receberam atendimento** neste turno.

Suprimento em campo não tem essa trava. Um caminhão pode alcançar e atender uma unidade que já se moveu — o que importa ali é ela não ter recebido nada ainda naquele turno. Faz sentido: o caminhão chegou até ela.

Já o lote do comando é serviço de guarnição, prestado antes do expediente começar. Quem já saiu para trabalhar, perdeu.

### A Cadeia

Nem todo agente logístico faz tudo. Cada um pertence a um tier que define o que pode passar para quem:

Hub — doa e recebe. É o elo que movimenta estoque pela cadeia.

Receiver — apenas recebe. É a ponta da linha, o consumidor final.

A direção importa e não é simétrica. Um Hub abastece outro Hub ou qualquer Receiver. Um Receiver não devolve para ninguém — nem para Hub, nem para outro Receiver. O estoque desce a cadeia e não sobe.

No topo existe o Hub infinito: ele só doa, nunca recebe. É a fonte, e não faz sentido encher uma fonte.

### O Estoque Acaba

Um detalhe que muda o planejamento de campanha inteiro: **construções gastam reserva ao prestar serviço.**

Uma cidade que reabastece, rearma e repara vai esvaziando. Quando o estoque dela zera, os serviços param — e aquele ponto do mapa, que parecia uma base segura, vira apenas um prédio.

Reabastecer as próprias bases é trabalho seu. O Trem de Carga e o caminhão de carga existem para isso: mover estoque de onde ele sobra para onde ele falta, entre cidades e em direção ao front. É uma malha de distribuição que você opera na mão — sem rotas automáticas, sem entrega programada. Nada se move sozinho.

### A Cadeia Chega ao Mar e ao Ar

A cadeia mais longa do jogo é também a mais bonita de montar, e vale ver o desenho completo:

Um navio-tanque coleta estoque nas cidades da costa.

Ele leva esse estoque para um porta-aviões no mar aberto.

O porta-aviões converte em serviço para as aeronaves que pousam nele — ou repassa para um avião-tanque.

O avião-tanque decola e atende caças **no ar**, sem que ninguém precise voltar para casa.

E a corrente funciona em cascata dentro do mesmo hexágono. Um porta-aviões atracado num porto, com aeronaves a bordo: as aeronaves aparecem e são supridas **pelo porta-aviões**; depois o porta-aviões é suprido **pelo porto**. Cada elo atende o elo seguinte, na ordem, e o estoque desce a fila.

Cada elo é uma unidade sua, que se move, gasta o turno e pode ser atacada. Uma frota longe da costa só se sustenta enquanto essa corrente estiver inteira — e cortar um elo no meio dela vale mais do que afundar um caça.

### Quem Dá o Primeiro Passo

Uma regra que economiza confusão: construções nunca iniciam uma transferência.

Um depósito não sai atrás das suas tropas. Quem se desloca, encosta e pede é sempre a unidade. A construção é o ponto no mapa — a iniciativa é de quem tem pernas.

Isso significa que a logística é uma coisa que você faz, não uma coisa que acontece. Nenhum caminhão se move sozinho, nenhuma reserva se redistribui por conta própria. Uma unidade parada ao lado de um depósito cheio continua sem munição até você mandar buscar.

### Por Que Isso Decide Partidas

Compare os dois lados da mesma moeda.

O front que avança sem cadeia logística avança rápido e para de repente. As unidades chegam longe, gastam munição no primeiro combate sério e ficam paradas — vivas, posicionadas e inúteis. Uma unidade sem munição ocupa território, mas não disputa nada.

O front que avança com cadeia avança mais devagar e não para. Cada hexágono conquistado vira base para o próximo.

Por isso a logística não é um sistema acessório do The Map Room. É o que separa uma investida de uma campanha.
