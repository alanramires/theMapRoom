# Ideias Futuras — Multiplayer Online

Registro do debate de design (jul/2026) sobre jogar online com outro jogador. **Nada implementado** — este documento captura o raciocínio pra retomar sem reconstruí-lo.

---

## Os dois modos discutidos

1. **Assíncrono (PBEM-like)** — você joga seu turno, o pacote de jogadas é enviado ao oponente; ele abre o jogo "como se fosse modo AI", assiste o automata executar suas jogadas sob o fog dele, e então joga o turno dele. *Este é o alvo primeiro.*
2. **Tempo real (espectador ao vivo)** — um joga, o outro assiste em tempo real sob o próprio fog (como o humano assiste a AI hoje). *Evolução do assíncrono, não um projeto separado.*

---

## Princípio central: a origem do batch não importa

O núcleo de execução não deve distinguir quem produziu uma intenção:

```text
AI local ───────────┐
humano local ───────┤
jogador remoto ─────┼─> batch versionado ─> executor transacional único
segundo jogador ────┤
replay/teste ───────┘
```

Cada origem decide **o que tentar**. O executor autoritativo continua responsável por validar e comprometer:

- slot ator e unidade;
- estado-base esperado;
- destino, alvo e ação solicitada;
- custos e regras;
- retorno a `Neutral`;
- efeitos definitivos e hash resultante.

O batch deve expressar intenção, nunca impor diretamente o resultado final. Por exemplo: “unidade X tenta mover para Y e atacar Z”, não “unidade X terminou em Y, gastou N e causou D”. Movimento, dano, combustível, detecção e captura são derivados pelo mesmo código usado no jogo local.

Isso já corresponde ao rumo da arquitetura atual: a AI planeja e lê batches, mas executa pela mesma máquina de estados transacional do humano. Rede, hot seat e replay devem ser apenas novos produtores/transportadores desses batches.

---

## Por que o assíncrono já tem uma fundação forte

A arquitetura atual já paga os custos difíceis, sem ter mirado em multiplayer:

1. **ReplayManager é executor, não gravador**: a AI já joga *através* dele (`ExecuteRecordedUnitActionBatch`), e o Serviço do Comando unificou preview/execução por replay pra matar divergência promessa≠execução. "Ver o automata executando as jogadas do outro" = modo AI de hoje com outra fonte de ações.
2. **Invariante transacional é ouro de rede**: só ações COMPROMETIDAS existem no log; nada provisório viaja. De brinde: anti-oráculo entre jogadores (o adversário nunca vê movimentos cancelados).
3. **Apresentação com fog por observador** já resolvida por `PlayerSlotId` — o humano assiste o turno da AI pelo próprio fog, sem misturar participantes que compartilhem o mesmo `TeamId`.
4. **Determinismo como cultura**: combate é matemática fechada (sem dado), a fila de queda por combustível RE-RESOLVE deterministicamente no replay, save/load completo com DTOs.

Não registrar uma porcentagem fechada aqui: executor e modelo transacional estão adiantados, mas protocolo, autoridade, validação cruzada, recuperação e testes entre máquinas ainda são trabalho material.

---

## As 5 peças que faltam

> **Fundação construída (jul/2026)**: `MatchStateHasher` (Assets/Scripts/Shared/SaveData) — hash SHA256 canônico do `SaveGameData` (listas ordenadas por chaves estáveis; voláteis E estado derivado excluídos — caches de fog são recomputados no load/por cliente, hasheá-los daria falso desync). O save persiste canônico, loga `state_hash=` a cada gravação e guarda o hash no manifest (`stateHash`). Comandos debug: `state hash` (hash do estado vivo) e `state dump` (JSON canônico em arquivo, pra diffar quando divergir). Limitação v1: listas do planner/intel da AI ainda não canonicalizadas.
>
> **Round-trip VALIDADO em jogo (17/jul/2026)**: load → hash A; save → hash A; load → hash A (três medições idênticas = round-trip + idempotência). Cobertura cresce conforme o ritual roda em estados mais ricos (locks, pendências, estoques).
>
> **Nota sobre o registro de estudo**: o save já acumula `matchHistory` completo (um registro por turno com `StartSnapshot` + ações, sem poda) — o "replay da partida inteira desde o round 1" está sendo GRAVADO passivamente em todo save; falta apenas a UI de navegação do viewer (ReplayManager: batch e automated player em uso pela AI; viewer completo adiado por decisão).

1. **Pacote de turno serializado e versionado**: identidade da partida, versão do protocolo, `actorSlotId`, número/ordem do turno, hash do estado-base, comandos comprometidos, hash do estado final e, na modalidade confiável, savegame completo como recuperação. *Metade do hash: pronta (acima). Falta o empacotamento do log de ações.*
2. **Validação anti-desync em duas fronteiras**:
   - antes de executar, o hash local deve coincidir com o `baseStateHash` do pacote;
   - depois de executar, o hash resultante deve coincidir com o `resultStateHash`.

   Se divergir, o pacote entra em quarentena e produz diagnóstico. Em PBEM entre amigos, o jogador pode aceitar explicitamente o savegame de recuperação. Não substituir estado silenciosamente: isso esconderia bugs e, em ambiente competitivo, abriria uma autoridade indevida para o remetente.
3. **Transporte**: v1 SEM servidor — exportar arquivo e mandar por WhatsApp/Discord (tradição PBEM; testável imediatamente). v2: relay burro (bucket + notificação). Nunca precisa de servidor com lógica de jogo (exceto anti-cheat, ver abaixo).
4. **Fluxo de chegada do turno** (o problema do "ué, cadê meu caça?") — ver seção seguinte.
5. **Caça ao não-determinismo residual: auditoria estática concluída (17/jul/2026)** — não foram encontrados RNGs relevantes no caminho competitivo conhecido (combate com arredondamento explícito, sem dados, upkeep independente por unidade, execução por log/InstanceId). Isso é uma evidência forte, mas “determinístico entre máquinas” só fica aprovado depois de testes repetidos em builds/processos distintos. Ainda devem ser observados: ordem de iteração de coleções, diferenças de versão/configuração, física, ponto flutuante e identidade de assets. Achados do grep de `Random`:
   - 4 usos **cosméticos** (seleção de música, pulso visual da bandeira ×3, screen shake) — irrelevantes para estado;
   - 1 uso de **gameplay confinado ao tutorial** (`AutomataTargetPreference.Random` — alvo aleatório do automata, deliberado para o tutorial não ser decorável). Duplamente inofensivo: escopo tutorial + o sorteio roda UMA vez na máquina de quem age e o resultado vira batch gravado por InstanceId — replay/remoto reproduzem a escolha, nunca re-sorteiam.
   - Eixo ordem-de-iteração: resoluções automáticas de início de turno são independentes por unidade (pouso de emergência é in-place, sem disputa de hex; economia é soma por slot/time conforme a regra); as que interagem (fila de queda por combustível) gravam a ordem nos substeps do replay. Ainda assim, o teste cruzado deve confirmar essa conclusão empiricamente.

---

## Fluxo de chegada: resumo primeiro, cinema depois

O problema: você loga e seu caça sumiu — "alguém abateu?". A resposta em camadas:

1. **Resumo obrigatório** ao abrir o turno: "Turno 12 do Vermelho: Caça B perdido, cidade Norte capturada, comboio atacado em (x,y)". Ninguém precisa assistir nada pra saber o que aconteceu de material.
2. **Replay opcional e pulável**: reprodução cinematográfica do turno inimigo FILTRADA PELO SEU FOG — você vê só o que suas unidades viram. É o modo AI de hoje.
3. **Detalhe fog-honesto**: unidade sua morta FORA da sua visão → o resumo não revela o assassino; diz "**contato perdido em (x,y)**". Você sabe que perdeu (a unidade é sua); o COMO é inteligência que você não coletou. Névoa funcionando na narrativa.

---

## Tempo real = mesmo log, entrega por gotejamento

Cada ação comprometida é transmitida na hora; o espectador aplica sob o fog dele. O executor e o formato lógico do comando não mudam, mas tempo real exige uma camada de protocolo adicional:

- número de sequência e idempotência;
- confirmação de recebimento;
- reconexão e retomada;
- timeout e abandono;
- eleição/fixação da autoridade;
- tratamento de mensagens atrasadas, repetidas ou fora de ordem.

Por isso a ordem continua correta: assíncrono primeiro constrói log serializado, hash e apresentação. Tempo real reaproveita essa fundação, mas não deve ser descrito apenas como troca de transporte.

---

## Autoridade e envelope mínimo

Para PBEM confiável, o jogador que possui o turno é autoridade temporária para **propor** a sequência daquele turno. O receptor nunca aceita resultados arbitrários: ele valida o estado-base e reexecuta as intenções localmente.

Envelope mínimo sugerido:

```text
protocolVersion
matchId
scene/map content hash
turnNumber
actorSlotId
sequenceNumber
baseStateHash
committedCommands[]
resultStateHash
optionalRecoverySave
```

Cada comando também precisa de identidade/idempotência estável. Reimportar o mesmo pacote não pode executar o turno duas vezes.

---

## Aviso de segurança (registrado, não resolvido)

No modelo par-a-par, o pacote pode carregar o estado completo e o log integral de comandos — INCLUINDO unidades e movimentos que o observador não deveria conhecer. Filtrar apenas a reprodução visual não impede um jogador técnico de abrir o arquivo e "furar" o fog.

- **Entre amigos**: irrelevante; v1 assume confiança e documenta.
- **Ranqueado**: exigiria servidor autoritativo que guarda o estado e entrega a cada jogador só o que o fog do seu `PlayerSlotId` conhece. A separação LÓGICA já existe no FoW (conhecimento por slot); virar separação FÍSICA é projeto grande. Não deixar esse fantasma atrasar o resto.

---

## Próximos passos SEM impacto no jogo principal (prateleira)

Todos aditivos — código novo ou atrás de comando de debug, zero mudança em fluxo de gameplay:

1. **Canonicalizar as listas da AI no hasher** (~meia hora, arquivo único) — remove a limitação v1; pré-requisito para hash cruzar máquinas.
2. **Definir o DTO canônico de comando** — separar intenção de resultado e incluir `commandId`, `actorSlotId`, unidade, ação, destino/alvo e parâmetros estritamente necessários.
3. **`export turn` (debug)** — empacotar comandos comprometidos + hashes de base/resultado + savegame opcional de segurança num `.tmrturn` versionado. Só lê dados existentes.
4. **`import turn` dry-run (debug)** — abre o pacote, valida protocolo, partida, mapa, slot, sequência e hash-base, SEM aplicar. Demo completa da primeira fronteira do detector de desync.
5. **Teste cruzado de determinismo** — exportar em um processo/build, aplicar em outro e comparar `resultStateHash`; repetir com movimento, combate, captura, transporte, supply e queda por combustível.
6. **Gerador de resumo de turno** — função pura sobre os registros do turno ("perdeu Caça B, cidade X capturada") + comando debug; vira o painel de chegada depois, e serve pro modo estudo e log local desde já.

**Primeiro passo que TOCA o jogo** (fica para quando decidido): aplicar o pacote pelo automated player — executa ações "estrangeiras" no tabuleiro real; mesmo atrás de debug merece cautela.

## Roadmap sugerido

1. Esquema canônico de intenção + pacote de turno + hashes de base/resultado
2. Aplicação do pacote como "modo AI" (engenharia de verdade)
3. Teste cruzado de determinismo e quarentena de desync
4. Resumo de chegada (gerador da prateleira prepara o dado; falta a UI)
5. Replay pulável do turno inimigo (UI sobre o que existe; dados já gravados — matchHistory completo)
6. Relay de transporte (infra, sem lógica de jogo)
7. (depois) Tempo real por streaming ordenado e idempotente de ações
8. (um dia, se ranqueado) Servidor autoritativo com fog físico

**Determinismo (pré-requisito transversal): auditoria estática favorável; validação cruzada ainda pendente** — ver peça 5 acima.
