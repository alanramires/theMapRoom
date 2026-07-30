# v4.6.8 — Refactor da AI Capturer 3/3

## Objetivo

Concluir o refactor da decisão de transporte do `AI Capturer`, removendo os
gates locais substituídos e tornando `QueroCaronaService` a única autoridade
para responder se o capturador precisa de transporte.

O controller continua responsável por prioridade operacional, escolha do
transportador, disputa por vaga e materialização da ação.

## Autoridade única

O Capturer realiza uma única consulta por decisão:

`EvaluateCapturerRideNeed → QueroCaronaService.Evaluate`

Essa consulta cobre:

- capturador com plano;
- capturador rogue ou rebelde;
- Tactical;
- Operational;
- objetivos ocupados por aliados;
- alternativas livres;
- emergência por `IsUnderRepair`.

Nenhum caminho posterior recalcula a necessidade.

## Gates removidos

Foram removidos:

- `ShouldSkipCapturerEmbarkForShortWalk`;
- `ShouldSkipRogueTransportForFinalPressure`;
- short-walk repetido dentro do executor físico;
- `CapturerShortWalkEmbarkCost`;
- `FindNearestCapturableForUnit`.

Também foram eliminados os cálculos locais associados:

- distância geométrica até objetivo;
- custo de terreno até prédio ou representante;
- troca local de objetivo já ocupado;
- tolerância própria de caminhada;
- comparação paralela com threshold de transporte.

Essas perguntas já pertencem ao `QueroCaronaService`.

## Fluxo final

Antes da consulta permanecem:

- reparo;
- handoff de Blitzkrieg;
- swap;
- captura na célula atual;
- captura próxima;
- defesa imediata;
- retenção em rally assembly.

Depois de `QueroCarona = SIM` permanecem:

- combate ou captura imediata de rogue;
- `PodeEmbarcarSensor`;
- passageiro formal;
- compatibilidade de setor;
- compatibilidade de rota naval;
- transporte livre;
- fila courier;
- overflow;
- disputa e cessão de vaga;
- embarque adjacente;
- embarque estendido;
- aproximação ao transportador.

`QueroCarona = NÃO` encerra apenas a tentativa de transporte. A unidade retorna
à agenda normal do Capturer.

## Executor físico

`TryCapturerEmbarkFromHex` continua exigindo o resultado positivo propagado.

`TryEmbarkFromHex` permanece como executor físico compartilhado com EVAC. Ele
valida legalidade, contexto, slot, movimento restante e batch, sem aplicar
política própria de necessidade de carona.

Essa separação evita obrigar EVAC a fabricar uma decisão de Capturer.

## Documentação do Capturer

`Capturer.md` foi atualizado para registrar:

- consulta única antes dos scans;
- contexto com plano;
- contexto rogue ou rebelde;
- avaliação Tactical e Operational;
- emergência;
- propagação do resultado;
- separação entre necessidade, escolha e legalidade.

Foi removida a descrição da antiga tolerância local de caminhada.

## Arquitetura transacional

- `QueroCaronaService` permanece somente leitura.
- A consulta não altera posição, recursos, ocupação, FOW ou detecção.
- O resultado não cria reserva nem `PlayerAction`.
- O controller continua escolhendo e materializando o batch.
- `PodeEmbarcarSensor` permanece como fonte de verdade da legalidade.
- O compromisso definitivo continua no fluxo explícito que retorna a
  `CursorState.Neutral`.

## Arquivos alterados

- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Scan.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Transporter.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/Capturer.md`

## Resultado

O fluxo passa a responder separadamente:

1. `QueroCaronaService`: o capturador precisa de transporte?
2. `AIController`: qual transporte deve ser tentado?
3. `PodeEmbarcarSensor`: o embarque é permitido?

A limpeza remove aproximadamente 150 linhas líquidas de política duplicada.

## Verificação

- auditoria de ausência dos gates removidos;
- auditoria da consulta única de `QueroCaronaService`;
- auditoria da preservação de rally, combate e disputa por vaga;
- auditoria do executor físico compartilhado com EVAC;
- auditoria do contrato transacional;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
