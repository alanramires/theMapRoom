Quero que você me atualize sobre o estado REAL atual do projeto com base no código-fonte existente, não com base em textos antigos, planos ou intenções.

Responda de forma objetiva e concreta, sempre citando:

* o que já existe funcionando no código
* o que existe parcialmente
* o que ainda não existe
* quais classes/arquivos principais sustentam isso

Perguntas:

A) Quais são hoje as camadas centrais da arquitetura do jogo?
Quero um mapa das peças principais: turn/state manager, unit manager, combat, fog/vision, AI, shopping, planner, save/load, replay, UI.

B) O sistema de turnos/estados hoje está consolidado em qual fluxo?
Liste os estados reais existentes no código e como o jogo passa de um para outro.

C) Como está hoje o sistema de visão e detecção?
Quero saber o que está realmente implementado para:

* visão de terreno
* detecção de unidades
* contributors/quem vê quem
* diferenças entre enxergar, detectar e revelar
* quais domínios/camadas já participam disso

D) Quais domínios e alturas existem de fato no código hoje?
Liste todos os domains/heights/layers reais usados por unidades e regras, e onde eles impactam:

* movimento
* combate
* visão
* bloqueio de hex
* embarque/desembarque
* supply

E) O combate hoje depende de quais fórmulas e dados?
Quero saber:

* como ataque/defesa/dano/revide são calculados
* quais multiplicadores entram
* o que vem de terreno, HP, ammo, domain, etc
* quais partes estão hardcoded e quais estão data-driven

F) O DPQ ou alguma métrica derivada da fórmula já está realmente usada no projeto?
Se sim, onde?
Se não, o que existe hoje no lugar para avaliação de combate, targeting ou balanceamento?

G) Como está hoje o planner da IA?
Explique o que já existe de fato para:

* criação de planos
* tipos de plano
* condições de ativação
* alocação de unidades
* reavaliação por turno
* consolidação/hold/secure de setor
* abandono ou troca de plano

H) Como está hoje o shopping manager?
Quero o fluxo real:

* como demandas são geradas
* como prioridades são comparadas
* como orçamento influencia
* se existe saving for expensive unit
* se existe pressão logística
* como decide entre capturador, combate, suporte e supply

I) Como está hoje o comportamento por unidade via AIUnitProfile?
Quero saber o que já é realmente lido pela IA e o que ainda é campo sem uso.
Separar claramente:

* campos ativos
* campos obsoletos
* campos definidos mas ainda não conectados

J) Transporte e logística: o que já existe mesmo?
Separar por tipo:

* terrestre
* naval
* aéreo
  Para cada um, dizer o que funciona hoje em código:
* embarcar
* desembarcar
* reabastecer
* reparar
* transferir
* restrições por domínio/camada

K) O sistema de save/load hoje cobre exatamente o quê?
Quero saber o que persiste e o que ainda não persiste direito:

* unidades
* FoW
* planos da IA
* shopping pressures
* replay
* estados de UI
* construções
* autonomia/ammo/HP/status

L) O replay hoje está em que estágio real?
Existe snapshot?
Existe command log?
Existe reprodução jogável?
O que já é arquitetura pronta e o que já roda de verdade?

M) Quais são hoje os principais pontos fortes do projeto vistos no código?
Não quero elogio abstrato. Quero 5 a 10 pontos fortes concretos, do tipo:

* separação boa entre X e Y
* sistema Z está maduro
* tal fluxo está consistente
* tal parte está fácil de expandir

N) Quais são hoje os principais riscos, dívidas ou incoerências?
Também quero 5 a 10 pontos concretos, com foco em:

* código duplicado
* responsabilidade misturada
* sistemas que parecem prontos mas ainda estão frágeis
* campos não usados
* regras paralelas
* pontos que podem quebrar quando entrar naval/aéreo/transporte completo

O) Se você tivesse que resumir o estágio atual do projeto em 1 diagnóstico técnico e 1 diagnóstico de design, quais seriam?

No final, faça mais 3 blocos:

1. “Já funciona de verdade”
2. “Existe, mas ainda está incompleto”
3. “Próximos passos mais sensatos pela arquitetura atual”

Se possível, inclua nomes de arquivos e classes mais importantes em cada resposta.
