# Relatorio de Atualizacao - v1.4.13

## Em uma frase
A versao v1.4.13 corrigiu o Servico do Comando: ordenacao transporter-antes-de-filhos, reset de embarcados no inicio do turno, sprite de transporte em unidades aninhadas, e invisibilidade de sprites durante a animacao de suprimento.

## O que isso trouxe na pratica
- Porta-avioes e navios transporte agora sao atendidos antes dos seus passageiros, em profundidade pre-ordem.
- Unidades embarcadas (bombardeiros no PA, soldados no APC) voltam com HasActed e ReceivedSuppliesThisTurn zerados no inicio do turno.
- Transportadores aninhados exibem o sprite correto de transporte ao serem revelados durante a animacao.
- Sprites e HUDs de passageiros embarcados nao somem mais durante a animacao de suprimento (conflito com FoW).

## Principais melhorias

1. Ordenacao pre-ordem no Servico do Comando
- `ServicoDoComandoSensor` e `TurnStateManager.CommandService` substituiram a reordenacao flat por uma travessia pre-ordem recursiva.
- Transporter e emitido primeiro, depois cada passageiro por ordem de assento; se o passageiro e ele mesmo transporter, desce antes de continuar.
- Resultado percebido: PA recebe suprimento antes do bombardeiro que carrega; navio recebe antes do APC que carrega.

2. Reset de turno para unidades embarcadas (Bug-A)
- `MatchController` identificava unidades ativas com `GetActiveUnitsOnScene()`, que excluia embarcados (`IsEmbarked`).
- Adicionado loop sobre `TransportedUnitSlots` apos reset de cada unidade para propagar `ResetActed()` e `ClearReceivedSuppliesThisTurn()` aos passageiros do mesmo time.
- Resultado percebido: bombardeiro embarcado no PA pode agir normalmente apos mudanca de turno.

3. Sprite de transporte em nested transporters
- `HasAnyEmbarkedPassenger()` em `UnitManager` tinha guard `|| isEmbarked` que retornava `false` para qualquer unidade ela mesma embarcada.
- Removido o guard: APC embarcado num navio agora avalia corretamente seus proprios passageiros e exibe `spriteTransport` quando carregado.
- Resultado percebido: APC com soldados dentro mostra sprite de transporte ao aparecer sobre o navio durante animacao.

4. Visibilidade de sprite durante animacao de suprimento
- `hiddenByFogOfWar = true` e setado em embarcados no inicio do turno (nao estao no mapa ativo).
- `SetSpriteVisible`, `SetHudVisible` e `ApplyFogOfWarVisibility` forcavam invisibilidade mesmo com `IsEmbarkedVisualPreviewActive`.
- Adicionada excecao: FoW nao sobrescreve visibilidade quando `isEmbarked && IsEmbarkedVisualPreviewActive`.
- Resultado percebido: sprites e HUDs dos passageiros permanecem visiveis durante toda a animacao de preenchimento de HP/AUT.

5. Sensor PodeFundir - correcao de ordem e tipo
- Verificacao `IsSameTypeAndTeam` passou a anteceder `HasPassengers` no loop de candidatos.
- Corrige falso positivo onde transportador de tipo diferente aparecia como candidato invalido de fusao, causando retorno `true` no sensor para um bombardeiro sem candidatos reais.

6. Sensor PodeEmergir (novo)
- Criado `PodeEmergirSensor` para isolar a logica de emersao voluntaria de submarinos (Sub/Submerged -> Naval/Surface).
- Usado em `TurnStateManager.ScannerPrompt`, `TurnStateManager.SupplyQueue` e `TurnStateManager.CommandService` no lugar do flag `forceSurfaceBeforeSupply` inline.

## Bloco tecnico curto
- Scripts principais alterados:
  - `Assets/Scripts/Sensors/PodeFundirSensor.cs`
  - `Assets/Scripts/Sensors/PodeEmergirSensor.cs` (novo)
  - `Assets/Scripts/Sensors/SensorLogGate.cs`
  - `Assets/Scripts/Sensors/ServicoDoComandoSensor.cs`
  - `Assets/Scripts/Units/UnitManager.cs`
  - `Assets/Scripts/Match/MatchController.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.CommandService.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.SupplyQueue.cs`
  - `Assets/Scripts/Match/TurnState/TurnStateManager.ScannerPrompt.cs`
- Documentacao:
  - `docs/relatorio_v1.4.13.md`

## Resultado
A v1.4.13 fecha um conjunto de bugs interligados no Servico do Comando: a ordem de atendimento agora respeita a hierarquia de transporte, embarcados voltam ao estado correto a cada turno, e a apresentacao visual durante a animacao e fiel ao estado real das unidades.
