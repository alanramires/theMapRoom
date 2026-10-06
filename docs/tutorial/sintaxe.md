# Sintaxe do roteiro de tutorial

Referência de tudo que um `TutorialData` entende: as **tarefas** (objectives), as
**falas** do roteiro e os **comandos** que elas disparam. Conferido no código em
2026-10-05 (`TutorialManager.cs`, `TutorialData.cs`, `AutomataData.cs`,
`PanelDialogTutorialController.cs`). Se algo aqui divergir do jogo, quem manda é o
código; corrija este arquivo.

Vários comandos do roteiro são os mesmos gestos dos atalhos de debug. O `wake`, por
exemplo, é o "wake unit", e o `ammo=` usa o mesmo caminho do "set ammo". O que se
testa no debug costuma funcionar igual no roteiro.

---

## 1. Coordenadas: o que `x,y` quer dizer

Todo `x,y` do roteiro (spawn, move, wake, pan, cursor, show/hide, `UNIT_AT_HEX`,
`UNIT_SELECTED`, `CAMERA_PAN`) e o **hex-alvo do AutomataData** passam por um único
leitor (`TryParseScriptCell`).

| onde a aula roda | o que `x,y` significa |
|---|---|
| **missão da Academia** (quadrante com Aula) | coordenada do **mundo de autoria**, a que você vê na cena. A Batalha converte: `tabuleiro = mundo − canto do quadrante + origem da pintura` |
| cena de tutorial antiga (sem quadrante) | coordenada da própria cena |

- **Ajustar o retângulo** do quadrante (expandir, encolher, mover o canto) **não
  quebra** o roteiro, desde que os hexes que ele usa fiquem dentro.
- **Levar o desenho** da aula para outro lugar do mundo **quebra**: o roteiro
  continua apontando para o lugar antigo.
- Grid hexagonal odd-r: **não existe** coordenada relativa confiável. `+1` em x
  numa linha par e numa ímpar vai para lados diferentes. Use sempre o hex exato.

## 2. Tokens: como o roteiro acha uma unidade ou construção

**Unidade** (`UnitMatchesTargetToken`): casa, sem diferenciar maiúscula, com
qualquer um destes:
- o id ou o nome de exibição da instância;
- o nome do GameObject (por *contém*);
- o `id`, o `displayName` ou o **apelido** do `UnitData`;
- o nome do asset (por *contém*).

O nome dado no spawn (`name=Ryan`) entra no nome da unidade, então `Ryan` funciona
como token.

Apelidos do catálogo (os mais úteis):

| apelido | unidade | | apelido | unidade |
|---|---|---|---|---|
| `SD` | Soldado | | `TB` | Tanque Leve |
| `BZ` | Bazooka | | `TA` | Tanque de Batalha |
| `MG` | Metranca | | `TZ` | Tanque Pesado |
| `JP` | Jipe | | `OL` | Obus Leve |
| `APC` | APC | | `OM` | Obuseiro Móvel |
| `ST` | Suprimentos | | `AC` | Artilharia de Campanha |
| `18w` | Caminhão de Carga | | `A2` | Lança Foguetes |
| `RM` | Radar Móvel | | `AAA` / `SAM` | antiaéreos |
| `CH` | Chinook | | `AP` | Apache |

**Construção** (`FindConstructionByName`): casa pelo nome do GameObject, por
*contém*. O nome do GameObject vem do **nome de exibição autorado**
(`Bandeira` → `Bandeira_T-1_C5`). Para uma construção ser achada pelo roteiro numa
missão da Academia:
1. dê o nome dela na cena de autoria (nome de exibição do `ConstructionManager`);
2. **refaça o bake**: o nome viaja no campo `nomeAutorado` do bake.

## 3. Tarefas (objectives)

Cada tarefa tem:

| campo | para que serve |
|---|---|
| `id` | o **tipo de evento** que completa a tarefa (tabela abaixo) |
| `key` | identidade única da tarefa (ex.: `sold_1_02`). É por ela que as falas esperam e revelam |
| `parameters` | filtro do evento (por tipo, abaixo). Também aceita o prefixo `spawn:` |
| `description` | texto na lista de tarefas, e na derrota se for condição de derrota |
| `startHidden` | começa oculta; uma fala revela |
| `isOptional` | não conta para a vitória |
| `isDefeatCondition` | **inverte**: o evento acontecer é **derrota** |

**Vitória** = todas as tarefas não opcionais e não de derrota completas.
**Derrota** = qualquer tarefa `isDefeatCondition` acontecer.

**Numa aula, só as tarefas decidem o fim.** As regras de fim de partida (QG
capturado, exército eliminado, estrelas de vitória) ficam desligadas
(`MatchController.UsesMatchEndRules`). Rendição vira derrota da aula. "Ficar sem
unidades" só derrota se a aula tiver a tarefa `PLAYER_ELIMINATED`.

Uma tarefa só completa estando **visível** e **pendente**. Quando um evento casa
com várias do mesmo tipo, os tipos sem parâmetro (como `ATTACK_UNIT`) completam
**só a primeira** pendente da lista.

### Tipos de evento

| `id` | completa quando | `parameters` |
|---|---|---|
| `UNIT_AT_HEX` | uma unidade **do slot 0** terminou a ação (agiu, sem estar embarcada) num dos hexes | expressão de hex (ver abaixo) |
| `UNIT_SELECTED` | uma unidade é selecionada num dos hexes | expressão de hex |
| `HOLD_POSITION` | o jogador (slot 0) mantém posição | vazio = qualquer; token ou expressão de hex |
| `ATTACK_UNIT` | qualquer ataque é resolvido | — |
| `ATTACK_UNIT_MOUNTAIN` | ataque com o atacante em terreno cujo id contém `mountain`/`montanha` | — |
| `ATTACK_UNIT_PLAINS` | idem, `plain`/`planicie`/`grass` | — |
| `DESTROY_ENEMY_UNIT` | uma unidade de **outro slot que não o da vez** é destruída | — |
| `UNIT_DEAD` / `DEAD_UNIT` | morre uma unidade que casa com o token | `TOKEN` ou `TOKEN \|\| TOKEN2` |
| `UNIT_DEAD` com autonomia | a unidade fica com combustível `<= X` (checado ao mover e no início do turno) | `TOKEN \|\| AUT=X` |
| `HAS_EMBARKED_UNIT` | alguém embarca num transporte que casa com o token | token do **transporte** (ex.: `APC`) |
| `SUPPLY_UNIT` | um suprimento é feito | vazio = qualquer; token do **supridor ou do alvo** |
| `USED_ROAD_BOOST` | uma unidade do slot 0 terminou a ação e o último movimento usou estrada | vazio = qualquer; token |
| `CAPTURE_CONSTRUCTION` | o jogador (slot 0) **termina** uma captura: o prédio muda de dono | vazio = qualquer; `Bandeira`, `60,32`, `Bandeira \|\| 60,32` ou `SD && Bandeira` |
| `CAPTURE_PROGRESS` | o jogador (slot 0) **age capturando**, mesmo sem terminar | idem |
| `ENEMY_CAPTURE` | um slot **que não é o aluno** toma um prédio (muda de dono). Use como derrota | igual ao `CAPTURE_CONSTRUCTION` |
| `PLAYER_ELIMINATED` | o aluno (slot 0) fica **sem nenhuma unidade**, contando as embarcadas. Avaliado só quando uma unidade morre. Use como derrota | — |
| `PURCHASE_UNIT` | qualquer compra | — |
| `INSPECT_ALLY_UNIT` | inspecionar unidade do slot **da vez** | — |
| `INSPECT_ENEMY_UNIT` | inspecionar unidade de **outro** slot | — |
| `FOW_REVEAL_UNIT` | uma unidade sai da névoa | — |
| `END_TURN` | o jogador passa a vez | — |
| `CAMERA_PAN` | a câmera se desloca | vazio = qualquer deslocamento; `x,y` = chegar a até 2,5 hexes daquele ponto |
| `CAMERA_ZOOM` | o zoom muda em relação a quando a tarefa ficou ativa | — |
| qualquer outro (ex.: `ENDING`) | **nenhum evento**. Só completa pelo comando `complete <key>` | — |

### Expressão de hex (`UNIT_AT_HEX`, `UNIT_SELECTED`, `HOLD_POSITION`)

```text
SD && (23,1 || 24,1)      Soldado num desses hexes                 ← formato preferido
SD && Bandeira            Soldado no hex da construção "Bandeira"
SD && (Bandeira || 5,4)   misturar nome e hex vale
23,1 || 24,1              qualquer unidade nesses hexes
SD || 23,1 || 24,1        (antigo) token global + lista
SD 23,1 || APC 24,1       (antigo) token por hex
```

### Prefixo `spawn:` nos parâmetros

`spawn:slot1 SD 7,2` executa o spawn **uma vez**, quando a tarefa fica visível e
pendente. É a mesma sintaxe do `spawnCommand`.

## 4. Falas (o roteiro)

O roteiro é a lista `script`, em ordem. Cada fala:

| campo | para que serve |
|---|---|
| `text` | o que o Sargento diz. **Vazio = fala muda**: executa os comandos sem abrir balão (direção de cena) |
| `voice` | áudio gravado da fala (opcional) |
| `advance` | **quando esta fala dá lugar à próxima** (abaixo) |
| `objectiveKey` | tarefa que o `advance = Objective Completed` espera e/ou que esta fala revela |
| `revealObjective` | revela a tarefa `objectiveKey` quando a fala aparece |
| `spawnCommand` | spawns ao aparecer (uma vez) |
| `statCommand` | comandos de cena ao aparecer (uma vez) |
| `turn` | trava ou libera o **passar a vez** daqui em diante |
| `movement` | estado do **movimento** do jogador daqui em diante |
| `interactionType` | só para o linter (ritmo). `Auto` infere |

### `advance`

| valor | avança quando |
|---|---|
| `Immediate` | o jogador confirma o balão |
| `Objective Completed` | a tarefa `objectiveKey` completa |
| `All Units Acted` | todas as unidades do jogador agiram |
| `Player Turn Started` | começa um novo turno do jogador (depois do inimigo jogar) |
| `Enemy Turn Started` | começa o turno inimigo |
| `Aim Opened (Mirar)` | o jogador abre a mira |

### `turn` (passar a vez)

`No Effect` mantém · `Locked` trava · `Unlocked` libera.

### `movement`

| valor | o jogador pode |
|---|---|
| `No Effect` | (mantém o estado anterior) |
| `Locked` | nada: nem mover, nem manter posição |
| `Hold Only` | manter posição e atacar parado; sair do hex, não |
| `Attack Only` | mirar; "finalizar parado" (apenas mover / M), não |
| `Unlocked` | tudo |

Se **qualquer** fala usar `Unlocked`, a aula **começa travada**.
Se **qualquer** fala revelar tarefa, o painel **começa vazio** e só mostra o que for
revelado. Sem nenhuma revelação, todas as tarefas não ocultas aparecem.

### Marcação no texto

| tag | efeito | uso |
|---|---|---|
| `[ordem]...[/ordem]` | amarelo, negrito | o que **fazer** |
| `[enfase]...[/enfase]` | laranja | o conceito a **fixar** |
| `[azul]`, `[amarelo]`, `[vermelho]` | só cor | realçar elemento da tela |

O linter avisa tag desconhecida ou sem fechar.

## 5. Comandos

Vários comandos no mesmo campo: separe por `;`.

### `spawnCommand`

```text
slotN SIGLA x,y [opções]
  slot0 SD 60,32                      soldado do aluno
  1 SD 63,33                          (sem "slot" também vale)
opções:
  acted                               nasce já tendo agido
  name=Recruta_Ryan                   nome (_ vira espaço) — vira token
  cursor                              leva o cursor até ela
ex.: slot0 SD 60,32 name=Ryan cursor; slot1 SD 64,33 acted
```

Prefira `slotN` a número de time: a cor do jogador é escolhida no menu.

**Hex ocupado não recebe spawn.** Se já houver unidade viva **no chão ou no mar**
no hex, o comando não executa (registra no log). **Aeronave no ar não bloqueia.**
É regra de design: quem guarda o ponto de spawn impede o reforço de nascer ali.

**Spawn por bandeira** (no lugar do `x,y`):

```text
slot1 SD @flag                       sorteia uma bandeira LIVRE cujo nome contém "flag"
slot1 SD @flag perto=Porto_Ferro     a bandeira livre mais próxima do alvo (x,y ou construção)
slot1 SD @flag; slot1 SD @flag       dois soldados: o 2º pega outra, porque a 1ª ficou ocupada
```

- Bandeira = qualquer construção cujo nome contém o token (`flag#1`, `flag#2`...).
  Use uma construção **não capturável** e desligue `isVisible` para escondê-la.
- Sem `perto=`, é sorteio: com mais bandeiras do que soldados, cada partida sai
  diferente.
- Nenhuma bandeira livre = ninguém nasce.

**Unidade embarcada desde o início:** embarque-a no transporte **na cena de
autoria** e refaça o bake. O bake guarda o passageiro preso ao transporte, e a
Batalha embarca de novo ao montar o quadrante.

### `statCommand`

| comando | efeito |
|---|---|
| `Ryan hp=5` · `SD fuel=2` · `APC ammo=0` | ajusta HP, combustível (`fuel`/`autonomia`) ou munição (`ammo`/`municao`) |
| `wake 60,32` · `wake SD 60,32` · `wake Ryan` | reativa a unidade (limpa "já agiu") |
| `complete sold_1_04` | completa a tarefa por key. Se for a última, **vence a aula** |
| `show Bandeira` · `hide 5,4` | mostra/oculta uma construção |
| `pan Bandeira` · `pan Ryan` · `pan 60,32` | desliza **só a câmera** |
| `cursor Ryan` · `cursor 60,32` | move **só o cursor**, sem selecionar |
| `zoom 1.5` (ou `1,5`) | zoom exato (orthographicSize) |
| `slotN TOKEN move DE [PARA] [attack]` | movimento scriptado (abaixo) |

### Movimento scriptado

```text
slot1 SD move 64,33 61,33            anda de 64,33 até 61,33
slot1 SD move 64,33 61,33 attack     anda e ataca ao chegar
slot1 SD move 64,33                  sem destino: avança pelo AutomataData
```

- Usa o caminho e o alcance **reais** (terreno, ocupação). Destino fora do alcance
  do turno não é alcançado.
- **Exige** um `AutomataData` para a unidade e o **Automata Database ligado no
  TutorialManager** da cena.
- Enquanto o movimento roda, o passar a vez fica travado.

## 6. Configuração geral do `TutorialData`

| campo | efeito |
|---|---|
| `alwaysActedUnits` | tokens (`;`) que amanhecem "já agiram" em todo turno do jogador: figurantes |
| `blockCommandService` | trava o Serviço do Comando (X) |
| `blockRemoveUnit` | trava dispensar/destruir (U) |
| `blockSurrender` | trava render-se |
| `blockStatusSummary` | trava a Situação do menu |
| broncas (`scold...`) | texto e voz do Sargento ao tentar ação travada. Vazio = texto padrão |
| `victoryDialog` | mensagem da vitória. Vazio = "TREINAMENTO CONCLUÍDO" |
| `inimigo` | quem joga pelos slots ≠ 0: **Automata** (roteirizado) ou **IA de verdade** (perfil do contrato; facção sem QG no quadrante = rebelde, puxada pelo magnético do capitão) |
| `forcarRegras` + `regras` | a aula impõe o preset de regras (ex.: `A Montanha Avacalha` = tudo ligado exceto névoa), ignorando o menu |

## 7. Inimigo roteirizado (`AutomataData`)

Dirige as unidades de **todo slot que não é o 0**, no turno delas, durante uma aula.
Fora de aula ele não age.

| campo | efeito |
|---|---|
| `unitToken` | unidade que esta regra controla (token, ex.: `SD`) |
| `teamId` | time que usa a regra |
| `stationary` | guarnição: nunca se move; só ataca o que estiver no alcance |
| `preferAttack` | tenta atacar antes de mover |
| `targetPreference` | com vários alvos: ordem de spawn, menor HP, maior HP, aleatório |
| `fallbackMove` | sem ataque possível, finaliza parado (M) |
| `moveTowardsTarget` + `moveTargetCell` | avança a cada turno até o hex (no **mesmo referencial do roteiro**) |
| `stopDistance` | para quando estiver a esta distância do alvo (1 = adjacente) |
| `tutorials` | vazio = vale para qualquer aula; preenchido = só para estas |

## 8. Convenções e limites atuais

- **O aluno é o slot 0.** O roteiro, as travas, `UNIT_AT_HEX` e o automata assumem
  isso.
- **Quem joga pelo inimigo é escolha da aula** (`inimigo`). Com **Automata**, a IA
  não joga e o automata move e passa a vez; unidade inimiga sem `AutomataData` fica
  parada. Com **IA de verdade**, o automata fica de fora e a IA joga com o perfil do
  contrato. Nos dois casos o aluno fica travado no turno inimigo.
- **Não salve no meio da aula.** O save não guarda em que fala o roteiro estava;
  carregar recomeça o roteiro e repete os spawns.
- A vitória da aula registra o quadrante na Campanha com o motivo "aula concluída"
  e volta para o mapa. A derrota volta sem registrar.
- `TutorialRules` tem uma regra presa ao id `tutorial 1 - soldado` (restaura o HP
  após atacar na montanha ou na planície). É legado. Não use esse id numa aula nova
  sem querer essa regra.
