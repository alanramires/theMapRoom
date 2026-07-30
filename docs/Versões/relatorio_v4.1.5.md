# v4.1.5 - Ajustes na AI, acertos na linha de trem

Esta versão consolida ajustes no planejamento econômico da IA, na configuração de unidades e construções e na representação das redes de transporte dos mapas.

## Inteligência artificial

- O planejamento de compras recebeu uma leitura mais detalhada da pressão inimiga e da cobertura disponível para cada categoria de counter.
- Demandas de anti-infantaria, anti-tanque, anti-aérea e anti-navio foram refinadas para considerar composição, memória de contatos e capacidade já presente no exército.
- A inspeção do shopping da IA passou a expor pressão bruta, cobertura, classes inimigas, contribuições próprias e os melhores counters elegíveis.
- Foram acrescentadas estruturas de presets de dificuldade e uma ferramenta de Editor para gerar e auditar presets a partir dos valores ativos da cena.
- Parâmetros estratégicos e econômicos foram organizados nos dados das unidades e construções para facilitar balanceamento e diagnóstico.

## Linha de trem e rede viária

- Dados do trem de carga, da estação ferroviária e dos trilhos foram revistos para alinhar o deslocamento ferroviário às rotas configuradas no mapa.
- As rotas de transporte dos mapas e catálogos de estruturas receberam correções e novos trechos.
- A antiga Ponte Alta foi substituída pela Ponte Rodoviária, com dados adequados à rede viária.
- Os mapas Battle Map 1 e Hot Seat 1 receberam os acertos correspondentes de cenário e infraestrutura.

## Regras, sensores e ferramentas de debug

- Sensores de embarque e desembarque receberam correções de elegibilidade e diagnóstico.
- Os comandos de debug `landing`, `take off`, `altitude`, `emerge` e `submerge` agora alteram a camada da unidade sob o cursor sem selecioná-la, respeitando regras e animações.
- A ordenação do cursor e das ferramentas foi corrigida: somente o FOW Total usa as camadas `fow`/`fow_tile`; os demais modos permanecem sob controle da camada SFX.

## Dados, interface e documentação

- Dados serializados de unidades, construções, mapas, fontes e painéis foram atualizados.
- O manual técnico e os documentos de projeto receberam revisões e reorganização.
- Os relatórios históricos foram agrupados em `docs/Versões`.

## Validação

- Projeto compilado com sucesso, sem erros, após os ajustes de código desta versão.
- Avisos preexistentes do projeto e de normalização de arquivos serializados do Unity permanecem sem bloquear a compilação.
