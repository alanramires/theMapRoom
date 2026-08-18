# 03 — `partial class` e como achar o arquivo certo

> **Meta da aula:** dado um comportamento do jogo, achar sozinho o arquivo que o
> decide — em menos de dois minutos, sem perguntar pra mim.

Esta é a aula que mais mexe no ponteiro do seu problema. As outras ensinam C#;
esta ensina a **entrar** no monstro.

---

## O que é `partial`

Uma classe C# normalmente mora num arquivo. `partial` desfaz essa amarra:

```csharp
// arquivo A
public partial class AIController : MonoBehaviour
{
    private int contador;
}

// arquivo B
public partial class AIController
{
    private void Somar() => contador++;
}
```

O compilador **cola os dois** antes de compilar. Depois disso não existe arquivo
A nem arquivo B: existe uma classe `AIController` com um campo e um método.

Três consequências que importam na prática:

1. **Não há isolamento nenhum.** Código no arquivo B enxerga `private` do arquivo
   A. `partial` não é módulo, não é encapsulamento — é só recorte de texto.
2. **A herança e a interface se declaram uma vez só.** Repare em
   [AIController.cs:29](Assets/Scripts/Match/AI/AIController.cs) —
   `: MonoBehaviour` aparece ali, e nos outros 100 arquivos é só
   `public partial class AIController`.
3. **O compilador não liga pra ordem nem pro nome dos arquivos.** Tudo que o nome
   do arquivo faz é ajudar **você**.

Esse terceiro ponto é a aula inteira.

---

## Os 101 arquivos, e o índice escondido neles

`AIController` está espalhado em 101 arquivos. Isso assusta até você reparar que
os nomes formam uma árvore:

```text
Match/AI/
├── AIController.cs                    campos serializados e propriedades. SÓ ISSO.
├── AIController.Router.cs             quem decide qual papel age
├── AIController.Initiative.cs         quem age primeiro
├── AIController.Batches.cs            como uma ação vira lote executável
├── AIController.AttackDecision.cs     a decisão de atacar
├── AIController.Lifecycle.cs          liga/desliga, assinatura de eventos
├── AIController.WorldCommit.cs        commit do mundo
├── AIController.Debug.cs              logs e ferramentas
│
├── 1. Phases/      (6)     Phase0…Phase4 + Phases — o loop do turno
├── 2. Planner/    (10)     o plano de objetivos
├── 3. Shopping/            compras
│
└── Units/                  UM SUBDIRETÓRIO POR PAPEL
    ├── Capturer/       (18)
    ├── Transport/      (18)
    ├── Fire Support/   (10)
    ├── Assault/         (7)
    ├── Logistics/       (7)
    ├── Repair/          (6)
    ├── Air/ Vigilancia/ Stock/
```

E dentro de `Capturer/`, os nomes viram um índice de comportamento:

```text
AIController.Capturer.cs                    a porta de entrada
AIController.Capturer.Helpers.cs            utilidades compartilhadas
AIController.Capturer.Attack.cs             ataque
AIController.Capturer.Swap.cs               troca de posição
AIController.Capturer.Vacate.cs             desocupar hex

AIController.Capturer.Agressive.cs      ┐
AIController.Capturer.Defender.cs       │
AIController.Capturer.Explorer.cs       ├── as fichas de papel
AIController.Capturer.Opportunist.cs    │   (o "6-role model" da memória)
AIController.Capturer.Pursuer.cs        │
AIController.Capturer.Rogue.cs          ┘

AIController.Capturer.Embark.cs             ┐
AIController.Capturer.Embark.Scan.cs        │  embarque, quebrado
AIController.Capturer.Embark.Pathing.cs     ├─ em cinco pedaços
AIController.Capturer.Embark.Extended.cs    │
AIController.Capturer.Embark.Transporter.cs ┘
```

> **O nome do arquivo é a única documentação que nunca fica desatualizada** —
> porque renomear o arquivo é uma ação deliberada, enquanto comentário apodrece
> sozinho.

Você construiu um índice sem chamar de índice. Ele funciona. **Mantenha a
convenção `AIController.<Área>.<Sub>.cs` religiosamente** — no dia em que um
arquivo se chamar `AIControllerNovo2.cs`, o índice começa a morrer.

---

## As quatro técnicas de navegação

> **Antes de tudo: confira se a navegação funciona.** Cursor em cima de um tipo
> qualquer, `F12`. Se pular pro arquivo da definição, siga em frente. Se disser
> *"No definition found"*, o servidor de C# não carregou o projeto e **nenhuma**
> das técnicas abaixo vai funcionar.
>
> O diagnóstico está no Output (`Ctrl+Shift+U`) → canal **C#**: se aparecer
> `Temp/roslyn-canonical-misc/.../Canonical.csproj` e nenhuma menção aos
> `.csproj` do projeto, ele está lendo arquivo por arquivo, sem enxergar os
> outros 459. A causa conhecida aqui foi `dotnet.defaultSolution` apontando pro
> `.slnx` em vez do `.sln`, em `.vscode/settings.json`.


Aqui está o que fazer no VS Code, em ordem de utilidade real.

### 1. `F12` — ir para a definição

Cursor em cima de qualquer nome, `F12`. Vai direto pra onde ele foi declarado, em
qualquer um dos 460 arquivos.

É a técnica que resolve 70% das dúvidas: *"que diabos é `TryDecideCapturerAction`?"*
→ `F12` → você está lá.

`Alt+←` volta. Use bastante — navegar fundo e voltar é o movimento normal.

### 2. `Shift+F12` — quem me chama

Esta é a mais importante, e a que quase ninguém usa o suficiente.

`F12` responde *"o que isso faz?"*. `Shift+F12` responde **"o que quebra se eu
mudar isso?"** — que é a sua pergunta, a que você me disse que não sabe
responder.

Exemplo concreto. Você quer mexer em `HasBake` do `QuadranteData`. Antes de
tocar:

```text
cursor em HasBake  →  Shift+F12  →  lista todos os lugares que dependem dele
```

Se a lista tem 2 itens, mexa à vontade. Se tem 30, leia os 30 antes.

**Rotina que eu recomendo virar hábito:** nunca edite um membro `public` sem
rodar `Shift+F12` primeiro. Custa cinco segundos e é o que separa "mudei uma
coisa" de "quebrei três sistemas".

### 3. `Ctrl+T` — buscar símbolo pelo nome

Você lembra que existe um método com "Embark" no nome, mas não onde.
`Ctrl+T`, digite `Embark`, e o VS Code lista todo símbolo do projeto que casa.

Funciona por pedaço: `TryDecideCap` acha `TryDecideCapturerAction`. Digitar em
maiúsculas iniciais também: `TDCA` costuma achar.

Diferença pro `Ctrl+P` (buscar **arquivo**): `Ctrl+T` acha método e classe,
`Ctrl+P` acha arquivo. Com sua convenção de nomes, `Ctrl+P` é surpreendentemente
poderoso — `Ctrl+P` + `Capturer.Emb` te leva na hora.

### 4. Busca em texto — quando o símbolo não basta

`Ctrl+Shift+F`. Serve para o que `F12` não alcança:

- **texto de log**: você viu `[AI][T3][Embark]` no Console e quer a linha que
  emitiu. Busque `"Embark"` com aspas.
- **nome em string**: `MenuItem("Tools/Utils/Map Helper")` — nenhum símbolo leva
  até ali.
- **contar ocorrências**: "quantos lugares usam isso?" quando `Shift+F12` falha
  (acontece em `partial` grande e em código dentro de `#if`).

---

## O método: do sintoma ao arquivo

Este é o procedimento. Decore-o.

```text
1. Qual PAPEL?          capturador, transporte, fire support…
                        → escolhe a pasta em Units/

2. Qual MOMENTO?        planejar, escolher unidade, mover, atacar, comprar
                        → escolhe entre Planner/, Phases/, Batches

3. Ctrl+P com o palpite do nome do arquivo
   ou Ctrl+T com o palpite do nome do método

4. Achou? → Shift+F12 pra ver quem chama, e SUBIR até reconhecer a entrada.
```

### Exemplo trabalhado

**Sintoma:** *"o capturador embarcou no APC quando não devia."*

```text
1. papel     → Capturer/
2. momento   → decisão de ação, especificamente embarque
3. Ctrl+P    → "Capturer.Embark"
             → cinco arquivos aparecem; comece pelo mais curto, o sem sufixo:
               AIController.Capturer.Embark.cs
4. lá dentro → ache TryDecideCapturerEmbarkAction
   Shift+F12 → o CLAUDE.md já avisa que ele é chamado
               "near the top of TryDecideCapturerAction"
             → confirmado, você entendeu o caminho
```

Levou menos de dois minutos, e em nenhum momento foi preciso ler 101 arquivos.

**O truque é que você nunca lê a classe.** Você lê **um caminho** dentro dela.

---

## Quando `partial` está te ajudando e quando está escondendo

`partial` é uma faca boa com um gume ruim.

**Ajuda quando** o recorte é por *assunto* e o nome do arquivo diz o assunto.
`AIController.Capturer.Vacate.cs` — você sabe o que tem lá sem abrir. Isso é o
seu caso na maioria esmagadora.

**Esconde quando:**

| sintoma | o que está acontecendo |
|---|---|
| você não acha onde um campo é escrito | qualquer um dos 101 arquivos pode escrever nele — `private` não protege nada aqui |
| dois arquivos mexem no mesmo estado sem saber | o `plannedDestinations` do `CLAUDE.md` é exatamente esse risco |
| um arquivo precisa de "helpers" de outro | acoplamento que a pasta esconde |

A defesa contra o primeiro caso é `Shift+F12` **no campo**, não no método. Um
campo `private` de classe `partial` tem escopo de 101 arquivos — trate como
público e confira sempre.

> A pergunta que separa recorte bom de recorte ruim: *se eu apagar este arquivo,
> o que o jogo deixa de saber fazer?* Se a resposta é uma frase clara ("o
> capturador deixa de desocupar hex"), o recorte é bom. Se é "sei lá, várias
> coisas", o recorte é por conveniência de tamanho, e vai doer.

---

## O caso especial: `AIController.cs` é o **único** que você não edita

O `CLAUDE.md` é explícito:

> *Never add new logic directly to `AIController.cs` (the root file only holds
> serialized fields and properties).*

E a regra é boa. Aquele arquivo é o **contrato com o Inspector**: os
`[SerializeField]` dele são o que está arrastado na cena. Mexer nele tem risco de
cena, não só de código — renomear um campo serializado desconecta o valor que
está gravado no `.unity`.

```csharp
// AIController.cs:52
[InspectorName("IA Rapida")]
[SerializeField] private bool iaRapida = true;
public bool IARapida => iaRapida;
```

Repare no padrão, que se repete dezenas de vezes ali: **campo `private`
serializado + propriedade `public` só de leitura**.

É o encapsulamento certo pra Unity, e vale entender por quê:

```text
private  + [SerializeField]   →  o Inspector escreve. Mais ninguém.
public   propriedade get-only →  os 100 outros arquivos leem. Ninguém escreve.
```

Se `iaRapida` fosse `public` direto, qualquer um dos 101 arquivos poderia mudá-la
em execução, e você teria um valor no Inspector que não corresponde ao que está
rodando. O par fecha essa porta **em tempo de compilação**.

Você já faz isso. Agora sabe nomear.

---

## Exercício

**E7. (o principal)** Sem me perguntar, e cronometrando: encontre o arquivo e o
método que decidem **se a IA compra uma unidade de transporte**.

Pistas legítimas: o `CLAUDE.md` fala de `AIShoppingPlanner.Decide` e de
`MinDistanceForTransportSlot`. Use `Ctrl+T`.

Anote: quanto tempo levou, e quais das quatro técnicas você usou.

**E8.** Rode `Shift+F12` em cima de `IARapida` (a propriedade em
[AIController.cs:53](Assets/Scripts/Match/AI/AIController.cs)). Quantos lugares
leem? Escolha um e leia o trecho — o que muda no jogo quando ela é `false`?

**E9.** Escolha um dos 18 arquivos de `Units/Capturer/` que você **nunca** abriu.
Abra, leia só as assinaturas de método (ignore os corpos) e escreva **uma frase**
respondendo: *se eu apagasse este arquivo, o que o jogo deixaria de saber fazer?*

Se você não conseguir escrever a frase, o recorte daquele arquivo é ruim — e isso
é uma descoberta útil, não uma falha sua. Anote qual foi.

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [04 — Coleções](04_colecoes.md).
