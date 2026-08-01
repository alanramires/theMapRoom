# Hotzone como Envelope de Serviço

## Versão

`v6.0.4`

## Objetivo

Este ponto de verificação consolida a transformação da antiga Hotzone de combate em um envelope geral de alcance materializável.

A pergunta deixa de ser apenas “onde esta unidade ameaça?” e passa a ser:

> Dentro de qual horizonte esta unidade consegue chegar e materializar determinada intenção pelas regras oficiais do jogo?

O serviço compartilhado passa a atender Editor, apresentação ao jogador e tomada de decisão da IA sem duplicar regras de movimento, domínio ou custo de entrada.

## Dois eixos explícitos

O novo modelo separa duas dimensões que antes estavam compostas principalmente na ferramenta de Editor.

### Intenção

- `Combat`: movimento e ataque, validados pelo `PodeMirarSensor`;
- `Service`: atendimento logístico, conforme `serviceRange` e `PodeSuprirSensor`;
- `Transfer`: coleta e transferência de estoque dos hubs, conforme `collectionRange`;
- `Fusion`: fusão de unidades, incluindo o custo real de entrada resolvido pelo `PodeFundirSensor`;
- `Embark`: encontro para embarque, incluindo o custo oficial resolvido pelo `PodeEmbarcarSensor`.

### Banda

- `Tactical`: ação materializável na rodada atual;
- `Operational`: alcance próprio em múltiplos turnos, com orçamento de MP e custos reais;
- `Strategic`: direção para um objetivo fora da rota própria, sem fingir que existe um caminho materializado.

`Strategic` não cria uma lista artificial de células alcançáveis. O perfil apenas escolhe, entre as células Tactical ou Operational já calculadas, aquela que fornece a melhor direção cúbica para o objetivo distante.

## UnitReachEnvelopeService

Foi criado `UnitReachEnvelopeService` como fonte compartilhada do envelope.

O pedido é representado por `UnitReachRequest`, contendo:

- unidade;
- tabuleiro e banco de terrenos;
- intenção;
- banda;
- orçamento de movimento;
- horizonte Operational;
- caminhos ou custos já calculados, quando disponíveis;
- filtro opcional por domínio operacional;
- parâmetros específicos de combate, serviço ou embarque.

O resultado é um `UnitReachEnvelope`, que expõe:

- caminhos por destino;
- custo conhecido por célula;
- células onde a unidade pode parar;
- células onde a intenção pode ser materializada;
- anel externo da ação;
- consultas de alcance, ação e custo.

`UnitReachProfile` combina Tactical e Operational e classifica qualquer destino nas três bandas.

## Reuso das fontes oficiais

O novo serviço não implementa um pathfinding paralelo.

Ele reutiliza:

- `UnitMovementPathRules` para caminhos válidos e custos;
- `AIActionReachCoordinator` para alcance setorial e geometria cúbica de aeronaves;
- `PodeMirarSensor` para combate;
- `PodeSuprirSensor` para serviço e transferência;
- `PodeFundirSensor` para custo de fusão;
- `PodeEmbarcarSensor` para custo e validade de embarque.

Quando o consumidor já possui uma onda de caminhos ou custos, ela pode ser entregue ao pedido e reutilizada.

## Compatibilidade durante a migração

`UnitThreatEnvelopeService` tornou-se uma fachada temporária.

Ele não mantém mais regras próprias: traduz chamadas antigas para `UnitReachEnvelopeService` e converte o resultado para os tipos legados. Isso permite migrar os consumidores gradualmente sem quebrar a jogabilidade no meio do refactor.

Quando não houver mais consumidores antigos, a fachada e o alias `UnitThreatEnvelope` poderão ser removidos.

## Ferramenta Hotzone

`Tools > Utils > Hotzone` agora permite escolher:

- Combate;
- Logística;
- Transferência;
- Fusão;
- Embarque.

A janela não recompõe mais movimento, domínio e alcance por conta própria. Ela solicita ao serviço as bandas Tactical e Operational e apenas apresenta o resultado.

O diagnóstico informa:

- intenção consultada;
- quantidade de células Tactical;
- quantidade de células Operational;
- orçamento Operational;
- geometria utilizada;
- quantidade de células restantes tratadas apenas como direção Strategic.

## Panel Helper

A inspeção de unidades passou a usar o mesmo envelope compartilhado.

A sequência visual respeita as capacidades reais da unidade:

1. combate;
2. combate a distância;
3. serviço logístico;
4. transferência de estoque para hubs;
5. Fog of War.

Foram adicionados:

- `white ring tools` para alcance de serviço;
- `white ring transfer` para transferência;
- sobreposição do deslocamento com `white ring black`;
- cor do time para os ícones de intenção;
- filtro de domínio operacional do fornecedor;
- proteção de Fog of War, impedindo a pintura sobre células ainda desconhecidas;
- salto direto para visão quando a unidade não possui armas nem capacidade logística aplicável.

Unidades `StockTransfer` não exibem a chave de serviço de campo. Hubs continuam exibindo transferência, e unidades híbridas como o porta-aviões percorrem combate, serviço e transferência conforme suas capacidades.

## Logística da IA

A seleção logística passou a trabalhar com o alvo específico escolhido e com a geometria oficial de serviço.

O posicionamento considera:

- alcance `SameHexOrEmbarked`, adjacente ou híbrido;
- célula real do alvo;
- passageiros próprios embarcados;
- domínio operacional;
- ocupação;
- construções produtoras;
- sensor oficial de suprimento;
- ameaça, DPQ e posição de retaguarda.

Isso reduz avaliações incompatíveis e aproxima o raciocínio da IA da área que o jogador enxerga no `panel_helper` e na ferramenta Hotzone.

## Captura data-driven

A capacidade de captura deixou de depender de:

- `UnitRole.Capturador`;
- nome ou ID hard-coded da skill;
- `AI Sensor Priority`.

`SkillData` agora possui a capacidade `Can Capture Constructions`. A skill `Captura Construções` está marcada com essa capacidade e é compartilhada por Soldado, Bazooka e Metranca.

`PodeCapturarSensor` é a fonte de verdade:

- verifica as skills declaradas em `Training > Skills`;
- autoriza captura ou recuperação quando uma delas possui a capacidade;
- continua usando o papel apenas para comportamento e eficiência, como a penalidade do capturador agressivo.

O fallback `PrudentFow` da IA também consulta essa mesma capacidade.

## Eliminação do AI Sensor Priority

O último leitor runtime de `aiSensorPriority` foi removido.

O campo:

- não aparece mais no inspector de `UnitData`;
- não controla captura, ataque ou reposicionamento;
- permanece temporariamente oculto apenas para desserializar assets antigos sem provocar uma migração destrutiva em massa.

Após uma migração dedicada dos assets, a declaração e o enum legado poderão ser apagados definitivamente.

## Relatório de início do turno

O relatório automático do jogador também recebeu ajustes de apresentação:

- mover o mouse não fecha mais a tela;
- o temporizador e a barra pausam enquanto o ponteiro está sobre o painel;
- clicar nos controles internos não fecha o relatório;
- o rodapé possui ação explícita de cancelar;
- o fechamento manual restaura corretamente o estado anterior quando a tela foi aberta pelo menu.

## Contrato transacional

O envelope é uma consulta pura.

Ele não altera:

- posição ou ocupação;
- Fog of War;
- detecção ou inteligência;
- combustível, munição, HP ou estoque;
- captura;
- `HasActed`;
- revisões confirmadas do tabuleiro.

Portanto pode ser usado durante seleção, preview e simulação sem transformar uma decisão provisória em estado definitivo.

## Validação

Foram executadas as compilações:

- `Assembly-CSharp.csproj`: 0 erros;
- `Assembly-CSharp-Editor.csproj`: 0 erros.

Os avisos remanescentes são avisos já existentes de APIs obsoletas e serialização, sem erro introduzido por este ponto.

## Resultado

A Hotzone deixou de ser apenas uma área de ameaça e tornou-se a representação compartilhada de alcance mais intenção.

O jogador, as ferramentas e a IA podem agora responder à mesma pergunta usando as mesmas fontes:

- onde a unidade consegue chegar;
- qual ação consegue materializar;
- quanto custa entrar;
- em qual banda o objetivo está;
- e qual direção seguir quando o destino ainda é Strategic.

Isso reduz drift, elimina cálculos duplicados e prepara a inclusão de novas intenções sem criar um sistema paralelo para cada uma.
