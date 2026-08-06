# Ideias Futuras

Backlog de design — sugestões discutidas mas **não implementadas**. Cada item registra a motivação e as regras esboçadas, pra retomar sem reconstruir o raciocínio. (Origem: conversas de design de logística, jul/2026.)

> **Multiplayer online** tem documento próprio: [ideias_futuras_multiplayer.md](ideias_futuras_multiplayer.md) — assíncrono PBEM como alvo primeiro, pacote de turno + hash, replay sob o fog do observador, "contato perdido".

---

## Logística

### 1. Pilhagem / Saque de recursos (roubo via transferência de time rival)

**Ideia**: permitir sugar estoque de construção **inimiga** para um transportador próprio (caminhão suga cidade rival, navio-tanque suga porto costeiro rival). Corso terrestre/naval.

**Por que não é só "abrir o PodeTransferir pra qualquer time"**: capturar custa turnos exposto roendo pontos de captura; uma transferência normal move o estoque inteiro num único confirm. Roubo instantâneo teria retorno maior que captura por uma fração do risco — vira jogada dominante.

**Regras esboçadas (verbo próprio "Saquear", com fricção simétrica à captura)**:
- Só construção **sem guarnição** (unidade inimiga em cima veta) → guarnecer depósito vira contra-jogo real.
- **Teto fixo por turno** de saque (preferido a percentual: número que o jogador conta de cabeça, ex.: "cada turno de saque = 20 galões"). Drenar cidade gorda custa múltiplos turnos atrás das linhas.
- **Barulhento**: dono recebe alerta ("sua cidade está sendo saqueada") — evento de corrida/resposta, não sangria silenciosa.
- **Consome a ação** (saqueador termina o turno no local; sem hit-and-run no mesmo turno).
- **Toggle de configuração da partida** ("Pilhagem on/off"), como o Total War: default OFF em PvE (a AI não guarnece nem reage — seria exploit grátis do humano), ON em hotseat PvP.
- Doar para rival: **não habilitar** (sem caso de uso; só ruído de UI e superfície de exploit).

**Cenário derivado**: "quem roubar X vence" — modo corsário, irmão sombrio do "quem doar X vence".

### 2. Ordem repetível de rota ("faz essa rota até eu cancelar")

**Motivação**: o risco do minigame logístico virar imposto em vez de jogo. A régua: quantas *decisões* o jogador toma por viagem? Se a resposta for "escolhe destino uma vez e repete o clique por 5 turnos", vira chore.

**Ideia**: o jogador define a *política* (rota A↔B do 18-wheeler/trem) e a unidade repete até cancelar ou a rota quebrar (inimigo no caminho, fonte seca). Remédio para tédio de playtest — **não** mexer nos números de estoque para "resolver" tédio.

### 3. AI operando a cadeia de transferência (haulage)

A AI hoje faz suprimento/reparo (Logistics), mas **não** coleta no HQ infinito pra distribuir em cidades/estações (não joga o minigame). O `PodeTransferirSensor` é a fonte de verdade pronta pra ela consumir — mesmo padrão dos outros papéis de AI.

Sub-itens:
- **Escolta de comboio**: com navio-tanque de 500 galões, interdição de comboio vira o alvo econômico mais valioso do jogo (afundar um tanque cheio > todo o combustível finito de um mapa típico). Se a AI nunca escoltar, o humano farma a logística dela de graça. Slot novo no planner.
- **Guarnição de depósito**: pré-requisito para ligar Pilhagem em PvE.
- **Resgate seguro de aeronave encalhada**: exceção cirúrgica no play-conservative do supridor — perdoar a penalidade de `rearArea` para alvo crítico (aeronave grounded 0 fuel) quando destino/corredor passam no `HasNearbyVisibleEnemy`. Ameaça real como critério, geometria como fallback. Só se playtests mostrarem caças apodrecendo em estrada segura.

### 4. Vitória por entrega ("quem doar X vence")

Cenário logístico: entregar N de um supply na(s) construção(ões)-alvo vence. Fundação existe (`isVictoryBuilding`, `VictoryReason` pluggável); falta um contador no fluxo de transferência (ponto único) + condição de vitória nova. Valores de estoque por mapa via override no `siteRuntime`, sem tocar nos assets.

### 5. Avião-tanque com auto-serviço (upgrade de elite)

Hoje: `serviceRange` 1 hex, **sem** auto-abastecimento — correto como default (loiter infinito mataria o ritmo de rotação da guerra aérea). Se um dia existir, é upgrade de elite (`eliteFrom`) com range híbrido 0–1: escolha cara, não default.

### 6. Estoque capturado — decisão pendente

Estoque da construção sobrevive à captura (ofertas são da construção, não do time) → estocar o front é emprestar pro inimigo. Tensão boa (velocidade vs segurança), **desde que o jogador saiba da regra**. Decidir: mantém herança integral, ou captura queima/saqueia parte do estoque? A pilha de ícones + marca d'água já comunicam o prêmio ("cidade inimiga gorda").

### 7. Observações de balanceamento a vigiar em playtest

- **Trem de Carga**: tropa embarcada + 300 de estoque no mesmo casco = ativo mais valioso do jogo por hex, em rota fixa (trilho = interdição previsível). Feature ou concentração de risco demais?
- **Soma finita do mapa**: o que limita não é o estoque de um prédio, é o total acessível por rota. Se ninguém puxa recurso do HQ em playtest, cortar os defaults pela metade (não repensar a tabela).
- **Artilharia acampada no barracks** (20 caixas): se virar firebase padrão, descer pra 15.
- **18W descartável**: unidade nasce de carroceria cheia → comprar caminhão = injetar 150 galões instantâneos. OK enquanto o preço doer; se virar compra-descarta, ajustar preço (não a regra).
- **Duas velocidades do combustível**: aviação sangra upkeep parada, terrestre gasta agindo — estratégia aérea é logística-intensiva por natureza (intencional; navio-tanque/porta-aviões são a conta disso).
- **Conversão 3/2/1 por classe de armadura** = imposto progressivo sobre blindado: blitz pesado profundo morre de sede sem comboio (contrapeso natural ao "MBT primeiro" do Hard).

---

## Combate / Camadas

### 8. Calibrar o "pin" de submarino (se necessário)

Navio parado sobre submarino com emersão pendente o mantém revelado, sem atacar, indefinidamente. Se playtests mostrarem que o pin é forte demais, calibrar o **custo de ficar revelado** — a mecânica de lock pendente em si não muda.

---

## IA / Transporte

### 9. Refactor do Quero Carona

A proposta de transformar `QueroCarona` em uma declaração de intenção ganhou
documento próprio:

[quero_carona_refactor.md](quero_carona_refactor.md)

O documento cobre captura, pressão, revelação de FOW, Vigilância Aérea,
logística, reparo/evacuação, suporte de pouso, reserva coletiva de construções,
save/load e aposentadoria futura de `QueroCaronaAereaService`.

---

## Captura

### 10. Eficiência de captura por par (chave × construção)

Hoje `PodeCapturarSensor.GetCapturePower(unit)` tem isto:

```csharp
unitData.roles[0] == UnitRole.CapturadorAgressivo
    → Mathf.Max(1, CeilToInt(hp / 2f))
```

Uma linha com **três** problemas empilhados:

1. **`roles[0]` estrito.** Unidade com `[Assalto, CapturadorAgressivo]` captura a
   100%. É a mesma armadilha que `UnitRoleCompatibility.CanSatisfy` existe para
   evitar.
2. **Papel de IA governando regra de jogo.** O papel é comportamento; a chave é a
   permissão. Aqui o papel decide *quanto* se captura — inclusive para o jogador
   humano, que não tem papel de IA nenhum.
3. **A eficiência é da unidade, não do par.** Não há como dizer "o robô é bom em
   cidade e ruim em bunker".

**Proposta do autor:** a v7.0.2 criou o espaço certo — a construção já lista quem
a captura. A lista passa a carregar eficiência:

```text
Required Skills To Capture
    Captura Construções      1.0
    Capturador Alternativo   0.5
    Robô Capturador          1.5
```

E a conta vira:

```text
poder = HP × eficiência do par × (0.5 se pré-requisito faltando)
```

Os dois 50% param de se confundir: um é **dado do par**, o outro é **regra de
pré-requisito**. Continuam multiplicativos, como o manual manda.

#### Decisões — fechadas pelo autor

| # | pergunta | decisão |
|---|---|---|
| 1 | unidade com duas chaves da mesma construção usa qual eficiência? | **a maior.** A unidade usa a melhor ferramenta que tem; carregar uma chave pior nunca é ônus |
| 2 | `CapturadorAgressivo` sai da conta? | **sai.** Quem captura pela metade passa a ser quem **carrega a chave 0.5**, não quem tem o papel |
| 3 | eficiência `0` é permitida? | **não.** "Tem a chave e não consegue" é confuso — para isso basta não listar a skill. Validar `> 0` no editor |

#### O que a decisão 2 exige, e o risco dela

Tirar o papel da conta **muda comportamento de jogo**, e o modo da falha é
silencioso: unidade hoje agressiva que não receber a chave `0.5` passa a
capturar a **100%**, e nada reclama — captura mais rápida é resultado legítimo.

Ordem segura:

1. criar a chave de eficiência reduzida (`Capturador Alternativo`, já existe)
2. listá-la nas construções, ao lado da chave normal
3. trocá-la nas fichas hoje `CapturadorAgressivo`
4. **auditar**: nenhuma ficha com `CapturadorAgressivo` pode ficar sem a chave
   reduzida — o `CaptureKeyAuditor` é o lugar natural desse aviso
5. só então remover o `roles[0] == CapturadorAgressivo` do
   `PodeCapturarSensor.GetCapturePower`

Enquanto os passos 1-4 não estiverem prontos, o hardcoded **fica**. As duas
regras convivendo por um tempo é preferível a uma janela em que ninguém captura
pela metade.

Repare que o papel `CapturadorAgressivo` **continua existindo** — ele só perde a
parte de regra de jogo. O que ele governa é comportamento de IA: atacar antes de
continuar capturando. Isso é dele e fica.

#### Quando

É degrau de **regra de jogo**, não de IA, e toca `PodeCapturarSensor`,
`ConstructionData`, o editor customizado e as fichas. Pelo esquema do autor é
**Y**, não Z: pega uma parte e trabalha ela e os filhos dela.

Pré-requisito: a metade da IA do critério de aceite do jipe precisa estar
validada antes — hoje só o lado do jogador foi testado.

---

## Percepção

### 12. Sensor com duas especializações de detecção (ar **e** submarino)

**Hoje nenhuma ficha tem dupla função.** A ideia é a aeronave de patrulha
marítima que varre o céu *e* a água — o P-3/P-8 do jogo. Registrado agora porque
o desenho do `MelhorDeteccao` esbarrou nela e a resposta do autor foi *"ainda não
fiz"*, não *"não existe"*.

**O que trava hoje:** `SurveillanceProfile` (`AIController.Vigilancia.cs:6-21`)
guarda **uma** `VisionCoverageLayer`, e `TryResolveSurveillanceProfile` devolve
uma só. A camada vai como parâmetro único para o `MelhorVisaoRequest.Layer`. Uma
ficha com duas entradas de detecção teria uma delas silenciosamente ignorada pela
vigilância — e o modo da falha é o pior possível: a unidade patrulha, o log diz
`layer=Air/AirHigh`, e ninguém percebe que a metade submarina nunca foi
consultada.

**As perguntas que ficam abertas:**

- **Uma varredura ou duas?** Duas chamadas de cobertura por célula candidata
  dobram o custo — e o censo de vigilância do turno 1 já mostrou ~10-21 ms por
  chamada, com o EWACS gastando 2 s em 95 delas.
- **Como se soma?** Duas redes de camadas diferentes não são comparáveis célula a
  célula: a mesma posição pode ser ótima para o sonar e medíocre para o radar. Ou
  se escolhe a camada por prioridade (e aí a prioridade mora na ficha), ou se
  soma com peso (e aí o peso é política da IA, não do serviço).
- **A postura vale para as duas?** Se a apetite de névoa vem de
  `playConservative`, uma ficha só tem um flag para duas missões.

**O caminho provável, quando existir:** a camada deixa de ser campo e vira lista,
e a vigilância trata cada especialização como uma **agenda** separada, ranqueada
entre si — que é o mesmo formato que `MelhorDesembarque` já usa para cruzar um
serviço burro consultado uma vez por sujeito.

---

## Engenharia

### 11. O capturador engenheiro — a raça que muda o mapa

Saiu do brainstorming de raças (`docs/revisao_papeis.md`). Fica aqui porque é a
única que **não** cabe na matriz sem obra.

> *Hoje o route manager monta uma estrada pronta. Mas se por um trecho oculto
> falta uma seção da ponte, o capturador engenheiro vai lá e ela aparece.*

#### Por que ela é diferente de todas as outras raças

Field medic, peão e Kradschützen reusam colunas que **já existem** — `PodeSuprir`,
`PodeTransferir`, `PodeEmbarcar`. Falta política, não maquinário: um capturador
com `isSupplier` já satisfaz `Logistica` hoje, ninguém apenas lhe pergunta nada.

**O engenheiro é a única que pede coluna nova.** Não existe `PodeConstruir` em
`Assets/Scripts/Sensors/` — verificado, a pasta vai de `PodeArremeter` a
`PodeTransferir` sem nada de construção.

#### O que já está pronto (e é a parte difícil)

`StructureData` já carrega a topologia da ponte, que é onde a complexidade mora:

- **`Ponte + Mar`** — o convés fica acima da água: `Land/Surface` e
  `Naval/Surface` deixam de ser o mesmo andar. Tanque para em cima, navio passa
  embaixo;
- **`Ponte + Praia`** — ali é cabeceira (encontro, aterro, estacas). Não há vão,
  e naval de superfície é proibido no par;
- **família topológica** — `Trilho + Ponte Ferroviária` se conectam por nó
  compartilhado, e a ponte ferroviária exige a skill de trilho no traçado
  superior sem afetar quem passa por baixo.

Logo: **a seção faltante não é caso especial — é um nó ausente da família.** O
`RoadNetworkManager` já desenha segmento por estrutura
(`StructureData.cs:191`).

#### A permissão segue a chave, como sempre

Quem decide quem fecha o vão é a **estrutura**, não a etiqueta do sujeito —
mesmo padrão de `requiredSkillsToCapture`. O teste do manual continua valendo:
o designer renomeia a etiqueta para *"eu conserto"* e nada quebra.

Se a eficiência por par (item 10) já estiver de pé, ela cai aqui de graça:
engenheiro pesado fecha o vão em 1 turno, capturador improvisado em 3.

#### O achado — o engenheiro inverte plano e realidade

Esta é a parte que some se não for escrita agora.

```text
hoje         descobrir um vão na névoa  →  o plano era MENTIRA  →  replaneja
engenheiro   descobrir um vão na névoa  →  o plano virou TAREFA
```

É a primeira vez que a IA pode **tornar o próprio plano verdadeiro** em vez de
ser corrigida pelo mundo. Para o planner isso não é feature nova — é uma
**categoria nova de resposta** a um plano invalidado, ao lado de "replaneja" e
"desiste".

#### E é a segunda instância de um padrão já nomeado

| papel | pede | a quem |
|---|---|---|
| artilheiro | **visão** que não tem | vigilância |
| engenheiro | **terreno** que não tem | engenharia |

Os dois são *um papel requisitando uma precondição a outro papel* — assunto de
`docs/AI Behavior/governanca_entre_papeis.md`. O pedido de spotter da artilharia
(`docs/ideias_melhorFow.md`) não era caso isolado: é o mesmo mecanismo, e quem
resolver um deles deve resolver o outro com a mesma peça.

#### Ordem sugerida

1. `PodeConstruirSensor` — o vão como nó ausente, permissão pela estrutura;
2. lado do jogador primeiro, como sempre (o jipe ensinou);
3. só então a IA — e a IA **só depois** que "plano virou tarefa" existir como
   resposta no planner. Sem isso, o engenheiro conserta a ponte e o planner
   segue achando que a rota é mentira.

É **X ou Y** pelo esquema do autor: sensor novo mexe em regra de jogo, e a
terceira resposta do planner é mudança de arquitetura.
