# Relatório de 2026-11-11 — continuação após v10.9.0

O fechamento anterior permanece no relatório versionado. Este documento registra o trabalho posterior.


### Combate aéreo com vaga de montagem no rally

- Decisão do autor (2026-11-11), caso T11 Golf: aeronaves com vaga em RallyAssembly montando podem combater nos arredores da posição do montinho; a vaga deve atraí-las ao rally, não a uma presa distante.
- `AIController.AirCombat.cs`: `TryResolveAirRallyAssembly` consulta a vaga publicada por `ResolveAnyAssignedObjective` e o estado de montagem. A âncora passa a abrir pela área do rally (`Rally:Montagem@Golf`): já presente, célula atual; ausente, célula mais próxima da Vanguarda (sem faixa, célula do rally). Depois do tiro local, aproxima/segura; não entra em caça operacional, interdição, negação de escolta, patrulha do capitão ou troca por plataforma nesse ramo. Emergências que já precedem o papel continuam no roteador.
- A coleta Tática descarta a célula de tiro fora de `IsRallyParticipantPresent(... Tactical, moveTo)` antes de rodar PodeMirar. Registra candidato `Ataque.Policy`, status blocked, gate `rally_area`, motivo `fora da área do montinho`. Área e sensores existentes; sem raio novo.
- O resolvedor chamado “Any” só cobria Capturador/Assalto/FogoIndireto. Completado em `AIController.Initiative.cs` para reconhecer também as demais vagas preenchidas (incluindo Interceptador/AtaqueAereo), preservando a prioridade anterior dos três resolvedores. A vaga do plano é autoridade, não depende do badge `AIHasAssignedPlan`.
- Sem vaga de rally montando, inclusive GoGreen, o ramo antigo conserva caça, interdição e escolta. A prioridade operacional do interceptador deixa de substituir o tiro local apenas durante montagem.
- `verificar_ar_no_rally.cs`: PASS fora do Play. Bombardeiro a 10h, soldado inimigo a 1h: recusa com gate rally_area e aproxima da Vanguarda mais próxima; dentro da área ataca com célula de tiro válida; sem alvo segura; sem vaga e GoGreen voltam ao ataque antigo. Vagas de Interceptador e Disruptor reconhecidas. Nenhum batch executado ou estado confirmado comprometido.
- Regressões solicitadas PASS: `verificar_prontidao_rally`, `verificar_caca_a_controle_ceu`, `verificar_desembarque_rally_aereo`. `bash tools/compilar.sh`: ambos assemblies verdes. Nada observado em Play; confirmação do caso Golf T11 continua para o autor. Um commit de correção e parada para revisão.
