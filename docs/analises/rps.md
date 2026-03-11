# RPS (explicado em linguagem humana)

## O que e RPS?
RPS e uma "tabela de vantagem e desvantagem" entre tipos de unidade/arma.

Pense assim:
- algumas combinacoes sao naturalmente boas (bonus)
- algumas sao ruins (penalidade)
- algumas sao neutras (zero)

No jogo, o RPS entra como um ajuste numerico no combate.

## Para que serve
Sem RPS, quase tudo viraria disputa de numero bruto (HP, ataque base, defesa).
Com RPS, o jogo recompensa escolher a unidade/arma certa para o alvo certo.

Ou seja: estrategia de composicao e posicionamento importa mais.

## Como ele entra no combate
De forma simplificada, o RPS ajusta o valor de ataque (e tambem pode ajustar defesa, dependendo da regra).

Exemplo mental:
- Ataque base da arma: 10
- RPS: -4
- Ataque efetivo daquela interacao cai.

Outro caso:
- Ataque base da arma: 10
- RPS: +2
- Ataque efetivo sobe.

## Exemplo pratico
- Um helicóptero pode ter desvantagem contra certo alvo aereo (RPS negativo).
- Mas se esse alvo estiver grounded (aeronave no chao), a regra pode neutralizar penalidade pesada.

Na v1.2.4, por exemplo, quando o alvo da troca e aeronave grounded:
- o RPS de ataque usado vira `max(RPS, 0)`.

Traduzindo:
- se era negativo, sobe para 0
- se era positivo, mantem positivo

## Como ler no jogo
Quando o combate e resolvido, o trace/log mostra os termos:
- ataque base da arma
- RPS aplicado
- modificadores de skill
- defesa efetiva
- resultado final

Se o resultado parecer estranho, quase sempre vale conferir:
1. categoria da arma
2. classe da unidade alvo
3. camada/dominio do alvo
4. se havia alguma regra especial ativa (ex.: grounded)

## Resumo curto
RPS e o sistema que transforma "quem atira" + "com o que atira" + "em quem atira" em vantagem real (ou desvantagem) no dano final.
