# v4.0.12 - Melhorias no rally assembly e LoS em montanhas

Esta versão melhora a montagem de rallies da IA, torna as compras coerentes com os requisitos da invasão e organiza artilharia e logística atrás da vanguarda. Também separa a altura visual do terreno da altura herdada pelo observador, permitindo que cadeias de montanhas bloqueiem unidades posicionadas atrás delas.

## Rally assembly e compras

- Demandas de fogo indireto do rally agora exigem artilharia com peso mínimo adequado, evitando que unidades leves satisfaçam repetidamente uma necessidade de apoio pesado.
- A necessidade de ruptura exige uma unidade terrestre blindada de assalto e exclui artilharia, direcionando a compra para tanques.
- A contagem de ruptura do `Go Green` usa o mesmo critério empregado pelo Shopping Planner.
- Enquanto um rally está em `Assembling` ou `Ready`, a meta de elite usa a proporção segura configurada no AI Manager: 50% no modo normal e 80% no Hard.
- Demandas pendentes de capturadores não bloqueiam a compra de elite durante a montagem quando o núcleo operacional físico já existe.
- Compromissos persistentes de elite não são cancelados quando a pressão inimiga original fica coberta; persistem até a compra ou até a oferta/dados da unidade deixarem de ser válidos.
- O estado macro `Collapsing` não rompe sozinho a reserva estratégica. A reserva somente é liberada quando também existe uma demanda marcada como `Urgent`.

## Shopping Pressure

- O painel foi reorganizado em `Ordens Gerais`, `Elite` e `Go Green`.
- A reserva informa explicitamente se está preservada ou qual emergência urgente a rompeu.
- A meta de qualidade mostra quando o percentual elevado decorre de um rally em montagem.
- Demandas exibem os filtros de peso mínimo de artilharia e ruptura blindada, deixando claro qual capacidade permanece aberta.

## Comportamento das unidades

- Unidades de assalto valorizam com mais força a classe de alvo configurada como primária.
- Capturadores agressivos, incluindo Bazooka e Metranca, atacam da posição atual quando já possuem um disparo válido; só procuram aproximação quando não há tiro estacionário.
- Artilharia vinculada a objetivos ofensivos e rallies exige retaguarda e uma tela aliada à frente antes de reposicionar.
- O reposicionamento independente de fogo indireto respeita a mesma geometria de retaguarda e não avança por fallback quando a posição é insegura.
- Suprimentos sem atendimento imediato não avançam para uma célula mais ameaçada e com avaliação final pior que permanecer parados.

## LoS e LdT em montanhas

- A EV de terreno passou a aceitar valores decimais.
- `TerrainTypeData` ganhou `Override EV To`, usado quando `Shooter Inherits Terrain EV` está ativo.
- A montanha pode bloquear a linha em uma EV superior à EV herdada pela unidade: o terreno representa o cume, enquanto o observador ocupa uma posição abaixo dele.
- `PodeDetectar`, `PodeMirar`, o resolver de visão e a janela de debug trabalham com EV em ponto flutuante.
- O Inspector customizado de terreno expõe explicitamente EV, herança, override e bloqueio de LoS.
- Com montanha em EV superior e override do atirador em `2`, uma unidade sobre a montanha permanece visível, mas outra atrás de uma cadeia montanhosa fica ocultada.

## Captura e Metranca

- Capturadores agressivos aplicam um ponto de captura para cada dois pontos de HP, arredondando para cima.
- HUD de ameaça, execução da captura e handoff da IA usam a mesma função central de poder de captura.
- O Metranca deixou de ser elite derivada, recebeu custo atualizado e novos ativos visuais.

## Controles de debug

- Ao usar `F11` ou `F12` com a IA pausada por `F10`, o estado atual é cancelado e retorna a `Neutral` antes de liberar step ou resume.
- Isso desfaz preparações e seleções intermediárias antes de a IA continuar.

## Validação

- `Assembly-CSharp.csproj`: build sem erros.
- `Assembly-CSharp-Editor.csproj`: build sem erros.
- Teste visual confirmou bloqueio por montanha intermediária mantendo visibilidade da unidade sobre o primeiro cume.
