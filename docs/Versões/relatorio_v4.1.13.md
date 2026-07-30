# v4.1.13 - Participantes por Slot

Esta versão fecha a migração das ferramentas de desenvolvimento e inspeção para o modelo em que cada participante é identificado por `PlayerSlotId`. Os sistemas de runtime já separados por slot passam a ser acompanhados por janelas de Editor que respeitam a mesma identidade.

O objetivo permanece permitir que dois ou mais participantes compartilhem a mesma cor sem compartilhar estado. `TeamId` representa apresentação; `PlayerSlotId` representa o participante.

## Ferramentas de Editor

- A janela de debug do Serviço do Comando consulta saldo, opções e ordens pelo slot selecionado.
- O Plan Evaluator consulta risco, distância, fábricas e planos pelo slot da IA.
- Shopping Pressure constrói snapshots, lê planos, Go Green, rally e compromissos de compra pelo slot.
- Match Stats seleciona as estatísticas do participante pelo slot.
- Construction Manager Editor monta eixos de invasão para o slot correto.
- Sector Manager Editor desenha e inspeciona eixos por participante.

## Seleção manual e cores repetidas

- Seguir o participante ativo usa diretamente `ActiveSlotId`.
- Uma seleção manual baseada em cor somente é convertida quando existe um único slot com aquela cor.
- Se a cor for compartilhada por vários slots, a ferramenta não escolhe arbitrariamente o primeiro.
- Informações visuais continuam usando `TeamId`, incluindo nome e cor exibidos na interface.

## Serviço do Comando

- `ServicoDoComandoSensor.CollectOptions` recebe o índice do slot.
- A simulação do Editor usa o mesmo participante do runtime.
- O relatório de custos consulta a economia do slot exato.
- A interface identifica simultaneamente o número do slot e sua cor visual.

## Inspeção da IA

- Planos vivos são obtidos por `ObjectiveManager.GetPlanForSlot`.
- Snapshots são construídos por `AIWorldSnapshot.Build(PlayerSlotId, ...)`.
- Risco, distância de HQ e distância de fábrica usam `PlayerSlotId`.
- Inteligência, compromissos de elite, rally e invasão permanecem independentes entre slots de mesma cor.

## Contrato de identidade

- `PlayerSlotId` governa ownership, economia, turnos, estatísticas, FOW, inteligência e IA.
- `TeamId` governa cores, sprites, nomes e demais propriedades cosméticas.
- Compatibilidade baseada em cor exige resolução inequívoca.
- Ferramentas internas seguem as mesmas regras dos sistemas de jogo.

## Contrato transacional

As mudanças são de identificação e inspeção. Nenhum estado provisório passou a alterar definitivamente FOW, inteligência, ocupação, recursos, HP, combustível, munição, captura ou `HasActed`. A confirmação das ações continua obedecendo ao retorno a `CursorState.Neutral`.

## Validação

- `Assembly-CSharp-Editor.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem 143 avisos não bloqueantes.
- `git diff --check` concluído sem erros.
- A validação do assembly de Editor encontrou e eliminou os chamadores legados que ainda forneciam `TeamId` a APIs migradas para slot.

## Verificações recomendadas

- Abrir cada janela de inspeção durante uma partida com dois slots da mesma cor.
- Alternar entre seguir o participante ativo e a seleção manual.
- Confirmar que a seleção manual ambígua não exibe dados de outro participante.
- Comparar planos, estatísticas, economia e Serviço do Comando dos dois slots repetidos.
