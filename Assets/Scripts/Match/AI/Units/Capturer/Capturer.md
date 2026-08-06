# AI Capturer

Este documento descreve o comportamento atual das unidades atendidas pelo modulo
`AIController.Capturer`. Ele registra a ordem real das decisoes e as regras que
devem ser preservadas ao alterar planner, shopping, transporte ou combate.

> **A DOUTRINA mora em `docs/AI Behavior/Capturador.md`** — o lema, a postura e o
> teste de cada excecao estao no §0 de la. Este documento aqui descreve o
> COMPORTAMENTO DO CODIGO: a ordem real das decisoes, os seis mecanismos de
> ceder e o inventario. Onde os dois divergirem, a doutrina manda e o codigo
> esta errado.
>
> O lema, em uma linha, para nao ter que abrir o outro: *o capturador adianta a
> renda do exercito; nenhum predio e dele, e o HP e o relogio.* Ele e o objetivo
> que TODAS as vinte excecoes deste documento servem, e sem ele elas parecem
> arbitrarias. As secoes 1 a 13 descrevem o codigo de HOJE;
> as tres ultimas descrevem o desenho para onde ele vai, e onde as duas
> divergem a divergencia esta marcada.

## Escopo de papel

- Os slots de captura do planner continuam usando o papel generico `Capturador`.
- Uma unidade pode atender esse comportamento quando
  `UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador)` for verdadeiro.
- Regras de composicao que exigem um capturador principal usam
  `UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Capturador`.
- `CapturadorCombatente` mantem a agenda de captura, mas recebe uma etapa de
  combate de abertura antes do avanco comum.
- As decisoes nao devem depender do nome da unidade ou do prefab.

## Ordem principal de decisao

`TryDecideCapturerAction` avalia as opcoes nesta ordem:

1. Entrar ou continuar no fluxo de reparo.
2. Ceder a construcao a um capturador mais forte do mesmo objetivo (`Swap`).
3. Capturar imediatamente a construcao sob a unidade, se ela nao estiver
   reservada para outro capturador.
4. Capturar um Rally Point ou outra oportunidade local antes de embarcar.
5. Defender uma construcao aliada sob pressao antes de embarcar.
6. Embarcar ou aproximar-se de um transporte adequado.
7. Executar o objetivo de setor atribuido pelo planner.
8. Sem objetivo atribuido, agir como `Rogue` apenas quando o planner marcar a
   unidade como rogue e existir um HQ inimigo conhecido.

Uma etapa que produz uma acao encerra a avaliacao naquele turno.

## Capturador atribuido

Quando existe um `SectorObjective`, a unidade procura uma construcao ainda
capturavel no setor.

- Se nao houver construcao capturavel, entra no modo `Defensor`.
- Se estiver sobre o alvo reservado de outro capturador, tenta liberar o hex.
- Se o alvo estiver na celula atual ou puder ser alcancado no turno, a
  `PontaLanca` move e captura diretamente.
- O `Perseguidor` resolve primeiro combates imediatos ligados ao avanco.
- Capturas oportunistas sao avaliadas antes do combate agressivo e do avanco
  normal.
- `CapturadorCombatente` pode abrir caminho contra uma ameaca proxima.
- Depois sao avaliados ataque defensivo de oportunidade, exploracao de alvo
  oculto e o scoring normal de movimento/ataque.

O scoring normal considera progresso real de rota, distancia ao objetivo,
DPQ, ameaca, ocupacao, preferencias de alvo e a possibilidade de atacar a
partir da celula escolhida. Distancia geometrica nao substitui distancia de
rota quando o pathfinder consegue calcula-la.

## PontaLanca

A `PontaLanca` e a conclusao direta da agenda de captura:

- captura se ja estiver no alvo;
- move e captura se o alvo estiver alcancavel;
- mantem o objetivo em estado de captura enquanto ele for valido;
- encerra a tarefa quando a construcao deixa de ser capturavel pelo time.

## Perseguidor

O `Perseguidor` trata o combate que bloqueia ou acompanha a captura:

- uma unidade sobre construcao aliada sob pressao pode ficar parada e atirar,
  mesmo que tenha sido realocada para outro setor;
- prefere `mover + atacar` quando isso mantem ou melhora o progresso de rota;
- se nao houver movimento de ataque melhor, tenta atacar da celula atual;
- pode trocar progresso por DPQ quando a situacao de combate justificar;
- todo ataque ainda precisa passar pela simulacao de sobrevivencia e dano.

## Capturador Combatente

`AIController.Capturer.Agressive` e aplicado somente quando o papel primario e
`CapturadorCombatente`.

- Atua depois das capturas diretas e oportunistas, portanto nunca troca uma
  captura segura por uma briga desnecessaria.
- Procura ameacas em raio curto e reaproveita a selecao tatica de escolta de
  assalto.
- Pode atacar para abrir passagem ao objetivo atribuido.
- Se nao encontrar ataque valido, devolve o controle ao fluxo normal do
  capturador.

O papel continua sendo capturador: o comportamento agressivo e uma capacidade
adicional, nao uma agenda independente de assalto.

> **SUPERADO PELO DESENHO.** Ver "Correcao 3 — o capturador alternativo nao
> precisa de papel proprio". A conclusao de la: `CapturadorCombatente` e a soma de
> uma **chave 0.5** com uma **ordem**, e cinco dos oito usos dele no codigo sao
> de shopping, nao de comportamento. O que sobrevive desta secao e o "abre
> passagem ao objetivo" — se isso for **selecao de alvo**, e politica de Mirar e
> continua existindo; se for so ordem, dissolve junto com o papel.

## Defensor

Depois que o setor e conquistado, o capturador passa a proteger o objetivo.

- SOS de Base/HQ pode redirecionar a defesa para uma necessidade critica.
- Rally ativo permanece como objetivo de montagem enquanto pertencer ao slot.
- Guarnicao recente e defesa critica impedem liberacao prematura do setor.
- A unidade somente libera o objetivo quando setor e area local estiverem sem
  inimigos visiveis e nao existir obrigacao critica, Rally ou guarnicao recente.
- A verificacao local usa a visibilidade real do time; inimigos adjacentes nao
  podem ser descartados por uma leitura simplificada de FoW.
- Sobre a celula representativa, ataca se houver alvo valido e, caso contrario,
  normalmente segura a posicao.
- Fora da celula representativa, tenta cobrir, interceptar, combater na zona ou
  marchar de volta.
- Uma construcao aliada sob pressao deve ser defendida do proprio hex sempre
  que sair permitir captura ou perda desnecessaria do local.

## Captura oportunista

Uma oportunidade e uma construcao capturavel e alcancavel que nao esta
completamente controlada pelo time.

- O capturador mais proximo pode reservar a oportunidade.
- A unidade atual cede quando outro capturador atribuido consegue atende-la
  melhor.
- Alvos formais de outro capturador ativo nao devem ser roubados.
- A regra e usada no fluxo atribuido, no defensor, no rogue e antes do embarque.
- Rally Points proximos recebem prioridade adicional antes do embarque.

## Explorer e alvo oculto

O `Explorer` e acionado quando o alvo ou seu ocupante ainda precisa ser
revelado.

- Usa observador avancado quando a posicao realmente melhora a revelacao.
- Caso contrario, procura uma celula de LOS/DPQ adequada.
- Pode combinar deslocamento lateral com ataque valido.
- Nao faz desvio de observacao quando o objetivo ja esta visivel e existe
  avanco util.
- Combate visivel perto do objetivo tem prioridade sobre um desvio de
  observacao.
- Apenas infantaria compativel com captura usa construcoes como observador
  avancado nesse fluxo.

## Embarque

O embarque e um meio para cumprir a agenda, nao um objetivo proprio.

- Apenas unidades compativeis com `Capturador` entram neste fluxo.
- `QueroCaronaService` decide uma unica vez se o passageiro precisa de
  transporte antes de qualquer scan.
- Unidade com plano avalia o representante e alternativas livres do setor em
  Tactical e Operational.
- Rogue ou rebelde avalia predios capturaveis livres nos mesmos envelopes.
- `IsUnderRepair` produz pedido emergencial de carona.
- A preferencia e: passageiro formal do mesmo objetivo, mesmo setor, setor
  vizinho compativel e, por ultimo, transporte livre.
- Rogue usa transporte livre ou compativel somente quando o contexto permite.
- Quando a rota propria cumpre a agenda em Tactical ou Operational, o Capturer
  recusa carona e continua sua acao normal.
- Transporte parado sobre construcao produtora deve primeiro liberar a base.
- Transporte morto, em reparo, embarcado, sem assento compativel ou com contexto
  invalido e descartado.
- A aproximacao ao transporte usa pathfinding e pode consumir um turno sem
  embarcar.
- Um capturador pode ceder o transporte a outro com necessidade maior.

`PodeEmbarcarSensor` e as regras de slot/carga do transporte sao a fonte de
verdade para autorizar o embarque.

`QueroCaronaService` nao escolhe transportador, vaga nem caminho. O mesmo
resultado positivo e propagado pelo embarque adjacente, formal, estendido,
overflow e aproximacao. O controller escolhe o transporte e materializa a acao.

## Rogue

Um capturador rogue usa o HQ inimigo como destino macro.

- Ataca imediatamente quando existe ataque valido, podendo buscar DPQ melhor.
- Sob contato inimigo, tenta primeiro uma captura oportunista e depois combate
  para abrir passagem.
- Captura o HQ se ele estiver alcancavel.
- Pode capturar oportunidades encontradas na rota.
- Se o ocupante do HQ estiver oculto, procura revelar por LOS/DPQ.
- Sem ataque ou captura, marcha pela melhor rota disponivel ate o HQ.

Rogue nao significa ignorar seguranca, FoW, reservas de captura ou simulacao de
combate.

## Swap e liberacao de hex

> Este e **um** dos seis mecanismos de ceder. Os outros cinco e a analise do
> conjunto estao em "A familia do ceder". E ver "Correcao 2": este swap compara
> **HP cru**, e isso quebra no dia em que a chave de eficiencia entrar.

`Swap` evita que um capturador danificado bloqueie seu proprio objetivo:

- aplica-se a capturadores de composicao primaria;
- exige outro capturador do mesmo objetivo;
- o substituto deve ter mais HP e conseguir chegar no turno;
- antes de sair, a unidade ocupante pode executar combate util;
- depois tenta continuar sua propria agenda sem bloquear o substituto.

Os helpers de `Vacate` ficam nesta pasta por origem historica, mas tambem sao
usados por outros papeis para liberar construcoes produtoras. Eles nao devem ser
tratados como regra exclusiva de capturador.

## Combate e fontes de verdade

As decisoes deste modulo devem respeitar:

- `TeamObjectivePlan` e `SectorObjective` para agenda e reservas;
- `UnitRoleCompatibility` para capacidade e composicao de papel;
- `UnitMovementPathRules` para alcance e rotas;
- `PodeMirarSensor` para confirmar que um alvo pode ser atacado;
- simulacao de ataque/HP para dano, morte e sobrevivencia esperados;
- `MatchController` para visibilidade e FoW;
- `PodeEmbarcarSensor` para embarque;
- `ConstructionManager` e `SectorManager` para captura, dono e distancia;
- ocupacao atual e destinos ja planejados para evitar colisao entre unidades.

Preferencias `Primary` e `Secondary` do `UnitData` alteram o score de alvo, mas
nao tornam um ataque ilegal em legal.

## Logs esperados

As categorias principais sao:

- `Capturador`: fluxo geral e movimento atribuido;
- `PontaLanca`: chegada e captura direta;
- `Perseguidor`: combate ligado ao avanco;
- `CapturadorCombatente`: abertura de caminho;
- `Oportunista`: captura local e reservas;
- `Explorador`: revelacao e observador avancado;
- `Defensor`: manutencao ou liberacao do setor;
- `Rogue`: avanco sem slot formal;
- `Swap`: substituicao no objetivo;
- `Base`: liberacao de construcao produtora.

Ao adicionar uma nova ramificacao, o log deve informar unidade, motivo, alvo,
celula escolhida e os bloqueios relevantes. O log deve explicar a decisao sem
substituir a validacao pelos sensores.

---

# A familia do ceder — e o plano de virar politica

> **Status:** o inventario e verificado no codigo. O plano de extracao (§3) e
> desenho, sem uma linha escrita.

## 1. O que sao, de verdade

As dezenove entradas da ordem de decisao **nao sao dezenove formas de
capturar**. Capturar e uma acao so: `PodeCapturar` responde, `BuildCaptureBatch`
executa. O que varia e **quem** captura, **quando** cede e **se termina**.

> Nao sao formas de capturar. Sao politicas de **coordenacao** de captura.

Todas nasceram como **excecao para otimizar a captura inteligente**, e quase
todas foram escritas antes de existir a nocao de Tatico/Operacional — por isso
carregam numero de hex cravado:

```text
AggressiveCapturerEngagementRadius   = 3
ExplorerForwardObserverTargetRadius  = 3
ThreatRadius                         = 3
HexDistance(fromCell, enemyCell) <= 1.5f     adjacencia
CapturerRideOperationalTurns         = 2     este ja nasceu banda
```

Cada politica extraida e uma oportunidade de trocar o numero fixo pela banda da
**unidade avaliada** — e agora isso deixou de ser elegancia: **jipe e barco
pirata capturam**, e um raio 3 de infantaria nao descreve nenhum dos dois.

## 2. Os seis mecanismos de ceder

| # | nome | quem cede | para quem / por que |
|---|---|---|---|
| 1 | **handoff blitzkrieg** | quem **abriu** a captura | ninguem — nao termina o predio, segue no eixo e outra infantaria fecha atras. So `hardMode` |
| 2 | **swap** | capturador **fraco** sobre o alvo | outro do **mesmo objetivo com HP maior** que chega este turno |
| 8 | **cede hex alheio** | quem esta sobre alvo de **outro setor** | marca o proprio hex como ocupado para se forcar a sair no scoring |
| 11 | **cede oportunista** | quem ia capturar de graca | o capturavel esta **reservado** para outro |
| 16 | **cede alheio bloqueado** | preso sobre alvo de outro | sai para **qualquer** hex livre, sem escolher |
| 20 | **libera producao** | qualquer um sobre produtora | **duas razoes distintas**, abaixo |

O item 20 sao dois mecanismos com o mesmo log `TL("Base")`:

```text
IsActiveUnitBlockingThreatenedHomeProduction   "libera producao (AMEACADA)"     defensivo
TryFindProductionUnlockVacateAction            "libera produtora travada        economico
                                                (3/5 ocupadas, cheapest=$1200)"
```

### O que salta aos olhos olhando os seis juntos

**O handoff e o unico que cede sem destinatario.** Todos os outros cedem *para
alguem* ou *por causa de alguem*. Ele cede para o **eixo** — e doutrina de blitz,
nao coordenacao entre pecas. Por isso e o unico preso a uma dificuldade.

**Tres mecanismos resolvem "estou sobre o predio de outro" (2, 8, 16).** O 8 sai
elegante, o 16 sai desesperado, o 2 sai porque chegou gente melhor. Sao tres
caminhos e tres logs para uma pergunta so.

**O 11 e o 17 sao a mesma busca com a reserva ligada e desligada.**
`TryFindOpportunisticCapture` respeita reserva; `TryFindUnreservedOpportunisticCapture`
ignora, e so roda quando o avanco travou.

**Esperar (18) nao e ceder — e o oposto.** Aliado sobre o alvo e a peca fica
parada. O que decide entre esperar e sair e so *onde ela esta*: sobre o alvo
alheio sai, ao lado do proprio alvo espera.

## 3. O plano — uma politica para cada, em pasta propria

**CONTRATO.** Desenho do autor, nada escrito.

### 3.1 A base e a mais burra, nao a mais esperta

Hoje o comportamento mais simples — **captura e fica**, mesmo fraco, mesmo que
alguem melhor terminasse — so existe no caminho **rebelde**, e e o **ultimo**
consultado depois de dezenove excecoes.

Como politica ele inverte de lugar: vira a **base declarada**, e as dezenove
viram variacoes opcionais em cima dela.

E ele tem cliente imediato: **capturador burro combina com IA Iniciante.** A
dificuldade deixa de ser um `if` espalhado e passa a ser *qual politica o papel
carrega*.

### 3.2 A politica e parametro do papel, nao do papel dela

Isto fecha com `docs/AI Behavior/ficha_do_papel.md` §7.3: a celula `Capture` da
ficha **nomeia uma politica**. E por isso um papel que nao e capturador pode
preencher a celula:

> Um **jipe de assalto** que chama capturer faz a consulta como atalho e pega a
> politica do capturador burro — a armadura dele justifica nao ser esperto.

Sem politica nomeada, esse jipe herdaria as dezenove excecoes escritas para
infantaria a pe.

### 3.3 A fronteira que nao pode ser cruzada

A pasta pode viver sob `Services/`, **desde que o que mora nela sejam objetos de
politica — dado e predicado — e nao servicos que decidem.**

```text
MelhorCaptura (servico)   "onde da para capturar, e a que custo"
CapturePolicy (politica)  "eu cedo? eu termino? eu espero?"
AIController  (organizador) usa as duas
```

> **A politica decide COM a resposta do servico, nunca no lugar dela.**

No instante em que uma politica comecar a varrer tabuleiro, ela virou servico
disfarcado — e o ranking volta para dentro da camada errada, que e o erro que a
tabela das tres camadas existe para impedir.

### 3.4 Checklist por politica extraida

```text
[ ] qual numero fixo ela carrega, e qual banda o substitui
[ ] ela vale para jipe e barco, ou so para infantaria a pe?
[ ] ela cede PARA alguem, POR alguem, ou para o eixo?
[ ] qual o log de entrada e o de baixa
[ ] ela e default, opcional, ou NAO SE APLICA (ficha_do_papel §4.1)
```

---

# A prioridade do capturador, e o porque de cada casa

> **Status:** desenho do autor. A ordem NAO esta implementada — hoje o roteador
> tem ordem fixa e global. As tres divergencias marcadas **MUDA REGRA** alteram
> codigo vivo; se entrarem como politica nova ao lado da regra antiga, a antiga
> ganha.

## O Pre — Repair nao e casa do questionario

```text
TryDecideRepairAction — chamado de SEIS lugares
  Router.cs:44
  Capturer.cs:23    FireSupport.cs:77    Logistics.cs:24
  Stock.cs:83       Transportador.cs:39
```

Todo papel o consulta **antes** da propria ordem, e nenhum papel pode reordena-lo.
Na ficha isso e uma linha **acima** da lista, com a mesma natureza da invariante
transacional: nao e politica, e regra. Hoje esta copiado em seis lugares — um
`Pre` declarado resolveria os seis.

`AIController.Repair` governa retaguarda **e** fusao, e e para la que o
controlador do capturador joga a bola.

## A ordem

```text
1  Capturar      2  Detectar     3  Enxergar    4  Embarcar   5  Desembarcar
6  Mirar         7  Fundir       8  Suprir      9  Transferir 10 Reposicionar
```

### 1. Capturar — capturar rapido e com maior eficiencia

**HP e a taxa de captura** (`PodeCapturarSensor.GetCapturePower` devolve HP;
metade para `CapturadorCombatente`). Tudo abaixo decorre disso.

- **Largar meia captura** quando a da frente esta vazia e alguem atras fecha.
  Economia de turno. **MUDA REGRA:** hoje isso e o handoff blitzkrieg,
  `hardMode`-only e guiado por eixo, com dicionario `unidade -> predio aberto`.
  Como politica base vira aritmetica, e o blitz vira so um limiar mais frouxo.
- **Capturar sempre com HP cheio.** Nao e preferencia, e a taxa — dai o swap
  (cede para quem tem mais HP) e o `CapturadorCombatente` como reserva.
- **Capturador de outro plano da uma forcinha** quando o dono do objetivo esta
  no Operacional (nao chega neste turno). **MUDA REGRA:**
  `IsOtherAssignedCapturerTarget` (`Capturer.cs:52`) hoje barra alvo alheio
  **incondicionalmente**. A regra nova a condiciona a banda.
- **Rogue e atraido magneticamente** por missao proxima, incluindo reconquista.
- **Carona quando o alvo esta no Operacional**; desembarque no **Tatico ou
  Operacional do alvo** — a banda que o `TransportDropOffRange = 4` deveria ser.
- **Prefere nao lutar**: HP e precioso porque HP e captura. Morder uma vitoria
  de graca no caminho e bem-vindo, sempre sob o attack decision.

### 2 e 3. Detectar e Enxergar — subiram porque economizam turno

Chegar na nevoa **impede capturar no mesmo turno**. Revelar antes vale um turno
inteiro de acao.

Duas coisas moram nesta linha, e sao diferentes:

```text
eu vou revelar          acao minha
eu peco que revelem     pedido a OUTRO papel (governanca_entre_papeis.md)
```

**Detectar** e quando os caminhos estao livres e ainda assim algo impede: alguem
ocupa o alvo e nao se sabe quem.

**Nenhuma das duas existe no runtime.** Correspondem a `RevelacaoDeContato` e
`RevelacaoTerritorial`, marcadas como brainstorming em `contrato_missoes.md`.
Duas das dez casas estao vazias.

### 4 e 5. Embarcar e Desembarcar

Acima de Mirar porque servem a casa 1: carona e o jeito de alcancar o que esta
fora do Tatico. Ver `docs/AI Behavior/Transporte.md`.

### 6. Mirar — abaixo do transporte de proposito

O `CapturadorCombatente` **inverte** isto. Em ficha, inverter nao e codigo novo:
e **outra lista**, outro `RoleData`.

### 7. Fundir — regra de recuo E politica de eficiencia

Hoje a fusao so existe dentro do `AIController.Repair.cs`, ligada a manutencao.
A **fusao por eficiencia** e codigo novo.

**O gate nao e a soma de HP — e a ausencia de trabalho paralelo.** Duas unidades
nao pisam no mesmo hex:

```text
2 x HP5, DOIS predios    5/turno cada, dois predios andando em paralelo
1 x HP10, UM predio      10/turno, um predio em metade do tempo
                         mesma soma de pontos, resultados diferentes
```

```text
ambos com capturavel alcancavel na banda  ->  NAO fundir (abandona um predio)
so um alvo para os dois                   ->  fundir ganha tempo
nenhum alvo na banda                      ->  ganha tempo E sobrevivencia
```

O HP elevado e **consequencia**, nao condicao — assim a regra e derivavel e nao
precisa de limiar autorado, que e o tipo de numero que envelhece igual aos raios
cubicos de 3.

**Custo de acao:** fundir consome a acao dos **dois** (ambos marcam agiu) e um
morre. Por isso a preferencia por absorver **quem ja agiu**: a fusao passa a
custar uma acao em vez de duas.

**Unidade ainda operacional tambem funde.** Um HP4 pode **atrair** um HP5 para
formar um HP9. Atrair significa **deslocamento dos dois** — e isso e o mesmo
problema do encontro do embarque, ja resolvido em `MelhorEmbarque`
(`ResolvePassengerMeeting`: encontro pelo dicionario antes da sonda cara). Nao
inventar rendezvous novo.

**A VERIFICAR, e decide se a politica presta:** a fusao em cima do predio
**preserva `currentCapturePoints`**? O progresso mora na construcao, mas o
ocupante muda de identidade. Se o motor zerar captura na troca de ocupante,
`15/20` vira `0/20` e a economia vira desperdicio silencioso.

**A VERIFICAR:** o `PodeFundir` permite absorver quem **ja agiu**, ou exige os
dois disponiveis? Se exigir, a condicao de economia inverte.

### 8 e 9. Suprir e Transferir — para unidades futuras

`fieldMedic` e engenharia. Hoje NAO SE APLICA ao capturador.

### 10. Reposicionar — o magnetico

`repCell` ou o plano, quando nada mais e possivel. E o rogue de hoje ganhando
lugar declarado em vez de ser fallback implicito do roteador.

## Onde a fusao de eficiencia mora

Ela e **politica de captura** — existe para aumentar a taxa de captura — mas o
mecanismo (`PodeFundir`) hoje so e chamado pelo Repair.

Escrita dentro do Repair por proximidade, ela some do lugar onde faz sentido
procura-la e o capturador volta a ter decisao invisivel. Pertence a
`Services/CapturePolicy/`, executando por um sensor que o Repair tambem usa.

---

# O lema, e as tres correcoes que ele produz

## O lema

> ## O capturador adianta a renda do exercito.
> ## Nenhum predio e dele, e o HP e o relogio.

E a imagem do autor, que explica por que as vinte excecoes existem:

> *"O capturador e a mosca atraida pela luz roxa. Ele nao consegue evitar."*

**As seis formas de ceder sao o contrapeso da compulsao.** Se a atracao nao fosse
irresistivel, nao seria preciso seis regras mandando ele sair de cima da luz. As
excecoes nao contradizem o lema — elas existem porque o lema e obedecido demais.

Cada clausula gera uma familia inteira:

```text
"adianta a renda"        pressa, carona quando o alvo esta no Operacional,
                         desembarque no Tatico do alvo. Renda antecipada COMPRA
                         a proxima captura — ela compoe, nao so soma

"nenhum predio e dele"   handoff, swap, ceder ao oportunista, ceder hex alheio,
                         nao estorvar. Cinco das seis caem daqui: o dono do
                         resultado e o exercito, nao a peca

"o HP e o relogio"       GetCapturePower devolve HP. HP nao e vida, e VELOCIDADE.
                         Dai evitar luta, capturar com HP cheio, e fundir
```

E as dez casas fecham nele — nenhuma serve outra coisa:

```text
Capturar              adianta renda agora
Detectar / Enxergar   destrava a captura que a nevoa esta segurando
Embarcar/Desembarcar  encurta o caminho ate ela
Mirar                 so quando o turno rende mais assim
Fundir                conserta o relogio para capturar mais rapido
Suprir / Transferir   (futuras)
Reposicionar          anda na direcao da PROXIMA — magnetico ao repCell,
                      que e a celula representativa do proximo predio
```

Nem a acao nula e neutra.

## Correcao 1 — o gate do Capturar e auto-contido

Chegou-se a propor que o `Capturar` precisasse espiar o que o `Mirar` responderia
antes de declinar (senao ele declinaria por um tiro que nao existe). **Nao
precisa** — era erro de categoria:

```text
"ha alvo no meu Tatico?"      FATO      pergunta ao PodeMirar / ameaca
"o que o Mirar decidiria?"    DECISAO   acoplaria as duas casas
```

O gate so precisa do fato. O questionario continua **primeira nao-nula ganha**,
cada casa se resolve sozinha, e nao ha necessidade de espiar, de repescagem nem
de pontuar todas as casas na mesma moeda.

## Correcao 2 — o swap compara CAP POWER, nao HP

`FindSwapIncomingCapturer` compara **HP cru**. Funciona hoje porque
`GetCapturePower` devolve HP. **Quebra no dia em que a chave de eficiencia
entrar** (`ideias_futuras` item 10):

```text
bazooka HP10 x 0.5 = 5   vs   soldado HP6 = 6
HP cru       ->  bazooka fica, capturando pela metade
cap power    ->  bazooka cede, que e o certo
```

O modo da falha e silencioso: a peca mais lenta segura o predio e o log diz que
ela e a mais forte. **Consequencia do item 10 que o item nao menciona.**

## Correcao 3 — o capturador alternativo nao precisa de papel proprio

O bazooka (e a metranca) le o mesmo questionario, na mesma ordem. O que muda e
a **chave 0.5** na ficha da unidade — e ela morde no terceiro passo do gate:

```text
1. o sensor devolveu opcao?           senao -> NAO SE APLICA
2. alguem com CAP POWER maior fecha antes?  -> cedo (swap)
3. vale o meu turno?                  <- aqui a chave 0.5 morde
```

Dito pelo lado positivo, que e como o autor formulou: **sem combate no meu
Tatico e sem capturador melhor que alcance o meu Tatico, ele captura para
quebrar o galho.** Meio relogio bate relogio nenhum.

Zero copia de papel, zero skill nova. Uma skill que "promove Mirar" seria poder
disfarcado de chave e falharia o teste do renome; a chave 0.5 e legitima porque
**quem a lista e a construcao**.

Consequencia: `CapturadorCombatente` pode sair do enum algum dia, pelo roteiro
seguro do item 10 — e a discussao de renomea-lo ("Capturador Alternativo" ja e o
nome da CHAVE, nao pode ser o do papel) deixa de precisar de decisao.
