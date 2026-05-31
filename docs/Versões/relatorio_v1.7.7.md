# Relatorio v1.7.7 - Transport Pressure

## Resumo

Esta versao adiciona a primeira camada estrategica de pressao por transportadores no planner e no shopping da IA. O objetivo foi fazer a IA sentir falta de APCs em planos de captura distantes, sem deixar `Transport` atropelar `Capture`, `Escort` ou `FireSupport`.

## O que entrou

- nova capability formal `Transport` no pipeline de planner/shopping;
- `AIUnitProfile` passa a reconhecer transportador terrestre como capability propria, separada de `Logistics`;
- planos de captura agora calculam `DesiredTransportCount`;
- a demanda considera capturadores terrestres realmente distantes do objetivo;
- o shopping desconta cobertura real de APCs ja uteis por plano;
- a falta de transporte virou demanda liquida, em vez de demanda bruta;
- calibragem inicial conservadora da prioridade de `Transport` no `AIShoppingManager`;
- exposicao da secao `Transporter` no `AIUnitProfile`;
- nova flag `returnToPickupAfterDisembark`.

## Regras atuais

- plano curto nao deve puxar transportador;
- plano distante com capturadores a pe passa a gerar `DesiredTransportCount`;
- a cobertura cai quando ja existe APC util, livre ou comprometido com o mesmo plano;
- APC ocupado com outro plano nao zera a demanda erradamente;
- `Transport` entra no shopping abaixo de `Escort` e `FireSupport`, mantendo `Capture` como prioridade dominante.

## Comportamento atual esperado

- objetivos distantes comecam a empurrar compra de APC;
- objetivos curtos continuam tendendo a marcha a pe;
- APC vazio sem plano pode voltar para a zona de pickup apos desembarque quando o profile habilitar isso;
- a IA passa a enxergar transporte como acelerador logistico de captura, e nao apenas como comportamento tatico isolado.

## Limites desta etapa

- ainda nao existe role formal de `Transport` no planner como existe para `Capture`, `Escort`, `Artillery` e `Support`;
- o ciclo completo de shuttle continuo ainda nao foi implementado;
- a calibragem de thresholds e da pressao de compra ainda pode subir ou descer conforme os testes de partida;
- a telemetria de `desired/assigned/missing transport` ainda pode ser melhorada no debug.
