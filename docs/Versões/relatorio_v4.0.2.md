# v4.0.2 - AI Jogadas, Pressão e Analises

Esta versão transforma o histórico da partida e a composição inimiga em sinais concretos para planejamento e compras. O shopping deixa de reagir apenas ao papel abstrato pedido pelo plano e passa a escolher, dentro desse papel, a unidade cuja arma e preferência de alvo melhor respondem à ameaça observada ou lembrada. A versão também consolida o master plan por eixos, rallys, âncoras e transporte.

## Counter pressure

- Novo analisador de pressão por quatro famílias de armas:
  - anti-infantaria contra Infantaria;
  - anti-tank contra Veículos, Blindados e Artilharia;
  - anti-aérea contra Jatos, Helicópteros e Aviões;
  - anti-navio contra Navios e Submarinos.
- Classificação dinâmica das unidades ofertadas pelas categorias de suas armas embarcadas e por `aiTargetPreferenceByClass`.
- Nenhuma tabela por nome de unidade: Bazooka, Obus Médio, Tanque A, ASTROS e demais candidatos são avaliados pelo conteúdo do `UnitData`.
- Pressão inimiga ponderada por quantidade, classe, elite, custo, HP atual e impacto recente em combate.
- Correção da família de `Vehicle`, que deixou de inflar pressão de infantaria e passou para anti-tank.
- O papel continua sendo definido pela demanda (`Assault`, `FireSupport` etc.); o counter pressure escolhe qual unidade satisfaz melhor esse papel.
- Déficit real de cobertura anti-tank pode abrir demanda adicional de FireSupport, sem comprar fora da lógica geral do plano.

## Memória do campo de batalha

- A pressão combina unidades atualmente visíveis com memória recente do `JogadasManager`.
- Memória rastreada por UID e sigla, com decaimento por turno.
- Unidades novamente visíveis não são contadas duas vezes.
- Unidades destruídas deixam de participar da composição lembrada.
- Classe, custo e elite são recuperados do `UnitData` associado à sigla registrada.
- Uma ameaça que saiu da visão continua relevante durante a janela de intel, especialmente quando causou dano ou baixas.

## Resultado estruturado de combate

- Eventos `Ataque` agora registram:
  - UID, sigla e time de atacante e defensor;
  - HP antes e depois de ambos;
  - dano causado;
  - baixa direta por ataque ou contra-ataque.
- Exemplo:

  `AC#132 10→10 vs TB#88 10→0`

- A IA passou a registrar a jogada depois da execução do batch, alinhando o fluxo da IA ao fluxo humano e garantindo estado pós-combate correto.
- Saves antigos permanecem compatíveis por meio do marcador `hasCombatResult`.

## Carga embarcada e perdas em cascata

- O combate captura recursivamente toda a árvore de carga antes da resolução.
- Dano propagado e morte por destruição do transporte são registrados com:
  - transporte raiz;
  - pai direto;
  - profundidade;
  - UID, sigla, time e classe;
  - HP antes/depois;
  - custo e elite;
  - causa da perda.
- Suporta transporte dentro de transporte, por exemplo:

  `AC#132 10→10 vs NT#88 10→0 [>APC#15 10→0; >>SD#1 10→0; >APC#16 10→0; >>BZ#2 10→0]`

- O agressor recebe crédito por dano e baixas indiretas.
- O valor estratégico da carga considera custo e elite: afundar um transporte cheio de unidades elite pesa muito mais que destruir um transporte vazio.
- O intel registra dano, baixas e valor econômico de carga destruída para orientar compras futuras.

## Shopping Pressure

- Nova seção **Counter pressure** em `Tools > Utils > Shopping Pressure`.
- Exibe:
  - score anti-infantaria, anti-tank, anti-aérea e anti-navio;
  - classe, quantidade e score inimigo;
  - separação entre unidades visíveis e lembradas;
  - melhores counters disponíveis que também atendem à fila atual.
- A janela também ganhou visão macro da IA, agrupamento dos objetivos por invasão/rally/defesa/captura e inspeção de base guards/âncoras.

## JogadasManager e exportação

- Inspector do `JogadasManager` mostra o resultado completo do combate.
- CSV ganhou colunas estruturadas para atacante, alvo, HP e carga.
- Exportação textual preserva a hierarquia da carga por profundidade.
- `AIIntelReport` passou a expor dano inimigo, baixas e valor de carga destruída.

## Plano, eixos e rally

- Materialização runtime do `InvasionAxisMap`.
- Objetivos e unidades passam a herdar o eixo estratégico do setor.
- Novos ícones e informações de eixo no HUD.
- Contexto macro territorial ampliado para classificar controle e força relativa.
- Rally de invasão ganhou critérios de preparação, readiness, go-green e progressão de força.
- Ajustes em âncoras, reserva de capturadores, objetivos defensivos e invasão do HQ.

## Transporte e captura

- Melhorias no vínculo entre transportador, capturador e objetivo atribuído.
- Transporte evita carregar passageiros incompatíveis com o plano e melhora pickup, rendezvous e desembarque.
- Capturador agressivo satisfaz corretamente slots de Capturador via `CanSatisfy`, evitando rogue falso e demanda inflada.
- Regras adicionais para embarque, caminhos, segurança e retomada de missão após transporte.

## Defesa, compras e economia

- Shopping reage ao estado macro quando o time está perdendo território.
- Concentração de gasto quando há poucos slots produtivos disponíveis.
- Melhor progressão e reserva para elites.
- Priorização de logística cresce com o volume de unidades em reparo.
- Defesa pode ocupar prédios produtivos vulneráveis para negar captura oportunista.
- Ajustes de assalto, suporte de fogo, ruptura de HQ e pressão anti-infantaria/anti-blindado.

## Conteúdo e ferramentas

- Atualizações nos mapas Ground e Air.
- Ajustes no prefab de unidade, HUD, fontes e ícones de eixo.
- Novos documentos de arquitetura para eixo de captura e transporte por eixo.
- Ferramentas adicionais de inspeção no `SectorManagerEditor` e `DebugManager`.

## Validação

- `dotnet build Assembly-CSharp.csproj --no-restore`
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`
- Resultado: 0 erros.
- Permanecem apenas avisos obsoletos já existentes nas APIs Unity.
