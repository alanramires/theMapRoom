# v9.3.0 — a aula vira um quadrante

Os tutoriais existiam: três Histórias com roteiro, objetivos, broncas do Sargento e
um inimigo roteirizado. O autor tinha parado a ideia por um motivo só:

> *"eu parei a ideia porque não queria criar 50 scenes"*

Cada aula era uma cena. Com o MVP validado, a pergunta virou: dá para a aula ser
**um quadrante de um mundo**, como os mapas da Campanha? Este dia montou o motor
para isso. O que já existia (o `TutorialData`) quase não mudou; quem mudou foi o
palco.

**A descoberta que organiza o resto:** o `TutorialData` já era dado puro. Falas,
tarefas, comandos e travas estavam todos no asset, e a cena só servia de palco.
Por isso o salto saiu em fios e conversões, não numa reescrita.

---

## Frente 1 — o quadrante vira missão

**A Academia é um mundo como o Fixture:** Mundo → Bloco (Caserna, Suboficiais) →
Campanha (patente: Soldado, Cabo...) → Quadrante (a aula). Quanto mais elaborada a
lição, mais alta a patente.

- **`QuadranteData.tutorial` (Aula).** É dado autoral e sobrevive ao bake. Quando a
  Batalha monta o quadrante, ele mesmo entrega a aula ao `MatchController`. O
  contrato só carrega o endereço, e quem resolve o endereço entrega o mapa e a aula
  da mesma fonte. Quadrante sem aula zera o tutorial, para não herdar o roteiro
  serializado na cena.
- **O Map Helper mostra o campo**, e o cabeçalho do quadrante ganha o selo "· AULA".
  No Inspector padrão o campo ficava cinco níveis abaixo e o autor não achou.
- **Fim da aula = fim de partida.** A vitória coroa o **humano local**, não o slot 0
  fixo. Ela dispara `OnMatchConcluded` com o motivo "aula concluída", e a Campanha
  registra e volta para o mapa. A derrota usa o `Panel_vitoria` ("DERROTA!"),
  porque o `Panel_endGame` das cenas antigas não existe na Batalha. Ela volta para
  o mapa sem pintar o quadrante.

### Coordenadas: três referenciais, e o autor escolheu o do mundo

A Batalha **não** é montada nas coordenadas do mundo. Ela é um transplante do
recorte, pintado a partir de `(0,0)` (ou `(0,1)` com `originY` ímpar, por causa da
paridade odd-r). Comecei com o roteiro em coordenada **local** do quadrante. O
autor respondeu:

> *"eu não vou fazer conta não... eu vou por move sd 60,32 63,33"*

E ele tinha razão: a coordenada que ele vê é a da cena de autoria. O roteiro passou
a falar **em coordenada do mundo**, e a Batalha converte com a mesma conta do bake
(`tabuleiro = mundo − canto + origem da pintura`), num leitor só
(`TryParseScriptCell`).

**Achado: o `x,y` era lido em seis lugares, três deles cópias inline.** Somar a
origem só nas funções deixaria o roteiro meio dentro, meio fora do mapa. Os seis
passam agora pelo mesmo leitor, e o hex-alvo do `AutomataData` também.

**Errei um aviso e corrigi:** escrevi primeiro que "mover o retângulo quebra o
roteiro". Com coordenada do mundo é o contrário: ajustar o retângulo não quebra
nada, porque o hex do mundo é o mesmo. O que quebra é levar o **desenho** da aula
para outro lugar.

### O nome do prédio atravessa o bake

O roteiro acha construção pelo nome (`show Bandeira`). Esse nome era o **nome de
exibição autorado**, que já existia no `ConstructionManager`, mas o bake não o
levava: na Batalha o prédio nascia como `Fábrica_T-1_C16`. Agora o bake leva
`nomeAutorado`, e o quadrante o devolve no spawn.

---

## Frente 2 — numa aula, só as tarefas decidem o fim

**Achado:** a regra normal de "exército eliminado" rodava nas aulas. Na aula da
cabeça de praia, a facção inimiga **só nasce no meio da lição**. No turno 2 ela
seria dada como eliminada, e o aluno venceria sem fazer nada.

O autor pediu para separar as flags em grupos. Ficaram **dois grupos de regras de
fim** (`MatchController.UsesMatchEndRules`):

```text
partida   QG capturado, exército eliminado, estrelas de vitória, rendição
aula      as tarefas do TutorialData
```

Numa aula, o grupo da partida não decide nada. A rendição vira derrota da aula. O
"ficar sem unidades" volta como tarefa declarada (`PLAYER_ELIMINATED`).

### Quem joga pelo inimigo é escolha da aula

**Achado:** o contrato da Campanha leva o slot 1 como **IA**, e o `AIController` não
sabia o que é uma aula. A IA de verdade e o automata dirigiriam o mesmo turno.
Primeiro tirei a IA das aulas. Aí o autor quis justamente a IA:

> *"eu pensei em usar o magnético do capitão de soldados rebeldes (sem hq no quadrante)"*

Virou o campo **`inimigo`** no `TutorialData`: *Automata* (roteirizado, previsível)
ou *IA de verdade* (perfil do contrato; facção sem QG = rebelde). Cada lado só joga
quando a aula pede.

**Outra armadilha do mesmo tipo:** o automata rodava em **qualquer** partida em que
o `TutorialManager` estivesse presente. Nas cenas antigas isso nunca apareceu,
porque o componente só existia nelas. Na Batalha, ele dirigiria o turno da IA de
verdade em toda partida normal. Agora ele só age com aula.

A aula também impõe as próprias **regras** (`forcarRegras` + `regras`) e a própria
**dificuldade** (`forcarDificuldade` + `dificuldade`), por cima do menu.

---

## Frente 3 — tarefas e spawn que a cabeça de praia pedia

A primeira aula de verdade veio do autor: um Chinook com dois soldados, uma
fábrica na praia, e a guarnição da Metalion surgindo quando a captura começa.

- **Não existia tarefa de captura.** Criei `TurnStateManager.OnCaptureResolved`,
  pendurado no `ExecuteCaptureSequence`, o ponto único de captura confirmada para
  humano e IA. Em cima dele, três tarefas: `CAPTURE_PROGRESS`, `CAPTURE_CONSTRUCTION`
  e `ENEMY_CAPTURE`. Também criei `PLAYER_ELIMINATED`, avaliada só quando uma
  unidade morre, nunca no começo da aula.
- **Achado: o spawn não conferia ocupação.** O inimigo nasceria empilhado em cima do
  aluno. Agora hex ocupado no chão não recebe spawn, e aeronave no ar não bloqueia.
  O "guardar o caixão" do autor virou regra.
- **Spawn por bandeira (`@flag`).** A ideia é do autor: construções ocultas
  `flag#1`, `flag#2`. `@flag` sorteia uma bandeira livre; com `perto=X`, pega a mais
  próxima. Com mais bandeiras do que soldados, cada partida sai diferente.

---

## Frente 4 — a unidade embarcada atravessa o bake

O autor embarca soldados no Chinook na cena de autoria. O bake gravava cada unidade
pela célula: o soldado viraria um soldado solto no mar. Agora o passageiro é
gravado preso ao transporte (`transportadorIndice` + compartimento), e o
transporte entra antes dele na lista. A Batalha embarca de novo pelo mesmo
`TryEmbarkPassengerInSlot` que o load usa.

---

## Frente 5 — o botão Tutorial abre a Academia

O mesmo assistente do "Campanha vs AI", em **modo Academia**: cor do aluno → cor do
inimigo → confirmar. Dificuldade e regras são da lição.

**Achado: o mundo não estava só na cena Campanha.** O `QuadranteController` da
**Batalha** também tinha o mundo fixo. Uma aula chegaria lá procurando a campanha
"Soldado" dentro do Fixture. O mundo agora viaja no contrato
(`PartidaConfig.MundoAtivo`) e não é limpo pelo `Clear()`, porque precisa
sobreviver às idas e voltas Campanha ↔ Batalha. "Campanha vs AI" põe vazio, e cada
cena usa o mundo serializado nela.

O campo `mundoAcademia` fica no grupo **Mundos** do `PanelMenu`. Primeiro eu tinha
posto o campo enfiado entre dois botões, e o autor quase não achou.

---

## Frente 6 — documentação para escrever aulas

- [`tutorial/sintaxe.md`](tutorial/sintaxe.md): todos os comandos, tarefas,
  parâmetros, campos e convenções, conferidos no código.
- [`tutorial/aulas_caserna.md`](tutorial/aulas_caserna.md): a Operação Cabeça de
  Ponte (Soldado 1), Quem tem o morro (Soldado 2), A carona (Cabo 1) e ideias por
  patente.

---

## Autoria do autor

O mundo **Academia** com a cena de autoria (Caserna, campanhas Soldado e Cabo, três
quadrantes). As aulas `Caserna - Soldado 1` e `2` estão criadas e ligadas aos
quadrantes, ainda **sem tarefas e sem falas**. O `TutorialManager` foi posto na
Batalha com o Automata Database. O botão Tutorial foi ativado na Tela de Entrada,
com a Academia ligada. A cena de autoria antiga `Autoria/Mundo.unity` foi apagada;
conferi que nenhum asset aponta para ela.

---

## O que não terminou

- **Nada disto rodou em Play.** Tudo compila. O caminho inteiro (menu → Academia →
  aula → vitória → volta) não foi exercitado.
- **As aulas estão vazias.** Os roteiros estão em `aulas_caserna.md`; as tarefas e
  falas ainda precisam ser passadas para os assets.
- **Riscos que só o Play mostra:**
  - **Chinook nascendo no ar.** O bake não guarda a camada; se nascer "pousado no
    mar", o primeiro voo ou o embarque podem falhar.
  - **Soldado no hex de mar antes de embarcar:** o spawner pode recusar. O Console
    avisa.
  - **O magnético da IA rebelde precisa de setor** na fábrica, senão o plano
    degenera.
- **Save no meio da aula:** o save não guarda em que fala o roteiro estava. Também,
  um save da Academia carregado pelo menu abre a Campanha no Fixture, porque o load
  não sabe o mundo.
- **O aluno é sempre o slot 0.** O roteiro, as travas e `UNIT_AT_HEX` assumem isso
  em vários pontos.
- **`TutorialRules`** tem uma regra legada presa ao id `tutorial 1 - soldado`.
- **`Mundo Fixture.asset`:** um campo `caminho` de eixo perdeu o primeiro elemento
  (`0,2,3,8` → `2,3,8`). Não sei se foi edição do autor ou do editor; foi junto do
  churn e precisa ser conferido.
