# v4.0.1 - AI Eixo Plan

Versão de calibração em cima do shopping por papéis (v4.0.0). Traz a primeira materialização do conceito de **Eixo de Invasão** (visualização no SectorManager), reintroduz reservas e travas de ritmo que a refatoração havia derrubado, conserta bugs do planner e do HUD, e adiciona ferramentas de inspeção (tela de pressão de shopping e export de jogadas).

## Eixo de Invasão (visualização)

- Novo botão **"Desenhar eixos"** no editor do `SectorManager`, ao lado de "Desenhar todas as linhas".
- Cada rally configurado vira um eixo; o nº de eixos = nº de rallys.
- **Leque angular**: cada setor é atribuído ao rally cuja direção (HQ→rally) é a mais próxima em ângulo da direção HQ→setor, calculado em world space (sem distorção do hex). Fatias disjuntas, sem cruzamento.
- Desenha `HQ → setores intermediários da fatia (por distância) → rally`.
- **Filtro por time** (Todos/Green/Red/…) para não misturar os eixos dos dois lados.
- Fica como base do "master plan" acima da camada de objetivos; ligar as regras da onda a essas fatias é trabalho futuro.

## Demanda de transporte por necessidade de carona

- O `GroundCapture` deixou de pedir transporte por "setor longe" e passou a pedir por **carona real**: capturador a ≥ 7 hexes (2 turnos) por caminhos válidos do objetivo, não embarcado.
- **Aposta futura limitada**: vaga de capturador vazia conta só em frente comprometida (Pursuing/Capturing ou já com capturador indo).
- Gate de massa mínima de capturadores (`MinCapturerMassForSupport`, default 4) antes de liberar transporte e fire support de composição — forma a base primeiro.
- O slot de Transportador deixou de ser removido prematuramente pelo `ReleaseOffensiveSupportWithoutCapturer` (transporte é aposta de turno seguinte).

## Reserva e ritmo de economia

- Reintroduzida a **reserva de elite** entre turnos no shopping por papéis (`EliteSaveMaxTurns`): segura caixa para um alvo elite alcançável em poucos turnos, em vez de gastar tudo guloso.
- Gate de elite por alcançabilidade substitui o piso fixo de caixa que, com renda baixa, nunca deixava o alvo aparecer.

## Planner

- **Reserva de âncora ciente de excedente**: deixou de reservar todos os capturadores por uma única vaga de âncora; só bloqueia os outros objetivos quando os capturadores são escassos. Resolve unidades viradas rogue.
- **Gate de distância na cascata** (`MaxCascadeBridgeDistance = 3`): a ponte só cobre o vizinho dentro do alcance de captura; acima disso o vizinho recebe a própria unidade.
- Transporte terrestre x aéreo: setores que preferem veículo recebem APC pelo GroundCapture; air-pref ficam no airlift, evitando demanda dupla.

## Papéis

- Removidos os papéis híbridos `LogisticaMovel` e `LogisticaEstoque`.
- `UnitRoleCompatibility.CanSatisfy(UnitData, role)` passou a ser **ciente dos flags**: quem tem `isTransporter` satisfaz Transportador e quem tem `isSupplier` satisfaz Logística — capacidade como fonte única de verdade.
- Removidas as flags de debug `onlyCapturers`/`onlyAssault`/… do shopping (legado).

## Ferramentas de inspeção

- **Tela Shopping Pressure** (Tools > Utils): objetivos e slots demandados vs. preenchidos, e a fila de pressão de compra por papel, em runtime.
- **Export de jogadas** no `JogadasManager`: CSV e texto (botões no Inspector, tudo ou filtrado). Colunas resolvidas de tipo de construção na origem/destino, badge do setor (omitida em bases) e observação de captura.
- Obs de captura precisa (`10/20`, `capturado`, `reparado`), gravada no momento da jogada com o tipo de operação vindo do `TurnStateManager.Capture`.
- Novo campo `sufixo` em `ConstructionData` para rótulo curto; "Flag" é refinado para Spot/Anchor/Rally pelo papel runtime da construção.

## Conteúdo e HUD

- Conserto do sprite de rally (semáforo) no `ConstructionHudController`: referência baked quebrada (sub-sprite de folha Multiple) caía em quadrado branco; auto-cura quando o sprite "off" serializado é válido, e o prefab aponta para o sprite correto.

## Validação

- `dotnet build Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj`
- Resultado: 0 erros.
- Permanecem apenas avisos obsoletos já existentes nas APIs Unity.
