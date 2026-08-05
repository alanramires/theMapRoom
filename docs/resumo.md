# Resumo — onde estamos e o que vem

Ponto de retomada. Escrito em 2026-08-05, **depois** da tag `v7.2.1`. Leia isto
primeiro; ele descreve o estado pós-versão e não pertence à tag.

---

## A dívida de validação em jogo

**Duas versões seguidas sem partida.** A `v7.2.0` compila e não rodou; a `v7.2.1`
**não foi nem compilada** — o Console do Editor não foi aberto uma vez no dia
dela. É a maior dívida aberta do projeto agora, e ela cresce.

Da `v7.2.0`:

```text
1. as duas janelas lado a lado         o teste do relatorio unificado
2. radar movido duas vezes sobre os    o delta do som: deve tocar na primeira,
   mesmos inimigos                     calar na segunda
3. aeronave no meio do voo -> fow off  deve RECUSAR com a mensagem; e depois,
                                       em Neutral, fow off/on -> nevoaTiles ~1700
```

Da `v7.2.1`, na ordem de quem quebra mais barato:

```text
4. compilar                            5 frentes, nenhuma vista pelo compilador
5. turno com 2+ cidades vazias         as duas tem que aparecer no Jornal
6. passar turno em hot seat DEMORANDO  o Jornal tem que abrir inteiro, com a
   de proposito na cortina             barra cheia, no instante em que a tela abre
7. menu > resumo do turno              o botao movido tem que abrir o Jornal
8. turno 1 do mesmo mapa               nenhum [FilaCarona] pode citar caca
9. linha [AI Perf][Unit] do APC #31    ela nunca chegou; sem ela o detalhe dos
                                       3,3 s restantes e estimativa
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

`v7.2.1` tagueada e publicada na `main`. Relatório: `docs/relatorio_v7.2.1.md`.

Sete commits, cinco frentes: Jornal (conteúdo, apresentação, ciclo de vida),
fiação do menu, MelhorEmbarque, rebasing aéreo, pergunta de carona da aeronave.

### A frase que organiza esta versão

> **A pergunta errada também responde.**

Nenhum defeito do dia apareceu como erro. O caça dizia *"sem rota própria"* com a
naturalidade de quem sabe do que fala; o Jornal listava uma cidade vazia com nome
e coordenada; o botão aparecia na tela e as setas chegavam nele. Em cinco frentes
o conserto não foi calcular melhor — foi **perguntar a coisa certa, a quem sabe
responder, na ordem certa**.

A frase da `v7.2.0` (*"apagar também é publicar"*) segue valendo e está no
relatório dela.

---

## Onde eu parei

### Jornal — três frentes que não se tocam

**Conteúdo.** `ConstructionManager.CollectDepletedSupplies` é agora o ponto único
do *"o que está zerado aqui"*, no mesmo universo do ícone do hex (catálogo ∪
ofertas runtime). O Jornal só redige, e emite **uma linha por prédio**. Categoria
nova `OCUPAÇÃO INIMIGA` (tier Atenção), que cala quando `SOB CAPTURA` fala.

**Apresentação.** `PanelHelperController`: viewport com máscara própria, título
fixo fora da área rolável, roda/arraste/setas. Teto `min(conteúdo, 52% da tela,
620px)`.

**Ciclo de vida.** `HoldTurnStartBriefingClockWhileInputBlocked`
(`TurnStateManager.HelperPanel.cs`), chamada **antes** da barreira de input em
`ScannerPrompt.cs:240`. O prazo rearma enquanto a cortina do hot seat estiver no
ar.

**O que falta:** as varreduras de estado ainda são **fotografia**. Estoque, sob
captura e ocupação são calculados no início do turno e não recalculam quando o
relatório é reaberto pelo menu — reabastecer uma cidade e reabrir ainda mostra o
aviso velho. A separação evento/varredura resolve os dois lados de uma vez:
evento congela (drenado do ledger, salvo), varredura recalcula a cada abertura.

### Menu — a fiação parou de depender do painel

`FindMenuButtonByNames` (`BattleMapMenuRootController`) tenta o painel de origem
e cai para o `menuRoot` inteiro. Os três "Voltar" ficaram de fora **de
propósito**: cada painel tem o seu.

Descoberta que vale guardar: `btnConsumo` e `btnCamada` estão `{fileID: 0}` no
prefab. Nunca estiveram no Inspector — sempre dependeram da auto-fiação.

### MelhorEmbarque — a inversão, e o que ficou medido pela metade

`ResolvePassengerMeeting` faz o encontro (dicionário) **antes** da sonda de
embarque. Resultado medido: frame da decisão do APC #31 de 11.808 ms para
3.308 ms; sondas do Suprimentos #24 de 92 para 11.

**Instrumentado e ainda não medido:** `melhorEmbarque.lzGates` +
`MelhorEmbarqueLzGateProbes/Rejects`. A hipótese é que o portão por LZ candidata
(a mesma sonda, uma por célula) domine o laço agora — no #24 sobraram ~82 ms de
91 no laço depois da inversão.

**A raiz continua lá:** `UnitMovementPathRules.TryGetEnterCellCost` faz quatro
consultas de tabuleiro **sem cache** por chamada (construção, estrutura, terreno,
tile). Dar um `MovementQueryCache` a ela barateia a sonda para todos os
consumidores — `melhorCaptura`, `validPaths`, o portão por LZ —, não só para o
transporte. É o conserto de raiz, e não foi feito.

### Aéreo — banda, rótulo, e a pergunta certa

`ResolveAirPlatformMinimumMissionGain` (`AIController.AirPlatform.cs`) devolve um
turno de voo da aeronave avaliada, no lugar do `2f` cravado. `logCategory`/
`logVerb` fizeram o rebasing parar de se anunciar como `[Repair] EVAC`.
`EvaluatePickupRideNeed` bifurca: aeronave responde só a `EvaluateEmergencyOnly`.

**A porta que continua aberta:** `acceptOnlyRecovery: true` aceita rebasing
quando a plataforma é a única recuperação no alcance, **sem olhar combustível**.
Um caça com 65/75 de autonomia não precisa de recuperação nenhuma. Se os caças
continuarem embarcando depois da banda, é por aqui — e o log agora dirá a frase
*"unica recuperacao compativel"* em vez do ganho.

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

**`OCUPAÇÃO INIMIGA` cobre prédio que É seu, não que ERA.** O caso do prédio que
o inimigo tomou e continua ocupando exige guardar o **dono anterior** da
construção e persistir no save. Hoje `previousOwnerSlot` só existe como variável
local no instante da captura (`TurnStateManager.Capture.cs:153`).

**Três predicados de aeronave no mesmo subsistema.** `GetAircraftType()`,
`UnitData.IsAircraft()` e `passengerData.domain == Domain.Air` (em
`CanMaterializePickupRendezvous`) respondem à mesma pergunta de jeitos
diferentes. O terceiro deixa de fora um hidroavião declarado como naval.

**O QueroCarona é recalculado por transportador.** Três transportadores no mesmo
turno perguntam aos mesmos passageiros; o cache morre junto com a decisão da
unidade (`QueroCaronaCalls: 15`, `CacheMisses: 15`, ~684 ms por transportador).
**Não verificado:** se `laterStopsBudget` carrega algo do transportador. Se não
carregar, dá para responder uma vez por passageiro por turno.

**`TryInferActionFromButton` é código morto** (zero chamadores) e tem a mesma
armadilha do painel: infere "consumo" só sob `MenuPanel.Options`. Se algum dia
voltar a ser chamado, quebra igual.

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
| 1 | `docs/relatorio_v7.2.1.md` | o fio mais recente; a seção 5 (onde eu errei) e a 6 (o que não terminou) |
| 2 | `docs/relatorio_v7.2.0.md` | a seção 4 (hipóteses erradas) e a 7 (o paradigma dos três consumidores) |
| 3 | `docs/relatorio_v7.1.0.md` | a separação, e a seção 9 (o erro que custou o dia) |
| 4 | `docs/manual/01_principios_e_vocabulario.md` | decide onde uma regra pode morar |
| 5 | `docs/arquitetura/acoes_transacionais.md` | obrigatório antes de ligar ferramenta a runtime |
| 6 | `docs/revisao_papeis.md` | matriz, traits e correções da taxonomia |
| 7 | `docs/AI Behavior/contrato_missoes.md` | vocabulário de missão. **Brainstorming** — as três missões descritas não existem no runtime |

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
| **sensor caro antes da rejeição barata** | `ResolvePassengerMeeting` sondava o embarque (0,73 ms, quatro consultas de tabuleiro sem cache) **antes** do dicionário de encontros, que rejeitava 93% dos pares. A ordem custava 9,8 s por decisão. Rejeição barata primeiro, sempre |
| **atribuir custo dentro do laço por dedução** | com 67 ms sobrando no laço, apostei na interpolação do `reason` montada para 12.195 pares. O contador mostrou outra coisa: era a **sonda irmã**, uma por LZ candidata. A string não custava nada perto disso. Instrumentar o trecho é mais barato que a aposta errada |
| **referência serializada vazia + auto-fiação escopada** | `btnConsumo: {fileID: 0}` sobrevivia porque a auto-fiação procurava dentro do `panelOptions`. Mover o botão de painel matou a fiação sem sintoma óbvio: ele continuava na tela e navegável, só não tinha ouvinte. Reference vazia + busca escopada = bomba-relógio no dia em que o objeto se move |
| **relógio de UI correndo atrás da cortina** | o prazo do Jornal arrancava dentro do `AdvanceTurn`, com a tela preta no ar. Quem sentava na cadeira já pegava o relatório vencido, e o auto-dismiss varria as linhas. Prazo de leitura só corre quando há alguém podendo ler |
| **duas verdades sobre o mesmo estado** | o ícone do hex lia catálogo ∪ ofertas runtime; o Jornal lia só as ofertas. A cidade mais vazia — a que não tem nem linha de oferta — sumia do relatório e acendia no mapa. Quando duas telas discordam, o fato tem que virar uma função só |
| **pergunta de um domínio feita a outro** | o `QueroCarona` mede necessidade de carona pelo envelope de **captura**. Perguntado sobre um caça, respondia "sem rota própria" — sempre, e sempre falso. Resposta plausível de pergunta inaplicável é pior que erro, porque não levanta suspeita |
| **limiar fixo onde devia ser banda** | `minimumMissionGain: 2f` para um caça de 9 MP: ele pagava o turno inteiro para ganhar meia rodada de voo. A doutrina já dizia "banda, não número de hex" — e mesmo assim aconteceu de novo, num arquivo que ninguém associava a alcance |
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

**Compilar vem antes de qualquer coisa.** Duas versões seguidas foram escritas
sem o Console: a `v7.2.0` compila mas não rodou, e a `v7.2.1` não foi nem
compilada. Cinco frentes de uma vez, nenhuma vista pelo compilador. Qualquer
plano que comece antes disso está construindo sobre suposição.

O critério da `v7.1.2` era de **saída**: as duas janelas descrevendo a mesma reta
com as mesmas palavras, e um contato novo avisando o jogador uma vez. As duas
coisas estão escritas; **nenhuma foi vista em jogo**.

O critério de **prova** da perna de percepção continua: o autor abrindo as duas
janelas lado a lado sem traduzir nada, e um radar movido duas vezes sobre os
mesmos inimigos tocando uma vez só.

E o da `v7.2.1` é de **honestidade do relatório**: o Jornal está pronto quando um
turno com duas cidades vazias mostrar as duas, quando ele abrir inteiro depois de
uma cortina demorada, e quando nenhum `[FilaCarona]` citar caça.

Depois disso, o degrau 0 tem uma linha que reabriu: **tirar o laço de hexágono de
dentro do `PodeDetectar`**. Enquanto ele for público lá, "PodeDetectar responde
só por unidades" é aspiracional, e o próximo consumidor que precisar de hex vai
achá-lo primeiro — foi assim que a tabela de flags cresceu da primeira vez.
