# Desembarque parcial e novo Hotzone tool

## Versão

`v6.0.2`

## Objetivo

Este ponto de verificação consolida o refinamento do planejamento de desembarque da IA e a unificação visual das zonas Tactical, Operational e Strategic.

O foco foi impedir que transportadores mantenham passageiros desnecessariamente embarcados, reconhecer aproximações válidas impostas pela geografia e tornar a política de Hotzone observável diretamente no Editor.

## Desembarque parcial

A antiga política de LZ conjunta podia bloquear uma entrega válida quando apenas parte da carga já possuía uma rota útil até seu próprio objetivo.

O comportamento foi alterado:

- a LZ conjunta continua preferida quando consegue entregar mais passageiros na rodada;
- ela não possui mais poder de veto sobre uma entrega Tactical válida;
- cada passageiro conserva sua própria missão e seu próprio destino;
- passageiros que já podem cumprir a missão desembarcam imediatamente;
- os demais permanecem embarcados para entregas posteriores;
- o batch de desembarque continua transacional, aplicando movimento e desembarque somente no compromisso da ação.

Esse comportamento foi validado em jogo com dois passageiros: um deles desembarcou enquanto o outro permaneceu a bordo para continuar sua viagem.

## LZ Tactical e Operational por passageiro

O transporte aéreo ainda utilizava o valor fixo `AirDropOffRange = 2` para limitar a perna terrestre da entrega. Esse valor podia esconder LZs legítimas, especialmente quando o objetivo estava protegido por montanhas ou outra geografia intransitável.

Agora o limite terrestre é individual:

- Tactical: movimento base do passageiro;
- Operational: movimento base do passageiro multiplicado por dois;
- caminhos válidos e custos reais de terreno continuam sendo a fonte de verdade;
- passageiros com mobilidades diferentes são avaliados com seus próprios limites.

Quando não existe uma entrega Tactical, a ferramenta procura uma LZ Operational válida. Se essa LZ ainda estiver distante, o transportador progride em sua direção. Se já estiver nos caminhos válidos da rodada, o batch executa movimento e desembarque na mesma ação.

Foi validado o caso de uma construção sobre montanha:

- a IA deixou de perseguir cegamente o hex da construção;
- encontrou uma LZ Operational no sopé;
- o Chinook alcançou a LZ e desembarcou o passageiro;
- o passageiro passou a concluir o trajeto por terra.

## Cálculo compartilhado do Melhor Desembarque

O cálculo não reconstrói um caminho para cada LZ testada.

Para cada passageiro e objetivo:

1. constrói um mapa reverso de custos terrestres;
2. compartilha esse mapa entre todas as LZs candidatas;
3. consulta os hexes reais oferecidos pelo `PodeDesembarcar`;
4. combina passageiros e spots exclusivos;
5. classifica as LZs por quantidade entregue, prioridade, rota restante e custo do transportador.

Assim, centenas de LZs podem ser consultadas usando apenas um mapa terrestre por passageiro/objetivo, sujeito ao cache confirmado do tabuleiro.

## Hotzone unificada

Foi adicionada a ferramenta:

`Tools > Utils > Hotzone`

Ela permite visualizar as três modalidades usadas pela IA:

- Tactical;
- Operational;
- Strategic.

A política centralizada considera:

- unidades aeronáuticas por distância cúbica;
- unidades não aeronáuticas pelos caminhos válidos e custos reais de movimento;
- Tactical baseado no movimento da unidade e, quando aplicável, no alcance ofensivo;
- Operational baseado em duas rodadas de movimento;
- Strategic como direção de longo alcance, sem reconstruir desnecessariamente caminhos completos.

A ferramenta serve para comparar a interpretação do Editor com a decisão runtime da IA e facilita a investigação de alvos que aparecem fora do setor esperado.

## Integrações relacionadas

O serviço compartilhado de Hotzone passou a apoiar decisões de:

- assalto e pressão operacional;
- fogo de suporte e antiaéreo;
- captura e reservas de construção;
- transporte, embarque e desembarque;
- estoque e logística;
- progressão de longo alcance.

O planejamento runtime também preserva destinos designados dos passageiros para que o transportador execute a intenção da carga, em vez de inventar um destino próprio.

## Validação

- desembarque parcial confirmado em jogo;
- desembarque Operational no sopé de montanha confirmado em jogo;
- carga restante preservada após entrega parcial;
- LZ alcançável na rodada convertida em `mover + desembarcar`;
- compilação de `Assembly-CSharp.csproj` concluída sem erros;
- avisos restantes pertencem a APIs obsoletas e analisadores já presentes no projeto.

## Resultado

O transportador passou a atuar como multiplicador das intenções dos passageiros:

- entrega quem já pode cumprir a missão;
- conserva quem ainda precisa viajar;
- respeita a geografia;
- utiliza a melhor aproximação comprovada;
- evita órbitas e esperas causadas pela antiga exigência de LZ conjunta.

