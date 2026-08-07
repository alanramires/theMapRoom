# Vigilância — doutrina

Doutrina definida pelo autor em 2026-08-06. Onde o código divergir dela, o código
está errado.

> **Tem uma guerra acontecendo em algum lugar e eu não me importo, contanto que eu
> ache minha presa.**

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge do que está escrito aqui |
| ❌ | não existe no código |
| ❓ | não conferido |

**Subpapéis:** Vigilância Aérea, Anti-Sub.

---

## 0. Quem é Vigilância — o teste é VISÃO ESPECIALIZADA, e ele já existe

> *"O F-22 e o B-2 são Interceptador e Ataque Aéreo, **porque não têm visão
> especializada**."* — autor, 2026-08-06

**O papel é derivável, não declarado.** O predicado já está no código:

```csharp
// UnitData.cs:612
public bool HasStealthDetectionFor(Domain targetDomain, HeightLevel targetHeightLevel)
    => TryGetVisionException(targetDomain, targetHeightLevel, out UnitVisionException entry)
       && entry != null
       && entry.detectUnitsWithFollowingSkills != null
       && entry.detectUnitsWithFollowingSkills.Count > 0;
```

Visão especializada é uma `UnitVisionException` **que carrega lista de
detecção** (`UnitData.cs:113`). Não basta ter exceção de visão — o F-22 tem, e
enxerga bem. O que decide é a **lista estar preenchida**.

✅ `VisionCoverageService.cs:122` já escolhe candidatos por essa mesma forma.

### Por que este teste, e não a moeda ou o comportamento

```text
carregar AR Stealth   -> voce e a FECHADURA. Nao muda seu papel.
listar AR Stealth     -> voce e a CHAVE. ISSO e Vigilancia.
```

É a doutrina da chave (`CLAUDE.md`) aplicada à classificação de papel, e é o
melhor tipo de critério que este projeto aceita: **um campo da ficha**, não uma
inferência de doutrina. Renomeie a skill para *"sai que isso é meu"* e o teste
continua respondendo certo.

| unidade | tem exceção de visão | lista de detecção | papel |
|---|---|---|---|
| EWACS, Radar Móvel | sim | **preenchida** | **Vigilância** |
| Super Tucano, Fragata ASW, submarino | sim | **preenchida** | **Vigilância** |
| **Caça F-22** | sim | vazia | **Assalto** (Interceptador) |
| **Bombardeiro B-2** | sim | vazia | **Ataque Aéreo** |

O F-22 e o B-2 são **a presa deste papel** — §3. A Vigilância existe por causa
deles.

⚠️ Precedente do mesmo formato: *facção sem QG* é derivada de **não possuir**
`isPlayerHeadQuarter`, não de um booleano `isRebel`.

---

## 1. A prioridade

```text
Detectar, Mirar, Reposicionar, Suprir, Transferir,
Desembarcar, Embarcar, Capturar, Fundir, Enxergar
```

**`Detectar` em 1º e `Enxergar` em 10º — o último.** É a separação mais extrema das duas
verdades em todo o projeto: este papel vive de **contato** e é praticamente
indiferente a **hexágono**.

> *"Não ligam pra mergulhar na névoa preta — detectar é mais importante do que
> saber o que está ali."*

**`Mirar` em 2º, mas só para quem pode agir na mesma rodada.** EWACS e Radar
Móvel **pulam** o sensor.

**Modalidade híbrida.** Todas as unidades de vigilância seguem a linha do
Artilheiro Combatente: **primeiro fogo de suporte, depois assalto**. É o terceiro
usuário da modalidade (ver `ficha_do_papel.md` §7.8).

**`Embarcar` logo depois de `Desembarcar`** — o par anda junto, e existe de
verdade: EWACS e Super Tucano em porta-aviões. Abaixo dele ficam as três casas
fracas deste papel: `Capturar`, `Fundir` e `Enxergar`.

---

## 2. Detectar ≠ Enxergar — e aqui a diferença é REGRA DE JOGO

```text
detecção de vigilância    6+ hexes, MESMO sobre hexes não descobertos
visão tradicional         1 a 4 hexes
```

A unidade detectada aparece **desfocada por cima da névoa**: o jogador sabe
**onde ela está**, mas não o que está embaixo nem **quem mais** está embaixo.

É o quadrante *"contato detectado + hex preto"* da doutrina das duas verdades
(`CLAUDE.md`), agora com consequência visual declarada.

**Quem faz isso:** EWACS e Radar Móvel (vigilância aérea) procurando F-22 e B-2;
Super Tucano (patrulha naval), Fragata ASW e o **próprio submarino** procurando
outros submarinos.

---

## 3. Chave e fechadura

A doutrina da chave, literal — o **alvo** carrega a etiqueta, o **caçador** lista
qual etiqueta enxerga:

| quem se esconde | etiqueta | quem acha | lista |
|---|---|---|---|
| caças e bombardeiros | `AR Stealth` | sensores | `Aeronave Furtiva` |
| submarinos | `Sub Ops` | sonoboias | `Detect Sub` |

✅ O maquinário existe: `UnitStealthSkillRule` no lado de quem esconde e
`detectUnitsWithFollowingSkills` no lado de quem procura.

---

## 4. Posicionamento — e ele bifurca por subpapel

**Não há um posicionamento do papel.** Há dois, opostos.

### 4.1 Vigilância aérea — REPELE, e a cobertura envelhece

> *"As unidades de vigilância aérea se repelem no tabuleiro a fim de ocupar a
> maior área detectável, que vai **degradando com o tempo**, forçando o
> patrulhamento. Dois radares móveis comprados juntos: um vai para o norte, outro
> para o sul."*

Vale entre **Radar↔Radar, EWACS↔EWACS e EWACS↔Radar**.

**A degradação é o ledger de idade** de `contrato_recencia_de_cobertura.md` —
desenhado no mesmo dia, e aqui declarado como **regra de posicionamento** e não
só como serviço. ❌ Não existe no código.

### 4.2 Anti-sub — AGRUPA, e a âncora é o leito

> *"As unidades anti-sub andam em grupos, ao contrário da vigilância aérea. O que
> as atrai é **o leito do fundo do oceano ou dos canais**. Elas não se importam
> com a guerra que acontece na superfície — é possível ver 2 subs e 1 fragata
> navegando juntos. O oponente vai com 1 sub achando que é uma presa e não faz
> ideia que tem um Super Tucano por perto."*

✅ **Fecha o `ABERTO` do `contrato_recencia_de_cobertura.md` §8**: a âncora da
patrulha naval é o **corredor** (leito, canais), com a idade por cima.

### 4.3 Magnético — só quem é conservador tem

> *"Algumas unidades têm `play conservative` e outras não. As que têm procuram a
> retaguarda como um fogo de suporte faria. **As que não têm não têm magnético** —
> só se forem alocadas em algum plano."*

✅ Confere com o censo do turno 1: EWACS e Radar Móvel têm `playConservative`;
submarino, fragata e Super Tucano não. E o EWACS usa `FollowMagnet` com o
capturador como ímã.

⚠️ **Confundimento ao testar:** hoje *"é aérea?"* e *"tem playConservative?"* dão a
mesma resposta para toda unidade de vigilância. Política construída sobre o flag
passa pelo motivo errado.

---

## 5. Ataque em vantagem — só o caso NAVAL mora aqui

> **A cláusula aérea saiu deste documento em 2026-08-06.** Ela descrevia
> *"unidades furtivas aéreas ignorando combates no caminho **até seus
> objetivos**"* — isso é a **presa** indo bombardear, não o caçador. Mudou para
> `Assalto.md` §5.1, junto com a regra do F-22 de `deteccao e caca.md` §10.1.
> O §0 explica por quê: quem carrega `AR Stealth` é fechadura, não chave.

**O que sobrevive aqui é o submarino** — e sobrevive porque ele é o único que é
furtivo **e** caçador ao mesmo tempo (§2: *"o próprio submarino procurando outros
submarinos"*). Para ele, atirar revela quem estava caçando.

É o que a Ponte da Marcha diz — *"não disparo por vaidade"*. A Ponte está certa
**pelo motivo naval**, não pelo aéreo.

❓ Não conferido. Existe precedente na mecânica de emersão forçada do submarino
(lock pendente, tempo revelado) — ver `project_pending_forced_layer`.

---

## 6. A moeda: a área coberta, e a idade dela

> *"Não se fundem — **menos cobertura do tabuleiro**."*

| papel | onde o valor mora | fundir |
|---|---|---|
| Capturador | o corpo — HP é a taxa | **ganha** |
| Transportador | as vagas | perde |
| Assalto | a arma | perde |
| Fogo de Suporte | a formação (cones cruzados) | perde, e agrupar também |
| **Vigilância** | **a área coberta e a idade dela** | perde |

A quinta moeda, e a única com **duas geometrias opostas para o mesmo valor**:
espalhar maximiza área (aérea); agrupar protege quem caça a presa (anti-sub).

**A formulação exata está na Marcha** (apêndice), e é melhor que a desta seção:

> *Cada casco é nova origem, / cada origem, outro setor;*
> *dois caçadores separados / valem mais que um só melhor.*

O valor não é a **unidade** — é o **ponto de origem de um cone**. Fundir dois
sensores avariados não soma cobertura: **apaga uma origem**. Por isso *"dois
sensores avariados ainda cobrem dois lugares"* é argumento suficiente contra a
fusão, mesmo quando o HP diz o contrário.

---

## 7. O resto quase não acontece

**Suprir e Transferir** acontecem de verdade: suprem **embarcados** e obtêm
estoque — *"a Fragata transporta Apaches"* — e os **liberam quando curados**
(desembarque). É o modo Hospital do transporte, aqui como função secundária.

**Raramente capturam.** E embarcar é caso raro — mas quando acontece, o par
`Desembarcar`/`Embarcar` anda junto, acima das três casas fracas.

---

## 8. Leituras

| documento | por quê |
|---|---|
| `contrato_recencia_de_cobertura.md` | o ledger de idade, a bifurcação aérea × naval, e o valor de N ainda ABERTO |
| `ficha_do_papel.md` §7.8 | o quadro canônico dos papéis e as três modalidades |
| `CLAUDE.md`, "As duas verdades" | por que `Detectar` em 1º e `Enxergar` em 10º não é contradição |
| `FireSupport.md` | a modalidade híbrida, que este papel também usa |

---

# Apêndice — Marcha da Vigilância

Escrita pelo autor em 2026-08-06. **Ela é a doutrina**, e vale a regra do
cabeçalho: **onde o código divergir de um verso, o código está errado.**

Quinta marcha do projeto, e a **única que não se compara a outro papel** — ela se
compara à guerra. *"A guerra que espere."* Coerente: é o único papel que se
define contra o jogo inteiro em vez de contra um vizinho.

| verso | o que ele fixa |
|---|---|
| *"não preciso ver o terreno / para o alvo revelar"* | `Detectar` 1º e `Enxergar` 10º, literal |
| *"não conheço o chão abaixo, / mas já sei onde você está"* | a regra de jogo do §2 — desfocado por cima da névoa |
| *"cobertura envelhecida / já não vale proteção"* | o **ledger de idade**, e a repulsa como **consequência** dele, não como regra separada |
| *"o canal atrai a matilha, / o fundo dita a direção"* | a âncora anti-sub decidida no mesmo dia (§4.2) |
| *"atacar revela o caçador"* | §5, e a mesma doutrina do Caça F em `deteccao e caca.md` §10.1 |
| *"cada casco é nova origem, / cada origem, outro setor"* | **a moeda**, melhor dita do que na §6: o valor não é a unidade, é o **ponto de origem de um cone** |

> ## Eu não sigo onde há combate. Sigo onde não há explicação.

A heurística de patrulha inteira em oito palavras: **vá onde a cobertura
envelheceu** — que é, por definição, onde não se sabe de nada.

---

**[Introdução — metais graves, pulso lento]**

Silêncio no rádio... / Atenção ao setor...

*Ping...*

Nenhum contato.

*Ping...*

A presa se moveu.

**[Estrofe 1]**

Há uma guerra ao longe, / há fumaça sobre o chão; / mas não sigo o estampido, / nem a voz do capitão.

Tenho olhos para o invisível, / tenho a chave da prisão; / onde todos veem o vazio, / eu procuro a posição.

Não me importa a grande batalha, / nem quem vence a progressão; / se minha presa está oculta, / ela será minha missão.

**[Refrão]**

Ping... / Escuta... / Marca o contato!

Ping... / Procura... / Fecha o espaço!

A guerra que espere, / eu não sigo a multidão: / eu encontro o que se esconde / fora de toda visão!

Radar no céu! / Sonar no mar! / A presa pode fugir, / mas não pode se apagar!

**[Estrofe 2 — detectar]**

Detectar é minha ordem, / antes mesmo de atacar; / não preciso ver o terreno / para o alvo revelar.

Sobre a névoa ainda fechada, / um sinal começa a arder; / não conheço o chão abaixo, / mas já sei onde você está.

Furtivo cruza o céu escuro, / submarino o fundo do mar; / cada presa tem fechadura, / cada sensor sabe encontrar.

**[Refrão]**

Ping... / Escuta... / Marca o contato!

Ping... / Procura... / Fecha o espaço!

Radar no céu! / Sonar no mar! / O invisível tem um nome / quando eu começo a caçar!

**[Estrofe 3 — vigilância aérea]**

Dois radares lado a lado / veem o mesmo corredor; / um vai ao norte, outro ao sul, / cada qual abre um setor.

EWACS gira sobre as nuvens, / Radar Móvel muda o chão; / cobertura envelhecida / já não vale proteção.

O espaço antes patrulhado / pode ocultar nova invasão; / por isso os olhos se afastam / e renovam a detecção.

Não concentro meus sensores, / quero a rede se estender: / quanto maior for o silêncio, / mais terreno há para varrer.

**[Ponte — furtividade]**

Se eu sou furtivo e vejo a presa, / não disparo por vaidade; / atacar revela o caçador / e entrega minha identidade.

Só abandono o esconderijo / com vantagem ou precisão; / não troco a minha sombra / por qualquer oportunidade.

Vejo. / Espero. / Calculo a ocasião.

Então a noite se ilumina / com um único clarão.

**[Estrofe 4 — caça submarina]**

Sob as ondas é diferente: / não disperso a formação; / o canal atrai a matilha, / o fundo dita a direção.

Duas sombras submarinas, / uma Fragata ASW; / e no alto um Super Tucano / que você jamais previu.

Você chega acreditando / ter encontrado uma presa só; / mas o sonar chama os outros / e a armadilha fecha o nó.

Na superfície há uma guerra? / Que prossiga sem meu olhar. / Minha batalha está no fundo, / onde ninguém pode enxergar.

**[Refrão forte]**

Ping... / Contato! / Todos em posição!

Ping... / Contato! / Fechem a direção!

A guerra que espere, / minha presa apareceu; / o que antes era invisível / agora o exército já viu!

Radar no céu! / Sonar no mar! / Depois que eu encontro a presa, / o resto vem para matar!

**[Estrofe 5 — posicionamento]**

Se carrego o modo prudente, / vou atrás da formação; / preservo o olho que enxerga / sem aceitar aproximação.

Mas sem ordem e sem cautela, / não me prende o capitão; / sigo apenas os caminhos / da provável infiltração.

Não procuro a linha aliada, / não me chama a construção; / só um plano pode dar-me / outro eixo de patrulhamento.

**Eu não sigo onde há combate. / Sigo onde não há explicação.**

**[Estrofe 6 — serviço e fusão]**

Fragata cuida dos Apaches, / dá estoque e manutenção; / quando estão prontos para a caça, / abre o convés para a missão.

Posso embarcar em outra base / para mudar de operação; / mas não abandono a busca / por conforto ou distração.

Dois sensores avariados / ainda cobrem dois lugares; / não me funda, não apague / um dos olhos dos radares.

Cada casco é nova origem, / cada origem, outro setor; / dois caçadores separados / valem mais que um só melhor.

**[Chamada e resposta]**

— Quem vê além da névoa? / — A Vigilância!

— Quem encontra o furtivo? / — A Vigilância!

— Quem escuta sob as ondas? / — A Vigilância!

— E quando a presa aparece? / — Contato confirmado!

**[Refrão final]**

Ping... / Escuta... / Algo está passando.

Ping... / Procura... / O sinal está voltando.

A guerra que espere, / não me importa a confusão: / eu encontro o que se esconde / e entrego sua posição!

Radar no céu! / Sonar no mar! / Pode correr na escuridão, / pode tentar se camuflar!

Eu não sigo a guerra. / Eu sigo o que ela não viu.

E quando encontro minha presa...

o invisível já caiu.

**[Coda — quase em silêncio]**

*Ping...*

Contato.

Coordenada confirmada.

**Agora chamem as armas.**
