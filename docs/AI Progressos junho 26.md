# AI Progressos - junho 26

Este documento e uma leitura de estado, nao um changelog. A AI ja passou do ponto de "um monte de unidades tomando decisoes locais" e comeca a parecer um time com intencao: captura, cobertura, artilharia, transporte, reparo e compra ja conversam o suficiente para produzir jogadas coerentes. Ainda existem decisoes pontuais tortas, mas elas estao ficando mais faceis de diagnosticar porque quase sempre aparecem como um conflito claro entre duas heuristicas.

## Visao geral

Minha leitura e que a AI esta entrando numa fase boa: o esqueleto operacional esta funcionando. O plano escolhe setores, abre slots, atribui papeis, compra para cobrir deficits e executa unidade por unidade com commits de mundo entre as acoes. Isso e bem diferente de uma AI puramente reativa.

O resultado pratico ja aparece em jogo. Unidades cedem espaco para outras com melhor funcao, transportes tentam entregar passageiros em vez de apenas andar, artilharia e tratada como ativo especial, reparo remove unidades quebradas da linha e o planner entende melhor quando uma base, anchor, rally ou setor comum pedem comportamentos diferentes.

O ponto mais importante: os bugs recentes nao indicam ausencia de sistema. Eles indicam excesso de regra local competindo com uma intencao global que ja existe. Isso e um bom problema. Significa que agora estamos calibrando julgamento, nao inventando a AI do zero.

## O que esta funcionando bem

### 1. Organizacao por papel

A separacao por papeis ficou produtiva. Capturador, assalto, fogo de suporte, transporte, logistica e reparo tem arquivos e fluxos proprios. Isso permite corrigir comportamento ruim no lugar certo sem baguncar o resto.

O ganho mais visivel e que a AI consegue tratar unidades hibridas com mais cuidado. O caso do Tanque Z mostrou isso: ele nao e "so assalto" nem "so artilharia"; ele precisa preferir o tiro bom quando existe, mas cair no comportamento de assalto quando nao existe tiro util. Esse tipo de nuance so e possivel porque a arquitetura ja suporta decisao por papel.

### 2. Plano, slots e handoff

O `TeamObjectivePlan` ja da uma linguagem boa para a AI: cada setor tem objetivo, status, prioridade e necessidades de unidade. O handoff entre papeis tambem esta melhor. A unidade nao precisa sempre "querer a mesma coisa"; ela pode liberar slot, sair para reparar, ceder predio, esperar transporte ou virar rogue quando nao tem atribuicao boa.

Esse e um dos sinais mais fortes de coesao: o time comeca a agir como conjunto porque a unidade individual nao e dona absoluta do plano.

### 3. Artilharia como ativo protegido

A artilharia esta mais perto do comportamento esperado. Ela nao deve correr para a vanguarda so porque alguem entrou na mira, e o transporte que carrega artilharia tambem nao deve atravessar a frente como se fosse APC de assalto.

Os ajustes recentes deixaram uma regra mais clara:

- artilharia procura tiro bom;
- se nao tem tiro bom, procura posicao coesa;
- se e conservadora, precisa de screen ou retaguarda;
- se esta embarcada em TOW/caminhao, nao deve ser levada para a vanguarda carregada;
- se nao da para entregar no objetivo, pode ser largada atras das linhas aliadas.

Ainda falta coordenar FoW para ela, mas a identidade do papel esta ficando correta.

### 4. Reparo e "lutar ate o fim"

O repair deixou de ser apenas "voltar para casa". Agora ele tem uma leitura mais militar: se existe caminho, recua; se esta em base/HQ sob ameaca, pode segurar; se todas as ancoras estao bloqueadas, pode entrar em modo de combate em vez de fugir mecanicamente.

Esse ponto e importante porque unidades quebradas ainda podem ser valiosas. Um tanque danificado que nao consegue escapar pode fazer uma troca boa antes de morrer. O ajuste para usar simulacao de combate nesse modo foi essencial: sem isso ele escolhia alvo por heuristica e nao por resultado esperado.

### 5. Debug e telemetria

Os logs estao carregando o projeto. Hoje da para olhar uma jogada ruim e descobrir a historia:

- que objetivo estava ativo;
- qual papel decidiu;
- quais filtros bloquearam candidatos;
- qual score venceu;
- se o ataque passou pela simulacao;
- se o movimento veio de progressao, rendezvous, fallback ou emergencia.

Isso diminui muito o custo de iterar. A AI esta complexa, mas esta ficando observavel.

## Onde a AI ainda tropeça

### 1. Ranking de combate ainda precisa ser unificado

Esse foi o padrao dos ultimos ajustes: uma rotina chama `PassesAttackDecision`, mas depois ranqueia alvos com heuristica propria. Quando a heuristica pesa DPQ, preferencia de alvo ou HP de forma exagerada, ela escolhe uma troca ruim mesmo que a calculadora consiga ver que outro alvo era melhor.

Minha opiniao: toda escolha de ataque que compara mais de um alvo deveria usar a simulacao como score principal. Heuristicas de papel devem ser bonus e desempate, nao a base da decisao.

Regra pratica:

- simulacao decide se a troca presta;
- kill, dano, perda propria e sobrevivencia pesam primeiro;
- prioridade de alvo, DPQ da celula, distancia e objetivo ajustam depois.

### 2. FoW ainda e mais filtro do que coordenacao

A AI respeita FoW em varios pontos, mas ainda nao usa FoW como operacao coordenada. O proximo salto para artilharia depende disso.

Hoje uma unidade pode revelar por oportunidade, e a artilharia pode atacar se ve alvo. O que falta e um pequeno protocolo:

- artilharia tem alvo provavel ou setor de tiro;
- batedor/infantaria identifica celulas que abririam LoS;
- iniciativa sobe quem revela antes de quem atira;
- se revelar funcionou, artilharia age no mesmo turno;
- se nao funcionou, artilharia reposiciona ou segura.

Isso transformaria artilharia de "peca que aproveita alvo visivel" para "peca que pede olhos".

### 3. Pequenos grupos ainda nao existem como entidade forte

Existe coesao local, mas nao existe ainda um "grupo" persistente com lider, membros, frente, retaguarda e objetivo comum. O planner tem slots e o tactical analyzer tem operacoes, mas no microturno cada unidade ainda decide muito por si.

O comportamento ja sugere grupos emergentes: capturador + assalto + artilharia + transporte. Falta transformar isso em uma abstracao leve:

- grupo de captura;
- grupo de defesa;
- grupo de pressao;
- grupo de rally;
- grupo de evac/reparo.

Nao precisa virar um sistema enorme. Pode comecar como uma leitura temporaria por objetivo: "quem esta tentando resolver este setor neste turno?".

### 4. Conservador/agressivo ainda e binario demais em alguns lugares

Vimos isso no TOW com artilharia: primeiro estava agressivo demais, depois conservador demais. O comportamento certo era intermediario: nao ir para vanguarda carregado, mas soltar a carga em retaguarda util.

Esse padrao vai aparecer de novo. "Conservador" nao deveria significar "parado"; deveria significar:

- nao piorar a exposicao;
- manter screen;
- preferir retaguarda;
- evitar troca ruim;
- ainda assim fazer trabalho util.

### 5. Rally, anchor e defesa territorial precisam continuar bem separados

A distincao recente foi correta:

- anchor e economia/recuperacao;
- rally so e rally de invasao se aponta para o HQ inimigo;
- rally que aponta para minha base pode ser defensivamente relevante, mas nao e rally de invasao para mim;
- setor recem-capturado pode pedir guarnicao temporaria, mas nao deve virar rally por acidente.

Isso precisa continuar protegido, porque e uma fonte facil de bugs "a AI ficou fazendo churrasco no setor errado".

## Minha avaliacao do estado atual

Eu diria que a AI esta em um ponto "jogavel e interessante". Nao e so funcional. Ela ja gera situacoes que parecem decisao militar: proteger ativo caro, recuar unidade quebrada, ceder slot, pressionar com assalto, usar transporte, tentar reparar, evitar vanguarda com peca fraca.

O que ainda falta nao e "fazer a AI jogar". Ela ja joga. Falta deixar ela menos surpreendida por casos compostos:

- alvo visivel mas troca ruim;
- artilharia boa mas sem olhos;
- transporte com carga valiosa perto da frente;
- unidade em reparo cercada;
- grupo com capturador avancado e apoio atrasado;
- setor com multiplos significados estrategicos.

Isso e calibracao de comando, nao fundacao.

## Proximos passos com maior retorno

### 1. Criar um scorer unico de ataque para AI

Extrair um helper publico interno tipo `ScoreAIAttackCandidate`, usando `TrySimulateAttackForAI` como base. Ele poderia retornar:

- score;
- targetDamagePct;
- attackerLossPct;
- kill;
- survives;
- attacker/defender DPQ;
- weapon usada;
- motivo resumido.

Depois substituir aos poucos os rankings locais que hoje repetem heuristica.

Prioridade alta porque isso evita decisoes "doidas" em todos os papeis: assalto, repair, capturador, swap, defesa, transporte armado.

### 2. Operacao simples de spotter para artilharia

Nao faria um sistema grande ainda. Eu faria uma primeira versao pequena:

- detectar artilharia sem alvo, mas com inimigo provavel/objetivo proximo;
- procurar aliado capaz de revelar LoS em celula segura;
- promover esse aliado na iniciativa;
- logar "spotter para FS #id".

Se funcionar, vira um dos comportamentos mais visiveis e satisfatorios da AI.

### 3. Group context temporario por objetivo

Antes de criar "squads" persistentes, montar um contexto por objetivo no turno:

- capturadores atribuidos;
- assaltos atribuidos ou proximos;
- fogo indireto atribuido ou proximo;
- transportes servindo esse objetivo;
- linha media do grupo;
- unidade mais avancada;
- unidade isolada.

Isso alimentaria regras simples: nao mandar artilharia alem da linha, nao mandar capturador sem apoio, reagrupar assalto isolado, escolher rendezvous.

### 4. Melhorar linguagem dos logs de bloqueio

Os logs bons aceleram tudo. Alguns pontos ainda precisam de contadores de "por que nao escolheu":

- ataque rejeitado por simulacao;
- drop de artilharia sem screen;
- courier sem celula de retaguarda;
- fire support sem spotter;
- repair sem anchor e sem alvo util.

Quando um log diz so "sem candidato", a investigacao ainda custa caro.

## Conclusao

Minha visao bate com a sua: esta ficando divertido. A AI ja mostra comportamento de time, nao apenas de pecas soltas. A sensacao de coesao vem de tres coisas que agora estao aparecendo juntas: plano compartilhado, papeis especializados e commits de mundo confiaveis entre acoes.

O proximo salto nao e adicionar mais regras em todos os lugares. E consolidar dois ou tres conceitos centrais:

- combate sempre ranqueado por simulacao;
- artilharia com spotter/FoW coordenado;
- pequenos grupos temporarios por objetivo.

Com isso, os ajustes pontuais devem diminuir e as jogadas boas devem parecer menos acidentais e mais intencionais.
