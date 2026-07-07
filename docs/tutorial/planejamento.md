# Planejamento dos tutoriais

## Direção geral

Em vez de criar uma cena para cada tarefa pequena, a proposta é trabalhar com **cinco cenas**, cada uma cobrindo um conjunto coerente de mecânicas.

As tarefas aparecem progressivamente por meio do `TutorialData`. O jogador conclui uma etapa, recebe a próxima e permanece no mesmo mapa. Isso reduz a quantidade de cenas para manter e evita carregamentos constantes.

Cada cena deve:

- introduzir poucas mecânicas novas;
- reutilizar as mecânicas aprendidas anteriormente;
- permitir experimentação sem punição imediata;
- terminar com uma situação curta que combine as tarefas ensinadas;
- usar eventos reais do jogo para validar objetivos, evitando scripts específicos sempre que possível.

## Cena 1 — Fundamentos

Objetivo: ensinar a interação básica com unidades e o mapa.

Possíveis tarefas:

1. Selecionar uma unidade.
2. Inspecionar seus atributos.
3. Mover para um hex indicado.
4. Comparar movimento em terreno aberto e terreno difícil.
5. Usar `MANTER POSIÇÃO`.
6. Escolher um alvo.
7. Confirmar e executar um ataque.
8. Encerrar o turno.

Situação final sugerida: atravessar um pequeno trecho com dois tipos de terreno e derrotar um inimigo simples.

Base existente: `História 1 - Aprendendo a Atirar`.

## Cena 2 — Armas e proteção

Objetivo: mostrar que posição, alcance e arma adequada mudam o resultado do combate.

Possíveis tarefas:

1. Atacar infantaria em terreno aberto.
2. Atacar uma unidade protegida por montanha ou floresta.
3. Comparar combate corporal e ataque à distância.
4. Identificar alcance mínimo e máximo das armas.
5. Escolher uma arma adequada contra um veículo.
6. Usar linha de visão e posição elevada.
7. Destruir um APC com a composição disponível.

Situação final sugerida: defender uma posição contra infantaria e um veículo leve.

Base existente: `História 2 - A Arma certa`.

## Cena 3 — Operações

Objetivo: ensinar compra, transporte, captura e retorno à base.

Possíveis tarefas:

1. Voltar ao HQ.
2. Comprar um APC.
3. Encontrar uma unidade isolada.
4. Aproximar o transportador.
5. Embarcar a unidade.
6. Cruzar uma área perigosa.
7. Desembarcar em um hex válido.
8. Capturar uma construção.
9. Retornar com a unidade sobrevivente.

Situação final sugerida: resgatar Ryan, eliminar a guarda do caminho e levá-lo em segurança ao objetivo.

Base existente: `História 3 - Resgate Off Road`.

## Cena 4 — Logística

Objetivo: ensinar autonomia, estradas, suprimento e Serviço do Comando.

Possíveis tarefas:

1. Identificar uma unidade com pouca autonomia.
2. Levar um caminhão de suprimentos ao ponto de encontro.
3. Reabastecer uma unidade.
4. Usar estrada para aproveitar o bônus de movimento.
5. Transferir ou distribuir suprimentos.
6. Usar o Serviço do Comando.
7. Manter caminhão e unidade escoltada vivos.
8. Alcançar a área de defesa antes do inimigo.

Situação final sugerida: abastecer o APC e conduzir o grupo até Ramelle sob fogo de obuses.

Base existente: `História 4 - Sem Combustível`.

A AI Easy pode substituir o Automata antigo dos obuses. O comportamento deve ser previsível o suficiente para ensinar, mas continuar usando as regras reais de sensores, alcance e combate.

## Cena 5 — Batalha guiada

Objetivo: combinar os sistemas anteriores em uma partida curta contra a AI.

Possíveis tarefas:

1. Explorar sem conhecer a posição inimiga.
2. Entender a nevoa e os sensores.
3. Detectar uma unidade escondida.
4. Usar um observador avançado.
5. Atacar uma ameaça fora da visão direta da artilharia.
6. Defender uma ponte ou construção estratégica.
7. Comprar reforços com recursos limitados.
8. Capturar ou manter o objetivo até a vitória.

Situação final sugerida: defender a ponte enquanto a AI Easy monta sua força gradualmente.

Base existente: `História 5 - Defenda a Ponte`.

## Papel da AI

- Cenas 1 e 2 podem usar inimigos estáticos ou ações altamente controladas.
- Cena 3 pode usar pouca AI, limitada à guarda e reação local.
- Cena 4 deve introduzir pressão à distância com AI Easy.
- Cena 5 deve usar o turno completo da AI Easy.

Quando o comportamento precisa ser didático, é preferível limitar objetivos, unidades disponíveis e espaço do mapa em vez de criar uma segunda lógica artificial exclusiva para tutorial.

## Estrutura técnica sugerida

- `TutorialData` define tarefas, ordem, textos, condições opcionais e derrota.
- `TutorialManager` escuta eventos reais do jogo e conclui objetivos.
- `TutorialRules` fica reservado para exceções didáticas inevitáveis.
- Spawns e diálogos devem ser declarativos sempre que possível.
- A cena define mapa, forças iniciais, construções e dificuldade da AI.
- Objetivos concluídos não devem depender de nomes de GameObjects quando puderem usar ID, time, tipo de unidade ou coordenada.

## Pontos para conversa

- O tutorial deve bloquear ações ainda não ensinadas ou apenas orientar?
- O jogador pode falhar e continuar experimentando ou a cena reinicia?
- As cinco cenas formam uma campanha obrigatória ou ficam disponíveis separadamente?
- Devemos manter nomes e narrativa inspirados em Ryan/Ramelle?
- Quais tarefas precisam de diálogo e quais bastam no `panel_helper`?
- A Cena 5 termina por objetivo tutorial ou pelas regras normais de vitória?
- O progresso entre cenas deve ser salvo?

## Próximo passo sugerido

Revisar a Cena 1 e definir uma sequência final de aproximadamente **seis a oito tarefas**. Depois, ligar a cena ao `TutorialData` atual e verificar quais eventos já funcionam sem código novo.

