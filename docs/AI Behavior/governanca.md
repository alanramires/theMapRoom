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

## Upkeep

No início de cada rodada, o sistema executa **três rotinas obrigatórias**.

### Consumo de Autonomia

Unidades cujo `autonomyData` possui consumo de upkeep deduzem autonomia
automaticamente. ✅

### Pouso de Emergência

Aeronaves que chegam a zero de autonomia após o upkeep chamam `PodePousar`. Se
não houver pouso válido, são destruídas. ✅

A aeronave que pousa assim fica com **os motores desligados**: não arremete
depois de ser suprida. É a exceção ao `PodeArremeter`, e é necessária — sem ela,
a aeronave sem combustível voltaria ao ar no primeiro suprimento.

### Jornal do Comando

Resumo da rodada anterior, especialmente útil em partidas assíncronas, incluindo
acontecimentos relevantes e o estado das aeronaves. ✅

---

# Ordens do Comandante

Cinco ordens globais, disponíveis **a qualquer momento** durante o turno. Ordem
não é ação de unidade: não consome movimento nem passa pela cadeia `PodeX`.

## Serviço do Comando

Rotina de suprimento sobre unidades que:

- ainda não agiram;
- ainda não receberam atendimento na rodada;
- estão em **construção aliada** ou **embarcadas em um supridor**.

A validação usa `PodeSuprir`. ✅ `ServicoDoComandoSensor`

> O Serviço do Comando **não encerra a ação** da unidade atendida. Ela ainda pode
> agir normalmente depois de receber o serviço.

## Dispensar Unidade

Destrói voluntariamente uma unidade. Útil ao atingir o limite de unidades do
tabuleiro, ou quando uma unidade permanece tempo demais sem possibilidade de
resgate.

**O dinheiro investido não é recuperado.** ✅ `SensorActionType.RemoveUnit`

## Comprar Unidade

Ao acessar uma construção **produtora controlada**, o jogador compra unidades do
catálogo dela mediante pagamento em dinheiro. ✅ `SensorActionType.Shopping`

## Passar a Vez

Encerra o turno independentemente de quantas unidades tenham agido. ✅

## Inspecionar

Permite inspecionar unidades aliadas **que já agiram**, unidades inimigas e
construções. Cada clique consecutivo revela nova informação — área de ameaça,
visão na névoa, e outros dados disponíveis. ✅

⚠️ Nota de nomenclatura: o Serviço do Comando é implementado **como sensor** e
Dispensar aparece como `RemoveUnit`. A distinção "ordem ≠ sensor" existe só neste
documento.

---

# Ciclo da Unidade

Toda unidade selecionada deve obrigatoriamente **posicionar-se** e depois
**executar uma ação**.

## Posicionamento

| forma | o quê |
|---|---|
| **Segurar Posição** | a unidade permanece no hexágono atual |
| **Escolher um Hexágono** | a unidade assume **provisoriamente** uma nova posição |

> Durante o posicionamento, o tabuleiro **não recalcula** seu estado definitivo.
> O jogador pode cancelar e reposicionar quantas vezes quiser antes de escolher
> uma ação.

✅ É o invariante transacional do `CLAUDE.md`: toda ação começa e termina em
`CursorState.Neutral`, e o que acontece no meio é **provisório e cancelável** —
não atualiza FoW, não revela unidade, não consome recurso, não marca a unidade
como agida. Ver `docs/arquitetura/acoes_transacionais.md`.

Este é o parágrafo do qual sai metade das regras do jogo. Fundação, não detalhe
de interface.

---

# Ações

Após o posicionamento, o sistema calcula as ações disponíveis pelos sensores
`PodeX`.

## Quem encerra a vez de quem

Tabela transversal — a regra mais fácil de errar, e a mais fácil de conferir:

| ação | encerra a vez de | conferido em |
|---|---|---|
| **Capturar** | o capturador | `TurnStateManager.Capture.cs:217` ✅ |
| **Embarcar** | **o transportador**, mesmo que ele não tenha agido | `ScannerPrompt.cs:2800` ✅ |
| **Desembarcar** | **o transportador E cada unidade desembarcada** | `Disembark.cs:866` e `:893` ✅ |
| **Suprir** | **só o supridor** — nunca o atendido | `SupplyQueue.cs:888` ✅ |
| **Fundir** | a unidade resultante | `Merge.cs:669` ✅ |
| **Transferir** | quem **iniciou** a transferência | ⚠️ **ninguém** — `TurnStateManager.Transfer.cs` não marca ação de nenhum lado |

A última linha é a única divergência da tabela, e ela é curiosa: a **segunda**
metade da regra de transferência — *"uma unidade ainda pode receber ou fornecer
estoque depois de já ter agido"* — funciona justamente **porque** a primeira não
foi implementada. Fechar a primeira sem cuidado quebra a segunda.

---

## Fonte de Renda — `PodeCapturar`

Requer a habilidade **`Captura Construções`**. ✅ `PodeCapturarSensor.cs:36`

A unidade converte **HP em progresso de captura**.

| penalidade | quando |
|---|---|
| −50% | alguns papéis capturam com penalidade |
| −50% | construção com pré-requisitos não atendidos, **sobre o valor já reduzido** |

> As penalidades são **multiplicativas**: metade da metade, e não uma redução
> total de 100%.

❓ as duas penalidades não foram conferidas no código.

---

## Combate — `PodeMirar`

Requer armas em `EmbarkedWeapons`.

### Combate de Contato

- Pode ocorrer **parado ou após movimento**.
- Armas com `rangeMin = 1`.
- **Pode gerar revide.**

### Combate à Distância

- Ocorre normalmente **apenas sem deslocamento**.
- Armas com `rangeMin > 1`.
- Não gera revide.

### Alcance zero — experimental

Armas com `rangeMin = 0` são um tipo especial e **ainda experimental** de Combate
à Distância. Atacam alvos no **mesmo hexágono** da unidade, mas em **outra camada
ou altura**. Não geram revide.

> É a única arma cujo alcance é medido em **camada**, não em hexes. Por isso ela
> não cabe na banda do envelope como as outras — e é por isso que está marcada
> como experimental.

### Combate Híbrido

A unidade tem armas dos dois tipos. Tenta **primeiro à distância**; não sendo
possível, tenta o Contato.

⚠️ O código tem o campo (`WeaponData.operationRangeMin`, default 1) mas **não os
nomes**. A classificação é doutrina; hoje vive espalhada em testes soltos desse
campo. É o mesmo `rangeMin ≥ 1` que a Hotzone usa para devolver `null` em Combate
+ Terrestre.

---

## Transporte

### `PodeEmbarcar`

O **passageiro** precisa conservar MP suficiente para pagar o custo do terreno
**onde está o transportador** — esse valor torna-se o custo do embarque.

O transportador define **onde** aceita embarque, **quais tipos** de unidade
aceita e **quais vagas** oferece.

Multiplicadores de consumo de autonomia **não** se aplicam ao embarque.

> Embarcar encerra o turno **do transportador**, mesmo que ele ainda não tenha
> agido. ✅

### `PodeDesembarcar`

O transportador precisa estar em local válido conforme a ficha dele; a mesma
ficha determina quais locais podem **receber a carga**.

O passageiro paga o custo de movimento do terreno de desembarque, sem
multiplicadores de autonomia.

> Desembarcar encerra o turno **do transportador e de todas as unidades
> desembarcadas**. ✅

Desembarque é sempre ação do **transportador**; embarque é sempre ação do
**passageiro**. As duas fichas participam, mas o dono da ação não muda.

---

## Logística — `PodeSuprir`

A supridora converte reservas em **serviços prestados em campo**.

| alcance | |
|---|---|
| mesmo hexágono ou unidades embarcadas | `SameHexOrEmbarked` |
| um hexágono adjacente | `Adjacent1Hex` |
| combinação das duas | |

O atendimento ocorre **na camada do supridor**; quando possível, o supridor tenta
**igualar sua camada à do atendido**.

Mas a camada do encontro é um **acordo entre os dois**, não uma imposição de um
lado: o caminhão não decola, e o caça pode pousar. Ver *A camada do encontro é um
acordo*, em `PodeMudarDeAltitude`.

- Aeronaves **pousam**, recebem e **arremetem**.
- Submersíveis **emergem**, recebem e **permanecem na superfície**.

O serviço tem custo e consome a ação **do supridor**, não a do atendido. ✅

> A aeronave volta ao lugar dela; o submersível não. Não é simetria, é
> assimetria — e é o ponto. O único que paga preço **permanente** pelo suprimento
> é o furtivo, porque o que o serviço tira dele é justamente o que o define.

---

## Estoque — `PodeTransferir`

| classe | o quê |
|---|---|
| **HUB** | trocam recursos entre si |
| **Receiver** | apenas recebem |

✅ `SupplierTier.Hub` / `Receiver` em `PodeTransferirSensor`.

A transferência não tem custo financeiro, ocorre na mesma camada do supridor, e
consome a ação de **quem iniciou**.

> Uma unidade ainda pode receber ou fornecer estoque **depois de já ter agido**,
> desde que a **outra** unidade se aproxime e inicie a operação.
>
> Exemplo: um navio-tanque que já transferiu para um porta-aviões encerrou sua
> ação. Ainda assim, uma fragata Receiver pode se aproximar e iniciar uma nova
> transferência, recebendo recursos desse navio-tanque.

Aeronaves de carga **pousam para transferir e não arremetem**.

Contraste que vale marcar: **suprir custa e arremete; transferir não custa e não
arremete.** Mesma geometria, economias opostas.

---

## Sobrevivência — `PodeFundir`

Usa a mesma lógica de aproximação e custo de entrada do **embarque**. ✅ o
envelope já trata Fusão assim (`ResolveFusionEnterCost`).

Só entre unidades **aliadas**, **idênticas** e **compatíveis para fusão**.

A unidade selecionada entra no candidato e é **absorvida**, formando uma única
unidade consolidada.

| grandeza | como combina |
|---|---|
| HP | **soma direta** |
| munição | média ponderada |
| autonomia | média ponderada |

> Após a fusão, a unidade resultante **encerra sua vez**. ✅ `Merge.cs:669`

⚠️ Fusão **não tem banda Operational** — ver `contrato_envelope_alcance.md`.

---

## Mobilidade — `ApenasMover`

Confirma a posição provisória **sem executar outra ação**. A unidade pode ter se
deslocado ou permanecido parada.

Segurar posição pode ser útil para manter uma linha, preservar uma posição,
servir como **observador avançado**, ou aguardar uma oportunidade futura.

✅ Não é sensor, e não deve ser: é a confirmação do posicionamento. Não existe
`PodeMoverSensor`.

---

# Sensores Aéreos e Navais

Não são apresentados ao jogador. São usados **internamente por outras ações**.

## Sensores Aéreos

### `PodeDecolar`

Verifica se a aeronave consegue decolar até a altitude desejada. Em **pistas
improvisadas**, a decolagem pode ficar limitada a uma única camada ou a um único
hexágono.

> Exemplo: ao decolar de um porta-aviões, qualquer aeronave avança **um** hexágono
> e permanece em `Air/Low`.

### `PodeArremeter`

Faz a aeronave pousar, aguarda a operação que acionou o sensor, e chama
`PodeDecolar` em seguida.

> Exemplo: um caça em `Air/High` sobrevoa uma pista. Um supridor na planície o
> aciona; o caça pousa, recebe reabastecimento e decola de novo para `Air/Low`.

✅ Confirmado: `PodeArremeterSensor` chama `PodeDecolarSensor.Evaluate` e loga
*"Arremetida autorizada; decolagem final validada por PodeDecolar."*

É o único sensor que **compõe** dois outros — pouso mais decolagem num gesto só.
Daí a necessidade de o pouso de emergência desligar o motor.

### `PodePousar`

Verifica se a aeronave tem as **habilidades** necessárias para pousar no local:
`VTOL`, `SVTOL`, `Aircraft Landing`, `Aircraft Carrier Landing`, `Sea Landing`.

> Exemplo: um hidroavião sobrevoando o mar pousa quando fica sem combustível ou
> quando recebe suprimento de um navio-tanque.

✅ **E é melhor do que o contrato descreve.** As habilidades não estão codificadas
no sensor: são **dados**. `SkillData.id` documenta os exemplos *"vtol, stovl,
aircraft landing"*, e é a **construção** que declara o que exige, em
`ConstructionData.requiredLandingSkillRules` — com
`requireAtLeastOneLandingSkill` escolhendo entre "basta uma" e "exige todas".

⚠️ Divergência de nome: o contrato escreve **SVTOL**; o código escreve **STOVL**
(*Short Take-Off, Vertical Landing*). Um dos dois precisa ceder.

### `PodeMudarDeAltitude`

Reposiciona a aeronave entre altitudes, geralmente durante operações de
suprimento. ✅ (`PodeMudarAltitudeSensor`)

> Exemplo: um KC-130 em `Air/High` aproxima-se de helicópteros em `Air/Low` e
> caças em `Air/High`. Como o helicóptero **não pode subir**, o avião-tanque
> **desce** para `Air/Low` e os caças **nivelam** nessa camada. Com todos na mesma
> altitude, o reabastecimento em voo acontece.

### A camada do encontro é um acordo

O sistema **procura um acordo**, e qualquer um dos dois lados pode ceder.

> O caminhão de suprimentos **não decola** para encontrar o caça — mas o caça
> **pode pousar**. Havendo acordo, o serviço acontece.

Não é "quem não pode mudar dita a camada". É **interseção**: cada lado tem o
conjunto de camadas em que consegue **prestar ou receber** o serviço, e o
atendimento ocorre se a interseção não for vazia. O caminhão tem um conjunto de
um elemento só; o caça tem três. Por isso o acordo cai no do caminhão — não por
regra de precedência, mas porque é o único ponto comum.

**O conjunto é de camadas de serviço, não de camadas ocupáveis**, e os dois nem
sempre coincidem. O KC-130 **só atende no ar**: `{Air/High, Air/Low}`. Que ele
consiga pousar é irrelevante — pousado, ele não presta o serviço.

### Interseção vazia é resultado legítimo

Encontro de três: avião-tanque, helicóptero e **blindado**.

| participante | camadas de serviço |
|---|---|
| KC-130 | `Air/High`, `Air/Low` |
| helicóptero | `Air/Low` |
| blindado | `Land/Surface` |

O acordo entre tanker e helicóptero é `Air/Low`. **O blindado fica de fora** — a
interseção dele com o tanker é vazia, e vazio não é erro nem caso especial: é
simplesmente "não há serviço".

Isso é o que a formulação por interseção compra. Com "o menos móvel dita a
camada", o blindado seria o menos móvel dos três e puxaria o encontro para o
chão, onde o tanker não atende ninguém. A regra errada não só erra o caso de três
— ela erra **para o lado pior**.

É a mesma forma do `MelhorEmbarque` e do `MelhorDesembarque`: pergunta-se uma vez
por participante e cruza-se o resultado. Cruzar, ordenar e desempatar são do
**consumidor**, nunca do serviço. Ver "As três camadas" no `CLAUDE.md`.

## Sensores Navais

### `PodeEmergir`

Verifica se o submarino pode subir à superfície. Usado, por exemplo, quando um
navio passa sobre a posição dele, ou quando outra operação exige que esteja na
superfície. ✅

### `PodeSubmergir`

Verifica se o submarino pode retornar à camada submersa.

> Submarinos **atingidos** ou **que dispararam** ficam temporariamente impedidos
> de submergir.

✅ o mecanismo existe (`IsLayerChangeBlockedByForcedLock`); ❓ os gatilhos não
foram conferidos.

Este lock é a peça que dá custo ao tiro do submarino: atirar **revela e prende**
na superfície. É a mesma economia do `PodeSuprir` — o furtivo é o único que paga
com a própria natureza.

### `PodeSubmergirRapidamente`

Permite que uma unidade **termine submersa** mesmo tendo iniciado a ação na
superfície, desde que todas as condições sejam atendidas. ✅

---

# Visão, Busca e Detecção

## Camadas de Visualização

✅ `FogOfWarVisionMode { All, Air, Surface, Sub }` — as quatro, exatamente.

| camada | o que exibe |
|---|---|
| **`ALL`** | todos os hexágonos liberados pela **combinação** dos sensores |
| **Aéreo** | só os hexágonos e contatos revelados pelas unidades de vigilância aérea |
| **Superfície** | só os hexágonos revelados pela visão de superfície |
| **Submarina** | o fundo do mar; o restante do tabuleiro fica escurecido |

Na visualização padrão considera-se a **linha de visão descendente** da posição
da unidade até a superfície. Encontrando obstáculo geográfico, os hexágonos
posteriores não são revelados nessa camada.

**Ainda assim, uma unidade pode detectar contatos de outra camada.**

> Exemplo: um Super Tucano está em `Air/Low`, atrás de uma montanha. O jogo
> revela três hexágonos, mas não o quarto — a linha descendente foi bloqueada
> pela geografia. Entretanto o Super Tucano tem visão aérea de quatro hexágonos e
> detecta um helicóptero inimigo. Na visualização padrão o helicóptero aparece
> **desfocado sobre a névoa**; mudando para a camada aérea, o jogador o vê
> corretamente.

Este exemplo é o documento inteiro em miniatura: **liberar hexágono e detectar
unidade são duas perguntas diferentes**, e podem discordar. O desfoque sobre a
névoa é a interface admitindo a discordância em vez de escondê-la.

## Névoa de Guerra

### `PodeEnxergar`

Libera hexágonos do tabuleiro conforme **alcance**, **elevação** e **linha de
visão**.

> O padrão da visão é projetado **em direção ao chão**. A curva pode permanecer
> **nivelada** ou **descer** ao longo do percurso.

Enxergar um hexágono **não** significa detectar todas as unidades nele. O
resultado depende do alcance de visão, das habilidades de detecção e das
**ocultações** presentes no local.

É o mesmo motivo pelo qual construção com `visão = N` revela terreno no raio N
mas só spotta unidade no raio 0 — prédio não é observador.

**`HexEnxergado`** é uma **consulta**: parte do hexágono e identifica quem
consegue vê-lo.

Na visualização padrão do tabuleiro, o jogador vê os hexágonos revelados pela
**visão de superfície**.

### `PodeDetectar`

Unidades com sensores especializados procuram **ocultações específicas**. O
sensor e a ocultação precisam ter **etiquetas compatíveis**.

| sensor | procura |
|---|---|
| caça submarina | unidades com operações submarinas |
| detector de furtivos | ocultação aérea `Stealth` |

Havendo correspondência, a unidade oculta é revelada. Detectada **atrás da
névoa**, ela aparece **desfocada sobre a névoa**.

A consulta inversa — *"alguém consegue me detectar?"* — usa a mesma lógica a
partir do ponto de vista da unidade observada.

Semântica já fixada: o olho significa que uma unidade **com skill de ocultação**
foi detectada — sem filtro de camada, intencionalmente.

### Estado do código

⚠️ **`PodeEnxergar` não tem arquivo de sensor.** Existe como
`PodeEnxergarRuntime` / `PodeEnxergarRuntimeLogs` dentro do `MatchController` e
como janela de Editor — nunca foi extraído para `Assets/Scripts/Sensors/`. E a
conta da linha ele **pega emprestada do vizinho**: a janela do `PodeEnxergar`
chama `PodeDetectarSensor.TryGetObservationLineDebug`, e a de `HexEnxergado`
chama `PodeDetectarSensor.CollectVisibleCells` — embora a própria ajuda dela diga
*"usando as regras do PodeEnxergar"*.

Liberar hexágono e detectar unidade **compartilham a geometria** e estão num
arquivo só, com o nome do outro. O contrato agora separa os dois com clareza; o
código ainda não.

Quatro ferramentas em `Tools > FoW` cobrem as quatro perguntas: **Pode Enxergar**
(o que eu vejo), **Hex Enxergado** (quem vê este hex), **Pode Detectar** (o que eu
detecto) e **Alguém me vê** (quem me detecta).

---

# Hotzone

A Hotzone nasceu de uma necessidade básica:

> Onde posso me posicionar sem sofrer o ataque de uma unidade que move 6
> hexágonos e ataca no sétimo?

Com o tempo, essa leitura deu origem a dois conceitos baseados em movimento: as
bandas **Tática** e **Operacional**.

O serviço de Hotzone **não escolhe** a melhor posição nem executa ações. Ele
apenas devolve uma área conforme as instruções recebidas. Essa área serve de base
para diversos serviços da IA, permitindo que movimento, combate, transporte,
logística e vigilância usem o **mesmo vocabulário espacial**.

Definição completa em `docs/AI Behavior/hotzone e bandas de alcance.md` e, como
norma, em `docs/AI Behavior/contrato_envelope_alcance.md`.

Em resumo:

| conceito | o quê | estado |
|---|---|---|
| **Tático** | usa o MP restante da unidade | ✅ `ReachBand.Tactical` |
| **Operacional** | projeta **duas bandas sucessivas** de MP | ✅ `ReachBand.Operational` |
| **Range** | alcance da arma, do serviço ou de outro efeito produzido pela unidade | ⚠️ **não é uma banda** |

**"Sucessivas" é a palavra que carrega a regra.** O Operacional é `Remaining +
Max × (turnos − 1)`, encadeado — **nunca** `MP × 2` empoçado. Um soldado de 3 MP
diante de duas montanhas de custo 2 alcança 2 hexes por turno, não 3.

⚠️ **`ReachBand` tem só dois valores: `Tactical` e `Operational`.** Não existe
banda `Range`. Alcance de arma e de serviço entram como `ActionCells` dentro de
uma das duas bandas — e é por isso que o artilheiro precisou de tratamento
especial: para ele, a banda **é** o alcance da arma, não o do movimento. Ver a
inversão do artilheiro no contrato do envelope.

O contrato acerta ao listar os três como conceitos; o código só não os
representa como irmãos.

---

# Papéis da IA

A IA organiza as unidades por **papéis**. Cada papel interpreta de maneira
diferente as Hotzones e as opções devolvidas pelos sensores `PodeX`.

> Os sensores são **os mesmos** para todas as unidades. O papel determina apenas
> quais intenções serão priorizadas e como as opções válidas serão avaliadas.

Essa frase é a chave de leitura de todos os contratos de papel desta pasta, e ela
tem uma consequência de arquitetura: papel **não é um conjunto de sensores**, é
uma **consulta** sobre os mesmos sensores —

```text
papel  =  intenção  ×  subetapa  ×  banda      (sobre o mesmo PodeX)
```

— que é exatamente a assinatura do `UnitReachEnvelopeService`. Daí: arquivo de
papel não deveria conter lógica de alcance nenhuma, só **política** (prioridade,
recusa, desempate, quando desistir). É por isso que consumir a Hotzone **encolhe**
os arquivos de papel: não é otimização, é o papel voltando a ser só o que ele é.

## Papéis principais

| papel | prioriza | no código |
|---|---|---|
| **Capturador** | captura de construções. Combate quando necessário, normalmente depois de avaliar a captura | `UnitRole.Capturador = 1` ✅ |
| **Assalto** | combate de contato, avanço e ruptura da linha inimiga | `UnitRole.Assalto = 2` ✅ |
| **Fire Support** | combate à distância e posicionamento de apoio na **Retaguarda** | ⚠️ `UnitRole.FogoIndireto = 5` |
| **Transportador** | coordena passageiros, pontos de encontro, embarque, desembarque e transporte entre objetivos. Atua como **pickup**, **courier** ou **táxi** | `UnitRole.Transportador = 3` ✅ |
| **Logística** | presta serviços de campo, mantém unidades operacionais, participa da cadeia de suprimentos | `UnitRole.Logistica = 4` ✅ |
| **Vigilância** | revelação da névoa, observação e detecção de unidades ocultas | ⚠️ `UnitRole.VigilanciaAerea = 6` |

**Vigilância** foi criada originalmente para operações em `Air/High`, mas passará
a ser baseada na **visão especializada e nas habilidades de detecção** da
unidade. O nome no código ainda carrega o `Aerea` da origem.

⚠️ Duas divergências de vocabulário, ambas de nome e não de comportamento:
**Fire Support** é `FogoIndireto` no enum, e **Vigilância** é `VigilanciaAerea`.
Os valores numéricos estão serializados em `UnitData` e saves, então renomear é
seguro **desde que os números não mudem**.

## Especializações de papel

Modificam o comportamento de um papel principal, ou participam de mais de uma
categoria.

| especialização | de quem | o quê | no código |
|---|---|---|---|
| **Capturador Agressivo** | Capturador | prefere **atacar primeiro e capturar depois**. Captura com eficiência menor que um Capturador | `CapturadorAgressivo = 12` ✅ |
| **Interceptador** | Assalto | contra alvos **aéreos**. Usado por unidades aéreas de contato | `Interceptador = 8` ✅ |
| **Ataque Aéreo** | Assalto | aeronaves atacando alvos de **superfície** | `AtaqueAereo = 9` ✅ |
| **Artilheiro Combatente** | Fire Support → Assalto | tenta primeiro o combate **à distância**; sem solução válida de longo alcance, passa para Assalto | `ArtilheiroCombatente = 13` ✅ |
| **Antiaéreo Combatente** | Fire Support → Assalto | mesma lógica, contra alvos **aéreos** | `AntiaereoCombatente = 14` ✅ |
| **Antiaéreo** | Fire Support | especialização **estacionária**, controle do espaço aéreo | `Antiaereo = 10` ✅ |
| **Estoque** | Logística | movimentação de consumíveis: entre unidades, de unidade para construção, de construção para unidade, e entre agentes logísticos compatíveis | `Estoque = 7` ✅ |

O comentário do enum já registra a distinção que o contrato faz: *"Antes chamado
Suprimentos: o papel é movimentar carga, não prestar serviço de suprimento — quem
supre é Logistica."*

Nota sobre participação em batalha: o código classifica os papéis em
`UnitBattleParticipation` **Direct** ou **Indirect**, e `FogoIndireto` é o
**único** Indirect. Artilheiro Combatente e Antiaéreo Combatente são Direct — o
que é coerente com a especialização deles ter uma perna em Assalto.

## Papéis incorporados ou descontinuados

| papel | destino | estado |
|---|---|---|
| **Raid Antissubmarino** | será incorporado à **Vigilância**. Pode virar a especialização **Vigilância Naval** | ⚠️ `RaidAntiSub = 11` ainda existe no enum |
| **Transportador Aéreo** | **foi** incorporado ao Transportador. O mesmo papel receberá regras específicas para transporte naval | ⚠️ `TransportadorAereo = 15` **ainda existe no enum**, com política de shopping própria documentada no comentário |

A segunda linha merece atenção: o contrato usa o **passado** ("foi
incorporado"), e o comportamento realmente está unificado — os arquivos aéreos
vivem em `Units/Transport/`. Mas o **valor do enum continua lá**, com uma regra
própria de compra: o Chinook mira os nós **intermediários** do eixo, enquanto o
APC só gera demanda depois que os nós iniciais foram conquistados. Ou essa regra
migra para uma condição dentro do Transportador, ou o papel não foi incorporado —
foi só mudado de pasta.

## Hierarquia

```text
Capturador
└── Capturador Agressivo

Assalto
├── Interceptador
├── Ataque Aéreo
├── Artilheiro Combatente
└── Antiaéreo Combatente

Fire Support
└── Antiaéreo

Transportador

Logística
└── Estoque

Vigilância
└── Vigilância Naval
```

⚠️ A hierarquia é **doutrina, não estrutura**: o `UnitRole` é um enum plano, sem
relação de pai e filho. Quem materializa o parentesco é
`UnitRoleCompatibility.CanSatisfy` — e é por isso que todo portão de papel em
sensor ou execução precisa usar `CanSatisfy`, e nunca `roles.Contains` estrito.
Com o teste estrito, `CapturadorAgressivo` não passa por um portão de
`Capturador`, embora seja um.

Note também que Artilheiro Combatente e Antiaéreo Combatente aparecem sob
**Assalto** nesta hierarquia, embora o texto diga que eles "participam primeiro
de Fire Support". Não é contradição: a hierarquia responde *"que papel ele
satisfaz"*, e a descrição responde *"em que ordem ele tenta"*. São perguntas
diferentes.

As relações de governo **entre** papéis — quem é âncora de quem, quem adota a
agenda de quem — estão em `docs/AI Behavior/governanca_entre_papeis.md`.

---

# Pendências

| # | contrato | código hoje |
|---|---|---|
| G2 | as três categorias de combate são nomeadas | ⚠️ só existe `operationRangeMin`; a classificação vive espalhada em testes soltos |
| G4 | a família de detecção tem 3 sensores | ⚠️ tem **2**. `HexEnxergado` e "alguém me vê" o próprio contrato define como **consultas**, não sensores |
| G5 | `PodeEnxergar` é sensor | ⚠️ **não tem arquivo** e empresta a matemática do `PodeDetectarSensor` |
| G6 | `PodeDecolar` é sempre chamado ao selecionar/ativar | ❓ |
| G7 | as duas penalidades de −50% da captura | ❓ |
| G8 | ordem ≠ sensor | ⚠️ o Serviço do Comando **é** um sensor no código |
| G9 | submersível emerge, recebe e permanece na superfície | ❓ |
| G10 | submarino atingido ou que disparou não submerge | ❓ o lock existe; os gatilhos não foram conferidos |
| G11 | ~~`PodePousar` distingue VTOL / SVTOL / pista~~ | ✅ **fechada** — e melhor: as skills são **dados** (`SkillData.id` + `ConstructionData.requiredLandingSkillRules`), não código. Falta só decidir entre **SVTOL** e **STOVL** |
| G12 | transferir consome a ação de quem iniciou | ⚠️ `TurnStateManager.Transfer.cs` não marca ação de **nenhum** lado. Cuidado: a segunda metade da regra depende disso |
| G13 | armas de `rangeMin = 0` atacam outra camada no mesmo hex | ❓ marcado pelo próprio autor como experimental. Não cabe na banda do envelope |
| G14 | **Range** é um dos três conceitos da Hotzone | ⚠️ `ReachBand` só tem `Tactical` e `Operational`. Alcance entra como `ActionCells` dentro de uma delas — e a inversão do artilheiro existe justamente porque, para ele, a banda **é** o alcance |
| G15 | o papel se chama **Fire Support** | ⚠️ o enum diz `FogoIndireto` |
| G16 | o papel se chama **Vigilância** | ⚠️ o enum diz `VigilanciaAerea`, nome da origem em `Air/High` |
| G17 | **Transportador Aéreo foi incorporado** | ⚠️ `TransportadorAereo = 15` ainda existe, com política de shopping própria. Mudou de pasta; a regra não migrou |
| G18 | **Raid Antissubmarino** vai para Vigilância | ⚠️ `RaidAntiSub = 11` ainda existe |
| G19 | a hierarquia de papéis | ⚠️ é doutrina, não estrutura: o enum é plano. Quem materializa o parentesco é `UnitRoleCompatibility.CanSatisfy` — portão que usa `roles.Contains` estrito barra especializações |
