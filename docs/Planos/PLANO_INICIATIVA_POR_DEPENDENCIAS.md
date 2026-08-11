# Plano — iniciativa por dependências

## Estado

Proposta incremental. Não implementar como um refactor único.

Ponto de partida já existente:

- a Fase 2 monta e ordena a fila uma vez no seu início;
- grupos menores agem antes;
- `UnitData.aiInitiative` e HP funcionam como desempates finais;
- o MelhorEmbarque já publica para a fila o fato de encontro conjunto
  `Tactical + ReachableNow`, promovendo o transportador antes do passageiro;
- ainda existe uma promoção genérica de toda unidade classificada como
  `GameUnitClass.Helicopter` para o grupo 1.

## Problema

"Helicóptero age cedo" é uma aproximação por tipo de unidade. Ela mistura
profissões diferentes:

- Chinook preparando embarque;
- Apache preparando ou executando combate;
- helicóptero sem oportunidade relevante;
- futura aeronave de vigilância ou serviço.

O nome `Chinook` não é comparado no runtime. O hardcode atual é doutrinário:
**todo helicóptero recebe precedência**, mesmo quando não prepara nenhuma ação
de outra unidade.

## Princípio

> A unidade ganha iniciativa porque prepara uma oportunidade concreta, não
> apenas porque pertence a um tipo de ficha.

Fluxo desejado:

```text
serviço MelhorX consulta o snapshot confirmado
    ↓
planejamento publica um fato pequeno e observável
    ↓
Initiative transforma o fato em precedência temporal
    ↓
controller do papel reavalia o estado atual e materializa a ação
```

Os serviços não publicam ordens. Eles descrevem possibilidades. Plano, claim,
Mission Intent, promessa, beacon ou snapshot transformam essas possibilidades
em fatos consumíveis. A Initiative somente organiza quem deve consultar e agir
antes de quem.

## Modelo mental

A fila representa dependências entre produtor e consumidor:

```text
transportador  → passageiro
observador     → artilharia/atacante
bloqueador     → capturador que precisa do hex
vacater        → unidade que ocupará o espaço
```

Quem está à esquerda prepara a ação de quem está à direita.

Os grupos atuais continuam sendo a implementação inicial dessas camadas. Uma
ordenação explícita por grafo é possibilidade futura, não requisito deste
refactor.

## Execução em fases

### Fase 1 — tornar a fila explicável, sem mudar comportamento

Criar um motivo de iniciativa observável, por exemplo `InitiativeReason`, e
registrá-lo junto ao grupo:

```text
[grp=2 reason=TacticalPickup] Navio#3
[grp=2 reason=ImmediateAttack] Apache#8
[grp=1 reason=SurveillancePrep] Fragata#4
[grp=4 reason=NoDependency] Chinook#5
```

Requisitos:

- cada retorno antecipado de `GetInitiativeGroup` recebe um motivo;
- o comparador continua usando os mesmos números e desempates;
- o trace permite saber qual regra venceu quando várias eram verdadeiras;
- nenhuma chamada de serviço é feita dentro do comparador.

### Fase 2 — classificar as promoções por oportunidade

Separar os fatos que hoje ficam misturados nos grupos:

- `TacticalPickup`: MelhorEmbarque provou encontro Tactical alcançável agora
  por transportador e passageiro;
- `SurveillancePrep`: a unidade pode produzir informação útil para um
  consumidor ainda não agido;
- `ImmediateAttack`: existe combate relevante materializável agora;
- `VacateDependency`: outra unidade depende do hex ocupado;
- `RepairCorridorRelease`: ferido precisa liberar a progressão durante invasão;
- `FormalTransportHandoff`: transportador formal precisa posicionar-se antes
  do passageiro do plano.

Nesta fase ainda é permitido manter o fallback genérico de helicóptero enquanto
se confere, pelos logs, quais helicópteros dependem exclusivamente dele.

### Fase 3 — remover a promoção genérica de helicóptero

Remover `IsHelicopterInitiativeUnit → grupo 1` somente quando os cenários que
realmente precisam de precedência estiverem cobertos por fatos próprios.

Comportamento esperado depois da remoção:

| Situação | Precedência |
|---|---|
| Chinook com encontro Tactical conjunto | sobe por `TacticalPickup` |
| Chinook buscando passageiro em Operational | não precisa preemptar o passageiro nesta rodada |
| Apache com ataque imediato relevante | sobe por `ImmediateAttack` |
| helicóptero de vigilância preparando consumidor | sobe por `SurveillancePrep` |
| helicóptero sem oportunidade/dependência | segue plano ou rogue normalmente |
| qualquer helicóptero empatado no mesmo grupo | usa `UnitData.aiInitiative`, depois os demais desempates |

### Fase 4 — revisar o desempate do grupo 4

Hoje `TransportDistance` é calculada para capturadores e comparada antes da
iniciativa da ficha. Unidades não capturadoras recebem infinito. Isso pode
empurrar transportadores e outras peças para o fim mesmo quando sua
`aiInitiative` é melhor.

Depois que `TacticalPickup` estiver estável, conferir se esse desempate deve:

- valer somente em comparação capturador × capturador;
- virar um fato de progressão, em vez de regra geral do grupo 4; ou
- ser removido e deixar a proximidade atuar no ranking da ação, não na fila.

Não alterar esse ponto junto com a retirada do hardcode de helicóptero. São
duas mudanças comportamentais diferentes e precisam de cenários separados.

### Fase 5 — dependências explícitas, opcional

Se os grupos começarem a acumular exceções demais, representar precedências
diretas entre unidades:

```text
Navio#3 antes de Soldado#2
Observador#7 antes de Artilharia#4
Unidade#5 antes de Capturador#9 porque ocupa seu destino
```

Uma ordenação topológica resolveria as relações; grupo, `aiInitiative`, HP e
ID determinístico seriam fallback para unidades sem aresta e para ciclos.

Esta fase só se justifica quando os motivos observáveis mostrarem que os grupos
deixaram de representar bem as dependências reais.

## Guardas arquiteturais

- Ler apenas snapshot confirmado na montagem da fila.
- Nunca executar MelhorX repetidamente dentro de `Sort`/comparador.
- Consultar no máximo uma vez por sujeito e guardar um fato pequeno.
- Não mover, reservar, consumir recurso nem marcar `HasActed` ao calcular
  iniciativa.
- A fila escolhe a ordem; não congela a ação.
- Quando chegar sua vez, o controller reavalia o tabuleiro confirmado atual.
- Promessa é farol distributivo, não lock de passageiro.
- Um fato que deixou de ser materializável não obriga o controller a executá-lo.
- Medir separadamente o custo de publicação dos fatos no setup da Fase 2.

## Cenários de aceitação

1. **Canal, encontro Tactical naval**
   - navio e soldado conseguem formar LZ conjunta na rodada;
   - navio age antes do soldado;
   - soldado reavalia e embarca na mesma rodada.

2. **Busca Operational**
   - Chinook ainda precisa de mais de uma rodada para encontrar o passageiro;
   - não recebe precedência apenas por ser helicóptero;
   - missão e promessa continuam preservadas normalmente.

3. **Apache com alvo imediato**
   - sobe pela oportunidade de combate, não pela classe helicóptero.

4. **Helicóptero ocioso**
   - sem coleta, observação ou combate materializável;
   - permanece no grupo correspondente ao plano/rogue.

5. **Observador e fogo de suporte**
   - observador que realmente habilita um tiro age antes do consumidor;
   - observador sem consumidor não ganha precedência artificial.

6. **Múltiplos transportadores**
   - todos podem ler os pedidos e promessas existentes;
   - faróis distribuem preferência sem impedir concorrência;
   - somente encontros Tactical materializáveis criam dependência de ordem na
     rodada atual.

7. **Cancelamento/rollback e replay**
   - cálculo da iniciativa não altera verdade confirmada;
   - ação continua começando e terminando em `CursorState.Neutral`;
   - replay observa a mesma ordem produzida pelos mesmos fatos confirmados.

## Ordem recomendada

1. Trace com motivo, sem mudança de ordem.
2. Auditoria dos motivos em cenários reais.
3. Fatos específicos para transporte, vigilância e combate.
4. Remoção isolada da promoção genérica de helicóptero.
5. Revisão isolada do desempate `TransportDistance` do grupo 4.
6. Grafo de dependências apenas se os traces demonstrarem necessidade.

