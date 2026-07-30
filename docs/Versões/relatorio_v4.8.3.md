# v4.8.3 — Refactor da AI Logística e de Estoque 3/5

## Visão geral

Esta etapa faz a rede de estoque deixar de ser apenas uma reação ao vazio. A
IA passa a enxergar quem precisa receber carga antes de ficar sem nada, evita
trocas circulares entre Hubs e permite que unidades de Logística também façam
o papel de rede quando não há um atendimento de campo mais urgente.

O resultado é mais natural: navios, porta-aviões, caminhões e outros Hubs não
ficam esperando uma pane completa para se reorganizar. Eles mantêm reserva para
continuar prestando os serviços para os quais existem.

## Estoque vira uma necessidade mensurável

Construções agora possuem um limite preventivo configurável para cada estoque,
com valor inicial de 25%. Abaixo desse ponto, a rede pode tratar a construção
como receptora mesmo antes de ela zerar.

A leitura de necessidade também foi consolidada para unidades, fontes móveis e
construções. Ela distingue reserva preventiva, situação operacional e estoque
crítico, levando em conta o tipo de carga que realmente pode ser transferido.

Isso evita dois comportamentos ruins:

- uma cidade receber carga infinita só porque não possui teto máximo;
- dois Hubs parcialmente abastecidos ficarem devolvendo os mesmos recursos um
  ao outro.

Para rebalancear, a fonte precisa possuir uma diferença material de reserva em
relação ao destino. E, se não houver quantidade útil para mover, a opção nem
entra na lista.

## Logística também sustenta a rede

O papel Logística continua priorizando seu motivo principal: atendimento de
campo — reabastecer, reparar ou rearmar alguém que precisa do serviço.

Quando não há cliente válido, unidades que também são Hubs podem circular
estoque pela mesma consulta usada pelo papel Estoque. Assim, um porta-aviões
sem galões pode procurar recarga antes de perder a capacidade de atender seus
caças, enquanto um navio-tanque ou caminhão ainda pode manter os nós próximos
abastecidos.

A escolha usa Tactical e Operational:

- em Tactical, o encontro e a transferência podem ocorrer na rodada;
- em Operational, a unidade avança para o rendezvous e reavalia a rede depois;
- a progressão mantém a preferência por caminho seguro quando o papel está
  procurando recarga.

## Atendimento aéreo e plataformas corretas

O fluxo de recuperação aérea passou a usar as mesmas regras de pouso que estão
na ficha e nas ferramentas. Uma plataforma só é considerada se seu slot aceitar
de fato a aeronave: classe, camada, skills exigidas e vaga disponível.

Também foi corrigida a compatibilidade de atendimento após pouso. Uma aeronave
que pousa numa praia fica em `Naval / Surface`; portanto um supridor terrestre
não pode atendê-la como se ainda estivesse na mesma camada. A operação agora
exige que o supridor suporte a camada real em que o cliente terminou.

## Melhor Local para Pouso

Foi adicionada a ferramenta:

`Tools > Operações Aéreas > Melhor Local para Pouso`

Ela organiza LZs autorizadas pelo `PodePousar` nas ondas Tactical e
Operational, mostra a camada final e o contexto que venceu a hierarquia
construção → estrutura + terreno → terreno.

A busca é intencionalmente econômica:

- descarta bandas `Air` e domínios que a aeronave não suporta em sua ficha;
- ignora LZs cuja superfície já esteja ocupada;
- respeita a autonomia atual: Tactical usa o menor entre movimento restante e
  combustível, e Operational nunca projeta além do combustível disponível.

Ela continua sendo uma consulta pura. Não move a aeronave, não pousa, não muda
ocupação e não revela informação do tabuleiro.

## Contrato transacional preservado

Todas as novas decisões são prospectivas. Consultar uma fonte de estoque, uma
plataforma de pouso ou uma LZ não altera combustível, posição, ocupação, FOW ou
qualquer estado confirmado. Movimento, pouso, suprimento e transferência só
acontecem pelo batch e pelos fluxos oficiais de compromisso.

## Validação

- build do runtime e do Editor sem erros;
- teste de transferência entre unidades logísticas compatíveis;
- teste de recarga crítica de Fragata por Navio-Tanque;
- teste de porta-aviões sem galões priorizando a recarga;
- teste de recuperação aérea com fornecedor Tactical e plataforma Operational;
- conferência de pouso em praia usando `Naval / Surface`;
- conferência de LZ ocupada e de limite por autonomia;
- `git diff --check` executado; os avisos remanescentes são espaços finais em
  YAML serializado pelo Unity, sobretudo na cena de desenvolvimento, sem erro
  de compilação ou alteração de gameplay associada;
- arquivos `.meta` novos preservados junto das ferramentas e serviços.
