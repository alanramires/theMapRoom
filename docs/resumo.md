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

## O PRÓXIMO PASSO — e é curto

**Rodar a fatia 1.** O commit `3e0565d` unificou o alvo de captura na missão
(`AIPlanRuntimeIntent.Capture` finalmente tem quem o escreva). Ele **compila nas
duas assemblies e nunca rodou**.

```text
1. o save do teste anterior NÃO carrega mais o alvo (os campos saíram do DTO)
2. refazer os dois turnos no Hot Seat 0 - Treino
3. turno 1: [Missao] 1 Capture -> (0,0,0) predio=#2 (adquirida)
            Inspector: Mission Intent = Capture, Has Designated Mission ✔
4. salvar, fechar, abrir, carregar
5. turno 2: [Missao] ... (mantida)  e ele sai de (4,0) para (1,0)
```

**O comportamento tem que ficar IDÊNTICO.** A fatia é subtração — se mudar, está
errada.

Antes de rodar, conferir `Project Settings > Editor > Enter Play Mode Settings`:
com **Reload Domain desligado**, os estáticos sobrevivem ao stop e o teste passa
pelo motivo errado.

**Fatia 2, depois:** a inversão do táxi — missão pendente resolvida no topo,
carona medida contra ela, latch da missão, nota proporcional ao excedente.

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
| **recompile em Play parece conserto** | salvar `.cs` religa `debugFogOfWarEnabled = true` |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; admissibilidade precisa ser explícita |
| **dividir commit por hunk sem rede** | guarde o arquivo final, aplique a frente A, **restaure**, e o resto é a frente B |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |

---

## Critério de retomada

**Rodar a fatia 1** — dois F11, um save/fecha/abre, e conferir que o
comportamento não mudou. É subtração; qualquer mudança é defeito.

Depois dela, a fila curta:

```text
1. fatia 2 — a inversão do táxi (missão no topo, carona medida contra ela)
2. limpar a origem das rotas (destravado; ownerDatabase por último)
3. a lista MUDA REGRA — cinco divergências doc × código
4. abrir o capturador: quantas das ~20 excecoes sobrevivem como POLITICA
```

**O item 4 é o degrau 3 de verdade**, e é o que o autor chamou de *"o grande
refactor das 6 armas"*. As vozes acabaram; o código não começou.

⚠️ **Os docs estão à frente do código em muitos pontos agora.** As marcas
`✅⚠️❌❓` das seis fichas só continuam úteis se alguém as mexer quando o código
alcançar. A `v8.0.1` sozinha adicionou sete linhas `❌`.

E a pergunta que fecha o major continua sendo um gesto: **duplicar uma cena,
apontar os catálogos, e o mapa novo nascer vazio.** Hoje isso vale para estrada.
Ainda não vale para construção.
