# v4.8.0 — Refactor da AI Logistica e de Estoque

## Visão geral

Esta versão abre o novo ciclo de trabalho da cadeia logística da IA. O objetivo
é deixar clara a diferença entre quem presta atendimento em campo, quem
movimenta estoques e quem transporta unidades, sem apagar as combinações que
fazem parte do roster.

Um Porta-Aviões continua sendo, antes de tudo, um transportador. Isso não o
impede de manter as aeronaves embarcadas. Um Trem de Carga continua buscando
passageiros, mas também funciona como elo ferroviário de estoque. Já o Navio
Tanque passa a ser reconhecido pela intenção que realmente define sua missão:
levar combustível até a força naval e mantê-la operando longe do porto.

## Logística e Estoque deixam de ser sinônimos

O antigo papel `Suprimentos` passa a se chamar `Estoque`, preservando o mesmo
valor serializado nos `UnitData`.

A mudança estabelece uma linguagem mais precisa:

- **Logística** transforma seus estoques internos em atendimento de campo,
  como abastecimento, reparo e rearmamento.
- **Estoque** movimenta carga entre construções, hubs e unidades recebedoras.
- **Transportador** decide passageiros, embarque, deslocamento e desembarque.

As capacidades continuam podendo se combinar. O papel define a prioridade da
IA; as seções Logistics e Transport do `UnitData` dizem o que a unidade
realmente consegue fazer.

## A cadeia naval ganha papéis mais claros

O Navio Tanque foi reclassificado como `Logistica`.

Ele permanece um Hub naval, coleta e distribui estoque no próprio hex ou a um
hex de distância e presta seu serviço de abastecimento somente a unidades
adjacentes. Isso prepara a IA para preferi-lo como ponto móvel de reposição,
antes de ordenar que uma unidade vazia atravesse o mapa de volta ao porto ou ao
QG.

A Fragata recebeu ajustes para representar melhor sua missão:

- permanece como `RaidAntiSub`;
- a carga de profundidade passa a operar em alcance zero, exigindo que a
  fragata alcance a coluna do submarino;
- seu perfil de serviços embarcados é reconhecido como atendimento de campo;
- pode liberar o passageiro quando não consegue mais atendê-lo.

Esse arranjo preserva o caráter híbrido da frota: a Fragata caça submarinos,
transporta e mantém seu Apache; o Porta-Aviões transporta e sustenta aeronaves;
o Navio Tanque abastece a formação e movimenta estoque.

## Ferramentas de transporte mais legíveis

As ferramentas de análise passam a explicitar que escolhem uma zona de
encontro, não que executam a operação:

- `Melhor Embarque` passa a se chamar **Melhor LZ de Embarque**;
- `Melhor Desembarque` passa a se chamar **Melhor LZ de Desembarque**;
- a explicação do **Quero Carona** acompanha a nova nomenclatura.

Essa apresentação reforça a divisão que orientará a próxima etapa:

- a ferramenta escolhe e classifica a LZ;
- `PodeEmbarcar` ou `PodeDesembarcar` confirma a legalidade;
- somente o controller transforma a decisão em ação.

## Próximo passo da IA logística

O refactor será conduzido sobre os sistemas que já funcionam.

`PodeSuprir` continuará sendo a autoridade para atendimento e manterá sua
seleção por alcance Tactical e Operational. A prioridade agora é extrair a
inteligência de estoque que ainda está presa ao controller.

Nas próximas partes, a IA deverá:

1. reconhecer quando o próprio estoque ficou preventivo, operacional ou
   criticamente baixo;
2. procurar Hubs móveis e construções em ondas Tactical e Operational;
3. usar uma nova consulta **Melhor Estoque** para escolher fonte, destino e
   rendezvous;
4. validar a transferência final com `PodeTransferir`;
5. recorrer ao QG ou à construção fixa somente quando a rede próxima não
   oferecer solução;
6. combinar essa decisão com `Quero Carona`, Melhor LZ de Embarque e Melhor LZ
   de Desembarque quando a unidade também for passageira ou transportadora.

Assim, um Avião Tanque vazio poderá procurar um Porta-Aviões, uma Fragata
poderá procurar um Navio Tanque e um Suprimentos terrestre poderá encontrar um
Hub móvel antes de abandonar a operação.

## Cena e apresentação

A cena de desenvolvimento `Quadrado` foi atualizada com o estado corrente dos
testes, incluindo o ajuste da renda configurada e a atualização dos dados
derivados de setores e distâncias.

Os assets de fonte do TextMesh Pro também foram atualizados. O fallback passa
a conter o glifo horizontal usado na apresentação e o atlas da VT323 foi
regenerado para os textos atuais da interface.

## Contrato de ações

As ferramentas e serviços deste ciclo permanecem consultas. Avaliar estoque,
carona, embarque, desembarque, suprimento ou transferência não altera posição,
recursos, ocupação, FOW ou detecção.

Somente o controller poderá materializar uma ação, e o estado definitivo
continua sendo atualizado no compromisso explícito e no retorno a
`CursorState.Neutral`.

## Validação

- build de runtime e Editor;
- conferência dos papéis e capacidades diretamente nos `UnitData`;
- conferência dos menus das ferramentas de transporte;
- `git diff --check`;
- preservação dos assets serializados pelo Unity incluídos no snapshot.
