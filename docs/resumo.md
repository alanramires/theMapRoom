# Resumo — onde estamos e o que vem

Ponto de retomada. Atualizado em 2026-10-07, **depois** da tag `v9.3.2`.
Leia isto primeiro.

---

## Estado

`v9.3.2` tagueada e publicada. **O primeiro MVP estável está no ar** (v9.2.0),
no Unity Play, e **o primeiro estranho já zerou uma fase**. Relatório do dia:
[`relatorio_v9.3.2.md`](relatorio_v9.3.2.md).

**A descoberta da v9.3.2: aula sem prédio é o primeiro mapa em que a IA rebelde não
tem âncora.** O capturador desistia, o reparo marchava para a bandeira do próprio
time e o Apache orbitava o soldado aliado. Agora o rebelde **caça** o inimigo
visível (com último recurso que ignora a decisão de ataque), o reparo sem prédio
que conserte cai, e o Apache caça no Operacional. O save passou a guardar a aula e
o mundo (o manifesto leva o `mundoId`). O motor das aulas ganhou fala avulsa,
`completeCommand`, relógio (`TURN_REACHED`) e alcance de serviço.

**A descoberta da v9.3.1: tutorial se testa tentando quebrar.** O autor jogou a
aula 1 da Caserna de ponta a ponta e cada atalho de aluno virou trava ou regra:
desembarcar um soldado só (impasse), capturar antes da hora, voar sem fazer
nada. Duas regras saíram: **bloqueio de roteiro é do aluno** (a IA levou bronca do
Sargento e ficou presa) e **a fala 0 roda depois do turno 1** (o início do turno
apagava o `acted CH`). A aula 1 fecha com fala final e volta automática à
Campanha; a aula 2 ainda não foi jogada.

**A descoberta da v9.3.0: a aula já era dado; só o palco era cena.** O
`TutorialData` carregava falas, tarefas, comandos e travas, e a cena só servia de
palco. A aula virou **um quadrante do mundo Academia** (Caserna → Soldado/Cabo →
aula), lançado pelo botão **Tutorial**. Numa aula, **só as tarefas decidem o fim**
(`UsesMatchEndRules`). O inimigo é escolha da aula (automata ou IA rebelde). O
roteiro fala na **coordenada do mundo de autoria**. Escrever aula:
[`tutorial/sintaxe.md`](tutorial/sintaxe.md) e
[`tutorial/aulas_caserna.md`](tutorial/aulas_caserna.md). **Nada rodou em Play
ainda**, e as duas aulas criadas estão vazias. A v8 fechou; os relatórios dela
estão em [`Versões/`](Versões/).

**A descoberta da v9.2.2: "lento" é duas coisas.** O testador (Easy, celular,
uma tarde num mapa de ~1 h) disse que o jogo "é lento". Tempo de **decisão** é
identidade (ritmo de xadrez); tempo de **execução** é atrito de toques, e só ele
se mexe. Daí a tela de Configurações (Partida × Preferências, Modo Turbo, Ação
Direta, Tela Cheia), a Ação Direta a partir da seleção (tocar no alvo anda até a
célula legal e para na confirmação) e, de tabela, o inventário do que ainda vaza
na névoa ([`pendencias do mvp.md`](pendencias%20do%20mvp.md)): **menu, mapa e
Jornal leem a mesma memória**. Nada disso foi visto em Play no fechamento.

**A descoberta da v9.2.1: a IA "decide e se manda".** O `DecideUnitAction` é uma
cascata em que a primeira resposta não-nula ganha. O autor desenhou o
substituto, e ele está em
[`AI Behavior/contrato_questionario.md`](AI%20Behavior/contrato_questionario.md):
todo papel responde as **mesmas nove casas**, numa ordem própria; a primeira
ação com SIM é só a **preliminar**, e as políticas revisam. A **missão** é outra
coluna do mesmo papel (Reparo, SOS, Guarnição...). O motor não pode conhecer
papel, senão o n × n volta. O degrau 1 (o observador, que só anota) está em
código; **nada do dia foi visto em Play**.

```text
v8.5.0   o laço fecha                     volta, dono por slot, tropa inicial
v8.5.1   o que atravessa a cena            save por endereço, isPlayable, 0b
v8.5.2   a forma casa com o dado           quadradinhos, paridade, fim de partida
v8.6.0   a etiqueta muda de dono          rally em lista, eixo escrito, 3 perfis
v8.6.1   configurado ≠ valendo            perfis autorados, portões fechados
v9.0.0   a mesma linha                    visão de regra única; Save Inspector
v9.1.0   o save manda na partida          load aplica o save; menu no turno da IA
v9.2.0   aguenta o celular de quem testa  toque, áudio/texturas web, sem LTO
v9.2.1   a IA pergunta antes de decidir   contrato do questionário; reparo pela linha
v9.2.2   o primeiro estranho jogou         Configurações, Ação Direta, névoa com uma memória
v9.3.0   a aula vira um quadrante          Academia, regras de fim da aula, captura/spawn no roteiro
v9.3.1   a primeira aula fecha             travas só do aluno, atalhos fechados, fala final, volta automática
v9.3.2   a aula 2 contra IA que briga      save da aula e do mundo, rebelde caça, peças de roteiro
```

**A descoberta da v9.2.0:** o Simulator da Unity não reproduz o navegador do
celular. "Funciona no Editor" não diz nada sobre memória. O celular morria **por
memória, sem erro no log**: música decodificada inteira, texturas DXT
descomprimidas no processador e logs. Mede-se pelo cabo (`adb` + DevTools
Protocol, `dumpsys meminfo` PSS), nunca por dentro da aba. Toda tela nova se
valida em **teclado, mouse e dedo**.

**A descoberta da v9.1.0:** a cena guardava estado de partida. Os flags de
início de turno, salvos como `1` na Batalha, prendiam a cortina depois do load.
Agora esses campos são `[NonSerialized]`. Se um campo do `MatchController`
muda durante a partida, ele não pode estar na cena.

**O MVP virou tela.** Menu → Campanha → Batalha → volta → o mapa pintado, o
placar, e o registro de quem tomou o quê, em quantas rodadas e **por quê**. O
autor jogou o Q1 até a rodada 3 com o perfil Médio.

**O major v9 se chama "Auditável".** O que está configurado tem de ser o que está
valendo, e dá para provar olhando. A direção vem do
[guia de modding](modding/guia%20de%20modding.md) do autor.

### A regra da visão (v9.0.0) — uma só, para ver, detectar e atirar

```text
ORIGEM   o terreno empresta EV a quem o ocupa?  sim → o emprestado   não → 0
         aeronave / submerso                     → EV da camada (DPQ para Ar)
ALVO     enxergar → cume do hex     detectar → camada da unidade
CONSTRUÇÃO  ocupante com altura: terreno empresta → emprestado, senão EV Base
            com Block LoS SUBSTITUI o EV do hex (cidade na montanha = 2)
            revela com a MESMA linha, partindo da altura dela; detecta só o próprio hex
```

Antes havia duas regras de origem, a construção não tinha altura (morava numa
lista do terreno) e revelava o disco inteiro sem linha. O autor definiu a regra
em quatro frases; a mais importante foi *"não tem essa salada"*. Se uma regra de
jogo parece pedir dois caminhos, **pergunte qual é a regra antes de oferecer
opções.**

### A descoberta da v8.6.1, que deu nome ao major

> **O que está configurado não é o que está valendo.**

Cinco vezes num dia, uma tela, um asset ou uma lembrança mostrava uma coisa e o
jogo fazia outra, e nenhuma deu erro: o Base Preset no Inspector, perfis copiados
do Gulosa, números do perfil que ninguém lia, a âncora da Retaguarda lendo tropa
real, e o "limpar o progresso" (que existe, sim: está no save do slot 1; ver a
errata no relatório v9.0.0).

### As descobertas da v8, comprimidas

- **v8.6.0, uma resposta só não serve a mais de um respondente:** a etiqueta muda
  de dono (rally em lista, eixo escrito, portões por capacidade). Se é preciso
  **desligar** metade da resposta para a outra funcionar, a entidade está errada,
  não o flag.
- **v8.5.2, a forma casa com o dado:** contável vira quadrado, contínuo vira
  barra.

### As descobertas anteriores, que seguem valendo

> **v8.5.1 — a cena `Campanha` parece uma partida e não é.**

Ela tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu
da cena-base de batalha. Perguntar *"estou numa partida?"* olhando o que **existe**
na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao
**da própria cena**, não a qualquer um.

> **v8.5.0 — cor não é identidade. É uma fantasia que o slot veste por uma
> partida.**

As duas cores são escolhidas no menu. Tudo que atravessa a fronteira entre autoria
e partida — ou entre uma partida e a seguinte — endereça por **slot**, e a cor se
resolve só na hora de pintar, por `GetTeamIdForSlot`. Vale para o tint do mapa, os
quadradinhos, o registro do vencedor e o dono de cada construção.

---

## Vocabulário

```text
MUNDO       uma cena de autoria + UM asset. O globo inteiro, desenhado de uma vez
 └─ BLOCO       Europeu · América do Norte · Rússia    ← o jogador escolhe
     └─ CAMPANHA    Europa · África
         └─ QUADRANTE   Inglaterra · Congo...          ← aqui se luta, e é o que é assado
```

| termo | é |
|---|---|
| **quadrante** | o retângulo recortável onde se joga |
| **setor** | `ConstructionSector` — rótulo estratégico numa construção |
| **slot** | quem é o dono. Slot 0, slot 1. **A cor é roupa dele nesta partida** |
| **turno** ⚠️ | **duas palavras para a mesma coisa, e elas discordam.** O `currentTurn` do `MatchController` só incrementa em `CloseRoundAndAdvanceToFirstPlayer` — ou seja, conta **rodadas** (o ciclo completo de jogadores). Mas o código o chama de "turno" em todo lugar: HUD da batalha, cortina de privacidade. O **autor** chama isso de *rodada* e reserva *turno* para a jogada individual. O registro do quadrante já usa o vocabulário do autor (`RODADAS: 3`); o HUD ainda não |

Um quadrante **contém** setores.

**ids são técnicos** (`feijao-torto`), nomes são livres (`Feijão Torto`). O id é o
que o save grava e o que o endereço casa; a bancada avisa quando ele tem acento ou
espaço.

---

## O fluxo, e onde cada peça mora

```text
Tela de Entrada   PanelMenu:980   cor, cor, dificuldade, preset → PartidaConfig
      ↓           espera o SFX de confirmação terminar (WaitForSecondsRealtime)
Campanha          CampaignSelectionController   mosaico, setas, tint por dono
      ↓           Set(...) + SetDifficulty + SetQuadrante → LoadScene("Batalha")
Batalha           QuadranteController (-9000)   pinta terreno, construções,
      ↓                                          camadas, rotas e TROPA INICIAL
                  MatchController (0)            aplica a config (ou já foi aplicada)
      ↓           vitória/derrota → grava o SLOT vencedor → Enter
Campanha          republica a config na volta, e o quadrante aparece na cor do dono
```

**Toda a travessia é `PartidaConfig`.** Ele é de **consumo único**: quem produz
chama `Set`, quem consome chama `Apply` + `Clear`. Por isso a volta tem de
republicar — foi o que quase quebrou o laço em silêncio.

---

## O que existe

```text
Assets/Scripts/Campanha/    MundoData · BlocoData · CampanhaData · QuadranteData
                            INoDoMapa · QuadranteController
                            ConstrucaoAssada · UnidadeAssada · CamadaAssada · RotaAssada
                            EconomiaInicialSlot          ← caixa inicial, autoral
                            ProgressoDaCampanha          ← Concluido() nos 3 níveis
                            CampanhaManager              ← ponte da cena p/ o placar
                            CampaignProgressStore        ← por SLOT, e dentro do save
Assets/Editor/              MapHelperWindow (a bancada) · MapaTerrenoJson
                            RoadRoutePainterWindow · SceneSanitizerWindow
Assets/DB/Campanha/         Mundo Fixture.asset
Assets/Scripts/UI/          BarraVencedorController      ← um quadrado por quadrante
                            PanelVitoriaController       ← os botões do fim de partida
Assets/Prefab/Managers/     AudioManager.prefab          ← nas 3 cenas do fluxo
Assets/Prefab/              Panel_turn · Panel_vitoria · MenuRoot · Cursor
Assets/Scenes/Autoria/      Fixture (26 construções, 6 trechos) · Mundo
Assets/Scenes/              Campanha · Batalha           ← as duas no Build Settings
```

O fixture, quatro quadrantes de tamanhos diferentes:

```text
bloco A · Auridia
 └─ campanha A_IA · "A invasão a Auridia"
     ├─ A_IA_Q1  Feijão Torto     (-18,10)  16×17   272 tiles
     ├─ A_IA_Q2  Terra Firme      (-18,-9)  21×20   420 tiles
     ├─ A_IA_Q3  Peixe Pequeno     (-3,10)  35×17   595 tiles
     └─ A_IA_Q4  Tubarão Branco     (2,-9)  30×20   600 tiles
```

29 construções assadas, **todas no Q1 e no Q2**:

```text
A_IA_Q1  HQ + fábricas cada lado    sem caixa: jogável, a renda do HQ (6000) entra antes da 1ª compra
A_IA_Q2  HQ + fábricas cada lado    10000 por slot, chinook no slot 0
A_IA_Q3  NENHUMA construção         2000 no slot 1, que não compra nada
A_IA_Q4  NENHUMA construção         2000 no slot 0, que não compra nada
```

Sem HQ, os dois lados do Q3 e do Q4 entram no modo rebelde (v8.6.1, Frente 4).

---

## Onde eu parei

### O que a v9.3.2 deixou — a caça rebelde ainda não foi vista atirando

1. **Ver a caça em Play.** As duas últimas correções (segunda passada sem
   `PassesAttackDecision`; varredura de tiro sempre, porque `HasEnemyInEngageRadius`
   mede com `Vector3Int.Distance`) não voltaram testadas. Log a procurar:
   `caca (ultimo recurso…) move+ataca` ou `caca: sem tiro… fecha distancia`
   (`AIController.Capturer.Rogue.cs`).
2. **`HasEnemyInEngageRadius` segue em reta na grade** para os capturadores com
   plano. Corrigir muda a IA com QG: decidir com o autor.
3. **Save de aula**: carregar no meio, pela aula aberta e pelo menu, ainda não foi
   visto. Saves anteriores à v9.3.2 não têm `mundoId` no manifesto (dançam uma vez).

### O que a v9.3.1 deixou — aula 1 jogada, aula 2 não

O roteiro é o JSON em `docs/tutorial/` (fonte de verdade); **todo ajuste exige
Importar no asset** — o jogo lê o asset, não o JSON.

1. **Rejogar a aula 1** depois das últimas mudanças (nenhuma foi vista em Play):
   contador de captura decrescente, `op_01` como gatilho interno, fala de vitória
   com o painel esperando 3,5 s, barra de 6 s voltando à Campanha.
2. **Jogar a aula 2** (`caserna_soldado_2.json`) com a mesma bateria de atalhos da
   aula 1: o que o aluno faz sem querer que trava o roteiro?
3. O rebelde que nasce **fora da tela** é intencional. Se ele atirar sem nunca ter
   entrado no enquadramento, pôr um `pan` no turno inimigo.
4. **Abertos conhecidos:**
   - o save no meio da aula não guarda o passo do roteiro;
   - um save da Academia carregado pelo menu abre no Fixture;
   - o aluno é sempre o slot 0;
   - o eixo do slot 0 do Fixture mudou (`0,2,3,8 → 2,3,8`) sem autor conhecido:
     conferir.

### O que a v9.2.2 deixou — primeiro jogar, depois o replay

**Só compilou.** Roteiro de Play, na ordem que mais rende:

```text
Configurações   abre pela Tela de Entrada, pelo Gerenciar da Batalha e pelo menu da
                Campanha; setas com destaque verde; Sobre na frente; Tela Cheia na Web
Ação Direta     tanque anda até o morro e mira; artilharia só atira parada; embarque
                num caminhão a 2–3 hexes; cancelar no meio desfaz tudo
captura         prédio seu capturado longe: antes do seu turno o menu NÃO oferece
                captura; depois do Jornal, o mapa pinta inimigo e oferece
transacional    cancelar movimento pela estrada não completa objetivo do tutorial;
                MovementPath do replay não vem vazio
```

**Próximo código: o descarte do replay ao voltar de submenu.** `HandleCancel`
(`TurnStateManager.StateMachine.cs`) e `HandleScannerPromptCancel`
(`TurnStateManager.ScannerPrompt.cs`) chamam `DiscardPendingCombatCinematicTrack`
no **topo**, antes de saber se é "voltar uma etapa" ou "cancelar a ação". Mover o
descarte para os ramos que voltam à seleção ou ao `Neutral`, lendo um a um.

**Decisão pendente do autor:** "falhou depois do compromisso = conclui e marca
como agiu" (como a captura já faz) para transferência e suprimento — entra no
contrato transacional se ele aprovar.

**Fora do MVP:** Zona de Controle, medida em
[`implementação pós mvp.md`](implementação%20pós%20mvp.md).

### O questionário — o tronco da IA agora

O contrato é o mapa; leia antes de mexer em decisão de papel. Estado:

```text
degrau 1  observador do Capturador        ESCRITO, só compilou, nunca rodou
          Assets/Scripts/Match/AI/Questionario/ → questionario_observador.log (raiz, só Editor)
degrau 2  Capturador decide pelo questionário, atrás de toggle   NÃO COMEÇOU
degrau 3  os outros cinco papéis                                 NÃO COMEÇOU
```

**Primeiro passo da retomada: jogar com o observador ligado** (Inspector do
`AIController`, *Questionário (observador)*) e ler o log. Procurar `<< DIVERGE`
(onde o código de hoje e o questionário discordam) e "prédio no tático, mas
PRETO" (rodadas perdidas por névoa).

O que foi decidido e espera código: o pedido de spotting (§6.3), Swap/Blitz/
Vacate como políticas (§6.7), quem fica com o prédio — com plano → Capturador
antes de Combatente → quem **fecha** primeiro (§6.8). Os quadros "Papéis em
condições normais" e "Missões" são do autor; o Reparo está desenhado (§11.1), as
outras seis missões têm só o nome.

**Abertos que dependem do autor:** #12 (qual missão ganha quando duas disputam a
peça) e #13 (o reparo ainda tira tanque e soldado de prédio ameaçado quando há
aliado são por perto — pela regra do autor, sair é só do SOS).

**Só compilou (v9.2.1)**, além do observador:

```text
fusão em reparo no perfil   Fácil todos · Médio só capturador · Difícil ninguém (perfis salvos)
ferido na vanguarda         sai primeiro na iniciativa (IsWoundedInVanguard, régua da Retaguarda)
reparo pela linha           não-elite recusa prédio à frente da linha, aceita atrás (sem voltar ao HQ)
fogo de suporte em reparo   atira de volta parado no prédio, em vez de sair
```

Os três últimos usam a mesma linha de combatentes: um cenário testa os três.
Mexer na iniciativa muda a ordem da Fase 2 inteira.

### O que o autor exercitou jogando

`bash tools/compilar.sh` prova que compila. Comportamento só se prova jogando, e
foi assim que a paridade do Q2, o título perdido e o `bar_tvencedor` apareceram.

```text
✅ o laço inteiro        menu → campanha → batalha → volta → mapa pintado
✅ paridade odd-r        o Q2 (originY ímpar) monta idêntico ao mosaico
✅ cores pelo slot       tint, quadradinhos e registro concordam
✅ tropa e caixa inicial  A_IA_Q2 tem chinook no slot 0 e 10000 para cada lado
✅ save/load de campanha  volta ao quadrante em foco
✅ perfil Médio           Q1 até a rodada 3, pelo menu (save do slot 2)
✅ cenários de visão      soldado na mata, montanhas A-B-C-D, HQ atrás da serra
✅ load pelo contrato     o save manda, não a cena; cortina só no load
✅ cursor depois do load  passa a vez e vai direto ao QG da IA
✅ captura = HP           Bazooka 10 numa cidade de 0,5 causa 5
✅ celular (Unity Play)   25 unidades em combate, ~660 MB, status normal, sem erro
✅ dedo na Campanha       seleciona quadrante e abre o menu pelo toque
✅ Carregar Jogo na web   abre (o build com LTO travava)
```

**Só compilou (v9.2.0):** o APC deixando de esperar soldado ferido e o botão de
menu da Batalha pausando a IA. **Aberto:** o planejador da IA leva 7–9 s por
turno no celular (105 ms no PC) e há ~1 s de atraso ao selecionar quadrante; os
dois pedem Profiler, não palpite.

**Só compilou (v9.1.0):** a IA retomando depois de sair do menu com ESC no
turno dela, o load de um save feito no turno da IA, o deadlock do shopping da IA
e a rede de 3 s do pedido de menu. **Fora do MVP:** hot seat humano × humano e
os tutoriais, que estão no build profile mas sem entrada no menu principal.
Ainda em aberto: o panel_dialog no menu do turno da IA mostra o que está sob o
cursor (onde a IA agia). Respeita a névoa, mas o autor ainda não decidiu se quer
isso.

Não visto em partida ainda: a construção revelando com linha, o Play direto com
perfil, a linha MOTIVO no painel (pode não caber), os perfis Fácil e Difícil.

### A IA obedece o perfil, mas só nos toggles

O catálogo está ligado na Batalha, e **nenhum portão pergunta mais "sou o
difícil?"**. Os três perfis foram montados a partir das frases do autor (v8.6.1,
Frente 2): Fácil com ⅓ da renda e sem blitzkrieg, Médio com blitzkrieg, Difícil
com spam de soldado e os números do lado hard.

O que falta, em ordem:

1. **A poupança está decidida (autor, pós-v8.6.1): as três dificuldades poupam
   para elite.** O que muda é o piso:
   ```text
   Fácil/Médio  compram pelo plano e podem deixar produtor vazio enquanto poupam
   Difícil      compra o básico em CADA fábrica (conscrição sempre); a sobra vai
                para o elite que ela persegue, senão compra mais básico
   ```
   No 2×2 do `AICapabilityPreset`, isso é Fácil/Médio = teto sem piso, e
   Difícil = piso + teto. O tooltip do 2×2 diz que, sem alvo elite, a sobra do
   Difícil compra "unidades do plano"; o autor descreve "o básico". Conferir
   qual dos dois o código faz quando a fase 2 ligar o `poupaPraElite`, que hoje
   não tem leitor.
2. **Fase 2, os números.** Nenhum dos ~70 valores do perfil é lido:
   - ~20 getters do `AIController`, cada um num ponto único (fácil);
   - ~50 campos públicos do `AIShoppingPlanner`, que viram campo privado com
     `FormerlySerializedAs`, mais getter.

   O mapa campo a campo está no gerador
   ([AIPresetGeneratorWindow.cs:275](../Assets/Editor/AI/AIPresetGeneratorWindow.cs)).
   Use ele, e não busca por nome.
3. ~~Perfil em uso e Play direto~~ — feitos na v9.0.0. O Inspector do
   `AIController` mostra o perfil e as capacidades efetivas; o Play direto resolve
   o perfil pelos flags da cena.

### Auditável: a direção que o autor escolheu

Feito na v9.0.0:

```text
Save Inspector          Tools ▸ Auditoria ▸ Save Inspector — lê pelo mesmo caminho do load
perfil em uso           quadro no fim do Inspector do AIController
resultado com motivo    o progresso grava o VictoryReason; placar com MOTIVO
regra de sensor única   visão: FogKnowledgeSnapshotBuilder.CollectConstructionVisibleCells
```

O que vem, em ordem de custo:

1. **Diff de dois saves** no Save Inspector. Foi comparar saves que revelou o bug
   no mod do CK3.
2. **Valor efetivo dos NÚMEROS** com a origem (perfil, cena ou reserva). Vira
   obrigatório quando a fase 2 religar os números; hoje só as capacidades mostram.
3. **A regra de inclusão da construção no FOW de runtime** (quem é dono, HQ como
   marco global) ainda é cópia própria em `ApplyFriendlyConstructionVision`. A
   linha foi unificada; a inclusão, não.

### O teste da blitzkrieg reprova no código atual

O [`teste_blitzkrieg.md`](AI%20Behavior/teste_blitzkrieg.md) do autor pede troca
sem plano, mas a troca mora no planner. Os casos C, E e F falham em
[Handoff.cs:97](../Assets/Scripts/Match/AI/2.%20Planner/AIController.PlanEvaluator.Handoff.cs):
- substituto que não alcança o prédio é aceito;
- o vizinho do prédio conta como chegar nele;
- ninguém confere se o substituto já agiu.

### O eixo autorado precisa de uma ferramenta melhor

Escrever o grafo funciona: ficha do prédio → dropdown → grava na lista do
quadrante, e o desenho no Scene View mostra o que o jogo vai seguir. O veredito
do autor:

> *"não é prático pra dar manutenção, mas resolve, e é o que temos no momento"*

O gargalo é montar o grafo prédio por prédio sem ver a linha se formando.
Melhorar o dropdown é polir o gesto errado — o caminho é desenhar no Scene View:
clicar o QG, clicar os setores na ordem. E lembrar: **o desenho só se refaz quando
se aperta "Desenhar eixos" de novo.**

### O `0b` NUNCA rodou

Os hooks de `sceneLoaded` do `ObjectiveManager` e do `AITacticalAnalyzer` estão
escritos desde a `v8.5.1` e ninguém exercitou. É o tipo de bug que só aparece na
segunda partida, e nenhuma sessão chegou lá.

> mapa A → turno 5 → menu → mapa B. No turno 1 do B, o plano da IA tem de nascer
> **vazio**.

Sintoma se falhar: a IA do segundo mapa persegue setor que não existe ali.

### O progresso só existe dentro de um save — e o slot 1 tem o falso

O `CampaignProgressStore` vive em memória, é zerado a cada Play e a cada Novo
Jogo, e só vai para o disco dentro do `.tmrsave`.

**O save do slot 1** (cena Campanha, 10/09) guarda o `RODADAS: 3` falso: Q2 no
slot 1 em 3 rodadas, Q1 no slot 0 em 2. Carregá-lo traz o placar falso de volta.
Apagar ou não é decisão do autor. O mesmo save tem um `capturedBuildingHistory`
estranho (slot 0 com `teamId: 2` numa cena sem construção), não investigado.

A pasta `CampaignProgress/` de uma versão antiga foi apagada; o código não a lia.

### Três lugares respondem "de quem é este quadrante"

```text
CampaignSelectionController.GetWonSectorCounts    conta por slot
CampaignSelectionController.GetQuadrantOwners     lista por quadrante
ProgressoDaCampanha                               a versão que sobe a hierarquia
```

Concordam hoje porque todos consultam o `CampaignProgressStore`. Vão discordar no
dia em que "concluído" virar recursivo — *campanha concluída = todos os quadrantes
dela* — que é o que o portão de destrave vai precisar. Gatilho conhecido, conserto
conhecido: os dois primeiros delegam ao terceiro.

### Arestas de tela, todas conhecidas

- **O painel de inspeção tapa a borda direita do mapa.** Navegar até um quadrante
  de lá é inspecionar algo que não se vê. Ideia parqueada: o painel escolher o
  lado oposto ao quadrante em foco.
- **Sem o `presentationTextOverride`, a campanha cai no "Turno {n}"** do ramo de
  batalha. Visível, então não é falha silenciosa — mas turno não significa nada
  numa tela de seleção.
- **Os números laterais e os quadradinhos dizem a mesma coisa** com 4 quadrantes.
  Ganham utilidade quando uma campanha tiver 12.
- **A mixagem de SFX segue espalhada** por três cenas, uma delas com cursor
  inline, e o menu toca a 3× o volume das outras.
- **O silêncio entre menu e campanha** continua: `MatchMusicAudioManager` sem
  `DontDestroyOnLoad`, e `BuildWorldMosaic` no `Awake` em ordem `-10000`.

### Depois

```text
1. testar o 0b — jogar o Q1 e o Q2 em sequência já é o teste
2. o fim da campanha: 4/4 oferece a volta à Tela de Entrada
3. unificar as três contagens (gatilho: o portão de destrave)
4. o portão hierárquico — Liberado() precisa de pai e irmãos, que o MundoData
   não expõe
```

**O Q3 e o Q4 ficam vazios por enquanto, por decisão do autor.** A campanha se
joga no Q1 e no Q2.

Da visão (v9.0.0), pequenos e conhecidos:
- **EV padrão das construções** é 1 / bloqueia (a flag, 0 / não). Docas,
  Hidrobase, Estação de Trem e Terminal talvez queiram outro valor.
- **O `Planicie.asset` ainda guarda a lista antiga** `constructionVisionOverrides`
  em YAML. O código não lê; a Unity descarta no próximo save do asset.
- **O bake da rodada 0 está desatualizado** (cache na versão 4). Não precisa para
  jogar: no Play é só atalho de partida. Só as ferramentas no Edit Mode pedem.

O rótulo "ALPHA BUILD v0.3" é indicativo: a grafia diferente entre as cenas fica
como está.

Pós-MVP, com rascunho feito: o **Quero Spotting**
([QueroSpottingWindow.cs](../Assets/Editor/QueroSpottingWindow.cs)) e o "quadro de
missões". **Antes de retomar missões, ler o
[`contrato_missoes.md`](AI%20Behavior/contrato_missoes.md)** (v7.2.1): ele já
desenha `SpottingDeCobertura`, e a conversa da v8.6.1 redescobriu partes dele sem
saber. O `IsForwardObserverSpot` vai cair, porque o autor não quer lugar
autorado de spotting.

⚠️ **`MatchController.cs` carrega duas frentes.** `isPlayable` (paralela) e
`TrySetStartMoney` (minha) entraram no mesmo arquivo no `aac0c48`. Reverter um sem
o outro é edição manual.

---

## A escada

```text
-1. serviços burros do tabuleiro  ✅
 0. sensores PodeX                ⚠️ o laço de HEX ainda mora no PodeDetectar;
                                     a LINHA tem regra única desde a v9.0.0
 1. serviços de área (Hotzone)    ⚠️ falta cobertura de DETECÇÃO
 2. consumidores Melhor*          ⚠️ faltam Suprir, Fundir, Detecção e Spotting
 3. papéis → somente POLÍTICA     ⚠️ as seis fichas existem; o QUESTIONÁRIO tem contrato
                                     e observador (v9.2.1); RoleData ainda não
 4. variações de papel            perfil/trait depois da extração
 5. CAMPANHA                      🟡 o laço roda de ponta a ponta e tem tela;
                                     falta EXERCITAR o 0b e o portão de destrave
```

O degrau 3 ganhou vizinho na v8.6.0: a **doutrina** saiu do `hardMode` e virou
capacidade nomeada, lida de um perfil autorado. É o mesmo movimento do degrau —
política deixando de ser `if` espalhado — só que na camada do general, não na do
papel. Na v8.6.1 a viagem das **capacidades** terminou; os *valores* ainda não
saíram do lugar.

O degrau 0 ficou mais firme na v9.0.0: a linha de visão, de detecção e de tiro
parte de uma regra só, e a construção usa a mesma reta que a unidade.

---

## Armadilhas que importam nesta retomada

| armadilha | regra |
|---|---|
| **Importar com a Unity compilando** | o campo novo chega VAZIO no asset: ele já conhece o campo, o importador ainda é o antigo (a fala da Capitã chegou muda). Importar só sem a rodinha de compilação |
| **checagem no início do turno** | `OnActiveTeamChanged` dispara ANTES do `ReleaseUnitsForActiveTeam` (upkeep). Quem lê combustível ali vê o valor de antes do consumo; leia no `Neutral` |
| **"escolher" e "bloquear" com a mesma regra** | aeronave no ar não bloqueia spawn, e a escolha da bandeira herdou isso: dois Apaches na mesma bandeira. Separar a regra de bloqueio da de preferência |
| **IA assumindo que sempre há prédio** | sem capturável e sem prédio que conserte, três ramos desistiam em silêncio. Mapa de aula/teste é o caso que os expõe |
| **trava de roteiro valendo para a IA** | o "passar a vez" travado por uma fala muda pegou a IA, que passa a vez pelo mesmo caminho do humano: bronca do Sargento na IA e turno preso. Toda trava de aula pergunta `IsActiveTeamAI` |
| **fala de abertura antes do turno 1** | `acted CH` na fala 0 era apagado pelo `ResetForTeamTurnStart`, que roda frames depois numa coroutine. Estado inicial de aula espera `MatchController.MatchStartApplied` |
| **painel que monta botão no `OnEnable`** | o `Panel_vitoria` pergunta `PodeVoltarParaCampanha` ao ligar; quem arma a volta é o `OnMatchConcluded`. Aviso **antes** do painel, senão o botão nasce escondido |
| **objetivo novo que já existia** | escrevi `UNIT_OUT_OF_FUEL`; o `UNIT_DEAD` já aceitava `TOKEN \|\| AUT=X`. Antes de criar objetivo/comando, ler a lista de ids no `TutorialManager` e a [`sintaxe.md`](tutorial/sintaxe.md) |
| **"só roda na cena de tutorial"** | componentes nascidos para cenas próprias (`TutorialManager`, automata) assumiam que estar presentes = estar numa aula. Na Batalha eles vivem em **toda** partida: o automata dirigiria o turno da IA. Ao mover um componente para uma cena compartilhada, procurar o que ele faz **sem** o dado que o justificava |
| **regra normal de fim numa partida especial** | eliminação, QG e estrelas rodavam nas aulas: a facção que só nasce no meio da lição era eliminada no turno 2. Partida especial declara o seu grupo de regras de fim (`UsesMatchEndRules`) |
| **mundo fixo em mais de uma cena** | o mundo estava serializado na Campanha **e** no `QuadranteController` da Batalha. Trocar de mundo só numa delas manda a Batalha procurar a campanha no mundo errado. O mundo viaja no contrato (`PartidaConfig.MundoAtivo`) |
| **coordenada lida em vários lugares** | o `x,y` do roteiro era lido em seis lugares (três cópias). Conversão de referencial só vale se todos passarem pelo mesmo leitor (`TryParseScriptCell`) |
| **árvore limpa tomada como Inspector salvo** | no fechamento da v9.2.1 o `git status` estava limpo e os perfis do Médio e do Difícil **não tinham o campo** em disco: o Inspector marca e não grava. Antes de taguear configuração, conferir o campo no `.asset` (grep) e pedir *File ▸ Save Project* |
| **afirmar a direção de um movimento lendo meia execução** | eu disse que na fusão o parceiro anda até o receptor; é o contrário — a **selecionada** anda até o parceiro e o consome (`TurnStateManager.Merge.cs`, 592–669). Li a linha que nomeia o receptor, não a que move. Ler a execução até o `SetCurrentCellPosition` |
| **"o papel X não faz Y" contado só nos arquivos do papel** | a revisão de papéis afirmou que a IA nunca funde; a fusão morava no **reparo**. Comportamento de papel pode estar num handler transversal (Repair, Logistics, Router) |
| **frente que mistura arquivo no commit** | `git add -p` não roda aqui (interativo). Monta-se o índice do arquivo como HEAD + só os blocos da frente (`git hash-object -w --path` + `update-index --cacheinfo`). Foi assim que a v9.2.1 saiu em cinco commits. **Na v9.2.2 o atalho de aplicar blocos de `git diff -U0` falhou duas vezes:** em modo texto o Python perde o CRLF, e chaves `{` soltas viram blocos que nenhuma palavra-chave pega. O arquivo misto foi inteiro para a frente dominante, com a mensagem dizendo o que ele carrega da outra |
| **Toggle que "não navega"** | navegava: o Toggle padrão seleciona com (245,245,245) sobre o quadradinho branco. O `Panel_NewGame` já resolvia com `ApplySelectionHighlight` (`#4A5A43`). Antes de depurar a navegação, conferir se a seleção é **visível** |
| **texto escrito uma vez, na criação** | os botões do helper gravavam `[helper.action.cancel]` para sempre porque eram montados antes de o banco de mensagens ser achado. Rótulo que depende de banco (ou de idioma) se resolve ao **aparecer**, não ao nascer |
| **busca de botão presa a um painel** | o `Button_config` mudou do `Panel_options` para o `Panel_gerenciar` e o menu só procurava no primeiro: o botão não abria, sem erro. Quando o autor move um botão de painel, conferir **onde** o código o procura |
| **"funciona no Simulator"** | o Simulator renderiza pelo Editor: não mede memória nem o navegador. Teste real = build servido na LAN (`npx http-server -a 0.0.0.0 -p 8000`) com o celular na **claro5** (subrede 192.168.1.x do PC) + `chrome://inspect` |
| **medir memória por dentro da aba** | `Runtime.queryObjects` varre o heap e DERRUBOU a aba. Use `adb shell dumpsys meminfo` e leia o **PSS**, não o RSS |
| **LTO na Web** | "Runtime Speed with LTO" travou o Carregar Jogo (`RuntimeError: unreachable`); sem LTO funciona. Para testar LTO de novo, gerar com Debug Symbols Embedded |
| **override de áudio na aba Web** | não existe taxa de amostragem nem nada além de formato e qualidade; Force To Mono vale para todas as plataformas. Taxa e canais se mudam no ARQUIVO |
| **campo autoatribuído por nome** | um `OnValidate` que reatribui pelo nome do arquivo desfaz a escolha do autor a cada edição. Autopreencher só o que estiver vazio |
| **estado de partida serializado na cena** | um campo do `MatchController` que muda em jogo, se for serializado, a cena salva com o valor do último teste. Os flags de início de turno em `1` prenderam a cortina do load. Campo de partida é `[NonSerialized]`; quem persiste é o save |
| **pausa como flag que alguém desliga** | a pausa do menu só soltava por um caminho de fechamento, e a IA parava para sempre pelos outros. Pausa é derivada (`PlayerPauseHolds`); nunca dependa de lembrar de desligar |
| **lista de estados copiada** | "o jogador está no menu" estava em três lugares, cada um com estados diferentes, e a mesma tela sumiu duas vezes. É `TurnStateManager.IsInPlayerMenuScope` |
| **`.tmrsave` é zip** | `grep` direto no arquivo não enxerga o texto comprimido e volta vazio. Foi assim que o relatório v8.6.1 afirmou que nenhum save tinha progresso, e o slot 1 tinha. Buscar em save pelo Save Inspector ou por `zipfile` |
| **nome do arquivo de save** | carrega a cena em que o slot foi criado: `quicksave_slot1_Battle Map 1 - Groud` é um save da Campanha. A cena vem do **manifesto** |
| **abrir opções para uma regra de jogo** | perguntado por que o soldado da mata herdava EV, eu ofereci três caminhos; a regra era uma só (*"não tem essa salada"*). Antes de propor alternativas, perguntar qual é a regra |
| **origem × alvo da linha** | são perguntas diferentes. Origem: o terreno decide se empresta EV (aeronave: camada). Alvo: enxergar = cume do hex, detectar = camada da unidade. Unificar uma não mexe na outra |
| **construção que ocupa substitui o hex** | com Block LoS, a altura dela é a do hex (cidade na montanha = 2, não 2,25). Sem Block LoS (flag) é marcador e o terreno fica |
| **cache que não vê mudança de regra** | o hash de config do FOW cobre mapa e flags, não regra. Mudou regra de visão? Suba `FogSourceCacheFormatVersion` e `FogRoundZeroSlotBake.CurrentFormatVersion` |
| **`git mv` já põe no índice** | um `git commit` seguinte leva as renomeações junto, mesmo sem `git add`. Separe antes de commitar outra coisa |
| **configurado tomado como valendo** | o Inspector mostra o campo, não o que o jogo usa. O `Base Preset` dizia Difícil e era só a reserva de um catálogo que cobre tudo. Antes de concluir pelo que a tela mostra, pergunte **quem responde em runtime** |
| **perfil copiado de um perfil de teste** | o Médio era o `AIPreset_Gulosa` renomeado, e o Fácil uma cópia dele: herdaram toggles de teste que contradiziam a doutrina escrita no próprio asset. Asset renomeado carrega o passado; confira contra a doutrina |
| **gerador que copia só um lado do par** | o gerador de preset copia o lado **normal** dos pares normal/hard. Os três perfis tinham os mesmos ~70 números. Religar leitura de valor sem comparar antes enfraquece o Difícil calado |
| **busca por nome tomada como ausência** | `intel.lookbackTurns` "sem leitor" se chama `IntelShoppingLookbackTurns` no shopping. Para mapear preset → cena, o gerador é o mapa; grep por nome não é |
| **Play direto na Batalha** | sem menu e sem save, a dificuldade não chega e a IA roda **sem perfil**, com os flags antigos da cena. Teste de perfil se faz pelo menu |
| **ferramenta que lê o fato em vez do observador** | a Retaguarda tira a "massa inimiga" da posição real das tropas. Para geografia o autor aceita; para decidir onde pedir olho é wallhack por procuração |
| **forma que não casa com o dado** | contável desenhado como barra obriga o jogador a extrair contagem de proporção; contínuo desenhado como quadrado finge discreto o que é fracionário. Antes de escolher barra ou quadrado, pergunte se o dado é contável |
| **busca por nome que falha calada** | `FindNamedChild("bar_vencedor")` com o objeto chamado `bar_tvencedor` não desenhou nada, sem erro nem log. Busca por nome dispensa arrastar referência, e o preço é falhar em silêncio — **toda busca por nome tem de avisar quando não acha** |
| **duas coisas amarradas no mesmo campo** | o `presentationTextOverride` decidia o título E se o placar aparecia. Quando a campanha precisou dos dois, alguém apagou o texto para o placar voltar, e o título se perdeu. Campo que decide duas coisas separa no dia em que elas divergirem |
| **escape `
` em script que gera código** | a ferramenta processa a barra antes do Python e o literal C# não casa. Usar `chr(92)`. **Está nesta tabela e eu tropecei duas vezes antes de olhar** |
| **`currentTurn` lido como jogada individual** | ele conta **rodadas**: só sobe quando o índice de jogador dá a volta. Passar a vez de um jogador ao outro não mexe nele. Todo número de "turno" que atravessa telas ou vira registro precisa dizer qual dos dois é |
| **translação de recorte em grade hexagonal** | odd-r: a posição de mundo de uma linha depende da PARIDADE do y, não da diferença entre dois y. Traduzir um retângulo autorado em y ímpar para y par inverte a paridade de todas as linhas e cisalha o recorte meia célula — e **nada reclama**, porque o tile continua na célula lógica certa. Se um recorte precisa mudar de y, a paridade tem de sobreviver |
| **nome interno que não casa com o botão** | a Tela de Entrada mostrava FÁCIL/MÉDIO/DIFÍCIL e o enum era Iniciante/Facil/Competitiva — o perfil chamado "facil" mudava o botão MÉDIO. Resolvido na v8.6.0 reduzindo a três com os nomes do jogador. Quando um rótulo de tela e um identificador interno convivem, **mostre os dois lado a lado na ferramenta** |
| **entrada de enum extinta num asset** | catálogo gravado com `dificuldade: 4` (o antigo `Competitiva`) não casa com nada depois da redução, e o perfil cai no caminho de reserva **calado**. Toda tabela dificuldade→asset precisa podar entrada que não existe mais, e dizer que podou |
| **reassar não conserta autoria** | a lista de eixos mora fora da seção assada de propósito; o bake não a toca. "Mudei e reassei e não mudou" é sintoma de estar olhando a fonte errada, não de bake quebrado |
| **o recorte de autoria vaza para o arquivo da cena** | não o recorte — o efeito dele. O `SectorManager` serializa `sectorInfos`, então salvar a Fixture com um quadrante em foco grava a lista **truncada**. Na autoria é cache e se refaz; mas é por isso que a `Fixture.unity` encolheu ~600 linhas na v8.6.0, e **não é regressão** |
| **editor que desenha o que o jogo não vai rodar** | o desenho de eixos só lia o quadrante que a Batalha pintou, e na autoria nada está pintado — mostrava sempre o leque automático. Editor divergindo do jogo é pior que editor sem desenho: a ferramenta passa a ensinar errado |
| **cena que parece partida** | a `Campanha` tem `MatchController`, `TurnStateManager` e lista de jogadores porque nasceu da cena-base de batalha. Perguntar "estou numa partida?" olhando o que EXISTE na cena responde sim e age errado. Pergunte ao `MatchController.IsPlayable` — e ao **da própria cena**, não a qualquer um |
| **`== null` da Unity em cache estático** | referência a objeto destruído responde `true` para `== null`. O padrão `if (cached == null) cached = Find(...)` **se auto-cura** entre cenas. "É estático e sobrevive à cena" NÃO é o teste de contaminação; o teste é **"guarda dado ou guarda referência?"** — dado contamina, referência morta se denuncia sozinha |
| **prefab compartilhado levando config de uma cena** | o `AudioManager` virou prefab das três cenas carregando o `playbackMode` da Tela de Entrada, e a Batalha passou a tocar a música do menu. O que é IGUAL nas cenas vai no prefab; o que é DIFERENTE deriva da cena ou vira override |
| **guarda que só sabe MANTER, não iniciar** | `TryEnsureSceneTrackPlayback` exigia que a faixa já estivesse tocando para protegê-la — confiava no `Start`, que tem saídas antecipadas. Guarda de invariante tem de perguntar "esta cena tem faixa própria?", não "a faixa própria já está tocando?" |
| **nome de cena como identidade de save** | o save gravava só o nome da cena, e toda partida de campanha é "Batalha". Carregar um save do Q3 pintava o Q1 com as peças do Q3 — e **funcionava** se o save viesse do Q1 |
| **cor tomada como identidade** | a cor é escolhida no menu, por partida. Dono, progresso e tropa endereçam por **slot**; a cor se resolve na hora de pintar. Violado em 3 pontos independentes num dia só, e nenhum deu erro |
| **`SetSlotIndex` tomado como "mudar de dono"** | ele só escreve o campo e **deixa a cor como estava**. Quem muda dono é `SetOwnerSlot` (construção) ou `SpawnAtCellForSlot` (unidade) — os mesmos caminhos da captura |
| **pintar antes da configuração chegar** | `QuadranteController` é `-9000`; o `Awake` do `MatchController` é `0`. Quem pinta antes tem de chamar `EnsurePartidaConfigApplied` primeiro |
| **`PartidaConfig` tomado como estado** | é de **consumo único**. Quem volta pra uma cena tem de republicar, senão a cena nasce com as cores serializadas nela |
| **`SetTile` tomado como cópia** | leva o tile e **descarta rotação, espelho e cor**. Só aparece em arte que tem lado; terreno é o caso em que o defeito é invisível |
| **`SetTransformMatrix` ignorado calado** | um tile com `LockTransform` faz o tilemap descartar a matriz sem avisar. Destravar (`SetTileFlags(None)`) antes de orientar |
| **recortar uma sequência como se fosse conjunto** | rota é ordenada e os consumidores leem PARES. Tirar uma célula do meio COLA as vizinhas numa aresta que ninguém desenhou |
| **`IsRouteValid` é tudo-ou-nada** | uma célula inválida descarta o desenho da rota INTEIRA |
| **`Build` fora do Play** | tudo que ele escreve é serializado. Numa cena compartilhada por todos os quadrantes, salvar grava o layout de um dentro dos outros |
| **camada decorativa é tilemap IRMÃO** | `ClearAllTiles` no tabuleiro não encosta nela |
| **contador de id recalculado do máximo** | certo no `UnitSpawner` (ids morrem com a partida), **errado** no mundo (o registro sobrevive ao nó) |
| **hexágono tratado como grade quadrada** | é odd-r: linha ímpar desloca meia célula |
| **campo sem leitor tomado como inofensivo** | `fieldEntries` tinha zero leitores **e** obrigava sete catálogos a existir. Por isso `UnidadeAssada` nasceu **sem** HP, combustível e elite |
| **generalizar do que se encontra sem checar a razão** | **perguntar por que aquilo existe antes de construir em cima** |
| **`ConstructionSector` default** | é `Alpha = 0`, não `None = -1`. Esquecer o setor não dá erro: dá plano degenerado |
| **id com acento ou espaço** | o YAML escapa (`"Feij\xE3o Torto"`) e o texto é digitado em dois lugares que precisam bater |
| **parse frágil de `.asset` virando afirmação** | contagem por faixa de linha é confiável; `$3` de `awk` não |
| **`.asset` em disco tomado como estado atual** | Inspector marca `dirty` e **não grava** |
| **`sed` em C# sem conferir chaves** | comeu um `}` de fechamento. Contar profundidade antes de seguir |
| **apagar dado do autor sem perguntar** | perguntar, sempre |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí o `-9000` e o portão |
| **construção na interseção de quadrantes** | a faixa é para o **chão**. Peça ali nasce nos dois |
| **`Grid` divergente entre autoria e Batalha** | cell size/layout/swizzle diferentes torcem toda tradução de coordenada |
| farol tratado como lock | promessa e claim distribuem preferência; nunca proíbem |
| singleton de mapa atravessando cenas | `BeachManager` e `SectorManager` são da cena corrente |
| posição hipotética criando verdade | nenhum cálculo provisório atualiza FOW, ocupação ou caches |

---

## Documentos de referência

| documento | uso |
|---|---|
| [`Planos/plano_campanha.md`](Planos/plano_campanha.md) | **o tronco** — autoria, recorte, progresso, cenas, bloqueios, teste |
| [`Planos/briefing_cena_campanha.md`](Planos/briefing_cena_campanha.md) | o contrato entre as duas frentes |
| [`AI Behavior/contrato_questionario.md`](AI%20Behavior/contrato_questionario.md) | **o questionário** — casas, duas etapas, o Capturador, as ordens, as missões, os abertos |
| [`relatorio_v9.3.2.md`](relatorio_v9.3.2.md) | a aula 2 contra IA que briga — save da aula e do mundo, rebelde caça, fala avulsa, relógio |
| [`relatorio_v9.3.1.md`](relatorio_v9.3.1.md) | a primeira aula fecha — travas só do aluno, atalhos fechados, fala final, volta automática |
| [`relatorio_v9.3.0.md`](relatorio_v9.3.0.md) | a aula vira um quadrante — Academia, regras de fim da aula, tarefas de captura, bake embarcado |
| [`tutorial/sintaxe.md`](tutorial/sintaxe.md) | **como escrever uma aula** — comandos, tarefas, parâmetros, convenções |
| [`relatorio_v9.2.2.md`](relatorio_v9.2.2.md) | o primeiro estranho jogou — Configurações, Ação Direta, a névoa com uma memória só |
| [`pendencias do mvp.md`](pendencias%20do%20mvp.md) | o inventário transacional: o que vaza na névoa, o que foi decidido, o que falta testar |
| [`playtest de 3 de outubro 2026.md`](playtest%20de%203%20de%20outubro%202026.md) | o primeiro playtest externo e o que saiu dele |
| [`relatorio_v9.2.1.md`](relatorio_v9.2.1.md) | a IA pergunta antes de decidir — questionário, fusão no perfil, reparo pela linha |
| [`relatorio_v9.2.0.md`](relatorio_v9.2.0.md) | aguenta o celular — memória medida, áudio/texturas web, LTO, toque |
| [`relatorio_v9.1.0.md`](relatorio_v9.1.0.md) | o save manda na partida — load, estado fora da cena, menu no turno da IA |
| [`relatorio_v9.0.0.md`](relatorio_v9.0.0.md) | a mesma linha — visão de regra única, construção com altura, auditável |
| [`Versões/relatorio_v8.6.1.md`](Versões/relatorio_v8.6.1.md) | configurado ≠ valendo — perfis, portões (com errata na v9.0.0) |
| [`modding/guia de modding.md`](modding/guia%20de%20modding.md) | o guia do autor; a direção "auditável" sai dele |
| [`AI Behavior/capturador_politicas.md`](AI%20Behavior/capturador_politicas.md) | as quatro políticas do capturador, e o [teste da blitzkrieg](AI%20Behavior/teste_blitzkrieg.md) |
| [`AI Behavior/contrato_missoes.md`](AI%20Behavior/contrato_missoes.md) | missões (brainstorming da v7.2.1) — ler antes de retomar o quadro de missões |
| [`Versões/relatorio_v8.5.2.md`](Versões/relatorio_v8.5.2.md) | a forma tem que casar com o dado — quadradinhos, paridade, fim de partida |
| [`Versões/relatorio_v8.5.1.md`](Versões/relatorio_v8.5.1.md) | o que atravessa a cena — save por endereço, isPlayable, o 0b |
| [`Versões/relatorio_v8.5.0.md`](Versões/relatorio_v8.5.0.md) | o laço fecha, e o dono deixa de ser uma cor |
| [`Versões/relatorio_v8.4.1.md`](Versões/relatorio_v8.4.1.md) | orientação, rotas partidas e identidade estável |
| [`Versões/relatorio_v8.4.0.md`](Versões/relatorio_v8.4.0.md) | o dia em que o catálogo parou de dizer onde |
| [`AI Behavior/Transporte.md`](AI%20Behavior/Transporte.md) | estados, promessas, coleta e entrega |
| [`arquitetura/acoes_transacionais.md`](arquitetura/acoes_transacionais.md) | lei de compromisso e rollback |

---

## Regras de trabalho

- **Nada no jogo é definitivo antes do compromisso da ação.**
- **Plano pedido não autoriza implementação.**
- **Verificar antes de documentar.** Busca vazia não prova ausência, e `.asset`
  em disco não prova estado.
- **Perguntar por que uma coisa existe antes de construir em cima dela.**
- **Uma frente por commit.** `git add .` só no churn.
- **Não editar `.asset` no disco com o Inspector aberto.**
- **Não salvar `.cs` enquanto o autor testa em Play.**
- Fechar o dia pela skill `.claude/skills/fechamento-do-dia/SKILL.md`.
