# v4.8.1a — Refactor da AI Logistica e de Estoque 1/5

## Visão geral

Esta revisão fecha a primeira aplicação prática da rede criada em `v4.8.1`.
O estoque já conseguia apontar encontros entre Hubs, Receivers e construções,
mas a autoridade final da transferência ainda tratava a unidade selecionada
como se ela fosse sempre a responsável pelos dois sentidos da troca.

Isso escondia situações legítimas. Um Hidroavião podia estar ao lado de um
Navio Tanque carregado e, mesmo assim, o sensor exigia uma construção. Agora a
pergunta é feita do ponto de vista correto: **quem está cedendo os recursos,
até onde a ficha dessa unidade permite entregar?**

## A logística passa a trocar recursos entre unidades

O `PodeTransferir` deixa de reconhecer apenas a relação entre unidade, Hub e
construção. Unidades logísticas aliadas com o serviço `Transfer` também passam
a formar conexões diretas.

A natureza declarada na ficha continua valendo:

- dois Hubs podem receber e doar entre si;
- um Hub pode abastecer um Receiver;
- um Receiver recebe do Hub, mas não se transforma em fornecedor;
- estoque, capacidade e tipos de carga precisam ser compatíveis;
- uma unidade embarcada em outro transportador não fica exposta à rede apenas
  por compartilhar uma coordenada.

O alcance é direcional. Em uma opção **Receber**, vale o `collectionRange` da
unidade que entrega. Em uma opção **Doar**, vale o alcance da unidade
selecionada. Assim, o Hidroavião não ganha alcance adjacente por conveniência:
é o `Hybrid0Or1Hex` do Navio Tanque que autoriza a entrega.

O mesmo princípio foi levado ao Melhor Estoque. Os hexes possíveis de encontro
agora consideram os alcances das duas fontes potenciais, mas toda recomendação
continua sendo filtrada pelo próprio `PodeTransferir`.

## O Hidroavião pousa para receber a carga

Transferir recursos entre camadas diferentes não virou uma exceção artificial.
Quando uma aeronave precisa alcançar a camada operacional de quem entrega a
carga, o sensor consulta o `PodePousar`.

No encontro entre Navio Tanque e Hidroavião:

1. o terreno real sob o Hidroavião é resolvido;
2. construções, estruturas, Aircraft Ops e skills de pouso são avaliados;
3. o pouso em `Naval/Surface` é incluído na opção;
4. a confirmação revalida o pouso antes de tocar no estoque;
5. o Hidroavião pousa na água;
6. somente então os recursos são transferidos.

A aeronave permanece pousada depois da operação. O recebimento de carga não
cria uma decolagem automática nem contorna as regras aéreas consolidadas no
refactor de mudança de camada.

Essa preparação funciona tanto quando a aeronave é a unidade selecionada
quanto quando ela é a outra ponta da transferência. A execução guarda
separadamente qual unidade deve pousar e qual será sua camada de atendimento.

## Ferramenta de campo mais fiel

A janela **Tools > Logistica > Pode Transferir** passa a reproduzir melhor o
contexto real da partida e também o Scene Editor.

Fora do Play Mode, ela encontra as unidades diretamente na cena, pois
`UnitManager.AllActive` ainda não foi preenchido. O serviço `Transfer` também
pode ser confirmado pela ficha quando a cópia runtime ainda não foi
sincronizada.

A lista de candidatos agora informa:

- o sentido da transferência;
- a outra unidade ou construção;
- o hex avaliado;
- o pouso exigido e a camada resultante;
- o motivo específico de uma recusa por alcance, camada, estoque ou
  capacidade.

O limite de pacientes de serviços de campo não bloqueia mais a circulação de
estoque. Para `Transfer`, `maxUnitsServedPerTurn=0` significa ausência desse
limite de atendimento, não proibição de trocar carga.

## Contrato transacional preservado

Toda descoberta continua sendo uma consulta pura. Procurar vizinhos, calcular
distância, simular o pouso ou comparar estoques não move unidades e não altera
recursos.

Na ação real, o pouso e a transferência só começam depois da confirmação. Antes
de qualquer estoque mudar, o jogo verifica novamente se a aeronave ainda pode
pousar na camada planejada. Se o contexto mudou, a operação é cancelada.

Não há atualização antecipada de ocupação confirmada, FOW, detecção, recursos
ou estado de ação durante a análise.

## Guia de entrada ampliado

O snapshot também incorpora uma nova sequência de leitura para quem está
chegando ao jogo.

O capítulo de boas-vindas apresenta a Sala de Mapas, a vitória por território,
a ausência de sorte e o princípio de que nada é definitivo antes da
confirmação. O primeiro guia agora alerta que o jogador comanda o exército
inteiro antes de passar a vez e explica a necessidade de liberar a saída da
fábrica.

Dois novos capítulos avançam a conversa:

- **Quem bate em quem** introduz desgaste, classes de combate, tiro indireto,
  terreno, combustível, munição e sustentação;
- **Olhando além do hexágono** apresenta FOW, observadores, transporte,
  embarque e desembarque, risco dos passageiros e as camadas de ar, superfície
  e profundezas.

O objetivo é ensinar pelas decisões que aparecem durante uma partida, sem
transformar a entrada do jogador em um catálogo técnico.

## Validação

- build do runtime com zero erros;
- build do Editor com zero erros;
- conferência da matriz Hub/Receiver;
- conferência do alcance direcional pela ficha de quem cede;
- conferência da descoberta de unidades no Scene Editor;
- conferência do pouso planejado pelo `PodePousar`;
- revalidação do pouso antes da execução;
- `git diff --check`;
- preservação dos arquivos `.meta` existentes.
