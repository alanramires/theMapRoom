# v4.8.1 — Refactor da AI Logistica e de Estoque 1/5

## Visão geral

Esta versão entrega a primeira fundação executável do novo ciclo logístico.
Até aqui, uma unidade com reservas baixas conhecia alguns lugares fixos onde
poderia recarregar, mas essa procura ainda estava presa ao controller e não
enxergava a cadeia como uma rede.

Agora existe uma consulta comum capaz de olhar para a unidade que precisa tomar
a decisão e perguntar:

- quanto estoque ainda existe;
- qual reserva está faltando;
- se a situação é preventiva, operacional ou crítica;
- quem pode entregar uma carga compatível;
- em qual hex o encontro pode acontecer;
- se esse encontro está em alcance Tactical, Operational ou Strategic.

O resultado ainda é uma recomendação, não uma ordem. Essa separação permite
evoluir a inteligência da IA nas próximas quatro partes sem transformar as
ferramentas de análise em controllers paralelos.

## Nasce o Melhor Estoque

A nova ferramenta **Tools > Logistica > Melhor Estoque** passa a ocupar, na
logística, o mesmo lugar que Melhor LZ de Embarque e Melhor LZ de Desembarque
ocupam no transporte.

A varredura sempre nasce na unidade selecionada. Um Receiver vazio procura
Hubs capazes de carregá-lo; um Hub carregado encontra Receivers e construções
que precisam de carga; um Hub vazio procura outra fonte; e unidades híbridas
podem revelar mais de um fluxo válido no mesmo contexto.

Cada possibilidade apresenta:

- origem e destino da carga;
- hex de encontro;
- sentido da transferência;
- custo de rota ou direção estratégica;
- distância cúbica;
- urgência do estoque atendido;
- tipos e quantidades de suprimento compatíveis;
- quantidade estimada;
- nota final e motivo da classificação.

Também é possível isolar uma intenção específica, como reabastecer a própria
unidade, atender um Receiver, equilibrar Hubs, abastecer uma construção,
coletar de uma construção ou distribuir carga aos embarcados.

## Tactical, Operational e Strategic

O Melhor Estoque consome o mesmo coordenador de alcance usado pelas decisões
mais recentes da IA.

**Tactical** representa o que a unidade pode alcançar agora. **Operational**
amplia a procura pelo número configurado de rodadas. **Strategic** não inventa
uma rota global: ele aponta o melhor sentido distante pela distância cúbica,
para que o controller possa escolher como progredir.

A janela permite retirar Strategic da decisão e ainda desenhar uma
**direção provável**. Assim, é possível inspecionar para onde a rede aponta sem
pagar o custo dessa opção no ranking operacional.

## A necessidade passa a ter linguagem comum

O novo avaliador de estoque compara as reservas reais da instância com a
capacidade declarada no `UnitData`.

A leitura deixa de ser apenas “tem ou não tem”:

- **Preventive**: ainda existe margem, mas a reserva já merece reposição;
- **Operational**: a falta começa a comprometer a permanência em campanha;
- **Critical**: a unidade está vazia ou próxima disso;
- **None**: as capacidades configuradas estão completas.

Quando a partida ainda não está rodando, a ferramenta consegue emular no
Scene Editor o estado inicial descrito pela ficha. Isso mantém o diagnóstico
útil antes do primeiro turno e respeita a diferença entre unidades que nascem
carregadas e unidades que precisam entrar na cadeia para receber carga.

## PodeTransferir continua sendo a autoridade

O Melhor Estoque não duplicou as regras finais de transferência.

Para cada encontro candidato, ele pergunta ao próprio `PodeTransferir` se a
operação seria válida naquele hex. Tier, domínio, camada, alcance de coleta,
construções, Hubs, Receivers, unidades embarcadas, pouso necessário e
compatibilidade de suprimentos continuam sob a mesma autoridade usada pelo
jogo.

O `PodeTransferir` ganhou uma entrada prospectiva: agora consegue responder
“e se esta unidade estivesse naquele hex?” sem alterar a posição real da
unidade.

Essa mudança remove do restock uma simulação antiga que deslocava
temporariamente o `CurrentCellPosition` e depois tentava restaurá-lo. O
resultado da consulta continua o mesmo, mas a análise deixa de tocar na verdade
confirmada do tabuleiro.

## Contrato transacional preservado

Calcular uma necessidade ou comparar encontros não:

- move unidades;
- transfere recursos;
- altera ocupação;
- consome movimento ou autonomia;
- atualiza FOW, detecção ou memória da IA;
- marca ação como realizada.

Mesmo a avaliação de um pouso necessário usa o hex prospectivo como parâmetro,
sem reposicionar a aeronave.

O controller existente ainda não consome o ranking completo do Melhor Estoque.
Esta primeira parte entrega a fundação, a ferramenta de campo e a consulta
prospectiva segura. A adoção das decisões pelo controller pertence às próximas
partes do refactor.

## Manual consolidado

O snapshot também incorpora a revisão corrente do manual do jogo.

A documentação passa a explicar:

- por que a ponte sobre o mar separa convés e água, enquanto a ponte sobre a
  praia continua sendo uma única superfície;
- como a estrada na montanha funciona como desfiladeiro e exige entrada pela
  rota declarada;
- por que supridores recém-comprados nascem vazios por padrão e quais fichas
  deverão declarar a exceção;
- o estado de implementação e auditoria dessas regras.

Essas páginas registram em linguagem de jogo as decisões já tomadas para
ocupação, movimento e nascimento da cadeia logística.

## Validação

- build do runtime com zero erros;
- build do Editor com zero erros;
- conferência da janela Melhor Estoque e de seu menu;
- conferência das ondas Tactical, Operational e Strategic;
- conferência da estimativa por tipo de suprimento;
- `git diff --check`;
- preservação dos arquivos `.meta`;
- preservação das alterações de manual presentes no snapshot.
