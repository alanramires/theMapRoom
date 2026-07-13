# Regras obrigatórias do projeto

## Lei fundamental: ações são transacionais

**NADA NO JOGO É DEFINITIVO ATÉ O JOGADOR COMPROMETER A AÇÃO.**

Toda ação de tabuleiro começa em `CursorState.Neutral` e termina em `CursorState.Neutral`.
Tudo entre esses dois estados é provisório, cancelável e não pode alterar a verdade confirmada do tabuleiro.

Antes do compromisso é proibido atualizar de forma definitiva:

- Fog of War, células reveladas e visão por camada;
- detecção, stealth, contatos e memória/inteligência da IA;
- ocupação definitiva, revisões globais e caches confirmados;
- recursos, combustível, munição, HP, captura ou estado `HasActed`;
- qualquer informação que sobreviva a cancelamento ou rollback.

Durante a ação provisória, apenas apresentação temporária é permitida. Ela deve possuir restauração completa no cancelamento.

O compromisso ocorre no fluxo explícito de confirmação da ação. Depois dele, o estado retorna a `Neutral`; somente então o tabuleiro, FOW, sensores, caches, HUD definitivo e IA são recalculados a partir do estado confirmado.

Não use “fim da animação”, “unidade chegou à célula”, abertura de sensor ou entrada em submenu como sinal de compromisso.

Leia e preserve o contrato completo em `docs/arquitetura/acoes_transacionais.md` antes de alterar TurnState, movimento, sensores, FOW, combate, captura, transporte, supply, merge ou IA.

