# Transporte — contrato

Doutrina definida pelo autor. Onde o código divergir dela, o código está errado.

> **Função:** alavancar unidades para que cheguem mais rápido aos seus destinos.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido — a busca não fecha a questão |

**Rogue/Rebelde vira Rogue.** Este documento já fala em rogue; onde o código
ainda disser rebelde, ver `docs/refactor/ai_sem_plano.md`.

---

## 0. Notação

| termo | significado |
|---|---|
| **Courier** | transportador **com carga**, fazendo entregas |
| **Pickup** | transportador **vazio** |

Não existe "parcialmente vazio". ✅ O código já se comporta assim: o scanner de
pickup (`Shuttle`) só roda vazio, e encher a vaga livre no caminho não existe
(`BuildAttempts` retorna cedo com carga).

---

## 1. Atribuição e planos

A IA **pode** atribuir transportadores a planos, mas **o ideal é que todos sejam
rogues**. O transportador atribuído a um plano usa isso de duas formas:

- gera uma **promessa de entrega**; ou
- dá **preferência**, quando vazio, a procurar unidades recém-compradas
  atribuídas àquele setor — para que cheguem ao destino mais rápido.

Ou seja: a atribuição influencia a **escolha de pickup**, não obriga rota.

---

## 2. Comportamento — a esteira

Um transportador pode prometer buscar unidades isoladas, mas **a promessa não
impede outros de embarcar no meio do caminho**.

> O passageiro mais antigo **assume o volante** e leva o transportador até onde
> quer ir. O carona (2º passageiro) pode tentar desembarcar junto se estiver no
> raio dele. O transportador procura o melhor envelope para ambos, mas a
> prioridade continua sendo o principal.

| regra | estado |
|---|---|
| quem embarca primeiro dita a rota (FIFO por turno de embarque) | ✅ `ResolvePrimaryPassenger` |
| larga esse passageiro no Tactical do objetivo **dele** | ✅ |
| o próximo da fila vira referência | ✅ — emergente: o principal é recalculado a cada decisão a partir de quem ainda está a bordo |
| promessa não bloqueia embarque de terceiros | ✅ |
| promessa persistente no `AIDesignatedMission` do transportador | ✅ `AIController.RidePromise.cs` |
| com carga, o transportador herda o **destino da carga** no AI Plan Runtime | ⚠️ hoje a promessa guarda o **alvo do resgate** (`AIDesignatedMissionTargetUnitInstanceId`) e é **limpa** ao embarcar, em vez de virar o destino da carga |
| encher a vaga livre no caminho | ❌ |
| promessa reserva **uma vaga**, não o veículo | ❌ |

⚠️ **Cascata registrada:** implementar "encher a vaga livre" sozinho **piora** a
fome do passageiro esquecido — o veículo fica ocupado mais tempo. Só entra junto
com a reserva de vaga ou com a pressão de compra.

---

## 3. Combate

**Transportador armado só entra em combate no modo Pickup (vazio).** Pode apoiar
a infantaria a tomar um setor, lutando no **Tático do capitão**, e depois volta
às suas tarefas.

Vale inclusive para o naval: **o transporte marítimo só atira vazio**, porque uma
rodada de combate significa que as aeronaves embarcadas não decolam — e caças e
bombardeiros são a defesa principal de uma armada.

Ele **consulta** a perna de combate de Assault ou Fire Support conforme foi
projetado, com **prioridade menor**.

| regra | estado |
|---|---|
| courier não ataca | ✅ **de facto** — `TryFindTransportCourierAttack` existe mas **não tem chamador**: é código morto a apagar |
| consulta a perna de Assault/Fire Support e volta à agenda | ❌ o roteador consulta o transporte **antes** dos papéis de combate e não volta |
| lutar no Tático do capitão | ❌ |

⚠️ **Conflito com o rascunho anterior deste mesmo documento.** A §"Quando o
Porta-Aviões atira" dizia que **com carga aérea** ele ainda atira, mas só contra
bombardeiro ("deles não dá para fugir"). Esta seção diz que **vazio é condição**.
A regra nova é mais restritiva e, sendo posterior, prevalece — mas o rascunho do
porta-aviões ficou abaixo, marcado, porque a justificativa dele (bombardeiro é
ameaça inescapável) não foi retirada, só sobreposta.

---

## 4. Embarque

Transportadores podem **embarcar em outros transportadores** — navios de
transporte, trens — para estender alcance poupando combustível.

> Um APC carregando um soldado que quer ir além-mar embarca no navio junto com
> ele; ou embarca num trem, e o trem assume a maior parte da viagem, liberando os
> dois no destino. Um APC com um soldado pode embarcar num Chinook, que leva a
> composição inteira.

Mas é **mais comum** ver o transportador usando o próprio combustível do que
aceitar uma carona aninhada.

| regra | estado |
|---|---|
| o sensor permite transportador como passageiro | ❓ `PodeEmbarcarSensor` valida o **transportador de destino**; não localizei bloqueio do lado do passageiro, mas também não confirmei que ele passa |
| a IA procura carona aninhada | ❌ nenhuma política busca isso |

---

## 5. Atração dos "quero carona"

Cada papel tem a sua atração — em geral quando o objetivo está **além do raio
Operacional** da unidade. Ela ergue a mão e **começa a contar o tempo**.

```text
espera ↑  →  pressão para atrair transportador rogue ↑
pressão alta  →  pressão para COMPRAR mais transportador ↑
```

Abertura comum: comprar Chinooks e infantaria na compra inicial, para os
Chinooks já começarem a levar soldados a destinos longos e a pressão nunca
acumular.

| regra | estado |
|---|---|
| tempo de espera vira urgência crescente, com teto | ✅ `AIController.RideWait.cs` |
| transportador que não alcança o passageiro não promete | ✅ |
| espera vira **pressão de compra** de mais transporte | ❌ |

---

## 6. Desembarque

### 6.1 A área de largada

Não é raio em hexes a partir do veículo. É **projeção invertida**, a partir do
passageiro:

> Teleporta a unidade para cima do objetivo, calcula o **Tactical dela** dali, e
> essa é a área.

A largada é boa quando o passageiro, no turno seguinte, fecha o objetivo com o
próprio movimento. Obus de 2 MP e infantaria de 3 MP não aceitam a mesma
largada. "0" é válido — em cima do alvo é o melhor caso, não exceção.

⚠️ Hoje é constante fixa e propriedade do **veículo**:
`TransportDropOffRange = 4`, `FireSupportDropOffRange = 3`, `AirDropOffRange = 2`.

### 6.2 As combinações

| | **sem atribuição** | **com atribuição** |
|---|---|---|
| **passageiro rogue** | alvo = capturável livre mais próximo | alvo = a coordenada da atribuição |
| **passageiro com plano** | alvo = o RepCell / capturável do setor dele | alvo = a coordenada da atribuição (pode ser outro prédio do plano, não só o RepCell) |

Nas quatro, o mecanismo depois é o mesmo: **Melhor LZ de Desembarque** para achar
onde largar. A atribuição muda a **âncora**, não o mecanismo. Espera-se que a IA
que atribui coordenadas saiba organizar isso — o desembarque não conserta
atribuição ruim, só a executa.

### 6.3 Tático ou Operacional

O transportador pode largar no **Tactical** ou no **Operational** do passageiro
em relação a onde ele quer ir. Verde = fecha no mesmo turno; azul = fecha no
turno seguinte.

**Dois passageiros:** tenta o principal primeiro (Tactical **ou** Operational) e
o carona **apenas no Tactical**, se houver envelope que atenda ambos.

| regra | estado |
|---|---|
| principal aceita Tactical ou Operational | ✅ dois passes de avaliação |
| carona só entra se a LZ atender ambos | ✅ `SearchMatching` maximiza entregues, depois minimiza rota |
| **carona limitado ao Tactical** | ❌ regra nova — hoje `BuildPassengerRouteLimits` aplica o mesmo teto a todos; o tier é do **passe**, não do papel do passageiro |

O que hoje separa principal de carona é outra coisa:
`ApplyOperationalDisembarkCapacity` — dois passageiros mirando o **mesmo** alvo,
e o alvo não estando sob pressão confirmada, o não-principal fica a bordo.

### 6.4 Casos especiais

| caso | regra | estado |
|---|---|---|
| **caças prontos** (transportador `isMaritime`, passageiro `isAircraft`) | **não** chama o Melhor LZ. Segue a rotina de reposicionamento e a aeronave decola em qualquer hex, de preferência na direção de onde foi designada | ❌ hoje `HasEmbarkedAircraft` apenas **alarga** o conjunto de LZs (devolve `null` e usa todos os caminhos), em vez de pular a consulta |
| **carga de superfície** (`isMaritime` + passageiro regular) | LZ normal | ✅ |
| **sem hex válido no Tactical** | tenta o Operational; sem isso também, **recusa a carona** | ❓ o `QueroCaronaService` tem o conceito (`isStranded`), mas a recusa por ausência de LZ não foi conferida |

> **Montanha.** Cidade na montanha herda as regras de montanha: sem infraestrutura
> de embarque/desembarque. Como desembarcar é sempre 1 MP e a cidade cobra 2 MP,
> ninguém embarca nem desembarca em montanha. *"Vai ficar assim por enquanto."*
> Larga a carga e ela vai a pé, ou usa estruturas na montanha.

### 6.5 Transporte não pousa em prédio capturável

O veículo **nunca** larga âncora em cima de um capturável: ocupa a célula que o
capturador precisa e trava a captura que ele veio viabilizar. Sem alternativa,
**sobe na iniciativa** para sair de lá primeiro na rodada seguinte.

❌ Nenhuma das duas existe. A iniciativa tem a regra espelhada só para o outro
lado (grupo 0 inclui *"blocker sobre o objetivo de outro capturador"*), e o teste
exige `CanSatisfy(targetData, UnitRole.Capturador)` — então um transportador
parado em cima do prédio **não é reconhecido como bloqueador**.

---

## 7. Transportador sendo transportado

O transportador aninhado que carrega transportador **lê os destinos das suas
cargas**, como faria transportando qualquer outra unidade. ❓ não conferido.

---

## 8. EVAC

Levam a carga para a **retaguarda**, numa construção aliada controlada — ou o
**HQ**, caso a retaguarda esteja impossível.

Unidades `isMaritime` seguem a rotina delas: são capazes de prestar socorro aos
próprios embarcados (têm `isLogistic` e o Serviço do Comando as aciona).

| regra | estado |
|---|---|
| destino é construção aliada de reparo | ✅ `FindRepairConstruction` em `AIController.Transportador.Evac.cs` |
| HQ como último recurso | ❓ |
| marítimo com `isLogistic` não faz EVAC, trata a bordo | ✅ é a mesma regra do modo Hospital (§11) |

---

## 9. Capturar

Apenas se existir a skill **"captura construções"**. É raro e deve ter
**prioridade baixa**.

✅ o sensor já exige `skill.canCaptureConstructions` (`PodeCapturarSensor.cs:36`).
❓ a prioridade baixa dentro do transporte não foi conferida.

---

## 10. Mobilidade e iniciativa

**Não ligam para flanco, vanguarda e retaguarda**, mesmo que isso os coloque em
perigo. A função é ser a alavanca, não a cautela.

**Iniciativa: média-alta** — abaixo de Fire Support e Assault. Levar tropa e
desembarcar o quanto antes.

⚠️ A iniciativa hoje **não ordena por papel**. Os grupos são situacionais e o
transportador aparece em dois deles: grupo 0 (transportador atribuído com
passageiro formal ainda não agido) e grupo 2 (rogue vazio com candidato de
pickup no alcance). Não existe "abaixo de Fire Support e Assault".

---

## 11. Leitura de FoW

> A IA pode não saber onde estão unidades e hexes revelados, mas **sempre sabe
> onde estão todas as construções**. Caso contrário o jogo vira um eterno
> explora-voa-explora-voa. É o **único vazamento autorizado** do jogo, e é para
> os transportadores.

✅ de facto: a resolução de alvo lê `ConstructionManager.AllActive` direto, sem
filtro de visibilidade.

O transportador **não tem opção de desembarque** se voar para célula em FoW
fechado; se voar para *explored* ou *revealed*, segue a política de
`unitData > transport > allow disembark when...`.

✅ a LZ do transportador já exige visível-ou-explorado
(`allowTransporterCell = IsConfirmedVisibleOrExploredCellForAI`). ❓ o
chaveamento pela política da ficha não foi conferido.

---

## 12. Fusão

**Transportadores não fundem.** Mesmo muito avariado, um transportador ainda
transporta.

> Com 2 Chinooks eu levo 4 infantarias; um Chinook fundido leva 2, porque vira
> uma unidade nova.

❓ O autor registra que "já existe um bloqueio que impede transportadores courier
de fundirem". Procurei por `isTransporter` cruzado com merge/fusão em
`Assets/Scripts/Match/AI` e não localizei — o que **não prova ausência**: pode
estar no sensor de fusão ou no `TurnStateManager.Merge`. A conferir.

---

## 13. Reparos

Qualquer construção **capturada que aceite pouso** serve. Unidades com carga
**não desembarcam a carga**: na construção ambos serão tratados (regra do Serviço
do Comando). ❓ não conferido.

---

## 14. Logística

Unidades com `isLogistic` que sejam transportadoras **suprem os próprios
embarcados preferencialmente**. Se o transportador só atende o adjacente, segue a
rotina normal dele.

✅ é o **modo Hospital** (`AIController.Transportador.Hospital.cs`):
`serviceRange = SameHexOrEmbarked` nutre a bordo; `Adjacent1Hex` estruturalmente
não consegue e continua desembarcando.

---

## 15. Transferência

Unidades **hub** retornam a construções, navios de carga, estações de trem ou 18w
para buscar suprimento quando acabam.

❌ para a IA: a cadeia `PodeTransferir` existe (tiers Hub/Receiver, domínio de
operação, baldeação navio↔caminhão), mas a IA ainda não a opera.

---

## 16. O Porta-Aviões é deste papel

Papel primário **Transportador**, com três pernas que ele **consulta**:

| perna | o quê |
|---|---|
| Fire Support | duas armas antiaéreas de longo alcance — tenta o tiro, volta à agenda |
| Logística | suporte |
| Transferência | é **Hub**: recebe do porto naval e repassa ao avião-tanque |

Segue a agenda courier/pickup deste documento. Não herda retaguarda, iniciativa
alta nem recusa de embarque do `FireSupport.md`: aquilo é de quem tem Fire
Support como papel primário.

### Rascunho anterior — sobreposto pela §3

> Mantido porque a justificativa não foi retirada, só sobreposta pela regra
> "só atira vazio".

| estado | comportamento |
|---|---|
| **vazio** | atira contra aeronaves no raio Tático |
| **com carga aérea** | atira **apenas** contra bombardeiro, porque dele não dá para fugir |
| **com carga aérea, sob caças** | ergue os caças — reconhecidamente tarde demais |

Com o convés cheio, gastar o tiro em caça é desperdício (caça se evita
manobrando); bombardeiro é ameaça inescapável. E a resposta contra caça não é a
arma do navio, é o próprio convés. "Erguer os caças tarde demais" admite que a
decisão de decolar deveria acontecer ao **detectar** o caça, não ao ser atacado.

---

## Pendências

| # | contrato | código hoje |
|---|---|---|
| T1 | a atribuição vale para todo passageiro | ⚠️ lida **só** no ramo rebelde — um call site, em `AIController.MelhorDesembarque.cs:1242`. Some no refactor de `ai_sem_plano.md` |
| T2 | rogue mira o capturável livre mais próximo | ⚠️ rogue de IA **com** QG ainda usa `TryResolveRogueCorridorCaptureTarget` — o funil do QG, doutrina derrubada na v6.1.2/6.1.3 e viva no transporte |
| T3 | passageiro com plano mira o capturável do setor | ⚠️ cai no `RepresentativeCell` quando não acha (`Courier.Passengers.cs:228`) **contra o comentário logo acima**: em setor já capturado o RepCell é a célula do próprio caminhão, e sai desembarque de distância zero |
| T4 | carona limitado ao Tactical | ❌ |
| T5 | o Melhor LZ consome a hotzone | ❌ hoje varre LZs por `CalcularCaminhosValidos` + `PodeDesembarcarSensor`. A **forma** já é a certa; a fonte de alcance é que não é o envelope |
| T6 | área de largada é banda do passageiro | ⚠️ constantes fixas do veículo (4 / 3 / 2) |
| T7 | courier não ataca | ✅ de facto — mas `AIController.Transportador.Courier.Attack.cs` é **código morto**, sem chamador. Apagar |
| T8 | consulta a perna de Assault/Fire Support com prioridade menor | ❌ o roteador consulta transporte antes do combate e não volta |
| T9 | naval só atira vazio | ❌ e **conflita** com o rascunho do porta-aviões, mantido na §16 |
| T10 | caças decolam sem Melhor LZ | ❌ `HasEmbarkedAircraft` alarga a LZ em vez de pular a consulta |
| T11 | com carga, herda o destino da carga no AI Plan Runtime | ⚠️ a promessa guarda o alvo do **resgate** e é limpa ao embarcar |
| T12 | não pousa em capturável / sobe na iniciativa | ❌ nenhuma das duas |
| T13 | iniciativa média-alta, abaixo de FS e Assault | ⚠️ não existe ordenação por papel; os grupos são situacionais |
| T14 | espera vira pressão de compra | ❌ |
| T15 | carona aninhada (APC em navio/trem/Chinook) | ❌ nenhuma política busca isso |
| T16 | transportadores não fundem | ❓ bloqueio não localizado — a conferir no sensor de fusão |
| T17 | hub busca suprimento | ❌ cadeia existe, IA não opera |

### Agrupadas por quem resolve

A tabela acima lista; esta agrupa. O critério não é severidade, é **de quem é o
trabalho** — porque metade destas pendências não é trabalho próprio.

| grupo | itens | quem resolve |
|---|---|---|
| **Já tem dono** | T1, T2 | o refactor de `docs/refactor/ai_sem_plano.md`. Somem junto com o funil do QG; não abrir frente própria |
| **Migração para o envelope** | T5, T6, T10 | um bloco só. Área de largada, fonte de alcance e dispensa de LZ para aeronave são a mesma mudança vista de três ângulos |
| **Independentes e pequenas** | T3, T7 | T7 é apagar arquivo morto. T3 é um `else` que contradiz o comentário acima dele — e **provavelmente já acontece em jogo sem ninguém notar** |
| **Precisam de decisão antes de código** | T8, T9, T13 | a ordem do roteador (transporte antes ou depois do combate), qual regra do porta-aviões sobrevive, e se iniciativa passa a ordenar por papel. Nada disso se decide lendo código |
| **Pergunta em aberto** | T16 | não é pendência até se confirmar que falta. Pode já existir onde não procurei |
| **Doutrina nova, sem base** | T4, T11, T12, T14, T15, T17 | trabalho de verdade, do zero |
