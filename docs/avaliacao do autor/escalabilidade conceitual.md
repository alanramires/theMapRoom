# Escalabilidade conceitual

## 1) O design parece preparado para expansao de mecanicas?
Sim, com boa base para expansao controlada.

O sistema foi estruturado para crescer em conteudo e regra sem exigir reescrita total do fluxo principal:

- Novas unidades:
  - entram por `UnitData` + `UnitDatabase`, sem alterar o loop de turno.
  - atributos, camadas, skills, armas e logistica sao declarativos.

- Novas regras de combate/logistica:
  - podem ser acopladas em sensores/resolvers (`Pode*Sensor`, `CombatModifierResolver`, `Service*Formula`) mantendo `TurnStateManager` como orquestrador.

- Novos terrenos/estruturas/construcoes:
  - entram via `TerrainTypeData`, `StructureData`, `ConstructionData` e seus bancos, com impacto em movimento, visao e operacoes por composicao de regra.

- Novos fluxos de acao:
  - a state machine do `TurnStateManager` ja suporta extensao por novo `CursorState` + handlers.

Limite atual: o sistema suporta bem expansao de mecanicas dentro do paradigma existente (jogo tatico por camadas). Mudancas de paradigma (ex.: simultaneo em tempo real) exigiriam revisao maior do orquestrador.

## 2) Quais abstracoes indicam planejamento de longo prazo?
As abstrações mais fortes sao:

- Separacao Data / Runtime / Regra:
  - `*Data` (configuracao), `*Manager` (estado em campo), `*Rules`/`*Resolver`/`*Sensor` (logica).
  - reduz acoplamento e facilita extensao incremental.

- Modelo de camadas operacionais (`Domain` + `HeightLevel`):
  - usado transversalmente em movimento, ocupacao, combate, visao, pouso/decolagem.
  - indica arquitetura pensada para variacao de contexto tatico.

- Orquestracao em dois niveis:
  - `MatchController` (turno macro) e `TurnStateManager` (fluxo micro da unidade).
  - facilita adicionar mecanicas sem misturar regras de partida com regras de acao.

- Sensores como API de elegibilidade:
  - familia `Pode*` padroniza "acoes validas + invalidas + motivo".
  - favorece crescimento de mecânicas com explicabilidade para UI/UX.

- Catalogos e lookup por ID:
  - `UnitDatabase`, `ConstructionDatabase`, `WeaponDatabase`, `DPQDatabase`, `RPSDatabase`.
  - base para escalar conteudo sem acoplamento com objetos de cena especificos.

- Cache e invalidacao por revisao:
  - `ThreatRevisionTracker` e caches de overlay.
  - mostra preocupacao com crescimento de custo computacional.

- Compatibilidade evolutiva:
  - presenca de `FormerlySerializedAs` e campos legados indica estrategia de migracao de dados sem quebrar assets antigos.

## Conclusao
O projeto demonstra escalabilidade conceitual real: ele foi modelado para ampliar conteudo e regras mantendo o nucleo de fluxo. As abstrações existentes sugerem planejamento de longo prazo, principalmente para evolucao incremental de mecanicas taticas.
