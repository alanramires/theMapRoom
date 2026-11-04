# Mylarhis: progresso e alinhamento indie

Leitura franca de 2026-10-27, no fechamento da v2.0.0 (capítulo 1 completo), com uma atualização de 2026-10-28 (v2.1.0) no fim. Cruza o que aconteceu no projeto com as lições do vídeo em [video_analise.md](video_analise.md) e com o que eu (Claude) vi trabalhando no código ao seu lado. É opinião, não plano aprovado.

## Em uma frase

Você fez em uma semana o que muito indie iniciante não termina: um ciclo **completo**, do começo ao fim, com identidade própria. O risco agora não é falta de capacidade. É você seguir construindo sistemas mais rápido do que pessoas de fora jogam o jogo.

## O que os números dizem

- Repositório criado em 2026-10-20; v1.0.0 em 2026-10-21; v2.0.0 em 2026-10-27. São 17 versões em 7 dias.
- Você tinha se dado uma semana de escopo. Cumpriu: prólogo em mangá, fase com checkpoints, chefe em dois estágios, perseguição até o reator, dois finais em mangá e uma tela de fim de capítulo.
- Testes de fora até agora: o Laio, com críticas ao design da fase 1, o comentário de que "parece *Choices*, mas você atira", e dois colegas de trabalho prometidos. Nenhuma garota testou ainda, e esse é o público que você mais quer alcançar.

A primeira parte é rara. A segunda é o ponto fraco.

## Lição por lição do vídeo

### Escopo é foco (decidir o que *não* fazer)

**Bem:** você cortou de verdade. A Vespa tem dois estágios, não três. Há um capítulo só. Os quadrinhos são imagens paradas com personagens entrando, não animação. Quando a ideia do terceiro estágio ou do tiro no reator apareceu, ela saiu. O High Speed Chase e as armas inimigas foram para `ideias_futuras.md` em vez de entrar no código. Isso é escopo bem feito.

**Atenção:** o corte foi bom no *conteúdo* e frouxo nas *ferramentas*. Em uma semana o jogo ganhou:
- banco de mensagens e histórias em dois idiomas;
- console de debug;
- painel de debug;
- TestArea com medição de DPS, sensores de cobertura e MTDPS de formações;
- perfil de joypad PS2 USB;
- checklist de menus;
- fichas de dificuldade e de chefe;
- conversão metro/pé.

Cada uma fez sentido no dia. Somadas, são um ERP de um jogo de uma fase. É o seu instinto de 15 anos de ERP/BI, e ele é uma força: ninguém mede DPS de tiro carregado numa primeira semana. Mas o vídeo é direto: o problema de escopo "estica sua energia por todas as partes". Ferramenta interna não aparece para quem joga.

**Franco:** a TestArea com MTDPS mediu o balanceamento com precisão antes de qualquer jogador de fora dizer se o tiro é *gostoso*. A ordem ideal é a inversa: alguém de fora diz "o carregado parece fraco", e aí a régua serve para consertar.

### O poder do tema

Esse é o seu ponto mais forte, de longe. "Uma garota alienígena que voa, salva a cidade e ama caramelo, contada como um mangá que eu escrevi" é um tema que:
- dá personalidade às mecânicas: o caramelo como combustível, o Sentinel oferecendo o primeiro caramelo no final;
- funciona como filtro de decisão, igual ao exemplo do vídeo, "é um jogo de samurai, então não tem bazuca".

**Use o filtro de forma explícita.** Antes de aceitar uma ideia, pergunte: isso aumenta o apego à Mila ou a sensação de ler o seu mangá? Pelo filtro:
- **High Speed Chase com caramelos como combustível:** passa.
- **Armas inimigas embarcadas na Mila:** precisam de uma justificativa de história. Por que ela usaria tecnologia de Lynn? Se a resposta render uma cena de mangá, passa. Se for só "é legal", espera.

A comparação com *Choices* confirma que o tema chega a quem joga. É um elogio disfarçado: a apresentação fala com o público de histórias de personagem.

### Copie com cuidado (homenagem de verdade)

Suas referências estão bem escolhidas e, mais importante, são **de escala viável**: Victory Girl, Mega Man, Gradius, Aero Fighters, Choplifter. Nada de "meu GTA". O vídeo alerta justamente contra referências AAA, e você ficou longe disso no núcleo. Stellar Blade e Ace Combat aparecem só como detalhe, como a tela preta ou a ideia da perseguição, não como molde.

**O que falta, segundo o próprio vídeo:** ele diz que muita gente copia a *memória* do jogo, não o jogo. Volte a jogar 20 minutos de Mega Man X e de um shmup curto, anotando:
- quanto tempo o tiro carregado leva para encher;
- como o som e a tela respondem quando ele está cheio;
- de quanto em quanto tempo aparece um inimigo novo.

Você fez isso com números na TestArea. Falta fazer com a referência do lado.

Jogue também indies pequenos e atuais do mesmo nicho. O vídeo insiste em uma "dieta" de jogos pequenos como modelo.

### Faça um brinquedo primeiro

O núcleo é voar e atirar, com o tiro carregado no estilo Mega Man. Ele existe e foi afinado (o padrão 1/2/5, a escala ×1,5). Mas a semana foi gasta em boa parte **em volta** do brinquedo: mangá, cinematic do reator, telas, tradução, mobile. O vídeo cita um jogo que encheu de fases e personagens "antes de acertar a sensação central".

Você não caiu tão fundo nisso, porque o conteúdo é pouco e tem foco. Mas a pergunta que ninguém de fora respondeu ainda é a mais importante: **voar e atirar na Mila é gostoso por si só, sem a história?** Se for, o mangá multiplica. Se não for, o mangá vira o motivo de jogar, e aí o jogo é um *Choices* com um interlúdio de ação. Isso também pode ser válido, mas precisa ser uma decisão sua.

### Use restrições espertas

Aqui você foi bem, talvez sem perceber:
- Quadrinhos como imagens com cinco lugares de personagem são uma restrição elegante. Custam pouco, a gramática de visual novel funciona, e o leitor não sente falta de animação.
- Uma fase com rolagem lateral e um chefe exige pouco level design e deixa espaço para cuidar do feel.
- Português como padrão e inglês como fallback deixaram traduzir aos poucos.

A restrição que **falta** é sobre ferramentas: "não construo ferramenta para um problema que um jogador de fora ainda não apontou".

## O que eu, como agente, preciso admitir

- **Comigo, sistema fica barato de escrever.** Isso empurra o escopo de ferramentas para cima. Uma tarde vira um sistema novo, e cada sistema novo aumenta o que precisa ser testado. Daqui para frente, vale eu perguntar "isso alguém de fora pediu?" antes de propor ferramenta.
- **Muito do que entreguei foi só compilado.** Meus relatórios repetem "compilou, não testado em Play" quase todo dia. Você testa bastante pelas capturas, mas há uma pilha de pequenas coisas nunca confirmadas: Continue no estágio II, descanso do spawner, pausa do Editor, os lugares de cima do leitor, o fade do jingle. Antes de crescer, vale passar uma tarde só jogando o capítulo inteiro com a checklist na mão.
- **Eu errei hipóteses.** Exemplos: o `defeatStory` vazio que parecia problema de idioma, a lista de arte copiada. Os relatórios registram esses erros, e isso é bom. Mas é sinal de que estamos rápidos demais em alguns pontos.

## Sobre o plano "depois do MVP vêm os refactors"

Aqui eu discordo um pouco, com respeito. Refatorar antes de ter jogadores é a armadilha mais comum de quem vem de engenharia: a arquitetura fica bonita para um jogo que ninguém validou. A ordem que eu sugiro:

1. **Publicar o capítulo 1 no itch.io**, jogável no navegador. Rode o Apply Web Overrides antes.
2. **Conseguir de 5 a 10 testes de fora**, incluindo pelo menos duas ou três pessoas do público que você quer. Observe sem explicar nada: onde travam, se leem o mangá ou pulam, se tentam o outro final.
3. **Refatorar só o que o capítulo 2 precisa** (catálogo de fases, save) e o que os testes mostrarem que dói.

O `arquitetura.md` continua valendo como mapa. Ele só não deveria vir antes do passo 2.

## Placar honesto

| Lição do vídeo | v2.0.0 (2026-10-27) | v2.1.0 (2026-10-28) |
|---|---|---|
| Escopo como foco | Bom no conteúdo, frouxo nas ferramentas | **Melhorou.** O código do dia respondeu a um problema real de jogador. O capítulo 2 ficou no papel, não no código. Única ferramenta sem pedido de fora: o tempo de fase (pequeno e oculto). |
| Tema forte | Ponto mais forte do projeto | **Confirmado por fora:** "E COMO ASSIM ERA A AVÓ DA GURIA??" e "igual anime". |
| Copiar com cuidado | Referências certas; falta rejogar com o olho de designer | **Avançou.** Você trouxe o tutorial do Stellar Blade e a barra do Mega Man, que são lembranças de *como* o jogo ensina, não só de como ele parece. |
| Brinquedo primeiro | O núcleo existe, mas não foi validado por quem é de fora | **Meio validado:** "gostei muito da mecânica", mas só com metade do brinquedo (sem o tiro carregado). |
| Restrições espertas | Bem usadas no mangá e na fase única | Igual. O tutorial reaproveitou o roteiro de voo em vez de criar uma fase de treino. |
| Testar cedo e sempre | **O maior buraco hoje** | **Saiu do zero:** 1 teste de fora, do público-alvo, e ele mudou o jogo no mesmo dia. Ainda falta o volume: a meta era de 5 a 10. |

## Atualização de 2026-10-28 (v2.1.0): como você está agora

**Em uma frase:** ontem o risco era construir mais rápido do que gente de fora joga. Hoje você inverteu a ordem pela primeira vez: alguém jogou, apontou um buraco, e o dia inteiro foi para fechar esse buraco. É exatamente o ciclo que o vídeo descreve.

### O que mudou de verdade

- **Você publicou.** O link do Unity Play rodou no celular da Bruninha sem instalar nada. Antes não existia nenhum caminho para alguém de fora jogar; agora existe.
- **O primeiro teste foi o certo.** Ela é do público-alvo, jogou no aparelho onde o jogo é mais frágil (celular, sem a linha de ajuda) e você observou sem explicar. Quando perguntou do tiro carregado, ela desviou da pergunta, e você insistiu até a resposta honesta. Isso é técnica de playtest, e você fez sem ninguém ensinar.
- **O feedback virou código no mesmo dia, e na proporção certa.** Não teve fase de tutorial, sistema de conquistas ou tela de controles. Teve uma dica no meio da luta, que some quando a jogadora executa. A solução é do tamanho do problema.
- **Você usou o teste para responder às perguntas abertas de ontem.** "Voar e atirar é gostoso por si só?" Ela gostou mesmo jogando só com o básico. "O mangá multiplica ou é o motivo de jogar?" A reação à Ti'la e o "igual anime" mostram que a história pegou. Mas ela também elogiou a mecânica antes de falar da história, então não é um *Choices* com interlúdio.

### O que ainda pede atenção (franco)

- **Um teste é uma pessoa.** A Bruninha é generosa ("tá tudo bem ksksks") e da família. Ela não vai dizer "achei chato". Faltam os testes em que a pessoa não tem motivo para ser gentil: os colegas de trabalho, e alguém que nunca te viu, pelo itch.io.
- **O ciclo ainda não fechou.** Você corrigiu, mas ninguém de fora conferiu a correção. O próximo teste mais valioso é a mesma Bruninha jogando de novo, sem ninguém contar nada: o tiro carregado aparece sozinho?
- **Quase tudo de ontem e de hoje continua só compilado.** A pilha de "falta conferir em Play" cresceu: a barra, o `SEGUIR`, o atraso, a faixa no celular, o resultado com a pontuação total. Uma tarde só jogando com a checklist na mão continua sendo o melhor investimento antes de publicar no itch.io.
- **O instinto de sistema apareceu de leve.** Depois do tutorial vieram, no mesmo embalo, o pushback acelerando, o truque bônus e o tempo de fase oculto. São todas boas ideias, e todas suas (não do testador). Nada grave: só vale perceber que, quando o fluxo está bom, a vontade de ir somando volta. Pergunte "alguém de fora pediu isso?" antes do próximo item.
- **O capítulo 2 está no ponto certo:** em ideia, com briefing para o artista e com "protótipo com quadrados primeiro" escrito. Mantenha assim até o capítulo 1 ter os 5 a 10 testes.

### O que eu, como agente, preciso admitir de hoje

- Mandei o trecho do roteiro para você colar e você achou que já estava no jogo. Custou uma rodada.
- Fiz mudanças "por coerência" que você não pediu: troquei a fala da Mila e mexi na faixa do ZoneOverlay. Você chamou de irritante, com razão. Ficou registrado na memória para não repetir.
- Tirei e depois devolvi o "no meio da tela" da dica, por ter suposto onde ela seria usada em vez de perguntar.

O padrão dos três é o mesmo: agir em cima de uma suposição em vez de confirmar. É o espelho do risco que eu apontei em você ontem (construir antes de ouvir), só que em escala de conversa.

### A ordem, revisada

1. Uma tarde jogando o capítulo inteiro com a checklist de "falta conferir" (resumo, passo 000).
2. A Bruninha joga de novo, sem ninguém contar nada. Anotar em [playtests.md](../planejamento/playtests.md).
3. itch.io jogável no navegador, e de 5 a 10 testes que não sejam da família.
4. Só então: refactor do que o capítulo 2 precisa e o protótipo com quadrados da cobertura aérea.

## O que eu mais gostaria que você levasse

Você já provou que termina coisas, e isso é o que mais separa quem fica de quem desiste no indie. O próximo salto não é técnico: é aguentar ver gente de fora jogando, ouvir "não entendi" ou "ficou chato aqui", e ajustar sem defender o código. O mangá, a Mila e o caramelo já são seus e ninguém tira. Agora deixe o mundo jogar.
