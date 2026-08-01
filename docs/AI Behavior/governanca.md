# Governança — o que existe acima de todos os papéis

Contrato do autor. Este documento fica **acima** dos contratos de papel: o que
está aqui vale para toda unidade do jogo, tenha ela papel, plano ou nenhum dos
dois.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido — a busca não fecha a questão |

> O jogador sempre tem à mão **2 ordens** e **diversos sensores**.

---

## 1. As duas ordens

Ordem não é ação de unidade: não consome movimento nem passa pela cadeia
`PodeX`. É o jogador agindo sobre o tabuleiro.

| ordem | o que faz | estado |
|---|---|---|
| **Serviço do Comando** | rotina de suprimento para unidades que **não agiram** e **não receberam suprimento** na rodada | ✅ `ServicoDoComandoSensor` |
| **Dispensar Unidades** | destrói uma unidade. Útil ao alcançar o limite do tabuleiro, ou quando uma unidade está há muito tempo sem pickup | ✅ `SensorActionType.RemoveUnit` |

⚠️ Nota de nomenclatura: o Serviço do Comando é implementado **como sensor**
(`ServicoDoComandoSensor`), e Dispensar aparece no código como `RemoveUnit`.
Nenhum dos dois usa a palavra "ordem". A distinção conceitual deste documento não
tem contraparte no código.

---

## 2. Os sensores do jogador

> Qualquer unidade no jogo pode **mover + agir**. As ações são governadas pelos
> `PodeX` abaixo.

| família | sensor | estado |
|---|---|---|
| **Fonte de renda** | `PodeCapturar` | ✅ |
| **Combate** | `PodeMirar` | ✅ |
| **Transporte** | `PodeEmbarcar`, `PodeDesembarcar` | ✅ |
| **Logística** | `PodeSuprir` | ✅ |
| **Estoque** | `PodeTransferir` | ✅ |
| **Sobrevivência** | `PodeFundir` | ✅ |
| **Mobilidade** | `ApenasMover` *(`PodeMover`, nome antigo descontinuado)* | ⚠️ não é um sensor: movimento é `UnitMovementPathRules`. Não existe arquivo `PodeMoverSensor` nem `ApenasMoverSensor` |

### O que a lista não cobre

**`PodeDetectar` existe e não está em nenhuma família.** É um sensor de verdade
(`PodeDetectarSensor`), com `PodeDetectarOption`, e governa quem enxerga quem.
Não é ação do jogador — mas também não é transição de domínio, então não cabe na
lista da §3. Fica registrado como buraco na taxonomia, a resolver.

---

## 3. Os sensores do sistema

> Não são acessados pelo jogador. Governam **transição de domínios** e são
> chamados pelos demais `PodeX`.

| domínio | sensor | estado |
|---|---|---|
| **Aéreos** | `PodeDecolar`, `PodeArremeter`, `PodePousar`, `PodeMudarDeAltitude` | ✅ (`PodeMudarAltitudeSensor`) |
| **Navais** | `PodeEmergir`, `PodeSubmergir` | ✅ |

⚠️ São **7**, não 6: existe também `PodeSubmergirRapidamenteSensor` (mergulho
rápido), separado do `PodeSubmergirSensor`.

Dois comportamentos já fixados:

- **Pouso de emergência** testa `PodePousar` **antes** de destruir a aeronave, e
  desliga os motores dela: pousada não arremete depois de ser suprida. ✅
- **`PodeDecolar` é sempre chamado** quando a unidade é selecionada, ou ativada
  por receber embarque. ❓ não conferido.

> Cada um será detalhado individualmente depois.

---

## 4. Os sensores, um a um

### PodeCapturar

Requer a skill **"Captura Construções"**. ✅ `PodeCapturarSensor.cs:36` exige
`skill.canCaptureConstructions`.

Converte **HP em captura**. Dois redutores de −50%:

| redutor | quando |
|---|---|
| papel | alguns papéis convertem a −50% |
| prédio | pré-requisitos não atendidos impõem −50% na entrada |

❓ os dois redutores não foram conferidos.

### PodeMirar

Requer **`EmbarkedWeapons`**. Três categorias, derivadas do alcance mínimo da
arma:

| categoria | quando acontece | `rangeMin` | revide |
|---|---|---|---|
| **Combate corporal** | parado **ou** após movimento | `= 1` | **gera** |
| **Combate à distância** | apenas parado | `> 1` | não gera |
| **Combate híbrido** | tem armas nos dois critérios: tenta a distância primeiro, e se não conseguir vai para o corporal | — | conforme a arma usada |

A **mina naval** é `rangeMin = 0`, portanto também não gera revide.

⚠️ O código tem o campo (`WeaponData.operationRangeMin`, default 1) mas **não tem
os três nomes**: não existe `CombateCorporal`, `CombateADistancia` nem
`CombateHibrido`. A classificação é doutrina; hoje ela vive espalhada em testes
de `operationRangeMin`. É o mesmo `rangeMin ≥ 1` que a Hotzone usa para devolver
`null` em Combate + Terrestre.

### PodeEmbarcar

Requer **pontos de movimento sobrando** para pagar o custo do terreno **do
transportador** — esse custo *é* o custo do embarque.

- O transportador **limita** onde aceita embarque e que **tipo de vaga** oferece.
- **Não** entra multiplicador de autonomia.

### PodeDesembarcar

- O **transportador** deve estar em local válido segundo a ficha dele.
- O **transportador** também elege os locais válidos **para a carga**, segundo a
  ficha dele.
- O **passageiro** paga o custo de MP do desembarque — esse custo *é* o custo do
  desembarque —, **sem** multiplicador de autonomia.

> Desembarque é sempre ação do **transportador**; embarque é sempre ação do
> **passageiro**. As duas fichas participam, mas o dono da ação não muda.

### PodeSuprir

A unidade supridora **converte recursos em serviços** e presta em campo, no
alcance da ficha dela.

| regra | detalhe |
|---|---|
| alcance | range 1, apenas embarcados, ou combinação dos dois |
| camada | o serviço acontece **na camada do supridor**, por um custo |
| aeronaves | pousam, e **arremetem** depois de supridas |
| aproximação | o supridor tenta chegar à camada do atendido, se tiver condições |
| ação | consome a ação **do supridor**, não a do suprido |

✅ o alcance por modo existe (`Adjacent1Hex`, `SameHexOrEmbarked`) e é o que
sustenta o modo Hospital do transporte.

### PodeTransferir

| regra | detalhe |
|---|---|
| classificação | **Hub** (trocam recursos entre si) e **Receiver** (apenas recebem) |
| custo | **não tem** |
| camada | mesma camada do supridor |
| aeronaves de carga | pousam e **não** arremetem |

✅ `SupplierTier.Hub` / `Receiver` em `PodeTransferirSensor`.

Contraste que vale marcar: **suprir custa e arremete; transferir não custa e não
arremete.** São a mesma geometria com economias opostas.

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

⚠️ Não existe como sensor. É a ausência de ação, não uma ação — mas o contrato a
trata como uma das oito, e há razão: "ficar" é uma decisão, não um resto.

---

## 5. A consequência para os papéis

Esta seção não é do contrato; é o que sai dele.

Se **toda** unidade lê **estes** sensores, então papel não é um conjunto de
sensores. É uma **consulta diferente sobre os mesmos sensores**:

```text
papel  =  intenção  ×  subetapa  ×  banda      (sobre o mesmo PodeX)
```

Que é exatamente a assinatura do `UnitReachEnvelopeService`. O envelope não foi
inventado para o transporte nem para a artilharia: ele é a forma geral de
"perguntar como um papel".

**Consequência direta:** arquivo de papel não deveria conter lógica de alcance
nenhuma. Só **política** — prioridade, recusa, desempate, quando desistir. Todo
cálculo de "até onde" pertence ao serviço.

É por isso que consumir a Hotzone **encolhe** os arquivos de papel. Não é
otimização: é o papel voltando a ser só o que ele é.

As relações de governo **entre** papéis — quem é âncora de quem, quem adota a
agenda de quem — são outro assunto, em
`docs/AI Behavior/governanca_entre_papeis.md`.

---

## Pendências

| # | contrato | código hoje |
|---|---|---|
| G1 | `ApenasMover` é um dos sensores | ⚠️ não existe sensor de movimento; é `UnitMovementPathRules` |
| G2 | as três categorias de combate são nomeadas | ⚠️ só existe `operationRangeMin`; a classificação vive espalhada em testes soltos |
| G3 | são 6 sensores de sistema | ⚠️ são 7 — falta `PodeSubmergirRapidamente` na lista |
| G4 | `PodeDetectar` tem lugar na taxonomia | ❌ existe e não está em nenhuma família |
| G5 | `PodeDecolar` é sempre chamado ao selecionar/ativar | ❓ |
| G6 | os dois redutores de −50% da captura | ❓ |
| G7 | ordem ≠ sensor | ⚠️ o Serviço do Comando **é** um sensor no código; a distinção só existe aqui |
