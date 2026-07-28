# v4.9.0 — Ataque Aéreo e Interceptadores

## Visão geral

Esta versão prepara o próximo ciclo da IA aérea sem transformar aeronaves em
transportadores improvisados. O foco é deixar clara a pergunta que antecede
qualquer resgate: uma aeronave realmente quer uma vaga em plataforma, ou ainda
tem uma missão viável para cumprir?

O resultado é uma consulta própria para caças, aeronaves de ataque e futuras
variações aéreas. Ela conversa com as regras já existentes de pouso, slots,
skills e camadas, sem criar atalhos para porta-aviões, fragatas ou pistas.

## Quero Carona Aérea

Foi incluída a ferramenta **Tools > Operações Aéreas > Quero Carona Aérea**.
Ela mostra a estimativa de necessidade de uma aeronave antes de qualquer ordem
ser criada:

- aeronave em recuperação ou com necessidade emergencial pede plataforma
  compatível antes de continuar a missão;
- fora da emergência, a plataforma só é desejável quando ajuda de fato a rota
  da missão escolhida;
- a validação de plataforma usa as regras reais de pouso e embarque: slot,
  classe, skill, camada, capacidade e exclusividade;
- a ferramenta é uma consulta pura: não pousa, embarca, move, gasta
  combustível nem reserva vaga.

Isso dá ao Porta-Aviões e à Fragata uma resposta útil do possível passageiro,
sem fazer o próprio caça escolher ou comandar o transportador.

## Recuperação aérea e plataformas

O ciclo de recuperação continua vindo antes da decisão de combate. Aeronaves
críticas podem procurar supridor, plataforma naval compatível, construção de
reparo ou local de pouso válido, sempre pelos sensores oficiais. A regra evita
que uma unidade sem autonomia fique presa em uma busca de alvo enquanto ainda
precisa sobreviver até o próximo upkeep.

Os testes de cenário também consolidaram o papel híbrido da Fragata: ela pode
operar como combatente naval e, ao mesmo tempo, receber um Apache no helipad
quando o slot e a skill declarados na ficha permitirem. Não há uma exceção de
carrier escondida no controller.

## Transporte respeita a resposta do passageiro

O resultado “não quero carona” deixou de ser uma ordem fraca de pickup. Ele
permanece útil para diagnóstico e ranking, mas não faz um transportador vazio
esperar ao lado de uma unidade que decidiu continuar a própria rota.

Depois de procurar pedidos reais em Tactical, Operational e Strategic:

- transportadores normais regressam na direção de produção ou HQ;
- rebeldes esgotam a busca distante e aguardam uma oportunidade nova, sem
  inventar uma coleta local;
- um candidato Strategic inseguro não encerra a onda: a busca tenta o próximo
  encontro seguro e materializável.

Essa mudança também melhora os futuros carriers aéreos: a plataforma responde
ao pedido explícito da aeronave, não apenas à proximidade física.

## Estoque e cenários de teste

Os gatilhos de reposição das unidades voltaram a ser guiados pelos controles da
ficha. Galões, caixas de munição e peças só geram demanda quando atingem o
percentual configurado — ou quando a ficha declara reação a estoque vazio.
Assim, uma unidade parcialmente carregada não abandona sua operação apenas por
uma reposição preventiva que ela não pediu.

O cenário de desenvolvimento e as fichas das unidades navais, terrestres e de
logística receberam os ajustes de teste correspondentes para exercitar
plataformas, estoques e encontros de transporte reais.

## Contrato transacional preservado

Todas as novas decisões são consultas de IA e batches normais. Nenhuma delas
altera combustível, pouso, embarque, FOW, ocupação ou estado da unidade antes
do compromisso oficial da ação.

## Validação

- build do runtime e do Editor sem erros;
- consulta de carona aérea disponível no menu de Operações Aéreas;
- plataformas avaliadas pelas regras oficiais de `PodePousar` e
  `PodeEmbarcar`;
- pickup recusado não usado como fallback ativo;
- busca Strategic continua após rejeitar um destino inseguro.
