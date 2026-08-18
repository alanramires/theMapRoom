# 07 — Eventos

> **Meta da aula:** entender a espinha dorsal de comunicação do seu jogo, e o
> preço que ela cobra.

---

## O problema que evento resolve

O `MatchController` troca o time ativo. Precisam saber disso: a UI, a IA, o
cursor, o minimapa, o gerenciador de névoa, o painel de turno.

Sem evento, o `MatchController` teria de conhecer os seis:

```csharp
// o jeito ruim
ui.AtualizarTime(novoTime);
ia.AtualizarTime(novoTime);
cursor.AtualizarTime(novoTime);
// ... e mais três
```

Todo sistema novo obriga a editar o `MatchController`. Ele vira o centro de tudo,
e depende de tudo.

Com evento, ele **anuncia** e não conhece ninguém:

```csharp
// Assets/Scripts/Match/MatchController.cs:185
public static event Action<int> OnActiveTeamChanged;
```

Quem se importa, assina. O `MatchController` não sabe quantos são nem quem são —
e, criticamente, **não precisa ser editado** quando um sétimo aparece.

> Essa é a inversão que dá o nome ao padrão: em vez de quem sabe chamar quem
> precisa, quem precisa se inscreve em quem sabe.

---

## A anatomia

```csharp
public static event Action<PlayerSlotId, PlayerSlotId> OnActiveSlotChanged;
```

Cinco pedaços, e cada um carrega uma decisão:

| pedaço | o que faz |
|---|---|
| `public` | qualquer arquivo pode assinar |
| `static` | pertence à **classe**, não a uma instância — ver abaixo |
| `event` | protege: de fora, só dá pra `+=` e `-=` |
| `Action<A, B>` | a **forma** do método que pode assinar |
| `OnAlgoAconteceu` | convenção de nome: `On` + fato no passado |

### `Action` e `Func`

```csharp
Action                    método sem parâmetro, sem retorno
Action<int>               recebe int, não devolve nada
Action<TeamId, int>       recebe dois, não devolve nada
Func<int>                 não recebe, devolve int
Func<UnitManager, bool>   recebe unidade, devolve bool
```

Regra: em `Func`, o **último** tipo é o retorno. `Action` nunca tem retorno.

São tipos prontos que substituem a palavra `delegate`. É por isso que você tem
`Action`/`Func` em 33 arquivos e `delegate` em 4 — você usa a forma moderna quase
sempre, e está certo.

### O que a palavra `event` acrescenta

Sem `event`, o campo seria um delegate normal, e qualquer arquivo poderia fazer:

```csharp
MatchController.OnActiveTeamChanged = MeuMetodo;   // APAGA todos os outros!
MatchController.OnActiveTeamChanged(3);            // dispara de fora
```

Com `event`, as duas linhas **não compilam** de fora da classe. Só `+=` e `-=`
passam, e só o `MatchController` pode disparar.

É uma proteção pequena e valiosa. O `=` no lugar do `+=` é um erro de digitação
que apaga todos os assinantes em silêncio; `event` transforma isso em erro de
compilação.

---

## `static event` — a decisão que você tomou, e o preço

Todos os seus eventos principais são `static`:

```csharp
// MatchController.cs
public static event Action<PlayerSlotId, PlayerSlotId> OnActiveSlotChanged;
public static event Action<int> OnActiveTeamChanged;
public static event Action OnFogOfWarUpdated;
public static event Action<TeamId> OnTeamDefeated;

// CursorController.cs:13
public static event Action OnCursorReturnedToNeutral;

// TurnStateManager.cs:15
public static event Action OnSensorsReady;
```

**O ganho** é grande e concreto: quem assina não precisa de uma referência ao
objeto. Não precisa arrastar o `MatchController` no Inspector, não precisa de
`FindObjectOfType`, não precisa existir na mesma cena. Só escreve
`MatchController.OnActiveTeamChanged += …`.

Num projeto onde os managers vivem em cenas diferentes e alguns são
`DontDestroyOnLoad`, isso resolve um problema real de ligação.

**O preço** é uma armadilha específica, e ela é séria:

> **Um `static event` sobrevive à troca de cena. O objeto que assinou, não.**

Sequência do desastre:

```text
1. objeto X, na cena A, assina o evento estático
2. carrega a cena B  →  X é destruído
3. mas a lista de assinantes é estática: ela NÃO foi destruída
4. o evento dispara  →  chama o método de um objeto morto
```

Na Unity isso dá o erro mais confuso do motor:

```text
MissingReferenceException: The object of type 'X' has been destroyed
but you are still trying to access it.
```

Confuso porque o stack trace aponta pra dentro do método de `X`, não pro
`MatchController` que disparou, nem pra troca de cena que é a causa real.

**A defesa é uma linha, e ela é obrigatória:**

```csharp
void OnDisable() { MatchController.OnActiveTeamChanged -= AoTrocarTime; }
```

`OnDisable` é chamado quando a cena descarrega. Se o `-=` estiver lá, o assinante
morto sai da lista antes de virar problema.

**Toda assinatura de evento estático precisa do seu `-=`. Sem exceção.** É o
custo fixo da escolha, e é barato — desde que você nunca esqueça.

Isso liga direto com o bloqueio **0b** do seu `resumo.md` (`sceneLoaded` nos 4
managers). São dois lados da mesma moeda: estado global que atravessa cena. A
campanha **vai** encadear cenas, e é quando isso deixa de ser teórico.

---

## Disparar com segurança

```csharp
OnActiveTeamChanged?.Invoke(novoTime);
```

O `?.` é o **operador condicional de nulo**: só chama se não for `null`.

Necessário porque um evento **sem nenhum assinante é `null`**, não uma lista
vazia. Disparar sem o `?.` quando ninguém assinou lança
`NullReferenceException`. E "ninguém assinou" acontece o tempo todo — no primeiro
frame, num teste isolado, numa cena reduzida.

Ao ler seu código, `Evento?.Invoke(…)` é a linha que **causa** tudo que acontece
depois. Se você está caçando "por que a UI atualizou?", ache o `?.Invoke` e daí
use `Shift+F12` no nome do evento pra listar todos os assinantes.

---

## Assinar direito

```csharp
private void OnEnable()
{
    CursorController.OnCursorReturnedToNeutral += AoVoltarPraNeutro;
}

private void OnDisable()
{
    CursorController.OnCursorReturnedToNeutral -= AoVoltarPraNeutro;
}

private void AoVoltarPraNeutro()
{
    RefreshAlgumaCoisa(force: true);
}
```

Três detalhes que fazem diferença:

**1. `OnEnable`/`OnDisable`, não `Start`/`OnDestroy`.** Já discutido na aula 6: é
o par que fecha em toda ativação.

**2. Método nomeado, não lambda.**

```csharp
// ❌ NÃO FAÇA
CursorController.OnCursorReturnedToNeutral += () => Refresh(true);
```

Não dá pra desassinar isso. Cada lambda é um objeto novo, então
`-= () => Refresh(true)` remove nada — é outra lambda, com outra identidade. A
assinatura fica presa pra sempre.

**3. `force: true` no refresh.** Esse é o argumento nomeado, e ele é doutrina do
`CLAUDE.md`, não estilo. O motivo já foi dado na aula 6: o cache é o que trava, e
reconferir sem furar o cache não reconfere nada.

---

## Ordem de execução — a garantia que não existe

Quando o evento dispara, os assinantes rodam **na ordem em que assinaram**. E a
ordem em que assinaram depende da ordem de `OnEnable`, que depende da hierarquia
da cena, que ninguém controla de propósito.

> **Nunca escreva código que dependa de outro assinante ter rodado antes.**

Se A precisa acontecer antes de B, eles não são dois assinantes do mesmo evento.
São uma sequência — e uma sequência tem dono. Ou A chama B, ou existe um terceiro
que chama os dois na ordem.

É o mesmo raciocínio da ordem `Awake`→`Start` da aula 6, num nível acima.

---

## Por que você não usa `UnityEvent` (e está certo)

`UnityEvent` é o campo de evento que aparece no Inspector, onde você arrasta um
objeto e escolhe o método numa lista.

Zero ocorrências no seu projeto. Bom, porque:

| | `static event` (seu) | `UnityEvent` |
|---|---|---|
| ligação | código | arrastada no Inspector |
| renomear o método | erro de compilação | **quebra em silêncio** |
| `Shift+F12` acha? | sim | **não** |
| revisão em git | diff legível | YAML de cena |

O item que decide é o terceiro. Num projeto de 460 arquivos onde sua dificuldade
declarada é *achar as coisas*, uma ligação que a busca não enxerga é veneno.

`UnityEvent` ganha num caso só: quando um designer não-programador precisa ligar
coisas sem abrir código. Você é o programador e o designer. Não precisa.

---

## Exercício

**E19.** Liste todos os eventos estáticos de
[MatchController.cs](Assets/Scripts/Match/MatchController.cs) (linhas 183-191) e,
para cada um, use `Shift+F12` pra contar quantos assinantes tem.

Qual é o mais escutado? Faz sentido que seja ele?

**E20.** Escolha um evento com poucos assinantes e siga o caminho inteiro:

```text
o ?.Invoke que dispara   →   cada += que assina   →   o que cada assinante faz
```

Escreva o caminho em três a cinco linhas de texto. Este exercício é a habilidade
central do curso: **seguir uma causa pelo código**.

**E21.** Volte à tabela do E17 (aula 6). Para cada assinante de
`OnCursorReturnedToNeutral`, responda: se a cena trocar enquanto ele está
assinado, o que acontece? Algum deles é `DontDestroyOnLoad`?

Isso conecta com o bloqueio **0b** do `resumo.md`. Se você achar um caso real,
anote — é material pro dia em que a campanha encadear cenas, e esse dia está
marcado.

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [08 — Corrotinas](08_corrotinas.md).
