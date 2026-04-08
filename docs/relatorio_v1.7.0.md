# Refactor de Fusao

## Resumo

Esta versao consolida a refatoracao do fluxo de fusao no sensor, no gameplay e no tooling de debug.

- `PodeFundirSensor` passou a expor candidatos validos e invalidos com motivo, custo e movimento restante do receptor.
- o fluxo de gameplay deixou de depender de "processar fila": confirmar o candidato agora executa a fusao imediatamente.
- a animacao da fusao foi invertida para o comportamento correto: a unidade selecionada anda ate o candidato.
- o tool `Tools > Sensors > Pode Fundir` foi alinhado com a regra real de gameplay e com o comportamento do `PodeEmbarcar`.
- auditoria de morte/fusao foi estabilizada para priorizar ids tecnicos em vez de nomes dinamicos.

## Principais mudancas

- sensor de fusao com colecao formal de:
  - validos
  - invalidos
  - motivo
  - custo para alcancar o alvo
  - movimento restante considerado
- janela de debug do `PodeFundir`:
  - sincroniza `UnitManager.AllActive` no editor antes da simulacao
  - mostra candidatos validos e invalidos
  - permite movimento restante simulado do receptor
  - deixa explicito que o movimento relevante e o da unidade selecionada
  - restringe a fila de debug a 1 candidato
- gameplay de fusao:
  - confirmacao do candidato dispara execucao imediata
  - remocao pratica da etapa de `Process Queue` no helper/fluxo da fusao
  - preview/linha da fusao sai da unidade selecionada e aponta para o candidato
  - execucao move o receptor ate o hex do candidato e conclui a unidade resultante nesse hex

## Auditoria e rastreio

- `deadByUnit` deixou de depender de nome amigavel e passou a priorizar:
  - `UnitId`
  - fallback `instance:<InstanceId>`
- morte do doador na fusao agora registra a unidade receptora em `deadByUnit`
- `UpdateDynamicName()` foi neutralizado para impedir renomeacao automatica por estados transitorios como:
  - `_X`
  - `_D`

## Efeito esperado

- fusao fica previsivel no scanner, no helper e na execucao
- debug de `PodeFundir` passa a refletir a regra real de movimento
- logs futuros de morte, fusao e auditoria ficam mais rastreaveis por id estavel
