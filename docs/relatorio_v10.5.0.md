# v10.5.0 — Ferido não é carga

2026-11-07 (noite). Continuação do dia da v10.4.0. O fio: o helicóptero que leva um ferido
não está fazendo uma viagem — está fazendo um **resgate**, e quase tudo que faltava era o jogo
tratar o ferido como alguém com destino próprio, não como carga a entregar no ponto do capitão.
O trabalho foi feito em dupla: o Codex escreveu o EVAC, eu revisei cada versão **antes** do
commit contra "procure a que já existe", e o autor decidiu a doutrina a cada passo.

---

## 1. O Disruptor tem uma ordem, não uma soma

`66ad89f`. Marca do autor (AI Step T8 amarelo): o Apache #49 matou um Soldado de 4 HP que estava
capturando, em vez de atacar o Chinook com dois capturadores frescos na frente dele. O autor:
*"a prioridade tem que ser interceptar poder de captura > quem está capturando > o que vier
depois"*.

O código tinha duas faixas, com "capturando" no topo, e dentro dela um score que soma o bônus de
abate (26000) ao dinheiro negado (10000 por ponto). O soldado de 4 HP que morre valia mais que o
Chinook que só toma dano. Agora são três faixas estritas (`ResolveDisruptorTargetTier`), no tiro e
na âncora: transporte com poder de captura a bordo > quem está capturando > o resto. Dentro da
faixa, o score de sempre.

**Visto em Play:** os dois Apaches foram no Chinook e o abateram. O segundo não terminou o
serviço "porque era vitória garantida", como o autor supôs: o Chinook com 5 HP ainda levava
capturador, então estava na faixa de cima, e o empate de 10×10 de poder de captura com o soldado
em terra nem chegou a ser comparado.

## 2. O EVAC aéreo — quatro revisões até o certo

`5248fff`, `21113a8`, `7c937bb` (Codex) e revisões minhas antes de cada commit.

**A primeira versão caiu na regra do dia.** O Codex escreveu a escolha do hospital como uma
varredura própria de todos os prédios, por fora do `FindRepairConstruction`. Funcionava no caso
da frente e perdia tudo o que o reparo já sabia: produtor fechado pela conscrição (o comentário
dizia até *"a producer may receive the patient"*), não-elite barrado da base sob pressão, reparo
pela linha (vivo desde `e72deff`), elite como último recurso. E rodava o sensor de desembarque em
volta de todo prédio do mapa. Pego na revisão antes do commit — o autor: *"é foda não poder
confiar na outra AI"*. Daí as diretrizes em `docs/resumo.md` e na skill de fechamento.

O que ficou, em camadas:

- **Hospital pela regra do reparo.** `FindRepairConstruction` virou um ranking reutilizável
  (`RankRepairConstructions`); o EVAC usa a lista, o reparo comum usa o primeiro dela. Um só dono
  da pergunta "onde este ferido se conserta" para APC, Chinook e navio — terrestre e naval também
  passaram a mandar `rejectBaseCluster` e `lineSnapshot`.
- **Par prédio + LZ.** O heli para **ao lado** do hospital, o ferido desce **no** prédio. O sensor
  só roda nas LZs alcançáveis nesta rodada, com cache. LZ e rota com ameaça são recusadas.
- **O anel do HQ é o último recurso e nunca falha** (doutrina do autor). Hospitais todos ocupados:
  o ferido desce na área segura em volta do HQ, mesmo em planície, e o heli volta à ação. A regra
  do anel é a mesma do `TryDecideRepairFallbackToHQ`, extraída em três funções. Com a base
  invadida (*"o Chinook nunca vai pousar"*) o anel cai em degraus: seguro → contestado de menor
  ameaça → aproxima/foge; nunca fica parado com a célula atual ameaçada.
- **EVAC conjunto.** Dois feridos a bordo descem juntos em células distintas, pelo pareamento do
  `MelhorDesembarqueService` (`MatchOptions`, sobre as opções já sondadas). Doutrina do autor:
  sem segunda vaga, o secundário **fica a bordo** e vira o principal da entrega seguinte (outro
  hospital próximo, ou o anel). **No anel do HQ não precisa ser estrito**: o segundo desce em
  terreno livre ao lado, se alcança a pé o ponto do principal, e vai andando — no degrau seguro,
  essa célula também precisa ser segura.
- **Custo vigiado.** O ranking auxiliar roda com `log:false`, as `repairCells` são montadas uma
  vez por decisão, e há contadores (`RepairConstructionRankings`, `EvacRepairCellsBuilds`,
  `AirEvacDisembarkProbes`) no `[AI Perf]`.

**Visto em Play:** EVAC com hospital livre — o Chinook pousou ao lado, o ferido desceu no prédio.
O resto (anel seguro/contestado, conjunto, dupla no anel) passou nos testes do Codex fora do Play
(`tools/ai_test/verificar_evac_predio_vago.cs`), com opções reais do sensor.

## 3. A cura vinha antes do diagnóstico

`017a097`. *"O Chinook tava com 1 HP e os soldados dentro com 2; quando começou a rodada foi
curado pra 3 e os soldados pra 4, e ele partiu pra viajar."*

O estado de reparo era decidido só no setup da Fase 2 — **depois** do Serviço do Comando. Os
soldados chegaram ao turno com 2 HP (gatilho de reparo: ≤ 3), foram curados para 4 no serviço e
chegaram à Fase 2 "sem ferimento". Pularam a faixa gatilho..recuperação inteira (só saem com 8).
Sem ferido marcado a bordo, o Chinook não virou EVAC. Agora a mesma `UpdateRepairState` roda no
começo da Fase 1, com o HP de chegada. Vale para **qualquer** unidade da IA curada pelo serviço.

**O Chinook não entra em reparo por HP, e isso é doutrina:** `repairTriggerHpBelow: 0` na ficha.
O autor: *"no Vietnã voavam na capa da gaita"* — só munição ou autonomia o recolhem.

## 4. A cruz no lugar do "T"

`f0654c0`, `36b65fe`. O autor queria bater o olho num helicóptero "voando estranho" e saber que é
EVAC. O indicador de transporte troca o "T" pela cruz branca (arte do autor, convenção dos
overlays) quando a unidade da IA leva ferido e o "Show AI Unit HUD" está ligado — o mesmo critério
do ícone de manutenção. Desligado, é o indicador de sempre. Campo num `[Header("AI EVAC")]`
próprio, não no meio do grupo Altitude. **Visto em Play.**

Branca e não vermelha: miolo tintável, sem brigar com a cor do time vermelho, e longe do emblema
da Cruz Vermelha antes da Steam.

## 5. Diretrizes que sobrevivem à sessão

`53779ec`, `70b2745`, `9035830`. A lição da v10.4.0 e desta versão virou texto que toda sessão
lê: **"Antes de escrever regra nova — procure a que já existe"**, na seção Regras de trabalho do
`docs/resumo.md`. A skill de fechamento passou a revisar cada frente (inclusive de outra sessão)
contra ela, a alimentá-la e a nunca podá-la, e ganhou o passo 8, backup do dia. Propagado aos
fechamentos da Mylarhis, Torre de Controle e Zone 5 (só local, sem push); o backup é diário em
The Map Room e Mylarhis, sob pedido nos outros dois. A skill global `faca-um-backup` deixou de
chamar Torre e Zone 5 de "descontinuados".

---

## Hipóteses que caíram

- **"A IA para depois do load"** — era o `startOnPause: 1` da cena Batalha (debug intencional).
  F12 (AI Resume) a destrava; o load está saudável. O `ai replan` funcionava por reiniciar o
  estágio 2, não por consertar nada.
- **"O soldado do EVAC não está em reparo porque embarcado sai do registro"** — embarcado continua
  em `UnitManager.AllActive` (o embarque só esconde sprite e HUD). O defeito era de **ordem**.

## O que não terminou

- **O Serviço do Comando rodou de novo depois de um load** (relato do autor). Se o save era do
  meio do turno, a IA pode estar decidindo de que estágio retomar **antes** de o load restaurar
  `aiRuntimeStage` — e cobraria o serviço duas vezes. Não verificado: o sinal é `Stage 1 ja
  concluido pelo save` no Console após o load.
- **EVAC conjunto, anel contestado e dupla no anel** — só nos testes fora do Play.
- **Volume do `AudioManager.prefab` 0,49 → 0,70** (`e78eb0a`) — comitado como autoria, mas pode ser
  o slider do `Panel_Config` gravando estado de runtime no prefab compartilhado. Conferir.
- **Atlas dinâmico das fontes** (VT323, LiberationSans Fallback) — entra como churn neste
  fechamento; regenera em todo Play.
- Carregados da v10.4.0: Fase 2 vendo o inimigo (`e72deff`) não observada; revisão do tabuleiro
  mudando durante a decisão do Chinook; 24 `validPaths` por decisão; plano B do courier aéreo;
  degrau "pousa agora, anda dois turnos"; Avião Tanque sem pré-compra; demanda do caminhão pela
  régua do vaivém.

## Docs de doutrina

Desatualizados
- `docs/AI Behavior/Transporte.md` — EVAC: hospital pela regra do reparo, par prédio + LZ, anel do
  HQ em degraus como fallback final, EVAC conjunto (secundário a bordo sem vaga; no anel, desce
  perto e vai a pé) (**hospital livre visto em Play**; o resto só em teste). Carregado da v10.4.0:
  entrega pela banda Operacional, não atraca com inimigo em cima, recusas contadas, entrega do heli
  na faixa do passageiro, fila de carona, ganância, corrida, LZ de Chinook pousado.
- `docs/AI Behavior/Logistica.md` — EVAC/hospital do supridor citam a mesma escolha de destino
  (conferir se descreve a regra antiga). **Só compilou.**
- `docs/AI Behavior/contrato_questionario.md` — reparo avaliado antes do Serviço do Comando
  (gatilho/recuperação com o HP de chegada) (**só compilou**).
- Disruptor por faixa estrita (em trânsito > capturando > resto) — **nenhum doc de doutrina descreve
  o Disruptor** fora do rascunho `rascunho/Resumo da AI em poucas linhas.md` (procurei "Disruptor",
  "interdição"). Lugar natural: `Assalto.md` ou `Papeis.md`. (**visto em Play**)
- Carregados sem mudança: `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` §6 (vanguarda, cadeia,
  breakthrough, vaivém, counter, anel AA...); `Capturador.md` (C6 vivo desde `e72deff`, espera fora
  da produtora, herdeiro do blitz, escala por prédio sem doc); `Papeis.md`, `ficha_do_papel.md`,
  `Assalto.md`, `Vigilancia.md` (Interceptador em dois); `FireSupport.md`; `magnetic_tabela.md`.

Prontos para consolidar
- Nenhum. O Disruptor foi visto em Play hoje, mas mudou hoje (precisa de 2 fechamentos estável);
  EVAC, reparo e shopping mexeram nesta versão.
