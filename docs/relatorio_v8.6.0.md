# v8.6.0 — a etiqueta muda de dono

O dia em que três coisas diferentes aprenderam a mesma lição. O rally, o eixo e a
dificuldade eram, cada um deles, **uma resposta só para uma pergunta que tinha
mais de um respondente**: um dono por rally, um leque por geometria, um modo
difícil que acendia seis comportamentos juntos.

A frase que organiza a versão saiu da boca do autor, olhando o desenho dos eixos
na cena de autoria:

> *"é como um grafo"*

E grafo não cabe em campo escalar.

---

## O fio do dia

A `v8.5.2` fechou o laço da campanha: a Batalha virou uma cena vazia que um
quadrante pinta. O que esta versão descobriu é o preço disso — **tudo que foi
desenhado quando cada mapa era uma cena ficou sem chão.**

O `SectorManager` identifica setor pelo nome do enum. Numa cena de partida isso
basta, porque existe um tabuleiro. Na cena de autoria existem quatro quadrantes
lado a lado, cada um com direito ao seu Alpha — e os dois Alphas caíam no mesmo
balde, com uma célula representativa no meio do caminho entre eles. Nenhum erro,
nenhum log: só eixos atravessando o mapa.

O mesmo padrão apareceu três vezes, e em nenhuma delas havia bug no sentido de
exceção:

```text
rally         um int  respondia  "de quem é"      → e o prédio era dos dois
eixo          ângulo  respondia  "por onde vou"   → e o autor queria escolher
dificuldade   hardMode respondia "o que eu faço"  → seis lâmpadas, uma chave
```

---

## Frente 1 — o rally passa a ter donos, no plural

O autor chegou num prédio que é o último ponto de reunião **dos dois lados** — o
centro de um mapa simétrico. `rallyOwnerSlotIndex` é um `int`: sabe responder um.

Existia um valor tentador, o `-1`, já usado como "sem dono explícito". Reaproveitá-lo
como "de todos" teria empilhado um segundo sentido sobre o primeiro — e os dois
já brigavam no código: `IsRallyOwnedBySlot` exigia igualdade exata (logo, `-1` não
era de ninguém), enquanto o portão da invasão tratava `-1` como *"pode ser meu"*.
Escolher um dos sentidos deixaria o outro errado.

Virou **lista de slots**, e cada portão passou a perguntar `IsRallyForSlot(slot)`.
A decisão de regra que sustenta isso é do autor:

> **Designação, não posse.** O inimigo capturar o rally não tira ele da sua lista:
> continua sendo *"onde eu paro antes de invadir"* — e é justamente por isso que a
> IA captura o rally antes de lançar a invasão, em vez de desistir dele.

A lâmpada do prédio é uma só, e aí veio a segunda regra, também do autor:

> *"se o rally é do inimigo, as cores dele nada significam pro outro slot; caso
> seja capturado, as cores são recalculadas pro novo dono."*

Então a leitura continua guardada **por slot** (os dois lados montam massa em
paralelo, sem se misturar), e a lâmpada mostra a do **slot que controla o prédio**.
Na captura, ela passa a ler a do novo dono sozinha — ninguém reemite nada.

Cenas antigas não perderam a calibração: o campo velho continua no arquivo,
escondido, e migra no `Awake` e no `OnValidate`.

## Frente 2 — o eixo vira grafo escrito

O `InvasionAxisMap` monta o leque por ângulo a partir dos rallies. Funcionava
quando o autor desenhava uma cena por mapa. Com o quadrante pintando uma cena
vazia, dois problemas ao mesmo tempo: o bake não levava a marca de rally, e o
autor não tinha como dizer *"não, o eixo é por aqui"*.

O achado que barateou a frente inteira: **eixo não é dado, é conta**. Ele já era
recalculado do zero a cada turno, a partir de QG + rally + rótulo de setor. Não
havia eixo guardado para migrar — bastava dar de onde ler.

`QuadranteData.eixos` guarda, por slot, a lista ordenada de setores; o último é o
rally. Quando o quadrante pintado tem eixo escrito, ele **manda**: sem ângulo e
sem override. Sem eixo escrito, nada muda — as cenas de mapa fixo seguem no
automático.

Guarda **rótulos, não coordenadas**, e é isso que permite morar no asset sem
virar layout de um mapa vazando no outro: "Alpha" só significa algo dentro do
quadrante que o lê. Cada quadrante repete Alpha–Zulu à vontade.

Autora-se pela ficha do prédio, que é onde o autor já está. O dropdown oferece
`E1..EN` **mesmo sem geometria nenhuma** — escolher um eixo que não existe é o que
o cria — e grava na lista do quadrante, que segue sendo a única verdade. O setor
entra ordenado pela distância ao QG; o que tem rally vai para o fim; eixo que
esvazia é removido, porque o número do eixo **é** a posição na lista, e buraco
faria o E3 do editor virar o E2 do jogo.

## Frente 3 — a autoria enxerga um quadrante por vez

Com o eixo escrito, o desenho do editor ficou mentindo de duas maneiras.

A primeira era o balde compartilhado descrito lá em cima. A correção foi na raiz,
e não no desenho: um **recorte de autoria** no `SectorManager` — construção fora
do retângulo do quadrante em foco não entra no rebuild. Agrupamento, representante,
vizinhos, distâncias e desenho passaram a enxergar um quadrante **sem nenhuma
dessas contas ser duplicada**. É estático, não serializado e ignorado em Play:
filtro ligado em partida apagaria meio tabuleiro sem erro nenhum.

A segunda era pior, e o autor a encontrou jogando:

> *"eu atualizei uma das construções pra outro eixo e ele ainda desenha como se
> fosse da antiga, mesmo eu reassando os trem"*

O desenho só sabia perguntar o eixo autorado ao **quadrante que a Batalha pintou**
— e na autoria nada está pintado. Ele caía sempre no leque por ângulo. Reassar
não tinha como ajudar: a lista de eixos é autoria e o bake nem toca nela.

> **Editor mostrando uma coisa e jogo rodando outra é pior que editor sem desenho.**

Hoje o quadrante selecionado no Map Helper manda nas duas coisas: no que o
`SectorManager` enxerga e no eixo que o desenho segue. Uma fonte, não duas que
podem discordar.

## Frente 4 — a dificuldade encolhe para três, e passa a mandar

O autor pediu para revisar os presets de IA e não lembrava para que serviam. A
migração estava na fase 1 de quatro: o asset existia, era gerado a partir dos
valores vivos da cena, e **nada no runtime lia dele**.

Duas descobertas ao abrir.

**A primeira é uma armadilha de nomes.** A Tela de Entrada oferecia três opções e
nenhuma tinha o nome que aparecia:

```text
o jogador via        o enum era              e o preset se chamava
FÁCIL           →    AIDifficulty.Iniciante  →  AIPreset_Facil
MÉDIO           →    AIDifficulty.Facil      ←  aqui
DIFÍCIL         →    AIDifficulty.Competitiva
```

Editar o perfil "fácil" mudaria o botão **MÉDIO**. O enum tinha seis valores e
três nunca foram oferecidos. Agora são três, com os nomes que o jogador lê, e
nenhum botão mudou de comportamento.

**A segunda é a que importa.** O autor descreveu o que queria dos perfis — *"a
difícil e a média partem pra frente; a fácil fica agarrada até terminar a
captura; a difícil respeita a lista banida, a média não"* — e isso era
**estruturalmente impossível**. As seis capacidades derivavam de `hardMode`: uma
chave só com seis lâmpadas. O próprio arquivo já admitia:

> *"Enquanto forem derivadas de hardMode, nenhum perfil pode ter uma sem ter
> todas."*

Então os portões voltaram a perguntar o que queriam:

| portão | perguntava | pergunta |
|---|---|---|
| blitzkrieg / handoff | `hardMode` | `FazHandoffEmProfundidade` |
| lista banida | `hardMode` | `RespeitaListaBanida` |
| abertura com blindado | `hardMode` | `AbreComBlindado` |
| teto de logística | `hardMode` | `LimitaLogistica` |
| dobro de capturadores | `hardMode` | `DobraSlotsDeCapturador` |
| projetar produção inimiga | `hardMode` | `ProjetaProducaoInimiga` |
| renda fora das cidades | `easyMode ? ⅓ : 1` | `FracaoRendaForaDeCidades` |

Sem preset ativo, cada uma responde exatamente o que o flag respondia — cena sem
asset continua idêntica, e a troca é rastreável portão a portão.

O modelo também inverteu. O código apostava em **um baseline + uma overlay de
dificuldade em código**; o autor quer **três perfis autorados**. Um catálogo liga
dificuldade → asset, e quando ele responde, a overlay não roda — ela é justamente
o que acende as capacidades em bloco.

---

## O que não terminou

**Os presets ainda não mandam em números.** Só as capacidades e a renda saíram do
`hardMode`. Os pares `normal/hard` de valor — `EliteRatio*`, `EliteSaveTurns`,
`CoreMin*` — continuam no `AIController` (fase 2 da migração). Enquanto isso, o
difícil e o médio compartilham a mesma tabela de números.

**O catálogo gerado antes da redução do enum está velho.** Ele guardou o difícil
como `dificuldade: 4`, que era o número de `Competitiva`. Hoje 4 não casa com
nada e o difícil **cai no caminho antigo sem dizer nada** — o modo de falha
silencioso de sempre. O botão de sincronizar agora remove entrada de dificuldade
extinta e avisa, mas **precisa ser clicado**.

**Nada disto foi jogado.** Compila, e compilar prova que os tipos batem. O
comportamento dos três perfis — e o blitzkrieg no médio, que é a primeira coisa
que passa a ser do autor e não do `hardMode` — só existe depois de o autor ligar o
catálogo no `AIController` e jogar.

**A ferramenta de eixo não é prática.** O veredito é do autor:

> *"não é prático pra dar manutenção, mas resolve, e é o que temos no momento"*

Montar o grafo prédio por prédio, sem ver a linha se formando, é o gargalo — e
melhorar o dropdown seria polir o gesto errado. O caminho, quando voltar à pauta,
é desenhar no Scene View: clicar o QG, clicar os setores na ordem.

**O `0b` continua sem teste.** Mapa A → turno 5 → menu → mapa B, para provar que a
IA do segundo mapa começa sem o plano do primeiro. É o último bloqueio do MVP que
ninguém exercitou.

---

## Armadilhas que esta versão pagou

**O recorte de autoria vaza para o arquivo da cena.** Não o recorte — o efeito
dele. O `SectorManager` serializa `sectorInfos`, e salvar a Fixture com um
quadrante em foco grava a lista **truncada**. Na autoria isso é cache e se refaz
sozinho; mas é a razão de a `Fixture.unity` desta versão ter perdido umas 600
linhas de distâncias, e não é regressão.

**Reassar não conserta autoria.** A lista de eixos mora fora da seção assada de
propósito, e o bake não a toca. Custou uma rodada de "mas eu reassei".

**Slot não é time, de novo.** A chave do Go Green era escrita com o slot e
removida com o número do time, no caminho da volta do save. Só acertava quando os
dois números calhavam de ser iguais; nos outros casos apagava a entrada de
ninguém, em silêncio.

**A supressão do Go Green exigia posse.** Ao acender, ela suprime os outros
rallies para nenhum feeder começar uma segunda montagem — mas o filtro pedia que
o prédio **já fosse meu**, enquanto a montagem só pede designação. O rally meu
ainda por conquistar, justamente o candidato a virar a segunda montagem, ficava
de fora.
