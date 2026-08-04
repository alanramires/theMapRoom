# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-04, **depois** da tag `v7.1.1`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## Primeira coisa a fazer

**Medir a perf de um turno de IA com muitas unidades.** Não é a tarefa mais
interessante da lista, é a que pode invalidar as outras.

A `v7.1.1` trocou filtro por varredura em **três** pontos do caminho quente, na
mesma sessão, sem medir nenhum:

```text
IsTargetObservedByTeam   par-a-par  →  CollectDetection por observador
refresh de visibilidade  delta      →  cheio, por commit
publish do snapshot      delta      →  cheio, por commit
```

Cada uma é defensável sozinha. As três juntas multiplicam trabalho. O
`FrameSpike` é o instrumento; o suspeito provável é o `CollectDetection` por
observador, que faz muito mais do que o par-a-par fazia.

Se estiver limpo, a fila continua na **tarefa 3 abaixo**. Se não estiver, ela
muda de ordem.

---

## A fila, na ordem

```text
1. medir perf do turno de IA                       ← acima
2. mover a linha para casa propria                 HasValidStraightObservationLine,
                                                   ResolveOriginEvForLos, o lerp, o cube-line
3. laco proprio do PodeEnxergar                    mata skipSpecializedTargetLayers,
                                                   preserveObserverLayerRangeForHexVisibility
                                                   e o ignoreDetectSpecializations (andaime)
4. linha de quem detectou no resultado             como o PodeEnxergar ja mostra
5. delta de contatos novos no publish              gancho do som e do Jornal
6. decidir skipLosForCurrentTarget                 meio (DPQ) ou metodo na ficha?
7. eixo de camada no FogKnowledgeSnapshot          destrava o Melhor Spotting
8. apagar residuo de exploracao nos saves          trivial: nada foi distribuido
```

A dívida que o autor cravou como princípio, e que a tarefa 3 fecha:

> *O `PodeEnxergar` não pode usar regras que pertençam ao `PodeDetectar` para
> liberar hexágonos.*

Ele ainda monta a resposta chamando `CollectVisibleCells` e desligando regras uma
a uma por flag. Foi assim que o mar do submarino sumiu — uma flag de detecção
descartando célula antes de qualquer conta de linha.

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
-1. serviços burros do tabuleiro  ✅ ObservationCellService, HexGridGeometry
 0. sensores PodeX                ⚠️ PodeDetectar é fonte única; PodeEnxergar
                                    ainda é ele com flags desligadas
 1. serviços de área (Hotzone)    ✅ prontos
 2. consumidores Melhor*          ⚠️ Melhor Visão consome a fotografia; falta Fusão
                                    e o Melhor Spotting
 3. papéis → só POLÍTICA          docs/revisao_papeis.md — 1 linha de 7 levantada
 4. variações de papel            vira perfil/trait depois da extração das linhas
```

O degrau 0 reabriu e ganhou um degrau **abaixo** dele: o que nunca foi regra de
sensor — fato de célula e geometria de grade — saiu para serviço próprio, porque
as duas verdades precisam dos mesmos fatos sem uma depender da outra.

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

### `PodeEnxergar` — entidade viva, sem laço próprio

`Assets/Scripts/Sensors/PodeEnxergarSensor.cs`. Responde só por hexágonos e é a
fonte do FOW: `CollectVisibleCellsForFogOfWar` delega a ele, e os três
consumidores herdam — FOW de runtime, bake da rodada zero, `RetaguardaWindow`.

Falta o laço próprio (ver "Primeira coisa a fazer").

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
ObservationCellService   terreno, construcao, estrutura, camada, EV, blockLoS
                         + os tres caches de refresh e o de grid
HexGridGeometry          CubeCoord, offset↔cubo, distancia, lerp, round, odd-row
```

Movimentação literal, com wrappers privados no `PodeDetectar` — nenhum dos ~20
chamadores internos mudou.

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

**Perf não medida.** A dívida mais urgente — ver "Primeira coisa a fazer".

**A linha ainda mora no `PodeDetectar`.** `HasValidStraightObservationLine`,
`ResolveOriginEvForLos`, o lerp e o cube-line. Com a geometria e o fato de célula
já fora, ela deixou de arrastar a teia.

**A linha de quem detectou não aparece no resultado.** O autor pediu que o
`PodeDetectar` mostre subida ou descida, como o `PodeEnxergar` já mostra.

**O alerta sonoro precisa de um gancho que não existe.** O certo não é
"detectou", é **"passou a detectar"** — o delta entre o conjunto anterior e o
novo no publish. Sem isso o sonar toca a cada refresh. O `radar.MP3` já está no
repositório, sem nada referenciando.

**`KnownCells` continua um balde só.** Não recebe mais conhecimento aéreo
especializado, mas ainda mistura terreno e memória de exploração, e o
`FogKnowledgeSnapshot` segue **sem eixo de camada**. O Melhor Spotting depende
disso.

**Saves antigos têm resíduo.** Hexes revelados pelo alcance de detecção antes da
`v7.1.0` já estão gravados como explorados. Como nada foi distribuído, é só
apagar.

**Contato sobre o preto ainda não foi validado em jogo.**
`ApplyFogDetectedContactPresentation` já faz `detectado && !geograficamenteVisível
→ cinza`, mas isso só passa a ser o caminho normal agora. O sprite pode ficar
atrás do overlay, que assume oclusão onde há tile.

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

A perna de detecção está pronta quando o `PodeDetectar` responder **só por
unidades**, sem nenhum caminho que contribua para conjunto de células de terreno,
e quando `Propagated` for escolhido pela ficha em vez de por `if` na camada.

E o `PodeEnxergar` está pronto quando puder ser lido sem abrir o
`PodeDetectarSensor`.
