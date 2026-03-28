# Relatorio de Atualizacao - v1.4.12

## Em uma frase
A versao v1.4.12 refinou FoW, deteccao e "Alguem me ve" com alinhamento de LoS ao Pode Mirar, distancia aquatica consistente no Pode Enxergar e ajustes de leitura no relatorio de cenarios.

## O que isso trouxe na pratica
- `PodeDetectar` passou a seguir o mesmo traco robusto de LoS usado no `PodeMirar`, reduzindo falsos positivos em diagonais de hex.
- `PodeEnxergar` foi alinhado para reportar distancia com filtro aquatico em `Sub/Submerged`, coerente com o sensor.
- A classificacao de modo no card de visao basica foi corrigida para nao rotular casos sem especializacao como "Especializado/forcado".
- O pacote de debug FoW ficou mais previsivel para validar casos de peninsulas, montanhas e camadas virtuais.

## Principais melhorias
1. Alinhamento de LoS entre Pode Mirar e Pode Detectar
- `PodeDetectarSensor` deixou de priorizar traco por cube-line no calculo de intermediarios de LoS.
- O metodo passou a usar o mesmo caminho robusto do `PodeMirar` (lerp com supersampling e fronteira ambigua).
- Resultado percebido: menor chance de enxergar/detectar "por fresta diagonal" quando o relevo deveria bloquear.

2. Distancia aquatica no Pode Enxergar (debug)
- `PodeEnxergarSensorDebugWindow` recebeu filtro de passabilidade aquatica para cenarios `Submarine/Submerged` no mapa de distancia exibido.
- Terra passa a ser parede de percurso no BFS para esse dominio/camada, igual ao comportamento esperado do sensor.
- Resultado percebido: relatorio e leitura visual do FoW ficam coerentes com navegacao aquatica real.

3. Correcoes de leitura no card de cenario
- Cenarios sem especializacao agora aparecem corretamente como visao base no relatorio (`baseVisionOnly`), evitando confusao de interpretacao.
- Resultado percebido: diagnostico mais fiel ao que esta configurado na ficha da unidade.

4. Validacao de integracao AirHigh/LoS
- Revisado o encadeamento de leitura de `DPQAirHeightConfig` para `blockLoS` em deteccao/FoW.
- Confirmado uso no fluxo de visibilidade com modo range-only para `AirHigh` quando configurado.
- Resultado percebido: comportamento de LoS para camada aerea alta permanece conectado a configuracao.

## Bloco tecnico curto
- Scripts principais alterados:
  - `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
  - `Assets/Editor/PodeEnxergarSensorDebugWindow.cs`
- Documentacao:
  - `docs/relatorio_v1.4.12.md`

## Resultado
A v1.4.12 fecha um ajuste de consistencia entre sensores: LoS de deteccao alinhada ao padrao robusto do tiro, FoW subaquatico com distancia aquatica no diagnostico e relatorios mais claros para leitura de visao base vs especializacao.
