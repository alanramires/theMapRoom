# v4.5.7 — Serviço de Transporte

## Objetivo

Criar um checkpoint seguro para a evolução do transporte em um serviço comum de
operações. Esta versão consolida correções preparatórias em desembarque, EVAC e
pickup naval antes da extração completa do `TransportOperationsService`.

O checkpoint permite retornar a um estado funcional caso a migração posterior de
APC, supridor, trem, Chinook, hidroavião, porta-aviões e navio de desembarque
introduza regressões.

## Desembarque parcial rebelde e rogue

- Uma entrega tática válida não é mais substituída automaticamente por progressão
  estratégica apenas porque a LZ não comporta todo o grupo.
- Quando a melhor LZ entrega somente parte das cargas, a IA avalia as opções
  físicas de desembarque do passageiro restante.
- A progressão terrestre operacional é calculada por dois turnos a partir das
  células oferecidas por `PodeDesembarcar`.
- Se o passageiro restante não possui outro prédio capturável nesse envelope, o
  passageiro prioritário desembarca imediatamente.
- O passageiro restante permanece embarcado.
- Se existir outro prédio operacionalmente alcançável, a IA preserva a entrega
  conjunta.

## EVAC orientado pelo UnitData

Foi removida a exclusão genérica que impedia passageiros de `Domain.Air` de
participarem do EVAC.

A compatibilidade passa a ser determinada pelos `transportSlots` configurados no
`UnitData` do transportador. A consulta existente valida:

- classe e domínio aceitos pelo slot;
- skills;
- camada;
- vaga;
- capacidade;
- exclusividade.

Não há regra específica pelo nome da unidade. Um porta-aviões pode receber
aeronaves em reparo quando sua ficha permite; outro navio sem slot compatível não
pode.

## Precedência de EVAC no transporte naval

- O pickup naval vazio procura primeiro uma unidade `IsUnderRepair`.
- Somente sem paciente compatível passa ao pickup comum.
- O envelope inicial considera encontro tático e progressão curta de até duas
  rodadas.
- Logs distinguem espera por `EVAC` de espera por embarque comum.
- O porta-aviões deixa de preferir automaticamente uma aeronave saudável quando
  existe uma aeronave em reparo compatível.

## Limites deste checkpoint

O serviço coordenador completo ainda será extraído em etapa posterior.

Esta versão ainda não contém:

- `TransportOperationsService`;
- modelo comum `TransportOperationDecision`;
- adaptadores formais Land, Air, Naval e Rail;
- escada completa Tactical, Operational e Strategic para todas as operações;
- supply comum para transportadores primários híbridos;
- política estratégica comum baseada em `Play Conservative`.

Esses itens permanecem como próximo refactor. A presente versão existe
deliberadamente como ponto estável anterior a essa migração.

## Arquitetura transacional

- As mudanças afetam somente seleção de decisão e construção de batches já
  existentes.
- Sensores continuam sendo as autoridades para embarque e desembarque.
- Nenhuma consulta consome combustível, estoque ou movimento.
- Nenhuma avaliação altera posição confirmada, FOW, detecção ou ocupação.
- Ações continuam passando pelo fluxo transacional normal e retornando a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/Services/AIController.MelhorDesembarque.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.Evac.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.Naval.cs`

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- Auditoria do desembarque parcial tactical.
- Auditoria da progressão operacional do passageiro restante.
- Auditoria da compatibilidade de EVAC pelos `transportSlots`.
- Auditoria da precedência EVAC sobre pickup naval comum.
- `git diff --check` aplicado aos arquivos de implementação.
- Resultado: build concluído com 0 erros.
