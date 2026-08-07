[SectorManager] rebuild reason=on-validate sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager] rebuild reason=on-enable sectors=1 bases=1 constructions=2 semSetor=0

[FogOfWar] Sorting layer validada em FogOfWar.

[BoardTopology] Runtime fallback 'Assets/Scenes/Mapas/Hot Seat 0 - Treino.unity::TileMap': 0 error(s), 0 warning(s).

[FoW][RoundZeroBake] restored=0/2

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=0 ms=0,000

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,457

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,765

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=3,621

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,445

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=1,232

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=2,760

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,155

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=6,508

[FoW][TurnStartCache] slot=0 activated=false

[FoW][TurnStartCache] slot=0 fallback=full

[FoW][Perf][Publish] slot=0 contributors=0,276ms knownCells=0,637ms memory=4,910ms geoOnly=0,012ms unitLoop=0,951ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=39,495ms | collect.total=12,791ms collect.avg/unit=12,791ms collect.units=1 collect.cells=4 | constructionVision=1,981ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,142ms publish=9,160ms unitVisibility=2,116ms intel=2,025ms render=5,006ms callbacks=0,449ms store=1,645ms | boardCells=153 unitsScanned=2 unaccounted=4,180ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=1,702 structure.ms=1,551 lerp.ms=1,927 lerp.cells=21 | collect.total=12,791ms outsideLos=12,791ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=12,791 cells=4

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=49,372

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=4,086

[TurnPerf] etapa=ApplyActiveTeam.Total ms=67,207

[AI] Start — matchController=True replayManager=True turnStateManager=True

[AI] Pausa de debug solicitada. Aguardando ponto seguro.

[AI] Start on Pause ativo - estado inicial equivalente ao F10.

[FrameSpike] frame=1 duration=5250,88ms state=Neutral substep=AwaitingAction selected=(none) boardRev=0 replay=False aiTurn=False aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=644,3MB managedDelta=0,0MB gcDelta=[345,345,345] unityAlloc=823,4MB

[TurnState] substep=-1 -> AwaitingAction | state=Neutral

[SectorManager] rebuild reason=on-enable sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=on-enable revision=0 sectors=1 bases=1 total=0,7ms

[SectorManager][Perf][Steps] contexts=0,4ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=2 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,2ms

[FrameSpike] frame=2 duration=1679,23ms state=Neutral substep=AwaitingAction selected=(none) boardRev=0 replay=False aiTurn=False aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=651,0MB managedDelta=+6,8MB gcDelta=[0,0,0] unityAlloc=845,7MB

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=0 total=1,075ms

[FoW][Warmup] host=0 slots=1 sources=1 total=1686,080ms

[FoW][Warmup][Steps] activate=0,0ms work=2,5ms store=0,0ms restore=0,1ms | cpu=2,7ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=1683,4ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,720

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,146

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=3,420

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,524

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,000

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,004

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,669

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,852

[FoW][Perf][Publish] slot=1 contributors=0,006ms knownCells=0,004ms memory=0,009ms geoOnly=0,001ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=17

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=1,220ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,009ms geoOnly=0,001ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,435ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=2,261

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,184

[TurnPerf] etapa=ApplyActiveTeam.Total ms=7,554

[TurnPerf] etapa=AdvanceTurn ms=9,766

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=758,511

[FrameSpike] frame=816 duration=667,77ms state=Neutral substep=AwaitingAction selected=(none) boardRev=0 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=677,3MB managedDelta=+0,3MB gcDelta=[0,0,0] unityAlloc=903,8MB

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Shortcuts] F12 AI Resume: cursor Neutral->Neutral selection=nenhuma antes de liberar a IA.

[AI] Pausa de debug encerrada. Retomando IA.

[AI Shortcuts] F12 — AI Resume

[AI ][T0] Fase0 concluída.

[AI Perf] Stage0 (wait): 545ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,002ms memory=0,007ms geoOnly=0,001ms unitLoop=0,040ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=17

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,243ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,792ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T1][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=2ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,002ms memory=0,007ms geoOnly=0,001ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=17

[FoW][Perf] total=3,006ms | collect.total=2,722ms collect.avg/unit=2,722ms collect.units=1 collect.cells=16 | constructionVision=0,007ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,078ms publish=0,160ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,011ms | boardCells=153 unitsScanned=2 unaccounted=0,024ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=16 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=51 construction.ms=0,067 structure.ms=0,061 lerp.ms=0,896 lerp.cells=21 | collect.total=2,722ms outsideLos=2,722ms

[FoW][Coverage] geographic=17 sensor=16 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(7,0) ms=2,722 cells=16

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,013ms memory=0,005ms geoOnly=0,001ms unitLoop=0,028ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,614ms | collect.total=2,587ms collect.avg/unit=2,587ms collect.units=1 collect.cells=4 | constructionVision=0,023ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,063ms publish=0,128ms unitVisibility=0,029ms intel=0,001ms render=0,598ms callbacks=0,162ms store=0,009ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,066 structure.ms=0,059 lerp.ms=0,905 lerp.cells=21 | collect.total=2,587ms outsideLos=2,587ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,587 cells=4

[AI Perf] CommitAIWorldHeavy: 18ms

[AI VERMELHO][T1] Turno 1 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 0

[AI VERMELHO][T1][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 25ms

[AI Perf] TacticalAnalyzer.Rebuild: 15ms

[AI VERMELHO][T1] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T1] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T1] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T1] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 539ms

[AI Perf] PRE-Stage2 acumulado: 1848ms

[AI VERMELHO][T1] Fase2 — iniciando ações.

[AI VERMELHO][T1] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (7, 0, 0) target=null


[AI Perf][InitiativeSetup] total=15,9ms available=1,7ms snapshot=1,7ms repair=1,1ms groups=8,7ms facts=1,9ms sort=0,0ms log=0,7ms

[AI VERMELHO][T1][FilaCarona] #1 entra na fila no turno 1 — fora das bandas (score=1000).

[AI VERMELHO][T1][Capturador] 1 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T1][Capturador] 1 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  nenhum transporte aliado <=8h


[AI VERMELHO][T1][SemPlano] 1 âncora = capturável (0, 0, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T1][Rogue] 1 marcha para âncora (0, 0, 0) via (4, 0, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=70 rev=0

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 4,0

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,007ms knownCells=0,005ms memory=0,010ms geoOnly=0,002ms unitLoop=0,037ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,021ms memory=0,005ms geoOnly=0,001ms unitLoop=0,031ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=8,171ms updateCache=6,332ms collect=6,217ms collected=True cells=37 render=0,001ms visibility=0,182ms intel=0,006ms detectionSfx=0,192ms persistence=0,005ms callbacks=0,161ms splitPresentation=True

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T1][Missao] 1 Capture -> (0, 0, 0) predio=#2 (adquirida).

[AI Commit Light][T1][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T1][Red] Soldado#1 action=move decision=136ms execution=428ms snapshot=0ms delay=503ms total=1068ms stages=validPaths:30,6ms/6,melhorCaptura:15,9ms/3,queroCarona:13,2ms/1,routeDistance:11,6ms/18,turnChainedCostMap:6,7ms/4,ownMovementComponent:4,5ms/2,opportunistic:1,7ms/2,aggressive:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:3,CellsVisited:437,MelhorCapturaCalls:3,MelhorCapturaCandidates:6,MelhorCapturaOutOfBandSkips:4,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:2,MobilityComponentBuilds:1,MobilityComponentHits:1,MovementCacheHits:5,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:395,MovementQueryConfirmedOccupancyUses:395,MovementWavesBuilt:1,OwnMovementComponentBuilds:2,PathStatesExpanded:29,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentBuilds:1,ReachableCellsProduced:420,TurnChainedCellsExpanded:408,ValidPathCacheHits:5,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T1][Red] decisions=1 completed=1
  decision=136ms execution=428ms snapshot=0ms delay=503ms measuredTotal=1068ms
  boardQueries stages=validPaths:30,6ms/6,melhorCaptura:15,9ms/3,queroCarona:13,2ms/1,routeDistance:11,6ms/18,turnChainedCostMap:6,7ms/4,ownMovementComponent:4,5ms/2,opportunistic:1,7ms/2,aggressive:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:3,CellsVisited:437,MelhorCapturaCalls:3,MelhorCapturaCandidates:6,MelhorCapturaOutOfBandSkips:4,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:2,MobilityComponentBuilds:1,MobilityComponentHits:1,MovementCacheHits:5,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:395,MovementQueryConfirmedOccupancyUses:395,MovementWavesBuilt:1,OwnMovementComponentBuilds:2,PathStatesExpanded:29,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentBuilds:1,ReachableCellsProduced:420,TurnChainedCellsExpanded:408,ValidPathCacheHits:5,ValidPathWaves:1
  #1 Soldado#1 action=move total=1068ms decision=136 execution=428 snapshot=0 delay=503


[AI VERMELHO][T1] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 1140ms

[AI VERMELHO][T1] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,007ms geoOnly=0,002ms unitLoop=0,031ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,210ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,562ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,002ms memory=0,007ms geoOnly=0,002ms unitLoop=0,031ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,448ms | collect.total=6,171ms collect.avg/unit=6,171ms collect.units=1 collect.cells=37 | constructionVision=0,006ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,056ms publish=0,165ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,030ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,077 structure.ms=0,069 lerp.ms=2,075 lerp.cells=54 | collect.total=6,171ms outsideLos=6,171ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(4,0) ms=6,171 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,013ms memory=0,005ms geoOnly=0,001ms unitLoop=0,028ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,728ms | collect.total=2,676ms collect.avg/unit=2,676ms collect.units=1 collect.cells=4 | constructionVision=0,023ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,062ms publish=0,132ms unitVisibility=0,031ms intel=0,001ms render=0,614ms callbacks=0,165ms store=0,010ms | boardCells=153 unitsScanned=2 unaccounted=0,014ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,064 structure.ms=0,059 lerp.ms=0,890 lerp.cells=21 | collect.total=2,676ms outsideLos=2,676ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,676 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=3 sectors=1 bases=1 total=0,3ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=18 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 18ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 5ms

[AI VERMELHO][T1][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Commit Heavy][T1][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=25ms

[AI Shopping Roles][T1][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T1][Red] fila unica budget=0 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T1][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T1][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=0 — caixa preservado

[AI Shopping Roles][T1][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T1] Fase3 concluída.

[AI Perf] Stage3 (shopping): 58ms

[AI VERMELHO][T1] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 24ms

[AI Perf] TURNO TOTAL (Red): 3097ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,712

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,144

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=3,899

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,145

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,313

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,029

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,782

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=1,340

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,006ms geoOnly=0,001ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,420ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,775

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,011

[TurnPerf] etapa=ApplyActiveTeam.Total ms=6,473

[TurnPerf] etapa=AdvanceTurn ms=6,755

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=111,435

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,079ms

[FoW][Warmup] host=0 slots=1 sources=0 total=39,224ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=39,2ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,696

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,142

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,927

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,116

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,003

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,663

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,842

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,007ms geoOnly=0,002ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,196ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,018ms memory=0,005ms geoOnly=0,001ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,385ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,052

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,006

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,232

[TurnPerf] etapa=AdvanceTurn ms=4,382

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=105,893

[AI VERMELHO][T1] Fase0 concluída.

[AI Perf] Stage0 (wait): 530ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,005ms knownCells=0,003ms memory=0,007ms geoOnly=0,002ms unitLoop=0,031ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,211ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,569ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T2][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,002ms memory=0,007ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,374ms | collect.total=6,086ms collect.avg/unit=6,086ms collect.units=1 collect.cells=37 | constructionVision=0,007ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,059ms publish=0,170ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,016ms | boardCells=153 unitsScanned=2 unaccounted=0,034ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,080 structure.ms=0,073 lerp.ms=2,069 lerp.cells=54 | collect.total=6,086ms outsideLos=6,086ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(4,0) ms=6,086 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,005ms geoOnly=0,001ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,589ms | collect.total=2,584ms collect.avg/unit=2,584ms collect.units=1 collect.cells=4 | constructionVision=0,021ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,065ms publish=0,114ms unitVisibility=0,030ms intel=0,001ms render=0,593ms callbacks=0,162ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,012ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,058 lerp.ms=0,891 lerp.cells=21 | collect.total=2,584ms outsideLos=2,584ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,584 cells=4

[AI Perf] CommitAIWorldHeavy: 19ms

[AI VERMELHO][T2] Turno 2 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 0

[AI VERMELHO][T2][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T2] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T2] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T2] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T2] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 520ms

[AI Perf] PRE-Stage2 acumulado: 1113ms

[AI VERMELHO][T2] Fase2 — iniciando ações.

[AI VERMELHO][T2] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (4, 0, 0) target=null


[AI Perf][InitiativeSetup] total=0,4ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T2][FilaCarona] #1 sai da fila apos 1 turno(s) — nao quer mais carona.

[AI VERMELHO][T2][Capturador] 1 QueroCarona=NAO contexto=RogueOuRebelde setor=None origemAlvo=reserva emergencia=False envelope=Operational custo=4 alvo=(0, 0, 0) motivo=Infantaria alcança alvo reservado Cidade@(0, 0, 0) (oportunidade) no Operational: custo=4 no turno 2 de 2. Recusa carona.

[AI VERMELHO][T2][SemPlano] 1 âncora = capturável (0, 0, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T2][Rogue] 1 marcha para âncora (0, 0, 0) via (1, 0, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=67 rev=3

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 1,0

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,007ms geoOnly=0,002ms unitLoop=0,027ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][Perf][Publish] slot=0 contributors=0,001ms knownCells=0,017ms memory=0,004ms geoOnly=0,001ms unitLoop=0,022ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=6,793ms updateCache=6,017ms collect=5,983ms collected=True cells=31 render=0,000ms visibility=0,131ms intel=0,002ms detectionSfx=0,020ms persistence=0,001ms callbacks=0,157ms splitPresentation=True

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T2][Missao] 1 Capture -> (0, 0, 0) predio=#2 (mantida).

[AI Commit Light][T2][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T2][Red] Soldado#1 action=move decision=25ms execution=353ms snapshot=0ms delay=509ms total=887ms stages=routeDistance:9,5ms/39,validPaths:3,2ms/5,queroCarona:3,0ms/1,melhorCaptura:2,7ms/1,turnChainedCostMap:2,7ms/2,opportunistic:1,8ms/1,aggressive:0,0ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:239,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaOutOfBandSkips:1,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:75,MovementQueryConfirmedOccupancyUses:75,MovementWavesBuilt:1,PathStatesExpanded:63,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:213,TurnChainedCellsExpanded:176,ValidPathCacheHits:4,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T2][Red] decisions=1 completed=1
  decision=25ms execution=353ms snapshot=0ms delay=509ms measuredTotal=887ms
  boardQueries stages=routeDistance:9,5ms/39,validPaths:3,2ms/5,queroCarona:3,0ms/1,melhorCaptura:2,7ms/1,turnChainedCostMap:2,7ms/2,opportunistic:1,8ms/1,aggressive:0,0ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:239,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaOutOfBandSkips:1,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:75,MovementQueryConfirmedOccupancyUses:75,MovementWavesBuilt:1,PathStatesExpanded:63,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:213,TurnChainedCellsExpanded:176,ValidPathCacheHits:4,ValidPathWaves:1
  #1 Soldado#1 action=move total=887ms decision=25 execution=353 snapshot=0 delay=509


[AI VERMELHO][T2] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 932ms

[AI VERMELHO][T2] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,390ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,980ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,002ms memory=0,009ms geoOnly=0,002ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][Perf] total=7,224ms | collect.total=6,794ms collect.avg/unit=6,794ms collect.units=1 collect.cells=31 | constructionVision=0,007ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,060ms publish=0,196ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,081ms | boardCells=153 unitsScanned=2 unaccounted=0,082ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=31 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=121 construction.ms=0,082 structure.ms=0,125 lerp.ms=2,292 lerp.cells=54 | collect.total=6,794ms outsideLos=6,794ms

[FoW][Coverage] geographic=32 sensor=31 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(1,0) ms=6,794 cells=31

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,075ms memory=0,007ms geoOnly=0,001ms unitLoop=0,035ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=4,607ms | collect.total=2,943ms collect.avg/unit=2,943ms collect.units=1 collect.cells=4 | constructionVision=0,064ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,069ms publish=0,269ms unitVisibility=0,071ms intel=0,002ms render=0,770ms callbacks=0,352ms store=0,011ms | boardCells=153 unitsScanned=2 unaccounted=0,056ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,067 structure.ms=0,059 lerp.ms=1,051 lerp.cells=21 | collect.total=2,943ms outsideLos=2,943ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,943 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=6 sectors=1 bases=1 total=0,4ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,1ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=39 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 25ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI VERMELHO][T2][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Commit Heavy][T2][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=27ms

[AI Shopping Roles][T2][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T2][Red] fila unica budget=0 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T2][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T2][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=0 — caixa preservado

[AI Shopping Roles][T2][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T2] Fase3 concluída.

[AI Perf] Stage3 (shopping): 29ms

[AI VERMELHO][T2] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 30ms

[AI Perf] TURNO TOTAL (Red): 2165ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,716

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,143

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,917

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,123

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,024

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,760

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,966

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,006ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,375ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,719

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,012

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,021

[TurnPerf] etapa=AdvanceTurn ms=4,184

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=105,498

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,077ms

[FoW][Warmup] host=0 slots=1 sources=0 total=37,128ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=37,1ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,718

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,145

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,989

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,134

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,004

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,694

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,883

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,002ms memory=0,007ms geoOnly=0,002ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,201ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,005ms geoOnly=0,001ms unitLoop=0,024ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,368ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,040

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,002

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,343

[TurnPerf] etapa=AdvanceTurn ms=4,506

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=105,113

[AI VERMELHO][T2] Fase0 concluída.

[AI Perf] Stage0 (wait): 528ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,031ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,221ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,587ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T3][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,007ms geoOnly=0,002ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=32

[FoW][Perf] total=6,259ms | collect.total=5,986ms collect.avg/unit=5,986ms collect.units=1 collect.cells=31 | constructionVision=0,005ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,056ms publish=0,164ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,016ms | boardCells=153 unitsScanned=2 unaccounted=0,028ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=31 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=121 construction.ms=0,076 structure.ms=0,121 lerp.ms=1,950 lerp.cells=54 | collect.total=5,986ms outsideLos=5,986ms

[FoW][Coverage] geographic=32 sensor=31 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(1,0) ms=5,986 cells=31

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,005ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,645ms | collect.total=2,641ms collect.avg/unit=2,641ms collect.units=1 collect.cells=4 | constructionVision=0,021ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,061ms publish=0,118ms unitVisibility=0,030ms intel=0,001ms render=0,589ms callbacks=0,162ms store=0,010ms | boardCells=153 unitsScanned=2 unaccounted=0,012ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,064 structure.ms=0,059 lerp.ms=0,884 lerp.cells=21 | collect.total=2,641ms outsideLos=2,641ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,641 cells=4

[AI Perf] CommitAIWorldHeavy: 18ms

[AI VERMELHO][T3] Turno 3 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 0

[AI VERMELHO][T3][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T3][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T3][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 5ms

[AI VERMELHO][T3] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T3] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T3] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T3] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 519ms

[AI Perf] PRE-Stage2 acumulado: 1110ms

[AI VERMELHO][T3] Fase2 — iniciando ações.

[AI VERMELHO][T3] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (1, 0, 0) target=null


[AI Perf][InitiativeSetup] total=0,4ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T3][Oportunista] 1 captura local perto Alpha @ (0, 0, 0) antes de embarcar score=2000 rally=False

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=64 rev=6

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para 0,0

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

[Captura] Soldado_T1_U1 causou 10 de captura em Cidade (20 -> 10).

[FSM] Estado: CapturandoExecuting -> Neutral

[TurnState] transition=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,005ms knownCells=0,003ms memory=0,011ms geoOnly=0,002ms unitLoop=0,034ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,018ms memory=0,004ms geoOnly=0,001ms unitLoop=0,022ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=6,990ms updateCache=6,105ms collect=6,065ms collected=True cells=37 render=0,000ms visibility=0,189ms intel=0,002ms detectionSfx=0,021ms persistence=0,002ms callbacks=0,164ms splitPresentation=True

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,251ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,648ms

[AI Commit Light][T3][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=ReusedAndReconciled removed=0

[FoW][Perf][Publish] slot=1 contributors=0,005ms knownCells=0,002ms memory=0,008ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,272ms | collect.total=5,992ms collect.avg/unit=5,992ms collect.units=1 collect.cells=37 | constructionVision=0,006ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,057ms publish=0,163ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,016ms | boardCells=153 unitsScanned=2 unaccounted=0,034ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,081 structure.ms=0,072 lerp.ms=1,953 lerp.cells=54 | collect.total=5,992ms outsideLos=5,992ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(0,0) ms=5,992 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,015ms memory=0,006ms geoOnly=0,001ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,708ms | collect.total=2,643ms collect.avg/unit=2,643ms collect.units=1 collect.cells=4 | constructionVision=0,024ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,064ms publish=0,149ms unitVisibility=0,030ms intel=0,002ms render=0,605ms callbacks=0,166ms store=0,010ms | boardCells=153 unitsScanned=2 unaccounted=0,015ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,060 lerp.ms=0,898 lerp.cells=21 | collect.total=2,643ms outsideLos=2,643ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,643 cells=4

[AI Perf][Unit][T3][Red] Soldado#1 action=move decision=7ms execution=921ms snapshot=0ms delay=501ms total=1430ms stages=validPaths:1,5ms/1 metrics=CellsVisited:63,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:1,MovementQueryConfirmedOccupancyUses:1,MovementWavesBuilt:1,PathStatesExpanded:63,ReachableCellsProduced:37,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T3][Red] decisions=1 completed=1
  decision=7ms execution=921ms snapshot=0ms delay=501ms measuredTotal=1430ms
  boardQueries stages=validPaths:1,5ms/1 metrics=CellsVisited:63,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:1,MovementQueryConfirmedOccupancyUses:1,MovementWavesBuilt:1,PathStatesExpanded:63,ReachableCellsProduced:37,ValidPathWaves:1
  #1 Soldado#1 action=move total=1430ms decision=7 execution=921 snapshot=0 delay=501


[AI VERMELHO][T3] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 1466ms

[AI VERMELHO][T3] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,216ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,737ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,002ms memory=0,009ms geoOnly=0,002ms unitLoop=0,033ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,387ms | collect.total=6,078ms collect.avg/unit=6,078ms collect.units=1 collect.cells=37 | constructionVision=0,008ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,058ms publish=0,188ms unitVisibility=0,000ms intel=0,004ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,036ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,082 structure.ms=0,072 lerp.ms=1,969 lerp.cells=54 | collect.total=6,078ms outsideLos=6,078ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(0,0) ms=6,078 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,013ms memory=0,006ms geoOnly=0,001ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,705ms | collect.total=2,609ms collect.avg/unit=2,609ms collect.units=1 collect.cells=4 | constructionVision=0,024ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,071ms publish=0,137ms unitVisibility=0,031ms intel=0,002ms render=0,632ms callbacks=0,175ms store=0,012ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,059 lerp.ms=0,893 lerp.cells=21 | collect.total=2,609ms outsideLos=2,609ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,609 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=7 sectors=1 bases=1 total=0,3ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=39 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 18ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI VERMELHO][T3][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T3][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T3][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T3][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=21ms

[AI Shopping Roles][T3][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T3][Red] fila unica budget=0 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T3][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T3][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=0 — caixa preservado

[AI Shopping Roles][T3][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T3] Fase3 concluída.

[AI Perf] Stage3 (shopping): 22ms

[AI VERMELHO][T3] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 19ms

[AI Perf] TURNO TOTAL (Red): 2644ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,696

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,147

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,897

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,121

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,025

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,746

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,951

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,005ms geoOnly=0,001ms unitLoop=0,027ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,375ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,718

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,010

[TurnPerf] etapa=ApplyActiveTeam.Total ms=3,982

[TurnPerf] etapa=AdvanceTurn ms=4,141

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=106,867

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,077ms

[FoW][Warmup] host=0 slots=1 sources=0 total=35,627ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=35,6ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,700

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,141

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,945

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,111

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,003

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,716

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,891

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,023ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,187ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,004ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,360ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,993

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,003

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,222

[TurnPerf] etapa=AdvanceTurn ms=4,378

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=108,293

[AI VERMELHO][T3] Fase0 concluída.

[AI Perf] Stage0 (wait): 531ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,218ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,591ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T4][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,003ms memory=0,008ms geoOnly=0,002ms unitLoop=0,033ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,414ms | collect.total=6,119ms collect.avg/unit=6,119ms collect.units=1 collect.cells=37 | constructionVision=0,011ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,053ms publish=0,175ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,038ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,081 structure.ms=0,073 lerp.ms=1,964 lerp.cells=54 | collect.total=6,119ms outsideLos=6,119ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(0,0) ms=6,119 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,015ms memory=0,005ms geoOnly=0,001ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,634ms | collect.total=2,615ms collect.avg/unit=2,615ms collect.units=1 collect.cells=4 | constructionVision=0,021ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,062ms publish=0,114ms unitVisibility=0,029ms intel=0,001ms render=0,607ms callbacks=0,166ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,012ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,063 structure.ms=0,059 lerp.ms=0,883 lerp.cells=21 | collect.total=2,615ms outsideLos=2,615ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,615 cells=4

[AI Perf] CommitAIWorldHeavy: 18ms

[AI VERMELHO][T4] Turno 4 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 0

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

[AI Perf] Stage1 (command): 521ms

[AI Perf] PRE-Stage2 acumulado: 1112ms

[AI VERMELHO][T4] Fase2 — iniciando ações.

[AI VERMELHO][T4] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (0, 0, 0) target=null


[AI Perf][InitiativeSetup] total=0,4ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T4][Oportunista] 1 captura célula atual (0, 0, 0) antes de embarcar

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=63 rev=7

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Capturando

[TurnState] transition=MoveuParado -> Capturando | reason=HandleCaptureActionRequested | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuParado > Capturando

[FSM][Enter] MoveuParado -> Capturando | reason=HandleCaptureActionRequested

[FSM] Estado: Capturando -> CapturandoExecuting

[TurnState] transition=Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuParado > Capturando > CapturandoExecuting

[FSM][Enter] Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin

[Captura] Soldado_T1_U1 causou 10 de captura em Cidade (10 -> 0).

[Captura] Construcao capturada por vermelho. Capture resetado para 20/20.

[FSM] Estado: CapturandoExecuting -> Neutral

[TurnState] transition=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=1 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,014ms memory=0,008ms geoOnly=0,002ms unitLoop=0,032ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,018ms memory=0,004ms geoOnly=0,001ms unitLoop=0,022ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=1 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=1 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=6,939ms updateCache=6,098ms collect=6,061ms collected=True cells=37 render=0,000ms visibility=0,176ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,154ms splitPresentation=True

[AI Perf][Unit][T4][Red] Soldado#1 action=wait decision=1ms execution=837ms snapshot=0ms delay=505ms total=1342ms stages=- metrics=-

[AI Perf][Phase2 Breakdown][T4][Red] decisions=1 completed=1
  decision=1ms execution=837ms snapshot=0ms delay=505ms measuredTotal=1342ms
  boardQueries stages=- metrics=-
  #1 Soldado#1 action=wait total=1342ms decision=1 execution=837 snapshot=0 delay=505


[AI VERMELHO][T4] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 1378ms

[AI VERMELHO][T4] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,009ms memory=0,007ms geoOnly=0,002ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=1 total=0,271ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,671ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,008ms memory=0,007ms geoOnly=0,002ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,244ms | collect.total=5,954ms collect.avg/unit=5,954ms collect.units=1 collect.cells=37 | constructionVision=0,016ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,054ms publish=0,172ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,029ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,079 structure.ms=0,071 lerp.ms=1,953 lerp.cells=54 | collect.total=5,954ms outsideLos=5,954ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(0,0) ms=5,954 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,015ms memory=0,005ms geoOnly=0,001ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,557ms | collect.total=2,556ms collect.avg/unit=2,556ms collect.units=1 collect.cells=4 | constructionVision=0,020ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,065ms publish=0,117ms unitVisibility=0,029ms intel=0,001ms render=0,591ms callbacks=0,157ms store=0,009ms | boardCells=153 unitsScanned=2 unaccounted=0,012ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,063 structure.ms=0,058 lerp.ms=0,890 lerp.cells=21 | collect.total=2,556ms outsideLos=2,556ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,556 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=8 sectors=1 bases=1 total=0,5ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=39 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,1ms

[AI Commit Heavy] SectorRebuild: 18ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI VERMELHO][T4][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T4][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T4][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T4][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=20ms

[AI Shopping Roles][T4][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T4][Red] fila unica budget=0 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T4][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T4][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=0 — caixa preservado

[AI Shopping Roles][T4][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T4] Fase3 concluída.

[AI Perf] Stage3 (shopping): 22ms

[AI VERMELHO][T4] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 19ms

[AI Perf] TURNO TOTAL (Red): 2558ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,704

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,137

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,916

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,122

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,024

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,750

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,959

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,006ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,377ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,715

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,010

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,012

[TurnPerf] etapa=AdvanceTurn ms=4,176

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=106,320

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,094ms

[FoW][Warmup] host=0 slots=1 sources=0 total=39,772ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=39,8ms

[FrameSpike] frame=3003 duration=17070,93ms state=Neutral substep=AwaitingAction selected=(none) boardRev=8 replay=False aiTurn=False aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=698,9MB managedDelta=+0,5MB gcDelta=[0,0,0] unityAlloc=901,5MB

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,698

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,139

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,022

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,118

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,005

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,711

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,901

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,009ms memory=0,007ms geoOnly=0,002ms unitLoop=0,023ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,215ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,004ms geoOnly=0,001ms unitLoop=0,024ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,362ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,032

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,002

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,377

[TurnPerf] etapa=AdvanceTurn ms=4,527

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=105,738

[AI VERMELHO][T4] Fase0 concluída.

[AI Perf] Stage0 (wait): 531ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,010ms memory=0,007ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,250ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,618ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T5][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,007ms memory=0,010ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=38

[FoW][Perf] total=6,309ms | collect.total=6,021ms collect.avg/unit=6,021ms collect.units=1 collect.cells=37 | constructionVision=0,014ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,053ms publish=0,171ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,030ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,079 structure.ms=0,071 lerp.ms=1,957 lerp.cells=54 | collect.total=6,021ms outsideLos=6,021ms

[FoW][Coverage] geographic=38 sensor=37 geographicOnly=1 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(0,0) ms=6,021 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,006ms geoOnly=0,001ms unitLoop=0,028ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,654ms | collect.total=2,623ms collect.avg/unit=2,623ms collect.units=1 collect.cells=4 | constructionVision=0,022ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,065ms publish=0,128ms unitVisibility=0,031ms intel=0,002ms render=0,605ms callbacks=0,159ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,058 lerp.ms=0,887 lerp.cells=21 | collect.total=2,623ms outsideLos=2,623ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,623 cells=4

[AI Perf] CommitAIWorldHeavy: 19ms

[AI VERMELHO][T5] Turno 5 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 1000

[AI VERMELHO][T5][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T5][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T5][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T5] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T5] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[FSM] Estado: Neutral -> CommandService

[TurnState] transition=Neutral -> CommandService | reason=TryOpenCommandServiceFromMenu | selected=(none) | stack=Neutral > CommandService

[FSM][Enter] Neutral -> CommandService | reason=TryOpenCommandServiceFromMenu

[TurnState] state=CommandService | step=HandleConfirm | selected=(none)

[TurnState] state=CommandService | step=HandleConfirmWhileCommandService | selected=(none)

[FSM] Estado: CommandService -> CommandServiceExecuting

[TurnState] transition=CommandService -> CommandServiceExecuting | reason=TryConfirmPendingCommandServiceOrder | selected=(none) | stack=Neutral > CommandService > CommandServiceExecuting

[FSM][Enter] CommandService -> CommandServiceExecuting | reason=TryConfirmPendingCommandServiceOrder

[AI VERMELHO][T5] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[FSM] Estado: CommandServiceExecuting -> Neutral

[TurnState] transition=CommandServiceExecuting -> Neutral | reason=ExecuteCommandServiceOrderSequence: completed | selected=(none) | stack=Neutral

[FSM][Reset] previous=CommandServiceExecuting -> Neutral | reason=ExecuteCommandServiceOrderSequence: completed

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ExecuteCommandServiceOrderSequence: cleanup | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ExecuteCommandServiceOrderSequence: cleanup

[AI VERMELHO][T5] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 1477ms

[AI Perf] PRE-Stage2 acumulado: 2076ms

[AI VERMELHO][T5] Fase2 — iniciando ações.

[AI VERMELHO][T5] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (0, 0, 0) target=null


[AI Perf][InitiativeSetup] total=0,5ms available=0,1ms snapshot=0,3ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T5][FilaCarona] #1 entra na fila no turno 5 — fora das bandas (score=1000).

[AI VERMELHO][T5][Capturador] 1 QueroCarona=SIM contexto=RogueOuRebelde setor=None origemAlvo=servico emergencia=False envelope=BeyondOperational custo=- alvo=(0, 0, 0) motivo=Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational: aceita carona.

[AI VERMELHO][T5][Capturador] 1 embarque scan: assigned=rogue reason=sem embarque valido adjacentOptions=0 best=- p=-
  nenhum transporte aliado <=8h


[AI VERMELHO][T5][SemPlano] 1 âncora = capturável (-7, 0, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T5][Rogue] 1 marcha para âncora (-7, 0, 0) via (-3, 0, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=70 rev=8

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para -3,0

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,013ms memory=0,007ms geoOnly=0,003ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,018ms memory=0,004ms geoOnly=0,001ms unitLoop=0,022ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=5,957ms updateCache=5,131ms collect=5,093ms collected=True cells=37 render=0,000ms visibility=0,165ms intel=0,002ms detectionSfx=0,021ms persistence=0,001ms callbacks=0,159ms splitPresentation=True

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T5][Missao] 1 Capture -> (-7, 0, 0) predio=#1 (adquirida).

[AI Commit Light][T5][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T5][Red] Soldado#1 action=move decision=84ms execution=374ms snapshot=0ms delay=505ms total=963ms stages=routeDistance:50,9ms/39,turnChainedCostMap:5,6ms/3,validPaths:3,7ms/6,opportunistic:3,6ms/2,melhorCaptura:3,4ms/3,queroCarona:3,2ms/1,ownMovementComponent:2,1ms/1,aggressive:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:3,CellsVisited:470,MelhorCapturaCalls:3,MelhorCapturaCandidates:6,MelhorCapturaOutOfBandSkips:2,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:1,MobilityComponentBuilds:1,MovementCacheHits:5,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:940,MovementQueryConfirmedOccupancyUses:940,MovementWavesBuilt:1,OwnMovementComponentBuilds:1,PathStatesExpanded:63,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:442,TurnChainedCellsExpanded:407,ValidPathCacheHits:5,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T5][Red] decisions=1 completed=1
  decision=84ms execution=374ms snapshot=0ms delay=505ms measuredTotal=963ms
  boardQueries stages=routeDistance:50,9ms/39,turnChainedCostMap:5,6ms/3,validPaths:3,7ms/6,opportunistic:3,6ms/2,melhorCaptura:3,4ms/3,queroCarona:3,2ms/1,ownMovementComponent:2,1ms/1,aggressive:0,0ms/1 metrics=CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:3,CellsVisited:470,MelhorCapturaCalls:3,MelhorCapturaCandidates:6,MelhorCapturaOutOfBandSkips:2,MelhorCapturaReachBuilds:1,MelhorCapturaReachReuses:2,MelhorCapturaTargets:1,MobilityComponentBuilds:1,MovementCacheHits:5,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:940,MovementQueryConfirmedOccupancyUses:940,MovementWavesBuilt:1,OwnMovementComponentBuilds:1,PathStatesExpanded:63,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:442,TurnChainedCellsExpanded:407,ValidPathCacheHits:5,ValidPathWaves:1
  #1 Soldado#1 action=move total=963ms decision=84 execution=374 snapshot=0 delay=505


[AI VERMELHO][T5] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 998ms

[AI VERMELHO][T5] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,005ms knownCells=0,009ms memory=0,007ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,230ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,593ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,008ms memory=0,007ms geoOnly=0,002ms unitLoop=0,029ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][Perf] total=6,321ms | collect.total=6,031ms collect.avg/unit=6,031ms collect.units=1 collect.cells=37 | constructionVision=0,014ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,055ms publish=0,168ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,019ms | boardCells=153 unitsScanned=2 unaccounted=0,031ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,076 structure.ms=0,122 lerp.ms=1,955 lerp.cells=54 | collect.total=6,031ms outsideLos=6,031ms

[FoW][Coverage] geographic=41 sensor=37 geographicOnly=4 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(-3,0) ms=6,031 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,006ms geoOnly=0,001ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,715ms | collect.total=2,656ms collect.avg/unit=2,656ms collect.units=1 collect.cells=4 | constructionVision=0,023ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,064ms publish=0,135ms unitVisibility=0,030ms intel=0,001ms render=0,618ms callbacks=0,166ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,058 lerp.ms=0,886 lerp.cells=21 | collect.total=2,656ms outsideLos=2,656ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,656 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=11 sectors=1 bases=1 total=0,3ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=75 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 18ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI VERMELHO][T5][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T5][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T5][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T5][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=20ms

[AI Shopping Roles][T5][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T5][Red] fila unica budget=993 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T5][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T5][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=993 — caixa preservado

[AI Shopping Roles][T5][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T5] Fase3 concluída.

[AI Perf] Stage3 (shopping): 21ms

[AI VERMELHO][T5] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 20ms

[AI Perf] TURNO TOTAL (Red): 3140ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,715

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,151

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=1,947

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,124

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,027

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,756

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,965

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,005ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,378ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,722

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,010

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,063

[TurnPerf] etapa=AdvanceTurn ms=4,229

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=105,008

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,095ms

[FoW][Warmup] host=0 slots=1 sources=0 total=45,663ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=45,7ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,728

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,156

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,094

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,119

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,005

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,688

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,877

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,010ms memory=0,007ms geoOnly=0,002ms unitLoop=0,026ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,229ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,004ms geoOnly=0,001ms unitLoop=0,025ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,381ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,083

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,002

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,483

[TurnPerf] etapa=AdvanceTurn ms=4,649

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=108,703

[AI VERMELHO][T5] Fase0 concluída.

[AI Perf] Stage0 (wait): 530ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,010ms memory=0,007ms geoOnly=0,002ms unitLoop=0,030ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,236ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,599ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T6][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,005ms knownCells=0,008ms memory=0,007ms geoOnly=0,002ms unitLoop=0,028ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=41

[FoW][Perf] total=6,267ms | collect.total=5,976ms collect.avg/unit=5,976ms collect.units=1 collect.cells=37 | constructionVision=0,016ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,054ms publish=0,170ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,019ms | boardCells=153 unitsScanned=2 unaccounted=0,029ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=37 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=126 construction.ms=0,076 structure.ms=0,124 lerp.ms=1,963 lerp.cells=54 | collect.total=5,976ms outsideLos=5,976ms

[FoW][Coverage] geographic=41 sensor=37 geographicOnly=4 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(-3,0) ms=5,976 cells=37

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,015ms memory=0,005ms geoOnly=0,001ms unitLoop=0,028ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,704ms | collect.total=2,654ms collect.avg/unit=2,654ms collect.units=1 collect.cells=4 | constructionVision=0,021ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,061ms publish=0,131ms unitVisibility=0,031ms intel=0,001ms render=0,613ms callbacks=0,169ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,014ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=42 construction.ms=0,065 structure.ms=0,059 lerp.ms=0,882 lerp.cells=21 | collect.total=2,654ms outsideLos=2,654ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,654 cells=4

[AI Perf] CommitAIWorldHeavy: 18ms

[AI VERMELHO][T6] Turno 6 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 1993

[AI VERMELHO][T6][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T6][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T6][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T6] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T6] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T6] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T6] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 523ms

[AI Perf] PRE-Stage2 acumulado: 1116ms

[AI VERMELHO][T6] Fase2 — iniciando ações.

[AI VERMELHO][T6] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (-3, 0, 0) target=null


[AI Perf][InitiativeSetup] total=0,4ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T6][FilaCarona] #1 sai da fila apos 1 turno(s) — nao quer mais carona.

[AI VERMELHO][T6][Capturador] 1 QueroCarona=NAO contexto=RogueOuRebelde setor=None origemAlvo=reserva emergencia=False envelope=Operational custo=5 alvo=(-7, 0, 0) motivo=Infantaria alcança alvo reservado HQ@(-7, 0, 0) (oportunidade) no Operational: custo=5 no turno 2 de 2. Recusa carona.

[AI VERMELHO][T6][SemPlano] 1 âncora = capturável (-7, 0, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T6][Rogue] 1 marcha para âncora (-7, 0, 0) via (-6, -1, 0)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=67 rev=11

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para -6,-1

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,011ms memory=0,009ms geoOnly=0,002ms unitLoop=0,089ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][Perf][Publish] slot=0 contributors=0,001ms knownCells=0,017ms memory=0,004ms geoOnly=0,001ms unitLoop=0,070ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=3,566ms updateCache=2,527ms collect=2,499ms collected=True cells=22 render=0,000ms visibility=0,205ms intel=0,001ms detectionSfx=0,088ms persistence=0,002ms callbacks=0,157ms splitPresentation=True

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T6][Missao] 1 Capture -> (-7, 0, 0) predio=#1 (mantida).

[AI Commit Light][T6][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T6][Red] Soldado#1 action=move decision=24ms execution=352ms snapshot=0ms delay=501ms total=878ms stages=routeDistance:11,5ms/38,validPaths:3,2ms/5,turnChainedCostMap:3,0ms/2,queroCarona:2,8ms/1,melhorCaptura:2,6ms/1,opportunistic:1,8ms/1,aggressive:0,0ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:264,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:73,MovementQueryConfirmedOccupancyUses:73,MovementWavesBuilt:1,PathStatesExpanded:62,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:238,TurnChainedCellsExpanded:202,ValidPathCacheHits:4,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T6][Red] decisions=1 completed=1
  decision=24ms execution=352ms snapshot=0ms delay=501ms measuredTotal=878ms
  boardQueries stages=routeDistance:11,5ms/38,validPaths:3,2ms/5,turnChainedCostMap:3,0ms/2,queroCarona:2,8ms/1,melhorCaptura:2,6ms/1,opportunistic:1,8ms/1,aggressive:0,0ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:264,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:73,MovementQueryConfirmedOccupancyUses:73,MovementWavesBuilt:1,PathStatesExpanded:62,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:238,TurnChainedCellsExpanded:202,ValidPathCacheHits:4,ValidPathWaves:1
  #1 Soldado#1 action=move total=878ms decision=24 execution=352 snapshot=0 delay=501


[AI VERMELHO][T6] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 914ms

[AI VERMELHO][T6] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,011ms memory=0,007ms geoOnly=0,002ms unitLoop=0,088ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,294ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,701ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,080ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][Perf] total=4,563ms | collect.total=4,222ms collect.avg/unit=4,222ms collect.units=1 collect.cells=22 | constructionVision=0,015ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,065ms publish=0,215ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,026ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=22 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=85 construction.ms=0,070 structure.ms=0,119 lerp.ms=1,368 lerp.cells=36 | collect.total=4,222ms outsideLos=4,222ms

[FoW][Coverage] geographic=30 sensor=23 geographicOnly=7 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(-6,-1) ms=4,222 cells=22

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,013ms memory=0,005ms geoOnly=0,001ms unitLoop=0,073ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,772ms | collect.total=2,666ms collect.avg/unit=2,666ms collect.units=1 collect.cells=4 | constructionVision=0,021ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,070ms publish=0,159ms unitVisibility=0,080ms intel=0,001ms render=0,596ms callbacks=0,160ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,012ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=48 construction.ms=0,067 structure.ms=0,061 lerp.ms=0,976 lerp.cells=25 | collect.total=2,666ms outsideLos=2,666ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,666 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=14 sectors=1 bases=1 total=0,4ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,1ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=95 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 16ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 1ms

[AI VERMELHO][T6][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T6][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T6][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T6][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=0 total=18ms

[AI Shopping Roles][T6][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T6][Red] fila unica budget=1993 stance=Offensive
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T6][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T6][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=1993 — caixa preservado

[AI Shopping Roles][T6][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T6] Fase3 concluída.

[AI Perf] Stage3 (shopping): 19ms

[AI VERMELHO][T6] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 19ms

[AI Perf] TURNO TOTAL (Red): 2094ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,573

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,139

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,391

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,121

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,023

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,750

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,951

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,017ms memory=0,005ms geoOnly=0,001ms unitLoop=0,078ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,477ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,868

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,010

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,617

[TurnPerf] etapa=AdvanceTurn ms=4,775

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=109,750

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,089ms

[FoW][Warmup] host=0 slots=1 sources=0 total=35,961ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=36,0ms

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

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,452

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,141

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,174

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,101

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,001

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,005

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,152

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,330

[FoW][Perf][Publish] slot=1 contributors=0,004ms knownCells=0,008ms memory=0,006ms geoOnly=0,002ms unitLoop=0,076ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,255ms

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,015ms memory=0,004ms geoOnly=0,001ms unitLoop=0,074ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,460ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,219

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,002

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,113

[TurnPerf] etapa=AdvanceTurn ms=4,262

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=106,429

[AI VERMELHO][T6] Fase0 concluída.

[AI Perf] Stage0 (wait): 529ms

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,009ms memory=0,007ms geoOnly=0,002ms unitLoop=0,087ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,290ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,647ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T7][slot=1][vermelho] reason=turn-start units=1 enemies=0 total=1ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,082ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=30

[FoW][Perf] total=4,681ms | collect.total=4,340ms collect.avg/unit=4,340ms collect.units=1 collect.cells=22 | constructionVision=0,016ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,057ms publish=0,220ms unitVisibility=0,000ms intel=0,003ms render=0,000ms callbacks=0,000ms store=0,016ms | boardCells=153 unitsScanned=2 unaccounted=0,029ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=22 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=85 construction.ms=0,074 structure.ms=0,163 lerp.ms=1,348 lerp.cells=36 | collect.total=4,340ms outsideLos=4,340ms

[FoW][Coverage] geographic=30 sensor=23 geographicOnly=7 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(-6,-1) ms=4,340 cells=22

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,005ms geoOnly=0,001ms unitLoop=0,075ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=3,717ms | collect.total=2,604ms collect.avg/unit=2,604ms collect.units=1 collect.cells=4 | constructionVision=0,022ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,069ms publish=0,166ms unitVisibility=0,081ms intel=0,001ms render=0,596ms callbacks=0,158ms store=0,008ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=48 construction.ms=0,066 structure.ms=0,059 lerp.ms=0,981 lerp.cells=25 | collect.total=2,604ms outsideLos=2,604ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,604 cells=4

[AI Perf] CommitAIWorldHeavy: 17ms

[AI VERMELHO][T7] Turno 7 | Stance: Offensive | 1 unidades | 0 inimigos visíveis | R$ 2993

[AI VERMELHO][T7][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Perf] BuildObjectivePlan: 0ms

[AI Ops][T7][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T7][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild: 1ms

[AI VERMELHO][T7] Fase1 — iniciando. replayManager=True turnStateManager=True

[AI VERMELHO][T7] Fase1 — enviando batch CommandService.

[Replay][CommandService] ExecuteRecordedCommandServiceBatch iniciado.

[AI VERMELHO][T7] Fase1 — batch concluído. Aguardando IsAutoCommandServiceBusy...

[AI VERMELHO][T7] Fase1 — Serviço do Comando concluído.

[AI Perf] Stage1 (command): 522ms

[AI Perf] PRE-Stage2 acumulado: 1113ms

[AI VERMELHO][T7] Fase2 — iniciando ações.

[AI VERMELHO][T7] Fase2 iniciativa (1 unidades):
  [grp=4] Soldado#1 @ (-6, -1, 0) target=null


[AI Perf][InitiativeSetup] total=0,4ms available=0,1ms snapshot=0,1ms repair=0,0ms groups=0,0ms facts=0,1ms sort=0,0ms log=0,1ms

[AI VERMELHO][T7][Capturador] 1 QueroCarona=NAO contexto=RogueOuRebelde setor=None origemAlvo=reserva emergencia=False envelope=Operational custo=3 alvo=(-7, 0, 0) motivo=Infantaria alcança alvo reservado HQ@(-7, 0, 0) (oportunidade) no Operational: custo=3 no turno 1 de 2. Recusa carona.

[AI VERMELHO][T7][SemPlano] 1 âncora = capturável (-7, 0, 0) (mais próximo alcançável a pé).

[AI VERMELHO][T7][FoW] 1 DPQ para revelar HQ via (-6, 0, 0) (ev=2)

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U1 unitId=304154 mp=3 fuel=64 rev=14

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U1

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U1

moveu para -6,0

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U1 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][CommittedDelta] kinds=UnitActed units=1 cells=2 full=False reconcile=False

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,012ms memory=0,007ms geoOnly=0,002ms unitLoop=0,049ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=28

[FoW][Perf][Publish] slot=0 contributors=0,001ms knownCells=0,020ms memory=0,005ms geoOnly=0,001ms unitLoop=0,074ms | recordMemory=True targetOnly=True evaluated=1 visibilityProbes=1 knownCells.count=19

[FoW][AffectedTargets] slot=0 cells=2 evaluated=1 totalUnits=2

[FoW][AffectedTargets][Visual] slot=0 cells=2 evaluated=1

[FoW][Perf][Incremental] unit=Soldado_T1_U1 total=5,204ms updateCache=1,977ms collect=1,953ms collected=True cells=21 render=0,000ms visibility=0,164ms intel=1,483ms detectionSfx=0,758ms persistence=0,002ms callbacks=0,158ms splitPresentation=True

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI VERMELHO][T7][Missao] 1 Capture -> (-7, 0, 0) predio=#1 (mantida).

[AI Commit Light][T7][slot=1][vermelho] reason=phase2:Soldado#1 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T7][Red] Soldado#1 action=move decision=11ms execution=167ms snapshot=0ms delay=506ms total=684ms stages=validPaths:2,6ms/5,turnChainedCostMap:1,8ms/2,melhorCaptura:1,6ms/1,queroCarona:1,4ms/1,opportunistic:1,1ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:177,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:49,MovementQueryConfirmedOccupancyUses:49,MovementWavesBuilt:1,PathStatesExpanded:43,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:156,TurnChainedCellsExpanded:134,ValidPathCacheHits:4,ValidPathWaves:1

[AI Perf][Phase2 Breakdown][T7][Red] decisions=1 completed=1
  decision=11ms execution=167ms snapshot=0ms delay=506ms measuredTotal=684ms
  boardQueries stages=validPaths:2,6ms/5,turnChainedCostMap:1,8ms/2,melhorCaptura:1,6ms/1,queroCarona:1,4ms/1,opportunistic:1,1ms/1 metrics=CaptureClaimAssignments:1,CaptureClaimSnapshotBuilds:1,CaptureClaimSnapshotHits:2,CellsVisited:177,MelhorCapturaCalls:1,MelhorCapturaCandidates:2,MelhorCapturaReachBuilds:1,MelhorCapturaTargets:1,MovementCacheHits:4,MovementCacheMisses:1,MovementCacheStores:1,MovementQueryCachesBuilt:49,MovementQueryConfirmedOccupancyUses:49,MovementWavesBuilt:1,PathStatesExpanded:43,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,ReachableCellsProduced:156,TurnChainedCellsExpanded:134,ValidPathCacheHits:4,ValidPathWaves:1
  #1 Soldado#1 action=move total=684ms decision=11 execution=167 snapshot=0 delay=506


[AI VERMELHO][T7] Fase2 concluída — todas as 1 unidades agiram.

[AI Perf] Stage2 (actions): 720ms

[AI VERMELHO][T7] Fase3 — compras.

[AI Commit Heavy] Sync1: 0ms

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,009ms memory=0,006ms geoOnly=0,002ms unitLoop=0,044ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=28

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,302ms

[FoW][PlanningBarrier] slot=1 result=reused total=0,663ms

[AI Commit Heavy] FogBarrier: 1ms slot=1 result=ReusedAndReconciled

[FoW][Perf][Publish] slot=1 contributors=0,003ms knownCells=0,007ms memory=0,007ms geoOnly=0,002ms unitLoop=0,037ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=28

[FoW][Perf] total=3,923ms | collect.total=3,625ms collect.avg/unit=3,625ms collect.units=1 collect.cells=21 | constructionVision=0,015ms constructions=2 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,057ms publish=0,179ms unitVisibility=0,000ms intel=0,005ms render=0,000ms callbacks=0,000ms store=0,017ms | boardCells=153 unitsScanned=2 unaccounted=0,026ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=21 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=74 construction.ms=0,069 structure.ms=0,120 lerp.ms=1,110 lerp.cells=29 | collect.total=3,625ms outsideLos=3,625ms

[FoW][Coverage] geographic=28 sensor=22 geographicOnly=6 sources=3 unitSources=1 constructionSources=2

[FoW][Pool] rents=1 releases=1 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T1_U1 slot=1 cell=(-6,0) ms=3,625 cells=21

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,014ms memory=0,005ms geoOnly=0,001ms unitLoop=0,069ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][Perf] total=4,316ms | collect.total=2,630ms collect.avg/unit=2,630ms collect.units=1 collect.cells=4 | constructionVision=0,020ms constructions=1 | dominant=CollectVisibleCells

[FoW][Perf][Steps] unitScan=0,061ms publish=0,161ms unitVisibility=0,672ms intel=0,003ms render=0,590ms callbacks=0,155ms store=0,010ms | boardCells=153 unitsScanned=2 unaccounted=0,013ms

[FoW][Cache] hits=0 misses=0

[FoW][Perf][Collect] runs=0 maxRange=0 distanceCells=0 outCells=4 layerChecks=0 specChecks=0 los.calls=0 los.hits=0 los.misses=0 aquaticMaps=0

[FoW][Perf][Los] los.ms=0,000 cellVision.calls=46 construction.ms=0,063 structure.ms=0,060 lerp.ms=0,970 lerp.cells=21 | collect.total=2,630ms outsideLos=2,630ms

[FoW][Coverage] geographic=9 sensor=4 geographicOnly=5 sources=2 unitSources=1 constructionSources=1

[FoW][Pool] rents=2 releases=2 fragataCollect.rents=0 fragataCollect.releases=0

[FoW][Perf][CollectTop1] unit=Soldado object=Soldado_T0_U2 slot=0 cell=(-7,0) ms=2,630 cells=4

[SectorManager] rebuild reason=ai-commit:phase3:pre-shopping sectors=1 bases=1 constructions=2 semSetor=0

[SectorManager][Perf] rebuild reason=ai-commit:phase3:pre-shopping revision=15 sectors=1 bases=1 total=0,3ms

[SectorManager][Perf][Steps] contexts=0,2ms (search=0,0ms calls=0) sectorLoop=0,0ms neighborPass=0,1ms | search.calls=4 search.ms=0,0 search.hits=4 search.failures=0 search.exhausted=0 search.expanded=0 cache.size=95 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=0/0 tile=0/0 | constructions=2 unaccounted=0,0ms

[AI Commit Heavy] SectorRebuild: 16ms reused=False

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI VERMELHO][T7][Plan] slot=1 rebelde runtime: BuildObjectivePlan ignorado; distribuicao sera unidade-a-unidade por distancia.

[AI Ops][T7][Red] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=0 activeSAM=0

[AI Ops][T7][Red] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Commit Heavy][T7][slot=1][vermelho] reason=phase3:pre-shopping units=1 enemies=1 total=18ms

[AI Shopping Roles][T7][Red] doutrina rebelde: demanda so Capturador — sem pacote 2/2/1 / elite / ar

[AI Shopping Roles][T7][Red] fila unica budget=2993 stance=Tactical
  pri=30 urgent=False Capturador/None x6 elite=0-2147483647 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI Shopping Roles][T7][Red] expansão econômica: prioriza até 3 capturador(es) no carrinho antes de diversificar

[AI Shopping Roles][T7][Red] carrinho vazio: nenhuma oferta elegível atende demanda com gastoLivre=2993 — caixa preservado

[AI Shopping Roles][T7][Red] pendente Capturador/None x6 pri=30 origem=rebel-insurgency motivo=doutrina rebelde: so capturador (sem composicao/elite/ar)

[AI VERMELHO][T7] Fase3 concluída.

[AI Perf] Stage3 (shopping): 23ms

[AI VERMELHO][T7] Fase4 — passando a vez.

[Replay][EndTurn] ExecuteRecordedEndTurnBatch iniciado.

[FSM] Estado: Neutral -> EndingTurnExecuting

[TurnState] transition=Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu | selected=(none) | stack=Neutral > EndingTurnExecuting

[FSM][Enter] Neutral -> EndingTurnExecuting | reason=TryExecuteEndingTurnFromMenu

[FSM] Estado: EndingTurnExecuting -> Neutral

[TurnState] transition=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched | selected=(none) | stack=Neutral

[FSM][Reset] previous=EndingTurnExecuting -> Neutral | reason=TryExecuteEndingTurnFromMenu: dispatched

[AI Perf] Stage4 (end turn): 23ms

[AI Perf] TURNO TOTAL (Red): 1905ms

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=False

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=2 ms=0,704

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=2 ms=0,142

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=2,013

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,124

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,002

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=0,025

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,748

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=0,956

[FoW][Perf][Publish] slot=0 contributors=0,002ms knownCells=0,016ms memory=0,005ms geoOnly=0,001ms unitLoop=0,072ms | recordMemory=True targetOnly=False evaluated=2 visibilityProbes=1 knownCells.count=19

[FoW][TurnStartCache] slot=0 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=1 constructions.removed=1 total=0,475ms

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=0,864

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,011

[TurnPerf] etapa=ApplyActiveTeam.Total ms=4,261

[TurnPerf] etapa=AdvanceTurn ms=4,422

[TurnPerf] etapa=AdvanceTurnTransitionRoutine.Total ms=106,187

[FoW][TurnStartCache] slot=1 activated=true units.changed=0 units.unchanged=1 units.removed=0 cells.collected=0 constructions=2 constructions.removed=2 total=0,090ms

[FoW][Warmup] host=0 slots=1 sources=0 total=36,245ms

[FoW][Warmup][Steps] activate=0,0ms work=0,0ms store=0,0ms restore=0,0ms | cpu=0,0ms clonedSources=0 frames=0 budget=40ms idleBetweenFrames=36,2ms

