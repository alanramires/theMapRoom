# Movimento, Terreno e Infraestrutura

*Como uma unidade percorre o mapa.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

### Onde o Terreno Pergunta

Alpino abre a montanha e a atravessa por 2. Toda a sua infantaria tem.

Fora-de-estrada também abre a montanha, mas cobra 6 — quase o turno inteiro. No exército inteiro, uma única unidade tem: o transporte blindado.

Guerrilha atravessa floresta por 1 em vez de 2. Também é da infantaria.

Motor é a etiqueta invertida: não abre nada, só cobra. Quem tem motor paga a mais na estrada da montanha e no trilho.

Some as três primeiras e você entende por que a infantaria é a rainha do terreno ruim. Ela sobe o que ninguém sobe, atravessa mato sem perder tempo, e é a única que toma território. O blindado ganha a batalha; a infantaria ganha o mapa.

### Onde a Estrutura Pergunta

Linha de trem é a etiqueta mais restritiva do jogo, e vale ver o tamanho disso: ela é exigida pela ponte ferroviária, e bloqueada pela estrada, pela ponte rodoviária e pelos cinco terrenos — planície, floresta, montanha, praia e mar.

E não adianta procurar atalho pelo mar: o navio de desembarque também recusa a etiqueta. Um trem não embarca.

Ou seja, o portador dessa etiqueta não anda em absolutamente nenhum lugar que não seja trilho, e não pode ser carregado para fora dele. E uma única unidade a carrega: o Trem de Carga.

A restrição é deliberada. Um trem sobre um navio de transporte é épico demais para ser plausível — e o Trem de Carga precisa continuar sendo o que ele é: uma unidade presa à malha que você construiu.

É o exemplo mais puro do sistema. A unidade não tem uma regra especial escrita nela. Ela tem uma etiqueta, e o mundo inteiro foi configurado para responder a essa etiqueta com "não" — exceto o trilho.

### Reboque

Duas etiquetas que se procuram: uma diz "preciso de reboque", a outra diz "sei rebocar".

A artilharia de campanha não se desloca sozinha. O caminhão de suprimentos e o trem de carga sabem puxá-la. Uma peça de artilharia sem rebocador por perto é uma peça que ficou onde estava.

## O Que Cada Estrutura Faz

Os domínios de cada estrutura estão declarados em `02_dominios_terrenos_e_ocupacao.md`. Aqui está o que elas fazem com quem passa por cima.

A **estrada** oferece bônus de velocidade para unidades motorizadas que percorrem todo o seu movimento sobre ela. Abre passagem por florestas e montanhas, permitindo que unidades alcancem cidades construídas em terrenos elevados.

Na combinação com planície, a estrada funciona como pista improvisada — aeronaves conseguem pousar para receber reabastecimento de unidades logísticas como o caminhão de suprimentos. Na montanha, essa mesma aeronave não pousa. O terreno cancela o acesso que a estrutura abriria.

A **linha de trem** não dá bônus de velocidade a ninguém, apesar do traçado parecido com o da estrada.

Quanto às **pontes**: a ponte rodoviária carrega o tráfego comum e bloqueia o Trem de Carga. A ponte ferroviária faz o oposto: exige a habilidade de linha de trem, e só o trem atravessa. Uma não substitui a outra.

A escolha de qual ponte construir define rotas inteiras de ataque e defesa — e decide se a sua artéria ferroviária alcança ou não o outro lado da água.

## Combinações que Importam

Você já sabe que estrutura e terreno são avaliados em par. Agora as combinações específicas que mudam o jogo — vale conhecê-las de cabeça, porque nenhuma delas está escrita no tabuleiro.

### O Custo do Terreno

Atravessar não custa igual em todo lugar — e em alguns lugares não é questão de custo, é questão de conseguir.

Planície e praia custam 1. É o padrão contra o qual tudo se compara.

Floresta custa 2. O dobro, para qualquer um — exceto para quem tem treinamento de guerrilha, que atravessa por 1. Tropa feita para o mato não é atrasada pelo mato.

Montanha custa 99. Isso não é uma penalidade cara: é uma parede. Nenhuma unidade tem movimento para pagar esse valor, e o efeito prático é que a montanha simplesmente não é atravessável.

Com exatamente duas exceções:

Tropas alpinas sobem por 2. É a especialidade delas.

Unidades fora-de-estrada sobem por 6. Conseguem, mas gastam quase todo o turno para isso.

Todo o resto não sobe. Não é caro — é impossível. Infantaria sem Alpino, blindados, artilharia e caminhões param no sopé. Vale a ressalva, porque ela muda o planejamento: **no exército atual, toda a infantaria tem Alpino**. Quem para no sopé é o resto da sua força, não a sua tropa a pé.

O Trem de Carga é bloqueado pelo **terreno puro** de floresta e montanha, sem exceção de habilidade — nenhuma etiqueta abre esses dois terrenos para ele. Ele só atravessa qualquer um dos dois onde existir rota ferroviária construída, e é isso que as próximas seções detalham.

### A Estrada Abre a Montanha

É aqui que a estrutura muda o mapa de verdade.

Construída na montanha, a estrada remove a exigência de habilidade. A parede vira passagem: obuses, caminhões de suprimento e veículos comuns — que sozinhos jamais subiriam — passam a alcançar o topo. Rotas que não existiam passam a existir, e é por isso que uma estrada na serra costuma ser a decisão de cenário mais impactante de um mapa.

Mas a montanha cobra o pedágio de duas formas:

Unidades motorizadas pagam 2 em vez de 1. A subida é íngreme mesmo com asfalto.

Ninguém recebe o bônus de estrada. O acréscimo de velocidade que a estrada dá na planície não vale aqui. Você anda por ela, não corre.

Existe uma forma boa de guardar isso: para um veículo, estrada na montanha custa exatamente o que custa uma floresta. É uma floresta em cima de um morro — mesmo preço, mesma lentidão, sem a cobertura.

E some ao que você já sabe da prioridade: a estrutura vence o terreno, então você fica com a posição Desfavorável da estrada em vez da posição Favorável da montanha. O quadro completo é esse — você chegou ao alto, devagar, e sem a proteção que o alto daria.

O consolo é a visão. A elevação da montanha continua sua para efeito de observação. Você enxerga como quem está no topo, mas se defende como quem está na estrada.

### Onde Nasce uma Pista

Estrada e linha de trem, construídas na planície, funcionam como pista improvisada. Aeronaves com a habilidade de pouso apropriada podem descer ali — e cada habilidade traz o seu procedimento: pouso convencional, decolagem curta ou vertical.

Na floresta e na montanha, não. A mesma estrada, o mesmo trilho, e nenhum pouso. O terreno cancela o que a estrutura ofereceria.

Pontes nunca aceitam pouso, de nenhum tipo.

Essa é a regra de prioridade aparecendo em campo: a estrutura propõe, o terreno decide.

### A Linha de Trem e Suas Exceções

O trilho é mais lento que a estrada. Custa o dobro para atravessar, e não dá bônus de velocidade a ninguém. Ele não existe para acelerar tropas — existe para a logística ferroviária.

Mas cada terreno tem sua lista de exceções por habilidade, e são elas que dão sentido ao traçado:

O Trem de Carga atravessa trilho pelo custo mínimo na planície e na floresta. É a via dele.

Tropas de guerrilha também atravessam por 1 nesses dois terrenos. Quem se move fora de estrada usa o leito da ferrovia como trilha.

Todos os outros pagam o preço cheio. Na floresta, quem tem motor paga o dobro do mínimo — passa, mas devagar.

### A Montanha Não Perdoa Nem o Trilho

Na serra, as regras mudam de tom, e vale entender por quê.

Motorizados são **proibidos**. Não é caro: é impossível. Trilho em montanha não é uma rampa para veículo.

O Trem de Carga passa, mas paga **2** em vez de 1. Nem a própria ferrovia torna a montanha barata para ele.

Tropas alpinas passam pagando **2** — exatamente o que pagariam na montanha nua. E é aqui que está o ponto: o trilho não oferece nada à infantaria na serra. Ela não confunde uma ferrovia com uma estrada; continua escalando do mesmo jeito, com trilho ou sem.

A leitura é boa: a estrada domestica a montanha, o trilho não. Uma estrada na serra abre passagem para quem jamais subiria. Uma ferrovia na serra é só uma ferrovia que por acaso está numa montanha — serve ao trem, e mesmo assim a contragosto.

### O Trem Não Segue a Hierarquia

Aqui está a exceção mais peculiar do jogo, e ela merece atenção porque parece um bug quando você a encontra sem saber.

Toda unidade obedece à ordem de prioridade — construção, depois estrutura mais terreno, depois terreno. O Trem de Carga não.

O trem ignora a hierarquia inteira. Ele faz uma pergunta só: o passo que estou dando agora corre sobre um **trecho contínuo** de uma rota de trilho que me aceite?

Repare que a pergunta é sobre o **segmento**, não sobre o hexágono. Não basta haver trilho no destino: o destino precisa estar ligado à célula de onde o trem vem por um trecho declarado da mesma rota. Dois pedaços de ferrovia que se encostam no mapa mas não pertencem à mesma rota contínua não formam caminho — o trem não salta a emenda.

E aqui está a parte que só importa a quem constrói mapas: **a continuidade é declarada, não desenhada.** Uma rota é uma lista ordenada de hexágonos, e o que abre passagem para o trem é dois hexágonos aparecerem **em sequência** nessa lista. Trilho pintado sem rota que o percorra é enfeite.

Isso tem um efeito bom: rotas que se cruzam funcionam sem regra adicional. O hexágono compartilhado pertence às duas listas, então o trem troca de linha ali naturalmente.

E tem um efeito que morde: dois hexágonos vizinhos, ambos com trilho, que nunca aparecem em sequência na mesma rota **não se conectam**. A vizinhança física não cria caminho — só a ordem da lista cria.

Se o segmento existe, ele passa. Se não existe, ele não passa — e não importa o que mais esteja ali.

A consequência que pega todo mundo é a cidade. Uma cidade é perfeitamente transitável para qualquer unidade terrestre. Para o trem, ela é irrelevante: se não houver trilho naquele hexágono, o trem não entra na cidade. A construção não abre caminho para ele, porque ele não está olhando para a construção.

Isso vale nos dois sentidos. A estrada comum bloqueia o trem — ele não anda em asfalto. A ponte rodoviária também. O trem só existe onde existe trilho: linha de trem e ponte ferroviária, e mais nada.

Planejar uma malha ferroviária, portanto, não é decorar o mapa com trilhos. É desenhar uma rota contínua, de ponta a ponta, incluindo dentro das construções que você quer servir. Um trecho faltando no meio de uma cidade corta a linha inteira.
