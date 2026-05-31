# Refactor de Fusao (AI)

## Resumo

Esta versao estende a refatoracao de fusao para a IA em `repairMode`, alinhando o comportamento automatico com a nova regra de gameplay.

- a IA agora tenta fundir imediatamente quando o `PodeFundirSensor` ja valida a acao na posicao atual
- se a fusao nao estiver disponivel ainda, a IA pode marchar ate uma aproximacao valida para fundir ainda no mesmo turno
- a navegacao e a apresentacao de candidatos invalidos em gameplay foram limpas para espelhar o fluxo de `PodeEmbarcar`
- o `Unit Painter` do editor passou a realmente substituir a unidade ocupante do hex em modo de edicao

## Principais mudancas

- `AIPlayerController`:
  - `repairMode` agora resolve objetivo de fusao em duas fases:
  - fusao imediata pela leitura do sensor
  - aproximacao para candidato compativel com PM suficiente para entrar no hex e fundir no mesmo turno
  - quando encontra esse objetivo, a IA move a unidade em direcao ao candidato antes de executar a fusao automatica
- gameplay de fusao:
  - cursor e helper agora escondem candidatos invalidos
  - overlay cinza de hexagonos invalidos foi removido da substep de selecao
- tooling:
  - `Unit Painter` em `Tools > Units > Unit Painter` agora encontra ocupantes tambem no editor, sem depender de `UnitManager.AllActive` em play mode

## Efeito esperado

- unidades em manutencao deixam de tentar apenas fusao parada no lugar
- a IA passa a aproveitar fusoes proximas como atalho de reparo quando a acao cabe no turno atual
- a UX da fusao em gameplay fica mais limpa, mostrando apenas o que pode ser confirmado
- o `Replace Existing Unit` do painter passa a substituir de fato a unidade existente no hex
