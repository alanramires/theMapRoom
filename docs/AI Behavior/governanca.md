# Governança do Sistema

Contrato do autor. Este documento fica **acima** dos contratos de papel: o que
está aqui vale para toda unidade do jogo, tenha ela papel, plano ou nenhum dos
dois.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido — a busca não fecha a questão |

---

## 1. Upkeep

Sempre no **início de cada rodada**, três coisas acontecem, nesta ordem:

| # | etapa | o quê | estado |
|---|---|---|---|
| 1 | **Consumo de autonomia** | unidades com `autonomyData` marcado no upkeep deduzem autonomia **obrigatoriamente** | ✅ |
| 2 | **Pouso de emergência** | toda aeronave que ficou com **0** chama `PodePousar`; falhando, é destruída | ✅ |
| 3 | **Jornal do Comando** | resumo do turno para partidas assíncronas: o que aconteceu e o estado das aeronaves | ✅ |

O pouso de emergência **desliga os motores**: pousada assim não arremete depois
de ser suprida. É a exceção ao comportamento normal do `PodeSuprir`.

---

## 2. As cinco ordens

O jogador sempre tem à mão **5 ordens**, executáveis **a qualquer momento** do
turno dele. Ordem não é ação de unidade: não consome movimento nem passa pela
cadeia `PodeX`.

| ordem | o que faz | estado |
|---|---|---|
| **Serviço do Comando** | rotina de suprimento para unidades que **não agiram** e **não receberam** suprimento na rodada (ver `PodeSuprir`) | ✅ `ServicoDoComandoSensor` |
| **Dispensar Unidades** | destrói uma unidade. Útil ao alcançar o limite do tabuleiro, ou quando uma unidade está há muito tempo sem pickup | ✅ `SensorActionType.RemoveUnit` |
| **Comprar Unidades** | clique numa construção para acessá-la; se for produtora, aparecem mais opções. Construções controladas vendem unidades por um preço em `$` | ✅ `SensorActionType.Shopping` |
| **Passar a Vez** | encerra o turno mesmo sem ter agido com nenhuma unidade, com algumas ou com todas | ✅ |
| **Inspecionar** | clique numa unidade **aliada que já agiu** ou **inimiga** para ver a área de ameaça dela. Cada clique consecutivo revela mais informação. Vale para construções também | ✅ |

⚠️ Nota de nomenclatura: o Serviço do Comando é implementado **como sensor**
(`ServicoDoComandoSensor`) e Dispensar aparece como `RemoveUnit`. A distinção
"ordem ≠ sensor" existe só neste documento; o código não a nomeia.

---

## 3. Movimento — o que acontece antes da ação

> Qualquer unidade selecionada deve **obrigatoriamente mover e fazer uma ação em
> seguida**.

O "mover" tem duas formas, e ambas contam:

| forma | o quê |
|---|---|
| **Segurar Posição** | você fica no mesmo lugar |
| **Escolher um Hex** | sua unidade se move para o novo lugar |

E duas garantias que valem para as duas formas:

> **Enquanto você move, o tabuleiro não recalcula — ele espera a sua ação.**
> Você pode desfazer quantas vezes quiser.

✅ É exatamente o invariante transacional do `CLAUDE.md`: toda ação começa e
termina em `CursorState.Neutral`, e o que acontece no meio é **provisório e
cancelável** — não atualiza FoW, não revela unidade, não consome recurso, não
marca a unidade como agida. Ver `docs/arquitetura/acoes_transacionais.md`.

Este é o parágrafo do qual sai metade das regras do jogo. Vale lê-lo como
fundação, não como detalhe de interface.

---

## 4. As ações

> Dependendo de onde você estiver, **depois** de segurar posição ou escolher um
> hex, o jogo calcula as opções de ação disponíveis.

### 4.1 Fonte de renda — `PodeCapturar`

Requer a skill **"Captura Construções"**. ✅ `PodeCapturarSensor.cs:36` exige
`skill.canCaptureConstructions`.

Converte **HP em captura**. Dois redutores de −50%, que **compõem**:

| redutor | quando |
|---|---|
| papel | alguns papéis convertem a −50% |
| prédio | pré-requisitos não atendidos impõem −50% **extra** |

> Os dois juntos **não** dão 100%: é a **metade da metade** na velocidade de
> captura.

❓ os redutores não foram conferidos no código.

### 4.2 Combate — `PodeMirar`

Requer **`EmbarkedWeapons`**. Três categorias, derivadas do alcance mínimo:

| categoria | quando acontece | `rangeMin` | revide |
|---|---|---|---|
| **Corporal** | parado **ou** após movimento | `= 1` | **gera** |
| **À distância** | apenas parado | `> 1` | não gera |
| **Híbrido** | tem armas nos dois critérios: tenta a distância primeiro; não dando, vai ao corporal | — | conforme a arma usada |

A **mina naval** é `rangeMin = 0`, portanto também não gera revide.

⚠️ O código tem o campo (`WeaponData.operationRangeMin`, default 1) mas **não os
três nomes**. A classificação é doutrina; hoje vive espalhada em testes soltos
desse campo. É o mesmo `rangeMin ≥ 1` que a Hotzone usa para devolver `null` em
Combate + Terrestre.

### 4.3 Transporte — `PodeEmbarcar` e `PodeDesembarcar`

**`PodeEmbarcar`** requer **pontos de movimento sobrando** para pagar o custo do
terreno **do transportador** — esse custo *é* o custo do embarque.

- O transportador **limita** onde aceita embarque e que **tipo de vaga** oferece.
- **Não** entra multiplicador de autonomia.

**`PodeDesembarcar`:**

- O **transportador** deve estar em local válido segundo a ficha dele.
- O **transportador** também elege os locais válidos **para a carga**.
- O **passageiro** paga o custo de MP do desembarque, **sem** multiplicador de
  autonomia.

> Desembarque é sempre ação do **transportador**; embarque é sempre ação do
> **passageiro**. As duas fichas participam, mas o dono da ação não muda.

### 4.4 Logística — `PodeSuprir`

A supridora **converte recursos em serviços** e presta em campo, no alcance da
ficha dela.

| regra | detalhe |
|---|---|
| alcance | range 1, apenas embarcados, ou combinação dos dois |
| camada | o serviço acontece **na camada do supridor**, por um custo |
| aeronaves | **pousam** e **arremetem** depois de supridas |
| **submersíveis** | **emergem, recebem, e NÃO mergulham de volta** |
| aproximação | o supridor tenta chegar à camada do atendido, se tiver condições |
| ação | consome a ação **do supridor**, não a do suprido |

✅ o alcance por modo existe (`Adjacent1Hex`, `SameHexOrEmbarked`) e é o que
sustenta o modo Hospital do transporte.

A aeronave **desce** (ou nivela) para receber e **arremete** depois: volta ao
lugar dela. O submersível **sobe** para receber e **não** mergulha de volta: fica
exposto. Não é simetria, é assimetria — e é o ponto. O único que paga preço
permanente pelo suprimento é o furtivo, porque o que o serviço tira dele é
justamente o que o define.

### 4.5 Estoque — `PodeTransferir`

| regra | detalhe |
|---|---|
| classificação | **Hub** (trocam entre si) e **Receiver** (apenas recebem) |
| custo | **não tem** |
| camada | mesma camada do supridor |
| aeronaves de carga | pousam e **não** arremetem |

✅ `SupplierTier.Hub` / `Receiver` em `PodeTransferirSensor`.

Contraste que vale marcar: **suprir custa e arremete; transferir não custa e não
arremete.** Mesma geometria, economias opostas.

### 4.6 Sobrevivência — `PodeFundir`

> Para todos os efeitos é **exatamente igual ao embarque** — custo de terreno,
> etc.

- Apenas entre unidades **aliadas e idênticas**.
- A diferença: você "embarca" no candidato e é **absorvido** por ele, tornando-se
  uma **unidade nova**.

| grandeza | como combina |
|---|---|
| munição | média ponderada |
| autonomia | média ponderada |
| HP | **soma simples** |

✅ o envelope já trata Fusão como embarque para efeito de custo
(`ResolveFusionEnterCost`). ⚠️ Fusão **não tem banda Operational** — ver
`contrato_envelope_alcance.md`.

### 4.7 Mobilidade — `ApenasMover`

*(antes `PodeMover`)* — **não é um sensor, é uma ação disponível.**

> Você segura a posição onde estiver. Às vezes a melhor ação é continuar onde
> está: segurar a linha, servir de **observador avançado**, etc.

✅ Pela definição do contrato: não existe `PodeMoverSensor`, e não deve existir.
É a ação que sobra quando nenhuma outra é escolhida — e escolher ficar é uma
decisão, não um resto.

---

## 5. Sensores aéreos e navais

O sistema usa **7** `PodeX` que **não** são acessados pelo jogador. Governam
**transição de domínio** e são chamados pelos demais.

### Aéreos

| sensor | o quê | estado |
|---|---|---|
| **`PodeDecolar`** | verifica condições de decolar até a altitude desejada — ou **1 casa** em pistas improvisadas | ✅ |
| **`PodeArremeter`** | faz a aeronave pousar, algo acontece (ser suprida, p. ex.), e **ela já chama o `PodeDecolar`** | ✅ confirmado: `PodeArremeterSensor` chama `PodeDecolarSensor.Evaluate` e loga *"decolagem final validada por PodeDecolar"* |
| **`PodePousar`** | verifica se há as skills necessárias para pouso em cada tipo de local: VTOL, SVTOL, pista | ❓ os nomes das skills não foram localizados |
| **`PodeMudarDeAltitude`** | reposiciona a aeronave entre altitudes, geralmente durante o suprir | ✅ (`PodeMudarAltitudeSensor`) |

`PodeArremeter` é o único que **compõe** dois outros: é pouso-mais-decolagem num
gesto só. Por isso o pouso de emergência precisa desligar o motor — senão a
aeronave sem combustível arremeteria de volta ao ar assim que fosse suprida.

### Navais

| sensor | o quê | estado |
|---|---|---|
| **`PodeEmergir`** | verifica se o submarino pode subir à superfície | ✅ |
| **`PodeSubmergir`** | verifica se pode voltar a submerso. **Submarino atingido ou que disparou fica trancado** e não pode submergir | ✅ existe o mecanismo (`IsLayerChangeBlockedByForcedLock`); ❓ não conferi que atingir/disparar são os gatilhos |
| **`PodeSubmergirRapidamente`** | a unidade **termina submersa** tendo começado emersa, mas precisa verificar as condições para isso | ✅ |

O lock do `PodeSubmergir` é a peça que dá custo ao tiro do submarino: atirar
**revela e prende** na superfície. É a mesma economia da §4.4 — o furtivo é o
único que paga com a própria natureza.

> **`PodeDecolar` é sempre chamado** quando a unidade é selecionada, ou ativada
> por receber embarque. ❓ não conferido.

---

## 6. Sensores de busca e detecção de furtivos

Governam a caça e a detecção contra unidades **stealth**.

### `PodeEnxergar` — libera tiles

Libera tiles no tabuleiro. **Hoje o padrão é a curva descendente ou nivelada.**

> Hex liberado **não** necessariamente revela o que há lá: isso depende do
> alcance de visão e das skills de detecção que a unidade tenha — ou não tenha.

Essa separação é a regra inteira em uma frase: **terreno e ocupante são duas
perguntas diferentes.** É o mesmo motivo pelo qual construção com `visão = N`
revela terreno no raio N mas só spotta unidade no raio 0 — prédio não é
observador.

**Hex Enxergado** é uma **função**, não um sensor: parte do hex para quem o vê.

### `PodeDetectar` — encontra o furtivo

Unidades com **visão especializada** procuram por unidades com a skill oposta
correspondente — caça submarina procura sub ops; detector de stealth procura ar
stealth. Encontrando, a unidade **aparece**.

**Alguém me vê** é o `PodeDetectar` ao contrário.

Semântica já fixada: o olho significa que uma unidade **com skill de ocultação**
foi detectada — sem filtro de camada, intencionalmente.

### Estado

⚠️ **O contrato anuncia "mais 3 sensores" e descreve 2** — `PodeEnxergar` e
`PodeDetectar`. Os outros dois nomes que aparecem, *Hex Enxergado* e *Alguém me
vê*, o próprio texto define como **função** e como **inverso**, não como sensores
próprios. Quatro ferramentas em `Tools > FoW`, duas perguntas de verdade.

⚠️ **`PodeEnxergar` não tem arquivo de sensor.** Existe como
`PodeEnxergarRuntime` / `PodeEnxergarRuntimeLogs` dentro do `MatchController` e
como janela de Editor — nunca foi extraído para `Assets/Scripts/Sensors/`. Pior:
a conta da linha ele **pega emprestada do vizinho** — a janela do `PodeEnxergar`
chama `PodeDetectarSensor.TryGetObservationLineDebug`, e a de *Hex Enxergado*
chama `PodeDetectarSensor.CollectVisibleCells` embora a própria ajuda dela diga
"usando as regras do PodeEnxergar".

Ou seja: liberar tile e detectar unidade **compartilham a geometria** e estão
implementados num arquivo só, com o nome do outro.

---

## 7. A consequência para os papéis

Esta seção não é do contrato; é o que sai dele.

Se **toda** unidade lê **estas** ações, então papel não é um conjunto de
sensores. É uma **consulta diferente sobre os mesmos sensores**:

```text
papel  =  intenção  ×  subetapa  ×  banda      (sobre o mesmo PodeX)
```

Que é exatamente a assinatura do `UnitReachEnvelopeService`. O envelope não foi
inventado para o transporte nem para a artilharia: é a forma geral de "perguntar
como um papel".

**Consequência direta:** arquivo de papel não deveria conter lógica de alcance
nenhuma. Só **política** — prioridade, recusa, desempate, quando desistir. Todo
cálculo de "até onde" pertence ao serviço.

É por isso que consumir a Hotzone **encolhe** os arquivos de papel. Não é
otimização: é o papel voltando a ser só o que ele é.

As relações de governo **entre** papéis estão em
`docs/AI Behavior/governanca_entre_papeis.md`.

---

## Pendências

| # | contrato | código hoje |
|---|---|---|
| G1 | ~~`ApenasMover` é sensor~~ | ✅ **fechada**: o contrato diz que é ação, não sensor. Código e doutrina concordam |
| G2 | as três categorias de combate são nomeadas | ⚠️ só existe `operationRangeMin`; a classificação vive espalhada em testes soltos |
| G3 | ~~são 6 sensores de sistema~~ | ✅ **fechada**: são 7, e o cabeçalho já diz 7 |
| G4 | a família de detecção tem 3 sensores | ⚠️ tem **2**. *Hex Enxergado* e *Alguém me vê* são função e inverso, pelo próprio texto |
| G5 | `PodeEnxergar` é sensor | ⚠️ **não tem arquivo** e empresta a matemática do `PodeDetectarSensor` |
| G6 | `PodeDecolar` é sempre chamado ao selecionar/ativar | ❓ |
| G7 | os dois redutores de −50% da captura | ❓ |
| G8 | ordem ≠ sensor | ⚠️ o Serviço do Comando **é** um sensor no código |
| G9 | submersível emerge, recebe e não mergulha | ❓ |
| G10 | submarino atingido ou que disparou não submerge | ❓ o lock existe (`IsLayerChangeBlockedByForcedLock`); os gatilhos não foram conferidos |
| G11 | `PodePousar` distingue VTOL / SVTOL / pista | ❓ não localizei as skills por esses nomes |
