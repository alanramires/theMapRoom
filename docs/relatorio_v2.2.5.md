# Relatorio v2.2.5 - AI Road Booster

## Tema

Correcao de tres bugs encadeados que impediam a IA de transporte de aproveitar o bonus de movimento em rodovias ao tomar decisoes de rota.

## Principais mudancas

### 1. Distancia euclidiana substituida por hexagonal em `CalculateThreatLevel`

`CalculateThreatLevel` usava `Vector3Int.Distance` (Pitagoras) para medir a distancia entre o inimigo e a celula avaliada. Em um grid hexagonal com coordenadas offset, isso produz uma zona de ameaca eliptica no espaco de grade em vez de circular em hexagonos. O efeito visivel era valores fracionarios como `threat=29.36` em vez de multiplos limpos de 10 (30, 20, 10). Corrigido para `SectorManager.HexDistance`.

### 2. Peso de ameaca excessivo bloqueava progresso de entrega (`threat * 8f → * 0.5f`)

Em `FindTransportMove` e `FindTransportExplorationMove`, a formula aplicava `threat * 8f` ao score de horizonte. Com um unico inimigo adjacente (threat=30), isso gerava penalidade de **240 pontos** contra uma vantagem de progressao de apenas ~17 pontos — a ameaca sempre vencia, qualquer que fosse a rota. Para um courier em missao de entrega, ameaca deve ser desempate suave. Multiplicador reduzido para `0.5f`: inimigo adjacente = 15 pontos de penalidade.

### 3. Celulas alcancadas via bonus de estrada eram super-penalizadas em custo (`TryScoreTwoTurnProgression`)

`CalculateMovementCostMap` nao implementa o bonus de rodovia (+1 passo livre ao percorrer toda a rota em estrada). Celulas alcancaveis somente via esse bonus nao aparecem no mapa de custos (`costFromOrigin`). O fallback anterior usava `caminho.Count - 1` (numero de waypoints), que para um passo bonus resulta em custo 7 em vez do real 6 MP — penalizando injustamente a celula em 2 pontos. Corrigido: quando a celula esta nos caminhos validos mas ausente do mapa de custos, usa-se `unit.RemainingMovementPoints` como custo (igual ao MP total gasto, pois o passo bonus consume exatamente o budget inteiro).

## Comportamento esperado

- APC em rodovia consegue alcançar a celula extra do bonus de estrada e a IA a escolhe corretamente quando oferece melhor progressao ao objetivo.
- Inimigos visiveis proximos continuam influenciando a rota de transporte, mas sem vetar completamente hexes com vantagem de entrega.
- Valores de ameaca sao agora multiplos de 10, consistentes com o raio hexagonal de `ThreatRadius = 3`.

### 4. Ferramenta de progressao atualizada (`CaminhosValidosWindow`)

A aba de Progressao da ferramenta "Caminhos Validos" foi refatorada para alinhar com a logica real da IA:

- **Slider de horizonte (1 ou 2 turnos):** modo 1 exibe apenas o progresso imediato; modo 2 ativa `CalculateTwoTurnProgressScore`, identico ao score usado pela IA.
- **Custo real em PM:** a ferramenta agora computa `costMap` via `CalculateMovementCostMap` a partir da origem e passa para a funcao de score, substituindo a contagem de waypoints.
- **`CanUseAsDebugStopCell`:** filtra celulas onde a unidade nao pode realmente parar (aliados ocupando o hex), eliminando resultados falso-positivos na visualizacao.
- **Secao "Passando Por" comentada:** substituida pela analise de dois turnos, que cobre o mesmo caso de uso com mais fidelidade.
- **`CalculateReachableProgressScore`:** funcao de horizonte simples (1 turno) adicionada como alternativa leve ao calculo completo de 2 turnos.

### 5. AI Intel Analyzer (`AIIntelAnalyzer.cs`) — novo componente

Novo `MonoBehaviour` que produz um relatorio estruturado (`AIIntelReport`) sobre o estado atual da IA para uma equipe:

- Consolida snapshot de objetivos, operacoes ativas, setores disputados, unidades por papel e estado de compras.
- Atualiza automaticamente ao trocar de time ativo, apos load, no Start/Enable ou a cada frame (configuravel por flags).
- Exibe resumo legivel em `[TextArea]` no Inspector para inspecao em tempo de execucao sem abrir logs.
- Integrado ao `AIOperationManager`: novas consultas `TryGetCaptureOperationForObjective` e `IsFireSupportScreenedForObjective` alimentam o relatorio de inteligencia operacional.

### 6. AIOperationManager — consultas de inteligencia operacional

- `TryGetCaptureOperationForObjective`: localiza a operacao de captura vinculada a um `SectorObjective`.
- `IsFireSupportScreenedForObjective`: verifica se uma unidade de fogo indireto possui escolta adequada antes de atirar, usando a operacao vinculada ao objetivo.
- Construcao de operacoes refatorada para receber e usar `AIIntelReport` como contexto.

## Arquivos alterados

- `Assets/Scripts/Match/AI/AIController.Capturer.Helpers.cs` — `CalculateThreatLevel`: euclidiana → hexagonal
- `Assets/Scripts/Match/AI/AIController.Transportador.cs` — `FindTransportMove` e `FindTransportExplorationMove`: `threat * 8f → * 0.5f`
- `Assets/Scripts/Match/AI/AIController.Progression.cs` — novo arquivo; `TryScoreTwoTurnProgression` com fallback de custo para celulas de bonus de estrada
- `Assets/Editor/CaminhosValidosWindow.cs` — ferramenta de progressao com horizonte configuravel, custo real e `CanUseAsDebugStopCell`
- `Assets/Scripts/Shared/Jogadas/AIIntelAnalyzer.cs` — novo componente de inteligencia operacional
- `Assets/Scripts/Shared/Jogadas/JogadasManager.cs` / `JogadasLog.cs` — sistema de registro de jogadas
- `Assets/Scripts/Match/AI/AIOperationManager.cs` — consultas de intel e construcao de operacoes com contexto de relatorio

## Validacao

- Build validado com `dotnet build Assembly-CSharp.csproj`.
- Resultado: 0 erros; permanecem apenas warnings conhecidos de APIs obsoletas do Unity.
