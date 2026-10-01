# v8.0.0 — A ausência precisa de nome próprio

Primeira versão do major **v8 — onde o dado mora**. Duas frentes independentes
caíram na mesma doença, e foi isso que abriu o major: um valor compartilhado
respondendo no lugar da ausência, e um catálogo respondendo no lugar da cena.

---

## 1. O fio

Todo defeito do dia tem a mesma forma. Não é cálculo errado — é **falta de um
nome para "nada aqui"**, e o vizinho mais próximo assume a vaga:

```text
default(ConstructionSector)     "nenhum setor"   respondia ALPHA
rota de outro catálogo          "não se aplica"  saía como ERROR
bucket de rota vazio            "não migrei"     igual a "não tem estrada aqui"
PodeSuprir valid=[] invalid=[]  "não se aplica"  igual a "não posso"
StructureData.roadRoutes        "onde está"      morando dentro de "o que é"
```

Cinco lugares, um padrão. E o padrão já tem nome no projeto — o **gate
inaplicável**, que a `v7.2.1` aplicou ao `QueroCarona`. A novidade é que ele
não era do transporte: é do jogo inteiro.

---

## 2. Frente — o layout saiu do catálogo

### O que estava errado

`StructureDatabase` carregava uma seção `Road Routes (Map Scope)` com **listas de
células absolutas**. O catálogo é compartilhado por todos os mapas; o layout é de
um tabuleiro só. Cinco leitores de runtime consumiam dali, dois deles em regra de
movimento (`UnitMovementPathRules`), além de `SectorManager` e do índice de
topologia.

A consequência barulhenta foi um mapa de treino 12×12 nascer com **289 erros de
validação** de rotas que nunca foram dele. A consequência silenciosa é pior, e é
a que justifica o major:

> Deu erro só porque as coordenadas do mapa grande não existem no 12×12. **Dois
> tabuleiros com faixas de coordenada parecidas aplicariam as estradas um do
> outro sem um único aviso.**

### O que foi feito

O `RoadNetworkManager` — componente **da cena** — passou a ser o dono do layout,
e ganhou o **ponto único de leitura** `GetRoadRoutes(structure)`. A cadeia de
compatibilidade é resolvida **uma vez** no lookup, não a cada consulta (o custo
de movimento pergunta rota célula a célula):

```text
1. bucket da CENA
2. (só enquanto routesMigratedToScene == false)  catálogo
3. (só enquanto routesMigratedToScene == false)  StructureData legado
```

Os quatro chamadores de runtime passaram pelo manager. A cadeia de fallback, que
estava **copiada nos quatro**, sumiu dos quatro — quatro cópias de uma regra são
quatro chances de divergir.

O flag `routesMigratedToScene` existe para separar *"vazio porque não migrei"* de
*"vazio porque **aqui** não tem rodovia deste tipo"*. É o mesmo remédio do fio do
dia, aplicado antes de o defeito nascer.

E a rota de outro catálogo agora **morre na origem**: o filtro por `ownerDatabase`
mudou para dentro do manager. Saiu junto o bloco do builder que avisava *"mantida
por compatibilidade"* e **depois validava as células dela mesmo assim**.

### A ferramenta de migração

Botão no inspector do `RoadNetworkManager`, com três garantias que nasceram de
errar duas vezes (§6):

- **copia, não move** — o catálogo não é tocado, e a cópia é profunda;
- **substitui, não empilha** — rodar duas vezes dá o mesmo número;
- **confere sozinha** — compara o que copiou com o que a cena passou a enxergar e
  emite `LogError` na divergência.

Todos os mapas foram migrados: Hot Seat 1, Battle Map 1, os demais e os
tutoriais. O treino fechou em **0 erros, 0 warnings**.

### O critério de aceitação, e ele é um gesto

> Duplicar uma cena, apontar os catálogos, e o mapa novo nascer **vazio**.

Foi o que aconteceu com o `Hot Seat 0 - Treino`. De brinde, o `SectorManager`
passou a enxergar `bases=1` onde via `bases=0`: sem rota fantasma na topologia, o
QG virou base de verdade.

---

## 3. Frente — `default` não é "nenhum" *(do autor)*

`default(ConstructionSector)` é **Alpha**, porque Alpha vale `0` e `None` vale
`-1`. Renumerar quebraria toda cena e save já gravados, então a regra passou a
ser explícita: *"sem setor" se escreve `ConstructionSector.None`, sempre — em
campo, em retorno e em comparação.*

Três parentes, todos silenciosos:

```text
!= default como "tem vizinho"    apagava Alpha do grafo de setores
enumValueIndex em editor         gravava o setor VIZINHO e mostrava o rótulo certo
                                 (um QG marcado "Base0" foi parar em Omega)
cast de int cru vindo do save    inventava objetivo que o planner depois perseguia
```

O conserto: `ConstructionSectorHelper.IsRealSector`, editores lendo e gravando
`intValue`, e `ObjectiveManager.ReadSavedSector` devolvendo `None` para valor que
não existe mais no enum — o mesmo cuidado que `objectiveType` e `rallyState` já
tomavam.

Verificado em jogo pelo autor: Base0 e Alpha aparecem onde devem.

---

## 4. A dívida de verificação da v7.2.1 foi paga

O resumo da `v7.2.1` listava treze itens sem validação em jogo. A sessão de F11
do turno 1 fechou os principais, e o maior deles era hipótese pura:

```text
item 11   hash de reivindicação estreitado
          previsão: QueroCaronaCacheHits 1 → ~16, queroCarona → perto de zero
          medido:   16 acertos em 16 chamadas, 814,1ms → 2,3ms
          decisão do Chinook #85: 1250ms → 276ms
```

O teste é limpo porque o Chinook #86 **agiu** entre as duas decisões e, não sendo
capturador, não entra mais no hash.

Também quitados: compilação das cinco frentes pós-tag (item 4 e 10), os números
do #86 (`MelhorEmbarquePassengers:17`, `Pairs:289`), e nenhum `[FilaCarona]`
citando caça — a fila do turno 1 é só Bazooka, Metranca e Soldado.

**Efeito colateral não previsto, e vale mais que o previsto:**
`MovementQueryCachesBuilt` caiu de **747 para 1** entre o primeiro e o segundo
transportador. Os caches não vinham de helper por caminho, como eu tinha
apontado: vivem na cadeia `queroCarona → melhorCaptura → reach de captura`. O
primeiro transportador do turno ainda paga a varredura inteira; os seguintes
pagam zero.

---

## 5. Desenho — dois contratos novos, nenhum código

### `contrato_recencia_de_cobertura.md`

Nasceu de um censo: **nove unidades de vigilância, turno 1, com número**. O
diagnóstico coube em duas linhas da mesma classe de navio:

```text
Fragata #79   hold   vis=58  marginal=38  novo=0   →  gain 1,9
Fragata #84   move 5 vis=46  overlap=39   novo=7   →  gain 137,5
```

`unexploredMarginalWeight: 25f` responde por ~98% do score. A moeda é **névoa**,
não contato — e névoa não regenera. Com o mapa explorado, `novo → 0` para todos
os caçadores ao mesmo tempo e todos congelam no estado da #79.

O contrato fecha: ledger `[slot, perfil, célula] = últimaRodadaObservada`
(carimbo, não contador — nada envelhece), chave reusando o predicado de
equivalência **que já existe** (`IsEquivalentSurveillanceObserver`), escrita só
após compromisso em `Neutral`, e *"preto"* significando **nunca coberto pela rede
de detecção naquela camada** — não FoW geográfico preto.

Decisão de doutrina do autor: **aérea repele e tem capitão; naval não faz nem uma
coisa nem outra.** Logo `overlap` negativo só na perna aérea.

### `contrato_missao_captura.md`

E aqui o achado que inverte o plano: **metade já existe.**
`DesignatedCaptureTarget` está em `UnitManager` como `[SerializeField]`, no DTO
do save, restaurado pelo `SaveDataMapper`, com `pending`/`commit` correto e cinco
condições de baixa — **só no caminho rebelde**. O *"salva, fecha, abre e o cara
que ia pro norte continua indo pro norte"* já funciona hoje.

O trabalho não é construir: é **promover à camada compartilhada** e parar de ter
três representações da mesma coisa.

---

## 6. Onde eu errei

**Afirmei que o `ConstructionDatabase` estava limpo.** Conferi **um** consumidor
— o builder de topologia itera `ConstructionManager` da cena — e generalizei. A
tela do inspector mostrou uma seção `Field Entries (Map Scope)` que eu não tinha
procurado. Inferência errada: um segundo armazenamento pode existir ao lado do
primeiro, e checar um leitor não prova nada sobre onde o dado mora.

**A migração copiou 23 rotas para lugar nenhum.** `GetOrCreateRoadRoutes` chamava
`EnsureRoutesLookup()`, que **antes** da migração preenche o lookup com listas
temporárias montadas a partir do catálogo. Ele devolveu uma dessas, e as cópias
foram para um objeto que ninguém serializa. O log dizia `rotas=23` e o relatório
seguinte dizia `total=0`.

**E depois copiou em dobro.** A segunda passada empilhou em vez de substituir:
23 → 46. Duas falhas opostas, o mesmo furo — a ferramenta não conferia o próprio
resultado. Agora confere, e as duas teriam parado ali.

**Propus `EnableLos = false` na requisição da vigilância.** Teria desligado a LoS
do radar junto, porque aquele campo é o **toggle global da partida**. A ficha já
tinha o mecanismo certo — `DetectionMethod.Propagated` e `ResolveLosValidationFor`
—, isto é, "LoS é fallback" já estava implementado por par (domínio, altura).

**Propus penalizar `overlap` nas duas famílias.** Errado para naval: sonar
sobreposto entre submarinos que navegam juntos é legítimo.

**Propus memória de missão para o EWACS**, espelhando o Fire Support. Desnecessário:
o Fire Support lembra porque o passageiro embarcado não é observável da posição;
o contato na rede é observável toda rodada. Sem estado, não existe o sensor
trancado perseguindo fantasma.

---

## 7. O que não terminou

**A limpeza da origem das rotas.** A seção `Road Routes (Map Scope)` continua no
`StructureDatabase`, e `StructureData.roadRoutes` e
`RoadRouteDefinition.ownerDatabase` continuam vivos. Todos os mapas já carregam o
flag, então a limpeza está destravada — mas é passo próprio, e o `ownerDatabase`
só pode sair depois dela (ele existe para desambiguar catálogo; com rota na cena
não sobra o que desambiguar).

**O `fieldEntries` do `ConstructionDatabase`.** Mesma doença, e barata: **zero
leitores de runtime** — só `ConstructionDatabaseEditor` e
`ConstructionPainterWindow`. O jogo instancia construção pela cena, não pela
seção. É autoria no asset errado. O autor quer a separação em
`constructionField` / `constructionData` / `constructionDatabase`.

**O teste do capturador nunca rodou.** Save, fechar o jogo, abrir, e conferir se
o soldado sai de (4,0) rumo a (0,0) com o mesmo `DesignatedCaptureTarget #2`. São
dois F11 e decidem se o trabalho da alocação pegajosa é construir ou promover.

**`[FoW][RoundZeroBake] restored=1/2`.** Um slot segue rejeitado e o motivo está
calado — `enableFogValidationLogs` continua desligado, e a linha
`rejected=<motivo>` existe.

**Nenhum dos dois contratos novos virou código.** São desenho, marcados linha a
linha com `HOJE` / `CONTRATO` / `ABERTO`.

**A frente dos managers globais nem começou.** Seis já são `DontDestroyOnLoad`,
cinco são singleton por cena. E há a suspeita inversa: o `ObjectiveManager` é
global **sem gancho de `sceneLoaded`** — falta conferir quem limpa o plano entre
mapas.

---

## 8. A doutrina que entrou no `CLAUDE.md`

Seção nova: **os três andares**, e a direção do conserto sendo sempre *para
cima*.

```text
global      manager DontDestroyOnLoad    serviços e estado entre partidas
catálogo    ScriptableObject             o que EXISTE: tipos, custos, chaves
cena        objetos da cena              o que está EM CAMPO: instâncias e layout
```

> *O catálogo diz o que uma coisa **É**. A cena diz **onde ela ESTÁ**.*

Com o teste de aceitação (duplicar cena → mapa vazio) e o aviso de que **o modo
de falha aqui é silêncio, não barulho**.
