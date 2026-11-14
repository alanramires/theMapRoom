# v10.11.0 — Quem alcança o alvo, e onde o ferido desce

2026-11-13. Duas entrevistas `pergunte-me` viraram duas regras, e as duas revisões acharam o
mesmo tipo de defeito: **uma pergunta que já tinha dono respondida por uma régua parecida, que
perdia o que o dono sabia.** Uma vez o erro foi meu, no bloco que escrevi para o Codex; duas vezes
foi do código. Nos três casos o fixture passava, porque o cenário era pequeno demais para mostrar
a diferença.

O relatório diário do Codex, com cada fixture, está em
[`relatorio_2026-11-13.md`](relatorio_2026-11-13.md). Este explica o porquê.

## O fio: a régua parecida não é a régua

| pergunta | dono | o que foi usado | o que se perdeu |
|---|---|---|---|
| "este capturador tem rota ao alvo da invasão?" | `AIRouteDistance.TryGet` (sem horizonte de turnos) | o envelope Operacional (2 turnos) | todo alvo a mais de 2 turnos: capitão -1, grupo inteiro em hold, carona pedida por quem tinha estrada |
| "esta célula do raio do HQ serve para largar o passageiro?" | o anel de `DecideAirEvacHqFallback` (`IsRepairHqFallbackCellAllowed`, névoa, seguro/contestado) | `HexDistance ≤ DefenseEnemyRange` | conscrição, cluster da base sob pressão, produtora de logística, célula contestada |
| "o prédio atual atende este soldado?" | `CanSurfaceRepairConstructionServe(…, ignoreTurnLimit: true)` | a mesma função **com** o limite por turno | todo soldado curado na Fase 1 lia "não atende" e saía no mesmo turno |

O primeiro é o que mais ensina, porque nasceu da doutrina e não do código. Na entrevista o autor
decidiu "capitão é quem **tem rota** ao alvo". No bloco eu escrevi "rota terrestre **pelo envelope
Operacional**", e o Codex implementou exatamente o que estava escrito. O fixture punha o QG inimigo
a 2 hexes do blindado, num tabuleiro de 10×6, e passou. Num mapa de verdade a invasão ficaria
parada no rally até o teto de 12 turnos. **Banda é formação; rota é existência.** Tático e
Operacional continuam governando quem espera quem; "tem caminho até lá" é do `AIRouteDistance`.

## Rally e invasão — capitania por alcance (P1–P9)

Caso que motivou: Golf GoGreen no T12 com o capitão #10 **a pé numa ilha**, sem rota ao alvo. O
grupo seguia quem não ia chegar, e o monitor declararia `FRACASSOU` por estagnação antes de um
Chinook aparecer. Decisões do autor, todas implementadas em `AIController.PlanEvaluator.RallyPoints.cs`:

1. **Capitão é quem alcança o alvo** (`RefreshRallyAdvanceCaptains`, reavaliado a cada planner e no
   monitor). Capturador primeiro, por menor custo de rota, a pé ou embarcado num transporte que
   chega; se nenhum capturador alcança, a **Ruptura** que alcança ("o capitão pode se tornar uma
   unidade de ruptura que chega lá"); quando um capturador embarca, a capitania volta para ele.
   Sem ninguém: `RallyCaptainId = -1`, estado legítimo.
2. **Só Ruptura e Ocupação seguram o capitão** no ESPERANDO (`UpdateRallyAdvanceWaiting`).
   Dissuasão, Apoio e aeronaves nunca seguram. **Ressalva do autor:** a Marinha não tem Força de
   Ruptura; quem segura o capitão no mar fica para o degrau 7.
3. **Capitão a bordo:** a banda é a do transporte; quem ficou em terra espera na área do rally
   (`TryResolveRallyAdvanceHold`), sem perseguir o helicóptero.
4. **Carona da invasão na frente de todos:** `QueroCaronaResult.pickupPriority`, decorado pela IA
   e lido pelo shuttle e pelo `MelhorEmbarque`. A emergência continua acima. Mesma fila persistente.
5. **Estagnação congela** enquanto um participante tem carona persistente publicada e nenhum
   transporte com vaga o alcançou.
6. **Teto de 12 turnos** (`RallyGoGreenSuppressTurns`) virou prazo absoluto de fracasso.

Colisão achada pelo Codex e decidida pelo autor: o "solta retardatário fora do Operacional"
removia o soldado ilhado antes de ele pedir carona. Quem tem carona da invasão publicada fica no
grupo, espera na área e não segura o capitão; o teto de 12 solta todos.

Correção da revisão: `TryGetRallyRouteCost` passou ao `AIRouteDistance.TryGet`; o embarque
adjacente do hold passou a perguntar ao `MelhorEmbarqueService` em vez de pegar a primeira
opção do sensor. O fixture ganhou o caso que faltava: estrada de 12+ hexes, fora do Operacional,
capturador vira capitão sem carona, sem hold, sem ESPERANDO.

**Só fixture.** O save do slot 3 já está no T14 e não tem mais o grupo de avanço do T12.

## EVAC e reparo por ramo

Doutrina do autor: o Exército prefere fábricas, QGs, construções civis e produtoras do Exército; a
Aeronáutica, civis pousáveis, aeroportos e produtoras navais; a Marinha, portos e produtoras
navais. Fora do raio do HQ, quem não tem entrega fica a bordo; dentro, todos descem. O transporte
para fora dos prédios. Hospital ocupado orienta o último trecho a pé, sem prometer vaga.

- **O destino continua no dono.** `IsPreferredRepairFacility` dá a afinidade pela Força do
  paciente; `RankRepairConstructions` ordena por camada (0 preferido, 1 outro, 2 último recurso)
  e depois pelo score de sempre. Superfície, naval e aéreo leem o mesmo ranking, e os seletores
  privados que sobravam foram apagados.
- **O serviço virou portão do ranking** (`CanSurfaceRepairConstructionServe`): prédio sem estoque
  ou sem o serviço que o ferido precisa não entra.

A revisão achou dois defeitos e o autor decidiu duas margens:

- **Anel do HQ:** o atalho por distância saiu; `BuildEvacHqRing`, extraído do fallback, é montado
  uma vez por decisão e passa como `finalRing`, com o filtro de estacionamento de cada passageiro.
- **Camada 2:** HQ ocupado mantido como fallback e célula reservada à defesa entraram no último
  recurso; antes a camada passava por cima das penalidades que existiam para perder.
- **D1:** o preferido só ganha a camada 0 dentro do Operacional do paciente.
- **D2:** o soldado só sai de um prédio de outro ramo quando ele não o atende, ou quando o
  preferido está no Tático dele. A pergunta "atende" ignora o limite por turno (o terceiro
  defeito da tabela acima, achado depois de a D2 entrar).

**Só fixture.** Nenhuma entrega vista em partida; a naval completa num mapa de praia nunca rodou.

## Suprimento infinito por debug

`set infinite supplies [setor[:slot]] [on|off]` no console de debug. Reusa o alvo sob o cursor do
`set owner` e o override que a construção já tinha; refaz o setor uma vez no fim. **Visto em Play**
pelo autor.

## O que não terminou

1. **A marcha de Golf em Play.** A capitania nova nunca rodou numa partida. Precisa de um save de
   antes do GoGreen ou de um rally novo chegando nele.
2. **Marca de 2026-11-13 09:16, T15 slot 0:** "a AI está montando o orçamento da invasão? a pressão
   por Chinook tá boa?". A carona da invasão agora entra na fila persistente com prioridade, e a
   fila já pede Chinook; a resposta só existe vendo a invasão em Play.
3. **Entrega naval do EVAC** num mapa de praia.
4. **Quem segura o capitão no mar** (ressalva de P2): degrau 7, junto com a bandeira naval.
5. Carregados da v10.10.0: logs sem gate antes da versão (`docs/checklist_logs_publicacao.md`
   seção C); commit pré-compra de 1,5 s; `pergunte-me` da cópia de `TryResolveCellVision` no
   `PodeMirarSensor`; caminhos válidos acima do MP; teto 60 vs 40; eixo forte/fraco.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- `docs/rally/rally_proposta.md`, "Ainda aberto": quatro das cinco fecharam na D2 (razão local,
  saturação, "esticou demais" = Operacional do capitão, aéreo vale 1); sobra o naval. E ganha as
  seis regras da capitania. Só fixture.
- `docs/rally/d2_prontidao_plano.md`: ressalva da Marinha sobre quem segura o capitão.
- `docs/AI Behavior/Transporte.md`: carona da invasão com prioridade na fila; EVAC por ramo,
  entrega no chão a um Tático do hospital, anel do HQ filtrado e liberação coletiva no raio. Só
  fixture.
- `docs/AI Behavior/Capturador.md` e `Logistica.md` (reparo): instalação preferida pela Força do
  paciente, margem no Operacional, troca de prédio só quando o atual não atende. Só fixture.
- Carregados da v10.10.0: `Transporte.md` §5 (pressão pela espera, visto em Play);
  `docs/manual/07_logistica_e_servicos.md` (acesso do Serviço do Comando, só fixture);
  `Transporte.md` (recuperação aérea livre, carga com tarefa atendida). Da v10.9.1 e anteriores:
  `Assalto.md`/`Papeis.md`, rascunhos de Shopping, `Forcas.md` inexistente,
  `contrato_questionario.md` §7.1, `ficha_do_papel.md`, `governanca.md` G17. Sem mudança.

Prontos para consolidar (estável há 2 fechamentos, visto em Play, sem marca aberta):

- **Interceptador não inicia tiro terciário** e **ar com vaga em rally montando fica na área**
  (`Assalto.md`/`Papeis.md`). Entraram na v10.9.1, vistos em Play nas marcas T12/T13;
  `AIController.AirCombat.cs` não foi tocado na v10.10.0 nem na v10.11.0, e a única marca nova é de
  orçamento da invasão. Segundo fechamento seguido na lista: ninguém chamou `consolidar-doutrina`.
