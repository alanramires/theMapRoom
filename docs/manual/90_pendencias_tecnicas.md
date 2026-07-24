# Pendências Técnicas

*Divergências entre a regra canônica e o comportamento implementado.*

> Derivado do Manual Técnico versão 9. Em caso de divergência entre documentos desta biblioteca, vale a ordem de precedência declarada em `00_fonte_unica_e_indice.md`.

Esta seção não é regra. É a lista de pontos onde o comportamento implementado e a intenção declarada neste manual podem estar em desacordo, registrados aqui para que ninguém os descubra por acidente e os tome por doutrina.

**Sobre os identificadores.** Cada pendência tem um ID estável no formato `ÁREA-NNN`. O ID nunca é reaproveitado, mesmo depois de resolvido, e é por ele que uma entrada de auditoria em `92_auditoria.md` e um parágrafo canônico se referem à mesma regra sem depender de o texto permanecer idêntico. Áreas em uso: `LOG` logística, `COM` combate, `FOW` visão e informação, `AIR` operações aéreas, `IA` inteligência artificial.

Dois itens que já foram desta lista — a isenção de consumo por presença no aeroporto e o alcance do Serviço do Comando aos passageiros — saíram porque **não são divergências**: são design deliberado. Migraram para `91_decisoes_de_design.md`. Os IDs `AIR-001` e `LOG-001` ficam aposentados e não serão reaproveitados.

---

### LOG-002 — Peso de munição com valor global escondido

**Regra canônica.** O peso por classe de munição é atributo da ficha de serviço.

**Comportamento atual.** Quando a ficha não declara o peso, o sistema aplica 3/2/1 por conta própria. Hoje é o caso de Reabastecimento e Reparos.

**Evidência.** `ServiceData.cs:173-181`.

**Impacto.** Baixo em comportamento, alto em previsibilidade: quem edita a ficha não vê o valor que está em vigor.

**Status.** Aberta.

---

### FOW-001 — Duas fontes para a duração da emersão forçada

**Regra canônica.** O submarino fica exposto por dois turnos do proprietário, tanto ao atacar quanto ao ser atingido (`05_visao_deteccao_e_nevoa.md`).

**Comportamento atual.** A emersão por ataque próprio lê a duração da ficha do submarino; a emersão por dano recebido usa um valor fixo do sistema. Hoje ambos valem 2 e o comportamento é coerente.

**Evidência.** `UnitData.emergeAfterAttackTurns` contra `ScannerPrompt.cs:4118` (`const int forcedTurns = 2`).

**Impacto.** Latente. Mudar a ficha desalinharia os dois sem aviso nenhum.

**Status.** Aberta.

---

### IA-001 — A inteligência artificial trata alcance 0 como 1

**Regra canônica.** Alcance 0 é recurso suportado, para armamento lançado sobre o próprio setor contra alvo em outro andar (`06_combate.md`).

**Comportamento atual.** O simulador de combate da IA recusa distância 0 de saída, e o chamador compensa enviando 1 no lugar. A IA avalia o ataque como se fosse adjacente, supõe um contra-ataque que nunca vai acontecer, e **subestima** a arma.

**Evidência.** `AICombatHpSimulator.cs:80` e `TurnStateManager.Automation.cs:391`.

**Impacto.** Uma fragata sob controle da IA não caça submarino com carga de profundidade.

**Status.** Aberta, adiada por decisão — a IA naval ainda não foi construída.

---

### FOW-002 — A divisão C/D/T contra E/F/S em terreno explorado não tem princípio declarado

**Regra canônica.** Com destino provisório em terreno apenas explorado, ficam liberados ataque, desembarque, captura e transferência; embarque, fusão e suprimento seguem suprimidos (`04_ciclo_de_acao_e_comprometimento.md`).

**Comportamento atual.** É exatamente isso que o código faz. O problema não é divergência — é que **nenhum princípio conhecido explica a linha**.

O argumento anti-oráculo justificaria bloquear o que revela presença inimiga. Mas os seis verbos miram **aliados ou construções**: transferência só alcança unidades do próprio time (verificado, FOW-020), embarque é sobre transportador aliado, fusão sobre unidade aliada, suprimento sobre unidade aliada, captura sobre construção estática e lembrada pela fotografia. Nenhum deles vaza inimigo. Logo, o critério que separa os liberados dos bloqueados não é o anti-oráculo — e não está escrito em lugar nenhum qual é.

**Evidência.** `TurnStateManager.Sensors.cs:460-499` (`RunExploredTerrainContextSensors`). O único comentário diz "Em terreno apenas memorizado, E/F/S continuam sob a barreira anti-oraculo", sem justificar por que E/F/S e não C/D/T.

**Impacto.** Baixo em jogo, alto em manutenção. Sem princípio declarado, a próxima pessoa que mexer nesses sensores não tem como decidir de que lado um verbo novo cai — e o manual declara em `00` que cada regra deve ter um endereço e um motivo.

**Decisão necessária.** Uma das três: (a) existe um motivo que não foi registrado, e basta escrevê-lo; (b) a linha foi traçada por instinto e deve ser refeita sob um princípio único; (c) a linha está certa e o princípio é outro que ainda não nomeamos.

**Status.** Aberta, sem prioridade.

---

### FOW-003 — Recusa de desembarque nomeia unidade não detectada

**Regra canônica.** O menu nunca filtra pela verdade oculta, e nenhuma recusa deve ensinar o que o time não sabe (`04_ciclo_de_acao_e_comprometimento.md`).

**Comportamento atual.** A checagem de ocupação do desembarque lê a ocupação crua, sem filtro de névoa, e devolve o **nome** do bloqueador: `"Hex ocupado por {blocker.name}"`. Isso entrega a identidade de uma unidade que o jogador nunca detectou.

**Evidência.** `PodeDesembarcarSensor.cs:295-299`.

**Impacto.** Vazamento real, e de categoria pior que os demais: presença já seria informação, mas identidade não tem equivalente em nenhum outro sensor. Acontece hoje em terreno visível e explorado, onde o desembarque roda normalmente.

**Decisão necessária.** Recusar sem nomear, e sem distinguir "ocupado" de "inválido" quando a unidade não é conhecida pelo time — o padrão de motivo neutro que a exceção de ataque no escuro já usa.

**Status.** Aberta. Consertar independe da decisão de FOW-002.

---

### LOG-003 — Supridor comprado já vem cheio, o que curto-circuita a cadeia

**Problema de design.** Se todo supridor nasce com a reserva cheia, a cadeia logística perde a razão de existir: em vez de montar trem → caminhão → front, ou trem → navio-tanque → porta-aviões, basta comprar direto o nó menor e usá-lo cheio. O elo mais barato substitui a corrente inteira, e o `07` promete uma logística que precisa ser construída.

**Proposta.** Um campo novo em `UnitData`, na seção de logística, logo abaixo de `isSupplier`: **"começa com 0 carga"**, com padrão **true**. Quem nasce cheio passa a ser a exceção declarada, não a regra silenciosa.

**Valores pretendidos por unidade** (definidos pelo autor, ainda não aplicados):

| Unidade | Começa com 0 carga | Razão |
|---|---|---|
| Trem de Carga | **false** — nasce cheio | é a fonte que alimenta a malha |
| Caminhão de Suprimentos | **false** — nasce cheio | comprado na construção, sai carregado |
| Avião-Tanque | **false** — nasce cheio | coletou no aeroporto onde foi comprado |
| Porta-Aviões | **false** — nasce cheio | coletou no porto de onde veio |
| Caminhão 18 rodas | **true** — nasce vazio | precisa ser carregado pela cadeia |
| Navio-Tanque | **true** — nasce vazio | comprado nas Docas, não no Porto Naval |
| Hidroavião | **true** — nasce vazio | a Hidrobase fornece o que tiver |

O critério que emerge da tabela: nasce cheio quem foi comprado **em uma instalação que tinha o que dar**; nasce vazio quem foi comprado em instalação que não abastece aquele tipo de carga.

**Impacto.** Restaura o valor da cadeia e dá sentido econômico às Docas e à Hidrobase como pontos de carregamento, não só de compra.

**Status.** Aberta, especificada. Implementação sugerida em duas partes: o campo e a aplicação no spawn em C#, e a marcação por ficha feita no inspector da Unity — assets não devem ser editados no disco com o editor aberto.

---

### LOG-004 — Perfil de autonomia "Rotor" nomeia mal o hidroavião

**Comportamento atual.** O `AR Hidroaviao` usa o perfil de autonomia **Rotor Autonomy** (consumo 2 por turno). O valor está correto e não se discute; o **nome** é que descreve asa rotativa, e o hidroavião não é helicóptero.

**Evidência.** `AR Hidroaviao.asset` → `autonomyData` GUID `86f5ee65…` = `Rotor Autonomy` (`turnStartUpkeep: 2`).

**Proposta.** Duplicar o perfil como **"Monomotor"**, com os mesmos valores, e repontar o hidroavião para ele. Puramente estético — nenhuma mudança de comportamento.

**Status.** Aberta, cosmética. Exige criação de asset no editor.

---

### IA-002 — A IA não distingue adversários e é cega para a corrida do primeiro abate

**Regra canônica.** A primeira eliminação encerra a partida inteira, e o vencedor é quem executou o abate (`10_turnos_jornal_e_vitoria.md`). Em partidas de três ou mais, isso torna o adversário **mais fraco** o alvo estrategicamente correto — ele é o gatilho mais barato.

**Comportamento atual.** O modelo de mundo da IA funde todos os inimigos numa massa única: `EnemyUnits`, `EnemyBuildings` e um `EnemyHQ` **singular**, que recebe o primeiro QG inimigo encontrado na varredura da cena. Não existe representação de "adversário A" contra "adversário B".

**Evidência.** `AIWorldSnapshot.cs:68` — `if (c.IsPlayerHeadQuarter && snap.EnemyHQ == null) snap.EnemyHQ = c;`

**Impacto.** Nulo em partidas de dois lados, onde o modelo agregado é adequado. Em três ou mais, a IA joga por atrito contra a massa inimiga e ignora que existe um adversário quase morto valendo a partida inteira. O QG que ela ataca é decidido pela ordem dos objetos na cena, não por proximidade de derrota.

**Decisão necessária.** Se partidas de três ou mais forem cenário suportado, o snapshot precisa segmentar inimigos por slot e o planner precisa de um critério de "quem está mais perto de cair". Se forem apenas curiosidade, basta registrar a limitação.

**Status.** Aberta. Mais consequente que vários itens desta lista se o multiplayer de 3+ virar prioridade.

---

### IA-003 — A fragata é comprada como caçadora e operada como unidade genérica

**Regra canônica.** A fragata existe para caçar submarino: sensor antissubmarino mais carga de profundidade de alcance 0 (`06_combate.md`).

**Comportamento atual.** O shopping funciona pelo motivo certo — há demanda explícita de `RaidAntiSub` quando a IA detecta capacidade submarina inimiga. A **operação** não existe: `RaidAntiSub` não tem tratador no roteador, e o único lugar que reconhece o papel é o combate aéreo, que exige `data.domain == Domain.Air`. Sendo Naval, a fragata passa por todos os tratadores e cai no `HexEvaluator` genérico.

**Evidência.** `AIShoppingPlanner.Demand.cs:2582` (demanda); `AIController.AirCombat.cs:39-44` (`IsAirCombatUnit` exige domínio aéreo); `AIController.Router.cs` (sem ramo para `RaidAntiSub`).

**Impacto.** A IA compra a unidade certa e a usa como barco armado qualquer. Não mantém contato, não fica sobre o alvo, não usa o sensor. Soma-se ao `IA-001`: mesmo que caçasse, o ataque de alcance 0 seria subavaliado.

**Decisão necessária.** Não resolver empurrando a fragata para o tratador aéreo — ele lida com decolagem, altitude e duelo aéreo, e produziria comportamento pior que o genérico. O que falta é lógica de caça antissubmarino própria.

**Status.** Aberta. Lacuna de escopo, não bug de uma linha.

---

### IA-004 — Navio de Desembarque opera com heurística de APC

**Regra canônica.** O desembarque anfíbio funciona porque o passageiro materializa sobre o transportador e pisa num vizinho válido — o navio nunca precisa alcançar a terra (`08_transporte_fusao_e_operacoes_aereas.md`).

**Comportamento atual.** `MA Desembarque` tem papel Transportador e **entra** no tratador de transporte, porque o gate é por papel e não por domínio. Como não é aéreo, cai no mesmo fluxo do APC (shuttle / courier / assigned).

As peças de baixo nível são agnósticas e funcionam: o embarque é ação do passageiro via `PodeEmbarcarSensor`, e o `PodeDesembarcarSensor` valida o destino pelas regras do passageiro. O que é terrestre é o **raciocínio** em volta:

- `FindTransportMove` roteia pelo custo do próprio transportador — correto para o navio (só água), mas o alvo é um objetivo em **terra** que ele nunca alcança;
- `TransportDropOffRange = 3` foi calibrado para APC, que descarrega e a tropa segue andando. No desembarque anfíbio o navio precisa estar **encostado** na praia certa.

**Evidência.** `AIController.Transportador.cs:20-47`; constantes em `AIController.Transportador.cs`.

**Impacto.** Não trava. O risco é o navio oscilar na costa sem concluir entrega, e nunca escolher a praia certa por não haver nada que modele "levar tropa por mar e desembarcar no litoral".

**Status.** Aberta, não observada em Play. Diferente do Chinook, que tem lógica dedicada (`AIController.Transportador.Air.cs`) e funciona.

---

### DAT-001 — Navio Tanque tem papel fora do enum

**Comportamento atual.** `MA Tanque` (id `navioTanque`) declara `roles: 10000000` = **16**. O enum `UnitRole` termina em `TransportadorAereo = 15`. É valor inválido.

**Evidência.** `Assets/DB/Units/Unit List/Marinha/MA Tanque.asset:35`; `Assets/Scripts/Units/UnitRole.cs`.

**Impacto.** Desconhecido e silencioso: comparações de papel simplesmente não casam, então a unidade tende a desaparecer da lógica de decisão sem erro visível.

**Decisão necessária.** Escolher o papel correto — pelos atributos (`isSupplier`, tier Hub) os candidatos são `Logistica (4)` ou `Suprimentos (7)`. É decisão de design, por isso não foi corrigido por conta.

**Status.** Aberta. Marcação de ficha no inspector.

---

### MAP-001 — Rota com salto desenha linha contínua que o trem não percorre

**Regra canônica.** O Trem de Carga passa entre dois hexágonos apenas quando eles aparecem **consecutivos** na lista de células de uma rota declarada (`03_movimento_terreno_e_infraestrutura.md`).

**Comportamento atual.** A validação de rota confere cada célula **individualmente** — terreno e domínio — e **nunca verifica se células consecutivas são de fato hexágonos vizinhos**. Se o autor do mapa declarar um salto na lista:

- o **visual desenha** a linha entre as duas células distantes, porque o desenho apenas liga os centros par a par, sem checar distância;
- o **movimento ignora** aquele par, porque a busca só testa arestas fisicamente adjacentes.

O resultado é uma ferrovia que **parece contínua no mapa e não é percorrível**, sem nenhum aviso — a rota passou na validação.

**Evidência.** `RoadNetworkManager.cs` — `IsRouteValid` (valida célula a célula, sem adjacência) e `CreateRouteSegments` (liga `cells[i]` a `cells[i+1]` por posição de mundo). Consumo em `UnitMovementPathRules.HasConnectedRouteSegmentAllowingUnit`.

**Impacto.** Não corrompe partida e não permite teleporte — o efeito é uma linha morta. Mas é silencioso e difícil de diagnosticar: quem desenhou vê o trilho na tela e conclui que o trem está bugado.

**Decisão necessária.** Acrescentar checagem de adjacência em `IsRouteValid`, emitindo o mesmo `LogWarning` que as demais rotas inválidas já emitem. Alternativa mais branda: manter a rota válida e avisar apenas no salto.

**Status.** Aberta. Ferramenta de autoria, não regra de jogo.

---

### MOV-001 — Emersão forçada pendente sob a ponte nunca foi testada

**Regra canônica.** Um submarino atingido por armamento apropriado é forçado a emergir; se o hexágono não permitir a emersão, a ordem fica **pendente** e o relógio de exposição não corre até ela acontecer (`05_visao_deteccao_e_nevoa.md`).

**Comportamento atual.** Desconhecido — é o ponto. A estrutura da ponte declara `forceEndMovementOnTerrainDomainForDomains`, criado para o submarino **parar** sob o vão. Essa regra nunca foi exercitada em conjunto com o lock de emersão forçada.

O cenário não testado: submarino que encerra o movimento sob a ponte por força do `forceEndMovement` **e** carrega uma emersão pendente por ter sido atingido. As duas regras se encontram num hexágono onde a superfície pode estar ocupada pelo convés, pelo tanque em cima dele ou por um navio embaixo.

**Evidência.** `StructureNavalOpsTerrainRule.forceEndMovementOnTerrainDomainForDomains`; interação com `HasPendingForcedLayerLock` e `PodeEmergirSensor.CanApplyLayerTransitionAtCell`.

**Impacto.** Provavelmente não é erro visível — o risco é o submarino ficar num estado que nenhuma das duas regras previu, com a exposição congelada indefinidamente por um bloqueio que o próprio sistema criou.

**Decisão necessária.** Testar o caso em Play antes de qualquer mudança. Só depois decidir se o `forceEndMovement` deve ceder ao lock, o lock ao `forceEndMovement`, ou se a combinação já resolve.

**Status.** Aberta, não diagnosticada. Registrada por ser combinação conhecida e não exercitada, não por sintoma observado.

---

### OCP-001 — Multiocupação por camadas estava presa ao Total War *(corrigido)*

**Regra canônica.** Os três andares do hexágono — ar, superfície, profundezas — são regra de tabuleiro e valem em qualquer partida (`02_dominios_terrenos_e_ocupacao.md`).

**Comportamento anterior.** `IsLayerAwareRulesActive` exigia `EnableLayerOccupancyResolver && IsTotalWarEnabled()`. Sem Total War, **todo** o modelo de camadas desligava e o jogo revertia para uma unidade por hexágono: nenhuma aeronave sobre tanque, nenhum submarino sob navio, em nenhum modo sem névoa.

O erro conceitual: FOW é cobertura de **informação** sobre o tabuleiro, não um modo de regras. Modos sem névoa (neblina leve, montanha, gameboy, física básica) apenas revelam o tabuleiro — a ocupação deveria ser idêntica. O próprio código descrevia a condição como "flag de rollout local", o que indica dívida de implantação, não desenho.

**Correção aplicada.** `IsLayerAwareRulesActive` passou a depender apenas de `EnableLayerOccupancyResolver`. O que é de fato exclusivo do Total War — dois **inimigos** dividirem a **mesma** banda, o hex disputado — foi separado em `AllowsEnemyShareInSameBand`, aplicado só onde cabe.

**Evidência.** `OccupancyResolver.cs` (`IsLayerAwareRulesActive`, `AllowsEnemyShareInSameBand`, `CanEndMoveInBand`).

**Status.** Corrigido, aguardando teste. Verificar nos modos sem névoa: aeronave sobre unidade terrestre, submarino sob navio, e — como controle — dois aliados de superfície ainda bloqueando entre si.
