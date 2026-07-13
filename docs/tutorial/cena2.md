# História 2 — A Arma Certa

> Rascunho narrativo e de direção de cena. As instruções entre `< >` descrevem o comportamento desejado sem pressupor que os eventos, gates ou comandos já existam no motor.

## Propósito da aula

Ensinar que:

- classe da unidade e família da arma são informações diferentes;
- fuzil e metralhadora são armas anti-infantaria;
- bazuca é uma arma anti-tanque;
- a arma inadequada ainda pode produzir uma vitória, mas com perdas muito maiores;
- a ordem dos ataques muda o custo da operação;
- o terreno pode ampliar ou reduzir a eficiência da arma correta;
- uma posição guarnecida pode exigir força suficiente e combate por mais de um turno.

## Situação

Mapa de aproximadamente 8×10 hexes.

Uma estrada atravessa uma área de floresta e conduz até Ramelle. A estrada permanece íntegra até a cidade; não há trecho destruído nem decoração que sugira interrupção da rota.

Ramelle fica no centro de uma clareira cercada por planície. Um APC rival com força completa ocupa a cidade.

Um pelotão neutro ferido é a última força de Ramelle diante do APC. Ele está fora do alcance operacional de Ryan, Mathias e Dias: o jogador pode agir no primeiro turno, mas não consegue chegar a tempo de salvá-lo. No turno inimigo, o pelotão não causa dano no revide e é destruído pelo APC.

Ryan, Mathias e Dias são os reforços mais próximos. Outros reforços inimigos estão a caminho, mas não chegam durante a parte dirigida do tutorial.

Unidades do jogador:

- Ryan: Soldado com fuzil anti-infantaria;
- Mathias: Bazuca com arma anti-tanque;
- Dias: Bazuca com arma anti-tanque.

Inimigo:

- APC em força completa, armado com metralhadora anti-infantaria e guarnecido em Ramelle.

Força de Ramelle:

- Soldado neutro ferido, isolado e fora do alcance do destacamento do jogador.

## Objetivos do sistema

1. `hist_2_01 INSPECT_ALLY_UNIT` — inspecionar Mathias ou Dias e identificar a bazuca anti-tanque;
2. `hist_2_02 INSPECT_ENEMY_UNIT` — inspecionar o APC, sua classe e sua metralhadora anti-infantaria;
3. `hist_2_03 DESTROY_ENEMY_UNIT` — destruir o APC da maneira escolhida pelo jogador;
4. `hist_2_04 ENDING` — concluir o debriefing correspondente ao estado de Ryan.

A inspeção da estrada e da planície é orientação do Sargento, não objetivo. Qualquer clique no mapa já apresenta o terreno, portanto não é necessário criar uma tarefa específica.

## Abertura — Ramelle cai

<Mapa começa focado no soldado neutro ferido diante do APC guarnecido em Ramelle. Mostrar que Ryan, Mathias e Dias estão longe demais para alcançá-lo. Depois, apresentar brevemente o destacamento do jogador e liberar suas ações.>

Sargento:

Última transmissão de Ramelle.

Um pelotão ferido ainda segura a cidade, mas não resistirá a outro ataque.

Estamos longe demais para impedir o próximo disparo.

Sargento:

[ordem]Movam-se em direção a Ramelle e façam o que puderem neste turno.[/ordem]

<O jogador age livremente. O soldado neutro permanece inalcançável. Quando as unidades disponíveis tiverem agido, orientar o jogador a encerrar o turno.>

Sargento:

Não chegaremos a tempo.

[ordem]Encerre o turno e observe o combate em Ramelle.[/ordem]

<No turno inimigo, focar a câmera em Ramelle. O APC ataca o soldado neutro. O soldado não causa dano no revide e é destruído. A queda de Ramelle é narrativa: não iniciar nem ensinar a mecânica formal de captura.>

Sargento:

Ramelle caiu.

O último pelotão não arranhou a blindagem — e a metralhadora não deixou sobreviventes.

Sargento:

Ryan, grave bem essa imagem.

Você carrega o mesmo tipo de alvo que aquela arma foi feita para destruir.

Agora o problema é nosso.

## Debriefing — Conheça suas armas

<Destacar Ryan.>

Sargento:

Recruta Ryan continua com o fuzil.

É uma arma anti-infantaria: eficiente contra soldados e inadequada contra blindagem.

<Destacar Mathias e Dias.>

Sargento:

Mathias e Dias carregam bazucas.

São armas anti-tanque, feitas para enfrentar veículos e blindados.

Sargento:

Prestem atenção: os três continuam pertencendo à infantaria.

O que muda é o armamento e a função de cada grupo no combate.

[ordem]Inspecione Mathias ou Dias e identifique a arma que carregam.[/ordem]

<Revelar `hist_2_01` e esperar a inspeção de Mathias ou Dias. Permitir que o jogador também inspecione livremente Ryan, terreno e outras unidades.>

Sargento:

Fuzil derruba homem.

Bazuca abre lata.

Se parece simples, ótimo. Vamos descobrir se continua simples quando a lata atira de volta.

## Debriefing — Conheça o inimigo e a posição

<Mover a câmera até o APC em Ramelle.>

Sargento:

Agora inspecione o ocupante.

Não olhe apenas para a blindagem. Veja a arma que ele carrega e o terreno que protege sua carcaça.

[ordem]Inspecione o APC e Ramelle.[/ordem]

<Revelar `hist_2_02` e esperar a inspeção do APC. A ficha deve mostrar a classe Veículo, a metralhadora anti-infantaria e a defesa fornecida pela cidade. A inspeção de Ramelle é apenas orientação, sem objetivo próprio.>

Sargento:

APC: veículo protegido por blindagem e armado com uma metralhadora anti-infantaria.

Aquela arma foi feita para triturar pelotões como o de Ryan.

Sargento:

E o APC está guarnecido na cidade.

Ramelle aumenta sua defesa e favorece o ocupante nos combates equilibrados.

Não estamos atacando apenas um veículo, recrutas.

Estamos atacando o veículo e a cidade que o protege.

## A marcha

<Panorâmica da cidade até o grupo, seguindo a estrada.>

Sargento:

A estrada é a rota mais rápida até Ramelle.

Marchem por ela, mas escolham com cuidado de onde iniciar o ataque.

[ordem]Conduza Ryan, Mathias e Dias pela estrada.[/ordem]

<Permitir marcha livre e passagem de turno. A rota reaproveita o que foi aprendido na História 1.>

Sargento:

A estrada ajuda a chegar ao combate. Não ajuda a sobreviver nele.

Antes de avançar, inspecione a estrada e a planície ao redor de Ramelle.

Compare a proteção oferecida por cada terreno e escolha de onde cada unidade iniciará o ataque.

[ordem]Inspecione os terrenos e escolha os locais de ataque de Ryan, Mathias e Dias.[/ordem]

<Esta orientação não revela objetivo e não espera evento próprio. Qualquer clique no mapa já apresenta o terreno. Não marcar os melhores hexes em verde nem indicar uma solução única. Se necessário, destacar apenas a região geral de aproximação, nunca posições ideais específicas.>

Sargento:

O plano é seu.

Minha recomendação: aproximem-se juntos e deixem as bazucas abrirem o combate.

[ordem]Aproxime o destacamento de Ramelle conforme o plano que escolheu.[/ordem]

<A estrada chega até Ramelle. O cursor continua livre. O jogador pode usar planície, asfalto ou combinar posições, e nenhuma escolha válida deve ser bloqueada.>

## Contato — A decisão é do jogador

<Quando a primeira unidade alcançar uma posição de ataque, focar brevemente o APC.>

Sargento:

Contato confirmado.

A missão é desalojar o APC de Ramelle.

Sargento:

Como vão fazer isso é decisão de comando.

Lembrem-se apenas da aula: arma, alvo, posição e ordem.

[ordem]Destrua o APC que ocupa Ramelle.[/ordem]

<A partir daqui, liberar experimentação. Não bloquear Ryan, não exigir uma ordem específica e não reiniciar a missão por uma decisão ineficiente. Registrar, se o motor permitir, quem atacou primeiro e de qual terreno.>

### Alertas antes do ataque

<Se uma unidade assumir posição de ataque no asfalto, comentar uma vez, sem bloquear nem mover a unidade.>

Sargento:

A estrada trouxe vocês até aqui, mas oferece pouca proteção.

Pode atacar dessa posição — só não finja surpresa quando chegar o relatório de baixas.

<Se as unidades chegarem muito separadas e uma delas estiver prestes a iniciar o combate sozinha, comentar uma vez, sem exigir que o jogador recue.>

Sargento:

Seu destacamento está disperso.

Ainda pode atacar, mas o APC enfrentará cada grupo separadamente. Minha recomendação continua a mesma: cheguem juntos.

## Reações possíveis do Sargento

### Se uma bazuca atacar primeiro a partir da planície

Sargento:

Arma anti-tanque contra veículo. Correto.

Mas não espere milagre. Ele está guarnecido e vai responder com tudo.

Depois do combate:

Sargento:

Bom impacto.

O APC continua de pé, mas já perdeu homens, força de combate e capacidade de resposta.

O primeiro ataque não venceu a batalha. Preparou o próximo.

### Se Ryan atacar primeiro

<Deixar o combate real acontecer. No teste atual, Ryan pode terminar por volta de 3, enquanto a missão ainda permanece recuperável.>

Sargento:

Sete baixas!

O fuzil mal feriu o veículo, e você entregou Ryan à arma que foi feita para destruir infantaria.

Sargento:

A missão ainda não acabou.

Mathias, Dias: corrijam a ordem.

### Se Ryan atacar a partir do asfalto

<Deixar o combate real acontecer. No teste atual, Ryan pode terminar por volta de 2 sem causar perdas ao APC.>

Sargento:

Oito baixas e nenhum dano confirmado!

Estrada é mobilidade, não proteção. Ryan atacou exposto contra um inimigo guarnecido.

Saia do asfalto antes que o próximo relatório venha escrito numa lápide.

### Se uma bazuca atacar a partir do asfalto

<Deixar o combate real acontecer. No teste atual, o bazuqueiro perde aproximadamente um homem a mais que numa abordagem melhor.>

Sargento:

A arma estava certa. A posição, não.

Você feriu o APC, mas pagou mais caro por atacar exposto.

### Se Ryan participar depois das bazucas

Sargento:

Agora o fuzil enfrenta um inimigo enfraquecido.

Ryan não abriu a blindagem. Entrou depois que os especialistas quebraram a vantagem.

Essa é a diferença entre apoio e desperdício.

## Fim do primeiro turno de combate

<Se todas as unidades disponíveis já agiram e o APC continua vivo, orientar a passagem de turno sem reiniciar a situação.>

Sargento:

Ele ainda está de pé.

Claro que está. A arma certa não transforma uma posição guarnecida em papelão.

Sargento:

A primeira investida desgastou o ocupante. A segunda precisa terminar o serviço.

[ordem]Encerre o turno e reorganize o ataque.[/ordem]

No turno seguinte:

Sargento:

Segundo ataque.

Vocês não enfrentam mais o mesmo APC. Ele está ferido e combate com menos força.

Foi para isso que serviu a primeira investida.

[ordem]Desaloje o APC de Ramelle.[/ordem]

## Objetivo principal concluído

<Quando o APC for destruído, completar a tarefa principal. Não declarar imediatamente a tela final: abrir primeiro o debriefing.>

Sargento:

APC destruído. Ramelle está livre das armas inimigas.

### Debriefing eficiente

<Usar se as bazucas abriram o combate e Ryan terminou relativamente preservado.>

Sargento:

Vocês empregaram as armas anti-tanque primeiro e preservaram o fuzileiro para o momento certo.

Vencer é cumprir o objetivo.

Vencer bem é ainda possuir uma força depois dele.

### Debriefing custoso

<Usar se Ryan estiver gravemente ferido ou se ataques importantes partiram da estrada.>

Sargento:

Missão cumprida. Mas olhem para o que restou do destacamento.

A arma errada, a ordem errada ou a posição errada nem sempre tornam a vitória impossível.

Tornam a vitória cara.

Sargento:

O inimigo perdeu um veículo.

Nós quase perdemos três pelotões para arrancá-lo dali.

Vitória não apaga uma ordem ruim, recruta. Só garante que ainda exista alguém para aprender com ela.

## Conclusão da História 2

Sargento:

Segunda lição concluída.

Ryan ensinou o valor do fuzil. Mathias e Dias ensinaram o valor da bazuca. O APC ensinou o preço de confundir os dois.

Sargento:

Arma anti-infantaria contra infantaria.

Arma anti-tanque contra veículos e blindados.

Mas a arma certa não apaga o terreno, não substitui força suficiente e não corrige uma ordem ruim.

Sargento:

Objetivo principal cumprido. Ramelle não cairá hoje.

<Marcar a missão como concluída, executar o debriefing correspondente ao estado de Ryan e exibir a vitória normal. O tutorial termina nesse ponto.>

## Resultados de simulação usados no rascunho

Valores provisórios, sujeitos a nova confirmação conforme posição e sequência exatas do mapa:

- Ryan não participa: SD 10, BZ 4, BZ 4;
- bazucas primeiro e Ryan participa depois: SD 8, BZ 5, BZ 5;
- Ryan atira primeiro: SD 3, BZ 5, BZ 6;
- composição antiga de 1 BZ + 2 SD: todos mortos;
- Ryan ataca do asfalto: SD 2, APC 10;
- bazuca ataca do asfalto: BZ 4, APC 7, aproximadamente uma baixa adicional em relação à abordagem melhor.

Esses números não precisam ser narrados previamente ao jogador. O Sargento comunica a avaliação operacional; o combate real demonstra a consequência.
