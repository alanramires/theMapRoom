# Relatorio de Atualizacao - v2.0.19

## AI Transporter (part 3)

Esta versao fecha a terceira rodada do pacote de transporte, refinando a entrega de passageiros, a escolha de rotas do APC e a disputa por vagas entre capturadores.

## Em uma frase

A IA Transportador passa a mover e desembarcar no mesmo lote quando isso acelera a captura, escolhe caminhos por custo real de movimento e evita embarques que desperdicariam turno ou vaga.

## O que isso trouxe na pratica

- APC com passageiro pode mover e desembarcar no mesmo `PlayerAction`.
- O courier simula o desembarque a partir da celula de destino antes de decidir a entrega.
- Passageiros sao colocados no melhor hex por objetivo, usando ameaca como desempate.
- Transportes deixam de atacar enquanto carregam passageiros; entregar a carga vira prioridade absoluta.
- Capturador que ja esta perto o suficiente do objetivo nao embarca.
- Capturador mais perto do objetivo cede embarque para outro capturador do mesmo setor que precisa mais da carona.
- Transportador atribuido solta a referencia do passageiro quando ele ja consegue caminhar ate o objetivo.
- APC sem passageiro pode pressionar setor e ainda quebrar bloqueio com ataque quando isso ajuda a rota.
- Movimento do transporte passa a considerar custo real de terreno da unidade, nao apenas distancia hexagonal.
- Objetivos em defesa removem slots de Transportador e limpam atribuicoes antigas desses APCs.

## Principais melhorias

1. Move + desembarque no mesmo lote
- Foi adicionado um `BuildDesembarcarBatch` com `MoveTo` e `MovementPath`.
- O APC pode andar primeiro e executar os sub-passos de desembarque depois.
- A IA usa esse caminho quando o movimento melhora a entrega em mais de 1 hex e o passageiro cai dentro do alcance de captura.

2. Courier mais focado em entrega
- O courier calcula o alvo principal do passageiro e decide entre mover+desembarcar, desembarcar parado ou continuar avancando.
- Se estiver bloqueado, ele libera a carga mesmo fora do range ideal para evitar passageiro preso dentro do APC.
- Ataque oportunista com passageiro embarcado foi removido desse fluxo para nao atrasar a entrega.

3. Roteamento por custo real de movimento
- `FindTransportMove` agora recebe a unidade e usa um mapa reverso de custo de movimento.
- O custo considera regras reais de terreno, estrada, construcao e tipo da unidade.
- Isso evita escolhas ruins em mapas onde a menor distancia em hexes nao e o caminho mais barato para o APC.

4. Embarque mais disciplinado
- Capturadores nao embarcam quando ja estao dentro do `TransportDropOffRange` do objetivo.
- Ao procurar embarque apos movimento, a IA filtra hexes ocupados para nao tentar parar em celula invalida.
- Se outro capturador do mesmo setor esta mais longe e ainda ao alcance do APC, a unidade mais proxima cede a vaga.

5. Transporte atribuido mais autonomo
- O transporte atribuido descarta passageiro que ja esta perto o suficiente para seguir a pe.
- Sem passageiro valido, ele pressiona o setor e pode atacar bloqueadores quando a rota pede.
- Shuttle e assigned passaram a usar o mesmo helper de movimento unit-aware.

6. Plano de defesa mais limpo
- Quando um objetivo entra em defesa, slots vazios e slots de Transportador sao removidos.
- Transportadores removidos do plano tem sua atribuicao limpa.
- A defesa preserva apenas unidades preenchidas de Capturador e Assalto que ainda fazem sentido naquele objetivo.

## Bloco tecnico curto

- Ajustado `AIController.Batches.cs` para suportar `MoveTo` + `Disembark` em um unico batch.
- Ajustado `AIController.Transportador.Courier.cs` para priorizar entrega, simular desembarque da celula movida e selecionar drop por distancia/ameaca.
- Ajustados `AIController.Transportador.cs`, `Assigned.cs` e `Shuttle.cs` para roteamento por custo real da unidade.
- Ajustado `AIController.Capturer.Embark.cs` para evitar embarque desnecessario, ceder vaga e filtrar paradas ocupadas.
- Ajustado `AIController.PlanEvaluator.cs` para limpar slots de transporte quando o objetivo muda para defesa.
- Ajustado `UnitMovementPathRules.cs` com `CalculateMovementCostMap`, usado pela IA para comparar rotas por MP real.
- Atualizado `CLAUDE.md` com as novas regras de transporte, move+desembarque e roteamento unit-aware.

## Resultado

Versao preparada como pacote `AI Transporter (part 3)`, focada em tornar o transporte da IA mais util, menos desperdicado e mais coerente com o terreno real do mapa.
