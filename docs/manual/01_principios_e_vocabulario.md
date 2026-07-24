# Princípios e Vocabulário

*Que tipo de jogo é este e qual vocabulário ele usa.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

Bem vindo comandante ao "The Map Room" Para entender o jogo primeiro você precisa ter uma visão do que ele é

## A Sala de Guerra

Imagine que estamos em uma sala de guerra e no meio da sala, uma grande mesa com um tabuleiro montado, maquetes representando montanhas e terrenos diversos, miniaturas de tanques, navios e aviões, uma grande régua para movimentar as peças, quadro de progressos da missão, pouca luz e uma linha direta com o comando.

Por estar em uma sala de mapas, o que você vê é estratégia pura."

É ali que você está, dando ordens, atualizando a posição da frota a medida que ela avança e registrando por onde a esquadrilha está sobrevoando, registrando o consumo logístico e dando ordens indiretas para suas tropas. Você não está no front de batalha, você está na sala de mapas, no estado maior comandando todas as 3 forças militares. Esse é o "The Map Room"

## Um jogo determinístico

Por estar em uma sala de mapas, não existe aleatoriedade, grind, rng nem dailies. Nada aqui é sorteado: as mesmas condições produzem sempre o mesmo resultado.

Quando este manual diz que não há percentual, ele fala de **probabilidade** — não existe "70% de chance de acertar", não existe rolagem escondida. Porcentagens de proporção existem e são usadas à vontade nos custos — quanto um reparo cobra sobre o valor da unidade, por exemplo. Essas são contas fixas, não apostas. No combate não há percentual nenhum.

Também não existem clima, tempo, ataque de oportunidade nem os elementos de RPG.

Duas ausências, porém, merecem ser ditas com mais cuidado, porque não são ausências de verdade — são absorções.

Cobertura, flanqueamento e exposição não têm modificadores próprios. Seus efeitos foram consolidados em Posição, domínio, alcance e DPQ. Estar na floresta protege; estar melhor colocado que o inimigo dá vantagem; estar no domínio errado te expõe. O que não existe é um sistema separado para cada uma dessas ideias.

Veterania adquirida também não existe. Nenhuma unidade ganha experiência, sobe de nível ou melhora ao longo da partida. O que existe são formações que já nascem de elite em confrontos específicos — o que é uma descrição da unidade, não uma progressão dela.

## O Tabuleiro

Aqui o tabuleiro representa um mapa, o hexagono é grande o bastante para caber uma cidade inteira, ou uma frota de porta aviões. e você tem uma representação de qual força está ocupando aquele setor no momento. Então se você vê um caça parado em um hexágono entre turnos, ele não está parado no ar, ele está ocupando aquele setor ou território que pode se extender em vários kilometros. Uma vez que você conseguiu abstrair o que é o "The Map Room" é hora de mergulhar nos conceitos. O primeiro deles, antes de falar de unidade, são os **Domínios** — as camadas em que cada coisa existe no tabuleiro. Eles abrem o próximo documento, `02_dominios_terrenos_e_ocupacao.md`. Aqui ficam o vocabulário e os princípios que você vai levar para lá.

## Habilidades

Uma habilidade não é um poder. É uma chave.

O nome que aparece na ficha — alpino, guerrilha, fora-de-estrada — não faz nada sozinho. Não concede bônus, não altera atributo, não dispara efeito. É um rótulo, e só.

Quem dá sentido a ele é o resto do mundo. A montanha diz "só entra quem for alpino ou fora-de-estrada, e o alpino atravessa por 2". A floresta diz "guerrilha passa por 1". A ponte ferroviária diz "só passa quem for da linha de trem". Cada lugar pendura a mesma etiqueta e define, ali, o que ela significa.

A consequência prática é essa: ver uma habilidade na ficha não te diz o que a unidade faz. Te diz onde procurar. Alpino não significa "escala bem" em abstrato — significa que existem lugares no mapa que perguntam por alpino, e nesses lugares essa unidade tem resposta.

### Por Que Isso Importa Para Você

Porque o jogo não cresce em regras — cresce em etiquetas.

Quando uma unidade nova entra, ela não traz mecânica nova. Ela traz um conjunto de chaves, e o mundo já sabe responder a elas. Um novo veículo com fora-de-estrada sobe montanha porque a montanha já pergunta por isso, não porque alguém escreveu uma exceção para ele.

E para você, na hora de montar exército, a leitura vira uma pergunta só: **quais portas do mapa eu consigo abrir?** Um exército inteiro de blindados é poderoso e não sobe montanha, não corta floresta, não anda em trilho e não captura nada. Ele ganha todos os combates que conseguir alcançar — e o mapa decide quantos serão.

## Classes

No The Map Room não dizemos que um MBT enfrentou um Leopard, ou que um F-14 disparou um heat seeker num bombardeiro. Dizemos que um Blindado atacou um Blindado com armas antitanque. Que jatos usaram antiaérea contra aviões.

O sistema enxerga conjuntos — classes de unidades e classes de armas. Se uma nova unidade for criada no futuro, basta encaixá-la na classe correspondente. O sistema já sabe como ela combate.

### Classes de Unidades

Existem 9 classes de unidades, distribuídas pelas três forças:

| Força | Classes |
|---|---|
| Exército | Infantaria · Veículos · Blindados · Artilharia |
| Aeronáutica | Jatos · Aviões · Helicópteros |
| Marinha | Navios · Submarinos |

A separação entre Veículos e Blindados é intencional — não é sobre rodas versus lagarta, mas sobre o conjunto de qualidades de combate e operacionais que tanques possuem em relação a veículos comuns. Da mesma forma, todo jato é um avião — mas jatos e aviões se comportam de maneiras distintas em combate. A classe captura esse comportamento, não a descrição física.

### Classes de Armas

As armas se dividem em quatro classes:

Antiaérea · Antitanque · Antiinfantaria · Antinavio

Construções, terrenos e estruturas são indestrutíveis no The Map Room — então a categoria de armas antiestrutura simplesmente não existe no sistema. Não há alvo para ela.

Cuidado para não confundir: mísseis de cruzeiro existem, e navios os carregam contra alvos navais. O que não existe é uma arma cujo alvo seja o prédio. Nenhum armamento do jogo derruba uma cidade, uma fábrica ou uma ponte — construções só mudam de dono por captura.

O comportamento é consistente com tudo que vimos até agora: o sistema não distingue entre um Sidewinder e um Patriot. Ambos são antiaérea. O nome da arma é temático — o que importa é a classe.

### Por que isso importa

Classes de unidades e classes de armas são o vocabulário do combate. Quando chegar no sistema de RPS — como cada classe de arma se comporta contra cada classe de unidade — você já vai ter o vocabulário necessário para entender o que está sendo dito sem precisar decorar nomes de equipamento militar.

## Classe de Armadura e de Potência

"Eu ouvi plate mail +5?" — jogador de RPG frustrado pelo sistema determinista

O nome lembra armadura de RPG, mas aqui ela cobre algo mais simples: uma faixa de valores que classifica a força do ataque de uma arma e a capacidade defensiva de uma unidade. Não é magia, não é percentual — é uma leitura da ficha.

### As Classes

| Classe | Armadura (Defesa) | Potência (Ataque) |
|---|---|---|
| Leve | 8 a 11 | 4 a 6 |
| Média | 12 a 14 | 7 e 8 |
| Pesada | 15 a 17 | 9 e 10 |

Nenhuma dessas classes é escolhida à mão. Elas são **derivadas** dos números que já estão na ficha: a armadura sai da defesa da unidade, a potência sai do ataque da arma. Definir a classe e definir o valor são a mesma ação.

### As Duas Classes Não Se Limitam

Aqui cabe desfazer uma expectativa que o nome cria, porque ela é forte e está errada: **a classe de armadura não é um teto para a arma que a unidade carrega.** As duas classificações são independentes. Uma plataforma leve pode operar armamento pesado, e isso não é uma exceção concedida — é simplesmente uma combinação que o sistema nunca proibiu.

O exemplo mais didático é a **AAA**: defesa 10, Armadura Leve, operando um Auto Gun de ataque 10, Potência Pesada. Uma peça sem blindagem carregando um canhão sério. É exatamente o que uma antiaérea rebocada é no mundo — poder de fogo alto sobre uma plataforma que não sobrevive a nada. O Astros II e o Obus Leve contam a mesma história: artilharia é potência barata montada em cima de fragilidade.

Então para que servem as classes, se não limitam nada? Para duas coisas concretas, e as duas aparecem mais adiante. A classe de **armadura** decide quanto uma unidade aproveita de cada ponto de reserva — blindagem pesada consome mais para andar o mesmo tanto. A classe de **potência** decide quanto pesa cada projétil reposto — munição pesada custa o triplo da leve.

A leitura correta, portanto, é econômica, não operacional: a classe não te diz o que a unidade pode fazer. Te diz **quanto ela custa para manter em campo**.

### Na Prática

O Soldado tem defesa 10 — Armadura Leve. Opera um rifle antiinfantaria com ataque 4 — Potência Leve. Barato de eliminar, barato de sustentar.

O Bazooka tem defesa 12 — Armadura Média — e opera LAW antitanque e Stinger antiaéreo, ambos com ataque 5, Potência Leve. Mais duro de matar que o Soldado, e ainda assim barato de rearmar. As duas classes andam separadas justamente porque medem coisas diferentes.

### Por Que o Caça A É Pesado?

Defesa 15, Armadura Pesada. A primeira imagem que vem à cabeça é metal, escudo, carapaça. Mas para a força aérea, manobrabilidade, velocidade e dificuldade de ser atingido também contribuem para uma defesa alta. Afinal — não é exatamente assim com o monk no D&D?

Armadura Pesada não descreve o quanto de metal você carrega. Descreve o quão difícil você é de eliminar.

### Eliminação, Não Dano

Classe de Armadura e Classe de Potência são também a chave para entender uma distinção fundamental do The Map Room: o combate não calcula dano.

Ele calcula eliminações.

Cada lado descobre quantas baixas impõe ao outro. Não há pontos de vida sendo corroídos gradualmente — há um confronto com um resultado. Essa distinção vai ficar clara quando chegar no capítulo de combate, mas vale registrar agora: o vocabulário que você está aprendendo não serve para calcular feridas. Serve para calcular desfechos.

## Esquadrão como HP

"Não! Não é ponto de vida!"

O ícone que aparece na tela puxa a leitura imediata para ponto de vida. Mas o que ele representa é outra coisa — o número de membros vivos dentro do token que você está vendo naquele momento.

Ao ver um Soldado marcando 10 nesse indicador, você não está vendo um soldado com 10 pontos de vida. Você está vendo 10 soldados reunidos, representados por aquele único token na tela. O mesmo vale para aviões, blindados, navios — qualquer esquadrão do jogo.

### O Esquadrão em Combate

Uma unidade com HP completo opera na capacidade máxima. Uma unidade com HP baixo está com o esquadrão desgastado — menos membros, menos eficiência.

Isso é diretamente refletido na força de ataque:

Força de Ataque = HP × Potência da Arma

Dez soldados atirando são mais letais que três. A matemática é simples — e faz sentido no mundo.

### O Que Você Está Comprando?

Quando você compra um esquadrão de Soldados por $1.000, não comprou um soldado com 10 pontos de vida. Comprou 10 soldados a $100 cada.

Pense num copo com 10 bolas de gude virado de cabeça para baixo, deslizando pelo tabuleiro. Quando esse esquadrão entrar em combate e sofrer 2 eliminações, o copo passa a ter 8 bolas. O token continua lá — mas o esquadrão está menor, e ataca como esquadrão menor.

### O Que Isso Muda na Leitura do Jogo

Com essa ideia em mente, os próximos atributos de uma unidade vão fazer mais sentido. Movimento é o movimento do esquadrão, não de cada membro. Autonomia é a autonomia do conjunto. Munição é por esquadrão, não por soldado.

Você está sempre vendo o conjunto — nunca os indivíduos.

## Elite — Diferenciando Unidades Caras

Elite não é experiência. É vocação.

Uma unidade com elite alto contra jatos não voou mais horas que as outras. Ela foi projetada, equipada e doutrinada para esse confronto. O Caça A é interceptador: a superioridade dele contra aeronaves está no projeto, não no histórico.

Por isso o Elite é sempre **contra alguma coisa**. Não existe unidade "de elite" em abstrato. Existe unidade que é excepcional numa luta específica e perfeitamente comum em todas as outras — e é essa assimetria que você compra quando paga mais caro.

O sistema de Elite, portanto, não é um número solto que indica tropa boa ou ruim. Ele é uma condição — e só age quando existe diferença de nível entre os combatentes.

E ele não contradiz o que foi dito lá no início. Nenhuma unidade sobe de nível durante a partida: o Caça A não fica melhor porque venceu duelos, e não fica pior porque apanhou. O nível de elite descreve o que aquela formação já era quando você a comprou — projeto, doutrina e equipamento, não experiência acumulada em campo.

Dois Caças A lutando entre si — ambos Elite 1 — não recebem bônus de elite. Os níveis se cancelam, e o confronto se resolve como qualquer combate simétrico. O mesmo vale para dois Caças B, ambos Elite 0: sem diferença de nível, o Elite não entra na conta.

O Elite só existe na diferença. Quando o Caça A enfrenta o Caça B — Elite 1 contra Elite 0, mesma classe, mesma arma, mesmo domínio — o combate deixa de ser simétrico e passa a favorecer o especialista de forma acentuada.

Uma ressalva honesta, porque ela evita uma leitura errada: o Elite não é a única diferença entre os dois. O Caça A também tem armadura superior à do Caça B — é mais difícil de eliminar antes mesmo do Elite entrar na conta. A diferença de 10.000 no custo paga as duas coisas juntas. O que o Elite acrescenta é a assimetria: mesmo que as armaduras fossem idênticas, a diferença de nível sozinha já inclinaria o confronto.

Uma observação importante: Elite é específico de classe e arma. O modificador de Dog Fight do Caça A se aplica ao confronto entre jatos — não contra um SAM antiaéreo, não contra Infantaria em terra. Nesses casos, o Elite simplesmente não entra no cálculo, a menos que a unidade tenha uma especialização adicional para aquele confronto específico.

Especialista em duelo aéreo não é especialista em tudo. Ele é excepcional exatamente naquilo para que foi projetado — e perfeitamente comum em todo o resto.

O objetivo aqui é só estabelecer o conceito antes de entrar nas contas. O que importa entender agora: Elite não é progressão de personagem. É um multiplicador de assimetria que só existe quando há desequilíbrio de nível — e quando existe, é brutal.
