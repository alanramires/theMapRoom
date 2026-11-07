[Quadrante] 'A invasão a Auridia/Peixe Pequeno' construido: 306 tiles, 0 buraco(s), 44 construcao(oes), 0 unidade(s), 1 camada(s), 2 trecho(s) de rota, 18x17 em 'TileMap' (origem de pintura 0,0; enquadramento pedido 0,0; origem de autoria -3,10) em 189 ms.

[BoardTopology] Runtime fallback 'Assets/Scenes/Batalha.unity::TileMap': 0 error(s), 0 warning(s).

[SectorManager][Perf] rebuild reason=first-query revision=59 sectors=17 bases=2 total=457,8ms

[SectorManager][Perf][Steps] contexts=2,7ms (search=0,0ms calls=0) sectorLoop=327,7ms neighborPass=120,8ms | search.calls=1442 search.ms=434,2 search.hits=379 search.failures=735 search.exhausted=0 search.expanded=46950 cache.size=1063 | vizinhos=392,6ms transicoes=279450 rota.calls=260518 rota.cache=257680 rota.topologia=156 rota.varreduraRede=2682 terreno=261253/306 tile=282865/378 | constructions=44 unaccounted=6,7ms

[FogOfWar] Sorting layer validada em SFX.

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=True

[AI] RunAITurn iniciado para Blue.

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=0 ms=0,000

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=44 ms=3,869

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=9,925

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=4,203

[TurnPerf] etapa=ReleaseUnits.EvaluateVictoryStars ms=0,450

[TurnPerf] etapa=ReleaseUnits.ApplyEconomy ms=2,574

[TurnPerf] etapa=ReleaseUnits.IterateUnits ms=0,001

[TurnPerf] etapa=ReleaseUnits.EnqueueFuelDepletionDeaths ms=0,172

[TurnPerf] etapa=ApplyActiveTeam.ReleaseUnitsForActiveTeam ms=5,510

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility ms=1,863

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=1,868

[TurnPerf] etapa=ApplyActiveTeam.Total ms=24,541

[FogOfWar] Sorting layer validada em SFX.

[Debug Command] FoW ON (debug).

[FoW][Estado][ON] slotApresentacao=0 slotAtivo=0 debugLigado=True parcial=True(manual=False) totalWar=False | explorado=0 nevoaTiles=0 celulasTabuleiro=0 displayVisivel=0 | bakes=3 overlayInit=False renderValido=False observadorCache=-2147483648

[AI][Preset] dificuldade=Dificil (DIFICIL) ? perfil 'AIPreset_Dificil' do catalogo, sem overlay (capacidades do perfil; valores ainda da cena)

[AI] Start — matchController=True replayManager=True turnStateManager=True

[AI] Pausa de debug solicitada. Aguardando ponto seguro.

[AI] Start on Pause ativo - estado inicial equivalente ao F10.

[AI] Start — time ativo (Blue) já é IA, iniciando turno.

[AI] HandleTeamChanged — teamIndex=2 newTeam=Blue matchController=True isAI=True

[AI] RunAITurn iniciado para Blue.

[FrameSpike] frame=1 duration=5954,60ms state=Neutral substep=AwaitingAction selected=(none) boardRev=59 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=739,5MB managedDelta=0,0MB gcDelta=[314,314,314] unityAlloc=782,9MB

[TurnState] substep=-1 -> AwaitingAction | state=Neutral

[SectorManager][Perf] rebuild reason=on-enable revision=59 sectors=17 bases=2 total=17,6ms

[SectorManager][Perf][Steps] contexts=0,8ms (search=0,0ms calls=0) sectorLoop=13,7ms neighborPass=2,9ms | search.calls=1442 search.ms=14,8 search.hits=1442 search.failures=735 search.exhausted=0 search.expanded=0 cache.size=1063 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=1041/0 tile=2740/0 | constructions=44 unaccounted=0,1ms

[FrameSpike] frame=2 duration=1223,72ms state=Neutral substep=AwaitingAction selected=(none) boardRev=59 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=744,6MB managedDelta=+5,1MB gcDelta=[0,0,0] unityAlloc=791,8MB

[AI ][T0] Fase0 concluída.

[AI Perf] Stage0 (wait): 604ms

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[FSM] Estado: Neutral -> Loading

[TurnState] transition=Neutral -> Loading | reason=TryEnterLoadingState | selected=(none) | stack=Neutral > Loading

[FSM][Enter] Neutral -> Loading | reason=TryEnterLoadingState

[FSM] Estado: Loading -> Neutral

[TurnState] transition=Loading -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Loading -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[SectorManager][Perf] rebuild reason=post-restore-constructions revision=128 sectors=17 bases=2 total=114,2ms

[SectorManager][Perf][Steps] contexts=1,5ms (search=0,0ms calls=0) sectorLoop=87,2ms neighborPass=25,4ms | search.calls=1636 search.ms=110,2 search.hits=1375 search.failures=835 search.exhausted=0 search.expanded=10024 cache.size=1324 | vizinhos=90,0ms transicoes=59826 rota.calls=54728 rota.cache=54728 rota.topologia=0 rota.varreduraRede=0 terreno=55877/0 tile=63097/0 | constructions=44 unaccounted=0,1ms

[FrameSpike] frame=431 duration=865,97ms state=Neutral substep=AwaitingAction selected=(none) boardRev=277 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=True managed=870,7MB managedDelta=+54,1MB gcDelta=[0,0,0] unityAlloc=801,7MB

[AI] HandleTeamChanged — teamIndex=3 newTeam=Yellow matchController=True isAI=True

[AI] HandleTeamChanged adiado: load em andamento; turno (re)inicia pos-restauracao.

[TurnPerf] handler=UnitManager.HandleActiveTeamChanged count=73 ms=21,423

[TurnPerf] handler=ConstructionManager.HandleActiveTeamChanged count=44 ms=5,570

[TurnPerf] etapa=ApplyActiveTeam.OnActiveSlotChanged ms=32,842

[TurnPerf] etapa=ApplyActiveTeam.TeleportCursorToHQ ms=0,000

[TurnPerf] etapa=ApplyActiveTeam.FogAndVisibility.Suppressed ms=0,009

[TurnPerf] etapa=ApplyActiveTeam.FlushTurnStartAutonomyHelper ms=0,004

[TurnPerf] etapa=ApplyActiveTeam.Total ms=33,086

[AI][Preset] dificuldade=Dificil (DIFICIL) ? perfil 'AIPreset_Dificil' do catalogo, sem overlay (capacidades do perfil; valores ainda da cena)

[FSM] Estado: Neutral -> Neutral

[TurnState] transition=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=Neutral -> Neutral | reason=ClearSelectionAndReturnToNeutral

[FoW][LoadCacheRestore] slot=-2147483648 success=false retained=false fallback=cold reason=cache_unavailable

[FrameSpike] frame=433 duration=763,42ms state=Neutral substep=AwaitingAction selected=(none) boardRev=292 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=932,6MB managedDelta=+61,4MB gcDelta=[0,0,0] unityAlloc=841,7MB

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[SectorManager][Perf] rebuild reason=after-load-success revision=292 sectors=17 bases=2 total=20,5ms

[SectorManager][Perf][Steps] contexts=1,7ms (search=0,0ms calls=0) sectorLoop=15,8ms neighborPass=2,8ms | search.calls=1636 search.ms=16,6 search.hits=1636 search.failures=835 search.exhausted=0 search.expanded=0 cache.size=1324 | vizinhos=0,0ms transicoes=0 rota.calls=0 rota.cache=0 rota.topologia=0 rota.varreduraRede=0 terreno=1149/0 tile=3014/0 | constructions=44 unaccounted=0,1ms

[AI] RunAITurn iniciado para Yellow.

[AI Stage] Pos-load: coroutine retomada para Yellow no turno 8, stage 2, paused=True.

[AI Stage] Retomando turno de Yellow a partir do stage 2.

[AI Stage] Stage 0 ja concluido pelo save.

[AI Perf][ResumeSettle] ready immediately: 0ms | state=Neutral stack=Neutral autoCS=False scanner=False replayBusy=False

[AI Perf] Resume settle wait: 0ms

[AI Commit Heavy] Sync1: 1ms

[AI Commit Heavy] FogBarrier: 0ms slot=1 result=Unavailable

[AI Commit Heavy] SectorRebuild: 0ms reused=True

[AI Commit Heavy] Sync2: 0ms

[AI Commit Heavy] AIWorldSnapshot.Build: 0ms

[AI Commit Heavy][T8][slot=1][amarelo] reason=turn-start units=27 enemies=31 total=2ms

[AI Perf] CommitAIWorldHeavy: 16ms

[AI AMARELO][T8] Turno 8 | Stance: Tactical | 27 unidades | 31 inimigos visíveis | R$ 25070

[AI AMARELO][T8][Stage] Retomando stage 2 com plano salvo; BuildObjectivePlan ignorado.

[AI Plan][T8][Yellow] resume assignments: restaurados=23 indisponiveis=0 duplicados=0 badges obsoletos=0 rogues=1

[AI Ops][T8][Yellow] SectorDefense Echo: sem demanda atual visibleGround=False visibleAir=False intelHot=4,2

[AI Ops][T8][Yellow] GroundCapture Lima: cap=2 ass=0 fire=0 trans=0 pref=Air risky=True slots=Capturerx2 Assaultx1

[AI Ops][T8][Yellow] GroundCapture India: cap=2 ass=0 fire=0 trans=0 pref=Air risky=True slots=Capturerx2 Assaultx1

[AI Ops][T8][Yellow] GroundCapture November: cap=2 ass=0 fire=0 trans=0 pref=Air risky=True slots=Capturerx2 Assaultx1

[AI Ops][T8][Yellow] EarlyAxisAirlift eixo=1 alvo=Hotel front=1 dist=340282300000000000000000000000000000000 (Chinook cedo nos nós intermediários)

[AI Ops][T8][Yellow] EarlyAxisAirlift eixo=3 alvo=Quebec front=1 dist=340282300000000000000000000000000000000 (Chinook cedo nos nós intermediários)

[AI Ops][T8][Yellow] AirliftCapture Charlie: desejado=4 qualified=4 embarked=1 cap_deficit=0 air_deficit=2 intelHot=57,6

[AI Ops][T8][Yellow] AirliftCapture Papa: desejado=2 qualified=2 embarked=0 cap_deficit=0 air_deficit=1 intelHot=10,4

[AI Ops][T8][Yellow] AirliftCapture Hotel: desejado=4 qualified=4 embarked=0 cap_deficit=0 air_deficit=2 intelHot=6,4

[AI Ops][T8][Yellow] AirRefuelSupport: lowFuel=1 critical=0 airFleet=8 activeTankers=0 deficit=1

[AI Ops][T8][Yellow] PreventiveDefense: Artilleryx1 AAAx0 SAMx0 aircraftNearHQ=0 activeArt=0 activeAAA=1 activeSAM=1

[AI Ops][T8][Yellow] GroundCapture Lima pri=4 phase=Engaging urgent=False preventive=False slots=Capturerx2 Assaultx1 assigned=2 screen=21 reason=screen #21 dist=3,0

[AI Ops][T8][Yellow] GroundCapture India pri=8 phase=Engaging urgent=False preventive=False slots=Capturerx2 Assaultx1 assigned=2 screen=52 reason=screen #52 dist=11,0

[AI Ops][T8][Yellow] GroundCapture November pri=10 phase=Holding urgent=False preventive=False slots=Capturerx2 Assaultx1 assigned=2 screen=40 reason=screen #40 dist=0,0

[AI Ops][T8][Yellow] AirliftCapture Hotel pri=4 phase=Engaging urgent=False preventive=False slots=AirTransportx1 assigned=1 screen=- reason=sem screen minimo

[AI Ops][T8][Yellow] AirliftCapture Quebec pri=4 phase=Engaging urgent=False preventive=False slots=AirTransportx1 assigned=1 screen=- reason=sem screen minimo

[AI Ops][T8][Yellow] AirliftCapture Charlie pri=5 phase=Forming urgent=False preventive=False slots=AirTransportx2 Assaultx1 Artilleryx1 FighterBx1 assigned=0 screen=- reason=sem screen minimo

[AI Ops][T8][Yellow] AirliftCapture Papa pri=7 phase=Forming urgent=False preventive=False slots=AirTransportx1 Assaultx1 FighterBx1 assigned=0 screen=- reason=sem screen minimo

[AI Ops][T8][Yellow] AirliftCapture Hotel pri=9 phase=Forming urgent=False preventive=False slots=AirTransportx2 Assaultx1 FighterBx1 assigned=0 screen=- reason=sem screen minimo

[AI Ops][T8][Yellow] AirRefuelSupport Base1 pri=3 phase=Forming urgent=False preventive=False slots=AirTankerx1 assigned=0 screen=- reason=

[AI Ops][T8][Yellow] PreventiveDefense Base1 pri=6 phase=Forming urgent=False preventive=True slots=Artilleryx1 assigned=0 screen=- reason=

[AI Perf] TacticalAnalyzer.Rebuild (resume): 202ms

[AI AMARELO][T8][Stage] Stage 1 ja concluido pelo save.

[AI Perf] PRE-Stage2 acumulado: 285ms

[AI AMARELO][T8] Fase2 — iniciando ações.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#6 Infantaria alcança alternativa livre no setor Juliet (16, 12, 0) no Operational: custo=4 no turno 2 de 2. Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#7 (carregado) Unidade IsUnderRepair runtime: necessidade emergencial aceita carona antes da avaliação de objetivo. O resultado é um pedido, não uma ordem de transporte.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#9 Infantaria alcança representante de Golf no Tactical: custo=2 no turno 1 (nesta rodada). Recusa carona.

[AI AMARELO][T8][FilaCarona] #10 embarcado — sai da fila apos 6 turno(s).

[AI AMARELO][T8][Transporte][QueroCarona] pax=#16 Infantaria sem destino livre alcançável no setor Charlie em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCaronaAerea] pax=#18 Unidade em emergência de reparo: aceita carona antes da avaliação de objetivo.

[AI AMARELO][T8][FilaCarona] #18 entra na fila no turno 8 — fora das bandas (score=2000).

[AI AMARELO][T8][Transporte][QueroCarona] pax=#21 Infantaria alcança representante de Lima no Operational: custo=3 no turno 1 de 2. Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#30 (carregado) Unidade alcança destino da carga (8, 12, 0) no Tactical: custo=6 no turno 1 (nesta rodada). Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#32 Infantaria alcança alternativa livre no setor Hotel (14, 14, 0) no Tactical: custo=0 no turno 1 (nesta rodada). Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#33 Infantaria alcança representante de Hotel no Tactical: custo=0 no turno 1 (nesta rodada). Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#39 Infantaria sem destino livre alcançável no setor Hotel em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#40 Infantaria alcança representante de November no Tactical: custo=0 no turno 1 (nesta rodada). Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#41 Infantaria alcança representante de Papa no Operational: custo=2 no turno 1 de 2. Recusa carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#48 Infantaria sem destino livre alcançável no setor Hotel em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCaronaAerea] pax=#49 Unidade sem emergência de reparo.

[AI AMARELO][T8][Transporte][QueroCaronaAerea] pax=#50 Unidade sem emergência de reparo.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#51 Infantaria sem destino livre alcançável no setor India em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#52 Infantaria sem destino livre alcançável no setor India em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#53 Unidade sem emergência de reparo.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#60 Infantaria rogue/rebelde sem prédio capturável livre alcançável em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#61 Infantaria sem destino livre alcançável no setor Papa em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#62 Infantaria sem destino livre alcançável no setor Charlie em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#63 Infantaria sem destino livre alcançável no setor Golf em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCaronaAerea] pax=#68 Unidade sem emergência de reparo.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#69 Infantaria sem destino livre alcançável no setor Lima em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

[AI AMARELO][T8][Transporte][QueroCarona] pax=#70 Unidade sem emergência de reparo.

[AI AMARELO][T8][Transporte][QueroCaronaAerea] pax=#71 Unidade sem emergência de reparo.

[AI AMARELO][T8][Missao] 72 HERDA da carga #10 -> Transport (16, 12, 0) (publicada no setup).

[AI AMARELO][T8][Transporte][QueroCarona] pax=#72 (carregado) Unidade alcança destino da carga (16, 12, 0) no Operational: custo=7 no turno 2 de 2. Recusa carona.

[AI AMARELO][T8] Fase2 iniciativa (22 unidades):
  [grp=0] Soldado#32 @ (14, 14, 0) target=(16, 14, 0) missao=Capture(14, 14, 0) carona=nao
  [grp=0] Soldado#6 @ (12, 13, 0) target=(16, 12, 0) missao=Capture(16, 12, 0) carona=nao
  [grp=0] Soldado#61 @ (10, 1, 0) target=(6, 3, 0) missao=Capture(6, 3, 0) carona=SIM(1t)
  [grp=0] Soldado#51 @ (9, 3, 0) target=(14, 16, 0) missao=Capture(14, 16, 0) carona=SIM(2t)
  [grp=1] Chinook#30 @ (11, 7, 0) target=null missao=Transport(8, 12, 0) carona=nao
  [grp=1] Apache#50 @ (13, 14, 0) target=null missao=None carona=nao
  [grp=1] Apache#49 @ (13, 12, 0) target=null missao=None carona=nao
  [grp=1] Apache#68 @ (13, 3, 0) target=null missao=None carona=nao
  [grp=1] Apache#71 @ (13, 2, 0) target=null missao=None carona=nao
  [grp=2] Soldado#33 @ (15, 14, 0) target=(16, 14, 0) missao=Capture(15, 14, 0) carona=nao
  [grp=2] Soldado#52 @ (11, 5, 0) target=(14, 16, 0) missao=Capture(14, 16, 0) carona=SIM(2t)
  [grp=2] Soldado#39 @ (12, 7, 0) target=(16, 14, 0) missao=Capture(14, 14, 0) carona=SIM(3t)
  [grp=2] Soldado#63 @ (15, 6, 0) target=(12, 13, 0) missao=Capture(12, 13, 0) carona=SIM(1t)
  [grp=2] Soldado#48 @ (14, 8, 0) target=(16, 14, 0) missao=Capture(14, 14, 0) carona=SIM(2t)
  [grp=2] Soldado#16 @ (11, 9, 0) target=(8, 12, 0) missao=Capture(8, 12, 0) carona=SIM(2t)
  [grp=2] Soldado#21 @ (1, 3, 0) target=(3, 5, 0) missao=Capture(3, 5, 0) carona=nao
  [grp=2] Soldado#9 @ (15, 12, 0) target=(12, 13, 0) missao=Capture(12, 13, 0) carona=nao
  [grp=2] Soldado#62 @ (12, 2, 0) target=(8, 12, 0) missao=Capture(8, 12, 0) carona=SIM(1t)
  [grp=3] Soldado#69 @ (15, 2, 0) target=(3, 5, 0) missao=Capture(4, 5, 0) carona=SIM(0t)
  [grp=4] Soldado#60 @ (15, 1, 0) target=null missao=Capture(6, 1, 0) carona=SIM(1t)
  [grp=4] Anti Arcraft Artilery#53 @ (9, 7, 0) target=null missao=None carona=nao
  [grp=5] Chinook#7 @ (8, 5, 0) target=null missao=Transport(8, 12, 0) carona=SIM(0t)


[AI Perf][InitiativeSetup] total=968,6ms available=10,8ms snapshot=2,6ms repair=467,7ms pickupFacts=1,0ms groups=435,0ms facts=43,9ms sort=1,2ms log=6,5ms

[AI Perf][PostLoadReady] iniciativa preparada; beep liberado.

[FrameSpike] frame=438 duration=1192,84ms state=Neutral substep=AwaitingAction selected=(none) boardRev=292 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=904,7MB managedDelta=-28,6MB gcDelta=[1,1,1] unityAlloc=846,2MB

[AI AMARELO][T8] Fase2 pacing adaptativo: unidades=22 delay=0,273s.

[AI] Pausa de debug ativa - aguardando 'AI RESUME' ou 'AI STEP'.

[TurnState] state=Neutral | step=HandleConfirm | selected=(none)

[TurnState] state=Neutral | step=HandleConfirmWhileNeutral | selected=(none)

[Debug Command] Show AI Step ON (decisao preservada).

[AI AMARELO][T8][Oportunista] 32 captura célula atual (14, 14, 0) antes de embarcar

[AI Review] #1 pendente: Soldado #32 vai capturar no hex (14,14), movendo de (14,14) para (14,14).

[FrameSpike] frame=1097 duration=291,14ms state=Neutral substep=AwaitingAction selected=(none) boardRev=292 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=927,0MB managedDelta=+1,5MB gcDelta=[0,0,0] unityAlloc=850,4MB

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U32 unitId=-295628 mp=3 fuel=67 rev=292

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U32 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U32

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U32

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuParado) | selected=Soldado_T1_U32

[FSM] Estado: UnitSelected -> MoveuParado

[TurnState] transition=UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado) | selected=Soldado_T1_U32 | stack=Neutral > UnitSelected > MoveuParado

[FSM][Enter] UnitSelected -> MoveuParado | reason=EnterSensorsState(anchor=MoveuParado)

[Movement] moveu no mesmo lugar

[FSM] Estado: MoveuParado -> Capturando

[TurnState] transition=MoveuParado -> Capturando | reason=HandleCaptureActionRequested | selected=Soldado_T1_U32 | stack=Neutral > UnitSelected > MoveuParado > Capturando

[FSM][Enter] MoveuParado -> Capturando | reason=HandleCaptureActionRequested

[FSM] Estado: Capturando -> CapturandoExecuting

[TurnState] transition=Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin | selected=Soldado_T1_U32 | stack=Neutral > UnitSelected > MoveuParado > Capturando > CapturandoExecuting

[FSM][Enter] Capturando -> CapturandoExecuting | reason=ExecuteCaptureSequence: begin

[Captura] Soldado_T1_U32 causou 10 de captura em Cidade (10 -> 0).

[Captura] Construcao capturada por amarelo. Capture resetado para 20/20.

[FSM] Estado: CapturandoExecuting -> Neutral

[TurnState] transition=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=CapturandoExecuting -> Neutral | reason=ClearSelectionAndReturnToNeutral

[AI Review] #1 Batch terminou: sucesso=True; caixa 25070->25070. Consulte JogadasLog para efeitos de combate/captura.

[AI Perf][Unit][T8][Yellow] Soldado#32 action=wait decision=16ms execution=12312ms snapshot=0ms delay=280ms total=12608ms stages=- metrics=-

[AI AMARELO][T8][PontaLanca] 6 revezou e ataca Soldado#13 via (14, 15, 0) em vez de so andar (capturando Metropole_T-1_C36 | hp=10 dpq=1,0 threat=30,0 score=6103620 atkDecision hp=5->1 loss=80%/70% dmg=10%/10% target=10->9 dpq=1/2 def=0/2 kill=False survive=True OK hpLossOfMax)

[AI AMARELO][T8] Fase2 — Soldado#6 NAO cedeu ataque em Soldado#13 (nenhum amaciamento de artilharia):
  Anti Arcraft Artilery#53: sensor-nao-mira-alvo-de-nenhuma-celula
  Surface Air Missile#70: indisponivel (acted=True dead=False embarked=False)


[AI AMARELO][T8][AirCombat] 49 Tactical scan: cells=80 prunedByCubic=61 sensorCalls=19 candidates=25 visibleEnemies=31

[AI AMARELO][T8] Fase2 — Soldado#6 cede ataque aereo em 13 para Apache#49 atacar primeiro via (13, 15, 0)

[FrameSpike] frame=1873 duration=827,16ms state=Neutral substep=AwaitingAction selected=(none) boardRev=293 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=894,2MB managedDelta=+0,7MB gcDelta=[0,0,0] unityAlloc=850,9MB

[AI AMARELO][T8][Capturador] 61 QueroCarona=SIM contexto=ComPlano setor=Papa origemAlvo=reserva emergencia=False envelope=BeyondOperational custo=- alvo=(6, 3, 0) motivo=Infantaria nao alcanca alvo do plano Papa Metropole@(6, 3, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

ACCEPT pax=#61 slot=0 carona=Requested ajuste=1600 fila=1t motivo=Infantaria nao alcanca alvo do plano Papa Metropole@(6, 3, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

LZ=(11, 4, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=4 rotaTatica=4

LZ=(12, 1, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=4 rotaTatica=4

LZ=(10, 3, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(10, 4, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(11, 2, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(12, 0, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(9, 4, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(10, 2, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(11, 0, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZs sem passageiro ReachableNow=105; preservadas apenas no ranking plano.

[AI AMARELO][T8][Capturador] 61 MelhorEmbarque: vai ao encontro; encontroPax=(10, 2, 0) LZTransport=(10, 3, 0) transportador=#72 tier=Tactical route=ReachableNow promessa=nao.

[AI Review] #2 pendente: Soldado #61 vai apenas mover de (10,1) para (10,2). Decisão: vai ao encontro do transportador Chinook#72; encontroPax=(10, 2, 0), LZTransport=(10, 3, 0), tier=Tactical, promessa=nao.

[FrameSpike] frame=1875 duration=579,98ms state=Neutral substep=AwaitingAction selected=(none) boardRev=293 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=901,2MB managedDelta=+6,9MB gcDelta=[0,0,0] unityAlloc=851,7MB

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U61 unitId=-302232 mp=3 fuel=67 rev=293

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U61 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U61

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U61

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U61

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U61

moveu para 10,2

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U61

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U61 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Review] #2 Batch terminou: sucesso=True; caixa 25070->25070. Consulte JogadasLog para efeitos de combate/captura.

[AI Commit Light][T8][slot=1][amarelo] reason=phase2:Soldado#61 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T8][Yellow] Soldado#61 action=move decision=437ms execution=4176ms snapshot=0ms delay=282ms total=4896ms stages=melhorEmbarque:309,2ms/1,melhorEmbarque.lzLoop:294,7ms/1,melhorEmbarque.lzGates:262,1ms/1,melhorEmbarque.resolveMeeting:19,6ms/1,movementCostMap:10,1ms/4,melhorEmbarque.passengerReach:8,7ms/1,validPaths:5,3ms/5,queroCarona:5,3ms/1,melhorEmbarque.longRangeMap:2,5ms/1,turnChainedCostMap:1,3ms/1,melhorEmbarque.transporterPaths:1,3ms/1,melhorEmbarque.candidateCells:0,9ms/1 metrics=CaptureClaimPublishedHits:1,CellsVisited:815,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:306,MelhorEmbarqueDestinationCostMaps:1,MelhorEmbarqueEmbarkProbeSkips:71,MelhorEmbarqueEmbarkProbes:43,MelhorEmbarqueLongRangeMapBuilds:1,MelhorEmbarqueLzGateProbes:306,MelhorEmbarqueLzGateRejects:192,MelhorEmbarquePairs:114,MelhorEmbarquePairsNoRoute:71,MelhorEmbarquePairsReachableLater:12,MelhorEmbarquePairsReachableNow:9,MelhorEmbarquePairsStrategic:22,MelhorEmbarquePassengers:1,MovementCacheHits:5,MovementCacheMisses:4,MovementCacheStores:4,MovementCostCellsExpanded:457,MovementCostWaves:4,MovementQueryCachesBuilt:334,MovementQueryConfirmedOccupancyUses:334,MovementWavesBuilt:4,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:508,TopologyCellsVisited:306,TopologyIndexCandidateCells:306,TopologyIndexHits:1,TopologyIndexQueries:1,TurnChainedCellsExpanded:52,ValidPathCacheHits:5

[AI AMARELO][T8][Capturador] 51 QueroCarona=SIM contexto=ComPlano setor=India origemAlvo=reserva emergencia=False envelope=BeyondOperational custo=- alvo=(14, 16, 0) motivo=Infantaria nao alcanca alvo do plano India Metropole@(14, 16, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

ACCEPT pax=#51 slot=0 carona=Requested ajuste=1700 fila=2t motivo=Infantaria nao alcanca alvo do plano India Metropole@(14, 16, 0) em Tactical ou Operational SEM ROTA PRÓPRIA (só chega de carona): aceita carona.

LZ=(10, 5, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=4 rotaTatica=4

LZ=(11, 4, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=4 rotaTatica=4

LZ=(11, 6, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=4 rotaTatica=4

LZ=(9, 5, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(10, 3, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(10, 4, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(10, 6, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(11, 2, 0) tier=Tactical paxAgora=1 opcoes=1 distTransport=5 rotaTatica=5

LZ=(8, 5, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(9, 4, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(9, 6, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(10, 1, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZ=(11, 0, 0) tier=Operational paxAgora=1 opcoes=1 distTransport=6 rotaTatica=-1

LZs sem passageiro ReachableNow=101; preservadas apenas no ranking plano.

[AI AMARELO][T8][Capturador] 51 MelhorEmbarque nao encontrou encontro terrestre materializavel (opcoes=114).

[AI AMARELO][T8][Capturador] 51 sem trabalho a pe: avanca via (10, 6, 0) para a cabeca de praia (10, 9, 0) (dist 7h do alvo (14, 16, 0); espera a carona la)

[AI Review] #3 pendente: Soldado #51 vai apenas mover de (9,3) para (10,6). Decisão: aguarda carona na cabeca de praia.

[FrameSpike] frame=2152 duration=1629,86ms state=Neutral substep=AwaitingAction selected=(none) boardRev=294 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=1018,0MB managedDelta=+97,3MB gcDelta=[0,0,0] unityAlloc=854,6MB

[RangeCache] MISS - reason: empty key | unit=Soldado_T1_U51 unitId=-299958 mp=3 fuel=65 rev=294

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Soldado_T1_U51 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Soldado_T1_U51

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Soldado_T1_U51

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Soldado_T1_U51

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Soldado_T1_U51

moveu para 10,6

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U51

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Soldado_T1_U51 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Review] #3 Batch terminou: sucesso=True; caixa 25070->25070. Consulte JogadasLog para efeitos de combate/captura.

[AI Commit Light][T8][slot=1][amarelo] reason=phase2:Soldado#51 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T8][Yellow] Soldado#51 action=move decision=1480ms execution=2731ms snapshot=0ms delay=284ms total=4495ms stages=validPaths:1115,1ms/7,melhorEmbarque:331,1ms/1,melhorEmbarque.lzLoop:278,1ms/1,melhorEmbarque.lzGates:251,9ms/1,melhorEmbarque.transporterPaths:48,3ms/1,melhorEmbarque.resolveMeeting:18,3ms/1,movementCostMap:9,0ms/4,queroCarona:4,6ms/1,melhorEmbarque.passengerReach:4,2ms/1,melhorEmbarque.longRangeMap:2,6ms/1,turnChainedCostMap:1,3ms/1,melhorEmbarque.candidateCells:0,0ms/1 metrics=CaptureClaimPublishedHits:1,CellsVisited:3436,MelhorEmbarqueCalls:1,MelhorEmbarqueCandidateCells:306,MelhorEmbarqueDestinationCostMaps:1,MelhorEmbarqueEmbarkProbeSkips:71,MelhorEmbarqueEmbarkProbes:43,MelhorEmbarqueLongRangeMapBuilds:1,MelhorEmbarqueLzGateProbes:306,MelhorEmbarqueLzGateRejects:192,MelhorEmbarquePairs:114,MelhorEmbarquePairsNoRoute:71,MelhorEmbarquePairsReachableLater:14,MelhorEmbarquePairsReachableNow:13,MelhorEmbarquePairsStrategic:16,MelhorEmbarquePassengers:1,MovementCacheHits:4,MovementCacheMisses:7,MovementCacheStores:7,MovementCostCellsExpanded:458,MovementCostWaves:4,MovementQueryCachesBuilt:386,MovementQueryConfirmedOccupancyUses:386,MovementWavesBuilt:7,PathStatesExpanded:2617,QueroCaronaCacheMisses:1,QueroCaronaCacheStores:1,QueroCaronaCalls:1,QueroCaronaCaptureReachBuilds:1,QueroCaronaMobilityComponentHits:1,ReachableCellsProduced:691,TopologyCellsVisited:306,TopologyIndexCandidateCells:306,TopologyIndexHits:1,TopologyIndexQueries:1,TurnChainedCellsExpanded:55,ValidPathCacheHits:4,ValidPathWaves:3

[AI Reach][Transport:30:Courier] Tactical:hit budget=6 action=(8, 12, 0) target=(8, 12, 0) score=90000 reason=carga embarcada count=2 dist=6

[TransportOps][Unit#30][Courier][Tactical] hit carga embarcada count=2 dist=6

[AI AMARELO][T8][Transporte] heli 30 courier — passageiro #8 alvo=(8, 12, 0) dist=6h

[AI AMARELO][T8][Transporte] 30 atraca no alvo de captura (8, 12, 0): única saída por perto; larga ao lado e sai no turno seguinte

[AI AMARELO][T8][Transporte] heli 30 courier — MP=6 grounded=False domain=Air paths=127 occupied=21

[AI AMARELO][T8][Transporte] alvo conjunto por missao: passageiro #8 intent=Capture -> (8, 12, 0).

[AI AMARELO][T8][Transporte] alvo conjunto rogue: passageiro #42 -> (3, 13, 0).

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 7, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 7, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U39.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 7, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 6, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 6, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 8, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 7, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 7, 0) REJECT reason=sensor_no_option (Hex bloqueado por Anti Arcraft Artilery_T1_U53.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 6, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U51.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 8, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 5, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U52.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 9, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U16.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 9, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 5, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 9, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 7, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 8, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U48.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 9, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 5, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 6, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 5, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 4, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 4, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 4, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 4, 0) REJECT reason=sensor_no_option (Nenhum spot valido para os passageiros.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 7, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U63.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 9, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=sensor_no_option (Hex bloqueado por Surface Air Missile_T1_U70.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 5, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 4, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 3, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 3, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 3, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=sensor_no_option (Hex bloqueado por Apache_T1_U68.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 7, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 8, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 4, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 3, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 2, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 2, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 2, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=sensor_no_option (Hex bloqueado por Apache_T1_U71.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 2, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 12, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 12, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 12, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 7, 0) REJECT reason=delivered_zero sensorOptions=2 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 8, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 12, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U9.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U69.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 4, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 10, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U34.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 3, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 11, 0) REJECT reason=sensor_no_option (Hex bloqueado por Anti Arcraft Artilery_T0_U38.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 2, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 12, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U25.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 1, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 1, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 13, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 13, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U6.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 13, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 13, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 13, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 13, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 13, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U1.)

[AI Reach][TransportDelivery:30] Tactical:miss budget=6

[AI Reach][TransportDelivery:30] Operational:disabled

[AI Reach][TransportDelivery:30] Strategic:disabled

[AI Reach][TransportDelivery:30] Tactical:disabled

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 7, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 7, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U39.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 7, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 6, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 6, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 8, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 7, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 7, 0) REJECT reason=sensor_no_option (Hex bloqueado por Anti Arcraft Artilery_T1_U53.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 6, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U51.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 8, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 5, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U52.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 9, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U16.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 9, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 5, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 9, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 7, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 8, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U48.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 9, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 5, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 6, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 5, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 6, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 4, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 4, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 4, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 4, 0) REJECT reason=sensor_no_option (Nenhum spot valido para os passageiros.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 7, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 8, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 6, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U63.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 9, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 9, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 5, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 4, 0) REJECT reason=sensor_no_option (Hex bloqueado por Surface Air Missile_T1_U70.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 5, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 4, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(8, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 3, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 3, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 3, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(9, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 3, 0) REJECT reason=sensor_no_option (Hex bloqueado por Apache_T1_U68.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 7, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 8, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 8, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 4, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 4, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 3, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 2, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 2, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 2, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 2, 0) REJECT reason=sensor_no_option (Hex bloqueado por Apache_T1_U71.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 2, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 12, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 12, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 12, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 2, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 12, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 7, 0) REJECT reason=delivered_zero sensorOptions=2 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 7, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 8, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 6, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 5, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 4, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 3, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 12, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U9.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 2, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U69.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 4, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 10, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U34.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 3, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 11, 0) REJECT reason=sensor_no_option (Hex bloqueado por Anti Arcraft Artilery_T0_U38.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 2, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 12, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U25.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 1, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 1, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 1, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 13, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 13, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U6.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 13, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 13, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 13, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 13, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 13, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U1.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 5, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 10, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 4, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 5, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 3, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 12, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 2, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 13, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 4, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 10, 0) sensorOptions=2 matching=1 move=7 rotaTotal=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 11, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 11, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 3, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U37.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 11, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U4.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 2, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 12, 0) sensorOptions=6 matching=2 move=7 rotaTotal=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 12, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 13, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 13, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 12, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 13, 0) target=(3, 13, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 13, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 1, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 13, 0) sensorOptions=6 matching=2 move=7 rotaTotal=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 13, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 12, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 14, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 13, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 12, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 14, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 0, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(10, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 0, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(11, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(12, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 0, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 0, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 0, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 14, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 14, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 14, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 14, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 14, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U32.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 14, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 14, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U33.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 14, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U14.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 11, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 12, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 2, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 13, 0) REJECT reason=delivered_zero sensorOptions=12 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 14, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 5, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 4, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 10, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U45.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 3, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 11, 0) sensorOptions=6 matching=2 move=8 rotaTotal=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 11, 0) target=(8, 12, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 12, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 10, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 12, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 10, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 2, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(7, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(5, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(7, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(5, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 12, 0) sensorOptions=6 matching=2 move=8 rotaTotal=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 12, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 13, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 11, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 12, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 13, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 11, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 1, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(7, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(7, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(7, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(7, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 13, 0) sensorOptions=8 matching=2 move=8 rotaTotal=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 13, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 12, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 14, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 12, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 13, 0) target=(3, 13, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 12, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 14, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 12, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 0, 0) REJECT reason=delivered_zero sensorOptions=2 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 14, 0) sensorOptions=8 matching=2 move=8 rotaTotal=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 13, 0) target=(8, 12, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 15, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 13, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 15, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 13, 0) target=(3, 13, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 15, 0) target=(3, 13, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 13, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 15, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 15, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 15, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 15, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 15, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 15, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 15, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(13, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(13, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 15, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 15, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(14, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(14, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 15, 0) sensorOptions=4 matching=2 move=8 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 15, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(7, 14, 0) target=(8, 12, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 15, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(7, 14, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 13, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 12, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 1, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 14, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 13, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 15, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(15, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 5, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U58.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 4, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 10, 0) sensorOptions=6 matching=2 move=9 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 10, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 11, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 11, 0) target=(8, 12, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 3, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(5, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(5, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 11, 0) sensorOptions=6 matching=2 move=9 rotaTotal=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 11, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 11, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 10, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 11, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 2, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U41.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 12, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 1, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(6, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 13, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 14, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 15, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(12, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(11, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(13, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(10, 16, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(14, 16, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U13.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(9, 16, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(15, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(8, 16, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(16, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(7, 16, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U47.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 15, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(16, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(17, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(17, 16, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 6, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 5, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U27.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 4, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 10, 0) sensorOptions=6 matching=2 move=10 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 10, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 11, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 11, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 3, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 11, 0) sensorOptions=8 matching=2 move=10 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 11, 0) target=(8, 12, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 11, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 10, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 10, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 2, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 12, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 1, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(5, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 13, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 14, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 15, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U59.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(6, 16, 0) sensorOptions=4 matching=2 move=10 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 16, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 15, 0) target=(8, 12, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 15, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(0, 7, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 5, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 4, 0) REJECT reason=delivered_zero sensorOptions=4 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 10, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T0_U5.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 3, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 4, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 11, 0) sensorOptions=8 matching=2 move=11 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(1, 11, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 11, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 12, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 10, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(1, 11, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 12, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 10, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 2, 0) REJECT reason=delivered_zero sensorOptions=10 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 12, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 1, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(4, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 13, 0) sensorOptions=4 matching=2 move=11 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 13, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 14, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 13, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 14, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 0, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 14, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 15, 0) sensorOptions=6 matching=2 move=11 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 15, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 16, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 16, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(5, 16, 0) sensorOptions=6 matching=2 move=11 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 16, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(6, 16, 0) target=(8, 12, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 15, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(6, 16, 0) target=(3, 13, 0) route=4

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(0, 6, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(0, 8, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(0, 5, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(0, 9, 0) REJECT reason=sensor_no_option (Unidade nao possui as skills exigidas para pouso neste contexto.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 4, 0) REJECT reason=sensor_no_option (Contexto atual nao permite pouso.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 10, 0) REJECT reason=transporter_cell_not_allowed

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 3, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U21.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(1, 11, 0) sensorOptions=4 matching=2 move=12 rotaTotal=8

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 11, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 12, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 12, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 2, 0) REJECT reason=delivered_zero sensorOptions=6 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 3, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 12, 0) sensorOptions=6 matching=2 move=12 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 11, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 13, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(1, 11, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 11, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 13, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(1, 11, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 1, 0) REJECT reason=delivered_zero sensorOptions=8 (nenhum passageiro chega ao alvo dele a partir das celulas de desembarque)

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#8 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 1, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 0, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(2, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]     REJECT pax=#42 spot=(3, 2, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(2, 13, 0) sensorOptions=8 matching=2 move=12 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 13, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 12, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(2, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 14, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 13, 0) target=(3, 13, 0) route=0

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 12, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 14, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 14, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 0, 0) REJECT reason=sensor_no_option (Hex bloqueado por Soldado_T1_U40.)

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 14, 0) sensorOptions=10 matching=2 move=12 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(2, 14, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 13, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(2, 13, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(2, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 15, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 14, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 13, 0) target=(3, 13, 0) route=0

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 13, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(3, 15, 0) sensorOptions=8 matching=2 move=12 rotaTotal=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 15, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   REJECT pax=#8 spot=(2, 15, 0) reason=route_or_limit

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 16, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 14, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(2, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 14, 0) target=(3, 13, 0) route=1

[AI AMARELO][T8][Transporte][MelhorDesembarque] LZ=(4, 16, 0) sensorOptions=6 matching=2 move=12 rotaTotal=7

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(5, 16, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(4, 15, 0) target=(8, 12, 0) route=5

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#8 spot=(3, 15, 0) target=(8, 12, 0) route=6

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(5, 16, 0) target=(3, 13, 0) route=3

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(4, 15, 0) target=(3, 13, 0) route=2

[AI AMARELO][T8][Transporte][MelhorDesembarque]   ACCEPT pax=#42 spot=(3, 15, 0) target=(3, 13, 0) route=2

[AI Reach][TransportDelivery:30] Operational:hit budget=12 action=(7, 12, 0) target=(8, 12, 0) score=199593 reason=2p/1p

[AI][Progressao2][Top] unit=30 intent=TransportDelivery from=(11, 7, 0) target=(7, 12, 0) best=(8, 10, 0) final=94000 candidatos=102 skips origin=1 occupied=24 stop=0 allow=0 score=0
  #1 (8, 10, 0) final=94000 tool=100 next=0,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=0,5 threat=120,0 dpq=1,0 tactical=0
  #2 (8, 11, 0) final=93500 tool=100 next=0,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=0,2 threat=130,0 dpq=1,0 tactical=0
  #3 (9, 12, 0) final=91000 tool=98 next=0,0 move=5 road=False prog=5,0/7,0 route=? progR=? line=1,6 threat=140,0 dpq=1,0 tactical=0
  #4 (9, 10, 0) final=90000 tool=94 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=0,3 threat=80,0 dpq=1,0 tactical=0
  #5 (8, 9, 0) final=90000 tool=93 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=1,1 threat=60,0 dpq=1,0 tactical=0
  #6 (7, 9, 0) final=87500 tool=92 next=0,0 move=5 road=False prog=4,0/7,0 route=? progR=? line=1,9 threat=90,0 dpq=1,0 tactical=0
  #7 (9, 11, 0) final=87500 tool=93 next=0,0 move=4 road=False prog=4,0/7,0 route=? progR=? line=0,9 threat=110,0 dpq=1,0 tactical=0
  #8 (10, 12, 0) final=87000 tool=92 next=0,0 move=5 road=False prog=4,0/7,0 route=? progR=? line=2,3 threat=100,0 dpq=1,0 tactical=0
  #9 (9, 13, 0) final=86500 tool=92 next=0,0 move=6 road=False prog=4,0/7,0 route=? progR=? line=2,2 threat=110,0 dpq=1,0 tactical=0
  #10 (6, 9, 0) final=85500 tool=91 next=0,0 move=6 road=False prog=4,0/7,0 route=? progR=? line=2,7 threat=110,0 dpq=1,0 tactical=0
  #11 (7, 8, 0) final=83500 tool=86 next=0,0 move=5 road=False prog=3,0/7,0 route=? progR=? line=2,5 threat=50,0 dpq=1,0 tactical=0
  #12 (6, 8, 0) final=81000 tool=85 next=0,0 move=6 road=False prog=3,0/7,0 route=? progR=? line=3,3 threat=80,0 dpq=1,0 tactical=0

[AI AMARELO][T8][Transporte] Aereo 30 preserva grupo 1p e progride (11, 7, 0)->(8, 10, 0) rumo a LZ conjunta (7, 12, 0) score=189 tier=Operational (toolIntent=TransportDelivery tool=100 next=0,0 moveCost=5 roadBonus=False prog=5,0/7,0 route=? progR=? line=0,5 dpq=1,0 threat=120,0 tactical=0 final=94000).

[AI Review] #4 pendente: Chinook #30 vai apenas mover de (11,7) para (8,10). Decisão: AI Move 30 â†’ (8, 10, 0).

[FrameSpike] frame=2336 duration=3355,31ms state=Neutral substep=AwaitingAction selected=(none) boardRev=369 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=865,4MB managedDelta=+43,9MB gcDelta=[1,1,1] unityAlloc=852,2MB

[FrameSpike] frame=2517 duration=5309,18ms state=Neutral substep=AwaitingAction selected=(none) boardRev=369 replay=False aiTurn=True aiInputLock=False turnTransition=False movementAnimating=False gameplayInputBlocked=False managed=827,8MB managedDelta=+0,4MB gcDelta=[0,0,0] unityAlloc=852,0MB

[RangeCache] MISS - reason: empty key | unit=Chinook_T1_U30 unitId=-295172 mp=6 fuel=36 rev=369

[FSM] Estado: Neutral -> UnitSelected

[TurnState] transition=Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId | selected=Chinook_T1_U30 | stack=Neutral > UnitSelected

[FSM][Enter] Neutral -> UnitSelected | reason=TryAutomatedSelectUnitByInstanceId

[TurnState] state=UnitSelected | step=HandleConfirm | selected=Chinook_T1_U30

[TurnState] state=UnitSelected | step=HandleConfirmWhileUnitSelected | selected=Chinook_T1_U30

[TurnState] state=UnitSelected | step=BeginMovementToSelectedCell | selected=Chinook_T1_U30

[TurnState] state=UnitSelected | step=HandleMovementAnimationCompleted(target=MoveuAndando) | selected=Chinook_T1_U30

moveu para 8,10

[TurnState] state=UnitSelected | step=EnterSensorsState(anchor=MoveuAndando) | selected=Chinook_T1_U30

[FSM] Estado: UnitSelected -> MoveuAndando

[TurnState] transition=UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando) | selected=Chinook_T1_U30 | stack=Neutral > UnitSelected > MoveuAndando

[FSM][Enter] UnitSelected -> MoveuAndando | reason=EnterSensorsState(anchor=MoveuAndando)

[FSM] Estado: MoveuAndando -> Neutral

[TurnState] transition=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral | selected=(none) | stack=Neutral

[FSM][Reset] previous=MoveuAndando -> Neutral | reason=ClearSelectionAndReturnToNeutral

[Acao] Apenas Mover ("M") confirmado. Unidade finalizou sem atacar.

[AI Review] #4 Batch terminou: sucesso=True; caixa 25070->25070. Consulte JogadasLog para efeitos de combate/captura.

[AI Commit Light][T8][slot=1][amarelo] reason=phase2:Chinook#30 fogBarrier=Unavailable removed=0

[AI Perf][Unit][T8][Yellow] Chinook#30 action=move decision=3268ms execution=16391ms snapshot=0ms delay=286ms total=19945ms stages=validPaths:1760,8ms/24,toolProgression.TransportDelivery:1313,1ms/1,melhorDesembarque:1214,7ms/2,movementCostMap:13,7ms/7,transportPlanning:6,7ms/1 metrics=CellsVisited:6968,DisembarkLzCellsVisited:408,MelhorDesembarqueCalls:2,MelhorDesembarquePassengerRouteBuilds:4,MelhorDesembarqueStructuralLzCandidates:612,MovementCacheBypasses:12,MovementCacheHits:8,MovementCacheMisses:23,MovementCacheStores:11,MovementCostCellsExpanded:391,MovementCostWaves:7,MovementQueryCachesBuilt:125,MovementQueryConfirmedOccupancyUses:48,MovementQueryLiveOccupancyFallbacks:77,MovementWavesBuilt:23,PathStatesExpanded:6169,ReachableCellsProduced:2303,ToolProgressionCubicDirectionUses:1,TopologyIndexHits:2,TopologyIndexQueries:2,TransportPlanningCalls:1,ValidPathCacheHits:8,ValidPathWaves:16

[AI AMARELO][T8][AirCombat] 50 Tactical scan: cells=66 prunedByCubic=48 sensorCalls=18 candidates=22 visibleEnemies=31

[AI AMARELO][T8][AirCombat] 50 rogue ataca via (13, 15, 0) -> Soldado#13 (score=125244 pref=Primary value=60000 posture=-40 weapon=CG hp=10 noMove=False combatScore=28300 kill=False dmg=6/60% loss=1/10% capturaNegada=6 capturando=True prioridade=atrasarCaptura atkDecision hp=10->9 loss=10%/80% dmg=60%/10% target=10->4 dpq=1/2 def=0/2 kill=False survive=True OK damage)

[AI AMARELO][T8] Fase2 — Apache#50 NAO cedeu ataque em Soldado#13 (nenhum amaciamento de artilharia):
  Anti Arcraft Artilery#53: sensor-nao-mira-alvo-de-nenhuma-celula
  Surface Air Missile#70: indisponivel (acted=True dead=False embarked=False)


[AI Review] #5 pendente: Apache #50 vai atacar Soldado #13 no hex (14,16), movendo de (13,14) para (13,15).

