# v9.5.0 — Uma IA validando a IA

O fio do dia: o Claude passou a **jogar** o jogo e a comparar cada decisão sua com a
que a IA tomaria no mesmo estado. O Unity CLI ligou o agente ao editor aberto, e o
que se achou jogando valeu mais que horas lendo código: três defeitos reais na IA do
Exército, um deles escondido atrás de outro.

---

## 1. O Claude joga: Unity CLI e o validador

O pacote `com.unity.pipeline` (guia em `docs/indie/unity_cli_mcp.md`) deixa o
terminal recompilar dentro da Unity, ler o Console e executar C# no editor
(`unity command script eval`). Com isso o Claude jogou o slot 1 em hot seat contra o
autor, **sem tela**:

- **lê o tabuleiro pelos sensores** (`CalcularCaminhosValidos`, `PodeCapturar`,
  `PodeMirar`), não por screenshot — o jogo foi feito para isso;
- **pergunta à IA** no mesmo estado: `BuildObjectivePlan` + `DecideUnitAction` por
  reflexão, **uma unidade de cada vez**, porque a IA é sequencial;
- **age pelo mesmo caminho da IA e do humano**: o batch (`BuildMoveBatch`,
  `BuildCaptureBatch`, `BuildShoppingBatch`, `BuildEndTurnBatch`) vai para
  `ExecuteLiveAIBatch`, e o automata emula a entrada — cursor, menu, confirmação.

**Duas correções de método, ambas do autor.** No turno 1 o Claude chamou funções por
dentro (`TryAIDirectPurchase`): a jogada valeu, mas não testou o menu nem o automata.
E pediu as três decisões de uma vez, o que mandou os três soldados ao mesmo hex —
artefato do ensaio, não da IA.

**Um erro do Claude que custou a partida:** `unity recompile` com o jogo em Play
recarregou o domínio e a partida voltou ao turno 1. O save do autor salvou o dia.
Regra: em Play, só `tools/compilar.sh`.

O registro completo, turno a turno, está em `docs/playtest_claude.md`.

## 2. O que o validador achou

| achado | tipo | onde |
|---|---|---|
| liberar produtora sai **sem capturar** o próprio alvo | bug | `Capturer.Vacate` |
| liberar produtora sai **para o lado errado**, ignorando a missão | bug | `Router` |
| vaga de captura aberta no plano **não vira compra** | decisão discutível → corrigida | `AIShoppingPlanner.Demand` |
| blitzkrieg desligado no Médio, contra a intenção registrada no código | autoria | `AIPreset_Medio` |
| capturar prédio **inexplorado** no mesmo turno é recusado | **regra, não bug** | `PodeCapturarSensor` |

**O achado que se escondia atrás de outro.** A regra de liberar produtora roda antes
de todo papel. No turno 2 ela tirou o capturador de cima do QG com batch de *mover* e
o pôs **em cima do próprio alvo da missão** — chegou e não capturou. Corrigido isso,
o turno 3 mostrou o segundo defeito: ela escolhe a saída por pontuação local e mandou
o Unit11 **para trás**. E o Unit11 era justamente o escalado para o revezamento do
blitzkrieg — que, mesmo ligado, nunca teria quem assumisse a fábrica.

Correções: `BuildVacateBatch` (sair capturando quando a saída é capturável) e, no
roteador, **o papel decide primeiro** — a liberação só entra quando a ação do papel
deixaria a unidade parada na produtora.

**Compra.** O shopping por papéis tirava os capturadores do pacote 2/2/1 (sempre 2
até o núcleo passar de 5). A demanda agora é `max(pacote, vagas abertas no plano)`,
antes do assalto: no turno 3 dá exatamente o Soldado + Tanque que o Claude comprou.

**Não-bug que valia verificar.** O Unit5 chegou à cidade 6,4 e não capturou:
"Terreno ainda desconhecido". Destino provisório na névoa só libera o ataque. A IA
estava certa — e foi o validador que provou, antes de alguém "corrigir".

## 3. O autor como referência do Difícil

O autor jogou 3 rodadas em Neblina Leve (todos veem todos, LoS e LDT valem: a IA tem
a mesma informação) com a IA em Difícil, para servir de referência. A doutrina dele,
registrada: **formiga** (massa de Soldado, todo produtor ocupado todo turno) e o caro
**sob demanda**, como resposta ao que o outro comprou. No turno 3 ele fez o
blitzkrieg nos dois lados. A comparação jogada a jogada (replay do turno, IA
consultada antes de cada batch) ficou para a próxima sessão.

**O "base preset" não decide a dificuldade.** O AI Manager da Batalha tinha
`AIPreset_Dificil` como base e a partida rodava Médio: quem decide é a dificuldade do
contrato (ou `hardMode` da cena, abrindo direto), e o catálogo mapeia para o perfil.

## 4. Fatos para medalhas

Medalha é consulta sobre o `JogadasLog` no fim da partida; a FSM só registra fatos
(`docs/conquistas/`). Entraram: partida iniciada/encerrada, renda, autonomia, pouso
de emergência e pane seca, prédio e dono anterior na captura, DPQ dos dois lados no
combate, custo da compra, HP antes da fusão.

**Achado:** `Jogada.team` já era **slot** em todo registrador, mas o
`AIIntelAnalyzer` o comparava com a **cor** da IA — a memória da IA trocava amigo por
inimigo sempre que a cor não coincidia com o número do slot. Corrigido.

## 5. Também nesta versão

- `CONTROL_CONSTRUCTIONS` (tomar e **segurar**: o contador desce quando o inimigo
  recaptura) e o rascunho da prova de Soldado.
- **Diagnóstico no save** (outra sessão): fatos estruturados em `diagnostico.json`
  dentro do `.tmrsave` — funil de decisão de ataque, opção que o `PodeMirar` recusa,
  e cada combate com a **previsão da IA ao lado** (`docs/modding/diagnostico.md`).

## O que não terminou

- **Nada das três correções da IA foi visto rodando.** O save do turno 3 foi
  sobrescrito; validar com um cenário novo.
- **A comparação autor × IA Difícil** das 3 rodadas: o plano é um script único dentro
  do editor que percorre o replay em modo rápido e grava as divergências num arquivo
  (umas 3 chamadas, não 30). O risco é a corrotina do replay precisar de um objeto
  da cena para avançar.
- **O roteador agora decide o papel duas vezes** quando a liberação se aplica; uma
  reserva registrada na decisão descartada ficaria órfã. Nenhum caso concreto achado.
- **Aeronáutica e Marinha** sem validação. Ordem combinada: fechar o Exército com
  cenários, Aeronáutica num mapa pequeno, Marinha começando por doutrina, sem mapa.
- Fato 7 (detecção) e fato 10 (foto do exército) das medalhas.
