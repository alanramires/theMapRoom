# v9.4.0 — Revisão do Motor

Dia sem tutorial. Um consultor externo leu a `main` em `3da75fd` e entregou dois
textos: um reconhecimento de arquitetura
(`docs/arquitetura/The-Map-Room-reconhecimento-arquitetural.md`) e uma revisão da
máquina de estados (`docs/arquitetura/revisao_maquina_de_estados.md`). A análise
foi estática — ele não rodou a Unity. Então a regra do dia foi **conferir cada
afirmação no código antes de aceitar**, e várias mudaram de tamanho no caminho.

O fio do dia: **o autor acreditava que a fórmula de combate era fonte única, com
cada consumidor chamando conforme a necessidade. Não era.** A conta tinha duas
cópias, e o DPQ que entra nela tinha quatro.

---

## 1. Conferir antes de aceitar

| achado do consultor | conferido | veredito |
|---|---|---|
| #3 desembarque revela o nome do bloqueador oculto | o texto só chega ao `LogScannerPanel` (debug) | **menor** — e a opção que some é vazamento aceito (ver §5) |
| #4 IA trata distância 0 como 1 | só no caminho legado; o caminho canônico nem recebe distância | **menor** do que parecia |
| #6 esperas da IA sem prazo | `WaitUntil` sem prazo nas fases 0/1/4 | **confirmado** → watchdog (§4) |
| #8 `FindObjectsByType` por consulta | existe, mas todo caminho da névoa passa `useOccupantLayerForTarget: false` e nunca chega lá | **fora do caminho quente** |
| #9 emersão duplicada (`emergeAfterAttackTurns` × `const 2`) | são duas regras: o atirador se revela, o alvo é forçado a emergir | **errado** — era uma constante sem dono (§4) |
| previsão × execução de combate divergem | a fórmula era igual; as **entradas** divergiam | **confirmado, por outro motivo** (§2) |
| máquina: Neutral anunciado antes do recálculo | `ExecuteAndReset` dispara o evento e só depois aplica o delta de FOW | **confirmado** (§3) |
| máquina: `IsResetOnlyState` não cobre as execuções | faltam 6; mas 3 delas saem por `Retreat` de propósito | **meio certo** (§3) |

⚠️ **Hipótese minha que o código desmentiu:** no primeiro relatório ao autor eu
disse que o alcance 0 da fragata quebrava a IA. O caminho canônico
(`SimulateWithWeapons`) usa a arma da opção do `PodeMirar` e não recebe distância
— a fragata funciona ali. O `Max(1, dist)` só afetava o caminho legado. Ler a
linha não basta; é preciso seguir quem chama.

## 2. Combate: a conta e as entradas viraram fonte única

Três commits, na ordem em que a verdade apareceu.

**DPQ (`6c3194c`).** Havia quatro resoluções do DPQ de combate:

| quem | o que fazia de diferente |
|---|---|
| execução (`TurnStateManager.Combat`) | a regra verdadeira: DPQ de camada para **Ar e submerso**; construção/estrutura/terreno só se aceitarem a camada |
| previsão da IA (`PositionDpqResolver`) | camada só para Ar; nenhum filtro de camada |
| `GetCellDpqPoints` (IA preferindo terreno defensivo) | terceira cópia, camada só no terreno |
| movimento contextual do jogador | o mesmo resolvedor da IA |

A regra da execução foi movida **literal** para o `PositionDpqResolver`, e todos
leem dele. A execução não mudou; a IA parou de divergir. O cache da IA virou
célula + camada. O painel de inspeção de terreno ficou de fora de propósito: ele
mostra o DPQ do *lugar*, sem unidade.

O autor lembrou que submerso e mar têm hoje o mesmo DPQ — a diferença é
irrisória. Mas se um dia mudar, já está coberto.

**A fórmula (`c2f6ba7`).** `CombatFormula.Resolve` é pura: não gasta munição, não
marca disparo, não aplica dano. Devolve todos os termos intermediários, para o
trace `[Combate] Resolve` continuar idêntico — e continuou: o autor colou dois
combates (Soldado × Soldado com revide, Katiusha × Soldado sem revide) e os números
bateram com a conta à mão. Quem decide se há revide continua sendo o chamador. O
`CombatModifierResolver` ganhou uma versão por ficha com texto opcional, porque a
IA chama isso em volume e não pode alocar string por simulação.

**A IA só simula o que o sensor oferece (`d280832`).** O achado conceitual do dia
veio do autor: *a calculadora por ficha foi feita para balancear fora da
gameplay* — planície, munição cheia, arma pela ficha. Ela está certa para isso. O
bug era **código de jogo pegando a calculadora emprestada**:

- **D3** — o defensor do capturador e a nota de ataque da automação simulavam pela
  ficha. Agora usam a arma e o revide da opção do `PodeMirar` e o DPQ real.
- **D4** — quando o sensor dizia "daqui não dá", o `AIController` simulava assim
  mesmo pelo atalho. Desligar o fallback não bastava: sem opção, o serviço caía em
  `simInvalid`, que **permite** — afrouxaria uns 40 portões de ataque. Daí o status
  novo `BlockedNoSensorOption`, que bloqueia.
- O `AICombatHpSimulator` agora diz no cabeçalho qual porta é jogo
  (`SimulateWithWeapons`) e qual é calculadora (`Simulate(ficha…)`).

A frase que organiza: **em jogo, só se simula o que o sensor ofereceu; se o sensor
não ofereceu, não há ataque.**

## 3. Máquina de estados: o Neutral tem dois momentos (`194f2c4`)

`ExecuteAndReset` dispara `OnCursorReturnedToNeutral` e, **na última linha**,
aplica o delta confirmado da névoa. "Voltou a Neutral" chega antes de "o mundo
recalculou". Hoje não dá bug: os dois listeners que leem névoa
(`HexCohabitationVisualManager`, `ConstructionManager`) escutam também
`OnFogOfWarUpdated`.

**Por que não inverter a ordem:** o `ConfirmedOccupancyIndex` reconcilia *dentro*
do evento de Neutral, e a ocupação alimenta `UnitOccupancyRules`. O delta da névoa
pode depender do índice já reconciliado. A ordem atual pode estar segurando algo.

O que entrou foi **dar nome ao segundo momento**: `CursorController.OnBoardSettledAtNeutral`,
depois do delta, no `ExecuteAndReset` e na volta por `Retreat`. Na volta por
`Retreat`, delta pendente (não deveria haver) é aplicado na hora com warning — antes
ficava esperando o próximo fim de ação. O CLAUDE.md ganhou a subseção "O Neutral
tem dois momentos".

`IsResetOnlyState` passou a cobrir `Capturando`, `Fundindo` e `AttackingExecuting`.
Os outros três ficaram de fora porque **saem por `Retreat` de propósito** — e esse é
o achado que sobra para o autor (ver "O que não terminou").

## 4. Pequenos

- **Watchdog (`76da76a`).** As esperas das fases 0, 1 e 4 e do estágio de debug
  avisam `[Watchdog]` a cada 20 s parado, com cursor, Serviço do Comando, replay e
  efeitos de início de turno. **Só avisa**: confirmar ou cancelar por tempo furaria
  o contrato transacional. O relógio para em pausa; a espera solta no fim da partida.
- **Emersão forçada (`66c6b39`).** O `const int forcedTurns = 2` virou
  `WeaponData.forcedEmergeTurns`, seguindo a regra irmã
  (`forceOpponentToGoToDomainAfterHit` já carrega `turns`). Padrão 2: balanceamento
  intacto.

## 5. Decisões de design registradas

- **Desembarque sobre inimigo oculto:** a opção simplesmente some. É vazamento
  **aceito** — o jogo não tem emboscada estilo AWBW, e isso segura a regra de
  multi-ocupação por camada. Proibido continua sendo o *nome* do bloqueador chegar
  à UI do jogador.
- **Fragata com alcance 0:** é experimento ("ataque sem revide por diferença de
  domínio"). Provavelmente volta a 1–2, porque alcance 0 abre "então por que o
  bombardeiro não ataca a artilharia de cima?". Decisão adiada para a review da
  Marinha.

## 6. Trabalho do autor que entrou junto

Feito em paralelo, antes da revisão, e comitado como frentes próprias.

- **Aula perdida:** o oponente do aluno é registrado como vencedor ("venceu:
  inimigo" em vez de "Nenhum"), com o motivo "aula perdida". Mas **perder ao refazer
  não desfaz uma aula já vencida por humano** — senão trancaria de novo as aulas que
  ela destravou (`QuadranteController`, `MatchController.ResolveTutorialOpponentSlot`).
- **Tutorial:** a aula 1 trocou a trava de captura por consequência (capturar cedo
  acorda a guarnição na hora); a aula 2 ficou só no reabastecimento, e a extração
  foi para o rascunho da aula 3 (`caserna_soldado_3_rascunho.json`). A `sintaxe.md`
  ganhou a seção de receitas, e entrou um texto de pesquisa sobre design de tutorial
  (`design_tutorial.md`, convertido para UTF-8).

---

## O que não terminou

- **Os 7 `Retreat` que saem de estados `*Executing`** (suprimento, desembarque,
  embarque). "Executing" nem sempre é ponto sem volta: o "embark failed" acontece
  *depois* da animação e devolve o combustível na mão. Cada um precisa desfazer
  tudo que a execução já tocou. É teste de gameplay — o autor ficou com ele. Sinais:
  `[FSM] Delta confirmado pendente…` e `Retreat chamado a partir de estado reset-only`.
- **Cena de caos** (caças, helicópteros, soldados, navios, subs) para ver a IA
  cruzar todos os pares e contar `noSensorOption`. Não rodada.
- **Teste automático da fórmula.** Agora é possível (`CombatFormula.Resolve` com
  fichas reais, sem cena); os dois traces do dia são o gabarito. Não escrito.
- **Listeners antigos** (`HexCohabitationVisualManager`, `ConstructionManager`)
  continuam no `OnFogOfWarUpdated`; não foram migrados para `OnBoardSettledAtNeutral`.
- **O caminho legado do `CombatEvaluationService`** (`AllowLegacyAutomaticWeaponFallback`)
  ainda existe, sem ninguém que o ligue.
- **Itens do consultor não tocados:** equivalência de save/load por hash,
  determinismo entre máquinas, pesos 3/2/1 implícitos do `ServiceData` (o tooltip
  já os documenta).

## Armadilha do dia

Comitar só o meu trecho de um arquivo com mudanças do autor usando
`git apply --cached --unidiff-zero`: sem contexto, duas linhas caíram no lugar
errado (um `return true;` dentro de um `for`) e entraram no commit. O `compilar.sh`
não pega isso — ele compila o disco, não o índice. Consertado com amend, porque não
havia push. O jeito certo: reconstruir o blob a partir do `HEAD` com a substituição
exata e conferir o diff staged **antes** de comitar.
