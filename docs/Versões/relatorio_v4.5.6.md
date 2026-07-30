# v4.5.6 — TransportPlanningSnapshot

## Visão geral

Este checkpoint implementa a Parte 6 do plano de otimização do tabuleiro:
um `TransportPlanningSnapshot` compartilhado por transportador e snapshot
confirmado.

O planejamento de transporte deixa de reconstruir os mesmos alcances e
avaliações ao alternar entre EVAC, Pickup, Supply, Tactical, Operational e
Strategic. A decisão continua sendo materializada somente depois que uma opção
vence; o snapshot não contém ordens, reservas definitivas ou efeitos de jogo.

## Conteúdo do snapshot

Cada `TransportPlanningSnapshot` registra:

- transportador, origem, movimento restante e combustível;
- referência do `AIWorldSnapshot` e do plano de objetivos;
- revisão confirmada do `ConfirmedOccupancyIndex`;
- caminhos alcançáveis pelo transportador na rodada;
- resultado classificado de `MelhorEmbarque`;
- respostas de `QueroCarona` por passageiro;
- projeções de embarque solicitadas pelo papel Assalto;
- alvo e tier escolhidos para Supply;
- opções Tactical, Operational e Strategic já validadas.

O ranking e as razões produzidos pelos sensores permanecem os mesmos. O
snapshot apenas conserva o resultado para os consumidores seguintes.

## EVAC e Pickup

EVAC e Pickup agora consultam a mesma execução de `MelhorEmbarque`.

- EVAC filtra passageiros em emergência;
- Pickup filtra pedidos normais e, quando permitido, oportunidades;
- Tactical, Operational e Strategic filtram o tier correspondente;
- rejeição de uma opção por segurança ou impossibilidade de materialização
  continua procurando a próxima opção da lista;
- nenhuma dessas tentativas reconstrói a malha.

O alcance do transportador calculado durante o planejamento também é reutilizado
na materialização do movimento até a LZ.

## Supply

A seleção de demanda de Supply passa a ser calculada uma vez por snapshot.

Antes, as tentativas Tactical e Operational podiam repetir:

- construção dos caminhos logísticos;
- verificação de compatibilidade via `PodeSuprir`;
- classificação das necessidades;
- busca do melhor alvo.

Agora o alvo é escolhido uma vez, recebe seu tier e é filtrado pelas tentativas
do `TransportOperationsService`. A materialização reutiliza os caminhos e o
estado de defesa de base já avaliados.

## Assalto e projeções de passageiro

O passageiro de Assalto também usa o `TransportPlanningSnapshot` do
transportador.

Quando o panorama completo de Pickup já existe, Assalto apenas filtra a opção
da própria unidade. Caso contrário, cria uma projeção estreita para aquele
passageiro, reutilizando o alcance do transportador. Isso evita transformar uma
consulta individual em uma avaliação de todos os passageiros do exército.

As projeções não reservam o transportador. A reserva provisória da Phase 2
continua ocorrendo somente quando a decisão escolhida é materializada.

## Integração com Movement Reach Cache

`MelhorEmbarqueRequest` passou a aceitar caminhos pré-calculados do
transportador. Quando fornecidos pelo snapshot, `MelhorEmbarqueService` não
abre outra onda para a mesma origem e orçamento.

O `MovementReachCache` da Parte 5 permanece como fonte compartilhada para
outras consultas equivalentes. O snapshot adiciona uma camada semântica acima
dele: além dos caminhos, conserva passageiros, LZs, `QueroCarona`, ranking,
tiers e decisões de serviço.

## Contrato transacional

O snapshot não altera a verdade do tabuleiro.

- não contém `PlayerAction`;
- não move unidade nem altera ocupação;
- não consome combustível, estoque ou recursos;
- não marca `HasActed`;
- não altera FOW, detecção ou inteligência;
- não confirma reservas de passageiro;
- não é publicado quando a unidade diverge da ocupação confirmada.

Para entrar no cache da Phase 2, o transportador runtime deve coincidir com seu
registro no `ConfirmedOccupancyIndex`. A chave lógica também exige a mesma
revisão confirmada, o mesmo `AIWorldSnapshot`, o mesmo plano, origem, movimento
e combustível.

Se a ocupação ainda não estiver pronta ou houver estado provisório, o resultado
fica restrito à consulta atual. O cache é limpo no começo de cada Phase 2.

## Telemetria

Foram adicionados contadores para observar a economia:

```text
TransportPlanningSnapshotBuilds
TransportPlanningSnapshotHits
TransportPlanningReachReuses
TransportPlanningRideNeedHits
TransportPlanningPassengerProjectionBuilds
TransportPlanningPassengerProjectionHits
```

`MelhorEmbarqueCalls`, `MovementCacheHits` e `MovementWavesBuilt` continuam
permitindo comparar quantas avaliações semânticas e ondas reais aconteceram.

## Arquivos principais

- `Assets/Scripts/Match/AI/Services/Transport/TransportOperationsService.cs`;
- `Assets/Scripts/Match/AI/Services/MelhorEmbarqueService.cs`;
- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`;
- `Assets/Scripts/Match/AI/Units/Assault/AIController.Assault.cs`;
- `Assets/Scripts/Match/AI/1. Phases/AIController.Phase2.cs`.

## Validação

- `Assembly-CSharp.csproj`: 0 erros;
- `git diff --check`: aprovado;
- nenhuma alteração em regras de compatibilidade, pontuação ou prioridade;
- nenhum estado provisório é publicado como snapshot confirmado;
- EVAC, Pickup e Supply reutilizam o planejamento durante a decisão e a
  materialização;
- Assalto reutiliza o alcance sem avaliar passageiros alheios desnecessariamente.

Os avisos já existentes de APIs obsoletas e serialização permanecem sem relação
com este checkpoint.

## Teste recomendado

Em uma rodada grande com transportadores vazios, supridores e passageiros:

- comparar `TransportPlanningSnapshotBuilds` com
  `TransportPlanningSnapshotHits`;
- confirmar que EVAC Tactical/Operational e Pickup
  Tactical/Operational/Strategic não multiplicam `MelhorEmbarqueCalls`;
- confirmar `TransportPlanningReachReuses` na seleção e materialização;
- observar Assalto e fogo de apoio pedindo transporte;
- testar Supply com alvo Tactical e com alvo Operational;
- testar cancelamento, batch recusado, embarque e movimento comprometido;
- confirmar que uma nova revisão de ocupação não reutiliza o snapshot anterior.
