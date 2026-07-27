# v4.6.1 — Refactor da AI Transporte 1/4

## Objetivo

Executar a primeira parte do refactor do transporte: estabelecer contratos
comuns e um ranking plano de combinações passageiro–LZ, sem alterar ainda a
política de decisão consumida pelo `AIController`.

Esta etapa transforma o Melhor Embarque em uma fonte de fatos comparáveis. O
serviço continua sem criar movimento, espera, reserva, ordem ou `PlayerAction`.

## Ranking plano passageiro–LZ

Foi criado `MelhorEmbarqueOption`. Cada opção registra:

- passageiro e posição atual;
- slot compatível;
- LZ;
- envelope do transportador;
- distância e custo de rota do transportador;
- estado e custo de rota do passageiro;
- disposição de carona;
- nota técnica;
- diagnóstico da combinação.

O resultado de `MelhorEmbarqueService` agora possui:

- `options`, ranking plano de combinações;
- `bestOption`, melhor combinação técnica;
- `ranking`, agrupamento legado por LZ;
- `rejectedPassengers`, rejeições estruturais.

## Estados da rota do passageiro

Foram introduzidos três estados:

- `ReachableNow`: o passageiro alcança a vizinhança da LZ com o movimento
  restante;
- `ReachableLater`: não alcança agora, mas existe rota dentro do envelope
  operacional calculado;
- `NoCurrentRoute`: nenhuma rota foi encontrada no horizonte consultado.

Ausência de rota atual deixa de apagar informação do diagnóstico. A combinação
continua no ranking plano e pode ser interpretada posteriormente pelo
`AIController`.

## Disposição de carona

O contrato já reserva os estados:

- `NotEvaluated`;
- `Emergency`;
- `Requested`;
- `OpportunisticFallback`.

Nesta primeira parte todas as opções permanecem `NotEvaluated`. A composição com
`QueroCaronaService` e suas notas pertence à parte 2/4.

## Compatibilidade com o comportamento atual

Para isolar esta etapa arquitetural:

- o ranking plano registra `ReachableNow`, `ReachableLater` e `NoCurrentRoute`;
- a coleção legada por LZ continua recebendo somente passageiros
  `ReachableNow`;
- `TryQueryTransportPickupOperation` continua consumindo a coleção legada;
- a política atual do controller não foi substituída;
- seletores antigos continuam presentes.

Assim, a fundação nova pode ser inspecionada antes de passar a comandar a escolha
da IA.

## Ferramenta Melhor Embarque

`Tools > Transporte > Melhor Embarque` agora exibe:

- ranking plano passageiro–LZ;
- envelope;
- slot;
- estado da rota do passageiro;
- custo da rota do passageiro;
- custo da rota do transportador;
- disposição;
- nota técnica;
- diagnóstico completo.

O agrupamento legado por LZ permanece visível e identificado, permitindo comparar
o contrato novo com a entrada ainda utilizada pelo controller.

A opção selecionada é destacada na Scene View, inclusive quando o passageiro está
em `ReachableLater` ou `NoCurrentRoute`.

## Desempenho da consulta

Os mapas de alcance atual e operacional do passageiro são calculados uma vez por
passageiro e reutilizados para todas as LZs. Isso evita reconstruir caminhos para
cada combinação.

Strategic continua opcional.

## Arquitetura transacional

- A consulta lê apenas o estado confirmado do tabuleiro.
- Nenhum ranking altera posição, ocupação, FOW ou detecção.
- Nenhum custo calculado é consumido.
- Nenhuma opção cria reserva ou ordem.
- A materialização continua sendo responsabilidade do `AIController`.
- O fluxo de compromisso permanece independente e retorna a
  `CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Match/AI/Services/MelhorEmbarqueService.cs`
- `Assets/Editor/MelhorEmbarqueWindow.cs`

## Próxima etapa

A parte 2/4 deve:

- consultar `QueroCaronaService` para cada passageiro;
- marcar `Emergency`, `Requested` ou `OpportunisticFallback`;
- aplicar bônus ou penalidade sem eliminar a resposta `NÃO`;
- preservar candidatos sem rota atual;
- produzir a nota bilateral que será consumida pelo controller na parte 3/4.

## Verificação

- `dotnet restore Assembly-CSharp.csproj`;
- `dotnet restore Assembly-CSharp-Editor.csproj`;
- `dotnet build Assembly-CSharp.csproj --no-restore`;
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`;
- auditoria da compatibilidade da coleção legada;
- auditoria de que `NoCurrentRoute` permanece no ranking plano;
- `git diff --check`;
- resultado: runtime e editor concluídos com 0 erros.
