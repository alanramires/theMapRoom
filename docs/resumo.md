# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-03, depois da `v7.0.3`. Leia isto
primeiro; ele diz o que ler depois.

---

## Primeira coisa a fazer

**Rodar a Vigilância no Unity.** Ela foi migrada e tagueada compilando, mas
**nunca executou**. Cinco fichas para observar — EWACS, Radar Móvel, Super
Tucano, Fragata, Submarino — e três perguntas concretas:

1. a **fragata** ganha iniciativa 1 e ilumina **antes** de a artilharia gastar a
   ação? (era o motivo da mudança)
2. o `AlliedObserverFilter` impede que um aliado qualquer "satisfaça" a cobertura
   `Submerged` e faça a fragata parar de caçar?
3. sem tiro legal, o `TryDecideAirCombatAttackOnly` devolve autoridade ao
   `MelhorVisao` — ou a unidade congela?

Depois: `Tools > AI > Semear Chaves de Captura` e `Tools > AI > Auditar Chaves de
Captura` — havia 11 fichas com entrada fantasma.

---

## Estado

`v7.0.3` tagueada. O dia teve três frentes: **Vigilância genérica**, **matriz de
papéis** e o **engenheiro** (só registrado).

### A descoberta que organiza o resto

> *Terminei a migração do MelhorVisão para a Vigilância… e isso destrancou a
> biologia.*

**Taxonomia não serve para nada enquanto cada bicho tem órgão próprio.** Enquanto
três lugares respondiam "para onde revelar" cada um à sua maneira, classificar
papéis era decorar nomes. Quando o órgão virou **um só**, a pergunta mudou:

```text
antes    "como ESTE aqui enxerga?"      →  implementação, uma por papel
depois   "este aqui EXPRESSA o órgão?"  →  coluna, e colunas viram tabela
```

**A matriz não destrancou a biologia — a unificação do primeiro órgão
destrancou.** Daí a ordem do trabalho que vem: *unifique o órgão, e a linha da
matriz se escreve quase sozinha.* Não o contrário.

### Os dois princípios que já custaram propostas erradas

> **Uma habilidade não é um poder. É uma chave.** Quem define o que a etiqueta
> abre é o **alvo**. Teste: *o designer renomeia a etiqueta para qualquer coisa e
> tudo continua funcionando?*

> **"Não se aplica" nunca foi propriedade do papel — era propriedade da ficha.**
> O capturador raiz não supre porque *aquele* `UnitData` não tem `isSupplier`.

---

## A escada

```text
0. sensores PodeX              ✅ prontos (falta PodeConstruir, se o engenheiro nascer)
1. serviços de área (Hotzone)  ✅ prontos
2. consumidores Melhor*        ⚠️ faltam DOIS: Combate e Fusão
3. papéis → só POLÍTICA        docs/revisao_papeis.md — 1 linha de 7 levantada
4. variações de papel          vira PARÂMETRO
```

Consumidores existentes: **Captura, Capitão, Visão, Desembarque, Embarque,
Estoque, Pouso**, mais `QueroCarona`.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/manual/01_principios_e_vocabulario.md` | as regras do **jogo**. Decide *onde uma regra pode morar* — não se recupera lendo código |
| 2 | `docs/revisao_papeis.md` | **a matriz papel × sensor**, o levantamento do Capturador e o brainstorming das raças |
| 3 | `docs/relatorio_v7.0.3.md` | a última versão fechada |
| 4 | `docs/AI Behavior/contrato_envelope_alcance.md` | norma das bandas |
| 5 | `docs/magnetic_tabela.md` | quem cada papel acompanha |

---

## Onde eu parei

### Vigilância genérica — feita, não rodada

`Units/Vigilancia Aerea/` → `Units/Vigilancia/` (GUIDs dos `.meta` preservados).
O núcleo é `SurveillanceProfile`, que **carrega** a camada da ficha em vez de
assumi-la; `IsAirLayer` virou pergunta, não premissa.

Dois helpers coexistem **de propósito** — não unifique:

| helper | significa |
|---|---|
| `IsAirSurveillanceUnit` | Vigilância cuja camada principal é **Air**. É o que interceptador, rally e plataforma precisam perguntar |
| `IsSurveillanceUnit` | **qualquer** camada. Governa a iniciativa |

`AirSurveillanceCoverageService` (435 linhas) foi removido — era protótipo
parcial do `MelhorVisaoService`. Saldo da frente: **−2.368 / +273**.

### A matriz — 1 linha de 7

O Capturador foi levantado (19 arquivos, 5.823 linhas). Três marcas de trabalho:

- **`Fundir` é branco de verdade** — zero referências ao `PodeFundirSensor`, e
  não é "não se aplica": infantaria fundir para se curar é mecânica central;
- **`Ver` é respondido sem sensor** — `C.Explorer`, 462 linhas, seis constantes
  de peso próprias;
- **`Mirar` e `Embarcar` são hipertróficos** — o papel gasta mais código atirando
  (38 ocorrências) do que capturando (13), e `Embarcar` ocupa 1.287 linhas em
  cinco arquivos.

Os **dez modos** do capturador já são o degrau 4 materializado como arquivos. A
matriz não precisa criá-los — precisa transformá-los em linhas que declaram só o
que difere.

**Faltam seis linhas.** O autor pediu para começar pelo capturador; não avance
para os outros papéis sem confirmar.

### Raças mistas — a forma da célula

Uma célula da matriz pode ser **uma cadeia**, não só uma política. Três formas, e
as três já rodam no projeto:

| forma | onde já roda |
|---|---|
| política única | `MelhorCaptura` |
| cadeia ordenada | lista de atração do `AICaptainData` |
| herdada do parente | `CanSatisfy` |

---

## Pendências abertas

**Dois dos três "para onde revelar" continuam à mão** — `Capturer.Explorer` e
`Transportador`. São as próximas colunas que o órgão unificado libera, e pela
lição do dia é por aí que se avança.

**`MelhorCapitao` continua sem consumidor.** Falta o tradutor `AICaptainData →
List<MelhorCapitaoAttraction>` e os predicados (`AliadoFerido`,
`AeronaveInimigaDetectada`, `PontoDeObservacao`).

**`roles[0] == CapturadorAgressivo` de pé** no `GetCapturePower`. Sai só depois
que as fichas agressivas trocarem para a chave `Capturador Alternativo` (0.5) e a
auditoria confirmar. Ordem e risco em `docs/ideias_futuras.md` item 10 — **o modo
de falha é silencioso**.

**O `Rebel.cs` vazou para fora do capturador.**
`FindNearestPlanlessCaptureTarget` é chamado por Transporte (2), Assalto
(`HQBreaker`) e o rogue do capturador. É a ponte para os degraus 4 e 5.

**Sobram 7 varreduras de tabuleiro no `Capturer/`** e o `QueroCaronaContext`.

**A metade de IA do critério do jipe** nunca foi testada; só o lado do jogador.

**`TransportadorAereo = 15` ainda existe** no enum com política de shopping
própria, apesar de a governança dizer que "foi incorporado". Mudou de pasta; a
regra não migrou.

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
- Fechar o dia: skill `fechamento-do-dia`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **classificar antes de unificar o órgão** | a matriz existia desde a manhã e não produziu nada; o que produziu foi a Vigilância consumindo o `MelhorVisao` |
| **executar em vez de avaliar** | o autor pediu revisão de plano e recebeu quatro arquivos alterados |
| **projetar sem ler o manual** | três arquiteturas propostas contra o princípio da primeira página |
| **skill que se declara** | se renomear quebra, o poder está no lugar errado |
| **cobertura aliada sem filtro** | um aliado qualquer que enxerga o mar faria a fragata achar que `Submerged` está coberto e parar de caçar |
| **troca de tipo em lista serializada** | a Unity **preserva a contagem** do array antigo e deixa o conteúdo nulo. Volta com fantasma, não vazia |
| **gate inaplicável** | o shopping pedia papel que nenhuma ficha tem. Todo gate precisa separar "ainda não satisfeito" de "impossível" |
| **otimizar por hipótese** | cortar 80% das chamadas ao sensor não moveu o tempo |
| **comparar rodadas pós-load** | ordem reembaralhada e cache frio |
| **`FrameSpike` com F11** | mede o input humano junto. Use `decision=` |
| **`FindObjectsByType` dentro de laço** | se o chamador já tem o objeto, passe-o |
| **rota é cara** | 12-16ms por pathfind naval. Cúbica é limite inferior — dá pra podar exato |
| **`git add .`** | varre trabalho do Editor junto. Só no passo de churn |
| **tag antes do commit final** | obriga a mover referência já publicada com `--force` |
| **predicado no eixo errado** | `TeamId == unit.TeamId` é time, não slot — apagou a reconquista em quatro papéis |

---

## Aviso

Lista grande e organizada **parece progresso**. O antídoto é o ritmo acima.

O teste final é um só: **os 7 perfis chamando uma fonte única, não 7 perfis com 7
definições diferentes.**
