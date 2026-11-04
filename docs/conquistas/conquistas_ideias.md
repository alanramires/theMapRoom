# Catálogo de medalhas — ideias

12/10/2026. As condições; o que a FSM precisa registrar para cada uma está em
`sensores.md` (a coluna "fatos" aponta para a tabela de lá).

✅ = o `JogadasLog` já grava o necessário · ❌ n = falta o fato n de `sensores.md`

## Combate

| medalha | condição | fatos |
|---|---|---|
| **Tiro no Escuro** | destruir uma unidade que não via o atacante | ✅ `attackerVisibleToDefender` |
| **Fogo Indireto** | destruir uma unidade com artilharia | ✅ `attackTrajectory` |
| **Martelo e Bigorna** | duas unidades suas atacam o mesmo alvo no turno, e a segunda destrói | ✅ |
| **Quem Tem o Morro** | vencer um combate atacando de terreno com mais DPQ que o defensor | ✅ `dpqAtacante/dpqDefensor` |
| **Eles Nunca Viram de Onde Veio** | 3 destruições pela névoa numa partida | ✅ |
| **Fantasma** | atacar com unidade furtiva e terminar a partida sem ela ter sido detectada | ❌ 7 |

## Logística

| medalha | condição | fatos |
|---|---|---|
| **Hospital de Campanha** | curar 10 de HP com supridor numa partida | ✅ `serviceHpGain` |
| **Ninguém Fica para Trás** | embarcar um ferido (HP ≤ 3) e ele terminar a partida vivo | ✅ |
| **Mais Unidos que Nunca** | fundir duas unidades com HP ≤ 3 | ✅ `hpAntes/hp2Antes` |
| **Pouso Forçado** | sua aeronave faz pouso de emergência e volta a voar | ✅ |
| **Logística Perfeita** | vencer sem nenhuma unidade sua zerar combustível ou munição | ⚠️ só o combustível de aeronave no início de turno; falta munição e combustível gasto andando |
| **Pane Seca** | uma aeronave inimiga cai por falta de combustível | ✅ |

## Manobra e conquista

| medalha | condição | fatos |
|---|---|---|
| **Cabeça de Ponte** | desembarcar e capturar com a mesma unidade no turno seguinte | ✅ |
| **Blitz** | capturar o QG inimigo | ✅ `predioQG` |
| **Economia de Guerra** | vencer gastando menos que o inimigo | ✅ `valor` |

## Por unidade

O **uso característico** de cada unidade. Cada uma é também candidata a aula: a aula
ensina a fazer; a medalha reconhece quando o jogador repete sozinho, numa partida.

| medalha | condição | fatos |
|---|---|---|
| **Pouso no Convés** | pousar um bombardeiro no porta-aviões | ❌ 10 |
| **Silêncio no Fundo** | afundar um navio com o submarino sem ser detectado | ❌ 7 |

## Por grupamento

Armas combinadas e composição do exército. Medalha de **estado** (o que o exército
**tinha** num momento), não de evento — por isso depende da foto do fato 10.

| medalha | condição | fatos |
|---|---|---|
| **Somos a Ponta da Espada** | ter 2 bombardeiros embarcados no mesmo porta-aviões | ❌ 10 |
| **USS Nimitz, a Volta do Inferno** | ter em campo, ao mesmo tempo, porta-aviões, destroyer, submarino e fragata | ❌ 10 |
| **Esquadrilha da Fumaça** | comprar 3 Supertucanos numa partida | ✅ soma das `Compra` |

## Academia e carreira

| medalha | condição | fatos |
|---|---|---|
| **Formado na Caserna** | concluir todas as aulas de uma patente | ✅ `CampaignProgressStore` |
| **Sem a Mão do Sargento** | vencer a prova de uma patente (a aula sem orientação) | ✅ (quando a prova existir) |
| **Veterano** | vencer 10 partidas | ❌ carreira |

Todas as condições são de partida e avaliadas no fim dela — ver "A decisão que vem
antes da FSM" em `sensores.md`.
