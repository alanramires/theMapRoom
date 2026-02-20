# Sensor PodeMirar

Documento de referencia do sensor `PodeMirar`.

## Objetivo

O sensor `PodeMirar` identifica quais alvos o atacante pode engajar no estado atual do tabuleiro, incluindo:

- arma candidata
- distancia
- possibilidade de revide
- motivo de invalidez (quando nao pode mirar)

## Arquivos principais

- `Assets/Scripts/Sensors/PodeMirarSensor.cs`
- `Assets/Scripts/Sensors/PodeMirarTargetOption.cs`
- `Assets/Scripts/Sensors/PodeMirarInvalidOption.cs`

## Entrada

O sensor recebe, entre outros:

- unidade atacante
- tilemap/tabuleiro
- contexto de terreno/DPQ
- modo de movimento (`SensorMovementMode`)
- dados de prioridade de arma

## Saida

## Validos

Lista de `PodeMirarTargetOption`, contendo:

- `attackerUnit`
- `targetUnit`
- `weapon` e `embarkedWeaponIndex` do atacante
- `distance`
- dados de revide:
  - `defenderCanCounterAttack`
  - `defenderCounterWeapon`
  - `defenderCounterEmbarkedWeaponIndex`
  - `defenderCounterReason`

## Invalidos

Lista de `PodeMirarInvalidOption`, contendo:

- atacante/alvo
- arma analisada
- distancia
- `reason` (motivo do bloqueio)

## Regras avaliadas (alto nivel)

O sensor cruza:

- alcance minimo/maximo da arma
- dominio/altura compativeis (layer)
- LoS/LdT conforme configuracao
- municao disponivel
- existencia de alvo valido no range
- condicoes de revide do defensor

## Integracao com combate

O combate usa uma opcao valida do sensor como entrada.

Runtime:
- `TurnStateManager.Combat.cs` resolve combate a partir de `PodeMirarTargetOption`.

Ferramentas:
- `Tools/Combat/Calcular Combate`
- `Tools/Combat/Matriz de Combate`

Ambas usam o sensor para montar o par atacante/defensor e escolher arma/revide.

## Debug rapido

Se o par nao aparece como valido:

1. conferir alcance e categoria da arma
2. conferir dominio/altura de atacante, alvo e arma
3. conferir LoS/LdT no caminho
4. conferir municao embarcada
5. abrir lista de invalidos e ler `reason`
