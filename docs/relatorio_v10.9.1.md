# v10.9.1 — A primeira noite da D2 em Play: o GoGreen disparou sozinho

2026-11-11 à noite e 2026-11-12 de madrugada. Cinco correções sobre a v10.9.0, todas nascidas
de marcas do AI Step na partida de ilhas (save do slot 3, turnos 11 a 13), e um resultado que
dois dias atrás não acontecia: **Golf saiu de `ASSEMBLING` para `GO_GREEN` no T12** com
`cap=7/4 ruptura=3/2 suporte=1`, sem artilharia, com Apache e Caça A presentes na área.
Capitão #10, 14 participantes, monitor de invasão armado. A marcha ainda não foi vista.

## O fio: cada marca era uma régua lendo a entrada errada

Nenhuma das cinco foi "a IA é burra". Foi sempre uma regra correta com uma suposição de
planície escondida, e o mapa de ilhas tirou a suposição:

| marca | suposição escondida | régua que entrou |
|---|---|---|
| bombardeiro e Apache com vaga em Golf caçando a 10h | o ramo aéreo nunca perguntou "tem vaga?" | aeronave com vaga em rally montando combate só de dentro da área e é atraída à Vanguarda |
| Artilharia de Campanha de 20k por conscrição, 34/60 unidades | "massa" = o mais barato do produtor; defesa anulava o teto | teto absoluto; massa é elite 0; projeção do Hard dos dois lados |
| Chinook parado com dois soldados cuja captura outro já fazia | sem alvo = fica parado | carga sem tarefa desembarca no montinho do transporte |
| Caça A descendo numa AAA de 2 HP com a Vulcan | "ar primeiro" era adiar, não proibir | interceptador não inicia tiro terciário |
| Caça A de Papa sem atirar em presa a 4h da borda | "nos arredores" = só de dentro | surtida: sai se a área está no Tático da célula de tiro |

## Ar com vaga no rally (`75d1e47`, `339fc99`, `8198f9b`)

`AIController.AirCombat.cs`. Os três aviões de Golf (Caça B #80, Bombardeiro #87, Apache #85)
decidiam como `rogue` porque nem o scan Tático nem a escada de âncoras consultavam a vaga do
plano; só a interdição olhava, e para o caso **sem** vaga. Entrou `TryResolveAirRallyAssembly`:
com vaga em `RallyAssembly` montando, a âncora é a Vanguarda mais próxima
(`Rally:Montagem@Golf`), o tiro só vale de célula dentro de Core ∪ Vanguarda, e caça livre,
interdição e presa Operacional ficam para quem não tem vaga.

**Regressão que eu aprovei e a marca seguinte pegou:** o primeiro commit desligou
`TryResolveInterceptorOperationalPursuit` durante a montagem (`!assembling &&`). Com isso o
Caça A perdeu a única proteção que tinha contra o tiro terciário no chão. Voltou em `339fc99`.
Lição: uma guarda nova não desliga a antiga; quem revisa confere o que o `&&` tirou.

**O que a doutrina dizia de verdade:** o autor lembrava "interceptador não ataca solo"; o
código da v10.7 dizia "adia o terciário **se** há presa aérea no Operacional". Decisão nova:
terciário nunca inicia, revide livre. A Vulcan é arma de defesa quando os mísseis acabam.
E tirar o terciário da ficha não resolveria: `ResolveAiTargetPriorityForTargetClass` devolve
`Tertiary` para qualquer classe **não listada**; é o padrão, não uma linha apagável.

**Surtida** (`8198f9b`): "do leque da vanguarda partem Táticos de caça; não ficar pingando de
ponto a ponto". A célula de tiro vale se está na área **ou** se, a partir dela, Core ∪
Vanguarda está no Tático da unidade. Medido sempre da área, nunca da posição anterior: é o que
impede a corrente de tiros cada vez mais longe. O autor pôs Helicopter em 2º na ficha do Caça A
durante a partida; a ficha foi lida na decisão seguinte (sem replan), e o Apache a 8h ficou sem
candidato só por alcance, não por gate.

## Conscrição (`0e283f9`)

`AIShoppingPlanner.Demand.cs` e `BuildMacroTerritoryContext`. `active = (byRatio || byCap) &&
!defense` deixava a defesa anular o teto; virou `byCap || (byRatio && !defense)`.
`BuySurgeMassBody` e o imposto exigem elite 0 do Exército: a Fábrica Pesada, cujo cardápio é
Pesado e Campanha, sai da conscrição. E a projeção do Hard ficou simétrica: a força própria conta
embarcados (`GetSlotUnitCounts`) e projeta os próprios produtores como projeta os inimigos.
Os 39% que davam "Perdendo" num 32 contra 36 eram `32 / (32 + 36 + 14 produtores inimigos)`;
em Normal seriam 47%, "Empatado". Os limiares 40% e 60% não mudaram.

## Carga sem tarefa (`a3df0e0`)

`AIController.Transportador.Air.cs`. O diagnóstico do Codex ("conflito entre a missão do
transporte e a dos passageiros") era meio certo: `TryResolveUnservedAirCapture` dizia, com
razão, que a captura de Hotel já estava atendida; o que faltava era o passo seguinte. Agora,
sem alvo para nenhum passageiro, o courier aéreo com vaga em rally montando entrega a carga no
Core do seu rally (`TryBuildAirRallyAssemblyDelivery` com `fallbackRally`), onde ela conta como
Ocupação. A raiz, dois soldados segurando vaga de Hotel que já não precisa deles, é do planner
e ficou.

## O que a tela ensinou

A prontidão na tela é o **fato do turno**: `EvaluateRallyReadiness` roda no planner e fica
cacheada por rally, slot e turno; a tela lê o mesmo fato de propósito. Caça e Apache entrando
na área no meio do T12 não mudam a barra até o próximo planner. Lido na hora pelo dump: os
cinco aviões presentes, `Missing=ruptura(1/2)` ainda no cache, e no planner seguinte GO_GREEN.

## Medição do teto (pergunta do autor: 60 é exagero?)

Dump do slot 1 no T12, 42/60 unidades: 25 em vaga de setor (4 embarcadas), 14 na onda de
invasão, 3 recém-compradas, **9 a mais de 6 PM do próprio objetivo**. O teto é a referência de
todas as válvulas (conscrição 50%, histerese 45%, projeção), então baixá-lo muda o jogo inteiro
sem código. Recomendação registrada: testar 40 e levar `MaxUnitsPerTeam` para o quadrante.
Decisão não tomada; repetir o dump por dois ou três turnos antes.

## O que não terminou

1. **A marcha.** GoGreen disparou no T12; `ESPERANDO`, `solta retardatário` e o capitão
   retomando não foram vistos. Capitão #10 está **a pé** na ilha: sem Chinook com vaga em 3
   turnos, o monitor declara `invasão FRACASSOU` e remonta. É corrida contra o relógio.
2. **Doutrina da invasão por ilha**, quadro do autor para a próxima `pergunte-me`: Ruptura e
   Dissuasão atacam; Chinooks buscam primeiro a infantaria que já está no montinho (o shuttle
   hoje não sabe o que é rally); caças e Apaches novos vão direto ao combate (já é assim). E a
   régua "capitão é quem alcança o alvo": primeiro a embarcar, grupo "com o capitão" enquanto
   ele está a bordo. Quem segura o capitão no ESPERANDO (hoje todo participante) segue aberto.
3. **Marca não lida:** 2026-11-12 00:45, T11 slot 1, "a compra veio pra fechar o rally né?".
4. **Teto de unidades:** medição acima; decisão pendente.
5. **Planner:** passageiros segurando vaga de setor já atendido (Hotel); eixo forte/fraco
   (`strongWeakSidePolitic`, protótipo desligado, só fire support) para a pulverização.
6. Herdados da v10.9.0: D4 formação ativa, degrau 7 naval, `docs/AI Behavior/Forcas.md`.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- `docs/AI Behavior/Assalto.md` e `Papeis.md`: ar com vaga em rally montando fica na área
  (âncora Vanguarda, tiro de dentro ou com volta no Tático); interceptador não inicia tiro
  terciário (antes: adiava só com presa aérea). **Visto em Play** nas marcas T12 e T13.
- `docs/AI Behavior/Transporte.md`: carga sem tarefa desembarca no montinho do transporte. Só
  fixture.
- `docs/rascunhos/AI Shopping.md`, `docs/Planos/Reformular Shopping.md`: conscrição com teto
  absoluto, massa elite 0, projeção simétrica. Visto em Play (a compra de 20k não repetiu).
- Carregados da v10.9.0: `Forcas.md` inexistente; `rally_proposta.md`; `contrato_questionario.md`
  §7.1 e `ficha_do_papel.md` (catálogo, `TransportadorAereo` morto); `governanca.md` G17;
  `Capturador.md` e `Transporte.md` (presença por banda ou área). Carregados da v10.8.0 e
  v10.7.0: lista no relatório v10.9.0, sem mudança.

Prontos para consolidar:

- Nenhum pelos três sinais: tudo que foi visto em Play mudou neste fechamento.
