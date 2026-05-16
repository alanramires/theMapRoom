# Relatorio de Atualizacao - v2.0.26b

## AI Refine IV

Esta versao continua o refinamento da IA iniciado na serie v2.0.26, com foco em reduzir passividade, melhorar decisoes mid-turn e deixar transporte/logistica mais coerentes em situacoes de combate.

## Em uma frase

A IA reage melhor durante a propria fase: invalida defesas que perderam ameaca, evita embarques ruins, permite captura emergencial quando o avanco trava e trata reboque/logistica conservadora com mais criterio.

## Ajustes principais

- Objetivos defensivos agora podem ser invalidados no meio do turno quando a ameaca visivel foi eliminada.
- A Fase 2 passa a usar `AIWorldSnapshot.BuildLight`, reduzindo custo por iteracao sem carregar dados que os handlers nao usam.
- A ordenacao de iniciativa foi otimizada com cache de grupo e re-sort apenas quando o grupo muda.
- Capturadores bloqueados tentam uma captura oportunista de emergencia antes de simplesmente aguardar.
- Capturadores rogue deixam de pular embarque por referencia incorreta ao capturavel mais proximo.
- Transporte para FireSupport conservador agora prioriza drop-off seguro em retaguarda, evitando avancar caminhao/supridor para a linha de frente.
- Suporte de fogo so usa fallback de avanco defensivo quando existe inimigo proximo do anchor, reduzindo deslocamento desnecessario.

## Dados e balanceamento

- Artilharia Campanha embarcada teve alcance minimo ajustado de 2 para 3.
- Bazooka passa a acionar reparo apenas em HP mais baixo (`repairTriggerHpBelow` de 5 para 2).
- Obus Leve passa para modo de compra IA `2`.
- Custos percentuais de servicos logisticos foram reduzidos:
  - Reabastecimento: 10 para 5.
  - Rearmamento: 25 para 10.
  - Reparos e Reparos Leves: 58 para 40.
- A cena `Battle Map Factory` foi atualizada junto ao pacote.

## Bloco tecnico curto

- Ajustados `AIController.Phases.cs`, `AIController.PlanEvaluator.cs`, `AIWorldSnapshot.cs` e `HexEvaluator.cs` para desempenho e reatividade mid-turn.
- Ajustados `AIController.Capturer.cs` e `AIController.Capturer.Embark.cs` para captura emergencial, capturadores rogue e filtros de transporte em reparo.
- Ajustado `AIController.Transportador.Courier.cs` para drop-off conservador de FireSupport rebocado.
- Ajustado `AIController.FireSupport.Helpers.cs` para conter fallback de avanco defensivo sem ameaca proxima.
- Atualizados assets de unidade, servicos logisticos e a cena de desenvolvimento.

## Resultado

Versao preparada como pacote `AI Refine IV`, melhorando a tomada de decisao da IA durante a fase, reduzindo custo de avaliacao e refinando parametros de suporte, transporte e logistica.
