# Visão, Detecção e Névoa de Guerra

*O que o jogador sabe e por que sabe.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## Linha de Visão e Linha de Tiro

"Mire nos olhos, príncipe — ele não pode atacar o que não pode ver." — Prince of Persia: Sands of Time

A citação vale para o The Map Room também. Ver o alvo é condição para atacá-lo. Mas ver não é suficiente — a trajetória até ele também precisa ser válida. São dois sistemas distintos, construídos sobre o mesmo princípio de domínios.

### Linha de Visão

A visão sempre considera uma linha reta entre o atirador e o alvo. O hexágono onde você está e os vizinhos imediatos são sempre visíveis — independente de elevação ou terreno. Você sempre sabe o que está ao redor. Isso não significa que detecta unidades furtivas, mas que o espaço próximo nunca é cego.

Para além do alcance imediato, a elevação define o que você enxerga. E aqui é preciso separar duas coisas que o senso comum funde: **a altura que um lugar tem como obstáculo** e **a elevação que ele concede a quem está em cima dele**. Quase sempre coincidem. Numa delas, não.

| Camada | Altura como obstáculo | Bloqueia linha de visão | Elevação concedida |
|---|---|---|---|
| Planície · Mar · Praia | 0 | não | 0 |
| Floresta | 1 | sim | 0 |
| **Montanha** | **2,25** | sim | **2** |
| Air/Low | — | não | 3 |
| Air/High | — | não | 4 |

Planície e mar não obstruem nada. Um disparo reto ou parabólico atravessa sem impedimento.

Floresta bloqueia, mas não eleva. Quem está na planície vê a floresta adjacente — e não vê o que está atrás dela. Quem está **dentro** da floresta também não enxerga melhor por isso: a copa das árvores atrapalha os outros, não ajuda você.

Montanha é o caso que decide batalhas, e agora dá para enunciá-lo com número. Quem está no alto herda elevação **2** e passa a enxergar sobre florestas e planícies. Mas a montanha, **como obstáculo, tem 2,25** — um degrau a mais do que ela concede. O resultado: dois picos não se enxergam por cima de um terceiro no meio do caminho, porque a linha entre eles corre a 2 e a serra intermediária está a 2,25. A montanha bloqueia mais do que ela própria alcança, e é por isso que uma cadeia de montanhas cria corredores cegos até entre unidades igualmente elevadas.

Air/Low concede elevação 3 a quem está lá. É por isso que helicópteros e turboélices enxergam sobre praticamente tudo abaixo deles: a linha de visão deles **parte** de três unidades de altura, e passa por cima de floresta e montanha sem ser interrompida.

Repare que a coluna de obstáculo do espaço aéreo está vazia, e isso não é descuido. Vale a regra mais importante desta seção, e ela vale para o jogo inteiro:

**Unidade nunca é obstáculo.** Só o mundo bloqueia linha de visão — terreno, construção e estrutura. Nenhuma unidade projeta sombra sobre outra, em nenhuma camada. Um helicóptero pairando entre você e o alvo não corta a sua linha; um porta-aviões parado no meio do canal também não. Eles ocupam o setor, não o interrompem.

A leitura tática é limpa e evita um erro comum de planejamento: você nunca ganha cobertura se escondendo **atrás de uma unidade**, amiga ou inimiga. Cobertura vem do relevo. Se quiser sumir, procure floresta, montanha ou o outro lado da serra — não a sombra de um aliado.

Air/High também nunca é obstáculo intermediário: nada se esconde atrás dela.

Cuidado com a recíproca, porque ela não vale. Não ser obstáculo não significa estar sempre à vista — um alvo em Air/High continua sujeito à geometria do relevo quando quem procura é um sensor de superfície. Só **aeronave olhando para o alto** dispensa a linha de visão contra alvos em Air/High, e isso vale apenas para detecção. A regra completa está em *O Céu Não Tem Sombra*, mais adiante neste documento.

Cuidado com a conclusão fácil: isso não significa que todo mundo enxerga o que está lá em cima. O alcance de visão continua valendo integralmente. Um soldado com visão 3 não vê um caça a dez hexágonos, por mais limpo que esteja o céu. Não bloquear e ser visto são coisas diferentes.

E mesmo quando você enxerga, ver não significa alcançar. Essa é a próxima questão.

### Linha de Tiro

Você viu o alvo. Agora precisa saber se consegue atingi-lo.

Para disparos parabólicos, a linha de tiro direta não é avaliada. A trajetória vai por cima dos obstáculos. Mas se o alvo estiver além da sua visão, você precisa de um observador avançado que veja onde ele está e transmita a posição. Sem olhos no alvo, a artilharia atira às cegas.

Qualquer unidade sua serve de observador — e as aeronaves são especialmente boas nisso. Um helicóptero pairando sobre a linha inimiga enxerga o que a artilharia não alcança e entrega o alvo para ela. É uma das parcerias mais fortes do jogo: quem vê não precisa ser quem atira.

Há um limite, e ele é preciso: observação avançada vale para alvos em terra, na superfície do mar e submersos. Não vale para alvos aéreos. Ninguém aponta um caça para a artilharia de outra pessoa — contra o céu, cada unidade depende dos próprios olhos.

Não pense em parabólico como sinônimo de artilharia. Mísseis antiaéreos de longo alcance e mísseis de cruzeiro também são parabólicos aqui.

E vale dizer o que a palavra significa neste manual: parabólico é o termo operacional do sistema para toda arma que **ignora os obstáculos do percurso**. Não é uma descrição balística rigorosa, e não vale discutir o perfil real de voo de cada munição. O que ela declara é uma coisa só: se essa arma alcança, o relevo entre você e o alvo não importa.

E há uma sutileza que muda a leitura de várias unidades: a trajetória pertence à arma como ela foi montada naquela unidade, não ao tipo de arma. O mesmo foguete pode ser parabólico de longo alcance numa plataforma de artilharia e reto de alcance curto num helicóptero de ataque. Duas unidades que carregam "o mesmo foguete" podem jogar de formas opostas.

Para disparos retos — canhões, torpedos, mísseis de trajetória direta — a arma precisa atravessar todos os domínios válidos no caminho até o alvo. Um torpedo opera em Naval/Surface e Submarine/Submerged. Se entre o atirador e o alvo existe uma faixa de terra, o torpedo não tem por onde passar. A península bloqueia não porque é um obstáculo físico no sentido do jogo — mas porque o domínio da arma não existe naquele trecho do trajeto.

O domínio não define só onde você ataca. Define por onde o ataque pode viajar.

### Onde o Combate Pergunta

Ocultação funciona pelo mesmo princípio, e isso explica uma regra que confunde muita gente.

A unidade furtiva carrega uma etiqueta de ocultação. O sensor não diz "eu detecto furtivos" — ele diz "eu detecto quem tiver esta etiqueta específica". São listas que precisam bater.

Por isso não existe detecção genérica de furtivo. O radar que encontra um caça furtivo não encontra um submarino, e não é por falta de alcance: é porque ele pergunta por uma etiqueta que o submarino não tem. São dois mundos de ocultação que não se cruzam, e você precisa de equipamento próprio para cada um.

## Névoa de Guerra

Você comanda de uma sala de mapas. Só existe no seu tabuleiro aquilo que alguém seu está vendo agora.

Essa é a última camada do jogo, e a que transforma tudo que você aprendeu numa disputa de verdade. Posição, alcance e RPS decidem combates. Informação decide se o combate acontece nos seus termos.

### O Que Você Já Viu, Você Lembra

Antes de tudo, uma distinção que muda como o mapa se comporta: existe diferença entre o que você **vê agora** e o que você **já viu**.

Terreno que suas unidades revelaram uma vez não volta ao breu quando elas se afastam. Ele fica como uma fotografia: o relevo continua desenhado no mapa, porque terreno não anda e não muda. Uma montanha que você viu no turno 3 ainda estará ali no turno 30, tenha você tropas por perto ou não.

O mesmo vale para as construções que você já avistou. Elas permanecem no mapa com o dono que tinham **no momento em que você as viu por último**. É memória, não vigilância.

### A Fotografia Pode Estar Velha

E aqui está a parte que engana: a lembrança não se atualiza sozinha.

Se você viu uma cidade inimiga no turno 3 e saiu de perto, ela continua marcada como inimiga — mesmo que o dono dela tenha mudado três vezes desde então. A fotografia mostra o que você sabia, não o que é verdade agora. Para atualizar, é preciso voltar a ter olhos sobre o hexágono.

Isso é honesto com a névoa, não uma brecha nela. O jogo não te dá de graça a informação que você não colheu; ele apenas não apaga a que você já colheu. Terreno é seguro lembrar, porque não muda. Dono de prédio é perigoso lembrar, porque muda — e a sua memória pode estar mentindo sem saber.

E isso vale inclusive para os prédios que eram **seus**. Quando uma construção sua é tomada, ela deixa de te dar visão no mesmo instante em que troca de dono — e a fotografia congela com a sua cor. Você vai continuar vendo aquele hexágono pintado como se fosse seu, até mandar alguém olhar.

Guarde esta separação, porque ela vai aparecer de novo no Jornal do Comandante: **o Jornal registra o que você ficou sabendo; o tabuleiro registra a última coisa que você viu.** Saber que perdeu o prédio não repinta o hexágono. São duas fontes de informação com relógios diferentes, e a mesa é sempre a mais lenta das duas.

E vale o mesmo que sempre valeu: lembrar o terreno não é lembrar quem está nele. A fotografia mostra o cenário parado. Quem se move por cima dele continua invisível até ser detectado de novo.

### Ver Não É Uma Coisa Só

Duas perguntas diferentes se escondem sob a palavra "ver":

O hexágono está revelado? — é o terreno, o que existe no mapa.

A unidade está detectada? — é o inimigo, quem está ocupando aquele setor.

Elas não andam sempre juntas. Você pode enxergar um hexágono perfeitamente e não fazer ideia de que há um submarino embaixo dele.

As construções são o exemplo mais claro dessa separação, e vale enunciá-la como matriz, porque os dois alcances são diferentes e independentes:

| O que a construção faz | Alcance |
|---|---|
| Revela terreno | o raio de visão dela |
| Detecta unidade inimiga | apenas o próprio hexágono |
| Detecta unidade oculta | nunca, em nenhum alcance |
| Serve de observador para ocultos | nunca |

Os raios de revelação de terreno são curtos. O **Quartel General** enxerga 2. Cidade, as três Fábricas, Aeroporto, Aeroporto Avançado, Porto Naval, Hidrobase e Docas enxergam 1 — o próprio hexágono e o anel imediato. Barracks, Estação de Trem e Terminal Rodoviário não revelam nada além do chão onde estão.

Mas ela é péssima repórter: **não aponta unidades a distância**. Ela ilumina o cenário sem denunciar quem está em cena. Detectar, ela só detecta quem estiver ocupando o próprio hexágono — e ainda assim só na superfície, no andar que trava. Uma aeronave sobrevoando a sua cidade e um submarino passando por baixo do seu porto não são vistos pelo prédio. Ele não olha para cima nem para baixo.

E contra ocultação, a construção não serve para nada: não fura, não denuncia, e não empresta os olhos dela para ninguém que esteja tentando furar. Prédio não é sensor.

Na prática, isso significa que território revelado não é território vigiado. O anel de mapa aberto ao redor da sua fábrica não substitui uma unidade de olho na estrada.

Guarde a distinção. Muita confusão sobre "por que eu não vi aquilo" se resolve percebendo que o mapa estava aberto e o inimigo não estava detectado.

### Os Três Estados de Conhecimento

Junte memória e detecção e o seu time tem, sobre qualquer hexágono do mapa, exatamente um de três estados. Eles são a moeda de toda decisão de informação do jogo, e vale nomeá-los:

**Visível agora.** Alguém seu está olhando para aquele hexágono neste momento. Terreno e ocupantes detectáveis estão atualizados.

**Explorado.** Você já esteve lá ou já enxergou dali, mas ninguém seu observa o setor agora. Você tem a fotografia: o relevo, as estruturas e o último dono conhecido das construções. Não tem quem se move por cima disso — e a fotografia pode estar velha.

**Nunca explorado.** O preto. Nenhuma unidade sua jamais viu aquele hexágono, e o seu tabuleiro não tem nada para mostrar ali.

A escada é sempre nesse sentido, e ela **não desce**: explorado nunca volta a ser preto, porque terreno não se desaprende. O que desce é a atualidade — visível vira explorado assim que você tira os olhos.

O que cada estado permite que a unidade **faça** a partir dali é assunto de `04_ciclo_de_acao_e_comprometimento.md`. Aqui ficam apenas os estados em si.

### Alcance e Linha de Visão

Detectar exige duas coisas ao mesmo tempo: estar dentro do alcance de visão e ter linha de visão válida.

O alcance é o número de hexágonos, e ele pode variar conforme a camada do alvo. Um sensor especializado enxerga muito mais longe naquilo para que foi construído do que no resto.

A linha de visão é a geometria que você já conhece do capítulo de elevação: floresta interrompe, montanha faz sombra, altitude vê por cima.

Faltando qualquer uma das duas, não há detecção.

### O Céu Não Tem Sombra

Uma exceção importante, e ela é deliberada.

Quando uma aeronave procura alvos que estão em Air/High, a linha de visão não é consultada. Vale o alcance e nada mais. Um caça não perde contato aéreo por causa de montanha, floresta ou falésia — no seu próprio plano, o céu é limpo.

Mas repare no escopo, porque ele é estreito: isso vale para aeronave olhando o alto. A mesma aeronave, olhando para o solo, volta a depender inteiramente da geometria. Os hexágonos atrás de uma serra continuam escuros para ela.

E sensores de superfície não recebem esse privilégio. Um radar terrestre aplica linha de visão inclusive contra o céu — sua cobertura é recortada pelo relevo à sua volta.

Daí a diferença real entre um radar de solo e um sensor aéreo: não é alcance, é a natureza da cobertura. O radar enxerga um céu picotado pelo terreno. O sensor aéreo enxerga o céu inteiro dentro do alcance. Um é sensor de posição. O outro é sensor de presença.

E aqui é preciso ser exato sobre o alcance dessa exceção, porque ela é mais estreita do que a frase sugere. São duas coisas diferentes, e só uma delas é privilégio:

**Air/High nunca é obstáculo, para ninguém.** Uma célula de alta altitude no meio do caminho não bloqueia a linha de nenhum observador, aéreo ou terrestre. Isso é geometria do mundo, não vantagem de quem voa.

**Dispensar a linha de visão é privilégio de aeronave, e só para detectar.** É a aeronave olhando para Air/High que troca geometria por alcance puro. O mesmo alívio **não existe na hora de atirar**: para atacar um alvo em Air/High, a linha de tiro é avaliada normalmente, para qualquer atacante. Você pode ter contato limpo com um caça e ainda assim não ter como acertá-lo.

É a separação de sempre entre ver e alcançar, e aqui ela é literal: a detecção usa uma regra, o tiro usa outra.

### Ocultação

Algumas unidades têm habilidade de ocultação — e ela é exclusiva de domínio. Funciona apenas na camada para a qual foi projetada.

Um caça furtivo é oculto em Air/High. Descendo para Air/Low, pousando, ou em qualquer outra camada, a habilidade continua na ficha e não produz efeito. Um submarino é oculto submerso. Emergindo, deixa de ser.

Isso não é uma exceção separada: é a mesma regra de domínio que rege o jogo inteiro. A habilidade existe onde ela foi feita para existir.

### Quem Fura a Ocultação

Estar perto não revela nada. Estar no mesmo hexágono não revela nada.

Uma unidade oculta só é detectada por um sensor que tenha especialização para aquele tipo de ocultação, naquela camada. Não basta ter alcance sobrando — é preciso ter o equipamento certo.

Por isso a guerra antissubmarino pertence a quem foi construído para ela, e não a quem tem o maior alcance do mapa. Um sensor aéreo poderoso pode ter números altos em toda a matriz e ainda assim ser incapaz de achar um submarino submerso, porque lhe falta a especialização — não o alcance.

Sensores produzem alvos. Armas exploram a informação. Uma unidade barata que não destrói nada pode mudar completamente as condições em que uma unidade cara opera.

### O Olho

Quando uma unidade sua com habilidade de ocultação é detectada por um inimigo, aparece um Olho sobre o token dela.

O Olho diz uma coisa só: você foi visto.

Ele não diz quem te viu, de que direção, com qual sensor, nem quantos inimigos já conhecem sua posição. É informação parcial — o suficiente para você decidir, insuficiente para você relaxar.

Duas leituras que valem guardar:

O Olho aparece em qualquer camada. Um caça furtivo pego em Air/Low, ou pousado e avistado por um soldado, acende o Olho igual. A camada é onde a habilidade foi feita para funcionar, não uma condição do aviso. Se você foi detectado, você merece saber.

A ausência do Olho não é promessa de segurança. Unidades comuns não recebem aviso nenhum — elas podem estar sendo rastreadas há turnos sem que você saiba. O Olho é um privilégio de quem tem ocultação, não uma garantia geral.

### Disparar Custa Ocultação

Ocultação dá iniciativa, não impunidade.

Uma unidade furtiva escolhe o momento do duelo e entra como atacante — com o benefício de posição que isso traz. O que ela não ganha é um tiro grátis: o defensor revida normalmente se tiver arma, munição e alcance.

E o disparo cobra o seu preço. Ao atacar, a unidade furtiva perde a ocultação temporariamente e fica exposta.

Antes dos números, a unidade de medida, porque numa partida com mais de dois times ela precisa ser inequívoca. **Toda duração deste manual é contada em turnos do proprietário da unidade.** Não em turnos globais, não em passagens completas por todos os times. Quando o texto disser "um turno", leia sempre "um turno de quem é dono daquela peça".

Para aeronaves furtivas, a exposição dura **um turno**: ela vai do disparo até o início do próximo turno do dono, atravessando os turnos de todos os adversários. Elas permanecem na própria camada e voltam a sumir quando você as recebe de volta. Representa velocidade — trocar de setor e reduzir assinatura antes que forças lentas cerquem a área.

Para o submarino, é mais severo, e dura **dois turnos** jogáveis completos seus. Ele não perde uma marcação — ele muda de camada, emerge, e fica preso na superfície durante esse tempo.

Há uma sutileza que favorece o submarino e vale conhecer, porque ela aparece perto da costa. Se o hexágono não permitir a emersão — porque a superfície já está ocupada, por exemplo — a ordem de emergir fica **pendente**: a unidade continua submersa, porém **revelada**, e o relógio da exposição **não corre** enquanto ela estiver nesse estado. Ele só começa a contar quando a emersão de fato acontece.

E não é só atacar que o traz para cima. **Revidar** também expõe, e **ser atingido** por armamento apropriado faz o mesmo — pelos mesmos dois turnos, de propósito, e desde que ele tenha perdido efetivo e sobrevivido. Se disparar custasse menos que ser pego, atacar seria mais seguro do que se esconder, e a arma antissubmarino puniria menos que o próprio torpedo do submarino.

Essa duração não é sabor narrativo: é o seu relógio de caçada. Dois turnos é o que você tem para aproximar a fragata, chamar a aeronave, fechar a rota de fuga ou atacar de novo antes que ele mergulhe.

A assimetria entre os dois é o tema de sempre: aeronaves rápidas desaparecem antes. Navios lentos permanecem expostos por mais tempo.
