# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-08, **depois** da tag `v8.1.2`. Leia isto
primeiro.

---

## Estado

`v8.1.2` tagueada e publicada. Relatório:
[`relatorio_v8.1.2.md`](relatorio_v8.1.2.md).

**Seis defeitos corrigidos, e nenhum era informação faltando.** Em todos, o dado
existia e o problema era de **publicação** — quem escreve, quando, e em que
camada:

```text
exclusiveSlot       tinha 4 leitores. Todos perguntando "posso AGORA?"
                    contra quem já está a bordo — nunca contra as outras opções
missão herdada      escrita e apagada todo turno. Existia; nunca no instante
                    em que alguém olhava
wantsRide           não era fato da unidade: era resposta da pergunta alheia
range da IA         pintado — na camada SFX, debaixo de 261 tiles de névoa
lista de slots      o drawer da Unity existia; o laço manual não o invocava
parcial=False       sem indicador na tela; só aparecia no log
```

### ⚠️ Nada disso destravou a travessia

**O APC continua sem embarcar no navio.** O que a v8.1.2 fez foi tirar seis
coisas que faziam a investigação mentir. A decisão não mudou.

---

## ⏭️ O PRÓXIMO ITEM — a banda do `Embarcar`

`Embarcar` é o **único degrau da escada sem banda**:

```csharp
// AIController.TransportOperations.cs:206 — TryDecideNestedTransportEmbarkAction
int budget = Mathf.Max(0, unit.RemainingMovementPoints);
PodeEmbarcarSensor.CollectOptions(unit, boardTilemap, terrainDatabase, budget, options);
if (options.Count == 0) return null;      // silencioso: nem hit nem miss no log
```

Todos os outros correm `Tactical → Operational → Strategic`. Este corre
`Tactical` e acabou. Ele só sabe dizer *"o navio está colado em mim neste
turno"* — e nos dois estados para os quais foi criado, o navio está a dois
turnos. Cala, e o `Strategic` do `Delivery`, que sempre acerta, leva a decisão.

É a doutrina de novo: *banda é parâmetro da unidade avaliada, nunca constante do
papel.*

### ❓ E ele precisa de UMA decisão do autor antes do código

**A banda do `Embarcar` é de quem?**

```text
do transportador que sobe    "eu alcanço o navio"
do encontro                  "nós dois nos encontramos em N turnos"
```

Os outros degraus perguntam do próprio sujeito. Mas carona é a única situação em
que **os dois lados andam** — e o `MelhorEmbarque` já responde banda de encontro
em `rotaPax` (`ReachableNow / Later / Strategic`). Talvez `Embarcar` deva ler
dali em vez de calcular a sua.

E a pergunta colada: **se ganhar banda, continua sendo o número 1?** No Tactical
sim. No Strategic ele disputa com um `Delivery` que também acerta lá — ali
"primeiro" precisa querer dizer *ganha o empate*, não *responde antes*, senão um
navio a cinco turnos congela um APC que podia estar andando.

---

## Os quatro estados do transportador — o modelo que organiza o resto

Desenho do autor, escrito em [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md)
§7.1 a §7.6. **Nenhum valor novo no enum:** o estado sai de dois fatos que a
unidade já publica.

| `HasCargo` | `wantsRide` | estado | âncora | combate | hoje |
|---|---|---|---|---|---|
| false | false | **pickup** | hex provável de LZ | **pode combater** | ✅ |
| true | false | **courier** | a coordenada da carga | **cuida dela** | ✅ |
| true | true | **need a lift** | a carga, longe **ou atrás de travessia** | como courier | ⚠️ cai em courier |
| false | true | **ASAP** | **quem ele prometeu** | ❓ | ❌ inalcançável |

**O degrau não muda — a banda muda.** As duas linhas de baixo são as duas de cima
com o alvo longe. E para o transportador, **`Embarcar` é o sensor número 1**.

### O critério de aceitação — o resgate na ilha

Percorre as quatro células, na ordem, e volta:

```text
ida    APC vazio sobe no navio                    ASAP
       navio cruza, desembarca o APC
       APC atravessa o território até o soldado   pickup
volta  APC carregado espera na praia              need a lift
       navio recebe, cruza, desembarca
       APC termina o resgate                      courier
```

**Ida e volta são a mesma viagem, e a única coisa que muda é um bit.** Se a volta
exigisse estado novo, a fatoração estaria errada.

### A fila, em ordem

```text
1. banda do Embarcar     destrava need a lift   ← o único que muda o que se vê hoje
2. a pergunta do vazio   destrava ASAP          ← transportador vazio nunca publica
                                                  wantsRide (cai em "emergência apenas")
3. âncora de praia       need a lift precisa de âncora PRÓPRIA — o ponto de
                         encontro, não o destino da carga (TouchesComponent)
4. LZ em névoa           conferir antes de culpar a IA
```

⚠️ **O item 4 é a aposta de onde o cenário da ilha falha primeiro.** A regra
oficial da LZ é *"terreno visível ou já explorado"*, e no log de 2026-08-08 essa
é a rejeição mais volumosa do transporte inteiro — 394 ocorrências de
`REJECT reason=transporter_cell_not_visible_or_explored`. O cenário tem **duas**
atracagens. Não é problema de modelo de carona; é a regra da LZ.

---

## Onde eu parei — o que ficou pela metade

- **`parcial=True` não foi visto em jogo.** A derivação (`MatchController.IsFogPartialObserverActive`)
  compila e a lógica está auditada nos quatro cenários, mas ninguém rodou uma
  partida AI vs AI depois da mudança.
- **A janela do range é curta.** Mesmo com o observador certo, o range é pintado
  em `SetSelectedUnit` e apagado quando a unidade anda. As duas pausas do F11
  caem **fora** dessa janela (`Preparando próximo batch` é antes da seleção;
  `Executando batch` é depois do movimento). Falta um ponto de parada entre
  `SetSelectedUnit` e a execução — proposto, não decidido.
- **O painel mostra `Move: 3`** — o atributo, não o restante. Foi essa lacuna que
  fez "0 de movimento" parecer "range quebrado". `HP` e `Autonomia` já vêm com
  fração.
- **Exclusividade × vaga livre.** Carga *e* assento livre com alguém na fila é o
  caso que a tabela de quatro estados não resolve: os estados são exclusivos e a
  unidade está em dois. Marcado com ❓ em `PublishInheritedMissionIntent`; a carga
  ganha por ora. É o mesmo `return` de `BuildAttempts:283`.
- **A missão herdada continua write-only.** `TryResolveCargoDestinationAnchor`
  escava o passageiro primário em vez de ler a ficha do transportador. É a fatia
  2 — e agora ela é possível, porque a ficha finalmente está certa no instante da
  leitura.
- **Metade do peso da vaga nasceu dormente.** Com o slot `APC` do Chinook
  removido, o catálogo ficou sem nenhum `exclusiveSlot` e a metade *"desloca"* é
  sempre 1. A metade *"traz"* serve o canal e está viva.
- **`CLAUDE.md` está desatualizado.** Lista o ataque oportunista do courier
  (HP≤2, ≤2h) como prioridade viva; a regra só existe como cabeçalho de seção
  vazio e `Courier.Attack.cs` com zero chamadores. A doutrina do autor já é o que
  o código faz. Uma linha, quando ele quiser.

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

## Onde eu parei — os documentos

| documento | o que é |
|---|---|
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | **§7.1–7.6 novas** — os quatro estados, o resgate na ilha, os dois furos, quando a missão é publicada |
| [`AI Behavior/ficha_do_papel.md`](AI%20Behavior/ficha_do_papel.md) | a matriz `Pode*` → `Melhor*` **pareada pelo autor**, o questionário padrão, `RoleData` como dado |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | §0 o lema; §1 e §3 revistos; apêndice com a **Marcha do Capturador** |
| `Match/AI/3. Shopping/Shopping.md` | o buraco elegibilidade × preferência, e os três papéis-fantasma |
| [`AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md) | alocação pegajosa e as condições de baixa |
| [`AI Behavior/contrato_recencia_de_cobertura.md`](AI%20Behavior/contrato_recencia_de_cobertura.md) | ledger de idade da vigilância |
| [`AI Behavior/Assalto.md`](AI%20Behavior/Assalto.md) | a ficha; §5.1 furtividade aérea |
| [`AI Behavior/FireSupport.md`](AI%20Behavior/FireSupport.md) | a ficha; a modalidade **híbrida**; o auto-repelir |
| [`AI Behavior/Vigilancia.md`](AI%20Behavior/Vigilancia.md) | §0 o teste de pertencimento; §2.1 detecção total; §5.1 o preço do tiro submarino |
| [`AI Behavior/Logistica.md`](AI%20Behavior/Logistica.md) | o espelho da Vigilância; §5.1 a triagem que lê a moeda de quem pede |
| `Units/Capturer/Capturer.md` | o código: a ordem real, os seis mecanismos de ceder, o inventário |
| `docs/AI Behavior/rascunho/` | **a fonte** — o que o autor escreveu antes das fichas |

**Dívida declarada:** o `Capturer.md` é quatro documentos grampeados.

---
## A voz dos papéis — o método, e o teste que ele produz

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

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta o de cobertura de DETECÇÃO
                                  ⚠️ e o degrau `Embarcar` do transporte não
                                     consome banda nenhuma — Tactical fixo
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
| **compilar não prova que o arquivo mudou** | um script que aborta antes de gravar deixa a árvore idêntica, e o `git commit` passa se outro arquivo mudou junto. Aconteceu: um commit meu descrevia trabalho que não existia. Conferir o alvo, não só o build |
| **ferramenta que discorda do jogo é pior que ferramenta faltando** | duas vezes num dia: a bancada não passava `allowTransporterCell` nem `maxRemainingRouteCost`, e aprovava LZ que o runtime recusa. A resposta errada parece legítima. Se a pergunta é a mesma, o código tem que ser o mesmo — `TryResolveDeliveryZoneAnchor` virou estática por isso |
| **conclusão sem abrir o que existe** | quatro vezes hoje: disse que o Tactical não era skip (estava no organizador), que nada valorizava subir a montanha (a ferramenta dá +15,6), que o APC não sobe (tem `OFF Road` no asset), e que o Strategic cúbico era defeito (é projeto). **Antes de afirmar ausência: abrir a ferramenta, o asset, e a camada de cima** |
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
| **teste que se chama "estrutural" e mede prazo** | cinco vezes na v8.1.1. Estrutural é topologia: *"algum dia, a qualquer distância"*. Orçamento de turnos é ranking, uma camada acima. Se o nome do teste promete estrutura, ele não pode ter constante de tempo dentro |
| **duas flags que deviam andar juntas e não andam** | `includeStrategic = true` com `resolveLongRangePassengerMeeting = false` fazia o tier Strategic negar o próprio horizonte. Quando um pedido tem dois eixos (o que produzir × o que calcular), conferir se algum caller os separou |
| **cinco cláusulas, um só `false`** | o `isMaterializable` do Pickup recusava 48 opções com log idêntico ao de zero opções. Adivinhei duas vezes; à segunda errei. **Contador por motivo antes do terceiro palpite** — custa nada e transforma a corrida seguinte em resposta |
| **alargar o conjunto de candidatos sem dar razão para ficar** | as cinco correções somadas fizeram o navio trocar de passageiro todo turno. Quem passa a ver mais opções precisa de **histerese** na mesma passada, senão vira otimizador global — exatamente o que já mordeu o capturador |
| **`alvo=(0,0,0)` não é "sem alvo"** | é a célula (0,0,0). Li como ausência e construí meia teoria em cima. Coordenada nula e coordenada zero são indistinguíveis no log — conferir no Inspector antes de concluir |
| **"não era o bloqueio" ≠ "não era necessário"** | classifiquei o horizonte por tier como decoração porque não destravou sozinho. A opção vencedora do navio é `ReachableStrategic` — era carga. Numa série de portas, cada uma é necessária e nenhuma é suficiente |
| **pedir estado ao humano em vez de ler o log** | perguntei *"o preset é Total?"* e *"o observador é o slot ativo?"*. Ele respondeu sim de boa-fé e eu segui por um caminho errado. `parcial=False` estava impresso no log dele o tempo todo, vindo direto do campo. **Se existe linha que imprime o campo, ela é a fonte — sim/não é trocar medição por lembrança** |
| **busca vazia não prova ausência; o diff prova** | o autor disse que a lista de slots era editável antes. Rodei `git log -S`, achei só a *adição* do laço manual e afirmei que os botões nunca existiram. Não abri o diff para ver **o que aquele laço substituiu**. Era um `PropertyField` no array inteiro. Regressão de `639c02e` |
| **overlay não some — desce de camada** | procurei um `if` que desligasse a pintura do range; não havia. `sortingLayerName = playerTurn ? "FogOfWar" : "SFX"`. Estava pintado embaixo de 261 tiles pretos. **Antes de procurar o gate que desliga, conferir se o que sumiu não está atrás de outra coisa** |
| **campo booleano sem indicador é indistinguível de si mesmo** | "liguei a névoa" e "liguei o parcial" pareciam iguais no jogo. Estado que só aparece no log só existe quando alguém roda o comando que o imprime |
| **defender o invariante onde ele não se aplica** | o autor achou estranho a missão ser registrada depois de agir; respondi "é o invariante transacional" como se fosse fim de discussão. O invariante protege o **tabuleiro**. Missão é **intenção**, e intenção publicada depois da ação não serve para a única coisa que ela faz. Ele estava certo; levei duas voltas |
| **a condição de baixa de um significado é a de início do outro** | `intent=Transport → #N` servia à promessa **e** à herança. Quando um campo tem dois donos, procure a transição onde um termina exatamente onde o outro começa — foi ela que apagou a missão do APC todo turno |
| **"não" alheio não é informação sobre mim** | quem responde sim sabe de um caminho real; quem responde não só sabe que **ele** não serve. Publicação vinda de terceiro pode **levantar**, nunca baixar |
| **o degrau que não loga é o que decide** | `Courier`, `Delivery` e `Pickup` logam hit/miss com motivo. `Embarcar` sai por `options.Count == 0` calado — e foi ele que segurou o APC em **duas** sessões seguidas |
| **um único asset pagando por uma dimensão inteira** | o Chinook era o **único** `exclusiveSlot` do catálogo, e obrigava serviço, sensores, motor e score a carregar o caso. Contar os portadores antes de generalizar |
| **regra nova mais estreita que a que substitui** | ia deixar `PublishRideNeed` como escritora única e derrubaria o Radar da fila — a zona de vigilância não está na missão. Só não aconteceu porque fui conferir os quatro pontos antes de cortar |

---
