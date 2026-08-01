# Plano de trabalho — os degraus, de baixo para cima

Escrito em 2026-08-01, depois de fechar os contratos de `governanca`,
`governanca_entre_papeis`, `Capturador`, `Assalto`, `FireSupport` e `Transporte`.
Eles produziram **40+ pendências**. Este documento é a ordem.

> **A ordem é por dependência, não por custo.** Um degrau nunca começa antes de o
> de baixo estar de pé. Não dá para esvaziar o consumidor de um serviço que ainda
> não existe — e mexer no papel antes de extrair o serviço é mexer no mesmo
> código duas vezes.

---

## A escada

| degrau | o quê | estado |
|---|---|---|
| **0 — sensores** | `PodeX`: a resposta legal | ✅ prontos (exceto `PodeEnxergar`, sem arquivo próprio) |
| **1 — serviços de área** | Hotzone/envelope, `UnitMovementPathRules`, `BoardTopologyIndex`: devolvem **área**, não escolha | ✅ prontos |
| **2 — consumidores `Melhor*`** | cruzam, ranqueiam, desempatam. **Terminais burros de decisão** | ⚠️ **8 existem, 4 faltam. É aqui que o trabalho está** |
| **3 — papéis** | política: prioridade, recusa, quando desistir | encolhem **sozinhos** conforme o degrau 2 sobe |
| **4 — variações de papel** | sem plano / rogue, agressivo, jipe capturador, robô da morte | vira **parâmetro**, não refactor |

O degrau 4 é a razão de a ordem ser esta. Hoje "IA sem plano" é um refactor
porque o `AIController` é gordo. Com o degrau 2 no lugar, o rebelde, o jipe e o
robô da morte são **o mesmo caso**: chamadores diferentes do mesmo serviço, com
parâmetros diferentes. O `Rebel.cs` não precisa ser desmontado — ele evapora.

---

# Degrau 2 — extrair os terminais

Quatro consumidores faltam. Cada um está **enfiado dentro de um papel**, e
extraí-lo é o que faz o papel encolher.

## 2.1 — Melhor Captura

*O primeiro, porque `PodeCapturar` já está pronto e é o serviço principal do papel
com mais arquivos.*

**Onde está hoje**, espalhado por 19 arquivos e 5.816 linhas em
`Units/Capturer/`:

| peça | arquivo |
|---|---|
| matching 1:1 de capturáveis | ✅ `CaptureOpportunityClaimService` (746) — **já é serviço**, ponto de partida |
| busca de alvo sem plano | `AIController.Capturer.Rogue.cs` (279) + `FindNearestPlanlessCaptureTarget` no `Rebel.cs` |
| alvo por setor/slot do plano | `AIController.Capturer.cs` (618) |
| predicados de elegibilidade | `AIController.Capturer.Helpers.cs` (304), `IsRebelCapturable` |
| alvo designado | `TryResolveUnitDesignatedCaptureTarget`, `CommitPendingRebelCaptureTarget` |

**O serviço deve responder:** dada uma unidade, uma origem e um conjunto de
candidatos, quais construções são alcançáveis nas bandas Tática e Operacional, e
em que ordem de prioridade — incluindo **construções aliadas abaixo dos pontos de
captura máximos**, que estão sob captura e precisam ser defendidas.

**O serviço NÃO deve saber:** se a unidade tem plano, se a facção tem QG, qual o
papel dela. Isso é do chamador.

**Teste de que ficou certo:** um `UnitData` novo com a skill de captura — o
"jipe capturador" — passa a capturar sem uma linha de IA escrita para ele.

## 2.2 — Melhor Atendimento

*Segundo, porque metade já existe e o custo é conectar.*

`StockNeedAssessmentService` (572) já responde *"quem precisa mais"*. Falta a
metade da área: quais unidades o supridor **alcança e consegue atender**, dado o
modo de serviço da ficha (`SameHexOrEmbarked`, `Adjacent1Hex`, ou os dois) e a
**camada de acordo** — a interseção das camadas em que cada lado consegue prestar
ou receber (ver `governanca.md`, *A camada do encontro é um acordo*).

Prioridade que o serviço entrega ordenada: crítico → manutenção preventiva →
Capitão/formação.

**Antes de começar:** descobrir se `MelhorEstoqueService` (867 linhas, consulta
pura, já classifica encontros) é **consumido** pela IA ou só existe. Se estiver
pronto e sem consumidor, este item é conectar, não escrever — e o mesmo pode
valer para 2.3.

## 2.3 — Melhor Combate

*Terceiro, porque depende das bandas certas.*

**Onde está hoje:** `Units/Assault/` (3.500 linhas), `Units/Fire Support/`, e o
`HexEvaluator` de fallback.

**Depende de:** a inversão do artilheiro já consumida pela IA. Enquanto
`BuildFireSupportPaths` devolver malha de **movimento** em 11 sítios, o serviço
de combate seria escrito sobre a banda errada — e reescrito depois.

Portanto: **2.3 começa com a migração do `BuildFireSupportPaths`**, não com o
serviço. Cuidado registrado: dois dos 11 sítios (`Phase2.cs:852` e
`Initiative.cs:505`) **não são código de fire support** — isolar antes de tocar
nos outros nove.

## 2.4 — Melhor Fusão

*Quarto, o único que não existe em lugar nenhum.*

Regra: voltar à **Retaguarda** antes de procurar unidade idêntica. Preparação usa
a Hotzone de Movimento; a fusão em si só existe na banda **Tática** — coerente
com Fusão não ter Operational no envelope.

Exceções por papel (Elite ignora fusão de baixo valor, Fire Support recua para se
recompor, unidade em risco imediato funde fora da Retaguarda) são **política do
chamador**, não do serviço.

---

# Degrau 3 — os papéis emagrecem

Não é uma fase separada: acontece **junto** de cada item do degrau 2. Extraiu o
serviço, o papel que o continha perde as linhas correspondentes no mesmo commit.

O que sobra em cada arquivo de papel, e só isso:

- prioridade entre intenções;
- recusa (quando **não** vale a pena);
- desempate entre opções que o serviço devolveu empatadas;
- quando desistir.

Nesse degrau também entram as constantes que sobreviverem:

| constante | vira |
|---|---|
| `TransportDropOffRange = 4` | `Tactical` do **passageiro**, projetado do objetivo |
| `FireSupportDropOffRange = 3` | some — a banda por unidade já resolve |
| `ShuttlePickupRange = 2` | `Tactical` + folga |
| `MinDistanceForTransportSlot` | `Operational` da unidade avaliada |

---

# Degrau 4 — variações de papel

Só aqui entra a **IA sem plano**, e o plano completo continua sendo o
`docs/refactor/ai_sem_plano.md`. A diferença é que, chegando neste ponto, o
trabalho descrito lá vira quase inteiramente **deleção**:

| item do `ai_sem_plano.md` | o que sobra depois do degrau 2 |
|---|---|
| âncora do rogue deixa de ser o QG | um parâmetro na chamada do Melhor Captura |
| `FindNearestRebelCaptureTarget` generalizar | **já é** o Melhor Captura |
| mover os três "gerais com nome errado" | já saíram junto com a extração |
| apagar as duplicatas | apagar |
| `Rebel.cs` vira roteador | apagar |
| a segunda cópia do funil, no transporte | apagar |

**O teste continua o mesmo, e continua obrigatório nas duas pontas:** IA com QG e
planos × IA sem QG e sem planos, no mesmo mapa, **antes** e **depois**. O log do
rebelde tem que passar a mostrar, sem nada escrito para ele:

```text
[Capturador] <id> ... dpq=... preferDpq=True        ← ficha honrada
[Capturador] <id> QueroCarona=... envelope=...      ← alcance pelo envelope
[FilaCarona] <id> entra na fila ...                 ← mesma fila de todos
```

Aqui também entram as outras variações: Capturador Agressivo, e as novas que
vierem.

---

# Fora da escada

## Limpeza barata — pode ir a qualquer momento

| # | tarefa |
|---|---|
| L1 | apagar `AIController.Transportador.Courier.Attack.cs` (sem chamador) e corrigir a linha do `CLAUDE.md` que ainda descreve o ataque oportunista do courier |
| L2 | decidir **SVTOL** × **STOVL** e alinhar contrato e dados |
| L3 | fechar o G4: a família de detecção tem três sensores ou dois? |
| L4 | T3 do `Transporte.md` — o `RepresentativeCell` que produz entrega de distância zero. Provavelmente já acontece em jogo sem ninguém notar |

## Trilha naval — paralela, ordem obrigatória

```text
M4b  →  M3  →  M4
```

A camada nativa do submarino mora **dentro** do fluxo de perseguir o capitão, que
o M3 remove. **Não rodar junto do degrau 4** — as duas mexem em âncora, e juntas
qualquer diferença no teste fica sem causa identificável.

Falta escrever o **magnético naval** no `governanca_entre_papeis.md` §2.3: hoje
ele não está lá, e o M3 depende dele.

## Adiado, com motivo

| item | motivo |
|---|---|
| renomear `FogoIndireto` → `FireSupport`, `VigilanciaAerea` → `Vigilancia` | seguro (valores numéricos não mudam), mas toca muitos arquivos e não muda comportamento. Janela vazia, nunca junto de refactor |
| `TransportadorAereo = 15` sair do enum | só depois que a política de compra do Chinook virar condição dentro do Transportador |
| `PodeEnxergar` ganhar arquivo próprio | dívida real, mas mexe em FoW — a área mais cara de regredir. Merece versão própria |
| esteira de transporte (reserva de vaga, encher vaga livre, pressão de compra) | doutrina nova sobre sistema vivo. Depois do degrau 3, e **reserva de vaga entra junto com encher vaga livre** — sozinho, o segundo piora o jogo |

---

# Calibragem

**~2 meses** é honesto para os degraus 2 a 4 no ritmo de uma classe por vez, com
teste em jogo entre elas.

O que já está pronto e não aparece na lista de pendências: **~5.700 linhas de
camada de consumidor** — `MelhorEmbarque` (1.207), `QueroCarona` (1.525),
`MelhorEstoque` (867), `CaptureOpportunityClaim` (746), `StockNeedAssessment`
(572), `MelhorDesembarque` (481), `MelhorPouso` (431), `QueroCaronaAerea` (308).
O buraco é menor do que 40 pendências sugerem.

Marcos que valem uma versão:

| marco | o que prova |
|---|---|
| fim do 2.1 | um `UnitData` novo com skill de captura captura **sem uma linha de IA** |
| fim do degrau 2 | todo `PodeX` do contrato tem um consumidor |
| fim do degrau 3 | não existe mais número fixo de hex em decisão de IA |
| fim do degrau 4 | existe **um** comportamento sem plano, não dois |
