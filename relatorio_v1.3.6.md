# Relatorio v1.3.6

Data: 2026-03-17
Versao: v1.3.6
Resumo: Ajustes de interface da gameplay, helper/dialog e usabilidade de fluxo de turno.

## Principais ajustes

- Helper de shopping com token de construcao:
  - `helper.title.shopping` agora aceita `<Construction>`.
  - O runtime preenche o token com o nome da construcao ativa.

- Save/Load com mensagens por slot:
  - Suporte ao token `<slot>` em mensagens de sucesso de salvar e carregar.
  - Textos e assets de dialog/helper alinhados para exibir o slot corretamente.

- Painel de dialog em compra:
  - Quando o painel expande para modo de compra e o helper estiver deslocado para a esquerda, o dialog pode centralizar temporariamente.
  - Retorno do layout quando cursor/helper voltam ao estado normal.

- Helper de virada de turno (consumo de autonomia):
  - Linhas e titulo externalizados para Helper Data.
  - Duracao do texto e do destaque `!` configuravel via Animation Manager.

- Camera (atalho `N`):
  - Segundo toque restaura o zoom anterior do jogador, sem fixar em valor constante.

- Turno neutro:
  - Corrigido fluxo de upkeep/economia no inicio do turno neutro.
  - Ajuste de comportamento para foco/teleporte quando neutro nao possui HQ.

- Audio de musica por time:
  - Sliders de volume por time (incluindo neutro).
  - Preview no manager para tocar/parar faixas de teste.
  - Correcoes para respeitar volume por time tambem no modo de playback livre.

- Editor de construcao (produtividade):
  - `ConstructionDataEditor` agora tambem possui quick fill de `Offered Units` por forca (`Army`, `Navy`, `Aeronautic`), igual ao manager.

## Arquivos-chave impactados

- `Assets/Scripts/UI/PanelHelperController.cs`
- `Assets/Scripts/UI/PanelDialogController.cs`
- `Assets/Scripts/Match/TurnState/TurnStateManager.HelperPanel.cs`
- `Assets/Scripts/Save/SaveGameManager.cs`
- `Assets/Scripts/Camera/CameraController.cs`
- `Assets/Scripts/Match/MatchController.cs`
- `Assets/Scripts/Audio/MatchMusicAudioManager.cs`
- `Assets/Scripts/Match/Animation/AnimationManager.cs`
- `Assets/Editor/ConstructionDataEditor.cs`

## Observacoes

- Esta versao consolida pequenos reparos iterativos de UX/gameplay feitos durante o dia.
- Tambem inclui atualizacoes de assets de dialog/helper, construcoes e cena em uso.
