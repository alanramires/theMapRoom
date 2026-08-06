# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-06, **depois** da tag `v8.0.0`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## Estado

`v8.0.0` tagueada e publicada na `main`. Relatório:
[`docs/relatorio_v8.0.0.md`](relatorio_v8.0.0.md). Sete commits, seis frentes.

O major `v7` fechou; seus dez relatórios foram arquivados em
[`docs/Versões/`](Vers%C3%B5es/).

### A frase que organiza esta versão

> **A ausência precisa de nome próprio.**

Todo defeito do dia teve a mesma forma — falta um nome para "nada aqui", e o
vizinho mais próximo assume a vaga:

```text
default(ConstructionSector)     "nenhum setor"   respondia ALPHA
rota de outro catálogo          "não se aplica"  saía como ERROR
bucket de rota vazio            "não migrei"     igual a "não tem estrada aqui"
PodeSuprir valid=[] invalid=[]  "não se aplica"  igual a "não posso"
StructureData.roadRoutes        "onde está"      morando dentro de "o que é"
```

É o **gate inaplicável** da `v7.2.1`, e a descoberta é que ele não era do
transporte: é do jogo inteiro.

### O major novo

`v8 — onde o dado mora`. O catálogo diz o que uma coisa **É**; a cena diz **onde
ela ESTÁ**. A doutrina dos três andares está no `CLAUDE.md`, com o teste de
aceitação em um gesto: **duplicar cena, apontar os catálogos, e o mapa novo
nascer vazio.**

---

## Onde eu parei

### Rotas — migrado, origem ainda não limpa

Todos os mapas carregam `routesMigratedToScene`. O treino fecha em **0 erros, 0
warnings** (eram 289).

**O que falta**, e agora está destravado porque todo mapa já migrou:

```text
StructureDatabase.roadRoutesByStructure    apagar a seção (Map Scope)
StructureData.roadRoutes                   apagar o fallback legado
RoadRouteDefinition.ownerDatabase          apagar — só existe para desambiguar
                                           catálogo, e com rota na cena não
                                           sobra o que desambiguar
```

Ordem obrigatória: `ownerDatabase` só sai **depois** da limpeza, não antes.

### `fieldEntries` — a mesma doença, e barata

`ConstructionDatabase` carrega uma seção `Field Entries (Map Scope)` com o layout
de construções do mapa. **Zero leitores de runtime** — só
`ConstructionDatabaseEditor` e `ConstructionPainterWindow`. O jogo instancia
construção pela cena; `ConstructionSpawner` faz `TryGetById`, simétrico ao
`UnitSpawner`.

É autoria no asset errado. O autor quer a separação em `constructionField` /
`constructionData` / `constructionDatabase`. Os catálogos `Hexagono` já existem
como cópia e são o começo disso.

### Capturador — o teste nunca rodou, e ele decide o plano

**O achado que inverte tudo:** metade da alocação pegajosa **já existe**.

```text
UnitManager.cs:169-171       aiHasDesignatedCaptureTarget + Id + Cell  [SerializeField]
SaveDataDtos.cs:306-309      no DTO
SaveDataMapper.cs:241-246    gravado e restaurado         ← ATRAVESSA O SAVE
AIController.Rebel.cs:281    pendingRebelCaptureTargets + Commit
AIController.Rebel.cs:223    as cinco condições de baixa
```

Só no caminho **rebelde**. `AIPlanRuntimeIntent.Capture` — verbo nº 1 do enum —
tem **zero ocorrências no código**.

**O teste, em dois F11**, no `Hot Seat 0 - Treino`:

```text
turno 1   soldado sai de (7,0) para (4,0), DesignatedCaptureTarget #2 confirmado
salvar → FECHAR o jogo → abrir → carregar
turno 2   MESMA missão, MESMO alvo, sem releilão
F11 e cancelar   →  NENHUMA missão fantasma
```

Se passar, o trabalho é **promover** (levar o mecanismo do rebelde para a camada
compartilhada), não construir. Contrato completo com as condições de baixa em
[`docs/AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md).

**Atenção ao roteador:** a decisão rebelde sai de `TryDecideRebelAction`
(`AIController.Router.cs:107`), **antes** do bloco `plan != null`. Mudança escrita
só no arquivo do capturador não é exercitada por tabuleiro rebelde. A
pegajosidade vai no `CaptureOpportunityClaimService` (compartilhado) e o commit
já é centralizado em `AIController.Phase2.cs:299`.

**Segundo tabuleiro, ainda por montar:** 2 capturadores e 2 cidades simétricos —
é a configuração que faz o otimizador global trocar de alvo entre turnos. Se
parar de trocar, o `−15` de aderência pode ser aposentado.

### Vigilância — contrato escrito, nada em código

[`docs/AI Behavior/contrato_recencia_de_cobertura.md`](AI%20Behavior/contrato_recencia_de_cobertura.md),
nascido de um censo de nove unidades no turno 1. O diagnóstico cabe em duas
linhas da mesma classe de navio:

```text
Fragata #79   hold   vis=58  marginal=38  novo=0   →  gain 1,9
Fragata #84   move 5 vis=46  overlap=39   novo=7   →  gain 137,5
```

`unexploredMarginalWeight: 25f` responde por ~98% do score. A moeda é **névoa**,
não contato — e névoa não regenera: com o mapa explorado, `novo → 0` para todos
os caçadores ao mesmo tempo e todos congelam no estado da #79.

**Aberto:** o valor de N em *"nunca coberta vale N vezes o teto da idade"*. Esse
número **é** a doutrina — teto baixo faz o sensor pastar perto de casa;
nunca-coberta alto demais faz ele furar para a fronteira e nunca voltar.

**Correção importante do que o resumo anterior dizia:** *"a moeda do MelhorVisão
está errada"* vale para o ramo **`IsAll`**, não para o ramo por camada. O ramo
por camada usa `forceVirtualTargetLayer` e **já pergunta detecção**. O defeito
está na **política** (termos de névoa aplicados a rede de detecção), não no
conjunto.

---

## A dívida de validação em jogo — o que sobrou

Da lista da `v7.2.1`, **quitados**: compilar (4 e 10), nenhum `[FilaCarona]`
citando caça (8), a linha do Chinook #85 (11) e os números do #86 (13).

Continua sem partida:

```text
1. as duas janelas lado a lado         o teste do relatorio unificado
2. radar movido duas vezes             o delta do som: toca na primeira, cala na segunda
3. aeronave em voo -> fow off          deve RECUSAR; depois, em Neutral, nevoaTiles ~1700
5. turno com 2+ cidades vazias         as duas tem que aparecer no Jornal
6. hot seat DEMORANDO na cortina       o Jornal abre inteiro no instante em que a tela abre
7. menu > resumo do turno              o botao movido tem que abrir o Jornal
9. linha [AI Perf][Unit] do APC #31    nunca chegou
12. Suprimentos #24 e #73 buscando     a lancadeira de reboque nao pode ter morrido
    artilharia                         com o descarte do inaplicavel
```

`nevoaTiles = 0` significa tabuleiro aberto, independentemente do que o cálculo
disser. **Protocolo de teste de névoa:** com a partida em Play, ninguém salva
`.cs` — recompile religa `debugFogOfWarEnabled = true` e parece conserto.

---

## A fila

```text
1. limpar a origem das rotas              destravado; ownerDatabase só depois
2. teste do capturador (2 F11)            decide construir vs promover
3. fieldEntries sai do ConstructionDatabase  zero leitor de runtime, barato
4. jornal para contato nao-furtivo        gancho já existe (v7.2.0)
5. exclamacao no contato novo             + foco pela linha; NAO pan automatico
6. portao de nevoa no PodePousar          PodeDesembarcar JA TEM (Disembark.cs:666)
7. tirar o laco de hex do PodeDetectar    CollectVisibleCells ainda e publico
8. servico de cobertura de DETECCAO       o buraco que os tres Melhor* precisam
9. fow off pausa cook/bake/snapshot       spec do autor, hoje nao existe
10. apagar residuo de exploracao nos saves
```

**Managers globais — frente própria, não bloqueia nada.** Seis já são
`DontDestroyOnLoad` (`ObjectiveManager`, `AIShoppingPlanner`, `AITacticalAnalyzer`,
`MatchStatsManager`, `PanelVisibilityHotkeys`, `HexCohabitationVisual`); cinco são
singleton por cena (`SectorManager`, `AIController`, `AnimationManager`,
`JogadasManager`, `DialogManager`). Duplicar cena funciona com manager por cena —
o que custa é refiar.

**A suspeita inversa:** `ObjectiveManager` é `DontDestroyOnLoad` e guarda plano
por slot, mas **não tem gancho de `sceneLoaded`**. `ClearPlanForSlot` e
`plans.Clear()` existem e são explícitos. Falta conferir **quem chama** — se
ninguém, o plano do mapa A chega no mapa B. Global demais também é contaminação.

---

## Pendências abertas

**`PodeDetectar` ainda responde por hexágono.** `CollectVisibleCells` continua
público, com quatro consumidores vivos, um deles com
`preserveObserverLayerRangeForHexVisibility: true` — uma das três portas que a
`v7.1.0` fechou. `MelhorVisaoService` e `AIController.Vigilancia` leem dali.

**`[FoW][RoundZeroBake] restored=1/2`.** Um slot segue rejeitado e o motivo está
calado: `enableFogValidationLogs` está desligado e a linha `rejected=<motivo>`
existe (`MatchController.cs:7093-7098`).

**O `fow off` não pausa nada.** `RefreshFogOfWarForCurrentTeamInternal` checa
`SuppressFogOfWarRefresh` e `enableTotalWar`, **não** `debugFogOfWarEnabled`.

**`MovementQueryCachesBuilt` continua alto no primeiro consumidor do turno.** 747
no Chinook #86, 395 num soldado sozinho num tabuleiro de 153 células — uma
construção por célula expandida no `CalculateTurnChainedCostMap`, cada uma
indexando todas as unidades confirmadas. O tabuleiro mínimo tornou isso
reproduzível num turno de 10 segundos.

**O terceiro passo do contrato da carona não foi dado.** O modelo do autor —
passageiro levanta a mão, transportador decide, mão não endereçada — está certo e
não deve ser acoplado. O que falta é publicar intenção: se a captura virar missão,
o `QueroCarona` passa a perguntar *"minha missão é aquele prédio; chego sozinho?"*
em vez de *"existe capturável livre no meu alcance?"*. **Os dois consertos são o
mesmo conserto.**

**`OCUPAÇÃO INIMIGA` cobre prédio que É seu, não que ERA.** Exige guardar o dono
anterior; hoje `previousOwnerSlot` só existe como local
(`TurnStateManager.Capture.cs:153`).

**`MelhorCapitao` continua sem consumidor.** **`roles[0] == CapturadorAgressivo`
continua no `GetCapturePower`.** **Melhor Combate e Melhor Captura não governam a
IA.** **A Vigilância da `v7.0.3` continua sem validação registrada.**

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅ HexGridGeometry, ObservationCellService,
                                    ObservationLineService
 0. sensores PodeX                ⚠️ PodeEnxergar e PodeDetectar separados, mas o
                                    laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta o de cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ falta Detecção e Spotting; o contrato de
                                    recência está escrito e sem código
 3. papéis → só POLÍTICA          docs/revisao_papeis.md — 1 linha de 7 levantada
 4. variações de papel            vira perfil/trait depois da extração
```

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | [`relatorio_v8.0.0.md`](relatorio_v8.0.0.md) | o fio mais recente; a seção 6 (onde eu errei) e a 7 (o que não terminou) |
| 2 | `CLAUDE.md`, seção "Subir pra cima" | os três andares e o teste de duplicar cena |
| 3 | [`AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md) | a §0 diz o que já existe; as baixas são a especificação |
| 4 | [`AI Behavior/contrato_recencia_de_cobertura.md`](AI%20Behavior/contrato_recencia_de_cobertura.md) | ledger de idade, escada da vigilância, as duas famílias |
| 5 | [`Versões/relatorio_v7.2.1.md`](Vers%C3%B5es/relatorio_v7.2.1.md) | *"a pergunta errada também responde"* — a origem do gate inaplicável |
| 6 | [`Versões/relatorio_v7.1.0.md`](Vers%C3%B5es/relatorio_v7.1.0.md) | a separação enxergar/detectar, e a seção 9 |
| 7 | `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| 8 | `docs/arquitetura/acoes_transacionais.md` | obrigatório antes de ligar ferramenta a runtime |

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** E **checar um leitor não prova onde o dado
  mora** — pode haver um segundo armazenamento ao lado.
- **Ler `docs/manual/` antes de decidir onde uma regra mora.**
- **Nada provisório publica verdade confirmada — e apagar é publicar.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não classificar arquivo do autor como churn sem perguntar.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- **Nada foi distribuído** — save e bake podem mudar de forma. Não propor shim.
- **Um commit por frente de trabalho**, não um pelo lote.
- **Número de build só entra em relatório se veio de build COM restore.**
  `dotnet build Assembly-CSharp.csproj -v q --nologo` — o Editor é outro assembly
  (`Assembly-CSharp-Editor.csproj`). Arquivo `.cs` novo não entra no `.csproj`
  até a Unity regerar; para compilar antes, adicione `<Compile Include>` à mão.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **`default` de enum não é "nenhum"** | `default(ConstructionSector)` é **Alpha**, porque Alpha vale 0 e None vale −1. `!= default` como "tem vizinho" apagou Alpha do grafo de setores. Escreva `None` explícito, sempre |
| **`enumValueIndex` não é o índice de `Enum.GetValues`** | o popup mostrava o rótulo escolhido e a cena gravava o setor vizinho; ao reler pelo mesmo índice torto, o círculo fechava e o inspector não dava um pio. Um QG marcado "Base0" virou Omega. O contrato de serialização é o **valor** — `intValue` |
| **checar um leitor não prova onde o dado mora** | afirmei que o `ConstructionDatabase` estava limpo porque o builder de topologia itera instâncias da cena. Havia uma seção `Field Entries (Map Scope)` que eu não procurei. Um segundo armazenamento pode existir ao lado do primeiro |
| **layout de mapa em asset de catálogo** | o modo de falha é **silêncio**: só grita quando as coordenadas não existem no outro tabuleiro. Dois mapas com faixas parecidas se contaminam sem um erro. "Sem erro" não é prova |
| **lookup que mistura fontes devolve lista temporária** | `GetOrCreateRoadRoutes` lia o lookup, que antes da migração vem preenchido com listas montadas a partir do catálogo. A escrita caiu num objeto que ninguém serializa: 23 rotas copiadas para lugar nenhum. Escrita procura o **bucket serializado**, nunca o lookup |
| **migração que não é idempotente** | rodar de novo empilhou: 23 → 46. Migração **substitui**, e confere o próprio resultado — comparar o copiado com o que a cena passou a enxergar teria pego as duas falhas opostas |
| **auto-assign por "o primeiro que aparecer"** | `FindObjectsSortMode.None` é ordem arbitrária. Cena com dois tilemaps hexagonais adotava o errado em silêncio. Desempate explícito, e **avisar** quando não há critério |
| **campo global usado como decisão local** | ia passar `EnableLos = false` na requisição da vigilância; aquele campo é o **toggle da partida** e teria desligado a LoS do radar junto. A ficha já tinha `DetectionMethod.Propagated` — "LoS é fallback" já estava implementado por par (domínio, altura) |
| **memória onde o fato é observável** | propus lembrar a missão do EWACS espelhando o Fire Support. O Fire Support lembra porque o passageiro embarcado não se vê da posição; contato na rede se vê toda rodada. Sem estado, não existe sensor trancado perseguindo fantasma |
| **política única para famílias opostas** | ia penalizar `overlap` em toda vigilância. Aérea repele; naval não — sonar sobreposto entre subs que navegam juntos é legítimo. Bifurcar por **família** antes de por postura |
| **confundimento que faz o teste passar pelo motivo errado** | hoje "é aérea?" e "tem `playConservative`?" dão a mesma resposta para toda unidade de vigilância. Política construída sobre o flag passaria sem provar nada |
| **onde eu ponho o teste erra mais que o que o teste faz** | duas vezes na mesma sessão pus a consulta cara antes do filtro barato. Neste subsistema o defeito quase nunca é a regra: é a **posição** dela no fluxo |
| **hash que se invalida pela própria contabilidade** | o `captureClaimStateHash` dobrava o `HasActed` das 66 unidades do slot. A prova estava num número: o segundo transportador acertava o cache 1 vez em 17 |
| **hit e miss de cache logam igual** | o QueroCarona faz `diagnosticLog(hit.reason)` no acerto. Não dá para auditar cache pelo texto; só pelo contador |
| **verdade vazia em laço de prova** | `for (...) if (achou) return;` seguido de "então não achei" conclui o pior quando a lista está **vazia** |
| **atribuir custo dentro do laço por dedução** | apostei na interpolação de string; o contador mostrou que era a sonda irmã. Instrumentar é mais barato que a aposta errada |
| **doc que envelhece e vira fato** | `skipLosForCurrentTarget` estava na fila e não existe no código há versões. E *"a moeda do MelhorVisão está errada"* valia só para um dos dois ramos |
| **testar só o caso especial** | o som generalizado funcionava desde a `v7.1.0` e passou despercebido porque todo teste usou furtivo |
| **premissa que funcionava por acidente** | o delta do FOW filtrava unidades por célula com revelação alterada. Separar revelar de detectar quebrou o delta sem tocar nele |
| **consertar metade e achar que acabou** | o mesmo filtro existia em `RefreshRuntimeUnitFogVisibilityForCells` **e** em `PublishFogGameplaySnapshot` |
| **recompile em Play parece conserto** | salvar `.cs` religa `debugFogOfWarEnabled = true`. Teste de névoa feito enquanto alguém edita é lixo |
| **`skipSpecializedTargetLayers`** | não ignora alcance: **descarta a célula** cuja camada tenha Detect Specialization. Foi ela que apagou o mar do submarino |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar e atirar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; missão obrigatória precisa de admissibilidade explícita — e trela conservadora também |
| **limiar fixo onde devia ser banda** | `minimumMissionGain: 2f` para um caça de 9 MP |
| **dividir commit por hunk sem rede** | guarde o arquivo final, aplique só a frente A, **restaure o arquivo inteiro**, e o resto é a frente B. `cmp` contra o backup prova que nada corrompeu |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |

---

## Critério de retomada

**Dois F11 e um save/fecha/abre.** O teste do capturador é a coisa mais barata da
fila e a que decide o formato do próximo trabalho: se o
`DesignatedCaptureTarget` atravessar o reload, a alocação pegajosa é **promoção**
e não construção, e o `QueroCarona` ganha o objeto certo para perguntar contra.

Depois disso, a limpeza da origem das rotas — está destravada e é a única coisa
que impede de dizer que o degrau das rotas fechou.

E o critério do major continua sendo um gesto: **duplicar uma cena, apontar os
catálogos, e o mapa novo nascer vazio.** Hoje isso vale para estrada. Ainda não
vale para construção.
