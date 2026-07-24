# v4.1.10 - Separar AI por SlotID

Esta versão conclui a sexta etapa da migração de identidade dos participantes. O ciclo operacional, os planos, os caches e os handlers da IA deixam de considerar `TeamId` como identidade e passam a operar pelo slot lógico do jogador.

O cenário de referência continua sendo uma partida com dois participantes vermelhos. `slot 2` e `slot 3` podem usar a mesma cor, mas cada IA controla somente suas próprias unidades, mantém seus próprios planos e considera o outro slot um adversário.

## Ciclo e Stages da IA

- O runtime da IA passou a armazenar `currentAISlotIndex`.
- A retomada de turno compara slot, turno e Stage, impedindo que uma IA reutilize o estado de outra IA da mesma cor.
- O ciclo Stage 0–4 recebe o `PlayerSlotId` ativo.
- Execução manual de Stage também resolve e preserva o slot exato.
- O identificador visual `currentAITeam` permanece apenas para cores, nomes, logs e compatibilidade.

## Snapshot e seleção de unidades

- `AIWorldSnapshot` possui construção explícita por `PlayerSlotId`.
- `MyUnits`, `MyBuildings`, QG, orçamento e renda são coletados pelo slot.
- Unidades de outro slot com a mesma cor entram como inimigas, respeitando o FOW daquele observador.
- A fila da Stage 2 contém apenas unidades cujo `SlotIndex` pertence à IA ativa.
- Iniciativa, preparação de ataque e seleção de apoio não puxam peças de outro slot.

## Planos e caches

- `TeamObjectivePlan` passou a persistir `SlotIndex`.
- `ObjectiveManager` consulta, cria e limpa planos por slot.
- APIs legadas por `TeamId` somente resolvem o slot ativo correspondente ou uma associação inequívoca.
- Operações táticas, intenções de setor, contexto macroterritorial, memória de rally, monitor de invasão, ameaças e contadores de reparo são separados por slot.
- Estado Go Green e supressão de rally usam o slot na chave.
- Exportação e restauração de planos, rallies e monitores preservam o participante exato.

## Decisões e handlers

Os filtros de propriedade e relação foram migrados de `TeamId` para `SlotIndex` nos principais sistemas da IA:

- assalto e combate aéreo;
- captura, defesa, exploração, perseguição e unidades rogue;
- embarque, desembarque e troca de transportadores;
- apoio de fogo;
- inteligência;
- logística, supply e reposicionamento;
- reparo e recuperação;
- transporte, shuttle, evacuação e courier;
- avaliação de objetivos, defesa, âncoras, progressão e compras.

Com isso, duas IAs da mesma cor não abastecem, transportam, reparam, escoltam, comandam ou reservam as unidades uma da outra.

## Save e compatibilidade

- O formato de save foi atualizado para a versão `13`.
- O runtime persiste `aiRuntimeSlotIndex`.
- Planos de objetivos persistem `slotIndex`.
- O load restaura a IA pelo slot e deriva o `TeamId` visual da configuração daquele participante.
- Saves antigos tentam migrar por cor somente quando há uma associação inequívoca; durante o turno ativo, o slot ativo fornece o contexto correto.

## Contrato transacional

A migração altera identidade e roteamento, não os pontos de compromisso. A IA continua executando ações através do mesmo fluxo confirmado do jogador: nenhuma posição, recurso, FOW, inteligência ou estado `HasActed` é consolidado durante previews ou antes do retorno a `CursorState.Neutral`.

## Validação

- `Assembly-CSharp.csproj` compilado com sucesso.
- Zero erros de compilação.
- Permanecem avisos não bloqueantes já existentes no projeto.
- `git diff --check` concluído sem erros.
- A auditoria não encontrou dicionários da IA diretamente indexados por `TeamId` nem os filtros principais `TeamId == aiTeam`.

## Próximas etapas

- Executar o stress test completo com duas IAs vermelhas.
- Validar retomada por save em cada Stage para ambos os slots.
- Revisar replay, telemetria e APIs legadas externas ao diretório da IA.
