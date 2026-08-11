# Capturer — análise da árvore de perguntas

> **Status:** sondagem de arquitetura, sem alteração de comportamento.
>
> Atualizado em 2026-08-09, depois da `v8.2.0`.

Este documento compara três coisas que não devem ser confundidas:

1. a **doutrina**, em
   [`../AI Behavior/Capturador.md`](../AI%20Behavior/Capturador.md);
2. o **comportamento atual**, inventariado em
   [`../../Assets/Scripts/Match/AI/Units/Capturer/Capturer.md`](../../Assets/Scripts/Match/AI/Units/Capturer/Capturer.md);
3. a **árvore desejada**, em que cada casa elimina uma explicação até sobrar a
   ação correta.

A análise é uma ponte temporária. Quando uma pergunta alcançar a doutrina, o
comportamento confirmado deve ser registrado no `Capturer.md` ao lado do código.
Este arquivo não substitui nenhum dos dois documentos autoritativos.

---

## 1. A frase-mãe

> **Qual é a forma mais barata de transformar esta construção em renda sem
> desperdiçar um capturador?**

Ela deriva diretamente do lema:

> **O capturador adianta a renda do exército.**
> **Nenhum prédio é dele, e o HP é o relógio.**

E da imagem operacional:

> **O capturador é a mosca atraída pela luz roxa. Ele não consegue evitar.**

A consequência para o questionário é:

> **O verde não pergunta “posso fazer isso?”. O verde pergunta “isso resolve o
> problema do meu papel?”.**

`PodeX` continua sendo a fonte de legalidade. A árvore pergunta por que o
Capturador está consultando aquele verbo e o que a resposta elimina.

---

## 2. A árvore desejada

```text
alvo publicado pelo plano, claim residual ou magnetismo
        │
        ▼
CAPTURAR
Existe construção no meu Tactical que precisa da minha ação agora?
        │
        ├─ necessário            → capturar
        ├─ redundante/blitzkrieg → manter objetivo e continuar
        └─ não materializável    → continuar
        │
        ▼
ENXERGAR
Sei o bastante sobre a construção e sua aproximação neste turno?
        │
        ├─ não → procurar posição que esclareça sem abandonar a missão
        └─ sim → continuar
        │
        ▼
DETECTAR
O bloqueio é falta de contato ou geografia conhecida?
        │
        ├─ contato incerto → posição de observação/detecção
        └─ geografia       → continuar
        │
        ▼
EMBARCAR
Consigo cumprir a captura sozinho, por caminho válido, em Tactical ou
Operational?
        │
        ├─ sim → recusar carona e continuar
        └─ não → transporte justificado
        │
        ▼
REPOSICIONAR
Qual posição transforma minha próxima ação em captura?
        │
        ├─ falta informação → spot de observação/aproximação
        ├─ descontinuidade  → passengerMeetingCell do MelhorEmbarque
        ├─ handshake        → LZ já combinada; mover e esperar
        └─ rota própria     → progressão magnética normal
        │
        ▼
MIRAR
A obrigação de captura terminou e minha presença vale mais protegendo a renda?
        │
        ├─ sim → defender/mirar
        └─ não → não deixar o combate sequestrar a profissão
        │
        ▼
FUNDIR
Tenho mais presença de captura do que trabalho paralelo e posso converter a
redundância em capture power?
        │
        ├─ sim → considerar fusão
        └─ não → preservar capturadores independentes
```

`Suprir`, `Transferir` e `Desembarcar` não são prioridades baixas. Eles não
fazem perguntas na árvore do Capturador atual:

- `Suprir` e `Transferir` pertencem a papéis futuros ou a serviços externos;
- `Desembarcar` é decisão do transportador, orientada pelo destino do
  passageiro.

---

## 3. A ordem real do código

### 3.1 Roteador global

`AIController.Router.cs` consulta, antes do papel:

```text
desbloqueio de produção
→ Repair global
→ operações globais de transporte
→ roteador sem HQ
→ Capturer, quando existe plano
```

O caminho sem HQ não é mais outro controlador. `AIController.Rebel.cs` valida o
contexto e chama `TryDecideCapturerAction(unit, snapshot, plan: null)`. Portanto
IA com HQ e sem HQ já compartilham a mesma árvore; o plano só muda a origem do
alvo.

### 3.2 Entrada do Capturer

`AIController.Capturer.cs:21` avalia:

```text
1. Repair novamente
2. handoff Blitzkrieg hard
3. Swap
4. captura na célula atual
5. captura local/rally antes do embarque
6. defesa de prédio aliado antes do embarque
7. embarque, aproximação ou LZ
8. objetivo formal ou ramo rogue
```

O `Repair` já foi consultado pelo roteador e é consultado outra vez na entrada
do Capturer. É duplicação de orquestração, ainda que a segunda chamada normalmente
retorne a mesma resposta ou `null`.

### 3.3 Capturador com plano

`DecideAssignedCapturerAction` avalia:

```text
1. PontaLanca: captura direta na chegada
2. Pursuer: combate ligado ao avanço
3. captura oportunista
4. ramo agressivo
5. célula recomendada de avanço
6. ataque defensivo de oportunidade
7. Explorer
8. scoring conjunto de movimento e ataque
```

### 3.4 Capturador sem plano

`DecideRogueCapturerAction` avalia:

```text
1. ataque da posição atual
2. sob contato: captura oportunista, depois combate
3. captura do alvo alcançável
4. captura oportunista
5. revelação de ocupante oculto
6. move+ataca durante o avanço
7. ramo agressivo
8. marcha magnética
```

Esse desenho ainda é uma cascata de ações: **a primeira ação não nula encerra a
decisão**. A árvore desejada é diferente: cada pergunta produz conhecimento
sobre o problema e só algumas folhas materializam uma ação.

---

## 4. Distância por pergunta

| pergunta | estado | resumo |
|---|---|---|
| `Capturar` | **parcial** | sensor, captura imediata, handoff e swap existem; falta o gate econômico único |
| `Enxergar` | **parcial/distante** | FOW e Explorer existem, mas visível/explorado não formam uma casa autônoma |
| `Detectar` | **parcial/distante** | ocupante oculto e spots existem, mas contato e geografia continuam misturados |
| `Embarcar` | **próximo** | já prova se a perna própria resolve em Tactical/Operational |
| `Reposicionar` | **próximo na LZ** | `passengerMeetingCell` e espera estão feitos; as outras causas continuam espalhadas |
| `Mirar` | **parcial** | defesa de renda está correta; combate também aparece antes e durante a conquista |
| `Fundir` | **distante** | existe apenas fusão de manutenção, não fusão por eficiência de captura |

Em uma frase:

```text
serviços e fatos       próximos
organizador por perguntas distante
folha LZ/espera        implementada
fusão econômica        inexistente
```

---

## 5. Capturar — existe ação; falta a pergunta econômica

### O que já existe

`PodeCapturarSensor` centraliza:

- chave exigida pela construção;
- captura e reconquista;
- capture power e eficiência da chave;
- FOW da hora de agir: célula visível **ou explorada**;
- consulta projetada sem alterar estado confirmado.

O controller usa o sensor para:

- captura na célula atual;
- chegada da `PontaLanca`;
- oportunidades no caminho;
- captura rogue;
- handoff e swap.

O planner publica os alvos formais diretamente no `MissionIntent`. Depois, o
`CaptureOpportunityClaimService` roda o matching residual N × M uma vez para os
capturadores sem plano. Formal retira sua refeição da mesa; rogue divide o resto.

### O que falta

O gate continua distribuído entre `Blitzkrieg`, `Swap`, reserva oportunista,
claim e captura imediata. Não existe uma resposta única para:

> **Eu sou necessário para terminar ou avançar esta captura, ou outro capturador
> consegue fazê-la sem me prender aqui?**

O handoff atual:

- é condicionado a `hardMode`;
- exige prédio já parcialmente capturado (`0 < pontos < máximo`);
- depende de sucessor e de objetivo aberto à frente;
- portanto normalmente age **depois** que a ponta já gastou uma ação abrindo o
  prédio.

Ele não cobre inteiramente o caso “já estou sobre o prédio intacto, outro pode
capturar agora, então não gasto minha ação”.

O `Swap` também compara `CurrentHP` cru. Hoje isso costuma coincidir com poder,
mas falha para chaves com eficiência diferente:

```text
bazooka HP10 × 0,5 = capture power 5
soldado HP6 × 1,0  = capture power 6
```

Comparar HP deixa a unidade mais lenta sobre o prédio. O gate deve comparar
`PodeCapturarSensor.GetCapturePower` para a construção concreta.

---

## 6. Enxergar — o fato está no sensor, não no organizador

A canção determina:

> *Mas se a cidade se esconde no breu, não entro às cegas como se já fosse meu.*
>
> *Quem chega sem ver perde o turno de capturar.*

O `PodeCapturarSensor` já considera suficiente:

```text
visível OU explorado → FOW permite tentar capturar
nenhum dos dois       → “Terreno ainda desconhecido”
```

O `Explorer`, porém, pergunta principalmente se a célula está **visível agora**.
Uma construção explorada, mas atualmente coberta, pode provocar busca de
observador mesmo quando a pergunta doutrinária deveria responder “sei o
suficiente”.

Além disso, `Explorer` roda depois de vários ramos de combate e depois do bloco
de embarque da entrada principal. Ele mistura:

- revelar ocupante oculto;
- ocupar construção de observador;
- escolher montanha/DPQ;
- ataque lateral oportunista;
- aproximação ao objetivo.

Há matéria-prima para a pergunta, mas não existe `Enxergar` como casa declarada.

---

## 7. Detectar — existe o sintoma, não a separação causal

A canção separa os dois casos:

> *Se a estrada está livre, mas não posso avançar, há presença escondida que
> alguém deve encontrar.*

O código atual encontra um `UnitManager` no hex do alvo e, se ele não estiver
visível, procura observador avançado ou uma célula de LOS/DPQ. Isso produz um
comportamento semelhante ao desejado, mas nasce de leitura direta da ocupação do
tabuleiro.

A pergunta ideal não é “há uma unidade invisível ali?”, pois isso já conhece o
segredo. É:

```text
o caminho deveria ser válido, mas a aproximação está bloqueada
e não existe contato conhecido que explique o bloqueio?
```

Hoje continuam misturados:

- terreno não conhecido;
- ocupante invisível;
- rota bloqueada por ocupação;
- componente de movimento desconectado;
- falta de uma posição de observação.

Também não existe ainda a missão entre papéis “peça para outra unidade revelar
ou detectar”. O Capturador só tenta resolver o problema com o próprio corpo.

---

## 8. Embarcar — a pergunta está quase pronta

`EvaluateCapturerRideNeed` já formula:

> **Eu chego sozinho no meu alvo?**

O alvo vem de uma única origem:

```text
com plano → MissionIntent/DesignatedCaptureTarget formal
sem plano com par → CaptureOpportunityClaim
sem plano sem par → MagneticTarget
```

Depois o `QueroCaronaService` mede o envelope próprio:

```text
Tactical/Operational por rota própria → recusa carona
Beyond ou componente desconectado     → aceita carona
```

Isso vale com HQ e sem HQ. O passageiro não pergunta primeiro se existe
helicóptero; pergunta se **precisa** dele.

As divergências restantes são de organização:

- o bloco roda antes das casas declaradas `Enxergar` e `Detectar`;
- depois de `QueroCarona=SIM`, rogue ainda pode deixar combate preemptar o
  transporte;
- `Embarcar` também executa aproximação, scan físico e reposicionamento, em vez
  de apenas justificar o transporte.

---

## 9. Reposicionar — a folha do canal está implementada

Quando `QueroCarona` responde SIM e não há embarque materializável no turno, o
Capturador chama `MelhorEmbarqueService.EvaluateForPassenger` com:

- ele próprio como passageiro;
- transportador vazio, permitindo comparar todos os aliados compatíveis;
- horizonte Strategic;
- resultado já calculado de `QueroCarona`;
- promessa persistida como preferência, nunca lock.

A ação usa sempre:

```text
passengerMeetingCell → lado terrestre, destino do passageiro
lzCell                → lado naval/aéreo, apenas diagnóstico do encontro
```

Se a unidade já está no encontro, espera. Se não consegue chegar na rodada,
progride para ele. Se existe descontinuidade estrutural e nenhum encontro
materializável, espera sem retomar a marcha cúbica contra o canal.

Essa é exatamente a folha desejada:

> **Mover para a LZ terrestre e esperar o helicóptero.**

O trabalho futuro é tornar `Reposicionar` a casa que agrega as três causas:

```text
informação → observação
geografia  → LZ
rota válida → magnetismo normal
```

Hoje cada uma vive numa partial diferente.

---

## 10. Mirar — defender renda funciona; combate ainda sequestra a árvore

A defesa posterior à captura está próxima da doutrina:

- sem capturável restante, o objetivo entra no `Defender`;
- setor recém-capturado pode ser guarnecido;
- ameaça visível ao prédio pode ser atacada;
- sem pressão relevante, a unidade é liberada;
- prédio é protegido pela renda, não por posse.

Isso corresponde ao verso:

> *Não guardo uma bandeira, eu guardo a produção.*

Mas `Mirar` não está confinado a esse estado. Existem ataques em:

- `Rogue`, antes de procurar captura alcançável;
- `Pursuer`, durante a aproximação;
- `Agressive`, antes do avanço normal;
- ataque defensivo de oportunidade;
- scoring final de movimento + ataque;
- exploração, por ataque lateral.

Alguns tiros removem um bloqueador real e servem à captura. Outros apenas
encontram um inimigo visível suficientemente perto. O código ainda não pergunta:

> **Este combate destrava ou protege renda, ou somente sequestra a profissão?**

O Capturador Agressivo pode responder de maneira diferente ao mesmo fato, mas
continua lendo a mesma árvore. A variação deve mudar o gate, não criar outro
controller.

---

## 11. Fundir — falta a política econômica

A canção já é uma especificação completa:

```text
dois prédios, duas tropas
um prédio, união
sem prédio, força inteira para a próxima missão
```

O código atual só funde dentro de `AIController.Repair.cs`, quando:

- a unidade está em manutenção;
- `fuseWhileInRepair` está ligado;
- receptor e candidato são do mesmo tipo;
- o resultado cabe em 10 HP;
- a célula é segura e, durante invasão, fica na retaguarda;
- a fusão melhora manutenção ou libera espaço.

Ele não consulta:

- quantidade de oportunidades livres;
- quantidade de capturadores disponíveis;
- capturadores que ficaram sem par;
- capture power atual e máximo;
- perda de trabalho paralelo;
- preferência por absorver quem já agiu.

O matcher da `v8.2.0` criou o fato necessário: há uma lista explícita de pares e
outra de capturadores sem par. Portanto “excesso de presença” não precisa mais
ser inferido pela ordem acidental das unidades.

### Cenário de aceitação

```text
1 prédio livre
3 capturadores: HP6, HP6, HP4

resultado desejado:
- um HP6 recebe e executa a captura;
- existe presença excedente;
- HP4 pode fundir com o outro HP6;
- 6 + 4 vira capture power 10;
- nenhuma captura paralela é perdida.
```

Com três prédios livres para os mesmos três capturadores, não fundir: os três
relógios trabalhando em paralelo rendem antes que um relógio maior trabalhando
sozinho.

O mecanismo continua sendo `PodeFundir`; a razão econômica pertence à política
de captura. Não duplicar legalidade de fusão dentro do Capturer.

---

## 12. Mapa das partials

| arquivo | responsabilidade atual | casa predominante desejada |
|---|---|---|
| `AIController.Capturer.cs` | entrada, ordem fixa, objetivo formal e scoring misto | organizador da árvore |
| `.PontaLanca.cs` | chegada e captura direta | `Capturar` |
| `.Blitzkrieg.cs` | handoff hard após captura parcial | política de `Capturar` |
| `.Swap.cs` | ceder prédio para unidade de HP maior | política de `Capturar` |
| `.Opportunist.cs` | captura transversal no caminho | política de `Capturar` |
| `.Helpers.cs` | sensor, reservas, missão formal, busca de alvo | fatos compartilhados; reduzir decisões |
| `.Explorer.cs` | ocupante oculto, observador, LOS/DPQ | separar `Enxergar` e `Detectar` |
| `.Embark.cs` | necessidade, scan, LZ, espera e farol | separar `Embarcar` de `Reposicionar` |
| `.Embark.Extended.cs` | embarque após movimento | executor físico de `Embarcar` |
| `.Embark.Pathing.cs` | caminhos locais de aproximação | executor físico de `Embarcar` |
| `.Embark.Scan.cs` | preferência e diagnóstico de transportes | executor/diagnóstico de `Embarcar` |
| `.Embark.Transporter.cs` | vagas, compatibilidade e embarque | executor físico compartilhado |
| `.Pursuer.cs` | combate durante progressão | verificar se destrava `Capturar` ou pertence a `Mirar` |
| `.Agressive.cs` | ramo combatente | variação de gate, não árvore paralela |
| `.Attack.cs` | ataques, LOS, defesa antes do embarque e scoring | legalidade/execução de `Mirar`; hoje espalhado |
| `.Defender.cs` | guarnição e proteção de setor conquistado | `Mirar` pós-captura |
| `.Rogue.cs` | alvo sem plano, combate e marcha magnética | consumir a mesma árvore com outra origem de alvo |
| `.Vacate.cs` | liberar produção e posições bloqueadas | precondição global/coordenação |
| `CaptureDecisionReport.cs` | modelo de candidatos e scores sem consumidores | candidato a trace das perguntas |

`CaptureDecisionReport` não possui chamador no código atual. Seu modelo ainda é
o antigo — candidatos que disputam por score —, mas o arquivo oferece um lugar
natural para um diagnóstico da nova árvore, desde que deixe de representar um
torneio de ações.

---

## 13. Forma sugerida para o diagnóstico

Antes de alterar comportamento, a decisão deveria conseguir explicar:

```text
[Capturador][Pergunta] #6 Capturar
  resultado=CONTINUA motivo=alvo fora do Tactical

[Capturador][Pergunta] #6 Enxergar
  resultado=CONTINUA motivo=construcao explorada

[Capturador][Pergunta] #6 Detectar
  resultado=CONTINUA motivo=bloqueio geografico conhecido

[Capturador][Pergunta] #6 Embarcar
  resultado=CONTINUA precisaTransporte=sim envelope=BeyondOperational

[Capturador][Pergunta] #6 Reposicionar
  resultado=ACAO tipo=Move encontroPax=(10,5) LZTransport=(9,5)
```

Resultados conceituais mínimos:

```text
NAO_SE_APLICA  esta pergunta não pertence a este contexto
CONTINUA       eliminou uma explicação, mas não materializou ação
ACAO           produziu uma folha executável
AGUARDA        não há ação melhor sem contradizer um fato já provado
```

Esses nomes ainda são desenho, não contrato de API. O ponto é impedir que um
`false` volte a significar ao mesmo tempo “não se aplica”, “falhou”, “não sei” e
“continue para a próxima política”.

---

## 14. Ordem de implementação do refactor

Cada passo deve compilar e ser conferido em jogo antes do seguinte. A migração
começa observando a árvore, depois lhe entrega autoridade uma folha por vez.

### Passo 1 — transformar `CaptureDecisionReport` no trace da árvore

Sem mudar comportamento:

1. registrar a decisão atual exatamente como aconteceu;
2. registrar a árvore nova em **modo sombra**, sem usá-la para escolher.

O relatório deve manter lado a lado:

```text
DECISAO ATUAL
Capturer.Pursuer -> Attack #14

ARVORE EM SOMBRA
Capturar     -> CONTINUA: alvo fora do Tactical
Enxergar     -> CONTINUA: construcao explorada
Detectar     -> CONTINUA: sem incognita de contato
Embarcar     -> CONTINUA: rota propria Operational
Reposicionar -> RECOMENDARIA: avanco ao predio
```

Primeiro o trace registra fatos da cascata existente: partial vencedora, ação,
alvo, atalhos consultados e motivo do encerramento. Conforme o contexto comum
nascer, as perguntas em sombra passam a consumi-lo. O trace não pode varrer o
tabuleiro ou resolver o alvo por um caminho paralelo, pois começaria a explicar
uma decisão diferente da que a IA realmente tomou.

Cobrir desde o primeiro passo:

- capturador formal;
- rogue de IA com HQ;
- facção sem HQ;
- Capturador Agressivo.

### Passo 2 — construir as perguntas verdes sem execução própria

Criar `Capturar`, `Enxergar`, `Detectar` e `Embarcar` como classificadores do
problema. Inicialmente nenhuma delas move, captura, ataca ou embarca.

Elas acumulam um contexto factual único por unidade:

- alvo e origem (`plano`, `claim`, `magnético`);
- operação (`captura` ou `reconquista`);
- banda e rota própria;
- visível/explorado;
- ocupação conhecida e bloqueio sem contato;
- capture power e tempo para concluir;
- pares, sem-par e oportunidades livres;
- necessidade de transporte, promessa e encontro.

Responsabilidade de cada pergunta:

```text
Capturar   necessario, redundante/blitzkrieg ou nao materializavel
Enxergar   conhecimento suficiente ou esclarecimento necessario
Detectar   contato incerto ou bloqueio geografico conhecido
Embarcar   perna propria resolve ou transporte esta justificado
```

O contexto não decide nem recalcula. Ele impede que cada partial resolva
novamente o alvo ou varra novamente o tabuleiro. Durante este passo, o executor
antigo continua sendo autoritativo e o trace compara sua ação com a recomendação
da árvore em sombra.

### Passo 3 — fazer `Reposicionar` virar a primeira grande folha executora

É a primeira transferência de autoridade porque os mecanismos já existem e a
folha da LZ já foi implementada:

```text
rota normal          -> aproximacao magnetica
falta de informacao  -> spot de observacao/aproximacao
descontinuidade      -> passengerMeetingCell
handshake existente  -> LZ combinada; mover e esperar
```

Preservar sem alteração semântica:

- `passengerMeetingCell` como destino terrestre do passageiro;
- `lzCell` apenas como lado naval/aéreo do encontro;
- promessa e claim como faróis, nunca locks;
- espera quando não existe progressão materializável;
- proibição de retomar a marcha cúbica contra o canal.

`Embarcar` justifica o transporte e o executor físico ainda materializa um
embarque imediato quando possível. `Reposicionar` consome o diagnóstico e o
resultado de `MelhorEmbarque`; não os recalcula.

### Passo 4 — podar `Mirar` até obedecer à doutrina

Só depois de `Reposicionar` estar estável, classificar cada ataque existente:

```text
defende renda conquistada      -> pertence
remove bloqueador do objetivo  -> pertence
agressivo escolhe lutar         -> variacao declarada do gate
inimigo apenas disponivel       -> nao sequestra o Capturador
```

Preservar a defesa de construção já conquistada. Separar bloqueador que impede
renda de inimigo meramente visível. O Capturador Agressivo altera a resposta da
mesma árvore; não ganha um controller paralelo.

Esta poda vem depois porque o combate atual mascara falhas anteriores: uma ação
de ataque pode parecer útil mesmo tendo preemptado captura, observação,
transporte ou reposicionamento.

### Passo 5 — implementar `Fundir` econômico por último

Ele depende das respostas anteriores para saber:

- qual é o objetivo;
- se existe trabalho imediato;
- quantos prédios podem ser trabalhados em paralelo;
- quem recebeu par e quem ficou excedente;
- capture power concreto e máximo;
- quem já agiu;
- se existe encontro legal para a fusão.

Consumir pares e sem-par do matching. Usar `PodeFundir` como fonte de legalidade
e reutilizar a solução de encontro existente, sem inventar outro rendezvous.

Antes de lhe entregar autoridade, validar:

- se trocar o ocupante preserva `CurrentCapturePoints`;
- semântica de `HasActed` para receptor e absorvido;
- se perder um token elimina alguma captura simultânea;
- preferência por absorver quem já agiu, quando legal.

### Passo 6 — remover resíduos e consolidar a documentação

Somente depois de a nova árvore reproduzir ou substituir conscientemente os
comportamentos antigos:

- retirar a chamada duplicada de Repair;
- dissolver scoring e atalhos que ficaram sem função;
- remover campos antigos de `CaptureDecisionReport` que não servem ao trace;
- atualizar o `Capturer.md` com a ordem confirmada;
- revalidar pendências antigas antes de mantê-las como abertas.

---

## 15. Cenários de aceitação

### Captura direta

Construção conhecida e capturável no Tactical, sem sucessor melhor:

```text
Capturar → ação de captura
```

### Blitzkrieg antes da captura instantânea

Unidade sobre a construção, sucessor capaz de concluí-la sem atrasar a renda e
objetivo economicamente melhor à frente:

```text
Capturar → redundante
Reposicionar → continuar o eixo
```

### Construção explorada

Construção não visível agora, mas já explorada e sem incógnita de contato:

```text
Enxergar → conhecimento suficiente
não procurar observador apenas para renovar visibilidade
```

### Bloqueio oculto

Rota deveria existir, mas a aproximação é bloqueada sem contato conhecido:

```text
Detectar → obter observação
não pedir transporte por um problema de contato
```

### Canal

Alvo além de componente terrestre e nenhum embarque imediato:

```text
Embarcar → transporte necessário
Reposicionar → passengerMeetingCell
chegou → esperar helicóptero
```

Nunca marchar por distância cúbica contra a água.

### Defesa da renda

Captura terminou e há ameaça à construção:

```text
Mirar → defender a renda
```

Sem ameaça e com trabalho de captura restante, não transformar o capturador em
combatente genérico.

### Fusão econômica

`HP6 / HP6 / HP4` e um único prédio livre:

```text
um HP6 captura
HP4 + HP6 → HP10, se PodeFundir autorizar
```

Com três prédios livres, os três permanecem independentes.

### Paridade de planejamento

Executar os mesmos cenários em:

```text
IA com HQ: formais recebem planos; rogues dividem o resto
IA sem HQ: todos são rogues
```

A origem do alvo muda. As perguntas e seus significados não.

---

## 16. Documentação envelhecida encontrada

O `Capturer.md` ao lado do código ainda contém trechos que precisam ser
revalidados quando o refactor começar:

- diz que rogue só age quando existe QG inimigo conhecido;
- em outra seção ainda descreve rogue marchando para o QG;
- lista a ordem antiga `Detectar → Enxergar`;
- inclui `Desembarcar`, `Suprir` e `Transferir` na ordem do papel;
- declara corretamente, mais abaixo, que a nova prioridade ainda não está
  implementada.

A lista de pendências da doutrina também contém itens anteriores à `v8.2.0`.
Exemplo: a antiga afirmação de que `IsOtherAssignedCapturerTarget` desaparecia
para sem-plano precisa ser reavaliada contra o tratamento atual de claims e
capturadores sem par.

Não corrigir esses documentos por inferência durante a análise. Atualizá-los
quando cada comportamento for confirmado no código e no jogo.

---

## 17. Conclusão

As correções da `v8.2.0` não foram desvios de transporte. Elas implementaram a
infraestrutura da parte inferior desta árvore:

```text
alvo formal/claim/magnético
→ envelope próprio
→ necessidade de carona
→ MelhorEmbarque
→ passengerMeetingCell
→ mover para LZ
→ esperar transportador
```

O próximo refactor não precisa inventar o Capturador. A doutrina e a canção já
definem seu comportamento; os serviços já publicam boa parte dos fatos. O
trabalho é transformar uma coleção de atalhos que produzem ações numa sequência
de perguntas que elimina causas.

> **Antes do tiro, vem o dinheiro.**
