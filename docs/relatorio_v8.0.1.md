# v8.0.1 — As seis armas: o vocabulário fecha, o código não começou

> *"Incrível condensar 33 unidades, 10 sensores e uma pancada de possibilidades
> em 6 armas, né?"* — autor, 2026-08-06

Esta versão é **inteiramente doutrina**. Nenhuma linha de C# mudou. O autor
resumiu o dia sem cerimônia — *"agente mais bateu papo e ficou planejando"* — e o
número `Z` está certo por isso.

Mas o que se planejou é o pré-requisito do degrau 3 inteiro, que até ontem
estava em `❌` **sem vocabulário**: não havia como descrever o que um papel é,
nem como decidir se uma unidade pertence a ele.

---

## 1. O fio do dia

O dia produziu duas coisas de naturezas diferentes, e vale separar:

```text
o que FOI CONSTRUIDO   as seis fichas e as seis marchas
o que FOI DESCOBERTO   que os papeis sao DERIVAVEIS, e que eu tinha
                       carimbado tres verificacoes falsas
```

A segunda coluna vale mais, e as três descobertas vieram de **perguntas do
autor**, não de análise minha. Está registrado em §5 porque é o tipo de coisa que
a próxima sessão repete se não estiver escrita.

---

## 2. Frente A — as seis fichas

Cada papel ganhou ficha com a mesma forma: **ordem do questionário, moeda,
posicionamento, justificativa**.

| papel | doc | a moeda — *onde mora o valor da peça* | funde |
|---|---|---|---|
| Capturador | `Capturador.md` §0 | o **corpo** — HP **é** a taxa | **ganha** |
| Transportador | `Transporte.md` §0.1 | as **vagas** | perde |
| Assalto | `Assalto.md` | a **arma** — cada casco é ameaça | perde |
| Fogo de Suporte | `FireSupport.md` | a **formação** — cones cruzados | perde, e agrupar também |
| Vigilância | `Vigilancia.md` | a **origem do cone** | perde |
| Logística | `Logistica.md` | o **estoque** | **ganha** |

**A moeda não era um enfeite: ela decide sozinha se a peça funde.** Seis papéis,
seis vezes a pergunta *"onde mora o valor desta peça?"* respondeu `Fundir`
corretamente — inclusive nas duas em que a resposta contraria a intuição de HP.

### O achado que organiza as seis

**Logística é o espelho exato da Vigilância:**

```text
Vigilancia   Detectar 1º ... Enxergar 10º    vive de CONTATO
Logistica    Enxergar 1º ... Detectar 10º    vive de TERRENO
```

A `v7.1.0` gastou dias separando `PodeEnxergar` de `PodeDetectar`, e o relatório
dela chamou isso de *"o erro que custou o dia"*. **A melhor prova de que são
entidades diferentes apareceu sozinha:** existe um papel inteiro que só precisa de
um, e outro papel inteiro que só precisa do outro. A justificativa do autor para
o segundo é definitiva — *"não se preocupam em detectar o inimigo, pois se ele
está tão perto já é tarde demais"*.

### Renome de rótulo

`CapturadorAgressivo` → **`CapturadorCombatente`**. Motivo do autor: *"levei
'capturador agressivo' para diversas AI e todas acharam que ela captura
AVIDAMENTE"*. O valor do enum continua `= 12`; asset, cena e save não sentem.

---

## 3. Frente B — as seis marchas

O autor escreveu uma marcha por papel. **Elas são doutrina**, não ilustração — a
regra declarada em cada apêndice é *onde o código divergir de um verso, o código
está errado* — e cada uma foi conferida verso a verso contra a ficha.

Três versos entraram como formulação canônica, **melhores que as minhas**:

| verso | o que ele substituiu |
|---|---|
| *"cada casco é nova origem, cada origem, outro setor"* | eu tinha escrito que a moeda da Vigilância era *"a área coberta"*. Errado por um degrau: o valor é o **ponto de origem de um cone**, e fundir **apaga uma origem** |
| *"eu não sigo onde há combate. Sigo onde não há explicação"* | a heurística de patrulha inteira, que até então só existia em forma numérica no ledger de recência |
| *"não atendo só o mais ferido: atendo o mais decisivo"* | **regra que não existia em lugar nenhum.** A fila do supridor não é ordenada por dano |

### A Marcha da Logística faz algo que nenhuma outra faz

Ela **descreve os outros cinco papéis**. Escrita da posição de quem serve todo
mundo, seus versos são indexados por *o que cada papel perde parado*:

```text
"o Capturador sem corpo perde tempo e producao"     -> HP e a taxa
"a aeronave sem combustivel perde o ceu"            -> autonomia
"o canhao sem suas caixas ja nao fecha o corredor"  -> a formacao
"o blindado de elite pode segurar o agressor"       -> a arma
```

São as quatro moedas dos outros papéis. Juntas, formam a triagem — e a triagem
**não precisa de tabela nova: ela lê a moeda de quem pede**. Promovida a
`Logistica.md` §5.1.

---

## 4. Frente C — três correções de doutrina, todas vindas de perguntas do autor

### 4.1 O papel é DERIVÁVEL, não declarado

Pergunta: *"então o F-22 e o B-2 não são vigilância?"* Resposta do autor, que é
melhor que o argumento por moeda que eu tinha dado: **eles não têm visão
especializada**.

E isso já existe como predicado:

```csharp
// UnitData.cs:612
public bool HasStealthDetectionFor(Domain targetDomain, HeightLevel targetHeightLevel)
    => TryGetVisionException(...) && entry.detectUnitsWithFollowingSkills.Count > 0;
```

A distinção fina que o predicado já faz: **não é *"tem exceção de visão"*** — o
F-22 tem, e enxerga bem. É *"a exceção carrega **lista de detecção**"*.

```text
carregar AR Stealth  -> voce e a FECHADURA. nao muda seu papel.
listar   AR Stealth  -> voce e a CHAVE. ISSO e Vigilancia.
```

É a doutrina da chave aplicada à classificação de papel, e é o melhor tipo de
critério que este projeto aceita: **um campo da ficha**, não uma inferência.
Mesmo formato de *facção sem QG*, derivada de **não possuir**
`isPlayerHeadQuarter`.

**Consequência imediata:** a cláusula do furtivo aéreo estava no documento
errado. Descrevia a **presa** indo bombardear. Mudou de `Vigilancia.md` §5 para
`Assalto.md` §5.1.

### 4.2 A detecção é TOTAL dentro do cone — e é ela que gera a repulsa e o ledger

> *"EWACS e Radar Móvel detectam qualquer aeronave no range, inclusive as
> stealth. Ele vê tudo."*

Eu tinha escrito repulsa e ledger como **duas decisões de design lado a lado**.
Não são. A cadeia:

```text
1. dentro do cone a resposta e COMPLETA
2. logo, varrer duas vezes a MESMA area nao acrescenta nada
3. logo, valor novo so existe em area nao coberta ou coberta ha muito
4. logo, dois sensores juntos desperdicam um deles    -> REPELIR
5. logo, a unica variavel que sobra e a IDADE         -> LEDGER
```

**Se a detecção fosse probabilística, a doutrina seria oposta** — valeria
concentrar e varrer a mesma área até o acúmulo revelar. É porque é total que os
olhos se afastam.

Aviso anexado: a totalidade vale **dentro** do alcance e só. Fora dele o EWACS
não é fraco, é **cego**. A borda corta, não degrada — e uma pontuação suave na
borda parece "mais realista" e inverteria o comportamento inteiro.

### 4.3 O preço do tiro furtivo — e para o submarino ele é de outra natureza

O `X rodadas` que era placeholder virou número: **1 rodada** para o furtivo
aéreo, **2** para o submarino. Mas o achado foi o terceiro item — *"e o sub ainda
emerge"*:

```text
furtivo aereo   perde o ATRIBUTO (stealth)   e MANTEM a camada
submarino       perde a CAMADA               e ali o atributo nem importa
```

O caça revelado continua em `Air/High`: quem quiser alcançá-lo ainda precisa de
arma que suba. O submarino que atira **sobe**, e na superfície ninguém pergunta
*"tenho sonar?"*. **A furtividade dele não foi vencida — foi abandonada.**

E isso reescreve a decisão de ataque:

```text
errado   "esse alvo vale o tiro?"
certo    "esse alvo vale o tiro E eu sobrevivo a 2 rodadas na superficie AQUI?"
```

A segunda pergunta é sobre a **célula**, não sobre a presa. **É a única decisão
de ataque do projeto em que o custo é pago no terreno e não no alvo.**

---

## 5. O que eu errei — três ✅ falsos, e todos pela mesma mecânica

Esta seção é a mais útil do relatório.

### 5.1 Verificar contra doc torto produz eco, não verificação

Carimbei a Ponte da Marcha da Vigilância (*"atacar revela o caçador"*) contra
`Vigilancia.md` §5. A §5 era **justamente** a cláusula que estava no documento
errado. Os dois erros se cancelaram num ✅.

> **Conferir coerência não é conferir correção.** Quando a referência está
> errada, o ✅ que ela produz é o resultado mais perigoso possível: parece
> verificação e é eco.

### 5.2 O descompasso de generalidade ERA a evidência

E o sinal estava à vista desde a primeira conferência:

```text
verso      "SE eu sou furtivo..."        condicional — cobre 2 ramos
clausula   "unidades furtivas AEREAS"    cobre 1
```

Errei **duas vezes seguidas estreitando o verso** — primeiro *"vale pelo aéreo"*,
depois *"vale pelo submarino"* — antes do autor apontar o óbvio: *"apesar da
canção estar errada, ela não está errada afinal — furtivos só atacam em
vantagem"*. O verso era o **invariante dos dois**.

> Quando o texto novo cobre **mais casos** que a regra contra a qual você o
> confere, o descompasso **é a evidência**: a regra é que está incompleta.

Daí a divisão que ficou, e que provavelmente vale para o resto do projeto:

```text
marcha   o INVARIANTE — o que os ramos compartilham
ficha    o PARAMETRO  — 1 rodada / 2 rodadas + emerge
```

**É por isso que as marchas envelhecem melhor que as seções.**

### 5.3 Causa escrita depois dos efeitos = relação entre efeitos chutada

Escrevi que a repulsa dos radares era consequência do ledger. Falso — as duas são
consequência da detecção total (§4.2), e nenhuma gera a outra. Só apareceu quando
o autor declarou a causa, **depois** dos dois efeitos já estarem documentados.

> Quando dois fatos aparecem juntos e um parece explicar o outro, desconfie de
> que **ainda falta o terceiro**, que explica os dois.

---

## 6. Frente D — os brasões

`Assets/img/logo/armas.png`: seis brasões, um por arma. **Cinco dos seis
desenharam a moeda, não a máquina** — a chave na porta do prédio, os canhões
cruzados, o olho-radar (olho cheio = detecção, a própria convenção de arte dos
overlays), galão/caixa/peça.

⚠️ **Uma lacuna registrada:** o brasão do Assalto é 100% terrestre, e Assalto tem
três ramos — dois deles aéreos (Interceptador e Ataque Aéreo). Do jeito que está,
o brasão **reforça exatamente o engano que abriu a §4.1**.

Notas de uso, se virarem ícone de HUD: Capturador e Vigilância compartilham o
mesmo verde escuro (e são justamente os dois que mais aparecem juntos, porque o
EWACS usa o capturador como ímã); Assalto e Fogo de Suporte não sobrevivem ao
teste de silhueta.

---

## 7. O que NÃO terminou

### A fatia 1 continua sem rodar — e isso já atravessou duas versões

O commit `3e0565d` (v8.0.0) unificou o alvo de captura na missão.
**Ele compila nas duas assemblies e nunca rodou.** O teste é pequeno: dois F11 e
um save/close/open no `Hot Seat 0 - Treino`, esperando comportamento **idêntico**.

Enquanto ele não rodar, `AIPlanRuntimeIntent.Capture` tem quem o escreva mas
ninguém confirmou que a leitura sobreviveu.

### Nada da doutrina virou código

As 33 unidades continuam tirando comportamento de ~20 arquivos parciais cheios de
exceção nomeada — *ponta de lança, handover, sai do meu prédio, ceder para o
capturador x*. Cada uma nasceu resolvendo um problema real com `2 hexes`
hardcoded, numa época sem tático/operacional.

**A compressão só se prova quando essas exceções forem re-derivadas de
`(papel, modalidade, banda, âncora)`.** As que não forem viram política explícita
no `Match/AI/Service/Capture_Policy` proposto pelo autor — o que também é
vitória, desde que seja escolha declarada e não resíduo.

### Itens marcados ❌ ou ❓ nas fichas de hoje

| item | onde | estado |
|---|---|---|
| ledger de recência de cobertura | `contrato_recencia_de_cobertura.md` | ❌ não existe |
| repulsa da vigilância aérea | `Vigilancia.md` §4.1 | ❌ não existe |
| âncora anti-sub (leito, canais) | `Vigilancia.md` §4.2 | ❌ não existe |
| 1 rodada / 2 rodadas de revelação | `Assalto.md` §5.1 | ❌ não está na IA |
| tiro do sub usa o caminho da emersão forçada? | `Vigilancia.md` §5.1 | ❓ não conferido |
| triagem por moeda | `Logistica.md` §5.1 | ❌ regra nova, sem código |
| IA não opera a cadeia de transferência | `Logistica.md` §3.2 | ⚠️ frente própria |

### Herdado da v8.0.0, intocado

- limpar a origem das rotas: seção do `StructureDatabase`, depois
  `StructureData.roadRoutes`, por último `RoadRouteDefinition.ownerDatabase`
- tirar `fieldEntries` do `ConstructionDatabase` (zero leitores em runtime)
- `ObjectiveManager` é `DontDestroyOnLoad` **sem** hook de `sceneLoaded`
- `[FoW][RoundZeroBake] restored=1/2` — um slot rejeitado, sem log de validação
- `AIController.Capturer.Agressive.cs` ainda com o nome antigo de arquivo

---

## 8. Os números

```text
6 papeis        questionario, moeda, posicionamento
6 marchas       o conjunto esta COMPLETO
3 modalidades   combatente, artilheiro, hibrida
17 rotulos      o que o shopping pede e a ficha declara
10 sensores     as mesmas dez perguntas para todos
7 ordenacoes    o Transportador precisou de duas: Pickup e Courier
─────────────────────────────────────────────────────────────
0 linhas de C#
```

**O que comprimiu não foram os papéis — foi o questionário ser fixo.** Se cada
papel tivesse a sua lista de perguntas, não haveria economia nenhuma: seria o
mesmo caos com nomes bonitos. A economia existe porque a resposta virou uma
**permutação de um conjunto fixo**. O papel responde a **ordem**; a ficha responde
a **capacidade**.

E `6` só fechou porque existem dois outros eixos. O **Artilheiro Combatente não
coube** — foi ele que forçou a modalidade híbrida a existir. Sem esse segundo
eixo, o sexto papel viraria o sétimo e depois o oitavo.

> Quando aparecer a próxima unidade que não encaixa, a pergunta certa é
> **"que eixo falta?"**, não *"que papel falta?"*.
