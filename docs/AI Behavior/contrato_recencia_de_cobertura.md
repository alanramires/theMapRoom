# Contrato — recência de cobertura de detecção

**Estado:** desenho fechado em conversa, **nada existe no runtime**. Cada seção
marca o que é contrato e o que é código de hoje. Escrito depois da sessão de F11
do turno 1 que levantou o censo das nove unidades de vigilância.

> Regra de leitura: onde estiver **HOJE**, é código verificado. Onde estiver
> **CONTRATO**, é decisão tomada e não escrita. Onde estiver **ABERTO**, ninguém
> decidiu.

---

## 1. O problema

**HOJE** o score da Vigilância distingue três estados de uma célula: coberta
agora, inexplorada, e explorada-mas-descoberta. Ele **não** distingue "foi
observada no turno passado" de "ninguém olha há dez turnos" — `RecoveredWeight`
é escalar fixo, sem idade (`MelhorVisaoService.cs`, `VisionCoverageScoringPolicy`).

A consequência foi medida no turno 1 de uma partida real:

```text
Fragata #79   hold   vis=58  marginal=38  mantem=38  novo=0   →  gain 1,9
Fragata #84   move 5 vis=46  marginal=7   overlap=39 novo=7   →  gain 137,5
```

`unexploredMarginalWeight: 25f` responde por ~98% do score. A moeda é **névoa**,
não contato. E como névoa não regenera, com o mapa explorado `novo → 0` para
todos os caçadores ao mesmo tempo e todos congelam no estado da #79 — segurando
uma rede de 58 células que o serviço avalia em 1,9.

Sem idade não nasce rota de patrulha: só existe a melhor fotografia instantânea.

---

## 2. O ledger

**CONTRATO.**

```text
recência[slot, perfilDeCobertura, célula] = ultimaRodadaObservada
idade = rodadaAtual − ultimaRodadaObservada
```

Carimbo, **não** contador. Nada envelhece: não existe varredura por turno sobre
o mapa. Célula sem carimbo tem idade ∞.

### 2.1 O perfil de cobertura — e ele já está escrito

**HOJE**, `AIController.Vigilancia.cs:421-435`:

```csharp
private static bool IsEquivalentSurveillanceObserver(ally, observedProfile)
{
    ... !allyProfile.Layer.Equals(observedProfile.Layer)  → false
    return !observedProfile.DetectsStealth || allyProfile.DetectsStealth;
}
```

Isso **é** `(domínio, altura, detectaStealth)` com a treliça de subsunção pronta:
quem detecta stealth serve a uma necessidade comum; o contrário não.

**CONTRATO:** o ledger **chama essa função**, não reimplementa a equivalência.
Duas definições do mesmo fato foi o que produziu o par ícone-do-hex × Jornal.

**CONTRATO:** a subsunção tem **um eixo só — stealth**. Camada nunca subsome
camada. Um radar antiaéreo não visita o oceano submarino, embora cubra o ar
acima dele; uma fragata antissubmarino não rejuvenesce a vigilância aérea da
mesma célula.

### 2.2 Máscara espacial por perfil

**CONTRATO.** Um sistema só, com universos espaciais por perfil — não dois mapas
rígidos. Dois conjuntos distintos:

```text
mapa de posições    onde o sensor pode ficar
mapa de cobertura   onde o alvo procurado pode existir
```

| perfil | posições candidatas | células cuja idade importa |
|---|---|---|
| aéreo | quase toda a projeção do tabuleiro, sujeita a autonomia, recuperação, ameaça e ocupação | o tabuleiro inteiro — aeronave inimiga ocupa o ar sobre mar e terra |
| naval / submarino | oceanos, canais e faixas navegáveis alcançáveis | só o habitat daquela camada; componentes aquáticos desconectados ficam separados |

`Naval/Surface` e `Submarine/Submerged` **não** são uma decisão nova: já são
chaves diferentes pelo `(domínio, altura)`.

**HOJE** a máquina de componentes desconectados existe e roda — a frase
`#84 descarta pax=#173: componentes de movimento nao se tocam` saiu do log do
turno 1. A máscara naval reusa isso; não é peça nova.

Consequência de doutrina: o **mesmo** cálculo de idade produz comportamentos
espaciais diferentes porque a topologia difere. A patrulha naval percorre
corredores e volta por canais antigos; a aérea forma órbitas largas ou varre
faixas. Ninguém programa rota circular.

---

## 3. Quem escreve, e onde

**CONTRATO.** A escrita acontece **só depois do compromisso, em `Neutral`, a
partir da cobertura confirmada**. Avaliação de posição hipotética nunca carimba —
é a invariante transacional, e "posição hipotética criando conhecimento" já está
na tabela de armadilhas do projeto.

**CONTRATO — onde pendurar.** Uma varredura por sensor, no fim do turno. **Não**
dentro do delta incremental de FoW.

O motivo é medido:

```text
visionCoverage           ~8,5 ms/chamada   (927,9 ms / 109, Super Tucano #107)
5 sensores × 1 chamada   ~40 ms/turno      barato
delta incremental FoW    ~210 ms, 66×/turno no turno colado
```

E são perguntas **diferentes**: o compromisso pergunta *"que unidades eu
detecto"*; o ledger pergunta *"que células pegariam um alvo da camada L"* — a
consulta com `forceVirtualTargetLayer`. Uma não sai da outra de graça.

### 3.1 Onde o ledger NÃO mora

**CONTRATO.**

- **Não** em `FogKnowledgeSnapshot`. Ele se declara fotografia instantânea e
  somente consultiva, sem memória histórica
  (`FogKnowledgeSnapshotBuilder.cs:45-48`).
- **Não** no `AIIntelLedger`. Contato lembra uma **unidade**; patrulha lembra a
  **recência de uma área**. São entidades diferentes.

**CONTRATO.** Persiste no save. Nada foi distribuído — o formato nasce do jeito
certo, sem shim nem leitor retrocompatível.

---

## 4. A urgência

**CONTRATO.**

```text
coberta agora por sensor equivalente    urgência 0
já coberta nesta rodada                 urgência 0
coberta anteriormente                   idade, com teto
nunca coberta                           prioridade máxima
```

```text
scorePatrulha = Σ urgência das células que seriam cobertas dali
```

O somatório faz rede grande ganhar — que é o desejado, e é de onde sai o "disco
perfeito" sem regra especial: posição com o disco cortado pela borda cobre menos
células e pontua menos sozinha.

### 4.1 O número que É a doutrina

**CONTRATO — e precisa ser declarado, não emergente.** O somatório põe duas
coisas para competir:

```text
169 células de idade 10 (no teto)   vs   20 células nunca cobertas
```

Quem vence é decidido **inteiramente** pela razão entre o **teto da idade** e o
valor de **nunca coberta**:

- teto baixo → o sensor pasta perto de casa varrendo o mesmo mar velho;
- nunca-coberta alto demais → fura para a fronteira e nunca volta; a patrulha
  deixa de existir.

Escrever como *"nunca coberta vale N vezes o teto"*, num lugar só, em vez de
deixar nascer de dois pesos ajustados separadamente.

**ABERTO:** o valor de N.

### 4.2 "Preto" não é preto geográfico

**CONTRATO.** Para o sensor, *nunca coberta* significa **nunca coberta pela rede
de detecção naquela camada** — não FoW geográfico preto. Um EWACS varre o céu
sobre terreno preto sem revelar o chão.

Consequência que vale sozinha: o consumidor de detecção fica **sem nenhuma
dependência de névoa**. Não lê `IsExplored`, não lê `IsKnown`, não recebe
`FogKnowledgeSnapshot` — dois delegates e uma fotografia a menos num laço que
roda ~100 vezes por decisão.

---

## 5. As três responsabilidades

**CONTRATO.**

```text
serviço burro    "o que este sensor cobriria desta posição?"     sem política
ledger por slot  "quando esta cobertura foi confirmada?"          sem política
Vigilância       idade, segurança, âncora e custo de movimento    só política
```

E o lugar disso no paradigma dos três consumidores:

```text
MelhorVisao       memória geográfica / hex revelado
MelhorDeteccao    cobertura atual + idade da última varredura
MelhorSpotting    contato sobre uma célula/camada específica
```

---

## 6. A escada da Vigilância

**HOJE** existe `SurveillancePolicyStage` (`AIController.Vigilancia.cs:37-48`)
com nove degraus, e o ramo aéreo passa por ela. O ramo de camada
(`TryDecideLayerSurveillanceAction` — submarino, fragata, Super Tucano ASW)
**não passa por escada nenhuma**: vai direto ao score e obedece.

**CONTRATO.** A escada ganha um degrau acima de `ImproveAirCoverage`, e o ramo de
camada passa a ter a sua:

```text
aérea    contato na minha rede  →  segura iluminado, NÃO chama o serviço
         senão                  →  patrulha
naval    contato na minha rede  →  Melhor Combate (resolve ele mesmo)
         senão                  →  patrulha
```

**CONTRATO — sem estado.** Não há missão a lembrar: o contato está na rede ou não
está, e isso se pergunta de graça toda rodada. Se o alvo saiu do alcance, a
unidade cai em patrulha sozinha. Memória só é necessária quando o fato não é
observável no turno — não é o caso aqui, ao contrário do Fire Support, cujo
passageiro embarcado não se vê da posição.

Isso elimina por construção o modo de falha do sensor trancado perseguindo
fantasma.

**CONTRATO — por unidade.** "Ninguém detectou nada" é **a minha rede**, e é o
**meu alcance** que decide segurar. Leitura simétrica, sem coordenação.

**HOJE, aviso:** existe um `aiSensorPriority` na ficha marcado `LEGADO
AI_Legacy`, sem consumidor compilado. O padrão certo é a escada acima e o
fallthrough ordenado do `DecideUnitAction` — não ressuscitar o campo velho porque
o nome combina.

---

## 7. As duas famílias

**CONTRATO — decidido pelo autor.**

```text
aérea    repele naturalmente (espalhar = cobrir mais área)   TEM capitão/magnético
naval    NÃO repele (subs podem navegar juntos por força     NÃO tem capitão —
         de combate)                                          caça e patrulha
```

Logo: `overlap` negativo **só** na perna aérea; naval fica neutro. `spacing`,
`repel` e `required` do `ImproveAirCoverage` **não** transferem para o ramo de
camada naval.

Massa por força de combate é assunto do Melhor Combate. Premiá-la dentro do score
de detecção seria pôr política de combate na camada de percepção.

### 7.1 Um confundimento a lembrar ao testar

**HOJE**, entre as unidades de vigilância:

```text
EWACS         aérea   playConservative + magnético
Radar Móvel   aérea   playConservative + longRangeStationary
Submarino     naval   —
Fragata       naval   —
Super Tucano  naval   —
```

"É aérea?" e "tem `playConservative`?" dão a **mesma resposta** para todas as
cinco. Política construída sobre o flag passa pelo motivo errado, e o erro só
aparece no dia em que existir uma fragata conservadora. Bifurcar por **família**
primeiro; usar o flag só para o que é postura de verdade — tamanho da passada.

### 7.2 A trela conservadora é portão, não peso

**CONTRATO.** A trela define **quais células são candidatas**; a idade maximiza
**dentro** delas.

Se a trela for peso negativo, ela perde: idade ∞ é o topo da escala e paga mais
que qualquer penalidade de distância. O EWACS vai educadamente para as trevas e o
log dirá que foi uma boa decisão. É a mesma lição do `FocusCells`, que só somava
pontos quando precisava gatear.

**HOJE** o mecanismo da trela existe e está correto:
`FollowMagnet ... CapturerMagnet=#116; magnetDist=7 escort=7`. Falta só ele
filtrar o conjunto que vai ao ranking, em vez de disputar pontos com ele.

---

## 8. O que este contrato NÃO cobre

- **ABERTO:** o valor de N (§4.1).
- **ABERTO:** âncora da patrulha naval além da idade — corredor (aproximações de
  porto, rota entre capturáveis) como base e contato conhecido como peso foi
  recomendado, não decidido.
- **FUTURO:** sensor com duas especializações (ar **e** submarino). Nenhuma ficha
  tem hoje; `SurveillanceProfile` guarda uma camada só. Registrado em
  `docs/ideias_futuras.md`, item 12.
- **FUTURO:** o mesmo ledger servindo o `MelhorVisao` (idade de exploração
  geográfica, chave própria).
