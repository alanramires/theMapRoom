# 08 — Corrotinas

> **Meta da aula:** ler `RunAITurn` de ponta a ponta e entender o que acontece em
> cada `yield`.

---

## O problema

O turno da IA tem de: esperar o tabuleiro assentar, montar o plano, mover
unidades uma a uma **com animação**, comprar, e passar o turno.

Numa função normal, isso é impossível. Uma função C# roda do começo ao fim sem
soltar a linha de execução — e a Unity desenha um frame só depois que **todo** o
código do frame terminou. Uma função que espera animação travaria o jogo pelo
turno inteiro. Tela congelada.

Corrotina resolve exatamente isso: **uma função que pode pausar no meio, devolver
o controle pra Unity desenhar, e continuar de onde parou no frame seguinte.**

---

## A forma

```csharp
private IEnumerator MinhaCorrotina()
{
    Debug.Log("A");
    yield return null;          // pausa: continua no PRÓXIMO frame
    Debug.Log("B");
    yield return new WaitForSeconds(1f);
    Debug.Log("C");
}

// não basta chamar — tem de entregar pro motor:
StartCoroutine(MinhaCorrotina());
```

Três regras que definem tudo:

1. **O retorno é `IEnumerator`.** Sempre. É o que autoriza o `yield`.
2. **`MinhaCorrotina()` sozinho não executa nada.** Chamar só cria o objeto; é o
   `StartCoroutine` que registra no motor. Esquecer isso é o erro nº 1 de quem
   começa — o método "não roda" e não há erro nenhum.
3. **`yield return` é o ponto de pausa.** O que vem depois dele diz *até quando*.

### Os `yield` que você usa

| `yield return` | continua quando |
|---|---|
| `null` | no próximo frame |
| `new WaitForSeconds(1f)` | 1 segundo depois (tempo de jogo, afetado por `timeScale`) |
| `new WaitForSecondsRealtime(1f)` | 1 segundo de relógio, ignora pausa |
| `new WaitUntil(() => condicao)` | quando a condição virar `true` |
| **outra corrotina** | quando ela terminar |
| `yield break` | não continua — **sai** da corrotina |

A quinta linha é a mais importante do seu código, e a mais fácil de ler errado.

---

## No seu jogo: `RunAITurn`

Abra [Assets/Scripts/Match/AI/1. Phases/AIController.Phases.cs:14](Assets/Scripts/Match/AI/1.%20Phases/AIController.Phases.cs).

```csharp
private IEnumerator RunAITurn(PlayerSlotId aiSlot)
{
    TeamId aiTeam = matchController != null
        ? matchController.GetVisualTeamForSlot(aiSlot)
        : TeamId.Neutral;
```

(O `? :` é o **operador ternário**: `condição ? seValor : senãoValor`. É um `if`
que devolve valor em vez de executar bloco. Aqui, uma guarda de nulo compacta.)

### `yield break` — a saída de emergência

```csharp
if (ShouldStopAIForMatchEnd("turn_start"))
    yield break;
```

`yield break` é o `return` da corrotina. A partida acabou no meio do turno da IA?
Ela para ali, e nada do resto roda.

Repare que isso aparece **depois de cada fase**, com uma etiqueta diferente:

```csharp
if (ShouldStopAIForMatchEnd("apos_stage0"))
    yield break;
yield return WaitIfDebugPaused();
if (ShouldStopAIForMatchEnd("apos_pause_stage0"))
    yield break;
```

Por que checar tantas vezes? **Porque entre um `yield` e o seguinte, o mundo
mudou.** A corrotina soltou o controle; nesse intervalo rodaram outros frames,
outros sistemas, talvez o fim da partida. A verificação que valia antes do
`yield` não vale mais depois dele.

> **Esta é a lei da corrotina, e é a única coisa desta aula que você precisa
> decorar:**
>
> **Todo `yield` é um buraco no tempo. Nada que você checou antes dele continua
> garantido depois.**

Referência que pode ter ficado nula. Unidade que pode ter morrido. Cena que pode
ter trocado. Por isso o `ShouldStopAIForMatchEnd` reaparece com etiqueta a cada
etapa: cada uma é um ponto onde a realidade pode ter mudado.

### Devolver um frame de propósito

```csharp
// Devolve um frame para desenhar o indicador antes do planejamento pesado.
yield return null;
```

Seu próprio comentário explica: sem essa linha, a Unity entraria no planejamento
pesado sem ter desenhado o indicador de "IA pensando". O jogador veria a tela
travar sem saber por quê.

`yield return null` custa um frame — 16 ms — e compra feedback visual. É a troca
mais barata que existe em jogo.

### Encadear corrotina

```csharp
yield return Phase0_WaitForTurnReady();
```

Isto **não** roda a fase 0 em paralelo. Significa: *"execute `Phase0` inteira e
só continue quando ela terminar"*.

É o que faz as cinco fases rodarem em sequência garantida, cada uma podendo
esperar quantos frames precisar. `RunAITurn` vira um roteiro legível:

```csharp
yield return Phase0_WaitForTurnReady();      // espera assentar
yield return CommitAIWorldHeavy(…);          // consolida o mundo
AIWorldSnapshot snapshot = AIWorldSnapshot.Build(aiSlot, matchController);
yield return null;
// … Phase1, Phase2, Phase3, Phase4
```

Cinco corrotinas em fila, uma por arquivo em `1. Phases/`. É a máquina de estados
do turno, escrita como se fosse código sequencial. **Corrotina é o que permite
isso.**

### Medir tempo entre `yield`

```csharp
float tCommit = Time.realtimeSinceStartup;
yield return CommitAIWorldHeavy(aiSlot, "turn-start", rebuildPlan: false);
Debug.Log($"[AI Perf] CommitAIWorldHeavy: {(Time.realtimeSinceStartup - tCommit) * 1000f:F0}ms");
```

Um cronômetro em volta de uma corrotina inteira. `realtimeSinceStartup` ignora
`timeScale`, então mede tempo de parede de verdade.

O `:F0` dentro da interpolação é **formatação**: zero casas decimais.
`{valor:F2}` daria duas. Vale conhecer — deixa log de performance legível sem
`Math.Round`.

A memória `feedback_perf_measure_dont_deduce` registra a lição associada: ler
código não acha gargalo; contador é grátis, cronômetro não. Estas linhas são o
cronômetro.

### Argumento nomeado

```csharp
CommitAIWorldHeavy(aiSlot, "turn-start", rebuildPlan: false)
```

`rebuildPlan: false` é **argumento nomeado**. Igual a passar `false` na posição,
mas quem lê sabe o que o `false` significa.

Use sempre que passar um booleano literal. `Refresh(true)` não diz nada;
`Refresh(force: true)` diz tudo. É a diferença entre um leitor que precisa abrir
a assinatura e um que não precisa.

---

## `StopCoroutine` e o cuidado com ela

```csharp
StartCoroutine(RunAITurn(slot));    // começa
StopAllCoroutines();                // para TODAS deste MonoBehaviour
```

Uma corrotina parada **não roda nenhuma limpeza**. Não há `finally`, não há
`OnDestroy`. Ela simplesmente deixa de existir no ponto em que estava.

Se ela tinha ligado um flag no começo pra desligar no fim, o flag fica ligado pra
sempre.

E automaticamente: **desativar o `GameObject` mata todas as corrotinas dele**. Um
`gameObject.SetActive(false)` no meio do turno da IA aborta o turno em silêncio.

> Se uma corrotina liga alguma coisa, quem a interrompe é responsável por
> desligar. O `yield break` é seguro porque é a própria corrotina saindo, e ela
> pode limpar antes.

---

## Por que corrotina e não `async`/`await`

O roadmap dedica o capítulo 9.1 a `async`/`await`. Você usa em **1** arquivo. É a
proporção certa, e vale saber por quê:

| | corrotina | `async`/`await` |
|---|---|---|
| thread | a principal, sempre | pode ir pra outra |
| pode tocar API da Unity? | **sim** | **não**, fora da principal |
| morre com o `GameObject`? | sim, automático | **não** — continua rodando |
| frame do jogo | integrada | não sabe o que é frame |

A segunda linha é decisiva: quase tudo da Unity — `transform`, `GetComponent`,
`Instantiate`, tilemap — **só pode ser tocado na thread principal**. Um
`await` que volta em outra thread e mexe num `Transform` lança exceção.

A terceira é a que causa bug feio: uma `Task` não sabe que a cena trocou. Ela
continua rodando e escreve em objetos destruídos.

`async` ganha em I/O de verdade: ler arquivo grande, rede, cálculo pesado que não
toca a Unity. Fora disso, corrotina é a ferramenta certa em gameplay Unity. Seu
código já reflete isso.

---

## Exercício

**E22.** Leia [AIController.Phases.cs:14-75](Assets/Scripts/Match/AI/1.%20Phases/AIController.Phases.cs)
e conte quantos pontos de `yield` existem no trecho. Para cada um, responda em
poucas palavras: *o que pode ter mudado no mundo enquanto ele estava pausado?*

**E23.** No mesmo trecho há um `if/else` grande em torno de `emulateStage0`.
Descreva os dois caminhos e responda: por que o caminho do `else` (retomada de
save) precisa de `WaitForResumeSettleTelemetry()`? Que problema ele evita?

O comentário nas linhas 54-56 responde. Sua tarefa é reescrever a resposta com
suas palavras — se você conseguir, entendeu.

**E24.** Sem rodar: o que este código imprime, e por quê?

```csharp
void Start()
{
    Debug.Log("1");
    MinhaCorrotina();              // ← repare: sem StartCoroutine
    Debug.Log("2");
}

IEnumerator MinhaCorrotina()
{
    Debug.Log("3");
    yield return null;
    Debug.Log("4");
}
```

E depois: o que muda se a linha virar `StartCoroutine(MinhaCorrotina())`?

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [09 — Quando trava](09_quando_trava.md). A partir daqui o curso deixa de
ser sobre linguagem e passa a ser sobre autonomia.
