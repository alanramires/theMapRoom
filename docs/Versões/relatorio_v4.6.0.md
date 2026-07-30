# v4.6.0 — Preparativos pro refactor do transporte

## Objetivo

Criar um checkpoint seguro antes da consolidação do `AIController` de transporte.
Esta versão introduz os contratos e ferramentas necessários para investigar e
classificar operações de APC, supridor, trem de carga, Chinook, hidroavião,
porta-aviões e navio de desembarque sem ainda remover todos os seletores antigos.

O checkpoint separa progressivamente três perguntas:

- a operação é legal agora;
- o passageiro precisa de transporte;
- qual combinação de passageiro e local de embarque é mais promissora.

## Serviço comum de operações de transporte

Foi criada a fundação `TransportOperationsService`, com:

- perfil de capacidades derivado do `UnitData`;
- adaptadores declarados para Land, Air, Naval e Rail;
- modelo comum `TransportOperationDecision`;
- operações Hospital, EVAC, Supply, Pickup, Courier e Delivery;
- progressão por envelopes Tactical, Operational e Strategic;
- consumo do `AIActionReachCoordinator`;
- diagnóstico uniforme por unidade, operação e envelope.

O serviço apenas classifica consultas fornecidas pelo consumidor. Ele não move
unidades, não monta `PlayerAction`, não consome recursos e não confirma batches.

## Integração preparatória no AIController

O roteador e os controladores de transporte começaram a consumir o coordenador
comum. A materialização das decisões continua no `AIController`, preservando as
particularidades de movimento e execução de cada domínio.

Foram preparados fluxos comuns para:

- busca Tactical e Operational de EVAC;
- atendimento por supridor híbrido;
- pickup;
- entrega e courier com carga;
- precedência de paciente em transportadores com capacidade hospitalar;
- uso da LZ escolhida pela consulta na progressão do transportador.

## Melhor Embarque

Foi criado `MelhorEmbarqueService`, uma consulta pura iniciada no transportador.
O serviço:

- lê os `transportSlots` do `UnitData`;
- valida classe, domínio, skill, capacidade e exclusividade;
- procura LZs permitidas por terreno, estrutura mais terreno ou construção;
- respeita as regras de pouso quando o transportador é aéreo;
- separa resultados em Tactical, Operational e Strategic;
- registra custos do transportador e do passageiro;
- produz ranking e diagnóstico de rejeições.

Também foi criada a janela:

`Tools > Transporte > Melhor Embarque`

A busca Strategic permanece opcional para evitar custo desnecessário. A direção
provável pode ser inspecionada separadamente, sem transformar a ferramenta em
controller de movimento.

## Quero Carona

Foi criado `QueroCaronaService` como contrapeso da oportunidade encontrada pelo
Melhor Embarque. A consulta estima se o passageiro ainda precisa de transporte:

- rogue ou rebelde procura objetivos capturáveis em Tactical e Operational;
- unidade com plano consulta o representante e alternativas livres do setor;
- objetivo já ocupado por aliado é ignorado, e a busca continua;
- resposta positiva é uma estimativa, não uma ordem;
- `IsUnderRepair` produz necessidade emergencial com prioridade elevada.

Em Scene/Edit Mode, a ferramenta emula `IsUnderRepair` sem alterar a unidade. A
emulação lê o estado do `UnitManager` e os critérios de `AI Behavior > Repair
Decision` no `UnitData`:

- limite de HP;
- percentual de autonomia;
- percentual de munição embarcada.

A janela informa se a emergência veio da flag runtime ou da emulação e apresenta
os valores e limites que participaram da avaliação.

Foi criada a janela:

`Tools > Transporte > Quero Carona`

## Apoios de diagnóstico e compatibilidade

- A ferramenta de Retaguarda foi reorganizada para trabalhar com massa inimiga,
  massa aliada e unidade/local investigado.
- A participação dos papéis em batalha foi explicitada para impedir que funções
  puramente logísticas recebam objetivos ofensivos indevidos.
- As consultas de pouso passaram a detalhar construção, estrutura mais terreno,
  terreno, camada e skills alternativas.
- `PodeSuprir` passou a compartilhar as regras de pouso e camada relevantes para
  atendimento de aeronaves.
- Casos híbridos, como hidroavião e porta-aviões supridor, passaram a depender
  mais das capacidades declaradas no `UnitData`.

## Limites conhecidos deste checkpoint

Esta versão prepara o refactor, mas deliberadamente ainda não o encerra:

- `QueroCarona` ainda não compõe a nota do `MelhorEmbarque`;
- a resposta `NÃO` ainda não é convertida em penalidade de fallback;
- o Melhor Embarque ainda exige uma rota atual do passageiro até a LZ;
- a escolha final ainda contém política baseada na distância ao objetivo dentro
  do controller;
- seletores antigos de shuttle, transporte aéreo, naval e EVAC continuam
  coexistindo;
- a implementação anterior desativada por `#if false` ainda precisa ser removida;
- o ranking ainda é centrado na LZ, não numa lista plana passageiro–LZ.

Esses pontos formam o escopo do refactor do transporte iniciado após este
checkpoint.

## Arquitetura transacional

- Todas as novas consultas são somente leitura.
- Nenhuma ferramenta seta `IsUnderRepair` ao emular a condição no editor.
- Nenhuma avaliação altera combustível, munição, HP, estoque ou movimento.
- Nenhum getter cria reserva, ordem ou `PlayerAction`.
- A materialização continua no `AIController`.
- A confirmação de ações permanece no fluxo transacional normal, com início e
  término em `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/Services/Transport/TransportOperationsService.cs`
- `Assets/Scripts/Match/AI/Services/MelhorEmbarqueService.cs`
- `Assets/Scripts/Match/AI/Services/QueroCaronaService.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`
- `Assets/Editor/MelhorEmbarqueWindow.cs`
- `Assets/Editor/QueroCaronaWindow.cs`
- `Assets/Editor/RetaguardaWindow.cs`

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- auditoria do fluxo Tactical e Operational de transporte;
- auditoria das LZs permitidas pelo `UnitData`;
- auditoria da emulação de `IsUnderRepair` em Scene/Edit Mode;
- auditoria de que os serviços não materializam ações;
- `git diff --check`;
- resultado: builds concluídos com 0 erros.
