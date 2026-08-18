# 10 — Ferramenta de editor

> **Meta da aula:** entender o `MapHelperWindow` bem o suficiente pra mexer nele
> sozinho — porque é ali que sua frente aberta está.

O `resumo.md` diz onde você parou: recorte de estruturas, assar o `A_IA_Q2`, bake
de unidades iniciais. Tudo isso passa por
[Assets/Editor/MapHelperWindow.cs](Assets/Editor/MapHelperWindow.cs), 1860 linhas.

A boa notícia: código de editor é o **mais seguro** do projeto pra praticar. Ele
não roda no jogo, não afeta build, e um erro ali estraga no máximo um clique seu.

---

## A pasta `Editor/` é mágica

Qualquer script dentro de uma pasta chamada `Editor` (em qualquer nível) é
**excluído do build**. Só existe dentro do Unity Editor.

É por isso que `MapHelperWindow` pode usar `using UnityEditor;` livremente —
esse namespace não existe no jogo compilado. Um script em `Assets/Scripts/` que
usasse `UnityEditor` quebraria o build.

Quando você precisa de código de editor **dentro** de um arquivo normal:

```csharp
#if UNITY_EDITOR
    EditorUtility.SetDirty(asset);
#endif
```

`#if` é uma **diretiva de pré-processador**: o compilador remove o trecho antes de
compilar, quando o símbolo não está definido. Não é `if` — não roda em execução,
o código **desaparece**. Você usa em 64 arquivos.

Isso é o capítulo 11.2 inteiro do roadmap, e é praticamente tudo que ele importa
pra você hoje: sem build de distribuição, `#if UNITY_EDITOR` é a única diretiva
que ganha o seu tempo.

---

## A anatomia da janela

### A classe e o menu

```csharp
// MapHelperWindow.cs:33
public class MapHelperWindow : EditorWindow
```

`EditorWindow` é a base de toda janela dockável da Unity.

```csharp
// linha 107
[MenuItem("Tools/Utils/Map Helper")]
public static void OpenWindow() => GetWindow<MapHelperWindow>("Map Helper");
```

`[MenuItem]` põe a entrada no menu superior — barras fazem submenu. O método
**tem de ser `static`**: a Unity chama sem ter instância nenhuma.

`GetWindow<T>` é inteligente: se a janela já está aberta, foca; se não, cria. Por
isso clicar duas vezes no menu não abre duas janelas.

### `OnEnable`/`OnDisable` — o par da aula 7, de novo

```csharp
// linha 110
private void OnEnable()
{
    SceneView.duringSceneGui += OnSceneGUI;
    AutoDetectTilemap();
    Scan();
    RecomputeOverlap();
}

private void OnDisable()
{
    SceneView.duringSceneGui -= OnSceneGUI;
    CancelPickSilently();
}
```

Reconheceu? É exatamente o padrão da aula 7: **`+=` no `OnEnable`, `-=` no
`OnDisable`**, com método nomeado.

`SceneView.duringSceneGui` é o evento que permite desenhar **dentro da janela de
cena** — é o que faz o retângulo do quadrante aparecer sobre o mapa.

E o `-=` aqui não é opcional. Sem ele, fechar e reabrir a janela deixaria dois
desenhadores ativos, e o retângulo seria desenhado duas vezes. Depois de cinco
aberturas, cinco vezes.

Você aplicou a regra certa num contexto que nem é de gameplay. Isso é o padrão
tendo virado hábito, e é exatamente o que o curso quer.

### `OnGUI` — a parte que estranha

```csharp
// linha 126
private void OnGUI()
{
    EditorGUILayout.LabelField("Map Helper", EditorStyles.boldLabel);
```

`OnGUI` roda **muitas vezes por segundo**, e a cada vez **redesenha a janela
inteira do zero**. Não existe "criar o botão uma vez". Você descreve a janela
toda, sempre.

Isso é chamado de **IMGUI** (*immediate mode GUI*), e é o oposto do resto do que
você faz na Unity. A consequência prática:

> **Não faça trabalho pesado dentro de `OnGUI`.** Ele roda dezenas de vezes por
> segundo enquanto a janela estiver visível.

O `MapHelperWindow` respeita isso: o trabalho pesado está em `Scan()`, chamado
por **evento** — no `OnEnable`, no clique do botão "Recalcular", na mudança do
tilemap. Nunca dentro do desenho.

Se um dia a janela ficar lenta, é aqui que a causa mora: alguma conta migrou pro
`OnGUI`.

### Ler e escrever um valor no mesmo passo

```csharp
// linha 131
overrideTilemap = (Tilemap)EditorGUILayout.ObjectField("Tilemap", overrideTilemap, typeof(Tilemap), true);
```

O campo aparece mostrando o valor atual **e** o resultado é atribuído de volta. Em
IMGUI é sempre assim: o controle recebe o valor e devolve o valor (possivelmente
mudado). Nunca há callback.

O `(Tilemap)` na frente é um **cast**: `ObjectField` devolve `Object` genérico, e
você afirma que é um `Tilemap`. Se não for, lança exceção — mas aqui o
`typeof(Tilemap)` no argumento garante que a Unity só aceite tilemaps.

### Detectar mudança

```csharp
// linha 130
EditorGUI.BeginChangeCheck();
overrideTilemap = (Tilemap)EditorGUILayout.ObjectField(…);
if (EditorGUI.EndChangeCheck())
    Scan();
```

*"O usuário mexeu em algo entre estas duas linhas?"* Se sim, `Scan()`.

É o jeito IMGUI de reagir a mudança sem rodar `Scan()` a cada frame de desenho. É
o padrão que mantém o `OnGUI` leve.

### Botões e layout

```csharp
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("Auto Detect"))
{
    overrideTilemap = null;
    AutoDetectTilemap();
    Scan();
}
if (GUILayout.Button("Recalcular"))
    Scan();
EditorGUILayout.EndHorizontal();
```

`GUILayout.Button` **desenha o botão e devolve `true` no frame do clique**. Por
isso o `if` — a leitura correta é *"se este botão foi clicado agora"*.

`BeginHorizontal`/`EndHorizontal` põem os dois lado a lado. **Todo `Begin` precisa
do `End`.** Esquecer um é o erro mais comum em IMGUI, e a mensagem que aparece é
inútil (`GUILayout: Mismatched LayoutGroup`) — ela não diz qual.

Se isso acontecer com você: procure o `Begin` mais recente que você adicionou. É
quase sempre esse.

---

## As duas linhas que salvam o dado

Este é o ponto mais importante da aula, e o que causa a maior perda de trabalho.

```csharp
// linha 200
Undo.RecordObject(mundo, "Editar mundo");
// … modifica o mundo …
EditorUtility.SetDirty(mundo);
```

### `Undo.RecordObject` — antes de mexer

Grava o estado **atual** pro `Ctrl+Z` funcionar. Vem **antes** da modificação —
ele fotografa o "antes".

O segundo argumento é o texto que aparece no menu Edit → Undo. Por isso o código
tem `"Editar nó"`, `"Destrave"`, `"Novo bloco"` — cada operação com seu nome.

### `EditorUtility.SetDirty` — depois de mexer

Marca o asset como "modificado, precisa gravar em disco". **Sem ele, a Unity não
sabe que mudou, e a mudança se perde quando você fechar o Editor.**

Sem erro. Sem aviso. Você trabalha uma hora no Map Helper, fecha a Unity, e o
`Mundo Fixture.asset` está como estava.

> **A dupla `RecordObject` … `SetDirty` é obrigatória em qualquer código de
> editor que altere um asset.** Uma antes, outra depois.

E aqui a armadilha que já está registrada na sua memória
(`feedback_asset_disk_editor_collision`), que é o outro lado da mesma moeda:

> **Não editar `.asset` no disco com o Inspector aberto** — o reimport descarta o
> que a Unity tem em memória.

Os dois juntos formam a regra completa:

```text
A Unity tem uma cópia do asset em MEMÓRIA.
O disco tem outra.

SetDirty       →  "memória é a verdade, grave no disco"
reimport       →  "disco é a verdade, jogue a memória fora"
```

Fazer os dois ao mesmo tempo perde trabalho, e é aleatório qual lado ganha.

---

## O `?.` que aparece na linha 101

```csharp
return b?.campanhas != null && selectedCampanha >= 0 && selectedCampanha < b.campanhas.Count
    ? b.campanhas[selectedCampanha]
    : null;
```

Duas construções numa linha só:

**`b?.campanhas`** — o mesmo `?.` do `Invoke` da aula 7. Se `b` for `null`, a
expressão inteira vira `null` em vez de lançar exceção. Substitui
`b != null && b.campanhas != null` por três caracteres.

**`condição ? a : b`** — o ternário da aula 8. Aqui a condição é longa e o
resultado é curto, então a quebra em três linhas é o que mantém legível.

E repare na ordem dos testes, que é a aula 1 voltando: `b?.campanhas != null`
vem **antes** de `b.campanhas.Count`. Curto-circuito. Se invertesse, o `.Count`
rodaria com `campanhas` nulo.

---

## Onde mexer primeiro

Sua frente aberta, do `resumo.md`:

```text
2. investigar por que as construções plantam só em PARTE
3. assar o A_IA_Q2
4. recorte de ESTRUTURAS
5. bake de unidades iniciais (mesmo molde do bakedConstrucoes)
```

O item **5** é o melhor exercício de programação que você tem no projeto agora, e
por um motivo específico: **ele é uma cópia estrutural de algo que já funciona.**

`bakedConstrucoes` já existe, já é assado, já é lido. Unidades iniciais seguem o
mesmo caminho:

```text
uma classe de dado assado           ← ConstrucaoAssada é o molde
um campo List<T> no QuadranteData   ← bakedConstrucoes é o molde
código de bake que preenche          ← o do MapHelperWindow é o molde
código de plantio que lê             ← o ConstructionSpawner é o molde
```

Quatro peças, todas com precedente no repositório. **Copiar uma estrutura que
funciona é a forma mais honesta de aprender**, e é bem diferente de inventar.

Antes de começar, o aviso do `resumo.md` que se aplica: *"o recorte de rotas lê o
`RoadNetworkManager` da cena. Não existe mais catálogo de onde ler"*. Ou seja:
para unidades, a fonte é a **cena de autoria**, não um catálogo. Aula 6 —
catálogo diz o que É, cena diz onde ESTÁ. Unidade posicionada é "onde está".

---

## Exercício

**E28.** Abra [MapHelperWindow.cs](Assets/Editor/MapHelperWindow.cs) e ache
**todos** os pares `Undo.RecordObject` … `EditorUtility.SetDirty`. Existe algum
`SetDirty` sem `RecordObject` antes, ou vice-versa?

(Dica: `Ctrl+Shift+F` nos dois termos, compare os números de linha. Já sei que há
mais `SetDirty` do que `RecordObject` — sua tarefa é dizer se cada caso é
problema ou é legítimo. Nem toda escrita precisa de undo.)

**E29.** Adicione um botão ao Map Helper que só imprima informação — nada de
modificar dado.

Sugestão: um botão "Diagnóstico" que faça `Debug.Log` do quadrante selecionado
usando o `ToString()` que você já tem, mais `CellCount`, `HasBake`, e a contagem
de `bakedConstrucoes`.

Isso exercita: achar o lugar certo no `OnGUI`, `GUILayout.Button`, interpolação de
string, e **é reversível** — não toca em asset nenhum.

**E30. (o grande)** Escreva o **plano** — não o código — do bake de unidades
iniciais. Quatro perguntas:

1. Que campos uma `UnidadeAssada` precisa ter? (olhe `ConstrucaoAssada` e pense:
   o que uma unidade tem que uma construção não tem?)
2. Onde ela é guardada? (a resposta está no `QuadranteData`)
3. Quem preenche, e quando?
4. Quem lê, e quando?

Traga o plano. Aí a gente implementa **junto** — e dessa vez você vai saber o que
está lendo.

Gabarito em [exercicios.md](exercicios.md).

---

Fim da trilha. Continue em [exercicios.md](exercicios.md), e depois volte ao
[README](README.md) — a segunda leitura das aulas 3, 6 e 7 rende bem mais do que
a primeira.
