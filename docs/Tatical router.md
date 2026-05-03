Sim, sua leitura está certa. Hoje o AI não é “general tático”; ele é mais um **roteador guloso por unidade**.

O fluxo atual é assim:

1. `Phase2_UnitActions` monta a lista de unidades disponíveis.
2. Ordena por grupos simples em `GetInitiativeGroup`:
   - reparo/vacater/blocker primeiro,
   - unidades com objetivo depois,
   - rogue depois.
3. Escolhe `available[0]`.
4. Só então `AIController.Router.cs` decide a melhor ação daquela unidade.
5. Executa, recalcula snapshot/FoW, repete.

Ou seja: ele pensa bem depois que escolhe a unidade, mas escolhe a unidade com uma heurística rasa. E tem um detalhe pior: dentro do mesmo grupo, quando o comparer retorna `0`, o `List.Sort` do C# não é estável. Então unidades empatadas podem parecer “meio sorteadas”. Isso explica bem esse sentimento de “escolho o pikachu”.

O que falta é uma camada entre `Phase2` e `Router`:

**Tactical Scheduler**
Em vez de:

```csharp
UnitManager unit = available[0];
PlayerAction action = DecideUnitAction(unit, current);
```

a IA deveria fazer:

```csharp
TacticalCandidate best = PickBestNextTacticalAction(available, current, activePlan);
UnitManager unit = best.Unit;
PlayerAction action = best.Action;
```

Essa camada avaliaria “quem deve agir agora” antes de chamar o router definitivo.

Para o caso que você descreveu, eu colocaria essas regras:

1. **Dono do objetivo tem preferência**
   Se o inimigo está em Echo, unidades alocadas para Echo devem ter preferência para resolver Echo. Uma unidade de Golf/Delta só deveria “roubar” esse alvo se:
   - o dono de Echo não consegue atacar;
   - ou é kill garantido muito valioso;
   - ou o alvo ameaça morrer/fugir/ocupar algo crítico.

2. **Desentocar objetivo vence DPQ periférico**
   A prioridade tática global deveria ser:
   - inimigo sobre construção objetivo;
   - inimigo adjacente ao objetivo;
   - inimigo em rota/DPQ bom;
   - avanço simples.

3. **Ações que destravam aliados sobem na fila**
   Se mover Delta para a montanha melhora a situação para o ataque seguinte de Echo, Delta deve agir antes. Isso pode ser aproximado com:
   - “ataca inimigo perto do próprio objetivo”;
   - “sai de hex que bloqueia aliado”;
   - “ocupa DPQ que abre linha de fogo sem roubar alvo de outro setor”.

4. **Reservas táticas**
   Além de `plannedDestinations`, criar algo como:
   - `plannedAttackTargets`
   - `reservedObjectiveTargets`
   
   Assim uma unidade de Golf não gasta ação atacando o defensor de Echo se Echo já tem um capturador apto para atacar aquele alvo.

Eu faria isso em etapas, sem reescrever tudo:

1. Criar `AIController.TacticalScheduler.cs`.
2. Nele, para cada unidade disponível, calcular um `TacticalScore`.
3. O score decidiria a ordem de ação antes do `DecideUnitAction`.
4. Começar só com capturadores:
   - `+100000` se pode atacar inimigo no próprio objetivo.
   - `+80000` se pode capturar próprio objetivo agora.
   - `+60000` se pode atacar inimigo perto do próprio objetivo.
   - `+40000` se pode liberar/bloquear rota de aliado.
   - penalizar atacar alvo de outro setor quando o dono desse setor ainda pode agir.
   - desempatar por prioridade do objetivo, distância, DPQ e iniciativa.

O `Router` continuaria existindo, mas mudaria de papel: ele deixaria de ser “quem escolhe a peça” e viraria “como essa peça executa a intenção”. A escolha da próxima peça passaria para o `TacticalScheduler`.

Então a arquitetura ficaria:

```text
ObjectiveManager
  define plano geral: Echo, Delta, Golf...

TacticalScheduler
  escolhe qual unidade age agora e por quê

Router
  decide a melhor ação daquela unidade

Partials do Capturador
  executam papel específico: defender, perseguir, oportunista, etc.
```

Esse é o caminho para sair do “pikachu” e ir para um general tático.