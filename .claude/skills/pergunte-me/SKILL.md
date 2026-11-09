---
name: pergunte-me
description: Entrevista em rodadas para fechar uma decisão de doutrina ou de design ANTES de codar — o agente pergunta, o autor decide. Use quando o autor disser "pergunte-me", "me pergunta", "me entrevista", "vamos decidir X", "quero fechar a regra de X", ou antes de mandar um bloco de correção ao Codex quando a doutrina ainda não está decidida. Não use para revisar log, diff ou commit.
---

# Pergunte-me

<!--
A short explanation of this skill:
It's mainly a decision-making interview before coding. The AI first checks the
existing code, architecture, and documented rules to see what already answers the
problem. Then it asks the author focused questions about anything still undecided,
including edge cases and trade-offs. The goal is to agree on the intended behavior
before implementation, while reusing existing logic and preserving what already works.
-->

Entrevista para chegar a um **entendimento comum** sobre uma regra antes de qualquer código. O
autor é um desenvolvedor solo decidindo a doutrina de um jogo: as decisões são dele, os fatos são
seus. Adaptado de `grilling`, de Matt Pocock (github.com/mattpocock/skills, licença MIT).

## A ideia

A regra a decidir é uma **árvore de decisões**: cada decisão abre as que dependem dela. A
**fronteira** são as decisões cujos pré-requisitos já estão resolvidos — as que dá para perguntar
**agora**, sem chutar respostas que ainda não vieram.

## Antes da primeira rodada: os fatos são seus

Nunca pergunte ao autor o que dá para descobrir. Antes de perguntar:

1. leia `docs/resumo.md`, em especial as **Regras de trabalho** ("procure a que já existe");
2. ache quem já responde a pergunta no código — a função dona (`FindRepairConstruction`,
   `MelhorDesembarque`, os sensores `Pode*`...) — e o que ela faz hoje, com arquivo:linha;
3. procure a doutrina escrita em `docs/AI Behavior/` e `docs/manual/` (por sinônimo, não só pelo
   nome: busca vazia não prova ausência) e nas memórias;
4. se uma marca do AI Step motivou a conversa, leia a marca.

Abra a primeira rodada com **três linhas** do que já existe ("hoje o código faz X em
`arquivo:linha`; a doutrina escrita diz Y; o caso que motivou foi Z"). Se um fato só fica pronto
depois (um subagente procurando, um teste rodando), segure só as perguntas que dependem dele e
pergunte o resto.

## As rodadas

Pergunte a fronteira inteira numa rodada, **no máximo 4 perguntas** — o autor responde de cabeça,
às vezes no fim do dia. Sobrou fronteira? Fica para a próxima rodada.

Formato:

```text
❓ **P1 — <título curto>**: <a pergunta, com as opções quando houver; cite o caso concreto do
   jogo (unidade, mapa, marca), não um exemplo abstrato>

➡ <sua recomendação, com o porquê em uma linha>

---

❓ **P2 — ...**
```

- Escreva cada pergunta de modo que **"sim" aceite a sua recomendação**. O autor responde
  "1 sim, 2 não: prefiro X, 3 sim".
- Pergunta que depende de outra **ainda aberta nesta rodada** vai para a rodada seguinte.
- A pergunta certa costuma ser a da **régua**, não a do número: *"o que eu perco se não atender
  agora?"* decide mais que *"quanto vale o bônus?"*. Prefira perguntar pela régua.
- Não ofereça três caminhos para uma regra que talvez seja uma só. Se parece haver dois
  caminhos, pergunte primeiro qual é a regra ("não tem essa salada").

Cada resposta muda a árvore: recalcule a fronteira e mande a próxima rodada.

## Quando parar

A entrevista acaba quando a fronteira fica vazia — nenhum galho visitado em silêncio, nada
assumido sem perguntar. Então:

1. **resuma as decisões** numa lista curta, com as frases do autor entre aspas quando ele as disse;
2. **espere a confirmação** de que é isso. Não aja antes;
3. confirmado, a decisão vai para onde mora:
   - **regra de IA** → o bloco de correção para o Codex (ou o código, se o autor pedir), com a
     doutrina citada e a função dona nomeada;
   - **doutrina que já assentou** → apontar no relatório do dia, seção "Docs de doutrina"; quem
     escreve o doc é a skill `consolidar-doutrina`, quando o autor chamar;
   - **lição de método** ("procure a que já existe", "leia o estado antes de quem o muda") →
     item novo nas Regras de trabalho do `docs/resumo.md`.

## O que não fazer

- Não transforme bate-papo em entrevista. Se o autor está pensando em voz alta, converse curto;
  esta skill é para quando ele **quer fechar** uma decisão.
- Não pergunte fato. Não peça ao autor arquivo, linha, valor de ficha ou o que o log diz.
- Não decida pelo autor o que é doutrina. Recomendar é seu; decidir é dele.
- Não escreva código nem doc de doutrina antes da confirmação do fim.
