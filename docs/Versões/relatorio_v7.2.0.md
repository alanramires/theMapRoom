# v7.2.0 — Apagar também é publicar

A `v7.1.2` fez as três verdades traçarem a **mesma reta**. Esta versão fecha a
saída dela — as janelas passam a descrever essa reta com as mesmas palavras — e,
no caminho, esbarra num buraco que não tinha nada a ver com percepção: a barreira
que protege o tabuleiro confirmado só cobria **metade** do que ela devia cobrir.

O fio do dia é esse. Duas frentes de acabamento e uma descoberta que vale mais
que as duas.

---

## 1. O relatório da linha virou um só

O pedido do autor foi direto, com print das duas janelas lado a lado e a frase
que importa: *"eu gostava daquele relatório, sabe"*.

O `PodeEnxergar` contava a viagem inteira — a subida passo a passo, a altura na
parada, o nome do bloqueador com o EV dele. O `PodeDetectar`, para a **mesma**
reta, imprimia `Altura por hex: 0 > 2` e `Bloqueio LOS: -23,11`, e o leitor
deduzia sozinho que 2,25 > 2.

A causa não era de formatação. Cada janela **remontava** o relatório a partir do
que sobrava do traçado: uma tinha quatro campos construídos para isso, a outra
recebeu na `v7.1.2` um `List<float>` cru. É a mesma lição de *uma pergunta, uma
implementação* — agora aplicada à **saída**.

Dois tipos novos, ambos em `Assets/Scripts/Sensors/`:

```text
ObservationLineProfile   a reta inteira num tipo so: origem, alvo, EVs, hexes
                         cruzados, altura ponto a ponto, bloqueador (com EV e
                         altura da linha ali) e o maior obstaculo que ela limpou
ObservationLineReport    transforma o perfil em linhas rotuladas. Nao calcula
                         nada: numero que nao esta no perfil nao aparece
```

O `ObservationLineService.TryTrace` ganhou um `profile` opcional que ele **anota
no mesmo laço** que decide a linha. Não há segunda passada, e com `profile ==
null` o comportamento é idêntico ao anterior — o parâmetro não muda decisão
nenhuma.

Três janelas passaram a imprimir o mesmo relatório: Pode Enxergar, Pode Detectar
e Alguém me vê. As duas primeiras eram o pedido; a terceira entrou porque
consome `PodeDetectarOption` e teria quebrado de qualquer jeito.

### O que morreu junto

`PodeDetectarSensor.TryTraceObservationLineDetailed` — 127 linhas que refaziam o
traçado numa segunda passada só para o relatório. Com ela foi embora a última
amarra entre as duas entidades: a **janela** do `PodeEnxergar` chamava o
`PodeDetectarSensor` para conseguir o detalhe da reta. Agora ela pergunta ao
`PodeEnxergarSensor.TryProfileVisionLine`.

O critério da `v7.1.2` era *"o arquivo do PodeEnxergar não menciona PodeDetectar"*.
Ele passou; a janela dele, não. Agora passa também.

### Um campo que ninguém lia

`PodeDetectarOption` perdeu cinco campos para ganhar um `lineProfile`. Entre os
cinco estava `lineOfSightIntermediateCells`: escrito e copiado **por candidato**
no caminho quente da detecção, e sem um único leitor em todo o projeto.

### Uma decisão de conteúdo, e o motivo

O campo "Passou por" mostrava o hex de maior EV que a linha cruzou, bloqueasse
ele a linha ou não. Passou a exigir `blockLoS`: anunciar folga sobre algo que
nunca foi obstáculo sugere uma margem que não existiu. É o **mesmo predicado**
que a decisão de bloqueio usa — que era exatamente o ponto do exercício.

E o autor pegou uma segunda, olhando o relatório real:

> *"subiu em 0 > 1,75 e parou no EV: 2,5 — não pode continuar subindo a linha, né?"*

A subida já parava (o traçado retorna antes de somar o EV do alvo). O problema
era a linha de cima: `Viagem da linha: ascendente 0,00 -> 4,00` anunciava o
destino **pretendido** e, lida em sequência com a subida logo abaixo, parecia
dizer que a reta chegou a 4. O 4 precisa aparecer — é dele que sai a inclinação,
e sem ele o 1,75 naquele hex não se explica —, mas não pode se passar por
resultado. Agora fecha com `NAO CHEGOU (parou em 1,75)`.

---

## 2. O delta de "passou a detectar"

Era o item 1 da fila: **um gancho, dois consumidores**.

O gancho ficou no `PublishFogGameplaySnapshot` — o único lugar que já responde
*"este slot detecta esta unidade"*. Ele fotografa o conjunto anterior antes de
mexer nele e publica `gainedContacts`: só a transição, sem cálculo novo.

Duas escolhas que valem registro:

**Novidade é propriedade do tabuleiro confirmado, não do sensor.** Os dois
consumidores rodam *depois* do publish (`ApplyCommittedUnitFog`, linhas 5684 →
5697) e leem a mesma resposta. Se um fosse consultado antes, veria o estado velho
e discordaria do outro — que é o começo de toda divergência que este projeto já
pagou.

**O primeiro publish de um slot é linha de base, não notícia.** Começo de partida
e save recém-carregado não enchem o Jornal com o que o jogador já estava vendo.

Consumidores:

- **Som** — só o fallback generalizado ganhou o portão. O bloco de chave-do-alvo
  casada continua repetindo a cada reconstrução, porque no furtivo repetir é o
  feedback de *"continuo te vendo"* e o autor o quis assim. O fallback é o que
  pega qualquer contato, e o radar reencontra os mesmos inimigos toda vez que
  age — repetido, vira ruído de fundo e o jogador para de escutar.
- **Jornal** — consulta o mesmo gancho antes do dedupe por célula+turno. O portão
  `detectorSlotIndex != ActiveSlotId` **continua lá**: revisá-lo é o item 2 da
  fila, não desta versão.

Deliberadamente **não** usa `currentlyObservedByTeamIds`. Aquele conjunto é o
olho, e o olho só fala de unidade com etiqueta de ocultação — generalizá-lo
mudaria a semântica do indicador.

---

## 3. A descoberta: apagar também é publicar

Esta é a parte que vale mais que as duas frentes acima, e ela veio de um caso que
o autor reproduziu por acidente.

Aeronave no meio do voo provisório — **não** em `Neutral`, no passo antes de
escolher "Apenas Mover". Ele abriu o debug, digitou `fow off`, depois `fow on`, e
o tabuleiro ficou inteiro aberto. Desfazendo o movimento, voltava ao normal.

A barreira de escrita (`IsFogVisualWriteAuthorized`) é **assimétrica**:

```text
APAGAR     ResetFogOfWarRuntime(clearTilemap: true) -> ClearAllTiles()     sem barreira
DESENHAR   RenderFogOverlayFromRuntimeCache / InitializeFogOverlay         com barreira
```

Fora de `Neutral`, a sequência produz o pior par possível: o apagamento **passa**
e a reposição é **recusada**. Ninguém repõe, e o tabuleiro fica aberto até o
próximo momento autorizado. Voltar a `Neutral` conserta — o que fazia o sintoma
parecer aleatório.

O CLAUDE.md diz *"nada provisório publica verdade confirmada"*. A barreira leu
isso pela metade: **apagar verdade confirmada também é publicar**.

E o buraco é maior que o comando de debug. `ResetFogOfWarRuntime(clearTilemap:
true)` tem **sete chamadores**, e há mais quatro `ClearAllTiles()` diretos no
arquivo. O `fow off` só foi o mais fácil de alcançar com o dedo.

### A barreira de apagar é mais estreita que a de desenhar

Esta parte quase virou uma regressão. A barreira completa exige
`fogCachedObserverSlotIndex == slot de apresentação`, o que faz todo o sentido
para desenhar — não pinte a névoa do slot A com o cache do slot B. Para apagar
não faz nenhum: **não existe "limpar a névoa do slot errado"**.

Pior: o próprio `ResetFogOfWarRuntime` zera `fogCachedObserverSlotIndex`. Usar a
barreira completa faria a segunda chamada em diante recusar **sempre**, e os
presets **sem** névoa ficariam com névoa velha pintada na tela.

Daí `IsFogVisualEraseAuthorized`, que guarda só a metade transacional
(`isNeutral` + contexto). Recusado, o overlay fica **velho em vez de vazio** — e
isso se cura sozinho, porque `fogRenderedVisibleCellsValid` vira `false` de
qualquer jeito e o próximo render autorizado repinta do zero.

### O comando diz não

`fow on|off|partial` passou a recusar fora de `Neutral`, com a razão na tela. A
correção real é a barreira; esta é para o autor não cair no caso em silêncio.
Palavras dele:

> *"eu não posso avacalhar o tabuleiro pra dar uma espiada sem que tudo esteja
> em neutral"*

---

## 4. Duas hipóteses erradas, e como elas morreram

Vale mais do que a correção, porque é o tipo de coisa que a próxima sessão repete.

Antes de o autor achar o caso real, eu tinha **duas** hipóteses e o autor tinha
uma terceira. Todas erradas:

| hipótese | quem | como morreu |
|---|---|---|
| memória de exploração contaminada durante o `fow off` | eu | o log mostrou o estado pós-ciclo **idêntico** ao do início do turno |
| `BuildFogDisplayVisibleCellsForAllModes` devolvendo demais | eu | `render=5,909ms` rodou e `geographic=63` de `boardCells=1768` — estava escondendo, não revelando |
| *"mudamos o contrato do enxergar e detectar"* | autor | o caminho do overlay não foi tocado em nenhum hunk da versão |

O que matou as três foi **um log de partida real**, não mais leitura de código. E
o que apontou o mecanismo certo foi um detalhe que nenhuma hipótese explicava:
*"quando eu desfaço o movimento, volta ao normal"*. Recompile é evento único e
irreversível; aquilo era uma correlação **reversível com o estado do cursor**, e
só a barreira produz isso.

Houve ainda um quarto suspeito, levantado pelo próprio autor: *"ou talvez você
arrumou enquanto eu testava"*. Legítimo — salvar `.cs` em Play recarrega o
domínio, e quase todo o estado da névoa é `[System.NonSerialized]`, inclusive
`debugFogOfWarEnabled = true`. Um recompile **religa a névoa sozinho**. Ficou o
protocolo: enquanto o autor testa névoa em Play, ninguém salva `.cs`.

---

## 5. Um instrumento que não existia

Nenhum log imprimia os dois números que decidem uma discussão de *"o tabuleiro
não voltou a esconder"*: o tamanho da memória permanente de exploração e quantos
tiles de névoa sobraram desenhados.

`LogFogDebugState` roda nos três comandos e imprime `[FoW][Estado][ON|OFF|PARTIAL]`
com `explorado`, `nevoaTiles`, `celulasTabuleiro`, `displayVisivel`, `bakes`,
`overlayInit`, `renderValido` e `observadorCache`.

`nevoaTiles` é a medida direta do que está na tela: `0` significa tabuleiro
inteiro aberto, independentemente do que o cálculo disse. No caso do autor teria
vindo `0` e apontado para o overlay vazio na primeira tentativa.

---

## 6. O que não terminou

**Nada desta versão rodou em Play.** Compilar não prova o relatório — a leitura
lado a lado das duas janelas é o teste, e o delta do som só aparece movendo um
radar duas vezes sobre os mesmos inimigos. A correção da barreira tem teste
descrito no resumo.

**`PodeDetectar` ainda responde por hexágono.** Levantado nesta sessão e **não**
corrigido: `PodeDetectarSensor.CollectVisibleCells` — o laço de hex com a tabela
de flags — continua público, com consumidores vivos:

```text
MatchController.AddFogLayerVisibleCellsForUnit   11 chamadas, forceVirtualTargetLayer
VisionCoverageService (2)                        um com preserveObserverLayerRange = TRUE
HexEnxergadoDebugWindow                          janela
```

O `preserveObserverLayerRangeForHexVisibility: true` é **uma das três portas que
a `v7.1.0` fechou**. Não alcança mais o FOW nem a memória permanente, mas
`MelhorVisaoService` e `AIController.Vigilancia` leem dali — a IA ainda enxerga
por uma regra que a revelação do jogo abandonou.

**Três coisas que a documentação afirmava e o código desmentiu**, verificadas
nesta sessão:

- `skipLosForCurrentTarget` **não existe** — zero ocorrências fora de docs. O
  item 4 da fila (*"decisão do autor: propriedade do meio ou método na ficha?"*)
  já foi decidido pela extração: o `blockLoS` do DPQ é consumido célula a célula
  por `TerrainVisionResolver` → `ObservationCellService` → `cellBlocksLoS`. Virou
  propriedade do meio, e não há `if` para tirar.
- `AddSpecializedAirKnowledge` tem **zero chamadas**. O gêmeo do bake foi
  desligado na `v7.1.0` e o corpo ficou. Idem `CollectVisibleAirCellsAt`.
- `rangeOnlyForAirHigh` na janela do Pode Enxergar é **sempre falso** — o único
  cenário que ela monta é Land/Surface com `forceLayer: false`.

**Perf continua sem número.** Adiado pelo autor: *"os testes da AIPerf foram
adiados, estamos trabalhando com a infra do jogo"*.

**O `fow off` não pausa nada.** A spec do autor pede *"sistema de cook, baking,
snapshots pausados"*. Hoje `RefreshFogOfWarForCurrentTeamInternal` checa
`SuppressFogOfWarRefresh` e `enableTotalWar` — **não** checa
`debugFogOfWarEnabled`. Com a névoa desligada o pipeline continua rodando a cada
compromisso, inclusive `RecordConfirmedExploredCells`.

---

## 7. O paradigma que nasceu no meio da conversa

Não virou código, e por isso está aqui: é o desenho que organiza o próximo
trabalho, e ele mudou duas vezes na mesma conversa.

O autor separou os consumidores de percepção:

```text
melhorVisao       de onde eu revelo AQUELA faixa de praia          moeda: hex
melhorDeteccao    de onde eu lanco a rede maior, sem revelar hex   moeda: contato
melhorSpotting    de onde eu vejo quem ocupa AQUELA cidade         moeda: contato
```

Primeiro veio a conclusão de que *"revelar hexágono não serve pra nada no serviço
de inteligência"*. Depois o próprio autor a revogou, com um caso concreto: Apache
e Chinook voando sobre o mar, o Chinook carregado publica a intenção *"quero
saber se aquela faixa de praia tem lugar pra pouso"*, o Apache assume, voa até a
névoa e revela com sua visão 3. **Revelar tem função** — porque não se desembarca
na névoa, e o Chinook indo na frente perderia o turno.

Três achados desse trecho:

**A moeda do `MelhorVisao` de hoje está errada.** Ele pontua `VisibleCount`, que
conta células de `VisionCoverageResult.VisibleCells`, que vêm do laço de **hex**
do `PodeDetectar`. Não é um rename — é contar outra coisa.

**Falta um serviço burro, não três.** Existe um que diz *onde você pode estar*
(`UnitReachEnvelopeService`) e um que diz *o que você revelaria*
(`VisionCoverageService`). Nenhum diz **o que você detectaria de lá**. Os três
`Melhor*` são consumidores, cada um com sua política — âncora é admissibilidade
(binário, depois custo), livre é maximização, e misturar as duas numa opção do
serviço é como um serviço vira política.

**A âncora do Spotting é (célula, camada), não célula.** *"O que tem no hex"*
precisa de camada para o `PodeDetectar` resolver o EV do destino. Só com a
célula, o serviço assume superfície em silêncio e nunca responde pelo helicóptero
parado sobre a mesma cidade.

E uma correção de fato: o portão de desembarque na névoa **existe** — em
`TurnStateManager.Disembark.cs:666` (`showDisembarkAboveFog`). Eu havia afirmado
que não, procurando só dentro do sensor. O print do autor, com o Chinook sobre o
mar oferecendo só "M - Apenas Mover", desmentiu na hora.

---

## 8. Dívidas criadas por esta versão

**O relatório mudou de conteúdo em dois pontos.** "Passou por" agora exige
`blockLoS`, e a subida acima de 16 valores resume o miolo (`8 + ...(+N)... + 6`).
Ambas são uma linha em `ObservationLineReport` se o autor discordar.

**`LogFogDebugState` varre o `cellBounds` do overlay.** É caro num mapa grande, e
por isso só roda no comando explícito. Se um dia for chamado de outro lugar,
precisa de amostragem.

**A barreira de apagar não foi testada nos sete chamadores.** Foi verificada por
leitura nos dois casos que importavam (preset sem névoa e turn start), mas só o
caminho do `fow off` tem repro conhecido.
