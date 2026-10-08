Sim — **esse ciclo está se tornando o contrato central do jogo**, e dá para consolidá-lo mantendo o `TurnStateManager` atual. O tamanho da classe é um risco de manutenção, mas sozinho não exige refactor.

Investiguei a máquina na `main`; ela ainda aponta para **`3da75fd`**, então suas mudanças locais ainda não estão nessa análise.

Os cinco estágios ficam bem definidos assim:

| Estágio | Função |
|---|---|
| **Neutro** | Tabuleiro confirmado, pronto para receber uma ordem |
| **Unidade selecionada** | Preparar movimento e possibilidades |
| **Posição provisória** | Experimentar destino ou permanecer parado, usando informação já conhecida |
| **Ação desejada** | Escolher alvo/opção; ainda permitir cancelamento |
| **Executando** | Ordem comprometida; aplicar efeitos e concluir o ciclo |

O recálculo fecha esse ciclo. Encontrei correspondências concretas: `MoveuAndando`/`MoveuParado`, estados como `Mirando`, estados `*Executing`, restauração de custos no cancelamento e deltas de mundo aguardando o retorno a `Neutral`.

**Há um detalhe importante na ordem atual:** o código coloca a máquina em `Neutral`, dispara `OnCursorReturnedToNeutral` e **depois** chama `NotifyTurnStateReturnedToNeutral()`, que aplica o delta pendente de FOW.

Portanto, hoje **“voltou a Neutral” pode ser anunciado antes de “o mundo terminou de recalcular”**. Isso merece atenção porque replay, automação e ocupação escutam esse evento. Não constatei um bug em execução, mas são dois momentos diferentes que o nome do evento aproxima.

Outro ponto: os estados novos de execução de combate, captura, embarque etc. existem, porém `IsResetOnlyState()` ainda lista apenas algumas execuções administrativas. **A proteção diagnóstica da pilha ainda não cobre todas as execuções.**

Minha orientação para suas mudanças é manter três fronteiras explícitas:

- **Antes de confirmar:** tudo reversível, sem descoberta nova.
- **Depois de confirmar:** execução protegida contra novas ordens e cancelamento.
- **Depois de recalcular:** mundo consistente e disponível para a próxima ordem.

Isso fortalece exatamente a máquina que você está consolidando, com ajustes pontuais dentro da estrutura atual.