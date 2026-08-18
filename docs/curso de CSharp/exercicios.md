# Exercícios — gabarito

Tente antes de ler. A resposta lida sem tentativa não fixa nada; a resposta lida
**depois** de uma tentativa errada fixa muito.

Alguns exercícios são abertos e não têm gabarito fechado — nesses, o que está
aqui é o critério para você julgar a própria resposta.

---

## Aula 1 — Anatomia de um arquivo

### E1 — leitura de `QuadranteData`

**1.** `bakedTiles` com 272 itens, `width = 16`, `height = 17`:

```text
CellCount = 16 × 17 = 272
HasBake   = 16>0 ✓  ∧  17>0 ✓  ∧  não-nulo ✓  ∧  272 == 272 ✓   →  TRUE
```

Com `width = 17`: `CellCount = 17 × 17 = 289`, e `272 != 289` → **`false`**.

E repare no que isso significa na prática: **mudar `width` no Inspector invalida
o bake inteiro**, sem apagar nada. O `HasBake` passa a `false`, o quadrante para
de pintar, e não há erro nenhum. É exatamente o comportamento certo — o bake é um
artefato do retângulo, e o retângulo mudou.

**2.** `GetBakedTile(15, 16)` num 16×17:

```text
guarda:  15 < 16 ✓    16 < 17 ✓     passa
índice:  (localY * width) + localX  =  (16 × 16) + 15  =  271
```

271 é o **último** índice de uma lista de 272 (que vai de 0 a 271). Ou seja,
`(15, 16)` é o canto oposto à origem. Confere.

**3.** `ContainsCampaignCell` com `<` no limite superior:

```text
com <     originX .. originX + width - 1     →  exatamente 'width' células ✓
com <=    originX .. originX + width         →  width + 1 células ✗
```

Com `<=`, o quadrante reivindicaria **uma coluna e uma linha a mais** do que
possui — e essa coluna pertence ao quadrante vizinho. Duas células compartilhadas
na borda.

Isso é a armadilha do `resumo.md`, literal: *"construção na interseção de
quadrantes — a faixa é para o chão. Peça ali nasce nos dois."* Um `<=` aqui
criaria essa interseção **em todo quadrante do mapa**, e o sintoma seria uma
fileira de peças duplicadas na borda. Sem erro.

> Metade dos bugs de grade é um `<` que virou `<=`. A outra metade é um índice
> que começou em 1.

### E2 — a propriedade `BakeIdade`

```csharp
/// <summary>Dias desde o bake. -1 quando nunca foi assado.</summary>
public double BakeIdadeDias => bakedAtUtcTicks <= 0
    ? -1d
    : (System.DateTime.UtcNow - new System.DateTime(bakedAtUtcTicks)).TotalDays;
```

Três coisas para conferir na sua resposta:

- **`bakedAtUtcTicks <= 0`** — a guarda. Sem ela, `new DateTime(0)` dá o ano 1 e
  a propriedade devolve ~740.000 dias. Não é erro; é resposta absurda, que é pior.
- **`-1` como sentinela.** Aceitável e comum. `double?` (nullable) seria mais
  honesto — mas veja o diagnóstico: você usa nullable em 6 arquivos, e o projeto
  não tem esse hábito. Coerência com o entorno vale mais que pureza.
- **É propriedade, não campo.** Tem de ser: o valor muda com o tempo. Um campo
  guardaria a idade do momento em que foi calculado, e envelheceria errado.

Se a sua versão usou `{ get { … } }` em vez de `=>`: idêntico. O `=>` é só forma
curta.

### E3 — outra classe `[System.Serializable]`

Em [Assets/Scripts/Campanha/](Assets/Scripts/Campanha/) há quatro:
`BlocoData`, `CampanhaData`, `ConstrucaoAssada` e `CamadaAssada` — esta última
com uma classe aninhada, também `[System.Serializable]`.

**Onde os dados ficam gravados:** dentro de `MundoData`, que é o único
`ScriptableObject` da pasta. Ou seja, em `Assets/DB/Campanha/Mundo Fixture.asset`.

A cadeia inteira, e vale desenhar porque explica o projeto:

```text
Mundo Fixture.asset          ← UM arquivo em disco
  └─ MundoData               ← ScriptableObject
      └─ List<BlocoData>            [System.Serializable]
          └─ List<CampanhaData>     [System.Serializable]
              └─ List<QuadranteData>[System.Serializable]
                  ├─ List<TileBase>
                  ├─ List<ConstrucaoAssada>
                  └─ List<CamadaAssada>
```

**Um arquivo, cinco níveis de classe.** Só o topo é `ScriptableObject`; todo o
resto são dados puros aninhados.

É por isso que a armadilha `feedback_asset_disk_editor_collision` é tão cara aqui:
editar esse `.asset` no disco não mexe num pedaço — mexe na árvore inteira do
mundo.

---

## Aula 2 — Valor vs referência

### E4 — o `HashSet` com `z` sujo

**Imprime `2`.**

```text
a = (3,5,1)      →  Add  →  conjunto: { (3,5,1) }
a.z = 0          →  a agora é (3,5,0) — struct, mudou a cópia local
a = (3,5,0)      →  Add  →  conjunto: { (3,5,1), (3,5,0) }   ← DUAS entradas
```

**Por que isso é o bug que a convenção previne:** as duas entradas são o **mesmo
hexágono do tabuleiro**, contado duas vezes. Num BFS, o hex é visitado duas vezes
e conta passos por dois caminhos; numa contagem de células reveladas, o total
infla; numa chave de dicionário, você guarda em uma e lê da outra.

E — o que torna isso perigoso — **em nenhum momento há erro**. O programa está
fazendo exatamente o que foi mandado: `(3,5,0)` e `(3,5,1)` *são* diferentes. Só
que não são, no seu jogo.

### E5 — `Vector3Int` como `class`

Quatro pontos de quebra em
[HexCoordinates.cs](Assets/Scripts/Hex/Core/HexCoordinates.cs), do mais sutil ao
mais fatal:

**Linhas 30-31** — `cellA.z = 0; cellB.z = 0;`
Deixariam de ser locais. Quem chamou `IsWithinRange` teria as próprias variáveis
zeradas pelas costas — efeito colateral invisível na assinatura do método.

**Linhas 55-56** — `Vector3Int n = neighbors[i]; n.z = 0;`
Pior: `n` seria apelido do item **dentro do buffer `neighbors`**, que é
reaproveitado a cada iteração e preenchido por
`GetImmediateHexNeighbors`. Você estaria escrevendo no buffer de outro sistema.

**Linha 58** — `visited.Contains(n)`
Sem `Equals`/`GetHashCode` sobrescritos, classes comparam por **identidade de
objeto**, não por conteúdo. Duas células `(4,7,0)` distintas na memória nunca
seriam "a mesma". O `Contains` daria sempre `false`, `visited` cresceria sem
limite e o BFS revisitaria tudo, várias vezes.

**Linha 60** — `if (n == cellB) return true;`
Mesma causa, consequência fatal: comparação por referência, **nunca** verdadeira.
O método passaria a devolver `false` sempre, para qualquer entrada.

> Repare no arco: as duas primeiras dão efeito colateral, a terceira dá lentidão
> catastrófica, e a quarta faz o método **sempre mentir**. E nenhuma delas lança
> exceção. É o resumo da aula 2 em quatro linhas.

### E6 — julgar um `struct`

Aberto. Critério para se auto-avaliar — sua justificativa precisa citar
**identidade**, não tamanho:

```text
✅ "duas instâncias com os mesmos campos são a mesma coisa"     → struct
❌ "é pequeno, então struct"                                     → insuficiente
```

Tamanho é critério secundário (a partir de ~16 bytes, a cópia começa a custar
mais que o ponteiro). Identidade é o critério primário.

E um sinal de alarme: **se o `struct` tem campo público que alguém modifica**, ele
provavelmente deveria ser `class` — ou, melhor, deveria ser imutável. Struct
mutável é onde a cópia silenciosa vira bug, e é a origem do E4.

---

## Aula 3 — `partial` e navegação

### E7 — achar a decisão de compra de transporte

**Caminho esperado:** `Ctrl+T` → `AIShoppingPlanner` → `Decide`. De lá,
`MinDistanceForTransportSlot` leva ao portão. O `CLAUDE.md` avisa que há **dois**
sistemas paralelos de demanda (memória `project_dual_transport_demand`) — se você
achou só um, achou metade, e a outra metade é a parte interessante.

**Autoavaliação, e é a parte que importa mais que a resposta:**

```text
< 2 min   você já sabe navegar. Pule pro E9.
2-10 min  normal na primeira vez. Repita com outro alvo.
> 10 min  você provavelmente leu arquivos inteiros em vez de usar Ctrl+T e F12.
          Refaça usando SÓ as quatro técnicas.
```

O erro clássico aqui é abrir um arquivo e ler de cima a baixo. Não leia a classe.
Leia **um caminho** dentro dela.

### E8 — `Shift+F12` em `IARapida`

Aberto. O que a propriedade controla está no próprio `[Tooltip]` de
[AIController.cs:50](Assets/Scripts/Match/AI/AIController.cs):

> *"Quando ligada, a IA executa batches sem sustentar menus e confirmações
> intermediárias. Desligue para apresentar no cursor e no panel_helper as mesmas
> etapas vistas pelo jogador."*

Com `false`, a IA joga **pelo caminho do jogador humano** — passando pelos mesmos
estados de cursor e painel. É a sua ferramenta de depuração mais forte para
qualquer bug de ação transacional, porque expõe cada estado intermediário que o
modo rápido pula.

Se você não sabia disso, esse foi o achado mais valioso do exercício.

### E9 — a frase de um arquivo

Aberto. O critério é o rigor da frase:

```text
✅ "o capturador deixa de ceder o hex para outro capturador que precisa passar"
❌ "deixa de fazer umas coisas de captura"
```

Se você não conseguiu a frase precisa, há duas causas possíveis, e vale
distinguir:

1. **você ainda não conhece aquele comportamento** — normal, e o remédio é ler os
   nomes dos métodos de novo, devagar;
2. **o arquivo não tem um assunto só** — e aí é achado real sobre o código.

Anote qual dos dois foi. O segundo caso é material de refatoração futura.

---

## Aula 4 — Coleções

### E10 — `HashSet` → `List` em `visited`

Três respostas diferentes, e a graça está em elas divergirem:

**Compila?** **Sim.** `new List<Vector3Int> { cellA }` é inicializador de coleção
válido, e `Contains`/`Add` existem nos dois tipos. Nenhum erro.

**Funciona?** **Sim.** `List.Contains` usa `Equals`, e `Vector3Int` é struct com
comparação por conteúdo. O resultado é idêntico.

**Quanto mais lento?** Num raio 5 são ~91 células e ~540 chamadas a `Contains`.

```text
HashSet   ~540 buscas de tempo constante
List      cada busca varre o que já entrou: ~1+2+…+91 × 6  ≈  25.000 comparações
```

Ordem de **40-50×** mais trabalho de comparação. E o fator **cresce com o
alcance** — dobrar o raio quadruplica as células e multiplica o custo do `List`
por ~16, enquanto o `HashSet` só quadruplica.

> É o modo de falha mais traiçoeiro em performance: rápido no teste pequeno,
> intragável no mapa grande. É por isso que a memória
> `project_planner_requires_sector_mapping` fala de ~60 s por chamada.

### E11 — `Queue` → `Stack`

Vira busca em profundidade, e ela quebra por causa de uma linha específica: o
`visited.Add(n)` acontece **na descoberta**, não na expansão. Um hex marcado com
uma contagem de passos alta nunca é reconsiderado com uma contagem menor.

Um caso concreto, com `maxRange = 3` e `cellB` a distância real 3:

```text
1. cellA expande: os 6 vizinhos entram com steps=1
2. LIFO tira o ÚLTIMO, X (anel 1). Expande: vizinhos ganham steps=2
3. LIFO tira Y (anel 2). Expande: vizinhos ganham steps=3
   — e entre eles há Z, que está de fato no ANEL 2, mas foi marcado com steps=3
4. quando Z sai da pilha: steps(3) >= maxRange(3)  →  NÃO expande
5. os vizinhos de Z que só chegam por ali nunca são descobertos
6. se cellB for um deles  →  retorna FALSE, estando a 3 de distância
```

Com `Queue`, o passo 3 não acontece: o anel 2 inteiro é processado antes de
qualquer nó de passo 3, então nenhum hex recebe contagem maior que a real.

**A garantia do BFS não é velocidade — é que a primeira vez que você toca um hex
é pelo caminho mais curto.** `Stack` destrói exatamente essa garantia.

### E12 — `ContainsKey` seguido de indexador

Aberto — é um levantamento. Ao classificar, separe:

```text
padrão    if (d.ContainsKey(k)) { var v = d[k]; … }      →  troque por TryGetValue
legítimo  if (!d.ContainsKey(k)) d[k] = novo;            →  só testa presença, ok
legítimo  if (d.ContainsKey(k)) continue;                →  idem
```

Só o primeiro é busca dupla. E o ganho é modesto em desempenho — a razão real
para trocar é **legibilidade**: `TryGetValue` diz "quero o valor se houver" numa
linha só.

Não mexa em caminho quente sem medir antes. Vale a aula 9.

---

## Aula 5 — LINQ

### E13 — `Sum` no `AIWorldSnapshot`

```csharp
int myHp    = snap.MyUnits.Sum(u => u.CurrentHP);
int enemyHp = snap.EnemyUnits.Sum(u => u.CurrentHP);
```

Sim, precisa de `using System.Linq;` no topo — o arquivo não tem hoje.

**É seguro?** Sim, e o raciocínio é o que interessa:

O `CLAUDE.md` diz que o snapshot é construído *"fresh at the start of each phase-2
iteration"* — ou seja, **uma vez por unidade que age**, não uma vez por turno. Com
20 unidades, ~20 construções por turno.

Ainda assim é seguro, por dois motivos:

- a lambda `u => u.CurrentHP` **não captura nada** do escopo externo. O compilador
  C# guarda uma instância estática dela e reusa — zero alocação por chamada.
- resta a alocação do enumerador que `Sum` cria ao percorrer a `List`. É um objeto
  minúsculo, ~40 por turno.

Quarenta objetos pequenos por turno, num jogo por turnos, é nada. **Se o mesmo
código estivesse dentro do BFS, a conta seria outra** — e é por isso que a
resposta certa aqui é "depende de quantas vezes roda", não "LINQ é bom" nem "LINQ
é ruim".

Se você respondeu "roda 1× por turno", releu o `CLAUDE.md` rápido demais. A frase
tem "each phase-2 iteration" dentro.

### E14 — onde mora `ZerarZ`

```csharp
public static Vector3Int ZerarZ(Vector3Int cell)
{
    cell.z = 0;
    return cell;
}
```

(Funciona porque `Vector3Int` é struct — o parâmetro é cópia. Aula 2.)

**Onde mora:** em [HexCoordinates.cs](Assets/Scripts/Hex/Core/HexCoordinates.cs),
ou noutro utilitário de hex do mesmo nível.

O critério é a tabela de camadas do `CLAUDE.md`:

```text
serviço burro   recebe um dado, devolve um dado. Sem política, sem contexto. ← aqui
consumidor      agrega, cruza, ranqueia
organizador     decide com política própria
```

`ZerarZ` não conhece unidade, time, objetivo nem alcance. É **serviço burro** no
sentido mais literal possível. Em `AIWorldSnapshot` ela estaria presa à IA, e o
mesmo `cell.z = 0` seria reescrito à mão nos outros 130 arquivos que precisam
dele — que, aliás, é o que acontece hoje.

**Bônus honesto:** essa função *deveria* existir e não existe. O padrão
`cell.z = 0` está repetido à mão por todo o projeto. Extraí-la é uma refatoração
real, pequena e segura — mas mexe em muito arquivo, então é conversa para um dia
de fechamento, não para um exercício.

### E15 — execução preguiçosa

**Imprime `2`.**

```text
nums = {1,2,3}
pares = nums.Where(par)     ← NADA executa. É uma promessa.
nums.Add(4)                 ← nums = {1,2,3,4}
pares.Count()               ← EXECUTA AGORA, sobre {1,2,3,4}  →  {2,4}  →  2
```

Quem respondeu `1` supôs que o `Where` tirou uma foto no momento da chamada. Ele
não tira. Ele guarda a **fonte** e o **critério**, e só percorre quando alguém
pede.

Isso corta nos dois sentidos, e o segundo é o que morde de verdade: se o `Add`
acontecesse **durante** o `foreach`, a `List` lançaria
`InvalidOperationException: Collection was modified`. Aqui não, porque a alteração
foi antes de a enumeração começar.

**A regra prática que evita os dois casos:** feche a consulta com `.ToList()`
assim que tiver o que quer. Aí vira dado, não promessa.

---

## Aula 6 — `ScriptableObject` e ciclo de vida

### E16 — `SkillData`

**1. Por que `ScriptableObject` e não `[System.Serializable]`?**

Porque uma skill precisa ser **apontada de vários lugares independentes**:
`TerrainTypeData.requiredSkillsToEnter`, `ConstructionData.blockedSkills`,
`StructureData.forceDetectUnitsWith…`, e as fichas de unidade. Um
`ScriptableObject` tem arquivo próprio e identidade — todos apontam para a mesma
instância, e a comparação "é esta skill?" é comparação de referência.

Se fosse `[System.Serializable]`, cada dono guardaria a **própria cópia** (aula 2:
dado puro mora dentro do dono). Trinta cópias de "alpino", cada uma se achando a
verdadeira, e nenhuma forma de perguntar se duas são a mesma.

**2. Por que `canCaptureConstructions` na skill era pior que na construção?**

Duas razões, e a segunda é a que o `CLAUDE.md` transformou em doutrina:

**Compartilhamento.** A skill é uma instância só, lida por todo mundo. Um campo de
poder ali é uma regra **global**, imutável por contexto. Em
`ConstructionData.requiredSkillsToCapture`, cada construção responde por si: a
cidade aceita infantaria, o aeroporto exige outra coisa, o QG exige outra. O poder
no alvo dá N respostas; na chave, dá uma.

**A etiqueta parou de ser renomeável.** É o teste do `CLAUDE.md`: *"o designer
consegue renomear esta skill para qualquer coisa e tudo continua funcionando?"*.
Com `canCaptureConstructions` dentro dela, o nome deixou de ser um rótulo e virou
um contrato — havia código que dependia daquela skill específica **existir**. É
por isso que a exceção saiu na v7.0.2, e é a razão de o `SkillData` hoje ter 33
linhas, das quais 17 são o comentário explicando isso.

### E17 — pares de assinatura

Aberto — é um levantamento. Formato:

| arquivo | assina em | desassina em | par fechado? |
|---|---|---|---|
| `ConstructionManager.cs` | | | |
| `CursorController.cs` | | | |
| `ConfirmedOccupancyIndex.cs` | | | |
| `TurnStateManager.cs` | | | |
| `AutomatedPlayer.cs` | | | |
| `ReplayManager.cs` | | | |
| `HexCohabitationVisualManager.cs` | | | |

Duas observações antes de você julgar um caso como errado:

- `CursorController` é quem **declara** o evento. Se ele aparecer na busca só pela
  declaração e pelo `?.Invoke`, não é assinante — não precisa de par.
- Um objeto que existe pela partida inteira e nunca é desativado *funciona* sem o
  `-=`. Funciona hoje. Ele quebra no dia em que a cena trocar — e o bloqueio 0b do
  `resumo.md` diz que esse dia está marcado.

Ou seja: **"funciona" e "está certo" não são a mesma coisa aqui.** Anote os
abertos como dívida com gatilho conhecido, não como bug.

### E18 — caça ao layout em catálogo

Aberto. O procedimento, campo a campo:

```text
"se eu fizer um segundo mapa, este valor é o mesmo?"

MESMO  →  custo, HP, alcance, cor, sprite, chaves exigidas     ✅ catálogo
MUDA   →  qualquer coordenada, qualquer lista de células,
          qualquer "quantos existem neste mapa"                ❌ layout vazado
```

O sinal mais forte é o tipo do campo: **`Vector3Int`, `List<Vector3Int>` ou
qualquer coisa com "cell", "route", "entries" dentro de um `ScriptableObject` é
suspeito até prova em contrário.**

Se achar um terceiro além de `fieldEntries` e `roadRoutes`, é achado novo e vale
uma conversa antes de mexer — o `resumo.md` avisa que apagar dado do autor é
sempre pergunta, nunca ação.

---

## Aula 7 — Eventos

### E19, E20, E21 — abertos

**E19.** Espere que `OnFogOfWarUpdated` e `OnActiveTeamChanged` sejam os mais
escutados, e faz sentido: névoa e troca de time mudam quase tudo que é visível.
Se algum evento tiver **zero** assinantes, anote — é um `?.Invoke` que não causa
nada, e ou falta um consumidor ou sobra um evento. (O `resumo.md` já registra um
caso desses noutro contexto: *"`BoardReady` não tem leitor."*)

**E20.** O critério é você conseguir escrever a cadeia sem reabrir os arquivos.
Se precisou reabrir, refaça com outro evento menor — a habilidade é reter o
caminho, não descobri-lo.

**E21.** O caso que interessa é um assinante `DontDestroyOnLoad`. O `CLAUDE.md`
avisa:

> *a manager that is `DontDestroyOnLoad` **with no `sceneLoaded` hook** carries
> the previous match's state into the next map.*

Se um objeto assim assina um evento estático, ele atravessa a troca de cena
**assinado e vivo** — que é o oposto do problema do `MissingReferenceException`, e
igualmente ruim: ele vai reagir a eventos do mapa novo com estado do mapa velho.

Achou um? É exatamente o material do bloqueio 0b. Anote o nome do manager.

---

## Aula 8 — Corrotinas

### E22 e E23 — abertos

**E22.** No trecho 14-75 há `yield` de três naturezas, e a classificação importa
mais que a contagem:

```text
yield break                 saída — não é pausa
yield return null           pausa de exatamente 1 frame
yield return OutraCorrotina espera indefinida — pode ser MUITOS frames
```

O terceiro tipo é o perigoso. Depois de `yield return CommitAIWorldHeavy(…)`,
podem ter passado segundos: unidade morta, partida encerrada, jogador pausou. É
por isso que `ShouldStopAIForMatchEnd` reaparece com etiqueta diferente a cada
etapa.

**E23.** A resposta está nas linhas 54-56, e reescrevê-la com suas palavras é o
exercício. O núcleo:

```text
caminho normal    Stage0 espera o mundo assentar antes de commitar
caminho de save   Stage0 foi PULADO (o save diz que já rodou),
                  mas as corrotinas do próprio carregamento ainda estão no ar
```

Sem `WaitForResumeSettleTelemetry()`, o `CommitAIWorldHeavy` rodaria sobre um
mundo ainda se montando — e cairia direto na armadilha do `resumo.md`:
*"consulta antes da pintura terminar — o `SectorManager` assa o vazio e cacheia.
Daí o `-9000`."*

**É o mesmo modo de falha do prédio preto do `CLAUDE.md`**, noutro sistema: foto
tirada no meio do movimento, e cacheada.

### E24 — `StartCoroutine`

**Sem `StartCoroutine`, imprime `1` e `2`.**

Chamar `MinhaCorrotina()` cria o objeto `IEnumerator` e **descarta**. O corpo não
executa nem uma linha — nem o `Debug.Log("3")`, que está antes de qualquer
`yield`. Nenhum erro, nenhum aviso.

> Este é o erro nº 1 de quem começa com corrotina, e o pior é o sintoma: "meu
> método não roda e não dá erro". Alguns compiladores avisam; o da Unity, não
> necessariamente.

**Com `StartCoroutine(MinhaCorrotina())`, imprime `1`, `3`, `2` — e `4` no frame
seguinte.**

A ordem surpreende, e é a chave para entender corrotina:

```text
Debug.Log("1")                    → 1
StartCoroutine(…)                 → executa o corpo IMEDIATAMENTE, na hora,
                                     até o primeiro yield        → 3
                                     (yield return null: devolve o controle)
Debug.Log("2")                    → 2      ← o Start CONTINUA
── fim do frame ──
                                     a corrotina retoma           → 4
```

`StartCoroutine` **não** agenda para depois. Ele roda o começo agora, síncrono, e
só solta o controle no primeiro `yield`.

---

## Aulas 9 e 10 — abertos

**E25.** O ponto não é consertar o erro — é ler o stack trace **antes**. Se o
duplo clique levou à linha errada, provavelmente você clicou numa linha do meio
do trace em vez da primeira com arquivo seu.

**E26.** Se o log que você achou é de uma unidade ou construção específica,
aproveite e acrescente o segundo parâmetro: `Debug.Log(msg, unidade.gameObject)`.
Log clicável que seleciona o objeto na Hierarchy paga o hábito na primeira vez que
você usa.

**E27.** A pergunta final é a que importa: *era o que você esperava?* Anote o
número **antes** de olhar o resultado, depois compare. É assim que se calibra
intuição — e a memória `feedback_perf_measure_dont_deduce` existe porque a
intuição erra com frequência incômoda.

**E28.** Espere achar mais `SetDirty` que `RecordObject`. Nem todo caso é
problema:

```text
legítimo   escrita derivada, logo depois de outra já registrada no undo
suspeito   modificação que o usuário fez por clique e não pode desfazer
```

O teste: **se um clique seu mudar o dado e o `Ctrl+Z` não desfizer**, falta
`RecordObject`. Vale testar na mão em vez de deduzir pelo código.

**E29.** Botão de leitura pura, sem `SetDirty` nenhum — porque não modifica nada.
Se você sentiu vontade de chamar `SetDirty` "por garantia", releia a aula 10: ele
marca o asset como sujo, e um asset marcado sujo é regravado. `SetDirty` sem
modificação é exatamente o hábito que produz churn no `git status`.

**E30.** Não há gabarito — é o plano que a gente vai discutir. Mas há um critério
de qualidade, e ele é o do `resumo.md`:

> *Perguntar por que uma coisa existe antes de construir em cima dela.*

Antes de propor a `UnidadeAssada`, responda: **por que `ConstrucaoAssada` tem os
campos que tem?** Se você souber justificar cada campo dela, o desenho da unidade
sai quase sozinho — e você vai reparar sozinho no que uma unidade tem que uma
construção não tem (time, HP atual, carga, estado de ação).

E a armadilha específica desta frente, do `resumo.md`: *"generalizar do que se
encontra sem checar a razão — quatro vezes em dois dias"*. `ConstrucaoAssada` é um
bom molde. Não é uma lei.

---

## Depois dos exercícios

Se você chegou até aqui tendo feito E7, E17, E20 e E30, o objetivo do curso está
cumprido: você navegou o código sozinho, auditou um padrão em sete arquivos,
seguiu uma causa de ponta a ponta e desenhou uma frente nova.

O que **não** se aprende lendo, e só vem com repetição:

- estimar quanto uma mudança vai vazar (`Shift+F12` antes de tocar, sempre)
- saber quando parar de investigar e medir (aula 9)
- desconfiar de "funciona" (aula 7, E17)

Volte às aulas 3, 6 e 7 daqui a algumas semanas. Elas rendem muito mais na segunda
leitura, porque aí você já terá encontrado os problemas de que elas falam.
