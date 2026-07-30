# v4.8.4 — Refactor da AI Logística e de Estoque 4/5

## Visão geral

Esta etapa aproxima logística, transporte e recuperação de aeronaves de uma
mesma ideia: a unidade não deve apenas saber que precisa de ajuda; ela precisa
encontrar um ponto real onde essa ajuda pode acontecer. A IA agora usa as
regras de pouso, embarque e ocupação já presentes no jogo para escolher seus
encontros, em vez de supor que qualquer proximidade no mapa resolve a operação.

## Recuperação aérea ganha lugares de verdade para pousar

Quando uma aeronave entra em recuperação, a procura continua priorizando
supridores, aeródromos, plataformas e construções capazes de atendê-la. Se
esses caminhos não bastam, ela também pode avaliar locais de pouso autorizados
no Tactical e no Operational.

Isso permite que um caça com pouca autonomia avance para uma estrada, praia ou
outra LZ que sua própria ficha aceite, sem inventar uma pista inexistente nem
cruzar o mapa além do combustível restante. A decisão respeita ocupação da
superfície, domínio, camada final e skills exigidas.

Em uma plataforma naval compatível, a recuperação pode se completar como uma
operação composta: a aeronave aproxima-se do anel de embarque e entra no slot
da Fragata ou do Porta-Aviões pelo fluxo oficial de `PodeEmbarcar`.

## Uma LZ melhor para pouso e para embarque

`Tools > Operações Aéreas > Melhor Local para Pouso` recebeu filtros mais
precisos. A ferramenta descarta camadas aéreas, domínios que a aeronave não
opera, locais ocupados na banda de superfície e resultados fora da autonomia
real. Ela mostra as opções Tactical e Operational que ainda são possíveis
naquele turno.

O Melhor Embarque também passou a tratar resgates de forma mais natural. Em
EVAC, o transportador procura a faixa adjacente ao passageiro — que é onde o
embarque realmente acontece — em vez de ficar parado e esperar que a unidade
vulnerável atravesse sozinha toda a distância.

## Trem, estações e redes que não inventam caminhos

As regras de contexto de embarque foram alinhadas entre sensor e consulta. Um
Trem de Carga só considera construção que também tenha a estrutura e o terreno
exigidos pela sua ficha, como trilho sobre o contexto correto. A ferramenta e
o gameplay deixam de discordar sobre qual estação é utilizável.

Transportadores de estação também não promovem uma unidade sem rota até a LZ
a um pickup imediato. Isso preserva a diferença importante entre uma aeronave
que pode aproximar-se para um resgate e uma unidade terrestre que precisa de
uma rota materializável.

## Serviços e carga preventiva

O roteador dá a unidades de transporte que também funcionam como Hub uma chance
de verificar a rede de estoque antes de sair em busca de passageiros. Assim o
Trem de Carga, por exemplo, pode priorizar uma construção aliada vazia que
precise receber carga; se não houver transferência útil, volta às demais
atividades normalmente.

As construções receberam os limites preventivos de estoque configurados nesta
linha de trabalho, e o serviço de reparos foi normalizado junto de seus
arquivos `.meta`. As alterações de cenário acompanham esses dados para manter
os testes de logística reproduzíveis.

## Apoio de fogo e carona

Foi removida a proibição genérica que só permitia que uma peça rebocada
embarcasse durante `IsInvading`. Agora a Artilharia de Campanha participa das
mesmas consultas de `Quero Carona`, `Melhor Embarque` e `PodeEmbarcar` usadas
pelas outras unidades.

A escolha de uma posição segura de artilharia continua sendo uma preocupação
válida e será consolidada como avaliação própria: ela deverá distinguir a
retaguarda da linha de frente e a faixa útil de tiro, em vez de depender de um
flag global de invasão.

## Contrato transacional preservado

Todas essas consultas são puras. Avaliar uma LZ, uma plataforma naval, uma
estação ou uma necessidade de estoque não move unidades, não consome recursos,
não altera ocupação e não revela FOW. Embarque, pouso, transferência e serviço
continuam ocorrendo apenas pelos batches e compromissos oficiais.

## Validação

- build do runtime e do Editor sem erros;
- teste de aeronave recuperando rumo a LZ Operational dentro da autonomia;
- teste de Apache aproximando-se e embarcando em Fragata compatível;
- conferência de EVAC escolhendo um hex adjacente ao passageiro;
- conferência de LZ de pouso ocupada sendo descartada;
- conferência de estação ferroviária exigindo trilho e contexto válido;
- arquivos `.meta` preservados com os assets renomeados e adicionados.
