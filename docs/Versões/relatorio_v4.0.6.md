# v4.0.6 - AI Progressão de Verdade

Esta versão corrige a pontuação de progressão da AI (caminho *tool route*) para realmente recompensar **aproximar-se do objetivo**, em vez de premiar enrolar no turno 1. O efeito visível: as unidades passam a usar todos os pontos de movimento para avançar o máximo possível na rodada, aproveitando estradas, e largam o passageiro assim que entram no drop range. A ferramenta `Caminhos Válidos` foi reescrita para refletir a mesma métrica do runtime e ganhou inspeção interativa.

## O problema

A fórmula antiga media progresso por `SectorManager.HexDistance` (correto só para unidades aéreas) e tinha um viés estrutural: o termo dominante era `2T` (melhor posição depois de 2 turnos), que **empata** quando duas rotas chegam igual. O desempate caía em termos fracos que penalizavam gastar PM (`mv`) e desviar da reta (`line`) — então a rota que avançava **menos** no turno 1 vencia. Pior: `line` e `mv` eram contados **duas vezes** (no `toolScore` bruto e de novo no score do intent). Em jogo, a célula que terminava o turno 1 mais perto do alvo perdia para a que ficava parada perto da origem.

## Nova métrica de progressão

- Distância passou a ser **route-aware**: usa o template de movimento da `UnitData` (`SectorManager.TryGetLandMovementDistance`), com `HexDistance` apenas como fallback para aéreas / sem rota.
- **RAW (toolScore):** `2T×10 + 1T×6 − line×1`.
  - `1T` (avanço já no turno 1) virou **termo principal** (antes era desempate ×2).
  - `2T` mantém a viabilidade de chegar em 2 turnos.
  - `line` entra suave e **contado uma só vez**.
  - **`mv` removido** — gastar movimento avançando é o objetivo, não defeito.
- **FIN (score do intent):** `toolScore×1000 + road + dpq + threat + route`. Os termos de progresso **não são re-somados** (era contagem dupla) e `mv` saiu de vez; só `road`/`dpq`/`threat`/`route` entram à parte.

## Prudência por carga

- Novo `ResolveProgressionThreatScale(unit)` modula o peso de `threat` por unidade.
- **APC vazio que também faz Assalto** (`CanSatisfy(Transportador) && CanSatisfy(Assalto) && sem passageiros`) **ignora a prudência** (threat ×0): quando vazio ele atua como assalto e não precisa contornar perigo para buscar o próximo passageiro.
- **Logística não se aplica:** o caminhão de suprimentos é papel Logística (não satisfaz Assalto), então mantém prudência sempre, mesmo vazio.
- A modulação afeta **somente o termo `threat`**; todos os termos de progressão ficam intactos.

## Ferramenta `Tools > Transporte > Caminhos Válidos`

- A pontuação de progressão da janela passou a **espelhar o runtime** (route-aware + nova fórmula), sem mexer no cálculo dos Caminhos Válidos em si.
- **Breakdown por célula:** cada candidato mostra a contribuição de cada termo (`RAW` do toolScore e `FIN` do intent), permitindo ver exatamente qual termo decide entre duas rotas.
- **Coluna interativa à direita:** lista as rotas calculadas ordenadas pela nota. Ao clicar numa rota, o mapa destaca **verde = caminho do turno 1** e **azul = turno 2 provável** (a melhor continuação simulada na segunda passada).
- **Toggle "APC vazio (ignora prudência)":** zera o `threat` na visualização, refletindo o comportamento do combat-transport vazio (a unidade temporária da janela não carrega passageiro real, então é manual).

## Runtime espelhado

- `AIController.Progression.cs` (`TryScoreToolRouteProgression`) e `AIController.ProgressionSelector.cs` (`ScoreToolProgressionIntent`) receberam a nova fórmula e o `threatScale`.
- Atende todos os consumidores do caminho *tool route*: Transporte, Logística, Fire Support, Assalto (HQ breaker) e Repair.
- O scorer separado do capturador (`TryScoreTwoTurnProgression`, pesos 55/15/8/2) **não** foi alterado nesta versão — fica para um passo futuro (usar o alcance real de 2 turnos como gate de embarque, no lugar do limite de hexes do *short walk*).

## Validação

- `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj`: 0 erros (apenas warnings de obsoletos Unity já existentes).
- Confirmado em partida: APC carregado usou estrada, gastou os 6 PM para máxima aproximação no turno 1, detectou o drop range e fez move+desembarque no mesmo batch; candidatos com `threat` alto foram corretamente despriorizados.
