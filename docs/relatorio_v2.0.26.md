# Relatorio de Atualizacao - v2.0.26

## AI Refine III

Esta versao fecha mais uma rodada de refinamento da IA, concentrada em embarque de capturadores, entrega por transporte, prioridade de unidades que bloqueiam captura, reparo/logistica e organizacao dos atalhos de debug.

## Em uma frase

A IA ficou menos travada em situacoes de frente congestionada: capturadores aceitam melhor APC livre, transportes desembarcam com criterio mais flexivel, unidades bloqueando captura saem antes do caminho e o AI Resume passa para F12 para reduzir conflito com outros paineis.

## Capturadores e embarque

- Capturadores primarios tambem podem aceitar APC sem passageiro formal.
- O embarque por shuttle livre deixa de depender de ser capturador secundario.
- O fluxo de embarque ganhou logs explicitos para bloqueio por setor, distancia de pickup, movimento restante e falta de slot.
- A verificacao de caminhada curta usa a posicao real da unidade, evitando comparacoes inconsistentes com o hex intermediario.

## Transporte courier

- O transporte agora registra as opcoes simuladas de desembarque e a distancia de cada celula ate o alvo.
- O desembarque apos movimento pode ocorrer quando a celula de desembarque ou o proprio transporte ja estao dentro do alcance desejado.
- Se o transporte estiver travado ou ja dentro do range, pode liberar passageiros mesmo quando a celula exata de desembarque ainda nao e perfeita.
- O score de desembarque deixou de penalizar ameaca generica no calculo base, preservando a distancia ao objetivo como criterio principal.
- Passageiros com setor atribuido podem cair para a celula representativa do setor quando nao ha construcao capturavel disponivel.

## Defesa, reparo e ordem de acao

- Defensor em SOS critico, quando movel, deixa de segurar posicao automaticamente e passa a avaliar mover+atacar dentro da zona.
- Unidades estacionarias de longo alcance continuam autorizadas a manter posicao no SOS.
- Dentro do grupo de prioridade alta, unidades que bloqueiam alvo de captura agem antes de vacaters e outras unidades.
- Unidades em reparo que estao bloqueando capturador designado priorizam sair do predio em vez de aguardar reparo seguro.
- O modo de last stand/reparo respeita melhor a necessidade de liberar construcao para captura planejada.

## Logistica e compras

- O score logistico passou a considerar valor economico da unidade atendida.
- Unidades caras em reparo, sem combustivel ou com municao baixa ganham prioridade maior no atendimento.
- A IA passa a demandar segunda unidade logistica com tres alvos de reparo, em vez de esperar quatro.
- Fire support elite deixa de ter limite superior rigido de duas unidades quando a massa de capturadores ja esta pronta.

## Debug e atalhos

- `AI Resume` foi movido de F9 para F12.
- F12 fica reservado nos fluxos de save, helper, tutorial e replay para evitar colisao com o DebugManager.
- F9 volta a ficar disponivel para os paineis que ja usavam esse atalho.
- O texto do AI Step foi atualizado para mostrar `F11: executar | F12: resume`.

## Bloco tecnico curto

- Ajustados `AIController.Capturer.Embark.cs` e `AIController.Capturer.Defender.cs`.
- Ajustado `AIController.Transportador.Courier.cs` para logs e regras de desembarque.
- Ajustados `AIController.Phases.cs` e `AIController.Repair.cs` para prioridade de blocker de captura.
- Ajustados `AIController.Logistics.Helpers.cs` e `AIShoppingPlanner.cs`.
- Ajustados `AIController.Debug.cs`, `DebugManager.cs`, `SaveGameManager.cs`, `PanelHelperController.cs`, `PanelVisibilityHotkeysController.cs` e `ReplayPanelUI.cs` para a reserva de F12.

## Resultado

Versao preparada como pacote `AI Refine III`, focada em reduzir bloqueios de execucao da IA e deixar transporte, captura, reparo e debug mais previsiveis em partidas com muita unidade disputando o mesmo setor.
