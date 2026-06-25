# Eixo de Captura — Documento de Design

> Documento de apresentação. Objetivo: explicar o conceito de **Eixo de Invasão/Captura** da AI, a arquitetura, as intenções de design, o que já está validado e o que falta — de forma honesta o bastante para sobreviver a um leitor crítico.

---

## 1. Resumo executivo

O **Eixo de Captura** é uma camada de *master plan* acima da camada de objetivos da AI. Enquanto os objetivos respondem à pergunta tática *"que setor capturo agora?"*, o eixo responde à pergunta operacional *"por quais corredores eu avanço, e onde termino?"*.

Cada **rally point** do mapa define um eixo: um corredor ordenado do HQ até aquele rally, com os setores intermediários atribuídos por geometria. As unidades ganham uma **identidade de eixo** (a "faixa" a que pertencem) que persiste entre objetivos, e o planner é enviesado para (a) avançar cada corredor em ordem e (b) manter cada unidade na sua faixa, rebalanceando só quando um eixo fica descoberto.

O sistema foi construído com uma disciplina deliberada: **primeiro o dado, validado visualmente; só depois o comportamento.** Hoje o dado (classificação, frente, identidade) está pronto e validado em jogo; a influência no comportamento está nas primeiras regras (avanço de corredor + estabilidade de faixa).

---

## 2. O que é um Eixo de Captura

Um eixo é a materialização runtime de uma **linha de avanço**, no sentido militar:

- **Apex:** o HQ do time (origem das forças).
- **Terminal:** um rally point (ponto de concentração / invasão). O número de eixos = número de rallys do time.
- **Corredor:** a sequência ordenada de setores entre o HQ e o rally, do mais perto ao mais longe.
- **Frente (leading edge):** o ponto do corredor onde o avanço está agora — o primeiro setor ainda não conquistado, caminhando do HQ pra fora.

Visualmente, são as faixas que o editor desenha em "Desenhar eixos": leques que partem do HQ e se abrem até os rallys, cada setor pertencendo a exatamente uma faixa.

---

## 3. Intenções de design (o porquê)

**3.1. Dar à AI uma intenção operacional, não só tática.**
Sem eixos, a AI escolhe setores por prioridade pontual e tende a pegar alvos espalhados — captura aqui, captura ali, sem formar uma linha. O eixo impõe uma *direção*: avançar corredores conectados rumo a pontos de concentração, como um general pensaria a campanha.

**3.2. Dar identidade e coesão às unidades.**
Tropas têm "esprit de corps": quem está no eixo 1 tende a permanecer no eixo 1. Isso evita o vai-e-vem de unidades cruzando o mapa a cada turno (que parece burro ao observador) e cria frentes estáveis. A troca de faixa acontece — mas como exceção justificada (handoff, rebalanceamento), não como ruído.

**3.3. Equilíbrio antes de aderência.**
A coesão não pode ser uma camisa de força. Se um eixo está vazio e outro lotado, manter a unidade presa é o comportamento errado. O design distingue **grudar na faixa** (penalidade) de **rebalancear** (recompensa), favorando cobertura de eixos famintos.

**3.4. Fonte única de verdade.**
A visualização do editor e a classificação que o planner usa são **o mesmo código** (`InvasionAxisMap`). Isso elimina a classe inteira de bugs "o desenho diz uma coisa, a AI faz outra" — o que o crítico vê na tela é literalmente o que a AI raciocina.

**3.5. Disciplina descritivo → validação → comportamento.**
Cada incremento primeiro produz um *dado* (sem efeito de jogo), que é *validado visualmente*, e só então vira *comportamento*. Essa disciplina foi cara de aprender: várias heurísticas de "cabeça de eixo" (grafos de vizinho-traseiro, agrupamento por raiz de cascata, híbrido fronteira-cabeça) foram tentadas e revertidas por quebrarem invariantes geométricos. O leque angular puro venceu por ser **auditável**.

---

## 4. Como funciona (arquitetura)

### 4.1. Classificação — o leque angular (`InvasionAxisMap`)

Para cada eixo, mede-se a direção `HQ→rally` em *world space* (evitando a distorção da grade hexagonal). Cada setor de campo é atribuído ao rally cuja direção é a **mais próxima em ângulo** da direção `HQ→setor`. Propriedades:

- **Fatias disjuntas** — um setor pertence a um único eixo; as faixas não se cruzam nem repetem.
- **Corredor ordenado por distância** — sempre "pra frente", do HQ ao rally.
- **Numeração 1..N por ângulo** (esquerda→direita), estável e legível.

A robustez: o classificador resolve o HQ-dono de cada rally entre **todos** os HQs (de todos os times) e só então filtra pelo time pedido — senão um rally inimigo cairia no fallback "HQ mais próximo" e se prenderia ao eixo errado (bug real que ocorreu e foi corrigido).

### 4.2. A frente (`Axis.FrontSector` / `FrontIndex` / `Complete`)

`ComputeFront` caminha o corredor de HQ pra fora e para no primeiro setor cujo `ControllingTeam != team` — esse é o próximo alvo. Se o corredor inteiro é seu, a frente vira o setor do rally; se até o rally é seu, o eixo está completo.

A frente atualiza no **rebuild do SectorManager**, que dispara na **virada de turno** (`OnActiveTeamChanged`). É a cadência certa: o rebuild roda antes do `BuildObjectivePlan`, então quando a AI decide, a frente já reflete a verdade do turno.

### 4.3. Identidade de eixo nas unidades (`aiEixo`)

Cada unidade carrega um `aiEixo` (0 = rogue/fora de eixo, 1..N). É atribuído no ponto único de etiquetagem (`ApplyPlanHUD`), herdando o eixo do setor do seu objetivo. Decisões importantes:

- **Persiste como memória.** Liberar o plano (handoff, captura concluída, virar rogue) **não** zera o `aiEixo`. A unidade pode passar um turno ociosa e voltar para a mesma faixa — a identidade sobrevive aos buracos. Isso é o que permite a estabilidade funcionar no handoff.
- **HUD desacoplado do plano.** A bandeirola (eixo1/2/3) é exibida enquanto a unidade tiver eixo, for da AI e o flag global "Show AI Unit HUD" estiver ligado — independente de ter plano ativo. O que se vê = o que o planner usa.
- **Persiste no save/load.**

### 4.4. Influência no planner

Duas regras já materializadas (a "onda"):

**R1 — A ponta avança.** `GetAxisFrontPriorityBonus` soma `+30` à prioridade do `FrontSector` de cada eixo. A AI prefere o próximo nó do corredor *em ordem*, empurrando o eixo como linha conectada em vez de pegar setores espalhados. Conquistou a frente → no turno seguinte a frente anda um nó → o bônus migra junto. É auto-propulsor.

**Estabilidade + rebalanceamento de faixa.** Somado ao custo de atribuição (o solver de backtracking que casa unidade↔objetivo), `CalculateAxisStabilityCost`:
- mesmo eixo / rogue / alvo-fora-de-eixo → neutro;
- eixo-alvo **igual ou mais coberto** → **penalidade** (`+10`): gruda na faixa;
- eixo-alvo **faminto** (≥2 unidades a menos que o atual) → **recompensa** (`−10`): puxa pra cobrir.

O limiar de `≥2` garante que a unidade nunca **esvazia** a própria faixa pra encher outra (move de 2→1, enche 0→1, fica balanceado, sem thrash). A presença por eixo é contada no início do turno (antes dos releases de handoff, pra incluir a unidade que vai sair).

Caso de uso real: tropa em Bravo (eixo 1) dando handoff é tentada a ir pra Foxtrot (eixo 1) em vez de Hotel (eixo 2, mais perto) — a não ser que o eixo 2 esteja vazio, quando o rebalanceamento a manda cobrir Hotel.

---

## 5. Metodologia de validação

Cada passo seguiu o mesmo rito:

1. **Construir o dado** (ex.: classificação, frente) sem efeito de jogo.
2. **Tornar o dado visível** (bandeirolas no HUD, marcadores no editor) e **conferir contra a realidade** — os badges de eixo das unidades têm de bater com o leque desenhado; a frente tem de cair no próximo setor a tomar.
3. **Só então ligar o comportamento** (bônus de prioridade, custo de atribuição), com magnitudes tratadas como *dials* a calibrar em playtest.

Esse rito é o que protege contra "heurística invisível quebrada" — o problema que mais custou tempo no histórico do projeto.

---

## 6. Etapa atual (pronto e validado)

- ✅ Classificação por leque angular (`InvasionAxisMap`), fonte única editor+planner.
- ✅ Bandeirolas de eixo no HUD, validadas em jogo (cores batendo com o desenho).
- ✅ Frente do eixo (dado + marcador no editor).
- ✅ `aiEixo` persistente (memória entre objetivos + save/load).
- ✅ R1: bônus de avanço de corredor.
- ✅ Estabilidade + rebalanceamento de faixa.
- ✅ Correção de pressão de transporte (não pedir APC quando a frente já tem capturador perto).

**Status do comportamento:** primeiras regras ligadas; magnitudes (`+30`, `±10`) ainda em calibração de playtest.

---

## 7. Próximos passos

- **R3 — Reforçar a frente.** Mandar unidade sobrando para o `FrontSector` do eixo (reforço da ponta), usando frente + presença que já existem.
- **R4 — Transporte por eixo (1º corte FEITO).** Paradigma do transportador terrestre trocado de *reativo* (carona por-objetivo) para *sinal por-eixo escalado pela profundidade da frente*: frente rasa → pressão zero (a pé resolve); frente profunda (setores segurados atrás) → 1 APC (o próximo capturador nasce no HQ e cruza tudo). Teto 1/eixo natural (demanda só no `FrontSector`). Falta: prioridade por eixo, rally-hold explícito, "prepara Foxtrot" sem objetivo na frente, air transport. Detalhado em [`plano_transportador_eixo.md`](plano_transportador_eixo.md).
- **R2 — Consolidação do seguidor** (formalizar a retaguarda guarnecer enquanto a ponta avança).
- **Calibração** das magnitudes de R1 e estabilidade.

---

## 8. Tensões, tradeoffs e limitações conhecidas

Esta seção é deliberadamente desconfortável — é o que um crítico atacaria primeiro.

**8.1. Geometria (leque) × seleção real do planner (espalhamento/fronteira).**
O leque angular é uma partição *geométrica*. A escolha real de setores iniciais do planner usa espalhamento/cascata/fronteira, que pode divergir do leque. Optou-se pelo leque por ser **auditável visualmente**; a reconciliação "a fronteira decide a cabeça, o ângulo desenha o corpo" foi tentada e revertida por quebrar os invariantes (fatias disjuntas + monotonicidade). Consequência: em mapas patológicos, a faixa desenhada pode não ser exatamente o caminho que o planner percorreria. Mitigação atual: a identidade de eixo é usada como *viés*, não como *trilho* — o planner ainda decide por prioridade/distância.

**8.2. Presença estática.**
A contagem de presença por eixo é um snapshot no início do turno; não decrementa conforme as atribuições acontecem dentro do mesmo turno. Em teoria, dois eixos lotados poderiam ambos "doar" para um vazio no mesmo turno. Na prática, os tetos de slot e o `≥2` limitam o efeito. Recontagem dinâmica é trabalho futuro se aparecer patologia.

**8.3. Frente é granular por turno.**
`ControllingTeam` só atualiza no rebuild da virada de turno, não ao vivo no meio do turno. Para o planner está correto (ele decide na virada). Para visualização ao vivo, o editor é um snapshot (reclicar para atualizar).

**8.4. Magnitudes são dials, não verdades.**
`+30` (frente), `±10` (faixa) foram escolhidos por raciocínio (acima do bônus de vizinho `−6`, abaixo do rally `+55`), não por otimização. Precisam de playtest. Um crítico tem razão em pedir dados de calibração — ainda não temos.

**8.5. Transporte ainda é reativo.**
A pressão de transporte hoje responde a um capturador específico já longe, não à intenção do eixo. O modelo "general" (antecipar a próxima frente, preparar Foxtrot antes de comprometer) está desenhado mas não implementado (R4).

**8.6. Casos de borda multi-HQ.**
A classificação assume tipicamente 1 HQ por time. Múltiplos HQs por time são tratados por grupo, mas a numeração global por ângulo entre grupos distintos é uma aproximação.

---

## 9. A pressão de shopping como equação de controle (contexto de design)

O transporte é parte de um problema maior: o shopping é um **arbitrador entre pressões concorrentes** sob orçamento. Cada papel é um sinal com **fonte (acúmulo)**, **teto (saturação)** e **alívio (decaimento)**; a arte é nenhum saturar e dominar:

| Pressão | Sobe com | Teto | Cai quando |
|---|---|---|---|
| Assalto | força inimiga | pacote `2/2/1` | compra / massa formada |
| Artilharia | volume inimigo (represália) | anti-spam | compra |
| Logística | feridos acumulando | `ceil(reparo/2)` | reparo concluído |
| Transporte | profundidade da frente do eixo / rally segurado | 1 por eixo | compra |

A intenção de design é que o **transporte seja o único papel cujo valor é antecipatório/posicional** — ele prepara o terreno que se vai tomar, não responde ao inimigo presente. Por isso pertence à camada do eixo. O teto "1 por eixo" é o que garante estruturalmente o "não pode dominar".

---

## 10. Referências de código

- `Assets/Scripts/Match/AI/2. Planner/InvasionAxisMap.cs` — classificação, corredor, frente.
- `Assets/Editor/SectorManagerEditor.cs` — "Desenhar eixos" (consome `InvasionAxisMap`).
- `Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.cs` — `BuildObjectivePlan`, `currentAxisMap`, R1 (`GetAxisFrontPriorityBonus`), estabilidade (`CalculateAxisStabilityCost`), presença (`BuildEixoPresence`).
- `Assets/Scripts/Match/AI/2. Planner/AIController.PlanEvaluator.Helpers.cs` — `ApplyPlanHUD` (etiquetagem do `aiEixo`).
- `Assets/Scripts/Units/UnitManager.cs` / `UnitHudController.cs` — `aiEixo`, bandeirola.
- `Assets/Scripts/Match/AI/3. Shopping/Demand/AITacticalAnalyzer.Builders.cs` — pressão de transporte (atual e ponto de partida do R4).
- `docs/plano_transportador_eixo.md` — plano do R4.
