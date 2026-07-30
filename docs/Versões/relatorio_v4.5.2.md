# v4.5.2 — Refactor de Mudança de camada 2/5

## Objetivo

Consolidar a escada aérea básica, tornando `PodeDecolarSensor` e
`PodePousarSensor` as fontes autoritativas para as operações de decolagem e
pouso, inclusive durante serviços logísticos compostos.

## Pode Decolar autoritativo

- O sensor passou a exigir explicitamente uma aeronave compatível.
- A consulta pode avaliar o hex atual ou um hex fornecido pelo consumidor, sem
  mover a unidade.
- São validados combustível, camada atual, perfil aéreo, pista ou local de
  decolagem, procedimento disponível e ocupação da banda aérea de destino.
- O procedimento real determina a altura final: decolagens curtas de estrada ou
  porta-aviões terminam em `AirLow`, enquanto decolagens completas preservam a
  altura prevista pelo perfil da aeronave.
- Aeronaves já no ar, embarcadas ou fora de uma camada de superfície válida não
  entram no fluxo de decolagem.

## Pode Pousar autoritativo

- O sensor passou a exigir explicitamente uma aeronave em voo.
- Terreno, estrutura, construção, pista ou local de pouso são avaliados no hex
  efetivo da operação.
- A camada e a banda de destino são informadas no relatório do sensor.
- Ocupação, perfil aéreo e restrições operacionais continuam prevalecendo.
- O `TurnStateManager` passou a consumir o resultado do sensor ao oferecer a
  opção de pouso, sem duplicar a decisão.

## Pouso operacional e abastecimento

- Uma aeronave que estava operando no ar e ainda possuía combustível pode pousar
  para receber o serviço e tentar retornar ao voo antes de `Neutral`.
- A autorização de retorno usa o combustível anterior ao serviço; combustível
  recebido no batch não cria permissão retroativa.
- A decolagem final é revalidada pelo `PodeDecolarSensor`, incluindo procedimento,
  camada de destino e ocupação.
- O mesmo contrato foi aplicado ao suprimento logístico e ao serviço de comando.

## Pouso forçado aguardando reabastecimento

- Criada no `UnitManager` a flag runtime
  `AircraftForcedLandingAwaitingRefuel`.
- O pouso de emergência por falta de combustível, processado no início do turno,
  liga a flag.
- A flag permanece ligada até ocorrer ganho real de combustível.
- Enquanto a flag estiver ligada, `PodeDecolarSensor` bloqueia a decolagem.
- O reabastecimento limpa a flag, mas não decola automaticamente a aeronave no
  mesmo batch.
- Depois do serviço, o jogador pode selecionar a aeronave e tentar decolar,
  sujeita novamente a todas as regras de `PodeDecolarSensor`.
- A flag foi incluída no save/load e no espelho runtime do Inspector.

## Ferramentas

- As ferramentas de `Pode Decolar` e `Pode Pousar` apresentam o hex, o
  procedimento e a camada resultante informados pelos sensores.
- As janelas não aplicam transições nem alteram o estado confirmado do tabuleiro.

## Arquitetura transacional

- As consultas dos sensores permanecem puras.
- O combustível anterior ao serviço é capturado no plano da operação, antes da
  aplicação dos suprimentos.
- Nenhum ganho posterior reescreve retroativamente a autorização original.
- As mudanças definitivas continuam ocorrendo apenas no fluxo comprometido e o
  estado confirmado é reconciliado no retorno a `CursorState.Neutral`.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- Resultado: builds concluídos com 0 erros.
- Implementação atual do refactor: `2/5`.
