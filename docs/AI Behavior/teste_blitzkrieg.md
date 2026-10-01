# Primeiro teste: blitzkrieg sem plano

**Status: especificação do comportamento desejado.** Este documento não implementa nem declara aprovado o comportamento atual. Complementa [capturador_politicas.md](capturador_politicas.md) e utiliza as regras de [Papeis.md](Papeis.md).

## Objetivo

Sem HQ, sem plano e sem eixo, dois capturadores devem conseguir revezar a captura quando a política Blitzkrieg estiver habilitada. Plano e eixo serão acrescentados depois como orientação, sem se tornarem requisitos da troca.

O teste pode ser montado em uma cópia de trabalho do cenário Quadrado. Não é necessário alterar a cena original para definir esta especificação.

## Situação inicial

Preparar o início da vez da IA, antes de qualquer unidade agir:

| Elemento | Preparação |
|---|---|
| Mapa | Sem HQ de nenhum jogador, sem plano atribuído e sem eixo. |
| Terreno | Corredor transitável, com custo de 1 ponto de movimento por hex e espaço livre para avançar. |
| Prédio B | Construção capturável não aliada, com captura já iniciada e ainda não concluída. |
| Prédio F | Outra construção capturável não aliada, conhecida pela IA, a 6 passos de B pelo corredor. |
| Capturador A | Sobre B, com 3 pontos de movimento disponíveis, ainda sem agir. |
| Capturador C | A 3 passos atrás de B, com 3 pontos de movimento disponíveis, ainda sem agir. |
| Compatibilidade | A e C podem capturar B e F pelas regras das construções. |
| Recursos e ameaças | Autonomia suficiente para os deslocamentos; sem combate, transporte ou atendimento concorrendo com a decisão no caso básico. |
| Perfil | Blitzkrieg explicitamente habilitada no perfil efetivo da execução. |

B deve exigir mais de uma ação para ser concluído no caso de referência. F precisa oferecer uma continuação concreta da agenda de A. Os dois prédios podem estar no mesmo setor: a troca não pode exigir setores diferentes.

## Execução esperada

1. A IA identifica que C consegue entrar no próprio hex de B nesta rodada, depois que A sair. Alcançar apenas um vizinho não basta.
2. A iniciativa coloca A antes de C.
3. A prepara um movimento de 3 passos na direção de F. Até a confirmação, B continua ocupado no estado confirmado e a substituição ainda não ocorreu.
4. A confirma o deslocamento e retorna a Neutral. Sua posição, a visão e as demais informações definitivas são atualizadas pelo fluxo normal.
5. A IA revalida a entrada de C em B a partir desse estado confirmado.
6. C entra em B e confirma Capturar. Ele pode continuar ou concluir a captura, conforme seu poder efetivo.

Ao fim da vez, A está entre B e F e C ocupa B. Não é necessário que C termine a captura para justificar a troca. Não deve haver ataque, embarque ou espera de C tomando o lugar da captura que motivou o revezamento no caso básico.

Na rodada seguinte, A deve continuar sua agenda rumo a F. Não deve retornar a B por causa de uma atribuição antiga. A continuidade de C depende de B já estar conquistado ou ainda precisar de captura.

## Variações obrigatórias

Cada variação parte novamente da situação inicial, alterando somente a condição indicada.

| Caso | Alteração | Resultado esperado |
|---|---|---|
| A — referência | Nenhuma. | A avança; C assume B na mesma rodada. |
| B — substituto ferido | Reduzir o HP de C, mantendo-o capaz de se mover e capturar, com autonomia suficiente. | O revezamento continua válido mesmo que C capture mais lentamente. Registrar se o estado de reparo interfere: HP baixo, por si só, não deve vetar a política. |
| C — apenas operacional | Colocar C a 4 passos de B no mesmo corredor de custo 1. | A permanece em B e captura. Não deixa o prédio vazio para a próxima rodada. |
| D — política desligada | Desabilitar Blitzkrieg no perfil efetivo, mantendo os dois saudáveis e com o mesmo poder de captura. | A permanece capturando. Não pode ocorrer saída equivalente por uma condição antiga ligada à dificuldade. |
| E — orçamento insuficiente | C tem 3 pontos, mas o caminho até B custa 4; ele consegue alcançar um vizinho. | A permanece capturando. Distância geométrica ou adjacência alcançável não substituem o custo de entrada no prédio. |
| F — substituto já agiu | Marcar C como já agido. | A permanece capturando: C não pode assumir nesta rodada. |
| G — cancelamento | Interromper/cancelar a prévia da saída de A antes do compromisso. | Restaurar o estado confirmado anterior, sem missão definitiva de troca, informação revelada ou captura transferida. Ao retomar, reavaliar a decisão. |

## Comparação com orientação estratégica

Depois de aprovar os casos sem plano, repetir a situação de referência em um contexto com base, atribuições e eixo B–F.

O resultado local deve permanecer o mesmo. O plano/eixo pode orientar a continuação de A, mas não deve exigir outro objetivo com vaga, outro setor ou uma classificação específica para autorizar a política.

Se o plano mantiver uma reserva de F para C, o revezamento precisa coordenar essa reserva com a nova agenda de A. Não basta mover A enquanto C continua comprometido com uma tarefa incompatível com assumir B.

## Evidência necessária

Para cada execução, registrar:

- Perfil efetivo e estado da política, sem depender apenas do nome do asset ou do checkbox de velocidade de execução da IA.
- IDs de A e C, posições, HP, movimento disponível e estado de ação.
- Prédio considerado, custo real de entrada de C e motivo de aceitar ou recusar a troca.
- Destino e motivo da continuação de A.
- Ordem das ações, confirmação ou cancelamento e posições finais.

Exemplo de explicação esperada: “Blitzkrieg aceita: C alcança B nesta rodada e ainda pode agir; A libera B e avança para F. HP baixo de C não impede a troca.”

Exemplo de recusa: “Blitzkrieg recusada: C só alcança B na próxima rodada; A mantém a captura.”

## Critério de aprovação e limite deste primeiro trabalho

O primeiro comportamento está aprovado quando as variações produzem os resultados descritos, o avanço continua na rodada seguinte e a presença ou ausência de plano não muda a elegibilidade local da troca.

Este teste não pretende reorganizar todos os papéis, resolver todas as prioridades de recuperação nem implementar captura oportunista, cobertura ou spotting. Ele verifica uma política, sua coordenação entre duas unidades e o respeito ao compromisso das ações. Situações não cobertas viram novos casos, sem exigir antecipar toda a gameplay.
