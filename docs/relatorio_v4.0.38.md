# v4.0.38 — AI conscription - extra dificulty

Data: 17/07/2026

## Visão geral

Esta versão amplia a seleção de dificuldade da IA para quatro perfis claros. O antigo nível difícil foi desdobrado em **Competitivo**, que preserva o pacote Hard e usa recrutamento forçado apenas quando está perdendo, e **Agressivo**, que mantém pressão de produção em todos os turnos por meio da nova Doutrina do Enxame.

## Matriz de dificuldade

| Modo | Income | Banned Units | Pacote Hard | Conscription |
|---|---|---|---|---|
| **Fácil** | 1/3 (só de construções que **não** são cidades — cidade paga cheio) | não | não | não |
| **Normal** | normal | não | não | não |
| **Competitivo** | normal | **sim** | **sim** | só perdendo |
| **Agressivo** | normal | **sim** | **sim** | sempre |

### Fácil

- A IA recebe apenas um terço da renda gerada por construções que não sejam cidades.
- Cidades continuam pagando a renda integral, preservando o valor da conquista territorial.
- Não há unidades banidas, pacote Hard nem conscrição.
- O planejamento e o shopping seguem o comportamento padrão com uma economia mais tolerante para o jogador.

### Normal

- Economia e catálogo de unidades funcionam sem modificadores.
- Não ativa regras do pacote Hard.
- Não usa recrutamento forçado; as compras são guiadas pelas demandas normais do planejador.

### Competitivo

- Mantém renda normal e ativa as unidades marcadas como banidas no Hard, impedindo que a IA as compre.
- Ativa o pacote Hard: maior pressão de captura, limites logísticos, prioridades e projeções estratégicas próprias desse perfil.
- A conscrição é emergencial: quando a avaliação macro classifica a IA como perdendo, o recrutamento forçado ocupa produtores disponíveis com massa terrestre barata.
- Fora da condição de derrota, preserva o shopping por demanda e a formação de reservas para unidades de elite.

### Agressivo

- Inclui toda a configuração do Competitivo, com renda normal, banimentos e pacote Hard.
- Ativa permanentemente a **Doutrina do Enxame**: todo produtor elegível do Exército deve tentar produzir uma unidade terrestre barata em cada turno.
- Antes de fechar o carrinho de demandas, o shopping calcula um imposto de conscrição e reserva o custo da massa garantida. Unidades de elite só são compradas quando cabem acima dessa reserva.
- Aeroportos e portos sem ofertas do Exército não são transformados em fábricas de massa; continuam poupando para demandas aéreas, navais e compromissos de elite.
- Sob ameaça direta a uma base, o preenchimento defensivo continua tendo prioridade sobre a composição do enxame.

## Seleção e persistência

- A tela de nova partida agora oferece **Fácil**, **Normal**, **Competitivo** e **Agressivo**.
- A escolha é aplicada como uma combinação explícita dos estados Easy, Hard e Conscription Doctrine.
- Os três estados são gravados no save e restaurados no load, evitando que uma partida carregada retorne aos defaults configurados na cena.
- O último Jornal do Comandante de cada equipe também passa a integrar o save, permitindo consultar novamente o resumo após carregar uma partida.

## Logs de suporte

- Os avisos de oferta de suprimento (`SupplyAlert`) e as mensagens de decolagem após Serviço do Comando passam a obedecer ao log mestre de `PodeSuprir` no `TurnStateManager`.
- Isso reduz ruído no Console durante partidas normais sem remover as informações necessárias para diagnóstico.

## Impacto esperado

- **Competitivo** oferece o desafio Hard anterior, mas só abandona a economia de elite em favor de massa quando a situação estratégica exige.
- **Agressivo** mantém produção e pressão territorial constantes, reduzindo janelas de descanso para o jogador.
- A distinção entre os modos fica visível tanto na economia quanto na composição das forças, sem conceder renda artificial aos níveis mais altos.

## Validação

- Build de `Assembly-CSharp.csproj`: **0 erros** (avisos preexistentes do projeto).
- Conferência da matriz de dificuldade contra os flags aplicados pelo `AIController`.
- Verificação do fluxo de conscrição emergencial e da Doutrina do Enxame no shopping da IA.
- Verificação da persistência de dificuldade e do Jornal do Comandante no save/load.
- `git diff --check` executado sem erros.
