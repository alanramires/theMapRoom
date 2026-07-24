# v4.1.14 - Debug e refactor na AI para mapas sem setores

Esta versão consolida duas frentes: a migração final dos comandos do painel de debug para participantes identificados por slot e a adaptação da IA para mapas pequenos, experimentais ou sem uma rede tradicional de setores e rally points.

## Debug por slot

- `spawn <unidade>` cria a unidade para o slot ativo.
- `spawn:<slot> <unidade>` cria a unidade para o slot informado.
- `ai spawn <unidade>` usa o slot ativo.
- `set active team <slot>` mantém o nome legado do comando, mas troca o participante ativo pelo slot sem avançar o turno.
- `wake all units` acorda somente as unidades do slot ativo.
- `set money <valor>` opera sobre o slot ativo.
- `set money:<slot> <valor>` opera sobre o slot explícito.
- `set owner <slot>` e `set construction team <slot>` passam a definir ownership por slot; `-1` deixa a construção neutra.
- Regras de venda `original` e `first` continuam recebendo owner slot.

## Spawn seguro por participante

- `UnitSpawner` ganhou uma entrada explícita de spawn por `PlayerSlotId`.
- A cor visual da unidade é derivada da configuração do slot.
- A unidade recebe o `SlotIndex` exato mesmo quando dois participantes compartilham o mesmo `TeamId`.
- A validação de ocupação no modo Total War considera o slot do participante.
- Construções e unidades na mesma camada continuam bloqueando spawns inválidos.

## IA em mapas sem rally

- O planner distingue “o rally existe, mas ainda não foi conquistado” de “não existe rally aplicável para este slot”.
- Quando não existe rally designado, o gate de Go Green é considerado inaplicável em vez de bloquear a invasão para sempre.
- Objetivos contra bases inimigas deixam de ser criados e dissolvidos repetidamente em mapas sem rally.
- Mapas tradicionais com rally preservam o gate de montagem e o comportamento calibrado existente.
- A verificação é feita por slot, permitindo que um participante tenha rally e outro não no mesmo mapa.

## Eixos de invasão sem setores tradicionais

- Quando o slot não possui rally, os QGs inimigos passam a funcionar como ápices do leque de invasão.
- Um eixo é criado para cada QG inimigo.
- Em mapas contendo somente QGs, o corredor pode ser vazio e a frente passa a ser a própria base inimiga.
- Em mapas com construções intermediárias, elas são distribuídas geometricamente pelos eixos.
- O fallback só é aplicado quando não há rally; mapas já calibrados não misturam eixos de rally com eixos sintéticos de base.
- Empates geométricos entre QGs são resolvidos deterministicamente pelo menor `SlotIndex`, eliminando dependência da ordem da cena.

## Núcleo operacional e catálogo disponível

- Metas de infantaria, assalto e artilharia são ajustadas quando nenhum produtor do mapa oferece uma unidade capaz de cumprir determinado componente.
- Gate e cálculo de maturidade usam o mesmo resolvedor de composição.
- Um componente impossível deixa de bloquear permanentemente compras de elite.
- O cálculo usa o papel efetivo de composição da unidade, evitando considerar como solução uma unidade que não fecha o requisito real.

## Gate suave de núcleo

- Foi adicionada a opção experimental `softCoreGate`.
- Quando ativa, a maturidade do núcleo vira um peso contínuo no score de unidades elite.
- O comportamento evita que o gate rígido force a IA a comprar a opção comum mais fraca apenas para destravar a composição.
- O piso financeiro continua rígido: a suavização doutrinária não permite compras que o caixa não suporta.
- A capacidade foi exposta em `AIPresetData` e no gerador de presets.

## Conteúdo de teste

- Foram incluídos ativos e uma cena de desenvolvimento “Quadrado” para validar mapas compactos e sem a estrutura tradicional de setores.
- Catálogos de unidade, construção e estrutura receberam as entradas correspondentes.
- Dados de autonomia e aeronáutica foram atualizados para o cenário de teste.

## Contrato transacional

Os comandos de debug são mutações explícitas e continuam respeitando as barreiras de estado já existentes. O refactor da IA altera planejamento, gates e scoring, sem antecipar efeitos definitivos de movimento, FOW, sensores, combate, captura, recursos ou `HasActed` durante estados provisórios.

## Validação

- Dependências dos projetos C# restauradas após limpeza da pasta temporária.
- `Assembly-CSharp.csproj` compilado com sucesso: zero erros.
- `Assembly-CSharp-Editor.csproj` compilado com sucesso: zero erros.
- Permanecem 248 avisos não bloqueantes no runtime.
- Permanecem 143 avisos não bloqueantes no assembly de Editor.

## Verificações recomendadas

- Testar `spawn` e `spawn:<slot>` com dois participantes da mesma cor.
- Alternar o slot ativo usando `set active team`.
- Executar IA em mapa contendo somente QGs.
- Executar IA em mapa com construções intermediárias, mas sem rally.
- Comparar mapas com rally para confirmar que o gate Go Green continua inalterado.
- Testar o gate suave ligado e desligado usando o mesmo seed, catálogo e situação econômica.
