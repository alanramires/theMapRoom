# v4.6.6 — Refactor da AI Capturer 1/3

## Objetivo

Executar a primeira parte do refactor da decisão de transporte do
`AI Capturer`: integrar `QueroCaronaService` como gate único antes de iniciar a
busca por transportadores.

Nesta etapa, a nova consulta já participa da decisão efetiva. As proteções
locais antigas permanecem temporariamente para comparação e serão removidas na
parte 3/3.

## Gate único de necessidade

Foi criado `EvaluateCapturerRideNeed`, chamado uma única vez por tentativa de
embarque.

O wrapper fornece ao serviço:

- unidade;
- Tilemap;
- Terrain Database;
- contexto da unidade;
- setor atribuído, quando houver;
- dois turnos como envelope Operational;
- emulação de `IsUnderRepair` desativada durante a partida.

O resultado é avaliado antes de qualquer scan do `PodeEmbarcarSensor`.

## Capturador com plano

Quando existe objetivo atribuído:

- o contexto é `QueroCaronaContext.ComPlano`;
- o setor atribuído é enviado ao serviço;
- Tactical e Operational são avaliados contra o representante e as alternativas
  livres do setor;
- `QueroCarona = NÃO` encerra a tentativa de embarque;
- `QueroCarona = SIM` permite procurar um transporte compatível.

Unidades híbridas que satisfazem Capturador, mas ocupam outro papel no plano,
continuam usando o objetivo efetivo resolvido pelo controller.

## Capturador rogue ou rebelde

Quando não existe objetivo atribuído:

- o contexto é `QueroCaronaContext.RogueOuRebelde`;
- o serviço procura capturáveis úteis em Tactical e Operational;
- objetivos já ocupados por aliados são ignorados pelo serviço;
- `NÃO` devolve a unidade à agenda normal de captura, combate ou avanço;
- `SIM` libera a procura por transporte.

O HQ inimigo permanece como direção macro do controller.

## Emergência

`IsUnderRepair` runtime continua produzindo:

- `wantsRide = true`;
- `isEmergency = true`;
- prioridade sobre a avaliação normal do objetivo.

A resposta ainda é apenas permissão para procurar transporte. Compatibilidade,
vaga e legalidade continuam sendo validadas posteriormente.

## Ordem operacional

Antes do gate permanecem:

- decisão geral de reparo;
- handoff de Blitzkrieg;
- swap;
- captura na célula atual;
- captura próxima;
- defesa imediata de construção;
- retenção em rally assembly.

Depois do gate permanecem:

- `PodeEmbarcarSensor`;
- escolha do transportador;
- passageiro formal;
- compatibilidade de setor;
- fila courier;
- disputa por vaga;
- embarque estendido;
- aproximação ao transportador;
- guards de captura e combate de rogue.

O scan de transportadores adjacentes foi movido para depois do gate, evitando
trabalho desnecessário quando a unidade responde que não precisa de carona.

## Diagnóstico

O log do Capturer agora informa:

- `QueroCarona=SIM` ou `QueroCarona=NAO`;
- contexto;
- setor;
- emergência;
- envelope encontrado;
- custo da rota;
- alvo ou construção avaliada;
- motivo completo retornado pelo serviço.

## Compatibilidade temporária

Continuam ativos nesta etapa:

- `ShouldSkipCapturerEmbarkForShortWalk`;
- `ShouldSkipRogueTransportForFinalPressure`;
- validações equivalentes dentro do embarque estendido.

Esses guards permitem comparar a consulta nova com o comportamento anterior. A
remoção será feita somente depois da consolidação de todos os caminhos na parte
2/3.

## Arquitetura transacional

- `QueroCaronaService` permanece somente leitura.
- A consulta não altera unidade, posição, recursos, ocupação, FOW ou detecção.
- O resultado não cria reserva nem `PlayerAction`.
- O `AIController` continua escolhendo e materializando a ação.
- `PodeEmbarcarSensor` permanece como fonte de verdade da legalidade.
- O compromisso definitivo continua no fluxo que retorna a
  `CursorState.Neutral`.

## Arquivo alterado

- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.cs`

## Próxima etapa

A parte 2/3 deve propagar o resultado único por todos os caminhos de:

- embarque adjacente;
- passageiro formal;
- embarque estendido;
- aproximação ao transportador;
- disputa e cessão de vagas.

Nenhum desses caminhos deve recalcular a necessidade de carona.

## Verificação

- auditoria da ordem de decisão do Capturer;
- auditoria dos contextos com plano e rogue/rebelde;
- auditoria de que o scan ocorre somente depois do gate;
- auditoria do contrato transacional;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
