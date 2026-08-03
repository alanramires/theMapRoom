# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-03, depois da `v7.0.2` e do trabalho que
veio em cima dela. Leia isto primeiro; ele diz o que ler depois.

---

## ⚠️ Primeira coisa a fazer

**Abrir o Unity e compilar.** O último trabalho (correção do `RaidAntiSub`) foi
escrito e **não passou pelo compilador**. Se der erro, é em um destes três:

```
Assets/Scripts/Units/UnitRole.cs
Assets/Scripts/Match/AI/Units/Air/AIController.AirCombat.cs
Assets/Scripts/Match/AI/3. Shopping/AIShoppingPlanner.Demand.cs
```

Depois: `Tools > AI > Semear Chaves de Captura` e `Tools > AI > Auditar Chaves de
Captura` — havia 11 fichas com entrada fantasma quando isto foi escrito.

---

## Estado

`v7.0.2` tagueada. Depois dela vieram três frentes: **eficiência de captura**,
**Melhor Visão** e a **matriz de papéis**.

O princípio que organiza tudo, e que já custou três propostas erradas por não
estar apontado no `CLAUDE.md`:

> **Uma habilidade não é um poder. É uma chave.** Quem define o que a etiqueta
> abre é o **alvo**, nunca a própria etiqueta.
>
> Teste antes de acrescentar qualquer campo: *o designer consegue renomear a
> etiqueta para qualquer coisa e tudo continua funcionando?*

E a formulação do autor que define o alvo da IA:

> **Todos os papéis atiram, mas nem todos atiram da mesma maneira.** É como raça
> de cachorro: mesma anatomia, expressão diferente.

---

## A escada

```text
0. sensores PodeX              ✅ prontos
1. serviços de área (Hotzone)  ✅ prontos
2. consumidores Melhor*        ⚠️ faltam DOIS: Combate e Fusão
3. papéis → só POLÍTICA        a matriz de docs/revisao_papeis.md
4. variações de papel          vira PARÂMETRO
```

Consumidores existentes: **Captura, Capitão, Visão, Desembarque, Embarque,
Estoque, Pouso**, mais `QueroCarona`.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/manual/01_principios_e_vocabulario.md` | as regras do **jogo**. Decide *onde uma regra pode morar* — não se recupera lendo código |
| 2 | `docs/revisao_papeis.md` | **a matriz papel × sensor.** O formato-alvo da IA |
| 3 | `docs/relatorio_v7.0.2.md` | a última versão fechada |
| 4 | `docs/AI Behavior/contrato_envelope_alcance.md` | norma das bandas |
| 5 | `docs/magnetic_tabela.md` | quem cada papel acompanha |
| 6 | `docs/ideias_melhorFow.md` | o Melhor Visão, e o pedido de spotter da artilharia |

---

## O plano em curso — Vigilância genérica

Cinco passos, definidos pelo autor. **O passo 1 foi escrito e não compilado; os
outros quatro não foram tocados.**

### ✍️ 1. `RaidAntiSub` — feito, não compilado

Era **bug vivo**, não limpeza: o shopping pedia `UnitRole.RaidAntiSub`, papel que
nenhuma ficha carrega. Contagem sempre zero, demanda disparando todo turno sem
poder ser preenchida.

O que foi feito:

- a demanda virou `Vigilancia` + `RequiredVisionDomain=Submarine` +
  `RequiredVisionHeight=Submerged`, no molde da demanda de vigilância aérea;
- `CountSurveillanceForLayer` conta por papel **e camada principal** — um EWACS
  não satisfaz demanda submarina;
- os dois ramos mortos do `AirCombat` viraram `IsArmedSurveillance`
  (vigilância **com arma**), o que devolve o Super Tucano ao pipeline aéreo por
  capacidade em vez de rótulo;
- `RaidAntiSub = 11` saiu do enum e virou comentário reservado.

**Não verificado:** se compila; se o Super Tucano de fato aparece no log de
`AirCombat`; se a demanda anti-sub passa a ser preenchível em partida.

### ⬜ 2. Perfil genérico e cobertura aliada

- preservar `IsAirSurveillanceUnit` — ele **já** significa "Vigilancia cuja
  camada principal é Air", e é o filtro que interceptador/rally precisam;
- criar o helper ausente `IsSurveillanceUnit`;
- tornar explícita no `MelhorVisaoRequest` a política de cobertura aliada.
  **Cuidado central:** uma unidade comum que enxerga o mar não pode fazer a
  Fragata acreditar que a cobertura `Submerged` já está garantida.

### ⬜ 3. Radar e EWACS consumindo Melhor Visão

Substituir a medição do `AirSurveillanceCoverageService`. Comparar decisões
antigas e novas **antes** de remover o wrapper.

### ⬜ 4. Super Tucano, Fragata, Submarino

Todos em `Submarine/Submerged`. Super Tucano mantém movimento cúbico; Fragata e
Submarino usam conectividade aquática. Origem pode vencer — ampliar área não
obriga movimento quando destrói cobertura exclusiva.

### ⬜ 5. Limpeza estrutural

`TryDecideAirSurveillanceAction` → `TryDecideSurveillanceAction`, renomear
pasta/controller, remover `AirSurveillanceCoverageService` após paridade.

---

## Serviços nascidos e ainda sem consumidor

Três, todos no mesmo estado: existem, têm ferramenta, **nenhum papel os chama**.

| serviço | ferramenta | falta |
|---|---|---|
| `MelhorCapitaoService` | `Tools > Hotzone > Melhor Capitão` | tradutor `AICaptainData → List<MelhorCapitaoAttraction>` e os predicados (`AliadoFerido`, `AeronaveInimigaDetectada`, `PontoDeObservacao`) |
| `MelhorVisaoService` | `Tools > Hotzone > Melhor Visão` | é o passo 3 do plano acima |
| `MelhorCapturaService` | `Tools > Hotzone > Melhor Captura` | **este já é consumido** — pelo claim service e pelo QueroCarona |

---

## Pendências abertas

**O `Rebel.cs` vazou para fora do capturador.**
`FindNearestPlanlessCaptureTarget` é chamado por Transporte (2), Assalto
(`HQBreaker`) e o rogue do capturador. Não é "o passo depois do capturador" — é a
**ponte para os degraus 4 e 5**. `IsRebelCapturable` já foi consertado por dentro
(delega ao sensor); o nome e os chamadores continuam.

**Sobram 7 varreduras de tabuleiro no `Capturer/`** e o `QueroCaronaContext`.

**O `roles[0] == CapturadorAgressivo` do `GetCapturePower` continua de pé.** Sai
só depois que as fichas agressivas trocarem para a chave `Capturador Alternativo`
(0.5) e a auditoria confirmar. Ordem e risco em `docs/ideias_futuras.md` item 10
— o modo de falha é silencioso.

**Os três "para onde revelar"** (`Capturer.Explorer`, `Transportador`,
`VigilanciaAerea`) ainda respondem por conta própria. O `MelhorVisao` existe para
substituí-los.

---

## Regras de trabalho (não são sugestão)

- **Uma classe por vez.** O autor compila e roda no jogo, e comita antes da
  próxima. **Não emenda fases.**
- **Avaliar não é executar.** Quando o autor pede avaliação de um plano, entregue
  a avaliação — não o código.
- **Verificar antes de documentar.** Busca vazia não prova ausência.
- **Ler `docs/manual/` antes de decidir onde uma regra mora.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Medir antes de otimizar.** Ler código não acha gargalo.
- **Não editar `.asset` no disco com o inspector aberto.**
- Fechar o dia: ver a skill `.claude/skills/fechamento-do-dia/`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **executar em vez de avaliar** | o autor pediu revisão de plano e recebeu quatro arquivos alterados |
| **projetar sem ler o manual** | três arquiteturas propostas contra o princípio da primeira página |
| **skill que se declara** | se renomear quebra, o poder está no lugar errado |
| **troca de tipo em lista serializada** | a Unity **preserva a contagem** do array antigo e deixa o conteúdo nulo. Não volta vazia — volta com fantasma |
| **gate inaplicável** | o shopping pedia papel que nenhuma ficha tem. Todo gate precisa separar "ainda não satisfeito" de "impossível" |
| **otimizar por hipótese** | cortar 80% das chamadas ao sensor não moveu o tempo |
| **comparar rodadas pós-load** | ordem reembaralhada e cache frio |
| **`FrameSpike` com F11** | mede o input humano junto. Use `decision=` |
| **`FindObjectsByType` dentro de laço** | se o chamador já tem o objeto, passe-o |
| **rota é cara** | 12-16ms por pathfind naval, 71 s numa decisão. Cúbica é limite inferior — dá pra podar exato |
| **`git add .`** | varre trabalho do Editor junto |
| **predicado no eixo errado** | `TeamId == unit.TeamId` é time, não slot — apagou a reconquista em quatro papéis |

---

## Aviso

Lista grande e organizada **parece progresso**. O antídoto é o ritmo acima.

O teste final é um só: **os 7 perfis chamando uma fonte única, não 7 perfis com 7
definições diferentes.** O jipe capturador já passou pelo lado do jogador; falta
o lado da IA.
