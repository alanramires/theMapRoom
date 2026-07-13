# Relatório v4.0.31 — Publicada PvP e tutorial

## Visão geral

Esta versão publica o fluxo inicial do modo PvP local em hot seat, atualiza a entrada da campanha e consolida ajustes no primeiro tutorial.

## PvP hot seat

- Adicionado o botão **Jogador vs Jogador** na Tela de Entrada.
- Os dois slots da configuração são controlados por jogadores humanos.
- O mapa inicial do modo foi fixado em **Hot Seat 1 - Pvp**.
- A cena PvP foi adicionada às cenas da build.
- O painel de passagem de turno protege a tela entre os jogadores e aguarda confirmação antes de liberar o tabuleiro.
- Removidas as falas `turno do jogador 1.mp3` e `turno do jogador 2.mp3`, preservando abertura, espera e fechamento do painel.

## Interface e navegação

- O botão **Sobre** abre um `panel_helper` ampliado com a apresentação do jogo e botão de confirmação.
- A navegação do menu inicial acompanha a ordem visual dos botões.
- O seletor de camada no mapa aceita setas, confirmação, sons de interface, Esc e clique direito.
- Cancelar a seleção de camada retorna ao MenuRoot com foco em **Camada**, como nos fluxos de Save/Load.

## Tutorial

- O soldado inimigo do Tutorial 1 foi configurado como estacionário e orientado a atacar parado.
- O inimigo não avança por iniciativa própria antes da instrução narrativa.
- O deslocamento roteirizado continua tendo prioridade e é executado somente no momento determinado pelo `TutorialData`.
- Depois do movimento do roteiro, o autômato permanece disponível para o combate estacionário.

## Mapas e ferramentas

- O espelhamento automático do **Map Generator (Basic)** agora só funciona enquanto a janela da ferramenta estiver aberta.
- Ao fechar a ferramenta, alvo e snapshot de espelhamento são descartados.
- O `RoadNetworkManager` restringe a descoberta de tilemaps à própria cena.
- Visuais derivados de estrada não são mais persistidos no YAML das cenas e são reconstruídos pelo banco correto de cada mapa.
- Isso impede que importações e builds misturem tabuleiros ou reconstruam estradas usando outra cena.

## Build Web

- Adicionado o perfil/configuração Web do projeto.
- Atualizada a lista de cenas para incluir a Tela de Entrada, campanha, PvP e tutoriais publicados.

## Validação

- Projeto runtime compilado sem erros.
- Assembly de ferramentas do Editor compilado sem erros.

