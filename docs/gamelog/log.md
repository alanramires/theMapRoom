[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 10159ms

[AI VERMELHO][T1] Fase2 — iniciando ações.

[AI VERMELHO][T1] Fase2 iniciativa (2 unidades):
  [grp=2] APC#3 @ (19, 2, 0) target=null
  [grp=4] Soldado#1 @ (20, -5, 0) target=null


[AI Perf][InitiativeSetup] total=20,2ms available=3,3ms snapshot=1,8ms repair=1,2ms groups=10,1ms facts=2,2ms sort=0,9ms log=0,7ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T1][Transporte][QueroCarona] pax=#1 Infantaria alcança prédio capturável próximo (20, -6, 0) [reserva 1:1 capturador=#1] no Tactical: custo=1 no turno 1 (nesta rodada). Recusa carona.

[AI VERMELHO][T1][Transporte][MelhorEmbarque] ACCEPT pax=#1 slot=0 carona=OpportunisticFallback ajuste=-5000 motivo=Infantaria alcança prédio capturável próximo (20, -6, 0) [reserva 1:1 capturador=#1] no Tactical: custo=1 no turno 1 (nesta rodada). Recusa carona.

[AI VERMELHO][T1][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=334; preservadas apenas no ranking plano.

[AI VERMELHO][T1][Transporte][PlanningSnapshot] unit=#3 confirmedRev=1 reach=63 rideNeeds=1 tiers=3 options=334 ranking=0

[AI Reach][Transport:3:Evac] Tactical:miss budget=6

[AI Reach][Transport:3:Evac] Operational:disabled

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Tactical] miss

[AI Reach][Transport:3:Evac] Tactical:disabled

[AI Reach][Transport:3:Evac] Operational:miss budget=12

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Operational] miss

[AI Reach][Transport:3:Pickup] Tactical:miss budget=6

[AI Reach][Transport:3:Pickup] Operational:disabled

[AI Reach][Transport:3:Pickup] Strategic:disabled

[TransportOps][Unit#3][Pickup][Tactical] miss

[AI Reach][Transport:3:Pickup] Tactical:disabled

[AI Reach][Transport:3:Pickup] Operational:miss budget=12

[AI Reach][Transport:3:Pickup] Strategic:disabled

[TransportOps][Unit#3][Pickup][Operational] miss

[AI Reach][Transport:3:Evac] Tactical:disabled

[AI Reach][Transport:3:Evac] Operational:disabled

[AI Reach][Transport:3:Evac] Strategic:miss budget=2147483647

[TransportOps][Unit#3][Evac][Strategic] miss

[AI Reach][Transport:3:Pickup] Tactical:disabled

[AI Reach][Transport:3:Pickup] Operational:disabled

[AI Reach][Transport:3:Pickup] Strategic:miss budget=2147483647

[TransportOps][Unit#3][Pickup][Strategic] miss

[AI VERMELHO][T1][Transporte] 3 sem pedido materializavel apos Tactical/Operational/Strategic; rebelde aguarda nova oportunidade.

[AI DecisionPreview] APC #3 vai apenas mover de (19,2) para (19,2). Decisão: AI Move 3 â†’ (19, 2, 0).

[AI Step] Origem e destino iguais; linha azul nao foi exibida.

[AI Step] Preview visual indisponivel para este batch.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=70 rev=0

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Neutral

[TurnState] transition=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=1 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,011ms geoOnly=0,002ms unitLoop=0,052ms | recordMemory=True targetOnly=False evaluated=3 visibilityProbes=1 knownCells.count=37

[FoW][Perf][Incremental] unit=APC_T1_U3 total=1,156ms updateCache=0,067ms collect=0,000ms collected=False cells=0 render=0,162ms visibility=0,228ms intel=0,006ms detectionSfx=0,173ms persistence=0,005ms callbacks=0,387ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf][Unit][T1][Red] APC#3 action=wait decision=182ms execution=987ms snapshot=0ms delay=508ms total=1677ms stages=transportPlanning:160,7ms/1,melhorEmbarque:114,1ms/1,melhorEmbarque.lzLoop:53,8ms/1,melhorEmbarque.passengerReach:46,3ms/1,melhorEmbarque.lzGates:40,7ms/1,queroCarona:33,3ms/1,validPaths:31,3ms/3,melhorEmbarque.resolveMeeting:8,1ms/1,turnChainedCostMap:6,1ms/2,ownMovementComponent:6,0ms/1,melhorCaptura:5,4ms/2,movementCostMap:3,5ms/2,melhorEmbarque.candidateCells:0,6ms/1,melhorEmbarque.transporterPaths:0,0ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CellsVisited:1004,MelhorCapturaCalls:2,MelhorCapturaCandidates:12,MelhorCapturaOutOfBandSkips:10,MelhorCapturaReachReuses:2,MelhorCapturaTargets:2,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:346,MelhorEmbarqueEmbarkProbeSkips:279,MelhorEmbarqueEmbarkProbes:55,MelhorEmbarqueLzGateProbes:334,MelhorEmbarquePairs:334,MelhorEmbarquePairsNoRoute:291,MelhorEmbarquePairsOpportunistic:334,MelhorEmbarquePairsReachableLater:28,MelhorEmbarquePairsReachableNow:15,MelhorEmbarquePassengers:1,MobilityComponentBuilds:1,MovementCacheHits:1,MovementCacheMisses:4,MovementCacheStores:4,MovementCostCellsExpanded:58,MovementCostWaves:2,MovementQueryCachesBuilt:20,MovementQueryConfirmedOccupancyUses:20,MovementWavesBuilt:4,OwnMovementComponentBuilds:1,PathStatesExpanded:207,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:525,TopologyCellsVisited:346,TopologyIndexCandidateCells:346,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:5,TurnChainedCellsExpanded:393,ValidPathCacheHits:1,ValidPathWaves:2

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T1][Oportunista] 1 captura local perto Bravo @ (20, -6, 0) antes de embarcar score=2000 rally=False

[AI DecisionPreview] Soldado #1 vai capturar no hex (20,-6), movendo de (20,-5) para (20,-6).

[AI Step] Linha azul: (C20,L-5,0) -> (C20,L-6,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=70 rev=0

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 20,-6

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Capturando

[TurnState] transition=MoveuAndando -> Capturando | reason=HandleCaptureActionRequested | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando > Capturando

[FSM][Enter] MoveuAndando -> Capturando | reason=HandleCaptureActionRequested

[FSM] Estado: Capturando -> CapturandoExecuting

[TurnState] transition=Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando > Capturando > CapturandoExecuting

[FSM][Enter] Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin

[Captura] Soldado_T1_U1 causou 10 de captura em Garagem APC (10 -> 0).

[Captura] Construcao capturada por vermelho. Capture resetado para 10/10.

[FSM] Estado: CapturandoExecuting -> Neutral

[TurnState] transition=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,007ms memory=0,009ms geoOnly=0,002ms unitLoop=0,083ms | recordMemory=True targetOnly=False evaluated=3 visibilityProbes=1 knownCells.count=36

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=1,917ms updateCache=1,043ms collect=0,857ms collected=True cells=14 render=0,178ms visibility=0,248ms intel=0,001ms detectionSfx=0,017ms persistence=0,002ms callbacks=0,358ms splitPresentation=False

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 8251ms

[AI VERMELHO][T2] Fase2 — iniciando ações.

[AI VERMELHO][T2] Fase2 iniciativa (2 unidades):
  [grp=4] Soldado#1 @ (20, -6, 0) target=null
  [grp=4] APC#3 @ (19, 2, 0) target=null


[AI Perf][InitiativeSetup] total=0,6ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,1ms facts=0,2ms sort=0,0ms log=0,1ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T2][FilaCarona] #1 entra na fila no turno 2 — fora das bandas (score=1000).

[AI VERMELHO][T2][Capturador] 1 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T2][Capturador] 1 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  heli/trans 3@(19, 2, 0) dist=8 sector=free formal=- slot=0 same=False compat=False prod=False acted=False repair=False empty


[AI VERMELHO][T2][SemPlano] 1 âncora = capturável (20, 6, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T2][Rogue] 1 marcha para âncora (20, 6, 0) via (20, -3, 0)

[AI DecisionPreview] Soldado #1 vai apenas mover de (20,-6) para (20,-3). Decisão: AI Move 1 â†’ (20, -3, 0).

[AI Step] Linha azul: (C20,L-6,0) -> (C20,L-3,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=70 rev=2

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 20,-3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,071ms | recordMemory=True targetOnly=False evaluated=3 visibilityProbes=1 knownCells.count=37

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=4,764ms updateCache=3,835ms collect=3,718ms collected=True cells=20 render=0,162ms visibility=0,231ms intel=0,001ms detectionSfx=0,016ms persistence=0,001ms callbacks=0,446ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T2][Missao] 1 Capture -> (20, 6, 0) predio=#4 (adquirida).

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Commit Light][T2][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T2][Red] Soldado#1 action=move decision=75ms execution=1995ms snapshot=0ms delay=501ms total=2571ms stages=routeDistance:26,0ms/16,turnChainedCostMap:11,9ms/4,queroCarona:10,5ms/1,ownMovementComponent:10,1ms/2,validPaths:3,2ms/6,melhorCaptura:3,1ms/3,opportunistic:1,8ms/2,aggressive:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:3,CellsVisited:808,MelhorCapturaCalls:3,MelhorCapturaCandidates:18,MelhorCapturaOutOfBandSkips:10,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:5,MobilityComponentBuilds:1,MobilityComponentHits:4,MovementCacheHits:5,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:343,MovementQueryConfirmedOccupancyUses:343,MovementWavesBuilt:1,OwnMovementComponentBuilds:2,PathStatesExpanded:26,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentBuilds:1,ReachableCellsProduced:786,TurnChainedCellsExpanded:782,ValidPathCacheHits:5,ValidPathWaves:1

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T2][Transporte][QueroCarona] pax=#1 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T2][Transporte][MelhorEmbarque] ACCEPT pax=#1 slot=0 carona=Requested ajuste=1000 motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T2][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=334; preservadas apenas no ranking plano.

[AI VERMELHO][T2][Transporte][PlanningSnapshot] unit=#3 confirmedRev=3 reach=63 rideNeeds=1 tiers=3 options=334 ranking=0

[AI Reach][Transport:3:Evac] Tactical:miss budget=6

[AI Reach][Transport:3:Evac] Operational:disabled

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Tactical] miss

[AI Reach][Transport:3:Evac] Tactical:disabled

[AI Reach][Transport:3:Evac] Operational:miss budget=12

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Operational] miss

[AI Reach][Transport:3:Pickup] Tactical:miss budget=6

[AI Reach][Transport:3:Pickup] Operational:disabled

[AI Reach][Transport:3:Pickup] Strategic:disabled

[TransportOps][Unit#3][Pickup][Tactical] miss

[AI Reach][Transport:3:Pickup] Tactical:disabled

[AI VERMELHO][T2][Promessa] #3 promete resgate de pax=#1 em (19, -5, 0)

[AI Reach][Transport:3:Pickup] Operational:hit budget=12 action=(19, -5, 0) target=(20, -3, 0) score=99298 reason=passageiro=#1 encontro=(19, -5, 0) tier=Operational carona=Requested rotaPax=ReachableLater custoPax=1+1=2 dist=7

[TransportOps][Unit#3][Pickup][Operational] hit passageiro=#1 encontro=(19, -5, 0) tier=Operational carona=Requested rotaPax=ReachableLater custoPax=1+1=2 dist=7

[AI][Progressao2][Top] unit=3 intent=TransportRendezvous from=(19, 2, 0) target=(19, -5, 0) best=(19, -4, 0) final=106000 candidatos=61 skips origin=1 occupied=1 stop=0 allow=0 score=0
  #1 (19, -4, 0) final=106000 tool=106 next=0,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #2 (20, -4, 0) final=105000 tool=105 next=0,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #3 (19, -3, 0) final=100000 tool=100 next=0,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #4 (18, -4, 0) final=99000 tool=99 next=0,0 move=6 road=False prog=5,0/7,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #5 (18, -3, 0) final=99000 tool=99 next=0,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #6 (19, -2, 0) final=94000 tool=94 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=0,0 threat=0,0 dpq=1,0 tactical=0
  #7 (18, -2, 0) final=93000 tool=93 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #8 (20, -2, 0) final=93000 tool=93 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #9 (17, -3, 0) final=92000 tool=92 next=0,0 move=5 road=False prog=4,0/7,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #10 (17, -4, 0) final=92000 tool=92 next=0,0 move=6 road=False prog=4,0/7,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #11 (16, -3, 0) final=85000 tool=85 next=0,0 move=5 road=False prog=3,0/7,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #12 (16, -4, 0) final=85000 tool=85 next=0,0 move=6 road=False prog=3,0/7,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T2][Transporte] 3 pickup Operational: progride para MelhorEmbarque LZ=(19, -5, 0) via=(19, -4, 0) passageiro=#1 (toolIntent=TransportRendezvous tool=106 next=0,0 moveCost=6 roadBonus=False prog=6,0/7,0 route=? progR=? line=0,0 dpq=1,0 threat=0,0 tactical=0 final=106000).

[AI DecisionPreview] APC #3 vai apenas mover de (19,2) para (19,-4). Decisão: AI Move 3 â†’ (19, -4, 0).

[AI Step] Linha azul: (C19,L2,0) -> (C19,L-4,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=70 rev=29

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 19,-4

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,047ms | recordMemory=True targetOnly=False evaluated=3 visibilityProbes=1 knownCells.count=30

[FoW][Perf][Incremental] unit=APC_T1_U3 total=3,591ms updateCache=2,696ms collect=2,562ms collected=True cells=27 render=0,217ms visibility=0,218ms intel=0,002ms detectionSfx=0,016ms persistence=0,001ms callbacks=0,371ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 5305ms

[AI VERMELHO][T3] Fase2 — iniciando ações.

[AI VERMELHO][T3] Fase2 iniciativa (2 unidades):
  [grp=2] APC#3 @ (19, -4, 0) target=null
  [grp=4] Soldado#1 @ (20, -3, 0) target=null


[AI Perf][InitiativeSetup] total=5,8ms available=0,1ms snapshot=0,1ms repair=5,1ms groups=0,2ms facts=0,2ms sort=0,0ms log=0,1ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T3][Transporte][QueroCarona] pax=#1 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T3][Transporte][MelhorEmbarque] ACCEPT pax=#1 slot=0 carona=Requested ajuste=1100 fila=1t motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T3][Transporte][MelhorEmbarque] Ranking encerrado no Tactical: existe passageiro Requested + ReachableNow e nenhuma emergência pendente.

[AI VERMELHO][T3][Transporte][MelhorEmbarque] LZs sem passageiro ReachableNow=62; preservadas apenas no ranking plano.

[AI VERMELHO][T3][Transporte][PlanningSnapshot] unit=#3 confirmedRev=4 reach=62 rideNeeds=1 tiers=3 options=62 ranking=0

[AI Reach][Transport:3:Evac] Tactical:miss budget=6

[AI Reach][Transport:3:Evac] Operational:disabled

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Tactical] miss

[AI Reach][Transport:3:Evac] Tactical:disabled

[AI Reach][Transport:3:Evac] Operational:miss budget=12

[AI Reach][Transport:3:Evac] Strategic:disabled

[TransportOps][Unit#3][Evac][Operational] miss

[AI Reach][Transport:3:Pickup] Tactical:hit budget=6 action=(19, -4, 0) target=(20, -3, 0) score=101098 reason=passageiro=#1 encontro=(19, -4, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=1+1=2 dist=0

[TransportOps][Unit#3][Pickup][Tactical] hit passageiro=#1 encontro=(19, -4, 0) tier=Tactical carona=Requested rotaPax=ReachableNow custoPax=1+1=2 dist=0

[AI VERMELHO][T3][Transporte] 3 pickup Tactical: aguarda na LZ (19, -4, 0) passageiro=#1 carona=Requested rotaPax=ReachableNow.

[AI DecisionPreview] APC #3 vai apenas mover de (19,-4) para (19,-4). Decisão: AI Move 3 â†’ (19, -4, 0).

[AI Step] Origem e destino iguais; linha azul nao foi exibida.

[AI Step] Preview visual indisponivel para este batch.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=64 rev=35

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Neutral

[TurnState] transition=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuParado -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=1 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,048ms | recordMemory=True targetOnly=False evaluated=3 visibilityProbes=1 knownCells.count=30

[FoW][Perf][Incremental] unit=APC_T1_U3 total=0,911ms updateCache=0,004ms collect=0,000ms collected=False cells=0 render=0,214ms visibility=0,214ms intel=0,001ms detectionSfx=0,019ms persistence=0,001ms callbacks=0,387ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf][Unit][T3][Red] APC#3 action=wait decision=34ms execution=965ms snapshot=0ms delay=505ms total=1504ms stages=transportPlanning:33,3ms/1,melhorEmbarque:24,8ms/1,melhorEmbarque.lzLoop:16,5ms/1,validPaths:10,8ms/2,melhorEmbarque.lzGates:7,9ms/1,melhorEmbarque.passengerReach:7,8ms/1,melhorEmbarque.resolveMeeting:7,6ms/1,queroCarona:5,7ms/1,movementCostMap:2,0ms/2,turnChainedCostMap:1,0ms/1,melhorCaptura:0,6ms/3,melhorEmbarque.transporterPaths:0,0ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CellsVisited:419,MelhorCapturaCalls:3,MelhorCapturaCandidates:18,MelhorCapturaOutOfBandSkips:10,MelhorCapturaReachReuses:3,MelhorCapturaTargets:5,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:346,MelhorEmbarqueDecisiveTacticalEarlyOuts:1,MelhorEmbarqueEmbarkProbeSkips:3,MelhorEmbarqueEmbarkProbes:59,MelhorEmbarqueLzGateProbes:62,MelhorEmbarquePairs:62,MelhorEmbarquePairsNoRoute:12,MelhorEmbarquePairsReachableLater:30,MelhorEmbarquePairsReachableNow:20,MelhorEmbarquePassengers:1,MobilityComponentHits:1,MovementCacheMisses:4,MovementCacheStores:4,MovementCostCellsExpanded:71,MovementCostWaves:2,MovementQueryCachesBuilt:24,MovementQueryConfirmedOccupancyUses:24,MovementWavesBuilt:4,PathStatesExpanded:235,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:204,TopologyCellsVisited:62,TopologyIndexCandidateCells:62,TopologyIndexHits:1,TopologyIndexQueries:1,TransportPlanningCalls:1,TransportPlanningReachReuses:2,TransportPlanningSnapshotBuilds:1,TransportPlanningSnapshotHits:2,TurnChainedCellsExpanded:51,ValidPathWaves:2

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T3][Capturador] 1 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T3][Capturador] 1 embarca (ext 2h) ? 3 slot 0 via (20, -4, 0)

[AI DecisionPreview] Soldado #1 vai embarcar em APC #3, movendo de (20,-3) para (20,-4).

[AI Step] Linha azul: (C20,L-3,0) -> (C20,L-4,0).

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=67 rev=35

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 20,-4

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Embarcando

[TurnState] transition=MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando

[FSM][Enter] MoveuAndando -> Embarcando | reason=HandleEmbarkActionRequested

Confirma embarque 1? APC_T1_U3 | passageiro (0/1) | custo=1 | movRest=2
(Enter=sim, ESC=voltar para ciclar)

[TurnState] substep=AwaitingAction -> EmbarkConfirmTarget | state=Embarcando

[Embarque] Opcao 1/1 [VALIDA]
APC_T1_U3 | passageiro (0/1) | custo=1 | movRest=2
Linha: VERDE
Custo de autonomia: 1
Botao Embarcar: habilitado
Enter confirma. ESC volta.

Confirma embarque 1? APC_T1_U3 | passageiro (0/1) | custo=1 | movRest=2
(Enter=sim, ESC=voltar para ciclar)

[FSM] Estado: Embarcando -> EmbarcandoExecuting

[TurnState] transition=Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando > Embarcando > EmbarcandoExecuting

[FSM][Enter] Embarcando -> EmbarcandoExecuting | reason=ExecuteEmbarkSequence: begin

[FSM] Estado: EmbarcandoExecuting -> Neutral

[TurnState] transition=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=EmbarcandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=3 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,006ms memory=0,007ms geoOnly=0,002ms unitLoop=0,031ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=28

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=1,413ms updateCache=0,559ms collect=0,000ms collected=False cells=0 render=0,214ms visibility=0,172ms intel=0,001ms detectionSfx=0,005ms persistence=0,000ms callbacks=0,371ms splitPresentation=False

Embarque concluido em: APC_T1_U3 | passageiro (0/1) | custo=1 | movRest=2 | custo=1 | autonomia 66->65

[TurnState] [roll back] substep=EmbarkConfirmTarget -> AwaitingAction | state=Neutral

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[FrameSpike] frame=9661 duration=1924,92ms state=Neutral substep=AwaitingAction selected=(none) boardRev=38 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=722,4MB managedDelta=+0,2MB gcDelta=[0,0,0] unityAlloc=978,2MB

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 6469ms

[AI VERMELHO][T4] Fase2 — iniciando ações.

[AI VERMELHO][T4][Promessa] #3 baixa a promessa a pax=#1: passageiro embarcou.

[AI VERMELHO][T4][FilaCarona] #1 embarcado — sai da fila apos 2 turno(s).

[AI VERMELHO][T4] Fase2 iniciativa (1 unidades):
  [grp=4] APC#3 @ (19, -4, 0) target=null


[AI Perf][InitiativeSetup] total=0,5ms available=0,1ms snapshot=0,1ms repair=0,2ms groups=0,0ms facts=0,0ms sort=0,0ms log=0,1ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T4][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T4][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI Reach][Transport:3:Courier] Tactical:hit budget=6 action=(20, 6, 0) target=(20, 6, 0) score=90000 reason=carga embarcada count=1 dist=10

[TransportOps][Unit#3][Courier][Tactical] hit carga embarcada count=1 dist=10

[AI VERMELHO][T4][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T4][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI VERMELHO][T4][Transporte] 3 courier — passageiro #1 alvo=(20, 6, 0) range=6 (Operational; Tactical=3) distAtual=10h

[AI VERMELHO][T4][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=False pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=True pax=#1

[AI VERMELHO][T4][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#1

[AI VERMELHO][T4][Transporte] alvo conjunto por missao: passageiro #1 intent=Capture -> (20, 6, 0).

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Tactical:miss budget=6

[AI Reach][TransportDelivery:3] Operational:disabled

[AI Reach][TransportDelivery:3] Strategic:disabled

[AI Reach][TransportDelivery:3] Tactical:disabled

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(20, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(14, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(13, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(7, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(7, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(7, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(8, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(9, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(10, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(11, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T4][Transporte][MelhorDesembarque] LZ=(12, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Operational:miss budget=12

[AI Reach][TransportDelivery:3] Strategic:hit budget=120 action=(20, 6, 0) target=(20, 6, 0) score=-10 reason=cubic=10

[AI][Progressao2][Top] unit=3 intent=TransportDelivery from=(19, -4, 0) target=(20, 6, 0) best=(20, 2, 0) final=106000 candidatos=61 skips origin=1 occupied=0 stop=0 allow=0 score=0
  #1 (20, 2, 0) final=106000 tool=106 next=3,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=0,4 threat=0,0 dpq=1,0 tactical=0
  #2 (19, 2, 0) final=105000 tool=105 next=3,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=0,6 threat=0,0 dpq=1,0 tactical=0
  #3 (18, 2, 0) final=104000 tool=104 next=3,0 move=6 road=False prog=6,0/7,0 route=? progR=? line=1,6 threat=0,0 dpq=1,0 tactical=0
  #4 (20, 1, 0) final=90000 tool=90 next=4,0 move=5 road=False prog=5,0/6,0 route=? progR=? line=0,5 threat=0,0 dpq=1,0 tactical=0
  #5 (19, 1, 0) final=90000 tool=90 next=4,0 move=5 road=False prog=5,0/6,0 route=? progR=? line=0,5 threat=0,0 dpq=1,0 tactical=0
  #6 (18, 1, 0) final=89000 tool=89 next=4,0 move=5 road=False prog=5,0/6,0 route=? progR=? line=1,5 threat=0,0 dpq=1,0 tactical=0
  #7 (17, 1, 0) final=88000 tool=88 next=4,0 move=5 road=False prog=5,0/6,0 route=? progR=? line=2,5 threat=0,0 dpq=1,0 tactical=0
  #8 (17, 2, 0) final=87000 tool=87 next=4,0 move=6 road=False prog=5,0/6,0 route=? progR=? line=2,6 threat=0,0 dpq=1,0 tactical=0
  #9 (18, 0, 0) final=83000 tool=83 next=4,0 move=4 road=False prog=4,0/6,0 route=? progR=? line=1,4 threat=0,0 dpq=1,0 tactical=0
  #10 (17, 0, 0) final=82000 tool=82 next=4,0 move=4 road=False prog=4,0/6,0 route=? progR=? line=2,4 threat=0,0 dpq=1,0 tactical=0
  #11 (16, 1, 0) final=81000 tool=81 next=4,0 move=5 road=False prog=4,0/6,0 route=? progR=? line=3,5 threat=0,0 dpq=1,0 tactical=0
  #12 (16, 2, 0) final=80000 tool=80 next=4,0 move=6 road=False prog=4,0/6,0 route=? progR=? line=3,6 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T4][Transporte] Terrestre 3 reach estrategico: alvo cubico (20, 6, 0) -> ancora (20, 6, 0); progride (19, -4, 0)->(20, 2, 0) (toolIntent=TransportDelivery tool=106 next=3,0 moveCost=6 roadBonus=False prog=6,0/7,0 route=? progR=? line=0,4 dpq=1,0 threat=0,0 tactical=0 final=106000).

[AI DecisionPreview] APC #3 vai apenas mover de (19,-4) para (20,2). Decisão: AI Move 3 â†’ (20, 2, 0).

[AI Step] Linha azul: (C19,L-4,0) -> (C20,L2,0).

[FrameSpike] frame=12282 duration=646,98ms state=Neutral substep=AwaitingAction selected=(none) boardRev=86 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=728,8MB managedDelta=+7,6MB gcDelta=[0,0,0] unityAlloc=978,8MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=64 rev=86

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 20,2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,006ms memory=0,007ms geoOnly=0,001ms unitLoop=0,074ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=18

[FoW][Perf][Incremental] unit=APC_T1_U3 total=5,563ms updateCache=4,607ms collect=4,430ms collected=True cells=16 render=0,263ms visibility=0,221ms intel=0,002ms detectionSfx=0,017ms persistence=0,001ms callbacks=0,382ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 4760ms

[AI VERMELHO][T5] Fase2 — iniciando ações.

[AI VERMELHO][T5] Fase2 iniciativa (1 unidades):
  [grp=4] APC#3 @ (20, 2, 0) target=null


[AI Perf][InitiativeSetup] total=0,3ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,0ms sort=0,0ms log=0,1ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T5][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T5][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI Reach][Transport:3:Courier] Tactical:hit budget=6 action=(20, 6, 0) target=(20, 6, 0) score=90000 reason=carga embarcada count=1 dist=4

[TransportOps][Unit#3][Courier][Tactical] hit carga embarcada count=1 dist=4

[AI VERMELHO][T5][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T5][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI VERMELHO][T5][Transporte] 3 courier — passageiro #1 alvo=(20, 6, 0) range=6 (Operational; Tactical=3) distAtual=4h

[AI VERMELHO][T5][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=False pax=#1

[AI VERMELHO][T5][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#1

[AI VERMELHO][T5][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=True pax=#1

[AI VERMELHO][T5][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#1

[AI VERMELHO][T5][Transporte] alvo conjunto por missao: passageiro #1 intent=Capture -> (20, 6, 0).

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Tactical:miss budget=6

[AI Reach][TransportDelivery:3] Operational:disabled

[AI Reach][TransportDelivery:3] Strategic:disabled

[AI Reach][TransportDelivery:3] Tactical:disabled

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(20, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(19, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(18, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(17, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(16, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(15, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(14, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(13, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(8, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(8, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(8, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(9, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(10, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(11, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T5][Transporte][MelhorDesembarque] LZ=(12, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Operational:miss budget=12

[AI Reach][TransportDelivery:3] Strategic:hit budget=120 action=(20, 6, 0) target=(20, 6, 0) score=-4 reason=cubic=4

[AI][Progressao2][Top] unit=3 intent=TransportDelivery from=(20, 2, 0) target=(20, 6, 0) best=(19, 2, 0) final=-1000 candidatos=51 skips origin=1 occupied=0 stop=0 allow=0 score=0
  #1 (19, 2, 0) final=-1000 tool=-1 next=4,0 move=1 road=False prog=0,0/0,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #2 (18, 2, 0) final=-2000 tool=-2 next=4,0 move=2 road=False prog=0,0/0,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #3 (17, 3, 0) final=-3000 tool=-3 next=4,0 move=3 road=False prog=0,0/0,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #4 (17, 4, 0) final=-3000 tool=-3 next=4,0 move=4 road=False prog=0,0/0,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #5 (16, 6, 0) final=-4000 tool=-4 next=4,0 move=6 road=False prog=0,0/0,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #6 (16, 5, 0) final=-4000 tool=-4 next=4,0 move=5 road=False prog=0,0/0,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #7 (18, 1, 0) final=-8000 tool=-8 next=4,0 move=2 road=False prog=-1,0/0,0 route=? progR=? line=2,2 threat=0,0 dpq=1,0 tactical=0
  #8 (17, 1, 0) final=-9000 tool=-9 next=4,0 move=3 road=False prog=-1,0/0,0 route=? progR=? line=3,2 threat=0,0 dpq=1,0 tactical=0
  #9 (17, 2, 0) final=-9000 tool=-9 next=4,0 move=3 road=False prog=-1,0/0,0 route=? progR=? line=3,0 threat=0,0 dpq=1,0 tactical=0
  #10 (16, 3, 0) final=-10000 tool=-10 next=4,0 move=4 road=False prog=-1,0/0,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #11 (16, 4, 0) final=-10000 tool=-10 next=4,0 move=5 road=False prog=-1,0/0,0 route=? progR=? line=4,0 threat=0,0 dpq=1,0 tactical=0
  #12 (15, 5, 0) final=-11000 tool=-11 next=4,0 move=6 road=False prog=-1,0/0,0 route=? progR=? line=5,0 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T5][Transporte] Terrestre 3 reach estrategico: alvo cubico (20, 6, 0) -> ancora (20, 6, 0); progride (20, 2, 0)->(19, 2, 0) (toolIntent=TransportDelivery tool=-1 next=4,0 moveCost=1 roadBonus=False prog=0,0/0,0 route=? progR=? line=1,0 dpq=1,0 threat=0,0 tactical=0 final=-1000).

[AI DecisionPreview] APC #3 vai apenas mover de (20,2) para (19,2). Decisão: AI Move 3 â†’ (19, 2, 0).

[AI Step] Linha azul: (C20,L2,0) -> (C19,L2,0).

[FrameSpike] frame=14409 duration=451,03ms state=Neutral substep=AwaitingAction selected=(none) boardRev=146 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=718,9MB managedDelta=+5,9MB gcDelta=[0,0,0] unityAlloc=978,1MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=58 rev=146

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 19,2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,006ms memory=0,006ms geoOnly=0,001ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=23

[FoW][Perf][Incremental] unit=APC_T1_U3 total=6,734ms updateCache=5,861ms collect=5,723ms collected=True cells=21 render=0,228ms visibility=0,173ms intel=0,001ms detectionSfx=0,016ms persistence=0,001ms callbacks=0,389ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Preparando proximo batch.

[AI Shortcuts] F11 — AI Step

[AI Perf] PRE-Stage2 acumulado: 5065ms

[AI VERMELHO][T6] Fase2 — iniciando ações.

[AI VERMELHO][T6] Fase2 iniciativa (1 unidades):
  [grp=4] APC#3 @ (19, 2, 0) target=null


[AI Perf][InitiativeSetup] total=0,3ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,0ms sort=0,0ms log=0,1ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[AI VERMELHO][T6][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T6][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI Reach][Transport:3:Courier] Tactical:hit budget=6 action=(20, 6, 0) target=(20, 6, 0) score=90000 reason=carga embarcada count=1 dist=4

[TransportOps][Unit#3][Courier][Tactical] hit carga embarcada count=1 dist=4

[AI VERMELHO][T6][Rebelde] 1 mantem DesignatedCaptureTarget #4 em (20, 6, 0).

[AI VERMELHO][T6][Transporte] PassengerTarget #1 rebelde ? capturavel proximo (20, 6, 0)

[AI VERMELHO][T6][Transporte] 3 courier — passageiro #1 alvo=(20, 6, 0) range=6 (Operational; Tactical=3) distAtual=4h

[AI VERMELHO][T6][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=False pax=#1

[AI VERMELHO][T6][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=False pax=#1

[AI VERMELHO][T6][Transporte] courier local-op rejeita Bravo@(20, -6, 0): ja_controlado assignedOk=True pax=#1

[AI VERMELHO][T6][Transporte] courier local-op rejeita Base0@(-7, 0, 0): setor_base assignedOk=True pax=#1

[AI VERMELHO][T6][Transporte] alvo conjunto por missao: passageiro #1 intent=Capture -> (20, 6, 0).

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Tactical:miss budget=6

[AI Reach][TransportDelivery:3] Operational:disabled

[AI Reach][TransportDelivery:3] Strategic:disabled

[AI Reach][TransportDelivery:3] Tactical:disabled

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(19, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(18, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(20, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(17, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(16, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(15, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(14, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(13, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(12, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(7, 2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(7, 1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(7, 3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 0, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, -1, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(8, 5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, -2, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, 6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(9, -3, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, -4, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(10, -5, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI VERMELHO][T6][Transporte][MelhorDesembarque] LZ=(11, -6, 0) REJECT reason=transporter_cell_not_visible_or_explored

[AI Reach][TransportDelivery:3] Operational:miss budget=12

[AI Reach][TransportDelivery:3] Strategic:hit budget=120 action=(20, 6, 0) target=(20, 6, 0) score=-4 reason=cubic=4

[AI][Progressao2][Top] unit=3 intent=TransportDelivery from=(19, 2, 0) target=(20, 6, 0) best=(18, 2, 0) final=-1000 candidatos=62 skips origin=1 occupied=0 stop=0 allow=0 score=0
  #1 (18, 2, 0) final=-1000 tool=-1 next=4,0 move=1 road=False prog=0,0/0,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #2 (20, 2, 0) final=-1000 tool=-1 next=4,0 move=1 road=False prog=0,0/0,0 route=? progR=? line=1,0 threat=0,0 dpq=1,0 tactical=0
  #3 (17, 3, 0) final=-2000 tool=-2 next=4,0 move=2 road=False prog=0,0/0,0 route=? progR=? line=2,2 threat=0,0 dpq=1,0 tactical=0
  #4 (17, 4, 0) final=-2000 tool=-2 next=4,0 move=3 road=False prog=0,0/0,0 route=? progR=? line=2,4 threat=0,0 dpq=1,0 tactical=0
  #5 (16, 5, 0) final=-4000 tool=-4 next=4,0 move=4 road=False prog=0,0/0,0 route=? progR=? line=3,6 threat=0,0 dpq=1,0 tactical=0
  #6 (16, 6, 0) final=-4000 tool=-4 next=4,0 move=5 road=False prog=0,0/0,0 route=? progR=? line=3,9 threat=0,0 dpq=1,0 tactical=0
  #7 (17, 2, 0) final=-8000 tool=-8 next=4,0 move=2 road=False prog=-1,0/0,0 route=? progR=? line=2,0 threat=0,0 dpq=1,0 tactical=0
  #8 (17, 1, 0) final=-8000 tool=-8 next=4,0 move=2 road=False prog=-1,0/0,0 route=? progR=? line=2,2 threat=0,0 dpq=1,0 tactical=0
  #9 (16, 4, 0) final=-9000 tool=-9 next=4,0 move=4 road=False prog=-1,0/0,0 route=? progR=? line=3,4 threat=0,0 dpq=1,0 tactical=0
  #10 (16, 3, 0) final=-9000 tool=-9 next=4,0 move=3 road=False prog=-1,0/0,0 route=? progR=? line=3,2 threat=0,0 dpq=1,0 tactical=0
  #11 (15, 6, 0) final=-11000 tool=-11 next=4,0 move=6 road=False prog=-1,0/0,0 route=? progR=? line=4,9 threat=0,0 dpq=1,0 tactical=0
  #12 (15, 5, 0) final=-11000 tool=-11 next=4,0 move=5 road=False prog=-1,0/0,0 route=? progR=? line=4,6 threat=0,0 dpq=1,0 tactical=0

[AI VERMELHO][T6][Transporte] Terrestre 3 reach estrategico: alvo cubico (20, 6, 0) -> ancora (20, 6, 0); progride (19, 2, 0)->(18, 2, 0) (toolIntent=TransportDelivery tool=-1 next=4,0 moveCost=1 roadBonus=False prog=0,0/0,0 route=? progR=? line=1,0 dpq=1,0 threat=0,0 tactical=0 final=-1000).

[AI DecisionPreview] APC #3 vai apenas mover de (19,2) para (18,2). Decisão: AI Move 3 â†’ (18, 2, 0).

[AI Step] Linha azul: (C19,L2,0) -> (C18,L2,0).

[FrameSpike] frame=16485 duration=531,91ms state=Neutral substep=AwaitingAction selected=(none) boardRev=196 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=709,5MB managedDelta=+5,8MB gcDelta=[0,0,0] unityAlloc=977,4MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F11 AI Step: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI Step] Executando batch preparado.

[AI Shortcuts] F11 — AI Step

[RangeCache] MISS - reason: empty key | unit=APC_T1_U3 unitId=-125530 mp=6 fuel=57 rev=196

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=APC_T1_U3 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=APC_T1_U3

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=APC_T1_U3

moveu para 18,2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=APC_T1_U3 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,006ms memory=0,008ms geoOnly=0,002ms unitLoop=0,027ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][Perf][Incremental] unit=APC_T1_U3 total=7,591ms updateCache=6,718ms collect=6,554ms collected=True cells=28 render=0,239ms visibility=0,169ms intel=0,001ms detectionSfx=0,016ms persistence=0,001ms callbacks=0,375ms splitPresentation=False

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

