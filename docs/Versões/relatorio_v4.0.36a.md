# v4.0.36a — Jornal do Comandante, Resumo do Turno

Data: 17/07/2026

## Visão geral

Esta revisão introduz o **Jornal do Comandante**, um resumo apresentado no início do turno para reunir acontecimentos relevantes ocorridos durante a ausência do jogador. O relatório reaproveita a navegação do antigo painel de consumo em voo, mas agora combina eventos táticos, alertas persistentes e autonomia em uma única leitura.

O objetivo é reduzir a sensação de informação perdida entre turnos, especialmente em partidas Hot Seat e na futura experiência multiplayer assíncrona, sem revelar dados que o jogador não teria direito de conhecer pelo Fog of War.

## Jornal do Comandante

- Eventos ocorridos durante turnos alheios são acumulados por time destinatário e apresentados quando esse time volta a jogar.
- O relatório pode informar contato perdido, tiro vindo da névoa, construção perdida, captura em andamento, pouso de emergência, queda por combustível, emersão automática, novo contato e estoque zerado.
- Eventos pontuais são drenados após a leitura do time correto; alertas derivados do estado atual, como captura parcial e estoque vazio, permanecem enquanto a condição existir.
- As entradas são organizadas por categoria e turno, mantendo uma ordem previsível.
- Detecções repetidas na mesma célula, categoria e turno são deduplicadas para evitar poluição visual.

## Informação fog-honesta

O evento é resolvido no momento em que ocorre, usando apenas o conhecimento permitido ao destinatário:

- uma unidade abatida durante o turno inimigo aparece como contato perdido;
- o atacante só é identificado se estava visível para o time da vítima;
- caso contrário, o jornal registra que não houve contato visual com o atacante;
- novos contatos passivos são registrados quando sensores do jogador detectam uma unidade durante o turno adversário;
- eventos vistos ao vivo no próprio turno não são repetidos desnecessariamente no resumo seguinte.

Esse desenho preserva o contrato transacional e evita que o relatório se transforme em uma fonte paralela de revelação de FOW.

## Interface e navegação

- O antigo cabeçalho de consumo em voo passa a ser **Jornal do Comandante**.
- O painel abre em modo largo para melhorar a leitura de descrições, coordenadas e nomes extensos.
- Eventos gerais não exibem a barra de combustível; linhas de autonomia continuam usando o formato e a barra existentes.
- O relatório mantém foco, navegação, pan até a célula relacionada e possibilidade de reabertura.
- No substep de escolha do desembarque, os botões agora aumentam de altura quando o texto quebra em várias linhas e ganharam mais espaçamento interno vertical.

## Persistência no save

- O ledger pendente do Jornal do Comandante foi adicionado ao `SaveGameData`.
- Cada entrada armazena time destinatário, categoria, assunto, detalhe já resolvido de forma fog-honesta, célula e número do turno.
- Save e load preservam eventos que ainda não foram apresentados ao jogador.
- Saves anteriores permanecem compatíveis quando a lista não existe ou está vazia.

## Correção da trava de camada

- A antiga referência específica a `Aircraft Operation Lock` foi generalizada para **Layer Lock**, pois a regra também atende unidades navais e submarinos.
- Um submarino atingido e forçado a emergir não pode mais contornar a restrição apenas se movendo para outra célula e voltando a submergir.
- Mudanças diretas de modo/camada agora respeitam a trava ativa.
- A duração representa turnos jogáveis completos do proprietário: o primeiro upkeep inicia a contagem, sem consumir imediatamente uma das rodadas prometidas.
- O estado interno da contagem é persistido no save, com compatibilidade para saves antigos.
- Emersões automáticas aplicadas no upkeep também passam a aparecer no Jornal do Comandante.

## Conteúdo e apresentação

- Prefabs e a cena Hot Seat foram atualizados para refletir a configuração atual da interface e das unidades.
- O painel financeiro recebeu limpeza de elementos antigos.
- Ajustes visuais e de layout acompanham o novo fluxo de resumo do turno.

## Validação

- Build de `Assembly-CSharp.csproj`: **0 erros**.
- O projeto mantém apenas os warnings já conhecidos de APIs obsoletas e análise de serialização do Unity.
- O fluxo do desembarque continua transacional; a mudança é exclusivamente de apresentação.
- O Jornal persiste somente eventos confirmados ou condições derivadas do estado confirmado do tabuleiro.
