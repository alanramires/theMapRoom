# v5.0.4b — Plano de Otimização do Tabuleiro, Melhor X e Jogabilidade 4a/8

## Visão geral

Esta versão complementa a Parte 4a com correções encontradas durante a validação
do comportamento de capturadores e transportadores da IA rebelde.

O cenário que revelou a regressão continha:

- um Soldado recém-desembarcado;
- uma construção capturável a dois pontos de movimento;
- um helicóptero ocupando a mesma coordenada da construção, mas em outra camada;
- um transporte disponível para reembarque.

Embora o jogador humano pudesse mover o Soldado até o prédio e capturá-lo, a IA
primeiro solicitava nova carona e, depois das correções iniciais, ainda parava no
hex adjacente. O problema não estava na regra do jogo: estava em verificações
paralelas da IA que tratavam coordenada como ocupação e reproduziam apenas parte
do sensor oficial de captura.

## Causas encontradas

Quatro decisões diferentes confundiam presença na coordenada com bloqueio real:

1. `QueroCaronaService` considerava qualquer aliado sobre o prédio como dono do
   objetivo, mesmo quando era uma aeronave sobre uma infantaria terrestre;
2. o seletor de objetivos rebeldes montava uma lista de células aliadas sem
   distinguir `Air`, `Sub` e `Blocking`;
3. a aproximação rebelde descartava destinos presentes em `BuildOccupied` ou
   `plannedDestinations` apenas pela coordenada;
4. `SimulateCaptureSensor` não consultava `PodeCapturarSensor`: ele mantinha uma
   cópia incompleta das regras de elegibilidade.

Essa combinação permitia uma contradição nos logs: `QueroCarona` identificava o
prédio próximo e recusava transporte, mas o planejador selecionava outro prédio
ou encerrava o movimento ao lado do alvo válido.

## Ocupação por camada

`UnitOccupancyRules` agora oferece uma consulta central para saber se uma unidade
realmente possui um bloqueador na célula pretendida.

A consulta delega a decisão final a `OccupancyResolver.CanEndMove`, preservando:

- aeronaves sobre unidades terrestres;
- submarinos sob unidades de superfície;
- separação entre convés e água em pontes compatíveis;
- proibição de empilhamento aliado na mesma banda;
- convivência inimiga autorizada pelas regras de Total War;
- comportamento legado quando o resolvedor por camada estiver desativado.

A IA não cria uma regra adicional de ocupação. Se o resolvedor oficial permite
terminar no hex, a célula permanece candidata.

## Quero Carona

Um aliado só reivindica uma construção para fins de recusa de carona quando
realmente disputa a camada operacional do passageiro.

Assim:

- um Soldado terrestre reconhece a construção sob um Apache como disponível;
- outro aliado terrestre sobre a construção continua impedindo duplicação de
  capturadores;
- pontes, água e bandas especiais continuam seguindo o resolvedor oficial;
- o passageiro recusa carona quando alcança um prédio capturável dentro do
  envelope previsto.

## Captura rebelde

O fluxo exclusivo da facção sem QG foi alinhado às mesmas regras:

- a seleção do prédio mais próximo não considera uma aeronave aliada como
  capturador terrestre já posicionado;
- a célula de aproximação consulta ocupação por camada;
- uma reserva anterior em `plannedDestinations` só bloqueia o Soldado quando
  existe ocupação aliada incompatível na camada dele;
- o objetivo Tactical reconhecido pelo fluxo de carona continua sendo um
  candidato material de captura;
- o batch de captura é produzido quando o sensor oficial confirma o destino.

As reservas continuam provisórias e limitadas à passada de decisão da Fase 2.
Elas não alteram a ocupação confirmada do tabuleiro.

## Pode Capturar como fonte de verdade

Foi removida a implementação paralela de elegibilidade que existia dentro de
`SimulateCaptureSensor`.

`PodeCapturarSensor` recebeu `TryGetCaptureTargetAtCell`, uma consulta que avalia
uma célula projetada sem mudar a posição da unidade. A API usa exatamente as
mesmas regras do fluxo humano:

- papel `Capturador` ou compatível;
- unidade embarcada ou neutra;
- modo `MoveuParado` ou `MoveuAndando`;
- visibilidade e exploração permitidas;
- existência e elegibilidade da construção;
- captura inimiga ou recuperação aliada;
- propriedade por slot e relações de aliança.

O wrapper da IA agora apenas escolhe o modo de movimento projetado e delega ao
sensor. Todos os pontos que já chamavam `SimulateCaptureSensor` passam, portanto,
pela mesma fonte de verdade sem precisar ser reescritos individualmente.

A responsabilidade fica separada:

- `PodeCapturarSensor` decide se a captura é legal;
- `OccupancyResolver` decide se a unidade pode terminar no hex;
- a IA classifica objetivos e escolhe entre opções legais;
- o batch materializa a ação;
- o compromisso definitivo continua no fluxo transacional do turno.

## Range map e privacidade da IA

O range map do jogador não foi reativado durante o turno automático.

Ocultar essa apresentação é intencional: exibir alcance, caminhos ou destinos
da IA poderia revelar posição, mobilidade e intenção sob Fog of War. A correção
de captura não depende de pintar o range.

Uma alteração experimental no pathfinder compartilhado foi retirada antes do
fechamento desta versão. Ao final:

- `UnitMovementPathRules` permanece sem alterações em relação a `v5.0.4a`;
- `TurnStateManager.Range` permanece sem alterações;
- a IA calcula seus caminhos internamente;
- nenhuma célula de alcance da IA é publicada na apresentação humana.

## Contrato transacional

A nova consulta projetada do sensor é somente leitura:

- não move a unidade;
- não altera `CurrentCellPosition`;
- não publica ocupação;
- não revela FOW;
- não grava exploração ou inteligência;
- não consome movimento, combustível ou recursos;
- não altera captura, HP ou `HasActed`;
- não sobrevive como verdade confirmada após cancelamento.

Somente o batch confirmado executa a captura. FOW, caches, HUD e estado do
tabuleiro continuam sendo atualizados depois do compromisso e do retorno a
`CursorState.Neutral`.

## Arquivos principais

- `Assets/Scripts/Units/Rules/UnitOccupancyRules.cs`;
- `Assets/Scripts/Sensors/PodeCapturarSensor.cs`;
- `Assets/Scripts/Match/AI/Services/QueroCaronaService.cs`;
- `Assets/Scripts/Match/AI/AIController.Rebel.cs`;
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.cs`;
- `Assets/Scripts/Match/AI/Units/Capturer/AIController.Capturer.Helpers.cs`.

## Validação técnica

- `Assembly-CSharp.csproj`: compilação incremental concluída com 0 erros e 0
  avisos;
- a reconstrução completa anterior concluiu com 0 erros e 258 avisos
  preexistentes de APIs obsoletas e serialização;
- `git diff --check` aprovado;
- pathfinder compartilhado sem alterações;
- range map compartilhado sem alterações;
- consulta projetada não altera estado confirmado;
- humano e IA usam `PodeCapturarSensor` como fonte de verdade;
- ocupação de destino usa `OccupancyResolver`.

## Resultado esperado

No cenário de validação, o Soldado rebelde deve:

1. reconhecer a construção próxima;
2. recusar nova carona;
3. ignorar o helicóptero que ocupa outra camada;
4. escolher o próprio hex da construção;
5. receber confirmação de `PodeCapturarSensor`;
6. produzir e executar o batch de captura.

O comportamento esperado nos logs deixa de ser uma marcha até o hex adjacente e
passa a registrar movimento até a construção seguido da captura.
