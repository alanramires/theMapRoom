# 00 — Diagnóstico: o roadmap contra o seu código

Antes de estudar qualquer coisa, vale saber o que você **já** usa. Isto não é
opinião: é uma varredura nos 460 arquivos `.cs` de `Assets/Scripts/`, procurando
cada tópico do roadmap.

Medido em 2026-08-17. Os números são "em quantos arquivos aparece", excluindo as
pastas `AI_Legacy~` (o `~` faz a Unity ignorar — é código morto que não compila
junto).

---

## O mapa de calor

### Você usa isto o tempo todo

| construção | arquivos | onde ver |
|---|---:|---|
| `partial class` | **138** | `AIController` sozinho tem **101** arquivos |
| `Dictionary<,>` | **166** | a estrutura central do jogo inteiro |
| `HashSet<>` | **136** | `plannedDestinations`, células visitadas, ocupação |
| `readonly` | **118** | caches estáticos |
| `const` | **96** | pesos e bônus do planner |
| `static class` | **94** | os serviços burros e as `*Rules` |
| `MonoBehaviour` | **60** | os managers de cena |
| `#if` (diretiva) | **64** | `UNITY_EDITOR` |
| `ScriptableObject` | **46** | os catálogos |
| `StringBuilder` | **44** | montagem de log |
| `Action`/`Func` | **33** | eventos e delegates em cache |
| `StartCoroutine` | **31** | as fases da IA, animação, replay |
| `struct` | **23** | tipos de valor pequenos |
| `override` | **36** | quase todo `ToString()` e ciclo Unity |

### Você quase não usa

| construção | arquivos | leitura |
|---|---:|---|
| `Queue<>` | 17 | BFS — sempre pathfinding |
| `virtual` | 8 | herança rasa, e isso é bom |
| `Nullable` (`int?`) | 6 | raro, e nem sempre necessário |
| `delegate` (palavra) | 4 | você prefere `Action`/`Func` — correto |
| pooling | 3 | pouco, e o jogo é por turnos |
| `Stack<>` | 2 | duas ocorrências no projeto todo |
| `interface` (declarada) | **2** | `INoDoMapa` e `IReplayCommand` |
| `Resources.Load` | 2 | você resolve por referência serializada |
| `System.Linq` | **2** | e os dois são código morto ou periférico |
| `LinkedList<>` | 1 | uma |
| `async`/`await` | 1 | uma |
| `Profiler.` | 1 | uma |

### Você não usa — zero ocorrências

| construção | capítulo do roadmap |
|---|---|
| `Rigidbody`, `OnCollision*`, `OnTrigger*` | **8. Unity Physics — inteiro** |
| `UnityEvent` | 5.2 |
| `SendMessage` / `BroadcastMessage` | 5.2 |
| `try` / `catch` / `finally` | 7.1 |
| `abstract class` | 2.2 |
| *switch expressions* (`switch { … }`) | 1.3 |
| Addressables | 10.1 |

---

## O que esses números querem dizer

### 1. O capítulo 8 do roadmap não serve pra você

Física: **zero ocorrências**. Não é descuido — é gênero. The Map Room é um jogo
de turnos em grade hexagonal. Nada cai, nada colide, nada tem massa. O que
resolve "essas duas peças estão no mesmo hex?" no seu jogo é
`ConfirmedOccupancyIndex`, não um `Collider`.

Estudar `AddForce` te custaria uma semana e não mudaria uma linha do que você
tem. **Fica de fora do curso**, e se um dia você fizer outro jogo, aprende lá.

### 2. O capítulo 5.2 propõe o que você já superou

`SendMessage` e `BroadcastMessage` são a forma antiga e frágil de comunicação na
Unity: acham o método pelo **nome em texto**, então renomear o método quebra
tudo em silêncio, sem erro de compilação.

Você não usa nenhum dos dois. Usa isto:

```csharp
// Assets/Scripts/Match/MatchController.cs:183
public static event Action<PlayerSlotId, PlayerSlotId> OnActiveSlotChanged;
```

Que é a forma **certa**: verificada pelo compilador, tipada, rastreável. Você
chegou nela sem que ninguém te ensinasse a diferença. A aula 7 explica por que
funciona e qual é o preço que ela cobra.

### 3. `try`/`catch` em zero arquivos é uma escolha, não um buraco

Chama atenção — 460 arquivos, nenhum `try`. Mas em código de gameplay Unity isso
é largamente **defensável**: você programa por guarda, não por exceção.

```csharp
// Assets/Scripts/Hex/Core/HexCoordinates.cs:9
if (tilemap == null)
    return Vector3.zero;
```

Esse padrão — checar e sair — está em todo lugar no seu código. Ele evita a
exceção em vez de capturá-la. Para gameplay, é melhor: engolir exceção em
`Update()` gera bug fantasma que aparece três horas depois.

Onde `try`/`catch` **faria** falta é em I/O — save/load, leitura de arquivo,
parse. Disco falha por motivos que nenhuma guarda prevê. A aula 9 trata disso, e
é a única parte do capítulo 7.1 que vira tarefa real.

### 4. LINQ em 2 arquivos, e os dois quase não contam

Os dois são `Match/AI_Legacy~/…` (morto, a Unity nem compila) e
`Shared/Jogadas/JogadasManager.cs`. Ou seja: no código **vivo** de gameplay, LINQ
é praticamente ausente.

Isso também é defensável — LINQ aloca, e alocar por frame é o que alimenta o GC.
Mas você paga um preço em outro lugar: laços manuais de 15 linhas onde três
resolveriam. A aula 5 mostra os dois lados e onde traçar a linha.

### 5. Duas interfaces em 460 arquivos

`INoDoMapa` e `IReplayCommand`. E a `INoDoMapa` é recente — nasceu do trabalho de
campanha, e nasceu **pelo motivo certo**, documentado no próprio arquivo:

> *"A interface existe pra ferramenta desenhar UM renderizador de nível em vez de
> três quase iguais."*

Isso é uma interface justificada por necessidade concreta, não por gosto
arquitetural. Não há dívida aqui. O curso não vai te empurrar mais interfaces:
vai te ensinar a reconhecer o momento em que uma se paga.

---

## Rastreio do roadmap original, tópico a tópico

Onde cada item da lista que você trouxe foi parar.

| roadmap | destino |
|---|---|
| **1.1** Basics, tipos, métodos, `namespace` | aulas 1 e 2 |
| **1.1** `ref` / `out` | fora — você já marcou "sem prioridade", e está certo |
| **1.2** Variáveis, `static`, `const`, `readonly`, propriedades | aula 1 |
| **1.2** Nullable types | nota na aula 2 (6 arquivos, baixo retorno) |
| **1.3** Control flow | fora como aula — você usa tudo isso corretamente há anos |
| **2.1** Classes, construtores, modificadores, `partial` | aulas 1 e 3 |
| **2.1** Destructors | fora — em C# gerenciado é quase sempre erro |
| **2.2** Herança, `virtual`/`override`, `abstract`, interfaces | aula 6 (parte) — herança rasa é feature sua |
| **3.1** Coleções | **aula 4** |
| **3.2** LINQ | **aula 5** |
| **4.1** Ciclo de vida do MonoBehaviour | **aula 6** |
| **4.2** Componentes, `GetComponent`, serialização | aula 6 |
| **5.1** Delegates, `Action`, `Func`, `event` | **aula 7** |
| **5.2** `SendMessage`, `UnityEvent`, observer | fora — ver ponto 2 acima |
| **6.1** Singleton, state machine, service locator | aula 3 e aula 6 — você já tem todos os três |
| **6.2** Refatoração | diluído nos exercícios |
| **7.1** `Debug.Log`, asserts, `try`/`catch` | **aula 9** |
| **7.2** Profiler, GC, alocação | aula 9 (parte) + `project_fow_perf_investigation` na memória |
| **8.x** Física — os dois subcapítulos | **fora, inteiro** — zero ocorrências |
| **9.1** `async`/`await`, threads | fora — 1 arquivo; Unity gameplay é corrotina |
| **9.2** Corrotinas | **aula 8** |
| **10.1** Asset bundles, Addressables, texturas | fora — nada shipado, e o `CLAUDE.md` diz que não há dívida de distribuição |
| **10.2** Cache de referência, alocação, `StringBuilder` | aula 9 |
| **11.1** Editor windows, inspectors, `ScriptableObject`, `Undo` | **aula 10** |
| **11.2** Build pipeline, `#if`, diretivas | nota na aula 10 |

### O que o roadmap não tinha, e virou aula

- **`partial class` como estratégia de navegação** (aula 3). Não é sintaxe: é o
  que decide se você acha o arquivo certo em 101 candidatos.
- **`ScriptableObject` como catálogo — a separação catálogo/cena** (aula 6). É a
  doutrina central do `CLAUDE.md` e o que mais quebrou no projeto.
- **Ler um stack trace do Console** (aula 9). O roadmap ensina a *escrever*
  `Debug.Log`. Sua necessidade é *ler* o que já está lá.

---

## Onde você está, em uma linha

> Você tem a **arquitetura** de um sênior e a **fluência de leitura** de alguém
> que nunca precisou ler. O curso ataca só a segunda.

Próximo passo: [01 — Anatomia de um arquivo](01_anatomia_de_um_arquivo.md).
