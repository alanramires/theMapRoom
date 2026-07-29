# Desembarque de unidades militares

## Versão

`v5.1.6`

## Objetivo

Consolidar o desembarque militar como a continuação transacional do transporte:
o passageiro mantém sua intenção, o Melhor LZ de Desembarque escolhe a posição
do transportador e o ponto de saída de cada carga, e o batch normal materializa
movimento e desembarque sem criar uma política paralela.

O princípio deste checkpoint é:

> A LZ pertence ao transportador; o destino pertence ao passageiro. O Melhor
> LZ combina os dois e sua escolha vencedora deve chegar intacta à escrita do
> batch.

## Ferramenta centrada no passageiro

`Tools > Transporte > Melhor LZ de Desembarque` passou a permitir:

- selecionar diretamente um passageiro embarcado;
- inferir automaticamente seu transportador;
- selecionar um transportador e escolher uma carga na grade;
- detectar a unidade do batch preparado pelo F11;
- estudar apenas um passageiro ou toda a carga em conjunto;
- respeitar o destino de captura já designado quando não existe alvo manual;
- informar dois hexes desejados independentes, um para cada carga;
- manter a mesma ordem FIFO das vagas na grade e no cálculo.

A seleção comum da Scene não altera mais silenciosamente o contexto da
ferramenta. Os botões `Usar Selecionado`, `Usar como Transportador` e
`Auto Detect` tornam explícita a origem da análise.

## Leitura visual

A apresentação da Scene View agora diferencia:

- amarelo: LZ vencedora do transportador;
- verde, laranja e vermelho: demais LZs do ranking;
- azul/ciano: hex onde o passageiro desembarca para a LZ selecionada;
- alvo da vaga 1 e alvo da vaga 2: destinos manuais independentes.

O amarelo representa a nota vencedora da LZ. O azul não é uma segunda escolha
do transportador: é o ponto de saída atribuído à carga.

## Matching conjunto e destinos por vaga

O serviço compartilhado aceita um filtro opcional de passageiro. Com filtro,
avalia somente a carga escolhida; sem filtro, preserva o matching conjunto.

Para duas cargas, o serviço:

1. ordena as vagas pela ordem real de embarque;
2. associa o primeiro destino manual à primeira carga;
3. associa o segundo destino manual à segunda carga;
4. usa a intenção automática individual quando um destino manual está vazio;
5. maximiza passageiros entregues;
6. desempata pela prioridade, rota restante e custo de movimento.

O comportamento rebelde de liberar duas cargas quando ambas conseguem seguir
a pé até seus objetivos continua preservado.

## Destino designado após save/load

Capturadores embarcados mantêm seu `DesignatedCaptureTarget`. O desembarque não
substitui essa reserva por outra construção apenas porque ela ficou
geometricamente mais próxima.

Quando duas cargas apontam para a mesma oportunidade reservada, a carga
prioritária preserva o objetivo e a outra permanece disponível para receber
outro destino, evitando que o transportador entregue um passageiro em uma
construção já destinada a outro capturador.

## Integração com o batch runtime

O runtime consulta o mesmo `MelhorDesembarqueService` usado pela ferramenta.
Quando existe LZ alcançável na rodada, o resultado fornece diretamente:

- `ActionCell`: LZ do transportador;
- spot exclusivo de desembarque por passageiro;
- alvo e rota restante de cada carga;
- ranking e nota utilizados na decisão.

O batch move o transportador para a `ActionCell` vencedora e encadeia o sensor
de desembarque. A Progressão continua sendo usada apenas quando a LZ final não
é alcançável naquela rodada; ela não pode substituir uma LZ tática válida por
um hex genérico incapaz de desembarcar.

## Regra de Fog of War

O filtro foi corrigido para refletir a regra oficial:

- transportador em célula visível: permitido;
- transportador em célula já explorada: permitido;
- transportador em preto desconhecido: negado;
- passageiro: pode desembarcar em célula desconhecida, desde que
  `PodeDesembarcar` e os caminhos válidos autorizem a operação.

O foco do filtro de FOW é a LZ do transportador. O spot do passageiro não recebe
uma segunda proibição de visibilidade.

Descartes de LZ desconhecida agora aparecem explicitamente como:

```text
reason=transporter_cell_not_visible_or_explored
```

## Contrato transacional

O cálculo do Melhor LZ permanece somente leitura. Ele não move unidades, não
consome recursos, não ocupa células e não altera FOW, detecção, captura,
passageiros ou `HasActed`.

Movimento, pouso temporário, escolha dos spots e desembarque são confirmados
pelo fluxo normal. O estado definitivo só é publicado depois do compromisso da
ação e do retorno a `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj`: compilado com 0 erros;
- `Assembly-CSharp-Editor.csproj`: compilado com 0 erros;
- ferramenta e runtime compartilham o mesmo serviço de ranking;
- teste observado: Chinook escolheu LZ tática válida, moveu até ela e
  desembarcou o Bazooka no spot calculado;
- compromisso final publicou `UnitActed, MultiUnitChanged` e retornou a
  `Neutral`.
