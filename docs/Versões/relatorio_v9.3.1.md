# v9.3.1 — A primeira aula fecha de ponta a ponta

A v9.3.0 deixou a Academia de pé: a aula é um quadrante, o roteiro anda, o mapa
nasce. Esta versão é o que apareceu quando o autor **jogou** a aula 1 da Caserna
do começo ao fim — e cada atalho que um aluno real tomaria virou ou uma trava
de roteiro ou uma regra que não existia.

O fio do dia: **um tutorial não é testado lendo o roteiro, é testado tentando
quebrá-lo.** Quase tudo abaixo nasceu de "e se o jogador fizer X sem querer?".

---

## 1. O roteiro roda depois do turno 1, não antes

A fala 0 da aula marcava o Chinook como "já agiu" (`acted CH`) e o helicóptero
começava ativo mesmo assim. Sem erro, sem log.

A ordem real de inicialização era:

```text
PanelDialogTutorialController.Start   → fala 0 → acted CH
MatchController (coroutine, frames depois)
  ApplyActiveTeamIfChanged(force) → ReleaseUnitsForActiveTeam → ResetForTeamTurnStart
                                    └── zera o HasActed que a fala acabou de pôr
```

A fala não estava errada; ela rodava **antes** de o turno 1 existir.
`MatchController.MatchStartApplied` sobe logo depois do primeiro início de turno,
e a fala 0 espera por ele (`BeginScriptAfterMatchStart`, teto de 5 s para load de
save e cena não jogável, onde a flag nunca sobe).

Consequência que o autor descobriu jogando: o combustível assado agora sofre o
upkeep do turno 1 antes da primeira fala. Ele assou 32 para o aluno ver 30. É o
comportamento honesto — o turno 1 cobra como qualquer outro.

## 2. O bake carrega o estado da autoria

Pintar o Chinook com 30 de combustível na Academia não adiantava: ele nascia com
60 e só "caía" para 30 quando a fala da Capitã Moura rodava `CH fuel=30`. O
jogador via o tanque esvaziar no meio de uma fala.

`UnidadeAssada` ganhou `hp` e `combustivel` (-1 = cheio). O bake só grava o que
**difere** do máximo, e trata 0 como cheio: unidade de cena nunca inicializada lê
0, e nascer com o tanque seco derrubaria o helicóptero no primeiro upkeep.

## 3. Travas que só valem para o aluno

O sargento deu bronca **na IA**. A fala muda que espera o turno do jogador trava o
"passar a vez" no início do turno inimigo — e a IA passa a vez pelo mesmo
`TryOpenEndingTurnConfirmation` do humano. Levava a bronca e ficava presa.

A regra que saiu disso vale para todas as travas de aula:

> **Bloqueio de roteiro é regra do aluno.** No turno da IA, `IsEndTurnLockedByTutorial`
> e os quatro `block*` (Serviço do Comando, remover, render-se, situação) respondem
> "livre". A trava de comando do Automata em andamento continua valendo.

A História 1 nunca acusou isso porque o inimigo dela é Automata, que não passa a
vez por esse caminho.

## 4. Atalhos que quebravam a aula

Cada um destes foi encontrado jogando, e cada um tinha uma saída diferente:

| atalho do aluno | o que acontecia | saída |
|---|---|---|
| desembarca **um** soldado só | Chinook já agiu, não solta o segundo; passar a vez travado → impasse | o passar a vez **destrava** na ordem de desembarque. Um dia a mais, nunca um impasse |
| sobe um soldado na fábrica antes da hora | captura começa sem o "um captura, outro cobre" | `capturable PortoFerro off/on` |
| fica voando sem fazer nada | aula infinita | derrota `UNIT_DEAD CH \|\| AUT=0` |

**A trava de captura não mexe no `isCapturable`.** Desligar o capturável do
prédio faria a IA e o `SectorManager` replanejarem em cima de uma fábrica que
"deixou de ser capturável". `ConstructionManager.CaptureLockedByScript` é lida só
pelo `PodeCapturarSensor`: o mundo continua vendo o mesmo prédio, só a **ação** é
negada.

**O "pousar" não existe.** Escrevi um objetivo `UNIT_LANDED` para a LZ e o autor
corrigiu: pouso é etapa de animação do desembarque (desce, solta, decola de
volta — termina no ar) ou pouso de emergência por combustível. O objetivo nunca
completaria e o aluno ficaria preso. A missão virou "esvazie o transporte":
`UNIT_DISEMBARKED CH` só completa com `GetEmbarkedPassengerCount() == 0`.

**Eu ia criar `UNIT_OUT_OF_FUEL` e ele já existia.** `UNIT_DEAD` com
`TOKEN || AUT=X` checa combustível ao mover e no início do turno. Apaguei o meu.
É o mesmo modo de falha registrado em "verificar antes de documentar": busca pelo
nome que eu imaginei não acha o conceito com o nome que ele tem.

E a aula 2 mostra por que isso é parâmetro e não regra: lá o Chinook **precisa**
chegar a 0 (pouso forçado de propósito), então ela usa `UNIT_DEAD CH` sem `AUT`.

## 5. O que o aluno vê

- **Contadores na lista**: `UNIT_DISEMBARKED` mostra `(1/2)` (total memorizado:
  o "/2" não vira "/1" depois do primeiro desembarque); `CAPTURE_CONSTRUCTION` e
  `CAPTURE_PROGRESS` mostram a resistência do prédio **decrescente** — o mesmo
  número da plaquinha.
- **Gatilho interno** (`isInternal`): tarefa checada mas fora da lista, sem bipe.
  "Comece a capturar" existe só para disparar o spawn da guarnição. Não é o
  `isVisible` — esse é estado de runtime, e tarefa invisível **não é checada**.
- **Fala final**: `defeatText` por tarefa e `victoryText` na aula. O sargento fala
  e fica (`ShowFinalLine`, retrato de bronca só na derrota); o painel de resultado
  espera 3,5 s para não cobrir o balão no mesmo frame.
- **O sargento some** quando a ordem é "passe a vez" ou "segure a fábrica": a fala
  avança no turno inimigo e uma fala muda espera o próximo gatilho com o balão
  escondido — o truque da História 1, agora usado de propósito.
- **Jornal do comando** some ao selecionar unidade (cursor sai do `Neutral`), não
  só depois de 6 s. A regra antiga de "qualquer tecla" não olhava toque e
  congelava com o mouse sobre o painel.

## 6. O botão "Campanha" sumia por ordem de eventos

No fim da aula o `Panel_vitoria` não tinha botão de voltar. O painel não estava
defasado: `PanelVitoriaController` monta os botões no `OnEnable` perguntando
`PodeVoltarParaCampanha`, e quem arma a volta é o `OnMatchConcluded`. Na partida
normal o aviso vem antes do painel; nas duas saídas de tutorial vinha **depois**.
Invertido nas duas.

O painel ganhou a barra de 6 s do panel_helper e volta sozinho à Campanha. Vale
para a campanha oficial também — sem estatísticas no MVP, 6 s bastam.

## 7. Campanha: mundo, cadeado e fundo

- **Música do mundo** na seleção de mapa (`MundoData.musicaSelecao`); a batalha
  segue nas trilhas dos times, por decisão do autor.
- **Destrave**: quadrante com `destravadoPor` fica sob cadeado até ser conquistado
  por um humano.
- **Fundo assado**: o resto do mapa da campanha pintado escurecido sob os
  quadrantes.
- **Bake** leva `nomeAutorado` e `oculta` (bandeira de spawn de aula não aparece no
  mosaico nem em campo).

## O que não terminou

- **Nada disto foi jogado de novo depois das últimas mudanças** de roteiro:
  contador de captura, gatilho interno, fala de vitória com espera, barra de 6 s.
  Compila; o comportamento é do autor provar.
- **Aula 2 não foi jogada.** O roteiro existe (`docs/tutorial/caserna_soldado_2.json`)
  e ganhou `defeatText`, mas não passou pela mesma bateria de atalhos.
- **O rebelde fora da tela é intencional** — o autor quer que o aluno aprenda a
  olhar. Se ele atirar sem nunca ter entrado no enquadramento, a derrota vai
  parecer injusta; vale um `pan` no turno inimigo.
- **Save no meio da aula não guarda o passo do roteiro**, e um save da Academia
  carregado pelo menu abre no mundo Fixture. Herdados da v9.3.0.
- **O aluno é sempre o slot 0** nas checagens de tarefa.
- **Contador de desembarque** memoriza o total na primeira vez que a lista
  desenha; com a lista escondida (F6) e um desembarque antes, o total sai menor.
- `Fixture`: eixo do slot 0 sem o setor 0 — mudança do autor, não atribuída.
