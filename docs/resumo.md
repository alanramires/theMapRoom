# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-01, logo depois de fechar a `v7.0.0`.
Leia isto primeiro; ele diz o que ler depois.

---

## Estado

**`v7.0.0` tagueada e no ar.** Major v6 fechado, relatórios arquivados em
`docs/Versões/`.

A versão foi quase toda **doutrina e ferramenta**. O jogo praticamente não mudou —
por construção: a IA é consumidora dos sensores, não dona deles.

A descoberta que organiza tudo o que vem:

> **O desacoplamento já estava metade feito.** Os serviços (`Melhor*`, envelope)
> são puros e não sabem quem os chamou. O acoplamento está (a) nos arquivos de
> papel, que carregam conta de alcance própria, e (b) nos adaptadores, que
> viraram organizadores disfarçados.

---

## A arquitetura, em cinco linhas

```text
0. sensores PodeX              → a resposta legal            ✅ prontos
1. serviços de área (Hotzone)  → devolvem ÁREA               ✅ prontos
2. consumidores Melhor*        → cruzam, ranqueiam, decidem  ⚠️ 8 existem, 4 faltam
3. papéis                      → só POLÍTICA                 encolhem junto do 2
4. variações de papel          → sem plano, agressivo, jipe  vira PARÂMETRO
```

**Um degrau nunca começa antes de o de baixo estar de pé.** Ordem por
dependência, não por custo — mexer no papel antes de extrair o serviço é mexer no
mesmo código duas vezes.

A razão está no degrau 4: hoje "IA sem plano" é um refactor **porque o
`AIController` é gordo**. Com o degrau 2 no lugar, o rebelde, o jipe capturador e
o robô da morte são o mesmo caso — chamadores diferentes do mesmo serviço. O
`Rebel.cs` não é desmontado, ele **evapora**.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/refactor/plano_de_trabalho.md` | **a fila.** A escada, o que falta em cada degrau, o que está adiado e por quê |
| 2 | `docs/AI Behavior/governanca.md` | a norma acima de todos os papéis: ordens, ciclo, ações `PodeX`, sensores de sistema, visão, Hotzone, papéis |
| 3 | `docs/AI Behavior/governanca_entre_papeis.md` | as arestas: os três tipos de governo e o Comportamento Magnético |
| 4 | `docs/AI Behavior/contrato_envelope_alcance.md` | **norma** das bandas. Inclui a inversão do artilheiro |
| 5 | o contrato do papel em que for mexer | `Capturador.md`, `Assalto.md`, `FireSupport.md`, `Transporte.md` |
| 6 | `docs/relatorio_v7.0.0.md` | o porquê da versão, se precisar do contexto |

Todos usam o mesmo esquema: ✅ conferido / ⚠️ diverge / ❌ não existe / ❓ não
conferido.

---

## Próximo passo concreto

### 2.1 — extrair o **Melhor Captura**

`PodeCapturar` já está pronto. A lógica de "qual construção capturar" está enfiada
em 19 arquivos e 5.816 linhas de `Assets/Scripts/Match/AI/Units/Capturer/`:

| peça | onde |
|---|---|
| matching 1:1 — **já é serviço, é o ponto de partida** | `CaptureOpportunityClaimService.cs` (746) |
| busca de alvo sem plano | `AIController.Capturer.Rogue.cs` (279) + `FindNearestPlanlessCaptureTarget` no `Rebel.cs` |
| alvo por setor/slot do plano | `AIController.Capturer.cs` (618) |
| predicados de elegibilidade | `AIController.Capturer.Helpers.cs` (304), `IsRebelCapturable` |
| alvo designado | `TryResolveUnitDesignatedCaptureTarget`, `CommitPendingRebelCaptureTarget` |

**O serviço responde:** dada uma unidade, uma origem e candidatos — quais
construções são alcançáveis nas bandas Tática e Operacional, e em que ordem de
prioridade. Inclui construções **aliadas abaixo dos pontos de captura máximos**
(estão sob captura, precisam ser defendidas).

**O serviço NÃO pode saber:** se a unidade tem plano, se a facção tem QG, qual o
papel dela. Isso é do chamador.

**Critério de aceite (do autor):**

> Um `UnitData` novo com a skill de captura — o "jipe capturador" — passa a
> capturar **sem uma linha de IA escrita para ele**.

### Antes disso, se quiser um aquecimento barato

| # | tarefa |
|---|---|
| L1 | apagar `AIController.Transportador.Courier.Attack.cs` — **não tem chamador**, é código morto. E corrigir a linha do `CLAUDE.md` que ainda descreve o ataque oportunista do courier |
| L2 | descobrir se `MelhorEstoqueService` (867 linhas, consulta pura) é **consumido** pela IA ou só existe. Pode encolher dois itens do degrau 2 de "escrever" para "conectar" |
| L3 | T3 do `Transporte.md` — o `RepresentativeCell` que produz desembarque de distância zero. Provavelmente já acontece em jogo sem ninguém notar |

---

## Regras de trabalho (não são sugestão)

- **Uma classe por vez.** Você mexe, o autor compila e roda no jogo, e comita
  antes da próxima. **Não emenda fases.**
- **Verificar antes de documentar.** Nota de design vira manual só depois de
  conferir no código. E **busca vazia não prova ausência** — procurar o conceito
  por sinônimos antes de afirmar que não existe.
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Medir antes de otimizar.** Ler código não acha gargalo.
- **Não editar `.asset` no disco com o inspector aberto** — o reimport descarta a
  memória da Unity.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| inundação de tabuleiro dentro de laço por candidato | duas vezes já: 43 s na v6.0.x e a janela de LZ pendurando o editor. O mapa reverso depende só de `(unidade, alvo)` — memorize por esse par |
| cache de movimento no Editor | `MovementReachCache.TryBuildKey` exige `Application.isPlaying`. **Fora do Play Mode não há rede de baixo** |
| ferramenta contra o contrato | travei a modalidade Desembarque em Tactical, e o contrato já dizia o contrário. Ler o contrato antes de "melhorar" a ferramenta |
| `git add .` | varre trabalho do editor Unity junto. Não é erro, mas confira o que entrou |
| `BuildFireSupportPaths` — 11 sítios | **dois deles (`Phase2.cs:852`, `Initiative.cs:505`) não são fire support.** Isolar antes de tocar nos outros nove |
| `roles.Contains` estrito | barra especializações. Portão de papel usa `UnitRoleCompatibility.CanSatisfy` |

---

## Trilha paralela — Naval

Ordem **obrigatória**: `M4b → M3 → M4`. A camada nativa do submarino mora dentro
do fluxo de perseguir o capitão, que o M3 remove.

**Não rodar junto do degrau 4** — as duas mexem em âncora, e juntas qualquer
diferença no teste fica sem causa identificável.

Falta escrever o **magnético naval** no `governanca_entre_papeis.md` §2.3: hoje
ele não está lá, e o M3 depende dele.

---

## Aviso

Os contratos produziram **40+ pendências**. Lista grande, organizada e marcada
**parece progresso**. O antídoto é o ritmo acima.

E o número que devolve a proporção: a camada de consumidor já tem **~5.700 linhas
escritas e funcionando**. Faltam de verdade **dois** serviços — Combate e Fusão.
