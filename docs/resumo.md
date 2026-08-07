# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-06, **depois** da tag `v8.0.1`. Leia isto
primeiro.

---

## Estado

`v8.0.1` tagueada e publicada. Relatório:
[`relatorio_v8.0.1.md`](relatorio_v8.0.1.md).

**A versão é inteiramente doutrina — zero linhas de C#.** O autor resumiu sem
cerimônia: *"agente mais bateu papo e ficou planejando"*.

O que se planejou é o pré-requisito do **degrau 3 inteiro**, que estava em `❌`
**sem vocabulário**: não havia como descrever o que um papel é, nem como decidir
se uma unidade pertence a ele. Agora há.

```text
6 papeis        ficha: questionario, moeda, posicionamento
6 marchas       o conjunto esta COMPLETO
3 modalidades   combatente, artilheiro, hibrida
17 rotulos      o que o shopping pede e a ficha declara
10 sensores     as mesmas dez perguntas para todos
 7 ordenacoes   o Transportador precisou de duas: Pickup e Courier
```

### As duas descobertas que organizam o que vem

**1. O papel é DERIVÁVEL, não declarado.** O critério é do autor e já existe como
predicado:

```csharp
// UnitData.cs:612 — quem passa nisto é Vigilância; quem não passa, não é
HasStealthDetectionFor(domain, height)
    => TryGetVisionException(...) && entry.detectUnitsWithFollowingSkills.Count > 0;
```

Não é *"tem exceção de visão"* — o F-22 tem, e enxerga bem. É *"a exceção carrega
**lista de detecção**"*. Carregar `AR Stealth` é ser **fechadura**; listar
`AR Stealth` é ser **chave**. Mesmo formato de *facção sem QG*, derivada de **não
possuir** `isPlayerHeadQuarter`.

**2. O que comprimiu não foram os papéis — foi o questionário ser fixo.** Se cada
papel tivesse a sua lista de perguntas, não haveria economia: seria o mesmo caos
com nomes bonitos. O papel responde a **ordem**; a ficha responde a
**capacidade**. E `6` só fechou porque o **Artilheiro Combatente não coube** e
forçou a modalidade híbrida a existir.

> Quando aparecer a próxima unidade que não encaixa, a pergunta certa é
> **"que eixo falta?"**, não *"que papel falta?"*.

### A heurística de busca da v8.0.0 continua valendo

> **A peça certa aparece encostada na errada.** Antes de escrever a peça nova,
> procurar a que já faz isso para outro dono.

Oito casos estão listados no [`relatorio_v8.0.0.md`](relatorio_v8.0.0.md). O
`HasStealthDetectionFor` de hoje é o nono.

---

## A FATIA 1 PASSOU — 2026-08-06, depois da v8.0.1

O commit `3e0565d` atravessou duas versões compilado e sem rodar. **Rodou, e
passou.** `Hot Seat 0 - Treino`, com `Reload Domain and Scene` ligado:

```text
T1   origemAlvo=servico   envelope=BeyondOperational   QueroCarona=SIM   (7,0)→(4,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (adquirida)
────────────── salvar · Stop · Play · carregar ──────────────
T2   origemAlvo=reserva   envelope=Operational  custo=4  QueroCarona=NAO  (4,0)→(1,0)
     [Missao] 1 Capture -> (0,0,0) predio=#2 (mantida)
     [FilaCarona] #1 sai da fila apos 1 turno(s)
```

Bate **linha por linha** com o traço pré-fatia do relatório da v8.0.0. A fatia é
subtração, então **igualdade É o resultado correto**: os três campos
`aiDesignatedCaptureTarget*` sumiram, saíram do DTO, e `AIPlanRuntimeIntent.Capture`
passou a ter escritor **e** leitor que atravessa o save.

**Onde a missão é escrita — e por que isso está certo:** depois do commit da
ação, nunca na decisão. É o invariante transacional. Não adianta conferir o
Inspector antes de a unidade agir no turno 1: não há missão ainda.

### O ciclo completo T1→T6 — corrido em 2026-08-06, log em `docs/gamelog/log.md`

| turno | dist | banda | QueroCarona | `MelhorCapturaCalls` | `decision` | estágios |
|---|---|---|---|---|---|---|
| T1 | 7h | BeyondOperational | **SIM** | **3** | 136ms | 8 |
| T2 | 4h | Operational | NAO | 1 | 25ms | 7 |
| T3 | 1h | *pulado* | — | 0 | 7ms | 1 |
| T4 | 0h | *pulado* | — | **0** | **1ms** | **0** |
| T5 | 7h | BeyondOperational | **SIM** | **3** | 84ms | 8 |
| T6 | 5h | Operational | NAO | 1 | 24ms | 7 |

**A missão morre limpa** — era a única incógnita do teste:

```text
T1 predio=#2 (adquirida)  ·  T2 (mantida)  ·  T4 capturado
T5 predio=#1 (ADQUIRIDA — nova, sozinha)  ·  T6 (mantida)
```

Sem resíduo e sem baixa forçada. O `[SemPlano]` reancorou no HQ inimigo quando o
serviço não achou mais capturável em banda.

**O T3 pula o `QueroCarona` — e o skip já existia.** Não no serviço: em
`TryDecideCapturerAction`, via `[Oportunista] captura local ... antes de embarcar`,
que retorna **antes** do gate de embarque. ⚠️ Eu procurei no `QueroCaronaService`,
não achei early-out e concluí "não existe" — **camada errada**.

**O T4 é o chão absoluto:** `stages=- metrics=-`. Em cima do próprio alvo, a IA
não consulta o tabuleiro uma única vez.

### Fatia 2 — o alvo agora está MEDIDO

```text
MelhorCapturaCalls   3 · 1 · 0 · 0 · 3 · 1
                     ↑           ↑
                     descobre    descobre
```

**3 quando descobre o alvo, 1 quando lembra dele.** A missão já corta 3→1 nos
turnos seguintes; a fatia 2 é levar o **turno de aquisição** de 3 para 1 também.
Turno mais caro da partida: T5, `routeDistance:50,9ms/39`,
`MovementQueryCachesBuilt:940`.

⚠️ **`ms` entre corridas não vale sem a contagem ao lado.** O mesmo T2 mediu
125ms logo após um load e 25ms em corrida seca — **contagens idênticas** (`/39`,
`/5`, `/1`). Era JIT, não lógica. As contagens não mentem; o relógio mente.

### Fatia 2 — medir antes de escrever

Ela ia inverter o táxi (missão no topo, carona medida contra ela). **Parte disso
já funciona no caminho rebelde** — o log do T2 mostra a reserva alimentando a
recusa de carona:

> *"alcança alvo reservado Cidade@(0,0,0) no Operational: custo=4 no turno 2 de
> 2. **Recusa carona**."*

Levantar o que sobra da fatia 2 antes de abrir editor.

---

## A voz dos papéis — o método que apareceu hoje

O capturador tinha ~20 exceções que pareciam arbitrárias. Elas ficaram legíveis no
instante em que o **lema** apareceu:

> **O capturador adianta a renda do exército.**
> **Nenhum prédio é dele, e o HP é o relógio.**
>
> *"É a mosca atraída pela luz roxa. Ele não consegue evitar."*

E o teste que ele produz, que hoje é critério de aceitação de regra nova:

> **Esta exceção adianta renda, ou existe porque a peça se achou dona?**
> As que adiantam renda viram **termo do score**. As que existem por posse
> **dissolvem**. O que sobrar é **gosto** — e só isso vira política.

**As seis vozes estão escritas.** Cada papel tem lema, ficha e marcha:

| papel | a moeda — *onde mora o valor da peça* | funde |
|---|---|---|
| Capturador | o **corpo** — HP **é** a taxa | **ganha** |
| Transportador | as **vagas** | perde |
| Assalto | a **arma** — cada casco é ameaça | perde |
| Fogo de Suporte | a **formação** — cones cruzados | perde, e agrupar também |
| Vigilância | a **origem do cone** | perde |
| Logística | o **estoque** — média ponderada conserva | **ganha** |

> **Cada papel tem uma moeda, e a moeda decide sozinha se fundir é ganho ou
> perda.** Seis papéis, seis acertos — inclusive nas duas vezes em que a resposta
> contraria a intuição de HP.

### Onde mora o quê — a divisão que apareceu ao errar

```text
marcha   o INVARIANTE — o que os ramos compartilham
ficha    o PARAMETRO  — 1 rodada / 2 rodadas + emerge
```

**As marchas envelhecem melhor que as seções** porque foram escritas no nível
certo. Se um verso parecer contradizer uma seção, **testar primeiro se o verso
está um nível acima dela** — errei isso duas vezes seguidas na mesma tarde.

---

## Onde eu parei — os documentos

| documento | o que é |
|---|---|
| [`AI Behavior/ficha_do_papel.md`](AI%20Behavior/ficha_do_papel.md) | a matriz `Pode*` → `Melhor*` **pareada pelo autor**, o questionário padrão, `RoleData` como dado |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | §0 o lema; §1 e §3 revistos; apêndice com a **Marcha do Capturador** |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | §0.1 a ficha; §7 aninhamento; §12 a moeda; §13 limiar de reparo; §15.1 postura ❓ |
| `Match/AI/3. Shopping/Shopping.md` | **novo** — o buraco elegibilidade × preferência, e os três papéis-fantasma |
| [`AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md) | alocação pegajosa e as condições de baixa |
| [`AI Behavior/contrato_recencia_de_cobertura.md`](AI%20Behavior/contrato_recencia_de_cobertura.md) | ledger de idade da vigilância |
| [`AI Behavior/Assalto.md`](AI%20Behavior/Assalto.md) | a ficha; §5.1 **novo** — furtividade aérea, 1 rodada, e por que o custo é de outra natureza que o do sub |
| [`AI Behavior/FireSupport.md`](AI%20Behavior/FireSupport.md) | a ficha; a modalidade **híbrida**; o auto-repelir como consequência da moeda |
| [`AI Behavior/Vigilancia.md`](AI%20Behavior/Vigilancia.md) | **novo** — §0 o teste de pertencimento; §2.1 detecção total; §5.1 o preço do tiro submarino |
| [`AI Behavior/Logistica.md`](AI%20Behavior/Logistica.md) | **novo** — o espelho da Vigilância; §5.1 a triagem que lê a moeda de quem pede |
| `Units/Capturer/Capturer.md` | o código: a ordem real, os seis mecanismos de ceder, o inventário |
| `docs/AI Behavior/rascunho/` | **a fonte** — o que o autor escreveu antes das fichas. Quando uma ficha divergir, confere-se aqui |

**Dívida declarada:** o `Capturer.md` é quatro documentos grampeados. A fronteira
com a doutrina está escrita, mas a ordem interna não ajuda quem lê do começo.

---

## MUDA REGRA — a lista de divergências doc × código

**É o trabalho concreto que sobrou.** Cada uma tem doc dizendo uma coisa e código
fazendo outra, e a regra dos docs de doutrina é *"onde o código divergir, o código
está errado"*.

```text
ajuda entre eixos          IsOtherAssignedCapturerTarget (Capturer.cs:52) barra
                           alvo alheio INCONDICIONALMENTE. A doutrina condiciona
                           à banda: se o dono está no Operacional, outro ajuda

swap por cap power         FindSwapIncomingCapturer compara HP CRU. Funciona hoje
                           porque GetCapturePower devolve HP — QUEBRA no dia em
                           que a chave de eficiência entrar (ideias_futuras §10)

capturador em Collapsing   Demand.cs:3092 dá +16000 a Assalto/Fogo/AA e NEGA ao
                           capturador, "porque é expansão". A doutrina diz que ele
                           defende a LINHA DE RENDA — corpo em prédio conquistado
                           é a defesa mais barata que existe

MelhorVisao (ramo IsAll)   a matriz diz "revelação pura de hexágonos"; o ramo
                           IsAll responde por detecção
                           (contrato_recencia_de_cobertura §4.2)

imposto de conscrição      só ConscriptionDoctrine liga. A política do autor pede
                           macroLosing como gatilho também (Shopping.md §6)
```

---

## Buracos estruturais

**Quatro `Melhor*` faltam:** `Suprir` (criticidade, peso por elite, manutenção
preventiva), `Fundir` (fundir na retaguarda — hoje dentro do `AIRepair`),
`MelhorDeteccao` e `MelhorSpotting`.

**Duas casas do questionário do capturador estão vazias no runtime:** `Detectar` e
`Enxergar` correspondem a `RevelacaoDeContato` e `RevelacaoTerritorial`, que o
`contrato_missoes.md` marca como brainstorming.

**Três papéis-fantasma no enum**, que existem para o shopping conseguir comprar:
`CapturadorCombatente` (12), `ArtilheiroCombatente` (13), `AntiaereoCombatente`
(14). Roteiro seguro de remoção em `Shopping.md` §3.1 — e `UnitData.roles` **não é
persistido no save**, então o risco é asset e cena, não arquivo de partida.

**Rotas: falta limpar a origem.** Todos os mapas migraram (`routesMigratedToScene`
ligado); falta apagar a seção do `StructureDatabase`, o `StructureData.roadRoutes`
e o `RoadRouteDefinition.ownerDatabase` — nessa ordem, `ownerDatabase` por último.

**`fieldEntries` do `ConstructionDatabase`:** mesma doença, **zero leitores de
runtime**. Autoria no asset errado.

**`ObjectiveManager` é `DontDestroyOnLoad` sem gancho de `sceneLoaded`.** Falta
conferir quem chama `ClearPlanForSlot` — se ninguém, o plano do mapa A chega no
mapa B.

**`[FoW][RoundZeroBake] restored=1/2`.** Um slot rejeitado, motivo calado:
`enableFogValidationLogs` desligado, e a linha `rejected=<motivo>` existe
(`MatchController.cs:7093`).

---

## A dívida de validação em jogo

Da lista da `v7.2.1`, continuam sem partida:

```text
1. as duas janelas lado a lado         o teste do relatorio unificado
2. radar movido duas vezes             delta do som: toca na primeira, cala na segunda
3. aeronave em voo -> fow off          deve RECUSAR; depois nevoaTiles ~1700
5. turno com 2+ cidades vazias         as duas no Jornal
6. hot seat DEMORANDO na cortina       o Jornal abre inteiro
7. menu > resumo do turno              o botao movido abre o Jornal
9. linha [AI Perf][Unit] do APC #31    nunca chegou
12. Suprimentos #24 e #73 buscando artilharia
```

**Protocolo de névoa:** com a partida em Play, ninguém salva `.cs` — recompile
religa `debugFogOfWarEnabled = true` e parece conserto.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta o de cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam quatro (§ Buracos estruturais)
 3. papéis → só POLÍTICA          ⚠️ as SEIS fichas escritas; RoleData não existe
 4. variações de papel            perfil/trait depois da extração
```

O degrau 3 tem **vocabulário completo e zero código**. As seis fichas descrevem a
forma; `RoleData`/`RoleDatabase` (o `ScriptableObject` que o autor desenhou) não
existe, e nenhuma das ~20 exceções do capturador foi re-derivada ainda.

**O teste do degrau 3** — e ele ainda não pode ser feito:

> Cada exceção nomeada (*ponta de lança, handover, sai do meu prédio, ceder para
> o capturador x*) ou se re-deriva de `(papel, modalidade, banda, âncora)`, ou
> vira **política declarada** em `Match/AI/Service/Capture_Policy`. O que não for
> nem uma nem outra é resíduo.

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** E **checar um leitor não prova onde o dado
  mora**; e **listar arquivo por nome pareia errado** — o que decide é a pergunta
  que o consumidor responde, e ela está na docstring.
- **Doutrina em `docs/AI Behavior/`; comportamento do código ao lado do código.**
- **Verso não é lugar de hipótese** — a Marcha vale como especificação.
- **Nada provisório publica verdade confirmada — e apagar é publicar.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não classificar arquivo do autor como churn sem perguntar.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- **Nada foi distribuído** — save e bake podem mudar de forma. Não propor shim.
- **Um commit por frente de trabalho.**
- `dotnet build Assembly-CSharp.csproj -v q --nologo` — o Editor é outro assembly.
  Arquivo `.cs` novo não entra no `.csproj` até a Unity regerar.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **conferir coerência não é conferir correção** | carimbei um verso da Marcha contra `Vigilancia.md` §5 — e a §5 era justamente a cláusula que estava no **documento errado**. Os dois erros se cancelaram num ✅. Quando a referência está torta, bater com ela é **sintoma**, não prova. O ✅ só vale se o doc de referência também já foi conferido |
| **descompasso de generalidade É a evidência** | o verso dizia *"**SE** eu sou furtivo"* (dois ramos); a cláusula dizia *"unidades furtivas **AÉREAS**"* (um). Estreitei o verso **duas vezes seguidas** para caber. Quando o texto novo cobre **mais** casos que a regra contra a qual se confere, **a regra é que está incompleta** |
| **causa escrita depois dos efeitos** | documentei repulsa e ledger como duas decisões lado a lado; as duas são consequência da **detecção ser total**, fato declarado depois. Quando dois fatos aparecem juntos e um parece explicar o outro, desconfie de que **falta o terceiro** |
| **listar arquivo por nome pareia errado** | montei a matriz `Pode*`→`Melhor*` por `find -name` e errei **quatro** linhas. Bastou abrir duas docstrings: *"uma combinação passageiro-**LZ**"* e *"usa a consulta prospectiva do **PodeTransferir**"*. O que decide o par é a **pergunta que o consumidor responde** |
| **checar um leitor não prova onde o dado mora** | afirmei que o `ConstructionDatabase` estava limpo porque o builder de topologia itera instâncias da cena. Havia uma seção `Field Entries (Map Scope)` que eu não procurei |
| **doutrina no doc de implementação** | escrevi o lema no `Capturer.md` (ao lado do código) sem checar que existia `docs/AI Behavior/Capturador.md`. Doutrina e comportamento têm casas diferentes, e a fronteira precisa estar escrita nos dois |
| **verso não é lugar de hipótese** | a Marcha vale como *"onde o código divergir de um verso, o código está errado"*. Um verso sobre unidade inexistente esvazia a regra para todos os outros. Regra sobre peça que existe entra; peça que não existe, não |
| **`default` de enum não é "nenhum"** | `default(ConstructionSector)` é **Alpha**. `!= default` como "tem vizinho" apagou Alpha do grafo |
| **`enumValueIndex` não é o índice de `Enum.GetValues`** | o popup mostrava o rótulo certo e a cena gravava o setor vizinho. O contrato de serialização é o **valor** |
| **layout de mapa em asset de catálogo** | o modo de falha é **silêncio**: só grita quando as coordenadas não existem no outro tabuleiro |
| **lookup que mistura fontes devolve lista temporária** | a migração copiou 23 rotas para um objeto que ninguém serializa. Escrita procura o **bucket serializado**, nunca o lookup |
| **migração que não é idempotente** | rodar de novo empilhou: 23 → 46. Migração **substitui**, e **confere o próprio resultado** |
| **auto-assign por "o primeiro que aparecer"** | `FindObjectsSortMode.None` é ordem arbitrária. Desempate explícito, e **avisar** quando não há critério |
| **`.gitignore` não desfaz o que já está no índice** | `Assets/_Recovery` tinha 28 arquivos rastreados; ignorar só barra o que é novo |
| **o limiar de SAÍDA é onde moram os turnos parados** | `repairTriggerHpBelow = 0` não solta ninguém: quem prende é `repairRecoverHpAbove = 8`. Os dois descem juntos |
| **campo global usado como decisão local** | ia passar `EnableLos = false` na vigilância; aquele campo é o **toggle da partida** |
| **memória onde o fato é observável** | o Fire Support lembra porque o passageiro embarcado não se vê da posição; contato na rede se vê toda rodada |
| **política única para famílias opostas** | aérea repele; naval não. Bifurcar por **família** antes de por postura |
| **onde eu ponho o teste erra mais que o que o teste faz** | a consulta cara antes do filtro barato, duas vezes na mesma sessão |
| **hash que se invalida pela própria contabilidade** | o `captureClaimStateHash` dobrava o `HasActed` das 66 unidades |
| **hit e miss de cache logam igual** | não dá para auditar cache pelo texto; só pelo contador |
| **verdade vazia em laço de prova** | `for (...) if (achou) return;` conclui o pior quando a lista está **vazia** |
| **doc que envelhece e vira fato** | e agora os docs estão **à frente** do código em vários pontos: as marcas `HOJE/CONTRATO/ABERTO` e `✅⚠️❌❓` só funcionam se alguém as mexer quando o código alcançar |
| **recompile em Play parece conserto** | salvar `.cs` religa `debugFogOfWarEnabled = true`. A configuração que causa isso é `Preferences > General > Script Changes While Playing`, **não** o `Enter Play Mode Settings` — são duas coisas diferentes e é fácil trocar |
| **estático que sobrevive ao Stop** | com `Enter Play Mode = Do not reload Domain or Scene`, um teste de save/load pode passar porque o estático **nunca morreu**. Para testar persistência: `Reload Domain and Scene`, ou fechar a Unity |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; admissibilidade precisa ser explícita |
| **dividir commit por hunk sem rede** | guarde o arquivo final, aplique a frente A, **restaure**, e o resto é a frente B |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |

---

## Critério de retomada

**A fatia 1 passou.** O bloqueio que atravessou duas versões acabou.

A fila curta:

```text
1. fatia 2 — levar MelhorCapturaCalls de 3 para 1 no turno de AQUISICAO
2. limpar a origem das rotas (destravado; ownerDatabase por último)
3. a lista MUDA REGRA — cinco divergências doc × código
4. abrir o capturador: quantas das ~20 excecoes sobrevivem como POLITICA
```

### Achados menores do ciclo T1→T6, para não se perderem

| achado | onde |
|---|---|
| `[FoW][RoundZeroBake] restored` deu **0/2** numa corrida e **1/2** noutra — o item pendente é **não-determinístico** | mesmo mapa, mesma cena |
| `[FoW][LoadCacheRestore] slot=0 success=false` com motivos **diferentes** (`construction_validation:Construction:1`, `split_gameplay_presentation`) — e o `LoadCacheVerify` diz `exact=True` no mesmo load | os dois não podem estar certos |
| shopping diz *"nenhuma oferta elegível ... **com gastoLivre=1993**"* quando o problema é **não ter fábrica**, não dinheiro | gate inaplicável de novo: não separa "não posso pagar" de "não tenho onde comprar" |
| `PreventiveDefense` pede `Artilleryx1` todo turno; shopping nega por *"doutrina rebelde: só Capturador"* | o Ops não sabe que o slot é rebelde |
| carimbo da Fase 0 lê o turno **antes** de avançar: `[AI ][T0] Fase0 concluída` no turno 1 | cosmético, mas faz ler log errado |
| `action=wait` no turno em que a unidade **capturou** | idem |

### O item 1, medido no tabuleiro isolado

Com 1 HQ, 1 cidade, 1 capturador rogue e **zero transportadores**, o portão
`Embarcar` rodou inteiro e **mutou estado**:

```text
[FilaCarona] #1 entra na fila no turno 1 — fora das bandas (score=1000)
[Capturador] 1 QueroCarona=SIM ... envelope=BeyondOperational
[Capturador] 1 embarque scan: ... nenhum transporte aliado <=8h
queroCarona: 14,1ms   melhorCaptura: 16,5ms/3 chamadas
```

Perguntou *"quero carona?"*, respondeu **sim**, varreu, não achou nada — e entrou
na fila. **A resposta não podia ser outra**, e mesmo assim pagou o custo e
escreveu estado.

Os outros três portões do topo ficaram inertes como previsto (reparo, handoff,
swap). O tabuleiro mínimo **apaga as exceções em vez de exigir que a gente as
desmonte** — é a única configuração em que a célula `Capturador × Capturar`
aparece sozinha.

**O item 4 é o degrau 3 de verdade**, e é o que o autor chamou de *"o grande
refactor das 6 armas"*. As vozes acabaram; o código não começou.

⚠️ **Os docs estão à frente do código em muitos pontos agora.** As marcas
`✅⚠️❌❓` das seis fichas só continuam úteis se alguém as mexer quando o código
alcançar. A `v8.0.1` sozinha adicionou sete linhas `❌`.

E a pergunta que fecha o major continua sendo um gesto: **duplicar uma cena,
apontar os catálogos, e o mapa novo nascer vazio.** Hoje isso vale para estrada.
Ainda não vale para construção.
