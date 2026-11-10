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
### 2026-11-08 — logística ordena manutenção pelo risco de não atender
- Marca T10/slot 0: o corretivo ignorava combustível no cálculo compartilhado, enquanto só a aproximação tinha bônus separado de até 20% de autonomia. HP baixo dominava preço e elite, e aproximação/atendimento podiam discordar.
- `ResolveLogisticsNeed` devolve faixa e componentes: autonomia aérea crítica pela autoridade existente `IsAircraftFuelCriticalForNextUpkeep`; HP até 50%; demais corretivos; preventivos. Faixas estritas, com limites para pontuação interna e contexto, inclusive na escolha da célula de serviço. Retirado o bônus percentual exclusivo da aproximação.
- Corretivo considera proporcionalmente HP, combustível e munição faltantes. Valor usa preço e elite com teto combinado de 9000 (preço até 6000, elite até 4500). Bônus estratégicos de papel/munição e penalidade da infantaria com fusão permanecem dentro da faixa; preço e elite deixam de ser duplicados nesses bônus. Preventivo mantém seus componentes anteriores, subordinado às faixas corretivas.
- Aproximação, ordem do atendimento e posição do supridor consomem a mesma necessidade; sensor/legalidade permanecem antes da seleção. Logs das escolhas finais mostram faixa, gravidade, valor e estratégico, sem log por opção sondada.
- `tools/ai_test/verificar_prioridade_manutencao_logistica.cs`: PASS fora do Play — Chinook no limite de autonomia vence soldado 2 HP; acima do limite perde; aproximação desde outra posição e atendimento pelo sensor ao chegar concordam nos dois casos; preço/elite extremos não ultrapassam faixa; elite desempata necessidades semelhantes; preventivo abaixo de corretivo; combustível e munição proporcionais; contexto não atravessa faixa; diagnóstico completo e sem atendimento confirmado.
- Regressão `verificar_recuperacao_aerea_celula_servico.cs`: PASS. `bash tools/compilar.sh`: ambas assemblies passaram. Ainda não validado em partida real.
### 2026-11-08 — prazo legal de atendimento dentro da faixa de manutenção
- A aproximação agora compara faixa → prazo (Tático antes de Operacional) → pontuação interna/contexto. O degrau de prazo cabe dentro da faixa: preventivo próximo não ultrapassa corretivo distante.
- O envelope Operacional existente é montado uma vez e compartilhado com a checagem de compatibilidade. A busca já existente pelas origens de serviço guarda o menor número de turnos de uma origem aceita pelo PodeSuprir; respostas do sensor por origem são reutilizadas entre alvos. Não cria busca de caminhos por alvo nem usa distância para determinar prazo.
- Opções legais de atendimento desta rodada recebem prazo Tático. Aeronave `AircraftAutonomy` sem origem legal nesta rodada sai da disputa do supridor com log `cai antes do atendimento`; permanece sob a recuperação/pouso de emergência existente.
- `FindLogisticsServiceTargetWithTiming` entrega o prazo junto do alvo ao planejamento de transporte. `EnsureTransportSupplyPlanning` deixa de classificar suprimento pela distância de movimento, incluindo atendimento após mover e usar alcance adjacente. Logs de aproximação/atendimento mostram prazo.
- `tools/ai_test/verificar_prazo_atendimento_logistica.cs`: PASS fora do Play — crítico Tático vence crítico Operacional com necessidade maior; preventivo Tático perde para corretivo Operacional; aeronave sem tempo sai com motivo; aproximação e atendimento real concordam; origem geometricamente Tática mas ocupada não conta; planejador classifica como Tático o serviço adjacente além do movimento. Sem atendimento confirmado.
- Regressão `verificar_prioridade_manutencao_logistica.cs`: PASS. `bash tools/compilar.sh`: ambas assemblies passaram. Não validado em partida real nesta revisão.
- Validacao em Play pelo autor: na marca T10, mantida a escolha da AAA critica no Operacional; Chinook com 13 de autonomia e upkeep 2 buscou a produtora vazia para recuperacao, como esperado.

### 2026-11-08 — entrega aérea localizada para RallyAssembly
- Marca Golf, Chinook #72: missão de reunião passava pela resolução de captura e terminava com alvo no mar sob o helicóptero. Passageiros permaneciam embarcados apesar de terreno disponível perto do rally ocupado.
- `TryBuildAirRallyAssemblyDelivery`, em `AIController.Transportador.Air.cs`, intercepta a entrega quando o passageiro principal está em RallyAssembly ativo e o rally está controlado. Usa a âncora autorada e a área atual de reunião (`RallyAssemblyForceRadius`), sem exigir construção capturável. Não implementa ainda o core por unidade-modelo da proposta.
- `MelhorDesembarqueService.Evaluate` escolhe a combinação conjunta nas LZs alcançáveis; `PodeDesembarcar` mantém a legalidade física. Filtros preservam área de cada passageiro, visibilidade da LZ, ocupação aérea, inimigo no ponto de descida e destinos planejados. Prioriza a entrega conjunta, permite entrega parcial e preserva passageiros de outra missão.
- Sem entrega legal nesta rodada, aproxima da âncora real ou espera com motivos de recusa; não substitui o alvo terrestre pelo hex de mar atual. EVAC conserva precedência; GoGreen, captura comum e rally ainda não controlado seguem os caminhos anteriores. Contagem de prontidão e demais regras de rally não foram alteradas.
- `tools/ai_test/verificar_desembarque_rally_aereo.cs`: PASS fora do Play — rally aliado ocupado entrega dois soldados no entorno; uma vaga entrega um; sem solo legal apenas aproxima/espera; outra missão não é desviada; GoGreen/captura/rally incompleto não interceptados; planejamento sem confirmar desembarque, HP, posição ou HasActed.
- Regressão `verificar_evac_predio_vago.cs`: PASS. `bash tools/compilar.sh`: ambas assemblies passaram. Ainda não reproduzido em partida real nesta revisão.
- Validacao em Play pelo autor: o Chinook desembarcou os dois soldados na area do rally; caso da marca resolvido.


### 2026-11-09 — shopping: resposta híbrida à parede de artilharia
- `BuildArtilleryWallDemands` separa suporte terrestre e antiaéreos pelo papel principal, sem duplicar InstanceId. Prefere AtaqueAereo contra suporte e Assalto contra AA, com conversão por oferta/acesso real, não por caixa. Apache principal DisruptorDeCaptura fica fora. Resposta primária ofertada precede secundária no domínio escolhido.
- Escala normal: uma resposta por três peças totais, teto global três, distribuída proporcionalmente; invasão: 1:1 sem teto. Unidades existentes com acesso e compras de composição descontam cobertura, sem usar a mesma peça nos dois grupos. Ofertas preservam o produtor com acesso, usando o mapa de movimento terrestre existente do SectorManager.
- `tools/ai_test/verificar_demanda_parede_artilharia.cs`: PASS em cena temporária fora do Play. Cobre 2 AAA + 1 obus, parede mista maior, teto, invasão, preferência primária/secundária, conversão nos dois sentidos, Apache excluído, preferência mantida com caixa insuficiente, cobertura existente e do carrinho, tanque ilhado e dois produtores do mesmo tanque com acessos distintos. Repintura de terreno da fixture invalida explicitamente o cache do SectorManager.
- `bash tools/compilar.sh`: ambas assemblies passaram; `git diff --check` dos arquivos alterados passou. Nenhuma compra, movimento ou gasto confirmado. Fixture isola a progressão (`eliteFrom = null`); política de compromisso/poupança existente não foi reescrita nem validada ponta a ponta em partida nesta revisão. Custo em mapas grandes ainda precisa de observação.


### 2026-11-09 — pressão ponderada de artilharia e Assalto
- Extensão da demanda híbrida: artilharia 0/1/2+ vale 1/1,5/3 pontos; Assalto terrestre vale metade (0,5/0,75/1,5). Três pontos geram uma resposta, teto normal três. Classificação usa o papel existente, com artilharia/AA antes de Assalto e identidade única. Antiaéreos permanecem separados, preferindo Assalto; suporte e Assalto inimigos preferem ataque aéreo.
- Invasão mantém uma resposta por artilharia, sem multiplicador de elite; pressão suplementar de Assalto conserva os pesos normais. Oferta, acesso, poupança e desconto de cobertura continuam pelo caminho existente. Logs/motivos passam a mostrar os pontos.
- `verificar_demanda_parede_artilharia.cs`: regressão estendida fora do Play para pesos, uma artilharia elite 2, duas elite 1, dois Assaltos elite 2, seis básicos, quatro elite 1, identidade duplicada e ArtilheiroCombatente sem dupla contagem. Sem compras confirmadas; falta observar em partida.


### 2026-11-09 — Caça B: presa aérea operacional antes do tiro terciário no solo
- Marca T11, slot 0, Caça B #73: escolheu tirar 1 HP do SAM #70 com VG, preferência terciária. A trava de prioridade aérea via apenas os candidatos do Tático; helicópteros no Operacional não participavam.
- `DecideRogueAirCombatAction` consulta `TryResolveInterceptorOperationalPursuit` antes de confirmar o ataque terrestre terciário. Só vale para papel principal Interceptador; a presa precisa estar no ar, viva, desembarcada, visível na lista já coletada, primária/secundária e compatível com arma carregada. Reutiliza `TryResolveOperationalHuntTarget` e seu envelope de combate Operacional uma vez, sobre a lista filtrada.
- `FindAirCombatAdvanceMove` fornece a aproximação pelos caminhos, ocupação, regras de decolagem e ponderação de ameaça existentes. Só troca o tiro se houver avanço; sem avanço conserva a decisão anterior. Ataques aéreos táticos, tiros terrestres não terciários, Disruptores, recuperação e busca rebelde conservam o fluxo anterior. Log registra a presa, o destino e o tiro adiado.
- `verificar_interceptador_presa_operacional.cs`: fixture temporária fora do Play cobre presa apenas no Operacional (envelopes reais), aproximação legal com progresso, alvo aéreo tático preservado, Disruptor não desviado, ausência de presa, passageiro embarcado, aeronave pousada e alvo fora do envelope. Não confirma ataque/movimento. Ambas assemblies compilaram. Falta validar a marca em partida.


### 2026-11-09 — shopping do rally: acesso por produtora e Assalto aéreo
- Marca T11, slot 0: Tanque B em (0,15) vinha da ruptura de Papa (`rally-assembly`, prioridade 9), passando por fora da conversão por isolamento usada pelas operações comuns. Pressão no anel era zero; a compra não era uma resposta defensiva.
- `ConfigureRallyShoppingAccess` filtra ruptura e artilharia pesada por oferta, progressão e acesso real entre cada produtora e a âncora do rally. Reutiliza `TryGetRallyAnchorCell` (agora interno ao assembly), com destino da operação como fallback; mapas terrestres existentes do SectorManager são reutilizados por perfil/alvo na decisão. A elegibilidade acompanha a demanda em todos os seletores que já consultavam `MatchesWallOffer`, incluindo carrinho e poupança. Demandas de rallies diferentes não se fundem perdendo o vínculo com o destino.
- Sem ruptura terrestre ofertada com acesso, a demanda usa AtaqueAereo da família Assalto, incluindo Apache. Sem alternativa alcançável, não compra peça encalhada; artilharia pesada sem acesso não é substituída por antiaéreo. Caixa insuficiente não torna a oferta inexistente. A contagem existente do core por família foi preservada: dois Apaches contam como dois Assaltos, e quatro Soldados como quatro Capturadores; parâmetros numéricos do AI Manager e a prontidão do rally não foram reescritos.
- `verificar_shopping_rally_acesso.cs`: PASS fora do Play — ilha separada converte para Assalto aéreo; mesma ficha em duas produtoras só aceita a que alcança o rally; artilharia pesada respeita o mesmo acesso; sem alternativa não compra; orçamento de $1000 não elimina oferta; core 2 Apaches + 4 Capturadores conta corretamente. Sem compra confirmada. Regressão `verificar_demanda_parede_artilharia.cs`: PASS. Ambas assemblies compilaram; falta observar em partida.


### 2026-11-09 — interceptadores não são compras contra pressão terrestre
- Marca T11, slot 0: Caça A #93 comprado por $26000 como `counter-pressure-elite` AntiInfantaria, não pela demanda de interceptação. O roster escolhia pela arma secundária sem respeitar a missão da ficha.
- `IsCounterResponseRoleAllowed` restringe papel principal Interceptador (incluindo InterceptadorDeDisruptor por `MatchesLabel`) à categoria AntiAerea nas contramedidas de compra. Compartilhado por `FindBestRosterCounter`, filtro final `DoesUnitMeetShoppingDemand`, bônus `ScoreCounterFitForDemand` e `OffersNonLandCounter` (um caça não promete resolver a infantaria de outra ilha). `ResolveElitePurchaseCommitment` invalida compromisso antigo de counter incompatível com essa regra e registra o motivo.
- Demandas próprias de interceptação e resposta a ameaça aérea continuam. Ataque oportunista com arma secundária, fichas e combate não mudam; a cobertura real já existente continua sendo medida. Apache e Bombardeiro não são excluídos pela regra dos interceptadores.
- `verificar_interceptador_counter_terrestre.cs`: PASS fora do Play, usando fichas reais de Caça A/B e seleção do roster/filtro do carrinho. Recusa contramedida terrestre; preserva AntiAerea, demanda de Interceptador, Apache AntiInfantaria e Bombardeiro AntiTanque. Não compra, não altera ledger e não confirma ações. Ambas assemblies compilaram; falta observar em partida. A migração de compromisso antigo foi conferida no fluxo de validação, sem modificar o ledger da partida no teste.


### 2026-11-09 — counter elite: pré-requisito não fura isolamento
- Marca T11, slot 0: novo Tanque B em (0,15) vinha de `counter-pressure-prerequisite` AntiInfantaria, para liberar Tanque A; a compra do rally já havia sido convertida corretamente para Bombardeiro. `RequiredUnitId` fazia o pré-requisito pular a exclusão terrestre.
- `FindBestEliteCounter` aplica a régua existente de `CounterOffLandShare` e `OffersNonLandCounter` antes de ranquear a elite: quando a pressão do matchup está majoritariamente em outra ilha e existe resposta não terrestre ofertada, candidatos terrestres não disputam nem geram sua cadeia. `FindBestRosterCounter` recebe esse filtro opcional; demais usos conservam seus padrões.
- `AddCounterPressureDemand` deixa de isentar ficha exata da exclusão terrestre. Uma contramedida aérea exata continua válida; falta de caixa não muda oferta/acesso. `ResolveElitePurchaseCommitment` também invalida um compromisso terrestre antigo quando as demandas atuais do mesmo matchup já excluem terra, com motivo no log. Sem essa exclusão atual, preserva a persistência anterior.
- A régua de ilha é a já existente (fatia >= 50%, havendo oferta não terrestre), não uma nova busca geométrica ou sensor por produtor/alvo. Sem alternativa não terrestre mantém o fallback anterior; pressão local conserva progressão terrestre. Não altera compra por rally nem a leitura de cobertura das peças já existentes.
- `verificar_counter_prerequisito_ilha.cs`: PASS fora do Play — elite terrestre isolado sai antes do pré-requisito; ficha exata não fura exclusão; Apache básico permanece; elite aéreo pode ser escolhido; pressão local conserva Tanque B -> A. Sem compras nem ledger alterados; cancelamento de compromisso antigo conferido no fluxo de validação, sem mexer na partida. Ambas assemblies compilaram. Regressões de interceptador/counter terrestre, shopping de rally e parede ponderada: PASS. Falta observar a marca em partida.


### 2026-11-10 — Caça A preventivo e controlador dos céus
- Doutrina do autor: piso preventivo de um Interceptador elite 1 pela progressão, não só reação a bombardeiros. Gatilho compartilhado de aeroporto inimigo com produção ou aeronave inimiga visível. `AddEliteProgressionDemand` recebeu teto opcional; só a linha Interceptador usa nível máximo 1. Caça B e A resolvem composição Interceptador; contagem não depende da igualdade estrita do subpapel. Log `origem=elite`, motivo `controle do ar`.
- `FindEliteInterceptorReserveTarget` valida cadeia, oferta e teto. `CompareEliteReserveLines`/`SelectEliteReserveTarget` fazem a terceira linha disputar em paralelo: com aeronave visível, Interceptador antes de Assalto; sem ela, Assalto primeiro. Carrinho por papéis conserva o compromisso, cálculo de reserva e pré-compra existentes. No fluxo legado a nova linha reaproveita progressão/core, `ComputeStrategicSavingReserve` e `TryPreCommitDemand`, sem outro modelo de poupança. Compromisso já assumido não foi cancelado só para trocar de linha.
- Achado durante a fixture: AR Caça A era Defensive. Por instrução posterior explícita do autor, AR Caça A e AR Caça F foram alterados para `aiPurchaseMode=Either` pelo Editor; não há bypass por postura no código. F permanece fora do piso preventivo pelo teto, portas reativas não reescritas. Presets e gates de core/cadeia/EliteSaveTurns permanecem.
- `AIAirExposureService` extrai `GetAntiAirUmbrella` e `TouchesUncovered` do shopping e passa a servir também a escolta. Caches locais por decisão/classe/inimigo. O wrapper antigo pedia combate Terrestre para SAM de mínimo >1 e recebia null; a seleção da subetapa agora consulta `IsSubStepValid` do serviço de alcance e usa Artilheiro quando requerido (Aereo para aeronaves). Não duplica a regra de arma.
- Sem presa aérea operacional e sem tiro imediato prioritário, `ResolveAirCombatFallbackAnchor` protege exposição: ponto comum alcançável no Tático do caça e nas bandas de resposta dos dois elos; sem interseção, EWACS exposto; senão vanguarda. EWACS coberto sem vanguarda não atrai por proximidade. Interseção usa duas bandas reversas de Mobility com OriginOverride e as rotas já montadas do caça, sem mover unidade e sem envelope por célula candidata; usa o centro minimax como referência e o ranking de movimento existente, incluindo ameaça, só dentro da interseção. Logs Exposed/Covered/Operational explicam o tier. Caça operacional continua antes da escolta; a presa escolhida também entra nos envolvidos no avanço normal.
- `verificar_caca_a_controle_ceu.cs`: PASS fora do Play — aeroporto sem bombardeiro pede um Caça A; sem aviação não pede; avistamento ativa; cadeia exige B; A em campo não pede nível 2; reserva alterna prioridade por avistamento; três posturas passam com Either; gate econômico/core preservado. Fixture monta o core explicitamente e não compra nem altera ledger.
- `verificar_interceptador_ancora_exposta.cs`: PASS fora do Play — exposição real dos dois elos por envelopes; cobertura conjunta no Tático; elos separados priorizam EWACS exposto; SAM em alcance cobre EWACS; coberto não puxa; ausência de EWACS usa vanguarda; estado confirmado intacto. A ameaça rápida sintética fica fora do Operacional do caça para isolar a política sem presa. Falta observar os novos comportamentos em partida real.

- Validação final desta frente: `bash tools/compilar.sh` passou nas duas assemblies, incluindo o novo serviço e seu `.meta` importado. A fixture de posição também executa `DecideRogueAirCombatAction` e verifica que `MoveTo` conserva a célula de cobertura conjunta validada (sem execução/commit). A fixture de compra verifica a ordem produzida pelo `TryPreCommitDemand`, sem transação de compra. Regressões `verificar_interceptador_presa_operacional.cs`, `verificar_interceptador_counter_terrestre.cs`, `verificar_counter_prerequisito_ilha.cs`, `verificar_shopping_rally_acesso.cs` e `verificar_demanda_parede_artilharia.cs`: PASS. Compromissos antigos e o comportamento das três dificuldades não foram exercitados ponta a ponta em partida; seus gates/configurações existentes não foram substituídos.

- Revisão de 2026-11-10: `TryFindSharedInterceptorCover` restringe caminhos à interseção e chama `FindAirCombatAdvanceMove`, reaproveitando integralmente o score e o peso de ameaça (`CalculateThreatLevel`, peso 120). Nesse uso pode manter posição ou mover lateralmente; os chamadores antigos preservam os padrões de avanço. O resultado não pode ser reavaliado fora da interseção. `AIShoppingDemand.IsPreventiveAirControl` identifica a linha sem interpretar Reason/Origin e participa da compatibilidade de merge. `ResolveAirCombatFallbackAnchor` devolve `out bool sharedExposedCover`; os consumidores usam/descartam o campo explícito, sem ler a string para decidir cobertura. Logs anteriores preservados. Ambas assemblies e as duas fixtures novas passaram novamente, além de `verificar_interceptador_presa_operacional.cs`. Fora do Play; sem ações confirmadas.

- Revisão 4 (2026-11-10): removido o retorno especial `Covered:EWACS; fallback HQ/hold`. EWACS coberto sem capitão agora segue os fallbacks já existentes abaixo, começando pelo inimigo mais próximo. Fixture ampliada com HQ presente, EWACS sob SAM e nenhum capturador no snapshot: espera a célula do inimigo e `Operational:CubicNearestEnemy`, não a do HQ nem espera forçada. Não muda a prioridade do EWACS exposto ou da vanguarda disponível.

- Revisão 5 (2026-11-10): `plannedEwacsEscorts` guarda EWACS InstanceId -> primeiro interceptador que escolheu sua escolta nesta passada; limpo ao lado de `plannedDestinations` no início de `Phase2_UnitActions`. `ResolveAirCombatFallbackAnchor` consulta o claim antes da cobertura conjunta: outro caça escolhe o capitão se ele está exposto, com tier `Exposed:EWACS#n (escoltado por #m) → Vanguarda#k`. Sem outro elo exposto continua aceitando o EWACS e conserva o primeiro dono do claim; o próprio dono pode ser reavaliado sem ser desviado. Não há atribuição global 1:1 nem persistência em save. Consultas de decolagem e do courier usam `registerEscortClaim:false` e não criam escolta por previsão.
- Fixture `verificar_interceptador_ancora_exposta.cs` ampliada: dois caças em sequência, duplicação quando não há outro elo, dono preservado, consulta sem registro, reavaliação idempotente e nova escolha após limpeza simulada do dicionário: PASS fora do Play. A limpeza no começo da Fase 2 foi conferida no código; a fixture não executa o laço de ações da partida. `verificar_caca_a_controle_ceu.cs` e as duas assemblies: PASS. Estado confirmado de movimento/ação não foi alterado; falta Play.


### 2026-11-10 — fatia A: partida e propriedade sem excecao de faccao

- `verificar_regras_sem_qg.cs`: PASS no Editor, fora do Play, em cena temporaria. Tres slots: sem QG toma QG, elimina apenas o dono anterior e a partida continua; promocao dinamica preservada. Compra segue FreeMarket/OriginalOwner/FirstOwner/Disabled, sem flag de faccao; ocupacao sem QG nao grava FirstOwner nos setters de slot/team, e o primeiro captor com QG grava.
- Zero unidades elimina tambem o slot sem QG e neutraliza suas construcoes pelo `MarkSlotDefeated`. Fixture cobre primeiro turno, configuracao desativada e tutorial; captura de QG tambem nao elimina em tutorial. Ultimo sobrevivente recebe motivo `HeadQuarterCaptured`.
- `QuadranteData.allowDefeatForZeroUnits` e configuracao autoral serializada, conservada pelo bake, editavel no Map Helper e aplicada pelo QuadranteController ao pintar. Sem quadrante, permanece o valor serializado da cena. Fixture confere aplicacao true/false e fallback null; pintura completa ainda nao observada em Play.
- Removidos campo `allowRebelAIPurchase`, excecao de compra e secao correspondente do Inspector. Antes da remocao, flag ligada em Barracks, Docas, Garagem APC, Hidrobase e Terminal Rodoviario; autor avisado. Nenhuma sellingRule de instancia foi migrada automaticamente. Serializacao legada nos assets deixa de ser lida.
- Ambas assemblies compiladas por `bash tools/compilar.sh`. Teste usa a resolucao privada da captura apos a guarda de runtime; nao executa animacao nem captura em Play. Intercepta o evento final para nao registrar progresso nem iniciar apresentacao na cena do autor. Derivacao `IsHeadQuarterlessTeam` inalterada. B, C e renomeacoes nao iniciados.


### 2026-11-10 — A6: eliminacao por exercito tambem apenas elimina

- `HandleUnitDestroyed` e o inicio de `ReleaseUnitsForActiveTeam` usam `TryResolveArmyElimination`: marca/neutraliza pelo fluxo existente e consulta `TryDeclareLastStandingWinner(ArmyEliminated, defeatedTeam)`. Tres lados continuam com dois sobreviventes; 1v1 termina com o unico de pe. Guardas de runtime, tutorial, configuracao e turno preservadas.
- `DeclareEliminationVictory` ficou apenas para rendicao; removida a sobrecarga por TeamId sem chamadores e corrigido o comentario antigo sobre primeiro eliminador.
- `verificar_regras_sem_qg.cs`: PASS ampliado, fora do Play. O mesmo funil chamado no inicio do turno foi exercitado com slot sem QG/zero unidades: tres slots eliminam/neutralizam sem vencedor; dois slots coroam o sobrevivente com `ArmyEliminated`. Fixture nao executa o turno completo nem dispara destruicao em Play; ligacao dos dois pontos ao funil conferida no codigo. Demais casos da fatia A continuam passando. Ambas assemblies compilaram com `bash tools/compilar.sh`.
- B e C nao iniciados.


### 2026-11-10 — fatia B: shopping sem QG por demanda reativa

- Caiu a porta `rebel-insurgency` (seis capturadores fixos). `BuildRoleShoppingDemands` monta roster/counter para todos e pergunta o fato `HasShoppingBase` (derivacao de QG existente). Sem base, nao entra em pacote 2/2/1, deficits de operacao/rally, vigilancia e progressao/qualidade de elite. Com base, o fluxo anterior continua.
- Captura: `CountUncoveredCaptureOpportunities` consulta `CaptureOpportunityClaimService.CountUnclaimedOpportunities`. O servico existente conta identidades uma vez, desconta claims publicados e Mission Intent de capturadores vivos (inclusive passageiro embarcado/formal). Nao ha segundo matching. A ficha ofertada usa a chave de captura do `PodeCapturarSensor`; o acesso usa a consulta de componente por ficha/produtora do `SectorManager`, reutilizando `ArtilleryWallContext`. Uma varredura por ficha/origem, nao por alvo; sem oferta legal ou sem acesso proprio nao conta. O magnetico do capturador e o matching existentes continuam donos do destino; nenhum ramo de comportamento foi alterado.
- Carona: a porta `AppendRideQueueTransportDemand` passa a ler `PressaoCaronaService.Read`, ja usado pela pressao operacional; conserva o calculo de gap/prioridade da porta. O dono da pergunta continua sendo o QueroCarona, com o mesmo pedido do capturador, sem um novo caminho de transporte. Fila nao vazia ainda precisa de observacao em Play; a fixture B nao valida uma coleta/compra de transporte.
- Counter: a mesma `AddCounterPressureDemands` agora e alcancada sem QG. Preservadas matriz, legalidade e resposta reativa, inclusive escalada reativa; isso nao e progressao preventiva. Nenhuma lista especial de produtores: demanda e carrinho usam `CanProduceUnitsForSlot`, como na A.
- Reservas/compromisso de elite e bootstrap blindado perguntam o mesmo fato da base. Compromisso antigo sem QG e cancelado no fluxo normal do shopping. Conscricao/imposto e recrutamento forcado de massa tambem nao contornam as demandas reativas sem base. Os seletores legados de reserva/base foram protegidos pelo mesmo fato; nao ha preset por faccao. Cancelamento de compromisso e conscricao conferidos no codigo, sem exercitar o ledger real da partida na fixture.
- `verificar_shopping_sem_qg.cs`: PASS fora do Play. Dois predios livres alcancaveis pedem dois capturadores; claim publicado reduz para um e passageiro embarcado conserva esse desconto; predio em outra ilha e mercado Disabled nao geram captura; Tanque A inimigo visivel gera counter AntiTanque; sem pacote/progressao/vigilancia/rally/operacao ou alvo de reserva; promover a produtora a QG religa composicao. Usa catalogos reais de combate em cena isolada e restaura publicacoes/singletons. Nao confirma compra nem acao.
- Regressao A/A6, controle preventivo do Caca A e counter/pre-requisito ilhado: PASS fora do Play. Ambas assemblies compiladas por `bash tools/compilar.sh`; diff sem erro de whitespace. Sem validacao em partida. A/A6 comitadas em `56980fe`; B fica para revisao. C e renomeacoes nao iniciadas.
