# v10.6.0 — Só aceita o que a etapa seguinte aceitaria

O fio do dia foi um defeito só, que apareceu em seis lugares: **uma etapa da IA aceitava uma
promessa que a etapa seguinte recusaria.** A âncora de fusão aceitava um parceiro onde a fusão
não acontece. O supridor era escolhido pelo hex atual, mas o pouso seria na célula futura. O
hospital era aceito sem que caminho nenhum chegasse mais perto dele. Quem cedia no swap "via" a
captura reservada pelo substituto. E o supridor somava necessidades sem perguntar o que se perde.
Em todos os casos o conserto foi o mesmo: **fazer a primeira etapa perguntar ao dono da
segunda**, em vez de escrever uma regra parecida ao lado.

Todas as frentes são trabalho do Codex, revisadas antes do commit, e todas vieram das marcas T10
do slot 0.

## 1. Reserva de pouso para a aeronave ferida (`2769674`)

A aeronave ferida que volta à produtora paira e espera o Serviço do Comando pousá-la. Se alguém
parasse embaixo dela, ou se a compra fizesse nascer uma unidade ali, ela não teria onde pousar.

O autor decidiu que isso é **política da IA, não regra do jogo**. O Codex tinha posto a reserva no
`OccupancyResolver` e na compra do `TurnStateManager`, o que valeria também para o humano. Na
revisão a reserva foi movida para a camada da IA: `IsAircraftReservingRepairLanding` é derivada e
some sozinha quando deixa de valer; entra no `BuildOccupied` no mesmo ponto da conscrição, no
`CanAIUnitEndMoveAtCell` e no `BuildProductionOccupiedCells` da compra; e o `Vacate` tira quem já
está embaixo. O humano continua movendo, pousando e comprando como antes.

## 2. EVAC sem avanço cai no anel do HQ (`6da7738`)

Na marca, o Chinook #56, com um Soldado de 2 HP, ficou parado vários turnos em (3,4) apontando para
um hospital que nenhum caminho seguro aproximava, e o fallback do HQ nunca disparava. Agora a LZ
hospitalar estimada só vale se algum destino seguro chega mais perto dela. Se nenhum chega, o
hospital é descartado e a busca segue; se todos são descartados, entra o anel do HQ com os
degraus que já existiam.

## 3. A fusão só vira recuperação onde a fusão aceitaria (`efb0519`)

O Apache #65, com 2 HP, voava para fundir com o #35, que estava cercado. A âncora "fusão" conferia
tipo, aliança e HP, mas a execução da fusão recusa célula sob ameaça e, na invasão, fora da
retaguarda. A recuperação tática voltava antes da busca de hospital, então o #65 nunca recuava.
Agora existe uma pergunta só, `IsRepairFusionSiteSafe`, usada pela execução e pela âncora.

## 4. O supridor é validado na célula futura (`d81ebbc`)

O Chinook #2 foi mandado para a floresta ao lado do Suprimentos #67, onde não pousa. O
`CanSupplierRecoverAircraft` consultava o hex atual. Agora ele recebe a célula: confere o
`PodePousarSensor.Evaluate(atCell:)` naquela célula, guarda as células de serviço válidas de cada
supridor e compara todas. O bônus de 5000 do supridor saiu: supridor e construção competem em pé
de igualdade, e o supridor só vence um empate exato.

## 5. Quem cede no swap captura outro prédio (`1c7a0b8`)

O Soldado #58, ao ceder a cidade para o #15, só sabia atirar ou andar. Agora segue "antes do tiro
vem o dinheiro": tenta uma captura oportunista fora do prédio cedido, depois combate útil, depois
movimento.

**Achado do Codex:** a autoridade de reservas via o substituto como candidato ao *segundo* prédio
também. O #15, mais forte, "reservava" o outro, o #58 cedia de novo, e ninguém capturava. O
substituto agora atravessa a autoridade de reservas que já existia como `committedCapturer`.

## 6. A régua do supridor: o que se perde se não atender agora (`3257843`, `64a846d`)

O supridor atendia o Soldado #1 (2 HP) em vez do Chinook #2 (13/60 de combustível). A régua somava
o HP faltante com peso alto e ignorava o combustível no ramo de reparo. A urgência de combustível
existia só na aproximação.

A doutrina é do autor: *"o que eu perco se não atender agora?"*. São faixas estritas, numa função
só (`ResolveLogisticsNeed`) que a aproximação e o atendimento consomem:

```text
1. AircraftAutonomy   aeronave que cai no próximo upkeep
2. CriticalHp         HP <= 50%
3. Corrective         HP, combustível e munição proporcionais
4. Preventive
```

Cada faixa vale 1.000.000 e o score interno tem teto, então nenhum bônus atravessa de uma faixa
para outra.

**O prazo** (`64a846d`, comitado fora da minha sessão e revisado no fechamento) entra dentro da
faixa, como um degrau de 300k: quem é atendível nesta rodada vence quem só é atendível no
Operacional. Um preventivo próximo nunca ultrapassa um corretivo distante. O prazo vem do envelope
Operacional e do `PodeSuprir` por origem, que já existiam; não há busca de caminho por alvo, nem
distância usada como prazo.

A exceção: uma aeronave da faixa 1 que só é atendível no Operacional cai antes do atendimento, então
sai da disputa do supridor e fica com o pouso de emergência. O planejamento do transporte também
deixou de classificar o suprimento pela distância de movimento. **Visto em Play pelo autor:** na
marca T10, a AAA crítica foi mantida no Operacional, e o Chinook com 13 de autonomia buscou a
produtora para a recuperação.

## 7. Skill pergunte-me (`31c1bac`)

É uma entrevista em rodadas para fechar uma doutrina antes de mandar o bloco ao Codex: no máximo 4
perguntas, "sim" aceita a recomendação, os fatos são do agente e a decisão é do autor. Foi
adaptada de `grilling` (Matt Pocock, MIT). A cópia da Mylarhis existe, mas não foi comitada, por
escolha do autor.

## O que não terminou

- **Só a régua do supridor foi vista em Play.** A reserva de pouso, o EVAC sem avanço, a fusão
  segura, a célula futura e o swap passaram nos testes fora do Play.
- **`AircraftRepairRecovery` é avaliada duas vezes na mesma decisão.** Isso ficou de fora do
  `efb0519` de propósito.
- Carregados da v10.5.0: o Serviço do Comando roda de novo depois do load (suspeita: a ordem entre
  o `resumeStage` e a restauração do `aiRuntimeStage`); o volume do `AudioManager` muda de 0,49
  para 0,70, talvez por estado de runtime; os casos do anel e do EVAC conjunto ainda não foram
  vistos em Play.
- O backup da v10.5.0 não rodou (a Unity estava em Play); o deste fechamento cobre.

## Docs de doutrina

Desatualizados
- `docs/AI Behavior/Logistica.md`: **nenhum doc descreve a régua do atendimento**. Procurei por
  prioridade, urgência e combustível. Faltam as 4 faixas estritas, o prazo Tático antes do
  Operacional dentro da faixa, a aeronave da faixa 1 que "cai antes" e a recuperação aérea na
  célula futura, sem o bônus do supridor (**régua e prazo vistos em Play**; a célula futura só em
  teste). Carregado: EVAC e hospital do supridor (só compilou).
- `docs/AI Behavior/Transporte.md`: EVAC sem avanço → anel do HQ (só em teste). Carregado da
  v10.5.0: hospital pela regra do reparo, par prédio + LZ, anel em degraus, EVAC conjunto, e a
  lista da v10.4.0.
- `docs/AI Behavior/Capturador.md`: quem cede no swap captura antes de atirar; o substituto não
  reserva o segundo prédio (só em teste).
- Reparo: a reserva de pouso da aeronave ferida e a fusão só onde a fusão aceitaria. Não há doc de
  reparo próprio; lugar natural: `contrato_questionario.md` ou `Logistica.md` (só em teste).
- Carregados sem mudança: `contrato_questionario.md` (reparo antes do Serviço do Comando);
  Disruptor sem doc (lugar natural: `Assalto.md` ou `Papeis.md`); `Shopping.md` §6;
  `Capturador.md` (C6, espera fora da produtora, herdeiro do blitz); `Papeis.md`,
  `ficha_do_papel.md`, `Assalto.md`, `Vigilancia.md`, `FireSupport.md`, `magnetic_tabela.md`.

Prontos para consolidar
- Nenhum. O Disruptor está estável há um fechamento só (precisa de 2). EVAC, reparo e logística
  mudaram hoje.
