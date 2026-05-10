# Relatorio de Atualizacao - v2.0.21

## AI victory conditions

Esta versao fecha uma serie de casos extremos da IA em torno de fim de partida, suporte de fogo, rota terrestre real, reparo cercado e defesa emergencial de producao.

## Em uma frase

A IA agora para de jogar assim que a partida tem vencedor e toma decisoes mais coerentes quando esta encurralada, bloqueando fabrica, sem rota direta ou escolhendo alvos para fogo indireto.

## Condicao de vitoria e parada da IA

- O `AIController` passa a consultar `MatchController.HasVictoryWinner` antes de iniciar turno de IA.
- A mesma guarda foi adicionada entre fases, dentro dos waits de debug/shopping, antes/depois de batches e antes de passar turno.
- Se uma acao da IA dispara a vitoria, a coroutine e interrompida antes de compras, novas acoes ou troca de turno.
- O estado interno da IA e limpo: stage volta a zero, time atual volta para `Neutral`, previews de debug sao cancelados e pausas sao liberadas.
- O log passa a registrar o contexto da interrupcao, por exemplo `Partida encerrada (batch_end); IA interrompida.`

## Fire Support

- O score de alvo agora inclui a simulacao de combate da matriz de HP.
- O log de ataque de fire support mostra dano simulado, percentual, kill garantido e score da simulacao.
- A preferencia de alvo configurada no `UnitData` ficou mais forte.
- Alvos `Primary` e `Secondary` nao sao penalizados por estarem em range 1 quando a arma aceita range 1-2.
- Range maximo continua sendo uma preferencia de posicionamento, nao uma razao para ignorar alvo favorito valido.

## Rotas e progressao

- Fire support ganhou fallback de avanco quando o score defensivo prefere ficar parado, mas existe hex valido que melhora a rota real ate o objetivo.
- Capturador, pursuer, rogue, assault, repair e transportador passaram a considerar distancia terrestre real quando disponivel.
- Quando a rota real nao existe, a IA ainda usa distancia hex como fallback.
- Isso reduz casos em que montanha, bloqueio ou estrada lateral fazem a IA parecer sem caminho mesmo havendo uma progressao valida.

## Reparo e ultimo recurso

- Unidade em reparo cercada, fora de construcao aliada, agora tenta lutar se nao houver caminho livre de fuga.
- A regra funciona como ultimo recurso: primeiro tenta o fluxo normal de reparo; se nao ha movimento util, procura alvo valido e aceita combate.
- Isso evita a unidade ficar parada esperando morrer quando todos os hexes ao redor estao bloqueados por inimigos.

## Defesa emergencial de producao

- Se a IA tem apenas uma unidade viva em cima de uma fabrica, construcoes proprias em captura e dinheiro para uma defesa relevante, ela tenta liberar a fabrica.
- A unidade bloqueadora tenta sair atacando; se nao houver ataque, reposiciona para um hex livre.
- Na fase de compras, esse mesmo estado abre demanda defensiva e prioriza fire support ou blindado de assalto acessivel.
- O objetivo e permitir compra emergencial, como um obus medio, em vez de manter a unica unidade parada bloqueando a producao.

## Debug

- Logs novos deixam claro quando a IA libera producao por `emergencia_fabrica`.
- Logs de fire support agora explicitam `pref=Primary/Secondary/Tertiary`.
- Logs de rota indicam fallback de avanco com progresso por rota e por hex.

## Validacao

- Build validado com `dotnet build Assembly-CSharp.csproj --no-restore`.
- Resultado: `0 Erro(s)`.
- Permanecem apenas os warnings obsoletos do Unity ja existentes no projeto.
