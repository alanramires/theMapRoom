# v4.0.9 - AI Hard mode

Esta versão introduz um **Hard Mode configurável no AI Manager** e consolida a doutrina de invasão por múltiplos Rally Points. O objetivo é aumentar a pressão territorial e a qualidade das compras sem transformar os rallies em uma fila única de unidades.

## Hard Mode no AI Manager

- Novo toggle `Hard Mode` no `AIController`.
- Parâmetros normal/hard separados para:
  - proporção de elites sob pressão e em situação segura;
  - turnos máximos de poupança para elites;
  - reserva de manutenção durante a poupança;
  - composição mínima que libera a compra de elites.
- Limite próprio de unidades logísticas no Hard Mode.
- `UnitData` ganhou `bannedOnHardMode`, impedindo unidades marcadas de entrar em qualquer rota de compra da IA, inclusive emergência e fallback barato.

## Pressão territorial

- Hard Mode dobra os slots de capturador por setor, com teto de 6 para evitar demanda descontrolada.
- A distribuição ocorre em largura: primeiro a IA abre as frentes ainda vazias; somente depois preenche as vagas extras dos setores já iniciados.
- A base inimiga também recebe a demanda ampliada de capturadores.

## Compras e elites

- Configurações de proporção, poupança e reserva de elites foram centralizadas no `AIController`.
- Shopping Pressure usa os mesmos valores efetivos do modo ativo, evitando divergência entre HUD e planner.
- O gate de elite passa a respeitar a composição mínima configurada para Normal ou Hard.
- A demanda logística respeita o teto configurado do Hard Mode.

## Rally Points multi-centro

- Cada Rally Point controlado continua sendo um centro próprio de concentração; não existe mais drenagem para um rally principal.
- Unidades operacionais livres são atribuídas ao rally controlado mais próximo, formando os arcos ao redor da base inimiga.
- A prontidão soma a união das áreas dos rallies controlados, sem contar duas vezes unidades em áreas sobrepostas.
- Os requisitos combinados são distribuídos entre os centros apenas como pisos; unidades já acolhidas não são expulsas.
- O Go Green é liberado quando a massa combinada satisfaz os requisitos, quando há domínio macro ou quando a vantagem numérica global alcança pelo menos `2:1`.
- APCs continuam atendendo infantaria distante em cada centro.
- O resume do save preserva a montagem multi-centro e reaplica as atribuições sem consolidar os rallies.

## Defesa sem desmontar a invasão

- Rally em montagem não é invalidado como defesa obsoleta quando a ameaça local desaparece.
- Conversões para defesa agora exigem pisos mínimos de intel, reduzindo defesas-fantasma causadas por memória residual.
- O motivo defensivo fica visível no Shopping Pressure (`visível`, `captura parcial`, `intel`, dano ou presença).

## HUD e assets

- Shopping Pressure exibe os parâmetros efetivos de elite do modo ativo e o motivo dos objetivos defensivos.
- Sprites e arquivo-fonte dos semáforos de rally foram atualizados para a identidade visual desta revisão.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
- `Assembly-CSharp-Editor.csproj`: build sem erros.
- `git diff --check`: sem erros de whitespace.
