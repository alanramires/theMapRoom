# Playtest — 3 de outubro de 2026

O primeiro playtest externo: um jogador que não é o autor jogou uma fase inteira
sozinho, do começo ao fim, e mandou o que achou.

## Quem e como

| | |
|---|---|
| jogador | João (marido da Poliana de Alencar) — relato repassado pela Poliana por WhatsApp |
| plataforma | celular, versão Web (play.unity.com), no navegador |
| mapa | o primeiro mapa (projetado para a queda durar ~1 h, com pontos de save) |
| dificuldade | Easy |
| resultado | **vitória** — zerou a fase |
| duração | a tarde toda |

## O que ele disse

**Funcionou**
- Músicas e efeitos sonoros funcionaram bem.
- Percebeu as diferenças entre unidades: HP, movimento e poder de fogo.
- *"O jogo é legal."*
- *"É nichado, mas pode agradar o público fiel exatamente por isso."*

**Problemas**
1. **Tela cheia.** Dentro da fase não havia opção de tela cheia; ela só existia na
   tela inicial. Ao bloquear o celular e voltar, o jogo saía da tela cheia e não
   tinha como reativar no meio da fase.
2. **Lento.** Não por ser difícil: achou demorado. Esclarecendo: não era a espera
   pela IA, era *"o jogo em si"*. Ele não sabe se terminaria em 1 h jogando direto,
   *"mas essa não é a realidade"* de quem joga no celular.
3. **Alcance e área.** Sentiu falta de variação de distância de tiro (alcance) e de
   dano em área.

Ele mesmo concluiu que o ritmo rápido de jogo de celular talvez não seja a proposta
do jogo.

## O que o autor respondeu

- Tela cheia: *"vou pôr essa correção lá."*
- Alcance: unidades mais fracas em HP já dão menos dano; dano em área ainda está
  sendo aprendido (lança-foguetes, ICBM).
- Ritmo: escolha de design de microgerenciamento. Não é tempo real como Red Alert
  ou Warcraft; o alvo é a velocidade de uma partida de xadrez, com saves para
  continuar depois.
- O Hard tem névoa de guerra e IA mais esperta; no Easy a IA recebe pouco dinheiro.
- A IA leva de 5 a 7 s de processamento por turno. Já existe um modo "IA Rápida",
  que pula a viagem do cursor até cada unidade, e ações mais contextuais estão a caminho
  ("tocar no transportador = embarcar", "tocar no inimigo = atirar").
- Promessa: em breve, um mapa com as três forças combinadas (exército, força aérea
  e marinha).

## Leitura

**"Lento" é duas coisas, e só uma delas é identidade.**

| | o que é | mexe? |
|---|---|---|
| tempo de **decisão** | o jogador parado, pensando "ataco ou recuo?" | não — é o jogo, ritmo de xadrez |
| tempo de **execução** | já decidiu, e ainda gasta toques navegando para fazer | sim — é atrito, e no celular parece lentidão |

Xadrez é lento pensando, nunca esperando nem clicando. Os Atalhos de Ação atacam
exatamente a segunda linha sem tirar nenhuma decisão do jogador. Um teste barato:
contar quantos toques custa um turno típico de 10 unidades.

**"Falta alcance" provavelmente é efeito do nerf.** O jogo tem alcance variável
(obus 3~4, Tanque Z 1~2), mas bazooka e metralhadora passaram de 1~2 para 1. No mapa
de estreia, com infantaria e blindado, quase tudo ficou com alcance 1. Não falta uma
mecânica nova: a variedade sumiu justamente da primeira fase.

**O Hard provavelmente não é mais rápido.** Névoa, IA com mais dinheiro, mais
unidades: mais para pensar, não menos.

## O que sai daqui

1. **Tela cheia alcançável dentro da fase.** Antes o atalho ficava ao lado da
   bandeira de voltar à base, mas comia espaço de hex e foi retirado. Agora vai
   para o menu de opções. Refinamento possível: um botão que só aparece quando o
   jogo **não** está em tela cheia (na Web), e some quando volta. O navegador exige
   um toque do jogador para entrar em tela cheia, e o botão é esse toque.
2. **"IA Rápida" ligada por padrão no build publicado.** Hoje ela existe no AI
   Manager, mas está desmarcada. Pula a viagem do cursor da IA pelo tabuleiro e as pausas de respiro entre uma escolha e outra (o resto continua igual, mas o turno fica muito mais ligeiro); os 5–7 s de processamento
   continuam. O testador **quer desesperadamente o modo turbo**: é o pedido dele
   que mais custa barato.
3. **Tela de opções com duas gavetas:**

   | gaveta | o quê | quando muda | onde vive |
   |---|---|---|---|
   | **Partida** | cor do humano, cor da IA, dificuldade | só antes do start; vai no save | tela pré-partida |
   | **Preferências** | tela cheia, cursor direto da IA, Atalhos de Ação | a qualquer momento, inclusive no meio da fase; lembradas entre partidas | menu de opções, acessível dentro da fase |

   É o caso do próprio testador: ele perdeu a tela cheia **no meio** da fase. Uma
   preferência que só existe antes do start reproduz o mesmo buraco no turno 8.

   Nomes: "Atalhos de Ação" diz o que faz, ao contrário de "Opções Inteligentes".
   "IA Rápida" promete que a IA pensa mais rápido, e não é isso: o que some é a
   viagem do cursor e o respiro entre escolhas. O nome deve dizer o que pula.
4. **Atalhos de Ação**: ação contextual pelo alvo do toque.
5. **Próximo playtest**: perguntar quanto tempo ele passou **esperando a vez dele**,
   para separar espera de decisão.

## Por que este registro existe

Ninguém de fora tinha jogado uma fase inteira até agora. O João pegou o celular,
jogou a tarde toda, venceu e voltou dizendo que é legal, e que o nicho é
justamente o ponto. O autor começou nos wargames aos 14 anos, com Game Boy Wars.
