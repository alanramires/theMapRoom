# v8.5.1 — o que atravessa a cena

A `v8.5.0` fechou o laço da campanha. Este dia foi descobrir tudo que **atravessa
a troca de cena** — o que não devia e estava atravessando, e o que precisava
atravessar e não estava.

Duas frentes de código e uma de autoria, e as três esbarraram na mesma coisa por
caminhos diferentes.

---

## O fio do dia

> **A cena `Campanha` parece uma partida e não é.**

Ela tem `MatchController`, `TurnStateManager`, lista de jogadores e cursor —
porque foi construída sobre a cena-base de batalha. Tudo que pergunta *"estou
numa partida?"* olhando para o que existe na cena responde **sim**, e age errado.

No mesmo dia isso apareceu em quatro lugares independentes:

```text
cortina de hot seat subindo ao carregar save     MatchController
apresentação de rodada na seleção de mapa        PanelRodadaController
menu de batalha abrindo no mapa de campanha      BattleMapMenuRootController
música do time tocando na seleção                MatchMusicAudioManager
```

Nenhum era bug do save, nem da música, nem do menu. Eram todos a mesma pergunta
mal formulada.

---

## Frente 1 — o save aprende o endereço (frente paralela)

O buraco era silencioso e do pior tipo: `SaveGameManager` gravava o **nome da
cena** e nada mais. Carregar um save feito no `A_IA_Q3` abria `"Batalha"`, o
`QuadranteController` não achava endereço no `PartidaConfig`, caía nos campos do
Inspector — `A_IA_Q1` — e pintava o Q1 com as peças do Q3. Se o save viesse do
Q1, funcionava. **Esse é o modo de falha que não se descobre testando.**

Agora existem dois DTOs, e o desenho está certo:

```csharp
CampaignSelectionSaveData   mundoId · campanhaId · quadranteId · quadranteSerial
                            + o contrato inteiro: teams, isAI, flipX,
                              commandAutomatic, preset, difficulty

BattleMapSaveData           o endereço + quadranteSerial + paintOrigin
                            + recordsCampaignResult
```

E o `TryResolveSavedMap` casa por **`IdSerial` primeiro**, com os ids de texto só
como reserva. É exatamente o que o `INoDoMapa` pede há versões, e o motivo está
escrito lá: renomear um nó faz o `TryGet` não achar, e aí *"o destrave
simplesmente não destrava. Nenhum erro, nenhum log."* O save foi o primeiro
consumidor a honrar isso.

O `CampaignProgressStore` foi reescrito junto: saiu o arquivo JSON próprio, entrou
**snapshot dentro do save** (`ExportSnapshot`/`ImportSnapshot`), com
`BeginNewGame()` limpando o cache e um `ResetRuntime` no `SubsystemRegistration`.

⚠️ **Consequência que vale saber antes de testar:** `RecordOwner` não grava mais
em disco sozinho. O progresso vive no cache estático durante a sessão e só
persiste **quando o jogo é salvo**. O teste de aceitação do MVP — *"jogar dois
quadrantes, sair, voltar, e o mapa continuar contando a mesma história"* — passa a
depender de ter havido save.

---

## Frente 2 — as quatro respostas para "sou jogável?"

O `MatchController` ganhou o campo que faltava:

```csharp
[Tooltip("Desative em cenas de selecao: mantem o contrato dos jogadores, sem
         iniciar turnos ou cortina de hot seat.")]
[SerializeField] private bool isPlayable = true;
```

E os outros três passaram a perguntar, cada um do seu jeito e no seu lugar:

- **`PanelRodadaController`** — `EnsurePresentationAllowed()`, que acha o
  `MatchController` **da própria cena** (não qualquer um) e cancela a apresentação
  se ele não for jogável.
- **`SaveGameManager`** — guards de `CampaignSelection` em cada passo que só faz
  sentido numa partida: apresentação de rodada, música, exigência de spawners.
- **`BattleMapMenuRootController`** — na cena de campanha ele se **desliga**
  (`enabled = false`) e delega para `CampaignSelectionController.TryToggleCampaignMenuFromShortcut()`.

### O diagnóstico que localizou a cortina

O sintoma era: carregar da Tela de Entrada um save feito na Campanha fazia subir
o `Panel_rodada`. A causa não estava no save.

`IsHotSeatPrivacyRequired()` é `CountActiveLocalHumanPlayers() >= 2`. E a cena
`Campanha` serializa **dois humanos locais**:

```text
slot 0   teamId 2 (Azul)      isAI: 0   isLocal: 1
slot 1   teamId 3 (Amarelo)   isAI: 0   isLocal: 1     ← causa
slot 2   teamId 1 (Vermelho)  isAI: 1   isLocal: 0
```

Vindo do menu, o `PartidaConfig` marca o slot 1 como IA e sobra um humano local.
Vindo do **Load**, não há `PartidaConfig` pendente — a cena fica com a lista
serializada, com os dois humanos, e a cortina de hot seat sobe. **Não era bug do
save: era a cena autorada como se fosse partida hot seat.**

---

## Frente 3 — o quadrante diz com que cada lado começa

O quadrante já dizia quem começa em campo (`bakedUnidades`, `v8.5.0`). Agora diz
também com quanto no bolso, e os dois juntos dissolvem a armadilha do turno 2:
`Batalha.unity` serializa `startMoney: 0` com `allowDefeatForZeroUnits` ligado, e
o teste roda a partir do turno 2 — quem não comprasse no turno 1 perdia sem
entender por quê.

Agora isso deixa de ser possível **por autoria**. A carência no toggle saiu da
mesa: escondia o sintoma.

### As três origens, que confundir é o único jeito de errar isto

| o quê | de onde vem | campo novo? |
|---|---|---|
| unidades no turno 1 | do **bake** — o que está pintado no retângulo | já existia |
| renda por rodada | dos **prédios controlados** (`capturedIncoming`) | **nenhum** |
| renda inicial | do **quadrante**, autoral, quase sempre 0 | `economiaInicial` |

⚠️ **Não criar campo de renda por rodada no quadrante.**
`RecalculateIncomePerTurnForAllPlayers` faz *atribuição*, não soma, e roda a cada
início de turno: um valor declarado seria apagado no primeiro recálculo, sem erro
— e se não fosse, capturar cidade deixaria de valer alguma coisa.

A economia inicial é **autoral, não assada**, e o lugar importa: dinheiro não é
espacial. Sob o header `"Assado — artefato, nao editar a mao"` seria mentira, e um
dia alguém limpa a seção inteira e leva o número do autor junto.

O leitor já existia: `ApplyEconomyAtTurnStartForActiveTeam` credita o `startMoney`
uma vez, junto com a renda, e o `PartidaConfig.Apply` já zera o
`startMoneyApplied`. Faltava só o `TrySetStartMoney` e quem escrevesse antes do
turno 1.

De passagem, o `EnsurePartidaConfigApplied` subiu do `BuildConstrucoes` para o
`Build`: lá ele morava atrás de dois `return` antecipados, e a chegada do contrato
não pode depender de existir um spawner.

---

## Frente 4 — o `0b`, e ele encolheu

O resumo pedia `sceneLoaded` em **quatro** managers. São **dois**, e os outros
dois foram descartados por motivos diferentes — os dois verificados, não supostos.

```text
ObjectiveManager      plans        limpa. Quem limpava era SÓ o RestoreSaveData:
                                   carregar save limpava, partida nova não.
AITacticalAnalyzer    operations   limpa. Indexado por SLOT, e o slot 0 da
                      BySlot       próxima partida pode ser outra pessoa.
```

O `nextId` do analisador **não** é zerado: contador que só sobe nunca colide, e
zerar abriria a chance de um id reaproveitado casar com referência velha.

### O erro que a implementação desmentiu

Eu tinha afirmado, do `HexCohabitationVisualManager`, que ele carregava
*"referências a objetos da cena anterior, já destruídos"*. Fui escrever o hook e
parei:

```csharp
if (cachedTurnStateManager == null)
    cachedTurnStateManager = Object.FindAnyObjectByType<TurnStateManager>();
```

Os dois caches são `UnityEngine.Object`, e o `==` da Unity responde **true para
objeto destruído**. O padrão se **auto-cura** na primeira chamada depois da troca
de cena. Eu tinha concluído de um grep de "quais estáticos existem", sem raciocinar
sobre o fake-null da Unity.

> **"É estático e sobrevive à cena" não é o teste. O teste é "guarda dado ou
> guarda referência?" — dado contamina, referência morta se denuncia sozinha.**

O `AIShoppingPlanner` caiu pelo outro lado: conferido campo a campo, é tudo
`public` sob `[Header]` com `[Range]` e `[Tooltip]`. Configuração, e configuração
**deve** atravessar cenas. Um `Clear()` ali apagaria tunables e a IA jogaria
zerada, sem um erro.

---

## Frente 5 — o progresso vira pergunta

`ProgressoDaCampanha` responde nos três níveis, e **nada é gravado além do
quadrante**:

```text
quadrante concluído = o ÚLTIMO vencedor registrado é o meu slot
campanha  concluída = todos os quadrantes dela concluídos
bloco     concluído = todas as campanhas dele concluídas
```

Um campo `campanhaConcluida` seria um segundo lugar da verdade, e divergiria na
primeira vez que alguém rejogasse e perdesse — que é permitido, porque o registro
do quadrante é o **retrato da última tentativa, não da melhor**.

Isso fechou a divergência nº 2 da `v8.4.1` no sentido oposto ao que aquele
relatório supunha: *"`lastTurn` é último, não melhor"* **não era defeito, era o
desenho**. Uma dívida saiu da lista sem uma linha de código, e o tronco
(`plano_campanha.md`) foi corrigido em duas regras — a marca e o carimbo de
conclusão, que deixou de existir.

Consequência aceita de propósito: **o progresso anda para trás.** Dá para estar em
4/4 e voltar para 3/4, e não existe ponto sem retorno.

---

## Frente 6 — áudio: cada cena manda na sua faixa

Duas coisas quebravam a regra de três linhas do jogo.

**O prefab compartilhado carregava `playbackMode: Loop`**, herdado da Tela de
Entrada quando o `AudioManager` virou prefab das três cenas. Na Batalha, o modo
Loop caía no `ResolveLoopClipCandidate`, achava o `AudioSource` sem clipe e
devolvia a faixa de **abertura**. O sintoma foi *"a música da partida é a do
menu"* — parece bug de música, é de modo.

**E o `TryEnsureSceneTrackPlayback` só sabia MANTER a faixa de cena, não iniciar.**
Ele exigia que o clipe já estivesse carregado, ou seja, confiava no `Start` ter
acertado — e o `Start` tem saídas antecipadas (privacidade, carregamento de save)
que o fazem retornar sem tocar nada. Quando isso acontecia, o ByTeam assumia a
cena Campanha e nunca mais devolvia.

A pergunta virou *"esta cena tem faixa própria?"* em vez de *"a faixa própria já
está tocando?"*. Idempotente, e conserta sozinho qualquer `Start` que escapou.

Alguém já tinha visto metade disso: `ResolveCurrentClipVolumeMultiplier` tem o
comentário *"faixas de cena não pertencem a um time; seus sliders precisam vencer
o modo ByTeam, inclusive em cenas-base reutilizadas como Campanha"*. O **volume**
já estava protegido; a **escolha** da faixa não estava.

---

## Frente 7 — autoria (autor)

A cena `Fixture` ganhou **20 construções novas** (com `ConstructionHudController`
e a UI de cada uma), e o mundo foi reassado: o fixture passou de **13 para 26
construções assadas**.

---

## O que NÃO terminou

### Nada desta versão foi compilado

Não há build por linha de comando. **Todo o código foi escrito contra as APIs
lidas.** É o mesmo aviso da `v8.5.0`, e agora acumulado sobre mais uma versão de
mudanças — inclusive assinaturas novas (`TrySetStartMoney`, `IsPlayable`,
`ProgressoDaCampanha`). Confira o Console antes de acreditar em qualquer coisa
acima.

### O contador `1/4` não tem tela

O serviço existe e responde; ninguém pergunta ainda. Falta o consumo na cena
Campanha — três chamadas:

```csharp
ProgressoDaCampanha.FormatarProgresso(mundo, campanha, meuSlot)   // "1/4"
ProgressoDaCampanha.Concluida(mundo, campanha, meuSlot)           // reconhecimento
matchController.TryGetSingleActiveLocalHumanSlot(out meuSlot)     // quem sou eu
```

E a campanha vencida **oferece** a saída para a Tela de Entrada, não expulsa.

### O portão de destrave não existe

`Liberado()` é a recursão que **desce** — bloco libera campanha, que libera
quadrante — e precisa de navegação de **pai e de irmãos**, que o `MundoData` não
expõe. Só existem `TryGetBloco/Campanha/Quadrante` por id e `TryGetPorSerial`;
não há `Pai(nó)` nem `Irmaos(nó)`.

Saiu do caminho crítico do MVP de propósito: os quatro quadrantes têm
`destravadoPor` vazio, então o portão responderia "liberado" para tudo e **nenhuma
tela mudaria**.

### A mixagem de SFX está espalhada por três cenas

Conferido, e é o mesmo problema do `AudioManager` um degrau atrás:

| cena | cursor vem de | master | ui | move |
|---|---|---|---|---|
| Tela de Entrada | **inline, não é o prefab** | 0.4 | 1 | 1 |
| Campanha | `Cursor.prefab` + override | 0.4 | 0.3 | 0.3 |
| Batalha | `Cursor.prefab` + override | 0.4 | 0.3 | 0.3 |

Os SFX de cursor do menu tocam a **três vezes** o volume dos das outras cenas, e
como o cursor de lá é inline, mexer no prefab não o alcança.

Tudo que seria preciso para mixar já existe — master + dez categorias de SFX,
master + volume por faixa de música. O que falta é **um lugar só** para mexer.

### Volume não é preferência de verdade

**Zero `PlayerPrefs` na árvore inteira** — verificado. Nada sobrevive à troca de
cena, quanto mais a fechar o jogo. Começamos e paramos para não sair do MVP; o
`plano_campanha.md` já classifica volume como preferência, não certidão.

### `MatchController.cs` carrega duas frentes

O `isPlayable` (frente paralela) e o `TrySetStartMoney` (minha) entraram no mesmo
arquivo no mesmo dia. O commit `aac0c48` diz isso na mensagem. Se um dia precisar
reverter um dos dois, é edição manual.
