# AI Unit Profiles

---

## Referência de Flags

### Sensor Priority

A ordem dos sensores define o que a unidade tenta fazer a cada turno. O primeiro sensor que encontra um objetivo válido vence — os demais são ignorados naquele turno.

| Sensor | O que faz |
|---|---|
| **Capture** | Procura o prédio capturável mais prioritário no setor e marcha até ele. Com setor planejado: prefere prédios livres sobre ocupados independente da distância. Avança com cautela (DPQ + movimento mínimo) quando o alvo é território inimigo sem visibilidade (FoW) — exceto se um aliado já ocupar o objetivo (FoW não se aplica, avança normalmente). |
| **Attack** | Procura o melhor inimigo para engajar com base nos critérios de Attack Decision e planeja o movimento para atacar. O planejamento ocorre mesmo que a unidade ainda não esteja em alcance de tiro — ela move-se em direção ao alvo e ataca ao chegar. |
| **Supply** | Procura aliados para reabastecer (combustível, munição, peças) dentro dos limiares configurados. |
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
Permite que o planner designe esta unidade como **escolta** de um capturador. Como escolta, a unidade segue a coesão do plano (fica perto do capturador) em vez de agir livremente. Sem esta flag, a unidade nunca será designada como escolta.

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

Define a **prioridade de ação dentro do turno** da IA. Antes de iniciar o loop de unidades, o sistema ordena a fila por `initiative` (crescente) e, dentro do mesmo nível, por **HP atual decrescente** (mais inteiras agem primeiro).

| Valor | Ordem | Quem usa |
|---|---|---|
| **Priority** | 1º | Artilharia, SAM/AAA, Estacionaria, Kamikaze |
| **High** | 2º | Escoltas, Lutadores, combatentes de linha |
| **Medium** | 3º | Padrão — Bazooka, Híbrido, Supridor |
| **Low** | 4º | Capturadores |

**Tiebreaker de HP:** unidades com o mesmo `initiative` são ordenadas por HP atual decrescente. As mais inteiras agem primeiro — garantindo que as que têm mais capacidade operacional executem as missões críticas (ex: captura de prédio). Unidades mais fracas do mesmo grupo ficam como reserva.

> **Exemplo:** dois capturadores no mesmo turno — o de HP 10 captura o prédio mais próximo, o de HP 4 vai para o segundo objetivo ou fica de reserva.

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

Uma unidade é **rogue** quando o planner não conseguiu atribuí-la a nenhuma missão — sem captura planejada, sem papel de escolta, sem intent de supply ou merge. Isso pode acontecer porque o time tem mais unidades do que slots de plano disponíveis, ou porque a unidade foi criada/movida fora de um ciclo de planejamento.

Unidades rogue ainda executam a IA normalmente, mas a cascata de `moveTarget` cai para o fallback genérico no final:

| Prioridade | Condição | Destino |
|---|---|---|
| 1 | Supply ativo | Alvo de supply |
| 2 | Merge ativo | Posição atual |
| 3 | Reparo ativo | Construção de reparo |
| 4 | Captura ativa (`captureObjectiveActive`) | Célula do objetivo de captura |
| 5 | Inimigo designado (`targetEnemy != null`) | Posição do inimigo |
| 6 | `retreatToHqWhenIdle` ou modo defesa | HQ aliado |
| 7 | Coesão de plano (`planCohesionActive`) | Célula de coesão |
| 8 | `holdGroundWhenIdle` | Posição atual |
| **9** | **Fallback rogue** | **HQ inimigo mais próximo** |

Se a unidade não ativar nenhum dos sensores acima (sem captura viável, sem inimigo, sem flags de idle), ela marchará diretamente para o HQ inimigo mais próximo. Esse comportamento é deliberado — a unidade exerce pressão mesmo sem plano — mas pode ser indesejado para artilharia e unidades de suporte.

**Como mitigar o comportamento rogue indesejado:**
- `holdGroundWhenIdle`: ancora onde está em vez de avançar.
- `retreatToHqWhenIdle`: recua ao HQ aliado em vez de avançar.
- Sensor `Attack` ativo: se houver inimigos visíveis, a unidade os engajará antes de cair no fallback.

> **Exemplo real:** o AI Artilheiro sem plano entrava no cluster inimigo porque a cascata caía no fallback rogue (HQ inimigo) sem passar por `holdGroundWhenIdle`. Corrigido com `holdGroundWhenIdle: true` na stance de Ataque.

---

## Modo Reparo

A unidade entra em modo reparo quando HP ≤ `hpRepairThreshold`, autonomia baixa ou munição de combate zerada. Sai quando HP ≥ `hpRepairExitThreshold` E autonomia E munição estiverem ok. O modo reparo **não é interrompido por mudança de postura** — o time pode cair em Defense e a unidade continua o retorno à base até estar apta.

**Ícone de manutenção (debug):** quando `showPlanDebugAtUnit` estiver marcado no `AIPlayerController`, unidades em modo reparo exibem um ícone de chave+martelo sobre o sprite. Ferramenta de desenvolvimento — não aparece em build final. Os limiares de entrada (`hpRepairThreshold`) e saída (`hpRepairExitThreshold`) vêm do `AIUnitProfile` de cada unidade.

| Situação | Comportamento |
|---|---|
| Unidade com `holdPositionWhenInRange`, já na construção | Atira parada se tiver alvo em alcance. Não move. |
| Unidade com `holdPositionWhenInRange`, ainda marchando para a base | Foca em chegar. Não atira durante a marcha. |
| Combatente sem `holdPositionWhenInRange` | Recua sem revidar. Prioriza chegar à base intacto. |
| Qualquer unidade com inimigo bloqueando o caminho | `repairDislodgeActive`: luta para desocupar o caminho, depois retoma o retorno. |

> Artilharia danificada continua atirando da construção — exatamente como no gameplay manual. O modo reparo significa "não me move daqui", não "paro de combater".

---

## Perfis Ativos

### AI Artilheiro

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

**Comportamento:** Prefere tiro parado quando já está em alcance. Se não conseguir linha de tiro, avança e ataca no range 1 (porrada) no mesmo turno — sem o atraso de um turno que o Artilheiro puro teria. Em ataque pode ser designado como escolta — após mover por coesão, faz rescan e ataca adjacentes via `engageNearestEnemies`. Em defesa não escolta.

---

### AI Kamikaze

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

| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **High** | **High** |
| Sensores | Attack > Reposition | Attack > Reposition |
| Dano mín/máx | 10% / 50% | 10% / 20% |
| Must Survive | Sim | Não |
| canEscort | Sim | Não |
| engageNearestEnemies | Sim | Sim |
| prioritizeDpq (battle) | Sim | Sim |

**Comportamento:** Combatente direto. O sensor Attack planeja o movimento em direção ao inimigo designado mesmo que ainda esteja fora de alcance — o engajamento acontece ao chegar, não exige linha de tiro prévia. Em ataque pode ser designado como escolta — segue o capturador por coesão e, com `engageNearestEnemies`, ataca qualquer inimigo adjacente após cada movimento. Em defesa não escolta e aceita mais risco (must survive desligado, threshold de dano recebido menor).

---

### AI Supridor

| Postura | Ataque/Invasão | Defesa |
|---|---|---|
| **Initiative** | **Medium** | **Medium** |
| Sensores | Supply > Reposition | Supply > Reposition |
| Must Survive | Sim | Sim |
| canEscort | Não | Não |
| engageNearestEnemies | Não | Não |
| retreatToHqWhenIdle | Não | Sim |
| playConservative | Sim | Sim |

**Comportamento:** Civil puro. Nunca ataca proativamente (sem sensor Attack). Joga conservadoramente em ambas as posturas — prefere células seguras e evita perigo. Em defesa, recua ao HQ quando sem aliados para suprir. Retorna à base para reabastecer quando combustível ≤20%, munição ≤10% ou peças ≤10%.
