# v4.0.29 - Tutorial para novatos em andamento

## Foco

Checkpoint de desenvolvimento do tutorial para novos jogadores, com ajustes de fluxo, UI e validacoes de atalho para deixar as primeiras cenas mais guiadas e menos propensas a entrada acidental.

## Tutorial

- Avanco da estrutura do tutorial inicial e cenas de aprendizado.
- Ajustes no `Panel_Tutorial` e no fluxo de tarefas para acompanhar objetivos progressivos.
- Inclusao/ajuste do dialogo tutorial separado do painel principal.
- Refinamento da cena "Historia 1 - Aprendendo a Atirar" e material de apoio em `docs/tutorial/cena1.md`.
- Bloqueios didaticos para comandos ainda nao liberados durante o tutorial, com feedback pelo dialogo tutorial.

## UI e atalhos

- Ajuste da navegacao da Tela de Entrada para manter a ordem visual dos botoes.
- Revisao do atalho `R` para passar turno rapidamente, permitindo passar pela confirmacao do helper quando a partida exigir.
- Cancelamento por clique direito integrado aos prompts de save/load abertos pelos atalhos `I` e `O`.
- Conferencia do comportamento de clique direito nos helpers de Servico do Comando e Destruir Unidade.
- Melhorias de texto no helper de inspecao, priorizando nome runtime da unidade quando disponivel.

## Gameplay

- Ajuste do preview de ameaca para unidades hibridas, separando melhor movimento com ataque e ataque parado durante a inspecao.
- Revisoes no Servico do Comando e no fluxo de Destruir Unidade para respeitar bloqueios de tutorial.
- Ajustes de painel e estados auxiliares para manter confirmacoes e cancelamentos consistentes.

## Estado

- Tutorial para novatos segue em andamento.
- Build C# verificado durante o ciclo com `dotnet build Assembly-CSharp.csproj -v:q`, sem erros.
