**Já dá para planejar com segurança.** A versão 9 estabilizou o vocabulário, as leis centrais, os casos-limite e até a relação entre doutrina e implementação. O que falta nos apêndices não impede o planejamento; apenas significa que alguns documentos nascerão inicialmente com seções marcadas como pendentes. 

Eu faria a separação em **três camadas**, não apenas numa pasta cheia de capítulos soltos.

# 1. Documento de autoridade

## `00_fonte_unica_e_indice.md`

Este arquivo seria a porta de entrada e o controlador da documentação.

Deve conter:

* versão atual da documentação;
* definição do que é regra canônica;
* ordem de precedência;
* lista dos documentos;
* breve descrição de cada um;
* glossário mínimo;
* convenção de números;
* lista de pendências conhecidas;
* histórico de alterações de regras.

A regra de autoridade poderia ser:

1. documentos canônicos numerados;
2. catálogos e matrizes;
3. documentos de implementação;
4. relatórios históricos;
5. conversas e anotações antigas.

Assim, quando uma regra mudar, você não precisará perguntar se vale o relatório antigo, a conversa ou o manual.

O manual monolítico atual pode continuar arquivado como:

## `The Map Room Manual tecnico(9) — snapshot.md`

Ele seria a fotografia histórica da versão anterior ao desmembramento, não mais o arquivo editado diariamente.

---

# 2. Documentos canônicos de regras

## `01_principios_e_vocabulario.md`

Conteúdo:

* A Sala de Guerra;
* abstração do tabuleiro;
* determinismo;
* ausência de RNG;
* esquadrão como HP;
* unidade versus arma;
* habilidades como etiquetas;
* classes de unidade;
* classes de arma;
* armadura e potência;
* Elite;
* glossário oficial.

Esse documento responde:

> “Que tipo de jogo é esse e qual vocabulário ele usa?”

Não deve conter fórmulas detalhadas nem tabelas de unidades.

---

## `02_dominios_terrenos_e_ocupacao.md`

Conteúdo:

* cinco camadas;
* terrenos;
* construções versus estruturas;
* prioridade construção ? estrutura/terreno ? terreno;
* domínios aceitos;
* transições de camada;
* três andares do hexágono;
* bloqueio de superfície;
* coexistência de unidades;
* submarino em praia;
* unidade nunca como obstáculo;
* altitude não criando vaga extra.

Esse é o documento estrutural mais importante depois dos princípios.

Ele responde:

> “Onde cada coisa existe e quem pode ocupar ou atravessar cada setor?”

---

## `03_movimento_terreno_e_infraestrutura.md`

Conteúdo:

* custo de planície, praia, floresta e montanha;
* Alpino;
* Guerrilha;
* Fora-de-estrada;
* Motor;
* estrada;
* linha de trem;
* pontes;
* ponte ferroviária;
* estrada na montanha;
* trilho na montanha;
* pista improvisada;
* exceção do Trem de Carga;
* reboque;
* movimento de distância zero.

Ele responde:

> “Como uma unidade percorre o mapa?”

A regra de trem não deveria continuar espalhada entre Habilidades, Estruturas e Combinações. Aqui seria sua declaração canônica.

---

## `04_ciclo_de_acao_e_comprometimento.md`

Conteúdo:

* movimento obrigatório mais ação;
* posicionamento provisório;
* confirmação única;
* cancelamento;
* sensores durante a prévia;
* estado “agiu”;
* restrições de ações;
* apenas mover;
* ações depois de movimento;
* engajar apenas no contato;
* artilharia como decisão de dois turnos;
* ordem operacional recomendada;
* recálculo do mundo após confirmação.

Esse documento responde:

> “Quando uma intenção vira uma alteração real no tabuleiro?”

A regra de Fog of War depende deste documento, mas não deve ser repetida integralmente nos dois.

---

## `05_visao_deteccao_e_nevoa.md`

Conteúdo:

* linha de visão;
* elevação e obstáculos;
* altura 2,25 da montanha;
* Air/Low e Air/High;
* linha de tiro;
* trajetória reta e parabólica;
* observador avançado;
* visão atual versus memória;
* revelação de terreno;
* detecção de unidade;
* construções como reveladoras;
* construções não sendo sensores;
* três estados de conhecimento;
* ocultação;
* sensores especializados;
* Olho;
* exposição de furtivos;
* exposição do submarino;
* informação conhecida versus verdade oculta.

Ele responde:

> “O que o jogador sabe e por que sabe?”

A matriz de sensores deve ficar num arquivo separado, porque é dado de catálogo, não explicação de sistema.

---

## `06_combate.md`

Conteúdo:

* alcance zero;
* alcance 1 e revide;
* ataques unilaterais;
* duelo entre duas unidades;
* simultaneidade;
* DPQ;
* posição;
* bônus de defesa;
* RPS;
* Elite;
* penalidade de ferido;
* força de ataque;
* força de defesa;
* eliminações brutas;
* arredondamento;
* teto do atacante;
* ordem completa da resolução;
* armas válidas conforme domínio;
* movimento e limitação ao contato;
* carga de profundidade.

Ele responde:

> “Como uma troca de fogo produz baixas?”

Esse documento provavelmente será o mais consultado durante testes e balanceamento.

---

## `07_logistica_e_servicos.md`

Conteúdo:

* autonomia;
* consumo por deslocamento;
* consumo aéreo por turno;
* pouso e isenção;
* combustível zerado;
* reservas;
* reabastecimento;
* rearme;
* reparo;
* custos;
* pesos por classe;
* atendimento recebido;
* capacidade do prestador;
* passageiros atendidos;
* construções e estoques;
* Hub e Receiver;
* transferência;
* Serviço do Comando;
* cadeia terrestre, aérea e naval;
* fila de atendimento.

Ele responde:

> “Como uma força continua operacional?”

Aqui devem ficar os valores canônicos de 5%, 10%, 40%, um paciente, dois pacientes e assim por diante — ou referências diretas ao catálogo, caso esses valores sejam configuráveis por ficha.

---

## `08_transporte_fusao_e_operacoes_aereas.md`

Conteúdo:

* vagas especializadas;
* embarque;
* desembarque;
* adoção do domínio do transportador;
* perda de sensores e captura;
* morte junto ao transportador;
* propagação proporcional de dano;
* cadeia de passageiros;
* fusão;
* redistribuição de autonomia e munição;
* pouso;
* decolagem;
* corrida;
* subida direta;
* toque e arremetida;
* permanência no solo;
* transferência deixando aeronave pousada;
* pouso de emergência.

Ele responde:

> “Como unidades mudam de estado, viajam dentro de outras ou se reorganizam?”

Fusão poderia ser um documento próprio, mas ainda não parece grande o suficiente para justificar outro arquivo.

---

## `09_captura_economia_e_progressao.md`

Conteúdo:

* quem captura;
* poder de captura;
* infantaria pesada;
* recuperação de prédio;
* captura do QG;
* renda;
* políticas de mercado;
* dono original;
* primeiro dono;
* mercado livre;
* desativado;
* progressão por conquista;
* memória de conquista;
* cadeia das fábricas;
* pré-requisitos;
* bloqueio da opção de captura;
* produção;
* local de nascimento;
* unidade surgindo na superfície.

Ele responde:

> “Como o mapa se transforma em economia e tecnologia?”

Captura, economia e progressão pertencem ao mesmo arquivo porque uma mecânica alimenta diretamente a outra.

---

## `10_turnos_jornal_e_vitoria.md`

Conteúdo:

* início do turno;
* renda;
* consumo;
* relatórios automáticos;
* Jornal do Comandante;
* níveis de urgência;
* informação limitada;
* diferença entre Jornal e mapa;
* rendição;
* captura do QG;
* eliminação total;
* objetivos de cenário.

Ele responde:

> “Como a partida avança e como termina?”

O Jornal poderia ficar em visão, mas operacionalmente ele pertence ao fluxo do turno.

---

# 3. Catálogos canônicos

Esses arquivos são dados, não capítulos narrativos.

## `11_catalogo_de_construcoes.md`

Uma ficha por construção, sempre com os mesmos campos:

* nome oficial;
* domínio;
* posição;
* bônus de defesa derivado;
* renda;
* visão de terreno;
* detecção;
* pontos de captura;
* política de mercado;
* catálogo;
* serviços;
* estoque;
* capacidade de atendimento;
* pouso;
* decolagem;
* isenção de autonomia;
* pré-requisito;
* comportamento ferroviário;
* regras especiais.

Aqui entram:

* HQ;
* Cidade;
* Fábrica Leve;
* Fábrica;
* Fábrica Pesada;
* Aeroporto;
* Aeroporto Avançado;
* Porto Naval;
* Estação de Trem;
* Hidrobase;
* Logística Naval;
* Barracks;
* Terminal Rodoviário.

---

## `12_catalogo_de_unidades.md`

Uma ficha por unidade:

* nome;
* força;
* classe;
* domínio preferido;
* domínio possível;
* preço;
* HP máximo;
* defesa;
* armadura derivada;
* movimento;
* autonomia;
* consumo;
* habilidades;
* visão;
* sensores;
* ocultação;
* Elite;
* armas;
* vagas;
* serviços;
* capacidade logística;
* pré-requisitos;
* regras particulares.

O catálogo não deve explicar o sistema. Deve apenas declarar os valores.

---

## `13_catalogo_de_armas.md`

Uma ficha por arma:

* nome;
* classe;
* potência;
* classe de potência;
* munição;
* alcance mínimo;
* alcance máximo;
* trajetória;
* domínio do operador;
* domínio dos alvos;
* possibilidade de revide;
* uso após deslocamento;
* regras de exposição;
* observador avançado;
* alcance zero.

Separar arma de unidade é coerente com a própria arquitetura do jogo.

---

## `14_matriz_rps_e_elite.md`

Conteúdo:

* todas as relações entre classes;
* modificadores ofensivos;
* modificadores defensivos;
* filtros de Elite;
* especializações;
* diferenças de nível;
* exemplos de aplicação.

Esse documento será muito melhor em formato tabular do que misturado ao manual narrativo.

---

## `15_matriz_de_sensores.md`

Linhas: sensores ou unidades sensoras.

Colunas:

* Land/Surface;
* Naval/Surface;
* Submarine/Submerged;
* Air/Low;
* Air/High;
* ocultação aérea;
* ocultação submarina;
* alcance;
* exige LoS;
* dispensa LoS;
* pode atuar como observador;
* limitações.

Ele responde imediatamente:

> “Quem detecta o quê?”

---

# 4. Documentos de engenharia

## `90_pendencias_tecnicas.md`

Tudo que hoje está no final do manual:

* isenção indevida sobre o aeroporto;
* caminhão atendendo passageiros pelo Serviço do Comando;
* duas fontes da duração de emersão;
* IA tratando alcance zero como 1;
* peso logístico oculto;
* qualquer divergência futura.

Cada entrada deveria ter:

* regra canônica;
* comportamento atual;
* impacto;
* sistema afetado;
* prioridade;
* estado;
* decisão necessária.

Assim o manual deixa de misturar regra com dívida técnica.

---

## `91_decisoes_de_design.md`

Para registrar mudanças importantes:

* decisão;
* problema original;
* alternativas consideradas;
* regra escolhida;
* consequência desejada;
* versão em que entrou.

Exemplos:

* revide exclusivamente no alcance 1;
* ausência de comando manual de altitude;
* prédio revela terreno, mas não detecta à distância;
* conquista permanece destravada após perder a construção;
* passageiros morrem com o transportador.

Isso impede que você volte daqui a seis meses, ache uma regra estranha e remova justamente a consequência que justificava sua existência.

---

# Dependências entre os documentos

A ordem conceitual seria:

**Princípios**
?
**Domínios e ocupação**
?
**Movimento e ciclo de ação**
?
**Visão e Fog of War**
?
**Combate**
?
**Transporte e logística**
?
**Captura, economia e progressão**
?
**Turno e vitória**

Os catálogos sustentam todos eles, mas não deveriam ser necessários para compreender a ideia geral.

# Regra contra duplicação

Cada regra precisa ter **um único endereço canônico**.

Nos outros arquivos, apenas uma frase curta e uma referência:

> O recálculo da névoa ocorre após a confirmação da ação, conforme `04_ciclo_de_acao_e_comprometimento.md`.

Não repetir três parágrafos em quatro documentos. Caso contrário, o primeiro rebalanceamento recriará versões conflitantes.

As repetições pedagógicas podem continuar no futuro **Manual do Jogador**, porque ali a prioridade é ensinar. Na documentação técnica, a prioridade deve ser localizar e alterar a regra uma única vez.

# O que eu não separaria ainda

Eu não criaria um documento diferente para cada pequena mecânica:

* um arquivo só para Fusão;
* um só para Elite;
* um só para submarino;
* um só para aeroportos;
* um só para Trem de Carga.

Isso pulverizaria demais a documentação e esconderia as relações sistêmicas que tornam o jogo compreensível.

# Ordem prática para realizar o split

1. Criar o índice e a hierarquia de autoridade.
2. Copiar o manual 9 inteiro como snapshot imutável.
3. Extrair os princípios e domínios.
4. Extrair movimento e ciclo de ação.
5. Extrair visão/Fog of War.
6. Extrair combate.
7. Extrair logística e transporte.
8. Extrair captura/economia.
9. Criar os catálogos incompletos com os campos já definidos.
10. Mover as pendências técnicas para o arquivo de engenharia.
11. Revisar duplicações e substituir por referências.
12. Só depois declarar o monólito como legado.

## Minha recomendação final

Planeje agora em **16 arquivos**, mas não tente preencher todos de uma vez.

O primeiro objetivo não é terminar os catálogos. É garantir que cada parágrafo da versão 9 tenha **um endereço definitivo** na nova estrutura.

A versão 9 já oferece estabilidade suficiente para isso. O próximo passo não é escrever mais um “Manual Técnico 10” monolítico. É usar a versão 9 como a última grande constituição indivisa e começar a construir a biblioteca canônica do jogo.
