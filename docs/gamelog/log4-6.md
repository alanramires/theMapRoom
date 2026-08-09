[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> EndingTurn

[TurnState] transition=Neutral -> EndingTurn | reason=TryOpenEndingTurnConfirmation | selected=(none) | stack=Neutral > EndingTurn

[FSM][Enter] Neutral -> EndingTurn | reason=TryOpenEndingTurnConfirmation

[FSM] Estado: EndingTurn -> EndingTurnExecuting

[TurnState] transition=EndingTurn -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromConfirmation | selected=(none) | stack=Neutral > EndingTurn > EndingTurnExecuting

[FSM][Enter] EndingTurn -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromConfirmation

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromConfirmation: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromConfirmation: dispatched

[AI] HandleTeamChanged — teamIndex=1 newTeam=Red matchController=True isAI=True

[AI] RunAITurn iniciado para Red.

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=10 ms=2,743

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=12 ms=0,868

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=5,578

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,691

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,048

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=5,359

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,001

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=5,610

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,018ms memory=0,022ms geoOnly=0,007ms unitLoop=0,168ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=151

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=7 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=1,511ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=2,872

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,003

[TurnPerf] etapa=ApplyActiveTeam.Total ms=15,100

[TurnPerf] etapa=AdvanceTurn ms=15,353

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=117,404

[AI VERMELHO][T3] Fase0 concluída.

[AI Perf] Stage0 (wait): 528ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,016ms memory=0,023ms geoOnly=0,007ms unitLoop=0,167ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=151

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=7 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,513ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,778ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI Commit Heavy][T4][slot=1][vermelho] reason=turn-start units=7 enemies=0 total=2ms

[AI Perf] CommitAIWorldHeavy: 8ms

[AI VERMELHO][T4] Turno 4 | Stance: Offensive | 7 unidades | 0 inimigos visíveis | R$ 8000

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

[AI Perf] Stage1 (command): 522ms

[AI Perf] PRE-Stage2 acumulado: 1114ms

[AI VERMELHO][T4] Fase2 — iniciando ações.

[AI VERMELHO][T4][Promessa] #9 baixa a promessa a pax=#4: passageiro embarcou.

[AI VERMELHO][T4][Promessa] #8 baixa a promessa a pax=#1: passageiro embarcou.

[AI VERMELHO][T4][FilaCarona] #4 embarcado — sai da fila apos 3 turno(s).

[AI VERMELHO][T4] Fase2 iniciativa (7 unidades):
  [grp=1] Chinook#9 @ (48, -2, 0) target=null
  [grp=2] Navio Transporte#5 @ (44, -4, 0) target=null
  [grp=4] Soldado#11 @ (52, -5, 0) target=null
  [grp=4] Soldado#10 @ (52, -6, 0) target=null
  [grp=4] Avião Tanque#13 @ (26, 6, 0) target=null
  [grp=4] APC#8 @ (49, -2, 0) target=null
  [grp=4] APC#3 @ (56, -2, 0) target=null


[AI Perf][InitiativeSetup] total=15,4ms available=0,1ms snapshot=0,2ms repair=12,5ms groups=0,7ms facts=1,8ms sort=0,0ms log=0,1ms

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

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=592; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#9 confirmedRev=22 reach=112 rideNeeds=4 tiers=3 options=2368 ranking=0

[AI VERMELHO][T4][Transporte] 9 Pickup[Tactical] recusa 2368 opcoes: carona=Requested!=Emergency=117 · rotaPax=ReachableLater=95 · rotaPax=ReachableStrategic=40 · tier=Operational!=Tactical=416 · tier=Strategic!=Tactical=1700

[AI Reach][Transport:9:Evac] Tactical:miss budget=6

[AI Reach][Transport:9:Evac] Operational:disabled

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Tactical] miss

[AI Reach][Transport:9:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 9 Pickup[Operational] recusa 2368 opcoes: tier=Tactical!=Operational=252 · carona=Requested!=Emergency=258 · rotaPax=ReachableStrategic=158 · tier=Strategic!=Operational=1700

[AI Reach][Transport:9:Evac] Operational:miss budget=12

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Operational] miss

[AI Reach][Transport:9:Pickup] Tactical:hit budget=6 action=(48, -2, 0) target=(49, -2, 0) score=101799 reason=passageiro=#8 encontro=(48, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=0+1=1 dist=0

[TransportOps][Unit#9][Pickup][Tactical] hit passageiro=#8 encontro=(48, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=0+1=1 dist=0

[AI VERMELHO][T4][Transporte] 9 pickup Tactical: aguarda na LZ (48, -2, 0) passageiro=#8 carona=Requested rotaPax=ReachableNow.

[FrameSpike] frame=7910 duration=844,85ms state=Neutral substep=AwaitingAction selected=(none) boardRev=390 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=743,0MB managedDelta=+18,9MB gcDelta=[0,0,0] unityAlloc=979,2MB

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

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,018ms memory=0,022ms geoOnly=0,008ms unitLoop=0,347ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=151

[FoW][Perf][Incremental] unit=Chinook_T1_U9 total=3,585ms updateCache=0,004ms collect=0,000ms collected=False cells=0 render=0,805ms visibility=1,177ms intel=0,002ms detectionSfx=0,022ms persistence=0,001ms callbacks=1,455ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Perf][Unit][T4][Red] Chinook#9 action=wait decision=831ms execution=89ms snapshot=0ms delay=503ms total=1423ms stages=transportPlanning:830,7ms/1,melhorEmbarque:791,4ms/1,melhorEmbarque.lzLoop:721,3ms/1,melhorEmbarque.lzGates:515,1ms/1,melhorEmbarque.resolveMeeting:175,6ms/1,melhorEmbarque.passengerReach:62,6ms/1,queroCarona:47,7ms/4,validPaths:44,2ms/6,movementCostMap:27,5ms/12,melhorEmbarque.longRangeMap:14,4ms/4,turnChainedCostMap:9,4ms/5,melhorCaptura:7,5ms/6,ownMovementComponent:0,3ms/1,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:1,CellsVisited:4394,MelhorCapturaCalls:6,MelhorCapturaCandidates:72,MelhorCapturaOutOfBandSkips:48,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:5,MelhorCapturaTargets:22,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:1496,MelhorEmbarqueEmbarkProbes:872,MelhorEmbarqueLongRangeMapBuilds:4,MelhorEmbarqueLzGateProbes:1050,MelhorEmbarqueLzGateRejects:458,MelhorEmbarquePairs:2368,MelhorEmbarquePairsNoRoute:1496,MelhorEmbarquePairsReachableLater:308,MelhorEmbarquePairsReachableNow:228,MelhorEmbarquePairsStrategic:336,MelhorEmbarquePassengers:4,MobilityComponentBuilds:1,MobilityComponentHits:7,MovementCacheHits:1,MovementCacheMisses:17,MovementCacheStores:17,MovementCostCellsExpanded:1748,MovementCostWaves:12,MovementQueryCachesBuilt:278,MovementQueryConfirmedOccupancyUses:278,MovementWavesBuilt:17,OwnMovementComponentBuilds:1,PathStatesExpanded:969,QueroCaronaCacheMisses:4,QueroCaronaCacheStores:4,QueroCaronaCalls:4,QueroCaronaCaptureReachBuilds:4,QueroCaronaMobilityComponentHits:4,ReachableCellsProduced:2724,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:627,ValidPathCacheHits:1,ValidPathWaves:5

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#10 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#10 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#3 (carregado) Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#3 slot=0 carona=Requested ajuste=1600 fila=1t motivo=Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=17; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#5 confirmedRev=22 reach=48 rideNeeds=3 tiers=3 options=51 ranking=0

[AI VERMELHO][T4][Transporte] 5 Pickup[Tactical] recusa 51 opcoes: rotaPax=ReachableLater=6 · rotaPax=ReachableStrategic=9 · tier=Operational!=Tactical=9 · tier=Strategic!=Tactical=27

[AI Reach][Transport:5:Evac] Tactical:miss budget=5

[AI Reach][Transport:5:Evac] Operational:disabled

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Tactical] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Operational] recusa 51 opcoes: tier=Tactical!=Operational=15 · rotaPax=ReachableStrategic=9 · tier=Strategic!=Operational=27

[AI Reach][Transport:5:Evac] Operational:miss budget=10

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Operational] miss

[AI VERMELHO][T4][Transporte] 5 Pickup[Tactical] recusa 51 opcoes: rotaPax=ReachableLater=6 · rotaPax=ReachableStrategic=9 · tier=Operational!=Tactical=9 · tier=Strategic!=Tactical=27

[AI Reach][Transport:5:Pickup] Tactical:miss budget=5

[AI Reach][Transport:5:Pickup] Operational:disabled

[AI Reach][Transport:5:Pickup] Strategic:disabled

[TransportOps][Unit#5][Pickup][Tactical] miss

[AI Reach][Transport:5:Pickup] Tactical:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Operational] recusa 51 opcoes: tier=Tactical!=Operational=15 · rotaPax=ReachableStrategic=9 · tier=Strategic!=Operational=27

[AI Reach][Transport:5:Pickup] Operational:miss budget=10

[AI Reach][Transport:5:Pickup] Strategic:disabled

[TransportOps][Unit#5][Pickup][Operational] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI Reach][Transport:5:Evac] Operational:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Strategic] recusa 51 opcoes: tier=Tactical!=Strategic=15 · tier=Operational!=Strategic=9 · carona=Requested!=Emergency=6 · rotaPax=NoCurrentRoute=21

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

[FrameSpike] frame=7992 duration=285,14ms state=Neutral substep=AwaitingAction selected=(none) boardRev=414 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=664,7MB managedDelta=+2,7MB gcDelta=[0,0,0] unityAlloc=979,0MB

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

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,018ms memory=0,022ms geoOnly=0,008ms unitLoop=0,171ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=150

[FoW][Perf][Incremental] unit=Navio Transporte_T1_U5 total=11,561ms updateCache=9,165ms collect=8,973ms collected=True cells=24 render=0,850ms visibility=0,572ms intel=0,002ms detectionSfx=0,022ms persistence=0,001ms callbacks=0,855ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Navio Transporte#5 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Navio Transporte#5 action=move decision=268ms execution=539ms snapshot=0ms delay=502ms total=1309ms stages=transportPlanning:202,5ms/1,melhorEmbarque:197,1ms/1,melhorEmbarque.lzLoop:192,2ms/1,melhorEmbarque.lzGates:182,9ms/1,toolProgression.TransportRendezvous:65,4ms/1,validPaths:43,9ms/13,melhorEmbarque.resolveMeeting:7,6ms/1,movementCostMap:5,6ms/10,melhorEmbarque.passengerReach:3,7ms/1,melhorEmbarque.longRangeMap:2,0ms/3,queroCarona:0,3ms/3,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CellsVisited:3466,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:21,MelhorEmbarqueEmbarkProbes:30,MelhorEmbarqueLongRangeMapBuilds:3,MelhorEmbarqueLzGateProbes:1028,MelhorEmbarqueLzGateRejects:1011,MelhorEmbarquePairs:51,MelhorEmbarquePairsNoRoute:21,MelhorEmbarquePairsReachableLater:6,MelhorEmbarquePairsStrategic:24,MelhorEmbarquePassengers:3,MobilityComponentHits:12,MobilityComponentTouchTests:2,MovementCacheBypasses:12,MovementCacheHits:9,MovementCacheMisses:14,MovementCacheStores:2,MovementCostCacheHits:9,MovementCostCellsExpanded:48,MovementCostWaves:1,MovementQueryCachesBuilt:61,MovementQueryConfirmedOccupancyUses:27,MovementQueryLiveOccupancyFallbacks:34,MovementWavesBuilt:14,PathStatesExpanded:2368,QueroCaronaCacheHits:3,QueroCaronaCalls:3,ReachableCellsProduced:1018,ToolProgressionCubicDirectionUses:1,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:5,ValidPathWaves:13

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

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,019ms memory=0,023ms geoOnly=0,008ms unitLoop=0,185ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=152

[FoW][Perf][Incremental] unit=Soldado_T1_U11 total=9,354ms updateCache=6,894ms collect=6,824ms collected=True cells=28 render=0,887ms visibility=0,609ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,843ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#11 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#11 action=move decision=77ms execution=335ms snapshot=0ms delay=501ms total=913ms stages=melhorCaptura:13,6ms/4,validPaths:12,1ms/10,opportunistic:4,9ms/2,turnChainedCostMap:4,0ms/3,queroCarona:3,3ms/2 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:5,CellsVisited:320,MelhorCapturaCalls:4,MelhorCapturaCandidates:48,MelhorCapturaOutOfBandSkips:36,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MobilityComponentHits:11,MovementCacheHits:8,MovementCacheMisses:2,MovementCacheStores:2,MovementQueryCachesBuilt:1328,MovementQueryConfirmedOccupancyUses:1328,MovementWavesBuilt:2,PathStatesExpanded:90,QueroCaronaCacheHits:1,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:2,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:280,TurnChainedCellsExpanded:230,ValidPathCacheHits:8,ValidPathWaves:2

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

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,017ms memory=0,020ms geoOnly=0,008ms unitLoop=0,164ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=147

[FoW][Perf][Incremental] unit=Soldado_T1_U10 total=3,813ms updateCache=1,563ms collect=1,483ms collected=True cells=22 render=0,821ms visibility=0,528ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,783ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#10 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#10 action=move decision=59ms execution=331ms snapshot=0ms delay=502ms total=891ms stages=melhorCaptura:9,3ms/4,validPaths:8,6ms/10,opportunistic:3,7ms/2,turnChainedCostMap:3,5ms/3,queroCarona:2,7ms/2 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:5,CellsVisited:289,MelhorCapturaCalls:4,MelhorCapturaCandidates:48,MelhorCapturaOutOfBandSkips:36,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MobilityComponentHits:11,MovementCacheHits:8,MovementCacheMisses:2,MovementCacheStores:2,MovementQueryCachesBuilt:1034,MovementQueryConfirmedOccupancyUses:1034,MovementWavesBuilt:2,PathStatesExpanded:90,QueroCaronaCacheHits:1,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:2,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:249,TurnChainedCellsExpanded:199,ValidPathCacheHits:8,ValidPathWaves:2

[AI Reach][FieldSupply:13] Tactical:disabled

[AI Reach][FieldSupply:13] Operational:miss budget=14

[AI Reach][FieldSupply:13] Strategic:disabled

[AI Reach][Transport:13:Supply] Tactical:miss budget=7

[AI Reach][Transport:13:Supply] Operational:disabled

[AI Reach][Transport:13:Supply] Strategic:disabled

[TransportOps][Unit#13][Supply][Tactical] miss

[AI Reach][Transport:13:Supply] Tactical:disabled

[AI Reach][Transport:13:Supply] Operational:miss budget=14

[AI Reach][Transport:13:Supply] Strategic:disabled

[TransportOps][Unit#13][Supply][Operational] miss

[AI VERMELHO][T4][Logistics] 13 restockCheck nao restockCheck ok Galões=150/150

[AI Reach][Stock:13] Tactical:miss budget=7

[AI Reach][Stock:13] Operational:miss budget=14

[AI Reach][Stock:13] Strategic:disabled

[AI Reach][FieldSupply:13] Tactical:disabled

[AI Reach][FieldSupply:13] Operational:miss budget=14

[AI Reach][FieldSupply:13] Strategic:hit budget=2147483647 action=(48, -2, 0) target=(48, -2, 0) score=-512 reason=critical_need_cubic

[AI VERMELHO][T4][Logistics] 13 move retaguarda via (32, 4, 0) anchor=home serviceTarget=Chinook#9@(48, -2, 0) toolProgress hold=-2628 toolIntent=LogisticsService tool=182 next=12,0 moveCost=7 roadBonus=False prog=7,0/14,0 route=? progR=? line=0,2 dpq=1,0 threat=0,0 tactical=2802 final=184832

[RangeCache] MISS - reason: empty key | unit=Avião Tanque_T1_U13 unitId=-58218 mp=7 fuel=120 rev=449

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Avião Tanque_T1_U13 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Avião Tanque_T1_U13

moveu para 32,4

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Avião Tanque_T1_U13

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Avião Tanque_T1_U13 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,016ms knownCells=0,027ms memory=0,021ms geoOnly=0,009ms unitLoop=0,174ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=158

[FoW][Perf][Incremental] unit=Avião Tanque_T1_U13 total=14,553ms updateCache=9,128ms collect=8,896ms collected=True cells=37 render=3,735ms visibility=0,605ms intel=0,003ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,962ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Avião Tanque#13 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Avião Tanque#13 action=move decision=155ms execution=705ms snapshot=0ms delay=505ms total=1365ms stages=toolProgression.LogisticsService:125,3ms/1,validPaths:57,6ms/15,transportPlanning:12,9ms/1,melhorEstoque:1,8ms/1,movementCostMap:1,1ms/1 metrics=CellsVisited:6290,MelhorEstoqueCalls:1,MelhorEstoqueConfirmedSupplierQueries:1,MelhorEstoqueIndexedConstructionQueries:1,MelhorEstoqueTacticalReachReuses:1,MovementCacheBypasses:12,MovementCacheHits:2,MovementCacheMisses:14,MovementCacheStores:2,MovementCostCellsExpanded:119,MovementCostWaves:1,MovementQueryCachesBuilt:132,MovementQueryConfirmedOccupancyUses:85,MovementQueryLiveOccupancyFallbacks:47,MovementWavesBuilt:14,PathStatesExpanded:6171,ReachableCellsProduced:2068,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:1,ValidPathCacheHits:2,ValidPathWaves:13

[AI VERMELHO][T4][Transporte] 8 ANINHA — embarca em #9 slot 1 levando a carga junto.

[RangeCache] MISS - reason: empty key | unit=APC_T1_U8 unitId=-39636 mp=6 fuel=53 rev=456

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U8 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U8

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U8

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U8

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U8 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Embarcando

[TurnState] transition=MoveuParado -> Embarcando | reason=HandleEmbarkActionRequested | selected=APC_T1_U8 | stack=Neutral > UnitSelected > MoveuParado > Embarcando

[FSM][Enter] MoveuParado -> Embarcando | reason=HandleEmbarkActionRequested

Confirma embarque 1? Chinook_T1_U9 | APC (0/1) | custo=1 | movRest=6 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[TurnState] substep=AwaitingAction -> EmbarkConfirmTarget | state=Embarcando

[Embarque] Opcao 1/1 [VALIDA]
Chinook_T1_U9 | APC (0/1) | custo=1 | movRest=6 | OBS: se escolhido, o transportador pousa antes do embarque
Linha: VERDE
Custo de autonomia: 1
Botao Embarcar: habilitado
Enter confirma. ESC volta.

Confirma embarque 1? Chinook_T1_U9 | APC (0/1) | custo=1 | movRest=6 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[FSM] Estado: Embarcando -> EmbarcandoExecuting

[TurnState] transition=Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin | selected=APC_T1_U8 | stack=Neutral > UnitSelected > MoveuParado > Embarcando > EmbarcandoExecuting

[FSM][Enter] Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin

[Embarque] Transportador pousou antes do embarque.

[FSM] Estado: EmbarcandoExecuting -> Neutral

[TurnState] transition=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=1 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,015ms knownCells=0,023ms memory=0,019ms geoOnly=0,008ms unitLoop=0,160ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=151

[FoW][Perf][Incremental] unit=APC_T1_U8 total=2,880ms updateCache=0,071ms collect=0,000ms collected=False cells=0 render=1,444ms visibility=0,559ms intel=0,002ms detectionSfx=0,002ms persistence=0,000ms callbacks=0,685ms splitPresentation=False

Embarque concluido em: Chinook_T1_U9 | APC (0/1) | custo=1 | movRest=6 | OBS: se escolhido, o transportador pousa antes do embarque | custo=1 | autonomia 53->52

[Embarque] Transportador decolou apos concluir o embarque.

[TurnState] [roll back] substep=EmbarkConfirmTarget -> AwaitingAction | state=Neutral

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:APC#8 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] APC#8 action=attack decision=21ms execution=1915ms snapshot=0ms delay=501ms total=2437ms stages=validPaths:18,9ms/1 metrics=CellsVisited:238,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:1,MovementQueryConfirmedOccupancyUses:1,MovementWavesBuilt:1,PathStatesExpanded:238,ReachableCellsProduced:78,ValidPathWaves:1

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

[AI VERMELHO][T4][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=False pax=#4

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#4

[AI VERMELHO][T4][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=True pax=#4

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

[FrameSpike] frame=8975 duration=3245,98ms state=Neutral substep=AwaitingAction selected=(none) boardRev=509 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=723,1MB managedDelta=+27,9MB gcDelta=[0,0,0] unityAlloc=979,7MB

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=47144 mp=6 fuel=60 rev=509

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

[FoW][Perf][Publish] slot=1 contributors=0,013ms knownCells=0,017ms memory=0,017ms geoOnly=0,008ms unitLoop=0,141ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][Perf][Incremental] unit=APC_T1_U3 total=9,499ms updateCache=6,970ms collect=6,741ms collected=True cells=36 render=1,264ms visibility=0,486ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,662ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T4][Stock] 3 confirma missao Transport destino=(20, 6, 0) unit=#4 construction=#-1 tier=None.

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:APC#3 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] APC#3 action=move decision=3237ms execution=638ms snapshot=0ms delay=502ms total=4378ms stages=melhorDesembarque:657,2ms/2,toolProgression.TransportDelivery:173,2ms/1,validPaths:160,6ms/14,movementCostMap:12,3ms/5,transportPlanning:0,5ms/1 metrics=CellsVisited:6660,DisembarkLzCellsVisited:328,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MovementCacheBypasses:12,MovementCacheMisses:19,MovementCacheStores:7,MovementCostCellsExpanded:867,MovementCostWaves:5,MovementQueryCachesBuilt:129,MovementQueryConfirmedOccupancyUses:76,MovementQueryLiveOccupancyFallbacks:53,MovementWavesBuilt:19,PathStatesExpanded:5465,ReachableCellsProduced:2486,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathWaves:14

[AI Perf][Phase2 Breakdown][T4][Red] decisions=7 completed=7
  decision=4647ms execution=4553ms snapshot=0ms delay=3516ms measuredTotal=12716ms
  boardQueries stages=transportPlanning:1046,6ms/4,melhorEmbarque:988,5ms/2,melhorEmbarque.lzLoop:913,5ms/2,melhorEmbarque.lzGates:698,0ms/2,melhorDesembarque:657,2ms/2,validPaths:345,8ms/69,melhorEmbarque.resolveMeeting:183,2ms/2,toolProgression.TransportDelivery:173,2ms/1,toolProgression.LogisticsService:125,3ms/1,melhorEmbarque.passengerReach:66,3ms/2,toolProgression.TransportRendezvous:65,4ms/1,queroCarona:54,1ms/11,movementCostMap:46,5ms/28,melhorCaptura:30,4ms/14,turnChainedCostMap:16,9ms/11,melhorEmbarque.longRangeMap:16,4ms/7,opportunistic:8,6ms/4,melhorEstoque:1,8ms/1,ownMovementComponent:0,3ms/1,melhorEmbarque.candidateCells:0,0ms/2,melhorEmbarque.transporterPaths:0,0ms/2 metrics=CaptureClaimSnapshotBuilds:3,CaptureClaimSnapshotHits:11,CellsVisited:21657,DisembarkLzCellsVisited:328,MelhorCapturaCalls:14,MelhorCapturaCandidates:168,MelhorCapturaOutOfBandSkips:120,MelhorCapturaReachBuilds:5,MelhorCapturaReachReuses:9,MelhorCapturaTargets:44,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MelhorEmbarqueCalls:2,MelhorEmbarqueCandidateCells:2100,MelhorEmbarqueEmbarkProbeSkips:1517,MelhorEmbarqueEmbarkProbes:902,MelhorEmbarqueLongRangeMapBuilds:7,MelhorEmbarqueLzGateProbes:2078,MelhorEmbarqueLzGateRejects:1469,MelhorEmbarquePairs:2419,MelhorEmbarquePairsNoRoute:1517,MelhorEmbarquePairsReachableLater:314,MelhorEmbarquePairsReachableNow:228,MelhorEmbarquePairsStrategic:360,MelhorEmbarquePassengers:7,MelhorEstoqueCalls:1,MelhorEstoqueConfirmedSupplierQueries:1,MelhorEstoqueIndexedConstructionQueries:1,MelhorEstoqueTacticalReachReuses:1,MobilityComponentBuilds:1,MobilityComponentHits:41,MobilityComponentTouchTests:2,MovementCacheBypasses:36,MovementCacheHits:28,MovementCacheMisses:69,MovementCacheStores:33,MovementCostCacheHits:9,MovementCostCellsExpanded:2782,MovementCostWaves:19,MovementQueryCachesBuilt:2963,MovementQueryConfirmedOccupancyUses:2829,MovementQueryLiveOccupancyFallbacks:134,MovementWavesBuilt:69,OwnMovementComponentBuilds:1,PathStatesExpanded:15391,QueroCaronaCacheHits:5,QueroCaronaCacheMisses:6,QueroCaronaCacheStores:6,QueroCaronaCalls:11,QueroCaronaCaptureReachBuilds:6,QueroCaronaMobilityComponentHits:6,ReachableCellsProduced:8903,ToolProgressionCubicDirectionUses:3,TopologyCellsVisited:2100,TopologyIndexCandidateCells:2100,TopologyIndexHits:5,TopologyIndexQueries:5,TransportPlanningCalls:4,TransportPlanningReachReuses:4,TransportPlanningSnapshotBuilds:3,TransportPlanningSnapshotHits:8,TurnChainedCellsExpanded:1056,ValidPathCacheHits:19,ValidPathWaves:50
  #1 APC#3 action=move total=4378ms decision=3237 execution=638 snapshot=0 delay=502
  #2 APC#8 action=attack total=2437ms decision=21 execution=1915 snapshot=0 delay=501
  #3 Chinook#9 action=wait total=1423ms decision=831 execution=89 snapshot=0 delay=503
  #4 Avião Tanque#13 action=move total=1365ms decision=155 execution=705 snapshot=0 delay=505
  #5 Navio Transporte#5 action=move total=1309ms decision=268 execution=539 snapshot=0 delay=502


[AI VERMELHO][T4] Fase2 concluída — todas as 7 unidades agiram.

[AI Perf] Stage2 (actions): 12846ms

[AI VERMELHO][T4] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,016ms memory=0,021ms geoOnly=0,008ms unitLoop=0,192ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=1 cells.collected=0 constructions=2 constructions.removed=2 total=0,535ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,797ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=5 bases=1 constructions=12 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=521 sectors=5 bases=1 total=1,0ms

[SectorManager][Perf][Steps] contexts=0,5ms (search=0,0ms calls=0) sectorLoop=0,3ms neighborPass=0,1ms | search.calls=42 search.ms=0,1 search.hits=42 search.failures=22 search.exhausted=0 search.expanded=0 cache.size=36 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=165/0 tile=425/0 | constructions=12 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 7ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI VERMELHO][T4][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T4][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T4][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T4][slot=1][vermelho] reason=phase3:pre-shopping units=6 enemies=0 total=9ms

[AI Shopping Roles][T4][Red] fila de carona: 2 esperando e 3 transportador(es) em campo — sem demanda de transporte.

[AI Shopping Roles][T4][Red] doutrina rebelde: Capturador + transporte derivado da fila de carona — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T4][Red] fila unica budget=8000 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T4][Red] expansão econômica: prioriza até 2 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T4][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=8000 — caixa preservado

[AI Shopping Roles][T4][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T4] Fase3 concluída.

[AI Perf] Stage3 (shopping): 11ms

[AI VERMELHO][T4] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 20ms

[AI Perf] TURNO TOTAL (Red): 14017ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=10 ms=2,246

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=12 ms=0,707

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=4,715

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,482

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,006

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,048

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,263

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,500

[FoW][Perf][Publish] slot=0 contributors=0,003ms knownCells=0,028ms memory=0,010ms geoOnly=0,002ms unitLoop=0,166ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=6 knownCells.count=35

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=3 constructions.removed=3 total=0,486ms

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,016ms memory=0,018ms geoOnly=0,008ms unitLoop=0,198ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=2,304ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=4,258

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,018

[TurnPerf] etapa=ApplyActiveTeam.Total ms=10,325

[TurnPerf] etapa=AdvanceTurn ms=10,591

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=110,704

[FoW][Warmup] host=0 slots=0 sources=0 total=13,666ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=13,7ms

[AI Shortcuts] F12 ignorado: o time ativo nao esta configurado como AI.

[FSM] Estado: Neutral -> EndingTurn

[TurnState] transition=Neutral -> EndingTurn | reason=TryOpenEndingTurnConfirmation | selected=(none) | stack=Neutral > EndingTurn

[FSM][Enter] Neutral -> EndingTurn | reason=TryOpenEndingTurnConfirmation

[FSM] Estado: EndingTurn -> EndingTurnExecuting

[TurnState] transition=EndingTurn -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromConfirmation | selected=(none) | stack=Neutral > EndingTurn > EndingTurnExecuting

[FSM][Enter] EndingTurn -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromConfirmation

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromConfirmation: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromConfirmation: dispatched

[AI] HandleTeamChanged — teamIndex=1 newTeam=Red matchController=True isAI=True

[AI] RunAITurn iniciado para Red.

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=10 ms=2,199

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=12 ms=0,684

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=4,461

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,559

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,043

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=4,950

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,001

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=5,237

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,020ms memory=0,020ms geoOnly=0,008ms unitLoop=0,150ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=2,101ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=3,296

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,003

[TurnPerf] etapa=ApplyActiveTeam.Total ms=13,900

[TurnPerf] etapa=AdvanceTurn ms=14,143

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=118,007

[AI VERMELHO][T4] Fase0 concluída.

[AI Perf] Stage0 (wait): 530ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,018ms memory=0,019ms geoOnly=0,007ms unitLoop=0,147ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,510ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,778ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI Commit Heavy][T5][slot=1][vermelho] reason=turn-start units=6 enemies=0 total=2ms

[AI Perf] CommitAIWorldHeavy: 8ms

[AI VERMELHO][T5] Turno 5 | Stance: Offensive | 6 unidades | 0 inimigos visíveis | R$ 9000

[AI VERMELHO][T5][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T5][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T5][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T5] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T5] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T5] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T5] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 521ms

[AI Perf] PRE-Stage2 acumulado: 1112ms

[AI VERMELHO][T5] Fase2 — iniciando ações.

[AI VERMELHO][T5][FilaCarona] #8 embarcado — sai da fila apos 4 turno(s).

[AI VERMELHO][T5][Promessa] #3 baixa a promessa a pax=#4: passageiro embarcou.

[AI VERMELHO][T5] Fase2 iniciativa (6 unidades):
  [grp=1] Chinook#9 @ (48, -2, 0) target=null
  [grp=4] Soldado#11 @ (49, -5, 0) target=null
  [grp=4] Soldado#10 @ (49, -6, 0) target=null
  [grp=4] Avião Tanque#13 @ (32, 4, 0) target=null
  [grp=4] Navio Transporte#5 @ (44, 1, 0) target=null
  [grp=4] APC#3 @ (50, -1, 0) target=null


[AI Perf][InitiativeSetup] total=12,5ms available=0,2ms snapshot=0,2ms repair=9,2ms groups=1,0ms facts=1,7ms sort=0,0ms log=0,1ms

[AI Reach][Transport:9:Courier] Tactical:miss budget=6

[AI Reach][Transport:9:Courier] Operational:disabled

[AI Reach][Transport:9:Courier] Strategic:disabled

[TransportOps][Unit#9][Courier][Tactical] miss

[AI Reach][Transport:9:Delivery] Tactical:disabled

[AI Reach][Transport:9:Delivery] Operational:hit budget=12 action=(0, 0, 0) target=(0, 0, 0) score=90000 reason=carga embarcada count=1 dist=49

[TransportOps][Unit#9][Delivery][Operational] hit carga embarcada count=1 dist=49

[AI VERMELHO][T5][Transporte] heli 9 courier — passageiro #8 alvo=(48, -2, 0) dist=0h

[AI VERMELHO][T5][Transporte] heli 9 courier — MP=6 grounded=False domain=Air paths=112 occupied=1

[AI VERMELHO][T5][Transporte] courier local-op rejeita passageiro #8: inPlan=False assignedOk=False repair=False

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] rebelde #8 permanece a bordo: nenhum segundo capturavel distinto em range 4 e objetivo primario sem pressao.

[AI VERMELHO][T5][Transporte] heli 9 courier sem LZ seguro para #8 em (48, -2, 0) - aguarda fora da producao perto de (48, -2, 0) via (48, -2, 0)

[FrameSpike] frame=10162 duration=368,19ms state=Neutral substep=AwaitingAction selected=(none) boardRev=521 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=671,3MB managedDelta=+2,7MB gcDelta=[0,0,0] unityAlloc=980,0MB

[RangeCache] MISS - reason: empty key | unit=Chinook_T1_U9 unitId=-42530 mp=6 fuel=32 rev=521

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

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,017ms memory=0,018ms geoOnly=0,008ms unitLoop=0,148ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=135

[FoW][Perf][Incremental] unit=Chinook_T1_U9 total=2,522ms updateCache=0,003ms collect=0,000ms collected=False cells=0 render=1,231ms visibility=0,485ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,673ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Perf][Unit][T5][Red] Chinook#9 action=wait decision=360ms execution=71ms snapshot=0ms delay=503ms total=934ms stages=validPaths:29,0ms/3,transportPlanning:13,3ms/1,melhorCaptura:12,8ms/2,routeDistance:2,4ms/224,turnChainedCostMap:2,2ms/2 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:525,MelhorCapturaCalls:2,MelhorCapturaCandidates:24,MelhorCapturaOutOfBandSkips:24,MelhorCapturaReachBuilds:2,MovementCacheMisses:3,MovementCacheStores:3,MovementQueryCachesBuilt:52,MovementQueryConfirmedOccupancyUses:52,MovementWavesBuilt:3,PathStatesExpanded:410,ReachableCellsProduced:276,TransportPlanningCalls:1,TurnChainedCellsExpanded:115,ValidPathWaves:3

[AI VERMELHO][T5][Capturador] 11 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Capturador] 11 embarca (ext 3h) ? 9 slot 0 via (48, -3, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U11 unitId=-43002 mp=3 fuel=58 rev=521

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U11

moveu para 48,-3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

Pode Embarcar ("E"): nao ha transportador valido adjacente.

[TurnState] state=MoveuAndando | step=HandleConfirm | selected=Soldado_T1_U11

[TurnState] state=MoveuAndando | step=HandleConfirmWhileMoveuAndando | selected=Soldado_T1_U11

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,013ms knownCells=0,018ms memory=0,018ms geoOnly=0,008ms unitLoop=0,146ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=134

[FoW][Perf][Incremental] unit=Soldado_T1_U11 total=11,689ms updateCache=9,097ms collect=9,020ms collected=True cells=34 render=1,290ms visibility=0,519ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,669ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

debug: inspecionando aliado que ja agiu (unit=Chinook_T1_U9, unitTeam=1, activeTeam=1, hasActed=True)

[HotzoneCache] MISS | unit=Chinook (9) | unit[h=0,m=1,rate=0,0%] | session[h=0,m=1,rate=0,0%]

[FSM] Estado: Neutral -> InspectingUnit

[TurnState] transition=Neutral -> InspectingUnit | reason=HandleConfirmFromNeutralLikeState: acted ally inspect | selected=(none) | stack=Neutral > InspectingUnit

[FSM][Enter] Neutral -> InspectingUnit | reason=HandleConfirmFromNeutralLikeState: acted ally inspect

[FrameSpike] frame=10280 duration=273,43ms state=InspectingUnit substep=AwaitingAction selected=(none) boardRev=523 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=675,6MB managedDelta=+0,1MB gcDelta=[0,0,0] unityAlloc=981,2MB

[FSM] Estado: InspectingUnit -> Neutral

[TurnState] [roll back] transition=InspectingUnit -> Neutral | reason=ExitInspectStateToNeutral | selected=(none) | stack=Neutral

[FSM][Reveal] exited=InspectingUnit revealed=Neutral | reason=ExitInspectStateToNeutral

[AI VERMELHO][T5][Missao] 11 Capture -> (25, -6, 0) predio=#7 (adquirida).

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:Soldado#11 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] Soldado#11 action=attack decision=204ms execution=6271ms snapshot=0ms delay=504ms total=6978ms stages=queroCarona:2,8ms/1,opportunistic:2,6ms/1,validPaths:1,8ms/4,turnChainedCostMap:1,2ms/1,melhorCaptura:0,5ms/2 metrics=CaptureClaimSnapshotHits:3,CellsVisited:63,MelhorCapturaCalls:2,MelhorCapturaCandidates:24,MelhorCapturaOutOfBandSkips:12,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MovementCacheHits:4,MovementQueryCachesBuilt:335,MovementQueryConfirmedOccupancyUses:335,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:63,TurnChainedCellsExpanded:63,ValidPathCacheHits:4

[AI VERMELHO][T5][Capturador] 10 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Progressao2] transporte 10 alvo=(48, -2, 0) via (47, -3, 0) (heuristica score=55200)

[AI VERMELHO][T5][Capturador] 10 rogue — avança para transporte rogue 9@(48, -2, 0) via (47, -3, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U10 unitId=-42766 mp=3 fuel=58 rev=523

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U10

moveu para 47,-3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,013ms knownCells=0,017ms memory=0,017ms geoOnly=0,008ms unitLoop=0,144ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=134

[FoW][Perf][Incremental] unit=Soldado_T1_U10 total=6,113ms updateCache=3,576ms collect=3,480ms collected=True cells=33 render=1,252ms visibility=0,505ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,662ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T5][Missao] 10 Capture -> (24, -4, 0) predio=#6 (adquirida).

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:Soldado#10 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] Soldado#10 action=move decision=87ms execution=331ms snapshot=0ms delay=501ms total=920ms stages=transportMove:58,8ms/1,melhorCaptura:7,4ms/4,validPaths:5,0ms/6,movementCostMap:4,5ms/2,transportReverseCostMap:3,7ms/1,turnChainedCostMap:3,2ms/3,queroCarona:2,6ms/1,opportunistic:1,8ms/1,transportOriginCostMap:0,9ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:452,MelhorCapturaCalls:4,MelhorCapturaCandidates:48,MelhorCapturaOutOfBandSkips:36,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MovementCacheHits:4,MovementCacheMisses:4,MovementCacheStores:4,MovementCostCellsExpanded:260,MovementCostWaves:2,MovementQueryCachesBuilt:528,MovementQueryConfirmedOccupancyUses:528,MovementWavesBuilt:4,PathStatesExpanded:45,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:432,TurnChainedCellsExpanded:147,ValidPathCacheHits:4,ValidPathWaves:2

[AI Reach][FieldSupply:13] Tactical:disabled

[AI Reach][FieldSupply:13] Operational:miss budget=14

[AI Reach][FieldSupply:13] Strategic:disabled

[AI Reach][Transport:13:Supply] Tactical:miss budget=7

[AI Reach][Transport:13:Supply] Operational:disabled

[AI Reach][Transport:13:Supply] Strategic:disabled

[TransportOps][Unit#13][Supply][Tactical] miss

[AI Reach][Transport:13:Supply] Tactical:disabled

[AI Reach][Transport:13:Supply] Operational:miss budget=14

[AI Reach][Transport:13:Supply] Strategic:disabled

[TransportOps][Unit#13][Supply][Operational] miss

[AI VERMELHO][T5][Logistics] 13 restockCheck nao restockCheck ok Galões=150/150

[AI Reach][Stock:13] Tactical:miss budget=7

[AI Reach][Stock:13] Operational:miss budget=14

[AI Reach][Stock:13] Strategic:disabled

[AI Reach][FieldSupply:13] Tactical:disabled

[AI Reach][FieldSupply:13] Operational:miss budget=14

[AI Reach][FieldSupply:13] Strategic:hit budget=2147483647 action=(48, -2, 0) target=(48, -2, 0) score=-163 reason=critical_need_cubic

[AI VERMELHO][T5][Logistics] 13 move retaguarda via (38, 2, 0) anchor=home serviceTarget=Chinook#9@(48, -2, 0) toolProgress hold=-1372 toolIntent=LogisticsService tool=182 next=5,0 moveCost=7 roadBonus=False prog=7,0/14,0 route=? progR=? line=0,2 dpq=1,0 threat=0,0 tactical=4129 final=186159

[RangeCache] MISS - reason: empty key | unit=Avião Tanque_T1_U13 unitId=-58218 mp=7 fuel=110 rev=550

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Avião Tanque_T1_U13 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Avião Tanque_T1_U13

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Avião Tanque_T1_U13

moveu para 38,2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Avião Tanque_T1_U13

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Avião Tanque_T1_U13 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,016ms memory=0,017ms geoOnly=0,007ms unitLoop=0,147ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=134

[FoW][Perf][Incremental] unit=Avião Tanque_T1_U13 total=11,792ms updateCache=9,154ms collect=8,883ms collected=True cells=37 render=1,318ms visibility=0,526ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,679ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:Avião Tanque#13 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] Avião Tanque#13 action=move decision=155ms execution=710ms snapshot=0ms delay=505ms total=1369ms stages=toolProgression.LogisticsService:133,6ms/1,validPaths:61,0ms/15,transportPlanning:8,5ms/1,melhorEstoque:2,1ms/1,movementCostMap:1,1ms/1 metrics=CellsVisited:6417,MelhorEstoqueCalls:1,MelhorEstoqueConfirmedSupplierQueries:1,MelhorEstoqueIndexedConstructionQueries:1,MelhorEstoqueTacticalReachReuses:1,MovementCacheBypasses:12,MovementCacheHits:2,MovementCacheMisses:14,MovementCacheStores:2,MovementCostCellsExpanded:142,MovementCostWaves:1,MovementQueryCachesBuilt:155,MovementQueryConfirmedOccupancyUses:101,MovementQueryLiveOccupancyFallbacks:54,MovementWavesBuilt:14,PathStatesExpanded:6275,ReachableCellsProduced:2118,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:1,ValidPathCacheHits:2,ValidPathWaves:13

[AI VERMELHO][T5][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1900 fila=4t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte][QueroCarona] pax=#10 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte][MelhorEmbarque] ACCEPT pax=#10 slot=0 carona=Requested ajuste=1900 fila=4t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T5][Transporte][QueroCarona] pax=#3 (carregado) Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte][MelhorEmbarque] ACCEPT pax=#3 slot=0 carona=Requested ajuste=1700 fila=2t motivo=Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T5][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=16; preservadas apenas no ranking plano.

[AI VERMELHO][T5][Transporte][PlanningSnapshot] unit=#5 confirmedRev=32 reach=59 rideNeeds=3 tiers=3 options=48 ranking=0

[AI VERMELHO][T5][Transporte] 5 Pickup[Tactical] recusa 48 opcoes: carona=Requested!=Emergency=2 · rotaPax=ReachableLater=7 · rotaPax=ReachableStrategic=6 · tier=Operational!=Tactical=12 · tier=Strategic!=Tactical=21

[AI Reach][Transport:5:Evac] Tactical:miss budget=5

[AI Reach][Transport:5:Evac] Operational:disabled

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Tactical] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI VERMELHO][T5][Transporte] 5 Pickup[Operational] recusa 48 opcoes: tier=Tactical!=Operational=15 · carona=Requested!=Emergency=8 · rotaPax=ReachableStrategic=4 · tier=Strategic!=Operational=21

[AI Reach][Transport:5:Evac] Operational:miss budget=10

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Operational] miss

[AI Reach][Transport:5:Pickup] Tactical:hit budget=5 action=(46, -2, 0) target=(50, -1, 0) score=101395 reason=passageiro=#3 encontro=(46, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=4+1=5 dist=3

[TransportOps][Unit#5][Pickup][Tactical] hit passageiro=#3 encontro=(46, -2, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=4+1=5 dist=3

[AI VERMELHO][T5][Transporte] 5 pickup Tactical: segue MelhorEmbarque LZ=(46, -2, 0) passageiro=#3.

[FrameSpike] frame=11419 duration=256,02ms state=Neutral substep=AwaitingAction selected=(none) boardRev=557 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=731,7MB managedDelta=+7,8MB gcDelta=[0,0,0] unityAlloc=980,3MB

[RangeCache] MISS - reason: empty key | unit=Navio Transporte_T1_U5 unitId=-32740 mp=5 fuel=70 rev=557

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Navio Transporte_T1_U5

moveu para 46,-2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,016ms memory=0,016ms geoOnly=0,006ms unitLoop=0,156ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=115

[FoW][Perf][Incremental] unit=Navio Transporte_T1_U5 total=7,408ms updateCache=4,783ms collect=4,627ms collected=True cells=24 render=1,335ms visibility=0,500ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,674ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:Navio Transporte#5 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] Navio Transporte#5 action=move decision=248ms execution=534ms snapshot=0ms delay=502ms total=1285ms stages=transportPlanning:248,1ms/1,melhorEmbarque:244,4ms/1,melhorEmbarque.lzLoop:191,2ms/1,melhorEmbarque.lzGates:174,0ms/1,melhorEmbarque.passengerReach:48,4ms/1,queroCarona:38,5ms/3,validPaths:28,8ms/5,movementCostMap:19,6ms/9,melhorEmbarque.resolveMeeting:15,7ms/1,melhorEmbarque.longRangeMap:10,7ms/3,melhorCaptura:9,5ms/6,turnChainedCostMap:9,3ms/5,ownMovementComponent:3,7ms/2,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:1,CellsVisited:3342,MelhorCapturaCalls:6,MelhorCapturaCandidates:72,MelhorCapturaOutOfBandSkips:48,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:5,MelhorCapturaTargets:22,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:21,MelhorEmbarqueEmbarkProbes:27,MelhorEmbarqueLongRangeMapBuilds:3,MelhorEmbarqueLzGateProbes:1018,MelhorEmbarqueLzGateRejects:1002,MelhorEmbarquePairs:48,MelhorEmbarquePairsNoRoute:21,MelhorEmbarquePairsReachableLater:15,MelhorEmbarquePairsReachableNow:2,MelhorEmbarquePairsStrategic:10,MelhorEmbarquePassengers:3,MobilityComponentBuilds:2,MobilityComponentHits:10,MobilityComponentTouchTests:2,MovementCacheHits:1,MovementCacheMisses:13,MovementCacheStores:13,MovementCostCellsExpanded:1176,MovementCostWaves:9,MovementQueryCachesBuilt:170,MovementQueryConfirmedOccupancyUses:170,MovementWavesBuilt:13,OwnMovementComponentBuilds:2,PathStatesExpanded:484,QueroCaronaCacheMisses:3,QueroCaronaCacheStores:3,QueroCaronaCalls:3,QueroCaronaCaptureReachBuilds:3,QueroCaronaMobilityComponentHits:3,ReachableCellsProduced:1967,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:632,ValidPathCacheHits:1,ValidPathWaves:4

[AI VERMELHO][T5][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI Reach][Transport:3:Courier] Tactical:miss budget=6

[AI Reach][Transport:3:Courier] Operational:disabled

[AI Reach][Transport:3:Courier] Strategic:disabled

[TransportOps][Unit#3][Courier][Tactical] miss

[AI Reach][Transport:3:Delivery] Tactical:disabled

[AI VERMELHO][T5][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI Reach][Transport:3:Delivery] Operational:hit budget=12 action=(20, 6, 0) target=(20, 6, 0) score=90000 reason=carga embarcada count=1 dist=34

[TransportOps][Unit#3][Delivery][Operational] hit carga embarcada count=1 dist=34

[AI VERMELHO][T5][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T5][Transporte] 3 courier — passageiro #4 alvo=(20, 6, 0) range=6 (Operational; Tactical=3) distAtual=34h

[AI VERMELHO][T5][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=False pax=#4

[AI VERMELHO][T5][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#4

[AI VERMELHO][T5][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=True pax=#4

[AI VERMELHO][T5][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#4

[AI VERMELHO][T5][Transporte] alvo conjunto por missao: passageiro #4 intent=Pressure -> (20, 6, 0).

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(51, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(52, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(53, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Tactical:miss budget=6

[AI Reach][TransportDelivery:3] Operational:disabled

[AI Reach][TransportDelivery:3] Strategic:disabled

[AI Reach][TransportDelivery:3] Tactical:disabled

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(46, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(51, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(52, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(53, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(55, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(54, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(51, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(52, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(53, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(54, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(55, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(55, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(51, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(52, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(46, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(53, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(54, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(56, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(55, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(50, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(49, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(51, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(48, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(52, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(47, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(53, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(54, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(55, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(57, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(56, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(56, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(58, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(57, 7, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(57, 8, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Operational:miss budget=12

[AI Reach][TransportDelivery:3] Strategic:hit budget=120 action=(20, 6, 0) target=(20, 6, 0) score=-34 reason=cubic=34

[AI][Progressao2][Top] unit=3 intent=TransportDelivery from=(50, -1, 0) target=(20, 6, 0) best=(47, 4, 0) final=102000 candidatos=97 skips origin=1 occupied=3 stop=0 allow=0 score=0
  #1 (47, 4, 0) final=102000 tool=102 next=27,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=4,2 threat=0,0 dpq=1,0 tactical=0
  #2 (47, 5, 0) final=101000 tool=101 next=27,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=5,2 threat=0,0 dpq=1,0 tactical=0
  #3 (47, 3, 0) final=97000 tool=97 next=27,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=3,2 threat=0,0 dpq=1,0 tactical=0
  #4 (48, 4, 0) final=96000 tool=96 next=27,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=4,4 threat=0,0 dpq=1,0 tactical=0
  #5 (48, 5, 0) final=95000 tool=95 next=27,0 move=6 road=False prog=5,0/7,0 route=? progR=? line=5,4 threat=0,0 dpq=1,0 tactical=0
  #6 (48, 2, 0) final=92000 tool=92 next=27,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=2,5 threat=0,0 dpq=1,0 tactical=0
  #7 (48, 3, 0) final=91000 tool=91 next=27,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=3,4 threat=0,0 dpq=1,0 tactical=0
  #8 (49, 4, 0) final=89000 tool=89 next=27,0 move=5 road=False prog=4,0/7,0 route=? progR=? line=4,6 threat=0,0 dpq=1,0 tactical=0
  #9 (49, 5, 0) final=88000 tool=88 next=27,0 move=6 road=False prog=4,0/7,0 route=? progR=? line=5,6 threat=0,0 dpq=1,0 tactical=0
  #10 (48, 1, 0) final=87000 tool=87 next=27,0 move=3 road=False prog=3,0/7,0 route=? progR=? line=1,5 threat=0,0 dpq=1,0 tactical=0
  #11 (47, -2, 0) final=56000 tool=56 next=30,0 move=4 road=False prog=3,0/4,0 route=? progR=? line=1,7 threat=0,0 dpq=1,0 tactical=0
  #12 (49, 2, 0) final=45000 tool=45 next=31,0 move=3 road=False prog=3,0/3,0 route=? progR=? line=2,7 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T5][Transporte] Terrestre 3 reach estrategico: alvo cubico (20, 6, 0) -> ancora (20, 6, 0); progride (50, -1, 0)->(47, 4, 0) (toolIntent=TransportDelivery tool=102 next=27,0 moveCost=6 roadBonus=False prog=6,0/7,0 route=? progR=? line=4,2 dpq=1,0 threat=0,0 tactical=0 final=102000).

[AI VERMELHO][T5][Transporte] 3 larga no TACTICAL (rota restante <= 3).

[FrameSpike] frame=11574 duration=2927,53ms state=Neutral substep=AwaitingAction selected=(none) boardRev=610 replay=False aiTurn=True aiInputLock=True turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=670,6MB managedDelta=-65,9MB gcDelta=[1,1,1] unityAlloc=980,7MB

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=47144 mp=6 fuel=54 rev=610

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 47,4

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,016ms memory=0,018ms geoOnly=0,006ms unitLoop=0,149ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=125

[FoW][Perf][Incremental] unit=APC_T1_U3 total=11,496ms updateCache=8,729ms collect=8,515ms collected=True cells=30 render=1,446ms visibility=0,524ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,675ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T5][Stock] 3 confirma missao Transport destino=(20, 6, 0) unit=#4 construction=#-1 tier=None.

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:APC#3 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] APC#3 action=move decision=2920ms execution=625ms snapshot=0ms delay=503ms total=4047ms stages=melhorDesembarque:565,8ms/2,validPaths:176,7ms/14,toolProgression.TransportDelivery:148,2ms/1,movementCostMap:32,6ms/5,transportPlanning:0,5ms/1 metrics=CellsVisited:5114,DisembarkLzCellsVisited:316,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MovementCacheBypasses:12,MovementCacheMisses:19,MovementCacheStores:7,MovementCostCellsExpanded:862,MovementCostWaves:5,MovementQueryCachesBuilt:116,MovementQueryConfirmedOccupancyUses:24,MovementQueryLiveOccupancyFallbacks:92,MovementWavesBuilt:19,PathStatesExpanded:3936,ReachableCellsProduced:1941,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathWaves:14

[AI Perf][Phase2 Breakdown][T5][Red] decisions=6 completed=6
  decision=3974ms execution=8542ms snapshot=0ms delay=3017ms measuredTotal=15533ms
  boardQueries stages=melhorDesembarque:565,8ms/2,validPaths:302,4ms/47,transportPlanning:270,4ms/4,melhorEmbarque:244,4ms/1,melhorEmbarque.lzLoop:191,2ms/1,melhorEmbarque.lzGates:174,0ms/1,toolProgression.TransportDelivery:148,2ms/1,toolProgression.LogisticsService:133,6ms/1,transportMove:58,8ms/1,movementCostMap:57,8ms/17,melhorEmbarque.passengerReach:48,4ms/1,queroCarona:43,8ms/5,melhorCaptura:30,3ms/14,turnChainedCostMap:15,9ms/11,melhorEmbarque.resolveMeeting:15,7ms/1,melhorEmbarque.longRangeMap:10,7ms/3,opportunistic:4,4ms/2,ownMovementComponent:3,7ms/2,transportReverseCostMap:3,7ms/1,routeDistance:2,4ms/224,melhorEstoque:2,1ms/1,transportOriginCostMap:0,9ms/1,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:3,CaptureClaimSnapshotHits:8,CellsVisited:15913,DisembarkLzCellsVisited:316,MelhorCapturaCalls:14,MelhorCapturaCandidates:168,MelhorCapturaOutOfBandSkips:120,MelhorCapturaReachBuilds:5,MelhorCapturaReachReuses:9,MelhorCapturaTargets:44,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:21,MelhorEmbarqueEmbarkProbes:27,MelhorEmbarqueLongRangeMapBuilds:3,MelhorEmbarqueLzGateProbes:1018,MelhorEmbarqueLzGateRejects:1002,MelhorEmbarquePairs:48,MelhorEmbarquePairsNoRoute:21,MelhorEmbarquePairsReachableLater:15,MelhorEmbarquePairsReachableNow:2,MelhorEmbarquePairsStrategic:10,MelhorEmbarquePassengers:3,MelhorEstoqueCalls:1,MelhorEstoqueConfirmedSupplierQueries:1,MelhorEstoqueIndexedConstructionQueries:1,MelhorEstoqueTacticalReachReuses:1,MobilityComponentBuilds:2,MobilityComponentHits:10,MobilityComponentTouchTests:2,MovementCacheBypasses:24,MovementCacheHits:11,MovementCacheMisses:53,MovementCacheStores:29,MovementCostCellsExpanded:2440,MovementCostWaves:17,MovementQueryCachesBuilt:1356,MovementQueryConfirmedOccupancyUses:1210,MovementQueryLiveOccupancyFallbacks:146,MovementWavesBuilt:53,OwnMovementComponentBuilds:2,PathStatesExpanded:11150,QueroCaronaCacheMisses:5,QueroCaronaCacheStores:5,QueroCaronaCalls:5,QueroCaronaCaptureReachBuilds:5,QueroCaronaMobilityComponentHits:5,ReachableCellsProduced:6797,ToolProgressionCubicDirectionUses:2,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:4,TopologyIndexQueries:4,TransportPlanningCalls:4,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:2,TransportPlanningSnapshotHits:3,TurnChainedCellsExpanded:957,ValidPathCacheHits:11,ValidPathWaves:36
  #1 Soldado#11 action=attack total=6978ms decision=204 execution=6271 snapshot=0 delay=504
  #2 APC#3 action=move total=4047ms decision=2920 execution=625 snapshot=0 delay=503
  #3 Avião Tanque#13 action=move total=1369ms decision=155 execution=710 snapshot=0 delay=505
  #4 Navio Transporte#5 action=move total=1285ms decision=248 execution=534 snapshot=0 delay=502
  #5 Chinook#9 action=wait total=934ms decision=360 execution=71 snapshot=0 delay=503


[AI VERMELHO][T5] Fase2 concluída — todas as 6 unidades agiram.

[AI Perf] Stage2 (actions): 15645ms

[AI VERMELHO][T5] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,016ms memory=0,019ms geoOnly=0,006ms unitLoop=0,142ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=125

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,516ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,788ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=5 bases=1 constructions=12 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=622 sectors=5 bases=1 total=1,0ms

[SectorManager][Perf][Steps] contexts=0,5ms (search=0,0ms calls=0) sectorLoop=0,3ms neighborPass=0,1ms | search.calls=42 search.ms=0,1 search.hits=42 search.failures=22 search.exhausted=0 search.expanded=0 cache.size=36 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=165/0 tile=425/0 | constructions=12 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 7ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI VERMELHO][T5][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T5][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T5][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T5][slot=1][vermelho] reason=phase3:pre-shopping units=6 enemies=0 total=10ms

[AI Shopping Roles][T5][Red] fila de carona: 2 esperando e 3 transportador(es) em campo — sem demanda de transporte.

[AI Shopping Roles][T5][Red] doutrina rebelde: Capturador + transporte derivado da fila de carona — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T5][Red] fila unica budget=9000 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T5][Red] expansão econômica: prioriza até 2 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T5][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=9000 — caixa preservado

[AI Shopping Roles][T5][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T5] Fase3 concluída.

[AI Perf] Stage3 (shopping): 11ms

[AI VERMELHO][T5] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 21ms

[AI Perf] TURNO TOTAL (Red): 16816ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=10 ms=2,270

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=12 ms=0,732

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=4,769

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,563

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,009

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,056

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,295

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,568

[FoW][Perf][Publish] slot=0 contributors=0,003ms knownCells=0,030ms memory=0,013ms geoOnly=0,002ms unitLoop=0,178ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=6 knownCells.count=35

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=3 constructions.removed=3 total=0,583ms

[FoW][Perf][Publish] slot=1 contributors=0,012ms knownCells=0,016ms memory=0,018ms geoOnly=0,006ms unitLoop=0,141ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=125

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=6 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=2,013ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=3,967

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,018

[TurnPerf] etapa=ApplyActiveTeam.Total ms=10,259

[TurnPerf] etapa=AdvanceTurn ms=10,522

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=114,393

[FoW][Warmup] host=0 slots=0 sources=0 total=14,718ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=14,7ms

