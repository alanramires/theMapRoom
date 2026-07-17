# Ideias Futuras — Multiplayer Online

Registro do debate de design (jul/2026) sobre jogar online com outro jogador. **Nada implementado** — este documento captura o raciocínio pra retomar sem reconstruí-lo.

---

## Os dois modos discutidos

1. **Assíncrono (PBEM-like)** — você joga seu turno, o pacote de jogadas é enviado ao oponente; ele abre o jogo "como se fosse modo AI", assiste o automata executar suas jogadas sob o fog dele, e então joga o turno dele. *Este é o alvo primeiro.*
2. **Tempo real (espectador ao vivo)** — um joga, o outro assiste em tempo real sob o próprio fog (como o humano assiste a AI hoje). *Evolução do assíncrono, não um projeto separado.*

---

## Por que o assíncrono está ~80% construído

A arquitetura atual já paga os custos difíceis, sem ter mirado em multiplayer:

1. **ReplayManager é executor, não gravador**: a AI já joga *através* dele (`ExecuteRecordedUnitActionBatch`), e o Serviço do Comando unificou preview/execução por replay pra matar divergência promessa≠execução. "Ver o automata executando as jogadas do outro" = modo AI de hoje com outra fonte de ações.
2. **Invariante transacional é ouro de rede**: só ações COMPROMETIDAS existem no log; nada provisório viaja. De brinde: anti-oráculo entre jogadores (o adversário nunca vê movimentos cancelados).
3. **Apresentação com fog por observador** já resolvida (`ShouldUseHumanFogPresentation` e família — o humano assiste o turno da AI pelo próprio fog).
4. **Determinismo como cultura**: combate é matemática fechada (sem dado), a fila de queda por combustível RE-RESOLVE deterministicamente no replay, save/load completo com DTOs.

---

## As 5 peças que faltam

> **Fundação construída (jul/2026)**: `MatchStateHasher` (Assets/Scripts/Shared/SaveData) — hash SHA256 canônico do `SaveGameData` (listas ordenadas por chaves estáveis; voláteis E estado derivado excluídos — caches de fog são recomputados no load/por cliente, hasheá-los daria falso desync). O save persiste canônico, loga `state_hash=` a cada gravação e guarda o hash no manifest (`stateHash`). Comandos debug: `state hash` (hash do estado vivo) e `state dump` (JSON canônico em arquivo, pra diffar quando divergir). Limitação v1: listas do planner/intel da AI ainda não canonicalizadas.
>
> **Round-trip VALIDADO em jogo (17/jul/2026)**: load → hash A; save → hash A; load → hash A (três medições idênticas = round-trip + idempotência). Cobertura cresce conforme o ritual roda em estados mais ricos (locks, pendências, estoques).
>
> **Nota sobre o registro de estudo**: o save já acumula `matchHistory` completo (um registro por turno com `StartSnapshot` + ações, sem poda) — o "replay da partida inteira desde o round 1" está sendo GRAVADO passivamente em todo save; falta apenas a UI de navegação do viewer (ReplayManager: batch e automated player em uso pela AI; viewer completo adiado por decisão).

1. **Pacote de turno serializado e versionado**: log de ações do turno + hash do estado final (+ savegame completo como seguro). *Metade do hash: pronta (acima). Falta o empacotamento do log de ações.*
2. **Validação anti-desync**: ao aplicar o pacote, comparar hash do estado resultante com o hash recebido. Bateu → segue; divergiu → fallback pro savegame do pacote. Transforma desync silencioso (o pior bug de multiplayer) em incômodo recuperável.
3. **Transporte**: v1 SEM servidor — exportar arquivo e mandar por WhatsApp/Discord (tradição PBEM; testável imediatamente). v2: relay burro (bucket + notificação). Nunca precisa de servidor com lógica de jogo (exceto anti-cheat, ver abaixo).
4. **Fluxo de chegada do turno** (o problema do "ué, cadê meu caça?") — ver seção seguinte.
5. ~~**Caça ao não-determinismo residual**~~ **CONCLUÍDO (17/jul/2026)** — auditoria de RNG no gameplay: o jogo é **100% determinístico no caminho competitivo** (combate com arredondamento explícito, sem dados, upkeep independente por unidade, execução por log/InstanceId). Achados do grep de `Random`:
   - 4 usos **cosméticos** (seleção de música, pulso visual da bandeira ×3, screen shake) — irrelevantes para estado;
   - 1 uso de **gameplay confinado ao tutorial** (`AutomataTargetPreference.Random` — alvo aleatório do automata, deliberado para o tutorial não ser decorável). Duplamente inofensivo: escopo tutorial + o sorteio roda UMA vez na máquina de quem age e o resultado vira batch gravado por InstanceId — replay/remoto reproduzem a escolha, nunca re-sorteiam.
   - Eixo ordem-de-iteração: resoluções automáticas de início de turno são independentes por unidade (pouso de emergência é in-place, sem disputa de hex; economia é soma por time); as que interagem (fila de queda por combustível) gravam a ordem nos substeps do replay.

---

## Fluxo de chegada: resumo primeiro, cinema depois

O problema: você loga e seu caça sumiu — "alguém abateu?". A resposta em camadas:

1. **Resumo obrigatório** ao abrir o turno: "Turno 12 do Vermelho: Caça B perdido, cidade Norte capturada, comboio atacado em (x,y)". Ninguém precisa assistir nada pra saber o que aconteceu de material.
2. **Replay opcional e pulável**: reprodução cinematográfica do turno inimigo FILTRADA PELO SEU FOG — você vê só o que suas unidades viram. É o modo AI de hoje.
3. **Detalhe fog-honesto**: unidade sua morta FORA da sua visão → o resumo não revela o assassino; diz "**contato perdido em (x,y)**". Você sabe que perdeu (a unidade é sua); o COMO é inteligência que você não coletou. Névoa funcionando na narrativa.

---

## Tempo real = mesmo log, entrega por gotejamento

Cada ação comprometida é transmitida na hora; o espectador aplica sob o fog dele. A arquitetura não muda — muda o transporte (conexão viva: relay websocket / Steam / Photon). Por isso a ordem: assíncrono primeiro (constrói log serializado, hash e apresentação); tempo real vira "assíncrono com entrega instantânea".

---

## Aviso de segurança (registrado, não resolvido)

No modelo par-a-par, o pacote carrega o estado completo — INCLUINDO unidades ocultas do adversário. Jogador técnico abre o arquivo e "fura" o fog.

- **Entre amigos**: irrelevante; v1 assume confiança e documenta.
- **Ranqueado**: exigiria servidor autoritativo que guarda o estado e entrega a cada jogador só o que o fog dele conhece. A separação LÓGICA já existe no FoW (conhecimento por time); virar separação FÍSICA é projeto grande. Não deixar esse fantasma atrasar o resto.

---

## Próximos passos SEM impacto no jogo principal (prateleira)

Todos aditivos — código novo ou atrás de comando de debug, zero mudança em fluxo de gameplay:

1. **Canonicalizar as listas da AI no hasher** (~meia hora, arquivo único) — remove a limitação v1; pré-requisito para hash cruzar máquinas.
2. **`export turn` (debug)** — empacotar log de ações do turno + stateHash + savegame de segurança num `.tmrturn` versionado. Só lê dados existentes.
3. **`import turn` dry-run (debug)** — abre o pacote, valida versão/cena e compara hash com o estado atual, SEM aplicar. Demo completa do detector de desync.
4. **Gerador de resumo de turno** — função pura sobre os registros do turno ("perdeu Caça B, cidade X capturada") + comando debug; vira o painel de chegada depois, e serve pro modo estudo e log local desde já.

**Primeiro passo que TOCA o jogo** (fica para quando decidido): aplicar o pacote pelo automated player — executa ações "estrangeiras" no tabuleiro real; mesmo atrás de debug merece cautela.

## Roadmap sugerido

1. Pacote de turno + hash — **fundação pronta e validada** (hash canônico, round-trip, manifest); falta o empacotamento do log (itens 2-3 da prateleira)
2. Aplicação do pacote como "modo AI" (engenharia de verdade)
3. Resumo de chegada (item 4 da prateleira prepara o dado; falta a UI)
4. Replay pulável do turno inimigo (UI sobre o que existe; dados já gravados — matchHistory completo)
5. Relay de transporte (infra, sem lógica de jogo)
6. (depois) Tempo real por streaming de ações
7. (um dia, se ranqueado) Servidor autoritativo com fog físico

**Determinismo (pré-requisito transversal): auditado e aprovado** — ver peça 5 acima.
