# Fundação, desacoplamento e generalização do uso dos sensores

## Versão

`v7.0.0`

## Objetivo

Esta é a versão em que a arquitetura da IA foi **decidida e escrita**, e em que a
ferramenta que a torna observável ficou pronta. Quase nada mudou no jogo — e isso
é o ponto: a camada de IA está sendo reorganizada em cima de um sistema que já
funciona sem ela.

O salto de major não é por volume de código. É porque **o critério de onde cada
coisa mora mudou**, e ele agora vale para tudo o que vier depois: o jipe
capturador, o robô da morte capturador, a marinha, o antiaéreo.

A descoberta central da versão é o inverso do que se esperava encontrar:

> **O desacoplamento já estava metade feito, e ninguém sabia.**

---

## 1. Seis contratos de doutrina

`docs/AI Behavior/` ganhou a camada que faltava — não a dos papéis, que já
existia, mas a que fica **acima** e a que fica **entre** eles:

| documento | o que fixa |
|---|---|
| `governanca.md` | o que vale para **toda** unidade: upkeep, as 5 ordens, o ciclo posicionar→agir, as ações `PodeX`, os sensores de sistema, visão e detecção, a Hotzone, os papéis e a hierarquia |
| `governanca_entre_papeis.md` | as **arestas**: os três tipos de governo e o contrato do Comportamento Magnético |
| `Transporte.md` | reescrito como contrato completo — 16 seções |
| `Capturador.md`, `Assalto.md`, `FireSupport.md` | já existiam; agora fecham com os dois de cima |

Todos com o mesmo esquema (✅ conferido / ⚠️ diverge / ❌ não existe / ❓ não
conferido) e **cada regra conferida no código antes de virar manual**.

O contrato de governança passou por **quatro reescritas** do autor na mesma
sessão. Duas pendências fecharam por reescrita, não por código: `ApenasMover` não
é sensor porque o contrato passou a dizer que é ação; os sensores de sistema
viraram 7 quando o `PodeSubmergirRapidamente` entrou na lista.

---

## 2. Os três tipos de governo

`governanca_entre_papeis.md` separa o que "governar" quer dizer, porque
confundi-los é o que produz o mesmo bug repetido:

| tipo | governa | exemplo |
|---|---|---|
| **magnético** | o **onde** — A vira âncora de B | capturador é capitão do assalto |
| **por agenda** | o **para quê** — A adota o objetivo de B | transporte: quem embarca primeiro assume o volante |
| **por exclusão** | o **onde não** — A é definido por onde B está | fire support não pode estar na vanguarda, e vanguarda é onde o assalto está |

E a consequência operacional: **as arestas determinam a ordem de refactor.** Foi o
que explicou, depois do fato, por que o naval está preso em `M4b → M3 → M4` — a
camada nativa do submarino mora dentro do fluxo de perseguir o capitão que o M3
remove.

A regra que sai disso, e que vale para qualquer refactor futuro:

> Antes de arrancar um fluxo de governo, verifique o que está morando dentro
> dele. Fluxo magnético é lugar tentador para pendurar lógica que não tem nada a
> ver com o capitão — e ela morre junto quando o fluxo sai.

---

## 3. A escada, e por que a ordem é essa

O plano de trabalho (`docs/refactor/plano_de_trabalho.md`) foi escrito duas
vezes. A primeira versão ordenava por **custo** — deletar primeiro, ganho barato.
O autor corrigiu para **dependência**, e dependência ganha:

| degrau | o quê | estado |
|---|---|---|
| 0 — sensores | `PodeX`: a resposta legal | ✅ prontos |
| 1 — serviços de área | Hotzone, caminhos, topologia: devolvem **área** | ✅ prontos |
| 2 — consumidores `Melhor*` | cruzam, ranqueiam, desempatam | ⚠️ 8 existem, **4 faltam** |
| 3 — papéis | só política | encolhem junto do degrau 2 |
| 4 — variações | sem plano, agressivo, jipe, robô | vira **parâmetro** |

A razão de a ordem ser essa está no degrau 4. Hoje "IA sem plano" é um refactor
**porque o `AIController` é gordo**. Com o degrau 2 no lugar, o rebelde, o jipe
capturador e o robô da morte são o mesmo caso: chamadores diferentes do mesmo
serviço. O `Rebel.cs` não precisa ser desmontado — ele evapora.

O teste de aceite do primeiro item é o do autor, e é o melhor critério que este
projeto já teve para "ficou desacoplado":

> Um `UnitData` novo com a skill de captura passa a capturar **sem uma linha de
> IA escrita para ele**.

---

## 4. O desacoplamento já estava metade feito

A investigação que motivou o plano encontrou o contrário do esperado.

**Os serviços já são puros.** `MelhorDesembarqueService` é estático com `request`
+ callback e não sabe quem chamou; `MelhorEstoqueService` se declara consulta
pura; o envelope idem. Passam no teste que importa — *podem ser chamados por um
papel que não existia quando foram escritos*.

**O acoplamento está em dois outros lugares:**

| lugar | sintoma |
|---|---|
| arquivos de papel | carregam conta de alcance própria (`BuildFireSupportPaths` devolve malha de movimento em 11 sítios) |
| adaptadores | viraram organizadores disfarçados (`AIController.MelhorDesembarque.cs` tem `if (IsRuntimeRebelSnapshot)` e resolução de alvo por papel) |

E a camada de consumidor já tem **~5.700 linhas escritas e funcionando**:
`MelhorEmbarque` (1.207), `QueroCarona` (1.525), `MelhorEstoque` (867),
`CaptureOpportunityClaim` (746), `StockNeedAssessment` (572), `MelhorDesembarque`
(481), `MelhorPouso` (431), `QueroCaronaAerea` (308).

O buraco é **menor** do que 40 pendências sugerem. O que falta de verdade é
Combate e Fusão — os dois únicos ❌ confirmados.

---

## 5. A Hotzone virou instrumento

O que entrou de código nesta versão é quase tudo ferramenta, e serve à mesma
tese: doutrina que não dá para ver não dá para conferir.

### Hex de referência e projeção invertida

`UnitReachRequest` ganhou `OriginOverride`, propagado no clone por banda e
resolvido em todos os pontos que liam `CurrentCellPosition`. Nada é movido: a
banda é calculada **como se** a unidade estivesse ali.

É o que responde a pergunta invertida do desembarque. Não *"até onde o APC
chega"*, mas *"de onde o passageiro alcança o objetivo"* — teleporta-se o
passageiro para cima do prédio pretendido, e a banda dele vira a zona de largada.

A modalidade `Desembarque` da janela nasceu daí. Ela exige hex de referência (sem
âncora não há projeção) e mostra as duas bandas: **verde é largada boa** (o
passageiro fecha no mesmo turno) e **azul é largada degradada** (fecha no
seguinte), para o caso raro de o objetivo estar cercado.

> Nota de método: eu travei a modalidade em Tactical, e o contrato já dizia o
> contrário — *"quando o objetivo está cercado, o destino deixa de ser Tactical:
> vira Operational"*. A regra estava escrita; a ferramenta é que passou por cima.
> Corrigido no mesmo dia.

### `Tools > Hotzone`

Cinco janelas espalhadas por quatro menus passaram a viver juntas: a Hotzone e os
quatro `Melhor<x>` que existem. O corte que isso cria vale mais que a arrumação:
**sensor por domínio, consumidor por serviço** — que é a escada aparecendo no
menu.

---

## 6. Correções de performance

### Melhor LZ de Desembarque pendurava o editor

Não era laço infinito, era explosão combinatória — a mesma classe do regressão de
43 segundos da v6.0.x. O resolvedor de alvo da janela inundava o tabuleiro
inteiro, horizonte 120, **uma vez por construção inimiga** — e era chamado **uma
vez por spot, por LZ**. `LZs × spots × construções`.

O mapa reverso depende só de `(passageiro, alvo)` — nunca da LZ nem do spot de
onde a pergunta veio. Memorizado por esse par, o custo cai para uma inundação por
par: a mesma forma que o runtime já usava em
`GetOrBuildDisembarkPassengerRoute`.

Agravante que vale registrar para a próxima ferramenta de Editor: **fora do Play
Mode não há rede de baixo.** `MovementReachCache.TryBuildKey` exige
`Application.isPlaying`, então no editor todo cache de movimento é ignorado e
cada chamada floda de verdade.

Entraram junto dois seguros: teto de mapas de rota (exposto na janela) que devolve
ranking parcial **avisado** em vez de pendurar, e barra de progresso liberada em
`finally` — para que cálculo lento pareça lento, e não travado.

---

## Verificação

O que foi conferido no código, com arquivo e linha, e virou marca nos contratos:

- **quem encerra a vez de quem** — capturar, embarcar, desembarcar, suprir e
  fundir confirmados; **transferir não marca ninguém**;
- as habilidades de pouso são **dados**, não código (`SkillData.id` +
  `ConstructionData.requiredLandingSkillRules`);
- `FogOfWarVisionMode { All, Air, Surface, Sub }` — as quatro camadas exatas;
- `PodeArremeter` **compõe** dois sensores: chama `PodeDecolarSensor.Evaluate`;
- `AIController.Transportador.Courier.Attack.cs` é **código morto** — sem
  chamador, embora o `CLAUDE.md` ainda o descreva;
- `MelhorEstoqueService` (867 linhas) **existe**, apesar de o contrato marcá-lo
  como não implementado;
- `PodeEnxergar` **não tem arquivo de sensor** e empresta a matemática do
  `PodeDetectarSensor`.

---

## Pendências

As tabelas por documento: `governanca.md` (G2–G23), `Transporte.md` (T1–T17),
mais as dos três contratos de papel. Consolidadas e ordenadas por dependência em
`docs/refactor/plano_de_trabalho.md`.

O aviso que fica escrito no plano, porque é o mais fácil de esquecer:

> Lista grande, organizada e marcada **parece progresso**. O antídoto é o ritmo
> que já existe — uma classe por vez, compila, roda no jogo, comita antes da
> próxima.

---

## Nota do autor

> *"cara, eu amei ter gastado 1 ano criando o sistema pra funcionar sem AI"*

É por isso que uma mudança de arquitetura desse tamanho tem impacto mínimo no
jogo: a IA é consumidora dos sensores, não dona deles. O tabuleiro, os `PodeX`, o
FoW e o ciclo transacional continuam intactos. O que mudou foi **a forma como a
IA os usa** — e a forma como qualquer IA futura vai usá-los.
