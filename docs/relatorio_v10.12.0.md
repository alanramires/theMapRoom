# v10.12.0 — O Chinook leva 2 do soldado e 7 da AAA

2026-11-13 à noite e 2026-11-14 de madrugada. Três marcas do AI Step no EVAC aéreo, e a terceira
mostrou que a régua de ameaça do EVAC não sabia o que ameaça um helicóptero. O relatório diário,
com cada fixture, está em [`relatorio_2026-11-14.md`](relatorio_2026-11-14.md).

## O fio: o atalho parecia o dono, de novo

Mesmo desenho da v10.11.0, e cada marca o repetiu:

| marca | régua usada | dono que já respondia |
|---|---|---|
| Chinook #66: o #25 largado no chão com a Fábrica Pesada colada no helicóptero | no anel, construção fora do anel seguro = recusa | `TryResolveEvacHospitalDrop`: aquela célula era hospital que atendia o #25 |
| AAA #38 em reparo, a 1 hex do HQ, agiu em 7º | "ferido na vanguarda age primeiro", com a frente na **média dos inimigos visíveis** | a geometria do anel do HQ (`IsWithinOwnHqRing`) |
| Chinook #99: "que passinho pro lado é esse???" | `CalculateThreatLevel`, proximidade de qualquer inimigo | a Matriz de HP (`AICombatHpSimulator`): o soldado tira 2 de um Chinook cheio, a AAA tira 7 |

**Achado que contradiz o que se supunha:** desconfiei que o soldado nem atirasse no helicóptero.
A ficha do Rifle tem `aditionalDomainsAllowed: Air/AirLow`, então ele atira, e o erro não era
contar quem não atira. Era pesar todos igual. A régua certa já existia na ferramenta que o autor
apontou (`Tools ▸ Combat ▸ Matriz de HP`) e no runtime que a alimenta.

## EVAC do segundo ferido (`pergunte-me` P1–P5)

Decisões do autor: o segundo ferido **prefere** um hospital que o atenda, sem ser obrigado a ele;
no anel, pode descer num hospital fora do anel seguro se a célula passar no `IsEvacDropCellSafe`;
na entrega conjunta, mais feridos em atendimento vencem antes da rota; o saudável desce no chão;
o anel é o último recurso, e antes dele o hospital tenta a rota de menor ameaça.

O diagnóstico do Codex apontava a comparação do anel. Estava certo em parte, mas a combinação certa
nunca chegava lá: a `Resolve` do `MatchEvacPatients` recusava a célula antes. A geometria fechou a
questão: (3,13) é vizinha da LZ (2,13) e fica a 3 do HQ.

Na correção, o Codex apagou sem avisar a **preferência por ramo** da v10.11.0, tanto no matching
quanto na comparação. A revisão pegou o defeito e ele voltou como régua única: o
`ResolveEvacDisembarkPreference` alimenta o matching e o `EvacDeliveryScore.Beats` (próprio ramo
direto 0, a pé 1, outro ramo direto 2, a pé 3, chão 4).

**Visto em Play:** na sessão seguinte, o passo 2 largou o #25 na Fábrica Pesada de (3,13).

## Iniciativa: ferido no anel do HQ age por último (`pergunte-me` P1–P2)

Ferido em reparo dentro do anel do próprio HQ vai para o grupo 5, antes de qualquer promoção,
inclusive a da vanguarda e a da invasão. A conta do raio saiu do código inline e virou
`IsWithinOwnHqRing`, uma régua só para o fallback do reparo, o BFS do anel do EVAC e a iniciativa.
O `Backline` não foi tocado: a âncora pela média dos inimigos continua como estava, e a regra
nova resolve por cima, no organizador. **Só fixture.**

## Ameaça do EVAC por dano previsto (`pergunte-me` P1–P4, implementado pelo Claude)

O Codex respondeu que já tinha feito, mas nada estava no disco. O autor pediu que eu fizesse.

- **A régua.** O cache de ameaça de cada decisão de EVAC virou `EvacThreatMap`, que sabe qual
  transporte está medindo. Ele monta uma vez, para cada inimigo visível que alcança a região, o
  envelope de combate do próximo turno e o pior HP que o `AICombatHpSimulator` prevê contra o
  transporte. A ameaça de uma célula é a soma dos inimigos cujo envelope a cobre.
- **A régua do autor na aproximação.** Na frase dele: "voar o mais rápido e mais longe da zona
  de ameaça, mas ainda na direção da base, o final da borda". Dano zero vence; entre os de dano
  zero, vence quem mais avança. Sem como sair da zona, vence quem sobrevive e mais avança. Ninguém
  sobrevive, vence o menor dano. Uma régua só (`TryPickEvacApproachMove`) serve o anel e a
  aproximação contestada do hospital, que agora vem antes do anel.
- **O passageiro ficou na régua antiga.** A célula onde ele desce continua medida por
  proximidade (`GetEvacDropThreat`), como a P4 pediu.

Duas correções de rumo, ambas minhas:

- **Sobreviver se mede na célula final.** O transporte só apanha no turno do inimigo, onde termina
  o movimento. O bloco que escrevi dizia "rota mais célula final", e isso contaria o mesmo soldado
  uma vez por hex atravessado.
- **Sem as tabelas, volta a proximidade.** A primeira versão devolvia zero quando não havia
  `TurnStateManager`. Dois fixtures antigos pegaram: zero dizia "seguro" justamente quando não se
  sabia. O "sobrevive" também só vale com a matriz, porque proximidade não é HP.

**Só fixture** (`verificar_evac_ameaca_dano`). A parte que confere a régua do passageiro é fraca:
compara zero com zero, porque os inimigos do teste não entram na lista que a proximidade lê.

## O que não terminou

1. **O Chinook #99 em Play.** É a marca que motivou a régua nova; precisa de partida.
2. **A AAA #38 no fim da fila em Play**, e a causa da vanguarda falsa confirmada por
   `ini_aaa38.cs` (scratchpad) num turno em Play. A âncora pela média dos inimigos continua no
   `Backline`, e outros leitores do `IsWoundedInVanguard` podem herdar a mesma vanguarda falsa.
3. **O `CalculateThreatLevel` dos outros papéis** ainda pesa todo inimigo igual (P4 adiou de
   propósito): capturador, reparo, logística.
4. **EVAC naval** num mapa de praia: nunca rodou.
5. **Um commit para duas frentes.** O EVAC do segundo ferido e a régua de dano dividem o
   `Transportador.Evac.cs` em blocos entrelaçados, e o estado intermediário do Codex não existe
   mais em lugar nenhum. Foram juntos, num commit só.
6. Carregados da v10.11.0: a marcha de Golf em Play; a marca do orçamento da invasão; quem segura
   o capitão no mar (degrau 7); logs sem gate antes da versão; o commit pré-compra de 1,5 s.

## Docs de doutrina

Desatualizados (apontados, não reescritos):

- `docs/AI Behavior/Transporte.md` (EVAC): a ameaça ao transporte é o dano previsto pela Matriz
  de HP; na aproximação, sair da zona rumo à base vence; o segundo ferido prefere hospital; o
  saudável desce no chão; o anel é o último recurso. Visto em Play só o segundo ferido no
  hospital; o resto só fixture.
- `CLAUDE.md`, tabela "Initiative ordering": fala em grupos 0 a 4, mas o código usa o grupo 5
  para o ferido, que agora inclui o ferido no anel do HQ. Só fixture.
- Carregados da v10.11.0: `rally_proposta.md` "Ainda aberto" e as seis regras da capitania;
  `d2_prontidao_plano.md` (ressalva naval); `Transporte.md` (carona da invasão, EVAC por ramo);
  `Capturador.md` e `Logistica.md` (reparo por ramo). Dos anteriores: `Transporte.md` §5,
  `manual/07_logistica_e_servicos.md`, `Assalto.md`/`Papeis.md`, rascunhos de Shopping,
  `Forcas.md` inexistente, `contrato_questionario.md` §7.1, `ficha_do_papel.md`,
  `governanca.md` G17.

Prontos para consolidar:

- **Interceptador não inicia tiro terciário** e **ar com vaga em rally montando fica na área**
  (`Assalto.md`/`Papeis.md`). Terceiro fechamento seguido na lista; `AirCombat.cs` não foi tocado,
  e as três marcas novas são de EVAC e iniciativa.

## Marcas do AI Step do dia

Três marcas, as três com correção na árvore: Chinook #66 (segundo ferido), AAA #38 (iniciativa),
Chinook #99 (régua de ameaça). Só a primeira foi vista corrigida em Play.
