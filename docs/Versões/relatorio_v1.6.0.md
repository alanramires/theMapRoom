# v1.6.0 - Canhões, Tanques e Soldados por AI

## Escopo

Esta versão consolida a evolução da IA terrestre para unidades de linha:

- soldados
- tanques
- canhões / artilharia terrestre

O foco desta etapa foi melhorar coerência tática, leitura do planner e consistência entre planejamento, execução, save/load e debug.

## Principais mudanças

- Introdução do `SectorManager` como intel pública por setor, derivada das construções ativas.
- Planejamento por catálogo fixo de planos por setor, com estados ativos/inativos/concluídos.
- Ajustes no save/load do planner e das construções para preservar contexto correto dos setores.
- Revisão do cálculo de progresso dos planos com base em controle efetivo de captura.
- Exposição de `risk` e `criteria` no `AI Manager` e em `Tools > AI > AI Planner`.
- Revisão do comportamento terrestre por classificação automática:
  - `Combatente`
  - `Artilheiro`
  - `Hibrido`
  - `Civil`

## Comportamento tático

- Combatentes passaram a buscar melhor DPQ quando entram em combate.
- Artilheiros priorizam tiro parado; sem tiro válido, caem corretamente para reposicionamento.
- Reposicionamento de artilharia passou a usar faixa real das armas, sem hardcode.
- Capturadores agora podem promover o turno para branch de combate antes do movimento quando a captura não pode ser concluída e existe ataque viável no turno.
- Correção para preservar o alvo escolhido pelo planner, evitando overwrite indevido pelo primeiro alvo retornado pelo sensor.

## Planner

- Menor dependência de papéis hardcoded de suporte.
- Distribuição de escoltas passou a acontecer em ondas entre planos ativos, evitando concentração excessiva no plano de maior risco.
- O planner setorial foi simplificado para refletir melhor `Capture + Escort`, enquanto o comportamento real da unidade continua vindo da classificação de combate.

## Debug e inspeção

- `AI Manager` e `AI Planner` agora exibem:
  - planos fixos
  - planos ativos
  - planos inativos
- `risk`, `criteria`, progresso e assignments ficaram mais legíveis para análise de turno.

## Estado do projeto

Esta versão fecha uma etapa importante da IA terrestre para:

- soldados
- tanques
- canhões

Próximas frentes previstas:

- shopping list
- remoção de hardcodes restantes de suprimentos, fusão e retorno para reparo
- embarque e desembarque
- sensores restantes
- aeronáutica
- marinha
