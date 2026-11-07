# v10.4.0 — Ler o que o jogo já sabe

2026-11-07. Dia que começou na compra aérea e terminou dentro da Fase 2. O fio: quase todo
defeito do dia era alguém **recalculando o que já estava pronto** — a janela refazia a compra,
o AI Step refazia a névoa, o utilitário de construção refazia a cena, o courier refazia o
consumidor — e, no fim, o achado maior: a Fase 2 inteira lia uma lista de inimigos que **nunca
tinha sido preenchida**.

---

## O achado do dia: a Fase 2 jogava sem ver o inimigo

`e72deff`. A Fase 2 usa `AIWorldSnapshot.BuildLight`, e ele omitia `EnemyUnits` com o
comentário *"nenhum handler de role lê essas listas do snapshot"*. Valia quando foi escrito.
Depois nasceram leitores — **Backline** (linha de combatentes: ferido na vanguarda, reparo pela
linha, fogo de suporte atrás da linha), `FireSupport.Helpers`/`Embark`, `Repair`,
`Assault.Rally`, `Logistics.Supply`, `MelhorDesembarque`, `Questionario` — e todos rodavam com
a lista **vazia**, sem erro nenhum: "sem inimigo conhecido, não há frente, responde false".

Não eram cegos: ataque (`PodeMirar`), ameaça (`CalculateThreatLevel`), combate aéreo (lista
própria), planner e shopping (snapshot completo). A IA lutava vendo o jogador; o que decidia
"onde ficar em relação à frente" decidia num campo vazio.

**Consequência para a leitura do passado:** vários itens "só compilou" da v9.2.1 — reparo pela
linha, ferido na vanguarda, fogo de suporte em reparo — nunca apareceram em Play por isso, não
por estarem errados. Observação de posicionamento por linha **anterior a `e72deff` não vale
como evidência.**

Agora o Light preenche `EnemyUnits` pela **névoa confirmada** (`IsUnitVisibleForSlot`), leitura
do snapshot de névoa que o jogo já calculou. *Só compilou.* Vai mudar o comportamento:
políticas de linha rodam na Fase 2 pela primeira vez.

### Como foi achado — e as hipóteses que caíram

O filtro novo "não atraca no alvo com inimigo em cima" (`6d66dd5`) não via um Soldado parado
na cidade. Hipótese 1: a faixa reversa do passageiro saía vazia por causa do inimigo no alvo —
**caiu**: `CalculateMovementCostMap` expande por terreno, não olha ocupante. Hipótese 2: o
snapshot (`NoCache`) e o AI Step (névoa confirmada) discordavam sobre a visibilidade — **caiu**:
a sonda `01ca338` nunca disparou, nem com névoa. Quem matou foi o autor: *"estou testando sem
fog"* — se tudo é visível e o filtro não vê, a lista não é a que se pensa. Era o `BuildLight`.
Sonda removida em `135fe2e`.

---

## 1. A escada de resposta aérea

`ea634be`, `8724ac0`. Do autor: *"o cara compra capturador, você compra Disruptor, ele compra
anti-Disruptor; as aeronaves dele voam na sua praia, você compra antiaérea; quando você tiver
muitas, ele compra bombardeiro; ele comprou bombardeiro, você ergue os caças."* E: *"você ergue
um SAM e ele fica lá na base... onde está o caça?"*

- **"Vende" = "compraria agora".** `OffersPrimaryRole`, `CanAnyOwnProducerOffer` e a escada da
  presa passaram a respeitar a cadeia de elite, como o carrinho. Antes a urgente pedia SAM sem
  AAA em campo (ou Caça A sem Caça B), o carrinho descartava, e ela nunca fechava. Agora desce
  ao secundário, compra o elo de baixo, e o elo abre a cadeia do de cima.
- **Retaguarda × vanguarda.** O anel diz **o que** a aeronave toca, fora do **guarda-chuva**
  (células que um antiaéreo nosso acerta no próximo turno, por classe de presa): prédio →
  antiaéreo; tropa fora de prédio → **caça pela presa** (bombardeiro/Plane → Caça A, senão B;
  Apache/Heli → Caça B). Coberto não pede: o guarda-chuva é o teto natural da bateria.
- **Breakthrough pela presa e com escala:** conta a classe Artillery (AAA e SAM inclusos — a
  presa primária do bombardeiro) e pede 1 a cada 3, até 3. Antes, papel `FogoIndireto` (pegava
  a AA por acidente) e um bombardeiro só, para sempre.
- **Peça aérea que rompe a poupança é pré-comprada** (`8724ac0`). Visto no AI Step T8: a
  vanguarda pediu Caça B, liberou 8580 da reserva, e o carrinho (que prefere largura) comprou
  Apache + Apache + Chinook com o dinheiro. O ramo "libera" soltava o dinheiro sem garantir a
  peça — defeito que já existia na escolta.
- O caça orbita o capturador-capitão (`AirCombat.cs:755`): nasce para ir com a frente.

*Vista em Play:* a demanda `vanguarda` nasceu com a presa certa. *Só compilou:* a compra depois
de `8724ac0`, o breakthrough, a escada com cadeia.

## 2. Avião Tanque pelo vaivém, não pela foto

`58f8387`. *"Os helicópteros estão indo e vindo freneticamente e ficando sem combustível."* A
operação `AirRefuelSupport` pedia tanque por aeronave com ≤35% **no instante da compra** — quem
já voltou estava reabastecido e sumia, e o Chinook (gatilho 40%) quase nunca ficava abaixo de 35%.
A pressão nova mede **turnos perdidos indo e voltando**: aeronave no gatilho de autonomia da
própria ficha, distância em turnos até a recuperação mais perto (aeródromo nosso ou tanque que
opere na camada dela, pelo `PodeSuprirSensor`), além do primeiro turno, ×2. 3 turnos = 1 tanque,
teto 2. Com tanque perto da frente a conta cai sozinha. *Só compilou* — e com prioridade 18 ele
provavelmente perde para o carrinho; a pré-compra ficou como decisão do autor.

A crítica do autor ficou registrada: a demanda logística terrestre também é *"um contador de
feridos, é idiota"* — a mesma régua (custo do vaivém) serve ao caminhão. **Não tocado.**

## 3. As ferramentas param de refazer o jogo

- **AI Step** (`acc0e5f`) virou *"um jogo separado do nosso jogo"*: a cada unidade, duas fotos
  do tabuleiro inteiro (névoa sem cache, varredura célula a célula do terreno, plano duas vezes)
  e ~700 KB de JSON formatado na thread principal, duas vezes por passo. Agora: névoa
  confirmada, terreno só no passo **marcado**, segunda foto só se a revisão mudou, JSON compacto,
  `captureMs`/`persistMs` no registro. **Medido em Play:** ~30 KB por passo, foto < 3 ms,
  gravação < 5 ms.
- **Shopping Pressure** (`e9c3265`, `278c838`, `104dea1`): pressões operacional e de counter eram
  chamadas **no desenho** (~40×/s), depois por relógio de 10 s (FrameSpike de 1–4 s com a IA
  parada no AI Step). O combinado do autor era *"é na hora de fazer compra"*: agora o `Decide`
  guarda o que o carrinho viu, por slot (recusado em outra cena — o planner é
  `DontDestroyOnLoad`), e a janela só lê. **Atualizar** simula, rotulado SIMULAÇÃO. De quebra o
  `DecideRoleBased` deixou de calcular roster e counter duas vezes.

## 4. O vilão dos 13 s — uma consulta com custo escondido

`9327b4a`, `ad51ff0`. O filtro da outra sessão (`1ab2a60`, espera de carona fora da produtora —
regra certa) chamava `IsOwnProductionCell` **por célula** do mapa de encontro de longo alcance, e
cada chamada fazia `GetConstructionAtCell` → `FindObjectsByType<ConstructionManager>`. Milhares
de buscas na cena por decisão. Conserto local (conjunto montado uma vez) e o estrutural:
`GetConstructionAtCell` lê `ConstructionManager.AllActive` em Play (mesmo conjunto que a busca
com `Exclude`); fora do Play segue buscando a cena — ferramenta de Editor tem que ver a cena.
**Visto em Play:** 13 s → 9 s (só o local) → ~2 s.

## 5. A viagem da carga

O helicóptero que *"sobrevoa por cima de onde vai entregar"*.

- **Poda no courier** (`d3ecee2`): o "move + desembarca" simulava o sensor em ~100 células;
  célula sem vizinho na faixa de entrega do principal morria no fim com certeza. Descartada antes.
- **O consumidor conta por que recusou** (`2f58bc2`): só `transporter_cell_not_allowed` falava;
  as LZs mortas em `CanEndMove`, no sensor ou no pareamento sumiam caladas — e o diagnóstico
  pareceu "ocupação aérea" quando o motivo era outro. O log então mostrou o quadro real: cidade
  (8,12) com Soldado inimigo em cima, AAA ao lado, terreno sem pouso em volta.
- **Banda Operacional para qualquer carga** (`6f61523`). Doutrina do autor: *"manteria a missão
  de pegar a cidade, mas pousaria em outro lugar e faria o resto do caminho a pé e em combate
  frontal."* O passe Operacional (passageiro chega em dois turnos a pé) só rodava para carga
  livre ou grupo de 2+. **Visto em Play:** `Operational:hit`, o heli progrediu para célula livre e
  deixou a entrega para a rodada seguinte; o plano B do courier **não rodou**; decisão do Chinook
  8,2 s → 3,3 s. Vale para os três couriers (aéreo, terrestre, naval) — o APC e o navio ainda não
  foram vistos.
- **Não atraca com inimigo em cima** (`6d66dd5`) — inútil até `e72deff`, pelo motivo acima.

O autor fechou a investigação no lugar certo: *"o propósito agora não é jogadas inteligentes,
mas sim a viagem da carga."* Parar na frente da AAA é jogada, não viagem.

## 6. Da outra sessão (lido no diff)

`1ab2a60`: herdeiro do blitz revalidado na execução (vivo, desembarcado, sem reparo, sem agir,
no Tático); embarcados fora dos candidatos de troca; espera de carona fora de produtora em todas
as bandas do MelhorEmbarque. Testes `tools/ai_test/verificar_handoff_embarcado.cs` e
`verificar_encontro_fora_produtora.cs` PASS no Editor fora do Play (registro em `docs/ai_test.md`).

---

## Hipóteses minhas que caíram

- **"Os FrameSpike de 1–4 s são do AI Step":** o principal suspeito era a Shopping Pressure (o
  relógio de 10 s); não confirmei por eliminação, o autor saiu do Play antes.
- **"Loop travando os testes":** eram **duas recargas de domínio** — eu salvei `.cs` com o autor
  em Play. Regra reforçada na memória: salvar `.cs` conta igual a `unity recompile`.
- **"A faixa do passageiro sai vazia com inimigo no alvo"** e **"as visibilidades discordam"** —
  ver o achado do dia.
- **"`execution=9375ms` é custo":** é o tempo do autor lendo o AI Step antes de clicar.

## O que não terminou

- **Comportamento novo da Fase 2** (`e72deff`): linha de frente, retaguarda, reparo pela linha,
  fogo de suporte, rally, logística e desembarque passam a ver inimigo. Ninguém viu rodar.
- **Revisão do tabuleiro mudando durante a decisão do Chinook:** o passo #4 do AI Step gravou
  `executionSameAsDecision=False` — a revisão mudou entre decidir e executar sem nada confirmado.
  Suspeito: a simulação de desembarque (`SetCurrentCellPosition(cell, false)` provisório) mexendo
  na revisão. Se for, é provisório tocando estado confirmado. **Não verificado.**
- **24 `validPaths` numa decisão do Chinook** (1,76 s dos 3,3 s): caminhos recalculados por
  orçamento; montar uma vez e reusar.
- **Plano B do courier** (laço da prioridade 1 em `Transportador.Air.cs`) — refaz o trabalho do
  MelhorDesembarque; aposentar depois de mais testes sem ele aparecer.
- **Degrau "pousa agora, anda dois turnos"** entre o Tático e o Operacional — o Operacional
  escolheu LZ de dois turnos de heli e parou na frente da AAA. Jogada, não viagem: depois.
- **Custo do MelhorDesembarque** (385 LZs avaliadas): podar LZ sem vizinho na faixa de nenhum
  passageiro, como no courier.
- **Avião Tanque** sem pré-compra (decisão do autor); demanda do caminhão pela mesma régua do
  vaivém (decisão do autor).
- **Delta de 3 SAM** explícito: não implementado; o guarda-chuva faz de teto. Se a base lotar, entra.
- **"Atraca" sem enxergar o inimigo** — resolvido em `e72deff`, mas não visto rodando.
- Carregados da v10.3.0: demanda "bombardeiro → Caça A" agora existe pela vanguarda (só
  compilou); SAM bloqueado pela cadeia resolvido pela escada (só compilou); freio de conscrição
  pela fila de carona; `aiAttackOnlyListedTargets` sem leitor; Batalha com `isAI` 1 nos dois
  slots e `hardMode` 1.

## Docs de doutrina

Desatualizados
- `Assets/Scripts/Match/AI/3. Shopping/Shopping.md` §6 — hoje: retaguarda × vanguarda e
  guarda-chuva (**demanda vista**, compra só compilou); "vende" com cadeia de elite (**só
  compilou**); breakthrough pela classe Artillery com escala (**só compilou**); pré-compra da
  peça aérea que rompe a poupança (**só compilou**); vaivém aéreo → Avião Tanque (**só
  compilou**); Shopping Pressure lê a última compra (ferramenta). Carregado da v10.3.0: anel e
  tolerância do counter, outra ilha, anel antiaéreo por presa, escolta por
  `InterceptadorDeDisruptor`, teto com embarcados; e da v10.2.0 (escalada da AA, rompimento no
  tamanho da demanda, escolta segura o caixa, folga 55/45, piso da fila de carona).
- `docs/AI Behavior/Transporte.md` — entrega pela banda Operacional para qualquer carga
  (**visto** no aéreo; APC/naval não vistos); não atraca com inimigo em cima (**só compilou**);
  MelhorDesembarque conta as recusas. Carregado: entrega do heli na faixa do passageiro
  (f694a24), fila de carona como piso de Chinook, ganância no desembarque, corrida no
  MelhorEmbarque, LZ de Chinook pousado.
- `docs/AI Behavior/Capturador.md` (C6, ferido na vanguarda, ~438) — passa a valer na Fase 2 só
  agora (`e72deff`, **só compilou**); espera de carona fora da produtora em todas as bandas e
  herdeiro do blitz revalidado (`1ab2a60`, PASS em teste de Editor, não visto em Play).
  Carregado: swap pela régua `RodadasAteFechar`, blitz.
- `docs/AI Behavior/Papeis.md`, `ficha_do_papel.md`, `Assalto.md`, `Vigilancia.md` —
  carregados da v10.3.0 (Interceptador em dois, Caça F e BF sem Disruptor).
- Escala de capturadores por prédio — continua **sem doc** (lugar natural: `Capturador.md`).
- Carregados sem mudança: `docs/AI Behavior/FireSupport.md` (AA rogue olha alvo conhecido),
  `docs/magnetic_tabela.md`.

Prontos para consolidar
- Nenhum. Shopping e transporte mexeram de novo hoje, e a Fase 2 mudou o terreno sob as
  políticas de linha — tudo que delas depende volta a ser não visto.
