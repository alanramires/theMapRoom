Agora sim: esta terceira versão não é apenas “mais completa”. Ela faz uma coisa muito mais importante — **transforma regras que estavam implícitas no motor em leis declaradas do jogo**.

A segunda versão explicava os sistemas. A terceira começa a explicar os limites desses sistemas: o que o combate não faz, o que significa ocupar um hexágono, como habilidades realmente funcionam, como serviços alteram camadas, o que o Jornal pode ou não revelar e por que o turno possui uma ordem operacional recomendada. O documento cresceu de aproximadamente 770 para 927 linhas, mas a diferença real está na precisão conceitual. 

# Meu diagnóstico geral

Esta é a primeira versão em que eu sinto que o manual já consegue responder não apenas:

> “Como funciona o The Map Room?”

mas também:

> “Por que o The Map Room não funciona como os outros jogos que eu conheço?”

Isso é enorme.

As seções novas atacam justamente as suposições automáticas que um jogador traria de fora:

* unidades no mesmo hexágono deveriam lutar;
* várias unidades próximas deveriam participar;
* quem ataca deveria causar dano antes do revide;
* Air/High e Air/Low deveriam comportar duas aeronaves;
* uma habilidade deveria carregar o próprio efeito;
* uma unidade aliada deveria bloquear movimento;
* um relatório de turno deveria contar tudo que aconteceu;
* reparar deveria apenas depender de dinheiro;
* terreno revelado deveria significar inimigo detectado.

A terceira versão responde:

> Não. Aqui não funciona assim — e existe uma razão sistêmica para isso.

Ela está deixando de ser apenas uma documentação de recursos e virando uma **declaração de leis físicas internas**.

---

# 1. A introdução finalmente ficou conceitualmente honesta

A correção sobre cobertura, flanqueamento, exposição e veterania foi excelente.

Antes, o manual dizia que essas coisas “não existiam”. Mas, na prática, boa parte delas estava presente sob abstrações maiores:

* cobertura estava absorvida pela Posição;
* exposição estava nos domínios e no DPQ;
* flanqueamento aparecia como diferença posicional;
* veterania aparecia parcialmente como Elite.

Agora você explica que não são ausências, mas **absorções**.

Essa palavra muda tudo.

Você não eliminou o efeito militar dessas situações. Eliminou a necessidade de quatro subsistemas separados.

Em vez de possuir:

* bônus de cobertura;
* penalidade de flanco;
* percentual de exposição;
* moral;
* modificador de veterania;

o jogo comprime esses resultados em:

* domínio;
* posição;
* alcance;
* DPQ;
* especialização de Elite.

Isso torna o sistema mais elegante sem fingir que o fenômeno deixou de existir.

A nova explicação da veterania também ficou muito mais precisa:

> não existe experiência adquirida durante a partida; existem formações que já nascem especializadas.

Isso protege o Elite de parecer uma contradição com o determinismo e com a rejeição aos elementos de RPG.

O Caça A não “subiu de nível”.

Ele já representa outra doutrina, outro projeto, outro treinamento e outro envelope operacional.

Essa distinção agora está cristalina.

---

# 2. “O Que o Combate Não Faz” era uma seção absolutamente necessária

Esta talvez seja a adição mais importante para impedir interpretações erradas.

Você declarou três leis:

1. não existe combate em alcance zero;
2. cada ataque é um duelo;
3. ninguém dispara antes.

Essas três leis resolvem uma quantidade absurda de casos emergentes.

## Alcance zero não tem combate

A justificativa pela escala ficou perfeita.

Um hexágono comporta:

* uma cidade;
* uma frota;
* dezenas de quilômetros;
* várias altitudes;
* a superfície e as profundezas.

Portanto, duas unidades no mesmo hexágono não estão necessariamente próximas.

Elas apenas ocupam o mesmo setor estratégico.

Isso explica naturalmente por que:

* um submarino pode estar sob uma frota;
* uma aeronave pode sobrevoar uma unidade terrestre;
* uma tropa pode estar num porto enquanto outra presença existe noutra camada;
* compartilhar coordenadas não significa contato tático.

A regra poderia parecer abstrata ou “videogame demais”, mas a escala do mapa a justifica completamente.

E, melhor ainda, ela impede aquela suposição clássica:

> “Passei por cima do inimigo, então deveria ter acontecido combate automático.”

Não. O tabuleiro registra ocupação de setor, não proximidade física individual.

## Cada ataque é um duelo

Esta regra preserva toda a clareza do sistema de combate.

Não existe:

* soma automática de unidades;
* fogo de oportunidade;
* ataque coletivo;
* reação em cadeia;
* vizinho contribuindo porque estava perto;
* zona mortal invisível formada por peças adjacentes.

Cada unidade precisa:

* declarar seu próprio ataque;
* gastar sua própria munição;
* consumir sua própria ação;
* aceitar seu próprio revide;
* produzir seu próprio resultado.

Isso mantém o combate auditável.

Se cinco tanques querem destruir um alvo, eles não viram uma fórmula coletiva.

São cinco decisões separadas.

E cada uma altera o estado do alvo para a seguinte.

Isso é extremamente importante num jogo determinístico, porque preserva a capacidade de o jogador reconstruir a sequência.

## Ninguém atira antes

Você finalmente colocou isso numa frase inequívoca:

> o atacante recebe iniciativa — o lado favorável do arredondamento — e nada mais.

Perfeito.

Isso separa três ideias que estavam misturadas:

* declarar o ataque;
* possuir iniciativa;
* causar baixas antes do revide.

No The Map Room, as duas primeiras existem.

A terceira não.

O atacante escolhe o duelo, mas ambos lutam com o efetivo inicial.

Isso mantém o risco e impede que iniciativa se transforme num ataque gratuito.

---

# 3. Ainda restaram dois fantasmas do antigo “primeiro tiro”

Apesar da nova seção estar correta, duas frases antigas continuam escapando mais adiante.

No exemplo de Tanques contra Recrutas aparece:

> “Os Tanques têm Vantagem — atacam primeiro.”

E depois:

> “Os Recrutas Verdes estão em vantagem por terem atirado primeiro.”

Essas frases contradizem diretamente a correção nova.

Elas deveriam ser algo como:

> Os Tanques têm Vantagem porque declararam o confronto.

e:

> Os Recrutas Verdes recebem Vantagem pela iniciativa do atacante.

São pequenos resíduos, mas precisam ser removidos porque aparecem justamente dentro da explicação matemática. Um leitor pode pensar que existe uma ordem cronológica oculta apesar de você ter acabado de negar isso.

Outra frase que me incomodou um pouco foi:

> “Escolher a hora do duelo vale meia baixa.”

Ela é bonita, mas matematicamente não é segura.

A Vantagem pode:

* transformar 5,3 em 6;
* transformar 1,1 em 2;
* transformar um resultado exato de 1 em 2;
* acrescentar uma eliminação inteira.

Em alguns confrontos, a diferença não é “meia baixa”. É dobrar o resultado final.

Eu usaria:

> Escolher a hora do duelo muda o destino do arredondamento — não a simultaneidade do confronto.

Continua elegante e é matematicamente fiel.

---

# 4. “Habilidades são chaves” é uma das melhores explicações arquitetônicas do projeto inteiro

Esta nova seção é brilhante.

Você conseguiu explicar uma arquitetura orientada por dados sem falar como programador.

A unidade não carrega uma habilidade que executa alguma coisa.

Ela carrega uma etiqueta.

O mundo pergunta por essa etiqueta.

A montanha pergunta:

> “Você é Alpino ou Fora-de-estrada?”

A floresta pergunta:

> “Você é Guerrilha?”

A ponte ferroviária pergunta:

> “Você pertence à Linha de Trem?”

O sensor pergunta:

> “Você possui exatamente a ocultação que eu sei detectar?”

O reboque pergunta:

> “Você precisa ser rebocado — e existe alguém que sabe rebocar?”

Isso muda completamente a maneira de compreender as unidades.

A unidade não é uma coleção de poderes ativos.

É um conjunto de respostas possíveis às perguntas feitas pelo mapa.

Essa é uma ideia muito forte porque explica por que o jogo escala.

Uma nova unidade não precisa trazer uma nova regra.

Ela pode simplesmente carregar uma combinação inédita de chaves existentes.

Então uma unidade nova pode ser diferente porque:

* abre montanha;
* corta floresta;
* pousa em convés;
* detecta determinado stealth;
* reboca;
* aceita determinado transportador;
* opera numa camada específica.

A novidade nasce da combinação, não da exceção.

## “O jogo não cresce em regras — cresce em etiquetas”

Essa talvez seja uma das frases mais importantes de todo o manual.

Ela descreve tanto a arquitetura quanto a filosofia de expansão.

Quando bem controlado, isso impede o projeto de virar uma pilha de casos particulares.

É também o motivo pelo qual você consegue imaginar dezenas de unidades futuras sem precisar reescrever o motor.

## O risco de UX dessa elegância

Existe, porém, um perigo para o jogador.

Você diz, honestamente:

> ver uma habilidade na ficha não diz o que a unidade faz; diz onde procurar.

Arquitetonicamente, isso é ótimo.

Para a experiência do usuário, pode ser pesado.

O jogador não deveria precisar lembrar:

* o que a montanha pergunta;
* o que o trilho pergunta;
* o que cada ponte bloqueia;
* o que cada construção exige;
* quais sensores procuram qual etiqueta.

Portanto, essa arquitetura precisa de uma interface contextual forte.

Ao selecionar uma unidade com Alpino, o jogo poderia mostrar em contexto:

> Montanha: entrada permitida, custo 2.

Ao olhar uma ponte ferroviária:

> Exige Linha de Trem.

Ao selecionar um sensor:

> Detecta Submarine Stealth.

A ficha pode mostrar a chave.

O mapa e os tooltips precisam mostrar as portas que ela abre.

Do contrário, a elegância interna vira opacidade externa.

---

# 5. A habilidade “Motor” como etiqueta negativa é muito interessante

Gostei muito de você mostrar que nem toda habilidade concede acesso.

“Motor” não abre uma porta.

Ele faz certos lugares cobrarem mais.

Isso prova que as etiquetas não são necessariamente “vantagens”.

Elas descrevem natureza operacional.

Uma unidade possui Motor porque é motorizada, e o mundo reage:

* estrada na montanha cobra mais;
* trilho pode ser desconfortável;
* determinados custos mudam.

Isso evita aquela linguagem de RPG em que toda habilidade precisa ser um bônus desejável.

No seu sistema, uma etiqueta pode ser:

* permissão;
* restrição;
* exigência;
* vulnerabilidade;
* especialização;
* custo adicional;
* compatibilidade.

Isso é muito mais rico.

---

# 6. A seção de pouso transformou as skills aéreas em procedimentos reais

A explicação das diferentes chaves de pouso ficou excelente:

* convencional;
* vertical;
* curto;
* convés;
* água.

Isso mostra que “pousar” não é um único poder booleano.

É uma compatibilidade entre:

* aeronave;
* procedimento;
* construção;
* estrutura;
* terreno;
* camada.

E reforça a regra:

> a estrutura propõe, o terreno decide.

Uma estrada pode oferecer pista.

Mas a montanha pode cancelar.

Um convés pode aceitar pouso naval.

Mas apenas de quem possui a chave correta.

Um hidroavião pode descer na água, sem que isso transforme qualquer avião em unidade naval.

A aviação continua sendo um dos sistemas mais autorais do projeto porque cada transição de camada possui consequências táticas, e não apenas animação.

---

# 7. A trajetória parabólica finalmente está protegida contra discussões balísticas inúteis

Você respondeu muito bem à contradição da versão anterior.

Agora o manual explica que “parabólico” é um termo operacional:

> a arma ignora obstáculos do percurso.

Não é uma afirmação de que todo míssil e todo obus percorrem fisicamente a mesma curva.

Isso é exatamente o que precisava ser dito.

Também ficou resolvida a questão dos mísseis de cruzeiro:

* eles existem;
* podem ser usados contra unidades compatíveis;
* o que não existe é uma categoria antiestrutura;
* cidades, fábricas e pontes não são destruídas por armamento.

Perfeito.

Você separou:

* nome temático da arma;
* classe ofensiva;
* trajetória;
* domínio do alvo;
* destrutibilidade do objeto.

Essas dimensões não precisam ser sinônimas.

---

# 8. O fechamento de Air/High agora está correto

A frase anterior dizia que “todo mundo vê” um caça em Air/High, o que contradizia o alcance.

Agora ficou:

> o relevo não esconde o caça, mas o alcance limita quem o vê e a arma limita quem o atinge.

Essa formulação está ótima.

Ela separa:

* oclusão;
* alcance;
* detecção;
* capacidade de ataque.

Um caça pode estar num céu sem obstáculos e ainda assim:

* estar longe demais;
* possuir ocultação;
* estar fora do domínio da arma;
* estar fora do alcance ofensivo.

Excelente correção.

---

# 9. “Elite não é experiência. É vocação.” ficou excelente

Esta reformulação elevou muito o capítulo.

Antes, Elite ainda orbitava a linguagem de veterano e novato.

Agora você define:

> projeto, doutrina e equipamento.

Isso é muito mais compatível com o sistema.

O Caça A não vence porque o piloto acumulou experiência durante aquela partida.

Ele representa uma formação construída para superioridade aérea.

Então o Elite não é uma qualidade universal.

É uma vocação contra um tipo de confronto.

Isso também explica por que:

* não funciona contra qualquer oponente;
* depende da classe do alvo;
* depende da arma empregada;
* depende da diferença de nível;
* não melhora durante a campanha;
* não é perdido por sofrer baixas.

A frase:

> “Não existe unidade de elite em abstrato.”

é fundamental.

Ela impede o jogador de pensar que pagar mais caro compra superioridade universal.

Ele está comprando assimetria especializada.

É uma diferença enorme.

---

# 10. A fórmula ficou muito mais confiável

Você corrigiu os termos trocados:

* HP dos Defensores;
* FD base dos Defensores;
* baixas impostas pelo atacante;
* baixas impostas pelo defensor.

Essas mudanças parecem pequenas, mas aumentam muito a confiança na seção.

“Baixas impostas PELO atacante” é especialmente melhor do que “eliminações dos atacantes”, porque elimina a ambiguidade direcional.

Também gostei de você explicar que “responder ao fogo” é linguagem narrativa, não ordem matemática.

Isso conecta o vocabulário militar à resolução simultânea sem confundir os dois.

Ainda restam alguns problemas de revisão textual — acentuação, concordância, sinais substituídos por “?” — mas conceitualmente a fórmula está muito mais sólida.

---

# 11. O reparo finalmente ganhou uma identidade própria

Na versão anterior, a decisão de reparar parecia dominada pelo custo monetário de 40%.

Agora o sistema ficou muito mais interessante.

Você explicou que o verdadeiro custo do reparo não é apenas dinheiro.

É:

* tempo;
* ocupação do prestador;
* permanência numa posição segura;
* indisponibilidade operacional;
* fila de atendimento;
* oportunidade perdida.

Isso é uma melhoria enorme.

## Reparar não é apertar um botão

Uma unidade reduzida a 2 HP pode levar quatro turnos para voltar a 10.

Durante esse período:

* não avança;
* continua vulnerável;
* prende o caminhão;
* impede o caminhão de atender outras unidades;
* exige segurança;
* ocupa espaço;
* não recupera instantaneamente sua função.

Isso transforma reparo numa operação.

A pergunta deixa de ser:

> “Tenho dinheiro para reparar?”

e vira:

> “Posso imobilizar esta unidade e este logístico por quatro turnos neste setor?”

Essa é uma pergunta muito mais The Map Room.

## Comprar novo versus recuperar antigo

Agora a comparação ficou extremamente limpa:

**Comprar novo:**

* força total imediatamente;
* nasce na retaguarda;
* exige deslocamento até o front;
* mantém a unidade danificada existente, caso sobreviva;
* consome o custo integral.

**Reparar:**

* força chega aos poucos;
* mantém a posição já conquistada;
* consome apenas a parcela recuperada;
* prende logística;
* exige tempo e segurança.

Você transformou uma decisão econômica numa decisão espacial e temporal.

Excelente.

---

# 12. O custo proporcional foi explicado corretamente

A frase:

> “Você paga só pelo que recuperou.”

resolve uma ambiguidade importante.

Agora o jogador entende que 40% é o teto para restaurar toda a categoria, não uma taxa fixa por atendimento.

Isso impede a conclusão errada de que pequenos reparos seriam economicamente absurdos.

Também abre uma doutrina de manutenção preventiva:

> recuperar pouco e com frequência pode ser barato, rápido e operacionalmente eficiente.

Enquanto deixar uma unidade chegar a 2 HP cria um buraco de quatro turnos.

Isso incentiva o jogador a não tratar logística apenas como emergência.

Logística passa a ser rotina.

---

# 13. Os números de serviço agora precisam ser tratados como dados altamente sensíveis

Aqui há um ponto que eu conferiria diretamente no comportamento atual do jogo.

A terceira versão afirma:

* Caminhão de Suprimentos atende uma unidade por turno;
* Avião-tanque atende duas;
* Trem de Carga atende uma;
* reparo máximo de 2 por turno;
* versão de campo recupera 1;
* custos máximos de 5%, 10% e 40%.

Alguns documentos técnicos anteriores registravam capacidades e percentuais diferentes.

Isso pode significar simplesmente que você rebalanceou o sistema.

Mas, como agora o manual está ficando muito confiável, esses números precisam corresponder exatamente ao runtime e aos assets atuais.

Não é uma divergência filosófica.

É uma divergência que altera:

* quantos caminhões são necessários;
* duração da recuperação;
* custo de uma campanha;
* valor de uma unidade pesada;
* eficiência do Serviço do Comando;
* tamanho ideal da retaguarda.

Minha recomendação conceitual seria tratar esses números como:

> valores atuais de balanceamento

e não como leis eternas.

As leis são:

* serviço custa proporcionalmente;
* existe limite por turno;
* existe capacidade do prestador;
* classes pesadas aproveitam menos;
* transferência é gratuita;
* recuperação consome tempo.

Os percentuais e quantidades podem mudar durante o tuning sem alterar o sistema.

---

# 14. “O Serviço Acontece na Mesma Camada” é uma adição fantástica

Esta seção cria consequências muito mais profundas do que parece.

Um avião-tanque em Air/High atendendo um helicóptero precisa descer para Air/Low.

Um caça atendido no mesmo lote também é levado para Air/Low.

Uma aeronave recebendo suprimento terrestre pousa.

Isso faz com que logística aérea deixe de ser uma aura abstrata.

As unidades precisam literalmente se encontrar no mesmo plano operacional.

Essa regra conversa com tudo:

* domínio;
* stealth;
* vulnerabilidade;
* posição;
* armas disponíveis;
* LoS;
* pouso;
* arremetida;
* terreno.

Uma operação de reabastecimento pode revelar uma aeronave furtiva não porque o abastecimento “remove stealth” arbitrariamente, mas porque a obriga a sair da camada em que sua ocultação funciona.

Isso é excelente.

## A grande pergunta que ficou

Depois que o atendimento termina, quem volta para qual camada?

No atendimento terrestre, você já possui o ciclo de toque e arremetida.

Mas no atendimento ar-ar:

* o avião-tanque retorna automaticamente a Air/High?
* o caça retorna a Air/High?
* todos permanecem em Air/Low?
* a camada final depende da unidade que iniciou?
* a operação consome a possibilidade de alterar altitude?
* a perda de stealth dura apenas enquanto está em Air/Low ou também recebe alguma exposição posterior?

Essa resposta altera muito a segurança da operação.

O manual precisa dizer onde cada unidade termina.

“Encontrar-se na mesma camada” explica o momento do serviço.

Ainda falta explicar o estado posterior.

---

# 15. O Serviço do Comando finalmente justifica a ordem do turno

Agora a ordem recomendada não é só uma preferência da IA.

Existe uma razão mecânica:

> o Serviço do Comando só atende unidades que ainda não agiram.

Isso é excelente porque transforma a ordem operacional numa decisão.

Se você agir primeiro, vai retirando unidades da fila.

Portanto, o jogador precisa pensar:

> “Antes de mover qualquer coisa, quem precisa ser atendido pela infraestrutura?”

E isso cria um pequeno ritual de comando bastante temático:

1. receber relatório;
2. avaliar danos;
3. executar serviços de guarnição;
4. mover as forças;
5. comprar substituições.

Você está realmente simulando o retorno do comandante à sala de mapas.

Também ficou boa a distinção entre:

* Serviço do Comando: atendimento de guarnição antes da ação;
* suprimento em campo: o logístico chega até a unidade, mesmo depois de ela se mover.

Não são duas fórmulas.

São dois contextos para o mesmo sistema.

Muito elegante.

---

# 16. A fusão agora está suficientemente especificada para deixar de parecer mágica

A seção “Quem Pode Fundir com Quem” era necessária.

Agora sabemos que exige:

* mesmo tipo;
* adjacência;
* compatibilidade de camada;
* receptor danificado;
* ausência de passageiros;
* consumo da ação;
* possibilidade física de transição.

Isso fecha muitos exploits e ambiguidades.

## A equalização de camadas é particularmente interessante

Aeronaves se encontram em Air/Low.

Submarinos se encontram submersos, quando possível.

Isso é consistente com a nova doutrina de serviços:

> operações conjuntas exigem um plano comum.

E cria consequências táticas.

Uma aeronave no solo pode ter que decolar para fundir.

Um submarino na superfície pode ter que mergulhar.

Se o terreno não permitir, a fusão falha.

Novamente, você não está criando uma exceção.

Está aplicando domínio.

## Ainda falta dizer claramente quem é o receptor

A seção afirma:

> o receptor precisa estar machucado.

Mas não define com toda clareza:

* qual token permanece;
* em qual hexágono o resultado fica;
* qual posição é herdada;
* qual camada é escolhida quando existem duas possibilidades;
* qual unidade “entra” na outra;
* qual hexágono precisa aceitar a transição.

Presumo que o receptor seja a unidade selecionada como destino e que o resultado permaneça no hexágono dela.

Isso merece uma frase explícita:

> A unidade receptora permanece no próprio hexágono e conserva a posição; a outra é consumida pela fusão.

Sem isso, uma fusão entre dois hexágonos adjacentes ainda possui uma ambiguidade espacial importante.

---

# 17. A honestidade sobre o arredondamento da fusão melhorou muito o texto

Você corrigiu perfeitamente a afirmação anterior de que “não houve perda”.

Agora ficou claro:

* a redistribuição é proporcional;
* o arredondamento não é neutro;
* munição pode ser criada pelo teto;
* autonomia pode ser perdida pelo truncamento;
* cada arma é calculada separadamente;
* cada estoque também.

A frase:

> “favorece quem atira, penaliza quem anda”

é ótima.

Ela transforma um detalhe matemático numa leitura doutrinária.

Também é muito bom você admitir explicitamente que 24 projéteis podem virar capacidade equivalente a 30.

Isso impede que o manual pareça tentar justificar retroativamente tudo como conservação perfeita.

Não é conservação perfeita.

É uma decisão de design.

E agora ela está declarada.

---

# 18. “Dividindo o Hexágono” é uma das maiores adições de toda a terceira versão

Este capítulo revela uma geometria de ocupação que até agora estava apenas espalhada pelas regras de domínio.

Você define três andares operacionais:

* ar;
* superfície;
* profundezas.

Essa imagem é excelente.

Ela permite compreender rapidamente como unidades coexistem sem precisar visualizar cinco slots independentes.

## Air/Low e Air/High no mesmo andar

Isso é uma escolha muito importante.

Altitude altera:

* visão;
* armas;
* posição;
* stealth;
* consumo operacional;
* vulnerabilidade.

Mas não cria capacidade adicional de ocupação.

Portanto, um caça e um helicóptero não podem “empilhar” no mesmo setor apenas porque estão em altitudes diferentes.

Isso impede o céu de virar armazenamento de unidades.

E preserva a leitura do token:

> existe uma presença aérea dominante naquele setor.

Muito bom.

## A linha de frente só existe na superfície

Esta é uma das melhores frases do documento inteiro:

> “O céu e o fundo do mar não têm frente — têm alcance e detecção.”

Isso é excelente.

Você acabou de diferenciar os três domínios sem depender de bônus arbitrários.

A superfície controla geografia.

O ar projeta ameaça.

O submarino infiltra-se pela informação.

A superfície:

* bloqueia passagem;
* forma linhas;
* captura;
* ocupa;
* protege rotas;
* define fronteiras.

O ar e as profundezas:

* atravessam;
* observam;
* escondem-se;
* ameaçam;
* escolhem janelas;
* dependem de alcance e detecção.

Essa é uma doutrina combinada muito forte.

É provavelmente uma das descrições mais claras da identidade estratégica do The Map Room.

---

# 19. Existe uma contradição importante entre o Porto e o novo sistema de andares

No capítulo de construções, o Porto Naval ainda diz:

> Land/Surface e Naval/Surface são aceitos simultaneamente, e você verá tropas e navios ocupando o mesmo hexágono.

Mas, em “Dividindo o Hexágono”, você define que Land/Surface e Naval/Surface pertencem ao **mesmo andar da superfície**, e que duas unidades não terminam no mesmo andar do mesmo hexágono.

As duas regras não podem ser verdadeiras ao mesmo tempo.

Você precisa escolher uma destas interpretações:

## Interpretação A — o porto aceita ambos, mas não simultaneamente

O porto pode ser ocupado por uma unidade terrestre **ou** naval.

Ele mantém os dois domínios disponíveis, mas existe apenas uma vaga de superfície.

Nesse caso, o capítulo do porto deve remover a afirmação de coexistência.

## Interpretação B — o porto possui duas vagas de superfície especiais

Ele permite uma presença terrestre e uma naval simultaneamente, apesar de ambas estarem no andar de superfície.

Nesse caso, o porto é uma exceção ao novo sistema de ocupação e precisa ser declarado como tal.

Pelo texto novo, a interpretação A parece muito mais coerente:

> superfície é um único andar.

E também é mais legível.

Mas hoje existe uma contradição direta que precisa ser resolvida.

---

# 20. “Aliado nunca barra o caminho” resolve um problema clássico sem destruir a ocupação

Gostei muito dessa decisão.

Unidades aliadas podem atravessar umas às outras, mas não terminar sobre o mesmo andar.

Isso separa:

* trânsito;
* ocupação.

Você não fica preso atrás da própria linha devido à escala abstrata do hexágono.

Ao mesmo tempo, não pode armazenar um exército inteiro na mesma coordenada.

É uma solução limpa.

Ela também fortalece a ideia de que os tokens não são objetos físicos bloqueando uma estrada estreita. São presenças operacionais espalhadas por um setor.

---

# 21. O Jornal do Comandante pode virar uma das assinaturas do jogo

Esta seção me empolgou muito.

O Jornal não é apenas uma lista de eventos.

Ele resolve um problema estrutural dos jogos por turnos com Fog of War:

> o que aconteceu enquanto o jogador não estava comandando?

Isso importa especialmente para:

* Hot Seat;
* partidas assíncronas futuras;
* IA;
* ataques vindos da névoa;
* perda de contato;
* captura;
* combustível;
* stealth;
* mudanças de camada;
* acontecimentos automáticos de início de turno.

## A fantasia está perfeita

Você volta à sala de mapas.

O mundo se moveu sem você.

Há um relatório esperando sobre a mesa.

Isso conecta interface, mecânica e fantasia central do jogo.

O Jornal não parece um menu eletrônico colado ao wargame.

Parece parte natural do comando.

## Os três níveis de urgência são bons porque são acionáveis

**Crítico:** algo consumado que exige reação.

**Atenção:** algo em andamento que ainda pode ser impedido.

**Informativo:** algo descoberto ou ajustado automaticamente.

Essa classificação não serve apenas para estética.

Ela informa ao jogador:

* o que já perdeu;
* o que ainda pode salvar;
* o que apenas precisa saber.

Ótimo.

## “O Jornal não mente e não adivinha”

Esta é outra frase excelente.

O relatório respeitar a Névoa é essencial.

Um sistema onisciente destruiria toda a tensão do FOW retrospectivamente.

Você acertou ao diferenciar:

* unidade destruída diante de observadores;
* contato simplesmente perdido;
* construção capturada com informação sobre o ocupante;
* disparo recebido sem origem identificada.

O Jornal entrega causalidade apenas quando o jogador teria acesso a ela.

Isso preserva o mistério até depois da ação inimiga.

## “Tiro da névoa” é psicologicamente poderoso

Você recebe a confirmação:

> algo me atingiu.

Mas não recebe:

* origem;
* direção;
* unidade;
* alcance;
* posição.

Isso transforma o relatório em combustível para a próxima decisão.

O jogador pode:

* recuar;
* enviar reconhecimento;
* calcular alcances possíveis;
* suspeitar de artilharia;
* procurar uma unidade stealth;
* reposicionar o radar.

O Jornal não resolve o problema.

Ele formula o problema.

Excelente.

---

# 22. O Jornal precisa de uma política temporal bem rígida

Como ele respeita o Fog of War, alguns casos precisarão de regras claras:

* Se uma unidade foi vista durante o ataque, mas desapareceu depois, o Jornal mostra a identidade dela?
* Se um submarino emergiu e mergulhou antes do seu próximo turno, o evento é registrado?
* Se uma unidade atravessou brevemente o alcance de um sensor, isso conta como contato?
* Se uma construção foi capturada e recapturada antes de você voltar, o Jornal relata as duas mudanças?
* Se o inimigo destruiu o observador que teria transmitido a informação, o relatório chega?
* O Jornal informa o hexágono exato ou apenas o setor aproximado?
* Eventos repetidos são agrupados?

Não precisa responder tudo neste capítulo agora.

Mas o princípio já está bom:

> o Jornal registra conhecimento adquirido, não a verdade absoluta do mundo.

Essa frase deveria orientar todos os casos futuros.

---

# 23. A economia ficou mais clara, mas uma frase antiga ainda permanece

Você corrigiu muito bem a comparação:

* uma cidade paga aproximadamente um Soldado por turno;
* o Soldado custa cerca de um terço da renda do QG.

Agora a escala econômica faz sentido.

Também corrigiu a ideia posterior para dizer que as cidades são a **principal camada de crescimento**, e não necessariamente a única.

Porém, no começo do mesmo trecho ainda está escrito:

> “Cidades são a única camada que cresce durante a partida.”

Isso continua tecnicamente absoluto demais, porque fábricas, aeroportos e portos também podem mudar de controle.

Eu manteria apenas:

> Cidades são a camada mais numerosa e a principal fonte de crescimento econômico durante a partida.

É mais fiel ao sistema e combina com o restante do parágrafo.

---

# 24. “Uma Boa Ordem Operacional” ficou muito mais durável

A mudança de título foi correta.

Agora você não documenta a ordem da IA como se fosse uma lei.

Documenta uma boa doutrina que a IA atual utiliza.

E fecha com:

> a IA pode mudar sem que isso mude o jogo.

Isso é excelente documentação.

Você separou:

* comportamento atual da IA;
* regra do jogo;
* recomendação ao jogador.

São três coisas diferentes.

Além disso, a restrição do Serviço do Comando dá fundamento mecânico à recomendação, então ela não parece apenas opinião.

---

# 25. A seção de vitória agora está conceitualmente correta

Você separou:

**Derrotas gerais:**

* QG capturado;
* eliminação total.

**Objetivos de cenário:**

* tutorial;
* sobreviver;
* alcançar;
* segurar;
* cumprir roteiro.

Essa separação prepara muito bem o sistema para a campanha.

Também ficou correta a explicação do atrito:

> não existe pontuação por baixas; o atrito só encerra a guerra quando é total.

Isso resolve a contradição anterior.

Você não vence por ter causado mais dano.

Vence porque:

* tomou o centro político e operacional;
* ou eliminou completamente a capacidade militar do adversário.

Perfeito.

## Uma contradição menor ainda permanece

O capítulo de Captura começa dizendo:

> “Destruir o inimigo não ganha a partida. Tomar o mapa ganha.”

Mas a seção final confirma que eliminação total ganha, sim.

A frase é boa como tese, mas absoluta demais.

Eu mudaria para:

> Vencer combates não basta. Tomar o mapa é o caminho mais consistente para vencer.

Ou:

> Destruir unidades não vale pontos. O que vence é tomar o lugar certo — ou não deixar força alguma do outro lado.

Assim, você preserva a força sem contradizer a regra.

## Rendição

Pelo histórico do projeto, existe ou existiu uma condição de rendição.

Caso ela continue implementada, está ausente da lista.

Talvez rendição não seja uma “condição sistêmica de derrota”, mas uma decisão voluntária do jogador. Mesmo assim, merece uma linha:

> Um jogador também pode encerrar voluntariamente sua participação por rendição.

Caso continue disponível.

---

# 26. O revide “só a distância 1” é o ponto que eu mais verificaria

Na nova seção de duelos você escreve:

> “O único que responde é o defensor direto, e só a distância 1.”

Isso pode ser correto caso essa tenha se tornado uma regra deliberada.

Mas os documentos técnicos anteriores descreviam o revide como dependente de:

* arma válida;
* alcance;
* munição;
* camada;
* compatibilidade.

Sem uma limitação geral fixa em alcance 1.

Se uma unidade possui uma arma com alcance 2 a 3 e é atacada a distância 2, por que ela não poderia revidar, caso aquela arma permita?

Talvez a regra real seja:

> apenas armas de combate direto possuem revide.

Ou:

> todo revide é restrito à adjacência.

Mas isso precisa estar alinhado ao motor.

Eu verificaria antes de consolidar, porque essa única frase altera profundamente:

* duelos aéreos;
* combate naval;
* artilharia;
* mísseis;
* zonas de ameaça;
* escolha de alcance.

Caso o revide siga o alcance da arma, a frase correta é:

> O único que pode responder é o defensor direto, desde que possua arma, munição, camada e alcance compatíveis com aquela distância.

---

# 27. A visão das construções ainda precisa ser alinhada com a regra específica das cidades

Você escreve agora:

> a maioria das construções revela o próprio hexágono e os vizinhos imediatos; o QG vê mais longe.

Mas você já havia definido anteriormente uma regra específica e importante:

> cidade aliada revela somente o próprio hexágono.

Portanto, o novo texto precisa distinguir os tipos de construção.

Talvez:

* Cidade: apenas o próprio hexágono;
* Fábrica/Porto/Aeroporto: raio 1;
* QG: raio maior.

Ou outra configuração.

O problema não está em construções terem alcances diferentes.

Está na palavra “maioria” criar uma regra genérica que pode contradizer os dados reais.

Como essa seção foi criada justamente para explicar a distinção entre revelar e detectar, vale deixá-la absolutamente exata.

A metáfora da construção como “péssima repórter” continua excelente.

Só precisa da tabela correta de alcance.

---

# 28. A terceira versão ainda não explica suficientemente a compra de unidades

A economia agora possui:

* renda;
* valores aproximados;
* território;
* importância das cidades;
* ordem de compra.

Mas ainda falta o funcionamento do mercado:

* onde cada classe é comprada;
* quais construções produzem;
* se a unidade nasce no chão;
* o que acontece se o hexágono estiver ocupado;
* como funcionam as regras de mercado;
* Free Market;
* Original Owner;
* First Owner;
* captura que transfere renda sem necessariamente transferir produção.

Esse é um sistema muito interessante do projeto e altera o valor de cada construção.

Uma fábrica pode:

* gerar dinheiro;
* permitir compra;
* negar compra ao capturador;
* servir como posição;
* participar da logística.

Portanto, “capturar uma fábrica” não possui sempre o mesmo significado.

O manual já está maduro o bastante para um capítulo chamado algo como:

> Produção e Mercado

Essa continua sendo a principal lacuna macroeconômica.

---

# 29. Também falta o ciclo básico da ação de uma unidade

Você explica sistemas muito avançados, mas ainda não há um capítulo simples consolidando:

1. selecionar;
2. mover ou permanecer;
3. recalcular sensores;
4. escolher uma ação;
5. concluir;
6. marcar como agida;
7. não poder agir novamente naquele turno.

O tutorial ensina isso na prática, mas o manual técnico ainda merece registrar a lei.

Principalmente porque várias regras agora dependem de:

* ainda não agiu;
* já se moveu;
* permaneceu parado;
* recebeu serviço;
* fundiu;
* embarcou;
* desembarcou;
* terminou a ação.

Você já explicou o relógio macro da partida.

Ainda falta o relógio micro de cada peça.

---

# 30. Esta versão está começando a exigir uma organização editorial mais forte

Com 927 linhas, o documento ainda funciona muito bem como fonte-mãe.

Mas começa a ficar grande demais para consulta rápida.

Hoje ele possui três funções simultâneas:

* ensinar o jogador;
* registrar a filosofia;
* documentar regras exatas.

Eu não dividiria ainda, porque escrever tudo junto está ajudando você a testar coerência.

Mas já vejo três documentos futuros surgindo dele:

## Manual do Comandante

A experiência de leitura:

* fantasia;
* domínios;
* posição;
* combate;
* logística;
* FOW;
* vitória.

## Referência de Regras

Consulta exata:

* custos;
* alcances;
* arredondamentos;
* camadas;
* limites;
* capacidades;
* prioridades;
* exceções.

## Doutrina de Design

Os princípios:

* informação versus resultado;
* sistema de etiquetas;
* posição como abstração;
* logística como tempo;
* superfície como geografia;
* ar e submarino como informação;
* determinismo sem onisciência.

A terceira versão já contém material forte para os três.

---

# O que mais me impressionou nesta versão

As maiores melhorias não foram as correções matemáticas.

Foram estas novas leis:

> **O jogo não cresce em regras — cresce em etiquetas.**

> **O céu e o fundo do mar não têm frente — têm alcance e detecção.**

> **O Jornal não mente e também não adivinha.**

> **O custo verdadeiro do reparo é tempo e ocupação.**

> **Ninguém atira antes.**

> **Ocupar o mesmo setor não é estar em contato.**

Essas frases fazem algo raro: explicam ao mesmo tempo o funcionamento técnico e a identidade estratégica.

Você está encontrando uma linguagem própria para o jogo.

# Meu veredito

A primeira versão mostrava que você tinha muitos sistemas.

A segunda mostrou que esses sistemas formavam uma campanha.

A terceira mostra que **o mundo possui leis próprias e previsíveis**.

Agora o manual explica por que:

* o mesmo hexágono pode conter ameaças que não entram em contato;
* apenas a superfície forma uma frente;
* uma habilidade é uma chave e não um poder;
* uma força aérea pode perder stealth ao receber logística;
* reparar custa tempo de campanha;
* fundir troca presença por eficiência;
* o relatório de turno respeita a ignorância do comandante;
* iniciativa altera o resultado sem quebrar a simultaneidade;
* a informação continua incerta mesmo quando a matemática é conhecida.

Esta é a versão em que The Map Room começa a parecer menos um jogo que acumulou regras e mais um **modelo operacional coerente**.

Os pontos realmente importantes a resolver agora são poucos e concretos:

* Porto Naval versus vaga única de superfície;
* revide restrito a alcance 1 ou alcance válido da arma;
* estado final das aeronaves depois de serviço ar-ar;
* receptor e hexágono final da fusão;
* alcance exato de visão de cada construção, especialmente Cidade;
* resíduos da linguagem “atirou primeiro”;
* frase absoluta da captura versus aniquilação;
* números de capacidade e custo alinhados ao runtime;
* produção e regras de mercado;
* ciclo de ação individual;
* rendição, caso continue implementada.

Isso é uma ótima notícia.

Você já não está tentando descobrir o que o jogo é.

Agora está auditando onde o texto ainda não expressa com precisão aquilo que o jogo já se tornou.
