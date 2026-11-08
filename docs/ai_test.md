# Testar a IA: método, descobertas e próximos cenários

Este é o ponto de entrada para validar a IA "de fora", com o Claude como jogador.
O registro turno a turno da primeira partida está em
[playtest_claude.md](playtest_claude.md). Este arquivo diz **como repetir** o
trabalho e **o que ele já achou**.

---

## 1. A ideia

A IA é testada contra o próprio jogo, não contra um modelo dela. O Claude ocupa um
slot como **humano** e faz duas coisas a cada decisão:

1. pergunta à IA o que ela faria no lugar dele, sem executar nada;
2. joga e compara a jogada dele com a resposta da IA.

Cada divergência entra em uma de três classes:

| classe | quer dizer | o que fazer |
|---|---|---|
| **bug** | a IA erra por um defeito de código | corrigir |
| **decisão discutível** | a IA segue uma regra de design que, nesta situação, joga mal | levar ao autor |
| **gosto** | preferência do Claude, sem argumento forte | registrar e não mexer |

Há uma quarta classe, que só aparece depois de investigar: **regra, não bug**. A IA
"não fez" porque o jogo não deixa. É o caso da captura no escuro (§4).

## 2. As ferramentas

O Claude mexe no editor pelo Unity CLI (`com.unity.pipeline`). O guia de conexão
está em `docs/indie/unity_cli_mcp.md`. Os scripts deste método ficam em
[tools/ai_test/](../tools/ai_test/), fora de `Assets/` para a Unity não tentar
compilá-los. Cada um roda com:

```bash
unity command script eval --file tools/ai_test/<script>.cs
```

| script | o que faz | executa? |
|---|---|---|
| `estado.cs` | turno, slots, dinheiro, dificuldade e blitz, além das unidades e dos prédios **visíveis para o slot** | não |
| `ia_plano.cs` | monta o plano da IA para o slot (`BuildObjectivePlan`) e lista objetivos e vagas | não* |
| `ia_unidade.cs` | o que a IA faria com **uma** unidade (`DecideUnitAction`) | não |
| `ia_compra.cs` | o que a IA compraria (`AIShoppingPlanner.Decide`) e o que cada produtor vende | não |
| `opcoes.cs` | prédios que cada unidade alcança e se o sensor deixa capturar, **com o motivo** quando não deixa | não |
| `agir.cs` | uma jogada (mover, capturar, comprar ou passar) por batch | **sim** |
| `conferir.cs` | se o automata terminou, se o último batch deu certo e como ficaram as unidades | não |
| `jogadas.cs` | o JogadasLog da partida, com os fatos estruturados (custo, HP, DPQ, captura) | não |

\* `ia_plano.cs` grava o plano no `ObjectiveManager`, como a própria IA faz no topo
do turno. Nada muda no tabuleiro.

Os parâmetros (slot, id da unidade, destino) são variáveis no topo de cada script.
Para ver o porquê de uma decisão, leia o Console logo depois de perguntar:

```bash
unity command console get --tail 40
```

As categorias do `TL(...)` (`[AI TEAM][T#][Base]`, `[Capturer]`...) contam a
cadeia de raciocínio.

## 3. As regras do método

Cada regra aqui custou um erro.

1. **Jogar por batch, nunca por dentro.** Toda jogada vira um `PlayerAction` montado
   pelos mesmos construtores da IA (`BuildMoveBatch`, `BuildCaptureBatch`,
   `BuildShoppingBatch`, `BuildEndTurnBatch`). O batch vai para
   `ReplayManager.ExecuteLiveAIBatch(action, false)`. O automata emula a entrada: o
   cursor anda, o menu abre e a escolha é confirmada. Chamar `TryAIDirectPurchase`
   ou `TryOpenEndingTurnConfirmation` faz a jogada valer, mas não testa o menu nem o
   automata, que é onde a IA já tropeçou. *(Erro do turno 1.)*
2. **Uma unidade por vez.** A IA é sequencial: decide uma unidade, executa, e só
   então decide a próxima, com o tabuleiro novo e `plannedDestinations` atualizado.
   Perguntar sobre todas de uma vez manda várias unidades ao mesmo hex. *(Erro do
   turno 2.)*
3. **Montar o plano antes de perguntar.** O shopping lê as vagas do plano. Sem
   `ia_plano.cs`, o ensaio de compra responde sobre um plano velho ou vazio. *(Erro
   do turno 1, que invalidou aquele ensaio de compra.)*
4. **Esperar o automata.** Depois de cada `agir.cs`, aguarde uns 6 s e rode
   `conferir.cs`. Só siga quando `ocupado=False` e `ok=True`.
5. **Fog-honesto.** Só se consulta inimigo que o slot enxerga
   (`IsUnitVisibleForSlot`). Validar a IA com informação que ela não tem não vale
   nada.
6. **Nunca `unity recompile` em Play.** O recompile recarrega o domínio e derruba a
   partida. Com o jogo rodando, compile só com `bash tools/compilar.sh`, que não
   toca no editor. *(Erro real: o autor teve de recarregar o save.)*
7. **Código por arquivo.** Use sempre `script eval --file`. Código inline quebra
   nas aspas do PowerShell.
8. **Falar em slot, não em cor.** `Jogada.team` é slot. Comparar slot com
   `(int)TeamId` foi o bug do `AIIntelAnalyzer` (§4).

## 4. O que o método já achou

Partida de 2026-11-03: Batalha em hot seat, mapa espelhado, o autor no slot 0 e o
Claude no slot 1.

| # | achado | classe | estado |
|---|---|---|---|
| 1 | **Liberar produtora atropelava a captura.** O capturador saía da fábrica para o hex do próprio alvo e só *movia*; a captura ficava para o turno seguinte. | bug | corrigido: `BuildVacateBatch` captura quando o sensor deixa (`AIController.Capturer.Vacate.cs`) |
| 2 | **Liberar produtora ignorava a missão.** A Unit11 foi mandada para trás só para desocupar o prédio. | bug | corrigido: o papel decide primeiro, e a liberação só vale se o papel ficaria parado (`AIController.Router.cs`, `suppressProductionUnlockVacate`) |
| 3 | **Produtor ocioso no turno 1.** Com 4000 em caixa e um produtor livre, a IA comprou 2 Soldados num mapa com 7 neutros, porque o pacote fixava 2 capturadores. | decisão discutível | corrigido: a demanda de capturador é `max(pacote, vagas abertas no plano)` (`AIShoppingPlanner.Demand.cs`) |
| 4 | **Blitzkrieg desligado no Médio.** `papeis.capturador.blitzkrieg` só está ligado no `AIPreset_Dificil`. Os comentários do preset citam "média faz blitzkrieg" como exemplo. | autoria | **decidido (autor, 2026-11-04): fica desligado.** O revezamento é a apelação do Difícil |
| 5 | **Captura no escuro recusada.** O sensor responde "Terreno ainda desconhecido". | regra, não bug | nada a fazer. `opcoes.cs` mostra o motivo |
| 6 | **`AIIntelAnalyzer` comparava slot com cor.** O analisador misturava as duas coisas. | bug | corrigido: `ResolveAISlot`, e toda comparação é slot contra slot |

Também saíram desta sessão, antes do playtest, as correções da IA rebelde: caça com
âncora (`TryResolveRebelHuntAnchor`), ataque como último recurso, e reparo que
desiste quando não há prédio nem fusão. Estão em `AIController.Capturer.Rogue.cs`,
`AIController.AirCombat.cs` e `AIController.Repair.cs`.

> **Nenhuma das correções 1, 2 e 3 foi vista rodando.** Elas compilam e seguem a
> lógica dos casos que as revelaram, mas nenhuma partida as exercitou depois do
> commit. O primeiro cenário de teste (§6) existe para isso.

## 5. A referência do autor: como joga um "Difícil"

O autor jogou 3 rodadas no papel de IA Difícil, em Neblina Leve, para servir de
gabarito. O JogadasLog registrou:

- **3 Soldados por turno**, todos os produtores ocupados, nunca um parado;
- **turno 3:** Lança-foguetes (A2) e mais 2 Soldados;
- **handoff de blitz no turno 3.**

A doutrina, nas palavras dele: **"formiga + counter"**. Massa barata o tempo todo, e
a peça cara só **sob demanda**, como resposta ao que apareceu. O achado 3 é o
mesmo princípio visto do lado da IA: produtor parado é renda perdida.

A comparação lance a lance entre essas 3 rodadas e a IA ainda **não foi feita**
(§6.2).

## 6. Próximos passos

### 6.1 Cenários de teste: o autor pinta, a IA joga, o Claude lê

Testar em partida inteira é lento: o achado que interessa aparece no turno 3, e
para chegar lá é preciso jogar os turnos 1 e 2 inteiros. O cenário corta isso:

1. o autor pinta um mapa pequeno com **uma** situação e o salva como teste
   repetível (sem shopping, se a compra não for o assunto);
2. a IA joga o turno de verdade;
3. o Claude lê o JogadasLog (`jogadas.cs`) e os logs da IA, compara com o esperado
   e corrige, e o autor recarrega o save.

O save é o teste. Recarregar é rodar de novo.

**Cenário 1: liberar produtora** (valida os achados 1, 2 e 4)

- 3 Soldados da IA, um em cada produtor dela;
- uma fábrica neutra **colada** a um dos produtores;
- uma cidade neutra **à frente**, ao alcance;
- dificuldade Médio (sem blitz: valida 1 e 2) e depois Difícil (com blitz: valida o handoff).

Esperado:

- quem tem a fábrica neutra ao lado sai **capturando**;
- quem tem missão à frente vai para a frente, e não para trás;
- os produtores ficam livres para a compra;
- com blitz, aparece o handoff.

**Cenários seguintes:** Força Aérea num mapa pequeno, e a Marinha, primeiro a
doutrina e depois o teste.

### 6.2 Comparar o replay do autor com a IA

A ideia é um script de editor que percorre o replay das 3 rodadas Difícil em modo
rápido (`StartReplayFromTurnAndSlot`, `ExecuteNextReplayBatch`). Antes de cada batch
do autor, ele pergunta à IA o que ela faria, e registra os dois lado a lado. O
resultado é uma tabela de divergências sem ninguém jogar.

Não está feito. O cuidado é o mesmo da regra 2: perguntar à IA **no estado exato**
antes de cada batch, e não no começo do turno.

### AAA prepara cobertura antes de retornar ao HQ (06/11/2026)

Marca T5/slot0, passo18: AAA #38 sem missao escolheu coesao da base (0,15) -> (0,16), embora houvesse aproximacao aerea. O tiro imediato e a aproximacao Operational continuam primeiro. Se ambos falharem, a nova politica procura uma posicao no componente terrestre alcançavel com o mesmo horizonte 30 do capturador que trabalha enquanto espera carona. Cruza essas posicoes com a mobilidade Tactical de turno completo das aeronaves visiveis e o alcance da arma da AAA. E previsao de cobertura, nao promessa de tiro; sensores de combate continuam validando ataques reais.

O comando anda apenas por um destino dos caminhos Tactical atuais, seguindo a rota longa valida. Evita destino ocupado/reservado e parada em produtor; mantem posicao se ja for a melhor cobertura, para coesao nao desfaze-la. Sem aproximacao cobrivel, conserva os fallbacks existentes de escolta/coesao. AI Step registra AAA prepara cobertura, posicao pretendida e aproximacoes cobertas.

Validacao: compilacao e tools/ai_test/verificar_aaa_cobertura.cs passaram fora do Play. O teste cobre progresso limitado a rodada, reserva, ocupacao, produtor, aeronave distante e manutencao de cobertura. Falta repetir o passo marcado na partida para avaliar o posicionamento no mapa real.


### Principal atendido promove passageiro secundario; desembarque oportunista distinto (06/11/2026)

Marca T5/slot1, passo21: Chinook #30 entregava #32 em Foxtrot e soltava #33 (missao Juliet) pela mesma captura (14,9), ja tambem atribuida ao #16 em terra. Agora o helicoptero pode promover o secundario quando o alvo do principal ja tem capturador comprometido, capaz de capturar e com acao/rota Tactical disponivel; simples proximidade nao basta. Se houver outro predio util no setor original, a entrega do principal continua util. Pressao confirmada permite reforco no mesmo alvo.

A prioridade promovida vale enquanto esse passageiro continuar a bordo, e e usada tanto na navegacao quanto na ordem/requisito da LZ. E preferencia de planejamento em memoria, recalculada apos carregar uma partida; nao altera FIFO confirmado das vagas nem missoes originais. O antigo principal pode descer como oportunista se houver trabalho livre na LZ. Oportunistas nao contam novamente os alvos ja incluidos nessa entrega nem os atendidos por capturadores comprometidos em terra; podem usar outro predio ou reforcar alvo sob pressao. Fallback aereo sem alvo util nao libera capturador automaticamente; escape por bloqueio continua preservado.

Validacao fora do Play: compilacao e tools/ai_test/verificar_passageiro_prioritario.cs passaram em cena temporaria descartada, com sensores e caminhos reais. Casos: compromisso/acao, promocao, ordem da LZ, prioridade estavel, fim da preferencia apos desembarque, outro predio no mesmo setor, alvo oportunista distinto, reforco sob pressao e preservacao das missoes. Falta repetir a marca no mapa real para avaliar a nova rota escolhida.

### Disruptor prioriza captura em andamento (06/11/2026)

Marca T5/slot1, passo14: Apache #29 preferiu negar 7 de poder de captura ao #4 fora de predio, em vez de 6 ao #14 sobre predio. O ranking agora compara primeiro a faixa capturando: alvo vivo, desembarcado, sobre construcao nao aliada que o PodeCapturarSensor confirma que ele pode capturar. Para o tiro, exige dano previsto que negue poder de captura; a aprovacao de ataque continua anterior a essa prioridade. Dentro da mesma faixa conserva o score de combate. A ancora de interdicao usa a mesma precedencia antes de poder de captura/distancia. Outros atacantes conservam o ranking existente.

AI Step informa capturando e prioridade=atrasarCaptura no motivo. Teste tools/ai_test/verificar_disruptor_capturando.cs passou fora do Play: sensor real, exclusao de transporte/embarcado/morto/aliado, regressao 6 vs 7 e ranking interno. Compilacao passou; repetir a marca para validar a escolha no tabuleiro real.


### Chinook no ar nao bloqueia captura na superficie (06/11/2026)

Marca T6/slot1, passo13: Chinook #18 fez somente vacate (3,5)->(4,6) antes de Pickup, embora estivesse na banda Air. TryBuildEmptyTransportCaptureTargetVacateAction agora consulta OccupancyResolver.CanEndMove com o capturador e apenas aquele transportador. Se podem coexistir, nao gera acao de desocupacao e deixa o roteador continuar normalmente em reparo/Pickup/papel. Nao acrescenta ordem de esperar nem impõe um destino de retorno; o fluxo normal escolhe quem buscar. Bloqueio real na superficie continua sujeito a desocupacao.

Compilacao e tools/ai_test/verificar_chinook_ocupacao_aerea.cs passaram fora do Play: mesmo hex em Air nao intercepta Pickup; em Land o aliado ainda gera desocupacao. Nenhuma acao executada.

### Abate oportunista durante encontro de carona (06/11/2026)

Marca T7/slot0, passo15: Soldado #25 seguia para encontro com Chinook #24 enquanto Soldado inimigo #19 (3 HP) estava proximo. Depois de esgotar embarques imediatos/estendidos e planejar o encontro, o capturador agora avalia abate oportunista. Exige sensor legal, AttackDecision aprovado, abate garantido, sobrevivencia e perda prevista de no maximo 1 HP. Nao altera o destino da missao nem a fila de carona.

Considera somente a parada planejada ou outra celula Tactical adjacente a mesma LZ, para preservar o encontro. Entre alternativas elegiveis, DPQ real de combate e o primeiro criterio; depois menor perda, menor ameaca e menor deslocamento. Nao aceita exposicao maior que na parada originalmente planejada nem nova parada em produtora/destino reservado. Embarque imediato e recuperacao continuam primeiro; o adiamento para o transportador escolher a coleta tambem permanece. AI Step registra abate oportunista enquanto espera carona com DPQ, perda prevista e LZ.

Compilacao e tools/ai_test/verificar_abate_espera_carona.cs passaram fora do Play, usando sensores e simulacao reais: 10 vs3 em planicie, melhor DPQ entre posicoes equivalentes (floresta vs planicie), LZ preservada, embarque imediato, recuperacao e recusa de luta sem abate. Nenhuma acao executada; falta repetir a marca no mapa real.


### 2026-11-07 — revezamento da ponta com herdeiro disponível
- Marca T8/slot 0, Soldado #13: memória de captura iniciada permitia abandonar prédio parcial sem herdeiro confirmado no plano.
- A execução agora revalida o herdeiro explícito: vivo, desembarcado, sem reparo, ainda sem agir e com alcance tático para assumir. Sem ele, volta ao fluxo normal de captura.
- O planejador rejeita embarcados também nos candidatos de troca e na consulta de alcance do revezamento.
- Regressão: `tools/ai_test/verificar_handoff_embarcado.cs` — PASS no Editor fora do Play: herdeiro válido aceita; embarcado, já agiu, distante, sem herdeiro e memória sem plano rejeitam. Embarcado não recebe alcance tático.
- `dotnet build Assembly-CSharp-Editor.csproj`: 0 erros; 477 avisos existentes.

### 2026-11-07 — espera por carona fora de produtores
- Marca T8/slot 0, Soldado #57: encontro com Chinook #24 terminava sobre a Fábrica Leve (1,14).
- O consumidor de MelhorEmbarque agora veta produtores próprios nas paradas do passageiro antes do ranking: Tactical, Operational e longo alcance. Caminhos continuam podendo atravessar essas células.
- Aproximação ao encontro e ataque oportunista durante a espera também vetam terminar em produtor próprio.
- Regressão `tools/ai_test/verificar_encontro_fora_produtora.cs`: PASS no Editor fora do Play; produtora mais barata descartada, vizinho alternativo para a mesma LZ, ausência de alternativa devolve vazio e mapa de passagem preservado.
- Compilação: 0 erros, 477 avisos existentes.

### 2026-11-07 — EVAC aéreo escolhe prédio vago e LZ conjunta
- Marca T9/slot 0, Chinook #24 com Soldado #22 (1 HP): EVAC tentava chegar à cidade (0,11), ocupada pelo Soldado #3, mesmo sem desembarque viável.
- Hospital e ordem vêm de `RankRepairConstructions`, extraído de `FindRepairConstruction` e também usado por ele: preserva conscrição, pressão na base, linha de combate, elite e exceção rebelde. EVAC acrescenta serviço compatível, ocupação do paciente e LZ vizinha; o helicóptero não estaciona no hospital.
- Prioriza o hospital mais bem ranqueado com entrega materializável agora. O sensor roda apenas em LZ alcançável neste turno, com cache por LZ. Sem alcance, aproxima de uma LZ estimada por distância/ameaça, sem sondagem remota, mantendo a carga; sem hospital elegível, aplica a área segura do fallback de reparo no anel do HQ; sem área segura/HQ conhecido, mantém carga.
- Retirado `ignoreTurnLimit: true`: a exceção ignorava `ReceivedSuppliesThisTurn` do paciente, não a cota de atendimento do hospital. A entrega agora respeita esse flag como o reparo normal.
- `tools/ai_test/verificar_evac_predio_vago.cs`: PASS fora do Play — cidade ocupada ignorada, entrega em fábrica livre a partir do lado, aproximação com alcance curto, troca após ocupação, desembarque no anel seguro com todos os hospitais ocupados e aproximação quando o HQ está longe. Planejamento mantém passageiro embarcado e HP intacto.
- Compilação: 0 erros, 477 avisos existentes.

- Revisão de 2026-11-07: teste ampliado PASS para conscrição do produtor Army no cluster da base, não-elite com base sob pressão, paciente já atendido e zero `AirEvacDisembarkProbes` na aproximação distante. `bash tools/compilar.sh` passou em Assembly-CSharp e Assembly-CSharp-Editor (Git Bash).

- Terceira revisão: fallback de HQ compartilha elegibilidade, segurança e score com `TryDecideRepairFallbackToHQ`; pode soltar em planície livre. `IsEvacDropCellSafe` voltou às descidas e LZs. Fora do fallback final, LZ com ameaça positiva é recusada pela régua conservadora já usada em transporte; rotas também recusam qualquer passo ameaçado (origem não bloqueia a fuga), com cache por célula.
- O EVAC terrestre agora passa o mesmo `rejectBaseCluster` do aéreo e do reparo normal. O log de destino selecionado voltou ao consumidor `FindRepairConstruction`; `RankRepairConstructions` não anuncia escolha.
- Regressão ampliada PASS fora do Play: anel seguro recebe o ferido, rota comum com ameaça intermediária perde mesmo com destino livre, alternativa de rota segura preservada, HQ distante aproxima sem sondagem remota. `bash tools/compilar.sh`: ambas assemblies passaram. Sem observação desta revisão em partida real.

- Fallback final em degraus: prefere o anel seguro; sob invasão usa células livres permitidas do anel, minimizando primeiro ameaça da descida, depois pico/total de ameaça da rota e custo. Ameaça positiva e inimigo perto não vetam esse último recurso. Ocupação, produtores fechados e PodeDesembarcar continuam obrigatórios. Sem desembarque imediato, aproxima/escapa; uma origem ameaçada não gera espera quando há movimento permitido.
- Testes PASS adicionais: inimigo a 1 hex do HQ entrega no anel; log `anel contestado, menor ameaça`; todas as rotas com ameaça positiva ainda entregam na célula de menor ameaça; origem ameaçada com movimento disponível não espera. Compilação via `bash tools/compilar.sh` passou nas duas assemblies. Validado fora do Play.

### 2026-11-07 — entrega conjunta de feridos no EVAC
- O EVAC passa a comparar LZs pela quantidade de feridos desembarcados em células distintas, depois pela quantidade entregue diretamente em reparo; o ranking de hospitais desempata entregas equivalentes.
- Reutiliza o matching do `MelhorDesembarqueService` sobre as opções já sondadas, sem novo scan de movimento/topologia/sensor. Preferência opcional de spot separa reparo de terreno livre, sem falsificar custo de rota; consumidores sem preferência mantêm o comportamento.
- Sem duas vagas de reparo, o principal desce no hospital e o secundário ferido permanece a bordo para a próxima entrega. Terreno livre só é aceito pelo fallback final do anel do HQ. Mantém as travas de segurança da entrega comum e a política de hospitais. Passageiros saudáveis não entram na entrega de EVAC.
- Seleção compartilhada dos EVACs terrestre/naval recebe o snapshot existente e também combina feridos; estacionamento ao lado do hospital considera a quantidade entregue.
- `verificar_evac_predio_vago.cs`: PASS fora do Play, incluindo dois hospitais, secundário retido sem vaga e atendido na entrega seguinte, dois feridos no anel HQ, spots exclusivos, passageiro saudável retido e estado confirmado inalterado. `verificar_passageiro_prioritario.cs`: PASS.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor passaram. Ainda sem validação desta alteração em partida real.

- Revisão do EVAC conjunto: `repairCells` é construída uma vez na entrada de cada decisão aérea/terrestre/naval e passada a estacionamento, seleção e fallback HQ. O hospital principal já ranqueado é reutilizado; só o secundário exige o ranking próprio. Ranking auxiliar usa `log: false`; `FindRepairConstruction` preserva o diagnóstico normal da escolha.
- Regressão PASS com contadores: aéreo com dois feridos e fallback HQ fazem 1 `EvacRepairCellsBuilds` e 2 `RepairConstructionRankings`; APC (um banco) faz 1 de cada, sem reconstrução na seleção/estacionamento. Consulta auxiliar não imprime `[Repair] skip`. Uma vaga retém secundário, que assume a entrega seguinte; duas vagas e fallback no anel continuam descarregando dois.
- `bash tools/compilar.sh`: ambas assemblies passaram. Testes executados fora do Play; sem observação desta revisão em partida real.

### 2026-11-07 — contorno flexível para a dupla de EVAC no HQ
- Marca T9/slot 1, Chinook #7: Soldados #28 e #31, ambos 4 HP e em reparo no save de referência; a entrega no anel do HQ selecionava só o #28.
- No fallback do HQ, o principal ancora a entrega dentro do anel. O segundo ferido pode usar terreno livre adjacente fora do contorno exato aceito, desde que uma rota terrestre de um tático dele alcance o ponto do principal. Reutiliza o mapa de rota do passageiro e o matching existente, sem reconstruir repairCells ou sondar LZ remota.
- Em hospital comum permanece a retenção do secundário sem vaga; a flexibilização não transforma construções externas em terreno livre nem altera regras de ocupação.
- Regressão `verificar_evac_predio_vago.cs`: PASS com opções reais do sensor, dupla a 4 HP e apenas uma célula no contorno aceito; o segundo desembarca fora dele e pode completar o trecho a pé. Demais casos e contadores preservados.
- `bash tools/compilar.sh`: ambas assemblies passaram. Validado fora do Play.
- Revisão da borda externa: `MatchEvacPatients` recebe `requireSafeOutsideRing` do degrau escolhido. No anel seguro, secundário fora do contorno também precisa de `IsEvacDropCellSafe`; no contestado, preserva a aceitação de risco. Sem alterar hospital comum, ocupação ou limite tático a pé.
- Regressão PASS: mesmas opções de desembarque com inimigo junto da célula externa — secundário recusado no degrau seguro e aceito no contestado. Todos os cenários e contadores anteriores passaram. `bash tools/compilar.sh`: ambas assemblies passaram, Editor fora do Play.

### 2026-11-07 — reserva de pouso para manutenção de aeronaves
- Aeronave viva no ar, marcada para reparo e sobre instalação aliada controlada que permite pouso e oferece serviço necessário reserva a banda de solo para o serviço do comando. A reserva é consulta pura do estado atual no lado da IA, em `AIController.IsAircraftReservingRepairLanding`, sem mutação de ocupação/FOW/recursos.
- Apenas a IA evita encerrar movimento sob a aeronave reservada: `AddRepairLandingReservedCells` alimenta `BuildOccupied` e `CanAIUnitEndMoveAtCell` guarda o destino final. Movimento/pouso do humano continuam nas regras normais. Encerramento/atendimento da manutenção ou saída do prédio libera a reserva automaticamente.
- O shopping da IA considera o produtor reservado no planejamento, sem alterar a regra de compra do jogo/humano. Unidade da IA que já ocupa o solo da produtora recebe ação para sair mesmo sem caixa ou sem todas as fábricas estarem bloqueadas. A aeronave de manutenção não é expulsa para liberar compra.
- `tools/ai_test/verificar_reserva_pouso_reparo.cs`: PASS fora do Play — movimento/pouso/compra do humano preservados; filtro e guarda de destino da IA bloqueiam; shopping da IA evita o produtor; passagem livre, pouso próprio, saída do ocupante com orçamento zero, proteção da aeronave, liberação da reserva e estado confirmado preservado.
- `verificar_evac_predio_vago.cs`: PASS de regressão. `bash tools/compilar.sh`: ambas assemblies passaram. Sem observação desta política em partida real.

- Revisão de escopo: retirado o predicado e seus bloqueios de `OccupancyResolver` e retirado o veto de `TurnStateManager.ConstructionShopping`; política restrita à IA. `verificar_reserva_pouso_reparo.cs` e regressão completa de EVAC passaram fora do Play. `bash tools/compilar.sh`: duas assemblies passaram.

### 2026-11-07 — EVAC não espera por hospital sem aproximação viável
- Marca T10/slot 0, Chinook #56 com Soldado #27 (2 HP): hospital estimado em (3,13) mantinha a decisão em (3,4), sem acionar o HQ.
- LZ hospitalar estimada agora exige ao menos um destino dos caminhos seguros atuais que realmente reduza a distância até ela, fora das paradas proibidas. Sem esse avanço, o hospital é descartado para aproximação e a busca segue para o próximo; se nenhum permitir avançar, usa o fallback do HQ com os caminhos originais e os degraus já existentes.
- A guarda final também aciona o HQ se o seletor de aproximação inesperadamente devolver origem igual a destino. Reusa caminhos e reservas já calculados; não sonda desembarque remoto nem relaxa ameaça em hospital comum. Recusa de aproximação ganha motivo no log.
- Regressão `verificar_evac_predio_vago.cs`: PASS para hospital melhor ranqueado sem progresso seguido de outro com avanço; hospital futuro existente sem nenhum passo libera entrega no anel do HQ. Casos anteriores e contadores preservados.
- `bash tools/compilar.sh`: ambas assemblies passaram. Validado fora do Play, sem executar a partida da marca.

### 2026-11-07 — aproximação ao HQ não economiza MP por ameaça acumulada
- Marca T10/slot 0, Chinook #56: fallback do HQ já acionado, mas escolhia só (3,4) → (2,5), com seis MP disponíveis. O desempate priorizava soma da ameaça da rota antes do progresso, favorecendo caminhos curtos.
- Mantido o menor pico de ameaça como critério principal de voo. Com pico igual, agora vence o maior avanço ao anel; exposição acumulada desempata depois. Hospital comum e travas de ameaça zero preservados.
- Log do fallback registra avanço em hexes, passos, pico e exposição acumulada para tornar o desempate legível no AI Step.
- Regressão `verificar_evac_predio_vago.cs`: PASS com HQ distante e ameaça uniforme 30 em todo o caminho; usa o deslocamento tático completo de seis passos. Demais cenários passaram. `bash tools/compilar.sh`: duas assemblies passaram; validação fora do Play, sem replay da marca.

### 2026-11-07 — fusão insegura não é âncora de recuperação aérea
- Marca T10/slot 0: Apache #65 (2 HP) avançava para o Apache #35 (3 HP), em (13,12), perto de inimigos. A aproximação reconhecia tipo/aliança/HP, mas ignorava a segurança exigida pela execução.
- `IsRepairFusionSiteSafe` compartilha a regra: célula segura pelo EVAC e, na invasão, dentro da retaguarda segura. Execução usa a pergunta única e mantém logs separados de ameaça/retaguarda; a âncora consulta a célula do parceiro antes de aceitar fusão como recuperação.
- Recusa registra parceiro, motivo e célula uma vez por snapshot/ator/parceiro, sem remover ou consolidar as avaliações repetidas de AircraftRepairRecovery (fora deste escopo).
- `tools/ai_test/verificar_ancora_fusao_retaguarda.cs`: PASS fora do Play — par compatível 2+3 HP com parceiro cercado não cria âncora nem é escolhido como fusão pela recuperação; o mesmo parceiro em célula segura cria âncora. Log de recusa único mesmo com consultas repetidas; sem fusão executada ou alteração de HP.
- `bash tools/compilar.sh`: ambas assemblies passaram. Não validado nesta revisão em partida real.

### 2026-11-07 — recuperação aérea valida o atendimento na célula futura
- Marca T10/slot 0: Chinook #2 seguia para floresta (5,14), ao lado do Suprimentos #67. A compatibilidade de pouso era consultada no hex atual e o primeiro vizinho alcançável virava atendimento sem validação do destino.
- Cada supridor guarda as células de serviço válidas (própria + seis vizinhas), consultadas uma vez por coleta. Atendimento na camada atual dispensa pouso; caso contrário, `PodePousarSensor.Evaluate(atCell: ...)` valida a célula futura e sua camada final precisa ser operada pelo supridor. Consulta pura, sem projetar estado confirmado da unidade.
- O Tático compara todos os vizinhos alcançáveis e válidos. O Operacional reutiliza as células validadas e não aceita supridor sem nenhuma. Removido o bônus de 5000: distância e ameaça comparam supridor e construção; supridor só vence empate exato. Construção exige serviço necessário disponível pela consulta existente `CanSurfaceRepairConstructionServe`, além do pouso válido.
- `tools/ai_test/verificar_recuperacao_aerea_celula_servico.cs`: PASS fora do Play — floresta recusada e outro vizinho aceito; melhor vizinho válido mesmo quando outro válido aparece primeiro; ausência de vizinhos elimina âncora Tática/Operacional; construção no caminho vence supridor distante; prédio ocupado/sem serviço recusado; supridor mais próximo vence e desempata independentemente da ordem. Sem alteração de HP, combustível, camada ou HasActed.
- Regressão `verificar_ancora_fusao_retaguarda.cs`: PASS. `bash tools/compilar.sh`: ambas assemblies passaram. Não reproduzido em partida real nesta revisão.
### 2026-11-07 — capturador que cede no swap procura outra captura
- Marca T10/slot 0: Soldado #58 (4 HP) liberava a cidade para #15 (10 HP), mas o fluxo tentava apenas combate e movimento, sem captura oportunista em outro prédio.
- `DecideSwapVacateAction` reutiliza `TryFindUnreservedOpportunisticCapture` com o hex cedido excluído: captura oportunista → combate útil → movimento de agenda como último fallback. Registra destino planejado e motivo no AI Step.
- Durante essa consulta, o substituto está comprometido com o prédio liberado e não disputa as outras oportunidades. Parâmetro opcional atravessa a autoridade existente de reservas; todos os demais chamadores preservam o comportamento anterior e terceiros disponíveis continuam podendo reservar uma captura.
- `tools/ai_test/verificar_swap_captura_oportunista.cs`: PASS fora do Play — outro prédio gera Capture em vez de None; não captura o hex cedido; substituto mais forte não monopoliza o segundo prédio; reserva legítima de terceiro e comportamento normal preservados; sem outra captura/combate, movimento libera o hex; sem confirmar movimento/captura/HP.
- Regressão `verificar_recuperacao_aerea_celula_servico.cs`: PASS. `bash tools/compilar.sh`: ambas assemblies passaram. Ainda não validado em partida real.