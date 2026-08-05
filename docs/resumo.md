# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-05, **depois** da tag `v7.2.0`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## A dívida de validação em jogo

A `v7.2.0` inteira **compila e não rodou em Play**. Três coisas precisam de
partida antes de virar verdade:

```text
1. as duas janelas lado a lado         o teste do relatorio unificado
2. radar movido duas vezes sobre os    o delta do som: deve tocar na primeira,
   mesmos inimigos                     calar na segunda
3. aeronave no meio do voo -> fow off  deve RECUSAR com a mensagem; e depois,
                                       em Neutral, fow off/on -> nevoaTiles ~1700
```

O item 3 tem instrumento: `LogFogDebugState` imprime `[FoW][Estado][ON|OFF|PARTIAL]`
com `explorado` e `nevoaTiles`. **`nevoaTiles = 0` significa tabuleiro aberto**,
independentemente do que o cálculo disser.

**Protocolo de teste de névoa:** enquanto a partida estiver em Play, ninguém
salva `.cs`. Salvar recarrega o domínio, e quase todo o estado da névoa é
`[System.NonSerialized]` — inclusive `debugFogOfWarEnabled = true`. Um recompile
**religa a névoa sozinho** e zera a memória de exploração, o que se parece
exatamente com "consertou sozinho".

---

## A fila

```text
1. jornal para contato nao-furtivo        hoje so unidade stealth entra; o
                                          gancho ja existe (v7.2.0)
2. exclamacao no contato novo             + foco pela linha do jornal;
                                          NAO pan automatico
3. portao de nevoa no PodePousar          o PodeDesembarcar JA TEM
                                          (TurnStateManager.Disembark.cs:666)
4. tirar o laco de hex do PodeDetectar    CollectVisibleCells ainda e publico
5. servico de cobertura de DETECCAO       o buraco que os tres Melhor* precisam
6. fow off pausa cook/bake/snapshot       spec do autor, hoje nao existe
7. apagar residuo de exploracao nos saves trivial: nada foi distribuido
8. medir FrameSpike em turno de IA        adiado pelo autor
```

**Item 4 da fila antiga saiu:** `skipLosForCurrentTarget` **não existe no código**
— zero ocorrências fora de docs. A decisão que estava esperando o autor já foi
tomada pela extração: o `blockLoS` do DPQ é consumido célula a célula por
`TerrainVisionResolver` → `ObservationCellService` → `cellBlocksLoS` da linha.
Virou propriedade do **meio**, e não há `if` para tirar.

---

## O paradigma dos três consumidores

Nasceu no meio da conversa da `v7.2.0`, mudou duas vezes, e organiza tudo que
vem. Não virou código.

```text
melhorVisao       de onde eu revelo AQUELA faixa de praia         moeda: hex
melhorDeteccao    de onde eu lanco a rede maior, sem revelar hex  moeda: contato
melhorSpotting    de onde eu vejo quem ocupa AQUELA cidade        moeda: contato
```

O autor primeiro concluiu que *"revelar hexágono não serve pra nada no serviço de
inteligência"* — e depois **revogou**, com um caso concreto: Apache e Chinook
sobre o mar, o Chinook carregado publica a intenção *"quero saber se aquela faixa
de praia tem lugar pra pouso"*, o Apache assume e revela com sua visão 3. Revelar
tem função, **porque não se desembarca na névoa**.

Três consequências que ainda valem:

**A moeda do `MelhorVisao` de hoje está errada.** Ele pontua `VisibleCount`, que
conta células de `VisionCoverageResult.VisibleCells`, que vêm do laço de **hex**
do `PodeDetectar`. Não é um rename — é contar outra coisa.

**Falta um serviço burro, não três.** Existe um que diz *onde você pode estar*
(`UnitReachEnvelopeService`) e um que diz *o que você revelaria*
(`VisionCoverageService`). Nenhum diz **o que você detectaria de lá**. Os três
`Melhor*` são consumidores, cada um com sua política: âncora é admissibilidade
(binário, depois custo), livre é maximização. Misturar as duas numa opção do
serviço é como serviço vira política.

**A âncora do Spotting é (célula, camada), não célula.** Sem a camada o serviço
assume superfície em silêncio e nunca responde pelo helicóptero parado sobre a
mesma cidade.

**Pergunta em aberto do `MelhorDeteccao`:** *"com base na cobertura que você já
tem"* — a linha de base é **(a)** a cobertura de detecção que o seu time já
lança, e o score é o *ganho marginal* (não desperdice rede onde já tem rede), ou
**(b)** onde os inimigos conhecidos estão? As duas posicionam o EWACS em lugares
opostos. Recomendação: (a) como base, (b) como peso depois.

---

## Estado

`v7.2.0` tagueada e publicada na `main`. Relatório: `docs/relatorio_v7.2.0.md`.

Seis frentes, em commits separados: relatório unificado da linha, delta de
detecção, barreira de apagar, botão de Camadas, relatório, cena reautorada.

### A frase que organiza esta versão

> **Apagar também é publicar.**

O CLAUDE.md diz *"nada provisório publica verdade confirmada"*. A barreira de
escrita da névoa leu isso pela metade: protegia o desenho e deixava o apagamento
passar. Fora de `Neutral`, o par vira o pior possível — apaga e não repõe.

---

## Onde eu parei

### O relatório da linha é um só

`Assets/Scripts/Sensors/ObservationLineProfile.cs` guarda a reta inteira num tipo
só. `ObservationLineReport.cs` a transforma em linhas rotuladas e **não calcula
nada**. O `TryTrace` anota o perfil no mesmo laço que decide a linha; com
`profile == null` o comportamento é idêntico ao anterior.

Três janelas imprimem o mesmo relatório: Pode Enxergar, Pode Detectar, Alguém me
vê. `TryTraceObservationLineDetailed` (127 linhas) morreu, e com ela a última
amarra: a **janela** do `PodeEnxergar` não chama mais o `PodeDetectarSensor`.

### O delta de detecção

`FogSlotGameplaySnapshot.gainedContacts`, calculado no `PublishFogGameplaySnapshot`.
Consultado por `HasGainedDetectionContact`. Dois consumidores, ambos rodando
**depois** do publish: o fallback do som e o Jornal.

O portão `detectorSlotIndex != ActiveSlotId` do Jornal **continua lá** — é o item
1 da fila.

### A barreira de apagar

`IsFogVisualEraseAuthorized` guarda só a metade transacional (`isNeutral` +
contexto), sem a comparação de slot em cache — que faz sentido para desenhar e
nenhum para apagar. `ResetFogOfWarRuntime` a consulta antes de tocar nos
tilemaps; recusado, o overlay fica **velho em vez de vazio**, e se cura sozinho
no próximo render autorizado.

`fow on|off|partial` recusa fora de `Neutral` com a razão na tela.

---

## Pendências abertas

**`PodeDetectar` ainda responde por hexágono.** `CollectVisibleCells` continua
público, com consumidores vivos:

```text
MatchController.AddFogLayerVisibleCellsForUnit   11 chamadas, forceVirtualTargetLayer
VisionCoverageService (2)                        um com preserveObserverLayerRange = TRUE
HexEnxergadoDebugWindow                          janela
```

O `preserveObserverLayerRangeForHexVisibility: true` é **uma das três portas que
a `v7.1.0` fechou**. Não alcança mais o FOW nem a memória permanente, mas
`MelhorVisaoService` e `AIController.Vigilancia` leem dali — a IA ainda enxerga
por uma regra que a revelação do jogo abandonou.

**Código morto confirmado nesta sessão:** `AddSpecializedAirKnowledge` tem zero
chamadas (o gêmeo do bake foi desligado na `v7.1.0` e o corpo ficou); idem
`CollectVisibleAirCellsAt`. E `rangeOnlyForAirHigh` na janela do Pode Enxergar é
sempre falso — o único cenário que ela monta é Land/Surface com `forceLayer:
false`.

**O `fow off` não pausa nada.** `RefreshFogOfWarForCurrentTeamInternal` checa
`SuppressFogOfWarRefresh` e `enableTotalWar`, **não** `debugFogOfWarEnabled`. Com
a névoa desligada o pipeline continua rodando a cada compromisso, inclusive
`RecordConfirmedExploredCells`.

**Perf não medida.** Adiado pelo autor — a infra do jogo vem antes.

**`KnownCells` continua um balde só.** Não bloqueia mais nada; ainda mistura
terreno e memória de exploração.

**Saves antigos têm resíduo.** Hexes revelados por alcance de detecção antes da
`v7.1.0` estão gravados como explorados. Nada foi distribuído — é só apagar.

**A Vigilância da `v7.0.3` continua sem validação registrada no Unity.**

**`MelhorCapitao` continua sem consumidor.** Falta o tradutor
`AICaptainData → List<MelhorCapitaoAttraction>`.

**`roles[0] == CapturadorAgressivo` continua no `GetCapturePower`.**

**Melhor Combate e Melhor Captura não governam a IA.**

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅ HexGridGeometry, ObservationCellService,
                                    ObservationLineService (+ Profile e Report)
 0. sensores PodeX                ⚠️ PodeEnxergar e PodeDetectar separados, mas o
                                    laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta o de cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ Melhor Visão conta a moeda errada; faltam
                                    Detecção e Spotting
 3. papéis → só POLÍTICA          docs/revisao_papeis.md — 1 linha de 7 levantada
 4. variações de papel            vira perfil/trait depois da extração das linhas
```

O degrau 0 **reabriu**. Ele tinha sido dado como fechado na `v7.1.2`, e a
verificação desta sessão mostrou que o `PodeDetectar` ainda responde pergunta de
hexágono para quatro chamadores.

---

## O que ler, e nesta ordem

| # | documento | por quê |
|---|---|---|
| 1 | `docs/relatorio_v7.2.0.md` | o fio mais recente; a seção 4 (hipóteses erradas) e a 7 (o paradigma) |
| 2 | `docs/relatorio_v7.1.0.md` | a separação, e a seção 9 (o erro que custou o dia) |
| 3 | `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| 4 | `docs/arquitetura/acoes_transacionais.md` | obrigatório antes de ligar ferramenta a runtime |
| 5 | `docs/revisao_papeis.md` | matriz, traits e correções da taxonomia |

---

## Regras de trabalho

- **Uma classe por vez.** Compilar e rodar no jogo antes da próxima fase.
- **Avaliar não é executar.** Plano pedido não autoriza implementação.
- **Verificar antes de documentar.** Ler diff e contrato real.
- **Ler `docs/manual/` antes de decidir onde uma regra mora.**
- **Nada provisório publica verdade confirmada — e apagar é publicar.**
- **Tem relatório, tem tag. Não tem relatório, é só commit.**
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não classificar arquivo do autor como churn sem perguntar.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- **Nada foi distribuído** — save e bake podem mudar de forma quantas vezes o
  design precisar. Não propor shim de versão nem retrocompatibilidade.
- **Um commit por frente de trabalho**, não um pelo lote.
- **Número de build só entra em relatório se veio de build COM restore.**
  `dotnet build Assembly-CSharp.csproj -v q --nologo` — e o Editor é outro
  assembly: `Assembly-CSharp-Editor.csproj`. Arquivo `.cs` novo não entra no
  `.csproj` até a Unity regerar; para compilar antes disso, adicione a linha
  `<Compile Include>` à mão (o `.csproj` é gitignored).
- Fechar o dia: skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---

## Armadilhas que já custaram tempo

| armadilha | lição |
|---|---|
| **barreira que só cobre um sentido** | a de escrita da névoa protegia desenhar e deixava apagar passar. Fora de Neutral, apaga e não repõe — e voltar a Neutral conserta, o que faz o sintoma parecer aleatório |
| **a mesma barreira em dois usos diferentes** | a de desenhar exige o slot em cache; a de apagar não pode exigir, porque o próprio reset zera o campo. Copiar a barreira inteira teria deixado névoa velha nos presets sem névoa |
| **três hipóteses erradas antes do log** | duas minhas e uma do autor, todas mortas por **um log de partida real**. O que apontou o mecanismo foi o detalhe que nenhuma explicava: *"quando desfaço o movimento, volta ao normal"* — correlação reversível, e só a barreira produz isso |
| **recompile em Play parece conserto** | salvar `.cs` recarrega o domínio e religa `debugFogOfWarEnabled = true`. Qualquer teste de névoa feito enquanto alguém edita é lixo |
| **procurar a regra só no sensor** | afirmei que não havia portão de névoa no desembarque; ele existe, em `TurnStateManager.Disembark.cs:666`. O print do autor desmentiu na hora |
| **doc que envelhece e vira fato** | `skipLosForCurrentTarget` estava na fila como decisão pendente do autor e **não existe no código** há versões. Conferir antes de planejar em cima |
| **uma pergunta, dois relatórios** | `PodeEnxergar` e `PodeDetectar` traçavam a **mesma** reta e a descreviam com campos diferentes. Comparar as duas janelas virava tradução manual, que é o oposto do que elas existem para fazer |
| **destino pretendido lido como chegada** | `Viagem da linha: 0,00 -> 4,00` com a subida parando em 1,75 logo abaixo. O alvo precisa aparecer (dele sai a inclinação) mas não pode se passar por resultado |
| **campo escrito que ninguém lê** | `lineOfSightIntermediateCells` era copiado por candidato no caminho quente da detecção, sem um único leitor |
| **uma pergunta, duas implementações** | a janela auditava `CollectDetection` e o jogo usava `CanObserverObserveTarget`. A ferramenta não estava errada — olhava outro caminho |
| **testar só o caso especial** | o som generalizado já funcionava desde a `v7.1.0` e passou despercebido porque todo teste usou furtivo, que nunca chega ao fallback |
| **uma regra aplicada onde ela não vale** | *"a unidade herda EV só para revelar hex e detectar"* virou o `PodeMirar` inteiro sem herança, e a bazuca da montanha perdeu a linha |
| **declarar dado órfão cedo demais** | propus apagar `shooterInheritsTerrainEv` porque ficara sem leitor. Ele ficara sem leitor **porque eu quebrei a regra** que o lia |
| **editar `.cs` por script no PS 5.1** | `Get-Content` sem BOM lê como ANSI e corrompe acentos. Use `ReadAllLines`/`WriteAllLines` com UTF-8 explícito — e confira o **diffstat** |
| **dividir commit por hunk sem rede** | ao separar duas frentes no mesmo arquivo: guarde o arquivo final, aplique só a frente A, depois **restaure o arquivo inteiro** e o resto é a frente B. `cmp` contra o backup prova que nada corrompeu |
| **premissa que funcionava por acidente** | o delta do FOW filtrava unidades por célula com revelação alterada. Separar revelar de detectar quebrou o delta sem tocar nele |
| **consertar metade e achar que acabou** | o mesmo filtro existia em `RefreshRuntimeUnitFogVisibilityForCells` **e** em `PublishFogGameplaySnapshot` |
| **afirmar mecanismo lendo um trecho** | o `PodeDetectarSensor` tem quatro caminhos parecidos. Cinco hipóteses erradas seguidas na `v7.1.0` |
| **`skipSpecializedTargetLayers`** | não ignora alcance: **descarta a célula** cuja camada tenha Detect Specialization. Foi ela que apagou o mar do submarino |
| **sensor com flags em vez de laço próprio** | toda flag desligada traz junto uma regra que ninguém pediu |
| **posição hipotética criando conhecimento** | movimento no cálculo não permite detectar e atirar antes do compromisso |
| **foco tratado como gate** | `FocusCells` só soma pontos; missão obrigatória precisa de admissibilidade explícita |
| **mudar inicializador de `EditorWindow`** | campo serializado preserva o valor antigo |
| **gate inaplicável** | separar "não satisfeito" de "impossível/desconhecido" |
| **otimizar por hipótese** | medir antes |
| **`git add .`** | só no passo de churn do fechamento |
| **tag antes do commit final** | tag é a última coisa da versão |

---

## Critério de retomada

O critério da `v7.1.2` era de **saída**: as duas janelas descrevendo a mesma reta
com as mesmas palavras, e um contato novo avisando o jogador uma vez. As duas
coisas estão escritas e compilam; **nenhuma foi vista em jogo**.

O novo critério é de **prova**: a perna de percepção está pronta quando o autor
abrir as duas janelas lado a lado sem traduzir nada, e quando um radar movido
duas vezes sobre os mesmos inimigos tocar uma vez só.

Depois disso, o degrau 0 tem uma linha que reabriu: **tirar o laço de hexágono de
dentro do `PodeDetectar`**. Enquanto ele for público lá, "PodeDetectar responde
só por unidades" é aspiracional, e o próximo consumidor que precisar de hex vai
achá-lo primeiro — foi assim que a tabela de flags cresceu da primeira vez.
