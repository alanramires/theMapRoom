# 05 — LINQ: o que você não usa, e por quê

> **Meta da aula:** entender a ferramenta que seu código quase ignora, e saber
> traçar a linha entre "aqui vale" e "aqui não".

Lembre do diagnóstico: **LINQ aparece em 2 arquivos de 460**, e os dois são
periféricos (`AI_Legacy~`, que a Unity nem compila, e `JogadasManager`). No
código vivo de gameplay, LINQ é ausente.

Isso é metade acerto e metade dívida. Vamos separar as metades.

---

## O que é

LINQ é um conjunto de métodos de extensão sobre coleções, que descrevem
**o que** você quer em vez de **como** obter.

```csharp
using System.Linq;

var vivos = unidades.Where(u => u.CurrentHP > 0).ToList();
```

Lê-se: *"das unidades, aquelas cujo HP é maior que zero, como lista"*.

O `u => u.CurrentHP > 0` é uma **lambda**: uma função sem nome. `u` é o
parâmetro, `=>` separa, e a direita é o corpo. É a mesma seta da propriedade de
corpo de expressão da aula 1, em outro papel.

### Os oito que resolvem quase tudo

| método | devolve | pergunta que responde |
|---|---|---|
| `Where(x => …)` | coleção filtrada | quais atendem? |
| `Select(x => …)` | coleção transformada | me dê só o campo Y de cada |
| `FirstOrDefault(x => …)` | um item, ou `null`/`0` | o primeiro que atende |
| `Any(x => …)` | `bool` | existe algum? |
| `All(x => …)` | `bool` | todos atendem? |
| `Count(x => …)` | `int` | quantos atendem? |
| `Sum(x => …)` | número | soma de quê |
| `OrderBy(x => …)` | coleção ordenada | ordenada por qual campo |

`FirstOrDefault` vs `First`: o segundo **lança exceção** se não achar nada. Em
código de gameplay, prefira `FirstOrDefault` e teste o `null`.

---

## No seu jogo: onde LINQ economizaria de verdade

Abra [Assets/Scripts/Match/AI/AIWorldSnapshot.cs:125](Assets/Scripts/Match/AI/AIWorldSnapshot.cs).
É o cálculo de stance, e é o melhor exemplo do projeto.

```csharp
Vector3Int hq = snap.MyHQ.CurrentCellPosition; hq.z = 0;
int enemyHpNearHQ = 0;
foreach (UnitManager enemy in snap.EnemyUnits)
{
    Vector3Int ec = enemy.CurrentCellPosition; ec.z = 0;
    if (ChebyshevDistance(hq, ec) <= 4) enemyHpNearHQ += enemy.CurrentHP;
}
if (enemyHpNearHQ > 0)
{
    int friendlyHpNearHQ = 0;
    foreach (UnitManager u in snap.MyUnits)
    {
        Vector3Int uc = u.CurrentCellPosition; uc.z = 0;
        if (ChebyshevDistance(hq, uc) <= 4) friendlyHpNearHQ += u.CurrentHP;
    }
    if (enemyHpNearHQ > friendlyHpNearHQ)
        return AIStance.Defensive;
}
```

Dois laços de cinco linhas, **estruturalmente idênticos**, diferindo só na
coleção. Em LINQ:

```csharp
int HpPertoDoHQ(List<UnitManager> unidades) => unidades
    .Where(u => ChebyshevDistance(hq, ZerarZ(u.CurrentCellPosition)) <= 4)
    .Sum(u => u.CurrentHP);

int enemyHpNearHQ    = HpPertoDoHQ(snap.EnemyUnits);
int friendlyHpNearHQ = HpPertoDoHQ(snap.MyUnits);

if (enemyHpNearHQ > 0 && enemyHpNearHQ > friendlyHpNearHQ)
    return AIStance.Defensive;
```

Quatorze linhas viram sete, e — mais importante — **a duplicação some**. Hoje, se
você mudar o raio de 4 para 5, tem de lembrar de mudar nos dois lugares. Um dia
alguém muda num só.

E logo abaixo, nas linhas 153-154:

```csharp
int myHp = 0, enemyHp = 0;
foreach (UnitManager u in snap.MyUnits)    myHp    += u.CurrentHP;
foreach (UnitManager u in snap.EnemyUnits) enemyHp += u.CurrentHP;
```

vira:

```csharp
int myHp    = snap.MyUnits.Sum(u => u.CurrentHP);
int enemyHp = snap.EnemyUnits.Sum(u => u.CurrentHP);
```

Aqui não há discussão: é a mesma coisa, menor e mais legível.

---

## O outro lado: por que sua ausência de LINQ é defensável

Agora a metade que é acerto, e ela é séria.

### LINQ aloca

Cada `Where` cria um objeto iterador. Cada `ToList` cria uma lista nova. Cada
lambda que captura variável externa cria um objeto de closure.

Nada disso importa quando roda uma vez por turno. Importa muito quando roda
milhares de vezes por segundo — porque tudo isso vira lixo, e o coletor de lixo
da Unity **para o jogo** para recolher. É o *stutter* que você não tem.

```text
roda 1× por turno       →  LINQ é grátis na prática.  Use.
roda por unidade/turno  →  LINQ é barato.             Use com atenção.
roda dentro de BFS,
por célula, por frame   →  LINQ é CARO.               Não use.
```

A memória `project_fow_perf_investigation` registra que você já pagou 108 ms por
unidade num `collect`. Naquele tipo de caminho, LINQ teria piorado.

### LINQ esconde o custo

Esta é a razão mais forte, e é de legibilidade, não de velocidade:

```csharp
var alvos = unidades.Where(u => Alcanca(u)).OrderBy(u => Distancia(u)).ToList();
```

Parece uma linha. É uma varredura, mais uma ordenação `n log n`, mais uma
alocação. Num `foreach` explícito, o custo está escrito na cara — você **vê** o
laço aninhado. Em LINQ ele desaparece atrás de uma frase bonita.

Para quem escreve sistema de tempo real, ver o custo é uma vantagem real.

### Encadeamento longo vira ilegível

```csharp
// não faça isso
var x = a.Where(…).SelectMany(…).GroupBy(…).OrderByDescending(…).Take(3).ToDictionary(…);
```

Uma cadeia dessas leva mais tempo pra entender do que o laço equivalente. Se
passar de três operações, quebre em passos com nome.

---

## A linha que eu recomendo pra você

Um critério só, e ele é o mesmo do resto do seu projeto — **frequência**:

| lugar | veredito |
|---|---|
| ferramentas de Editor (`MapHelperWindow`, sanitizer) | **use LINQ à vontade** — roda quando você clica |
| montagem de plano, 1× por turno (`AIWorldSnapshot`, planner) | **use** para tirar duplicação, como no exemplo acima |
| decisão por unidade, dezenas por turno | use se ficar mais claro; evite cadeia longa |
| sensores, BFS, pathfinding, qualquer coisa por célula | **não use.** Laço manual, buffer reaproveitado |
| `Update()` / `LateUpdate()` | **nunca** |

Repare que isso deixa a maior parte do seu código exatamente como está. A dívida
é pequena e localizada: ela mora nas ferramentas de editor e no montador de
snapshot.

---

## Uma armadilha que morde todo mundo

LINQ é **preguiçoso**. `Where` não faz nada quando você chama — só quando alguém
percorre o resultado.

```csharp
var vivos = unidades.Where(u => u.CurrentHP > 0);   // NADA rodou ainda

unidades.Clear();                                    // ...

foreach (var u in vivos) { }                         // roda AGORA. Zero itens.
```

E pior, o inverso: percorrer a mesma consulta duas vezes **refaz o trabalho duas
vezes**.

```csharp
var caros = unidades.Where(u => CalculoPesado(u));
int n = caros.Count();                // roda CalculoPesado em todos
var lista = caros.ToList();           // roda TUDO DE NOVO
```

**A defesa é uma regra simples:** termine toda consulta com `.ToList()` assim que
tiver o resultado que quer. Aí ela vira dado, não promessa.

---

## Exercício

**E13.** Reescreva as linhas 152-154 de
[AIWorldSnapshot.cs](Assets/Scripts/Match/AI/AIWorldSnapshot.cs) usando `Sum`.
Não salve ainda — escreva num rascunho e confira se precisa adicionar
`using System.Linq;` no topo.

Depois responda: essa mudança é segura? Quantas vezes por turno esse método roda?
(Dica: `Shift+F12` em `AIWorldSnapshot.Build`, e o `CLAUDE.md` diz que ele é
construído "fresh at the start of each phase-2 iteration" — leia isso com
cuidado, porque muda a resposta.)

**E14.** O trecho de `HpPertoDoHQ` que eu escrevi acima usa uma função
`ZerarZ` que **não existe** no seu projeto. Escreva ela. Onde ela deveria morar —
em `AIWorldSnapshot`, em `HexCoordinates`, ou em outro lugar? Justifique usando a
tabela de camadas do `CLAUDE.md` (serviço burro / consumidor / organizador).

**E15.** Sem rodar: o que este código imprime, e por quê?

```csharp
List<int> nums = new List<int> { 1, 2, 3 };
var pares = nums.Where(n => n % 2 == 0);
nums.Add(4);
Debug.Log(pares.Count());
```

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [06 — ScriptableObject e ciclo de vida](06_scriptableobject_e_ciclo.md),
onde a doutrina "catálogo diz o que É, cena diz onde ESTÁ" vira código.
