# v5.1.0 — Refinamento e Estudo da AI com as ferramentas novas

## Visão geral

Esta versão consolida o uso das ferramentas `Melhor X` como instrumentos de
decisão e estudo da IA. O trabalho reduz avaliações repetidas, reaproveita
alcances e snapshots já calculados, limita buscas aos candidatos estruturais do
tabuleiro e melhora a leitura do que a ferramenta sugere em comparação com o
que a política runtime realmente aceitaria.

O checkpoint também inclui os ajustes atuais do mapa de teste, construções,
terrenos, rodovias, trem de carga e fontes utilizados durante a investigação.

## Melhor LZ de Embarque: ferramenta e runtime

A janela `Tools > Transporte > Melhor LZ de Embarque` agora distingue dois
contextos:

- com o jogo parado ou pausado, apresenta somente o retrato atual;
- com o jogo rodando, apresenta o ranking bruto da ferramenta e a opção
  simulada pela política runtime de Pickup.

A comparação runtime:

- percorre `Tactical → Operational → Strategic`;
- descarta `OpportunisticFallback`;
- rejeita encontros sem rota materializável para passageiros terrestres;
- mantém a exceção de camada necessária para passageiros aéreos;
- identifica visualmente divergências entre a melhor nota bruta e a escolha
  que a política aceitaria.

O resultado bruto permanece amarelo e a opção runtime é identificada em azul e
com a marca `[RUNTIME]`. O painel informa que o filtro estratégico final de
segurança ainda pertence ao `AIController` durante a decisão real.

Essa apresentação é estritamente diagnóstica: não move unidades, não reserva
destinos e não altera FOW, ocupação, recursos, revisões ou caches confirmados.

## Quero Carona e embarque

`QueroCaronaService` passou a aceitar alcance Operational previamente calculado.
Isso permite que `MelhorEmbarqueService` construa o perfil de alcance do
passageiro uma vez e o reutilize na avaliação da necessidade de transporte.

O serviço ganhou cache limitado e condicionado ao snapshot confirmado. A chave
considera unidade, ficha, posição, movimento, HP, combustível, reparo, embarque,
domínio, camada, time, slot, contexto, setor, topologia, construções e revisão de
ocupação.

O cache é recusado quando o estado runtime não corresponde à ocupação
confirmada. Resultados de alcance fornecidos pelo chamador continuam sendo
tratados como dados de consulta, sem transferência de autoridade para o cache.

## Transporte aéreo e pouso

O resultado de `MelhorPouso` agora carrega a identidade do snapshot que o gerou:

- aeronave, mapa e banco de terrenos;
- origem;
- orçamento e quantidade de turnos;
- revisão confirmada de ocupação.

`QueroCaronaAerea` reutiliza esse resultado quando todos esses componentes ainda
correspondem ao estado atual. Caso contrário, reconstrói a consulta.

A verificação de emergência aérea deixou de executar toda a análise terrestre
de objetivos apenas para determinar necessidade de reparo. Isso remove uma onda
Operational desnecessária desse caminho.

## Melhor Estoque

`MelhorEstoqueService` pode receber:

- caminhos Tactical já calculados;
- custos Operational já calculados.

Os consumidores de Estoque e Logística repassam seus caminhos existentes, em
vez de iniciar outra varredura idêntica.

As origens fornecidas pelo chamador não são modificadas diretamente. Quando uma
correção local é necessária, o serviço cria uma cópia antes de acrescentar a
célula de origem.

As construções fornecedoras passaram a ser consultadas pelo
`BoardTopologyIndex`. Unidades fornecedoras usam o `ConfirmedOccupancyIndex`
quando ele pode servir consultas; o registro global permanece como fallback
para Editor e bootstrap.

## Melhor Desembarque

O desembarque passou a podar candidatos terrestres por células estruturais do
`BoardTopologyIndex`, reduzindo testes caros de `PodeDesembarcar` sobre células
que não podem formar uma LZ.

Aeronaves embarcadas preservam suas regras próprias de decolagem e não recebem
essa poda terrestre. Rotas de passageiros calculadas durante a fase são
reutilizadas e o cache correspondente é limpo no início de uma nova Fase 2.

Os dicionários de caminho reutilizados são clonados quando o serviço precisa
fazer ajustes locais.

## Instrumentação

Os logs de clique foram separados dos logs de spike de frame:

- `Show Click Spike Logs` controla `[PointerRaw]` e `[PointerSelect]`;
- `Show Frame Spike Logs` controla a observação de frames lentos;
- `Frame Spike Threshold Ms` fica associado visualmente ao seu toggle no
  Inspector.

Isso permite estudar entrada e decisão sem inundar o Console com categorias de
diagnóstico não relacionadas.

Foram mantidos e ampliados contadores de desempenho para distinguir:

- reutilização e construção de alcance;
- uso do índice de topologia;
- uso da ocupação confirmada e fallbacks;
- hits e builds de snapshots de pouso;
- candidatos estruturais de desembarque.

## Dados e cenário de estudo

O checkpoint inclui o estado atual dos dados usados na partida de investigação:

- ajustes no Trem de Carga;
- catálogo de construções e substituição de `Aeroporto Hidrobase` por
  `Hidrobase`;
- rodovias, Floresta e Planície;
- cena `Hot Seat 1 - Pvp`;
- materiais de fonte TextMesh Pro.

Essas alterações fazem parte deliberadamente do snapshot completo criado por
`git add .`.

## Contrato transacional

As novas consultas continuam obedecendo à regra de que nada é definitivo antes
do compromisso da ação.

- ferramentas de Editor e comparações runtime são observacionais;
- snapshots só são reutilizados quando sua identidade e revisão permanecem
  compatíveis;
- nenhum resultado de planejamento altera posição, ocupação, FOW, detecção,
  combustível, munição, HP, captura ou `HasActed`;
- caches confirmados não recebem estado provisório;
- a autoridade final continua nos sensores e no fluxo explícito de confirmação.

## Validação

- `Assembly-CSharp-Editor.csproj`: compilação concluída com 0 erros;
- os avisos apresentados são os avisos preexistentes do projeto;
- a comparação da janela não chama comandos da IA e não materializa ações;
- o relatório registra todo o worktree incluído neste checkpoint.

## Roteiro recomendado de teste

1. pausar a partida e consultar `Melhor LZ de Embarque`: deve existir apenas o
   retrato atual;
2. retomar a partida e consultar novamente: ranking bruto e política runtime
   devem aparecer juntos;
3. verificar um caso em que a opção bruta é `OpportunisticFallback`: a escolha
   runtime deve procurar o próximo pedido materializável;
4. repetir com passageiro terrestre, aeronave, trem e transportador naval;
5. observar os contadores para confirmar reutilização de alcance e snapshots;
6. cancelar ações provisórias e confirmar que nenhuma consulta sobrevive como
   alteração definitiva do tabuleiro.
