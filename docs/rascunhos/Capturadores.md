# Capturadores

Este documento descreve o comportamento atual das unidades com papel `Capturador` na IA.

O capturador nao e um papel fixo por turno. A mesma unidade pode agir como reparo, oportunista, ponta de lanca, explorador, perseguidor ou defensor conforme o estado do plano, do setor, dos caminhos validos e dos inimigos visiveis.

## Ordem de decisao

1. Reparo/fusao.
2. Objetivo atribuido pelo plano.
3. Se nao ha objetivo e a unidade e rogue, marcha para o HQ inimigo.
4. Se o setor atribuido ja foi conquistado, entra em modo defensor.
5. Se o setor ainda tem construcao capturavel:
   - captura direta se ja esta no alvo ou consegue chegar nele;
   - combate imediato ao redor do objetivo, quando aplicavel;
   - captura oportunista;
   - exploracao de FoW;
   - scoring de avanco ou move+attack.

Todas as decisoes de movimento usam os caminhos validos calculados por `UnitMovementPathRules.CalcularCaminhosValidos`, usando a mesma tilemap de movimento do jogo. Celulas ocupadas por unidades mortas ou embarcadas nao devem bloquear caminho nem DPQ.

## Flags importantes

### `preferMoveOnBestDPQ`

Afeta movimento. Quando ligada, DPQ entra no score de avanco mesmo se nao houver ataque.

Uso esperado: unidades que devem escolher terreno melhor ao se deslocar.

### `prioritizeDpqAtBattle`

Afeta combate. Nao e uma ordem para fugir para DPQ vazio.

Quando ligada, a IA pode considerar celulas alcancaveis que nao avancam diretamente ate o objetivo, mas apenas se dali a unidade puder atacar um inimigo relevante. Se vai lutar, ela tenta lutar a partir do melhor DPQ disponivel.

Criterio de desempate em ataque:

1. prioridade do alvo;
2. DPQ da celula de ataque;
3. score normal da celula;
4. proximidade do setor;
5. tie-break de HQ inimigo.

### `playConservative`

Aplica penalidade por ameaca local no score de movimento. A unidade evita celulas perigosas quando ha alternativa util.

## isUnderRepair

Roda antes dos papeis de captura.

A unidade entra em reparo por HP baixo, autonomia baixa ou municao baixa, conforme os gatilhos do `UnitData`. Ao entrar em reparo, libera seu slot do objetivo e sai do plano normal de captura.

Prioridades:

1. Se esta em uma construcao aliada conquistada:
   - se o setor esta seguro, pode aguardar ali;
   - se ha ameaca e nao existe substituto saudavel proximo, segura a construcao e ataca se puder;
   - se existe substituto, pode sair.
2. Tenta fusao se `fuseWhileInRepair` estiver ligado e a soma de HP for valida.
3. Procura construcao aliada conquistada e desocupada para reparar, ignorando repCells de objetivos defensivos ativos.
4. Se nao houver destino de reparo, retorna para o proprio HQ.
5. Se ja esta nos arredores seguros do HQ, sem inimigos proximos ao HQ nem a unidade, aguarda e tende a ir para o fim da fila de iniciativa.

## Oportunista

Pode ocorrer tanto em unidades com plano quanto em rogues e defensores.

A unidade captura uma construcao capturavel alcancavel que apareca como oportunidade, desde que:

- a celula esteja livre;
- nao seja a celula excluida pelo contexto atual;
- o sensor de captura simulado confirme que a unidade pode capturar ali.

Quando outro capturador aliado nao atuado consegue capturar a mesma celula com custo menor, ou e o dono do objetivo e chega com custo equivalente, a oportunidade e reservada para ele.

## Defensor

Usado quando o setor atribuido ja esta conquistado ou quando o plano marca o setor como defensivo.

O defensor trabalha em torno da `RepresentativeCell` do setor.

### No predio/repCell

Se a unidade esta na repCell:

- ataca o melhor alvo disponivel a partir da posicao atual;
- se nao houver alvo, mantem posicao.

A escolha de alvo nao e aleatoria. O score considera:

- inimigo em cima do objetivo;
- morte garantida pela simulacao de combate;
- dano estimado;
- HP baixo do alvo;
- proximidade do alvo com o setor;
- alvo em construcao;
- custo do caminho;
- tie-break estavel por ID.

### Fora do predio, mas com repCell alcancavel

Se a repCell esta livre e alcancavel:

- move para a repCell e ataca ameaca dentro do raio defensivo, se possivel;
- se nao puder atacar, reforca a repCell.

### RepCell ocupada ou inalcan??avel

A unidade combate na zona defensiva.

`zoneEnemies` inclui inimigos visiveis dentro de `DefenseEnemyRange` do setor. Se o defensor ja esta dentro da zona, inimigos adjacentes ao proprio defensor tambem contam como ameaca local, mesmo que estejam a 4h do predio. Isso evita recuo quando o defensor ja interceptou contato corpo a corpo na borda da zona.

Se esta fora da zona defensiva:

- tenta move+attack contra inimigos da zona;
- se nao houver ataque, marcha para reduzir distancia ate a ameaca mais proxima da zona;
- se nao houver ameaca, marcha para a repCell.

Se esta dentro da zona defensiva:

- ataca da posicao atual se houver alvo valido;
- tenta move+attack dentro da zona;
- se `prioritizeDpqAtBattle` estiver ligado, procura a melhor celula de linha de tiro/DPQ para atacar;
- se nao houver ataque, tenta interceptar uma ameaca.

Interceptacao defensiva:

- considera ameacas dentro de `DefenseEnemyRange` do setor;
- a celula precisa reduzir distancia ate a ameaca;
- nao pode sair da zona defensiva;
- nao pode aumentar distancia ao setor;
- se a celula de interceptacao ja permite ataque, monta batch de ataque, nao so movimento.

## Ponta de lanca

Atua quando o objetivo do setor ainda e capturavel.

Se a unidade esta no hex alvo e pode capturar, captura. Se o alvo e alcancavel e esta livre, move ate ele e captura no mesmo batch quando o sensor permite.

Quando o alvo esta ocupado por aliado, a unidade aguarda em vez de tentar entrar no hex ocupado.

Quando nao consegue capturar direto, o scoring normal escolhe o melhor avanco em direcao ao alvo.

## Explorador

Atua quando o alvo de captura esta ocupado por inimigo invisivel pelo FoW.

A unidade procura uma celula adjacente alcancavel com melhor linha de visao. Se `prioritizeDpqAtBattle` estiver ligado, prefere maior DPQ; caso contrario, usa EV.

Ao chegar nessa celula:

- se puder atacar um alvo lateral relevante proximo ao setor, move e ataca;
- caso contrario, move para revelar o ocupante oculto.

## Perseguidor

E o capturador em modo de combate ao redor do objetivo ainda nao conquistado.

Ele aparece quando:

- existe inimigo visivel no caminho ou nos arredores do objetivo;
- a unidade nao e a melhor opcao para ocupar a construcao agora;
- a construcao esta disputada;
- ha alvo atacavel a partir de uma celula que avanca, ou de uma celula lateral valida quando `prioritizeDpqAtBattle` esta ligado.

Prioridade de alvo:

1. defensor em cima do objetivo;
2. inimigo em construcao;
3. inimigo mais perto do objetivo;
4. demais inimigos relevantes no entorno.

Com `prioritizeDpqAtBattle`, o perseguidor pode escolher uma montanha/floresta/DPQ melhor para atacar, mas so se dali houver ataque real. Ele nao deve trocar combate util por passeio em DPQ.

Se nao existe move+attack que avance ou melhore a posicao de batalha, pode atacar parado para eliminar bloqueador.

## Rogue

Capturador sem objetivo atribuido marcha para o HQ inimigo.

Regras principais:

- ataca da posicao atual se houver alvo bom;
- se ha inimigo no raio de engajamento, captura oportunidade antes de lutar;
- se nao houver captura, tenta move+attack para abrir caminho;
- captura construcoes oportunistas no caminho para o HQ;
- se o HQ tiver ocupante invisivel, busca DPQ/linha de visao para revelar;
- caso contrario, avanca para reduzir distancia ao HQ.

## Ocupacao e historico

A IA de decisao deve consultar apenas unidades ativas, vivas e nao embarcadas para bloquear caminho, atacar ou ocupar hex.

Unidades mortas por combate ou fusao podem continuar salvas para historico de partida, mas nao podem aparecer em `UnitManager.AllActive` nem bloquear celula. O save/load preserva esse historico sem restaurar essas unidades como objetos ativos no campo.

## Diagnostico

Os logs de score exibem:

- distancia ao objetivo;
- custo de movimento;
- DPQ da celula;
- ameaca local;
- tie-break de setor e HQ;
- ataques possiveis a partir da celula;
- celulas DPQ adjacentes ao objetivo que nao entraram em `paths`, com motivo de `notReachable`, ocupante diagnosticado e rota aproximada.

Quando uma celula aparece como `MISS notReachable`, a primeira suspeita deve ser divergencia entre caminhos validos da IA e do jogo, ocupacao fantasma ou tilemap de movimento incorreta.
