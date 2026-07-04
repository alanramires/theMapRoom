# v4.0.13 - Qualidade de Vida, Adaptação para Mobile, Interface

Esta versão prepara a partida para controle por toque, tornando cursor, loja, ações de unidade e telas de save operáveis por clique, sem abandonar os atalhos de teclado existentes. Também traz melhorias de qualidade de vida na leitura das telas e um novo controle de granularidade para os spots de observador avançado.

## Adaptação para Mobile

- O clique/toque em um hex equivale a posicionar o cursor nele e apertar Enter: o `CursorController` roteia o ponteiro para `HandleConfirm` e reproduz o feedback de ação correspondente.
- No estado `UnitSelected`, o clique só confirma quando o hex está no alcance pintado ou é a própria célula da unidade, evitando movimentos inválidos por toque impreciso.
- Na escolha de ação pós-movimento (`MoveuAndando`/`MoveuParado`), clicar de novo na própria unidade infere a ação: captura tem prioridade quando disponível; com a lista de sensores vazia, "Apenas Mover" é a única escolha restante.
- A loja de construção pode ser aberta por toque na construção e confirmada clicando novamente sobre ela; qualquer clique fora da UI fecha a loja, como o botão SAIR/ESC.
- A unidade visível no hex passou a ser sempre o alvo primário da confirmação; a loja continua acessível pelo ciclo de seleção quando as regras permitirem.

### Botões de toque

- O painel de ajuda ganhou controles runtime-only para a loja: `<<`, `>>`, `COMPRAR` e `SAIR`, ligados a `TrySelectShoppingOptionFromPointer`, `TryConfirmShoppingFromPointer` e `TryCancelShoppingFromPointer`.
- As ações de sensor (Mirar, Embarcar, Desembarcar, Capturar, Fundir, Suprir, Transferir, Mover) são expostas como botões no painel de diálogo, acionando os mesmos handlers do teclado.
- As telas de save/load ganharam botões de slot por toque, incluindo confirmação de sobrescrita e cancelamento, com os rótulos de metadados de cada slot.
- O painel de contagem recebeu o `button_rodada` para passar o turno por toque, habilitado apenas quando o cursor está `Neutral` e sem transição/IA/vitória em andamento.
- O menu da batalha fecha ao clicar fora dele, com suporte a mouse, toque e Input System legado, ignorando cliques sobre o próprio atalho de menu.

## Qualidade de Vida

- O overlay de coordenadas passou a ser opt-in por partida: começa desligado e continua alternável por `F3`.
- O atalho do painel de debug agora fura o bloqueio de foco do próprio campo de comando, funcionando como toggle para fechar novamente.
- O preview do serviço de comando reflete exatamente a fila de execução: as linhas seguem a ordem por custo da unidade e mantêm famílias embarcadas agrupadas, sem reordenar pela posição no mapa.
- Blocos de serviço de comando são ordenados pelo custo da unidade raiz, de modo que uma família embarcada permanece junta na posição correspondente ao custo do transportador.
- Atendimento parcial no serviço de comando é comunicado em laranja (a unidade recebe ao menos um serviço, mas não todos cabem no saldo); cinza fica reservado às unidades não atendidas.
- A contagem de unidades no painel deixou de forçar dois dígitos com zero à esquerda.

## Interface e observador avançado

- `ForwardObserverSpotUsage` (`Operational`, `RevealOnly`, `Disabled`) permite separar o interesse da IA da simples participação na visibilidade: só `Operational` gera interesse tático.
- `IsOperationalForwardObserverSpot` e `IsPreferredForwardObserverSpotForTeam` centralizam a leitura do modo, respeitando neutralidade e time observador.
- O Inspector de construção expõe o campo `Usage` quando o spot está marcado como observador avançado.
- A janela Retaguarda passou a exibir a célula do spot selecionado e a selecioná-lo automaticamente ao clicar em uma construção de observador na cena.
- O modo do spot é persistido no save (`forwardObserverSpotUsage`), com fallback para `Operational` em valores inválidos.

## AI

- Demanda de captura pura passa a preferir o capturador dedicado ao agressivo: uma penalidade em `QualityScore` domina o viés de custo, mas mantém o agressivo como fallback quando é a única oferta para o slot.

## Validação

- Compilação e teste em Play mode no Unity Editor (Windows), conforme o fluxo do projeto.
