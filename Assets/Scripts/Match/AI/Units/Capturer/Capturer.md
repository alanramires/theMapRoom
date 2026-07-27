# AI Capturer

Este documento descreve o comportamento atual das unidades atendidas pelo modulo
`AIController.Capturer`. Ele registra a ordem real das decisoes e as regras que
devem ser preservadas ao alterar planner, shopping, transporte ou combate.

## Escopo de papel

- Os slots de captura do planner continuam usando o papel generico `Capturador`.
- Uma unidade pode atender esse comportamento quando
  `UnitRoleCompatibility.CanSatisfy(data, UnitRole.Capturador)` for verdadeiro.
- Regras de composicao que exigem um capturador principal usam
  `UnitRoleCompatibility.ResolveCompositionRole(data) == UnitRole.Capturador`.
- `CapturadorAgressivo` mantem a agenda de captura, mas recebe uma etapa de
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
- `CapturadorAgressivo` pode abrir caminho contra uma ameaca proxima.
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

## Capturador Agressivo

`AIController.Capturer.Agressive` e aplicado somente quando o papel primario e
`CapturadorAgressivo`.

- Atua depois das capturas diretas e oportunistas, portanto nunca troca uma
  captura segura por uma briga desnecessaria.
- Procura ameacas em raio curto e reaproveita a selecao tatica de escolta de
  assalto.
- Pode atacar para abrir passagem ao objetivo atribuido.
- Se nao encontrar ataque valido, devolve o controle ao fluxo normal do
  capturador.

O papel continua sendo capturador: o comportamento agressivo e uma capacidade
adicional, nao uma agenda independente de assalto.

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
- `CapturadorAgressivo`: abertura de caminho;
- `Oportunista`: captura local e reservas;
- `Explorador`: revelacao e observador avancado;
- `Defensor`: manutencao ou liberacao do setor;
- `Rogue`: avanco sem slot formal;
- `Swap`: substituicao no objetivo;
- `Base`: liberacao de construcao produtora.

Ao adicionar uma nova ramificacao, o log deve informar unidade, motivo, alvo,
celula escolhida e os bloqueios relevantes. O log deve explicar a decisao sem
substituir a validacao pelos sensores.
