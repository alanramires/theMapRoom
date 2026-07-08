# v4.0.27c - Minor Fixes com inspect aliado

Esta versao melhora a inspecao de unidades aliadas que ja agiram e centraliza o controle dos atalhos de debug.

## Inspecao de aliado

- Confirmar sobre uma unidade aliada que ja agiu agora exibe sua HotZone potencial.
- O calculo reutiliza o servico e o cache de envelope de ameaca existentes.
- A HotZone de inspecao usa 55% da opacidade normal para nao sugerir uma acao ainda disponivel.
- O overlay acompanha o ciclo normal do helper: fecha ao mover o cursor, receber outra entrada ou atingir o timeout.
- O texto `HOTZONE: PROXIMO TURNO` foi removido do Panel Helper para manter a apresentacao limpa.

## Atalhos de debug

- `AI Debug Shortcuts Enabled` passa a atuar como flag master para F10, F11 e F12.
- A mesma flag controla a abertura do `Panel_Debug`.
- Os atalhos existentes (`'`, `;` e crase) deixam de abrir o painel quando a flag esta desligada.
- `Ctrl+D` foi adicionado como atalho alternativo para abrir e fechar o painel.
- Um painel que ja esteja aberto ainda pode ser fechado mesmo apos a flag ser desligada.

## Validacao

- `Assembly-CSharp.csproj`: build concluido com 0 erros.
