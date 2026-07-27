# v4.6.4 — Refactor da AI Transporte 4/4

## Objetivo

Concluir o refactor do transporte, removendo a escolha paralela de passageiros
dos adaptadores de domínio e consolidando Pickup e EVAC sobre a decisão produzida
por `MelhorEmbarqueService`.

O serviço escolhe e descreve a operação. O `AIController` apenas materializa a
opção vencedora.

## Pickup consolidado

`TryQueryTransportPickupOperation` passou a atender os três envelopes:

- Tactical;
- Operational;
- Strategic.

A opção selecionada preserva passageiro, LZ, envelope, disposição da carona,
estado das rotas, custos e nota até a construção do batch.

O Strategic continua sujeito à validação de retaguarda e ao comportamento
conservador da unidade.

## EVAC pelo Melhor Embarque

EVAC deixou de procurar novamente um paciente por um seletor próprio.

A operação agora consulta o mesmo ranking do Pickup e exige uma opção com
disposição `Emergency`. Assim, passageiro e LZ permanecem os mesmos desde a
avaliação até a materialização.

O diagnóstico da decisão identifica explicitamente a operação como EVAC.

## Adaptadores de domínio

Os caminhos terrestre, aéreo e naval não reabrem mais uma busca global quando o
transportador está vazio.

As entradas legadas ficaram restritas à carga já embarcada:

- entrega terrestre e courier;
- entrega aérea;
- entrega naval;
- encaminhamento de paciente já embarcado.

Se um courier encontra um estado inconsistente sem passageiros, devolve `null` e
libera a unidade para outra atividade, em vez de iniciar um novo scanner de
pickup.

## Remoção de duplicações

Foram removidos do fluxo comum:

- implementação antiga desativada por `#if false`;
- classificação paralela por distância ao objetivo;
- coleta antiga de rendezvous por onda;
- interseção duplicada entre alcance do passageiro e LZ;
- materialização específica de EVAC;
- sondagens auxiliares que repetiam os seletores de Shuttle e Air.

O ponto único de materialização de Pickup e EVAC é
`TryBuildTransportPickupOperation`.

## Reserva de passageiro

O ranking unificado respeita a reserva 1:1 entre transportadores durante a mesma
passada da Phase 2.

- candidato reservado por outro transportador não entra na consulta;
- a consulta permanece somente leitura;
- a reserva é registrada somente quando o controller consegue materializar uma
  ordem;
- uma LZ sem movimento ou progressão válida não prende o passageiro.

Isso evita que dois transportadores produzam ordens para o mesmo passageiro sem
introduzir mutação no serviço de avaliação.

## Arquitetura transacional

- `MelhorEmbarqueService` e `QueroCaronaService` permanecem somente leitura.
- Consulta e ranking não alteram unidade, posição, recursos, ocupação, FOW ou
  detecção.
- A reserva entre transportadores é estado efêmero de planejamento da Phase 2.
- A materialização continua produzindo o batch transacional existente.
- Nenhuma consulta marca `HasActed`.
- O compromisso definitivo permanece no fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.Air.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.Naval.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.Transportador.Courier.cs`

## Resultado

O fluxo de transporte passa a ter uma única autoridade para escolher Pickup e
EVAC:

1. `TransportOperationsService` organiza a prioridade operacional;
2. `MelhorEmbarqueService` classifica passageiro e LZ;
3. `QueroCaronaService` informa a necessidade do passageiro;
4. o `AIController` materializa exatamente a opção selecionada;
5. os adaptadores de domínio cuidam da entrega da carga existente.

## Verificação

- auditoria dos chamadores das entradas terrestre, aérea e naval;
- auditoria dos caminhos residuais de Shuttle, Air, Naval e EVAC;
- auditoria da reserva 1:1 de passageiros;
- auditoria do contrato de ações transacionais;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
