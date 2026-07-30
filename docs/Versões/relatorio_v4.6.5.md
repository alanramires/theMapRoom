# v4.6.5 — Refactor da AI Capturer (transport decision)

## Objetivo

Definir o refactor da decisão de transporte do `AI Capturer`, integrando
`QueroCaronaService` como fonte única para responder se o capturador realmente
precisa de transporte antes que o controller procure APC, helicóptero, navio,
trem ou outro transportador compatível.

Este checkpoint registra o contrato e a ordem de implementação. A consulta não
escolhe o transportador e não materializa ações.

## Separação de responsabilidades

O fluxo passa a separar três perguntas:

1. `QueroCaronaService`: a unidade precisa de transporte?
2. `AIController`: qual transportador deve ser tentado?
3. `PodeEmbarcarSensor`: o embarque é legal neste estado e contexto?

O serviço informa necessidade. O controller preserva prioridade operacional,
seleção de transporte, disputa por vaga, aproximação e construção do batch.

## Consulta única por decisão

O ponto previsto de integração é `TryDecideCapturerEmbarkAction`, depois da
resolução do objetivo atribuído e antes dos scans de transportadores.

Será criado um wrapper de consulta, como `EvaluateCapturerRideNeed`, responsável
por fornecer:

- unidade;
- Tilemap;
- Terrain Database;
- contexto com plano ou rogue/rebelde;
- setor atribuído quando houver;
- quantidade de turnos operacionais;
- emulação de `IsUnderRepair` desativada durante a partida;
- diagnóstico do resultado.

A avaliação deve ocorrer uma única vez por decisão do capturador e ser reutilizada
durante todo o fluxo de embarque.

## Capturador com plano

Capturador com objetivo atribuído usa `QueroCaronaContext.ComPlano`.

O serviço deve:

- consultar o representante do setor;
- considerar alternativas livres previstas pelo serviço;
- avaliar os envelopes Tactical e Operational;
- recusar carona quando a unidade consegue cumprir a rota por conta própria;
- solicitar carona quando a rota própria é insuficiente;
- preservar a emergência de `IsUnderRepair`.

A resposta positiva não escolhe o transportador. Passageiro formal,
compatibilidade de setor, rota naval, fila courier, vagas e overflow continuam no
controller.

## Capturador rogue ou rebelde

Capturador sem objetivo formal usa `QueroCaronaContext.RogueOuRebelde`.

O serviço deve:

- procurar capturáveis relevantes em Tactical e Operational;
- ignorar objetivos já ocupados por aliados;
- continuar examinando alternativas dentro dos envelopes;
- recusar carona quando ainda existe objetivo útil alcançável;
- solicitar carona quando não existe rota própria útil.

O HQ inimigo continua sendo a direção macro do controller. O serviço não decide
marcha, captura ou combate.

## Ordem operacional preservada

Antes do gate de carona permanecem:

- decisão de reparo;
- handoff de Blitzkrieg;
- troca de ocupante;
- captura na célula atual;
- captura próxima antes do embarque;
- defesa imediata de construção;
- retenção em rally assembly;
- captura ou combate imediato de rogue.

Depois de `QueroCarona = SIM` ou emergência, o controller procura:

1. passageiro formal;
2. transporte do mesmo setor;
3. transporte de setor compatível;
4. transporte livre;
5. vaga courier adicional;
6. overflow como último recurso;
7. aproximação ao transportador quando não há embarque imediato.

## Duplicações a remover

A integração deve substituir:

- `ShouldSkipCapturerEmbarkForShortWalk`;
- `ShouldSkipRogueTransportForFinalPressure`;
- cálculos locais equivalentes de custo até prédio ou setor usados apenas para
  decidir necessidade de carona;
- troca duplicada de objetivo ocupado feita somente para essa decisão.

Devem permanecer:

- `ShouldHoldRallyAssemblyInsteadOfEmbark`;
- `ShouldRogueCapturerFightBeforeTransport`;
- `ShouldYieldEmbarkToNeedierCapturer`;
- `TryGetCapturerEmbarkPreference`;
- preferências de passageiro formal e setor;
- validação física e contextual do `PodeEmbarcarSensor`.

## Diagnóstico previsto

O log do Capturer deve informar:

- contexto com plano ou rogue/rebelde;
- setor consultado;
- resultado do Quero Carona;
- emergência;
- envelope encontrado;
- alvo ou representante avaliado;
- custo da rota;
- motivo da aceitação ou recusa;
- transportador escolhido, quando houver.

## Arquitetura transacional

- `QueroCaronaService` permanece somente leitura.
- A consulta não altera unidade, posição, recursos, ocupação, FOW ou detecção.
- O getter não cria reserva, ordem nem `PlayerAction`.
- A escolha e a aproximação permanecem no `AIController`.
- `PodeEmbarcarSensor` continua sendo a fonte de verdade da legalidade.
- O embarque definitivo permanece no fluxo de compromisso explícito que retorna
  a `CursorState.Neutral`.

## Arquivos previstos

- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Scan.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Pathing.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Embark.Transporter.cs`
- `Assets/Scripts/Match/AI/Services/QueroCaronaService.cs`
- `Assets/Scripts/Match/AI/Units/Capturer/Capturer.md`

## Cenários de verificação

- capturador com plano próximo ao representante recusa carona;
- capturador com plano distante solicita carona;
- capturador com transporte formal embarca quando precisa;
- rogue com capturável livre em Tactical ou Operational recusa;
- rogue ignora prédio já ocupado e continua o scan;
- rogue sem capturável alcançável solicita carona;
- unidade em emergência solicita transporte;
- unidade em rally ativo permanece no rally;
- captura ou combate imediato vence a carona;
- dois capturadores disputando vaga preservam reserva e prioridade;
- transporte incompatível continua bloqueado pelo `PodeEmbarcarSensor`;
- sem transporte disponível, o Capturer retoma sua agenda normal.

## Próximo passo

Implementar o gate único de `QueroCarona`, remover as decisões locais
substituídas, preservar as políticas próprias do controller e validar os
cenários de campo para capturadores com plano, rogue e em emergência.
