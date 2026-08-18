# 06 — `ScriptableObject` e o ciclo de vida

> **Meta da aula:** entender os dois tipos de objeto Unity que você usa, e por
> que a doutrina central do `CLAUDE.md` é uma consequência técnica, não um gosto.

---

## Os três tipos de classe do seu projeto

Você tem 460 arquivos e essencialmente três categorias:

| categoria | quantos | herda de | tem arquivo próprio | vive |
|---|---:|---|---|---|
| **dado puro** | muitos | nada (`[System.Serializable]`) | não — mora dentro de outro | junto do dono |
| **catálogo** | 46 | `ScriptableObject` | **sim**, um `.asset` | fora da cena, sempre |
| **componente** | 60 | `MonoBehaviour` | não — mora numa cena/prefab | só enquanto a cena vive |

Mais 94 `static class` — os serviços burros e as `*Rules`, que não têm estado
nenhum e por isso não precisam existir como objeto.

### Dado puro

```csharp
[System.Serializable]
public class QuadranteData : INoDoMapa
```

Sem herança de Unity. Não pode ser arrastado no Inspector sozinho, não tem
arquivo. Ele **mora dentro** — no seu caso, dentro do `Mundo Fixture.asset`.
Apagou o dono, apagou ele.

### Catálogo

```csharp
[CreateAssetMenu(menuName = "Game/Skills/Skill Data", fileName = "SkillData_")]
public class SkillData : ScriptableObject
```

`ScriptableObject` é um objeto que **existe como arquivo, fora de qualquer cena**.

O `[CreateAssetMenu]` é o que põe a entrada no menu *Assets → Create → Game →
Skills*. Sem ele, a classe funciona, mas você não teria como criar uma instância
pela interface.

A propriedade que muda tudo: **um `ScriptableObject` é uma instância só,
compartilhada**. Se dez unidades apontam pro mesmo `SkillData`, elas apontam pro
**mesmo objeto na memória** — não pra dez cópias. É a aula 2: referência.

Daí saem duas consequências, uma boa e uma perigosa.

**A boa:** 35 unidades apontando pro mesmo `TerrainTypeData` custam a memória de
um. Trocar um valor no asset muda para todas de uma vez. É o que faz balanceamento
ser viável.

**A perigosa:**

> **Escrever num campo de `ScriptableObject` em tempo de execução altera o
> arquivo, e a alteração PERSISTE depois do Play no Editor.**

Se algum código fizer `unidade.data.custo -= 10`, você acabou de editar o asset.
No build o efeito é temporário; no Editor, fica gravado. É a origem clássica de
"o balanceamento mudou sozinho".

A defesa é tratar catálogo como **somente leitura em runtime**, sempre. Copie o
valor pro componente e altere a cópia.

### Componente

```csharp
public partial class AIController : MonoBehaviour
```

Vive pendurado num `GameObject` numa cena. Nasce com a cena, morre com a cena.

E é aqui que a doutrina fecha.

---

## Por que "o catálogo diz o que É, a cena diz onde ESTÁ"

Essa frase do `CLAUDE.md` parece filosofia. É engenharia, e o motivo cabe numa
linha:

```text
ScriptableObject   é UM arquivo, lido por TODAS as cenas.
cena               é UM tabuleiro.
```

Portanto: **qualquer coisa que só valha para um tabuleiro, se for guardada num
`ScriptableObject`, vaza para todos os outros.**

Foi exatamente o que a `v8.4.0` consertou. O `resumo.md`:

> *O asset "Rodovias" — que diz o que uma rodovia é — carregava onze traçados
> concretos. Toda cena que usasse o tipo herdava estrada de outro mapa, sem
> erro.*

Repare no **sem erro**. Coordenada de mapa A aplicada no mapa B só reclama se a
coordenada não existir lá. Se os dois mapas cobrem faixas parecidas, a estrada
fantasma aparece e nada avisa.

> **É por isso que o teste de aceitação do `CLAUDE.md` é um gesto e não uma
> conferência:** duplique a cena, aponte pros catálogos, e o mapa novo tem de
> nascer **vazio**. Se aparecer uma estrada, um catálogo está guardando layout.

Agora a versão prática, que você pode aplicar sozinho ao escrever qualquer campo
novo:

```text
Pergunte:  "se eu fizer um segundo mapa, este valor é o MESMO nos dois?"

MESMO  → catálogo (ScriptableObject).    "uma cidade custa 1000"
MUDA   → cena (componente).              "há uma cidade em (4,7)"
```

Coordenada absoluta dentro de um `ScriptableObject` é **sempre** o mesmo bug.

---

## O ciclo de vida do `MonoBehaviour`

O roadmap lista sete métodos. Na prática, quatro decidem tudo, e a **ordem** entre
eles é o que causa bug.

```text
Awake()        uma vez, ao objeto nascer. Antes de qualquer Start.
OnEnable()     toda vez que o objeto é ativado. Depois do Awake.
Start()        uma vez, antes do primeiro Update. DEPOIS de todos os Awake.
Update()       todo frame.
LateUpdate()   todo frame, depois de todos os Update.
OnDisable()    ao desativar. Espelho do OnEnable.
OnDestroy()    ao morrer.
```

### A regra de ouro da ordem

```text
Awake   →  arrume a SUA casa.       (cache de referência, inicializar campo)
Start   →  fale com os OUTROS.      (perguntar coisas a outros objetos)
```

O motivo: a Unity roda **todos** os `Awake` da cena antes de **qualquer**
`Start`. Então, no seu `Start`, todo mundo já se inicializou.

Se você perguntar algo a outro objeto no `Awake`, está apostando numa ordem que a
Unity não garante. Funciona hoje, quebra quando você arrastar o objeto pra outro
lugar da hierarquia. É um dos bugs mais difíceis de achar em Unity, porque ele
depende de algo que não está escrito em lugar nenhum.

### `OnEnable`/`OnDisable` — o par que **tem** de ser par

Este é o ponto que mais importa no seu projeto, e leva pra aula 7.

```csharp
void OnEnable()  { CursorController.OnCursorReturnedToNeutral += Refresh; }
void OnDisable() { CursorController.OnCursorReturnedToNeutral -= Refresh; }
```

`OnEnable` e `OnDisable` são chamados **muitas vezes** ao longo da vida do
objeto — toda ativação e desativação. `Awake` e `OnDestroy`, uma vez cada.

Por isso a assinatura de evento vai no par `OnEnable`/`OnDisable`, nunca em
`Awake`/`OnDestroy`: se você assinar no `Awake` e o objeto for desativado e
reativado, ele continua assinado o tempo todo — o que às vezes é o que você quer,
mas nunca é o que você espera.

E se você assinar no `OnEnable` e **esquecer** de desassinar no `OnDisable`, cada
ciclo de ativação adiciona mais uma assinatura. Depois de cinco, o método roda
cinco vezes por evento.

Sete arquivos seus assinam `OnCursorReturnedToNeutral`. Confira em cada um se o
par está fechado — é o E17 desta aula.

---

## O contrato do `Neutral`, e por que ele mora aqui

O `CLAUDE.md` traz esta obrigação:

> *Se o seu estado deriva do tabuleiro, você TEM de recalcular ao voltar para
> `Neutral` — e com `force`, furando cache.*

```csharp
CursorController.OnCursorReturnedToNeutral += … ;   // no OnEnable
RefreshAlgumaCoisa(force: true);                    // e SEMPRE com force
```

A razão está documentada, e vale reler à luz desta aula: o `ConstructionManager`
mantinha o escurecimento por `OnUnitOccupancyChanged`, que dispara **junto** com a
troca de célula — enquanto a unidade ainda está animando por cima do hex. O estado
era derivado da **posição visual**, não da célula lógica. E o cache guardava a
foto errada pra sempre.

Três coisas, e nenhuma errada sozinha:

```text
evento disparado no meio da animação
estado derivado da POSIÇÃO em vez da célula lógica
cache que economiza refresh comparando o valor errado
```

Traduzindo para o vocabulário desta aula: **escolher o evento errado para
assinar é um bug de ciclo de vida**, e ele não dá erro — dá um prédio preto para
sempre.

O `force: true` é obrigatório porque o cache é o que trava. Reconferir sem furar
o cache não reconfere nada.

---

## `[SerializeField]` — o que atravessa o Play

```csharp
[SerializeField] private MatchController matchController;
```

Serialização é o que a Unity grava no arquivo `.unity` ou `.asset`. As regras
que mordem:

| situação | serializa? |
|---|---|
| `public` campo | sim |
| `private` + `[SerializeField]` | sim |
| `private` sem atributo | **não** |
| `public` + `[System.NonSerialized]` | não |
| **propriedade** (`public int X => …`) | **NUNCA** |
| `Dictionary<,>` | **NUNCA** |

As duas últimas explicam decisões suas. `Dictionary` não serializa — é por isso
que `QuadranteData` guarda `List<TileBase>` achatado em row-major em vez de um
dicionário de coordenada→tile. Não foi escolha de performance: era a única opção.

E a consequência mais importante:

> **Renomear um campo serializado desconecta o valor gravado.** O `.unity` guarda
> por nome. Renomeou, a Unity não acha, o campo nasce vazio — sem erro de
> compilação, sem aviso.

A defesa existe e você já usa:

```csharp
[FormerlySerializedAs("nomeAntigo")]
[SerializeField] private int nomeNovo;
```

É a razão de o `CLAUDE.md` proibir mexer em `AIController.cs`: aquele arquivo é
todo `[SerializeField]`, e cada nome ali é uma amarra com a cena.

---

## Exercício

**E16.** Abra [Assets/Scripts/Skills/SkillData.cs](Assets/Scripts/Skills/SkillData.cs)
— são 33 linhas, quase todas comentário. Responda:

1. Por que essa classe é `ScriptableObject` e não `[System.Serializable]`?
2. O comentário conta que existiu ali um `canCaptureConstructions`, removido na
   v7.0.2. **Usando o que esta aula ensinou sobre compartilhamento**, explique por
   que aquele campo era pior que um campo equivalente em `ConstructionData`.

**E17.** Busque `OnCursorReturnedToNeutral` (`Ctrl+Shift+F`). São sete arquivos.
Em cada um, confira se o `+=` tem um `-=` correspondente, e se estão em
`OnEnable`/`OnDisable`.

Faça uma tabela: arquivo | assina onde | desassina onde | par fechado?

Se achar algum aberto, **não conserte** — traga. Pode ser intencional, e o motivo
importa.

**E18.** Escolha um `ScriptableObject` do projeto que **não** seja `SkillData`.
Para cada campo dele, responda: *"se eu fizer um segundo mapa, este valor é o
mesmo?"* Se algum for "muda", você achou um vazamento de layout — anote arquivo e
campo.

(O `resumo.md` diz que `fieldEntries` e `roadRoutes` já foram tratados. Se você
achar um terceiro, é achado novo.)

Gabarito em [exercicios.md](exercicios.md).

---

Próxima: [07 — Eventos](07_eventos.md).
