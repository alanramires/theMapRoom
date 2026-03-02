# The Map Room v1

## Visao geral (atualizado ate v1.1.1)

The Map Room e um jogo tatico por turnos em grade hexagonal, com combate orientado a dados e foco em legibilidade de simulacao.

No estado atual, o v1 ja cobre:

- combate previsivel com rastreabilidade de calculo (RPS + Elite + DPQ + revide)
- sensores taticos com retorno de valido/invalido e motivo
- operacoes de mobilidade (embarque e operacao aerea) por contexto de mapa
- economia de construcoes com regras de producao/venda por ownership
- save/load de partida em runtime (incluindo quick save/load)
- base de dados e ferramentas de editor para tuning rapido

## Loop principal (estado atual)

1. Selecionar unidade e movimentar.
2. Rodar sensores taticos (ex.: `PodeMirar`, `PodeEmbarcar`, operacao aerea).
3. Escolher acao (combater, embarcar, pousar/decolar, capturar, comprar ou apenas mover).
4. Resolver acao com regras de estado de turno (`TurnState`) e validacoes de contexto.
5. Aplicar resultados no runtime (dano, ammo/fuel/HP, ownership, recursos) e encerrar acao.
6. Opcional: salvar/carregar para continuidade de partidas longas (`F8`/`F9` no fluxo rapido).

## Sistemas implementados

## 1) Sensores de acao taticos

Arquivos principais:

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`
- `Assets/Editor/PodeEmbarcarSensorDebugWindow.cs`

Capacidades:

- validacao de alcance, dominio/altura, arma e contexto de acao
- lista de opcoes validas e invalidas com motivo detalhado
- validacao de revide no combate
- validacao de embarque por terreno/construcao, slots e restricoes de unidade

## 2) LoS e LdT por terreno/camada

Referencias:

- `docs/regras de LoS e LdT.md`
- `Assets/Scripts/Terrain/TerrainVisionResolver.cs`
- `Assets/Scripts/Sensors/PodeMirarSensor.cs`

Estado atual:

- `Straight`: bloqueio por percurso e EV/BlockLoS
- `Parabolic`: validacao de destino com checks basicos
- override de ar por `DPQAirHeightConfig` (`AirLow`/`AirHigh`)

## 3) Combate em runtime

Arquivo central:

- `Assets/Scripts/Match/TurnState/TurnStateManager.Combat.cs`

Pontos chave:

- consumo de municao de atacante e revide
- RPS por classe/categoria de arma
- modificadores de elite por skill
- defesa efetiva e arredondamento de dano por DPQ
- trace de combate no log para calibracao

## 4) RPS e Elite por dados

Arquivos:

- `Assets/Scripts/RPS/RPSData.cs`
- `Assets/Scripts/RPS/RPSDatabase.cs`
- `Assets/Scripts/Skills/SkillData.cs`
- `Assets/Scripts/Units/UnitData.cs`

Caracteristicas:

- match exato por chave de classe e categoria
- fallback sem match com bonus `+0`
- `eliteLevel` por unidade e regras condicionais de modificador

## 5) Operacoes aereas (`Landing/Takeoff`)

Arquivos:

- `Assets/Scripts/Units/Rules/AircraftOperationRules.cs`
- `Assets/Scripts/Construction/ConstructionData.cs`
- `Assets/Scripts/Structures/StructureData.cs`
- `Assets/Scripts/Terrain/TerrainTypeData.cs`

Caracteristicas:

- pouso/decolagem definidos por contexto do hex (construcao/estrutura/terreno)
- regras por classe aerea e skills requeridas
- suporte a variacoes futuras sem hardcode acoplado em `UnitData`

## 6) Construcoes, captura e regras de mercado

Arquivos:

- `Assets/Scripts/Construction/ConstructionSiteRuntime.cs`
- `Assets/Scripts/Construction/ConstructionManager.cs`

Regras de venda/producao suportadas:

- `FreeMarket`
- `OriginalOwner`
- `FirstOwner`
- `Disabled`

Estado atual:

- ownership e venda/producao respondem ao estado da partida
- suporte a configuracoes por instancia (incluindo overrides)
- captura e economia integradas ao fluxo do `TurnState`

## 7) Save/Load de partida (v1.1.1)

Arquivo-chave:

- `Assets/Scripts/Save/SaveGameManager.cs`

Capacidades:

- save por slot em JSON
- restauracao de turno, time ativo, unidades, construcoes e ownership
- restauracao de estado de embarque, ammo/fuel/HP e runtime de ofertas/servicos/suprimentos
- quick save/load com atalhos (`F8` salva, `F9` carrega)

## 8) Fluxo de turno e robustez de runtime

Arquivos impactados:

- `TurnStateManager.*`
- `MatchController`
- `ConstructionManager`
- `UnitManager`

Melhorias recentes:

- menos chance de travar em estado intermediario apos acao/animacao
- compras em construcao com validacoes e atalhos mais robustos
- melhor estabilidade para combate, captura, sensores e shopping

## 9) Ferramentas de editor para calibracao

Menu:

- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`

Arquivos:

- `Assets/Editor/CombatCalculatorWindow.cs`
- `Assets/Editor/CombatMatrixWindow.cs`

Estado atual:

- alinhadas ao runtime de combate
- suporte a analise par-a-par e matriz maior de balanceamento
- reducao de retrabalho no tuning de dados

## 10) Organizacao por mapa e catalogos (v1.1.0+)

Estado atual:

- dados de mapa menos acoplados entre cenas
- estruturas/rotas e field de construcoes centralizados nas bases corretas
- suporte mais limpo para multiplos mapas sem misturar catalogos

## Ordem oficial de altitude (HeightLevel)

Referencia de codigo: `Assets/Scripts/Units/DomainManager.cs`

- `Submerged = 2`
- `Surface = 3` (Land/Naval)
- `AirLow = 4`
- `AirHigh = 5`

Regra de projeto:

- manter essa ordem numerica como base para comparacoes de camada/altura
- evitar alterar esses valores sem migracao explicita dos sistemas dependentes

## Diferencial do v1

O v1 ja tem um nucleo tatico funcional, observavel e iteravel:

- log detalhado para diagnostico de balanceamento
- simuladores e janelas de editor alinhados com runtime
- sensores com justificativa de invalidacao
- estrutura de dados pronta para crescer em faccoes, classes, counters e mobilidade
- continuidade de teste/play com save/load integrado

## Limites atuais (conhecidos)

- LoS/LdT ainda difere entre trajetorias (`Straight` vs `Parabolic`)
- balanceamento segue em tuning iterativo de assets
- parte do runtime logistico ainda depende de integracoes pendentes
- cena de teste e catalogos continuam em evolucao frequente

## Documentos de apoio

- `docs/Combat.md`
- `docs/Elite.md`
- `docs/regras de LoS e LdT.md`
- `docs/RELATORIO_V1.1.0.md`
- `docs/RELATORIO_V1.1.1.md`
