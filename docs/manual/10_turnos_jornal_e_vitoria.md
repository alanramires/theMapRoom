# Turno, Jornal e Vitória

*Como a partida avança e como termina.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

## O Início do Turno

Antes de você receber o comando, o jogo executa uma sequência fixa. Ela importa porque várias regras deste manual dependem de **em que ponto dessa ordem** algo acontece — e porque é aqui que os relógios de exposição, autonomia e atendimento avançam.

**Primeiro, o time ativo assume** e as condições de vitória são avaliadas. É também aqui que se verifica a eliminação total, a partir do segundo turno, para que ninguém seja declarado derrotado antes de ter comprado a primeira força.

**Depois, a renda é creditada.** Os valores e a origem deles estão em `09_captura_economia_e_progressao.md`.

**Em seguida, cada unidade sua passa pelo upkeep**, nesta ordem interna:

Travas de camada pendentes tentam se aplicar — uma emersão forçada que estava bloqueada por hexágono ocupado acontece agora, se o hexágono liberou. No turno em que a trava finalmente se aplica, o relógio dela ainda **não** corre; a janela de exposição só começa a contar com a unidade de fato na camada forçada.

A autonomia é cobrada. Quem paga, quanto, e quem está isento está em `07_logistica_e_servicos.md`.

Aeronave em voo que chega a zero de combustível entra na fila de resolução: pouso de emergência onde o hexágono permite, queda onde não permite.

Por fim, os estados de turno são reiniciados — a unidade volta a poder agir, volta a poder receber atendimento, e a exposição por disparo do turno anterior se encerra.

**Só então o Jornal é montado**, e há uma sutileza deliberada aqui: o relatório já contém o desfecho das aeronaves sem combustível, mesmo que a fila de pouso e queda só role depois. O resultado é pré-avaliado pela mesma regra determinística que a fila vai aplicar, para que você não descubra o buraco na sua força aérea depois de já ter lido o briefing.

**Aí o comando passa a você.**

> *Esta seção descreve a ordem implementada e ainda não passou por auditoria formal. Ver `92_auditoria.md`.*

## O Jornal do Comandante

Você comanda de uma sala de mapas, e entre um turno seu e o próximo o mundo não parou. O adversário se moveu, atirou, capturou e avançou — e você não estava lá para ver.

O Jornal do Comandante é o que estava sobre a mesa quando você voltou. No início de cada turno, ele apresenta o registro do que aconteceu enquanto você não comandava.

### Três Níveis de Urgência

As entradas chegam separadas por gravidade, para você ler o que importa primeiro:

**Crítico** é perda consumada ou golpe recebido. Contato perdido, conquista perdida, aeronave caída por falta de combustível, tiro vindo da névoa.

**Atenção** é ameaça em curso ou escassez. Uma construção sua sob captura parcial, um estoque logístico que zerou.

**Informativo** é ganho de informação ou ajuste automático. Um novo contato detectado, um submarino que emergiu, uma aeronave que fez pouso de emergência.

A leitura é direta: o crítico já aconteceu e você precisa reagir. O atenção ainda está acontecendo e você talvez consiga impedir. O informativo é o seu serviço de inteligência entregando o que descobriu.

### O Jornal Não Mente, e Também Não Adivinha

O ponto mais importante: **o Jornal respeita a névoa.**

Ele só registra o que você teria como saber. Uma unidade sua que desapareceu vira "contato perdido" — não "destruída pelo tanque inimigo em tal hexágono", porque você não viu isso acontecer. Uma construção sua que mudou de dono é reportada com o novo dono nomeado, porque a guarnição que estava lá viu quem entrou.

Ele não é onisciente e não te dá de graça a informação que o Fog of War cobra. É o relatório honesto de quem esteve em campo, com os limites de quem esteve em campo.

**E o Jornal informa sem repintar o mapa.** Esta é a assimetria que mais confunde, e ela é deliberada: o relatório te diz que a sua cidade caiu e nomeia quem entrou, mas o hexágono no tabuleiro continua com a sua cor até você voltar a ter olhos nele. Não é bug nem esquecimento — é a Sala de Mapas funcionando como uma sala de mapas. O despacho chegou à sua mesa; ninguém foi lá mover a miniatura.

A leitura prática é útil e um pouco cruel: **depois de ler o Jornal, você sabe mais do que o seu próprio tabuleiro mostra.** Cabe a você lembrar disso ao planejar, porque o mapa não vai lembrar por você.

Se você precisar de um princípio para prever o que o Jornal vai ou não contar, é este: **ele registra conhecimento adquirido, não a verdade absoluta do mundo.** Se ninguém seu poderia ter sabido daquilo, não aparece. Se alguém seu viu e depois morreu, o que ele viu antes de morrer já era seu.

Por isso "tiro da névoa" é uma categoria própria e crítica: você levou fogo de uma origem que não conseguiu identificar. O jogo te conta que aconteceu, e não te conta de onde veio. Descobrir isso é trabalho seu.

## Como se Ganha

Existem duas categorias diferentes, e vale separá-las.

**Derrotas gerais** — valem em qualquer partida:

Captura do Quartel General. O adversário perde no instante em que o QG dele muda de dono — e quem capturou vence a partida, não apenas aquele duelo.

Eliminação total. Um lado sem nenhuma unidade restante no tabuleiro está fora. A verificação começa a valer a partir do segundo turno, para que ninguém seja eliminado antes de comprar a primeira força.

Vale precisar o que "no tabuleiro" quer dizer, porque a dúvida é natural: a contagem olha para as unidades presentes no mapa, não para os passageiros dentro delas. Isso na prática nunca te prejudica — como perder o transportador mata toda a tropa embarcada, não existe situação em que você tenha gente viva dentro de um veículo e nada em campo. **O que te mantém na partida é o transportador; a tropa dentro dele não sobrevive à perda dele.** Um APC carregado é uma unidade em campo e vale como tal.

E há uma terceira via, que não é derrota imposta: **a rendição**. Um jogador pode encerrar voluntariamente a própria participação. O efeito no tabuleiro é o mesmo de uma derrota, mas a decisão foi dele.

### A Primeira Eliminação Encerra a Partida

Esta é a regra que reorganiza todas as outras, e ela surpreende: **quem eliminar um jogador primeiro vence na hora — mesmo que ainda restem outros participantes em campo.**

Não existe "sobreviver até sobrar um". Numa partida de três ou quatro lados, o primeiro a tomar um Quartel General ou a destruir um exército inteiro leva a partida inteira, e os demais param onde estavam. A rendição de qualquer participante produz o mesmo efeito: ela encerra o jogo, não abre uma vaga.

A leitura estratégica muda por completo. Você não está numa guerra de atrito com vários rivais, esperando que se desgastem entre si — está numa corrida, e o prêmio vai para quem fechar o primeiro abate. Deixar dois vizinhos se destruindo enquanto você cresce é a pior estratégia possível: o vencedor daquele duelo vence **você** junto.

**E a partida congela de verdade.** Declarada a vitória, o turno não avança mais. Nada se move, nem em jogador contra jogador — o que resta é abrir o menu para ler o Jornal, consultar as estatísticas da partida e sair. Não há epílogo jogável, não há limpeza de campo. O placar fechou.

Quanto ao território de quem saiu: as construções dele **voltam a ser neutras, com os pontos de captura restaurados ao máximo** — não passam para o vencedor. Na prática do jogo base isso raramente se joga, já que a partida termina no mesmo instante; a regra existe e importa em cenários que decidirem manter a partida viva após uma eliminação.

**Objetivos de cenário** — valem só onde foram definidos:

Concluir as tarefas de um tutorial encerra aquele cenário. É condição do roteiro, não regra do jogo — e é por essa porta que entram, no futuro, objetivos como segurar um ponto, sobreviver a um número de turnos ou alcançar uma posição.

Repare no que essas condições têm em comum: nenhuma delas transforma baixas em placar. Não existe meta de destruição, não existe pontuação por dano, não existe vitória por "causar mais estrago".

O atrito só encerra a partida quando é total — quando o adversário não tem mais nenhuma força com que continuar. Você não vence porque destruiu vinte unidades. Vence porque tomou o lugar certo, ou porque não sobrou nada do outro lado.

É por isso que o The Map Room premia quem administra território, informação e logística acima de quem administra combates. Ganhar todas as trocas de tiro e não tomar nada é perder devagar.

## Epílogo — A Sala de Mapas

Se você guardar uma única ideia deste manual, guarde esta: **o The Map Room não é um jogo de tempo real. É um jogo de intel.**

Num jogo de tempo real, o mapa é a verdade. Você vê as coisas acontecerem enquanto acontecem, e a informação chega junto com o evento. Aqui não. Aqui você comanda de uma sala de mapas, e sobre a mesa há apenas a última inteligência que chegou até você. O que está além do alcance dos seus sensores não é escuro porque o jogo o esconde — é escuro porque ninguém seu esteve lá para contar.

### Avançar Não É Descobrir

É por isso que mover não revela.

Você levanta a peça, desenha o trajeto, olha as opções que aquela posição ofereceria — e nada no mundo mudou ainda. Pode devolver a peça à origem e tentar outra coisa. Pode mudar de ideia quantas vezes quiser. O tabuleiro é seu para pensar, e pensar não custa nada nem entrega nada.

O mundo só se atualiza depois que você **compromete** a ação e o jogo volta ao repouso. É nesse retorno — e só nele — que a névoa é recalculada, que a fotografia do terreno é refeita, que os contatos aparecem ou somem. Antes disso, tudo é ensaio.

Repouso, ação comprometida, repouso, recálculo. Essa é a respiração do jogo, e ela é o que separa o The Map Room de um mapa que você tateia às cegas. Você não descobre o mundo empurrando o cursor contra a escuridão. Você toma uma decisão sob incerteza, aceita as consequências, e **então** o mundo te conta o que você encontrou.

### A Aposta

No fim, é isso que o jogo pede de você: decidir com informação incompleta e viver com o resultado.

Cada avanço para dentro da névoa é uma aposta sobre o que há do outro lado. Cada disparo é uma aposta sobre se o alvo que apareceu no seu tabuleiro está mesmo vulnerável — ou se foi você quem entrou na mira de alguém. A última intel conhecida é tudo o que você tem, e ela pode estar velha, incompleta, ou ser exatamente a isca que o inimigo quis que você visse.

Vencer aqui não é ter a melhor arma. É administrar melhor a observação, a ocultação, o alcance, o momento e a posição dos seus olhos no mapa.

A guerra não é sobre atirar. É sobre saber.
