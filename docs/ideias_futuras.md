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

### 9. "Quero Carona" orientado por intenção (e aposentadoria do Quero Carona Aérea)

Hoje o `QueroCaronaService` responde uma pergunta booleana: *esta unidade quer carona?* Com o refactor de Vigilância Aérea introduzindo escolha de parâmetros, ele passa a ser o **ponto central de demanda por transporte** — e aí a resposta precisa carregar *para quê*.

**Intenções previstas** (checklist do desenho original):

- [ ] `CapturePressure` — capturar / pressionar setores. **Default**, é o comportamento atual.
- [ ] `AirSurveillance` — levar Radar/EWACS a setores com ganho de cobertura.
- [ ] `LogisticsSupport` — alcançar setores ou aliados com estoque crítico.
- [ ] `LandingSupport` — procurar unidade com suporte de pouso compatível (porta-aviões, fragata para helicóptero).

A quarta intenção é o que **aposenta o `QueroCaronaAereaService`**: "quero embarque aéreo" deixa de ser um serviço paralelo e vira um valor de intenção. Emergência de combustível é a mesma intenção com prioridade máxima — não um caminho próprio.

**A solicitação passa a carregar**: unidade solicitante, intenção, setor/alvo desejado, urgência, tipo de suporte ou carga exigido, ganho esperado, e uma razão legível para ferramentas e logs.

**Inversão de responsabilidade**: hoje o transportador decide sozinho o que fazer com quem quer carona. Depois, ele **lê a intenção** do pedido e decide apenas *como materializá-la* — sem atropelar as fontes oficiais:

- `PodeEmbarcarSensor` valida compatibilidade (slot, camada, classe, skills, exclusividade, vaga);
- `MelhorEmbarqueService` encontra o ponto de encontro;
- `MelhorDesembarqueService` materializa o destino;
- reservas impedem dois transportadores de atenderem o mesmo pedido.

Para `LandingSupport`, plataformas com convés (porta-aviões, fragatas) entram como transportadores especiais — o que casa com a regra de pouso em plataforma que o `PodePousarSensor.CanLandOnTransporter` já aplica e que o `MelhorPousoService` já enxerga como LZ.

**Sequenciamento**: fica **para depois** do refactor de Vigilância Aérea. A Parte 4 daquele trabalho já abre a porta ao usar alvo explícito; ampliar agora misturaria duas mudanças grandes. Entra como etapa própria logo em seguida.
