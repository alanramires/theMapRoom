# Ficha do papel — a matriz das capacidades e o questionário padrão

**Estado:** desenho do autor, capturado em 2026-08-06. A **matriz** (§2) é
inventário verificado do código. A **ficha** (§4), a **ordem** (§5) e o
**RoleData** (§7) são contrato de desenho, sem uma linha de implementação. O
único item em aberto é o §7.6.

> **HOJE** = verificado no código. **CONTRATO** = decidido, não escrito.
> **ABERTO** = ninguém decidiu.

---

## 1. Por que existe

É o **degrau 3 da escada** — *"papéis → só POLÍTICA"* — ganhando forma. Se todo
papel responde o **mesmo questionário**, a diferença entre papéis passa a ser a
política, por construção. Não sobra lugar para um papel esconder regra própria.

E as categorias não foram inventadas: elas **são a família `Pode*`**. Isso é a
doutrina se mostrando — o sensor é a fonte de verdade das ações legais, então a
IA se organiza pelo sensor, não por uma taxonomia paralela.

---

## 2. A matriz — três colunas por categoria

**Pareamento definido pelo autor em 2026-08-06.** A primeira versão desta tabela,
montada por listagem de arquivo, errava **quatro** linhas — as correções estão
marcadas.

| # | `Pode*` | `Melhor*` | o que o consumidor resolve |
|---|---|---|---|
| 1 | `PodeCapturar` | `MelhorCaptura` | capturáveis e **recapturáveis** na proximidade, ou pela força do plano atribuído |
| 2 | **Reposicionamento** | `MelhorEmbarque`, `MelhorCapitao` | a atração de **onde você tem que estar** ⬅ *corrigido* |
| 3 | `PodeEmbarcar` | `QueroCarona` | o momento de **atrair transportadores** para embarcar neles ⬅ *ausente antes* |
| 4 | `PodeDesembarcar` | `MelhorDesembarque` | interseção das zonas operacionais das cargas, para liberar **todos** nos melhores locais |
| 5 | `PodeMirar` | `MelhorCombate` | a guerra |
| 6 | `PodeDetectar` | `MelhorDeteccao`, `MelhorSpotting` | encontrar inimigos |
| 7 | `PodeEnxergar` | `MelhorVisao` | **revelação pura de hexágonos** |
| 8 | `PodeTransferir` | `MelhorEstoque` | quem precisa de recursos ⬅ *corrigido* |
| 9 | `PodeSuprir` | — | ❌ sem serviço. Quando desmembrar do AI Suprir: **criticidade, peso por elite, manutenção preventiva** |
| 10 | `PodeFundir` | — | ❌ sem serviço. A única regra é **fundir na retaguarda**, e mora no `AIRepair` |

### Os quatro erros da primeira versão, e o que cada um ensinou

```text
MelhorEmbarque em "embark"    ele devolve "uma combinacao passageiro-LZ" —
                              LZ e LUGAR. E reposicionamento, nao o ato
QueroCarona ausente           eu o tratava como consumidor do envelope de
                              CAPTURA; ele e o lado-passageiro do embarque
MelhorEstoque em "supply"     "usa a consulta prospectiva do PodeTransferir" —
                              e a rede de estoque, nao o ato de suprir
MelhorCapitao "sem categoria" e o outro consumidor do reposicionamento
```

Listar arquivo por nome pareia errado. **O que decide o par é a pergunta que o
consumidor responde**, e ela está na docstring dele.

### O que a tabela corrigida mostra

**Quatro `Melhor*` faltam:** os de `Suprir` e `Fundir` (o autor já disse o que
farão), e `MelhorDeteccao` e `MelhorSpotting` — desenhados em
`contrato_recencia_de_cobertura.md`, sem uma linha de código.

**`MelhorPouso` sai da lista.** Pouso e decolagem **não são chamados como
serviço** — são consequência de evento (upkeep, rebasing). Ver §6.

**E `MelhorVisao` tem o trabalho certo escrito aqui:** *revelação pura de
hexágonos*. É exatamente o que ele **deveria** fazer, e hoje o ramo `IsAll` dele
responde por detecção — a divergência está registrada em
`contrato_recencia_de_cobertura.md` §4.2.

**Uma linha só está de pé quando as três colunas existem.** Hoje isso vale para
**capture**, e há poucas horas — a unificação que fez o alvo de captura virar
missão está na árvore, **compilada e ainda não commitada**. Até ela,
`AIPlanRuntimeIntent.Capture` era o verbo nº 1 do enum com **zero ocorrências no
código**.

Use a linha de capture como referência de como uma linha completa se parece.

---

## 3. As três assimetrias que a matriz expõe

**Combat tem três verbos para uma categoria.** `Pressure`, `FireSupport` e
`AntiAir` não são coisas diferentes de **fazer** — são o **papel** disfarçado de
verbo. Se o padrão continuar, o enum cresce por papel novo em vez de por ação
nova. O verbo deveria ser um, e o papel dizer como.

**Embark e disembark não têm verbo do lado do passageiro** — e agora sabe-se
qual consumidor faz essa pergunta: `QueroCarona` (linha 3 da matriz). `Transport` é do
**transportador** — é a promessa dele, com o passageiro em
`AIDesignatedMissionTargetUnitInstanceId`. Quem levanta a mão não tem missão
nenhuma, e é exatamente por isso que ele pede carona sem destino:

> **Ninguém pede táxi sem saber para onde vai.**

Com a missão de captura escrita, o pedido passa a ser medido contra ela — ver
`contrato_missao_captura.md`.

**Fuse e transfer têm sensor e mais nada.** A IA nunca funde; e a cadeia
logística está registrada em `ideias_futuras.md` (item 3) como frente própria.

E `MelhorCapitao` é **consumidor sem consumidor**: falta o tradutor
`AICaptainData → List<MelhorCapitaoAttraction>`.

---

## 4. A ficha padrão do papel

**CONTRATO.** Todo papel expõe as mesmas entradas:

```text
AIController.<papel>
    Capture
    Aim
    Embark
    Disembark
    Fuse
    Supply
    Transfer
    Enxergar          ← duas linhas, nunca uma: ver §7.5
    Detectar
    Repositioning     ← acao nula, SEMPRE por ultimo: ver §6
```

### 4.1 Célula vazia precisa de TRÊS estados

Ficha padrão só funciona se "vazio" não for ambíguo:

```text
política própria   este papel decide diferente do genérico
padrão             cai no comportamento genérico
NÃO SE APLICA      o sensor nunca devolve opção para esta peça
```

Sem o terceiro, a matriz repete o defeito que a `v8.0.0` nomeou — *"não
respondi"* e *"a pergunta não é minha"* virando a mesma célula em branco. Um
submarino na linha `capture` não é buraco: é inaplicável, e a ficha tem que
dizer isso em vez de deixar em silêncio.

### 4.2 O gate da categoria é o sensor, não a etiqueta

**CONTRATO — e é a formulação que evita o erro mais caro do projeto.**

```text
ERRADO   tenho a etiqueta X  →  faço X
CERTO    o sensor devolveu opção não-vazia  →  tenho assunto
         →  o papel decide se vale o turno
```

A chave não é poder da unidade. Para captura, quem lista a chave é a
**construção** (`ConstructionData.requiredSkillsToCapture`), não a ficha da
unidade. A diferença entre as duas formulações é a diferença entre **consultar o
sensor** e **reimplementar a regra dele** — e reimplementar é como a tabela de
flags cresceu da primeira vez.

Corolário: gates de papel usam `UnitRoleCompatibility.CanSatisfy`, nunca
`roles.Contains` estrito, senão `CapturadorCombatente` e parentes são barrados
por variação de nome.

---

## 5. A ordem da ficha é o papel

**CONTRATO.** **HOJE** o roteador tem ordem fixa e global
(`AIController.Router.cs`): rebelde → capturador → assalto → transportador →
fallback do `HexEvaluator`. A ordem é a mesma para todo mundo, e cada papel se
defende com guardas internas.

Com ficha padrão, a ordem passa a ser **de cada papel**:

```text
Capturador      Capture → Embark → Aim → Repositioning
Artilheiro      Aim → Supply → Repositioning   (Capture NÃO SE APLICA)
Transportador   Embark → Disembark → Repositioning
```

**Isso não pede refactor de arquivo.** Os ~20 partials podem continuar nomeados
por papel; o que muda é cada um expor as mesmas entradas e uma ordem declarada.
Reorganizar por categoria, se um dia valer, vem depois.

### 5.1 A ideia certa que estava no lugar errado

O campo `aiSensorPriority` (`UnitData`, marcado `LEGADO AI_Legacy`, apagado na
`v8.0.0`) tentava exatamente isto: uma lista ordenada de ações por unidade.
Estava errado em **dois** pontos, e vale registrar para não ressuscitar a forma
junto com a ideia:

```text
na ficha da UNIDADE   prioridade de ação é do PAPEL, não da peça
consumido por um orquestrador que hoje não compila (AI_Legacy~)
```

A ideia renasce como **ordem do questionário do papel**, em código, versionada
com ele — não como campo serializado que ninguém lê.

---

## 6. `move / magnético` — RESOLVIDO: ação nula, e por último

Todas as demais categorias têm um `Pode*` que devolve opções. Movimento não:
`UnitMovementPathRules` não é sensor, e o magnético é doutrina
(`MelhorCapitao`, o capitão que a peça orbita).

Duas leituras, e elas mudam o desenho:

**(a) substrato.** Movimento sai da lista e vira **coluna**: o *"para onde"* de
cada categoria. `capture = mover + capturar`, `combat = mover + atirar`,
`embark = mover + embarcar`. O magnético é a âncora quando a categoria não tem
alvo próprio.

**(b) ação nula.** Movimento fica na lista como *"o que eu faço quando nenhuma
outra categoria respondeu"* — que é o rogue de hoje
(`[Rogue] 1 marcha para âncora (0,0,0)`).

Se for **(a)**, `Move` nunca compete com `Capture`. Se for **(b)**, compete — e
aí precisa ser sempre o último da ordem, senão engole as outras.

**CONTRATO — o autor decidiu (b)**, e já a colocou onde ela precisa estar: o
nome vira `Repositioning` e é o **último item** da prioridade. É o rogue de hoje
(`[Rogue] 1 marcha para âncora (0,0,0)`) ganhando lugar declarado em vez de ser
fallback implícito do roteador.

**E `camada` sai da lista de vez.** Pouso e decolagem **não são chamados como
serviço** — são consequência de evento. `PodePousar` aparece no upkeep (pouso de
emergência com 0 de combustível) e o `MelhorPouso` no rebasing. Ninguém "decide
pousar" como decide capturar, então não há linha de questionário para eles.

---

## 7. A ficha como dado — `RoleData` e `RoleDatabase`

**CONTRATO.** Desenho do autor. Nada existe.

### 7.1 O motivo, que também é o critério de sucesso

> *"Hoje o código que você faz no `AIController.Capturer` para mim é uma
> caixinha de surpresas. Com esses data, eu creio que eles ficarão visíveis."*

Isso não é conveniência — é a **medida** do desenho. É o mesmo movimento que a
Hotzone e a janela de Shopping Pressure já fizeram: tornar visível o que só
existia como decisão enterrada. **Se o asset não tornar visível o que hoje está
escondido, ele falhou** — e vira só mais um lugar para procurar.

E é o que decide a fronteira do §7.3: o que se expõe é exatamente o que hoje é
invisível — **ordem e peso**. O que fica em código é o que é genuinamente
algoritmo.

### 7.2 O formato

Segue o padrão da casa (`UnitData`/`UnitDatabase`, `ConstructionData`,
`StructureData`) — seria o quarto da família:

```text
RoleData
    id
    role          (o enum UnitRole que ja existe)
    descricao

    SensorPriority   (lista ORDENADA)
        Capture, Aim, Embark, Disembark, Fuse, Supply, Transfer,
        Enxergar, Detectar, Repositioning

    Politica por categoria
        Capture:   ...
        Aim:       ...
        Embark:    ...
```

### 7.3 A fronteira — o que a célula pode carregar

**CONTRATO, e é aqui que o desenho vive ou morre.**

```text
PODE    ordem              a prioridade do questionario
PODE    pesos e limiares   apetite, banda minima, required, aderencia
PODE    admissibilidade    esta categoria nao se aplica a este papel
─────────────────────────────────────────────────────────────────────
NAO     o codigo que decide  fica no AIController.<papel>
```

> **O asset carrega o questionário e o peso das respostas. Nunca o responder.**

Concretamente: a célula `Capture` do Capturador pode dizer *"ordem 1, aderência
ao objetivo anterior −15, banda de aquisição Operacional"*. **Não** pode dizer
*como* escolher entre dois prédios — isso é o `MelhorCaptura`, e ele é serviço.

Sem essa linha, o asset vira linguagem de script no inspector e o teste da chave
falha: *renomear o papel quebra alguma coisa?*

### 7.4 Identidade: o enum continua mandando

`id` **e** `role` são duas identidades para a mesma coisa, e é o **enum** que vai
no save (`UnitData.roles`, `aiAssignedPlanRole` como int).

**CONTRATO:** o enum continua sendo a identidade e o `RoleData` é resolvido por
ele — igual ao `ConstructionData.id`. O asset acrescenta política sem disputar
identidade. E herda a regra do `AIPlanRuntimeIntent`: **valor novo entra no fim;
renumerar não migra papel antigo, troca papel antigo.**

### 7.5 `Fow and Detect` são DUAS linhas

**CONTRATO.** A `v7.1.0` inteira foi para separá-las:

```text
PodeEnxergar   revela HEXAGONOS
PodeDetectar   faz UNIDADES aparecerem
```

Uma célula só no questionário é o primeiro passo para elas voltarem a
compartilhar resposta — que é exatamente o defeito que custou dias. Duas linhas,
cada uma com seu consumidor: `MelhorVisao` de um lado, o `MelhorDeteccao` que
ainda falta do outro.

`Aim` está melhor que `combat`, porque casa 1:1 com `PodeMirar`. Sobra o
consumidor chamado `MelhorCombate` — desalinhado no nome, e só isso.

### 7.6 ABERTO — "não se aplica" é autorado ou derivado?

Um submarino não captura, mas ele também **não tem o papel Capturador**. Então a
inaplicabilidade já se resolve por *quais papéis a peça tem* × *o sensor
devolveu vazio* — sem campo nenhum.

Se for assim, o terceiro estado da §4.1 é **leitura**, não dado: menos coisa
autorada é menos coisa para envelhecer. Falta conferir se existe papel que se
aplica a uma categoria e não a outra de um jeito que o sensor não pegue. Se
existir, o campo se justifica.

---

## 7.7 PAPEL e RÓTULO — a separação que fecha o degrau 3

**CONTRATO — decidido pelo autor em 2026-08-06.** É a resolução de tudo que este
documento discute: os valores do enum **não saem**. Eles são **rebaixados**.

```text
PAPEL    comportamento    ordem do questionário, moeda, posicionamento, marcha
RÓTULO   identidade       o que o shopping pede, o que a ficha declara,
                          o que nomeia a subvariante
```

| papel | rótulos / subvariantes |
|---|---|
| **Capturador** | Capturador, **Capturador Combatente** |
| **Transportador** | Transportador Terrestre, Transportador Aéreo, Transportador Naval |
| **Assalto** | Assalto, Caça Interceptador, Ataque Aéreo |
| **Fogo de Suporte** | Fogo de Suporte, Antiaéreo, Artilheiro Combatente, Antiaéreo Combatente |

> **Por que `Capturador Combatente` e não `Agressivo`** (renomeado em 2026-08-06):
> o autor levou o rótulo antigo a várias IAs e **todas leram como "captura
> avidamente"** — o oposto do que ele significa, que é *briga em vez de capturar*.
> O nome não era impreciso: estava dizendo outra coisa, e o erro se repetia em
> todo leitor.
>
> Com `Combatente`, o sufixo passa a significar **sempre a mesma coisa em toda a
> família** — *tem arma de contato*: `Capturador Combatente`,
> `Artilheiro Combatente`, `Antiaéreo Combatente`.
>
> O valor do enum continua `= 12`; só o identificador mudou, então asset, cena e
> save não sentem.

**Quatro papéis. Onze rótulos.** Quatro questionários, quatro moedas, quatro
marchas — e onze nomes para o shopping distinguir demanda.

> Escrito quando só quatro papéis tinham ficha. O quadro fechado é o de §7.8:
> **seis papéis, seis moedas, dezessete rótulos, cinco marchas.** Este parágrafo
> fica como marco de onde a contagem estava.

### A tradução JÁ EXISTE, e mora no mesmo arquivo do enum

```csharp
// UnitRole.cs — UnitRoleCompatibility.ResolveCompositionRole(UnitData)
CapturadorCombatente   -> Capturador
ArtilheiroCombatente  -> data.unitClass == Armored ? Assalto : FogoIndireto
TransportadorAereo    -> Transportador
```

`ResolveCompositionRole` **é** *"rótulo → papel"*. Até o rótulo de transporte já
está lá — os três tipos de transporte não eram invenção nova, eram os que
faltavam nomear.

E repare na linha do `ArtilheiroCombatente`: ela decide o papel **pela
`unitClass`**. É exatamente a regra que a discussão chegou por outro caminho —
**a armadura decide a posição; a arma decide o tiro** — já implementada. O que
falta é ela ser **regra declarada** em vez de um `if` dentro de um resolvedor.

### O que muda: quem consulta o quê

```text
roteador e IA        consultam o PAPEL      via ResolveCompositionRole
shopping e ficha     consultam o RÓTULO     demanda, pressão, subvariante
```

**Consequência:** o roteiro de remoção do `Shopping.md` §3.1 fica **superado**.
Não é preciso tirar valor nenhum do enum — o que resolve a objeção registrada lá
(*"remover o papel apaga a demanda defensiva barata sem substituto"*). O rótulo
fica; só o comportamento consolida.

### E a decisão sobre os combatentes

`ArtilheiroCombatente` e `AntiaereoCombatente` são rótulos de **Fogo de Suporte**,
não de Assalto. A base é o fogo de suporte, e a falha das três primeiras casas
(`Detectar`, `Enxergar`, `Mirar` no alcance) é o que autoriza o avanço ao contato
— **e não é preciso caminho de volta**: o turno seguinte roda o questionário do
começo, e a regra *"sozinha na vanguarda → recua"* (`FireSupport.md`) já traz a
peça de volta sem código novo.

---

---

# 7.8 Os quatro papéis — quadro canônico

**Consolidado pelo autor em 2026-08-06, no fim da sessão.** Onde este quadro
divergir das fichas por papel, **este vale** — as divergências estão marcadas em
`⚠️ DELTA` e as fichas precisam ser corrigidas.

---

## Capturador — *"converter $ pro exército"*

**Subpapéis:** Capturador (Soldados), Capturador Combatente (Bazookas, Metranca)

```text
Enxergar, Detectar, Capturar, Embarcar, Reposicionar,
Mirar, Fundir, Suprir, Transferir, Desembarcar
```

> **⚠️ DELTA vs `Capturador.md`.** A ficha anterior tinha `Capturar` em 1º e
> `Reposicionar` em 10º. Mudou:
>
> ```text
> antes   Capturar, Detectar, Enxergar, Embarcar, Desembarcar, Mirar, ...Reposicionar
> agora   Enxergar, Detectar, Capturar, Embarcar, Reposicionar, Mirar, ...Desembarcar
> ```
>
> **Enxergar e Detectar passaram na frente de Capturar** — coerente com a
> justificativa que já estava escrita: *chegar na névoa impede capturar no mesmo
> turno*, então a visibilidade se resolve **antes** de comprometer o turno.
> E `Reposicionar` subiu de 10º para 5º: deixou de ser só a ação nula do fim.
>
> A **Marcha do Capturador** não precisa de ajuste — a estrofe da névoa já vem
> antes das de deslocamento.

---

## Transportador — *"leva e traz unidades"*

**Subpapéis:** Terrestre (APC), Aéreo (Chinook), Naval (Porta-Aviões)

```text
Pickup (vazio)    Embarcar, Reposicionar, Enxergar, Detectar, Mirar,
                  Transferir, Suprir, Capturar, Desembarcar, Fundir

Courier (carga)   Embarcar, Reposicionar, Enxergar, Detectar, Suprir,
                  Transferir, Desembarcar, Mirar, Capturar, Fundir
```

> **⚠️ DELTA vs `Transporte.md` §0.1.**
> - **Pickup:** `Desembarcar` caiu de 6º para 9º — vazio, não há o que largar.
> - **Courier:** `Detectar` subiu de 7º para **4º**, antes de `Suprir`.
>
> O `Detectar` alto no Courier reforça a disciplina do modal: com carga, saber
> quem está no ponto de largada é **precondição**, não valor.

---

## Assalto — *"eu rompo barreiras, seja em terra ou no ar!"*

**Subpapéis:** Assalto (tanques), Interceptadores (caças), Ataque Aéreo
(bombardeiros)

```text
Detectar, Mirar, Embarcar, Reposicionar, Capturar,
Transferir, Suprir, Desembarcar, Enxergar, Fundir
```

| eixo | valor |
|---|---|
| **modalidade** | **combatente** — combate em contato (alcance mín 1) |
| **posicionamento** | **vanguarda** — entre a massa oponente e o capitão, **à frente dele** |

✅ Sem delta: bate com `Assalto.md`.

---

## Fogo de Suporte — *"A morte vem do alto, e você nem verá de onde veio"*

**Subpapéis:** Fogo de Suporte (Art. Campanha), **Bombarda Naval (Destroyer)**,
Antiaéreo (SAM), Antiaéreo Combatente (AAA com lagarta), Artilheiro Combatente
(**Morteiro**)

```text
Detectar, Enxergar, Mirar, Reposicionar, Embarcar,
Transferir, Suprir, Desembarcar, Capturar, Fundir
```

| eixo | valor |
|---|---|
| **modalidade artilheiro** | atira **parado**, segurando a posição. **Sem arma de contato** (alcance mín > 1) |
| **modalidade híbrida** (× combatente) | tenta os **três primeiros sensores** no fogo de suporte; **se falhar, vai pro assalto** |
| **retaguarda** | entre a massa oponente e o capitão, **atrás dele** |
| **flancos** | esquerda e direita do capitão, como cobertura de fogo |
| **auto-repelir** | repele os seus dentro do tático. Três canhões juntos: basta um assalto. Três em delta cobrindo os pontos cegos: nada entra |
| **atração** | fogo de suporte com o **capitão**; antiaéreos com as **vigilâncias aéreas** |

**Tático e Operacional invertidos, e sempre cúbicos** (ignoram geografia):
`Tático` = alcance da arma (min–max); `Operacional` = o dobro.
**O movimento não serve para medir a cobertura.**

> **⚠️ DELTA vs `FireSupport.md`.**
> - **`modalidade híbrida` é o nome certo** do que a ficha registrava como
>   "decisão (a)". Não é rótulo nem papel: é uma **terceira modalidade**, ao lado
>   de combatente e artilheiro. Mais limpo, e resolve a pergunta (a)/(b) sem
>   inventar caminho de volta.
> - **Subpapéis expandidos:** o Destroyer ganhou rótulo próprio — **Bombarda
>   Naval** — e o exemplo do Artilheiro Combatente é o **Morteiro** (arma longa
>   *e* de contato), não um obus.

---

## Vigilância — *"tem uma guerra acontecendo em algum lugar e eu não me importo, contanto que eu ache minha presa"*

**Subpapéis:** Aérea (EWACS, Radar Móvel), Anti-Sub (Super Tucano, Fragata ASW,
Submarino)

```text
Detectar, Mirar, Reposicionar, Suprir, Transferir,
Desembarcar, Embarcar, Capturar, Fundir, Enxergar
```

| eixo | valor |
|---|---|
| **modalidade** | **híbrida** — segue a linha do Artilheiro Combatente: primeiro fogo de suporte, depois assalto |
| **posicionamento** | **bifurca**: aérea **repele** (maximiza área, que envelhece); anti-sub **agrupa** (âncora no leito e nos canais) |
| **magnético** | só quem tem `playConservative`; os outros **não têm**, salvo alocação em plano |

**`Detectar` em 1º e `Enxergar` em 10º** — a separação mais extrema das duas
verdades no projeto. Ficha completa em `Vigilancia.md`.

---

## Logística — *"está ferido? acabou a bala? aguenta aí que eu tô chegando!"*

**Subpapéis:** Logística de Campo (o serviço), Estoque (o movimento)

```text
Enxergar, Suprir, Transferir, Reposicionar, Embarcar,
Desembarcar, Mirar, Fundir, Capturar, Detectar
```

**É o espelho exato da Vigilância** — `Enxergar` 1º e `Detectar` 10º, contra
`Detectar` 1º e `Enxergar` 10º. Os dois papéis ocupam as pontas opostas da
doutrina das duas verdades, e isso é a melhor prova de que os dois sensores
respondem perguntas diferentes: **um papel inteiro só precisa de um deles.**

> *"Não se preocupam em detectar o inimigo, pois se ele está tão perto já é tarde
> demais."*

Ficha completa em `Logistica.md`.

---

## O que este quadro fixa

```text
3 modalidades    combatente, artilheiro, HÍBRIDA
6 papéis         questionário, moeda, posicionamento
17 subpapéis     o que o shopping pede e o que a ficha declara
6 marchas        uma por papel — o conjunto está COMPLETO
```

A sexta (Logística) é a única que **descreve os outros cinco**: ela triage lendo
a moeda de quem pede. Ver `Logistica.md` §5.1.

E as **seis moedas**, que respondem sozinhas se uma peça funde:

| papel | onde o valor mora | fundir |
|---|---|---|
| Capturador | o **corpo** — HP é a taxa | **ganha** |
| Transportador | as **vagas** | perde |
| Assalto | a **arma** — cada casco é ameaça | perde |
| Fogo de Suporte | a **formação** — cones cobrindo pontos cegos | perde, e **agrupar também** |
| Vigilância | a **origem do cone** — *"cada casco é nova origem"* | perde — fundir **apaga uma origem** |
| **Logística** | o **estoque** | **ganha** — a média ponderada conserva tudo |

**Só dois papéis fundem**, e por razões diferentes: no Capturador o HP **é** a
taxa, então concentrar acelera; na Logística o estoque é **conservado** na fusão,
então o casco novo dura mais sem perder nada. Os outros quatro perdem algo
insubstituível — uma vaga, uma arma, um nó da malha, um pedaço de área.

---

## 8. O que este documento NÃO cobre

- **Variações de papel** (degrau 4): `CapturadorCombatente` e parentes viram
  perfil/trait depois da extração das linhas. Ver `docs/revisao_papeis.md`.
- **A política de cada célula.** Esta é a forma da ficha, não o conteúdo. O que
  um Artilheiro faz na linha `combat` mora em `docs/AI Behavior/` por papel.
- **Plano × sem plano.** É eixo do **organizador**, e hoje o curto-circuito
  rebelde existe **só para captura** (`Router.cs:107`). As outras categorias não
  têm rebelde: caem no `plan != null` e simplesmente não rodam para uma facção
  sem QG.

---

## 9. Leituras

| documento | por quê |
|---|---|
| `CLAUDE.md`, "A skill is a key" | por que o gate é o sensor e não a etiqueta |
| `CLAUDE.md`, "As três camadas" | serviço burro / consumidor / organizador |
| `contrato_missao_captura.md` | a linha completa da matriz, e as condições de baixa |
| `contrato_envelope_alcance.md` | banda, âncora e camada são parâmetro da unidade avaliada |
| `docs/revisao_papeis.md` | a matriz de papéis e a taxonomia que ainda não foi extraída |
