



## Veredito

**Sim: os 19 arquivos satisfazem a separação planejada.** A biblioteca cobre todos os grandes sistemas do Manual 9, tem ordem de precedência, distingue doutrina, dados, dívida técnica, decisões e auditoria, e não precisa ganhar novos documentos neste momento.

Mas eu classificaria o estado atual como:

- **Arquitetura da separação:** 9/10  
- **Distribuição temática:** 8/10  
- **Ausência de duplicação:** 7/10  
- **Completude da fonte única:** 6,5/10  
- **Pronta para substituir o monólito:** sim, como biblioteca em construção  
- **Pronta para dispensar consulta aos assets:** ainda não  

Uma pequena precisão: atualmente são **19 arquivos no total**, não 19 capítulos mais apêndices:

- 1 índice;
- 10 documentos canônicos;
- 5 catálogos e matrizes;
- 3 documentos de engenharia.

O próprio índice formaliza corretamente essa estrutura e declara que os documentos `01` a `10` são a doutrina, enquanto os catálogos ainda incompletos recorrem aos assets. ?filecite?turn6file0?

# Avaliação dos 19 arquivos

| Arquivo | Veredito | Observação |
|---|---|---|
| `00_fonte_unica_e_indice.md` | **Excelente** | Resolve autoridade, precedência, vocabulário e ordem de leitura. |
| `01_principios_e_vocabulario.md` | **Ajustar** | Cumpre a função, mas o epílogo sobre névoa e comprometimento está fora de seu escopo. |
| `02_dominios_terrenos_e_ocupacao.md` | **Bom** | Estruturalmente correto, mas ainda carrega detalhes de movimento que pertencem ao `03`. |
| `03_movimento_terreno_e_infraestrutura.md` | **Excelente** | Tem fronteira clara e reúne corretamente terreno, estrada, trilho, habilidades e reboque. |
| `04_ciclo_de_acao_e_comprometimento.md` | **Excelente com ajuste** | Deve possuir o uso da informação pela interface, mas não redefinir toda a doutrina da névoa. |
| `05_visao_deteccao_e_nevoa.md` | **Excelente com correção** | É a autoridade natural sobre informação, mas ainda tem uma frase problemática sobre Air/High. |
| `06_combate.md` | **Excelente** | Tem escopo suficiente e funciona como autoridade sobre a resolução de baixas. |
| `07_logistica_e_servicos.md` | **Excelente com ajuste** | Deve perder parte das explicações operacionais de pouso e decolagem para o `08`. |
| `08_transporte_fusao_e_operacoes_aereas.md` | **Bom** | Os três assuntos cabem juntos porque tratam de mudança de estado, embora seja o documento mais pesado. |
| `09_captura_economia_e_progressao.md` | **Excelente** | As três mecânicas alimentam uma à outra e pertencem ao mesmo endereço. |
| `10_turnos_jornal_e_vitoria.md` | **Incompleto no escopo** | Jornal e vitória estão bons, mas ainda falta o fluxo macro do turno prometido pelo título. |
| `11_catalogo_de_construcoes.md` | **Estrutura correta** | Tem resumo útil, mas ainda não é um catálogo completo. |
| `12_catalogo_de_unidades.md` | **Esqueleto** | Ainda está praticamente vazio e conserva um título interno que menciona armas. |
| `13_catalogo_de_armas.md` | **Estrutura correta** | O esquema está bom, mas precisa distinguir atributos declarados de comportamentos derivados. |
| `14_matriz_rps_e_elite.md` | **Esqueleto** | A divisão é correta, mas sem os valores ainda não cumpre sua função de consulta. |
| `15_matriz_de_sensores.md` | **Esqueleto** | É exatamente o apêndice necessário, mas ainda precisa receber a matriz real. |
| `90_pendencias_tecnicas.md` | **Muito bom** | Separa corretamente bug e doutrina; precisa apenas de formato mais operacional. |
| `91_decisoes_de_design.md` | **Excelente** | Registra o “porquê” sem declarar-se autoridade sobre a regra. |
| `92_auditoria.md` | **Excelente** | É a camada que transforma documentação confiante em documentação comprovada. |

# O que já está muito bem resolvido

## 1. Não falta nenhum grande sistema

Os dez canônicos cobrem:

- princípios e vocabulário;
- domínio e ocupação;
- movimento;
- ciclo de ação;
- informação;
- combate;
- logística;
- transporte, fusão e aviação;
- captura, economia e progressão;
- turno, relatório e vitória.

Portanto, **não vejo necessidade de um capítulo 11 canônico**. O universo de regras está completamente coberto.

## 2. Os apêndices escolhidos são exatamente os necessários

Os cinco catálogos respondem às entidades que mudam com dados:

- construções;
- unidades;
- armas;
- RPS e Elite;
- sensores.

Isso preserva a diferença correta entre:

> “Como o sistema funciona?”

e:

> “Quais valores esta peça possui?”

O catálogo de armas, por exemplo, já separa adequadamente plataforma e armamento, e registra alcance, trajetória, domínio e munição como propriedades próprias. ?filecite?turn10file1?

## 3. A camada de engenharia ficou melhor do que eu havia imaginado

A combinação é excelente:

- `90`: o que está divergente;
- `91`: por que decidimos assim;
- `92`: o que foi realmente verificado.

A auditoria já registra evidência, commit e veredicto, e reconhece honestamente que cerca de um terço das afirmações normativas foi conferido. Ela também lista quais documentos ainda não foram auditados e quais partes permanecem sem validação, como Elite e alcance de sensores. ?filecite?turn12file0?

Isso é muito mais robusto do que simplesmente escrever um manual e presumir que o jogo o obedece.

# Ajustes necessários para a separação ficar limpa

## 1. Retirar o epílogo do documento `01`

O arquivo de princípios termina entrando novamente em:

- informação incompleta;
- movimento provisório;
- recálculo da névoa;
- comprometimento da ação.

Essas regras pertencem ao `04` e ao `05`. O epílogo literário pode ir para o final do `10`, fechando toda a biblioteca após a vitória.

Assim:

- `01` abre a doutrina;
- `10` fecha a experiência.

O `01` deve terminar depois de estabelecer vocabulário, esquadrão e Elite. ?filecite?turn6file1?

## 2. Limpar a fronteira entre `02` e `03`

O `02` deve responder:

> Onde isso existe e quem cabe aqui?

O `03` deve responder:

> Quanto custa entrar, por onde passa e que habilidade é exigida?

Hoje o `02` ainda explica:

- bônus de velocidade da estrada;
- pista improvisada;
- comportamento ferroviário;
- quem atravessa cada ponte.

Esses detalhes reaparecem de maneira mais completa no `03`. ?filecite?turn6file2?turn7file0?

No `02`, bastaria:

> Estradas, trilhos e pontes preservam os domínios declarados. Custos, permissões e combinações estão em `03_movimento_terreno_e_infraestrutura.md`.

A descrição física pode continuar. A regra operacional fica apenas no `03`.

## 3. Definir a fronteira entre `04` e `05`

A divisão ideal é:

### `04` possui

- prévia;
- confirmação;
- cancelamento;
- estados da peça;
- quais ações aparecem;
- como o menu usa a informação;
- quando o mundo é recalculado.

### `05` possui

- visível agora;
- explorado;
- nunca explorado;
- memória do terreno;
- última observação;
- detecção;
- ocultação;
- conhecimento do time.

Hoje o `04` explica detalhadamente os três estados de conhecimento, enquanto o `05` também constrói toda a epistemologia da névoa. ?filecite?turn7file1?turn7file2?

A correção é simples:

> O `05` define o que o time sabe.  
> O `04` define o que a interface permite fazer com esse conhecimento.

Não precisa remover os nomes dos três estados do `04`, mas deve trocar a explicação completa por uma referência.

## 4. Corrigir uma frase no `05`

Ainda está escrito que Air/High:

> “não projeta sombra e também não recebe nenhuma.”

Isso é amplo demais. O próprio capítulo depois afirma que sensores terrestres continuam sujeitos ao relevo ao procurar alvos em Air/High.

A formulação segura seria:

> Air/High nunca funciona como obstáculo intermediário. Um alvo em Air/High, porém, ainda pode ficar fora da linha de visão de um sensor terrestre; somente aeronaves detectando Air/High dispensam essa geometria.

A tabela também deveria mostrar `—` na altura como obstáculo de Air/High, porque a altura 4 é elevação da camada, não obstáculo que bloqueia visão.

## 5. Separar melhor `07` e `08`

O `07` deve possuir:

- consumo;
- isenção;
- combustível;
- reservas;
- custos;
- capacidade;
- atendimento;
- transferência logística.

O `08` deve possuir:

- como pousa;
- como decola;
- arremetida;
- permanência no solo;
- transição de camada;
- emergência;
- operação em convés e água.

Hoje o `07` explica em profundidade que o aeroporto dá subida completa, que outros locais levam a Air/Low e como isso expõe furtivos. Isso já é operação aérea, não logística. ?filecite?turn8file1?turn8file2?

No `07`, basta declarar:

> Aeronaves pousadas em instalações aeronáuticas válidas não pagam consumo. O ciclo de pouso e decolagem é definido em `08_transporte_fusao_e_operacoes_aereas.md`.

## 6. Completar o prometido pelo `10`

O arquivo se chama **Turno, Jornal e Vitória**, mas atualmente contém essencialmente Jornal e Vitória. ?filecite?turn9file1?

Ele precisa de uma seção curta:

## Início do turno

1. define-se o time ativo;
2. processam-se consumo e efeitos temporários;
3. resolvem-se pousos de emergência e perdas;
4. credita-se a renda;
5. reiniciam-se capacidades de atendimento e estados de ação;
6. monta-se o Jornal;
7. o jogador recebe o comando.

Não precisa duplicar as fórmulas de logística ou economia. Deve apenas indicar a ordem e apontar para `07` e `09`.

Alternativamente, renomeá-lo para `10_jornal_e_vitoria.md`, mas prefiro completar o fluxo. A biblioteca precisa de um endereço para o relógio macro da partida.

# Ajustes nos apêndices

## `11_catalogo_de_construcoes.md`

A tabela inicial é boa, mas cada construção ainda precisa da ficha completa prometida:

- domínios;
- posição;
- renda;
- visão;
- detecção;
- captura;
- mercado;
- catálogo;
- serviços;
- estoque;
- pouso;
- progressão;
- trem.

O próprio documento reconhece essa pendência. ?filecite?turn9file2?

## `12_catalogo_de_unidades.md`

O título interno ainda diz:

> Apêndice — Unidades e Armas

Mas as armas já possuem o `13`. Deve virar apenas:

> Apêndice — Unidades

Também falta declarar o molde exato de cada ficha antes de começar a preenchê-las. ?filecite?turn10file0?

## `13_catalogo_de_armas.md`

O campo “se pode revidar” não deve parecer uma escolha livre se o revide for derivado de:

- alcance mínimo 1;
- munição;
- domínio atual do operador;
- domínio do alvo;
- validade da arma.

Eu usaria:

> **Revide:** derivado / bloqueado por exceção explícita.

Caso não exista override de arma no sistema, nem precisa ser um campo da ficha.

## `14` e `15`

A separação está correta, mas estes dois arquivos só satisfazem plenamente a função depois de preenchidos. Hoje eles são contratos de estrutura, não fontes de consulta. ?filecite?turn10file2?turn11file0?

A matriz de sensores deveria conter pelo menos:

| Sensor | Camada alvo | Alcance | Ocultação detectada | Exige LoS | Escopo da revelação | Duração |
|---|---|---:|---|---|---|---|

# Ajustes nos documentos de engenharia

## `90_pendencias_tecnicas.md`

O conteúdo está correto, mas cada pendência deveria receber:

- ID;
- regra canônica relacionada;
- comportamento atual;
- evidência;
- impacto;
- prioridade;
- status;
- commit em que foi observada.

Hoje ele é uma excelente lista narrativa. Com esses campos, vira uma fila real de manutenção. ?filecite?turn11file1?

## `91_decisoes_de_design.md`

Está muito bom, mas algumas decisões apontam para **dois endereços canônicos**, por exemplo altitude em `05` e `08`, ou Jornal em `05` e `10`. Isso enfraquece ligeiramente a regra de “um endereço por regra”. ?filecite?turn11file2?

A solução é escolher um proprietário:

- mudança de camada: `08`;
- consequência para detecção: `05`;
- conhecimento produzido pelo Jornal: `10`;
- efeito da informação na memória do mapa: `05`.

Cada decisão deve apontar para a regra primária, e a secundária apenas referencia a primária.

## `92_auditoria.md`

Está excelente e não precisa mudar de conceito.

Eu apenas adicionaria um identificador para cada afirmação:

- `LOG-001`;
- `COM-014`;
- `FOW-008`.

Assim, uma entrada da auditoria, uma pendência e um parágrafo canônico conseguem apontar para a mesma regra sem depender do texto permanecer idêntico.

# Duas pendências pequenas de regra que sobreviveram ao split

## Arredondamento do dano aos passageiros

O `08` diz que a fração de dano é “arredondada”, mas ainda não declara claramente:

- arredondamento comum;
- para cima;
- para baixo.

O exemplo com 30% não resolve um resultado de 1,5. Essa direção precisa entrar no texto, mesmo que a auditoria já tenha confirmado o restante da implementação. ?filecite?turn8file2?turn12file0?

## “Classe de arma” no alcance zero

O `06` diz que existe “uma classe de arma” operando no alcance zero. Mas “classe de arma” já significa:

- antiaérea;
- antitanque;
- antiinfantaria;
- antinavio.

Melhor escrever:

> Existe um tipo de operação de arma no alcance zero.

Assim, carga de profundidade não parece criar uma quinta classe.

# O snapshot precisa ser verificado

O índice referencia exatamente:

`The Map Room Manual tecnico (9) — snapshot.md`

Mas não encontrei esse arquivo com esse nome exato entre os anexos; aparecem diversas versões genericamente chamadas `The Map Room Manual tecnico.md`. O snapshot precisa ser criado ou renomeado exatamente como o índice declara, para que a primeira referência da biblioteca não nasça quebrada. ?filecite?turn13file13?

# Conclusão

**A separação foi bem-sucedida.**

Não há capítulo sobrando, não há sistema importante sem endereço e não vejo motivo para criar mais arquivos. Até o `08`, que reúne transporte, fusão e aviação, ainda é coerente porque os três assuntos tratam de unidades mudando de estado.

O que falta agora não é redesenhar a biblioteca. É uma rodada de **normalização**:

1. retirar duplicações;
2. escolher proprietário único para regras de fronteira;
3. completar o fluxo macro do `10`;
4. preencher os catálogos;
5. estruturar pendências e auditorias com IDs;
6. corrigir os dois pequenos pontos normativos;
7. garantir a existência do snapshot.

Depois desses ajustes, a versão 9 pode realmente ser congelada, e os 19 arquivos passam a ser a autoridade operacional do projeto — não apenas uma cópia bem organizada do monólito.