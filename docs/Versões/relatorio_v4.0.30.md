# v4.0.30 - Ajustes no FOW, Stealth, Submarine Ops, visibilidade, hot seat e editor de mapa

## Foco

Checkpoint amplo de gameplay e ferramentas, concentrado na consolidação do Fog of War por camadas, operações submarinas e aéreas, apresentação hot seat, visibilidade temporária durante ações e evolução do editor de mapas.

## Fog of War e visibilidade

- Inclusão dos modos de visualização `Todas`, `Aérea`, `Superfície` e `Submarina`.
- O modo `Todas` passa a ser a união explícita das três camadas especializadas.
- Preferência de camada mantida separadamente por jogador durante a partida, sem persistência em saves.
- Integração da seleção de camada ao `MenuRoot`, ao atalho `L`, ao `Panel_remaining` e aos botões modais do `panel_helper`.
- Construções aplicam seu alcance configurado de visão em todas as visualizações de camada.
- Correções de cache e atualização do FOW após compras e mudanças de camada.
- Ferramenta `Tools > FoW > Hex Enxergado` para investigar todas as unidades e construções que revelam um hex, inclusive por camada virtual.
- Correções na apresentação temporária de unidade, HUD, cursor e área de movimento acima do FOW durante ações ainda não confirmadas.
- Restauração defensiva das sorting layers ao confirmar movimento e ao trocar de turno, evitando vazamento de unidades inimigas.

## Sensores, stealth e altitude

- Revisão do `PodeDetectarSensor` para separar alcance especializado, camada observada e EV atual do observador.
- Linhas de visão especializadas passam a iniciar no EV correspondente à camada real da unidade.
- O atalho de observação `AirHigh` sem LoS só é aplicado quando o observador está efetivamente no ar.
- Aeronaves pousadas deixam de projetar linhas como se estivessem em `AirHigh`, mantendo a geometria ascendente a partir do solo.
- Ajustes de stealth, scanner e persistência de detecção para unidades e camadas especializadas.
- Novos dados e ajustes em unidades stealth, radar, infantaria e submarinos.

## Submarine Ops e aeronaves

- Refinamentos de domínio, altura e visão para operações navais e submarinas.
- Ajustes no submarino e nas regras de detecção de alvos submersos.
- Correção da classificação de aeronaves para usar `IsAircraft`, abrangendo hidroaviões e futuras unidades híbridas.
- Hidroaviões passam a decolar após embarque de passageiro quando as regras de operação aérea permitem.
- Compra de aeronaves conclui a configuração de estado pousado antes do refresh final do FOW.

## Hot seat

- Novo `Panel_rodada` para partidas locais com jogadores humanos.
- Bloqueio real do carregamento/apresentação inicial até a confirmação do jogador.
- Sequência de áudio com abertura, identificação do jogador, espera em loop e fechamento.
- Confirmação por botão ou Enter, sem aceitar clique fora do botão.
- Nome e cor do time, turno real do `MatchController` e animações sincronizadas.
- Botão e textos surgem juntos no fade-in; interação só é liberada após a fala.
- Música da partida e entradas de gameplay permanecem bloqueadas durante a passagem de tela.

## Editor de mapa

- Evolução do `BasicMapGeneratorWindow` para espelhamento automático em mapas hexagonais.
- Linha central preservada sem espelhamento.
- Espelhamento superior/inferior com correção de paridade para coordenadas offset do hex grid.
- Operação sem depender de dimensões fixas, acompanhando diretamente o Tilemap selecionado.
- Ajustes em mapas, catálogos, construções e estruturas de tutorial e hot seat.

## UI, menus e tutorial

- Navegação do `MenuRoot` baseada na ordem visual dos botões, adaptando-se a reposicionamentos no prefab.
- Fluxo modal de seleção de camada seguindo o mesmo padrão de Save/Load.
- Atualizações no `Panel_remaining`, `panel_helper`, atalhos e indicadores de camada.
- Continuidade do tutorial para novatos, incluindo automações, tarefas, cenas, documentação e novos assets.
- Inclusão do mapa e catálogos de Hot Seat PvP.

## Estado

- Build de runtime verificado com `dotnet build Assembly-CSharp.csproj --no-restore`.
- Build do Editor verificado durante o ciclo com `dotnet build Assembly-CSharp-Editor.csproj --no-restore`.
- Compilações concluídas sem erros.
