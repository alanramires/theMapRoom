# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-06, **depois** da tag `v8.0.0` e de dez
commits que vieram depois dela. Leia isto primeiro.

---

## Estado

`v8.0.0` tagueada e publicada. Relatório: [`relatorio_v8.0.0.md`](relatorio_v8.0.0.md).

**Depois da tag vieram dez commits, nenhum com tag** — não há relatório novo, e
pela regra do projeto isso é só commit. O dia pós-tag foi **quase todo desenho**:
uma fatia de código (compilada, não rodada) e sete documentos.

### O que passou em jogo hoje

**O teste do capturador PASSOU.** Era o critério de retomada anterior e ele foi
feito:

```text
save → FECHAR o jogo → abrir → carregar
Inspector, ANTES de a IA pensar:
   Has Designated Capture Target  ✔   ID 2   Cell (0,0)
turno 2: origemAlvo=servico → origemAlvo=reserva, envelope=Operational
         [FilaCarona] #1 sai da fila — nao quer mais carona
```

**Conclusão: a alocação pegajosa é PROMOÇÃO, não construção.** O mecanismo já
existia inteiro — só no caminho rebelde.

### A descoberta que organiza o dia

> **A peça certa aparece encostada na errada.**

Sete vezes, e em subsistemas que não se falam:

```text
DesignatedCaptureTarget    alocação pegajosa completa — só no rebelde
CounterPressure            canal por capacidade existe — o Demand usa nome de papel
imposto de conscrição      a barra dupla existe — presa à dificuldade, não à postura
modo hospital              SameHexOrEmbarked já explicava a assimetria do Suprir
nested transporters        o motor suporta; falta o Courier pedir carona
FilaCarona + RidePromise   o anti-fome já está lá, antiguidade que não zera
isSupplier / Logistica     "ninguém apenas lhe pergunta nada" (ideias_futuras §11)
```

**Vira heurística de busca:** antes de escrever a peça nova, procurar a que já faz
isso para outro dono.

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

O transportador ganhou a dele:

> **O transportador serve a carga.**
> **Chegar cedo é entregar; o casco é a capacidade, e o destino nunca é meu.**

**Faltam:** Assalto, Fire Support, Logística, Vigilância.

E o princípio que saiu da comparação entre os dois, e que vale para o próximo:

> **Cada papel tem uma moeda, e a moeda decide se fundir é ganho ou perda.**
> Capturador: HP é o relógio → concentrar acelera → fundir ganha.
> Transportador: o casco é a capacidade → concentrar destrói → fundir perde.

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
| `Units/Capturer/Capturer.md` | o código: a ordem real, os seis mecanismos de ceder, o inventário |

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
 3. papéis → só POLÍTICA          ⚠️ a ficha está escrita; RoleData não existe
 4. variações de papel            perfil/trait depois da extração
```

O degrau 3 **saiu do zero hoje**: `ficha_do_papel.md` descreve a forma, e dois
papéis já têm voz.

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
4. as vozes que faltam: Assalto, Fire Support, Logística, Vigilância
```

E a pergunta que fecha o major continua sendo um gesto: **duplicar uma cena,
apontar os catálogos, e o mapa novo nascer vazio.** Hoje isso vale para estrada.
Ainda não vale para construção.
