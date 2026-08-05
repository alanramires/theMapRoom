# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-05, **depois** da tag `v7.1.2` e da
partida de validação. Leia isto primeiro; ele descreve o estado pós-versão e não
pertence à tag.

---

## A partida de teste aconteceu — e passou

O autor validou cinco dos seis itens em jogo. **Nenhum voltou atrás:**

```text
bazuca da montanha atras da floresta   OK
deteccao ar-ar exigindo linha          OK
radar.mp3 no caca furtivo              OK  ("ficou muito legal")
contato cinza sobre o preto            OK
botao Camadas fora do painel           OK  (o autor deletou o objeto)
```

Isso encerra a dívida de validação das três versões. **O que ficou sem número é
a perf**: `FrameSpike` num turno de IA com muitas unidades nunca foi medido. A
`v7.1.1` trocou filtro por varredura em três pontos do caminho quente; o
suspeito é o `CollectDetection` por observador, que faz muito mais do que o
par-a-par fazia. Enquanto não houver número, não há problema **nem** garantia.

---

## Primeira coisa a fazer

**Padronizar a saída das duas janelas de sensor.** É o pedido direto do autor,
com print dos dois relatórios lado a lado, e a frase que importa:
*"eu gostava daquele relatório, sabe"*.

O `PodeEnxergar` diz, quando a linha morre:

```text
Subida da linha   0,00 -> 0,00      (passo a passo, string pronta)
EV na parada      0,00
Tentou ver EV     2,25
EV Bloqueador     Montanha (EV: 2,25)
```

O `PodeDetectar`, para a **mesma** reta, diz só `Altura por hex: 0 > 2` e
`Bloqueio LOS: -23,11`. Não conta quanto a linha tinha subido quando parou, nem
contra o quê — o leitor precisa deduzir que 2,25 > 2.

A causa é rasa e a correção é estrutural. O `PodeEnxergar` tem campos
construídos para o relatório (`lineRiseTrace`, `losHeightAtPassedCell`,
`passedLayerLabel`, `finalReachedEv`). O `PodeDetectar` recebeu na `v7.1.2` só
um `List<float> lineOfSightEvPath` cru, e a janela o imprime juntando os
números.

**O lugar certo é o `ObservationLineService`.** Ele já calcula tudo isso no
traçado detalhado; o que falta é ele devolver o *perfil da linha* como um tipo
só, e as duas janelas formatarem a partir dele. Mesma reta, mesmo relatório —
é a mesma lição de "uma pergunta, uma implementação", agora aplicada à saída.

Enquanto forem dois formatos, comparar as duas janelas continua sendo trabalho
manual de tradução, e é exatamente para isso que elas existem.

---

## A fila depois disso

```text
1. delta de "passou a detectar"           UM gancho, DOIS consumidores:
                                          o som e o Jornal (ver abaixo)
2. jornal para contato nao-furtivo        hoje so unidade stealth entra
3. exclamacao no contato novo             + foco pela linha do jornal;
                                          NAO pan automatico
4. decidir skipLosForCurrentTarget        DECISAO DO AUTOR: propriedade do
                                          MEIO (DPQ) ou metodo na ficha?
5. medir FrameSpike em turno de IA        a unica divida da v7.1.1 sem numero
6. apagar residuo de exploracao nos saves trivial: nada foi distribuido
```

O eixo de camada no `FogKnowledgeSnapshot` **saiu da fila** — ver "O item que
deixou de existir".

Só depois disso o **Melhor Spotting** — foi o combinado, e agora ele não tem
pré-requisito nenhum.

---

## O alerta de contato — o que já existe e o que falta

O autor levantou o caso: reposiciona o radar móvel, ele flagra um helicóptero
voando baixo atrás da montanha, sobre névoa preta. Sem aviso, o jogador pode
nunca saber que viu.

**O som já cobre isso.** O fallback da `v7.1.0` dispara em
`detectedStealth.Count > 0 || spottedCandidates.Count > 0` — qualquer detecção,
não só furtiva. Unidade comum entra em `spottedCandidates`; é de lá que
`IsTargetObservedByTeam` tira o "sim" para qualquer alvo. Observador sem chave
de sensor e sem clipe na skill fica calado, que é o gate desejado.

O problema é o inverso: **toca demais.** O radar reencontra os mesmos inimigos
toda vez que age. Repetição no furtivo é feedback ("continuo te vendo") e o
autor a quis de propósito; generalizada vira ruído de fundo.

**O jornal não cobre.** Dois portões barram:

```text
IsStealthUnit()                     MatchController.cs:5890
                                    so unidade furtiva chega ao NewContact
detectorSlotIndex != ActiveSlotId   MatchController.cs:6008
                                    "deteccao no proprio turno foi vista ao vivo"
```

O segundo é a premissa a revisar. "Viu ao vivo" vale para quem se move dentro
do seu campo de visão. **Não vale para um sensor que alcança onde seus olhos não
estão** — e é justamente o caso do radar sobre o preto. A regra fog-honesta não
é "turno de quem", é *"o jogador teve chance de ver isto acontecer?"*.

**Pan automático não.** Rouba a câmera no meio da jogada e, com três contatos
novos, o "primeiro" é arbitrário. Exclamação no contato (o marcador de
combustível já existe) mais a linha do jornal, que já sabe centralizar
(`isFocused` em `HelperTurnStartAutonomyLine`). O jogador decide quando ir.

---

## O item que deixou de existir

O **eixo de camada no `FogKnowledgeSnapshot`** estava escrito aqui como
pré-requisito do Melhor Spotting. Ele foi cancelado pelo autor, e não por ter
sido feito:

> *"se o terreno é conhecido, ele é conhecido/explorado. seu primeiro item
> 'conhecido em qual camada' realmente não importa"*

Depois da separação, só o `PodeEnxergar` libera hexágono, e só no alcance da
visão da ficha. EWACS e Supertucano são excelentes detectores de unidade
escondida — e não são mais reveladores de hexágono. Não existindo revelação por
camada, não existe conhecimento por camada. A pergunta perdeu o sentido.

Consequência: o atalho `L` virou no-op documentado e o botão de Camadas saiu do
painel (o autor deletou o objeto da cena). `CycleFogOfWarVisionMode` continua
público para depuração.

---

## Estado

`v7.1.1` tagueada e publicada na `main`. Relatórios:
`docs/relatorio_v7.1.0.md` e `docs/relatorio_v7.1.1.md`.

**As duas mudam comportamento em partida.** Até a `v7.0.4` tudo era ferramenta e
consumidor. A `v7.1.0` trocou a fonte da revelação de terreno; a `v7.1.1` trocou
a implementação que decide se **cada unidade inimiga aparece**.

Validado em jogo pelo autor: o submarino revela 3, os caças detectados aparecem,
o combate se resolveu junto e o recálculo ao destruir unidade também.

### As duas descobertas que organizam o próximo trabalho

> **Detecção não revela FOW.**

> **Uma pergunta, uma implementação.**

A segunda veio da primeira: existiam duas respostas para "eu detecto este alvo",
a que as ferramentas auditavam e a que o jogo usava. Elas discordavam, e a janela
dava confiança falsa sobre uma ficha correta.

Quatro quadrantes, todos existem, nenhum derivável do outro:

| | hex conhecido | contato detectado |
|---|---|---|
| soldado comum | sim | sim |
| EWACS a 7 | **não** | sim |
| sniper ao lado | sim | **não** |

Por isso as duas respostas não podem morar no mesmo conjunto. Um sonar ouvindo
um motor não pode ensinar onde fica a costa.

### A regra do `PodeEnxergar`, como o autor a definiu

```text
alcance    UnitData.visao — nada da lista de Detect alarga ou estreita
camada     a superfície do terreno da célula; revelação não tem meio, só alcance
origem     o EV do lugar onde a unidade está
linha      descendente até o EV do destino; para só se um bloqueador tiver EV MAIOR
borda      célula sem tile não é hex recusado, é ausência de tabuleiro
```

Ar e submerso não são terreno: o EV vem do `DPQAirHeightConfig`, por consulta.
O submarino em `Submerged` sai de EV 0 e **é um soldado em cima da água**.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅ HexGridGeometry, ObservationCellService,
                                    ObservationLineService
 0. sensores PodeX                ✅ PodeEnxergar com laço próprio, PodeDetectar
                                    fonte única, PodeMirar consumindo a mesma reta
 1. serviços de área (Hotzone)    ✅ prontos
 2. consumidores Melhor*          ⚠️ Melhor Visão consome a fotografia; falta Fusão
                                    e o Melhor Spotting
 3. papéis → só POLÍTICA          docs/revisao_papeis.md — 1 linha de 7 levantada
 4. variações de papel            vira perfil/trait depois da extração das linhas
```

O degrau 0 fechou de novo, e agora com um degrau **abaixo** dele. O que nunca foi
regra de sensor — fato de célula, geometria de grade e traçado de reta — saiu
para serviço próprio, porque as três verdades precisam dos mesmos fatos sem uma
depender da outra.

**830 linhas saíram do `PodeDetectarSensor`** nesta sequência; só 193 eram lixo,
as outras 637 mudaram de casa.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/relatorio_v7.1.1.md` | o fio mais recente, e a seção 8 (dívidas criadas) |
| 1b | `docs/relatorio_v7.1.0.md` | a separação, e a seção 9 (o erro que custou o dia) |
| 2 | `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| 3 | `docs/implementar_melhor_spotting.md` | ponto de execução do consumidor, bloqueado pelo eixo de camada |
| 4 | `docs/arquitetura/acoes_transacionais.md` | obrigatório antes de ligar ferramenta a runtime |
| 5 | `docs/revisao_papeis.md` | matriz, traits e correções da taxonomia |

---

## Onde eu parei

### `PodeEnxergar` — entidade completa, com laço próprio

`Assets/Scripts/Sensors/PodeEnxergarSensor.cs`. Responde só por hexágonos e é a
fonte do FOW: `CollectVisibleCellsForFogOfWar` delega a ele, e os três
consumidores herdam — FOW de runtime, bake da rodada zero, `RetaguardaWindow`.

O laço é dele: raio pela `visao` da ficha, `HexGridGeometry.CollectCellsInRadius`,
e uma chamada ao `ObservationLineService.TryTrace` por célula. **O arquivo não
menciona `PodeDetectar` em lugar nenhum** — que era o critério de retomada.

### As três portas fechadas

Fechar o vazamento exigiu achar três caminhos independentes:

- `preserveObserverLayerRangeForHexVisibility` — elevava a revelação ao alcance
  da camada do próprio observador;
- `BuildFogDisplayVisibleCellsForAllModes` — somava especialização de Air no
  conjunto que pinta terreno, e dali para a **memória permanente**;
- `AddSpecializedAirKnowledge` — o gêmeo no bake.

### `PodeDetectar` é fonte única

`IsTargetObservedByTeam` consome `CollectDetection` — a mesma coleta que as
janelas auditam. Antes havia duas implementações da mesma pergunta e elas
divergiam. Todo consumidor de visibilidade de unidade herda daí.

`CanObserverObserveTarget`, a segunda implementação, foi apagada.

### `DetectionMethod`

`LosPolicy` virou `DetectionMethod` com os números preservados:
`LineOfSight = 0`, `Propagated = 2`, o `1` morto sem herdeiro. Nenhuma ficha
precisou de edição manual.

`Propagated` agora **decide o mapa de distância**, não só a linha, e vale para
**qualquer camada** — o meio sai da camada do alvo. Um `detect land/surface 5
propagate` é um megafone sem código novo.

Ainda soldado num `if`: o `range only` de `AirHigh`, via `skipLosForCurrentTarget`,
que vem do `DPQAirHeightConfig` e fala do **meio**, não do sensor.

### Os serviços que saíram do `PodeDetectar`

```text
HexGridGeometry          CubeCoord, offset↔cubo, distancia, lerp, round, odd-row,
                         CollectCellsInRadius
ObservationCellService   terreno, construcao, estrutura, camada, EV, blockLoS
                         + os tres caches de refresh e o de grid
ObservationLineService   TryTrace, ResolveOriginEv, o lerp, o cube-line,
                         LosGrazeEpsilon e os contadores do traçado
```

Movimentação literal, com wrappers privados no `PodeDetectar` — nenhum dos ~20
chamadores internos mudou.

### A origem da reta é uma regra nomeada

Os três sensores usam a **mesma** reta. O que difere é de onde ela parte:

```text
InheritTerrain                    revelar hexagono, detectar unidade
ShooterInheritsWhenTerrainAllows  linha de tiro
```

A segunda lê `shooterInheritsTerrainEv` do terreno — Montanha é o único com ele
ligado. É o que faz a bazuca de alcance 2 na montanha acertar quem está atrás da
floresta: parte de EV 2 e passa por cima do EV 1 da árvore.

**Confirmado a pedido do autor:** EV nunca foi fator de combate.
`TurnStateManager.Combat.cs` — herdeiro do antigo `CombatResolver.cs`, hoje
inexistente — não tem uma ocorrência de EV, nem o `CombatModifierResolver`. EV
existe em um lugar só: a linha de visada.

**E o spotter empresta olho, não trajetória.** Em tiro reto, sem LdT a opção
morre num `continue` antes de o observador avançado ser cogitado.

### O recálculo no compromisso

O delta por células afetadas assumia que revelar e detectar eram a mesma coisa.
Depois da separação, um radar que detecta sem revelar não põe a célula do alvo no
conjunto, e o inimigo ficava com o valor velho. **Dois** lugares tinham o filtro:
`RefreshRuntimeUnitFogVisibilityForCells` e `PublishFogGameplaySnapshot` — este
último é de onde a apresentação de contato lê. No commit, os dois são completos.

### `PodeMirar` aceita alvo detectado em hex preto

Contato confirmado autoriza o tiro; o estado do terreno não opina. O que ficou é
a neutralização do **motivo** das entradas inválidas — o alvo pode aparecer
nomeado, a descrição do obstáculo não.

### Melhor Visão consome a fotografia

`MelhorVisaoService` aceita `FogKnowledgeSnapshot` e lê a cobertura aliada das
contribuições por hex. `ResolveUsableSnapshot` separa "não há fotografia" de "a
fotografia não se aplica". Nenhum `AIController` consome.

---

## Pendências abertas

**Perf não medida.** Três varreduras novas no caminho quente, nenhuma com número.
É a última dívida da `v7.1.1` sem resposta, e a única capaz de reordenar a fila.

**As duas janelas de sensor descrevem a mesma reta em formatos diferentes.** A
primeira tarefa da fila — ver "Primeira coisa a fazer".

**O som de detecção não tem noção de novidade.** Toca a cada reconstrução, para
qualquer contato. Foi decisão consciente no furtivo; generalizado precisa do
delta "passou a detectar". Mesmo gancho que o Jornal precisa.

**O Jornal só registra contato furtivo, e só em turno alheio.** Ver "O alerta de
contato". A premissa a revisar é a segunda, não a primeira.

**`KnownCells` continua um balde só.** Não recebe mais conhecimento aéreo
especializado, mas ainda mistura terreno e memória de exploração. Já **não**
bloqueia o Melhor Spotting — o eixo de camada foi cancelado.

**Saves antigos têm resíduo.** Hexes revelados pelo alcance de detecção antes da
`v7.1.0` já estão gravados como explorados. Como nada foi distribuído, é só
apagar.

**A Vigilância da `v7.0.3` continua sem validação registrada no Unity.**

**`MelhorCapitao` continua sem consumidor.** Falta o tradutor
`AICaptainData → List<MelhorCapitaoAttraction>`.

**`roles[0] == CapturadorAgressivo` continua no `GetCapturePower`.**

**Melhor Combate e Melhor Captura não governam a IA.**

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** Ler diff e contrato real.
- **Ler `docs/manual/` antes de decidir onde uma regra mora.**
- **Nada provisório publica verdade confirmada.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não classificar arquivo do autor como churn sem perguntar.** Cena e `.asset`
  grandes podem ser o estado que ele usou para validar.
- **Nada foi distribuído** — save e bake podem mudar de forma quantas vezes o
  design precisar. Não propor shim de versão nem retrocompatibilidade.
- **Um commit por frente de trabalho**, não um pelo lote.
- **Número de build só entra em relatório se veio de build COM restore.**
  `dotnet build Assembly-CSharp.csproj -v q --nologo` — e o Editor é outro
  assembly: `Assembly-CSharp-Editor.csproj`.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **uma pergunta, duas implementações** | a janela auditava `CollectDetection` e o jogo usava `CanObserverObserveTarget`. A ferramenta não estava errada — estava olhando outro caminho, e deu confiança falsa sobre uma ficha correta |
| **uma pergunta, dois relatórios** | a versão de saída do mesmo erro. `PodeEnxergar` e `PodeDetectar` traçam a **mesma** reta e a descrevem com campos diferentes. Comparar as duas janelas vira tradução manual, que é o oposto do que elas existem para fazer |
| **testar só o caso especial** | o som generalizado já funcionava para unidade comum desde a `v7.1.0`, e passou despercebido porque todo teste usou furtivo — que casa chave própria e nunca chega ao fallback. O caminho geral só aparece quando o caso especial não se aplica |
| **uma regra aplicada onde ela não vale** | *"a unidade herda EV só para revelar hex e detectar"* virou, na minha mão, o `PodeMirar` inteiro sem herança — e a bazuca da montanha perdeu a linha. A LdT precisa da herança; e o `PodeMirar` usa **duas** retas, tiro e observação, com regras diferentes |
| **declarar dado órfão cedo demais** | cheguei a propor apagar `shooterInheritsTerrainEv` porque tinha ficado sem leitor. Ele tinha ficado sem leitor **porque eu quebrei a regra** que o lia |
| **editar `.cs` por script no PS 5.1** | `Get-Content` sem BOM lê como ANSI e corrompe acentos; `Set-Content -Encoding utf8` põe BOM em mensagem de commit. Use `ReadAllLines`/`WriteAllLines` com UTF-8 explícito — e confira o **diffstat**: deleção pura com inserções é sinal de reescrita não intencional |
| **premissa que funcionava por acidente** | o delta do FOW filtrava unidades por célula com revelação alterada. Isso cobria detecção só enquanto revelar e detectar eram a mesma coisa. Separar as duas quebrou o delta sem tocar nele |
| **consertar metade e achar que acabou** | o mesmo filtro existia em `RefreshRuntimeUnitFogVisibilityForCells` **e** em `PublishFogGameplaySnapshot`. O segundo só apareceu depois de o primeiro ser corrigido |
| **generalizar e abrir buraco** | ao tirar o privilégio do submarino no `Propagated`, o mapa de distância continuou sendo montado uma vez sem guardar de que camada era |
| **`Get-Content` do PS 5.1 em arquivo sem BOM** | lê como ANSI e corrompe acentos. Pego porque o diffstat de uma **deleção pura** mostrava 11 inserções — esse número tem que ser zero |
| **afirmar mecanismo lendo um trecho** | o `PodeDetectarSensor` tem quatro caminhos parecidos. Ler um pedaço não permite dizer qual roda. Cinco hipóteses erradas seguidas na `v7.1.0`, uma delas piorando o sintoma e exigindo revert |
| **`skipSpecializedTargetLayers`** | não ignora o alcance das especializações: **descarta a célula** cuja camada tenha Detect Specialization. Foi ela que apagou o mar do submarino |
| **sensor com flags em vez de laço próprio** | toda flag desligada traz junto uma regra que ninguém pediu |
| **contraste lido como causa** | "o soldado funciona e o submarino não" era sobre *ter especialização*, não sobre água |
| **correlação tratada como mecanismo** | um revert feito por coincidência temporal; a detecção nunca passou pelo código alterado |
| **`Set-Content -Encoding utf8` em mensagem de commit** | PowerShell 5.1 escreve BOM, e ele aparece no `git log`. Usar o editor de arquivos |
| **here-string do PS 5.1 com aspas para native exe** | o argumento é re-quebrado. Mensagem de commit vai por `-F arquivo` |
| **`dotnet build --no-restore` com `Temp/obj` limpo** | imprime "0 Warning(s)" porque nada compilou |
| **um commit para o lote inteiro** | impede reverter uma frente sem derrubar as outras |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar e atirar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; missão obrigatória precisa de admissibilidade explícita |
| **recalcular percepção por candidato** | snapshot/bake já possui conhecimento e contribuições |
| **mudar inicializador de `EditorWindow`** | campo serializado preserva o valor antigo |
| **ocupado = inalcançável** | reporte chegada e ocupação separadamente |
| **classificar antes de unificar o órgão** | primeiro extraia a fonte única |
| **skill que se declara** | se renomear a etiqueta quebra, o poder está no lugar errado |
| **troca de tipo em lista serializada** | Unity preserva a contagem e deixa conteúdo nulo |
| **gate inaplicável** | separar "não satisfeito" de "impossível/desconhecido" |
| **otimizar por hipótese** | medir antes |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |

---

## Critério de retomada

O critério anterior foi cumprido: o `PodeDetectar` responde só por unidades, o
`PodeEnxergar` pode ser lido sem abrir o `PodeDetectarSensor`, e a partida
confirmou os dois em jogo.

O que sobrou dele é uma linha só: **`Propagated` ainda não é escolhido pela
ficha em toda camada** — o `range only` de `AirHigh` continua vindo de um `if`
sobre o `DPQAirHeightConfig`. É decisão do autor (item 4 da fila), porque a flag
fala do *meio*, não do sensor.

O novo critério é de **saída**: a perna de percepção está pronta quando as duas
janelas descreverem a mesma reta com as mesmas palavras, e quando um contato
novo avisar o jogador uma vez — em vez de calar no Jornal e repetir no alto-falante.
