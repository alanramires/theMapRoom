# Matriz de RPS e Elite

*Como cada classe de arma se comporta contra cada classe de unidade, e onde o Elite entra nessa conta.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

> **Catálogo incompleto.** As matrizes abaixo ainda não foram preenchidas. Campo ausente não significa "sem valor" — na falta de ficha, a autoridade é o asset do jogo.

O sistema por trás destas tabelas está explicado em `06_combate.md`. Aqui ficam apenas os valores.

## Matriz de RPS

Cada confronto entre duas classes gera **quatro** entradas, e todas precisam estar declaradas:

| Entrada | Significado |
|---|---|
| RPS Ataque do operador | soma à potência da arma de quem ataca |
| RPS Defesa do alvo | soma à defesa de quem recebe |
| RPS Ataque do alvo | vale quando ele for o atacante do confronto inverso |
| RPS Defesa do operador | vale quando ele for o defensor do confronto inverso |

Linhas: classe de unidade operadora × classe de arma. Colunas: classe de unidade alvo.

*A preencher.*

## Especializações de Elite

Cada especialização precisa declarar os três filtros que a doutrina exige — classe do oponente, categoria da arma, e a relação de nível — mais os quatro valores que ela move de uma vez: ataque próprio, defesa própria, ataque do oponente, defesa do oponente.

Especializações válidas no mesmo confronto **se somam**.

*A preencher.*

## Nota de auditoria

Nenhuma afirmação deste documento foi verificada contra o código. O capítulo de Elite em `06_combate.md` — os três filtros, os quatro valores movidos, a soma de especializações — está na fila de auditoria registrada em `92_auditoria.md`.
