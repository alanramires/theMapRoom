# v4.6.7 — Refactor da AI Capturer 2/3

## Objetivo

Executar a segunda parte do refactor da decisão de transporte do
`AI Capturer`: propagar o único `QueroCaronaResult` produzido no início da
decisão por todos os caminhos de embarque e aproximação.

Nenhum ramo posterior volta a consultar `QueroCaronaService`.

## Resultado único

O resultado criado por `EvaluateCapturerRideNeed` agora acompanha a tentativa
completa de embarque:

- passageiro formal;
- embarque estendido;
- encontro direto com o transportador;
- transporte do mesmo setor;
- transporte de setor compatível;
- transporte livre;
- fila courier;
- overflow;
- aproximação rogue ao transportador.

Os métodos internos recusam operar quando não recebem uma decisão positiva.

## Embarque estendido

`TryBuildExtendedEmbarkBatch` e
`TryBuildDirectTransporterExtendedEmbarkBatch` passaram a receber o
`QueroCaronaResult`.

O mesmo objeto é preservado durante:

- tentativa a partir da célula atual;
- movimento até uma célula de parada;
- cálculo do movimento restante;
- busca de transportador em uma célula;
- validação de passageiro formal;
- fallback por setor;
- overflow.

Isso impede que diferentes passos do mesmo embarque reconstruam interpretações
independentes da necessidade do passageiro.

## Executor específico do Capturer

Foi criado `TryCapturerEmbarkFromHex`.

Esse wrapper:

- exige `QueroCaronaResult` não nulo;
- exige `wantsRide = true`;
- não recalcula Tactical ou Operational;
- encaminha a execução para o validador físico compartilhado.

Assim, qualquer caminho de embarque iniciado pelo Capturer precisa carregar a
decisão positiva produzida na entrada.

## Executor físico compartilhado

`TryEmbarkFromHex` continua sendo o executor físico de embarque.

Ele permanece separado porque também é utilizado pelo fluxo de EVAC. Uma
operação de resgate já possui sua própria autorização operacional e não deve
fabricar um `QueroCaronaResult` de Capturer apenas para reutilizar:

- contexto do transportador;
- slot compatível;
- custo de embarque;
- movimento restante;
- construção do batch.

A separação preserva a reutilização sem misturar políticas de agenda.

## Aproximação ao transportador

`FindNearestRogueTransporter` passou a receber a decisão de carona.

A aproximação:

- só ocorre depois de `QueroCarona = SIM`;
- não chama novamente o serviço;
- continua selecionando apenas transportador utilizável;
- continua respeitando slot, estado e limite de aproximação.

## Disputa por vagas

A cessão de vaga continua sendo responsabilidade de
`ShouldYieldEmbarkToNeedierCapturer`.

Ela é alcançada somente depois do gate positivo e preserva:

- reserva 1:1 entre transportador e passageiro;
- passageiro formal;
- vaga adicional;
- comparação com capturador mais distante;
- capacidade física do transportador.

`QueroCarona` não escolhe o vencedor da disputa.

## Compatibilidade temporária

Os guards antigos continuam ativos para comparação:

- `ShouldSkipCapturerEmbarkForShortWalk`;
- `ShouldSkipRogueTransportForFinalPressure`;
- short-walk interno do executor físico.

Eles ainda podem bloquear uma decisão positiva. A parte 3/3 removerá essas
duplicações e deixará `QueroCaronaService` como autoridade única da necessidade.

## Arquitetura transacional

- O resultado propagado é somente leitura.
- Nenhuma etapa altera unidade, posição, recursos, ocupação, FOW ou detecção.
- O wrapper não cria reservas.
- O executor constrói somente o batch transacional existente.
- EVAC preserva sua autorização operacional própria.
- O embarque definitivo continua no fluxo de compromisso que retorna a
  `CursorState.Neutral`.

## Arquivos alterados

- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Extended.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Scan.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Pathing.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Transporter.cs`

## Próxima etapa

A parte 3/3 deve:

- remover os guards locais substituídos;
- eliminar cálculos duplicados de distância e custo até objetivo;
- revisar logs e nomes;
- atualizar `Capturer.md`;
- auditar que existe uma única consulta de necessidade;
- validar capturadores com plano, rogue, rally, emergência e disputa de vaga.

## Verificação

- auditoria de todos os chamadores de embarque estendido;
- auditoria dos chamadores compartilhados por EVAC;
- auditoria da consulta única de `QueroCaronaService`;
- auditoria do contrato transacional;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
