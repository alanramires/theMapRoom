# Plano de trabalho — da doutrina ao código

Escrito em 2026-08-01, depois de fechar cinco contratos (`governanca.md`,
`governanca_entre_papeis.md`, `Capturador.md`, `Assalto.md`, `FireSupport.md`,
`Transporte.md`). Eles produziram **40+ pendências**. Este documento é a fila.

> **Aviso que vale mais que o plano:** lista grande, organizada e marcada
> **parece progresso**. O ritmo que impede isso é o que já existe — uma classe
> por vez, você compila, roda no jogo, e comita antes da próxima. Não emenda
> fases.

## O diagnóstico em uma frase

Os **serviços já estão desacoplados**. `MelhorDesembarqueService` é estático puro
com `request` + callback; `MelhorEstoqueService` se declara consulta pura; o
envelope idem. Passam no teste que importa:

> *Este serviço pode ser chamado por um papel que não existia quando ele foi
> escrito?*

O acoplamento está em **dois outros lugares**:

| lugar | sintoma | exemplo |
|---|---|---|
| **arquivos de papel** | contêm conta de alcance própria | `BuildFireSupportPaths` devolve malha de movimento em 11 sítios |
| **adaptadores** | viraram organizadores disfarçados | `AIController.MelhorDesembarque.cs` tem `if (IsRuntimeRebelSnapshot)` e resolução de alvo por papel |

Daí as **três frentes**, que não devem se misturar:

1. tirar conta de alcance dos papéis → migração para a Hotzone;
2. esvaziar os adaptadores → política volta para o papel;
3. escrever os dois consumidores que faltam → Combate e Fusão.

---

## Ordem

### Fase 0 — limpeza barata

*Dias. Reduz ruído antes de qualquer coisa cara.*

| # | tarefa | prova |
|---|---|---|
| 0.1 | apagar `AIController.Transportador.Courier.Attack.cs` (sem chamador) e corrigir a linha do `CLAUDE.md` que ainda descreve o ataque oportunista do courier | compila; nenhuma referência restante |
| 0.2 | descobrir se `MelhorEstoqueService` (867 linhas) é **consumido** pela IA ou só existe | um log do serviço aparece numa partida com papel Estoque |
| 0.3 | decidir **SVTOL** × **STOVL** e alinhar contrato e dados | uma busca só devolve um dos dois |
| 0.4 | fechar o G4: existe um terceiro sensor de detecção, ou são dois? | o contrato passa a dizer o número certo |

**0.2 é o mais importante da fase** e pode encolher a Fase 4 sozinho. Se o
serviço estiver pronto e sem consumidor, o trabalho de Estoque é conectar, não
escrever.

### Fase 1 — unidade sem plano (rogue absorve rebelde)

*2 a 3 semanas. É a única fase que **remove** código.*

Plano completo em `docs/refactor/ai_sem_plano.md`. Resumo da ordem:

| # | tarefa |
|---|---|
| 1.1 | **rodar o teste de linha de base**: IA com QG e planos × IA rebelde sem QG, mesmo mapa, rebelde **como está**. Guardar o log |
| 1.2 | âncora do rogue deixa de ser o QG — sem ramo (decisão do autor) |
| 1.3 | renomear/generalizar `FindNearestRebelCaptureTarget` → objetivo de unidade sem plano |
| 1.4 | mover os três "gerais com nome errado" (`IsRebelCapturable`, `TryResolveUnitDesignatedCaptureTarget`, `CommitPendingRebelCaptureTarget`) |
| 1.5 | apagar as duplicatas (busca própria, `FindRebelApproachCell`, portão `MP × 2`) |
| 1.6 | matar a **segunda cópia do funil**, dentro do transporte (T-A, T-B) |
| 1.7 | `Rebel.cs` vira roteador de 3 linhas |
| 1.8 | **rodar o mesmo teste** — a verificação |

**Prova:** o log do rebelde passa a mostrar, sem ninguém ter escrito nada
específico para ele:

```text
[Capturador] <id> ... dpq=... preferDpq=True        ← ficha honrada
[Capturador] <id> QueroCarona=... envelope=...      ← alcance pelo envelope
[FilaCarona] <id> entra na fila ...                 ← mesma fila de todos
```

**Fecha de graça:** T1 e T2 do `Transporte.md`, C8/S9 dos outros contratos, e o
`IsRuntimeRebelSnapshot` some — com ele, a atribuição passa a ser lida para todo
mundo sem uma linha nova.

> **Desvio consciente do `ai_sem_plano.md`.** Aquele documento sugere fechar a
> esteira de transporte **antes**. A restrição real que ele descreve é *não
> simultâneo* — fazer os dois juntos tira a base de comparação. Como os itens da
> esteira são doutrina nova (cara) e este refactor é deleção (barato, e destrava
> pendência de três contratos), inverti a ordem. Se você preferir a original, o
> único ajuste é trocar Fase 1 por Fase 3.

### Fase 2 — migração para a Hotzone

*3 a 4 semanas. Mecânica e verificável.*

| # | alvo | hoje | vira |
|---|---|---|---|
| 2.1 | `BuildFireSupportPaths` — **11 sítios** | `CalcularCaminhosValidos(RemainingMovementPoints)` | banda do artilheiro (0→alcance, 2× alcance) |
| 2.2 | `TransportDropOffRange = 4` | constante do veículo | `Tactical` do **passageiro**, projetado do objetivo |
| 2.3 | `FireSupportDropOffRange = 3` | idem | idem — some, porque a banda por unidade já resolve |
| 2.4 | `ShuttlePickupRange = 2` | constante | `Tactical` + folga (já é quase isso nos call sites) |
| 2.5 | `MinDistanceForTransportSlot` | tunável 7 | `Operational` da unidade avaliada |

**Cuidado no 2.1:** dois dos 11 sítios (`Phase2.cs:852` e `Initiative.cs:505`)
**não são código de fire support** — trocar a malha lá muda o significado. Isolar
esses dois antes de tocar nos outros nove.

**Prova por item:** um obus de 2 MP e um fuzileiro de 3 MP deixam de aceitar a
mesma largada. Dá para ver na ferramenta Hotzone antes de rodar partida.

### Fase 3 — esteira de transporte e adaptadores

*3 a 4 semanas.*

| # | tarefa | pendência |
|---|---|---|
| 3.1 | promessa reserva **uma vaga**, não o veículo | `Transporte.md` §2 |
| 3.2 | encher a vaga livre no caminho — **só junto com 3.1** | §2 + cascata registrada |
| 3.3 | espera vira **pressão de compra** de transporte | T14 |
| 3.4 | com carga, herdar o destino da carga no `AIDesignatedMission` | T11 |
| 3.5 | esvaziar `AIController.MelhorDesembarque.cs`: política volta ao papel, adaptador vira tradução | — |
| 3.6 | T3: o `RepresentativeCell` que produz entrega de distância zero | T3 |

⚠️ **3.2 sozinho piora o jogo.** Encher a vaga livre sem reserva de vaga deixa o
veículo ocupado mais tempo e agrava a fome do passageiro esquecido. Os dois
entram juntos ou nenhum entra.

**T3 (3.6) merece atenção antecipada:** ele provavelmente já acontece hoje sem
ninguém notar — o caminhão "entrega" sem sair do lugar. Se aparecer no log da
Fase 1, sobe de prioridade.

### Fase 4 — os consumidores que faltam

*3 a 4 semanas. Por último, e de propósito.*

| # | serviço | por que agora |
|---|---|---|
| 4.1 | **Melhor Combate** | precisa de doutrina nova **e** depende das bandas certas. Escrito antes da Fase 2, é escrito duas vezes |
| 4.2 | **Melhor Fusão** | depende da regra de Retaguarda, que depende do envelope |

Os dois são os únicos ❌ **confirmados** do catálogo de consumidores. Todo o resto
existe: `MelhorEmbarque` (1.207), `QueroCarona` (1.525), `MelhorEstoque` (867),
`CaptureOpportunityClaim` (746), `StockNeedAssessment` (572),
`MelhorDesembarque` (481), `MelhorPouso` (431), `QueroCaronaAerea` (308).

**~5.700 linhas de camada de consumidor já escritas.** O buraco é menor do que a
tabela de pendências sugere.

---

## Trilha paralela — Naval

Corre **fora** das fases acima, e tem ordem **obrigatória**:

```text
M4b  →  M3  →  M4
```

A lógica de camada nativa do submarino mora **dentro** do fluxo de perseguir o
capitão. O M3 remove esse fluxo. Fazer M4 antes apaga a camada nativa junto;
fazer M3 antes do M4b apaga sem ter onde recolocá-la.

**Não rodar junto com a Fase 1.** As duas mexem em âncora e movimento — juntas,
qualquer diferença no teste fica sem causa identificável.

Falta também escrever o **magnético naval** no contrato: o
`governanca_entre_papeis.md` §2.3 não o lista, e o `Assalto.md` M3 depende dele.

---

## O que fica de fora, e por quê

| item | motivo |
|---|---|
| renomear `FogoIndireto` → `FireSupport` e `VigilanciaAerea` → `Vigilancia` | seguro (os valores numéricos não mudam), mas toca muitos arquivos e não muda comportamento. Fazer numa janela vazia, nunca junto de refactor |
| `TransportadorAereo = 15` sair do enum | só depois que a política de compra do Chinook virar condição dentro do Transportador |
| `PodeEnxergar` ganhar arquivo próprio | é dívida real, mas mexe em FoW — a área mais cara de regredir. Merece versão própria |
| `Melhor Captura` como serviço | `CaptureOpportunityClaimService` já faz metade. Reavaliar depois da Fase 1 |
| as três categorias de combate ganharem nome no código | melhora a leitura, não muda comportamento. Cabe dentro da Fase 4.1 |

---

## Calibragem

Quatro fases mais a trilha naval, no ritmo de uma classe por vez com teste em
jogo entre elas: **~2 meses** é uma estimativa honesta, e a Fase 3 é a que mais
tende a estourar, porque é a única com doutrina nova em cima de sistema vivo.

Os marcos que valem parar e comemorar, porque cada um é uma versão:

| marco | o que ele prova |
|---|---|
| fim da Fase 1 | existe **um** comportamento sem plano, não dois |
| fim da Fase 2 | não existe mais número fixo de hex em decisão de IA |
| fim da Fase 3 | o transporte cumpre promessa e a espera vira dinheiro |
| fim da Fase 4 | todo `PodeX` do contrato tem um consumidor |
