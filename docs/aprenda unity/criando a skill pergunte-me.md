# Criando a skill pergunte-me

Entrevista de decisão antes de codar. Nota de método, para reusar fora deste projeto.

A nossa `.claude/skills/pergunte-me/SKILL.md` nasceu de um prompt do tipo *"com base na
skill grilling, pesquise na web, adapte à nossa realidade e crie uma skill"*. A ideia
reproduzível é essa: **pegar uma skill que existe, pesquisar, adaptar à realidade do projeto e
criar**. O que faz dar certo não é o prompt ser longo; é ele dizer **qual é a realidade**, para a
adaptação ter o que mudar.

### O prompt para colar no Claude

```text
Quero uma skill de entrevista de decisão antes de codar, no estilo "grilling"
(Matt Pocock, github.com/mattpocock/skills). Pesquise a original na web e leia
a ideia dela: perguntar em rodadas, só a fronteira de decisões que já dá para
perguntar, até chegar a um entendimento comum antes de escrever código.

Adapte para a minha realidade:
- quem decide: eu (<desenvolvedor solo / líder do time>); quem traz os fatos: você
- onde moram os fatos do projeto: <docs/, pastas de regras, o código que já responde>
- o que você faz com a decisão no fim: <bloco para outra IA / código / doc>
- como eu respondo: de cabeça, poucas perguntas por rodada, "sim" aceita sua recomendação
- língua: <pt-br>

Crie em .claude/skills/<nome>/SKILL.md, com uma description que dispare quando
eu disser "<gatilhos>". Depois rode a skill numa decisão real: <a decisão>.
```

### O mecânico

Uma skill é uma pasta `.claude/skills/<nome>/` com um `SKILL.md`. O arquivo começa com um
cabeçalho de duas linhas, `name` e `description`. A `description` é o gatilho: é por ela que o
Claude decide invocar sozinho. O resto é o roteiro, em texto corrido. Não tem código.

### A ideia, que é o que vale ensinar

- **Fatos são do Claude, decisões são do autor.** Antes da primeira pergunta ele lê o código e
  os docs e abre com "hoje o código faz X, a doutrina diz Y, o caso que motivou foi Z". Nunca
  pergunta o que dá para descobrir.
- **A regra é uma árvore; pergunte a fronteira.** Só o que não depende de resposta ainda não
  dada, no máximo quatro perguntas por rodada, porque o autor responde de cabeça.
- **Cada pergunta traz uma recomendação, escrita para que "sim" a aceite.** O autor responde
  "1 sim, 2 não: prefiro X".
- **Pergunte pela régua, não pelo número.** "O que eu perco se não atender agora?" decide mais
  que "quanto vale o bônus?".
- **Acaba quando a fronteira esvazia**, com um resumo com as frases do autor entre aspas, e
  espera confirmação antes de qualquer código.

### Como ela nasce de verdade

A primeira versão vai ser genérica. A nossa só ficou boa depois de algumas rodadas reais, quando
o Claude perguntou fato que podia descobrir, ofereceu três caminhos para uma regra só, ou virou
entrevista um bate-papo. Cada vez que isso irritar, o autor manda: *"isso não pode acontecer, põe
na skill"*. É assim que nascem as seções "O que não fazer" e "Quando parar": são cicatrizes, não
teoria.

Não precisa lembrar o prompt original. A skill pronta carrega a origem dentro dela (a nossa diz
no próprio arquivo de onde veio e sob qual licença). Para ensinar alguém, basta mandar o
`SKILL.md` como exemplo junto com o prompt acima: *"aqui está uma adaptada para um jogo de
estratégia, faça a sua para <o projeto>"*. O que a pessoa precisa adaptar é só a parte dos
fatos: onde eles moram no projeto dela e para onde vai a decisão depois.
