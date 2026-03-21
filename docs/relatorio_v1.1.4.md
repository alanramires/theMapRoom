# Relatorio de Atualizacao - v1.1.4

## Em uma frase
Atualizacao focada em paineis de HUD/feedback em campo e melhoria de mensagens de estado para reduzir dependencia de log.

## O que isso trouxe na pratica
- Fluxo de turno e acoes com mais feedback visual direto no jogo (sem depender do console).
- Paineis de suporte para unidade, helper e hotkeys com comportamento mais consistente.
- Melhor leitura de erro/estado em acoes como compra, mira, servico de comando, ciclo de unidades, save/load.

## Principais melhorias
1. Painel de unidade como canal de status curto
- `panel_unit` passou a exibir mensagens transientes para eventos de campo e erros comuns.
- Resultado: jogador recebe contexto imediato para a acao atual (`Hex ocupado`, `Sem dinheiro suficiente`, `Game saved`, `Game loaded`, etc.).

2. Fluxos de confirmacao e atalhos
- Confirmacao de fim de turno em `R` padronizada e exibida no painel da unidade.
- Resultado: reduz erro operacional de encerrar turno sem intencao e melhora legibilidade do estado de confirmacao.

3. HUD auxiliar e consistencia de UI
- Evolucao de `panel_helper`, controle de visibilidade de paineis e ajustes de cor/ancoragem para leitura em campo.
- Resultado: interface mais previsivel durante sensores, shopping, disembark e debug tatico.

## Regras importantes (opcional)
- `Mensagens informativas`: sao curtas e somem automaticamente apos alguns segundos.
- `Confirmacoes criticas`: permanecem no painel ate confirmar/cancelar (ex.: fim de turno).
- `SFX de erro`: agora podem acionar mensagem curta no `panel_unit` para manter feedback textual.

## Bloco tecnico curto
- Sistemas-chave atualizados: `CursorController`, `TurnStateManager` (modulos de scanner/comando/shopping), `PanelUnitController`, `PanelHelperController`, `PanelMoneyController`, `SaveGameManager`.
- Inclusao e integracao de novos controladores UI para helper, unidade, turno e visibilidade de paineis.
- Mudancas em prefabs/cena e ajustes de dados/servicos para suportar o novo comportamento em campo.

## Resultado
- Versao deixa o gameplay mais legivel durante teste de campo, com menos friccao de controle e feedback mais claro para cada acao.
