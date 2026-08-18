# Curso de C# — pelo The Map Room

Um curso de C# e Unity **feito em cima do seu próprio jogo**. Nenhum exemplo de
`Animal → Cachorro`, nenhum `Player.cs` de tutorial. Todo conceito é apresentado
com o arquivo, a linha e o bug que ele já causou aqui dentro.

---

## Por que este curso existe

Você escreveu isto:

> *"quando os créditos acabam eu fico parado pq não sei mexer no monstro que
> construímos"*

Essa frase é o diagnóstico inteiro, e ela **não é sobre C#**. Repare no que ela
diz e no que ela não diz:

```text
não diz    "não sei o que é um for"
diz        "não sei ONDE mexer, nem o que quebra quando eu mexo"
```

São problemas diferentes, e o segundo não se resolve com um curso de sintaxe.

O `docs/avaliacao do autor/diagnostico do autor.md` já tinha registrado isso:
*"programador iniciante — baixa aderência"*. Um iniciante não sustenta 460
arquivos com máquina de estados, cache com invalidação por revisão e um sistema
de sensores. Você não é iniciante. Você é **um programador pragmático que nunca
foi obrigado a ler o próprio código**, porque sempre teve alguém lendo por você.

Então o curso tem um objetivo declarado, e é este:

> **Que você abra um arquivo qualquer do The Map Room e saiba dizer, sozinho, o
> que ele faz, quem chama ele, e o que arrebenta se você mudar.**

Escrever código novo vem depois — e vem fácil, quando a leitura está resolvida.

---

## O roadmap que você trouxe, e o que eu fiz com ele

O roadmap que a outra IA montou é um índice genérico de C#/Unity. Ele é honesto e
bem organizado, mas foi escrito **sem olhar o seu jogo**. Eu olhei — medi os 460
arquivos, um por um, procurando cada tópico da lista.

O resultado está em [00_diagnostico.md](00_diagnostico.md), e ele muda o curso:

- **Capítulos inteiros do roadmap não têm um único uso no seu código.** Física
  (capítulo 8) aparece **zero** vezes. `SendMessage`/`BroadcastMessage`/
  `UnityEvent` (5.2): **zero**. `async`/`await` (9.1): **um** arquivo.
  Addressables: **zero**.
- **O que seu jogo mais usa não está no roadmap.** `partial class` — 138
  arquivos, e o `AIController` sozinho tem **101**. `ScriptableObject` como
  catálogo — 46. Eventos estáticos como espinha dorsal. Nada disso ganhou um
  tópico na lista.

O roadmap ensinaria você a fazer um jogo. Este curso ensina você a mexer **no
seu**.

Nada foi jogado fora: o mapeamento tópico-a-tópico do roadmap original está no
diagnóstico, marcando o que virou aula, o que virou nota de rodapé e o que ficou
de fora — com o motivo.

---

## A trilha

Dez aulas, na ordem que devolve autonomia mais rápido. Cada uma cabe numa sessão
de estudo; nenhuma passa de meia hora de leitura.

### Parte I — Ler

O objetivo aqui não é escrever nada. É abrir um arquivo e entender.

| # | aula | você sai sabendo |
|---|---|---|
| 1 | [Anatomia de um arquivo](01_anatomia_de_um_arquivo.md) | ler `QuadranteData.cs` inteiro, linha por linha, sem pular nada |
| 2 | [Valor vs referência](02_valor_vs_referencia.md) | por que `cell.z = 0` existe em todo canto do seu código |
| 3 | [Achar o arquivo certo](03_partial_e_navegacao.md) | navegar 101 arquivos de `AIController` sem se perder |

### Parte II — Os dados

| # | aula | você sai sabendo |
|---|---|---|
| 4 | [Coleções](04_colecoes.md) | por que `HashSet` em 136 arquivos e `Stack` em 2 |
| 5 | [LINQ](05_linq.md) | o que você não usa, por que, e quando valeria |

### Parte III — Unity

| # | aula | você sai sabendo |
|---|---|---|
| 6 | [ScriptableObject e ciclo de vida](06_scriptableobject_e_ciclo.md) | catálogo vs cena — a doutrina do `CLAUDE.md`, agora em código |
| 7 | [Eventos](07_eventos.md) | `static event`, e o vazamento que você não teve por sorte |
| 8 | [Corrotinas](08_corrotinas.md) | as quatro fases do turno da IA, do `yield` ao fim |

### Parte IV — Autonomia

| # | aula | você sai sabendo |
|---|---|---|
| 9 | [Quando trava](09_quando_trava.md) | ler um erro do Console e achar a linha, sem me chamar |
| 10 | [Ferramenta de editor](10_ferramenta_de_editor.md) | mexer no `MapHelperWindow` — sua frente aberta é campanha |

E os [exercícios](exercicios.md): tarefas reais no seu repositório, em ordem de
dificuldade, com gabarito.

---

## Como estudar

**Uma aula por sessão, com o Unity aberto e o arquivo na tela.** As aulas citam
arquivo e linha justamente pra isso — ler a aula sem abrir o código é perder
metade.

Toda aula tem a mesma forma:

```text
O que é          o conceito, curto
No seu jogo      onde ele está, com arquivo:linha
A armadilha      o que ele quebra quando mal entendido
Exercício        uma tarefa no repositório
```

**Faça o exercício.** É a única parte que não dá pra terceirizar, e é onde o
aprendizado acontece de verdade. Ler sobre `partial class` custa cinco minutos;
achar sozinho qual dos 101 arquivos decide se um capturador embarca — isso muda
o que você consegue fazer sozinho na semana seguinte.

---

## Suas notas ficam em `notas/`

O curso tem dois donos, e a divisão é explícita:

```text
docs/curso de CSharp/*.md          as AULAS — do Claude. Ele corrige e melhora.
docs/curso de CSharp/notas/*.md    suas NOTAS — suas. Ele nunca edita.
```

**Por quê:** na primeira semana os dois editaram o mesmo arquivo ao mesmo tempo, e
o último save ganhou — uma correção já acordada foi desfeita sem ninguém notar.
Um arquivo, um dono, e o problema some.

Você pode pedir a qualquer momento: *"olha minhas notas"* — para revisão,
correção, ou para promover uma nota sua ao corpo da aula. O caminho é sempre esse
sentido, das notas para a aula, nunca o contrário.

**Uma armadilha prática:** salve sempre em **UTF-8**. Um editor gravou a aula 1 em
ANSI e todo acento virou byte inválido para o git e para o GitHub. No VS Code a
codificação aparece no canto inferior direito — se não disser `UTF-8`, clique nela
e use *Save with Encoding*.

---

## Duas regras de segurança

Você vai estudar mexendo num projeto de anos. Então:

1. **`git status` antes, `git status` depois.** Todo exercício é reversível. Se
   você não sabe desfazer, não comece.
2. **Não salve `.cs` com o Editor em Play.** Já está no `resumo.md` como regra de
   trabalho — vale igual quando o motivo é estudo.

Os exercícios foram desenhados pra não tocar em nada que esteja no caminho
crítico das frentes abertas (campanha, bloqueios 0a e 0b).

---

## O que este curso não é

Não é um substituto pro `CLAUDE.md` nem pro `docs/manual/`. Aqueles dois
ensinam **as regras do jogo e onde uma regra pode morar** — decisões de design
que não se recuperam lendo código. Este curso ensina **a linguagem em que essas
decisões estão escritas**.

Ordem sugerida quando bater dúvida: aula → código → `CLAUDE.md`. Nessa direção.
