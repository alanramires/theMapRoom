# Relatorio de Atualizacao - v2.0.7

## AI Grafo Capturador

Esta versao consolida a evolucao do planner de capturadores para uma leitura de frente, cadeia e grafo de objetivos. A IA passa a ter uma base mais clara para pensar nao apenas no setor mais proximo, mas tambem no tempo de chegada, na continuidade da rota e no futuro handoff entre capturadores.

---

## Em uma frase

A IA de captura ganhou mais encapsulamento, debug pausavel e uma direcao de planejamento para operar capturadores como uma frente coordenada em vez de unidades isoladas.

---

## Principais pontos revisados

### 1. Reparo encapsulado em partial proprio

- A logica de reparo saiu do comportamento direto do capturador e foi isolada em `AIController.Repair.cs`.
- O novo partial centraliza gatilhos de HP, combustivel e municao, saida de predios parcialmente capturados, fusao oportunista e marcha ate construcao aliada.
- O objetivo e impedir que `AIController.Capturer.cs` cresca com responsabilidades de manutencao que tambem serao usadas por outras unidades.

### 2. Entrada generica de decisao de reparo

- O fluxo passou a usar `TryDecideRepairAction`.
- Capturadores chamam essa entrada antes da decisao de captura, mas o metodo ja esta preparado para ser reutilizado por outras familias de unidade.
- O planner agora pode liberar slots e limpar atribuicoes quando uma unidade entra em reparo, sem acoplar esse comportamento ao papel de capturador.

### 3. AI Pause e AI Resume no debug

- Os comandos `AI PAUSE` e `AI RESUME` voltaram a operar sobre o `AIController` atual.
- A pausa respeita o ponto seguro: o batch em execucao termina antes da coroutine da IA ficar bloqueada.
- Durante a pausa de debug, o bloqueio de input humano por turno de IA e suspenso, permitindo mover cursor, abrir menu e inspecionar o cenario.

### 4. Base para grafo de objetivos de captura

- O debate de planejamento passou a tratar setores como uma frente encadeada: por exemplo, um capturador pode abrir `Golf` e depois seguir para `Eco`, enquanto reforcos finalizam o setor anterior.
- O planner atual ainda usa slots sticky por setor, mas o proximo passo definido e permitir handoff controlado de objetivos parcialmente capturados.
- A leitura estrategica deixa de ser apenas "qual setor vale mais agora" e passa a considerar lead time, continuidade da rota e custo de deixar objetivos distantes para turnos futuros.

### 5. Capturador HandOff como proximo passo

- O plano conceitual de handoff define uma captura em esteira:
  - capturador A inicia uma captura parcial;
  - capturador B comprado ou recem-chegado herda o objetivo;
  - capturador A avanca para o proximo setor da cadeia.
- O handoff deve ser bloqueado quando houver ameaca visivel local, quando nao existir substituto viavel ou quando abandonar o objetivo atrasar a captura.
- Esta versao registra a direcao de design sem ainda implementar o handoff final.

---

## Bloco tecnico curto

- Arquivos principais desta etapa: `AIController.cs`, `AIController.Capturer.cs`, `AIController.Repair.cs`, `AIController.PlanEvaluator.cs`, `MatchController.cs` e `DebugManager.cs`.
- `AIController` ganhou pontos de pausa segura entre fases, unidades e compras.
- `MatchController.IsPlayerInputLockedByActiveAI` passa a respeitar `AIController.IsDebugPaused`.
- O reparo agora e uma porta de decisao propria, separada do fluxo de captura.
- O planejamento futuro do capturador fica documentado como grafo/frente de objetivos, preparando o terreno para slots de escolta e handoff.

---

## Resultado

A base da IA de captura ficou mais organizada para evoluir. O capturador continua executando o plano atual, mas o sistema agora separa manutencao, permite pausar a simulacao para inspecao e estabelece a proxima direcao: transformar a atribuicao de setores em um grafo de frente com continuidade e handoff.

---

## Validacao

Build C# executado:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
```

Resultado: 0 erros. Permanecem warnings antigos de APIs obsoletas do Unity.
