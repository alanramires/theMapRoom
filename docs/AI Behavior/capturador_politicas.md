# Políticas táticas do capturador

Documento de trabalho para organizar os comportamentos desejados do capturador e sua configuração por perfil de IA. Não é uma declaração de que todas as políticas abaixo já estão implementadas, nem uma ordem de implementação.

As regras das ações e as convenções de alcance estão em [Papeis.md](Papeis.md). A doutrina do papel permanece em [Capturador.md](Capturador.md). Este documento detalha as maneiras de cumprir uma missão de captura.

## Propósito e hierarquia

O capturador contribui para a conquista e a renda do exército. A avaliação considera tanto o prédio atual quanto o progresso das unidades na sequência de objetivos.

O lema do **capturador ganancioso e avarento** orienta essas políticas: buscar novos espaços e conquistas sem entregar gratuitamente o espaço que já está ocupando. Como no futebol, liberar uma posição para quem vem atrás permite que a unidade da frente ocupe outro espaço. A blitzkrieg aumenta a presença distribuída do exército, não o número de unidades.

| Camada | Responsabilidade |
|---|---|
| AI Manager — estratégia e eixos | Define os setores que compõem a invasão e a direção do avanço. |
| AI Planner | Atribui objetivos às unidades. |
| Políticas táticas do papel | Orientam como cumprir os objetivos diante da situação local. |
| AI Initiative | Organiza a execução, considerando dependências como liberar o prédio antes de outro capturador entrar. |
| Perfil de IA | Configura quais políticas são consideradas e seus parâmetros. |

Priorizar a captura não significa escolher sempre a ação Capturar. Um capturador pode escolher Reposicionar para permitir que outro termine o prédio enquanto ele antecipa o próximo objetivo.

## Convenções de análise

- O tático de um prédio identifica quem pode alcançá-lo nesta rodada, usando o movimento disponível e as restrições de cada candidato.
- O operacional considera duas rodadas separadas, com seus respectivos orçamentos de movimento. Não soma pontos como um orçamento contínuo.
- Estar perto em hexes não basta: o substituto precisa conseguir chegar, poder capturar e ainda ter ação disponível.
- A presença de um candidato apenas no operacional não garante substituição nesta rodada.
- Na blitzkrieg, ceder beneficia a ocupação de espaços mesmo quando quem vem atrás tem menos HP: o substituto continua a captura e quem está na frente avança. Não é necessário concluir o prédio na rodada da troca.

## Ficha de uma política

| Campo | Conteúdo |
|---|---|
| Propósito | A vantagem buscada. |
| Condições | A situação que permite considerar a política. |
| Consulta | Capacidade usada, faixa tática ou operacional e referência da análise. |
| Decisão | O comportamento escolhido pela unidade. |
| Coordenação | Dependências entre unidades e ordem de execução. |
| Limites | Condições de recusa ou retorno a outra política. |
| Perfil | Habilitação e parâmetros configuráveis. |

### Resumo das políticas

| Política | Resumo |
|---|---|
| **Persistência na captura** | O capturador permanece no prédio e continua capturando quando não existe substituto capaz de assumir nesta rodada, inclusive quando outro capturador está apenas no operacional. Preserva a ocupação e o progresso da conquista, reavaliando a situação a cada rodada. |
| **Blitzkrieg — revezamento ao longo do eixo** | Com a política habilitada, o ocupante cede o prédio a um capturador que vem de trás e consegue assumir no tático, avançando para ocupar novos espaços. A troca acontece mesmo com substituto ferido ou menos eficiente; a prioridade é distribuir a presença do exército. Se o substituto estiver apenas no operacional, o ocupante fica e captura. |
| **Substituição por eficiência de captura** | Um ocupante ferido cede a captura a um substituto mais eficiente que pode assumir no tático, mas continua sua agenda: pode avançar, dar cobertura, buscar uma fusão marcada, fundir com capturadores que já agiram ou atender a um pedido de spotting da artilharia. Não sai para uma posição aleatória. Com substituto apenas no operacional, continua capturando. |
| **Captura oportunista** | Antes do tiro vem o dinheiro: durante a viagem para seu objetivo, o capturador pode adiantar a captura de outro prédio e depois ceder ao responsável original. A pé, pode aproveitar a oportunidade mesmo com o responsável no operacional; embarcado, só considera desembarcar para isso se o responsável estiver fora do operacional do prédio, pois o desembarque consome a ação e adia a captura. |

## 1. Persistência na captura

**Situações discutidas:** o capturador está sozinho, sem substituto no tático ou operacional do prédio, ou o candidato está apenas no operacional e não consegue assumir nesta rodada.

| Campo | Política |
|---|---|
| Propósito | Manter a conquista progredindo enquanto não há substituição disponível. |
| Condições | Há captura válida e não há substituto disponível para assumir. |
| Consulta | Capturadores no tático e no operacional em relação ao prédio. |
| Decisão | Capturar nesta rodada e manter a intenção de continuar na próxima, reavaliando a situação. |
| Coordenação | Não depende de troca com outra unidade. |
| Limites | A continuidade não dispensa nova avaliação de ameaças, recuperação e mudanças de objetivo. A precedência dessas situações ainda será definida. |
| Perfil | A IA fácil deve permanecer no prédio até concluir a captura, sem usar blitzkrieg. Outras exceções desse perfil ainda serão definidas. |

Não executar blitzkrieg e manter vários soldados disputando o mesmo prédio são comportamentos distintos. A distribuição dos demais capturadores depende também do planner e das alternativas disponíveis.

## 2. Blitzkrieg — revezamento ao longo do eixo

**Situação discutida:** quem vem atrás pode ocupar o prédio nesta rodada, liberando a unidade da frente para ocupar novos espaços pelo eixo.

| Campo | Política |
|---|---|
| Propósito | Aumentar a presença distribuída e ocupar espaços vazios rapidamente, mantendo o prédio com um capturador ao final da troca. |
| Condições | Um capturador ocupa B e outro, vindo de trás, está no tático do prédio e pode assumir a captura nesta rodada. |
| Consulta | Substituto no tático de B, calculado com as capacidades do próprio substituto; progressão do ocupante rumo ao próximo objetivo. |
| Decisão | Com a política habilitada e a troca executável, o ocupante sempre libera B para quem vem atrás e avança; o substituto entra para continuar ou concluir a captura, mesmo ferido ou com menos HP. |
| Coordenação | O ocupante precisa sair antes da entrada do substituto. A iniciativa deve considerar essa dependência. |
| Limites | Se o substituto estiver apenas no operacional, o ocupante permanece e captura. HP baixo do substituto, menor poder de captura ou não concluir B imediatamente não vetam a blitzkrieg. As regras legais de movimento e captura continuam obrigatórias. |
| Perfil | Desabilitada na IA fácil; habilitada no perfil rápido pretendido. Os nomes e parâmetros de configuração ainda serão definidos. |

### Exemplo B → F

B e F estão separados por 6 hexes de um percurso em que cada capturador avança 3 hexes por rodada. O segundo capturador vem 3 hexes atrás. Neste exemplo, ele tem poder de captura suficiente para concluir B ao chegar.

| Rodada | Capturador 1 | Capturador 2 |
|---|---|---|
| 1 | Inicia a captura de B. | Está 3 hexes atrás de B. |
| 2 | Sai de B e avança 3 hexes rumo a F. | Chega a B e conclui a conquista. |
| 3 | Chega a F e inicia a captura. | Avança 3 hexes, ficando atrás do primeiro. |
| 4 | Pode seguir adiante, se houver próximo objetivo e nova troca válida. | Chega a F para continuar ou concluir a captura. |

O ganho está no progresso da dupla: o primeiro chega antes a F sem atrasar a conquista de B. Verificar apenas se o ocupante consegue terminar B nesta rodada não é suficiente para decidir que ele deve ficar.

Nesse exemplo, B é concluído na chegada do substituto, mas isso não é requisito da política. Se o substituto estiver ferido, ele assume assim mesmo: a captura pode levar mais tempo, enquanto a unidade da frente amplia a ocupação. A finalidade é distribuir a presença, não maximizar os pontos aplicados apenas no prédio atual.

### Substituto no tático ou apenas no operacional

| Situação | Decisão |
|---|---|
| Substituto apto no tático, mesmo ferido ou com pouco HP | O ocupante sai e deixa o substituto assumir nesta rodada. |
| Substituto apenas no operacional | O ocupante fica e captura; não deixa o prédio vazio até a rodada seguinte. |

O intervalo entre turnos importa. Um prédio deixado vazio pode ser ocupado pelo adversário antes que o substituto chegue. Um APC com movimento de 6 a 7, conforme o percurso e a estrada, pode avançar e desembarcar um soldado no hex à frente. A ameaça relevante inclui a posição em que ele consegue entregar sua carga, não apenas o movimento do soldado por conta própria. Essa entrega não implica que o soldado possa capturar na mesma rodada: desembarcar consome sua ação, conforme Papeis.md.

### Ideia em avaliação: ameaça de desembarque

Ainda não é uma condição adicional aprovada para a blitzkrieg. Podemos estudar se vale consultar a inteligência legitimamente disponível para identificar transportadores inimigos capazes de alcançar e desembarcar sobre um espaço deixado livre. Isso envolveria movimento do transportador, locais válidos de desembarque e a carga conhecida, sem presumir conhecimento de unidades ocultas.

Essa análise teria custo de processamento e não substitui a regra já definida: com substituto apenas no operacional, o ocupante continua capturando. Não detectar ameaça também não prova que o intervalo seria seguro.

## 3. Substituição por eficiência de captura

**Situação discutida:** o ocupante está ferido e existe outro capturador em melhores condições para executar o serviço.

| Campo | Política |
|---|---|
| Propósito | Melhorar a eficiência da captura no prédio atual. |
| Condições | O ocupante tem pouco HP e existe substituto com maior poder de captura efetivo. |
| Consulta | Alcance do substituto em relação ao prédio: tático para assumir agora, operacional para aproximação futura. |
| Decisão | Com substituto apto no tático, ceder a captura. Se ele estiver apenas no operacional, continuar capturando enquanto não pode ser substituído. |
| Coordenação | O ocupante libera o prédio antes da entrada do substituto, sem bloquear seu acesso. |
| Limites | Maior HP não dispensa verificar a eficiência da skill naquela construção. Limiares de ferimento, ganho mínimo e casos em que o ocupante já conclui a captura ainda serão definidos. |
| Perfil | Configuração independente da blitzkrieg. Habilitação por perfil e parâmetros ainda serão definidos. |

Essa política pode produzir a mesma saída do prédio que a blitzkrieg, mas seu propósito é diferente. **Ceder não apaga a agenda de captura da unidade e não autoriza movimento aleatório.** A saída deve integrar uma tarefa que continue contribuindo para a operação.

### Agenda depois de ceder

As alternativas discutidas são:

| Continuação | Condição ou finalidade |
|---|---|
| Avançar pela blitzkrieg | Ocupar novos espaços e seguir a agenda de captura pelo eixo. |
| Dar cobertura ao substituto | Apoiar o capturador que assume o prédio; a forma concreta de cobertura ainda será detalhada. |
| Recuar para uma fusão marcada | Seguir a coordenação de fusão já estabelecida, respeitando as regras de elegibilidade abaixo. |
| Fundir com outro capturador | Considerar apenas parceiros que **já agiram**; nunca consumir na fusão um capturador que ainda não agiu. As regras legais de fusão também precisam ser satisfeitas. |
| Atender a uma solicitação de spotter | Quando a artilharia solicitar, ocupar uma posição de observação, como um morro, para revelar ou detectar alvos após confirmar a ação. |

Essas alternativas ainda não têm uma ordem fixa de preferência. A escolha deve preservar a saída do prédio e o acesso do substituto, além de respeitar as políticas habilitadas pelo perfil.

As tarefas são alternativas para a ação da unidade, não uma sequência de ações extras. Se sair e confirmar posição para observar, ela encerra sua ação e só então atualiza o conhecimento; se fundir, executa a fusão conforme seu fluxo de confirmação.

## 4. Captura oportunista

**Lema: antes do tiro vem o dinheiro.** Durante a viagem para seu objetivo, o capturador aproveita uma construção disponível para adiantar sua conquista, sem se tornar dono permanente da tarefa atribuída a outro.

| Campo | Política |
|---|---|
| Propósito | Aproveitar uma janela de captura durante o deslocamento e adiantar o trabalho antes da chegada do responsável original. |
| Condições | Existe um prédio capturável no caminho e uma oportunidade válida conforme o estado do oportunista, a pé ou embarcado. |
| Consulta | Alcance do capturador originalmente alocado em relação ao prédio; acesso do oportunista ao prédio ou a um desembarque válido. |
| Decisão | A pé, adiantar a captura e depois ceder ao responsável original. Embarcado, considerar a oportunidade somente se o responsável estiver fora do operacional do prédio. |
| Coordenação | Liberar o prédio para o responsável quando ele puder assumir. O oportunista mantém sua própria agenda de captura. |
| Limites | Desembarcar não permite capturar na mesma rodada. Não transformar a oportunidade em bloqueio à chegada do responsável. Desvio máximo e casos sem responsável atribuído ainda serão detalhados. |
| Perfil | Habilitação e parâmetros configuráveis ainda serão definidos, separadamente da blitzkrieg e da substituição por eficiência. |

### A pé ou embarcado

| Estado do oportunista | Responsável original | Decisão discutida |
|---|---|---|
| A pé, com captura válida nesta rodada | No operacional do prédio | Pode capturar uma parte agora; depois libera para o responsável assumir. |
| Embarcado | No tático ou no operacional do prédio | Não desembarca por essa oportunidade: consumiria a ação desembarcando e, na rodada seguinte, o responsável já poderia chegar. |
| Embarcado | Fora do operacional do prédio | Pode considerar desembarcar para aproveitar a oportunidade, verificando destino e captura posterior válidos. |

**A referência é sempre o prédio oportunista, e a faixa consultada é a do capturador original.** Estar fora do operacional não torna automaticamente qualquer desembarque vantajoso; permite considerar a oportunidade sem o conflito de tempo identificado acima.

O caso a pé com o responsável já no tático ainda precisa de uma regra explícita de precedência, para não atrasar quem pode assumir imediatamente.

## Coordenação e compromisso das ações

O planejamento da troca não equivale à sua conclusão. Cada ação começa e termina em Neutral e respeita o [contrato transacional](../arquitetura/acoes_transacionais.md).

1. Identificar a possibilidade de revezamento e sua dependência de execução.
2. O ocupante realiza e confirma sua ação de saída.
3. Após o retorno a Neutral, atualizar o mundo confirmado e reavaliar a possibilidade de entrada do substituto.
4. O substituto realiza e confirma sua própria ação de captura, se ela continuar válida.

Movimento provisório não publica nova visão, detecção ou memória de IA. Missões e resultados definitivos não podem ser registrados como concluídos antes do compromisso correspondente.

## Questões ainda abertas

- Qual política prevalece quando blitzkrieg, substituição por eficiência e oportunidade local são possíveis ao mesmo tempo?
- Quando ameaça ou necessidade de recuperação interrompem a persistência?
- Como escolher o destino de avanço depois de ceder, preservando a prioridade de ocupar novos espaços?
- Como priorizar avanço, cobertura, fusão com parceiro que já agiu e pedidos de spotter na agenda de quem cede?
- Como arbitrar a captura oportunista a pé quando o responsável original já está no tático do prédio?
- Vale o custo de consultar ameaças conhecidas de transporte e desembarque? Em quais decisões essa consulta seria usada?
- Como proceder se, após a saída confirmada, o substituto não puder mais assumir?
- Quais parâmetros devem ser expostos no perfil e quais condições são regras comuns a todos os perfis?

Essas questões serão desenvolvidas com situações concretas de partida antes de definir uma prioridade global ou uma fórmula de pontuação.
