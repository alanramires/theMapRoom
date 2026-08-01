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

| família | ação | estado |
|---|---|---|
| **Fonte de renda** | `PodeCapturar` | ✅ |
| **Combate** | `PodeMirar` | ✅ |
| **Transporte** | `PodeEmbarcar`, `PodeDesembarcar` | ✅ |
| **Logística** | `PodeSuprir` | ✅ |
| **Estoque** | `PodeTransferir` | ✅ |
| **Sobrevivência** | `PodeFundir` | ✅ |
| **Mobilidade** | **`ApenasMover`** *(antes `PodeMover`)* — **não é um sensor, é uma ação disponível** | ✅ pela definição do contrato: não existe `PodeMoverSensor`, e não deve existir |

A distinção da última linha resolve o que parecia divergência: `ApenasMover` não
tem arquivo de sensor porque **não é um sensor**. É a ação que sobra quando
nenhuma outra é escolhida — e escolher ficar é uma decisão, não um resto.

---

## 5. Sensores aéreos e navais

Não são acessados pelo jogador. Governam **transição de domínio** e são chamados
pelos demais `PodeX`.

| domínio | sensores | estado |
|---|---|---|
| **Aéreos** | `PodeDecolar`, `PodeArremeter`, `PodePousar`, `PodeMudarDeAltitude` | ✅ (o arquivo é `PodeMudarAltitudeSensor`) |
| **Navais** | `PodeEmergir`, `PodeSubmergir`, `PodeSubmergirRapidamente` | ✅ |

⚠️ O cabeçalho do contrato ainda diz "6 `PodeX`"; a lista tem **7**. O sétimo é o
`PodeSubmergirRapidamente`, que ganhou linha própria nesta reescrita.

Dois comportamentos já fixados:

- **Pouso de emergência** chama `PodePousar` antes de destruir, e desliga os
  motores. ✅
- **`PodeDecolar` é sempre chamado** quando a unidade é selecionada, ou ativada
  por receber embarque. ❓ não conferido.

---

## 6. Sensores de busca e detecção de furtivos

Governam a caça e a detecção contra unidades **stealth**.

| sensor | o quê | estado |
|---|---|---|
| **`PodeEnxergar`** | revela **hexes**. Usa a **linha ascendente ou descendente** entre observador e alvo | ✅ o comportamento existe; ver a nota de arquivo abaixo |
| **`PodeDetectar`** | detecta **unidades** furtivas. É quem **possui** a linha de observação | ✅ arquivo próprio, com `PodeDetectarOption` |

**A linha.** Traça-se do observador ao alvo comparando a elevação (EV) de cada
célula do caminho contra a altura da linha naquele ponto. A linha **sobe ou
desce** conforme a diferença entre as duas pontas, e a serra bloqueia quando a EV
dela supera a altura da linha ali. Quatro tools em `Tools > FoW` mostram isso:

| ferramenta | pergunta que responde |
|---|---|
| **Pode Enxergar** | quais hexes **esta unidade** enxerga — com o traço da subida da linha, célula bloqueadora e célula que passou |
| **Hex Enxergado** | o inverso: quais unidades enxergam **este hex** |
| **Alguém me vê** | quem **me** detecta — a recíproca, do lado do furtivo |
| **Pode Detectar** | detecção de unidade, direto |

**Quem faz a conta é o `PodeDetectar`.** A janela do `PodeEnxergar` chama
`PodeDetectarSensor.TryGetObservationLineDebug`, e a de Hex Enxergado chama
`PodeDetectarSensor.CollectVisibleCells` "usando as regras do PodeEnxergar". Ou
seja: revelar hex e detectar unidade compartilham a **mesma** geometria; o que
muda é o que se pergunta no fim dela.

⚠️ **Duas divergências, e esta é a família menos assentada das três.**

1. O contrato diz "mais **3** sensores" e lista `PodeEnxergar` duas vezes. No
   catálogo completo de identificadores `Pode[A-Z]…` do projeto existem
   **dois** nesta família. Mas há **quatro ferramentas** em `Tools > FoW`, e duas
   delas — *Hex Enxergado* e *Alguém me vê* — são perguntas distintas, não vistas
   da mesma. É provável que o terceiro nome perdido seja uma delas.
2. **`PodeEnxergar` não tem arquivo de sensor.** Existe como
   `PodeEnxergarRuntime` / `PodeEnxergarRuntimeLogs` dentro do `MatchController`,
   e como janela de Editor — mas nunca foi extraído para
   `Assets/Scripts/Sensors/`. É o único `PodeX` do contrato nessa situação, e
   hoje ele empresta a matemática do vizinho.

Semântica já fixada do `PodeDetectar`: o olho significa que uma unidade **com
skill de ocultação** foi detectada — sem filtro de camada, intencionalmente.

---

## 7. Notas dos sensores do jogador

### PodeCapturar

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

### PodeMirar

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

### PodeEmbarcar

Requer **pontos de movimento sobrando** para pagar o custo do terreno **do
transportador** — esse custo *é* o custo do embarque.

- O transportador **limita** onde aceita embarque e que **tipo de vaga** oferece.
- **Não** entra multiplicador de autonomia.

### PodeDesembarcar

- O **transportador** deve estar em local válido segundo a ficha dele.
- O **transportador** também elege os locais válidos **para a carga**.
- O **passageiro** paga o custo de MP do desembarque, **sem** multiplicador de
  autonomia.

> Desembarque é sempre ação do **transportador**; embarque é sempre ação do
> **passageiro**. As duas fichas participam, mas o dono da ação não muda.

### PodeSuprir

A supridora **converte recursos em serviços** e presta em campo, no alcance da
ficha dela.

| regra | detalhe |
|---|---|
| alcance | range 1, apenas embarcados, ou combinação dos dois |
| camada | o serviço acontece **na camada do supridor**, por um custo |
| aeronaves | pousam e **arremetem** depois de supridas |
| **submersíveis** | **emergem, recebem, e NÃO mergulham de volta** |
| aproximação | o supridor tenta chegar à camada do atendido, se tiver condições |
| ação | consome a ação **do supridor**, não a do suprido |

✅ o alcance por modo existe (`Adjacent1Hex`, `SameHexOrEmbarked`) e é o que
sustenta o modo Hospital do transporte.

A aeronave **desce** (ou nivela) para receber, e **arremete** depois: volta ao
lugar dela. O submersível **sobe** para receber, e **não** mergulha de volta:
fica exposto.

Não é simetria — é assimetria, e é o ponto. O único que paga preço permanente
pelo suprimento é o furtivo, porque o que o serviço tira dele é justamente o que
o define.

### PodeTransferir

| regra | detalhe |
|---|---|
| classificação | **Hub** (trocam entre si) e **Receiver** (apenas recebem) |
| custo | **não tem** |
| camada | mesma camada do supridor |
| aeronaves de carga | pousam e **não** arremetem |

✅ `SupplierTier.Hub` / `Receiver` em `PodeTransferirSensor`.

Contraste que vale marcar: **suprir custa e arremete; transferir não custa e não
arremete.** Mesma geometria, economias opostas.

### PodeFundir

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

### ApenasMover

> Você segura a posição onde estiver. Às vezes a melhor ação é continuar onde
> está: segurar a linha, servir de **observador avançado**, etc.

---

## 8. A consequência para os papéis

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
| G1 | ~~`ApenasMover` é sensor~~ | ✅ **fechada** na 2ª reescrita: o contrato passou a dizer que é ação, não sensor. Código e doutrina concordam |
| G2 | as três categorias de combate são nomeadas | ⚠️ só existe `operationRangeMin`; a classificação vive espalhada em testes soltos |
| G3 | ~~são 6 sensores de sistema~~ | ✅ **fechada**: `PodeSubmergirRapidamente` entrou na lista. Falta só corrigir o "6" do cabeçalho para 7 |
| G4 | a família de detecção tem 3 sensores | ⚠️ tem **2** nomes `Pode*`, mas **4** ferramentas em `Tools > FoW`. O terceiro nome é provavelmente *Hex Enxergado* ou *Alguém me vê* |
| G5 | `PodeEnxergar` é sensor | ⚠️ **não tem arquivo**: vive no `MatchController` e na janela de Editor, e a conta da linha ele pega emprestada do `PodeDetectarSensor`. Único `PodeX` do contrato nessa situação |
| G6 | `PodeDecolar` é sempre chamado ao selecionar/ativar | ❓ |
| G7 | os dois redutores de −50% da captura | ❓ |
| G8 | ordem ≠ sensor | ⚠️ o Serviço do Comando **é** um sensor no código; a distinção só existe aqui |
| G9 | submersível emerge, recebe e não mergulha | ❓ regra nova nesta reescrita, não conferida |
