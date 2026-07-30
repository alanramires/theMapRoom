# v4.8.2 — Refactor da AI Logística e de Estoque 2/5

## Visão geral

Esta versão liga a fundação criada na primeira parte à tomada de decisão da
IA. O **Melhor Estoque** deixa de ser apenas uma ferramenta de inspeção e passa
a orientar a recarga das unidades de Logística durante a partida.

Até aqui, o controller mantinha uma segunda implementação da procura: escolhia
fontes por regras próprias, simulava posições e tentava montar a transferência
depois. Isso fazia a janela de diagnóstico e a IA enxergarem a mesma rede por
caminhos diferentes.

Agora a pergunta é única: **onde esta unidade consegue receber estoque,
segundo o PodeTransferir, dentro das ondas Tactical e Operational?**

## A unidade vazia procura a rede próxima

Quando uma unidade logística atinge o limite de reposição definido em sua
ficha, a IA consulta sua necessidade real de estoque e pede ao Melhor Estoque
uma opção de `ReplenishSelf`.

A decisão respeita duas escalas:

- em **Tactical**, a unidade alcança o encontro nesta rodada e prepara um único
  batch com movimento e transferência;
- em **Operational**, a unidade escolhe uma progressão segura na direção do
  encontro, move-se e volta a consultar a rede no próximo turno.

Strategic não cria uma perseguição global nesta etapa. A recarga operacional
prioriza uma conexão verificável com a rede próxima, sem transformar uma
direção distante em promessa de transferência.

O rendezvous continua podendo ser um Hub móvel ou uma construção. A
compatibilidade dos estoques, o sentido da troca, o alcance de coleta e um
eventual pouso são confirmados pelo `PodeTransferir`, inclusive depois que o
movimento realmente termina.

## A escolha também considera a segurança

Uma fonte válida não é automaticamente uma boa parada.

O Melhor Estoque ganhou um limite de ameaça opcional. A IA logística usa esse
limite para descartar encontros expostos e, no deslocamento Operational,
procura uma célula que:

- avance de fato em direção ao rendezvous;
- possa ser ocupada pela unidade;
- não esteja sob ameaça conhecida;
- preserve a preferência de terreno e o custo do caminho.

Assim, uma unidade vazia não abandona seu papel apenas porque encontrou carga,
nem atravessa deliberadamente uma área hostil para economizar alguns
hexágonos.

## O modo Hospital usa a mesma rede

Transportadores que também prestam atendimento e carregam um paciente deixam
de possuir uma busca particular por recarga.

Se ainda conseguem manter o paciente a bordo, eles consultam o mesmo Melhor
Estoque:

- uma solução Tactical permite mover e receber carga;
- uma solução Operational permite aproximar-se da fonte;
- sem solução próxima, o fluxo não prende o paciente e devolve a decisão ao
  EVAC normal.

Isso preserva a prioridade humana da operação: procurar carga pode sustentar o
hospital móvel, mas não pode transformar a unidade ferida em passageiro
esquecido.

## Uma consulta, uma autoridade

A implementação antiga mantinha um `RestockSource` dentro do controller e
repetia a seleção de construções, Hubs, células vizinhas, ameaças e notas.
Essa cópia foi removida.

Também foram retiradas as consultas que mudavam temporariamente o
`CurrentCellPosition` da unidade para perguntar o que aconteceria em outro
hex. O `PodeSuprir` passa a oferecer uma consulta prospectiva explícita, assim
como o `PodeTransferir`: recebe a célula hipotética como parâmetro e avalia o
atendimento sem deslocar a peça no tabuleiro.

A separação fica mais nítida:

- Melhor Estoque escolhe e classifica o encontro;
- PodeTransferir decide se a troca de carga cabe ali;
- PodeSuprir decide quem pode receber atendimento de campo;
- o controller decide se move, transfere, atende, continua a aproximação ou
  devolve o caso a outro papel.

O papel `Estoque` não foi transformado em Logística. Nesta parte, o foco é a
recarga de quem presta serviços e dos híbridos que entram no modo Hospital. A
distribuição ativa de carga por Hubs e unidades de Estoque permanece uma
decisão própria das próximas etapas.

## Contrato transacional reforçado

Nenhuma avaliação prospectiva altera a verdade confirmada do tabuleiro.

Durante a busca por estoque ou por alvos de atendimento, a IA não:

- move a unidade real para uma célula hipotética;
- muda ocupação ou camada;
- transfere recursos antecipadamente;
- consome movimento, combustível ou ação;
- recalcula FOW, detecção ou memória a partir de uma posição provisória.

O movimento e a transferência continuam sendo materializados pelo batch e
revalidados no fluxo oficial. O estado definitivo só muda depois do
compromisso da ação.

## Guia de entrada

O capítulo **Olhando além do hexágono** ganhou uma explicação sobre o ritmo do
transporte.

Embarcar ou desembarcar é a ação do transportador naquela rodada, e a tropa que
desembarca também encerra seu turno. Ao mesmo tempo, o guia esclarece que o
transporte pode mover e desembarcar dentro da mesma ordem: a entrega completa é
uma única ação planejada, não um bônus posterior ao deslocamento.

## Validação

- build do runtime com zero erros;
- build do Editor com zero erros;
- conferência do restock Tactical com movimento e transferência no mesmo
  batch;
- conferência da progressão Operational com nova avaliação no turno seguinte;
- conferência do modo Hospital com paciente embarcado;
- auditoria das consultas prospectivas de PodeSuprir e PodeTransferir;
- remoção das simulações de posição no fluxo alterado;
- `git diff --check`;
- preservação dos arquivos `.meta` existentes.
