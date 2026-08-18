# 01 — Anatomia de um arquivo

  

> **Meta da aula:** abrir [Assets/Scripts/Campanha/QuadranteData.cs](Assets/Scripts/Campanha/QuadranteData.cs)
> e não pular nenhuma linha. Nenhuma.

Escolhi esse arquivo porque ele é pequeno (104 linhas), é **seu** e é recente — saiu da frente de campanha, que é onde você parou. E porque ele contém, num espaço curto, quase tudo do capítulo 1 e 2.1 do roadmap.  

Abra ele agora, ao lado desta aula.

---
## Linha 1-3 — `using`

```csharp

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

```
  
Um `using` **não importa código**. Não copia nada, não carrega nada, não custa nada em tempo de execução. Ele só diz ao compilador: *"quando eu escrever `List`, procure em `System.Collections.Generic`"*.

`List<TileBase>` sem o `using` viraria `System.Collections.Generic.List<UnityEngine.Tilemaps.TileBase>`. O `using` é economia de digitação, e mais nada.

>[!important]
>## `Tilemap` ? `TileBase`
>
>**Tilemap:**    o COMPONENTE na cena. A grade em si, que guarda "célula (4,7) tem tal tile".
>           É o objeto que você pinta.
>           
>**TileBase:**   o PINCEL. Um asset da paleta — "Floresta", "Montanha", "Oceano".
>           É com o que você pinta.
> 

**Como saber qual `using` falta:** o erro `CS0246: The type or namespace name 'List' could not be found` é sempre isso. No VS Code, o cursor em cima do nome vermelho + `Ctrl+.` oferece o `using` certo.

**Namespace** é o outro lado da moeda: é o sobrenome de um tipo, pra dois tipos com o mesmo nome poderem coexistir. Repare que **suas classes não têm namespace** — `QuadranteData` mora no namespace global. É comum em projeto Unity de um autor só, e é uma decisão com consequência: se você um dia importar um pacote que tenha uma classe `QuadranteData`, os dois colidem. Enquanto o jogo for seu, tudo bem.

> [!note] Namespace
> Namespace é como um “grupo do WhatsApp” das classes. :D

---
## Linha 5-21 — o comentário XML

```csharp
/// <summary>
/// Um quadrante: o retangulo recortavel do mapa de campanha, onde se luta.
/// ...
/// </summary>
```

Três barras (`///`) em vez de duas fazem um **comentário de documentação**. A diferença prática: passe o mouse por cima de `QuadranteData` em qualquer outro arquivo e o VS Code mostra esse texto. Duas barras não fazem isso.  

Repare no que você escreveu ali dentro:  

```csharp
/// NAO confundir com <see cref="ConstructionSector"/> — "setor" ali e rotulo
```

`<see cref="…"/>` marca que aquilo é um **símbolo**, não texto solto. No VS Code ele **não** vira link clicável — para visitar, o caminho é o `F12` em cima do nome, igual a qualquer outro símbolo. (Em Visual Studio cheio, aí sim é link.)

O que o `cref` te dá aqui é mais discreto: o **`F2` (renomear símbolo) atualiza o `cref` junto**, então o comentário não envelhece quando o tipo muda de nome.

>[!warning] O compilador NÃO confere o cref neste projeto
>Existe um aviso para `cref` quebrado (`CS1574`), mas ele só aparece com a geração de XML doc **ligada** — e o `Assembly-CSharp.csproj` que a Unity gera não liga. Ligar não adianta: a próxima regeneração apaga.

**Isto aqui é uma força sua, e vale nomear.** A maioria dos programadores documenta *o quê*. Você documenta *por quê*:  

```csharp
/// O bake guarda TileBase direto em vez de um id de terreno porque o jogo ja
/// resolve terreno a partir do tile (TerrainDatabase.TryGetByPaletteTile). Uma
/// tabela de traducao no meio so criaria uma segunda fonte pra divergir.
```

Esse parágrafo responde uma pergunta que o código não responde: *por que não tem um id aqui?* Daqui a um ano, ele é o que impede você de "consertar" para pior.

>[!note] Sobre o atalho 
>CTRL+SHIFT+P na opção >"Developer: Reload Window" é muito útil para dar reload no projeto
>F12: leva ate o arquivo
>Shift+F12: quem usa esse **símbolo** (não o arquivo — ele lista cada chamada)

---
## Linha 22-23 — o atributo e a declaração

```csharp
[System.Serializable]
public class QuadranteData : INoDoMapa
```

Três coisas em duas linhas.
### `[System.Serializable]`

Um **atributo**: metadado colado numa declaração, entre colchetes. Não executa nada sozinho. Alguém lê ele depois e muda de comportamento.  

Aqui, quem lê é a Unity. `[System.Serializable]` significa: *"esta classe pode ser gravada em disco e desenhada no Inspector"*. Sem ela, um `QuadranteData` dentro de um `ScriptableObject` **não seria salvo** — você preencheria no Inspector, fecharia a Unity, e voltaria vazio. Sem erro nenhum.

> Regra prática: classe `[System.Serializable]` = dado puro. Classe `ScriptableObject` ou `MonoBehaviour` = objeto Unity, com identidade e arquivo próprio. `QuadranteData` é do primeiro tipo: ele **mora dentro** do `Mundo Fixture.asset`, não tem arquivo próprio.


>[!note] **O que serializar significa**
>O valor é gravado **dentro** do arquivo da cena (ou do prefab / do asset). Um valor por cena. 25 cenas com o mesmo script = 25 valores independentes.
> Serializar não é compartilhar — é o oposto. É dar a cada cena a sua própria cópia.
### `public class`

`public` é o **modificador de acesso**. Os que importam:  

| modificador | quem enxerga          | onde você usa                               |
| ----------- | --------------------- | ------------------------------------------- |
| `public`    | todo mundo            | quase tudo aqui                             |
| `private`   | só a própria classe   | campos `[SerializeField]` do `AIController` |
| `protected` | a classe e quem herda | raro no seu código (herança rasa)           |
| `internal`  | o mesmo assembly      | você não usa                                |
Sem modificador, o padrão de um membro de classe é `private`. Escrever `private` explícito é preferência — você faz isso no `AIController` e não faz aqui. Não é inconsistência grave, mas escolher um e manter ajuda.

>[!note] Private vs Public
>Responde **quem no código** pode ler e escrever.
>```csharp
private float musicVolume;   // só esta classe
public  float musicVolume;   // qualquer arquivo do projeto
>```
>E só isso. `public` **não** torna nada global, compartilhado ou permanente. É uma porta, não um cofre.

---
# Ponto de Parada do Estudo
---

### `: INoDoMapa`

  

Os dois-pontos significam *"herda de"* ou *"implementa"*. Como `INoDoMapa` é uma

**interface** (o `I` na frente é convenção, não regra da linguagem), aqui é

implementação.

  

Uma interface é um **contrato sem corpo**: ela lista o que a classe tem de

oferecer, sem dizer como. Veja

[Assets/Scripts/Campanha/INoDoMapa.cs](Assets/Scripts/Campanha/INoDoMapa.cs) — só

assinaturas.

  

E você mesmo escreveu por que ela existe:

  

> *"A interface existe pra ferramenta desenhar UM renderizador de nível em vez de

> três quase iguais."*

  

Guarde essa frase. É o **único** motivo bom para criar uma interface: você tem N

coisas parecidas e um código que quer tratar as N do mesmo jeito. Interface criada

"para deixar flexível", sem N concreto, é peso morto.

  

---

  

## Linha 25-39 — campos, e os atributos de Inspector

  

```csharp

[Header("Identidade")]

public string quadranteId = "Q1";

public string displayName = "Quadrante";

[TextArea(2, 4)] public string descricao;

```

  

**Campo** é uma variável que pertence ao objeto. Cada `QuadranteData` tem o seu

`quadranteId`.

  

O `= "Q1"` é **inicializador de campo**: roda quando o objeto nasce. Repare que

`descricao` não tem — então nasce `null`, porque `string` é tipo de referência

(aula 2). Um `string` sem valor é `null`, não `""`. É a origem clássica de

`NullReferenceException`, e é por isso que `descricao` só é lida com cuidado.

  

Os atributos aqui são todos **cosméticos, do Inspector**:

  

| atributo | efeito |

|---|---|

| `[Header("…")]` | título em negrito acima do campo |

| `[TextArea(2, 4)]` | caixa de texto de 2 a 4 linhas em vez de uma |

| `[Tooltip("…")]` | texto ao passar o mouse |

| `[Min(1)]` | o Inspector recusa valor abaixo de 1 |

  

```csharp

[Min(1)] public int width = 18;

```

  

`[Min(1)]` **não é validação de código**. Ele impede a digitação no Inspector.

Se outro script fizer `quadrante.width = -5`, passa liso. Por isso o

`ContainsCampaignCell` e o `GetBakedTile` conferem limites de novo, na mão:

  

```csharp

if (localX < 0 || localX >= width || localY < 0 || localY >= height)

    return null;

```

  

> **Atributo de Inspector protege o autor. Guarda em código protege o programa.**

> Você precisa dos dois, e eles não se substituem.

  

---

  

## Linha 41-49 — implementação explícita de interface

  

Esta é a parte mais avançada do arquivo, e vale devagar.

  

```csharp

string INoDoMapa.Id { get => quadranteId; set => quadranteId = value; }

```

  

Leia da esquerda: *"a propriedade `Id`, **do contrato `INoDoMapa`**, é do tipo

`string`; ler devolve `quadranteId`, escrever grava em `quadranteId`"*.

  

**Propriedade** é um par de métodos disfarçado de campo. `get` roda na leitura,

`set` roda na escrita, e `value` é a palavra reservada que carrega o que foi

atribuído. Do lado de fora parece variável; por dentro é código.

  

O que torna isto **explícito** é o prefixo `INoDoMapa.`. A consequência é

concreta e vale testar no editor:

  

```csharp

QuadranteData q = ...;

q.quadranteId       // ? compila

q.Id                // ? NÃO compila — Id só existe pelo contrato

  

INoDoMapa no = q;

no.Id               // ? compila

no.quadranteId      // ? NÃO compila — o contrato não tem esse campo

```

  

Por que fazer isso? Porque você tem **dois públicos diferentes** para o mesmo

dado:

  

```text

o Inspector e o resto do jogo    querem  quadranteId   (nome específico, honesto)

o MapHelperWindow genérico       quer    Id            (nome do contrato)

```

  

A implementação explícita serve os dois **sem poluir o autocomplete de ninguém**.

Quem trabalha com quadrante vê `quadranteId`; quem trabalha com nó genérico vê

`Id`. Você não inventou duas cópias do dado — inventou duas portas para ele.

  

Isso é um padrão de programador experiente. Você o aplicou.

  

---

  

## Linha 51-64 — o bloco assado, e um atributo que é aviso

  

```csharp

[Header("Assado — artefato, nao editar a mao")]

[Tooltip("Row-major: indice = (y * width) + x. Null significa buraco, e buraco e valido.")]

public List<TileBase> bakedTiles = new List<TileBase>();

```

  

`List<TileBase>` — o `<TileBase>` é **genérico**: uma lista *de* `TileBase`. O

compilador garante que só entra `TileBase` ali. Antes dos genéricos existia

`ArrayList`, que aceitava qualquer coisa e explodia em execução. Não use.

  

`new List<TileBase>()` na declaração é importante: sem ele, `bakedTiles` nasce

`null` e o primeiro `.Add()` lança `NullReferenceException`. Você faz isso em

todas as listas do arquivo. Bom hábito, mantido.

  

**"Row-major: índice = (y * width) + x"** é a técnica de guardar uma grade 2D numa

lista 1D. A Unity não serializa `List<List<T>>` nem array 2D — então achatar é

obrigatório, não escolha. A conta reaparece na linha 85:

  

```csharp

return bakedTiles[(localY * width) + localX];

```

  

**As duas contas têm de ser a mesma, sempre.** Se uma virar `(x * height) + y`, o

mapa sai transposto sem nenhum erro. É a razão de a conta estar escrita no

`[Tooltip]`.

  

---

  

## Linha 66-75 — propriedades calculadas

  

```csharp

public int CellCount => Mathf.Max(0, width) * Mathf.Max(0, height);

```

  

Isto é uma **propriedade só de leitura, corpo de expressão**. Forma longa

equivalente:

  

```csharp

public int CellCount

{

    get { return Mathf.Max(0, width) * Mathf.Max(0, height); }

}

```

  

O `=>` (*fat arrow*) é só açúcar. Mas a diferença **campo vs propriedade** aqui é

real e vale entender:

  

```text

campo          guarda um valor.        Ler é grátis.

propriedade    calcula um valor.       Ler EXECUTA CÓDIGO, toda vez.

```

  

`CellCount` não existe em lugar nenhum na memória — ele é recalculado a cada

leitura. Para duas multiplicações, irrelevante. Mas a mesma sintaxe pode esconder

trabalho pesado, e aí ler uma "propriedade" dentro de um laço vira gargalo

silencioso. Já mordeu você: o `project_fow_full_refresh_per_move` na memória é

exatamente isso, em escala maior.

  

> **Regra de leitura:** ao ver `algo.Coisa`, olhe se `Coisa` é campo ou

> propriedade antes de colocar num laço. `F12` em cima do nome responde.

  

Agora o `HasBake`:

  

```csharp

public bool HasBake =>

    width > 0

    && height > 0

    && bakedTiles != null

    && bakedTiles.Count == CellCount;

```

  

O `&&` é **avaliação em curto-circuito**: para no primeiro `false`. Isso não é

detalhe de performance — é o que torna a linha **correta**. Se `bakedTiles` for

`null`, o `bakedTiles != null` dá `false` e o `.Count` **nunca roda**. Trocar

`&&` por `&` faria as quatro rodarem sempre, e a quarta lançaria exceção.

  

A ordem dos testes num `&&` é, muitas vezes, a guarda em si.

  

---

  

## Linha 100-103 — `override`

  

```csharp

public override string ToString()

{

    return $"{quadranteId} ({originX},{originY}) {width}x{height}";

}

```

  

`ToString()` já existe — **todo** tipo em C# herda de `object`, que tem

`ToString()`. `override` diz: *"a versão de `object` não serve, use a minha"*.

  

Sem esse `override`, um `Debug.Log(quadrante)` imprimiria `QuadranteData` e mais

nada. Com ele, imprime `A_IA_Q1 (-18,10) 16x17`. É o investimento de debug mais

barato que existe: três linhas, e todo log que tocar num quadrante fica legível.

  

O `$"…"` é **interpolação de string**: o que está dentro de `{}` é avaliado e

convertido. `$"{width}x{height}"` é o mesmo que

`width.ToString() + "x" + height.ToString()`, mais legível.

  

---

  

## Recapitulando o arquivo inteiro

  

```text

using               atalho de nome, custo zero

///                 documentação; <see cref=""/> marca símbolo (F2 renomeia junto)

[Atributo]          metadado; alguém lê depois e muda de comportamento

class : Contrato    implementa interface — só crie uma quando houver N iguais

campo               guarda valor; sem inicializador, referência nasce null

[Header]/[Min]      protegem o AUTOR no Inspector, não o programa

Tipo.Membro { }     implementação explícita: duas portas, um dado

List<T>             genérico; sempre inicialize com new

=>                  corpo de expressão; propriedade EXECUTA a cada leitura

&&                  curto-circuito — a ordem dos testes é a guarda

override            substitui método herdado; ToString() é o melhor custo-benefício

$"{}"               interpolação

```

  

---

  

## Exercício

  

**E1.** Abra [Assets/Scripts/Campanha/QuadranteData.cs](Assets/Scripts/Campanha/QuadranteData.cs)

e responda **sem rodar o jogo**:

  

1. Se `bakedTiles` tiver 272 itens e `width`/`height` forem 16 e 17, o que

   `HasBake` devolve? E se `width` virar 17?

2. `GetBakedTile(15, 16)` num quadrante 16×17 — que índice da lista ele acessa?

3. Por que `ContainsCampaignCell` usa `<` no limite de cima e `>=` no de baixo?

   O que aconteceria com `<=`?

  

**E2.** Adicione ao `QuadranteData` uma propriedade só de leitura chamada

`BakeIdade`, que devolva quantos dias se passaram desde `bakedAtUtcTicks`.

  

Dicas: `new System.DateTime(bakedAtUtcTicks)` constrói a data;

`System.DateTime.UtcNow` é agora; subtrair duas datas dá um `TimeSpan`, que tem

`.TotalDays`. Cuidado com `bakedAtUtcTicks == 0` (nunca assado).

  

Use corpo de expressão se couber em uma linha. Se não couber, use `{ get { … } }`

— e isso é uma resposta legítima, não uma derrota.

  

**E3.** Não escreva código. Só procure: encontre no projeto **outra** classe

`[System.Serializable]` que não seja `MonoBehaviour` nem `ScriptableObject`.

Comece por [Assets/Scripts/Campanha/](Assets/Scripts/Campanha/). Depois responda:

onde é que os dados dela ficam gravados, já que ela não tem arquivo próprio?

  

Gabarito em [exercicios.md](exercicios.md).

  

---

  

Próxima: [02 — Valor vs referência](02_valor_vs_referencia.md), onde o

`cell.z = 0` finalmente faz sentido.