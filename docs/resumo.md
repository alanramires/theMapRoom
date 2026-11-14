# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-11-13, **depois** da tag `v10.11.0`.
Leia isto primeiro — **em especial as Regras de trabalho, no fim.**

---

## Estado

`v10.11.0` tagueada e publicada (v10 — Cada mapa, a mesma IA). O MVP estável segue na
v9.2.0. Relatórios: [`relatorio_v10.11.0.md`](relatorio_v10.11.0.md),
[`relatorio_v10.10.0.md`](relatorio_v10.10.0.md), [`relatorio_v10.9.1.md`](relatorio_v10.9.1.md).

**A descoberta da v10.11.0: a régua parecida não é a régua.** Duas entrevistas viraram duas
regras (capitania da invasão por alcance, P1–P9; EVAC e reparo pela instalação do próprio ramo), e
as revisões acharam três vezes o mesmo defeito: pergunta com dono respondida por uma régua
vizinha. "Tem rota ao alvo?" pelo Operacional de 2 turnos em vez do `AIRouteDistance` (o erro
foi do meu bloco, e deixaria toda invasão parada no rally num mapa grande); "serve para largar?"
pela distância ao HQ em vez do anel do `DecideAirEvacHqFallback`; "o prédio atende?" com o limite
por turno, que a cura da Fase 1 acende. Os três fixtures passavam: cenário pequeno não mostra a
diferença entre a régua e a parecida. Tudo **só fixture**; o suprimento infinito por debug foi
visto em Play.

**A descoberta da v10.10.0: o turno da IA congelava o frame por 14 s, e ninguém tinha
medido.** Plano 6,5 s, pré-compra 1,5 s, compra 6,5 s, num mapa de 86 unidades; o jogador via
tela morta. Nenhum era algoritmo: eram **três utilitários com nome de consulta e corpo de
varredura, chamados em laço por célula** — `ResolveAISlotKey` (`FindAnyObjectByType` em 264
sítios), `GetStructureAtCell` (`FindObjectsByType<RoadNetworkManager>` por célula da linha de
tiro, por célula do envelope, por aeronave inimiga) e `CalcularCaminhosValidos` com orçamento 30
(o estado é `(célula, passos exatos)`, então **enumera passeios, não caminhos**: 57 células em
1,7 s). Fechou em **3 s** com cache por cena, registro `RoadNetworkManager.AllActive` e matriz
do plano por `CalculateMovementCostMap`. A hipótese do autor ("o AI Step tá rastreando tudo") e
duas minhas (solver, recrutamento do rally) caíram pelo número; os oito scripts de
`tools/perf/` são o método. Também entraram: frota de Chinooks pela **fila persistente** (visto
em Play: "parou de spamar soldadinhos"), **acesso do Serviço do Comando é do transportador** e a
família pousa uma vez, recuperação aérea em célula livre, carga com tarefa atendida. E o
`docs/checklist_logs_publicacao.md`: `showAILogs` desligado gateia 12 das 541 linhas `[AI …]`.

**A noite da v10.9.1: o GoGreen de Golf disparou sozinho no T12** (`cap=7/4 ruptura=3/2`, sem
artilharia), e cinco marcas viraram cinco réguas: aeronave com vaga em rally montando fica na
área e faz **surtida** (sai se a área está no Tático da célula de tiro; medido sempre da área);
interceptador **não inicia tiro terciário** (a Vulcan é defesa; "ar primeiro" era só adiar);
conscrição com **teto absoluto** e massa elite 0; projeção do Hard dos **dois lados**; carga sem
tarefa desembarca no montinho do transporte. A marcha não foi vista: capitão #10 a pé na ilha,
monitor de invasão armado, Chinook contra o relógio de 3 turnos.

**A descoberta da v10.9.0: a conta, a vaga e o gate têm de ler a mesma régua.** O rally de
Golf esperava 3 de artilharia num mapa sem artilharia, com 33 mil parados, enquanto o autor
dizia há semanas que "artilharia é OPCIONAL" e a proposta escrita concordava. O mesmo erro
voltou duas vezes no dia: a conta somava o Caça B como Ruptura e a vaga pedia
`UnitRole.Assalto`; "rally no meu Tático" dava o Apache no hangar como presente e o caça
colado em Golf como Approach, porque alcance de aeronave é meio mapa. Entrou: **matriz de
Forças** (Ruptura/Dissuasão/Ocupação/Apoio; Bombardeiro, Obus Leve e Tanque Pesado viram
Fogo de Suporte, Apache fica Disruptor/Assalto), **prontidão** com piso 4+2, suporte que soma e
nunca gateia, inimigo local no Approach do modelo com razão por perfil (`rallyLocalRatio`),
saturação, capitão persistido e ESPERANDO com tiro parado, **presença aérea pela célula na
área** (`IsRallyParticipantPresent`) e embarcado como Ocupação, e o **catálogo de papéis como
asset** (`UnitRoleCatalog`, `Tools > Role Families` edita; os cinco `switch` de `UnitRole.cs`
morreram sem fallback). Para o caça virar porradeiro o autor marca `Assalto` em "satisfaz
também" no asset. **Visto em Play:** duas fotos de Golf T11 e o Ctrl+Z da janela. O resto, só
fixture.

**A descoberta da v10.8.0: o rebelde era o rogue.** A "AI rebel" nasceu como ramo paralelo
para o cenário sem QG; meses depois o autor viu que rebelde = unidade sem vaga no plano, e que o
**magnético** já era a tradução de volta. Régua para qualquer `isRebel`: *o magnético já
responde?* Sim → a porta cai; não → o magnético está incompleto e é ele que ganha o caso.
Nunca um terceiro caminho. Sobrou da **facção** só o que não é da unidade: sem base para o
shopping (`HasShoppingBase`, três demandas reativas), captura sem pré-requisito e nunca dona do
que ocupa (`firstOwnerSlotIndex`). A IA pergunta "tem vaga?" (`IsPassengerInPlanSlot`,
`AreAllPassengersPlanless`); a partida pergunta "tem QG?". Compra pela `sellingRule` da
instância (`allowRebelAIPurchase` morreu); 0 unidades derrota e neutraliza; QG capturado elimina
só o dono, fim por último de pé; cenário com spawn tardio protege-se por `UsesMatchEndRules` ou
`allowDefeatForZeroUnits` no `QuadranteData`. O Caça A entrou pela progressão do Interceptador
com gatilho de aviação inimiga e se posiciona por **exposição** (EWACS/vanguarda fora do
guarda-chuva, `AIAirExposureService`). **Nada disso foi visto em Play** — cinco commits, todos
"só compilou + fixture".

**A descoberta da v10.7.0: ter arma não é ter missão; ter ficha não é chegar ao alvo.**
O counter comprava Caça A pela metralhadora. O rally comprava Tanque B ilhado. Corrigido o
rally, o tanque reapareceu por outra porta: pré-requisito do Tanque A, com ficha exata pulando
a exclusão de ilha. A seleção da elite agora respeita a régua de isolamento antes de pedir a
cadeia; interceptadores não nascem como contramedida terrestre. **Esses dois últimos ajustes
só passaram em fixtures fora do Play**; ler a próxima marca antes de declarar resolvido.

Parede ponderada: artilharia níveis 0/1/2+ vale 1/1,5/3 pontos; Assalto terrestre metade;
três pontos por resposta, teto normal três. Suporte/Assalto pede ar; AA pede Assalto, com
conversão por oferta/acesso, não por falta de caixa. Invasão conserva 1:1 por artilharia mais
pressão suplementar de Assalto. Core já conta família (Apache incluído); parâmetros do AI
Manager não foram reescritos. Compra especial do rally filtra a produtora até a âncora real.

**Vistos em Play:** Chinook entregou os dois passageiros na área do rally; Caça B #73 adiou o
tiro terciário no SAM e avançou para o **Chinook #18**, não o Bombardeiro; registro do shopping
converteu o rally para Bombardeiro e recusou artilharia sem acesso. Marcas/linhas de envolvidos
ficaram rosa e movimento azul escuro (compilados, confirmação visual pendente). Diagnóstico
arquitetural em [`arquitetura/arquitetura_atual.md`](arquitetura/arquitetura_atual.md), sem
terceira refatoração; terminar a Aeronáutica primeiro. Montinho completo continua proposta.

Da v10.6.0 permanece a régua do supridor: autonomia aérea > HP crítico > corretivo > preventivo,
com prazo Tático antes do Operacional dentro da faixa. Régua/prazo vistos em Play; demais
casos de reparo, reserva e EVAC conservam seus débitos de validação abaixo.

**A descoberta da v10.5.0: ferido não é carga.** O transporte que leva ferido está num resgate,
e o ferido tem destino próprio — o do **reparo**. O EVAC aéreo (Codex, revisado antes de cada
commit) escolhe o hospital pelo `RankRepairConstructions` (o mesmo ranking do
`FindRepairConstruction`), para ao lado e solta o ferido no prédio; sem hospital, o **anel do HQ**
é o último recurso e nunca falha (seguro → contestado → aproxima/foge); dois feridos descem juntos
(sem vaga, o secundário fica a bordo; no anel, desce perto e vai a pé). E o reparo passou a ser
**avaliado antes do Serviço do Comando curar**: quem chegava abaixo do gatilho era curado e pulava
a faixa de reparo — o Chinook com soldados de 2 HP não virava EVAC. O Disruptor ganhou ordem
estrita: em trânsito > capturando > resto (visto em Play). A cruz EVAC no HUD (debug da IA) mostra
quem está em resgate. E a primeira versão do EVAC foi pega pela diretriz nova: escolhia hospital
por fora do dono da pergunta.

**A descoberta da v10.4.0: a Fase 2 jogava sem ver o inimigo.** O `AIWorldSnapshot.BuildLight`
(o snapshot da Fase 2) omitia `EnemyUnits` sob a promessa "nenhum handler de role lê isso" — e
Backline, reparo pela linha, fogo de suporte, rally, logística, MelhorDesembarque e questionário
liam, recebendo lista **vazia**, sem erro ("sem inimigo conhecido, não há frente"). Corrigido em
`e72deff` (névoa confirmada). **Todo comportamento de posicionamento por linha visto antes disso
não vale como evidência** — e passa a rodar agora, pela primeira vez. Ataque, ameaça, combate
aéreo, planner e shopping nunca foram cegos.

O fio do dia foi o mesmo em todas as frentes: **recalcular o que já estava pronto.** A Shopping
Pressure refazia a compra (agora lê a última compra do `Decide`); o AI Step refazia a névoa e
varria o terreno a cada unidade (agora ~30 KB e < 5 ms por passo, medido); o
`GetConstructionAtCell` buscava a cena a cada chamada (agora lê `ConstructionManager.AllActive`
em Play — o filtro "fora da produtora" custava 13 s por decisão); o courier aéreo refazia o
MelhorDesembarque (agora a entrega sem LZ no Tático passa pela banda **Operacional** para
qualquer carga: o passageiro pousa onde dá e anda dois turnos — doutrina do autor). E a **escada
aérea**: retaguarda pede antiaéreo, vanguarda (tropa fora do guarda-chuva) pede caça pela presa,
"vende" respeita a cadeia de elite, breakthrough pela classe Artillery; Avião Tanque pela pressão
do **vaivém**, não pela foto de combustível.

**A descoberta da v10.3.0: a IA comprava por igualdade.** O counter só sossegava quando a
cobertura *empatava* a força inimiga (efeito Pernalonga × Gaguinho, inclusive contra infantaria
do outro lado do mar); o Médio planejava igual ao Fácil (o multiplicador do preset não tinha
leitor); o "Interceptador" era um nome só para dois trabalhos. Cada igualdade virou régua:

- **counter:** urgente só no **anel** (Operacional do inimigo, para terra), fora dele tolera
  60% de cobertura, e alvo em outra ilha não aceita resposta terrestre quando há ar à venda;
- **capturadores por prédio** no perfil (Fácil 1,1,2,2 · Médio 1,2,3,4 · Difícil 2,4,5,6),
  reforço do Difícil desliga em `Defensive`; a invasão da base segue com regra própria;
- **dois ramos aéreos** (autor): poder de captura < Apache < Caça B < Caça A; bombardeiro <
  Caça B < Caça A. Novo sub-rótulo `InterceptadorDeDisruptor` (Caça B), no molde do Disruptor;
  a escolta o pede pelo papel principal;
- **antiaéreo pela presa:** bombardeiro no anel pede SAM (Plane PRI), Apache pede AAA (Heli PRI);
  sem primário à venda, o secundário. **Um PRI por classe** — empate pelo preço nunca compra SAM.

Vistos em Play: teto de conscrição contando embarcados, escolta pré-comprada (Caça B saiu),
counter sem resposta terrestre para outra ilha. O resto só compilou.

**A descoberta da v10.2.0: a compra gastava o dinheiro do elite em qualquer urgência.** Três AAA,
um Apache, uma Metranca — e os caças nunca nasciam. Romper a poupança passou a ser **no tamanho
da demanda**, a AA escala (1 urgente por turno), Chinook não conta no anel quando há Disruptor, e
a escolta que não cabe **segura o caixa e passa a vez** (a mesma régua `EliteSaveTurns` da
poupança do elite). Com a folga da conscrição (para aos 55% do teto), o Médio comprou Caça B em
Play. A fila de carona agora é contada (`PressaoCaronaService`): ~4× mais pedidos que bancos,
quase todos ilhados — **os corpos da conscrição nascem ilhados** quando a ilha de casa fecha.

**A v10.2.0 também preparou o jogo para outra pessoa jogar:** `Tools ▸ Publicação ▸ Checklist`
(debug das cenas do build, texturas Web 512/1024, Web sem LTO), volume geral e da música no
`Panel_Config`, estrada inteira no mapa da Campanha. As práticas vieram da Mi'Larhys — ler os
menus `Tools/Mylarhis/*` e o resumo dela, não só copiar a casca.

**A descoberta da v10.1.0: a força aérea nasceu como cópia da terrestre.** Cada marca era uma
suposição de chão vestida de ar. A regra do ar que apareceu: **o capturador põe dinheiro no
prédio, o Disruptor tira dinheiro do inimigo** ("você não vai fazer dinheiro não!"). O papel
`DisruptorDeCaptura` (Apache) tem como capitão o **poder de captura combinado** e atira pelo
poder negado; o Bombardeiro (Disruptor secundário) nega a **escolta**. O shopping pede
Disruptor em pares pelo poder de captura em trânsito. Vistos rodando: a âncora CCP, o tiro no
soldado em vez do Chinook vazio, a demanda rompendo a poupança, o "trabalha enquanto espera".
O resto só compilou. **O fechamento agora aponta docs de doutrina** (relatório, seção "Docs de
doutrina"); a skill `consolidar-doutrina` escreve quando o autor quiser.

**A descoberta da v10.0.1: a regra da ilha estava numa porta só.** O pacote 2/2/1 já trocava
tanque por ar na ilha sem ligação por terra; o counter, o elite, o déficit de operação, a AA
urgente e a vigilância continuavam comprando como se houvesse estrada. Agora toda porta pergunta
à mesma fonte (`OwnLandProbe`). No plano, a mesma lição: o alocador e o blitz decidiam sem
perguntar se a unidade chega lá a pé. **Quem fecha nesta rodada ganha o objetivo** (troca pelo
Tático, também entre quem já tem vaga), e **a ponta só reveza com rota terrestre** — esta,
vista rodando. O resto só compilou.

**A descoberta da v10.0.0: quase toda marca do AI Step era uma regra no lugar errado.** Não um
número: uma constante de hex onde devia haver banda, uma composição de solo onde devia haver
família, um corte de estrada que nenhuma ficha declarava. Cada conserto tirou a regra da IA e a
devolveu ao mapa:

- **blitz "o de trás paga":** a fila é o eixo, a base é a raiz, o herdeiro captura no Tático,
  a ponta que reveza luta (dinheiro primeiro, melhor DPQ) e o herdeiro fica gravado no objetivo;
- **embarque:** o veículo escolhe o par, a LZ fica na borda do Tático do passageiro rumo ao
  destino, o hex de encontro é reservado;
- **estrada atravessa construção** (regra de movimento, humano e IA): a construção cobra custo,
  a rodovia decide o bônus por terreno — **visto rodando** (Tanque Leve pelo QG até (6,10));
- **núcleo e pacote 2/2/1 por família** (`Tools > Role Families`); **ilha sem ligação por terra**
  pede Apache e SAM/AAA, sem poupança de MBT, e a **ilha invadida** religa o terrestre;
- **teto macro** não corta objetivo do eixo com capturador a caminho;
- **Shopping:** veículo nasce na fábrica com estrada.

O mapa de teste agora é o **Peixe Pequeno** (Q3, ilhas de metrópoles, ar + exército sem ligação
por terra). A Batalha aponta para ele. **As marcas da v10.7.0 mostram o shopping rodando; cada correção tem validação própria no relatório.**

**A descoberta da v9.6.0: o jogo grava o pensamento da IA, o autor marca, o agente lê.**
A tela **AI Step** ([`ai_step.md`](ai_step.md)) para a IA antes de cada execução e
mostra decisão, missão, motivos e alternativas. O autor marca o passo estranho com
uma pergunta, que vira uma linha em `ai_step_marcas.jsonl` na raiz. Responder pelo
registro custa uma fração de reconstruir a partida pelo código.

Três marcas no mapa sem elite (o **núcleo** das decisões) renderam três mudanças:

- **grana antes do counter** no Médio e no Difícil: o bônus de encaixe atravessava
  a prioridade da fila. **Visto rodando.**
- **postura medida no mesmo ponto do ciclo:** quem joga em segundo caía em
  Defensive no T2 de toda partida, por ordem de turno.
- **log do handoff do blitz:** a decisão estava certa; o log é que não contava.

O método completo, com scripts, está em [`ai_test.md`](ai_test.md).

**A descoberta da v9.5.0: o Claude joga, e jogando acha o que lendo não achava.** O
Unity CLI (`com.unity.pipeline`, guia em [`indie/unity_cli_mcp.md`](indie/unity_cli_mcp.md))
liga o agente ao editor aberto: ele lê o tabuleiro pelos sensores (`script eval`),
pergunta à IA no mesmo estado (`DecideUnitAction`, uma unidade por vez) e age pelo
batch no automata, como a IA e o humano. Em 3 turnos achou: liberar produtora
atropelando a captura **e** mandando a unidade para trás (escondendo o revezamento
do blitzkrieg), e vaga de captura do plano que não virava compra. Registro em
[`playtest_claude.md`](playtest_claude.md). **Nada das correções foi visto rodando.**

**A descoberta da v9.4.0: a fórmula de combate não era fonte única — e o DPQ que
entra nela tinha quatro cópias.** Dia de revisão, a partir de dois textos de um
consultor externo ([`arquitetura/`](arquitetura/)), cada achado conferido no
código antes de aceitar. Agora `CombatFormula` é a conta (execução e IA chamam a
mesma), `PositionDpqResolver` é o DPQ (com a regra da execução), e **em jogo a IA
só simula o que o `PodeMirar` oferece** — a calculadora por ficha (planície,
munição cheia) é ferramenta de balanceamento e está marcada assim. Na máquina de
estados, o Neutral ganhou nome para o segundo momento: `OnBoardSettledAtNeutral`
dispara **depois** do delta da névoa (o CLAUDE.md explica qual escutar).

**A descoberta da v9.3.2: aula sem prédio é o primeiro mapa em que a IA rebelde não
tem âncora.** O capturador desistia, o reparo marchava para a bandeira do próprio
time e o Apache orbitava o soldado aliado. Agora o rebelde **caça** o inimigo
visível (com último recurso que ignora a decisão de ataque), o reparo sem prédio
que conserte cai, e o Apache caça no Operacional. O save passou a guardar a aula e
o mundo (o manifesto leva o `mundoId`). O motor das aulas ganhou fala avulsa,
`completeCommand`, relógio (`TURN_REACHED`) e alcance de serviço.

**A descoberta da v9.3.1: tutorial se testa tentando quebrar.** O autor jogou a
aula 1 da Caserna de ponta a ponta e cada atalho de aluno virou trava ou regra:
desembarcar um soldado só (impasse), capturar antes da hora, voar sem fazer
nada. Duas regras saíram: **bloqueio de roteiro é do aluno** (a IA levou bronca do
Sargento e ficou presa) e **a fala 0 roda depois do turno 1** (o início do turno
apagava o `acted CH`). A aula 1 fecha com fala final e volta automática à
Campanha; a aula 2 ainda não foi jogada.

**A descoberta da v9.3.0: a aula já era dado; só o palco era cena.** O
`TutorialData` carregava falas, tarefas, comandos e travas, e a cena só servia de
palco. A aula virou **um quadrante do mundo Academia** (Caserna → Soldado/Cabo →
aula), lançado pelo botão **Tutorial**. Numa aula, **só as tarefas decidem o fim**
(`UsesMatchEndRules`). O inimigo é escolha da aula (automata ou IA rebelde). O
roteiro fala na **coordenada do mundo de autoria**. Escrever aula:
[`tutorial/sintaxe.md`](tutorial/sintaxe.md) e
[`tutorial/aulas_caserna.md`](tutorial/aulas_caserna.md). **Nada rodou em Play
ainda**, e as duas aulas criadas estão vazias. A v8 fechou; os relatórios dela
estão em [`Versões/`](Versões/).

**A descoberta da v9.2.2: "lento" é duas coisas.** O testador (Easy, celular,
uma tarde num mapa de ~1 h) disse que o jogo "é lento". Tempo de **decisão** é
identidade (ritmo de xadrez); tempo de **execução** é atrito de toques, e só ele
se mexe. Daí a tela de Configurações (Partida × Preferências, Modo Turbo, Ação
Direta, Tela Cheia), a Ação Direta a partir da seleção (tocar no alvo anda até a
célula legal e para na confirmação) e, de tabela, o inventário do que ainda vaza
na névoa ([`pendencias do mvp.md`](pendencias%20do%20mvp.md)): **menu, mapa e
Jornal leem a mesma memória**. Nada disso foi visto em Play no fechamento.

**A descoberta da v9.2.1: a IA "decide e se manda".** O `DecideUnitAction` é uma
cascata em que a primeira resposta não-nula ganha. O autor desenhou o
substituto, e ele está em
[`AI Behavior/contrato_questionario.md`](AI%20Behavior/contrato_questionario.md):
todo papel responde as **mesmas nove casas**, numa ordem própria; a primeira
ação com SIM é só a **preliminar**, e as políticas revisam. A **missão** é outra
coluna do mesmo papel (Reparo, SOS, Guarnição...). O motor não pode conhecer
papel, senão o n × n volta. O degrau 1 (o observador, que só anota) está em
código; **nada do dia foi visto em Play**.

```text
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
v8.5.1   o que atravessa a cena            save por endereço, isPlayable, 0b
v8.5.2   a forma casa com o dado           quadradinhos, paridade, fim de partida
v8.6.0   a etiqueta muda de dono          rally em lista, eixo escrito, 3 perfis
v8.6.1   configurado ≠ valendo            perfis autorados, portões fechados
v9.0.0   a mesma linha                    visão de regra única; Save Inspector
v9.1.0   o save manda na partida          load aplica o save; menu no turno da IA
v9.2.0   aguenta o celular de quem testa  toque, áudio/texturas web, sem LTO
v9.2.1   a IA pergunta antes de decidir   contrato do questionário; reparo pela linha
v9.2.2   o primeiro estranho jogou         Configurações, Ação Direta, névoa com uma memória
v9.3.0   a aula vira um quadrante          Academia, regras de fim da aula, captura/spawn no roteiro
v9.3.1   a primeira aula fecha             travas só do aluno, atalhos fechados, fala final, volta automática
v9.3.2   a aula 2 contra IA que briga      save da aula e do mundo, rebelde caça, peças de roteiro
v9.4.0   revisão do motor                  combate em fonte única, IA só simula o sensor, Neutral em 2 momentos
v9.5.0   uma IA validando a IA             Claude joga pelo Unity CLI; produtora e compra corrigidas; fatos de medalha
```

**A descoberta da v9.2.0:** o Simulator da Unity não reproduz o navegador do
celular. "Funciona no Editor" não diz nada sobre memória. O celular morria **por
memória, sem erro no log**: música decodificada inteira, texturas DXT
descomprimidas no processador e logs. Mede-se pelo cabo (`adb` + DevTools
Protocol, `dumpsys meminfo` PSS), nunca por dentro da aba. Toda tela nova se
valida em **teclado, mouse e dedo**.

**A descoberta da v9.1.0:** a cena guardava estado de partida. Os flags de
início de turno, salvos como `1` na Batalha, prendiam a cortina depois do load.
Agora esses campos são `[NonSerialized]`. Se um campo do `MatchController`
muda durante a partida, ele não pode estar na cena.

**O MVP virou tela.** Menu → Campanha → Batalha → volta → o mapa pintado, o
placar, e o registro de quem tomou o quê, em quantas rodadas e **por quê**. O
autor jogou o Q1 até a rodada 3 com o perfil Médio.

**O major v9 se chama "Auditável".** O que está configurado tem de ser o que está
valendo, e dá para provar olhando. A direção vem do
[guia de modding](modding/guia%20de%20modding.md) do autor.

### A regra da visão (v9.0.0) — uma só, para ver, detectar e atirar

```text
ORIGEM   o terreno empresta EV a quem o ocupa?  sim → o emprestado   não → 0
         aeronave / submerso                     → EV da camada (DPQ para Ar)
ALVO     enxergar → cume do hex     detectar → camada da unidade
CONSTRUÇÃO  ocupante com altura: terreno empresta → emprestado, senão EV Base
            com Block LoS SUBSTITUI o EV do hex (cidade na montanha = 2)
            revela com a MESMA linha, partindo da altura dela; detecta só o próprio hex
```

Antes havia duas regras de origem, a construção não tinha altura (morava numa
lista do terreno) e revelava o disco inteiro sem linha. O autor definiu a regra
em quatro frases; a mais importante foi *"não tem essa salada"*. Se uma regra de
jogo parece pedir dois caminhos, **pergunte qual é a regra antes de oferecer
opções.**

### A descoberta da v8.6.1, que deu nome ao major

> **O que está configurado não é o que está valendo.**

Cinco vezes num dia, uma tela, um asset ou uma lembrança mostrava uma coisa e o
jogo fazia outra, e nenhuma deu erro: o Base Preset no Inspector, perfis copiados
do Gulosa, números do perfil que ninguém lia, a âncora da Retaguarda lendo tropa
real, e o "limpar o progresso" (que existe, sim: está no save do slot 1; ver a
errata no relatório v9.0.0).

### As descobertas da v8, comprimidas

- **v8.6.0, uma resposta só não serve a mais de um respondente:** a etiqueta muda
  de dono (rally em lista, eixo escrito, portões por capacidade). Se é preciso
  **desligar** metade da resposta para a outra funcionar, a entidade está errada,
  não o flag.
- **v8.5.2, a forma casa com o dado:** contável vira quadrado, contínuo vira
  barra.

### As descobertas anteriores, que seguem valendo

> **v8.5.1 — a cena `Campanha` parece uma partida e não é.**

Ela tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu
da cena-base de batalha. Perguntar *"estou numa partida?"* olhando o que **existe**
na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao
**da própria cena**, não a qualquer um.

> **v8.5.0 — cor não é identidade. É uma fantasia que o slot veste por uma
> partida.**

As duas cores são escolhidas no menu. Tudo que atravessa a fronteira entre autoria
e partida — ou entre uma partida e a seguinte — endereça por **slot**, e a cor se
resolve só na hora de pintar, por `GetTeamIdForSlot`. Vale para o tint do mapa, os
quadradinhos, o registro do vencedor e o dono de cada construção.

---

## Vocabulário

```text
MUNDO       uma cena de autoria + UM asset. O globo inteiro, desenhado de uma vez
 └─ BLOCO       Europeu · América do Norte · Rússia    ← o jogador escolhe
     └─ CAMPANHA    Europa · África
         └─ QUADRANTE   Inglaterra · Congo...          ← aqui se luta, e é o que é assado
```

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável onde se joga |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção |
| **slot** | quem é o dono. Slot 0, slot 1. **A cor é roupa dele nesta partida** |
| **turno** ⚠️ | **duas palavras para a mesma coisa, e elas discordam.** O `currentTurn` do `MatchController` só incrementa em `CloseRoundAndAdvanceToFirstPlayer` — ou seja, conta **rodadas** (o ciclo completo de jogadores). Mas o código o chama de "turno" em todo lugar: HUD da batalha, cortina de privacidade. O **autor** chama isso de *rodada* e reserva *turno* para a jogada individual. O registro do quadrante já usa o vocabulário do autor (`RODADAS: 3`); o HUD ainda não |

Um quadrante **contém** setores.

**ids são técnicos** (`feijao-torto`), nomes são livres (`Feijão Torto`). O id é o
que o save grava e o que o endereço casa; a bancada avisa quando ele tem acento ou
espaço.

---

## O fluxo, e onde cada peça mora

```text
Tela de Entrada   PanelMenu:980   cor, cor, dificuldade, preset → PartidaConfig
      ↓           espera o SFX de confirmação terminar (WaitForSecondsRealtime)
Campanha          CampaignSelectionController   mosaico, setas, tint por dono
      ↓           Set(...) + SetDifficulty + SetQuadrante → LoadScene("Batalha")
Batalha           QuadranteController (-9000)   pinta terreno, construções,
      ↓                                          camadas, rotas e TROPA INICIAL
                  MatchController (0)            aplica a config (ou já foi aplicada)
      ↓           vitória/derrota → grava o SLOT vencedor → Enter
Campanha          republica a config na volta, e o quadrante aparece na cor do dono
```

**Toda a travessia é `PartidaConfig`.** Ele é de **consumo único**: quem produz
chama `Set`, quem consome chama `Apply` + `Clear`. Por isso a volta tem de
republicar — foi o que quase quebrou o laço em silêncio.

---

## O que existe

```text
Assets/Scripts/Campanha/    MundoData · BlocoData · CampanhaData · QuadranteData
                            INoDoMapa · QuadranteController
                            ConstrucaoAssada · UnidadeAssada · CamadaAssada · RotaAssada
                            EconomiaInicialSlot          ← caixa inicial, autoral
                            ProgressoDaCampanha          ← Concluido() nos 3 níveis
                            CampanhaManager              ← ponte da cena p/ o placar
                            CampaignProgressStore        ← por SLOT, e dentro do save
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            RoadRoutePainterWindow · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Scripts/UI/          BarraVencedorController      ← um quadrado por quadrante
                            PanelVitoriaController       ← os botões do fim de partida
Assets/Prefab/Managers/     AudioManager.prefab          ← nas 3 cenas do fluxo
Assets/Prefab/              Panel_turn · Panel_vitoria · MenuRoot · Cursor
Assets/Scenes/Autoria/      Fixture (26 construções, 6 trechos) · Mundo
Assets/Scenes/              Campanha · Batalha           ← as duas no Build Settings
```

O fixture, quatro quadrantes de tamanhos diferentes:

```text
bloco A · Auridia
 └─ campanha A_IA · "A invasão a Auridia"
     ├─ A_IA_Q1  Feijão Torto     (-18,10)  16×17   272 tiles
     ├─ A_IA_Q2  Terra Firme      (-18,-9)  21×20   420 tiles
     ├─ A_IA_Q3  Peixe Pequeno     (-3,10)  35×17   595 tiles
     └─ A_IA_Q4  Tubarão Branco     (2,-9)  30×20   600 tiles
```

29 construções assadas, **todas no Q1 e no Q2**:

```text
A_IA_Q1  HQ + fábricas cada lado    sem caixa: jogável, a renda do HQ (6000) entra antes da 1ª compra
A_IA_Q2  HQ + fábricas cada lado    10000 por slot, chinook no slot 0
A_IA_Q3  NENHUMA construção         2000 no slot 1, que não compra nada
A_IA_Q4  NENHUMA construção         2000 no slot 0, que não compra nada
```

Sem HQ, os dois lados do Q3 e do Q4 são facção sem QG (v8.6.1, Frente 4; desde a v10.8.0 é só "todos sem vaga").

---

## Onde eu parei

### O que a v10.11.0 deixou — ver a invasão e o EVAC andando

1. **A marcha em Play.** A capitania nova (`RefreshRallyAdvanceCaptains`, `RallyPoints.cs`)
   nunca rodou numa partida. O save do slot 3 já está no T14 e não tem o grupo do T12: precisa
   de um save de antes do GoGreen. Esperado no log: `[Rally] <setor> capitao #a->#b`, `preserva #id:
   carona da invasao publicada`, `espera carona persistente: estagnacao congelada`.
2. **Marca de 2026-11-13 09:16, T15 slot 0:** "a AI está montando o orçamento da invasão? a
   pressão por Chinook tá boa?". Só se responde vendo a invasão em Play com a fila nova.
3. **EVAC naval** num mapa de praia; nenhuma entrega de EVAC foi vista em partida.
4. **Quem segura o capitão no mar** (ressalva da P2): degrau 7, com a bandeira naval.
5. **Consolidar:** interceptador sem terciário e ar com vaga na área do rally, segundo fechamento
   seguido na lista "Prontos".

### O que a v10.10.0 deixou — os 3 s que sobraram e os logs

1. **Logs sem gate antes da versão:** `docs/checklist_logs_publicacao.md`, seção C. Decisão
   do autor entre a chave do build (`Debug.unityLogger.filterLogType = Warning` fora do Editor,
   recomendada) e flags novas na janela `Tools ▸ Publicação ▸ Checklist`. O
   `[AI Turn Transition Timing]` (~15 linhas por turno) entrou hoje sem flag.
2. **Commit pré-compra de 1,5 s:** `[AI Commit Heavy]` soma 1.504 ms com todos os parciais em
   0 ms; o custo está fora dos trechos cronometrados de `CommitAIWorldHeavy`
   (`AIController.WorldCommit.cs:193-256`). É a próxima tela morta visível. Depois dela,
   `PressaoCaronaService.Read` (0,4 s por slot) e o setup do plano (870 ms antes do Passo 1).
3. **`pergunte-me` da Correção 2:** `PodeMirarSensor.TryResolveCellVision`
   (`PodeMirarSensor.cs:1868`) duplica `ObservationCellService.TryResolveCellVision` sem o cache
   por refresh, e **não são idênticas** (sem ocupante: Land/Surface na cópia; camada por
   `TryResolveObservationLayer` no serviço). O CLAUDE.md diz que o serviço é a base.
4. **`CalcularCaminhosValidos` acima do MP** segue enumerando passeios para Handoff, Initiative,
   Progression e Router. Podar estados dominados mexe no bônus de estrada: `pergunte-me`.
   `TryGetLandMovementDistance` custa 1,8 ms por chamada; hoje não é gargalo.
5. **Consolidar:** interceptador sem terciário e ar com vaga na área do rally cumprem os três
   sinais (relatório v10.10.0, "Prontos para consolidar"). Primeiro item desde a v10.8.0.
6. Carregados da v10.9.1 (abaixo): marcha de Golf, invasão por ilha, teto 60 vs 40, passageiro
   segurando vaga, eixo forte/fraco. Nenhum andou hoje.

### O que a v10.9.1 deixou — ver a marcha

1. **A onda de Golf andando:** no save do slot 3 a partir do T13, logs `[AI Rally]` `ESPERANDO`,
   `solta retardatário`, capitão #10 retomando. Risco nomeado: capitão a pé sem Chinook em 3
   turnos = `invasão FRACASSOU` e remontagem (`UpdateInvasionMonitor`).
2. **`pergunte-me` da invasão por ilha**, quadro do autor: Ruptura e Dissuasão atacam; Chinooks
   buscam primeiro quem já está no montinho (o `Transportador.Shuttle.cs` não sabe o que é
   rally); aeronaves novas vão direto ao combate (já é assim). Régua candidata: capitão é quem
   alcança o alvo, o primeiro a embarcar; grupo "com o capitão" enquanto ele está a bordo. Quem
   segura o capitão no ESPERANDO continua aberto.
3. **Teto de unidades:** dump do T12 deu 9 de 25 a mais de 6 PM do objetivo com 4 embarcados.
   Repetir por dois ou três turnos; se "longe" crescer com "embarcados" parado, 60 é alto para a
   logística do mapa. `MaxUnitsPerTeam` vive no `Match`; candidato a ir para o quadrante.
4. **Marca não lida:** 2026-11-12 00:45, T11 slot 1, "a compra veio pra fechar o rally né?".
5. **Planner:** passageiros segurando vaga de setor já atendido (Hotel, handoff); eixo forte e
   fraco (`strongWeakSidePolitic`, só fire support, desligado) contra a pulverização.

### O que a v10.9.0 deixou — ver o rally sair

1. **D2 em Play:** save do slot 3, turno 11, `ai show rally 1`. Esperado: Hold 5/4, Suporte
   "(opcional)", Força com exigido pequeno (dois inimigos perto), Apache na base em Fora, Caça B
   no Core. Depois GO_GREEN em Golf; logs `ESPERANDO`, `solta retardatário`, capitão retomando.
   Dono: `EvaluateRallyReadiness` e `UpdateRallyAdvanceWaiting` em
   `AIController.PlanEvaluator.RallyPoints.cs`.
2. **Flag do caça:** até marcar `Assalto` em "satisfaz também" de `Interceptador` e
   `InterceptadorDeDisruptor` no `UnitRoleCatalog.asset`, a vaga de ruptura segue pedindo blindado.
3. **D4 = formação ativa** (Ruptura na vanguarda e flancos, resto na retaguarda, usando a
   `RallyArea` da tela) só depois do item 1. **Degrau 7** = rally naval (core 4 fogo de suporte
   naval + 4 embarcados ou 2 transportes carregados; navios bombardeiam e esperam). Pergunta
   aberta: quem segura o capitão no ESPERANDO — hoje todo participante, logística inclusa.
4. **Doutrina:** `docs/AI Behavior/Forcas.md` não existe; lista completa no relatório v10.9.0.

### O que a v10.8.0 deixou — jogar uma partida sem QG e uma com

1. **Facção sem QG em Play:** procurar `sem QG: oportunidades livres=N` e `origem=capture-opportunity`
   no shopping; fila de carona não vazia; APC entregando carga toda sem vaga sem esperar GoGreen.
   Conferir as `sellingRule` das instâncias onde a rebelde comprava (Barracks, Docas, Garagem APC,
   Hidrobase, Terminal Rodoviário) — a flag por catálogo morreu e nada foi migrado.
2. **IA com QG em Play:** o capturador sem vaga agora **caça** sem capturável à vista
   (`TryResolvePlanlessHuntAnchor`, fog-honesto); aceito como fallback, não visto. E as demandas de
   AA/caça podem mudar: o SAM de alcance mínimo > 1 passou a contar no guarda-chuva.
3. **Caça A:** ver a compra pela progressão (`origem=` elite "controle do ar"), a terceira linha
   da reserva e os tiers `Exposed`/`Covered`/`Operational:Hunt` no AI Step; três perfis.
4. **Doutrina:** lista completa no relatório v10.8.0; nenhum item pronto para consolidar.

### O que a v10.7.0 deixou — fechar a Aeronáutica em partida

1. **Ver a próxima compra:** Caça A/B fora do counter terrestre; cadeia Tanque B → A não
   contorna isolamento. `AIShoppingPlanner.CounterPressure.cs` e `.Demand.cs`; quatro fixtures
   passaram após o último ajuste, mas falta a compra real. Não confundir outra origem com
   regressão do rally: ler `origem=` e a escolha da elite.
2. **Shopping híbrido/ponderado:** testar mistura, cobertura e invasão final no mapa real;
   medir custo de mapas do SectorManager com muitos perfis/alvos. Poupança não foi reescrita;
   migração de compromisso antigo não foi testada ponta a ponta no ledger da partida.
3. **Rally:** entrega dos dois passageiros vista; compra com acesso vista no registro. A proposta
   completa de `docs/rally/rally_proposta.md`, reforços e liberação de montinhos continua parcial.
   Não trocar os gates de prontidão por uma regra nova durante o fechamento.
4. **AI Step:** conferir envolvidos rosa, vínculo e foco da presa operacional; `TraceAIReviewParticipant`
   já é usado. A perseguição do Caça B foi vista; não significa que perseguia o Bombardeiro.
5. **Arquitetura:** mapa atual publicado, sem refatoração executada. A terceira só depois da
   revisão da Aeronáutica; procurar primeiro os predicados/serviços que já existem.
6. **Doutrina:** lista completa de débitos, incluindo os herdados, no relatório v10.7.0.


### O que a v10.6.0 deixou — ver rodando

1. **Só a régua do supridor foi vista em Play.** Reserva de pouso, EVAC sem avanço → HQ, fusão
   segura, célula futura do supridor e swap que captura passaram só nos testes de `tools/ai_test/`.
2. **`AircraftRepairRecovery` avaliada duas vezes na mesma decisão** — fora do `efb0519` de
   propósito.
3. **Doc da régua do atendimento:** `Logistica.md` não descreve faixas nem prazo.
4. `pergunte-me` na Mylarhis existe sem commit, por escolha do autor.

### O que a v10.5.0 deixou — o resgate

1. **Serviço do Comando depois de load:** o autor viu o serviço rodar de novo após carregar. Se o
   save era do meio do turno, a IA pode decidir o estágio de retomada (`RunAITurn`, `resumeStage`)
   **antes** de o load restaurar `aiRuntimeStage` (`SaveGameManager.cs:2308`) — e cobrar o serviço
   duas vezes. Sinal: `Stage 1 ja concluido pelo save` no Console. Não verificado.
2. **Ver rodando:** anel do HQ (seguro e contestado), EVAC conjunto e dupla no anel — só nos
   testes do Codex (`tools/ai_test/verificar_evac_predio_vago.cs`, fora do Play).
3. **Volume do `AudioManager.prefab` 0,49 → 0,70** (`e78eb0a`): comitado como autoria; se veio do
   slider do `Panel_Config` em Play, é estado de runtime gravado no prefab das três cenas.
4. **`startOnPause: 1` na Batalha:** é o "IA parada depois do load" (F12 destrava). Desligar antes de
   publicar (o checklist de publicação faz).
5. **Doc do Disruptor:** nenhum doc de doutrina o descreve (só o rascunho). Lugar: `Assalto.md` ou
   `Papeis.md`.

### O que a v10.4.0 deixou — a Fase 2 acorda

1. **Ver rodando a Fase 2 com inimigo** (`e72deff`): linha de frente/retaguarda
   (`Services/AIController.Backline.cs`), ferido na vanguarda, reparo pela linha, fogo de suporte
   atrás da linha, rally, logística. Tudo escrito, nada visto. Se o posicionamento ficar estranho,
   é daqui.
2. **Revisão do tabuleiro mudando durante a decisão:** o AI Step do Chinook gravou
   `executionSameAsDecision=False`. Suspeito: `SimulateDisembarkFromCell`
   (`SetCurrentCellPosition` provisório) mexendo na revisão — provisório tocando estado
   confirmado. Não verificado.
3. **Tempo do Chinook (3,3 s):** 24 `validPaths` numa decisão (1,76 s) — montar por orçamento uma
   vez; `toolProgression.TransportDelivery` 1,3 s; MelhorDesembarque 385 LZs (podar LZ sem vizinho
   na faixa de nenhum passageiro).
4. **Aposentar o plano B do courier** (laço da prioridade 1 em
   `Units/Transport/AIController.Transportador.Air.cs`) depois de mais testes sem ele aparecer.
5. **Ver comprando:** Caça pela vanguarda (`origem=vanguarda`, pré-compra após romper a
   poupança), breakthrough com escala, escada com cadeia (SAM via AAA, Caça A via Caça B), Avião
   Tanque (`origem=vaivem-aereo`, prioridade 18 — provavelmente perde no carrinho; pré-compra é
   decisão do autor).
6. **Entrega Operacional no APC e no navio** — o portão é compartilhado; só o aéreo foi visto.
7. **Decisões do autor:** demanda do caminhão pela régua do vaivém (*"contador de feridos é
   idiota"*); degrau "pousa agora, anda dois turnos" (jogada, não viagem); delta de 3 SAM
   explícito (hoje o guarda-chuva faz de teto).
8. **Errata:** o commit `43ea971` (churn) diz "canvas vazio"; o canvas é o rascunho do autor
   "A Escalada da Demanda". Arquivos certos, mensagem errada.

### O que a v10.3.0 deixou — régua no lugar de igualdade

1. **Ver rodando o que só compilou:** anel e tolerância do counter
   (`AIShoppingPlanner.CounterPressure.cs`, log `counter … sem compra — cobertura … >= 60%`),
   a escala de capturadores por prédio (Médio no Peixe Pequeno: o caixa trava em infantaria?),
   o anel antiaéreo por presa (`presa em primario/secundario` no motivo da demanda), a escolta
   pedindo `InterceptadorDeDisruptor`.
2. **Custo do anel Operacional do counter** — não medido. Lentidão da IA no Feijão Torto:
   começar por aqui (há cache por inimigo/célula/turno).
3. ~~Demanda "bombardeiro inimigo → Caça A"~~ — vanguarda pela presa (v10.4.0, só compilou).
4. **`aiAttackOnlyListedTargets` (Caça F, BF) sem leitor, de propósito** — quando um furtivo
   "revelar por quê?", é ele. Hoje classe fora da lista vale Terciária e o "ar primeiro" atira.
5. ~~SAM bloqueado pela cadeia de elite~~ — "vende" respeita a cadeia e a escada desce (v10.4.0).
6. **Freio da conscrição pela fila de carona** — continua decisão do autor.
7. **Batalha agora com `isAI` 1 nos dois slots e `hardMode` 1** (`a036cfc`, autoria) — rever
   antes de publicar.
8. **Docs de doutrina:** lista no relatório v10.3.0 (Shopping.md §6, Papeis/ficha_do_papel/
   Assalto/Vigilancia — Interceptador em dois; a fórmula de slots por setor não tem doc).

### O que a v10.2.0 deixou — compra aérea e o primeiro build para fora

1. **Ver rodando o que só compilou:** swap pelo Tático da vizinhança
   (`AIController.Capturer.Swap.cs`), o piso de Chinook da fila de carona (origem `fila-carona`
   no log do carrinho), a estrada do fundo em "Selecione o Mapa" (precisa do "Assar TODOS
   (quadrantes + fundos)") e a guarda do Panel_helper.
2. **No build Web:** volume persiste ao reabrir? Retrato e logo em 1024 ficaram nítidos?

O caminho do Caça B (respondido: pré-compra da escolta) e a Metranca contra inimigo fora da
ilha (resolvido pela regra "outra ilha" do counter) saíram desta lista na v10.3.0.

### O que a v10.1.0 deixou — a força aérea no Peixe Pequeno

1. **Ver rodando o que só compilou:** negação de escolta do Bombardeiro (`NegaEscolta:` no
   tier), ganância no desembarque (`ganancia: #N desce…`), corrida pesando no MelhorEmbarque
   (`corrida=x,y posicao=n/m`), espera fora da produtora e cabeça de praia
   (`AIController.Capturer.Embark.cs`).
2. **Foco no mesmo alvo** para dois Apaches ("concentrar, não dividir"): não existe; cada um
   escolhe o seu (`TryResolveCaptureInterdictionAnchor`).
3. **Docs de doutrina desatualizados:** a lista está no relatório v10.1.0 (Assalto.md,
   magnetic_tabela.md, Papeis.md, ficha_do_papel.md, Capturador.md, Transporte.md, Shopping.md).
   Nada pronto para consolidar ainda.
4. **AICaptain.asset** continua sem leitor — decisão do autor: é o "guru"; migrar um papel por
   vez, Disruptor como piloto.
5. **Disruptor não aéreo** (drone de chão, sniper): o pipeline terrestre não tem a âncora CCP, e
   o shopping o contaria como ataque aéreo.
6. **Courier escolhe o rumo pelo primeiro passageiro** (`PassengerTarget`), não pelo destino mais
   perto nem pela missão do transporte.

### O que a v10.0.1 deixou — ver rodando no Peixe Pequeno

1. **Procurar nos logs do Shopping:** `foraDaTerra=N` (counter), `(ilha sem ligacao por
   terra)` (elite e escolta de operação), `ameaca aerea: X/Y … dentro do anel`,
   `vigilancia aerea: sem demanda`. Conferir se o Chinook do Difícil sai no T1 (imposto de
   conscrição sem elite).
2. **Planner:** `[Plan][TrocaTatico]` quando um capturador com o prédio no Tático está em
   outra vaga. Observar se o desconto de −20 puxa capturador para outro eixo.
3. **LZ do Chinook pousado** cai ao lado do passageiro, nunca em cima
   (`MelhorEmbarqueService.CanAircraftLandForPickup`). Com a aeronave pousada o terreno de
   pouso segue sem teste: o `PodePousar` só responde para quem está no ar.
4. **Apaches demais?** A escolta "risky" vira Apache por setor e soma com o pacote.
5. **Compra de prioridade 20 no troco** da de 15 que não coube: problema do carrinho, para
   toda demanda. Não tocado.
6. **SAM/AAA posicionados** (ideia do autor): SAM nos cantos expande a cobertura, um cobre o
   ponto cego do outro, AAA escolta o SAM. Posicionamento, não compra.

### O que a v10.0.0 deixou e segue aberto

1. **Ver rodando** a `ilha invadida` religando tanque e MBT quando o inimigo desembarcar
   (`TryFindEnemyOnOwnIsland`).
2. ~~Counter compra tanque contra blindado na ilha do inimigo~~ — feito na v10.0.1.
3. **Capturador com vaga que não está revezando** ainda pode tomar o prédio do herdeiro
   escolhido (`HandoffHeirUnitId`); a trava só vale para a ponta que reveza.
4. **Prédio neutro dentro da própria base** (aeroporto do Q3) não vira objetivo; só o
   oportunista pega. Teste proposital do autor.
5. **Feijão Torto depois da família:** o Obus Leve passou a contar como assalto e a vaga de
   fogo aceita AAA. Observar se a composição ficou boa.
6. **Medir** o mapa de custo real por destino no `MelhorEmbarqueService`
   (`MelhorEmbarqueDestinationCostMaps`); aposentar `ShuttlePickupRange` depois.
7. **Escrever a doutrina do blitz** em `AI Behavior/Capturador.md`.

### O que a v9.6.0 deixou — revisão lance a lance do núcleo, em andamento

O fluxo: o autor joga com AI Step ligado e marca. "Adicionei um estranho" = ler a
**última linha** de `ai_step_marcas.jsonl` (PowerShell `ConvertFrom-Json`; o
Python da máquina quebra no `import json`). Os passos vizinhos ficam em
`persistentDataPath/AIReviews/<sessionId>/`.

1. **Ver rodando** a postura (`AIWorldSnapshot.CalculateStance`) e o log novo do
   handoff (`DescribeBlitzkriegHandoff`). Save do slot 2 da 1ª marca, T2, slot 1: a fila
   deve dizer `stance=Tactical`.
2. **Ver rodando** as correções da v9.5.0 que ainda não apareceram: sair da produtora
   capturando (`BuildVacateBatch`), papel antes da liberação (`Router`).
3. **Instrumentar o carrinho de compras no AI Step:** `BuildBestRoleShoppingCart` é o
   caminho em uso, e o `UnitPicker` instrumentado não é. Sem isso, `candidates` vem
   vazio em toda compra.
4. **Pergunta aberta ao autor:** quanto o anti-infantaria pode furar a prioridade de
   Assalto ("por que Tanque B?", em [`ai_step_investigacao_2026-11-04.md`](ai_step_investigacao_2026-11-04.md)).
5. **Depois do núcleo:** a comparação das 3 rodadas Difícil do autor (replay + AI Step),
   elite, Aeronáutica num mapa pequeno, e a Marinha começando por doutrina.

### O que a v9.4.0 deixou — o motor mudou por dentro, o autor testa por fora

Só compilou, exceto o trace de combate (dois combates colados pelo autor
bateram com a conta à mão). O que falta, com o sinal a procurar no Console:

1. **Os 7 `Retreat` que saem de `*Executing`** — suprimento (2), desembarque (2) e
   embarque (3, incluindo o "embark failed" *depois* da animação,
   `TurnStateManager.ScannerPrompt.cs` ~2788). Cada um tem de desfazer tudo o que
   a execução já tocou. **Tarefa do autor, em Play.** Sinais:
   `[FSM] Delta confirmado pendente aplicado numa volta ao Neutral por Retreat` e
   `Retreat chamado a partir de estado reset-only` (este só com
   `enableTurnStateRuntimeLogs`).
2. **Cena de caos** (caças, helicópteros, soldados, navios, subs): a IA cruzando
   todos os pares. Contar `noSensorOption` — muitas num papel só = esse papel monta
   pares (alvo, célula) que o sensor recusa.
3. **Teste automático da fórmula.** `CombatFormula.Resolve` roda sem cena; os dois
   traces da v9.4.0 (relatório §2) são o gabarito. Primeiro teste de gameplay do
   projeto.
4. **Migrar** `HexCohabitationVisualManager` e `ConstructionManager` para
   `OnBoardSettledAtNeutral` (hoje se salvam pelo `OnFogOfWarUpdated`). Não urgente.
5. **Watchdog:** `[Watchdog] esperando '…' ha Ns` aparecendo = a IA está presa
   nesse ponto. Ele só avisa; não destrava.
6. **Fragata com alcance 0** é experimento — decidir na review da Marinha.

O `[Combate] Resolve` só aparece com `enableTurnStateRuntimeLogs` ligado, que está
**desligado na Campanha e no AI Ground**.

### O que a v9.3.2 deixou — a caça rebelde ainda não foi vista atirando

1. **Ver a caça em Play.** As duas últimas correções (segunda passada sem
   `PassesAttackDecision`; varredura de tiro sempre, porque `HasEnemyInEngageRadius`
   mede com `Vector3Int.Distance`) não voltaram testadas. Log a procurar:
   `caca (ultimo recurso…) move+ataca` ou `caca: sem tiro… fecha distancia`
   (`AIController.Capturer.Rogue.cs`).
2. **`HasEnemyInEngageRadius` segue em reta na grade** para os capturadores com
   plano. Corrigir muda a IA com QG: decidir com o autor.
3. **Save de aula**: carregar no meio, pela aula aberta e pelo menu, ainda não foi
   visto. Saves anteriores à v9.3.2 não têm `mundoId` no manifesto (dançam uma vez).

### O que a v9.3.1 deixou — aula 1 jogada, aula 2 não

O roteiro é o JSON em `docs/tutorial/` (fonte de verdade); **todo ajuste exige
Importar no asset** — o jogo lê o asset, não o JSON.

1. **Rejogar a aula 1** depois das últimas mudanças (nenhuma foi vista em Play):
   contador de captura decrescente, `op_01` como gatilho interno, fala de vitória
   com o painel esperando 3,5 s, barra de 6 s voltando à Campanha.
2. **A aula 2 encolheu** (só reabastecimento); a extração está em
   `caserna_soldado_3_rascunho.json`, ainda sem mapa (`aulas_caserna.md`, Aula 3).
3. O rebelde que nasce **fora da tela** é intencional. Se ele atirar sem nunca ter
   entrado no enquadramento, pôr um `pan` no turno inimigo.
4. **Abertos conhecidos:**
   - o aluno é sempre o slot 0;
   - o eixo do slot 0 do Fixture mudou (`0,2,3,8 → 2,3,8`) sem autor conhecido:
     conferir.

### O que a v9.2.2 deixou — primeiro jogar, depois o replay

**Só compilou.** Roteiro de Play, na ordem que mais rende:

```text
Configurações   abre pela Tela de Entrada, pelo Gerenciar da Batalha e pelo menu da
                Campanha; setas com destaque verde; Sobre na frente; Tela Cheia na Web
Ação Direta     tanque anda até o morro e mira; artilharia só atira parada; embarque
                num caminhão a 2–3 hexes; cancelar no meio desfaz tudo
captura         prédio seu capturado longe: antes do seu turno o menu NÃO oferece
                captura; depois do Jornal, o mapa pinta inimigo e oferece
transacional    cancelar movimento pela estrada não completa objetivo do tutorial;
                MovementPath do replay não vem vazio
```

**Próximo código: o descarte do replay ao voltar de submenu.** `HandleCancel`
(`TurnStateManager.StateMachine.cs`) e `HandleScannerPromptCancel`
(`TurnStateManager.ScannerPrompt.cs`) chamam `DiscardPendingCombatCinematicTrack`
no **topo**, antes de saber se é "voltar uma etapa" ou "cancelar a ação". Mover o
descarte para os ramos que voltam à seleção ou ao `Neutral`, lendo um a um.

**Decisão pendente do autor:** "falhou depois do compromisso = conclui e marca
como agiu" (como a captura já faz) para transferência e suprimento — entra no
contrato transacional se ele aprovar.

**Fora do MVP:** Zona de Controle, medida em
[`implementação pós mvp.md`](implementação%20pós%20mvp.md).

### O questionário — o tronco da IA agora

O contrato é o mapa; leia antes de mexer em decisão de papel. Estado:

```text
degrau 1  observador do Capturador        ESCRITO, só compilou, nunca rodou
          Assets/Scripts/Match/AI/Questionario/ → questionario_observador.log (raiz, só Editor)
degrau 2  Capturador decide pelo questionário, atrás de toggle   NÃO COMEÇOU
degrau 3  os outros cinco papéis                                 NÃO COMEÇOU
```

**Primeiro passo da retomada: jogar com o observador ligado** (Inspector do
`AIController`, *Questionário (observador)*) e ler o log. Procurar `<< DIVERGE`
(onde o código de hoje e o questionário discordam) e "prédio no tático, mas
PRETO" (rodadas perdidas por névoa).

O que foi decidido e espera código: o pedido de spotting (§6.3), Swap/Blitz/
Vacate como políticas (§6.7), quem fica com o prédio — com plano → Capturador
antes de Combatente → quem **fecha** primeiro (§6.8). Os quadros "Papéis em
condições normais" e "Missões" são do autor; o Reparo está desenhado (§11.1), as
outras seis missões têm só o nome.

**Abertos que dependem do autor:** #12 (qual missão ganha quando duas disputam a
peça) e #13 (o reparo ainda tira tanque e soldado de prédio ameaçado quando há
aliado são por perto — pela regra do autor, sair é só do SOS).

**Só compilou (v9.2.1)**, além do observador:

```text
fusão em reparo no perfil   Fácil todos · Médio só capturador · Difícil ninguém (perfis salvos)
ferido na vanguarda         sai primeiro na iniciativa (IsWoundedInVanguard, régua da Retaguarda)
reparo pela linha           não-elite recusa prédio à frente da linha, aceita atrás (sem voltar ao HQ)
fogo de suporte em reparo   atira de volta parado no prédio, em vez de sair
```

Os três últimos usam a mesma linha de combatentes: um cenário testa os três.
Mexer na iniciativa muda a ordem da Fase 2 inteira.

### O que o autor exercitou jogando

`bash tools/compilar.sh` prova que compila. Comportamento só se prova jogando, e
foi assim que a paridade do Q2, o título perdido e o `bar_tvencedor` apareceram.

```text
✅ o laço inteiro        menu → campanha → batalha → volta → mapa pintado
✅ paridade odd-r        o Q2 (originY ímpar) monta idêntico ao mosaico
✅ cores pelo slot       tint, quadradinhos e registro concordam
✅ tropa e caixa inicial  A_IA_Q2 tem chinook no slot 0 e 10000 para cada lado
✅ save/load de campanha  volta ao quadrante em foco
✅ perfil Médio           Q1 até a rodada 3, pelo menu (save do slot 2)
✅ cenários de visão      soldado na mata, montanhas A-B-C-D, HQ atrás da serra
✅ load pelo contrato     o save manda, não a cena; cortina só no load
✅ cursor depois do load  passa a vez e vai direto ao QG da IA
✅ captura = HP           Bazooka 10 numa cidade de 0,5 causa 5
✅ celular (Unity Play)   25 unidades em combate, ~660 MB, status normal, sem erro
✅ dedo na Campanha       seleciona quadrante e abre o menu pelo toque
✅ Carregar Jogo na web   abre (o build com LTO travava)
```

**Só compilou (v9.2.0):** o APC deixando de esperar soldado ferido e o botão de
menu da Batalha pausando a IA. **Aberto:** o planejador da IA leva 7–9 s por
turno no celular (105 ms no PC) e há ~1 s de atraso ao selecionar quadrante; os
dois pedem Profiler, não palpite.

**Só compilou (v9.1.0):** a IA retomando depois de sair do menu com ESC no
turno dela, o load de um save feito no turno da IA, o deadlock do shopping da IA
e a rede de 3 s do pedido de menu. **Fora do MVP:** hot seat humano × humano e
os tutoriais, que estão no build profile mas sem entrada no menu principal.
Ainda em aberto: o panel_dialog no menu do turno da IA mostra o que está sob o
cursor (onde a IA agia). Respeita a névoa, mas o autor ainda não decidiu se quer
isso.

Não visto em partida ainda: a construção revelando com linha, o Play direto com
perfil, a linha MOTIVO no painel (pode não caber), os perfis Fácil e Difícil.

### A IA obedece o perfil, mas só nos toggles

O catálogo está ligado na Batalha, e **nenhum portão pergunta mais "sou o
difícil?"**. Os três perfis foram montados a partir das frases do autor (v8.6.1,
Frente 2): Fácil com ⅓ da renda e sem blitzkrieg, Médio **sem** blitzkrieg (decisão do autor em 2026-11-04: "essa apelação é do difícil") e com conscrição quando perdendo, Difícil
com spam de soldado e os números do lado hard.

O que falta, em ordem:

1. **A poupança está decidida (autor, pós-v8.6.1): as três dificuldades poupam
   para elite.** O que muda é o piso:
   ```text
   Fácil/Médio  compram pelo plano e podem deixar produtor vazio enquanto poupam
   Difícil      compra o básico em CADA fábrica (conscrição sempre); a sobra vai
                para o elite que ela persegue, senão compra mais básico
   ```
   No 2×2 do `AICapabilityPreset`, isso é Fácil/Médio = teto sem piso, e
   Difícil = piso + teto. O tooltip do 2×2 diz que, sem alvo elite, a sobra do
   Difícil compra "unidades do plano"; o autor descreve "o básico". Conferir
   qual dos dois o código faz quando a fase 2 ligar o `poupaPraElite`, que hoje
   não tem leitor.
2. **Fase 2, os números.** Nenhum dos ~70 valores do perfil é lido:
   - ~20 getters do `AIController`, cada um num ponto único (fácil);
   - ~50 campos públicos do `AIShoppingPlanner`, que viram campo privado com
     `FormerlySerializedAs`, mais getter.

   O mapa campo a campo está no gerador
   ([AIPresetGeneratorWindow.cs:275](../Assets/Editor/AI/AIPresetGeneratorWindow.cs)).
   Use ele, e não busca por nome.
3. ~~Perfil em uso e Play direto~~ — feitos na v9.0.0. O Inspector do
   `AIController` mostra o perfil e as capacidades efetivas; o Play direto resolve
   o perfil pelos flags da cena.

### Auditável: a direção que o autor escolheu

Feito na v9.0.0:

```text
Save Inspector          Tools ▸ Auditoria ▸ Save Inspector — lê pelo mesmo caminho do load
perfil em uso           quadro no fim do Inspector do AIController
resultado com motivo    o progresso grava o VictoryReason; placar com MOTIVO
regra de sensor única   visão: FogKnowledgeSnapshotBuilder.CollectConstructionVisibleCells
```

O que vem, em ordem de custo:

1. **Diff de dois saves** no Save Inspector. Foi comparar saves que revelou o bug
   no mod do CK3.
2. **Valor efetivo dos NÚMEROS** com a origem (perfil, cena ou reserva). Vira
   obrigatório quando a fase 2 religar os números; hoje só as capacidades mostram.
3. **A regra de inclusão da construção no FOW de runtime** (quem é dono, HQ como
   marco global) ainda é cópia própria em `ApplyFriendlyConstructionVision`. A
   linha foi unificada; a inclusão, não.

### O teste da blitzkrieg reprova no código atual

**Reavaliar:** o handoff foi reescrito na v10.0.0 (herdeiro no Tático, fila do eixo, herdeiro gravado); a análise abaixo é da versão antiga. O [`teste_blitzkrieg.md`](AI%20Behavior/teste_blitzkrieg.md) do autor pede troca
sem plano, mas a troca mora no planner. Os casos C, E e F falham em
[Handoff.cs:97](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.Handoff.cs):
- substituto que não alcança o prédio é aceito;
- o vizinho do prédio conta como chegar nele;
- ninguém confere se o substituto já agiu.

### O eixo autorado ganhou ferramenta (v10.0.0)

`Tools > Axis Editor`: escolhe o quadrante (liga o mesmo recorte do Map Helper), mostra a
fila de cada eixo por slot com setas, rally sempre no fim, e desenha no Scene View a linha do
QG ao rally com bolinha numerada por setor. Na Fixture lê os prédios **da cena**; fora dela, o
bake. O bloco de eixo no Inspector da construção segue a ordem do clique e o quadrante do Map
Helper.

### O `0b` NUNCA rodou

Os hooks de `sceneLoaded` do `ObjectiveManager` e do `AITacticalAnalyzer` estão
escritos desde a `v8.5.1` e ninguém exercitou. É o tipo de bug que só aparece na
segunda partida, e nenhuma sessão chegou lá.

> mapa A → turno 5 → menu → mapa B. No turno 1 do B, o plano da IA tem de nascer
> **vazio**.

Sintoma se falhar: a IA do segundo mapa persegue setor que não existe ali.

### O progresso só existe dentro de um save — e o slot 1 tem o falso

O `CampaignProgressStore` vive em memória, é zerado a cada Play e a cada Novo
Jogo, e só vai para o disco dentro do `.tmrsave`.

**O save do slot 1** (cena Campanha, 10/09) guarda o `RODADAS: 3` falso: Q2 no
slot 1 em 3 rodadas, Q1 no slot 0 em 2. Carregá-lo traz o placar falso de volta.
Apagar ou não é decisão do autor. O mesmo save tem um `capturedBuildingHistory`
estranho (slot 0 com `teamId: 2` numa cena sem construção), não investigado.

A pasta `CampaignProgress/` de uma versão antiga foi apagada; o código não a lia.

### Três lugares respondem "de quem é este quadrante"

```text
CampaignSelectionController.GetWonSectorCounts    conta por slot
CampaignSelectionController.GetQuadrantOwners     lista por quadrante
ProgressoDaCampanha                               a versão que sobe a hierarquia
```

Concordam hoje porque todos consultam o `CampaignProgressStore`. Vão discordar no
dia em que "concluído" virar recursivo — *campanha concluída = todos os quadrantes
dela* — que é o que o portão de destrave vai precisar. Gatilho conhecido, conserto
conhecido: os dois primeiros delegam ao terceiro.

### Arestas de tela, todas conhecidas

- **O painel de inspeção tapa a borda direita do mapa.** Navegar até um quadrante
  de lá é inspecionar algo que não se vê. Ideia parqueada: o painel escolher o
  lado oposto ao quadrante em foco.
- **Sem o `presentationTextOverride`, a campanha cai no "Turno {n}"** do ramo de
  batalha. Visível, então não é falha silenciosa — mas turno não significa nada
  numa tela de seleção.
- **Os números laterais e os quadradinhos dizem a mesma coisa** com 4 quadrantes.
  Ganham utilidade quando uma campanha tiver 12.
- **A mixagem de SFX segue espalhada** por três cenas, uma delas com cursor
  inline, e o menu toca a 3× o volume das outras.
- **O silêncio entre menu e campanha** continua: `MatchMusicAudioManager` sem
  `DontDestroyOnLoad`, e `BuildWorldMosaic` no `Awake` em ordem `-10000`.

### Depois

```text
1. testar o 0b — jogar o Q1 e o Q2 em sequência já é o teste
2. o fim da campanha: 4/4 oferece a volta à Tela de Entrada
3. unificar as três contagens (gatilho: o portão de destrave)
4. o portão hierárquico — Liberado() precisa de pai e irmãos, que o MundoData
   não expõe
```

**O Q3 e o Q4 ficam vazios por enquanto, por decisão do autor.** A campanha se
joga no Q1 e no Q2.

Da visão (v9.0.0), pequenos e conhecidos:
- **EV padrão das construções** é 1 / bloqueia (a flag, 0 / não). Docas,
  Hidrobase, Estação de Trem e Terminal talvez queiram outro valor.
- **O `Planicie.asset` ainda guarda a lista antiga** `constructionVisionOverrides`
  em YAML. O código não lê; a Unity descarta no próximo save do asset.
- **O bake da rodada 0 está desatualizado** (cache na versão 4). Não precisa para
  jogar: no Play é só atalho de partida. Só as ferramentas no Edit Mode pedem.

O rótulo "ALPHA BUILD v0.3" é indicativo: a grafia diferente entre as cenas fica
como está.

Pós-MVP, com rascunho feito: o **Quero Spotting**
([QueroSpottingWindow.cs](../Assets/Editor/QueroSpottingWindow.cs)) e o "quadro de
missões". **Antes de retomar missões, ler o
[`contrato_missoes.md`](AI%20Behavior/contrato_missoes.md)** (v7.2.1): ele já
desenha `SpottingDeCobertura`, e a conversa da v8.6.1 redescobriu partes dele sem
saber. O `IsForwardObserverSpot` vai cair, porque o autor não quer lugar
autorado de spotting.

⚠️ **`MatchController.cs` carrega duas frentes.** `isPlayable` (paralela) e
`TrySetStartMoney` (minha) entraram no mesmo arquivo no `aac0c48`. Reverter um sem
o outro é edição manual.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar;
                                     a LINHA tem regra única desde a v9.0.0
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; o QUESTIONÁRIO tem contrato
                                     e observador (v9.2.1); RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 o laço roda de ponta a ponta e tem tela;
                                     falta EXERCITAR o 0b e o portão de destrave
```

O degrau 3 ganhou vizinho na v8.6.0: a **doutrina** saiu do `hardMode` e virou
capacidade nomeada, lida de um perfil autorado. É o mesmo movimento do degrau —
política deixando de ser `if` espalhado — só que na camada do general, não na do
papel. Na v8.6.1 a viagem das **capacidades** terminou; os *valores* ainda não
saíram do lugar.

O degrau 0 ficou mais firme na v9.0.0: a linha de visão, de detecção e de tiro
parte de uma regra só, e a construção usa a mesma reta que a unidade.

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **fixture pequeno aprova a régua errada** | o QG a 2 hexes num tabuleiro de 10×6 fazia o Operacional e a rota darem a mesma resposta, e o capitão "por Operacional" passou. Todo fixture de alcance tem um caso com o alvo **além** da banda (12+ hexes), porque é ali que régua e régua parecida divergem |
| **flag por turno lida como capacidade** | `ReceivedSuppliesThisTurn` acende na Fase 1 quando o Serviço do Comando cura; a Fase 2 que perguntar "este prédio atende?" sem `ignoreTurnLimit: true` lê "não" para todo ferido já curado. "Atende de novo neste turno" e "atende esta unidade" são perguntas diferentes |
| **o fix isolado passa e o turno real não** | `medir_planner` caiu de 8,9 s para 0,5 s e o `BuildObjectivePlan` do turno real seguiu em 6,4 s: o replay refaz o plano com os capturadores já atribuídos, e a matriz só roda para quem está livre. Custo que roda **uma vez por turno** só se mede no turno real (marcas do `[AI Perf]`), nunca por replay |
| **caminho enumerado não é custo** | `CalcularCaminhosValidos(…, 30)` enumera passeios (estado por passos exatos): 57 células em 1,7 s, linear no orçamento mesmo com a malha saturada. Distância para decidir é `CalculateMovementCostMap` (3 ms); caminhos válidos só com o MP real da unidade |
| **`showAILogs` não cala a IA** | gateia 12 das 541 linhas `[AI …]` do `TL()`; os `[AI Perf]`, `[AI Commit Heavy]`, `[AI Turn Transition Timing]` e `[SectorManager][Perf]` não têm flag. Ver `docs/checklist_logs_publicacao.md` antes de afirmar que um build está sem log |
| **guarda nova que desliga a antiga** | o commit do ar no rally pôs `!assembling &&` na frente de `TryResolveInterceptorOperationalPursuit` e o Caça A perdeu a proteção contra o terciário; a revisão deixou passar. Todo `&&` novo numa condição existente é lido como "o que isto tira", não só "o que isto põe" |
| **doutrina lembrada ≠ doutrina escrita** | o autor lembrava "interceptador não ataca solo"; o código dizia "adia se há presa aérea". Antes de responder "isso já existe", leia a condição da regra, não o nome dela. `Tertiary` é o padrão para classe não listada na ficha, não uma linha apagável |
| **a barra da tela é o fato do turno** | `EvaluateRallyReadiness` é cacheada por rally, slot e turno; a tela lê o mesmo fato de propósito. Unidade que entra na área no meio do turno só muda a prontidão no próximo planner. Para ver "agora", dump de presença (`IsRallyParticipantPresent`), não a tela |
| **a conta diz uma coisa, o gate ou a vaga diz outra** | artilharia "opcional" na doutrina e obrigatória no gate; Caça B somado como Ruptura e vaga pedindo `UnitRole.Assalto`. Quem gateia, quem conta e quem abre vaga leem o mesmo dono (`EvaluateRallyReadiness`, `EnsureRallyRoleSlots` byForce) |
| **alcance de aeronave tomado por presença** | "rally no meu Tático" dá o Apache no hangar como presente: a banda de um avião é meio mapa. Aéreo é presente pela **célula** na área do rally (`IsRallyParticipantPresent`); a banda própria só para terrestre e para o que é relativo ao capitão |
| **tabela de doutrina vivendo em `switch`** | família, Força e "satisfaz" em `UnitRole.cs` obrigavam commit para o autor mudar uma flag. Catálogo é asset (`UnitRoleCatalog`); o código lê, não decide. Sem fallback para tabela em código |
| **relógio que começa antes do gate** | a saturação contava turnos de piso com o rally ainda inimigo e disparava GO_GREEN no turno da captura. Observação e consulta usam o mesmo predicado (`HoldReady && BreakthroughReady`) |
| **porta por facção onde o magnético já responde** | `isRebel`/`IsSlotRebel` na IA era sempre um rogue mal traduzido. Pergunte "tem vaga?" (`IsPassengerInPlanSlot`); o que é da facção mora na partida e no `HasShoppingBase`, não no papel |
| **imunidade de facção no lugar de regra de cenário** | a rebelde era imune a 0 unidades por causa de aulas com spawn tardio. Proteção de cenário é `UsesMatchEndRules`/`allowDefeatForZeroUnits` no quadrante; não dá imunidade à facção |
| **vitória declarada por quem elimina** | `HandleUnitDestroyed` coroava quem zerava um exército mesmo com três lados. Todo fim passa por `TryDeclareLastStandingWinner`; `DeclareEliminationVictory` só na rendição |
| **arma secundária vira missão de compra** | o Caça A foi comprado contra infantaria. `IsCounterResponseRoleAllowed` separa contramedida de compra e tiro oportunista; interceptadores respondem a ar |
| **ficha exata pula a ilha** | `RequiredUnitId` não torna a contramedida alcançável. `FindBestEliteCounter` aplica `CounterOffLandShare`/`OffersNonLandCounter` antes da cadeia; a exclusão também vale para o pré-requisito |
| **mesmo tanque, origem diferente** | a correção do rally funcionou e o Tanque B reapareceu pelo counter-prerequisite. Conferir `origem=`, compromisso e cadeia antes de culpar o ramo corrigido |
| **ar primeiro só no Tático** | a presa no Operacional não entra no scan de tiro imediato. `TryResolveOperationalHuntTarget` e avanço existente permitem ao caça adiar o tiro terrestre terciário; não mudar o Disruptor |
| **aceitar o que a etapa seguinte recusa** | a âncora de fusão, o supridor da recuperação aérea e a LZ do hospital aceitavam alvos que a execução recusava (ameaça, pouso na célula, caminho sem avanço) — e a unidade ficava presa no ramo, sem cair no fallback. A primeira etapa pergunta **ao predicado da segunda**, na célula onde a ação vai acontecer |
| **estado avaliado depois de quem o muda** | o reparo era decidido na Fase 2, depois de o Serviço do Comando curar: quem chegava abaixo do gatilho saía curado e "sem ferimento". Estado que reage a um evento (dano, cura) tem de ser lido **antes** do passo que o altera |
| **"a IA parou depois do load"** | primeiro F12 (AI Resume). Com `startOnPause: 1` todo Play novo nasce pausado; o `ai replan` destrava por reiniciar o estágio, não por consertar |
| **outra IA escrevendo ao lado do dono** | a 1ª versão do EVAC aéreo escolhia hospital varrendo prédios por fora do `FindRepairConstruction` e perdia conscrição, pressão, linha e elite. Revisar o diff dela antes do commit |
| **lista do snapshot vazia na Fase 2** | a Fase 2 usa `BuildLight`, não `Build`. Até `e72deff` ele não preenchia `EnemyUnits`, e oito sistemas liam vazio sem erro. Antes de confiar numa lista do snapshot, ver qual Build a montou |
| **consulta com nome de lookup e custo de varredura** | `GetConstructionAtCell` fazia `FindObjectsByType` por chamada; num filtro por célula virou 13 s. Antes de pôr utilitário dentro de laço por célula, ler o corpo dele |
| **ferramenta que recalcula o jogo** | Shopping Pressure e AI Step refaziam compra/névoa/terreno ao lado do jogo (FPS, FrameSpike de segundos). Ferramenta lê o fato de quem o produziu; só simula sob pedido |
| **log que só conta uma das recusas** | o MelhorDesembarque logava só `transporter_cell_not_allowed`; seis linhas pareciam "ocupação aérea" e o motivo real morria calado. Toda recusa conta o motivo |
| **liberar a poupança sem pré-comprar** | o ramo "libera da reserva" soltava o dinheiro e o carrinho (largura) o dava a 3 peças baratas. Ao liberar para uma peça, garantir a peça |
| **`execution=` do `[AI Perf]` com AI Step ligado** | inclui o tempo do autor lendo antes de clicar. Custo da IA é `decision=` |
| **salvar `.cs` com o autor em Play** | recarrega o domínio igual a `unity recompile` (duas recargas pareceram "loop travando"). Deixar pronto e salvar quando ele parar |
| **`snapshot.MyUnits` não tem embarcados** | o limite do jogo (`MatchController.GetSlotUnitCounts`) e o HUD contam todos. Qualquer régua comparada ao teto tem de contar como o teto, senão a festa da carona esconde metade do exército |
| **sub-rótulo novo some das comparações estritas** | `roles[0] == X` não vê o sub-rótulo. Antes de criar um (Disruptor, `InterceptadorDeDisruptor`), trocar as leituras por `MatchesLabel`/`CanSatisfy` — e conferir que a CONTAGEM e a DEMANDA usam a mesma régua, senão a demanda aceita, a contagem não vê, e a compra entra em loop |
| **empate de prioridade decide pelo preço** | duas peças com a mesma presa em primário: a barata ganha sempre (a AAA nunca deixaria o SAM nascer). Um PRI por classe; a peça barata entra pelo secundário |
| **o carrinho prefere largura** | o beam soma atendimentos: 6 peças baratas vencem 1 cara que cabe. Peça que precisa sair (1º MBT, escolta) vai por `TryPreCommitDemand`, antes do carrinho |
| **inspeção que loga vira loop** | a Shopping Pressure recalculava a demanda por segundo; linha sem `if (log …)` aparece no Console a cada refresh |
| **Bash é lento neste disco por arquivo** | `grep`/`basename` por arquivo em laço passa de 2 min. Auditar fichas com PowerShell num processo só (o script de auditoria fica no scratchpad da sessão) |
| **"Voltar ao debug" só restaura o que o "Preparar" mudou** | flag desligada à mão antes do Preparar não entra no backup (`UserSettings/PublicacaoChecklist.json`). No fechamento da v10.2.0, cinco flags da Batalha (Start On Pause, atalhos, HUD e logs da IA) só voltaram pelo valor do HEAD |
| **a raiz do prefab é override em cada cena** | aumentar o `Panel_Config` no prefab não chegava nas cenas: tamanho e posição da raiz ficam gravados na instância. Mexer no prefab E nas três instâncias |
| **liberar a poupança sem fechar a peça** | soltar a reserva do elite para uma demanda que não cabe só entrega o dinheiro a outra demanda. Rompe se compra; senão, segura o caixa |
| **"trazer a ideia da Mi'Larhys"** | é trazer as práticas (menus `Tools/Mylarhis/*`, resumo dela), não o formato da janela |
| **sensor de captura barra embarcado** | simular um passageiro numa célula de desembarque com o `SimulateCaptureSensor` normal nunca dá captura. Quem projeta o desembarque usa `PodeCapturarSensor.TryGetCaptureTargetAtCell(..., applyEmbarkedGate: false)` |
| **"ar primeiro" vale para todo aéreo** | `ShouldConsiderAirCombatTarget` exclui alvo de chão se há aeronave atacável — é doutrina de caça; o Disruptor passa por fora. Antes de mudar o alvo de um aéreo, ver quem entra nos candidatos |
| **o editor da ficha cortava `roles` em 1** | agora há Unit Secondary Role (`roles[1]`); qualquer leitura de papel deve usar `CanSatisfy`/`MatchesLabel`, não `roles[0] ==` |
| **tag antes do churn** | no fechamento da v10.1.0 o churn entrou depois da tag (commit `b2a1213`). A tag não foi movida. Conferir `git status` antes do `git tag` |
| **o plano persiste entre turnos** | quem já tem vaga não volta para a matriz de atribuição de capturadores. Mudar o custo da matriz não mexe nele, nem com `ai replan`. Antes de mexer num custo, conferir **quem entra** na atribuição |
| **aeronave pousada é `Land`** | `GetDomain() != Air` não diz "não é aeronave": o Chinook no aeroporto pulava o teste de pouso. Perguntar à ficha (`IsAircraft`) |
| **grep no arquivo do sensor não segue a delegação** | eu disse que o `PodePousar` não olhava ocupação; ele delega a `AircraftOperationRules`, onde ela mora. Seguir a chamada antes de dizer que um sensor não faz algo |
| **regra de ilha numa porta só** | o shopping tem várias portas (pacote, counter, elite, operação, AA, vigilância). Regra de mapa que vira demanda tem de passar por todas |
| **testar mudança de IA com save do meio do turno** | o plano vai no save; a retomada no stage 2 reexecuta a decisão antiga e esconde o código novo. Usar `ai replan` na vez da IA, ou save de antes de ela planejar |
| **o bake é foto** | mexer em rally ou eixo na cena e não rebakear deixa a Batalha com o dado velho (o dono do rally de Juliet estava na cena e não no bake). Na autoria, ler a cena |
| **o Shopping em uso é o carrinho por papéis** | `AIShoppingPlanner.Demand.cs`, log `[AI Shopping Roles] … carrinho`. A ordenação de `landBuildings` em `AIShoppingPlanner.cs` não roda nesta partida |
| **família não é composição** | o pacote conta por `ResolveFamily`; a composição antiga (`ResolveCompositionRole`) diverge em Obus Leve e antiaéreos. Demanda que conta por família tem de comprar por família (`RequiredFamily`) |
| **afirmar sobre estrada sem ler a rota** | a rota autorada (bake do quadrante) e o `Rodovias.asset` decidem; a construção só cobra custo. Conferir antes de dizer que há ou não bônus |
| **retângulos de quadrante que se tocam** | a faixa compartilhada entra no bake dos dois mapas, sem erro. Conferir o bake de cada quadrante depois de mexer nas bordas |
| **`git mv` antes de outros commits** | a renomeação fica no índice e entra no próximo `git commit`, de qualquer frente. Mover arquivos por último, ou `git reset` antes |
| **`unity recompile` com o jogo em Play** | recarrega o domínio e **derruba a partida** (voltou ao turno 1). Em Play, só `tools/compilar.sh`; a Unity recompila quando sai do Play |
| **validar a IA chamando funções por dentro** | `TryAIDirectPurchase` faz a jogada valer mas não testa menu nem automata. Agir por batch → `ExecuteLiveAIBatch` |
| **perguntar à IA todas as unidades de uma vez** | ela é sequencial: sem a reserva da anterior, o ensaio manda todos ao mesmo hex. Uma unidade, executa, a próxima |
| **ensaio de compra sem o plano montado** | a IA monta o plano (`BuildObjectivePlan`) antes de comprar; sem ele o shopping responde outra coisa |
| **"base preset" não é a dificuldade** | quem decide é o contrato (ou `hardMode` da cena aberta direto); o catálogo mapeia para o perfil |
| **`script eval` com aspas pelo PowerShell** | quebra os argumentos; mandar o código num arquivo (`--file`) |
| **"prioridade" no score do carrinho** | um degrau vale 3.000; o bônus de encaixe do counter, até 36.000. A prioridade ordena a lista, mas quem decide é o encaixe. Antes de dizer "a demanda X tem prioridade", somar os bônus |
| **comparar os dois lados com fotos de momentos diferentes** | quem joga em segundo vê o inimigo já depois da compra e a si mesmo antes dela. Qualquer razão de força precisa igualar o ponto do ciclo, e só para quem joga depois |
| **"coerente com a demanda" não responde "por que"** | explicar a intenção não prova que a decisão é boa. Ir ao código do score: foi ali que estava o defeito da Metranca |
| **resposta que não está no registro do AI Step** | o defeito é o log não contar a decisão (o handoff dizia só "via (7,11)"). Consertar o log também é correção |

| armadilha | regra |
|---|---|
| **"fonte única" tomada por suposição** | o autor acreditava que a fórmula de combate era uma só; eram duas cópias, e o DPQ tinha quatro. Antes de dizer "é tudo o mesmo cálculo", procurar quem chama a conta **e** quem monta as entradas (`DivideAndRound`, `PositionDpqResolver`) |
| **calculadora de balanceamento em decisão de jogo** | `AICombatHpSimulator.Simulate(ficha…)` assume planície, munição cheia, arma pela ficha. Em jogo: opção do `PodeMirar` → `SimulateWithWeapons` com DPQ real |
| **desligar fallback que cai em permissivo** | sem opção do sensor, `simInvalid` **permite** o ataque. Desligar o fallback sozinho afrouxaria ~40 portões; foi preciso o `BlockedNoSensorOption`. Antes de desligar um caminho, ver o que o "inválido" responde |
| **"voltou a Neutral" ≠ "o mundo recalculou"** | `OnCursorReturnedToNeutral` chega ANTES do delta da névoa. Estado que deriva de névoa/detecção escuta `OnBoardSettledAtNeutral`. Não inverter a ordem: o índice de ocupação reconcilia no primeiro |
| **ler a linha e não seguir quem chama** | eu disse que alcance 0 quebrava a IA lendo o `Max(1, dist)`; o caminho canônico nem recebe distância. Antes de afirmar impacto, seguir o chamador até o uso |
| **Importar com a Unity compilando** | o campo novo chega VAZIO no asset: ele já conhece o campo, o importador ainda é o antigo (a fala da Capitã chegou muda). Importar só sem a rodinha de compilação |
| **checagem no início do turno** | `OnActiveTeamChanged` dispara ANTES do `ReleaseUnitsForActiveTeam` (upkeep). Quem lê combustível ali vê o valor de antes do consumo; leia no `Neutral` |
| **"escolher" e "bloquear" com a mesma regra** | aeronave no ar não bloqueia spawn, e a escolha da bandeira herdou isso: dois Apaches na mesma bandeira. Separar a regra de bloqueio da de preferência |
| **IA assumindo que sempre há prédio** | sem capturável e sem prédio que conserte, três ramos desistiam em silêncio. Mapa de aula/teste é o caso que os expõe |
| **trava de roteiro valendo para a IA** | o "passar a vez" travado por uma fala muda pegou a IA, que passa a vez pelo mesmo caminho do humano: bronca do Sargento na IA e turno preso. Toda trava de aula pergunta `IsActiveTeamAI` |
| **fala de abertura antes do turno 1** | `acted CH` na fala 0 era apagado pelo `ResetForTeamTurnStart`, que roda frames depois numa coroutine. Estado inicial de aula espera `MatchController.MatchStartApplied` |
| **painel que monta botão no `OnEnable`** | o `Panel_vitoria` pergunta `PodeVoltarParaCampanha` ao ligar; quem arma a volta é o `OnMatchConcluded`. Aviso **antes** do painel, senão o botão nasce escondido |
| **objetivo novo que já existia** | escrevi `UNIT_OUT_OF_FUEL`; o `UNIT_DEAD` já aceitava `TOKEN \|\| AUT=X`. Antes de criar objetivo/comando, ler a lista de ids no `TutorialManager` e a [`sintaxe.md`](tutorial/sintaxe.md) |
| **"só roda na cena de tutorial"** | componentes nascidos para cenas próprias (`TutorialManager`, automata) assumiam que estar presentes = estar numa aula. Na Batalha eles vivem em **toda** partida: o automata dirigiria o turno da IA. Ao mover um componente para uma cena compartilhada, procurar o que ele faz **sem** o dado que o justificava |
| **regra normal de fim numa partida especial** | eliminação, QG e estrelas rodavam nas aulas: a facção que só nasce no meio da lição era eliminada no turno 2. Partida especial declara o seu grupo de regras de fim (`UsesMatchEndRules`) |
| **mundo fixo em mais de uma cena** | o mundo estava serializado na Campanha **e** no `QuadranteController` da Batalha. Trocar de mundo só numa delas manda a Batalha procurar a campanha no mundo errado. O mundo viaja no contrato (`PartidaConfig.MundoAtivo`) |
| **coordenada lida em vários lugares** | o `x,y` do roteiro era lido em seis lugares (três cópias). Conversão de referencial só vale se todos passarem pelo mesmo leitor (`TryParseScriptCell`) |
| **árvore limpa tomada como Inspector salvo** | no fechamento da v9.2.1 o `git status` estava limpo e os perfis do Médio e do Difícil **não tinham o campo** em disco: o Inspector marca e não grava. Antes de taguear configuração, conferir o campo no `.asset` (grep) e pedir *File ▸ Save Project* |
| **afirmar a direção de um movimento lendo meia execução** | eu disse que na fusão o parceiro anda até o receptor; é o contrário — a **selecionada** anda até o parceiro e o consome (`TurnStateManager.Merge.cs`, 592–669). Li a linha que nomeia o receptor, não a que move. Ler a execução até o `SetCurrentCellPosition` |
| **"o papel X não faz Y" contado só nos arquivos do papel** | a revisão de papéis afirmou que a IA nunca funde; a fusão morava no **reparo**. Comportamento de papel pode estar num handler transversal (Repair, Logistics, Router) |
| **frente que mistura arquivo no commit** | `git add -p` não roda aqui (interativo). Monta-se o índice do arquivo como HEAD + só os blocos da frente (`git hash-object -w --path` + `update-index --cacheinfo`). Foi assim que a v9.2.1 saiu em cinco commits. **Na v9.2.2 o atalho de aplicar blocos de `git diff -U0` falhou duas vezes:** em modo texto o Python perde o CRLF, e chaves `{` soltas viram blocos que nenhuma palavra-chave pega. O arquivo misto foi inteiro para a frente dominante, com a mensagem dizendo o que ele carrega da outra. **Na v9.4.0 tropecei de novo** com `git apply --cached --unidiff-zero`: um `return true;` foi parar dentro de um `for` e entrou no commit (o `compilar.sh` lê o disco, não o índice). Só o caminho do blob (HEAD + substituição exata, assert de 1 ocorrência) é seguro; conferir `git diff --cached` ANTES de comitar |
| **Toggle que "não navega"** | navegava: o Toggle padrão seleciona com (245,245,245) sobre o quadradinho branco. O `Panel_NewGame` já resolvia com `ApplySelectionHighlight` (`#4A5A43`). Antes de depurar a navegação, conferir se a seleção é **visível** |
| **texto escrito uma vez, na criação** | os botões do helper gravavam `[helper.action.cancel]` para sempre porque eram montados antes de o banco de mensagens ser achado. Rótulo que depende de banco (ou de idioma) se resolve ao **aparecer**, não ao nascer |
| **busca de botão presa a um painel** | o `Button_config` mudou do `Panel_options` para o `Panel_gerenciar` e o menu só procurava no primeiro: o botão não abria, sem erro. Quando o autor move um botão de painel, conferir **onde** o código o procura |
| **"funciona no Simulator"** | o Simulator renderiza pelo Editor: não mede memória nem o navegador. Teste real = build servido na LAN (`npx http-server -a 0.0.0.0 -p 8000`) com o celular na **claro5** (subrede 192.168.1.x do PC) + `chrome://inspect` |
| **medir memória por dentro da aba** | `Runtime.queryObjects` varre o heap e DERRUBOU a aba. Use `adb shell dumpsys meminfo` e leia o **PSS**, não o RSS |
| **LTO na Web** | "Runtime Speed with LTO" travou o Carregar Jogo (`RuntimeError: unreachable`); sem LTO funciona. Para testar LTO de novo, gerar com Debug Symbols Embedded |
| **override de áudio na aba Web** | não existe taxa de amostragem nem nada além de formato e qualidade; Force To Mono vale para todas as plataformas. Taxa e canais se mudam no ARQUIVO |
| **campo autoatribuído por nome** | um `OnValidate` que reatribui pelo nome do arquivo desfaz a escolha do autor a cada edição. Autopreencher só o que estiver vazio |
| **estado de partida serializado na cena** | um campo do `MatchController` que muda em jogo, se for serializado, a cena salva com o valor do último teste. Os flags de início de turno em `1` prenderam a cortina do load. Campo de partida é `[NonSerialized]`; quem persiste é o save |
| **pausa como flag que alguém desliga** | a pausa do menu só soltava por um caminho de fechamento, e a IA parava para sempre pelos outros. Pausa é derivada (`PlayerPauseHolds`); nunca dependa de lembrar de desligar |
| **lista de estados copiada** | "o jogador está no menu" estava em três lugares, cada um com estados diferentes, e a mesma tela sumiu duas vezes. É `TurnStateManager.IsInPlayerMenuScope` |
| **`.tmrsave` é zip** | `grep` direto no arquivo não enxerga o texto comprimido e volta vazio. Foi assim que o relatório v8.6.1 afirmou que nenhum save tinha progresso, e o slot 1 tinha. Buscar em save pelo Save Inspector ou por `zipfile` |
| **nome do arquivo de save** | carrega a cena em que o slot foi criado: `quicksave_slot1_Battle Map 1 - Groud` é um save da Campanha. A cena vem do **manifesto** |
| **abrir opções para uma regra de jogo** | perguntado por que o soldado da mata herdava EV, eu ofereci três caminhos; a regra era uma só (*"não tem essa salada"*). Antes de propor alternativas, perguntar qual é a regra |
| **origem × alvo da linha** | são perguntas diferentes. Origem: o terreno decide se empresta EV (aeronave: camada). Alvo: enxergar = cume do hex, detectar = camada da unidade. Unificar uma não mexe na outra |
| **construção que ocupa substitui o hex** | com Block LoS, a altura dela é a do hex (cidade na montanha = 2, não 2,25). Sem Block LoS (flag) é marcador e o terreno fica |
| **cache que não vê mudança de regra** | o hash de config do FOW cobre mapa e flags, não regra. Mudou regra de visão? Suba `FogSourceCacheFormatVersion` e `FogRoundZeroSlotBake.CurrentFormatVersion` |
| **`git mv` já põe no índice** | um `git commit` seguinte leva as renomeações junto, mesmo sem `git add`. Separe antes de commitar outra coisa |
| **configurado tomado como valendo** | o Inspector mostra o campo, não o que o jogo usa. O `Base Preset` dizia Difícil e era só a reserva de um catálogo que cobre tudo. Antes de concluir pelo que a tela mostra, pergunte **quem responde em runtime** |
| **perfil copiado de um perfil de teste** | o Médio era o `AIPreset_Gulosa` renomeado, e o Fácil uma cópia dele: herdaram toggles de teste que contradiziam a doutrina escrita no próprio asset. Asset renomeado carrega o passado; confira contra a doutrina |
| **gerador que copia só um lado do par** | o gerador de preset copia o lado **normal** dos pares normal/hard. Os três perfis tinham os mesmos ~70 números. Religar leitura de valor sem comparar antes enfraquece o Difícil calado |
| **busca por nome tomada como ausência** | `intel.lookbackTurns` "sem leitor" se chama `IntelShoppingLookbackTurns` no shopping. Para mapear preset → cena, o gerador é o mapa; grep por nome não é |
| **Play direto na Batalha** | sem menu e sem save, a dificuldade não chega e a IA roda **sem perfil**, com os flags antigos da cena. Teste de perfil se faz pelo menu |
| **ferramenta que lê o fato em vez do observador** | a Retaguarda tira a "massa inimiga" da posição real das tropas. Para geografia o autor aceita; para decidir onde pedir olho é wallhack por procuração |
| **forma que não casa com o dado** | contável desenhado como barra obriga o jogador a extrair contagem de proporção; contínuo desenhado como quadrado finge discreto o que é fracionário. Antes de escolher barra ou quadrado, pergunte se o dado é contável |
| **busca por nome que falha calada** | `FindNamedChild("bar_vencedor")` com o objeto chamado `bar_tvencedor` não desenhou nada, sem erro nem log. Busca por nome dispensa arrastar referência, e o preço é falhar em silêncio — **toda busca por nome tem de avisar quando não acha** |
| **duas coisas amarradas no mesmo campo** | o `presentationTextOverride` decidia o título E se o placar aparecia. Quando a campanha precisou dos dois, alguém apagou o texto para o placar voltar, e o título se perdeu. Campo que decide duas coisas separa no dia em que elas divergirem |
| **escape `
` em script que gera código** | a ferramenta processa a barra antes do Python e o literal C# não casa. Usar `chr(92)`. **Está nesta tabela e eu tropecei duas vezes antes de olhar** |
| **`currentTurn` lido como jogada individual** | ele conta **rodadas**: só sobe quando o índice de jogador dá a volta. Passar a vez de um jogador ao outro não mexe nele. Todo número de "turno" que atravessa telas ou vira registro precisa dizer qual dos dois é |
| **translação de recorte em grade hexagonal** | odd-r: a posição de mundo de uma linha depende da PARIDADE do y, não da diferença entre dois y. Traduzir um retângulo autorado em y ímpar para y par inverte a paridade de todas as linhas e cisalha o recorte meia célula — e **nada reclama**, porque o tile continua na célula lógica certa. Se um recorte precisa mudar de y, a paridade tem de sobreviver |
| **nome interno que não casa com o botão** | a Tela de Entrada mostrava FÁCIL/MÉDIO/DIFÍCIL e o enum era Iniciante/Facil/Competitiva — o perfil chamado "facil" mudava o botão MÉDIO. Resolvido na v8.6.0 reduzindo a três com os nomes do jogador. Quando um rótulo de tela e um identificador interno convivem, **mostre os dois lado a lado na ferramenta** |
| **entrada de enum extinta num asset** | catálogo gravado com `dificuldade: 4` (o antigo `Competitiva`) não casa com nada depois da redução, e o perfil cai no caminho de reserva **calado**. Toda tabela dificuldade→asset precisa podar entrada que não existe mais, e dizer que podou |
| **reassar não conserta autoria** | a lista de eixos mora fora da seção assada de propósito; o bake não a toca. "Mudei e reassei e não mudou" é sintoma de estar olhando a fonte errada, não de bake quebrado |
| **o recorte de autoria vaza para o arquivo da cena** | não o recorte — o efeito dele. O `SectorManager` serializa `sectorInfos`, então salvar a Fixture com um quadrante em foco grava a lista **truncada**. Na autoria é cache e se refaz; mas é por isso que a `Fixture.unity` encolheu ~600 linhas na v8.6.0, e **não é regressão** |
| **editor que desenha o que o jogo não vai rodar** | o desenho de eixos só lia o quadrante que a Batalha pintou, e na autoria nada está pintado — mostrava sempre o leque automático. Editor divergindo do jogo é pior que editor sem desenho: a ferramenta passa a ensinar errado |
| **cena que parece partida** | a `Campanha` tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu da cena-base de batalha. Perguntar "estou numa partida?" olhando o que EXISTE na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao **da própria cena**, não a qualquer um |
| **`== null` da Unity em cache estático** | referência a objeto destruído responde `true` para `== null`. O padrão `if (cached == null) cached = Find(...)` **se auto-cura** entre cenas. "É estático e sobrevive à cena" NÃO é o teste de contaminação; o teste é **"guarda dado ou guarda referência?"** — dado contamina, referência morta se denuncia sozinha |
| **prefab compartilhado levando config de uma cena** | o `AudioManager` virou prefab das três cenas carregando o `playbackMode` da Tela de Entrada, e a Batalha passou a tocar a música do menu. O que é IGUAL nas cenas vai no prefab; o que é DIFERENTE deriva da cena ou vira override |
| **guarda que só sabe MANTER, não iniciar** | `TryEnsureSceneTrackPlayback` exigia que a faixa já estivesse tocando para protegê-la — confiava no `Start`, que tem saídas antecipadas. Guarda de invariante tem de perguntar "esta cena tem faixa própria?", não "a faixa própria já está tocando?" |
| **nome de cena como identidade de save** | o save gravava só o nome da cena, e toda partida de campanha é "Batalha". Carregar um save do Q3 pintava o Q1 com as peças do Q3 — e **funcionava** se o save viesse do Q1 |
| **cor tomada como identidade** | a cor é escolhida no menu, por partida. Dono, progresso e tropa endereçam por **slot**; a cor se resolve na hora de pintar. Violado em 3 pontos independentes num dia só, e nenhum deu erro |
| **`SetSlotIndex` tomado como "mudar de dono"** | ele só escreve o campo e **deixa a cor como estava**. Quem muda dono é `SetOwnerSlot` (construção) ou `SpawnAtCellForSlot` (unidade) — os mesmos caminhos da captura |
| **pintar antes da configuração chegar** | `QuadranteController` é `-9000`; o `Awake` do `MatchController` é `0`. Quem pinta antes tem de chamar `EnsurePartidaConfigApplied` primeiro |
| **`PartidaConfig` tomado como estado** | é de **consumo único**. Quem volta pra uma cena tem de republicar, senão a cena nasce com as cores serializadas nela |
| **`SetTile` tomado como cópia** | leva o tile e **descarta rotação, espelho e cor**. Só aparece em arte que tem lado; terreno é o caso em que o defeito é invisível |
| **`SetTransformMatrix` ignorado calado** | um tile com `LockTransform` faz o tilemap descartar a matriz sem avisar. Destravar (`SetTileFlags(None)`) antes de orientar |
| **recortar uma sequência como se fosse conjunto** | rota é ordenada e os consumidores leem PARES. Tirar uma célula do meio COLA as vizinhas numa aresta que ninguém desenhou |
| **`IsRouteValid` é tudo-ou-nada** | uma célula inválida descarta o desenho da rota INTEIRA |
| **`Build` fora do Play** | tudo que ele escreve é serializado. Numa cena compartilhada por todos os quadrantes, salvar grava o layout de um dentro dos outros |
| **camada decorativa é tilemap IRMÃO** | `ClearAllTiles` no tabuleiro não encosta nela |
| **contador de id recalculado do máximo** | certo no `UnitSpawner` (ids morrem com a partida), **errado** no mundo (o registro sobrevive ao nó) |
| **hexágono tratado como grade quadrada** | é odd-r: linha ímpar desloca meia célula |
| **campo sem leitor tomado como inofensivo** | `fieldEntries` tinha zero leitores **e** obrigava sete catálogos a existir. Por isso `UnidadeAssada` nasceu **sem** HP, combustível e elite |
| **generalizar do que se encontra sem checar a razão** | **perguntar por que aquilo existe antes de construir em cima** |
| **`ConstructionSector` default** | é `Alpha = 0`, não `None = -1`. Esquecer o setor não dá erro: dá plano degenerado |
| **id com acento ou espaço** | o YAML escapa (`"Feij\xE3o Torto"`) e o texto é digitado em dois lugares que precisam bater |
| **parse frágil de `.asset` virando afirmação** | contagem por faixa de linha é confiável; `$3` de `awk` não |
| **`.asset` em disco tomado como estado atual** | Inspector marca `dirty` e **não grava** |
| **`sed` em C# sem conferir chaves** | comeu um `}` de fechamento. Contar profundidade antes de seguir |
| **apagar dado do autor sem perguntar** | perguntar, sempre |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí o `-9000` e o portão |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem toda tradução de coordenada |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` são da cena corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação ou caches |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`relatorio_v10.4.0.md`](relatorio_v10.4.0.md) | ler o que o jogo já sabe — Fase 2 vê o inimigo, escada aérea, vaivém, ferramentas leves, entrega Operacional |
| [`relatorio_v10.0.0.md`](relatorio_v10.0.0.md) | a IA lance a lance aprende o mapa — o de trás paga, o veículo escolhe o par, estrada pelo QG, núcleo por família, ilha sem ligação |
| [`ilhas.md`](ilhas.md) | as ilhas geográficas do BeachManager e o recorte de autoria |
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`Planos/briefing_cena_campanha.md`](Planos/briefing_cena_campanha.md) | o contrato entre as duas frentes |
| [`AI Behavior/contrato_questionario.md`](AI%20Behavior/contrato_questionario.md) | **o questionário** — casas, duas etapas, o Capturador, as ordens, as missões, os abertos |
| [`relatorio_v9.5.0.md`](Versões/relatorio_v9.5.0.md) | uma IA validando a IA — Claude joga pelo Unity CLI, produtora e compra corrigidas, fatos de medalha, diagnóstico no save |
| [`playtest_claude.md`](playtest_claude.md) | o playtest turno a turno: IA × Claude, cada divergência com tipo e arquivo |
| [`relatorio_v9.4.0.md`](Versões/relatorio_v9.4.0.md) | revisão do motor — veredito de cada achado do consultor, combate em fonte única, Neutral em dois momentos |
| [`arquitetura/The-Map-Room-reconhecimento-arquitetural.md`](arquitetura/The-Map-Room-reconhecimento-arquitetural.md) | o reconhecimento do consultor (estático, em `3da75fd`) — riscos e sequência sugerida; ler com o veredito do relatório v9.4.0 ao lado |
| [`arquitetura/revisao_maquina_de_estados.md`](arquitetura/revisao_maquina_de_estados.md) | os cinco estágios do ciclo e as três fronteiras (antes de confirmar, depois de confirmar, depois de recalcular) |
| [`relatorio_v9.3.2.md`](Versões/relatorio_v9.3.2.md) | a aula 2 contra IA que briga — save da aula e do mundo, rebelde caça, fala avulsa, relógio |
| [`relatorio_v9.3.1.md`](Versões/relatorio_v9.3.1.md) | a primeira aula fecha — travas só do aluno, atalhos fechados, fala final, volta automática |
| [`relatorio_v9.3.0.md`](Versões/relatorio_v9.3.0.md) | a aula vira um quadrante — Academia, regras de fim da aula, tarefas de captura, bake embarcado |
| [`tutorial/sintaxe.md`](tutorial/sintaxe.md) | **como escrever uma aula** — comandos, tarefas, parâmetros, convenções |
| [`relatorio_v9.2.2.md`](Versões/relatorio_v9.2.2.md) | o primeiro estranho jogou — Configurações, Ação Direta, a névoa com uma memória só |
| [`pendencias do mvp.md`](pendencias%20do%20mvp.md) | o inventário transacional: o que vaza na névoa, o que foi decidido, o que falta testar |
| [`playtest de 3 de outubro 2026.md`](playtest%20de%203%20de%20outubro%202026.md) | o primeiro playtest externo e o que saiu dele |
| [`relatorio_v9.2.1.md`](Versões/relatorio_v9.2.1.md) | a IA pergunta antes de decidir — questionário, fusão no perfil, reparo pela linha |
| [`relatorio_v9.2.0.md`](Versões/relatorio_v9.2.0.md) | aguenta o celular — memória medida, áudio/texturas web, LTO, toque |
| [`relatorio_v9.1.0.md`](Versões/relatorio_v9.1.0.md) | o save manda na partida — load, estado fora da cena, menu no turno da IA |
| [`relatorio_v9.0.0.md`](Versões/relatorio_v9.0.0.md) | a mesma linha — visão de regra única, construção com altura, auditável |
| [`Versões/relatorio_v8.6.1.md`](Versões/relatorio_v8.6.1.md) | configurado ≠ valendo — perfis, portões (com errata na v9.0.0) |
| [`modding/guia de modding.md`](modding/guia%20de%20modding.md) | o guia do autor; a direção "auditável" sai dele |
| [`AI Behavior/capturador_politicas.md`](AI%20Behavior/capturador_politicas.md) | as quatro políticas do capturador, e o [teste da blitzkrieg](AI%20Behavior/teste_blitzkrieg.md) |
| [`AI Behavior/contrato_missoes.md`](AI%20Behavior/contrato_missoes.md) | missões (brainstorming da v7.2.1) — ler antes de retomar o quadro de missões |
| [`Versões/relatorio_v8.5.2.md`](Versões/relatorio_v8.5.2.md) | a forma tem que casar com o dado — quadradinhos, paridade, fim de partida |
| [`Versões/relatorio_v8.5.1.md`](Versões/relatorio_v8.5.1.md) | o que atravessa a cena — save por endereço, isPlayable, o 0b |
| [`Versões/relatorio_v8.5.0.md`](Versões/relatorio_v8.5.0.md) | o laço fecha, e o dono deixa de ser uma cor |
| [`Versões/relatorio_v8.4.1.md`](Versões/relatorio_v8.4.1.md) | orientação, rotas partidas e identidade estável |
| [`Versões/relatorio_v8.4.0.md`](Versões/relatorio_v8.4.0.md) | o dia em que o catálogo parou de dizer onde |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.**
- **Plano pedido não autoriza implementação.**
- **Verificar antes de documentar.** Busca vazia não prova ausência, e `.asset`
  em disco não prova estado.
- **Perguntar por que uma coisa existe antes de construir em cima dela.**
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.** Salvar recarrega o domínio igual a
  `unity recompile`. Deixe pronto, avise, e salve quando ele disser que parou.
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.

### Antes de escrever regra nova — procure a que já existe

O erro mais caro da v10 não foi regra errada: foi **regra certa escrita ao lado de uma que já
existia**, refazendo o trabalho dela e esquecendo o que ela sabia. Antes de codar:

1. **Ache quem já responde a mesma pergunta.** Destino de reparo/EVAC → `FindRepairConstruction`
   (conscrição, pressão à base, reparo pela linha, elite). Onde descer → `MelhorDesembarque`. Onde
   embarcar → `MelhorEmbarque`. "Tem construção aqui?" → `GetConstructionAtCell`. Pode-se mover/
   parar/atacar → o sensor `Pode*`. Várias unidades fazem a mesma coisa (EVAC: APC, Chinook, hospital
   do supridor); a regra tem de ser **uma só** para todas. Varredura própria por fora do dono da
   pergunta é o defeito, mesmo quando funciona no caso da frente.
2. **Leia o corpo do utilitário antes de pô-lo num laço por célula.** Nome de consulta pode
   esconder custo de varredura (`GetConstructionAtCell` fazia `FindObjectsByType` → 13 s).
   Montar o conjunto uma vez por decisão e consultar o conjunto.
3. **Rode o sensor só onde a resposta pode ser usada.** Não simule desembarque/tiro em célula que
   o filtro do fim vai recusar com certeza (alcance, faixa do passageiro). Pode antes.
4. **Ferramenta lê, não recalcula.** Shopping Pressure e AI Step leem o fato de quem o produziu; só
   simulam sob pedido explícito.
5. **Confira qual snapshot você recebeu.** A Fase 2 usa `BuildLight`; até `e72deff` ele não tinha
   `EnemyUnits`. Lista vazia não dá erro.
6. **Toda recusa conta o motivo** no log. "Sem LZ" com um só motivo registrado esconde os outros.
7. **Compile com `bash tools/compilar.sh`** e diga o que foi visto em Play e o que só compilou.
8. **Leia o estado antes de quem o muda.** O reparo (`UpdateRepairState`) era avaliado depois de o
   Serviço do Comando curar, e o ferido pulava a faixa de reparo. Estado que reage a dano ou cura
   tem de ser lido no começo da Fase 1, não depois dela.
9. **Destino de quem está ferido é do reparo, não de quem o carrega.** EVAC de qualquer
   transporte (APC, Chinook, navio) usa `RankRepairConstructions` / `FindRepairConstruction` com
   `rejectBaseCluster` e `lineSnapshot`; sem hospital, o anel do HQ pela régua do
   `TryDecideRepairFallbackToHQ`.
10. **Aceite só o que a etapa seguinte aceitaria — perguntando a ela.** Escolha (âncora, alvo, LZ)
    que a execução pode recusar usa o **mesmo predicado** da execução, na célula futura: fusão →
    `IsRepairFusionSiteSafe`; pouso do supridor → `PodePousarSensor.Evaluate(atCell:)` via
    `CanSupplierRecoverAircraft`; prazo de suprimento → envelope + `PodeSuprir` por origem, nunca
    distância. Cópia parecida do critério "na escolha" é o defeito.

11. **A elegibilidade acompanha a cadeia inteira da compra.** No counter, usar
    `IsCounterResponseRoleAllowed` para a missão e `CounterOffLandShare`/`OffersNonLandCounter`
    para isolamento **antes** de `FindBestEliteCounter` escolher a elite; `RequiredUnitId` e
    seu pré-requisito não são exceções. No rally, usar `TryGetRallyAnchorCell` e o acesso do
    `SectorManager` por ficha/produtora. Regra correta só no carrinho deixa o planejador
    continuar prometendo uma peça que não atende o destino.

12. **Não crie um terceiro caminho para o que já é um modo.** A "AI rebel" foi um controlador
    paralelo para o que o rogue da IA com plano já fazia; cada regra nova precisava ser escrita
    duas vezes e a segunda atrasava. Antes de abrir `if (facção)`, pergunte se o magnético
    (`TryDecidePlanlessCapturerAction`, capitão do assalto, EWACS/capitão do caça) já responde.
    Se responde, a porta cai; se não, complete o magnético. O fato da facção fica em quem o
    deriva (`IsHeadQuarterlessTeam`), lido só pela partida e pelo `HasShoppingBase`.

13. **Quem gateia, quem conta e quem abre vaga leem a mesma régua, no mesmo dono.** O rally
    exigia 3 de artilharia (`hasArtillery`) enquanto a doutrina a dava como opcional; a conta
    somava o Caça B como Ruptura e a vaga pedia `UnitRole.Assalto`; a presença aérea era medida
    pelo alcance. Cada divergência dessas é uma IA esperando algo que a outra metade já entregou.
    Antes de criar gate, contagem ou vaga, ache o dono (`EvaluateRallyReadiness`,
    `EnsureRallyRoleSlots`, `IsRallyParticipantPresent`) e faça os três lerem dele — e se a
    régua é doutrina configurável, ela mora em asset (`UnitRoleCatalog`), não em `switch`.

14. **Serviço burro não busca a cena, e pergunta de distância não pede caminho.** A regra 2 já
    dizia do `GetConstructionAtCell`; na v10.10.0 caíram os irmãos: `ResolveAISlotKey` fazia
    `FindAnyObjectByType` em 264 sítios e `StructureOccupancyRules.GetStructureAtCell` fazia
    `FindObjectsByType` por célula da linha de tiro — 14 s de frame congelado por turno. Um
    `Find*` dentro de um serviço que outros chamam em laço é defeito mesmo que cada chamada seja
    "só 0,8 ms"; o serviço lê um registro (`ConstructionManager.AllActive`,
    `RoadNetworkManager.AllActive`, `AIController.Instance.Match`). E quem precisa de distância
    pede custo a `CalculateMovementCostMap`, não caminhos a `CalcularCaminhosValidos` com
    orçamento inventado: a busca de caminhos enumera passeios acima do MP real. Antes de otimizar,
    **meça o turno real**: a hipótese "é o AI Step" e as hipóteses "é o solver" e "é o rally"
    caíram pelo número, e o fix isolado que "passou" deixou 6,4 s no turno de verdade.

15. **Banda é formação; rota é existência; e o atalho perde os filtros do dono.** Na v10.11.0
    "tem rota ao alvo?" foi respondida pelo Operacional de 2 turnos, e não pelo
    `AIRouteDistance.TryGet`: toda invasão com base a mais de 2 turnos ficaria sem capitão, parada
    no rally. O erro nasceu no bloco de correção, não no código: **a doutrina diz o que se pergunta,
    o bloco diz quem responde — nomeie o dono, não a banda.** No mesmo dia, "esta célula serve no
    raio do HQ?" virou `HexDistance` e pulou os filtros do anel de `DecideAirEvacHqFallback`
    (`IsRepairHqFallbackCellAllowed`, névoa, seguro/contestado). Régua vizinha que dá a mesma
    resposta no caso pequeno é o defeito; o teste é um fixture com o caso **além** da régua.
