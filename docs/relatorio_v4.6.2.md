# v4.6.2 — Refactor da AI Transporte 2/4

## Objetivo

Executar a segunda parte do refactor do transporte: incorporar a estimativa de
necessidade do passageiro ao ranking plano do Melhor Embarque.

O `MelhorEmbarqueService` passa a combinar:

- oportunidade física de encontro;
- compatibilidade do passageiro;
- estado das rotas;
- necessidade indicada pelo `QueroCaronaService`.

A composição continua sendo uma consulta. Ela não decide movimento nem cria uma
ordem de pickup.

## Quero Carona dentro do Melhor Embarque

`MelhorEmbarqueRequest` agora aceita um avaliador de necessidade por passageiro.
A consulta é executada uma vez para cada passageiro estruturalmente elegível e o
resultado é reutilizado em todas as combinações passageiro–LZ.

Cada `MelhorEmbarqueOption` passa a armazenar:

- resultado completo de `QueroCaronaResult`;
- disposição da carona;
- ajuste produzido pela necessidade;
- motivo da estimativa;
- nota final da combinação.

## Disposições e pontuação

A necessidade é traduzida da seguinte forma:

- `Emergency`: bônus mínimo de `+2000`;
- `Requested`: bônus mínimo de `+1000`;
- `OpportunisticFallback`: penalidade de `-5000`;
- `NotEvaluated`: ajuste zero.

Os valores positivos respeitam uma eventual nota superior produzida pelo
`QueroCaronaService`.

A resposta `NÃO` não elimina o passageiro. Ela o transforma em oportunidade de
baixa prioridade, permitindo ao controller utilizá-lo futuramente quando não
houver trabalho mais importante.

## Elegibilidade estrutural e política legada

Foram separados dois conceitos antes misturados no mesmo filtro:

### Elegibilidade estrutural

Define quem pode aparecer no ranking novo:

- unidade aliada;
- unidade viva;
- unidade não embarcada;
- slot compatível;
- capacidade e exclusividade disponíveis;
- passageiro não reservado formalmente para outro transportador.

### Política legada

Continua alimentando somente a coleção consumida pelo controller atual. Ela ainda
considera:

- unidade que já agiu;
- participação em batalha;
- objetivo conhecido;
- possibilidade de chegar ao objetivo sem transporte;
- thresholds existentes do shuttle.

Com isso, logística, passageiro que já agiu ou unidade que recusou carona podem
ser observados e classificados sem alterar ainda a execução da IA.

## Ausência de rota

`ReachableLater` e `NoCurrentRoute` continuam válidos no ranking plano.

Não alcançar a LZ nesta rodada significa dificuldade circunstancial, não
incompatibilidade. A opção recebe sua penalidade de rota, preserva o resultado do
Quero Carona e permanece disponível para decisão posterior do controller.

## Integração preparatória no AIController

`TryQueryTransportPickupOperation` agora fornece ao Melhor Embarque:

- filtro estrutural;
- filtro separado da coleção legada;
- avaliação do Quero Carona;
- contexto de plano ou rogue/rebelde;
- envelope operacional;
- diagnóstico da necessidade.

O controller ainda escolhe e materializa pela coleção legada. A troca para o
ranking novo pertence à parte 3/4.

## Ferramenta Melhor Embarque

`Tools > Transporte > Melhor Embarque` agora mostra:

- disposição `Emergency`, `Requested` ou `OpportunisticFallback`;
- ajuste da carona;
- nota final;
- motivo completo retornado pelo Quero Carona;
- rota e custo do passageiro;
- rota e custo do transportador;
- LZ e envelope.

Em Scene/Edit Mode, a avaliação preserva a emulação de `IsUnderRepair` baseada
nos critérios de `UnitData > AI Behavior > Repair Decision`.

A ferramenta sincroniza os registros de unidades e construções antes da
consulta.

## Arquitetura transacional

- Quero Carona e Melhor Embarque permanecem consultas de leitura.
- A emulação de reparo não seta flags no `UnitManager`.
- Nenhum cálculo consome movimento, combustível, munição ou estoque.
- Nenhuma opção cria reserva ou `PlayerAction`.
- A materialização continua no controller.
- Nenhuma informação provisória é promovida a estado confirmado.

## Arquivos principais

- `Assets/Scripts/Match/AI/Services/MelhorEmbarqueService.cs`
- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`
- `Assets/Editor/MelhorEmbarqueWindow.cs`

## Próxima etapa

A parte 3/4 deve:

- fazer o controller consumir diretamente `MelhorEmbarqueOption`;
- remover a segunda escolha baseada apenas na distância ao objetivo;
- transportar passageiro, LZ, envelope, disposição e nota em
  `TransportOperationDecision`;
- centralizar a materialização do pickup;
- usar `OpportunisticFallback` somente depois das atividades prioritárias.

## Verificação

- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- auditoria da resposta `NÃO` preservada no ranking;
- auditoria de `ReachableLater` e `NoCurrentRoute`;
- auditoria da separação entre filtro estrutural e política legada;
- auditoria de ausência de mutações nas consultas;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
