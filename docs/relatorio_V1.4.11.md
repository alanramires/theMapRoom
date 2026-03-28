# Relatorio de Atualizacao - v1.4.11

## Em uma frase
A versao v1.4.11 consolidou ajustes de FoW, deteccao e diagnostico "Alguem me ve", alinhando LoS, distancia aquatica para submarinos e ferramentas de debug no menu Tools/FoW.

## O que isso trouxe na pratica
- O calculo de visibilidade/deteccao ficou coerente entre `Pode Mirar`, `Pode Detectar`, `Pode Enxergar` e `Alguem me ve`.
- Submarinos agora respeitam caminho aquatico no BFS (sem atravessar terra), tanto para detectar quanto para revelar FoW.
- O controle de LoS por especializacao ficou configuravel por dominio/camada via policy (`InheritGlobal`, `ForceOn`, `ForceOff`).
- Ferramentas de debug ganharam relatorios e linhas em cena mais claras para validar casos reais de mapa.

## Principais melhorias
1. LoS por especializacao (`losPolicy`)
- `UnitVisionException` recebeu `LosPolicy` por especializacao.
- `PodeDetectarSensor` passou a resolver LoS efetiva por camada detectada, em vez de depender apenas da flag global.
- Resultado percebido: sensores especializados podem herdar, forcar ou ignorar LoS sem quebrar os demais dominios.

2. Distancia aquatica para Sub/Submerged
- `BuildDistanceMapInto` passou a aceitar filtro de passabilidade.
- Para `Submarine/Submerged`, o BFS usa apenas celulas aquaticas (agua/submerso), tratando terra como parede de percurso.
- Aplicado em `CollectDetection`, `CanObserverObserveTarget` e `CollectVisibleCells`.
- Resultado percebido: submarino atras de peninsula deixa de "detectar/revelar atravessando terra", mas detecta/revela ao contornar por agua dentro do alcance.

3. Ajuste de traco LoS no Pode Detectar
- O traco intermediario de LoS foi alinhado ao mesmo metodo robusto do `PodeMirar` (lerp com supersampling e fronteira ambigua), evitando atalho por cube-line em diagonais.
- Resultado percebido: menos falsos positivos de visada em casos de diagonal entre hexes bloqueadores.

4. FoW e diagnosticos especializados
- `PodeEnxergarSensorDebugWindow` recebeu o mesmo refinamento de distancia aquatica para cenarios `Sub/Submerged`.
- `AlguemMeVeDebugWindow` foi criado para mostrar quem detecta o alvo, incluindo buckets, motivos e desenho de linha.
- Linhas validas/invalidas em cena foram padronizadas (verde para sucesso, vermelho para falha nos contextos aplicaveis).
- Resultado percebido: leitura rapida do motivo de sucesso/falha na deteccao e na revelacao de FoW.

5. Organizacao do menu de ferramentas
- Menu de debug foi reorganizado para `Tools/FoW`, centralizando:
  - `Pode Enxergar`
  - `Pode Detectar`
  - `Alguem me ve`
- Alias redundante foi removido para evitar duplicidade.

## Bloco tecnico curto
- Scripts principais alterados:
  - `Assets/Scripts/Sensors/PodeDetectarSensor.cs`
  - `Assets/Scripts/Units/UnitData.cs`
  - `Assets/Editor/PodeDetectarSensorDebugWindow.cs`
  - `Assets/Editor/PodeEnxergarSensorDebugWindow.cs`
  - `Assets/Editor/AlguemMeVeDebugWindow.cs`
- Dados e assets relacionados:
  - `Assets/DB/Character/Unit/Marinha/MA Submarino.asset`
  - ajustes de cenas e unidades usadas em validacao local de sensores/FoW.

## Resultado
A v1.4.11 fecha um pacote consistente para FoW e deteccao: LoS configuravel por especializacao, submarino com distancia aquatica correta e ferramentas de debug mais precisas para validar comportamento em mapa real.
