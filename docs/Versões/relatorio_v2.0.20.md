# Relatorio de Atualizacao - v2.0.20

## AI Defesa

Esta versao consolida a rodada de ajustes defensivos da IA, com foco em proteger Base/HQ, responder a blindados proximos, limpar planos incoerentes e tornar reparo, transporte e apoio mais consistentes.

## Em uma frase

A IA passa a tratar ameaca no quintal da base como prioridade real: compra contra-medidas melhores, evita apoio sem capturador, reorganiza transporte vazio e usa construcoes de BaseX para reparo sem travar por estado defensivo.

## O que isso trouxe na pratica

- BaseX agora e interpretada pelo dono do HQ do setor; Base inimiga gera ataque, nao SOS de defesa.
- Planos ofensivos sem capturador deixam de manter Assalto, Fogo Indireto ou Transportador sozinhos no setor.
- Transporte vazio perto demais do objetivo deixa a vaga logistica e volta ao pool para shuttle/HQ.
- Capturador perto do objetivo nao embarca sem necessidade quando consegue chegar andando em 1 ou 2 turnos.
- Transporte atribuido prioriza buscar passageiro antes de lutar quando foi alocado para pickup.
- Assalto em avanco pode atacar alvo no caminho quando o scan do setor encontrou inimigos relevantes.
- Reparos em BaseX aceitam qualquer construcao propria completa do setor, nao apenas HQ.
- Construcoes de BaseX seguem validas para reparo mesmo se o plano local esta em defesa.
- Celula home ocupada vira apenas fallback de reparo; uma fabrica livre deve vencer o HQ ocupado.
- HUD de base propria usa `#` em vez de `B`, evitando conflito visual com setor Bravo.
- Servico do Comando atualiza o HUD dos alvos atendidos apos recuperar municao.

## Compras defensivas

1. Ameaca blindada perto da base
- A IA detecta blindados visiveis perto de Base/HQ em raio defensivo ampliado.
- Com caixa suficiente, `Artilharia de Campanha` entra como resposta suprema de fire support elite 2.
- Se nao houver caixa para a defesa elite/suprema nem para elite tank, a IA abre fallback anti-blindado acessivel.

2. Ordem de fallback anti-blindado
- Primeiro tenta unidade `Assalto/FogoIndireto`, como `Obus Leve`.
- Depois tenta `FogoIndireto`, como lancador/ASTROS quando disponivel.
- Por ultimo aceita `Assalto/Capturador`, como `Bazooka`.
- Soldado comum e massa basica deixam de passar na frente quando a ameaca e blindada e existe resposta anti-armor acessivel.

3. Reserva e gasto
- Reserva de elite fire support nao bloqueia mais uma compra defensiva urgente quando a ameaca ja esta no quintal.
- Tanque leve ainda pode ser comprado, mas nao deve atropelar a fila anti-blindado quando a regra de fallback esta ativa.
- O log agora explicita quando entrou `defesa blindada`, fallback anti-blindado ou defesa suprema.

## Planejamento defensivo

1. Dono correto de BaseX
- O setor BaseX passa a ser considerado proprio apenas quando o HQ daquele setor pertence ao time da IA.
- Isso impede `SOS Base2` para o time verde quando Base2 e a base vermelha.
- O mesmo setor, nesse caso, passa a ser alvo ofensivo normal.

2. Apoio precisa de capturador
- Assalto, Fogo Indireto e Transporte nao ficam pendurados em plano ofensivo sem Capturador.
- Se o plano perde o capturador, essas unidades sao liberadas para reatribuicao.
- Isso reduz casos de APC ou assalto sozinho guardando Delta sem ninguem capaz de capturar.

3. Estabilidade durante Phase2
- Ajustes de SOS/defesa nao removem slots no meio da execucao da Phase2.
- Isso evita o caso em que o transporte se move para buscar passageiro e o plano muda antes do capturador embarcar.

## Transporte e embarque

- Capturador so embarca quando a distancia ao objetivo justifica o transporte.
- Transporte vazio proximo do alvo e sem carga util volta a procurar passageiro ou reatribuicao.
- Se o APC esta em plano de pickup, buscar passageiro vence ataque oportunista.
- Transporte continua podendo apoiar combate quando o plano realmente precisa de apoio defensivo.

## Reparo

- BaseX e HQ nunca sao bloqueados por estarem em estado defensivo.
- Qualquer construcao propria completa de BaseX serve como destino de reparo.
- Se uma celula home esta ocupada, ela fica como fallback com penalidade alta.
- A IA deve preferir uma fabrica livre da Base1 em vez de tentar marchar para um HQ ocupado.

## UI e debug

- Badge de base propria normal virou `#`.
- `!` continua indicando defesa critica.
- `>>` continua indicando ataque contra base inimiga.
- O Servico do Comando agora refresca o HUD dos alvos tocados e tambem atualiza imediatamente quando recupera municao.
- Isso evita a impressao de bug em que a munição so aparecia correta apos selecionar a unidade.

## Bloco tecnico curto

- Ajustado `AIController.PlanEvaluator.cs` para ownership de BaseX por HQ, limpeza de apoio sem capturador, liberacao de transporte vazio perto do alvo e badge `#`.
- Ajustados fluxos de Phase2 em `AIController.Phases.cs` para evitar mutacao de plano durante execucao de lote.
- Ajustados `AIController.Capturer.Embark.cs` e arquivos de transporte para evitar embarque inutil e priorizar pickup.
- Ajustado `AIController.Assault.cs` para ataque oportunista no caminho em modo de avanco.
- Ajustado `AIController.Repair.cs` para destino de reparo em BaseX e fallback de home ocupado.
- Ajustado `AIShoppingPlanner.cs` para defesa blindada, artilharia suprema e fallback anti-blindado.
- Ajustado `TurnStateManager.CommandService.cs` para refresh de HUD apos servico de comando.

## Resultado

Versao preparada como pacote `AI Defesa`, focada em deixar a IA mais coerente quando a base esta sob pressao e em reduzir decisoes defensivas visualmente ou taticamente confusas.
