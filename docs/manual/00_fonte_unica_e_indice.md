# The Map Room — Fonte Única e Índice

*Porta de entrada da documentação canônica. Este arquivo declara o que é regra, quem manda quando dois documentos discordam, e onde cada assunto mora.*

## Versão

Biblioteca derivada do **Manual Técnico versão 9**, congelado em `The Map Room Manual tecnico (9) — snapshot.md`.

O snapshot é imutável. Ele existe como referência histórica e como base de comparação — não deve ser editado nem consultado como regra atual. A partir da versão 9, a autoridade passou a ser esta biblioteca.

## Ordem de precedência

Quando duas fontes discordarem sobre uma regra, vale a primeira desta lista:

1. **Documentos canônicos numerados** (`01` a `10`) — a doutrina do jogo.
2. **Catálogos e matrizes** (`11` a `15`) — os valores, quando preenchidos.
3. **Pendências técnicas** (`90`) — divergências conhecidas entre doutrina e implementação.
4. **Decisões de design** (`91`) — por que uma regra existe.
5. **Snapshot da versão 9 e relatórios antigos** — histórico, nunca autoridade.
6. **Conversas e anotações avulsas** — não são fonte.

Duas consequências que essa ordem resolve na prática. Encontrar um comportamento estranho no jogo **não** o torna regra: se ele contraria um documento canônico, é pendência técnica até que alguém decida o contrário. E um documento antigo achado numa pasta qualquer nunca revoga um canônico, por mais detalhado que pareça.

## Catálogo incompleto não é autoridade

Os catálogos `11` a `15` estão em esqueleto. Campo ausente **não** significa "sem valor" — significa "ainda não registrado". Na falta de ficha, a autoridade é o asset do jogo, e a lacuna deve ser tratada como trabalho pendente, nunca como declaração.

## Regra contra duplicação

Cada regra tem **um único endereço canônico**. Os outros documentos que precisarem dela usam uma frase curta e uma referência, nunca uma cópia:

> O recálculo da névoa ocorre após a confirmação da ação, conforme `04_ciclo_de_acao_e_comprometimento.md`.

Repetição pedagógica é bem-vinda num futuro Manual do Jogador, onde a prioridade é ensinar. Aqui a prioridade é localizar e alterar a regra uma vez só.

## Os documentos

**Regras canônicas**

| Arquivo | Responde |
|---|---|
| `01_principios_e_vocabulario.md` | Que tipo de jogo é este e qual vocabulário ele usa |
| `02_dominios_terrenos_e_ocupacao.md` | Onde cada coisa existe e quem ocupa ou atravessa cada setor |
| `03_movimento_terreno_e_infraestrutura.md` | Como uma unidade percorre o mapa |
| `04_ciclo_de_acao_e_comprometimento.md` | Quando uma intenção vira alteração real no tabuleiro |
| `05_visao_deteccao_e_nevoa.md` | O que o jogador sabe e por que sabe |
| `06_combate.md` | Como uma troca de fogo produz baixas |
| `07_logistica_e_servicos.md` | Como uma força continua operacional |
| `08_transporte_fusao_e_operacoes_aereas.md` | Como unidades viajam dentro de outras ou se reorganizam |
| `09_captura_economia_e_progressao.md` | Como o mapa se transforma em economia e tecnologia |
| `10_turnos_jornal_e_vitoria.md` | Como a partida avança e como termina |

**Catálogos** — `11_catalogo_de_construcoes.md`, `12_catalogo_de_unidades.md`, `13_catalogo_de_armas.md`, `14_matriz_rps_e_elite.md`, `15_matriz_de_sensores.md`.

**Engenharia** — `90_pendencias_tecnicas.md` (dívida), `91_decisoes_de_design.md` (por quê), `92_auditoria.md` (o que já foi verificado contra o código).

## Ordem de leitura

Os canônicos foram escritos numa cadeia de dependência, e ela é a promessa do índice: nenhum conceito é usado antes de ser definido.

Princípios → domínios e ocupação → movimento → ciclo de ação → visão e névoa → combate → logística → transporte → captura e economia → turno e vitória.

Os catálogos sustentam todos eles, mas não são necessários para entender o sistema.

## Glossário mínimo

**Domínio** — a camada onde algo existe: Air/High, Air/Low, Land/Surface, Naval/Surface, Submarine/Submerged.

**Andar** — divisão de ocupação do hexágono. São três: ar, superfície, profundezas. Air/Low e Air/High compartilham o andar aéreo.

**Posição** — a qualidade tática do lugar onde a unidade está, em cinco níveis de Desfavorável a Único.

**DPQ** — a diferença de posição entre atacante e defensor, sempre calculada do ataque para a defesa.

**Etiqueta (habilidade)** — rótulo consultado pelo mundo. Não faz nada sozinha; quem lhe dá sentido é o terreno, a estrutura ou o sensor que pergunta por ela.

**Esquadrão** — o que o indicador de HP mostra: número de membros vivos no token, não pontos de vida.

**Turno** — sempre o turno do **proprietário** da unidade. Nenhuma duração deste manual é contada em turnos globais nem em passagens completas por todos os times.

## Convenção de Números

Todo valor citado neste manual pertence a uma de três categorias, e saber qual muda o que você pode concluir dele:

**Atributo de construção ou unidade.** Vive na ficha daquele elemento e pode ser diferente em outro cenário. É a categoria mais comum — renda, visão, alcance, percentuais de serviço, capacidade de atendimento, pontos de captura.

**Constante do sistema.** Vale para o jogo inteiro e não é configurável por cenário. São poucas: os pisos de 1 no cálculo de combate, a escala de qualidade de posição e o bônus de defesa que dela deriva, o teto de 10 do esquadrão.

**Ajuste de cenário.** Um mapa pode sobrescrever a ficha de um prédio específico, ponto a ponto. Hoje nenhum cenário usa isso, mas a porta existe — e é por ela que valores "oficiais" podem legitimamente divergir num mapa customizado.

Quando este manual dá um número sem qualificar, ele é atributo de ficha. As duas outras categorias são sempre ditas com todas as letras.
