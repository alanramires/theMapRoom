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

