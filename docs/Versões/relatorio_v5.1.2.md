# v5.1.2 — Refinamento: Repulsão entre Radar Movel

## Objetivo

Este checkpoint refina a distribuição das unidades de Vigilância Aérea.

O problema observado não era uma leitura desatualizada do `Commit Light`.
Depois que o primeiro Radar Móvel confirmava seu movimento, o segundo já
avaliava o tabuleiro com a nova revisão e a posição atualizada. O ranking,
porém, ainda atribuía valor excessivo à cobertura individual total, mesmo
quando grande parte dela já era fornecida por outro sensor aliado.

Na prática, dois sensores podiam escolher posições vizinhas porque a grande
área bruta de cobertura vencia o pequeno ganho marginal para o time.

## Nova política de cobertura

`AirSurveillanceCoverageService` agora separa explicitamente:

- cobertura nova, acrescentada ao time;
- cobertura sobreposta, já fornecida por outro sensor aliado;
- cobertura AirLow;
- cobertura AirHigh;
- capacidade de detectar unidades stealth.

A cobertura nova recebe quase todo o valor do ranking. A cobertura sobreposta
mantém somente um pequeno valor de redundância operacional.

O bônus de detecção stealth também incide principalmente nas células novas.
Assim, o sensor deixa de ser premiado por repetir maciçamente uma área já
observada.

## Repulsão entre sensores

Além da cobertura marginal, o ranking recebeu uma penalidade explícita de
proximidade entre unidades de Vigilância Aérea.

A regra vale para todas as combinações:

- Radar Móvel com Radar Móvel;
- Radar Móvel com EWACS;
- EWACS com Radar Móvel;
- EWACS com EWACS.

Cada unidade avalia a distância até o sensor aliado mais próximo. A separação
preferida é derivada de seu próprio alcance de visão aérea:

- Radar Móvel: normalmente 3 hexes;
- EWACS: normalmente 4 hexes;
- limite operacional da preferência: entre 3 e 5 hexes.

Quando a distância é menor que a preferida:

- cada hex faltante adiciona penalidade ao ranking;
- sensores adjacentes recebem penalidade adicional;
- ao alcançar a distância preferida, a penalidade desaparece.

A repulsão não é uma proibição absoluta. Emergência de combustível, pouso,
reparo, plataforma compatível, segurança, terreno, retaguarda e ganho de
cobertura continuam podendo justificar uma aproximação temporária.

## Integração

A nova pontuação foi integrada em:

- reposicionamento Tactical do Radar Móvel estacionário;
- escolha de destino operacional do Radar Móvel por transporte;
- ranking de cobertura do EWACS;
- comparação entre permanecer e mover;
- explicação textual das decisões da IA.

O cálculo é somente leitura. Ele consulta as posições confirmadas no snapshot,
não altera FOW, contatos, ocupação, revisão do tabuleiro ou estado das
unidades durante a avaliação provisória.

## Logs

Os logs de Vigilância Aérea agora expõem:

```text
low=<total>(new=<marginal>)
high=<total>(new=<marginal>)
overlap=<AirLow>/<AirHigh>
coverage=<utilidade para o time>
spacing=<distância atual>/<distância preferida>
repel=<penalidade de proximidade>
```

Isso permite distinguir:

- ganho real de observação;
- redundância de cobertura;
- afastamento desejado;
- exceções em que outra prioridade venceu a repulsão.

## Validação runtime

O cenário de teste possuía dois Radares Móveis inicialmente muito próximos.

### Radar Móvel #119

```text
spacing=1/3 repel=780
→
spacing=2/3 repel=260
```

O radar saiu da adjacência imediata e melhorou fortemente sua cobertura
marginal.

### Radar Móvel #91

Depois do movimento confirmado do Radar #119, o `Commit Light` publicou a
revisão seguinte do tabuleiro. O Radar #91 avaliou essa posição atualizada:

```text
spacing=2/3 repel=260
→
spacing=4/3 repel=0
```

O segundo radar abriu a formação, eliminou a penalidade e escolheu uma região
com cobertura majoritariamente nova.

O resultado confirma que:

- o snapshot entre batches estava atualizado;
- a falha anterior estava no peso do ranking, não no commit;
- os sensores passaram a se distribuir como uma rede;
- a cobertura marginal e a repulsão trabalham juntas.

## Validação técnica

- `Assembly-CSharp` compilado;
- `Assembly-CSharp-Editor` compilado;
- 0 erros;
- 0 avisos;
- arquivos de código alterados sem erros de whitespace;
- cena da partida de teste preservada no checkpoint solicitado.

## Próximo estudo

Após uma rodada completa de observação, o próximo refactor planejado é a
unificação e parametrização de `Quero Carona`.

Esse trabalho deve continuar separado deste checkpoint para que decisões de
Vigilância Aérea, transporte, captura, suporte logístico e recuperação aérea
possam ser comparadas com evidência clara da partida completa.
