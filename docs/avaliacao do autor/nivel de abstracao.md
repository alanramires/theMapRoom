# Nivel de abstracao

## Classificacao (escala 1-5)
**4 — arquitetura de sistema**  
Com elementos fortes de **3 — modelagem de dominio** e alguns traços parciais de **5 — motor de simulacao**.

## Por que 4?
O projeto vai alem de OO basico e organiza o jogo em camadas arquiteturais claras:

- **Dados declarativos**: `ScriptableObject` (`UnitData`, `ConstructionData`, `ServiceData`, `DPQData`, `RPSData`) + bancos por ID.
- **Regras de dominio isoladas**: `*Rules`, `*Resolver`, `*Sensor` para movimento, combate, visao, logistica, operacoes aereas.
- **Orquestracao de fluxo**: `MatchController` (turno macro) + `TurnStateManager` (estado micro da unidade).
- **Interface desacoplada da regra**: `PanelHelperController`/HUD consomem resultados e motivos do dominio.

Esse desenho e tipico de arquitetura de sistema: separa responsabilidade, controla complexidade e permite evolucao incremental.

## Por que nao e 5 completo (motor/engine)?
Apesar de ter sinais de engine tatico (sensores, state machine, cache por revisao, regras compostas), ainda esta fortemente acoplado ao contexto especifico do jogo e ao runtime Unity:

- nao ha uma camada totalmente generica/reutilizavel de simulacao independente do jogo;
- boa parte da orquestracao e centrada em comportamentos concretos do produto atual.

Ou seja: e um sistema arquitetado e robusto, mas ainda **produto-especifico**, nao um motor geral.

## Resumo
Se fosse reduzir a uma nota unica: **4/5**.  
Se fosse por perfil tecnico: **arquitetura de sistema com modelagem de dominio madura**.
