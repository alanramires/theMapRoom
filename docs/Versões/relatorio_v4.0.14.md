# v4.0.14 - Qualidade de Vida, Interface, Navegação com Teclado, Melhorias Parte 2

Esta versão amplia a adaptação da interface para mouse, teclado e dispositivos móveis. O foco foi reduzir cliques, transformar opções textuais em controles interativos e manter todos os atalhos alinhados com a máquina de estados oficial do turno.

## Mouse e ações contextuais

- O clique esquerdo passou a reproduzir a confirmação feita pelo cursor e pela tecla Enter nos estados compatíveis.
- Unidades têm prioridade sobre construções quando ambas ocupam o mesmo hex.
- Um segundo clique na unidade selecionada infere ações inequívocas:
  - conquista uma construção capturável;
  - confirma apenas o movimento quando não existe outra ação contextual;
  - entra diretamente na confirmação de ataque ao clicar em um inimigo válido.
- Durante a confirmação de ataque, clicar novamente no mesmo alvo executa o ataque.
- A inferência de ataque usa os resultados oficiais do `PodeMirar`; alvos inválidos ou hexes ambíguos não são escolhidos automaticamente.

## Shopping e compra de unidades

- O painel de compras ganhou áreas clicáveis para navegar, comprar e sair.
- A construção que abriu o shopping funciona como atalho de compra rápida da unidade visível.
- Clicar fora do painel e fora da construção fecha o shopping.
- As opções de compra também aparecem como botões no `panel_helper`, incluindo acesso direto a itens distantes da lista.
- A largura do `panel_dialog` é ampliada durante o shopping e reserva espaço proporcional para navegação, imagem e descrição.
- Contador, botões e textos respeitam a cor do time ativo.

## Panel Helper e navegação

- Opções pós-movimento foram convertidas em botões navegáveis com setas e comportamento circular.
- O foco sempre começa na primeira opção ao entrar em um novo contexto.
- O botão `CANCELAR` participa da navegação nos fluxos aplicáveis.
- Foram adicionados controles equivalentes para:
  - remoção de unidade;
  - Serviços do Comando;
  - seleção e confirmação de alvos;
  - salvar e carregar jogo.
- Enter executa o botão em foco e os cliques atualizam o foco antes de agir.
- Os botões gerados por script usam uma identidade visual comum baseada na cor do time.

## Mira e confirmação de ataque

- O `panel_helper` espelha as etapas oficiais de `PodeMirar`: seleção de alvo e confirmação do ataque.
- Quando existe apenas um alvo válido, o fluxo avança diretamente para a confirmação, preservando a regra original.
- A navegação entre alvos reutiliza o resolvedor oficial da mira e inclui o cancelamento no ciclo.
- A confirmação exibe sprite, nome e HP da unidade inimiga.
- O campo `LOCAL` prioriza o nome e o sprite da construção existente no hex; o terreno é usado como fallback.
- Confirmar e cancelar são botões navegáveis e clicáveis.

## Serviços do Comando

- A fila é ordenada por prioridade econômica, mantendo unidades embarcadas junto de seus pais.
- Preview e execução utilizam o mesmo plano comprometido, evitando divergência entre custo previsto e serviço realizado.
- Serviços parciais e unidades não atendidas recebem diferenciação visual.
- Alertas de saldo insuficiente usam cor de atenção.
- O painel oferece botões `EXECUTAR` e `CANCELAR`, com navegação circular por teclado.

## Save, load e menu

- Slots de salvar e carregar foram transformados em botões clicáveis.
- Setas navegam pelos slots, confirmação, retorno e cancelamento com wrap.
- O atalho de menu fecha ao clicar fora e pode forçar o retorno ao estado neutro quando necessário.
- O botão de passar rodada no `panel_remaining` replica o atalho do menu e permanece desabilitado fora do estado neutro.
- O painel de debug alterna entre aberto e fechado ao pressionar novamente sua tecla de atalho.
- O cursor de tooltip iniciado por F3 agora começa desligado.

## Validação

- `Assembly-CSharp.csproj`: compilação concluída sem erros.
- Fluxos de teclado, mouse e botões continuam delegando as ações à máquina de estados do turno.

