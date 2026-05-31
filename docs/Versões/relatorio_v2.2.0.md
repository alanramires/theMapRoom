# v2.2.0 - AI do Comando (antes)

## Resumo

Point save antes da implementacao da camada operacional de comando da IA.

Este marco registra o estado atual da IA antes de introduzir `AIOperation`, operacoes nomeadas e deficits tipados por necessidade.

## Contexto

- A IA ainda opera principalmente por unidade individual e por slots de `TeamObjectivePlan`.
- O planejamento global existe, mas funciona mais como lista de objetivos/setores do que como comando de grupos.
- Compras defensivas e preventivas ainda estao espalhadas no `AIShoppingPlanner`.
- Base, setor ameacado, helicopteros, antiaereo e suporte indireto ainda dependem de heuristicas separadas.

## Problemas observados

- Base sob ataque aereo pode gerar respostas incoerentes ou tardias.
- Setores proprios sob pressao, como Alpha, precisam de pacote defensivo mais claro.
- Helicopteros podem ficar desorientados procurando passageiros ou objetivos sem uma operacao formal.
- Compra por `UnitRole` generico confunde necessidades diferentes, como tanque versus AAA ou obus versus SAM.
- Mapas grandes exigem uma visao por frente/operacao, nao apenas decisoes locais por unidade.

## Proxima direcao

- Criar `AIOperation` como camada acima de `TeamObjectivePlan`.
- Introduzir `AINeedKind` para deficits tipados: `AAA`, `SAM`, `Artillery`, `FighterB`, `AirTransport`, entre outros.
- Implementar operacoes iniciais:
  - `BaseDefense`
  - `SectorDefense`
  - `AirliftCapture`
  - `PreventiveDefense`
- Fazer o shopping ler deficits operacionais antes dos slots avulsos.
- Manter `Router`, `Initiative` e handlers taticos atuais como fallback no primeiro passo.

## Defesa preventiva desejada

- Comprar `Artilharia de Campanha`, `AAA` e `SAM` de forma explicita quando a base ainda nao tem cobertura minima.
- Permitir defesa preventiva mesmo sem ameaca imediata, especialmente com caixa alto ou turno avancado.
- Manter `ComputeGuaranteedBaseDefense()` como safety net ate validar a nova camada.

## Observacao

Este save e o ponto de comparacao antes da IA ganhar uma camada de comando operacional. A validacao futura deve comparar se a nova versao reduz caos em defesa de base, coordena melhor helicopteros e melhora compras em mapas grandes.
