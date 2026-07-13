# v4.0.30a - Ajustes no FOW

## Foco

Atualização incremental dedicada à correção visual e à otimização do Fog of War, especialmente na visão especializada do EWACS e na apresentação de movimentos sobre áreas desconhecidas.

## Visão por camadas

- O modo `ALL` volta a reutilizar o cache da visão comum, evitando recalcular todas as camadas para unidades sem visão especializada.
- Somente unidades com `visionSpecializations` executam cálculos adicionais de camada.
- Camadas especializadas duplicadas de uma mesma unidade são ignoradas durante a união.
- Correção do refresh do modo `ALL` ao concluir movimentos, sem exigir troca manual pelo atalho `L`.
- A visão aérea deixa de permanecer visualmente presa à posição anterior da unidade.

## Cache especializado

- Inclusão de cache por unidade, posição, estado, alcance e camada especializada.
- O spawn ou movimento de outra unidade não invalida mais a visão especializada de um EWACS parado.
- Unidades destruídas, fundidas, desativadas ou embarcadas removem suas entradas especializadas.
- O cache é recalculado quando a própria unidade muda de posição, domínio, altitude ou configuração relevante.
- Limite defensivo de entradas para impedir acúmulo de estados antigos.

## Movimento e apresentação

- Unidade e Unit HUD podem permanecer temporariamente acima do FOW durante o movimento humano em área desconhecida.
- O rastro do caminho acompanha essa apresentação temporária no turno humano.
- Unidade, HUD, rastro e área válida de movimento continuam abaixo do FOW durante o turno da IA, sem revelar origem ou percurso oculto.
- Sorting layers originais são restauradas ao confirmar, cancelar ou reverter o movimento.

## Interface

- Correção gramatical das descrições de estruturas no `panel_helper`.
- Pontes passam a ser descritas naturalmente, como `Pontes sobre o Mar`, em vez de `Pontes na Mar`.
- Demais estruturas escolhem `no` ou `na` conforme o terreno.

## Estado

- Build de runtime verificado com `dotnet build Assembly-CSharp.csproj --no-restore`.
- Compilação concluída sem erros.
