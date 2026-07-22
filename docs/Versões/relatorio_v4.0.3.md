# v4.0.3 - AI Carrinho de Compras

Esta versão consolida a tomada de decisão econômica da IA. O shopping deixa de escolher cada unidade isoladamente e passa a planejar um carrinho global, considerando simultaneamente orçamento, vendedores disponíveis, demandas operacionais, composição inimiga, reserva estratégica e diversidade de papéis.

## Carrinho global de compras

- Levantamento de todos os vendedores controlados pelo slot, incluindo HQ, cidades e fábricas.
- Orçamento único compartilhado entre todos os vendedores, sem divisão artificial de caixa por construção.
- Limite de uma compra por vendedor disponível em cada fase de shopping.
- Avaliação do conjunto completo de compras antes da emissão das ordens.
- Busca limitada por feixe para comparar combinações sem explosão combinatória.
- Vendedores com catálogos mais restritos são avaliados primeiro, preservando oportunidades difíceis de substituir.

## Critérios de escolha

O carrinho é classificado nesta ordem:

1. atendimento de demandas urgentes;
2. cobertura das demandas de maior prioridade;
3. quantidade de demandas diferentes cobertas;
4. atendimento total das quantidades solicitadas;
5. qualidade e adequação das unidades;
6. aproveitamento do orçamento disponível.

Com isso, a IA prefere cobrir FireSupport, Assault e Transportador antes de repetir três vezes o mesmo papel, desde que existam ofertas elegíveis e orçamento suficiente.

## Preservação de caixa

- Não comprar também é uma alternativa válida durante a montagem do carrinho.
- Quando nenhuma oferta atende às demandas atuais, o dinheiro é preservado.
- Compras sem demanda continuam restritas às exceções já existentes, como defesa crítica e preenchimento defensivo de construções vulneráveis.
- A reserva estratégica para elite continua protegida durante a composição do carrinho.
- Estado crítico de território ainda pode remover a reserva e priorizar sobrevivência imediata.

## Gates preservados

Cada candidato continua respeitando:

- postura ofensiva, defensiva ou flexível;
- papel de composição e capacidade exigida;
- categoria de arma requerida pelo counter pressure;
- cadeia de progressão elite;
- domínio e força militar;
- célula de produção livre;
- orçamento disponível após reserva;
- compatibilidade operacional de transportadores.

## Counter e operational pressure

- Pressão de counter continua escolhendo a arma adequada dentro do papel solicitado.
- Operational pressure acrescenta demandas de transporte conforme os eixos avançam.
- Pressão logística considera desgaste e unidades em reparo.
- Transportador operacional é separado de unidade primariamente logística.
- Demandas são recalculadas por cobertura, evitando compras repetidas sem necessidade.

## Inteligência persistente

- `AIIntelLedger` mantém memória de ameaças observadas mesmo quando a unidade observadora morre antes do próximo turno.
- Ataques recebidos podem revelar a categoria da ameaça sem revelar indevidamente sua posição.
- Memória de unidades, danos, baixas e carga destruída participa da pressão de compras.
- Estado de inteligência foi incorporado ao save consolidado da partida.

## JogadasManager e save

- Eventos de combate registram HP antes e depois, atacante, defensor e perdas de carga embarcada.
- Exportação CSV usa campos vazios quando não existe segundo participante ou resultado de HP.
- Capturas recebem prefixo textual para evitar interpretação automática como data em planilhas.
- Terminologia de times foi ajustada para slots nos dados estruturados.
- Sistemas auxiliares de save foram integrados ao save principal da partida.

## Dados e conteúdo

- Reorganização dos dados de unidades, armas, habilidades e catálogos para `Assets/DB/Units`.
- Inclusão da matriz de combate em `docs/COMBAT_MATRIX.csv`.
- Atualizações na janela `Tools > Utils > Shopping Pressure`.
- Ajustes em fontes, dados de unidade e componentes de runtime relacionados ao novo fluxo.

## Diagnóstico

O shopping agora registra o resumo do carrinho:

`carrinho itens=3 demandas=3 atendimentos=3 gasto=... saldo livre=...`

Quando não há compra válida:

`carrinho vazio: nenhuma oferta elegível atende demanda ... caixa preservado`

## Validação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- Resultado: 0 erros.
- Permanecem apenas avisos preexistentes de APIs Unity obsoletas.
