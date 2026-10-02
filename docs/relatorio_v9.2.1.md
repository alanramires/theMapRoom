# v9.2.1 — a IA pergunta antes de decidir

**Um dia de desenho, com quatro consertos pequenos que caíram dele.** Nada aqui
foi visto em Play: tudo **compilou** (`bash tools/compilar.sh`) e nada mais.

O fio do dia é uma pergunta do autor:

> *"hoje a AI toma uma decisão e se manda, né?"*

Sim. O `DecideUnitAction` é uma cascata em que **a primeira resposta não-nula
ganha** e a unidade executa. Ninguém compara, a ordem é uma só para todo papel,
e cada exceção vira um `if` cruzando dois papéis. O autor trouxe o desenho do
que quer no lugar: **todo papel responde as mesmas perguntas, numa ordem
própria, e só depois decide**. É, nas palavras dele, *"a melhor maneira de
evitar n × n de decisões"*.

Desse desenho saíram o contrato do questionário, o primeiro degrau dele em
código, e — porque desenhar a missão Reparo obrigou a ler o reparo inteiro —
quatro divergências entre o que o autor descreveu e o que o código fazia.

---

## Frente 1 — o questionário

Contrato em [`AI Behavior/contrato_questionario.md`](AI%20Behavior/contrato_questionario.md).
O que ele fixa, e por quê:

**As casas são de duas espécies.** O [`papeis.md`](AI%20Behavior/papeis.md) já
dizia: *8 ações diretas + 2 capacidades de informação*. Enxergar e Detectar não
são ações — ninguém "executa Detectar". Elas condicionam as casas abaixo delas.
Isso explicou o desenho novo do Capturador, que pôs **Capturar antes de
Enxergar**: *"posso capturar agora?"* é fato do prédio ao alcance e não depende
de visão. O §7.8 da ficha estava errado, não o desenho.

**Duas etapas.** A primeira casa de ação com SIM é só a decisão **preliminar**;
as políticas a revisam olhando o entorno. O exemplo do autor: o capturador pode
capturar, mas há outro capturador atrás com HP cheio e outro prédio livre no
alcance — a política troca "capturar" por "sair e deixar vago". A saída é o trio
**(célula, ação, motivo)**, e o motivo é a agenda do papel, nunca o efeito
colateral.

**As respostas em conjunto são um diagnóstico.** O segundo exemplo do autor:
*"consigo capturar? NÃO / enxergo? SIM / detecto alguém? NÃO — só essas três já
me contam a história do mapa"*. Uma combinação que não fecha é informação. Isso
é legítimo porque o pathfinding barra ocupante pela posição real, com ou sem
detecção (`UnitMovementPathRules.cs:170-200`), e o humano vê o mesmo buraco no
alcance — o `papeis.md` chama isso de *indício*.

**O prédio preto.** Conferido no sensor: o `PodeCapturar` recusa na hora de agir
só a célula **nunca explorada** (`PodeCapturarSensor.cs:358-369`). Hex explorado,
mesmo fora da visão, se captura. Daí o pedido de spotting do capturador: **prazo
é a fase de ações, não a rodada**, porque o spotter tem de agir antes dele no
mesmo turno; e **adiamento único**, pela guarda `!secondPass` que todo adiamento
da Fase 2 já exige.

**A missão é outra coluna do mesmo papel.** O próprio desenho tinha o
precedente: o Transportador tem duas colunas, Pickup e Courier. Reparo, SOS,
Guarnição e Rally trocam a coluna enquanto duram, com as mesmas casas. O autor
desenhou o quadro "Papéis em condições normais" (o modo de ataque padrão) e o
quadro "Missões"; o Reparo está desenhado inteiro (§11.1).

**O motor não pode conhecer papel.** Se aparecer `if capturador` dentro dele, o
n × n voltou. Por isso a divisão em três: motor genérico, respondentes iguais
para todo papel, e o papel só com ordem e políticas.

### O degrau 1, em código

`Assets/Scripts/Match/AI/Questionario/` — o **observador**. Roda depois de cada
decisão de um Capturador, antes da execução, sobre o mesmo estado, e escreve
`questionario_observador.log` na raiz, só no Editor: as respostas com o porquê,
a preliminar, as políticas, o final e o que o código de hoje fez, marcando
`<< DIVERGE`. **Não muda comportamento.** Liga e desliga no Inspector do
`AIController`.

Para o observador consultar o mesmo pedido de carona que o capturador, a
montagem do pedido foi separada do carimbo de espera
(`BuildCapturerRideRequestWithTarget`, `Capturer.Embark.cs`): o carimbo é efeito
colateral, e o observador só lê.

### As decisões do autor sobre o Capturador

- **A casa Capturar** pergunta "existe capturável na minha célula ou no meu
  **tático**?" — o teto fixo de 3 hexes perto de rally sai.
- **Com plano × sem plano:** o sem-plano cede se o com-plano chega ao prédio no
  tático; se só no operacional, o sem-plano captura primeiro. **Já existia**
  (`TryFindAssignedCapturerForCaptureTarget`), e ainda confere se o alocado já
  agiu — o caso F do teste da blitzkrieg, resolvido ali e esquecido no handoff.
- **Swap, Blitz, Vacate e ceder** viram políticas.
- **Capturador Combatente é rótulo de consulta**: classifica para baixo entre
  os capturadores. Quem fica com o prédio: com plano → Capturador antes de
  Combatente → quem **fecha** primeiro.
- **Defensor e Rally** são políticas que leem a postura — e, no fim do dia,
  SOS e Guarnição passaram a ser lidos como **missões**.

---

## Frente 2 — a fusão no reparo virou capacidade do perfil

O autor: *"o único papel que vale a pena fundir são os capturadores"*. Nos
outros, a fusão aumenta o dano e diminui a presença; sob invasão, abre lacuna
na defesa.

**O que o código fazia:** `fuseWhileInRepair` estava ligado em **27 de 35
fichas** — tanques, artilharia, EWACS, transportes. A doutrina dizia uma coisa,
as fichas autorizavam outra, e nenhuma deu erro. É o *configurado ≠ valendo* do
major, pelo avesso.

A decisão do autor foi não corrigir as fichas, e sim dar ao perfil a escolha:

```text
AICapabilityPreset.fusaoEmReparo
Fácil    Todos          funde sempre e erra — a regra antiga
Médio    Só capturador
Difícil  Desligado
```

O valor `0` é `Todos`, então perfil antigo não muda até alguém escolher. A flag
da ficha continua como permissão da peça. A pergunta única é
`PermiteFusaoEmReparo`, lida pelo reparo **e pela logística** — que adiava
suprir infantaria prestes a fundir; com duas respostas, o caminhão esperaria uma
fusão que o reparo nunca faria.

**A fusão saiu do questionário.** A "fusão de trabalho" (capturador ferido fora
do reparo, fundindo para capturar mais rápido) foi discutida e descartada:
*"as outras políticas já cobrem isso"* — capturador de 5 HP com um de 6 atrás é
caso de Swap, que mantém as duas peças em campo. O questionário ficou com nove
casas; a decisão de fundir é só do reparo.

### ⚠️ Eu li a fusão ao contrário

No meio da conversa afirmei, com arquivo e linha, que **o parceiro anda até o
receptor** e que por isso a fusão de trabalho "custaria uma rodada de captura".
Escrevi isso no contrato. Estava invertido: em `TurnStateManager.Merge.cs`
(592–669) **a unidade selecionada anda até o hex do parceiro**, o parceiro é
consumido, e não há filtro de "o parceiro já agiu". Eu tinha lido a linha que
diz quem é o receptor e não a que move a unidade. O contrato foi corrigido no
mesmo dia. A lição é a do resumo: **ler a execução inteira antes de afirmar a
direção de um movimento**.

---

## Frente 3 — o ferido da linha de frente sai primeiro

O autor, de partida jogada:

> *"a AI manda vários soldados, eu atiro nos da linha de frente. No turno
> seguinte os feridos continuam lá (porque agora são os últimos da iniciativa),
> então os tanques que vêm atrás não conseguem me atacar"*

O código já conhecia o problema **pela metade**: na invasão, o ferido ia para o
grupo 1 para liberar o corredor (`Initiative.cs:264`). Fora dela, grupo 5.

Agora o ferido **à frente da linha de combatentes sãos** também vai para o
grupo 1 (`IsWoundedInVanguard`, `AIController.Backline.cs`). A régua é a da
ferramenta **Tools ▸ Utils ▸ Retaguarda**, que o autor apontou — o mesmo
`AIBacklineAnalyzer` roda na janela e no jogo. Em jogo ele é honesto: lê
`snapshot.EnemyUnits`, filtrado pela névoa. A "trapaça" que a memória registra
é uma opção só da janela.

A linha é feita de capturadores e assaltos **sãos**, então o ferido na ponta
com tanques sãos atrás é vanguarda por construção. Muda só a **ordem** em que o
ferido age; o que ele faz continua sendo do reparo.

---

## Frente 4 — o reparo mede o prédio pela linha

O autor: elite terrestre repara em prédio na vanguarda, *"porque tem armadura e
aguenta o tranco"* — isso já existia (`EliteHoldsDangerousRepair`). O não-elite
*"recua para trás da linha, em direção ao HQ ou a prédios atrás da vanguarda"*.

O código não media "atrás da linha": media **"nenhum inimigo visível a N hexes
fixos"**. Um prédio a N+1 hexes do inimigo, mas à frente dos próprios tanques,
passava como seguro. E o contrário também: a força de combate segurando a
frente, um prédio aliado no meio do mapa atrás dela, um inimigo a N hexes — e o
soldado voltava para o HQ.

Agora, para o não-elite terrestre, nos dois sentidos:

```text
À FRENTE da linha     recusado — recua
ATRÁS da linha        aceito mesmo com inimigo a N hexes; só recusa inimigo encostado
NA linha / sem linha  a régua antiga
```

"Atrás" é **profundidade**, não a fatia lateral que a ferramenta pinta: um
prédio no meio do mapa conta mesmo fora do cone. A linha é montada uma vez por
busca. Vale no reparo e na evacuação, que reutiliza a mesma busca.

---

## Frente 5 — o fogo de suporte não repara passivo

O autor: *"fogo de suporte e unidades híbridas atiram em reparo — ficam no
prédio recebendo o reparo, mas não de forma passiva"*.

O código atirava em dois casos e **abandonava o prédio** no terceiro: inimigo a
≤3 hexes **com** um aliado são por perto. O raio fixo do "seguro" marcava
"ameaçado" justamente quando o obus tinha alvo (inimigo a 3–4 hexes), e o teste
do substituto o tirava do prédio sem disparar. Agora o tiro parado vem antes do
teste do substituto.

O que **não** mudou, de propósito: a caminho, fora de prédio de reparo, o
ferido não atira — recua. E a vacância do SOS: o não-elite numa base sob
pressão segue vagando a produtora; o elite fica. *"O SOS, além de atrair
unidades próximas pra voltar, também muda as condições do reparo."*

---

## O que não terminou

**Nada foi visto em Play.** As cinco frentes compilaram; nenhuma rodou. Antes
de confiar em qualquer uma, jogar.

- **Os perfis do Médio e do Difícil** precisam ter `fusaoEmReparo` salvo em
  disco (ver o fim desta seção).
- **O observador cobre só o Capturador**, e só duas políticas (pedido de
  spotting, cessão do prédio pela regra de hoje). Blitz, Swap, Vacate,
  montanha, Defensiva e Rally aparecem como "não ligadas".
- **O degrau 2** — o Capturador decidir pelo questionário, atrás de um toggle —
  não começou.
- **Aberto #13:** o reparo tem um segundo motivo de saída que não é o SOS —
  qualquer unidade num prédio ameaçado sai se houver aliado são a ≤3 hexes. Pela
  regra do autor, sair é só do SOS. Não mexi.
- **Aberto #12:** quando duas missões disputam a peça (ferido numa base sob SOS),
  qual ganha.
- **As colunas das outras missões** (SOS, Guarnição, Rally, EVAC, Spotting,
  Quero Carona) têm só o nome no quadro.
- **O Swap compara HP cru**, não cap power: um bazooka de 6 HP com chave 0.5
  ganha de um soldado de 5. Quando o Swap virar política, a régua é "quem fecha
  primeiro".
- **O oportunista pega o primeiro capturável** na ordem do dicionário, sem
  ranquear.
- **O `MelhorCombateService` existe e nenhuma IA o chama** — o contrato o
  marcava como inexistente; corrigido.

**O estado dos perfis em disco, no fechamento:** os quatro
`Assets/DB/AI/Presets/AIPreset_*.asset` não tinham o campo `fusaoEmReparo`. O
autor configurou no Inspector, mas a Unity só grava ao salvar o projeto. Se o
commit de churn desta versão não trouxer os perfis, o Médio e o Difícil rodam
como **Todos** — a regra antiga.
