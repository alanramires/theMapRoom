# v10.0.0 — A IA lance a lance aprende o mapa

*2026-11-04 e 2026-11-05. Abre a v10.*

O fio destes dois dias foi o mesmo da v9.6.0: o autor joga, marca o passo estranho no AI Step,
e o agente lê só a última marca. A diferença é o que as marcas revelaram. Quase nenhuma era um
número errado; quase todas eram **uma regra presa no lugar errado** — uma constante de hex onde
devia haver banda, uma composição de solo onde devia haver família, um corte de estrada que
nenhuma ficha declarava. Consertar uma por uma foi tirando regra de dentro da IA e devolvendo
ao mapa: eixo, ilha, estrada, família. Daí o nome do major.

## Por que X, e não Y

A tabela do CHANGELOG diz que X é "alterar regra já validada". Esta versão muda uma **regra de
movimento do jogo**, para humano e IA: a estrada que atravessa uma construção deixou de cortar
o bônus de estrada (seção 3). Somado a isso, o núcleo mínimo do exército passou a contar por
**família** em vez de rótulo (seção 5) — o mesmo pacote 2/2/1, mas com outra definição de quem
fecha cada vaga. Os dois mudam o comportamento de todo mapa, não de um papel.

---

## 1. Blitzkrieg: "o de trás paga"

A doutrina do autor é a brincadeira do sorveteiro: cada um pega o sorvete, diz "o de trás paga"
e segue; só o último paga. A ponta **abre** o prédio e avança pelo eixo; quem fecha é quem vem
atrás.

- **A fila é o eixo, e a raiz é a base.** Quem está num prédio próprio do setor base está atrás
  em todos os eixos. Capturador de outro objetivo, no mesmo eixo, pode herdar. Ajuda lateral
  entre eixos continua com o oportunista, de propósito.
- **Ponta é quem está em cima do prédio**, mesmo que seja de outro objetivo (ela mantém a vaga
  e segue para o dela). Antes era "a primeira vaga", e o replanejamento do meio do turno
  invertia ponta e herdeiro.
- **O herdeiro tem de capturar no Tático dele**, de qualquer origem, com o desempate do autor:
  vem de trás > fecha > poder de captura > eixo > custo real.
- **Revezar não é ser expulso.** A ponta que sai faz o que um capturador faria: atira em quem
  captura prédio nosso, depois em quem captura o dele ("antes do tiro vem o dinheiro" — e se a
  gente puder atrasar o dinheiro do oponente também), depois em quem ameaça prédio nosso, e do
  melhor DPQ. Andar é o fallback.
- **Troca por poder de captura:** com inimigo colado no prédio, só reveza se o herdeiro fecha
  e a ponta não (Echo: #4 HP 7 contra #10 HP 10).
- **O herdeiro escolhido fica gravado no objetivo** (`HandoffHeirUnitId`, no save do plano).
  Antes ele era deduzido de "todo outro ocupante de vaga": o AI Step mostrava herdeiro errado e
  a ponta de Echo roubava Golf do herdeiro escolhido.

> **Dois erros meus, registrados para não repetir.** Primeiro, defendi uma decisão "porque a
> ponta fechava a cidade naquele turno" e depois implementei a trava "ponta que fecha não
> reveza" — que matava o blitz inteiro. Segundo, liberei o Operacional para o herdeiro da fila
> do eixo: o #5 largou Foxtrot, com inimigo perto, para quem só chegava no turno seguinte.
> "Não é simplesmente largar." As duas estão na memória do agente como doutrina.

## 2. Embarque: o veículo escolhe o par

O APC estacionava em cima da própria fábrica, depois no hex entre ele e o passageiro, e por fim
virou uma festa: cinco passageiros andando para o mesmo APC.

- **LZ nunca em produtora própria.**
- **LZ balizada pelo destino do passageiro:** +300 por MP que ela o adianta, medido no mapa de
  custo **real** do transportador a partir do destino (relativo ao passageiro, nunca distância
  absoluta). Somado ao "ele chega neste turno", a LZ cai na borda do Tático dele, rumo ao
  objetivo. O "2h fixo" (`ShuttlePickupRange`) era a gambiarra disso; segue no código.
- **O QueroCarona não dizia para onde ia justamente quem pedia carona.** `evaluatedTarget` só
  era preenchido por quem recusava; o balizamento puxava para (0,0), e o Questionário e o reboque
  de artilharia liam o mesmo vazio.
- **Quem escolhe o par é o veículo:** o passageiro que quer ir ao encontro de um veículo que
  ainda não agiu cede a vez; fechado o par, o hex de encontro fica reservado e casco com dono não
  atrai mais ninguém.

## 3. A estrada atravessa a construção (regra de movimento)

O tanque saía da fábrica pelo QG e não ganhava o hex extra, embora a rota autorada ligasse
fábrica e QG. A busca dos Caminhos Válidos só contava o passo como estrada com a **estrada
governando o hex**; no QG em planície quem governa é a construção, e a corrente quebrava sem
regra escrita nenhuma. `DidUseRoadFullMoveBonus` e a conta de combustível não exigiam isso — as
três discordavam.

Decisão do autor: a construção continua cobrando custo e skills (e `inheritStructureRulesOnlyOn`
segue decidindo, por terreno, se a estrada assume o hex), mas **a continuidade do bônus segue a
rota**, onde a própria rodovia permitir. Na montanha a rodovia já desliga o bônus, então cidade
nos Alpes não vira atalho. Uma pergunta só para as três contas (`IsRoadBoostStep`). Manual 03
atualizado. Validado no AI Step: Tanque Leve #15 pela fábrica e pelo QG até (6,10) com bônus.

> **Erro meu:** afirmei duas vezes sem conferir — que não havia trecho fábrica→QG (havia, no
> `Mundo Fixture.asset`) e que a cidade nos Alpes ganhava bônus (a `Rodovias.asset` desliga na
> montanha). Antes de falar de estrada, ler a rota autorada e o StructureData da rodovia.

## 4. Teto macro: atribuído não cai

O amarelo tomou Foxtrot, o território do azul caiu a 36%, a fase virou Perdendo, o teto ofensivo
baixou de 6 para 3 — e Foxtrot, com o #14 a caminho, foi o cortado. O dono do setor não tira a
atribuição; foi a fase que ele disparou. Agora objetivo do eixo com capturador vivo a caminho fica
protegido do corte, como a captura em andamento.

## 5. Shopping

- **O veículo nasce na fábrica com estrada.** O bônus só vale partindo dela. Primeiro mexi na
  ordenação antiga de fábricas, que **não é o caminho em uso** (o carrinho por papéis é), e
  ordenar não bastava (a fábrica com estrada levava Soldado). O conserto decide O QUE e depois
  troca ONDE (`SwapRoadVehiclesToRoadFactories`). "Tem estrada" = estrutura no próprio hex.
- **Núcleo e pacote 2/2/1 por família.** O core continua "2 Assalto + 1 Fogo de Suporte", mas
  contado por `ResolveFamily` (Apache e caças são Assalto; SAM e AAA são Fogo de Suporte). O
  Feijão Torto fecha com tanque e artilharia como sempre.
- **Ilha sem ligação por terra ao inimigo** (Peixe Pequeno): o pacote pede ataque aéreo e
  antiaéreo, e a poupança do primeiro blindado desliga. A intel separa "sem dado" (não corta) de
  "sem caminho"; **ilha invadida** (inimigo em terra na mesma ilha geográfica do QG) religa tudo.
- **`RequiredFamily` na demanda:** o Obus Leve é família Assalto mas satisfaz FogoIndireto e é o
  fogo mais barato; sem isso, a vaga de fogo compraria Obus que nunca somava nela.

## 6. Ilhas e recorte de autoria (trabalho do autor)

O `BeachManager` cataloga **ilhas geográficas** (planície, floresta, montanha e praia contínuas;
água e ponte separam) com setores por prédio, praias e contagem de terrenos, e passou a respeitar
o quadrante do Map Helper. É fato de geografia, não de alcance: "o tanque chega lá?" continua com
o `SectorManager`. O Shopping usa a ilha para saber se a casa está sendo invadida.

## 7. Ferramentas

- **AI Step** (outra sessão de IA): classificação da marca, nota multilinha, postura no
  cabeçalho, F10 prioritário, quem vai agir e os envolvidos do passo.
- **`show/hide eixos`** e **`ai replan`**: o replan existe porque o plano vai no save — carregar
  um save do meio do turno reexecuta a decisão antiga e esconde a mudança de código.
- **`Tools > Axis Editor`**: a fila de cada eixo por slot, com setas, rally sempre no fim e o
  desenho no Scene View. Lê a cena na autoria (o bake é foto e fica velho — o dono do rally de
  Juliet estava na cena e não no bake). O bloco de eixo no Inspector passou a respeitar a ordem
  do clique e o quadrante do Map Helper.
- **`Tools > Role Families`**: as seis famílias, os rótulos de cada uma e as fichas, lidos das
  regras do código.

## 8. Mapa: metrópole, town e Peixe Pequeno (trabalho do autor)

Metrópole (30 pontos, 2000 de renda) para enxugar ilhas de cidades; town redesenhada (traço fino
e cinza, menor). Retângulos dos quadrantes encolhidos para não se sobreporem — a borda
compartilhada enfiava prédios do Peixe Pequeno no bake do Feijão Torto e da Terra Firme.

---

## O que não terminou

- **Nada de shopping, ilha, família ou estrada foi visto rodando no Peixe Pequeno.** O teste da
  IA aérea + exército sem ligação por terra é o próximo passo.
- **Counter compra tanque contra blindado na ilha do inimigo:** a demanda de counter não pergunta
  se a ameaça alcança a nossa ilha.
- **Capturador com vaga num objetivo que NÃO está revezando** ainda pode chegar antes do herdeiro
  escolhido e capturar.
- **O APC adiado e o passageiro adiado** caem na ordem de iniciativa na segunda passada.
- **Prédio neutro na própria base** (o aeroporto do Q3) não vira objetivo; só o oportunista pega.
- **Perdendo cedo demais com prédios neutros** foi discutido e mantido; o autor vai observar.
- **Medir** o custo do mapa de custo real por destino no MelhorEmbarque (contador
  `MelhorEmbarqueDestinationCostMaps`).
- **Feijão Torto:** o Obus Leve passou a contar como assalto e a vaga de fogo aceita AAA. Observar.
- **ShuttlePickupRange = 2** pode ser aposentado depois que o embarque for validado.
- **Doutrina do blitz** ainda não está escrita em `docs/AI Behavior/Capturador.md`.
