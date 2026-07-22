# Transporte, Fusão e Operações Aéreas

*Como unidades mudam de estado, viajam dentro de outras ou se reorganizam.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## Transporte e Embarque

Você já viu o princípio: uma unidade embarcada adota o domínio do transportador. Tropas dentro de um helicóptero atravessam Air/Low. Infantaria num navio cruza Naval/Surface.

Agora as consequências, que são mais fundas do que parecem.

### Não É Espaço, É Vaga

Um transportador não tem "lugar para três unidades". Ele tem vagas — e cada vaga é especializada, com regras próprias.

Cada vaga define quantas unidades aceita, quais classes pode receber, quais habilidades exige ou proíbe, e em que camadas ela opera.

Isso é deliberado. Uma vaga de passageiro não vira vaga de carga porque sobrou lugar. Um caminhão que reboca artilharia não leva infantaria no engate só porque ele está livre. A vaga tem função, não volume.

O navio de desembarque é o exemplo mais claro. O porão dele leva duas unidades — e recusa explicitamente duas coisas: o Trem de Carga, que pertence aos trilhos, e a artilharia que depende de reboque, que precisaria do seu rebocador junto. Ele desembarca tropa e veículo numa praia. Não é uma balsa para qualquer coisa que caiba.

Na prática, a pergunta certa nunca é "cabe?". É "este transportador tem uma vaga que aceita esta unidade?".

### Quem Faz o Quê

Uma assimetria que confunde no começo e depois faz todo o sentido:

Embarcar é ação do passageiro. Quem sobe é quem decide subir — a tropa corre até o veículo.

Desembarcar é ação do transportador. Quem abre a porta é quem dirige — o veículo escolhe onde e quando descarregar.

Por isso você seleciona a infantaria para embarcá-la, mas seleciona o helicóptero para desembarcá-la.

### O Preço de Ser Carga

Enquanto está embarcada, a unidade sai do tabuleiro em quase todos os sentidos que importam:

Não consome autonomia. Quem paga a viagem é quem dirige.

Não enxerga. Uma unidade embarcada não detecta nada — ela não contribui com visão nem com detecção para o seu time. Um EWACS dentro de um transporte é um passageiro, não um radar.

Não captura. Nenhuma captura acontece de dentro de um veículo.

Some como alvo independente. Ela existe pelo transportador — e compartilha do destino dele.

Essa última merece ser dita sem metáfora, porque é uma das regras mais duras do jogo. **Quando o transportador é destruído, todos os passageiros morrem com ele.** Não há teste de sobrevivência, não há desembarque de emergência, não há resgate. A regra desce por toda a cadeia: passageiro de passageiro morre junto, quantos níveis houver. E as reservas que estavam a bordo — combustível, munição, peças — são perdidas com a carga.

O contrapeso existe e vale conhecer, porque muda como você usa transporte sob fogo: **enquanto o transportador está vivo, quem está dentro não morre.**

A propagação é proporcional, e vale enunciá-la sem ambiguidade porque a palavra "proporcional" admite várias leituras:

Primeiro se calcula a **fração perdida pelo transportador** — quanto ele perdeu dividido pelo que ele tinha antes do golpe. Um transportador de 10 que cai para 7 perdeu 30%.

Essa mesma fração é aplicada ao efetivo **atual de cada passageiro**, com mínimo de 1. Um esquadrão de 10 embarcado perde 3; um de 4 perde 1.

O arredondamento é o comum, com uma particularidade que vale declarar em vez de deixar implícita: quando o resultado cai exatamente em meio ponto, ele vai para o **par mais próximo**. Uma fração que dê 1,5 baixa vira 2; uma que dê 2,5 vira 2. Na prática isso quase nunca decide nada, porque o piso de 1 e o teto do efetivo cortam antes — mas a fonte técnica não deve deixar a direção do arredondamento em aberto.

Nenhum passageiro desce abaixo de **1 ponto de vida**, por pior que seja a conta.

E a mesma fração desce por toda a cadeia, sem ser recalculada a cada nível. Se houver passageiro dentro do passageiro, ele sofre os mesmos 30%, aplicados ao próprio efetivo.

Duas consequências práticas: qualquer golpe que arranhe o transportador tira pelo menos 1 de cada passageiro, então transporte sob fogo sangra mesmo quando o veículo aguenta bem; e a tropa desembarcada de um comboio castigado chega desfalcada, com a penalidade de esquadrão ferido já valendo. Um comboio castigado entrega tropa machucada; um comboio destruído não entrega nada.

Some as duas metades e o quadro aparece: transportar é concentrar risco num único token, com um único conjunto de defesa e posição. Um comboio bem atacado não perde uma unidade. Perde a operação inteira.

## Fusão de Esquadrões

Reparar custa caro e depende de logística no lugar certo. Existe uma alternativa mais rápida para reconstituir força: juntar dois esquadrões desgastados num só.

Dois esquadrões de 5 HP viram um esquadrão de 10. A unidade some do tabuleiro, o token continua — e você trocou duas peças fracas por uma inteira.

### Quem Pode Fundir com Quem

A fusão é restrita, e as restrições importam tanto quanto a conta:

Mesmo tipo de unidade. Soldado funde com Soldado. Não se junta infantaria com blindado para fabricar um híbrido.

Adjacentes. Os dois precisam estar a um hexágono de distância — e é bom saber quem fica onde: a unidade que você seleciona é a **receptora**. Ela permanece no próprio hexágono e conserva a posição dela; a outra se desloca até lá e é consumida pela fusão. O hexágono do resultado é sempre o do receptor.

Mesma camada — mas o jogo tenta resolver isso por você. Se as duas estiverem em alturas diferentes, a fusão iguala automaticamente: aeronaves se encontram em Air/Low, o que faz a que estava no solo decolar; submarinos se encontram submersos, quando o hexágono permite.

Se a igualação não for possível — o hex não aceita a camada, a transição está bloqueada — a fusão simplesmente não acontece.

O receptor precisa estar machucado. Unidade com esquadrão completo não recebe fusão — não há onde colocar mais gente.

Nenhuma das duas pode estar transportando. Quem carrega passageiros não funde, de nenhum dos lados.

E a fusão consome a ação. O esquadrão resultante termina o turno sem movimento e sem poder agir. Você trocou duas peças por uma, e essa uma já fez o que tinha para fazer.

### Contribuição, Não Herança

Aqui o jogo poderia ter feito o simples: pegar o maior valor de cada atributo e seguir em frente. Não é o que acontece, e a diferença importa.

Volte à ideia de esquadrão. Quando um esquadrão de 7 homens anda um hexágono, não é uma pessoa que anda — são sete. Quando ele dispara uma vez, não é um tiro — são sete.

Então cada unidade contribui com o total real que ela carrega:

Passos contribuídos = autonomia × HP

Projéteis contribuídos = disparos × HP

E o esquadrão resultante divide esse total pelo novo tamanho.

### Um Exemplo Completo

Um esquadrão de Soldados com 7 HP, 30 de autonomia e 3 disparos contribui com 210 passos e 21 projéteis.

Chega outro, com 3 HP, 50 de autonomia e 1 disparo: contribui com 150 passos e 3 projéteis.

A fusão soma tudo e redistribui pelo novo efetivo de 10 homens:

HP: 7 + 3 = 10.

Autonomia: 360 passos ÷ 10 = 36.

Disparos: 24 projéteis ÷ 10 = 3.

Repare no segundo esquadrão. Ele tinha 50 de autonomia e o resultado ficou em 36 — os 50 dele nunca foram 50 para dez pessoas. Eram 50 para três. Espalhados por um esquadrão maior, viram 36.

Não houve herança arbitrária: houve redistribuição pelo tamanho real da tropa. O combustível que levava três homens longe não leva dez igualmente longe.

O que acontece depois da divisão, aí sim, não é neutro — e é o assunto da próxima seção.

### Dois Detalhes que Decidem

O arredondamento não é simétrico, e é bom saber de que lado ele cai.

Munição e reservas arredondam para cima. Foi por isso que 2,4 disparos viraram 3 no exemplo — e vale ser honesto sobre o que isso significa: o esquadrão novo passa a carregar o equivalente a 30 projéteis, embora só 24 tenham sido contribuídos. Houve ganho real, não apenas redistribuição. A fusão é generosa com o paiol.

A generosidade é proposital, e existe para evitar um resultado absurdo: sem ela, juntar dois esquadrões que ainda tinham disparos poderia produzir um esquadrão com **zero** munição. Uma tropa não pode ficar desarmada por ter se reorganizado.

Autonomia trunca para baixo. Sobra de combustível que não completa um passo inteiro simplesmente se perde. Aqui a fusão é mesquinha.

Some os dois e a leitura correta é essa: a redistribuição é proporcional, e o arredondamento em cima dela é intencionalmente torto — favorece quem atira, penaliza quem anda.

Cada arma é calculada separadamente. Uma unidade com canhão e metralhadora não mistura os dois paióis: cada armamento recebe a sua própria soma de projéteis e a sua própria divisão. O mesmo vale para cada tipo de suprimento transportado.

O teto de 10 é absoluto. Fundir um esquadrão de 7 com um de 5 não produz 12 — produz 10, e os 2 HP excedentes desaparecem. Fusão com sobra é desperdício puro, e quase sempre vale mais fundir o de 7 com um de 3 e deixar o outro inteiro.

### Quando Vale a Pena

A fusão brilha exatamente onde o reparo é ruim: longe da logística, com pressa, e com unidades baratas.

Ela não custa dinheiro, não exige suprimento no hexágono, e devolve um esquadrão cheio imediatamente. O preço é que você fica com uma unidade a menos no tabuleiro — menos presença, menos frentes, menos hexágonos ocupados.

Repor com fusão é concentrar. Repor com reparo é manter espalhado. As duas coisas são úteis, e raramente ao mesmo tempo.

### Onde o Céu Pergunta

Pousar é o caso com mais chaves diferentes, porque cada tipo de aeronave desce de um jeito:

Pouso convencional exige pista de verdade: aeroporto, ou estrada e trilho construídos na planície.

Pouso vertical dispensa pista. Helicópteros descem em qualquer construção.

Pouso curto é o meio-termo, e no seu exército uma única aeronave o tem.

Pouso em convés é exigido pelo hangar do porta-aviões e da fragata. Sem essa etiqueta, a aeronave não embarca no navio.

Pouso na água é só do hidroavião — mar e praia perguntam por ela, e ninguém mais responde.

## Decolagem e Pouso

A ponte entre o solo e o ar não é instantânea para todo mundo, e a diferença define quem sobrevive ao decolar.

### Três Maneiras de Subir

Aeronaves decolam por procedimentos diferentes, conforme o que elas são:

Subida direta — a aeronave sai do solo já na altura que prefere, e ainda dispõe do movimento do turno. É o privilégio de quem decola na vertical.

Corrida de um hexágono — a aeronave precisa correr para ganhar sustentação.

Corrida curta — de zero a um hexágono, conforme a situação.

E aqui está o detalhe que decide vidas: quem precisa de corrida termina a decolagem em Air/Low. Não em Air/High.

O lugar de onde se decola muda esse desfecho. **Do aeroporto, a subida é completa**: a aeronave sai direto para a altitude que prefere, sem passar por Air/Low. De qualquer outro ponto, quem precisa de corrida termina na camada baixa — e passa pela altura onde a furtividade não funciona e a antiaérea alcança. É por isso que a instalação aeronáutica vale como posição, e não só como posto de manutenção.

### Por Que Isso Importa

Air/Low é a camada onde a furtividade aérea não funciona.

Um caça furtivo é invisível em Air/High. Mas ele não nasce em Air/High — ele decola, corre, e passa por Air/Low no caminho. Durante essa janela, ele é uma aeronave comum como qualquer outra, visível para qualquer sensor no alcance.

A vulnerabilidade no lançamento é intencional. Ela existe para que aeroportos, porta-aviões e pistas improvisadas sejam alvos que valem a pena — e para que o corredor de subida seja algo que você precisa proteger, não algo que você ignora.

Quem controla o céu sobre o aeroporto inimigo não precisa destruir o aeroporto. Basta esperar as aeronaves subirem.

### Onde se Pousa

Pousar exige superfície compatível, e o jogo reconhece algumas:

A pista de aeroporto — a superfície completa, com tudo que vem junto.

A estrada servindo de pista improvisada — onde o terreno permite.

O solo plano, para quem pode.

E no mar, o convés e a doca, para as aeronaves navais.

Vale relembrar a regra de prioridade que você já conhece, porque ela morde aqui: o terreno cancela o que a estrutura abriria. Uma estrada na planície vira pista. A mesma estrada na montanha, não. A estrutura oferece, o terreno decide.

### Toque e Arremetida

Aeronave no The Map Room não estaciona. Ela toca o solo, faz o que precisa, e volta para o ar.

Uma aeronave recém-comprada nasce no chão. Na primeira atividade — você a seleciona, ou manda alguém embarcar nela — ela decola. E a partir daí, toda operação que exige o solo segue o mesmo ciclo automático:

Recebe um passageiro: pousa, embarca a unidade, decola.

Desembarca tropas: pousa, desembarca, decola.

Recebe suprimento: pousa, é atendida, decola.

Recebe Serviço do Comando: pousa, é atendida, decola.

Você não precisa mandar decolar. O ciclo é automático, e ele existe também como confirmação: ver a aeronave voltar ao ar é o sinal de que a operação deu certo.

### Quando Ela Fica no Chão

Duas situações quebram o ciclo — e nas duas, ficar parado é o ponto.

Combustível esgotado. Uma aeronave que fez pouso de emergência por falta de autonomia não arremete. Ela fica ali, imóvel, esperando alguém trazer combustível. Só depois de suprida ela volta a voar, e aí pelas regras normais de decolagem: subida direta, corrida curta ou corrida de um hexágono, conforme o que ela é e onde está.

Transferência de recursos. Uma aeronave que transfere estoque para outro agente pousa e permanece pousada. Transferir carga não é reabastecer em trânsito — é uma operação demorada, e ela cobra o tempo em que a aeronave fica no solo.

Existe ainda uma condição silenciosa sobre o ciclo: a arremetida só acontece se a decolagem for possível naquele hexágono. Se o terreno não permite subir dali, a aeronave permanece no solo depois da operação, queira você ou não. Vale relembrar a regra da estrada na montanha — a estrutura oferece, o terreno decide.

### Como se Mantém uma Aeronave Pousada

A pergunta aparece cedo, e a resposta é menos óbvia do que parece: **não existe comando de estacionar.** Você não manda a aeronave permanecer no solo, do mesmo jeito que não manda subir nem descer.

O que existe é o ciclo transacional de sempre. Ao selecionar uma aeronave pousada, ela é levantada **provisoriamente**, só para o jogo poder te mostrar o alcance que ela teria no ar. Nada disso tocou o tabuleiro ainda. A partir daí:

Se você **cancelar**, ela volta ao solo exatamente como estava. A decolagem provisória é desfeita junto com o resto do ensaio.

Se você **confirmar qualquer ação**, a decolagem é confirmada junto. Não há como agir e continuar no chão.

Então a resposta operacional é essa: **uma aeronave permanece pousada quando você não age com ela.** Ela pode ficar no hangar por quantos turnos você quiser, e o custo de mantê-la ali é zero se o hangar for um aeroporto, avançado ou hidrobase. Deixá-la parada não é uma omissão do jogador — é a única forma de descanso que existe.

O mesmo vale para a aeronave recém-comprada: ela nasce no chão e só decola na primeira atividade. Comprar um caça no fim do turno e não mexer nele deixa o caça em solo, íntegro e sem consumir, esperando o turno seguinte.

### Por Que o Ciclo Existe

Lembre do capítulo de domínios: uma aeronave pousada está em Land/Surface. Ela saiu do céu — e com isso, saiu do alcance de tudo que foi construído para derrubar aeronaves. Armas antiaéreas miram Air/Low e Air/High. Contra um caça estacionado no chão, elas não têm alvo.

Se cada operação deixasse a aeronave pousada, o resultado seria uma força aérea que toca o solo para ficar invulnerável à defesa antiaérea e sobe só para atacar. A antiaérea viraria uma unidade decorativa, pagando caro para nunca encontrar um alvo.

A arremetida automática fecha essa porta. Por padrão, aeronave operacional está no ar — que é onde a antiaérea pode encontrá-la, e onde ela paga o consumo de autonomia que você já conhece.

E as duas exceções passam a fazer sentido como o que realmente são: janelas deliberadas de vulnerabilidade. A aeronave sem combustível e a aeronave transferindo carga estão no chão porque ficaram sem escolha ou porque escolheram uma operação lenta. Nos dois casos, ela está fora do alcance da antiaérea e dentro do alcance de tudo que atira em solo.

Não é impunidade. É troca de ameaça.
