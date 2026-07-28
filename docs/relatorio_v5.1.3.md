# v5.1.3 — Refinamento: Embarque de Capturadores com Quero Carona

## Objetivo

Este checkpoint corrige duas causas que faziam transportadores abandonarem
capturadores próximos ou aguardarem em pontos onde o embarque ainda não podia
ser concluído:

1. vários capturadores usavam a mesma construção vazia para recusar carona;
2. `ReachableNow` considerava apenas a aproximação do passageiro e não
   reservava movimento para a ação de embarque.

O cenário de laboratório possuía:

- uma construção vazia;
- vários soldados próximos;
- nenhuma segunda construção dentro do Operational;
- transportadores disponíveis;
- construções distantes que exigiam transporte.

## Diagnóstico da construção duplicada

`QueroCaronaService` avaliava cada unidade isoladamente:

> Existe alguma construção capturável que esta unidade alcança?

Como a construção estava vazia, todos os soldados respondiam positivamente.
Cada um recusava carona, embora somente um fosse necessário para capturá-la.

O transportador recebia:

- vários `OpportunisticFallback` com ajuste `-5000`;
- poucos pedidos `Requested` com ajuste `+1000`;
- demanda aparentemente válida apenas em regiões distantes.

Por isso algumas decisões que pareciam aleatórias eram coerentes com uma lista
incorreta de pedidos.

## CaptureOpportunityClaimService

Foi criado um planejamento coletivo e puro de oportunidades de captura.

Para cada snapshot confirmado, o serviço:

1. reúne capturadores ativos do slot;
2. reúne construções capturáveis;
3. calcula alcance Operational por caminhos válidos;
4. preserva a prioridade do plano formal;
5. distribui as oportunidades restantes pelo custo real de rota;
6. atribui no máximo uma construção por capturador;
7. atribui no máximo um capturador por construção;
8. produz uma projeção cacheada e somente leitura.

O matching prioriza cardinalidade: quando existem várias construções, ele tenta
atender o maior número possível de capturadores antes de usar custo e ID como
desempates determinísticos.

As reivindicações não:

- ocupam construções;
- alteram `TeamObjectivePlan`;
- reservam células definitivas;
- incrementam revisão;
- alteram FOW;
- sobrevivem à mudança do snapshot confirmado.

## Integração no Quero Carona

O desempate é aplicado somente a unidades compatíveis com
`UnitRole.Capturador`.

Uma construção reivindicada por outro capturador deixa de justificar a recusa
de carona. A unidade procura uma segunda oportunidade dentro de seu
Operational. Quando nenhuma existe, passa a solicitar transporte.

Logs do vencedor:

```text
[reserva 1:1 capturador=#X]
Recusa carona.
```

Logs dos demais:

```text
1 oportunidade reservada 1:1 para outro capturador (#X)
Aceita carona.
```

O estado da reivindicação participa da chave do cache de `QueroCarona`.
Movimento confirmado, ação, morte, embarque, reparo, mudança das construções ou
do plano invalidam a resposta correspondente.

## Hotzone materializável de embarque

Um passageiro não está `ReachableNow` apenas porque consegue terminar seu
movimento perto do transportador.

O encontro imediato agora exige:

```text
custo do caminho até a posição de embarque
+ custo oficial para entrar na célula do transportador
<= movimento restante do passageiro
```

Isso substitui qualquer necessidade de hard-code como `Tactical - 1`.

### Terreno e obstáculos

- o custo do caminho vem de `UnitMovementPathRules`;
- desvios e obstáculos aumentam a primeira parcela;
- o custo de embarque vem de `PodeEmbarcarSensor`;
- terreno e overrides de skill entram na segunda parcela;
- o mesmo cálculo oficial valida a execução real.

Exemplo com soldado de 3 pontos:

```text
caminho=2 + embarque=1 = 3  → ReachableNow
caminho=2 + embarque=2 = 4  → ReachableLater
caminho=3 + embarque=1 = 4  → ReachableLater
```

### Fallback entre camadas

Quando o passageiro entra normalmente na célula do transportador, vale o custo
real do terreno.

Quando depende do fallback de transição — por exemplo, avião ou helicóptero
embarcando em navio — o custo é sempre `1`. A aeronave precisa conservar pelo
menos um ponto de movimento para concluir o embarque.

Terreno, contexto e requisitos de skill continuam sendo validados antes de
aceitar o fallback.

## Tactical e Operational

- `ReachableNow`: caminho e embarque cabem no movimento restante.
- `ReachableLater`: a soma cabe apenas no horizonte Operational.
- `NoCurrentRoute`: não existe encontro materializável no horizonte atual.

Um `Pickup Tactical` só pode usar `ReachableNow`. Uma opção Operational ainda
pode orientar a aproximação futura, mas não autoriza o transportador a gastar
a ação esperando fora da hotzone real do passageiro.

## Ranking e ferramentas

`MelhorEmbarqueOption` agora conserva separadamente:

- custo da rota do passageiro;
- custo de embarque;
- custo total da ação;
- estado `ReachableNow`, `ReachableLater` ou `NoCurrentRoute`.

O ranking usa o custo total. Consumidores de Assalto também usam esse total
como desempate.

Os logs runtime mostram:

```text
custoPax=<caminho>+<embarque>=<total>
```

`Tools > Transporte > Melhor LZ de Embarque` apresenta rota, embarque e total
separadamente.

## Validação runtime

Antes da correção, o Chinook #85 permaneceu em `(-10, 9)` para o Soldado #2,
embora o soldado não pudesse concluir aproximação e embarque no Tactical.

Depois:

```text
[Transport:85:Pickup] Tactical:hit
passageiro=#2
encontro=(-10, 10, 0)
rotaPax=ReachableNow
custoPax=2+1=3
dist=1
```

O Chinook aproximou sua LZ em um hex:

```text
Chinook #85 vai de (-10,9) até (-10,10).
```

O passageiro passou a possuir um encontro realmente materializável:

- caminho: 2;
- embarque: 1;
- total: 3;
- orçamento: 3.

## Caos operacional permitido

Não foram criadas reservas permanentes de corredores ou células
intermediárias.

Outras unidades continuam livres para cumprir suas próprias agendas. Se uma
delas bloquear posteriormente o caminho:

- o snapshot confirmado muda;
- passageiro e transportador reavaliam;
- uma aproximação alternativa pode ser escolhida;
- a carona pode ser adiada sem produzir ação ilegal.

O objetivo é garantir legalidade e capacidade de replanejamento, não uma
coreografia perfeita entre todas as unidades.

## Refactor futuro

A proposta completa foi extraída para:

[quero_carona_refactor.md](quero_carona_refactor.md)

O próximo desenho transforma o resultado booleano em uma declaração de
intenção:

- capturar;
- pressionar setor;
- revelar FOW;
- Vigilância Aérea;
- suporte logístico;
- reparo ou evacuação;
- suporte de pouso.

O passageiro declarará a finalidade; o transportador decidirá somente como
materializá-la.

## Validação técnica

- `Assembly-CSharp` compilado;
- `Assembly-CSharp-Editor` compilado;
- 0 erros;
- 0 avisos;
- diff de arquivos rastreados sem erros de whitespace;
- contrato transacional preservado;
- nenhum estado definitivo é alterado pela consulta de reivindicação ou pelo
  ranking de encontro.
