Parte 1:
Grid hexagonal. Movimentação nas 6 direções de hex. Cada unidade tem pontos de movimento (RemainingMovementPoints) consumidos pelo custo de terreno de cada hex atravessado. Não há limite de direção, só de pontos. Custo e permissão de entrada dependem de TerrainTypeData + skills da unidade — terrenos como rios ou montanhas exigem skills específicas; unidades aéreas ignoram essas restrições.

Capturar um prédio significa ocupar o hex onde ele está e executar a ação Capture a cada turno. A captura não é instantânea — o prédio tem CapturePointsMax e CurrentCapturePoints; a unidade precisa permanecer e capturar turno a turno até encher os pontos.

Defensor no prédio: dois hexes não podem ser co-ocupados na mesma camada de altitude. Para entrar no hex do prédio o inimigo não pode estar lá — é preciso expulsá-lo ou eliminá-lo antes. O AI verifica isso via HexOccupancyQuery.FindUnitAtCell.

Posição vantajosa é definida pelo DPQ (TerrainTypeData.dpqData) — uma escala de qualidade de posição: Unfavorable (0) ? Default (1) ? Improved (2) ? Favorable (3) ? Unique (4). DPQ controla bônus de defesa (defesaBonus). EV (TerrainTypeData.ev) é separado: controla visão/FoW — unidades em terreno com EV alto enxergam mais longe. Os dois não são equivalentes: montanha tem EV alto e DPQ favorável, mas uma estrada em montanha pode herdar o EV sem herdar o DPQ.

Cobertura, camuflagem, modificadores de ataque por terreno: não existem. Não é um RPG. O terreno afeta a qualidade de posição defensiva (DPQ) e a visibilidade (EV/FoW), não precisão ou dano diretamente.

Ataque:

Alcance definido por arma (UnitEmbarkedWeapon) com minRange e maxRange
Mover e atirar no mesmo turno é possível, mas com restrição de alcance seletivo:
MoveuParado (não moveu): pode usar alcance mínimo e máximo
MoveuAndando (moveu): pode usar apenas o alcance mínimo, e somente se esse mínimo for 1
Não há penalidade numérica de alcance — é uma questão de quais alcances ficam disponíveis
HP representa membros do esquadrão, não pontos de vida no sentido RPG. Um esquadrão começa com 10 membros. "Dano" é eliminação de membros. A força de ataque efetiva escala com o HP atual do atacante; a defesa é quase sempre fixa e não varia com os membros restantes. Uma unidade com HP 2 está gravemente enfraquecida para atacar, mas sua capacidade de absorver ataques não cai na mesma proporção.

O que bloqueia o caminho ao prédio:

Terreno com restrições de acesso (requer skill, ou unidade sem domínio correto)
Unidades aliadas — podem ser atravessadas, mas o movimento não pode terminar no mesmo hex (exceto camadas de altitude diferentes, onde coexistência é permitida)
Unidades inimigas — bloqueiam passagem; é preciso desviar ou abrir caminho via combate. Unidades de altitude diferente passam por cima, independente de time
Pathfinding: UnitMovementPathRules.CalcularCaminhosValidos retorna Dictionary<Vector3Int, List<Vector3Int>> — cada chave é um hex alcançável, o valor é o caminho. Retorna nulo/vazio se a unidade não tem movimento ou está cercada. É a fonte de verdade que o AI consulta antes de qualquer decisão.

---
Parte 2:
Ações disponíveis em um turno:

Uma unidade executa uma ação por turno composta de movimento + sensor. As combinações relevantes para um capturador:

Mover — desloca para outro hex dentro do alcance de movimento
Capturar — move até o hex do prédio (ou permanece nele) e executa SensorAction.Capture; só válido se o prédio for capturável e a unidade tiver a role Capturador
Atacar — move até uma posição e executa SensorAction.Attack contra um alvo válido; depende de ter arma embarcada e alvo em alcance
Aguardar — move para o próprio hex (custo zero), sem sensor action
A ordem é sempre mover ? sensor action. Não é possível atirar antes de mover. Capturar e atacar no mesmo turno não é possível — são sensor actions mutuamente exclusivas.

Visão limitada: sim, FoW completo. Cada unidade enxerga dentro do seu raio visao (campo em UnitData), modificado por EV do terreno e regras de LoS. O AI só reage a inimigos visíveis — MatchController.IsUnitVisibleForTeam é consultado em cada decisão. Um inimigo atrás de uma montanha ou fora do raio é invisível mesmo que o AI "saiba" que ele existe no estado interno do jogo; a decisão é tomada como se ele não estivesse lá. A visibilidade é atualizada após cada movimento de cada unidade no turno — o que o capturador X enxerga pode mudar depois que o aliado Y se moveu e revelou novas células.

Ocupar o mesmo hex:

Aliado: pode passar pelo hex durante o movimento, mas não pode terminar o turno lá (exceto camadas de altitude diferentes, onde coexistência é permitida)
Inimigo: não pode passar nem terminar — o hex é bloqueado; é preciso contornar ou combater antes
Não existe captura por movimento (entrar no hex não captura nada) nem combate corpo a corpo automático ao tentar entrar no hex inimigo
Munição: o sistema existe (UnitEmbarkedWeapon com controle de ammo por arma, e os campos repairTriggerAmmoEnabled/repairTriggerAmmoPct para modo de reparo). O sensor de ataque (PodeMirarSensor) só retorna alvos válidos se a arma tiver munição disponível — portanto o AI não tenta atacar sem ammo porque o sensor simplesmente não oferece o alvo. Não há recarga por turno explícita no código do AI; a gestão de ammo é feita pela camada de execução do sensor, não pelo planner. Um capturador desarmado (sem embarkedWeapons) nunca recebe ação de ataque — HasAttackTargetAtCurrentPos sempre retorna false e ele passa direto para capturar/mover.

---
Parte 3: 
O que é um plano:

TeamObjectivePlan é um objeto por time que contém:

Objectives — lista de SectorObjective, um por setor a capturar
RogueUnitIds — conjunto de IDs de capturadores sem atribuição
Cada SectorObjective tem: setor-alvo, time, status (Pending/Pursuing/Capturing/Complete), prioridade numérica, e Slots — lista de vagas (SlotNeed) com role exigida, se está preenchida e qual unidade foi atribuída.

Sequência ou intenção:

Intenção de alto nível. O objetivo diz "capture o setor X" — não há sequência de hexes, waypoints ou ordens encadeadas. O caminho, o prédio específico dentro do setor e cada movimento são decididos turno a turno pelo capturer code. O plano é um rótulo persistente de responsabilidade, não um script de ações.

Como a unidade recebe um plano:

BuildObjectivePlan roda uma vez por turno (início do turno da IA) e:

Valida objetivos existentes (remove setores já conquistados, libera slots de unidades mortas)
Cria novos objetivos para setores não cobertos
Atribui capturadores livres a setores via SolveAssignment (backtracking que minimiza distância total unidade?prédio)
Capturadores sem setor disponível viram RogueUnitIds
A atribuição é ao setor (zona de mapa), não a um prédio específico. O prédio dentro do setor é resolvido a cada decisão por FindCapturableInSector — primeiro prédio não-conquistado encontrado.

O que faz o SectorManager:

Fornece informação territorial sobre cada setor do mapa. Via GetAllSectorInfos() retorna SectorInfo com:

Quais construções pertencem ao setor e de qual time cada uma é
Se o setor está totalmente controlado (IsFullyControlled), disputado (IsDisputed), e por quem
Nível de risco para um time (GetRiskLevelFor: Safe/Low/Medium/High)
Distância do setor ao HQ de cada time (GetDistanceToHQ)
Não retorna lista de inimigos — isso é domínio do UnitManager.AllActive + filtros de FoW. SectorManager é puramente sobre posse territorial e construções.

Como o setor é definido:

Por atribuição explícita do designer. Cada ConstructionManager tem um campo Sector (enum ConstructionSector). Não é raio, não é zona geométrica — é um label em cada prédio do mapa. O SectorManager agrupa os prédios por esse label.

Unidade sem plano (Rogue):

DecideRogueCapturerAction — marcha em direção ao HQ inimigo, capturando oportunisticamente qualquer prédio alcançável no caminho. Prioridade Attack > Capture: se há inimigo visível no raio de engajamento, delega ao HexEvaluator para combater antes de avançar. O rogue não defende posição nem patrulha — avança sempre.

Integração no loop do AI:

DecideUnitAction é chamado uma vez por unidade por turno, dentro do while de Phase2_UnitActions. Não há OnTurnStart por unidade. O fluxo é:


RunAITurn
 ?? BuildObjectivePlan          ? uma vez por turno
 ?? Phase2_UnitActions
     ?? loop: enquanto há unidades disponíveis
         ?? DecideUnitAction(unit)
         ?   ?? TryDecideCapturerAction  ? capturer code
         ?   ?? HexEvaluator            ? fallback
         ?? ExecuteLiveAIBatch(action)  ? executa e aguarda
Acesso aos managers:

Sim, todos acessíveis por métodos estáticos:

ObjectiveManager.GetOrCreatePlanForTeam(team) / GetPlanForTeam(team)
SectorManager.GetAllSectorInfos() / TryGetSectorInfo(sector, out info)
ConstructionManager.AllActive — lista de todas as construções ativas
UnitManager.AllActive — lista de todas as unidades ativas

----
Parte 4:
Acesso ao estado do jogo:

A IA tem acesso ao estado interno completo — posição, HP, tipo de todas as unidades e construções. Mas as decisões são filtradas por FoW: MatchController.IsUnitVisibleForTeam é chamado explicitamente antes de reagir a qualquer inimigo. O AI conhece a existência de um inimigo no estado bruto do jogo, mas o trata como invisível na tomada de decisão se a visibilidade retornar false. Terreno e construções são sempre completamente conhecidos — FoW se aplica a unidades, não ao mapa. A visibilidade é recalculada após cada movimento de cada unidade no turno, então o que era invisível no início do turno pode ser revelado quando um aliado avança.

Prédio alvo:

O plano atribui um setor, não um prédio específico. No momento da decisão, FindCapturableInSector resolve o prédio capturável mais próximo da unidade dentro desse setor — se o setor tem três prédios (A a 1 hex, B a 3 hexes, C a 5 hexes), o capturador vai para A. Esse cálculo acontece turno a turno: se A for conquistado no turno seguinte, B passa a ser o mais próximo e o capturador redireciona automaticamente. Não há "lock" em um prédio específico entre turnos.

Pathfinding:

UnitMovementPathRules.CalcularCaminhosValidos retorna Dictionary<Vector3Int, List<Vector3Int>> — cada chave é um hex alcançável neste turno, o valor é a rota completa até lá. Considera custo de terreno, skills da unidade e domínio/altitude. Hexes ocupados por qualquer unidade são excluídos como destino.

O que o pathfinding não faz: não avalia perigo ao longo da rota. Diz "você consegue chegar em X" mas não "passar por Y coloca você sob fogo". Inimigos bloqueiam o hex que ocupam, mas não criam zona de influência nos hexes adjacentes dentro do pathfinding.

Avaliação de ameaças:

A IA tem três mecanismos, todos baseados em posição estática — não em rota:

HexEvaluator.safety — para cada hex candidato avalia o perigo com base em inimigos visíveis nas proximidades. É o mecanismo mais rico, consultado quando o capturer code retorna null.
HasEnemyInEngageRadius — há algum inimigo visível dentro de movimentação+1 hexes da posição atual. Não considera o alcance real da arma inimiga, só distância geométrica.
HasAttackTargetAtCurrentPos — consulta PodeMirarSensor para saber se a unidade consegue atirar de onde está agora. Avalia o alcance real da própria arma, não da inimiga.
O que não existe: avaliação de "o inimigo cobre o hex X que preciso atravessar". A IA não computa zonas de controle inimigas nem verifica se a rota de movimento expõe a unidade a fogo inimigo. A consciência de ameaça é sempre sobre a posição de chegada, nunca sobre o caminho até ela.

---
Parte 5: Prioridade entre eliminar ameaças e capturar:

Depende do tipo de capturador:

Assigned (tem setor): Capture > Attack. Avança em direção ao alvo mesmo com inimigos próximos. Só luta via auto-defesa (HasAttackTargetAtCurrentPos — inimigo em alcance de tiro direto da posição atual). Se bloqueado sem avanço possível, delega ao HexEvaluator.
Rogue (sem setor): Attack > Capture. Se há inimigo visível no raio de engajamento (movimento+1 hexes), delega ao HexEvaluator para combater antes de avançar. Captura oportunisticamente no caminho ao HQ inimigo.
Sobrevivência / recuo / cura:

O capturer code não toma decisões baseadas em HP. Há um sistema de reparo em UnitData (repairTriggerHpBelow, fuseWhileInRepair) mas é tratado em outra camada do AI, não aqui. O HexEvaluator tem safety que indiretamente evita posições perigosas quando assume o controle, mas não há lógica de "recuar se HP baixo" no código do capturador. Uma unidade ferida avança da mesma forma que uma intacta.

Desviar da rota mais curta por posição vantajosa:

O padrão canAdvance atual escolhe o hex geometricamente mais próximo do alvo entre os alcançáveis — sem considerar DPQ ou segurança do caminho. É otimização de distância pura. Posição vantajosa é considerada apenas no FoW passinho (TryFindBestLoSCell, que usa DPQ ou EV conforme prioritizeDpqAtBattle) e pelo HexEvaluator quando assume o controle. Na rota normal de avanço, não há desvio por qualidade de terreno.

Posições candidatas:

canAdvance avalia todos os hexes alcançáveis e retorna o único melhor (mais próximo do alvo). O HexEvaluator avalia todos os hexes livres alcançáveis com scoring multi-fator (captureProximity, combatValue, cohesion, deviation, safety) e escolhe o de maior total. São abordagens distintas: capturer code = um vencedor por critério simples; HexEvaluator = ranking completo com pesos.

Caminho obstruído ? atirar no bloqueador:

Se o inimigo está em alcance de tiro da posição atual ? HasAttackTargetAtCurrentPos dispara ? null ? HexEvaluator escolhe posição e ataca. Se o inimigo bloqueia o caminho mas está fora de alcance ? o capturer avança via canAdvance em direção ao alvo mesmo assim — contornando o inimigo se o pathfinding encontrar rota alternativa, ou aproximando-se até entrar em alcance de tiro. Não há decisão explícita de "atire no bloqueador" — isso emerge naturalmente quando a distância cai o suficiente.

Escolher entre múltiplos bloqueadores:

FindBestAttackTarget pontua cada alvo visível assim:

+200 × (10 ? HP) — preferência por alvos fracos
+5000 se HP ? 2 (provável eliminação)
?50 × distância — penalidade por distância
+10000 se o alvo está sobre uma construção (bônus exclusivo para capturadores)
O alvo em construção tem prioridade absoluta sobre qualquer outro critério.

Prédio ocupado por inimigo:

Dois hexes da mesma camada de altitude não podem ser co-ocupados. Captura é impossível enquanto o defensor estiver no hex — não é uma regra do AI, é uma restrição da mecânica do jogo. O fluxo atual é: o capturer avança até o inimigo entrar em alcance de tiro ? HasAttackTargetAtCurrentPos ? HexEvaluator ataca ? quando o hex fica livre ? capturer entra e captura. Não há decisão "ataque à distância vs entrar" — o capturador nunca pode entrar num hex ocupado de qualquer forma.

----
Parte 6 : 
Linguagem e engine:

C# + Unity. Todo o código do AI é MonoBehaviour/ScriptableObject padrão Unity, rodando na main thread via coroutines (IEnumerator). Sem threading, sem async, sem engine customizada.

Limitações de desempenho:

Não há limite técnico ao número de capturadores simultâneos — SolveAssignment tem comentário explícito bounding a N?8 unidades e M?8 objetivos abertos (P(8,8) = 40.320 iterações, trivial). Na prática o jogo opera com 10–30 unidades por lado. As iterações sobre UnitManager.AllActive e ConstructionManager.AllActive acontecem várias vezes por turno mas as listas são pequenas. Nenhuma decisão usa física, raycast de mundo 3D ou busca em grafo pesado — pathfinding é BFS/Dijkstra em hex grid pequeno.

Classes e funções disponíveis:

Estado de unidade


UnitManager                       — instância em campo
  .CurrentCellPosition            — Vector3Int, hex atual
  .CurrentHP                      — int (= membros do esquadrão)
  .RemainingMovementPoints        — int
  .TeamId                         — enum
  .InstanceId                     — int, identificador único
  .IsDead / IsEmbarked / HasActed — bool
  .TryGetUnitData(out UnitData)   — acessa definição da unidade
  UnitManager.AllActive           — IEnumerable de todas as unidades vivas

UnitData  (ScriptableObject)
  .movement / .visao / .roles
  .embarkedWeapons                — lista de armas com min/maxRange
  .prioritizeDpqAtBattle          — bool
  .aiInitiative                   — enum (Priority ? Retreat)
Estado de construção


ConstructionManager               — prédio em campo
  .CurrentCellPosition            — Vector3Int
  .TeamId                         — dono atual
  .Sector                         — enum ConstructionSector
  .IsCapturable / .CapturePointsMax / .CurrentCapturePoints
  ConstructionManager.AllActive   — IEnumerable de todos os prédios
Terreno


TerrainTypeData  (ScriptableObject)
  .ev                             — int, alcance de visão
  .dpqData                        — DPQData (qualidade de posição)
  .basicAutonomyCost              — custo de movimento para entrar

DPQData
  .qualidadeDePosicao             — enum (Unfavorable=0 … Unique=4)
  .Pontos                         — int 0–4
  .DefesaBonus                    — int, ?1 a +6
Grid e pathfinding


UnitMovementPathRules
  .CalcularCaminhosValidos(tilemap, unit, movPoints, terrainDb)
    ? Dictionary<Vector3Int, List<Vector3Int>>
      chave = hex alcançável, valor = rota completa

  .GetImmediateHexNeighbors(tilemap, cell, List<Vector3Int>)
    ? preenche lista com os 6 vizinhos válidos do hex

HexOccupancyQuery
  .FindUnitAtCell(cell)           — UnitManager ou null (ignora FoW)

ConstructionOccupancyRules
  .GetConstructionAtCell(tilemap, cell) — ConstructionManager ou null
Planejamento e setores


ObjectiveManager
  .GetOrCreatePlanForTeam(team)   — TeamObjectivePlan
  .GetPlanForTeam(team)           — TeamObjectivePlan ou null

TeamObjectivePlan
  .Objectives                     — List<SectorObjective>
  .RogueUnitIds                   — HashSet<int>

SectorObjective
  .Sector / .Status / .Priority / .Slots

SectorManager
  .GetAllSectorInfos()            — IReadOnlyList<SectorInfo>
  .TryGetSectorInfo(sector, out info) — bool

SectorInfo
  .IsFullyControlled / .ControllingTeam / .IsDisputed
  .GetRiskLevelFor(team)          — enum (Safe/Low/Medium/High)
  .GetDistanceToHQ(team)          — float
Combate e visibilidade


PodeMirarSensor
  .CollectTargets(unit, tilemap, terrainDb, movementMode, targets, fromCell)
    ? bool; preenche List<PodeMirarTargetOption>
    movementMode: MoveuParado (min+max range) | MoveuAndando (só min se =1)

MatchController
  .IsUnitVisibleForTeam(unit, team) — bool, respeita FoW
Snapshot de turno


AIWorldSnapshot  (construído no início de cada turno)
  .AITeam / .MyUnits / .EnemyUnits
  .EnemyHQ                        — ConstructionManager do HQ inimigo
  .Budget / .TurnNumber / .Stance