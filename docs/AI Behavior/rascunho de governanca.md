# Governança do Sistema — doutrina em prosa

> **Natureza deste documento:** esta é a apresentação da doutrina do jogo em
> prosa, destinada a autores e revisores. Ela descreve **como o sistema deve se
> comportar**, sem misturar nomes de classes, auditoria do código ou estado de
> implementação. A tradução técnica desta doutrina vive em `governanca.md`.

## Upkeep

No início de cada rodada, o sistema executa três rotinas obrigatórias.

### Consumo de Autonomia

Unidades cujo `autonomyData` possui consumo de upkeep deduzem autonomia automaticamente.

### Pouso de Emergência

Aeronaves que chegam a zero de autonomia após o upkeep chamam `PodePousar`.

Se não houver pouso válido, são destruídas.

Quando o pouso é de emergência, a aeronave permanece com os motores desligados e não arremete automaticamente depois de ser suprida.

### Jornal do Comando

Apresenta um resumo da rodada anterior, especialmente útil em partidas assíncronas, incluindo acontecimentos relevantes e o estado das aeronaves.

---

# Ordens do Comandante

O jogador possui cinco ordens globais, disponíveis a qualquer momento durante seu turno.

## Serviço do Comando

Executa uma rotina de suprimento sobre unidades que:

- ainda não agiram;
    
- ainda não receberam atendimento na rodada;
    
- estão em uma construção aliada ou embarcadas em um supridor.
    

A validação utiliza `PodeSuprir`.

O Serviço do Comando não encerra a ação da unidade atendida. Ela ainda pode agir normalmente após receber o serviço.

## Dispensar Unidade

Destrói voluntariamente uma unidade.

É útil ao atingir o limite de unidades do tabuleiro ou quando uma unidade permanece tempo demais sem possibilidade de resgate, por exemplo.

O dinheiro investido na unidade não é recuperado.

## Comprar Unidade

Ao acessar uma construção produtora controlada, o jogador pode comprar unidades disponíveis em seu catálogo mediante pagamento em dinheiro.

## Passar a Vez

Encerra o turno independentemente de quantas unidades tenham agido.

## Inspecionar

Permite inspecionar:

- unidades aliadas que já agiram;
    
- unidades inimigas;
    
- construções.
    

Cada clique consecutivo sobre uma unidade revela novas informações, como área de ameaça, visão na névoa de guerra e outros dados disponíveis.

---

# Ciclo da Unidade

Toda unidade selecionada deve obrigatoriamente:

1. posicionar-se;
    
2. executar uma ação.
    

## Posicionamento

### Segurar Posição

A unidade permanece no hexágono atual.

### Escolher um Hexágono

A unidade assume provisoriamente uma nova posição.

Durante o posicionamento, o tabuleiro não recalcula seu estado definitivo.

O jogador pode cancelar e reposicionar a unidade quantas vezes quiser antes de escolher uma ação.

---

# Ações

Após o posicionamento, o sistema calcula as ações disponíveis por meio dos sensores `PodeX`.

## Fonte de Renda

### `PodeCapturar`

Requer a habilidade `Captura Construções`.

A unidade converte HP em progresso de captura.

Alguns papéis capturam com penalidade de 50%.

Construções cujos pré-requisitos não foram atendidos aplicam outra penalidade de 50% sobre o valor já reduzido.

As penalidades são multiplicativas: metade da metade, e não uma redução total de 100%.

---

## Combate

### `PodeMirar`

Requer armas em `EmbarkedWeapons`.

#### Combate de Contato

- Pode ocorrer parado ou após movimento.
    
- Utiliza armas com `rangeMin = 1`.
    
- Pode gerar revide.
    

#### Combate à Distância

- Ocorre normalmente apenas sem deslocamento.
    
- Utiliza armas com `rangeMin > 1`.
    
- Não gera revide.
    

Armas com `rangeMin = 0` são um tipo especial e ainda experimental de Combate à Distância.

Elas atacam alvos localizados no mesmo hexágono da unidade, mas em outra camada ou altura.

Ataques de alcance zero não geram revide.

#### Combate Híbrido

A unidade possui armas de contato e de longo alcance.

Primeiro tenta combater à distância. Se isso não for possível, tenta o Combate de Contato.

---

## Transporte

### `PodeEmbarcar`

O passageiro precisa conservar Pontos de Movimento suficientes para pagar o custo de entrada da célula onde está o transportador.

Esse custo considera o local efetivamente ocupado: construção, estrutura e terreno.

Esse valor torna-se o custo do embarque.

O transportador define:

- onde aceita embarque;
    
- quais tipos de unidade aceita;
    
- quais vagas oferece.
    

Multiplicadores de consumo de autonomia não são aplicados ao embarque.

Quando uma unidade embarca, o turno do transportador também é encerrado, mesmo que ele ainda não tenha agido.

### `PodeDesembarcar`

O transportador precisa estar em um local válido conforme sua ficha.

A ficha do transportador também determina quais locais podem receber sua carga.

O passageiro paga o custo de entrada da célula de desembarque.

Esse custo considera o local efetivamente ocupado: construção, estrutura e terreno.

Esse valor torna-se o custo do desembarque, sem multiplicadores de autonomia.

Desembarcar encerra o turno do transportador e de todas as unidades desembarcadas.

---

## Logística

### `PodeSuprir`

Uma unidade supridora converte suas reservas em serviços prestados em campo.

O alcance pode ser:

- mesmo hexágono ou unidades embarcadas;
    
- um hexágono adjacente;
    
- combinação das duas modalidades.
    

O atendimento ocorre na camada do supridor.

Quando possível, o supridor tenta igualar sua camada à do atendido.

Aeronaves pousam, recebem o serviço e arremetem.

Submersíveis emergem, recebem o serviço e permanecem na superfície.

O serviço possui custo e consome a ação do supridor, não a do atendido.

O sistema tenta encontrar um acordo... o caminhão de suprimentos não decola para encontrar o caça, mas o caça pode pousar! como houve acordo, o serviço ocorre; Entre Aviao tanker, helicoptero e um blindado, o blindado fica fora, o kc-130 só atende no ar, não no chao

Suprir encerra o turno do supridor, mas não o da unidade atendida.

---

## Estoque

### `PodeTransferir`

Unidades logísticas são classificadas como:

- **HUB:** trocam recursos entre si;
    
- **Receiver:** apenas recebem recursos.
    

A transferência:

- não possui custo financeiro;
    
- ocorre na mesma camada do supridor;
    
- consome a ação da unidade que iniciou a transferência.
    

Uma unidade ainda pode receber ou fornecer estoque depois de já ter agido, desde que a outra unidade se aproxime e inicie a operação.

Exemplo: um navio-tanque que já transferiu recursos para um porta-aviões encerrou sua ação. Ainda assim, uma fragata Receiver pode se aproximar e iniciar uma nova transferência, recebendo recursos desse navio-tanque.

Aeronaves de carga pousam para transferir e não arremetem.

---

## Sobrevivência

### `PodeFundir`

Utiliza a mesma lógica de aproximação e custo de entrada do embarque.

Só ocorre entre unidades:

- aliadas;
    
- idênticas;
    
- compatíveis para fusão.
    

A unidade selecionada entra no candidato e é absorvida, formando uma única unidade consolidada.

O HP é somado diretamente.

Munição e autonomia são redistribuídas por média ponderada.

Após a fusão, a unidade resultante encerra sua vez.

---

## Mobilidade

### `ApenasMover`

Confirma a posição provisória sem executar outra ação.

A unidade pode ter se deslocado ou permanecido parada.

Segurar posição pode ser útil para:

- manter uma linha;
    
- preservar uma posição;
    
- servir como observador avançado;
    
- aguardar uma oportunidade futura.
    

---

# Sensores Aéreos e Navais

Esses sensores não são apresentados diretamente ao jogador.

São utilizados internamente por outras ações.

## Sensores Aéreos

### `PodeDecolar`

Verifica se a aeronave consegue decolar até a altitude desejada.

Em pistas improvisadas, a decolagem pode ser limitada a uma única camada ou a um único hexágono.

Quando a aeronave parte de uma plataforma transportadora, a camada atual da plataforma e a regra de lançamento do compartimento determinam quantos degraus ela sobe, quantos hexágonos percorre e em qual camada termina.

Exemplo: um porta-aviões em `Naval/Surface` lança a aeronave um degrau acima; ela avança um hexágono e termina em `Air/Low`. Uma plataforma aérea em `Air/High` pode lançá-la sem rebaixá-la, mantendo-a em `Air/High`.

### `PodeArremeter`

Faz a aeronave pousar, aguarda a operação que acionou o sensor e chama `PodeDecolar` em seguida.

Exemplo: um caça em `Air/High` sobrevoa uma pista. Um supridor na planície o aciona, o caça pousa, recebe reabastecimento e decola novamente para `Air/Low`.

### `PodePousar`

Verifica se a aeronave possui as habilidades necessárias para pousar no local, como:

- `VTOL`;
    
- `STOVL`;
    
- `Aircraft Landing`;
    
- `Aircraft Carrier Landing`;
    
- `Sea Landing`.
    

Exemplo: um hidroavião sobrevoando o mar pousa quando fica sem combustível ou quando recebe suprimentos de um navio-tanque.

### `PodeMudarDeAltitude`

Permite reposicionar a aeronave entre altitudes, geralmente durante operações de suprimento.

Exemplo: um KC-130 em `Air/High` aproxima-se de helicópteros em `Air/Low` e caças em `Air/High`.

Como o helicóptero não pode subir, o avião-tanque desce para `Air/Low`, e os caças também nivelam nessa camada. Com todos na mesma altitude, o reabastecimento em voo pode acontecer.

## Sensores Navais

### `PodeEmergir`

Verifica se o submarino pode subir à superfície.

É utilizado, por exemplo, quando um navio passa sobre sua posição ou quando outra operação exige que ele esteja na superfície.

### `PodeSubmergir`

Verifica se o submarino pode retornar à camada submersa.

Submarinos atingidos ou que dispararam permanecem temporariamente impedidos de submergir.

### `PodeSubmergirRapidamente`

Permite que uma unidade termine submersa mesmo tendo iniciado a ação na superfície, desde que todas as condições sejam atendidas.

---

# Visão, Busca e Detecção

## Camadas de Visualização

O jogo apresenta ao jogador as seguintes camadas de revelação de hexágonos.

### `ALL`

Exibe no tabuleiro todos os hexágonos liberados pela combinação dos sensores.

### Aéreo

Exibe apenas os hexágonos e contatos revelados pelas unidades de vigilância aérea.

### Superfície

Exibe apenas os hexágonos revelados pela visão de superfície das unidades.

### Submarina

Exibe o fundo do mar e mantém o restante do tabuleiro escurecido.

Na visualização padrão, considera-se a linha de visão descendente da posição da unidade até a superfície.

Se essa linha encontrar um obstáculo geográfico, os hexágonos posteriores não são revelados na camada padrão.

Ainda assim, uma unidade pode detectar contatos pertencentes a outra camada.

Exemplo: um Super Tucano está em `Air/Low`, atrás de uma montanha. O jogo revela três hexágonos ao jogador, mas não revela o quarto, pois a linha descendente foi bloqueada pela geografia.

Entretanto, o Super Tucano possui visão aérea global de quatro hexágonos e detecta um helicóptero inimigo. Na visualização padrão, o helicóptero aparece desfocado sobre a névoa. Ao mudar para a camada aérea, o jogador consegue visualizá-lo corretamente.

---

## Névoa de Guerra

### `PodeEnxergar`

Libera hexágonos do tabuleiro conforme:

- alcance;
    
- elevação;
    
- linha de visão.
    

O padrão da visão é projetado em direção ao chão.

A curva de visão pode permanecer nivelada ou descer ao longo do percurso.

Enxergar um hexágono não significa necessariamente detectar todas as unidades presentes nele.

O resultado depende:

- do alcance de visão;
    
- das habilidades de detecção;
    
- das ocultações presentes no local.
    

`HexEnxergado` é uma consulta que parte do hexágono e identifica quem consegue vê-lo.

Na visualização padrão do tabuleiro, o jogador vê os hexágonos revelados pela visão de superfície.

### `PodeDetectar`

Unidades com sensores especializados procuram ocultações específicas.

O sensor e a ocultação precisam possuir etiquetas compatíveis.

Exemplos:

- caça submarina procura unidades com operações submarinas;
    
- detector de furtivos procura ocultação aérea `Stealth`.
    

Quando a correspondência é encontrada, a unidade oculta é revelada.

Se a unidade for detectada atrás da névoa de guerra, ela aparece desfocada sobre a névoa.

A consulta inversa — “alguém consegue me detectar?” — utiliza a mesma lógica a partir do ponto de vista da unidade observada.

---
# Hotzone

A Hotzone nasceu de uma necessidade básica:

> Onde posso me posicionar sem sofrer o ataque de uma unidade que move 6 hexágonos e ataca no sétimo?

Com o tempo, essa leitura deu origem a dois conceitos baseados em movimento: as bandas **Tática** e **Operacional**.

O serviço de Hotzone não escolhe a melhor posição nem executa ações. Ele apenas devolve uma área conforme as instruções recebidas.

Essa área serve de base para diversos serviços da IA, permitindo que movimento, combate, transporte, logística e vigilância utilizem o mesmo vocabulário espacial.

Consulte `hotzone e bandas de alcance.md` para a definição completa.

Em resumo:

- **Tático:** utiliza o MP restante da unidade;
    
- **Operacional:** projeta duas bandas sucessivas de MP;
    
- **Range:** alcance da arma, serviço ou outro efeito produzido pela unidade.
    

---

# Papéis da IA

A IA organiza as unidades por papéis.

Cada papel interpreta de maneira diferente as Hotzones e as opções devolvidas pelos sensores `PodeX`.

Os sensores são os mesmos para todas as unidades. O papel determina apenas quais intenções serão priorizadas e como as opções válidas serão avaliadas.

## Papéis principais

### Capturador

Prioriza a captura de construções.

Combate quando necessário, normalmente depois de avaliar a possibilidade de captura.

### Assalto

Prioriza combate de contato, avanço e ruptura da linha inimiga.

### Fire Support

Prioriza combate à distância e posicionamento de apoio na Retaguarda.

### Transportador

Coordena passageiros, pontos de encontro, embarque, desembarque e transporte entre objetivos.

Pode atuar como pickup, courier ou táxi.

### Logística

Presta serviços de campo, mantém unidades operacionais e participa da cadeia de suprimentos.

### Vigilância

Prioriza revelação da névoa de guerra, observação e detecção de unidades ocultas.

Foi criada originalmente para operações em `Air/High`, mas passará a ser baseada na visão especializada e nas habilidades de detecção da unidade.

---

# Especializações de Papel

Algumas funções modificam o comportamento de um papel principal ou participam de mais de uma categoria.

### Capturador Agressivo

Especialização de Capturador.

Prefere atacar primeiro e capturar depois.
Captura com uma eficiência menor que um Capturador.

### Interceptador

Especialização de Assalto voltada contra alvos aéreos.
Usado por unidades aéreas de contato

### Ataque Aéreo

Especialização de Assalto para aeronaves atacando alvos de superfície.
Usado por unidades aéreas de ataque ao solo

### Artilheiro Combatente

Participa primeiro de Fire Support, tentando combater à distância.

Quando não encontra uma solução válida de longo alcance, passa para Assalto.

### Antiaéreo Combatente

Segue a mesma lógica do Artilheiro Combatente, mas contra alvos aéreos.

Tenta primeiro o combate antiaéreo à distância e, quando aplicável, recorre ao comportamento de Assalto.

### Antiaéreo

Especialização estacionária de Fire Support voltada ao controle do espaço aéreo.

### Estoque

Especialização de Logística responsável pela movimentação de consumíveis:

- entre unidades;
    
- de unidades para construções;
    
- de construções para unidades;
    
- entre agentes logísticos compatíveis.
    

---

# Papéis Incorporados ou Descontinuados

### Raid Antissubmarino

Será incorporado à Vigilância.

Pode tornar-se uma especialização chamada **Vigilância Naval**.

### Transportador Aéreo

Foi incorporado ao papel Transportador.

O mesmo papel também receberá regras específicas para transporte naval.

---

# Hierarquia dos Papéis

- **Capturador**
    
    - Capturador Agressivo
        
- **Assalto**
    
    - Interceptador
        
    - Ataque Aéreo
        
    - Artilheiro Combatente
        
    - Antiaéreo Combatente
        
- **Fire Support**
    
    - Antiaéreo
        
- **Transportador**
    
- **Logística**
    
    - Estoque
        
- **Vigilância**
    
    - Vigilância Naval

# Comportamento Magnético

O comportamento Magnético determina qual referência espacial cada unidade procura acompanhar.

A unidade não recebe uma posição fixa. Ela é atraída por um líder, objetivo ou necessidade compatível com seu papel.

## Unidades sem Plano

Uma unidade sem plano escolhe uma referência próxima para seguir.

Normalmente, essa referência é um Capturador eleito como **Capitão**.

Se o Capitão for destruído, a unidade procura outro Capturador próximo.

Quando o Capitão embarca, as unidades sem plano procuram temporariamente outro Capitão.

O acompanhamento de Capitães embarcados ainda precisa ser definido.

## Unidades com Plano

Uma unidade com plano viaja até o setor designado.

Ao chegar:

1. procura um Capturador no setor para eleger como Capitão;
    
2. se não houver Capturador, utiliza a própria `RepCell` do setor como referência.
    

A `RepCell` funciona como um Capitão abstrato até que uma liderança real esteja disponível.

---

# Atração dos Papéis Principais

## Capturador

É atraído por:

- construções capturáveis;
    
- construções aliadas sob captura ou ataque.
    

## Assault

É atraído por Capturadores próximos.

Um deles é eleito Capitão.

A unidade procura posições de:

- Vanguarda;
    
- Flanco.
    

## Fire Support

É atraído por Capturadores próximos.

O Capturador escolhido torna-se seu Capitão.

A unidade se posiciona dentro do envelope da formação, ocupando a região definida para apoio de fogo.

## Transportador

É atraído por unidades que desejam alcançar objetivos além da própria Banda Operacional.

Algumas unidades também podem solicitar transporte para objetivos dentro da Banda Operacional, conforme seu papel, autonomia ou modalidade de emprego.

## Logística

É atraída, nesta ordem geral, por:

1. unidades em estado crítico;
    
2. unidades que precisam de manutenção preventiva;
    
3. Capitão, quando não existe atendimento prioritário.
    

## Vigilância

É atraída por:

- áreas ainda cobertas pela névoa de guerra;
    
- Capitão, quando não há setor prioritário para observar.
    

A Vigilância não deve necessariamente utilizar todo o seu movimento.

Para evitar avançar sobre forças inimigas ainda não detectadas, pode limitar seu deslocamento a uma fração da Banda Tática, como:

**Tático ÷ 2**

Essa limitação ainda está em avaliação.

---

# Atração dos Papéis Secundários

## Capturador Agressivo

Utiliza as mesmas referências do Capturador.

A diferença está na decisão local: tende a atacar antes de continuar a captura.

## Interceptador

É atraído por:

- unidades de Vigilância Aérea;
    
- Capitão.
    

Entre as referências disponíveis, acompanha a mais próxima ou mais relevante.

## Ataque Aéreo

É atraído por:

- Interceptador;
    
- Capitão, quando não há Interceptador adequado.
    

## Artilheiro Combatente

É atraído pelo Capitão e acompanha a Vanguarda. É principalmente uma unidade de Assault, mas tenta primeiro utilizar suas armas de longo alcance.

Quando não encontra uma solução de tiro à distância, continua o avanço e combate por contato.

## Antiaéreo Combatente

É atraído por:

- aeronaves inimigas detectadas;
    
- Capitão, quando não há ameaça aérea prioritária.
    

## Antiaéreo

É atraído por:

- Vigilância Aérea;
    
- Capitão.
    

A Vigilância oferece informação e orientação; o Capitão mantém a unidade integrada à formação.

## Estoque

É atraído por:

- construções aliadas sem recursos;
    
- unidades supridoras;
    
- Capitão, quando não há demanda logística prioritária.
    

---

# Princípio Magnético

Cada papel possui uma referência preferencial.

A unidade se desloca em direção a essa referência até entrar na região adequada para exercer seu papel.

O Magnetismo não escolhe obrigatoriamente um hexágono exato. Ele define:

- quem ou o que a unidade acompanha;
    
- em qual direção deve progredir;
    
- qual Hotzone deve procurar;
    
- qual região da formação deve ocupar.
    

A posição final é escolhida pelo serviço responsável, considerando:

- Hotzones;
    
- Vanguarda;
    
- Retaguarda;
    
- Flancos;
    
- caminhos válidos;
    
- segurança;
    
- utilidade para o papel.
    

Assim, o Capitão organiza a formação sem controlar diretamente cada unidade.

# Usos da Hotzone

As unidades utilizam a Hotzone de maneiras diferentes, conforme a intenção.

A Hotzone delimita a área relevante. Os serviços especializados localizam candidatos nessa área, consultam os sensores `PodeX`, comparam as respostas e apresentam as melhores opções. O papel da unidade decide o que fazer com elas.

## Melhor Captura

Baseia-se em `PodeCapturar` e serve principalmente ao Capturador.

Para unidades sem plano, procura a construção capturável mais adequada dentro das bandas Tática e Operacional. Unidades com plano recebem seus destinos das ordens atribuídas.

Construções aliadas que perderam pontos de captura também são candidatas, pois podem precisar de defesa ou reconquista.

A busca usa movimento geográfico e caminhos válidos: não basta uma construção estar perto; a unidade precisa conseguir alcançá-la.

## Melhor Combate

Baseia-se em `PodeMirar` e atende principalmente Assalto e Fire Support.

Unidades de Assalto combatem oponentes na banda Tática e se aproximam dos que estão na Operacional. Unidades de Fire Support atacam na Tática e se reposicionam para obter solução de tiro contra alvos na Operacional.

Para Fire Support, as bandas relevantes são orientadas pelo alcance das armas, não apenas pelo movimento da unidade. Unidades híbridas tentam primeiro o comportamento de Fire Support e, sem solução válida, passam ao comportamento de Assalto.

## Melhor Embarque e Quero Carona

Baseiam-se em `PodeEmbarcar`.

Melhor Embarque avalia a unidade que deseja embarcar, o destino que ela pretende alcançar, os transportadores disponíveis e os pontos de encontro possíveis. A preferência é encontrar uma solução dentro da banda Operacional; quando isso não for possível, a unidade se aproxima do melhor encontro disponível.

Quero Carona representa o pedido do passageiro. Ele comunica aos transportadores a referência magnética ou o destino pretendido, permitindo que eles organizem coleta, encontro e entrega.

## Melhor Desembarque

Baseia-se em `PodeDesembarcar` e pertence ao lado do transportador.

O transportador avalia os destinos de seus passageiros, os locais válidos para desembarque e o cruzamento das bandas Tática e Operacional projetadas ao redor desses destinos. A melhor entrega permite que o maior número possível de passageiros prossiga até seus objetivos em poucas rodadas.

## Melhor Atendimento

Baseia-se em `PodeSuprir` e serve às unidades de Logística.

A Hotzone determina quem pode ser alcançado e atendido. Melhor Atendimento compara as necessidades encontradas e prioriza unidades críticas, manutenção preventiva e a formação acompanhada.

## Melhor Estoque

Baseia-se em `PodeTransferir` e serve ao papel Estoque.

Identifica quem possui recursos, quem precisa recebê-los, quais faixas podem ser transferidas e onde os encontros logísticos podem acontecer. O objetivo é organizar coleta, redistribuição e entrega sem confundir movimentação de estoque com atendimento de campo.

## Melhor Pouso

Baseia-se em `PodePousar`.

Organiza locais e plataformas de pouso válidos dentro das bandas Tática e Operacional. A permissão para pousar continua pertencendo ao sensor; o serviço apenas compara as alternativas autorizadas.

## Melhor Fusão

Baseia-se em `PodeFundir`.

A regra geral é retornar à Retaguarda antes de procurar uma unidade idêntica para fusão. Unidades Elite podem ignorar recomposições de pouco valor, Fire Support tende a recuar quando precisa se recompor, e unidades em risco imediato podem fundir fora da Retaguarda para sobreviver.

