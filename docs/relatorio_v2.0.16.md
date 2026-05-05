# Relatorio de Atualizacao - v2.0.16

## AI Tank

Esta versao fecha uma rodada de ajustes no comportamento de tanques, breakers e batedores de assalto, com foco em compra de elite, pressao ao HQ inimigo e uso correto de edificios ja conquistados.

## Em uma frase

A IA passa a tratar tanque elite como compra valida mesmo com a composicao cheia, avanca melhor com unidades de assalto e evita bloquear tanque em predio aliado que ja esta totalmente conquistado.

## O que isso trouxe na pratica

- A compra de assalto elite pode exceder o limite normal quando a elite esta liberada e existe cash suficiente.
- Breakers de assalto passam a pressionar HQ/construcoes inimigas com prioridade maior do que ficar presos em ameacas locais ou em celulas ocupadas por aliados.
- Movimento de rogue/assalto passou a priorizar progresso real ate o alvo antes de desempates por ameaca ou DPQ.
- Batedores de assalto saem da inercia quando ainda estao fora da zona designada.
- Inimigos visiveis perto da rota de viagem tambem entram como ameacas de ataque para batedores.
- Tanques e batedores podem ocupar predio aliado ja conquistado; a reserva do predio continua valendo quando a captura ainda nao esta cheia.

## Principais melhorias

1. Compra de tanque elite
- O planner de compras agora identifica uma unidade de assalto elite disponivel e libera uma compra excedente quando a composicao minima ja foi atingida.
- A reserva economica para elite considera custo, renda do turno seguinte e colchao percentual configuravel.
- Em ameaca direta contra a base, a IA ainda pode priorizar compra defensiva imediata.

2. Pressao de assalto ao HQ
- O alvo de pressao de breakers passou a preferir HQ inimigo e construcoes inimigas antes de inimigos visiveis soltos.
- A escolha de movimento exclui ficar parado como candidato normal de pressao.
- O desempate favorece progresso ate o alvo, avanço na linha, maior uso de caminho disponivel, menor ameaca e DPQ apenas depois desses criterios.

3. Batedores de assalto
- Batedores designados para um setor nao ficam parados fora da zona quando existe movimento que aproxima do setor.
- A avaliacao de patrulha agora pode considerar inimigos proximos da rota, nao apenas inimigos ja dentro da zona final.
- Isso permite que o batedor escolha atirar quando a viagem atravessa uma area com inimigo visivel e valido.

4. Flags de DPQ respeitadas
- `preferMoveOnBestDPQ` controla DPQ de movimento.
- `prioritizeDpqAtBattle` controla DPQ de batalha apenas quando existe alvo de combate.
- `playConservative=false` continua removendo a penalidade de seguranca defensiva.
- Capturadores em avanco sem combate passam a preferir progresso ate o objetivo antes de bonus laterais de coesao/DPQ.

5. Tanque em predio conquistado
- A celula ancora do setor so fica reservada quando o predio capturavel ainda nao esta totalmente sob controle aliado.
- Se o setor ja esta conquistado, tanque/batedor pode ocupar o predio sem bloquear reparo ou captura pendente.

## Bloco tecnico curto

- Ajustado `AIShoppingPlanner.cs` para compra excedente de assalto elite, reserva economica e priorizacao defensiva de base.
- Ajustado `AIController.Assault.cs` para alvo de pressao, movimento de breaker e inclusao de ameacas ao longo da rota de batedores.
- Ajustado `AIController.Assault.Defender.cs` para patrulha de batedores, ameacas de viagem e liberacao de predio aliado totalmente conquistado.
- Ajustado `HexEvaluator.cs` para separar DPQ de movimento, DPQ de batalha e criterio de progresso em `CaptureAdvance`.

## Resultado

- Versao preparada como pacote `AI Tank`, focada em tornar tanques e assaltantes mais decididos, sem perder as regras de captura/reparo dos edificios.
