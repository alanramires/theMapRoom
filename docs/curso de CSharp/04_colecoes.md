# 04 — Coleções

> **Meta da aula:** saber, olhando um problema, qual coleção escolher — e
> entender por que seu código tem `HashSet` em 136 arquivos e `Stack` em 2.

---

## As cinco que importam

| coleção | serve pra | "tem isso?" custa | mantém ordem |
|---|---|---|---|
| `List<T>` | sequência, você percorre | **lento** — varre tudo | sim, de inserção |
| `Dictionary<K,V>` | mapa chave → valor | **instantâneo** | não |
| `HashSet<T>` | conjunto, sem repetido | **instantâneo** | não |
| `Queue<T>` | fila: primeiro a entrar, primeiro a sair | lento | sim, FIFO |
| `Stack<T>` | pilha: último a entrar, primeiro a sair | lento | sim, LIFO |

A coluna do meio é a que decide quase tudo.

```text
List.Contains(x)         percorre item por item.   1000 itens = até 1000 testes.
HashSet.Contains(x)      calcula o hash e vai.     1000 itens = 1 teste.
Dictionary.TryGetValue   idem.                     1000 itens = 1 teste.
```

Numa lista de 10, irrelevante. Num BFS que testa "já visitei?" a cada vizinho de
cada célula de um mapa de 1800 hexes, é a diferença entre 2 ms e travar.

> **A regra que resolve 90% das escolhas:** se você vai perguntar *"isto está
> aí?"* mais de um punhado de vezes, não use `List`.

---

## No seu jogo: um BFS com três coleções, e cada uma pelo motivo certo

[Assets/Scripts/Hex/Core/HexCoordinates.cs:25](Assets/Scripts/Hex/Core/HexCoordinates.cs)
é uma aula inteira em 40 linhas. Abra.

```csharp
HashSet<Vector3Int> visited = new HashSet<Vector3Int> { cellA };
Queue<Vector3Int> frontier = new Queue<Vector3Int>();
Queue<int> steps = new Queue<int>();
frontier.Enqueue(cellA);
steps.Enqueue(0);

List<Vector3Int> neighbors = new List<Vector3Int>(6);
```

Quatro coleções, quatro papéis distintos:

### `visited` é `HashSet` — porque a pergunta é "já vi?"

```csharp
if (visited.Contains(n))
    continue;
visited.Add(n);
```

Essa pergunta roda **seis vezes por célula visitada**. Num raio 5 são umas 90
células, ~540 perguntas. Com `List`, cada pergunta varreria as até 90 já vistas:
~24 mil comparações. Com `HashSet`: 540. E o custo escala pior quanto maior o
alcance.

E tem o segundo papel, mais silencioso: `HashSet` **não deixa repetir**. Mesmo
que a lógica falhasse e tentasse adicionar duas vezes, o conjunto se protege
sozinho.

### `frontier` é `Queue` — porque BFS é FIFO, e é isso que garante a resposta

Essa escolha não é performance. É **correção**.

```text
Queue  (FIFO)  →  visita tudo do raio 1, depois tudo do raio 2, depois 3…
                  = BUSCA EM LARGURA. O primeiro caminho achado é o MAIS CURTO.

Stack  (LIFO)  →  mergulha fundo numa direção antes de voltar
                  = busca em profundidade. Acha um caminho. Não o menor.
```

Troque `Queue` por `Stack` nessas linhas e `IsWithinRange` passa a mentir: vai
responder `false` para células que estão dentro do alcance, porque estourou o
`maxRange` descendo por um caminho torto antes de tentar o reto.

> **`Queue` vs `Stack` não é gosto. É qual resposta você quer.** Menor caminho →
> `Queue`. É por isso que você tem 17 arquivos com `Queue` e 2 com `Stack`:
> praticamente todo `Queue` do seu projeto é pathfinding, e pathfinding quer o
> menor.

### Duas `Queue` paralelas — e a alternativa

```csharp
Queue<Vector3Int> frontier = new Queue<Vector3Int>();
Queue<int> steps = new Queue<int>();
```

Duas filas andando em sincronia: a célula e quantos passos custou chegar nela.
Funciona, e é rápido. O risco é humano — se alguém adicionar um `Enqueue` numa e
esquecer da outra, elas dessincronizam e o BFS conta passos errados **sem erro
nenhum**.

A alternativa seria uma fila só de um par:

```csharp
Queue<(Vector3Int cell, int steps)> frontier = new Queue<(Vector3Int, int)>();
// ...
frontier.Enqueue((cellA, 0));
var (current, currentSteps) = frontier.Dequeue();
```

Isso é uma **tupla nomeada** — um struct anônimo de dois campos. Impossível
dessincronizar, porque é um item só.

Não estou pedindo pra mudar esse arquivo; ele está correto e testado. Mas quando
você escrever o **próximo** BFS, use a tupla. É a mesma velocidade com uma classe
de bug a menos.

### `neighbors` é `List` — com um número dentro do `new`

```csharp
List<Vector3Int> neighbors = new List<Vector3Int>(6);
```

Aqui `List` é a escolha certa: você só percorre, nunca pergunta "contém". E o `6`
não é o tamanho — é a **capacidade inicial**.

Uma `List` guarda um array por dentro. Quando enche, aloca um array maior e copia
tudo. Como um hex tem no máximo 6 vizinhos, dizer `6` na criação significa **zero
realocações**, sempre.

Detalhe importante: repare que a lista é criada **fora** do `while`, e
`GetImmediateHexNeighbors` recebe ela pra preencher. Isso é reúso de buffer — em
vez de criar uma lista nova por célula visitada (e dar 90 listas pro coletor de
lixo), a mesma lista é limpa e reutilizada.

Esse padrão — **passar a coleção de saída como parâmetro em vez de retorná-la** —
está por todo o seu código de sensor:

```csharp
PodeMirarSensor.CollectTargets(unit, …, targets, fromCell)
PodeEmbarcarSensor.CollectOptions(unit, …, options)
```

É a técnica padrão de Unity pra não alimentar o GC. Você já usa. É maduro.

---

## `Dictionary` — a coleção mais usada do projeto (166 arquivos)

```csharp
Dictionary<Vector3Int, List<Vector3Int>> caminhos =
    UnitMovementPathRules.CalcularCaminhosValidos(boardTilemap, unit, mp, terrainDatabase);
```

Lê-se: *"dada uma célula de destino, me dê o caminho até ela"*. Chave `Vector3Int`,
valor `List<Vector3Int>`.

### As três formas de ler, e quando usar cada uma

```csharp
// 1. Direto — EXPLODE se a chave não existe
var caminho = caminhos[destino];

// 2. TryGetValue — a forma segura, e a que você deve usar
if (caminhos.TryGetValue(destino, out List<Vector3Int> caminho))
{
    // 'caminho' vale aqui dentro
}

// 3. ContainsKey + indexador — FAZ A BUSCA DUAS VEZES
if (caminhos.ContainsKey(destino))
    var caminho = caminhos[destino];
```

A forma 3 é a mais comum entre quem está aprendendo, e é sempre pior que a 2 —
mesmo resultado, dobro do trabalho. Se você achar `ContainsKey` seguido de
indexador no seu código, é candidato a limpeza segura.

O `out` na forma 2 é um parâmetro de **saída**: o método escreve nele. É o mesmo
`out` que o roadmap lista em 1.1 e você marcou como "sem prioridade" — mas você
já usa em toda parte via `TryGet*`. Não precisa estudar à parte; é isto aqui.

### A chave precisa ser confiável

`Dictionary` e `HashSet` acham as coisas por **hash** — um número derivado do
conteúdo da chave. Duas regras seguem disso:

1. **Se o conteúdo da chave muda depois de inserida, o item se perde.** Ele fica
   guardado no balde do hash antigo e nunca mais é encontrado. Por isso chave boa
   é imutável: `Vector3Int`, `string`, `int`, `enum`.
2. **`Vector3Int` inclui o `z` no hash.** É a aula 2 voltando: `(4,7,0)` e
   `(4,7,1)` são chaves **diferentes**. Zerar o `z` antes de usar como chave não é
   estilo — é o que faz o dicionário funcionar.

---

## `readonly` num campo de coleção (118 arquivos)

```csharp
// AIController.Phase2.cs:1216
private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();
```

`readonly` significa: *"a variável não pode apontar pra outro objeto depois da
construção"*. E só isso.

```csharp
Entries = new Dictionary<string, Entry>();   // ❌ proibido depois do construtor
Entries.Add("x", e);                          // ✅ permitido, e é o uso normal
Entries.Clear();                              // ✅ também permitido
```

> **`readonly` congela o apelido, não o conteúdo.** É a aula 2 de novo: a variável
> é uma referência, e `readonly` protege a referência.

Isso é exatamente o que você quer num cache estático: ninguém troca o dicionário
por outro pelas costas, mas todo mundo pode escrever nele.

E o contraste com `const` (96 arquivos):

```csharp
// AIController.PlanEvaluator.AnchorSectors.cs:6
private const int AnchorSectorRecoveryPriorityBonus = 90;
```

| | `const` | `readonly` |
|---|---|---|
| quando o valor é fixado | compilação | execução (construtor) |
| tipos aceitos | só primitivos e `string` | qualquer um |
| `const int x = 90` vira | o número **90** colado em todo lugar que usa | uma leitura de campo |

Por isso um `Dictionary` nunca pode ser `const` — ele não existe em tempo de
compilação. E por isso os pesos do planner são `const`: são números literais,
fixos, e o compilador os embute.

---

## `Stack` — por que só 2 no projeto inteiro

Pilha serve pra "desfazer a última coisa": undo, avaliar expressão aninhada,
percorrer árvore em profundidade.

Seu jogo raramente precisa disso, e onde precisaria — o Undo do editor — a Unity
já tem `Undo.RecordObject` (aula 10). Duas ocorrências é o número certo. Não é
lacuna.

`LinkedList` (1 ocorrência) é ainda mais nichado: só vale quando você insere e
remove **no meio** de uma sequência longa, muitas vezes. Se você nunca precisar
dele de novo, ótimo.

---

## Tabela de decisão

Cole isto na parede:

```text
vou perguntar "contém?"           →  HashSet
tenho chave e quero valor         →  Dictionary
BFS / menor caminho / fila        →  Queue
só percorro, ordem importa        →  List
tamanho fixo e conhecido          →  array T[]
undo / profundidade               →  Stack
```

E duas regras de higiene que seu código já segue:

- **Inicialize na declaração** (`= new List<T>()`), senão nasce `null`.
- **Diga a capacidade quando souber** (`new List<T>(6)`).

---

## Exercício

**E10.** Em [HexCoordinates.cs](Assets/Scripts/Hex/Core/HexCoordinates.cs), troque
mentalmente `HashSet<Vector3Int> visited` por `List<Vector3Int> visited`. O código
**compila**? Ele **funciona**? Fica mais lento em que proporção, num raio 5?

(As três respostas são diferentes. Essa é a graça da pergunta.)

**E11.** Ainda em `HexCoordinates`: troque mentalmente as duas `Queue` por
`Stack`. Descreva um caso concreto — `cellA`, `cellB`, `maxRange` — em que o
método passaria a devolver `false` estando errado.

**E12.** Busque no projeto (`Ctrl+Shift+F`) por `ContainsKey`. Para cada
ocorrência, veja se logo em seguida vem um acesso `[chave]`. Liste os arquivos e
linhas onde isso acontece — cada um é uma busca dupla que `TryGetValue`
eliminaria.

**Não corrija ainda.** Traga a lista; decidimos juntos o que vale mexer.

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [05 — LINQ](05_linq.md), e por que sua ausência quase total dele é meio
acerto e meio dívida.
