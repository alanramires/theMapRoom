# Fundamentos das ações por papel

Esta primeira parte descreve oito ações da unidade selecionada após o movimento provisório. O papel da unidade orienta suas prioridades; suas capacidades e o retorno dos sensores determinam quais opções podem ser utilizadas em cada situação.

O fluxo é sempre o mesmo: **primeiro a unidade se posiciona; depois escolhe o que fazer naquela posição.** Permanecer no mesmo hex também é uma escolha de posicionamento.

---
> Esse movimento é provisório até a confirmação da ação. Antes dela, o jogador pode cancelar sem alterar o estado confirmado do tabuleiro.
---

Uma opção disponível representa uma possibilidade, não uma obrigação. Poder capturar, atacar ou embarcar não impede a unidade de apenas confirmar sua posição.

## Convenção: alcance tático, operacional e estratégico

As decisões da IA utilizam as faixas **tática** e **operacional** para delimitar as possibilidades que serão examinadas. Essas faixas representam a capacidade relevante para a decisão; não são raios fixos em hexes nem possuem sempre a unidade como centro.

### Faixas de movimento

Quando a decisão depende de deslocamento, o alcance converte os pontos de movimento em células efetivamente alcançáveis, considerando o custo do caminho e as restrições da unidade:

- **Tático:** o que a unidade consegue alcançar na rodada considerada, com o movimento disponível.
- **Operacional:** o que consegue alcançar ao longo de duas rodadas, respeitando o orçamento de movimento de cada uma.

Por exemplo, uma unidade com **3 pontos de movimento**, atravessando montanhas que custam **2 pontos por hex**, alcança apenas **1 hex no tático**. Ela gasta 2 pontos no primeiro hex e o ponto restante não basta para entrar no segundo.

No operacional, ela alcança **2 hexes**: anda um na primeira rodada, encerra a vez, recupera seus 3 pontos de movimento na rodada seguinte e anda mais um. Não pode somar os orçamentos como se tivesse 6 pontos contínuos para atravessar três montanhas.

**O operacional não é calculado simplesmente dobrando a distância tática.** Neste exemplo, o resultado numérico é o dobro, mas ele vem da simulação de duas rodadas separadas. Custos e restrições do percurso determinam o resultado em cada caso.

### Referência da consulta

As faixas também podem ser consultadas em relação ao alvo desejado. Para uma captura, por exemplo, a pergunta pode ser **"quais capturadores chegam a este prédio no tático ou no operacional?"**. O prédio é a referência da busca, mas cada candidato conserva seus próprios pontos de movimento, custos de terreno e restrições.

Em um atendimento aéreo, a análise pode procurar a interseção das possibilidades de alcance das aeronaves envolvidas para identificar posições compatíveis com o serviço. A faixa precisa ser interpretada junto com a operação que está sendo avaliada.

### Faixas baseadas na arma

Quando a decisão depende da capacidade de fogo, a referência pode ser o alcance da arma, em vez do deslocamento da unidade.

Uma artilharia de campanha que **move 1** e possui arma de alcance **3~4**, por exemplo, utiliza **tático 4** e **operacional 8** nessa escala de avaliação de fogo. Essa convenção delimita a região examinada; não significa que a arma possa disparar a 8 hexes nem que todos os hexes dentro da faixa tática sejam alvos válidos. Continuam valendo o alcance mínimo de 3, o máximo de 4 e as demais condições de ataque.

O operacional 8 da artilharia de campanha não é alcance de tiro. É limiar de decisão: com inimigo dentro de 8 hexes, mover a peça perde uma chance de tiro e, na rodada seguinte, o inimigo já está no quintal. Nesse caso, a IA prefere não reposicionar.

A regra de movimento em duas rodadas e a escala baseada na arma são contextos diferentes. Toda consulta precisa deixar claro **qual capacidade define a faixa e em relação a qual unidade, alvo ou posição ela está sendo avaliada**.

### Faixa estratégica

O que fica além das faixas tática e operacional da consulta é considerado **estratégico** e geralmente permanece fora do escaneamento local. Essa distância continua relevante para os eixos e objetivos do planejamento, mas normalmente não entra na avaliação imediata de oportunidades da unidade.

## 1. Reposicionar

Reposicionar confirma o posicionamento da unidade e encerra sua ação, sem executar outra atividade.

É a opção básica do jogo. A unidade pode utilizá-la tanto para avançar quanto para permanecer onde está, mesmo que os sensores indiquem outras ações disponíveis.

O rótulo acompanha o movimento realizado:

| Situação | Rótulo |
|---|---|
| A unidade se deslocou para outro hex. | **Apenas Mover** |
| A unidade permaneceu no hex de origem. | **Confirmar Posição** |

Para a IA, reposicionar pode servir para aproximar-se de um objetivo, ocupar uma posição defensiva, liberar passagem ou aguardar. A existência de outra ação disponível não elimina essas razões.

## 2. Capturar

Capturar utiliza a capacidade da unidade para conquistar uma construção ou reforçar o controle de uma construção própria ou aliada.

A unidade precisa possuir uma skill aceita pela construção, como:

- **Captura Construções**
- **Capturador Alternativo**

A construção define quais skills aceita e a eficiência correspondente. Os pontos aplicados são calculados a partir do HP atual da unidade e da eficiência de captura, sem consumir HP.

Depois do posicionamento provisório, o sensor verifica se a unidade está sobre uma construção válida e indica se a opção está disponível. Os pontos de captura só são aplicados quando a ação é confirmada.

O rótulo distingue as duas situações:

| Situação | Rótulo |
|---|---|
| Converter uma construção neutra ou inimiga para seu lado. | **Conquistar** |
| Recuperar pontos de controle de uma construção própria ou aliada. | **Reforçar controle** |

**Conquistar** reduz os pontos da construção. Quando chegam a zero, ela passa a pertencer ao jogador que realizou a captura e seus pontos são restaurados ao máximo.

**Reforçar controle** aumenta os pontos da construção até o máximo, mantendo o proprietário atual. Se os pontos já estiverem no máximo, essa opção não fica disponível.

Para a IA, a disponibilidade dessa opção responde **"posso capturar aqui?"**. A decisão de iniciar, continuar ou ceder a captura depende do papel, da missão e da situação tática.

## 3. Embarcar

Embarcar é a ação do passageiro de entrar em um transportador. A unidade que escolhe essa opção é quem será transportada.

Depois do movimento provisório, o passageiro precisa estar adjacente a um transportador aliado, com vaga compatível disponível, e ainda possuir pontos de movimento suficientes para pagar o custo de embarque.

Normalmente, o custo dessa entrada depende do terreno e das regras de movimento do passageiro. Por exemplo, se entrar em uma montanha custa 2 pontos de movimento para um soldado, ele precisa ter pelo menos 2 pontos restantes para embarcar em um transportador naquele hex.

Quando o passageiro não pode entrar normalmente no hex do transportador, o embarque pode usar uma regra de transição com custo de **1 ponto de movimento**. Essa transição ainda exige que o passageiro cumpra os requisitos de skill do terreno e todas as demais condições de embarque.

É o caso de um soldado embarcando em um navio na praia: o navio precisa estar em um local onde suas regras permitem receber o passageiro. O soldado pode então transicionar para o hex do navio, mesmo que não pudesse ocupá-lo normalmente. Ele está entrando no transportador, não ganhando permissão para andar naquele terreno.

Além das regras de movimento do passageiro, o transportador possui suas próprias condições de embarque, que podem restringir:

- Os tipos de unidade que aceita transportar.
- As skills exigidas do passageiro.
- Os locais em que permite o embarque.

O sensor verifica as condições e indica os embarques disponíveis. A entrada no transportador só se torna definitiva quando a ação é confirmada.

**O ponto de vista é sempre o da unidade selecionada.** Ao escolher Embarcar, essa unidade assume o papel de passageiro, mesmo que ela própria seja um transportador carregando outras unidades.

Isso permite o **embarque aninhado**: um APC com um soldado a bordo pode se mover até ficar adjacente a uma fragata compatível e escolher Embarcar. O APC entra na fragata levando seu passageiro; o soldado continua embarcado no APC. Essa ação depende das condições de embarque do APC na fragata, incluindo compatibilidade, vaga e movimento restante.

Aproximar um transportador de outra unidade não faz essa outra unidade entrar nele automaticamente. Se o APC selecionado chega ao lado de um soldado, ele não recolhe o soldado pela opção Embarcar. Se chega ao lado de uma fragata compatível, pode usar essa opção para entrar nela. **Quem embarca é a unidade que está realizando a ação.**

**Embarcar consome a ação tanto do passageiro quanto do transportador.** Depois de executar essa opção, nenhum dos dois pode realizar outra ação naquele turno.

Para a IA, a disponibilidade dessa opção responde **"posso entrar neste transportador?"**. A decisão de embarcar depende de como esse transporte contribui para a missão do passageiro.

## 4. Desembarque

Desembarcar é a ação da unidade selecionada de liberar uma carga que está transportando. O ponto de vista é o do transportador: ele escolhe qual passageiro desembarcar e em qual destino válido.

A disponibilidade depende de duas condições distintas:

- **Local do transportador:** ele precisa estar em um local onde suas regras permitem realizar o desembarque.
- **Destino do passageiro:** o hex de saída precisa ser válido para aquela carga, conforme as regras de desembarque, suas capacidades e seu domínio/altura.

Um trem, por exemplo, só pode liberar carga quando está em uma estação. Estar ao lado de um hex que o passageiro poderia ocupar não basta se o próprio trem não estiver em um local autorizado para desembarcar.

Um porta-aviões no mar ou na praia pode liberar seus caças nas direções permitidas pelo desembarque, independentemente do terreno abaixo do destino. Os caças saem em sua camada aérea, de modo que uma floresta ou outro terreno no solo não impede essa saída. As demais condições de validade do destino continuam sendo verificadas.

Já um Chinook pousado na planície não pode liberar sua carga na floresta. O local em que o transportador está autorizado a operar e os destinos em que pode colocar a carga precisam ser avaliados separadamente.

Depois do posicionamento provisório, o sensor indica as combinações válidas de passageiro e destino. O desembarque só se torna definitivo quando a ação é confirmada.

**O desembarque não libera cargas aninhadas em uma única ação.** Ele retira apenas o passageiro diretamente embarcado no transportador selecionado. Se esse passageiro também transporta unidades, elas permanecem dentro dele.

No exemplo do APC com um soldado a bordo, ambos atravessam o canal dentro da fragata. Ao chegar à praia, a fragata escolhe Desembarque e libera o APC. A ação da fragata e a do APC terminam, enquanto o soldado continua embarcado no APC. Somente no próximo turno o APC pode ser selecionado para realizar seu próprio desembarque e liberar o soldado; essa segunda operação consome a ação do APC e a do soldado.

**Desembarcar consome a ação tanto do transportador quanto do passageiro desembarcado.** Depois de executar essa opção, nenhum dos dois pode realizar outra ação naquele turno. A mesma regra de consumo de ação vale para o embarque.

Para a IA, a disponibilidade dessa opção responde **"qual passageiro posso liberar e onde posso colocá-lo?"**. A decisão considera como a posição de desembarque contribui para a missão do passageiro e para a operação do transportador.

## 5. Mirar

Mirar é a ação padrão de combate da unidade selecionada. Sua disponibilidade depende das armas embarcadas na unidade, do posicionamento provisório e da existência de um alvo válido.

Cada arma possui uma notação de alcance que indica como pode ser utilizada. As três formas são:

| Tipo de arma | Exemplo de alcance | Forma de combate |
|---|---|---|
| Combatente | **1** | Apenas combate adjacente. |
| Artilheiro | **2~3** | Apenas combate à distância, entre 2 e 3 hexes. |
| Híbrido | **1~3** | Combate adjacente e à distância, até 3 hexes. |

A notação com um único valor, sem a segunda marcação, identifica uma arma exclusiva de combatente. A unidade pode possuir diferentes armas; a validade do ataque depende da arma utilizada.

### Movimento e alcance disponível

**O ataque à distância só está disponível quando a unidade permaneceu no mesmo hex durante o movimento provisório.** Se ela se deslocou, pode realizar apenas combate adjacente, desde que possua uma arma compatível com essa modalidade.

| Arma dos exemplos | Permaneceu no mesmo hex | Moveu-se para outro hex |
|---|---|---|
| Combatente — **1** | Pode atacar a 1 hex. | Pode atacar a 1 hex. |
| Artilheiro — **2~3** | Pode atacar entre 2 e 3 hexes. | Não pode atacar com essa arma. |
| Híbrido — **1~3** | Pode atacar entre 1 e 3 hexes. | Pode atacar apenas a 1 hex. |

No caso híbrido, mover-se mantém a possibilidade de combate adjacente e retira a possibilidade de tiro à distância. Isso não significa liberar o alcance mínimo de qualquer arma: uma arma de artilheiro **2~3** continua sem poder disparar depois de se mover.

### Validade do alvo

Estar dentro do alcance não basta. O sensor também verifica munição disponível, compatibilidade entre a arma e o domínio/altura do alvo e os demais critérios de combate.

Um rifle, por exemplo, não pode atacar um caça apenas porque as unidades estão em hexes adjacentes. A arma precisa ser capaz de atingir aquele alvo. Quando existe uma combinação válida de arma e alvo, a opção Mirar fica disponível.

### Revide e aplicação das baixas

**No combate adjacente, pode haver revide.** O defensor precisa atender às condições necessárias para responder, incluindo munição, alcance e compatibilidade da arma com o atacante. O revide é uma possibilidade condicionada por essas regras, não uma resposta garantida.

A arma de revide é escolhida automaticamente pelo jogo, com prioridade para a primária. Se a primária não for compatível com o atacante (ex.: canhão 105mm contra helicóptero), a secundária pode revidar (ex.: metralhadora montada). Critérios: domínio, alcance, munição e detecção.

Quando há revide, **as baixas dos dois lados são aplicadas simultaneamente**. A resposta do defensor não é calculada apenas com o que sobreviver ao ataque recebido: ambos participam da troca de fogo antes da aplicação das baixas resultantes.

**No combate à distância, nunca há revide**, mesmo que duas artilharias estejam dentro do alcance uma da outra.

A seleção de arma e alvo faz parte da preparação da ação. O ataque e seus efeitos só se tornam definitivos quando a ação é confirmada.

Para a IA, a disponibilidade dessa opção responde **"com qual arma posso atacar qual alvo a partir desta posição?"**. A decisão de atacar considera a missão, o resultado esperado do combate e, quando aplicável, o revide que a unidade pode receber.

## 6. Fundir

Fundir consolida duas unidades em uma só. A unidade selecionada realiza a fusão com outra unidade adjacente e compatível, depois do movimento provisório. Ao confirmar, deixam de existir duas peças separadas e permanece uma unidade com os valores resultantes da fusão.

A fusão funciona como um embarque definitivo: as unidades passam a formar uma única peça, sem a possibilidade de separá-las por desembarque.

### Condições para fundir

A fusão acontece apenas entre unidades do mesmo tipo. Um caça não pode se fundir a um tanque.

Além da adjacência e do tipo de unidade, o sensor verifica as regras de fusão, incluindo domínio, movimento restante e os demais critérios de compatibilidade. Estar ao lado de uma unidade do mesmo tipo não garante, por si só, que a fusão esteja disponível.

**Transportadores com cargas embarcadas não podem se fundir.**

### Valores resultantes

| Atributo | Resultado da fusão |
|---|---|
| HP | Soma dos HP das duas unidades, limitada a **10**. |
| Munição | Calculada por média ponderada. |
| Autonomia | Calculada por média ponderada. |

Munição e autonomia não são simplesmente somadas. Uma unidade com pouco HP e pouca autonomia, ao se fundir com outra de HP e autonomia médios, reduz a autonomia resultante em relação à segunda unidade. O mesmo princípio vale para a munição: os recursos de ambas participam do resultado conforme a ponderação da fusão.

**Confira a prévia da unidade resultante antes de confirmar.** Ganhar HP não significa necessariamente ganhar munição ou autonomia em relação à unidade que estava em melhores condições.

### Ganho de força e perda de presença

A vantagem da fusão depende da situação e da missão das unidades.

Para capturadores, concentrar HP aumenta o poder de captura da unidade resultante, pois esse poder é baseado no HP. Em troca, o exército passa a ter menos capturadores para ocupar posições e trabalhar em objetivos diferentes.

Para tanques, a unidade resultante pode ter mais poder de fogo e representar uma ameaça maior individualmente. Em troca, o exército passa de dois tanques para um: perde a possibilidade de manter duas posições e realizar dois ataques separados quando ambos poderiam agir.

Fundir, portanto, exige comparar a força da unidade resultante com o valor de manter duas unidades independentes em campo.

A escolha do parceiro e a visualização do resultado fazem parte da preparação. A fusão e seus efeitos só se tornam definitivos quando a ação é confirmada.

Para a IA, a disponibilidade dessa opção responde **"com qual unidade posso me fundir e qual será o resultado?"**. A decisão considera o ganho de capacidade da peça resultante, os recursos que ela terá e a perda de presença e de ações independentes no tabuleiro.

## 7. Suprir

Suprir é a ação da unidade selecionada de prestar serviços logísticos a unidades adjacentes em campo.

Algumas unidades de logística carregam estoques de recursos além de seus próprios HP, munição e autonomia. Esses estoques são utilizados para prestar serviços que recuperam os atributos de outras unidades.

| Recurso transportado | Serviço | Atributo recuperado no cliente |
|---|---|---|
| Galões | Abastecimento | Autonomia. |
| Caixas | Rearmamento | Munição. |
| Peças | Reparos | HP. |

Os recursos destinados ao atendimento são distintos dos atributos do próprio supridor: transportar galões para abastecer outras unidades não é o mesmo que possuir autonomia para se deslocar.

### Custo dos serviços

O atendimento consome os recursos correspondentes e tem um custo monetário tabelado, calculado conforme a eficiência do serviço e proporcional ao custo unitário da unidade atendida.

Por isso, recuperar um atributo de unidades diferentes pode ter custos muito distintos. Abastecer um soldado com ração e cantil pode custar, por exemplo, 3 de dinheiro, enquanto o abastecimento de uma artilharia de campanha pode chegar a 1000. Esses valores ilustram a diferença de custo entre os clientes; o valor aplicável depende das regras do serviço.

### Adjacência e compatibilidade de atendimento

O supridor e o cliente precisam estar adjacentes e conseguir estabelecer uma combinação compatível de domínio e altura para o serviço. A proximidade no mapa, sozinha, não garante que o atendimento seja possível.

Um caça SVTOL pode pousar para receber abastecimento de um caminhão. Se estiver sobrevoando uma floresta onde não consegue pousar, essa adequação não é possível e ele não recebe o serviço daquele supridor.

A adequação também pode partir do supridor, quando suas capacidades e as condições locais permitem. Um KC-130, por exemplo, pode descer para **Air/Low** para abastecer helicópteros que não conseguem subir até sua altura original.

O sensor considera a possibilidade de estabelecer essa compatibilidade. Havendo uma combinação válida e atendidas as demais condições do serviço, o suprimento pode ser realizado.

### Limites de recuperação

Reparos costumam recuperar no máximo **1 ou 2 pontos de HP por rodada**, conforme o serviço utilizado.

Abastecimento e rearmamento não seguem esse mesmo limite de 1 ou 2 pontos. A recuperação depende dos recursos disponíveis, dos custos e das regras do serviço, respeitando o máximo do atributo atendido.

Depois do posicionamento provisório, o sensor indica os atendimentos disponíveis. O consumo de recursos e dinheiro, a recuperação dos atributos e as demais alterações definitivas do atendimento só ocorrem após a confirmação da ação.

Para a IA, a disponibilidade dessa opção responde **"quais unidades posso atender, com quais serviços e a que custo?"**. A decisão considera a necessidade dos clientes, os estoques do supridor e a contribuição do atendimento para manter as unidades em condições de cumprir suas missões.

## 8. Transferir

Transferir movimenta estoques de recursos entre unidades e construções compatíveis. Esses recursos são a matéria-prima dos serviços logísticos: galões, caixas e peças que os supridores utilizam para abastecer, rearmar e reparar outras unidades.

Um supridor sem o recurso necessário não consegue prestar o serviço correspondente. Ele precisa buscar uma fonte de estoque e utilizar Transferir para receber recursos antes de voltar ao campo e retomar os atendimentos.

Enquanto Suprir utiliza recursos para recuperar atributos de uma unidade, Transferir movimenta os próprios recursos entre estoques. Receber galões, por exemplo, repõe o estoque disponível para prestar abastecimento.

### Hub e receiver

Unidades e construções podem assumir duas funções na rede de estoques:

| Função | Participação na transferência |
|---|---|
| **Hub** | Movimenta recursos de e para seu próprio estoque, trocando com vizinhos compatíveis. Pode receber e distribuir. |
| **Receiver** | Recebe recursos de uma fonte para seu próprio estoque. Não exerce a função de redistribuição de um hub. |

Essa distinção permite separar quem transporta e distribui recursos pela rede de quem os recebe para prestar serviços às unidades em campo.

### Transferência para unidades embarcadas

A transferência também pode atender unidades embarcadas, quando a relação de transferência é válida.

Um porta-aviões, por exemplo, pode usar Transferir para mover galões de seu estoque para um KC-130 embarcado nele. O avião passa a carregar a matéria-prima necessária para prestar reabastecimento aéreo quando posteriormente sair do porta-aviões e voltar a operar.

Nesse caso, o ponto de vista da ação é o da unidade selecionada: o porta-aviões realiza a transferência e o KC-130 recebe os recursos em seu estoque.

### Cadeia de distribuição

Os recursos podem passar por vários pontos antes de chegar ao supridor que atende as tropas. Por exemplo:

1. Um navio-tanque recebe recursos de um porto.
2. O navio navega até alto-mar e transfere recursos para um porta-aviões, ou cruza o canal para entregá-los em outro porto.
3. No porto de destino, um caminhão civil coleta recursos e os transporta até outra cidade.
4. Nessa cidade, um caminhão de suprimentos com função de receiver recebe os recursos.
5. O caminhão de suprimentos retorna à frente para prestar serviços às tropas.

Cada transferência depende de uma fonte com estoque e de uma relação válida entre os participantes. A cadeia conecta os pontos de coleta, os meios de transporte e os supridores que realizam o atendimento final.

**Transferir consome a ação da unidade que executa a opção.** A movimentação definitiva dos estoques só ocorre após a confirmação da ação.

Para a IA, a disponibilidade dessa opção responde **"de onde posso receber recursos ou para onde posso transferi-los?"**. A decisão considera os estoques disponíveis, a função de hub ou receiver e as necessidades dos supridores e dos pontos de distribuição que sustentam a frente.

# Parte 2. Ações do jogador

As oito ações anteriores são relativas à unidade selecionada após o movimento provisório. As ações desta parte não dependem dessa etapa: podem ser acessadas a qualquer momento, com ou sem unidade selecionada, conforme os requisitos de cada opção.

Quando uma ação atua sobre uma unidade específica, como Destruir/Dispensar, é necessário selecioná-la para indicar o alvo.

## 1. Destruir unidade / Dispensar unidade

Esta ação remove a unidade selecionada do jogo após a confirmação do jogador. A peça é perdida e os recursos gastos nela não retornam.

Dispensar uma unidade pode ser útil quando o limite de unidades em campo foi atingido ou quando uma unidade ferida está isolada, muito distante, e o jogador não consegue ou não pretende mobilizar recursos para recuperá-la.

Selecionar a opção não remove a unidade imediatamente: a remoção depende da confirmação.

### Aeronaves sem combustível

Unidades aéreas que ficam sem combustível entram na fila de pouso de emergência. O resultado depende da existência de um local válido para pousar, conforme as regras da aeronave — por exemplo, uma estrada para um avião que possa utilizá-la.

- **Sem local válido:** a aeronave cai e é destruída.
- **Com local válido:** a aeronave pousa e permanece no chão com **0 de combustível**, imóvel e sem poder atacar.

Uma aeronave que sobrevive ao pouso de emergência fica aguardando atendimento. O jogador pode enviar um supridor para recuperá-la ou selecionar a aeronave e confirmar sua destruição manual.

## 2. Serviços do Comando

Serviços do Comando realiza um atendimento logístico em lote às unidades elegíveis do jogador. Pode ser executado **uma vez por rodada do jogador**, a qualquer momento. Seu uso é opcional: o jogador pode escolher quando acioná-lo ou passar a rodada sem utilizá-lo.

**Por padrão, a IA sempre executa Serviços do Comando na abertura de sua vez, antes de iniciar as ações das unidades.** O atendimento continua sujeito às mesmas condições de elegibilidade e disponibilidade de recursos.

### Unidades elegíveis

O atendimento é destinado apenas a unidades que **ainda não agiram** e que estejam em uma das seguintes situações:

- Sobre uma construção válida para prestar o serviço.
- Embarcadas em um supridor capaz de atendê-las.

A elegibilidade continua sujeita às condições e aos recursos necessários para cada serviço.

### Atendimento em lote e cargas aninhadas

Serviços do Comando presta os mesmos tipos de serviço da ação Suprir, mas organiza o atendimento em lote para todas as unidades válidas. Esse atendimento alcança também unidades embarcadas e suas cargas aninhadas, desde que sejam elegíveis.

Por exemplo, uma fragata danificada chega a um porto com um APC danificado a bordo, e esse APC transporta soldados feridos. Nos Serviços do Comando, **a fragata, o APC e os soldados podem receber atendimento**, respeitando as condições de cada serviço.

Em campo, na mesma composição aninhada, o atendimento por um supridor à transportadora base alcançaria apenas a fragata. Serviços do Comando permite atender também às unidades que estão dentro dela, inclusive o soldado dentro do APC.

### Recursos e papel das construções

Serviços do Comando representa a mobilização da cidade ou do esforço de guerra para recuperar as unidades. Assim como na ação Suprir, os serviços consomem os recursos necessários ao atendimento.

Os estoques das construções são limitados. Uma cidade pode esgotar seus recursos e ficar sem condições de prestar determinados serviços até que seu estoque seja reposto.

**Serviços do Comando é a única opção do jogo que permite que uma cidade ou outra construção preste serviços a uma unidade em campo.** Estar sobre uma construção válida torna a unidade elegível; o atendimento ocorre por meio desta opção.

A disponibilidade do atendimento não autoriza alterações provisórias nos estoques ou nos atributos das unidades. O consumo de recursos e a recuperação só se tornam definitivos após a confirmação da execução.

# Parte 3. Sensores que não são ações do jogador

Os sensores desta parte não são opções que o jogador escolhe executar. Eles descrevem informações e condições do jogo que orientam as decisões do jogador e também são úteis para a IA.

## 1. Enxergar

Todas as unidades e construções possuem um alcance de visão, medido em hexes. Sob neblina de guerra, esse alcance participa da definição de quais células ficam visíveis e de quais partes do mapa passam a ser conhecidas pelo jogador.

### Alcance e linha de visão

Estar dentro do alcance de visão não garante que uma célula possa ser vista. Quando a regra de linha de visão está ativada, a elevação do terreno pode bloquear a visão entre o observador e o alvo.

Por exemplo, um soldado tem alcance de visão de 3 hexes, mas uma montanha no segundo hex bloqueia sua visão do terceiro. Nesse caso, o terceiro hex não é revelado por esse soldado, apesar de estar dentro de seu alcance nominal.

A linha de visão também importa em mapas sem neblina de guerra. O tabuleiro estar revelado não significa que todas as unidades tenham uma linha de visão livre até qualquer alvo. Se essa regra estiver ativada, o soldado pode continuar sem enxergar uma unidade atrás da montanha. Já o tiro parabólico de uma artilharia dispensa linha de visão direta até o alvo, mas ainda depende de o alvo estar disponível pelo conhecimento confirmado e pelas regras de detecção, além das demais condições de ataque.

Essas restrições dependem das configurações da partida. Há configurações que desativam os bloqueios, permitindo que a montanha funcione apenas como bônus defensivo nesse contexto.

### Visível, conhecido e desconhecido

Enxergar acrescenta conhecimento do mapa ao jogador, mas é necessário distinguir o que está sendo observado agora do que foi visto anteriormente.

| Estado do hex | Informação disponível |
|---|---|
| **Visível agora** | Há cobertura de visão atual sobre a célula. |
| **Conhecido, fora da visão atual** | O terreno já foi revelado, mas seu conteúdo atual não é mostrado; permanece a última informação conhecida. |
| **Desconhecido** | O hex ainda não foi revelado ao jogador. |

Quando uma unidade se afasta e o hex deixa de ser coberto pela visão do jogador, ele não volta a ser totalmente desconhecido. Permanece parcialmente revelado, como um local visitado, com a última informação conhecida, sem atualizar o que está acontecendo ali fora da visão atual.

Por exemplo, uma construção vista pela última vez como sua pode continuar aparecendo dessa forma no mapa mesmo depois de o jornal informar que ela foi perdida. A notícia da perda não atualiza automaticamente a representação visual daquele hex: é preciso voltar a enxergá-lo para atualizar essa informação.

### Uso pela IA e atualização do conhecimento

Para a IA, esse sensor ajuda a distinguir **o que está visível agora, o que é apenas informação anterior e o que ainda é desconhecido**. Uma informação antiga sobre um hex não garante que seu conteúdo permaneça igual.

O movimento provisório não revela novas células nem atualiza a memória do jogador ou da IA. A visão e o conhecimento definitivo são recalculados a partir do estado confirmado, após o compromisso da ação e o retorno a Neutral.

## 2. Detecção

Enxergar e detectar respondem a perguntas diferentes. Enxergar revela hexes e acrescenta conhecimento do terreno. Detectar torna um alvo visível para o jogador. **Um hex revelado não garante que todas as unidades presentes nele sejam detectadas.**

A visão do terreno parte da altura do observador e traça uma linha até o hex observado, respeitando o relevo e as regras ativas da partida. Um helicóptero pode enxergar a primeira cordilheira e talvez a segunda, mas continuar sem enxergar a planície atrás delas, conforme a altura e o ângulo dessa linha.

A detecção, por sua vez, depende dos canais de observação e das capacidades que permitem identificar cada tipo de alvo. Para atirar em uma unidade, é necessário que ela esteja detectada, além de cumprir as demais condições de ataque.

### Chave e fechadura

A detecção funciona como uma relação de chave e fechadura: a capacidade de ocultação do alvo exige uma capacidade de detecção compatível no observador, dentro do alcance correspondente.

Um navio com visão de 3 hexes pode estar ao lado de um submarino sem detectá-lo. Mesmo passando sobre sua posição, em outra camada, pode navegar sem que o submarino apareça no mapa. A proximidade e o conhecimento do terreno, por si só, não substituem a capacidade necessária para detectar aquele alvo.

Quando um observador possui a chave adequada e atende às condições de detecção, o alvo fica visível no tabuleiro para o jogador. Outras unidades desse jogador podem então atacá-lo, desde que suas próprias armas, alcances, munições e demais regras de combate permitam.

### Alcance de visão do terreno e alcance de detecção

Os alcances podem ser diferentes. Detectar uma unidade distante não exige revelar também o terreno abaixo dela.

Considere um EWACS com **visão de terreno 3** e **detecção aérea, incluindo stealth, de alcance 7**:

- A visão de terreno revela os hexes dentro de seu alcance de 3, conforme as regras de visibilidade.
- A detecção aérea torna visíveis as aeronaves dentro do alcance de 7, incluindo caças e bombardeiros furtivos, quando atendidas as condições desse canal.
- Uma aeronave detectada pode aparecer por cima da neblina preta, enquanto o terreno abaixo dela permanece desconhecido.

O jogador pode, portanto, saber que existe um caça naquela posição sem conhecer o chão sob ele. A detecção do contato e a revelação do terreno são informações distintas.

Agora considere um dirigível de vigilância hipotético, com **visão de terreno 3** e **alcance de observação aérea 6**, mas sem capacidade de detectar stealth. Ele pode observar aeronaves comuns nesse alcance, mas não revela aeronaves furtivas apenas por elas estarem dentro dos mesmos 6 hexes. Esse dirigível é um exemplo conceitual; não existe no jogo.

### Detecção de submarinos

A mesma relação de chave e fechadura vale para a caça submarina. Fragatas e Super Tucanos dependem de suas capacidades compatíveis para detectar submarinos. Revelar a superfície do mar não equivale a revelar o que está submerso.

### Detectar não garante uma trajetória de tiro válida

A detecção informa que o alvo está presente. A trajetória do disparo determina se a arma consegue atingi-lo. São verificações diferentes: **um alvo revelado e detectado ainda pode estar protegido por um obstáculo ao tiro**.

A detecção submarina, por exemplo, não utiliza linha de visão: utiliza propagação do som por hexes. Um submarino pode detectar outro do lado oposto de uma península por essa propagação indireta.

O torpedo, porém, não percorre o caminho do som. Seu disparo é reto, não faz curvas e não é parabólico. Se a linha até o alvo atravessa a península, passando por mar, superfície terrestre e novamente mar, ela cruza um domínio inválido para essa arma. **O alvo é detectado, mas o disparo é negado.**

O mesmo princípio vale para um canhão hipotético de tiro reto com alcance de 3 hexes. Mesmo que um soldado atrás de uma floresta esteja visível para o jogador, a floresta intermediária pode bloquear a trajetória e impedir o disparo. Conhecer a posição do alvo não faz o projétil atravessar o obstáculo.

Isso difere do tiro parabólico, que pode passar por cima de obstáculos conforme suas regras. A forma como o alvo foi detectado não altera o tipo de trajetória da arma.

**Contra um inimigo revelado e detectado em um hex adjacente, o ataque é permitido desde que sejam atendidas as condições de combate descritas no item 5, Mirar.** Não há um hex intermediário bloqueando o trajeto, mas continuam valendo munição, compatibilidade de arma e alvo e os demais critérios de combate.

### Uso pela IA e estado confirmado

Para a IA, esse sensor responde **"quais alvos estão detectados e quais capacidades permitem encontrá-los?"**. A cobertura de visão do terreno e a cobertura de detecção precisam ser consideradas separadamente ao posicionar unidades de vigilância ou procurar ameaças ocultas.

Assim como a visão, a detecção não publica novos contatos a partir de movimento provisório. A visibilidade definitiva dos alvos e a memória de inteligência são atualizadas a partir do estado confirmado, após o compromisso da ação e o retorno a Neutral.

# Parte 4. Sensores auxiliares

Além dos sensores anteriores, existem sensores auxiliares que verificam se uma unidade pode decolar, pousar, mudar de altitude, emergir ou submergir, e em quais condições essas transições acontecem.

Essas transições não são ações independentes escolhidas pelo jogador. São regras internas que o jogo consulta ao selecionar uma unidade, durante o fluxo de uma ação ou após seu compromisso, conforme a operação envolvida.

O sensor determina se a transição é válida e quais condições devem ser aplicadas. O fluxo da ação utiliza essa resposta para realizar a operação no momento apropriado.

## Decolagem e movimento disponível

As condições de decolagem podem permitir **0, 1 ou todos os pontos de movimento**, conforme a unidade e o contexto. O resultado indica a condição de movimento da decolagem, não uma escolha livre do jogador entre essas três possibilidades.

Exemplos:

- Um caça que pousou em uma pista improvisada por falta de combustível, depois de reabastecido, pode decolar com **1 ponto de movimento**.
- Aeronaves lançadas de um porta-aviões podem decolar com **1 ponto de movimento**, pela catapulta.
- Helicópteros podem decolar com **todos os pontos de movimento**, conforme suas regras.

## Emersão, submersão e altitude

As mudanças de domínio e altura também dependem das condições da unidade e da operação em andamento.

Um submarino revelado e emerso após disparar, por exemplo, submerge ao final de duas rodadas, depois de se mover, conforme a regra desse estado. Essa transição é conduzida pelo fluxo interno do jogo.

## Transições durante uma operação

Os sensores auxiliares também são consultados enquanto uma ação é executada. Uma operação pode exigir uma sequência de transições para que seu objetivo seja cumprido.

Um helicóptero pode **pousar, receber o passageiro e decolar novamente no mesmo hex** como parte do embarque. Da mesma forma, um caça pode **pousar, receber rearmamento e decolar no mesmo hex** durante o atendimento.

Essas etapas integram a operação que está sendo realizada; não representam novas ações independentes escolhidas pelo jogador. Cada etapa continua sujeita às condições que permitem sua execução.

## Relação com o compromisso da ação

Consultar um sensor não compromete uma ação. Durante a seleção e a preparação provisória, qualquer apresentação temporária de domínio, altura ou posição precisa ser cancelável e completamente restaurável.

As transições definitivas pertencem à execução comprometida da operação ou ao fluxo automático confirmado correspondente. O fim de uma animação, um pouso visual ou a entrada em um submenu não substituem a confirmação da ação.

FOW, detecção e inteligência continuam sendo atualizados a partir do estado confirmado, respeitando o retorno a Neutral previsto pelo contrato transacional.

Para a IA, esses sensores informam **em quais condições uma operação pode acontecer e como a unidade ficará ao concluí-la**. Isso permite avaliar embarques, atendimentos e deslocamentos considerando as transições necessárias, sem tratar pouso, decolagem ou mudança de camada como ordens independentes.

# Resumo dos fluxos

| Fluxo | Sequência |
|---|---|
| **Ação da unidade** | Unidade selecionada > movimento provisório, inclusive permanecer no mesmo hex > ação escolhida > confirmação. |
| **Destruir / Dispensar** | Unidade selecionada > ação do comando: destruir > confirmação. |
| **Serviços do Comando** | Ação do comando: Serviços do Comando > confirmação > atendimento das unidades elegíveis. Não exige unidade selecionada. |
| **Visão e detecção na tática** | Táticas do jogador > posicionamento e emprego das unidades > enxergar e detectar, conforme suas capacidades e as regras da partida. |

Enxergar e detectar não são comandos independentes: são capacidades que o jogador utiliza por meio de suas decisões táticas. As informações obtidas também orientam suas próximas decisões.

Os sensores auxiliares acompanham esses fluxos quando necessários, validando transições como pouso, decolagem e mudança de camada. Movimento provisório não revela novas informações; visão, detecção e inteligência definitivas respeitam o compromisso da ação e o retorno a Neutral.

## Exemplo: reconhecer antes de atacar

O jogador desconfia que existem inimigos atrás de uma montanha. A última informação disponível não mostrava unidades naquela região, que agora está fora de sua visão atual.

Ele pode enviar um tanque para a área desconhecida, mas **o movimento provisório não revela os hexes nem fornece novos alvos de ataque**. Mesmo depois de terminar a animação de movimento, o tanque continua decidindo com base nas informações confirmadas disponíveis antes dessa ação.

Se houver um inimigo oculto no destino da observação, ele não se torna um alvo disponível apenas porque o tanque se aproximou. Quando não existe outro alvo válido já conhecido, a opção Mirar não aparece. O alcance de movimento pode fornecer indícios — como haver menos casas alcançáveis à frente —, mas esses indícios não equivalem a detectar ou identificar uma unidade e não autorizam um disparo contra ela.

Para reconhecer a região, o jogador pode posicionar o soldado na montanha e escolher **Confirmar Posição**, se permaneceu no hex de origem, ou **Apenas Mover**, se chegou ali por deslocamento. Ao confirmar, a ação do soldado termina. Após o retorno a Neutral, o jogo recalcula visão e detecção a partir da posição confirmada e revela o que o soldado efetivamente consegue observar do outro lado.

Com essa informação confirmada, outras unidades que ainda podem agir passam a considerar os alvos revelados:

- Artilharias com tiro parabólico podem atacar por cima do morro, sem exigir linha de visão direta própria, desde que atendam às demais condições do ataque.
- Outros tanques podem avançar para enfrentar os inimigos agora conhecidos, conforme seus movimentos e ataques válidos.

**Investigar é permitido, mas obter informação nova exige comprometer a ação de reconhecimento.** O tanque que confirmou sua posição já utilizou sua ação; a informação obtida permite coordenar as ações seguintes do restante do exército.

# Quadro geral

| Grupo | Opções |
|---|---|
| **8 ações diretas da unidade** | Reposicionar, Capturar, Embarcar, Desembarcar, Mirar, Fundir, Suprir e Transferir. |
| **2 ações do comando** | Destruir/Dispensar e Serviços do Comando. |
| **2 capacidades exploradas pela tática** | Enxergar e Detectar. |

Os **sensores auxiliares** sustentam esses fluxos — pouso, decolagem, altitude, emersão e submersão — sem acrescentar ações independentes à lista.

São **10 ações escolhidas + 2 capacidades de informação**.

# Glossário

### Domínio/altura

Par que indica onde a unidade está ou opera. Sempre consultado em conjunto.

| Camada vertical | Domínios possíveis |
|---|---|
| ar/alta | ar |
| ar/baixa | ar |
| superfície | terrestre, naval |
| submerso | submarino |

Exemplos: helicóptero em `ar/baixa`; caça em `ar/alta`; navio em `naval/superfície`; submarino em `submarino/submerso`.

### Fundir

Consolida duas unidades adjacentes do mesmo tipo em uma só. HP é o número de membros vivos (máx. 10): os membros somam, limitados a 10. Munição e autonomia são a **média ponderada pelo HP atual** de cada unidade — munição arredonda pra cima, autonomia pra baixo. Membro com pouco HP pesa pouco no resultado. Transportadores com carga não fundem.

Exemplo: 3 membros (5 munição, 10 autonomia) + 7 membros (2 munição, 40 autonomia) ? 10 membros, `(3×5+7×2)/10 = 2,9 ? 3` munição, `(3×10+7×40)/10 = 31` autonomia.

### Visão vs. Detecção

Capacidades distintas.

| | Visão | Detecção |
|---|---|---|
| Revela | Terreno (hexes) | Contatos (unidades) |
| Alcance | Medido em hexes, respeita altura e linha de visão | Canal próprio, pode ser maior que a visão |
| Terreno abaixo | Revelado dentro do alcance | **Não** revelado |

Exemplo: um EWACS com visão 3 e detecção aérea 7 revela o chão em 3 hexes, mas faz um caça furtivo aparecer a até 7 hexes — sobre neblina preta.

### Faixa estratégica

O que está além das faixas tática e operacional da consulta. Não entra no escaneamento local: é longe demais para gastar processamento procurando alvos. Continua relevante para eixos e objetivos de planejamento, não para oportunidades imediatas.
