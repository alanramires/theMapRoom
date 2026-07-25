# v4.4.0 — AI Transporte Naval

## Objetivo

Consolidar o transporte naval da IA como um courier de passageiros, usando os
mesmos contratos operacionais para IA normal e IA rebelde, sem substituir
alcançabilidade por distância geométrica.

## Transporte naval

- Navio vazio procura passageiros e assume comportamento de pickup.
- O ponto de encontro é validado pelo `PodeEmbarcarSensor` e pelas regras
  `Allow Embark When Transporter At` do `UnitData`.
- A praia de rendezvous precisa ser compatível com os caminhos válidos do
  passageiro, incluindo embarque estendido a partir de um stop adjacente.
- A progressão até a praia usa `Caminhos Válidos` com
  `ToolProgressionIntent.TransportRendezvous`.
- Removido do pickup naval o fallback de aproximação geométrica direta.
- O navio carregado resolve o destino a partir do passageiro e utiliza a
  progressão de transporte até uma cabeça de praia elegível.
- O desembarque é seletivo: passageiros cujo destino ainda está longe
  permanecem embarcados.

## Passageiros e fila de entrega

- Navio e Chinook podem receber um passageiro oportunista na vaga adicional,
  mesmo quando seu objetivo é diferente do passageiro formal.
- O transporte entrega um passageiro por vez e promove automaticamente o
  passageiro restante após o desembarque do anterior.
- Cada `UnitTransportSeatRuntime` registra `embarkedOnTurn`.
- A prioridade do courier é FIFO por turno de embarque.
- Em empate de turno, vence a menor vaga física.
- O turno é gravado no compromisso do embarque, limpo no desembarque e
  preservado quando as vagas runtime são reconstruídas.
- `embarkedOnTurn` foi incluído no save/load e exibido no Inspector do
  `UnitManager`.

## IA rebelde

- O `MatchController` passou a expor `Is Rebel (runtime)` por `SlotID`.
- O estado rebelde é derivado da presença de QG no tabuleiro e aparece como
  somente leitura no Inspector.
- Slots rebeldes não executam `BuildObjectivePlan`, não criam eixos e não usam
  prioridades estratégicas de setor.
- Capturadores rebeldes distribuem objetivos por proximidade, reservando o
  prédio escolhido para que a unidade seguinte avance para a próxima bolha.
- Embarque, courier e transporte naval continuam compartilhados com a IA
  normal.

## Combate e apresentação

- Ajustes no roteamento de ações de combate da IA para infantaria, assalto,
  defesa antiaérea e unidades aéreas.
- Ajustes de apresentação e privacidade no `PanelRodadaController`.
- Atualizações do mapa de calibração, assets de unidade, fontes e arte
  realizadas durante os testes desta versão.

## Persistência e arquitetura

- Metadados da fila de passageiros persistem no save/load.
- Ações de embarque e desembarque continuam respeitando o contrato
  transacional: o estado runtime definitivo é alterado somente no compromisso
  do batch.
- Seleção de destino pode usar distância para ranking, mas alcançabilidade e
  movimento são validados pelos serviços de caminhos válidos e progressão.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- Resultado: build concluído sem erros.
