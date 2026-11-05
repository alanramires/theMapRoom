# v9.2.2 — o primeiro estranho jogou

**Um playtest de verdade virou uma tela de opções, um atalho de toque e um
inventário do que ainda vaza na névoa.** Nada daqui foi visto em Play pelo autor
no momento do fechamento, exceto o que está marcado: tudo **compilou**
(`bash tools/compilar.sh`), e o resto é leitura de código.

O fio do dia é uma frase do testador, repassada por WhatsApp:

> *"O jogo em si leva muito tempo. É lento."*

João (marido da Poliana de Alencar) zerou o primeiro mapa no Easy, pelo navegador
do celular, numa tarde inteira (o mapa foi projetado para ~1 h). Gostou e achou o
jogo *"nichado, mas pode agradar o público fiel exatamente por isso"*. O registro
completo está em [`playtest de 3 de outubro 2026.md`](playtest%20de%203%20de%20outubro%202026.md).

Esta versão também fecha o trabalho commitado depois da tag v9.2.1 (régua de
captura, perfil, modalidade, faixa de entrega, i18n), resumido no fim.

---

## Frente 1 — "lento" é duas coisas, e só uma é identidade

A leitura que organizou o dia:

| | o que é | mexe? |
|---|---|---|
| tempo de **decisão** | o jogador parado, pensando | não — é ritmo de xadrez, é o jogo |
| tempo de **execução** | já decidiu, e ainda gasta toques | sim — é atrito, e no celular parece lentidão |

Tudo o que veio depois ataca a segunda linha sem tocar na primeira.

O testador também sentiu falta de "variação de alcance". O jogo tem alcance
variável (obus 3~4), mas o autor tinha reduzido bazooka e metralhadora de 1~2
para 1 — e nesta versão a metralhadora do **Tanque Z** também foi para 1. O
mapa de estreia ficou quase todo alcance 1. **Não falta mecânica: a variedade
saiu justamente da primeira fase.**

---

## Frente 2 — a tela de Configurações: Partida × Preferências

Duas gavetas com donos diferentes, e é isso que decide onde cada opção mora:

| gaveta | o quê | muda quando | vive onde |
|---|---|---|---|
| **Partida** | cores, dificuldade | antes do start; vai no save | tela pré-partida |
| **Preferências** | Modo Turbo, Ação Direta, Tela Cheia | a qualquer hora, inclusive no meio da fase | `PreferenciasDoJogador` (PlayerPrefs) |

**Por quê:** o testador perdeu a tela cheia *no meio* da fase (bloquear o celular
derruba a tela cheia do navegador) e não tinha onde reativar. Preferência que só
existe antes do start reproduz o mesmo buraco no turno 8.

- `PreferenciasDoJogador` é estático, não vive em cena. Sem escolha gravada vale
  o default da cena. Na Batalha, o `MatchController` e o `AIController` copiam a
  preferência no `Awake`; a Campanha lê direto. **Não** se criou um
  `MatchController` na Campanha para ler uma opção.
- **Tela Cheia não é gravada:** o navegador recusa entrar em tela cheia sem um
  toque do jogador, então "lembrar ligada" nunca funcionaria na Web. O check é
  **espelho** de `Screen.fullScreen` e pede a troca no `PointerDown` (o release do
  Toggle chega tarde para o navegador aceitar como gesto).
- `FullscreenShortcutButton` saiu de dentro de `PanelMoneyController.cs` para
  arquivo próprio: a Unity só lista no *Add Component* uma classe cujo arquivo tem
  o nome dela.
- Nomes de jogador, não de designer: **Modo Turbo** (o próprio testador pediu
  "desesperadamente o modo turbo"; pula a viagem do cursor e o respiro entre
  escolhas, os 5–7 s de processamento continuam) e **Ação Direta** (o antigo
  "atalho contextual").
- `PainelConfiguracoesController` funciona nos dois mundos: na Tela de Entrada o
  teclado é do `MainMenuStateController` (estado `Config`, que já existia sem
  ninguém usar); na Batalha e na Campanha, onde ele não existe, a tela roteia o
  próprio teclado. Prefab `Panel_Config` nas três cenas.
- Batalha: o `Button_config` mudou do `Panel_options` para o `Panel_gerenciar`, e
  o `BattleMapMenuRootController` só procurava no primeiro — por isso não abria.
- O método `OnConfigButtonClicked` do `PanelMenu` **abria o Sobre** (sobra de
  quando "config" era o Sobre). Virou `OnAboutButtonClicked`, e o nome antigo
  passou a fazer o que diz.

**Achado que custou duas rodadas:** "a seta para cima não navega". Navegava — o
Toggle padrão seleciona com (245,245,245) sobre o quadradinho branco. O
`Panel_NewGame` já tinha a solução (`ApplySelectionHighlight`, `#4A5A43`). A
primeira hipótese (o controlador lendo a *ajuda* em vez do rótulo do toggle) era
real e foi corrigida também, mas não era a causa do sintoma.

## Frente 3 — Ação Direta a partir da seleção

Com a unidade selecionada, tocar num alvo faz a unidade **andar sozinha** até uma
célula legal e abrir o prompt já mirando o alvo. **Nunca executa**: para na
confirmação (o segundo toque no mesmo lugar, que o autor implementou), e cancelar
desfaz o movimento provisório. Ataque, embarque, fusão (só se a soma do HP ≤ 10)
e suprimento. Desembarque fica de fora (o toque já é movimento), captura já tinha
o segundo toque, transferir é o "capturar" do supridor.

- **Quem decide é o sensor, da célula projetada**, sem mover ninguém:
  `PodeMirar(fromCell)`, `PodeEmbarcar.CanEmbarkFromProjectedCell`,
  `PodeFundir(fromCell)`, `PodeSuprir.CollectOptionsFromCell`. A IA simula
  movendo a unidade (`SetCurrentCellPosition`), e copiar isso no lado do jogador
  dispararia eventos de ocupação no meio da ação — o modo de falha do prédio
  escuro do CLAUDE.md.
- **Célula de chegada no ataque: o melhor DPQ** (a régua do combate,
  `PositionDpqResolver`), depois onde o cursor estava, depois a mais barata. Foi
  decidido em conversa: a regra "anda o mínimo" era previsível mas fazia o
  vizinho na planície ficar e o de 2 hexes subir o morro — parecia aleatório. Com
  DPQ para todos, o atalho **ensina** ("daqui é melhor pra lutar"), e o helper diz
  o porquê só quando o terreno mudou a resposta. Quem quer atirar de um lugar
  específico toca primeiro na célula.
- Na Campanha, com a Ação Direta ligada, tocar de novo no **mesmo** quadrante com
  a confirmação aberta entra nele — o mesmo gesto da batalha.

## Frente 4 — a névoa: menu, mapa e Jornal leem a mesma memória

A pendência "captura lê o estado atual fora da visão" virou decisão do autor:

```text
mapa: prédio SEU (intel defasada)   realidade: já é do inimigo
antes:   a unidade chega → menu oferece "Capturar" → o menu sabia mais que o mapa
agora:   menu responde pela MEMÓRIA → nada a capturar → confirma → Neutral recalcula
         → o dono real aparece → "Capturar" no turno seguinte
```

- `PodeCapturarSensor` ganhou `respectFogMemory` (OFF por padrão). Ligado só no
  menu, no pedido de captura e no **rótulo** do helper ("Reforçar controle"
  entregaria que o prédio é seu — vazamento lateral achado na implementação).
  Execução e IA seguem pelo estado real.
- **O Jornal conta como informação legítima.** Ao relatar `ConstructionLost`, vira
  pendência na memória do time que perdeu, aplicada no próximo registro
  confirmado do fog **daquele** time — a barreira
  (`IsFogConfirmedMemoryWriteAuthorized`) só deixa escrever no `Neutral` e para o
  time cujo fog está sendo calculado, e a perda acontece no turno de outro. Vai
  no save (`isOwnerReport`).
- A memória guarda dono, **não pontos**: fora da visão nunca aparece "Recuperar".

**Desembarque na névoa parcial: aceito por design.** O autor decidiu que o hex
ocupado sumir das opções num hex explorado é vazamento aceitável (dois
terrestres não ocupam o mesmo hex; o jogador estranha e conclui que há algo
ali). Registrado com a alternativa recusada — a "emboscada" do Advance Wars —
para ninguém "corrigir" sem decisão. O filtro do **preto** (`IsCellKnownForDisembark`,
`MatchController.IsCellVisibleOrExploredForSlot`) entrou nesta versão.

## Frente 5 — consertos transacionais pequenos

Da revisão de [`pendencias do mvp.md`](pendencias%20do%20mvp.md):

- **Trajeto do replay** guardava a mesma lista que `ClearCommittedMovement`
  esvazia antes da promoção do buffer. Agora é cópia.
- **Objetivo de estrada do tutorial** completava no evento de movimento ainda
  cancelável; foi para o poll que exige `HasActed`. O indicador
  `UsedRoadBoostOnLastMove` é devolvido no rollback (ninguém o restaurava).
- **Fallback do rollback** deixava a unidade no destino com o movimento já
  devolvido. Agora volta à origem pelo mesmo caminho do rollback animado.
- **Cache de debug** bumpava a revisão global fora do `Neutral`.
- **i18n:** CANCELAR e MANTER POSIÇÃO do helper gravavam `[helper.action.cancel]`
  para sempre — o texto era escrito uma vez, na criação, antes de o banco de
  mensagens ser achado. Agora resolve ao aparecer (e acompanha troca de idioma).
- `PodeDesembarcarSensor`: o `MatchController` passou a ser cacheado como
  **objeto**, nunca a resposta da névoa, que segue lida a cada chamada.

## Frente 6 — i18n de rótulos fixos (outro agente)

`UITextData` + `LocalizedText` + `UITextDataEditor`: texto fixo de botão desenhado
na Unity ganha ficha em `Assets/DB/Messages/UI Text Data`, com português e inglês,
aplicada ao habilitar o objeto. Não precisa de Database. A janela de mensagens e
a auditoria (`tools/audit_panel_messages.py`) passaram a enxergar esse tipo. A
auditoria fechou em **428 IDs usados, 450 mensagens, 0 faltando** — por isso o
`[id]` do helper não era ficha faltando, era ordem de leitura.

## Frente 7 — o que entrou commitado depois da tag v9.2.1

- **Capturador:** quem fica com o prédio é quem **fecha primeiro**
  (`RodadasAteFechar`), não HP cru nem passos.
- **Perfil:** seções Papéis e Missões no `AIPresetData`, com a escada das três IAs
  (Fácil elementar, Médio uma peça, Difícil coordenação). O autor configurou os
  três presets nesta versão.
- **Modalidade pela arma, família pelo rótulo:** `preferArtilleryModeBeforeCombatant`
  caiu — nerfar a arma não mudava o comportamento.
- **Faixa de entrega do passageiro (C7):** `FaixaDeEntregaService` responde o que
  mais de trinta lugares respondiam com número fixo; não estacionar no alvo de
  captura; o acordo da artilharia.

## Fora do MVP

**Zona de Controle** ("hex de parada") foi medida e documentada em
[`implementação pós mvp.md`](implementação%20pós%20mvp.md): conceitualmente é uma
regra no pathfinding, mas são três buscas (a reversa é assimétrica), centenas de
`HexDistance` na IA, caches que passam a depender da **visibilidade** dos inimigos
e um parâmetro novo de ponto de vista. É um **X**, como regra opcional de partida.

---

## O que não terminou

- **Nada desta versão foi visto em Play** pelo autor no fechamento: Ação Direta,
  memória da captura, consertos transacionais, Configurações na Batalha e na
  Campanha.
- **Replay: voltar de um submenu descarta o buffer inteiro** (`HandleCancel` e
  `HandleScannerPromptCancel` chamam `DiscardPendingCombatCinematicTrack` no topo).
  Confirmado no código, não corrigido: exige ler cada ramo do cancelamento.
- **Regra a decidir:** "falhou depois do compromisso = conclui e marca como agiu"
  para transferência e suprimento (a captura já faz assim).
- **Configurações na Batalha** fecham para o tabuleiro, não de volta ao menu
  (reabrir passa pelo estado `PlayerMenu`, com guardas próprias).
- **Helper atrás do painel de Configurações no Sobre:** a correção sobe o ramo na
  hierarquia; se os dois estiverem em Canvas diferentes, não resolve.
- Ataque com corredor de tiro por terreno não explorado e origem visível: a
  neutralização de motivo só existe no ramo de destino escuro. A testar.
- Vigilância: o tiro do submarino (custo pago na célula, não no alvo) e o grupo
  anti-sub continuam só no papel.
