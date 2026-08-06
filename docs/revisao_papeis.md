# Revisão dos Papéis — a matriz papel × sensor

> **Todos os papéis atiram, mas nem todos atiram da mesma maneira. Todos
> capturam, mas nem todos têm a skill ou a priorizam. Todos embarcam, mas alguns
> têm regra própria para pedir carona.**
>
> *É como raça de cachorro: mesma anatomia, expressão diferente.*

Este documento define o formato que a IA deve alcançar. Não é plano de execução
— é o alvo contra o qual cada migração é medida.

---

## O que a metáfora decide

Um papel **não é uma classe com lógica própria**. É uma **linha de uma matriz**,
e as colunas são as perguntas que o tabuleiro já sabe responder.

| | consequência |
|---|---|
| todo cachorro tem os mesmos órgãos | **toda linha tem todas as colunas** — não existe papel que "não usa" uma coluna, existe papel que usa o padrão dela |
| a raça difere em poucos órgãos | **a matriz é esparsa** — o papel declara só onde desvia; o resto herda |
| a raça não inventa um órgão novo | **o papel não cria pergunta** — se precisa de uma resposta nova, ela é um sensor ou um consumidor, nunca um `if` dentro do papel |

O terceiro item é o que mata o formato atual. Hoje um papel que precisa de algo
escreve o cálculo dentro de si, e a mesma pergunta acaba respondida N vezes com
respostas que podem discordar — foi o que aconteceu com `IsRebelCapturable`
(quatro papéis herdando um predicado no eixo errado) e com o "para onde revelar"
(três implementações com pesos próprios).

---

## As colunas

Cada coluna é um sensor `PodeX` — a resposta legal — mais o consumidor `Melhor*`
que ranqueia. O papel entra **só com política**.

| coluna | sensor | consumidor | o que o papel decide |
|---|---|---|---|
| **Apenas Mover** | Hotzone (`UnitReachEnvelope`) | `MelhorCapitao` | políticas, fallback, quem é o capitão, posição preferida entre vanguarda, flanco e retaguarda |
| **PodeCapturar** | ✅ | `MelhorCaptura` ✅ | papéis primários e secundários, ponta de lança ou fica até capturar |
| **PodeMirar** | ✅ | **falta** ❌ | como a unidade luta, DPQ escolhido, lutar conservadoramente, não lutar |
| **PodeEmbarcar** | ✅ | `MelhorEmbarque` + `QueroCarona` ✅ | regra para pedir carona, pressão quando espera demais |
| **PodeDesembarcar** | ✅ | `MelhorDesembarque` ✅ | regras para levar a carga |
| **PodeFundir** | ✅ | **falta** ❌ | sobrevivência, retaguarda ou fundir em prédios na vanguarda, posicionamento, recuar |
| **PodeSuprir** | ✅ | `MelhorEstoque` ✅ | quem atender, prioridade crítica, manutenção preventiva, carregar aeronaves em reparo |
| **PodeTransferir** | ✅ | `MelhorEstoque` ✅ | regras para movimentar estoque |
| **PodeEnxergar / PodeDetectar** | ✅ | `MelhorVisao` ✅ | observador avançado, pedido de hex para artilharia, caçar aeronaves furtivas, perseguir submarinos, patrulhar o mar, revisitar hexes conhecidos |
| **PodePousar** | ✅ | `MelhorPouso` ✅ | onde se recuperar, quando aceitar convés |

**Faltam exatamente dois consumidores: Combate e Fusão.** É a mesma conta do
`docs/resumo.md`, chegando por outro caminho — a matriz confirma a escada.

### Duas colunas que não são sensor

Elas atravessam todas as outras e por isso não têm `PodeX` próprio:

| coluna | o que o papel decide |
|---|---|
| **Rally** | onde a unidade fica durante a organização da invasão, e como se comporta enquanto a massa junta |
| **Defesa** | o que muda quando o slot vira postura defensiva |

São **modificadores de contexto**, não ações. A mesma unidade, no mesmo hex, com
a mesma resposta de sensor, decide diferente conforme o rally está juntando ou o
slot virou defesa.

---

## As linhas

Papéis vivos hoje (`UnitRole`):

| principais | secundários | apoio |
|---|---|---|
| Capturador | CapturadorCombatente | Transportador |
| Assalto | ArtilheiroCombatente | Logística |
| FogoIndireto | AntiaereoCombatente | Estoque |
| Vigilância | Interceptador | TransportadorAéreo |
| | AtaqueAereo | |
| | Antiaéreo | |

`RaidAntiSub` foi removido: todas as cinco unidades usam `roles: 06`; shopping e
operação especializam a demanda pela camada principal da ficha, e o valor 11
permanece apenas reservado para não ser reutilizado.

Aproximadamente **14 linhas × 12 colunas = 168 células**. A matriz cheia é
impraticável e não é o objetivo: **a maioria das células é o padrão.** Uma raça
difere em meia dúzia de órgãos, não em todos.

---

## O formato de uma linha

```text
AI Role: Capturador

  Apenas Mover      capitão = capturável; posição = onde captura;
                    fallback = RepCell do setor
  PodeCapturar      primário; ponta de lança cede o prédio e segue o eixo
  PodeMirar         luta só para abrir passagem; prefere DPQ da ficha
  PodeEmbarcar      pede carona quando o alvo está fora do Operational;
                    não embarca se está montando massa em rally ativo
  PodeDesembarcar   (padrão)
  PodeFundir        funde para sobreviver, na retaguarda
  PodeSuprir        (não se aplica — sem capacidade)
  PodeTransferir    (não se aplica)
  Enxergar/Detectar (padrão) — mas ganha nota por abrir hex que a artilharia pede
  PodePousar        (não se aplica)
  Rally             conta presença; sair atrasa o GoGreen
  Defesa            recolhe para os arredores do HQ
```

**"(padrão)" e "(não se aplica)" são respostas legítimas e devem aparecer.**
Célula em branco esconde a diferença entre "ninguém decidiu ainda" e "aqui o
comum serve" — e foi assim que apareceu, por exemplo, gate que pede unidade que
o mapa não pode produzir.

---

## Onde isso já está acontecendo

O formato não é aspiração: três colunas já operam assim.

| exemplo | como |
|---|---|
| **`AICaptainData`** | a coluna *Apenas Mover* já é **asset**: uma lista de atração por papel, ordenada, com "com plano" derivada. Trocar quem o antiaéreo segue é arrastar uma linha |
| **`MelhorCaptura`** | o serviço não conhece papel; o setor entra como filtro e a reserva como `evaluateAdjustment`. A política é do chamador |
| **`requiredSkillsToCapture`** | a construção diz quem captura e com que rendimento. O papel deixou de governar quanto se captura |

A direção é a mesma nos três: **o que era `if` virou parâmetro, e o parâmetro
virou dado.**

---

## Pendências que a matriz revela

### Colunas sem consumidor

**Melhor Combate** (`PodeMirar`) e **Melhor Fusão** (`PodeFundir`). Enquanto não
existirem, cada papel responde essas duas por conta própria — que é exatamente o
estado que a matriz quer terminar.

### ~~`RaidAntiSub`: demanda inaplicável~~ — resolvida

O gate antigo pedia um papel sem representantes. Agora a demanda pede:

```csharp
Vigilancia
  + RequiredVisionDomain = Submarine
  + RequiredVisionHeight = Submerged
```

A contagem usa papel + camada principal. Um EWACS não satisfaz a demanda
submarina, e Super Tucano/Fragata/Submarino podem satisfazê-la.

### "Pode atirar" precisa de dono

O substituto implementado não é outro papel: **vigilância armada** entra na
consulta de combate, e o `PodeMirar` conserva a autoridade sobre a legalidade do
tiro. Sem tiro materializável, o `MelhorVisao` conserva a autoridade sobre o
reposicionamento.

É o mesmo movimento que a captura fez: o papel deixou de governar a permissão.

### Cobertura aliada precisa ser filtrada por missão

Uma unidade comum que enxerga o mar não pode fazer a Fragata acreditar que a
cobertura `Submerged` já está garantida. O `MelhorVisao` já recebe
`AlliedCoverageWithoutObserver` como conjunto pronto. O chamador agora declara
`AlliedObserverFilter`; `AIController.Vigilancia` aceita somente aliados da mesma
camada principal e, quando necessário, com detecção stealth equivalente.

---

## Levantamento — Capturador

Primeira linha preenchida contra o código real. **19 arquivos, 5.823 linhas.**

Contagem de referências a cada coluna, por arquivo:

| arquivo | ln | Mover | Captur | Mirar | Embarc | Rally | Defesa |
|---|---:|---:|---:|---:|---:|---:|---:|
| `C.Attack` | 899 | 2 | – | 3 | – | – | 9 |
| `C` (entrada) | 618 | 6 | 4 | 3 | – | 12 | 2 |
| `C.Vacate` | 593 | 3 | – | 3 | – | – | 3 |
| `C.Defender` | 500 | 8 | 1 | 10 | – | 12 | 11 |
| `C.Explorer` | 462 | 6 | – | 2 | – | – | – |
| `C.Embark.Transporter` | 373 | – | – | – | 6 | – | – |
| `C.Embark` | 340 | 2 | – | 2 | 5 | 21 | – |
| `C.Helpers` | 304 | 2 | 6 | – | – | – | – |
| `C.Rogue` | 300 | 7 | 4 | 7 | – | – | – |
| `C.Swap` | 251 | 4 | – | 1 | – | – | – |
| `C.Embark.Pathing` | 246 | – | – | – | – | – | – |
| `C.Blitzkrieg` | 217 | 3 | 4 | – | – | 1 | – |
| `C.Embark.Scan` | 176 | – | – | – | – | – | – |
| `C.Pursuer` | 163 | – | – | 5 | – | – | – |
| `C.Embark.Extended` | 152 | – | – | – | – | – | – |
| `C.Agressive` | 101 | – | – | 2 | – | – | 1 |
| `C.PontaLanca` | 44 | 1 | 4 | – | – | – | – |
| `C.Opportunist` | 21 | – | 1 | – | – | – | – |

**Colunas ausentes por completo:** Desembarcar, Fundir, Suprir, Transferir,
Ver/Detectar, Pousar.

### O que o levantamento revela

**1. O capturador gasta mais código atirando do que capturando.**
`Mirar` aparece 38 vezes contra 13 de `Capturar`, e o maior arquivo da pasta —
`C.Attack`, 899 linhas — é sobre combate. O papel chamado "capturador" é, em
volume, um papel de combate com uma agenda de captura em cima.

**2. Embarcar está espalhado por cinco arquivos.**
`Embark`, `Embark.Transporter`, `Embark.Pathing`, `Embark.Scan` e
`Embark.Extended` somam **1.287 linhas** — mais que a coluna de captura inteira.
Três deles não referenciam sensor nenhum: são pathfinding e varredura próprios.

**3. `Ver/Detectar` está em branco, mas a pergunta é respondida.**
`C.Explorer` tem 462 linhas sobre onde revelar névoa, com seis constantes de peso
próprias (`ExplorerForwardObserver*`) — e **zero** referências a
`PodeDetectar`/`PodeEnxergar`. É a coluna respondida sem o sensor, à mão. Uma das
três implementações que o `MelhorVisao` existe para substituir.

**4. `Fundir` é branco de verdade.**
Nenhuma referência a `PodeFundirSensor` em 5.823 linhas. Não é "não se aplica" —
infantaria fundir para se curar é mecânica central. É **ninguém decidiu ainda**,
que é exatamente o que a matriz quer tornar visível.

**5. Quatro colunas são "(não se aplica)" legítimas.**
`Suprir` e `Transferir` (o capturador não tem capacidade logística), `Pousar`
(não é aeronave) e `Desembarcar` — esta última por doutrina: *desembarque é
sempre ação do transportador*.

**6. Rally e Defesa estão presentes e concentrados.**
46 e 26 referências, com massa em `C.Defender` (12/11) e `C.Embark` (21 de
rally). As duas colunas transversais já existem de fato — só não estão
declaradas como política, e sim espalhadas em condicionais.

### Os dez modos

O capturador já tem, na prática, dez variações de comportamento em arquivo
próprio:

```
Rogue · Defender · Explorer · Blitzkrieg · PontaLanca
Opportunist · Agressive · Pursuer · Swap · Vacate
```

Isso é o **degrau 4** — "variações de papel viram parâmetro" — já materializado
como arquivos. A matriz não precisa criar essas variações; precisa transformá-las
em linhas que declaram só o que difere.

### A linha, como deveria ficar declarada

```text
AI Role: Capturador

  Mover/Posicionar  capitão = capturável; fallback = RepCell do setor
  PodeCapturar      primário; ponta de lança cede o prédio e segue o eixo
  PodeMirar         ⚠️ hoje é o maior bloco do papel — precisa virar política,
                    não 899 linhas
  PodeEmbarcar      não embarca montando massa em rally ativo;
                    ⚠️ 1.287 linhas em cinco arquivos
  PodeDesembarcar   (não se aplica — ação do transportador)
  PodeFundir        ❌ EM BRANCO — ninguém decidiu
  PodeSuprir        (não se aplica)
  PodeTransferir    (não se aplica)
  Ver/Detectar      ⚠️ respondido à mão em C.Explorer, sem sensor
  PodePousar        (não se aplica)
  Rally             conta presença; sair atrasa o GoGreen
  Defesa            recolhe para os arredores do HQ
```

Três marcas de trabalho: **um branco real** (`Fundir`), **uma coluna respondida
sem sensor** (`Ver`) e **duas hipertrofiadas** (`Mirar`, `Embarcar`).

---

## As raças que a matriz destrava

> *Agora que eu sei que papel é raça, dá pra pensar em coisas interessantes.*

O levantamento acima marcou quatro colunas do Capturador como *"(não se
aplica)"*. **A ironia é que "não se aplica" nunca foi propriedade do papel — era
propriedade da ficha.** O capturador raiz não supre porque *aquele* `UnitData`
não tem `isSupplier`. Outro capturador pode ter.

Cada raça é uma coluna em branco sendo preenchida:

| raça | coluna que preenche | estava marcada como |
|---|---|---|
| capturador **raiz** | — | a linha base |
| capturador **agressivo** | `PodeMirar` + `PodeCapturar` | já existe |
| capturador **field medic** | `PodeSuprir` | *(não se aplica)* |
| capturador **peão** — carrega caixas na mochila | `PodeTransferir` | *(não se aplica)* |
| capturador **transportador** — leva o outro nas costas | `PodeEmbarcar`/`PodeDesembarcar` pelo lado do veículo | *(não se aplica)* |
| capturador **vigilante** — o spotter que a artilharia pede, ou sniper | `Ver/Detectar` | ⚠️ respondido à mão |

### O mecanismo já existe

Nenhuma dessas precisa de `UnitRole` novo — e a lição do `RaidAntiSub` é
exatamente essa. O `UnitRoleCompatibility.CanSatisfy` já faz a ponte, e o
comentário dele já declara a doutrina:

```csharp
// Capacidade mecânica é a fonte de verdade: quem carrega satisfaz Transportador
// e quem supre satisfaz Logística — sem precisar de papel híbrido.
if (requestedRole == UnitRole.Transportador && data.isTransporter) return true;
if (requestedRole == UnitRole.Logistica && data.isSupplier) return true;
```

O **field medic já é expressável hoje**: `roles: [Capturador]` + `isSupplier`. O
jogo já o reconhece como logística quando alguém pergunta. O que falta é a
política: ninguém pergunta *"capturador, o que você faz com sua capacidade de
suprir?"*, porque o `AIController.Capturer` nunca chega perto do `PodeSuprir`.

```text
ficha diz o que a unidade PODE          →  isSupplier, isTransporter, skills
matriz diz o que o papel FAZ com isso   →  a política daquela coluna
```

Mesma divisão da captura: a chave é da construção, o rendimento é do par, e o
papel decide se prioriza.

---

## Raças mistas — cadeia dentro da coluna

As raças acima **acrescentam** colunas. As mistas fazem outra coisa: elas
**encadeiam parentes dentro de uma coluna**.

### Labradoodle — o capturador combatente

> *Vira mistura de Fire Support com Assault. Consulta o `PodeMirar` do FS, e se
> não der, o `PodeMirar` do Assault.*

Não é "meio de cada". É **ordem**: tenta a resposta do primeiro parente; não
havendo solução, tenta a do segundo. A doutrina já descreve isso para o
Artilheiro Combatente — `docs/AI Behavior/rascunho de governanca.md`, linhas
758-760:

> *É principalmente uma unidade de Assault, mas tenta primeiro utilizar suas
> armas de longo alcance. Quando não encontra uma solução de tiro à distância,
> continua o avanço e combate por contato.*

E a linha 554 do mesmo documento nomeia a cadeia sem rodeio: *"Quando não
encontra uma solução válida de longo alcance, **passa para Assalto**."*

E o `CanSatisfy` já registra o parentesco:

```csharp
case UnitRole.ArtilheiroCombatente:
    return requestedRole == UnitRole.Assalto
        || requestedRole == UnitRole.FogoIndireto;
```

**A forma é a mesma da lista de atração do `MelhorCapitao`:** sequência ordenada,
a primeira faixa que produzir candidato vence, mesmo que a seguinte pareça mais
próxima. O que vale para "quem eu sigo" vale para "como eu luto".

### Caramelo — o porta-aviões

> *Mistura de Transportador com supridor, estoquista e Fire Support. Consulta
> pernas em vários papéis, mas em essência é um transportador.*

Duas coisas ficam claras aqui, e valem para toda raça mista:

**1. Existe uma essência.** `roles[0]` é o que a unidade **é** quando as colunas
discordam. O porta-aviões que pode atirar, suprir e estocar continua sendo
transporte — se a agenda de transporte e a de fogo pedirem coisas opostas, o
transporte ganha. Sem essência declarada, a mistura vira empate sem
desempatador.

**2. As demais colunas não são secundárias — são só outras colunas.** O
porta-aviões supre *de verdade*; ele apenas não deixa de ser transporte por
causa disso.

### O que isso pede da matriz

Uma célula deixa de ser "uma política" e passa a poder ser **uma cadeia**:

```text
AI Role: Capturador Combatente
  PodeMirar   FireSupport → Assault      (tenta longe; sem solução, contato)

AI Role: Porta-aviões        essência: Transportador
  PodeEmbarcar     Transportador
  PodeDesembarcar  Transportador
  PodeSuprir       Logística
  PodeTransferir   Estoque
  PodeMirar        FireSupport
```

Três formas de célula, e todas já existem em algum lugar do projeto:

| forma | exemplo já implementado |
|---|---|
| **política única** | `MelhorCaptura` — o capturador tem uma só |
| **cadeia ordenada** | lista de atração do `AICaptainData` |
| **herdada do parente** | `CanSatisfy` traduzindo papel especializado |

**Nenhuma raça nova precisa de código.** Precisa de ficha, essência declarada e
linha na matriz.

---

## Preferência não é identidade — a morte dos papéis secundários

**Avaliação, não plano de execução.** Direção aceita; a sequência no fim da
seção é a parte que muda o quando.

### O problema, na formulação do autor

> *No dia que eu inventar o Field Medic, ele vai precisar de um papel novo só
> pra dizer que "logística" pesa mais que "captura"? Hoje o Capturador Combatente
> diz que capturar é a prioridade secundária, e por isso criamos um papel só pra
> ele. A combinação explode em 10×10.*

Correto, e o nome disso é preciso: **`CapturadorCombatente` não é uma identidade,
é uma ordem entre duas agendas.** Cadastrar cada cruzamento como `UnitRole` faz o
catálogo crescer no quadrado das características.

```text
Soldado        essência Capturador · capacidades Capturar          · padrão
Bazooka        essência Capturador · capacidades Capturar, AT      · Agressivo
Metralhadora   essência Capturador · capacidades Capturar, defesa  · Defensivo
Field Medic    essência Capturador · capacidades Capturar, Suprir  · Logística antes de Captura
```

### Não são quatro conceitos novos — é um

A proposta separa **capacidade / essência / preferência / sensores**. Três já
existem:

| conceito | onde já mora hoje |
|---|---|
| capacidade | `isSupplier`, `isTransporter`, armas, skills |
| essência | `roles[0]` — já é o desempate |
| sensores | os `PodeX` |
| **preferência** | **não existe** |

Isso encolhe a obra de "refundação" para **um campo novo**. Vale registrar
porque a proposta parece maior do que é.

### O teste para separar trait de chave

O colaborador do autor avisa para não misturar preferências com skills mecânicas
como `Alpino`. Está certo, e o motivo é derivável do manual — **a direção da
leitura**:

| | quem lê | exemplo |
|---|---|---|
| **chave** | o **mundo** lê sobre você | a montanha pergunta se você é alpino; a construção pergunta se você tem a chave |
| **trait** | **você** lê sobre si mesmo | só o próprio laço de decisão consulta |

O teste já classifica o caso difícil da `v7.0.2`: `Capturador Alternativo` é
**chave** (a construção lê, e por isso a eficiência mora no par `chave ×
construção`); `Agressivo` é **trait** (ninguém no mundo pergunta se você é
agressivo).

### ⚠️ A distinção que a proposta apaga

A lista de mortes junta dois mecanismos **diferentes**:

```text
CapturadorCombatente   combater ANTES de capturar      ordem ENTRE colunas
ArtilheiroCombatente  tiro longo, senão contato       cadeia DENTRO da coluna
```

O segundo não reordena agendas — responde **uma pergunta só** (`PodeMirar`)
consultando dois pais em sequência. É o Labradoodle desta mesma revisão.

**Se o sistema de preferências só souber reordenar colunas, o Artilheiro
Combatente não é expressável e volta como papel.** São necessários os dois.

### O reframe que torna isso pequeno

O router **já é a lista de prioridade** — só que fixa e global:

```text
AIController.Router.cs
  auto-reparo → desbloqueio de produção → transporte → papel → HexEvaluator
```

Então "Action Priority" não é arquitetura nova. É: **a ordem do router deixa de
ser código e vira dado da ficha.**

### Ordem estrita não elimina o arbitrário — muda de lugar

A proposta argumenta que faixas ordenadas evitam comparar "87 pontos de
suprimento com 92 de captura". Verdade, mas repare no que segura a faixa 1:

```text
1. Suprir CRÍTICO     ← a palavra "crítico" é que faz a faixa funcionar
2. Capturar
3. Combater
```

Sem o limiar, **o Field Medic em cima de um capturável, com um aliado a 90% de HP
ao lado, nunca captura.** Ordem lexicográfica ignora magnitude por construção.

Logo: **cada faixa precisa de um predicado de entrada**, e o ajuste fino migra do
peso para o limiar. É um ganho real — limiar é legível, peso não — mas não é "sem
números"; é números em outro lugar. Entrar de olhos abertos evita a sensação de
regressão quando aparecer "e se crítico fosse 30% em vez de 25%".

### O que morre, contado

| papel | veredito |
|---|---|
| `CapturadorCombatente = 12` | morre → `Capturador` + trait |
| `ArtilheiroCombatente = 13` | morre → cadeia dentro de `PodeMirar` |
| `AntiaereoCombatente = 14` | morre → mesma forma |
| `TransportadorAereo = 15` | ~~sobrevive como preferência de compra~~ → **morre**, ver auditoria abaixo |
| `Antiaereo = 10` | ~~sobrevive — capacidade de arma real~~ → **morre**, ver auditoria abaixo |
| `Vigilancia = 6` | sobrevive — agenda real, camada como parâmetro |
| ~~`RaidAntiSub = 11`~~ | já morreu na `v7.0.3`: era capacidade + camada |

⚠️ **As duas linhas tachadas são erro desta revisão, corrigido na auditoria da
próxima seção.** Ficam visíveis de propósito: as duas foram defendidas com o
argumento que a própria doutrina refuta.

Ganho colateral: `CanSatisfy` e `ResolveCompositionRole` existem hoje quase só
para traduzir esses híbridos. Três mortes esvaziam os dois.

### Sequência — a única objeção de fundo

Traits são **degrau 4** ("variação de papel vira parâmetro"). O degrau 3 tem
**1 linha de 7**.

O caso concreto que mostra o risco: o `Agressivo` de hoje está espalhado —
inclusive o `roles[0] == CapturadorCombatente` com 50% hardcoded no
`GetCapturePower`, que continua de pé. Transformá-lo em trait agora não
centraliza a política; **move o espalhamento para um campo novo.**

> **Não dá pra parametrizar uma política que ainda não foi extraída.**

Quando as 7 linhas existirem, a diferença entre `Capturador` e
`CapturadorCombatente` estará visível como **duas células divergentes numa linha
só** — e aí o trait é a forma óbvia de registrar a divergência. Hoje seria
palpite.

É o mesmo padrão da `v7.0.3`: lá o **órgão unificado destrancou a coluna**; aqui
a **linha extraída destranca o trait**.

---

## Auditoria da seção anterior — quatro correções

Revisada por auditor externo, com um acréscimo do autor. **Duas correções são
erro da seção acima**, não refinamento.

### 1. ❌ `Antiaereo` — o veredito estava contraditório

A seção acima o salvou dizendo *"sobrevive — capacidade de arma real"*. Pela
doutrina aplicada duas linhas antes, **isso é motivo para morrer**: capacidade
pertence à ficha, não ao papel.

Ele só sobreviveria se significasse uma **política** — priorizar alvo aéreo,
proteger um capitão, guardar corredor. Se significa "consegue atirar no ar", é
capacidade.

**E o dado já existe** (observação do autor, verificada):

```csharp
// WeaponData.cs:47-49
[Tooltip("Dominios/alturas adicionais onde a arma pode operar.")]
public List<WeaponLayerMode> aditionalDomainsAllowed;   // Domain + HeightLevel
```

O sistema **já pode ver que a arma atira pra cima**. Perguntar
`roles.Contains(Antiaereo)` é perguntar à etiqueta o que a arma declara — o erro
exato do `RaidAntiSub`.

### 2. ❌ `TransportadorAereo` — também morre

A seção acima o salvou como "preferência de compra, não de ação". Mas a demanda
**já sabe carregar camada**, e o precedente é da própria `v7.0.3`:

```csharp
antiSub.RequiredVisionDomain = Domain.Submarine;   // AIShoppingPlanner.Demand.cs
```

`RequiredDomain = Air` numa demanda de `Transportador` é a mesma peça. Uma
preferência de composição pode viver no shopping **sem ocupar um `UnitRole` que a
IA de ação também lê**.

### 3. ⚠️ Nem todo o router pode virar dado

A seção acima disse "a ordem do router deixa de ser código e vira dado da ficha".
**Incompleto, e perigoso se lido ao pé da letra.** O router mistura duas
espécies, e só a segunda é reordenável:

```text
Router.cs  43–85    auto-reparo · desbloqueio de produção · transporte
                    ── INVARIANTE. Nunca ultrapassável por trait ──
Router.cs  107+     rebelde · capturador · assalto · FS · vigilância ·
                    combate aéreo · logística · estoque · HexEvaluator
                    ── AGENDA. Só esta parte aceita reordenação ──
```

Obrigação transacional e gate universal ficam **acima** da política. A fronteira
já está desenhada no arquivo; o que falta é declará-la para que ninguém a
atravesse por engano.

### 4. O campo novo aponta para um perfil, não para uma lista de enums

```text
AIBehaviorProfile
  essência
  faixas de ação + predicados de entrada
  política/cadeia por coluna
  traits modificadores
```

Continua sendo **um** campo em `UnitData`, mas apontando para dado estruturado.
Precedente duplo no projeto: `AIPresetData` e `AICaptainData`.

Ganho que não estava na proposta: **perfis são compartilhados.** Dez fichas de
fuzileiro apontam para o mesmo perfil; só Bazooka e Metralhadora divergem. A
explosão combinatória morre também no nível de asset.

---

### O "7 perfis" do critério de aceite — verificado

A tabela de `## As linhas` classifica 14 papéis vivos: **4 principais**
(Capturador, Assalto, FogoIndireto, Vigilância), **4 de apoio** (Transportador,
Logística, Estoque, TransportadorAéreo) e **6 secundários**.

São 8 sobreviventes — *a menos que* `TransportadorAereo` morra (correção 2). Aí:

```text
14 papéis  =  7 que ficam  +  7 que morrem
              4 principais    6 secundários
            + 3 de apoio    + TransportadorAereo
```

**O critério de aceite do autor já contava 7.** Quando "os 7 perfis chamando uma
fonte única" foi escrito, já assumia que o Transportador Aéreo colapsa. A
correção 2 não propõe — **lê o que já estava decidido**.

---

### Os seis secundários não morrem do mesmo jeito

Acréscimo desta revisão. São dois grupos, com mecanismos distintos:

| grupo | papéis | mecanismo |
|---|---|---|
| **preferência / cadeia** | CapturadorCombatente, ArtilheiroCombatente, AntiaereoCombatente | ordem entre colunas, ou cadeia dentro de uma |
| **agenda + camada** | Interceptador, AtaqueAereo, Antiaéreo | **uma agenda só, parametrizada pela camada do ALVO** |

O segundo grupo **não é caso de trait — é a solução da Vigilância outra vez.** Lá
foi uma agenda com a camada da *visão* como parâmetro; aqui é combate aéreo com a
camada do *alvo* como parâmetro, e quem responde é o `aditionalDomainsAllowed` da
arma.

Consequência prática, e ela encurta o caminho:

```text
3 papéis  →  traits          (esperam o degrau 4)
3 papéis  →  parâmetro de camada  (é órgão unificado — não espera)
1 papel   →  campo de demanda no shopping
```

**Nenhum dos sete precisa esperar o sistema de traits ficar pronto**, e dois dos
três caminhos já foram percorridos este ano. O grupo "agenda + camada" entra na
**fila dos órgãos**, não na do degrau 4.

---

## Critério de pronto da matriz

O teste do autor, inalterado:

> **Os 7 perfis chamando uma fonte única, não 7 perfis com 7 definições
> diferentes.**

Operacionalmente, um papel está migrado quando:

1. as 12 colunas dele estão declaradas — inclusive as "(padrão)" e as "(não se aplica)";
2. nenhuma delas contém cálculo próprio de alcance, elegibilidade ou visão;
3. trocar a política de uma coluna não exige recompilar — ou é dado, ou é
   parâmetro passado pelo chamador;
4. um `UnitData` novo com as chaves certas entra no papel **sem uma linha escrita
   para ele**.

O item 4 já passou uma vez: o jipe capturador.
