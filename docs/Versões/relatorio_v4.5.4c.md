# v4.5.4c — Ponto de Verificação FOW, AI, speed gameplay, sprite-eco

## Visão geral

Este ponto de verificação parte do estado confirmado em `v5.0.4b` e reúne
somente os comportamentos reaplicados e validados durante a partida de teste
com grande quantidade de unidades.

O objetivo é preservar a velocidade conquistada sem alterar as regras
consolidadas do tabuleiro, especialmente Fog of War, ocupação por camada,
embarque e compromisso transacional das ações.

## FOW e sprite-eco

Uma unidade detectada numa camada especializada pode estar confirmada para o
observador mesmo quando o terreno sob ela continua coberto pela nevoa.

Nessa situação, a apresentação agora cria um sprite-eco:

- usa o sprite e a cor do time da unidade detectada;
- aparece acima da camada opaca de FOW;
- também permanece legível sobre terreno conhecido ou explorado;
- não apresenta HUD, HP, munição ou outras informações;
- não altera detecção, memória, ocupação ou visibilidade lógica;
- é removido quando o contato deixa de ser válido, morre ou embarca.

O eco é exclusivamente visual. A fonte da verdade continua sendo o snapshot
confirmado de visibilidade do slot observador.

Nenhum comando `FOW PARTIAL` é necessário para o funcionamento normal. A
perspectiva e a camada do turno da IA permanecem com o comportamento de
`v5.0.4b`.

## Play Conservative e retaguarda aliada

`Play Conservative`, em `UnitData > AI Behavior`, passa a definir também um
fallback de formação.

Quando não existe combate, reparo, transporte, serviço ou outra tarefa
prioritária, a unidade:

- identifica a linha formada por capturadores e unidades de assalto aliadas;
- procura uma faixa segura aproximadamente dois hexes atrás da frente;
- evita ocupar a vanguarda;
- considera ameaça, coesão, custo do caminho e ocupação por camada;
- permanece parada quando já está bem posicionada;
- evita oscilar entre células equivalentes.

Esse comportamento permite que plataformas conservadoras, como o Porta-Aviões,
acompanhem a retaguarda da esquadra em vez de permanecerem isoladas perto da
produção aguardando uma solicitação.

## Estoque Strategic

Unidades cujo papel primário é `Estoque`, como o Caminhão 18W, agora seguem o
envelope:

1. procuram demandas no Tactical;
2. procuram no Operational;
3. quando não encontram, escolhem uma direção Strategic por distância cúbica;
4. materializam somente uma célula alcançável pelos caminhos válidos da rodada;
5. reavaliam demanda e direção no turno seguinte.

O Strategic não constrói uma rota global nem visita o mapa inteiro. Ele fornece
direção barata; o movimento real continua limitado ao range e aos caminhos
válidos já calculados para a unidade.

Transportadores híbridos não recebem automaticamente essa perseguição
estratégica: o estágio é habilitado apenas para o papel primário `Estoque`.

## Batch da IA e erro de movimento

Foi restaurada a proteção transacional do batch automático.

Um movimento válido pode permanecer brevemente em `UnitSelected` enquanto a
animação começa. A execução não usa mais esse estado transitório como sinal de
falha: consulta o retorno real de `HandleConfirm`.

- `Confirm`: aguarda o movimento e continua o batch normalmente;
- `Error`: cancela o batch sem compromisso, limpa a seleção e libera a IA;
- exceção durante a decisão: registra o erro e produz `Mover Parado`;
- batch abortado não é registrado como jogada confirmada;
- destino ocupado é revalidado antes da execução;
- quando possível, a unidade cede a vez para o aliado bloqueador agir primeiro;
- sem liberação possível, a unidade executa `Mover Parado`.

Isso impede que Trem de Carga, transportadores ou outras unidades deixem a IA
presa em `UnitSelected` ao receber uma ordem impossível.

## Pode Embarcar e precedência do hex

Foi restaurado o contrato consolidado do `PodeEmbarcarSensor`:

`construção > estrutura+terreno > terreno`

Quando existe uma construção no hex, somente o filtro
`Allowed Embark When Transporter At` de construção decide o contexto.
`Any Construction` autoriza o embarque sem exigir também um par
estrutura+terreno.

Estrutura e terreno continuam sendo avaliados somente quando não existe uma
construção com precedência. Movimento do trem permanece separado: sua
circulação é decidida pelos tiles válidos, enquanto embarque e desembarque são
definidos pelo `UnitData`.

## Desempenho e jogabilidade

As decisões Strategic reaplicadas usam distância cúbica como direção e
materializam apenas o movimento tático disponível. O fallback conservador
reutiliza os caminhos válidos da rodada e a geometria compartilhada de
retaguarda.

O ponto de verificação não reintroduz:

- varredura global de caminhos para cada destino Strategic;
- reconstrução do mapa por unidade;
- publicação de range map da IA para o jogador;
- alteração automática de `FOW PARTIAL`;
- mudanças em cena, prefab ou painel de passagem de turno.

## Contrato transacional

As consultas de direção, formação, ocupação, FOW e embarque são somente
planejamento ou apresentação:

- não movem definitivamente a unidade;
- não consomem movimento, combustível ou recursos;
- não alteram `HasActed`;
- não publicam ocupação confirmada;
- não gravam detecção ou memória da IA;
- não revelam células;
- não sobrevivem a cancelamento como verdade do tabuleiro.

O batch só é registrado depois de concluir com sucesso. Em caso de `Error`, o
fluxo cancela e retorna ao estado seguro sem compromisso.

## Arquivos principais

- `Assets/Scripts/Match/AI/1. Phases/AIController.Phase2.cs`;
- `Assets/Scripts/Match/AI/AIController.Debug.cs`;
- `Assets/Scripts/Match/AI/AIController.Router.cs`;
- `Assets/Scripts/Match/AI/Services/AIController.Backline.cs`;
- `Assets/Scripts/Match/AI/Units/Logistics/AIController.Logistics.cs`;
- `Assets/Scripts/Match/AI/Units/Stock/AIController.Stock.cs`;
- `Assets/Scripts/Match/AI/Units/Transport/AIController.TransportOperations.cs`;
- `Assets/Scripts/Match/MatchController.cs`;
- `Assets/Scripts/Replay/ReplayManager.cs`;
- `Assets/Scripts/Sensors/PodeEmbarcarSensor.cs`;
- `Assets/Scripts/Units/UnitData.cs`;
- `Assets/Scripts/Units/UnitManager.cs`.

## Verificação

- base restaurada em `v5.0.4b`;
- `Play Conservative` e proteção do batch comparados com o stash de segurança
  que preservou a implementação anterior;
- `Assembly-CSharp.csproj` compilado com 0 erros;
- compilação incremental final de `Assembly-CSharp.csproj` com 0 avisos;
- `Assembly-CSharp-Editor.csproj` compilado com 0 erros e 159 avisos
  preexistentes de APIs obsoletas;
- a reconstrução completa anterior registrou 258 avisos preexistentes no
  projeto runtime;
- `git diff --check` aprovado;
- nenhuma alteração de cena, prefab ou asset foi incluída neste ponto de
  verificação.
