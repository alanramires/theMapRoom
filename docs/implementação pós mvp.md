# Implementação pós-MVP

Mudanças de **projeto de jogo** que ficam conscientemente para depois do MVP.
Não são bugs nem pendências: são regras novas que alteram como o jogo inteiro se
joga e, por isso, a IA inteira. Cada uma entra como versão **X**.

---

## Zona de Controle (ZoC) — o "hex de parada"

*Levantado em 2026-10-03.*

### O problema

Hoje uma unidade terrestre cercada num "formigueiro" que encontra uma brecha
**escapa com o movimento inteiro**. Não existe "segurar" o inimigo, só bloquear
hexes fisicamente. Por isso a linha de frente não significa nada além de ocupação.

O jogo não tem ZoC de propósito: a referência é o Game Boy Wars de 1990, que não
tinha. Advance Wars também não tem.

### A proposta

| decisão | escolha | por quê |
|---|---|---|
| sabor | **hex de parada**: entrar num hex vizinho de inimigo encerra o movimento; sair dele no turno seguinte é livre | o mais legível dos três clássicos (os outros: custo extra para sair; proibido ir de ZoC para ZoC). O jogador entende em um turno |
| quem projeta | só inimigo **visível no snapshot confirmado** do lado que se move | ZoC de inimigo escondido encurtaria a área pintada e viraria **oráculo de névoa** ("por que não passo dali?"). Também respeita o contrato transacional: movimento provisório não revela nada. Emboscada, se um dia existir, é outra mecânica |
| domínio | **terra contra terra** no começo | avião não para por causa de tanque, e vice-versa |
| quem não projeta | unidade sem arma (caminhão, APC vazio) — a decidir | uma zona que não ameaça nada é estranha |
| exceção | **por habilidade, do lado de quem PROJETA**: "minha ZoC não segura quem tiver estas habilidades" | a habilidade é chave, não poder (CLAUDE.md). Nada de `ignoraZoC` na ficha de quem anda. Renomear a etiqueta continua de graça |
| ativação | **regra de partida** (gaveta *Partida*, ao lado de névoa e Total War, no `GameSetupPreset`): "Clássico" sem ZoC, "Moderno" com | não trai a referência de 1990 e dá profundidade a quem quiser |

### "Só muda no pathfinding" — verdade, com quatro pegadinhas

Conceitualmente a regra mora num lugar só (`UnitMovementPathRules`), e todo
consumidor herda: área pintada, envelope Tático/Operacional, rotas de transporte,
faixa de entrega, handoff do capturador. Na prática "o pathfinding" é mais de uma
coisa.

**Medido no código em 2026-10-03** (fora do `AI_Legacy~`):

| porta de entrada | chamadas | arquivos | o que muda |
|---|---|---|---|
| `CalcularCaminhosValidos` (busca de ida, área pintada) | 74 | 48 | herda direto: é aqui que a parada entra |
| `CalculateMovementCostMap` (busca **reversa**, "quanto custa chegar até o alvo") | 22 | 14 | **cuidado**: o hex de parada é assimétrico. No reverso, um caminho que passa colado no inimigo e segue em frente não existe. Errar aqui = a IA planeja rotas que o jogo não deixa andar |
| `CalculateTurnChainedCostMap` (Operacional encadeado) | 3 | — | mesma assimetria, com virada de turno no meio |
| `CalculateAutonomyCostForPath` (custo de um caminho pronto) | 14 | 11 | não muda: recebe o caminho já decidido |
| `TryGetEnterCellCost` | 13 | — | não muda: custo de UMA célula |
| `HasTraversableRouteIgnoringUnits` | 4 | — | por definição ignora unidades; **fica sem ZoC**, e quem o usa precisa saber disso |
| `UnitReachEnvelopeService.*` | 17 | — | herda, desde que as buscas por baixo herdem |
| `SectorManager.HexDistance` | **506** (484 na IA) | — | a grande maioria é geometria legítima (alcance de arma, raio, setor). Só 2 linhas comparam direto com MP, mas o uso como **aproximação de custo** de rota se espalha em decisões (CLAUDE.md já marca isso como legado). Esses pontos **não herdam** a ZoC e passam a discordar do movimento real. Hoje funcionam porque distância e rota quase coincidem; com ZoC, deixam de coincidir |

**Caches que passam a depender da posição e da VISIBILIDADE dos inimigos:**

| cache | onde | risco |
|---|---|---|
| `movementRangeCacheByUnit` (chave inclui `globalBoardRevision`) | `TurnStateManager.Range.cs` | se um inimigo for **revelado** sem bumpar a revisão do tabuleiro, a área pintada fica velha. É o modo de falha do prédio escuro (CLAUDE.md, "O mundo só recalcula no Neutral") |
| `MovementQueryCache` | `UnitMovementPathRules.cs` | por consulta; ok se a ZoC for lida de fora dele |
| `custoPorQuadro` | `FaixaDeEntregaService.cs` | por quadro; ok |
| `routeEnterCostCache` | `SectorManager.cs` | distância terrestre; precisa decidir se ignora ZoC (geografia) ou não |
| bake do `BoardTopologyIndex` | `Hex/Core` | é topologia de terreno: **não** deve conter ZoC |

**O pathfinding ganha ponto de vista.** "Até onde eu chego" passa a depender de
quem olha (o lado ativo, com a névoa dele). Hoje a busca não recebe time
observador. A IA pergunta com a visão dela, e o jogador com a dele. Parâmetro novo
nas buscas de ida e reversa.

### Plano sugerido

1. **Serviço:** a regra entra nas buscas de ida e reversa, ligada pela regra de
   partida, com perspectiva explícita. Teste de paridade: ida e reversa concordam
   em todo hex.
2. **Jogador humano primeiro:** área pintada, preview e Ação Direta. Jogar em Play
   e ver se a linha de frente "pega".
3. **Caches:** a chave do range cache passa a incluir a revisão de visibilidade do
   lado ativo.
4. **IA:** auditar os `HexDistance` usados como custo de rota (não os
   geométricos) e regressão completa em Play: capturador, transporte, envelope,
   handoff.
5. **Exceções por habilidade** nas fichas, por último, quando a regra base já
   estiver validada.

### Em aberto

- Unidade sem arma projeta ZoC?
- Unidade **dentro de construção** projeta ZoC normalmente?
- ZoC naval (navio contra navio) entra junto ou depois?
- Quem começa o turno já colado no inimigo sai livre (proposta: sim) — e se sair
  para outro hex também colado, para de novo (proposta: sim, é o próprio hex de
  parada).
