# Hotzones e Bandas de Alcance

> **Documento de apresentação e avaliação — não é a norma.**
>
> A especificação oficial é `docs/contrato_envelope_alcance.md`. Onde os dois
> divergirem, o contrato vence. Este texto existe para explicar o conceito a
> quem chega de fora; o contrato existe para ser obedecido pelo código.
>
> O resumo de bandas, a definição de MP e Range, e a lista do que falta
> implementar vivem **no contrato**, e são a versão a consultar antes de mexer
> em código.

A Hotzone é a área relevante para uma determinada intenção da unidade.

Ela não representa apenas “até onde a unidade chega”. Representa o espaço no qual ela consegue produzir um efeito útil, considerando o que pretende fazer.

A mesma unidade pode possuir Hotzones diferentes para:

* movimentar;
* capturar;
* combater;
* fundir;
* embarcar;
* desembarcar;
* prestar serviços;
* transferir recursos;
* operar estoques.

A Hotzone pertence à combinação:

**unidade + intenção + modalidade + estado atual + geografia**

Por isso não existe uma fórmula universal que sirva para todas as decisões da IA.

---

## Distância Aérea e Caminhos Válidos

A forma de medir a Hotzone depende da unidade e da intenção.

### Distância Aérea

A Distância Aérea considera apenas a separação geométrica entre dois hexágonos.

Na implementação, ela utiliza a geometria cúbica da malha hexagonal.

Ela ignora:

* custos de terreno;
* montanhas;
* estradas;
* obstáculos;
* permissões de travessia;
* existência de uma rota realmente percorrível.

Responde apenas:

> Quantos hexágonos separam o ponto A do ponto B?

Essa geometria é utilizada principalmente por:

* unidades aéreas;
* perfis artilheiros;
* intenções cuja influência não depende da rota terrestre entre origem e destino.

### Caminhos Válidos

A distância por Caminhos Válidos considera a rota que a unidade realmente consegue percorrer.

Ela depende de:

* domínio;
* terreno;
* estruturas;
* construções;
* habilidades;
* custos de entrada;
* bloqueios;
* Pontos de Movimento disponíveis.

Dois setores podem estar a três hexágonos de Distância Aérea, mas exigir seis Pontos de Movimento por Caminhos Válidos.

Também podem estar próximos geometricamente e serem completamente inacessíveis para determinada unidade.

A escolha entre Distância Aérea e Caminhos Válidos é feita pelo perfil da intenção.

---

# MP e Range

## MP — Pontos de Movimento Restantes

Nas Hotzones táticas, MP significa os **Pontos de Movimento restantes da unidade**, e não necessariamente o seu valor máximo de movimento.

A implementação consulta `RemainingMovementPoints`.

Quando esse valor já está zerado, o sistema utiliza o movimento máximo como fallback, conforme a decisão estabelecida na Fase 0.

Essa escolha possui uma consequência importante:

> As bandas podem encolher conforme a unidade consome movimento durante a rodada.

Uma unidade com movimento máximo 4, mas apenas 2 pontos restantes, projeta sua Hotzone tática a partir desses 2 pontos disponíveis.

## Range — Alcance do Efeito

Range representa o alcance do efeito produzido pela intenção.

Pode ser:

* alcance de uma arma;
* alcance de um serviço;
* alcance de coleta;
* alcance de transferência;
* alcance operacional de um estoque;
* outra forma de atuação à distância.

Nem todo Range é um número simples.

Serviços e coletas podem utilizar modos de alcance, como:

* `SameHexOrEmbarked`;
* `Adjacent1Hex`;
* `Hybrid0Or1Hex`.

Nesses casos, não se deve calcular literalmente:

**MP + Range**

O procedimento correto é:

1. calcular a área alcançável pelo movimento;
2. expandir essa área conforme o modo de serviço ou coleta.

---

# Bandas da Hotzone

As Hotzones possuem duas gradações principais.

## Banda Tática

A Banda Tática representa o espaço no qual a unidade pode produzir a intenção durante a rodada atual.

Ela utiliza o estado atual da unidade, incluindo seus Pontos de Movimento restantes.

## Banda Operacional

A Banda Operacional representa a segunda janela de atuação.

Ela projeta uma nova área a partir da primeira banda, simulando uma busca encadeada.

Dependendo da intenção, essa segunda banda pode representar:

* uma nova rodada de movimento;
* uma área de preservação;
* uma área de influência;
* uma posição a partir da qual a unidade poderá atuar depois.

A Banda Operacional não é necessariamente a Banda Tática multiplicada por dois.

Cada intenção define sua própria regra.

---

# Hotzone de Movimento

A Hotzone de Movimento representa até onde a unidade consegue se deslocar.

| Banda       | Cálculo |
| ----------- | ------- |
| Tática      | MP      |
| Operacional | MP + MP |

A Banda Tática utiliza os Pontos de Movimento restantes.

A Banda Operacional é produzida por uma busca encadeada: uma primeira área de movimento seguida por uma segunda área de movimento.

A geografia é considerada por Caminhos Válidos, exceto para unidades cujo perfil determine geometria cúbica.

Essa é a Hotzone básica para a maioria das decisões relacionadas a deslocamento.

---

# Hotzone de Captura

A Hotzone de Captura utiliza a mesma projeção da Hotzone de Movimento.

| Banda       | Cálculo |
| ----------- | ------- |
| Tática      | MP      |
| Operacional | MP + MP |

No código, Captura e Movimento caem no mesmo ramo de construção do perfil.

A captura não acrescenta alcance próprio porque a unidade precisa alcançar fisicamente a construção.

Portanto:

**Hotzone de Captura = Hotzone de Movimento**

---

# Hotzone de Combate

A Hotzone de Combate depende da modalidade de emprego escolhida.

Existem três modalidades:

* Combatente;
* Artilheiro;
* Híbrido.

## Combatente

O Combatente avança e utiliza uma arma ou outro efeito de curto alcance.

| Banda       | Cálculo    |
| ----------- | ---------- |
| Tática      | MP + Range |
| Operacional | MP + MP    |

A Banda Tática combina:

1. o deslocamento disponível;
2. o alcance da arma a partir da posição alcançada.

Um Soldado com:

* MP 3;
* rifle de Range 1;

possui:

* Hotzone Tática de Combate 4;
* Hotzone Operacional de Combate 6.

A Banda Operacional não acrescenta o alcance da arma.

Ela representa apenas duas janelas de aproximação por movimento. A projeção da arma pertence à banda tática, não à banda operacional.

Em termos visuais:

* a banda de arma aparece como alcance ofensivo;
* a banda operacional permanece uma área de aproximação e posicionamento.

## Artilheiro

O Artilheiro controla uma área a partir da posição em que já se encontra.

| Banda       | Cálculo                     |
| ----------- | --------------------------- |
| Tática      | 0 até o Range Máximo        |
| Operacional | Range Máximo + Range Máximo |

A Hotzone do Artilheiro utiliza Distância Aérea por geometria cúbica.

Uma Artilharia de Campanha com:

* MP 1;
* alcance mínimo 3;
* alcance máximo 4;

possui:

* Hotzone Tática 4;
* Hotzone Operacional 8.

A banda é representada de 0 até o alcance máximo porque ela descreve a área de influência e preservação da unidade.

Isso não elimina o alcance mínimo da arma.

A artilharia do exemplo continua atacando apenas entre as distâncias 3 e 4. Os setores entre 0 e 2 fazem parte da leitura da Hotzone, mas não são alvos válidos para aquela arma.

A Banda Operacional do Artilheiro é uma área de preservação:

> Enquanto houver inimigos dentro dela, a unidade ainda está operacionalmente comprometida com aquela frente.

Por isso, o Artilheiro não deve abandonar sua posição apenas porque nenhum alvo está atualmente dentro do alcance de tiro.

Um inimigo a seis ou sete hexágonos ainda está dentro de sua área operacional e pode entrar na área tática rapidamente.

## Híbrido

Uma unidade híbrida pode consultar o perfil Combatente ou Artilheiro conforme sua modalidade atual.

> **A modalidade não é livre para todos.** Combate + Combatente exige arma de
> alcance **mínimo 1**: o tiro pós-movimento colapsa para 1, então uma peça de
> mínimo 2 não dispara depois de andar. Pedir o perfil Combatente para ela é
> pedido inválido e devolve nulo — a pergunta certa é Artilheiro.
>
> Por isso o Submarino (torpedo 1~3) é híbrido de verdade, e a Artilharia de
> Campanha (alcance 3~4) **não é**: ela só tem o perfil Artilheiro.

Um Submarino com:

* MP 4;
* torpedos de alcance 1 a 3;

pode operar como Combatente quando pretende avançar e atacar.

Também pode operar como Artilheiro quando pretende preservar posição, controlar uma passagem ou aguardar a aproximação de alvos.

A intenção da IA decide qual perfil será utilizado.

---

# Hotzone de Fusão

A Hotzone de Fusão verifica se uma unidade consegue alcançar outra unidade compatível e realizar a fusão na rodada atual.

| Banda       | Cálculo                                         |
| ----------- | ----------------------------------------------- |
| Tática      | MP menos o custo de entrada no terreno da fusão |
| Operacional | não possui                                      |

O sistema precisa reservar o custo de entrada no hexágono onde a fusão será concluída.

Essa lógica é executada pela expansão por custo de entrada e validada pelo sensor de fusão.

A Fusão não deve possuir uma Banda Operacional própria.

Quando as unidades estão distantes demais para fundir nesta rodada, a decisão deixa de ser uma intenção imediata de Fusão e passa a ser uma decisão de Movimento.

Normalmente, esse movimento deve ocorrer em direção à Retaguarda.

Portanto:

> A Fusão existe no Tático. A preparação para fundir utiliza a Hotzone de Movimento.

### Estado atual da implementação

A doutrina determina que Fusão não possui Banda Operacional.

Entretanto, o construtor atual de perfis sempre devolve as duas bandas.

Essa divergência ainda precisa ser corrigida no código.

---

# Hotzone de Embarque

A Hotzone de Embarque verifica se o passageiro consegue alcançar um transportador e embarcar.

| Banda       | Cálculo                                          |
| ----------- | ------------------------------------------------ |
| Tática      | MP menos o custo de entrada no local de embarque |
| Operacional | MP + MP                                          |

Assim como na Fusão, o movimento precisa conservar os Pontos de Movimento necessários para entrar no terreno ou célula em que o embarque ocorrerá.

A validação final continua pertencendo ao sensor de embarque.

## Quando aceitar carona

Unidades comuns podem rejeitar transporte quando conseguem alcançar o objetivo utilizando sua própria Hotzone Operacional.

Para essas unidades:

* objetivo dentro da Hotzone Operacional: seguir por meios próprios;
* objetivo além da Hotzone Operacional: procurar transporte.

Unidades de assalto, especialmente blindados, podem aceitar transporte mesmo quando o objetivo está dentro da Banda Operacional.

Elas aceitam carona quando o destino está:

* dentro da Banda Operacional;
* no limite da Banda Operacional;
* além da Banda Operacional.

Isso preserva combustível e permite que unidades de baixa autonomia cheguem ao combate em melhores condições.

---

# Hotzone de Desembarque

A Hotzone de Desembarque é projetada de maneira invertida.

Ela não começa no passageiro ou no transportador.

Ela começa no objetivo.

A pergunta é:

> Em quais setores o passageiro pode ser desembarcado para alcançar o objetivo depois?

## Banda Tática

A Banda Tática é formada pela área de movimento do passageiro projetada ao redor do objetivo.

| Banda       | Cálculo                                         |
| ----------- | ----------------------------------------------- |
| Tática      | MP do passageiro projetado ao redor do objetivo |
| Operacional | MP + MP do passageiro                           |

O desembarque é simbolicamente tratado como um transporte direto até o ponto escolhido.

Depois disso, verifica-se quanto o passageiro precisará se deslocar para alcançar o objetivo.

Um helicóptero transportando um Soldado de MP 3 pode desembarcá-lo:

* diretamente no objetivo;
* a um hexágono;
* a dois hexágonos;
* a até três hexágonos do objetivo.

Em qualquer um desses pontos, o Soldado consegue alcançar o destino em sua próxima rodada.

Um Bazooka de MP 2 projeta uma área de desembarque de dois hexágonos ao redor do objetivo.

## Voadores e acesso ao destino

Transportadores voadores podem alcançar pontos de desembarque independentemente dos caminhos terrestres tradicionais, desde que o hexágono aceite a operação.

Para outras modalidades de transporte, deve existir pelo menos um caminho ou ponto de entrada válido.

Pode acontecer de o objetivo estar cercado por montanhas ou isolado de tal maneira que o passageiro não consiga alcançá-lo normalmente.

Nesse caso:

* o desembarque ainda pode aproximar a unidade;
* o destino deixa de ser Tático;
* a operação passa a ser classificada como Operacional;
* a unidade poderá precisar de rodadas adicionais para concluir o percurso.

### Estado atual da implementação

A intenção de Desembarque ainda não existe no sistema de alcance.

`ReachIntent.Disembark` ainda precisa ser criado.

Essa ausência bloqueia a migração completa da lógica de desembarque para o novo sistema de Hotzones.

---

# Hotzone de Logística

A Hotzone de Logística representa o espaço no qual uma unidade consegue se deslocar e prestar um serviço em campo.

| Banda       | Cálculo                                   |
| ----------- | ----------------------------------------- |
| Tática      | área de MP expandida pelo modo de serviço |
| Operacional | MP + MP                                   |

Não se deve somar MP a um Range numérico de maneira automática.

Primeiro é calculada a área de movimento.

Depois ela é expandida conforme o modo de serviço:

### `SameHexOrEmbarked`

O serviço ocorre:

* no mesmo hexágono;
* ou sobre unidades embarcadas quando a regra do serviço permitir.

Não existe expansão para um hexágono adjacente.

### `Adjacent1Hex`

A área de movimento é expandida em um hexágono.

A unidade pode prestar o serviço a um alvo adjacente à posição alcançada.

### `Hybrid0Or1Hex`

O serviço pode ocorrer:

* no mesmo hexágono;
* ou a um hexágono de distância.

A expansão é determinada pelo modo, e não por uma soma numérica genérica.

## A arma logística

O alcance do serviço deve ser visualmente exposto como uma banda própria.

Da mesma forma que o combate mostra o alcance da arma, a logística deve mostrar em vermelho o alcance no qual o serviço pode ser prestado.

Atualmente, as células de serviço entram em `ActionCells` sem uma distinção visual própria.

Essa separação ainda precisa ser implementada.

---

# Hotzone de Transferência

A Hotzone de Transferência representa a área na qual uma unidade consegue alcançar uma fonte ou destino e transferir recursos.

| Banda       | Cálculo                                  |
| ----------- | ---------------------------------------- |
| Tática      | área de MP expandida pelo modo de coleta |
| Operacional | MP + MP                                  |

A lógica é semelhante à Hotzone de Logística.

O sistema:

1. calcula a área de movimento;
2. consulta o `collectionRange`;
3. expande a área conforme o modo de coleta.

O alcance de coleta não deve ser tratado obrigatoriamente como um número simples.

Ele pode representar:

* mesma célula;
* célula adjacente;
* comportamento híbrido;
* outra regra declarada pela ficha.

---

# Hotzone de Estoque

Estoque deve existir como uma intenção própria.

Ele representa a capacidade de uma fonte logística projetar ou receber caixas, munições, combustível, peças ou outros recursos dentro de sua área operacional.

Essa intenção funciona como uma espécie de arma logística:

> Uma arma de caixas em vez de uma arma de combate.

Sua projeção deve utilizar o `operationalRange` declarado pela fonte de estoque.

Assim como Suprir, a intenção de Estoque deve expor seu alcance em vermelho, distinguindo:

* onde a unidade ou construção está;
* onde o estoque consegue produzir efeito.

### Estado atual da implementação

A intenção de Estoque ainda não existe no sistema de alcance.

Ela precisa ser criada como um perfil separado, com sua própria banda de efeito e seu `operationalRange`.

---

# Resumo das Hotzones

| Intenção             | Banda Tática                                     | Banda Operacional | Geometria principal             |
| -------------------- | ------------------------------------------------ | ----------------- | ------------------------------- |
| Movimento            | MP                                               | MP + MP           | Caminhos Válidos                |
| Captura              | MP                                               | MP + MP           | Caminhos Válidos                |
| Combate — Combatente | MP + Range                                       | MP + MP           | Conforme a unidade              |
| Combate — Artilheiro | 0 até Range Máximo                               | 2 × Range Máximo  | Distância Aérea                 |
| Fusão                | MP menos custo de entrada                        | não possui        | Caminhos Válidos                |
| Embarque             | MP menos custo de entrada                        | MP + MP           | Caminhos Válidos                |
| Desembarque          | MP do passageiro projetado do objetivo para fora | MP + MP           | Invertida conforme o passageiro |
| Logística            | MP expandido pelo modo de serviço                | MP + MP           | Caminhos Válidos                |
| Transferência        | MP expandido pelo modo de coleta                 | MP + MP           | Caminhos Válidos                |
| Estoque              | conforme `operationalRange`                      | conforme ficha    | Conforme a fonte                |

---

# Lacunas Atuais da Implementação

A doutrina já está definida, mas o sistema ainda possui quatro lacunas principais.

## Fusão ainda recebe Banda Operacional

O construtor de perfis cria automaticamente duas bandas, embora Fusão deva possuir apenas a Banda Tática.

## Alcance logístico ainda não possui representação própria

Suprir e Estoque devem exibir a área de efeito em vermelho, como uma arma logística.

Atualmente, esse alcance entra nas células de ação sem distinção visual.

## Estoque ainda não existe como intenção

A intenção precisa utilizar o `operationalRange` da fonte logística.

## Desembarque ainda não existe como intenção

A projeção invertida do passageiro ao redor do objetivo ainda precisa ser incorporada ao sistema.

---

# Princípio Final

A IA não deve perguntar apenas:

> Qual é a distância até o objetivo?

Ela deve perguntar:

> Qual é a intenção desta unidade, qual modalidade ela está utilizando e em qual banda o objetivo se encontra?

A resposta pode mudar completamente conforme a intenção.

Um alvo pode estar:

* dentro da Hotzone de Movimento;
* fora da Hotzone de Captura;
* dentro da Hotzone Tática de Combatente;
* dentro apenas da Hotzone Operacional de Artilheiro;
* acessível por Distância Aérea;
* inacessível por Caminhos Válidos;
* próximo o bastante para desembarque;
* distante demais para seguir sem transporte.

A Hotzone transforma distância em linguagem operacional.

Ela permite que Movimento, Combate, Logística, Transporte e IA utilizem o mesmo vocabulário sem fingir que todas essas intenções medem o espaço da mesma maneira.
