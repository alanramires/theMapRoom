# Refinamento: Embarque de Capturadores com Quero Carona II

## Versão

`v5.1.3a`

## Objetivo

Consolidar a autoridade do passageiro na decisão de embarque e reduzir a
dispersão de combatentes sem tarefa formal.

O transportador não determina a agenda do capturador. Primeiro os
capturadores distribuem entre si as oportunidades de conquista alcançáveis;
depois cada unidade informa se consegue cumprir sua missão em Tactical ou
Operational. Somente quem permanece sem oportunidade adequada solicita
carona.

## Claim 1:1 orientado pelo capturador

O matching de construções continua maximizando o número de capturadores com
objetivos distintos, mas o desempate agora prioriza de forma estável o
capturador antes do custo de rota.

Consequências:

- a intenção do passageiro é definida antes da escolha do transportador;
- o capturador que alcança sua conquista em Tactical ou Operational recusa
  carona;
- oportunidades já atribuídas não fazem vários soldados recusarem transporte
  pelo mesmo prédio;
- quem sobra sem oportunidade declara `Requested`;
- o transportador apenas consulta os pedidos e escolhe outro passageiro quando
  o candidato alcança seu objetivo sozinho.

## Validação em jogo

O cenário observado possuía Soldado #2 e Bazooka #72 disputando uma construção
ao norte.

Resultado confirmado:

```text
Soldado #2
  construção: (-12, 9, 0)
  envelope: Operational
  custo: 5 <= 6
  decisão: recusa carona

Bazooka #72
  oportunidade local: atribuída ao Soldado #2
  decisão: solicita carona

Chinook #85
  passageiro escolhido: #72
  LZ: (-11, 12, 0)
  passageiro: ReachableNow
  custo de embarque: 0 + 1 = 1
```

O efeito visual da fumaça verde foi produzido pelo passageiro que realmente
precisava do transporte. Não houve imposição do táxi nem seleção por
`OpportunisticFallback`.

## Capturador como cabeça de ponte

Combatentes sem tarefa formal passam a usar o Capturador aliado ativo mais
próximo como direção:

- Assault rogue;
- FireSupport rogue;
- Interceptador;
- Ataque Aéreo;
- Raid AntiSub;
- aeronave combatente ainda embarcada, ao informar sua direção ao
  transportador.

O objetivo é reduzir unidades espalhadas pelo mapa e impedir que aeronaves
iniciem campanhas isoladas sem relação com a infantaria que materializa a
frente.

Ataques e necessidades Tactical continuam tendo precedência. O ímã atua
somente como fallback de direção e é recalculado após cada estado confirmado.

## Faixa preferencial de escolta

Aeronaves combatentes preferem permanecer a 1 hex do Capturador usado como
âncora.

Essa faixa é uma preferência de ranking:

- distância de 1 hex recebe bônus;
- sobreposição no mesmo hex recebe penalidade;
- o hex do capitão não se torna ilegal;
- quando não existe alternativa útil, a sobreposição continua permitida.

Em unidades terrestres, a própria ocupação normalmente produz esse respiro.

## Mapas com ilhas

A emenda melhora coesão, mas ainda não resolve a travessia coletiva.

Depois que um Capturador atravessa o mar:

- aeronaves podem continuar acompanhando a cabeça de ponte;
- combatentes terrestres avançam na direção da costa;
- outro Capturador mais próximo pode assumir como ímã;
- Capturadores embarcados não atuam como âncora ativa;
- unidades terrestres ainda precisam declarar futuramente uma ruptura de
  mobilidade por meio do `Quero Carona` tipado.

O plano completo de travessias, finalidades de carona e componentes
desconectados permanece documentado em `docs/quero_carona_refactor.md`.

## Contrato transacional

As novas decisões são consultas puras ao snapshot confirmado:

- não reservam destino definitivo;
- não movem passageiro ou transportador;
- não alteram FOW;
- não consomem movimento, combustível ou recursos;
- não modificam `HasActed`;
- são recalculadas depois do compromisso e do retorno a `Neutral`.

O movimento continua sendo materializado e comprometido pelo fluxo normal do
turno.

## Validação técnica

- `Assembly-CSharp.csproj`: compilação concluída, 0 erros;
- `Assembly-CSharp-Editor.csproj`: compilação concluída, 0 erros;
- `git diff --check`: nenhuma inconsistência de patch;
- teste runtime: Soldado #2 recusou corretamente no Operational e Chinook #85
  selecionou Bazooka #72 com `carona=Requested`.

