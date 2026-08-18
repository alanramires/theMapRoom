# 09 — Quando trava

> **Meta da aula:** ler um erro do Console e chegar na linha culpada sozinho.

Esta é a aula que mais reduz sua dependência de mim. Não porque ensina algo
difícil, mas porque ninguém nunca te mostrou como ler as três linhas que a Unity
já imprime.

---

## Ler um stack trace

O Console mostra algo assim:

```text
NullReferenceException: Object reference not set to an instance of an object
  AIController.TryDecideCapturerAction (UnitManager unit, AIWorldSnapshot snapshot)
      (at Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.cs:412)
  AIController.DecideUnitAction (UnitManager unit, AIWorldSnapshot snapshot)
      (at Assets/Scripts/Match/AI/AIController.Router.cs:87)
  AIController+<Phase2_UnitActions>d__31.MoveNext ()
      (at Assets/Scripts/Match/AI/1. Phases/AIController.Phase2.cs:203)
```

Leia **de cima pra baixo**, e saiba o que cada faixa significa:

```text
linha 1        O QUE aconteceu           NullReferenceException
linha 2-3      ONDE aconteceu            Capturer.cs:412   ← COMECE AQUI
linha 4-5      quem chamou               Router.cs:87
linha 6-7      quem chamou aquele        Phase2.cs:203
```

**A primeira linha com um arquivo seu é a culpada.** No Console da Unity, ela é
clicável: duplo clique abre o arquivo na linha.

As linhas de baixo são o **caminho** até lá — e servem pra responder *"como o
programa foi parar nessa situação?"*, que é a pergunta seguinte.

### Aquele `d__31.MoveNext` esquisito

```text
AIController+<Phase2_UnitActions>d__31.MoveNext ()
```

Isso é uma **corrotina**. O compilador transforma toda corrotina numa classe
escondida com um método `MoveNext`, e é esse nome que aparece.

Traduza mentalmente: `<Phase2_UnitActions>d__31.MoveNext` significa *"dentro da
corrotina `Phase2_UnitActions`"*. O número é gerado, ignore.

### As quatro exceções que você vai encontrar

| exceção | significa | onde olhar primeiro |
|---|---|---|
| `NullReferenceException` | usou algo que era `null` | campo do Inspector não arrastado; `GetComponent` que não achou |
| `IndexOutOfRangeException` / `ArgumentOutOfRange` | índice fora da lista | a conta row-major; laço com `<=` onde devia ser `<` |
| `KeyNotFoundException` | `dicionario[chave]` sem a chave | trocar por `TryGetValue`; ou `z` sujo na chave |
| `MissingReferenceException` | objeto Unity **destruído**, ainda referenciado | `static event` sem `-=`, aula 7 |

A quarta é a mais confusa e a mais provável no seu projeto, pela quantidade de
eventos estáticos. Repare na diferença:

```text
NullReferenceException   nunca teve objeto
MissingReferenceException   TEVE, e foi destruído — a referência ainda aponta
```

A segunda é quase sempre ciclo de vida: alguém guardou referência ou assinatura
que sobreviveu a quem deveria ter morrido junto.

---

## Achar a linha quando não há exceção

Pior que crash é resultado errado sem erro. É a maioria dos seus bugs — o
`resumo.md` está cheio deles:

> *"Duas boards com faixas de coordenada sobrepostas aplicam a estrada uma da
> outra sem aviso nenhum."*
> *"Esquecer o setor não dá erro: dá plano degenerado."*
> *"Só um prédio preto."*

Contra esses, o `Debug.Log` é a ferramenta, e você já tem a versão boa dela.

### O padrão `TL()`

```csharp
Debug.Log($"{TL()} Turno {snapshot.TurnNumber} | Stance: {snapshot.Stance}");
```

O `CLAUDE.md` documenta: `TL("Categoria")` carimba `[AI TEAM][T#][Categoria]`.

Isso resolve o problema real do log em jogo — **volume**. Num turno de IA saem
centenas de linhas. Sem prefixo, você rola o Console procurando. Com prefixo, a
caixa de busca do Console filtra: digite `[Embark]` e sobra só o que interessa.

> **Log sem prefixo filtrável é log que você não vai conseguir usar.** É a
> diferença entre ter informação e ter ruído.

### Os três níveis

```csharp
Debug.Log("normal");                 // branco
Debug.LogWarning("suspeito");        // amarelo
Debug.LogError("errado");            // vermelho, e PARA o Console em "Error Pause"
```

Use `LogError` no que **nunca deveria acontecer**. Vale ouro: com *Error Pause*
ligado no Console, o jogo congela no frame exato do erro, e você inspeciona o
estado ao vivo em vez de reconstruir depois.

Aquele caso do `resumo.md` — o `ConstructionSpawner` da `Batalha` sem catálogo —
é exatamente o que merece `LogError`. E, de fato, você registrou que "o log diz
isso explicitamente".

### O segundo parâmetro que quase ninguém conhece

```csharp
Debug.Log("Unidade travada", unidade.gameObject);
```

Passando um objeto Unity como segundo argumento, **clicar no log seleciona o
objeto na Hierarchy**. Num tabuleiro com 40 unidades, isso responde "qual delas?"
instantaneamente.

Custa uma palavra. Use em todo log que fale de uma unidade ou construção
específica.

---

## O diagnóstico por arquivo — a técnica do seu projeto

Sua memória `project_debug_file_diagnostics` registra uma descoberta que vale
repetir aqui, porque é contraintuitiva:

> **Logs de Play não vão para o `Editor.log`.**

Ou seja: quando o Console tem milhares de linhas, ou quando o Unity trava e você
perde tudo, não adianta procurar no `Editor.log`. A saída que você achou foi
escrever direto num arquivo:

```csharp
System.IO.File.AppendAllText("diagnostico.txt", $"{DateTime.Now}: {mensagem}\n");
```

Na raiz do projeto, fora de `Assets/` (senão a Unity importa como asset e
reimporta a cada escrita).

Quando isso ganha do Console:

- saída grande demais — o Console trunca e fica lento
- você quer **comparar duas execuções** — dois arquivos, um `diff`
- travamento onde o Console some
- você quer processar depois (contar, ordenar, filtrar com ferramenta de texto)

E aqui, finalmente, o `try`/`catch` que seu projeto não tem em lugar nenhum:

```csharp
try
{
    System.IO.File.AppendAllText(caminho, texto);
}
catch (System.Exception e)
{
    Debug.LogWarning($"Não consegui gravar o diagnóstico: {e.Message}");
}
```

Disco cheio, arquivo aberto no Notepad, permissão negada — todos lançam exceção,
e nenhum deles é prevenível por guarda de nulo. **É a diferença entre I/O e
lógica:** lógica você confere antes; I/O falha por motivo externo, e a única
defesa é capturar.

> **A regra que resume o capítulo 7.1 pro seu projeto:** `try`/`catch` só onde o
> mundo pode falhar por conta própria — arquivo, rede, parse de texto externo.
> Em gameplay, guarda de nulo, sempre.

E nunca faça isto:

```csharp
try { fazTudo(); }
catch { }              // ❌ engole o erro. O bug vira fantasma.
```

---

## Quando o jogo está lento

O roadmap dedica o 7.2 a isso. A regra da sua própria memória
(`feedback_perf_measure_dont_deduce`) é o resumo:

> **Ler código não acha gargalo. Contador é grátis, cronômetro não.**

O jeito mais barato, e o que já está no seu código:

```csharp
float t0 = Time.realtimeSinceStartup;
FazerAlgoPesado();
Debug.Log($"[Perf] Algo: {(Time.realtimeSinceStartup - t0) * 1000f:F0}ms");
```

**Meça antes de mexer, e meça de novo depois.** A memória
`project_fow_perf_investigation` registra dois candidatos que foram medidos e
**descartados** — interop e clone O(n²). Sem medir, você teria "otimizado" os
dois e não teria ganho nada.

O Profiler da Unity (*Window → Analysis → Profiler*) faz isso melhor, mas tem uma
curva. Comece com o cronômetro. Ele responde 80% das perguntas e você já sabe
usar.

Uma referência de escala pra calibrar julgamento:

```text
60 FPS  =  16,6 ms por frame — TOTAL, pra tudo
```

Qualquer coisa que passe de 16 ms num frame come o orçamento inteiro. Num jogo de
turnos você tem folga (um turno de IA pode gastar segundos sem incomodar), mas o
número é a régua.

---

## O checklist de quando trava

Antes de me chamar, nesta ordem:

```text
1. O Console tem erro VERMELHO?
   → primeira linha com arquivo SEU = a culpada. Duplo clique.

2. É NullReferenceException?
   → qual campo é null?
   → se for [SerializeField]: está arrastado no Inspector? (verifique NA CENA)

3. É MissingReferenceException?
   → algum static event sem -=  (aula 7)

4. Não tem erro, mas o resultado está errado?
   → Debug.Log com prefixo filtrável NOS DOIS lados da suspeita
   → confirme o valor de entrada antes de duvidar da lógica

5. Não compila?
   → SEMPRE resolva o PRIMEIRO erro da lista. Os outros costumam ser eco dele.

6. Está lento?
   → cronômetro. Não deduza.
```

O passo 5 vale sublinhar. Um `}` faltando gera vinte erros em cascata, e os
dezenove de baixo são fantasmas. Resolver o primeiro apaga todos.

E lembre da armadilha do `resumo.md`: *"`sed` em C# sem conferir chaves — comeu
um `}` de fechamento"*. Se muitos erros apareceram de uma vez depois de uma
edição, suspeite de chave antes de suspeitar de lógica.

---

## Exercício

**E25.** Provoque um erro de propósito, num arquivo descartável.

Crie `Assets/Scripts/Estudo/TesteErro.cs` com um `MonoBehaviour` que, no `Start`,
acesse um campo `List<int>` declarado sem `new`. Ponha numa cena de teste, rode,
e **leia o stack trace inteiro** antes de consertar.

Anote: qual foi a primeira linha com arquivo seu? O duplo clique levou ao lugar
certo?

Depois **apague o arquivo e a cena**. É estudo, não conteúdo.

**E26.** Ache no seu projeto um `Debug.Log` que **não** tenha prefixo filtrável.
Você acha ele no Console durante um turno de IA cheio? Reescreva usando o padrão
`TL()` — mas não salve ainda, só escreva a linha.

**E27.** Cronometre algo real. Escolha um método que você suspeita ser pesado
(candidatos honestos: qualquer coisa em `Phase2`, ou o pintor de quadrante da
frente de campanha), envolva com o cronômetro, rode um turno, e anote os
milissegundos.

Depois responda: era o que você esperava? A memória
`feedback_perf_measure_dont_deduce` existe porque a resposta costuma ser não.

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [10 — Ferramenta de editor](10_ferramenta_de_editor.md), que é onde sua
frente aberta está.
