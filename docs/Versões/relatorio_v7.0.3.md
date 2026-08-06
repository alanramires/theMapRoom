# v7.0.3 — A camada virou parâmetro, e a taxonomia destrancou

Fecha o dia 2026-08-03, a partir da `v7.0.2` tagueada de manhã.

O fio do dia foi dito pelo autor no fim, e explica retroativamente tudo o que
veio antes:

> *Estranho que eu terminei a migração do MelhorVisão para a Vigilância… mas
> isso destrancou a biologia, né? E eu sempre achei que biologia e taxonomia não
> serviam pra nada.*

É exatamente o que aconteceu, e vale enunciar o mecanismo porque ele diz o que
fazer a seguir: **taxonomia não serve para nada enquanto cada bicho tem órgão
próprio.** Enquanto `Capturer.Explorer`, `Transportador` e `VigilanciaAerea`
respondiam "para onde revelar" cada um à sua maneira, classificar papéis era
decorar nomes. No instante em que o órgão virou **um só** (`MelhorVisaoService`),
a pergunta muda:

```text
antes    "como ESTE aqui enxerga?"      →  implementação, uma por papel
depois   "este aqui EXPRESSA o órgão?"  →  coluna, e colunas viram tabela
```

A matriz de papéis não destrancou a biologia. **A unificação do primeiro órgão
destrancou.** O que sugere a ordem do resto: cada órgão unificado libera uma
coluna, e não o contrário.

---

## 1. Vigilância genérica — a camada deixou de ser identidade

Frente principal, e a única com código novo de peso. Fecha os passos 2 a 5 do
plano de cinco etapas; o passo 1 (`RaidAntiSub`) fechou na `a41ca17`.

### O que estava errado

O módulo era **aéreo por construção**, não por escolha. A porta de entrada
chamava-se `IsAirborneAirSurveillanceUnit` — o nome denunciava. Um radar móvel
que vigia o chão e um submarino que vigia a água executam a **mesma agenda**;
só a camada muda. Tratá-los como papéis distintos obriga a reescrever a agenda
por camada, que é a doutrina do projeto ao contrário.

### O que foi feito

`Units/Vigilancia Aerea/` → **`Units/Vigilancia/`**, preservando os GUIDs dos
`.meta` (renomear no disco sem recriar o arquivo — o Unity não perde referência).

O núcleo é um tipo novo, `SurveillanceProfile`, que carrega a camada resolvida
da ficha em vez de assumi-la:

```csharp
private readonly struct SurveillanceProfile
{
    public readonly VisionCoverageLayer Layer;
    public readonly bool DetectsStealth;
    public bool IsAirLayer => !Layer.IsAll && Layer.Domain == Domain.Air;
}
```

`TryResolveSurveillanceProfile` resolve a camada **principal** pela ficha, e
`IsAirLayer` passa a ser uma *pergunta*, não uma premissa. Toda a nomenclatura
seguiu: `TryDecideAirSurveillanceAction` → `TryDecideSurveillanceAction`,
`AirSurveillancePolicyStage` → `SurveillancePolicyStage`,
`LogAirSurveillancePolicyStage` → `LogSurveillancePolicyStage`.

`IsAirSurveillanceUnit` **foi preservado de propósito** — ele significa
"Vigilância cuja camada principal é Air", e é isso que interceptador, rally e
plataforma realmente precisam perguntar. Ao lado dele nasceu `IsSurveillanceUnit`
(qualquer camada), que é quem passou a governar iniciativa:

```csharp
// AIController.Initiative.cs:266
// Vigilancia (EWACS, radar movel, Super Tucano, fragata e submarino)
// age cedo para iluminar alvos na camada especializada da ficha.
if (IsSurveillanceUnit(unit)) return 1;
```

Antes, só o EWACS ganhava iniciativa alta. Agora a fragata também — e é a
correção que importa, porque iluminar antes de a artilharia gastar a ação é o
motivo de o papel existir.

### O `AirSurveillanceCoverageService` morreu

435 linhas removidas. Era protótipo parcial do que o `MelhorVisaoService` faz
completo. O ranking local passou a sair do serviço unificado; alvos operacionais
fora da hotzone continuam no `VisionCoverageService`.

**Total: −2.368 linhas, +273.** A generalização encolheu o código, o que é o
sinal de que a camada era mesmo duplicação e não especialização.

### O cuidado que quase passou

`MelhorVisaoRequest` ganhou um campo, e ele existe por um motivo específico:

```csharp
public Func<UnitManager, bool> AlliedObserverFilter;
```

Sem ele, **uma unidade comum que enxerga o mar faria a fragata acreditar que a
cobertura `Submerged` já está garantida** — e a fragata pararia de caçar. O
filtro deixa o consumidor dizer quais aliados cobrem *a mesma missão*, e nulo
preserva o comportamento geral da ferramenta. É política de consumidor num
serviço burro, exatamente onde deve morar.

### Tiro sem perder a posição

`TryDecideAirCombatAttackOnly` (90 linhas em `AIController.AirCombat.cs`)
reaproveita candidatos, `PodeMirar`, prioridades e regras de decolagem do combate
aéreo — mas **não** materializa a patrulha nem o fallback de movimento. Se não há
ataque legal, o `MelhorVisao` conserva a autoridade sobre onde a unidade termina
o turno.

Isso é a cadeia de raça mista aplicada de verdade: *arma primeiro, visão depois*,
e não uma média das duas. E o estado de pouso é restaurado em `finally`,
respeitando a invariante transacional.

### Verificado

`dotnet build Assembly-CSharp.csproj` — **0 erros**, 258 warnings pré-existentes.

**Não verificado: nada disso rodou no Unity.** Ver seção 4.

---

## 2. Matriz de papéis — a primeira linha levantada

Segunda frente, e a que gerou o vocabulário do dia.

### O levantamento do Capturador (`14b6613`)

19 arquivos, 5.823 linhas, contados coluna por coluna. Quatro achados:

- **o papel gasta mais código atirando do que capturando** — `Mirar` 38
  ocorrências contra 13 de `Capturar`, e o maior arquivo da pasta (`C.Attack`,
  899 linhas) é sobre combate;
- **`Embarcar` está espalhado por cinco arquivos, 1.287 linhas** — mais que a
  coluna de captura inteira, e três deles não referenciam sensor nenhum;
- **`Ver/Detectar` aparece em branco, mas a pergunta É respondida**: `C.Explorer`
  tem 462 linhas e seis constantes de peso próprias, com **zero** referências a
  `PodeDetectar`/`PodeEnxergar`;
- **`Fundir` é branco de verdade** — nenhuma referência ao `PodeFundirSensor` em
  5.823 linhas. Não é "não se aplica": infantaria fundir para se curar é mecânica
  central. É *ninguém decidiu ainda*, que é o que a matriz existe para tornar
  visível.

E a confirmação do modelo: os **dez modos** do capturador (Rogue, Defender,
Explorer, Blitzkrieg, PontaLanca, Opportunist, Agressive, Pursuer, Swap, Vacate)
já são o **degrau 4 materializado como arquivos**. A matriz não precisa criá-los
— precisa transformá-los em linhas que declaram só o que difere.

### O brainstorming das raças (`50d6822`)

O achado conceitual do dia:

> **"Não se aplica" nunca foi propriedade do papel — era propriedade da ficha.**

O capturador raiz não supre porque *aquele* `UnitData` não tem `isSupplier`.
Outro capturador pode ter. Três das quatro colunas marcadas "(não se aplica)" no
levantamento são, na verdade, raças esperando para existir:

| raça | coluna | estado |
|---|---|---|
| field medic | `PodeSuprir` | ficha resolve; falta política |
| peão (caixas na mochila) | `PodeTransferir` | idem |
| Kradschützen (moto com sidecar) | `PodeEmbarcar` pelo lado do veículo | idem |
| vigilante / spotter | `Ver/Detectar` | respondido à mão |

E **o mecanismo já existe** — o comentário do `CanSatisfy` já declara a doutrina
("capacidade mecânica é a fonte de verdade"). Um capturador com `isSupplier` **já
satisfaz `Logistica` hoje**; ninguém apenas lhe pergunta nada, porque o
`AIController.Capturer` nunca chega perto do `PodeSuprir`.

### Raças mistas — cadeia dentro da coluna

Duas do autor, e as duas ensinam coisas diferentes:

**Labradoodle** (capturador agressivo) — não faz média, faz **ordem**: tenta o
`PodeMirar` do Fire Support; sem solução, o do Assault. Já estava escrito em
`rascunho de governanca.md:554`: *"quando não encontra uma solução válida de
longo alcance, passa para Assalto"*. E a forma é **idêntica à lista de atração do
`MelhorCapitao`** — sequência ordenada, o primeiro que responde vence. O padrão
desenhado para "quem eu sigo" serve inteiro para "como eu luto".

**Caramelo** (porta-aviões) — transporte + supridor + estoquista + FS. Ensina que
mistura precisa de **essência**: `roles[0]` é o que a unidade é quando as colunas
discordam. E que as demais colunas não são secundárias — o porta-aviões supre de
verdade, só não deixa de ser transporte por causa disso.

**Consequência para a matriz:** uma célula deixa de ser *uma política* e passa a
poder ser *uma cadeia*. Três formas, e as três já rodam no projeto — política
única (`MelhorCaptura`), cadeia ordenada (`AICaptainData`) e herdada do parente
(`CanSatisfy`). **O vocabulário da matriz ficou completo antes de existir uma
matriz.**

---

## 3. O engenheiro — registrado, não construído

`docs/ideias_futuras.md` item 11. Entrou porque é a única raça que **não** cabe
na matriz sem obra.

> *Hoje o route manager monta uma estrada pronta. Mas se por um trecho oculto
> falta uma seção da ponte, o capturador engenheiro vai lá e ela aparece.*

Três coisas ficaram escritas, e a terceira é a que sumiria:

1. **É a única raça que pede coluna nova.** Não existe `PodeConstruir` —
   verificado, a pasta `Sensors/` vai de `PodeArremeter` a `PodeTransferir` sem
   nada de construção. Field medic, peão e Kradschützen reusam colunas
   existentes; o engenheiro não.

2. **A topologia da ponte já está pronta**, e é a parte difícil. `StructureData`
   já sabe que `Ponte + Mar` põe o convés acima da água (tanque em cima, navio
   embaixo), que `Ponte + Praia` é cabeceira sem vão, e que `Trilho + Ponte
   Ferroviária` são a mesma família topológica. **A seção faltante não é caso
   especial — é um nó ausente da família.**

3. **O engenheiro inverte plano e realidade:**

   ```text
   hoje         descobrir um vão na névoa  →  o plano era MENTIRA  →  replaneja
   engenheiro   descobrir um vão na névoa  →  o plano virou TAREFA
   ```

   É a primeira vez que a IA pode **tornar o próprio plano verdadeiro** em vez de
   ser corrigida pelo mundo. Para o planner não é feature — é uma **categoria
   nova de resposta**, ao lado de "replaneja" e "desiste".

E é a **segunda instância de um padrão já nomeado**: artilheiro pede *visão* que
não tem, engenheiro pede *terreno* que não tem. Os dois são um papel requisitando
precondição a outro papel — assunto do `governanca_entre_papeis.md`. O pedido de
spotter não era caso isolado; quem resolver um deve resolver o outro com a mesma
peça.

---

## 4. O que não terminou

**Nada da Vigilância rodou no Unity.** Compila e só. Falta observar as cinco
fichas — EWACS, Radar Móvel, Super Tucano, Fragata, Submarino — e conferir nos
logs a camada resolvida e o ganho de cobertura. Especificamente:

- a fragata realmente ganha iniciativa 1 e ilumina antes da artilharia?
- o `AlliedObserverFilter` impede mesmo que um aliado qualquer "satisfaça" a
  cobertura submarina?
- o `TryDecideAirCombatAttackOnly` devolve autoridade ao `MelhorVisao` quando não
  há tiro, ou a unidade congela?

**As chaves de captura seguem com fantasma.** `Tools > AI > Semear Chaves de
Captura` e depois `Auditar` — havia 11 fichas com entrada nula. Não rodou hoje.

**`roles[0] == CapturadorAgressivo` continua de pé** no `GetCapturePower`. Sai só
depois de as fichas agressivas trocarem para a chave `Capturador Alternativo`
(0.5). Ordem e risco no item 10 — o modo de falha é silencioso.

**Dois dos três "para onde revelar" continuam à mão.** A Vigilância migrou;
`Capturer.Explorer` (462 linhas) e `Transportador` não. São as próximas colunas
que o órgão unificado libera.

**`MelhorCapitao` continua sem consumidor** — falta o tradutor `AICaptainData →
List<MelhorCapitaoAttraction>` e os predicados (`AliadoFerido`,
`AeronaveInimigaDetectada`, `PontoDeObservacao`).

**A metade de IA do critério do jipe** nunca foi testada; só o lado do jogador.

---

## 5. O que este dia ensinou, e vale mais que o entregue

**Unificar órgão vem antes de classificar.** A matriz de papéis existia no papel
desde a manhã e não produziu nada. O que produziu foi a Vigilância consumindo o
`MelhorVisao` — porque só então "enxergar" virou coluna em vez de implementação.
A ordem correta do resto do trabalho é a mesma: unifique o órgão, e a linha da
matriz se escreve quase sozinha.

**Generalizar encolheu o código.** −2.368 / +273. Quando uma camada era mesmo
especialização, generalizar custa linhas; quando era duplicação, devolve. O saldo
negativo é a evidência de que a camada nunca foi identidade.

**O vocabulário chegou antes da estrutura.** As três formas de célula da matriz
(política única, cadeia ordenada, herança do parente) já existiam no projeto,
construídas para resolver outros problemas. A matriz não precisa inventar
mecanismo — precisa reconhecer o que já foi construído.
