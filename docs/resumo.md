# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-02, logo depois de fechar a `v7.0.1`.
Leia isto primeiro; ele diz o que ler depois.

---

## Estado

**`v7.0.1` tagueada e no ar.** O degrau 2 saiu do papel: existe um
`MelhorCapturaService`, e dois consumidores reais o chamam.

A descoberta que organiza tudo o que vem — e que a v7.0.1 confirmou em jogo:

> **Consertar a fonte conserta o vizinho de graça.** O navio de transporte
> voltou a esperar na praia sem uma linha tocada no transporte, porque o
> `MelhorEmbarque` recebe a necessidade de carona por delegate e é inteiramente
> downstream dela. Foi o único resultado da versão que ninguém programou.

---

## A arquitetura, em cinco linhas

```text
0. sensores PodeX              → a resposta legal            ✅ prontos
1. serviços de área (Hotzone)  → devolvem ÁREA               ✅ prontos
2. consumidores Melhor*        → cruzam, ranqueiam, decidem  ⚠️ 9 existem, 3 faltam
3. papéis                      → só POLÍTICA                 encolhem junto do 2
4. variações de papel          → sem plano, agressivo, jipe  vira PARÂMETRO
```

**Um degrau nunca começa antes de o de baixo estar de pé.** Ordem por
dependência, não por custo.

Faltam de verdade **Combate** e **Fusão**. O terceiro buraco é o `Rebel.cs` —
ver abaixo, porque ele mudou de lugar na fila.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/relatorio_v7.0.1.md` | **o que acabou de acontecer**, incluindo o que não terminou |
| 2 | `docs/refactor/plano_de_trabalho.md` | a fila. A escada, o que falta em cada degrau, o que está adiado |
| 3 | `docs/AI Behavior/governanca.md` | a norma acima dos papéis |
| 4 | `docs/AI Behavior/contrato_envelope_alcance.md` | **norma** das bandas. Inclui a inversão do artilheiro |
| 5 | o contrato do papel em que for mexer | `Capturador.md`, `Assalto.md`, `FireSupport.md`, `Transporte.md` |

---

## Onde eu parei — o 2.1 pela metade

`MelhorCapturaService` existe, é consumido, e a ferramenta
`Tools > Hotzone > Melhor Captura` mostra o que ele responde.

**Feito:**

- o `CaptureOpportunityClaimService` chama o serviço uma vez por capturador e
  ficou só com o matching 1:1. `IsEligibleConstruction` foi deletado
- o `QueroCaronaService` parou de varrer o tabuleiro (sobrou o hash de cache)
- **a ordem foi invertida:** o matching aloca → carona e âncora *leem* a
  alocação por `TryGetClaimForUnit`. Antes eram duas resoluções independentes,
  livres para discordar

**Falta:**

- 7 varreduras de tabuleiro no `Capturer/` — Blitzkrieg (2), Explorer (2),
  Embark (rally), Helpers, e as 2 do `Rebel.cs`
- `QueroCaronaContext { ComPlano, RogueOuRebelde }`. Matar exige o chamador
  passar o filtro, o que muda a assinatura do request
- o `Rebel.cs`

**Sobre a métrica `IsCapturable`:** ela ainda aparece em 27 arquivos da IA. Nos
dois que passaram pelo refactor ele sobrou **só no hash de cache** — a decisão
saiu. Nos outros 25 ele ainda decide. Não use a contagem crua de arquivos como
progresso; ela não distingue decisão de hash.

### O achado que reordena a fila

**O `Rebel.cs` vazou para fora do capturador.** `FindNearestPlanlessCaptureTarget`
e `IsRebelCapturable` são chamados por:

| quem | onde |
|---|---|
| `AIController.MelhorDesembarque.cs` | 5 sítios |
| `Transportador.Courier.Disembark.cs` | 2 |
| `Transportador.Courier.Passengers.cs` | 1 |
| `Transportador.Naval.cs` | 1 |
| `Assault.HQBreaker.cs` | 1 |
| `Phase2.cs` | `CommitPendingRebelCaptureTarget` |
| `Router.cs:107` | `TryDecideRebelAction`, antes do planner |

Transporte, Assalto e Desembarque decidem alvo de captura chamando funções do
rebelde. **Ele não é "o passo depois do capturador" — é a ponte para os degraus
4 e 5.** Matar aquelas duas funções converte três papéis de uma vez.

**Cuidado com o nome:** há duas coisas chamadas "rebelde". A *facção sem QG* é
conceito de jogo (derivado de não possuir `isPlayerHeadQuarter`) e **fica**. O
`AIController.Rebel.cs` é controlador paralelo de IA e **evapora**.

### Critério de aceite, inalterado

> Um `UnitData` novo com a skill de captura — o "jipe capturador" — passa a
> capturar **sem uma linha de IA escrita para ele**. Há um `jeep.png` e um
> `soldado_jetpack.png` no repo esperando esse teste.

---

## Regras de trabalho (não são sugestão)

- **Uma classe por vez.** Você mexe, o autor compila e roda no jogo, e comita
  antes da próxima. **Não emenda fases.**
- **Verificar antes de documentar.** E **busca vazia não prova ausência** —
  procurar o conceito por sinônimos antes de afirmar que não existe.
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Medir antes de otimizar.** Ler código não acha gargalo.
- **Não editar `.asset` no disco com o inspector aberto** — o reimport descarta
  a memória da Unity.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **otimizar por hipótese** | cortei 80% das chamadas ao sensor por candidata e o tempo **não se mexeu**. O custo estava nos 16 envelopes que o claim service constrói, um por capturador. Ler o log inteiro antes de escolher o alvo |
| **comparar rodadas incomparáveis** | pós-load a IA reembaralha a ordem das unidades e o cache está frio (`MovementCacheMisses` de 1 para 52). Só compare turnos com o mesmo save, mesma ordem |
| **`FrameSpike` com F11** | mede o frame inteiro, incluindo o input do humano. Não serve de métrica de IA. Use `decision=` da linha `[AI Perf][Unit]` |
| **uma função, duas perguntas opostas** | `CollectCaptureCandidates` serve escolha de alvo (quer o alcançável) e fome estrutural (pergunta sobre o que está longe). Fixar o corte declarava encalhada quem tinha alvo a pé. Parâmetro obrigatório, sem default |
| **`FindObjectsByType` dentro de laço** | `GetConstructionAtCell` varre a cena por chamada. Barato uma vez, O(n²) por candidata. Se o chamador já tem o objeto, passe-o |
| inundação de tabuleiro por candidato | duas vezes já: 43 s na v6.0.x e a janela de LZ pendurando o editor |
| cache de movimento no Editor | `MovementReachCache.TryBuildKey` exige `Application.isPlaying` |
| ferramenta contra o contrato | ler o contrato antes de "melhorar" a ferramenta |
| **`git add .`** | varre trabalho do editor Unity junto. Numa sessão a cena veio com **12.530 linhas** alteradas. Não é erro, mas confira o que entrou |
| `roles.Contains` estrito | barra especializações. Portão de papel usa `UnitRoleCompatibility.CanSatisfy` |
| predicado no eixo errado | `construction.TeamId == unit.TeamId` é **time**, não slot — e apagava a reconquista inteira. Relação entre lados é `PlayerSlotRelations.AreAllies(slot, slot)` |

---

## Aquecimento barato, se quiser

| # | tarefa | estado |
|---|---|---|
| L1 | apagar `AIController.Transportador.Courier.Attack.cs` — sem chamador | **ainda de pé** |
| L2 | descobrir se `MelhorEstoqueService` é consumido | ✅ **é** — `AIController.Stock.cs:189` e `Logistics.Restock.cs:44` |
| L3 | T3 do `Transporte.md` — `RepresentativeCell` com desembarque de distância zero | não conferido |

---

## Trilha paralela — Naval

Ordem **obrigatória**: `M4b → M3 → M4`. A camada nativa do submarino mora dentro
do fluxo de perseguir o capitão, que o M3 remove.

**Não rodar junto do degrau 4** — as duas mexem em âncora.

Falta escrever o **magnético naval** no `governanca_entre_papeis.md` §2.3.

---

## Aviso

Os contratos produziram 40+ pendências. Lista grande, organizada e marcada
**parece progresso**. O antídoto é o ritmo acima.

E o teste final continua sendo um só: **os 7 perfis chamando uma fonte única,
não 7 perfis com 7 definições diferentes.**
