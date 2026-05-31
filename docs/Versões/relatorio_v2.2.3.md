# Relatorio v2.2.3 - AI ajustes em compras e defesa

## Tema

Consolidacao dos ajustes de defesa operacional, compras da IA e respostas a setores proprios sob ataque ou captura parcial.

## Principais mudancas

- Reavaliacao do plano e das operacoes no inicio da Fase 3, usando um snapshot fresco depois das acoes da Fase 2.
- Setores controlados pela IA, mas em disputa ou captura parcial, passam a ser tratados como defensaveis mesmo quando ainda nao estao `IsFullyControlled`.
- Objetivos ofensivos existentes em setores proprios sob disputa podem ser convertidos para `Defending` em vez de bloquearem a criacao da defesa.
- Objetivos defensivos nao sao invalidados mid-turn quando o setor ainda esta `IsDisputed` ou `HasPartialCapture`.
- `AIOperationManager` passa a aceitar setor proprio disputado/parcial para gerar `SectorDefense`.
- Defesa de base foi refinada para distinguir melhor ameaca aerea de ameaca terrestre e evitar compras terrestres por causa de aeronaves isoladas.
- Compras consideram melhor a diferenca entre Caca A e Caca B, evitando responder a caca elite com interceptor inferior quando houver necessidade real de superioridade aerea.
- Compra em construcao com aeronave sobrevoando foi ajustada para nao bloquear producao quando a camada de superficie esta livre.

## Comportamento esperado

- Se uma unidade revelar inimigos durante a Fase 2, a compra da Fase 3 ja pode reagir a essa nova informacao.
- Setores como Alpha, India, Oscar ou Foxtrot podem pedir defesa quando estiverem sob disputa, mas a demanda operacional continua sendo piso via `Mathf.Max`, nao soma explosiva.
- Setores proximos e com resposta barata tendem a receber apoio antes de setores distantes.
- Setores distantes sob pressao podem ficar para recuperacao posterior se nao houver unidade em alcance pratico.

## Validacao

- Build validado com `dotnet build Assembly-CSharp.csproj`.
- Resultado: 0 erros; permanecem apenas warnings conhecidos de APIs obsoletas do Unity.

## Observacoes

Esta versao ainda nao implementa a camada completa de task force. O foco foi tornar compras e defesa mais coerentes no caos de multiplos setores atacados ao mesmo tempo, sem criar panico de producao ou redistribuicao global excessiva.
