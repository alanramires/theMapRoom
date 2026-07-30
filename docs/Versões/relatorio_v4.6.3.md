# v4.6.3 — Refactor da AI Transporte 3/4

## Objetivo

Executar a terceira parte do refactor do transporte: migrar a escolha efetiva do
pickup para o ranking plano produzido por `MelhorEmbarqueService`.

O `AIController` deixa de escolher novamente o passageiro depois da consulta e
passa a materializar diretamente a combinação passageiro–LZ vencedora.

## Escolha pelo ranking plano

`TryQueryTransportPickupOperation` agora:

1. consulta o Melhor Embarque;
2. filtra o envelope solicitado;
3. seleciona a melhor `MelhorEmbarqueOption`;
4. cria uma `TransportOperationDecision` com os dados da opção.

Foi removida da escolha ativa a segunda classificação por
`objectiveDistance`. A nota bilateral do Melhor Embarque passa a ser a autoridade
para comparar os candidatos.

## TransportOperationDecision ampliada

A decisão comum passa a carregar:

- opção de pickup vencedora;
- passageiro;
- LZ;
- envelope;
- nota;
- disposição da carona;
- estado da rota do passageiro;
- custo da rota do passageiro;
- custo da rota do transportador;
- diagnóstico.

Esses dados seguem juntos da consulta até a materialização, evitando que cada
domínio reconstrua outra interpretação do encontro.

## Materialização centralizada

`TryBuildTransportPickupOperation` passou a respeitar diretamente a LZ escolhida:

- se a LZ é alcançável agora, move para ela;
- se existe progressão válida, avança em direção a ela;
- se já está na LZ, aguarda o passageiro;
- se a LZ não pode ser alcançada nem progredida, devolve `null` e libera a
  unidade para outra atividade.

O fallback ativo não recalcula outra LZ por regras paralelas de Air, Naval ou
Shuttle.

O serviço continua escolhendo e descrevendo. O controller continua sendo o único
responsável por construir o `PlayerAction`.

## Fallback oportunista

`OpportunisticFallback` não participa do pickup prioritário.

Passageiros que responderam `NÃO` ao Quero Carona são avaliados novamente apenas
depois de:

- objetivos;
- captura e assalto;
- fogo indireto e combate;
- inteligência;
- operações aéreas;
- logística.

Somente quando essas atividades não produziram uma ação o transportador pode usar
a oportunidade negativa para se posicionar ou aguardar.

Esse comportamento preserva o candidato próximo sem transformar a estimativa
`NÃO` em ordem obrigatória.

## Envelopes

O fluxo normal continua respeitando Tactical antes de Operational.

O fallback oportunista possui uma consulta dedicada:

- tenta Tactical;
- depois tenta Operational;
- aceita somente opções `OpportunisticFallback`.

`Emergency` e `Requested` permanecem no fluxo normal do
`TransportOperationsService`.

## Compatibilidade e dívida restante

A escolha ativa do serviço já não depende da coleção legada por LZ.

Ainda permanecem no arquivo, para remoção na parte 4/4:

- implementação anterior desativada por `#if false`;
- `IsPickupCandidateForTransportWave`;
- coletores antigos de rendezvous;
- helpers de interseção substituídos pelo ranking plano;
- seletores paralelos ainda usados por caminhos residuais fora do serviço.

## Arquitetura transacional

- O Melhor Embarque e o Quero Carona continuam somente leitura.
- A decisão não altera posição, ocupação, recursos, FOW ou detecção.
- A materialização produz apenas o batch transacional existente.
- Esperar na LZ usa o fluxo normal de movimento parado.
- Nenhuma consulta marca `HasActed`.
- O compromisso permanece no fluxo explícito e retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/AIController.Router.cs`
- `Assets/Scripts/Match/AI/Services/Transport/TransportOperationsService.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`

## Próxima etapa

A parte 4/4 deve:

- remover código desativado e helpers sem chamadores;
- migrar ou eliminar seletores residuais de Air, Naval, Shuttle e EVAC;
- manter adaptadores de domínio apenas para execução;
- revisar logs e nomes;
- confirmar que nenhum caminho reabre uma escolha global paralela;
- executar testes de campo por tipo de transportador.

## Verificação

- `dotnet restore Assembly-CSharp.csproj`;
- `dotnet restore Assembly-CSharp-Editor.csproj`;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- auditoria dos chamadores de `TryQueryTransportPickupOperation`;
- auditoria da remoção da escolha ativa por `objectiveDistance`;
- auditoria da ordem do fallback oportunista no roteador;
- auditoria da materialização única pela LZ;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
