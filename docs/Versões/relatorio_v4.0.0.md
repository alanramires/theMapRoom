# v4.0.0 - AI Novo Shopping Manager

Esta versão coloca em produção o shopping orientado a papéis preparado na v3.0.8. O caminho legado, imperativo e com mais de mil linhas de flags acopladas, é substituído por uma fila única de demandas pontuada. O legado permanece no código apenas como modo de comparação.

## Arquitetura

- `AIShoppingPlanner.Decide` passa a bifurcar logo no topo: com `UseRoleBasedShopping` ativo chama o novo `DecideRoleBased`; caso contrário usa o caminho legado.
- O novo planner é declarativo: toda decisão de compra vira uma `AIShoppingDemand` (papel, papel exato, domínio, classe-alvo, faixa de elite, contagem, prioridade, urgência, origem e motivo).
- A seleção é gulosa por turno: enquanto há orçamento, varre edifícios × unidades ofertadas × demandas, pontua cada candidato e compra o melhor.
- Sem hardcode por nome de unidade. Compatibilidade de papel é resolvida por `UnitRoleCompatibility`.

## Fila de demandas

`BuildRoleShoppingDemands` mescla demandas de várias fontes e ordena por urgência, prioridade e papel:

- **Operações táticas** — déficits do `AITacticalAnalyzer` mapeados para papel, domínio e elite. Urgentes mantêm a prioridade da operação.
- **Composição base** — pacote 2/2/1 (capturador / assalto / fogo indireto) escalado pela massa do exército.
- **Antiaéreo** — por aeronaves inimigas visíveis, alternando AAA combatente e SAM.
- **Ruptura** — bombardeiro contra parede de artilharia inimiga.
- **Intel** — quando o oponente possui capacidade aeroportuária.
- **Anti-sub** — quando há submarino visível ou porto inimigo.
- **Logística** — por unidades próprias em reparo.
- **Progressão elite** — para assalto e fogo indireto, atrás de gate de massa e caixa.

## Pontuação e gates

- Score parte da prioridade invertida, com forte bônus para demandas urgentes.
- Foco de alvo soma pela prioridade da unidade contra a classe-alvo da demanda e contra a classe inimiga dominante visível.
- Elite é gated por economia madura (massa mínima e caixa): liberada quando pronta, fortemente penalizada quando não.
- Postura influencia o score via `aiPurchaseMode` da unidade.
- Filtros duros antes da pontuação: custo, exclusão de Marinha, postura permitida e cadeia elite disponível (`eliteFrom` já em campo).
- Cada edifício produz no máximo uma unidade por turno.

## Compatibilidade de papéis

- Papéis de composição (capturador, assalto, fogo indireto) usam `ResolveCompositionRole` para correspondência exata.
- Papéis híbridos (`ArtilheiroCombatente`, `AntiaereoCombatente`, `CapturadorAgressivo`, `LogisticaMovel`) satisfazem mais de uma demanda via `CanSatisfy`, mas cada unidade atende apenas uma demanda por compra.

## Pendências conhecidas

- A reserva de caixa entre turnos do legado (juntar para elite, transporte aéreo, passageiro de capturador) não foi reproduzida; o novo planner é guloso e depende do gate de economia para não comprar elite cedo demais.
- Necessário revalidar do zero, a começar pelo mapa Exército contra Exército, antes de avançar para os mapas com Aeronáutica e Marinha.
- Marinha permanece provisória e fora desta etapa.

## Validação

- `dotnet build Assembly-CSharp.csproj`
- Resultado: 0 erros.
- Permanecem apenas avisos obsoletos já existentes nas APIs Unity.
