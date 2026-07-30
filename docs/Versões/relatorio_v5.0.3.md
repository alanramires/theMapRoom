# v5.0.3 — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 3/8

## Visão geral

Esta versão conclui a terceira parte do plano de otimização: retirar as
varreduras completas de mapa executadas por `MelhorPouso` e
`MelhorEmbarque`.

As duas ferramentas passam a consumir o `BoardTopologyIndex` criado na Parte
2. A mudança substitui somente a descoberta repetida da geografia. Alcance,
ocupação, plataforma, slot, skill, combustível e legalidade continuam sendo
avaliados no estado atual pelos mesmos serviços e sensores.

## Melhor local de pouso

`MelhorPousoService` deixa de percorrer
`map.cellBounds.allPositionsWithin` na rota normal.

A consulta agora combina:

- superfícies potenciais de pouso preparadas pelo índice;
- células de plataformas móveis atualmente ativas;
- remoção de duplicatas;
- ordenação equivalente à travessia histórica do tilemap.

Plataformas móveis não são inseridas no índice permanente. Elas continuam
sendo avaliadas a cada consulta porque posição, vaga, exclusividade e
passageiros podem mudar durante a partida.

Depois da seleção barata de candidatos, permanecem inalteradas:

- inspeção dos ocupantes da superfície;
- compatibilidade de domínio e altura;
- alcance tático e operacional;
- `PodePousarSensor`;
- `AirOperationResolver`;
- disponibilidade e compatibilidade do slot da plataforma;
- pontuação e ordenação final.

Portanto, uma célula indexada é apenas uma candidata. O índice nunca concede
autorização de pouso.

## Melhor LZ de embarque

`MelhorEmbarqueService` deixa de percorrer o retângulo inteiro de
`cellBounds`.

A enumeração usa as células pintadas já hidratadas pelo
`BoardTopologyIndex`. Como o índice pode reunir camadas compatíveis da mesma
grade, a verificação `map.HasTile` é preservada para manter exatamente o
universo histórico da ferramenta.

Cada célula continua sendo submetida a:

- `PodeEmbarcarSensor.IsTransporterCellValidForEmbark`;
- validação de pouso quando o transportador é aéreo;
- tier tático, operacional ou estratégico;
- alcance atual e futuro de cada passageiro;
- compatibilidade e disponibilidade de slot;
- resultado de `QueroCarona`;
- pontuação e ordenação existentes.

Nenhuma regra de terreno, estrutura, construção ou ficha foi transferida para
o serviço de ranking.

## Plataformas móveis

O pouso precisa considerar uma exceção à topologia estática: conveses e
plataformas transportadoras podem mudar de célula.

Por isso a ferramenta percorre somente as unidades ativas e acrescenta a
célula de uma plataforma quando `CanLandOnTransporter` confirma que ela pode
receber a aeronave naquele snapshot.

No processamento da célula, a plataforma é novamente encontrada pela consulta
oficial de ocupação. Essa segunda verificação preserva a mesma autoridade usada
antes da otimização e evita transformar a lista local de candidatos em
reserva de vaga.

## Compatibilidade determinística

O índice passou a expor `IndexedCells`, hidratado uma vez e ordenado na mesma
sequência usada historicamente por `BoundsInt`:

1. camada Z;
2. coordenada Y;
3. coordenada X.

Praias, costas, pouso, embarque e desembarque recebem a mesma ordenação. Isso
preserva desempates que dependam da estabilidade da lista mesmo quando apenas
um subconjunto de células é visitado.

O reconhecimento de pistas por par estrutura+terreno também passou a aceitar
um terreno equivalente pelo ID, igual ao comportamento do
`AirOperationResolver`. Índices produzidos com uma associação de asset antiga
são detectados pelo fingerprint e reconstruídos uma vez.

## Instrumentação

As consultas registram:

- `TopologyIndexQueries`;
- `TopologyIndexHits`;
- `TopologyIndexMisses`;
- `TopologyIndexCandidateCells`;
- `TopologyCellsVisited`;
- `CellsVisited`.

`TopologyFullScans` só é incrementado pelo caminho de compatibilidade. Em uma
cena normal, o bootstrap ou o fallback de load já fornece o índice e esse
contador permanece em zero durante as decisões.

Isso permite comparar diretamente a linha de base da Parte 1 com a quantidade
real de candidatos visitados depois da migração.

## Fallback de compatibilidade

Se uma ferramenta de Editor ou cena de desenvolvimento não fornecer um índice
válido e também não permitir sua criação em runtime, o comportamento antigo é
mantido.

Esse fallback:

- percorre `cellBounds`;
- incrementa `TopologyIndexMisses`;
- incrementa `TopologyFullScans`;
- produz as mesmas verificações e resultados históricos.

O fallback não é o caminho esperado das partidas. Sua presença evita quebrar
ferramentas isoladas e cenas auxiliares enquanto o conteúdo é migrado.

## Contrato transacional

As listas consultadas pelo `BoardTopologyIndex` contêm somente geografia
imutável. A lista de plataformas é local à chamada e descartada ao final.

Nenhuma das duas ferramentas:

- move unidade;
- reserva slot;
- altera ocupação;
- incrementa revisão confirmada;
- invalida cache;
- atualiza FOW;
- persiste posição provisória.

Sensores continuam observando o snapshot fornecido pelo fluxo atual, mas a
consulta não publica esse estado como verdade confirmada. Cancelar uma ação
não modifica o índice e não deixa candidato ou plataforma armazenados.

## Validação técnica

- `Assembly-CSharp.csproj` compilado sem erros;
- `Assembly-CSharp-Editor.csproj` compilado sem erros;
- `git diff --check` aprovado;
- a rota indexada preserva `map.HasTile`;
- superfícies móveis permanecem fora da topologia permanente;
- os avisos encontrados já pertenciam ao projeto;
- nenhuma cena ou arquivo gerado foi alterado.

## Próxima etapa

A Parte 4 implementará o `ConfirmedOccupancyIndex`, separado da topologia.

Ele reunirá unidades ativas, ocupantes por célula e camada, transportadores e
perfis logísticos a partir do snapshot confirmado. Sua revisão só poderá mudar
depois de ações comprometidas e do retorno a `CursorState.Neutral`.
