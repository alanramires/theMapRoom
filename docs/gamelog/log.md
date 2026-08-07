[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F12 AI Resume: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI] Pausa de debug encerrada. Retomando IA.

[AI Shortcuts] F12 — AI Resume

[AI] Retomando execucao da IA.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,007ms memory=0,018ms geoOnly=0,006ms unitLoop=0,145ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=118

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,445ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,694ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI Commit Heavy][T4][slot=1][vermelho] reason=turn-start units=6 enemies=0 total=2ms

[AI Perf] CommitAIWorldHeavy: 12ms

[AI VERMELHO][T4] Turno 4 | Stance: Offensive | 6 unidades | 0 inimigos visíveis | R$ 4000

[AI VERMELHO][T4][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T4][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T4][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T4] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T4] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T4] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T4] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 532ms

[AI Perf] PRE-Stage2 acumulado: 5585ms

[AI VERMELHO][T4] Fase2 — iniciando ações.

[AI VERMELHO][T4][Promessa] #9 baixa a promessa a pax=#4: passageiro embarcou.

[AI VERMELHO][T4][Promessa] #8 baixa a promessa a pax=#1: passageiro embarcou.

[AI VERMELHO][T4][FilaCarona] #4 embarcado — sai da fila apos 3 turno(s).

[AI VERMELHO][T4] Fase2 iniciativa (6 unidades):
  [grp=1] Chinook#9 @ (48, -2, 0) target=null
  [grp=2] Navio Transporte#5 @ (44, -4, 0) target=null
  [grp=4] Soldado#11 @ (52, -5, 0) target=null
  [grp=4] Soldado#10 @ (52, -6, 0) target=null
  [grp=4] APC#8 @ (49, -2, 0) target=null
  [grp=4] APC#3 @ (56, -2, 0) target=null


[AI Perf][InitiativeSetup] total=14,4ms available=0,1ms snapshot=0,2ms repair=12,0ms groups=0,5ms facts=1,5ms sort=0,0ms log=0,1ms

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#10 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#10 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte] PassengerTarget #1 MISSAO (25, -2, 0) verbo=Capture

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#8 (carregado) Unidade nao alcanca destino da carga (25, -2, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#8 slot=1 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Unidade nao alcanca destino da carga (25, -2, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#3 (carregado) Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#3 slot=1 carona=Requested ajuste=1600 fila=1t motivo=Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=591; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#9 confirmedRev=22 reach=112 rideNeeds=4 tiers=3 options=2364 ranking=0

[AI VERMELHO][T4][Transporte] 9 Pickup[Tactical] recusa 2364 opcoes: carona=Requested!=Emergency=117 · rotaPax=ReachableLater=95 · rotaPax=ReachableStrategic=40 · tier=Operational!=Tactical=416 · tier=Strategic!=Tactical=1696

[AI Reach][Transport:9:Evac] Tactical:miss budget=6

[AI Reach][Transport:9:Evac] Operational:disabled

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Tactical] miss

[AI Reach][Transport:9:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 9 Pickup[Operational] recusa 2364 opcoes: tier=Tactical!=Operational=252 · carona=Requested!=Emergency=258 · rotaPax=ReachableStrategic=158 · tier=Strategic!=Operational=1696

[AI Reach][Transport:9:Evac] Operational:miss budget=12

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Operational] miss

[AI Reach][Transport:9:Pickup] Tactical:hit budget=6 action=(48, -2, 0) target=(49, -2, 0) score=101799 reason=passageiro=#8 encontro=(48, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=0+1=1 dist=0

[TransportOps][Unit#9][Pickup][Tactical] hit passageiro=#8 encontro=(48, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=0+1=1 dist=0

[AI VERMELHO][T4][Transporte] 9 pickup Tactical: aguarda na LZ (48, -2, 0) passageiro=#8 carona=Requested rotaPax=ReachableNow.

[FrameSpike] frame=5955 duration=784,35ms state=Neutral substep=AwaitingAction selected=(none) boardRev=390 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=714,0MB managedDelta=+14,3MB gcDelta=[0,0,0] unityAlloc=977,4MB

[RangeCache] MISS - reason: empty key | unit=Chinook_T1_U9 unitId=-42530 mp=6 fuel=34 rev=390

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Chinook_T1_U9 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Chinook_T1_U9

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Chinook_T1_U9

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=Chinook_T1_U9

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=Chinook_T1_U9 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Neutral

[TurnState] transition=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=1 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,011ms knownCells=0,007ms memory=0,016ms geoOnly=0,006ms unitLoop=0,149ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=118

[FoW][Perf][Incremental] unit=Chinook_T1_U9 total=1,927ms updateCache=0,004ms collect=0,000ms collected=False cells=0 render=0,745ms visibility=0,458ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,603ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Perf][Unit][T4][Red] Chinook#9 action=wait decision=768ms execution=105ms snapshot=0ms delay=500ms total=1374ms stages=transportPlanning:768,2ms/1,melhorEmbarque:735,2ms/1,melhorEmbarque.lzLoop:667,9ms/1,melhorEmbarque.lzGates:476,4ms/1,melhorEmbarque.resolveMeeting:164,4ms/1,melhorEmbarque.passengerReach:61,7ms/1,queroCarona:47,2ms/4,validPaths:45,5ms/6,movementCostMap:26,9ms/12,melhorEmbarque.longRangeMap:13,9ms/4,turnChainedCostMap:9,0ms/5,melhorCaptura:7,2ms/6,ownMovementComponent:0,3ms/1,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:1,CellsVisited:4394,MelhorCapturaCalls:6,MelhorCapturaCandidates:66,MelhorCapturaOutOfBandSkips:44,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:5,MelhorCapturaTargets:22,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:1492,MelhorEmbarqueEmbarkProbes:872,MelhorEmbarqueLongRangeMapBuilds:4,MelhorEmbarqueLzGateProbes:1050,MelhorEmbarqueLzGateRejects:459,MelhorEmbarquePairs:2364,MelhorEmbarquePairsNoRoute:1492,MelhorEmbarquePairsReachableLater:308,MelhorEmbarquePairsReachableNow:228,MelhorEmbarquePairsStrategic:336,MelhorEmbarquePassengers:4,MobilityComponentBuilds:1,MobilityComponentHits:5,MovementCacheHits:1,MovementCacheMisses:17,MovementCacheStores:17,MovementCostCellsExpanded:1748,MovementCostWaves:12,MovementQueryCachesBuilt:278,MovementQueryConfirmedOccupancyUses:278,MovementWavesBuilt:17,OwnMovementComponentBuilds:1,PathStatesExpanded:969,QueroCaronaCacheMisses:4,QueroCaronaCacheStores:4,QueroCaronaCalls:4,QueroCaronaCaptureReachBuilds:4,QueroCaronaMobilityComponentHits:4,ReachableCellsProduced:2724,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:627,ValidPathCacheHits:1,ValidPathWaves:5

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#10 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#10 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#3 (carregado) Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#3 slot=0 carona=Requested ajuste=1600 fila=1t motivo=Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=16; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#5 confirmedRev=22 reach=48 rideNeeds=3 tiers=3 options=48 ranking=0

[AI VERMELHO][T4][Transporte] 5 Pickup[Tactical] recusa 48 opcoes: rotaPax=ReachableLater=6 · rotaPax=ReachableStrategic=9 · tier=Operational!=Tactical=9 · tier=Strategic!=Tactical=24

[AI Reach][Transport:5:Evac] Tactical:miss budget=5

[AI Reach][Transport:5:Evac] Operational:disabled

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Tactical] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Operational] recusa 48 opcoes: tier=Tactical!=Operational=15 · rotaPax=ReachableStrategic=9 · tier=Strategic!=Operational=24

[AI Reach][Transport:5:Evac] Operational:miss budget=10

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Operational] miss

[AI VERMELHO][T4][Transporte] 5 Pickup[Tactical] recusa 48 opcoes: rotaPax=ReachableLater=6 · rotaPax=ReachableStrategic=9 · tier=Operational!=Tactical=9 · tier=Strategic!=Tactical=24

[AI Reach][Transport:5:Pickup] Tactical:miss budget=5

[AI Reach][Transport:5:Pickup] Operational:disabled

[AI Reach][Transport:5:Pickup] Strategic:disabled

[TransportOps][Unit#5][Pickup][Tactical] miss

[AI Reach][Transport:5:Pickup] Tactical:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Operational] recusa 48 opcoes: tier=Tactical!=Operational=15 · rotaPax=ReachableStrategic=9 · tier=Strategic!=Operational=24

[AI Reach][Transport:5:Pickup] Operational:miss budget=10

[AI Reach][Transport:5:Pickup] Strategic:disabled

[TransportOps][Unit#5][Pickup][Operational] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI Reach][Transport:5:Evac] Operational:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Strategic] recusa 48 opcoes: tier=Tactical!=Strategic=15 · tier=Operational!=Strategic=9 · carona=Requested!=Emergency=6 · rotaPax=NoCurrentRoute=18

[AI Reach][Transport:5:Evac] Strategic:miss budget=2147483647

[TransportOps][Unit#5][Evac][Strategic] miss

[AI Reach][Transport:5:Pickup] Tactical:disabled

[AI Reach][Transport:5:Pickup] Operational:disabled

[AI Reach][Transport:5:Pickup] Strategic:hit budget=2147483647 action=(45, 7, 0) target=(52, -6, 0) score=95686 reason=passageiro=#10 encontro=(45, 7, 0) tier=Strategic carona=Requested rotaPax=ReachableStrategic custoPax=13+1=14 dist=11

[TransportOps][Unit#5][Pickup][Strategic] hit passageiro=#10 encontro=(45, 7, 0) tier=Strategic carona=Requested rotaPax=ReachableStrategic custoPax=13+1=14 dist=11

[AI][Progressao2][Top] unit=5 intent=TransportRendezvous from=(44, -4, 0) target=(45, 7, 0) best=(44, 1, 0) final=130000 candidatos=47 skips origin=1 occupied=0 stop=0 allow=0 score=0
  #1 (44, 1, 0) final=130000 tool=130 next=1,0 move=5 road=False prog=5,0/10,0 route=? progR=? line=0,5 threat=0,0 dpq=1,0 tactical=0
  #2 (43, 1, 0) final=129000 tool=129 next=1,0 move=5 road=False prog=5,0/10,0 route=? progR=? line=1,4 threat=0,0 dpq=1,0 tactical=0
  #3 (42, 1, 0) final=128000 tool=128 next=1,0 move=5 road=False prog=5,0/10,0 route=? progR=? line=2,4 threat=0,0 dpq=1,0 tactical=0
  #4 (44, 0, 0) final=114000 tool=114 next=2,0 move=4 road=False prog=4,0/9,0 route=? progR=? line=0,4 threat=0,0 dpq=1,0 tactical=0
  #5 (43, 0, 0) final=113000 tool=113 next=2,0 move=4 road=False prog=4,0/9,0 route=? progR=? line=1,4 threat=0,0 dpq=1,0 tactical=0
  #6 (42, 0, 0) final=112000 tool=112 next=2,0 move=4 road=False prog=4,0/9,0 route=? progR=? line=2,4 threat=0,0 dpq=1,0 tactical=0
  #7 (41, 1, 0) final=111000 tool=111 next=2,0 move=5 road=False prog=4,0/9,0 route=? progR=? line=3,4 threat=0,0 dpq=1,0 tactical=0
  #8 (43, -1, 0) final=97000 tool=97 next=3,0 move=3 road=False prog=3,0/8,0 route=? progR=? line=1,3 threat=0,0 dpq=1,0 tactical=0
  #9 (42, -1, 0) final=96000 tool=96 next=3,0 move=3 road=False prog=3,0/8,0 route=? progR=? line=2,3 threat=0,0 dpq=1,0 tactical=0
  #10 (41, 0, 0) final=95000 tool=95 next=3,0 move=5 road=False prog=3,0/8,0 route=? progR=? line=3,3 threat=0,0 dpq=1,0 tactical=0
  #11 (41, -1, 0) final=95000 tool=95 next=3,0 move=4 road=False prog=3,0/8,0 route=? progR=? line=3,3 threat=0,0 dpq=1,0 tactical=0
  #12 (40, -1, 0) final=78000 tool=78 next=4,0 move=5 road=False prog=2,0/7,0 route=? progR=? line=4,3 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T4][Transporte] 5 pickup Strategic: progride para MelhorEmbarque LZ=(45, 7, 0) via=(44, 1, 0) passageiro=#10 (toolIntent=TransportRendezvous tool=130 next=1,0 moveCost=5 roadBonus=False prog=5,0/10,0 route=? progR=? line=0,5 dpq=1,0 threat=0,0 tactical=0 final=130000).

[FrameSpike] frame=6014 duration=258,87ms state=Neutral substep=AwaitingAction selected=(none) boardRev=414 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=721,4MB managedDelta=+4,9MB gcDelta=[0,0,0] unityAlloc=977,4MB

[RangeCache] MISS - reason: empty key | unit=Navio Transporte_T1_U5 unitId=-32740 mp=5 fuel=75 rev=414

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Navio Transporte_T1_U5

moveu para 44,1

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,008ms memory=0,016ms geoOnly=0,006ms unitLoop=0,144ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=117

[FoW][Perf][Incremental] unit=Navio Transporte_T1_U5 total=10,944ms updateCache=8,849ms collect=8,665ms collected=True cells=24 render=0,824ms visibility=0,472ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,685ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Navio Transporte#5 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Navio Transporte#5 action=move decision=245ms execution=572ms snapshot=0ms delay=500ms total=1317ms stages=transportPlanning:188,0ms/1,melhorEmbarque:183,1ms/1,melhorEmbarque.lzLoop:178,3ms/1,melhorEmbarque.lzGates:169,6ms/1,toolProgression.TransportRendezvous:56,4ms/1,validPaths:39,7ms/13,melhorEmbarque.resolveMeeting:7,2ms/1,movementCostMap:5,4ms/10,melhorEmbarque.passengerReach:3,7ms/1,melhorEmbarque.longRangeMap:1,9ms/3,queroCarona:0,3ms/3,melhorEmbarque.candidateCells:0,0ms/1,melhorEmbarque.transporterPaths:0,0ms/1 metrics=CellsVisited:3466,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:18,MelhorEmbarqueEmbarkProbes:30,MelhorEmbarqueLongRangeMapBuilds:3,MelhorEmbarqueLzGateProbes:1028,MelhorEmbarqueLzGateRejects:1012,MelhorEmbarquePairs:48,MelhorEmbarquePairsNoRoute:18,MelhorEmbarquePairsReachableLater:6,MelhorEmbarquePairsStrategic:24,MelhorEmbarquePassengers:3,MobilityComponentHits:10,MobilityComponentTouchTests:2,MovementCacheBypasses:12,MovementCacheHits:9,MovementCacheMisses:14,MovementCacheStores:2,MovementCostCacheHits:9,MovementCostCellsExpanded:48,MovementCostWaves:1,MovementQueryCachesBuilt:61,MovementQueryConfirmedOccupancyUses:27,MovementQueryLiveOccupancyFallbacks:34,MovementWavesBuilt:14,PathStatesExpanded:2368,QueroCaronaCacheHits:3,QueroCaronaCalls:3,ReachableCellsProduced:1018,ToolProgressionCubicDirectionUses:1,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:5,ValidPathWaves:13

[AI VERMELHO][T4][Capturador] 11 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 11 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  heli/trans 9@(48, -2, 0) dist=6 sector=free formal=- slot=0 same=False compat=False prod=False acted=True repair=False empty
  heli/trans 8@(49, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo
  heli/trans 3@(56, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo


[AI VERMELHO][T4][SemPlano] 11 sem capturável alcançável a pé — cai no fluxo normal (carona ou HexEvaluator).

[AI VERMELHO][T4][Capturador] 11 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 11 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  heli/trans 9@(48, -2, 0) dist=6 sector=free formal=- slot=0 same=False compat=False prod=False acted=True repair=False empty
  heli/trans 8@(49, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo
  heli/trans 3@(56, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo


[AI] 11 ? move para (49, -5, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U11 unitId=-43002 mp=3 fuel=61 rev=419

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U11

moveu para 49,-5

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,008ms memory=0,018ms geoOnly=0,006ms unitLoop=0,144ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=119

[FoW][Perf][Incremental] unit=Soldado_T1_U11 total=8,414ms updateCache=6,428ms collect=6,374ms collected=True cells=28 render=0,778ms visibility=0,481ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,620ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#11 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#11 action=move decision=69ms execution=363ms snapshot=0ms delay=506ms total=938ms stages=melhorCaptura:12,3ms/4,validPaths:11,4ms/10,opportunistic:4,5ms/2,turnChainedCostMap:3,8ms/3,queroCarona:2,9ms/2 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:5,CellsVisited:320,MelhorCapturaCalls:4,MelhorCapturaCandidates:44,MelhorCapturaOutOfBandSkips:33,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MobilityComponentHits:11,MovementCacheHits:8,MovementCacheMisses:2,MovementCacheStores:2,MovementQueryCachesBuilt:1328,MovementQueryConfirmedOccupancyUses:1328,MovementWavesBuilt:2,PathStatesExpanded:90,QueroCaronaCacheHits:1,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:2,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:280,TurnChainedCellsExpanded:230,ValidPathCacheHits:8,ValidPathWaves:2

[AI VERMELHO][T4][Capturador] 10 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 10 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  heli/trans 9@(48, -2, 0) dist=6 sector=free formal=- slot=0 same=False compat=False prod=False acted=True repair=False empty
  heli/trans 8@(49, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo
  heli/trans 3@(56, -2, 0) dist=6 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo


[AI VERMELHO][T4][SemPlano] 10 sem capturável alcançável a pé — cai no fluxo normal (carona ou HexEvaluator).

[AI VERMELHO][T4][Capturador] 10 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 10 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  heli/trans 9@(48, -2, 0) dist=6 sector=free formal=- slot=0 same=False compat=False prod=False acted=True repair=False empty
  heli/trans 8@(49, -2, 0) dist=5 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo
  heli/trans 3@(56, -2, 0) dist=6 sector=free formal=- slot=-1 same=False compat=False prod=False acted=False repair=False cargo


[AI] 10 ? move para (49, -6, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U10 unitId=-42766 mp=3 fuel=61 rev=422

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U10

moveu para 49,-6

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,008ms memory=0,016ms geoOnly=0,006ms unitLoop=0,141ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=114

[FoW][Perf][Incremental] unit=Soldado_T1_U10 total=3,483ms updateCache=1,535ms collect=1,459ms collected=True cells=22 render=0,781ms visibility=0,448ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,607ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#10 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#10 action=move decision=57ms execution=348ms snapshot=0ms delay=504ms total=909ms stages=melhorCaptura:9,7ms/4,validPaths:9,1ms/10,opportunistic:3,6ms/2,turnChainedCostMap:3,5ms/3,queroCarona:2,7ms/2 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:5,CellsVisited:289,MelhorCapturaCalls:4,MelhorCapturaCandidates:44,MelhorCapturaOutOfBandSkips:33,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MobilityComponentHits:11,MovementCacheHits:8,MovementCacheMisses:2,MovementCacheStores:2,MovementQueryCachesBuilt:1034,MovementQueryConfirmedOccupancyUses:1034,MovementWavesBuilt:2,PathStatesExpanded:90,QueroCaronaCacheHits:1,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:2,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:249,TurnChainedCellsExpanded:199,ValidPathCacheHits:8,ValidPathWaves:2

[AI VERMELHO][T4][Transporte] PassengerTarget #1 MISSAO (25, -2, 0) verbo=Capture

[AI Reach][Transport:8:Courier] Tactical:miss budget=6

[AI Reach][Transport:8:Courier] Operational:disabled

[AI Reach][Transport:8:Courier] Strategic:disabled

[TransportOps][Unit#8][Courier][Tactical] miss

[AI Reach][Transport:8:Delivery] Tactical:disabled

[AI VERMELHO][T4][Transporte] PassengerTarget #1 MISSAO (25, -2, 0) verbo=Capture

[AI Reach][Transport:8:Delivery] Operational:hit budget=12 action=(25, -2, 0) target=(25, -2, 0) score=90000 reason=carga embarcada count=1 dist=24

[TransportOps][Unit#8][Delivery][Operational] hit carga embarcada count=1 dist=24

[AI VERMELHO][T4][Transporte] PassengerTarget #1 MISSAO (25, -2, 0) verbo=Capture

[AI VERMELHO][T4][Transporte] 8 courier — passageiro #1 alvo=(25, -2, 0) range=6 (Operational; Tactical=3) distAtual=24h

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#1

[AI VERMELHO][T4][Transporte] alvo conjunto por missao: passageiro #1 intent=Capture -> (25, -2, 0).

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:8] Tactical:miss budget=6

[AI Reach][TransportDelivery:8] Operational:disabled

[AI Reach][TransportDelivery:8] Strategic:disabled

[AI Reach][TransportDelivery:8] Tactical:disabled

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(46, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(46, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(57, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:8] Operational:miss budget=12

[AI Reach][TransportDelivery:8] Strategic:hit budget=120 action=(25, -2, 0) target=(25, -2, 0) score=-24 reason=cubic=24

[AI][Progressao2][Top] unit=8 intent=TransportDelivery from=(49, -2, 0) target=(25, -2, 0) best=(47, -2, 0) final=32000 candidatos=74 skips origin=1 occupied=3 stop=0 allow=0 score=0
  #1 (47, -2, 0) final=32000 tool=32 next=22,0 move=2 road=False prog=2,0/2,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #2 (46, -3, 0) final=31000 tool=31 next=22,0 move=3 road=False prog=2,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #3 (47, -3, 0) final=25000 tool=25 next=22,0 move=2 road=False prog=1,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #4 (47, -4, 0) final=24000 tool=24 next=22,0 move=3 road=False prog=1,0/2,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #5 (46, -5, 0) final=23000 tool=23 next=22,0 move=4 road=False prog=1,0/2,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #6 (48, -3, 0) final=19000 tool=19 next=22,0 move=1 road=False prog=0,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #7 (48, -1, 0) final=19000 tool=19 next=22,0 move=6 road=False prog=0,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=3,0 tactical=0
  #8 (48, -4, 0) final=18000 tool=18 next=22,0 move=2 road=False prog=0,0/2,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #9 (47, -5, 0) final=17000 tool=17 next=22,0 move=3 road=False prog=0,0/2,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #10 (47, -6, 0) final=16000 tool=16 next=22,0 move=4 road=False prog=0,0/2,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #11 (47, 3, 0) final=-1000 tool=-1 next=23,0 move=5 road=False prog=-1,0/1,0 route=? progR=? line=5,0 threat=0,0 dpq=1,0 tactical=0
  #12 (47, 4, 0) final=-12000 tool=-12 next=24,0 move=6 road=False prog=-1,0/0,0 route=? progR=? line=6,0 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T4][Transporte] Terrestre 8 reach estrategico: alvo cubico (25, -2, 0) -> ancora (25, -2, 0); progride (49, -2, 0)->(47, -2, 0) (toolIntent=TransportDelivery tool=32 next=22,0 moveCost=2 roadBonus=False prog=2,0/2,0 route=? progR=? line=0,0 dpq=1,0 threat=0,0 tactical=0 final=32000).

[AI VERMELHO][T4][Transporte] 8 larga no TACTICAL (rota restante <= 3).

[FrameSpike] frame=6272 duration=2722,28ms state=Neutral substep=AwaitingAction selected=(none) boardRev=473 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=688,6MB managedDelta=-53,8MB gcDelta=[1,1,1] unityAlloc=976,2MB

[RangeCache] MISS - reason: empty key | unit=APC_T1_U8 unitId=-39636 mp=6 fuel=53 rev=473

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U8 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U8

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U8

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U8

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U8

moveu para 47,-2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U8

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U8 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,011ms knownCells=0,007ms memory=0,016ms geoOnly=0,006ms unitLoop=0,140ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=111

[FoW][Perf][Incremental] unit=APC_T1_U8 total=6,494ms updateCache=4,535ms collect=4,444ms collected=True cells=24 render=0,795ms visibility=0,449ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,604ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T4][Stock] 8 confirma missao Transport destino=(25, -2, 0) unit=#1 construction=#-1 tier=None.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:APC#8 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] APC#8 action=move decision=2705ms execution=298ms snapshot=0ms delay=501ms total=3504ms stages=melhorDesembarque:430,7ms/2,validPaths:299,8ms/14,toolProgression.TransportDelivery:222,7ms/1,movementCostMap:9,8ms/5,transportPlanning:0,5ms/1 metrics=CellsVisited:3858,DisembarkLzCellsVisited:271,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MovementCacheBypasses:12,MovementCacheMisses:19,MovementCacheStores:7,MovementCostCellsExpanded:371,MovementCostWaves:5,MovementQueryCachesBuilt:93,MovementQueryConfirmedOccupancyUses:10,MovementQueryLiveOccupancyFallbacks:83,MovementWavesBuilt:19,PathStatesExpanded:3216,ReachableCellsProduced:1250,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathWaves:14

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI Reach][Transport:3:Courier] Tactical:miss budget=6

[AI Reach][Transport:3:Courier] Operational:disabled

[AI Reach][Transport:3:Courier] Strategic:disabled

[TransportOps][Unit#3][Courier][Tactical] miss

[AI Reach][Transport:3:Delivery] Tactical:disabled

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI Reach][Transport:3:Delivery] Operational:hit budget=12 action=(20, 6, 0) target=(20, 6, 0) score=90000 reason=carga embarcada count=1 dist=40

[TransportOps][Unit#3][Delivery][Operational] hit carga embarcada count=1 dist=40

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T4][Transporte] 3 courier — passageiro #4 alvo=(20, 6, 0) range=6 (Operational; Tactical=3) distAtual=40h

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#4

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#4

[AI VERMELHO][T4][Transporte] alvo conjunto por missao: passageiro #4 intent=Pressure -> (20, 6, 0).

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Tactical:miss budget=6

[AI Reach][TransportDelivery:3] Operational:disabled

[AI Reach][TransportDelivery:3] Strategic:disabled

[AI Reach][TransportDelivery:3] Tactical:disabled

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(57, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(58, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(57, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(57, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(56, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(55, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(54, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(53, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(52, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(51, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(50, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(47, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(48, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(49, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Operational:miss budget=12

[AI Reach][TransportDelivery:3] Strategic:hit budget=120 action=(20, 6, 0) target=(20, 6, 0) score=-40 reason=cubic=40

[AI][Progressao2][Top] unit=3 intent=TransportDelivery from=(56, -2, 0) target=(20, 6, 0) best=(50, -1, 0) final=156000 candidatos=110 skips origin=1 occupied=0 stop=0 allow=0 score=0
  #1 (50, -1, 0) final=156000 tool=156 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=0,3 threat=0,0 dpq=1,0 tactical=0
  #2 (50, -2, 0) final=155000 tool=155 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=1,3 threat=0,0 dpq=1,0 tactical=0
  #3 (51, 0, 0) final=155000 tool=155 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=0,9 threat=0,0 dpq=1,0 tactical=0
  #4 (51, 1, 0) final=154000 tool=154 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=1,8 threat=0,0 dpq=1,0 tactical=0
  #5 (52, 2, 0) final=153000 tool=153 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #6 (52, 3, 0) final=152000 tool=152 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #7 (53, 4, 0) final=151000 tool=151 next=28,0 move=6 road=False prog=6,0/12,0 route=? progR=? line=5,2 threat=0,0 dpq=1,0 tactical=0
  #8 (51, -1, 0) final=140000 tool=140 next=29,0 move=5 road=False prog=5,0/11,0 route=? progR=? line=0,1 threat=0,0 dpq=1,0 tactical=0
  #9 (51, -2, 0) final=139000 tool=139 next=29,0 move=5 road=False prog=5,0/11,0 route=? progR=? line=1,1 threat=0,0 dpq=1,0 tactical=0
  #10 (52, 0, 0) final=139000 tool=139 next=29,0 move=5 road=False prog=5,0/11,0 route=? progR=? line=1,1 threat=0,0 dpq=1,0 tactical=0
  #11 (52, 1, 0) final=138000 tool=138 next=29,0 move=5 road=False prog=5,0/11,0 route=? progR=? line=2,1 threat=0,0 dpq=1,0 tactical=0
  #12 (50, -3, 0) final=138000 tool=138 next=29,0 move=6 road=False prog=5,0/11,0 route=? progR=? line=2,3 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T4][Transporte] Terrestre 3 reach estrategico: alvo cubico (20, 6, 0) -> ancora (20, 6, 0); progride (56, -2, 0)->(50, -1, 0) (toolIntent=TransportDelivery tool=156 next=28,0 moveCost=6 roadBonus=False prog=6,0/12,0 route=? progR=? line=0,3 dpq=1,0 threat=0,0 tactical=0 final=156000).

[AI VERMELHO][T4][Transporte] 3 larga no TACTICAL (rota restante <= 3).

[FrameSpike] frame=6343 duration=3059,95ms state=Neutral substep=AwaitingAction selected=(none) boardRev=525 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=711,9MB managedDelta=+21,6MB gcDelta=[0,0,0] unityAlloc=976,4MB

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=47144 mp=6 fuel=60 rev=525

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 50,-1

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,009ms memory=0,019ms geoOnly=0,007ms unitLoop=0,185ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=95

[FoW][Perf][Incremental] unit=APC_T1_U3 total=10,485ms updateCache=7,765ms collect=7,474ms collected=True cells=36 render=1,156ms visibility=0,599ms intel=0,003ms detectionSfx=0,026ms persistence=0,001ms callbacks=0,812ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T4][Stock] 3 confirma missao Transport destino=(20, 6, 0) unit=#4 construction=#-1 tier=None.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:APC#3 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] APC#3 action=move decision=3042ms execution=701ms snapshot=0ms delay=509ms total=4252ms stages=melhorDesembarque:581,4ms/2,toolProgression.TransportDelivery:182,5ms/1,validPaths:173,5ms/14,movementCostMap:11,8ms/5,transportPlanning:0,5ms/1 metrics=CellsVisited:6660,DisembarkLzCellsVisited:328,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MovementCacheBypasses:12,MovementCacheMisses:19,MovementCacheStores:7,MovementCostCellsExpanded:867,MovementCostWaves:5,MovementQueryCachesBuilt:129,MovementQueryConfirmedOccupancyUses:76,MovementQueryLiveOccupancyFallbacks:53,MovementWavesBuilt:19,PathStatesExpanded:5465,ReachableCellsProduced:2486,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathWaves:14

[AI Perf][Phase2 Breakdown][T4][Red] decisions=6 completed=6
  decision=6887ms execution=2386ms snapshot=0ms delay=3020ms measuredTotal=12293ms
  boardQueries stages=melhorDesembarque:1012,1ms/4,transportPlanning:957,3ms/4,melhorEmbarque:918,3ms/2,melhorEmbarque.lzLoop:846,2ms/2,melhorEmbarque.lzGates:646,0ms/2,validPaths:579,1ms/67,toolProgression.TransportDelivery:405,2ms/2,melhorEmbarque.resolveMeeting:171,6ms/2,melhorEmbarque.passengerReach:65,4ms/2,toolProgression.TransportRendezvous:56,4ms/1,movementCostMap:53,9ms/32,queroCarona:53,1ms/11,melhorCaptura:29,2ms/14,turnChainedCostMap:16,3ms/11,melhorEmbarque.longRangeMap:15,9ms/7,opportunistic:8,2ms/4,ownMovementComponent:0,3ms/1,melhorEmbarque.candidateCells:0,0ms/2,melhorEmbarque.transporterPaths:0,0ms/2 metrics=CaptureClaimSnapshotBuilds:3,CaptureClaimSnapshotHits:11,CellsVisited:18987,DisembarkLzCellsVisited:599,MelhorCapturaCalls:14,MelhorCapturaCandidates:154,MelhorCapturaOutOfBandSkips:110,MelhorCapturaReachBuilds:5,MelhorCapturaReachReuses:9,MelhorCapturaTargets:44,MelhorDesembarqueCalls:4,MelhorDesembarquePassengerRouteBuilds:4,MelhorDesembarqueStructuralLzCandidates:4200,MelhorEmbarqueCalls:2,MelhorEmbarqueCandidateCells:2100,MelhorEmbarqueEmbarkProbeSkips:1510,MelhorEmbarqueEmbarkProbes:902,MelhorEmbarqueLongRangeMapBuilds:7,MelhorEmbarqueLzGateProbes:2078,MelhorEmbarqueLzGateRejects:1471,MelhorEmbarquePairs:2412,MelhorEmbarquePairsNoRoute:1510,MelhorEmbarquePairsReachableLater:314,MelhorEmbarquePairsReachableNow:228,MelhorEmbarquePairsStrategic:360,MelhorEmbarquePassengers:7,MobilityComponentBuilds:1,MobilityComponentHits:37,MobilityComponentTouchTests:2,MovementCacheBypasses:36,MovementCacheHits:26,MovementCacheMisses:73,MovementCacheStores:37,MovementCostCacheHits:9,MovementCostCellsExpanded:3034,MovementCostWaves:23,MovementQueryCachesBuilt:2923,MovementQueryConfirmedOccupancyUses:2753,MovementQueryLiveOccupancyFallbacks:170,MovementWavesBuilt:73,OwnMovementComponentBuilds:1,PathStatesExpanded:12198,QueroCaronaCacheHits:5,QueroCaronaCacheMisses:6,QueroCaronaCacheStores:6,QueroCaronaCalls:11,QueroCaronaCaptureReachBuilds:6,QueroCaronaMobilityComponentHits:6,ReachableCellsProduced:8007,ToolProgressionCubicDirectionUses:3,TopologyCellsVisited:2100,TopologyIndexCandidateCells:2100,TopologyIndexHits:6,TopologyIndexQueries:6,TransportPlanningCalls:4,TransportPlanningReachReuses:4,TransportPlanningSnapshotBuilds:2,TransportPlanningSnapshotHits:7,TurnChainedCellsExpanded:1056,ValidPathCacheHits:17,ValidPathWaves:50
  #1 APC#3 action=move total=4252ms decision=3042 execution=701 snapshot=0 delay=509
  #2 APC#8 action=move total=3504ms decision=2705 execution=298 snapshot=0 delay=501
  #3 Chinook#9 action=wait total=1374ms decision=768 execution=105 snapshot=0 delay=500
  #4 Navio Transporte#5 action=move total=1317ms decision=245 execution=572 snapshot=0 delay=500
  #5 Soldado#11 action=move total=938ms decision=69 execution=363 snapshot=0 delay=506


[AI VERMELHO][T4] Fase2 concluída — todas as 6 unidades agiram.

[AI Perf] Stage2 (actions): 12454ms

[AI VERMELHO][T4] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,010ms knownCells=0,007ms memory=0,015ms geoOnly=0,005ms unitLoop=0,141ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=95

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,417ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,674ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=5 bases=1 constructions=11 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=537 sectors=5 bases=1 total=1,0ms

[SectorManager][Perf][Steps] contexts=0,5ms (search=0,0ms calls=0) sectorLoop=0,3ms neighborPass=0,1ms | search.calls=42 search.ms=0,1 search.hits=42 search.failures=22 search.exhausted=0 search.expanded=0 cache.size=36 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=165/0 tile=425/0 | constructions=11 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 11ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI VERMELHO][T4][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T4][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T4][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T4][slot=1][vermelho] reason=phase3:pre-shopping units=6 enemies=0 total=14ms

[AI Shopping Roles][T4][Red] fila de carona: 2 esperando e 4 transportador(es) em campo — sem demanda de transporte.

[AI Shopping Roles][T4][Red] doutrina rebelde: Capturador + transporte derivado da fila de carona — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T4][Red] fila unica budget=4000 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T4][Red] expansão econômica: prioriza até 2 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T4][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=4000 — caixa preservado

[AI Shopping Roles][T4][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T4] Fase3 concluída.

[AI Perf] Stage3 (shopping): 15ms

[AI VERMELHO][T4] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 33ms

[AI Perf] TURNO TOTAL (Red): 18133ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=9 ms=2,329

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=11 ms=0,612

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=4,424

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,390

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,005

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,046

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,257

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,492

[FoW][Perf][Publish] slot=0 contributors=0,003ms knownCells=0,027ms memory=0,010ms geoOnly=0,002ms unitLoop=0,165ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=6 knownCells.count=35

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=3 constructions.removed=3 total=0,450ms

[FoW][Perf][Publish] slot=1 contributors=0,010ms knownCells=0,007ms memory=0,013ms geoOnly=0,005ms unitLoop=0,134ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=95

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=1,378ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=3,116

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,017

[TurnPerf] etapa=ApplyActiveTeam.Total ms=8,780

[TurnPerf] etapa=AdvanceTurn ms=9,040

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=113,440

[FoW][Warmup] host=0 slots=0 sources=0 total=23,101ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=23,1ms

