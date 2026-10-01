# v8.1.1 — Cinco portas em série, e a última é um aperto de mão pela metade

O dia começou com uma pergunta simples do autor — *"por que o helicóptero foi
buscar e o navio ficou parado?"* — e terminou com uma cadeia de resgate
multimodal a funcionar até ao penúltimo passo.

O fio não é nenhuma das cinco correções. É o que elas têm em comum:

> **Cinco vezes, o que barrava era prazo, política ou formação vestidos de fato
> — sempre dentro de um teste que se chamava estrutural.**

E o dia produziu também o seu contrário: **a quinta correção fez o navio começar
a vaguear**, e a causa fui eu. Está na seção própria.

---

## O método que tornou o resto possível

As duas primeiras portas eu achei lendo código. A terceira eu errei. A partir
daí parei de adivinhar e pus um instrumento:

```csharp
// TransportOperations.cs — ResolveRejection
string ResolveRejection(MelhorEmbarqueOption option) { ... }
```

O `isMaterializable` do Pickup tem **cinco cláusulas** e todas somem pelo mesmo
`false`. Com 48 opções recusadas e seis tiers em `miss`, o log dizia exatamente
o mesmo que diria se não houvesse opção nenhuma. O contador por motivo custa
nada e transformou cada corrida seguinte em **resposta** em vez de palpite:

```text
5 Pickup[Strategic] recusa 48 opcoes: tier=Operational!=Strategic=12 · rotaPax=NoCurrentRoute=36
```

Da quarta porta em diante, cada corrida do autor apontou a próxima sozinha.

---

## As cinco portas

### 1. `MeetingWalkTurns = 2` — prazo dentro do filtro estrutural

`CanTransporterMeetPassenger` respondia *"os dois se encontram?"* de forma
assimétrica: componente completo do transportador contra **2 turnos de
caminhada** do passageiro. Chamava-se `IsStructurallyEligible`, e estrutural é
topologia — orçamento de caminhada é "dá tempo?", que é ranking, uma camada
acima.

O efeito era invisível no ar e fatal no mar: o Chinook passava porque o
componente aéreo encosta em todos; o navio reprovava porque a praia ficava a 3
turnos do soldado.

**Agora:** componente ∩ componente, com o passageiro a ganhar componente próprio
pelo mesmo cache **por perfil** que o transportador já usava, e o memo chaveado
pelos **dois componentes** — quatro soldados iguais na mesma ilha são uma
pergunta só.

Os *43 segundos* que a docstring citava eram de um desenho anterior, sem cache.
Medido depois: `MobilityComponentBuilds: 1` e `2`, `TouchTests: 2`. Sem
regressão.

### 2. A fonte de âncora chumbada em "capturável que eu capturo"

`EvaluatePickupRideNeed` só deixava perguntar carona a quem satisfaz
`UnitRole.Capturador`. Todo o resto caía em "emergência de reparo, ou nada" —
e um APC cheio de soldados no lado errado do canal **nunca pedia o navio**,
porque a única fonte de coordenada embutida na pergunta era um capturável.

O autor foi direto ao ponto: *"os transportadores não podem se importar com a
missão de seus passageiros, só aonde eles querem ir"*.

**Agora:** `TryResolveCargoDestinationAnchor` resolve o destino da carga e
pergunta com `useExplicitTarget`. Cai em `ApplyBeyondReachRideNeed` — o ramo que
já era agnóstico de captura e responde por topologia pura.

```text
[QueroCarona] pax=#8 (carregado) nao alcanca destino da carga (25,-2,0)
              SEM ROTA PRÓPRIA: aceita carona
```

### 3. Horizonte por tier — o Strategic negava a si mesmo

`CanMaterializePickupRendezvous` recusava `NoCurrentRoute` **e**
`ReachableStrategic` juntos. Mas não são a mesma coisa: o primeiro é
impossibilidade, o segundo é distância. Com os dois barrados, o tier Strategic —
orçamento infinito — só aceitava estados de rota que cabem no Operational.

**Agora** cada tier aceita até o seu horizonte, e só.

⚠️ **Eu classifiquei esta mudança como "correta em doutrina, não era o
bloqueio". Estava errado.** A opção vencedora do navio é
`rotaPax=ReachableStrategic` — esta correção é carga, não decoração. Não era
*suficiente* sozinha, que é coisa diferente de não ser necessária.

### 4. Flags invertidas entre dois pedidos ao `MelhorEmbarque`

```text
A (696) — alimenta os tiers do Pickup:  includeStrategic = true,   longRange = false
B (870) — projeção por passageiro:      includeStrategic = false,  longRange = true
```

O pedido que produz LZs de tier Strategic nunca construía o mapa de encontro de
longo alcance do passageiro. Todo passageiro fora do orçamento now/later voltava
`NoCurrentRoute` — 48 opções, 48 recusas.

**Agora andam juntas:** quem pede alcance estratégico paga o mapa estratégico.
Custo medido: `melhorEmbarque.longRangeMap: 13,8ms/4`. Não foi ele que pesou.

### 5. Retaguarda é formação, não segurança — e a prudência é da ficha

`IsTransportStrategicTargetSafe` exigia `InRearSlice` da LZ estratégica. Mas o
ponto de recolha de um resgate está, por definição, **onde a tropa ficou** —
fora da linha. Todas as 36 opções estratégicas eram recusadas, turno após turno.

A primeira versão que escrevi tirou a retaguarda de toda a gente. O autor
corrigiu, e a correção é melhor:

> *o porta-aviões e o caminhão de suprimentos declaram `playConservative`; os
> outros — Chinook, hidroavião, APC, trem de carga, navio de desembarque — são
> loucos varridos.*

A regra nasceu para proteger **a carga** (artilharia de campanha no caminhão),
não o ofício. Estava no papel, e devia estar na ficha.

**Agora:** sem `playConservative`, não lhe é perguntado. Com, mantém retaguarda
**e** hex sem ameaça. Isso *devolve* a retaguarda ao porta-aviões, que a minha
versão tinha tirado.

O autor corrigiu o `AR Hidroaviao.asset` (`playConservative: 1 → 0`) na mesma
passada — o asset discordava da lista que ele tinha em mente.

---

## O resultado, no log

```text
Soldado#1  embarca (ext 2h) → APC#8 slot 0
           [Missao] 1 Capture -> (25,-2,0)

APC#8      [QueroCarona] pax=#8 (carregado) nao alcanca destino da carga
           SEM ROTA PRÓPRIA: aceita carona

Navio#5    [Promessa] #5 promete resgate de pax=#8 em (46,-2,0)
           Pickup[Strategic] hit  rotaPax=ReachableStrategic
           moveu para 36,0
```

Soldado → APC → navio. Cada degrau faz a **mesma pergunta** ao degrau seguinte,
que é o desenho que o autor descreveu antes de existir código para ele.

---

## O que NÃO terminou

### O navio persegue um passageiro que foge — e a causa fui eu

As cinco correções somadas abriram o oceano inteiro como lista de candidatos, e
**nenhuma delas deu ao navio motivo para ficar**:

```text
portão topológico  + passageiros distantes viram candidatos
longo alcance      + eles ganham rota
horizonte por tier + o Strategic passa a aceitá-los
playConservative   + nada o segura perto
```

O ciclo observado no T4:

```text
1. navio promete #8 e navega até o encontro
2. #8 chega a sua vez e escolhe Delivery — dirige para longe
3. o encontro evapora; a opção de #8 sai da lista
4. o farol da promessa não a acha, e cai para o próximo passageiro
5. repete
```

**O navio não tem histerese.** É o mesmo defeito que o capturador já teve
(`project_axis_capturer_balance`: otimizador global sem aderência ao objetivo
anterior). A promessa devia ser a histerese, mas o farol só pesa quando a opção
do prometido **está na lista** — fora dela, ela não perde para nada, ela não
existe.

Endereço: `TryQueryTransportPickupOperation` — promessa pendente cujo passageiro
não é materializável neste turno deve **segurar posição**, não trocar de
passageiro. *"Promessa não é preempção"* continua certo; *"promessa evapora em um
turno"* é o que acontece hoje.

### O APC carregado pede carona e não pode aceitá-la

```csharp
// TransportOperationsService.BuildAttempts
if (caps.CanTransport && context.HasCargo) { Courier; Delivery; return; }
```

Transportador com carga só considera Courier e Delivery. Nunca lhe é perguntado
`Embarcar` — que a ficha do papel manda ser **o topo nos dois modos**, por causa
do aninhamento.

⚠️ **Registro de um erro meu:** confundi o *"Embarcar do transportador"*
(aninhar-se em outro veículo) com o *"embarcar da unidade"* (o passageiro subir
nele). São perguntas diferentes; a que falta é a primeira, feita ao APC.

As duas pendências acima são **as duas metades do mesmo aperto de mão**: uma faz
o táxi esperar, a outra faz o passageiro subir. Nenhuma sozinha fecha o ciclo.

### O defer da iniciativa — compilado, não rodado

`ShouldDeferPassengerForIncomingTransport`: quem está na fila de carona cede a
vez a um transportador que ainda não agiu, tem vaga, está a ≤ MP+1 e encosta por
topologia. Segue o padrão dos cinco defers que já existiam, com a guarda
`!secondPass` — que é o que impede a cessão mútua com o `cede destino` do
bloqueio.

Motivo: o passageiro agia primeiro, marchava para longe, e só então o
transportador chegava. Sinergia desfeita pela **ordem**, não pela decisão.

**Nunca rodou.** Sinal esperado:
`Fase2 — Soldado#N espera carona e cede vez para APC#M encostar primeiro.`

### Perf: o laço de LZ

`Chinook#9 decision=1013ms`, com `melhorEmbarque.lzLoop: 697,7ms` e
`lzGates: 500,2ms` sobre `MelhorEmbarquePairs: 2356` e 1050 células candidatas.
O mapa de longo alcance que liguei custou `13,8ms/4` — não é ele. Vale medir num
turno cheio antes de aceitar como normal.

### Sete métodos possivelmente mortos

Varredura por ocorrência única em `Assets/Scripts` inteiro:

```text
DecideAirShuttleAction · DecideIdleTransportReturnAction · DecideNavalPickupAction
DecideRogueShuttleAction · TryDecideAirEvacShuttleAction · TryFindAttackFromCell
TryFindTransportCourierAttack
```

O último é o T7 que a `Transporte.md` já dava como morto. **Os outros seis não
foram confirmados um a um** — trate como suspeita, não como fato.

---

## Doutrina que o dia produziu, e ainda não está escrita nas fichas

O autor desceu o modal de sensores à mão para quatro casos do capturador e três
do transportador. O que apareceu:

**O modal não é uma cadeia "o primeiro que responde ganha". É um funil que
preenche quatro campos:**

```text
âncora       PARA ONDE            Capturar, Enxergar, Detectar
movimento    ATÉ ONDE hoje        Reposicionar
etiqueta     O QUE fazer lá       Capturar, Mirar, Desembarcar, Suprir, Fundir
publicação   o que fica no mundo  Embarcar → "quero carona"
```

E essa forma **já é o `PlayerAction`**: todos os builders do motor são
`move + uma ação`. O modal não precisa de máquina nova — é a receita de montar o
batch que já existe.

Consequências que caíram sozinhas:

- **Subpapéis terrestre/aéreo/naval não devem existir.** O mesmo funil serve
  trem (o móvel mais preso do jogo) e hidroavião (o mais solto). O teste que
  sai disso: *se um passo do modal responderia diferente para o trem e para o
  hidroavião, ele não é um passo — é resposta de sensor.*
- **`TouchesComponent` é a forma geral de "praia".** Estação, helipad, convés e
  praia são a mesma pergunta: onde os dois componentes se encostam. O jogo nunca
  vai precisar catalogar praia.
- **A escada de âncora é compartilhada entre papéis:** EVAC → casa → retaguarda
  da massa → fica. O transportador vazio pula do degrau 2 para o 4 porque o
  degrau 3 (`TryBuildConservativeRearFollowAction`) exige direção de frente
  conhecida — e "retaguarda da massa" é derivável das âncoras da própria massa,
  sem inimigo nenhum.
- **O campo `publicação` é o barramento entre papéis.** Já tem quatro
  moradores: quero carona, preciso de EVAC, não me fundam, preciso de
  suprimento. Nenhum papel chama outro; todos publicam e leem.
- **`isStranded` é fato de topologia, mas só é medido contra capturáveis.** Para
  quem não captura, o encalhamento não tem sujeito.
- **Uma publicação só vale se o solicitante puder aceitar a ajuda.** Um soldado
  com 0 de autonomia não embarca nem com o transporte ao lado (embarque custa
  1–2 de terreno) — e o anti-fome promove esse pedido a *inegociável* depois de
  3 turnos. O sensor que responde isso, `PodeEmbarcarSensor`, já é do passageiro
  e recebe o MP restante; só não é consultado antes de publicar.
