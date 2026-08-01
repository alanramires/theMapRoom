# Governança entre papéis — as arestas

Os contratos desta pasta descrevem **papéis**. O `governanca.md` descreve o que
vale **acima** de todos eles — as duas ordens e os sensores. Este descreve o que
existe **entre** eles.

Nasceu de uma observação do autor, escrevendo o quarto contrato:

> *"cada papel lê todos os sensores, mas de maneira diferente, e alguns governam
> como outros papéis se comportam"*

A primeira metade dessa frase está respondida em *Papéis da IA*, no `governanca.md`: papel é
uma consulta, não um conjunto. Este documento é a segunda metade.

---

## 1. Três tipos de governo

"Governar" não é uma coisa só. São três, e confundi-las é o que produz o mesmo
bug repetido.

| tipo | governa | mecanismo |
|---|---|---|
| **magnético** | o **onde** | o papel A vira a **âncora** do papel B |
| **por agenda** | o **para quê** | o papel A **adota o objetivo** do papel B |
| **por exclusão** | o **onde não** | o papel A é definido por onde o papel B **está** |

### 1.1 Magnético — governa o onde

O papel governado orbita o governante. Não precisa ficar colado: fica entre 1 e
o **Tactical do capitão**. **Contrato completo na §2 deste documento.**

### 1.2 Por agenda — governa o para quê

O papel governado **não tem objetivo próprio**: adota o de quem ele serve.

O transporte é o caso puro. *"O passageiro mais antigo assume o volante e leva o
transportador até onde quer ir."* O destino do veículo **é** o destino da carga —
e por isso o `AIDesignatedMission` dele deveria herdar o objetivo do passageiro
(pendência T11 do `Transporte.md`).

É o único papel governado por **todos** os outros. Isso não é acidente de
implementação, é a definição da função dele: alavancar.

### 1.3 Por exclusão — governa o onde não

O papel governado é definido, em parte, por uma região **proibida**, e essa
região é a posição de outro papel.

Fire Support não pode estar na vanguarda. Mas "vanguarda" não é uma constante do
mapa: é **onde o assalto está**. Mover o assalto move a proibição do fire
support, sem ninguém tocar em fire support.

O mesmo vale, invertido, para o transporte: ele não pode largar âncora em cima de
capturável porque essa célula é do capturador (§6.5 do `Transporte.md`).

---

## 2. Comportamento Magnético — o contrato

Contrato do autor.

| marca | significado |
|---|---|
| ✅ | implementado e conferido |
| ⚠️ | existe, mas diverge |
| ❌ | não existe no código |
| ❓ | não conferido |

O comportamento Magnético determina **qual referência espacial** cada unidade
procura acompanhar. A unidade **não recebe uma posição fixa**: é atraída por um
líder, objetivo ou necessidade compatível com o papel dela.

> ✅ "Magnético" já é palavra do código, não só do contrato:
> `AIController.Backline.cs` tem `TryResolveFireSupportMagnet`, que devolve um
> `leader` e um `magnetKind`.

### 2.1 Quem é o Capitão

**Unidade sem plano** escolhe uma referência próxima para seguir — normalmente um
Capturador eleito **Capitão**.

| regra | estado |
|---|---|
| capitão destruído → procura outro Capturador próximo | ✅ o capitão é resolvido a cada decisão, não guardado |
| capitão embarca → procura temporariamente outro Capitão | ❌ nenhum código trata capitão embarcado |
| acompanhar Capitão **embarcado** | ❓ *"ainda precisa ser definido"* — palavras do autor |

**Unidade com plano** viaja até o setor designado. Ao chegar:

1. procura um Capturador no setor para eleger como Capitão;
2. não havendo, usa a **`RepCell`** do setor como referência.

> A `RepCell` funciona como um **Capitão abstrato** até que uma liderança real
> esteja disponível.

✅ `RepresentativeCell` é usada como âncora em 43 sítios da IA. ⚠️ Mas ver a
pendência T3 do `Transporte.md`: em setor já capturado a RepCell coincide com a
célula do próprio veículo, e o resultado é uma entrega de distância zero. Capitão
abstrato de setor já conquistado é um capitão que não lidera para lugar nenhum.

### 2.2 Atração dos papéis principais

| papel | é atraído por | estado |
|---|---|---|
| **Capturador** | construções capturáveis; construções aliadas **sob captura ou ataque** | ✅ |
| **Assalto** | Capturadores próximos — um vira Capitão. Procura posições de **Vanguarda** e **Flanco** | ✅ `Assault.cs:213` resolve o capitão e loga o id |
| **Fire Support** | Capturadores próximos — o escolhido vira Capitão. Posiciona-se **dentro do envelope da formação**, na região de apoio de fogo | ✅ `Backline.cs:163` — `magnetKind = "CapturerMagnet"` |
| **Transportador** | unidades que querem alcançar objetivos **além da própria Banda Operacional**. Algumas pedem transporte mesmo dentro dela, conforme papel, autonomia ou modalidade | ✅ ver `Transporte.md` §5 |
| **Logística** | 1. unidades em estado **crítico**; 2. manutenção **preventiva**; 3. **Capitão**, sem atendimento prioritário | ❓ a ordem não foi conferida |
| **Vigilância** | áreas ainda **em névoa**; **Capitão**, sem setor prioritário | ✅ `VigilanciaAerea.cs:305` resolve capitão |

**Vigilância não deve necessariamente usar todo o movimento.** Para evitar avançar
sobre forças inimigas ainda não detectadas, pode limitar o deslocamento a uma
fração da Banda Tática — por exemplo **Tático ÷ 2**. ❌ não existe; e o próprio
autor marca como *"ainda em avaliação"*.

> Essa é a única regra do contrato que **encolhe** uma banda em vez de escolher
> dentro dela. Vale notar o formato: não é um número de hexes, é uma fração da
> banda da unidade — continua honrando *"banda é sempre parâmetro da unidade
> avaliada"*.

### 2.3 Atração dos papéis secundários

| papel | é atraído por | estado |
|---|---|---|
| **Capturador Agressivo** | as mesmas referências do Capturador. A diferença é **local**: tende a atacar antes de continuar a captura | ✅ |
| **Interceptador** | unidades de **Vigilância Aérea**; Capitão. Acompanha a mais próxima ou mais relevante | ❓ |
| **Ataque Aéreo** | **Interceptador**; Capitão, quando não há Interceptador adequado | ❓ |
| **Artilheiro Combatente** | Capitão, acompanhando a **Vanguarda**. É principalmente Assalto, mas tenta primeiro as armas de longo alcance; sem solução de tiro, avança e combate por contato | ✅ coerente com `UnitBattleParticipation.Direct` |
| **Antiaéreo Combatente** | aeronaves inimigas **detectadas**; Capitão, sem ameaça aérea prioritária | ❓ |
| **Antiaéreo** | **Vigilância Aérea**; Capitão. A Vigilância dá informação e orientação, o Capitão mantém a unidade integrada à formação | ✅ `Backline.cs:143` — o magnete de radar móvel existe, e o ramo testa `UnitRole.Antiaereo` |
| **Estoque** | construções aliadas **sem recursos**; unidades supridoras; Capitão, sem demanda logística prioritária | ✅ `Stock.cs:426` — `TryResolveStockRearCaptain` |

⚠️ **O magnético naval não aparece nesta lista.** O `Assalto.md` (item M3) fixa
que o capitão da marinha é o **Assalto marítimo** (`isMaritime`), e que hoje
fragata e submarino seguem o capitão **terrestre** por hardcode, para o jogo de
testes continuar rodando. Ou entra aqui, ou o M3 fica sem contrato que o
sustente.

### 2.4 Princípio Magnético

Cada papel tem uma **referência preferencial**. A unidade se desloca em direção a
ela até entrar na região adequada para exercer o papel.

O Magnetismo **não escolhe um hexágono exato**. Ele define:

- **quem ou o quê** a unidade acompanha;
- **em qual direção** deve progredir;
- **qual Hotzone** deve procurar;
- **qual região da formação** deve ocupar.

A posição final é escolhida pelo **serviço responsável**, considerando Hotzones,
Vanguarda, Retaguarda, Flancos, caminhos válidos, segurança e utilidade para o
papel.

> Assim, o Capitão organiza a formação **sem controlar diretamente cada unidade**.

Esta seção é a doutrina das três camadas dita em vocabulário militar. O magnete é
**organizador** — decide quem seguir e que região ocupar. A Hotzone é **serviço
burro** — devolve a área. Entre os dois entra o **consumidor**, que cruza a área
com a região da formação e devolve a célula. O magnete nunca aponta um hex, e a
Hotzone nunca escolhe um: cada camada faz só o que lhe cabe.

E é o que sustenta a frase final: uma formação que emerge de N unidades
perguntando *"onde está meu capitão e que região me cabe?"* não precisa de
ninguém comandando posição por posição.

---

## 3. Por que isso importa: ordem de refactor

**As arestas determinam a ordem, e a ordem não é negociável.**

O caso que já foi esbarrado, e que motivou este documento:

> A lógica de camada nativa do submarino mora **dentro** do fluxo de perseguir o
> capitão. O M3 remove esse fluxo. Fazer M4 antes de M3 apagaria a camada nativa
> junto; fazer M3 antes de M4b apagaria sem ter onde recolocá-la. Daí a ordem
> obrigatória **M4b → M3 → M4**.

Ninguém deduziu isso do plano. Descobriu-se batendo. Com o grafo escrito, a
regra é mecânica:

> **Antes de arrancar um fluxo de governo, verifique o que está morando dentro
> dele.** Um fluxo magnético é um lugar tentador para pendurar lógica que não tem
> nada a ver com o capitão — e ela morre junto quando o fluxo sai.

---

## 4. O bug que este documento explica

Os quatro contratos produziram tabelas de pendência que **rimam**. Sempre uma
destas três formas:

| forma | exemplos |
|---|---|
| **número fixo onde devia ter banda** | `TransportDropOffRange = 4`, `FireSupportDropOffRange = 3`, `ShuttlePickupRange = 2`, `AirDropOffRange = 2` |
| **âncora congelada onde devia ter parâmetro** | o funil do QG no capturador rogue — derrubado na v6.1.2/6.1.3 e **ainda vivo** na sua cópia dentro do transporte (T2) |
| **cópia paralela onde devia ter parâmetro** | `Rebel.cs` como espelho do capturador; e a segunda cópia do mesmo funil no desembarque |

Não são quatro problemas. É um, aparecendo quatro vezes — e é o que a regra
geradora do `CLAUDE.md` já dizia antes de existirem os casos:

> *banda, âncora e camada são sempre parâmetro da unidade avaliada — nunca
> constante do papel.*

Este documento acrescenta a razão de a violação ser **tão fácil de cometer**:
quando o papel A governa o papel B, a tentação é escrever em B a constante que
descreve A hoje, em vez de perguntar a A. Funciona, e continua funcionando até A
mudar — aí B fica com a doutrina velha, e ninguém percebe, porque B compila e
roda.

---

## 5. Aviso de método

Contrato escreve mais rápido do que código implementa. Quatro contratos já
produziram dezenas de pendências, e boa parte é doutrina do zero.

Uma lista grande, organizada e marcada **parece progresso**. O antídoto é o ritmo
que o autor já usava por instinto: **uma classe por vez — mexe, compila, roda no
jogo, comita antes da próxima.** Não emenda fases.
