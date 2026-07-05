# v4.0.16 - AI Rapida

Esta versão adiciona dois modos de apresentação para a IA, preservando a mesma execução oficial dos batches e permitindo escolher entre velocidade máxima e acompanhamento visual das decisões.

## Configuração

- O `AIController` recebeu a flag `IA Rapida` no Inspector.
- Com a flag ligada, a IA mantém a execução compacta dos batches.
- Com a flag desligada, a IA apresenta ao jogador as etapas intermediárias da interface.
- O modo selecionado é encaminhado ao `ReplayManager` durante a execução ao vivo da IA.

## Apresentação da IA normal

- O cursor e as seleções respeitam os delays visuais configurados para replay e automação.
- O `panel_helper` consegue exibir o estado de opções antes da escolha do sensor.
- Quando existem vários sensores, o foco navega pelo menu até a ação escolhida pela IA.
- A navegação utiliza o mesmo foco circular, cores e feedback sonoro disponíveis ao jogador.
- `Apenas Mover` também é alcançado pela navegação oficial do menu.
- Embarque e desembarque apresentam separadamente seleção, confirmação e inclusão na fila.

## Execução da IA rápida

- A execução rápida não aguarda delays de apresentação ou navegação visual.
- O Serviço do Comando utiliza o fluxo direto equivalente ao atalho `X`.
- Passar a vez utiliza o fluxo direto equivalente ao atalho `R` confirmado.
- Esses atalhos evitam corridas de seleção causadas pela abertura e navegação instantânea do menu.
- A execução continua validada pela máquina de estados oficial do turno.

## Robustez

- O estado temporário do modo rápido é restaurado ao final de cada batch, inclusive quando a execução é interrompida.
- O modo de replay existente permanece separado da apresentação ao vivo da IA.
- As rotinas automatizadas de embarque e desembarque mantêm caminhos compactos para a IA rápida e caminhos em etapas para a IA normal.

## Validação

- `Assembly-CSharp.csproj`: compilação concluída sem erros.
- `git diff --check` restrito aos scripts alterados: sem erros de whitespace.

