# v10.8.0 — O rebelde era o rogue

**Data:** 2026-11-10. Tag `v10.8.0`. Major v10 — Cada mapa, a mesma IA.

Um dia inteiro de Codex implementando e Claude revisando, em cinco commits, um por frente.
Nenhuma linha foi vista em Play: tudo o que está aqui passou por `bash tools/compilar.sh` e
por fixtures rodadas no Editor fora do Play, via `unity command script eval`. A seção
"O que não terminou" diz o que ainda precisa de uma partida.

## 1. A descoberta que organizou o dia

O autor, ao pedir que a "AI rebel" fosse resolvida de vez:

> *"quando eu criei a AI da primeira vez, eu criei ela atrelado a planos, a rotas, objetivos,
> capturas, HQ e tal... então um dia eu quis criar um cenário, e criei uma subdivisão de tropas
> sem HQ... chamei esse ramo de rebelde, mas descobri meses mais tarde que rebelde é o mesmo que
> o rogue de uma AI com HQ."*

> *"o capturador com plano, preferência o plano... capturador sem plano, é magnético atraído
> pelos prédios... capturador rebel = capturador sem plano (rogue) e o conceito de magnético
> surgiu pra fazer essa tradução da ai rebel de volta pra rogue."*

Isso dá a régua para cada `isRebel` que sobrou: **o magnético já responde?** Se sim, a porta cai.
Se não, o magnético está incompleto e é ele que ganha o caso. Nunca um terceiro caminho.

O que de fato pertence à **facção** sem QG, e não à unidade sem plano, ficou reduzido a três
fatos, todos fora da IA: ela não tem base para o shopping (`HasShoppingBase`), ela captura sem
pré-requisito de progressão (`MatchController.CanCaptureConstruction`) e ela nunca é dona do
que ocupa (`firstOwnerSlotIndex`). O resto é "tem vaga no plano?", e a IA com QG já sabia
responder isso para os seus rogues.

A `pergunte-me` fechou onze decisões; as frases do autor que valem como doutrina:

- compra: *"a facção sem QG se comporta igual a facção com QG com prédios capturados, deve
  respeitar a selling rules do construction manager... se lá diz 'first owner' fica uma
  ressalva, pq por não ter HQ ela não é o dono de factum, ela só está ocupando"*;
- shopping: *"o core 2/2/1 não se aplica à facção sem HQ"*;
- fim de partida: *"a AI rebelde eliminada não trigga o fim da partida, a menos que não haja
  outros oponentes"*; e a rebelde que captura um QG *"se torna AI com HQ"* pela derivação;
- cenários: a imunidade a 0 unidades existia *"pq existem cenários onde inimigos só aparecem lá
  pro turno 4 ou 5"*; a proteção passou a ser do cenário (`UsesMatchEndRules`,
  `allowDefeatForZeroUnits` no `QuadranteData`), não da facção.

## 2. Fatia A — partida e propriedade (`56980fe`)

- Compra pela `sellingRule` da instância, para qualquer facção: FreeMarket compra, OriginalOwner
  só trava, FirstOwner grava o primeiro captor **com** QG; o time sem QG nunca escreve
  `firstOwnerSlotIndex` nos três setters de dono. `ConstructionData.allowRebelAIPurchase` e a
  seção "AI Behavior" do Inspector morreram. **Atenção:** a flag estava ligada em Barracks,
  Docas, Garagem APC, Hidrobase e Terminal Rodoviário; nenhuma `sellingRule` de instância foi
  migrada. Cenário em que a rebelde comprava precisa de conferência das instâncias.
- `TryDefeatSlotIfZeroUnits` usa `MarkSlotDefeated` sem imunidade: 0 unidades derrota e
  neutraliza os prédios do slot. Captura de QG elimina só o dono anterior; vencedor só pelo
  `TryDeclareLastStandingWinner`.
- A6, achado na revisão: `HandleUnitDestroyed` e o início do turno ainda declaravam vitória
  imediata ao eliminar um exército, contradizendo "fim por último de pé". Ambos passaram pelo
  mesmo funil (`ArmyEliminated`); `DeclareEliminationVictory` ficou só para rendição.
- `allowDefeatForZeroUnits` subiu ao `QuadranteData` (catálogo diz o que a cena é; o quadrante
  diz a regra da partida dele), aplicado pelo `QuadranteController` e editável no Map Helper.
- Fixture `verificar_regras_sem_qg.cs`: três slots, quatro `sellingRule`, FirstOwner por slot e
  por time, promoção dinâmica, tutorial e turno zero protegidos, A6 com dois e três slots.

## 3. Fatia B — shopping sem base (`7e0dbfa`)

Caiu a porta `rebel-insurgency` (seis capturadores fixos). Sem base ficam três demandas
reativas, cada uma lendo quem já responde:

| demanda | dono da pergunta |
|---|---|
| capturador por capturável livre alcançável | `CaptureOpportunityClaimService.CountUnclaimedOpportunities` (desconta claims e missões a bordo) + chave do `PodeCapturarSensor` + acesso por ficha/produtora do `SectorManager` via `ArtilleryWallContext` |
| transporte pela fila de carona | `PressaoCaronaService.Read` (antes a porta recontava flags) |
| counter pela ameaça visível | a mesma `AddCounterPressureDemands` |

Pacote 2/2/1, progressão, reserva de elite, vigilância, rally, conscrição e bootstrap blindado
perguntam `HasShoppingBase` e se calam; compromisso de elite antigo é cancelado com motivo.
Fixture `verificar_shopping_sem_qg.cs`: dois prédios livres → dois capturadores; claim publicado
e passageiro embarcado descontam; ilha sem acesso e mercado Disabled não contam; QG religa o
pacote. Nota de nome: `ArtilleryWallContext` serve parede, rally e agora captura; débito já anotado
na v10.7.0.

## 4. Fatia C — portas por missão, não por facção (`3d8cdff`) e renomes (`dd3ce4e`)

| porta anterior | fato consultado agora |
|---|---|
| planner `IsSlotRebel` | `snapshot.MyHQ == null` limpa objetivos/eixo |
| ar `isRebel` (hunt só rebelde; interdição só com QG) | para todos: interdição → nega-escolta → presa Operacional → capitão; interceptador conserva a cadeia própria |
| assalto atrás de `plan != null` | assalto sem plano entra no papel pelo Router; capitão com objetivo → escolta da vanguarda dele; sem objetivo → capturável planless dele |
| reparo por facção | `hasRepairLine` da própria `TryMontarLinhaDeCombate`: sem linha não há retaguarda para exigir |
| courier/invasão/desembarque por facção | `AreAllPassengersPlanless` (toda a carga sem vaga infiltra; carga mista conserva GoGreen/ponte/screen) |
| destino do passageiro por facção | Mission Intent primeiro; capturador rogue usa o buscador planless; outros papéis, seus magnéticos |
| capturador por facção | `TryDecidePlanlessCapturerAction` atende todo capturador sem vaga, com ou sem QG, e chama `TryDecideCapturerAction(plan: null)` |

Único `IsHeadQuarterlessTeam` restante na IA: `HasShoppingBase`. `AIController.Rebel.cs` virou
`AIController.Planless.cs` com o GUID preservado; `QueroCaronaContext.RogueOuRebelde` virou
`SemPlano` no mesmo ordinal; categoria de log `Rebelde` → `SemPlano`.

**Delta que ninguém pediu de cara, anotado na revisão:** o capturador sem vaga da IA **com** QG,
sem capturável à vista, agora **caça** o inimigo visível mais próximo (`TryResolvePlanlessHuntAnchor`),
porque o plano chega nulo. Antes devolvia null e caía no HexEvaluator. Conferido que a lista
`RogueUnitIds` recebia todo capturador livre, então o gate antigo era equivalente; a caça é a
única diferença real. O autor aceitou como fallback: o soldado preso na ilha cai no Quero Carona
antes (o embarque vem antes do ramo rogue em `TryDecideCapturerAction`), e no endgame ele marcha
para onde há tropas. A âncora é fog-honesta: sem inimigo visível, fica parado.

Fixture `verificar_politicas_sem_plano.cs` e cinco regressões de interceptação, desembarque,
EVAC e swap: PASS.

## 5. Caça A — controlador dos céus (`4caddd5`)

Entrou antes das fatias, pela `pergunte-me` do Caça A (oito decisões):

- **Compra:** o Caça A nasce pela progressão de elite do Interceptador (Caça B em campo) até o
  nível 1, com gatilho "aeroporto inimigo produzindo ou aeronave vista"; terceira linha da
  reserva em paralelo (ar visível → caça antes do Assalto); não escala preventivamente ao F.
  `IsPreventiveAirControl` na demanda, sem identificar por texto do motivo. As fichas AR Caça A
  e F passaram de `Defensive` para `Either`: o autor preferiu mudar a ficha a abrir bypass de
  postura no código (*"defensive era a primeira maneira que eu criei... não ficou muito legal"*).
- **Posição sem presa:** pelo tempo de resposta (*"se o caça A estiver atrás da retaguarda e o
  EWACS longe detectando ameaças, o tempo de resposta será longo e o EWACS estará em perigo"*):
  cobertura conjunta de EWACS e vanguarda no Tático próprio, com peso de ameaça do
  `FindAirCombatAdvanceMove`; sem interseção, EWACS exposto, depois vanguarda; EWACS coberto não
  puxa; um caça por elo via `plannedEwacsEscorts`, limpo com `plannedDestinations`.
- `AIAirExposureService` extraiu guarda-chuva AA e exposição, compartilhados por shopping e
  posicionamento. **Efeito colateral a observar:** a consulta antiga não contava o SAM de alcance
  mínimo > 1 no guarda-chuva (subetapa errada); agora conta. Demandas de AA/caça de vanguarda e
  retaguarda vistas na v10.4.0 podem mudar.
- Revisões do Codex pegaram: cobertura sem filtro de ameaça, identificação por `Reason.Contains`,
  fallback ao HQ para EWACS coberto, falta do claim por elo.

## 6. O que não terminou

- **Nada deste dia foi visto em Play.** Prioridade: uma partida com facção sem QG (origem
  `capture-opportunity`, `sem QG: oportunidades livres=N`, fila de carona não vazia) e uma com
  QG olhando o rogue caçador e as demandas de AA após o SAM entrar no guarda-chuva.
- Instâncias de `sellingRule` nos cenários em que a rebelde comprava (Barracks, Docas, Garagem
  APC, Hidrobase, Terminal Rodoviário) não foram conferidas.
- `IsLogisticsReloadConstruction` perdeu o ramo `CanProduceUnits` para o time sem QG: caminhão
  sem QG só recarrega em supplier Hub. Aceito, não validado.
- Caça A: prevenção, poupança e postura em partida; os três perfis ponta a ponta; migração de
  compromisso antigo no ledger real.
- Herdado da v10.7.0, sem avanço hoje: shopping híbrido no mapa real, rally completo
  (`docs/rally/rally_proposta.md`), envolvidos rosa no AI Step, terceira refatoração só depois
  da Aeronáutica.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- `docs/AI Behavior/governanca_entre_papeis.md` §2.3, linha Interceptador: exposição em vez de
  proximidade, cobertura conjunta no Tático, EWACS exposto/vanguarda, um caça por elo. Só fixture.
- `docs/AI Behavior/Capturador.md` R1 e §10, `Assalto.md` S1/S9, `contrato_missao_captura.md`:
  "sem QG todos são rogues"; capturador sem vaga declara missão antes dos atalhos; assalto sem
  plano segue a vanguarda do capitão com objetivo. Só fixture.
- `docs/AI Behavior/Transporte.md`: carga toda sem vaga infiltra; carga mista conserva os gates. Só fixture.
- `docs/rascunhos/AI Shopping.md` e `docs/Planos/Reformular Shopping.md`: piso preventivo do
  Interceptador, gatilho de aviação, terceira linha da reserva, shopping sem base com três
  demandas reativas. Só fixture.
- `docs/manual/`: regras de partida (compra por `sellingRule`, FirstOwner só com QG, 0 unidades
  derrota, QG capturado elimina só o dono, `allowDefeatForZeroUnits` no quadrante). Só fixture.
- Carregados da v10.7.0: `Transporte.md` (EVAC/hospital, prédio+LZ, anel, entrega conjunta),
  `Assalto.md`/`Papeis.md`/`ficha_do_papel.md`/`magnetic_tabela.md` (Disruptor, aproximação
  operacional), `Logistica.md`, `Capturador.md` (swap, C6, herdeiro do blitz),
  `contrato_questionario.md`, `Vigilancia.md`, `FireSupport.md`.

Prontos para consolidar:

- Nenhum. Tudo que mudou hoje é "só compilou"; os débitos herdados não ganharam o segundo
  fechamento estável com validação em Play.
