# v2.3.3 - AI Refresh Global

## Objetivo

Consolidar a leitura de mundo da IA entre uma jogada e outra, aproximando o fluxo normal do comportamento observado apos save/load: decisoes passam a acontecer sobre um tabuleiro sincronizado, com FoW, setores, plano e demandas reconstruidos.

## Principais mudancas

- Criada barreira central `CommitAIWorldAfterAction`.
- A IA agora consolida o mundo:
  - no inicio do turno;
  - apos cada acao efetiva de unidade na Fase 2;
  - antes da Fase 3 de compras;
  - apos cada compra executada.
- O commit sincroniza celulas das unidades, atualiza FoW, solicita rebuild do `SectorManager`, espera o rebuild do proximo frame e reconstrui snapshot/plano/analisador tatico.
- Logs `[AI Commit]` foram adicionados para comparar o trilho normal com o estado pos-load.

## Iniciativa

- Combate local de assalto agora preempta progressao/revelacao.
- Tanques/APCs com ataque valido contra ameaca visivel entram na fila antes de capturadores que apenas estavam sendo puxados para observacao ou perseguicao.
- Observador avancado continua ativo, mas nao passa mais na frente de combate local adequado.

## Compras

- A demanda ofensiva por segunda artilharia anti-infantaria agora exige screen suficiente.
- Com uma unica peca de fogo indireto ativa e pouca tropa de cobertura, a IA tende a preservar demandas de transporte/captura em vez de comprar outro foguete pesado cedo demais.
- O log de demanda passou a exibir `screen2` para explicar quando a segunda artilharia ofensiva foi liberada ou bloqueada.

## Resultado esperado

- Menos divergencia entre jogar direto e jogar apos load.
- Menos decisoes baseadas em plano ou FoW defasados.
- Menos artilharia alocada/comprada fora de hora por leitura antiga de setor quente.
- Mais consistencia em captura oportunista e captura ao terminar movimento sobre construcao.
