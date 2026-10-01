# v8.2.1 — O táxi não estaciona na renda que ele mesmo viabilizou

Fechada em 2026-08-09. Antecessora: [`v8.2.0`](relatorio_v8.2.0.md).

> **Nota de autoria.** O trabalho desta versão não foi feito na sessão que a
> fechou — foi conduzido em paralelo, com outro assistente, enquanto o crédito
> desta acabava. O relatório foi escrito **lendo os diffs**, não acompanhando a
> execução. Onde uma intenção não estava escrita no código, ela está marcada como
> inferida em vez de afirmada.

---

## O fio do dia

Duas correções, e as duas são a mesma frase vista de dois lados:

> **Quem viabiliza uma coisa não pode ser o que a impede.**

```text
o táxi        entrega o capturador no porto — e fica em cima do prédio,
              bloqueando exatamente a captura que ele acabou de tornar possível

a iniciativa  cortava por "o navio sozinho alcança o passageiro" (MP+1),
              quando quem decide é o ENCONTRO — os dois lados andam
```

O primeiro é um bloqueio físico; o segundo, um bloqueio de ordem. E o segundo
responde uma pergunta que ficou aberta em aberto na `v8.1.2`.

---

## Frente A — O táxi vazio não vira estacionamento

Cinco arquivos, um comportamento.

`AIController.TransportOperations.cs` ganha
`TryBuildEmptyTransportCaptureTargetVacateAction`, e a docstring diz o porquê
sem rodeio:

> *"Transportador vazio não transforma a cabeça de praia em estacionamento. Se
> ocupa a construção de uma missão Capture ainda não agida, libera o hex antes de
> avaliar Pickup — inclusive na IA sem HQ, onde não existe `TeamObjectivePlan`."*

**A ordem no roteador é argumentada, não acidental** (`AIController.Router.cs`):

> *"A entrega terminou; o táxi não pode continuar estacionado sobre a renda que
> acabou de viabilizar. Vem **antes** de Pickup e Repair pela mesma razão do
> blocker de iniciativa: primeiro libera a verdade econômica do passageiro,
> depois escolhe o próximo serviço."*

Duas guardas que evitam que a correção vire outro problema:

```text
se a própria ficha captura   o controlador do Capturador decide entre tomar e
                             ceder. A regra é para o táxi ALHEIO à captura

ao escolher para onde sair   não resolve um bloqueio criando o mesmo bloqueio
                             para outro capturador do lote
```

### O achado que estava escondido no claim service

`CaptureOpportunityClaimService.cs` ganha
`IsExistingCaptureTargetBlockedOnlyByEmptyAlliedTransport`, e o comentário é o
mais valioso da versão:

> *"Um táxi vazio sobre o endereço que o próprio passageiro já carregava não
> invalida a refeição. Ele é um bloqueador temporário que a iniciativa manda
> sair antes da captura. **Sem esta exceção, o solve seguinte apaga o farol
> exatamente porque o transporte terminou a entrega no porto.**"*

Ou seja: o sucesso da entrega **destruía a reserva**. O claim via a célula
ocupada, concluía "indisponível" e baixava o alvo — no instante exato em que a
operação tinha dado certo. É a mesma família do bug da `v8.1.2` em que a
condição de baixa de um significado era a condição de início do outro.

### Iniciativa: Mission Intent vira fonte primária

`AIController.Initiative.cs` passa a resolver o bloqueio pela **ficha**, com o
plano como compatibilidade:

> *"Mission Intent também existe na IA sem HQ. Ele é a fonte primária; o plano
> abaixo permanece como compatibilidade para uma ordem formal ainda não
> materializada na ficha."*

E `TryResolveUnactedCaptureMissionAtCell` declara o que **não** é filtro:

> *"Ocupação não é filtro: a pergunta é justamente se o bloqueador deve sair. A
> validade da captura continua vindo do `PodeCapturarSensor` por meio de
> `IsRebelCapturable`."*

Isso fecha o círculo com a `v8.1.2`: a missão publicada na ficha, que naquela
versão ganhou o instante certo de leitura, agora tem **leitor** — e o primeiro
leitor é a iniciativa.

---

## Frente B — O encontro é dos dois lados, e a fila passou a saber

`AIController.Phase2.cs` troca o critério de cessão de vez:

> *"A fila consome o mesmo panorama do `MelhorEmbarque` usado depois pela decisão
> do transportador. Assim, **'os dois chegam ao encontro agora' substitui o corte
> antigo 'o navio sozinho alcança o passageiro'**."*

O fato é congelado no setup, não recalculado
(`BuildTacticalPickupInitiativeFacts` / `HasTacticalPickupInitiativeFact`), e a
justificativa arquitetural está no código:

> *"Fato puro, congelado junto com a fila da Fase 2 (…) Nasce do mesmo
> `TransportPlanningSnapshot` consumido pela decisão do transportador;
> **iniciativa e cessão de vez não mantêm um segundo modelo de distância ao lado
> do `MelhorEmbarque`**."*

Essa última cláusula é a que impede o `project_dual_transport_demand` de se
repetir: um só modelo de distância, dois consumidores.

Na `Initiative.cs`, transportador rogue vazio com encontro conjunto provado sobe
para o grupo 2 — antes dos capturadores — com o motivo escrito:

> *"O fato considera o movimento dos DOIS lados; distância atual ≤ MP+1 não
> considera."*

### ⚠️ Isto responde a pergunta que a `v8.1.2` deixou em aberto

O resumo da versão anterior perguntava, como pré-requisito do próximo item:

> *"A banda do `Embarcar` é do transportador que sobe (**eu alcanço o navio**) ou
> do **encontro** (**nós dois nos encontramos em N turnos**)?"*

**A resposta implementada é: do encontro.** Ela entrou pela iniciativa, não pelo
degrau `Embarcar` — mas o precedente está posto, e o degrau que ainda falta agora
tem um lugar de onde copiar a forma.

---

## Frente C — O BeachManager vira catálogo estratégico do naval

`AIController.Transportador.Naval.cs` ganha
`TryResolveKnownMilitaryBeachApproach`. O ponto arquitetural:

> *"BeachRepCell escolhe a **identidade**; **nunca vira LZ fixa**. A LZ vem da
> borda naval da faixa inteira e permanece sujeita ao
> `MelhorDesembarque`/`PodeDesembarcar` quando a execução estiver próxima."*

E a decisão de FoW é deliberada e está justificada:

> *"Não consulta FoW de propósito. Assim como o endereço das construções, a
> geografia das praias militares pertence ao mapa conhecido. **Isto não revela
> ocupantes, ameaças ou contatos escondidos.**"*

Isso é coerente com as duas verdades do projeto: revelar geografia não é revelar
contato. `PodeEnxergar` e `PodeDetectar` continuam separados.

Sem catálogo, o fallback legado nasce **no objetivo** e expande em bolhas até
achar a primeira praia/LZ visível ou explorada — comportamento preservado.

**O que isso destrava:** o navio passa a poder perseguir uma praia sob névoa
preta, porque a identidade da praia é geografia, não contato. Era um dos quatro
itens da fila da `v8.1.2` (*"LZ em névoa — conferir antes de culpar a IA"*), e a
conferência resultou numa separação, não num remendo.

---

## Frente D — O dono da LZ é o passageiro 1/FIFO

`AIController.MelhorDesembarque.cs` e `MelhorDesembarqueService.cs`:

> *"Passageiro 1/FIFO é o dono da LZ. Sem endereço planejado nem oportunidade
> para ele, não se escolhe uma LZ independente só para uma carga secundária."*

E a redução do caso:

> *"O desembarque só distingue duas coisas: o passageiro trouxe um **endereço**
> (Mission Intent) ou precisa que a própria LZ encontre uma **oportunidade**.
> Quem publicou a missão não muda nem a coordenada nem a prioridade FIFO da
> carga."*

Duas casas, não quatro. É a mesma economia de estados dos quatro do transportador
(`Transporte.md` §7.1): o comportamento sai de fatos já publicados, sem valor
novo em enum.

---

## Frente E — Dois planos novos, sem código

Ambos em `docs/Planos/`, datados de 2026-08-09.

### `PLANO_INICIATIVA_POR_DEPENDENCIAS.md`

Ataca um hardcode nomeado com honestidade:

> *"'Helicóptero age cedo' é uma aproximação por **tipo de unidade**. Ela mistura
> profissões diferentes: Chinook preparando embarque; Apache preparando ou
> executando combate; helicóptero sem oportunidade relevante (…) O nome `Chinook`
> não é comparado no runtime. **O hardcode atual é doutrinário: todo helicóptero
> recebe precedência, mesmo quando não prepara nenhuma ação de outra unidade.**"*

Cinco fases, explicitamente incrementais — *"não implementar como um refactor
único"* — e a Fase 1 é **tornar a fila explicável sem mudar comportamento**. Essa
ordem é a mesma lição que a `v8.1.0` já tinha pago: medir antes de mexer.

### `CAPTURER_ANALISE.md` — 836 linhas

Compara três coisas que o projeto insiste em não confundir: a **doutrina**
(`AI Behavior/Capturador.md`), o **comportamento** (`Capturer.md`, ao lado do
código) e a **árvore desejada**. E se declara temporário:

> *"A análise é uma ponte temporária. Quando uma pergunta alcançar a doutrina, o
> comportamento confirmado deve ser registrado no `Capturer.md` ao lado do
> código. **Este arquivo não substitui nenhum dos dois documentos
> autoritativos.**"*

A frase-mãe derivada do lema:

> **Qual é a forma mais barata de transformar esta construção em renda sem
> desperdiçar um capturador?**

O inventário por casa (§5 a §11) marca o que existe e o que falta — e o
diagnóstico mais duro é o da §10: *"defender renda funciona; **combate ainda
sequestra a árvore**"*.

---

## O que NÃO terminou

**Nada foi validado em jogo nesta versão.** Cinco arquivos de lógica mudaram e a
árvore compila; nenhuma corrida de aceitação consta. Vale o mesmo aviso da
`v8.1.0`: *o que se sabe é que compila, não que se comporta*.

Os cenários de aceitação existem escritos, no `PLANO_INICIATIVA_POR_DEPENDENCIAS.md`
§"Cenários de aceitação" e no `CAPTURER_ANALISE.md` §11. Não foram corridos.

### A fila da v8.1.2, revisitada

```text
1. banda do Embarcar     ⚠️ RESPONDIDA em princípio (é do encontro), mas o degrau
                            Embarcar continua sem banda — a forma entrou pela
                            iniciativa, não por ele
2. a pergunta do vazio   ❌ transportador vazio continua sem publicar wantsRide;
                            a célula ASAP segue inalcançável
3. âncora de praia       ✅ o BeachManager existe e o naval o consome
4. LZ em névoa           ✅ separado, não remendado: geografia de praia não é contato
```

**O deadlock do soldado com 2 de autonomia continua aberto.** Ele depende do item
2, e o item 2 não se mexeu.

### Outros pendentes

- **Os dois planos não têm código.** O de iniciativa declara cinco fases e nenhuma
  foi executada; a Fase 1 (tornar a fila explicável sem mudar comportamento) é a
  entrada barata.
- **A promoção genérica de helicóptero continua lá** — é a Fase 3 do plano.
- **`CLAUDE.md` continua desatualizado** sobre o ataque oportunista do courier
  (herdado da `v8.1.2`, não tocado).
- **A missão herdada continua write-only no transporte** —
  `TryResolveCargoDestinationAnchor` ainda escava o passageiro primário. Mas a
  iniciativa **já lê** a ficha (frente A), então o padrão de leitura existe agora
  em pelo menos um consumidor.

---

## O que eu não pude verificar

Escrito por leitura de diff, não por acompanhamento. Concretamente:

- **As intenções acima são as que estão escritas nos comentários do código.**
  Onde o comentário explica o porquê, citei; onde não explica, descrevi só o
  efeito. Não inferi motivação além disso.
- **Não conferi a cena.** `Hot Seat 0 - Treino.unity` mudou ~41.775 linhas; o
  autor classificou como churn de teste (unidades criadas e apagadas), e eu
  aceitei a classificação sem abrir o diff — o volume é grande demais para o
  orçamento desta sessão.
- **Não rodei nada.** A única verificação de execução foi `dotnet build`: 0 erros.
- **Não sei se os cenários de aceitação dos planos foram pensados antes ou depois
  do código.** A ordem importa para saber se são especificação ou racionalização,
  e o diff não responde.

### E um erro meu nesta sessão, para não se repetir

Afirmei que *"não há nenhum `BeachManager.cs` nesta árvore"*. Há —
`Assets/Scripts/Terrain/BeachManager.cs`, commitado desde a `v8.2.0`. Eu havia
olhado a lista de arquivos **modificados** e concluído sobre os **rastreados**.

> **`git status` responde "o que mudou", nunca "o que existe".** Para ausência,
> a pergunta é `git ls-files`.

É a terceira vez em duas versões que eu concluo ausência a partir da ferramenta
errada — as outras duas estão na tabela de armadilhas do resumo.
