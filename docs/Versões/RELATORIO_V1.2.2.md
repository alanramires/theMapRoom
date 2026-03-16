# Relatorio de Atualizacao - v1.2.2

## Em uma frase
A v1.2.2 consolida as regras de autonomia aerea, corrige excecoes de upkeep em construcoes e melhora a leitura de coordenadas/eventos no fluxo de turno.

## O que isso trouxe na pratica
- Aeronaves em construcao com isencao (ex.: aeroporto) nao perdem fuel no upkeep de inicio de turno, inclusive em sobrevoo.
- A leitura de upkeep/isencao para aeronaves considera tambem o contexto de sobrevoo da celula (terreno/estrutura), nao apenas estado pousado.
- Aeronaves embarcadas continuam sem consumo de upkeep de autonomia no inicio do turno.
- Construcoes operacionais (cidade, barracks, fabrica, hq, porto) passaram a explicitar `aircraftUnitsPaysUpkeep: 1` para evitar ambiguidades de serializacao.
- Helper de autonomia exibe historico curto e direto: `Fuel A >- X > B`.
- Overlay de coordenadas contextual no mapa com toggle (`F3`) para facilitar localizacao rapida.

## Principais melhorias
1. Regra de upkeep aereo alinhada ao ConstructionData
- Resolucao defensiva do `ConstructionData` por `constructionId` no runtime para manter a regra orientada a classe.
- Eliminacao de dependencia de estado mutable da instancia para decidir isencao de upkeep.

2. Isencao de autonomia em construcao aplicada tambem em voo
- A excecao de upkeep para `aircraftUnitsPaysUpkeep = false` passou a valer para aeronave na celula da construcao, nao apenas pousada.
- Comportamento esperado para sobrevoo de aeroporto agora consistente com a regra de design.
- Em termos de regra operacional: o contexto da celula (terreno/estrutura embaixo da aeronave) participa da decisao de cobrar ou isentar upkeep no inicio do turno.
- Para aeroporto (isento), a regra e independente da camada da aeronave na celula: `AirHigh`, `AirLow` ou `Landed`.

3. Dados de construcao normalizados
- Campos `aircraftUnitsPaysUpkeep` explicitados nos assets de construcao principais para reduzir risco de default implicito.
- `Aeroporto` mantido como `0` (isento), demais contextos definidos como `1` (cobra upkeep).

4. Leitura operacional no helper de turno
- Linhas de consumo de autonomia encurtadas para leitura imediata no painel.
- Exibicao de aeronaves ajustada para incluir casos relevantes de operacao aerea.

5. Coordenadas contextuais no mapa
- Overlay leve com labels de cursor/unidade/evento.
- Hotkey `F3` para ligar/desligar sem poluicao permanente da tela.
- Largura do label do overlay parametrizada no `CursorController` via Inspector.

6. Hotkey de display
- Toggle de fullscreen em runtime via `F11` com feedback textual de estado.

## Regras importantes
- A decisao de upkeep de aeronave deve usar dados de `ConstructionData`.
- Isencao em construcoes com `aircraftUnitsPaysUpkeep = false` vale para aeronave na celula mesmo em sobrevoo.
- A validacao nao fica restrita ao estado `grounded`: a celula sobrevoada (terreno/estrutura) tambem entra na regra de upkeep.
- Unidade `IsEmbarked` permanece com upkeep de autonomia zero no inicio do turno.
- Construcoes que devem cobrar upkeep precisam manter o campo explicitamente definido nos assets.

## Bloco tecnico curto
- Scripts-chave: `OperationalAutonomyRules`, `MatchController`, `TurnStateManager.HelperPanel`, `PanelHelperController`, `CursorController`, `PanelVisibilityHotkeysController`.
- Assets-chave: `Assets/DB/World Building/Construction/*`.

## Resultado
A v1.2.2 fecha um ciclo importante de consistencia aerea: regra correta no upkeep, dados mais robustos em asset, e melhor capacidade de leitura/diagnostico durante a partida.
