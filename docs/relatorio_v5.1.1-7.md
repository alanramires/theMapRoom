# v5.1.1-7 — Refinamento: Vigilância Aérea 7/8

## Objetivo

Extrair a cobertura de Vigilância Aérea para uma consulta pura e compartilhada,
evitando reconstruir o alcance AirLow e AirHigh para cada candidato sempre que
o Radar Móvel ou outro sensor aliado for avaliado.

Esta cobertura não substitui `Tools > Utils > Retaguarda`:

- `Retaguarda` descreve formação, vanguarda, flancos, linha aliada e distância
  segura atrás dos combatentes;
- a cobertura de Vigilância Aérea descreve o alcance efetivo do sensor sobre as
  camadas AirLow e AirHigh.

As duas avaliações são complementares.

## Consulta pura

Foi criado `AirSurveillanceCoverageService`.

A consulta recebe:

- unidade observadora;
- célula candidata;
- mapa e Terrain Database;
- configuração de altura aérea;
- política global de LoS;
- cobertura AirLow e AirHigh já fornecida pelos aliados.

Ela devolve:

- quantidade de células AirLow observáveis;
- quantidade de células AirHigh observáveis;
- ganho marginal AirLow;
- ganho marginal AirHigh;
- capacidade de detectar stealth em cada camada;
- pontuação estrutural de cobertura.

Nenhum candidato move unidade, pinta FOW, publica contato ou atualiza
inteligência.

## Fonte de verdade

O serviço não possui uma implementação paralela de visão.

Em um cache miss, ele consulta:

```text
PodeDetectarSensor.CollectVisibleAirCellsAt
```

Assim, alcance, LoS, montanhas, políticas por camada e configuração aérea
continuam obedecendo à fonte oficial de detecção.

## Cache estrutural

O resultado estrutural é armazenado por:

- identidade do mapa;
- identidade do Terrain Database;
- identidade da configuração aérea;
- perfil de UnitData;
- célula candidata;
- versão e fingerprint do `BoardTopologyIndex`;
- alcance AirLow e AirHigh;
- política de LoS por camada;
- domínio e altura do observador;
- estado do toggle global de LoS.

O cache não usa a revisão global causada por movimento de unidades. Terreno,
montanhas e topologia não mudam durante a partida, portanto uma unidade que
andou não invalida a geometria já calculada.

Os resultados internos são arrays imutáveis para os consumidores. A cobertura
aliada dinâmica é calculada fora do cache.

## Limite e descarte

O cache mantém até 4096 combinações estruturais. Ao atingir o limite, remove as
entradas mais antigas em ordem de inserção, sem limpar todo o conteúdo de uma
vez.

Isso evita o comportamento de reconstrução completa após um simples limite de
capacidade.

## Integração do Radar Móvel

O Radar Móvel usa a consulta compartilhada para:

- comparar a posição atual com células Tactical alcançáveis;
- detectar posições bloqueadas geograficamente;
- medir cobertura já fornecida por Radar ou EWACS aliado;
- valorizar ganho marginal;
- decidir permanecer `Stationary`;
- justificar transporte terrestre somente quando houver ganho operacional.

As coleções temporárias AirLow e AirHigh deixaram de ser recriadas para cada
candidato cacheado.

## Detecção stealth

A avaliação anterior chamava `CanDetectStealthFor` sem fornecer um alvo
stealth concreto. Essa API responde se uma unidade específica pode ser
detectada e, sem alvo, retornava falso.

Foi acrescentada a consulta de capacidade:

```text
UnitData.HasStealthDetectionFor
```

Ela verifica se o perfil possui especialização de detecção stealth na camada,
permitindo pontuar corretamente a capacidade estrutural do Radar ou EWACS.

## Métricas

O relatório de desempenho da decisão passa a registrar:

- `AirSurveillanceCoverageCacheHits`;
- `AirSurveillanceCoverageCacheMisses`;
- `AirSurveillanceCoverageCacheStores`;
- `AirSurveillanceCoverageCellsBuilt`.

Essas métricas permitem confirmar no turno real que células repetidas são
reutilizadas.

## Ideia futura preservada

Foi preservado em `docs/ideias_futuras.md` o desenho de unificação das intenções
do `QueroCarona`, incluindo captura, Vigilância Aérea, logística e suporte de
pouso. Essa ampliação permanece depois do refactor atual.

## Contrato transacional

- A consulta somente lê o snapshot e a topologia.
- A célula candidata é virtual e não altera a posição confirmada.
- O cache contém apenas geometria estática, não FOW ou contatos.
- Sobreposição aliada não é publicada como visão confirmada.
- Cancelamento não exige rollback do cache porque nenhuma verdade do tabuleiro
  foi modificada.
- FOW e detecção reais continuam recalculados somente após compromisso e
  retorno a `Neutral`.

## Validação

- `git diff --check` concluído sem erros.
- Runtime e editor compilados com zero erros.
- Os 417 avisos pertencem ao baseline atual.

## Próxima etapa

A Parte 8 integrará a cobertura exata ao ranking dos destinos do próprio EWACS.

Atualmente:

- o Radar Móvel já compara cobertura estrutural e marginal por candidato;
- o EWACS contribui para a cobertura aliada;
- o destino do EWACS ainda é ranqueado principalmente por retaguarda, envelope
  de `airVis`, ameaça, coesão e segurança de recuperação.

O próximo passo adicionará a cobertura AirLow/AirHigh marginal ao ranking do
EWACS sem permitir que ela vença emergência, combustível, pouso seguro,
retaguarda ou ameaça excessiva.
