# v2.3.2 - AI Rally Point

## Objetivo

Consolidar a primeira versao jogavel do comportamento de Rally Point da IA: setores de concentracao deixam de competir diretamente com a progressao natural de captura, mas passam a orientar reuniao, suporte e preparacao antes de ataques maiores.

## Principais mudancas

- Construction Manager recebeu novos marcadores taticos para mapa:
  - Rally Point com multiplos target slots.
  - Anchor Sector e Anchor Sector Slot.
  - Forward Observer Spot persistido em save/load.
  - Visibilidade runtime por `isVisible`.
- Sector Manager e planner passaram a considerar rally/anchor na leitura estrategica.
- Anchors ganharam prioridade em fases de recuperacao/expansao inicial, evitando que a IA abandone setores-base importantes para perseguir objetivos longinquos.
- Rally Points passaram a servir como pontos de montagem para assalto e fogo indireto, sem substituir a progressao natural por setores vizinhos.

## Planejamento de IA

- O planner agora protege capturadores em setores defensivos locais antes de joga-los no solver global.
- A alocacao de capturadores recebeu custo de continuidade de setor:
  - mesmo setor e vizinhos ficam mais atrativos;
  - sucessor natural/campanha recebe preferencia;
  - saltos para setores mais profundos nao adjacentes ficam penalizados.
- Em `Collapsing`, plano de invasao final/base inimiga fica desencorajado para priorizar recuperacao de anchors.
- O plano passa a manter melhor coerencia entre posicao atual da unidade e setor sugerido.

## Forward Observer

- Capturadores exploradores agora procuram `Forward Observer Spot` para revelar objetivos ocultos por FoW.
- O uso de observador avancado foi limitado para nao roubar prioridade de combate:
  - se ha inimigo visivel e atacavel perto do objetivo, o capturador deixa o scoring normal escolher `move+attack`;
  - se o alvo esta oculto, o comportamento de observador/DPQ continua ativo.
- `Forward Observer Spot` passou a ser salvo e restaurado.

## Compras e Load

- A Fase 3 de compras agora marca o stage como comprometido apos decidir a lista de compras, evitando recalcular compras ao carregar um save feito durante a fase.
- O fim do turno da IA preserva `AIStage = 4` em vez de zerar para 0.
- A retomada por save/load so usa o stage salvo se for o mesmo time e o mesmo numero de turno; em novo turno a IA comeca do stage 0 normalmente.

## Resultado esperado

- IA prioriza anchors quando precisa se recompor.
- Rally Points funcionam como base de concentracao sem sequestrar a captura natural.
- Capturadores tendem a continuar por setores vizinhos em vez de saltar para objetivos distantes.
- Observadores avancados ajudam a revelar FoW quando faz sentido, mas nao substituem tiro disponivel.
- Load game no inicio/fim de turno preserva melhor a foto do estado da IA.
