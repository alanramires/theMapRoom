# Plano da D2 — prontidão do rally

Estado: implementação da D2 concluída em 2026-11-11, aguardando revisão em Play.
Abaixo permanece o bloco do autor, com a nota naval e a matriz por mapa; modelo/bandeira no mar continuam no degrau 7.

## Nota do autor (2026-11-11) — embarcado é Ocupação; a Marinha bombardeia e espera

"Ruptura sempre vai ter (blindados), porém quando eles embarcam, eles passam a ser a equipe de
ocupação (pois estão embarcados)." Regra para a contagem: **unidade embarcada conta como carga da
Ocupação**, uma vez, pela ficha do transporte que a leva; só conta como Ruptura quem está a pé ou
no ar. Na vanguarda ficam os que podem atuar ali; vanguarda vazia não é ausência de Ruptura.

Operação naval (degrau 7, fora desta D2, registrado para não se perder): os blindados embarcados
"não podem ficar na frente da linha da marinha, pois os navios estão bombardeando a praia,
esperando o resto das forças navais chegarem". O core naval bate com **4 de fogo de suporte naval
e 4 unidades embarcadas (ou 2 transportadores carregados)**; aí os transportes vão à praia e
desembarcam blindados e capturadores, que formam a vanguarda em terra; os navios que carregam
artilharia só vão depois que a vanguarda na praia estiver consolidada. O destroyer segue Dissuasão;
ninguém inventa couraçado nem converte suporte em Ruptura.

O piso ruptura >= 2 fica: todo catálogo de batalha oferece blindado ou aeronave de Ruptura.

## Plano completo do autor

D2 — Prontidão do rally (degrau 3 da proposta), em AIController.PlanEvaluator.RallyPoints.cs
Dono da pergunta: EvaluateRallyReadiness. Nada de função paralela; a tela já lê dele.
1. Ruptura = soma de GetRallyAssaultWeight de toda unidade da Força de Ruptura contada no core, por peso: blindado terrestre pelo peso atual (Tanque 1, MBT 2,5), Apache 1, Caça B 1, Caça A/F 1. IsRallyPrimaryAssaultUnit deixa de exigir Domain.Land/Armored: a condição é Força de Ruptura. Some IsOperationalRallyAirAttackUnit nessa conta (munição e combustível continuam valendo) e apague AirAttack como contagem separada.
2. Suporte = soma de GetRallyArtilleryWeight de toda unidade da Força de Dissuasão no core (artilharia pelos pesos atuais; Bombardeiro, AAA, SAM, Artilheiro Combatente, destroyer entram com 1 por padrão). Soma no ForceScore e no HUD, nunca gateia: apague hasArtillery, hasLocalLaunchArtillery, RallyMinimumArtillery, RallyMinimumLocalLaunchArtillery, GetRallyArtilleryRequirement, overwhelmingBreakthrough e o status ASSEMBLING_WAIT_ART. BuildRallyMissingText perde artGlobal/artLocal.
3. Piso fixo: Capturers >= 4 e ruptura >= 2. RequiredPackages e KnownEnemyForce / 8 saem.
4. Inimigo local: inimigo conhecido pela névoa confirmada do slot cuja célula está no Operacional do core (união dos Operacionais dos participantes do core, ou o Operacional do modelo a partir do rally, o que já estiver calculado) no sentido do alvo (eixo do slot). avança se piso E (força do core >= razão × força inimiga local), razão lida do AIPresetData em campo novo rallyLocalRatio (Fácil 1,2; Médio 1,5; Difícil 2,0), num [Header("Rally")]. Sem inimigo local conhecido, piso basta.
5. No core / em aproximação pela banda da unidade: conta no core se a célula do rally está no Tático dela (UnitReachEnvelopeService, intenção de movimento, banda Tactical), em aproximação se no Operacional. Embarcado conta se o transporte conta, **e conta como Ocupação (carga), nunca como Ruptura nem Dissuasão** (ver a nota do autor acima; um tanque dentro de um Chinook não soma no piso de ruptura). Apague RallyAssemblyForceRadius, RallyAirAttackRadius, RallyArtilleryRadius, RallyAirSurveillanceRadius, RallyLogisticsRadius. Reaproveite o cache do envelope; a conta roda uma vez por turno no planner.
6. Saturação no lugar de RallyAssemblyTimeoutTurns: grave no SectorObjective o turno em que o piso foi cumprido e a força do turno anterior. Saturou quando (piso cumprido e força não cresceu "de verdade" por 2 turnos: crescer é +1 ponto de ruptura ou +2 pontos no total) ou (4 turnos desde o piso). Saturado = avança mesmo com a razão local aberta.
7. Capitão: durante a montagem o capitão do montinho é o próprio rally; ao avançar (GoGreen), o capitão passa a ser o capturador que estava no Tático do rally no turno do GoGreen (grave o InstanceId no objetivo). Quem orbita o capitão do rally hoje (FireSupport, assalto de rally, transportes do UpdateInvasionMonitor) lê esse campo em vez de recalcular. ESPERANDO: o montinho entra em espera quando o capitão sai do Tático de um participante ainda a caminho, e o capitão para até o retardatário voltar a tê-lo no Tático. Reaproveite o monitor de invasão existente; não crie um segundo laço.
8. Tela aérea: em HasAlliedCombatScreenAheadOfDrop, unidade da Força de Ruptura de domínio aéreo cujo Tático alcança a LZ conta como tela, com o mesmo peso de um blindado.
9. Mantidos: numericalDominance 2:1, macroDominating, "um montinho libera todos" (BuildRallyAggregate), ativação pelo eixo. O modelo naval e a bandeira no mar não entram.
10. Fixtures: estenda verificar_tela_rally.cs ou crie verificar_prontidao_rally.cs com o caso do mapa de ilhas (Golf: 3 soldados, 2 Chinook com 2 soldados a bordo, Apache e Caça B no Tático do rally, zero artilharia, 36 inimigos no mapa e 2 no Operacional local): hoje ASSEMBLING_WAIT_ART, depois GO_GREEN. Mais: um soldado chegando por turno não impede a saturação em 4 turnos; Bombardeiro no core soma suporte e não gateia; 3 capturadores não cumprem piso. Regressões: verificar_desembarque_rally_aereo, shopping, políticas sem plano.
11. Atualize docs/rally/rally_proposta.md só na tabela "o que muda em relação a hoje" marcando o que entrou; a doutrina consolidada fica para a skill. Diário do dia com o que só compilou. Pare depois do commit da D2 para revisão.
## Matriz por tipo de mapa (autor, 2026-11-11)

"Exército sempre vai ter; não tem jogo sem exército." A Força de cada unidade não muda; o que muda
por mapa é **quem está disponível** e, no mar, **quem está embarcado** (embarcado = Ocupação).

| mapa | Ruptura | Dissuasão | Ocupação | Apoio |
|---|---|---|---|---|
| só exército | Assalto | Fogo de Suporte | Capturadores, Transporte | Logística, Estoque (Vigilância não usada) |
| exército + aeronáutica | Assalto, Interceptadores, Disruptores | Fogo de Suporte, Ataque ao Solo | Capturadores, Transporte | Logística, Estoque, Vigilância (se FOW) |
| exército + marinha | **não tem** (Assalto só quando desembarca) | Fogo de Suporte Naval (destroyers) | Capturadores, Transporte | Logística, Estoque, Vigilância naval (subs, fragatas); Porta Aviões não usado |
| exército + aeronáutica + marinha, desembarque naval | Interceptadores, Disruptores (Assalto só no desembarque) | Fogo de Suporte Naval, Ataque ao Solo (Fogo de Suporte após desembarque) | Capturadores, Transporte | Logística, Estoque, Vigilância |
| exército + aeronáutica + marinha, arquipélago ou continente | Interceptadores, Disruptores, Assalto | Fogo de Suporte Naval, Ataque ao Solo, Fogo de Suporte | Capturadores, Transporte | Logística, Estoque, Vigilância |

Consequência para a prontidão: o piso "ruptura >= 2" vale onde a Ruptura pode estar **a pé ou no
ar** no rally (linhas 1, 2, 4 e 5). Num rally no mar (linha 3), a Ruptura está embarcada e conta
como Ocupação; o core é o da nota naval acima (4 de fogo de suporte naval + 4 embarcados ou 2
transportes carregados). Isso é o degrau 7; a D2 cobre rally em terra.


## Correção do autor sobre `53dce5f` — presença e inimigo local (2026-11-11)

Esta decisão substitui a união dos Operacionais dos participantes do item 4 e o teste por alcance
para aeronaves do item 5. O inimigo local é medido no **Approach do modelo terrestre**, a partir
do rally, preservando névoa confirmada e direção do eixo.

Presença terrestre continua pela própria banda. Para aeronave, inclusive o transporte de uma
unidade embarcada: Tático é célula em **Core ∪ Vanguard**; Operacional é célula em **Approach**.
Poder voar até o rally não é estar reunido nele. A mesma `RallyArea` de `RallyAreaService` é
cacheada por rally/slot e `GlobalBoardRevision`, compartilhada por planner, painel e entrega.

`IsRallyParticipantPresent` responde presença no rally; `IsRallyParticipantInBand` continua para
coesão em relação ao capitão e cobertura da LZ, onde a banda própria permanece correta.
