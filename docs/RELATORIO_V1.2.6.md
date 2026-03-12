# v1.2.6 - Fog of War

## Resumo
- Implementado o sistema de **Fog of War** para partidas com `Total War = true`.
- Separada a logica de **iluminacao do terreno** da logica de **visibilidade real de unidades**.
- Adicionado controle de debug `FoW on/off` no `panel_debug`.

## Principais entregas
- Novo bloco de FoW no `MatchController` com pintura de overlay no tilemap `FogOfWar`.
- Recalculo de FoW por troca de turno e por eventos de unidade (`HasAct = true`).
- Cache incremental inspirado no padrao da Hotzone para reduzir recomputo.
- Construcoes aliadas iluminam o proprio hex (visao 0).
- Unidade inimiga so aparece se:
  - o hex estiver iluminado; e
  - houver observacao/deteccao valida por regras de camada, LOS/spotter e stealth.
- Unidade oculta por FoW:
  - some visualmente (sprite e HUD),
  - nao pode ser selecionada/inspecionada,
  - nao entra como alvo em `Pode Mirar`.

## Correcao importante de vazamento de informacao
- Corrigido o caso em que o FoW revelava "buracos" por conta da camada do ocupante escondido.
- A iluminacao do FoW passou a usar camada do **terreno** (nao do ocupante oculto), evitando denunciar presenca em hex.

## Debug
- Comando novo no `DebugManager`:
  - `FoW off`: limpa nevoa e revela todas as unidades.
  - `FoW on`: restaura comportamento normal do FoW.

## Observacoes
- O sistema atual cobre o ciclo base de visibilidade e ocultacao por turno/acao.
- Regras futuras (efeitos especiais, sensores dedicados por habilidade, novos estados temporarios de revelacao) ficam para iteracoes seguintes sob demanda.
