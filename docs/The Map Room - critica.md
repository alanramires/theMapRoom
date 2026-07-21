



Li o **Manual Técnico (6)** integralmente. Ele tem cerca de 21 mil palavras e já cobre praticamente todo o organismo do jogo: domínios, posição, combate, logística, transporte, aviação, captura, névoa, economia, progressão, turno e vitória. ?filecite?turn2file0?

## Veredito

**Não precisa reescrever do zero.**

Esta é, de longe, a versão mais madura do manual. Ela já pode ser considerada o **documento-mãe do The Map Room**.

Como manual de doutrina e identidade do jogo, eu daria **9/10**.

Como fonte única de verdade técnica, no estado atual, daria **7/10** — não por falta de conteúdo, mas porque algumas regras importantes ainda aparecem:

- contraditórias em pontos diferentes;
- misturadas entre implementação atual e intenção futura;
- explicadas de maneira narrativa, mas sem uma definição normativa final;
- incompletas em relação ao catálogo atual de construções e unidades.

**Eu não dividiria o documento ainda.** Primeiro fecharia as dúvidas abaixo dentro dele. Só depois faria o desmembramento. Dividir agora pode transformar ambiguidades locais em contradições espalhadas por dez arquivos.

---

# O que ficou excepcional

## 1. O domínio virou realmente o alicerce do jogo

A melhor conquista do manual é que domínio deixou de ser uma classificação técnica e virou uma ferramenta mental.

Tanques, aviões, submarinos, armas, terrenos, pouso, trajetória e ocupação podem ser deduzidos a partir da pergunta:

> Em qual domínio isso existe agora?

Isso reduz muito a aparência de “exceções arbitrárias”. O jogador não precisa decorar que um caça pousado pode ser atingido por artilharia; ele deduz isso porque ambos estão em Land/Surface naquele momento. ?filecite?turn2file0?

## 2. O conceito de habilidades como chaves é excelente

A explicação de que Alpino, Guerrilha, Linha de Trem, Pouso em Convés e Ocultação não são poderes independentes, mas **etiquetas consultadas pelo mundo**, é talvez a melhor explicação sistêmica do projeto.

A frase conceitual por trás disso é muito forte:

> O jogo não cresce em regras; cresce em etiquetas.

Isso descreve não apenas o jogo, mas a arquitetura do projeto.

## 3. A separação entre DPQ e bônus de defesa está muito clara

O manual finalmente deixa inequívoco que:

- DPQ determina **como o resultado será arredondado**;
- bônus de defesa determina **quanto o combate custa**;
- posição não é altitude;
- iniciativa não é disparar antes.

Essa seção está didaticamente muito boa e preserva a identidade determinística do combate. ?filecite?turn3file7?

## 4. O combate simultâneo está bem defendido

A repetição de que ninguém atira antes é útil, porque “iniciativa” facilmente seria interpretada como ataque preventivo.

A ideia de que o atacante escolhe o momento e recebe o lado favorável do arredondamento, mas ambos usam o efetivo inicial, está sólida.

## 5. A logística ganhou uma identidade própria

A logística não aparece como um conjunto de botões de cura. Ela aparece como:

- estoque físico;
- capacidade de atendimento;
- dinheiro;
- tempo;
- geografia;
- camadas;
- cadeia de distribuição;
- vulnerabilidade operacional.

A frase “uma unidade sem munição ocupa território, mas não disputa nada” resume perfeitamente o papel da logística.

Também ficou excelente a diferenciação entre:

- receber serviço;
- prestar serviço;
- transferir reserva;
- Serviço do Comando;
- Hub;
- Receiver.

## 6. A progressão por conquista é uma mecânica de identidade

O desbloqueio permanente por “já capturou uma vez” é muito mais interessante do que uma árvore tecnológica abstrata.

A progressão transforma o mapa na árvore tecnológica. Isso combina perfeitamente com o manifesto: o tabuleiro é o protagonista.

A ideia de impedir a captura do prédio avançado e mostrar a opção bloqueada com a explicação do pré-requisito é especialmente boa. Ensina o sistema sem esconder a porta.

## 7. A névoa finalmente está explicada como inteligência

O manual separa corretamente:

- terreno revelado;
- memória do terreno;
- última informação sobre uma construção;
- unidade detectada;
- ocultação;
- aviso de detecção;
- informação do Jornal do Comandante.

O epílogo é excelente. Ele fecha o documento voltando à Sala de Mapas, não ao combate. O jogo termina conceitualmente onde começou: informação incompleta e decisão consciente. ?filecite?turn1file7?

---

# Problemas críticos antes do desmembramento

## 1. Há duas versões incompatíveis do comprometimento da jogada

Na seção sobre movimento dentro da névoa, o texto dá a entender esta sequência:

1. a unidade se desloca;
2. a visão é recalculada;
3. novos contatos aparecem;
4. o jogador escolhe o que fará com a informação recém-descoberta.

Mas, em “Existe Uma Confirmação Só” e no epílogo, a regra apresentada é outra:

1. o jogador experimenta posições;
2. os sensores mostram apenas o que já era conhecido;
3. o jogador escolhe a ação;
4. confirma;
5. somente então o mundo e a névoa são recalculados;
6. a unidade encerra a atividade.

Essas duas regras produzem jogos muito diferentes.

Pelo que você vinha definindo, a segunda é a correta: **o jogador não anda, revela o inimigo e então decide atacar na mesma jogada**. Ele precisa comprometer a atividade inteira e só depois recebe a nova fotografia do mundo.

A seção “Mover É Se Comprometer” precisa ser alinhada com “Existe Uma Confirmação Só”. Esta é a contradição mais importante do documento.

---

## 2. O aeroporto interrompe consumo, mas não está claro como estacionar uma aeronave nele

O manual afirma simultaneamente que:

- aeronave pousada em aeroporto não consome autonomia;
- operações no solo terminam normalmente em arremetida automática;
- aeronaves não “estacionam”;
- não existem comandos livres de altitude;
- pousar e decolar aparecem entre as ações possíveis.

Falta definir o procedimento canônico:

- Como o jogador manda uma aeronave permanecer pousada no aeroporto?
- Pousar no aeroporto é uma ação específica que encerra a atividade?
- Uma aeronave atendida no aeroporto arremete automaticamente ou permanece no hangar?
- Ao ser selecionada no turno seguinte, ela decola automaticamente?
- É possível deixá-la pousada por vários turnos sem selecioná-la?
- Aeroporto comum e avançado se comportam igualmente?

Sem essa resposta, a principal vantagem estratégica do aeroporto — interromper o consumo — fica sem um ciclo operacional completamente explicado.

---

## 3. A visão concedida pelas construções precisa ser confirmada

O manual diz que cidade, fábrica, porto e aeroporto revelam:

- o próprio hexágono;
- o anel imediato ao redor;

e que o QG alcança um hexágono adicional.

Isso diverge da regra que havíamos fechado anteriormente para cidades: **a cidade revelava apenas o próprio hexágono**, permitindo detectar uma unidade inimiga que estivesse ocupando exatamente aquele espaço.

É preciso escolher uma regra definitiva e separar claramente:

- alcance de revelação de terreno;
- alcance de detecção de unidades;
- detecção no próprio hexágono;
- detecção de ocultos;
- memória após perder visão.

Minha dúvida concreta: **as construções abrem somente terreno a distância, mas detectam unidades apenas no próprio hexágono?** O texto parece sugerir algo próximo disso, porém ainda não declara a matriz exata.

---

## 4. O revide é sempre limitado à distância 1?

O manual afirma que o defensor só responde a distância 1.

Isso precisa ser marcado como uma lei absoluta ou corrigido para “quando sua arma de revide alcança o atacante”.

As duas possibilidades têm consequências enormes:

- Um obuseiro atacado de longe nunca revida, mesmo possuindo arma com alcance compatível?
- Dois navios com armas de alcance 2 não trocam fogo quando um ataca a dois hexágonos?
- Um míssil de longo alcance é sempre unilateral?
- Apenas combate adjacente é simultâneo?

Os documentos técnicos anteriores descreviam o revide como dependente de arma, munição, domínio e validade do confronto, sem destacar a distância 1 como lei universal. O Manual 6 precisa encerrar essa questão.

---

## 5. O teto de eliminações mudou e precisa ser validado

O Manual 6 estabelece dois tetos:

- não eliminar mais do que o alvo possui;
- não eliminar mais do que o esquadrão atacante possuía no começo da troca.

Isso produz a regra de que uma unidade com 1 de efetivo nunca elimina mais de 1 inimigo, mesmo com uma arma extremamente poderosa.

A regra é coerente com a metáfora do esquadrão, mas o relatório técnico anterior descrevia principalmente o limite pelo efetivo do alvo. ?filecite?turn0file12?

Como ela altera fortemente o resultado dos confrontos, precisa haver confirmação explícita:

**o limite pelo efetivo de quem atira já é a regra implementada ou é a nova regra de design que ainda será aplicada?**

---

## 6. A montanha possui dois valores de elevação?

O manual diz:

- montanha tem elevação 2;
- quem está nela herda essa elevação;
- uma montanha intermediária bloqueia a visão entre dois outros picos porque, como obstáculo, ela é ligeiramente mais alta do que a posição que concede.

Isso implica que há dois números diferentes:

- elevação recebida pela unidade;
- altura usada pela montanha como obstáculo.

Essa é uma boa regra, mas o documento não informa os valores.

Se ambos fossem 2, pela descrição matemática anterior, a montanha intermediária poderia não bloquear uma linha exatamente nivelada. Portanto, é preciso declarar algo como:

- posição no topo: elevação 2;
- obstáculo montanha: altura de bloqueio 3;

ou explicar a regra específica que produz o bloqueio.

Sem isso, a frase está conceitualmente boa, mas não é reproduzível como fonte técnica.

---

## 7. Há uma contradição interna sobre infantaria e montanha

Primeiro o manual afirma que toda a infantaria do exército possui Alpino.

Depois afirma que “infantaria comum” para no sopé da montanha junto com blindados, artilharia e caminhões.

Se toda a infantaria disponível é Alpina, “infantaria comum” não existe nesse roster. A frase deve virar algo como:

- infantaria sem Alpino não entra;
- no exército atual, todas as infantarias possuem Alpino.

---

## 8. Há uma contradição aparente sobre trem, floresta e montanha

Em “O Custo do Terreno”, o Trem de Carga é descrito como bloqueado completamente em floresta e montanha, sem exceção.

Depois, o manual explica que:

- o trem cruza floresta quando há trilho;
- o trem cruza montanha quando há trilho, pagando 2.

A regra real parece ser:

> O Trem de Carga é bloqueado pelo terreno puro; ele só atravessa esses terrenos quando existe uma rota ferroviária válida.

A expressão “bloqueado por completo, sem exceção” precisa incluir “na ausência de trilho”, porque hoje ela contradiz as seções seguintes.

---

## 9. Classe de armadura é teto de arma ou apenas classificação?

O manual declara que uma unidade leve não carrega armas médias ou pesadas.

Isso soa como uma restrição absoluta do sistema.

Porém, versões anteriores do catálogo continham combinações como plataformas leves ou médias utilizando armas classificadas acima de sua armadura. Se essas combinações continuam existindo, então a classe de armadura não é um teto obrigatório: é apenas uma faixa descritiva ou uma convenção de balanceamento.

É necessário decidir entre:

- **regra obrigatória:** nenhuma unidade opera arma de potência superior à própria classe;
- **diretriz de balanceamento:** normalmente não ocorre, mas unidades especializadas podem violar;
- **conceitos independentes:** armadura e potência não restringem uma à outra.

---

## 10. Os percentuais logísticos precisam de uma única versão

O Manual 6 informa:

- reabastecimento: até 5%;
- rearmamento: até 10%;
- reparo: até 40%.

O relatório técnico anterior registrava valores diferentes:

- reabastecimento: 10%;
- rearmamento: 25%;
- reparo: 65%. ?filecite?turn0file11?

Como este será o documento-mãe, precisa ficar explícito:

- quais são os valores atuais;
- se são valores padrão ou definidos por cenário;
- se o Manual 6 já representa um rebalanceamento ainda não aplicado.

Hoje não dá para saber se o manual está descrevendo o jogo ou prescrevendo a próxima alteração.

---

## 11. Quantos pacientes o caminhão atende?

O Manual 6 diz que cada prestador atende uma unidade, com duas exceções:

- avião-tanque;
- porta-aviões.

?filecite?turn3file10?

Entretanto, o catálogo anterior do Caminhão de Suprimentos dizia que ele atendia até duas unidades por turno. ?filecite?turn0file15?

É preciso confirmar se:

- o caminhão foi reduzido para um atendimento;
- continua atendendo dois;
- a quantidade depende do serviço ou da configuração individual do prestador.

---

## 12. “Uma rodada” e “duas rodadas” precisam de definição formal

Para o caça furtivo, o texto diz que a exposição dura uma rodada e termina no próximo turno dele.

Para o submarino, diz que permanece exposto por duas rodadas.

Em uma partida com mais de dois times, “rodada” pode significar:

- dois turnos do proprietário;
- dois turnos globais;
- duas passagens completas por todos os times;
- dois inícios de turno da unidade;
- dois turnos adversários.

A definição deve ser feita em termos inequívocos, preferencialmente a partir do turno do proprietário da unidade.

---

## 13. O Jornal sabe que uma construção foi tomada, mas a fotografia continua velha?

A seção da névoa afirma que uma construção fora de visão permanece no mapa com o último dono observado.

Depois, o Jornal afirma que, quando uma construção sua é tomada, ele informa inclusive o novo dono, pois a guarnição soube quem entrou.

Minha dúvida:

- o Jornal atualiza também a cor da construção no mapa?
- ou o mapa continua mostrando a fotografia velha, apesar de o jogador ter recebido o relatório?
- uma construção que era sua continua sendo considerada “observada” durante o processo de captura?
- a propriedade sempre comunica sua própria perda, mesmo sem unidade aliada por perto?

A regra de conhecimento está ótima, mas falta definir como essa informação se propaga para a representação visual.

---

## 14. A compra de submarinos precisa de uma camada inicial

O manual diz que a unidade nasce na construção produtora e que a superfície precisa estar livre.

Isso explica bem:

- unidades terrestres;
- navios;
- aeronaves, que nascem pousadas.

Mas o submarino nasce:

- em Naval/Surface;
- já em Submarine/Submerged;
- na camada preferida;
- dependendo do porto e da profundidade local?

Como a camada inicial afeta ocupação, detecção e ocultação, ela precisa ser declarada.

---

## 15. O destino dos passageiros deve ser dito sem metáfora

O texto afirma que passageiros compartilham o destino do transportador.

A interpretação natural é que todos são eliminados quando o transportador é destruído. Porém, por ser uma fonte técnica, precisa declarar diretamente:

- todos os passageiros são eliminados;
- não existe teste de sobrevivência;
- não existe desembarque de emergência;
- reservas e cargas embarcadas também são perdidas;
- unidades transportadas contam para evitar eliminação total do time.

A última parte já aparece na condição de derrota, mas a resolução da destruição do transportador ainda está implícita.

---

# Lacunas de cobertura como fonte única de verdade

## Catálogo de construções

O manual descreve muito bem cidade, fábrica, porto, aeroporto, QG e prédios da progressão, mas o jogo atual possui ou está definindo também:

- Fábrica Leve;
- Fábrica Média;
- Fábrica Pesada;
- Estação de Trem;
- Barracks;
- Aeroporto Avançado;
- Terminal Rodoviário;
- Docas;
- Hidrobase.

Como fonte única, o manual precisa registrar para cada uma:

- domínios aceitos;
- posição e defesa;
- renda;
- mercado;
- unidades produzidas;
- serviços oferecidos;
- capacidade de atendimento;
- estoque;
- regras de captura;
- pré-requisitos;
- regras de pouso;
- comportamento do trem;
- visão e detecção.

A Hidrobase, por exemplo, tem uma identidade importante: aceita o pouso de qualquer aeronave para manutenção, mas comercializa somente hidroaviões. Isso ainda não aparece no documento.

## Catálogo exato de unidades e armas

O manual ensina o sistema muito bem, mas ainda não é suficiente para reconstruir as fichas atuais.

Como fonte única, precisará conter ou anexar:

- atributos de cada unidade;
- classe;
- domínio preferido;
- custos;
- movimento;
- autonomia;
- consumo;
- defesa;
- elite;
- armas;
- alcance;
- munição;
- trajetória;
- alvos;
- transportes;
- serviços;
- sensores;
- ocultações.

Não precisa interromper a narrativa com dezenas de tabelas. Isso pode ficar em apêndices canônicos.

## Matriz de detecção

Falta uma matriz clara dizendo quem detecta:

- caça furtivo;
- submarino;
- unidades comuns em cada camada;
- quais sensores ignoram relevo;
- quais detectam apenas terreno;
- quais detectam unidades;
- por quanto tempo a revelação persiste;
- se a revelação pertence ao time detector ou a todos.

A explicação conceitual está ótima; falta o catálogo operacional.

## Valores globais versus valores de cenário

A renda aparece como “cerca de” 3.000, 1.500 e 1.000. Isso é bom para ensinar, mas não para uma fonte técnica.

O documento precisa identificar cada número como:

- valor global fixo;
- valor padrão configurável;
- exemplo do cenário atual;
- estimativa narrativa.

O mesmo vale para renda, estoques, capacidade, pontos de captura, alcance de visão e catálogo de produção.

---

# Problemas editoriais que afetam a regra

## 1. O arquivo está com problemas de codificação

Em alguns trechos, os valores negativos aparecem como:

- `?1`;
- `?2`.

Isso ocorre justamente nas tabelas de DPQ e defesa, onde o sinal muda completamente a regra. Em outras versões, os mesmos valores aparecem corretamente como ?1 e ?2. ?filecite?turn3file12?turn3file18?

O arquivo também está em uma codificação antiga, não em UTF-8. Antes de desmembrar, isso precisa ser corrigido, porque o erro será replicado para todos os documentos filhos.

## 2. O arquivo não está realmente estruturado como Markdown

Apesar da extensão, os títulos não possuem uma hierarquia consistente de cabeçalhos. As tabelas também foram achatadas em várias partes.

Isso prejudica:

- índice automático;
- navegação;
- links internos;
- divisão futura;
- comparação de versões;
- busca por capítulo.

## 3. “Fábrica” está ambígua

Na progressão aparecem:

- Fábrica Leve;
- Fábrica;
- Fábrica Pesada.

Mas o vocabulário que você vinha adotando é:

- Fábrica Leve;
- Fábrica Média;
- Fábrica Pesada.

“Fábrica” também é usada genericamente ao longo do manual para qualquer instalação industrial. O degrau intermediário precisa ser sempre chamado de **Fábrica Média**, caso esse seja o nome oficial.

## 4. Regras e justificativas estão misturadas

Isso dá personalidade ao texto, mas dificulta a consulta técnica.

O formato ideal de cada assunto seria:

**Regra**  
O que sempre acontece.

**Procedimento**  
Em qual ordem acontece.

**Exceções**  
Quando não acontece.

**Exemplo**  
Uma situação concreta.

**Razão de design**  
Por que a regra existe.

Hoje essas cinco coisas frequentemente aparecem dentro do mesmo fluxo narrativo.

---

# Minha avaliação da estrutura futura

Quando chegar a hora de dividir, eu usaria aproximadamente esta arquitetura:

1. **Princípios e glossário**  
   Determinismo, escala do hexágono, esquadrão, domínio, posição, ação e informação.

2. **Domínios, camadas e ocupação**  
   Terrenos, construções, estruturas, três andares, prioridade e coexistência.

3. **Movimento e terreno**  
   Custos, habilidades, estradas, trilhos, pontes, reboque e bloqueio.

4. **Visão, detecção e Fog of War**  
   Elevação, LoS, memória, ocultação, sensores, Olho e Jornal.

5. **Combate**  
   Armas, alcance, trajetória, revide, RPS, elite, DPQ, feridos e eliminações.

6. **Logística**  
   Reservas, serviços, dinheiro, capacidade, tiers, transferências e Serviço do Comando.

7. **Transportes e operações aéreas**  
   Vagas, embarque, passageiros, pouso, decolagem, autonomia e emergência.

8. **Captura, economia e progressão**  
   Pontos de captura, renda, mercado, pré-requisitos e produção.

9. **Turno, ações e vitória**  
   Confirmação, sensores, estados da unidade, ordem operacional e derrotas.

10. **Catálogos canônicos**  
    Unidades, armas, construções, estruturas, sensores, serviços e matrizes.

Mas o documento-mãe deve continuar existindo. Os arquivos menores não devem virar autoridades independentes. Eles devem declarar algo como:

> Derivado do Manual Técnico, versão X. Em caso de divergência, prevalece o documento-mãe.

---

# Ordem que eu seguiria agora

Antes de qualquer revisão de português ou divisão:

1. resolver a sequência de movimento, confirmação e recálculo da névoa;
2. resolver estacionamento e arremetida de aeronaves;
3. fechar visão e detecção das construções;
4. confirmar revide a distância;
5. confirmar os dois tetos de eliminações;
6. confirmar percentuais e capacidades logísticas;
7. definir duração exata da exposição stealth;
8. revisar trem, montanha e elevação;
9. completar o catálogo de construções;
10. converter o arquivo para UTF-8 e Markdown estruturado.

## Conclusão

O Manual Técnico (6) já encontrou a **linguagem definitiva do The Map Room**.

Ele consegue explicar sistemas complexos sem parecer uma documentação de engenharia e, ao mesmo tempo, apresenta razões táticas suficientes para que as regras não pareçam caprichos. Domínio, posição, etiquetas, logística e inteligência formam agora uma doutrina única.

O que falta não é criatividade nem uma grande reescrita. Falta **congelar algumas decisões e remover ambiguidades normativas**.

Depois disso, ele estará pronto para ser desmembrado sem perder a alma — e, mais importante, sem criar várias verdades concorrentes.

