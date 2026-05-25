# 11 - Perguntas Gerais de Analise do Projeto

Este documento e um questionario de referencia para analise tecnica do projeto a partir do codigo-fonte.
As perguntas cobrem todas as camadas principais do sistema.

## Onde as respostas estao (indice de referencia cruzada)

| Pergunta | Topico | Respondida em |
|---|---|---|
| A | Camadas da arquitetura | `09_game_systems_overview.md` |
| B | Sistema de turnos/estados | `07_relatorio_turn_state_manager.md` |
| C | Visao e deteccao | `05_relatorio_visao_spotting.md` |
| D | Dominios e alturas | `03_relatorio_terrenos_dpq.md`, `05_relatorio_visao_spotting.md` |
| E | Formulas de combate | `02_relatorio_sistema_combate.md` |
| F | DPQ no projeto | `02_relatorio_sistema_combate.md`, `03_relatorio_terrenos_dpq.md` |
| G | Planner da IA | `12_AI_do_projeto.md`, `CLAUDE.md` |
| H | Shopping manager | `12_AI_do_projeto.md`, `CLAUDE.md` |
| I | AIUnitProfile / papeis | `12_AI_do_projeto.md`, `CLAUDE.md` |
| J | Transporte e logistica | `04_relatorio_logistica.md`, `12_AI_do_projeto.md` |
| K | Save/load | nao documentado em detalhe |
| L | Replay | nao documentado em detalhe |
| M | Pontos fortes | `10_Andamento_projeto.md` |
| N | Riscos e incoerencias | `10_Andamento_projeto.md` |
| O | Diagnostico geral | `10_Andamento_projeto.md` |

---

## Perguntas

Quero que voce me atualize sobre o estado REAL atual do projeto com base no codigo-fonte existente, nao com base em textos antigos, planos ou intencoes.

Responda de forma objetiva e concreta, sempre citando:

* o que ja existe funcionando no codigo
* o que existe parcialmente
* o que ainda nao existe
* quais classes/arquivos principais sustentam isso

### A) Quais sao hoje as camadas centrais da arquitetura do jogo?
Quero um mapa das pecas principais: turn/state manager, unit manager, combat, fog/vision, AI, shopping, planner, save/load, replay, UI.

### B) O sistema de turnos/estados hoje esta consolidado em qual fluxo?
Liste os estados reais existentes no codigo e como o jogo passa de um para outro.

### C) Como esta hoje o sistema de visao e deteccao?
Quero saber o que esta realmente implementado para:

* visao de terreno
* deteccao de unidades
* contributors/quem ve quem
* diferencas entre enxergar, detectar e revelar
* quais dominios/camadas ja participam disso

### D) Quais dominios e alturas existem de fato no codigo hoje?
Liste todos os domains/heights/layers reais usados por unidades e regras, e onde eles impactam:

* movimento
* combate
* visao
* bloqueio de hex
* embarque/desembarque
* supply

### E) O combate hoje depende de quais formulas e dados?
Quero saber:

* como ataque/defesa/dano/revide sao calculados
* quais multiplicadores entram
* o que vem de terreno, HP, ammo, domain, etc
* quais partes estao hardcoded e quais estao data-driven

### F) O DPQ ou alguma metrica derivada da formula ja esta realmente usada no projeto?
Se sim, onde?
Se nao, o que existe hoje no lugar para avaliacao de combate, targeting ou balanceamento?

### G) Como esta hoje o planner da IA?
Explique o que ja existe de fato para:

* criacao de planos
* tipos de plano
* condicoes de ativacao
* alocacao de unidades
* reavaliacao por turno
* consolidacao/hold/secure de setor
* abandono ou troca de plano

### H) Como esta hoje o shopping manager?
Quero o fluxo real:

* como demandas sao geradas
* como prioridades sao comparadas
* como orcamento influencia
* se existe saving for expensive unit
* se existe pressao logistica
* como decide entre capturador, combate, suporte e supply

### I) Como esta hoje o comportamento por unidade via AIUnitProfile?
Quero saber o que ja e realmente lido pela IA e o que ainda e campo sem uso.
Separar claramente:

* campos ativos
* campos obsoletos
* campos definidos mas ainda nao conectados

### J) Transporte e logistica: o que ja existe mesmo?
Separar por tipo:

* terrestre
* naval
* aereo

Para cada um, dizer o que funciona hoje em codigo:

* embarcar
* desembarcar
* reabastecer
* reparar
* transferir
* restricoes por dominio/camada

### K) O sistema de save/load hoje cobre exatamente o que?
Quero saber o que persiste e o que ainda nao persiste direito:

* unidades
* FoW
* planos da IA
* shopping pressures
* replay
* estados de UI
* construcoes
* autonomia/ammo/HP/status

### L) O replay hoje esta em que estagio real?
Existe snapshot?
Existe command log?
Existe reproducao jogavel?
O que ja e arquitetura pronta e o que ja roda de verdade?

### M) Quais sao hoje os principais pontos fortes do projeto vistos no codigo?
Nao quero elogio abstrato. Quero 5 a 10 pontos fortes concretos, do tipo:

* separacao boa entre X e Y
* sistema Z esta maduro
* tal fluxo esta consistente
* tal parte esta facil de expandir

### N) Quais sao hoje os principais riscos, duvidas ou incoerencias?
Tambem quero 5 a 10 pontos concretos, com foco em:

* codigo duplicado
* responsabilidade misturada
* sistemas que parecem prontos mas ainda estao frageis
* campos nao usados
* regras paralelas
* pontos que podem quebrar quando entrar naval/aereo/transporte completo

### O) Se voce tivesse que resumir o estagio atual do projeto em 1 diagnostico tecnico e 1 diagnostico de design, quais seriam?

No final, faca mais 3 blocos:

1. "Ja funciona de verdade"
2. "Existe, mas ainda esta incompleto"
3. "Proximos passos mais sensatos pela arquitetura atual"

Se possivel, inclua nomes de arquivos e classes mais importantes em cada resposta.
