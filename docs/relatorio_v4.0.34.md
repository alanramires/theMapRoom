# Relatório v4.0.34 — Bug fixes e ajustes em logística

## Visão geral

Atualização concentrada em correções de fluxo de rodada, autonomia aérea e melhorias no controle, apresentação e persistência dos estoques logísticos de unidades e construções.

## Logística

- Construções fornecedoras passam a exibir alertas visuais próprios para estoques vazios de galões, caixas de munição e peças.
- O prefab de construção recebeu os slots `supply_top`, `supply_middle` e `supply_bottom` para organizar os alertas de estoque.
- Os comandos de debug `set galoes`, `set caixas` e `set pecas` agora funcionam tanto em unidades supridoras quanto em construções sob o cursor.
- Construções aceitam valores runtime de estoque sem teto, mantendo o tratamento explícito para fornecimento infinito.
- A captura ou troca de proprietário de uma construção preserva seus estoques runtime, serviços e configuração de mercado, sem restaurar os valores padrão do `ConstructionData`.
- Sprites de galões, caixas e peças permanecem na orientação original durante suprimentos e transferências, sem girar como munições de combate.
- Ajustados os painéis e indicadores relacionados a estoque, suprimento e consumo de autonomia.

## Aviação e rodada

- Ajustada a operação de aeronaves no início da rodada, incluindo consumo de autonomia e pouso emergencial quando aplicável.
- Melhorada a apresentação do relatório de consumo em voo.
- Corrigido o controle de exibição do painel de rodada em partidas PVP quando a opção de debug está ativada.

## Interface e depuração

- Melhorados espaçamentos e informações apresentadas nos painéis auxiliares.
- Atualizados comandos e ajuda do Debug Manager para os novos controles de estoque em construções.
- Ajustados recursos visuais, cursores e fontes utilizados pelas interfaces modificadas.

## Validação

- Projeto runtime compilado sem erros.
- Alterações verificadas nos fluxos de estoque, captura, transferência logística e início de rodada.
