# v4.0.26 - AI HotZone como Serviço de Otimização na tomada de decisão + AI Hard com Recrutamento Forçado

Esta versão transforma a HotZone em infraestrutura compartilhada entre interface e IA, eliminando reconstruções redundantes durante decisões de movimento e ataque. Também amplia a resposta do Hard Mode quando a IA está perdendo, projetando a próxima onda inimiga e ocupando os produtores disponíveis com massa antes de reservar o último produtor para um elite viável.

## HotZone como serviço público

- O cálculo antes restrito à inspeção visual foi extraído para `UnitThreatEnvelopeService`.
- O serviço expõe dois contextos:
  - `Potential`, usando o movimento máximo para a inspeção de ameaça pelo jogador;
  - `CurrentTurn`, usando o movimento restante para decisões executáveis da IA.
- O envelope compartilhado fornece:
  - caminhos por destino;
  - células de movimento;
  - HotZone completa em `AttackableCells`;
  - células usadas pelas camadas visuais de movimento e linha de fogo.
- A chave de cache considera posição, movimento, combustível, domínio, altura, estado da aeronave, armas, munição, trajetória e revisões do tabuleiro/observadores/regras.
- O overlay visual continua usando as duas camadas sem sobreposição, mas ambas representam uma única HotZone semântica.
- O cache público é invalidado junto com o cache da inspeção existente.

## HotZone na tomada de decisão da IA

- A IA obtém o envelope `CurrentTurn` de cada unidade sob demanda.
- Inimigos fora de `AttackableCells` são descartados imediatamente por consulta em `HashSet`, sem simulação de ataque por hex.
- Para alvos dentro da HotZone, `PodeMirarSensor.CollectTargets` é calculado no máximo uma vez por origem durante a decisão.
- O resultado por origem é reutilizado por perseguidor, explorador e scoring final.
- O fluxo anterior multiplicava inimigos por hexes alcançáveis e reconstruía a lista completa de alvos para cada combinação.
- A geração do envelope é lazy: unidades que não consultam possibilidades de ataque não pagam pelo cálculo.

## Resultado medido

Teste reproduzido no turno 8 com o mesmo estado de partida:

- Verde:
  - CPU de decisão: `95,3 s -> 28,1 s` (`-70%`);
  - turno total: `137 s -> 68 s`.
- Vermelho:
  - CPU de decisão: `135,7 s -> 30,4 s` (`-78%`);
  - turno total: `198 s -> 88 s`.
- As decisões individuais que antes bloqueavam a thread principal por `20-27 s` caíram para aproximadamente `2,5-5,4 s` nos casos mais pesados medidos.
- A construção da HotZone atual custou aproximadamente `90-204 ms` por unidade nos casos instrumentados.

## Instrumentação de desempenho

- A Fase 2 passou a separar por unidade:
  - decisão;
  - execução/animação;
  - reconstrução de snapshot;
  - delay entre batches.
- O resumo final informa totais e as cinco ações mais lentas.
- Escopos internos medem caminhos válidos, distância de rota, progressão de dois turnos, perseguidor, explorador, oportunidades e construção do envelope.
- A instrumentação permitiu demonstrar que `BuildLight` não era o gargalo e localizar as recomputações de ataque.

## AI Hard: projeção de força inimiga

- No Hard Mode, cada produtor inimigo é contado como uma unidade projetada para a próxima onda de produção.
- A postura macro compara a força atual da IA contra inimigos conhecidos mais produtores projetados.
- Normal e Easy continuam usando apenas a fotografia atual da força conhecida.
- O Shopping Pressure diferencia força conhecida e projeção adicional do Hard.

## AI Hard: recrutamento forçado

- Quando o Hard Mode está em estado macro `Perdendo`, o shopping entra em `RECRUTAMENTO FORÇADO`.
- Cada produtor livre recebe primeiro o corpo terrestre permitido mais barato, priorizando massa imediata.
- Se houver elite comprometido ou elite atendendo uma demanda pendente, seu produtor é reservado para o final.
- O elite só é comprado se ainda couber no caixa depois de preencher os outros produtores com massa.
- Quando o elite não cabe, o último produtor também recebe massa e o compromisso estratégico é preservado para outro turno.
- Unidades navais e opções proibidas pela postura/Hard continuam excluídas.

## Produção livre sob pressão

- Infantaria não-elite em reparo deixa de ocupar HQ, base ou âncora quando a base está sob pressão.
- A pressão é ativada pelo estado macro `Perdendo` ou por inimigo visível próximo ao cluster da base.
- Essas unidades procuram outras construções de reparo ou posições seguras ao redor do HQ.
- Elites mantêm acesso às construções centrais.
- O objetivo é preservar células de produção para o recrutamento em massa.

## HUD e diagnóstico

- Shopping Pressure mostra quando o recrutamento forçado está ativo.
- A força projetada do Hard aparece separada da força inimiga conhecida.
- Logs de compra identificam massa, elite no último produtor e orçamento restante.

## Validação

- `Assembly-CSharp.csproj`: build concluído com `0` erros.
- Comparação de desempenho realizada no mesmo turno e estado de partida antes e depois da integração.
