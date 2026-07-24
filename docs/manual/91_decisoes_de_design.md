# Decisões de Design

*Por que uma regra existe. Este arquivo impede que alguém volte daqui a seis meses, ache uma regra estranha e remova justamente a consequência que a justificava.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

Cada entrada registra a decisão, o problema que a motivou, as alternativas consideradas e a consequência desejada. Decisão registrada aqui **não** é regra — a regra mora no documento canônico correspondente. Aqui fica o motivo.

---

## Pouso e decolagem foram tirados das mãos do jogador

**Problema.** Numa versão anterior, pousar era uma ação que o jogador escolhia. Como toda ação se resolve no comprometimento e o inimigo só responde no turno dele, um jogador pousava o caça em solo firme na frente da bateria antiaérea inimiga — e a antiaérea, que só mira o céu, não tinha alvo quando seu turno chegava. A aviação usava o pouso manual como escudo, e tanques e antiaérea ficavam decorativos. As unidades de logística pioravam: forçavam o pouso para atender, e o jogador deixava o caça no chão de turno em turno, transportando aviões "no modo pipa" com uma fila de caminhões.

**Decisão.** Mudança de camada deixou de ser ação. Pousar, decolar, subir, descer, emergir e submergir são **automáticos**, chamados pelos sensores que precisam deles. Selecionar uma unidade a manda para a camada natural do domínio. Suprir uma aeronave a faz pousar, receber e **relançar no mesmo turno** (arremetida). Transferir estoque a faz pousar e ficar. A isenção de consumo em instalação aeronáutica é concedida pela **presença** sobre o hexágono, sem verificar se está pousada — a aeronave sobrevoa, "toca e recolhe", e continua operacional no ar.

**Alternativas.** Combate por interrupção, como em Jagged Alliance, em que a antiaérea dispararia no meio do voo inimigo. Rejeitada: quebraria a invariante transacional do jogo, em que o movimento inteiro se resolve antes de o outro lado responder.

**Consequência desejada.** A aviação fica no ar a maior parte do tempo — onde a antiaérea e os caças a alcançam. Restam duas janelas de solo deliberadas: a aeronave recém-comprada, que nasce no chão até algo interagir com ela, e a que transfere estoque, que aceita a lentidão. As duas são vulnerabilidade escolhida, não impunidade.

**Onde vive a regra.** `04_ciclo_de_acao_e_comprometimento.md` (não é ação) e `08_transporte_fusao_e_operacoes_aereas.md` (o ciclo).

---

## O Serviço do Comando alcança quem está embarcado

**Problema.** Um atendimento de campo trata só o veículo que encostou. Isso deixava a tropa embarcada sem forma prática de manutenção coletiva — cada passageiro teria que desembarcar e procurar prédio.

**Decisão.** O Serviço do Comando — a habilidade de guarnição do comandante, acionada no início do turno — desce até o **primeiro nível** de carga e atende também os passageiros diretos. Um Chinook com um APC dentro tem os dois tratados; o soldado dentro do APC, não. A fila é ordenada por **prioridade econômica**: cada transportador e sua carga formam um bloco, e os blocos são servidos do mais caro para o mais barato, para que a reserva curta seja gasta primeiro no que vale mais.

**Consequência desejada.** A cidade e o comando representam o tempo e as mãos de uma retaguarda, onde várias peças são cuidadas de uma vez — enquanto o caminhão na estrada continua sendo um atendimento pontual, de uma unidade só. Dá à tropa embarcada um caminho de manutenção sem desmontar a operação.

**Onde vive a regra.** `07_logistica_e_servicos.md`.

---

## Disparar custa ocultação — inclusive no revide

**Problema.** A ocultação caía ao atacar, mas o revide não cobrava nada: a aeronave furtiva atacada respondia ao fogo e continuava invisível. O submarino já emergia ao revidar, então os dois furtivos do jogo seguiam regras diferentes sem que ninguém tivesse decidido isso.

**Decisão.** Revidar expõe igual a atacar. Disparar é disparar, seja iniciando o duelo ou respondendo a ele.

**Justificativa física.** A aeronave furtiva precisa abrir o compartimento de armas para atirar, e a cavidade quebra o desenho anguloso que devolvia a onda de radar para longe. No instante do disparo ela deixa de ser uma superfície lisa.

**Consequência desejada.** Como o revide é automático — não existe segurar fogo para permanecer oculto —, ser atacado passa a custar a ocultação. É o contrapeso de entrar no duelo escolhendo a hora: o furtivo atira primeiro, mas ninguém atira de graça. E os dois furtivos do jogo voltam a obedecer ao mesmo princípio.

**Onde vive a regra.** `05_visao_deteccao_e_nevoa.md`.

---

## Revide existe apenas no alcance 1

**Problema.** Não estava claro se o defensor responde quando possui arma de alcance compatível com a distância do ataque.

**Alternativas.** Revide dependente do alcance da arma de revide; ou distância 1 como lei absoluta.

**Decisão.** Distância exatamente 1, sem exceção — e a arma de revide ainda precisa ter alcance **mínimo** 1, de modo que uma arma de 2 a 4 nunca responde nem com o inimigo colado.

**Consequência desejada.** Separa duelo de execução. Ataque a distância é sempre unilateral, o que cria necessidade de escolta, linhas de proteção e controle de corredores — e torna artilharia encurralada genuinamente indefesa.

**Onde vive a regra.** `06_combate.md`.

---

## Não existem comandos de altitude

**Problema.** Subir, descer, emergir e submergir poderiam ser ordens diretas do jogador.

**Decisão.** Não são. A unidade vai para a camada preferida do domínio dela quando selecionada, e toda outra mudança é **consequência** de algo que ela fez — decolar, pousar, atacar, ser atingida, receber serviço.

**Consequência desejada.** Altitude deixa de ser alavanca e vira resultado. Isso impede que a aviação use o solo como abrigo antiaéreo à vontade, e faz a emersão do submarino ser preço de ação, não escolha.

**Onde vive a regra.** `08_transporte_fusao_e_operacoes_aereas.md` e `05_visao_deteccao_e_nevoa.md`.

---

## Construção revela terreno, mas não detecta a distância

**Problema.** Se o prédio enxergasse unidades no raio de visão dele, território revelado viraria território vigiado e a névoa perderia função perto da infraestrutura.

**Decisão.** A construção revela **terreno** no raio dela e detecta **unidade apenas no próprio hexágono**, na faixa da superfície. Nunca fura ocultação e nunca serve de observador para quem tenta furar.

**Consequência desejada.** O mapa aberto ao redor da sua fábrica não substitui uma unidade de olho na estrada. Prédio não é sensor.

**Onde vive a regra.** `05_visao_deteccao_e_nevoa.md`.

---

## A conquista não se perde

**Problema.** Perder um prédio poderia retrancar o arsenal que ele havia destravado.

**Decisão.** Basta capturar uma vez. O desbloqueio é permanente para o resto da partida, mesmo que a construção volte ao inimigo no turno seguinte.

**Consequência desejada.** A progressão vira memória de campanha, não inventário. O jogo pergunta "você já teve?", não "você tem?" — e o mapa vira a árvore tecnológica.

**Onde vive a regra.** `09_captura_economia_e_progressao.md`.

---

## Passageiros morrem com o transportador

**Problema.** Transporte poderia ser risco barato se a tropa embarcada sobrevivesse à perda do veículo.

**Decisão.** Todos os passageiros morrem, recursivamente, sem teste de sobrevivência nem desembarque de emergência; reservas embarcadas são perdidas. Em compensação, enquanto o transportador vive, o dano proporcional nos passageiros nunca os mata — piso de 1.

**Consequência desejada.** Transportar é concentrar risco. Um comboio bem atacado não perde uma unidade, perde a operação — e essa é a contrapartida da mobilidade que o transporte concede.

**Onde vive a regra.** `08_transporte_fusao_e_operacoes_aereas.md`.

---

## Armadura não limita a potência da arma

**Problema.** Uma regra antiga dizia que unidade leve não poderia operar armamento médio ou pesado.

**Decisão.** Regra descartada. Classe de armadura e classe de potência são derivadas automaticamente dos números da ficha e são **independentes**. O único efeito delas é logístico: a armadura decide quanto rende cada ponto de reserva, e a potência decide quanto pesa cada projétil reposto.

**Consequência desejada.** Antiaérea rebocada e artilharia leve — poder de fogo alto sobre plataforma frágil — passam a ser combinações legítimas em vez de violações. A classe deixa de dizer o que a unidade pode fazer e passa a dizer quanto ela custa para manter.

**Onde vive a regra.** `01_principios_e_vocabulario.md`.

---

## Alcance 0 é recurso, não brecha

**Problema.** "Nenhuma arma atinge o próprio hexágono" parecia lei, mas impedia armamento antissubmarino de contato.

**Decisão.** A lei preservada é a da escala — ocupar o mesmo setor não é estar ao alcance, e nenhuma arma de tiro direto atinge o próprio hexágono. A exceção é armamento lançado sobre o setor onde você já está, contra alvo em **outro andar**: a distância horizontal é zero porque a separação é vertical.

**Consequência desejada.** A fragata pode mover sobre o submarino e bombardeá-lo. Como não há revide no alcance 0, o ataque é sempre unilateral — e a resposta do submarino continua sendo não ter sido encontrado.

**Onde vive a regra.** `06_combate.md`.

---

## Quem se deslocou engaja apenas no contato

**Problema.** Sem restrição, artilharia poderia avançar e disparar no mesmo turno.

**Decisão.** Unidade que andou só ataca a distância 1 — ou 0, para armas que operam no próprio hexágono. Toda arma de alcance mínimo 2 ou mais fica indisponível após o movimento. A trava não é sobre ter alcance longo; é sobre **não alcançar o contato**.

**Consequência desejada.** Posicionar artilharia vira decisão de dois turnos, e perder a posição dela custa caro.

**Onde vive a regra.** `04_ciclo_de_acao_e_comprometimento.md`.

---

## O Jornal informa sem repintar o mapa

**Problema.** Ao perder uma construção, o jogador recebe o relatório — mas o hexágono deixa de lhe dar visão no mesmo instante, e a memória de névoa congela com a cor antiga.

**Alternativas.** Atualizar a fotografia junto com o relatório; ou declarar a assimetria intencional.

**Decisão.** Assimetria intencional. O Jornal registra conhecimento adquirido; o tabuleiro registra a última observação.

**Consequência desejada.** É a Sala de Mapas funcionando como sala de mapas: o despacho chegou à sua mesa, mas ninguém foi lá mover a miniatura. Depois de ler o Jornal, você sabe mais do que o seu próprio tabuleiro mostra.

**Onde vive a regra.** `05_visao_deteccao_e_nevoa.md` e `10_turnos_jornal_e_vitoria.md`.

---

## O menu nunca filtra pela verdade oculta

**Problema.** Opções apresentadas ao jogador poderiam denunciar o que existe no escuro pela própria ausência delas.

**Decisão.** Ou o menu filtra pelo que o time conhece e mostra só o conhecido, ou não filtra e mostra tudo. Nunca filtra pela verdade oculta.

**Consequência desejada.** O leque de movimento pode alcançar o preto — oferta ampla com motivo escondido é honesta. O que seria desonesto é uma lista cujas ausências desenham o inimigo.

**Onde vive a regra.** `04_ciclo_de_acao_e_comprometimento.md`.
