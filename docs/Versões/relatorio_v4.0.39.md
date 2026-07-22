# v4.0.39 - Reparos gerais em save e load, ajustes na AI hard com conscript

Esta versao consolida correcoes de restauracao de partidas e amplia o comportamento da AI Hard com recrutamento emergencial, controle de massa e novos criterios de compra e operacao.

## Save e load

- Construcoes do mapa passam a ser reutilizadas pelo `InstanceId` durante o load, preservando referencias de planejamento, badges e estado visual.
- Construcoes ausentes no mapa ainda podem ser recriadas; instancias que nao pertencem ao snapshot sao removidas.
- O limite de captura volta a usar o `ConstructionData` como fonte de verdade, sem perpetuar configuracoes antigas gravadas no save.
- Saves antigos com limites de captura diferentes migram o progresso proporcionalmente.
- Ocupacao de construcoes e HUD sao recalculados depois da restauracao definitiva das unidades, planejamento e Fog of War.
- O comando de debug `set fow off` tambem atualiza os visuais de ocupacao das construcoes.
- Continuidade de partidas AI vs AI foi corrigida para retomar o ciclo da IA depois do load.
- Replay e load normal compartilham a restauracao autoritativa das configuracoes de construcao.

## AI Hard e conscript

- Dificuldades da IA foram reorganizadas entre iniciante, facil, medio, formigueiro, competitiva e agressiva.
- Conscricao emergencial ao perder foi separada da doutrina permanente de enxame.
- Estado e configuracao de conscricao agora persistem corretamente no save.
- Nova Fase de Massacre interrompe recrutamento excessivo quando a IA ja possui vantagem ou se aproxima do limite de unidades.
- Histerese evita alternancia instavel entre conscript e compras de elite.
- Shopping, demanda, defesa, transporte, reparo e selecao de unidades receberam ajustes para considerar pressao operacional e o modo Hard.
- Planejamento aeronautico e operacoes de reparo receberam refinamentos de avaliacao e movimento.

## Interface e ferramentas

- Painel de turno ganhou estatisticas territoriais e contagem de tropas por slot.
- Barras territoriais usam as cores das equipes e sentidos opostos de preenchimento.
- Glossario dos termos internos da IA foi adicionado em `docs/ai_termos.md`.
- Comandos e menus de debug foram ampliados para as novas dificuldades e estados da IA.

## Validacao

- `git diff --check`: sem erros de whitespace, apenas avisos de normalizacao LF/CRLF.
- A compilacao encontrou uma incompatibilidade de assinatura preexistente em `AIController.Repair.cs`, na chamada de `FindRepairConstruction`.
