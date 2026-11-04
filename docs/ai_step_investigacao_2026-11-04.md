# Investigacao das marcas do AI Step — 2026-11-04

Fonte: `ai_step_marcas.jsonl`, tres marcas lidas sem recalcular a IA, executar
jogadas ou carregar saves. As conclusoes abaixo separam fatos gravados de
avaliacao de design.

## Passo 10, turno 3, slot 0 — por que Tanque B?

Marca: `2f96911d2aae4f589a954a44b6242aa5`.
Nota: por que comprar Tanque B diante de soldados, em vez de Lanca-Foguetes/APC?

**Classificacao: decisao discutivel de doutrina; bug estrategico nao demonstrado.**

O shopping tinha 12000 ao planejar. O carrinho escolhido foi Soldado (1000),
Soldado (1000), Tanque Leve (6000), total 8000. A foto anterior a esta ultima
compra tinha 10000, pois os dois soldados ja haviam sido comprados.

A fila gravada explica a escolha:

| Demanda | Quantidade | Prioridade | Origem |
|---|---:|---:|---|
| Assalto | 4 | 14 | operacoes de captura + composicao |
| Capturador | 12 | 15 | operacoes + vagas do plano + composicao |
| FogoIndireto | 2 | 16 | defesa preventiva + composicao |

A composicao registrou `pacote 2/2/1 cap=6 ass=0 art=0`. Portanto existe pressao
para preencher Assalto, reforcada pelas operacoes; nao e simplesmente comprar
uma peca para desbloquear elite. O roster nao tinha elite ofertado, e o proprio
log cancelou compromisso elite por falta de oferta.

Tanque Leve foi comprado para Assalto com score 136000. Lanca-Foguetes custa
6200, apenas 200 a mais: dois soldados + foguetes somariam 8200 e caberiam ate
no orcamento livre de 9000 apos o imposto de conscricao de 3000. Logo nao se
pode atribuir sua ausencia a falta de caixa. Ele atenderia outro papel, com
prioridade menor na fila. Nao havia demanda Counter/Infantry nem Transportador
nessa fila; APC nao entra como substituto direto do papel Assalto.

O comparador de carrinhos considera cobertura/prioridade antes do score de
qualidade. A escolha e coerente com essa fila, mas nao prova que o tanque fosse
a melhor resposta aos soldados. Nao temos os scores dos carrinhos concorrentes
no registro para sustentar essa conclusao.

Save de referencia gravado: slot 2, turno 3, slot ativo 0, duas acoes de replay
antes do passo; hash `a57e4e31025c8f52681078072c17f91f2de5263dba600f3a28ed20bc8584c1a8`.
O arquivo nao foi carregado nem alterado nesta investigacao.

## Passo 4, turno 2, slot 1 — por que Metranca?

Marca: `ab9693cf0eee4c878e5b42e277d3862d`.
**Classificacao: decisao coerente com a demanda registrada.**

A fila tinha Counter/Infantry com prioridade 16, origem counter-pressure:
pressao bruta 2,4, cobertura 1,2, saldo 1,2, seis inimigos visiveis. Metranca
foi comprada para essa demanda, score 202210, depois de dois soldados. Portanto
nao foi selecionada como capturador caro nem por uma exigencia do core.
Isso explica a intencao; nao e uma prova de superioridade sobre cada alternativa.

## Passo 6, turno 3, slot 0 — movimento aleatorio?

Marca: `9489aec256884d2fa949ff3101fb4c4c`.
**Classificacao: regra de blitzkrieg/revezamento; nao foi movimento aleatorio.**

O motivo gravado diz `PontaLanca: 1 hard handoff Alpha -> Echo via (7,11)`.
O plano anterior identifica a unidade 1 como a que cede a captura de Alpha e
mantem a unidade 7 atribuida a Alpha. A rota gravada e
(5,13) -> (6,13) -> (7,12) -> (7,11). A intencao e avancar e deixar a captura
para o substituto. O registro original nao explicou essa passagem em linguagem
clara; a acao do substituto depois disso nao foi comprovada nesta investigacao.

## Bug confirmado na observabilidade do shopping

Nas tres marcas, `candidatesObserved=0` e so existe o candidato sintetico final.
O shopping por papeis desta partida usa `BuildBestRoleShoppingCart` em
`AIShoppingPlanner.Demand.cs`. A instrumentacao de alternativas instalada no
`UnitPicker.cs` observa outro caminho e nao cobre esse beam/cart.

Portanto a ferramenta ainda nao mostra as alternativas reais dessas compras.
Para corrigir: instrumentar os gates, candidatos e comparacoes do carrinho em
uso, preservando os contextos de produtor, demanda, orcamento e estado do beam;
nao comparar scores de contextos diferentes nem rodar o shopping de novo.

Nenhuma regra de compra foi alterada. Antes de trocar a doutrina, a decisao do
autor a esclarecer e quanto a demanda tatica anti-infantaria deve poder furar
a prioridade de Assalto/composicao neste tipo de mapa.
