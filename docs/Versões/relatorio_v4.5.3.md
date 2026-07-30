# v4.5.3 — Refactor de Mudança de camada 3/5

## Objetivo

Criar a fundação comum das transições de camada e eliminar implementações
paralelas da pergunta estrutural: “esta unidade cabe nesta camada deste hex?”.

## LayerTransitionRules

- Criado `LayerTransitionRules` em `Assets/Scripts/Units/Rules`.
- A fundação valida suporte da unidade à camada de destino.
- Terreno, estrutura e construção são resolvidos no hex efetivo da consulta.
- Modos principais e modos adicionais continuam válidos.
- Regras de skills obrigatórias e bloqueadas foram centralizadas.
- Regras de terreno, estrutura combinada com terreno e construção que forçam
  outra camada continuam prevalecendo.
- A ocupação da banda de destino é validada pela mesma consulta.
- A resolução de terreno por tilemap principal ou tilemaps irmãos também passou
  a ser compartilhada.

## Separação de responsabilidades

- `LayerTransitionRules` responde apenas se a camada de destino é estruturalmente
  utilizável naquele hex.
- Sensores continuam responsáveis pelo momento da operação: camada atual,
  combustível, disparo, dano, detecção, locks, exposição e demais restrições
  operacionais.
- A fundação é uma consulta pura e não move unidades nem altera estado runtime.

## Consumidores migrados

- Menu genérico de mudança de camada do `TurnStateManager`.
- Suprimento logístico e serviço de comando.
- Merge e consultas de alcance.
- Preferência automática e transições forçadas de camada.
- Emersão necessária para combate, movimento e início de turno.
- `PodeEmergirSensor`, `PodeSubmergirSensor`, `PodeSuprirSensor` e
  `ServicoDoComandoSensor`.
- Resolução da camada de pouso de hidroaviões.
- Ferramentas `Pode Mudar de Altitude` e `Pode Fundir`.

## Remoção das cópias

- Removido `TurnStateManager.CanUseLayerModeAtCurrentCell`.
- Removida a implementação paralela de `PodePousarWindow`.
- Removidos helpers especializados de terreno, estrutura, construção, skills e
  ocupação dos sensores naval e logístico.
- Removida a cópia usada pela preferência forçada de camada.
- Aproximadamente 1.565 linhas de validação duplicada foram eliminadas.
- As janelas e sensores migrados agora chamam diretamente a fundação comum.

## Arquitetura transacional

- A fundação somente consulta o estado confirmado ou o contexto hipotético
  fornecido pelo consumidor.
- Nenhuma consulta atualiza FOW, detecção, ocupação confirmada, recursos, camada
  ou caches globais.
- Transições provisórias continuam restauráveis no cancelamento.
- O compromisso definitivo permanece vinculado à confirmação explícita e ao
  retorno a `CursorState.Neutral`.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore -v:minimal -clp:ErrorsOnly`
- `git diff --check`
- Resultado: builds concluídos com 0 erros e diff sem erros de whitespace.
- Implementação atual do refactor: `3/5`.
