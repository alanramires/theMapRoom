# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-04, **depois** da tag `v7.1.0`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## Primeira coisa a fazer

**A perna de detecção** — a irmã do `PodeEnxergar`, que hoje ficou pronta.

A `v7.1.0` separou quem revela hexágono de quem faz unidade aparecer, mas só
terminou o primeiro lado. O `PodeDetectar` continua carregando as duas
responsabilidades no mesmo arquivo, e ainda decide por `if` coisas que deveriam
estar na ficha.

Antes ou junto disso, uma dívida que o autor cravou como princípio:

> *O `PodeEnxergar` não pode usar regras que pertençam ao `PodeDetectar` para
> liberar hexágonos.*

Hoje ele **não tem laço próprio**: monta a resposta chamando `CollectVisibleCells`
e desligando regras uma a uma por flag. Foi exatamente assim que o mar do
submarino sumiu — uma flag de detecção descartando célula antes de qualquer conta
de linha. Enquanto for flag, toda regra nova do `PodeDetectar` volta a vazar
para cá sem aviso.

O laço próprio precisa de duas primitivas expostas como **geometria pura**, sem
política dentro:

```text
GetIntermediateCellsByCellLerp   a caminhada dos hexes da linha
TryResolveCellVision             EV e blockLoS de uma célula
```

As duas são `private` no `PodeDetectarSensor`. Expor não é duplicar regra — é o
serviço burro sendo dividido, com cada sensor dono da política dele.

---

## Estado

`v7.1.0` tagueada e publicada na `main`. Relatório:
`docs/relatorio_v7.1.0.md`.

**Primeira versão desta série que muda comportamento em partida.** Até a
`v7.0.4` tudo era ferramenta e consumidor; agora o FOW revela terreno por uma
fonte diferente.

### A descoberta que organiza o próximo trabalho

> **Detecção não revela FOW.**

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
0. sensores PodeX               ⚠️ PodeEnxergar nasceu; PodeDetectar ainda mistura
1. serviços de área (Hotzone)   ✅ prontos
2. consumidores Melhor*         ⚠️ Melhor Visão consome a fotografia; falta Fusão
                                   e o Melhor Spotting
3. papéis → só POLÍTICA         docs/revisao_papeis.md — 1 linha de 7 levantada
4. variações de papel           vira perfil/trait depois da extração das linhas
```

O degrau 0 reabriu. Ele estava marcado como pronto e não estava: um sensor
respondia duas perguntas.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/relatorio_v7.1.0.md` | o fio do dia, e a seção 9 (o erro que custou o dia) |
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

### `DetectionMethod`

`LosPolicy` virou `DetectionMethod` com os números preservados:
`LineOfSight = 0`, `Propagated = 2`, o `1` morto sem herdeiro. Nenhuma ficha
precisou de edição manual.

`Propagated` já existia escondido em dois `if`: o mapa de distância aquático
soldado a `Submarine/Submerged` e o `range only` de `AirHigh`. **Continuam
soldados** — declará-los na ficha é trabalho da perna de detecção.

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

**A perna de detecção não começou.** É a próxima.

**`KnownCells` continua um balde só.** Não recebe mais conhecimento aéreo
especializado, mas ainda mistura terreno e memória de exploração, e o
`FogKnowledgeSnapshot` segue **sem eixo de camada**. O Melhor Spotting depende
disso, e o contato desenhado sobre o preto também.

**Saves antigos têm resíduo.** Hexes revelados pelo alcance de detecção antes da
`v7.1.0` já estão gravados como explorados e não são limpos.

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
- **Um commit por frente de trabalho**, não um pelo lote.
- **Número de build só entra em relatório se veio de build COM restore.**
  `dotnet build Assembly-CSharp.csproj -v q --nologo` — e o Editor é outro
  assembly: `Assembly-CSharp-Editor.csproj`.
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
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
