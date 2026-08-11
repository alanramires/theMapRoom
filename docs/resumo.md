# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-08-09, **depois** da tag `v8.2.1`.
Leia isto primeiro.

---

## Estado

`v8.2.1` tagueada e publicada. Relatório:
[`relatorio_v8.2.1.md`](relatorio_v8.2.1.md) — fecha o trabalho feito sobre a
`v8.2.0` ([relatório](relatorio_v8.2.0.md)).

### O que a v8.2.1 acrescentou — uma frase, dois lados

> **Quem viabiliza uma coisa não pode ser o que a impede.**

```text
bloqueio FÍSICO   o táxi entregava o capturador no porto e ficava em cima do
                  prédio, bloqueando a captura que ele acabou de viabilizar.
                  Pior: o claim via a célula ocupada e APAGAVA O FAROL —
                  o sucesso da entrega destruía a reserva

bloqueio de ORDEM a fila cortava por "o navio sozinho alcança o passageiro"
                  (MP+1). Agora o critério é o ENCONTRO: os dois lados andam,
                  e o fato vem congelado do mesmo TransportPlanningSnapshot
```

⚠️ **Isto responde, em princípio, a pergunta que a `v8.1.2` deixou aberta:** a
banda do `Embarcar` é **do encontro**, não do transportador. Mas a forma entrou
pela **iniciativa**, não pelo degrau — `Embarcar` continua sem banda, e agora
tem de onde copiar.

E o naval passou a consumir o `BeachManager` como catálogo estratégico:
`BeachRepCell` dá **identidade** e nunca vira LZ fixa; a LZ nasce da borda naval
da faixa. Não consulta FoW de propósito — geografia de praia é mapa conhecido,
e isso não revela ocupante nem contato.

O refactor fechou uma parte grande da família do capturador e montou as peças
necessárias para o cenário de evacuação naval:

```text
PLANO FORMAL (HQ)       publica Capture no MissionIntent
                              │
                              ▼
MELHOR CAPTURA          recebe apenas quem ficou sem plano
                       resolve o resto em pareamento N × N
                              │
                 ┌────────────┴────────────┐
                 ▼                         ▼
              com par                   sem par
        claim/reserva de endereço   magnético do capturável
                 │                         │
                 └────────────┬────────────┘
                              ▼
                 se não há rota terrestre estrutural
                 pede carona e marcha para a LZ, não
                 contra o canal
```

### A regra que organiza o capturador

**Plano é ordem; claim é endereço; sobra não é erro.**

- Com HQ, o plano formal é soberano e já publica a missão `Capture` no
  `MissionIntent`.
- Sem HQ, todas as unidades são rogues e o `MelhorCaptura` distribui o que puder.
- Com HQ, o `MelhorCaptura` recebe somente os capturadores que ficaram sem plano
  e divide o resto da refeição.
- O solve conjunto roda uma vez no plano, não uma vez dentro de cada unidade.
- O matcher usa custo de troca `15`; alargar candidatos sem histerese faria o
  conjunto permutar todas as peças a cada pequena mudança.
- Se há três capturadores e um prédio, um recebe o par. Os outros dois continuam
  atraídos magneticamente pelo mesmo prédio, mesmo que o claim pertença ao
  primeiro.
- Se o prédio está `BeyondOperational` e não existe rota terrestre própria, os
  três podem pedir carona. Só um tem plano formal; o transportador decide quem
  atender.

O claim nunca é um cadeado de posse. Ele é o farol usado para distribuir
preferências e dar estabilidade; não impede ajuda nem transforma prédio em
propriedade privada.

### A única exceção à marcha magnética burra

O destino da missão continua sendo o prédio. A **âncora imediata de movimento**
muda quando a topologia prova que a unidade não consegue chegar por terra:

```text
há rota terrestre própria     marcha magneticamente para o capturável
não há rota terrestre própria consulta MelhorEmbarque e marcha para meetingCell
```

A unidade não tenta subir o canal “a nado”. Ela facilita a coleta. O encontro é
sempre a célula terrestre do passageiro (`passengerMeetingCell`), nunca a célula
naval do transportador (`lzCell`).

### Transporte: promessa é farol, não lock

- Cada transportador ainda faz sua promessa na própria decisão.
- As promessas persistidas dos outros transportadores entram no farol coletivo.
- A atribuição é distributiva, mas não impeditiva: dois ou três transportadores
  podem convergir se isso for a melhor resposta observável.
- Quando o primeiro passageiro embarca, os demais transportadores não ficam
  presos a uma reserva inválida. A futura escolta do capitão embarcado ainda não
  existe.
- O transportador não herda nem interpreta a missão da carga. Para ele importa o
  endereço do passageiro; depois do embarque, a carga passa a publicar seu
  próprio destino.

No `Melhor LZ de Embarque` existem agora duas perspectivas:

```text
Passageiro       melhor encontro para chegar a um transportador
Transportador    melhor encontro para buscar um ou mais passageiros
```

Para múltiplos passageiros, o envelope conjunto só é obrigatório quando todos
se encontram no `Tactical`. Combinações `Tactical × Operational` ou
`Operational × Operational` mantêm o comportamento 1:1: pega um, depois o outro.

### Praias militares

`BeachManager` é serviço de mapa por cena, irmão conceitual do `RoadManager`:

- descobre praia pelo palette associado ao `TerrainTypeData`, sem nome
  hardcoded;
- componentes desconectados recebem identidades diferentes;
- divide uma costa contínua pela distância percorrida na cadeia, com extensão
  máxima configurável (padrão `6` hexes operacionais);
- usa o alfabeto fonético americano e desenha somente a inicial (`A`, `B`, ...);
- oferece pintura liga/desliga sobre a camada visual;
- expõe `BeachRepCell` para log, Inspector e rótulo.

**`BeachRepCell` não é âncora de encontro.** Praia é faixa; o encontro é a
interseção do envelope naval com o terrestre dentro dessa faixa. O representante
serve para identidade e apresentação.

O `SectorManager` consome o `BeachManager`; não varre o tilemap outra vez. Ambos
são escopados por cena/mapa, evitando que a campanha carregue topologia do mapa
anterior.

---

## Próximo teste — fechar a travessia no jogo

O build prova integração estática, não o comportamento. Retomar pelo cenário
Hot Seat simplificado e observar esta sequência:

1. No AI Stage 1, capturadores com plano recebem `MissionIntent=Capture`.
2. Os capturadores sem plano recebem somente os claims residuais do matcher.
3. Quem ficar sem par continua magnético para um capturável existente.
4. Se houver descontinuidade terrestre, o log deve mostrar pedido de carona e
   `MelhorEmbarque` fornecendo `passengerMeetingCell`.
5. Não deve aparecer, para esse caso, `Rogue marcha para âncora` apontando para
   um hex que apenas aproxima geometricamente do prédio através da água.
6. Cada transportador consulta as promessas já publicadas pelos anteriores e
   escolhe um farol de forma distributiva, sem recusar candidatos reservados.
7. No turno seguinte, confirmar que a promessa persistida ainda participa do
   farol coletivo e não causa oscilação.

Critério visual: o soldado na margem anda para a LZ terrestre útil; o navio ou
helicóptero converge para o encontro; ninguém trata a célula central da praia
como cais obrigatório.

---

## Onde ficou incompleto

- O comportamento mais recente de LZ do capturador e promessa persistida passou
  nos builds, mas ainda não foi exercitado em partida.
- O cenário completo de evacuação naval — ida, coleta, volta e entrega — ainda
  não foi fechado de ponta a ponta.
- `MelhorEmbarque` e `MelhorDesembarque` ainda não restringem diretamente seus
  candidatos pelas praias nomeadas. O provedor e o gateway pelo `SectorManager`
  estão prontos para essa ligação.
- O custo runtime de avaliar todos os transportadores e o horizonte Strategic
  para cada passageiro não foi medido.
- O cenário Hot Seat e os presets foram conferidos por diff e compilação, não por
  uma partida completa após a última alteração.
- `parcial=True` da névoa ainda não foi observado numa partida AI vs AI.
- A pausa visual do F11 continua fora da janela curta entre seleção e movimento;
  o range da IA pode estar correto e ainda assim não permanecer visível.
- **Nada da `v8.2.1` foi validado em jogo.** Cinco arquivos de lógica mudaram e a
  árvore compila (0 erros); nenhuma corrida de aceitação consta. Os cenários
  existem escritos — `PLANO_INICIATIVA_POR_DEPENDENCIAS.md` §Cenários e
  `CAPTURER_ANALISE.md` §11 — e não foram corridos.
- **Os dois planos novos não têm código.** A Fase 1 do de iniciativa (tornar a
  fila explicável **sem mudar comportamento**) é a entrada barata.
- **A promoção genérica de helicóptero continua no runtime** — Fase 3 do plano.
  Hoje todo `GameUnitClass.Helicopter` ganha precedência mesmo sem preparar ação
  de ninguém.
- **A célula `ASAP` segue inalcançável:** transportador vazio nunca publica
  `wantsRide` (cai no ramo "emergência apenas"). É o que trava o deadlock do
  soldado com 2 de autonomia — ele depende disso e não se mexeu.
- **`CLAUDE.md` continua desatualizado** sobre o ataque oportunista do courier
  (HP≤2, ≤2h): a regra só existe como cabeçalho de seção vazio e
  `Courier.Attack.cs` com zero chamadores.

---

## Arquitetura atual

### Captura

```text
BuildObjectivePlan
  ├─ publica planos formais e MissionIntent=Capture
  └─ chama MelhorCaptura uma vez para o conjunto residual
       └─ CaptureOpportunityClaimService é o único estado de claim

decisão por unidade
  ├─ tem plano  → cumpre o plano
  ├─ tem claim  → usa o endereço residual
  └─ sem par    → magnético do capturável disponível
```

Não criar um segundo sistema de reservas ao lado do
`CaptureOpportunityClaimService`.

### Transporte

```text
RidePromise persistida
        │
        ├─ farol coletivo lido por transportadores posteriores
        └─ preferência, nunca exclusão

passageiro sem rota própria
        └─ MelhorEmbarque
             ├─ lzCell                 lado naval/aéreo
             └─ passengerMeetingCell   lado terrestre, usado pelo passageiro
```

### Mapa

```text
Tilemap + TerrainDatabase
          │
          ▼
     BeachManager (por cena)
          │
          ▼
     SectorManager (consumidor/gateway)
          │
          ├─ MelhorEmbarque       ligação direta pendente
          └─ MelhorDesembarque    ligação direta pendente
```

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
                                  ✅ MelhorEmbarque tem duas perspectivas
                                  ✅ BeachManager dá semântica operacional à costa
 2. consumidores Melhor*          ✅ MelhorCaptura ganhou eixo N e matcher conjunto
                                  ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
```

Diretriz central para todos os serviços `Melhor*`:

> Eles servem tanto à IA com HQ quanto à IA sem HQ. O HQ distribui os planos;
> a ferramenta resolve os rogues. Sem HQ, todos são rogues.

---

## Documentos de referência

| documento | uso |
|---|---|
| [`relatorio_v8.2.0.md`](relatorio_v8.2.0.md) | inventário completo da versão, validações e pendências |
| [`AI Behavior/Capturador.md`](AI%20Behavior/Capturador.md) | doutrina e voz da família do capturador |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`AI Behavior/contrato_missao_captura.md`](AI%20Behavior/contrato_missao_captura.md) | publicação e baixa da missão de captura |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |
| [`AI Behavior/ficha_do_papel.md`](AI%20Behavior/ficha_do_papel.md) | matriz `Pode* → Melhor*` e questionário dos papéis |

O lema continua sendo o teste da família:

> **O capturador adianta a renda do exército. Nenhum prédio é dele, e o HP é o
> relógio.**

E a imagem operacional continua correta:

> **É a mosca atraída pela luz roxa. Ele não consegue evitar.**

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem outro candidato de ajudar |
| destino confundido com âncora imediata | o prédio permanece a missão; a LZ só substitui o próximo passo quando não há rota própria |
| planejados misturados aos rogues | o HQ publica primeiro; `MelhorCaptura` recebe apenas o conjunto residual |
| capturador sem par tratado como erro | sobra é resposta explícita e cai no comportamento magnético |
| matcher global sem histerese | o custo de troca deve nascer junto com o eixo N; hoje vale `15` |
| representante da praia usado como cais | `BeachRepCell` é metadado; o encontro nasce da interseção dos envelopes |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` pertencem à cena/tilemap corrente |
| célula naval entregue ao passageiro | passageiro marcha para `passengerMeetingCell`, nunca para `lzCell` |
| missão da carga interpretada pelo transportador | antes do embarque vale o endereço do passageiro; depois a carga publica seu destino |
| compilação `--no-restore` falha após limpeza da Unity | verificar `Temp/obj/project.assets.json` e restaurar antes de culpar o código |
| compilar não prova que o arquivo mudou | conferir o diff e o arquivo-alvo antes do commit |
| ferramenta que discorda do runtime | bancada e jogo devem chamar o mesmo serviço e passar os mesmos parâmetros |
| busca vazia tomada como prova de ausência | abrir o diff histórico e o consumidor real |
| alargar candidatos sem razão para ficar | toda expansão de horizonte precisa de aderência/histerese na mesma mudança |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação, recursos ou caches confirmados |
| **`git status` respondendo "o que existe"** | ele responde **o que mudou**. Afirmei que não havia `BeachManager.cs` na árvore olhando os arquivos *modificados*; ele está commitado desde a `v8.2.0`. Para ausência, a pergunta é `git ls-files` |
| **"não é meu trabalho" usado como "não posso fechar"** | de quem é o trabalho não importa — o ritual prevê fechar o que o autor fez em paralelo. O que importa é **ter lido o diff**. Trocar uma objeção pela outra é se esconder atrás da regra errada |
| **arquivo no `git status` sem diferença de conteúdo** | oito arquivos entraram por *line-ending* apenas, incluindo os de persistência. `git diff -U0` separa churn de mudança real antes de escrever qualquer coisa sobre eles |
| **o sucesso de uma operação apagando a reserva dela** | o claim via a célula ocupada pelo próprio táxi que acabou de entregar. Quando um efeito colateral do sucesso parece falha, procurar quem lê ocupação como veto |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.** Toda ação começa e
  termina em `CursorState.Neutral`; o meio é cancelável.
- **Plano pedido não autoriza implementação.** Avaliar e executar são trabalhos
  diferentes.
- **Doutrina mora em `docs/AI Behavior/`; comportamento observado mora ao lado
  do código.**
- **Uma frente por commit.** Não misturar código, autoria de cenário e churn do
  Editor sem necessidade.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Build runtime e Editor são separados; arquivo novo só entra no `.csproj` após
  regeneração pela Unity.
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.

---
