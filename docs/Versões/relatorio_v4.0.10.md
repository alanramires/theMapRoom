# v4.0.10 - AI Invasion and Damaged Units

Esta versão dá à **invasão** um estado de primeira classe na IA e organiza o comportamento das **unidades danificadas** (em reparo) durante a operação ofensiva. O fio condutor é a coerência: o mesmo estado de invasão alimenta o HUD, o planner, a ordenação de ações e as regras de embarque/fusão — e tudo sobrevive ao save/load.

## Invasão como macro-estado

- A invasão (operação GoGreen em andamento) passa a ser um **macro-estado sobreposto à postura ofensiva**, exposto como `AIWorldSnapshot.IsInvading` — **não** um novo valor de `AIStance`. Substituir a postura quebraria as dezenas de gates `== Offensive` que dirigem ar, fogo e compra; a invasão é ortogonal (a IA pode estar ofensiva **e** invadindo).
- `IsInvading` é populado tanto no snapshot completo quanto no `BuildLight` (consumido pelos handlers da Fase 2), via lookup barato no registro de GoGreen.
- **Persistência**: o registro GoGreen vive num dicionário estático que não pertence a nenhum `SectorObjective` depois que o rally é consumido. Agora é serializado à parte em `AIObjectivePlanSaveData`, então a invasão "em andamento" sobrevive ao save/load — incluindo o resume a partir do Stage 2.

## HUD coerente (Shopping Pressure)

- O painel `Tools > Utils > Shopping Pressure` mostra **"Invasão em Andamento"** (com setores e turno de início) quando o GoGreen está em execução, em vez de "sem rally de invasão ativo".
- O rótulo de postura passa a exibir **"Invasão"** enquanto a operação está em voo.
- O **semáforo do ponto de rally** segue a OPERAÇÃO, não a massa parada no ancoradouro: fica verde enquanto a invasão está em voo (mesmo sem rally objective ativo e após um load), e cai para amarelo quando a operação falha e a montagem reabre.

## Re-montagem por desfecho (2ª onda)

- A supressão pós-GoGreen deixa de soltar **no relógio**: o release agora é por **desfecho**. `RallyGoGreenSuppressTurns` (12) vira apenas um teto de segurança.
- `UpdateInvasionMonitor` (topo do `BuildObjectivePlan`) detecta fracasso por **OR(colapso, estagnação)** sobre a força de invasão (unidades vivas nos objetivos de base inimiga):
  - **colapso**: nenhum Assalto vivo atribuído (o breaker morreu);
  - **estagnação**: a menor distância-terrestre até o alvo não melhora por 3 turnos, com guarda anti-falso-positivo (não conta quando está assaltando o portão, em contato ou em hold de cabeça-de-ponte); graça de 2 turnos antes de julgar.
- Ao falhar, a supressão é liberada e os rallies **reabrem a montagem da 2ª onda pelo portão normal** (sem conta-gotas). A frente sobrevivente **não recua** — segue nos slots do objetivo de base. Efeito único para colapso e estagnação: reforçar, nunca recolher.
- O monitor (`bestDistance`, `stallCounter`) também é persistido por time no save.

## Unidades danificadas na invasão

- **Iniciativa**: durante a invasão, unidade **ferida (em reparo)** age primeiro (grupo 1) em vez de por último (grupo 5), liberando o corredor de avanço antes que a coluna pathe pelo hex dela. É só ordenação — recuar/curar/seguir continua a cargo do handler. Fora da invasão, segue por último (sem regressão).
- **Fusão segura**, em duas camadas:
  - *sempre*: nunca fundir na cara do inimigo — mesma noção de segurança do evac (`IsEvacDropCellSafe`: sem inimigo no hex e sem inimigo visível por perto), independente de orientação;
  - *na invasão*: além disso, só na **retaguarda segura** (atrás da linha), usando a ferramenta de retaguarda (`AIBacklineAnalyzer`) com o HQ inimigo como referência de frente — evitando que as feridas empurradas pra frente se fundam no raio do HQ inimigo.

## Embarque de artilharia de reboque

- Unidade com a skill **"Precisa de Reboque"** (`precisaReboque`, ex.: Artilharia de Campanha) só embarca no supridor durante a **invasão**. Fora dela, o embarca/desembarca descoordenado confundia a IA; na invasão a operação é coordenada e o tow funciona certinho.
- O gate é por **skill**, não por papel ou nome — apoio de fogo sem reboque não é afetado.
- O supridor também ignora candidatos de reboque fora da invasão, evitando dirigir até uma unidade que não vai embarcar.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
- `Assembly-CSharp-Editor.csproj`: build sem erros.
- `git diff --check`: sem erros de whitespace.
