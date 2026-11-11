# Relatorio — 2026-11-11

## Tela de formacao do rally (somente leitura)

Comandos no `PanelDebugController`: `ai show rally [on|off] [slot]` e `show rally [on|off] [slot]`. Sem slot, acompanha a vez na proxima atualizacao confirmada. `show rally off` remove a pintura e o relatorio. A tela ocupa a janela do AI Step, com arraste, redimensionamento e bloqueio de clique/zoom sob o painel; abrir/fechar o relatorio nao liga AI Step, nao pausa, nao aprova batch e nao move unidade.

`QuadranteData` ganhou o Header Rally com `rallyModelLand` e `rallyModelNaval`, expostos no Map Helper. Sem quadrante ou modelo configurado, o modelo efetivo e o Soldado do UnitDatabase da partida, identificado no painel como `modelo: fallback`. A escolha terra/mar le o terreno do pivo; nenhum predio precisa receber uma ficha individual.

`RallyAreaService` calcula Core/Tactical e Approach/Operational pelo `UnitReachEnvelopeService`, intencao Mobility, a partir da celula do rally. O envelope aceita um modelo de ficha isolado: adaptador inativo, oculto, nunca registrado em AllActive e destruido ao fim da consulta. Nao usa Apply/Setup nem altera unidade real. A malha de movimento por terreno existente evita que os ocupantes recortem a area. Nenhum numero de hex substitui as bandas.

Vanguard e Rear vem de `AIBacklineAnalyzer.Analyze`, com as configuracoes Default e pivo no rally. Os aliados combatentes sao os do slot dentro de Approach; inimigos entram somente pela visibilidade confirmada do slot. Como a ancora da ferramenta e fixa, foi adicionado um override opcional de orientacao ao analisador: massa inimiga conhecida ou direcao do eixo do slot na ausencia dela. O padrao dos consumidores anteriores continua identico. Faixas sao recortadas pelo Approach e permanecem disjuntas; nao ha ranking nem escolha de destino no servico.

`RallyFormationReport` le objetivo RallyAssembly, SlotNeed preenchidas/vazias e RogueUnitIds. Solicita ao dono original o resultado de EvaluateRallyReadiness; Status e Missing sao os existentes, sem reconstruir BuildRallyMissingText. Foram expostos quatro fatos na estrutura AIRallyReadiness: pacote de hold, ruptura, artilharia e forca. O comparador de ruptura foi apenas nomeado e reutilizado; os raios, pesos, contagens, condicoes e resultado GoGreen nao mudaram.

Participantes aparecem uma vez por identidade, com posicao atual (ou a do transporte quando embarcados), Core/Approach/Outside, destino da decisao observada e Mission Intent, enquadramento do destino e indicador de que estao indo para a area. O conjunto antigo plannedDestinations nao guardava a identidade do sujeito: a Fase 2 agora publica esse dado de apresentacao ao lado da acao ja produzida, limpa por passada e remove no fracasso, sem influir na decisao. A posicao esperada le familia e banda das armas; a possibilidade de revide vem do sensor existente. Aeronave que so ataca solo fica atras pelo criterio da arma, sem lista de nomes de unidades.

Pintura em `TurnStateManager.RallyDebug`: Core azul, Approach laranja, Vanguard vermelho, Rear violeta e pivo branco. Linhas de participantes usam as mesmas trilhas/material/ordenacao/animacao do AxisDebug: verde para destino na area, cinza fora. A apresentacao possui colecoes e limpeza proprias, sem apagar show axis. Recalculo com `force: true` no OnBoardSettledAtNeutral, depois da nevoa confirmada; abertura em Neutral tambem solicita essa leitura. Fora de Neutral aguarda. Nao assina evento de movimento nem recalcula visibilidade.

### Validacao e limites

- `bash tools/compilar.sh`: **Assembly-CSharp e Assembly-CSharp-Editor verdes**, incluindo todos os arquivos novos.
- `tools/ai_test/verificar_tela_rally.cs`: **PASS fora do Play**. Rally com modelo e tres unidades (core, indo, fora), Core diferente de Approach, Core contido em Approach, Vanguard/Rear nao vazios e disjuntos, orientacao pelo eixo sem inimigo conhecido, vaga + rogues sem duplicacao, destino observado e missao de fallback, demanda preenchida/vazia, modelo mais lento reduzindo a area e parser dos aliases/on/off/slot. Contagem de AllActive e revisao global do tabuleiro inalteradas, sem HasActed nem posicao alterados pela consulta.
- Regressao fora do Play: desembarque do rally aereo, ancora por exposicao do interceptador e politicas sem plano passaram. O comportamento padrao do analisador permanece sem override.
- **Nao visto em Play:** janela final, cores/legibilidade/anel, trilhas animadas, troca automatica de slot e limpeza visual ao desligar. Integracao da prontidao, do modelo naval e do fallback de catalogo compilou; o fixture de tela valida a geometria terrestre e os fatos dos participantes, nao uma partida naval nem o fluxo visual completo.
- Nenhuma unidade foi movida pela tela. Nenhuma regra de prontidao ou composicao foi alterada. Nenhuma porta nova por faccao ou ausencia de QG. Edit mode nao e suportado nesta entrega.

Entrega termina no commit da tela, para revisao do autor em Play. Nenhum degrau posterior de prontidao foi iniciado.


## D1 — Matriz de Forcas, sem alterar fichas

- `UnitForce` derivado, sem campo em UnitData/save: Assalto → Ruptura; FogoDeSuporte → Dissuasao; Capturador/Transportador → Ocupacao; Vigilancia/Logistica → Apoio; ausencia → Nenhuma. `ResolveForce` le a familia, sem tabela por nome de unidade.
- `AtaqueAereo` (exibido como **Ataque ao Solo** no Inspector, valor serializado 9 preservado) e `ArtilheiroCombatente` agora pertencem a FogoDeSuporte. Apache continua DisruptorDeCaptura/Assalto/Ruptura. Todo ArtilheiroCombatente compoe FogoIndireto, inclusive o blindado Tanque Z.
- `RallyFormationReport.ExpectedPosition`: so Ruptura fica na vanguarda; demais Forcas na retaguarda. Nao consulta posicao, distancia nem armas/revide. Fichas, rotulos, MatchesLabel, CanSatisfy, escada aerea, negacao de escolta e carona aerea nao foram editados.
- `verificar_matriz_forcas.cs`: PASS fora do Play. Imprime todas as fichas de todos os UnitDatabase, por catalogo (rotulo → familia → Forca), verifica as pecas pedidas e demonstra a expectativa de formacao com unidade distante e sem armas runtime.
- **Efeito medido de demanda:** com 4 Soldados + 1 Tanque Z, a composicao pede **Assalto x2, Fogo x0**. No controle 4 Soldados + 1 Tanque B, pede **Assalto x1, Fogo x1**. O Z nao substitui mais o Assalto do pacote 2/2/1 e ja preenche o fogo pela familia. A parede ponderada ja classificava ArtilheiroCombatente como artilharia: o Z elite 2 conserva os **3 pontos**, sem alterar sua formula.
- **Fronteira D1/D2 confirmada:** a matriz e a posicao esperada ja colocam o Z em Dissuasao, e o helper antigo de ruptura do rally agora devolve 0 por composicao. Entretanto `GetRallyArtilleryWeight` ainda usa `HasRole` literal e devolve **0** para ele. Nao foi corrigido no D1: a migracao da contagem de prontidao para Forcas pertence ao D2. Isso foi avisado ao autor; nao declarar o rally integralmente migrado neste passo.
- Regressao: shopping sem QG, controle preventivo do Caca A, counter/pre-requisito ilhado, tela de rally e parede ponderada: PASS fora do Play. `bash tools/compilar.sh`: ambas assemblies verdes. Nenhuma partida em Play, nenhuma compra/acao confirmada nem ficha alterada.
- D2 nao iniciado: nenhum raio, piso, gate de artilharia, razao local, saturacao ou capitao alterado. Aguardando revisao do D1 antes de prosseguir. Sem commit do D1 nesta etapa.


### D1 — Progressao de elite do Pesado conferida

`AddEliteProgressionDemand` usa `CountsForEliteLine`, que no acesso terrestre le `ResolveCompositionRole`: com a matriz D1, ArtilheiroCombatente/Pesado compoe FogoIndireto, portanto sai da progressao de Assalto. Com MBT em campo, Assalto nao encontra outro degrau. Nao foi adicionado teto artificial nem gate por nome.

`IsEliteChainAvailable` ja consulta a referencia `eliteFrom`, independentemente da familia do pai. Fichas reais conferidas: **Tanque Z → Tanque A/MBT**; **Artilharia de Campanha → Obuseiro Movel**. A referencia do Pesado nao precisa ser mudada para uma artilharia. Nao foi necessario alterar nenhuma dessas duas funcoes nem qualquer ficha.

Fixture de shopping que cobre a mudanca de resultado: **`verificar_matriz_forcas.cs`, caso de progressao do Pesado**. Antes da reclassificacao, o Pesado era o proximo elite de Assalto depois do MBT. Agora o caso exige **zero demanda de progressao Assalto com MBT**, e **uma demanda FogoIndireto elite 2**, atendivel tanto pelo Pesado quanto pela Campanha. Verificou tambem Leve → MBT no nivel 1 com core completo; retirar MBT bloqueia somente a cadeia do Pesado, retirar Obuseiro Movel bloqueia somente a Campanha. PASS fora do Play.

As duas fichas continuam respeitando postura, oferta/acesso, core, caixa e poupanca; compartilhar a linha nao significa comprar ambas nem ignorar `aiPurchaseMode`. As regressoes antigas de shopping nao tinham o caso do Tanque Z e continuam PASS (sem QG, controle do ar, counter ilhado; tela e parede tambem passaram na D1). A expectativa alterada ficou explicita no caso novo, sem atribuir uma falha a fixture antiga que nao falhou.

D2 continua nao iniciado. Sem partida em Play e sem commit nesta etapa.

