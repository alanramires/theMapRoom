# Relatorio de Atualizacao - v1.5.1

## Em uma frase
A versao v1.5.1 avanca a IA para um turno mais completo (comando, movimentacao, ataque e compras), introduz perfil de compra configuravel por asset e consolida melhorias de automacao, editor e fluxo de partida.

## O que isso trouxe na pratica
- A IA agora executa ciclo mais robusto por unidade, com selecao de alvo, deslocamento por distancia hex e tentativa de ataque automatizado.
- O comportamento de compra da IA ficou configuravel por perfil (ataque/defesa), com grupos, porcentagens e fallback de unidades.
- O turno automatizado ganhou funcoes utilitarias para navegar cursor, escolher celula alcancavel e acionar acoes com feedback.
- O setup de partida e os editores foram reforcados para facilitar configuracao de IA e manutencao no Inspector.

## Principais entregas

### 1. IA de turno com execucao completa
- `AIPlayerController` foi expandido para orquestrar fases de comando, movimentacao por unidade e compras no fim do turno.
- A IA reavalia snapshot durante a execucao para decidir com estado atualizado (unidades aliadas, inimigos visiveis, HQ e construcoes conhecidas).
- A atribuicao de alvos evita concentracao excessiva no mesmo inimigo e respeita modo de defesa por raio do HQ.
- Em modo defesa, a IA prioriza protecao do HQ e controle de area proxima; em ataque, pressiona alvos e objetivos avancados.

### 2. Perfil de compras configuravel por asset
- Novo `AIShoppingProfile` (`ScriptableObject`) com modos de ataque e defesa.
- Cada modo suporta composicao por grupos com prioridade, meta percentual e lista de unidades especificas.
- Fallback de compra e opcao de economizar para proxima rodada foram incorporados na tomada de decisao.
- Foi adicionado asset base em `Assets/DB/AI/AI Basic.asset` para uso imediato e customizacao.

### 3. Automacao de turno e shopping para IA
- `TurnStateManager.Automation` recebeu utilitarios para selecao automatizada de unidade, movimento, ataque e sincronizacao de estado neutro.
- `TurnStateManager.ConstructionShopping` foi ampliado com APIs para IA selecionar unidade e executar compra direta com validacoes de limite e saldo.
- A IA passa a usar melhor avaliacao de celulas alcancaveis (distancia hex + DPQ), incluindo banda de distancia para unidades de maior alcance.

### 4. Ferramentas de editor e fluxo de configuracao
- Novo `AIPlayerControllerEditor` com suporte para criar, duplicar, abrir e editar inline o perfil de compras.
- Atualizacoes em editores e telas de setup mantem a configuracao de jogadores IA/Humano/Off mais consistente.
- Ajustes de persistencia em `PartidaConfig`, `SaveData` e `SaveGameManager` reforcam continuidade entre cena de setup e batalha.

## Bloco tecnico
- Scripts novos:
  - `Assets/Scripts/AI/AIShoppingProfile.cs`
  - `Assets/Editor/AIPlayerControllerEditor.cs`
  - `Assets/Editor/BattleMapMenuRootControllerEditor.cs`
- Assets novos:
  - `Assets/DB/AI/AI Basic.asset`
  - `Assets/Prefab/Panel_dialog.prefab`
  - `Assets/Scenes/Em dev/AI Ground.unity`
- Scripts modificados (principais):
  - `Assets/Scripts/AI/AIPlayerController.cs`
  - `Assets/Scripts/AI/AISnapshot.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.Automation.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.ConstructionShopping.cs`
  - `Assets/Scripts/Match/MatchController.cs`
  - `Assets/Scripts/Match/PartidaConfig.cs`
  - `Assets/Scripts/UI/NewGamePanelController.cs`
  - `Assets/Scripts/Shared/SaveData/SaveDataDtos.cs`
  - `Assets/Scripts/Shared/SaveData/SaveDataMapper.cs`

## Pendencias conhecidas (proxima versao)
- Evoluir a IA para estrategia de medio/longo prazo (captura de construcoes, priorizacao economica por contexto de mapa e fase da partida).
- Expandir variedade de perfis de compra e validar calibracao por faccao/mapa.
- Cobrir com testes de regressao os fluxos de automacao e shopping IA para reduzir risco em mudancas futuras.

## Resultado
A v1.5.1 transforma a IA de fundacao em comportamento jogavel mais completo, com compra configuravel, automacao mais robusta e pipeline de configuracao mais pratico para iterar rapidamente nas proximas versoes.
