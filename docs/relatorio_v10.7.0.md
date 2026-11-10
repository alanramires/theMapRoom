# v10.7.0 — A compra precisa cumprir a missão

Fechamento de 09/11/2026. O shopping passou a distinguir **ter arma, ter papel e conseguir
chegar**. As marcas mostraram que essas três perguntas eram respondidas em portas diferentes:
o rally aceitava um tanque ilhado, o counter comprava um caça pela metralhadora e o pré-requisito
de elite contornava a exclusão terrestre por ter uma ficha exata.

A lição não é criar outro shopping. É conservar a intenção da demanda até a seleção da ficha,
da produtora e da cadeia. O combate continua podendo aproveitar uma arma secundária; isso não
transforma a unidade numa compra apropriada para aquela missão.

## 1. O helicóptero entrega no lugar da reunião

`7bd499b`: RallyAssembly deixava os passageiros embarcados porque tentava resolver a entrega
como captura de prédio. `TryBuildAirRallyAssemblyDelivery` passou a usar a âncora do rally e a
área atual de reunião, mesmo com o prédio ocupado. Reutiliza `MelhorDesembarqueService` para a
combinação e `PodeDesembarcar` para a legalidade, permite entrega parcial e preserva outra missão.
EVAC continua antes desse ramo. **Visto em Play:** o autor confirmou que o Chinook soltou os dois.

Isso é o primeiro ajuste localizado do assemble. Não implementa o montinho completo da proposta,
o core por unidade-modelo ou todas as decisões de reforço/liberação discutidas em `docs/rally/`.

## 2. Parede mista pede resposta mista, com peso

`f7490c7`: suporte terrestre prefere AtaqueAereo; antiaéreo prefere Assalto terrestre. Sem oferta
ou acesso, permite a alternativa nos dois sentidos. Caixa insuficiente não converte a preferência:
continua valendo a poupança. Apache principal DisruptorDeCaptura permanece separado dessa demanda.

Normal: três pontos por resposta, teto global três. Artilharia níveis 0/1/2+ vale 1/1,5/3 pontos;
Assalto terrestre vale metade: 0,5/0,75/1,5. Não depende do nome “tanque”. Antiaéreos ficam no
placar que pede Assalto; suporte e Assalto inimigos no que prefere ataque aéreo. Cada InstanceId
conta uma vez; ArtilheiroCombatente não pontua novamente como Assalto. Vagas proporcionais,
desempate por cobertura e custo; uma peça não cobre os dois grupos.

Na invasão final conserva uma resposta por artilharia, sem multiplicador de elite nem teto três;
Assalto acrescenta sua pressão normal. Cobertura existente com acesso e compras do carrinho
descontam vagas. O acesso usa o mapa terrestre existente do SectorManager por perfil/alvo,
inclusive para distinguir produtoras com o mesmo catálogo.

As marcas registram o ramo em execução (`pressao=3,5`, resposta aos antiaéreos convertida para ar),
mas não demonstram ainda todos os limiares, distribuição e comportamento na invasão. Esses casos
passaram em fixture fora do Play. Referência: `docs/shopping_resposta_artilharia_proposta.md`.

## 3. O caça não precisa beliscar um SAM enquanto há presa aérea no Operacional

`51ab4fb`: Caça B #73 tiraria 1 HP do SAM #70 com VG, preferência terciária. A trava “ar primeiro”
só olhava aeronaves atacáveis nesta rodada. Não houve comparação perdida pelo helicóptero:
ele estava fora do Tático e nem participou.

Antes do tiro terrestre terciário, o interceptador agora consulta uma presa primária/secundária
no Operacional real, compatível com arma carregada. Reutiliza `TryResolveOperationalHuntTarget`
e `FindAirCombatAdvanceMove`; só troca se existe avanço. InterceptadorDeDisruptor é incluído por
`MatchesLabel`; Disruptores e ataques aéreos táticos conservam o fluxo.

**Visto em Play:** na observação seguinte #73 moveu para (6,5), adiantando interceptação do
Chinook #18 e adiando o tiro no SAM. O autor perguntou se perseguia o Bombardeiro: **o log
confirmou Chinook**, não Bombardeiro. Não se deve inferir o alvo pelo desenho do movimento.

## 4. A mesma peça ilhada apareceu por duas portas

`8d540ed`: o Tanque B da primeira marca vinha de `rally-assembly`. Essa demanda especial pulava
a conversão de operações comuns. `ConfigureRallyShoppingAccess` agora valida a produtora até a
âncora real; sem ruptura terrestre ofertada com acesso, admite AtaqueAereo da família Assalto.
Artilharia pesada sem acesso deixa de gerar compra encalhada; não vira AA para fingir fogo pesado.

O core já conta pela família: dois Apaches contam como dois Assaltos; quatro Soldados como quatro
Capturadores. Os parâmetros do AI Manager e a prontidão do rally não foram reescritos. A validação
por produtora acompanha a demanda nos seletores de carrinho/poupança e não é perdida ao fundir
demandas de rallies diferentes. **Visto em registro de Play:** a compra do rally virou Bombardeiro,
e a artilharia foi recusada por falta de acesso. Entrega e conclusão do rally ainda pendentes.

Depois o tanque reapareceu. **Não foi a correção do rally falhar:** o log mostrava outra origem,
`counter-pressure-prerequisite`. Queria liberar Tanque A contra infantaria de outra ilha.
`RequiredUnitId` pulava a exclusão terrestre.

A correção final aplica `CounterOffLandShare` e `OffersNonLandCounter` antes da escolha da elite
e retira a exceção da ficha exata. A régua existente é majoritariamente fora da terra (>= 50%),
havendo resposta não terrestre ofertada; sem alternativa conserva o fallback anterior. Não é uma
nova busca por distância. Pressão local preserva a cadeia Tanque B → A. Compromisso terrestre
antigo também é invalidado quando as demandas atuais daquele matchup excluem terra. **Só em
teste fora do Play nesta revisão**; não declarar resolvido em partida antes de ler a próxima compra.

## 5. O Caça A anti-infantaria era uma compra errada

`a094d68`: o autor achou que o Caça A nascia para combater Bombardeiro ou por invasão final.
O registro desmentiu: era `counter-pressure-elite` AntiInfantaria, postura Tactical. A primeira
explicação do agente tratou o encaixe como aceitável; o autor corrigiu a doutrina.

`IsCounterResponseRoleAllowed` restringe interceptadores às contramedidas AntiAerea. A mesma
pergunta é usada no roster, filtro final, bônus, oferta não terrestre e validação de compromisso.
Não altera as fichas nem proíbe tiro oportunista em combate. **Teste fora do Play:** Caças A/B
recusados contra solo; suas demandas aéreas, Apache e Bombardeiro preservados. Falta validar compra.

## 6. O AI Step mostra qual era o alvo

`65a0efe` e `a364632`: movimento fica azul escuro; envolvidos e relações ficam rosa. A presa da
interceptação usa `TraceAIReviewParticipant`, a mesma infraestrutura de embarque/revezamento,
vinculada à ação escolhida e ao snapshot congelado, com opção de focar. Não há nova varredura de
alvos no painel. Compilou; visibilidade e foco do novo marcador ainda precisam ser vistos pelo autor.

## 7. Entender a árvore antes da terceira refatoração

`0b37f86`: `docs/arquitetura/arquitetura_atual.md` mapeia fluxo principal, Capturador, retornos
antecipados, políticas compartilhadas e acessíveis só em alguns galhos. Confirma uma combinação
de sequência por prioridades e pontuação local, não uma IA inteira reduzida a switch/case.
Separa fatos, suspeitas e oportunidades. Nenhuma refatoração foi executada; a terceira fica para
depois da revisão da Aeronáutica.

`8669794`: comentário da skill `pergunte-me` explica o processo em inglês, e `CLAUDE.md` registra
o fluxo de colaboração. A entrevista fecha doutrina antes de código; não substitui investigação.

## Reuso e validação

Revisão contra “procure a que já existe”: entrega usa MelhorDesembarque/PodeDesembarcar; acesso
usa SectorManager e a pergunta de âncora existente; perseguição usa envelope e avanço existentes;
marca usa o participante já instrumentado; exclusão do pré-requisito usa a régua de ilha do
counter. Não entrou uma segunda regra global de ocupação nem um sensor novo de movimento.
O contexto de acesso reaproveitado no rally ainda tem nome ligado à parede; limpeza de nome ou
extração pode ser avaliada depois, sem fazer outra refatoração neste fechamento.

`bash tools/compilar.sh`: ambas assemblies passaram após as mudanças. Fixtures executadas fora
do Play, sem confirmar ações na partida:

- `verificar_desembarque_rally_aereo.cs` e regressão EVAC no trabalho inicial.
- `verificar_demanda_parede_artilharia.cs`: pesos, mistura, cobertura, invasão e acesso por produtor.
- `verificar_interceptador_presa_operacional.cs`: envelope real, aproximação e filtros de presa.
- `verificar_shopping_rally_acesso.cs`: oferta acessível, alternativa aérea e contagem do core.
- `verificar_interceptador_counter_terrestre.cs`: missão do caça antes da arma secundária.
- `verificar_counter_prerequisito_ilha.cs`: elite isolado, ficha exata, alternativa e cadeia local.

Testes e limites detalhados em `docs/ai_test.md`. Compilar e fixture não equivalem à validação
da partida. Os testes não executaram ponta a ponta a migração do ledger de poupança antigo.

## O que não terminou

- Ver em Play as duas correções finais do counter e o resultado completo do novo shopping.
- Rally/montinho continua parcial: decisões de `docs/rally/rally_proposta.md` não significam
  implementação integral. Não reescrever a prontidão ao fechar este dia.
- Medir custo de mapas terrestres do shopping em mapas grandes, com muitos perfis/alvos.
- Marcação rosa e foco da presa: compilados, confirmação visual pendente.
- Terceira refatoração adiada até terminar a Aeronáutica; arquitetura é diagnóstico.
- Herdados da v10.6.0: dupla avaliação de `AircraftRepairRecovery`; verificações em partida de
  reserva de pouso, EVAC sem avanço, fusão segura, recuperação na célula futura e swap.
- Herdados da v10.5.0: Serviço do Comando após load e possível cobrança repetida; volume do
  AudioManager 0,49 → 0,70; casos do anel/EVAC conjunto ainda sem nova confirmação neste fechamento.

## Docs de doutrina

Desatualizados (apontados, não reescritos neste fechamento):

- `docs/rascunhos/AI Shopping.md` e `docs/Planos/Reformular Shopping.md`: localizar a consolidação
  das demandas híbridas/ponderadas, missão do interceptador, acesso por produtora e pré-requisitos.
  A referência herdada a `Shopping.md` §6 não corresponde a um arquivo em `docs/AI Behavior/`;
  conservar o débito e usar os caminhos existentes. Parte observada no registro de Play; counter
  final e limiares completos só em fixture.
- `docs/AI Behavior/Transporte.md`: entrega localizada no rally (vista em Play); manter os débitos
  EVAC/hospital pela regra de reparo, prédio+LZ, anel em degraus, entrega conjunta e lista da v10.4.
- `docs/AI Behavior/Assalto.md`, `Papeis.md`, `ficha_do_papel.md` e `docs/magnetic_tabela.md`:
  aproximação operacional do interceptador (vista em Play), distinção compra/arma oportunista
  (só em fixture) e contribuições aéreas ao rally. Manter os débitos anteriores do Disruptor.
- `docs/AI Behavior/Logistica.md`: manter 4 faixas, prazo dentro da faixa e aeronave que cai antes
  (régua/prazo vistos em Play); recuperação na célula futura, EVAC e hospital (só em teste).
- `docs/AI Behavior/Capturador.md`: manter swap que captura sem reservar o segundo prédio, C6,
  espera fora da produtora e herdeiro do blitz (sem nova validação hoje).
- `contrato_questionario.md`: manter reparo antes do Serviço do Comando; reserva de pouso só da
  IA e fusão onde a execução aceita (sem documento próprio de reparo).
- Carregados: `Vigilancia.md`, `FireSupport.md` e demais débitos de papéis dos relatórios anteriores.

Prontos para consolidar:

- Nenhum novo item declarado pronto neste fechamento. Aeronáutica, rally e shopping mudaram;
  para os demais débitos não foi demonstrada aqui a combinação de dois fechamentos estáveis,
  validação em Play e ausência de marca aberta. A consolidação continua sendo chamada à parte.
