[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 7013ms

[AI VERMELHO][T4] Fase2 — iniciando ações.

[AI VERMELHO][T4][FilaCarona] #4 embarcado — sai da fila apos 3 turno(s).

[AI VERMELHO][T4][Promessa] #8 baixa a promessa a pax=#1: passageiro embarcou.

[AI VERMELHO][T4][Promessa] #9 baixa a promessa a pax=#4: passageiro embarcou.

[AI VERMELHO][T4] Fase2 iniciativa (7 unidades):
  [grp=1] Chinook#9 @ (48, -2, 0) target=null
  [grp=2] Navio Transporte#5 @ (44, -4, 0) target=null
  [grp=4] Soldado#10 @ (52, -6, 0) target=null
  [grp=4] Soldado#11 @ (52, -5, 0) target=null
  [grp=4] APC#3 @ (56, -2, 0) target=null
  [grp=4] APC#8 @ (49, -2, 0) target=null
  [grp=4] Avião Tanque#13 @ (26, 6, 0) target=null


[AI Perf][InitiativeSetup] total=54,8ms available=2,9ms snapshot=1,6ms repair=30,1ms groups=14,8ms facts=3,8ms sort=0,9ms log=0,7ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#10 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#10 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=592; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#9 confirmedRev=4 reach=112 rideNeeds=2 tiers=3 options=1184 ranking=0

[AI VERMELHO][T4][Transporte] 9 Pickup[Tactical] recusa 1184 opcoes: carona=Requested!=Emergency=31 · rotaPax=ReachableLater=55 · rotaPax=ReachableStrategic=40 · tier=Operational!=Tactical=208 · tier=Strategic!=Tactical=850

[AI Reach][Transport:9:Evac] Tactical:miss budget=6

[AI Reach][Transport:9:Evac] Operational:disabled

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Tactical] miss

[AI Reach][Transport:9:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 9 Pickup[Operational] recusa 1184 opcoes: tier=Tactical!=Operational=126 · carona=Requested!=Emergency=55 · rotaPax=ReachableStrategic=153 · tier=Strategic!=Operational=850

[AI Reach][Transport:9:Evac] Operational:miss budget=12

[AI Reach][Transport:9:Evac] Strategic:disabled

[TransportOps][Unit#9][Evac][Operational] miss

[AI Reach][Transport:9:Pickup] Tactical:hit budget=6 action=(49, -5, 0) target=(52, -6, 0) score=101497 reason=passageiro=#10 encontro=(49, -5, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=2+1=3 dist=3

[TransportOps][Unit#9][Pickup][Tactical] hit passageiro=#10 encontro=(49, -5, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=2+1=3 dist=3

[AI VERMELHO][T4][Transporte] 9 pickup Tactical: segue MelhorEmbarque LZ=(49, -5, 0) passageiro=#10.

[AI DecisionPreview] Chinook #9 vai apenas mover de (48,-2) para (49,-5). Decisão: AI Move 9 â†’ (49, -5, 0).

[AI Step] Linha azul: (C48,L-2,0) -> (C49,L-5,0).

[FrameSpike] frame=2610 duration=709,36ms state=Neutral substep=AwaitingAction selected=(none) boardRev=32 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=665,0MB managedDelta=+5,5MB gcDelta=[0,0,0] unityAlloc=805,0MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Chinook_T1_U9 unitId=-14324 mp=6 fuel=34 rev=32

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Chinook_T1_U9 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Chinook_T1_U9

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Chinook_T1_U9

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Chinook_T1_U9

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Chinook_T1_U9

moveu para 49,-5

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Chinook_T1_U9

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Chinook_T1_U9 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,014ms knownCells=0,018ms memory=0,021ms geoOnly=0,008ms unitLoop=0,193ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=146

[FoW][Perf][Incremental] unit=Chinook_T1_U9 total=4,898ms updateCache=2,288ms collect=2,104ms collected=True cells=28 render=0,874ms visibility=0,614ms intel=0,006ms detectionSfx=0,192ms persistence=0,005ms callbacks=0,806ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Chinook#9 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Chinook#9 action=move decision=685ms execution=4232ms snapshot=0ms delay=506ms total=5423ms stages=transportPlanning:677,2ms/1,melhorEmbarque:624,7ms/1,melhorEmbarque.lzLoop:554,4ms/1,melhorEmbarque.lzGates:456,7ms/1,melhorEmbarque.resolveMeeting:80,5ms/1,melhorEmbarque.passengerReach:62,4ms/1,queroCarona:46,3ms/2,validPaths:37,0ms/4,movementCostMap:12,7ms/6,melhorCaptura:11,7ms/6,turnChainedCostMap:6,8ms/4,melhorEmbarque.longRangeMap:6,7ms/2,ownMovementComponent:3,8ms/2,melhorEmbarque.candidateCells:0,6ms/1,melhorEmbarque.transporterPaths:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:1,CellsVisited:2605,MelhorCapturaCalls:6,MelhorCapturaCandidates:72,MelhorCapturaOutOfBandSkips:48,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:5,MelhorCapturaTargets:22,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:748,MelhorEmbarqueEmbarkProbes:436,MelhorEmbarqueLongRangeMapBuilds:2,MelhorEmbarqueLzGateProbes:1050,MelhorEmbarqueLzGateRejects:458,MelhorEmbarquePairs:1184,MelhorEmbarquePairsNoRoute:748,MelhorEmbarquePairsReachableLater:95,MelhorEmbarquePairsReachableNow:46,MelhorEmbarquePairsStrategic:295,MelhorEmbarquePassengers:2,MobilityComponentBuilds:1,MobilityComponentHits:9,MovementCacheHits:1,MovementCacheMisses:9,MovementCacheStores:9,MovementCostCellsExpanded:673,MovementCostWaves:6,MovementQueryCachesBuilt:88,MovementQueryConfirmedOccupancyUses:88,MovementWavesBuilt:9,OwnMovementComponentBuilds:2,PathStatesExpanded:411,QueroCaronaCacheMisses:2,QueroCaronaCacheStores:2,QueroCaronaCalls:2,QueroCaronaCaptureReachBuilds:2,QueroCaronaMobilityComponentBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:1302,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:471,ValidPathCacheHits:1,ValidPathWaves:3

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T4][Transporte] PassengerTarget #4 MISSAO (20, 6, 0) verbo=Pressure

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#3 (carregado) Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#3 slot=0 carona=Requested ajuste=1600 fila=1t motivo=Unidade nao alcanca destino da carga (20, 6, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte] PassengerTarget #1 MISSAO (25, -2, 0) verbo=Capture

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#8 (carregado) Unidade nao alcanca destino da carga (25, -2, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#8 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Unidade nao alcanca destino da carga (25, -2, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][QueroCarona] pax=#11 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] ACCEPT pax=#11 slot=0 carona=Requested ajuste=1800 fila=3t INEGOCIÁVEL motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=17; preservadas apenas no ranking plano.

[AI VERMELHO][T4][Transporte][PlanningSnapshot] unit=#5 confirmedRev=5 reach=48 rideNeeds=3 tiers=3 options=51 ranking=0

[AI VERMELHO][T4][Transporte] 5 Pickup[Tactical] recusa 51 opcoes: carona=Requested!=Emergency=5 · rotaPax=ReachableLater=5 · rotaPax=ReachableStrategic=5 · tier=Operational!=Tactical=9 · tier=Strategic!=Tactical=27

[AI Reach][Transport:5:Evac] Tactical:miss budget=5

[AI Reach][Transport:5:Evac] Operational:disabled

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Tactical] miss

[AI Reach][Transport:5:Evac] Tactical:disabled

[AI VERMELHO][T4][Transporte] 5 Pickup[Operational] recusa 51 opcoes: tier=Tactical!=Operational=15 · carona=Requested!=Emergency=3 · rotaPax=ReachableStrategic=6 · tier=Strategic!=Operational=27

[AI Reach][Transport:5:Evac] Operational:miss budget=10

[AI Reach][Transport:5:Evac] Strategic:disabled

[TransportOps][Unit#5][Evac][Operational] miss

[AI Reach][Transport:5:Pickup] Tactical:hit budget=5 action=(45, -3, 0) target=(49, -2, 0) score=101896 reason=passageiro=#8 encontro=(45, -3, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=3+1=4 dist=2

[TransportOps][Unit#5][Pickup][Tactical] hit passageiro=#8 encontro=(45, -3, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=3+1=4 dist=2

[AI VERMELHO][T4][Transporte] 5 pickup Tactical: segue MelhorEmbarque LZ=(45, -3, 0) passageiro=#8.

[AI DecisionPreview] Navio Transporte #5 vai apenas mover de (44,-4) para (45,-3). Decisão: AI Move 5 â†’ (45, -3, 0).

[AI Step] Linha azul: (C44,L-4,0) -> (C45,L-3,0).

[FrameSpike] frame=3309 duration=272,01ms state=Neutral substep=AwaitingAction selected=(none) boardRev=35 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=708,1MB managedDelta=+10,6MB gcDelta=[0,0,0] unityAlloc=807,6MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Navio Transporte_T1_U5 unitId=-13872 mp=5 fuel=75 rev=35

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Navio Transporte_T1_U5

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Navio Transporte_T1_U5

moveu para 45,-3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Navio Transporte_T1_U5 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,013ms knownCells=0,019ms memory=0,020ms geoOnly=0,007ms unitLoop=0,166ms | recordMemory=True targetOnly=False evaluated=8 visibilityProbes=1 knownCells.count=139

[FoW][Perf][Incremental] unit=Navio Transporte_T1_U5 total=5,169ms updateCache=2,845ms collect=2,638ms collected=True cells=33 render=0,838ms visibility=0,560ms intel=0,002ms detectionSfx=0,023ms persistence=0,001ms callbacks=0,805ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Navio Transporte#5 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Navio Transporte#5 action=move decision=255ms execution=1637ms snapshot=0ms delay=508ms total=2400ms stages=transportPlanning:254,0ms/1,melhorEmbarque:250,5ms/1,melhorEmbarque.lzLoop:186,2ms/1,melhorEmbarque.lzGates:169,7ms/1,melhorEmbarque.passengerReach:63,0ms/1,queroCarona:48,2ms/3,validPaths:30,5ms/5,movementCostMap:20,0ms/9,melhorEmbarque.resolveMeeting:15,1ms/1,turnChainedCostMap:10,4ms/5,melhorEmbarque.longRangeMap:9,7ms/3,melhorCaptura:7,0ms/4,ownMovementComponent:3,0ms/1,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CellsVisited:4060,MelhorCapturaCalls:4,MelhorCapturaCandidates:48,MelhorCapturaOutOfBandSkips:36,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:3,MelhorCapturaTargets:11,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:1050,MelhorEmbarqueEmbarkProbeSkips:21,MelhorEmbarqueEmbarkProbes:30,MelhorEmbarqueLongRangeMapBuilds:3,MelhorEmbarqueLzGateProbes:1028,MelhorEmbarqueLzGateRejects:1011,MelhorEmbarquePairs:51,MelhorEmbarquePairsNoRoute:21,MelhorEmbarquePairsReachableLater:10,MelhorEmbarquePairsReachableNow:5,MelhorEmbarquePairsStrategic:15,MelhorEmbarquePassengers:3,MobilityComponentHits:12,MobilityComponentTouchTests:2,MovementCacheMisses:14,MovementCacheStores:14,MovementCostCellsExpanded:1421,MovementCostWaves:9,MovementQueryCachesBuilt:254,MovementQueryConfirmedOccupancyUses:254,MovementWavesBuilt:14,OwnMovementComponentBuilds:1,PathStatesExpanded:787,QueroCaronaCacheMisses:3,QueroCaronaCacheStores:3,QueroCaronaCalls:3,QueroCaronaCaptureReachBuilds:3,QueroCaronaMobilityComponentBuilds:1,QueroCaronaMobilityComponentHits:2,ReachableCellsProduced:2502,TopologyCellsVisited:1050,TopologyIndexCandidateCells:1050,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:802,ValidPathWaves:5

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T4][Capturador] 10 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 10 embarca (ext 3h) ? 9 slot 0 via (50, -6, 0)

[AI DecisionPreview] Soldado #10 vai embarcar em Chinook #9, movendo de (52,-6) para (50,-6).

[AI Step] Linha azul: (C52,L-6,0) -> (C50,L-6,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U10 unitId=-14550 mp=3 fuel=61 rev=37

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U10

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U10

moveu para 50,-6

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Embarcando

[TurnState] transition=MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando

[FSM][Enter] MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested

Confirma embarque 1? Chinook_T1_U9 | passageiros (0/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[TurnState] substep=AwaitingAction -> EmbarkConfirmTarget | state=Embarcando

[Embarque] Opcao 1/1 [VALIDA]
Chinook_T1_U9 | passageiros (0/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
Linha: VERDE
Custo de autonomia: 1
Botao Embarcar: habilitado
Enter confirma. ESC volta.

Confirma embarque 1? Chinook_T1_U9 | passageiros (0/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[FSM] Estado: Embarcando -> EmbarcandoExecuting

[TurnState] transition=Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin | selected=Soldado_T1_U10 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando > EmbarcandoExecuting

[FSM][Enter] Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin

[Embarque] Transportador pousou antes do embarque.

[FSM] Estado: EmbarcandoExecuting -> Neutral

[TurnState] transition=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=3 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,060ms knownCells=0,080ms memory=0,085ms geoOnly=0,032ms unitLoop=0,645ms | recordMemory=True targetOnly=False evaluated=7 visibilityProbes=1 knownCells.count=139

[FoW][Perf][Incremental] unit=Soldado_T1_U10 total=13,039ms updateCache=2,608ms collect=0,000ms collected=False cells=0 render=3,962ms visibility=2,177ms intel=0,006ms detectionSfx=0,023ms persistence=0,002ms callbacks=3,740ms splitPresentation=False

Embarque concluido em: Chinook_T1_U9 | passageiros (0/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque | custo=1 | autonomia 59->58

[Embarque] Transportador decolou apos concluir o embarque.

[TurnState] [roll back] substep=EmbarkConfirmTarget -> AwaitingAction | state=Neutral

[AI VERMELHO][T4][Missao] 10 Capture -> (25, -6, 0) predio=#7 (adquirida).

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#10 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#10 action=attack decision=211ms execution=2869ms snapshot=0ms delay=511ms total=3590ms stages=melhorCaptura:55,5ms/4,validPaths:42,4ms/6,turnChainedCostMap:11,1ms/3,queroCarona:4,5ms/1,opportunistic:2,5ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:307,MelhorCapturaCalls:4,MelhorCapturaCandidates:48,MelhorCapturaOutOfBandSkips:36,MelhorCapturaReachBuilds:2,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MovementCacheHits:4,MovementCacheMisses:2,MovementCacheStores:2,MovementQueryCachesBuilt:311,MovementQueryConfirmedOccupancyUses:311,MovementWavesBuilt:2,PathStatesExpanded:90,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:267,TurnChainedCellsExpanded:217,ValidPathCacheHits:4,ValidPathWaves:2

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T4][Capturador] 11 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI VERMELHO][T4][Capturador] 11 embarca (ext 3h) ? 9 slot 0 via (50, -5, 0)

[AI DecisionPreview] Soldado #11 vai embarcar em Chinook #9, movendo de (52,-5) para (50,-5).

[AI Step] Linha azul: (C52,L-5,0) -> (C50,L-5,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U11 unitId=-14776 mp=3 fuel=61 rev=43

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U11

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U11

moveu para 50,-5

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Embarcando

[TurnState] transition=MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando

[FSM][Enter] MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested

Confirma embarque 1? Chinook_T1_U9 | passageiros (1/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[TurnState] substep=AwaitingAction -> EmbarkConfirmTarget | state=Embarcando

[Embarque] Opcao 1/1 [VALIDA]
Chinook_T1_U9 | passageiros (1/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
Linha: VERDE
Custo de autonomia: 1
Botao Embarcar: habilitado
Enter confirma. ESC volta.

Confirma embarque 1? Chinook_T1_U9 | passageiros (1/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque
(Enter=sim, ESC=voltar para ciclar)

[FSM] Estado: Embarcando -> EmbarcandoExecuting

[TurnState] transition=Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin | selected=Soldado_T1_U11 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando > EmbarcandoExecuting

[FSM][Enter] Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin

[Embarque] Transportador pousou antes do embarque.

[FSM] Estado: EmbarcandoExecuting -> Neutral

[TurnState] transition=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=3 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,057ms knownCells=0,074ms memory=0,085ms geoOnly=0,030ms unitLoop=0,526ms | recordMemory=True targetOnly=False evaluated=6 visibilityProbes=1 knownCells.count=133

[FoW][Perf][Incremental] unit=Soldado_T1_U11 total=9,784ms updateCache=0,234ms collect=0,000ms collected=False cells=0 render=3,781ms visibility=1,861ms intel=0,005ms detectionSfx=0,013ms persistence=0,001ms callbacks=3,419ms splitPresentation=False

Embarque concluido em: Chinook_T1_U9 | passageiros (1/2) | custo=1 | movRest=1 | OBS: se escolhido, o transportador pousa antes do embarque | custo=1 | autonomia 59->58

[Embarque] Transportador decolou apos concluir o embarque.

[TurnState] [roll back] substep=EmbarkConfirmTarget -> AwaitingAction | state=Neutral

[AI VERMELHO][T4][Missao] 11 Capture -> (25, -6, 0) predio=#7 (adquirida).

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:Soldado#11 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] Soldado#11 action=attack decision=207ms execution=2646ms snapshot=0ms delay=509ms total=3363ms stages=melhorCaptura:4,3ms/3,validPaths:3,4ms/5,queroCarona:2,9ms/1,turnChainedCostMap:2,5ms/2,opportunistic:2,5ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:213,MelhorCapturaCalls:3,MelhorCapturaCandidates:36,MelhorCapturaOutOfBandSkips:24,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:11,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:376,MovementQueryConfirmedOccupancyUses:376,MovementWavesBuilt:1,PathStatesExpanded:51,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:190,TurnChainedCellsExpanded:162,ValidPathCacheHits:4,ValidPathWaves:1

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

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

[AI DecisionPreview] APC #3 vai apenas mover de (56,-2) para (50,-1). Decisão: AI Move 3 â†’ (50, -1, 0).

[AI Step] Linha azul: (C56,L-2,0) -> (C50,L-1,0).

[FrameSpike] frame=3957 duration=3194,63ms state=Neutral substep=AwaitingAction selected=(none) boardRev=97 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=684,1MB managedDelta=+22,9MB gcDelta=[0,0,0] unityAlloc=806,5MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-13420 mp=6 fuel=60 rev=97

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

[FoW][Perf][Publish] slot=1 contributors=0,010ms knownCells=0,015ms memory=0,018ms geoOnly=0,005ms unitLoop=0,125ms | recordMemory=True targetOnly=False evaluated=6 visibilityProbes=1 knownCells.count=111

[FoW][Perf][Incremental] unit=APC_T1_U3 total=6,482ms updateCache=4,231ms collect=4,007ms collected=True cells=36 render=0,912ms visibility=0,440ms intel=0,002ms detectionSfx=0,019ms persistence=0,001ms callbacks=0,786ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T4][Stock] 3 confirma missao Transport destino=(20, 6, 0) unit=#4 construction=#-1 tier=None.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T4][slot=1][vermelho] reason=phase2:APC#3 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T4][Red] APC#3 action=move decision=3181ms execution=3132ms snapshot=0ms delay=509ms total=6822ms stages=melhorDesembarque:599,1ms/2,toolProgression.TransportDelivery:184,6ms/1,validPaths:162,8ms/14,movementCostMap:11,7ms/5,transportPlanning:0,5ms/1 metrics=CellsVisited:6660,DisembarkLzCellsVisited:328,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:2,MelhorDesembarqueStructuralLzCandidates:2100,MovementCacheBypasses:12,MovementCacheMisses:19,MovementCacheStores:7,MovementCostCellsExpanded:867,MovementCostWaves:5,MovementQueryCachesBuilt:129,MovementQueryConfirmedOccupancyUses:76,MovementQueryLiveOccupancyFallbacks:53,MovementWavesBuilt:19,PathStatesExpanded:5465,ReachableCellsProduced:2486,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathWaves:14

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

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

[AI VERMELHO][T4][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=False pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Alpha@(26, 6, 0): ja_controlado assignedOk=True pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#1

[AI VERMELHO][T4][Transporte] alvo conjunto por missao: passageiro #1 intent=Capture -> (25, -2, 0).

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

[AI][Progressao2][Top] unit=8 intent=TransportDelivery from=(49, -2, 0) target=(25, -2, 0) best=(47, -2, 0) final=32000 candidatos=75 skips origin=1 occupied=2 stop=0 allow=0 score=0
  #1 (47, -2, 0) final=32000 tool=32 next=22,0 move=2 road=False prog=2,0/2,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #2 (46, -3, 0) final=31000 tool=31 next=22,0 move=3 road=False prog=2,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #3 (48, -2, 0) final=26000 tool=26 next=22,0 move=1 road=False prog=1,0/2,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #4 (47, -3, 0) final=25000 tool=25 next=22,0 move=2 road=False prog=1,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #5 (47, -4, 0) final=24000 tool=24 next=22,0 move=3 road=False prog=1,0/2,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #6 (46, -5, 0) final=23000 tool=23 next=22,0 move=4 road=False prog=1,0/2,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #7 (48, -3, 0) final=19000 tool=19 next=22,0 move=1 road=False prog=0,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #8 (48, -1, 0) final=19000 tool=19 next=22,0 move=6 road=False prog=0,0/2,0 route=? progR=? line=1,0 threat=0,0 dpq=3,0 tactical=0
  #9 (48, -4, 0) final=18000 tool=18 next=22,0 move=2 road=False prog=0,0/2,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #10 (47, -5, 0) final=17000 tool=17 next=22,0 move=3 road=False prog=0,0/2,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #11 (47, -6, 0) final=16000 tool=16 next=22,0 move=4 road=False prog=0,0/2,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #12 (47, 3, 0) final=-1000 tool=-1 next=23,0 move=5 road=False prog=-1,0/1,0 route=? progR=? line=5,0 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T4][Transporte] Terrestre 8 reach estrategico: alvo cubico (25, -2, 0) -> ancora (25, -2, 0); progride (49, -2, 0)->(47, -2, 0) (toolIntent=TransportDelivery tool=32 next=22,0 moveCost=2 roadBonus=False prog=2,0/2,0 route=? progR=? line=0,0 dpq=1,0 threat=0,0 tactical=0 final=32000).

[AI VERMELHO][T4][Transporte] 8 larga no TACTICAL (rota restante <= 3).

[AI DecisionPreview] APC #8 vai apenas mover de (49,-2) para (47,-2). Decisão: AI Move 8 â†’ (47, -2, 0).

[AI Step] Linha azul: (C49,L-2,0) -> (C47,L-2,0).

[FrameSpike] frame=4253 duration=2910,55ms state=Neutral substep=AwaitingAction selected=(none) boardRev=157 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=643,4MB managedDelta=-53,9MB gcDelta=[1,1,1] unityAlloc=806,2MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U8 unitId=-14098 mp=6 fuel=53 rev=157

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

[FoW][Perf][Publish] slot=1 contributors=0,010ms knownCells=0,015ms memory=0,019ms geoOnly=0,005ms unitLoop=0,118ms | recordMemory=True targetOnly=False evaluated=6 visibilityProbes=1 knownCells.count=111

[FoW][Perf][Incremental] unit=APC_T1_U8 total=4,655ms updateCache=2,406ms collect=2,364ms collected=True cells=24 render=0,925ms visibility=0,429ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,782ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T4][Stock] 8 confirma missao Transport destino=(25, -2, 0) unit=#1 construction=#-1 tier=None.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

