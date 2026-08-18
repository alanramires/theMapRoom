# 02 — Valor vs referência, e por que `cell.z = 0`

> **Meta da aula:** entender a distinção que mais causa bug silencioso em C#, e
> descobrir que seu código já depende dela em centenas de lugares.

---

## A distinção

C# tem duas famílias de tipo, e elas se comportam de forma diferente quando você
**atribui** ou **passa para um método**.

| | tipo de **valor** (`struct`) | tipo de **referência** (`class`) |
|---|---|---|
| exemplos | `int`, `float`, `bool`, `enum`, `Vector3Int`, `Vector3` | `string`, `List<T>`, `UnitData`, todo `MonoBehaviour` |
| `b = a` faz | **cópia** | **apelido** — os dois apontam pro mesmo objeto |
| pode ser `null`? | não (salvo `int?`) | sim, e é o `NullReferenceException` |
| mora | na pilha, ou embutido no dono | no heap |

A linha que importa:

```csharp
// VALOR — cópia
Vector3Int a = new Vector3Int(1, 2, 0);
Vector3Int b = a;
b.x = 99;
// a.x continua 1.   São dois objetos.

// REFERÊNCIA — apelido
List<int> x = new List<int> { 1, 2, 3 };
List<int> y = x;
y.Add(4);
// x.Count agora é 4.   É a MESMA lista, com dois nomes.
```

Não é sutileza acadêmica. É a diferença entre "mudei minha cópia" e "mudei a
coisa do outro".

---

## No seu jogo

### `Vector3Int` é `struct` — e o seu código conta com isso

Abra [Assets/Scripts/Hex/Core/HexCoordinates.cs](Assets/Scripts/Hex/Core/HexCoordinates.cs):

```csharp
// linha 25
public static bool IsWithinRange(Tilemap tilemap, Vector3Int cellA, Vector3Int cellB, int maxRange)
{
    if (tilemap == null || maxRange < 0)
        return false;

    cellA.z = 0;   // linha 30
    cellB.z = 0;
```

Olhe bem para a linha 30. Ela **modifica um parâmetro**.

Se `Vector3Int` fosse `class`, isso seria um efeito colateral grave: quem chamou
`IsWithinRange` teria a própria variável zerada pelas costas, e passaria o resto
da execução com um `z` que ele não zerou.

Mas `Vector3Int` é `struct`. Então `cellA` dentro do método é uma **cópia** do que
o chamador passou. Zerar o `z` dela é local, descartável, invisível de fora.

> **A linha 30 é segura por causa do tipo, não por causa de cuidado.** Troque
> `Vector3Int` por uma classe e o mesmo código vira bug em todos os chamadores.

### `cell.z = 0` — por que existe em todo canto

O `CLAUDE.md` traz isso como convenção obrigatória:

> *All cell positions: zero out `z` before comparisons (`cell.z = 0`).*

A razão é a mesma distinção, do outro lado. `Vector3Int` é `struct`, e structs se
comparam **por conteúdo, campo a campo** — os três campos, incluindo o `z`.

```csharp
new Vector3Int(4, 7, 0) == new Vector3Int(4, 7, 0)   // true
new Vector3Int(4, 7, 0) == new Vector3Int(4, 7, 1)   // FALSE
```

Duas células no mesmo hex, `false`, sem erro nenhum. E como `HashSet<Vector3Int>`
e `Dictionary<Vector3Int, …>` usam o mesmo `==` por baixo, um `z` sujo faz a
mesma célula entrar **duas vezes** no conjunto.

O `z` do tilemap da Unity não significa nada no seu jogo — ele existe porque a
API é 3D. Então ele é lixo que precisa ser zerado antes de qualquer comparação,
`Contains`, ou uso como chave.

Repare no que a linha 57 do mesmo arquivo faz:

```csharp
Vector3Int n = neighbors[i];
n.z = 0;
if (visited.Contains(n))
```

Zera **antes** do `Contains`. Se zerasse depois, o `visited` acumularia
duplicatas e o BFS visitaria o mesmo hex várias vezes — mais lento, e com
`maxRange` potencialmente errado. De novo: sem erro, sem log. Só resultado
errado.

> **O modo de falha de tipo de valor não é exceção. É resposta errada em
> silêncio.** Ao contrário do `null`, que grita.

### `string` é referência, mas finge ser valor

```csharp
public string quadranteId = "Q1";
```

`string` é `class` — logo pode ser `null`, e é o `null` que mais aparece nos seus
crashes. Mas ela é **imutável**: nenhum método de `string` altera a original, todos
devolvem uma nova. Por isso `a = b` em string parece cópia mesmo sendo apelido —
como ninguém consegue mudar o objeto, a distinção some na prática.

Consequência prática, e é a que te custa performance:

```csharp
texto += "algo";     // NÃO altera 'texto'. Cria uma string nova e reaponta.
```

Dentro de um laço de 200 iterações isso são 200 strings jogadas fora. É
exatamente por isso que você tem `StringBuilder` em **44 arquivos** — alguém (ou
você) já sentiu isso.

### `enum` é valor, e o `default` te mordeu

O `resumo.md` lista, entre as armadilhas:

> *`ConstructionSector` default — é `Alpha = 0`, não `None = -1`. Esquecer o setor
> não dá erro: dá plano degenerado.*

Isso é tipo de valor puro. `enum` é `int` com nomes, é struct, **não pode ser
`null`**, e um campo não inicializado vale `0`. Se `Alpha = 0`, todo setor
esquecido *é* Alpha, e o planner recebe um mapa onde tudo é Alpha sem que ninguém
tenha errado uma linha.

A defesa, quando você puder pagá-la, é fazer o valor `0` significar "não
preenchido":

```csharp
public enum ConstructionSector
{
    None = 0,      // ← o default vira "esqueci", e dá pra detectar
    Alpha,
    Bravo,
    // ...
}
```

Não estou pedindo que mude — mexer nisso hoje reserializa todos os assets do
projeto, e a ordem numérica atual está gravada em disco. **É uma decisão de
dados, não de código**, e cara. Mas agora você sabe *por que* o bug existe, e não
só *que* ele existe.

---

## `null`, e a exceção que você mais vê

`NullReferenceException` significa uma coisa só: *você chamou algo em cima de uma
referência que não aponta pra objeto nenhum.*

As três origens, em ordem de frequência no seu tipo de código:

| origem | exemplo |
|---|---|
| campo `[SerializeField]` não arrastado no Inspector | o `ConstructionSpawner` da `Batalha` sem catálogo — está no seu `resumo.md` |
| coleção declarada sem `new` | `public List<X> lista;` e depois `.Add()` |
| busca que não achou | `GetComponent<T>()` devolve `null` quando não existe |

Sua defesa padrão, e ela está certa, é a guarda:

```csharp
// HexCoordinates.cs:9
if (tilemap == null)
    return Vector3.zero;
```

Repare que ela devolve `Vector3.zero` em vez de explodir. É uma escolha, e tem
preço: some o crash, mas some também o aviso. Um `tilemap` nulo devolve
silenciosamente a origem do mundo, e o chamador segue achando que perguntou
certo.

> Para argumento que **nunca deveria** ser nulo, considere gritar em vez de
> engolir. A aula 9 mostra como, com `Debug.LogError` e `Assert`.

### Nullable — o `?` que você quase não usa

Tipos de valor não aceitam `null`. `int? idade = null` é legal e cria um
`Nullable<int>`. Você usa em 6 arquivos.

Não vou empurrar. Em código Unity, o padrão mais comum e mais legível para
"não achei" é o par `TryGet`:

```csharp
if (TerrainDatabase.TryGetByPaletteTile(tile, out TerrainTypeData terreno))
{
    // achou, e 'terreno' é válido AQUI DENTRO
}
```

Que é o padrão que seu código já adota. Está bom. Siga.

---

## Quando criar um `struct` seu

Você tem 23. A regra prática:

```text
struct   pequeno (≤ 16 bytes, uns 4 campos), imutável, sem identidade,
         comparado por conteúdo.       Ex.: uma coordenada, um par id+peso.

class    tem identidade ("esta unidade", não "uma unidade igual"),
         é grande, ou muda ao longo da vida.
```

O teste decisivo é **identidade**: duas unidades com HP 10 e mesma posição são a
*mesma* unidade? Não — são duas peças no tabuleiro. Logo, `class`. Duas
coordenadas `(4,7)` são a mesma coordenada? Sim. Logo, `struct`.

---

## Exercício

**E4.** Sem rodar: o que este código imprime?

```csharp
Vector3Int a = new Vector3Int(3, 5, 1);
HashSet<Vector3Int> visto = new HashSet<Vector3Int>();
visto.Add(a);

a.z = 0;
visto.Add(a);

Debug.Log(visto.Count);
```

Depois explique, em uma frase, por que isso é o bug que a convenção
`cell.z = 0` do `CLAUDE.md` previne.

**E5.** Abra [Assets/Scripts/Hex/Core/HexCoordinates.cs](Assets/Scripts/Hex/Core/HexCoordinates.cs)
e responda: se `Vector3Int` fosse uma `class`, quais linhas do método
`IsWithinRange` virariam bug? Liste os números de linha e diga o que quebraria
em cada uma.

**E6.** Escolha um dos 23 `struct` do projeto (procure por `public struct`) e
justifique em duas frases se ele deveria mesmo ser `struct`, usando o teste da
identidade. Se você concluir que deveria ser `class`, **não mude** — anote e
traga.

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [03 — Achar o arquivo certo](03_partial_e_navegacao.md). São 101
arquivos de `AIController`, e você vai aprender a mirar.
