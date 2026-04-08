# AI Unit Profiles

---

## Sistema de Planejamento

O planejamento da IA ocorre **uma vez por turno**, antes do loop de movimentação das unidades. O componente responsável é o `AIPlanEvaluator` (stateless) chamado por `EvaluatePlanner()` no `AIPlayerController`.

---

### Fluxo geral por turno
inicio do turno
- EvaluatePlanner()
- TryActivateInvasionPlan() quando a postura for Invasao
- SelectActiveSectorPlans()
- AssignSectorPlanInfantryAcrossActivePlans()
- AssignSectorPlanSupportForcesAcrossActivePlans()
- ApplyMissionPersistenceAndReallocation()
- loop de unidades por initiative efetiva + HP
- snapshot por unidade
- cada unidade executa seu papel conforme AIPlanAssignment
FoW durante o turno da IA:
- o snapshot de decisao nao dispara RefreshFogOfWarForActiveTeam() antes de cada unidade
- a IA decide com base no estado observado pelo sensor/time
- o refresh visual/global de FoW fica fora desse loop interno
---

### Quais setores capturar? (`maxVariablePlans`)

O planner avalia **todos os setores** conhecidos e descarta:
- Setores completamente controlados pela IA (sem construcoes a capturar)
- Setores de base (tratados separadamente pelo plano de invasao)

Os candidatos restantes sao pontuados e ordenados por:

| Criterio | Direcao | Peso |
|---|---|---|
| Distancia ao proprio HQ | Menor = melhor | 1o |
| Construcoes nao capturadas | Mais = melhor | 2o |
| Pressao inimiga no setor | Mais = melhor | 3o |
| Possui HQ inimigo | Sim = melhor | 4o |

Os `maxVariablePlans` setores mais bem rankeados sao ativados como planos do turno. **Padrao: 3.** Configuravel em `AIGeneralProfile.maxVariablePlans`.

> **Postura Invasao:** ativa primeiro um plano especial de invasao da melhor base inimiga. Esse plano consome um slot de `maxVariablePlans`.

---

### Quantas unidades por papel? (`ComputePlannedForce`)

Para cada setor ativo, o planner calcula uma **forca planejada por capability**, nao mais por tipo hardcoded de unidade.

As demandas calculadas sao:
- `Capture`
- `Escort`
- `FireSupport`
- `Logistics`

A heuristica atual funciona assim:

| Situacao | Efeito na forca planejada |
|---|---|
| Cada construcao nao capturada | +1 `Capture` |
| Setor distante do HQ (`DistToOwnHq >= 8`) | +1 `Capture`, +1 `Escort` |
| Pressao inimiga `>= 2` | +1 `Capture` base, mais extras conforme a pressao; +`Escort`; +`FireSupport` |
| Setor com HQ inimigo ou a ate 6 hexes dele | +1 `Capture`, +1 `Escort`, +1 `FireSupport` |
| Multiplos HQs inimigos proximos (`EnemyHqNearbyCount >= 2` ou `EnemyHqThreatSum >= 10`) | +1 `Capture`, +1 `Escort`, +1 `FireSupport`, +1 `Logistics` |
| Operacao longa (`Capture >= 3` e setor distante ou com muitas construcoes) | +1 `Logistics` |
| Pressao inimiga muito alta (`>= 3`) | +1 `Logistics` |

Resumo pratico da formula atual:
- `Capture`: cresce com quantidade de objetivos, distancia, pressao e proximidade de HQ inimigo.
- `Escort`: cresce para acompanhar a captura quando o setor e grande, distante ou contestado.
- `FireSupport`: entra quando o setor pede apoio ofensivo mais pesado.
- `Logistics`: entra quando a operacao fica longa ou muito pressionada.

**Como a alocacao acontece:**

| `AIPlanRole` | Capability exigida | Como o planner aloca |
|---|---|---|
| `Capture` | `AIPlanCapability.Capture` | `MinCostMaxFlow`, minimizando a soma das distancias para os alvos de captura |
| `Escort` | `AIPlanCapability.Escort` | Alocacao em waves, sempre pegando a unidade livre mais proxima do alvo do plano |
| `Artillery` | `AIPlanCapability.FireSupport` | Mesmo metodo greedy por proximidade, mas em wave separada de `Escort` |
| `Support` | `AIPlanCapability.Logistics` | Mesmo metodo greedy por proximidade, em wave propria |
| `Assault` | `AIPlanCapability.Assault` | Nao entra no `ComputePlannedForce`; fica como papel disponivel para unidades ofensivas fora dessa composicao de captura |

**Ordem real de alocacao dentro do plano:**
1. O planner escolhe os setores ativos.
2. Calcula a `PlannedForce` de cada setor.
3. Aloca `Capture` primeiro via `MinCostMaxFlow`.
4. So depois aloca suporte por waves separadas nesta ordem:
   - `Artillery` (`FireSupport`)
   - `Support` (`Logistics`)
   - `Escort` (`Escort`)
5. Se um plano nao conseguir ao menos um `Capture`, ele nao entra como plano ativo final.

> **Ponto importante:** `ComputePlannedForce()` nao pensa mais em `APC`, `ArmoredEscort` ou tipos fixos. Ele pede funcao tatica, e quem pode cumprir cada funcao vem de `planCapabilities` no `AIUnitProfile`.

---

### Ciclo de vida dos planos

Cada plano tem um estado no catalogo interno (`PlannerCatalogStatus`):

| Estado | Condicao |
|---|---|
| **Inactive** | Setor nao selecionado no turno atual (sem slots disponiveis ou nao priorizado) |
| **Active** | Selecionado e com pelo menos um capturador designado |
| **Completed** | Setor totalmente controlado pela IA |

**Persistencia entre turnos (`MissionAssignmentMemory`):**
O planner registra ao final de cada turno quais unidades estavam em qual plano e qual era a distancia ao alvo. No proximo turno, ao montar os novos planos, ele tenta **manter as atribuicoes anteriores** antes de redistribuir.

**Estagnacao (`stagnationTurns`):**
Se uma unidade esta no mesmo plano ha N turnos sem progresso, ela fica elegivel para **realocacao**. Padrao: 2 turnos. Configuravel em `AIGeneralProfile.stagnationTurns`.

**Fallback de plano salvo:**
Se uma unidade tinha um plano no turno anterior mas o setor nao foi reselecionado, o planner cria um **plano fantasma** para manter a unidade em missao ate que o setor seja reincorporado ou a unidade seja necessaria em outro lugar.

---

### Parametros configuraveis (`AIGeneralProfile`)

| Campo | Padrão | Efeito |
|---|---|---|
| `maxVariablePlans` | 3 | Máximo de setores ativos por turno |
| `stagnationTurns` | 2 | Turnos sem progresso antes de elegibilidade para realocação |
| `minimumRangeForDefensePlan` | 5 | Raio mínimo para considerar ameaça próxima ao HQ (plano defensivo) |

---

## Planner Capabilities

`planCapabilities` define **para quais papeis o planner pode escalar uma unidade**. Isso e separado do comportamento tatico da unidade durante o turno.

Capabilities atuais:
- `Capture`: unidade elegivel para ocupar slots de captura de setor.
- `Escort`: unidade elegivel para acompanhar planos de captura como escolta de linha.
- `FireSupport`: unidade elegivel para o papel `Artillery` do planner.
- `Logistics`: unidade elegivel para o papel `Support` do planner.
- `Assault`: unidade ofensiva pura. Existe como capability valida, mas **nao entra no `ComputePlannedForce()`** dos planos de captura.

Regra pratica:
- `planCapabilities` responde "em que papel o planner pode me usar?"
- `sensorPriority` e flags como `holdPositionWhenInRange`, `playConservative` e `retreatToHqWhenIdle` respondem "como eu ajo quando chegar a minha vez?"

Se `planCapabilities` estiver vazio, o codigo ainda usa inferencia legada a partir do profile atual. O objetivo do modelo novo e preencher os assets explicitamente e depender menos dessa inferencia.

## Referência de Flags

### Sensor Priority

A ordem dos sensores define o que a unidade tenta fazer a cada turno. O primeiro sensor que encontra um objetivo válido vence — os demais são ignorados naquele turno.

| Sensor | O que faz |
|---|---|
| **Capture** | Procura o prédio capturável mais prioritário no setor e marcha até ele. Com setor planejado: prefere prédios livres sobre ocupados independente da distância. Avança com cautela (DPQ + movimento mínimo) quando o alvo é território inimigo sem visibilidade (FoW) — exceto se um aliado já ocupar o objetivo (FoW não se aplica, avança normalmente). |
| **Attack** | Procura o melhor inimigo para engajar com base nos critérios de Attack Decision e planeja o movimento para atacar. O planejamento ocorre mesmo que a unidade ainda não esteja em alcance de tiro — ela move-se em direção ao alvo e ataca ao chegar. |
| **Supply** | Procura aliados para reabastecer (combustivel, municao, pecas) dentro dos limiares configurados. Primeiro tenta suprir sem mover; se nao houver alvo imediato, navega ate o aliado valido mais proximo. Criticidade so desempata. |
| **Reposition** | Fallback: move para a melhor célula disponível sem objetivo específico. |

> A ordem importa: `Capture > Attack > Reposition` = capturador que só briga se necessário. `Attack > Capture > Reposition` = combatente que captura se não tiver inimigo.

---

### Attack Decision

Critérios aplicados quando o sensor Attack avalia se vale a pena engajar um inimigo.

| Campo | Efeito |
|---|---|
| **Min Damage Dealt %** | Exige que o ataque cause pelo menos X% do HP máximo do alvo. 0 = ignora. |
| **Max Damage Received %** | Recusa engajar se o contra-ataque esperado superar X% do HP máximo do atacante. 0 = ignora. |
| **Must Survive** | Recusa engajar se o atacante não sobreviveria ao contra-ataque. |
| **Target Preference** | `Either` = qualquer alvo. `Primary` = bônus de score para alvo primário. `Secondary` = bônus para alvo secundário. |

---

### Behavior Flags

#### canEscort
`canEscort` **nao e mais a fonte primaria do planner**. A elegibilidade formal para escolta agora vem de `planCapabilities = Escort`.

Hoje essa flag continua relevante para comportamento legado e para a inferencia automatica quando um asset ainda nao foi migrado. Em termos de design, ela deve ser lida como uma pista de comportamento de unidade combatente/escolta, nao como a definicao oficial de papeis do planner.

#### engageNearestEnemies
Flag com três efeitos cumulativos, todos ativados ou desativados juntos:

1. **Sensor Attack**: desabilita o filtro de contexto de escolta — ataca qualquer inimigo visível, não apenas os relevantes para o plano.
2. **Pós-movimento (escolta)**: após mover por coesão, faz rescan e ataca inimigos alcançáveis da nova posição (range 1).
3. **Pós-movimento (captura)**: habilita "morde no caminho" (ataque no corredor de captura) e fallback oportunista para qualquer inimigo alcançável após mover.

> Sem esta flag, unidades de captura ignoram inimigos durante a marcha. Sem esta flag em escolta, a unidade segue o capturador sem brigar no caminho.
>
> **Nota de design:** os três efeitos compartilham uma flag única. Se no futuro for necessário granularidade (ex: efeito 1 sem o efeito 3), a estrutura precisaria de flags separadas por contexto.

#### retreatToHqWhenIdle
"Volta porra!" — Sem sensor ativo nem inimigo para engajar, abandona a coesão do plano e marcha de volta ao HQ aliado. **Tem prioridade sobre planCohesion.** Ideal para tropas de elite que defendem a base e não devem vagar pelo mapa ociosamente. A unidade vai até o hex do HQ, não apenas ao anel de defesa.

#### playConservative
Joga de forma conservadora: move em células seguras próximas a aliados, penaliza células de perigo, ativa patrulha territorial. **Não é sinônimo de Postura de Defesa** — é um modificador de comportamento independente da postura. Desenhado para civis e suprimentos que precisam sobreviver acima de tudo.

#### prioritizeDpqAtBattle
Quando engajando um inimigo, prefere células com cobertura (DPQ) ao calcular movimento. Peso de 40% DPQ vs 60% proximidade do alvo.

#### prioritizeDpqDuringTravel
Mesmo sem inimigo engajado (em marcha, coesão ou reposicionamento), prefere células DPQ. Útil para unidades que nunca devem ficar expostas.

#### requireSightlineBeforeEngaging
Exige linha de tiro da posição **atual** para considerar um alvo válido. Se não puder atirar parado, descarta o alvo e vai para o próximo sensor. Se `repositionToFireRange` também estiver ativo, salva o alvo como âncora de reposicionamento. **Típico de artilharia indireta.**

#### holdPositionWhenInRange
Quando já está em alcance de tiro do alvo, **para de mover e atira da posição atual**. Combatentes sem esta flag continuam avançando até o alcance mínimo.

Efeito adicional em modo reparo: unidades com esta flag **continuam atirando mesmo danificadas**, desde que já estejam na construção de reparo e não tenham movido naquele turno. Combatentes em reparo não revidiam — priorizam chegar à base. Isso reflete o gameplay manual: artilharia danificada não para de atirar, só não sai da posição de reparo.

#### repositionToFireRange
Sem alvo alcançável agora, reposiciona-se em direção ao alvo até entrar em alcance de tiro, em vez de avançar diretamente. Na prática, dado que mover colapsa o alcance para range=1, esta flag faz a unidade **avançar até o alcance e atacar no range 1** (a "porrada"). Como âncora: se `requireSightlineBeforeEngaging` também estiver ativo, o alvo descartado é salvo como destino de reposicionamento.

#### preferMaxEngagementRange
Ao reposicionar durante o engajamento, prefere a **distância máxima** de tiro em vez da mínima. Mantém a artilharia o mais longe possível do inimigo. **Típico de artilharia pura.**

#### captureInterruptBias
Define o limiar de score mínimo para interromper a marcha de captura e atacar um inimigo no corredor ("morde no caminho"). Aplicado tanto no planejamento pré-movimento quanto no fallback pós-movimento. Só tem efeito quando `engageNearestEnemies` está ativo.

| Valor | Score mínimo | Comportamento |
|---|---|---|
| **None** | — | Nunca interrompe a captura por iniciativa própria. Sem "morde no caminho", sem fallback oportunista. Exceção: inimigo **no próprio prédio objetivo** sempre é atacado para desbloqueá-lo. |
| **Passive** | 38.000 | Raramente abandona captura para brigar. Só ataca alvos muito vantajosos no corredor. Sem fallback oportunista pós-movimento. |
| **Normal** | 28.000 | Limiar padrão. Ataca se o score justificar. Fallback oportunista ativo. |
| **Aggressive** | 22.000 | Interrompe captura com facilidade. Ataca quase qualquer inimigo no caminho. |

> Capturador puro deve usar **None** (rush total, só ataca quem bloqueia o objetivo). Bazooka skirmisher deve usar **Aggressive** (oportunista). Padrão é Normal.

#### holdGroundWhenIdle
Sem objetivo ativo (sem inimigo, sem captura, sem supply, sem plano), **ancora na posição atual** em vez de avançar em direção ao HQ inimigo (que é o fallback padrão). Com `prioritizeDpqDuringTravel`, o path planning ainda escolhe a melhor célula DPQ dentro do alcance de movimento — na prática a unidade gravita para o prédio ou cobertura mais próximos e fica lá.

Efeito adicional com `repositionToFireRange`: quando não existe célula em alcance de tiro para a âncora de reposicionamento, a unidade **não avança** em direção ao inimigo — fica parada e aguarda o próximo turno. Sem esta flag, o fallback seria avançar para reduzir a distância. Essencial para artilharia que não deve se expor marchando em direção ao inimigo quando não há posição de tiro viável.

Diferença em relação a `retreatToHqWhenIdle`: em vez de marchar até o HQ aliado, a unidade se entrincheira onde está. Ideal para defesa posicional em que o HQ está longe mas a unidade deve segurar uma linha.

---

### Combinações Notáveis

| Combinação | Arquétipo | Comportamento |
|---|---|---|
| `requireSightlineBeforeEngaging` + `holdPositionWhenInRange` + `repositionToFireRange` + `preferMaxEngagementRange` + `holdGroundWhenIdle` | **Artilheiro puro** | Fica parado se puder atirar à distância. Se não puder, reposiciona para o alcance máximo sem engajar diretamente. Se não houver posição viável, âncora onde está e aguarda. Recusa completamente engajar sem linha de tiro. Atira mesmo em modo reparo enquanto estiver na construção. |
| `holdPositionWhenInRange` + `repositionToFireRange` | **Híbrido** | Prefere tiro parado. Se não conseguir, avança e soca no range 1 no mesmo turno. Nunca recusa engajar. |
| `canEscort` + `engageNearestEnemies` | **Escolta combatente** | Segue o capturador por coesão e, após cada movimento, faz rescan e ataca qualquer inimigo adjacente. Move em direção ao inimigo designado mesmo que ainda esteja fora de alcance. |
| Sensor `Capture` primeiro + `engageNearestEnemies` desligado | **Capturador rush** | Marcha pura para captura. Ignora inimigos pelo caminho. Avança com cautela em território inimigo desconhecido (FoW). |
| Sensor `Capture` primeiro + `engageNearestEnemies` ligado | **Capturador oportunista** | Captura é prioridade, mas morde inimigos no corredor e ataca qualquer alvo adjacente após mover. |
| `retreatToHqWhenIdle` + `engageNearestEnemies` | **Guarda de base** | Quando sem objetivo, volta ao HQ. Se encontrar inimigos pelo caminho ou perto da base, ataca. |
| `playConservative` + Sensor `Supply` | **Civil/Supridor** | Nunca se expõe. Prefere células seguras e patrulha território aliado. Age apenas para suprir aliados. |
| `retreatToHqWhenIdle` + `playConservative` | **Defensor absoluto** | Recua ao HQ quando ocioso E joga conservadoramente. Nunca avança território inimigo voluntariamente. |
| `holdPositionWhenInRange` + `holdGroundWhenIdle` + `prioritizeDpqDuringTravel` | **Entrincheirada** | Sem alvo, gravita para o melhor DPQ próximo (prédio, cobertura) e fica lá. Com alvo, atira parada. Nunca avança nem recua — segura a linha. |
| `holdPositionWhenInRange` + `retreatToHqWhenIdle` | **Guardiã de base** | Fica onde está se puder atirar. Quando sem alvo, volta ao HQ. Atira mesmo em reparo na construção. |

---

### Turn Order (Initiative)
Define a prioridade de acao dentro do turno da IA. Antes de iniciar o loop de unidades, o sistema ordena a fila por initiative efetiva (crescente) e, dentro do mesmo nivel, por HP atual decrescente (mais inteiras agem primeiro).
| Valor | Ordem | Quem usa |
|---|---|---|
| Priority | 1o | Artilharia, SAM/AAA, Estacionaria, Kamikaze |
| High | 2o | Escoltas, Lutadores, combatentes de linha |
| Medium | 3o | Padrao - Bazooka, Hibrido, Supridor |
| Low | 4o | Capturadores |
| Retreat | 5o | Estado temporario de unidades em Return to Base / Repair |
Initiative efetiva:
- Retreat nao e configuravel no AIUnitProfile
- ela e aplicada temporariamente quando a unidade entra em Return to Base / Repair
- ao sair desse modo, a unidade volta automaticamente para a initiative do profile
Tiebreaker de HP:
- unidades com o mesmo initiative sao ordenadas por HP atual decrescente
- as mais inteiras agem primeiro
- unidades mais fracas do mesmo grupo ficam como reserva
Exemplo:
- dois capturadores no mesmo turno: o de HP 10 captura o predio mais proximo, o de HP 4 vai para o segundo objetivo ou fica de reserva
---

### 0 Flags Ativadas

Unidade **peão**. Executa o sensor ativo da forma mais direta possível:
- Marcha para o objetivo do sensor sem preferência por cobertura.
- Não reposiciona para melhorar ângulo de tiro.
- Não escolta ninguém.
- Não ataca oportunisticamente fora do sensor Attack.
- Não recua ao HQ quando ociosa — avança em direção ao HQ inimigo (fallback genérico).
- Sem `playConservative`: se expõe sem hesitar.
- Em modo reparo: para completamente de combater e marcha para a base.

Útil como baseline ou para unidades que devem seguir ordens do planner sem nenhuma autonomia adicional.

---

### Todas as Flags Ativadas

**Comportamento imprevisível por conflito de flags:**

- `requireSightlineBeforeEngaging` cancela o alvo antes de engajar → `repositionToFireRange` salva como âncora e reposiciona → `engageNearestEnemies` (rescan pós-mover) encontra qualquer adjacente e ataca. Resultado: recusa engajar formalmente mas acaba socando qualquer um que apareça após mover.
- `retreatToHqWhenIdle` + `engageNearestEnemies` competem: recua ao HQ mas ataca durante o recuo.
- `playConservative` + `retreatToHqWhenIdle`: redundância parcial, ambos direcionam ao HQ por caminhos diferentes.
- `holdPositionWhenInRange` + `repositionToFireRange`: coerentes entre si (fica se pode, avança se não pode), mas com `requireSightlineBeforeEngaging` o "avança se não pode" vira reposicionamento sem engajamento direto.
- `holdGroundWhenIdle` + `retreatToHqWhenIdle`: conflito direto — `retreatToHqWhenIdle` tem prioridade na cascata e anula o holdGround. Use um ou outro.
- `canEscort`: se designado como escolta E com todas as outras flags, a unidade segue coesão mas depois do movimento ataca qualquer adjacente via `engageNearestEnemies`.

Não existe uso prático para todas as flags juntas. Use combinações específicas descritas acima.

---

## Unidades Rogue (Sem Plano)

Uma unidade e **rogue** quando o planner nao conseguiu atribui-la a nenhuma missao formal de `Capture`, `Escort`, `Artillery` ou `Support` naquele turno, e tambem nao ha intent relevante de supply, merge ou reparo para ela naquele ciclo.

No modelo novo, isso acontece com mais frequencia em dois casos:
- unidades com capability `Assault`, porque `Assault` nao entra no `ComputePlannedForce()` dos planos de captura;
- unidades que ate tem capability planejavel, mas sobraram fora dos slots do turno por distancia, prioridade ou falta de demanda.

Isso significa que **ate um capturador pode ficar rogue**. Quando isso acontece, ele continua usando o proprio profile e seus sensores normais, so que sem uma missao formal do planner. Na pratica, um capturador rogue pode acabar parecendo um skirmisher oportunista: sem plano de captura travando a unidade, ela reage ao que encontrar no caminho e pode sair atacando se o sensor/estado atual apontar para isso. Se isso estiver te parecendo interessante no gameplay, vale tratar como comportamento emergente valido, nao necessariamente como bug.

Unidades rogue ainda executam a IA normalmente, mas a cascata de `moveTarget` cai para o fallback generico no final:

| Prioridade | Condicao | Destino |
|---|---|---|
| 1 | Supply ativo | Alvo de supply |
| 2 | Merge ativo | Posicao atual |
| 3 | Reparo ativo | Construcao de reparo |
| 4 | Captura ativa (`captureObjectiveActive`) | Celula do objetivo de captura |
| 5 | Inimigo designado (`targetEnemy != null`) | Posicao do inimigo |
| 6 | `retreatToHqWhenIdle` ou modo defesa | HQ aliado |
| 7 | Coesao de plano (`planCohesionActive`) | Celula de coesao |
| 8 | `holdGroundWhenIdle` | Posicao atual |
| **9** | **Fallback rogue** | **HQ inimigo mais proximo** |

Se a unidade nao ativar nenhum dos sensores acima, ela marchara diretamente para o HQ inimigo mais proximo. Esse comportamento e deliberado: a unidade continua exercendo pressao mesmo sem plano formal. Isso costuma ser bom para `Assault`, mas pode ser indesejado para artilharia, suporte logistico ou outros perfis que deveriam permanecer sob controle mais rigido.

**Como mitigar comportamento rogue indesejado:**
- `holdGroundWhenIdle`: ancora onde esta em vez de avancar.
- `retreatToHqWhenIdle`: recua ao HQ aliado em vez de avancar.
- Sensor `Attack` ativo: se houver inimigos visiveis, a unidade os engajara antes de cair no fallback.
- Mais oferta de planos/capabilities: reduz a chance de sobras fora da composicao.

> **Exemplos praticos:** `AI Bazooka` e `AI Kamikaze` tendem a ficar rogue por desenho, porque sao `Assault`. Ja um `AI Capturador` rogue nao era o objetivo principal do modelo, mas pode gerar um comportamento emergente util: unidade leve sobrando do planner e brigando por conta propria no corredor.

---
## Modo Reparo
A unidade entra em modo reparo quando HP <= hpRepairThreshold, autonomia baixa ou municao de combate zerada. Sai quando HP >= hpRepairExitThreshold e autonomia e municao estiverem ok. O modo reparo nao e interrompido por mudanca de postura.
Efeito na ordem do turno:
- enquanto essa flag estiver ativa, a unidade passa a usar a iniciativa temporaria Retreat, sempre abaixo de Low
- ao sair do modo reparo, volta automaticamente para a initiative configurada no AIUnitProfile
Icone de manutencao (debug):
- quando showPlanDebugAtUnit estiver marcado no AIPlayerController, unidades em modo reparo exibem um icone de chave+martelo sobre o sprite
- ferramenta de desenvolvimento; nao aparece em build final
- os limiares de entrada (hpRepairThreshold) e saida (hpRepairExitThreshold) vem do AIUnitProfile de cada unidade
| Situacao | Comportamento |
|---|---|
| Unidade com holdPositionWhenInRange, ja na construcao | Atira parada se tiver alvo em alcance. Nao move. |
| Unidade com holdPositionWhenInRange, ainda marchando para a base | Foca em chegar. Nao atira durante a marcha. |
| Combatente sem holdPositionWhenInRange | Recua sem revidar. Prioriza chegar a base intacto. |
| Qualquer unidade com inimigo bloqueando o caminho | repairDislodgeActive: luta para desocupar o caminho, depois retoma o retorno. |
> Artilharia danificada continua atirando da construcao. O modo reparo significa "nao me move daqui", nao "paro de combater".
---

## Perfis Ativos

### AI Artilheiro
Planner Capability: `FireSupport`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Priority** | **Priority** |
| Sensores | Attack > Reposition | Attack > Reposition |
| Dano mín/máx | 10% / 50% | 10% / 20% |
| Must Survive | Sim | Não |
| canEscort | Não | Não |
| engageNearestEnemies | Sim | Sim |
| holdGroundWhenIdle | Sim | Não |
| retreatToHqWhenIdle | Não | Sim |
| prioritizeDpq (battle/travel) | Sim / Não | Sim / Sim |
| requireSightlineBeforeEngaging | Sim | Sim |
| holdPositionWhenInRange | Sim | Sim |
| repositionToFireRange | Sim | Sim |
| preferMaxEngagementRange | Sim | Sim |

**Comportamento:** Artilharia indireta pura. Só engaja se puder atirar parado. Reposiciona para o alcance máximo em busca de linha de tiro. Quando não há posição de tiro viável e não tem plano, âncora onde está e aguarda (`holdGroundWhenIdle`) — não avança em direção ao inimigo. Em defesa, recua ao HQ quando sem alvo e prioriza DPQ mesmo durante a marcha. O `engageNearestEnemies` garante rescan pós-mover caso a unidade se desloque por coesão. Em modo reparo, permanece na construção e continua atirando se tiver alcance.

---

### AI Bazooka
Planner Capability: `Assault`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Medium** | **Medium** |
| Sensores | Attack > Capture > Reposition | Attack > Capture > Reposition |
| Dano mín/máx | 0% / 0% | 0% / 0% |
| Must Survive | Sim | Não |
| Target Preference | Primário | Primário |
| canEscort | Não | Não |
| engageNearestEnemies | Sim | Sim |
| captureInterruptBias | Aggressive | Aggressive |
| prioritizeDpq (battle/travel) | Sim / Sim | Sim / Sim |

**Comportamento:** Combatente puro agressivo focado em alvos primários (blindados). Sem thresholds de dano — ataca qualquer alvo que apareça. `captureInterruptBias: Aggressive` (score 22.000): interrompe captura com facilidade ao encontrar inimigos no caminho. Prioriza DPQ em todo momento. Sem flags de estilo de combate: avança direto até o alcance e ataca.

> **Nota de design:** sem `retreatToHqWhenIdle` ou `playConservative`, o Bazooka em defesa sem alvo visível vai vagar pelo mapa em Reposition genérico. Isso é intencional — ele exerce pressão constante mesmo em postura defensiva, nunca para voluntariamente. Se quiser um Bazooka que recue ao HQ quando ocioso em defesa, adicione `retreatToHqWhenIdle` ao bloco de Defesa.
>
> Capture aparece no sensor após Attack. Se não houver inimigo, tenta capturar prédios.

---

### AI Capturador
Planner Capability: `Capture`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Low** | **Low** |
| Sensores | Capture > Attack > Reposition | Capture > Attack > Reposition |
| Dano mín/máx | 10% / 50% | 10% / 20% |
| Must Survive | Sim | Não |
| canEscort | Não | Não |
| engageNearestEnemies | Não | Não |
| captureInterruptBias | None | None |
| retreatToHqWhenIdle | Não | Sim |
| prioritizeDpq (battle/travel) | Sim / Não | Sim / Sim |

**Comportamento:** Rush puro para captura. Fluxo de decisão por turno:
- **Não alcança o objetivo este turno** → move em direção a ele. Com `engageNearestEnemies` desligado, ignora inimigos no caminho.
- **Alcança o objetivo e está vazio** → captura.
- **Alcança o objetivo e tem aliado** → o planner designa outro prédio no setor; se não houver, avança para o mesmo objetivo (aliado sai do caminho no turno seguinte).
- **Alcança o objetivo e tem inimigo visível** → posiciona em DPQ adjacente e ataca para desbloquear, independente de `captureInterruptBias`. `None` não impede este ataque — ele é parte da missão de captura, não uma interrupção dela.
- **Objetivo em território inimigo sem visibilidade (FoW)** → avança com cautela preferindo DPQ e movimento mínimo para revelar antes de entrar, exceto se um aliado já ocupa o prédio.

`captureInterruptBias: None` garante que a unidade nunca abandona a missão por oportunismo — nem "morde no caminho", nem fallback oportunista pós-movimento. Em defesa, recua ao HQ quando sem objetivo.

---

### AI Estacionaria
Planner Capability: `FireSupport`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Priority** | **Priority** |
| Sensores | Attack > Reposition | Attack > Reposition |
| Dano mín/máx | 10% / 20% | 10% / 20% |
| Must Survive | Não | Não |
| canEscort | Não | Não |
| engageNearestEnemies | Sim | Sim |
| retreatToHqWhenIdle | Não | Não |
| playConservative | Não | Não |
| holdGroundWhenIdle | Não | Sim |
| holdPositionWhenInRange | Sim | Sim |
| requireSightlineBeforeEngaging | Sim | Sim |
| repositionToFireRange | Sim | Sim |
| prioritizeDpq (battle/travel) | Sim / Sim | Sim / Sim |

**Comportamento em ataque:** Move em busca de linha de tiro, reposiciona para o alcance máximo, avança se necessário.

**Comportamento em defesa:** Sem alvo, ancora na posição atual e gravita para o melhor DPQ próximo (prédio, cobertura) — nunca recua ao HQ nem avança. Com alvo em alcance, atira parada. Atira mesmo em modo reparo enquanto estiver na construção.

> **Por que as posturas diferem agora?** Em ataque a unidade pode se mover para encontrar linha de tiro — é artilharia de campanha. Em defesa ela entrincheira no melhor abrigo disponível e aguarda. A distinção reflete a diferença entre "artilharia apoiando avanço" e "artilharia fixada em posição defensiva".

---

### AI Hibrido
Planner Capability: `FireSupport`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Medium** | **Medium** |
| Sensores | Attack > Reposition | Attack > Reposition |
| Dano mín/máx | 10% / 50% | 10% / 20% |
| Must Survive | Sim | Não |
| canEscort | Sim | Não |
| engageNearestEnemies | Sim | Sim |
| requireSightlineBeforeEngaging | Não | Não |
| holdPositionWhenInRange | Sim | Sim |
| repositionToFireRange | Sim | Sim |
| preferMaxEngagementRange | Não | Não |

**Comportamento:** Prefere tiro parado quando já está em alcance. Se não conseguir linha de tiro, avança e ataca no range 1 (porrada) no mesmo turno — sem o atraso de um turno que o Artilheiro puro teria. No modelo novo, entra no planner como `FireSupport`, nao como escolta de linha. Continua podendo agir como apoio ofensivo e fazer rescan apos mover via `engageNearestEnemies`. Em defesa nao escolta.

---

### AI Kamikaze
Planner Capability: `Assault`


| Postura | Única (Ataque, Invasão e Defesa) |
|---|---|
| **Initiative** | **Priority** |
| Sensores | Attack > Reposition |
| Dano mín/máx | 0% / 0% |
| Must Survive | Não |
| Target Preference | Primário |
| canEscort | Não |
| engageNearestEnemies | Não |
| prioritizeDpq (battle) | Sim |

**Comportamento:** Ataca qualquer alvo primário sem nenhum limiar de dano ou sobrevivência. Avança direto até o inimigo. Postura única para todas as stances — sem distinção entre ataque e defesa. Thresholds de reparo mínimos (HP ≤1 entra em reparo, sai com HP=2) para maximizar tempo no campo.

---

### AI Lutador
Planner Capability: `Escort`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **High** | **High** |
| Sensores | Attack > Reposition | Attack > Reposition |
| Dano mín/máx | 10% / 50% | 10% / 20% |
| Must Survive | Sim | Não |
| canEscort | Sim | Não |
| engageNearestEnemies | Sim | Sim |
| prioritizeDpq (battle) | Sim | Sim |

**Comportamento:** Combatente direto. O sensor Attack planeja o movimento em direção ao inimigo designado mesmo que ainda esteja fora de alcance — o engajamento acontece ao chegar, não exige linha de tiro prévia. Como `Escort`, pode ser designado para acompanhar o capturador por coesao e, com `engageNearestEnemies`, ataca qualquer inimigo adjacente apos cada movimento. Em defesa não escolta e aceita mais risco (must survive desligado, threshold de dano recebido menor).

---

### AI Supridor
Planner Capability: `Logistics`


| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Medium** | **Medium** |
| Sensores | Supply > Reposition | Supply > Reposition |
| Must Survive | Sim | Sim |
| canEscort | Não | Não |
| engageNearestEnemies | Não | Não |
| retreatToHqWhenIdle | Não | Sim |
| playConservative | Sim | Sim |

**Comportamento:** Civil puro. Nunca ataca proativamente (sem sensor Attack). Joga conservadoramente em ambas as posturas — prefere celulas seguras e evita perigo. Em defesa, recua ao HQ quando sem aliados para suprir. Quando o sensor `Supply` encontra mais de um aliado valido, prioriza o **mais proximo**; se houver empate de distancia, desempata pela criticidade (HP, municao e combustivel). Primeiro tenta suprir sem mover; se nao houver alvo imediato, navega ate o aliado escolhido. Retorna a base para reabastecer quando combustivel, municao ou pecas da propria carroceria caem abaixo dos limiares de restock do profile.






