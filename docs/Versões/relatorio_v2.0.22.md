# Relatorio de Atualizacao - v2.0.22

## AI Artillery refine

Esta versao refina o comportamento de artilharia e suporte da IA, com ajustes em prioridade de tiro, reposicionamento, compras de apoio e suporte logistico.

## Em uma frase

A IA passa a usar artilharia com mais disciplina: valoriza alvos caros e capturadores em construcoes, evita avancos perigosos, compra suporte com mais criterio e coordena logistica para manter unidades em operacao.

## Artilharia e fire support

- Unidades com preferencia de modo artilharia passam a tratar tiro a distancia como postura principal.
- O filtro de ataque pode exigir tiro no alcance maximo quando a unidade deve evitar combate aproximado.
- Rogue fire support agora avanca prioritariamente na direcao do HQ inimigo quando nao ha tiro disponivel.
- O score de alvo ganhou peso por custo, nivel elite e ameaca a construcao capturavel.
- Capturadores inimigos em construcao propria parcialmente capturada viram alvo de alta prioridade.
- O log de decisao exibe valor economico do alvo e ameaca de construcao.

## Reposicionamento

- Fire support conservador evita trocar para celulas com ameaca maior que a posicao atual.
- A pressao tatica foi limitada para nao superar demais criterios defensivos.
- Ameaca local ganhou peso maior para unidades conservadoras.
- O fallback de avanco so entra quando a unidade ainda esta fora do alcance desejado.
- A margem minima de movimento pode ser ajustada por chamada, reduzindo reposicionamentos ruins.

## Iniciativa defensiva

- Fire support com tiro contra capturador inimigo em construcao propria ganha prioridade na fila.
- Isso permite que a artilharia tente interromper captura antes da infantaria defensora se reposicionar.
- A verificacao respeita linha de tiro, arma, movimento possivel e decisao normal de ataque.

## Logistica

- Foi adicionado fluxo dedicado para unidades de logistica.
- A logistica tenta reparar, recarregar por transferencia, voltar para recarga, suprir aliados e reposicionar em retaguarda.
- Unidades sob reparo e alvos preventivos entram no score de atendimento.
- A IA considera emergencia defensiva de base ao escolher alvo e celula de apoio.
- Compras agora calculam demanda logistica a partir de unidades em reparo e logistica ativa.

## Compras da IA

- O planejador ganhou filtro `onlyLogistics`.
- Capturadores agora sao comprados em lotes progressivos para abrir espaco a transporte, logistica e fire support.
- Transporte preventivo considera massa de capturadores em campo.
- Fire support em reparo deixa de bloquear compra de novo suporte de fogo.
- A IA evita comprar unidade puramente antiaerea quando nao ha ameaca aerea ativa.
- Defesa contra blindado pode reservar dinheiro para tank elite no turno seguinte.
- Compras defensivas usam apenas construcoes criticas de base/HQ como referencia.

## Configuracao e cenas

- Configuracao de servico do comando passou a ser por jogador/time.
- O atalho de partida padrao agora inicia Verde humano contra Vermelho IA com comando automatico para a IA.
- Mapas em desenvolvimento foram reorganizados para a area `Em dev`.
- Catalogos e assets de Battle Map receberam ajustes para os novos cenarios.

## Bloco tecnico curto

- Ajustados `AIController.FireSupport.Helpers.cs`, `AIController.FireSupport.cs`, `AIController.FireSupport.Rogue.cs` e `AIController.FireSupport.Defender.cs`.
- Adicionados `AIController.Logistics.cs` e `AIController.Logistics.Helpers.cs`.
- Ajustados `AIController.Batches.cs` para batches de supply e transfer receive.
- Ajustado `AIController.Initiative.cs` para prioridade de artilharia em defesa de construcao.
- Ajustado `AIShoppingPlanner.cs` para demanda logistica, compra progressiva e reservas defensivas.
- Ajustados `PartidaConfig.cs`, `NewGamePanelController.cs` e `PanelMenu.cs` para comando automatico por time.

## Resultado

Versao preparada como pacote `AI Artillery refine`, focada em tornar artilharia, logistica e compras da IA mais coerentes em partidas com pressao de captura, defesa de base e suporte de longo alcance.
