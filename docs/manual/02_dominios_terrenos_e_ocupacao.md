# Domínios, Terrenos e Ocupação

*Onde cada coisa existe e quem pode ocupar ou atravessar cada setor.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## Domínios

Tal como na natureza, domínios classificam o ambiente onde as unidades operam e as balas voam. São três grandes domínios — ar, terra e mar — cada um subdividido em altitudes que definem com precisão onde cada unidade existe no mundo.

O domínio do Ar se divide em duas camadas. As Altas Altitudes (Air/High) — ar rarefeito, altas velocidades, onde operam caças e bombardeiros. E as Baixas Altitudes (Air/Low) — arrasto ideal para helicópteros e turboélices.

O domínio da Terra (Land) contém apenas a Superfície (Land/Surface), onde operam todas as unidades do exército — planícies, florestas, e as construções humanas como fábricas, cidades e estradas.

O domínio do Mar (Naval) agrupa dois subdomínios. O Nível do Mar (Naval/Surface), onde navegam as unidades da marinha e se localizam construções como portos navais. E as Águas Profundas (Submarine/Submerged), onde operam unidades abaixo d'água como o Submarino.

Se classificássemos em altitude como um bolo em camadas, teríamos do topo à base:

Air/High » Air/Low » Land/Surface · Naval/Surface » Submarine/Submerged

## Terrenos

O mundo do jogo é organizado em terrenos — e cada terreno pertence a um domínio. Isso determina quem pode entrar, operar e sobreviver em cada parte do mapa.

Planície — campos abertos. Sem obstáculos, sem cobertura. Favorece velocidade e visibilidade. Land/Surface.

Floresta — árvores densas que dificultam o movimento e bloqueiam a linha de visão. Cobertura natural para infantaria. Land/Surface.

Montanha — obstáculo natural intransponível para a maioria das unidades. Para quem consegue escalar, oferece as melhores condições de observação e defesa do jogo. Land/Surface.

Praia — a fronteira entre dois domínios. Terreno instável para blindados, porta de entrada para operações anfíbias. Naval/Surface.

Mar — terreno especial que abriga dois subdomínios simultaneamente. Na superfície operam os navios. Nas profundezas, os submarinos. Naval/Surface e Submarine/Submerged coexistem no mesmo hexágono.

Espaço Aéreo — não é um terreno jogável, mas está presente sobre todos os outros. Divide-se em duas camadas: Air/Low para helicópteros e turboélices, Air/High para caças e bombardeiros.

Uma unidade opera apenas no domínio para o qual foi projetada. Um tanque não entra na praia. Um navio não sobe uma montanha. Mas algumas unidades cruzam domínios conforme a situação — e isso é onde o jogo fica interessante.

Um helicóptero pousado desembarcando tropas está em Land/Surface. Quando decola, passa para Air/Low. Um caça pode operar em Land/Surface quando pousado, Air/Low em trânsito, e Air/High em missão. Um submarino navega em Naval/Surface quando está na superfície — e mergulha para Submarine/Submerged quando some das telas de radar inimigas.

O domínio não é só onde a unidade está. É o que ela consegue fazer — e o que o inimigo consegue fazer contra ela.

## Construções

Quando a humanidade modifica o terreno, as regras mudam.

Uma construção sobrescreve o domínio do terreno onde foi erguida — e pode aceitar domínios que o terreno original não aceitava. Isso cria situações táticas que só existem porque alguém construiu algo ali.

Cidade — Land/Surface. Se construída sobre uma praia, a cidade sobrescreve o domínio naval do terreno. O hexágono passa a ser Land/Surface: canhões estacionam, navios não entram. A engenharia humana fechou o porto natural.

Fábrica — Land/Surface. Produção e reparo de unidades terrestres. Quem controla a fábrica controla a capacidade de reposição do front.

Porto Naval — construído na praia, aceita tanto Land/Surface quanto Naval/Surface. É a construção que mantém os dois domínios abertos onde o terreno sozinho só teria um: ali cabe uma tropa, ou cabe um navio.

Cuidado com a palavra "ambos": o porto aceita os dois domínios, mas não os dois ao mesmo tempo. A superfície é um andar só — se há um navio atracado, não entra tropa; se há tropa no porto, não entra navio. O porto amplia suas opções, não a lotação do hexágono.

Aeroporto — Land/Surface. A ponte entre o ar e o solo. Uma aeronave não recebe manutenção em Air/Low ou Air/High — ela precisa pousar, igualar o domínio ao da construção, para então receber reparos, combustível e munição. Voar tem custo logístico. O aeroporto é onde esse custo é pago.

Uma observação sobre submarinos: a praia não aceita o domínio Submerged — nem sozinha, nem com porto construído sobre ela. Um submarino que se aproxima da costa é forçado a emergir para Naval/Surface. Não há profundidade suficiente para operar submerso. A geografia impõe a limitação antes de qualquer regra de jogo.

## Estruturas

Construções modificam o domínio do terreno. Estruturas não — elas trabalham sobre o terreno, melhorando acesso e mobilidade sem alterar a natureza do hexágono.

A regra fundamental das estruturas: elas são sempre avaliadas em par com o terreno onde estão. Uma estrada na planície e uma estrada na montanha são combinações diferentes com resultados diferentes. Quando quiser saber se uma ação é possível, pergunte: qual estrutura, em qual terreno?

Estrada — Land/Surface.

Pontes e Pontes Ferroviárias — Land/Surface e Naval/Surface. Construídas sobre praias e travessias marítimas, interligam territórios que estradas sozinhas não alcançam — estradas não podem ser construídas em praias por diferença de domínio.

Pontes existem em duas variantes, e a diferença entre elas não é altura — é quem passa por cima.

Por baixo, as duas se comportam igual: ambas permitem passagem de Naval/Surface e de Submarine/Submerged. Navios cruzam por baixo, submarinos submersos também.

Linha de Trem — Land/Surface. Similar à estrada em traçado. Existe exclusivamente para operação do Trem de Carga — a artéria logística que conecta a base ao front em mapas grandes.

> **Custos, permissões e combinações não moram aqui.** Este documento declara em que domínio cada estrutura existe. Quanto custa atravessar, quem tem permissão de passar, qual bônus se aplica e o que cada par estrutura+terreno habilita está em `03_movimento_terreno_e_infraestrutura.md`.

## Ordem de Prioridade

Quando um hexágono contém terreno, estrutura e construção ao mesmo tempo, uma regra simples determina o que vale:

Construção vem primeiro. Estrutura + terreno vem em segundo. Terreno sozinho vem por último.

Na prática, isso significa que a unidade está no elemento de maior prioridade — e é ele que define defesa, acesso e operação.

Uma unidade numa cidade com estrada está na cidade — recebe a proteção da construção, não o bônus ou penalidade da estrada. Uma unidade numa estrada dentro da floresta está na estrada — a floresta não a protege, embora os atributos físicos do terreno, como a elevação para spotters, continuem acessíveis. Uma unidade na floresta sem estrutura ou construção está no terreno — fazendo camping.

Estruturas por si só são desfavoráveis à defesa. Uma estrada na montanha dá acesso ao terreno elevado e mantém os atributos de visão da montanha — mas não oferece a proteção dela. Você chegou lá, mas ficou exposto.

A exceção que confirma a regra: o Trem de Carga é a única unidade que exige a presença de Linha de Trem na construção para operar. As demais unidades seguem a hierarquia naturalmente — a prioridade determina o contexto, não bloqueia o acesso.

## A Unidade e o Domínio

Unidades militares são projetadas para operar em domínios específicos — e não cruzam para domínios para os quais não foram projetadas. Esse é o princípio que você já conhece.

A exceção é o embarque. Uma unidade embarcada em um transportador adota temporariamente o domínio dele. Tropas dentro de um helicóptero atravessam Air/Low. Infantaria num navio cruza Naval/Surface. A unidade não mudou — ela está no mundo do seu transportador enquanto durar a viagem.

Toda unidade possui atributos que definem como ela existe no tabuleiro — movimento, defesa, visão, entre outros. Mas uma unidade, por si só, não ataca. O ataque vem das armas embarcadas nela.

Pense assim: o tanque é uma plataforma. O canhão é a arma. A plataforma define onde você vai e como você sobrevive. A arma define o que você pode destruir e como.

Essa separação é fundamental para entender o jogo — e vai ficar mais clara quando chegarmos nos atributos e no sistema de combate. Por ora, o que importa saber é que unidade e arma são conceitos distintos, mesmo quando andam juntos.

## As armas e o Domínio

Tal como as presas de uma águia, as armas definem como uma unidade interage com o mundo — e com o inimigo.

Cada arma foi projetada para um domínio de alvo. Ela só funciona contra unidades que operam nesse domínio. O míssil antiaéreo existe no espaço do ar — Air/Low e Air/High. Embarcado num SAM terrestre, ele alcança o céu a partir do solo. Mas não pode ser usado contra um tanque ou um navio. A arma sabe onde está seu território.

O torpedo existe nos domínios Naval/Surface e Submarine/Submerged. Um Super Tucano pode carregá-lo e dispará-lo a partir do ar — mas só contra alvos na água. Contra um tanque em terra ou um caça no ar, o torpedo é inútil. Plataforma aérea, arma naval, alvo aquático. A combinação precisa fazer sentido.

Há uma segunda restrição além do domínio do alvo: o domínio atual da unidade que dispara.

Um caça carrega mísseis heat seeker do domínio aéreo. Pousado para manutenção, ele está em Land/Surface — e não consegue disparar. A arma existe para o ar, e o caça momentaneamente não está no ar. Quando decolar, os mísseis voltam a estar disponíveis.

Nem todas as unidades têm essa restrição. Mas onde ela existe, ela é intencional — e tematicamente correta. Armas têm contexto de uso. Fora do contexto, ficam silenciosas.

A lógica funciona nos dois sentidos. Se uma unidade fora do seu domínio natural perde acesso a certas armas, ela também fica exposta a armas que normalmente não a alcançariam.

A Artilharia de Campanha ataca Land/Surface e Naval/Surface. Caças em Air/High estão fora do seu alcance — a artilharia não foi projetada para derrubar aviões. Mas um caça que pousa para manutenção desce para Land/Surface. Nesse momento, ele está no domínio da artilharia. Canhões de campanha e tanques podem atingi-lo normalmente.

O domínio não é só onde você opera. É o que te protege — e o que te expõe.

## Domínio — Fechamento

Tudo que você viu até agora — terrenos, construções, estruturas, unidades, armas, visão e tiro — gira em torno de um único eixo: o domínio.

Cinco camadas definem o mundo:

Air/High » Air/Low » Land/Surface · Naval/Surface » Submarine/Submerged

Cada elemento do jogo pertence a uma ou mais dessas camadas. E as regras emergem naturalmente dessa pertença — sem exceções arbitrárias, sem casos especiais inventados. Quando algo parece estranho, a resposta quase sempre está no domínio.

Um tanque não entra na praia — diferença de domínio.

Um torpedo não acerta um caça — diferença de domínio.

Um caça pousado pode ser destruído por artilharia — mesmo domínio.

Um submarino na praia emerge — o terreno não suporta o domínio submerso.

Uma cidade construída na praia fecha o acesso naval — a construção sobrescreveu o domínio.

Um torpedo não atravessa uma península — a arma perdeu seu domínio no meio do caminho.

O relevo não esconde o caça em Air/High — mas o alcance ainda limita quem o vê, e a arma certa limita quem o atinge.

Você não precisa memorizar regras para cada situação. Você precisa entender em qual domínio cada coisa existe — e o resto se deduz.

Esse é o alicerce do jogo. Tudo que vem a seguir é construído sobre ele.

## Dividindo o Hexágono

Se o hexágono comporta uma cidade inteira, ele obviamente comporta mais de uma unidade. Mas não de qualquer jeito.

Cada hexágono tem três andares operacionais:

O ar. Qualquer aeronave, em qualquer altitude.

A superfície. Terra e mar ao nível do solo, juntos no mesmo andar.

As profundezas. Submarinos submersos.

Como cada andar comporta uma presença, o máximo que um hexágono exibe são três unidades — e o tabuleiro as desenha nessa mesma ordem, de cima para baixo. Quando você vir uma pilha, leia a posição vertical: quem está em cima está no ar, quem está no meio está na superfície, quem está embaixo está submerso. A pilha não é amontoado; é o corte transversal do setor.

### Altitude Não Cria Andar

Aqui está a regra que mais surpreende: **Air/Low e Air/High são o mesmo andar para efeito de ocupação.**

Um helicóptero em baixa altitude e um caça em alta altitude não dividem o hexágono. Para o tabuleiro, os dois estão "no ar sobre aquele setor", e o setor aéreo comporta uma presença.

A altitude importa para visão, para posição, para quais armas te alcançam e para onde sua furtividade funciona. Para ocupação, não. Voar mais alto não abre uma vaga nova.

### A Superfície É o Andar que Trava

Dos três, só a superfície bloqueia passagem — e é por isso que ela é o andar disputado.

Uma unidade inimiga na superfície impede que outra unidade de superfície atravesse aquele hexágono. Não dá para passar por dentro de uma linha inimiga.

Os outros dois andares não travam nada. Uma aeronave sobrevoa tropas inimigas livremente. Um submarino passa por baixo de uma frota inimiga sem pedir licença. E qualquer unidade cruza um hexágono ocupado por alguém de outro andar, sempre.

Isso desenha a geografia real da guerra: **a linha de frente existe apenas na superfície.** O céu e o fundo do mar não têm frente — têm alcance e detecção.

### A Ponte Sobre o Mar É Dois Andares

Há uma exceção, e ela é física, não arbitrária: a **ponte sobre o mar**. Ali o convés fica acima da água, então terra e mar deixam de ser o mesmo andar naquele hexágono. Um tanque para em cima da ponte enquanto um navio passa por baixo — os dois coexistem, como coexistiriam um avião e um submarino. É o único lugar onde "superfície" se divide em dois.

A ponte sobre a **praia** não faz isso. Ali a ponte encosta no chão — é a cabeceira, com aterro e estacas, não vão. Aquele hexágono continua sendo um andar só, e navio e tanque voltam a disputá-lo. Por isso o navio nem atraca numa praia com ponte: não há água navegável embaixo, há a base da obra.

A regra geral que fica: fora da ponte sobre o mar, terra e mar ao nível do solo são sempre o mesmo andar — é o que impede um navio e um tanque de dividirem uma praia comum.

### Aliado Nunca Barra o Caminho

Uma unidade sua jamais impede a passagem de outra unidade sua. Você atravessa suas próprias tropas à vontade.

O que continua valendo é onde você **termina** o movimento: dois aliados não param no mesmo andar do mesmo hexágono. Passar por cima do companheiro, sim. Acampar em cima dele, não.

Isso evita o problema clássico de embaralhar a própria linha e ficar preso atrás das suas peças, sem transformar o hexágono num depósito infinito de tropa.
