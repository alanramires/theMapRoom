# Pend√™ncias do MVP

- [x] Constru√ß√£o aliada como *spotter* apenas para si mesma: pode contribuir para a artilharia, mas somente contra alvos em sua pr√≥pria localiza√ß√£o.
    - tambem houveram ajustes nos contribuidores de vis√£o
- [x] Confirmar a regra de "apenas mover" (em debate).
    - inferir movimento virou flag "Atalho Contextual"


- [ ] Criar um tutorial para novatos.
- [x] em FOW Total, apenas revelar o FOW do jogador, a AI joga escondida (cursor escondido, unidades escondidas)
    - cursor e proj√©teis da AI aparecem somente nos hexes vis√≠veis pelo jogador
    - `fow partial` restaura a perspectiva ativa antiga para depura√ß√£o; `fow off` revela tudo
- [x] Implementar a AI no modo Easy. A IA recebe 1/3 da renda de construcoes, exceto cidades (renda integral).

0) AI Hard caiu em 15 turnos, n„o fez progress„o rapida, n„o fez blitzkrieg, deixou a base vazia varias vezes

1) hmmm.... se HQ È top prioridade, pq qdo ele tava vazio o capturador foi capturar uma fabrica ou uma cidade neutra?  o que est· puxando o capturador pra n„o aproveitar essa botija? È o plano? È o eixo? 

2) se a AI t· vendo infantaria pipocando em seu jogadas manager pq ela comprou obus Leve? sÛ pq È resposta de defesa? poderia ter comprado o metranca q tem um score anti-inf melhor, ela n„o viu isso nao? 

[AI VERMELHO][T1] Fase3 ó compras.
UnityEngine.Debug:Log (object)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:16)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy] Sync1: 0ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:123)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy] RefreshFoW: 7ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:127)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=8 bases=2 constructions=21
UnityEngine.Debug:Log (object)
SectorManager:RebuildFromActiveConstructions (string) (at Assets/Scripts/Construction/SectorManager.cs:792)
SectorManager/<RebuildNextFrameRoutine>d__43:MoveNext () (at Assets/Scripts/Construction/SectorManager.cs:575)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy] yield+SectorRebuild: 379ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:135)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy] Sync2: 0ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:139)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:143)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Rally][T1][Red] Hotel via Cidade_T-1_C147 owner=1 held=False rallies=0 focus=None artGlobal=0/3 artLocal=0 cap=0 ass=0 airAtk=0 intel=0 log=0 threat=0 knownEnemy=5 packages=1 force=0/10 rallyState=WaitHold goGreen=False timeout=False missing=hold WAIT_HOLD
UnityEngine.Debug:Log (object)
AIController:LogRallyReadiness (ConstructionManager,int,TeamId,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:245)
AIController:BuildRallyPlanContext (TeamId,int,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:139)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:82)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Rally][T1][Red] Bravo via Cidade_T-1_C144 owner=1 held=False rallies=0 focus=None artGlobal=0/3 artLocal=0 cap=0 ass=0 airAtk=0 intel=0 log=0 threat=0 knownEnemy=5 packages=1 force=0/10 rallyState=WaitHold goGreen=False timeout=False missing=hold WAIT_HOLD
UnityEngine.Debug:Log (object)
AIController:LogRallyReadiness (ConstructionManager,int,TeamId,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:245)
AIController:BuildRallyPlanContext (TeamId,int,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:139)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:82)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Rally][T1][Red] Charlie via Cidade_T-1_C143 owner=1 held=False rallies=0 focus=None artGlobal=0/3 artLocal=0 cap=0 ass=0 airAtk=0 intel=0 log=0 threat=0 knownEnemy=5 packages=1 force=0/10 rallyState=WaitHold goGreen=False timeout=False missing=hold WAIT_HOLD
UnityEngine.Debug:Log (object)
AIController:LogRallyReadiness (ConstructionManager,int,TeamId,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:245)
AIController:BuildRallyPlanContext (TeamId,int,int,AIIntelReport) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.RallyPoints.cs:139)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:82)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Anchor][T1][Red] Golf via Cidade_T-1_C149 slot=1 held=False
UnityEngine.Debug:Log (object)
AIController:BuildAnchorPlanContext (TeamId,int) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.AnchorSectors.cs:68)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:87)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] cap objetivos escalado: piso=4 setores=8 -> 6
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:246)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] cap inicial por produtores: unidades=0 produtores=3 cap=6->3
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:253)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Macro][T1][Red] phase=EarlyExpansion setores=0/0/8 pontos=0/0 disputa=0 ratio=50% forÁa=0v5 fr=0% offensiveCap=3
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:268)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Alpha: fora do eixo, rally ainda montando massa (sem GoGreen)
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:306)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Bravo: fronteira exige Echo antes
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:313)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Charlie: fronteira exige Echo antes
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:313)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Delta: jù tem objetivo
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:297)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Foxtrot: jù tem objetivo
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:297)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Golf: jù tem objetivo
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:297)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] skip Hotel: fronteira exige Echo antes
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:313)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] cap atingido (3): Echo descartado (pri=99)
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:444)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] base inimiga Base1 travada: nenhum rally juntou massa (GoGreen) ainda
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:496)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] SOS Base/HQ: Base2 sob captura/ameaca critica
UnityEngine.Debug:Log (object)
AIController:EnsureCriticalHomeDefenseObjectives (TeamObjectivePlan,TeamId,System.Collections.Generic.IReadOnlyList`1<SectorManager/SectorInfo>) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.Defense.cs:317)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:598)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] SOS Base/HQ live: Base2 sob captura/ameaca no conjunto da base
UnityEngine.Debug:Log (object)
AIController:EnsureCriticalHomeDefenseObjectivesFromConstructions (TeamObjectivePlan,TeamId) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.Defense.cs:396)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:599)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] cascata: Golf ? Echo (3,0h)
UnityEngine.Debug:Log (object)
AIController:MarkCascadeNeighbor1 (ConstructionSector,System.Collections.Generic.HashSet`1<ConstructionSector>,TeamId,System.Collections.Generic.HashSet`1<ConstructionSector>,AIController/AIRallyPlanContext) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.Handoff.cs:220)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:902)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Plan] Red ù 4 objetivos:
  pri=1 Base2: ù
  pri=1 Base2: ù
  pri=2 Golf: ù
  pri=3 Delta: ù
  pri=4 Foxtrot: ù
  ? 0 atribuùdos | 0 rogues
UnityEngine.Debug:Log (object)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1607)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Alpha=Avoid relation=EnemyNatural conf=0,25 reason=owner=Neutral zone=EnemyNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Bravo=Avoid relation=EnemyNatural conf=0,25 reason=owner=Neutral zone=EnemyNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Charlie=Covered relation=EnemyNatural conf=0,25 reason=covered_by_cascade owner=Neutral zone=EnemyNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Delta=Secure relation=OwnNatural conf=0,50 reason=obj=Pending/pri3 owner=Neutral zone=OwnNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Echo=Covered relation=OwnNatural conf=0,25 reason=covered_by_cascade owner=Neutral zone=OwnNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Foxtrot=Secure relation=OwnNatural conf=0,50 reason=obj=Pending/pri4 owner=Neutral zone=OwnNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Golf=Secure relation=OwnNatural conf=0,50 reason=obj=Pending/pri2 owner=Neutral zone=OwnNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Hotel=Covered relation=EnemyNatural conf=0,25 reason=covered_by_cascade owner=Neutral zone=EnemyNatural
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] Base1=Avoid relation=EnemyBase conf=0,25 reason=owner=Green zone=EnemyBase
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Intel][Intent][T1][Red][Plan] None=Avoid relation=Unknown conf=0,25 reason=sem sinal forte
UnityEngine.Debug:Log (object)
AISectorIntentAnalyzer:Log (TeamId,int,string,System.Collections.Generic.Dictionary`2<ConstructionSector, AISectorIntent>) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:229)
AISectorIntentAnalyzer:RebuildAndLog (TeamId,AIWorldSnapshot,TeamObjectivePlan,AIIntelReport,string) (at Assets/Scripts/Match/AI/2. Planner/Save and Persist/AISectorIntent.cs:76)
AIController:BuildObjectivePlan (AIWorldSnapshot) (at Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs:1609)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:147)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] BaseDefense URGENTE aircraft=0 fighterA=0 armor=0 capture=False intelHot=- slots=Assaultx1 Artilleryx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:TryBuildBaseDefenseOp (TeamId,AIWorldSnapshot,TeamObjectivePlan,System.Collections.Generic.List`1<AITacticalNeed>,AIIntelReport) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs:51)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:61)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Golf: cap=1 ass=0 fire=0 trans=0 pref=Vehicle risky=False slots=Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:TryBuildGroundCaptureOps (TeamId,AIWorldSnapshot,TeamObjectivePlan,System.Collections.Generic.List`1<AITacticalNeed>,AIIntelReport) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs:143)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:63)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Delta: cap=1 ass=0 fire=0 trans=0 pref=Vehicle risky=False slots=Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:TryBuildGroundCaptureOps (TeamId,AIWorldSnapshot,TeamObjectivePlan,System.Collections.Generic.List`1<AITacticalNeed>,AIIntelReport) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs:143)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:63)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Foxtrot: cap=1 ass=0 fire=0 trans=0 pref=Vehicle risky=False slots=Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:TryBuildGroundCaptureOps (TeamId,AIWorldSnapshot,TeamObjectivePlan,System.Collections.Generic.List`1<AITacticalNeed>,AIIntelReport) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs:143)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:63)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] BaseDefense Base2 pri=1 phase=Holding urgent=True preventive=False slots=Assaultx1 Artilleryx1 assigned=0 screen=- reason=
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:LogOperations (TeamId,int,System.Collections.Generic.List`1<AITacticalNeed>) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Units.cs:128)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:71)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Golf pri=5 phase=Forming urgent=False preventive=False slots=Capturerx1 assigned=0 screen=- reason=sem screen minimo
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:LogOperations (TeamId,int,System.Collections.Generic.List`1<AITacticalNeed>) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Units.cs:128)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:71)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Delta pri=6 phase=Forming urgent=False preventive=False slots=Capturerx1 assigned=0 screen=- reason=sem screen minimo
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:LogOperations (TeamId,int,System.Collections.Generic.List`1<AITacticalNeed>) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Units.cs:128)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:71)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] GroundCapture Foxtrot pri=7 phase=Forming urgent=False preventive=False slots=Capturerx1 assigned=0 screen=- reason=sem screen minimo
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:LogOperations (TeamId,int,System.Collections.Generic.List`1<AITacticalNeed>) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Units.cs:128)
AITacticalAnalyzer:Rebuild (TeamId,AIWorldSnapshot,TeamObjectivePlan) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:71)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:148)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Commit Heavy][T1][vermelho] reason=phase3:pre-shopping units=0 enemies=2 total=401ms
UnityEngine.Debug:Log (object)
AIController/<CommitAIWorldHeavy>d__369:MoveNext () (at Assets/Scripts/Match/AI/AIController.WorldCommit.cs:151)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] deficit BaseDefense Base2: Assaultx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:GetDeficits (TeamId,bool) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:154)
AIShoppingPlanner:BuildRoleShoppingDemands (AIWorldSnapshot,bool) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2136)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1225)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] deficit BaseDefense Base2: Artilleryx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:GetDeficits (TeamId,bool) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:154)
AIShoppingPlanner:BuildRoleShoppingDemands (AIWorldSnapshot,bool) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2136)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1225)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] deficit GroundCapture Golf: Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:GetDeficits (TeamId,bool) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:154)
AIShoppingPlanner:BuildRoleShoppingDemands (AIWorldSnapshot,bool) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2136)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1225)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] deficit GroundCapture Delta: Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:GetDeficits (TeamId,bool) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:154)
AIShoppingPlanner:BuildRoleShoppingDemands (AIWorldSnapshot,bool) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2136)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1225)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Ops][T1][Red] deficit GroundCapture Foxtrot: Capturerx1
UnityEngine.Debug:Log (object)
AITacticalAnalyzer:GetDeficits (TeamId,bool) (at Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.cs:154)
AIShoppingPlanner:BuildRoleShoppingDemands (AIWorldSnapshot,bool) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2136)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1225)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] concentra gasto: sÛ 1 prÈdio(s) disponÌvel(is) ó cada slot leva a peÁa mais forte
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1281)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] fila unica budget=6000 stance=Defensive
  pri=1 urgent=True Assalto/None x2 elite=0-2147483647 origem=operation+composition motivo=BaseDefense Base2; pacote 2/2/1 cap=0 ass=0 art=0
  pri=1 urgent=True FogoIndireto/None x1 elite=0-2147483647 origem=operation motivo=BaseDefense Base2
  pri=15 urgent=False Capturador/None x3 elite=0-2147483647 origem=operation+composition motivo=GroundCapture Golf; GroundCapture Delta; GroundCapture Foxtrot; pacote 2/2/1 cap=0 ass=0 art=0
  pri=16 urgent=False Counter/Infantry+AntiInfantaria?Infantry x1 elite=0-2147483647 origem=counter-pressure motivo=AntiInfantaria/Infantry bruto=2,1 cobertura=0,0 saldo=2,1 vis=2 memoria=0
UnityEngine.Debug:Log (object)
AIShoppingPlanner:LogRoleShoppingQueue (AIWorldSnapshot,System.Collections.Generic.List`1<AIShoppingDemand>,int) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:2910)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1287)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] expans„o econÙmica: prioriza atÈ 3 capturador(es) no carrinho antes de diversificar
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1290)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] carrinho itens=1 demandas=1 atendimentos=1 gasto=4000 saldo livre=2000 expans„oCap=0/3
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1300)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] F·brica compra Obus Leve $4000 para FogoIndireto/None origem=operation pri=1 score=448000 restante=2000
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1322)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] pendente Assalto/None x2 pri=1 origem=operation+composition motivo=BaseDefense Base2; pacote 2/2/1 cap=0 ass=0 art=0
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1359)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] pendente Capturador/None x3 pri=15 origem=operation+composition motivo=GroundCapture Golf; GroundCapture Delta; GroundCapture Foxtrot; pacote 2/2/1 cap=0 ass=0 art=0
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1359)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI Shopping Roles][T1][Red] pendente Counter/Infantry+AntiInfantaria?Infantry x1 pri=16 origem=counter-pressure motivo=AntiInfantaria/Infantry bruto=2,1 cobertura=0,0 saldo=2,1 vis=2 memoria=0
UnityEngine.Debug:Log (object)
AIShoppingPlanner:DecideRoleBased (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs:1359)
AIShoppingPlanner:Decide (AIWorldSnapshot) (at Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.cs:127)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:25)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1][Shopping] Obus Leve @ (5, -5, 0)
UnityEngine.Debug:Log (object)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:46)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[Jogadas] #2 T1 team=1 Compra uid=6 (OL) de (5,-5) para (0,0)
UnityEngine.Debug:Log (object)
JogadasLog:Registrar (Jogada) (at Assets/Scripts/Shared/Jogadas/JogadasLog.cs:49)
JogadasManager:RegistrarCompra (int,int,int,int,string,int) (at Assets/Scripts/Shared/Jogadas/JogadasManager.cs:74)
TurnStateManager:RecordShoppingBuyReplayCommand (UnityEngine.GameObject,UnitData,TeamId,UnityEngine.Vector3Int,int,int,int) (at Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs:950)
TurnStateManager:TryPurchaseShoppingUnitByIndex (int) (at Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs:290)
TurnStateManager:TryConfirmSelectedShoppingOption () (at Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs:126)
TurnStateManager:TryConfirmSelectedShoppingOptionForReplay () (at Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs:380)
ReplayManager/<ExecuteRecordedShoppingBatch>d__123:MoveNext () (at Assets/Scripts/Replay/ReplayManager.cs:1182)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[AI VERMELHO][T1] Fase3 concluÌda.
UnityEngine.Debug:Log (object)
AIController/<Phase3_Shopping>d__15:MoveNext () (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase3.cs:68)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)
