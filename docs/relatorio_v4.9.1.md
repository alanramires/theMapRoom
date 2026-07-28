# v4.9.1 — Ataque Aéreo e Interceptadores 1/5

## Visão geral

Esta primeira etapa prepara a IA aérea para crescer sem depender da posição de
um papel dentro da ficha da unidade. Interceptadores e aeronaves de ataque agora
possuem uma leitura explícita de missão, que será a base para separar combate,
recuperação e carona nas próximas etapas.

## Perfis de missão aérea

O controller deixou de presumir que o primeiro item de `roles` define toda a
identidade da aeronave. A nova resolução consulta os papéis declarados na
ficha e classifica, por enquanto, dois perfis:

- **Interceptador**, para a futura política de defesa e caça aérea;
- **Ataque Aéreo**, para a futura política de pressão contra alvos em solo e
  superfície.

Essa base também é usada na leitura de alvos, evitando que uma futura ficha
híbrida seja interpretada de maneira diferente conforme o ponto do controller
que a consulta.

## Raid Anti-Sub permanece independente

Raid Anti-Sub continua passando pelo comportamento aéreo já existente, mas
fica conscientemente fora deste refactor. Sua missão naval e submersa merece um
ciclo próprio; ela não será absorvida pelas regras de caça ou ataque aéreo só
por compartilhar domínio.

## Sem alteração de gameplay nesta etapa

Prioridades de alvo, pouso, decolagem, autonomia, embarque e recuperação não
foram alterados ainda. A mudança é estrutural: ela remove uma fragilidade da
leitura de papéis antes que as políticas distintas sejam introduzidas.

## Contrato transacional preservado

A resolução de perfil é uma consulta de ficha. Ela não move unidades, não
altera camada, combustível, FOW, ocupação ou estado confirmado do tabuleiro.

## Validação

- build do runtime e do Editor sem erros;
- Interceptador e Ataque Aéreo reconhecidos sem `roles[0]`;
- Raid Anti-Sub mantido no comportamento legado.
