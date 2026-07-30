# AI e FoW com Planos

## Resumo
- Planejador da IA refinado para gerar e priorizar planos variaveis por setor com criterio espacial.
- Coesao de esquadra melhorada para evitar escoltas se afastando do plano alocado.
- FoW endurecido para evitar regressao estrutural onde todas as unidades inimigas ficavam visiveis.

## Alteracoes principais
- `AIPlanEvaluator`
  - Priorizacao de setores proximos ao HQ com peso maior na selecao.
  - Alocacao de infantaria por distancia entre unidades e construcoes do setor.
  - Distribuicao entre planos considerando disputa espacial (evita preencher um plano sem avaliar alternativa proxima).
  - Composicao dinamica por setor (INF/ARM/ART/APC) e risco tatico por plano.

- `AIPlayerController`
  - Logs de plano com risco (`risco: N`) e unidades alocadas por funcao.
  - Log tatico por unidade com o plano de alocacao (`alocado: <plano> [papel]`).
  - Ajuste de coesao: unidades de escolta/reposicionamento priorizam objetivo do proprio plano antes de fallback global.
  - Fallback de captura para combate no mesmo turno quando captura falha e existe ataque valido apos mover.

- `AIPlannerWindow` / `AIIntelDebugWindow`
  - Preview de planos em cena (circulos por setor e linhas de alocacao).
  - Selecao visual de participantes do plano para inspecao de escolha.
  - Linhas mais espessas e circulos menores para leitura.
  - Planos fixos (defesa/ataque) sem participantes iniciam ocultos no overlay.

- `MatchController` (FoW)
  - Hardening em `SetFogOfWarDebugEnabled`: toggle de debug nao persiste mais o estado serializado estrutural de FoW.
  - Remocao de efeito colateral do editor que podia desligar FoW ao fechar janela de debug.

## Comportamento esperado
- Planos variaveis sao gerados por setor com foco em capturar o setor inteiro (todas as construcoes do setor).
- A alocacao inicial considera distancia relativa entre planos concorrentes para evitar decisoes espaciais ruins no opener.
- Escoltas permanecem mais coesas ao plano designado.
- FoW volta a esconder corretamente unidades inimigas fora da visao.

## Validacao
- Build `Assembly-CSharp`: sucesso.
- Build `Assembly-CSharp-Editor`: sucesso.
