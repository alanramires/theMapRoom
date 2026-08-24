# v8.4.1 — O recorte aprende que a peça tem lado

> O quadrante já copiava as coisas certas para os lugares certos. Faltava
> perceber que uma delas **aponta para algum lugar** — e que copiar sem o lado
> não é copiar.

Duas frentes correram em paralelo hoje: o recorte do quadrante (aqui) e a cena
de seleção de campanha (a frente paralela, briefada em `docs/Planos/briefing_cena_campanha.md`).
As duas se encontraram num ponto que ninguém planejou, e esse encontro é o
achado do dia — está na seção final.

---

## 1. O quebra-mar saiu aleatório, e a causa não era o recorte

O sintoma foi visual e imediato: as peças do quebra-mar apareceram nas células
certas, apontando para todos os lados.

A causa não estava em nada que tivesse sido escrito para o recorte:

```csharp
Tilemap.SetTile(cell, tile);   // copia o tile e DESCARTA rotação, espelho e cor
```

A Unity guarda a transformação de cada célula em **arrays paralelos**
(`m_TileMatrixArray`), não dentro do tile. E o quebra-mar é exatamente o tipo
de arte que depende disso: **um sprite só, girado de 60 em 60 graus** para
acompanhar a costa.

O dado do mapa de teste explica por que isso nunca tinha aparecido antes:

```text
quebraMar     39 células   →  10 matrizes distintas
terreno     1800 células   →   1 (identidade)
```

Hexágono de planície fica igual girado. Peça de quebra-mar não. O recorte de
terreno vinha funcionando havia dias justamente porque o terreno é o caso em
que o defeito é invisível.

**Guardado como paleta, não como valor por célula** — 10 orientações para 39
marcas, que é a forma da própria Unity (`m_TileMatrixArray` tem contagem de
referência). E guardado como as **4 colunas cruas**, não decomposto em
posição/rotação/escala: espelhar é escala **negativa**, e `Matrix4x4.lossyScale`
não devolve o sinal de forma confiável. Recompor de um TRS desespelharia a peça
sem erro nenhum.

Um segundo detalhe que só apareceu ao escrever a volta: um tile pode declarar
`LockTransform`, e nesse caso o tilemap **ignora** `SetTransformMatrix` calado.
Por isso o build destrava a célula antes de orientar. Como o valor assado é o
**efetivo** (lido da cena de autoria já com os locks dela aplicados), destravar
e reaplicar reproduz o que foi desenhado, não outra coisa.

### A camada também virou esparsa

Enfeite é ~2% denso e a camada cresce com o **mundo**, não com o quadrante. O
array do retângulo seria ~98% nulo, multiplicado por quadrante *e* por camada.
Agora cada quadrante leva só as marcas que caem dentro dele, e buraco não ocupa
lugar.

⚠️ `quebraMar` **não é rótulo livre**. A memória de névoa fotografa três coisas
— hexágono, construção e quebra-mar — e o nome está fixo em
`MatchController.RenderFogBreakwaterMemory`. Camada com outro nome é copiada
pelo recorte mas **não** é fotografada: aparece onde está visível e some onde
está só explorado, sem erro.

---

## 2. Rotas: o único bake que **parte** o que copia

Construção e enfeite se recortam por pertencimento — a célula está dentro, vem;
está fora, fica. Rota não. Rota é **sequência ordenada**, e o autor não desenhou
um conjunto de células: desenhou uma sucessão de arestas.

Os três consumidores leem **pares consecutivos**. Tirar uma célula do meio
**cola** as duas vizinhas dela numa aresta que ninguém traçou:

| consumidor | o que a aresta falsa produz |
|---|---|
| visual | `CreateRouteSegments` estica um sprite de centro a centro sem conferir adjacência — estrada reta atravessando o vazio |
| movimento | se a rota sai do retângulo e volta por uma célula **vizinha** da que saiu, o par colado é adjacente de verdade: atalho de estrada pela quina, com bônus |
| topologia | `BoardTopologyIndexBuilder` e `SectorManager` leem as mesmas rotas |

O segundo é o perigoso: não aparece na tela, não loga, só faz a IA e o jogador
andarem mais rápido por onde não há estrada.

**Quebra em buraco também**, por um motivo separado que só apareceu ao ler o
código: `IsRouteValid` é **tudo-ou-nada** — uma única célula inválida descarta o
desenho da rota **inteira**. Uma rodovia atravessando um hex vazio sumiria por
completo em vez de aparecer partida em duas.

Não precisou de geometria de hexágono: a quebra é sempre "perdi uma célula", e
as arestas são as do autor, não adjacência calculada.

### O descasamento de catálogo que o recorte atravessa de propósito

As duas cenas apontam para `StructureDatabase` **diferentes**:

```text
autoria   Fixture.asset          só Rodovias
Batalha   All Structures.asset   Rodovias, Trilhos, as duas pontes
```

Funciona porque o assado guarda o **id** e o build resolve no catálogo da cena
de destino, recarimbando o dono. É a mesma escolha do `ConstrucaoAssada`, e é o
que faz "onde a estrada está" ser cópia por quadrante enquanto "o que uma
estrada é" continua uma só.

A consequência aceita: enquanto a cena de autoria apontar para `Fixture.asset`,
não dá para desenhar Trilhos lá. Não é bug do recorte; é o padrão velho de "um
catálogo por mapa" sobrevivendo no nome.

---

## 3. O `Build` deixou de rodar fora do Play

Ao fechar o guarda das rotas, ficou claro que o escopo era maior do que o
levantamento inicial. **Tudo** que o `Build` escreve é serializado:

```text
map.SetTile(...)        tilemap        → serializado
BuildConstrucoes(...)   GameObjects    → serializados
BuildCamadas(...)       tilemaps       → serializados
BuildRotas(...)         roadRoutes     → serializado
```

Construir pelo menu de contexto fora do Play e salvar gravaria o layout de **um**
quadrante dentro da `Batalha`, que é uma cena só para todos. E o modo de falha é
o silencioso do `CLAUDE.md`: só dá erro onde as coordenadas não existem no outro
quadrante — e Q1 e Q2 compartilham faixa de x.

Guardar só as rotas teria dado a sensação de resolvido protegendo um quarto do
problema, que é pior que não guardar.

Verificado antes de escrever: a `Batalha.scene` está **limpa** — 0 tiles, 0
construções, 0 rotas. O guarda entrou antes do primeiro estrago, não depois.
`LimparTabuleiro` existe como saída caso aconteça, e varre também as camadas
decorativas, que são tilemaps **irmãos**: `ClearAllTiles` no tabuleiro não
encosta nelas.

---

## 4. Terreno em JSON, para uma IA externa desenhar mapa

Pedido do autor: um formato que qualquer IA externa leia, aprenda e devolva.

**Sem tabela de tradução.** O `paletteTile` já é o dicionário, nos dois
sentidos — `tile → TryGetByPaletteTile → id` e `id → TryGetById → paletteTile`.
É a mesma razão pela qual o bake guarda `TileBase` direto: uma tabela no meio só
criaria uma segunda fonte para divergir. O vocabulário sai do `TerrainDatabase`
da cena, com o símbolo derivado da primeira letra do id — uma sexta paleta entra
sozinha.

**A parte difícil não é o texto, é o hexágono.** Um array de linhas sugere grade
quadrada, e não é. A convenção foi determinada **empiricamente**, não de memória:
células consecutivas de uma rota são adjacentes por definição, então o traçado
desenhado pelo autor serve de oráculo.

```text
odd-r   (linha ÍMPAR deslocada)    33/33 pares    consistente
even-r  (linha PAR deslocada)      24/33          falha
```

A regra e os seis vizinhos por paridade vão **dentro** do arquivo, porque tratar
como grade quadrada produz costa serrilhada e rio partido — e o mapa carrega sem
erro nenhum, só fica errado.

**Na volta, o catálogo manda, não a legenda do arquivo.** Um documento pode
voltar meses depois com um terreno renomeado. Símbolo fora da legenda **recusa o
import inteiro** antes de tocar no tilemap: aplicar meio mapa e só então falhar
deixaria a cena num estado que ninguém pediu.

O botão existe em dois lugares e não são duplicata: no corpo comum do nó (serve
bloco, campanha e quadrante pelo `INoDoMapa`) e na **caixa desenhada** — este
último porque antes do primeiro bloco existir não há nó nenhum, e um mundo novo
não teria como sair.

---

## 5. `idSerial`: identidade estável nos três níveis

Pendência antiga que tinha prazo: **antes de existir o primeiro arquivo de
progresso**. O prazo venceu hoje (seção 7).

```text
id         "A_IA_Q1"    texto livre — é o que aparece no rótulo do mapa
idSerial   #7           atribuído na criação, cinza, nunca reusado
```

O `id` é um `TextField` e **vai** ser renomeado. Hoje é ele que o progresso e o
grafo de `destravadoPor` endereçam, e renomear quebra os dois em silêncio: o
`TryGet` não acha, o quadrante lê como neutro, o portão não abre.

**A regra que difere das unidades.** O `UnitSpawner` recalcula o contador a
partir do maior id em uso (`SetNextIdAfterMax`) e está **certo**: lá os ids
morrem com a partida. Aqui o registro sobrevive ao nó — apagar o último
quadrante e criar outro devolveria o serial do morto, e um arquivo de progresso
antigo grudaria a marca de um lugar noutro. Por isso `GerarSerial()` **só sobe**.

Ficou nos **três** níveis, não só no quadrante, porque `destravadoPor` está no
`INoDoMapa`: bloco e campanha têm o mesmo id editável e o mesmo grafo frágil.
Meia migração de endereço é pior que nenhuma.

Selagem por **varredura**, não por nó criado: são quatro caminhos de criação
(três botões mais o divisor em lote), e chamar `RepararSeriais` depois de cada
um custa nada e cobre um caminho que eu venha a esquecer. Ele também desfaz
duplicata de copiar/colar, em que o clone herdaria o progresso do original.

---

## 6. Rótulos do Scene

Bloco e quadrante são **lugares** e ganharam o nome centrado no próprio
retângulo; campanha é **organização** dos quadrantes e ficou no canto. Distinção
do autor, e ela se sustenta sozinha.

Mostra o **nome**, não o id — mas leva o id na linha pequena, porque é ele que
se digita no `QuadranteController` e o que aparece no erro quando o endereço não
casa. Sair do canto para o centro não podia custar o endereço.

Centrar de verdade exigiu espaço de tela (`Handles.BeginGUI` + `WorldToGUIPoint`
+ `CalcSize`): `Handles.Label` ancora pelo canto, e o `alignment` do estilo não
tem retângulo para agir dentro.

---

## 7. O encontro das duas frentes — e a colisão

A frente paralela entregou, entre outras coisas, **exatamente o passo 0a do
plano**: `MatchController.OnMatchConcluded(vencedor, derrotado, motivo, turno)`,
publicado de dentro de `HandleVictoryAestheticPresentation`.

**A armadilha do plano está fechada, e foi verificada, não aceita de palavra:**
`ImportVictoryState` não chama o funil, então carregar um save de partida
concluída não registra outra vitória. Confirmado nos seis pontos de chamada.

Isso destrava o passo 6 (progresso), que o plano dizia depender de 0a.

Mas o `CampaignProgressStore` que veio junto **diverge do que o plano fechou em
três pontos**, e nenhum deles é bug — são decisões de design que precisam ser
tomadas:

```csharp
public string quadranteId;      // endereça pelo texto EDITÁVEL
public int ownerTeamId;
public int lastTurn;            // sobrescrito sempre, e mora junto do dono
```

| # | divergência | por quê importa |
|---|---|---|
| 1 | endereça pelo `id` editável | é exatamente o que o `idSerial` desta versão existe para evitar. Agora é barato de consertar |
| 2 | `lastTurn` é *último*, não *melhor*, e mora junto do dono | o plano separa `dono` (estado, muda sempre) de `marca` (registro, nunca piora). Colapsados, perder o quadrante apaga que você o tomou em 11 turnos |
| 3 | `schemaVersion` | inofensivo, mas nada foi distribuído — migração de save não compra nada hoje (`CLAUDE.md`) |

O item 2 é o que eu trataria primeiro: é decisão, não conserto.

---

## O que NÃO terminou

| pendência | estado |
|---|---|
| **reassar os 4 quadrantes** | o asset tem `transformacoes: 0` — o bake do quebra-mar rodou **antes** do conserto de orientação. Até reassar, o enfeite continua torto |
| **selar os nós** | `idSerial` ainda não aparece no asset. A bancada vai mostrar "Selar 6 nó(s)" — precisa de um clique |
| **nada foi compilado** | o código de hoje foi escrito contra as APIs lidas, sem passar pelo compilador da Unity |
| **rota partida não foi exercitada** | as 3 rotas do mapa caem inteiras em seus quadrantes; a lógica de split está escrita e revisada, mas nenhum dado a exercita. Só uma rota que saia e volte prova |
| **unidades iniciais** | `bakedUnidades` não existe. É o último item da comparação com `Battle Map 1 - Ground` |
| **`destravadoPor` ainda guarda texto** | o `idSerial` é o pré-requisito e agora existe; a conversão das listas é frente própria |
| **`sceneLoaded` nos 4 managers** | passo 0b do plano, intocado |
| **`.vscode/settings.json`** | voltou a apontar para `.slnx`, desfazendo o commit `769a3dc`. O `.sln` é gerado pela Unity e não existe no disco agora. Deixado fora dos commits por decisão do autor |

---

## Onde eu errei

**Subestimei o escopo do guarda.** Ofereci proteger só as rotas contra escrita
no Editor. Ao ir escrever, o `Build` inteiro serializava em quatro lugares. Se
eu tivesse entregue o que ofereci, teria protegido um quarto do problema e
removido o incômodo que faria alguém olhar de novo.

**Errei a colocação dos botões de JSON.** Coloquei no corpo do nó, que serve os
três níveis — e é bom — mas deixei sem saída a caixa desenhada, que é o mapa
inteiro e o único caso que existe antes de haver qualquer nó. O autor foi
procurar exatamente ali.

**Sugeri contaminação onde havia só trabalho não feito.** Ao ver o quebra-mar da
`Fixture` com as mesmas 39 células do `Battle Map 1 - Ground`, levantei herança
da duplicação de cena. Era só o autor não ter desenhado o resto ainda.

**Escrevi um fallback para uma API que não existe.** `ResolveTerrainDatabase`
caía em `CursorController.TerrainDatabase`; o `CursorController` não expõe
terreno. Pego antes de compilar, mas só porque conferi — não porque desconfiei.
