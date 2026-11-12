# Relatório de 2026-11-12

Continuação do trabalho registrado em `relatorio_2026-11-11.md`.


### Recuperação aérea — célula de atendimento livre

- Decisão do autor, marca T13 slot 0, Chinook #56: recuperação escolhia Metrópole Lima ocupada pelo Chinook #2 pairando; a execução rebaixava o movimento e deixava o ferido no mar.
- `TryEvaluateAircraftRecoveryAnchor` recebe o conjunto de ocupação aérea já montado pelo reparo. Recusa cada célula diferente da origem que esteja ocupada, tanto no Tático como no Operacional (ServiceCells de supridor ou célula da âncora). Sem alterar score, serviços disponíveis ou prioridade.
- `HasAircraftRecoveryWithinReach`, `EvaluateAircraftRecoveryReach` e `TryBuildAircraftRecoveryApproachAction` compartilham esse conjunto; uma âncora bloqueada não suprime mais o hover de emergência. A materialização do trecho Operacional também exclui destinos ocupados. O chamador local de emergência do EWACS monta o conjunto uma única vez e o passa para recuperação e hover.
- `verificar_recuperacao_aerea_ocupada`: PASS fora do Play. Chinook carregado com 21/60 de autonomia, cidade a 2h ocupada por outro Chinook pairando e cidade livre a 5h: escolhe a livre. Com orçamento Tático menor, devolve Operacional e materializa trecho livre. Só a cidade ocupada: HasRecovery falso e hover em solo pousável, nunca hold sobre mar. Carga, combustível, camada e HasActed preservados.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_carga_sem_tarefa_rally`, `verificar_recuperacao_aerea_celula_servico`, `verificar_ancora_fusao_retaguarda`. Os dois últimos adaptam a chamada por reflexão ao novo parâmetro explícito. `verificar_pouso_emergencia` não existe em tools/ai_test nesta revisão.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nada observado em Play, nenhum batch executado. Alterações do autor em fichas, cena e fontes não entram neste commit. Parada para revisão.


### Carga com tarefa atendida — capturável desatendido (nota fora do commit, por pedido do autor)

- Caso T13, Chinook #72: Hotel já atendido; India, objetivo do transporte, também ocupada por aliado. Antes do fallback de rally, `DecideAirCourierAction` consulta `FindNearestPlanlessCaptureTarget` para cada capturador aéreo sem alvo. O primeiro com alternativa vira principal; a entrega segue pelo fluxo existente, inclusive Operacional, vetos de atracação e estacionamento. Sem alternativa: rally próprio montando; sem rally: hold.
- Os endereços alternativos vivem apenas durante a decisão, com restauração em finally. O resolvedor de passageiro e o montador conjunto leem esse contexto antes da missão antiga; não mudam vagas, Mission Intent nem o planner. Sem um serviço novo de busca ou de LZ.
- Duas réguas existentes permanecem diferentes: o buscador planless descarta ocupação aliada na camada de captura, reivindicação coletiva e reserva; `TryResolveUnservedAirCapture` também considera missão/vaga de outro capturador com alcance Tático e pressão confirmada, podendo buscar alternativa no setor. Nenhuma delas foi reescrita ou unificada neste bloco. Esta observação fica no relatório fora do commit solicitado.
- `verificar_carga_sem_tarefa_rally`: PASS com missão Capture→Hotel persistida, Hotel e India ocupadas, cidade desatendida a 8h de India; aproxima e desembarca nessa cidade. Sem alternativa mantém entrega no Core; sem rally mantém hold; alvo original válido continua em Hotel. Missões/vagas intactas, contexto temporário não vaza.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_prontidao_rally`, `verificar_ar_no_rally`. `bash tools/compilar.sh`: ambos assemblies verdes. Nada observado em Play e nenhum batch executado. Nota deixada pendente no working tree de propósito; alterações do autor fora do commit.
