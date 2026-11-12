# Relatório de 2026-11-12

Continuação do trabalho registrado em `relatorio_2026-11-11.md`.


### Recuperação aérea — célula de atendimento livre

- Decisão do autor, marca T13 slot 0, Chinook #56: recuperação escolhia Metrópole Lima ocupada pelo Chinook #2 pairando; a execução rebaixava o movimento e deixava o ferido no mar.
- `TryEvaluateAircraftRecoveryAnchor` recebe o conjunto de ocupação aérea já montado pelo reparo. Recusa cada célula diferente da origem que esteja ocupada, tanto no Tático como no Operacional (ServiceCells de supridor ou célula da âncora). Sem alterar score, serviços disponíveis ou prioridade.
- `HasAircraftRecoveryWithinReach`, `EvaluateAircraftRecoveryReach` e `TryBuildAircraftRecoveryApproachAction` compartilham esse conjunto; uma âncora bloqueada não suprime mais o hover de emergência. A materialização do trecho Operacional também exclui destinos ocupados. O chamador local de emergência do EWACS monta o conjunto uma única vez e o passa para recuperação e hover.
- `verificar_recuperacao_aerea_ocupada`: PASS fora do Play. Chinook carregado com 21/60 de autonomia, cidade a 2h ocupada por outro Chinook pairando e cidade livre a 5h: escolhe a livre. Com orçamento Tático menor, devolve Operacional e materializa trecho livre. Só a cidade ocupada: HasRecovery falso e hover em solo pousável, nunca hold sobre mar. Carga, combustível, camada e HasActed preservados.
- Regressões PASS: `verificar_desembarque_rally_aereo`, `verificar_carga_sem_tarefa_rally`, `verificar_recuperacao_aerea_celula_servico`, `verificar_ancora_fusao_retaguarda`. Os dois últimos adaptam a chamada por reflexão ao novo parâmetro explícito. `verificar_pouso_emergencia` não existe em tools/ai_test nesta revisão.
- `bash tools/compilar.sh`: Assembly-CSharp e Assembly-CSharp-Editor verdes. Nada observado em Play, nenhum batch executado. Alterações do autor em fichas, cena e fontes não entram neste commit. Parada para revisão.
