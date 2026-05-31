# Relatorio v2.2.4 - Antes de notação

## Tema

Ajustes finais antes da camada de notacao: foco em estabilidade operacional da IA, transporte/EVAC, compras, replay e documentacao de analise.

## Principais mudancas

- EVAC de unidade em reparo deixa de usar a selecao generica de courier, evitando que soldado ferido seja tratado como "rogue" e enviado para alvo ofensivo.
- Desembarque EVAC agora escolhe apenas celula segura e proxima do destino de reparo, sem inimigo visivel nas redondezas.
- Unidade terrestre em reparo nao escolhe aeroporto como destino normal de reparo quando nao e aeronave.
- Transporte aereo e shuttle receberam ajustes para resgate, pickup e desembarque com melhor consideracao de rota, ocupacao e seguranca.
- Iniciativa e embarque de capturadores foram refinados para reduzir bloqueios e melhorar prioridade operacional.
- Compras e planejamento da IA foram calibrados para reagir melhor ao estado atual do mapa.
- Automacao de turno, replay e servicos de comando receberam ajustes de consistencia.
- Documentacao de IA foi expandida com nova avaliacao e analise de comportamento.

## Comportamento esperado

- Feridos embarcados em EVAC devem ser levados para HQ/base/construcao aliada segura, nao para construcao inimiga capturavel.
- Helicopteros de resgate devem continuar carregando o ferido quando o ponto de desembarque ainda nao for seguro ou estiver fora do alcance adequado.
- Aeroportos deixam de atrair unidade terrestre ferida como se fossem oficina universal.
- A IA deve produzir menos decisoes contraditorias entre transporte, reparo, iniciativa e compra.

## Validacao

- Build validado com `dotnet build Assembly-CSharp.csproj`.
- Resultado: 0 erros; permanecem apenas warnings conhecidos de APIs obsoletas do Unity.

## Observacoes

Esta versao fecha a rodada de ajustes "Antes de notação", preservando as mudancas pendentes de cena, IA, sensores, replay e documentacao no mesmo pacote de entrega.
