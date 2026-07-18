# v4.0.38a — Raycast tratado

Data: 17/07/2026

## Visão geral

Esta revisão corrige uma falha intermitente de seleção que dava a impressão de lag: algumas unidades, especialmente o submarino, exigiam vários cliques antes de serem selecionadas. A investigação separou o custo real do cálculo de alcance da entrega do ponteiro e revelou que elementos decorativos do HUD estavam interceptando o mouse por meio de `Raycast Target`.

## Causa raiz

- O clique direto no mapa executava `EventSystem.RaycastAll()` e recusava a seleção quando encontrava qualquer elemento de UI sob o ponteiro.
- Imagens e textos decorativos dos prefabs de unidade e construção possuíam áreas retangulares maiores que o desenho visível e estavam marcados como `Raycast Target`.
- Essas áreas transparentes criavam regiões mortas sobre o tabuleiro: o clique chegava ao jogo, mas era classificado como `pointer-over-ui` antes da conversão da posição do mouse para a célula hexagonal.
- O problema parecia intermitente porque pequenos deslocamentos do ponteiro entravam ou saíam do retângulo invisível do HUD.

## Evidência do diagnóstico

- Nove cliques consecutivos na região do submarino foram recebidos pelo Input System e rejeitados como `pointer-over-ui` em aproximadamente `0,29 ms` cada.
- Um clique poucos pixels ao lado saiu da máscara invisível e selecionou a unidade imediatamente.
- O pipeline de seleção do submarino ficou entre aproximadamente `22 ms` e `28 ms` nos testes finais.
- Quatro seleções consecutivas foram concluídas no primeiro clique, com tempos totais de `23,05 ms`, `22,76 ms`, `22,72 ms` e `22,63 ms`.
- O cálculo e a pintura do alcance continuam sendo a maior parte desse pequeno custo, mas não eram responsáveis pelos cliques ignorados.

## Correção no Cursor

- Em `Neutral`, o mapa deixa de ser bloqueado por qualquer imagem retornada pelo raycast de UI.
- A proteção agora considera somente elementos que realmente implementam clique, incluindo handlers presentes nos objetos pais.
- Botões e controles interativos continuam impedindo que o clique atravesse para o tabuleiro.
- Menus abertos preservam sua prioridade no `CursorController`.
- Elementos puramente visuais não criam mais buracos mortos sobre unidades ou construções.

## Limpeza dos prefabs

- `Raycast Target` foi desligado nos elementos decorativos do `unit.prefab`.
- `Raycast Target` foi desligado nos elementos decorativos do `construction.prefab`.
- HUD, barras, ícones, textos, bandeiras, indicadores de camada e demais componentes visuais deixam de participar desnecessariamente do raycast do EventSystem.
- Unidades e construções restauradas por load recebem a configuração atual dos prefabs; o save não persiste o valor visual de `raycastTarget`.

## Instrumentação de performance e input

- O snapshot `F8` passou a escrever diretamente no Console e deixou de depender de `Enable TurnState Runtime Logs`.
- A janela de frames do snapshot agora é realmente móvel e mantém apenas as últimas 120 amostras; spikes antigos não permanecem presos no valor máximo.
- Nova flag `Show Frame Spike Logs`, com limiar configurável, registra automaticamente frames longos.
- O log `[FrameSpike]` informa estado, substep, unidade selecionada, revisão do tabuleiro, replay, IA, transição de turno, animação, bloqueio de input, memória e atividade do GC.
- O log `[PointerRaw]` confirma a chegada física do mouse antes dos bloqueios do cursor.
- Os logs `[PointerSelect] received`, `processed` e `ignored` distinguem clique entregue, processado ou rejeitado, incluindo tempos de UI, célula e confirmação.

## Organização dos logs

- Hash, diagnóstico e caminho de save passam a obedecer a `Show SaveLoad Logs`.
- Mensagens `[LayerForce]` passam a obedecer a `Show Movement Logs`.
- Logs de inspeção passam a obedecer a `Enable TurnState Runtime Logs`.
- Métricas `[HotzoneCache]` passam a obedecer a `Enable Range Cache Debug Logs`.
- Desligar as flags silencia apenas o Console; as operações, métricas internas e regras de jogo continuam funcionando.

## Contrato transacional

- A correção atua somente na decisão de encaminhar o clique do ponteiro.
- Nenhum estado de unidade, alcance, combustível, camada, Fog of War ou sensor é comprometido antecipadamente.
- A seleção continua começando em `Neutral` e entrando no fluxo provisório normal do `TurnStateManager`.
- Botões interativos permanecem protegidos contra cliques que atravessariam para o tabuleiro.

## Validação

- Testes manuais confirmaram seleção do submarino no primeiro clique sobre diferentes pontos do HUD.
- UI check estabilizado em aproximadamente `0,15–0,16 ms`.
- `SetCell` estabilizado em aproximadamente `0,15–0,17 ms`.
- Pipeline total de seleção estabilizado em aproximadamente `22–23 ms` no cenário final.
- Build de `Assembly-CSharp.csproj`: **0 erros**.
- `git diff --check` executado sem erros.

