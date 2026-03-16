# Relatorio de Atualizacao - v1.1.1

## Como a experiencia comeca
Quando voce seleciona uma unidade, o jogo agora responde de forma mais clara e previsivel. As opcoes de acao aparecem com menos ruido, e as opcoes de sensores entram no fluxo de forma natural: primeiro voce entende o que a unidade pode fazer naquele momento, depois decide se vale escanear, avancar ou encerrar a acao.

Na pratica, isso reduz aquela sensacao de "menu brigando com menu". O turno fica mais legivel, com menos interrupcoes e menos chance de perder tempo por causa de transicoes confusas.

## O que mudou para quem joga
- O fluxo de turno ficou mais estavel em momentos criticos: combate, captura, compra em construcao e uso de sensores.
- A partida longa ficou mais segura com quick save/load em runtime (F8 para salvar, F9 para carregar).
- A navegacao entre escolhas ficou mais consistente, com menos risco de ficar preso em estados intermediarios.

## Impacto no dia a dia do projeto
- Dados de unidades e armas foram revisados para melhorar coerencia de combate e balanceamento.
- Catalogos e conteudo por mapa ficaram mais organizados, facilitando manutencao.
- Ferramentas de editor receberam ajustes para acelerar calibracao e reduzir retrabalho.

## Regras importantes (resumo)
- `Quick Save/Load`: F8 salva e F9 carrega no slot rapido.
- `Cross-scene load`: por padrao, o jogo pode bloquear load de save criado em outra cena.
- `Force load when busy`: opcao para forcar carregamento fora do estado ideal.

## Bloco tecnico curto
- Novo componente de save/load: `Assets/Scripts/Save/SaveGameManager.cs`.
- Ajustes de fluxo em arquivos centrais de turno e partida (`TurnStateManager`, `MatchController`, `ConstructionManager`, entre outros).
- Escopo da versao: consolidacao ampla com foco em confiabilidade de runtime, save/load e dados de combate.

## Resultado
A v1.1.1 fortalece o que mais importa para jogar e testar: menos friccao no turno, mais seguranca durante a partida e uma base mais estavel para evoluir gameplay e balanceamento.
