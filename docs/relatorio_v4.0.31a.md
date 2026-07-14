# Relatório v4.0.31a — Hotseat Fixes

## Visão geral

Correções encontradas durante uma partida completa de PvP hot seat, com foco em submarinos, Fog of War, combate multicamada, desempenho e usabilidade do `panel_helper`.

## Submarinos, estruturas e camadas

- Regras de emergência naval passam a aceitar configuração por combinação de estrutura e terreno.
- Pontes sobre o mar permitem a passagem de submarinos submersos.
- Pontes sobre praia continuam podendo forçar a emergência por configuração data-driven.
- Submarinos que atacam ou revidam emergem conforme a regra da unidade.
- Ajustes nos dados do Porto Naval e nas estruturas usadas pelo mapa hot seat.

## Fog of War e sensores

- A apresentação de unidades em coabitação multicamada não é recalculada durante ações provisórias.
- Mudanças visuais de coabitação aguardam o compromisso da ação e o retorno a `Neutral`.
- O menu de ataque humano consulta somente o snapshot confirmado de visibilidade.
- Alvos ocultos não aparecem antecipadamente durante movimento, seleção ou confirmação cancelável.
- Revisões de visibilidade, sensores e HUD permanecem alinhadas ao contrato transacional do tabuleiro.
- Auditada a elegibilidade de aeronaves, incluindo helicópteros, como observadores avançados.
- O observador precisa estar aliado, ativo, não embarcado, dentro do alcance visual da camada do alvo e apto a detectar stealth quando aplicável.
- Identificada a necessidade de manter a política de LoS de `AirHigh` consistente entre `PodeDetectar` e a busca de observadores do `PodeMirar`.

## Combate

- O revide de submarinos aplica a mesma regra de emergência utilizada no ataque normal.
- A seleção de alvos preserva validações de alcance, camada, detecção, LoS e munição.
- Ajustes nos dados dos Caças A e Caças Stealth utilizados na partida.

## Desempenho

- Cálculos de alcance passam a reutilizar cache por unidade e contexto.
- Consultas de ocupação utilizam índice espacial para reduzir varreduras quando o tabuleiro está cheio.
- A seleção de unidades e a construção de áreas de movimento ficam mais responsivas em partidas densas.

## Panel Helper

- O painel pode ser arrastado por uma alça visível.
- A posição escolhida é limitada à tela e persistida entre sessões.
- O posicionamento manual não é desfeito pelo antigo deslocamento automático por proximidade.
- Botões de seleção de alvo medem o texto e aumentam a própria altura quando necessário.
- Textos extensos empurram os itens seguintes sem reduzir a legibilidade.
- Sprites de unidades transportadas respeitam a cor efetiva do time/slot.
- A lista e a confirmação de alvos descrevem a camada operacional atual da unidade, em vez do terreno físico abaixo dela.
- Unidades em `AirHigh` exibem **Altas Altitudes** e unidades em `AirLow` exibem **Baixas Altitudes**, conforme o `DPQAirHeightConfig`.
- Submarinos em `Submarine/Submerged` exibem **Submerso**, sem cair para rótulos como Mar, Porto ou Ponte.
- Na ausência eventual do tile visual da camada, o nome configurado continua sendo preservado; domínio/altura são usados como último fallback.

## Conteúdo hot seat

- Atualização da cena **Hot Seat 1 - Pvp** a partir dos testes da partida live.
- Atualização dos assets de fonte e fallback utilizados pela interface.

## Validação

- Fluxos exercitados em partida live com tabuleiro denso, FOW, unidades multicamada, transporte, desembarque, ataque, revide e decolagem.
- Projeto runtime compilado sem erros.
