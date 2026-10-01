# v8.3.0 — Três formas erradas até o dado caber, e o primeiro quadrante em 2 ms

Fechada em 2026-08-14. Antecessora: [`v8.2.2`](relatorio_v8.2.2.md).

---

## O fio do dia

O plano da `v8.2.2` dizia que a campanha tinha 1120 linhas de desenho e zero de
código. Hoje ela tem código, e ele funciona:

```text
[Quadrante] 'campanha A1/QA1' construido: 361 tiles, 0 buraco(s), 19x19
            origem local 0,0 · origem de autoria -18,-9 · em 2 ms
```

Uma cena de batalha **vazia** recebeu um endereço, leu o que estava assado e
desenhou o tabuleiro. É o recorte provado — e a prova não precisou de partida,
nem de QG, nem de unidade.

Mas o fio de verdade é outro, e é desconfortável: **a forma do dado esteve errada
três vezes, e as três correções vieram do autor.** Está escrito abaixo com
detalhe porque é o tipo de coisa que a próxima sessão repete se não estiver.

---

## Frente A — O dado da campanha

Seis arquivos em `Assets/Scripts/Campanha/`. A forma final:

```text
MundoData (ScriptableObject)      UM asset por cena de autoria
 └─ BlocoData      [Serializable]   Europeu · América do Norte · Rússia
     └─ CampanhaData [Serializable]   Europa · África
         └─ QuadranteData [Ser.]        Inglaterra · Congo...   ← assado e jogado
```

Tudo inline num asset só. Bloco, campanha e quadrante são **retângulos com
destrave**, e implementam `INoDoMapa` — o que faz a bancada ter um renderizador
de nível em vez de três quase iguais.

### O destrave: um mecanismo, três níveis

O autor descreveu três requisitos que pareciam diferentes:

```text
quadrante   "completar os demais quadrantes"          (last map)
campanha    "complete outra campanha"
bloco       "complete as campanhas do bloco X"
```

Eles colapsam num campo só — `destravadoPor: List<string>` — assim que
"concluído" é definido recursivamente:

```text
quadrante concluído  =  venci ele
campanha  concluída  =  todos os quadrantes dela concluídos
bloco     concluído  =  todas as campanhas dele concluídas
```

Aí *"complete as campanhas do bloco X"* vira `destravadoPor = ["X"]`. Sobra só o
caso "todos os meus irmãos", que virou o flag `exigeIrmaos` — e é flag em vez de
lista porque listar irmãos na mão **quebra em silêncio** no dia em que se
acrescenta um.

É a mesma economia da habilidade-como-chave: não se inventa mecanismo por nível,
define-se o que "concluído" significa e todo mundo lê.

⚠️ **A avaliação não existe** — só o dado. Ela só serve quando houver progresso
salvo, e não há.

### ⚠️ As três formas erradas, e por que eu defendi cada uma

Isto é o registro que mais importa deste relatório.

**Primeira forma — `CampaignData` como asset separado.** O autor apontou que ele
"perdeu o sentido de existir". Eu concordei, propus colapsar, e então **medi** o
padrão da casa (`UnitDatabase → UnitData`) e **desdisse a mim mesmo**, defendendo
manter o asset.

**Estava errado, e o erro foi de método:** apliquei a regra sem checar a razão
dela. `UnitData` e `ConstructionData` são assets próprios porque são
**compartilhados** — um Soldado é referenciado por vários mapas. Campanha
pertence a um mundo só. O padrão não se aplicava.

E o custo não era estético: enquanto campanha fosse asset, existia a
possibilidade de "campanha solta, fora da lista" — que é **exatamente** o que já
tinha custado tempo naquele mesmo dia, quando o `QuadranteController` não achava
nada porque o asset existia e a galeria estava vazia. Inline, isso é impossível
por construção em vez de detectado por aviso.

**Segunda forma — `MundoData` sem blocos.** O autor descreveu Oceania, Metallion
e Equilibrium como "campanhas". Eram **mundos**. E Europa/África eram campanhas
de um mundo. Faltava um nível inteiro, e ele só apareceu quando o autor descreveu
o destrave entre continentes.

**Terceira forma — um mundo por cena.** Eu defendi que mundos não são contíguos e
por isso moram em cenas separadas. O autor corrigiu: é **uma cena com o mundo
inteiro**, fatiada em blocos. E aí a contiguidade vale em todo lugar — Europa
encosta na África *e* na Rússia — que é o que faz o mapa acender por região.

**O padrão dos três erros é o mesmo:** eu generalizei de um caso conhecido sem
verificar se a razão do caso valia aqui.

---

## Frente B — O `QuadranteController`

`Assets/Scripts/Campanha/QuadranteController.cs`. Recebe
`(campanhaId, quadranteId)`, resolve no `MundoData` e pinta.

### `DefaultExecutionOrder(-9000)` não é cosmético

O mais baixo do projeto era `-8500`. O pintor precisa rodar **antes de tudo**
porque quem consultar o tabuleiro antes da tinta secar recebe resposta vazia — e
o `SectorManager`, que se reconstrói sozinho na primeira consulta, **assa o vazio
e cacheia**. Sem erro nenhum no console; só um plano degenerado.

O log do teste confirma que a ordem segurou: `[Quadrante]` aparece **antes** de
`[BoardTopology]` e `[SectorManager]`.

Existe também `QuadranteController.BoardReady` — o portão. **Ninguém o lê ainda**,
e isso é deliberado: a cena de batalha sempre nasceu pronta, então a garantia era
acidental. Com a cena vazia ela desaparece, e o portão é o que precisa
substituí-la. Ligar os consumidores é trabalho de outra etapa.

### O bake guarda `TileBase`, não id de terreno

Porque o jogo já resolve terreno a partir do tile
(`TerrainDatabase.TryGetByPaletteTile`). Uma tabela de tradução no meio só criaria
uma segunda fonte para divergir.

### O diagnóstico foi reescrito depois de falhar

A primeira mensagem de erro era `"Endereco nao resolve: campanha 'fixture',
quadrante 'Q1'"` — e mandava adivinhar qual das duas metades falhou. Agora ela
separa as duas e **lista o que existe**, marcando quadrante sem bake. A resposta
estava sempre a um passo de distância e a mensagem não a dava.

---

## Frente C — A bancada, reescrita duas vezes

`Assets/Editor/MapHelperWindow.cs`: de 880 para 1562 linhas.

Ela deixou de ser "ferramenta de ver o mapa" e virou **o editor de campanha**:
criar o mundo, criar/remover blocos, campanhas e quadrantes, desenhar cada um por
dois cliques, assar, e salvar.

**Três validações que nasceram de erros reais do dia:**

| validação | o erro que a motivou |
|---|---|
| id repetido nos três níveis | `TryGet*` casa por string e devolve o **primeiro**; o segundo existe no asset e é impossível de carregar |
| cena de autoria ≠ cena aberta | assar da cena errada grava tiles nulos, e o sintoma parece "bake não rodou", não "bake rodou no lugar errado" |
| alterações não gravadas | renomear marcava `dirty` e não gravava — o `.asset` em disco e a tela divergiam em silêncio |

A terceira apareceu porque a tela mostrava `Lago C` e o arquivo dizia `Q3`. Só o
bake chamava `SaveAssetIfDirty`.

**A árvore editada é a do asset, direto.** Não existe cópia dentro da ferramenta:
cópia criaria a divergência "desenhei o retângulo mas esqueci de assar".

---

## Frente D — A Faxina de Cena, e uma correção minha

`Assets/Editor/SceneSanitizerWindow.cs` (commitada na `v8.2.2`, mas o achado é de
hoje).

Duplicar uma cena de mapa para criar a de autoria trouxe **46 rotas de estrada de
outro mapa**. O autor reagiu com *"eu já falei que essas coisas são
desacopladas"* — e o diagnóstico certo é o inverso: as rotas estarem na cena **é
o desacoplamento funcionando** (`routesMigratedToScene: 1`). O que faltava não era
desacoplar mais, era existir uma operação de **esvaziar o tabuleiro**.

⚠️ **Correção de afirmação minha:** eu disse que as rotas estrangeiras "viram
estrada de verdade no mapa". **Falso.**
`RoadNetworkManager.IsRouteAllowedForCurrentDatabase` (`:361`) as filtra por
`ownerDatabase`. Elas eram peso morto, com duas minas: rota com `ownerDatabase`
**nulo** passa como legado, e o bake do recorte, se ler a lista crua, pega tudo.

---

## Frente E — Autoria

`Autoria/Fixture.unity`: **703 → 950 tiles**, 41 GameObjects inalterados. Nasceu
uma ilha cercada de água a leste, e o mapa passou de 37 para 50 colunas.

`Mundo Fixture.asset`:

```text
bloco A   (-18,-9) 37×19      campanha A1     QA1 19×19 = 361 tiles
                                              QA2 19×19 = 361 tiles
bloco B   ( 18,-9) 14×19      campanha B1     QB1 14×19 = 266 tiles
```

### As costuras caíram na geografia, e isso não foi instruído

O autor pôs as emendas em cima de feições: **serra em `x=0`** (entre QA1 e QA2) e
**mar em `x=18`** (entre os blocos). O plano prescreve isso — *"a faixa de
fronteira quer ser geografia, não patrimônio"* — mas ele chegou lá desenhando,
não lendo.

O efeito é o do Game Boy Wars: quem joga QA2 vê a serra na borda oeste e o oceano
na leste, e entende que existe mundo dos dois lados sem que nada afirme isso.

### O serrilhado apareceu três vezes no mesmo dia

Onze buracos na borda direita (dez em `x=16`, **só nas linhas ímpares**); depois a
muralha com montanha só nas linhas **pares** em `x=1`. É a assinatura de pintar
"onde parece reto" numa grade hexagonal com linhas deslocadas.

É por isso que o contorno do Map Helper é desenhado **ligando os centros das
células da borda** — ele sai serrilhado de propósito. Um retângulo limpo mentiria
sobre a armadilha que a ferramenta existe para expor.

---

## Frente F — Áudio

`campaign_select.MP3` — música da tela de seleção de campanha, confirmada pelo
autor. `map_select.MP3` entra junto **por simetria de nome; não foi confirmado**,
e está marcado assim de propósito.

Pendência que nasce daí: o `MundoData` ainda não tem campo de música.

---

## O que não terminou

- **A `Batalha.unity` está commitada com um endereço que não resolve**:
  `campanhaId: fixture`, `quadranteId: Q1`. O teste rodou com
  `campanha A1` / `QA1` — o autor mudou no Inspector e não salvou a cena. É a
  primeira coisa a arrumar amanhã.
- **Só terreno.** Estruturas (a estrada) e construções não são recortadas nem
  pintadas. O teste de aceitação do plano — *a estrada entra na linha R nas duas
  bordas* — **não foi feito**, porque depende delas.
- **Metade do teste dá para fechar sem estrutura**: `QA1` mostra a serra e o passo
  na borda leste, `QA2` deve mostrar os mesmos na oeste, **na mesma linha local
  9**. Não foi verificado.
- **`BoardReady` não tem leitor.** O portão existe e ninguém o consulta.
- **A avaliação de destrave não existe.** Só os campos.
- **Identidade por string é frágil e vai quebrar.** O autor levantou: renomear uma
  campanha invalidaria save. A saída é um `guid` estável separado do nome, e o
  gatilho é claro — **tem que entrar antes do primeiro arquivo de progresso.**
  Depois disso custa migração; antes, custa dez linhas. O projeto já aprendeu essa
  lição no `ConstructionSector` (*"os ints são contrato de serialização"*).
- **`mundoId` está como `mundo fixture`, com espaço.** Não quebra nada hoje;
  incomoda quando o menu resolver mundo por id.
- **Nada foi medido em escala.** A bancada e o pintor rodaram sobre 950 células.
  Os três gates do rótulo por hexágono foram desenhados para dezenas de milhares.
- **As duas cenas de execução seguem fora do Build Settings.**
- **Os `FrameSpike` de 5 s e 14 s continuam sem explicação** — mas são
  **anteriores** a este trabalho: apareciam no Play da cena vazia. O pintor custou
  2 ms.

---

## Armadilhas que este dia acrescenta

| armadilha | regra |
|---|---|
| **aplicar o padrão da casa sem checar a razão dele** | `Database → Data` como assets vale porque `UnitData` é **compartilhado**. Campanha pertence a um mundo só. Generalizar sem verificar a razão errou três vezes hoje |
| **parse frágil de `.asset` virando afirmação** | errei a leitura do arquivo **três vezes** (`awk` cortando em espaço, indentação a mais do nível novo). Contagem por faixa de linha é confiável; `$3` não é |
| **`.asset` em disco tomado como estado atual** | edição no Inspector marca `dirty` e **não grava**. Disco e tela divergem até alguém salvar |
| **id repetido** | `TryGet*` devolve o **primeiro**. O segundo existe no asset e é inalcançável, sem erro |
| **assar com a cena errada aberta** | grava tiles nulos, e o sintoma parece "bake não rodou" |
| **consulta antes da pintura terminar** | `SectorManager` assa o vazio e **cacheia**. Daí `-9000` e o portão |
| **listar irmãos na mão** | quebra em silêncio quando se acrescenta um irmão. `exigeIrmaos` é flag, não lista |
| **identidade de serialização igual ao nome editável** | renomear invalida save. `ConstructionSector` já carrega esse aviso |

---

## Onde isso deixa a próxima sessão

A ordem do plano continua valendo, com uma etapa a menos:

```text
C1  seleção que só lê          ✅ feita (Map Helper)
C2  bake                       ✅ feito, só terreno
C3  pintor                     ✅ feito, e roda em runtime direto
►   estruturas e construções   ← a próxima
    TESTE DE ACEITAÇÃO         depende delas
C5  save dirige a pintura      depois
```

E os dois bloqueios da `v8.2.2` seguem intocados e válidos: **evento único de fim
de partida** e **`sceneLoaded` nos 4 managers**. Nenhum dos dois depende de
decisão em aberto, e a campanha vai encadear cenas.
