# Relatório v4.0.33 — Combate, painéis e usabilidade mobile

## Visão geral

Atualização concentrada em correções de combate e sensores, novas regras para submarinos, melhor leitura dos painéis e seleção mais acessível de unidades empilhadas em dispositivos móveis.

## Combate, detecção e Fog of War

- Hex revelado e unidade detectada passam a ser tratados separadamente: revelar o terreno não expõe automaticamente tropas que o observador não consegue detectar.
- O fluxo de mira passa a reconhecer observadores avançados aéreos e a consultar corretamente a detecção compartilhada do alvo.
- Tiros a partir do Fog of War respeitam a detecção efetiva, o domínio, a camada, a linha de visão e o alcance da arma.
- A lista **Escolher Alvo** informa a arma e a categoria avaliadas em cada opção.
- Motivos de rejeição, como alcance máximo e domínio incompatível, permanecem detalhados no `panel_dialog`.
- Alvos válidos aparecem antes dos inválidos, preservando a ordenação geográfica dentro de cada grupo.
- Opções inválidas selecionadas recebem contorno na cor do time para tornar o foco visível.
- Ajustadas a seleção e a prioridade de armas em cenários com múltiplos alvos e domínios.

## Novas regras de combate submarino

- Ataques e efeitos capazes de revelar um submarino podem impor emersão forçada e lock temporário de camada.
- A transição para a superfície valida terreno, construção, estrutura e ocupação da camada de destino.
- Quando a superfície está bloqueada, o lock permanece pendente e é aplicado assim que um destino válido for alcançado.
- O submarino com emersão pendente permanece revelado; o tempo do lock só começa a correr depois que a camada for efetivamente aplicada.
- Movimento, alcance pintado, cancelamento e rollback respeitam o lock sem violar o contrato transacional.
- O estado completo do lock de camada é persistido em saves, com compatibilidade para o formato anterior.

## Painéis e apresentação

- O `panel_helper` de escolha de alvo ganhou altura máxima, recorte e rolagem por roda do mouse, toque ou arrasto com o botão esquerdo.
- A navegação por teclado mantém automaticamente a opção focada dentro da área visível.
- Botões de alvo exibem unidade, HP, camada/local, arma e categoria em linhas separadas.
- A confirmação de fusão ganhou alturas e espaçamentos próprios para resumo, resultado e botão de confirmação.
- O relatório de consumo de autonomia em voo agora mostra o sprite da unidade, nome, cálculo do combustível e coordenada como informação secundária.
- Ajustados fontes, fallback de caracteres e dimensionamento de textos para os novos conteúdos.

## Empilhamento e mobile

- Unidades e construções compartilhando o mesmo hex podem ser alternadas pelo fluxo unificado de seleção.
- O `panel_helper` passa a oferecer controle tocável para trocar a seleção no hex, equivalente ao ciclo por Page Up/Page Down.
- A posição atual e o total de entradas empilhadas ficam disponíveis para a interface.
- A troca preserva o estado correto ao alternar entre unidade e construção.

## Transporte e aviação

- Hidroaviões voltam a decolar automaticamente após receber passageiros, seguindo o comportamento de transportadores aéreos.
- O fluxo aceita a decolagem válida mesmo quando o relatório não corresponde apenas aos casos extremos de movimento.

## Efeitos visuais

- Projéteis parabólicos ganharam sombra projetada sobre o tabuleiro.
- A sombra percorre a projeção reta da linha de tiro, gira no sentido origem–alvo e varia escala e opacidade conforme a altura do arco.
- O efeito usa a layer `SFX`, abaixo do projétil, e foi configurado nas cenas de jogo.

## Depuração e caches

- Spawns de debug agora invalidam automaticamente as revisões de ameaça para que unidades existentes reconheçam os novos alvos.
- Adicionados os aliases `refresh cache`, `refresh caches` e `reset cache` para atualização global dos caches runtime.
- `remove unit` passa a funcionar como alias de `destroy unit`.
- A atualização de caches durante **Escolher Alvo** preserva o modo de movimento, o foco e a etapa da mira.
- Os atalhos da IA e os atalhos gerais de debug passam a possuir flags independentes, evitando exposição em gameplay normal.

## Validação

- Projeto runtime compilado sem erros.
- Verificação de diferenças concluída sem erros de whitespace.
