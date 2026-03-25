# Após o Planning

Versao: v1.4.6  
Status: checkpoint pós-planning

## Resumo
- Concluída a implementação da arquitetura e execução automatizada de ordens de planejamento (Rally Points).
- Integração completa do sistema de Planning com o fluxo do turno e com o sistema de persistência (Save/Load).
- Ajustes visuais garantiram a exibição correta dos elementos gráficos de planejamento na camada de interface apropriada.

## Entregas principais

### 1) Sistema de Planning (Rally Points)
- Implementação do `PlanningManager` para gerenciar o ciclo de vida dos Rally Points (Criação, Seleção, Atribuição e Deleção).
- Adicionada a lógica para atribuir e desatribuir Unidades a um determinado Rally Point clicando no mapa.
- Capacidade de exibir múltiplas rotas ou destinações com feedbacks visuais (Flags pulsando e unidades designadas destacadas).
- Correção na layer de renderização ("SFX") para garantir que as bandeiras dos Rally Points apareçam sempre sob as camadas adequadas (sem conflitos com a layer Default).

### 2) Execução Automatizada no Início de Turno
- Introduzido o `ExecuteTurnStartRallyPhase` na inicialização do turno para processar os comandos de Rally.
- Unidades subordinadas a um Rally Point ativo são movidas automaticamente rumo ao destino, baseando-se no `UnitMovementPathRules` para tentar encontrar sempre o melhor trajeto disponível de acordo com os pontos de movimento remanescentes.
- Validações dinâmicas na execução bloqueiam ou removem a atribuição ("assignment") caso a rota seja bloqueada, o caminho exija entrar em combate, a unidade saia de campo ou atinja as proximidades diretas do Rally Point.

### 3) Persistência e Salve (Save/Load)
- Atualização do `SaveGameManager` e `SaveDataDtos` (`PlanningConfigSaveData`, `RallyPointSaveData`, `RallyAssignmentSaveData`) para exportar/importar as informações do sistema de Planning.
- Garantia de que após carregar um jogo as flags voltem à vida exibindo o Owner correto e as unidades mantenham suas atribuições.

### 4) UX de Planning e Feedbacks
- Adicionados tooltips e textos transitórios ao `PanelDialogController` informando ações de Planning (ex: "Define um hex destino antes de criar o rally", "Unidade XYZ atribuída ao rally").
- Controles de UI no painel de Planning interconectados com o fluxo de câmera e inputs protegidos para nãoconflitarem o estado neutro com replays e ações normais de jogo.

## Estado antes do próximo passo
- O sistema principal de Rally Points está estabilizado.
- Preparado terreno para novos features de inteligência artificial de jogo e avanços no gerenciamento global de turno.
