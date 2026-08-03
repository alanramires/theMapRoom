# Tabela Magnética — quem cada papel acompanha

Fonte da lista de atração que os papéis passam ao `MelhorCapitaoService`.

Rascunho do autor + notas de revisão. **Ainda não está no código:** os quatro
resolvedores antigos (`TryResolveCapturerMagnet`, `TryResolveFireSupportMagnet`,
`TryResolveNearestEwacsMagnet`, `TryResolveStockRearCaptain`) continuam mandando.

---

## Notação

`A → B → C` — **ordem de preferência com fallback.** Tenta A; não havendo, B;
não havendo, C.

A seta substituiu a barra do rascunho original. `/` lê como "ou" e já produziu
uma leitura errada; `→` lê como "senão" e não tem segunda interpretação.

**A primeira faixa que produzir candidato vence, mesmo que alguém de faixa
inferior esteja mais perto.** É contraintuitivo e é de propósito: um
interceptador vai atrás de uma Vigilância Aérea a 9 hexes em vez de um
Capturador a 2, porque a vigilância é o que o mantém útil.

**Distância é de ROTA, não cúbica.** Um capturador a quatro hexes em linha reta
atrás de uma serra está mais longe que um a cinco hexes de estrada. Aeronave
recebe distância de hex automaticamente — a geometria é da unidade, não uma
opção do chamador.

---

## A tabela

Uma lista por papel. Para a versão **com plano**, aplique as duas regras da
seção seguinte.

| papel | lista de atração |
|---|---|
| Capturador | capturável / reconquistável |
| Capturador agressivo | capturável / reconquistável |
| Assault | Capitão |
| Artilheiro combatente | Capitão |
| FireSupport | Capitão |
| AntiAéreo combatente | aeronaves detectadas → Capitão |
| AntiAéreo | Vigilância Aérea → Capitão |
| Interceptador | Vigilância Aérea → Ataque Aéreo → Capitão |
| Ataque Aéreo | unidades de superfície detectadas → Capitão |
| Transportador | passageiros que pedem carona |
| Logística | feridos → manutenção → Capitão |
| Estoque | construção aliada falida → Logística → Capitão |
| Vigilância | ponto de observação → Capitão *(ver seção própria)* |

**Capitão** significa **Capturador elegível como líder** — a doutrina do projeto:
*a unidade que outra unidade orbita*. Não aparece na linha do Capturador porque
ele **é** o capitão dos outros; o magnetismo dele é o objetivo, não uma liderança.

---

## Com plano = mesma lista + duas regras

A coluna "Com plano" do rascunho não era outra lista. É a mesma, filtrada pelo
setor, com a RepCell anexada no fim:

```
lista_com_plano(papel, setor) = filtrar_por_setor(lista(papel), setor) + [RepCell(setor)]
```

É como o `MelhorCapturaService` já trata setor: **filtro sobre o conjunto de
candidatas**, nunca uma pergunta separada — o serviço recebe "estas quatro
construções", nunca "o setor C".

Duas linhas do rascunho se consertam sozinhas com isso:

- **Capturador com plano** deixa de ser só `RepCell` e vira `capturável do setor
  → RepCell`. O rascunho contradizia o que já está no código: um setor tem
  vários prédios, e representante ocupado **não** quer dizer setor ocupado.
- **Estoque com plano** deixa de ser só `RepCell` e passa a atender construção
  aliada falida do próprio setor antes de cair no consolo abstrato.

---

## Vigilância — o campo que vira ponto

O rascunho dizia *"libera mais visão especializada"*, que não nomeia coisa
nenhuma para orbitar. Todas as outras linhas dizem **quem**; essa dizia um
**critério sobre área**.

A diferença é estrutural:

> **Construção é ponto. Névoa é campo.**
>
> Ponto é enumerável, fixo e nomeável — por isso serve de âncora. Campo não.

### A saída para a vigilância terrestre

Ela não precisa perseguir névoa: pode ser atraída pela **construção que quer
observar**. Construções são pontos fixos do mapa, então voltam a caber na tabela.

O risco óbvio é ela grudar na construção para sempre. A saída é o predicado do
próprio chamador:

```csharp
matchConstruction = c => !ArredoresJaRevelados(c)
```

Terminada a observação, o predicado para de casar, a construção sai da lista e a
próxima ganha por distância. **Não é caso especial do serviço** — é o predicado
do chamador fazendo o trabalho dele. O `MelhorCapitao` nem fica sabendo.

### O caso que não tem prédio

**Vigilância naval caçando submarino não tem construção embaixo d'água.** A
referência é o próprio oceano não explorado.

Esse limite não é da vigilância — é do `MelhorCapitao`, que responde sobre
**pontos**. Mas o encaixe é limpo, porque o campo **reduz a ponto**:

```
MelhorVisão (campo → ponto)              MelhorCapitao (ponto → referência)
  "qual célula revela mais?"    ───────►   entra como atração de célula fixa
```

O `hasFixedCell` já existe — nasceu para a RepCell e serve igual para "célula de
fronteira da névoa". Então:

| | ponto de observação |
|---|---|
| vigilância terrestre | construção a observar |
| vigilância naval | célula de fronteira do oceano |

Mesma lista, mesma mecânica. A diferença fica onde deve: em **quem calcula o
ponto**. O `MelhorCapitao` continua sem saber o que é névoa.

---

## Pendências

### ~~1. RepCell antes ou depois do Capturador?~~ — VIROU DADO

A tabela deixou de ser código: virou `Assets/DB/AI/AICaptain.asset`
(`AICaptainData`), gerável por `Tools > AI > Gerar Tabela Magnética (Capitão)`.

Com isso a pendência para de travar qualquer coisa. **O asset nasce com a versão
da prosa** — Capturador primeiro, RepCell no fim, pelo argumento abaixo. Se você
discordar, arrasta a linha no inspector; não é mais decisão de código.

O argumento original, para constar:

O rascunho escreve `RepCell / Capturador`, que com a notação confirmada
significa **RepCell primeiro**. A prosa da governança diz o contrário:

> *Ao chegar: 1. procura um Capturador no setor para eleger como Capitão;
> 2. **se não houver** Capturador, utiliza a própria `RepCell` do setor.*
>
> *A `RepCell` funciona como um Capitão abstrato **até que uma liderança real
> esteja disponível**.*

**A prosa vence.** Com a RepCell na frente, um assalto com plano marcharia para
uma célula abstrata tendo um capturador de carne e osso trabalhando o mesmo
setor — e a frase "até que uma liderança real esteja disponível" diz que ela é o
consolo, não a preferência.

---

## A tabela como asset

`AICaptainData` guarda **composição e ordem**. O que ele NÃO guarda, e por quê:

> "Aeronaves detectadas", "construção aliada falida", "feridos", "capturável" —
> nenhum desses é um papel. São **predicados**, e predicado é função: precisa
> consultar sensor, ficha, estoque, detecção. Fingir que cabem num asset
> produziria um campo de texto que ninguém valida.

Então o asset escolhe **qual** predicado e em **que ordem** (o enum
`AICaptainAttractionKind`); o código guarda **como** cada um responde. Mesma
divisão do resto do projeto.

### A coluna "com plano" não é preenchida

Ela nasce vazia de propósito. O `AICaptainData.TryResolve` deriva:

```
com plano = mesma lista + restringir ao setor + aceitar embarcado + RepCell no fim
```

Preencher só se algum papel precisar de algo diferente disso. É essa derivação
que conserta as duas linhas erradas do rascunho original — Capturador e Estoque
iam direto para a RepCell e deixavam de olhar o próprio setor.

### 2. "Passageiros antigos" — confirmar

No Transportador sem plano. Entende-se como *quem espera carona há mais turnos*.
Existe `QueroCaronaResult.rideWaitTurns` no código, que seria exatamente isso —
mas o autor não confirmou.

### ~~3. Capitão embarcado~~ — RESOLVIDA

> **Capitão embarcado, seguidor com plano: pede carona para seguir ele.**

O seguidor **não troca de capitão** só porque o dele entrou num veículo. Muda o
meio de locomoção, não a referência: marchar vira pedir carona.

Sem plano a regra da governança continua valendo — pega outro capitão próximo,
porque quem não tem plano não tem compromisso com aquele setor.

No serviço isso virou `MelhorCapitaoAttraction.allowEmbarked`, **desligado por
padrão**. Só a lista "com plano" liga. Morto, em reparo e inativo continuam
descarte fixo — esses não vão a lugar nenhum; embarcado vai, só não a pé.

O serviço **não pede a carona nem escolhe o alvo dela**. Devolve o capitão
marcado como embarcado e quem o carrega (`carrier`). Fica aberto para o papel:

| alvo da carona | preço |
|---|---|
| hex atual do transportador | vira corrida atrás de alvo móvel |
| destino da viagem do capitão | mais estável, mas exige saber para onde o transporte vai |

Não é pergunta deste serviço. É a mesma fronteira de sempre: ele diz *quem*, o
papel decide *como chegar*.

---

## O que já está resolvido no serviço

`MelhorCapitaoService` foi escrito para consumir esta tabela sem conhecê-la:

- **Não conhece papel.** A lista é montada pelo chamador e passada. Trocar de
  papel é trocar a lista.
- **Não corta por banda.** Capturar exige chegar; acompanhar não. Um capitão a
  dez hexes continua sendo a direção certa.
- **Não escolhe hexágono.** Devolve *quem*; vanguarda, retaguarda, flanco e casa
  exata são do papel, com a Hotzone. Contrato literal do `Princípio Magnético`.
- **A resposta pode ser célula.** É o que permite a RepCell — e a fronteira da
  névoa — serem referência sem gambiarra.

Ferramenta: `Tools > Hotzone > Melhor Capitão`.

---

## Achados para a migração

### Os quatro resolvedores de capitão

Repetem **as mesmas seis guardas** (null, self, morto, embarcado, em reparo,
inativo) — já unificadas no serviço novo. E discordam entre si em dois pontos:

| | defeito |
|---|---|
| `TryResolveStockRearCaptain` | filtra com `roles[0] != UnitRole.Logistica` — **estrito**, barra especializações. O irmão ao lado usa `UnitRoleCompatibility.CanSatisfy` corretamente |
| `TryResolveCapturerMagnet` | usa distância **cúbica**. O do FireSupport usa rota, e o comentário dele explica por quê: *"EWACS sobre uma ilha ou no mar não pode arrastar o SAM até uma costa sem saída"* — está certo, e a cúbica dos outros é que é frouxa |

### Os três "para onde revelar"

A pergunta que viraria o `MelhorVisão` **já está respondida em três lugares
independentes**, cada um com pesos próprios:

| onde | o que faz |
|---|---|
| `AIController.Capturer.Explorer.cs` | seis constantes de peso próprias (`ExplorerForwardObserver*`) |
| `AIController.Transportador.cs` | `FindTransportExplorationMove` |
| `AIController.VigilanciaAerea.cs` | conta `unexploredMarginal` e pontua com `* 25f` |

Mesmo padrão do `IsRebelCapturable`: uma pergunta genérica escrita à mão, várias
vezes, com respostas que podem discordar.
