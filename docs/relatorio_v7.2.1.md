# v7.2.1 — A pergunta errada também responde

Nenhum dos defeitos deste dia apareceu como erro. Todos deram **resposta
plausível**, e foi por isso que sobreviveram: o caça dizia *"sem rota própria"*
com a naturalidade de quem sabe do que fala; o Jornal listava uma cidade vazia
com nome e coordenada; o botão do menu aparecia na tela e as setas chegavam nele.

O fio do dia é esse. Em cinco frentes diferentes, o conserto não foi calcular
melhor — foi **perguntar a coisa certa, a quem sabe responder, na ordem certa**.

---

## 1. O Jornal do Comandante

Três frentes no mesmo relatório, e elas não se tocam.

### 1.1 Duas verdades sobre "vazio"

O aviso de estoque trazia **uma linha por insumo**: três linhas idênticas do
mesmo depósito empurravam o resto do Jornal para fora da tela. Isso era só o
sintoma visível. O de baixo era pior — havia cidade zerada que **não aparecia**.

A varredura do Jornal lia as **ofertas runtime** da construção. O ícone que
acende no hex lê outra coisa: **catálogo ∪ ofertas runtime**, com uma regra
explícita em `ConstructionManager.RefreshSupplierStockAlerts` — *"catálogo sem
oferta runtime: só alerta o zerado"*.

É aí que a cidade some. Um prédio que **deveria** estocar munição mas não tem
nenhuma linha de oferta runtime para ela não é "sem munição" para o Jornal: é
ausência de assunto. O ícone acende, o relatório cala. E o caso é exatamente o da
cidade mais vazia de todas.

O conserto pôs o fato onde ele mora: `ConstructionManager.CollectDepletedSupplies`
responde *"o que está zerado aqui"* no mesmo universo do ícone. O Jornal só
redige. Se as duas telas discordarem de novo, é uma função só para corrigir.

### 1.2 A rolagem, e o título que não vai embora

O Jornal era o único painel-lista sem viewport: crescia sem teto e o `RectMask2D`
do painel cortava o excedente sem avisar. Agora ele segue o arranjo do Serviço do
Comando — título fixo **fora** da área rolável, viewport com máscara própria,
altura limitada a `min(conteúdo, 52% da tela, 620px)`.

Roda do mouse, arraste e setas do teclado rolam. Na roda usei só a **direção**:
o Input System entrega ±120 por clique e o legado ±1, e multiplicar o valor cru
mandaria a lista para o fim num clique só.

Quando sobra notícia fora da janela, o título diz quantas existem. Sem isso, o
corte da máscara parece o fim do relatório.

### 1.3 O relógio não corre atrás da cortina

O autor perguntou por que o Jornal do início do turno é diferente do aberto pelo
menu. **Não é** — é a mesma lista, guardada em `lastTurnStartAutonomyHelperLines`.
O que diferia era só quanto dele dava para ler.

Em hot seat, `AvancarTurno` cobre a tela **antes** de trocar o time, e o
relatório é montado **dentro** de `AdvanceTurn` — com a cortina preta já no ar e
o próximo jogador ainda nem sentado. O prazo de exibição (`Time.time + duração`)
arrancava ali. Quando o jogador apertava o botão, os segundos tinham vencido, e o
auto-dismiss — que só roda **depois** da barreira `IsGameplayInputBlocked` —
encontrava o relatório expirado e varria as linhas.

`HoldTurnStartBriefingClockWhileInputBlocked` rearma o prazo para a duração
inteira enquanto o input estiver bloqueado. Quem senta na cadeira recebe o
relatório do zero, não o resto dele. O quadro de ativação é rearmado junto, senão
a tecla que derruba a cortina seria lida como *"já li, pode fechar"*.

Vale para a cortina e para o load — os dois passam pelo mesmo bloqueio.

### 1.4 Uma notícia que faltava

`OCUPAÇÃO INIMIGA`, tier Atenção: unidade dele parada em cima de um prédio seu.
É o aviso que **antecede** a captura; assim que ela começa, `SOB CAPTURA` conta a
história melhor e este cala, para não render duas notícias do mesmo hex.

Aliado em cima não é ocupação, é guarnição — o filtro é por time, não por slot. E
é fog-honesto por `IsUnitVisibleForSlot`: se você não enxerga o ocupante, não há
notícia. Nunca a dedução de que "alguém deve estar lá".

---

## 2. O botão que aparecia e não fazia nada

O autor moveu o `Button_consumo` de `Panel_options` para `Panel_menu` no prefab —
ato de design — e o Jornal parou de abrir pelo menu.

A causa não estava no painel. `btnConsumo` **nunca esteve preenchido no
Inspector** (`{fileID: 0}` no prefab, sem override na cena). Quem resolvia isso
era a fiação automática, e ela é escopada ao painel:

```csharp
if (btnConsumo == null) btnConsumo = FindButtonByNames(panelOptions, "button_consumo", …);
```

Fora do `Panel_options`, a busca devolve null, `BindButton` não faz nada, e o
botão fica **sem nenhum ouvinte de clique**. O cruel é o sintoma: ele continua na
tela e as setas continuam chegando nele, porque a lista de navegação é montada
varrendo os filhos do painel, não pelo campo serializado. Aperta Enter,
`onClick?.Invoke()` não tem ninguém, nada acontece.

`FindMenuButtonByNames` tenta o painel de origem e, se não achar, varre o
`menuRoot` inteiro. Os três "Voltar" ficaram **de fora de propósito**: cada painel
tem o seu, e a busca larga traria o do vizinho.

O ganho real: mover botão de painel voltou a ser ato de design, sem religar nada.

### O que isso quase custou à IA

A IA encena esse menu para o jogador ver, e navega **por predicado**, não por
contagem — `while (!menu.IsRodadaButtonSelected && guard++ < 10)`. Um botão novo
na lista custa um passo a mais, e só.

Mas `IsRodadaButtonSelected` retorna false quando a referência é null. Se o
`btnRodada` tivesse sido movido em vez do Consumo, o `while` giraria as dez
voltas da guarda, abortaria com *"Botão Rodada não encontrado"* e **a IA não
passaria a vez** — o turno travaria, e o log culparia o botão em vez da fiação.
O fallback fecha essa porta também.

---

## 3. MelhorEmbarque: medir antes de otimizar

O APC #31 levava **11,9 segundos** para decidir um movimento, num frame só.

### O que a instrumentação disse

A linha de perf já apontava `melhorEmbarque: 11.580 ms` numa **única chamada**,
com os estágios aninhados somando 1,76 s. Sobravam ~9,8 s sem dono. Em vez de
deduzir, instrumentei: estágios por trecho (`transporterPaths`, `candidateCells`,
`passengerReach`, `lzLoop`, `resolveMeeting`, `longRangeMap`) e contadores de
pares, sondas, e distribuição de estado de rota.

O caso pequeno respondeu por todos. O caminhão de Suprimentos #24 — mesmo código,
2 passageiros, 92 pares — mostrou:

```
resolveMeeting: 67,2 ms / 92 pares = 0,73 ms por sonda
MelhorEmbarqueEmbarkProbeRejects: ausente (zero)
MelhorEmbarquePairsNoRoute: 86 de 92
```

As 92 sondas **acertaram**. E 86 dos 92 pares morreram como `NoRoute` — veredito
que veio do dicionário de encontros, depois, de graça. O sensor estava sendo pago
para descobrir "não" em 93% dos casos.

E o custo dele não é mistério: `TryGetEnterCellCost` faz **quatro consultas de
tabuleiro sem cache** por chamada — construção, estrutura, terreno, tile pintado.

### A inversão

`ResolvePassengerMeeting` passou a fazer o encontro **primeiro** (dicionário) e a
sonda **depois**. Onde não há encontro, o veredito é `NoCurrentRoute` de qualquer
jeito e a pontuação daquele par nem lê o custo de embarque — `routePenalty` é
10000 fixo. Mesmo resultado, sem a sonda.

Um caso quase se perdeu na inversão: quando o encontro **existe mas não cabe no
orçamento**, o longo alcance ainda é a última chance. Ele agora é materializado
em dois pontos — antes da sonda para quem não tinha encontro algum, depois da
cadeia de viabilidade para quem tinha —, o que mantém a semântica antiga exata.

### O resultado

| | antes | depois |
|---|---:|---:|
| frame da decisão do APC #31 | 11.808 ms | **3.308 ms** |
| `resolveMeeting` (#24) | 67,2 ms | **9,2 ms** |
| sondas de embarque (#24) | 92 | **11** (81 puladas) |

A conta fecha exata: 11 = 6 encontros viáveis + 5 inviáveis. Nenhuma sonda a mais
que o necessário.

---

## 4. O caça e o porta-aviões

O autor viu dois caças 10/10 embarcarem num porta-aviões e perguntou por quê.

Não foi reparo, e não foi "tinha plataforma por perto". Foi **rebasing**: a
plataforma aproximava a missão de 7 para 3 hexes, ganho 4 ≥ limiar 2.

### O limiar era um número, não uma banda

`minimumMissionGain: 2f`, cravado na chamada. Dois hexes fixos — para um caça de
9 MP. Ganhar 4 custava a rodada inteira (mover, embarcar, e decolar de novo
depois) numa aeronave que voa 9 por turno.

Agora o limiar é **um turno de voo da aeronave avaliada**. Os dois casos do log
passam a ser recusados (ganho 4 contra limiar 8; ganho 3 contra 9), e um avião
lento — ou um com pouco MP restante — volta a aceitar carona por ganhos pequenos,
que para ele não são pequenos. É a banda funcionando nos dois sentidos.

### O log mentia o motivo

O rebasing reaproveita a rotina de EVAC, e por isso um caça inteiro aparecia como
`[Capturador] embarca` seguido de `[Repair] EVAC`. `TryEmbarkFromHex` e
`TryBuildRepairEvacExtendedEmbarkAction` ganharam `logCategory`/`logVerb`, com
default igual ao comportamento antigo. Agora sai `[AirPlatform] REBASING`.

### A pergunta terrestre feita a quem voa

O achado que vale mais que os dois consertos: o porta-aviões **também** avaliava
esses caças, por outra régua e sem saber do resto.

O `MelhorEmbarque` do transportador pergunta a necessidade de carona pelo
`QueroCaronaService`, cuja única fonte de alcance é o envelope de **Captura**
(`Intent = Capture`, `SubStep = Terrestre`). Um caça nunca captura. Logo a
resposta era estruturalmente sempre a mesma, e sempre falsa:

```
pax=#96 … sem prédio capturável livre alcançável … SEM ROTA PRÓPRIA (só chega de carona)
[FilaCarona] #96 entra na fila no turno 1 — SEM ROTA PRÓPRIA (score=1500)
```

Dito de uma aeronave com 9 MP e tanque cheio, que voa o mapa inteiro — só não
*captura*. Com score 1500 ela entrava na fila do transportador **na frente de
quem estava de fato encalhado**.

O mesmo caça tinha, no mesmo turno, dois vereditos de carona calculados por dois
serviços diferentes, e nenhum sabia do outro.

`EvaluatePickupRideNeed` agora bifurca: aeronave responde só a
`EvaluateEmergencyOnly` — a única necessidade que o transportador enxerga
sozinho. Rebasing continua sendo decisão da aeronave, no turno dela, comparando a
plataforma com a **missão** dela, que o transportador não tem como saber.
Embarque é ação do passageiro; carona aérea não se planeja de fora.

O predicado é **estrutural**, não por altura: `GetAircraftType() != None`. Um caça
pousado continua aeronave — com `domain == Air` em runtime, o avião parado na
pista voltaria a receber a pergunta errada.

---

## 5. Onde eu errei, e a medição desmentiu

**Achei que fosse a string.** Com `lzLoop − resolveMeeting ≈ 67 ms` sobrando,
apostei na interpolação do `reason`, montada para os 12.195 pares. O contador
mostrou outra coisa: 91 células candidatas × 0,73 ms ≈ 64 ms — era a **sonda
irmã**, o `IsTransporterCellValidForEmbark`, uma por LZ. A string não custava
nada perto disso. Instrumentei o portão por LZ (`melhorEmbarque.lzGates`) em vez
de assumir de novo.

**Errei a previsão do ganho.** Estimei que a decisão do #31 cairia para 1,5–2,5 s;
o frame veio em 3,3 s. Como a linha `[AI Perf][Unit]` do #31 nunca apareceu nos
logs colados, ainda não sei o quanto desse frame é decisão e o quanto é preview,
log e GC.

---

## 6. O que não terminou

**A linha de perf do APC #31 nunca chegou.** Ela é impressa depois da execução do
batch e caiu sempre fora do trecho colado. Sem ela, o detalhe dos 3,3 s restantes
é estimativa.

**O `lzGates` está instrumentado e não medido.** A hipótese (a sonda por LZ
candidata domina o laço agora) ainda não foi confirmada em partida.

**A porta da "única recuperação compatível" continua aberta.** A chamada passa
`acceptOnlyRecovery: true`, e há uma segunda condição que aceita rebasing quando
a plataforma é a única recuperação no alcance e não afasta da missão. Numa região
sem pista amiga perto, os caças podem embarcar **mesmo com o limiar novo** — só
que agora o log dirá isso na cara. Essa cláusula não olha combustível: um caça
com 65/75 de autonomia não precisa de recuperação nenhuma.

**As varreduras de estado do Jornal ainda são fotografia.** Estoque, sob captura e
ocupação são calculados no início do turno e não recalculam quando o relatório é
reaberto pelo menu. Reabastecer uma cidade e reabrir o Jornal ainda mostra o
aviso velho. A separação evento/varredura resolve, e não foi feita.

**`OCUPAÇÃO INIMIGA` cobre prédio que É seu, não que ERA.** O caso do prédio que o
inimigo tomou e continua ocupando exige guardar o dono anterior da construção e
persistir no save — hoje `previousOwnerSlot` só existe como variável local no
instante da captura.

**Três predicados de aeronave no mesmo subsistema.** `GetAircraftType()`,
`UnitData.IsAircraft()` e `passengerData.domain == Domain.Air` (em
`CanMaterializePickupRendezvous`) respondem à mesma pergunta de jeitos
diferentes. O terceiro deixa de fora um hidroavião declarado como naval.

**O QueroCarona é recalculado por transportador.** Três transportadores no mesmo
turno perguntam aos mesmos passageiros, e o cache morre junto com a decisão da
unidade (`QueroCaronaCalls: 15`, `CacheMisses: 15`). Se a resposta for mesmo
transporte-independente — não verifiquei se `laterStopsBudget` carrega algo do
transportador —, dá para responder uma vez por passageiro por turno.

**Nada disto foi compilado nem jogado.** Todo o código deste dia é leitura
estática; o Console do Editor não foi aberto uma vez. Os números de performance
citados vêm de logs de execuções **anteriores** aos últimos consertos.

---

## Arquivos

**Jornal**
`MatchController.cs` · `ConstructionManager.cs` · `PanelHelperController.cs` ·
`TurnStateManager.HelperPanel.cs` · `TurnStateManager.ScannerPrompt.cs`

**Menu**
`BattleMapMenuRootController.cs` · `ReplayManager.cs` · `MenuRoot.prefab`

**Transporte**
`MelhorEmbarqueService.cs` · `AIController.TransportOperations.cs` ·
`AIController.Transportador.Evac.cs` · `AIController.Capturer.Embark.Transporter.cs`

**Aéreo**
`AIController.AirPlatform.cs` · `AIController.AirCombat.cs` ·
`AIController.Vigilancia.cs`
